using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardForecastRow
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardForecastRow(
            string scenario,
            string expectedStep,
            string output,
            string notice)
        {
            Scenario = scenario ?? string.Empty;
            ExpectedStep = expectedStep ?? string.Empty;
            Output = output ?? string.Empty;
            Notice = notice ?? string.Empty;
        }

        public string Scenario { get; }
        public string ExpectedStep { get; }
        public string Output { get; }
        public string Notice { get; }
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorCardBranchKind
    {
        Success = 0,
        Failure = 1,
        ConditionMet = 2
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview(
            string stepId,
            string title,
            CharacterTestRuntimeAiPlannerBehaviorCardStepKind kind,
            int startFrame,
            int endFrame,
            int durationFrames,
            bool durationKnown,
            string durationSource,
            bool hasConditionWindow,
            int conditionWindowStartFrame,
            int conditionWindowEndFrame,
            string conditionWindowSummary,
            string warningText,
            string timelineSummary)
        {
            StepId = stepId ?? string.Empty;
            Title = title ?? string.Empty;
            Kind = kind;
            StartFrame = Math.Max(0, startFrame);
            EndFrame = Math.Max(StartFrame, endFrame);
            DurationFrames = Math.Max(0, durationFrames);
            DurationKnown = durationKnown;
            DurationSource = durationSource ?? string.Empty;
            HasConditionWindow = hasConditionWindow;
            ConditionWindowStartFrame = Math.Max(0, conditionWindowStartFrame);
            ConditionWindowEndFrame = Math.Max(ConditionWindowStartFrame, conditionWindowEndFrame);
            ConditionWindowSummary = conditionWindowSummary ?? string.Empty;
            WarningText = warningText ?? string.Empty;
            TimelineSummary = timelineSummary ?? string.Empty;
        }

        public string StepId { get; }
        public string Title { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardStepKind Kind { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public int DurationFrames { get; }
        public bool DurationKnown { get; }
        public string DurationSource { get; }
        public bool HasConditionWindow { get; }
        public int ConditionWindowStartFrame { get; }
        public int ConditionWindowEndFrame { get; }
        public string ConditionWindowSummary { get; }
        public string WarningText { get; }
        public string TimelineSummary { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview(
            string fromStepId,
            string fromTitle,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind,
            string targetStepId,
            string targetTitle,
            bool targetIsEnd,
            int windowStartFrame,
            int windowEndFrame,
            string conditionSummary,
            string riskText)
            : this(
                fromStepId + "." + kind.ToString(),
                fromStepId,
                fromTitle,
                kind,
                kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure
                    ? CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure
                    : (kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet
                        ? CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition
                        : CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess),
                FormatDefaultBranchLabel(kind),
                targetStepId,
                targetTitle,
                targetIsEnd,
                windowStartFrame,
                windowEndFrame,
                conditionSummary,
                100,
                riskText)
        {
        }

        public CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview(
            string branchId,
            string fromStepId,
            string fromTitle,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType,
            string label,
            string targetStepId,
            string targetTitle,
            bool targetIsEnd,
            int windowStartFrame,
            int windowEndFrame,
            string conditionSummary,
            int priority,
            string riskText)
        {
            BranchId = branchId ?? string.Empty;
            FromStepId = fromStepId ?? string.Empty;
            FromTitle = fromTitle ?? string.Empty;
            Kind = kind;
            TriggerType = triggerType;
            Label = label ?? string.Empty;
            TargetStepId = targetStepId ?? string.Empty;
            TargetTitle = targetTitle ?? string.Empty;
            TargetIsEnd = targetIsEnd;
            WindowStartFrame = Math.Max(0, windowStartFrame);
            WindowEndFrame = Math.Max(WindowStartFrame, windowEndFrame);
            ConditionSummary = conditionSummary ?? string.Empty;
            Priority = priority;
            RiskText = riskText ?? string.Empty;
        }

        public string BranchId { get; }
        public string FromStepId { get; }
        public string FromTitle { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchKind Kind { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType TriggerType { get; }
        public string Label { get; }
        public string TargetStepId { get; }
        public string TargetTitle { get; }
        public bool TargetIsEnd { get; }
        public int WindowStartFrame { get; }
        public int WindowEndFrame { get; }
        public string ConditionSummary { get; }
        public int Priority { get; }
        public string RiskText { get; }

        private static string FormatDefaultBranchLabel(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure:
                    return "Failure";
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet:
                    return "Condition";
                default:
                    return "Default";
            }
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardTerminalPreview
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardTerminalPreview(
            string terminalId,
            string reason,
            bool returnsToBehaviorGroup,
            int incomingBranchCount)
        {
            TerminalId = terminalId ?? string.Empty;
            Reason = reason ?? string.Empty;
            ReturnsToBehaviorGroup = returnsToBehaviorGroup;
            IncomingBranchCount = Math.Max(0, incomingBranchCount);
        }

        public string TerminalId { get; }
        public string Reason { get; }
        public bool ReturnsToBehaviorGroup { get; }
        public int IncomingBranchCount { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview()
        {
            Steps = new List<CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview>();
            Branches = new List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview>();
            Terminals = new List<CharacterTestRuntimeAiPlannerBehaviorCardTerminalPreview>();
            SummaryText = string.Empty;
            WarningText = string.Empty;
        }

        public List<CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview> Steps { get; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview> Branches { get; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardTerminalPreview> Terminals { get; }
        public int TotalFrames { get; set; }
        public string SummaryText { get; set; }
        public string WarningText { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep(
            string stepId,
            int stepIndex,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview mainBranch)
        {
            StepId = stepId ?? string.Empty;
            StepIndex = Math.Max(0, stepIndex);
            MainBranch = mainBranch;
            LeftBranches = new List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview>();
            RightBranches = new List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview>();
        }

        public string StepId { get; }
        public int StepIndex { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview MainBranch { get; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview> LeftBranches { get; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview> RightBranches { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel()
        {
            Steps = new List<CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep>();
            MainPathStepIds = new List<string>();
            EndIncomingBranchCount = 0;
        }

        public List<CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep> Steps { get; }
        public List<string> MainPathStepIds { get; }
        public int EndIncomingBranchCount { get; set; }
    }

}
