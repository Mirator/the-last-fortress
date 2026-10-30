using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// The board and its surroundings as one ground mesh with river water, built from the played map (plan 013, mainland
    /// since plan 016). The board itself stays exactly flat at y = 0 on every land and Keep cell (buildings, enemies and the
    /// hero stand there); only non-walkable water cells sink into a river channel with sloped banks, and fords dip a few
    /// centimetres under a film of water. Outside the 20 x 20 board open ground rises gently into the edge mist (nothing
    /// stands there: the field may grow later), and a dirt road leaves each side midpoint (where the entries are) and
    /// bends away into the mist. Rivers that leave the board run on into the mist. Presentation only: nothing here is read by the
    /// simulation.
    /// </summary>
    public sealed partial class WorldView
    {
        const float RiverLevel = -0.06f;
        const float ChannelFloor = -0.17f;
        const float FordFloor = -0.03f;
        const float BankRun = 0.32f, FordRun = 0.4f;
        const float TerrainStep = 0.2f, TerrainMargin = 6f;

        /// <summary>Spec 017 meadow edge: radius (cells) of the blur that rounds the fertile mask.</summary>
        const float MeadowBlur = 1.25f;

        Material terrainGround, riverWater;
        Color waterSky, riverShallow, riverDeep, foamColor;
        float waterClock;

        /// <summary>Whether the authored terrain (ground shader and river water) replaces the plateau and planes.</summary>
        public bool HasTerrain => terrainGround != null && riverWater != null;

        void InitializeTerrain()
        {
            if (art.groundBlend == null || art.riverWater == null) return;
            terrainGround = new Material(art.groundBlend);
            riverWater = new Material(art.riverWater);
            waterSky = riverWater.GetColor("_SkyColor");
            foamColor = riverWater.GetColor("_FoamColor");
            riverShallow = riverWater.GetColor("_ShallowColor");
            riverDeep = riverWater.GetColor("_DeepColor");
        }

        /// <summary>Ripples run on the presentation clock: they hold still while Night is paused or the menu is open.</summary>
        void TickWater(float fxDt)
        {
            if (!HasTerrain) return;
            waterClock = (waterClock + fxDt) % 3600f;
            Shader.SetGlobalFloat(WaterTimeId, waterClock);
        }

        static readonly int WaterTimeId = Shader.PropertyToID("_TlfWaterTime");

        /// <summary>Day/night colours for the rivers: a cool blue at night, lighter than the dark ground so it still reads as water.</summary>
        void ApplyTerrainLighting(float t)
        {
            ApplyEdgeMist(t);
            if (!HasTerrain) return;
            var night = new Color(0.55f, 0.65f, 1f);
            Color River(Color c) => Color.Lerp(c, c * night, t) * Mathf.Lerp(1f, 0.5f, t);
            riverWater.SetColor("_ShallowColor", River(riverShallow));
            riverWater.SetColor("_DeepColor", River(riverDeep));
            riverWater.SetColor("_SkyColor", Color.Lerp(waterSky, new Color(0.16f, 0.2f, 0.32f), t));
            riverWater.SetColor("_FoamColor", Color.Lerp(foamColor, foamColor * 0.45f, t));
        }

        static readonly int MistColorId = Shader.PropertyToID("_TlfMistColor");
        static readonly int MistShapeId = Shader.PropertyToID("_TlfMistShape");
        static readonly int MistBoardId = Shader.PropertyToID("_TlfMistBoard");

        /// <summary>Plan 016: mist starts this far beyond the board edge (tiles) and is opaque by <see cref="MistFull"/>.</summary>
        public const float MistStart = 2.5f, MistFull = 5f;

        /// <summary>
        /// Edge mist globals for <see cref="EdgeMistFeature"/>: pale warm grey by day, dark blue-grey at night. Off where there
        /// is no authored terrain (the prototype scene), and when this view goes away, so it never leaks into another scene.
        /// </summary>
        void ApplyEdgeMist(float t)
        {
            if (!HasTerrain)
            {
                Shader.SetGlobalColor(MistColorId, Color.clear);
                return;
            }

            var mist = MistColor(t);
            mist.a = 1f;
            Shader.SetGlobalColor(MistColorId, mist);
            Shader.SetGlobalVector(MistShapeId, new Vector4(MistStart, MistFull, 1.2f, 0.12f));
            Shader.SetGlobalVector(MistBoardId, new Vector4(0f, 0f, GridCoordinates.Size, GridCoordinates.Size));
        }

        void OnDestroy() => Shader.SetGlobalColor(MistColorId, Color.clear);

        /// <summary>Mist by day/night blend: the hero sky's horizon colour, a little darker at night.</summary>
        public static Color MistColor(float night) => Color.Lerp(HeroSky.DayHorizon, HeroSky.NightHorizon * 0.8f, night);

        /// <summary>The played map's water layout, answered for any world point (on or off the board).</summary>
        public sealed class TerrainShape
        {
            readonly TerrainMap map;
            readonly HashSet<GridPos> crossings;

            readonly IReadOnlyList<GridPos> entries;
            readonly HashSet<GridPos> tracks = new HashSet<GridPos>();

            public TerrainShape(TerrainMap map)
            {
                this.map = map;
                crossings = map != null ? new HashSet<GridPos>(MapProfiles.RiverCrossings(map)) : new HashSet<GridPos>();
                entries = map != null ? map.EntriesOrSides() : new[] { MvpCampaign.West, MvpCampaign.East, MvpCampaign.North, MvpCampaign.South };

                // Spec 017: a faint worn track along each entry's open route on the empty board (generated maps only).
                if (map?.Entries == null || map.Profile == "" || !MapRecipes.IsRecipe(map.Profile)) return;
                foreach (var e in map.Entries)
                {
                    var r = MapProfiles.OpenRoute(map, e);
                    if (r.Found && !r.IsBreach) tracks.UnionWith(r.Cells);
                }
            }

            public bool IsFertile(GridPos c) => map?.Fertile != null && map.Fertile.Contains(c);

            public TerrainType Type(GridPos c) => map == null || !GridCoordinates.IsInBounds(c) ? TerrainType.Land : map[c];

            /// <summary>Water or any river crossing (ford or bridge): the river continues through it.</summary>
            public bool River(GridPos c) => GridCoordinates.IsInBounds(c) && (Type(c) == TerrainType.Water || crossings.Contains(c));

            /// <summary>Carved below the ground: water cells, and ring crossings (bridges stand over the channel).</summary>
            public bool Channel(GridPos c) => GridCoordinates.IsInBounds(c) && (Type(c) == TerrainType.Water || crossings.Contains(c) && GridCoordinates.IsReservedRing(c));

            public bool Ford(GridPos c) => crossings.Contains(c) && !GridCoordinates.IsReservedRing(c);

            /// <summary>Distance from (fx, fz) inside <paramref name="cell"/> to its nearest side or corner whose neighbour matches.</summary>
            public static float SideDistance(GridPos cell, float fx, float fz, System.Func<GridPos, bool> match)
            {
                var d = 9f;
                bool W = match(new GridPos(cell.X - 1, cell.Z)), E = match(new GridPos(cell.X + 1, cell.Z));
                bool S = match(new GridPos(cell.X, cell.Z - 1)), N = match(new GridPos(cell.X, cell.Z + 1));
                if (W) d = Mathf.Min(d, fx);
                if (E) d = Mathf.Min(d, 1f - fx);
                if (S) d = Mathf.Min(d, fz);
                if (N) d = Mathf.Min(d, 1f - fz);
                void Corner(int dx, int dz, bool a, bool b)
                {
                    if (a || b || !match(new GridPos(cell.X + dx, cell.Z + dz))) return;
                    var cx = dx < 0 ? fx : 1f - fx;
                    var cz = dz < 0 ? fz : 1f - fz;
                    d = Mathf.Min(d, Mathf.Sqrt(cx * cx + cz * cz));
                }

                Corner(-1, -1, W, S);
                Corner(1, -1, E, S);
                Corner(-1, 1, W, N);
                Corner(1, 1, E, N);
                return d;
            }

            static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            /// <summary>Height on the board: 0 on land and the Keep, a sloped channel in water cells, a shallow dip in fords.</summary>
            public float Board(float x, float z)
            {
                var cell = new GridPos(Mathf.Min((int)x, GridCoordinates.Size - 1), Mathf.Min((int)z, GridCoordinates.Size - 1));
                float fx = x - cell.X, fz = z - cell.Z;
                if (Channel(cell))
                {
                    // Off-board neighbours count as river: a ring channel continues into the mist.
                    var land = SideDistance(cell, fx, fz, n => GridCoordinates.IsInBounds(n) && !River(n));
                    var h = ChannelFloor * Smooth(land / BankRun);
                    var ford = SideDistance(cell, fx, fz, Ford);
                    if (ford < FordRun) h = Mathf.Max(h, Mathf.Lerp(FordFloor, ChannelFloor, Smooth(ford / FordRun)));
                    return h;
                }

                if (Ford(cell)) return FordFloor * Smooth(SideDistance(cell, fx, fz, n => !River(n)) / 0.22f);
                return 0f;
            }

            /// <summary>Height anywhere: the board, or the surround outside it (with river outlets).</summary>
            public float Height(float x, float z)
            {
                const float size = GridCoordinates.Size;
                float qx = Mathf.Max(0f, Mathf.Max(-x, x - size)), qz = Mathf.Max(0f, Mathf.Max(-z, z - size));
                if (qx <= 0f && qz <= 0f) return Board(x, z);
                var d = Mathf.Sqrt(qx * qx + qz * qz);
                var coast = Surround(x, z, d);
                // A river that reaches the board edge runs on, in its channel, into the mist.
                var cx = Mathf.Clamp(x, 0f, size - 0.001f);
                var cz = Mathf.Clamp(z, 0f, size - 0.001f);
                var edge = new GridPos((int)cx, (int)cz);
                if (!Channel(edge)) return coast;
                var lateral = SideDistance(edge, cx - edge.X, cz - edge.Z, n => GridCoordinates.IsInBounds(n) && !River(n));
                if (lateral > 8f) lateral = 1f;
                return Mathf.Min(coast, ChannelFloor * Smooth(lateral / BankRun));
            }

            /// <summary>
            /// Verge, then open ground rising gently into the mist (plan 016). <paramref name="d"/> is the distance from the
            /// board. Roads cut a little into the rise so they read as worn tracks.
            /// </summary>
            float Surround(float x, float z, float d)
            {
                var n1 = Mathf.PerlinNoise(x * 0.31f + 3.1f, z * 0.31f + 7.7f);
                var n2 = Mathf.PerlinNoise(x * 0.93f + 11.3f, z * 0.93f + 2.9f);
                var verge = 0.8f + 0.9f * n1;
                var rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(verge, verge + 4f, d));
                var h = rise * (0.2f + 0.18f * n2);
                return Mathf.Lerp(h, h * 0.45f, Road(x, z));
            }

            /// <summary>Road half-width in tiles (a cart track, about 3.6 m).</summary>
            public const float RoadHalfWidth = 0.22f;

            /// <summary>
            /// Distance from (x, z) to the nearest road's centre line, off the board. Each road leaves its entry cell's centre
            /// (the map's entries; the side midpoints without a list) straight out, then bends away about two tiles to one side
            /// before the mist. 99 when no road is near (or on the board).
            /// </summary>
            public float RoadDistance(float x, float z)
            {
                const float size = GridCoordinates.Size;
                var best = 99f;
                foreach (var e in entries)
                {
                    var direction = RouteOverlay.Outward(e);
                    var outward = direction.x < 0f ? -x : direction.x > 0f ? x - size : direction.z < 0f ? -z : z - size;
                    if (outward < 0f) continue;
                    var across = direction.x != 0f ? z - (e.Z + 0.5f) : x - (e.X + 0.5f);
                    best = Mathf.Min(best, Mathf.Abs(across - RoadBend(direction) * RoadOffset(outward)));
                }

                return best;
            }

            /// <summary>How far a road has bent sideways <paramref name="outward"/> tiles beyond the board edge.</summary>
            public static float RoadOffset(float outward) => 2f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 6f, outward));

            /// <summary>Which way the road leaving in <paramref name="outward"/> bends along <see cref="AcrossAxis"/>: west and north +, east and south −.</summary>
            public static float RoadBend(Vector3 outward) => outward.x < 0f || outward.z > 0f ? 1f : -1f;

            /// <summary>The axis a road's bend is measured on: z for the west and east roads, x for the south and north ones.</summary>
            public static Vector3 AcrossAxis(Vector3 outward) => outward.x != 0f ? Vector3.forward : Vector3.right;

            /// <summary>Road weight: 1 on the track, soft at its shoulders, 0 elsewhere.</summary>
            public float Road(float x, float z) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadHalfWidth, RoadHalfWidth + 0.14f, RoadDistance(x, z)));

            /// <summary>
            /// Material weights for the ground shader: r soil (banks, fords, forest floor, roads, worn ground by the Keep),
            /// g sand (riverbed), b rock (rock cells), a lush grass.
            /// </summary>
            public Color Weights(float x, float z, float h)
            {
                const float size = GridCoordinates.Size;
                var lush = 0.45f + 0.55f * Mathf.PerlinNoise(x * 0.13f + 40f, z * 0.13f + 13f);
                var worn = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.42f + 5f, z * 0.42f + 21f) - 0.62f) * 2.2f) * 0.35f;
                if (x < 0f || z < 0f || x > size || z > size)
                {
                    var sandOut = Mathf.Clamp01((-0.12f - h) * 9f);
                    var road = Road(x, z);
                    var bx = Mathf.Max(0f, Mathf.Max(-x, x - size));
                    var bz = Mathf.Max(0f, Mathf.Max(-z, z - size));
                    var floor = Mathf.Clamp01((Mathf.Sqrt(bx * bx + bz * bz) - 1.4f) / 2.5f); // drier, worn ground further out
                    var soilOut = Mathf.Max(Mathf.Clamp01(0.15f + worn + 0.35f * floor), 0.9f * road);
                    return new Color(soilOut, sandOut, 0f, lush * Mathf.Lerp(0.7f, 0.15f, road));
                }

                var cell = new GridPos(Mathf.Min((int)x, (int)size - 1), Mathf.Min((int)z, (int)size - 1));
                float fx = x - cell.X, fz = z - cell.Z;
                var soil = worn;
                var sand = Mathf.Clamp01((-0.05f - h) * 10f) * 0.85f;
                var rock = 0f;
                var type = Type(cell);
                if (Channel(cell) || Ford(cell)) soil = Mathf.Max(soil, 0.55f);
                else
                {
                    var toWater = SideDistance(cell, fx, fz, River);
                    soil = Mathf.Max(soil, Mathf.Clamp01(1f - toWater / 0.3f) * 0.65f);
                    lush = Mathf.Max(lush, Mathf.Clamp01(1f - toWater / 0.9f));
                }

                // Spec 017: fertile meadows read as rich green with soft furrows; other ground on a fertile-list map is drier.
                // The meadow mask is the fertile share of a ~1-cell blur, with a noisy threshold: the edge falls on the cell
                // border but reads as an organic patch, not a staircase. Every cell type gets it, so forest and rock cells
                // blend into the ground around them instead of keeping a square of the old green.
                if (map?.Fertile != null)
                {
                    float sum = 0f, total = 0f;
                    for (var dz = -2; dz <= 2; dz++)
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        var n = new GridPos(cell.X + dx, cell.Z + dz);
                        var ox = fx - 0.5f - dx;
                        var oz = fz - 0.5f - dz;
                        var w = Mathf.Max(0f, 1f - Mathf.Sqrt(ox * ox + oz * oz) / MeadowBlur);
                        if (w <= 0f) continue;
                        total += w;
                        if (IsFertile(n)) sum += w;
                    }

                    var wobble = (Mathf.PerlinNoise(x * 1.3f + 7f, z * 1.3f + 31f) - 0.5f) * 0.32f;
                    var fertile = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, sum / total + wobble));
                    var furrow = Mathf.Clamp01((Mathf.Sin((x + z) * Mathf.PI * 2.5f) - 0.55f) * 3f) * 0.14f;
                    lush = Mathf.Lerp(Mathf.Min(lush, 0.32f), 1f, fertile);
                    soil = Mathf.Lerp(Mathf.Max(soil, 0.3f), Mathf.Max(furrow, Mathf.Min(soil, 0.55f)), fertile);
                }

                // Blocked cells read as blocked: rock ground under boulders, needle litter under trees (soft at the cell edge).
                // Round patches (merging across neighbouring blocked cells), not squares that would read as building plots.
                var inner = 0f;
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var n = new GridPos(cell.X + dx, cell.Z + dz);
                    if (Type(n) != type || !GridCoordinates.IsInBounds(n)) continue;
                    var ox = fx - 0.5f - dx;
                    var oz = fz - 0.5f - dz;
                    inner = Mathf.Max(inner, Mathf.Clamp01((0.62f - Mathf.Sqrt(ox * ox + oz * oz)) / 0.25f));
                }
                if (type == TerrainType.Rock) rock = 0.85f * inner;
                if (type == TerrainType.Forest)
                {
                    soil = Mathf.Max(soil, 0.5f * inner);
                    lush = Mathf.Lerp(lush, 0.85f, inner); // greener under the canopy, fading into the ground around it
                }

                if (tracks.Contains(cell)) soil = Mathf.Max(soil, 0.3f); // a worn track along an entry's route

                var k = map?.Keep ?? GridState.KeepCell; // the prototype scene has no map
                var keep = Mathf.Max(Mathf.Abs(x - k.X - 0.5f), Mathf.Abs(z - k.Z - 0.5f));
                soil = Mathf.Max(soil, Mathf.Clamp01(1.4f - keep) * 0.5f); // a trodden yard around the Keep
                return new Color(soil, sand, rock, lush);
            }
        }

        /// <summary>Builds the ground and river meshes for <paramref name="map"/> under <see cref="terrainRoot"/>.</summary>
        void BuildTerrain(TerrainMap map)
        {
            var shape = new TerrainShape(map);
            terrainShape = shape;
            var min = -TerrainMargin;
            var count = Mathf.RoundToInt((GridCoordinates.Size + 2f * TerrainMargin) / TerrainStep) + 1;
            var heights = new float[count * count];
            var verts = new List<Vector3>(count * count);
            var colors = new List<Color>(count * count);
            for (var j = 0; j < count; j++)
            for (var i = 0; i < count; i++)
            {
                float x = min + i * TerrainStep, z = min + j * TerrainStep;
                var h = shape.Height(x, z);
                heights[j * count + i] = h;
                verts.Add(new Vector3(x, h, z));
                colors.Add(shape.Weights(x, z, h));
            }

            var tris = new List<int>((count - 1) * (count - 1) * 6);
            for (var j = 0; j + 1 < count; j++)
            for (var i = 0; i + 1 < count; i++)
            {
                int a = j * count + i, b = a + 1, c = a + count, d = c + 1;
                if (heights[a] < -0.9f && heights[b] < -0.9f && heights[c] < -0.9f && heights[d] < -0.9f) continue;
                // Split along the diagonal that follows the slope, so banks and cliffs keep clean edges.
                if (Mathf.Abs(heights[a] - heights[d]) <= Mathf.Abs(heights[b] - heights[c]))
                {
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
                else
                {
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            var ground = TerrainMesh("TerrainGround", verts, tris, terrainGround, false);
            ground.GetComponent<MeshFilter>().sharedMesh.SetColors(colors);
            BuildRivers(shape);
            BuildGrass(shape);
        }

        GameObject TerrainMesh(string name, List<Vector3> verts, List<int> tris, Material material, bool flat)
        {
            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            terrainMeshes.Add(mesh);
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            if (flat)
            {
                var normals = new List<Vector3>(verts.Count);
                for (var i = 0; i < verts.Count; i++) normals.Add(Vector3.up);
                mesh.SetNormals(normals);
            }
            else mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(terrainRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>River and pond water over every channel cell, a film over fords, and outlets running on into the mist.</summary>
        void BuildRivers(TerrainShape shape)
        {
            const int sub = 5;
            var verts = new List<Vector3>();
            var depth = new List<Vector2>();
            var tris = new List<int>();
            void Patch(float x0, float z0, float sx, float sz, System.Func<float, float, float, float> surface)
            {
                var k = verts.Count;
                for (var j = 0; j <= sub; j++)
                for (var i = 0; i <= sub; i++)
                {
                    float x = x0 + sx * i / sub, z = z0 + sz * j / sub;
                    var h = shape.Height(x, z);
                    var y = surface(x, z, h);
                    verts.Add(new Vector3(x, y, z));
                    depth.Add(new Vector2(y - h, 0f));
                }

                for (var j = 0; j < sub; j++)
                for (var i = 0; i < sub; i++)
                {
                    int a = k + j * (sub + 1) + i, b = a + 1, c = a + sub + 1, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
            }

            var any = false;
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var cell = new GridPos(x, z);
                if (shape.Channel(cell)) Patch(x, z, 1f, 1f, (_, _, h) => RiverLevel);
                else if (shape.Ford(cell)) Patch(x, z, 1f, 1f, (_, _, h) => Mathf.Max(RiverLevel, Mathf.Min(-0.006f, h + 0.026f)));
                else continue;
                any = true;
                if (!GridCoordinates.IsReservedRing(cell) || !shape.Channel(cell)) continue;
                // Outlet: the river runs on in its channel from the board edge until the mist has swallowed it.
                var outward = RouteOverlay.Outward(cell);
                const float run = 7f;
                float ox = outward.x < 0 ? x - run : outward.x > 0 ? x + 1f : x;
                float oz = outward.z < 0 ? z - run : outward.z > 0 ? z + 1f : z;
                float sx = outward.x != 0 ? run : 1f, sz = outward.z != 0 ? run : 1f;
                Patch(ox, oz, sx, sz, (_, _, h) => RiverLevel);
            }

            if (!any) return;
            var river = TerrainMesh("RiverWater", verts, tris, riverWater, true);
            river.GetComponent<MeshFilter>().sharedMesh.SetUVs(1, depth);
        }
    }
}
