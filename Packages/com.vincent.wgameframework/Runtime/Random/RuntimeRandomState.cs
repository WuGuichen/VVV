using System;

namespace MxFramework.Runtime
{
    [Serializable]
    public readonly struct RuntimeRandomState
    {
        public RuntimeRandomState(string algorithmId, uint seed, uint state, long drawCount)
        {
            if (string.IsNullOrWhiteSpace(algorithmId))
            {
                throw new ArgumentException("Random algorithm id cannot be null or empty.", nameof(algorithmId));
            }

            if (drawCount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(drawCount), "Random draw count cannot be negative.");
            }

            AlgorithmId = algorithmId;
            Seed = seed;
            State = state;
            DrawCount = drawCount;
        }

        public string AlgorithmId { get; }
        public uint Seed { get; }
        public uint State { get; }
        public long DrawCount { get; }
    }
}
