using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Hit
{
    public readonly struct HitResolveResult : IEquatable<HitResolveResult>
    {
        public HitResolveResult(
            CombatEntityId attackerId,
            CombatEntityId targetId,
            int actionId,
            int actionInstanceId,
            int traceId,
            CombatFrame frame,
            HitResolveKind kind,
            int damage,
            int staggerFrames,
            FixVector3 knockback)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Damage cannot be negative.");
            }

            if (staggerFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(staggerFrames), "Stagger frames cannot be negative.");
            }

            AttackerId = attackerId;
            TargetId = targetId;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            TraceId = traceId;
            Frame = frame;
            Kind = kind;
            Damage = damage;
            StaggerFrames = staggerFrames;
            Knockback = knockback;
        }

        public CombatEntityId AttackerId { get; }

        public CombatEntityId TargetId { get; }

        public int ActionId { get; }

        public int ActionInstanceId { get; }

        public int TraceId { get; }

        public CombatFrame Frame { get; }

        public HitResolveKind Kind { get; }

        public int Damage { get; }

        public int StaggerFrames { get; }

        public FixVector3 Knockback { get; }

        public bool IsAcceptedDamage => Kind == HitResolveKind.Damage && Damage > 0;

        public bool Equals(HitResolveResult other)
        {
            return AttackerId.Equals(other.AttackerId)
                && TargetId.Equals(other.TargetId)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && Frame.Equals(other.Frame)
                && Kind == other.Kind
                && Damage == other.Damage
                && StaggerFrames == other.StaggerFrames
                && Knockback.Equals(other.Knockback);
        }

        public override bool Equals(object obj)
        {
            return obj is HitResolveResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttackerId.Value;
                hash = (hash * 397) ^ TargetId.Value;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ Frame.Value;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Damage;
                hash = (hash * 397) ^ StaggerFrames;
                hash = (hash * 397) ^ Knockback.GetHashCode();
                return hash;
            }
        }
    }
}
