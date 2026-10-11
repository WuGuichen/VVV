using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static void FillReplayWorkspace(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel root,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            CharacterTestRuntimeAiLabReplayWorkspaceViewModel view = root.Replay
                ?? new CharacterTestRuntimeAiLabReplayWorkspaceViewModel();
            root.Replay = view;
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot snapshot =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            view.HasCapture = snapshot.HasCapture;
            view.State = snapshot.State;
            view.StateLabel = ReplayStateLabel(snapshot.State);
            view.ResultCode = snapshot.Code;
            view.Progress01 = snapshot.FrameCount > 0
                ? (float)snapshot.ComparedFrames / snapshot.FrameCount
                : 0f;
            view.CanFreeze = !snapshot.IsBusy && controller.CanFreezeReplayValidationCapture;
            view.CanValidate = snapshot.HasCapture && !snapshot.IsBusy;
            view.CanCancel = snapshot.IsBusy;
            view.CanRetry = snapshot.HasCapture && !snapshot.IsBusy
                && snapshot.State != CharacterTestReplayValidationState.Ready;
            view.CanReturn = !snapshot.IsBusy;
            view.CanLocateDivergence = snapshot.CanLocateDivergence;
            view.CanOpenFacts = snapshot.CanOpenFacts;
            view.CanOpenBehaviorCard = snapshot.CanOpenBehaviorCard;
            view.TimelineProjection = snapshot.TimelineProjection;
            view.RecordMetadataText = snapshot.HasCapture
                ? "已冻结第 " + snapshot.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "–" + snapshot.EndFrame.ToString(CultureInfo.InvariantCulture) + " 帧"
                : "尚未冻结回放记录";
            view.BootstrapIdentityText = snapshot.HasCapture
                ? "初始状态已冻结"
                : string.Empty;
            view.AuthorityIdentityText = snapshot.HasCapture
                ? "权威输入已记录"
                : string.Empty;
            view.ResultEvidenceText = snapshot.State == CharacterTestReplayValidationState.Passed
                ? "回放结果与记录一致"
                : snapshot.HasFailureFrame
                    ? "回放结果与记录不一致"
                    : string.Empty;
            view.SelectedVisualStableKey = snapshot.SelectedVisualStableKey;
            root.WorkspaceNavigationEnabled = !snapshot.IsBusy;
            root.TimelineProjection = snapshot.TimelineProjection;
            root.TimelineSelectedVisualStableKey = snapshot.SelectedVisualStableKey;
            if (snapshot.TimelineProjection != null)
            {
                root.TimelineRetainedStartFrame = snapshot.TimelineProjection.Domain.RetainedStartFrame;
                root.TimelineFutureAdvanceStartFrame = snapshot.TimelineProjection.Domain.LatestFrame;
                root.TimelineFutureAdvanceActive = false;
                root.TimelineHasLoopRange = false;
            }
            view.RecordText = snapshot.HasCapture
                ? "记录 " + snapshot.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "–" + snapshot.EndFrame.ToString(CultureInfo.InvariantCulture)
                    + " · " + snapshot.FrameCount.ToString(CultureInfo.InvariantCulture) + " 帧"
                : "尚未冻结回放记录";
            view.IdentityText = snapshot.HasCapture
                ? "录制命令 "
                    + snapshot.RecordedFrameCommandCount.ToString(CultureInfo.InvariantCulture)
                    + " · 权威命令 "
                    + snapshot.AuthoritativePacketCommandCount.ToString(CultureInfo.InvariantCulture)
                : controller.CanFreezeReplayValidationCapture
                    ? "推进测试后即可冻结当前记录。"
                    : "当前会话不支持回放校验。";
            view.StatusText = snapshot.IsBusy
                ? view.StateLabel + " · " + snapshot.ComparedFrames.ToString(CultureInfo.InvariantCulture)
                    + "/" + snapshot.FrameCount.ToString(CultureInfo.InvariantCulture)
                : view.StateLabel;
            root.StatusText = BuildReplayShellStatus(snapshot);
            view.DetailText = snapshot.HasFailureFrame
                ? "第 " + snapshot.FailureFrame.ToString(CultureInfo.InvariantCulture)
                    + " 帧结果不一致"
                    + "\n请定位到该帧查看感知、决策、动作和结果事件。"
                : snapshot.State == CharacterTestReplayValidationState.Passed
                    ? "已校验 " + snapshot.ComparedFrames.ToString(CultureInfo.InvariantCulture)
                        + " 帧，回放结果一致。"
                    : snapshot.TimelineProjection != null
                        ? "回放记录已准备，可开始一致性校验。"
                        : "当前记录尚未生成可查看的时间轴。";
        }

        private static string BuildReplayShellStatus(
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot snapshot)
        {
            if (snapshot.IsBusy)
            {
                return "正在校验回放 · "
                    + snapshot.ComparedFrames.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + snapshot.FrameCount.ToString(CultureInfo.InvariantCulture);
            }
            switch (snapshot.State)
            {
                case CharacterTestReplayValidationState.Ready:
                    return "回放记录已准备";
                case CharacterTestReplayValidationState.Passed:
                    return "回放结果一致";
                case CharacterTestReplayValidationState.Diverged:
                    return snapshot.HasFailureFrame
                        ? "第 " + snapshot.FailureFrame.ToString(CultureInfo.InvariantCulture)
                            + " 帧结果不一致"
                        : "回放结果不一致";
                case CharacterTestReplayValidationState.Failed:
                    return "回放校验未完成";
                case CharacterTestReplayValidationState.Cancelled:
                    return "回放校验已取消";
                default:
                    return snapshot.HasCapture ? "回放记录已冻结" : "暂无回放记录";
            }
        }

        private static string ReplayStateLabel(CharacterTestReplayValidationState state)
        {
            switch (state)
            {
                case CharacterTestReplayValidationState.Ready: return "已准备";
                case CharacterTestReplayValidationState.InspectingRecord: return "正在读取记录";
                case CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap:
                    return "正在恢复权威输入";
                case CharacterTestReplayValidationState.RestoringGameplayBootstrap:
                    return "正在恢复测试状态";
                case CharacterTestReplayValidationState.ComparingFrames: return "正在校验";
                case CharacterTestReplayValidationState.Passed: return "结果一致";
                case CharacterTestReplayValidationState.Diverged: return "结果不一致";
                case CharacterTestReplayValidationState.Failed: return "校验失败";
                case CharacterTestReplayValidationState.Cancelled: return "已取消";
                default: return "暂无记录";
            }
        }
    }
}
