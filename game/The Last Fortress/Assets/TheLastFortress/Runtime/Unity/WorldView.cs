using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>Short presentation-only cues; never affect simulation, occupancy or timing.</summary>
    public enum CueKind
    {
        Hit = 0,
        Kill = 1,
        Collapse = 2,

        /// <summary>Muzzle flash at the top of a Tower the moment it fires.</summary>
        Fire = 3,

        /// <summary>Flat expanding ring: a Cannon shell's splash area or a Bomber's blast (plan 008).</summary>
        Blast = 4,

        /// <summary>A few cyan sparks rising from a repaired building (manual or dawn repair).</summary>
        Repair = 5,
    }

    /// <summary>
    /// Renders simulation state. Views are created and removed by diffing simulation IDs every frame;
    /// GameObjects never decide occupancy or outcome. All combat presentation (arrows, cues, gait, burn pulse, building
    /// reactions) advances only by the presentation delta passed to <see cref="Sync"/>, which the bootstrap sets to zero
    /// while Night is paused or the menu is open. It never drains simulation events: the bootstrap drains once per frame
    /// and hands the same batch to this view and the audio director.
    /// </summary>
    public sealed partial class WorldView : MonoBehaviour
    {
        /// <summary>Arrow flight time; a kill caused by the shot shows its cue when the arrow lands.</summary>
        public const float ArrowSeconds = 0.18f;

        /// <summary>Cannon shell flight: slower and higher than an arrow, so shell and splash read as one heavy shot.</summary>
        public const float ShellSeconds = 0.42f;

        const float ShellArc = 1.1f, ArrowArc = 0.4f;

        /// <summary>Hard caps on simultaneous cues per kind (oldest recycled first) and their lifetimes.</summary>
        public static readonly int[] CueCaps = { 40, 30, 12, 24, 12, 16 };
        public static readonly float[] CueSeconds = { 0.16f, 0.45f, 0.6f, 0.1f, 0.4f, 0.7f };

        /// <summary>Day/Night lighting blend (presentation only; the simulation starts Night immediately).</summary>
        public const float LightBlendSeconds = 0.8f;

        /// <summary>Below this HP fraction a building shows its damaged state (smoke).</summary>
        public const float DamagedFraction = 0.5f;

        sealed class Cue
        {
            public CueKind Kind;
            public GameObject Go;
            public Transform[] Parts;
            public Vector3 Position;
            public float Age;
            public float Delay;

            /// <summary>Blast ring radius in tiles (Blast cues only).</summary>
            public float Radius;
        }
        sealed class BuildingView
        {
            public Building Building;
            public GameObject Root;
            public Transform HpFill;
            public GameObject HpBar;
            public float Flash;

            /// <summary>Wall connection mask the current mesh was built for (-1 for non-Walls).</summary>
            public int WallMask = -1;

            public int Orientation;

            /// <summary>Smoke shown while damaged (lazily created) and the House's night window glow.</summary>
            public GameObject Smoke;
            public GameObject Glow;
            public float Recoil;

            /// <summary>Cannon only: the pivot of the parts that aim, its muzzle, and the direction held after a shot.</summary>
            public Transform Turret;

            public Transform Muzzle;
            public Vector3 AimDirection;
            public float AimHold;

            /// <summary>Torch tips (Keep, Archer Tower) that flicker at night, and the particle emission accumulators.</summary>
            public Transform[] Torches;

            public float TorchAccum, FireAccum;

            /// <summary>Authored Archer Tower: the bow (child of the archer, which is the Turret) and its draw/release state.</summary>
            public Transform Bow;

            public Vector3 BowRest;
            public float Draw, Release;

            /// <summary>Amber light at the tower torch, lit at night (authored art only).</summary>
            public Light TorchLight;
        }

        sealed class EnemyView
        {
            public Enemy Enemy;
            public GameObject Root;

            /// <summary>Animated visual child; the root keeps the simulation position, HP bar and footprint.</summary>
            public Transform Model;

            public Transform HpFill;
            public GameObject HpBar;
            public GameObject Burn;

            /// <summary>Where the burn flame and the fuse spark sit on the model at size 1; they follow the figure's shown size.</summary>
            public Vector3 BurnAt, FuseAt;

            /// <summary>Bomber fuse spark (shown while lit) and Siege Commander aura ring (always shown while alive).</summary>
            public GameObject Fuse;

            public GameObject Aura;
            public Vector3 LastPosition;
            public float GaitPhase;
            public float GaitAmount;
            public float Lunge;
            public float WindUp;

            /// <summary>Whether the view has been turned to its first facing (later turns are rate-limited).</summary>
            public bool Oriented;

            /// <summary>HP fraction drawn on the bar: it waits for a projectile in flight, then drains quickly.</summary>
            public float ShownFraction = 1f;

            public float BarHold;

            /// <summary>Fixed world offset so enemies sharing a lane do not stand inside each other (presentation only, never turns).</summary>
            public Vector3 Offset;

            /// <summary>How much of <see cref="Offset"/> is applied: eases from 0 after the spawn (plan 016 walk-ins).</summary>
            public float OffsetIn;

            /// <summary>Bomber: the wick tip and the hand torch (particle sources), and emission accumulators.</summary>
            public Transform Wick, Torch;

            public float Height, BurnAccum, WickAccum, TorchAccum;

            /// <summary>0..1 forward strike building up over the last moment before a hit is due.</summary>
            public float StrikeRise;

            /// <summary>Authored part rig (null for blockouts), the light kinds' short wind-up, and the hit flinch.</summary>
            public CharacterRig Rig;

            public float LightWind, Hit, HitDelay;

            /// <summary>
            /// How far (root units) the strike carries the body toward its target (plan 013): enough for the weapon to meet
            /// the building's face or the hero, whatever the figure's shown size. Presentation only; reach is the simulation's.
            /// </summary>
            public float Reach = DefaultReach;

            /// <summary>World width of the HP bar at scale 1 (bar width times the root's enemy scale).</summary>
            public float BarWidth;

            /// <summary>How far (root units) the engaged figure has walked toward its target's face (eases toward Reach).</summary>
            public float Approach;

            /// <summary>Last shown velocity (world/s), gait rate and run amount: a body killed by a projectile keeps walking until it lands.</summary>
            public Vector3 Velocity;

            public float PhasePerTile, Run;
        }

        const float DefaultReach = 0.12f;

        /// <summary>Standing height of each kind's model in model units (before the enemy and hero-view scales).</summary>
        static float BodyHeight(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return 0.55f;
                case EnemyKind.Brute: return 0.78f;
                case EnemyKind.Bomber: return 0.52f;
                case EnemyKind.SiegeCommander: return 0.75f; // a 2.1 m man (plan 014), the banner rises above
                case EnemyKind.SiegeGolem: return 1.2f;
                default: return 0.6f;
            }
        }

        /// <summary>
        /// Strike reach in root units for an enemy at <paramref name="at"/> engaging a building or the hero: the gap from its
        /// weapon's reach (about 0.45 of its shown height) to the building's face (footprint inset 0.07) or the hero's body,
        /// capped so a long gap reads as a step-in, never a teleport.
        /// </summary>
        float ContactReach(RunSimulation sim, Enemy e, EnemyView view, Vector3 at)
        {
            var rootScale = Mathf.Max(0.01f, view.Root.transform.localScale.x);
            var body = BodyHeight(e.Kind) * rootScale * CharacterScale;
            float gap;
            if (e.TargetsHero && sim.Hero != null)
                gap = new Vector2((float)sim.Hero.X - at.x, (float)sim.Hero.Z - at.z).magnitude - 0.1f * CharacterScale;
            else if (e.TargetBuildingId >= 0 && sim.GetBuilding(e.TargetBuildingId) is Building b)
            {
                var size = b.Definition.FootprintSize;
                const float inset = 0.07f;
                var dx = Mathf.Max(Mathf.Max(b.Cell.X + inset - at.x, 0f), at.x - (b.Cell.X + size - inset));
                var dz = Mathf.Max(Mathf.Max(b.Cell.Z + inset - at.z, 0f), at.z - (b.Cell.Z + size - inset));
                gap = Mathf.Sqrt(dx * dx + dz * dz);
            }
            else return DefaultReach * CharacterScale;

            return Mathf.Clamp(gap - 0.45f * body, 0.03f * rootScale * CharacterScale, 0.6f) / rootScale;
        }

        sealed class ArrowFx
        {
            public GameObject Go;
            public Vector3 From, To;
            public float T;

            /// <summary>Cannon shell: lands with a splash ring of this radius (0 for arrows).</summary>
            public float Splash;

            public float Duration, Arc, Smoke;
        }

        readonly List<Cue> cues = new List<Cue>();
        readonly Stack<Cue>[] cuePools = { new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>() };
        readonly HashSet<int> shotTargets = new HashSet<int>();
        Material cueHit, cueKill, cueDebris, smokeMat, glowMat, blastMat, repairMat, auraMat, enemyArrowMat;
        FxParticles particles;

        /// <summary>Short point-light flashes (explosions, cannon fire); a few pooled lights, never one per effect.</summary>
        sealed class FlashLight
        {
            public Light Light;
            public float Age, Life, Peak;
        }

        readonly List<FlashLight> flashes = new List<FlashLight>();

        /// <summary>Live particle count (diagnostics).</summary>
        public int ParticleCount => particles?.LiveCount ?? 0;
        readonly Stack<GameObject> shellPool = new Stack<GameObject>();
        float fxTime;

        /// <summary>0 = day lighting, 1 = night lighting; eased toward the phase by the presentation clock.</summary>
        public float NightBlend { get; private set; }

        /// <summary>Delay from the hero's swing event to the blade's contact in the shown swing.</summary>
        public const float HeroBladeDelay = 0.08f;

        readonly List<(int enemy, float left)> pendingSparks = new List<(int, float)>();

        /// <summary>Sparks off enemies the hero's blade reaches, when the shown swing connects (presentation clock).</summary>
        void EmitHeroSparks(float dt)
        {
            for (var i = pendingSparks.Count - 1; i >= 0; i--)
            {
                var (id, left) = pendingSparks[i];
                left -= dt;
                if (left > 0f)
                {
                    pendingSparks[i] = (id, left);
                    continue;
                }

                pendingSparks.RemoveAt(i);
                Vector3 at;
                if (enemyViews.TryGetValue(id, out var v)) at = v.Root.transform.position;
                else
                {
                    var body = dying.Find(d => d.Id == id); // killed by this swing: sparks at its falling body
                    if (body == null) continue;
                    at = body.Root.transform.position;
                }
                at += Vector3.up * 0.3f * CharacterScale;
                for (var k = 0; k < 4; k++)
                    particles.Emit(at, Vector3.up * particles.Range(0.6f, 1.2f) + particles.InSphere(1.1f), particles.Range(0.15f, 0.3f), 0.03f, 0.008f, Spark, SparkEnd, 6f, 0.5f);
            }
        }

        /// <summary>
        /// Size of the walking figures relative to their model size: true scale against the buildings, the same in both views
        /// (see <see cref="PrototypeBootstrap.FigureScale"/>).
        /// </summary>
        public float CharacterScale { get; set; } = 1f;

        /// <summary>Clear colour of the strategic camera for the current light (kept even while the hero view renders).</summary>
        public Color? StrategicBackground { get; private set; }


        /// <summary>Event batches and events received from the bootstrap (diagnostics: one delivery per batch).</summary>
        public int BatchesSeen { get; private set; }
        public int EventsSeen { get; private set; }

        public bool IsShowingDamage(int buildingId) => buildingViews.TryGetValue(buildingId, out var v) && v.Smoke != null && v.Smoke.activeSelf;

        public int ActiveArrowCount => arrows.Count;

        public IEnumerable<Vector3> ArrowTargets()
        {
            foreach (var a in arrows) yield return a.To;
        }

        public IEnumerable<Vector3> ArrowPositions()
        {
            foreach (var a in arrows) yield return a.Go.transform.position;
        }

        /// <summary>Cues currently visible (delay elapsed), with their world position.</summary>
        public IEnumerable<(CueKind kind, Vector3 position)> VisibleCues()
        {
            foreach (var c in cues)
                if (c.Go.activeSelf) yield return (c.Kind, c.Position);
        }

        public int PendingCueCount => cues.Count;

        /// <summary>Local offset of an enemy's animated model (gait/contact), or null when it has no view.</summary>
        public Vector3? EnemyModelOffset(int enemyId) => enemyViews.TryGetValue(enemyId, out var v) ? v.Model.localPosition : (Vector3?)null;

        /// <summary>How far into its walk an enemy's shown gait is (0 standing .. 1 walking), or null when it has no view.</summary>
        public Vector3? EnemyShownPosition(int enemyId) => enemyViews.TryGetValue(enemyId, out var v) ? v.Root.transform.position : (Vector3?)null;

        public float? EnemyGait(int enemyId) => enemyViews.TryGetValue(enemyId, out var v) ? v.GaitAmount : (float?)null;

        PrototypeContent content;
        Light sun;
        Transform buildingsRoot, enemiesRoot, fxRoot;
        GameObject buildGrid;
        Light keepLight;
        Material hpBack, hpFill, hpFillEnemy;

        readonly Dictionary<int, BuildingView> buildingViews = new Dictionary<int, BuildingView>();
        readonly Dictionary<int, EnemyView> enemyViews = new Dictionary<int, EnemyView>();
        readonly List<ArrowFx> arrows = new List<ArrowFx>();

        /// <summary>
        /// Views of enemies killed or buildings destroyed this run, kept briefly for a death/collapse animation instead of
        /// vanishing on the simulation step. Presentation only: the simulation already removed them.
        /// </summary>
        sealed class Dying
        {
            public GameObject Root;
            public Transform Model;
            public bool Building;
            public float Delay, Age;
            public Vector3 BasePosition;

            /// <summary>Parts hidden when the fall starts (HP bar, burn, fuse, aura): the bar stays up until the hit lands.</summary>
            public GameObject[] Hide;

            public CharacterRig Rig;

            /// <summary>Enemy only: its id (sparks at the blade's contact), and the walk it carried in from life.</summary>
            public int Id = -1;

            public Vector3 Velocity;
            public float GaitPhase, GaitAmount, PhasePerTile, Run;
        }

        readonly List<Dying> dying = new List<Dying>();
        readonly HashSet<int> detonatedThisFrame = new HashSet<int>();
        readonly HashSet<int> destroyedThisFrame = new HashSet<int>();
        readonly Dictionary<int, float> killDelay = new Dictionary<int, float>();

        /// <summary>Enemies hit by a projectile drawn this frame, with that projectile's flight time.</summary>
        readonly Dictionary<int, float> shotFlight = new Dictionary<int, float>();
        MaterialPropertyBlock cueBlock;

        /// <summary>Enemy fall and building collapse lengths, and the most of them shown at once (oldest finish first).</summary>
        public const float EnemyDeathSeconds = 1.15f, BuildingCollapseSeconds = 0.45f;

        /// <summary>An enemy's fall: tipped over by this age, lying still until <see cref="EnemyLieSeconds"/>, then sinking.</summary>
        const float EnemyFallSeconds = 0.45f, EnemyLieSeconds = 0.85f;

        public const int MaxDying = 96;

        public int DyingCount => dying.Count;
        readonly Stack<GameObject> arrowPool = new Stack<GameObject>();
        readonly List<int> scratch = new List<int>();
        bool? nightTarget;
        GameObject terrainRoot;
        Material poolWater, shallowWater, bankSand, bridgeTimber;
        readonly List<Mesh> terrainMeshes = new List<Mesh>();
        int topologyRevision = -1;
        int topologyReset = -1;

        public int BuildingViewCount => buildingViews.Count;

        /// <summary>Number of Wall meshes rebuilt because their connections changed (diagnostic: no per-frame regeneration).</summary>
        public int WallRebuildCount { get; private set; }

        public int TerrainDecorationCount { get; private set; }

        public int WallMaskOf(int buildingId) => buildingViews.TryGetValue(buildingId, out var v) ? v.WallMask : -1;

        public float ViewYaw(int buildingId) => buildingViews.TryGetValue(buildingId, out var v) ? v.Root.transform.eulerAngles.y : float.NaN;

        bool ConnectedWalls => content.wallShapePrefabs != null && content.wallShapePrefabs.Length == 6 && content.wallShapePrefabs[0] != null;
        public int EnemyViewCount => enemyViews.Count;

        public int CountBuildingViews(BuildingKind kind)
        {
            var n = 0;
            foreach (var v in buildingViews.Values)
                if (v.Building.Kind == kind) n++;
            return n;
        }

        public bool HasBuildingView(int id) => buildingViews.ContainsKey(id);

        /// <summary>Whether this enemy's view shows a lit fuse / a Commander aura disc (diagnostics).</summary>
        public bool FuseVisible(int enemyId) => enemyViews.TryGetValue(enemyId, out var v) && v.Fuse != null && v.Fuse.activeSelf;

        public bool AuraVisible(int enemyId) => enemyViews.TryGetValue(enemyId, out var v) && v.Aura != null && v.Aura.activeInHierarchy;
        public bool HasEnemyView(int id) => enemyViews.ContainsKey(id);
        public bool BuildGridVisible => buildGrid.activeSelf;

        public void Initialize(PrototypeContent content, Light sun)
        {
            this.content = content;
            this.sun = sun;
            buildingsRoot = new GameObject("Buildings").transform;
            buildingsRoot.SetParent(transform, false);
            enemiesRoot = new GameObject("Enemies").transform;
            enemiesRoot.SetParent(transform, false);
            fxRoot = new GameObject("Fx").transform;
            fxRoot.SetParent(transform, false);
            hpBack = Prims.Tinted(content.overlay, new Color(0.1f, 0.1f, 0.1f, 0.85f));
            hpFill = Prims.Tinted(content.overlay, Palette.RouteCyan);
            hpFillEnemy = Prims.Tinted(content.overlay, Palette.WarningCoral);
            // Unsorted transparent overlay (no depth write): draw fills after the dark back so the back never covers them.
            hpFill.renderQueue = hpFillEnemy.renderQueue = hpBack.renderQueue + 1;
            cueHit = Prims.Tinted(content.overlay, Palette.Amber);
            cueKill = Prims.Tinted(content.overlay, Palette.WarningCoral);
            cueDebris = content.timberShadow;
            smokeMat = Prims.Tinted(content.overlay, new Color(0.42f, 0.4f, 0.38f, 0.7f));
            glowMat = Prims.Tinted(content.overlay, new Color(1f, 0.78f, 0.42f, 0.95f));
            blastMat = Prims.Tinted(content.overlay, Palette.Amber.WithAlpha(0.32f)); // shows the area without hiding who is in it
            repairMat = Prims.Tinted(content.overlay, Palette.RouteCyan.WithAlpha(0.9f));
            auraMat = Prims.Tinted(content.overlay, Palette.WarningCoral.WithAlpha(0.16f));
            enemyArrowMat = Prims.Tinted(content.overlay, Palette.WarningCoral);
            particles = new FxParticles(fxRoot, content.overlay);
            for (var i = 0; i < 4; i++)
            {
                var go = new GameObject("FlashLight");
                go.transform.SetParent(fxRoot, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                go.SetActive(false);
                flashes.Add(new FlashLight { Light = l });
            }

            art = content.art;
            if (art != null) InitializeArt();
            BuildBoard();
            var keepLightGo = new GameObject("KeepTorchLight");
            keepLightGo.transform.SetParent(transform, false);
            keepLightGo.transform.position = new Vector3(10.5f, 2.7f, 10.5f); // above the real-scale Keep's roof
            keepLight = keepLightGo.AddComponent<Light>();
            keepLight.type = LightType.Point;
            keepLight.color = Palette.Amber;
            keepLight.range = 7f;
            keepLight.intensity = 0f;
            keepLight.shadows = LightShadows.None;
            ApplyLighting(0f);
        }

        // ------------------------------------------------------------------ authored look (plan 011)

        ArtDirection art;

        /// <summary>Runtime copies of authored materials the view animates (never the assets themselves, so play mode leaves them untouched).</summary>
        Material windowSource, windowMat, riverMat, fordMat;

        UnityEngine.Rendering.Volume dayVolume, nightVolume;

        public bool HasArt => art != null;

        void InitializeArt()
        {
            riverMat = art.river != null ? new Material(art.river) : null;
            fordMat = art.ford != null ? new Material(art.ford) : null;
            // The window material glows with the night; find it on the Keep so the prefabs need no extra reference.
            if (content.keepPrefab != null)
                foreach (var r in content.keepPrefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name == "ArtWindow") windowSource = m;
            if (windowSource != null) windowMat = new Material(windowSource);
            InitializeTerrain();
            dayVolume = CreateVolume("DayVolume", art.dayProfile, 1f);
            nightVolume = CreateVolume("NightVolume", art.nightProfile, 0f);
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = true;
            else if (cam != null) cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        }

        UnityEngine.Rendering.Volume CreateVolume(string name, UnityEngine.Rendering.VolumeProfile profile, float weight)
        {
            if (profile == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<UnityEngine.Rendering.Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
            v.weight = weight;
            return v;
        }

        /// <summary>Swaps the shared window material for the view's night-reactive copy on one instantiated model.</summary>
        void UseRuntimeMaterials(GameObject root)
        {
            if (windowMat == null) return;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != windowSource) continue;
                    mats[i] = windowMat;
                    changed = true;
                }

                if (changed) r.sharedMaterials = mats;
            }
        }

        static void Tile(GameObject go, float tiling)
        {
            var block = new MaterialPropertyBlock();
            block.SetVector("_BaseMap_ST", new Vector4(tiling, tiling, 0f, 0f));
            go.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        void BuildBoard()
        {
            var board = new GameObject("Board").transform;
            board.SetParent(transform, false);
            // With authored terrain the ground and water are its meshes, rebuilt per map in SetTerrain; without art (the
            // prototype scene) a plain plateau stands over a flat water plane.
            if (!HasTerrain)
            {
                var ground = Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.1f, 10f), new Vector3(20f, 0.2f, 20f), art?.ground != null ? art.ground : content.grass, "Ground");
                ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (art?.ground != null) Tile(ground, art.groundTiling);
                var edge = Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.45f, 10f), new Vector3(20.4f, 0.5f, 20.4f), art?.cliff != null ? art.cliff : content.timberShadow, "BoardEdge");
                if (art?.cliff != null) Tile(edge, 8f);
                var water = Prims.Create(PrimitiveType.Plane, board, new Vector3(10f, -0.3f, 10f), new Vector3(8f, 1f, 8f), content.water, "DecorativeWater");
                water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            }

            buildGrid = new GameObject("BuildGrid");
            buildGrid.transform.SetParent(board, false);
            var lineMat = Prims.Tinted(content.overlay, new Color(1f, 1f, 1f, 0.18f));
            var ringMat = Prims.Tinted(content.overlay, Palette.WarningCoral.WithAlpha(0.22f));
            buildGrid.AddComponent<MeshFilter>().sharedMesh = GridLinesMesh();
            buildGrid.AddComponent<MeshRenderer>().sharedMaterial = lineMat;
            var ring = new GameObject("ReservedRing");
            ring.transform.SetParent(buildGrid.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = ReservedRingMesh();
            ring.AddComponent<MeshRenderer>().sharedMaterial = ringMat;
            buildGrid.SetActive(false);
        }

        static Mesh GridLinesMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const float w = 0.025f, y = 0.012f;
            for (var i = 0; i <= GridCoordinates.Size; i++)
            {
                AddQuad(verts, tris, new Vector3(i - w, y, 0), new Vector3(i + w, y, GridCoordinates.Size));
                AddQuad(verts, tris, new Vector3(0, y, i - w), new Vector3(GridCoordinates.Size, y, i + w));
            }

            var mesh = new Mesh { name = "GridLines" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh ReservedRingMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const float y = 0.008f;
            for (var x = 0; x < GridCoordinates.Size; x++)
            for (var z = 0; z < GridCoordinates.Size; z++)
            {
                if (!GridCoordinates.IsReservedRing(new GridPos(x, z))) continue;
                // diagonal hatch: two thin strips per cell so the ring reads without relying on color
                AddQuad(verts, tris, new Vector3(x + 0.04f, y, z + 0.04f), new Vector3(x + 0.96f, y, z + 0.96f));
            }

            var mesh = new Mesh { name = "ReservedRing" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddQuad(List<Vector3> v, List<int> t, Vector3 min, Vector3 max)
        {
            var i = v.Count;
            v.Add(new Vector3(min.x, min.y, min.z));
            v.Add(new Vector3(min.x, min.y, max.z));
            v.Add(new Vector3(max.x, min.y, max.z));
            v.Add(new Vector3(max.x, min.y, min.z));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void SetBuildGridVisible(bool visible)
        {
            if (buildGrid.activeSelf != visible) buildGrid.SetActive(visible);
        }

        /// <summary>Decorative meshes for forest/rock/water cells (no colliders, no hidden blockers); rebuilt per run.</summary>
        public void SetTerrain(TerrainMap map)
        {
            if (terrainRoot != null) Destroy(terrainRoot);
            groundDetail.Clear(); // its tufts die with terrainRoot, also when no new terrain follows
            groundDetailRevision = -1;
            foreach (var m in terrainMeshes) Destroy(m);
            terrainMeshes.Clear();
            terrainRoot = null;
            TerrainDecorationCount = 0;
            if (HasTerrain && map == null)
            {
                terrainRoot = new GameObject("Terrain");
                terrainRoot.transform.SetParent(transform, false);
                BuildTerrain(null); // flat ground under the title or a map-less run, never a hole
                return;
            }

            if (map == null || content.foliage == null) return;
            if (poolWater == null) poolWater = riverMat != null ? riverMat : Prims.Tinted(content.water, Palette.Hex(0x3B6E8C));
            terrainRoot = new GameObject("Terrain");
            terrainRoot.transform.SetParent(transform, false);
            for (var i = 0; i < map.Cells.Length; i++)
            {
                var type = map.Cells[i];
                if (type == TerrainType.Land) continue;
                var cell = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                var root = new GameObject($"{type} {cell}").transform;
                root.SetParent(terrainRoot.transform, false);
                root.position = Prims.CellCenter(cell);
                var h = (cell.X * 73 + cell.Z * 151) % 7 / 7f; // stable per-cell variation, not gameplay randomness
                if (art != null && art.pine != null && type != TerrainType.Water)
                {
                    PlaceArtCell(root, type, cell, h);
                    TerrainDecorationCount++;
                    continue;
                }

                switch (type)
                {
                    case TerrainType.Forest:
                        for (var t = 0; t < 2; t++)
                        {
                            var p = new Vector3(t == 0 ? -0.18f : 0.2f, 0, t == 0 ? 0.15f : -0.17f);
                            var s = 0.8f + 0.25f * ((h + t * 0.37f) % 1f);
                            Prims.Create(PrimitiveType.Cube, root, p + new Vector3(0, 0.15f * s, 0), new Vector3(0.09f, 0.3f * s, 0.09f), content.timberShadow, "Trunk");
                            Prims.Create(PrimitiveType.Cube, root, p + new Vector3(0, 0.42f * s, 0), new Vector3(0.36f * s, 0.34f * s, 0.36f * s), content.foliage, "Canopy", Quaternion.Euler(0, 45 + h * 40, 0));
                            Prims.Create(PrimitiveType.Cube, root, p + new Vector3(0, 0.66f * s, 0), new Vector3(0.22f * s, 0.24f * s, 0.22f * s), content.foliage, "CanopyTop", Quaternion.Euler(0, h * 40, 0));
                        }

                        break;
                    case TerrainType.Rock:
                        Prims.Create(PrimitiveType.Cube, root, new Vector3(-0.08f, 0.18f, 0.02f), new Vector3(0.5f, 0.36f, 0.44f), content.rock, "Boulder", Quaternion.Euler(8, 30 + h * 60, 6));
                        Prims.Create(PrimitiveType.Cube, root, new Vector3(0.22f, 0.1f, -0.2f), new Vector3(0.26f, 0.2f, 0.24f), content.rock, "Stone", Quaternion.Euler(-6, 10 + h * 80, 0));
                        break;
                    case TerrainType.Water:
                        break; // drawn below as one connected surface with the river's fords
                }

                TerrainDecorationCount++;
            }

            if (HasTerrain) BuildTerrain(map);
            BuildWater(map);
            if (art != null && art.tuft != null) BuildGroundDetail(map);
        }

        /// <summary>Two trees (pine or broadleaf by cell) and a bush per forest cell; one boulder group per rock cell. Inside the cell.</summary>
        void PlaceArtCell(Transform root, TerrainType type, GridPos cell, float h)
        {
            if (type == TerrainType.Forest)
            {
                for (var t = 0; t < 2; t++)
                {
                    var p = new Vector3(t == 0 ? -0.18f : 0.2f, 0, t == 0 ? 0.15f : -0.17f);
                    var pick = ((cell.X * 31 + cell.Z * 17 + t * 7) % 5) < 3 ? art.pine : art.broadleaf;
                    Prop(pick, root, p, (h * 360f + t * 137f) % 360f, 0.95f + 0.3f * ((h + t * 0.37f) % 1f));
                }

                if (art.bush != null) Prop(art.bush, root, new Vector3(0.22f, 0f, 0.22f) * (h > 0.5f ? 1f : -1f), h * 200f, 1f);
            }
            else if (type == TerrainType.Rock) Prop(art.rockLarge, root, Vector3.zero, 30f + h * 300f, 0.95f + 0.15f * h);
        }

        GameObject Prop(GameObject prefab, Transform parent, Vector3 local, float yaw, float scale)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Ground tufts on open land, by cell (index -> object), hidden while a building stands on the cell.</summary>
        readonly Dictionary<GridPos, GameObject> groundDetail = new Dictionary<GridPos, GameObject>();

        int groundDetailRevision = -1;

        public int GroundDetailCount => groundDetail.Count;

        /// <summary>
        /// Plan 010 stage 2.1 for the art pass: low tufts (under 0.08, no shadow) on Land cells only, outside a readability
        /// mask (the reserved ring where the entries are, the Keep and its neighbours, water banks, fords and the cells
        /// either side of each ford). Reeds and pebbles go in water cells along the banks, which are impassable anyway.
        /// Derived from the played map each run; deterministic; never touches the simulation.
        /// </summary>
        void BuildGroundDetail(TerrainMap map)
        {
            groundDetail.Clear();
            groundDetailRevision = -1;
            var mask = new HashSet<GridPos>();
            var crossings = new HashSet<GridPos>(MapProfiles.RiverCrossings(map));
            for (var i = 0; i < map.Cells.Length; i++)
            {
                var cell = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                if (map.Cells[i] != TerrainType.Land || crossings.Contains(cell)) Around(mask, cell, map.Cells[i] == TerrainType.Water || crossings.Contains(cell) ? 1 : 0);
            }

            Around(mask, GridState.KeepCell, 1);
            var detail = new GameObject("GroundDetail").transform;
            detail.SetParent(terrainRoot.transform, false);
            for (var i = 0; i < map.Cells.Length; i++)
            {
                var cell = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                var hash = (cell.X * 92821 + cell.Z * 68917) % 100;
                if (map.Cells[i] == TerrainType.Water && !crossings.Contains(cell))
                {
                    // Reeds and a pebble on the bank side of some water cells (water is never walkable or buildable).
                    if (hash % 3 != 0 || art.reeds == null) continue;
                    var side = new Vector3(((hash / 3) % 3 - 1) * 0.3f, 0f, ((hash / 9) % 3 - 1) * 0.3f);
                    Prop(art.reeds, detail, Prims.CellCenter(cell) + side + Vector3.up * (HasTerrain ? RiverLevel - 0.03f : WaterY), hash * 7f, 0.9f + hash % 4 * 0.1f);
                    continue;
                }

                if (map.Cells[i] != TerrainType.Land || GridCoordinates.IsReservedRing(cell) || mask.Contains(cell) || hash >= 22) continue;
                var offset = new Vector3((hash % 7 - 3) * 0.1f, 0f, (hash % 5 - 2) * 0.12f);
                // True scale next to a 0.22-tile person (plan 013): ankle-high grass, not knee-high; the ground shader carries the rest.
                groundDetail[cell] = Prop(art.tuft, detail, Prims.CellCenter(cell) + offset, hash * 13f, HasTerrain ? 0.45f + (hash % 3) * 0.1f : 1f + (hash % 3) * 0.25f);
            }
        }

        static void Around(HashSet<GridPos> set, GridPos c, int r)
        {
            for (var dx = -r; dx <= r; dx++)
            for (var dz = -r; dz <= r; dz++)
                set.Add(new GridPos(c.X + dx, c.Z + dz));
        }

        /// <summary>Hides ground detail under buildings; rechecked only when occupancy changes.</summary>
        void SyncGroundDetail(RunSimulation sim)
        {
            if (groundDetail.Count == 0 || sim.Grid.Revision == groundDetailRevision) return;
            groundDetailRevision = sim.Grid.Revision;
            foreach (var pair in groundDetail)
            {
                var free = sim.Grid.GetOccupant(pair.Key) == null;
                if (pair.Value.activeSelf != free) pair.Value.SetActive(free);
            }
        }

        const float BankWidth = 0.12f;
        const float WaterY = 0.02f;
        const float BankY = 0.03f;

        /// <summary>
        /// Water as connected surfaces: a cell's water runs to every side whose neighbor is also water (or a ford / ring
        /// crossing on a Crossing river) and has a sand bank on the others, so rivers read as one stream and pools as ponds.
        /// Fords get shallow water and stepping stones and ring crossings a bridge. Decoration only: everything stays below the route lines and never touches the simulation.
        /// </summary>
        void BuildWater(TerrainMap map)
        {
            if (shallowWater == null) shallowWater = fordMat != null ? fordMat : Prims.Tinted(content.water, Palette.Hex(0x6A9DB2));
            if (bankSand == null) bankSand = art?.bank != null ? art.bank : Prims.Tinted(content.rock, Palette.Hex(0xC9B98F));
            if (bridgeTimber == null) bridgeTimber = art?.bridge != null ? art.bridge : Prims.Tinted(content.timberShadow, Palette.Hex(0x8A6A48));
            var crossings = new HashSet<GridPos>(MapProfiles.RiverCrossings(map));
            bool IsRiver(GridPos c) => GridCoordinates.IsInBounds(c) && (map[c] == TerrainType.Water || crossings.Contains(c));

            var deep = (new List<Vector3>(), new List<int>());
            var shallow = (new List<Vector3>(), new List<int>());
            var banks = (new List<Vector3>(), new List<int>());
            var props = new GameObject("RiverProps").transform;
            props.SetParent(terrainRoot.transform, false);
            for (var i = 0; i < map.Cells.Length; i++)
            {
                var cell = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                var ford = crossings.Contains(cell);
                if (map.Cells[i] != TerrainType.Water && !ford) continue;
                bool Joined(int dx, int dz)
                {
                    var n = new GridPos(cell.X + dx, cell.Z + dz);
                    return IsRiver(n) || (ford && !GridCoordinates.IsInBounds(n)); // a ring crossing flows on off the board
                }

                bool w = Joined(-1, 0), e = Joined(1, 0), s = Joined(0, -1), n = Joined(0, 1);
                float x0 = cell.X, z0 = cell.Z, x1 = cell.X + 1, z1 = cell.Z + 1;
                var ring = GridCoordinates.IsReservedRing(cell);
                AddQuad((ford && !ring ? shallow : deep).Item1, (ford && !ring ? shallow : deep).Item2,
                    new Vector3(x0 + (w ? 0 : BankWidth), WaterY, z0 + (s ? 0 : BankWidth)), new Vector3(x1 - (e ? 0 : BankWidth), WaterY, z1 - (n ? 0 : BankWidth)));
                if (!w) AddQuad(banks.Item1, banks.Item2, new Vector3(x0, BankY, z0), new Vector3(x0 + BankWidth, BankY, z1));
                if (!e) AddQuad(banks.Item1, banks.Item2, new Vector3(x1 - BankWidth, BankY, z0), new Vector3(x1, BankY, z1));
                if (!s) AddQuad(banks.Item1, banks.Item2, new Vector3(x0, BankY, z0), new Vector3(x1, BankY, z0 + BankWidth));
                if (!n) AddQuad(banks.Item1, banks.Item2, new Vector3(x0, BankY, z1 - BankWidth), new Vector3(x1, BankY, z1));
                if (!ford) continue;

                // The river runs along z when it continues north/south of this cell; enemies cross it along the other axis.
                var alongZ = n || s;
                var across = alongZ ? Vector3.right : Vector3.forward;
                var center = Prims.CellCenter(cell);
                if (ring)
                {
                    var deck = Prims.Create(PrimitiveType.Cube, props, center + new Vector3(0, 0.04f, 0),
                        alongZ ? new Vector3(1f, 0.03f, 0.78f) : new Vector3(0.78f, 0.03f, 1f), bridgeTimber, "BridgeDeck");
                    deck.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    foreach (var side in new[] { -0.39f, 0.39f })
                        Prims.Create(PrimitiveType.Cube, props, center + (alongZ ? Vector3.forward : Vector3.right) * side + new Vector3(0, 0.055f, 0),
                            alongZ ? new Vector3(1f, 0.03f, 0.05f) : new Vector3(0.05f, 0.03f, 1f), content.timberShadow, "BridgeRail");
                    if (HasTerrain)
                        foreach (var side in new[] { -0.36f, 0.36f })
                        foreach (var end in new[] { -0.42f, 0.42f })
                        {
                            // Piers down to the carved channel floor: the deck visibly spans the river instead of floating.
                            var at = center + (alongZ ? Vector3.forward : Vector3.right) * side + across * end;
                            var pier = Prims.Create(PrimitiveType.Cube, props, new Vector3(at.x, (ChannelFloor + 0.03f) * 0.5f, at.z), new Vector3(0.07f, 0.03f - ChannelFloor, 0.07f), bridgeTimber, "BridgePier");
                            pier.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                }
                else
                {
                    var h = (cell.X * 73 + cell.Z * 151) % 7 / 7f;
                    for (var k = -1; k <= 1; k++)
                    {
                        var jitter = (alongZ ? Vector3.forward : Vector3.right) * (((h + k * 0.31f) % 1f) - 0.5f) * 0.2f;
                        if (art?.fordStone != null) Prop(art.fordStone, props, center + across * (k * 0.3f) + jitter + new Vector3(0, 0.02f, 0), 20 + h * 50 + k * 25, 1f + 0.15f * k * k);
                        else
                            Prims.Create(PrimitiveType.Cube, props, center + across * (k * 0.3f) + jitter + new Vector3(0, 0.035f, 0),
                                new Vector3(0.17f, 0.04f, 0.15f), content.rock, "SteppingStone", Quaternion.Euler(0, 20 + h * 50 + k * 25, 0));
                    }
                }
            }

            if (HasTerrain) return; // channels, banks and water are part of the terrain meshes; only the props above remain
            MeshChild("RiverWater", deep, poolWater);
            MeshChild("Ford", shallow, shallowWater);
            MeshChild("RiverBank", banks, bankSand);
        }

        void MeshChild(string name, (List<Vector3>, List<int>) data, Material material)
        {
            if (data.Item2.Count == 0) return;
            var mesh = new Mesh { name = name };
            terrainMeshes.Add(mesh);
            mesh.SetVertices(data.Item1);
            mesh.SetTriangles(data.Item2, 0);
            var uv = new List<Vector2>(data.Item1.Count);
            foreach (var v in data.Item1) uv.Add(new Vector2(v.x, v.z) * 0.5f); // world-aligned, so water and banks tile seamlessly across cells
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            var go = new GameObject(name);
            go.transform.SetParent(terrainRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void ClearAll()
        {
            pendingSparks.Clear();
            ClearArrivals();
            foreach (var v in buildingViews.Values) Destroy(v.Root);
            foreach (var v in enemyViews.Values) Destroy(v.Root);
            foreach (var a in arrows)
            {
                a.Go.SetActive(false);
                (a.Splash > 0f ? shellPool : arrowPool).Push(a.Go);
            }

            foreach (var c in cues) Recycle(c);
            particles.Clear();
            foreach (var f in flashes)
            {
                f.Life = 0f;
                f.Light.gameObject.SetActive(false);
            }

            foreach (var d in dying) Destroy(d.Root);
            dying.Clear();
            lastStrikeBurst.Clear();
            buildingViews.Clear();
            enemyViews.Clear();
            arrows.Clear();
            cues.Clear();
            nightTarget = null;
            topologyRevision = -1;
        }

        /// <summary>Apply this frame's event batch, diff views against the simulation, interpolate enemies.</summary>
        /// <param name="events">Events drained by the bootstrap this frame (shared read-only with the audio director).</param>
        /// <param name="deltaTime">Presentation delta: zero while Night is paused or the menu suspends combat.</param>
        public void Sync(RunSimulation sim, IReadOnlyList<SimEvent> events, float deltaTime)
        {
            fxTime += deltaTime;
            BatchesSeen++;
            EventsSeen += events.Count;
            shotTargets.Clear();
            detonatedThisFrame.Clear();
            destroyedThisFrame.Clear();
            killDelay.Clear();
            shotFlight.Clear();
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.RunReset:
                        ClearAll();
                        shotTargets.Clear();
                        break;
                    case SimEventType.TowerShot:
                        // Positions were captured when the shot happened, so a target removed in the same step still gets it.
                        var cannon = sim.GetBuilding(e.SourceId) is Building src && src.Kind == BuildingKind.Cannon ? src : null;
                        buildingViews.TryGetValue(e.SourceId, out var shooter);
                        Vector3? muzzle = null;
                        if (cannon != null && shooter != null && e.HasPositions)
                        {
                            // Hold the aim on the shot's point (the target may die this step) and fire from the muzzle.
                            shooter.AimDirection = new Vector3((float)(e.ToX - e.FromX), 0f, (float)(e.ToZ - e.FromZ));
                            shooter.AimHold = CannonAimHoldSeconds;
                            if (shooter.Muzzle != null) muzzle = shooter.Muzzle.position;
                        }

                        var flight = cannon != null ? ShellSeconds : ArrowSeconds;
                        SpawnArrow(e, cannon != null ? (float)sim.SplashRadius(cannon) : 0f, false, muzzle);
                        shotTargets.Add(e.TargetId);
                        shotFlight[e.TargetId] = flight;
                        if (e.HasPositions) SpawnCue(CueKind.Fire, muzzle ?? Pos(e.FromX, e.FromZ, 1.0f), 0f);
                        if (muzzle.HasValue) EmitMuzzleBlast(muzzle.Value, shooter.AimDirection);
                        if (shooter != null) shooter.Recoil = 0.14f;
                        if (shooter?.Bow != null && e.HasPositions)
                        {
                            // The archer looses at the shot's point and holds it while the arrow flies, then draws again.
                            shooter.AimDirection = new Vector3((float)(e.ToX - e.FromX), 0f, (float)(e.ToZ - e.FromZ));
                            shooter.AimHold = 0.35f;
                            shooter.Release = 1f;
                            shooter.Draw = 0f;
                        }
                        break;
                    case SimEventType.SplashHit:
                        shotTargets.Add(e.TargetId); // a splash kill shows when the shell lands, like the primary's
                        shotFlight[e.TargetId] = ShellSeconds;
                        break;
                    case SimEventType.RangedShot:
                        SpawnArrow(e, 0f, true);
                        if (enemyViews.TryGetValue(e.SourceId, out var archer)) archer.Lunge = 0.25f;
                        break;
                    case SimEventType.BomberDetonated:
                        detonatedThisFrame.Add(e.SourceId); // the blast is its death: no fall animation
                        if (enemyViews.TryGetValue(e.SourceId, out var bomberView)) EmitExplosion(bomberView.Root.transform.position);
                        else if (e.HasPositions) EmitExplosion(Pos(e.ToX, e.ToZ, 0f));
                        if (e.HasPositions)
                        {
                            SpawnCue(CueKind.Blast, Pos(e.ToX, e.ToZ, 0.08f), 0f, 1.5f);
                            SpawnCue(CueKind.Collapse, Pos(e.ToX, e.ToZ, 0.25f), 0f);
                        }

                        break;
                    case SimEventType.BuildingRepaired:
                        if (buildingViews.TryGetValue(e.SourceId, out var repaired))
                            SpawnCue(CueKind.Repair, repaired.Root.transform.position + Vector3.up * 0.4f, 0f);
                        break;
                    case SimEventType.EnemyKilled:
                        // A Tower kill shows when its arrow lands; trap/burn/reflection kills show immediately. One cue per kill.
                        // The body falls when the same projectile lands, never before it.
                        killDelay[e.SourceId] = Mathf.Max(shotFlight.TryGetValue(e.SourceId, out var landsIn) ? landsIn : 0f,
                            pendingSparks.Exists(p => p.enemy == e.SourceId) ? HeroBladeDelay : 0f); // the hero's blade: at its contact
                        if (e.HasPositions) SpawnCue(CueKind.Kill, Pos(e.ToX, e.ToZ, 0.3f), killDelay[e.SourceId]); // height follows the view (ApplyCue)
                        break;
                    case SimEventType.BuildingDamaged:
                        // Actual damage only (invulnerable fixture targets emit no BuildingDamaged).
                        if (buildingViews.TryGetValue(e.TargetId, out var bv)) bv.Flash = 0.18f;
                        break;
                    case SimEventType.EnemyAttacked:
                        if (enemyViews.TryGetValue(e.SourceId, out var attacker))
                        {
                            attacker.Lunge = 0.25f;
                            if (sim.GetBuilding(e.TargetId) is Building struck) EmitStrikeImpact(attacker.Root.transform.position, struck);
                        }

                        break;
                    case SimEventType.HeroHit:
                        // The hero's blade connects when the shown swing peaks (~0.08 s in): flinch and sparks then, not before.
                        if (enemyViews.TryGetValue(e.TargetId, out var bladed))
                        {
                            bladed.HitDelay = Mathf.Max(bladed.HitDelay, HeroBladeDelay);
                            pendingSparks.Add((e.TargetId, HeroBladeDelay));
                        }

                        break;
                    case SimEventType.HeroDamaged:
                        // An enemy's hit on the hero (plan 012): the attacker strikes; an archer's arrow flies to the hero.
                        if (enemyViews.TryGetValue(e.SourceId, out var heroAttacker))
                        {
                            heroAttacker.Lunge = 0.25f;
                            if (heroAttacker.Enemy.Definition.IsRanged) SpawnArrow(e, 0f, true);
                        }

                        break;
                    case SimEventType.TrapTriggered:
                    case SimEventType.BurnTick:
                        if (enemyViews.TryGetValue(e.TargetId, out var hurt)) hurt.Hit = Mathf.Max(hurt.Hit, e.Type == SimEventType.BurnTick ? 0.45f : 1f);
                        break;
                    case SimEventType.BuildingDestroyed:
                        destroyedThisFrame.Add(e.SourceId);
                        if (e.HasPositions) SpawnCue(CueKind.Collapse, Pos(e.ToX, e.ToZ, 0.25f), 0f);
                        if (buildingViews.TryGetValue(e.SourceId, out var fallen)) EmitCollapseDust(fallen.Root.transform.position, Footprint(fallen.Building.Kind));
                        break;
                }
            }

            var viewer = Camera.main;
            barViewer = viewer;
            BarRotation = Quaternion.Euler(0f, viewer != null ? viewer.transform.eulerAngles.y : CameraRig.Yaw, 0f);
            if (riverMat != null) riverMat.SetTextureOffset("_BaseMap", new Vector2(fxTime * 0.02f, fxTime * 0.045f));
            if (fordMat != null) fordMat.SetTextureOffset("_BaseMap", new Vector2(fxTime * 0.03f, fxTime * 0.06f));
            TickWater(deltaTime);
            SyncGroundDetail(sim);
            SyncBuildings(sim, deltaTime);
            SyncArrivals(sim);
            SyncEnemies(sim, deltaTime);
            EmitHeroSparks(deltaTime);
            UpdateCues(deltaTime); // before arrows: cues spawned by landing arrows this frame start at age zero
            UpdateArrows(deltaTime);
            UpdateDying(deltaTime);
            particles.Update(deltaTime);
            UpdateFlashes(deltaTime);
            var night = sim.State == RunState.Night || sim.State == RunState.Reward || sim.State == RunState.Defeat || sim.StressMode;
            if (nightTarget == null)
            {
                NightBlend = night ? 1f : 0f; // a new or loaded run starts in its phase's light, without a blend
                ApplyLighting(NightBlend);
            }
            else if (!Mathf.Approximately(NightBlend, night ? 1f : 0f))
            {
                NightBlend = Mathf.MoveTowards(NightBlend, night ? 1f : 0f, deltaTime / LightBlendSeconds);
                ApplyLighting(NightBlend);
            }

            nightTarget = night;
        }

        void SyncBuildings(RunSimulation sim, float dt)
        {
            // Wall topology only changes with solid occupancy; recompute masks on a new revision and rebuild only Walls
            // whose connections changed (placed/sold/destroyed Wall and its neighbors), never every frame.
            var topologyChanged = ConnectedWalls && (sim.Grid.Revision != topologyRevision || sim.ResetCount != topologyReset);
            topologyRevision = sim.Grid.Revision;
            topologyReset = sim.ResetCount;
            foreach (var b in sim.Grid.AllBuildings())
            {
                if (!buildingViews.TryGetValue(b.Id, out var view))
                {
                    view = CreateBuildingView(sim, b);
                    buildingViews[b.Id] = view;
                }
                else if (topologyChanged && b.Kind == BuildingKind.Wall && WallTopology.Mask(sim.Grid, b.Cell) != view.WallMask)
                {
                    Destroy(view.Root);
                    var flash = view.Flash;
                    view = CreateBuildingView(sim, b);
                    view.Flash = flash;
                    buildingViews[b.Id] = view;
                    WallRebuildCount++;
                }

                if (b.Kind != BuildingKind.Wall && view.Orientation != b.Orientation)
                {
                    view.Orientation = b.Orientation;
                    view.Root.transform.rotation = Quaternion.Euler(0, b.Orientation * 90f, 0);
                }

                if (view.Turret != null) AimCannon(sim, b, view, dt);
                if (view.Bow != null) AnimateArcher(sim, b, view, dt);

                var fraction = b.MaxHp > 0 ? (float)b.Hp / b.MaxHp : 1f;
                var height = BarHeight(b.Kind);
                var showBar = fraction < 0.999f;
                if (view.HpBar.activeSelf != showBar) view.HpBar.SetActive(showBar);
                if (showBar)
                {
                    SetFill(view.HpFill, fraction);
                    view.HpBar.transform.rotation = BarRotation; // flat to the screen whatever the building's facing
                    view.HpBar.transform.localScale = Vector3.one * BarScale(view.HpBar.transform.position, 0.84f, 1f);
                }

                // Buildings never bounce (owner feedback 2026-10-19): no squash on a hit, no rise when a tower fires. Hits read
                // through impact sparks, the HP bar and the Keep cues; shots through the archer, muzzle and projectile.
                if (view.Flash > 0f) view.Flash -= dt;
                if (view.Recoil > 0f) view.Recoil = Mathf.Max(0f, view.Recoil - dt);
                if (view.Root.transform.localScale != Vector3.one) view.Root.transform.localScale = Vector3.one;

                var damaged = b.Definition.HasHp && fraction < DamagedFraction && b.IsAlive;
                if (damaged && view.Smoke == null) view.Smoke = CreateSmoke(view.Root.transform, height * 0.7f, 5);
                if (view.Smoke != null)
                {
                    if (view.Smoke.activeSelf != damaged) view.Smoke.SetActive(damaged);
                    if (damaged) AnimateSmoke(view.Smoke.transform, b.Id);
                }

                if (view.Glow != null)
                {
                    var lit = NightBlend > 0.4f;
                    if (view.Glow.activeSelf != lit) view.Glow.SetActive(lit);
                }

                EmitBuildingFire(view, fraction, height, dt);
            }

            scratch.Clear();
            foreach (var pair in buildingViews)
                if (pair.Value.Building.Removed || sim.GetBuilding(pair.Key) != pair.Value.Building)
                    scratch.Add(pair.Key);
            foreach (var id in scratch)
            {
                var v = buildingViews[id];
                buildingViews.Remove(id);
                // Destroyed in combat: collapse in place. Sold or reset: gone at once.
                if (destroyedThisFrame.Contains(id)) StartDying(v.Root, null, true, 0f, v.HpBar, v.Smoke, v.Glow);
                else Destroy(v.Root);
            }
        }

        void SyncEnemies(RunSimulation sim, float dt)
        {
            var alpha = Mathf.Clamp01((float)sim.InterpolationAlpha);
            foreach (var e in sim.Enemies)
            {
                if (!enemyViews.TryGetValue(e.Id, out var view))
                {
                    view = CreateEnemyView(e);
                    enemyViews[e.Id] = view;
                    if (TakeArrival(e, out var arrivedFacing, out var arrivedPhase))
                    {
                        // Plan 016: it walked in out of the mist; keep its stride and heading through the hand-over.
                        view.Root.transform.rotation = arrivedFacing;
                        view.Oriented = true;
                        view.GaitPhase = arrivedPhase;
                        view.GaitAmount = 1f;
                    }
                }

                var prev = new Vector3((float)e.PrevX, 0f, (float)e.PrevZ);
                var cur = new Vector3((float)e.X, 0f, (float)e.Z);
                // Always interpolated: when the night ends mid-step the accumulator is zero, so the view rests on the last
                // stepped position instead of jumping a whole step ahead on the final frame.
                var pos = Vector3.Lerp(prev, cur, alpha);
                var t = view.Root.transform;
                Vector3 facing;
                if (e.TargetBuildingId >= 0 && sim.GetBuilding(e.TargetBuildingId) is Building target)
                    facing = new Vector3((float)target.CenterX, 0f, (float)target.CenterZ) - cur; // a 2x2 target's centre, not its anchor
                else if (e.TargetsHero && sim.Hero != null)
                    facing = new Vector3((float)sim.Hero.X, 0f, (float)sim.Hero.Z) - cur;
                else facing = cur - prev;
                if (!view.Oriented && facing.sqrMagnitude <= 1e-6f) facing = Prims.CellCenter(GridState.KeepCell) - cur; // standing spawn: face the Keep
                t.position = pos;
                if (facing.sqrMagnitude > 1e-6f)
                {
                    // Turn at a bounded rate on the presentation clock (frozen while paused) instead of snapping 90° in a frame
                    // at corners and on acquiring a target; a new view starts already facing its way.
                    var want = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
                    t.rotation = view.Oriented ? Quaternion.RotateTowards(t.rotation, want, TurnRate(e.Kind) * dt) : want;
                    view.Oriented = true;
                }

                // A new enemy eases into its lane offset instead of starting beside where it walked in.
                view.OffsetIn = Mathf.MoveTowards(view.OffsetIn, 1f, dt * 1.5f);
                t.position = pos + view.Offset * view.OffsetIn;
                if (!Mathf.Approximately(view.Model.localScale.x, CharacterScale))
                {
                    view.Model.localScale = Vector3.one * CharacterScale;
                    view.HpBar.transform.localPosition = new Vector3(0f, view.Height * CharacterScale, 0f);
                    view.Burn.transform.localPosition = view.BurnAt * CharacterScale; // markers keep their size, not their height
                    if (view.Fuse != null) view.Fuse.transform.localPosition = view.FuseAt * CharacterScale;
                }
                // The bar drops when the projectile lands, not when it is fired, then drains over ~0.15 s.
                if (shotFlight.TryGetValue(e.Id, out var landsIn))
                {
                    view.BarHold = Mathf.Max(view.BarHold, landsIn);
                    view.HitDelay = Mathf.Max(view.HitDelay, Mathf.Max(landsIn, 1e-4f)); // flinch when the projectile lands
                }

                if (view.HitDelay > 0f)
                {
                    view.HitDelay -= dt;
                    if (view.HitDelay <= 0f) view.Hit = 1f;
                }
                else view.Hit = Mathf.Max(0f, view.Hit - dt / HitFlinchSeconds);
                var fraction = (float)e.Hp / e.Definition.MaxHp;
                if (view.BarHold > 0f) view.BarHold -= dt;
                else view.ShownFraction = Mathf.MoveTowards(view.ShownFraction, fraction, dt / BarDrainSeconds);
                if (fraction > view.ShownFraction) view.ShownFraction = fraction; // healing never lags
                var showBar = view.ShownFraction < 0.999f;
                if (view.HpBar.activeSelf != showBar) view.HpBar.SetActive(showBar);
                if (showBar) SetFill(view.HpFill, view.ShownFraction);
                view.HpBar.transform.rotation = BarRotation;
                // Strategic: half the old width over true-scale figures; hero view: constant on-screen size.
                if (showBar) view.HpBar.transform.localScale = Vector3.one * BarScale(view.HpBar.transform.position, view.BarWidth, 0.5f);
                if (view.Burn.activeSelf != e.Burning) view.Burn.SetActive(e.Burning);
                if (e.Burning) view.Burn.transform.localScale = BurnSize * (0.8f + 0.25f * Mathf.Sin(fxTime * 18f + e.Id));
                EmitEnemyFire(view, e.Burning, e.FuseLit, dt);
                // Heavy enemies telegraph their strike: a short lean back before the attack is due (no timing change).
                var heavy = e.Kind == EnemyKind.Brute || e.Definition.IsBoss;
                if (view.Fuse != null)
                {
                    if (view.Fuse.activeSelf != e.FuseLit) view.Fuse.SetActive(e.FuseLit);
                    if (e.FuseLit)
                    {
                        // Grows and flickers faster as the blast nears: the 1.5 s warning has a visible countdown.
                        var left = e.FuseTicks > 0 ? Mathf.Clamp01((float)((e.DetonateTick - sim.Tick - alpha) / e.FuseTicks)) : 0f;
                        var burn = 1f - left;
                        view.Fuse.transform.localScale = FuseSize * (1f + burn) * (0.75f + 0.35f * Mathf.Abs(Mathf.Sin(fxTime * Mathf.Lerp(10f, 40f, burn) + e.Id)));
                    }
                }
                // The hit lands during the step that runs while Tick == AttackDueTick, which comes after the interpolated
                // frame shown now: hence +1 step. Without it the pose thinks the hit is overdue one step early and drops out.
                var dueAny = (e.TargetBuildingId >= 0 || e.TargetsHero) && !e.FuseLit && sim.State == RunState.Night ? (e.AttackDueTick - sim.Tick + 1 - alpha) * PrototypeDefinitions.StepSeconds : double.MaxValue;
                view.StrikeRise = dueAny >= 0 && dueAny < StrikeRiseSeconds ? 1f - (float)(dueAny / StrikeRiseSeconds) : 0f;
                var due = heavy ? dueAny : double.MaxValue;
                var windUp = due >= 0 && due < WindUpSeconds ? 1f - (float)(due / WindUpSeconds) : 0f;
                // Lean back as the strike nears; when it lands, come forward over ~0.1 s instead of in one frame.
                view.WindUp = windUp >= view.WindUp ? windUp : Mathf.Max(windUp, view.WindUp - dt / WindUpReleaseSeconds);
                // Light kinds with a rig raise the weapon over the last 0.3 s instead (same timing source, no delay to the hit).
                var light = !heavy && dueAny >= 0 && dueAny < LightWindSeconds ? 1f - (float)(dueAny / LightWindSeconds) : 0f;
                view.LightWind = light >= view.LightWind ? light : Mathf.Max(light, view.LightWind - dt / WindUpReleaseSeconds);
                view.Reach = (e.TargetBuildingId >= 0 || e.TargetsHero) && !e.Definition.IsRanged && !e.Moving ? ContactReach(sim, e, view, pos + view.Offset) : 0f;
                AnimateModel(view, pos, dt);
            }

            scratch.Clear();
            foreach (var pair in enemyViews)
                if (!pair.Value.Enemy.Alive || sim.GetEnemy(pair.Key) != pair.Value.Enemy)
                    scratch.Add(pair.Key);
            foreach (var id in scratch)
            {
                var v = enemyViews[id];
                enemyViews.Remove(id);
                if (detonatedThisFrame.Contains(id) || !killDelay.TryGetValue(id, out var delay)) Destroy(v.Root);
                else
                {
                    StartDying(v.Root, v.Model, false, delay, v.HpBar, v.Burn, v.Fuse, v.Aura);
                    var d = dying[dying.Count - 1];
                    d.Rig = v.Rig;
                    d.Id = id;
                    d.Velocity = v.Velocity;
                    d.GaitPhase = v.GaitPhase;
                    d.GaitAmount = v.GaitAmount;
                    d.PhasePerTile = v.PhasePerTile;
                    d.Run = v.Run;
                }
            }
        }

        BuildingView CreateBuildingView(RunSimulation sim, Building b)
        {
            var mask = -1;
            var yaw = b.Orientation * 90f;
            GameObject prefab;
            if (b.Kind == BuildingKind.Wall && ConnectedWalls)
            {
                mask = WallTopology.Mask(sim.Grid, b.Cell);
                WallTopology.Classify(mask, out var shape, out var turns);
                prefab = content.wallShapePrefabs[(int)shape];
                yaw = turns * 90f;
            }
            else prefab = content.PrefabFor(b.Kind);

            var root = Instantiate(prefab, buildingsRoot);
            root.name = $"{b.Kind} #{b.Id} {b.Cell}";
            root.transform.position = Prims.CellCenter(b.Cell) + RouteOverlay.FootprintOffset(b.Kind);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var height = BarHeight(b.Kind);
            var bar = CreateBar(root.transform, height, 0.8f, hpFill, out var fill);
            GameObject glow = null;
            if (b.Kind == BuildingKind.House && windowMat == null) // an authored House glows through its window material
            {
                // Warm window at night: an unlit pane over the modeled window (no extra light per building).
                glow = Prims.Create(PrimitiveType.Cube, root.transform, new Vector3(0.2f, 0.4f, -0.33f), new Vector3(0.11f, 0.11f, 0.01f), glowMat, "WindowGlow");
                glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glow.SetActive(false);
            }

            var turret = b.Kind == BuildingKind.Cannon ? BuildTurret(root) : null;
            var muzzle = turret != null ? turret.Find("Muzzle") : null;
            var torches = new List<Transform>();
            foreach (Transform child in root.transform)
                if (child.name == "Torch") torches.Add(child);
            // Authored Archer Tower: the archer turns to aim like the Cannon's gun, and the bow draws between shots.
            Transform bow = null;
            if (b.Kind == BuildingKind.Tower && root.transform.Find("Archer") is Transform archer)
            {
                turret = archer;
                bow = archer.Find("Bow");
            }

            Light torchLight = null;
            if (art != null && b.Kind == BuildingKind.Tower && torches.Count > 0)
            {
                var lightGo = new GameObject("TorchLight");
                lightGo.transform.SetParent(torches[0], false);
                torchLight = lightGo.AddComponent<Light>();
                torchLight.type = LightType.Point;
                torchLight.shadows = LightShadows.None;
                torchLight.color = art.torchColor;
                torchLight.range = art.towerLightRange;
                torchLight.intensity = art.towerLightIntensity * NightBlend;
                lightGo.SetActive(NightBlend > 0.01f);
            }

            UseRuntimeMaterials(root);
            return new BuildingView
            {
                Building = b, Root = root, HpBar = bar, HpFill = fill, Glow = glow, Turret = turret, Muzzle = muzzle, WallMask = mask,
                Orientation = b.Kind == BuildingKind.Wall ? 0 : b.Orientation, Torches = torches.ToArray(), Bow = bow,
                BowRest = bow != null ? bow.localPosition : Vector3.zero, TorchLight = torchLight,
            };
        }

        /// <summary>Cannon traverse: presentation only, the shot already happened in the simulation.</summary>
        public const float CannonTurnDegreesPerSecond = 240f;

        const float CannonAimHoldSeconds = 0.6f;

        /// <summary>Parts of the Cannon blockout that traverse; the stone platform and merlons stay square on the grid.</summary>
        static readonly string[] TurretParts = { "Carriage", "Barrel", "Muzzle", "Wheel" }; // the Shot pile stays on the deck

        /// <summary>Moves the gun parts of a Cannon view under one pivot so only the gun turns.</summary>
        static Transform BuildTurret(GameObject root)
        {
            var turret = new GameObject("Turret").transform;
            turret.SetParent(root.transform, false);
            var parts = new List<Transform>();
            foreach (Transform child in root.transform)
                if (System.Array.IndexOf(TurretParts, child.name) >= 0) parts.Add(child);
            foreach (var p in parts) p.SetParent(turret, true);
            return parts.Count > 0 ? turret : null;
        }

        /// <summary>
        /// The Cannon's gun swings toward its current target at a bounded rate (frozen while paused); with no target it
        /// returns to the facing the player chose. Only the turret turns; the simulation never depends on this rotation.
        /// </summary>
        void AimCannon(RunSimulation sim, Building b, BuildingView view, float dt)
        {
            var want = Quaternion.identity;
            if (view.AimHold > 0f && view.AimDirection.sqrMagnitude > 1e-4f)
            {
                view.AimHold -= dt; // keep pointing where the last shell went before easing home or to a new target
                want = Quaternion.Inverse(view.Root.transform.rotation) * Quaternion.LookRotation(view.AimDirection);
            }
            else if (sim.State == RunState.Night && b.TargetEnemyId >= 0 && sim.GetEnemy(b.TargetEnemyId) is Enemy target)
            {
                var dir = new Vector3((float)(target.X - b.CenterX), 0f, (float)(target.Z - b.CenterZ));
                if (dir.sqrMagnitude > 1e-4f) want = Quaternion.Inverse(view.Root.transform.rotation) * Quaternion.LookRotation(dir);
            }

            view.Turret.localRotation = Quaternion.RotateTowards(view.Turret.localRotation, want, CannonTurnDegreesPerSecond * dt);
        }

        /// <summary>
        /// The tower archer draws over ~0.5 s while it has a target at Night (relaxed half-draw otherwise), snaps forward on
        /// each shot (the TowerShot event) and draws again. Presentation only; the shot already happened.
        /// </summary>
        void AnimateArcher(RunSimulation sim, Building b, BuildingView view, float dt)
        {
            var engaged = sim.State == RunState.Night && b.TargetEnemyId >= 0;
            view.Draw = Mathf.MoveTowards(view.Draw, engaged ? 1f : 0.25f, dt / 0.5f);
            view.Release = Mathf.Max(0f, view.Release - dt / 0.15f);
            view.Bow.localPosition = view.BowRest + new Vector3(0f, 0f, -0.035f * view.Draw + 0.02f * view.Release);
            view.Bow.localRotation = Quaternion.Euler(-8f * view.Draw + 6f * view.Release, 0f, 0f);
        }

        static float BarHeight(BuildingKind kind)
        {
            switch (kind)
            {
                // Just above each model at real scale (plan 014: 1 tile = 8 m, the Keep tallest).
                case BuildingKind.Keep: return 2.55f;
                case BuildingKind.Tower: return 1.6f;
                case BuildingKind.House: return 1.05f;
                case BuildingKind.Farm: return 0.75f;
                case BuildingKind.Cannon: return 0.65f;
                case BuildingKind.Watchtower: return 2.15f;
                case BuildingKind.RepairStation: return 1.6f;
                default: return 0.8f;
            }
        }

        /// <summary>
        /// Restrained procedural gait on the model child: phase follows distance actually travelled, so it stops when the
        /// enemy stands still; attack events add a short forward lunge. Runner quick and light, Brute/Golem slow and heavy.
        /// </summary>
        void AnimateModel(EnemyView view, Vector3 pos, float dt)
        {
            var kind = view.Enemy.Kind;
            // Cadence is steps per second; steps per tile follow from the kind's speed, so fast units do not vibrate.
            float cadence, bob, sway;
            switch (kind)
            {
                case EnemyKind.Runner: cadence = 4.5f; bob = 0.035f; sway = 5f; break;
                case EnemyKind.Brute: cadence = 2.0f; bob = 0.05f; sway = 7f; break;
                case EnemyKind.SiegeGolem: cadence = 1.5f; bob = 0.06f; sway = 6f; break;
                case EnemyKind.SiegeCommander: cadence = 1.5f; bob = 0.05f; sway = 4f; break;
                case EnemyKind.Bomber: cadence = 3.2f; bob = 0.05f; sway = 9f; break;
                case EnemyKind.Archer: cadence = 2.8f; bob = 0.04f; sway = 6f; break;
                default: cadence = 3.0f; bob = 0.04f; sway = 6f; break;
            }

            var speed = Mathf.Max(0.05f, (float)view.Enemy.Definition.SpeedTilesPerSecond);
            var stride = cadence / speed;
            // Rigged figures step by their shown size (plan 013), so the planted foot keeps pace with the ground in both
            // views: the large strategic figures walk, the true-scale hero-view figures run with longer strides.
            var shownHeight = BodyHeight(kind) * view.Root.transform.localScale.x * CharacterScale;
            var run = view.Rig != null ? CharacterRig.RunAmount(speed, shownHeight) : 0f;
            var phasePerTile = view.Rig != null ? CharacterRig.PhasePerTile(shownHeight, run) : stride * Mathf.PI;

            var delta = new Vector2(pos.x - view.LastPosition.x, pos.z - view.LastPosition.z);
            var moved = delta.magnitude;
            view.LastPosition = pos;
            // Contact (plan 015 review): an engaged figure walks the last gap to the wall face or the hero at its own pace,
            // with its feet stepping, instead of skating metres on every blow. Presentation only; the hit timing is the event's.
            var rootScale = Mathf.Max(0.01f, view.Root.transform.localScale.x);
            if (dt > 0f)
            {
                var before = view.Approach;
                view.Approach = Mathf.MoveTowards(view.Approach, view.Reach, speed / rootScale * dt);
                moved += Mathf.Abs(view.Approach - before) * rootScale;
                view.Velocity = new Vector3(delta.x, 0f, delta.y) / dt;
            }

            view.PhasePerTile = phasePerTile;
            view.Run = run;
            if (dt > 0f)
            {
                view.GaitPhase += moved * phasePerTile;
                var walking = moved > 1e-4f;
                view.GaitAmount = Mathf.MoveTowards(view.GaitAmount, walking ? 1f : 0f, dt * 6f);
                view.Lunge = Mathf.Max(0f, view.Lunge - dt);
            }

            var step = Mathf.Sin(view.GaitPhase);
            // The strike is at its furthest the moment the hit lands (the event), then recovers.
            var strike = Mathf.Max(view.StrikeRise, view.Lunge > 0f ? Mathf.Pow(view.Lunge / 0.25f, 2f) : 0f);
            // The blow itself is a short lean-in (a fifth of the body height); the distance was walked by the approach.
            var lunge = view.Approach + strike * 0.2f * BodyHeight(kind) * CharacterScale;
            var windUp = view.WindUp * view.WindUp;
            var size = CharacterScale; // offsets are in root units: they shrink with the hero-view figures
            if (view.Rig != null)
            {
                // Limbs carry the stride, the strike and the flinch; the whole-body sway stays as a small accent.
                view.Rig.Pose(view.GaitPhase, view.GaitAmount, strike, Mathf.Max(view.WindUp, view.LightWind), view.Hit, 0f, run, fxTime * 2.1f + view.Enemy.Id);
                sway *= 0.35f;
                bob = 0f; // the rig's hips bob in the right phase; a whole-body bob lifted the planted feet
                lunge -= view.Hit * 0.05f * size;
            }
            view.Model.localPosition = new Vector3(0f, Mathf.Abs(step) * bob * view.GaitAmount * size, lunge - windUp * 0.08f * size);
            view.Model.localRotation = Quaternion.Euler((kind == EnemyKind.Runner ? 6f * view.GaitAmount : 0f) - windUp * 12f, 0f, step * sway * view.GaitAmount);
        }

        EnemyView CreateEnemyView(Enemy e)
        {
            var root = new GameObject($"{e.Kind} #{e.Id}");
            root.transform.SetParent(enemiesRoot, false);
            if (content.enemyScale > 0f && !Mathf.Approximately(content.enemyScale, 1f)) root.transform.localScale = Vector3.one * content.enemyScale;
            var model = Instantiate(content.EnemyPrefabFor(e.Kind), root.transform).transform;
            model.name = "Model";
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            var height = e.Kind == EnemyKind.SiegeGolem ? 1.75f : e.Kind == EnemyKind.Brute || e.Kind == EnemyKind.SiegeCommander ? 1.05f : e.Definition.IsBomber ? 0.95f : 0.8f;
            var width = e.Definition.IsBoss ? 1.0f : e.Kind == EnemyKind.Brute ? 0.7f : 0.5f;
            var bar = CreateBar(root.transform, height, width, hpFillEnemy, out var fill);
            // Burn feedback: a small flickering amber flame above the enemy (shape + motion, not color alone).
            var burn = Prims.Create(PrimitiveType.Cube, root.transform, new Vector3(0, height - 0.18f, 0), BurnSize, content.arrow, "Burn",
                Quaternion.Euler(0, 45, 0));
            burn.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            burn.SetActive(false);
            GameObject fuse = null, aura = null;
            if (e.Definition.IsBomber)
            {
                // Lit fuse: a flickering amber spark on top of the pack (shape + motion; the blast follows 1.5 s later).
                var wickTip = model.Find("WickTip");
                var fusePos = wickTip != null ? root.transform.InverseTransformPoint(wickTip.position) : new Vector3(0, 0.72f, 0);
                fuse = Prims.Create(PrimitiveType.Cube, root.transform, fusePos, FuseSize, glowMat, "Fuse", Quaternion.Euler(45, 45, 0));
                fuse.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fuse.SetActive(false);
            }

            if (e.Definition.AuraRadius > 0)
            {
                // The Commander's aura: a faint coral disc of the exact radius on the ground, readable at default zoom.
                var d = (float)e.Definition.AuraRadius * 2f / Mathf.Max(0.01f, root.transform.localScale.x);
                aura = Prims.Create(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.03f, 0), new Vector3(d, 0.005f, d), auraMat, "Aura");
                aura.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            GridCoordinates.TileCenter(e.Cell, out var sx, out var sz);
            // Bosses walk the lane centre; others get one of seven fixed offsets (by ID) so a column reads as several bodies.
            var offset = e.Definition.IsBoss ? Vector3.zero : new Vector3(((e.Id * 37) % 7 - 3) * 0.04f, 0f, ((e.Id * 53) % 7 - 3) * 0.04f);
            return new EnemyView
            {
                Enemy = e, Root = root, Model = model, HpBar = bar, HpFill = fill, Burn = burn, Fuse = fuse, Aura = aura, Offset = offset,
                BurnAt = burn.transform.localPosition, FuseAt = fuse != null ? fuse.transform.localPosition : Vector3.zero,
                LastPosition = new Vector3((float)sx, 0f, (float)sz), Height = height, Wick = model.Find("WickTip"), Torch = model.Find("TorchFlame"),
                Rig = model.GetComponent<CharacterRig>(), BarWidth = (width + 0.04f) * root.transform.localScale.x,
            };
        }

        void StartDying(GameObject root, Transform model, bool building, float delay, params GameObject[] hide)
        {
            if (delay <= 0f)
                foreach (var h in hide)
                    if (h != null) h.SetActive(false);
            if (dying.Count >= MaxDying)
            {
                Destroy(dying[0].Root);
                dying.RemoveAt(0);
            }

            dying.Add(new Dying { Root = root, Model = model, Building = building, Delay = delay, BasePosition = root.transform.position, Hide = hide });
        }

        /// <summary>
        /// Enemies tip over and sink; buildings sag and sink into the ground. Advances on the presentation clock, so it
        /// freezes while paused like every other combat cue.
        /// </summary>
        void UpdateDying(float dt)
        {
            for (var i = dying.Count - 1; i >= 0; i--)
            {
                var d = dying[i];
                if (d.Delay > 0f)
                {
                    d.Delay -= dt;
                    if (!d.Building && dt > 0f)
                    {
                        // Until the arrow or shell lands the body keeps walking: no freeze mid-stride, then a snap.
                        d.Root.transform.position += d.Velocity * dt;
                        d.BasePosition = d.Root.transform.position;
                        d.GaitPhase += d.Velocity.magnitude * dt * d.PhasePerTile;
                        if (d.Rig != null) d.Rig.Pose(d.GaitPhase, d.GaitAmount, 0f, 0f, 0f, 0f, d.Run, 0f);
                    }

                    if (d.Delay > 0f) continue;
                    foreach (var h in d.Hide)
                        if (h != null) h.SetActive(false);
                }

                d.Age += dt;
                var length = d.Building ? BuildingCollapseSeconds : EnemyDeathSeconds;
                var t = Mathf.Clamp01(d.Age / length);
                if (t >= 1f)
                {
                    Destroy(d.Root);
                    dying.RemoveAt(i);
                    continue;
                }

                var ease = t * t;
                if (d.Building)
                {
                    d.Root.transform.localScale = new Vector3(1f + 0.08f * t, Mathf.Lerp(1f, 0.15f, ease), 1f + 0.08f * t);
                    d.Root.transform.position = d.BasePosition + Vector3.down * 0.15f * ease;
                }
                else
                {
                    // Tip over with an ease-in (a falling body starts slow), the stride fading out over the first 0.15 s; lie
                    // still; then sink into the ground at full size (plan 015 review: it used to drop in 0.1 s and deflate).
                    var tip = Mathf.Clamp01(d.Age / EnemyFallSeconds);
                    var fall = 1f - Mathf.Cos(tip * Mathf.PI * 0.5f);
                    var gait = d.GaitAmount * (1f - Mathf.Clamp01(d.Age / 0.15f));
                    if (d.Rig != null) d.Rig.Pose(d.GaitPhase, gait, 0f, 0f, 1f - tip, tip, d.Run, 0f); // limbs go slack as the body tips
                    if (d.Model != null)
                    {
                        d.Model.localRotation = Quaternion.Euler(-85f * fall, 0f, 0f);
                        d.Model.localScale = Vector3.one * CharacterScale;
                    }

                    var sink = Mathf.Clamp01((d.Age - EnemyLieSeconds) / (EnemyDeathSeconds - EnemyLieSeconds));
                    d.Root.transform.position = d.BasePosition + Vector3.down * (0.08f * sink * sink * CharacterScale);
                }
            }
        }

        /// <summary>A few grey puffs rising in a loop; bounded to one small group per damaged building.</summary>
        GameObject CreateSmoke(Transform parent, float height, int puffs)
        {
            var root = new GameObject("DamageSmoke");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, height, 0f);
            for (var i = 0; i < puffs; i++)
                Prims.Create(PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.one * 0.14f, smokeMat, "Puff", Quaternion.Euler(20, i * 40, 15))
                    .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        void AnimateSmoke(Transform smoke, int seed)
        {
            for (var i = 0; i < smoke.childCount; i++)
            {
                var t = Mathf.Repeat(fxTime * 0.45f + i / (float)smoke.childCount + seed * 0.13f, 1f);
                var puff = smoke.GetChild(i);
                puff.localPosition = new Vector3(Mathf.Sin((t + i) * 5f) * 0.12f, t * 1.1f, Mathf.Cos((t + i) * 4f) * 0.1f);
                puff.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.42f, t) * (1f - t * 0.45f);
            }
        }

        GameObject CreateBar(Transform parent, float height, float width, Material fillMat, out Transform fill)
        {
            var bar = new GameObject("HpBar");
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0, height, 0);
            Prims.Create(PrimitiveType.Cube, bar.transform, Vector3.zero, new Vector3(width + 0.04f, 0.07f, 0.07f), hpBack, "Back")
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pivot = new GameObject("FillPivot").transform;
            pivot.SetParent(bar.transform, false);
            pivot.localPosition = new Vector3(-width / 2f, 0, 0);
            Prims.Create(PrimitiveType.Cube, pivot, new Vector3(width / 2f, 0, 0), new Vector3(width, 0.08f, 0.08f), fillMat, "Fill")
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fill = pivot;
            bar.SetActive(false);
            return bar;
        }

        /// <summary>
        /// HP bars share the rendering camera's yaw, so they read as horizontal on screen instead of following a world axis:
        /// the strategic camera's fixed 45 degrees, or the hero camera's current orbit (updated each sync).
        /// </summary>
        Quaternion BarRotation = Quaternion.Euler(0f, CameraRig.Yaw, 0f);

        Camera barViewer;

        /// <summary>
        /// HP-bar scale (plan 015 review). Strategic camera: <paramref name="strategic"/>. Perspective (hero) camera: a constant
        /// on-screen width, about 5% of the view height, whatever the distance, so a damaged Wall next to the hero never puts a
        /// metres-wide slab across the fight. <paramref name="worldWidth"/> is the bar's world width at scale 1.
        /// </summary>
        float BarScale(Vector3 at, float worldWidth, float strategic)
        {
            if (barViewer == null || barViewer.orthographic) return strategic;
            var distance = Vector3.Distance(barViewer.transform.position, at);
            return Mathf.Clamp(distance * 0.05f / Mathf.Max(0.01f, worldWidth), 0.02f, strategic);
        }

        /// <summary>Fastest enemy turn rate (Runner): a 90° corner in 0.125 s. Heavier kinds turn slower.</summary>
        public const float TurnDegreesPerSecond = 720f;

        /// <summary>Turn rate by weight: Runner 720, light 480, Brute 300, bosses 180 °/s.</summary>
        public static float TurnRate(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return TurnDegreesPerSecond;
                case EnemyKind.Brute: return 300f;
                case EnemyKind.SiegeGolem:
                case EnemyKind.SiegeCommander: return 180f;
                default: return 480f;
            }
        }

        const float BarDrainSeconds = 0.15f;

        /// <summary>Flinch length after a hit lands, and how long before a light kind's hit its rig raises the weapon.</summary>
        const float HitFlinchSeconds = 0.22f;

        const double LightWindSeconds = 0.3;

        /// <summary>The forward strike rises over this long before the hit is due, so it peaks exactly on contact.</summary>
        const float StrikeRiseSeconds = 0.1f;

        /// <summary>How long the wind-up pose takes to release after the strike (the lunge peaks at once).</summary>
        const float WindUpReleaseSeconds = 0.06f;

        /// <summary>How long before a Brute/Golem strike its wind-up pose begins.</summary>
        const double WindUpSeconds = 0.35;

        /// <summary>Burn flame size; the flicker scales around it rather than replacing it.</summary>
        static readonly Vector3 BurnSize = new Vector3(0.14f, 0.2f, 0.14f);

        static readonly Vector3 FuseSize = new Vector3(0.12f, 0.12f, 0.12f);

        /// <summary>Cannon shells and enemy arrows currently in flight (diagnostics).</summary>
        public int ActiveShellCount
        {
            get
            {
                var n = 0;
                foreach (var a in arrows)
                    if (a.Splash > 0f) n++;
                return n;
            }
        }

        static void SetFill(Transform pivot, float fraction) => pivot.localScale = new Vector3(Mathf.Clamp01(fraction), 1, 1);

        static Vector3 Pos(double x, double z, float y) => new Vector3((float)x, y, (float)z);

        /// <summary>
        /// Arrow (or Cannon shell, or a Raider Archer's coral arrow) from the positions recorded on the event; needs neither
        /// the shooter nor the target alive. A shell lands with a splash ring of the radius actually used by the shot.
        /// </summary>
        void SpawnArrow(SimEvent e, float splash = 0f, bool enemy = false, Vector3? origin = null)
        {
            if (!e.HasPositions) return;
            GameObject go;
            if (splash > 0f)
                go = shellPool.Count > 0 ? shellPool.Pop() : Prims.Create(PrimitiveType.Sphere, fxRoot, Vector3.zero, Vector3.one * 0.07f, glowMat, "Shell"); // a glowing ball, readable but not 2 m
            else
                go = arrowPool.Count > 0 ? arrowPool.Pop() : CreateArrow();
            if (splash <= 0f) go.GetComponent<MeshRenderer>().sharedMaterial = enemy ? enemyArrowMat : content.arrow;
            go.SetActive(true);
            // Launch heights at real scale: a Raider Archer's bow, the archer on the tower's 7 m platform, the gun's muzzle.
            var from = origin ?? (enemy ? Pos(e.FromX, e.FromZ, 0.6f * CharacterScale) : Pos(e.FromX, e.FromZ, splash > 0f ? 0.3f : 1.0f));
            var a = new ArrowFx
            {
                // An enemy arrow at the hero lands at chest height of the shown body; at a building, on its wall.
                Go = go, From = from, To = Pos(e.ToX, e.ToZ, enemy ? (e.Type == SimEventType.HeroDamaged ? 0.55f * CharacterScale : 0.45f) : 0.35f * CharacterScale), Splash = splash,
                Duration = splash > 0f ? ShellSeconds : ArrowSeconds, Arc = splash > 0f ? ShellArc : ArrowArc,
            };
            go.transform.position = a.From;
            arrows.Add(a);
        }

        void UpdateArrows(float dt)
        {
            for (var i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                a.T += dt / a.Duration;
                if (a.T >= 1f)
                {
                    if (a.Splash > 0f)
                    {
                        SpawnCue(CueKind.Blast, new Vector3(a.To.x, 0.08f, a.To.z), 0f, a.Splash);
                        EmitShellImpact(new Vector3(a.To.x, 0f, a.To.z));
                    }
                    else SpawnCue(CueKind.Hit, a.To, 0f);
                    a.Go.SetActive(false);
                    (a.Splash > 0f ? shellPool : arrowPool).Push(a.Go);
                    arrows.RemoveAt(i);
                    continue;
                }

                var p = Vector3.Lerp(a.From, a.To, a.T) + Vector3.up * Mathf.Sin(a.T * Mathf.PI) * a.Arc;
                a.Go.transform.position = p;
                if (a.Splash > 0f && dt > 0f) // a thin smoke trail behind the shell, by time (per frame it scaled with frame rate)
                {
                    a.Smoke += dt;
                    for (; a.Smoke >= 0.03f; a.Smoke -= 0.03f)
                        particles.Emit(p, Vector3.up * 0.15f + particles.InSphere(0.06f), 0.4f, 0.03f, 0.09f, SmokeLight.WithAlpha(0.4f), SmokeLight.WithAlpha(0f), -0.3f, 1f);
                }

                // Point along the arc (its tangent), not the straight line from bow to target.
                var dir = a.To - a.From + Vector3.up * (Mathf.Cos(a.T * Mathf.PI) * Mathf.PI * a.Arc);
                if (dir.sqrMagnitude > 1e-4f) a.Go.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        /// <param name="delay">Seconds before the cue appears (e.g. a Tower kill waits for its arrow).</param>
        /// <param name="radius">Blast ring radius in tiles (Blast cues only).</param>
        void SpawnCue(CueKind kind, Vector3 position, float delay, float radius = 0f)
        {
            var active = 0;
            Cue oldest = null;
            foreach (var c in cues)
            {
                if (c.Kind != kind) continue;
                active++;
                if (oldest == null) oldest = c;
            }

            if (active >= CueCaps[(int)kind] && oldest != null)
            {
                cues.Remove(oldest);
                Recycle(oldest);
            }

            var pool = cuePools[(int)kind];
            var cue = pool.Count > 0 ? pool.Pop() : CreateCue(kind);
            cue.Position = position;
            cue.Age = 0f;
            cue.Delay = delay;
            cue.Radius = radius;
            cue.Go.transform.position = position;
            cue.Go.SetActive(delay <= 0f);
            ApplyCue(cue);
            cues.Add(cue);
        }

        Cue CreateCue(CueKind kind)
        {
            var go = new GameObject($"Cue{kind}");
            go.transform.SetParent(fxRoot, false);
            var count = kind == CueKind.Hit || kind == CueKind.Fire || kind == CueKind.Blast ? 1 : kind == CueKind.Kill || kind == CueKind.Repair ? 5 : 8;
            var mat = kind == CueKind.Hit || kind == CueKind.Fire ? cueHit : kind == CueKind.Kill ? cueKill : kind == CueKind.Blast ? blastMat : kind == CueKind.Repair ? repairMat : cueDebris;
            var parts = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var shape = kind == CueKind.Blast ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                var p = Prims.Create(shape, go.transform, Vector3.zero, Vector3.one * 0.1f, mat, "Part", kind == CueKind.Blast ? Quaternion.identity : Quaternion.Euler(0, i * 47f, 30f));
                p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parts[i] = p.transform;
            }

            return new Cue { Kind = kind, Go = go, Parts = parts };
        }

        void UpdateCues(float dt)
        {
            for (var i = cues.Count - 1; i >= 0; i--)
            {
                var c = cues[i];
                if (c.Delay > 0f)
                {
                    c.Delay -= dt;
                    if (c.Delay > 0f) continue;
                    c.Go.SetActive(true);
                }
                else c.Age += dt;

                if (c.Age >= CueSeconds[(int)c.Kind])
                {
                    cues.RemoveAt(i);
                    Recycle(c);
                    continue;
                }

                ApplyCue(c);
            }
        }

        /// <summary>Hit: a brief expanding amber flash. Kill: coral shards bursting and falling. Collapse: timber debris.</summary>
        void ApplyCue(Cue c)
        {
            var t = Mathf.Clamp01(c.Age / CueSeconds[(int)c.Kind]);
            // Hit flashes and kill shards belong to a figure: in the hero view they shrink with it (plan 013).
            var figure = c.Kind == CueKind.Hit || c.Kind == CueKind.Kill ? CharacterScale : 1f;
            if (!Mathf.Approximately(c.Go.transform.localScale.x, figure)) c.Go.transform.localScale = Vector3.one * figure;
            if (c.Kind == CueKind.Kill) c.Go.transform.position = new Vector3(c.Position.x, c.Position.y * figure, c.Position.z); // chest height of the shown body
            if (c.Kind == CueKind.Hit)
            {
                c.Parts[0].localScale = Vector3.one * Mathf.Lerp(0.12f, 0.32f, t) * (1f - t * 0.6f);
                return;
            }

            if (c.Kind == CueKind.Fire)
            {
                c.Parts[0].localScale = Vector3.one * Mathf.Lerp(0.2f, 0.08f, t);
                return;
            }

            if (c.Kind == CueKind.Blast)
            {
                // Snaps out to the real splash diameter in ~0.1 s, then fades away: the area the hit actually covered.
                var grow = Mathf.Clamp01(c.Age / 0.1f);
                var d = c.Radius * 2f * Mathf.Lerp(0.6f, 1f, 1f - (1f - grow) * (1f - grow));
                c.Parts[0].localScale = new Vector3(d, 0.01f, d);
                cueBlock ??= new MaterialPropertyBlock();
                cueBlock.SetColor("_Color", Palette.Amber.WithAlpha(0.4f * (1f - t) * (1f - t)));
                c.Parts[0].GetComponent<MeshRenderer>().SetPropertyBlock(cueBlock);
                return;
            }

            if (c.Kind == CueKind.Repair)
            {
                for (var i = 0; i < c.Parts.Length; i++)
                {
                    var a = i * Mathf.PI * 2f / c.Parts.Length;
                    c.Parts[i].localPosition = new Vector3(Mathf.Cos(a) * 0.3f, t * 0.9f + i * 0.05f, Mathf.Sin(a) * 0.3f);
                    c.Parts[i].localScale = Vector3.one * 0.09f * (1f - t);
                }

                return;
            }

            var spread = c.Kind == CueKind.Kill ? 0.35f : 0.6f;
            for (var i = 0; i < c.Parts.Length; i++)
            {
                var a = i * Mathf.PI * 2f / c.Parts.Length;
                var r = spread * Mathf.Sqrt(t);
                var height = (c.Kind == CueKind.Kill ? 0.35f : 0.5f) * Mathf.Sin(t * Mathf.PI) - 0.2f * t;
                c.Parts[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Max(-0.2f, height), Mathf.Sin(a) * r);
                c.Parts[i].localScale = Vector3.one * (c.Kind == CueKind.Kill ? 0.1f : 0.16f) * (1f - t * 0.7f);
            }
        }

        void Recycle(Cue c)
        {
            c.Go.SetActive(false);
            cuePools[(int)c.Kind].Push(c);
        }

        static readonly Color FlameHot = new Color(1f, 0.93f, 0.6f, 0.95f);
        static readonly Color FlameMid = new Color(1f, 0.58f, 0.16f, 0.85f);
        static readonly Color FlameEnd = new Color(0.62f, 0.14f, 0.05f, 0f);
        static readonly Color Spark = new Color(1f, 0.96f, 0.75f, 1f);
        static readonly Color SparkEnd = new Color(1f, 0.45f, 0.1f, 0f);
        static readonly Color SmokeDark = new Color(0.16f, 0.15f, 0.15f, 0.7f);
        static readonly Color SmokeLight = new Color(0.78f, 0.76f, 0.72f, 0.6f);
        static readonly Color Dust = new Color(0.8f, 0.74f, 0.6f, 0.6f);
        static readonly Color Dirt = new Color(0.42f, 0.33f, 0.22f, 1f);

        /// <summary>Particles due this frame at <paramref name="rate"/> per second (fractional remainder carried over).</summary>
        static int Due(ref float accum, float rate, float dt)
        {
            accum += rate * dt;
            var n = (int)accum;
            accum -= n;
            return n;
        }

        static float Footprint(BuildingKind kind) => kind == BuildingKind.Farm || kind == BuildingKind.Cannon || kind == BuildingKind.RepairStation ? 0.75f : 0.3f;

        /// <summary>Torches flicker at night; a building below a third of its HP burns, with embers.</summary>
        void EmitBuildingFire(BuildingView view, float fraction, float height, float dt)
        {
            if (dt <= 0f) return;
            if (NightBlend > 0.3f && view.Torches.Length > 0)
            {
                var n = Due(ref view.TorchAccum, 7f * view.Torches.Length, dt);
                for (var i = 0; i < n; i++)
                {
                    var tip = view.Torches[i % view.Torches.Length].position + Vector3.up * 0.04f;
                    particles.Emit(tip + particles.InSphere(0.02f), new Vector3(0f, particles.Range(0.4f, 0.7f), 0f) + particles.InSphere(0.06f),
                        particles.Range(0.22f, 0.36f), 0.07f, 0.01f, FlameHot, FlameEnd, -0.5f);
                }
            }

            var b = view.Building;
            if (!b.Definition.HasHp || !b.IsAlive || fraction >= 0.34f) return;
            var r = Footprint(b.Kind) * 0.8f;
            var c = view.Root.transform.position;
            var fires = Due(ref view.FireAccum, Mathf.Lerp(26f, 12f, fraction / 0.34f), dt);
            for (var i = 0; i < fires; i++)
            {
                // On the roofline (the HP bar sits just above the model), not inside the mesh where it would be hidden.
                var p = c + new Vector3(particles.Range(-r, r), height * particles.Range(0.6f, 0.78f), particles.Range(-r, r));
                if (particles.Range(0f, 1f) < 0.2f)
                    particles.Emit(p, new Vector3(0f, particles.Range(1.2f, 2f), 0f) + particles.InSphere(0.5f), particles.Range(0.6f, 1.1f), 0.04f, 0.015f, Spark, SparkEnd, -0.4f, 0.8f);
                else
                    particles.Emit(p, new Vector3(0f, particles.Range(0.7f, 1.2f), 0f) + particles.InSphere(0.15f), particles.Range(0.35f, 0.6f),
                        particles.Range(0.18f, 0.3f), 0.03f, particles.Range(0f, 1f) < 0.5f ? FlameHot : FlameMid, FlameEnd, -0.6f, 0.5f);
            }
        }

        /// <summary>Burning enemies trail flames; a Bomber's wick smoulders and sprays sparks once lit; its torch burns.</summary>
        void EmitEnemyFire(EnemyView view, bool burning, bool fuseLit, float dt)
        {
            if (dt <= 0f) return;
            if (burning)
            {
                var n = Due(ref view.BurnAccum, 16f, dt);
                var basePos = view.Root.transform.position + Vector3.up * view.Height * 0.5f;
                for (var i = 0; i < n; i++)
                    particles.Emit(basePos + particles.InSphere(0.14f), new Vector3(0f, particles.Range(0.8f, 1.3f), 0f) + particles.InSphere(0.15f),
                        particles.Range(0.3f, 0.5f), particles.Range(0.1f, 0.15f), 0.02f, i % 2 == 0 ? FlameHot : FlameMid, FlameEnd, -0.5f, 0.6f);
            }

            if (view.Torch != null)
            {
                var n = Due(ref view.TorchAccum, 10f, dt);
                for (var i = 0; i < n; i++)
                    particles.Emit(view.Torch.position + Vector3.up * 0.03f + particles.InSphere(0.02f), new Vector3(0f, particles.Range(0.5f, 0.8f), 0f) + particles.InSphere(0.08f),
                        particles.Range(0.2f, 0.32f), 0.075f, 0.01f, FlameHot, FlameEnd, -0.5f);
            }

            if (view.Wick != null)
            {
                var n = Due(ref view.WickAccum, fuseLit ? 50f : 3f, dt);
                for (var i = 0; i < n; i++)
                {
                    var tip = view.Wick.position;
                    if (fuseLit && i % 6 == 5)
                        particles.Emit(tip, Vector3.up * 0.5f + particles.InSphere(0.2f), 0.6f, 0.06f, 0.16f, SmokeDark.WithAlpha(0.45f), SmokeDark.WithAlpha(0f), -0.2f, 1f);
                    else
                        particles.Emit(tip, fuseLit ? particles.InSphere(1f) * 2.2f + Vector3.up * 1.2f : Vector3.up * 0.4f + particles.InSphere(0.15f),
                            particles.Range(0.2f, fuseLit ? 0.4f : 0.3f), fuseLit ? 0.05f : 0.035f, 0.01f, Spark, SparkEnd, fuseLit ? 5f : 0f, 0.5f);
                }
            }
        }

        /// <summary>Bomber blast: light flash, fireball, sparks, flying debris, a rising smoke column and a fading scorch mark.</summary>
        void EmitExplosion(Vector3 at)
        {
            at.y = 0f;
            Flash(at + Vector3.up * 0.8f, new Color(1f, 0.7f, 0.35f), 7f, 6f, 0.45f);
            var p = particles;
            p.Emit(at + Vector3.up * 0.02f, Vector3.zero, 9f, 1.5f, 1.8f, new Color(0.07f, 0.06f, 0.05f, 0.6f), new Color(0.07f, 0.06f, 0.05f, 0f), 0f, 0f, flat: true);
            p.Emit(at + Vector3.up * 0.06f, Vector3.zero, 0.32f, 0.4f, 3.0f, Palette.Amber.WithAlpha(0.55f), Palette.Amber.WithAlpha(0f), 0f, 0f, flat: true);
            for (var i = 0; i < 3; i++)
                p.Emit(at + Vector3.up * 0.35f + p.InSphere(0.15f), Vector3.up * 0.4f, 0.2f, 0.55f, 1.1f, new Color(1f, 0.97f, 0.85f, 0.95f), FlameMid.WithAlpha(0f));
            for (var i = 0; i < 28; i++)
            {
                var d = p.InSphere(1f);
                d.y = Mathf.Abs(d.y) * 0.8f + 0.2f;
                p.Emit(at + Vector3.up * 0.3f + d * 0.2f, d.normalized * p.Range(2f, 4.5f) + Vector3.up * 0.8f, p.Range(0.45f, 0.85f),
                    p.Range(0.28f, 0.42f), 0.05f, i % 3 == 0 ? FlameHot : FlameMid, FlameEnd, -1.5f, 3.5f);
            }

            for (var i = 0; i < 18; i++)
                p.Emit(at + Vector3.up * 0.4f, p.InSphere(1f) * 5.5f + Vector3.up * 2f, p.Range(0.4f, 0.75f), 0.055f, 0.02f, Spark, SparkEnd, 6f, 1f);
            for (var i = 0; i < 14; i++)
            {
                var d = p.InSphere(1f);
                d.y = 0f;
                p.Emit(at + Vector3.up * 0.3f, d.normalized * p.Range(1.2f, 3f) + Vector3.up * p.Range(2.5f, 4.5f), p.Range(1.2f, 1.7f),
                    p.Range(0.07f, 0.13f), 0.07f, i % 2 == 0 ? Palette.TimberShadow : Dirt, Dirt.WithAlpha(0f), 9.8f, 0.2f, ground: true);
            }

            for (var i = 0; i < 16; i++)
                p.Emit(at + Vector3.up * 0.4f + p.InSphere(0.4f), p.InSphere(0.8f) + Vector3.up * p.Range(0.9f, 1.7f), p.Range(1.6f, 2.6f),
                    p.Range(0.25f, 0.4f), p.Range(0.7f, 0.95f), SmokeDark, SmokeDark.WithAlpha(0f), -0.2f, 1.4f);
        }

        /// <summary>Cannon shot: muzzle fire and a grey smoke cloud blown along the barrel.</summary>
        void EmitMuzzleBlast(Vector3 muzzle, Vector3 aim)
        {
            var dir = aim.sqrMagnitude > 1e-4f ? aim.normalized : Vector3.forward;
            Flash(muzzle, new Color(1f, 0.75f, 0.4f), 3f, 3.5f, 0.15f);
            for (var i = 0; i < 5; i++)
                particles.Emit(muzzle + dir * 0.1f, dir * particles.Range(3f, 5f) + particles.InSphere(0.6f), 0.12f, 0.2f, 0.05f, FlameHot, FlameEnd, 0f, 4f);
            for (var i = 0; i < 8; i++)
                particles.Emit(muzzle + dir * 0.15f, dir * particles.Range(0.8f, 2.2f) + particles.InSphere(0.35f) + Vector3.up * 0.3f, particles.Range(0.7f, 1.2f),
                    0.14f, particles.Range(0.38f, 0.5f), SmokeLight, SmokeLight.WithAlpha(0f), -0.25f, 2.5f);
        }

        /// <summary>A Cannon shell landing: a fountain of dirt, a short flash of fire and dust.</summary>
        void EmitShellImpact(Vector3 at)
        {
            for (var i = 0; i < 10; i++)
            {
                var d = particles.InSphere(1f);
                d.y = 0f;
                particles.Emit(at + Vector3.up * 0.08f, d * 1.2f + Vector3.up * particles.Range(1.6f, 3f), particles.Range(0.6f, 0.9f),
                    particles.Range(0.05f, 0.09f), 0.05f, Dirt, Dirt.WithAlpha(0f), 9.8f, 0.2f, ground: true);
            }

            for (var i = 0; i < 6; i++)
                particles.Emit(at + Vector3.up * 0.15f + particles.InSphere(0.15f), particles.InSphere(1.3f) + Vector3.up * 0.6f, 0.25f, 0.18f, 0.03f, FlameHot, FlameEnd, -1f, 3f);
            for (var i = 0; i < 5; i++)
                particles.Emit(at + Vector3.up * 0.15f + particles.InSphere(0.3f), particles.InSphere(0.5f) + Vector3.up * 0.5f, particles.Range(0.8f, 1.2f),
                    0.18f, 0.45f, Dust, Dust.WithAlpha(0f), -0.1f, 1.5f);
        }

        static readonly Color ArrowTrail = new Color(1f, 0.95f, 0.82f, 0.45f);
        static readonly Color Splinter = new Color(0.62f, 0.45f, 0.28f, 1f);

        Mesh arrowMesh;
        Material trailMat;

        /// <summary>Presentation time of each building's last strike burst: at most one burst per building per <see cref="StrikeBurstSeconds"/>.</summary>
        readonly Dictionary<int, float> lastStrikeBurst = new Dictionary<int, float>();

        const float StrikeBurstSeconds = 0.2f;

        /// <summary>Arrow (0.42 long, +Z forward): square shaft, iron head, three fletching vanes. Built once, shared by the pool.</summary>
        GameObject CreateArrow()
        {
            if (arrowMesh == null)
            {
                var v = new List<Vector3>();
                var t = new List<int>();
                void Box(Vector3 c, Vector3 size)
                {
                    var h = size * 0.5f;
                    var i = v.Count;
                    for (var k = 0; k < 8; k++) v.Add(c + new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, (k & 4) == 0 ? -h.z : h.z));
                    int[] q = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
                    for (var f = 0; f < 6; f++)
                    {
                        t.Add(i + q[f * 4]); t.Add(i + q[f * 4 + 1]); t.Add(i + q[f * 4 + 2]);
                        t.Add(i + q[f * 4]); t.Add(i + q[f * 4 + 2]); t.Add(i + q[f * 4 + 3]);
                    }
                }

                Box(Vector3.zero, new Vector3(0.022f, 0.022f, 0.36f));
                Box(new Vector3(0f, 0f, 0.19f), new Vector3(0.045f, 0.018f, 0.06f));
                Box(new Vector3(0f, 0.018f, -0.15f), new Vector3(0.006f, 0.04f, 0.08f));
                Box(new Vector3(0.016f, -0.01f, -0.15f), new Vector3(0.04f, 0.006f, 0.08f));
                Box(new Vector3(-0.016f, -0.01f, -0.15f), new Vector3(0.04f, 0.006f, 0.08f));
                arrowMesh = new Mesh { name = "Arrow" };
                arrowMesh.SetVertices(v);
                arrowMesh.SetTriangles(t, 0);
                arrowMesh.RecalculateNormals();
            }

            var go = new GameObject("Arrow");
            go.transform.SetParent(fxRoot, false);
            go.transform.localScale = Vector3.one * 0.3f; // a 1 m arrow at real scale (plan 015 review; the mesh is authored 0.42 long)
            go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = content.arrow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (art != null)
            {
                // A short pale streak behind the head so fast arrows read in a crowded fight; part of the pooled arrow,
                // so it costs nothing per frame (a per-frame particle trail scaled with frame rate).
                trailMat ??= Prims.Tinted(content.overlay, ArrowTrail);
                Prims.Create(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.62f), new Vector3(0.012f, 0.012f, 0.9f), trailMat, "Trail")
                    .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return go;
        }

        /// <summary>
        /// An enemy's blow landing on a building (the EnemyAttacked event): stone throws sparks and grit, timber throws
        /// splinters, both a small dust puff at the contact point. Bounded: a handful of particles per hit.
        /// </summary>
        void EmitStrikeImpact(Vector3 attacker, Building target)
        {
            var centre = new Vector3((float)target.CenterX, 0f, (float)target.CenterZ);
            var toward = centre - attacker;
            toward.y = 0f;
            var dir = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
            var at = attacker + dir * 0.32f + Vector3.up * 0.32f;
            var stone = target.Kind == BuildingKind.Keep;
            // Bounded: a crowd hammering one Wall shows a steady trickle, not a particle per attacker per hit.
            if (lastStrikeBurst.TryGetValue(target.Id, out var last) && fxTime - last < StrikeBurstSeconds) return;
            lastStrikeBurst[target.Id] = fxTime;
            for (var i = 0; i < 2; i++)
            {
                var v = -dir * particles.Range(0.8f, 1.6f) + Vector3.up * particles.Range(0.8f, 1.8f) + particles.InSphere(0.6f);
                if (stone) particles.Emit(at, v, particles.Range(0.18f, 0.3f), 0.035f, 0.01f, Spark, SparkEnd, 6f, 0.5f);
                else particles.Emit(at, v, particles.Range(0.35f, 0.55f), 0.05f, 0.03f, Splinter, Splinter.WithAlpha(0f), 9.8f, 0.3f, ground: true);
            }

            particles.Emit(at - Vector3.up * 0.2f, particles.InSphere(0.3f) + Vector3.up * 0.3f, particles.Range(0.5f, 0.8f), 0.1f, 0.24f, Dust.WithAlpha(0.45f), Dust.WithAlpha(0f), -0.1f, 1.5f);
        }

        /// <summary>A collapsing building throws up a dust cloud and a few splinters.</summary>
        void EmitCollapseDust(Vector3 at, float radius)
        {
            for (var i = 0; i < 12; i++)
            {
                var d = particles.InSphere(1f);
                d.y = 0f;
                particles.Emit(at + d * radius + Vector3.up * 0.15f, d * 0.9f + Vector3.up * particles.Range(0.2f, 0.6f), particles.Range(1f, 1.5f),
                    particles.Range(0.22f, 0.3f), particles.Range(0.55f, 0.75f), Dust, Dust.WithAlpha(0f), -0.05f, 2f);
            }

            for (var i = 0; i < 6; i++)
                particles.Emit(at + Vector3.up * 0.4f, particles.InSphere(1.5f) + Vector3.up * 2.2f, 1.1f, 0.09f, 0.07f, Palette.Timber, Palette.Timber.WithAlpha(0f), 9.8f, 0.2f, ground: true);
        }

        void Flash(Vector3 at, Color color, float intensity, float range, float seconds)
        {
            FlashLight pick = null;
            foreach (var f in flashes)
                if (pick == null || f.Life - f.Age < pick.Life - pick.Age) pick = f; // a free light, else the one closest to fading
            pick.Age = 0f;
            pick.Life = seconds;
            pick.Peak = intensity;
            pick.Light.color = color;
            pick.Light.range = range;
            pick.Light.transform.position = at;
            pick.Light.intensity = intensity;
            pick.Light.gameObject.SetActive(true);
        }

        void UpdateFlashes(float dt)
        {
            foreach (var f in flashes)
            {
                if (!f.Light.gameObject.activeSelf || dt <= 0f) continue;
                f.Age += dt;
                if (f.Age >= f.Life)
                {
                    f.Light.gameObject.SetActive(false);
                    f.Life = 0f;
                    continue;
                }

                var k = 1f - f.Age / f.Life;
                f.Light.intensity = f.Peak * k * k;
            }
        }

        /// <summary>Lerps day (0) to night (1): cooler ambient, weaker sun, the Keep torch lit.</summary>
        void ApplyLighting(float t)
        {
            if (art != null)
            {
                ApplyArtLighting(t);
                return;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.62f, 0.62f, 0.58f), Palette.NightAmbient * 1.25f, t);
            if (sun != null)
            {
                sun.color = Color.Lerp(new Color(1f, 0.95f, 0.86f), new Color(0.62f, 0.72f, 0.95f), t);
                sun.intensity = Mathf.Lerp(1.15f, 0.55f, t);
            }

            keepLight.intensity = 2.2f * t;
        }

        /// <summary>
        /// Authored day/night: trilight ambient (sky, horizon, ground) instead of one flat colour so forms keep their depth,
        /// warm sun to cool moonlight, softer night shadows, amber settlement light (Keep, tower torches, windows), the
        /// mist and background darkening, and the day/night post profiles cross-faded. Values live in the ArtDirection asset.
        /// </summary>
        void ApplyArtLighting(float t)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(art.daySky, art.nightSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(art.dayEquator, art.nightEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(art.dayGround, art.nightGround, t);
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, 0.2f, t); // the sky reflection must not make water glow at night
            if (sun != null)
            {
                sun.color = Color.Lerp(art.sunColor, art.moonColor, t);
                sun.intensity = Mathf.Lerp(art.sunIntensity, art.moonIntensity, t);
                sun.shadowStrength = Mathf.Lerp(art.dayShadowStrength, art.nightShadowStrength, t);
            }

            if (keepLight != null)
            {
                keepLight.color = art.torchColor;
                keepLight.intensity = art.keepLightIntensity * t;
            }

            if (windowMat != null) windowMat.SetColor("_EmissionColor", art.torchColor * Mathf.Lerp(art.windowDay, art.windowNight, t));
            // Water loses its daylight colour at night instead of staying saturated blue under the moon.
            if (riverMat != null) riverMat.SetColor("_BaseColor", art.river.GetColor("_BaseColor") * Mathf.Lerp(1f, 0.4f, t));
            if (fordMat != null) fordMat.SetColor("_BaseColor", art.ford.GetColor("_BaseColor") * Mathf.Lerp(1f, 0.45f, t));
            ApplyTerrainLighting(t);
            StrategicBackground = MistColor(t); // plan 016: anything the ground misses is mist
            if (Camera.main != null) Camera.main.backgroundColor = StrategicBackground.Value;
            if (dayVolume != null) dayVolume.weight = 1f - t;
            if (nightVolume != null) nightVolume.weight = t;
            foreach (var v in buildingViews.Values)
            {
                if (v.TorchLight == null) continue;
                v.TorchLight.intensity = art.towerLightIntensity * t;
                if (v.TorchLight.gameObject.activeSelf != t > 0.01f) v.TorchLight.gameObject.SetActive(t > 0.01f);
            }
        }
    }
}
