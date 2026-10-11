using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool TryHandleSessionCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start:
                    if (IsBehaviorTestAuthoringContext())
                    {
                        PublishBehaviorCardContextIfNeeded();
                        if (_controller == null)
                        {
                            LastStatusMessage = "启动失败：控制器不可用。";
                            UnityEngine.Debug.LogError("[Runtime AI Lab][TestStartFailed] entry=authoring reason=ControllerUnavailable");
                        }
                        else
                        {
                            _controller.QueueTestCurrentChanges(SelectedWorkbenchEntryIndex, out string message);
                            LastStatusMessage = PublishRuntimeTestStatus(message);
                        }
                        _refreshPanel?.Invoke();
                        break;
                    }
                    int requestedEntryIndex = SelectedWorkbenchEntryIndex;
                    PublishBehaviorCardContextIfNeeded();
                    bool started = _controller != null
                        && _controller.StartSessionThroughShell();
                    if (!started)
                    {
                        string failure = _controller?.StatusMessage ?? "控制器不可用。";
                        if (_controller == null) UnityEngine.Debug.LogError("[Runtime AI Lab][TestStartFailed] entry=setup reason=ControllerUnavailable");
                        // Recovery owns the original page. Do not navigate to a guessed repair card.
                        SyncFromShellState();
                        SelectedWorkbenchEntryIndex = requestedEntryIndex;
                        LastStatusMessage = PublishRuntimeTestStatus("启动失败：" + failure);
                        _refreshPanel?.Invoke();
                        break;
                    }
                    LastStatusMessage = PublishRuntimeTestStatus("测试已开始。");
                    SyncFromShellState();
                    // BeginSessionStart owns success navigation. Do not run a second navigation
                    // against the newly-created sink's default card index after the session rebuild.
                    _refreshPanel?.Invoke();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume:
                    LastStatusMessage = Execute(_controller.TogglePause, "暂停状态已切换。", "切换暂停状态失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step:
                    LastStatusMessage = Execute(
                        _controller.StepFrame,
                        "模拟已推进一个运行时帧。",
                        "推进失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecompute:
                    LastStatusMessage = Execute(
                        _controller.ToggleBranchRecompute,
                        "重算模式已切换。",
                        "无法切换重算模式。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecomputeCancel:
                    LastStatusMessage = Execute(
                        _controller.CancelBranchRecomputeConfirmation,
                        "已取消建立新未来。",
                        "当前没有待确认的分支请求。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack:
                    LastStatusMessage = Execute(_controller.StepBackFrame, "已后退一帧。", "后退失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack10:
                    LastStatusMessage = Execute(_controller.StepBack10, "已后退 10 帧。", "后退失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepForward10:
                    LastStatusMessage = Execute(_controller.StepForward10, "已前进 10 帧。", "前进失败。" );
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset:
                    if (IsRuntimeTestWorkspace)
                    {
                        LastStatusMessage = RestartRuntimeTestSession();
                        break;
                    }
                    if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                    {
                        LastStatusMessage = RestartRuntimeTestSession(
                            returnToBehaviorCard: true);
                        break;
                    }
                    bool resetSucceeded = _controller != null && _controller.ResetSession();
                    LastStatusMessage = resetSucceeded
                        ? "测试已重置。"
                        : "重置失败。 " + (_controller?.StatusMessage ?? string.Empty);
                    if (resetSucceeded)
                    {
                        SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.Setup);
                        SelectedWorkbenchEntryIndex = 0;
                        SelectedCompiledStepIndex = 0;
                        SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.None;
                        SelectedLogicElementIndex = -1;
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
