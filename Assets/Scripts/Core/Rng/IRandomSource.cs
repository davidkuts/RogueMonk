using System.Collections.Generic;

namespace Game.Core.Rng
{
    /// <summary>
    /// The only source of randomness gameplay may use (CLAUDE.md hard rule 5). Everything
    /// draws from a seeded instance owned by the RunContext, so a run can be replayed exactly.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>The seed this source was created from, for logging and reproduction.</summary>
        uint Seed { get; }

        /// <summary>Uniform in [0, 1).</summary>
        float NextFloat();

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Uniform float in [minInclusive, maxExclusive).</summary>
        float NextFloat(float minInclusive, float maxExclusive);

        bool NextBool();
    }

    /// <summary>
    /// xorshift128. Chosen over <see cref="System.Random"/> because System.Random's algorithm
    /// is not contractually stable across .NET versions or platforms, which would silently
    /// break seed reproducibility — the one property this type exists to guarantee.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        uint x, y, z, w;

        public XorShiftRandom(uint seed)
        {
            Seed = seed == 0u ? 0x9E3779B9u : seed; // an all-zero state would lock the generator

            // Scatter the seed across the state with splitmix-style mixing so nearby seeds
            // do not produce visibly similar streams.
            uint s = Seed;
            x = Mix(ref s);
            y = Mix(ref s);
            z = Mix(ref s);
            w = Mix(ref s);
        }

        public uint Seed { get; }

        static uint Mix(ref uint state)
        {
            state += 0x9E3779B9u;
            uint value = state;
            value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
            value = (value ^ (value >> 13)) * 0xC2B2AE35u;
            return value ^ (value >> 16);
        }

        public uint NextUInt()
        {
            uint t = x ^ (x << 11);
            x = y; y = z; z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);
            return w;
        }

        public float NextFloat()
        {
            // 24 bits of mantissa keeps the result strictly below 1.
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            long range = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)(NextFloat() * range));
        }

        public float NextFloat(float minInclusive, float maxExclusive) =>
            minInclusive + NextFloat() * (maxExclusive - minInclusive);

        public bool NextBool() => (NextUInt() & 1u) == 1u;
    }

    /// <summary>Shuffling and weighted picks, all drawing from a supplied source.</summary>
    public static class RandomExtensions
    {
        /// <summary>In-place Fisher-Yates.</summary>
        public static void Shuffle<T>(this IRandomSource random, IList<T> items)
        {
            if (random == null || items == null)
                return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.NextInt(0, i + 1);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }

        /// <summary>
        /// Picks an index proportional to its weight. Returns -1 when nothing has positive
        /// weight, which callers must handle rather than assuming a valid pick.
        /// </summary>
        public static int PickWeighted(this IRandomSource random, IReadOnlyList<float> weights)
        {
            if (random == null || weights == null || weights.Count == 0)
                return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                    total += weights[i];
            }

            if (total <= 0f)
                return -1;

            float roll = random.NextFloat() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                    continue;

                roll -= weights[i];
                if (roll <= 0f)
                    return i;
            }

            // Floating point can leave a sliver; fall back to the last positive weight.
            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0f)
                    return i;
            }

            return -1;
        }
    }
}
