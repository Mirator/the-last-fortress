using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Presents the hero (specs/003 3.3, 4.4): the authored or blockout model posed by <see cref="CharacterRig"/>, a ground
    /// ring that makes it findable from the strategic view, and the swing/hit/fall cues from simulation events. While the
    /// player steers in the hero view, the shown position is predicted one fractional step ahead from the current command
    /// with the same movement rules, so input moves the hero in the same frame; the next real step replaces it. Nothing
    /// here writes to the simulation.
    /// </summary>
    public sealed class HeroPresenter : MonoBehaviour
    {
        const float SwingSeconds = 0.3f, FlinchSeconds = 0.25f, FallSeconds = 0.6f;
        const float JumpSeconds = 0.45f, JumpHeight = 0.12f, SlashSeconds = 0.2f, RiseSeconds = 0.7f;

        /// <summary>Hero model height (model units) and how far its sword reaches in front of the body when it strikes.</summary>
        const float BodyHeight = 0.6f, BladeReach = 0.42f;

        /// <summary>Degrees per second the shown body turns toward where it runs (it snaps to the aim when it swings).</summary>
        const float TurnRate = 900f;

        /// <summary>
        /// Fastest the shown hero may move (tiles/s), a little above its 3 tiles/s: pressing, releasing or reversing a key
        /// changes the prediction by up to a step at once; the figure (and the camera that follows it) glides instead of jolting.
        /// </summary>
        const float ShownSpeed = 2.4f;

        PrototypeContent content;
        Transform model;
        CharacterRig rig;
        GameObject ring;
        Material ringMaterial;
        bool built, hasShown;
        float gaitPhase, gaitAmount, swing, flinch, fall, jump, slash, bodyYaw, slashYaw, rise, idleClock, bank, shownSpeed;

        /// <summary>World distance from the swing's origin to the nearest enemy it struck (0: a miss), and where it stood.</summary>
        float strikeDistance;

        Vector3 strikeTarget;
        Vector3 lastShown, shown, slashAt;
        GameObject slashArc, reachArc;
        Material slashMaterial, reachMaterial;

        /// <summary>World position shown this frame (feet), used by the hero camera.</summary>
        public Vector3 ShownPosition { get; private set; }

        /// <summary>Hero facing shown this frame, in degrees (0 = north/+Z, clockwise).</summary>
        public float ShownYaw { get; private set; }

        public void Initialize(PrototypeContent content) => this.content = content;

        void Build()
        {
            built = true;
            var prefab = content.heroPrefab != null ? content.heroPrefab : content.gruntPrefab;
            var go = Instantiate(prefab, transform);
            go.name = "Model";
            model = go.transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            if (content.heroPrefab == null) TintBlockout(go); // placeholder until the authored hero exists
            rig = go.GetComponent<CharacterRig>();
            if (content.enemyScale > 0f) transform.localScale = Vector3.one * content.enemyScale;
            ringMaterial = Prims.Tinted(content.overlay != null ? content.overlay : content.arrow, Palette.RouteCyan.WithAlpha(0.7f));
            ring = Prims.Create(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.9f, 0.004f, 0.9f), ringMaterial, "Ring");
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The swing's real reach and arc (world size, not scaled with the figure): what it shows is what it hits.
            slashArc = new GameObject("SlashArc");
            slashArc.transform.SetParent(transform.parent, false);
            slashArc.AddComponent<MeshFilter>().sharedMesh = ArcMesh((float)HeroRules.SwingReach, 120f);
            var mr = slashArc.AddComponent<MeshRenderer>();
            slashMaterial = Prims.Tinted(content.overlay != null ? content.overlay : content.arrow, new Color(1f, 0.95f, 0.8f, 0f));
            mr.sharedMaterial = slashMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            slashArc.SetActive(false);

            // A faint copy in front of the hero while steering: the reach and angle before the swing, not only after it.
            reachArc = new GameObject("ReachArc");
            reachArc.transform.SetParent(transform.parent, false);
            reachArc.AddComponent<MeshFilter>().sharedMesh = slashArc.GetComponent<MeshFilter>().sharedMesh;
            var rr = reachArc.AddComponent<MeshRenderer>();
            reachMaterial = Prims.Tinted(content.overlay != null ? content.overlay : content.arrow, new Color(1f, 0.95f, 0.8f, 0.09f));
            rr.sharedMaterial = reachMaterial;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reachArc.SetActive(false);
        }

        /// <summary>A flat ring sector (inner 40% of the radius cut out), centred on +Z, lying on the ground plane.</summary>
        static Mesh ArcMesh(float radius, float degrees)
        {
            const int segments = 16;
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];
            for (var i = 0; i <= segments; i++)
            {
                var a = (-degrees * 0.5f + degrees * i / segments) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = d * radius * 0.4f;
                verts[i * 2 + 1] = d * radius;
            }

            for (var i = 0; i < segments; i++)
            {
                var k = i * 6;
                tris[k] = i * 2; tris[k + 1] = i * 2 + 1; tris[k + 2] = i * 2 + 2;
                tris[k + 3] = i * 2 + 1; tris[k + 4] = i * 2 + 3; tris[k + 5] = i * 2 + 2;
            }

            return new Mesh { name = "HeroSlashArc", vertices = verts, triangles = tris };
        }

        /// <summary>Space in the hero view: a short hop. Presentation only; the simulation never sees it.</summary>
        public void Jump()
        {
            if (jump <= 0f && fall <= 0f) jump = JumpSeconds;
        }

        public bool Airborne => jump > 0f;

        /// <summary>Settlement colours on the Grunt rig: a readable stand-in, not the authored hero.</summary>
        static void TintBlockout(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var name = mats[i].name.ToLowerInvariant();
                    var warm = name.Contains("skin") ? new Color(0.86f, 0.68f, 0.52f) : name.Contains("metal") || name.Contains("iron") ? new Color(0.75f, 0.78f, 0.82f) : new Color(0.22f, 0.42f, 0.72f);
                    Prims.SetColor(mats[i], warm);
                }

                r.materials = mats;
            }
        }

        /// <summary>
        /// Updates the hero's view. <paramref name="predicted"/> is the command the player is steering with in the hero view
        /// while the simulation advances (null otherwise: interpolate between the last two steps).
        /// </summary>
        public void Sync(RunSimulation sim, IReadOnlyList<SimEvent> events, float dt, HeroCommand? predicted, bool strategicView, float characterScale = 1f)
        {
            var hero = sim.Hero;
            if (hero == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!built) Build();
            foreach (var e in events)
                switch (e.Type)
                {
                    case SimEventType.HeroAttacked:
                        swing = SwingSeconds;
                        slash = SlashSeconds;
                        slashYaw = e.TargetId * 360f / 64f; // the facing and place the swing was resolved with
                        slashAt = new Vector3((float)e.FromX, 0f, (float)e.FromZ);
                        strikeDistance = 0f;
                        break;
                    case SimEventType.HeroHit when e.HasPositions:
                        var d = new Vector2((float)e.ToX - slashAt.x, (float)e.ToZ - slashAt.z).magnitude;
                        if (strikeDistance <= 0f || d < strikeDistance)
                        {
                            strikeDistance = d;
                            strikeTarget = new Vector3((float)e.ToX, 0f, (float)e.ToZ);
                        }
                        break;
                    case SimEventType.HeroDamaged: flinch = FlinchSeconds; break;
                    case SimEventType.HeroDowned: fall = 0.0001f; break;
                    case SimEventType.HeroRespawned:
                        fall = 0f;
                        rise = RiseSeconds; // gets up from a crouch at the spawn cell
                        lastShown = new Vector3((float)hero.X, 0f, (float)hero.Z);
                        hasShown = false;
                        break;
                    case SimEventType.RunReset:
                    case SimEventType.HeroRelocated:
                        fall = 0f;
                        lastShown = new Vector3((float)hero.X, 0f, (float)hero.Z);
                        hasShown = false; // a teleport: show it at once
                        break;
                }

            var alpha = Mathf.Clamp01((float)sim.HeroInterpolationAlpha);
            Vector3 pos;
            float aim;
            var attacking = swing > 0f;
            if (predicted.HasValue && predicted.Value.Controlled && hero.Alive)
            {
                var c = predicted.Value;
                pos = Predict(sim, hero, c, alpha);
                aim = c.Facing * 360f / 64f;
                attacking |= c.Attack;
            }
            else
            {
                pos = Vector3.Lerp(new Vector3((float)hero.PrevX, 0f, (float)hero.PrevZ), new Vector3((float)hero.X, 0f, (float)hero.Z), alpha);
                aim = hero.Facing * 360f / 64f;
            }

            // Rate-limited toward the raw (predicted or interpolated) position: smooth through presses, releases and pauses;
            // frozen while the presentation clock is; a jump of more than a tile (respawn, load) shows at once.
            if (!hasShown || (pos - shown).sqrMagnitude > 1f) shown = pos;
            else if (dt > 0f) shown = Vector3.MoveTowards(shown, pos, ShownSpeed * dt);
            hasShown = true;
            pos = shown;

            // The body faces where it is steered (or runs, when not steered); it turns to the aim to swing.
            var heading = predicted.HasValue && predicted.Value.Controlled ? predicted.Value.Heading : -1;
            var moved = new Vector2(pos.x - lastShown.x, pos.z - lastShown.z);
            var want = attacking ? aim : heading >= 0 ? heading * 22.5f : moved.sqrMagnitude > 1e-7f ? Mathf.Atan2(moved.x, moved.y) * Mathf.Rad2Deg : bodyYaw;
            var before = bodyYaw;
            // Turns fast to the aim to swing, but never in one frame (it snapped up to 180 degrees).
            bodyYaw = dt <= 0f ? want : Mathf.MoveTowardsAngle(bodyYaw, want, (attacking ? 2000f : TurnRate) * dt);
            // Lean into turns while running (a bank, eased), never while swinging.
            if (dt > 0f) bank = Mathf.MoveTowards(bank, attacking ? 0f : Mathf.Clamp(-Mathf.DeltaAngle(before, bodyYaw) / dt * 0.02f * gaitAmount, -12f, 12f), 60f * dt);

            ShownPosition = pos;
            ShownYaw = aim;
            transform.localScale = Vector3.one * (content.enemyScale > 0f ? content.enemyScale : 1f) * characterScale;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, bodyYaw, 0f));
            Animate(pos, dt, hero.Alive);
            if (ring.activeSelf != (strategicView && hero.Alive)) ring.SetActive(strategicView && hero.Alive);
            // The ground ring keeps its world size (about 1.2 tiles) whatever the figure's size, so the hero stays findable.
            ring.transform.localScale = new Vector3(0.9f, 0.004f, 0.9f) / Mathf.Max(0.01f, characterScale);
            UpdateSlash(dt);
            var reach = predicted.HasValue && hero.Alive && slash <= 0f;
            if (reachArc.activeSelf != reach) reachArc.SetActive(reach);
            if (reach) reachArc.transform.SetPositionAndRotation(pos + Vector3.up * 0.004f, Quaternion.Euler(0f, aim, 0f)); // on the ground, not knee-high
        }

        /// <summary>The resolved swing: shown where and how it was resolved (it does not slide with the running hero).</summary>
        void UpdateSlash(float dt)
        {
            if (slash > 0f && dt > 0f) slash = Mathf.Max(0f, slash - dt);
            var show = slash > 0f;
            if (slashArc.activeSelf != show) slashArc.SetActive(show);
            if (!show) return;
            slashArc.transform.SetPositionAndRotation(slashAt + Vector3.up * 0.005f, Quaternion.Euler(0f, slashYaw, 0f));
            Prims.SetColor(slashMaterial, new Color(1f, 0.95f, 0.8f, 0.55f * slash / SlashSeconds));
        }

        /// <summary>The last stepped position plus the fraction of the next step this command will move (same rules, no writes).</summary>
        static Vector3 Predict(RunSimulation sim, Hero hero, HeroCommand c, float alpha)
        {
            var ux = hero.UnitsX;
            var uz = hero.UnitsZ;
            if (c.Heading >= 0)
            {
                var dx = Mathf.RoundToInt(HeroRules.HeadingX[c.Heading] * hero.MoveUnits / HeroRules.UnitsPerTile * alpha);
                var dz = Mathf.RoundToInt(HeroRules.HeadingZ[c.Heading] * hero.MoveUnits / HeroRules.UnitsPerTile * alpha);
                if (!sim.HeroOverlapsBlocked(ux + dx, uz)) ux += dx;
                if (!sim.HeroOverlapsBlocked(ux, uz + dz)) uz += dz;
            }

            return new Vector3(ux / (float)HeroRules.UnitsPerTile, 0f, uz / (float)HeroRules.UnitsPerTile);
        }

        void Animate(Vector3 pos, float dt, bool alive)
        {
            var moved = new Vector2(pos.x - lastShown.x, pos.z - lastShown.z).magnitude;
            lastShown = pos;
            // Steps follow the shown size (plan 013): at true scale the hero runs with long strides, so its feet keep pace.
            var scale = Mathf.Max(0.01f, transform.localScale.x);
            var height = BodyHeight * scale;
            var run = CharacterRig.RunAmount(shownSpeed, height);
            if (dt > 0f)
            {
                shownSpeed = Mathf.Lerp(shownSpeed, moved / dt, 1f - Mathf.Exp(-dt * 10f));
                gaitPhase += moved * CharacterRig.PhasePerTile(height, run);
                gaitAmount = Mathf.MoveTowards(gaitAmount, moved > 1e-4f ? 1f : 0f, dt * 8f);
                swing = Mathf.Max(0f, swing - dt);
                flinch = Mathf.Max(0f, flinch - dt);
                rise = Mathf.Max(0f, rise - dt);
                idleClock += dt * 2.1f;
                if (!alive && fall > 0f) fall = Mathf.Min(1f, fall + dt / FallSeconds);
            }

            if (jump > 0f && dt > 0f) jump = Mathf.Max(0f, jump - dt);
            // Front-loaded: the blade peaks a quarter into the swing (when the hit flinch and sparks show), then recovers.
            var progress = swing > 0f ? 1f - swing / SwingSeconds : 1f;
            var strike = swing > 0f ? (progress < 0.25f ? progress / 0.25f : 1f - (progress - 0.25f) / 0.75f) : 0f;
            // Sweep: the body winds right and snaps through left (ease-out), so the arc reads as a slash.
            // Eased in over the first 0.04 s and out over the last 0.1 s, so the swing never pops the body 45 degrees.
            var sweep = 0f;
            if (swing > 0f)
                sweep = Mathf.Lerp(-45f, 50f, 1f - Mathf.Pow(1f - progress, 3f))
                        * Mathf.Min(Mathf.SmoothStep(0f, 1f, progress / 0.13f), Mathf.SmoothStep(0f, 1f, (1f - progress) / 0.33f));
            var hit = flinch / FlinchSeconds;
            var t = 1f - jump / JumpSeconds;
            var hop = jump > 0f ? 4f * t * (1f - t) * JumpHeight / Mathf.Max(0.01f, transform.localScale.y) : 0f;
            // Getting up after a respawn: from a crouch (slack limbs), standing over the first part of the rise.
            var up = rise > 0f ? Mathf.Clamp01(rise / RiseSeconds) : 0f;
            var crouch = up * up;
            if (rig != null) rig.Pose(gaitPhase, jump > 0f ? 0.3f : gaitAmount, strike, 0f, hit, alive ? crouch * 0.45f : fall, jump > 0f ? 0f : run, idleClock);
            // The step-in: at the blade's contact the body has closed the gap to the nearest enemy it struck, so the sword
            // meets it at the simulation's reach (only a presentation offset; position, reach and hits are the simulation's).
            // A short step (at most 0.12 tile, about half a body height) toward the enemy it struck, not a 3 m lunge along the aim.
            var stepIn = Vector3.zero;
            if (strikeDistance > 0f)
            {
                var toward = new Vector3(strikeTarget.x - slashAt.x, 0f, strikeTarget.z - slashAt.z);
                if (toward.sqrMagnitude > 1e-6f)
                    stepIn = transform.InverseTransformDirection(toward.normalized) * (Mathf.Clamp(strikeDistance - BladeReach * scale, 0f, 0.12f) / scale);
            }

            var bobY = rig != null ? 0f : Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.03f * gaitAmount; // the rig's hips bob in phase
            model.localPosition = new Vector3(0f, bobY + hop - crouch * 0.08f - (alive ? 0f : fall * 0.03f), strike * 0.08f) + stepIn * strike;
            model.localRotation = Quaternion.Euler(alive ? 0f : fall * 80f, swing > 0f ? sweep : 0f, alive ? bank : 0f);
        }
    }
}
