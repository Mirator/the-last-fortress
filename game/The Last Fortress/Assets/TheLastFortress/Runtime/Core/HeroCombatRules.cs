using System;

namespace TheLastFortress
{
    /// <summary>
    /// release-rules-4 melee geometry (specs/003 section 4): whether a strike from the hero's centre to an enemy's centre
    /// is stopped by a solid building or impassable terrain. Integer and exact; no Unity physics.
    /// </summary>
    public static class HeroCombatRules
    {
        /// <summary>Whether a cell is blocked for a strike.</summary>
        public delegate bool CellBlocked(GridPos cell);

        /// <summary>
        /// True when the segment between two points (1/1200-tile units) touches no blocked cell. A cell counts as touched
        /// when the closed segment meets its closed square, so grazing a corner blocks. The cells holding the endpoints are
        /// walkable for any legal hero or enemy, so only true obstructions between them stop the strike.
        /// </summary>
        public static bool StrikeClear(long x0, long z0, long x1, long z1, CellBlocked blocked)
        {
            const int u = HeroRules.UnitsPerTile;
            var cx0 = FloorDiv(Math.Min(x0, x1), u);
            var cx1 = FloorDiv(Math.Max(x0, x1), u);
            var cz0 = FloorDiv(Math.Min(z0, z1), u);
            var cz1 = FloorDiv(Math.Max(z0, z1), u);
            // A point on a grid line also touches the cell below it.
            if (Math.Min(x0, x1) % u == 0) cx0--;
            if (Math.Min(z0, z1) % u == 0) cz0--;
            // The cells holding the endpoints never block: under release-rules-4 the hero may stand in the margin of a
            // building's cell, beside its drawn model.
            var startCell = new GridPos((int)FloorDiv(x0, u), (int)FloorDiv(z0, u));
            var endCell = new GridPos((int)FloorDiv(x1, u), (int)FloorDiv(z1, u));
            for (var z = cz0; z <= cz1; z++)
            for (var x = cx0; x <= cx1; x++)
            {
                var cell = new GridPos((int)x, (int)z);
                if (cell == startCell || cell == endCell) continue;
                if (blocked(cell) && SegmentTouchesSquare(x0, z0, x1, z1, x * u, z * u, u)) return false;
            }

            return true;
        }

        /// <summary>Closed segment against the closed square [sx, sx + size] x [sz, sz + size], exact in integers.</summary>
        public static bool SegmentTouchesSquare(long x0, long z0, long x1, long z1, long sx, long sz, long size)
        {
            if (Math.Max(x0, x1) < sx || Math.Min(x0, x1) > sx + size) return false;
            if (Math.Max(z0, z1) < sz || Math.Min(z0, z1) > sz + size) return false;
            // Separating axis along the segment's normal: every corner strictly on one side means no contact.
            long dx = x1 - x0, dz = z1 - z0;
            int pos = 0, neg = 0;
            for (var i = 0; i < 4; i++)
            {
                var cx = sx + (i & 1) * size;
                var cz = sz + (i >> 1) * size;
                var cross = dx * (cz - z0) - dz * (cx - x0);
                if (cross > 0) pos++;
                else if (cross < 0) neg++;
                else return true;
            }

            return pos > 0 && neg > 0;
        }

        static long FloorDiv(long a, long b) => a >= 0 ? a / b : (a - b + 1) / b;
    }
}
