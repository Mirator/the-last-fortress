namespace TheLastFortress
{
    public enum WallShape
    {
        Isolated = 0,
        End = 1,
        Straight = 2,
        Corner = 3,
        T = 4,
        Cross = 5,
    }

    /// <summary>
    /// Cosmetic Wall auto-connection from authoritative occupancy. Mask bits N=1, E=2, S=4, W=8 (cardinal neighboring
    /// Walls only). Canonical masks: 0 isolated, 1 end-N, 5 straight N/S, 3 corner N/E, 7 T N/E/S, 15 cross; a variant is
    /// the canonical mask rotated clockwise k times by ((m &lt;&lt; 1) | (m &gt;&gt; 3)) &amp; 15, drawn with yaw k * 90 degrees.
    /// Never affects footprint, routing, combat or identity.
    /// </summary>
    public static class WallTopology
    {
        public const int North = 1, East = 2, South = 4, West = 8;

        static readonly GridPos[] Directions = { GridPos.North, GridPos.East, GridPos.South, GridPos.West };
        static readonly int[] Bits = { North, East, South, West };

        public static int RotateClockwise(int mask) => ((mask << 1) | (mask >> 3)) & 15;

        public static int CanonicalMask(WallShape shape)
        {
            switch (shape)
            {
                case WallShape.End: return North;
                case WallShape.Straight: return North | South;
                case WallShape.Corner: return North | East;
                case WallShape.T: return North | East | South;
                case WallShape.Cross: return 15;
                default: return 0;
            }
        }

        /// <summary>Shape and clockwise quarter turns (0..3) that reproduce <paramref name="mask"/> from its canonical form.</summary>
        public static void Classify(int mask, out WallShape shape, out int quarterTurns)
        {
            mask &= 15;
            var bits = 0;
            for (var m = mask; m != 0; m >>= 1) bits += m & 1;
            if (bits == 0) shape = WallShape.Isolated;
            else if (bits == 1) shape = WallShape.End;
            else if (bits == 3) shape = WallShape.T;
            else if (bits == 4) shape = WallShape.Cross;
            else shape = mask == 5 || mask == 10 ? WallShape.Straight : WallShape.Corner;

            var c = CanonicalMask(shape);
            for (quarterTurns = 0; quarterTurns < 4; quarterTurns++)
            {
                if (c == mask) return;
                c = RotateClockwise(c);
            }

            throw new System.InvalidOperationException($"mask {mask} not reachable from {shape}");
        }

        static bool IsWall(GridState grid, GridPos cell)
        {
            var b = grid.GetOccupant(cell);
            return b != null && b.Kind == BuildingKind.Wall && b.IsAlive;
        }

        /// <summary>Connection mask of a Wall cell; <paramref name="hypotheticalWall"/> counts as a Wall (construction ghost).</summary>
        public static int Mask(GridState grid, GridPos cell, GridPos? hypotheticalWall = null)
        {
            var mask = 0;
            for (var i = 0; i < 4; i++)
            {
                var n = cell + Directions[i];
                if (IsWall(grid, n) || (hypotheticalWall.HasValue && hypotheticalWall.Value == n)) mask |= Bits[i];
            }

            return mask;
        }
    }
}
