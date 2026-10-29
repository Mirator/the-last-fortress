namespace TheLastFortress
{
    /// <summary>Spec 014 (release-rules-4) unit collision constants [P].</summary>
    public static class UnitBodies
    {
        /// <summary>Sideways lanes from the route centreline (tiles), chosen by enemy ID; 0.14 apart, so two 0.07 bodies fit side by side.</summary>
        public static readonly double[] Lanes = { -0.28, -0.14, 0.0, 0.14, 0.28 };

        /// <summary>Steps a body or a spawn waits before it may pass through bodies (4 s).</summary>
        public const int WaitTicks = 80;

        /// <summary>Units a long-blocked body may then walk through other bodies (half a tile).</summary>
        public const int PassUnits = 600;

        /// <summary>Most the hero is pushed out of enemy bodies in one step (0.1 tile, units).</summary>
        public const int HeroPushUnits = 120;

        /// <summary>Body radius of the smallest enemies; larger bodies extend reach by the difference.</summary>
        public const double BaseRadius = 0.07;

        /// <summary>Extra reach to (and from) an enemy whose body is wider than a Grunt's.</summary>
        public static double ExtraReach(EnemyDefinition d) => d.BodyRadius > BaseRadius ? d.BodyRadius - BaseRadius : 0;
    }
}
