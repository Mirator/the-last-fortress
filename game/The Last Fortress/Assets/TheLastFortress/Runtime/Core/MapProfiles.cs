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

        /// <summary>The accepted map for a seed: first valid attempt, otherwise the validated skeleton marked as a fallback.</summary>
        public static TerrainMap Generate(string id, uint seed)
        {
            var profile = Get(id);
            for (var attempt = 0; attempt < MapGenerator.MaxAttempts; attempt++)
            {
                var map = Candidate(profile, seed, attempt);
                if (Validate(profile, map) == null) return map;
            }

            var fallback = Skeleton(profile, seed);
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

        /// <summary>Null when the map satisfies the common rules and the profile's named constraint; otherwise the first failure.</summary>
        public static string Validate(Profile profile, TerrainMap map)
        {
            var common = MapGenerator.Validate(map); // open routes from all four entries, no isolated pocket, 2 Farm sites
            if (common != null) return common;
            var sites = CountDisjointSites(map);
            if (sites < profile.MinDisjointSites) return $"only {sites} disjoint buildable 2x2 sites (needs {profile.MinDisjointSites})";
            return profile.Constraint(map);
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
