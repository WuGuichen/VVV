using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MxFramework.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private const int TimelineRows = 8;

        public static CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel Build(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            string commandStatus,
            IMxUiTextProvider textProvider = null,
            CharacterTestRuntimeAiLabWorkspaceMode workspaceMode =
                CharacterTestRuntimeAiLabWorkspaceMode.Setup,
            int selectedWorkbenchEntryIndex = 0,
            int selectedCompiledStepIndex = 0,
            bool isSelectedCardExpanded = false,
            CharacterTestRuntimeAiLabLogicElementKind selectedLogicElementKind =
                CharacterTestRuntimeAiLabLogicElementKind.Stage,
            int selectedLogicElementIndex = -1,
            CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab =
                CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView behaviorCardLibrary = null,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState? behaviorCardLibraryState = null,
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel behaviorCardWorkspace = null,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel reusableViewModel = null,
            long presentationCommandRevision = 0L,
            CharacterTestRuntimeAiLabTimelineMode timelineMode =
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
            bool? runtimeTraceExpanded = null,
            bool? behaviorPlanExpanded = null,
            int pendingSetupBehaviorGroupDeleteDraftId = -1,
            int workbenchScrollOffset = 0)
        {
            // Accept the old expanded-card argument as a migration seam, but
            // represent the card editor as a real workspace from this point on.
            if (isSelectedCardExpanded)
                workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            if (workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary)
                return new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(behaviorCardWorkspace)
                { WorkspaceMode = workspaceMode, StatusText = commandStatus, TacticalPresentationRevision = controller?.DraftWorkspace?.CurrentRevision ?? 0 };
            bool unifiedTimelineHost =
                workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
            bool behaviorCardContent =
                workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                || (unifiedTimelineHost
                    && timelineMode != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
            isSelectedCardExpanded = behaviorCardContent;

            long behaviorCardSourceRevision = behaviorCardContent
                ? ComputeBehaviorCardBuildSourceRevision(
                    controller,
                    commandStatus,
                    textProvider,
                    selectedWorkbenchEntryIndex,
                    selectedCompiledStepIndex,
                    selectedLogicElementKind,
                    selectedLogicElementIndex,
                    behaviorCardLibrary,
                    behaviorCardLibraryState,
                    presentationCommandRevision,
                    behaviorCardWorkspace?.Timeline?.Presentation)
                : 0L;
            if (reusableViewModel != null
                && behaviorCardContent
                && reusableViewModel.WorkspaceMode == workspaceMode
                && reusableViewModel.TimelineMode == timelineMode
                && reusableViewModel.HasBehaviorCardBuildSourceRevision
                && reusableViewModel.BehaviorCardBuildSourceRevision == behaviorCardSourceRevision)
            {
                return reusableViewModel;
            }

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel = reusableViewModel
                ?? new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(behaviorCardWorkspace);
            if (behaviorCardWorkspace != null
                && !ReferenceEquals(viewModel.BehaviorCardWorkspace, behaviorCardWorkspace))
            {
                viewModel.BehaviorCardWorkspace = behaviorCardWorkspace;
            }
            viewModel.ImportTacticalSource = controller == null ? null : controller.RequestTacticalImport;
            viewModel.TacticalPresentationRevision = controller?.TacticalPresentationRevision ?? 0;
            viewModel.TacticalFrameSummary = controller == null ? null : () => controller.Timeline?.HasSelection == true
                ? controller.Timeline.SelectedFrame + " / " + controller.Timeline.LatestFrame + " f" : "尚未测试";
            viewModel.HasTacticalTestFrame = controller == null ? null : () => controller.Timeline?.HasSelection == true;
            viewModel.CreateTacticalBlank = controller == null ? null : () => controller.CreateTacticalSource(false);
            viewModel.CreateTacticalTemplate = controller == null ? null : () => controller.CreateTacticalSource(true);
            viewModel.PrepareTacticalSummary = null;
            viewModel.RequeryTacticalSource = controller == null ? null : controller.RefreshTacticalPreview;
            FillStaticLabels(viewModel, textProvider);
            viewModel.TimelineMode = timelineMode;
            viewModel.ShowTimelineModeHost = unifiedTimelineHost;
            viewModel.ShowBehaviorPlan = unifiedTimelineHost
                && timelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan;
            viewModel.ShowBehaviorLogic = unifiedTimelineHost
                && timelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic;
            viewModel.RuntimeTraceModeSelected =
                timelineMode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
            viewModel.BehaviorPlanModeSelected =
                timelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan;
            viewModel.BehaviorLogicModeSelected =
                timelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic;
            viewModel.RuntimeTraceExpanded = runtimeTraceExpanded ?? true;
            viewModel.BehaviorPlanExpanded = behaviorPlanExpanded ?? true;

            if (controller == null)
            {
                viewModel.PresentationContext = CharacterTestRuntimeAiPlannerDevPresentationContext.Unavailable;
                viewModel.StatusText = "运行时智能实验室控制器不可用。";
                viewModel.PhaseText = "不可用";
                viewModel.FrameSummary = "当前没有测试会话。";
                viewModel.PreviewInstructionsText = BuildPreviewInstructions();
                viewModel.SetupInstructionsText = BuildSetupInstructions();
                viewModel.SetupSelectedSlotText = "尚未选择测试槽位。";
                viewModel.SetupValidationText = "测试配置不可用。";
                viewModel.PreviewStatusText = "场景标记不可用。";
                viewModel.TimelineText = "尚未捕获帧。";
                viewModel.AgentListText = "当前没有运行时智能规划智能体。";
                viewModel.DiagnosticsText = "请打开 CharacterTestRuntimeAiLabScene 使用此面板。";
                FillAuthoringContext(
                    viewModel,
                    null,
                    workspaceMode,
                    selectedWorkbenchEntryIndex,
                    CharacterTestRuntimeAiPlannerDevPresentationContext.Unavailable);
                return viewModel;
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot = controller.CaptureSnapshot();
            CharacterTestRuntimeAiPlannerDevPresentationContext presentationContext =
                ResolvePresentationContext(snapshot);
            viewModel.PresentationContext = presentationContext;
            viewModel.ShowSetupToolbar = presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Setup;
            viewModel.ShowLiveToolbar = presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Live;
            viewModel.ShowPausedToolbar = presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect
                || presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Review
                || presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview
                || presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Completed;
            viewModel.ShowReviewState = presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Review
                || presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview;
            viewModel.ShowTimeline = unifiedTimelineHost
                && timelineMode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
            viewModel.RuntimeTraceExpanded = runtimeTraceExpanded ?? controller.TimelineExpanded;
            viewModel.BehaviorPlanExpanded = behaviorPlanExpanded ?? true;
            viewModel.TimelineExpanded = viewModel.RuntimeTraceExpanded;
            viewModel.CanToggleTimelineExpanded = viewModel.ShowTimeline;
            viewModel.CanZoomTimeline = viewModel.ShowTimeline;
            viewModel.CanPanTimeline = viewModel.ShowTimeline && controller.TimelineZoomLevel >= 0;
            // Timeline mode is presentation state. Keep both tabs available while
            // reviewing completed or historical RuntimeFrame data; only the absence
            // of retained history makes the shared host unavailable.
            viewModel.CanSwitchTimelineMode = unifiedTimelineHost
                && snapshot.Timeline != null
                && snapshot.Timeline.Count > 0;
            viewModel.TimelineModeDisabledReasonText = viewModel.CanSwitchTimelineMode
                ? string.Empty
                : "开始测试并保留至少一帧后才能切换时间轴模式。";
            viewModel.FrameLimitLabel = "帧上限 " + controller.FrameLimit.ToString(CultureInfo.InvariantCulture);
            viewModel.TimelineZoomLabel = controller.TimelineZoomLevel < 0
                ? "适配"
                : "缩放 " + controller.TimelineZoomLevel.ToString(CultureInfo.InvariantCulture);
            viewModel.StatusText = string.IsNullOrWhiteSpace(commandStatus)
                ? snapshot.StatusMessage
                : commandStatus;
            viewModel.PhaseText = BuildPhaseText(presentationContext, snapshot.Timeline);
            viewModel.PauseResumeLabel = snapshot.HistoryPlaybackActive
                ? "暂停回放"
                : presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview
                ? "暂停循环"
                : presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Review
                    ? snapshot.Timeline != null && snapshot.Timeline.LoopEnabled
                        ? "播放循环"
                        : snapshot.BranchRecomputeArmed
                            ? "从此重算并播放"
                            : "从当前帧播放"
                : snapshot.Paused
                    ? L(textProvider, "ui.character_test.runtime_ai_planner.dev.resume", "播放")
                    : L(textProvider, "ui.character_test.runtime_ai_planner.dev.pause", "暂停");
            CharacterTestRuntimeAiLabSpawnValidationResult setupValidation =
                snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                    ? controller.ValidateSetupSpawns()
                    : null;
            viewModel.CanStart = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                && setupValidation?.CanStart == true;
            if (snapshot.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                viewModel.StartLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.started", "已开始");
            viewModel.CanPauseResume = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && !snapshot.ReplayInProgress;
            bool canStepFrames = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && snapshot.Paused
                && !snapshot.LoopPlaybackActive
                && !snapshot.ReplayInProgress
                && controller.SessionContinuable;
            viewModel.CanStep = canStepFrames
                && snapshot.Timeline != null
                && snapshot.Timeline.HasSelection
                && (snapshot.Timeline.SelectedFrame == snapshot.Timeline.LatestFrame
                    || snapshot.BranchRecomputeArmed)
                && snapshot.Timeline.Count < controller.FrameLimit;
            viewModel.BranchRecomputeSelected = snapshot.BranchRecomputeArmed
                || snapshot.BranchConfirmationPending;
            viewModel.BranchRecomputeLabel = snapshot.BranchRecomputeArmed
                ? "取消重算"
                : snapshot.BranchConfirmationPending ? "确认重算" : "从此重算";
            viewModel.CanToggleBranchRecompute = snapshot.BranchRecomputeArmed
                || snapshot.BranchConfirmationPending
                || (canStepFrames
                    && snapshot.Timeline != null
                    && snapshot.Timeline.HasSelection
                    && snapshot.Timeline.SelectedFrame < snapshot.Timeline.LatestFrame
                    && !controller.FutureAdvanceActive);
            viewModel.BranchRecomputeRiskText = snapshot.BranchRecomputeArmed
                || snapshot.BranchConfirmationPending
                ? "基于帧 " + snapshot.BranchRecomputeBaseFrame
                    + "，首次生成将删除 " + (snapshot.BranchRecomputeBaseFrame + 1)
                    + "…" + snapshot.BranchRecomputeOriginalLatestFrame
                : string.Empty;
            viewModel.CanStepBack = canStepFrames && snapshot.Timeline != null && snapshot.Timeline.Count > 0;
            viewModel.CanStep10 = canStepFrames;
            viewModel.CanJumpFrame = viewModel.ShowPausedToolbar
                && !snapshot.ReplayInProgress
                && snapshot.Timeline != null
                && snapshot.Timeline.Count > 0;
            viewModel.CanChangeSpeed = (viewModel.ShowLiveToolbar || viewModel.ShowPausedToolbar)
                && !snapshot.ReplayInProgress;
            viewModel.CanSetLoop = viewModel.ShowPausedToolbar
                && !snapshot.ReplayInProgress
                && snapshot.Timeline != null
                && snapshot.Timeline.Count > 0;
            viewModel.CanSetLoopRange = viewModel.CanSetLoop && !snapshot.LoopPlaybackActive;
            viewModel.CanReset = true;
            viewModel.CanToggleEditGate = presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect
                && !snapshot.ReplayInProgress;
            viewModel.EditGateSelected = snapshot.DebugEditGateEnabled;
            viewModel.EditGateText = snapshot.DebugEditGateEnabled
                ? "编辑锁：暂停会话中已解锁。"
                : "编辑锁：已锁定。暂停后可进行受控选择编辑。";
            viewModel.CanTogglePreviewOverlay = true;
            viewModel.PreviewOverlaySelected = controller.PreviewOverlayEnabled;
            viewModel.CanToggleDetails = true;
            viewModel.CanToggleStateCapture = viewModel.ShowPausedToolbar
                && !snapshot.ReplayInProgress;
            viewModel.CanValidateState = viewModel.ShowPausedToolbar
                && !snapshot.ReplayInProgress
                && snapshot.Timeline != null
                && snapshot.Timeline.HasSelection;
            viewModel.CanSyncRecordedState = viewModel.CanValidateState
                && controller.HasSelectedTestStateCapture;
            viewModel.StateCaptureSelected = controller.StateCaptureEnabled;
            viewModel.CanColdBootstrapVerify = viewModel.CanValidateState
                && snapshot.Paused
                && !snapshot.LoopPlaybackActive;
            viewModel.StateValidationText = controller.FrameSyncStatusText
                + "\n"
                + controller.PresentationStatusText;
            viewModel.CanEditSetupPlacement = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
            viewModel.CanFitSetupView = snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
            viewModel.SetupSnapSelected = controller.SetupSnapEnabled;
            viewModel.ToggleSetupSnapLabel = controller.SetupSnapEnabled ? "吸附 1 米" : "自由移动";
            viewModel.DetailsExpanded = snapshot.DetailsExpanded;
            viewModel.ShowAdvancedDiagnostics = snapshot.DetailsExpanded
                && presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect;
            viewModel.ToggleDetailsLabel = snapshot.DetailsExpanded
                ? L(textProvider, "ui.character_test.runtime_ai_planner.dev.hide_details", "收起详情")
                : L(textProvider, "ui.character_test.runtime_ai_planner.dev.details", "详情");
            viewModel.PreviewInstructionsText = BuildPreviewInstructions();
            viewModel.SetupInstructionsText = BuildSetupInstructions();
            if (workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Setup)
            {
                // Presentation reads the last matching preflight receipt, never runs startup work.
                string sessionStartValidation = string.Empty;
                bool validationPending = false;
                bool sessionStartValid = snapshot.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                    || controller.TryGetSessionStartStatusForDisplay(out sessionStartValidation, out validationPending);
                FillSetupPlacement(
                    viewModel,
                    controller,
                    setupValidation,
                    pendingSetupBehaviorGroupDeleteDraftId);
                if (validationPending && setupValidation?.CanStart == true)
                {
                    viewModel.SetupReadyTitleText = "● 待启动校验";
                    viewModel.SetupReadyDescriptionText = "开始测试时将核对当前行为卡、装配与资源。";
                }
                if (!sessionStartValid)
                {
                    viewModel.SetupTopValidationText = "● 配置待修复";
                    viewModel.SetupReadyTitleText = "● 待修复";
                    if (setupValidation == null || setupValidation.CanStart)
                    {
                        viewModel.SetupValidationText = sessionStartValidation;
                        viewModel.SetupReadyDescriptionText = sessionStartValidation
                            + "\n测试不会启动；可点击行为组右侧的“编辑”直接修复。";
                    }
                }
            }

            CharacterTestRuntimeAiPlannerDevFrameBuffer buffer = snapshot.FrameBuffer;
            CharacterTestRuntimeAiPlannerDevTimeline timeline = snapshot.Timeline;
            CharacterTestRuntimeAiLabCommittedFrameReceipt committedReceipt = snapshot.CommittedFrameReceipt;
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame = committedReceipt.IsValid
                ? committedReceipt.Bundle.Record.FrameSnapshot
                : snapshot.SelectedFrame;
            int frameCount = buffer != null ? buffer.Count : 0;
            viewModel.CanSelectFrame = viewModel.ShowPausedToolbar && frameCount > 0;
            viewModel.FrameSummary = BuildFrameSummary(buffer, timeline, selectedFrame, controller.FrameLimit);
            if (!string.IsNullOrEmpty(controller.SimulationPacingStatus)) viewModel.StatusText = controller.SimulationPacingStatus + " " + viewModel.StatusText;
            viewModel.JumpFrameInputText = selectedFrame != null
                ? selectedFrame.Frame.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            if (behaviorCardContent)
            {
                FillBehaviorCardRuntimeControl(
                    viewModel,
                    controller,
                    snapshot,
                    timeline,
                    selectedFrame);
            }
            bool buildRuntimeTimeline = unifiedTimelineHost;
            if (buildRuntimeTimeline)
            {
                viewModel.TimelineText = BuildTimelineText(timeline, buffer);
                FillTimelineRange(viewModel, timeline, selectedFrame);
                viewModel.TimelineProjection = controller.BuildTimelineProjection();
                if (controller.TryGetSelectedTimelineItem(out CharacterTestRuntimeAiLabTimelineItem selectedTimelineItem))
                    viewModel.TimelineSelectedItem = selectedTimelineItem;
                viewModel.TimelineSelectedVisualStableKey = controller.SelectedTimelineVisualStableKey;
                viewModel.TimelineFutureAdvanceActive = controller.FutureAdvanceActive;
                viewModel.TimelineFutureAdvanceStartFrame = controller.FutureAdvanceStartFrame;
                viewModel.TimelineFutureAdvanceTargetFrame = controller.FutureAdvanceTargetFrame;
                viewModel.TimelineStatusText = snapshot.BranchRecomputeArmed
                    ? viewModel.BranchRecomputeRiskText
                    : controller.FutureAdvanceActive
                    ? "正在模拟 " + timeline.LatestFrame + "/" + controller.FutureAdvanceTargetFrame
                    : viewModel.TimelineProjection?.StatusMessage ?? string.Empty;
                FillRuntimeFrameWorkspace(
                    viewModel,
                    controller,
                    snapshot,
                    timeline,
                    viewModel.TimelineProjection,
                    selectedFrame,
                    snapshot.SelectedAgentIndex);
            }
            else if (workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Replay)
            {
                FillReplayWorkspace(viewModel, controller);
            }
            viewModel.SpeedLabel = timeline != null
                ? timeline.PlaybackSpeed.ToString("0.##", CultureInfo.InvariantCulture) + "x"
                : "1x";
            viewModel.LoopStartLabel = timeline != null && timeline.LoopStartFrame >= 0
                ? "A " + timeline.LoopStartFrame.ToString(CultureInfo.InvariantCulture)
                : "A";
            viewModel.LoopEndLabel = timeline != null && timeline.LoopEndFrame >= 0
                ? "B " + timeline.LoopEndFrame.ToString(CultureInfo.InvariantCulture)
                : "B";
            viewModel.ToggleLoopLabel = timeline != null && timeline.LoopEnabled ? "循环已开" : "循环";
            viewModel.LoopSelected = timeline != null && timeline.LoopEnabled;
            viewModel.PreviewStatusText = BuildPreviewStatus(controller, selectedFrame);

            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                selectedFrame != null ? selectedFrame.Agents : null;
            int agentCount = agents != null ? agents.Count : 0;
            viewModel.CanSelectAgent = agentCount > 0;
            int selectedAgentIndex = ClampIndex(snapshot.SelectedAgentIndex, agentCount);
            if (workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics)
            {
                if (diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Facts)
                {
                    FillFactsWorkspace(viewModel, controller, snapshot, agents, selectedFrame);
                }
                else
                {
                    viewModel.AgentListText = BuildAgentListText(agents, selectedAgentIndex);
                    FillSelectedAgent(viewModel, agents, selectedAgentIndex, selectedFrame);
                }
            }
            bool hasSelectedAgent = agents != null
                && selectedAgentIndex >= 0
                && selectedAgentIndex < agents.Count;
            CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent = hasSelectedAgent
                ? agents[selectedAgentIndex]
                : default;
            CharacterTestRuntimeAiLabWorkspaceMode contentWorkspaceMode =
                behaviorCardContent
                    ? CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    : workspaceMode;
            FillWorkbench(
                viewModel,
                controller,
                contentWorkspaceMode,
                diagnosticsTab,
                selectedWorkbenchEntryIndex,
                selectedCompiledStepIndex,
                selectedLogicElementKind,
                selectedLogicElementIndex,
                isSelectedCardExpanded,
                presentationContext,
                selectedFrame,
                selectedAgent,
                hasSelectedAgent,
                timeline?.LatestFrame ?? 0,
                workbenchScrollOffset);
            if (behaviorCardContent)
            {
                viewModel.BehaviorCardWorkspace.Timeline.CommandFeedbackText =
                    commandStatus ?? string.Empty;
            }
            if (behaviorCardContent)
                FillCurrentBehaviorSummary(viewModel, snapshot, selectedAgent, hasSelectedAgent);
            if (behaviorCardContent
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup)
            {
                CharacterTestRuntimeAiLabDraftWorkspace authoringWorkspace =
                    controller.DraftWorkspace;
                if (authoringWorkspace != null)
                {
                    FillBehaviorCardLibrary(
                        viewModel,
                        behaviorCardLibrary,
                        behaviorCardLibraryState,
                        authoringWorkspace,
                        controller);
                    viewModel.BehaviorCardWorkspace.Library.HeaderText =
                        controller.IsProjectBehaviorCardLibraryActive
                            ? "项目行为卡目录"
                            : "本地玩家卡库";
                    CharacterTestRuntimeAiLabBehaviorCardViewport viewport =
                        controller.BehaviorCardPresentationViewport;
                    CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel behaviorTimeline =
                        viewModel.BehaviorCardWorkspace.Timeline;
                    behaviorTimeline.HorizontalStartFrame = viewport.HorizontalStartFrame;
                    behaviorTimeline.HorizontalEndFrame = viewport.HorizontalEndFrame;
                    behaviorTimeline.ZoomLevel = viewport.ZoomLevel;
                    behaviorTimeline.VerticalScrollOffset = viewport.VerticalScrollOffset;
                    behaviorTimeline.FollowCurrent = viewport.FollowCurrent;
                }
                FinalizeBehaviorCardWorkspaceRevision(viewModel, presentationCommandRevision);
            }
            if (behaviorCardContent
                && !string.IsNullOrEmpty(viewModel.PlayerLegalityStatusText))
            {
                viewModel.StatusText = viewModel.PlayerLegalityStatusText
                    + " | "
                    + viewModel.StatusText;
            }
            if (behaviorCardContent)
            {
                viewModel.BehaviorCardBuildSourceRevision = behaviorCardSourceRevision;
                viewModel.HasBehaviorCardBuildSourceRevision = true;
            }
            if (unifiedTimelineHost)
            {
                viewModel.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                viewModel.DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
                viewModel.IsSelectedCardExpanded = false;
                viewModel.ShowWorkbench = false;
                CharacterTestRuntimeAiLabWorkspaceViewModel workspaceView = viewModel.Workspace
                    ?? new CharacterTestRuntimeAiLabWorkspaceViewModel();
                viewModel.Workspace = workspaceView;
                workspaceView.ActiveWorkspace =
                    CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                workspaceView.ActiveDiagnosticsTab =
                    CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
                workspaceView.ActiveTimelineMode = timelineMode;
                workspaceView.ShowSetup = false;
                workspaceView.ShowBehaviorGroup = false;
                workspaceView.ShowBehaviorCard = false;
                workspaceView.ShowDiagnostics = true;
            }
            FillAuthoringContext(
                viewModel,
                controller,
                contentWorkspaceMode,
                selectedWorkbenchEntryIndex,
                presentationContext);
            ApplyFrameSelectionGlobalGate(viewModel, snapshot);
            return viewModel;
        }

        private static void ApplyFrameSelectionGlobalGate(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot)
        {
            bool failedLocked = snapshot.FrameSelectionPhase
                == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked;
            bool busy = snapshot.FrameSelectionPhase
                != CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
            if (!failedLocked && !busy)
                return;

            viewModel.CanStart = false;
            viewModel.CanPauseResume = false;
            viewModel.CanStep = false;
            viewModel.CanStepBack = false;
            viewModel.CanStep10 = false;
            viewModel.CanJumpFrame = false;
            viewModel.CanChangeSpeed = false;
            viewModel.CanSetLoop = false;
            viewModel.CanSetLoopRange = false;
            viewModel.CanToggleEditGate = false;
            viewModel.CanToggleStateCapture = false;
            viewModel.CanValidateState = false;
            viewModel.CanSyncRecordedState = false;
            viewModel.CanColdBootstrapVerify = false;
            viewModel.CanEditSetupPlacement = false;
            viewModel.CanOpenBehaviorGroup = false;
            viewModel.CanCreateSetupBehaviorGroup = false;
            viewModel.CanDeleteSetupBehaviorGroup = false;
            viewModel.SetupBehaviorGroupDeleteConfirming = false;
            viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = -1;
            viewModel.CanSelectFrame = false;
            viewModel.CanSelectAgent = false;
            viewModel.CanEditWorkbench = false;
            viewModel.CanUndoDraft = false;
            viewModel.CanApplyDraft = false;
            viewModel.CanToggleBranchRecompute = false;
            viewModel.CanReset = failedLocked;
            if (failedLocked)
            {
                viewModel.ApplyDraftDisabledReasonText =
                    "当前会话恢复失败，只能重置或退出 Runtime AI Lab。";
            }
        }

        private static void FillStaticLabels(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            IMxUiTextProvider textProvider)
        {
            viewModel.Title = L(textProvider, "ui.character_test.runtime_ai_planner.dev.title", "运行时智能实验室");
            viewModel.PreviewHeader = L(textProvider, "ui.character_test.runtime_ai_planner.dev.preview_header", "当前智能体");
            viewModel.SetupHeader = L(textProvider, "ui.character_test.runtime_ai_planner.dev.setup_header", "三维测试配置");
            viewModel.TimelineHeader = L(textProvider, "ui.character_test.runtime_ai_planner.dev.timeline", "帧时间轴");
            viewModel.AgentHeader = L(textProvider, "ui.character_test.runtime_ai_planner.dev.agents", "智能体");
            viewModel.DecisionHeader = L(textProvider, "ui.character_test.runtime_ai_planner.dev.decision", "决策详情");
            viewModel.StartLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.start", "开始测试");
            viewModel.StepLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.step", "推进 +1");
            viewModel.StepBackLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.step_back", "-1");
            viewModel.StepBack10Label = L(textProvider, "ui.character_test.runtime_ai_planner.dev.step_back_10", "-10");
            viewModel.StepForward10Label = L(textProvider, "ui.character_test.runtime_ai_planner.dev.step_forward_10", "+10");
            viewModel.ResetLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.reset", "重置");
            viewModel.PreviousFrameLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.previous_frame", "< Frame");
            viewModel.NextFrameLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.next_frame", "Frame >");
            viewModel.LatestFrameLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.latest", "最新帧");
            viewModel.JumpFrameLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.jump_frame", "跳转");
            viewModel.PreviousAgentLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.previous_agent", "< 智能体");
            viewModel.NextAgentLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.next_agent", "智能体 >");
            viewModel.ToggleEditGateLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.edit_gate", "编辑锁");
            viewModel.ToggleStateCaptureLabel = "状态采集";
            viewModel.ValidateStateLabel = "校验状态";
            viewModel.SyncRecordedStateLabel = "同步记录状态";
            viewModel.TogglePreviewOverlayLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.overlay", "场景标记");
            viewModel.ToggleSetupSnapLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.snap", "吸附 1 米");
            viewModel.ColdBootstrapVerifyLabel = "冷启动校验";
            viewModel.MirrorSpawnsLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.mirror", "镜像站位");
            viewModel.ResetSpawnDefaultsLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.reset_pads", "重置站位");
            viewModel.FitSetupViewLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.fit_view", "适配视野");
            viewModel.CloseLabel = L(textProvider, "ui.character_test.runtime_ai_planner.dev.hide", "隐藏");
        }

        /// <remarks>NoAlloc after the controller and Draft workspace have been initialized.</remarks>
        internal static long ComputeBehaviorCardBuildSourceRevision(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            string commandStatus,
            IMxUiTextProvider textProvider,
            int selectedWorkbenchEntryIndex,
            int selectedCompiledStepIndex,
            CharacterTestRuntimeAiLabLogicElementKind selectedLogicElementKind,
            int selectedLogicElementIndex,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView behaviorCardLibrary,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState? behaviorCardLibraryState,
            long presentationCommandRevision = 0L,
            BehaviorPlanPresentationState presentation = null)
        {
            unchecked
            {
                long hash = 17L;
                hash = hash * 397L ^ StableTextHash(commandStatus);
                hash = hash * 397L ^ CharacterTestUiTextProviderSignature.Create(textProvider).GetHashCode();
                hash = hash * 397L ^ selectedWorkbenchEntryIndex;
                hash = hash * 397L ^ selectedCompiledStepIndex;
                hash = hash * 397L ^ (int)selectedLogicElementKind;
                hash = hash * 397L ^ selectedLogicElementIndex;
                hash = hash * 397L ^ presentationCommandRevision;
                if (presentation != null)
                {
                    hash = hash * 397L ^ (int)presentation.RecipeInspectorTargetKind;
                    hash = hash * 397L ^ StableTextHash(presentation.SelectedRecipeTimelineId);
                    hash = hash * 397L ^ StableTextHash(presentation.SelectedRecipeEventId);
                    hash = hash * 397L ^ (int)presentation.SelectedRecipeEventKind;
                    hash = hash * 397L ^ StableTextHash(presentation.SelectedRecipeEventStableKey);
                }
                if (controller == null)
                    return hash;

                CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot = controller.CaptureSnapshot();
                hash = hash * 397L ^ (int)snapshot.Phase;
                hash = hash * 397L ^ StableTextHash(snapshot.StatusMessage);
                hash = hash * 397L ^ (snapshot.Paused ? 1 : 0);
                hash = hash * 397L ^ (snapshot.DebugEditGateEnabled ? 1 : 0);
                hash = hash * 397L ^ (snapshot.DetailsExpanded ? 1 : 0);
                hash = hash * 397L ^ (controller?.TacticalPresentationRevision ?? 0L);
                hash = hash * 397L ^ (snapshot.LoopPlaybackActive ? 1 : 0);
                hash = hash * 397L ^ (snapshot.ReplayInProgress ? 1 : 0);
                hash = hash * 397L ^ (snapshot.HistoryPlaybackActive ? 1 : 0);
                hash = hash * 397L ^ (int)snapshot.FrameSelectionPhase;
                hash = hash * 397L ^ snapshot.FrameSelectionTransactionId;
                hash = hash * 397L ^ snapshot.ActiveRestoreTarget.Frame;
                hash = hash * 397L ^ snapshot.QueuedTarget.Frame;
                hash = hash * 397L ^ snapshot.CommittedFrameReceipt.CommitGeneration;
                hash = hash * 397L ^ snapshot.CommittedFrameReceipt.Frame;
                hash = hash * 397L ^ snapshot.CommittedFrameReceipt.ResolvedAgentEntityId;
                hash = hash * 397L ^ snapshot.CommittedFrameReceipt.AuthorityHash;
                hash = hash * 397L ^ (snapshot.BranchRecomputeArmed ? 1 : 0);
                hash = hash * 397L ^ (snapshot.BranchConfirmationPending ? 1 : 0);
                hash = hash * 397L ^ snapshot.BranchConfirmationGeneration;
                hash = hash * 397L ^ snapshot.BranchRecomputeBaseFrame;
                hash = hash * 397L ^ snapshot.BranchRecomputeOriginalLatestFrame;
                hash = hash * 397L ^ snapshot.SelectedAgentIndex;
                CharacterTestRuntimeAiPlannerDevFrameBuffer frameBuffer = snapshot.FrameBuffer;
                if (frameBuffer != null)
                {
                    hash = hash * 397L ^ frameBuffer.Count;
                    hash = hash * 397L ^ frameBuffer.SelectedIndex;
                }
                hash = hash * 397L ^ controller.FrameLimit;
                hash = hash * 397L ^ controller.TimelineZoomLevel;
                hash = hash * 397L ^ (controller.TimelineExpanded ? 1 : 0);
                hash = hash * 397L ^ (controller.SessionContinuable ? 1 : 0);
                hash = hash * 397L ^ (controller.PreviewOverlayEnabled ? 1 : 0);
                hash = hash * 397L ^ (controller.HasSelectedTestStateCapture ? 1 : 0);
                hash = hash * 397L ^ (controller.StateCaptureEnabled ? 1 : 0);
                hash = hash * 397L ^ StableTextHash(controller.FrameSyncStatusText);
                hash = hash * 397L ^ StableTextHash(controller.PresentationStatusText);
                hash = hash * 397L ^ (controller.SetupSnapEnabled ? 1 : 0);
                hash = hash * 397L ^ controller.SelectedSetupTeamId;
                hash = hash * 397L ^ controller.SelectedSetupSlotIndex;
                hash = hash * 397L ^ (int)controller.AuthoringMode;
                hash = hash * 397L ^ (int)controller.CandidateState;
                hash = hash * 397L ^ (controller.CanEditBehaviorCardDraft ? 1 : 0);
                hash = hash * 397L ^ (controller.CanApplyCandidate ? 1 : 0);
                hash = hash * 397L ^ StableTextHash(controller.CandidateDiagnostic);
                hash = hash * 397L ^ StableTextHash(controller.DraftPersistenceStatus);
                hash = hash * 397L ^ (controller.DraftLastSaveWasAutomatic ? 1L : 0L);
                hash = hash * 397L ^ StableTextHash(controller.LastPublishStatus);
                hash = hash * 397L ^ StableTextHash(controller.LastPublishStableCardId);
                hash = hash * 397L ^ (controller.HasLastPublishAttempt ? 1 : 0);
                hash = hash * 397L ^ (controller.LastPublishSucceeded ? 1 : 0);
                CharacterTestRuntimeAiLabCandidateRevision candidate = controller.CandidateRevision;
                hash = hash * 397L ^ (candidate?.Revision ?? 0);
                hash = hash * 397L ^ (candidate?.AuthorityHash ?? 0L);
                hash = hash * 397L ^ StableTextHash(candidate?.GroupDraftId);
                hash = AppendSelectedCandidateCardIdentity(
                    hash,
                    candidate,
                    selectedWorkbenchEntryIndex);
                CharacterTestRuntimeAiLabCandidateRevision active = controller.ActiveRevision;
                hash = hash * 397L ^ (active?.Revision ?? 0);
                hash = hash * 397L ^ (active?.AuthorityHash ?? 0L);
                hash = hash * 397L ^ StableTextHash(active?.GroupDraftId);
                hash = AppendSelectedCandidateCardIdentity(
                    hash,
                    active,
                    selectedWorkbenchEntryIndex);
#if UNITY_EDITOR
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult roleResult =
                    controller.LastAuthoringWorkspaceRoleResult;
                hash = hash * 397L ^ (int)roleResult.Status;
                hash = hash * 397L ^ (int)roleResult.Role;
                hash = hash * 397L ^ StableTextHash(roleResult.Message);
                hash = hash * 397L ^ (controller.HasActiveAuthoringWorkspaceRole ? 1 : 0);
                hash = hash * 397L ^ (controller.HasAuthoringWorkspaceRoleOpenFailure ? 1 : 0);
#endif
                hash = hash * 397L ^ (controller.FutureAdvanceActive ? 1 : 0);
                hash = hash * 397L ^ controller.FutureAdvanceStartFrame;
                hash = hash * 397L ^ controller.FutureAdvanceTargetFrame;
                CharacterTestRuntimeAiPlannerDevTimeline timeline = snapshot.Timeline;
                if (timeline != null)
                {
                    hash = hash * 397L ^ timeline.ContentRevision;
                    hash = hash * 397L ^ timeline.Count;
                    hash = hash * 397L ^ timeline.SelectedFrame;
                    hash = hash * 397L ^ timeline.LatestFrame;
                    hash = hash * 397L ^ timeline.RetainedStartFrame;
                    hash = hash * 397L ^ (int)timeline.Mode;
                    hash = hash * 397L ^ timeline.PlaybackSpeedIndex;
                    hash = hash * 397L ^ (timeline.LoopEnabled ? 1 : 0);
                    hash = hash * 397L ^ timeline.LoopStartFrame;
                    hash = hash * 397L ^ timeline.LoopEndFrame;
                    hash = hash * 397L ^ timeline.EditBranchStartFrame;
                }
                CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame =
                    snapshot.CommittedFrameReceipt.IsValid
                        ? snapshot.CommittedFrameReceipt.Bundle.Record.FrameSnapshot
                        : snapshot.SelectedFrame;
                if (selectedFrame != null)
                {
                    hash = hash * 397L ^ selectedFrame.Frame;
                    hash = hash * 397L ^ RuntimeHelpers.GetHashCode(selectedFrame);
                }

                CharacterTestRuntimeAiLabDraftWorkspace workspace = controller.DraftWorkspace;
                hash = hash * 397L ^ (workspace != null ? 1 : 0);
                if (workspace != null)
                {
                    hash = hash * 397L ^ workspace.CurrentRevision;
                    hash = hash * 397L ^ workspace.LastSavedRevision;
                    hash = hash * 397L ^ (workspace.IsReadOnly ? 1 : 0);
                }
                if (workspace != null && workspace.TryGetGroupForSlot(
                        controller.SelectedSetupTeamId,
                        controller.SelectedSetupSlotIndex,
                        out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
                {
                    hash = hash * 397L ^ StableTextHash(group.GroupDraftId);
                    hash = hash * 397L ^ group.CurrentRevision;
                    if (selectedWorkbenchEntryIndex >= 0
                        && selectedWorkbenchEntryIndex < group.Entries.Count
                        && group.Entries[selectedWorkbenchEntryIndex] != null
                        && !string.IsNullOrWhiteSpace(
                            group.Entries[selectedWorkbenchEntryIndex].CardDraftId)
                        && workspace.Cards.TryGetValue(
                            group.Entries[selectedWorkbenchEntryIndex].CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                    {
                        hash = hash * 397L ^ StableTextHash(card.CardDraftId);
                        hash = hash * 397L ^ card.CurrentRevision;
                    }
                }

                CharacterTestRuntimeAiLabShellState shell = controller.ShellCoordinator?.CurrentState;
                if (shell != null)
                {
                    hash = hash * 397L ^ shell.Revision;
                    CharacterTestRuntimeAiLabBehaviorCardViewport viewport =
                        controller.BehaviorCardPresentationViewport;
                    hash = hash * 397L ^ viewport.HorizontalStartFrame;
                    hash = hash * 397L ^ viewport.HorizontalEndFrame;
                    hash = hash * 397L ^ viewport.VerticalScrollOffset.GetHashCode();
                }
                CharacterTestRuntimeAiLabBehaviorCardLibraryState libraryState = behaviorCardLibraryState
                    ?? CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;
                hash = hash * 397L ^ StableTextHash(libraryState.Query);
                hash = hash * 397L ^ (int)libraryState.Filter;
                hash = hash * 397L ^ StableTextHash(libraryState.FirstVisibleStableCardId);
                hash = hash * 397L ^ StableTextHash(libraryState.FocusedStableCardId);
                hash = hash * 397L ^ libraryState.ScrollOffsetY.GetHashCode();
                if (behaviorCardLibrary != null)
                {
                    hash = hash * 397L ^ RuntimeHelpers.GetHashCode(behaviorCardLibrary);
                    hash = hash * 397L ^ behaviorCardLibrary.DeckHash;
                    hash = hash * 397L ^ behaviorCardLibrary.Cards.Count;
                }
                return hash;
            }
        }

        private static long AppendSelectedCandidateCardIdentity(
            long hash,
            CharacterTestRuntimeAiLabCandidateRevision revision,
            int selectedEntryIndex)
        {
            int count = revision?.CardIdentities?.Count ?? 0;
            hash = hash * 397L ^ count;
            if (selectedEntryIndex < 0 || selectedEntryIndex >= count)
                return hash;
            CharacterTestRuntimeAiLabRevisionCardIdentity identity =
                revision.CardIdentities[selectedEntryIndex];
            hash = hash * 397L ^ StableTextHash(identity?.DraftEntryId);
            hash = hash * 397L ^ StableTextHash(identity?.CardDraftId);
            hash = hash * 397L ^ StableTextHash(identity?.PublishedStableId);
            hash = hash * 397L ^ (identity?.SourcePublishedRevision ?? 0);
            hash = hash * 397L ^ (identity?.SourcePublishedSemanticHash ?? 0L);
            hash = hash * 397L ^ (identity?.SourcePublishedContentHash ?? 0L);
            hash = hash * 397L ^ (identity?.CandidateSemanticHash ?? 0L);
            return hash;
        }

    }
}
