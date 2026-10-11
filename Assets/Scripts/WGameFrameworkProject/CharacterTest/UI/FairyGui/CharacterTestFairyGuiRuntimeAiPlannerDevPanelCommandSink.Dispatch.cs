using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        public void Enqueue(MxUiCommand command)
        {
            SynchronizeHistoricalPresentationState();
            CommandCount++;
            LastCommand = command;
            string commandId = command.CommandId;
            CharacterTestRuntimeAiLabShellState shellBeforeCommand = _controller?.ShellCoordinator?.CurrentState;
            if (commandId != CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteSetupBehaviorGroup)
                PendingSetupBehaviorGroupDeleteDraftId = -1;

            if (_behaviorCardWorkspaceCommands != null
                && _behaviorCardWorkspaceCommands.TryHandle(command, out string workspaceMessage))
            {
                LastStatusMessage = workspaceMessage;
                // The edit handler can refresh while it is still resolving the newly
                // created selection. Refresh once more after the command result becomes
                // shell authority so timeline, Inspector, and feedback stay in sync.
                _refreshPanel?.Invoke();
                return;
            }
            if (_behaviorCardWorkspaceCommands?.IsHistorical == true
                && CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .ClassifyShellCommand(commandId)
                    != CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.ReadOnlyAllowed)
            {
                _behaviorCardWorkspaceCommands.CancelStageEditSessions(clearSelection: false);
                LastStatusMessage = CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .ReadOnlyDiagnostic;
                _refreshPanel?.Invoke();
                return;
            }

            if (commandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close
                || commandId == MxFramework.UI.FairyGui.MxFairyGuiCommandIds.Cancel)
            {
                if (RejectReplayNavigationWhileBusy())
                    return;
                if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary)
                { CloseTacticalLibrary(); return; }
                if (!TryPrepareTerminalCloseNavigation())
                    return;
                if (_controller?.TacticalCardTestMode == true)
                {
                    LastStatusMessage = ReturnRuntimeTestToSetup();
                    return;
                }
                if (IsRuntimeTestWorkspace)
                {
                    bool returnToLibrary = _controller.HasStandaloneTacticalCard;
                    LastStatusMessage = ReturnRuntimeTestToSetup();
                    if (returnToLibrary) OpenTacticalLibrary(preserveOrigin: true);
                    return;
                }
                if (_controller?.ShellCoordinator != null)
                {
                    PublishBehaviorCardContextIfNeeded();
                    CharacterTestRuntimeAiLabShellCoordinator coordinator =
                        _controller.ShellCoordinator;
                    CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
                    CharacterTestRuntimeAiLabNavigationResult result =
                        coordinator.Back(
                            state.Generation,
                            state.Revision,
                            state.ActiveWorkspace);
                    if (!result.Success)
                    {
                        LastStatusMessage = result.Message;
                        return;
                    }
                    if (coordinator.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.Setup
                        || coordinator.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                        _controller.EndStandaloneTacticalEditing();
                    if (coordinator.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                    {
                        ResetTransientUiContextAfterTerminalClose();
                        LastStatusMessage = "已返回行为卡组。";
                    }
                    else
                    {
                        SyncFromShellState();
                        LastStatusMessage = coordinator.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.Setup
                            ? "已返回实验设置。" : "已返回进入编辑前的页面。";
                    }
                }
                else
                {
                    _closePanel?.Invoke();
                    LastStatusMessage = "Runtime AI Lab 已关闭。按 F9 重新打开。";
                }
                return;
            }

            if (commandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellHide)
            {
                LastStatusMessage = "Runtime AI Lab 已隐藏。按 F9 恢复恢复界面。";
                _controller?.ShellCoordinator?.ToggleVisibility();
                return;
            }

            if (commandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellRestore)
            {
                CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
                CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult? result = state != null
                    ? coordinator.RestoreLastWorkspace(state.Generation, state.Revision)
                    : null;
                LastStatusMessage = result.HasValue && result.Value.Success
                    ? "已返回最后一个安全工作区。"
                    : "无法恢复安全工作区。" + (result?.Message ?? string.Empty);
                return;
            }

            if (commandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellReset)
            {
                CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
                CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult? result = state != null
                    ? coordinator.ResetRecoveryToSetup(
                        state.Generation,
                        state.Revision,
                        () => _controller.ResetSession())
                    : null;
                LastStatusMessage = result.HasValue && result.Value.Success
                    ? "已重置会话并返回实验设置。"
                    : "重置恢复失败。" + (result?.Message ?? string.Empty);
                return;
            }

            if (TryParseSetupSlotCommand(commandId, out int teamId, out int slotIndex))
            {
                LastStatusMessage = Execute(
                    () => _controller.SelectSetupSpawnSlot(teamId, slotIndex),
                    "已选择测试槽位。",
                    "选择测试槽位失败。" );
                _refreshPanel?.Invoke();
                return;
            }
            if (TryParseSetupCharacterCommand(commandId, out int characterId))
            {
                LastStatusMessage = Execute(
                    () => _controller.SelectSetupCharacterId(characterId),
                    "已选择角色。",
                    "选择角色失败。");
                _refreshPanel?.Invoke();
                return;
            }
            if (TryParseSetupBehaviorGroupCommand(
                    commandId,
                    out int behaviorGroupDraftId,
                    out string groupDraftId))
            {
                LastStatusMessage = Execute(
                    () => !string.IsNullOrWhiteSpace(groupDraftId)
                        ? _controller.SelectSetupBehaviorGroupWorkspaceDraft(
                            groupDraftId,
                            behaviorGroupDraftId)
                        : _controller.SelectSetupBehaviorGroupDraft(behaviorGroupDraftId),
                    "已选择行为组。",
                    "选择行为组失败。");
                _refreshPanel?.Invoke();
                return;
            }

            if (TryParseWorkbenchEntryCommand(commandId, out int entryIndex))
            {
                _followCurrentRuntimeBehavior = false;
                SelectedWorkbenchEntryIndex = entryIndex;
                SelectedCompiledStepIndex = 0;
                SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                SelectedLogicElementIndex = 0;
                if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup)
                {
                    // Group rows select an installed entry; card authoring has its own library entry point.
                    LastStatusMessage = "已选择装配条目。";
                    _refreshPanel?.Invoke();
                }
                else
                {
                    OpenSelectedBehaviorData();
                    if (WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                        _refreshPanel?.Invoke();
                }
                return;
            }
            if (TryParseWorkbenchScrollCommand(commandId, out int workbenchOffset))
            {
                WorkbenchScrollOffset = workbenchOffset;
                _refreshPanel?.Invoke();
                return;
            }
            if (TryParseCompiledStepCommand(commandId, out int compiledStepIndex))
            {
                SelectedCompiledStepIndex = compiledStepIndex;
                SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                SelectedLogicElementIndex = compiledStepIndex;
                LastStatusMessage = "已选择编译步骤。";
                _refreshPanel?.Invoke();
                return;
            }


            bool handled =
                TryHandleSessionCommand(command)
                || TryHandleTimelineCommand(command)
                || TryHandleCharacterActionAuthoringCommand(command)
                || TryHandleRuntimeTestAndSetupCommand(command)
                || TryHandleNavigationReplayAndFactsCommand(command)
                || TryHandleTestCurrentChangesCommand(command)
                || TryHandlePublishCurrentChangesCommand(command)
                || TryHandleWorkbenchCommand(command);
            if (!handled)
            {
                LastStatusMessage = "不支持的运行时智能规划开发命令：" + commandId;
            }

            if (handled)
            {
                SynchronizeHistoricalPresentationState();
                // A successful group navigation has already projected and bound the target page.
                // Same-page commands and failed mounts still need their normal feedback refresh.
                bool groupMounted = commandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup
                    && WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                    && !ReferenceEquals(shellBeforeCommand, _controller?.ShellCoordinator?.CurrentState);
                if (!groupMounted && !CharacterActionWorkspaceUsesLocalRefresh(commandId))
                    _refreshPanel?.Invoke();
                if (_openSelectedBehaviorStageAfterRefresh)
                {
                    _openSelectedBehaviorStageAfterRefresh = false;
                    _behaviorCardWorkspaceCommands?.OpenStageEditor(
                        Math.Max(0, SelectedLogicElementIndex));
                }
            }
        }

        private static bool CharacterActionWorkspaceUsesLocalRefresh(string commandId)
        {
            return commandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .OpenCharacterActionAuthoring
                || commandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .CharacterActionWorkspaceCommand
                || commandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .CharacterActionWorkspaceClose
                || commandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .CharacterActionWorkspacePreview;
        }
    }
}
