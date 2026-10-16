using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 008 tactical map profiles (008-rules.md section 5): an authored skeleton plus bounded seeded variation, validated
    /// with the real route planner. Up to 20 attempts, then the skeleton alone (flagged as a fallback). The six-night
    /// MVP keeps <see cref="MapGenerator"/> unchanged.
    /// </summary>
    public static class MapProfiles
    {
        public const string Ravine = "ravine";
        public const string Crossing = "crossing";
        public const string Meadow = "meadow";

        public static readonly string[] All = { Ravine, Crossing, Meadow };

        /// <summary>
        /// Each seed shows its profile in one of the 8 symmetries of the square board (4 rotations, each optionally
        /// mirrored), so the same profile changes which entries face its corridors, rivers and open land. Drawn from a
        /// stream separate from waves, rewards and terrain variation.
        /// </summary>
        public const uint SymmetrySalt = 0x2545F491;

        /// <summary>When the player leaves the map to chance, the seed picks the profile from this separate stream.</summary>
        public const uint ProfileSalt = 0x68E31DA4;

        public static int SymmetryFor(uint seed) => (int)new RunRandom(seed ^ SymmetrySalt).NextBounded(8);

        public static string ForSeed(uint seed) => All[(int)new RunRandom(seed ^ ProfileSalt).NextBounded((uint)All.Length)];

        public static readonly string[] SymmetryNames = { "", "turned 90°", "turned 180°", "turned 270°", "mirrored", "mirrored, turned 90°", "mirrored, turned 180°", "mirrored, turned 270°" };

        /// <summary>Cell of the board after symmetry <paramref name="s"/>: optional mirror (x → 19 − x), then s mod 4 quarter turns.</summary>
        public static GridPos Apply(int s, GridPos c)
        {
            var max = GridCoordinates.Size - 1;
            if (s >= 4) c = new GridPos(max - c.X, c.Z);
            for (var i = 0; i < (s & 3); i++) c = new GridPos(max - c.Z, c.X);
            return c;
        }

        /// <summary>The map under symmetry <paramref name="s"/>; the ring maps to the ring, so the four entries stay land.</summary>
        public static TerrainMap Transform(TerrainMap source, int s)
        {
            var map = new TerrainMap { Seed = source.Seed, Attempt = source.Attempt, Fallback = source.Fallback, Profile = source.Profile, Symmetry = s };
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var to = Apply(s, new GridPos(x, z));
                map.Cells[to.Z * GridCoordinates.Size + to.X] = source[new GridPos(x, z)];
            }

            return map;
        }

        /// <summary>The profile's own (untransformed) layout of a generated map, where its named constraint is defined.</summary>
        public static TerrainMap Canonical(TerrainMap map)
        {
            if (map.Symmetry == 0) return map;
            var canonical = new TerrainMap { Seed = map.Seed, Attempt = map.Attempt, Fallback = map.Fallback, Profile = map.Profile };
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
                canonical.Cells[z * GridCoordinates.Size + x] = map[Apply(map.Symmetry, new GridPos(x, z))];
            return canonical;
        }

        public sealed class Profile
        {
            public string Id;
            public string DisplayName;
            public string Summary;
            public uint Salt;
            public int VariationCount;
            public int MinDisjointSites;
            public Dictionary<GridPos, TerrainType> Skeleton = new Dictionary<GridPos, TerrainType>();
            public Func<GridPos, bool> Protected = _ => false;

            /// <summary>Profile-specific rule on a candidate without buildings: null when satisfied, otherwise why not.</summary>
            public Func<TerrainMap, string> Constraint = _ => null;
        }

        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;

        public static Profile Get(string id)
        {
            switch (id)
            {
                case Ravine: return CreateRavine();
                case Crossing: return CreateCrossing();
                case Meadow: return CreateMeadow();
                default: throw new ArgumentException($"Unknown map profile '{id}'.", nameof(id));
            }
        }

        static bool Central(GridPos c) => c.X >= 8 && c.X <= 12 && c.Z >= 8 && c.Z <= 12;

        static void Put(Profile p, TerrainType t, params (int x, int z)[] cells)
        {
            foreach (var (x, z) in cells) p.Skeleton[new GridPos(x, z)] = t;
        }

        // Rock ridges at z = 7 and 13 make 5-wide W/E channels; N/S come through the central gap.
        static Profile CreateRavine()
        {
            var p = new Profile
            {
                Id = Ravine, DisplayName = "Ravine", Summary = "Long west and east channels between rock ridges: corridors and kill zones.",
                Salt = 0x1F3D5B79, VariationCount = 8, MinDisjointSites = 4,
            };
            for (var x = 2; x <= 17; x++)
            {
                if (x >= 9 && x <= 11) continue;
                p.Skeleton[new GridPos(x, 7)] = TerrainType.Rock;
                p.Skeleton[new GridPos(x, 13)] = TerrainType.Rock;
            }

            Put(p, TerrainType.Forest, (3, 3), (4, 3), (3, 4), (15, 3), (16, 3), (16, 4), (3, 16), (3, 15), (4, 16), (16, 16), (15, 16), (16, 15));
            p.Protected = c => (c.Z >= 8 && c.Z <= 12) || (c.X >= 9 && c.X <= 11);
            p.Constraint = map =>
            {
                for (var x = 2; x <= 17; x++)
                    if ((x < 9 || x > 11) && (map[new GridPos(x, 7)] != TerrainType.Rock || map[new GridPos(x, 13)] != TerrainType.Rock))
                        return "ridge broken";
                foreach (var entry in new[] { MvpCampaign.West, MvpCampaign.East })
                {
                    var route = PlanOpen(map, entry);
                    foreach (var c in route.Cells)
                        if (((c.X >= 2 && c.X <= 8) || (c.X >= 12 && c.X <= 17)) && (c.Z < 8 || c.Z > 12))
                            return $"{WaveText.EntryName(entry)} route leaves its channel at {c}";
                }

                return null;
            };
            return p;
        }

        static readonly GridPos[] WestFords = { new GridPos(5, 3), new GridPos(5, 4), new GridPos(5, 15), new GridPos(5, 16) };
        static readonly GridPos[] EastFords = { new GridPos(14, 4), new GridPos(14, 5), new GridPos(14, 14), new GridPos(14, 15) };

        // Two rivers (x = 5 and 14) with two fords each: west and east attackers split onto far-apart crossings.
        static Profile CreateCrossing()
        {
            var p = new Profile
            {
                Id = Crossing, DisplayName = "Crossing", Summary = "Rivers with fords split the west and east attacks into separate fronts.",
                Salt = 0x6A09E667, VariationCount = 8, MinDisjointSites = 4,
            };
            for (var z = 1; z <= 18; z++)
            {
                var w = new GridPos(5, z);
                var e = new GridPos(14, z);
                if (Array.IndexOf(WestFords, w) < 0) p.Skeleton[w] = TerrainType.Water;
                if (Array.IndexOf(EastFords, e) < 0) p.Skeleton[e] = TerrainType.Water;
            }

            Put(p, TerrainType.Forest, (2, 8), (2, 12), (17, 8), (17, 12));
            p.Protected = c =>
            {
                foreach (var f in WestFords)
                    if (c.ManhattanDistance(f) <= 1) return true;
                foreach (var f in EastFords)
                    if (c.ManhattanDistance(f) <= 1) return true;
                return false;
            };
            p.Constraint = map =>
            {
                if (!Crosses(PlanOpen(map, MvpCampaign.West), WestFords)) return "west route does not use a ford";
                if (!Crosses(PlanOpen(map, MvpCampaign.East), EastFords)) return "east route does not use a ford";
                return null;
            };
            return p;
        }

        static bool Crosses(Route route, GridPos[] fords)
        {
            foreach (var c in route.Cells)
                if (Array.IndexOf(fords, c) >= 0) return true;
            return false;
        }

        // Wide open land with four forest clumps: room for an economy, no natural chokepoints.
        static Profile CreateMeadow()
        {
            var p = new Profile
            {
                Id = Meadow, DisplayName = "Meadow", Summary = "Open grassland with room for a large economy and few natural chokepoints.",
                Salt = 0xBB67AE85, VariationCount = 10, MinDisjointSites = 8,
            };
            foreach (var (x0, z0) in new[] { (3, 3), (15, 3), (3, 15), (15, 15) })
                Put(p, TerrainType.Forest, (x0, z0), (x0 + 1, z0), (x0, z0 + 1), (x0 + 1, z0 + 1));
            Put(p, TerrainType.Rock, (6, 15), (13, 4));
            p.Protected = c => c.X >= 7 && c.X <= 13 && c.Z >= 7 && c.Z <= 13;
            return p;
        }

        /// <summary>The accepted map for a seed, in the seed's symmetry.</summary>
        public static TerrainMap Generate(string id, uint seed) => Generate(id, seed, SymmetryFor(seed));

        /// <summary>
        /// First valid attempt in symmetry <paramref name="symmetry"/> (0 = the profile's own orientation), otherwise the
        /// validated skeleton in that symmetry, marked as a fallback.
        /// </summary>
        public static TerrainMap Generate(string id, uint seed, int symmetry)
        {
            var profile = Get(id);
            for (var attempt = 0; attempt < MapGenerator.MaxAttempts; attempt++)
            {
                var map = Transform(Candidate(profile, seed, attempt), symmetry);
                if (Validate(profile, map) == null) return map;
            }

            var fallback = Transform(Skeleton(profile, seed), symmetry);
            fallback.Fallback = true;
            return fallback;
        }

        public static TerrainMap Skeleton(Profile profile, uint seed)
        {
            var map = new TerrainMap { Seed = seed, Attempt = -1, Profile = profile.Id };
            foreach (var kv in profile.Skeleton) map.Cells[kv.Key.Z * GridCoordinates.Size + kv.Key.X] = kv.Value;
            return map;
        }

        /// <summary>Skeleton plus N variation cells by partial Fisher-Yates over the profile's variable cells in (z,x) order.</summary>
        public static TerrainMap Candidate(Profile profile, uint seed, int attempt)
        {
            var map = Skeleton(profile, seed);
            map.Attempt = attempt;
            var eligible = new List<GridPos>();
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var c = new GridPos(x, z);
                if (GridCoordinates.IsReservedRing(c) || Central(c) || profile.Skeleton.ContainsKey(c) || profile.Protected(c)) continue;
                eligible.Add(c);
            }

            var rng = RunRandom.ForMapAttempt(seed ^ profile.Salt, attempt);
            var picks = Math.Min(profile.VariationCount, eligible.Count);
            for (var i = 0; i < picks; i++)
            {
                var j = i + (int)rng.NextBounded((uint)(eligible.Count - i));
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
                var c = eligible[i];
                map.Cells[c.Z * GridCoordinates.Size + c.X] = i % 2 == 0 ? TerrainType.Forest : TerrainType.Rock;
            }

            return map;
        }

        /// <summary>
        /// Null when the map (as shown) satisfies the common rules and the profile's named constraint holds for its own
        /// layout; otherwise the first failure.
        /// </summary>
        public static string Validate(Profile profile, TerrainMap map)
        {
            var common = MapGenerator.Validate(map); // open routes from all four entries, no isolated pocket, 2 Farm sites
            if (common != null) return common;
            var sites = CountDisjointSites(map);
            if (sites < profile.MinDisjointSites) return $"only {sites} disjoint buildable 2x2 sites (needs {profile.MinDisjointSites})";
            return profile.Constraint(Canonical(map));
        }

        /// <summary>Greedy count of non-overlapping buildable 2x2 sites in (z,x) order (a lower bound on the best packing).</summary>
        public static int CountDisjointSites(TerrainMap map)
        {
            var used = new bool[GridCoordinates.Size * GridCoordinates.Size];
            var count = 0;
            for (var z = 0; z < GridCoordinates.Size - 1; z++)
            for (var x = 0; x < GridCoordinates.Size - 1; x++)
            {
                var ok = true;
                foreach (var c in BuildingDefinition.FootprintCells(BuildingKind.Farm, new GridPos(x, z)))
                {
                    if (GridCoordinates.IsReservedRing(c) || c == GridState.KeepCell || map[c] != TerrainType.Land || used[c.Z * GridCoordinates.Size + c.X])
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok) continue;
                foreach (var c in BuildingDefinition.FootprintCells(BuildingKind.Farm, new GridPos(x, z))) used[c.Z * GridCoordinates.Size + c.X] = true;
                count++;
            }

            return count;
        }

        static Route PlanOpen(TerrainMap map, GridPos entry)
        {
            var grid = new GridState(map.ImpassableCells());
            grid.Install(new Building(1, PrototypeDefinitions.CreateDefault().Keep, GridState.KeepCell, 0));
            return new RoutePlanner().Plan(grid, entry, null);
        }
    }
}
