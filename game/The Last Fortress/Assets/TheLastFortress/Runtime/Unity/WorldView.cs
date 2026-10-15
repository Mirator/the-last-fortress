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
        }

        sealed class EnemyView
        {
            public Enemy Enemy;
            public GameObject Root;
            public Transform HpFill;
            public GameObject HpBar;
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

        public int BuildingViewCount => buildingViews.Count;
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

        public void ClearAll()
        {
            foreach (var v in buildingViews.Values) Destroy(v.Root);
            foreach (var v in enemyViews.Values) Destroy(v.Root);
            foreach (var a in arrows) Destroy(a.Go);
            buildingViews.Clear();
            enemyViews.Clear();
            arrows.Clear();
            nightLook = null;
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
            var night = sim.State == RunState.Night || (sim.State == RunState.Defeat) || sim.StressMode;
            if (nightLook != night) ApplyLighting(night);
        }

        void SyncBuildings(RunSimulation sim, float dt)
        {
            foreach (var b in sim.Grid.AllBuildings())
            {
                if (!buildingViews.TryGetValue(b.Id, out var view))
                {
                    view = CreateBuildingView(b);
                    buildingViews[b.Id] = view;
                }

                var fraction = (float)b.Hp / b.Definition.MaxHp;
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

        BuildingView CreateBuildingView(Building b)
        {
            var root = Instantiate(content.PrefabFor(b.Kind), buildingsRoot);
            root.name = $"{b.Kind} #{b.Id} {b.Cell}";
            root.transform.position = Prims.CellCenter(b.Cell);
            var height = b.Kind == BuildingKind.Keep ? 1.95f : b.Kind == BuildingKind.Tower ? 2.3f : 1.05f;
            var bar = CreateBar(root.transform, height, 0.8f, hpFill, out var fill);
            return new BuildingView { Building = b, Root = root, HpBar = bar, HpFill = fill };
        }

        EnemyView CreateEnemyView(Enemy e)
        {
            var root = Instantiate(content.gruntPrefab, enemiesRoot);
            root.name = $"Grunt #{e.Id}";
            var bar = CreateBar(root.transform, 0.8f, 0.5f, hpFillEnemy, out var fill);
            return new EnemyView { Enemy = e, Root = root, HpBar = bar, HpFill = fill };
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
