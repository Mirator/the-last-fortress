using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 013 hero-view grass: short instanced clumps on open land around the hero camera, at true scale next to a
    /// 0.22-tile person. Only open land gets grass: never water, fords, rock or forest cells, river banks, or a cell with a
    /// building or a trap on it (rechecked when occupancy changes), so walkable ground looks walkable and blockers stand
    /// clear. The strategic view never draws it. Presentation only.
    /// </summary>
    public sealed partial class WorldView
    {
        const float GrassRadius = 6.5f, GrassFade = 1.5f;
        const int ClumpsPerCell = 34;

        Material grassMat;
        Mesh grassMesh;

        /// <summary>Clump spots by cell (x, y, z, scale in w; yaw from the scale's hash) for every cell that may hold grass.</summary>
        readonly Dictionary<GridPos, Vector4[]> grassSpots = new Dictionary<GridPos, Vector4[]>();

        readonly List<Matrix4x4> grassBatch = new List<Matrix4x4>(1023);
        int grassRevision = -1, grassTrapRevision = -1;
        readonly HashSet<GridPos> grassBlocked = new HashSet<GridPos>();

        /// <summary>Clumps drawn on the last hero-view frame (diagnostics and the performance capture).</summary>
        public int GrassDrawn { get; private set; }

        void BuildGrass(TerrainShape shape)
        {
            grassSpots.Clear();
            grassRevision = grassTrapRevision = -1;
            if (art?.grass == null) return;
            if (grassMat == null) grassMat = new Material(art.grass) { enableInstancing = true };
            if (grassMesh == null) grassMesh = ClumpMesh();
            const int size = GridCoordinates.Size;
            for (var z = -2; z < size + 2; z++)
            for (var x = -2; x < size + 2; x++)
            {
                var cell = new GridPos(x, z);
                var onBoard = GridCoordinates.IsInBounds(cell);
                if (onBoard && (shape.Type(cell) != TerrainType.Land || shape.River(cell))) continue;
                // A fixed hash per cell (decoration, not gameplay randomness): the same map always grows the same grass.
                var rng = new System.Random(x * 7349 + z * 92821 + 17);
                var spots = new List<Vector4>(ClumpsPerCell);
                for (var k = 0; k < ClumpsPerCell; k++)
                {
                    float fx = (float)rng.NextDouble(), fz = (float)rng.NextDouble();
                    float px = x + fx, pz = z + fz;
                    if (onBoard && TerrainShape.SideDistance(cell, fx, fz, shape.River) < 0.2f) continue; // banks stay bare soil
                    if (!onBoard && shape.RoadDistance(px, pz) < TerrainShape.RoadHalfWidth + 0.08f) continue; // roads stay bare
                    var h = shape.Height(px, pz);
                    if (h < -0.004f) continue; // river outlets
                    spots.Add(new Vector4(px, h, pz, 0.7f + 0.55f * (float)rng.NextDouble()));
                }

                if (spots.Count > 0) grassSpots[cell] = spots.ToArray();
            }
        }

        /// <summary>Seven tapered, slightly bent blades in a tuft (about 0.03-0.045 tall); colour runs from root to tip.</summary>
        static Mesh ClumpMesh()
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var rng = new System.Random(5);
            Color root = new Color(0.15f, 0.21f, 0.08f), tip = new Color(0.34f, 0.42f, 0.17f); // the ground's own albedo, so lit blades never outshine it
            for (var b = 0; b < 7; b++)
            {
                var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var r = 0.004f + 0.01f * (float)rng.NextDouble();
                var origin = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var lean = new Vector3(-side.z, 0f, side.x) * (0.006f + 0.01f * (float)rng.NextDouble());
                var height = 0.016f + 0.014f * (float)rng.NextDouble(); // ankle to shin high on a 0.22-tile person
                const float width = 0.0045f;
                var k = verts.Count;
                for (var s = 0; s <= 2; s++)
                {
                    var t = s / 2f;
                    var c = origin + Vector3.up * height * t + lean * t * t;
                    var w = width * (1f - t * 0.85f);
                    verts.Add(c - side * w);
                    verts.Add(c + side * w);
                    colors.Add(Color.Lerp(root, tip, t));
                    colors.Add(Color.Lerp(root, tip, t));
                    uvs.Add(new Vector2(0f, t));
                    uvs.Add(new Vector2(1f, t));
                }

                verts.Add(origin + Vector3.up * height * 1.25f + lean * 1.6f);
                colors.Add(tip * 1.08f);
                uvs.Add(new Vector2(0.5f, 1.25f));
                tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3, k + 2, k + 4, k + 3, k + 3, k + 4, k + 5, k + 4, k + 6, k + 5 });
            }

            var mesh = new Mesh { name = "HeroGrassClump" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            var normals = new List<Vector3>(verts.Count);
            for (var i = 0; i < verts.Count; i++) normals.Add(Vector3.up);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Draws the clumps within reach of the hero camera (shrinking out at the edge, so nothing pops).</summary>
        public void DrawGrass(Camera cam, RunSimulation sim, float height = 1f)
        {
            GrassDrawn = 0;
            if (grassMat == null || grassSpots.Count == 0 || cam == null || sim == null || height <= 0f) return;
            if (sim.Grid.Revision != grassRevision || sim.Grid.TrapRevision != grassTrapRevision)
            {
                grassRevision = sim.Grid.Revision;
                grassTrapRevision = sim.Grid.TrapRevision;
                grassBlocked.Clear();
                foreach (var cell in grassSpots.Keys)
                    if (GridCoordinates.IsInBounds(cell) && (sim.Grid.GetOccupant(cell) != null || sim.Grid.HasTrap(cell)))
                        grassBlocked.Add(cell);
            }

            var eye = cam.transform.position;
            var forward = cam.transform.forward;
            // Centre the patch a little ahead of the camera, where the ground fills the view.
            var centre = new Vector2(eye.x + forward.x * 2f, eye.z + forward.z * 2f);
            var rp = new RenderParams(grassMat)
            {
                camera = cam, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true,
                worldBounds = new Bounds(new Vector3(centre.x, 0f, centre.y), new Vector3(GrassRadius * 2f, 1f, GrassRadius * 2f)),
            };
            var r = Mathf.CeilToInt(GrassRadius);
            int cx = Mathf.FloorToInt(centre.x), cz = Mathf.FloorToInt(centre.y);
            grassBatch.Clear();
            for (var z = cz - r; z <= cz + r; z++)
            for (var x = cx - r; x <= cx + r; x++)
            {
                var cell = new GridPos(x, z);
                if (!grassSpots.TryGetValue(cell, out var spots) || grassBlocked.Contains(cell)) continue;
                foreach (var s in spots)
                {
                    var d = new Vector2(s.x - centre.x, s.z - centre.y).magnitude;
                    if (d > GrassRadius) continue;
                    var scale = s.w * height * Mathf.Clamp01((GrassRadius - d) / GrassFade);
                    if (scale < 0.05f) continue;
                    var yaw = s.w * 997f % 360f;
                    grassBatch.Add(Matrix4x4.TRS(new Vector3(s.x, s.y, s.z), Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale)));
                    if (grassBatch.Count == 1023) Flush(rp);
                }
            }

            Flush(rp);
        }

        void Flush(RenderParams rp)
        {
            if (grassBatch.Count == 0) return;
            Graphics.RenderMeshInstanced(rp, grassMesh, 0, grassBatch);
            GrassDrawn += grassBatch.Count;
            grassBatch.Clear();
        }
    }
}
