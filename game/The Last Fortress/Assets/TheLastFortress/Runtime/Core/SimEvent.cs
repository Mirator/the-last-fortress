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
    }

    /// <summary>Notification for presentation adapters. Simulation state stays authoritative.</summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly long Tick;
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly GridPos Cell;

        public SimEvent(SimEventType type, long tick, int sourceId = -1, int targetId = -1, GridPos cell = default)
        {
            Type = type;
            Tick = tick;
            SourceId = sourceId;
            TargetId = targetId;
            Cell = cell;
        }

        public override string ToString() => $"{Tick}:{Type} src={SourceId} tgt={TargetId} cell={Cell}";
    }
}
