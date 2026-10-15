using System;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Serialized MVP tuning, separate from the prototype's <see cref="PrototypeDefinitionsAsset"/> so MVP tuning passes
    /// never touch the prototype. Footprints and fixed economy rules are code, not tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Fortress/MVP Definitions", fileName = "MvpDefinitions")]
    public sealed class MvpDefinitionsAsset : ScriptableObject
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
        }

        public int startingGold;
        public BuildingTuning keep = new BuildingTuning();
        public BuildingTuning wall = new BuildingTuning();
        public BuildingTuning tower = new BuildingTuning();
        public BuildingTuning house = new BuildingTuning();
        public BuildingTuning farm = new BuildingTuning();
        public BuildingTuning spikeTrap = new BuildingTuning();
        public EnemyTuning grunt = new EnemyTuning();
        public EnemyTuning runner = new EnemyTuning();
        public EnemyTuning brute = new EnemyTuning();
        public EnemyTuning siegeGolem = new EnemyTuning();

        public void CopyFrom(PrototypeDefinitions d)
        {
            startingGold = d.StartingGold;
            Copy(d.Keep, keep);
            Copy(d.Wall, wall);
            Copy(d.Tower, tower);
            Copy(d.House, house);
            Copy(d.Farm, farm);
            Copy(d.SpikeTrap, spikeTrap);
            Copy(d.Grunt, grunt);
            Copy(d.Runner, runner);
            Copy(d.Brute, brute);
            Copy(d.SiegeGolem, siegeGolem);
        }

        /// <summary>
        /// Adds definitions introduced after this asset was created (e.g. 004 enemy kinds) without touching existing tuning.
        /// Returns true when anything was filled.
        /// </summary>
        public bool FillMissing(PrototypeDefinitions defaults)
        {
            var changed = false;
            if (runner.maxHp <= 0) { Copy(defaults.Runner, runner); changed = true; }
            if (brute.maxHp <= 0) { Copy(defaults.Brute, brute); changed = true; }
            if (siegeGolem.maxHp <= 0) { Copy(defaults.SiegeGolem, siegeGolem); changed = true; }
            return changed;
        }

        static void Copy(EnemyDefinition from, EnemyTuning to)
        {
            to.displayName = from.DisplayName;
            to.maxHp = from.MaxHp;
            to.speedTilesPerSecond = (float)from.SpeedTilesPerSecond;
            to.attackDamage = from.AttackDamage;
            to.attackIntervalSeconds = (float)from.AttackIntervalSeconds;
            to.wallDamageMultiplier = from.WallDamageMultiplier;
        }

        static void Copy(BuildingDefinition from, BuildingTuning to)
        {
            to.displayName = from.DisplayName;
            to.cost = from.Cost;
            to.maxHp = from.MaxHp;
            to.attackDamage = from.AttackDamage;
            to.attackIntervalSeconds = (float)from.AttackIntervalSeconds;
            to.range = (float)from.Range;
            to.dailyIncome = from.DailyIncome;
            to.trapDamage = from.TrapDamage;
        }

        public PrototypeDefinitions ToDefinitions() => new PrototypeDefinitions
        {
            StartingGold = startingGold,
            Keep = ToBuilding(BuildingKind.Keep, keep),
            Wall = ToBuilding(BuildingKind.Wall, wall),
            Tower = ToBuilding(BuildingKind.Tower, tower),
            House = ToBuilding(BuildingKind.House, house),
            Farm = ToBuilding(BuildingKind.Farm, farm),
            SpikeTrap = ToBuilding(BuildingKind.SpikeTrap, spikeTrap),
            Grunt = ToEnemy(EnemyKind.Grunt, grunt),
            Runner = ToEnemy(EnemyKind.Runner, runner),
            Brute = ToEnemy(EnemyKind.Brute, brute),
            SiegeGolem = ToEnemy(EnemyKind.SiegeGolem, siegeGolem),
        };

        static EnemyDefinition ToEnemy(EnemyKind kind, EnemyTuning t) => new EnemyDefinition
        {
            Kind = kind,
            DisplayName = t.displayName,
            MaxHp = t.maxHp,
            SpeedTilesPerSecond = Rounded(t.speedTilesPerSecond),
            AttackDamage = t.attackDamage,
            AttackIntervalSeconds = Rounded(t.attackIntervalSeconds),
            WallDamageMultiplier = t.wallDamageMultiplier,
        };

        static BuildingDefinition ToBuilding(BuildingKind kind, BuildingTuning t) => new BuildingDefinition
        {
            Kind = kind,
            DisplayName = t.displayName,
            Cost = t.cost,
            MaxHp = t.maxHp,
            AttackDamage = t.attackDamage,
            AttackIntervalSeconds = Rounded(t.attackIntervalSeconds),
            Range = Rounded(t.range),
            DailyIncome = t.dailyIncome,
            TrapDamage = t.trapDamage,
        };

        static double Rounded(float value) => Math.Round(value, 6);
    }
}
