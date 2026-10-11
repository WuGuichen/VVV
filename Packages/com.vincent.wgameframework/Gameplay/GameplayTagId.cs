using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayTagId : IComparable<GameplayTagId>, IEquatable<GameplayTagId>
    {
        public static readonly GameplayTagId None = new GameplayTagId(0);

        public GameplayTagId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Gameplay tag id cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }
        public bool IsNone => Value == 0;
        public bool IsValid => Value > 0;

        public int CompareTo(GameplayTagId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(GameplayTagId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTagId other && Equals(other);
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
