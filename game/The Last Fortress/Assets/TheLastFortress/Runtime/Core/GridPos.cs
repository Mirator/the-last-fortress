using System;

namespace TheLastFortress
{
    /// <summary>Integer logical grid cell. X runs east, Z runs north.</summary>
    [Serializable]
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Z;

        public GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static readonly GridPos North = new GridPos(0, 1);
        public static readonly GridPos East = new GridPos(1, 0);
        public static readonly GridPos South = new GridPos(0, -1);
        public static readonly GridPos West = new GridPos(-1, 0);

        /// <summary>Neighbor visit order mandated by specification 002: N, E, S, W.</summary>
        public static readonly GridPos[] CardinalOrder = { North, East, South, West };

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Z + b.Z);

        public int ManhattanDistance(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

        public bool IsCardinalNeighbor(GridPos other) => ManhattanDistance(other) == 1;

        public bool Equals(GridPos other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
        public override string ToString() => $"({X},{Z})";
    }

    /// <summary>World/grid conversions. One tile equals one world unit; the grid origin is world (0,0).</summary>
    public static class GridCoordinates
    {
        public const int Size = 20;

        public static bool IsInBounds(GridPos cell) =>
            cell.X >= 0 && cell.X < Size && cell.Z >= 0 && cell.Z < Size;

        /// <summary>The outermost ring is reserved for enemy entry and never buildable.</summary>
        public static bool IsReservedRing(GridPos cell) =>
            IsInBounds(cell) && (cell.X == 0 || cell.Z == 0 || cell.X == Size - 1 || cell.Z == Size - 1);

        public static void TileCenter(GridPos cell, out double x, out double z)
        {
            x = cell.X + 0.5;
            z = cell.Z + 0.5;
        }

        /// <summary>Floors world X/Z. Points outside the grid are rejected, never clamped.</summary>
        public static bool TryWorldToGrid(double worldX, double worldZ, out GridPos cell)
        {
            var x = (int)Math.Floor(worldX);
            var z = (int)Math.Floor(worldZ);
            cell = new GridPos(x, z);
            if (double.IsNaN(worldX) || double.IsNaN(worldZ) || !IsInBounds(cell))
            {
                cell = default;
                return false;
            }

            return true;
        }
    }
}
