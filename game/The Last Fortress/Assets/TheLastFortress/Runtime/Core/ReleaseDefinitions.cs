namespace TheLastFortress
{
    /// <summary>
    /// Initial plan 008 release tuning (spec 005-release-rules.md sections 1-2): the MVP definitions plus Cannon, Watchtower, Repair
    /// Station, Raider Archer, Bomber and Siege Commander. The MVP defaults are untouched; values marked provisional there
    /// are tuned in the release definitions asset, never by editing the MVP.
    /// </summary>
    public static class ReleaseDefinitions
    {
        public static PrototypeDefinitions CreateDefault()
        {
            var d = MvpDefinitions.CreateDefault();
            // Cheaper build (2026-10 tuning): every building costs 30% less (rounded) and does about 30% less — HP, fire
            // rate, trap damage, income and repair are scaled down, so the player places more, smaller pieces. The MVP
            // profile keeps its own values.
            Scale(d.Wall, cost: 14, hp: 80);
            Scale(d.Tower, cost: 49, hp: 80);
            d.Tower.AttackIntervalSeconds = 0.9;
            Scale(d.House, cost: 28, hp: 64);
            d.House.DailyIncome = 10;
            Scale(d.Farm, cost: 42, hp: 80);
            d.Farm.DailyIncome = 17;
            d.SpikeTrap.Cost = 25;
            d.SpikeTrap.TrapDamage = 7;
            d.Cannon = new BuildingDefinition
            {
                Kind = BuildingKind.Cannon, DisplayName = "Cannon", Cost = 77, MaxHp = 120,
                AttackDamage = 20, AttackIntervalSeconds = 2.5, Range = 4.5, SplashRadius = 1.25,
            };
            d.Watchtower = new BuildingDefinition
            {
                Kind = BuildingKind.Watchtower, DisplayName = "Watchtower", Cost = 32, MaxHp = 64, RangeBonus = 0.8,
            };
            d.RepairStation = new BuildingDefinition
            {
                Kind = BuildingKind.RepairStation, DisplayName = "Repair Station", Cost = 56, MaxHp = 96, RepairBudget = 40, RepairReach = 2,
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

        static void Scale(BuildingDefinition b, int cost, int hp)
        {
            b.Cost = cost;
            b.MaxHp = hp;
        }
    }
}
