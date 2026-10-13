using System;
using MxFramework.CharacterAction;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAccessoryHitProducer { Unknown, WeaponTrace, ProjectileDirectSweep, PeriodicTick, DerivedChain, InteractionMark }

    /// <summary>Written where the hit is produced, alongside the exact resolved-hit key. No SourceKind/ID inference.</summary>
    internal readonly struct CharacterTestAccessoryHitEvidence
    {
        private CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer producer, CharacterActionId action, FixVector3 direction,
            TacticalDirectAttackStamp source = default, int sourceActionInstance = 0, TacticalSecondBatchSource lineage = default, GameplayEntityId attacker = default,
            GameplayEntityId target = default, FixVector3 facing = default, bool actualEnemy = false, ulong occurrence = 0)
        { Lineage = lineage; Attacker = attacker; Target = target; Facing = facing; ActualEnemy = actualEnemy; RootOccurrence = occurrence; Producer = producer; Action = action; Direction = BoundHorizontalWitness(direction); DoubleEdgeSource = source; SourceActionInstance = sourceActionInstance; }
        internal CharacterTestAccessoryHitProducer Producer { get; }
        internal CharacterActionId Action { get; }
        internal FixVector3 Direction { get; }
        internal TacticalDirectAttackStamp DoubleEdgeSource { get; }
        internal int SourceActionInstance { get; }
        internal TacticalSecondBatchSource Lineage { get; }
        internal GameplayEntityId Attacker { get; }
        internal GameplayEntityId Target { get; }
        internal FixVector3 Facing { get; }
        internal bool ActualEnemy { get; }
        internal ulong RootOccurrence { get; }
        internal TacticalSecondBatchDirection RearWitness => new TacticalSecondBatchDirection(Facing, Direction,
            Attacker.IsValid && Target.IsValid);
        internal CharacterTestAccessoryHitEvidence WithCollision(GameplayEntityId attacker, CharacterTestCharacterEntity target,
            bool actualEnemy, TacticalSecondBatchSource lineage, ulong occurrence)
        {
            FixVector3 facing = default;
            if (target != null) {
                CharacterTestDeterministicMath.SinCosMilliDegrees(target.Transform.YawMilliDegrees, out int sin, out int cos);
                facing = new FixVector3(Fix64.FromRatio(sin, CharacterTestDeterministicMath.TrigScale), Fix64.Zero,
                    Fix64.FromRatio(cos, CharacterTestDeterministicMath.TrigScale));
            }
            return new CharacterTestAccessoryHitEvidence(Producer, Action, Direction, DoubleEdgeSource, SourceActionInstance,
                lineage, attacker, target?.GameplayEntityId ?? default, facing, actualEnemy, occurrence);
        }
        internal CharacterTestAccessoryHitEvidence WithSource(in TacticalDirectAttackStamp root)
            => new CharacterTestAccessoryHitEvidence(Producer, Action, Direction, root, SourceActionInstance,
                Lineage, Attacker, Target, Facing, ActualEnemy, RootOccurrence);
        internal TacticalAccessoryDamageOrigin Origin => Producer == CharacterTestAccessoryHitProducer.WeaponTrace
            || Producer == CharacterTestAccessoryHitProducer.ProjectileDirectSweep ? TacticalAccessoryDamageOrigin.DirectAttack
            : Producer == CharacterTestAccessoryHitProducer.PeriodicTick ? TacticalAccessoryDamageOrigin.Periodic
            : (Producer == CharacterTestAccessoryHitProducer.DerivedChain || Producer == CharacterTestAccessoryHitProducer.InteractionMark) ? TacticalAccessoryDamageOrigin.Derived
            : TacticalAccessoryDamageOrigin.Unclassified;
        internal static CharacterTestAccessoryHitEvidence DirectTrace(CharacterActionId action, FixVector3 actualIncomingDisplacement,
            TacticalDirectAttackStamp source = default, int sourceActionInstance = 0)
            => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.WeaponTrace, action, actualIncomingDisplacement, source, sourceActionInstance);
        internal static CharacterTestAccessoryHitEvidence DirectProjectile(CharacterActionId action, FixVector3 projectileDirectionAtImpact,
            TacticalDirectAttackStamp source = default)
            => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.ProjectileDirectSweep, action, projectileDirectionAtImpact, source);
        internal static CharacterTestAccessoryHitEvidence Periodic => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.PeriodicTick, default, default);
        internal static CharacterTestAccessoryHitEvidence Mark => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.InteractionMark, default, default);
        internal static CharacterTestAccessoryHitEvidence Derived => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.DerivedChain, default, default);
        // Preserve an actual witness, including a tiny displacement. Zero/invalid evidence stays zero.
        private static FixVector3 BoundHorizontalWitness(FixVector3 direction)
        {
            if (direction.X.RawValue == long.MinValue || direction.Z.RawValue == long.MinValue) return default;
            long largest = Math.Max(Math.Abs(direction.X.RawValue), Math.Abs(direction.Z.RawValue));
            if (largest == 0 || largest > long.MaxValue / Fix64.Scale) return default;
            return new FixVector3(Fix64.FromRatio(direction.X.RawValue, largest), Fix64.Zero,
                Fix64.FromRatio(direction.Z.RawValue, largest));
        }
    }
}
