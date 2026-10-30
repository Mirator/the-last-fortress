using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>Stable serialized IDs: append only. Forest/Rock/Water are unwalkable, unbuildable and indestructible.</summary>
    public enum TerrainType
    {
        Land = 0,
        Forest = 1,
        Rock = 2,
        Water = 3,
    }

    /// <summary>A 20 x 20 terrain layout plus how it was produced (for logs/saves; a fallback never poses as generated).</summary>
    public sealed class TerrainMap
    {
        public readonly TerrainType[] Cells = new TerrainType[GridCoordinates.Size * GridCoordinates.Size];
        public uint Seed;

        /// <summary>Accepted attempt index 0..19, or -1 for the authored map.</summary>
        public int Attempt = -1;

        /// <summary>True when every generated attempt failed validation and the authored all-land map is used.</summary>
        public bool Fallback;

        /// <summary>True for the explicitly selected flat authored map (not a generation result).</summary>
        public bool Authored;

        /// <summary>Plan 008 map profile that produced it ("" for the MVP generator or the flat map).</summary>
        public string Profile = "";

        /// <summary>Plan 008: which of the 8 board symmetries (rotations/mirrors) was applied to the profile layout.</summary>
        public int Symmetry;

        /// <summary>Spec 016: gold vein cells (land; a Mine may only stand on one). Empty for the MVP, the flat map and older saves.</summary>
        public readonly List<GridPos> Veins = new List<GridPos>();

        /// <summary>Spec 017: the Keep's cell (the fixed centre for the MVP, the flat map and the plan 008 profiles).</summary>
        public GridPos Keep = GridState.KeepCell;

        /// <summary>
        /// Spec 017: the map's entries in campaign index order (the table's W, E, N, S are indices 0-3). Null for the MVP and
        /// the flat map, whose campaigns name the side midpoints directly.
        /// </summary>
        public List<GridPos> Entries;

        public TerrainType this[GridPos c] => Cells[c.Z * GridCoordinates.Size + c.X];

        /// <summary>The map's entries, or the four side midpoints when it lists none.</summary>
        public IReadOnlyList<GridPos> EntriesOrSides() =>
            Entries ?? (IReadOnlyList<GridPos>)new[] { MvpCampaign.West, MvpCampaign.East, MvpCampaign.North, MvpCampaign.South };

        public bool IsVein(GridPos c) => Veins.Contains(c);

        /// <summary>
        /// Spec 017 (release-rules-4): fertile land cells, which pay a House or Farm beside them while free. Null means every
        /// land cell is fertile: a save from before the list (the D9-a rule), the MVP and the flat map.
        /// </summary>
        public HashSet<GridPos> Fertile;

        public List<GridPos> ImpassableCells()
        {
            var list = new List<GridPos>();
            for (var i = 0; i < Cells.Length; i++)
                if (Cells[i] != TerrainType.Land) list.Add(new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size));
            return list;
        }

        public static TerrainMap Flat() => new TerrainMap { Authored = true };

        public string Describe() =>
            Authored ? "flat authored map"
            : Profile != "" ? (Fallback ? $"{Profile} seed {Seed}: FALLBACK skeleton (20 attempts invalid)" : $"{Profile} seed {Seed}: generated (attempt {Attempt})")
            : Fallback ? $"seed {Seed}: FALLBACK flat map (20 attempts invalid)" : $"seed {Seed}: generated (attempt {Attempt})";
    }

    /// <summary>
    /// Deterministic terrain from plan 005: per attempt, xorshift32 seeded with seed ^ 0xA341316C ^ (attempt * 0x9E3779B9);
    /// partial Fisher-Yates over eligible cells in (z,x) order picks 30 cells: 10 forest, 10 rock, 10 water. Candidates are
    /// validated with the real route planner plus a flood fill; up to 20 attempts, then the authored all-land fallback.
    /// The reward stream is separate and never touched here.
    /// </summary>
    public static class MapGenerator
    {
        public const int MaxAttempts = 20;
        public const int ObstaclesPerType = 10;

        static readonly GridPos[] Entries = { MvpCampaign.West, MvpCampaign.East, MvpCampaign.North, MvpCampaign.South };

        /// <summary>Boundary ring, Keep, the four entries and the central 8..12 square always stay land.</summary>
        public static bool IsEligible(GridPos c) =>
            !GridCoordinates.IsReservedRing(c) && !(c.X >= 8 && c.X <= 12 && c.Z >= 8 && c.Z <= 12);

        public static TerrainMap Generate(uint seed)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var map = Candidate(seed, attempt);
                if (Validate(map) == null) return map;
            }

            return new TerrainMap { Seed = seed, Fallback = true };
        }

        public static TerrainMap Candidate(uint seed, int attempt)
        {
            var rng = RunRandom.ForMapAttempt(seed, attempt);
            var eligible = new List<GridPos>();
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var c = new GridPos(x, z);
                if (IsEligible(c)) eligible.Add(c);
            }

            var picks = ObstaclesPerType * 3;
            for (var i = 0; i < picks; i++)
            {
                var j = i + (int)rng.NextBounded((uint)(eligible.Count - i));
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }

            var map = new TerrainMap { Seed = seed, Attempt = attempt };
            for (var i = 0; i < picks; i++)
            {
                var type = i < ObstaclesPerType ? TerrainType.Forest : i < ObstaclesPerType * 2 ? TerrainType.Rock : TerrainType.Water;
                var c = eligible[i];
                map.Cells[c.Z * GridCoordinates.Size + c.X] = type;
            }

            return map;
        }

        /// <summary>Null when valid, otherwise the first failed rule.</summary>
        public static string Validate(TerrainMap map)
        {
            var grid = new GridState(map.ImpassableCells());
            grid.Install(new Building(1, PrototypeDefinitions.CreateDefault().Keep, GridState.KeepCell, 0));
            var planner = new RoutePlanner();
            foreach (var entry in Entries)
            {
                var route = planner.Plan(grid, entry, null);
                if (!route.Found || route.IsBreach) return $"entry {entry} has no open route to a Keep neighbor";
            }

            // Every walkable land cell except the Keep belongs to one region (no isolated pockets).
            var seen = new bool[GridCoordinates.Size * GridCoordinates.Size];
            var queue = new Queue<GridPos>();
            queue.Enqueue(Entries[0]);
            seen[Index(Entries[0])] = true;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in GridPos.CardinalOrder)
                {
                    var n = c + d;
                    if (!GridCoordinates.IsInBounds(n) || seen[Index(n)] || n == GridState.KeepCell || map[n] != TerrainType.Land) continue;
                    seen[Index(n)] = true;
                    queue.Enqueue(n);
                }
            }

            for (var i = 0; i < seen.Length; i++)
            {
                var c = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                if (map.Cells[i] == TerrainType.Land && c != GridState.KeepCell && !seen[i]) return $"isolated walkable pocket at {c}";
            }

            return HasTwoDisjointFarmSites(map) ? null : "fewer than two disjoint buildable 2x2 Farm sites";
        }

        static int Index(GridPos c) => c.Z * GridCoordinates.Size + c.X;

        static bool Buildable(TerrainMap map, GridPos c) =>
            GridCoordinates.IsInBounds(c) && !GridCoordinates.IsReservedRing(c) && c != GridState.KeepCell && map[c] == TerrainType.Land;

        public static bool HasTwoDisjointFarmSites(TerrainMap map)
        {
            var sites = new List<GridPos>();
            for (var z = 0; z < GridCoordinates.Size - 1; z++)
            for (var x = 0; x < GridCoordinates.Size - 1; x++)
            {
                var a = new GridPos(x, z);
                var ok = true;
                foreach (var c in BuildingDefinition.FootprintCells(BuildingKind.Farm, a))
                    if (!Buildable(map, c)) ok = false;
                if (ok) sites.Add(a);
            }

            for (var i = 0; i < sites.Count; i++)
            for (var j = i + 1; j < sites.Count; j++)
                if (System.Math.Abs(sites[i].X - sites[j].X) >= 2 || System.Math.Abs(sites[i].Z - sites[j].Z) >= 2)
                    return true;
            return false;
        }
    }
}
