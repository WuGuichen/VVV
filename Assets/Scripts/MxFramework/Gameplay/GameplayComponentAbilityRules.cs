using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class GameplayComponentAbilityRules
    {
        public static GameplayComponentAbilityRuleResult Evaluate(
            GameplayComponentWorld world,
            GameplayEntityId caster,
            int abilityId,
            GameplayComponentAbilityRuleSet rules,
            RuntimeFrame frame)
        {
            if (world == null || !caster.IsValid || abilityId <= 0)
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason);

            rules = rules ?? GameplayComponentAbilityRuleSet.Empty;
            if (rules.IsEmpty)
                return GameplayComponentAbilityRuleResult.Succeeded();

            if (rules.CooldownFrames < 0L)
            {
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason);
            }
            if (rules.CooldownFrames > 0L && rules.CooldownFrames > long.MaxValue - frame.Value)
            {
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason,
                    abilityId);
            }

            if (world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldowns) &&
                cooldowns.TryGet(caster, out GameplayAbilityCooldownComponent cooldown))
            {
                long remaining = cooldown.GetRemainingFrames(abilityId, frame);
                if (remaining > 0L)
                {
                    return GameplayComponentAbilityRuleResult.Failed(
                        GameplayComponentAbilityFailureCode.OnCooldown,
                        GameplayComponentAbilityEvents.AbilityOnCooldownReason,
                        abilityId,
                        remaining);
                }
            }

            IReadOnlyList<GameplayAbilityCost> costs = rules.Costs;
            if (costs.Count > 0)
            {
                if (!world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributesStore) ||
                    !attributesStore.TryGet(caster, out GameplayAttributeSetComponent attributes))
                {
                    return GameplayComponentAbilityRuleResult.Failed(
                        GameplayComponentAbilityFailureCode.InsufficientCost,
                        GameplayComponentAbilityEvents.InsufficientCostReason);
                }

                for (int i = 0; i < costs.Count; i++)
                {
                    GameplayAbilityCost cost = costs[i];
                    if (cost.Amount == 0)
                        continue;
                    if (!attributes.TryGet(cost.AttributeId, out GameplayAttributeValue value) ||
                        value.CurrentValue < cost.Amount)
                    {
                        return GameplayComponentAbilityRuleResult.Failed(
                            GameplayComponentAbilityFailureCode.InsufficientCost,
                            GameplayComponentAbilityEvents.InsufficientCostReason,
                            cost.AttributeId);
                    }
                }
            }

            return GameplayComponentAbilityRuleResult.Succeeded();
        }

        public static GameplayComponentAbilityRuleResult Commit(
            GameplayComponentWorld world,
            GameplayEntityId caster,
            int abilityId,
            GameplayComponentAbilityRuleSet rules,
            RuntimeFrame frame,
            int commandId = 0,
            string traceId = "")
        {
            if (world == null || !caster.IsValid || abilityId <= 0)
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason);

            rules = rules ?? GameplayComponentAbilityRuleSet.Empty;
            if (rules.IsEmpty)
                return GameplayComponentAbilityRuleResult.Succeeded();

            GameplayComponentAbilityRuleResult costResult = CommitCosts(
                world,
                caster,
                abilityId,
                rules,
                frame,
                commandId,
                traceId);
            if (!costResult.Success)
                return costResult;

            return CommitCooldown(
                world,
                caster,
                abilityId,
                rules,
                frame);
        }

        public static GameplayComponentAbilityRuleResult CommitCosts(
            GameplayComponentWorld world,
            GameplayEntityId caster,
            int abilityId,
            GameplayComponentAbilityRuleSet rules,
            RuntimeFrame frame,
            int commandId = 0,
            string traceId = "")
        {
            if (world == null || !caster.IsValid || abilityId <= 0)
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason);

            rules = rules ?? GameplayComponentAbilityRuleSet.Empty;
            return CommitCostsCore(
                world,
                caster,
                abilityId,
                rules,
                frame,
                commandId,
                traceId);
        }

        public static GameplayComponentAbilityRuleResult CommitCooldown(
            GameplayComponentWorld world,
            GameplayEntityId caster,
            int abilityId,
            GameplayComponentAbilityRuleSet rules,
            RuntimeFrame frame)
        {
            if (world == null || !caster.IsValid || abilityId <= 0)
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason);

            rules = rules ?? GameplayComponentAbilityRuleSet.Empty;
            if (rules.CooldownFrames == 0L)
                return GameplayComponentAbilityRuleResult.Succeeded();
            if (rules.CooldownFrames < 0L || rules.CooldownFrames > long.MaxValue - frame.Value)
            {
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason,
                    abilityId);
            }

            GameplayComponentStore<GameplayAbilityCooldownComponent> cooldowns =
                world.GetOrCreateStore<GameplayAbilityCooldownComponent>();
            cooldowns.TryGet(caster, out GameplayAbilityCooldownComponent cooldown);
            GameplayAbilityCooldownComponent updated;
            try
            {
                updated = cooldown.Start(abilityId, frame, rules.CooldownFrames);
            }
            catch (Exception)
            {
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                    GameplayComponentAbilityEvents.InvalidRuleReason,
                    abilityId);
            }

            cooldowns.Set(caster, updated);
            return GameplayComponentAbilityRuleResult.Succeeded();
        }

        public static void RemoveExpiredCooldowns(
            GameplayComponentWorld world,
            GameplayEntityId caster,
            RuntimeFrame frame)
        {
            if (world == null || !caster.IsValid)
                return;
            if (!world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldowns) ||
                !cooldowns.TryGet(caster, out GameplayAbilityCooldownComponent cooldown))
            {
                return;
            }

            GameplayAbilityCooldownComponent updated = cooldown.RemoveExpired(frame);
            if (updated.Count == 0)
                cooldowns.Remove(caster);
            else if (!updated.Equals(cooldown))
                cooldowns.Set(caster, updated);
        }

        private static GameplayComponentAbilityRuleResult CommitCostsCore(
            GameplayComponentWorld world,
            GameplayEntityId caster,
            int abilityId,
            GameplayComponentAbilityRuleSet rules,
            RuntimeFrame frame,
            int commandId,
            string traceId)
        {
            IReadOnlyList<GameplayAbilityCost> costs = rules.Costs;
            if (costs.Count == 0)
                return GameplayComponentAbilityRuleResult.Succeeded();

            if (!world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributesStore) ||
                !attributesStore.TryGet(caster, out GameplayAttributeSetComponent attributes))
            {
                return GameplayComponentAbilityRuleResult.Failed(
                    GameplayComponentAbilityFailureCode.InsufficientCost,
                    GameplayComponentAbilityEvents.InsufficientCostReason);
            }

            GameplayAttributeSetComponent updated = attributes;
            for (int i = 0; i < costs.Count; i++)
            {
                GameplayAbilityCost cost = costs[i];
                if (cost.Amount == 0)
                    continue;
                if (!updated.TryGet(cost.AttributeId, out GameplayAttributeValue value) ||
                    value.CurrentValue < cost.Amount)
                {
                    return GameplayComponentAbilityRuleResult.Failed(
                        GameplayComponentAbilityFailureCode.InsufficientCost,
                        GameplayComponentAbilityEvents.InsufficientCostReason,
                        cost.AttributeId);
                }

                int oldValue = value.CurrentValue;
                int delta = -cost.Amount;
                try
                {
                    updated = updated.AddCurrentValue(cost.AttributeId, delta);
                }
                catch (Exception)
                {
                    return GameplayComponentAbilityRuleResult.Failed(
                        GameplayComponentAbilityFailureCode.InvalidAbilityRule,
                        GameplayComponentAbilityEvents.InvalidRuleReason,
                        cost.AttributeId);
                }

                int newValue = updated.GetCurrentValueOrDefault(cost.AttributeId);
                world.EnqueueEvent(new GameplayRuntimeEvent(
                    frame,
                    GameplayRuntimeEventType.ComponentAttributeChanged,
                    commandId == 0 ? GameplayRuntimeCommandIds.CastComponentAbility : commandId,
                    casterEntityId: 0,
                    abilityId: abilityId,
                    targetEntityId: caster.Index,
                    failureCode: GameplayAbilityRuntimeFailureCode.None,
                    reason: GameplayComponentAbilityEvents.AbilityCostCommittedReason,
                    traceId: traceId,
                    componentEntityIndex: caster.Index,
                    componentEntityGeneration: caster.Generation,
                    attributeId: cost.AttributeId,
                    oldAttributeValue: oldValue,
                    newAttributeValue: newValue,
                    attributeDelta: delta));
            }

            attributesStore.Set(caster, updated);
            return GameplayComponentAbilityRuleResult.Succeeded();
        }
    }
}
