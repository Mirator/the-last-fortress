namespace TheLastFortress
{
    /// <summary>A placed structure. Occupancy is owned by <see cref="GridState"/>, not by any GameObject.</summary>
    public sealed class Building
    {
        public readonly int Id;
        public readonly BuildingKind Kind;
        public readonly GridPos Cell;
        public readonly BuildingDefinition Definition;
        public readonly int PurchasePrice;
        public int Hp;
        public bool Removed;

        // Tower combat state (unused by Keep/Wall).
        public int TargetEnemyId = -1;
        public long AttackDueTick;

        public Building(int id, BuildingDefinition definition, GridPos cell, int purchasePrice)
        {
            Id = id;
            Kind = definition.Kind;
            Definition = definition;
            Cell = cell;
            PurchasePrice = purchasePrice;
            Hp = definition.MaxHp;
        }

        public bool IsAlive => !Removed && Hp > 0;
        public bool IsDestructible => Kind != BuildingKind.Keep;
    }

    public sealed class Enemy
    {
        public readonly int Id;
        public readonly int EntryIndex;
        public readonly EnemyDefinition Definition;
        public int Hp;
        public bool Alive = true;

        /// <summary>Cell whose center the enemy last stood on (segment start while moving).</summary>
        public GridPos Cell;
        public GridPos NextCell;
        public bool Moving;
        public int MoveTicks;

        public double X, Z;
        public double PrevX, PrevZ;

        public Route Route;
        public int RouteIndex;

        public int TargetBuildingId = -1;
        public long AttackDueTick;

        public Enemy(int id, int entryIndex, EnemyDefinition definition, GridPos cell)
        {
            Id = id;
            EntryIndex = entryIndex;
            Definition = definition;
            Hp = definition.MaxHp;
            Cell = cell;
            NextCell = cell;
            GridCoordinates.TileCenter(cell, out X, out Z);
            PrevX = X;
            PrevZ = Z;
        }
    }
}
