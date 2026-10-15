using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Renders simulation state. Views are created and removed by diffing simulation IDs every frame;
    /// GameObjects never decide occupancy or outcome.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
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
        }

        sealed class EnemyView
        {
            public Enemy Enemy;
            public GameObject Root;
            public Transform HpFill;
            public GameObject HpBar;
            public GameObject Burn;
        }

        sealed class ArrowFx
        {
            public GameObject Go;
            public Vector3 From, To;
            public float T;
        }

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
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly List<int> scratch = new List<int>();
        bool? nightLook;
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
            ApplyLighting(false);
        }

        void BuildBoard()
        {
            var board = new GameObject("Board").transform;
            board.SetParent(transform, false);
            Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.1f, 10f), new Vector3(20f, 0.2f, 20f), content.grass, "Ground");
            Prims.Create(PrimitiveType.Cube, board, new Vector3(10f, -0.45f, 10f), new Vector3(20.4f, 0.5f, 20.4f), content.timberShadow, "IslandEdge");
            var water = Prims.Create(PrimitiveType.Plane, board, new Vector3(10f, -0.3f, 10f), new Vector3(8f, 1f, 8f), content.water, "DecorativeWater");
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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
            foreach (var a in arrows) Destroy(a.Go);
            buildingViews.Clear();
            enemyViews.Clear();
            arrows.Clear();
            nightLook = null;
            topologyRevision = -1;
        }

        /// <summary>Drain events, diff views against the simulation, interpolate enemies.</summary>
        public void Sync(RunSimulation sim, float deltaTime)
        {
            events.Clear();
            sim.DrainEvents(events);
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.RunReset:
                        ClearAll();
                        break;
                    case SimEventType.TowerShot:
                        SpawnArrow(sim, e.SourceId, e.TargetId);
                        break;
                    case SimEventType.EnemyAttacked:
                        if (buildingViews.TryGetValue(e.TargetId, out var bv)) bv.Flash = 0.18f;
                        break;
                }
            }

            SyncBuildings(sim, deltaTime);
            SyncEnemies(sim);
            UpdateArrows(deltaTime);
            var night = sim.State == RunState.Night || sim.State == RunState.Reward || sim.State == RunState.Defeat || sim.StressMode;
            if (nightLook != night) ApplyLighting(night);
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
                var showBar = fraction < 0.999f;
                if (view.HpBar.activeSelf != showBar) view.HpBar.SetActive(showBar);
                if (showBar) SetFill(view.HpFill, fraction);
                if (view.Flash > 0f)
                {
                    view.Flash -= dt;
                    var s = 1f + Mathf.Clamp01(view.Flash / 0.18f) * 0.06f;
                    view.Root.transform.localScale = new Vector3(s, 1f / s, s);
                }
                else if (view.Root.transform.localScale != Vector3.one) view.Root.transform.localScale = Vector3.one;
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

        void SyncEnemies(RunSimulation sim)
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
                view.HpBar.transform.rotation = Quaternion.identity;
                if (view.Burn.activeSelf != e.Burning) view.Burn.SetActive(e.Burning);
                if (e.Burning) view.Burn.transform.localScale = Vector3.one * (0.8f + 0.25f * Mathf.Sin(Time.unscaledTime * 18f + e.Id));
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
            return new BuildingView { Building = b, Root = root, HpBar = bar, HpFill = fill, WallMask = mask, Orientation = b.Kind == BuildingKind.Wall ? 0 : b.Orientation };
        }

        static float BarHeight(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Keep: return 1.95f;
                case BuildingKind.Tower: return 2.3f;
                case BuildingKind.House: return 1.35f;
                case BuildingKind.Farm: return 0.9f;
                default: return 1.05f;
            }
        }

        EnemyView CreateEnemyView(Enemy e)
        {
            var root = Instantiate(content.EnemyPrefabFor(e.Kind), enemiesRoot);
            root.name = $"{e.Kind} #{e.Id}";
            if (content.enemyScale > 0f && !Mathf.Approximately(content.enemyScale, 1f)) root.transform.localScale = Vector3.one * content.enemyScale;
            var height = e.Kind == EnemyKind.SiegeGolem ? 1.75f : e.Kind == EnemyKind.Brute ? 1.05f : 0.8f;
            var width = e.Kind == EnemyKind.SiegeGolem ? 1.0f : e.Kind == EnemyKind.Brute ? 0.7f : 0.5f;
            var bar = CreateBar(root.transform, height, width, hpFillEnemy, out var fill);
            // Burn feedback: a small flickering amber flame above the enemy (shape + motion, not color alone).
            var burn = Prims.Create(PrimitiveType.Cube, root.transform, new Vector3(0, height - 0.18f, 0), new Vector3(0.14f, 0.2f, 0.14f), content.arrow, "Burn",
                Quaternion.Euler(0, 45, 0));
            burn.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            burn.SetActive(false);
            return new EnemyView { Enemy = e, Root = root, HpBar = bar, HpFill = fill, Burn = burn };
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

        static void SetFill(Transform pivot, float fraction) => pivot.localScale = new Vector3(Mathf.Clamp01(fraction), 1, 1);

        void SpawnArrow(RunSimulation sim, int towerId, int enemyId)
        {
            var tower = sim.GetBuilding(towerId);
            var enemy = sim.GetEnemy(enemyId);
            if (tower == null || enemy == null) return;
            var go = arrowPool.Count > 0 ? arrowPool.Pop() : Prims.Create(PrimitiveType.Cube, fxRoot, Vector3.zero,
                new Vector3(0.05f, 0.05f, 0.4f), content.arrow, "Arrow");
            go.SetActive(true);
            arrows.Add(new ArrowFx
            {
                Go = go,
                From = Prims.CellCenter(tower.Cell, 1.75f),
                To = new Vector3((float)enemy.X, 0.35f, (float)enemy.Z),
            });
        }

        void UpdateArrows(float dt)
        {
            for (var i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                a.T += dt / 0.18f;
                if (a.T >= 1f)
                {
                    a.Go.SetActive(false);
                    arrowPool.Push(a.Go);
                    arrows.RemoveAt(i);
                    continue;
                }

                var p = Vector3.Lerp(a.From, a.To, a.T) + Vector3.up * Mathf.Sin(a.T * Mathf.PI) * 0.4f;
                a.Go.transform.position = p;
                var dir = a.To - a.From;
                if (dir.sqrMagnitude > 1e-4f) a.Go.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        void ApplyLighting(bool night)
        {
            nightLook = night;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            if (night)
            {
                RenderSettings.ambientLight = Palette.NightAmbient * 1.25f;
                if (sun != null)
                {
                    sun.color = new Color(0.62f, 0.72f, 0.95f);
                    sun.intensity = 0.55f;
                }

                keepLight.intensity = 2.2f;
            }
            else
            {
                RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.58f);
                if (sun != null)
                {
                    sun.color = new Color(1f, 0.95f, 0.86f);
                    sun.intensity = 1.15f;
                }

                keepLight.intensity = 0f;
            }
        }
    }
}
