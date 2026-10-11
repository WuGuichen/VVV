using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceEditCommandHandler
    {
        private delegate bool ElementMutation(int entryIndex, int elementIndex, out string message);

        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private Action _refresh;
        private Func<int> _selectedEntryIndex;
        private Action<CharacterTestRuntimeAiLabLogicElementKind, int> _setSelection;
        private Func<CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel> _workspaceView;
        private string _pendingPriorityDetectorId = string.Empty;
        private int _pendingPriorityDirection;
        private CharacterTestRuntimeAiLabStageEditIdentity _pendingPriorityIdentity;
        private string _pendingCascadeDetectorId = string.Empty;
        private CharacterTestRuntimeAiLabStageEditIdentity _pendingCascadeIdentity;
        private bool _canonicalizationConfirmationPending;
        private CharacterTestRuntimeAiLabStageEditIdentity _canonicalizationIdentity;

        internal string LastMessage { get; private set; } = string.Empty;
        internal bool CanonicalizationConfirmationPending => _canonicalizationConfirmationPending;

        internal void CancelCascadeConfirmation()
        {
            ClearPendingCascade();
        }

        internal bool TryAuthorizeCascadeDeletion(string detectorId, out string message)
        {
            if (string.IsNullOrEmpty(detectorId)
                || !TryResolveElement(
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    detectorId,
                    out _))
            {
                ClearPendingCascade();
                LastMessage = "待删除的分支事件已失效，请重新选择。";
                message = LastMessage;
                return false;
            }
            if (!TryPrepareGraphMutation(out _, out var identity))
            {
                ClearPendingCascade();
                message = LastMessage;
                return false;
            }

            _pendingCascadeDetectorId = detectorId;
            _pendingCascadeIdentity = identity;
            LastMessage = "正在删除分支事件…";
            message = LastMessage;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                "ui-confirm",
                "delete-branch-event",
                detectorId,
                _selectedEntryIndex?.Invoke() ?? -1,
                identity,
                "authorized",
                message,
                _workspaceView?.Invoke()?.Timeline?.DetectorCount ?? -1);
            return true;
        }

        private bool IsHistorical =>
            _controller?.IsHistoricalFrameSelected == true
            || _workspaceView?.Invoke()?.Timeline?.Historical == true;

        internal void CancelCanonicalizationConfirmation()
        {
            _canonicalizationConfirmationPending = false;
            _canonicalizationIdentity = default;
        }

        internal void CancelPendingConfirmations()
        {
            CancelCanonicalizationConfirmation();
            ClearPendingPriority();
            ClearPendingCascade();
        }

        internal void Configure(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            Action refresh,
            Func<int> selectedEntryIndex,
            Action<CharacterTestRuntimeAiLabLogicElementKind, int> setSelection,
            Func<CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel> workspaceView)
        {
            _controller = controller;
            _refresh = refresh;
            _selectedEntryIndex = selectedEntryIndex;
            _setSelection = setSelection;
            _workspaceView = workspaceView;
        }

        internal bool TryHandle(
            string commandId,
            CharacterTestRuntimeAiLabBehaviorCardElementCommand element,
            out string message)
        {
            if (!string.Equals(
                    commandId,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CanonicalizeDetectorWindows,
                    StringComparison.Ordinal))
            {
                _canonicalizationConfirmationPending = false;
                _canonicalizationIdentity = default;
            }
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage:
                    SelectElement(CharacterTestRuntimeAiLabLogicElementKind.Stage, element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector:
                    SelectElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition:
                    SelectElement(CharacterTestRuntimeAiLabLogicElementKind.Transition, element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStage:
                    AddStage(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCardDetector:
                    AddCardDetector();
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageDetector:
                    AddStageDetector(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageBranchAtFrame:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddDurationEventAtFrame:
                    AddStageBranchAtFrame(
                        element.StableId,
                        element.Direction,
                        CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddInstantEventAtFrame:
                    AddStageBranchAtFrame(
                        element.StableId,
                        element.Direction,
                        CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SetEntryStage:
                    SetEntryStage(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateDetector:
                    DuplicateDetector(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateBranchEvent:
                    DuplicateBranchEvent(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CanonicalizeDetectorWindows:
                    CanonicalizeDetectorWindows();
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransition:
                    AddTransition(
                        element.StableId,
                        (CharacterTestRuntimeAiLabTransitionResultPreset)element.Direction);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransitionToStage:
                    AddTransitionToStage(element.StableId, element.Direction);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorFact:
                    CycleDetectorFact(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorTrigger:
                    CycleDetectorTrigger(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorWindowMode:
                    CycleDetectorWindowMode(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.MoveDetectorWindowStart:
                    MoveDetectorWindow(element.StableId, element.Direction);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ResizeDetectorWindow:
                    ResizeDetectorWindow(element.StableId, element.Direction);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AdjustDetectorPriority:
                    AdjustDetectorPriority(element.StableId, element.Direction);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionPolicy:
                    CycleTransitionPolicy(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget:
                    CycleTransitionTarget(element.StableId, element.Direction);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement:
                    RemoveElement(element);
                    break;
                default:
                    message = string.Empty;
                    return false;
            }

            message = LastMessage;
            return true;
        }

        private void SelectElement(CharacterTestRuntimeAiLabLogicElementKind kind, string stableId)
        {
            if (!TryResolveElement(kind, stableId, out int index))
            {
                LastMessage = "所选时间轴元素已失效，请重新选择。";
                return;
            }

            ClearPendingPriority();
            _setSelection?.Invoke(kind, index);
            PublishSelection(kind, stableId);
            LastMessage = kind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                ? "已选择逻辑阶段。"
                : kind == CharacterTestRuntimeAiLabLogicElementKind.Detector
                    ? "已选择持续检测器。"
                    : "已选择事件跳转。";
            _refresh?.Invoke();
        }

        private void AddStage(string anchorStageId)
        {
            int stageCount = Math.Max(0, _workspaceView?.Invoke()?.Timeline?.StageCount ?? 0);
            int anchor = stageCount > 0 ? stageCount - 1 : -1;
            int createdIndex = stageCount;
            if (!string.IsNullOrEmpty(anchorStageId)
                && !TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Stage, anchorStageId, out anchor))
            {
                LastMessage = "新增 Stage 的锚点已失效。";
                return;
            }
            if (!string.IsNullOrEmpty(anchorStageId))
                createdIndex = anchor + 1;
            if (!CanMutate(out int entryIndex))
                return;
            if (!_controller.TryAddLogicStage(entryIndex, anchor, out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.PrimaryAction;
                timeline.Presentation.SelectedRecipeEventStableKey = string.Empty;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Stage, createdIndex);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void AddStageDetector(string stageId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Stage, stageId, out int stageIndex))
            {
                LastMessage = "请先选择 Stage。";
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            if (!_controller.TryAddLogicDetector(
                    entryIndex,
                    identity,
                    CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                    stageId,
                    out string detectorId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            int detectorIndex = ResolveCreatedDetectorIndex(detectorId);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            _setSelection?.Invoke(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorIndex);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void AddStageBranchAtFrame(
            string stageId,
            int frame,
            CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timingKind)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            bool recipeAuthority = CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(
                timeline?.RecipePayload);
            bool timelineExists = recipeAuthority
                ? TryResolveRecipeTimeline(timeline?.RecipePayload, stageId)
                : TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Stage, stageId, out _);
            if (!timelineExists)
            {
                LastMessage = "所选时间轴已失效，请重新选择后再添加事件。";
                _refresh?.Invoke();
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
            {
                _refresh?.Invoke();
                return;
            }
            if (!_controller.TryAddLogicBranchEvent(
                    entryIndex,
                    identity,
                    stageId,
                    Math.Max(0, frame),
                    timingKind,
                    out string detectorId,
                    out _,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                _refresh?.Invoke();
                return;
            }
            timeline = _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                recipeAuthority = CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(
                    timeline.RecipePayload);
                timeline.Presentation.SelectedRecipeEventKind = recipeAuthority
                    ? BehaviorRecipeEventKind.Event
                    : BehaviorRecipeEventKind.Branch;
                string eventId = detectorId != null && detectorId.StartsWith("event.", StringComparison.Ordinal)
                    ? detectorId.Substring("event.".Length)
                    : detectorId;
                timeline.Presentation.SelectedRecipeEventStableKey = recipeAuthority
                    ? BehaviorRecipeTimelineProjection.BuildV6EventStableKey(stageId, eventId)
                    : string.Empty;
                if (recipeAuthority)
                {
                    timeline.Presentation.SelectEvent(stageId, eventId);
                }
            }
            if (!recipeAuthority)
            {
                int detectorIndex = ResolveCreatedDetectorIndex(detectorId);
                PublishGraphContext(
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    detectorId,
                    resultingCardDraftId);
                _setSelection?.Invoke(
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    detectorIndex);
            }
            LastMessage = "已添加事件。";
            _refresh?.Invoke();
        }

        internal void AddCharacterActionEventAtFrame(
            in CharacterTestRuntimeAiLabBehaviorRecipeCharacterActionEventCommand command)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (!CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(timeline?.RecipePayload)
                || !TryResolveRecipeTimeline(timeline.RecipePayload, command.TimelineId))
            {
                LastMessage = "所选时间轴已失效，请重新选择后再添加角色动作事件。";
                _refresh?.Invoke();
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
            {
                _refresh?.Invoke();
                return;
            }
            if (!_controller.TryAddCharacterActionEvent(
                    entryIndex,
                    identity,
                    command.TimelineId,
                    Math.Max(0, command.Frame),
                    command.CharacterActionId,
                    out string eventId,
                    out _,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                _refresh?.Invoke();
                return;
            }
            timeline = _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
                timeline.Presentation.SelectedRecipeEventStableKey =
                    BehaviorRecipeTimelineProjection.BuildV6EventStableKey(command.TimelineId, eventId);
                timeline.Presentation.SelectEvent(command.TimelineId, eventId);
            }
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private static bool TryResolveRecipeTimeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId)
        {
            if (payload == null || string.IsNullOrWhiteSpace(timelineId))
                return false;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                if (string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private int ResolveCreatedDetectorIndex(string detectorId)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            int count = Math.Min(timeline?.DetectorCount ?? 0, timeline?.Detectors?.Length ?? 0);
            string normalized = detectorId != null
                && detectorId.StartsWith("event.event.", StringComparison.Ordinal)
                    ? detectorId.Substring("event.".Length)
                    : detectorId ?? string.Empty;
            int createdIndex = -1;
            for (int i = 0; i < count; i++)
            {
                string candidate = timeline.Detectors[i]?.DetectorId ?? string.Empty;
                if (string.Equals(candidate, detectorId, StringComparison.Ordinal)
                    || string.Equals(candidate, normalized, StringComparison.Ordinal))
                {
                    return i;
                }
                if (candidate.StartsWith("event.detector.new.", StringComparison.Ordinal)
                    || candidate.StartsWith("event.event.new.", StringComparison.Ordinal))
                {
                    createdIndex = i;
                }
            }
            return createdIndex >= 0 ? createdIndex : Math.Max(0, count - 1);
        }

        private void SetEntryStage(string stageId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Stage, stageId, out int stageIndex)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel currentTimeline =
                _workspaceView?.Invoke()?.Timeline;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(
                    currentTimeline?.RecipePayload))
            {
                if (!_controller.TryApplyBehaviorRecipeTriggerEventField(
                    entryIndex,
                    identity,
                    stageId,
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetEntryTimeline,
                    -1,
                    default,
                    out string v6ResultingCardDraftId,
                    out string v6Diagnostic))
                {
                    LastMessage = v6Diagnostic;
                    return;
                }
                currentTimeline.Presentation.SelectTimeline(stageId);
                currentTimeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                currentTimeline.Presentation.SelectedRecipeEventStableKey =
                    "condition-owner:timeline:" + stageId;
                PublishGraphContext(
                    CharacterTestRuntimeAiLabLogicElementKind.Stage,
                    stageId,
                    v6ResultingCardDraftId);
                LastMessage = v6Diagnostic;
                _refresh?.Invoke();
                return;
            }
            if (!_controller.TrySetLogicEntryStage(
                    entryIndex,
                    identity,
                    stageId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.StageEntry;
                timeline.Presentation.SelectedRecipeEventStableKey = "event:" + stageId + ":entry";
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Stage, stageIndex);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Stage,
                stageId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void AddCardDetector()
        {
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            int detectorIndex = _workspaceView?.Invoke()?.Timeline?.DetectorCount ?? 0;
            if (!_controller.TryAddLogicDetector(
                    entryIndex,
                    identity,
                    CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                    string.Empty,
                    out string detectorId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorIndex);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void DuplicateDetector(string detectorId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out _)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            int detectorIndex = _workspaceView?.Invoke()?.Timeline?.DetectorCount ?? 0;
            if (!_controller.TryDuplicateLogicDetector(
                    entryIndex,
                    identity,
                    detectorId,
                    out string createdDetectorId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorIndex);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                createdDetectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void DuplicateBranchEvent(string detectorId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out _)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            int detectorIndex = _workspaceView?.Invoke()?.Timeline?.DetectorCount ?? 0;
            if (!_controller.TryDuplicateLogicBranchEvent(
                    entryIndex,
                    identity,
                    detectorId,
                    out string createdDetectorId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Branch;
                timeline.Presentation.SelectedRecipeEventStableKey = string.Empty;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorIndex);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                createdDetectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void CanonicalizeDetectorWindows()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = _workspaceView?.Invoke()?.Timeline;
            if (timeline == null || !timeline.RequiresDetectorWindowCanonicalization)
            {
                CancelCanonicalizationConfirmation();
                LastMessage = "当前草稿不需要规范化 Detector window。";
                return;
            }
            if (!_canonicalizationConfirmationPending)
            {
                if (!TryPrepareGraphMutation(out int previewEntryIndex, out var previewIdentity))
                    return;
                if (!_controller.TryBuildLogicDetectorWindowCanonicalizationPreview(
                        previewEntryIndex,
                        previewIdentity,
                        out string preview,
                        out string previewDiagnostic))
                {
                    LastMessage = previewDiagnostic;
                    return;
                }
                _canonicalizationConfirmationPending = true;
                _canonicalizationIdentity = previewIdentity;
                LastMessage = preview;
                _refresh?.Invoke();
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity)
                || !_canonicalizationIdentity.Matches(identity))
            {
                CancelCanonicalizationConfirmation();
                LastMessage = "规范化确认已因行为卡身份或 revision 改变而取消，请重新预览。";
                _refresh?.Invoke();
                return;
            }
            if (!_controller.TryCanonicalizeLogicDetectorWindows(
                    entryIndex,
                    identity,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            CancelCanonicalizationConfirmation();
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.None,
                string.Empty,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

    }
}
