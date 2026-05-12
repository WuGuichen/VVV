using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatRayQuery
    {
        public CombatRayQuery(CombatQueryHeader header, FixVector3 origin, FixVector3 direction, Fix64 length)
        {
            if (header.Kind != CombatQueryKind.Ray)
            {
                throw new ArgumentException("Query header kind must be Ray.", nameof(header));
            }

            if (direction.IsZero)
            {
                throw new ArgumentException("Ray direction cannot be zero.", nameof(direction));
            }

            if (length < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Ray length cannot be negative.");
            }

            Header = header;
            Origin = origin;
            Direction = direction;
            Length = length;
        }

        public CombatQueryHeader Header { get; }

        public FixVector3 Origin { get; }

        public FixVector3 Direction { get; }

        public Fix64 Length { get; }
    }

    public readonly struct CombatSphereQuery
    {
        public CombatSphereQuery(CombatQueryHeader header, FixVector3 center, Fix64 radius)
        {
            if (header.Kind != CombatQueryKind.Sphere)
            {
                throw new ArgumentException("Query header kind must be Sphere.", nameof(header));
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere radius cannot be negative.");
            }

            Header = header;
            Center = center;
            Radius = radius;
        }

        public CombatQueryHeader Header { get; }

        public FixVector3 Center { get; }

        public Fix64 Radius { get; }
    }

    public readonly struct CombatCapsuleQuery
    {
        public CombatCapsuleQuery(CombatQueryHeader header, FixVector3 pointA, FixVector3 pointB, Fix64 radius)
        {
            if (header.Kind != CombatQueryKind.Capsule)
            {
                throw new ArgumentException("Query header kind must be Capsule.", nameof(header));
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Capsule radius cannot be negative.");
            }

            Header = header;
            PointA = pointA;
            PointB = pointB;
            Radius = radius;
        }

        public CombatQueryHeader Header { get; }

        public FixVector3 PointA { get; }

        public FixVector3 PointB { get; }

        public Fix64 Radius { get; }
    }

    public readonly struct CombatAabbQuery
    {
        public CombatAabbQuery(CombatQueryHeader header, FixVector3 min, FixVector3 max)
        {
            if (header.Kind != CombatQueryKind.Aabb)
            {
                throw new ArgumentException("Query header kind must be Aabb.", nameof(header));
            }

            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                throw new ArgumentException("AABB min cannot be greater than max.");
            }

            Header = header;
            Min = min;
            Max = max;
        }

        public CombatQueryHeader Header { get; }

        public FixVector3 Min { get; }

        public FixVector3 Max { get; }
    }

    public readonly struct CombatSectorQuery
    {
        public CombatSectorQuery(
            CombatQueryHeader header,
            FixVector3 origin,
            FixVector3 direction,
            Fix64 radius,
            Fix64 minDot)
        {
            if (header.Kind != CombatQueryKind.Sector)
            {
                throw new ArgumentException("Query header kind must be Sector.", nameof(header));
            }

            if (direction.IsZero)
            {
                throw new ArgumentException("Sector direction cannot be zero.", nameof(direction));
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sector radius cannot be negative.");
            }

            if (minDot < -Fix64.One || minDot > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(minDot), "Sector min dot must be in range -1..1.");
            }

            Header = header;
            Origin = origin;
            Direction = direction;
            Radius = radius;
            MinDot = minDot;
        }

        public CombatQueryHeader Header { get; }

        public FixVector3 Origin { get; }

        public FixVector3 Direction { get; }

        public Fix64 Radius { get; }

        public Fix64 MinDot { get; }
    }
}
