using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        internal static void FinalizeBehaviorCardWorkspaceRevision(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            long presentationCommandRevision = 0L)
        {
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                viewModel.BehaviorCardWorkspace;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = workspace.Timeline;
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtime =
                viewModel.BehaviorCardRuntimeControl;
            long revision = workspace.WorkspaceRevision;
            unchecked
            {
                revision = revision * 397L ^ presentationCommandRevision;
                if (runtime != null)
                {
                    revision = revision * 397L ^ (runtime.Available ? 1 : 0);
                    revision = revision * 397L ^ (runtime.Paused ? 1 : 0);
                    revision = revision * 397L ^ (runtime.ReplayInProgress ? 1 : 0);
                    revision = revision * 397L ^ (runtime.HistoryPlaybackActive ? 1 : 0);
                    revision = revision * 397L ^ (runtime.FutureAdvanceActive ? 1 : 0);
                    revision = revision * 397L ^ (runtime.BranchRecomputeArmed ? 1 : 0);
                    revision = revision * 397L ^ (runtime.BranchConfirmationPending ? 1 : 0);
                    revision = revision * 397L ^ runtime.BranchConfirmationGeneration;
                    revision = revision * 397L ^ (runtime.CanSeek ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanFirst ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanLatest ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanJump ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanStepBackward ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanStepForward ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanPlayPause ? 1 : 0);
                    revision = revision * 397L ^ (runtime.CanBranchRecompute ? 1 : 0);
                    revision = revision * 397L ^ (runtime.HasDifferentActiveCard ? 1 : 0);
                    revision = revision * 397L ^ runtime.SessionStartFrame;
                    revision = revision * 397L ^ runtime.RetainedStartFrame;
                    revision = revision * 397L ^ runtime.SelectedFrame;
                    revision = revision * 397L ^ runtime.LatestFrame;
                    revision = revision * 397L ^ runtime.SessionEndFrame;
                    revision = revision * 397L ^ runtime.FrameLimit;
                    revision = revision * 397L ^ runtime.FutureAdvanceTargetFrame;
                    revision = revision * 397L ^ runtime.PendingTargetFrame;
                    revision = revision * 397L ^ runtime.QueuedTargetFrame;
                    revision = revision * 397L ^ runtime.CommitGeneration;
                    revision = revision * 397L ^ runtime.AuthorityHash;
                    revision = revision * 397L ^ (int)runtime.SelectionPhase;
                    revision = revision * 397L ^ StableTextHash(runtime.FirstDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.FirstDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.BackwardDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.BackwardDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.ForwardDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.ForwardDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.LatestDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.LatestDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.PlayDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.PlayDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.JumpDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.JumpDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.BranchDisabledReasonCode);
                    revision = revision * 397L ^ StableTextHash(runtime.BranchDisabledReasonText);
                    revision = revision * 397L ^ StableTextHash(runtime.PlayPauseLabel);
                    revision = revision * 397L ^ StableTextHash(runtime.FrameInputText);
                    revision = revision * 397L ^ StableTextHash(runtime.FrameSummaryText);
                    revision = revision * 397L ^ StableTextHash(runtime.SeekStatusText);
                    revision = revision * 397L ^ StableTextHash(runtime.BranchActionLabel);
                    revision = revision * 397L ^ StableTextHash(runtime.BranchRiskText);
                    revision = revision * 397L ^ StableTextHash(runtime.ActiveCardName);
                }
                revision = revision * 397L ^ StableTextHash(workspace.Library.Query);
                revision = revision * 397L ^ (int)workspace.Library.Filter;
                revision = revision * 397L ^ workspace.Library.FirstVisibleIndex;
                revision = revision * 397L ^ workspace.Library.FocusedIndex;
                revision = revision * 397L ^ timeline.HorizontalStartFrame;
                revision = revision * 397L ^ timeline.HorizontalEndFrame;
                revision = revision * 397L ^ BitConverter.SingleToInt32Bits(timeline.ZoomLevel);
                revision = revision * 397L ^ BitConverter.SingleToInt32Bits(timeline.VerticalScrollOffset);
                revision = revision * 397L ^ (timeline.FollowCurrent ? 1 : 0);
                revision = revision * 397L ^ (int)timeline.SelectedElementKind;
                revision = revision * 397L ^ timeline.SelectedElementIndex;
                revision = revision * 397L ^ (int)timeline.Presentation.SelectedRecipeEventKind;
                revision = revision * 397L ^ StableTextHash(
                    timeline.Presentation.SelectedRecipeEventStableKey);
                revision = revision * 397L ^ (int)timeline.Presentation.RecipeInspectorTargetKind;
                revision = revision * 397L ^ StableTextHash(
                    timeline.Presentation.SelectedRecipeTimelineId);
                revision = revision * 397L ^ StableTextHash(
                    timeline.Presentation.SelectedRecipeEventId);
                revision = revision * 397L ^ (timeline.Available ? 1 : 0);
                revision = revision * 397L ^ (timeline.CanEdit ? 1 : 0);
                revision = revision * 397L ^ (timeline.HasBlockingDiagnostics ? 1 : 0);
                revision = revision * 397L ^ (timeline.RequiresDetectorWindowCanonicalization ? 1 : 0);
                revision = revision * 397L ^ timeline.DurationFrames;
                revision = revision * 397L ^ StableTextHash(timeline.CardTitleText);
                revision = revision * 397L ^ StableTextHash(timeline.SemanticHashText);
                revision = revision * 397L ^ StableTextHash(timeline.SummaryText);
                revision = revision * 397L ^ StableTextHash(timeline.DiagnosticsText);
                revision = revision * 397L ^ StableTextHash(timeline.RuntimeStatusText);
                revision = revision * 397L ^ StableTextHash(timeline.ValidationBadgeText);
                revision = revision * 397L ^ StableTextHash(timeline.CommandFeedbackText);
                for (int i = 0; i < timeline.StageCount; i++)
                {
                    CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[i];
                    revision = revision * 397L ^ StableTextHash(stage.StageId);
                    revision = revision * 397L ^ stage.StartFrame;
                    revision = revision * 397L ^ stage.DurationFrames;
                    revision = revision * 397L ^ StableTextHash(stage.DetectorSummaryText);
                    revision = revision * 397L ^ StableTextHash(stage.TransitionSummaryText);
                    revision = revision * 397L ^ stage.ActualDurationFrames;
                    revision = revision * 397L ^ stage.ActionDurationFrames;
                    revision = revision * 397L ^ stage.ActionProgressFrames;
                    revision = revision * 397L ^ stage.ActionRuntimeState;
                    revision = revision * 397L ^ (stage.ActionProgressIndeterminate ? 1 : 0);
                    revision = revision * 397L ^ (stage.ActionProgressOverrun ? 1 : 0);
                    revision = revision * 397L ^ StableTextHash(stage.RuntimeStatusText);
                    revision = revision * 397L ^ (stage.Current ? 1 : 0);
                    revision = revision * 397L ^ (stage.ActionRunning ? 1 : 0);
                }
                for (int i = 0; i < timeline.DetectorCount; i++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[i];
                    revision = revision * 397L ^ StableTextHash(detector.DetectorId);
                    revision = revision * 397L ^ detector.Priority;
                    revision = revision * 397L ^ detector.ConditionCount;
                    revision = revision * 397L ^ StableTextHash(detector.OwningStageId);
                    revision = revision * 397L ^ (detector.Triggered ? 1 : 0);
                    revision = revision * 397L ^ (detector.Armed ? 1 : 0);
                    revision = revision * 397L ^ detector.RuntimeElapsedFrames;
                    for (int conditionIndex = 0;
                         conditionIndex < detector.ConditionCount && conditionIndex < detector.Conditions.Length;
                         conditionIndex++)
                    {
                        CharacterTestRuntimeAiLabLogicConditionViewModel condition = detector.Conditions[conditionIndex];
                        revision = revision * 397L ^ StableTextHash(condition.ConditionId);
                        revision = revision * 397L ^ (int)condition.Fact;
                        revision = revision * 397L ^ (int)condition.Operator;
                        revision = revision * 397L ^ BitConverter.SingleToInt32Bits(condition.Value);
                        revision = revision * 397L ^ BitConverter.SingleToInt32Bits(condition.MaxValue);
                        revision = revision * 397L ^ condition.AbilityId;
                        revision = revision * 397L ^ StableTextHash(condition.CharacterActionId);
                    }
                }
                for (int i = 0; i < timeline.TransitionCount; i++)
                {
                    CharacterTestRuntimeAiLabLogicTransitionViewModel transition = timeline.Transitions[i];
                    revision = revision * 397L ^ StableTextHash(transition.TransitionId);
                    revision = revision * 397L ^ transition.Priority;
                    revision = revision * 397L ^ (int)transition.Policy;
                    revision = revision * 397L ^ StableTextHash(transition.SourceStageId);
                    revision = revision * 397L ^ StableTextHash(transition.TargetStageId);
                    revision = revision * 397L ^ StableTextHash(transition.EndReason);
                    revision = revision * 397L ^ (transition.Pending ? 1 : 0);
                    revision = revision * 397L ^ (transition.Committed ? 1 : 0);
                    revision = revision * 397L ^ transition.RuntimeFrame;
                }
            }
            workspace.WorkspaceRevision = revision;
        }

        private static string BuildLibraryCardStatus(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            bool currentEdit,
            bool runtimeActive)
        {
            if (runtimeActive)
                return currentEdit ? "运行中 · 当前编辑" : "运行中";
            if (currentEdit)
                return card.IsInstalled ? "当前编辑 · 已装配" : "当前编辑 · Draft";
            if (card.HasNewerRevision)
                return "存在更新 r" + card.PinnedRevision + " → r" + card.Revision;
            if (card.IsInstalled)
                return "已装配";
            if (card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible)
                return "不兼容";
            if (card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable)
                return "不可用";
            return card.IsUnlocked ? "已解锁" : "未解锁";
        }

        private static string BuildLibraryBlockReason(CharacterTestRuntimeAiPlannerPlayerDeckCardView card)
        {
            switch (card.BlockReason)
            {
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Unavailable:
                    return "当前目录缺少这张卡";
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Incompatible:
                    return "没有兼容的 Published revision";
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.NotUnlocked:
                    return "尚未解锁";
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.SessionLocked:
                    return "会话已锁定";
                default:
                    return string.Empty;
            }
        }

        private static string JoinLibraryTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return "无标签";
            var builder = new StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                if (i > 0)
                    builder.Append(" · ");
                builder.Append(tags[i]);
            }
            return builder.ToString();
        }
    }
}
