using System;

namespace TheLastFortress
{
    /// <summary>
    /// The hero's tuning and geometry for one rules version (game rules section 17; release-rules-4: specs/003 sections 3-5).
    /// Immutable and selected once per run by <see cref="For"/>, so profile differences live here instead of in scattered
    /// version checks. The release-rules-2/3 profiles carry exactly the <see cref="HeroRules"/> values they always used.
    /// </summary>
    public sealed class HeroProfile
    {
        /// <summary>release-rules-4: accelerating motor, wind-up/contact/recovery attack, profile reaches.</summary>
        public bool ContactPhase { get; private set; }

        /// <summary>Body radius in 1/1200 tile.</summary>
        public int RadiusUnits { get; private set; }

        /// <summary>Top speed in 1/1200 tile per step (the legacy move per step).</summary>
        public int MaxSpeedUnits { get; private set; }

        /// <summary>release-rules-4: most the velocity may change toward a desired velocity / toward rest in one step.</summary>
        public int AccelUnits { get; private set; }

        public int BrakeUnits { get; private set; }

        /// <summary>release-rules-4: desired speed while an accepted attack runs (wind-up through recovery), percent.</summary>
        public int AttackSpeedPercent { get; private set; } = 100;

        public int SwingDamage { get; private set; }

        /// <summary>Accepted attack (or legacy swing) to the next acceptance, in steps.</summary>
        public int SwingIntervalTicks { get; private set; }

        /// <summary>release-rules-4: acceptance to contact in steps (0: legacy, the swing hits on the step it starts).</summary>
        public int WindUpTicks { get; private set; }

        /// <summary>Centre-to-centre swing reach in tiles, and the cosine of half its arc.</summary>
        public double SwingReach { get; private set; }

        public double SwingHalfArcCos { get; private set; }

        /// <summary>Melee enemies engage within this distance at a decision point and hold within the sticky one.</summary>
        public double EngageRange { get; private set; }

        public double StickyRange { get; private set; }

        /// <summary>A melee enemy's hit on the hero lands only within this distance (legacy: the hero's swing reach).</summary>
        public double EnemyHitReach { get; private set; }

        /// <summary>release-rules-4: an enemy this close (bodies touching) is struck whatever the aim; 0 for legacy rules.</summary>
        public double TouchReach { get; private set; }

        /// <summary>
        /// release-rules-4 threat (bounded pursuit): Grunts, Runners and Brutes within <see cref="PursuitStart"/> tiles at a
        /// decision point (or struck by the hero) leave their route and chase for at most <see cref="PursuitTicks"/> steps
        /// while the hero stays within <see cref="PursuitLeash"/>; then they walk back to their cell centre and replan, and
        /// do not start another proximity chase for <see cref="PursuitCooldownTicks"/> (no effect while each enemy may chase
        /// only once a night; kept for a larger budget). 0 disables (legacy rules).
        /// </summary>
        public double PursuitStart { get; private set; }

        public double PursuitLeash { get; private set; }
        public int PursuitTicks { get; private set; }
        public int PursuitCooldownTicks { get; private set; }

        /// <summary>release-rules-4: a struck melee enemy turns on the hero at its next decision within this many steps.</summary>
        public int ProvokeTicks { get; private set; }

        /// <summary>
        /// release-rules-4: how far the hero's collision box of a building sits inside its footprint on every side, in
        /// 1/1200 tile, matching the drawn model (measured from the art; owner playtest 2026-10-21). Walls and the Keep stay
        /// full: connected palisades must never leave a gap. 0 for every kind under legacy rules (whole cells block).
        /// </summary>
        public int BuildingInsetUnits(BuildingKind kind)
        {
            if (!ContactPhase) return 0;
            switch (kind)
            {
                case BuildingKind.Tower:
                case BuildingKind.House: return 84; // 0.86-tile models (a House is 0.76 deep: see HouseDepthInsetUnits)
                case BuildingKind.Watchtower: return 228; // 0.62-tile legs and platform
                case BuildingKind.Farm: return 154; // its fence line, 0.872 tile from the centre of the 2 x 2 field
                case BuildingKind.Cannon: return 0; // the 1 x 1 field gun is drawn shapes: see FieldGunRadiusUnits
                case BuildingKind.RepairStation: return 144; // 1.76 of 2 tiles
                case BuildingKind.Mine: return 84; // its 0.86-tile spoil pad
                default: return 0; // Wall, Keep
            }
        }

        /// <summary>
        /// release-rules-4 1 x 1 Cannon: the gun (wheels, carriage, breech) and its gunner within 0.31 tile of the cell centre; the shot pile
        /// and the powder kegs each within 0.09 tile of a point 0.38 tile from the centre on both axes, in opposite corners.
        /// </summary>
        public const int FieldGunRadiusUnits = 372, FieldGunPropOffsetUnits = 456, FieldGunPropRadiusUnits = 108;

        /// <summary>release-rules-4: a House is 0.76 tile deep (front to back), so that axis is inset more; it turns with the House.</summary>
        public const int HouseDepthInsetUnits = 144;

        /// <summary>
        /// release-rules-4 Walls: a palisade line 0.08 tile thick (half 48 units) from the centre post to the edge of every side
        /// with a neighbouring Wall, so connected Walls stay closed; a lone Wall is a ring of logs (half 108 units).
        /// </summary>
        public const int WallHalfUnits = 48, LoneWallHalfUnits = 108;

        /// <summary>
        /// release-rules-4 water: its edge lies 0.128 tile (154 units) inside a water cell from each land side, where the
        /// drawn bank slopes down to the water line; the hero may stand on that bank.
        /// </summary>
        public const int BankUnits = 154;

        /// <summary>release-rules-4: the hero may swing during Day too (nothing to hit; practice and feel).</summary>
        public bool DayAttacks => ContactPhase;

        /// <summary>release-rules-4: open routes prefer going straight and turning late among equally short routes.</summary>
        public bool TurnLateRoutes => ContactPhase;

        HeroProfile()
        {
        }

        static HeroProfile Legacy(int moveUnits) => new HeroProfile
        {
            RadiusUnits = HeroRules.RadiusUnits, MaxSpeedUnits = moveUnits, SwingDamage = HeroRules.SwingDamage,
            SwingIntervalTicks = HeroRules.SwingIntervalTicks, SwingReach = HeroRules.SwingReach, SwingHalfArcCos = HeroRules.SwingHalfArcCos,
            EngageRange = HeroRules.EngageRange, StickyRange = HeroRules.StickyRange, EnemyHitReach = HeroRules.SwingReach,
        };

        /// <summary>release-rules-2: sprint speed.</summary>
        public static readonly HeroProfile V2 = Legacy(HeroRules.MoveUnitsPerStep);

        /// <summary>release-rules-3: 0.8 tiles/s.</summary>
        public static readonly HeroProfile V3 = Legacy(48);

        /// <summary>
        /// release-rules-4 (game rules section 18) [P values]: a 0.48 m body (72 units), 0.7 tiles/s top speed reached in
        /// three steps and stopped in two, half speed while attacking; 10 damage at 0.225 tile in a 100 degree arc (and any
        /// enemy within 0.1 whatever the aim), contact three steps after acceptance, every 15 steps; melee enemies hit within
        /// 0.225, the hero's own reach (specs/008 D2), and chase the hero once a night (start 1.0, leash 1.5, 4 s; struck:
        /// turn within 2 s), then fight back in place.
        /// </summary>
        public static readonly HeroProfile V4 = new HeroProfile
        {
            ContactPhase = true, RadiusUnits = 72, MaxSpeedUnits = 42, AccelUnits = 14, BrakeUnits = 21, AttackSpeedPercent = 50,
            SwingDamage = 10, SwingIntervalTicks = 15, WindUpTicks = 3, SwingReach = 0.225, TouchReach = 0.15, // spec 014: bodies touching (0.06 + 0.07 + 0.02)
            SwingHalfArcCos = 0.6427876096865394, // cos 50 degrees, written out: no runtime trigonometry in the rules
            EngageRange = 0.2, StickyRange = 0.35, EnemyHitReach = 0.225,
            PursuitStart = 1.0, PursuitLeash = 1.5, PursuitTicks = 80, PursuitCooldownTicks = 40, ProvokeTicks = 40,
        };

        /// <summary>The profile of a hero rules version (null for rules without a hero).</summary>
        public static HeroProfile For(string rulesVersion)
        {
            switch (rulesVersion)
            {
                case ReleaseCampaign.RulesV2: return V2;
                case ReleaseCampaign.RulesV3: return V3;
                case ReleaseCampaign.RulesV4: return V4;
                default: return null;
            }
        }

        /// <summary>
        /// Whether a point (dx, dz) tiles from the hero centre lies inside this profile's swing for a facing; an enemy wider
        /// than a Grunt adds <paramref name="extra"/> to both reaches (spec 014), so its larger body stays in reach.
        /// </summary>
        public bool InSwing(int facing, double dx, double dz, double extra = 0)
        {
            var d2 = dx * dx + dz * dz;
            double reach = SwingReach + extra, touch = TouchReach + extra;
            if (d2 > reach * reach + 1e-9) return false;
            if (d2 < 1e-12 || d2 <= touch * touch + 1e-9) return true; // touching: struck whatever the aim
            var dot = (dx * HeroRules.FacingX[facing] + dz * HeroRules.FacingZ[facing]) / 1024.0;
            return dot >= SwingHalfArcCos * Math.Sqrt(d2) - 1e-9;
        }
    }

    /// <summary>
    /// The hero's movement tick as a pure integer function (game rules section 17; release-rules-4: specs/003 section 3).
    /// The simulation and the view's prediction run the same code: the simulation on the live hero, the prediction on a
    /// copy, so the shown hero can never take a different collision path. Nothing here reads Unity, time or input.
    /// </summary>
    public static class HeroMotion
    {
        /// <summary>Whether a body centred at (x, z) units would overlap a blocked cell.</summary>
        public delegate bool Overlaps(int ux, int uz);

        /// <summary>
        /// The 64 release-rules-4 headings as integer unit vectors scaled to 1024 (0 = north/+Z, clockwise in 5.625 degree
        /// steps). Checked in, so the motor uses no runtime trigonometry; lengths are 1023-1025 and every speed is clamped
        /// after scaling.
        /// </summary>
        public static readonly int[] DirX =
        {
            0, 100, 200, 297, 392, 483, 569, 650, 724, 792, 851, 903, 946, 980, 1004, 1019, 1024, 1019, 1004, 980, 946, 903, 851, 792,
            724, 650, 569, 483, 392, 297, 200, 100, 0, -100, -200, -297, -392, -483, -569, -650, -724, -792, -851, -903, -946, -980,
            -1004, -1019, -1024, -1019, -1004, -980, -946, -903, -851, -792, -724, -650, -569, -483, -392, -297, -200, -100,
        };

        public static readonly int[] DirZ =
        {
            1024, 1019, 1004, 980, 946, 903, 851, 792, 724, 650, 569, 483, 392, 297, 200, 100, 0, -100, -200, -297, -392, -483, -569,
            -650, -724, -792, -851, -903, -946, -980, -1004, -1019, -1024, -1019, -1004, -980, -946, -903, -851, -792, -724, -650,
            -569, -483, -392, -297, -200, -100, 0, 100, 200, 297, 392, 483, 569, 650, 724, 792, 851, 903, 946, 980, 1004, 1019,
        };

        /// <summary>Smallest integer r with r * r &gt;= n (n &gt;= 0).</summary>
        public static long IsqrtCeil(long n)
        {
            if (n <= 0) return 0;
            var r = (long)Math.Sqrt(n);
            while (r * r > n) r--;
            while (r * r < n) r++;
            return r;
        }

        /// <summary>
        /// Scales (x, z) down to length at most <paramref name="max"/>: x * max / ceil(|v|) with division toward zero, so
        /// the result never exceeds the cap after rounding; a vector already within it is unchanged.
        /// </summary>
        public static void ClampLength(ref int x, ref int z, int max)
        {
            var len2 = (long)x * x + (long)z * z;
            if (len2 <= (long)max * max) return;
            var len = IsqrtCeil(len2);
            x = (int)(x * (long)max / len);
            z = (int)(z * (long)max / len);
        }

        /// <summary>
        /// release-rules-4 velocity update for one step: toward heading x speed (zero without movement) by a Euclidean change
        /// of at most the acceleration cap, or the braking cap when the desired velocity is zero; then clamped to the speed
        /// cap. Reversing therefore decelerates through zero instead of flipping.
        /// </summary>
        public static void Accelerate(ref int vx, ref int vz, int heading64, int speed, HeroProfile p)
        {
            int dx = 0, dz = 0;
            if (heading64 >= 0 && speed > 0)
            {
                dx = DirX[heading64] * speed / 1024;
                dz = DirZ[heading64] * speed / 1024;
                ClampLength(ref dx, ref dz, speed);
            }

            var cap = dx == 0 && dz == 0 ? p.BrakeUnits : p.AccelUnits;
            long ex = dx - vx, ez = dz - vz;
            var len2 = ex * ex + ez * ez;
            if (len2 <= (long)cap * cap)
            {
                vx = dx;
                vz = dz;
            }
            else
            {
                var len = IsqrtCeil(len2);
                vx += (int)(ex * cap / len);
                vz += (int)(ez * cap / len);
            }

            ClampLength(ref vx, ref vz, p.MaxSpeedUnits);
        }

        /// <summary>
        /// Largest legal distance (0..|delta|) a body at (ux, uz) can move along one axis before it would overlap a blocked
        /// cell: the whole move if its end is free, else a binary search up to contact. Signed like <paramref name="delta"/>.
        /// </summary>
        public static int LegalAxisMove(int ux, int uz, int delta, bool alongX, Overlaps overlaps)
        {
            if (delta == 0) return 0;
            var sign = Math.Sign(delta);
            var lo = 0;
            var hi = Math.Abs(delta);
            if (!Blocked(ux, uz, sign * hi, alongX, overlaps)) lo = hi;
            else
                while (hi - lo > 1) // largest legal distance up to the wall
                {
                    var mid = (lo + hi) / 2;
                    if (Blocked(ux, uz, sign * mid, alongX, overlaps)) hi = mid;
                    else lo = mid;
                }

            return sign * lo;
        }

        static bool Blocked(int ux, int uz, int d, bool alongX, Overlaps overlaps) => alongX ? overlaps(ux + d, uz) : overlaps(ux, uz + d);

        /// <summary>
        /// One movement step on a position/velocity pair. Legacy profiles: the full move along a 16-direction heading, axis by
        /// axis, no velocity. release-rules-4: accelerate (desired speed scaled while <paramref name="attacking"/>), then
        /// move X then Z by the largest legal part; a blocked component of the velocity becomes zero, the free one slides.
        /// </summary>
        public static void Step(ref int ux, ref int uz, ref int vx, ref int vz, HeroCommand command, bool attacking, HeroProfile p, Overlaps overlaps)
        {
            if (!p.ContactPhase)
            {
                if (!command.Controlled || command.Heading < 0) return;
                // Axis-separated so the body slides along walls; integer units keep it exact.
                ux += LegalAxisMove(ux, uz, HeroRules.HeadingX[command.Heading] * p.MaxSpeedUnits / HeroRules.UnitsPerTile, true, overlaps);
                uz += LegalAxisMove(ux, uz, HeroRules.HeadingZ[command.Heading] * p.MaxSpeedUnits / HeroRules.UnitsPerTile, false, overlaps);
                return;
            }

            var speed = attacking ? p.MaxSpeedUnits * p.AttackSpeedPercent / 100 : p.MaxSpeedUnits;
            Accelerate(ref vx, ref vz, command.Controlled ? command.Heading64 : -1, speed, p);
            var mx = LegalAxisMove(ux, uz, vx, true, overlaps);
            if (mx != vx) vx = 0; // pressing into a wall stores nothing
            ux += mx;
            var mz = LegalAxisMove(ux, uz, vz, false, overlaps);
            if (mz != vz) vz = 0;
            uz += mz;
        }
    }
}
