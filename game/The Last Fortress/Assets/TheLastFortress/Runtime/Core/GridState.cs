using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Authoritative 20 x 20 occupancy. Every occupancy change increments <see cref="Revision"/> exactly once.
    /// </summary>
    public sealed class GridState
    {
        public static readonly GridPos KeepCell = new GridPos(10, 10);

        readonly Building[] occupants = new Building[GridCoordinates.Size * GridCoordinates.Size];
        readonly bool[] impassable = new bool[GridCoordinates.Size * GridCoordinates.Size];

        public int Revision { get; private set; }
        public Building Keep { get; private set; }

        /// <param name="impassableCells">Indestructible terrain; only used by test-only maps.</param>
        public GridState(IEnumerable<GridPos> impassableCells = null)
        {
            if (impassableCells == null) return;
            foreach (var cell in impassableCells)
            {
                if (!GridCoordinates.IsInBounds(cell)) throw new ArgumentException($"Terrain cell {cell} out of bounds.");
                if (cell == KeepCell) throw new ArgumentException("Terrain cannot cover the Keep cell.");
                impassable[Index(cell)] = true;
            }
        }

        static int Index(GridPos cell) => cell.Z * GridCoordinates.Size + cell.X;

        public bool IsImpassableTerrain(GridPos cell) => GridCoordinates.IsInBounds(cell) && impassable[Index(cell)];

        public Building GetOccupant(GridPos cell) =>
            GridCoordinates.IsInBounds(cell) ? occupants[Index(cell)] : null;

        public bool IsSolid(GridPos cell) => GetOccupant(cell) != null;

        public void Install(Building building)
        {
            if (!GridCoordinates.IsInBounds(building.Cell)) throw new InvalidOperationException($"{building.Cell} out of bounds.");
            if (occupants[Index(building.Cell)] != null) throw new InvalidOperationException($"{building.Cell} already occupied.");
            if (impassable[Index(building.Cell)]) throw new InvalidOperationException($"{building.Cell} is impassable terrain.");
            occupants[Index(building.Cell)] = building;
            if (building.Kind == BuildingKind.Keep) Keep = building;
            Revision++;
        }

        /// <summary>Releases occupancy once. Returns false when the building was already released.</summary>
        public bool Release(Building building)
        {
            if (building.Removed) return false;
            var index = Index(building.Cell);
            if (occupants[index] != building) return false;
            occupants[index] = null;
            building.Removed = true;
            Revision++;
            return true;
        }

        public IEnumerable<Building> AllBuildings()
        {
            foreach (var b in occupants)
                if (b != null) yield return b;
        }
    }
}
