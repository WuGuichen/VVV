using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private string ResolveCurrentBehaviorGroupName(int draftId)
        {
            if (_controller?.MatchConfig?.BehaviorGroups?.TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft) == true
                && draft != null)
            {
                return draft.DisplayName ?? draft.StableId;
            }
            return "当前行为组";
        }

        private static bool TryParseSetupCharacterCommand(string commandId, out int characterId)
        {
            characterId = 0;
            if (string.IsNullOrEmpty(commandId)
                || !commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .SelectSetupCharacterPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(
                    commandId.Substring(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .SelectSetupCharacterPrefix.Length),
                    out characterId)
                && characterId > 0;
        }

        private static bool TryParseSetupBehaviorGroupCommand(
            string commandId,
            out int draftId,
            out string groupDraftId)
        {
            draftId = 0;
            groupDraftId = string.Empty;
            if (string.IsNullOrEmpty(commandId)
                || !commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .SelectSetupBehaviorGroupPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string payload = commandId.Substring(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .SelectSetupBehaviorGroupPrefix.Length);
            int separator = payload.IndexOf('|');
            string arenaText = separator >= 0 ? payload.Substring(0, separator) : payload;
            if (!int.TryParse(arenaText, out draftId) || draftId < 0)
                return false;
            if (separator >= 0 && separator + 1 < payload.Length)
                groupDraftId = payload.Substring(separator + 1);
            return draftId > 0 || !string.IsNullOrWhiteSpace(groupDraftId);
        }

        internal void ConfigureBehaviorCardWorkspaceCommands(
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler commands)
        {
            _behaviorCardWorkspaceCommands = commands;
#if UNITY_EDITOR
            RestoreOpenCharacterActionWorkspaceAfterHostRebind();
#endif
        }

        internal void SetBehaviorCardSelection(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            int index)
        {
            SelectedLogicElementKind = kind;
            SelectedLogicElementIndex = Math.Max(0, index);
            SelectedCompiledStepIndex = kind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                ? SelectedLogicElementIndex
                : 0;
        }

        internal void SetBehaviorCardDiagnosticFocus(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            int index)
        {
            SetBehaviorCardSelection(kind, index);
            PublishBehaviorCardContextIfNeeded();
        }

        internal void SetHistoricalBehaviorCardEntryIndex(int index)
        {
            if (!_historicalPresentationSelectionCaptured)
                return;
            SelectedWorkbenchEntryIndex = Math.Max(0, index);
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = 0;
        }

        internal void SetWorkbenchEntryIndex(int index)
        {
            SelectedWorkbenchEntryIndex = Math.Max(0, index);
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = 0;
        }

        private void SynchronizeHistoricalPresentationState()
        {
            if (_controller == null)
                return;
            bool historical = _controller.IsHistoricalFrameSelected;
            if (historical && !_historicalPresentationSelectionCaptured)
            {
                _latestSelectedWorkbenchEntryIndex = SelectedWorkbenchEntryIndex;
                _latestSelectedCompiledStepIndex = SelectedCompiledStepIndex;
                _latestSelectedLogicElementKind = SelectedLogicElementKind;
                _latestSelectedLogicElementIndex = SelectedLogicElementIndex;
                _historicalPresentationSelectionCaptured = true;
                _controller.BeginHistoricalBehaviorPresentation();
                _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
                return;
            }
            if (historical || !_historicalPresentationSelectionCaptured)
                return;

            SelectedWorkbenchEntryIndex = _latestSelectedWorkbenchEntryIndex;
            SelectedCompiledStepIndex = _latestSelectedCompiledStepIndex;
            SelectedLogicElementKind = _latestSelectedLogicElementKind;
            SelectedLogicElementIndex = _latestSelectedLogicElementIndex;
            _historicalPresentationSelectionCaptured = false;
            if (!_controller.EndHistoricalBehaviorPresentation(out string diagnostic)
                && !string.IsNullOrEmpty(diagnostic))
            {
                LastStatusMessage = diagnostic;
            }
        }

        private string ExecuteTimelineItemNavigation(object payload)
        {
            if (!(payload is CharacterTestRuntimeAiLabTimelineNavigationCommand navigation)
                || !navigation.Locator.IsValid)
            {
                return "时间轴事件定位参数无效。";
            }
            return Execute(
                () => _controller.NavigateTimelineItem(navigation.Locator, navigation.UseEndFrame),
                navigation.UseEndFrame ? "已定位到事件终点。" : "已定位到事件起点。",
                "定位时间轴事件失败。");
        }

        private void SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode mode)
        {
            if (mode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                && _controller?.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic);
                return;
            }
            if (WorkspaceMode == mode)
                return;
            if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                && mode != CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
            {
                _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
            }
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            if (coordinator != null)
            {
                CharacterTestRuntimeAiLabWorkspace target = ToShellWorkspace(mode, DiagnosticsTab);
                CharacterTestRuntimeAiLabShellState shellState = coordinator.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult result;
                if (target == CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
                {
                    result = TryNavigateShellToSelectedBehaviorCard(coordinator, shellState);
                }
                else
                {
                    PublishBehaviorCardContextIfNeeded();
                    PublishRuntimeFrameContextIfNeeded();
                    shellState = coordinator.CurrentState;
                    result = coordinator.Navigate(
                        shellState.Generation,
                        shellState.Revision,
                        shellState.ActiveWorkspace,
                        target);
                }
                if (!result.Success)
                {
                    LastStatusMessage = result.Code + ": " + result.Message;
                    return;
                }
                SyncFromShellState();
                return;
            }
            if (mode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics)
            {
                _workspaceBeforeDiagnostics = WorkspaceMode;
            }
            WorkspaceMode = mode;
            _controller?.ClearTimelineItemSelection();
        }

        private bool IsBehaviorTestAuthoringContext()
        {
            if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                return true;

            return WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && TimelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic;
        }

        /// <remarks>
        /// LowFreqAlloc. Explicit user navigation only; the active RuntimeFrame root remains
        /// mounted when the command originates from the unified timeline host.
        /// </remarks>
        internal bool SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode mode)
        {
            if (_controller == null)
            {
                LastStatusMessage = "时间轴模式切换失败：控制器不可用。";
                return false;
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot = _controller.CaptureSnapshot();
            if (snapshot.Timeline == null || snapshot.Timeline.Count <= 0)
            {
                LastStatusMessage = "开始测试并保留至少一帧后才能切换时间轴模式。";
                return false;
            }

            if (mode != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                && !_controller.IsHistoricalFrameSelected)
            {
                if (mode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan)
                    _followCurrentRuntimeBehavior = true;
                TrySelectCurrentRuntimeBehaviorForPresentation(
                    openAuthoringWorkspace: false,
                    out _);
            }

            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                _controller.ShellCoordinator;
            if (coordinator == null)
            {
                TimelineMode = mode;
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
                LastStatusMessage = TimelineModeStatus(mode);
                return true;
            }

            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            if (state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
            {
                CharacterTestRuntimeAiLabNavigationResult navigation;
                if (mode != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)
                {
                    navigation = TryNavigateShellToSelectedBehaviorCard(coordinator, state);
                }
                else
                {
                    PublishBehaviorCardContextIfNeeded();
                    state = coordinator.CurrentState;
                    navigation = coordinator.Navigate(
                        state.Generation,
                        state.Revision,
                        state.ActiveWorkspace,
                        CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
                }
                if (!navigation.Success)
                {
                    LastStatusMessage = navigation.Code + ": " + navigation.Message;
                    return false;
                }
                SyncFromShellState();
                state = coordinator.CurrentState;
            }

            if (state.PageContext.TimelineMode
                == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)
            {
                PublishRuntimeFrameContextIfNeeded();
            }
            else
            {
                PublishBehaviorCardContextIfNeeded();
            }
            state = coordinator.CurrentState;

            if (mode != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                && !_controller.IsHistoricalFrameSelected
                && !_historicalPresentationSelectionCaptured
                && _controller.TryCaptureBehaviorCardShellContext(
                    SelectedWorkbenchEntryIndex,
                    SelectedLogicElementKind,
                    SelectedLogicElementIndex,
                    state.PageContext.BehaviorCardViewport,
                    out CharacterTestRuntimeAiLabPageContext behaviorPage))
            {
                CharacterTestRuntimeAiLabNavigationResult contextUpdate =
                    coordinator.TryUpdateBehaviorCardContext(
                        state.Generation,
                        state.Revision,
                        behaviorPage.GroupDraftId,
                        behaviorPage.EntryId,
                        behaviorPage.CardDraftId,
                        behaviorPage.SelectedElementKind,
                        behaviorPage.SelectedElementStableId,
                        behaviorPage.BehaviorCardViewport);
                if (!contextUpdate.Success)
                {
                    LastStatusMessage = contextUpdate.Code + ": " + contextUpdate.Message;
                    return false;
                }
                state = coordinator.CurrentState;
            }

            bool outgoingExpanded =
                state.PageContext.TimelineMode
                    == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                    ? _controller.TimelineExpanded
                    : state.PageContext.BehaviorPlanExpanded;
            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.TrySetTimelineMode(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    mode,
                    outgoingExpanded);
            if (!result.Success)
            {
                LastStatusMessage = result.Code + ": " + result.Message;
                return false;
            }

            SyncFromShellState();
            if (mode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)
            {
                _controller.RestoreTimelineExpanded(
                    coordinator.CurrentState.PageContext.RuntimeTraceExpanded);
            }
            LastStatusMessage = TimelineModeStatus(mode);
            return true;
        }

        private void OpenBehaviorLogicTab()
        {
            if (SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic))
                return;

            LastStatusMessage = PublishRuntimeTestStatus(
                "无法打开“编辑逻辑”：" + (LastStatusMessage ?? string.Empty));
        }

        /// <remarks>NoAlloc. Stable latest-frame refresh; explicit card selection disables following.</remarks>
        internal void SynchronizeLiveBehaviorPlanSelection()
        {
            if (!_followCurrentRuntimeBehavior
                || TimelineMode != CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan
                || _controller == null
                || _controller.IsHistoricalFrameSelected
                || !_controller.TryResolveLatestCommittedBehaviorEntryIndex(out int entryIndex)
                || entryIndex == SelectedWorkbenchEntryIndex)
            {
                return;
            }

            SelectedWorkbenchEntryIndex = entryIndex;
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = 0;
        }

        private static string TimelineModeStatus(CharacterTestRuntimeAiLabTimelineMode mode)
        {
            return mode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                ? "已切换到运行轨迹。"
                : mode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan
                    ? "已切换到行为计划。"
                    : "已切换到编辑逻辑。";
        }

        private void PersistActiveTimelineExpanded(bool expanded)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null
                || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
            {
                return;
            }

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.TrySetTimelineMode(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    state.PageContext.TimelineMode,
                    expanded);
            if (result.Success)
                SyncFromShellState();
        }

        private bool RejectReplayNavigationWhileBusy()
        {
            if (_controller == null
                || WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                || DiagnosticsTab != CharacterTestRuntimeAiLabDiagnosticsTab.Replay
                || !_controller.CaptureReplayValidationWorkspaceSnapshot().IsBusy)
            {
                return false;
            }
            LastStatusMessage = "回放校验正在运行；离开当前工作区前请先取消。";
            return true;
        }

        private bool NavigateWorkspaceBack()
        {
            if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary)
            { CloseTacticalLibrary(); return true; }
            PublishBehaviorCardContextIfNeeded();
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            if (coordinator != null)
            {
                CharacterTestRuntimeAiLabShellState shellState = coordinator.CurrentState;
                if (shellState.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.Replay
                    && _replayReturnToken.IsValid)
                {
                    if (!_controller.MatchesReplayReturnToken(_replayReturnToken))
                    {
                        _replayReturnToken = default;
                        LastStatusMessage = "REPLAY_RETURN_TOKEN_STALE";
                        return true;
                    }
                    CharacterTestRuntimeAiLabNavigationResult replayReturn = coordinator.Navigate(
                        shellState.Generation,
                        shellState.Revision,
                        shellState.ActiveWorkspace,
                        CharacterTestRuntimeAiLabWorkspace.Replay);
                    if (replayReturn.Success)
                    {
                        int restoredFrame = _replayReturnToken.ReplayFrame;
                        _replayReturnToken = default;
                        SyncFromShellState();
                        LastStatusMessage = "Replay context restored · frame "
                            + restoredFrame.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        return true;
                    }
                    LastStatusMessage = "REPLAY_RETURN_NAVIGATION_FAILED: " + replayReturn.Message;
                    return true;
                }
                CharacterTestRuntimeAiLabNavigationResult back = coordinator.Back(
                    shellState.Generation,
                    shellState.Revision,
                    shellState.ActiveWorkspace);
                if (!back.Success)
                {
                    LastStatusMessage = "WORKSPACE_BACK_FAILED: " + back.Message;
                    return true;
                }
                if (coordinator.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.Setup
                    || coordinator.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                    _controller.EndStandaloneTacticalEditing();
                SyncFromShellState();
                return false;
            }
            if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.Setup);
            else if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup)
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.Setup);
            else if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics)
                SetWorkspaceMode(_workspaceBeforeDiagnostics);
            else
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.Setup);
            return false;
        }

        private void ResetTransientUiContextAfterTerminalClose()
        {
            _controller?.ResetTransientUiContextForClose();
            _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: true);
            _replayReturnToken = default;
            _workspaceBeforeDiagnostics = CharacterTestRuntimeAiLabWorkspaceMode.Setup;
            _historicalPresentationSelectionCaptured = false;
            _latestSelectedWorkbenchEntryIndex = 0;
            _latestSelectedCompiledStepIndex = 0;
            _latestSelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.None;
            _latestSelectedLogicElementIndex = -1;
            SelectedWorkbenchEntryIndex = 0;
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.None;
            SelectedLogicElementIndex = -1;
            WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Setup;
            DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts;
            TimelineMode = CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
        }

        private void OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab tab)
        {
            if (tab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame
                && _controller?.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
                return;
            }
            string runtimeFrameRestoreDiagnostic = string.Empty;
            _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            if (coordinator != null)
            {
                PublishBehaviorCardContextIfNeeded();
                PublishRuntimeFrameContextIfNeeded();
                CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult result = coordinator.Navigate(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    ToShellWorkspace(CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics, tab));
                if (!result.Success)
                {
                    LastStatusMessage = result.Code + ": " + result.Message;
                    return;
                }
                SyncFromShellState();
                if (tab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame)
                {
                    _controller.RestoreRuntimeFramePageContext(
                        coordinator.CurrentState.PageContext.RuntimeFrameContext,
                        out runtimeFrameRestoreDiagnostic);
                }
            }
            else
            {
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics);
                DiagnosticsTab = tab;
            }
            LastStatusMessage = tab == CharacterTestRuntimeAiLabDiagnosticsTab.Facts
                ? "已打开 Facts 诊断页。"
                : tab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame
                    ? "已打开 RuntimeFrame 调试页。"
                    : "已打开 Replay 调试页。";
            if (!string.IsNullOrEmpty(runtimeFrameRestoreDiagnostic))
                LastStatusMessage = runtimeFrameRestoreDiagnostic;
        }

        private void PublishRuntimeFrameContextIfNeeded()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
                return;
            coordinator.TryUpdateRuntimeFrameContext(
                state.Generation,
                state.Revision,
                _controller.CaptureRuntimeFramePageContext());
        }

        internal void RestoreNavigationState(
            CharacterTestRuntimeAiLabWorkspaceMode workspaceMode,
            CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab,
            CharacterTestRuntimeAiLabTimelineMode timelineMode,
            CharacterTestRuntimeAiLabWorkspaceMode workspaceBeforeDiagnostics,
            int selectedWorkbenchEntryIndex,
            int selectedCompiledStepIndex,
            CharacterTestRuntimeAiLabLogicElementKind selectedLogicElementKind,
            int selectedLogicElementIndex)
        {
            WorkspaceMode = workspaceMode;
            DiagnosticsTab = diagnosticsTab;
            TimelineMode = timelineMode;
            _workspaceBeforeDiagnostics = workspaceBeforeDiagnostics;
            SelectedWorkbenchEntryIndex = Math.Max(0, selectedWorkbenchEntryIndex);
            SelectedCompiledStepIndex = Math.Max(0, selectedCompiledStepIndex);
            SelectedLogicElementKind = selectedLogicElementIndex >= 0
                ? selectedLogicElementKind
                : CharacterTestRuntimeAiLabLogicElementKind.None;
            SelectedLogicElementIndex = Math.Max(-1, selectedLogicElementIndex);
        }

        /// <remarks>NoAlloc. A pending mount takes precedence until its transaction commits.</remarks>
        internal void SynchronizeShellNavigationState(CharacterTestRuntimeAiLabShellState mountingState = null)
        {
            SyncFromShellState(mountingState);
        }

        private CharacterTestRuntimeAiLabNavigationResult TryNavigateShellToSelectedBehaviorCard(
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            CharacterTestRuntimeAiLabShellState shellState)
        {
            if (_controller.TryCaptureBehaviorCardShellContext(
                    SelectedWorkbenchEntryIndex,
                    SelectedLogicElementKind,
                    SelectedLogicElementIndex,
                    shellState.PageContext.BehaviorCardViewport,
                    out CharacterTestRuntimeAiLabPageContext page))
            {
                return coordinator.NavigateToBehaviorCard(
                    shellState.Generation,
                    shellState.Revision,
                    shellState.ActiveWorkspace,
                    page.GroupDraftId,
                    page.EntryId,
                    page.CardDraftId,
                    page.SelectedElementKind,
                    page.SelectedElementStableId,
                    page.BehaviorCardViewport);
            }

            return new CharacterTestRuntimeAiLabNavigationResult(
                false,
                "BEHAVIOR_CARD_CONTEXT_UNAVAILABLE",
                "The selected behavior card can no longer be resolved.",
                shellState.ActiveWorkspace,
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                shellState.Revision);
        }

        internal CharacterTestRuntimeAiPlannerDevSceneController LibraryController => _controller;
        internal void OpenTacticalCardTestLibrary()
        {
            if (_controller == null) return;
            if (!_controller.BeginTacticalCardTest(out string key, out string issue))
            { LastStatusMessage = issue; _panelStatusSink?.Invoke(issue); return; }
            // Restore the selection in the library; editing and using a card are explicit card-face actions.
            _controller.TacticalLibrarySelectedKey = key;
            _controller.TacticalLibraryQuery = string.Empty;
            _controller.TacticalLibraryPage = 0;
            OpenTacticalLibrary();
        }

        internal void OpenTacticalCardTest()
        {
            if (_controller == null) return;
            if (!_controller.BeginTacticalCardTest(out string key, out string issue))
            { LastStatusMessage = issue; _panelStatusSink?.Invoke(issue); return; }
            if (string.IsNullOrEmpty(key)) OpenTacticalLibrary();
            else if (!OpenTacticalLibraryCard(key)) _controller.EndTacticalCardTest();
        }
        internal void ResetTacticalCardTest()
            => LastStatusMessage = RestartRuntimeTestSession(returnToBehaviorCard: true);

        internal void OpenTacticalLibrary(bool preserveOrigin = false)
        {
            if (_controller == null) return;
            if ((WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard || IsRuntimeTestWorkspace)
                && !TryPrepareWorkspaceBackNavigation()) return;
            PublishBehaviorCardContextIfNeeded();
            var coordinator = _controller.ShellCoordinator;
            var state = coordinator.CurrentState;
            if (!preserveOrigin && state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.TacticalLibrary
                && (_controller.TacticalCardTestMode
                    || state.PageContext.BehaviorCardReturnWorkspace != CharacterTestRuntimeAiLabWorkspace.TacticalLibrary))
            {
                _controller.TacticalLibraryOrigin = state.PageContext;
                _controller.TacticalLibraryOriginCard = _controller.TacticalCardContext;
            }
            var result = coordinator.Navigate(state.Generation, state.Revision, state.ActiveWorkspace, CharacterTestRuntimeAiLabWorkspace.TacticalLibrary);
            LastStatusMessage = result.Success ? string.Empty : result.Message;
            SyncFromShellState(); _refreshPanel?.Invoke();
        }

        internal void CloseTacticalLibrary()
        {
            var coordinator = _controller?.ShellCoordinator;
            if (coordinator == null) return;
            if (_controller.TacticalLibraryOriginCard?.ConfiguredTestCardId is string testId)
            { OpenTacticalLibraryCard("test:" + testId); return; }
            var previous = _controller.TacticalCardContext;
            _controller.RestoreStandaloneTacticalEditing(_controller.TacticalLibraryOriginCard);
            var origin = _controller.TacticalLibraryOrigin ?? CharacterTestRuntimeAiLabPageContext.CreateDefault().WithWorkspace(CharacterTestRuntimeAiLabWorkspace.Setup);
            var state = coordinator.CurrentState;
            var result = coordinator.RestoreLibraryOrigin(state.Generation, state.Revision, origin);
            if (!result.Success) _controller.RestoreStandaloneTacticalEditing(previous);
            LastStatusMessage = result.Success ? string.Empty : result.Message;
            SyncFromShellState(); _refreshPanel?.Invoke();
        }

        internal bool OpenTacticalLibraryCard(string cardId, bool selectForTest = true, bool editBehavior = true)
        {
            if (_controller == null) return false;
            if (!_controller.CanSelectTacticalTestCard(out string selectionIssue))
            { LastStatusMessage = selectionIssue; _panelStatusSink?.Invoke(selectionIssue); return false; }
            var previous = _controller.TacticalCardContext;
            if (!_controller.BeginStandaloneTacticalEditing(cardId, out string issue))
            { LastStatusMessage = issue; _panelStatusSink?.Invoke(issue); return false; }
            SetWorkbenchEntryIndex(0);
            var coordinator = _controller.ShellCoordinator;
            var result = TryNavigateShellToSelectedBehaviorCard(coordinator, coordinator.CurrentState);
            if (!result.Success) { _controller.RestoreStandaloneTacticalEditing(previous); LastStatusMessage = result.Message; return false; }
            if (selectForTest)
            {
                _controller.SelectTacticalTestCard();
                _controller.PrepareCurrentTacticalCardSummaries();
            }
            else _controller.ClearPendingTacticalTestCard();
            SyncFromShellState();
            _openSelectedBehaviorStageAfterRefresh = editBehavior;
            _refreshPanel?.Invoke();
            return true;
        }

        private void OpenSelectedBehaviorData()
        {
            if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup)
            {
                LastStatusMessage = "当前页只管理行为卡装配；制作请从卡库进入。";
                return;
            }
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (coordinator == null || state == null)
            {
                LastStatusMessage = "行为卡编辑上下文不可用。";
                return;
            }

            CharacterTestRuntimeAiLabNavigationResult navigation =
                TryNavigateShellToSelectedBehaviorCard(coordinator, state);
            if (!navigation.Success)
            {
                LastStatusMessage = navigation.Code + ": " + navigation.Message;
                return;
            }

            SyncFromShellState();
            _openSelectedBehaviorStageAfterRefresh = true;
            LastStatusMessage = "已打开所选行为卡的数据编辑器。";
        }

        private bool IsLockedFreeStrategyEntry(int entryIndex)
        {
            return false;
        }

        /// <remarks>
        /// LowFreqAlloc. Runs only when the user opens the behavior-group authoring surface.
        /// </remarks>
        private int FindFirstEditableBehaviorEntryIndex()
        {
            if (_controller == null
                || !_controller.DraftWorkspace.TryGetGroupForSlot(
                    _controller.SelectedSetupTeamId,
                    _controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                return -1;
            }

            return group.Entries.Count > 0 ? 0 : -1;
        }

        private void PublishBehaviorCardContextIfNeeded()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            if (coordinator == null
                || _controller.IsHistoricalFrameSelected
                || _historicalPresentationSelectionCaptured
                || (coordinator.CurrentState.ActiveWorkspace
                        != CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    && (coordinator.CurrentState.ActiveWorkspace
                            != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                        || coordinator.CurrentState.PageContext.TimelineMode
                            == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)))
            {
                return;
            }

            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            if (!_controller.TryCaptureBehaviorCardShellContext(
                    SelectedWorkbenchEntryIndex,
                    SelectedLogicElementKind,
                    SelectedLogicElementIndex,
                    state.PageContext.BehaviorCardViewport,
                    out CharacterTestRuntimeAiLabPageContext page))
            {
                return;
            }
            coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                page.CardDraftId,
                page.SelectedElementKind,
                page.SelectedElementStableId,
                page.BehaviorCardViewport);
        }

        private void SyncFromShellState(CharacterTestRuntimeAiLabShellState mountingState = null)
        {
            CharacterTestRuntimeAiLabShellState state = mountingState ?? _controller?.ShellCoordinator?.CurrentState;
            if (state == null)
                return;

            switch (state.ActiveWorkspace)
            {
                case CharacterTestRuntimeAiLabWorkspace.TacticalLibrary:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary;
                    break;
                case CharacterTestRuntimeAiLabWorkspace.BehaviorGroup:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
                    break;
                case CharacterTestRuntimeAiLabWorkspace.BehaviorCard:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                    if (_controller.TryResolveBehaviorCardShellContext(
                            state.PageContext,
                            out int entryIndex,
                            out CharacterTestRuntimeAiLabLogicElementKind selectedKind,
                            out int selectedIndex))
                    {
                        SelectedWorkbenchEntryIndex = entryIndex;
                        SelectedLogicElementKind = selectedIndex >= 0
                            ? selectedKind
                            : CharacterTestRuntimeAiLabLogicElementKind.None;
                        SelectedLogicElementIndex = selectedIndex;
                        SelectedCompiledStepIndex = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                            ? Math.Max(0, selectedIndex)
                            : 0;
                    }
                    break;
                case CharacterTestRuntimeAiLabWorkspace.Facts:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts;
                    break;
                case CharacterTestRuntimeAiLabWorkspace.RuntimeFrame:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
                    TimelineMode = state.PageContext.TimelineMode;
                    if (TimelineMode != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                        && _controller.TryResolveBehaviorCardShellContext(
                            state.PageContext,
                            out int behaviorEntryIndex,
                            out CharacterTestRuntimeAiLabLogicElementKind behaviorSelectedKind,
                            out int behaviorSelectedIndex))
                    {
                        SelectedWorkbenchEntryIndex = behaviorEntryIndex;
                        SelectedLogicElementKind = behaviorSelectedIndex >= 0
                            ? behaviorSelectedKind
                            : CharacterTestRuntimeAiLabLogicElementKind.None;
                        SelectedLogicElementIndex = behaviorSelectedIndex;
                        SelectedCompiledStepIndex =
                            behaviorSelectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                                ? Math.Max(0, behaviorSelectedIndex)
                                : 0;
                    }
                    break;
                case CharacterTestRuntimeAiLabWorkspace.Replay:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Replay;
                    break;
                default:
                    WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Setup;
                    TimelineMode = CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
                    break;
            }
            CharacterTestRuntimeAiLabWorkspace returnWorkspace = state.PageContext.ReturnWorkspace;
            _workspaceBeforeDiagnostics = returnWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                ? CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                : CharacterTestRuntimeAiLabWorkspaceMode.Setup;
        }

        private static CharacterTestRuntimeAiLabWorkspace ToShellWorkspace(
            CharacterTestRuntimeAiLabWorkspaceMode workspace,
            CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab)
        {
            if (workspace == CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary)
                return CharacterTestRuntimeAiLabWorkspace.TacticalLibrary;
            if (workspace == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                return CharacterTestRuntimeAiLabWorkspace.BehaviorCard;
            if (workspace == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup)
                return CharacterTestRuntimeAiLabWorkspace.BehaviorGroup;
            if (workspace == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics)
            {
                if (diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame)
                    return CharacterTestRuntimeAiLabWorkspace.RuntimeFrame;
                if (diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Replay)
                    return CharacterTestRuntimeAiLabWorkspace.Replay;
                return CharacterTestRuntimeAiLabWorkspace.Facts;
            }
            return CharacterTestRuntimeAiLabWorkspace.Setup;
        }

        internal void SetShellStatus(
            string status,
            bool synchronizeWhenTransitionCompletes = false)
        {
            LastStatusMessage = status ?? string.Empty;
            _shellTransitionStatusPending = synchronizeWhenTransitionCompletes;
        }

        internal void SynchronizeDeferredBehaviorRecipeSaveStatus()
        {
            if ((!_behaviorRecipeSaveStatusPending && !_shellTransitionStatusPending)
                || _controller == null)
                return;
            if (_controller.BehaviorRecipeSaveQueued || _controller.BehaviorRecipeSaveInProgress)
                return;
            if (_shellTransitionStatusPending
                && _controller.ShellCoordinator?.CurrentState?.PresentationMode
                    == CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress)
            {
                return;
            }

            _behaviorRecipeSaveStatusPending = false;
            _shellTransitionStatusPending = false;
            _lastStatusMessage = _controller.StatusMessage ?? string.Empty;
        }

    }
}
