using System;

namespace MxFramework.Core.Math
{
    public readonly struct FixVector3 : IEquatable<FixVector3>
    {
        public static readonly FixVector3 Zero = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Zero);

        public FixVector3(Fix64 x, Fix64 y, Fix64 z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Fix64 X { get; }

        public Fix64 Y { get; }

        public Fix64 Z { get; }

        public bool IsZero => X.IsZero && Y.IsZero && Z.IsZero;

        public Fix64 LengthSquared()
        {
            return Dot(this);
        }

        public Fix64 Dot(FixVector3 other)
        {
            return (X * other.X) + (Y * other.Y) + (Z * other.Z);
        }

        public bool TryNormalize(out FixVector3 normalized)
        {
            Fix64 lengthSquared = LengthSquared();
            if (lengthSquared.IsZero)
            {
                normalized = Zero;
                return false;
            }

            Fix64 length = lengthSquared.Sqrt();
            normalized = this / length;
            return true;
        }

        public bool Equals(FixVector3 other)
        {
            return X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is FixVector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public static FixVector3 operator +(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static FixVector3 operator -(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static FixVector3 operator -(FixVector3 value)
        {
            return new FixVector3(-value.X, -value.Y, -value.Z);
        }

        public static FixVector3 operator *(FixVector3 value, Fix64 scalar)
        {
            return new FixVector3(value.X * scalar, value.Y * scalar, value.Z * scalar);
        }

        public static FixVector3 operator *(Fix64 scalar, FixVector3 value)
        {
            return value * scalar;
        }

        public static FixVector3 operator /(FixVector3 value, Fix64 scalar)
        {
            return new FixVector3(value.X / scalar, value.Y / scalar, value.Z / scalar);
        }

        public static bool operator ==(FixVector3 left, FixVector3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FixVector3 left, FixVector3 right)
        {
            return !left.Equals(right);
        }
    }
}
