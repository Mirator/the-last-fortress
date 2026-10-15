using System;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>Serialized tuning. Converted into plain <see cref="PrototypeDefinitions"/> before a run.</summary>
    [CreateAssetMenu(menuName = "The Last Fortress/Prototype Definitions", fileName = "PrototypeDefinitions")]
    public sealed class PrototypeDefinitionsAsset : ScriptableObject
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
        }

        [Serializable]
        public sealed class EnemyTuning
        {
            public string displayName;
            public int maxHp;
            public float speedTilesPerSecond;
            public int attackDamage;
            public float attackIntervalSeconds;
        }

        public int startingGold;
        public BuildingTuning keep = new BuildingTuning();
        public BuildingTuning wall = new BuildingTuning();
        public BuildingTuning tower = new BuildingTuning();
        public EnemyTuning grunt = new EnemyTuning();

        public void CopyFrom(PrototypeDefinitions d)
        {
            startingGold = d.StartingGold;
            Copy(d.Keep, keep);
            Copy(d.Wall, wall);
            Copy(d.Tower, tower);
            grunt.displayName = d.Grunt.DisplayName;
            grunt.maxHp = d.Grunt.MaxHp;
            grunt.speedTilesPerSecond = (float)d.Grunt.SpeedTilesPerSecond;
            grunt.attackDamage = d.Grunt.AttackDamage;
            grunt.attackIntervalSeconds = (float)d.Grunt.AttackIntervalSeconds;
        }

        static void Copy(BuildingDefinition from, BuildingTuning to)
        {
            to.displayName = from.DisplayName;
            to.cost = from.Cost;
            to.maxHp = from.MaxHp;
            to.attackDamage = from.AttackDamage;
            to.attackIntervalSeconds = (float)from.AttackIntervalSeconds;
            to.range = (float)from.Range;
        }

        public PrototypeDefinitions ToDefinitions() => new PrototypeDefinitions
        {
            StartingGold = startingGold,
            Keep = ToBuilding(BuildingKind.Keep, keep),
            Wall = ToBuilding(BuildingKind.Wall, wall),
            Tower = ToBuilding(BuildingKind.Tower, tower),
            Grunt = new EnemyDefinition
            {
                DisplayName = grunt.displayName,
                MaxHp = grunt.maxHp,
                SpeedTilesPerSecond = Rounded(grunt.speedTilesPerSecond),
                AttackDamage = grunt.attackDamage,
                AttackIntervalSeconds = Rounded(grunt.attackIntervalSeconds),
            },
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
        };

        // Serialized floats such as 0.75f are exact, but round to 6 decimals so values like 0.1f do not drift.
        static double Rounded(float value) => Math.Round(value, 6);
    }
}
