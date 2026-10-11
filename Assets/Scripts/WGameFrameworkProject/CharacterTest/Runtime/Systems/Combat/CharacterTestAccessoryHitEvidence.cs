using System;
using MxFramework.CharacterAction;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAccessoryHitProducer { Unknown, WeaponTrace, ProjectileDirectSweep, PeriodicTick, DerivedChain }

    /// <summary>Written where the hit is produced, alongside the exact resolved-hit key. No SourceKind/ID inference.</summary>
    internal readonly struct CharacterTestAccessoryHitEvidence
    {
        private CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer producer, CharacterActionId action, FixVector3 direction)
        { Producer = producer; Action = action; Direction = BoundHorizontalWitness(direction); }
        internal CharacterTestAccessoryHitProducer Producer { get; }
        internal CharacterActionId Action { get; }
        internal FixVector3 Direction { get; }
        internal TacticalAccessoryDamageOrigin Origin => Producer == CharacterTestAccessoryHitProducer.WeaponTrace
            || Producer == CharacterTestAccessoryHitProducer.ProjectileDirectSweep ? TacticalAccessoryDamageOrigin.DirectAttack
            : Producer == CharacterTestAccessoryHitProducer.PeriodicTick ? TacticalAccessoryDamageOrigin.Periodic
            : Producer == CharacterTestAccessoryHitProducer.DerivedChain ? TacticalAccessoryDamageOrigin.Derived
            : TacticalAccessoryDamageOrigin.Unclassified;
        internal static CharacterTestAccessoryHitEvidence DirectTrace(CharacterActionId action, FixVector3 actualIncomingDisplacement)
            => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.WeaponTrace, action, actualIncomingDisplacement);
        internal static CharacterTestAccessoryHitEvidence DirectProjectile(CharacterActionId action, FixVector3 projectileDirectionAtImpact)
            => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.ProjectileDirectSweep, action, projectileDirectionAtImpact);
        internal static CharacterTestAccessoryHitEvidence Periodic => new CharacterTestAccessoryHitEvidence(CharacterTestAccessoryHitProducer.PeriodicTick, default, default);
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
