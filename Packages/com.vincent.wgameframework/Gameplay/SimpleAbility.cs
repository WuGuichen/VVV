using System;

namespace MxFramework.Gameplay
{
    /// <summary>Default minimal ability implementation that selects targets, applies effects, and publishes lifecycle events.</summary>
    public sealed class SimpleAbility : IAbility
    {
        private readonly ITargetSelector _targetSelector;
        private readonly IAbilityEffect[] _effects;

        public SimpleAbility(int abilityId, ITargetSelector targetSelector, IAbilityEffect[] effects)
        {
            AbilityId = abilityId;
            _targetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
            _effects = effects ?? Array.Empty<IAbilityEffect>();
        }

        public int AbilityId { get; }

        public AbilityCastResult Cast(AbilityContext context)
        {
            Publish(context.Caster, new AbilityEvent(AbilityEventType.CastStarted, AbilityId, context.Caster));

            var targets = _targetSelector.SelectTargets(context);
            if (targets.Count == 0)
            {
                Publish(context.Caster, new AbilityEvent(
                    AbilityEventType.CastFailed,
                    AbilityId,
                    context.Caster,
                    failureReason: "NoValidTargets"));

                return AbilityCastResult.Fail("NoValidTargets");
            }

            Publish(context.Caster, new AbilityEvent(AbilityEventType.TargetSelected, AbilityId, context.Caster, targets[0]));

            for (int t = 0; t < targets.Count; t++)
            {
                IRuntimeEntity target = targets[t];
                for (int e = 0; e < _effects.Length; e++)
                    _effects[e].Apply(context, target);

                Publish(context.Caster, new AbilityEvent(AbilityEventType.EffectApplied, AbilityId, context.Caster, target));
            }

            Publish(context.Caster, new AbilityEvent(AbilityEventType.CastFinished, AbilityId, context.Caster));
            return AbilityCastResult.Ok(targets);
        }

        private static void Publish(IRuntimeEntity caster, AbilityEvent evt)
        {
            caster.AbilityEvents.Publish(evt);
        }
    }
}
