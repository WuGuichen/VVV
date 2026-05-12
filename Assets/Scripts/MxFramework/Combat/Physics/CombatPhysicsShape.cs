using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public enum CombatPhysicsShapeKind
    {
        Ray = 1,
        Sphere = 2,
        Capsule = 3,
        Aabb = 4,
        Sector = 5,
        Obb = 6,
    }

    public readonly struct CombatPhysicsShape : IEquatable<CombatPhysicsShape>
    {
        private CombatPhysicsShape(
            CombatPhysicsShapeKind kind,
            FixVector3 origin,
            FixVector3 direction,
            Fix64 length,
            FixVector3 center,
            Fix64 radius,
            FixVector3 pointA,
            FixVector3 pointB,
            FixVector3 min,
            FixVector3 max,
            Fix64 minDot,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ)
        {
            Kind = kind;
            Origin = origin;
            Direction = direction;
            Length = length;
            Center = center;
            Radius = radius;
            PointA = pointA;
            PointB = pointB;
            Min = min;
            Max = max;
            MinDot = minDot;
            HalfExtents = halfExtents;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
        }

        public CombatPhysicsShapeKind Kind { get; }

        public FixVector3 Origin { get; }

        public FixVector3 Direction { get; }

        public Fix64 Length { get; }

        public FixVector3 Center { get; }

        public Fix64 Radius { get; }

        public FixVector3 PointA { get; }

        public FixVector3 PointB { get; }

        public FixVector3 Min { get; }

        public FixVector3 Max { get; }

        public Fix64 MinDot { get; }

        public FixVector3 HalfExtents { get; }

        public FixVector3 AxisX { get; }

        public FixVector3 AxisY { get; }

        public FixVector3 AxisZ { get; }

        public static CombatPhysicsShape Ray(FixVector3 origin, FixVector3 direction, Fix64 length)
        {
            if (direction.IsZero)
            {
                throw new ArgumentException("Ray shape direction cannot be zero.", nameof(direction));
            }

            if (length < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Ray shape length cannot be negative.");
            }

            return new CombatPhysicsShape(
                CombatPhysicsShapeKind.Ray,
                origin,
                direction,
                length,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero);
        }

        public static CombatPhysicsShape Sphere(FixVector3 center, Fix64 radius)
        {
            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere shape radius cannot be negative.");
            }

            return new CombatPhysicsShape(
                CombatPhysicsShapeKind.Sphere,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                center,
                radius,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero);
        }

        public static CombatPhysicsShape Capsule(FixVector3 pointA, FixVector3 pointB, Fix64 radius)
        {
            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Capsule shape radius cannot be negative.");
            }

            return new CombatPhysicsShape(
                CombatPhysicsShapeKind.Capsule,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                radius,
                pointA,
                pointB,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero);
        }

        public static CombatPhysicsShape Aabb(FixVector3 min, FixVector3 max)
        {
            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                throw new ArgumentException("AABB shape min cannot be greater than max.");
            }

            return new CombatPhysicsShape(
                CombatPhysicsShapeKind.Aabb,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                min,
                max,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero);
        }

        public static CombatPhysicsShape Sector(FixVector3 origin, FixVector3 direction, Fix64 radius, Fix64 minDot)
        {
            if (direction.IsZero)
            {
                throw new ArgumentException("Sector shape direction cannot be zero.", nameof(direction));
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sector shape radius cannot be negative.");
            }

            if (minDot < -Fix64.One || minDot > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(minDot), "Sector shape min dot must be in range -1..1.");
            }

            return new CombatPhysicsShape(
                CombatPhysicsShapeKind.Sector,
                origin,
                direction,
                Fix64.Zero,
                FixVector3.Zero,
                radius,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                minDot,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero);
        }

        public static CombatPhysicsShape Obb(
            FixVector3 center,
            FixVector3 halfExtents,
            FixVector3 axisX,
            FixVector3 axisY,
            FixVector3 axisZ)
        {
            if (halfExtents.X < Fix64.Zero || halfExtents.Y < Fix64.Zero || halfExtents.Z < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), "OBB shape half extents cannot be negative.");
            }

            if (axisX.IsZero || axisY.IsZero || axisZ.IsZero)
            {
                throw new ArgumentException("OBB shape axes cannot be zero.");
            }

            return new CombatPhysicsShape(
                CombatPhysicsShapeKind.Obb,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                center,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero,
                halfExtents,
                axisX,
                axisY,
                axisZ);
        }

        public bool Equals(CombatPhysicsShape other)
        {
            return Kind == other.Kind
                && Origin.Equals(other.Origin)
                && Direction.Equals(other.Direction)
                && Length.Equals(other.Length)
                && Center.Equals(other.Center)
                && Radius.Equals(other.Radius)
                && PointA.Equals(other.PointA)
                && PointB.Equals(other.PointB)
                && Min.Equals(other.Min)
                && Max.Equals(other.Max)
                && MinDot.Equals(other.MinDot)
                && HalfExtents.Equals(other.HalfExtents)
                && AxisX.Equals(other.AxisX)
                && AxisY.Equals(other.AxisY)
                && AxisZ.Equals(other.AxisZ);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsShape other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ Length.GetHashCode();
                hash = (hash * 397) ^ Center.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ PointA.GetHashCode();
                hash = (hash * 397) ^ PointB.GetHashCode();
                hash = (hash * 397) ^ Min.GetHashCode();
                hash = (hash * 397) ^ Max.GetHashCode();
                hash = (hash * 397) ^ MinDot.GetHashCode();
                hash = (hash * 397) ^ HalfExtents.GetHashCode();
                hash = (hash * 397) ^ AxisX.GetHashCode();
                hash = (hash * 397) ^ AxisY.GetHashCode();
                hash = (hash * 397) ^ AxisZ.GetHashCode();
                return hash;
            }
        }
    }
}
