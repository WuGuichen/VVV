using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayStatusId : IComparable<GameplayStatusId>, IEquatable<GameplayStatusId>
    {
        public static readonly GameplayStatusId None = new GameplayStatusId(0);

        public GameplayStatusId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Gameplay status id cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }
        public bool IsNone => Value == 0;
        public bool IsValid => Value > 0;

        public int CompareTo(GameplayStatusId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(GameplayStatusId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayStatusId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
