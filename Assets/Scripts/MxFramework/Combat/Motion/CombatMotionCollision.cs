using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionCollision : IEquatable<CombatMotionCollision>
    {
        public CombatMotionCollision(
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            FixVector3 normal,
            Fix64 distance,
            Fix64 fraction,
            CombatMotionCollisionFlags flags)
        {
            if (distance < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Motion collision distance cannot be negative.");
            }

            if (fraction < Fix64.Zero || fraction > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), "Motion collision fraction must be in range 0..1.");
            }

            TargetEntityId = targetEntityId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            Normal = normal;
            Distance = distance;
            Fraction = fraction;
            Flags = flags;
        }

        public CombatEntityId TargetEntityId { get; }

        public CombatBodyId TargetBodyId { get; }

        public CombatColliderId TargetColliderId { get; }

        public FixVector3 Normal { get; }

        public Fix64 Distance { get; }

        public Fix64 Fraction { get; }

        public CombatMotionCollisionFlags Flags { get; }

        public bool Equals(CombatMotionCollision other)
        {
            return TargetEntityId.Equals(other.TargetEntityId)
                && TargetBodyId.Equals(other.TargetBodyId)
                && TargetColliderId.Equals(other.TargetColliderId)
                && Normal.Equals(other.Normal)
                && Distance.Equals(other.Distance)
                && Fraction.Equals(other.Fraction)
                && Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMotionCollision other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TargetEntityId.GetHashCode();
                hash = (hash * 397) ^ TargetBodyId.GetHashCode();
                hash = (hash * 397) ^ TargetColliderId.GetHashCode();
                hash = (hash * 397) ^ Normal.GetHashCode();
                hash = (hash * 397) ^ Distance.GetHashCode();
                hash = (hash * 397) ^ Fraction.GetHashCode();
                hash = (hash * 397) ^ (int)Flags;
                return hash;
            }
        }
    }
}
