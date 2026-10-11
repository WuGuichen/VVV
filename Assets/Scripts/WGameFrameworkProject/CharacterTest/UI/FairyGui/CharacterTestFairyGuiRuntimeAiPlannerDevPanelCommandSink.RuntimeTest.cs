namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool IsRuntimeTestWorkspace =>
            WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
            && DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;

        private string ReturnRuntimeTestToSetup()
        {
            if (_controller == null)
                return PublishRuntimeTestStatus("返回实验设置失败：控制器不可用。");

            int selectedTeam = _controller.SelectedSetupTeamId, selectedSlot = _controller.SelectedSetupSlotIndex;
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                _controller.ShellCoordinator;
            if (coordinator != null)
            {
                CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult navigation = coordinator.Navigate(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    CharacterTestRuntimeAiLabWorkspace.Setup);
                if (!navigation.Success)
                    return PublishRuntimeTestStatus("返回实验设置失败：" + navigation.Message);
                SyncFromShellState();
            }

            if (!_controller.ResetSession())
            {
                return PublishRuntimeTestStatus(
                    "返回实验设置失败："
                    + (_controller.StatusMessage ?? "无法停止当前测试。"));
            }

            _controller.EndStandaloneTacticalEditing();
            _controller.SelectSetupSpawnSlot(selectedTeam, selectedSlot);
            SyncFromShellState();
            const string successMessage = "已返回实验设置；当前阵容配置已保留。";
            PublishRuntimeTestStatus(successMessage);
            _refreshPanel?.Invoke();
            return successMessage;
        }

        private string RestartRuntimeTestSession(
            bool refreshPanel = true,
            bool returnToBehaviorCard = false)
        {
            if (_controller == null)
                return PublishRuntimeTestStatus("重新测试失败：控制器不可用。");

            if (_controller.TacticalCardTestMode)
            {
                if (!_controller.SaveConfiguredRecovery(out string recoveryIssue))
                    return PublishRuntimeTestStatus("重置失败：" + recoveryIssue);
                if (!_controller.ResetSession())
                    return PublishRuntimeTestStatus(_controller.StatusMessage);
                // ResetSession keeps the configured card workspace and restores manual-play state.
                // Reopening its internal draft id would leave that workspace and resolve a base card instead.
                _openSelectedBehaviorStageAfterRefresh = false;
                SyncFromShellState();
                if (refreshPanel) _refreshPanel?.Invoke();
                return PublishRuntimeTestStatus("战场已重置并暂停；确认所选战术卡后出牌。");
            }

            CharacterTestRuntimeAiLabTimelineMode timelineMode = TimelineMode;

            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                _controller.ShellCoordinator;
            if (coordinator != null)
            {
                CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult navigation = coordinator.Navigate(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    CharacterTestRuntimeAiLabWorkspace.Setup);
                if (!navigation.Success)
                {
                    return PublishRuntimeTestStatus(
                        "重新测试失败：" + navigation.Message);
                }
                SyncFromShellState();
            }

            if (!_controller.ResetSession())
            {
                return PublishRuntimeTestStatus(
                    "重新测试失败："
                    + (_controller.StatusMessage ?? "无法重置当前测试。"));
            }

            if (!_controller.StartSessionThroughShell(timelineMode))
            {
                SyncFromShellState();
                return PublishRuntimeTestStatus(
                    "重新测试失败："
                    + (_controller.StatusMessage ?? "无法重新启动测试。"));
            }

            SyncFromShellState();
            if (returnToBehaviorCard && coordinator != null)
            {
                CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
                CharacterTestRuntimeAiLabNavigationResult navigation = coordinator.Navigate(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    CharacterTestRuntimeAiLabWorkspace.BehaviorCard);
                if (!navigation.Success)
                {
                    return PublishRuntimeTestStatus(
                        "测试已重新开始，但返回行为编辑页失败：" + navigation.Message);
                }
                SyncFromShellState();
            }
            string successMessage = returnToBehaviorCard
                ? "测试已从第 0 帧重新播放，并保留当前行为编辑页。"
                : "测试已重新开始，并停在首个可见帧。";
            PublishRuntimeTestStatus(successMessage);
            if (refreshPanel)
                _refreshPanel?.Invoke();
            return successMessage;
        }

        private string SelectRuntimeTestAgent(object payload)
        {
            if (!(payload is int agentIndex) || agentIndex < 0)
                return "选择智能体失败：选择项无效。";
            return Execute(
                () => _controller.SelectAgent(agentIndex),
                "已切换测试智能体。",
                "选择智能体失败。");
        }

        private string PublishRuntimeTestStatus(string message)
        {
            _panelStatusSink?.Invoke(message);
            return message;
        }
    }
}
