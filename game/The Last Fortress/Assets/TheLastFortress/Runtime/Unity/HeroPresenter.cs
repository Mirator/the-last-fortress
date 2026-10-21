using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Presents the hero (game rules section 17): the authored or blockout model posed by <see cref="CharacterRig"/>, a
    /// ground ring that makes it findable from the strategic view, and the swing/hit/fall cues from simulation events. While
    /// the player steers in the hero view, the shown position is the last step moved toward the next one by the fraction of
    /// a step elapsed; the next step comes from the simulation's own movement code run on a copy
    /// (<see cref="RunSimulation.PredictHeroStep"/>), so the shown hero never takes a different collision path. Under
    /// release-rules-4 (specs/003 sections 3-4, 7) the attack pose runs on the simulation's ticks: the wind-up starts with
    /// the accepted attack and the blade reaches its contact pose on the frame the contact is resolved; legacy rules keep
    /// their timed swing. Nothing here writes to the simulation.
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

        /// <summary>release-rules-4: the run's hero tuning, the committed facing of the running attack and the contact sweep.</summary>
        HeroProfile profile;

        float committedYaw;

        /// <summary>release-rules-4: the command last predicted with, kept while the presentation is frozen (pause, menu).</summary>
        HeroCommand frozenCommand;
        GameObject contactArc;
        Material contactMaterial;

        /// <summary>release-rules-4 cosmetic step toward the struck enemy: at most 0.02 tile (specs/003 section 7).</summary>
        const float ContactStepIn = 0.02f, ContactSweepSeconds = 0.15f;

        Vector3 strikeTarget;
        Vector3 lastShown, shown, slashAt;
        GameObject slashArc, reachArc;
        Material slashMaterial, reachMaterial;

        /// <summary>World position shown this frame (feet), used by the hero camera.</summary>
        public Vector3 ShownPosition { get; private set; }

        /// <summary>Hero facing shown this frame, in degrees (0 = north/+Z, clockwise).</summary>
        public float ShownYaw { get; private set; }

        public void Initialize(PrototypeContent content) => this.content = content;

        /// <summary>
        /// The figure for this run's rules: release-rules-4 shows the articulated hero (<see cref="HeroAnimationRig"/>), older
        /// saves the legacy hero (<see cref="CharacterRig"/>); swapped when a run of the other kind is loaded.
        /// </summary>
        void BuildModel(bool contact)
        {
            if (model != null) Destroy(model.gameObject);
            modelContact = contact;
            var prefab = contact ? content.contactHeroPrefab : content.heroPrefab != null ? content.heroPrefab : content.gruntPrefab;
            var go = Instantiate(prefab, transform);
            go.name = "Model";
            model = go.transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            if (!contact && content.heroPrefab == null) TintBlockout(go); // placeholder until the authored hero exists
            rig = go.GetComponent<CharacterRig>();
            animRig = contact ? go.GetComponent<HeroAnimationRig>() : null;
            modelRenderers = go.GetComponentsInChildren<MeshRenderer>(true);
            modelShadows = new UnityEngine.Rendering.ShadowCastingMode[modelRenderers.Length];
            for (var i = 0; i < modelRenderers.Length; i++) modelShadows[i] = modelRenderers[i].shadowCastingMode;
            cameraHidden = false;
        }

        MeshRenderer[] modelRenderers;
        UnityEngine.Rendering.ShadowCastingMode[] modelShadows;
        bool cameraHidden;

        /// <summary>
        /// Tight-space fallback (specs/003 section 6): with the camera pressed against the hero's head, only the hero's own
        /// figure stops drawing (its shadow stays); scenery is never hidden.
        /// </summary>
        public void SetHiddenFromCamera(bool hidden)
        {
            if (hidden == cameraHidden || modelRenderers == null) return;
            cameraHidden = hidden;
            for (var i = 0; i < modelRenderers.Length; i++)
                if (modelRenderers[i] != null)
                    modelRenderers[i].shadowCastingMode = hidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : modelShadows[i];
        }

        /// <summary>The release-rules-4 adapter when that figure is shown (tests and captures read its phase and footfalls).</summary>
        public HeroAnimationRig AnimationRig => animRig;

        /// <summary>The release-rules-4 attack phase shown this frame (-1: none).</summary>
        public float ShownAttackPhase { get; private set; } = -1f;

        HeroAnimationRig animRig;
        bool modelContact;
        float turnRate;

        void Build()
        {
            built = true;
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

            // release-rules-4: no standing reach sector; a short pale sweep at the true reach where the contact resolved.
            contactArc = new GameObject("ContactSweep");
            contactArc.transform.SetParent(transform.parent, false);
            contactArc.AddComponent<MeshFilter>().sharedMesh = ArcMesh((float)HeroProfile.V4.SwingReach, 100f);
            var cr = contactArc.AddComponent<MeshRenderer>();
            contactMaterial = Prims.Tinted(content.overlay != null ? content.overlay : content.arrow, new Color(1f, 0.95f, 0.8f, 0f));
            cr.sharedMaterial = contactMaterial;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            contactArc.SetActive(false);
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
        public void Sync(RunSimulation sim, IReadOnlyList<SimEvent> events, float dt, HeroCommand? predicted, bool strategicView, float characterScale = 1f, float? cameraYaw = null)
        {
            var hero = sim.Hero;
            if (hero == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!built) Build();
            profile = hero.Profile;
            var contactRules = profile.ContactPhase;
            var contactFigure = contactRules && content.contactHeroPrefab != null;
            if (contactRules && !contactFigure && model == null) Debug.LogWarning("Release-rules-4 hero figure missing: showing the legacy hero.");
            if (model == null || modelContact != contactFigure) BuildModel(contactFigure);
            foreach (var e in events)
                switch (e.Type)
                {
                    case SimEventType.HeroAttacked when contactRules:
                        committedYaw = e.TargetId * 360f / 64f; // the wind-up starts; the pose itself runs on the simulation's ticks
                        strikeDistance = 0f;
                        break;
                    case SimEventType.HeroContact:
                        slash = ContactSweepSeconds; // the sweep shows where and how the contact resolved, also on a miss
                        slashYaw = Mathf.Atan2((float)(e.ToX - e.FromX), (float)(e.ToZ - e.FromZ)) * Mathf.Rad2Deg;
                        slashAt = new Vector3((float)e.FromX, 0f, (float)e.FromZ);
                        strikeDistance = 0f;
                        break;
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
                    case SimEventType.HeroDowned:
                        fall = 0.0001f;
                        slash = 0f; // death wins over the attack: no sweep left behind
                        break;
                    case SimEventType.HeroRespawned:
                        fall = 0f;
                        slash = 0f;
                        rise = RiseSeconds; // gets up from a crouch at the spawn cell
                        lastShown = new Vector3((float)hero.X, 0f, (float)hero.Z);
                        hasShown = false;
                        break;
                    case SimEventType.RunReset:
                    case SimEventType.HeroRelocated:
                        fall = 0f;
                        slash = 0f; // no sweep left at the old spot
                        lastShown = new Vector3((float)hero.X, 0f, (float)hero.Z);
                        hasShown = false; // a teleport: show it at once
                        break;
                }

            var alpha = Mathf.Clamp01((float)sim.HeroInterpolationAlpha);
            Vector3 pos;
            float aim;
            var attacking = swing > 0f;
            if (contactRules && !predicted.HasValue && dt <= 0f && frozenCommand.Controlled) predicted = frozenCommand; // frozen: hold the pose
            frozenCommand = contactRules && predicted.HasValue ? predicted.Value : HeroCommand.None;
            if (predicted.HasValue && predicted.Value.Controlled && hero.Alive)
            {
                var c = predicted.Value;
                pos = Predict(sim, hero, c, alpha);
                // The shown aim follows the camera's continuous yaw; the 64-way facing is only what the simulation reads
                // (snapping the body to it turned it in 5.6 degree steps).
                aim = cameraYaw ?? c.Facing * 360f / 64f;
                if (!contactRules) attacking |= c.Attack;
            }
            else if (contactRules && hero.Alive)
            {
                pos = Predict(sim, hero, HeroCommand.None, alpha); // the guard brakes with the same motor
                aim = hero.Facing * 360f / 64f;
            }
            else
            {
                pos = Vector3.Lerp(new Vector3((float)hero.PrevX, 0f, (float)hero.PrevZ), new Vector3((float)hero.X, 0f, (float)hero.Z), alpha);
                aim = hero.Facing * 360f / 64f;
            }

            // Legacy rules: rate-limited toward the raw position, so presses, releases and pauses never jolt the view.
            // release-rules-4: the prediction is the simulation's own next step and the motor limits every change, so the
            // shown root is the prediction itself (no lag to correct). A jump of more than a tile (respawn, load) shows at once.
            if (!hasShown || (pos - shown).sqrMagnitude > 1f || contactRules) shown = pos;
            else if (dt > 0f) shown = Vector3.MoveTowards(shown, pos, ShownSpeed * dt);
            hasShown = true;
            pos = shown;

            // The body faces where it is steered (or runs, when not steered); it turns to the aim to swing. release-rules-4:
            // to the facing committed when the attack was accepted, from the wind-up through the contact.
            var phase = contactRules ? AttackPhase(sim, hero, alpha) : -1f;
            if (contactRules) attacking = phase >= 0f && phase < 1.35f;
            var heading = predicted.HasValue && predicted.Value.Controlled ? predicted.Value.Heading64 * 5.625f : -1f;
            if (predicted.HasValue && predicted.Value.Controlled && predicted.Value.Heading64 < 0) heading = -1f;
            var moved = new Vector2(pos.x - lastShown.x, pos.z - lastShown.z);
            var want = attacking ? (contactRules ? committedYaw : aim) : heading >= 0f ? heading : moved.sqrMagnitude > 1e-7f ? Mathf.Atan2(moved.x, moved.y) * Mathf.Rad2Deg : bodyYaw;
            var steering = predicted.HasValue && predicted.Value.Controlled;
            // The articulated figure keeps facing the aim (or the committed attack) and lets its hips and legs follow the
            // travel, so sideways and backward movement read as strafing and backpedalling, not as a forward run turned away.
            if (animRig != null) want = attacking ? committedYaw : steering ? aim : moved.sqrMagnitude > 1e-7f ? Mathf.Atan2(moved.x, moved.y) * Mathf.Rad2Deg : bodyYaw;
            var before = bodyYaw;
            // Turns fast to the aim to swing, but never in one frame (it snapped up to 180 degrees).
            bodyYaw = dt <= 0f ? want : Mathf.MoveTowardsAngle(bodyYaw, want, (attacking ? 2000f : animRig != null ? 540f : TurnRate) * dt);
            turnRate = dt > 0f ? Mathf.DeltaAngle(before, bodyYaw) / dt : 0f;
            // Lean into turns while running (a bank, eased), never while swinging.
            if (dt > 0f) bank = Mathf.MoveTowards(bank, attacking ? 0f : Mathf.Clamp(-Mathf.DeltaAngle(before, bodyYaw) / dt * 0.02f * gaitAmount, -12f, 12f), 60f * dt);

            ShownPosition = pos;
            ShownYaw = aim;
            transform.localScale = Vector3.one * (content.enemyScale > 0f ? content.enemyScale : 1f) * characterScale;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, bodyYaw, 0f));
            Animate(pos, dt, hero.Alive, phase, steering ? Mathf.DeltaAngle(bodyYaw, attacking ? committedYaw : aim) : 0f);
            if (ring.activeSelf != (strategicView && hero.Alive)) ring.SetActive(strategicView && hero.Alive);
            // The ground ring keeps its world size (about 1.2 tiles) whatever the figure's size, so the hero stays findable.
            ring.transform.localScale = new Vector3(0.9f, 0.004f, 0.9f) / Mathf.Max(0.01f, characterScale);
            UpdateSlash(dt, contactRules);
            var reach = !contactRules && predicted.HasValue && hero.Alive && slash <= 0f;
            if (reachArc.activeSelf != reach) reachArc.SetActive(reach);
            if (reach) reachArc.transform.SetPositionAndRotation(pos + Vector3.up * 0.004f, Quaternion.Euler(0f, aim, 0f)); // on the ground, not knee-high
        }

        /// <summary>The resolved swing: shown where and how it was resolved (it does not slide with the running hero).</summary>
        void UpdateSlash(float dt, bool contactRules)
        {
            if (slash > 0f && dt > 0f) slash = Mathf.Max(0f, slash - dt);
            var show = slash > 0f;
            var arc = contactRules ? contactArc : slashArc;
            var other = contactRules ? slashArc : contactArc;
            if (other.activeSelf) other.SetActive(false);
            if (arc.activeSelf != show) arc.SetActive(show);
            if (!show) return;
            arc.transform.SetPositionAndRotation(slashAt + Vector3.up * 0.005f, Quaternion.Euler(0f, slashYaw, 0f));
            if (contactRules) Prims.SetColor(contactMaterial, new Color(1f, 0.95f, 0.8f, 0.35f * slash / ContactSweepSeconds));
            else Prims.SetColor(slashMaterial, new Color(1f, 0.95f, 0.8f, 0.55f * slash / SlashSeconds));
        }

        /// <summary>
        /// The last step moved toward the next one by the fraction of a step elapsed. The next step is the simulation's own
        /// movement code run on a copy of the hero (same collision, acceleration and slide), so prediction and the real step
        /// agree; nothing is written.
        /// </summary>
        static Vector3 Predict(RunSimulation sim, Hero hero, HeroCommand c, float alpha)
        {
            sim.PredictHeroStep(c, out var nx, out var nz);
            var x = hero.UnitsX + (nx - hero.UnitsX) * alpha;
            var z = hero.UnitsZ + (nz - hero.UnitsZ) * alpha;
            return new Vector3(x / HeroRules.UnitsPerTile, 0f, z / HeroRules.UnitsPerTile);
        }

        /// <summary>
        /// release-rules-4 attack phase on the simulation's clock: 0..1 from acceptance to contact, then 1..2 through the
        /// recovery; -1 with no attack (or a cleared one). The shown time is the last completed step plus the elapsed fraction,
        /// so the contact pose (phase 1) lands on the frame whose step resolved the contact, and pause freezes it.
        /// </summary>
        static float AttackPhase(RunSimulation sim, Hero hero, float alpha)
        {
            if (!(sim.State == RunState.Night || sim.State == RunState.Day) || hero.AttackStartTick < 0 || !hero.Alive) return -1f;
            var p = hero.Profile;
            var since = sim.HeroClock - 1 + alpha - hero.AttackStartTick; // Night steps, or the hero's Day clock
            if (since < 0f) return -1f;
            if (since < p.WindUpTicks) return since / p.WindUpTicks;
            var recovery = since - p.WindUpTicks;
            var length = Mathf.Max(1, p.SwingIntervalTicks - p.WindUpTicks);
            return recovery >= length ? -1f : 1f + recovery / length;
        }

        void Animate(Vector3 pos, float dt, bool alive, float attackPhase, float aimOffset)
        {
            var travel = new Vector2(pos.x - lastShown.x, pos.z - lastShown.z);
            var moved = travel.magnitude;
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
            var windUp = 0f;
            // Sweep: the body winds right and snaps through left (ease-out), so the arc reads as a slash.
            // Eased in over the first 0.04 s and out over the last 0.1 s, so the swing never pops the body 45 degrees.
            var sweep = 0f;
            if (swing > 0f)
                sweep = Mathf.Lerp(-45f, 50f, 1f - Mathf.Pow(1f - progress, 3f))
                        * Mathf.Min(Mathf.SmoothStep(0f, 1f, progress / 0.13f), Mathf.SmoothStep(0f, 1f, (1f - progress) / 0.33f));
            if (profile != null && profile.ContactPhase)
            {
                // release-rules-4 on the simulation's ticks: wind up (blade back, body coiled right) until the contact step,
                // strike through at phase 1, then recover. The pose's peak is the contact, never a separate timer.
                var w = attackPhase >= 0f && attackPhase < 1f ? attackPhase : 0f;
                var r = attackPhase >= 1f ? attackPhase - 1f : -1f;
                windUp = attackPhase >= 0f && attackPhase < 1f ? Mathf.SmoothStep(0f, 1f, w) : 0f;
                strike = r >= 0f ? 1f - Mathf.SmoothStep(0f, 1f, r / 0.6f) : 0f;
                sweep = attackPhase < 0f ? 0f
                    : attackPhase < 1f ? Mathf.Lerp(0f, -40f, windUp)
                    : Mathf.Lerp(-40f, 45f, Mathf.Clamp01(r / 0.12f)) * (1f - Mathf.SmoothStep(0f, 1f, (r - 0.12f) / 0.6f));
            }
            var hit = flinch / FlinchSeconds;
            var t = 1f - jump / JumpSeconds;
            var hop = jump > 0f ? 4f * t * (1f - t) * JumpHeight / Mathf.Max(0.01f, transform.localScale.y) : 0f;
            // Getting up after a respawn: from a crouch (slack limbs), standing over the first part of the rise.
            var up = rise > 0f ? Mathf.Clamp01(rise / RiseSeconds) : 0f;
            var crouch = up * up;
            if (rig != null) rig.Pose(gaitPhase, jump > 0f ? 0.3f : gaitAmount, strike, windUp, hit, alive ? crouch * 0.45f : fall, jump > 0f ? 0f : run, idleClock);
            ShownAttackPhase = attackPhase;
            if (animRig != null)
            {
                // The figure's own frame: travel along its forward and right axes, from the shown root (camera-stable).
                var yaw = bodyYaw * Mathf.Deg2Rad;
                var fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                var right = new Vector2(fwd.y, -fwd.x);
                var v = dt > 0f ? travel / dt : Vector2.zero;
                animRig.Pose(new HeroPoseInput
                {
                    Forward = Vector2.Dot(v, fwd), Right = Vector2.Dot(v, right), TurnRate = turnRate, AimOffset = aimOffset,
                    AttackPhase = alive ? attackPhase : -1f, Hit = hit, Fall = alive ? 0f : fall, Crouch = alive ? crouch : 0f,
                    Airborne = jump > 0f, Dt = dt, BodyHeight = height,
                });
                sweep = 0f; // the adapter twists the torso itself
            }
            // The step-in: at the blade's contact the body has closed the gap to the nearest enemy it struck, so the sword
            // meets it at the simulation's reach (only a presentation offset; position, reach and hits are the simulation's).
            // A short step (at most 0.12 tile, about half a body height) toward the enemy it struck, not a 3 m lunge along the aim.
            var stepIn = Vector3.zero;
            if (strikeDistance > 0f)
            {
                var toward = new Vector3(strikeTarget.x - slashAt.x, 0f, strikeTarget.z - slashAt.z);
                var most = profile != null && profile.ContactPhase ? ContactStepIn : 0.12f;
                if (toward.sqrMagnitude > 1e-6f)
                    stepIn = transform.InverseTransformDirection(toward.normalized) * (Mathf.Clamp(strikeDistance - BladeReach * scale, 0f, most) / scale);
            }

            var bobY = rig != null || animRig != null ? 0f : Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.03f * gaitAmount; // the rig's hips bob in phase
            // release-rules-4: no forward lurch beyond the bounded step-in (at most 0.02 tile, cosmetic).
            var lurch = animRig != null ? 0f : strike * 0.08f;
            model.localPosition = new Vector3(0f, bobY + hop - crouch * 0.08f - (alive ? 0f : fall * 0.03f), lurch) + stepIn * strike;
            model.localRotation = Quaternion.Euler(alive ? 0f : fall * 80f, swing > 0f || (profile != null && profile.ContactPhase) ? sweep : 0f, alive ? bank : 0f);
        }
    }
}
