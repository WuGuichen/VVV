using System;

namespace MxFramework.Core.Math
{
    /// <summary>
    /// Minimal deterministic fixed-point value for combat authority logic.
    /// Scale is 1,000,000. Division and multiplication truncate toward zero.
    /// Overflow is intentionally checked so unsafe ranges fail loudly in tests.
    /// </summary>
    public readonly struct Fix64 : IComparable<Fix64>, IEquatable<Fix64>
    {
        public const long Scale = 1000000L;

        public static readonly Fix64 Zero = new Fix64(0);
        public static readonly Fix64 One = new Fix64(Scale);
        public static readonly Fix64 Half = new Fix64(Scale / 2);
        public static readonly Fix64 MinValue = new Fix64(long.MinValue);
        public static readonly Fix64 MaxValue = new Fix64(long.MaxValue);

        private Fix64(long rawValue)
        {
            RawValue = rawValue;
        }

        public long RawValue { get; }

        public bool IsZero => RawValue == 0;

        public static Fix64 FromRaw(long rawValue)
        {
            return new Fix64(rawValue);
        }

        public static Fix64 FromInt(int value)
        {
            return new Fix64(checked(value * Scale));
        }

        public static Fix64 FromLong(long value)
        {
            return new Fix64(checked(value * Scale));
        }

        public static Fix64 FromRatio(long numerator, long denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException();
            }

            return new Fix64(checked(numerator * Scale) / denominator);
        }

        public int ToInt()
        {
            return checked((int)(RawValue / Scale));
        }

        public Fix64 Abs()
        {
            if (RawValue == long.MinValue)
            {
                throw new OverflowException("Cannot get absolute value of Fix64.MinValue.");
            }

            return RawValue < 0 ? new Fix64(-RawValue) : this;
        }

        public Fix64 Sqrt()
        {
            if (RawValue < 0)
            {
                throw new InvalidOperationException("Cannot calculate square root of a negative fixed-point value.");
            }

            ulong scaled = checked((ulong)RawValue * (ulong)Scale);
            return new Fix64(checked((long)IntegerSqrt(scaled)));
        }

        public int CompareTo(Fix64 other)
        {
            return RawValue.CompareTo(other.RawValue);
        }

        public bool Equals(Fix64 other)
        {
            return RawValue == other.RawValue;
        }

        public override bool Equals(object obj)
        {
            return obj is Fix64 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public override string ToString()
        {
            long whole = RawValue / Scale;
            long fraction = RawValue % Scale;
            if (fraction < 0)
            {
                fraction = -fraction;
            }

            if (RawValue < 0 && whole == 0)
            {
                return $"-0.{fraction:D6}";
            }

            return $"{whole}.{fraction:D6}";
        }

        public static Fix64 Min(Fix64 left, Fix64 right)
        {
            return left.RawValue <= right.RawValue ? left : right;
        }

        public static Fix64 Max(Fix64 left, Fix64 right)
        {
            return left.RawValue >= right.RawValue ? left : right;
        }

        public static Fix64 Clamp(Fix64 value, Fix64 min, Fix64 max)
        {
            if (min > max)
            {
                throw new ArgumentException("Min cannot be greater than max.", nameof(min));
            }

            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        public static Fix64 operator +(Fix64 left, Fix64 right)
        {
            return new Fix64(checked(left.RawValue + right.RawValue));
        }

        public static Fix64 operator -(Fix64 left, Fix64 right)
        {
            return new Fix64(checked(left.RawValue - right.RawValue));
        }

        public static Fix64 operator -(Fix64 value)
        {
            return new Fix64(checked(-value.RawValue));
        }

        public static Fix64 operator *(Fix64 left, Fix64 right)
        {
            return new Fix64(checked(left.RawValue * right.RawValue) / Scale);
        }

        public static Fix64 operator /(Fix64 left, Fix64 right)
        {
            if (right.RawValue == 0)
            {
                throw new DivideByZeroException();
            }

            return new Fix64(checked(left.RawValue * Scale) / right.RawValue);
        }

        public static bool operator ==(Fix64 left, Fix64 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Fix64 left, Fix64 right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(Fix64 left, Fix64 right)
        {
            return left.RawValue < right.RawValue;
        }

        public static bool operator >(Fix64 left, Fix64 right)
        {
            return left.RawValue > right.RawValue;
        }

        public static bool operator <=(Fix64 left, Fix64 right)
        {
            return left.RawValue <= right.RawValue;
        }

        public static bool operator >=(Fix64 left, Fix64 right)
        {
            return left.RawValue >= right.RawValue;
        }

        private static ulong IntegerSqrt(ulong value)
        {
            ulong result = 0;
            ulong bit = 1UL << 62;

            while (bit > value)
            {
                bit >>= 2;
            }

            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }

                bit >>= 2;
            }

            return result;
        }
    }
}
