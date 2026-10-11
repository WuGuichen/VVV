using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private CharacterTestRuntimeAiLabStableReturnContext _characterActionStableReturnContext;

        internal CharacterTestRuntimeAiLabNavigationFeedback LastNavigationFeedback =>
            _controller?.NavigationFeedback
            ?? CharacterTestRuntimeAiLabNavigationFeedback.None;

        internal bool ExecuteNavigationFeedbackAction(
            CharacterTestRuntimeAiLabNavigationFeedbackAction action)
        {
            if (_controller == null)
                return false;

            CharacterTestRuntimeAiLabNavigationExitIntent pendingIntent =
                _controller.PendingNavigationExitIntent;
            bool proceed;
            CharacterTestRuntimeAiLabNavigationExitResult result;
            switch (action)
            {
                case CharacterTestRuntimeAiLabNavigationFeedbackAction.RetrySave:
                    proceed = _controller.RetryPendingAuthoringNavigationSave(out result);
                    LastStatusMessage = result.Feedback.BuildPrimaryText();
                    break;
                case CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardPendingChanges:
                    proceed = _controller.DiscardPendingAuthoringNavigationChanges(out result);
                    LastStatusMessage = result.Feedback.BuildPrimaryText();
                    break;
                case CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel:
                    _controller.CancelPendingAuthoringNavigation();
                    _controller.ClearAuthoringNavigationFeedback();
                    LastStatusMessage = "已取消返回，继续留在当前页面。";
                    _panelStatusSink?.Invoke(LastStatusMessage);
                    _refreshPanel?.Invoke();
                    return true;
                default:
                    return false;
            }

            _panelStatusSink?.Invoke(LastStatusMessage);
            if (!proceed)
            {
                _refreshPanel?.Invoke();
                return false;
            }

            string commandId = pendingIntent ==
                    CharacterTestRuntimeAiLabNavigationExitIntent.WorkspaceBack
                ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack
                : CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close;
            Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                null));
            return true;
        }

        private bool TryPrepareWorkspaceBackNavigation()
        {
            if (_controller?.IsConfiguredTestCardEditing == true)
            {
                bool saved = _controller.SaveConfiguredRecovery(out string issue);
                if (!saved) { LastStatusMessage = issue; _panelStatusSink?.Invoke(issue); }
                return saved;
            }
            return TryPrepareNavigationExit(
                CharacterTestRuntimeAiLabNavigationExitIntent.WorkspaceBack,
                characterActionOpen: false,
                characterActionDirty: false);
        }

        private bool TryPrepareTerminalCloseNavigation()
        {
            return TryPrepareNavigationExit(
                CharacterTestRuntimeAiLabNavigationExitIntent.TerminalClose,
                characterActionOpen: false,
                characterActionDirty: false);
        }

        internal CharacterTestRuntimeAiLabNavigationExitDisposition
            PrepareCharacterActionWorkspaceReturn(bool discardCharacterActionChanges = false)
        {
            bool proceed = TryPrepareNavigationExit(
                CharacterTestRuntimeAiLabNavigationExitIntent.CharacterActionReturn,
                characterActionOpen: CharacterActionWorkspace?.Available == true,
                characterActionDirty: !discardCharacterActionChanges
                    && CharacterActionWorkspace?.Dirty == true);
            return proceed
                ? CharacterTestRuntimeAiLabNavigationExitDisposition.Proceed
                : _controller?.LastNavigationExitResult.Disposition
                    ?? CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed;
        }

        private bool TryPrepareNavigationExit(
            CharacterTestRuntimeAiLabNavigationExitIntent intent,
            bool characterActionOpen,
            bool characterActionDirty)
        {
            if (_controller == null)
            {
                LastStatusMessage = "返回失败：Runtime AI Lab 控制器不可用。";
                return false;
            }
            if (_controller.TryPrepareAuthoringNavigationExit(
                    intent,
                    characterActionOpen,
                    characterActionDirty,
                    out CharacterTestRuntimeAiLabNavigationExitResult result))
            {
                return true;
            }

            LastStatusMessage = result.Feedback.BuildPrimaryText();
            return false;
        }

#if UNITY_EDITOR
        private void CaptureCharacterActionStableReturnContext()
        {
            CharacterTestRuntimeAiLabPageContext page =
                _controller?.ShellCoordinator?.CurrentState?.PageContext;
            CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection selection =
                _behaviorCardWorkspaceCommands?.CaptureAuthoringWorkspaceRecipeSelection()
                ?? default;
            _characterActionStableReturnContext =
                _controller?.CaptureStableAuthoringReturnContext(
                    selection.TimelineId,
                    selection.EventId,
                    (int)selection.TargetKind,
                    page)
                ?? default;
        }

        private void RestoreCharacterActionStableReturnContext()
        {
            if (_controller == null || !_characterActionStableReturnContext.IsValid)
                return;

            bool baseRestored = _controller.TryRestoreStableAuthoringReturnContext(
                _characterActionStableReturnContext,
                out bool exactCardContext,
                out CharacterTestRuntimeAiLabNavigationFeedback feedback);
            bool exactRecipeSelection = false;
            if (baseRestored && exactCardContext && _behaviorCardWorkspaceCommands != null)
            {
                var selection = new CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection(
                    (BehaviorRecipeInspectorTargetKind)
                        _characterActionStableReturnContext.InspectorTargetKind,
                    _characterActionStableReturnContext.TimelineId,
                    _characterActionStableReturnContext.EventId);
                exactRecipeSelection =
                    _behaviorCardWorkspaceCommands.TryRestoreAuthoringWorkspaceRecipeSelection(
                        selection);
                if (!exactRecipeSelection)
                {
                    var timelineFallback =
                        new CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection(
                            BehaviorRecipeInspectorTargetKind.Timeline,
                            _characterActionStableReturnContext.TimelineId,
                            string.Empty);
                    if (!_behaviorCardWorkspaceCommands
                            .TryRestoreAuthoringWorkspaceRecipeSelection(timelineFallback))
                    {
                        _behaviorCardWorkspaceCommands.TryRestoreAuthoringWorkspaceRecipeSelection(
                            new CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection(
                                BehaviorRecipeInspectorTargetKind.Recipe,
                                string.Empty,
                                string.Empty));
                    }
                }
            }

            SyncFromShellState();
            if (!baseRestored)
            {
                LastStatusMessage = feedback.BuildPrimaryText();
            }
            else if (!exactRecipeSelection
                && _characterActionStableReturnContext.EventId.Length > 0)
            {
                LastStatusMessage = "已返回行为卡逻辑时间轴；原事件已不存在。";
            }
            else
            {
                LastStatusMessage = "已返回角色动作事件。";
            }
            _characterActionStableReturnContext = default;
        }
#else
        private void CaptureCharacterActionStableReturnContext()
        {
        }

        private void RestoreCharacterActionStableReturnContext()
        {
        }
#endif
    }
}
