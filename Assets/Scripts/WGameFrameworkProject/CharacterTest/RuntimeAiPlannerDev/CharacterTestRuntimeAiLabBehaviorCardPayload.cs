using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabCardStepKind
    {
        UseAbility = 0,
        CharacterAction = 1,
        MoveToTarget = 2,
        Wait = 3,
        LegacyMigrationOnly = 4,
        Logic = 5
    }

    internal enum CharacterTestRuntimeAiLabCardConditionFact
    {
        TargetExists = 0,
        TargetVisible = 1,
        TargetDistance = 2,
        SelfActionRunning = 3,
        SelfCanTransitionToAction = 4,
        AbilityAvailable = 5,
        MovePathAvailable = 6,
        TargetAttackWillHitSelf = 7,
        TargetAttackImpactFrames = 8,
        TargetAttackInstanceResponded = 9,
        SelfDodgeCooldownReady = 10,
        TargetInAttackRange = 11,
        TargetLineOfSightBlocked = 12,
        EnvironmentNearHazard = 13,
        TargetDead = 14,
        TargetDistanceAvailable = 15,
        TargetRootDistance = 16,
        TargetState = 17,
        SelfActionInterruptible = 18,
        SelfHealthRatio = 19,
        TargetFacingAngle = 20,
        TargetCurrentAttackWindowEnded = 21
    }

    internal enum CharacterTestRuntimeAiLabCardConditionOperator
    {
        Equals = 0,
        NotEquals = 1,
        LessOrEqual = 2,
        GreaterOrEqual = 3,
        Between = 4,
        GreaterThan = 5
    }

    internal enum CharacterTestRuntimeAiLabCardBranchTrigger
    {
        DefaultSuccess = 0,
        Condition = 1,
        Failure = 2
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardPayload
    {
        public const string Schema = "mx.character-test.runtime-ai-lab.behavior-card-payload.v1";
        public const int MaxStepCount = 5;

        public string EntryStepId { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string RiskSummary { get; set; } = string.Empty;
        public List<CharacterTestRuntimeAiLabCardCondition> TriggerConditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();
        public List<CharacterTestRuntimeAiLabCardStep> Steps { get; } =
            new List<CharacterTestRuntimeAiLabCardStep>();

        public CharacterTestRuntimeAiLabBehaviorCardPayload Clone()
        {
            var copy = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = EntryStepId,
                Intent = Intent,
                RiskSummary = RiskSummary
            };
            for (int i = 0; i < TriggerConditions.Count; i++)
                copy.TriggerConditions.Add(TriggerConditions[i].Clone());
            for (int i = 0; i < Steps.Count; i++)
                copy.Steps.Add(Steps[i].Clone());
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabCardCondition
    {
        public string ConditionId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabCardConditionFact Fact { get; set; }
        public CharacterTestRuntimeAiLabCardConditionOperator Operator { get; set; }
        public bool BoolValue { get; set; }
        public float Value { get; set; }
        public float MaxValue { get; set; }
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
        public uint AllowedTargetPhases { get; set; }
        public uint RequiredTargetFlags { get; set; }
        public uint ForbiddenTargetFlags { get; set; }

        public CharacterTestRuntimeAiLabCardCondition Clone()
        {
            return (CharacterTestRuntimeAiLabCardCondition)MemberwiseClone();
        }
    }

    internal sealed class CharacterTestRuntimeAiLabCardStep
    {
        public string StepId { get; set; } = string.Empty;
        public int Order { get; set; }
        public CharacterTestRuntimeAiLabCardStepKind Kind { get; set; }
        public string Title { get; set; } = string.Empty;
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
        public CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; set; }
        public float MoveMinDistance { get; set; }
        public float MoveMaxDistance { get; set; }
        public int WaitFrames { get; set; }
        public int EstimatedDurationFrames { get; set; }
        public string Note { get; set; } = string.Empty;
        public List<CharacterTestRuntimeAiLabCardCondition> EntryConditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();
        public List<CharacterTestRuntimeAiLabCardCondition> MonitorConditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();
        public List<CharacterTestRuntimeAiLabCardBranch> Branches { get; } =
            new List<CharacterTestRuntimeAiLabCardBranch>();

        public CharacterTestRuntimeAiLabCardStep Clone()
        {
            var copy = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = StepId,
                Order = Order,
                Kind = Kind,
                Title = Title,
                AbilityId = AbilityId,
                CharacterActionId = CharacterActionId,
                LegacyActionKind = LegacyActionKind,
                MoveMinDistance = MoveMinDistance,
                MoveMaxDistance = MoveMaxDistance,
                WaitFrames = WaitFrames,
                EstimatedDurationFrames = EstimatedDurationFrames,
                Note = Note
            };
            for (int i = 0; i < EntryConditions.Count; i++)
                copy.EntryConditions.Add(EntryConditions[i].Clone());
            for (int i = 0; i < MonitorConditions.Count; i++)
                copy.MonitorConditions.Add(MonitorConditions[i].Clone());
            for (int i = 0; i < Branches.Count; i++)
                copy.Branches.Add(Branches[i].Clone());
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabCardBranch
    {
        public const string EndTarget = "End";

        public string BranchId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabCardBranchTrigger Trigger { get; set; }
        public int Priority { get; set; }
        public string TargetStepId { get; set; } = EndTarget;
        public string Label { get; set; } = string.Empty;
        public int WindowStartFrame { get; set; }
        public int WindowEndFrame { get; set; }
        public string EndReason { get; set; } = string.Empty;
        public List<CharacterTestRuntimeAiLabCardCondition> Conditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();

        public CharacterTestRuntimeAiLabCardBranch Clone()
        {
            var copy = new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = BranchId,
                Trigger = Trigger,
                Priority = Priority,
                TargetStepId = TargetStepId,
                Label = Label,
                WindowStartFrame = WindowStartFrame,
                WindowEndFrame = WindowEndFrame,
                EndReason = EndReason
            };
            for (int i = 0; i < Conditions.Count; i++)
                copy.Conditions.Add(Conditions[i].Clone());
            return copy;
        }
    }

    internal enum CharacterTestRuntimeAiLabCardDiagnosticSeverity
    {
        Warning = 0,
        Error = 1
    }

    internal readonly struct CharacterTestRuntimeAiLabCardDiagnostic
    {
        public CharacterTestRuntimeAiLabCardDiagnostic(
            CharacterTestRuntimeAiLabCardDiagnosticSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterTestRuntimeAiLabCardDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardValidator
    {
        public static IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Validate(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (payload == null)
            {
                Error(diagnostics, "CARD_PAYLOAD_MISSING", "$", "Behavior card payload is missing.");
                return diagnostics;
            }
            if (payload.Steps.Count == 0)
                Error(diagnostics, "CARD_STEPS_EMPTY", "$.steps", "Behavior card requires at least one executable step.");
            if (payload.Steps.Count > CharacterTestRuntimeAiLabBehaviorCardPayload.MaxStepCount)
                Error(diagnostics, "CARD_STEPS_LIMIT", "$.steps", "Behavior card exceeds the five-step limit.");

            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < payload.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[i];
                string path = "$.steps[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (step == null)
                {
                    Error(diagnostics, "CARD_STEP_NULL", path, "Behavior card step is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(step.StepId))
                    Error(diagnostics, "CARD_STEP_ID_MISSING", path + ".stepId", "Step id is required.");
                else if (!stepIds.Add(step.StepId))
                    Error(diagnostics, "CARD_STEP_ID_DUPLICATE", path + ".stepId", "Duplicate step id: " + step.StepId);
                ValidateStepPayload(step, path, diagnostics);
                ValidateConditions(step.EntryConditions, path + ".entryConditions", diagnostics);
                ValidateConditions(step.MonitorConditions, path + ".monitorConditions", diagnostics);
                int successCount = 0;
                int failureCount = 0;
                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    CharacterTestRuntimeAiLabCardBranch branch = step.Branches[branchIndex];
                    string branchPath = path + ".branches[" + branchIndex.ToString(CultureInfo.InvariantCulture) + "]";
                    if (branch == null)
                    {
                        Error(diagnostics, "CARD_BRANCH_NULL", branchPath, "Branch is null.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(branch.BranchId))
                        Error(diagnostics, "CARD_BRANCH_ID_MISSING", branchPath + ".branchId", "Branch id is required.");
                    else if (!branchIds.Add(branch.BranchId))
                        Error(diagnostics, "CARD_BRANCH_ID_DUPLICATE", branchPath + ".branchId", "Duplicate branch id: " + branch.BranchId);
                    if (branch.Trigger == CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess) successCount++;
                    if (branch.Trigger == CharacterTestRuntimeAiLabCardBranchTrigger.Failure) failureCount++;
                    if (branch.WindowStartFrame < 0 || branch.WindowEndFrame < branch.WindowStartFrame)
                        Error(diagnostics, "CARD_BRANCH_WINDOW_INVALID", branchPath, "Branch window must be non-negative and ordered.");
                    ValidateConditions(branch.Conditions, branchPath + ".conditions", diagnostics);
                }
                if (successCount != 1)
                    Error(diagnostics, "CARD_DEFAULT_BRANCH_COUNT", path + ".branches", "Each step requires exactly one DefaultSuccess branch.");
                if (failureCount > 1)
                    Error(diagnostics, "CARD_FAILURE_BRANCH_COUNT", path + ".branches", "Each step allows at most one Failure branch.");
            }

            if (!string.IsNullOrWhiteSpace(payload.EntryStepId) && !stepIds.Contains(payload.EntryStepId))
                Error(diagnostics, "CARD_ENTRY_STEP_MISSING", "$.entryStepId", "Entry step does not exist: " + payload.EntryStepId);
            else if (string.IsNullOrWhiteSpace(payload.EntryStepId) && payload.Steps.Count > 0)
                Error(diagnostics, "CARD_ENTRY_STEP_EMPTY", "$.entryStepId", "Entry step id is required.");

            for (int i = 0; i < payload.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[i];
                if (step == null) continue;
                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    CharacterTestRuntimeAiLabCardBranch branch = step.Branches[branchIndex];
                    if (branch == null || string.Equals(branch.TargetStepId, CharacterTestRuntimeAiLabCardBranch.EndTarget, StringComparison.Ordinal))
                        continue;
                    if (!stepIds.Contains(branch.TargetStepId))
                        Error(diagnostics, "CARD_BRANCH_TARGET_MISSING", "$.steps[" + i + "].branches[" + branchIndex + "].targetStepId", "Branch target does not exist: " + branch.TargetStepId);
                }
            }

            ValidateConditions(payload.TriggerConditions, "$.triggerConditions", diagnostics);
            ValidateGraph(payload, diagnostics);
            diagnostics.Sort(CompareDiagnostics);
            return diagnostics;
        }

        public static bool HasErrors(IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void ValidateStepPayload(
            CharacterTestRuntimeAiLabCardStep step,
            string path,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility && step.AbilityId <= 0)
                Error(diagnostics, "CARD_ABILITY_ID_MISSING", path + ".abilityId", "UseAbility requires a positive ability id.");
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction && !step.CharacterActionId.IsValid)
                Error(diagnostics, "CARD_ACTION_ID_MISSING", path + ".characterActionId", "CharacterAction requires a stable CharacterAction id.");
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
                Error(diagnostics, "CARD_LEGACY_ACTION_UNMIGRATED", path + ".kind", "Legacy runtime action must be migrated to a typed stage.");
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                && (step.MoveMinDistance < 0f || step.MoveMaxDistance < step.MoveMinDistance))
            {
                Error(diagnostics, "CARD_MOVE_BAND_INVALID", path, "Move distance band is invalid.");
            }
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait && step.WaitFrames <= 0)
                Error(diagnostics, "CARD_WAIT_FRAMES_INVALID", path + ".waitFrames", "Wait requires a positive frame count.");
        }

        private static void ValidateConditions(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string path,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                string conditionPath = path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (condition == null)
                {
                    Error(diagnostics, "CARD_CONDITION_NULL", conditionPath, "Condition is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(condition.ConditionId))
                    Error(diagnostics, "CARD_CONDITION_ID_MISSING", conditionPath + ".conditionId", "Condition id is required.");
                else if (!ids.Add(condition.ConditionId))
                    Error(diagnostics, "CARD_CONDITION_ID_DUPLICATE", conditionPath + ".conditionId", "Duplicate condition id: " + condition.ConditionId);
                bool boolFact = condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    && condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                    && condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames;
                if (boolFact
                    && condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.NotEquals)
                {
                    Error(diagnostics, "CARD_CONDITION_OPERATOR_INVALID", conditionPath + ".operator", "Boolean fact only supports Equals or NotEquals.");
                }
                if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                    && condition.MaxValue < condition.Value)
                {
                    Error(diagnostics, "CARD_CONDITION_RANGE_INVALID", conditionPath, "Between maximum must be greater than or equal to minimum.");
                }
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable && condition.AbilityId <= 0)
                    Error(diagnostics, "CARD_CONDITION_ABILITY_MISSING", conditionPath + ".abilityId", "AbilityAvailable requires an ability id.");
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    && !condition.CharacterActionId.IsValid)
                {
                    Error(diagnostics, "CARD_CONDITION_ACTION_MISSING", conditionPath + ".characterActionId", "SelfCanTransitionToAction requires a CharacterAction id.");
                }
            }
        }

        private static void ValidateGraph(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (payload.Steps.Count == 0 || string.IsNullOrWhiteSpace(payload.EntryStepId))
                return;
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            bool reachesEnd = Visit(payload, payload.EntryStepId, visiting, visited, diagnostics);
            if (!reachesEnd)
                Error(diagnostics, "CARD_END_UNREACHABLE", "$.entryStepId", "Behavior card has no reachable End branch.");
        }

        private static bool Visit(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            string stepId,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (string.Equals(stepId, CharacterTestRuntimeAiLabCardBranch.EndTarget, StringComparison.Ordinal))
                return true;
            if (visiting.Contains(stepId))
            {
                Error(diagnostics, "CARD_GRAPH_CYCLE", "$.steps", "Behavior card cycle detected at step: " + stepId);
                return false;
            }
            if (visited.Contains(stepId))
                return false;
            CharacterTestRuntimeAiLabCardStep step = FindStep(payload, stepId);
            if (step == null)
                return false;
            visiting.Add(stepId);
            bool reachesEnd = false;
            for (int i = 0; i < step.Branches.Count; i++)
            {
                if (step.Branches[i] != null)
                    reachesEnd |= Visit(payload, step.Branches[i].TargetStepId, visiting, visited, diagnostics);
            }
            visiting.Remove(stepId);
            visited.Add(stepId);
            return reachesEnd;
        }

        private static CharacterTestRuntimeAiLabCardStep FindStep(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            string stepId)
        {
            for (int i = 0; i < payload.Steps.Count; i++)
            {
                if (payload.Steps[i] != null && string.Equals(payload.Steps[i].StepId, stepId, StringComparison.Ordinal))
                    return payload.Steps[i];
            }
            return null;
        }

        private static void Error(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private static int CompareDiagnostics(
            CharacterTestRuntimeAiLabCardDiagnostic left,
            CharacterTestRuntimeAiLabCardDiagnostic right)
        {
            int path = string.CompareOrdinal(left.Path, right.Path);
            return path != 0 ? path : string.CompareOrdinal(left.Code, right.Code);
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardFactory
    {
        public static CharacterTestRuntimeAiLabBehaviorCardPayload CreateLegacyAtomic(
            string behaviorId,
            CharacterTestRuntimeAiPlannerActionKind actionKind = CharacterTestRuntimeAiPlannerActionKind.Wait)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = "action",
                Intent = "Compatibility wrapper for published behavior definition " + (behaviorId ?? string.Empty) + ".",
                RiskSummary = "Legacy atomic definition; authoring expansion requires an explicit typed draft."
            };
            ApplyLegacyActivationMigration(
                payload,
                behaviorId,
                CharacterTestAiBehaviorProfileSource.DefaultAttackRange);
            var step = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = "action",
                Order = 10,
                Title = behaviorId ?? "Published behavior",
                LegacyActionKind = actionKind
            };
            ApplyLegacyMigration(step);
            step.Branches.Add(EndBranch("action.success", CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess, 100, "Complete"));
            step.Branches.Add(EndBranch("action.failure", CharacterTestRuntimeAiLabCardBranchTrigger.Failure, 0, "Failed"));
            payload.Steps.Add(step);
            return payload;
        }

        /// <summary>
        /// Repairs the activation contract that old atomic behavior cards lost when copied
        /// from a behavior-module entry into an editable Runtime AI Lab card.
        /// </summary>
        /// <remarks>LowFreqAlloc: draft creation and persisted-workspace migration only.</remarks>
        internal static bool ApplyLegacyActivationMigration(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            string behaviorId,
            float attackRange)
        {
            if (payload == null)
                return false;

            bool changed = TryMigrateLegacyApproachActivation(
                payload.TriggerConditions,
                behaviorId,
                attackRange);
            if (payload.TriggerConditions.Count == 0)
                return AddLegacyActivationConditions(
                    payload.TriggerConditions,
                    behaviorId,
                    attackRange);

            return EnsureLivingTargetCondition(payload.TriggerConditions, behaviorId) || changed;
        }

        /// <remarks>LowFreqAlloc: persisted-workspace migration only.</remarks>
        internal static bool ApplyLegacyActivationMigration(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string behaviorId,
            float attackRange)
        {
            if (payload == null)
                return false;

            bool changed = TryMigrateLegacyApproachActivation(
                payload.ActivationConditions,
                behaviorId,
                attackRange);
            if (payload.ActivationConditions.Count == 0)
            {
                changed |= AddLegacyActivationConditions(
                    payload.ActivationConditions,
                    behaviorId,
                    attackRange);
            }
            else
            {
                changed |= EnsureLivingTargetCondition(payload.ActivationConditions, behaviorId);
            }

            return ApplyLegacyFallbackHoldMigration(payload, behaviorId) || changed;
        }

        /// <summary>
        /// Migrates the generated fallback compatibility wrapper from a one-frame wait into a
        /// persistent hold stage. Higher-priority cards end the hold through normal preemption.
        /// </summary>
        /// <remarks>LowFreqAlloc: draft creation and persisted-workspace migration only.</remarks>
        internal static bool ApplyLegacyFallbackHoldMigration(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string behaviorId)
        {
            if (payload == null
                || !string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal)
                || payload.Stages.Count != 1
                || payload.Detectors.Count != 2
                || payload.Transitions.Count != 2)
            {
                return false;
            }

            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[0];
            if (stage == null
                || !string.Equals(stage.StageId, "action", StringComparison.Ordinal)
                || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.Wait
                || !HasGeneratedEndTransition(payload, "action.success")
                || !HasGeneratedEndTransition(payload, "action.failure"))
            {
                return false;
            }

            stage.DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Infinite;
            stage.PlannedDurationFrames = 0;
            payload.Detectors.Clear();
            payload.Transitions.Clear();
            return true;
        }

        private static bool HasGeneratedEndTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (transition != null
                    && string.Equals(transition.TransitionId, transitionId, StringComparison.Ordinal)
                    && string.Equals(
                        transition.TargetStageId,
                        CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AddLegacyActivationConditions(
            ICollection<CharacterTestRuntimeAiLabCardCondition> conditions,
            string behaviorId,
            float attackRange)
        {
            return false;
        }

        private static bool TryMigrateLegacyApproachActivation(
            IList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string behaviorId,
            float attackRange)
        {
            if (!string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                || conditions == null)
            {
                return false;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (condition == null
                    || condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange
                    || condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    || condition.BoolValue)
                {
                    continue;
                }

                conditions[i] = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = condition.ConditionId,
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan,
                    Value = NormalizePositiveDistance(attackRange)
                };
                return true;
            }

            return false;
        }

        private static float NormalizePositiveDistance(float distance)
        {
            return float.IsNaN(distance)
                || float.IsInfinity(distance)
                || distance <= 0f
                    ? CharacterTestAiBehaviorProfileSource.DefaultAttackRange
                    : distance;
        }

        private static void AddPrimaryAttackActivationConditions(
            ICollection<CharacterTestRuntimeAiLabCardCondition> conditions,
            float attackRange)
        {
            float normalizedRange = float.IsNaN(attackRange)
                || float.IsInfinity(attackRange)
                || attackRange <= 0f
                    ? CharacterTestAiBehaviorProfileSource.DefaultAttackRange
                    : attackRange;
            conditions.Add(Bool(
                "trigger.target",
                CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                true));
            conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "trigger.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between,
                Value = 0f,
                MaxValue = normalizedRange
            });
            conditions.Add(Bool(
                "trigger.idle",
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning,
                false));
            AddLivingTargetCondition(conditions);
        }

        private static bool EnsureLivingTargetCondition(
            ICollection<CharacterTestRuntimeAiLabCardCondition> conditions,
            string behaviorId)
        {
            if (!string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                && !string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                return false;
            }

            foreach (CharacterTestRuntimeAiLabCardCondition condition in conditions)
            {
                if (condition != null
                    && condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDead
                    && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && !condition.BoolValue)
                {
                    return false;
                }
            }

            AddLivingTargetCondition(conditions);
            return true;
        }

        private static void AddLivingTargetCondition(
            ICollection<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            conditions.Add(Bool(
                "activation.target-alive",
                CharacterTestRuntimeAiLabCardConditionFact.TargetDead,
                false));
        }

        internal static void ApplyLegacyMigration(CharacterTestRuntimeAiLabCardStep step)
        {
            if (step == null)
                return;

            switch (step.LegacyActionKind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                    step.CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
                    step.LegacyActionKind = default;
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                    step.CharacterActionId = CharacterTestCharacterActionConfigSource.DefaultEvadeActionId;
                    step.LegacyActionKind = default;
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                    step.LegacyActionKind = default;
                    if (step.MoveMaxDistance < step.MoveMinDistance)
                        step.MoveMaxDistance = step.MoveMinDistance;
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                    if (step.MoveMaxDistance < step.MoveMinDistance)
                        step.MoveMaxDistance = step.MoveMinDistance;
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Wait:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.Wait;
                    step.WaitFrames = Math.Max(1, step.WaitFrames > 0 ? step.WaitFrames : step.EstimatedDurationFrames);
                    step.LegacyActionKind = default;
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility;
                    step.LegacyActionKind = default;
                    break;
                default:
                    step.Kind = CharacterTestRuntimeAiLabCardStepKind.Logic;
                    break;
            }
        }

        internal static void ApplyContinuousApproachConditionMigration(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload == null)
                return;
            for (int stepIndex = 0; stepIndex < payload.Steps.Count; stepIndex++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[stepIndex];
                if (step == null
                    || step.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    || step.MoveMinDistance <= 0f)
                {
                    continue;
                }
                for (int conditionIndex = 0;
                     conditionIndex < payload.TriggerConditions.Count;
                     conditionIndex++)
                {
                    CharacterTestRuntimeAiLabCardCondition condition =
                        payload.TriggerConditions[conditionIndex];
                    if (condition == null
                        || condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                        || condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual
                        || Math.Abs(condition.Value - step.MoveMinDistance) > 0.0001f)
                    {
                        continue;
                    }
                    condition.Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan;
                }
            }
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CreateActionChainFixture()
        {
            CharacterActionId[] lightAttackSequence = ResolveFormalCommandSequence(
                CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId,
                minimumLength: 2);
            CharacterActionId firstLightAttack = lightAttackSequence[0];
            CharacterActionId secondLightAttack = lightAttackSequence[1];
            var payload = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = "dash",
                Intent = "Close from medium range, then chain two CharacterAction light attacks when legal.",
                RiskSummary = "Requires the referenced CharacterAction transition windows."
            };
            payload.TriggerConditions.Add(Bool("trigger.target", CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true));
            payload.TriggerConditions.Add(Bool("trigger.visible", CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true));
            payload.TriggerConditions.Add(Range("trigger.distance", 3f, 7f));
            payload.TriggerConditions.Add(Bool("trigger.idle", CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning, false));
            CharacterTestRuntimeAiLabCardStep dash = ActionStep(
                "dash", 10, "Dash Strike", CharacterTestCharacterActionConfigSource.DashStrikeActionId, 54);
            dash.Branches.Add(ConditionBranch("dash.near", "attack.first", 10, Range("dash.distance", 0f, 1f), "Within 1m"));
            dash.Branches.Add(SuccessBranch("dash.continue", "second_dash", 100, "Second dash"));
            dash.Branches.Add(EndBranch("dash.failure", CharacterTestRuntimeAiLabCardBranchTrigger.Failure, 0, "Dash failed"));
            payload.Steps.Add(dash);

            CharacterTestRuntimeAiLabCardStep secondDash = ActionStep(
                "second_dash", 20, "Second Dash", CharacterTestCharacterActionConfigSource.DashStrikeActionId, 54);
            secondDash.Branches.Add(ConditionBranch("second.near", "attack.first", 10, Range("second.distance", 0f, 1f), "Within 1m"));
            secondDash.Branches.Add(EndBranch("second.complete", CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess, 100, "Still out of range"));
            secondDash.Branches.Add(EndBranch("second.failure", CharacterTestRuntimeAiLabCardBranchTrigger.Failure, 0, "Second dash failed"));
            payload.Steps.Add(secondDash);

            CharacterTestRuntimeAiLabCardStep attack = ActionStep("attack.first", 30, "Light Attack", firstLightAttack, 76);
            attack.EntryConditions.Add(ActionTransition("attack.transition", firstLightAttack));
            attack.EntryConditions.Add(Range("attack.distance", 0f, 1f));
            attack.Branches.Add(SuccessBranch("attack.combo", "attack.second", 100, "Combo window"));
            attack.Branches.Add(EndBranch("attack.failure", CharacterTestRuntimeAiLabCardBranchTrigger.Failure, 0, "Attack rejected"));
            payload.Steps.Add(attack);

            CharacterTestRuntimeAiLabCardStep attack2 = ActionStep(
                "attack.second",
                40,
                "Light Attack 2",
                secondLightAttack,
                76);
            attack2.EntryConditions.Add(ActionTransition(
                "attack2.transition",
                secondLightAttack));
            attack2.EntryConditions.Add(Range("attack2.distance", 0f, 1f));
            attack2.Branches.Add(EndBranch("attack2.complete", CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess, 100, "Combo complete"));
            attack2.Branches.Add(EndBranch("attack2.failure", CharacterTestRuntimeAiLabCardBranchTrigger.Failure, 0, "Combo rejected"));
            payload.Steps.Add(attack2);
            return payload;
        }

        private static CharacterActionId[] ResolveFormalCommandSequence(int intentId, int minimumLength)
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterActionSetConfig actionSet = null;
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            if (runtime?.Registry != null && definitions.TryGet(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, out var definition))
            {
                string characterId = CharacterTestCharacterRuntimeWorld.ResolveCharacterStableId(definition);
                string equipmentId = CharacterTestCharacterRuntimeWorld.ResolveDefaultEquipmentStateStableId(
                    CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot));
                foreach (CharacterActionSetConfig candidate in runtime.Registry.ActionSets)
                {
                    if (!string.Equals(candidate.CharacterStableId, characterId, StringComparison.Ordinal)
                        || !string.Equals(candidate.EquipmentStateStableId, equipmentId, StringComparison.Ordinal))
                        continue;
                    if (actionSet != null)
                        throw new InvalidOperationException("Runtime AI Lab template requires an unambiguous default-character ActionSet.");
                    actionSet = candidate;
                }
            }
            if (actionSet == null)
            {
                throw new InvalidOperationException(
                    "Runtime AI Lab template requires the current formal CharacterAction ActionSet.");
            }

            for (int i = 0; i < actionSet.CommandSequences.Length; i++)
            {
                CharacterActionCommandSequence sequence = actionSet.CommandSequences[i];
                if (sequence != null
                    && sequence.IntentId == intentId
                    && sequence.ActionIds.Length >= minimumLength)
                {
                    return sequence.ActionIds;
                }
            }

            throw new InvalidOperationException(
                "Runtime AI Lab template requires a formal '" + intentId
                + "' CharacterAction sequence with at least " + minimumLength + " actions.");
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CreateReactiveDodgeAtomic(
            int minimumImpactFrames = 6,
            int maximumImpactFrames = 12)
        {
            int minimum = Math.Max(0, minimumImpactFrames);
            int maximum = Math.Max(minimum, maximumImpactFrames);
            var payload = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = "dodge",
                Intent = "Dodge once when the selected target attack is predicted to hit inside the reaction window.",
                RiskSummary = "Reactive override; one response per attack instance and cooldown are enforced by runtime authority."
            };
            payload.TriggerConditions.Add(Bool(
                "trigger.target",
                CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                true));
            payload.TriggerConditions.Add(Bool(
                "trigger.will-hit",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf,
                true));
            payload.TriggerConditions.Add(NumericRange(
                "trigger.impact-window",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames,
                minimum,
                maximum));
            payload.TriggerConditions.Add(Bool(
                "trigger.not-responded",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded,
                false));
            payload.TriggerConditions.Add(Bool(
                "trigger.cooldown",
                CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady,
                true));

            var step = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = "dodge",
                Order = 10,
                Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                Title = "侧向闪避",
                CharacterActionId = CharacterTestCharacterActionConfigSource.DefaultEvadeActionId,
                EstimatedDurationFrames = 1,
                Note = "直接请求配置中的 CharacterAction；接受或拒绝结果写入时间线。"
            };
            step.Branches.Add(EndBranch(
                "dodge.success",
                CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess,
                100,
                "重新决策并恢复靠近"));
            step.Branches.Add(EndBranch(
                "dodge.failure",
                CharacterTestRuntimeAiLabCardBranchTrigger.Failure,
                0,
                "记录拒绝并重新决策"));
            payload.Steps.Add(step);
            return payload;
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CreateContinuousApproachAtomic(
            float stopDistance = 1.5f)
        {
            float distance = Math.Max(0f, stopDistance);
            var payload = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = "approach",
                Intent = "Continuously approach the selected target until the gameplay stop distance is reached.",
                RiskSummary = "Base behavior; a higher-priority reactive behavior may preempt it for one decision."
            };
            payload.TriggerConditions.Add(Bool(
                "trigger.target",
                CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                true));
            payload.TriggerConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "trigger.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan,
                Value = distance
            });
            payload.TriggerConditions.Add(Bool(
                "trigger.path",
                CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable,
                true));

            var step = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = "approach",
                Order = 10,
                Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                Title = "持续靠近",
                MoveMinDistance = distance,
                MoveMaxDistance = 999f,
                Note = "闪避完成后，下次决策会自动回到本基础行为。"
            };
            step.Branches.Add(EndBranch(
                "approach.continue",
                CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess,
                100,
                "继续"));
            step.Branches.Add(EndBranch(
                "approach.failure",
                CharacterTestRuntimeAiLabCardBranchTrigger.Failure,
                0,
                "路径不可用时重新决策"));
            payload.Steps.Add(step);
            return payload;
        }

        private static CharacterTestRuntimeAiLabCardStep AbilityStep(string id, int order, string title, int abilityId, int duration)
        {
            return new CharacterTestRuntimeAiLabCardStep
            {
                StepId = id,
                Order = order,
                Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility,
                Title = title,
                AbilityId = abilityId,
                EstimatedDurationFrames = duration
            };
        }

        private static CharacterTestRuntimeAiLabCardStep ActionStep(
            string id,
            int order,
            string title,
            MxFramework.CharacterAction.CharacterActionId actionId,
            int duration)
        {
            return new CharacterTestRuntimeAiLabCardStep
            {
                StepId = id,
                Order = order,
                Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                Title = title,
                CharacterActionId = actionId,
                EstimatedDurationFrames = duration
            };
        }

        private static CharacterTestRuntimeAiLabCardCondition Bool(string id, CharacterTestRuntimeAiLabCardConditionFact fact, bool value)
        {
            return new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = id,
                Fact = fact,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = value
            };
        }

        private static CharacterTestRuntimeAiLabCardCondition Ability(string id, int abilityId)
        {
            CharacterTestRuntimeAiLabCardCondition condition = Bool(id, CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable, true);
            condition.AbilityId = abilityId;
            return condition;
        }

        private static CharacterTestRuntimeAiLabCardCondition ActionTransition(
            string id,
            MxFramework.CharacterAction.CharacterActionId actionId)
        {
            CharacterTestRuntimeAiLabCardCondition condition = Bool(id, CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction, true);
            condition.CharacterActionId = actionId;
            return condition;
        }

        private static CharacterTestRuntimeAiLabCardCondition Range(string id, float min, float max)
        {
            return NumericRange(
                id,
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                min,
                max);
        }

        private static CharacterTestRuntimeAiLabCardCondition NumericRange(
            string id,
            CharacterTestRuntimeAiLabCardConditionFact fact,
            float min,
            float max)
        {
            return new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = id,
                Fact = fact,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between,
                Value = min,
                MaxValue = max
            };
        }

        private static CharacterTestRuntimeAiLabCardBranch SuccessBranch(string id, string target, int priority, string label)
        {
            return new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = id,
                Trigger = CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess,
                Priority = priority,
                TargetStepId = target,
                Label = label
            };
        }

        private static CharacterTestRuntimeAiLabCardBranch ConditionBranch(
            string id,
            string target,
            int priority,
            CharacterTestRuntimeAiLabCardCondition condition,
            string label)
        {
            var branch = new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = id,
                Trigger = CharacterTestRuntimeAiLabCardBranchTrigger.Condition,
                Priority = priority,
                TargetStepId = target,
                Label = label
            };
            branch.Conditions.Add(condition);
            return branch;
        }

        private static CharacterTestRuntimeAiLabCardBranch EndBranch(
            string id,
            CharacterTestRuntimeAiLabCardBranchTrigger trigger,
            int priority,
            string reason)
        {
            return new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = id,
                Trigger = trigger,
                Priority = priority,
                TargetStepId = CharacterTestRuntimeAiLabCardBranch.EndTarget,
                Label = reason,
                EndReason = reason
            };
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardJson
    {
        public static string Serialize(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            return WritePayload(payload).ToString(Formatting.Indented);
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload Deserialize(string json)
        {
            JObject root = JObject.Parse(json ?? string.Empty);
            string schema = (string)root["schema"];
            if (!string.Equals(schema, CharacterTestRuntimeAiLabBehaviorCardPayload.Schema, StringComparison.Ordinal))
                throw new JsonSerializationException("Unsupported behavior card payload schema: " + (schema ?? string.Empty));
            var payload = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = (string)root["entryStepId"] ?? string.Empty,
                Intent = (string)root["intent"] ?? string.Empty,
                RiskSummary = (string)root["riskSummary"] ?? string.Empty
            };
            ReadConditions(root["triggerConditions"] as JArray, payload.TriggerConditions);
            JArray steps = root["steps"] as JArray;
            for (int i = 0; steps != null && i < steps.Count; i++)
                payload.Steps.Add(ReadStep((JObject)steps[i]));
            CharacterTestRuntimeAiLabBehaviorCardFactory
                .ApplyContinuousApproachConditionMigration(payload);
            return payload;
        }

        public static JObject WritePayload(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            payload = payload ?? new CharacterTestRuntimeAiLabBehaviorCardPayload();
            var steps = new JArray();
            for (int i = 0; i < payload.Steps.Count; i++)
                steps.Add(WriteStep(payload.Steps[i]));
            return new JObject
            {
                ["schema"] = CharacterTestRuntimeAiLabBehaviorCardPayload.Schema,
                ["entryStepId"] = payload.EntryStepId,
                ["intent"] = payload.Intent,
                ["riskSummary"] = payload.RiskSummary,
                ["triggerConditions"] = WriteConditions(payload.TriggerConditions),
                ["steps"] = steps
            };
        }

        private static JObject WriteStep(CharacterTestRuntimeAiLabCardStep step)
        {
            var branches = new JArray();
            for (int i = 0; i < step.Branches.Count; i++)
                branches.Add(WriteBranch(step.Branches[i]));
            return new JObject
            {
                ["stepId"] = step.StepId,
                ["order"] = step.Order,
                ["kind"] = step.Kind.ToString(),
                ["title"] = step.Title,
                ["abilityId"] = step.AbilityId,
                ["characterActionId"] = step.CharacterActionId.Value,
                ["legacyActionKind"] = step.LegacyActionKind.ToString(),
                ["moveMinDistance"] = step.MoveMinDistance,
                ["moveMaxDistance"] = step.MoveMaxDistance,
                ["waitFrames"] = step.WaitFrames,
                ["estimatedDurationFrames"] = step.EstimatedDurationFrames,
                ["note"] = step.Note,
                ["entryConditions"] = WriteConditions(step.EntryConditions),
                ["monitorConditions"] = WriteConditions(step.MonitorConditions),
                ["branches"] = branches
            };
        }

        private static JObject WriteBranch(CharacterTestRuntimeAiLabCardBranch branch)
        {
            return new JObject
            {
                ["branchId"] = branch.BranchId,
                ["trigger"] = branch.Trigger.ToString(),
                ["priority"] = branch.Priority,
                ["targetStepId"] = branch.TargetStepId,
                ["label"] = branch.Label,
                ["windowStartFrame"] = branch.WindowStartFrame,
                ["windowEndFrame"] = branch.WindowEndFrame,
                ["endReason"] = branch.EndReason,
                ["conditions"] = WriteConditions(branch.Conditions)
            };
        }

        private static JArray WriteConditions(IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            var result = new JArray();
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                result.Add(new JObject
                {
                    ["conditionId"] = condition.ConditionId,
                    ["fact"] = condition.Fact.ToString(),
                    ["operator"] = condition.Operator.ToString(),
                    ["boolValue"] = condition.BoolValue,
                    ["value"] = condition.Value,
                    ["maxValue"] = condition.MaxValue,
                    ["abilityId"] = condition.AbilityId,
                    ["characterActionId"] = condition.CharacterActionId.Value
                });
            }
            return result;
        }

        private static CharacterTestRuntimeAiLabCardStep ReadStep(JObject root)
        {
            var step = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = (string)root["stepId"] ?? string.Empty,
                Order = (int?)root["order"] ?? 0,
                Kind = ParseEnum<CharacterTestRuntimeAiLabCardStepKind>(root, "kind"),
                Title = (string)root["title"] ?? string.Empty,
                AbilityId = (int?)root["abilityId"] ?? 0,
                CharacterActionId = new MxFramework.CharacterAction.CharacterActionId(
                    (int?)root["characterActionId"] ?? 0),
                LegacyActionKind = ParseEnum<CharacterTestRuntimeAiPlannerActionKind>(root, "legacyActionKind"),
                MoveMinDistance = (float?)root["moveMinDistance"] ?? 0f,
                MoveMaxDistance = (float?)root["moveMaxDistance"] ?? 0f,
                WaitFrames = (int?)root["waitFrames"] ?? 0,
                EstimatedDurationFrames = (int?)root["estimatedDurationFrames"] ?? 0,
                Note = (string)root["note"] ?? string.Empty
            };
            ReadConditions(root["entryConditions"] as JArray, step.EntryConditions);
            ReadConditions(root["monitorConditions"] as JArray, step.MonitorConditions);
            JArray branches = root["branches"] as JArray;
            for (int i = 0; branches != null && i < branches.Count; i++)
                step.Branches.Add(ReadBranch((JObject)branches[i]));
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
                CharacterTestRuntimeAiLabBehaviorCardFactory.ApplyLegacyMigration(step);
            return step;
        }

        private static CharacterTestRuntimeAiLabCardBranch ReadBranch(JObject root)
        {
            var branch = new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = (string)root["branchId"] ?? string.Empty,
                Trigger = ParseEnum<CharacterTestRuntimeAiLabCardBranchTrigger>(root, "trigger"),
                Priority = (int?)root["priority"] ?? 0,
                TargetStepId = (string)root["targetStepId"] ?? CharacterTestRuntimeAiLabCardBranch.EndTarget,
                Label = (string)root["label"] ?? string.Empty,
                WindowStartFrame = (int?)root["windowStartFrame"] ?? 0,
                WindowEndFrame = (int?)root["windowEndFrame"] ?? 0,
                EndReason = (string)root["endReason"] ?? string.Empty
            };
            ReadConditions(root["conditions"] as JArray, branch.Conditions);
            return branch;
        }

        private static void ReadConditions(JArray source, List<CharacterTestRuntimeAiLabCardCondition> destination)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                JObject root = (JObject)source[i];
                destination.Add(new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = (string)root["conditionId"] ?? string.Empty,
                    Fact = ParseEnum<CharacterTestRuntimeAiLabCardConditionFact>(root, "fact"),
                    Operator = ParseEnum<CharacterTestRuntimeAiLabCardConditionOperator>(root, "operator"),
                    BoolValue = (bool?)root["boolValue"] ?? false,
                    Value = (float?)root["value"] ?? 0f,
                    MaxValue = (float?)root["maxValue"] ?? 0f,
                    AbilityId = (int?)root["abilityId"] ?? 0,
                    CharacterActionId = new MxFramework.CharacterAction.CharacterActionId(
                        (int?)root["characterActionId"] ?? 0)
                });
            }
        }

        private static T ParseEnum<T>(JObject root, string property) where T : struct
        {
            string value = (string)root[property];
            if (!Enum.TryParse(value, ignoreCase: false, out T parsed) || !Enum.IsDefined(typeof(T), parsed))
                throw new JsonSerializationException("Invalid " + property + ": " + (value ?? string.Empty));
            return parsed;
        }
    }
}
