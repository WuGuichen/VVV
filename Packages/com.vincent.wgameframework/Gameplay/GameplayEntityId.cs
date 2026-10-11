using System;

namespace MxFramework.Gameplay
{
    /// <summary>Generation-based gameplay entity identity for component runtime stores.</summary>
    public readonly struct GameplayEntityId : IComparable<GameplayEntityId>, IEquatable<GameplayEntityId>
    {
        public GameplayEntityId(int index, int generation)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Gameplay entity index cannot be negative.");
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation), "Gameplay entity generation cannot be negative.");
            if ((index == 0) != (generation == 0))
                throw new ArgumentException("Gameplay entity id must be either default or have both index and generation greater than zero.");

            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index > 0 && Generation > 0;

        public int CompareTo(GameplayEntityId other)
        {
            int indexComparison = Index.CompareTo(other.Index);
            return indexComparison != 0 ? indexComparison : Generation.CompareTo(other.Generation);
        }

        public bool Equals(GameplayEntityId other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayEntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Generation;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"{Index}:{Generation}" : "Invalid";
        }

        public static bool operator ==(GameplayEntityId left, GameplayEntityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayEntityId left, GameplayEntityId right)
        {
            return !left.Equals(right);
        }
    }
}
