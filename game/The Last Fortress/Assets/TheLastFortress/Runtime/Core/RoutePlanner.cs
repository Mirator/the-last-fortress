using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>Planned path from a start cell. Immutable once returned.</summary>
    public sealed class Route
    {
        public readonly bool Found;
        public readonly bool IsBreach;
        public readonly IReadOnlyList<GridPos> Cells;
        public readonly int Cost;
        public readonly int Revision;

        /// <summary>Index in <see cref="Cells"/> of the first solid structure on a breach route; -1 for open routes.</summary>
        public readonly int BlockerIndex;

        public Route(bool found, bool isBreach, IReadOnlyList<GridPos> cells, int cost, int revision, int blockerIndex)
        {
            Found = found;
            IsBreach = isBreach;
            Cells = cells;
            Cost = cost;
            Revision = revision;
            BlockerIndex = blockerIndex;
        }

        public GridPos Start => Cells[0];
        public GridPos Goal => Cells[Cells.Count - 1];
        public GridPos Blocker => Cells[BlockerIndex];

        /// <summary>Last cell the enemy actually walks to: the Keep neighbor, or the cell before the blocker.</summary>
        public int ReachableEndIndex => IsBreach ? BlockerIndex - 1 : Cells.Count - 1;

        public static Route NotFound(GridPos start, int revision) =>
            new Route(false, false, new[] { start }, -1, revision, -1);
    }

    /// <summary>
    /// Open-route-first planner from specification 002. Any open route beats breaching regardless of length.
    /// Both phases use one uniform-cost search with the spec's deterministic ordering:
    /// frontier by (cost, z, x, insertion), neighbors N/E/S/W, first predecessor kept on equal cost.
    /// </summary>
    public sealed class RoutePlanner
    {
        public const int LandCost = 1;
        public const int BreachCost = 6;

        const int CellCount = GridCoordinates.Size * GridCoordinates.Size;

        readonly Dictionary<GridPos, Route> cache = new Dictionary<GridPos, Route>();
        int cacheRevision = -1;

        // Search scratch buffers reused across queries.
        readonly int[] costs = new int[CellCount];
        readonly int[] predecessors = new int[CellCount];
        readonly bool[] settled = new bool[CellCount];
        readonly int[] toGoal = new int[CellCount];
        readonly Queue<int> bfs = new Queue<int>();
        readonly Frontier frontier = new Frontier();

        /// <summary>
        /// release-rules-4: an open route keeps its length and goal but, among equally short routes, goes straight from the
        /// start and turns as late as possible (it first heads along the axis with the larger distance to the goal), instead
        /// of the search's first-predecessor shape, which often stepped sideways at the entry. Breach routes are unchanged.
        /// </summary>
        public bool TurnLate { get; set; }

        public int SearchCount { get; private set; }

        /// <summary>Plans against live occupancy using a per-revision cache. Never call with a preview overlay.</summary>
        public Route GetLiveRoute(GridState grid, GridPos start)
        {
            if (cacheRevision != grid.Revision)
            {
                cache.Clear();
                cacheRevision = grid.Revision;
            }

            if (cache.TryGetValue(start, out var route)) return route;
            route = Plan(grid, start, null);
            cache[start] = route;
            return route;
        }

        /// <summary>Peek at a cached live route without planning (null when absent or stale).</summary>
        public Route PeekCachedRoute(GridState grid, GridPos start) =>
            cacheRevision == grid.Revision && cache.TryGetValue(start, out var r) ? r : null;

        public void ClearCache()
        {
            cache.Clear();
            cacheRevision = -1;
        }

        /// <summary>
        /// Uncached plan. <paramref name="previewSolids"/> are treated as extra destructible structures
        /// (construction ghosts) without touching live occupancy, revision, or cache.
        /// </summary>
        public Route Plan(GridState grid, GridPos start, ICollection<GridPos> previewSolids)
        {
            var open = Search(grid, start, previewSolids, breach: false);
            if (open != null) return open;
            var breach = Search(grid, start, previewSolids, breach: true);
            return breach ?? Route.NotFound(start, grid.Revision);
        }

        static int Index(GridPos c) => c.Z * GridCoordinates.Size + c.X;
        static GridPos FromIndex(int i) => new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);

        static bool IsSolid(GridState grid, GridPos cell, ICollection<GridPos> preview) =>
            grid.IsSolid(cell) || (preview != null && preview.Contains(cell));

        static bool IsKeepNeighbor(GridPos cell) => cell.IsCardinalNeighbor(GridState.KeepCell);

        /// <summary>Cost to enter a cell, or -1 when the cell can never be traversed in this phase.</summary>
        static int EnterCost(GridState grid, GridPos cell, ICollection<GridPos> preview, bool breach)
        {
            if (!GridCoordinates.IsInBounds(cell) || grid.IsImpassableTerrain(cell)) return -1;
            if (cell == GridState.KeepCell) return -1;
            if (!IsSolid(grid, cell, preview)) return LandCost;
            if (!breach) return -1;
            var occupant = grid.GetOccupant(cell);
            if (occupant != null && !occupant.IsDestructible) return -1;
            return BreachCost;
        }

        Route Search(GridState grid, GridPos start, ICollection<GridPos> preview, bool breach)
        {
            SearchCount++;
            if (!GridCoordinates.IsInBounds(start)) return null;
            for (var i = 0; i < CellCount; i++)
            {
                costs[i] = int.MaxValue;
                predecessors[i] = -1;
                settled[i] = false;
            }

            frontier.Clear();
            var startIndex = Index(start);
            costs[startIndex] = 0;
            frontier.Push(0, start, startIndex);

            while (frontier.TryPop(out var nodeCost, out var nodeIndex))
            {
                if (settled[nodeIndex] || nodeCost != costs[nodeIndex]) continue;
                settled[nodeIndex] = true;
                var cell = FromIndex(nodeIndex);

                var isGoal = IsKeepNeighbor(cell) && (breach || !IsSolid(grid, cell, preview));
                if (isGoal) return !breach && TurnLate ? BuildStraightRoute(grid, preview, start, nodeIndex, nodeCost) : BuildRoute(grid, preview, nodeIndex, nodeCost, breach);

                foreach (var dir in GridPos.CardinalOrder)
                {
                    var next = cell + dir;
                    var step = EnterCost(grid, next, preview, breach);
                    if (step < 0) continue;
                    var nextIndex = Index(next);
                    var nextCost = nodeCost + step;
                    if (nextCost >= costs[nextIndex]) continue; // keep first predecessor on equal cost
                    costs[nextIndex] = nextCost;
                    predecessors[nextIndex] = nodeIndex;
                    frontier.Push(nextCost, next, nextIndex);
                }
            }

            return null;
        }

        Route BuildRoute(GridState grid, ICollection<GridPos> preview, int goalIndex, int cost, bool breach)
        {
            var cells = new List<GridPos>();
            for (var i = goalIndex; i >= 0; i = predecessors[i]) cells.Add(FromIndex(i));
            cells.Reverse();
            var blocker = -1;
            if (breach)
            {
                for (var i = 0; i < cells.Count; i++)
                {
                    if (!IsSolid(grid, cells[i], preview)) continue;
                    blocker = i;
                    break;
                }

                // A breach search that found no solid cell would have been an open route.
                if (blocker <= 0) throw new InvalidOperationException("Breach route without a blocker.");
            }

            return new Route(true, breach, cells.AsReadOnly(), cost, grid.Revision, blocker);
        }

        /// <summary>
        /// Turn-late open route to the goal the search chose, with the same length: walk from the start choosing at each cell
        /// a neighbour one step closer to the goal, preferring the current direction, then the axis with more distance left,
        /// then N, E, S, W.
        /// </summary>
        Route BuildStraightRoute(GridState grid, ICollection<GridPos> preview, GridPos start, int goalIndex, int cost)
        {
            for (var i = 0; i < CellCount; i++) toGoal[i] = int.MaxValue;
            bfs.Clear();
            toGoal[goalIndex] = 0;
            bfs.Enqueue(goalIndex);
            var startIndex = Index(start);
            while (bfs.Count > 0)
            {
                var i = bfs.Dequeue();
                if (i == startIndex) break;
                var cell = FromIndex(i);
                foreach (var dir in GridPos.CardinalOrder)
                {
                    var next = cell + dir;
                    if (!GridCoordinates.IsInBounds(next)) continue;
                    var ni = Index(next);
                    if (toGoal[ni] != int.MaxValue) continue;
                    if (ni != startIndex && EnterCost(grid, next, preview, false) < 0) continue;
                    toGoal[ni] = toGoal[i] + 1;
                    bfs.Enqueue(ni);
                }
            }

            if (toGoal[startIndex] != cost) return BuildRoute(grid, preview, goalIndex, cost, false); // never expected
            var goal = FromIndex(goalIndex);
            var cells = new List<GridPos> { start };
            var at = start;
            var heading = default(GridPos);
            var hasHeading = false;
            while (at != goal)
            {
                var best = default(GridPos);
                var bestRank = int.MaxValue;
                for (var k = 0; k < GridPos.CardinalOrder.Length; k++)
                {
                    var dir = GridPos.CardinalOrder[k];
                    var next = at + dir;
                    if (!GridCoordinates.IsInBounds(next) || toGoal[Index(next)] != toGoal[Index(at)] - 1) continue;
                    var major = Math.Abs(goal.X - at.X) >= Math.Abs(goal.Z - at.Z) ? dir.X != 0 : dir.Z != 0;
                    var rank = (hasHeading && dir == heading ? 0 : major ? 1 : 2) * 10 + k;
                    if (rank >= bestRank) continue;
                    bestRank = rank;
                    best = dir;
                }

                heading = best;
                hasHeading = true;
                at += best;
                cells.Add(at);
            }

            return new Route(true, false, cells.AsReadOnly(), cost, grid.Revision, -1);
        }

        /// <summary>Binary heap ordered by (cost, z, x, insertion sequence).</summary>
        sealed class Frontier
        {
            struct Node
            {
                public int Cost, Z, X, Index;
                public long Seq;
            }

            readonly List<Node> heap = new List<Node>(256);
            long sequence;

            public void Clear()
            {
                heap.Clear();
                sequence = 0;
            }

            static bool Less(in Node a, in Node b)
            {
                if (a.Cost != b.Cost) return a.Cost < b.Cost;
                if (a.Z != b.Z) return a.Z < b.Z;
                if (a.X != b.X) return a.X < b.X;
                return a.Seq < b.Seq;
            }

            public void Push(int cost, GridPos cell, int index)
            {
                heap.Add(new Node { Cost = cost, Z = cell.Z, X = cell.X, Index = index, Seq = sequence++ });
                var i = heap.Count - 1;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (!Less(heap[i], heap[parent])) break;
                    (heap[i], heap[parent]) = (heap[parent], heap[i]);
                    i = parent;
                }
            }

            public bool TryPop(out int cost, out int index)
            {
                if (heap.Count == 0)
                {
                    cost = 0;
                    index = -1;
                    return false;
                }

                var top = heap[0];
                var last = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                if (heap.Count > 0)
                {
                    heap[0] = last;
                    var i = 0;
                    while (true)
                    {
                        var l = i * 2 + 1;
                        var r = l + 1;
                        var smallest = i;
                        if (l < heap.Count && Less(heap[l], heap[smallest])) smallest = l;
                        if (r < heap.Count && Less(heap[r], heap[smallest])) smallest = r;
                        if (smallest == i) break;
                        (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                        i = smallest;
                    }
                }

                cost = top.Cost;
                index = top.Index;
                return true;
            }
        }
    }
}
