using System;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public readonly struct WeaponTraceFrame : IEquatable<WeaponTraceFrame>
    {
        public WeaponTraceFrame(
            int traceId,
            FixVector3 rootPrev,
            FixVector3 tipPrev,
            FixVector3 rootNow,
            FixVector3 tipNow,
            Fix64 radius,
            CombatPhysicsLayerMask targetMask)
        {
            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Weapon trace radius cannot be negative.");
            }

            TraceId = traceId;
            RootPrev = rootPrev;
            TipPrev = tipPrev;
            RootNow = rootNow;
            TipNow = tipNow;
            Radius = radius;
            TargetMask = targetMask;
        }

        public int TraceId { get; }

        public FixVector3 RootPrev { get; }

        public FixVector3 TipPrev { get; }

        public FixVector3 RootNow { get; }

        public FixVector3 TipNow { get; }

        public Fix64 Radius { get; }

        public CombatPhysicsLayerMask TargetMask { get; }

        public Fix64 TipDeltaLengthSquared()
        {
            return (TipNow - TipPrev).LengthSquared();
        }

        public bool Equals(WeaponTraceFrame other)
        {
            return TraceId == other.TraceId
                && RootPrev.Equals(other.RootPrev)
                && TipPrev.Equals(other.TipPrev)
                && RootNow.Equals(other.RootNow)
                && TipNow.Equals(other.TipNow)
                && Radius.Equals(other.Radius)
                && TargetMask.Equals(other.TargetMask);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponTraceFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TraceId;
                hash = (hash * 397) ^ RootPrev.GetHashCode();
                hash = (hash * 397) ^ TipPrev.GetHashCode();
                hash = (hash * 397) ^ RootNow.GetHashCode();
                hash = (hash * 397) ^ TipNow.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ TargetMask.GetHashCode();
                return hash;
            }
        }
    }
}
