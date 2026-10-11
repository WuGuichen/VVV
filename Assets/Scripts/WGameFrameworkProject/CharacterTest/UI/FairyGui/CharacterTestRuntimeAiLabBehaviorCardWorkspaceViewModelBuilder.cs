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
        private static readonly Unity.Profiling.ProfilerMarker WorkspaceBuildMarker = new Unity.Profiling.ProfilerMarker("CharacterTest.UI.BuildAiLabCardWorkspace");
        private const int RuntimeDurationCacheLimit = 4096;
        private static readonly string[] CurrentStageDurationText = CreateRuntimeDurationText("当前阶段 · 实际 ");
        private static readonly string[] CurrentActionDurationText = CreateRuntimeDurationText("当前阶段 · 主动作执行中 · 实际 ");
        internal static void Build(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            string cardName,
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            int selectedStageIndex,
            CharacterTestRuntimeAiLabLogicElementKind selectedElementKind =
                CharacterTestRuntimeAiLabLogicElementKind.Stage,
            string executingStageId = "",
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState = null,
            CharacterTestRuntimeAiLabLogicEventBuffer latestEvents = null,
            int runtimeFrame = -1,
            string behaviorEntryId = "",
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection historical = null,
            CharacterTestRuntimeAiLabActiveActionProgress activeActionProgress = default,
            CharacterTestRuntimeAiPlannerDevSceneController controller = null,
            float observedTargetDistance = -1f,
            float movementCompletionDistance = -1f,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent = null)
        {
            using var profileScope = WorkspaceBuildMarker.Auto();
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            timeline.CardTitleText = NonEmpty(cardName, "行为卡");
            timeline.RecipePayload = payload;
            timeline.RuntimeState = runtimeState;
            timeline.CompiledRecipeV6 = CharacterTestBehaviorExecutionProjection.Get(compile?.Program);
            PopulateCharacterActionOptions(timeline, controller);
            string nextBehaviorEntryId = behaviorEntryId ?? string.Empty;
            if (!string.IsNullOrEmpty(timeline.BehaviorEntryId)
                && !string.Equals(
                    timeline.BehaviorEntryId,
                    nextBehaviorEntryId,
                    StringComparison.Ordinal))
            {
                timeline.Presentation.SelectRecipe();
                timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                timeline.Presentation.SelectedRecipeEventStableKey = "condition-owner:activation";
            }
            timeline.BehaviorEntryId = nextBehaviorEntryId;
            BehaviorPlanProjectionBuilder.BeginBuild(timeline.Structure);
            timeline.Available = false;
            BehaviorPlanRuntimeOverlayBuilder.Populate(
                timeline.RuntimeOverlay,
                runtimeState,
                historical,
                activeActionProgress,
                runtimeFrame,
                observedTargetDistance);
            timeline.RuntimeCursor.Populate(
                timeline.CompiledRecipeV6,
                runtimeState,
                compile?.Program?.SemanticHash ?? 0L,
                nextBehaviorEntryId,
                runtimeFrame,
                historical != null);
            BehaviorRuntimeTimeSnapshot runtimeTime = timeline.RuntimeOverlay.Time;
            timeline.RequiresDetectorWindowCanonicalization =
                CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(payload);
            timeline.HasBlockingDiagnostics = compile != null && !compile.Success;
            timeline.ValidationBadgeText = timeline.HasBlockingDiagnostics ? "存在阻断问题" : "等待验证";
            if (compile == null || !compile.Success || compile.Program == null)
            {
                if (payload != null && payload.Stages.Count > 0)
                {
                    FillInvalidLogicDraftView(
                        viewModel,
                        cardName,
                        payload,
                        selectedStageIndex,
                        selectedElementKind,
                        compile?.Diagnostics);
                }
                else
                {
                    timeline.DiagnosticsText = BuildCompileDiagnostics(compile?.Diagnostics);
                    timeline.SemanticHashText = "hash --------";
                    timeline.SummaryText = compile == null
                        ? "等待编译"
                        : "逻辑编译失败；当前运行版本保持不变。";
                    timeline.HasCachedStructure = false;
                }
                BehaviorRecipeTriggerEventInspectorProjection.Rebuild(
                    timeline, payload, runtimeState, observedAgent, runtimeFrame);
                return;
            }

            CharacterTestRuntimeAiLabCompiledLogicProgram program = compile.Program;
            timeline.AutoFocusedDiagnosticKey = 0L;
            timeline.HasAutoFocusedDiagnosticKey = false;
            string expectedBehaviorEntryId = behaviorEntryId ?? string.Empty;
            long expectedProgramSemanticHash = program.SemanticHash;
            int expectedProfileSlot = historical?.ProfileSlot ?? -1;
            int expectedCardInstanceStartFrame = historical?.CardInstanceStartFrame ?? -1;
            int eventEntityId = historical == null ? runtimeState?.EntityId ?? 0 : 0;
            string eventBehaviorEntryId = historical == null ? expectedBehaviorEntryId : string.Empty;
            long eventProgramSemanticHash = historical == null ? expectedProgramSemanticHash : 0L;
            int eventProfileSlot = historical == null ? expectedProfileSlot : -1;
            int eventCardInstanceStartFrame = historical == null ? expectedCardInstanceStartFrame : -1;
            long structureKey = ComputeCompiledStructureKey(cardName, program, payload);
            structureKey = structureKey * 397L ^ (controller != null ? 1L : 0L);
            structureKey = structureKey * 397L ^ (movementCompletionDistance > 0f
                ? Mathf.RoundToInt(movementCompletionDistance * 1000f)
                : 0);
            bool rebuildStructure = !timeline.HasCachedStructure
                || timeline.CachedStructureKey != structureKey;
            EnsureTimelineCapacity(
                timeline,
                program.Stages.Count,
                program.Detectors.Count,
                program.Transitions.Count);
            timeline.StageCount = program.Stages.Count;
            timeline.DetectorCount = program.Detectors.Count;
            timeline.TransitionCount = program.Transitions.Count;
            if (rebuildStructure)
            {
                timeline.ActivationConditionCount = program.ActivationConditions?.Count ?? 0;
                timeline.ActivationConditionSummaryText = timeline.ActivationConditionCount > 0
                    ? timeline.ActivationConditionCount + " 条行为激活条件"
                    : "无额外行为激活条件";
                string fullHash = unchecked((ulong)program.SemanticHash).ToString("X16", CultureInfo.InvariantCulture);
                timeline.SemanticHashText = "hash " + fullHash;
                timeline.SummaryText = program.Stages.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个逻辑阶段 | "
                    + program.Detectors.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个持续检测 | "
                    + program.Transitions.Count.ToString(CultureInfo.InvariantCulture)
                    + " 条事件跳转 | 横轴按秒/帧表达计划时长，事件命中时跳转逻辑阶段";
            }
            viewModel.DraftHashText = timeline.SemanticHashText;
            timeline.HasBlockingDiagnostics = false;
            timeline.DiagnosticsText = string.Empty;
            timeline.ValidationBadgeText = "验证通过";

            int selected = selectedStageIndex;
            switch (selectedElementKind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    selected = program.Detectors.Count > 0
                        ? Math.Max(0, Math.Min(selected, program.Detectors.Count - 1))
                        : -1;
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    selected = program.Transitions.Count > 0
                        ? Math.Max(0, Math.Min(selected, program.Transitions.Count - 1))
                        : -1;
                    break;
                default:
                    selectedElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                    selected = program.Stages.Count > 0
                        ? Math.Max(0, Math.Min(selected, program.Stages.Count - 1))
                        : -1;
                    break;
            }
            timeline.SelectedElementKind = selectedElementKind;
            timeline.SelectedElementIndex = selected;
            timeline.Available = true;
            if (rebuildStructure)
                timeline.DurationFrames = ResolveLogicTimelineDuration(program);
            int currentStageIndex = runtimeState?.CurrentStageIndex ?? -1;
            if (historical == null)
            {
                for (int transitionIndex = 0; transitionIndex < program.Transitions.Count; transitionIndex++)
                {
                    CharacterTestRuntimeAiLabCompiledLogicTransition committedTransition =
                        program.Transitions[transitionIndex];
                    if (!ContainsLogicEvent(
                            latestEvents,
                            CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                            string.Empty,
                            committedTransition.TransitionId,
                            eventEntityId,
                            eventBehaviorEntryId,
                            eventProgramSemanticHash,
                            eventProfileSlot,
                            eventCardInstanceStartFrame))
                    {
                        continue;
                    }
                    currentStageIndex = committedTransition.EndsCard
                        ? -1
                        : committedTransition.TargetStageIndex;
                    break;
                }
            }
            bool primaryActionRunning = runtimeState != null
                && runtimeState.PrimaryActionState == (int)CharacterTestRuntimeAiLabLogicActionState.Running;
            int primaryActionStageIndex = primaryActionRunning && runtimeState != null
                ? runtimeState.PrimaryActionStageIndex >= 0
                    ? runtimeState.PrimaryActionStageIndex
                    : runtimeState.CurrentStageIndex
                : -1;
            timeline.RuntimeStatusText = timeline.Historical
                ? BuildHistoricalRuntimeStatus(historical)
                : runtimeState == null
                    ? "设计态：逻辑游标尚未绑定到所选行为卡"
                    : runtimeState.PendingTransitionIndex >= 0
                        ? primaryActionRunning
                            ? "运行态 · Pending · 主动作执行中"
                            : "运行态 · Pending"
                        : primaryActionRunning
                            ? "运行态 · 主动作执行中"
                            : "运行态";

            for (int i = 0; i < program.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledLogicStage stage = program.Stages[i];
                CharacterTestRuntimeAiLabLogicStageViewModel row = timeline.Stages[i];
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
                    row.MoveMinDistance = stage.MoveMinDistance > 0f
                        ? stage.MoveMinDistance
                        : stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                            && movementCompletionDistance > 0f
                                ? movementCompletionDistance
                                : 0f;
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
                        payload?.EntryStageId,
                        stage.StageId,
                        StringComparison.Ordinal);
                    row.Title = NonEmpty(stage.Title, stage.StageId);
                    row.Subtitle = FormatStepKind(stage.Kind)
                        + (stage.OwnsPrimaryAction ? " · 主动作" : " · 逻辑阶段");
                    row.TimeText = BuildStageTimeText(
                        stage.PlannedStartFrame,
                        ResolveStageDisplayDuration(stage),
                        stage.DurationMode);
                    row.ActionText = FormatStepKind(stage.Kind)
                        + (stage.OwnsPrimaryAction ? " · 占用唯一主动作" : " · 不占用主动作");
                    row.OwnsPrimaryAction = stage.OwnsPrimaryAction;
                    row.ActionConflict = false;
                    ConfigureActionGeometry(row, controller);
                }
                row.ActualDurationFrames = i == currentStageIndex && runtimeState != null
                    ? runtimeTime.GetStageElapsedFrame()
                    : -1;
                row.Selected = selectedElementKind == CharacterTestRuntimeAiLabLogicElementKind.Stage && i == selected;
                row.Current = i == currentStageIndex;
                row.ActionRunning = i == primaryActionStageIndex;
                UpdateActionProgress(
                    row,
                    i,
                    runtimeState,
                    runtimeFrame,
                    activeActionProgress);
                row.RuntimeStatusText = FormatStageRuntimeStatus(
                    i == currentStageIndex,
                    i == primaryActionStageIndex,
                    row.ActualDurationFrames);
            }

            if (rebuildStructure)
                FillDetectorDisplayOrder(program, timeline.DetectorOrderScratch);
            for (int rank = 0; rank < program.Detectors.Count; rank++)
            {
                int i = timeline.DetectorOrderScratch[rank];
                CharacterTestRuntimeAiLabCompiledLogicDetector detector = program.Detectors[i];
                bool triggered = ContainsLogicEvent(
                    latestEvents,
                    CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                    detector.DetectorId,
                    string.Empty,
                    eventEntityId,
                    eventBehaviorEntryId,
                    eventProgramSemanticHash,
                    eventProfileSlot,
                    eventCardInstanceStartFrame);
                CharacterTestRuntimeAiLabLogicDetectorViewModel row = timeline.Detectors[i];
                if (rebuildStructure)
                {
                    row.Index = i;
                    row.PriorityRank = rank;
                    row.Priority = detector.Priority;
                    row.StageIndex = detector.StageIndex;
                    row.StartFrame = detector.ActivationStartFrame;
                    row.DurationFrames = ResolveDetectorDisplayDuration(detector, program);
                    row.LinkedTransitionCount = CountTransitionsForDetector(program, i);
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
                    row.OwningStageId = detector.StageIndex >= 0
                        && detector.StageIndex < program.Stages.Count
                            ? program.Stages[detector.StageIndex].StageId
                            : string.Empty;
                    FillConditionRows(row, detector.Conditions);
                    row.Title = NonEmpty(detector.Title, detector.DetectorId);
                    row.HeaderText = "P" + detector.Priority.ToString(CultureInfo.InvariantCulture)
                        + " · " + detector.Trigger + " · " + row.Title;
                    row.FactText = FormatLogicDetectorConditions(detector);
                    row.ScopeText = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                        ? "卡级持续检测"
                        : "阶段级持续检测";
                    row.CardScope = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card;
                    row.ScopeText += " · +" + detector.ActivationStartFrame.ToString(CultureInfo.InvariantCulture)
                        + "f · " + detector.WindowEndMode
                        + (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                            ? " " + detector.ActivationDurationFrames.ToString(CultureInfo.InvariantCulture) + "f"
                            : string.Empty)
                        + " · " + row.LinkedTransitionCount.ToString(CultureInfo.InvariantCulture) + " routes";
                }
                row.Selected = selectedElementKind == CharacterTestRuntimeAiLabLogicElementKind.Detector && i == selected;
                row.Active = runtimeState != null
                    && CharacterTestRuntimeAiLabDetectorActivity.IsActive(
                        runtimeFrame,
                        detector,
                        runtimeState.CardStartedFrame,
                        runtimeState.StageEnteredFrame,
                        currentStageIndex);
                row.Armed = runtimeState != null
                    && runtimeState.DetectorArmed != null
                    && i < runtimeState.DetectorArmed.Length
                    && runtimeState.DetectorArmed[i];
                row.Triggered = triggered;
                row.RuntimeElapsedFrames = ResolveDetectorElapsedFrames(
                    row.Active,
                    row.Triggered,
                    detector,
                    runtimeState,
                    runtimeFrame);
                row.StatusText = triggered
                    ? "命中"
                    : row.Active
                        ? row.Armed ? "Active · 已武装" : "Active · 等待重新武装"
                        : "Inactive";
            }

            for (int i = 0; i < program.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledLogicTransition transition = program.Transitions[i];
                CharacterTestRuntimeAiLabLogicTransitionViewModel row = timeline.Transitions[i];
                if (rebuildStructure)
                {
                    row.Index = i;
                    row.SourceStageIndex = transition.SourceStageIndex;
                    row.TargetStageIndex = transition.TargetStageIndex;
                    row.DetectorIndex = transition.DetectorIndex;
                    row.Priority = transition.Priority;
                    row.Policy = transition.Policy;
                    row.TransitionId = transition.TransitionId;
                    row.SourceStageId = transition.SourceStageIndex >= 0
                        && transition.SourceStageIndex < program.Stages.Count
                            ? program.Stages[transition.SourceStageIndex].StageId
                            : string.Empty;
                    row.TargetStageId = transition.EndsCard
                        ? CharacterTestRuntimeAiLabLogicTransition.EndTarget
                        : program.Stages[transition.TargetStageIndex].StageId;
                    row.Label = NonEmpty(transition.Label, transition.TransitionId);
                    row.EndReason = transition.EndReason;
                    row.PolicyText = transition.Policy.ToString();
                    row.TargetText = ResolveRecipeTransitionTargetText(
                        transition.EndsCard,
                        transition.EndReason,
                        transition.Label,
                        transition.EndsCard
                            ? string.Empty
                            : program.Stages[transition.TargetStageIndex].Title);
                    row.EndsCard = transition.EndsCard;
                    row.SourceEditable = transition.DetectorIndex >= 0
                        && transition.DetectorIndex < program.Detectors.Count
                        && program.Detectors[transition.DetectorIndex].Scope
                            == CharacterTestRuntimeAiLabLogicDetectorScope.Card;
                    row.IsBackEdge = transition.TargetStageIndex >= 0
                        && transition.SourceStageIndex >= 0
                        && transition.TargetStageIndex <= transition.SourceStageIndex;
                    row.DisplayTitleText = (row.IsBackEdge ? "↩ " : string.Empty) + row.Label;
                    row.DetailText = "P" + transition.Priority.ToString(CultureInfo.InvariantCulture)
                        + " · " + row.PolicyText + " → " + row.TargetText;
                    row.Invalid = false;
                }
                row.Selected = selectedElementKind == CharacterTestRuntimeAiLabLogicElementKind.Transition && i == selected;
                row.DetectorHit = transition.DetectorIndex >= 0
                    && transition.DetectorIndex < program.Detectors.Count
                    && ContainsLogicEvent(
                        latestEvents,
                        CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                        program.Detectors[transition.DetectorIndex].DetectorId,
                        transition.TransitionId,
                        eventEntityId,
                        eventBehaviorEntryId,
                        eventProgramSemanticHash,
                        eventProfileSlot,
                        eventCardInstanceStartFrame);
                row.Pending = runtimeState != null && runtimeState.PendingTransitionIndex == i;
                row.Committed = historical?.ExactCommittedTransitionIndex == i
                    || historical == null
                    && ContainsLogicEvent(
                            latestEvents,
                            CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                            string.Empty,
                            transition.TransitionId,
                            eventEntityId,
                            eventBehaviorEntryId,
                            eventProgramSemanticHash,
                            eventProfileSlot,
                            eventCardInstanceStartFrame);
                row.PreviousCommitted = historical != null
                    && !row.Committed
                    && historical.PreviousCommittedTransitionIndex == i;
                row.RuntimeFrame = historical?.ExactCommittedTransitionIndex == i
                    ? historical.SelectedFrame
                    : FindLogicEventFrame(
                        latestEvents,
                        CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                        transition.TransitionId,
                        eventEntityId,
                        eventBehaviorEntryId,
                        eventProgramSemanticHash,
                        eventProfileSlot,
                        eventCardInstanceStartFrame);
                if (row.RuntimeFrame < 0 && row.PreviousCommitted)
                    row.RuntimeFrame = historical.PreviousCommittedRuntimeFrame;
                row.StatusText = runtimeState != null && runtimeState.PendingTransitionIndex == i
                        ? "等待动作边界"
                        : row.Committed
                                ? "本帧已提交"
                                : row.PreviousCommitted
                                    ? "历史路径 · F"
                                        + row.RuntimeFrame.ToString(CultureInfo.InvariantCulture)
                                : row.DetectorHit
                                    ? "同帧未胜出"
                                : transition.TargetStageIndex >= 0
                                    && transition.SourceStageIndex >= 0
                                    && transition.TargetStageIndex <= transition.SourceStageIndex
                                        ? "回跳"
                                        : "待触发";
            }
            if (rebuildStructure)
            {
                FillStageCardSummaries(
                    timeline.Stages,
                    timeline.StageCount,
                    timeline.Detectors,
                    timeline.DetectorCount,
                    timeline.Transitions,
                    timeline.TransitionCount);
                timeline.CachedStructureKey = structureKey;
                timeline.HasCachedStructure = true;
            }
            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(
                timeline, payload, runtimeState, observedAgent, runtimeFrame);
        }

    }
}
