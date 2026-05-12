using System;

namespace MxFramework.Combat.Core
{
    public readonly struct CombatFrame : IComparable<CombatFrame>, IEquatable<CombatFrame>
    {
        public static readonly CombatFrame Zero = new CombatFrame(0);

        public CombatFrame(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Combat frame cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }

        public CombatFrame Next()
        {
            return Add(1);
        }

        public CombatFrame Add(int frames)
        {
            if (frames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frames), "Combat frame delta cannot be negative.");
            }

            return new CombatFrame(checked(Value + frames));
        }

        public int CompareTo(CombatFrame other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(CombatFrame other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(CombatFrame left, CombatFrame right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CombatFrame left, CombatFrame right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(CombatFrame left, CombatFrame right)
        {
            return left.Value < right.Value;
        }

        public static bool operator >(CombatFrame left, CombatFrame right)
        {
            return left.Value > right.Value;
        }

        public static bool operator <=(CombatFrame left, CombatFrame right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >=(CombatFrame left, CombatFrame right)
        {
            return left.Value >= right.Value;
        }
    }
}
