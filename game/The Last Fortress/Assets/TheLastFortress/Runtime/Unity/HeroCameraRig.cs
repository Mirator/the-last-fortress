using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastFortress
{
    /// <summary>
    /// Third-person perspective camera for the hero view (specs/003 3.2). A separate camera from the strategic
    /// <see cref="CameraRig"/>, enabled instead of it. Mouse look orbits behind the hero; the wheel sets the distance. It never
    /// touches the simulation: collision is a read-only query of the grid (buildings, forest, rock) by approximate height.
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

        /// <summary>Most the camera rises over the shoulder to clear an obstacle behind the hero, and how fast it gets there.</summary>
        const float MaxLift = 15f, LiftDegreesPerSecond = 75f;

        Camera cam;
        float distance = GameSettings.DefaultHeroDistance, shownDistance = GameSettings.DefaultHeroDistance;
        float shownZoom = GameSettings.DefaultHeroDistance, shownLift;
        float transition;
        Vector3 fromPosition;
        Quaternion fromRotation;

        public Camera Camera => cam != null ? cam : cam = GetComponent<Camera>();

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

        public void SetDefaultDistance(float d) => distance = shownDistance = shownZoom = Mathf.Clamp(d, MinDistance, MaxDistance);

        /// <summary>
        /// Starts the view from the strategic camera and eases to the orbit pose (skipped with <see cref="SkipTransition"/>).
        /// The start sits along the strategic direction at the distance where this perspective camera frames what the
        /// orthographic view framed, so the move is a smooth push-in instead of a dive from 40 units out.
        /// </summary>
        public void BeginFrom(Transform from, float yaw, float orthographicSize, Vector3 focus)
        {
            Yaw = yaw;
            Pitch = 16f;
            var frame = orthographicSize / Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            fromPosition = focus - from.forward * frame;
            fromRotation = from.rotation;
            transition = TransitionSeconds;
            shownDistance = shownZoom = distance;
            shownLift = 0f;
        }

        public void SkipTransition() => transition = 0f;

        /// <summary>Scripted orbit (captures): yaw in degrees (0 = north), pitch clamped to the usual range.</summary>
        public void SetOrbit(float yaw, float pitch)
        {
            Yaw = Mathf.Repeat(yaw, 360f);
            Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
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

        /// <summary>Places the camera behind <paramref name="hero"/> (the displayed position), pulled in by solid obstacles.</summary>
        public void Follow(Vector3 hero, RunSimulation sim, float dt)
        {
            // An obstacle behind the hero first pulls the camera in; only when even a short distance is blocked does it rise
            // over the shoulder, smoothly and at most MaxLift, so it neither jumps in steps nor flips to a top-down view.
            var targetLift = MaxLift;
            for (var lift = 0f; lift <= MaxLift; lift += 5f)
            {
                var r = Quaternion.Euler(Mathf.Min(MaxPitch, Pitch + lift), Yaw, 0f);
                var p = hero + PivotHeight + r * Vector3.right * ShoulderOffset;
                if (ClearDistance(sim, p, r * Vector3.back, distance) < Mathf.Min(distance, 0.55f)) continue;
                targetLift = lift;
                break;
            }

            shownLift = dt > 0f ? Mathf.MoveTowards(shownLift, targetLift, LiftDegreesPerSecond * dt) : targetLift;
            var rotation = Quaternion.Euler(Mathf.Min(MaxPitch, Pitch + shownLift), Yaw, 0f);
            var pivot = hero + PivotHeight + rotation * Vector3.right * ShoulderOffset;
            var allowed = ClearDistance(sim, pivot, rotation * Vector3.back, distance);
            // The wheel eases in and out; an obstacle pulls in at once and releases gently, so the view does not pump.
            shownZoom = dt > 0f ? Mathf.MoveTowards(shownZoom, distance, dt * 6f) : distance;
            var wanted = Mathf.Min(allowed, shownZoom);
            shownDistance = wanted < shownDistance || dt <= 0f ? wanted : Mathf.MoveTowards(shownDistance, wanted, dt * 6f);
            var position = pivot + rotation * Vector3.back * shownDistance;
            if (position.y < 0.12f) position.y = 0.12f;
            if (transition > 0f)
            {
                transition = Mathf.Max(0f, transition - dt);
                var t = Mathf.SmoothStep(0f, 1f, 1f - transition / TransitionSeconds);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, t), Quaternion.Slerp(fromRotation, rotation, t));
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Distance along the ray from the pivot before it meets a building, forest or rock volume (grid query).</summary>
        static float ClearDistance(RunSimulation sim, Vector3 from, Vector3 direction, float max)
        {
            const float step = 0.05f, margin = 0.15f;
            for (var t = step; t <= max; t += step)
            {
                var p = from + direction * t;
                if (p.y < ObstacleHeight(sim, p.x + direction.x * margin, p.z + direction.z * margin) + margin)
                    return Mathf.Max(0.2f, t - margin);
            }

            return max;
        }

        /// <summary>Approximate height of what stands on the cell at this point (0 for open ground or off the board).</summary>
        public static float ObstacleHeight(RunSimulation sim, float x, float z)
        {
            if (sim == null || !GridCoordinates.TryWorldToGrid(x, z, out var cell)) return 0f;
            var b = sim.Grid.GetOccupant(cell);
            if (b != null)
                switch (b.Kind)
                {
                    // Scaled with the real-size models (plan 014).
                    case BuildingKind.Keep: return 4.6f;
                    case BuildingKind.Tower: return 1.85f;
                    case BuildingKind.Watchtower: return 1.9f;
                    case BuildingKind.House: return 1.35f;
                    case BuildingKind.RepairStation: return 1.8f;
                    case BuildingKind.Cannon: return 0.55f;
                    case BuildingKind.Wall: return 0.75f;
                    case BuildingKind.Farm: return 0.5f;
                    default: return 1f;
                }

            var terrain = sim.Config.Terrain;
            if (terrain == null) return 0f;
            switch (terrain.Cells[cell.Z * GridCoordinates.Size + cell.X])
            {
                case TerrainType.Forest: return 5.2f; // real-height trees (plan 014)
                case TerrainType.Rock: return 0.5f; // the boulder models are low
                default: return 0f;
            }
        }
    }
}
