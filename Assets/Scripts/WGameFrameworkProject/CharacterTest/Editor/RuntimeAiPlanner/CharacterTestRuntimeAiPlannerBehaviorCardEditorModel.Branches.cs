using System;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public static void EnsureStructuredBranches(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            if (draft == null)
                return;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null
                    || step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.End
                    || step.Branches.Count > 0)
                {
                    continue;
                }

                string successTarget = string.IsNullOrWhiteSpace(step.OnSuccess) ? "End" : step.OnSuccess;
                string failureTarget = string.IsNullOrWhiteSpace(step.OnFailure) ? "End" : step.OnFailure;
                string conditionTarget = string.IsNullOrWhiteSpace(step.OnConditionMet) ? "End" : step.OnConditionMet;

                step.Branches.Add(CreateBranch(
                    step.StepId,
                    "default",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                    successTarget,
                    "Default",
                    "Step completed.",
                    step.ConditionWindowStartFrame,
                    step.ConditionWindowEndFrame,
                    100));
                step.Branches.Add(CreateBranch(
                    step.StepId,
                    "failure",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure,
                    failureTarget,
                    "Failure",
                    "Ability unavailable, interrupted, target lost, or step failed.",
                    0,
                    Math.Max(0, step.EstimatedDurationFrames),
                    0));
                step.Branches.Add(CreateBranch(
                    step.StepId,
                    "condition",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                    conditionTarget,
                    "Condition",
                    string.IsNullOrWhiteSpace(step.ConditionWindowSummary)
                        ? step.ConditionSummary
                        : step.ConditionWindowSummary,
                    step.ConditionWindowStartFrame,
                    step.ConditionWindowEndFrame,
                    10));
            }
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft CreateBranch(
            string stepId,
            string suffix,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType,
            string targetStepId,
            string label,
            string conditionSummary,
            int windowStartFrame,
            int windowEndFrame,
            int priority)
        {
            var branch = new CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft(
                CreateBranchId(stepId, suffix),
                triggerType,
                targetStepId)
            {
                Label = string.IsNullOrWhiteSpace(label)
                    ? CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft.FormatTriggerType(triggerType)
                    : label,
                ConditionSummary = conditionSummary ?? string.Empty,
                WindowStartFrame = Math.Max(0, windowStartFrame),
                WindowEndFrame = Math.Max(Math.Max(0, windowStartFrame), windowEndFrame),
                Priority = priority
            };
            return branch;
        }

        public static string CreateBranchId(string stepId, string suffix)
        {
            string owner = string.IsNullOrWhiteSpace(stepId) ? "step" : stepId;
            string idSuffix = string.IsNullOrWhiteSpace(suffix) ? "branch" : suffix;
            return owner + "." + idSuffix;
        }

        public static string GetDefaultSuccessTarget(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch =
                FindFirstBranch(step, CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess);
            if (branch != null && !string.IsNullOrWhiteSpace(branch.TargetStepId))
                return branch.TargetStepId;

            return string.IsNullOrWhiteSpace(step != null ? step.OnSuccess : null)
                ? "End"
                : step.OnSuccess;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft FindFirstBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType)
        {
            if (step == null)
                return null;

            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft best = null;
            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch == null || branch.TriggerType != triggerType)
                    continue;

                if (best == null || branch.Priority < best.Priority)
                    best = branch;
            }

            return best;
        }
    }
}
