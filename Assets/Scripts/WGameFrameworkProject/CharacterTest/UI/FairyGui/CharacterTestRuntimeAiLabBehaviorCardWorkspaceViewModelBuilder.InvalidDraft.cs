using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder
    {
        private static void FillInvalidLogicDraftView(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            string cardName,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            int selectedIndex,
            CharacterTestRuntimeAiLabLogicElementKind selectedKind,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            long structureKey = ComputeInvalidDraftStructureKey(cardName, payload, diagnostics);
            bool rebuildStructure = !timeline.HasCachedStructure
                || timeline.CachedStructureKey != structureKey;
            bool diagnosticResolved = TryResolvePrimaryBlockingDiagnostic(
                payload,
                diagnostics,
                out CharacterTestRuntimeAiLabCardDiagnostic primaryDiagnostic,
                out CharacterTestRuntimeAiLabLogicElementKind diagnosticKind,
                out int diagnosticIndex);
            if (diagnosticResolved
                && timeline.Presentation.SelectedRecipeEventKind == BehaviorRecipeEventKind.None
                && (!timeline.HasAutoFocusedDiagnosticKey
                    || timeline.AutoFocusedDiagnosticKey != structureKey))
            {
                selectedKind = diagnosticKind;
                selectedIndex = diagnosticIndex;
                timeline.AutoFocusedDiagnosticKey = structureKey;
                timeline.HasAutoFocusedDiagnosticKey = true;
            }
            NormalizeInvalidDraftSelection(payload, ref selectedKind, ref selectedIndex);
            timeline.SelectedElementKind = selectedKind;
            timeline.SelectedElementIndex = selectedIndex;
            EnsureTimelineCapacity(
                timeline,
                payload.Stages.Count,
                payload.Detectors.Count,
                payload.Transitions.Count);
            timeline.Available = true;
            timeline.StageCount = payload.Stages.Count;
            timeline.DetectorCount = payload.Detectors.Count;
            timeline.TransitionCount = payload.Transitions.Count;
            if (rebuildStructure)
            {
                timeline.CardTitleText = NonEmpty(cardName, "行为卡");
                timeline.ActivationConditionCount = payload.ActivationConditions?.Count ?? 0;
                timeline.ActivationConditionSummaryText = timeline.ActivationConditionCount > 0
                    ? timeline.ActivationConditionCount + " 条行为激活条件"
                    : "无额外行为激活条件";
                timeline.SemanticHashText = "hash --------";
                timeline.SummaryText = "逻辑编译失败；当前运行版本保持不变。";
                timeline.DurationFrames = ResolveLogicTimelineDuration(payload);
                timeline.DiagnosticsText = BuildCompileDiagnostics(diagnostics);
            }
            timeline.RuntimeStatusText = diagnosticResolved
                ? BuildPrimaryRepairInstruction(primaryDiagnostic, diagnosticKind)
                : "草稿存在阻断问题；请查看诊断并修复后再应用到下一帧。";
            timeline.HasBlockingDiagnostics = true;
            timeline.ValidationBadgeText = "存在阻断问题";
            CharacterTestRuntimeAiLabLogicStageViewModel[] stageRows = timeline.Stages;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                CharacterTestRuntimeAiLabLogicStageViewModel row = stageRows[i];
                if (stage == null)
                {
                    if (rebuildStructure)
                        FillMissingDraftStageRow(row, i);
                    row.Selected = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                        && i == selectedIndex;
                    row.ActionConflict = true;
                    continue;
                }
                if (rebuildStructure)
                {
                    CharacterTestRuntimeAiLabLogicStageLayout layout = FindLogicLayout(payload, stage.StageId);
                    row.Index = i;
                    row.Order = stage.Order;
                    row.Column = layout?.Column ?? i;
                    row.Width = layout?.Width ?? 1f;
                    row.Offset = layout?.Offset ?? 0f;
                    row.StartFrame = stage.PlannedStartFrame;
                    row.DurationFrames = ResolveStageDisplayDuration(stage);
                    row.Kind = stage.Kind;
                    row.DurationMode = stage.DurationMode;
                    row.AbilityId = stage.AbilityId;
                    row.CharacterActionId = stage.CharacterActionId;
                    row.LegacyActionKind = stage.LegacyActionKind;
                    row.MovementStrategy = stage.MovementStrategy;
                    row.MovementTargetKind = stage.MovementTargetKind;
                    row.MoveTargetPositionX = stage.MoveTargetPositionX;
                    row.MoveTargetPositionZ = stage.MoveTargetPositionZ;
                    row.MoveTargetEntityId = stage.MoveTargetEntityId;
                    row.MoveMinDistance = stage.MoveMinDistance;
                    row.MoveMaxDistance = stage.MoveMaxDistance;
                    row.WaitFrames = stage.WaitFrames;
                    row.Note = stage.Note;
                    row.DurationModeText = FormatDurationMode(stage.DurationMode);
                    row.EntryConditionCount = stage.EntryConditions?.Count ?? 0;
                    row.EntryConditionSummaryText = row.EntryConditionCount > 0
                        ? row.EntryConditionCount + " 条阶段入口条件"
                        : "无额外阶段入口条件";
                    row.StageId = stage.StageId;
                    row.Entry = string.Equals(
                        payload.EntryStageId,
                        stage.StageId,
                        StringComparison.Ordinal);
                    row.Title = NonEmpty(stage.Title, stage.StageId);
                    row.Subtitle = FormatStepKind(stage.Kind) + (stage.OwnsPrimaryAction ? " · 主动作" : " · 逻辑阶段");
                    row.TimeText = BuildStageTimeText(
                        stage.PlannedStartFrame,
                        ResolveStageDisplayDuration(stage),
                        stage.DurationMode);
                    row.ActionText = FormatStepKind(stage.Kind)
                        + (stage.OwnsPrimaryAction ? " · 占用唯一主动作" : " · 不占用主动作");
                    row.OwnsPrimaryAction = stage.OwnsPrimaryAction;
                    row.RuntimeStatusText = "草稿待验证";
                }
                row.Selected = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                    && i == selectedIndex;
                row.ActionConflict = false;
            }

            int[] detectorOrder = timeline.DetectorOrderScratch;
            if (rebuildStructure)
                FillDraftDetectorDisplayOrder(payload, detectorOrder);
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectorRows = timeline.Detectors;
            for (int rank = 0; rank < payload.Detectors.Count; rank++)
            {
                int i = detectorOrder[rank];
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                CharacterTestRuntimeAiLabLogicDetectorViewModel row = detectorRows[i];
                if (detector == null)
                {
                    if (rebuildStructure)
                        FillMissingDraftDetectorRow(row, i, rank);
                    row.Selected = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Detector
                        && i == selectedIndex;
                    continue;
                }
                if (rebuildStructure)
                {
                    row.Index = i;
                    row.PriorityRank = rank;
                    row.Priority = detector.Priority;
                    row.StageIndex = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                        ? FindDraftStageIndex(payload, detector.StageId)
                        : -1;
                    row.StartFrame = detector.ActivationStartFrame;
                    row.DurationFrames = ResolveDetectorDisplayDuration(detector, payload);
                    row.Scope = detector.Scope;
                    row.Trigger = detector.Trigger;
                    row.WindowEndMode = detector.WindowEndMode;
                    row.ExpectedResult = detector.ExpectedResult;
                    row.LocalFrameAnchor = detector.LocalFrameAnchor;
                    row.LocalFrame = detector.LocalFrame;
                    row.ActionSignalReasonCode = detector.ActionSignalReasonCode.ToString(CultureInfo.InvariantCulture);
                    row.LegacyUnfilteredSignal = detector.LegacyUnfilteredSignal;
                    row.WindowEndModeText = detector.WindowEndMode.ToString();
                    row.DetectorId = detector.DetectorId;
                    row.OwningStageId = detector.StageId ?? string.Empty;
                    row.LinkedTransitionCount = CountDraftTransitionsForDetector(payload, detector.DetectorId);
                    FillConditionRows(row, detector.Conditions);
                    row.Title = NonEmpty(detector.Title, detector.DetectorId);
                    row.HeaderText = "P" + detector.Priority.ToString(CultureInfo.InvariantCulture)
                        + " · " + NonEmpty(detector.Title, detector.DetectorId);
                    row.FactText = detector.Conditions.Count > 0
                        ? detector.Conditions[0].Fact.ToString()
                        : detector.Trigger.ToString();
                    row.ScopeText = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                        ? "卡级持续检测"
                        : "阶段级持续检测";
                    row.CardScope = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card;
                    row.StatusText = "草稿待验证";
                }
                row.Selected = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Detector
                    && i == selectedIndex;
            }

            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitionRows = timeline.Transitions;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                CharacterTestRuntimeAiLabLogicTransitionViewModel row = transitionRows[i];
                if (transition == null)
                {
                    if (rebuildStructure)
                        FillMissingDraftTransitionRow(row, i);
                    row.Selected = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Transition
                        && i == selectedIndex;
                    continue;
                }
                int source = FindDraftStageIndex(payload, transition.SourceStageId);
                int target = FindDraftStageIndex(payload, transition.TargetStageId);
                bool actionConflict = transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
                    && target >= 0
                    && payload.Stages[target].OwnsPrimaryAction;
                if (actionConflict)
                {
                    stageRows[target].ActionConflict = true;
                    if (source >= 0)
                        stageRows[source].ActionConflict = true;
                }
                if (rebuildStructure)
                {
                    row.Index = i;
                    row.SourceStageIndex = source;
                    row.TargetStageIndex = target;
                    row.DetectorIndex = FindDraftDetectorIndex(payload, transition.DetectorId);
                    row.Priority = transition.Priority;
                    row.Policy = transition.Policy;
                    row.TransitionId = transition.TransitionId;
                    row.SourceStageId = transition.SourceStageId ?? string.Empty;
                    row.TargetStageId = transition.TargetStageId ?? string.Empty;
                    row.Label = NonEmpty(transition.Label, transition.TransitionId);
                    row.EndReason = transition.EndReason ?? string.Empty;
                    row.PolicyText = transition.Policy.ToString()
                        + (actionConflict ? " · 主动作冲突" : string.Empty);
                    bool endsCard = string.Equals(
                        transition.TargetStageId,
                        CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal);
                    row.TargetText = ResolveRecipeTransitionTargetText(
                        endsCard,
                        transition.EndReason,
                        transition.Label,
                        target >= 0
                            ? NonEmpty(payload.Stages[target].Title, transition.TargetStageId)
                            : transition.TargetStageId);
                    row.EndsCard = string.Equals(
                        transition.TargetStageId,
                        CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal);
                    row.SourceEditable = row.DetectorIndex >= 0
                        && row.DetectorIndex < payload.Detectors.Count
                        && payload.Detectors[row.DetectorIndex].Scope
                            == CharacterTestRuntimeAiLabLogicDetectorScope.Card;
                    row.IsBackEdge = source >= 0 && target >= 0 && target <= source;
                    row.DisplayTitleText = (row.IsBackEdge ? "↩ " : string.Empty)
                        + NonEmpty(transition.Label, transition.TransitionId);
                    row.DetailText = "P" + transition.Priority.ToString(CultureInfo.InvariantCulture)
                        + " · " + transition.Policy.ToString()
                        + " → " + row.TargetText;
                    row.StatusText = actionConflict ? "阻断 Apply" : "草稿待验证";
                    row.Invalid = actionConflict || HasTransitionDiagnostic(diagnostics, i);
                }
                row.Selected = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Transition
                    && i == selectedIndex;
            }
            MarkInvalidTemporalActionConflicts(payload, stageRows);
            if (rebuildStructure)
            {
                FillStageCardSummaries(
                    stageRows,
                    timeline.StageCount,
                    detectorRows,
                    timeline.DetectorCount,
                    transitionRows,
                    timeline.TransitionCount);
                timeline.CachedStructureKey = structureKey;
                timeline.HasCachedStructure = true;
            }
        }

        private static void FillMissingDraftStageRow(
            CharacterTestRuntimeAiLabLogicStageViewModel row,
            int index)
        {
            row.Index = index;
            row.Order = index;
            row.Column = index;
            row.Width = 1f;
            row.Offset = 0f;
            row.StartFrame = 0;
            row.DurationFrames = 1;
            row.Kind = default;
            row.DurationMode = default;
            row.AbilityId = 0;
            row.CharacterActionId = default;
            row.LegacyActionKind = default;
            row.MovementStrategy = default;
            row.MovementTargetKind = default;
            row.MoveTargetPositionX = 0f;
            row.MoveTargetPositionZ = 0f;
            row.MoveTargetEntityId = 0;
            row.MoveMinDistance = 0f;
            row.MoveMaxDistance = 0f;
            row.WaitFrames = 0;
            row.Note = string.Empty;
            row.DurationModeText = "Invalid";
            row.EntryConditionCount = 0;
            row.EntryConditionSummaryText = "入口条件不可用";
            row.StageId = "invalid.stage." + index.ToString(CultureInfo.InvariantCulture);
            row.Entry = false;
            row.Title = "Invalid Stage";
            row.Subtitle = "Draft node is null";
            row.TimeText = "--";
            row.ActionText = "Fix the structured diagnostic before Apply F+1";
            row.OwnsPrimaryAction = false;
            row.RuntimeStatusText = "Invalid draft node";
        }

        private static void FillMissingDraftDetectorRow(
            CharacterTestRuntimeAiLabLogicDetectorViewModel row,
            int index,
            int rank)
        {
            row.Index = index;
            row.PriorityRank = rank;
            row.Priority = int.MinValue;
            row.StageIndex = -1;
            row.StartFrame = 0;
            row.DurationFrames = 1;
            row.Scope = default;
            row.Trigger = default;
            row.WindowEndMode = default;
            row.ExpectedResult = true;
            row.LocalFrameAnchor = default;
            row.LocalFrame = 0;
            row.ActionSignalReasonCode = string.Empty;
            row.LegacyUnfilteredSignal = false;
            row.WindowEndModeText = "Invalid";
            row.DetectorId = "invalid.detector." + index.ToString(CultureInfo.InvariantCulture);
            row.OwningStageId = string.Empty;
            row.LinkedTransitionCount = 0;
            row.ConditionCount = 0;
            row.Title = "Invalid Detector";
            row.HeaderText = "Invalid Detector";
            row.FactText = "Draft node is null";
            row.ScopeText = "Fix the structured diagnostic";
            row.CardScope = false;
            row.StatusText = "Invalid draft node";
        }

        private static void FillMissingDraftTransitionRow(
            CharacterTestRuntimeAiLabLogicTransitionViewModel row,
            int index)
        {
            row.Index = index;
            row.SourceStageIndex = -1;
            row.TargetStageIndex = -1;
            row.DetectorIndex = -1;
            row.Priority = int.MinValue;
            row.Policy = default;
            row.TransitionId = "invalid.transition." + index.ToString(CultureInfo.InvariantCulture);
            row.SourceStageId = string.Empty;
            row.TargetStageId = string.Empty;
            row.Label = "Invalid Transition";
            row.EndReason = string.Empty;
            row.PolicyText = "Draft node is null";
            row.TargetText = "--";
            row.EndsCard = false;
            row.SourceEditable = false;
            row.IsBackEdge = false;
            row.DisplayTitleText = "Invalid Transition";
            row.DetailText = "Fix the structured diagnostic before Apply F+1";
            row.StatusText = "Invalid draft node";
            row.Invalid = true;
        }

        private static void MarkInvalidTemporalActionConflicts(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stageRows)
        {
            int count = Math.Min(payload?.Stages?.Count ?? 0, stageRows?.Length ?? 0);
            for (int leftIndex = 0; leftIndex < count; leftIndex++)
            {
                CharacterTestRuntimeAiLabLogicStage left = payload.Stages[leftIndex];
                if (left == null
                    || !left.OwnsPrimaryAction
                    || left.DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                    || left.PlannedDurationFrames <= 0)
                {
                    continue;
                }

                long leftEnd = (long)left.PlannedStartFrame + left.PlannedDurationFrames;
                for (int rightIndex = leftIndex + 1; rightIndex < count; rightIndex++)
                {
                    CharacterTestRuntimeAiLabLogicStage right = payload.Stages[rightIndex];
                    if (right == null
                        || !right.OwnsPrimaryAction
                        || right.DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                        || right.PlannedDurationFrames <= 0)
                    {
                        continue;
                    }

                    long rightEnd = (long)right.PlannedStartFrame + right.PlannedDurationFrames;
                    if (left.PlannedStartFrame >= rightEnd || right.PlannedStartFrame >= leftEnd)
                        continue;
                    stageRows[leftIndex].ActionConflict = true;
                    stageRows[rightIndex].ActionConflict = true;
                }
            }
        }

        private static bool HasTransitionDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            int transitionIndex)
        {
            string prefix = "$.transitions[" + transitionIndex.ToString(CultureInfo.InvariantCulture) + "]";
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiLabCardDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.Severity == CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error
                    && diagnostic.Path != null
                    && diagnostic.Path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ResolveRecipeTransitionTargetText(
            bool endsCard,
            string endReason,
            string label,
            string stageTitle)
        {
            if (!endsCard)
                return NonEmpty(stageTitle, "未配置目标时间轴");
            if (ContainsReplanSemantic(endReason) || ContainsReplanSemantic(label))
                return "重新决策";
            return "结束行为卡";
        }

        private static bool ContainsReplanSemantic(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.IndexOf("重新决策", StringComparison.Ordinal) >= 0
                    || value.IndexOf("Replan", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildHistoricalRuntimeStatus(
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection historical)
        {
            if (historical == null)
                return "历史行为状态不可用";
            string prefix = "历史回看 · Frame "
                + historical.SelectedFrame.ToString(CultureInfo.InvariantCulture)
                + " / Latest "
                + historical.LatestFrame.ToString(CultureInfo.InvariantCulture)
                + " · 只读";
            if (historical.Status == CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Unavailable)
                return prefix + " · " + historical.DiagnosticCode;
            if (!historical.EventEvidenceComplete && !string.IsNullOrEmpty(historical.DiagnosticCode))
                return prefix + " · " + historical.DiagnosticCode;
            return prefix;
        }

    }
}
