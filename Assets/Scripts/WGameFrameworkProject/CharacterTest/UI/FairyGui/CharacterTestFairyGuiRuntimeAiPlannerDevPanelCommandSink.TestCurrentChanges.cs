using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool TryHandleTestCurrentChangesCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            bool compatibilityAlias = commandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft
                && TimelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic
                && IsBehaviorTestAuthoringContext();
            if (commandId
                    != CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges
                && !compatibilityAlias)
            {
                return false;
            }

            if (_controller == null)
            {
                LastStatusMessage = "Runtime AI Lab 控制器不可用。";
                UnityEngine.Debug.LogError("[Runtime AI Lab][TestStartFailed] entry=test-current-changes reason=ControllerUnavailable");
                _panelStatusSink?.Invoke(LastStatusMessage);
                return true;
            }

            _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
            PublishBehaviorCardContextIfNeeded();
            if (_controller.TacticalCardTestMode && _controller.TacticalCardTestInFlight)
            {
                _controller.ToggleTacticalCardTestPlayback(out string playbackIssue);
                LastStatusMessage = playbackIssue; _panelStatusSink?.Invoke(playbackIssue);
                return true;
            }
            bool queued = _controller.QueueTestCurrentChanges(
                SelectedWorkbenchEntryIndex,
                out string message,
                playAfterStart: command.Payload is bool play && play);
            LastStatusMessage = message;
            _behaviorRecipeSaveStatusPending = queued;
            _panelStatusSink?.Invoke(LastStatusMessage);
            return true;
        }
    }
}
