using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Serialized release (plan 008) tuning: all nine building kinds and seven enemy kinds, converted into plain C#
    /// <see cref="PrototypeDefinitions"/> at run creation and validated before play. Separate from the MVP asset so release
    /// tuning never changes the six-night profile. Footprints, rules and the night table are code, not tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Fortress/Release Definitions", fileName = "ReleaseDefinitions")]
    public sealed class ReleaseDefinitionsAsset : ScriptableObject
    {
        [Serializable]
        public sealed class BuildingTuning
        {
            public string displayName;
            public int cost;
            public int maxHp;
            public int attackDamage;
            public float attackIntervalSeconds;
            public float range;
            public int dailyIncome;
            public int trapDamage;
            public float splashRadius;
            public float rangeBonus;
            public int repairBudget;
            public int repairReach;
        }

        [Serializable]
        public sealed class EnemyTuning
        {
            public string displayName;
            public int maxHp;
            public float speedTilesPerSecond;
            public int attackDamage;
            public float attackIntervalSeconds;
            public int wallDamageMultiplier = 1;
            public float attackRange;
            public float fuseSeconds;
            public int blastDamage;
            public int blastSplashDamage;
            public float auraRadius;
            public int auraDamagePercent;
        }

        public int startingGold;
        public BuildingTuning keep = new BuildingTuning();
        public BuildingTuning wall = new BuildingTuning();
        public BuildingTuning tower = new BuildingTuning();
        public BuildingTuning house = new BuildingTuning();
        public BuildingTuning farm = new BuildingTuning();
        public BuildingTuning spikeTrap = new BuildingTuning();
        public BuildingTuning cannon = new BuildingTuning();
        public BuildingTuning watchtower = new BuildingTuning();
        public BuildingTuning repairStation = new BuildingTuning();
        public EnemyTuning grunt = new EnemyTuning();
        public EnemyTuning runner = new EnemyTuning();
        public EnemyTuning brute = new EnemyTuning();
        public EnemyTuning siegeGolem = new EnemyTuning();
        public EnemyTuning raiderArcher = new EnemyTuning();
        public EnemyTuning bomber = new EnemyTuning();
        public EnemyTuning siegeCommander = new EnemyTuning();

        IEnumerable<(BuildingKind kind, string field, BuildingTuning t)> Buildings()
        {
            yield return (BuildingKind.Keep, nameof(keep), keep);
            yield return (BuildingKind.Wall, nameof(wall), wall);
            yield return (BuildingKind.Tower, nameof(tower), tower);
            yield return (BuildingKind.House, nameof(house), house);
            yield return (BuildingKind.Farm, nameof(farm), farm);
            yield return (BuildingKind.SpikeTrap, nameof(spikeTrap), spikeTrap);
            yield return (BuildingKind.Cannon, nameof(cannon), cannon);
            yield return (BuildingKind.Watchtower, nameof(watchtower), watchtower);
            yield return (BuildingKind.RepairStation, nameof(repairStation), repairStation);
        }

        IEnumerable<(EnemyKind kind, string field, EnemyTuning t)> Enemies()
        {
            yield return (EnemyKind.Grunt, nameof(grunt), grunt);
            yield return (EnemyKind.Runner, nameof(runner), runner);
            yield return (EnemyKind.Brute, nameof(brute), brute);
            yield return (EnemyKind.SiegeGolem, nameof(siegeGolem), siegeGolem);
            yield return (EnemyKind.Archer, nameof(raiderArcher), raiderArcher);
            yield return (EnemyKind.Bomber, nameof(bomber), bomber);
            yield return (EnemyKind.SiegeCommander, nameof(siegeCommander), siegeCommander);
        }

        public void CopyFrom(PrototypeDefinitions d)
        {
            startingGold = d.StartingGold;
            foreach (var (kind, _, t) in Buildings()) Copy(d.For(kind), t);
            foreach (var (kind, _, t) in Enemies()) Copy(d.EnemyFor(kind), t);
        }

        /// <summary>Fills kinds added after the asset was created, keeping existing tuning. True when anything changed.</summary>
        public bool FillMissing(PrototypeDefinitions defaults)
        {
            var changed = false;
            foreach (var (kind, _, t) in Buildings())
                if (string.IsNullOrEmpty(t.displayName))
                {
                    Copy(defaults.For(kind), t);
                    changed = true;
                }

            foreach (var (kind, _, t) in Enemies())
                if (t.maxHp <= 0)
                {
                    Copy(defaults.EnemyFor(kind), t);
                    changed = true;
                }

            return changed;
        }

        public PrototypeDefinitions ToDefinitions()
        {
            var d = new PrototypeDefinitions { StartingGold = startingGold };
            d.Keep = ToBuilding(BuildingKind.Keep, keep);
            d.Wall = ToBuilding(BuildingKind.Wall, wall);
            d.Tower = ToBuilding(BuildingKind.Tower, tower);
            d.House = ToBuilding(BuildingKind.House, house);
            d.Farm = ToBuilding(BuildingKind.Farm, farm);
            d.SpikeTrap = ToBuilding(BuildingKind.SpikeTrap, spikeTrap);
            d.Cannon = ToBuilding(BuildingKind.Cannon, cannon);
            d.Watchtower = ToBuilding(BuildingKind.Watchtower, watchtower);
            d.RepairStation = ToBuilding(BuildingKind.RepairStation, repairStation);
            d.Grunt = ToEnemy(EnemyKind.Grunt, grunt);
            d.Runner = ToEnemy(EnemyKind.Runner, runner);
            d.Brute = ToEnemy(EnemyKind.Brute, brute);
            d.SiegeGolem = ToEnemy(EnemyKind.SiegeGolem, siegeGolem);
            d.RaiderArcher = ToEnemy(EnemyKind.Archer, raiderArcher);
            d.Bomber = ToEnemy(EnemyKind.Bomber, bomber);
            d.SiegeCommander = ToEnemy(EnemyKind.SiegeCommander, siegeCommander);
            return d;
        }

        /// <summary>Problems naming this asset and the field, then the rule-level checks of the converted definitions.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var (_, field, t) in Buildings())
                if (string.IsNullOrEmpty(t.displayName)) errors.Add($"{name}.{field}: display name missing (run setup to fill new kinds).");
            foreach (var (_, field, t) in Enemies())
                if (string.IsNullOrEmpty(t.displayName)) errors.Add($"{name}.{field}: display name missing (run setup to fill new kinds).");
            foreach (var e in ToDefinitions().Validate()) errors.Add($"{name}: {e}");
            return errors;
        }

        static void Copy(BuildingDefinition from, BuildingTuning to)
        {
            if (from == null) return;
            to.displayName = from.DisplayName;
            to.cost = from.Cost;
            to.maxHp = from.MaxHp;
            to.attackDamage = from.AttackDamage;
            to.attackIntervalSeconds = (float)from.AttackIntervalSeconds;
            to.range = (float)from.Range;
            to.dailyIncome = from.DailyIncome;
            to.trapDamage = from.TrapDamage;
            to.splashRadius = (float)from.SplashRadius;
            to.rangeBonus = (float)from.RangeBonus;
            to.repairBudget = from.RepairBudget;
            to.repairReach = from.RepairReach;
        }

        static void Copy(EnemyDefinition from, EnemyTuning to)
        {
            if (from == null) return;
            to.displayName = from.DisplayName;
            to.maxHp = from.MaxHp;
            to.speedTilesPerSecond = (float)from.SpeedTilesPerSecond;
            to.attackDamage = from.AttackDamage;
            to.attackIntervalSeconds = (float)from.AttackIntervalSeconds;
            to.wallDamageMultiplier = from.WallDamageMultiplier;
            to.attackRange = (float)from.AttackRange;
            to.fuseSeconds = (float)from.FuseSeconds;
            to.blastDamage = from.BlastDamage;
            to.blastSplashDamage = from.BlastSplashDamage;
            to.auraRadius = (float)from.AuraRadius;
            to.auraDamagePercent = from.AuraDamagePercent;
        }

        static BuildingDefinition ToBuilding(BuildingKind kind, BuildingTuning t) => new BuildingDefinition
        {
            Kind = kind, DisplayName = t.displayName, Cost = t.cost, MaxHp = t.maxHp, AttackDamage = t.attackDamage,
            AttackIntervalSeconds = Rounded(t.attackIntervalSeconds), Range = Rounded(t.range), DailyIncome = t.dailyIncome,
            TrapDamage = t.trapDamage, SplashRadius = Rounded(t.splashRadius), RangeBonus = Rounded(t.rangeBonus),
            RepairBudget = t.repairBudget, RepairReach = t.repairReach,
        };

        static EnemyDefinition ToEnemy(EnemyKind kind, EnemyTuning t) => new EnemyDefinition
        {
            Kind = kind, DisplayName = t.displayName, MaxHp = t.maxHp, SpeedTilesPerSecond = Rounded(t.speedTilesPerSecond),
            AttackDamage = t.attackDamage, AttackIntervalSeconds = Rounded(t.attackIntervalSeconds), WallDamageMultiplier = t.wallDamageMultiplier,
            AttackRange = Rounded(t.attackRange), FuseSeconds = Rounded(t.fuseSeconds), BlastDamage = t.blastDamage,
            BlastSplashDamage = t.blastSplashDamage, AuraRadius = Rounded(t.auraRadius), AuraDamagePercent = t.auraDamagePercent,
        };

        static double Rounded(float value) => Math.Round(value, 6);
    }
}
