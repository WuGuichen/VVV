using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Hit
{
    /// <summary>Stable identity of the physical producer and its authored occurrence.
    /// It supplements action identity; it never replaces action/target/frame metadata.</summary>
    public enum CombatHitProducerKind { None = 0, ProjectileDirect = 1, ProjectileArea = 2, ProjectilePeriodic = 3, EffectTimeline = 4, DerivedDamage = 5 }

    public readonly struct CombatHitProducerIdentity : IEquatable<CombatHitProducerIdentity>, IComparable<CombatHitProducerIdentity>
    {
        public CombatHitProducerIdentity(CombatHitProducerKind kind, ulong id, ulong generation, ulong occurrence)
        {
            if (kind <= CombatHitProducerKind.None || kind > CombatHitProducerKind.DerivedDamage || id == 0)
                throw new ArgumentOutOfRangeException(nameof(id));
            Kind = kind; Id = id; Generation = generation; Occurrence = occurrence;
        }
        public CombatHitProducerKind Kind { get; }
        public ulong Id { get; }
        public ulong Generation { get; }
        public ulong Occurrence { get; }
        public bool IsValid => Kind != CombatHitProducerKind.None && Id != 0;
        public bool Equals(CombatHitProducerIdentity other) => Kind == other.Kind && Id == other.Id
            && Generation == other.Generation && Occurrence == other.Occurrence;
        public override bool Equals(object value) => value is CombatHitProducerIdentity other && Equals(other);
        public int CompareTo(CombatHitProducerIdentity other)
        {
            int c = ((int)Kind).CompareTo((int)other.Kind); if (c == 0) c = Id.CompareTo(other.Id);
            if (c == 0) c = Generation.CompareTo(other.Generation);
            return c == 0 ? Occurrence.CompareTo(other.Occurrence) : c;
        }
        public override int GetHashCode()
        { unchecked { return (((int)Kind * 397 ^ Id.GetHashCode()) * 397 ^ Generation.GetHashCode()) * 397 ^ Occurrence.GetHashCode(); } }
    }

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
            FixVector3 knockback,
            FixVector3 hitPointWorld = default,
            FixVector3 hitNormalWorld = default)
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
            HitPointWorld = hitPointWorld;
            HitNormalWorld = hitNormalWorld;
            ProducerIdentity = default;
        }

        public CombatHitProducerIdentity ProducerIdentity { get; }
        private HitResolveResult(in HitResolveResult value, CombatHitProducerIdentity identity)
        { this = value; ProducerIdentity = identity; }
        public HitResolveResult WithProducerIdentity(CombatHitProducerIdentity identity) => new HitResolveResult(this, identity);

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

        public FixVector3 HitPointWorld { get; }

        public FixVector3 HitNormalWorld { get; }

        public bool IsAcceptedDamage => Kind == HitResolveKind.Damage && Damage > 0;

        public bool Equals(HitResolveResult other)
        {
            return ProducerIdentity.Equals(other.ProducerIdentity)
                && AttackerId.Equals(other.AttackerId)
                && TargetId.Equals(other.TargetId)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && Frame.Equals(other.Frame)
                && Kind == other.Kind
                && Damage == other.Damage
                && StaggerFrames == other.StaggerFrames
                && Knockback.Equals(other.Knockback)
                && HitPointWorld.Equals(other.HitPointWorld)
                && HitNormalWorld.Equals(other.HitNormalWorld);
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
                hash = (hash * 397) ^ HitPointWorld.GetHashCode();
                hash = (hash * 397) ^ HitNormalWorld.GetHashCode();
                return ProducerIdentity.IsValid ? (hash * 397) ^ ProducerIdentity.GetHashCode() : hash;
            }
        }
    }
}
