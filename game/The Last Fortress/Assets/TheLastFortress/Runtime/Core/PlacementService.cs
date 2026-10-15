namespace TheLastFortress
{
    public enum PlacementRejection
    {
        None = 0,
        OutOfBounds,
        ReservedRing,
        Occupied,
        ImpassableTerrain,
        InsufficientGold,
        NotDay,
        NotPurchasable,
    }

    public enum SellRejection
    {
        None = 0,
        NotDay,
        NothingSelected,
        KeepCannotBeSold,
        AlreadyRemoved,
    }

    public static class PlacementText
    {
        public static string Describe(PlacementRejection r)
        {
            switch (r)
            {
                case PlacementRejection.None: return "Valid";
                case PlacementRejection.OutOfBounds: return "Outside the map";
                case PlacementRejection.ReservedRing: return "Edge reserved for enemy entry";
                case PlacementRejection.Occupied: return "Tile occupied";
                case PlacementRejection.ImpassableTerrain: return "Impassable terrain";
                case PlacementRejection.InsufficientGold: return "Not enough gold";
                case PlacementRejection.NotDay: return "Build only during Day";
                case PlacementRejection.NotPurchasable: return "Cannot be built";
                default: return r.ToString();
            }
        }
    }

    /// <summary>Atomic purchase/sale. Owns the gold balance; never mutates anything on rejection.</summary>
    public sealed class PlacementService
    {
        readonly GridState grid;
        readonly PrototypeDefinitions definitions;

        public int Gold { get; private set; }

        public PlacementService(GridState grid, PrototypeDefinitions definitions, int startingGold)
        {
            this.grid = grid;
            this.definitions = definitions;
            Gold = startingGold;
        }

        /// <summary>Validation shared by ghost preview and actual placement (phase checked by the caller).</summary>
        public PlacementRejection Check(BuildingKind kind, GridPos cell)
        {
            var def = definitions.For(kind);
            if (!def.Purchasable) return PlacementRejection.NotPurchasable;
            if (!GridCoordinates.IsInBounds(cell)) return PlacementRejection.OutOfBounds;
            if (GridCoordinates.IsReservedRing(cell)) return PlacementRejection.ReservedRing;
            if (grid.IsImpassableTerrain(cell)) return PlacementRejection.ImpassableTerrain;
            if (grid.IsSolid(cell)) return PlacementRejection.Occupied;
            if (Gold < def.Cost) return PlacementRejection.InsufficientGold;
            return PlacementRejection.None;
        }

        public PlacementRejection TryPlace(BuildingKind kind, GridPos cell, int buildingId, out Building building)
        {
            building = null;
            var rejection = Check(kind, cell);
            if (rejection != PlacementRejection.None) return rejection;
            var def = definitions.For(kind);
            building = new Building(buildingId, def, cell, def.Cost);
            grid.Install(building);
            Gold -= def.Cost;
            return PlacementRejection.None;
        }

        public static int RefundFor(Building building) => building.PurchasePrice / 2; // floor for non-negative prices

        public SellRejection TrySell(Building building, out int refund)
        {
            refund = 0;
            if (building == null) return SellRejection.NothingSelected;
            if (!building.IsDestructible) return SellRejection.KeepCannotBeSold;
            if (!grid.Release(building)) return SellRejection.AlreadyRemoved;
            refund = RefundFor(building);
            Gold += refund;
            return SellRejection.None;
        }
    }
}
