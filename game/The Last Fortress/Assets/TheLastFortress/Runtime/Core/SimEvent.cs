namespace TheLastFortress
{
    public enum SimEventType
    {
        RunReset,
        StateChanged,
        BuildingPlaced,
        BuildingSold,
        BuildingDestroyed,
        BuildingRemovedScripted,
        BuildingDamaged,
        EnemySpawned,
        EnemyKilled,
        TowerShot,
        EnemyAttacked,
        RouteMissing,
        IncomePaid,
        BuildingRepaired,
        TrapTriggered,
        DamageReflected,
        BuildingRotated,
        OfferCreated,
        PerkAcquired,
        BurnTick,

        // Plan 008 (release profile).
        /// <summary>A Cannon shot also hit this enemy (source Cannon, target enemy). The primary hit is the TowerShot.</summary>
        SplashHit,

        /// <summary>A Raider Archer's shot (source enemy, target building; positions archer → building centre).</summary>
        RangedShot,

        /// <summary>A Bomber reached its target and lit its fuse (source enemy, target building).</summary>
        FuseLit,

        /// <summary>A Bomber exploded at its cell (source enemy, target building or -1; positions = blast centre).</summary>
        BomberDetonated,

        /// <summary>A Repair Station spent its dawn budget (source station, target = HP restored in total).</summary>
        DawnRepair,
    }

    /// <summary>Notification for presentation adapters. Simulation state stays authoritative.</summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly long Tick;
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly GridPos Cell;

        /// <summary>
        /// True when From/To carry world positions captured when the event happened (TowerShot: tower → target;
        /// EnemyKilled and BuildingDestroyed: From = To = where it happened). They stay valid after the entities are
        /// removed later in the same step, so presentation never has to look them up or fall back to <see cref="Cell"/>.
        /// </summary>
        public readonly bool HasPositions;

        public readonly double FromX, FromZ, ToX, ToZ;

        public SimEvent(SimEventType type, long tick, int sourceId = -1, int targetId = -1, GridPos cell = default)
        {
            Type = type;
            Tick = tick;
            SourceId = sourceId;
            TargetId = targetId;
            Cell = cell;
            HasPositions = false;
            FromX = FromZ = ToX = ToZ = 0;
        }

        public SimEvent(SimEventType type, long tick, int sourceId, int targetId, GridPos cell, double fromX, double fromZ, double toX, double toZ)
            : this(type, tick, sourceId, targetId, cell)
        {
            HasPositions = true;
            FromX = fromX;
            FromZ = fromZ;
            ToX = toX;
            ToZ = toZ;
        }

        public override string ToString() => $"{Tick}:{Type} src={SourceId} tgt={TargetId} cell={Cell}";
    }
}
