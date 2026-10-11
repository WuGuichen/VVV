using System;
using System.Collections.Generic;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds
    {
        internal const string Prefix = "character_test.runtime_ai_lab.behavior_card.";
        internal const string OpenLibraryCard = Prefix + "open_library_card";
        internal const string SelectStage = Prefix + "select_stage";
        internal const string SelectDetector = Prefix + "select_detector";
        internal const string SelectTransition = Prefix + "select_transition";
        internal const string AddStage = Prefix + "add_stage";
        internal const string AddCardDetector = Prefix + "add_card_detector";
        internal const string AddStageDetector = Prefix + "add_stage_detector";
        internal const string AddStageBranchAtFrame = Prefix + "add_stage_branch_at_frame";
        internal const string AddInstantEventAtFrame = Prefix + "add_instant_event_at_frame";
        internal const string AddDurationEventAtFrame = Prefix + "add_duration_event_at_frame";
        internal const string AddCharacterActionEventAtFrame = Prefix + "add_character_action_event_at_frame";
        internal const string SetEntryStage = Prefix + "set_entry_stage";
        internal const string DuplicateDetector = Prefix + "duplicate_detector";
        internal const string DuplicateBranchEvent = Prefix + "duplicate_branch_event";
        internal const string CanonicalizeDetectorWindows = Prefix + "canonicalize_detector_windows";
        internal const string AddTransition = Prefix + "add_transition";
        internal const string AddTransitionToStage = Prefix + "add_transition_to_stage";
        internal const string CycleDetectorFact = Prefix + "cycle_detector_fact";
        internal const string CycleDetectorTrigger = Prefix + "cycle_detector_trigger";
        internal const string CycleDetectorWindowMode = Prefix + "cycle_detector_window_mode";
        internal const string MoveDetectorWindowStart = Prefix + "move_detector_window_start";
        internal const string ResizeDetectorWindow = Prefix + "resize_detector_window";
        internal const string AdjustDetectorPriority = Prefix + "adjust_detector_priority";
        internal const string CycleTransitionPolicy = Prefix + "cycle_transition_policy";
        internal const string CycleTransitionTarget = Prefix + "cycle_transition_target";
        internal const string RemoveSelectedElement = Prefix + "remove_selected_element";
        internal const string EditTriggerEvent = Prefix + "edit_trigger_event";
        internal const string DeleteTriggerEvent = Prefix + "delete_trigger_event";
        internal const string ZoomTimelineOut = Prefix + "zoom_timeline_out";
        internal const string ZoomTimelineIn = Prefix + "zoom_timeline_in";
        internal const string FitTimeline = Prefix + "fit_timeline";
        internal const string PanTimelineLeft = Prefix + "pan_timeline_left";
        internal const string PanTimelineRight = Prefix + "pan_timeline_right";
        internal const string ScrollTimelineUp = Prefix + "scroll_timeline_up";
        internal const string ScrollTimelineDown = Prefix + "scroll_timeline_down";
        internal const string ToggleStageSnap = Prefix + "toggle_stage_snap";
        internal const string DuplicateStages = Prefix + "duplicate_stages";
        internal const string DeleteStages = Prefix + "delete_stages";
        internal const string UndoStageEdit = Prefix + "undo_stage_edit";
        internal const string RedoStageEdit = Prefix + "redo_stage_edit";
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorCardElementCommand
    {
        internal CharacterTestRuntimeAiLabBehaviorCardElementCommand(
            string stableId,
            int direction = 0,
            CharacterTestRuntimeAiLabLogicElementKind kind = CharacterTestRuntimeAiLabLogicElementKind.None)
        {
            StableId = stableId ?? string.Empty;
            Direction = direction;
            Kind = kind;
        }

        internal string StableId { get; }
        internal int Direction { get; }
        internal CharacterTestRuntimeAiLabLogicElementKind Kind { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand
    {
        internal CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand(
            string timelineId,
            string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            int conditionIndex = -1,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value = default)
        {
            TimelineId = timelineId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Field = field;
            ConditionIndex = conditionIndex;
            Value = value;
        }

        internal string TimelineId { get; }
        internal string EventId { get; }
        internal CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField Field { get; }
        internal int ConditionIndex { get; }
        internal CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue Value { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorRecipeCharacterActionEventCommand
    {
        internal CharacterTestRuntimeAiLabBehaviorRecipeCharacterActionEventCommand(
            string timelineId,
            int frame,
            MxFramework.CharacterAction.CharacterActionId characterActionId)
        {
            TimelineId = timelineId ?? string.Empty;
            Frame = frame;
            CharacterActionId = characterActionId;
        }

        internal string TimelineId { get; }
        internal int Frame { get; }
        internal MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; }
    }

    internal enum CharacterTestRuntimeAiLabTransitionResultPreset
    {
        Automatic = 0,
        NextStage = 1,
        EndBehaviorCard = 2,
        Replan = 3
    }

    /// <summary>
    /// Page-scoped command boundary for the Runtime AI Lab Behavior Card workspace.
    /// It owns presentation paging only; Draft mutations remain in the existing controller.
    /// </summary>
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
    {
        private readonly CharacterTestRuntimeAiLabBehaviorCardWorkspaceEditCommandHandler _editHandler =
            new CharacterTestRuntimeAiLabBehaviorCardWorkspaceEditCommandHandler();
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private CharacterTestRuntimeAiPlannerLocalPlayerDeckService _deckService;
        private Func<string, bool> _openCardInLab;
        private Action _refresh;
        private Func<CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel> _workspaceView;
        private Func<CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView> _libraryView;
        private Func<int> _selectedEntryIndex;
        private Action<int> _selectHistoricalEntry;
        private Action<int> _selectEntry;
        private Action<CharacterTestRuntimeAiLabLogicElementKind, int> _setSelection;

        internal int FirstVisibleLibraryIndex => FindProjectedIndex(LibraryState.FirstVisibleStableCardId, 0);
        internal int FocusedLibraryIndex => FindProjectedIndex(LibraryState.FocusedStableCardId, -1);
        internal CharacterTestRuntimeAiLabBehaviorCardLibraryFilter LibraryFilter => LibraryState.Filter;
        internal string LibraryQuery => LibraryState.Query;
        internal string LastMessage { get; private set; } = string.Empty;
        internal bool CanonicalizationConfirmationPending =>
            _editHandler.CanonicalizationConfirmationPending;
        internal void CancelCascadeConfirmation() =>
            _editHandler.CancelCascadeConfirmation();
        internal bool TryAuthorizeCascadeDeletion(string detectorId, out string message)
        {
            bool authorized = _editHandler.TryAuthorizeCascadeDeletion(detectorId, out message);
            LastMessage = message;
            return authorized;
        }
        internal bool IsHistorical =>
            _controller?.IsHistoricalFrameSelected == true
            || _workspaceView?.Invoke()?.Timeline?.Historical == true;

        internal bool TryRestoreRecipeEventSelection(string timelineId, string eventId)
        {
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace = _workspaceView?.Invoke();
            if (workspace?.Timeline?.Presentation == null
                || string.IsNullOrWhiteSpace(timelineId)
                || string.IsNullOrWhiteSpace(eventId))
            {
                return false;
            }
            workspace.Timeline.Presentation.SelectEvent(timelineId, eventId);
            workspace.Timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
            workspace.Timeline.Presentation.SelectedRecipeEventStableKey =
                BehaviorRecipeTimelineProjection.BuildV6EventStableKey(timelineId, eventId);
            _refresh?.Invoke();
            return true;
        }

        internal static bool HostsBehaviorCardContext(CharacterTestRuntimeAiLabShellState state)
        {
            return state != null
                && (state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    || (state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                        && state.PageContext.TimelineMode
                            != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace));
        }

        internal void CancelCanonicalizationConfirmation()
        {
            _editHandler.CancelCanonicalizationConfirmation();
        }

        internal CharacterTestRuntimeAiLabBehaviorCardLibraryState LibraryState =>
            _controller?.BehaviorCardPresentationLibraryState
            ?? CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;

        internal void Configure(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService deckService,
            Func<string, bool> openCardInLab,
            Action refresh,
            Func<int> selectedEntryIndex = null,
            Action<CharacterTestRuntimeAiLabLogicElementKind, int> setSelection = null,
            Func<CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel> workspaceView = null,
            Func<CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView> libraryView = null,
            Action<int> selectHistoricalEntry = null,
            Action<int> selectEntry = null)
        {
            _controller = controller;
            _deckService = deckService;
            _openCardInLab = openCardInLab;
            _refresh = refresh;
            _workspaceView = workspaceView;
            _libraryView = libraryView;
            _selectedEntryIndex = selectedEntryIndex;
            _selectHistoricalEntry = selectHistoricalEntry;
            _selectEntry = selectEntry;
            _setSelection = setSelection;
            _editHandler.Configure(
                controller,
                refresh,
                selectedEntryIndex,
                setSelection,
                workspaceView);
            ReconcileLibraryIndex();
        }

        internal bool TryHandle(MxUiCommand command, out string message)
        {
            message = string.Empty;
            string commandId = command.CommandId;
            if (string.IsNullOrEmpty(commandId)
                || !commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.Prefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardElementCommand element =
                command.Payload is CharacterTestRuntimeAiLabBehaviorCardElementCommand typed
                    ? typed
                    : default;
            if (commandId == CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.EditTriggerEvent
                && command.Payload is CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand triggerEdit)
            {
                ApplyTriggerEventEdit(triggerEdit);
                message = LastMessage;
                return true;
            }
            if (commandId == CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteTriggerEvent
                && command.Payload is CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand triggerDelete)
            {
                DeleteTriggerEvent(triggerDelete);
                message = LastMessage;
                return true;
            }
            if (commandId == CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCharacterActionEventAtFrame
                && command.Payload is CharacterTestRuntimeAiLabBehaviorRecipeCharacterActionEventCommand actionEvent)
            {
                if (IsHistorical)
                {
                    LastMessage = CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy.ReadOnlyDiagnostic;
                    message = LastMessage;
                    return true;
                }
                _editHandler.AddCharacterActionEventAtFrame(actionEvent);
                LastMessage = _editHandler.LastMessage;
                message = LastMessage;
                return true;
            }
            if (IsHistorical
                && !CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .IsWorkspaceCommandReadOnlyAllowed(commandId))
            {
                CancelStageEditSessions(clearSelection: false);
                _editHandler.CancelPendingConfirmations();
                LastMessage = CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .ReadOnlyDiagnostic;
                message = LastMessage;
                return true;
            }
            if (_editHandler.TryHandle(commandId, element, out string editMessage))
            {
                LastMessage = editMessage;
                message = editMessage;
                return true;
            }
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.OpenLibraryCard:
                    TryOpenCard(element.StableId);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineOut:
                    UpdateTimelineViewport(0, -1, 0, false);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineIn:
                    UpdateTimelineViewport(0, 1, 0, false);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.FitTimeline:
                    UpdateTimelineViewport(0, 0, 0, true);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineLeft:
                    UpdateTimelineViewport(-1, 0, 0, false);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineRight:
                    UpdateTimelineViewport(1, 0, 0, false);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineUp:
                    UpdateTimelineViewport(0, 0, -1, false);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineDown:
                    UpdateTimelineViewport(0, 0, 1, false);
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ToggleStageSnap:
                    ToggleStageSnap();
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateStages:
                    DuplicateSelectedStages();
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteStages:
                    DeleteSelectedStages();
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.UndoStageEdit:
                    UndoStageEdit();
                    break;
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RedoStageEdit:
                    RedoStageEdit();
                    break;
                default:
                    LastMessage = "未知的行为卡工作区命令。";
                    break;
            }
            message = LastMessage;
            return true;
        }

        internal bool ApplyRecipeTriggerEventField(
            string timelineId,
            string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value)
        {
            if (string.IsNullOrWhiteSpace(timelineId) || string.IsNullOrWhiteSpace(eventId))
            {
                LastMessage = "Timeline event identity is unavailable.";
                return false;
            }
            return ApplyTriggerEventEdit(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand(
                timelineId,
                eventId,
                field,
                value: value));
        }

        internal bool SelectRecipeInspector(
            BehaviorRecipeInspectorTargetKind targetKind,
            string timelineId = "",
            string eventId = "")
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (timeline == null)
                return false;

            if (timeline.TacticalPreview?.Editor != null) timeline.TacticalPreview.Editor.ConditionsOpen = false;

            switch (targetKind)
            {
                case BehaviorRecipeInspectorTargetKind.Event:
                    if (string.IsNullOrWhiteSpace(timelineId) || string.IsNullOrWhiteSpace(eventId))
                        return false;
                    timeline.Presentation.SelectEvent(timelineId, eventId);
                    timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
                    timeline.Presentation.SelectedRecipeEventStableKey =
                        BehaviorRecipeTimelineProjection.BuildV6EventStableKey(timelineId, eventId);
                    break;
                case BehaviorRecipeInspectorTargetKind.Timeline:
                    if (string.IsNullOrWhiteSpace(timelineId))
                        return false;
                    timeline.Presentation.SelectTimeline(timelineId);
                    timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                    timeline.Presentation.SelectedRecipeEventStableKey =
                        "condition-owner:timeline:" + timelineId;
                    break;
                default:
                    timeline.Presentation.SelectRecipe();
                    timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                    timeline.Presentation.SelectedRecipeEventStableKey = "condition-owner:activation";
                    break;
            }

            _refresh?.Invoke();
            return true;
        }

        private bool ApplyTriggerEventEdit(
            in CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand command)
        {
            if (!_editHandler.TryPrepareGraphMutation(out int entryIndex, out var identity))
            {
                LastMessage = _editHandler.LastMessage;
                return false;
            }
            bool changed = _controller.TryApplyBehaviorRecipeTriggerEventField(
                entryIndex,
                identity,
                command.TimelineId,
                command.EventId,
                command.Field,
                command.ConditionIndex,
                command.Value,
                out string resultingCardDraftId,
                out string message);
            LastMessage = message ?? string.Empty;
            if (!changed)
                return false;
            _editHandler.PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                "event." + command.EventId,
                resultingCardDraftId);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                bool v6 = CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(
                    timeline.RecipePayload);
                if (v6)
                {
                    if (string.Equals(
                        command.EventId,
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                        StringComparison.Ordinal))
                    {
                        timeline.Presentation.SelectRecipe();
                        timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                        timeline.Presentation.SelectedRecipeEventStableKey = "condition-owner:activation";
                    }
                    else if (string.Equals(
                        command.EventId,
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                        StringComparison.Ordinal))
                    {
                        timeline.Presentation.SelectTimeline(command.TimelineId);
                        timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                        timeline.Presentation.SelectedRecipeEventStableKey =
                            "condition-owner:timeline:" + command.TimelineId;
                    }
                    else
                    {
                        timeline.Presentation.SelectEvent(command.TimelineId, command.EventId);
                        timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
                        timeline.Presentation.SelectedRecipeEventStableKey =
                            BehaviorRecipeTimelineProjection.BuildV6EventStableKey(
                                command.TimelineId,
                                command.EventId);
                    }
                }
                else
                {
                    timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Branch;
                    timeline.Presentation.SelectedRecipeEventStableKey =
                        "event:" + command.TimelineId + ":detector:event." + command.EventId;
                }
            }
            _refresh?.Invoke();
            return true;
        }

        private bool DeleteTriggerEvent(
            in CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand command)
        {
            if (!_editHandler.TryPrepareGraphMutation(out int entryIndex, out var identity))
            {
                LastMessage = _editHandler.LastMessage;
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                    "action=delete-event result=rejected timeline="
                    + command.TimelineId
                    + " event="
                    + command.EventId
                    + " detail="
                    + LastMessage);
                return false;
            }
            int beforeCount = CountRecipeEvents(_workspaceView?.Invoke()?.Timeline?.RecipePayload);
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                "command",
                "delete-event",
                command.EventId,
                entryIndex,
                identity,
                "requested",
                "正在删除事件…",
                beforeCount);
            bool changed = _controller.TryDeleteBehaviorRecipeTriggerEvent(
                entryIndex,
                identity,
                command.TimelineId,
                command.EventId,
                out string resultingCardDraftId,
                out string message);
            LastMessage = message ?? string.Empty;
            int afterCount = changed ? Math.Max(0, beforeCount - 1) : beforeCount;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                "mutation",
                "delete-event",
                command.EventId,
                entryIndex,
                identity,
                changed ? "committed" : "failed",
                LastMessage,
                beforeCount,
                afterCount);
            if (!changed)
                return false;
            _editHandler.PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.None,
                string.Empty,
                resultingCardDraftId);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = _workspaceView?.Invoke()?.Timeline;
            if (timeline != null)
            {
                timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                timeline.Presentation.SelectedRecipeEventStableKey = string.Empty;
                timeline.Presentation.SelectTimeline(command.TimelineId);
            }
            _refresh?.Invoke();
            return true;
        }

        private static int CountRecipeEvents(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            int count = 0;
            for (int timelineIndex = 0;
                 payload?.Timelines != null && timelineIndex < payload.Timelines.Count;
                 timelineIndex++)
            {
                count += payload.Timelines[timelineIndex]?.Events?.Count ?? 0;
            }
            return count;
        }

        private bool RejectHistoricalAuthoring()
        {
            if (!IsHistorical)
                return false;
            LastMessage = CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                .ReadOnlyDiagnostic;
            return true;
        }

        internal void SetLibraryQuery(string query)
        {
            string normalized = CharacterTestRuntimeAiLabBehaviorCardLibraryQuery.Normalize(query);
            if (string.Equals(normalized, LibraryQuery, StringComparison.Ordinal))
                return;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                "library-query=" + normalized);
            CommitLibraryState(new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                normalized,
                LibraryFilter,
                FirstProjectedStableCardId(),
                LibraryState.FocusedStableCardId,
                0f));
        }

        internal void CycleLibraryFilter()
        {
            CharacterTestRuntimeAiLabBehaviorCardLibraryFilter next;
            switch (LibraryFilter)
            {
                case CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All:
                    next = CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed;
                    break;
                case CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed:
                    next = CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnlockedAvailable;
                    break;
                case CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnlockedAvailable:
                    next = CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnavailableOrIncompatible;
                    break;
                default:
                    next = CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All;
                    break;
            }
            SetLibraryFilter(next);
        }

        internal void SetLibraryFilter(CharacterTestRuntimeAiLabBehaviorCardLibraryFilter filter)
        {
            CommitLibraryState(new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                LibraryQuery,
                filter,
                FirstProjectedStableCardId(),
                LibraryState.FocusedStableCardId,
                0f));
        }

        internal CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView CurrentLibrary =>
            _libraryView?.Invoke() ?? _deckService?.Workbench?.View;

        internal bool TryOpenCard(string stableCardId)
        {
            if (string.IsNullOrWhiteSpace(stableCardId) || _openCardInLab == null)
            {
                LastMessage = "行为卡打开入口不可用。";
                return false;
            }

            string normalizedStableCardId = stableCardId.Trim();
            if (IsHistorical)
                return TryOpenHistoricalCard(normalizedStableCardId);
            if (TryFindProjectedCard(
                    normalizedStableCardId,
                    out CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card)
                && !card.Available)
            {
                LastMessage = string.IsNullOrEmpty(card.BlockReasonText)
                    ? "该行为卡当前不可用，不能进入 Lab 编辑。"
                    : card.BlockReasonText;
                return false;
            }
            if (card == null
                || !TryFindWorkbenchCard(
                    normalizedStableCardId,
                    out CharacterTestRuntimeAiPlannerPlayerDeckCardView currentCard))
            {
                LastMessage = "该行为卡已不在当前可用牌库投影中，操作已取消。";
                return false;
            }
            if (currentCard.Availability
                != CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available)
            {
                LastMessage = "该行为卡的当前 Published revision 已不可用或不兼容，操作已取消。";
                return false;
            }

            bool opened = _openCardInLab(normalizedStableCardId);
            LastMessage = opened ? "已切换行为卡。" : "行为卡未能打开，当前草稿保持不变。";
            if (opened)
            {
                _editHandler.CancelPendingConfirmations();
                _refresh?.Invoke();
            }
            return opened;
        }

        internal bool TryUpgradeProjectCardReference(string stableCardId)
        {
            string message = "Runtime AI Lab 项目行为卡编辑器不可用。";
            bool changed = _controller != null
                && _controller.TryUpgradeCurrentGroupCardReference(stableCardId, out message);
            LastMessage = message ?? string.Empty;
            if (changed)
                _refresh?.Invoke();
            return changed;
        }

        internal bool TryAddProjectCardToCurrentBehaviorGroup(string stableCardId)
        {
            string message = "Runtime AI Lab 项目行为组编辑器不可用。";
            int entryIndex = -1;
            bool changed = _controller != null
                && _controller.TryAddProjectCardToCurrentBehaviorGroup(
                    stableCardId,
                    out entryIndex,
                    out message);
            LastMessage = message ?? string.Empty;
            if (changed)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView library = _libraryView?.Invoke();
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = null;
                for (int i = 0; library?.Cards != null && i < library.Cards.Count; i++)
                {
                    if (string.Equals(library.Cards[i].StableCardId, stableCardId, StringComparison.Ordinal))
                    {
                        card = library.Cards[i];
                        break;
                    }
                }
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities =
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy
                        .ResolveCompatibleResponsibilities(card?.RequiredCapabilities);
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                    "library-add=" + stableCardId
                    + ";responsibilities=" + string.Join(",", responsibilities));
                _selectEntry?.Invoke(entryIndex);
                _refresh?.Invoke();
            }
            return changed;
        }

        private bool TryOpenHistoricalCard(string stableCardId)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = _controller?.DraftWorkspace;
            if (workspace == null
                || !workspace.TryGetGroupForSlot(
                    _controller.SelectedSetupTeamId,
                    _controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                LastMessage = "HISTORY_CARD_CONTEXT_UNAVAILABLE";
                return false;
            }
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (!workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                {
                    continue;
                }
                string candidateId = card.HasPublishedSource
                    ? card.PublishedStableId
                    : entry.BehaviorId;
                if (!string.Equals(candidateId, stableCardId, StringComparison.Ordinal))
                    continue;
                _selectHistoricalEntry?.Invoke(i);
                LastMessage = "已切换历史行为卡视图；当前 Draft 身份保持不变。";
                _refresh?.Invoke();
                return true;
            }
            LastMessage = "HISTORY_CARD_NOT_IN_CURRENT_DRAFT";
            return false;
        }

        internal bool MoveLibraryPage(int direction, int visibleCapacity, int projectedCardCount)
        {
            int count = Math.Max(0, projectedCardCount);
            int capacity = Math.Max(1, visibleCapacity);
            int maximum = Math.Max(0, count - capacity);
            int target = Math.Max(0, Math.Min(maximum, FirstVisibleLibraryIndex + direction * capacity));
            if (target == FirstVisibleLibraryIndex)
                return false;
            return CommitLibraryState(new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                LibraryQuery,
                LibraryFilter,
                ProjectedStableCardId(target),
                LibraryState.FocusedStableCardId,
                target));
        }

        internal bool FocusLibraryCard(int visibleIndex, int projectedCardCount)
        {
            int absoluteIndex = FirstVisibleLibraryIndex + visibleIndex;
            int count = Math.Max(0, projectedCardCount);
            if (absoluteIndex < 0 || absoluteIndex >= count)
                return false;
            return CommitLibraryState(new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                LibraryQuery,
                LibraryFilter,
                LibraryState.FirstVisibleStableCardId,
                ProjectedStableCardId(absoluteIndex),
                LibraryState.ScrollOffsetY));
        }

        internal bool UpdateTimelineViewport(
            int horizontalDirection,
            int zoomDirection,
            int verticalDirection,
            bool fit)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null
                || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                LastMessage = "行为时间轴上下文已失效。";
                return false;
            }

            CharacterTestRuntimeAiLabPageContext page = state.PageContext;
            CharacterTestRuntimeAiLabBehaviorCardViewport current =
                _controller.BehaviorCardPresentationViewport;
            int contentFrames = Math.Max(60, _workspaceView?.Invoke()?.Timeline?.DurationFrames ?? 240);
            var currentViewport = new BehaviorTimelineViewport(
                current.HorizontalStartFrame,
                current.HorizontalEndFrame,
                current.ZoomLevel,
                current.VerticalScrollOffset,
                current.FollowCurrent,
                StageSnapEnabled ? 15 : 1);
            BehaviorTimelineViewport nextViewport = BehaviorPlanViewportCommands.CalculateNext(
                currentViewport,
                contentFrames,
                horizontalDirection,
                zoomDirection,
                verticalDirection,
                fit);
            var next = new CharacterTestRuntimeAiLabBehaviorCardViewport(
                nextViewport.StartRelativeFrame,
                nextViewport.EndRelativeFrame,
                nextViewport.ZoomLevel,
                nextViewport.VerticalScrollOffset,
                nextViewport.FollowCurrent);
            if (_controller.HistoricalBehaviorPresentationActive)
            {
                _controller.UpdateHistoricalBehaviorCardViewport(next);
                LastMessage = "历史行为时间轴视口已更新。";
                _refresh?.Invoke();
                return true;
            }
            CharacterTestRuntimeAiLabNavigationResult result = coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                page.CardDraftId,
                page.SelectedElementKind,
                page.SelectedElementStableId,
                next);
            LastMessage = result.Success ? "行为时间轴视口已更新。" : result.Message;
            if (result.Success)
                _refresh?.Invoke();
            return result.Success;
        }

        internal bool CommitTimelineViewport(
            int startFrame,
            int endFrame,
            float zoomLevel,
            float verticalScrollOffset,
            bool followCurrent)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null
                || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                return false;
            }

            var next = new CharacterTestRuntimeAiLabBehaviorCardViewport(
                Math.Max(0, startFrame),
                Math.Max(startFrame + 1, endFrame),
                Math.Max(0.01f, zoomLevel),
                Math.Max(0f, verticalScrollOffset),
                followCurrent);
            if (_controller.HistoricalBehaviorPresentationActive)
            {
                _controller.UpdateHistoricalBehaviorCardViewport(next);
                return true;
            }

            CharacterTestRuntimeAiLabPageContext page = state.PageContext;
            return coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                page.CardDraftId,
                page.SelectedElementKind,
                page.SelectedElementStableId,
                next).Success;
        }

        internal void ReconcileLibraryIndex()
        {
            CharacterTestRuntimeAiLabBehaviorCardLibraryState state = LibraryState;
            string first = FindProjectedIndex(state.FirstVisibleStableCardId, -1) >= 0
                ? state.FirstVisibleStableCardId
                : FirstProjectedStableCardId();
            string focused = string.IsNullOrEmpty(state.FocusedStableCardId)
                || WorkbenchContainsStableCardId(state.FocusedStableCardId)
                    ? state.FocusedStableCardId
                    : string.Empty;
            if (!string.Equals(first, state.FirstVisibleStableCardId, StringComparison.Ordinal)
                || !string.Equals(focused, state.FocusedStableCardId, StringComparison.Ordinal))
            {
                CommitLibraryState(new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    state.Query,
                    state.Filter,
                    first,
                    focused,
                    state.ScrollOffsetY));
            }
        }

        private int FindProjectedIndex(string stableCardId, int fallback)
        {
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library = _workspaceView?.Invoke()?.Library;
            int count = Math.Min(library?.ActiveCount ?? 0, library?.Cards?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(library.Cards[i]?.StableCardId, stableCardId, StringComparison.Ordinal))
                    return i;
            }
            return fallback;
        }

        private string ProjectedStableCardId(int index)
        {
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library = _workspaceView?.Invoke()?.Library;
            int count = Math.Min(library?.ActiveCount ?? 0, library?.Cards?.Length ?? 0);
            return index >= 0 && index < count
                ? library.Cards[index]?.StableCardId ?? string.Empty
                : string.Empty;
        }

        private bool TryFindProjectedCard(
            string stableCardId,
            out CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card)
        {
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library = _workspaceView?.Invoke()?.Library;
            int count = Math.Min(library?.ActiveCount ?? 0, library?.Cards?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel candidate = library.Cards[i];
                if (candidate != null
                    && string.Equals(candidate.StableCardId, stableCardId, StringComparison.Ordinal))
                {
                    card = candidate;
                    return true;
                }
            }
            card = null;
            return false;
        }

        private bool WorkbenchContainsStableCardId(string stableCardId)
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view = CurrentLibrary;
            if (view == null)
                return true;
            for (int i = 0; i < view.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = view.Cards[i];
                if (card != null
                    && string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryFindWorkbenchCard(
            string stableCardId,
            out CharacterTestRuntimeAiPlannerPlayerDeckCardView card)
        {
            card = null;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view = CurrentLibrary;
            for (int i = 0; view != null && i < view.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView candidate = view.Cards[i];
                if (candidate != null
                    && string.Equals(candidate.StableCardId, stableCardId, StringComparison.Ordinal))
                {
                    card = candidate;
                    return true;
                }
            }
            return false;
        }

        private string FirstProjectedStableCardId() => ProjectedStableCardId(0);

        private bool CommitLibraryState(CharacterTestRuntimeAiLabBehaviorCardLibraryState state)
        {
            if (_controller?.HistoricalBehaviorPresentationActive == true)
            {
                _controller.UpdateHistoricalBehaviorCardLibraryState(state);
                LastMessage = "历史牌库视图已更新。";
                _refresh?.Invoke();
                return true;
            }
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState shell = coordinator?.CurrentState;
            if (shell == null)
                return false;
            CharacterTestRuntimeAiLabNavigationResult result = coordinator.TryUpdateBehaviorCardLibraryState(
                shell.Generation,
                shell.Revision,
                state);
            LastMessage = result.Message;
            if (result.Success)
                _refresh?.Invoke();
            return result.Success;
        }

    }
}
