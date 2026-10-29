using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastFortress
{
    /// <summary>
    /// Third-person perspective camera for the hero view (game rules section 17, specs/003 section 6). A separate camera from
    /// the strategic <see cref="CameraRig"/>, enabled instead of it. Mouse look orbits behind the hero; the wheel sets the
    /// distance. Obstacles (what is drawn for buildings, trees and boulders, and the ground) first pull the camera in; only
    /// when even the close pose cannot clear them does it lift, by at most 15 degrees. Every shown pose, transitions
    /// included, keeps its near-plane volume clear or takes a documented fallback. It never touches the simulation.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HeroCameraRig : MonoBehaviour
    {
        public const float MinPitch = -10f, MaxPitch = 60f, MinDistance = 0.5f, MaxDistance = 2.2f;
        public const float TransitionSeconds = 0.35f;

        /// <summary>Degrees of turn per pixel of mouse movement at sensitivity 1.</summary>
        const float LookDegreesPerPixel = 0.12f;

        /// <summary>
        /// Pivot above the hero's feet, and the over-the-shoulder offset to the right: sized for the hero-view character scale
        /// (a hero about 0.4 tall), so the camera sits close behind its head.
        /// </summary>
        static readonly Vector3 PivotHeight = new Vector3(0f, 0.3f, 0f);

        const float ShoulderOffset = 0.12f;

        /// <summary>
        /// Most the camera rises over the shoulder, in 5 degree candidates, and how fast it rises (lowering is damped).
        /// It lifts only when the pulled-in distance at the current lift is below <see cref="CloseDistance"/>.
        /// </summary>
        const float MaxLift = 15f, LiftDegreesPerSecond = 75f, CloseDistance = 0.2f;

        /// <summary>Outward release time constant (specs/003: 0.15 s, within 5% after 0.5 s); inward is immediate.</summary>
        const float ReleaseSeconds = 0.15f;

        Camera cam;
        float distance = GameSettings.DefaultHeroDistance, shownDistance = GameSettings.DefaultHeroDistance;
        float shownZoom = GameSettings.DefaultHeroDistance, shownLift, targetLift;
        float transition;
        bool orbitKept;
        Vector3 fromPosition;
        Quaternion fromRotation;
        Vector3 lastPivot, lastPosition;
        Quaternion lastRotation;
        bool hasLast;

        public Camera Camera => cam != null ? cam : cam = GetComponent<Camera>();

        /// <summary>Read-only presentation blockers (refreshed from the world view by the bootstrap).</summary>
        public HeroCameraObstacles Obstacles { get; } = new HeroCameraObstacles();

        /// <summary>Orbit yaw in degrees (0 = looking north/+Z, clockwise); the hero faces this way when controlled.</summary>
        public float Yaw { get; private set; } = CameraRig.Yaw;

        public float Pitch { get; private set; } = 16f;

        public float Distance => distance;
        public float Sensitivity { get; set; } = 1f;
        public bool InvertY { get; set; }

        public float FieldOfView
        {
            get => Camera.fieldOfView;
            set => Camera.fieldOfView = Mathf.Clamp(value, 50f, 90f);
        }

        public bool InTransition => transition > 0f;

        /// <summary>Distance and lift actually shown this frame (after obstacles and damping).</summary>
        public float ShownDistance => shownDistance;

        public float ShownLift => shownLift;

        /// <summary>The pivot the shown pose orbits (shoulder, or a fallback when the shoulder is inside a blocker).</summary>
        public Vector3 ShownPivot => lastPivot;

        /// <summary>Entry transitions cut short because an interpolated pose would have entered a blocker.</summary>
        public int TransitionCuts { get; private set; }

        /// <summary>Frames that used the tight-space fallback (no clear pose: the last clear pose was held).</summary>
        public int FallbackFrames { get; private set; }

        public void SetDefaultDistance(float d) => distance = shownDistance = shownZoom = Mathf.Clamp(d, MinDistance, MaxDistance);

        /// <summary>
        /// Starts the view from the strategic camera and eases to the orbit pose (skipped with <see cref="SkipTransition"/>).
        /// The start sits along the strategic direction at the distance where this perspective camera frames what the
        /// orthographic view framed, so the move is a smooth push-in instead of a dive from 40 units out. Within a run the
        /// last orbit is kept; a new, loaded or restarted run begins from the strategic yaw (<see cref="ResetOrbit"/>).
        /// </summary>
        public void BeginFrom(Camera from, float yaw, float orthographicSize, Vector3 focus)
        {
            if (!orbitKept)
            {
                Yaw = yaw;
                Pitch = 16f;
                orbitKept = true;
            }

            var frame = orthographicSize / Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            fromPosition = focus - from.transform.forward * frame;
            fromRotation = from.transform.rotation;
            MatchStrategic(from, orthographicSize, fromPosition);
            toStrategic = toStrategicFrom = 1f;
            transition = TransitionSeconds;
            shownDistance = shownZoom = distance;
            shownLift = targetLift = 0f;
            hasLast = false;
        }

        bool exiting;
        float exitLeft;
        Vector3 exitFrom;
        Quaternion exitFromRotation;

        /// <summary>
        /// How far the shown view has gone from the hero view (0) toward the strategic one (1). The first half fades the fog
        /// out; the second blends the projection from perspective to the strategic camera's orthographic one, so the views swap
        /// on the same image instead of jumping (owner, 2026-10-29), and the entry starts from it.
        /// </summary>
        float toStrategic, toStrategicFrom;

        float orthoSize, orthoNear, orthoFar;

        /// <summary>Hero-view fog shown, 0..1 (1 outside the transitions).</summary>
        public float FogAmount => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(toStrategic * 2f));

        /// <summary>The projection blend toward the strategic view, 0..1.</summary>
        public float StrategicBlend => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(toStrategic * 2f - 1f));

        /// <summary>Easing back out to the strategic framing (owner, 2026-10-21: both directions smooth).</summary>
        public bool Exiting => exiting;

        /// <summary>Starts the eased exit from the pose shown now toward the strategic camera's framing.</summary>
        public void BeginExit()
        {
            exiting = true;
            exitLeft = TransitionSeconds;
            exitFrom = transform.position;
            exitFromRotation = transform.rotation;
            toStrategicFrom = toStrategic;
            transition = 0f;
        }

        /// <summary>Re-entering during an exit: ease back in from the pose shown now (the orbit is kept).</summary>
        public void BeginFromCurrent()
        {
            exiting = false;
            fromPosition = transform.position;
            fromRotation = transform.rotation;
            toStrategicFrom = toStrategic;
            transition = TransitionSeconds;
            shownDistance = shownZoom = distance;
            shownLift = targetLift = 0f;
            hasLast = false;
        }

        /// <summary>
        /// One frame of the exit: the perspective camera moves to where it frames what the orthographic strategic camera
        /// frames (as the entry starts) while its projection blends into the strategic one, then the views swap. A pose that
        /// would enter a blocker ends the exit at once. Returns true when it is finished.
        /// </summary>
        public bool FollowExit(Camera strategic, float orthographicSize, Vector3 focus, float dt)
        {
            if (!exiting) return true;
            var frame = orthographicSize / Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var to = focus - strategic.transform.forward * frame;
            MatchStrategic(strategic, orthographicSize, to); // the player may zoom the strategic view during the exit
            exitLeft = Mathf.Max(0f, exitLeft - dt);
            var t = Mathf.SmoothStep(0f, 1f, 1f - exitLeft / TransitionSeconds);
            var p = Vector3.Lerp(exitFrom, to, t);
            var r = Quaternion.Slerp(exitFromRotation, strategic.transform.rotation, t);
            if (exitLeft > 0f && (Obstacles.Blocked(p, HeroCameraObstacles.Margin) || !NearPlaneClear(p, r, Camera))) exitLeft = 0f;
            else
            {
                toStrategic = Mathf.Lerp(toStrategicFrom, 1f, t);
                transform.SetPositionAndRotation(p, r);
                ApplyProjection();
            }

            if (exitLeft > 0f) return false;
            exiting = false;
            return true;
        }

        /// <summary>
        /// The orthographic projection the strategic camera shows, placed at <paramref name="at"/> on its view axis: the same
        /// half-height, and its clip planes moved by the distance between the two cameras so the same geometry is drawn.
        /// </summary>
        void MatchStrategic(Camera strategic, float orthographicSize, Vector3 at)
        {
            var offset = Vector3.Dot(at - strategic.transform.position, strategic.transform.forward);
            orthoSize = orthographicSize;
            orthoNear = strategic.nearClipPlane - offset;
            orthoFar = strategic.farClipPlane - offset;
        }

        /// <summary>
        /// The element-wise blend of the perspective and the strategic orthographic projection. It is the perspective view
        /// seen from ever farther back with an ever narrower angle, so the framing at the strategic focus stays put.
        /// </summary>
        void ApplyProjection()
        {
            var c = Camera;
            var s = StrategicBlend;
            if (s <= 0f)
            {
                c.ResetProjectionMatrix();
                return;
            }

            var perspective = Matrix4x4.Perspective(c.fieldOfView, c.aspect, c.nearClipPlane, c.farClipPlane);
            var ortho = Matrix4x4.Ortho(-orthoSize * c.aspect, orthoSize * c.aspect, -orthoSize, orthoSize, orthoNear, orthoFar);
            var m = new Matrix4x4();
            for (var i = 0; i < 16; i++) m[i] = Mathf.Lerp(perspective[i], ortho[i], s);
            c.projectionMatrix = m;
        }

        /// <summary>A new, loaded or restarted run: the next entry starts from the strategic yaw again.</summary>
        public void ResetOrbit() => orbitKept = false;

        public void SkipTransition() => transition = 0f;

        /// <summary>Scripted orbit (captures): yaw in degrees (0 = north), pitch clamped to the usual range.</summary>
        public void SetOrbit(float yaw, float pitch)
        {
            Yaw = Mathf.Repeat(yaw, 360f);
            Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            orbitKept = true;
        }

        /// <summary>Mouse look and wheel distance; real (unscaled) time, so it also works while Night is paused.</summary>
        public void Look(Mouse mouse, bool allowed)
        {
            if (mouse == null || !allowed) return;
            var delta = mouse.delta.ReadValue();
            Yaw = Mathf.Repeat(Yaw + delta.x * LookDegreesPerPixel * Sensitivity, 360f);
            // Default (Invert look off): mouse forward looks up, as for aiming (owner, 2026-10-20, ready for a future bow; it was
            // the other way since 2026-10-19). Invert look on: mouse forward looks down.
            Pitch = Mathf.Clamp(Pitch + (InvertY ? delta.y : -delta.y) * LookDegreesPerPixel * Sensitivity, MinPitch, MaxPitch);
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.15f, MinDistance, MaxDistance);
        }

        /// <summary>
        /// The pivot for an orbit rotation: the shoulder, or nearer the head when the shoulder is inside a blocker (a wall the
        /// hero stands against), or lower under an overhang; each stepped off a blocker surface it is too close to (at most
        /// <see cref="MaxPivotStep"/>). Null when none is clear.
        /// </summary>
        Vector3? Pivot(Vector3 hero, Quaternion rotation, float pad)
        {
            var head = hero + PivotHeight * 0.7f;
            foreach (var (height, shoulder) in new[] { (1f, ShoulderOffset), (1f, 0f), (0.8f, 0f), (0.6f, 0f) })
            {
                var p = hero + PivotHeight * height + rotation * Vector3.right * shoulder;
                if (Obstacles.Blocked(p, pad) && !Obstacles.StepOut(p, pad, MaxPivotStep, out p)) continue;
                var toPivot = p - head;
                var length = toPivot.magnitude;
                if (length > 1e-4f && Obstacles.FreeDistance(head, toPivot / length, length, 0f) < length - 1e-4f) continue;
                return p;
            }

            return null;
        }

        /// <summary>
        /// Farthest a pivot steps sideways off a blocker: the hero touches a drawn Wall or Tower 0.06 tile from its box,
        /// inside the near-plane padding (about 0.064 at FOV 60), and a corner can need a step from two surfaces.
        /// </summary>
        const float MaxPivotStep = 0.1f;

        /// <summary>The clear distance behind the pivot at one lift, the pivot and rotation used (distance -1: no clear pivot).</summary>
        float Solve(Vector3 hero, float lift, float wanted, float pad, out Vector3 pivot, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(Mathf.Min(MaxPitch, Pitch + lift), Yaw, 0f);
            var p = Pivot(hero, rotation, pad);
            pivot = p ?? hero + PivotHeight;
            if (!p.HasValue) return -1f;
            return Obstacles.FreeDistance(pivot, rotation * Vector3.back, wanted, pad);
        }

        /// <summary>Places the camera behind <paramref name="hero"/> (the displayed root), clear of every presentation blocker.</summary>
        public void Follow(Vector3 hero, RunSimulation sim, float dt)
        {
            var c = Camera;
            var pad = HeroCameraObstacles.NearPadding(c);
            // The wheel eases in and out.
            shownZoom = dt > 0f ? Mathf.MoveTowards(shownZoom, distance, dt * 6f) : distance;

            // Pull in first: lift only when the close pose at the current lift still cannot clear, the smallest lift that does.
            var atRest = Solve(hero, 0f, shownZoom, pad, out _, out _);
            var wantLift = 0f;
            if (atRest < Mathf.Min(CloseDistance, shownZoom))
            {
                wantLift = MaxLift;
                for (var lift = 5f; lift <= MaxLift; lift += 5f)
                    if (Solve(hero, lift, shownZoom, pad, out _, out _) >= Mathf.Min(CloseDistance, shownZoom))
                    {
                        wantLift = lift;
                        break;
                    }
            }

            // Hysteresis: a lift is dropped only once the pose without it clears with room to spare, so a stationary
            // corner does not flip between candidates.
            if (wantLift < targetLift && atRest < Mathf.Min(CloseDistance * 1.25f, shownZoom)) wantLift = targetLift;
            targetLift = wantLift;
            if (dt <= 0f) shownLift = targetLift;
            else if (targetLift > shownLift) shownLift = Mathf.MoveTowards(shownLift, targetLift, LiftDegreesPerSecond * dt);
            else shownLift += (targetLift - shownLift) * (1f - Mathf.Exp(-dt / ReleaseSeconds));

            var allowed = Solve(hero, shownLift, shownZoom, pad, out var pivot, out var rotation);
            Vector3 position;
            if (allowed < 0f)
            {
                // Tight-space fallback: no clear pivot at all (inside overlapping blockers). Hold the last clear pose.
                FallbackFrames++;
                if (hasLast)
                {
                    position = lastPosition;
                    rotation = lastRotation;
                    pivot = lastPivot;
                }
                else
                {
                    // No clear pose ever: inside the hero's own head, looking along the orbit (only the hero can be clipped).
                    pivot = position = hero + PivotHeight * 0.7f;
                }
            }
            else
            {
                // Inward at once for safety; outward with time-based damping so the view does not pump.
                if (allowed < shownDistance || dt <= 0f) shownDistance = allowed;
                else shownDistance += (allowed - shownDistance) * (1f - Mathf.Exp(-dt / ReleaseSeconds));
                shownDistance = Mathf.Min(shownDistance, allowed);
                position = pivot + rotation * Vector3.back * shownDistance;
                if (!Obstacles.PoseClear(pivot, position, rotation, c))
                {
                    shownDistance = Mathf.Max(0f, allowed - HeroCameraObstacles.Margin);
                    position = pivot + rotation * Vector3.back * shownDistance;
                }
            }

            lastPivot = pivot;
            lastPosition = position;
            lastRotation = rotation;
            hasLast = true;
            if (transition > 0f)
            {
                transition = Mathf.Max(0f, transition - dt);
                var t = Mathf.SmoothStep(0f, 1f, 1f - transition / TransitionSeconds);
                var p = Vector3.Lerp(fromPosition, position, t);
                var r = Quaternion.Slerp(fromRotation, rotation, t);
                if (transition > 0f && (Obstacles.Blocked(p, HeroCameraObstacles.Margin) || !NearPlaneClear(p, r, c)))
                {
                    transition = 0f; // the eased path would enter a blocker: cut to the validated pose instead
                    TransitionCuts++;
                }
                else
                {
                    toStrategic = Mathf.Lerp(toStrategicFrom, 0f, t);
                    transform.SetPositionAndRotation(p, r);
                    ApplyProjection();
                    return;
                }
            }

            toStrategic = 0f;
            transform.SetPositionAndRotation(position, rotation);
            ApplyProjection();
        }

        bool NearPlaneClear(Vector3 position, Quaternion rotation, Camera c)
        {
            var near = c.nearClipPlane;
            var hh = near * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var hw = hh * c.aspect;
            for (var i = 0; i < 4; i++)
                if (Obstacles.Blocked(position + rotation * new Vector3((i & 1) == 0 ? -hw : hw, (i & 2) == 0 ? -hh : hh, near), HeroCameraObstacles.Margin))
                    return false;
            return true;
        }

        /// <summary>Whether the pose shown now is clear of every blocker (tests; transitions validate their own path).</summary>
        public bool ShownPoseClear() => !hasLast || Obstacles.PoseClear(lastPivot, transform.position, transform.rotation, Camera);
    }
}
