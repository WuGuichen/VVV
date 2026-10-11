namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV11Migration
    {
        private const uint ExistingPhases =
            (uint)CharacterTestRuntimeAiPlannerTargetStateMask.ExistingPhaseMask;
        private const uint DeadCompatiblePhases =
            (uint)(CharacterTestRuntimeAiPlannerTargetStateMask.Dying
                | CharacterTestRuntimeAiPlannerTargetStateMask.Dead
                | CharacterTestRuntimeAiPlannerTargetStateMask.Reviving);

        internal static bool HasV11Authority(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload) =>
            payload != null && payload.BehaviorRecipeVersion >= 11;

        internal static void SynchronizeForRead(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            if (!HasV11Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.SynchronizeForRead(payload);
                UpgradeFromV10(payload);
            }
            Canonicalize(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.RebuildLegacyProjection(payload);
        }

        internal static void SynchronizeForWrite(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload) =>
            SynchronizeForRead(payload);

        internal static void UpgradeFromV10(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null || HasV11Authority(payload))
                return;
            RewriteNode(payload.ActivationConditionRoot);
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null)
                    continue;
                RewriteNode(timeline.EntryConditionRoot);
                for (int j = 0; j < timeline.Events.Count; j++)
                    RewriteNode(timeline.Events[j]?.ConditionRoot);
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
                RewriteNode(payload.LegacySharedEvents[i]?.ConditionRoot);
            payload.BehaviorRecipeVersion = 11;
        }

        internal static void Canonicalize(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return;
            payload.ActivationConditionRoot = RewriteDeprecatedSelfActionNode(
                payload.ActivationConditionRoot,
                insideSubjectScope: false);
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null)
                    continue;
                timeline.EntryConditionRoot = RewriteDeprecatedSelfActionNode(
                    timeline.EntryConditionRoot,
                    insideSubjectScope: false);
                for (int j = 0; j < timeline.Events.Count; j++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[j];
                    if (evt != null)
                    {
                        evt.ConditionRoot = RewriteDeprecatedSelfActionNode(
                            evt.ConditionRoot,
                            insideSubjectScope: false);
                    }
                }
            }
            for (int i = 0; i < payload.LegacySharedEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[i];
                if (evt != null)
                {
                    evt.ConditionRoot = RewriteDeprecatedSelfActionNode(
                        evt.ConditionRoot,
                        insideSubjectScope: false);
                }
            }
            payload.BehaviorRecipeVersion = 11;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode RewriteDeprecatedSelfActionNode(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            bool insideSubjectScope)
        {
            if (node == null)
                return null;
            bool subjectScope = node.Kind
                == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope;
            CharacterTestRuntimeAiLabCardCondition condition = node.Predicate;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                && condition != null
                && (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning
                    || condition.Fact
                        == CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible
                    || condition.Fact
                        == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    || condition.Fact
                        == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                    || condition.Fact
                        == CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady
                    || condition.Fact
                        == CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard))
            {
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning)
                {
                    condition.Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible;
                    condition.BoolValue = !condition.BoolValue;
                }
                if (!insideSubjectScope)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject =
                        condition.Fact
                            == CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard
                        ? CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment
                        : CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self;
                    var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                    {
                        NodeId = "condition.scope." + ((int)subject).ToString(
                                System.Globalization.CultureInfo.InvariantCulture)
                            + "." + (node.NodeId ?? string.Empty),
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                        Subject = subject
                    };
                    scope.Children.Add(node);
                    return scope;
                }
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                node.Children[i] = RewriteDeprecatedSelfActionNode(
                    node.Children[i],
                    insideSubjectScope || subjectScope);
            }
            return node;
        }

        private static void RewriteNode(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null)
                return;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                && node.Predicate != null)
            {
                RewritePredicate(node.Predicate);
            }
            for (int i = 0; i < node.Children.Count; i++)
                RewriteNode(node.Children[i]);
        }

        private static void RewritePredicate(CharacterTestRuntimeAiLabCardCondition condition)
        {
            if (condition == null)
                return;
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance)
            {
                condition.Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance;
                return;
            }
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning)
            {
                condition.Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible;
                condition.BoolValue = !condition.BoolValue;
                return;
            }
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState)
                return;
            bool expected = condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                ? !condition.BoolValue
                : condition.BoolValue;
            uint flag = 0u;
            switch (condition.Fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    condition.AllowedTargetPhases = expected
                        ? ExistingPhases
                        : (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    condition.AllowedTargetPhases = expected
                        ? DeadCompatiblePhases
                        : (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    flag = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    flag = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    flag = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    flag = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    flag = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    flag = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded;
                    break;
                default:
                    return;
            }
            if (flag != 0u)
            {
                condition.AllowedTargetPhases = ExistingPhases;
                if (expected)
                    condition.RequiredTargetFlags = flag;
                else
                    condition.ForbiddenTargetFlags = flag;
            }
            condition.Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState;
            condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals;
            condition.BoolValue = true;
            condition.Value = 0f;
            condition.MaxValue = 0f;
            condition.AbilityId = 0;
            condition.CharacterActionId = default;
        }
    }
}
