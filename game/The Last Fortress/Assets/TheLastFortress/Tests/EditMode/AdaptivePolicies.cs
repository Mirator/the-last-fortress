using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Spec 017 section 4: the four policy scripts as small heuristic builders that read the board as played (the Keep, every
    /// night's entries, their Day-1 routes, terrain, veins and fertile land) instead of fixed cells. Each returns the same
    /// item lists the fixed scripts use ("Kind x z [tier]", "Mine veinN [tier]"), so <see cref="ReleasePolicyTests.Play"/>
    /// buys them with the same Day pacing. Pacing, economy window and perks are copied from the fixed twins, so a comparison
    /// isolates placement. They check viability only; they are not balance claims. Deterministic: no random numbers, every
    /// choice breaks ties by (z, x).
    /// </summary>
    public static class AdaptivePolicies
    {
        public static readonly ReleasePolicyTests.Policy KillZone = Twin(ReleasePolicyTests.KillZone, BuildKillZone);
        public static readonly ReleasePolicyTests.Policy Corridor = Twin(ReleasePolicyTests.Corridor, BuildCorridor);
        public static readonly ReleasePolicyTests.Policy Economy = Twin(ReleasePolicyTests.Economy, BuildEconomy);
        public static readonly ReleasePolicyTests.Policy HouseRing = Twin(ReleasePolicyTests.HouseRing, BuildHouseRing);

        public static readonly ReleasePolicyTests.Policy[] All = { KillZone, Corridor, Economy, HouseRing };

        static ReleasePolicyTests.Policy Twin(ReleasePolicyTests.Policy fixedScript, Func<RunSimulation, (string[], string[])> build) =>
            new ReleasePolicyTests.Policy
            {
                Name = fixedScript.Name + "*", Map = fixedScript.Map, Recorded = new uint[0], MinDefense = fixedScript.MinDefense,
                EconomyUntilDay = fixedScript.EconomyUntilDay, Perks = fixedScript.Perks, Build = build,
            };

        /// <summary>
        /// Stage 2 stand-in for spec 017's fertile land: every land cell (the D9-a rule for a save without a fertile list).
        /// Stage 1 replaces it with the map's fertile flag.
        /// </summary>
        static bool IsFertile(Board b, GridPos c) => b.IsLand(c);

        // ---------- kill-zone: seal the Keep, ring it with Towers, Cannons on the approaches ----------

        static (string[], string[]) BuildKillZone(RunSimulation sim)
        {
            var b = new Board(sim);
            var d = new List<string>();
            foreach (var o in Diagonals) b.Ring(d, BuildingKind.Tower, o);
            foreach (var o in Cardinals) b.Ring(d, BuildingKind.Wall, o);
            var second = b.ByApproach(Cardinals.Select(o => Scale(o, 2))).ToList(); // the two busiest sides first
            b.Ring(d, BuildingKind.Tower, second[0]);
            b.Ring(d, BuildingKind.Tower, second[1]);
            for (var i = 0; i < 4; i++)
            {
                b.Best(d, BuildingKind.Cannon, c => b.Cheb(c) >= 3 && b.Cheb(c) <= 4 && b.Spaced(c, BuildingKind.Cannon, 3), c => b.Coverage(c, 4.5));
                b.Best(d, BuildingKind.Watchtower, c => b.Cheb(c) == 2, c => 100 * b.AdjacentShooters(c) + b.Coverage(c, 4.0));
            }

            var towers = b.Placed(BuildingKind.Tower);
            foreach (var t in towers.Take(4)) d.Add(Item(BuildingKind.Tower, t, 2));
            b.Ring(d, BuildingKind.Tower, second[2]);
            b.Ring(d, BuildingKind.Tower, second[3]);
            foreach (var c in b.Placed(BuildingKind.Cannon).Take(2)) d.Add(Item(BuildingKind.Cannon, c, 2));
            foreach (var t in towers.Take(2)) d.Add(Item(BuildingKind.Tower, t, 3));
            b.Best(d, BuildingKind.RepairStation, c => b.Cheb(c) >= 2 && b.Cheb(c) <= 4, c => b.NearBuildings(c, 2));
            for (var i = 0; i < 4; i++)
                b.Best(d, BuildingKind.Cannon, c => b.Cheb(c) >= 5 && b.Cheb(c) <= 6 && b.Spaced(c, BuildingKind.Cannon, 3), c => b.Coverage(c, 4.5));
            for (var i = 0; i < 6; i++)
                b.Best(d, BuildingKind.Tower, c => b.Cheb(c) >= 3 && b.Cheb(c) <= 4, c => b.Coverage(c, 4.0));
            return (d.ToArray(), BuildEconomyItems(b, new[] { "H", "H", "M", "H", "F", "H", "F", "H", "F" }));
        }

        // ---------- corridor: traps on the routes, Towers and Cannons beside them, routes kept open ----------

        static (string[], string[]) BuildCorridor(RunSimulation sim)
        {
            var b = new Board(sim) { KeepOpen = true };
            var d = new List<string>();
            void Towers(int n) { for (var i = 0; i < n; i++) b.Best(d, BuildingKind.Tower, c => b.Cheb(c) >= 1 && b.Cheb(c) <= 8 && b.NextToRoute(c), c => b.Coverage(c, 4.0)); }
            void Traps(int n) { for (var i = 0; i < n; i++) b.Best(d, BuildingKind.SpikeTrap, c => b.OnRoute(c) && b.Cheb(c) >= 2 && b.Cheb(c) <= 9, c => 100 * b.RouteWeight(c) - b.Cheb(c)); }
            void Cannons(int n) { for (var i = 0; i < n; i++) b.Best(d, BuildingKind.Cannon, c => b.Cheb(c) >= 2 && b.Cheb(c) <= 8 && b.Spaced(c, BuildingKind.Cannon, 3), c => b.Coverage(c, 4.5)); }
            void Watch(int n) { for (var i = 0; i < n; i++) b.Best(d, BuildingKind.Watchtower, c => b.Cheb(c) >= 1, c => 100 * b.AdjacentShooters(c) + b.Coverage(c, 4.0)); }

            Towers(4); Traps(2); Towers(2); Traps(4); Cannons(2); Watch(2); Towers(2);
            var towers = b.Placed(BuildingKind.Tower);
            foreach (var t in towers.Take(2)) d.Add(Item(BuildingKind.Tower, t, 2));
            Traps(2);
            foreach (var t in b.Placed(BuildingKind.SpikeTrap).Take(2)) d.Add(Item(BuildingKind.SpikeTrap, t, 2));
            foreach (var c in b.Placed(BuildingKind.Cannon).Take(2)) d.Add(Item(BuildingKind.Cannon, c, 2));
            Towers(2); Traps(2); Cannons(2);
            b.Best(d, BuildingKind.RepairStation, c => b.Cheb(c) >= 2 && b.Cheb(c) <= 6, c => b.NearBuildings(c, 2));
            Towers(4); Traps(2); Towers(4); Traps(2); Towers(2);
            return (d.ToArray(), BuildEconomyItems(b, new[] { "H", "H", "M", "F", "H", "H", "F", "H", "F" }));
        }

        // ---------- economy: Farms and Houses on the best open land, Mines on veins, a light Keep guard ----------

        static (string[], string[]) BuildEconomy(RunSimulation sim)
        {
            var b = new Board(sim);
            var d = new List<string>();
            foreach (var o in Diagonals) b.Ring(d, BuildingKind.Tower, o);
            foreach (var o in Cardinals) b.Ring(d, BuildingKind.Wall, o);

            // The economy is planned before the rest of the defence, so Towers and Repair Stations go where the fields are.
            var economy = BuildEconomyItems(b, new[] { "H", "M0", "M1", "H", "M2", "F", "H", "H", "F", "U0", "U1", "H", "H", "F", "H", "H", "F", "H", "H", "UH", "UH", "UF" });
            void Towers(int n, int lo, int hi) { for (var i = 0; i < n; i++) b.Best(d, BuildingKind.Tower, c => b.Cheb(c) >= lo && b.Cheb(c) <= hi, c => b.Coverage(c, 4.0) + 3 * b.NearEconomy(c, 3)); }
            void Cannons(int n) { for (var i = 0; i < n; i++) b.Best(d, BuildingKind.Cannon, c => b.Cheb(c) >= 3 && b.Cheb(c) <= 7 && b.Spaced(c, BuildingKind.Cannon, 3), c => b.Coverage(c, 4.5) + 3 * b.NearEconomy(c, 4)); }
            void Repair() => b.Best(d, BuildingKind.RepairStation, c => b.Cheb(c) >= 2, c => b.NearEconomy(c, 2) * 10 + b.NearBuildings(c, 2));

            Towers(2, 2, 3); Repair();
            b.Best(d, BuildingKind.Watchtower, c => b.Cheb(c) >= 1, c => 100 * b.AdjacentShooters(c) + b.Coverage(c, 4.0));
            Cannons(2); Towers(2, 2, 3); Cannons(2);
            var towers = b.Placed(BuildingKind.Tower);
            foreach (var t in towers.Take(4)) d.Add(Item(BuildingKind.Tower, t, 2));
            Repair();
            b.Best(d, BuildingKind.Watchtower, c => b.Cheb(c) >= 1, c => 100 * b.AdjacentShooters(c) + b.Coverage(c, 4.0));
            foreach (var c in b.Placed(BuildingKind.Cannon).Take(2)) d.Add(Item(BuildingKind.Cannon, c, 2));
            Towers(2, 2, 4); Cannons(2); Towers(6, 2, 5);
            return (d.ToArray(), economy);
        }

        // ---------- house-ring: the owner's layout, Houses sealing and ringing the Keep, Towers around them ----------

        static readonly (int dx, int dz, BuildingKind kind)[] HouseRingLayout =
        {
            (-1, 0, BuildingKind.House), (1, 0, BuildingKind.House), (0, -1, BuildingKind.House), (0, 1, BuildingKind.House),
            (-1, -1, BuildingKind.Tower), (1, 1, BuildingKind.Tower), (-1, 1, BuildingKind.Tower), (1, -1, BuildingKind.Tower),
            (-2, 0, BuildingKind.House), (2, 0, BuildingKind.House), (0, -2, BuildingKind.House), (0, 2, BuildingKind.House),
            (-2, -1, BuildingKind.Tower), (2, 1, BuildingKind.Tower), (-1, 2, BuildingKind.Tower), (1, -2, BuildingKind.Tower),
            (-2, 1, BuildingKind.Tower), (2, -1, BuildingKind.Tower), (-1, -2, BuildingKind.Tower), (1, 2, BuildingKind.Tower),
        };

        static (string[], string[]) BuildHouseRing(RunSimulation sim)
        {
            var b = new Board(sim);
            var d = new List<string>();
            foreach (var (dx, dz, kind) in HouseRingLayout) b.Ring(d, kind, (dx, dz));
            var inner = b.Placed(BuildingKind.Tower);
            foreach (var t in inner.Take(4)) d.Add(Item(BuildingKind.Tower, t, 2));
            foreach (var o in Cardinals) b.Ring(d, BuildingKind.Tower, Scale(o, 3));
            foreach (var t in inner.Skip(4).Take(4)) d.Add(Item(BuildingKind.Tower, t, 2));
            foreach (var o in new[] { (-3, -2), (3, 2), (-2, 3), (2, -3) }) b.Ring(d, BuildingKind.Tower, o);
            foreach (var t in inner.Take(4)) d.Add(Item(BuildingKind.Tower, t, 3));
            return (d.ToArray(), BuildEconomyItems(b, new[] { "F", "F", "H", "H", "F", "H", "F", "H" }));
        }

        // ---------- shared economy picker ----------

        /// <summary>
        /// Economy items in order: H a House, F a Farm, Mn a Mine on vein n (Mn without n = the next vein), Un the Mine on vein
        /// n to tier II, UH / UF the next House / Farm to tier II. Sites are scored by free fertile edge neighbours (what the
        /// land rule pays for), then nearness to the Keep; a site beside a Day-1 route (a raid stop) is avoided.
        /// </summary>
        static string[] BuildEconomyItems(Board b, string[] codes)
        {
            var items = new List<string>();
            var vein = 0;
            int upHouse = 0, upFarm = 0;
            foreach (var code in codes)
            {
                switch (code[0])
                {
                    case 'H':
                        b.Best(items, BuildingKind.House, c => b.Cheb(c) >= 2 && b.Cheb(c) <= 7, c => SiteScore(b, BuildingKind.House, c));
                        break;
                    case 'F':
                        b.Best(items, BuildingKind.Farm, c => b.Cheb(c) >= 2 && b.Cheb(c) <= 7, c => SiteScore(b, BuildingKind.Farm, c));
                        break;
                    case 'M':
                        var n = code.Length > 1 ? code[1] - '0' : vein;
                        vein = Math.Max(vein, n + 1);
                        items.Add($"Mine vein{n}");
                        break;
                    case 'U':
                        if (code[1] == 'H' || code[1] == 'F')
                        {
                            var kind = code[1] == 'H' ? BuildingKind.House : BuildingKind.Farm;
                            var list = b.Placed(kind);
                            var i = kind == BuildingKind.House ? upHouse++ : upFarm++;
                            if (i < list.Count) items.Add(Item(kind, list[i], 2));
                        }
                        else items.Add($"Mine vein{code[1] - '0'} 2");

                        break;
                }
            }

            return items.ToArray();
        }

        static int SiteScore(Board b, BuildingKind kind, GridPos anchor)
        {
            var fertile = b.FreeEdgeNeighbours(kind, anchor, c => IsFertile(b, c));
            return 10 * fertile - 2 * b.Cheb(anchor) - (b.BesideRoute(kind, anchor) ? 15 : 0);
        }

        // ---------- the board as the builders see it ----------

        static readonly (int, int)[] Diagonals = { (-1, -1), (1, 1), (-1, 1), (1, -1) };
        static readonly (int, int)[] Cardinals = { (-1, 0), (1, 0), (0, -1), (0, 1) };

        static (int, int) Scale((int x, int z) o, int k) => (o.x * k, o.z * k);

        static string Item(BuildingKind kind, GridPos c, int tier = 1) => tier > 1 ? $"{kind} {c.X} {c.Z} {tier}" : $"{kind} {c.X} {c.Z}";

        /// <summary>A Day-1 view of the board plus the buildings the plan has already claimed (nothing is placed in the sim).</summary>
        sealed class Board
        {
            readonly RunSimulation sim;
            readonly GridPos keep;
            readonly List<GridPos> entries;
            readonly Dictionary<GridPos, int> routeWeight = new Dictionary<GridPos, int>();
            readonly HashSet<GridPos> solids = new HashSet<GridPos>(), traps = new HashSet<GridPos>();
            readonly List<(BuildingKind kind, GridPos anchor)> placed = new List<(BuildingKind, GridPos)>();

            /// <summary>Corridor: a solid is never planned where it would turn an open Day-1 route into a breach.</summary>
            public bool KeepOpen;

            public Board(RunSimulation sim)
            {
                this.sim = sim;
                keep = sim.Keep.Cell;
                solids.Add(keep);

                // Every night's entries, weighted by the nights that use them (later nights add sides).
                var uses = new Dictionary<GridPos, int>();
                foreach (var night in sim.Campaign.Nights)
                foreach (var e in night.EntryCells)
                    uses[e] = uses.TryGetValue(e, out var n) ? n + 1 : 1;
                entries = uses.Keys.OrderBy(e => e.Z).ThenBy(e => e.X).ToList();
                foreach (var e in entries)
                {
                    var route = sim.GetLiveRoute(e);
                    if (!route.Found) continue;
                    foreach (var c in route.Cells.Distinct())
                        routeWeight[c] = (routeWeight.TryGetValue(c, out var w) ? w : 0) + uses[e];
                }
            }

            public bool IsLand(GridPos c) => GridCoordinates.IsInBounds(c) && !sim.Grid.IsImpassableTerrain(c);

            public int Cheb(GridPos c) => Math.Max(Math.Abs(c.X - keep.X), Math.Abs(c.Z - keep.Z));

            public bool OnRoute(GridPos c) => routeWeight.ContainsKey(c);

            public int RouteWeight(GridPos c) => routeWeight.TryGetValue(c, out var w) ? w : 0;

            public bool NextToRoute(GridPos c) => !OnRoute(c) && GridPos.CardinalOrder.Any(d => OnRoute(c + d));

            public List<GridPos> Placed(BuildingKind kind) => placed.Where(p => p.kind == kind).Select(p => p.anchor).ToList();

            GridPos[] Footprint(BuildingKind kind, GridPos anchor) => sim.Definitions.For(kind).Footprint(anchor);

            /// <summary>Route cells (weighted by nights used) within <paramref name="range"/> of a 1 x 1 building centre.</summary>
            public int Coverage(GridPos c, double range)
            {
                var sum = 0;
                foreach (var kv in routeWeight)
                {
                    double dx = kv.Key.X - c.X, dz = kv.Key.Z - c.Z;
                    if (dx * dx + dz * dz <= range * range + 1e-9) sum += kv.Value;
                }

                return sum;
            }

            public bool Spaced(GridPos c, BuildingKind kind, int min) => Placed(kind).All(p => Math.Max(Math.Abs(p.X - c.X), Math.Abs(p.Z - c.Z)) >= min);

            public int AdjacentShooters(GridPos c) =>
                GridPos.CardinalOrder.Count(d => placed.Any(p => (p.kind == BuildingKind.Tower || p.kind == BuildingKind.Cannon) && Footprint(p.kind, p.anchor).Contains(c + d)));

            /// <summary>Planned solid buildings with a footprint cell within Chebyshev <paramref name="reach"/> of the 2 x 2 at <paramref name="anchor"/>.</summary>
            public int NearBuildings(GridPos anchor, int reach) => NearKinds(anchor, reach, k => k != BuildingKind.SpikeTrap);

            public int NearEconomy(GridPos anchor, int reach) =>
                NearKinds(anchor, reach, k => k == BuildingKind.House || k == BuildingKind.Farm || k == BuildingKind.Mine);

            int NearKinds(GridPos anchor, int reach, Func<BuildingKind, bool> kinds) =>
                placed.Count(p => kinds(p.kind) && Footprint(p.kind, p.anchor).Any(f =>
                    Math.Abs(f.X - anchor.X - 0.5) <= reach + 0.5 && Math.Abs(f.Z - anchor.Z - 0.5) <= reach + 0.5));

            public int FreeEdgeNeighbours(BuildingKind kind, GridPos anchor, Func<GridPos, bool> counts)
            {
                var cells = Footprint(kind, anchor);
                var seen = new HashSet<GridPos>();
                foreach (var c in cells)
                foreach (var d in GridPos.CardinalOrder)
                {
                    var n = c + d;
                    if (cells.Contains(n) || solids.Contains(n) || traps.Contains(n) || !counts(n)) continue;
                    seen.Add(n);
                }

                return seen.Count;
            }

            public bool BesideRoute(BuildingKind kind, GridPos anchor) =>
                Footprint(kind, anchor).Any(c => GridPos.CardinalOrder.Any(d => OnRoute(c + d)));

            bool Fits(BuildingKind kind, GridPos anchor)
            {
                foreach (var c in Footprint(kind, anchor))
                    if (!IsLand(c) || GridCoordinates.IsReservedRing(c) || solids.Contains(c) || traps.Contains(c)) return false;
                if (kind == BuildingKind.SpikeTrap || !KeepOpen) return true;
                foreach (var c in Footprint(kind, anchor))
                    if (OnRoute(c)) return false;
                if (openEntries == null)
                {
                    var now = solids.Where(s => s != keep).ToList();
                    openEntries = entries.Where(e => { var r = sim.Planner.Plan(sim.Grid, e, now); return r.Found && !r.IsBreach; }).ToList();
                }

                var preview = solids.Where(s => s != keep).ToList();
                preview.AddRange(Footprint(kind, anchor));
                foreach (var e in openEntries)
                {
                    var after = sim.Planner.Plan(sim.Grid, e, preview);
                    if (!after.Found || after.IsBreach) return false;
                }

                return true;
            }

            /// <summary>Entries with an open route under the claimed solids; recomputed after each claim.</summary>
            List<GridPos> openEntries;

            void Claim(List<string> items, BuildingKind kind, GridPos anchor)
            {
                foreach (var c in Footprint(kind, anchor)) (kind == BuildingKind.SpikeTrap ? traps : solids).Add(c);
                placed.Add((kind, anchor));
                openEntries = null;
                items.Add(Item(kind, anchor));
            }

            /// <summary>
            /// Claims the cell at the Keep offset, or, when it is not buildable, the nearest free one on the same Chebyshev ring
            /// (squared distance to the wanted cell, then (z, x)). Skipped when the ring has none.
            /// </summary>
            public void Ring(List<string> items, BuildingKind kind, (int dx, int dz) offset)
            {
                var want = new GridPos(keep.X + offset.dx, keep.Z + offset.dz);
                var ring = Math.Max(Math.Abs(offset.dx), Math.Abs(offset.dz));
                var best = Cells().Where(c => Cheb(c) == ring && Fits(kind, c))
                    .OrderBy(c => (c.X - want.X) * (c.X - want.X) + (c.Z - want.Z) * (c.Z - want.Z)).ThenBy(c => c.Z).ThenBy(c => c.X)
                    .Select(c => (GridPos?)c).FirstOrDefault();
                if (best != null) Claim(items, kind, best.Value);
            }

            /// <summary>Offsets ordered by the route weight on their side of the Keep (busiest first), ties in the given order.</summary>
            public IEnumerable<(int, int)> ByApproach(IEnumerable<(int dx, int dz)> offsets) =>
                offsets.Select((o, i) => (o, i)).OrderByDescending(p => routeWeight.Where(kv =>
                        Math.Sign(kv.Key.X - keep.X) == Math.Sign(p.o.dx) && Math.Sign(kv.Key.Z - keep.Z) == Math.Sign(p.o.dz)).Sum(kv => kv.Value))
                    .ThenBy(p => p.i).Select(p => p.o);

            /// <summary>Claims the highest-scoring fitting anchor that passes <paramref name="where"/>; ties by (z, x). Skipped when none.</summary>
            public void Best(List<string> items, BuildingKind kind, Func<GridPos, bool> where, Func<GridPos, int> score)
            {
                GridPos? best = null;
                var bestScore = int.MinValue;
                foreach (var c in Cells())
                {
                    if (!where(c) || !Fits(kind, c)) continue;
                    var s = score(c);
                    if (s <= bestScore) continue;
                    bestScore = s;
                    best = c;
                }

                if (best != null) Claim(items, kind, best.Value);
            }

            static IEnumerable<GridPos> Cells()
            {
                for (var z = 0; z < GridCoordinates.Size; z++)
                for (var x = 0; x < GridCoordinates.Size; x++)
                    yield return new GridPos(x, z);
            }
        }
    }
}
