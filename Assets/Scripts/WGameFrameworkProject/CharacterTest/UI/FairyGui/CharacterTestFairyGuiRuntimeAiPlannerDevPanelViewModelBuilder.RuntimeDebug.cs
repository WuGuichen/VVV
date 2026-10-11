using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static void FillTimelineRange(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            if (viewModel == null || timeline == null || timeline.Count <= 0)
                return;

            viewModel.TimelineStartFrame = timeline.Records[0].Frame;
            viewModel.TimelineEndFrame = timeline.LatestFrame;
            viewModel.TimelineSelectedFrame = selectedFrame != null
                ? selectedFrame.Frame
                : timeline.SelectedFrame;
            viewModel.TimelineRetainedStartFrame = timeline.RetainedStartFrame;
            viewModel.TimelineLoopStartFrame = timeline.LoopStartFrame;
            viewModel.TimelineLoopEndFrame = timeline.LoopEndFrame;
            viewModel.TimelineHasLoopRange = timeline.HasLoopRange;
        }

        private static void FillBehaviorCardRuntimeControl(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            in CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtime =
                viewModel.BehaviorCardRuntimeControl
                ?? new CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel();
            viewModel.BehaviorCardRuntimeControl = runtime;
            runtime.FrameLimit = Math.Max(1, controller.FrameLimit);
            runtime.Paused = snapshot.Paused;
            runtime.ReplayInProgress = snapshot.ReplayInProgress;
            runtime.HistoryPlaybackActive = snapshot.HistoryPlaybackActive;
            runtime.FutureAdvanceActive = controller.FutureAdvanceActive;
            runtime.FutureAdvanceTargetFrame = controller.FutureAdvanceTargetFrame;
            runtime.BranchRecomputeArmed = snapshot.BranchRecomputeArmed;
            runtime.BranchConfirmationPending = snapshot.BranchConfirmationPending;
            runtime.BranchConfirmationGeneration = snapshot.BranchConfirmationGeneration;
            runtime.SelectionPhase = snapshot.FrameSelectionPhase;
            runtime.CommitGeneration = snapshot.CommittedFrameReceipt.CommitGeneration;
            runtime.AuthorityHash = snapshot.CommittedFrameReceipt.AuthorityHash;
            runtime.PendingTargetFrame = snapshot.ActiveRestoreTarget.IsValid
                ? snapshot.ActiveRestoreTarget.Frame
                : 0;
            runtime.QueuedTargetFrame = snapshot.QueuedTarget.IsValid
                ? snapshot.QueuedTarget.Frame
                : 0;
            runtime.PlayPauseLabel = snapshot.Paused && !snapshot.HistoryPlaybackActive
                ? "播放"
                : "暂停";

            if (timeline == null || timeline.Count <= 0)
            {
                runtime.Available = false;
                runtime.CanPlayPause = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running;
                runtime.FrameSummaryText = "尚未捕获运行帧";
                runtime.SeekStatusText = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                    ? "开始测试后可观察角色行动并调整模拟帧"
                    : "等待首个 RuntimeFrame";
                return;
            }

            runtime.Available = true;
            runtime.SessionStartFrame = timeline.Records[0].Frame;
            runtime.RetainedStartFrame = timeline.RetainedStartFrame;
            runtime.SelectedFrame = snapshot.CommittedFrameReceipt.IsValid
                ? snapshot.CommittedFrameReceipt.Frame
                : selectedFrame != null ? selectedFrame.Frame : timeline.SelectedFrame;
            runtime.LatestFrame = timeline.LatestFrame;
            runtime.SessionEndFrame = runtime.SessionStartFrame + runtime.FrameLimit - 1;
            runtime.FrameInputText = runtime.SelectedFrame.ToString(CultureInfo.InvariantCulture);
            bool locked = snapshot.FrameSelectionPhase == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked;
            bool busy = snapshot.FrameSelectionPhase != CharacterTestRuntimeAiLabFrameSelectionPhase.Idle
                || snapshot.ReplayInProgress
                || controller.FutureAdvanceActive;
            runtime.CanSeek = !busy && !locked;
            runtime.CanFirst = runtime.CanSeek && runtime.SelectedFrame > runtime.RetainedStartFrame;
            runtime.CanLatest = runtime.CanSeek && runtime.SelectedFrame < runtime.LatestFrame;
            runtime.CanJump = runtime.CanSeek;
            runtime.CanStepBackward = !busy && runtime.SelectedFrame > runtime.RetainedStartFrame;
            runtime.CanStepForward = !busy
                && !locked
                && runtime.SelectedFrame < runtime.LatestFrame;
            runtime.CanPlayPause = !busy
                && !locked
                && snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running;
            runtime.CanBranchRecompute = !busy
                && !locked
                && snapshot.Paused
                && runtime.SelectedFrame < runtime.LatestFrame;
            FillRuntimeControlDisabledReasons(runtime, busy, locked);
            runtime.FrameSummaryText = "选中 "
                + runtime.SelectedFrame.ToString(CultureInfo.InvariantCulture)
                + " / 最新 "
                + runtime.LatestFrame.ToString(CultureInfo.InvariantCulture)
                + " / 上限 "
                + runtime.SessionEndFrame.ToString(CultureInfo.InvariantCulture);
            runtime.SeekStatusText = snapshot.FrameSelectionPhase == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked
                ? "会话恢复失败：只能重置或退出"
                : runtime.PendingTargetFrame > 0
                    ? "当前 " + runtime.SelectedFrame.ToString(CultureInfo.InvariantCulture)
                        + " · 正在定位 " + runtime.PendingTargetFrame.ToString(CultureInfo.InvariantCulture)
                        + (runtime.QueuedTargetFrame > 0
                            ? " · 最终 " + runtime.QueuedTargetFrame.ToString(CultureInfo.InvariantCulture)
                            : string.Empty)
                : snapshot.ReplayInProgress
                    ? "Warm Replay 正在恢复历史帧…"
                : controller.FutureAdvanceActive
                    ? "正在模拟 "
                        + runtime.LatestFrame.ToString(CultureInfo.InvariantCulture)
                        + " / "
                        + controller.FutureAdvanceTargetFrame.ToString(CultureInfo.InvariantCulture)
                    : snapshot.HistoryPlaybackActive
                        ? "正在顺序回放已捕获历史"
                        : runtime.SelectedFrame < runtime.LatestFrame
                            ? "历史只读回看；建立新未来需显式选择“从此重算”"
                            : snapshot.Paused ? "已暂停在最新帧" : "实时模拟中";
            runtime.BranchActionLabel = snapshot.BranchRecomputeArmed
                ? "取消重算"
                : snapshot.BranchConfirmationPending ? "确认重算" : "从此重算";
            runtime.BranchRiskText = viewModel.BranchRecomputeRiskText;
        }

        private static void FillRuntimeControlDisabledReasons(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtime,
            bool busy,
            bool locked)
        {
            string globalCode = locked ? "SESSION_LOCKED" : busy ? "SEEK_BUSY" : string.Empty;
            string globalText = locked
                ? "当前会话恢复失败，只能重置或退出。"
                : busy ? "正在定位 RuntimeFrame，请等待提交完成。" : string.Empty;
            runtime.FirstDisabledReasonCode = globalCode;
            runtime.FirstDisabledReasonText = globalText;
            runtime.BackwardDisabledReasonCode = globalCode;
            runtime.BackwardDisabledReasonText = globalText;
            runtime.ForwardDisabledReasonCode = globalCode;
            runtime.ForwardDisabledReasonText = globalText;
            runtime.LatestDisabledReasonCode = globalCode;
            runtime.LatestDisabledReasonText = globalText;
            runtime.PlayDisabledReasonCode = globalCode;
            runtime.PlayDisabledReasonText = globalText;
            runtime.JumpDisabledReasonCode = globalCode;
            runtime.JumpDisabledReasonText = globalText;
            runtime.BranchDisabledReasonCode = globalCode;
            runtime.BranchDisabledReasonText = globalText;

            if (locked || busy)
                return;
            if (!runtime.CanFirst)
            {
                runtime.FirstDisabledReasonCode = "RETAINED_START";
                runtime.FirstDisabledReasonText = "已经位于最早保留帧。";
            }
            if (!runtime.CanStepBackward)
            {
                runtime.BackwardDisabledReasonCode = "RETAINED_START";
                runtime.BackwardDisabledReasonText = "没有更早的保留帧。";
            }
            if (!runtime.CanStepForward)
            {
                runtime.ForwardDisabledReasonCode = "LATEST_FRAME";
                runtime.ForwardDisabledReasonText = "已经位于最新已捕获帧。";
            }
            if (!runtime.CanLatest)
            {
                runtime.LatestDisabledReasonCode = "LATEST_FRAME";
                runtime.LatestDisabledReasonText = "已经位于最新已捕获帧。";
            }
            if (!runtime.CanPlayPause)
            {
                runtime.PlayDisabledReasonCode = "SESSION_NOT_RUNNING";
                runtime.PlayDisabledReasonText = "当前会话不可播放。";
            }
            if (!runtime.CanBranchRecompute)
            {
                runtime.BranchDisabledReasonCode = runtime.SelectedFrame >= runtime.LatestFrame
                    ? "HISTORY_FRAME_REQUIRED"
                    : "VERIFIED_HISTORY_REQUIRED";
                runtime.BranchDisabledReasonText = runtime.SelectedFrame >= runtime.LatestFrame
                    ? "请先选择一个历史帧。"
                    : "请先暂停并完成历史帧校验。";
            }
        }

        private static string BuildFrameSummary(
            CharacterTestRuntimeAiPlannerDevFrameBuffer buffer,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame,
            int frameLimit)
        {
            if (buffer == null || buffer.Count <= 0 || selectedFrame == null)
                return "时间轴：配置中 | 尚未捕获运行时帧";

            string mode = timeline != null ? FormatTimelineMode(timeline.Mode) : "未知";
            string latest = timeline != null
                ? timeline.LatestFrame.ToString(CultureInfo.InvariantCulture)
                : "-";
            return mode
                + " | 捕获 "
                + buffer.Count.ToString(CultureInfo.InvariantCulture)
                + "/"
                + Math.Max(1, frameLimit).ToString(CultureInfo.InvariantCulture)
                + " | 选 "
                + selectedFrame.Frame.ToString(CultureInfo.InvariantCulture)
                + " | 新 "
                + latest;
        }

        private static string BuildTimelineText(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameBuffer buffer)
        {
            if (timeline == null || timeline.Count <= 0)
                return "尚未捕获帧。";

            int selected = timeline.SelectedIndex >= 0 ? timeline.SelectedIndex : timeline.Count - 1;
            int start = Math.Max(0, selected - TimelineRows / 2);
            int end = Math.Min(timeline.Count, start + TimelineRows);
            if (end - start < TimelineRows)
                start = Math.Max(0, end - TimelineRows);
            var builder = new StringBuilder(512);
            for (int i = start; i < end; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                bool isSelected = i == timeline.SelectedIndex;
                builder.Append(isSelected ? "> " : "  ")
                    .Append(record.Frame.ToString(CultureInfo.InvariantCulture));
                if (record.HasCheckpoint)
                    builder.Append("C");
                if (timeline.EditBranchStartFrame == record.Frame)
                    builder.Append("*");
                if (timeline.LoopStartFrame == record.Frame)
                    builder.Append("[A]");
                if (timeline.LoopEndFrame == record.Frame)
                    builder.Append("[B]");
                builder.Append(" 智能体 ")
                    .Append(record.FrameSnapshot.Agents != null ? record.FrameSnapshot.Agents.Count : 0)
                    .Append(" 哈希 ")
                    .Append(record.StateHash.ToString("X8", CultureInfo.InvariantCulture));
                if (i + 1 < end)
                    builder.Append("    ");
            }

            return builder.ToString();
        }

        private static string BuildPreviewInstructions()
        {
            return "暂停并推进一帧后切换智能体；查看场景连线、距离和移动箭头。";
        }

        private static string BuildSetupInstructions()
        {
            return "三维配置：拖动角色预览，或选择槽位后点击地面移动。WASD/右键平移，Shift+右键旋转，滚轮缩放，F/适配视野显示整个场地。";
        }

    }
}
