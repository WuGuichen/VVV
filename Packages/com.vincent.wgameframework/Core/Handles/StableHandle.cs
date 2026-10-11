using System;

namespace MxFramework.Core.Handles
{
    public readonly struct StableHandle : IEquatable<StableHandle>
    {
        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0 && Generation > 0;

        public StableHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public bool Equals(StableHandle other) => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is StableHandle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Generation;
            }
        }

        public override string ToString()
        {
            return IsValid
                ? $"StableHandle(Index={Index}, Generation={Generation})"
                : "StableHandle.Invalid";
        }

        public static bool operator ==(StableHandle left, StableHandle right) => left.Equals(right);

        public static bool operator !=(StableHandle left, StableHandle right) => !left.Equals(right);
    }
}
