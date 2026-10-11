using System;
using MxFramework.CharacterAction;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload
            _characterActionAuthoringReturnPayload;

        private void RestoreOpenCharacterActionWorkspaceAfterHostRebind()
        {
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot snapshot =
                CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Capture,
                    });
            if (snapshot?.Available != true)
                return;

            CharacterActionWorkspace = snapshot;
            TimelineMode = snapshot.ReturnTimelineMode;
            _characterActionAuthoringReturnPayload =
                new CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload
                {
                    ActionId = new CharacterActionId(snapshot.SelectedActionId),
                    StableCardId = snapshot.ReturnStableCardId,
                    TimelineId = snapshot.ReturnTimelineId,
                    EventId = snapshot.ReturnEventId,
                };
            CharacterTestRuntimeAiLabPageContext page =
                _controller?.ShellCoordinator?.CurrentState?.PageContext;
            _characterActionStableReturnContext =
                _controller?.CaptureStableAuthoringReturnContext(
                    snapshot.ReturnTimelineId,
                    snapshot.ReturnEventId,
                    (int)BehaviorRecipeInspectorTargetKind.Event,
                    page)
                ?? default;
        }

        private bool TryHandleCharacterActionAuthoringCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            bool open = commandId
                == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenCharacterActionAuthoring;
            bool retry = commandId
                == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RetryCharacterActionApply;
            bool workspaceCommand = commandId
                == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspaceCommand;
            bool workspaceClose = commandId
                == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspaceClose;
            bool workspacePreview = commandId
                == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspacePreview;
            if (workspaceCommand)
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand workspacePayload =
                    command.Payload as CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand;
                CharacterActionWorkspace = CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Execute,
                        Command = workspacePayload,
                    });
                LastStatusMessage = CharacterActionWorkspace.Status;
                return true;
            }
            if (workspaceClose)
            {
                bool discardCharacterActionChanges = command.Payload is bool discard && discard;
                if (!TryPrepareNavigationExit(
                        CharacterTestRuntimeAiLabNavigationExitIntent.CharacterActionReturn,
                        characterActionOpen: CharacterActionWorkspace?.Available == true,
                        characterActionDirty: !discardCharacterActionChanges
                            && CharacterActionWorkspace?.Dirty == true))
                {
                    return true;
                }
                bool requiresRuntimeRestart =
                    CharacterActionWorkspaceCloseRequiresRuntimeRestart(CharacterActionWorkspace);
                if (requiresRuntimeRestart)
                    _controller?.TryStopCharacterActionWorkspacePreview(out _);
                CharacterActionWorkspace = CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Close,
                    });
                if (requiresRuntimeRestart)
                {
                    RestartRuntimeTestSession(refreshPanel: false);
                    LastStatusMessage = "已退出动作编排并从第 0 帧重新开始测试。";
                    _remountPanel?.Invoke();
                }
                else
                {
                    LastStatusMessage = "已返回角色动作事件。";
                }
                RestoreCharacterActionStableReturnContext();
                return true;
            }
            if (workspacePreview)
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest preview = command.Payload
                    as CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest;
                bool wasPreviewActive = CharacterActionWorkspace?.PreviewActive == true;
                bool requiresBaselineReset = !wasPreviewActive
                    || preview?.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewScrub;
                if (requiresBaselineReset)
                {
                    if (wasPreviewActive)
                        _controller?.TryStopCharacterActionWorkspacePreview(out _);
                    RestartRuntimeTestSession(refreshPanel: false);
                }
                CharacterActionWorkspace = CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(preview);
                if (preview?.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStop)
                {
                    _controller?.TryStopCharacterActionWorkspacePreview(out _);
                    RestartRuntimeTestSession(refreshPanel: false);
                }
                else if (CharacterActionWorkspace.Available)
                {
                    bool play = preview?.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPlay;
                    bool handledExisting = false;
                    string previewMessage = string.Empty;
                    if (wasPreviewActive
                        && preview?.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStep)
                    {
                        handledExisting = _controller?.TryStepCharacterActionWorkspacePreview(out previewMessage) == true;
                    }
                    else if (wasPreviewActive
                        && preview?.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPause)
                    {
                        handledExisting = _controller?.TryPauseCharacterActionWorkspacePreview(true, out previewMessage) == true;
                    }
                    else if (wasPreviewActive
                        && preview?.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPlay)
                    {
                        handledExisting = _controller?.TryPauseCharacterActionWorkspacePreview(false, out previewMessage) == true;
                    }
                    if (!handledExisting && (_controller == null
                        || !_controller.TryStartCharacterActionWorkspacePreview(
                            new CharacterActionId(CharacterActionWorkspace.SelectedActionId),
                            CharacterActionWorkspace.PreviewSourceDirectory,
                            play,
                            CharacterActionWorkspace.PreviewFrame,
                            out previewMessage)))
                    {
                        CharacterActionWorkspace.Status = previewMessage ?? "当前角色草稿预览失败。";
                    }
                    else if (!string.IsNullOrWhiteSpace(previewMessage))
                    {
                        CharacterActionWorkspace.Status = previewMessage;
                    }
                }
                LastStatusMessage = CharacterActionWorkspace.Status;
                if (requiresBaselineReset
                    || preview?.Kind
                        == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStop)
                {
                    // ResetSession temporarily unmounts the Runtime AI Lab view. Reopen it only
                    // after the bridge contains the final preview snapshot; the replacement
                    // command sink then captures the same CharacterAction session and restores
                    // the embedded workspace instead of returning the user to the base page.
                    _remountPanel?.Invoke();
                }
                return true;
            }
            if (!open && !retry)
                return false;

            var payload = command.Payload
                as CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload;
            if (payload == null || !payload.ActionId.IsValid)
            {
                LastStatusMessage = "角色动作编辑请求无效。";
                ShowToast(LastStatusMessage, true);
                return true;
            }
            string diagnostic = string.Empty;
            if (_controller == null
                || !_controller.TryGetSelectedCharacterActionAuthoringContext(
                    out CharacterTestRuntimeAiLabCharacterActionAuthoringContext context,
                    out diagnostic))
            {
                LastStatusMessage = diagnostic ?? "当前角色动作上下文不可用。";
                ShowToast(LastStatusMessage, true);
                return true;
            }

            var request = new CharacterTestRuntimeAiLabCharacterActionAuthoringRequest
            {
                Operation = retry
                    ? CharacterTestRuntimeAiLabCharacterActionAuthoringOperation.RetryApply
                    : CharacterTestRuntimeAiLabCharacterActionAuthoringOperation.OpenEmbedded,
                ActionId = payload.ActionId,
                Context = context,
                SelectedAgentIndex = _controller.SelectedAgentIndex,
                StableCardId = payload.StableCardId,
                TimelineId = payload.TimelineId,
                EventId = payload.EventId,
                TimelineMode = TimelineMode,
                StatusChanged = OnCharacterActionAuthoringStatusChanged,
            };
            _characterActionAuthoringReturnPayload = payload;
            if (open)
            {
                CaptureCharacterActionStableReturnContext();
                CharacterActionWorkspace = CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Execute(
                    new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                    {
                        Kind = CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Open,
                        OpenRequest = request,
                    });
                LastStatusMessage = CharacterActionWorkspace.Status;
                if (!CharacterActionWorkspace.Available)
                    ShowToast(LastStatusMessage, true);
                return true;
            }
            CharacterTestRuntimeAiLabCharacterActionAuthoringResult result =
                CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.Execute(request);
            LastStatusMessage = result.Message;
            if (result.Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed
                || result.Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Unavailable)
            {
                ShowToast(result.Message, true);
            }
            return true;
        }

        private void OnCharacterActionAuthoringStatusChanged(
            CharacterTestRuntimeAiLabCharacterActionAuthoringResult result)
        {
            CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.Publish(result);
            if (result.Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Succeeded)
            {
                int selectedAgentIndex = _controller?.SelectedAgentIndex ?? 0;
                CharacterTestRuntimeAiLabTimelineMode timelineMode = TimelineMode;
                CharacterTestRuntimeAiLabCharacterActionAuthoringResult restarting =
                    new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                        CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Restarting,
                        result.ActionId,
                        result.PackagePath,
                        "动作已导入并热重载；正在从第 0 帧重新开始测试。",
                        sourceSaved: true);
                CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.Publish(restarting);
                string restartMessage = RestartRuntimeTestSession(refreshPanel: false);
                if (_controller?.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
                {
                    TimelineMode = timelineMode;
                    _controller.SelectAgent(selectedAgentIndex);
                    _behaviorCardWorkspaceCommands?.TryRestoreRecipeEventSelection(
                        _characterActionAuthoringReturnPayload?.TimelineId,
                        _characterActionAuthoringReturnPayload?.EventId);
                    result = new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                        CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Succeeded,
                        result.ActionId,
                        result.PackagePath,
                        "动作已保存、导入并从第 0 帧重新开始测试。",
                        sourceSaved: true);
                }
                else
                {
                    result = new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                        CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed,
                        result.ActionId,
                        result.PackagePath,
                        "动作已保存、导入并热重载，但重新开始测试失败：" + restartMessage,
                        sourceSaved: true);
                }
                CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.Publish(result);
            }

            LastStatusMessage = result.Message;
            bool terminal = result.Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Succeeded
                || result.Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed;
            if (terminal)
            {
                ShowToast(
                    result.Message,
                    result.Status == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed);
            }
            _remountPanel?.Invoke();
        }

        internal static bool CharacterActionWorkspaceCloseRequiresRuntimeRestart(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot workspace)
        {
            return workspace?.PreviewActive == true;
        }
    }
}
