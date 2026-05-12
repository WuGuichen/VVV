using System;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public readonly struct WeaponTraceSegment : IEquatable<WeaponTraceSegment>
    {
        public WeaponTraceSegment(
            int traceId,
            int segmentIndex,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            CombatPhysicsLayerMask targetMask)
        {
            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");
            }

            if (segmentIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentIndex), "Segment index cannot be negative.");
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Weapon trace radius cannot be negative.");
            }

            TraceId = traceId;
            SegmentIndex = segmentIndex;
            PointA = pointA;
            PointB = pointB;
            Radius = radius;
            TargetMask = targetMask;
        }

        public int TraceId { get; }

        public int SegmentIndex { get; }

        public FixVector3 PointA { get; }

        public FixVector3 PointB { get; }

        public Fix64 Radius { get; }

        public CombatPhysicsLayerMask TargetMask { get; }

        public bool Equals(WeaponTraceSegment other)
        {
            return TraceId == other.TraceId
                && SegmentIndex == other.SegmentIndex
                && PointA.Equals(other.PointA)
                && PointB.Equals(other.PointB)
                && Radius.Equals(other.Radius)
                && TargetMask.Equals(other.TargetMask);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponTraceSegment other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TraceId;
                hash = (hash * 397) ^ SegmentIndex;
                hash = (hash * 397) ^ PointA.GetHashCode();
                hash = (hash * 397) ^ PointB.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ TargetMask.GetHashCode();
                return hash;
            }
        }
    }
}
