using System;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentAttributeDeltaAbility : IGameplayComponentAbility
    {
        private readonly int _attributeId;
        private readonly int _delta;
        private readonly GameplayComponentTargetMode _targetMode;

        public GameplayComponentAttributeDeltaAbility(
            int abilityId,
            int attributeId,
            int delta,
            GameplayComponentTargetMode targetMode)
            : this(abilityId, attributeId, delta, targetMode, null)
        {
        }

        public GameplayComponentAttributeDeltaAbility(
            int abilityId,
            int attributeId,
            int delta,
            GameplayComponentTargetMode targetMode,
            GameplayComponentAbilityRuleSet rules)
        {
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Component ability id must be greater than zero.");
            if (attributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay attribute id must be greater than zero.");
            if (targetMode != GameplayComponentTargetMode.Self &&
                targetMode != GameplayComponentTargetMode.ExplicitSingle)
            {
                throw new ArgumentOutOfRangeException(nameof(targetMode), "Component ability target mode is not supported.");
            }

            AbilityId = abilityId;
            _attributeId = attributeId;
            _delta = delta;
            _targetMode = targetMode;
            Rules = rules ?? GameplayComponentAbilityRuleSet.Empty;
        }

        public int AbilityId { get; }
        public GameplayComponentAbilityRuleSet Rules { get; }

        public GameplayComponentAbilityResult Cast(GameplayComponentAbilityContext context)
        {
            GameplayEntityId targetEntityId = ResolveTarget(context);
            if (!targetEntityId.IsValid || !context.World.IsAlive(targetEntityId))
            {
                return GameplayComponentAbilityResult.Failed(
                    AbilityId,
                    context.CasterEntityId,
                    GameplayComponentAbilityFailureCode.MissingTarget,
                    GameplayComponentAbilityEvents.MissingTargetReason);
            }

            if (!context.World.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store) ||
                !store.TryGet(targetEntityId, out GameplayAttributeSetComponent attributes))
            {
                return GameplayComponentAbilityResult.Failed(
                    AbilityId,
                    context.CasterEntityId,
                    GameplayComponentAbilityFailureCode.MissingAttributeSet,
                    GameplayComponentAbilityEvents.MissingAttributeSetReason,
                    new[] { targetEntityId });
            }

            if (!attributes.TryGet(_attributeId, out _))
            {
                return GameplayComponentAbilityResult.Failed(
                    AbilityId,
                    context.CasterEntityId,
                    GameplayComponentAbilityFailureCode.MissingAttributeSet,
                    GameplayComponentAbilityEvents.MissingAttributeSetReason,
                    new[] { targetEntityId });
            }

            int oldValue = attributes.GetCurrentValueOrDefault(_attributeId);
            GameplayAttributeSetComponent updated;
            try
            {
                updated = attributes.AddCurrentValue(_attributeId, _delta);
            }
            catch (Exception)
            {
                return GameplayComponentAbilityResult.Failed(
                    AbilityId,
                    context.CasterEntityId,
                    GameplayComponentAbilityFailureCode.EffectFailed,
                    GameplayComponentAbilityEvents.EffectFailedReason,
                    new[] { targetEntityId });
            }

            store.Set(targetEntityId, updated);
            int newValue = updated.GetCurrentValueOrDefault(_attributeId);
            context.World.EnqueueEvent(new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentAttributeChanged,
                context.CommandId,
                casterEntityId: 0,
                abilityId: AbilityId,
                targetEntityId: targetEntityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: GameplayAttributeEvents.AddAttributeReason,
                traceId: context.TraceId,
                componentEntityIndex: targetEntityId.Index,
                componentEntityGeneration: targetEntityId.Generation,
                attributeId: _attributeId,
                oldAttributeValue: oldValue,
                newAttributeValue: newValue,
                attributeDelta: _delta));

            return GameplayComponentAbilityResult.Succeeded(
                AbilityId,
                context.CasterEntityId,
                new[] { targetEntityId });
        }

        private GameplayEntityId ResolveTarget(GameplayComponentAbilityContext context)
        {
            if (_targetMode == GameplayComponentTargetMode.Self)
                return context.CasterEntityId;

            return context.TargetEntityIds.Count > 0 ? context.TargetEntityIds[0] : default;
        }
    }
}
