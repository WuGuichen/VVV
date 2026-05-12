using System;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    public static class CombatGameplayEventBridge
    {
        public static bool TryCreateAbilityEvent(
            HitResolveResult result,
            int abilityId,
            IRuntimeEntity caster,
            IRuntimeEntity target,
            out AbilityEvent abilityEvent)
        {
            if (caster == null)
            {
                throw new ArgumentNullException(nameof(caster));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (result.Kind == HitResolveKind.Damage)
            {
                abilityEvent = new AbilityEvent(
                    AbilityEventType.EffectApplied,
                    abilityId,
                    caster,
                    target);
                return true;
            }

            abilityEvent = new AbilityEvent(
                AbilityEventType.CastFailed,
                abilityId,
                caster,
                target,
                result.Kind.ToString());
            return false;
        }
    }
}
