namespace TheLastFortress
{
    /// <summary>A placed structure. Occupancy is owned by <see cref="GridState"/>, not by any GameObject.</summary>
    public sealed class Building
    {
        public readonly int Id;
        public readonly BuildingKind Kind;

        /// <summary>Anchor: the lowest (x,z) footprint cell.</summary>
        public readonly GridPos Cell;

        public readonly GridPos[] Cells;
        public readonly BuildingDefinition Definition;

        /// <summary>Actual price paid (after any discount); refunds use this, never the current definition cost.</summary>
        public readonly int PurchasePrice;

        public int Hp;

        /// <summary>Effective maximum HP (definition plus run modifiers). Equals the definition value without perks.</summary>
        public int MaxHp;

        /// <summary>Cosmetic yaw in 90-degree steps (0..3). Never affects footprint, routing or combat.</summary>
        public int Orientation;

        public bool Removed;

        // Tower combat state (unused by other kinds).
        public int TargetEnemyId = -1;
        public long AttackDueTick;

        public Building(int id, BuildingDefinition definition, GridPos cell, int purchasePrice)
        {
            Id = id;
            Kind = definition.Kind;
            Definition = definition;
            Cell = cell;
            Cells = definition.Footprint(cell);
            PurchasePrice = purchasePrice;
            MaxHp = definition.MaxHp;
            Hp = definition.MaxHp;
        }

        public bool IsAlive => !Removed && (!Definition.HasHp || Hp > 0);
        public bool IsDestructible => Kind != BuildingKind.Keep;
        public bool IsSolid => Definition.IsSolid;

        public bool Covers(GridPos cell)
        {
            foreach (var c in Cells)
                if (c == cell) return true;
            return false;
        }

        /// <summary>World centre of the footprint: the cell centre for 1x1, the shared corner of the four cells for 2x2.</summary>
        public double CenterX => Cell.X + Definition.FootprintSize / 2.0;

        public double CenterZ => Cell.Z + Definition.FootprintSize / 2.0;

        /// <summary>Smallest Chebyshev distance between any footprint cell and <paramref name="cell"/>.</summary>
        public int ChebyshevTo(GridPos cell)
        {
            var best = int.MaxValue;
            foreach (var c in Cells)
            {
                var d = System.Math.Max(System.Math.Abs(c.X - cell.X), System.Math.Abs(c.Z - cell.Z));
                if (d < best) best = d;
            }

            return best;
        }

        /// <summary>Smallest Chebyshev distance between the two footprints.</summary>
        public int ChebyshevTo(Building other)
        {
            var best = int.MaxValue;
            foreach (var c in other.Cells)
            {
                var d = ChebyshevTo(c);
                if (d < best) best = d;
            }

            return best;
        }

        /// <summary>Whether any footprint cell shares an edge with a cell of <paramref name="other"/>.</summary>
        public bool SharesEdgeWith(Building other)
        {
            foreach (var a in Cells)
            foreach (var b in other.Cells)
                if (a.IsCardinalNeighbor(b)) return true;
            return false;
        }
    }

    public sealed class Enemy
    {
        public readonly int Id;
        public readonly int EntryIndex;
        public readonly EnemyDefinition Definition;
        public readonly EnemyKind Kind;

        /// <summary>Whole simulation steps between attacks for this enemy's own definition.</summary>
        public readonly int AttackTicks;

        /// <summary>Fixed-point movement budget per step (1200 units = one tile).</summary>
        public readonly int MoveUnitsPerStep;

        /// <summary>Bomber fuse length in steps (0 for every other kind).</summary>
        public readonly int FuseTicks;

        public int Hp;
        public bool Alive = true;

        /// <summary>Cell whose center the enemy last stood on (segment start while moving).</summary>
        public GridPos Cell;
        public GridPos NextCell;
        public bool Moving;

        /// <summary>Diagnostic steps spent in the current segment; <see cref="ProgressUnits"/> is authoritative.</summary>
        public int MoveTicks;

        /// <summary>Units travelled within the current segment.</summary>
        public int ProgressUnits;

        /// <summary>Units left over at the last cell center, consumed by the next segment so no time is lost per tile.</summary>
        public int CarryUnits;

        // FlamingArrows burn state (one instance; refresh extends expiry, keeps cadence).
        public bool Burning;
        public long BurnNextTick;
        public long BurnExpiryTick;

        public double X, Z;
        public double PrevX, PrevZ;

        public Route Route;
        public int RouteIndex;

        public int TargetBuildingId = -1;

        /// <summary>Melee or ranged enemy whose current target is the hero (then <see cref="TargetBuildingId"/> is -1).</summary>
        public bool TargetsHero;

        public long AttackDueTick;

        /// <summary>
        /// release-rules-4 bounded pursuit of the hero (game rules section 18): chasing off the route until
        /// <see cref="PursueUntilTick"/>, then <see cref="Returning"/> to its cell centre before it replans. Position in
        /// integer units while off the route.
        /// </summary>
        public bool Pursuing, Returning;

        public long PursueUntilTick, PursueCooldownTick, ProvokedUntilTick;
        public int OffUnitsX, OffUnitsZ;

        /// <summary>Bomber: fuse lit at its attack cell; it never moves or retargets again and detonates at <see cref="DetonateTick"/>.</summary>
        public bool FuseLit;

        public long DetonateTick;

        public Enemy(int id, int entryIndex, EnemyDefinition definition, GridPos cell)
        {
            Id = id;
            EntryIndex = entryIndex;
            Definition = definition;
            Kind = definition.Kind;
            AttackTicks = PrototypeDefinitions.ToTicks(definition.AttackIntervalSeconds);
            MoveUnitsPerStep = definition.MoveUnitsPerStep;
            FuseTicks = definition.FuseSeconds > 0 ? PrototypeDefinitions.ToTicks(definition.FuseSeconds) : 0;
            Hp = definition.MaxHp;
            Cell = cell;
            NextCell = cell;
            GridCoordinates.TileCenter(cell, out X, out Z);
            PrevX = X;
            PrevZ = Z;
        }
    }
}
