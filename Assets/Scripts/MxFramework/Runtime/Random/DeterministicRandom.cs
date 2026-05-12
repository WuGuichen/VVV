using System;

namespace MxFramework.Runtime
{
    public sealed class DeterministicRandom : IDeterministicRandom
    {
        public const string XorShift32AlgorithmId = "MxFramework.Runtime.Random.XorShift32.v1";

        private const uint ZeroSeedState = 0x6D2B79F5u;
        private const float OneOverTwentyFourBits = 1f / 16777216f;

        private uint _seed;
        private uint _state;
        private long _drawCount;

        public DeterministicRandom(uint seed)
        {
            Reset(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "maxExclusive must be greater than minInclusive.");
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            ulong bucketSize = (uint.MaxValue + 1UL) / range;
            ulong acceptedValueLimit = bucketSize * range;

            uint value;
            do
            {
                value = NextUInt32();
            }
            while (value >= acceptedValueLimit);

            long offset = (long)(value / bucketSize);
            return (int)(minInclusive + offset);
        }

        public float NextFloat01()
        {
            return (NextUInt32() >> 8) * OneOverTwentyFourBits;
        }

        public bool Chance(float probability)
        {
            if (float.IsNaN(probability) || probability < 0f || probability > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(probability),
                    "Probability must be in the inclusive range [0, 1].");
            }

            if (probability <= 0f)
            {
                return false;
            }

            if (probability >= 1f)
            {
                return true;
            }

            return NextFloat01() < probability;
        }

        public void Reset(uint seed)
        {
            _seed = seed;
            _state = NormalizeSeed(seed);
            _drawCount = 0L;
        }

        public RuntimeRandomState CaptureState()
        {
            return new RuntimeRandomState(XorShift32AlgorithmId, _seed, _state, _drawCount);
        }

        public void RestoreState(RuntimeRandomState state)
        {
            if (!string.Equals(state.AlgorithmId, XorShift32AlgorithmId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Random state algorithm id is not supported by this random source.",
                    nameof(state));
            }

            if (state.State == 0u)
            {
                throw new ArgumentException("XorShift32 random state cannot be zero.", nameof(state));
            }

            if (state.DrawCount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(state), "Random draw count cannot be negative.");
            }

            _seed = state.Seed;
            _state = state.State;
            _drawCount = state.DrawCount;
        }

        private uint NextUInt32()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            _drawCount++;
            return x;
        }

        private static uint NormalizeSeed(uint seed)
        {
            return seed == 0u ? ZeroSeedState : seed;
        }
    }
}
