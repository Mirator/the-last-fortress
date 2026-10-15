using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    public enum BuildingKind
    {
        Keep = 0,
        Wall = 1,
        Tower = 2,
    }

    /// <summary>Tuning values for a one-cell structure.</summary>
    public sealed class BuildingDefinition
    {
        public BuildingKind Kind;
        public string DisplayName;
        public int Cost;
        public int MaxHp;
        public int AttackDamage;
        public double AttackIntervalSeconds;
        public double Range;

        public bool CanAttack => AttackDamage > 0;
        public bool Purchasable => Kind != BuildingKind.Keep;
    }

    public sealed class EnemyDefinition
    {
        public string DisplayName;
        public int MaxHp;
        public double SpeedTilesPerSecond;
        public int AttackDamage;
        public double AttackIntervalSeconds;
    }

    /// <summary>Complete prototype tuning set. Values are validated before a run can start.</summary>
    public sealed class PrototypeDefinitions
    {
        public const double StepSeconds = 0.05;

        public int StartingGold;
        public BuildingDefinition Keep;
        public BuildingDefinition Wall;
        public BuildingDefinition Tower;
        public EnemyDefinition Grunt;

        /// <summary>Initial tuning from specification 002, "Initial combat tuning".</summary>
        public static PrototypeDefinitions CreateDefault() => new PrototypeDefinitions
        {
            StartingGold = 250,
            Keep = new BuildingDefinition { Kind = BuildingKind.Keep, DisplayName = "Keep", Cost = 0, MaxHp = 500 },
            Wall = new BuildingDefinition { Kind = BuildingKind.Wall, DisplayName = "Wall", Cost = 20, MaxHp = 100 },
            Tower = new BuildingDefinition
            {
                Kind = BuildingKind.Tower, DisplayName = "Archer Tower", Cost = 70, MaxHp = 100,
                AttackDamage = 10, AttackIntervalSeconds = 0.75, Range = 4,
            },
            Grunt = new EnemyDefinition
            {
                DisplayName = "Grunt", MaxHp = 30, SpeedTilesPerSecond = 1, AttackDamage = 10, AttackIntervalSeconds = 1,
            },
        };

        public BuildingDefinition For(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Keep: return Keep;
                case BuildingKind.Wall: return Wall;
                case BuildingKind.Tower: return Tower;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>Converts seconds into whole simulation steps; callers must validate first.</summary>
        public static int ToTicks(double seconds) => (int)Math.Round(seconds / StepSeconds);

        public int GruntTicksPerTile => ToTicks(1.0 / Grunt.SpeedTilesPerSecond);

        /// <summary>Returns human-readable problems; an empty list means the definitions can start a run.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (StartingGold < 0) errors.Add("Starting gold must not be negative.");
            ValidateBuilding(Keep, BuildingKind.Keep, errors);
            ValidateBuilding(Wall, BuildingKind.Wall, errors);
            ValidateBuilding(Tower, BuildingKind.Tower, errors);
            if (Grunt == null)
            {
                errors.Add("Grunt definition missing.");
                return errors;
            }

            if (Grunt.MaxHp <= 0) errors.Add("Grunt HP must be positive.");
            if (Grunt.AttackDamage < 0) errors.Add("Grunt damage must not be negative.");
            ValidateInterval("Grunt attack interval", Grunt.AttackIntervalSeconds, errors);
            if (Grunt.SpeedTilesPerSecond <= 0) errors.Add("Grunt speed must be positive.");
            else ValidateInterval("Grunt seconds per tile", 1.0 / Grunt.SpeedTilesPerSecond, errors);
            return errors;
        }

        static void ValidateBuilding(BuildingDefinition def, BuildingKind expected, List<string> errors)
        {
            if (def == null)
            {
                errors.Add($"{expected} definition missing.");
                return;
            }

            if (def.Kind != expected) errors.Add($"{expected} definition has kind {def.Kind}.");
            if (def.MaxHp <= 0) errors.Add($"{def.DisplayName} HP must be positive.");
            if (def.Cost < 0) errors.Add($"{def.DisplayName} cost must not be negative.");
            if (def.CanAttack)
            {
                ValidateInterval($"{def.DisplayName} attack interval", def.AttackIntervalSeconds, errors);
                if (def.Range <= 0) errors.Add($"{def.DisplayName} range must be positive.");
            }
        }

        /// <summary>Intervals must be positive and land on whole fixed steps so scheduling stays integral.</summary>
        static void ValidateInterval(string label, double seconds, List<string> errors)
        {
            if (!(seconds > 0))
            {
                errors.Add($"{label} must be positive.");
                return;
            }

            var ticks = seconds / StepSeconds;
            if (Math.Abs(ticks - Math.Round(ticks)) > 1e-6)
                errors.Add($"{label} ({seconds}s) must be a whole multiple of the {StepSeconds}s step.");
        }
    }
}
