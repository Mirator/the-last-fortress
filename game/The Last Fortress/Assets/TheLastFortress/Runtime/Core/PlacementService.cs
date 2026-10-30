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
        NoVein,
    }

    public enum SellRejection
    {
        None = 0,
        NotDay,
        NothingSelected,
        KeepCannotBeSold,
        AlreadyRemoved,
    }

    public enum RepairRejection
    {
        None = 0,
        NotDay,
        NothingSelected,
        NotRepairable,
        FullHealth,
        InsufficientGold,
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
                case PlacementRejection.NoVein: return "Mines need a gold vein";
                default: return r.ToString();
            }
        }
    }

    /// <summary>
    /// Atomic purchase/sale/repair/income. Single owner of the gold balance; never mutates anything on rejection
    /// and never lets gold go negative.
    /// </summary>
    public sealed class PlacementService
    {
        readonly GridState grid;
        readonly PrototypeDefinitions definitions;

        public int Gold { get; private set; }

        /// <summary>ThriftyConstruction: future prices max(1, floor(base * 0.9)). Existing instance prices are unchanged.</summary>
        public bool Thrifty { get; set; }

        /// <summary>Spec 016: the map's gold vein cells; a Mine's footprint must lie on them.</summary>
        public TerrainMap Terrain { get; set; }

        public PlacementService(GridState grid, PrototypeDefinitions definitions, int startingGold)
        {
            this.grid = grid;
            this.definitions = definitions;
            Gold = startingGold;
        }

        /// <summary>Current construction price for a kind, or -1 when it cannot be purchased.</summary>
        public int PriceFor(BuildingKind kind)
        {
            var def = definitions.For(kind);
            if (def == null || !def.Purchasable) return -1;
            return EconomyRules.ConstructionPrice(def.Cost, Thrifty);
        }

        /// <summary>
        /// Validation shared by ghost preview and actual placement (phase checked by the caller). Every footprint
        /// cell must pass before affordability is considered.
        /// </summary>
        public PlacementRejection Check(BuildingKind kind, GridPos anchor)
        {
            var def = definitions.For(kind);
            if (def == null || !def.Purchasable) return PlacementRejection.NotPurchasable;
            foreach (var cell in def.Footprint(anchor))
            {
                if (!GridCoordinates.IsInBounds(cell)) return PlacementRejection.OutOfBounds;
                if (GridCoordinates.IsReservedRing(cell)) return PlacementRejection.ReservedRing;
                if (grid.IsImpassableTerrain(cell)) return PlacementRejection.ImpassableTerrain;
                if (grid.IsSolid(cell) || grid.HasTrap(cell)) return PlacementRejection.Occupied;
                if (kind == BuildingKind.Mine && (Terrain == null || !Terrain.IsVein(cell))) return PlacementRejection.NoVein;
            }

            if (Gold < PriceFor(kind)) return PlacementRejection.InsufficientGold;
            return PlacementRejection.None;
        }

        public PlacementRejection TryPlace(BuildingKind kind, GridPos anchor, int buildingId, out Building building)
        {
            building = null;
            var rejection = Check(kind, anchor);
            if (rejection != PlacementRejection.None) return rejection;
            var price = PriceFor(kind);
            building = new Building(buildingId, definitions.For(kind), anchor, price);
            grid.Install(building);
            Gold -= price;
            return PlacementRejection.None;
        }

        /// <summary>floor(total paid x 0.5): the purchase price plus every upgrade price (spec 015).</summary>
        public static int RefundFor(Building building) => building.TotalPaid / 2; // floor for non-negative prices

        /// <summary>Current price of upgrading this building one tier (after ThriftyConstruction), or -1 when it cannot be upgraded.</summary>
        public int UpgradePriceFor(Building building)
        {
            if (building?.Definition.Tiers == null || building.Tier >= BuildingUpgrades.MaxTier) return -1;
            return EconomyRules.ConstructionPrice(BuildingUpgrades.BasePrice(building.Definition, building.Tier + 1), Thrifty);
        }

        /// <summary>Charges an upgrade atomically; false (nothing changed) when the price is unaffordable.</summary>
        public bool TrySpend(int price)
        {
            if (price < 0 || Gold < price) return false;
            Gold -= price;
            return true;
        }

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

        /// <summary>Repairs as much missing HP as gold permits at 5 HP/gold, charging ceil(restored / 5).</summary>
        public RepairRejection TryRepair(Building building, out int restored, out int cost)
        {
            restored = 0;
            cost = 0;
            if (building == null) return RepairRejection.NothingSelected;
            if (!building.Definition.HasHp || !building.IsAlive) return RepairRejection.NotRepairable;
            if (building.Hp >= building.MaxHp) return RepairRejection.FullHealth;
            EconomyRules.RepairQuote(building.MaxHp - building.Hp, Gold, out restored, out cost);
            if (restored <= 0) return RepairRejection.InsufficientGold;
            Gold -= cost;
            building.Hp += restored;
            return RepairRejection.None;
        }

        /// <summary>Adds non-negative gold (income, Recycling refunds).</summary>
        public void Earn(int amount)
        {
            if (amount > 0) Gold += amount;
        }
    }
}
