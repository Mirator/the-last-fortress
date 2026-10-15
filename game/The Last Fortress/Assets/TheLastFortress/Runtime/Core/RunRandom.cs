namespace TheLastFortress
{
    /// <summary>
    /// Locked deterministic xorshift32 stream (plans 004/005). Never use UnityEngine.Random, System.Random or string
    /// hashes for gameplay. Each stream owns its state; persisting <see cref="State"/> resumes it exactly.
    /// </summary>
    public sealed class RunRandom
    {
        public const uint ZeroFallback = 0x6D2B79F5;
        public const uint RewardSalt = 0xC8013EA4;
        public const uint MapSalt = 0xA341316C;
        public const uint MapAttemptStep = 0x9E3779B9;

        public uint State { get; private set; }

        public RunRandom(uint state)
        {
            State = state == 0 ? ZeroFallback : state;
        }

        public static RunRandom ForRewards(uint seed) => new RunRandom(seed ^ RewardSalt);

        public static RunRandom ForMapAttempt(uint seed, int attemptIndex) =>
            new RunRandom(unchecked(seed ^ MapSalt ^ ((uint)attemptIndex * MapAttemptStep)));

        public uint Next()
        {
            unchecked
            {
                var x = State;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                State = x;
                return x;
            }
        }

        /// <summary>Uniform value in [0, n) by rejection: words below (2^32 - n) % n are discarded.</summary>
        public uint NextBounded(uint n)
        {
            if (n == 0) throw new System.ArgumentOutOfRangeException(nameof(n));
            var threshold = unchecked(0u - n) % n;
            while (true)
            {
                var word = Next();
                if (word >= threshold) return word % n;
            }
        }
    }
}
