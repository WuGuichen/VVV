using System;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Hit
{
    public readonly struct HitCandidate : IComparable<HitCandidate>, IEquatable<HitCandidate>
    {
        public HitCandidate(
            CombatEntityId attackerId,
            CombatEntityId targetId,
            int actionId,
            int actionInstanceId,
            int traceId,
            CombatFrame frame,
            CombatQueryResult physicsHit,
            int damage,
            int staggerFrames,
            FixVector3 knockback,
            HitTargetStateFlags targetState,
            int resolvePriority = 0)
        {
            if (actionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Action id cannot be negative.");
            }

            if (actionInstanceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionInstanceId), "Action instance id cannot be negative.");
            }

            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");
            }

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
            PhysicsHit = physicsHit;
            Damage = damage;
            StaggerFrames = staggerFrames;
            Knockback = knockback;
            TargetState = targetState;
            ResolvePriority = resolvePriority;
        }

        public CombatEntityId AttackerId { get; }

        public CombatEntityId TargetId { get; }

        public int ActionId { get; }

        public int ActionInstanceId { get; }

        public int TraceId { get; }

        public CombatFrame Frame { get; }

        public CombatQueryResult PhysicsHit { get; }

        public int Damage { get; }

        public int StaggerFrames { get; }

        public FixVector3 Knockback { get; }

        public HitTargetStateFlags TargetState { get; }

        public int ResolvePriority { get; }

        public WeaponHitOnceKey HitOnceKey => new WeaponHitOnceKey(ActionInstanceId, TraceId, TargetId);

        public int CompareTo(HitCandidate other)
        {
            int compare = other.ResolvePriority.CompareTo(ResolvePriority);
            if (compare != 0)
            {
                return compare;
            }

            compare = PhysicsHit.CompareTo(other.PhysicsHit);
            if (compare != 0)
            {
                return compare;
            }

            compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            return ActionInstanceId.CompareTo(other.ActionInstanceId);
        }

        public bool Equals(HitCandidate other)
        {
            return AttackerId.Equals(other.AttackerId)
                && TargetId.Equals(other.TargetId)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && Frame.Equals(other.Frame)
                && PhysicsHit.Equals(other.PhysicsHit)
                && Damage == other.Damage
                && StaggerFrames == other.StaggerFrames
                && Knockback.Equals(other.Knockback)
                && TargetState == other.TargetState
                && ResolvePriority == other.ResolvePriority;
        }

        public override bool Equals(object obj)
        {
            return obj is HitCandidate other && Equals(other);
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
                hash = (hash * 397) ^ PhysicsHit.GetHashCode();
                hash = (hash * 397) ^ Damage;
                hash = (hash * 397) ^ StaggerFrames;
                hash = (hash * 397) ^ Knockback.GetHashCode();
                hash = (hash * 397) ^ (int)TargetState;
                hash = (hash * 397) ^ ResolvePriority;
                return hash;
            }
        }
    }
}
