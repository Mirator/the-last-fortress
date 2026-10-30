using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 008 tactical map profiles (specs/002-game-rules.md section 11): an authored skeleton plus bounded seeded variation, validated
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
        /// mirrored). The campaign's entries turn with it (<see cref="EntryFor"/>), so every orientation keeps the profile's
        /// promise: the fronts that cross rivers or run down channels do so on every seed. Drawn from a stream separate
        /// from waves, rewards and terrain variation.
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

        /// <summary>The profile-layout cell that symmetry <paramref name="s"/> moves to <paramref name="c"/> (inverse of <see cref="Apply"/>).</summary>
        public static GridPos Inverse(int s, GridPos c)
        {
            var max = GridCoordinates.Size - 1;
            for (var i = 0; i < (s & 3); i++) c = new GridPos(c.Z, max - c.X);
            if (s >= 4) c = new GridPos(max - c.X, c.Z);
            return c;
        }

        /// <summary>
        /// The entry that plays the role of <paramref name="entry"/> (a side midpoint written for the profile's own layout) on
        /// the board turned by symmetry <paramref name="s"/>: the standard midpoint of the side it lands on. Snapping to the
        /// midpoint keeps every entry in line with the Keep; the 20-cell board has no centre cell, so the raw image of
        /// (0, 10) can land one cell off it.
        /// </summary>
        public static GridPos EntryFor(int s, GridPos entry)
        {
            var c = Apply(s, entry);
            var max = GridCoordinates.Size - 1;
            if (c.X == 0) return MvpCampaign.West;
            if (c.X == max) return MvpCampaign.East;
            if (c.Z == 0) return MvpCampaign.South;
            return c.Z == max ? MvpCampaign.North : c;
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

            /// <summary>
            /// Profile-specific rule on a candidate without buildings, checked on the board as played: the map in its symmetry
            /// with the entries turned to match (<see cref="EntryFor"/>). Null when satisfied, otherwise why not.
            /// </summary>
            public Func<TerrainMap, int, string> Constraint = (_, _) => null;
        }

        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0 || MapRecipes.IsRecipe(id); // spec 017: recipe maps are stored whole

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
            p.Constraint = (map, s) =>
            {
                for (var x = 2; x <= 17; x++)
                    if ((x < 9 || x > 11) && (map[Apply(s, new GridPos(x, 7))] != TerrainType.Rock || map[Apply(s, new GridPos(x, 13))] != TerrainType.Rock))
                        return "ridge broken";
                foreach (var front in new[] { MvpCampaign.West, MvpCampaign.East })
                {
                    var entry = EntryFor(s, front);
                    foreach (var shown in PlanOpen(map, entry).Cells)
                    {
                        var c = Inverse(s, shown);
                        if (((c.X >= 2 && c.X <= 8) || (c.X >= 12 && c.X <= 17)) && (c.Z < 8 || c.Z > 12))
                            return $"{WaveText.EntryName(entry)} route leaves its channel at {shown}";
                    }
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
            p.Constraint = (map, s) =>
            {
                if (!Crosses(PlanOpen(map, EntryFor(s, MvpCampaign.West)), WestFords, s)) return "west front does not use a ford";
                if (!Crosses(PlanOpen(map, EntryFor(s, MvpCampaign.East)), EastFords, s)) return "east front does not use a ford";
                return null;
            };
            return p;
        }

        static readonly int[] RiverColumns = { 5, 14 };

        /// <summary>
        /// Presentation only: the walkable cells on a Crossing river's line (its fords, and where it passes under the
        /// boundary ring). Found by matching the map's own water against the skeleton, so a restored save, which does not
        /// store its symmetry, draws the same river. Empty for any other profile.
        /// </summary>
        public static List<GridPos> RiverCrossings(TerrainMap map)
        {
            var cells = new List<GridPos>();
            if (map != null && map.Fords.Count > 0) return new List<GridPos>(map.Fords); // spec 017 recipe rivers
            if (map == null || map.Profile != Crossing) return cells;
            var skeleton = CreateCrossing().Skeleton;
            for (var s = 0; s < 8; s++)
            {
                var matches = true;
                foreach (var kv in skeleton)
                {
                    if (kv.Value != TerrainType.Water || map[Apply(s, kv.Key)] == TerrainType.Water) continue;
                    matches = false;
                    break;
                }

                if (!matches) continue;
                foreach (var x in RiverColumns)
                for (var z = 0; z < GridCoordinates.Size; z++)
                {
                    var c = Apply(s, new GridPos(x, z));
                    if (map[c] == TerrainType.Land) cells.Add(c);
                }

                break;
            }

            return cells;
        }

        static bool Crosses(Route route, GridPos[] fords, int s)
        {
            foreach (var c in route.Cells)
                if (Array.IndexOf(fords, Inverse(s, c)) >= 0) return true;
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
                if (Validate(profile, map) != null) continue;
                Finish(map, symmetry);
                return map;
            }

            var fallback = Transform(Skeleton(profile, seed), symmetry);
            fallback.Fallback = true;
            Finish(fallback, symmetry);
            return fallback;
        }

        /// <summary>Spec 017: a profile map keeps the centre Keep and lists the four side midpoints, turned, as entries 0-3; then veins and meadows.</summary>
        static void Finish(TerrainMap map, int symmetry)
        {
            map.Keep = GridState.KeepCell;
            map.Entries = new List<GridPos>
            {
                EntryFor(symmetry, MvpCampaign.West), EntryFor(symmetry, MvpCampaign.East), EntryFor(symmetry, MvpCampaign.North), EntryFor(symmetry, MvpCampaign.South),
            };
            PlaceVeins(map);
            PlaceFertile(map);
        }

        public const uint FertileSalt = 0x1B873593;
        public const int FertileMeadows = 4, FertileMeadowsOpen = 6, FertileRadius = 2, FertileSpacing = 4, FertileKeepDistance = 3;

        /// <summary>
        /// Spec 017, provisional until the map recipes (plan 037 stage 4): fertile meadows on the board as played. From their
        /// own stream (seed ^ FertileSalt), meadow centres are drawn from land cells at Chebyshev 3-8 from the Keep, at least 4
        /// apart; each meadow is the land within Manhattan distance 2 of its centre, never the reserved ring, a vein or a Keep
        /// neighbour. 4 meadows (6 on the open Meadow profile); fewer when the board has no room.
        /// </summary>
        public static void PlaceFertile(TerrainMap map)
        {
            map.Fertile = new HashSet<GridPos>();
            var keep = map.Keep;
            var centres = new List<GridPos>();
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var c = new GridPos(x, z);
                var d = Chebyshev(c, keep);
                if (d >= FertileKeepDistance && d <= 8 && !GridCoordinates.IsReservedRing(c) && map[c] == TerrainType.Land) centres.Add(c);
            }

            var rng = new RunRandom(map.Seed ^ FertileSalt);
            var want = map.Profile == Meadow ? FertileMeadowsOpen : FertileMeadows;
            var chosen = new List<GridPos>();
            for (var i = 0; i < centres.Count && chosen.Count < want; i++)
            {
                var j = i + (int)rng.NextBounded((uint)(centres.Count - i));
                (centres[i], centres[j]) = (centres[j], centres[i]);
                var c = centres[i];
                if (chosen.Exists(o => Chebyshev(o, c) < FertileSpacing)) continue;
                chosen.Add(c);
                for (var dz = -FertileRadius; dz <= FertileRadius; dz++)
                for (var dx = -FertileRadius; dx <= FertileRadius; dx++)
                {
                    var f = new GridPos(c.X + dx, c.Z + dz);
                    if (Math.Abs(dx) + Math.Abs(dz) > FertileRadius || !GridCoordinates.IsInBounds(f) || GridCoordinates.IsReservedRing(f)) continue;
                    if (map[f] != TerrainType.Land || map.IsVein(f) || Chebyshev(f, keep) <= 1) continue;
                    map.Fertile.Add(f);
                }
            }
        }

        public const uint VeinSalt = 0x3C6EF372;
        public const int VeinCount = 3, VeinKeepDistance = 5, VeinRouteReach = 2, VeinSpacing = 3;

        /// <summary>
        /// Spec 016 gold veins on the board as played: buildable land at Chebyshev distance at least 5 from the Keep, off every
        /// entry's open route but within 2 of one, never on a river's line, at least 3 apart. Candidates in (z,x) order are
        /// shuffled by their own stream (seed ^ VeinSalt); the first pass takes one per quadrant around the Keep, the second
        /// fills up to three. Fewer when the board has no room; none is still a valid map.
        /// </summary>
        public static void PlaceVeins(TerrainMap map)
        {
            map.Veins.Clear();
            var route = new HashSet<GridPos>();
            foreach (var entry in map.EntriesOrSides())
            {
                var r = PlanOpen(map, entry);
                if (r.Found && !r.IsBreach) route.UnionWith(r.Cells);
            }

            var river = new HashSet<GridPos>(RiverCrossings(map));
            var candidates = new List<GridPos>();
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var c = new GridPos(x, z);
                if (GridCoordinates.IsReservedRing(c) || map[c] != TerrainType.Land || route.Contains(c) || river.Contains(c)) continue;
                if (Chebyshev(c, map.Keep) < VeinKeepDistance) continue;
                var near = false;
                foreach (var r in route)
                    if (Chebyshev(c, r) <= VeinRouteReach) { near = true; break; }
                if (near) candidates.Add(c);
            }

            var rng = new RunRandom(map.Seed ^ VeinSalt);
            for (var i = 0; i < candidates.Count; i++)
            {
                var j = i + (int)rng.NextBounded((uint)(candidates.Count - i));
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            var quadrants = new bool[4];
            for (var pass = 0; pass < 2; pass++)
            foreach (var c in candidates)
            {
                if (map.Veins.Count >= VeinCount) return;
                var q = (c.X < map.Keep.X ? 0 : 1) + (c.Z < map.Keep.Z ? 0 : 2);
                if (pass == 0 && quadrants[q]) continue;
                if (map.Veins.Exists(v => v == c || Chebyshev(v, c) < VeinSpacing)) continue;
                map.Veins.Add(c);
                quadrants[q] = true;
            }
        }

        static int Chebyshev(GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

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
        /// Null when the map as played (its symmetry, with the entries turned to match) satisfies the common rules and the
        /// profile's named constraint; otherwise the first failure.
        /// </summary>
        public static string Validate(Profile profile, TerrainMap map)
        {
            var common = MapGenerator.Validate(map); // open routes from all four entries, no isolated pocket, 2 Farm sites
            if (common != null) return common;
            var sites = CountDisjointSites(map);
            if (sites < profile.MinDisjointSites) return $"only {sites} disjoint buildable 2x2 sites (needs {profile.MinDisjointSites})";
            return profile.Constraint(map, map.Symmetry);
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

        /// <summary>Spec 017: an entry's route on the empty board as played (roads and the worn tracks follow it).</summary>
        public static Route OpenRoute(TerrainMap map, GridPos entry) => PlanOpen(map, entry);

        static Route PlanOpen(TerrainMap map, GridPos entry)
        {
            var grid = new GridState(map.ImpassableCells(), map.Keep);
            grid.Install(new Building(1, PrototypeDefinitions.CreateDefault().Keep, map.Keep, 0));
            return new RoutePlanner().Plan(grid, entry, null);
        }
    }
}
