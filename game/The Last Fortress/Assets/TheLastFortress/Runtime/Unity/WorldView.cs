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
    public sealed class WorldView : MonoBehaviour
    {
        /// <summary>Arrow flight time; a kill caused by the shot shows its cue when the arrow lands.</summary>
        public const float ArrowSeconds = 0.18f;

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

            /// <summary>Bomber fuse spark (shown while lit) and Siege Commander aura ring (always shown while alive).</summary>
            public GameObject Fuse;

            public GameObject Aura;
            public Vector3 LastPosition;
            public float GaitPhase;
            public float GaitAmount;
            public float Lunge;
            public float WindUp;
        }

        sealed class ArrowFx
        {
            public GameObject Go;
            public Vector3 From, To;
            public float T;

            /// <summary>Cannon shell: lands with a splash ring of this radius (0 for arrows).</summary>
            public float Splash;
        }

        readonly List<Cue> cues = new List<Cue>();
        readonly Stack<Cue>[] cuePools = { new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>(), new Stack<Cue>() };
        readonly HashSet<int> shotTargets = new HashSet<int>();
        Material cueHit, cueKill, cueDebris, smokeMat, glowMat, blastMat, repairMat, auraMat, enemyArrowMat;
        readonly Stack<GameObject> shellPool = new Stack<GameObject>();
        float fxTime;

        /// <summary>0 = day lighting, 1 = night lighting; eased toward the phase by the presentation clock.</summary>
        public float NightBlend { get; private set; }

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

        PrototypeContent content;
        Light sun;
        Transform buildingsRoot, enemiesRoot, fxRoot;
        GameObject buildGrid;
        Light keepLight;
        Material hpBack, hpFill, hpFillEnemy;

        readonly Dictionary<int, BuildingView> buildingViews = new Dictionary<int, BuildingView>();
        readonly Dictionary<int, EnemyView> enemyViews = new Dictionary<int, EnemyView>();
        readonly List<ArrowFx> arrows = new List<ArrowFx>();
        readonly Stack<GameObject> arrowPool = new Stack<GameObject>();
        readonly List<int> scratch = new List<int>();
        bool? nightTarget;
        GameObject terrainRoot;
        Material poolWater;
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
            blastMat = Prims.Tinted(content.overlay, Palette.Amber.WithAlpha(0.55f));
            repairMat = Prims.Tinted(content.overlay, Palette.RouteCyan.WithAlpha(0.9f));
            auraMat = Prims.Tinted(content.overlay, Palette.WarningCoral.WithAlpha(0.16f));
            enemyArrowMat = Prims.Tinted(content.overlay, Palette.WarningCoral);
            BuildBoard();
            var keepLightGo = new GameObject("KeepTorchLight");
            keepLightGo.transform.SetParent(transform, false);
            keepLightGo.transform.position = new Vector3(10.5f, 2.2f, 10.5f);
            keepLight = keepLightGo.AddComponent<Light>();
            keepLight.type = LightType.Point;
            keepLight.color = Palette.Amber;
            keepLight.range = 7f;
            keepLight.intensity = 0f;
            keepLight.shadows = LightShadows.None;
            ApplyLighting(0f);
        }

        void BuildBoard()
        {
            var board = new GameObject("Board").transform;
            board.SetParent(transform, false);
            Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.1f, 10f), new Vector3(20f, 0.2f, 20f), content.grass, "Ground");
            Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.45f, 10f), new Vector3(20.4f, 0.5f, 20.4f), content.timberShadow, "IslandEdge");
            var water = Prims.Create(PrimitiveType.Plane, board, new Vector3(10f, -0.3f, 10f), new Vector3(8f, 1f, 8f), content.water, "DecorativeWater");
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (content.IsMvp) BuildShoreAndGround(board);

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

        /// <summary>Static decoration count (shore pieces and ground patches); bounded and created once per scene.</summary>
        public int ShoreDecorationCount { get; private set; }

        /// <summary>
        /// MVP island finish, decoration only: a pale beach ring just above the sea, stones and reeds on it (outside the
        /// 20 x 20 board, so no legal cell looks blocked), and a few soft dark-grass patches flat on the ground — broad,
        /// low-contrast shapes rather than per-cell checkerboard noise. Deterministic; never touches the simulation.
        /// </summary>
        void BuildShoreAndGround(Transform board)
        {
            var sand = Prims.Tinted(content.rock, Palette.Hex(0xC9B98F));
            var reeds = Prims.Tinted(content.foliage, Palette.Hex(0x7E8C4E));
            var beach = Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.36f, 10f), new Vector3(21.3f, 0.2f, 21.3f), sand, "Beach");
            beach.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shore = new GameObject("Shore").transform;
            shore.SetParent(board, false);
            for (var i = 0; i < 64; i++)
            {
                // Walk the perimeter of the beach ring (0.2–0.45 outside the island edge).
                var u = (i + 0.5f) / 64f * 4f;
                var side = (int)u;
                var along = (u - side) * 21f - 0.5f;
                var h = (i * 37 % 11) / 11f; // stable per-piece variation, not gameplay randomness
                var outward = 20.55f + 0.2f * h;
                Vector3 p;
                switch (side)
                {
                    case 0: p = new Vector3(along, 0f, -outward + 20f); break;
                    case 1: p = new Vector3(outward, 0f, along); break;
                    case 2: p = new Vector3(20f - along, 0f, outward); break;
                    default: p = new Vector3(-outward + 20f, 0f, 20f - along); break;
                }

                p.y = -0.26f;
                if (i % 3 == 0)
                {
                    Prims.Create(PrimitiveType.Cube, shore, p + new Vector3(0, 0.06f, 0), new Vector3(0.22f, 0.14f, 0.18f) * (0.8f + 0.5f * h), content.rock, "ShoreStone",
                        Quaternion.Euler(6f * h, 40f * h + i * 17f, 0f));
                }
                else if (i % 3 == 1)
                {
                    for (var r = 0; r < 3; r++)
                        Prims.Create(PrimitiveType.Cube, shore, p + new Vector3((r - 1) * 0.07f, 0.12f + 0.03f * r, (r % 2) * 0.05f), new Vector3(0.03f, 0.26f + 0.06f * r, 0.03f), reeds, "Reed",
                            Quaternion.Euler((r - 1) * 9f, 0f, (r - 1) * 7f));
                }
                else continue;

                ShoreDecorationCount++;
            }

            var patch = Prims.Tinted(content.grass, Color.Lerp(Palette.Grass, Palette.DarkGrass, 0.45f)); // lit like the ground, so it darkens at night
            var patches = new[] { new Vector4(4.2f, 5.1f, 3.4f, 21f), new Vector4(15.3f, 4.4f, 2.8f, 63f), new Vector4(5.6f, 16.2f, 3.0f, 12f), new Vector4(14.8f, 15.5f, 3.6f, 48f), new Vector4(10.4f, 2.6f, 2.2f, 30f), new Vector4(2.8f, 10.7f, 2.0f, 75f) };
            foreach (var v in patches)
            {
                var g = Prims.Create(PrimitiveType.Quad, board, new Vector3(v.x, 0.003f, v.y), new Vector3(v.z, v.z * 0.7f, 1f), patch, "GroundPatch", Quaternion.Euler(90f, v.w, 0f));
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ShoreDecorationCount++;
            }
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
            terrainRoot = null;
            TerrainDecorationCount = 0;
            if (map == null || content.foliage == null) return;
            if (poolWater == null) poolWater = Prims.Tinted(content.water, Palette.Hex(0x3B6E8C));
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
                        // Raised clear of the ground top (y = 0) so the faces never z-fight, with a pale rim so a pool reads
                        // differently from the sea around the board.
                        Prims.Create(PrimitiveType.Cube, root, new Vector3(0, 0.007f, 0), new Vector3(0.98f, 0.014f, 0.98f), content.rock, "Rim")
                            .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        var pool = Prims.Create(PrimitiveType.Cube, root, new Vector3(0, 0.018f, 0), new Vector3(0.84f, 0.012f, 0.84f), poolWater, "Pool");
                        pool.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Prims.Create(PrimitiveType.Cube, root, new Vector3(0.28f, 0.03f, 0.3f), new Vector3(0.16f, 0.06f, 0.1f), content.rock, "ShoreStone", Quaternion.Euler(0, 25, 0));
                        break;
                }

                TerrainDecorationCount++;
            }
        }

        public void ClearAll()
        {
            foreach (var v in buildingViews.Values) Destroy(v.Root);
            foreach (var v in enemyViews.Values) Destroy(v.Root);
            foreach (var a in arrows)
            {
                a.Go.SetActive(false);
                (a.Splash > 0f ? shellPool : arrowPool).Push(a.Go);
            }

            foreach (var c in cues) Recycle(c);
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
                        SpawnArrow(e, cannon != null ? (float)sim.SplashRadius(cannon) : 0f);
                        shotTargets.Add(e.TargetId);
                        if (e.HasPositions) SpawnCue(CueKind.Fire, Pos(e.FromX, e.FromZ, cannon != null ? 1.2f : 2.05f), 0f);
                        if (buildingViews.TryGetValue(e.SourceId, out var shooter)) shooter.Recoil = 0.14f;
                        break;
                    case SimEventType.SplashHit:
                        shotTargets.Add(e.TargetId); // a splash kill shows when the shell lands, like the primary's
                        break;
                    case SimEventType.RangedShot:
                        SpawnArrow(e, 0f, true);
                        if (enemyViews.TryGetValue(e.SourceId, out var archer)) archer.Lunge = 0.2f;
                        break;
                    case SimEventType.BomberDetonated:
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
                        if (e.HasPositions) SpawnCue(CueKind.Kill, Pos(e.ToX, e.ToZ, 0.3f), shotTargets.Contains(e.SourceId) ? ArrowSeconds : 0f);
                        break;
                    case SimEventType.BuildingDamaged:
                        // Actual damage only (invulnerable fixture targets emit no BuildingDamaged).
                        if (buildingViews.TryGetValue(e.TargetId, out var bv)) bv.Flash = 0.18f;
                        break;
                    case SimEventType.EnemyAttacked:
                        if (enemyViews.TryGetValue(e.SourceId, out var attacker)) attacker.Lunge = 0.25f;
                        break;
                    case SimEventType.BuildingDestroyed:
                        if (e.HasPositions) SpawnCue(CueKind.Collapse, Pos(e.ToX, e.ToZ, 0.25f), 0f);
                        break;
                }
            }

            SyncBuildings(sim, deltaTime);
            SyncEnemies(sim, deltaTime);
            UpdateCues(deltaTime); // before arrows: cues spawned by landing arrows this frame start at age zero
            UpdateArrows(deltaTime);
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

                var fraction = b.MaxHp > 0 ? (float)b.Hp / b.MaxHp : 1f;
                var height = BarHeight(b.Kind);
                var showBar = fraction < 0.999f;
                if (view.HpBar.activeSelf != showBar) view.HpBar.SetActive(showBar);
                if (showBar)
                {
                    SetFill(view.HpFill, fraction);
                    view.HpBar.transform.rotation = BarRotation; // flat to the screen whatever the building's facing
                }

                if (view.Flash > 0f)
                {
                    view.Flash -= dt;
                    var s = 1f + Mathf.Clamp01(view.Flash / 0.18f) * 0.06f;
                    view.Root.transform.localScale = new Vector3(s, 1f / s, s);
                }
                else if (view.Recoil > 0f)
                {
                    // Firing moment: the Tower briefly rises and settles (presentation only; the shot already happened).
                    view.Recoil = Mathf.Max(0f, view.Recoil - dt);
                    var k = Mathf.Sin(view.Recoil / 0.14f * Mathf.PI) * 0.035f;
                    view.Root.transform.localScale = new Vector3(1f - k * 0.5f, 1f + k, 1f - k * 0.5f);
                }
                else if (view.Root.transform.localScale != Vector3.one) view.Root.transform.localScale = Vector3.one;

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
            }

            scratch.Clear();
            foreach (var pair in buildingViews)
                if (pair.Value.Building.Removed || sim.GetBuilding(pair.Key) != pair.Value.Building)
                    scratch.Add(pair.Key);
            foreach (var id in scratch)
            {
                Destroy(buildingViews[id].Root);
                buildingViews.Remove(id);
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
                }

                var prev = new Vector3((float)e.PrevX, 0f, (float)e.PrevZ);
                var cur = new Vector3((float)e.X, 0f, (float)e.Z);
                var pos = sim.State == RunState.Night ? Vector3.Lerp(prev, cur, alpha) : cur;
                var t = view.Root.transform;
                Vector3 facing;
                if (e.TargetBuildingId >= 0 && sim.GetBuilding(e.TargetBuildingId) is Building target)
                    facing = Prims.CellCenter(target.Cell) - cur;
                else facing = cur - prev;
                t.position = pos;
                if (facing.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
                var fraction = (float)e.Hp / e.Definition.MaxHp;
                var showBar = fraction < 0.999f;
                if (view.HpBar.activeSelf != showBar) view.HpBar.SetActive(showBar);
                if (showBar) SetFill(view.HpFill, fraction);
                view.HpBar.transform.rotation = BarRotation;
                if (view.Burn.activeSelf != e.Burning) view.Burn.SetActive(e.Burning);
                if (e.Burning) view.Burn.transform.localScale = BurnSize * (0.8f + 0.25f * Mathf.Sin(fxTime * 18f + e.Id));
                // Heavy enemies telegraph their strike: a short lean back before the attack is due (no timing change).
                var heavy = e.Kind == EnemyKind.Brute || e.Definition.IsBoss;
                if (view.Fuse != null)
                {
                    if (view.Fuse.activeSelf != e.FuseLit) view.Fuse.SetActive(e.FuseLit);
                    if (e.FuseLit) view.Fuse.transform.localScale = FuseSize * (0.7f + 0.5f * Mathf.Abs(Mathf.Sin(fxTime * 22f + e.Id)));
                }
                var due = heavy && e.TargetBuildingId >= 0 && sim.State == RunState.Night ? (e.AttackDueTick - sim.Tick - alpha) * PrototypeDefinitions.StepSeconds : double.MaxValue;
                view.WindUp = due >= 0 && due < WindUpSeconds ? 1f - (float)(due / WindUpSeconds) : 0f;
                AnimateModel(view, pos, dt);
            }

            scratch.Clear();
            foreach (var pair in enemyViews)
                if (!pair.Value.Enemy.Alive || sim.GetEnemy(pair.Key) != pair.Value.Enemy)
                    scratch.Add(pair.Key);
            foreach (var id in scratch)
            {
                Destroy(enemyViews[id].Root);
                enemyViews.Remove(id);
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
            if (b.Kind == BuildingKind.House)
            {
                // Warm window at night: an unlit pane over the modeled window (no extra light per building).
                glow = Prims.Create(PrimitiveType.Cube, root.transform, new Vector3(0.2f, 0.4f, -0.33f), new Vector3(0.11f, 0.11f, 0.01f), glowMat, "WindowGlow");
                glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glow.SetActive(false);
            }

            return new BuildingView { Building = b, Root = root, HpBar = bar, HpFill = fill, Glow = glow, WallMask = mask, Orientation = b.Kind == BuildingKind.Wall ? 0 : b.Orientation };
        }

        static float BarHeight(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Keep: return 1.95f;
                case BuildingKind.Tower: return 2.3f;
                case BuildingKind.House: return 1.35f;
                case BuildingKind.Farm: return 0.9f;
                case BuildingKind.Cannon: return 1.45f;
                case BuildingKind.Watchtower: return 2.6f;
                case BuildingKind.RepairStation: return 1.5f;
                default: return 1.05f;
            }
        }

        /// <summary>
        /// Restrained procedural gait on the model child: phase follows distance actually travelled, so it stops when the
        /// enemy stands still; attack events add a short forward lunge. Runner quick and light, Brute/Golem slow and heavy.
        /// </summary>
        void AnimateModel(EnemyView view, Vector3 pos, float dt)
        {
            var kind = view.Enemy.Kind;
            float stride, bob, sway;
            switch (kind)
            {
                case EnemyKind.Runner: stride = 9f; bob = 0.035f; sway = 5f; break;
                case EnemyKind.Brute: stride = 4.5f; bob = 0.05f; sway = 7f; break;
                case EnemyKind.SiegeGolem: stride = 3f; bob = 0.06f; sway = 6f; break;
                case EnemyKind.SiegeCommander: stride = 3f; bob = 0.05f; sway = 4f; break;
                case EnemyKind.Bomber: stride = 7f; bob = 0.05f; sway = 9f; break;
                default: stride = 6f; bob = 0.04f; sway = 6f; break;
            }

            var moved = new Vector2(pos.x - view.LastPosition.x, pos.z - view.LastPosition.z).magnitude;
            view.LastPosition = pos;
            if (dt > 0f)
            {
                view.GaitPhase += moved * stride * Mathf.PI;
                var walking = moved > 1e-4f;
                view.GaitAmount = Mathf.MoveTowards(view.GaitAmount, walking ? 1f : 0f, dt * 6f);
                view.Lunge = Mathf.Max(0f, view.Lunge - dt);
            }

            var step = Mathf.Sin(view.GaitPhase);
            var lunge = view.Lunge > 0f ? Mathf.Sin(view.Lunge / 0.25f * Mathf.PI) * 0.12f : 0f;
            var windUp = view.WindUp * view.WindUp;
            view.Model.localPosition = new Vector3(0f, Mathf.Abs(step) * bob * view.GaitAmount, lunge - windUp * 0.08f);
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
            var height = e.Definition.IsBoss ? 1.75f : e.Kind == EnemyKind.Brute ? 1.05f : 0.8f;
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
                fuse = Prims.Create(PrimitiveType.Cube, root.transform, new Vector3(0, 0.72f, 0), FuseSize, glowMat, "Fuse", Quaternion.Euler(45, 45, 0));
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
            return new EnemyView { Enemy = e, Root = root, Model = model, HpBar = bar, HpFill = fill, Burn = burn, Fuse = fuse, Aura = aura, LastPosition = new Vector3((float)sx, 0f, (float)sz) };
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

        /// <summary>HP bars share the fixed camera's yaw, so they read as horizontal on screen instead of following a world axis.</summary>
        static readonly Quaternion BarRotation = Quaternion.Euler(0f, CameraRig.Yaw, 0f);

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
        void SpawnArrow(SimEvent e, float splash = 0f, bool enemy = false)
        {
            if (!e.HasPositions) return;
            GameObject go;
            if (splash > 0f)
                go = shellPool.Count > 0 ? shellPool.Pop() : Prims.Create(PrimitiveType.Sphere, fxRoot, Vector3.zero, Vector3.one * 0.22f, content.timberShadow, "Shell");
            else
                go = arrowPool.Count > 0 ? arrowPool.Pop() : Prims.Create(PrimitiveType.Cube, fxRoot, Vector3.zero, new Vector3(0.05f, 0.05f, 0.4f), content.arrow, "Arrow");
            if (splash <= 0f) go.GetComponent<MeshRenderer>().sharedMaterial = enemy ? enemyArrowMat : content.arrow;
            go.SetActive(true);
            var from = enemy ? Pos(e.FromX, e.FromZ, 0.6f) : Pos(e.FromX, e.FromZ, splash > 0f ? 1.1f : 1.75f);
            var a = new ArrowFx { Go = go, From = from, To = Pos(e.ToX, e.ToZ, enemy ? 0.8f : 0.35f), Splash = splash };
            go.transform.position = a.From;
            arrows.Add(a);
        }

        void UpdateArrows(float dt)
        {
            for (var i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                a.T += dt / ArrowSeconds;
                if (a.T >= 1f)
                {
                    if (a.Splash > 0f) SpawnCue(CueKind.Blast, new Vector3(a.To.x, 0.08f, a.To.z), 0f, a.Splash);
                    else SpawnCue(CueKind.Hit, a.To, 0f);
                    a.Go.SetActive(false);
                    (a.Splash > 0f ? shellPool : arrowPool).Push(a.Go);
                    arrows.RemoveAt(i);
                    continue;
                }

                var p = Vector3.Lerp(a.From, a.To, a.T) + Vector3.up * Mathf.Sin(a.T * Mathf.PI) * 0.4f;
                a.Go.transform.position = p;
                var dir = a.To - a.From;
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
                // Grows to the real splash diameter, then thins out: the area the hit actually covered.
                var d = c.Radius * 2f * Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(t));
                c.Parts[0].localScale = new Vector3(d, 0.01f + 0.04f * (1f - t), d);
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

        /// <summary>Lerps day (0) to night (1): cooler ambient, weaker sun, the Keep torch lit.</summary>
        void ApplyLighting(float t)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.62f, 0.62f, 0.58f), Palette.NightAmbient * 1.25f, t);
            if (sun != null)
            {
                sun.color = Color.Lerp(new Color(1f, 0.95f, 0.86f), new Color(0.62f, 0.72f, 0.95f), t);
                sun.intensity = Mathf.Lerp(1.15f, 0.55f, t);
            }

            keepLight.intensity = 2.2f * t;
        }
    }
}
