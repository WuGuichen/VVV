using System;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeFrame : IComparable<RuntimeFrame>, IEquatable<RuntimeFrame>
    {
        public RuntimeFrame(long value)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Runtime frame cannot be negative.");
            }

            Value = value;
        }

        public long Value { get; }

        public static RuntimeFrame Zero => new RuntimeFrame(0L);

        public RuntimeFrame Next()
        {
            if (Value == long.MaxValue)
            {
                throw new InvalidOperationException("Runtime frame cannot advance beyond long.MaxValue.");
            }

            return new RuntimeFrame(Value + 1L);
        }

        public int CompareTo(RuntimeFrame other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(RuntimeFrame other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(RuntimeFrame left, RuntimeFrame right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeFrame left, RuntimeFrame right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(RuntimeFrame left, RuntimeFrame right)
        {
            return left.Value < right.Value;
        }

        public static bool operator >(RuntimeFrame left, RuntimeFrame right)
        {
            return left.Value > right.Value;
        }

        public static bool operator <=(RuntimeFrame left, RuntimeFrame right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >=(RuntimeFrame left, RuntimeFrame right)
        {
            return left.Value >= right.Value;
        }
    }
}
