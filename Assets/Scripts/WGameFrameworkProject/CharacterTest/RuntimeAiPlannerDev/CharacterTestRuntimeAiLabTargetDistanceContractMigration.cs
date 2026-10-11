using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Applies the current product decision that legacy Runtime AI Lab distance
    /// predicates now use the locked-Hurtbox surface gap instead of root distance.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabTargetDistanceContractMigration
    {
        internal static void UpgradeToHurtboxDistanceReferences(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;

            Upgrade(payload.ActivationConditions);
            Upgrade(payload.ActivationConditionRoot);
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null)
                    continue;
                Upgrade(timeline.EntryEvent?.Conditions);
                Upgrade(timeline.EntryConditionRoot);
                for (int j = 0; j < timeline.TriggerEvents.Count; j++)
                    Upgrade(timeline.TriggerEvents[j]?.Conditions);
                for (int j = 0; j < timeline.Events.Count; j++)
                    Upgrade(timeline.Events[j]?.ConditionRoot);
            }
            for (int i = 0; i < payload.LegacySharedTriggerEvents.Count; i++)
                Upgrade(payload.LegacySharedTriggerEvents[i]?.Conditions);
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
                Upgrade(payload.LegacySharedEvents[i]?.ConditionRoot);
            for (int i = 0; i < payload.Stages.Count; i++)
                Upgrade(payload.Stages[i]?.EntryConditions);
            for (int i = 0; i < payload.Detectors.Count; i++)
                Upgrade(payload.Detectors[i]?.Conditions);
        }

        private static void Upgrade(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (condition?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance)
                    condition.Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance;
            }
        }

        private static void Upgrade(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null)
                return;
            if (node.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance)
                node.Predicate.Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance;
            for (int i = 0; i < node.Children.Count; i++)
                Upgrade(node.Children[i]);
        }
    }
}
