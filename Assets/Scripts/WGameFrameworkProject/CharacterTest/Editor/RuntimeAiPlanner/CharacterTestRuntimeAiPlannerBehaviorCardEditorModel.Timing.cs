using System;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        private static CharacterTestCharacterActionRuntime s_characterActionRuntime;

        public static CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview BuildFlowTimePreview(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var preview = new CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview();
            if (draft == null)
                return preview;

            EnsureStructuredBranches(draft);

            int cursor = 0;
            int unknownCount = 0;
            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                StepDurationInfo duration = ResolveStepDuration(step);
                if (!duration.Known)
                    unknownCount++;

                int durationFrames = duration.DurationFrames > 0
                    ? duration.DurationFrames
                    : UnknownDurationFrames;
                int start = cursor;
                int end = start + durationFrames;
                bool hasWindow = step.ConditionWindowEndFrame >= step.ConditionWindowStartFrame
                    && step.ConditionWindowEndFrame > 0;
                preview.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview(
                    step.StepId,
                    step.DisplayTitle,
                    step.Kind,
                    start,
                    end,
                    durationFrames,
                    duration.Known,
                    duration.Source,
                    hasWindow,
                    start + step.ConditionWindowStartFrame,
                    start + step.ConditionWindowEndFrame,
                    step.ConditionWindowSummary,
                    duration.Warning,
                    duration.TimelineSummary));
                cursor = end;
            }

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing =
                    FindStepPreview(preview, step.StepId);
                AddBranchPreviews(draft, preview, step, timing);
            }

            preview.Terminals.Add(new CharacterTestRuntimeAiPlannerBehaviorCardTerminalPreview(
                "End",
                "行为结束，交回行为组重新决策",
                returnsToBehaviorGroup: true,
                CountTerminalBranches(preview)));
            preview.TotalFrames = Math.Max(cursor, 1);
            preview.SummaryText = draft.DisplayName
                + " | "
                + preview.Steps.Count.ToString(CultureInfo.InvariantCulture)
                + " steps | "
                + preview.Branches.Count.ToString(CultureInfo.InvariantCulture)
                + " branches | End returns to behavior group";
            preview.WarningText = unknownCount == 0
                ? string.Empty
                : unknownCount.ToString(CultureInfo.InvariantCulture) + " step duration unknown.";
            return preview;
        }

        public static string FormatTimelinePreview(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return "未选择步骤。";

            StepDurationInfo duration = ResolveStepDuration(step);
            string durationText = duration.Known
                ? duration.DurationFrames.ToString(CultureInfo.InvariantCulture) + " frames"
                : "? frames";
            string windowText = step.ConditionWindowEndFrame > 0
                ? "判断窗口 frames "
                    + step.ConditionWindowStartFrame.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + step.ConditionWindowEndFrame.ToString(CultureInfo.InvariantCulture)
                    + " | "
                    + step.ConditionWindowSummary
                : "无显式判断窗口";
            string resolvedPreview = CharacterTestRuntimeAiPlannerBehaviorCardStepDraft.FormatKind(step.Kind)
                + " | "
                + step.TargetSummary
                + " | "
                + durationText
                + " | source="
                + duration.Source
                + " | "
                + windowText
                + " | "
                + duration.TimelineSummary
                + (string.IsNullOrWhiteSpace(duration.Warning) ? string.Empty : " | warning=" + duration.Warning);
            if (!string.IsNullOrWhiteSpace(resolvedPreview))
                return resolvedPreview;

            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return step.AbilityId > 0
                        ? "Ability "
                            + step.AbilityId.ToString(CultureInfo.InvariantCulture)
                            + " | 只读预览: request / root motion / VFX / CharacterAction binding 由 Ability 配置决定。"
                        : "未绑定 / 未找到 Ability。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return step.CharacterActionId.IsValid
                        ? "CharacterAction "
                            + step.CharacterActionId
                            + " | 只读预览: 切换窗口 / hit trace / gameplay request / VFX 由 CharacterAction 决定。"
                        : "未绑定 / 未找到 CharacterAction。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return "移动步骤没有动作时间轴；只预览目标距离带和路径失败处理。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return "等待步骤没有动作时间轴；只预览等待后分支。";
                default:
                    return "End 步骤没有动作时间轴。";
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview FindStepPreview(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            if (preview == null || string.IsNullOrWhiteSpace(stepId))
                return default;

            for (int i = 0; i < preview.Steps.Count; i++)
            {
                if (string.Equals(preview.Steps[i].StepId, stepId, StringComparison.Ordinal))
                    return preview.Steps[i];
            }

            return default;
        }

        private static void AddBranchPreviews(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = FindBranchByPriority(step, preview);
                if (branch == null)
                    break;

                AddStructuredBranchPreview(draft, preview, step, timing, branch);
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft FindBranchByPriority(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft best = null;
            for (int i = 0; step != null && i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft candidate = step.Branches[i];
                if (candidate == null || BranchAlreadyPreviewed(preview, candidate.BranchId))
                    continue;

                if (best == null
                    || candidate.Priority < best.Priority
                    || (candidate.Priority == best.Priority
                        && string.CompareOrdinal(candidate.BranchId, best.BranchId) < 0))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool BranchAlreadyPreviewed(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string branchId)
        {
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                if (string.Equals(preview.Branches[i].BranchId, branchId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AddStructuredBranchPreview(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch)
        {
            string target = string.IsNullOrWhiteSpace(branch.TargetStepId) ? "End" : branch.TargetStepId;
            bool targetIsEnd = string.Equals(target, "End", StringComparison.Ordinal);
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft targetStep = targetIsEnd
                ? null
                : FindStep(draft, target);
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind = ToPreviewBranchKind(branch.TriggerType);
            int windowStart = timing.StartFrame + Math.Max(0, branch.WindowStartFrame);
            int windowEnd = branch.WindowEndFrame > 0
                ? timing.StartFrame + branch.WindowEndFrame
                : timing.EndFrame;
            string condition = string.IsNullOrWhiteSpace(branch.ConditionSummary)
                ? branch.Label
                : branch.ConditionSummary;

            preview.Branches.Add(new CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview(
                branch.BranchId,
                step.StepId,
                step.DisplayTitle,
                kind,
                branch.TriggerType,
                branch.Label,
                target,
                targetIsEnd ? "End" : (targetStep != null ? targetStep.DisplayTitle : target),
                targetIsEnd,
                windowStart,
                windowEnd,
                condition,
                branch.Priority,
                targetIsEnd ? "End returns to behavior group." : string.Empty));
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchKind ToPreviewBranchKind(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType)
        {
            switch (triggerType)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure:
                    return CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure;
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition:
                    return CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet;
                default:
                    return CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success;
            }
        }

        private static void AddBranchPreview(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind,
            string targetStepId)
        {
            string target = string.IsNullOrWhiteSpace(targetStepId) ? "End" : targetStepId;
            bool targetIsEnd = string.Equals(target, "End", StringComparison.Ordinal);
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft targetStep = targetIsEnd
                ? null
                : FindStep(draft, target);
            int windowStart = timing.StartFrame;
            int windowEnd = timing.EndFrame;
            string condition = step.ConditionSummary;
            if (kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet
                && timing.HasConditionWindow)
            {
                windowStart = timing.ConditionWindowStartFrame;
                windowEnd = timing.ConditionWindowEndFrame;
                condition = string.IsNullOrWhiteSpace(step.ConditionWindowSummary)
                    ? step.ConditionSummary
                    : step.ConditionWindowSummary;
            }

            preview.Branches.Add(new CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview(
                step.StepId,
                step.DisplayTitle,
                kind,
                target,
                targetIsEnd ? "End" : (targetStep != null ? targetStep.DisplayTitle : target),
                targetIsEnd,
                windowStart,
                windowEnd,
                condition,
                targetIsEnd ? "终点会交回行为组重新决策" : string.Empty));
        }

        private static int CountTerminalBranches(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            int count = 0;
            if (preview == null)
                return count;

            for (int i = 0; i < preview.Branches.Count; i++)
            {
                if (preview.Branches[i].TargetIsEnd)
                    count++;
            }

            return count;
        }

        private static StepDurationInfo ResolveStepDuration(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return StepDurationInfo.Unknown("missing step");

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait)
            {
                int wait = Math.Max(0, step.WaitFrames);
                return new StepDurationInfo(
                    true,
                    wait,
                    "Editor wait frames",
                    string.Empty,
                    "Wait for " + wait.ToString(CultureInfo.InvariantCulture) + " frames.");
            }

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget)
            {
                int estimated = step.EstimatedDurationFrames > 0
                    ? step.EstimatedDurationFrames
                    : UnknownDurationFrames;
                return new StepDurationInfo(
                    false,
                    estimated,
                    "Editor estimate",
                    "移动步骤没有固定动作时长",
                    "MoveToTarget lasts until condition met, timeout, or path failure.");
            }

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.End)
                return new StepDurationInfo(true, 0, "Terminal", string.Empty, step.EndReason);

            CharacterActionConfig action = null;
            string source = string.Empty;
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                if (TryResolveAbilityAction(step.AbilityId, out action))
                    source = "Ability binding -> CharacterAction " + action.ActionId;
            }
            else if (step.CharacterActionId.IsValid
                && TryResolveAction(step.CharacterActionId, out action))
            {
                source = "CharacterAction " + action.ActionId;
            }

            if (action == null)
            {
                int estimated = step.EstimatedDurationFrames > 0
                    ? step.EstimatedDurationFrames
                    : UnknownDurationFrames;
                return new StepDurationInfo(
                    false,
                    estimated,
                    "Unknown",
                    "动作时长未知：未找到 CharacterAction/Ability 绑定",
                    "No CharacterAction timeline is available for preview.");
            }

            int frames = action.DurationFrames.GetValueOrDefault();
            if (frames <= 0)
            {
                frames = step.EstimatedDurationFrames > 0
                    ? step.EstimatedDurationFrames
                    : UnknownDurationFrames;
            }

            bool known = action.DurationFrames.HasValue && action.DurationFrames.Value > 0;
            return new StepDurationInfo(
                known,
                frames,
                string.IsNullOrWhiteSpace(source) ? "CharacterAction " + action.ActionId : source,
                known ? string.Empty : "CharacterAction durationFrames 缺失",
                FormatActionTimelineSummary(action));
        }

        private static bool TryResolveAbilityAction(int abilityId, out CharacterActionConfig action)
        {
            action = null;
            CharacterTestCharacterActionRuntime runtime = GetCharacterActionRuntime();
            if (abilityId <= 0 || runtime == null || runtime.ActionSet == null)
                return false;

            CharacterAbilityActionBinding[] bindings = runtime.ActionSet.AbilityBindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = bindings[i];
                if (binding.AbilityId == abilityId
                    && TryResolveAction(binding.ActionId, out action))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveAction(CharacterActionId actionId, out CharacterActionConfig action)
        {
            action = null;
            CharacterTestCharacterActionRuntime runtime = GetCharacterActionRuntime();
            return runtime != null
                && runtime.Registry != null
                && runtime.Registry.TryGetAction(actionId, out action);
        }

        private static CharacterTestCharacterActionRuntime GetCharacterActionRuntime()
        {
            if (s_characterActionRuntime != null)
                return s_characterActionRuntime;

            try
            {
                s_characterActionRuntime = CharacterTestCharacterActionConfigSource.Load();
            }
            catch (Exception)
            {
                s_characterActionRuntime = null;
            }

            return s_characterActionRuntime;
        }

        private static string FormatActionTimelineSummary(CharacterActionConfig action)
        {
            if (action == null)
                return string.Empty;

            string cancel = action.CancelRules.Length == 0
                ? "no transition window"
                : "transition "
                    + action.CancelRules[0].StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + action.CancelRules[0].EndFrame.ToString(CultureInfo.InvariantCulture);
            return "duration="
                + (action.DurationFrames.HasValue
                    ? action.DurationFrames.Value.ToString(CultureInfo.InvariantCulture)
                    : "?")
                + " | "
                + cancel
                + " | combat events="
                + action.CombatTrack.Events.Length.ToString(CultureInfo.InvariantCulture);
        }

        private readonly struct StepDurationInfo
        {
            public StepDurationInfo(
                bool known,
                int durationFrames,
                string source,
                string warning,
                string timelineSummary)
            {
                Known = known;
                DurationFrames = Math.Max(0, durationFrames);
                Source = source ?? string.Empty;
                Warning = warning ?? string.Empty;
                TimelineSummary = timelineSummary ?? string.Empty;
            }

            public bool Known { get; }
            public int DurationFrames { get; }
            public string Source { get; }
            public string Warning { get; }
            public string TimelineSummary { get; }

            public static StepDurationInfo Unknown(string warning)
            {
                return new StepDurationInfo(
                    false,
                    UnknownDurationFrames,
                    "Unknown",
                    warning,
                    string.Empty);
            }
        }
    }
}
