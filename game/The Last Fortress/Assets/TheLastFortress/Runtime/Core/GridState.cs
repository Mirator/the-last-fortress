using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Authoritative 20 x 20 occupancy. Solid structures may span several cells; every cell resolves to its owning
    /// building. Every solid install/release increments <see cref="Revision"/> exactly once. Spike Traps live on a
    /// separate walkable layer and never change <see cref="Revision"/>, so they do not invalidate route caches.
    /// </summary>
    public sealed class GridState
    {
        public static readonly GridPos KeepCell = new GridPos(10, 10);

        readonly Building[] occupants = new Building[GridCoordinates.Size * GridCoordinates.Size];
        readonly Building[] traps = new Building[GridCoordinates.Size * GridCoordinates.Size];
        readonly bool[] impassable = new bool[GridCoordinates.Size * GridCoordinates.Size];

        public int Revision { get; private set; }

        /// <summary>Changes on every trap install/release; separate so traps never invalidate route caches.</summary>
        public int TrapRevision { get; private set; }
        public Building Keep { get; private set; }

        /// <param name="impassableCells">Indestructible, unbuildable terrain (forest/rock/water or test maps).</param>
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

        /// <summary>Solid building owning the cell, or null. Traps are not occupants.</summary>
        public Building GetOccupant(GridPos cell) =>
            GridCoordinates.IsInBounds(cell) ? occupants[Index(cell)] : null;

        public Building GetTrap(GridPos cell) =>
            GridCoordinates.IsInBounds(cell) ? traps[Index(cell)] : null;

        public bool IsSolid(GridPos cell) => GetOccupant(cell) != null;

        public bool HasTrap(GridPos cell) => GetTrap(cell) != null;

        /// <summary>Whether any trap shares an edge with the cell.</summary>
        public bool HasAdjacentTrap(GridPos cell)
        {
            foreach (var d in GridPos.CardinalOrder)
                if (HasTrap(cell + d)) return true;
            return false;
        }

        public void Install(Building building)
        {
            foreach (var cell in building.Cells)
            {
                if (!GridCoordinates.IsInBounds(cell)) throw new InvalidOperationException($"{cell} out of bounds.");
                if (occupants[Index(cell)] != null || traps[Index(cell)] != null) throw new InvalidOperationException($"{cell} already occupied.");
                if (impassable[Index(cell)]) throw new InvalidOperationException($"{cell} is impassable terrain.");
            }

            if (!building.IsSolid)
            {
                traps[Index(building.Cell)] = building;
                TrapRevision++;
                return;
            }

            foreach (var cell in building.Cells) occupants[Index(cell)] = building;
            if (building.Kind == BuildingKind.Keep) Keep = building;
            Revision++;
        }

        /// <summary>Releases every cell once. Returns false when the building was already released.</summary>
        public bool Release(Building building)
        {
            if (building.Removed) return false;
            var layer = building.IsSolid ? occupants : traps;
            if (layer[Index(building.Cell)] != building) return false;
            foreach (var cell in building.Cells) layer[Index(cell)] = null;
            building.Removed = true;
            if (building.IsSolid) Revision++;
            else TrapRevision++;
            return true;
        }

        /// <summary>Each solid building once (at its anchor, in cell order), then each trap.</summary>
        public IEnumerable<Building> AllBuildings()
        {
            for (var i = 0; i < occupants.Length; i++)
            {
                var b = occupants[i];
                if (b != null && Index(b.Cell) == i) yield return b;
            }

            foreach (var t in traps)
                if (t != null) yield return t;
        }
    }
}
