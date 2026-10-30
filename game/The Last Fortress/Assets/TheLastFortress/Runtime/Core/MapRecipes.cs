using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Spec 017 D8 map recipes (release-rules-4 new runs): each recipe paints its land features in turn, then the Keep (anywhere
    /// in the central 8 x 8), 3-5 entries on the boundary ring, a choke if no route has one, gold veins and fertile meadows.
    /// Every candidate is validated on the board as played with the real route planner and the v0.4 fairness values; up to
    /// <see cref="MaxAttempts"/> attempts from <c>RunRandom.ForMapAttempt(seed ^ RecipeSalt, attempt)</c>, then a plain
    /// fallback (centre Keep, four side entries, meadows) flagged as such. Deterministic per recipe and seed.
    /// </summary>
    public static class MapRecipes
    {
        public const string RiverValley = "river-valley";
        public const string Highlands = "highlands";
        public const string ForestEdge = "forest-edge";
        public const string OpenPlains = "open-plains";

        public static readonly string[] All = { RiverValley, Highlands, ForestEdge, OpenPlains };

        public static string Title(string id)
        {
            switch (id)
            {
                case RiverValley: return "River Valley";
                case Highlands: return "Highlands";
                case ForestEdge: return "Forest Edge";
                case OpenPlains: return "Open Plains";
                default: return id;
            }
        }

        public static string Summary(string id)
        {
            switch (id)
            {
                case RiverValley: return "A river splits the land; fords are the crossings and the meadows lie along the banks.";
                case Highlands: return "Rock ridges make chokepoints; small meadows sit between them.";
                case ForestEdge: return "A forest belt closes one side; the meadows lie across the open far side.";
                case OpenPlains: return "Wide meadows and few obstacles: a rich economy, few natural chokepoints.";
                default: return "";
            }
        }

        public static bool IsRecipe(string id) => Array.IndexOf(All, id) >= 0;

        public const uint RecipeSalt = 0x7FEB352D;
        public const uint PickSalt = 0x846CA68B;
        public const int MaxAttempts = 40;

        /// <summary>v0.4 fairness values [P].</summary>
        public const int MinFertile = 50, MaxFertile = 110, MinRouteSteps = 9, MaxRouteSteps = 22, ChokeWidth = 2, OpenSide = 3, ClearSides = 2, ClearReach = 3;

        public const int KeepAreaMin = 6, KeepAreaMax = 13, MinEntries = 3, MaxEntries = 5, EntrySpacing = 4;

        public static string ForSeed(uint seed) => All[(int)new RunRandom(seed ^ PickSalt).NextBounded((uint)All.Length)];

        public static TerrainMap Generate(string id, uint seed)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var map = Candidate(id, seed, attempt);
                if (map != null && Validate(map) == null) return map;
            }

            return Fallback(id, seed);
        }

        /// <summary>The plain board used when every attempt fails: centre Keep, the four side midpoints, meadows. Not re-validated.</summary>
        public static TerrainMap Fallback(string id, uint seed)
        {
            var map = new TerrainMap { Seed = seed, Attempt = -1, Profile = id, Fallback = true, Keep = GridState.KeepCell };
            map.Entries = new List<GridPos> { MvpCampaign.West, MvpCampaign.East, MvpCampaign.North, MvpCampaign.South };
            MapProfiles.PlaceVeins(map);
            MapProfiles.PlaceFertile(map);
            return map;
        }

        /// <summary>One attempt: features, Keep, entries, a choke when needed, veins, meadows. Null when entries cannot be placed.</summary>
        public static TerrainMap Candidate(string id, uint seed, int attempt)
        {
            var rng = RunRandom.ForMapAttempt(seed ^ RecipeSalt, attempt);
            var map = new TerrainMap { Seed = seed, Attempt = attempt, Profile = id, Fertile = new HashSet<GridPos>() };
            map.Keep = new GridPos(KeepAreaMin + (int)rng.NextBounded(KeepAreaMax - KeepAreaMin + 1), KeepAreaMin + (int)rng.NextBounded(KeepAreaMax - KeepAreaMin + 1));
            var meadows = new List<(GridPos centre, int radius)>();
            switch (id)
            {
                case RiverValley: PaintRiver(map, rng, meadows); break;
                case Highlands: PaintRidges(map, rng, meadows); break;
                case ForestEdge: PaintForestBelt(map, rng, meadows); break;
                default: PaintPlains(map, rng, meadows); break;
            }

            if (!PlaceEntries(map, rng)) return null;
            if (!HasChoke(map)) Pinch(map, rng);
            map.Fords.RemoveAll(f => map[f] != TerrainType.Land); // a later clump or pinch may have covered one
            MapProfiles.PlaceVeins(map);
            foreach (var (centre, radius) in meadows) Meadow(map, centre, radius);
            return map;
        }

        // ---------- features ----------

        static int Cheb(GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

        static int Manhattan(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

        static int Next(RunRandom rng, int lo, int hi) => lo + (int)rng.NextBounded((uint)(hi - lo + 1));

        /// <summary>Interior cells (off the ring) at Chebyshev 2+ from the Keep: where features may go.</summary>
        static bool Paintable(TerrainMap map, GridPos c) =>
            GridCoordinates.IsInBounds(c) && !GridCoordinates.IsReservedRing(c) && Cheb(c, map.Keep) >= 2;

        static void Set(TerrainMap map, GridPos c, TerrainType t)
        {
            if (Paintable(map, c)) map.Cells[c.Z * GridCoordinates.Size + c.X] = t;
        }

        /// <summary>A cell at Chebyshev 3+ from the Keep, off the ring, drawn uniformly (with up to 20 tries for <paramref name="ok"/>).</summary>
        static GridPos Spot(TerrainMap map, RunRandom rng, Func<GridPos, bool> ok = null)
        {
            var c = default(GridPos);
            for (var i = 0; i < 20; i++)
            {
                c = new GridPos(Next(rng, 1, GridCoordinates.Size - 2), Next(rng, 1, GridCoordinates.Size - 2));
                if (Cheb(c, map.Keep) >= 3 && (ok == null || ok(c))) return c;
            }

            return c;
        }

        static void Clumps(TerrainMap map, RunRandom rng, int count, int size)
        {
            for (var i = 0; i < count; i++)
            {
                var c = Spot(map, rng);
                var type = rng.NextBounded(2) == 0 ? TerrainType.Forest : TerrainType.Rock;
                for (var k = 0; k < size; k++)
                {
                    Set(map, c, type);
                    c = c + GridPos.CardinalOrder[(int)rng.NextBounded(4)];
                }
            }
        }

        static void AddMeadows(TerrainMap map, RunRandom rng, List<(GridPos, int)> meadows, int count, int radius, Func<GridPos, bool> ok = null)
        {
            for (var i = 0; i < count; i++)
                meadows.Add((Spot(map, rng, c => meadows.TrueForAll(m => Cheb(m.Item1, c) >= 4) && (ok == null || ok(c))), radius));
        }

        /// <summary>River Valley: a meandering 1-wide river across the board, 2-3 fords two cells wide, meadows along its banks.</summary>
        static void PaintRiver(TerrainMap map, RunRandom rng, List<(GridPos, int)> meadows)
        {
            var vertical = rng.NextBounded(2) == 0;
            var keepA = vertical ? map.Keep.X : map.Keep.Z;
            var options = new List<int>();
            for (var a = 4; a <= 15; a++)
                if (Math.Abs(a - keepA) >= 2) options.Add(a);
            var p = options[(int)rng.NextBounded((uint)options.Count)];
            var line = new List<GridPos>();
            for (var t = 1; t < GridCoordinates.Size - 1; t++)
            {
                var step = (int)rng.NextBounded(5);
                var q = step == 0 ? p - 1 : step == 1 ? p + 1 : p;
                if (q >= 3 && q <= 16 && Math.Abs(q - keepA) >= 2) p = q;
                line.Add(vertical ? new GridPos(p, t) : new GridPos(t, p));
            }

            foreach (var c in line) Set(map, c, TerrainType.Water);
            var fords = Next(rng, 2, 3);
            var taken = new List<int>();
            for (var tries = 0; taken.Count < fords && tries < 30; tries++)
            {
                var t = Next(rng, 1, line.Count - 3);
                if (taken.Exists(o => Math.Abs(o - t) < 4)) continue;
                taken.Add(t);
                foreach (var c in new[] { line[t], line[t + 1] })
                {
                    map.Cells[c.Z * GridCoordinates.Size + c.X] = TerrainType.Land;
                    map.Fords.Add(c);
                }
            }

            // The river runs on under bridges on the ring at both ends.
            var max = GridCoordinates.Size - 1;
            foreach (var end in new[] { line[0], line[line.Count - 1] })
                map.Fords.Add(vertical ? new GridPos(end.X, end.Z == 1 ? 0 : max) : new GridPos(end.X == 1 ? 0 : max, end.Z));

            Clumps(map, rng, 3, 2);
            var river = new HashSet<GridPos>(line);
            AddMeadows(map, rng, meadows, 6, 2, c => map[c] == TerrainType.Land && NearAny(c, river, 2));
        }

        static bool NearAny(GridPos c, HashSet<GridPos> cells, int reach)
        {
            foreach (var o in cells)
                if (Cheb(o, c) <= reach) return true;
            return false;
        }

        /// <summary>Highlands: 2-3 rock ridges, 5-8 long with one jog, a few loose rocks, small meadows between.</summary>
        static void PaintRidges(TerrainMap map, RunRandom rng, List<(GridPos, int)> meadows)
        {
            var ridges = Next(rng, 2, 3);
            var rock = new HashSet<GridPos>();
            for (var r = 0; r < ridges; r++)
            {
                var c = Spot(map, rng);
                var along = GridPos.CardinalOrder[(int)rng.NextBounded(4)];
                var jog = new GridPos(along.Z, along.X);
                var length = Next(rng, 5, 8);
                var bend = Next(rng, 2, length - 2);
                for (var i = 0; i < length; i++)
                {
                    if (i == bend) c = c + jog;
                    Set(map, c, TerrainType.Rock);
                    if (map[c] == TerrainType.Rock) rock.Add(c);
                    c = c + along;
                    if (!GridCoordinates.IsInBounds(c)) break;
                }
            }

            Clumps(map, rng, 4, 1);
            AddMeadows(map, rng, meadows, 5, 2, c => map[c] == TerrainType.Land);
            AddMeadows(map, rng, meadows, 3, 1, c => map[c] == TerrainType.Land);
        }

        /// <summary>Forest Edge: a forest belt 3-4 deep along one side with 2-3 two-cell gaps, meadows on the far half.</summary>
        static void PaintForestBelt(TerrainMap map, RunRandom rng, List<(GridPos, int)> meadows)
        {
            var side = (int)rng.NextBounded(4); // 0 west, 1 east, 2 south, 3 north
            var depth = Next(rng, 3, 4);
            var max = GridCoordinates.Size - 1;
            GridPos At(int inward, int across) =>
                side == 0 ? new GridPos(inward, across) : side == 1 ? new GridPos(max - inward, across) : side == 2 ? new GridPos(across, inward) : new GridPos(across, max - inward);

            var gaps = new List<int>();
            var count = Next(rng, 2, 3);
            for (var tries = 0; gaps.Count < count && tries < 30; tries++)
            {
                var g = Next(rng, 2, max - 3);
                if (!gaps.Exists(o => Math.Abs(o - g) < 5)) gaps.Add(g);
            }

            for (var across = 1; across < max; across++)
            {
                if (gaps.Exists(g => across == g || across == g + 1)) continue;
                for (var inward = 1; inward <= depth; inward++) Set(map, At(inward, across), TerrainType.Forest);
            }

            Clumps(map, rng, 3, 2);
            AddMeadows(map, rng, meadows, 6, 2, c => map[c] == TerrainType.Land && (side == 0 ? c.X : side == 1 ? max - c.X : side == 2 ? c.Z : max - c.Z) >= 9);
        }

        /// <summary>Open Plains: five small forest or rock clumps and wide meadows.</summary>
        static void PaintPlains(TerrainMap map, RunRandom rng, List<(GridPos, int)> meadows)
        {
            Clumps(map, rng, 5, 3);
            AddMeadows(map, rng, meadows, 7, 2, c => map[c] == TerrainType.Land);
        }

        /// <summary>Fertile land within Manhattan <paramref name="radius"/>: never the ring, a vein or a Keep neighbour.</summary>
        static void Meadow(TerrainMap map, GridPos centre, int radius)
        {
            for (var dz = -radius; dz <= radius; dz++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var f = new GridPos(centre.X + dx, centre.Z + dz);
                if (Math.Abs(dx) + Math.Abs(dz) > radius || !GridCoordinates.IsInBounds(f) || GridCoordinates.IsReservedRing(f)) continue;
                if (map[f] != TerrainType.Land || map.IsVein(f) || Cheb(f, map.Keep) <= 1) continue;
                map.Fertile.Add(f);
            }
        }

        // ---------- entries and the choke ----------

        /// <summary>3-5 entries on the ring (no corners), Manhattan 10-21 from the Keep, Chebyshev 4+ apart, inward cell land, on at least two sides; index order as drawn.</summary>
        static bool PlaceEntries(TerrainMap map, RunRandom rng)
        {
            var max = GridCoordinates.Size - 1;
            var ring = new List<GridPos>();
            for (var i = 1; i < max; i++)
                foreach (var c in new[] { new GridPos(0, i), new GridPos(max, i), new GridPos(i, 0), new GridPos(i, max) })
                {
                    var inward = new GridPos(c.X == 0 ? 1 : c.X == max ? max - 1 : c.X, c.Z == 0 ? 1 : c.Z == max ? max - 1 : c.Z);
                    var d = Manhattan(c, map.Keep);
                    if (d >= 10 && d <= 21 && map[inward] == TerrainType.Land) ring.Add(c);
                }

            var want = Next(rng, MinEntries, MaxEntries);
            map.Entries = new List<GridPos>();
            for (var i = 0; i < ring.Count && map.Entries.Count < want; i++)
            {
                var j = i + (int)rng.NextBounded((uint)(ring.Count - i));
                (ring[i], ring[j]) = (ring[j], ring[i]);
                var c = ring[i];
                if (map.Entries.TrueForAll(e => Cheb(e, c) >= EntrySpacing)) map.Entries.Add(c);
            }

            return map.Entries.Count >= MinEntries && Sides(map.Entries) >= 2;
        }

        /// <summary>How many board sides the entries use (a map whose every front comes from one side is redrawn).</summary>
        public static int Sides(IReadOnlyList<GridPos> entries)
        {
            var max = GridCoordinates.Size - 1;
            var sides = new HashSet<int>();
            foreach (var e in entries) sides.Add(e.X == 0 ? 0 : e.X == max ? 1 : e.Z == 0 ? 2 : 3);
            return sides.Count;
        }

        static Route OpenRoute(TerrainMap map, GridPos entry)
        {
            var grid = new GridState(map.ImpassableCells(), map.Keep);
            grid.Install(new Building(1, PrototypeDefinitions.CreateDefault().Keep, map.Keep, 0));
            return new RoutePlanner().Plan(grid, entry, null);
        }

        /// <summary>Land cells across the route at <paramref name="i"/> (the cell itself plus contiguous land on both sides, up to <paramref name="cap"/> each).</summary>
        static (int left, int right) Across(TerrainMap map, IReadOnlyList<GridPos> cells, int i, int cap)
        {
            var next = cells[Math.Min(i + 1, cells.Count - 1)];
            var prev = cells[Math.Max(i - 1, 0)];
            var dx = next.X - prev.X;
            var dz = next.Z - prev.Z;
            var p = Math.Abs(dx) >= Math.Abs(dz) ? new GridPos(0, 1) : new GridPos(1, 0);
            int Side(GridPos step)
            {
                var n = 0;
                var c = cells[i] + step;
                while (n < cap && GridCoordinates.IsInBounds(c) && map[c] == TerrainType.Land)
                {
                    n++;
                    c = c + step;
                }

                return n;
            }

            return (Side(p), Side(new GridPos(-p.X, -p.Z)));
        }

        static bool Interior(GridPos c) =>
            c.X >= 2 && c.Z >= 2 && c.X <= GridCoordinates.Size - 3 && c.Z <= GridCoordinates.Size - 3;

        static bool HasChoke(TerrainMap map)
        {
            foreach (var e in map.Entries)
            {
                var r = OpenRoute(map, e);
                if (!r.Found || r.IsBreach) continue;
                for (var i = 1; i < r.Cells.Count - 1; i++)
                {
                    if (!Interior(r.Cells[i])) continue;
                    var (a, b) = Across(map, r.Cells, i, ChokeWidth);
                    if (1 + a + b <= ChokeWidth) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Narrows one entry's route to two cells: short rock walls on both sides of a route cell at Chebyshev 3-7 from the
        /// Keep (three cells along the route, two cells apart across it), so the route still runs straight through the gap.
        /// </summary>
        static void Pinch(TerrainMap map, RunRandom rng)
        {
            var e = map.Entries[(int)rng.NextBounded((uint)map.Entries.Count)];
            var r = OpenRoute(map, e);
            if (!r.Found || r.IsBreach) return;
            var spots = new List<int>();
            for (var i = 2; i < r.Cells.Count - 2; i++)
                if (Interior(r.Cells[i]) && Cheb(r.Cells[i], map.Keep) >= 3 && Cheb(r.Cells[i], map.Keep) <= 7 &&
                    r.Cells[i - 1].X + r.Cells[i + 1].X == 2 * r.Cells[i].X && r.Cells[i - 1].Z + r.Cells[i + 1].Z == 2 * r.Cells[i].Z) spots.Add(i);
            if (spots.Count == 0) return;
            var k = spots[(int)rng.NextBounded((uint)spots.Count)];
            var c = r.Cells[k];
            var d = new GridPos(r.Cells[k + 1].X - c.X, r.Cells[k + 1].Z - c.Z);
            var p = new GridPos(d.Z, d.X);
            foreach (var along in new[] { new GridPos(0, 0), d, new GridPos(-d.X, -d.Z) })
            {
                Set(map, c + along + new GridPos(2 * p.X, 2 * p.Z), TerrainType.Rock);
                Set(map, c + along + new GridPos(-p.X, -p.Z), TerrainType.Rock);
            }
        }

        // ---------- validation ----------

        /// <summary>Null when the map meets every rule; otherwise the first failure.</summary>
        public static string Validate(TerrainMap map)
        {
            if (map.Entries == null || map.Entries.Count < MinEntries || map.Entries.Count > MaxEntries || Sides(map.Entries) < 2) return "entry count";
            if (map[map.Keep] != TerrainType.Land) return "Keep on terrain";
            if (map.Keep.X < KeepAreaMin || map.Keep.X > KeepAreaMax || map.Keep.Z < KeepAreaMin || map.Keep.Z > KeepAreaMax) return "Keep outside the central area";
            var open = false;
            foreach (var e in map.Entries)
            {
                if (!GridCoordinates.IsReservedRing(e) || map[e] != TerrainType.Land) return $"entry {e} off the ring";
                var r = OpenRoute(map, e);
                if (!r.Found || r.IsBreach) return $"entry {e} has no open route";
                var steps = r.Cells.Count - 1;
                if (steps < MinRouteSteps || steps > MaxRouteSteps) return $"entry {e} route {steps} steps";
                var wide = 0;
                var counted = 0;
                for (var i = 1; i < r.Cells.Count - 1; i++)
                {
                    if (!Interior(r.Cells[i])) continue;
                    counted++;
                    var (a, b) = Across(map, r.Cells, i, OpenSide);
                    if (a >= OpenSide && b >= OpenSide) wide++;
                }

                if (counted > 0 && wide * 10 >= counted * 6) open = true;
            }

            if (!HasChoke(map)) return "no choke";
            if (!open) return "no open approach";

            var clear = 0;
            foreach (var d in GridPos.CardinalOrder)
            {
                var ok = true;
                for (var i = 1; i <= ClearReach; i++)
                {
                    var c = map.Keep + new GridPos(d.X * i, d.Z * i);
                    if (!GridCoordinates.IsInBounds(c) || map[c] != TerrainType.Land) ok = false;
                }

                if (ok) clear++;
            }

            if (clear < ClearSides) return "Keep too enclosed";

            // No isolated walkable pocket: every land cell but the Keep is reachable from the first entry.
            var seen = new bool[GridCoordinates.Size * GridCoordinates.Size];
            var queue = new Queue<GridPos>();
            queue.Enqueue(map.Entries[0]);
            seen[map.Entries[0].Z * GridCoordinates.Size + map.Entries[0].X] = true;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in GridPos.CardinalOrder)
                {
                    var n = c + d;
                    if (!GridCoordinates.IsInBounds(n) || n == map.Keep || map[n] != TerrainType.Land || seen[n.Z * GridCoordinates.Size + n.X]) continue;
                    seen[n.Z * GridCoordinates.Size + n.X] = true;
                    queue.Enqueue(n);
                }
            }

            for (var i = 0; i < seen.Length; i++)
            {
                var c = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                if (map.Cells[i] == TerrainType.Land && c != map.Keep && !seen[i]) return $"isolated pocket at {c}";
            }

            var fertile = map.Fertile?.Count ?? 0;
            if (fertile < MinFertile || fertile > MaxFertile) return $"{fertile} fertile cells";
            return null;
        }
    }
}
