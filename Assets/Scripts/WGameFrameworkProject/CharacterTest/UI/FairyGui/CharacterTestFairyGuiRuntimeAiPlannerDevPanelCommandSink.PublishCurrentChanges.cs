using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
    {
        Preview = 0,
        Confirm = 1,
        Cancel = 2,
        Retry = 3,
        ToggleDetails = 4,
        Dismiss = 5,
        RestartPublishedVersion = 6,
        ContinueCurrentTest = 7,
        Recover = 8
    }

    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload
    {
        internal CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload(
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent intent,
            string previewToken = null)
        {
            Intent = intent;
            PreviewToken = previewToken ?? string.Empty;
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent Intent { get; }
        internal string PreviewToken { get; }

        internal bool IsValid
        {
            get
            {
                switch (Intent)
                {
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Preview:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Retry:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Dismiss:
                        return true;
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Confirm:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Recover:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Cancel:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.ToggleDetails:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .RestartPublishedVersion:
                    case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .ContinueCurrentTest:
                        return PreviewToken.Trim().Length > 0;
                    default:
                        return false;
                }
            }
        }
    }

    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private readonly CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator
            _publishCurrentChangesPopupCoordinator =
                new CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator();

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator
            PublishCurrentChangesPopupCoordinator =>
                _publishCurrentChangesPopupCoordinator;

        private bool TryHandlePublishCurrentChangesCommand(MxUiCommand command)
        {
            if (command.CommandId
                != CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishCurrentChanges)
            {
                return false;
            }

            CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload payload =
                command.Payload as CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload;
            if (command.Payload != null && payload == null)
            {
                LastStatusMessage = "发布操作请求无效；草稿和游戏可用版本都没有改变。";
                _panelStatusSink?.Invoke(LastStatusMessage);
                return true;
            }
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent intent =
                payload?.Intent
                    ?? CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Preview;
            if (payload != null && !payload.IsValid)
            {
                LastStatusMessage = "发布操作已失效，请重新打开发布摘要。";
                _panelStatusSink?.Invoke(LastStatusMessage);
                return true;
            }

            switch (intent)
            {
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Recover:
                    _controller.TryRecoverPublishCurrentBehaviorGroupChanges(payload.PreviewToken, out var recovered, out var recoveryMessage);
                    _publishCurrentChangesPopupCoordinator.Present(recovered);
                    LastStatusMessage = recoveryMessage;
                    _refreshPanel?.Invoke();
                    break;
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Confirm:
                    _controller.TryConfirmPublishCurrentBehaviorGroupChanges(
                        payload.PreviewToken,
                        out CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
                        out string confirmMessage);
                    _publishCurrentChangesPopupCoordinator.Present(result);
                    LastStatusMessage = result.Success
                        ? "发布完成，游戏可用版本已更新。请选择如何处理当前测试。"
                        : _publishCurrentChangesPopupCoordinator.Current.Message;
                    CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                        "action=publish-current-changes"
                        + " result=" + (result.Success ? "success" : "failed")
                        + " stage=" + result.Stage
                        + " status=" + result.Status);
                    break;
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Cancel:
                    _controller.CancelPublishCurrentBehaviorGroupChanges(
                        payload.PreviewToken,
                        out string cancelMessage);
                    _publishCurrentChangesPopupCoordinator.Dismiss();
                    LastStatusMessage = cancelMessage;
                    break;
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Retry:
                    PresentPublishCurrentChangesPreview();
                    break;
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.ToggleDetails:
                    _publishCurrentChangesPopupCoordinator.ToggleDetails(payload.PreviewToken);
                    return true;
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Dismiss:
                    if (payload.PreviewToken.Length > 0)
                    {
                        _controller.CancelPublishCurrentBehaviorGroupChanges(
                            payload.PreviewToken,
                            out _);
                    }
                    _publishCurrentChangesPopupCoordinator.Dismiss();
                    return true;
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                    .RestartPublishedVersion:
                case CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                    .ContinueCurrentTest:
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice choice =
                        intent == CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                            .RestartPublishedVersion
                            ? CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                                .RestartPublishedVersion
                            : CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                                .ContinueCurrentTest;
                    bool accepted = _controller.TryChoosePublishCurrentChangesPostTestAction(
                        payload.PreviewToken,
                        choice,
                        SelectedWorkbenchEntryIndex,
                        out CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult postTest,
                        out string postTestMessage);
                    if (accepted)
                    {
                        _publishCurrentChangesPopupCoordinator.Dismiss();
                        LastStatusMessage = postTestMessage;
                        _refreshPanel?.Invoke();
                    }
                    else
                    {
                        _publishCurrentChangesPopupCoordinator.PresentPostTestFailure(
                            _controller.LastPublishCurrentChangesResult,
                            postTest);
                        LastStatusMessage =
                            _publishCurrentChangesPopupCoordinator.Current.Message;
                    }
                    break;
                default:
                    PresentPublishCurrentChangesPreview();
                    break;
            }
            _panelStatusSink?.Invoke(LastStatusMessage);
            return true;
        }

        private void PresentPublishCurrentChangesPreview()
        {
            _controller.TryPreparePublishCurrentBehaviorGroupChanges(
                out _,
                out _);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                _controller.LastPublishCurrentChangesResult;
            _publishCurrentChangesPopupCoordinator.Present(result);
            LastStatusMessage = result?.PreviewReady == true
                ? "发布摘要已准备，请确认本次影响。"
                : _publishCurrentChangesPopupCoordinator.Current.Message;
        }
    }
}
