using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerBehaviorCardStepKind
    {
        UseAbility = 0,
        UseAction = 1,
        MoveToTarget = 2,
        Wait = 3,
        End = 4
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardStepDraft
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
            string stepId,
            CharacterTestRuntimeAiPlannerBehaviorCardStepKind kind)
        {
            StepId = stepId ?? string.Empty;
            Kind = kind;
            Title = string.Empty;
            ConditionSummary = string.Empty;
            OnSuccess = "End";
            OnFailure = "End";
            OnConditionMet = "End";
            AbilityId = 0;
            CharacterActionId = MxFramework.CharacterAction.CharacterActionId.None;
            MoveDistanceBand = string.Empty;
            WaitFrames = 0;
            EstimatedDurationFrames = 0;
            ConditionWindowStartFrame = 0;
            ConditionWindowEndFrame = 0;
            ConditionWindowSummary = string.Empty;
            EndReason = "交回行为组重新决策";
            Note = string.Empty;
            Branches = new List<CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft>();
        }

        public string StepId { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorCardStepKind Kind { get; set; }
        public string Title { get; set; }
        public string ConditionSummary { get; set; }
        public string OnSuccess { get; set; }
        public string OnFailure { get; set; }
        public string OnConditionMet { get; set; }
        public int AbilityId { get; set; }
        public CharacterActionId CharacterActionId { get; set; }
        public string MoveDistanceBand { get; set; }
        public int WaitFrames { get; set; }
        public int EstimatedDurationFrames { get; set; }
        public int ConditionWindowStartFrame { get; set; }
        public int ConditionWindowEndFrame { get; set; }
        public string ConditionWindowSummary { get; set; }
        public string EndReason { get; set; }
        public string Note { get; set; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft> Branches { get; }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                return FormatKind(Kind);
            }
        }

        public string TargetSummary
        {
            get
            {
                switch (Kind)
                {
                    case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                        return AbilityId > 0
                            ? "AbilityId " + AbilityId.ToString(CultureInfo.InvariantCulture)
                            : "未绑定 Ability";
                    case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                        return CharacterActionId.IsValid
                            ? CharacterActionId.ToString()
                            : "未绑定 CharacterAction";
                    case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                        return string.IsNullOrWhiteSpace(MoveDistanceBand)
                            ? "移动到目标"
                            : MoveDistanceBand;
                    case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                        return WaitFrames > 0
                            ? WaitFrames.ToString(CultureInfo.InvariantCulture) + " frames"
                            : "短暂停留";
                    default:
                        return "结束";
                }
            }
        }

        public static string FormatKind(CharacterTestRuntimeAiPlannerBehaviorCardStepKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return "UseAbility";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return "UseAction";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return "MoveToTarget";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return "Wait";
                default:
                    return "End";
            }
        }
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType
    {
        DefaultSuccess = 0,
        Condition = 1,
        Failure = 2
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft(
            string branchId,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType,
            string targetStepId)
        {
            BranchId = branchId ?? string.Empty;
            TriggerType = triggerType;
            Label = FormatTriggerType(triggerType);
            ConditionSummary = string.Empty;
            WindowStartFrame = 0;
            WindowEndFrame = 0;
            TargetStepId = string.IsNullOrWhiteSpace(targetStepId) ? "End" : targetStepId;
            Priority = triggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure
                ? 0
                : 100;
            Note = string.Empty;
        }

        public string BranchId { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType TriggerType { get; set; }
        public string Label { get; set; }
        public string ConditionSummary { get; set; }
        public int WindowStartFrame { get; set; }
        public int WindowEndFrame { get; set; }
        public string TargetStepId { get; set; }
        public int Priority { get; set; }
        public string Note { get; set; }

        public static string FormatTriggerType(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType)
        {
            switch (triggerType)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition:
                    return "Condition";
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure:
                    return "Failure";
                default:
                    return "Default";
            }
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardDraft
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardDraft(
            string cardId,
            string stableId,
            string displayName)
        {
            CardId = cardId ?? string.Empty;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = "Pursuit";
            CapabilityText = "Pursuit + Damage";
            IntentText = string.Empty;
            TriggerSummary = string.Empty;
            OutputSummary = string.Empty;
            RiskText = string.Empty;
            SourceText = "Editor sample draft";
            IsPublished = false;
            SavedRevision = 0;
            Steps = new List<CharacterTestRuntimeAiPlannerBehaviorCardStepDraft>();
        }

        public string CardId { get; set; }
        public string StableId { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string CapabilityText { get; set; }
        public string IntentText { get; set; }
        public string TriggerSummary { get; set; }
        public string OutputSummary { get; set; }
        public string RiskText { get; set; }
        public string SourceText { get; set; }
        public bool IsPublished { get; set; }
        public int SavedRevision { get; set; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardStepDraft> Steps { get; }
    }

}
