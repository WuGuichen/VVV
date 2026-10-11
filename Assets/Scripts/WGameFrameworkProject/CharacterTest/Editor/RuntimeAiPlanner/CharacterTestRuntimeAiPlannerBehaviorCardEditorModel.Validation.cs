using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public static List<string> Validate(CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var diagnostics = new List<string>();
            if (draft == null)
            {
                diagnostics.Add("缺少行为卡草稿。");
                return diagnostics;
            }

            EnsureStructuredBranches(draft);

            if (draft.Steps.Count < MinStepCount)
                diagnostics.Add("至少需要 1 个步骤。");
            if (draft.Steps.Count > MaxStepCount)
                diagnostics.Add("步骤超过上限 " + MaxStepCount.ToString(CultureInfo.InvariantCulture) + "。");

            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                {
                    diagnostics.Add("步骤 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.StepId))
                    diagnostics.Add("步骤 " + (i + 1).ToString(CultureInfo.InvariantCulture) + " 缺少 step id。");
                else if (!stepIds.Add(step.StepId))
                    diagnostics.Add("重复 step id: " + step.StepId);

                if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility
                    && step.AbilityId <= 0)
                {
                    diagnostics.Add(step.StepId + " 未绑定 Ability。");
                }

                if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction
                    && !step.CharacterActionId.IsValid)
                {
                    diagnostics.Add(step.StepId + " 未绑定 CharacterAction。");
                }
            }

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                ValidateBranch(stepIds, step.StepId, "成功后", step.OnSuccess, diagnostics);
                ValidateBranch(stepIds, step.StepId, "失败后", step.OnFailure, diagnostics);
                ValidateBranch(stepIds, step.StepId, "条件满足后", step.OnConditionMet, diagnostics);
            }

            ValidateStructuredBranchGraph(draft, stepIds, diagnostics);

            return diagnostics;
        }

        private static void ValidateStructuredBranchGraph(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            HashSet<string> stepIds,
            List<string> diagnostics)
        {
            if (draft == null)
                return;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                ValidateBranches(stepIds, step, diagnostics);
            }

            if (draft.Steps.Count == 0 || draft.Steps[0] == null)
                return;

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            DetectBranchCycle(draft, draft.Steps[0].StepId, visiting, visited, diagnostics);

            var reachVisited = new HashSet<string>(StringComparer.Ordinal);
            if (!CanReachEnd(draft, draft.Steps[0].StepId, reachVisited))
                diagnostics.Add("Behavior card has no reachable End path.");

            if (AllBranchesEnd(draft))
                diagnostics.Add("Warning: all configured branches go directly to End.");
        }

        private static void ValidateBranches(
            HashSet<string> stepIds,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            List<string> diagnostics)
        {
            if (step == null || step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.End)
                return;

            int defaultCount = 0;
            int failureCount = 0;
            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch == null)
                {
                    diagnostics.Add(step.StepId + " has null branch.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(branch.BranchId))
                    diagnostics.Add(step.StepId + " has branch without branch id.");
                else if (!branchIds.Add(branch.BranchId))
                    diagnostics.Add(step.StepId + " duplicate branch id: " + branch.BranchId);

                if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess)
                    defaultCount++;
                if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure)
                    failureCount++;

                ValidateBranch(stepIds, step.StepId, branch.BranchId, branch.TargetStepId, diagnostics);
            }

            if (defaultCount != 1)
                diagnostics.Add(step.StepId + " requires exactly one DefaultSuccess branch.");
            if (failureCount > 1)
                diagnostics.Add(step.StepId + " allows at most one Failure branch.");
        }

        private static bool DetectBranchCycle(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<string> diagnostics)
        {
            if (draft == null || string.IsNullOrWhiteSpace(stepId))
                return false;
            if (string.Equals(stepId, "End", StringComparison.Ordinal))
                return false;
            if (visiting.Contains(stepId))
            {
                diagnostics.Add("Branch cycle detected at step: " + stepId);
                return true;
            }
            if (visited.Contains(stepId))
                return false;

            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = FindStep(draft, stepId);
            if (step == null)
                return false;

            visiting.Add(stepId);
            bool found = false;
            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch == null || string.Equals(branch.TargetStepId, "End", StringComparison.Ordinal))
                    continue;

                found |= DetectBranchCycle(draft, branch.TargetStepId, visiting, visited, diagnostics);
            }

            visiting.Remove(stepId);
            visited.Add(stepId);
            return found;
        }

        private static bool CanReachEnd(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId,
            HashSet<string> visited)
        {
            if (draft == null || string.IsNullOrWhiteSpace(stepId))
                return false;
            if (string.Equals(stepId, "End", StringComparison.Ordinal))
                return true;
            if (!visited.Add(stepId))
                return false;

            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = FindStep(draft, stepId);
            if (step == null)
                return false;

            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch != null && CanReachEnd(draft, branch.TargetStepId, visited))
                    return true;
            }

            return false;
        }

        private static bool AllBranchesEnd(CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            if (draft == null)
                return false;

            bool sawBranch = false;
            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[branchIndex];
                    if (branch == null)
                        continue;

                    sawBranch = true;
                    if (!string.Equals(branch.TargetStepId, "End", StringComparison.Ordinal))
                        return false;
                }
            }

            return sawBranch;
        }

        private static void ValidateBranch(
            HashSet<string> stepIds,
            string owner,
            string branchName,
            string target,
            List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(target)
                || string.Equals(target, "End", StringComparison.Ordinal))
            {
                return;
            }

            if (!stepIds.Contains(target))
                diagnostics.Add(owner + " " + branchName + " 指向不存在步骤 " + target);
        }
    }
}
