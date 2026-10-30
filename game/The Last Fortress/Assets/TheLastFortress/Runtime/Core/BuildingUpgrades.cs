namespace TheLastFortress
{
    public enum UpgradeRejection
    {
        None = 0,
        NotDay,
        NothingSelected,
        NotUpgradable,
        MaxTier,
        InsufficientGold,
    }

    /// <summary>
    /// One upgrade tier's changes against the definition (game rules section 7, upgrade tiers). Zero means "as the definition";
    /// tier III values include tier II's. Shared and read-only.
    /// </summary>
    public sealed class BuildingTier
    {
        /// <summary>Tier I: the definition unchanged.</summary>
        public static readonly BuildingTier None = new BuildingTier();

        /// <summary>Max HP and attack damage raised by these percentages of the definition (floor).</summary>
        public int HpPercent, DamagePercent;

        /// <summary>Added to the definition's range and splash radius (before perks).</summary>
        public double RangeBonus, SplashBonus;

        /// <summary>Replaces the definition's attack interval when positive.</summary>
        public double IntervalSeconds;

        /// <summary>House, Farm and Mine: gold added to the definition's daily income.</summary>
        public int IncomeBonus;

        /// <summary>Spike Trap: replaces the definition's damage on entry when positive.</summary>
        public int TrapDamage;

        /// <summary>Spike Trap barbs, Cannon concussion: slow applied to each enemy hit.</summary>
        public int SlowPercent;

        public double SlowSeconds;

        /// <summary>Wall spikes: damage reflected per melee hit received (adds to the trap synergy and Barbed Defenses).</summary>
        public int Reflect;

        /// <summary>Wall stone: the enemy Wall multiplier no longer applies to it.</summary>
        public bool Stone;

        /// <summary>Archer Tower: second arrow damage as a percentage of a full hit (0 = no second arrow).</summary>
        public int SecondArrowPercent;

        /// <summary>House Market hall: each edge-adjacent Farm earns this much more (once per Farm).</summary>
        public int MarketHallBonus;

        /// <summary>Farm Granary: destroyed in combat, it still pays half its income at the next dawn.</summary>
        public bool Granary;

        /// <summary>Mine Stockade (spec 016): melee hits received during a raid are halved.</summary>
        public bool Stockade;

        /// <summary>Watchtower: range bonus (replaces the definition's), spotter damage percentage, and Chebyshev reach (0 = edge-adjacent).</summary>
        public double WatchRangeBonus;

        public int SpotterPercent, WatchReach;

        /// <summary>Repair Station: budget and reach (replace the definition's when positive); Keep care makes the Keep eligible.</summary>
        public int RepairBudget, RepairReach;

        public bool KeepCare;
    }

    /// <summary>
    /// Upgrade tiers II and III (game rules section 7) [P]. Only definitions carrying <see cref="BuildingDefinition.Tiers"/> can be
    /// upgraded; <see cref="ReleaseCampaign.Contact"/> adds them for release-rules-4 only, so legacy profiles never upgrade.
    /// </summary>
    public static class BuildingUpgrades
    {
        public const int MaxTier = 3;

        /// <summary>The selection panel's reason for a disabled Upgrade button.</summary>
        public static string Describe(UpgradeRejection r)
        {
            switch (r)
            {
                case UpgradeRejection.None: return "Upgrade";
                case UpgradeRejection.NotDay: return "Upgrade only during Day";
                case UpgradeRejection.MaxTier: return "Maximum tier";
                case UpgradeRejection.InsufficientGold: return "Not enough gold";
                case UpgradeRejection.NothingSelected: return "Nothing selected";
                default: return "Cannot be upgraded";
            }
        }

        /// <summary>Roman numeral of a tier (I-III) for labels.</summary>
        public static string Numeral(int tier) => tier >= 3 ? "III" : tier == 2 ? "II" : "I";

        /// <summary>Tier III price as a percentage of the base price [P]; tier II costs the base price.</summary>
        public const int TierThreePricePercent = 150;

        /// <summary>Price of upgrading to <paramref name="tier"/> before Thrifty Construction: base, then floor(1.5 x base).</summary>
        public static int BasePrice(BuildingDefinition def, int tier) => tier >= 3 ? def.Cost * TierThreePricePercent / 100 : def.Cost;

        /// <summary>The changes of <paramref name="tier"/> for this definition (<see cref="BuildingTier.None"/> at tier I or without tiers).</summary>
        public static BuildingTier For(BuildingDefinition def, int tier) =>
            tier <= 1 || def.Tiers == null || tier - 2 >= def.Tiers.Length ? BuildingTier.None : def.Tiers[tier - 2];

        /// <summary>Spec 015 section 4: tiers II and III per kind (null for the Keep).</summary>
        public static BuildingTier[] Table(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Wall:
                    return new[]
                    {
                        new BuildingTier { HpPercent = 30, Reflect = 2 },
                        new BuildingTier { HpPercent = 60, Reflect = 4, Stone = true },
                    };
                case BuildingKind.Tower:
                    return new[]
                    {
                        new BuildingTier { DamagePercent = 20, SecondArrowPercent = 50 },
                        new BuildingTier { DamagePercent = 40, RangeBonus = 0.5, SecondArrowPercent = 100 },
                    };
                case BuildingKind.House:
                    return new[]
                    {
                        new BuildingTier { IncomeBonus = 5 },
                        new BuildingTier { IncomeBonus = 10, MarketHallBonus = 5 },
                    };
                case BuildingKind.Farm:
                    return new[]
                    {
                        new BuildingTier { IncomeBonus = 5 },
                        new BuildingTier { IncomeBonus = 10, Granary = true },
                    };
                case BuildingKind.SpikeTrap:
                    return new[]
                    {
                        new BuildingTier { TrapDamage = 10, SlowPercent = 30, SlowSeconds = 1.5 },
                        new BuildingTier { TrapDamage = 14, SlowPercent = 50, SlowSeconds = 2.0 },
                    };
                case BuildingKind.Cannon:
                    return new[]
                    {
                        new BuildingTier { DamagePercent = 25, SplashBonus = 0.25, SlowPercent = 30, SlowSeconds = 2.0 },
                        new BuildingTier { DamagePercent = 25, SplashBonus = 0.25, SlowPercent = 50, SlowSeconds = 2.0, IntervalSeconds = 2.0 },
                    };
                case BuildingKind.Watchtower:
                    return new[]
                    {
                        new BuildingTier { WatchRangeBonus = 1.0, SpotterPercent = 10 },
                        new BuildingTier { WatchRangeBonus = 1.2, SpotterPercent = 20, WatchReach = 2 },
                    };
                case BuildingKind.RepairStation:
                    return new[]
                    {
                        new BuildingTier { RepairBudget = 60, RepairReach = 3 },
                        new BuildingTier { RepairBudget = 80, RepairReach = 3, KeepCare = true },
                    };
                case BuildingKind.Mine:
                    return new[]
                    {
                        new BuildingTier { IncomeBonus = 10 },
                        new BuildingTier { IncomeBonus = 20, Stockade = true },
                    };
                default:
                    return null;
            }
        }
    }
}
