namespace TheLastFortress
{
    /// <summary>
    /// Initial plan 008 release tuning (008-rules.md sections 1-2): the MVP definitions plus Cannon, Watchtower, Repair
    /// Station, Raider Archer, Bomber and Siege Commander. The MVP defaults are untouched; values marked provisional there
    /// are tuned in the release definitions asset, never by editing the MVP.
    /// </summary>
    public static class ReleaseDefinitions
    {
        public static PrototypeDefinitions CreateDefault()
        {
            var d = MvpDefinitions.CreateDefault();
            d.Cannon = new BuildingDefinition
            {
                Kind = BuildingKind.Cannon, DisplayName = "Cannon", Cost = 110, MaxHp = 150,
                AttackDamage = 20, AttackIntervalSeconds = 2.0, Range = 4.5, SplashRadius = 1.25,
            };
            d.Watchtower = new BuildingDefinition
            {
                Kind = BuildingKind.Watchtower, DisplayName = "Watchtower", Cost = 45, MaxHp = 80, RangeBonus = 1.0,
            };
            d.RepairStation = new BuildingDefinition
            {
                Kind = BuildingKind.RepairStation, DisplayName = "Repair Station", Cost = 80, MaxHp = 120, RepairBudget = 50, RepairReach = 2,
            };
            d.RaiderArcher = new EnemyDefinition
            {
                Kind = EnemyKind.Archer, DisplayName = "Raider Archer", MaxHp = 35, SpeedTilesPerSecond = 0.9,
                AttackDamage = 6, AttackIntervalSeconds = 1.5, AttackRange = 3.5,
            };
            d.Bomber = new EnemyDefinition
            {
                Kind = EnemyKind.Bomber, DisplayName = "Bomber", MaxHp = 40, SpeedTilesPerSecond = 1.0,
                AttackDamage = 0, AttackIntervalSeconds = 1.5, WallDamageMultiplier = 2,
                FuseSeconds = 1.5, BlastDamage = 60, BlastSplashDamage = 20,
            };
            d.SiegeCommander = new EnemyDefinition
            {
                Kind = EnemyKind.SiegeCommander, DisplayName = "Siege Commander", MaxHp = 1000, SpeedTilesPerSecond = 0.4,
                AttackDamage = 30, AttackIntervalSeconds = 2.0, WallDamageMultiplier = 2,
                AuraRadius = 2.5, AuraDamagePercent = 50,
            };
            return d;
        }
    }
}
