using System;

namespace TheLastFortress
{
    /// <summary>
    /// What the player asks of the hero for one fixed step (specs/003 2.1). The view samples input into this record; the
    /// simulation reads nothing else from input. <see cref="None"/> means the player is not controlling the hero (Strategic
    /// view), so it guards: holds position and swings at enemies in reach.
    /// </summary>
    public readonly struct HeroCommand : IEquatable<HeroCommand>
    {
        /// <summary>False: not controlled (guard behaviour). True: the fields below are the player's input.</summary>
        public readonly bool Controlled;

        /// <summary>Move heading 0..15 (0 = north/+Z, clockwise in 22.5 degree steps), or -1 for no movement.</summary>
        public readonly sbyte Heading;

        public readonly bool Attack;

        /// <summary>Facing 0..63 (0 = north/+Z, clockwise in 5.625 degree steps).</summary>
        public readonly byte Facing;

        public HeroCommand(int heading, bool attack, int facing)
        {
            Controlled = true;
            Heading = (sbyte)(heading < 0 ? -1 : heading & 15);
            Attack = attack;
            Facing = (byte)(facing & 63);
        }

        public static readonly HeroCommand None = default;

        public bool Equals(HeroCommand o) => Controlled == o.Controlled && Heading == o.Heading && Attack == o.Attack && Facing == o.Facing;
        public override bool Equals(object obj) => obj is HeroCommand o && Equals(o);
        public override int GetHashCode() => (Controlled ? 1 : 0) | (Heading + 1) << 1 | (Attack ? 1 : 0) << 6 | Facing << 7;
        public override string ToString() => Controlled ? $"h{Heading}{(Attack ? "!" : "")}f{Facing}" : "-";
    }

    /// <summary>
    /// The player hero (specs/003). Position is kept in integer units (1200 per tile, like enemy movement) so movement and
    /// collision are exact; <see cref="X"/>/<see cref="Z"/> are derived for presentation and distance tests.
    /// </summary>
    public sealed class Hero
    {
        public int UnitsX, UnitsZ;
        public int PrevUnitsX, PrevUnitsZ;
        public int Hp;
        public bool Alive = true;

        /// <summary>Movement per step in 1/1200 tile (by rules version: <see cref="ReleaseCampaign.HeroMoveUnits"/>).</summary>
        public int MoveUnits = HeroRules.MoveUnitsPerStep;

        /// <summary>Facing 0..63 (see <see cref="HeroCommand.Facing"/>).</summary>
        public int Facing;

        public long RespawnTick;
        public long SwingReadyTick;

        public double X => UnitsX / (double)HeroRules.UnitsPerTile;
        public double Z => UnitsZ / (double)HeroRules.UnitsPerTile;
        public double PrevX => PrevUnitsX / (double)HeroRules.UnitsPerTile;
        public double PrevZ => PrevUnitsZ / (double)HeroRules.UnitsPerTile;

        public GridPos Cell => new GridPos(Floor(UnitsX), Floor(UnitsZ));

        static int Floor(int units) => units >= 0 ? units / HeroRules.UnitsPerTile : (units - HeroRules.UnitsPerTile + 1) / HeroRules.UnitsPerTile;

        public void PlaceAt(GridPos cell)
        {
            UnitsX = PrevUnitsX = cell.X * HeroRules.UnitsPerTile + HeroRules.UnitsPerTile / 2;
            UnitsZ = PrevUnitsZ = cell.Z * HeroRules.UnitsPerTile + HeroRules.UnitsPerTile / 2;
        }
    }

    /// <summary>Hero tuning and geometry (specs/003 sections 2.1-2.5). [P] values are provisional tuning.</summary>
    public static class HeroRules
    {
        public const int UnitsPerTile = EnemyDefinition.UnitsPerTile;

        public const int MaxHp = 200; // [P]

        /// <summary>1.5 tiles/s at 0.05 s per step [P]; 3.0 until owner feedback 2026-10-19 (too fast at true scale).</summary>
        public const int MoveUnitsPerStep = 90;

        /// <summary>Body radius 0.3 tile [P].</summary>
        public const int RadiusUnits = 360;

        public const int SwingDamage = 10; // [P]
        public const int SwingIntervalTicks = 15; // 0.75 s [P]; tuned from 15 dmg / 0.6 s by the plan 012 balance check
        public const double SwingReach = 0.6; // [P]; 1.2, then 1.0, until the hero-view figures reached true scale (2026-10-19)

        /// <summary>Half of the 120 degree arc: cos 60 degrees.</summary>
        public const double SwingHalfArcCos = 0.5;

        public const int RespawnTicks = 200; // 10 s [P]

        /// <summary>Melee enemies engage within this distance at a decision point and keep the target within the sticky one [P].</summary>
        public const double EngageRange = 0.6;

        public const double StickyRange = 0.9;

        /// <summary>Kill/damage source ID for the hero in events (buildings use positive IDs, burn uses -1).</summary>
        public const int SourceId = -2;

        /// <summary>Raider Archer target class of the hero (code classes are 0-based: 0 = House/Farm).</summary>
        public const int RangedClass = 0;

        /// <summary>Unit vectors (scaled to <see cref="UnitsPerTile"/>) for the 16 move headings; integer so movement is exact.</summary>
        public static readonly int[] HeadingX = new int[16], HeadingZ = new int[16];

        /// <summary>Unit vectors (scaled to 1024) for the 64 facings, used by the swing arc.</summary>
        public static readonly int[] FacingX = new int[64], FacingZ = new int[64];

        static HeroRules()
        {
            // Rounded once to integers, so every later use is integer arithmetic (or a fixed double derived from it).
            for (var i = 0; i < 16; i++)
            {
                var a = i * Math.PI / 8;
                HeadingX[i] = (int)Math.Round(Math.Sin(a) * UnitsPerTile);
                HeadingZ[i] = (int)Math.Round(Math.Cos(a) * UnitsPerTile);
            }

            for (var i = 0; i < 64; i++)
            {
                var a = i * Math.PI / 32;
                FacingX[i] = (int)Math.Round(Math.Sin(a) * 1024);
                FacingZ[i] = (int)Math.Round(Math.Cos(a) * 1024);
            }
        }

        /// <summary>Facing index (0..63) closest to the direction (dx, dz); 0 for a zero vector.</summary>
        public static int FacingToward(double dx, double dz)
        {
            if (dx == 0 && dz == 0) return 0;
            var a = Math.Atan2(dx, dz); // 0 = +Z, clockwise toward +X
            var i = (int)Math.Round(a / (Math.PI / 32));
            return ((i % 64) + 64) % 64;
        }

        /// <summary>Whether a point at (dx, dz) from the hero centre lies inside the swing arc for this facing.</summary>
        public static bool InSwing(int facing, double dx, double dz)
        {
            var d2 = dx * dx + dz * dz;
            if (d2 > SwingReach * SwingReach + 1e-9) return false;
            if (d2 < 1e-12) return true;
            var dot = (dx * FacingX[facing] + dz * FacingZ[facing]) / 1024.0;
            return dot >= SwingHalfArcCos * Math.Sqrt(d2) - 1e-9;
        }
    }
}
