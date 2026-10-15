namespace TheLastFortress
{
    /// <summary>
    /// Initial six-night MVP tuning from specification 003. Separate from the prototype defaults, which stay unchanged.
    /// Fixed rules (not tuning) live in <see cref="EconomyRules"/> and <see cref="PerkRules"/>.
    /// </summary>
    public static class MvpDefinitions
    {
        public static PrototypeDefinitions CreateDefault()
        {
            var d = PrototypeDefinitions.CreateDefault();
            d.StartingGold = 250;
            d.Keep.DailyIncome = 20;
            d.House = new BuildingDefinition { Kind = BuildingKind.House, DisplayName = "House", Cost = 40, MaxHp = 80, DailyIncome = 15 };
            d.Farm = new BuildingDefinition { Kind = BuildingKind.Farm, DisplayName = "Farm", Cost = 60, MaxHp = 100, DailyIncome = 25 };
            d.SpikeTrap = new BuildingDefinition { Kind = BuildingKind.SpikeTrap, DisplayName = "Spike Trap", Cost = 35, TrapDamage = 8 };
            d.Grunt.Kind = EnemyKind.Grunt;
            d.Runner = new EnemyDefinition
            {
                Kind = EnemyKind.Runner, DisplayName = "Runner", MaxHp = 20, AttackDamage = 6, AttackIntervalSeconds = 0.75, SpeedTilesPerSecond = 1.75,
            };
            d.Brute = new EnemyDefinition
            {
                Kind = EnemyKind.Brute, DisplayName = "Brute", MaxHp = 150, AttackDamage = 20, AttackIntervalSeconds = 1.5, SpeedTilesPerSecond = 0.6,
                WallDamageMultiplier = 2,
            };
            d.SiegeGolem = new EnemyDefinition
            {
                Kind = EnemyKind.SiegeGolem, DisplayName = "Siege Golem", MaxHp = 800, AttackDamage = 40, AttackIntervalSeconds = 2.0,
                SpeedTilesPerSecond = 0.4, WallDamageMultiplier = 2,
            };
            return d;
        }
    }
}
