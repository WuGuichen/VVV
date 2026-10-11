using System;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPublishCurrentChangesPopupState
    {
        Hidden = 0,
        Preview = 1,
        Failure = 2,
        SuccessChoice = 3,
        PostTestFailure = 4
    }

    internal readonly struct CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction
    {
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction(
            string label,
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload payload,
            bool enabled = true)
        {
            Label = label ?? string.Empty;
            Payload = payload;
            Enabled = enabled && payload != null;
        }

        internal string Label { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload Payload { get; }
        internal bool Enabled { get; }
        internal bool Visible => Payload != null && Label.Length > 0;
    }

    /// <summary>Read-only projection for the Composer-owned aggregate publish popup.</summary>
    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel
    {
        private CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupState state,
            string previewToken,
            string title,
            string message,
            string summary,
            string details,
            bool detailsExpanded,
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction primary,
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction secondary,
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction detailsAction,
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload closePayload)
        {
            State = state;
            PreviewToken = previewToken ?? string.Empty;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Summary = summary ?? string.Empty;
            Details = details ?? string.Empty;
            DetailsExpanded = detailsExpanded;
            Primary = primary;
            Secondary = secondary;
            DetailsAction = detailsAction;
            ClosePayload = closePayload;
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupState State { get; }
        internal bool Visible => State != CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.Hidden;
        internal string PreviewToken { get; }
        internal string Title { get; }
        internal string Message { get; }
        internal string Summary { get; }
        internal string Details { get; }
        internal bool DetailsExpanded { get; }
        internal bool HasDetails => Details.Length > 0;
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction Primary { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction Secondary { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction DetailsAction { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload ClosePayload { get; }

        internal static CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel Hidden()
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.Hidden,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                detailsExpanded: false,
                default,
                default,
                default,
                closePayload: null);
        }

        internal static CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel FromResult(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            bool detailsExpanded = false)
        {
            if (result?.PreviewReady == true && result.Preview != null)
                return Preview(result, detailsExpanded);
            if (result?.Success == true && result.Preview != null)
                return Success(result, detailsExpanded);
            return Failure(result, detailsExpanded);
        }

        internal static CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel
            FromPostTestFailure(
                CharacterTestRuntimeAiLabPublishCurrentChangesResult publish,
                CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult postTest,
                bool detailsExpanded = false)
        {
            string token = publish?.Preview?.Token ?? string.Empty;
            string details = BuildDetails(publish, postTest);
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.PostTestFailure,
                token,
                "新版本已发布，但测试未重启",
                "游戏可用版本已更新。\n当前测试没有改变，可以重试。",
                string.Empty,
                details,
                detailsExpanded,
                Action(
                    "重试重新测试",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .RestartPublishedVersion,
                    token),
                Action(
                    "继续当前测试",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .ContinueCurrentTest,
                    token),
                BuildDetailsAction(token, detailsExpanded, details.Length > 0),
                Payload(
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .ContinueCurrentTest,
                    token));
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel WithDetails(
            bool expanded)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
                State,
                PreviewToken,
                Title,
                Message,
                Summary,
                Details,
                expanded && HasDetails,
                Primary,
                Secondary,
                BuildDetailsAction(PreviewToken, expanded, HasDetails),
                ClosePayload);
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel Preview(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            bool detailsExpanded)
        {
            string token = result.Preview.Token;
            string details = BuildDetails(result, default);
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.Preview,
                token,
                "发布本次修改？",
                "请确认本次发布影响。当前测试不会自动中断。",
                result.Preview.BuildSummary(),
                details,
                detailsExpanded,
                Action(
                    "确认发布",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Confirm,
                    token),
                Action(
                    "取消",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Cancel,
                    token),
                BuildDetailsAction(token, detailsExpanded, details.Length > 0),
                Payload(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Cancel, token));
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel Success(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            bool detailsExpanded)
        {
            string token = result.Preview.Token;
            string details = BuildDetails(result, default);
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.SuccessChoice,
                token,
                "发布完成",
                "游戏可用版本已更新。请选择如何处理当前测试。",
                string.Empty,
                details,
                detailsExpanded,
                Action(
                    "用新版本重新开始测试",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .RestartPublishedVersion,
                    token),
                Action(
                    "继续当前测试",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .ContinueCurrentTest,
                    token),
                BuildDetailsAction(token, detailsExpanded, details.Length > 0),
                Payload(
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                        .ContinueCurrentTest,
                    token));
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel Failure(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            bool detailsExpanded)
        {
            string token = result?.Preview?.Token ?? string.Empty;
            string title = "发布失败";
            string message = "发布失败，草稿仍然安全保留。\n游戏可用版本没有改变。";
            bool canRetry = result != null && !result.RequiresRecovery;
            if (result?.RequiresRecovery == true || result?.CatalogChanged == true)
            {
                bool confirmed = result.Group != null && result.State?.Success == true;
                title = confirmed ? "已提交，等待恢复" : "提交状态待核实";
                message = confirmed
                    ? "已确认行为组 r" + result.Group.Revision + " 提交。草稿检查点或实验配置尚未同步，请恢复。"
                    : "目录提交或回滚尚未完整核实。请核对目录并恢复；恢复前不能再次发布。";
                canRetry = false;
            }
            else if (result?.Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.NoChanges)
            {
                title = "没有需要发布的修改";
                message = "当前行为组及其直接引用行为卡已经与游戏可用版本一致。";
                canRetry = false;
            }
            else if (result?.Status
                == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.MainWorkspaceRequired)
            {
                title = "只能从主工作区发布";
                message = "请先把实验工作区的修改应用到主工作区。";
                canRetry = false;
            }
            else if (result?.Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReadOnly)
            {
                title = "当前环境不能发布";
                message = "项目正式版本没有改变。请在可写的 Unity Editor 中重试。";
                canRetry = false;
            }

            string details = BuildDetails(result, default);
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction primary = result?.RequiresRecovery == true && token.Length > 0
                ? Action("核对目录并恢复", CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Recover, token)
                : canRetry
                ? Action(
                    "重试发布",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Retry,
                    token)
                : Action(
                    "关闭",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Dismiss,
                    token);
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction secondary = canRetry
                ? Action(
                    "关闭",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Dismiss,
                    token)
                : default;
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel(
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.Failure,
                token,
                title,
                message,
                string.Empty,
                details,
                detailsExpanded,
                primary,
                secondary,
                BuildDetailsAction(token, detailsExpanded, details.Length > 0),
                Payload(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Dismiss, token));
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction BuildDetailsAction(
            string token,
            bool expanded,
            bool visible)
        {
            return visible
                ? Action(
                    expanded ? "收起详情" : "查看详情",
                    CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.ToggleDetails,
                    token)
                : default;
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction Action(
            string label,
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent intent,
            string token)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction(
                label,
                Payload(intent, token));
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload Payload(
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent intent,
            string token)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload(intent, token);
        }

        private static string BuildDetails(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult postTest)
        {
            var builder = new StringBuilder(384);
            if (result != null)
            {
                builder.Append("Stage: ").Append(result.Stage)
                    .Append("\nStatus: ").Append(result.Status)
                    .Append("\nCatalogChanged: ").Append(result.CatalogChanged)
                    .Append("\nRequiresRecovery: ").Append(result.RequiresRecovery);
                if (result.Preview != null)
                {
                    builder.Append("\nWorkspace: ").Append(result.Preview.WorkspaceId)
                        .Append(" r").Append(result.Preview.WorkspaceRevision)
                        .Append("\nGroup: ").Append(result.Preview.StableGroupId)
                        .Append(" r").Append(result.Preview.SourceGroupRevision)
                        .Append(" -> r").Append(result.Preview.TargetGroupRevision)
                        .Append("\nToken: ").Append(result.Preview.Token);
                }
                for (int i = 0; i < result.Diagnostics.Count; i++)
                {
                    CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic diagnostic =
                        result.Diagnostics[i];
                    builder.Append("\n[").Append(diagnostic.Stage).Append("] ")
                        .Append(diagnostic.Code).Append(": ").Append(diagnostic.Message);
                }
            }
            if (!string.IsNullOrEmpty(postTest.Code)
                || !string.IsNullOrEmpty(postTest.Message))
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append("PostTest: ").Append(postTest.Choice)
                    .Append("\n").Append(postTest.Code)
                    .Append(": ").Append(postTest.Message);
            }
            return builder.ToString();
        }
    }

    /// <summary>Owns popup projection state; it does not own FairyGUI objects or Catalog writes.</summary>
    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator
    {
        private CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel _current =
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.Hidden();

        internal event Action<CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel> Changed;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel Current => _current;

        internal void Present(CharacterTestRuntimeAiLabPublishCurrentChangesResult result)
        {
            Set(CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.FromResult(result));
        }

        internal void PresentPostTestFailure(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult publish,
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult postTest)
        {
            Set(CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.FromPostTestFailure(
                publish,
                postTest));
        }

        internal bool ToggleDetails(string previewToken)
        {
            string normalized = previewToken?.Trim() ?? string.Empty;
            if (!_current.Visible
                || !_current.HasDetails
                || !string.Equals(
                    normalized,
                    _current.PreviewToken,
                    StringComparison.Ordinal))
            {
                return false;
            }
            Set(_current.WithDetails(!_current.DetailsExpanded));
            return true;
        }

        internal void Dismiss()
        {
            Set(CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.Hidden());
        }

        private void Set(CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel value)
        {
            _current = value ?? CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.Hidden();
            Changed?.Invoke(_current);
        }
    }
}
