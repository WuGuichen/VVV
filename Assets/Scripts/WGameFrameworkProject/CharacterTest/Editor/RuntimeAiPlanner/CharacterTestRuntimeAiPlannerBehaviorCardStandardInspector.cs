using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind
    {
        DashAbility = 0,
        Ability = 1,
        MeleeAttack = 2,
        ComboFollowUp = 3,
        MoveToTarget = 4,
        Wait = 5,
        EndReturn = 6
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind
    {
        TargetExists = 0,
        TargetVisible = 1,
        DistanceCompare = 2,
        TargetLost = 3,
        SelfIdle = 4,
        AbilityAvailable = 5,
        CharacterActionTransitionWindowAllowed = 6
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind
    {
        RunningMonitor = 0,
        Completion = 1,
        Failure = 2
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView(
            string displayName,
            string tooltip)
        {
            DisplayName = displayName ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
        }

        public string DisplayName { get; }
        public string Tooltip { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
            CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind kind,
            string label,
            string value)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind Kind { get; }
        public string Label { get; }
        public string Value { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView()
        {
            Presets = new List<CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView>();
            OperatorText = "AND";
            SourceSummary = string.Empty;
            IsMainEditorCompatible = true;
            WarningText = string.Empty;
        }

        public string OperatorText { get; set; }
        public string SourceSummary { get; set; }
        public bool IsMainEditorCompatible { get; set; }
        public string WarningText { get; set; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView> Presets { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardStepExitView
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardStepExitView(
            CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind kind,
            string branchId,
            string label,
            string conditionText,
            string targetStepId,
            string targetText,
            string windowText)
        {
            Kind = kind;
            BranchId = branchId ?? string.Empty;
            Label = label ?? string.Empty;
            ConditionText = conditionText ?? string.Empty;
            TargetStepId = string.IsNullOrWhiteSpace(targetStepId) ? "End" : targetStepId;
            TargetText = string.IsNullOrWhiteSpace(targetText) ? TargetStepId : targetText;
            WindowText = windowText ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind Kind { get; }
        public string BranchId { get; }
        public string Label { get; }
        public string ConditionText { get; }
        public string TargetStepId { get; }
        public string TargetText { get; }
        public string WindowText { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView()
        {
            TemplateName = string.Empty;
            TemplateDescription = string.Empty;
            ActionLabel = string.Empty;
            ActionValue = string.Empty;
            DurationText = string.Empty;
            EntryConditions = new CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView();
            RunningExits = new List<CharacterTestRuntimeAiPlannerBehaviorCardStepExitView>();
            CompletionExit = default;
            FailureExit = default;
            SourceConditionSummary = string.Empty;
        }

        public CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind TemplateKind { get; set; }
        public string TemplateName { get; set; }
        public string TemplateDescription { get; set; }
        public string ActionLabel { get; set; }
        public string ActionValue { get; set; }
        public string DurationText { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView EntryConditions { get; set; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardStepExitView> RunningExits { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardStepExitView CompletionExit { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorCardStepExitView FailureExit { get; set; }
        public string SourceConditionSummary { get; set; }
    }

    internal static class CharacterTestRuntimeAiPlannerBehaviorCardStandardInspector
    {
        public static CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView BuildStepView(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            var view = new CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView();
            if (draft == null || step == null)
                return view;

            CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.EnsureStructuredBranches(draft);
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildFlowTimePreview(draft);
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing =
                FindTiming(preview, step.StepId);

            view.TemplateKind = InferTemplateKind(step);
            view.TemplateName = FormatTemplateName(view.TemplateKind);
            view.TemplateDescription = FormatTemplateDescription(view.TemplateKind);
            view.ActionLabel = FormatActionLabel(step);
            view.ActionValue = FormatActionValue(step);
            view.DurationText = FormatDuration(timing, step);
            view.EntryConditions = BuildEntryConditions(step);
            view.SourceConditionSummary = step.ConditionSummary ?? string.Empty;

            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch == null)
                    continue;

                CharacterTestRuntimeAiPlannerBehaviorCardStepExitView exit = BuildExitView(branch);
                switch (branch.TriggerType)
                {
                    case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition:
                        view.RunningExits.Add(exit);
                        break;
                    case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure:
                        if (string.IsNullOrEmpty(view.FailureExit.BranchId)
                            || branch.Priority < FindBranchPriority(step, view.FailureExit.BranchId))
                        {
                            view.FailureExit = exit;
                        }
                        break;
                    default:
                        if (string.IsNullOrEmpty(view.CompletionExit.BranchId)
                            || branch.Priority < FindBranchPriority(step, view.CompletionExit.BranchId))
                        {
                            view.CompletionExit = exit;
                        }
                        break;
                }
            }

            view.RunningExits.Sort((left, right) =>
                FindBranchPriority(step, left.BranchId).CompareTo(FindBranchPriority(step, right.BranchId)));
            return view;
        }

        public static int CountRunningMonitorExits(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            int count = 0;
            if (step == null)
                return count;

            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch != null
                    && branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
                {
                    count++;
                }
            }

            return count;
        }

        public static bool HasCompletionExit(CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            return CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.FindFirstBranch(
                step,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess) != null;
        }

        public static bool HasFailureExit(CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            return CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.FindFirstBranch(
                step,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure) != null;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView BuildActionDisplay(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return new CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView("无动作", string.Empty);

            string raw = FormatActionLabel(step) + ": " + FormatActionValue(step);
            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return new CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView(
                        step.AbilityId == CharacterTestAbilityIds.DashStrikeAbilityId
                            ? "突进斩"
                            : "能力 " + step.AbilityId.ToString(CultureInfo.InvariantCulture),
                        raw);
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return new CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView(
                        FormatCharacterActionDisplayName(step.CharacterActionId),
                        raw);
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return new CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView("移动到目标", raw);
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return new CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView("等待", raw);
                default:
                    return new CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView(
                        "结束",
                        raw);
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind InferTemplateKind(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.Wait;

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.End)
                return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.EndReturn;
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget)
                return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.MoveToTarget;
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait)
                return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.Wait;
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                if (step.AbilityId == CharacterTestAbilityIds.DashStrikeAbilityId
                    || string.Equals(step.StepId, "dash", StringComparison.Ordinal)
                    || string.Equals(step.StepId, "second_dash", StringComparison.Ordinal))
                {
                    return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.DashAbility;
                }

                return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.Ability;
            }

            if (step.CharacterActionId == new CharacterActionId(324))
                return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.ComboFollowUp;

            return CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.MeleeAttack;
        }

        private static string FormatTemplateName(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.DashAbility:
                    return "突进能力";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.Ability:
                    return "普通能力";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.MeleeAttack:
                    return "近身攻击";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.ComboFollowUp:
                    return "连段后续";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.MoveToTarget:
                    return "移动接近";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.EndReturn:
                    return "End 回流";
                default:
                    return "等待";
            }
        }

        private static string FormatTemplateDescription(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.DashAbility:
                    return "请求稳定 Ability，执行中监听距离，完成后进入补救或回流。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.Ability:
                    return "请求指定 Ability，Ability 与动作表现由运行时绑定决定。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.MeleeAttack:
                    return "请求近身 CharacterAction，监听连段窗口和目标距离。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.ComboFollowUp:
                    return "请求连段后续 CharacterAction，完成后显式回流。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.MoveToTarget:
                    return "持续移动到目标，直到距离、路径或超时条件改变。";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.EndReturn:
                    return "结束当前行为卡，交回行为组重新决策。";
                default:
                    return "等待固定帧数或等待下一次行为组决策。";
            }
        }

        private static string FormatActionLabel(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return "动作";

            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return "Ability";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return "CharacterAction";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return "移动目标";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return "等待";
                default:
                    return "结束";
            }
        }

        private static string FormatActionValue(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return string.Empty;

            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return step.AbilityId == CharacterTestAbilityIds.DashStrikeAbilityId
                        ? "DashStrikeAbilityId"
                        : step.AbilityId.ToString(CultureInfo.InvariantCulture);
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return !step.CharacterActionId.IsValid
                        ? "未绑定 CharacterAction"
                        : step.CharacterActionId.ToString();
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return string.IsNullOrWhiteSpace(step.MoveDistanceBand)
                        ? "目标距离"
                        : step.MoveDistanceBand;
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return step.WaitFrames.ToString(CultureInfo.InvariantCulture) + " frames";
                default:
                    return "交回行为组重新决策";
            }
        }

        private static string FormatDuration(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (timing.StepId.Length > 0)
            {
                string duration = timing.DurationKnown
                    ? timing.DurationFrames.ToString(CultureInfo.InvariantCulture) + " frames"
                    : "? frames";
                if (string.IsNullOrWhiteSpace(timing.DurationSource))
                    return duration;

                return duration + " | " + timing.DurationSource;
            }

            return Math.Max(0, step != null ? step.EstimatedDurationFrames : 0)
                .ToString(CultureInfo.InvariantCulture)
                + " frames";
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView BuildEntryConditions(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            var group = new CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView
            {
                SourceSummary = step != null ? step.ConditionSummary ?? string.Empty : string.Empty
            };
            group.IsMainEditorCompatible = IsAndOnlyCondition(group.SourceSummary);
            if (!group.IsMainEditorCompatible)
                group.WarningText = "主编辑只支持 AND 条件；复杂表达式暂不开放。";

            if (step == null)
                return group;

            AddTargetPresets(group, step);
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                group.Presets.Add(new CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
                    CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind.AbilityAvailable,
                    "Ability 可用",
                    FormatActionValue(step)));
            }

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction)
            {
                group.Presets.Add(new CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
                    CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind.CharacterActionTransitionWindowAllowed,
                    "动作窗口允许",
                    step.CharacterActionId.Value.ToString(CultureInfo.InvariantCulture)));
            }

            return group;
        }

        private static void AddTargetPresets(
            CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView group,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            group.Presets.Add(new CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
                CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind.TargetExists,
                "目标存在",
                "target.exists"));

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility
                || step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget)
            {
                group.Presets.Add(new CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
                    CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind.TargetVisible,
                    "目标可见",
                    "target.visible"));
            }

            string distance = InferDistancePreset(step);
            if (!string.IsNullOrWhiteSpace(distance))
            {
                group.Presets.Add(new CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
                    CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind.DistanceCompare,
                    "距离条件",
                    distance));
            }

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                group.Presets.Add(new CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView(
                    CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetKind.SelfIdle,
                    "自身空闲",
                    "self idle"));
            }
        }

        private static string InferDistancePreset(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return string.Empty;

            if (string.Equals(step.StepId, "dash", StringComparison.Ordinal))
                return "3m <= target.distance <= 7m";
            if (string.Equals(step.StepId, "second_dash", StringComparison.Ordinal))
                return "1m < target.distance <= 7m";
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction)
                return "target.distance <= 1m";
            if (!string.IsNullOrWhiteSpace(step.MoveDistanceBand))
                return step.MoveDistanceBand;

            return string.Empty;
        }

        private static string FormatCharacterActionDisplayName(CharacterActionId characterActionId)
        {
            if (!characterActionId.IsValid)
                return "未绑定动作";
            return characterActionId.ToString();
        }

        private static bool IsAndOnlyCondition(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return true;

            return source.IndexOf("||", StringComparison.Ordinal) < 0
                && source.IndexOf(" or ", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepExitView BuildExitView(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind kind =
                CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.Completion;
            if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
                kind = CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.RunningMonitor;
            else if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure)
                kind = CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.Failure;

            return new CharacterTestRuntimeAiPlannerBehaviorCardStepExitView(
                kind,
                branch.BranchId,
                string.IsNullOrWhiteSpace(branch.Label)
                    ? CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft.FormatTriggerType(branch.TriggerType)
                    : branch.Label,
                branch.ConditionSummary,
                branch.TargetStepId,
                FormatTargetText(branch.TargetStepId),
                FormatWindow(branch.WindowStartFrame, branch.WindowEndFrame));
        }

        private static string FormatTargetText(string targetStepId)
        {
            if (string.IsNullOrWhiteSpace(targetStepId) || string.Equals(targetStepId, "End", StringComparison.Ordinal))
                return "End / 交回行为组重新决策";

            return targetStepId;
        }

        private static string FormatWindow(int startFrame, int endFrame)
        {
            if (endFrame <= 0)
                return "无固定窗口";

            return startFrame.ToString(CultureInfo.InvariantCulture)
                + "-"
                + endFrame.ToString(CultureInfo.InvariantCulture)
                + "f";
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview FindTiming(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            if (preview == null || string.IsNullOrWhiteSpace(stepId))
                return default;

            for (int i = 0; i < preview.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing = preview.Steps[i];
                if (string.Equals(timing.StepId, stepId, StringComparison.Ordinal))
                    return timing;
            }

            return default;
        }

        private static int FindBranchPriority(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            string branchId)
        {
            if (step == null || string.IsNullOrWhiteSpace(branchId))
                return int.MaxValue;

            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch != null && string.Equals(branch.BranchId, branchId, StringComparison.Ordinal))
                    return branch.Priority;
            }

            return int.MaxValue;
        }
    }
}
