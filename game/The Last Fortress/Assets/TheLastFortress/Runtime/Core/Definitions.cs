using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>Stable serialized IDs: append only, never reorder.</summary>
    public enum BuildingKind
    {
        Keep = 0,
        Wall = 1,
        Tower = 2,
        House = 3,
        Farm = 4,
        SpikeTrap = 5,
    }

    /// <summary>Tuning values for a structure. Shared and read-only during a run; perks never mutate it.</summary>
    public sealed class BuildingDefinition
    {
        public BuildingKind Kind;
        public string DisplayName;
        public int Cost;
        public int MaxHp;
        public int AttackDamage;
        public double AttackIntervalSeconds;
        public double Range;

        /// <summary>Gold paid on entering each new Day while the building survives (MVP only).</summary>
        public int DailyIncome;

        /// <summary>Damage dealt once to an enemy completing entry into the cell (Spike Trap only).</summary>
        public int TrapDamage;

        public bool CanAttack => AttackDamage > 0;
        public bool Purchasable => Kind != BuildingKind.Keep;

        /// <summary>Traps are a walkable floor layer; every other structure blocks movement.</summary>
        public bool IsSolid => Kind != BuildingKind.SpikeTrap;

        public bool HasHp => Kind != BuildingKind.SpikeTrap;

        /// <summary>Square footprint edge length. Fixed by the rules, not tuning: Farm is 2 x 2, everything else 1 x 1.</summary>
        public int FootprintSize => FootprintSizeOf(Kind);

        public static int FootprintSizeOf(BuildingKind kind) => kind == BuildingKind.Farm ? 2 : 1;

        /// <summary>Footprint cells from the lowest (x,z) anchor, in offset order (0,0),(1,0),(0,1),(1,1).</summary>
        public static GridPos[] FootprintCells(BuildingKind kind, GridPos anchor)
        {
            var size = FootprintSizeOf(kind);
            var cells = new GridPos[size * size];
            var i = 0;
            for (var dz = 0; dz < size; dz++)
            for (var dx = 0; dx < size; dx++)
                cells[i++] = new GridPos(anchor.X + dx, anchor.Z + dz);
            return cells;
        }
    }

    /// <summary>Stable serialized IDs: append only, never reorder.</summary>
    public enum EnemyKind
    {
        Grunt = 0,
        Runner = 1,
        Brute = 2,
        SiegeGolem = 3,
    }

    public sealed class EnemyDefinition
    {
        public EnemyKind Kind;
        public string DisplayName;
        public int MaxHp;
        public double SpeedTilesPerSecond;
        public int AttackDamage;
        public double AttackIntervalSeconds;

        /// <summary>Damage multiplier applied only when the target is a Wall (Brute/Golem x2).</summary>
        public int WallDamageMultiplier = 1;

        /// <summary>Fixed-point movement: one tile = this many units.</summary>
        public const int UnitsPerTile = 1200;

        /// <summary>Movement units consumed per 0.05 s step: speed * 60 (Grunt 60, Runner 105, Brute 36, Golem 24).</summary>
        public int MoveUnitsPerStep => (int)Math.Round(SpeedTilesPerSecond * UnitsPerTile * PrototypeDefinitions.StepSeconds);
    }

    /// <summary>
    /// Complete tuning set. Values are validated before a run can start. House/Farm/SpikeTrap are optional:
    /// absent in the prototype (not purchasable there), present in the MVP (see <see cref="MvpDefinitions"/>).
    /// </summary>
    public sealed class PrototypeDefinitions
    {
        public const double StepSeconds = 0.05;

        public int StartingGold;
        public BuildingDefinition Keep;
        public BuildingDefinition Wall;
        public BuildingDefinition Tower;
        public BuildingDefinition House;
        public BuildingDefinition Farm;
        public BuildingDefinition SpikeTrap;
        public EnemyDefinition Grunt;
        public EnemyDefinition Runner;
        public EnemyDefinition Brute;
        public EnemyDefinition SiegeGolem;

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

        /// <summary>Definition for a kind, or null for an optional kind absent from this tuning set.</summary>
        public BuildingDefinition For(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Keep: return Keep;
                case BuildingKind.Wall: return Wall;
                case BuildingKind.Tower: return Tower;
                case BuildingKind.House: return House;
                case BuildingKind.Farm: return Farm;
                case BuildingKind.SpikeTrap: return SpikeTrap;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>Definition for an enemy kind, or null for a kind absent from this tuning set (prototype: Grunt only).</summary>
        public EnemyDefinition EnemyFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Grunt: return Grunt;
                case EnemyKind.Runner: return Runner;
                case EnemyKind.Brute: return Brute;
                case EnemyKind.SiegeGolem: return SiegeGolem;
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
            if (House != null) ValidateBuilding(House, BuildingKind.House, errors);
            if (Farm != null) ValidateBuilding(Farm, BuildingKind.Farm, errors);
            if (SpikeTrap != null)
            {
                if (SpikeTrap.Kind != BuildingKind.SpikeTrap) errors.Add($"SpikeTrap definition has kind {SpikeTrap.Kind}.");
                if (SpikeTrap.Cost < 0) errors.Add("Spike Trap cost must not be negative.");
                if (SpikeTrap.TrapDamage <= 0) errors.Add("Spike Trap damage must be positive.");
            }

            if (Grunt == null)
            {
                errors.Add("Grunt definition missing.");
                return errors;
            }

            ValidateEnemy(Grunt, errors);
            if (Runner != null) ValidateEnemy(Runner, errors);
            if (Brute != null) ValidateEnemy(Brute, errors);
            if (SiegeGolem != null) ValidateEnemy(SiegeGolem, errors);
            return errors;
        }

        static void ValidateEnemy(EnemyDefinition e, List<string> errors)
        {
            var name = e.DisplayName ?? e.Kind.ToString();
            if (e.MaxHp <= 0) errors.Add($"{name} HP must be positive.");
            if (e.AttackDamage < 0) errors.Add($"{name} damage must not be negative.");
            if (e.WallDamageMultiplier < 1) errors.Add($"{name} wall multiplier must be at least 1.");
            ValidateInterval($"{name} attack interval", e.AttackIntervalSeconds, errors);
            if (!(e.SpeedTilesPerSecond > 0) || double.IsInfinity(e.SpeedTilesPerSecond))
            {
                errors.Add($"{name} speed must be positive and finite.");
                return;
            }

            // Movement is fixed-point: speed * 60 units per step must be a whole number (attack timing is validated separately).
            var units = e.SpeedTilesPerSecond * EnemyDefinition.UnitsPerTile * StepSeconds;
            if (Math.Abs(units - Math.Round(units)) > 1e-6 || Math.Round(units) < 1)
                errors.Add($"{name} speed ({e.SpeedTilesPerSecond} tiles/s) must be a whole number of 1/1200-tile units per {StepSeconds}s step.");
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
            if (def.DailyIncome < 0) errors.Add($"{def.DisplayName} income must not be negative.");
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
