using System;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionCapsuleProxy : IEquatable<CombatMotionCapsuleProxy>
    {
        public CombatMotionCapsuleProxy(
            Fix64 radius,
            Fix64 height,
            FixVector3 center,
            Fix64 skinWidth,
            int layer,
            CombatPhysicsLayerMask collisionMask)
        {
            if (radius <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Capsule radius must be positive.");
            }

            if (height < radius * Fix64.FromInt(2))
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Capsule height cannot be smaller than diameter.");
            }

            if (skinWidth < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth), "Capsule skin width cannot be negative.");
            }

            Radius = radius;
            Height = height;
            Center = center;
            SkinWidth = skinWidth;
            Layer = layer;
            CollisionMask = collisionMask;
        }

        public Fix64 Radius { get; }

        public Fix64 Height { get; }

        public FixVector3 Center { get; }

        public Fix64 SkinWidth { get; }

        public int Layer { get; }

        public CombatPhysicsLayerMask CollisionMask { get; }

        public Fix64 HalfHeight => Height / Fix64.FromInt(2);

        public Fix64 SegmentHalfLength => HalfHeight - Radius;

        public FixVector3 HalfExtents => new FixVector3(Radius, HalfHeight, Radius);

        public FixVector3 GetWorldCenter(FixVector3 position)
        {
            return position + Center;
        }

        public void GetSegment(FixVector3 position, out FixVector3 pointA, out FixVector3 pointB)
        {
            FixVector3 center = GetWorldCenter(position);
            Fix64 halfSegment = SegmentHalfLength;
            pointA = center + new FixVector3(Fix64.Zero, -halfSegment, Fix64.Zero);
            pointB = center + new FixVector3(Fix64.Zero, halfSegment, Fix64.Zero);
        }

        public void GetBounds(FixVector3 position, out FixVector3 min, out FixVector3 max)
        {
            FixVector3 center = GetWorldCenter(position);
            FixVector3 halfExtents = HalfExtents;
            min = center - halfExtents;
            max = center + halfExtents;
        }

        public static CombatMotionCapsuleProxy FromHalfExtents(
            FixVector3 halfExtents,
            Fix64 skinWidth,
            int layer,
            CombatPhysicsLayerMask collisionMask)
        {
            Fix64 radius = Fix64.Min(halfExtents.X, halfExtents.Z);
            Fix64 height = halfExtents.Y * Fix64.FromInt(2);
            return new CombatMotionCapsuleProxy(radius, height, FixVector3.Zero, skinWidth, layer, collisionMask);
        }

        public bool Equals(CombatMotionCapsuleProxy other)
        {
            return Radius.Equals(other.Radius)
                && Height.Equals(other.Height)
                && Center.Equals(other.Center)
                && SkinWidth.Equals(other.SkinWidth)
                && Layer == other.Layer
                && CollisionMask.Equals(other.CollisionMask);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMotionCapsuleProxy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Radius.GetHashCode();
                hash = (hash * 397) ^ Height.GetHashCode();
                hash = (hash * 397) ^ Center.GetHashCode();
                hash = (hash * 397) ^ SkinWidth.GetHashCode();
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ CollisionMask.GetHashCode();
                return hash;
            }
        }
    }
}
