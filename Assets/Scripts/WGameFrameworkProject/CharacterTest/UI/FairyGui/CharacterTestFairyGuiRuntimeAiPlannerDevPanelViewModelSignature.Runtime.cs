using System;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature
    {
        private static void AppendAuthoringUxStatusSignature(
            StringBuilder builder,
            CharacterTestRuntimeAiLabAuthoringUxStatusViewModel status)
        {
            builder.Append(status != null ? (int)status.BehaviorGroupPublishState : 0).Append('|')
                .Append(status != null ? (int)status.BehaviorCardPublishState : 0).Append('|')
                .Append(status != null ? (int)status.DraftSaveState : 0).Append('|')
                .Append(status != null ? (int)status.TestSourceState : 0).Append('|')
                .Append(status != null && status.BehaviorGroupHasValidationProblems ? '1' : '0')
                .Append(status != null && status.BehaviorCardHasValidationProblems ? '1' : '0')
                .Append('|');
            AppendToken(builder, status?.BehaviorGroupPublishStatusText);
            AppendToken(builder, status?.BehaviorCardPublishStatusText);
            AppendToken(builder, status?.DraftSaveStatusText);
            AppendToken(builder, status?.TestSourceStatusText);
            AppendToken(builder, status?.BehaviorGroupValidationStatusText);
            AppendToken(builder, status?.BehaviorCardValidationStatusText);
            AppendToken(builder, status?.AdvancedDiagnosticText);
        }

        private static void AppendBehaviorCardWorkspaceSignature(
            StringBuilder builder,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                viewModel.BehaviorCardWorkspace;
            builder.Append(workspace.WorkspaceRevision).Append('|');
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library = workspace.Library;
            builder.Append(library.Available ? '1' : '0')
                .Append((int)library.Filter).Append('|')
                .Append(library.FirstVisibleIndex).Append('|')
                .Append(library.VisibleCapacity).Append('|')
                .Append(library.FocusedIndex).Append('|')
                .Append(library.ActiveCount).Append('|');
            AppendToken(builder, library.Query);
            AppendToken(builder, library.HeaderText);
            AppendToken(builder, library.SummaryText);
            AppendToken(builder, library.EmptyText);
            for (int i = 0; i < library.Cards.Length; i++)
            {
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card = library.Cards[i];
                builder.Append(card.Available ? '1' : '0')
                    .Append(card.Installed ? '1' : '0')
                    .Append(card.CurrentEdit ? '1' : '0')
                    .Append(card.RuntimeActive ? '1' : '0')
                    .Append(card.Draft ? '1' : '0')
                    .Append(card.DisplayNameEditable ? '1' : '0')
                    .Append(card.HasUnsavedMetadataChanges ? '1' : '0')
                    .Append(card.ResponsibilityEditable ? '1' : '0')
                    .Append(card.EntryIndex).Append('|')
                    .Append(card.PublishedRevision).Append('|')
                    .Append((int)card.PublishState).Append('|')
                    .Append(card.ResponsibilityCompatibilityMask).Append('|')
                    .Append(card.ReferencingGroupCount).Append('|')
                    .Append(card.ReferenceCount).Append('|');
                AppendToken(builder, card.StableCardId);
                AppendToken(builder, card.Title);
                AppendToken(builder, card.Description);
                AppendToken(builder, card.UseText);
                AppendToken(builder, card.RiskText);
                AppendToken(builder, card.TagsText);
                AppendToken(builder, card.CostText);
                AppendToken(builder, card.RevisionText);
                AppendToken(builder, card.StatusText);
                AppendToken(builder, card.BlockReasonText);
                AppendToken(builder, card.ReferenceSummaryText);
                for (int responsibilityIndex = 0;
                     card.ResponsibilityKinds != null
                     && responsibilityIndex < card.ResponsibilityKinds.Length;
                     responsibilityIndex++)
                {
                    builder.Append((int)card.ResponsibilityKinds[responsibilityIndex]).Append(',');
                }
                builder.Append('|');
            }

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = workspace.Timeline;
            builder.Append(timeline.Available ? '1' : '0')
                .Append(timeline.CanEdit ? '1' : '0')
                .Append(timeline.CanEditCardDisplayName ? '1' : '0')
                .Append(timeline.CanEditResponsibilities ? '1' : '0')
                .Append(timeline.EntryIndex).Append('|')
                .Append(timeline.ResponsibilityCompatibilityMask).Append('|')
                .Append(timeline.HasBlockingDiagnostics ? '1' : '0')
                .Append(timeline.RequiresDetectorWindowCanonicalization ? '1' : '0')
                .Append((int)timeline.SelectedElementKind).Append('|')
                .Append(timeline.SelectedElementIndex).Append('|')
                .Append((int)timeline.Presentation.SelectedRecipeEventKind).Append('|')
                .Append((int)timeline.Presentation.RecipeInspectorTargetKind).Append('|')
                .Append(timeline.DurationFrames).Append('|')
                .Append(timeline.RuntimeFrame).Append('|')
                .Append(timeline.HorizontalStartFrame).Append('|')
                .Append(timeline.HorizontalEndFrame).Append('|')
                .Append(timeline.ZoomLevel.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(timeline.VerticalScrollOffset.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(timeline.FollowCurrent ? '1' : '0').Append('|');
            AppendToken(builder, timeline.Presentation.SelectedRecipeEventStableKey);
            AppendToken(builder, timeline.Presentation.SelectedRecipeTimelineId);
            AppendToken(builder, timeline.Presentation.SelectedRecipeEventId);
            AppendToken(builder, timeline.CardTitleText);
            AppendToken(builder, timeline.SemanticHashText);
            AppendToken(builder, timeline.SummaryText);
            AppendToken(builder, timeline.DiagnosticsText);
            AppendToken(builder, timeline.RuntimeStatusText);
            AppendToken(builder, timeline.ValidationBadgeText);
            for (int responsibilityIndex = 0;
                 timeline.ResponsibilityKinds != null
                 && responsibilityIndex < timeline.ResponsibilityKinds.Length;
                 responsibilityIndex++)
            {
                builder.Append((int)timeline.ResponsibilityKinds[responsibilityIndex]).Append(',');
            }
            builder.Append('|');
            for (int i = 0; i < timeline.Stages.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[i];
                if (stage == null)
                {
                    builder.Append("null-stage|");
                    continue;
                }
                builder.Append(stage.Index).Append('|').Append(stage.Column).Append('|')
                    .Append(stage.StartFrame).Append('|').Append(stage.DurationFrames).Append('|')
                    .Append(stage.ActualDurationFrames).Append('|')
                    .Append(stage.ActionDurationFrames).Append('|')
                    .Append(stage.ActionProgressFrames).Append('|')
                    .Append(stage.ActionInstanceId).Append('|')
                    .Append(stage.ActionRuntimeState).Append('|')
                    .Append(stage.ActionProgressIndeterminate ? '1' : '0')
                    .Append(stage.ActionProgressOverrun ? '1' : '0')
                    .Append(stage.Selected ? '1' : '0').Append(stage.Current ? '1' : '0')
                    .Append(stage.ActionRunning ? '1' : '0').Append(stage.ActionConflict ? '1' : '0');
                AppendToken(builder, stage.StageId);
                AppendToken(builder, stage.Title);
                AppendToken(builder, stage.CharacterActionDisplayName);
                AppendToken(builder, stage.TimeText);
                AppendToken(builder, stage.ActionText);
                AppendToken(builder, stage.DetectorSummaryText);
                AppendToken(builder, stage.TransitionSummaryText);
                AppendToken(builder, stage.RuntimeStatusText);
            }
            for (int i = 0; i < timeline.Detectors.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[i];
                builder.Append(detector.Index).Append('|').Append(detector.Priority).Append('|')
                    .Append(detector.StageIndex).Append('|').Append(detector.StartFrame).Append('|')
                    .Append(detector.DurationFrames).Append('|').Append(detector.LinkedTransitionCount).Append('|')
                    .Append(detector.ConditionCount).Append('|').Append((int)detector.Scope).Append('|')
                    .Append((int)detector.Trigger).Append('|').Append((int)detector.WindowEndMode).Append('|')
                    .Append(detector.Selected ? '1' : '0').Append(detector.Active ? '1' : '0')
                    .Append(detector.Armed ? '1' : '0').Append(detector.Triggered ? '1' : '0')
                    .Append(detector.RuntimeElapsedFrames).Append('|');
                AppendToken(builder, detector.DetectorId);
                AppendToken(builder, detector.OwningStageId);
                AppendToken(builder, detector.Title);
                AppendToken(builder, detector.FactText);
                AppendToken(builder, detector.ScopeText);
                AppendToken(builder, detector.StatusText);
                for (int conditionIndex = 0;
                     conditionIndex < detector.ConditionCount && conditionIndex < detector.Conditions.Length;
                     conditionIndex++)
                {
                    CharacterTestRuntimeAiLabLogicConditionViewModel condition = detector.Conditions[conditionIndex];
                    builder.Append((int)condition.Fact).Append('|').Append((int)condition.Operator).Append('|')
                        .Append(condition.BoolValue ? '1' : '0').Append('|')
                        .Append(condition.Value.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(condition.MaxValue.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(condition.AbilityId).Append('|');
                    AppendToken(builder, condition.ConditionId);
                    AppendToken(builder, condition.CharacterActionId);
                }
            }
            for (int i = 0; i < timeline.Transitions.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = timeline.Transitions[i];
                builder.Append(transition.Index).Append('|').Append(transition.SourceStageIndex).Append('|')
                    .Append(transition.TargetStageIndex).Append('|').Append(transition.Priority).Append('|')
                    .Append((int)transition.Policy).Append('|')
                    .Append(transition.RuntimeFrame).Append('|')
                    .Append(transition.Selected ? '1' : '0').Append(transition.DetectorHit ? '1' : '0')
                    .Append(transition.Pending ? '1' : '0')
                    .Append(transition.Committed ? '1' : '0')
                    .Append(transition.PreviousCommitted ? '1' : '0')
                    .Append(transition.IsBackEdge ? '1' : '0');
                AppendToken(builder, transition.TransitionId);
                AppendToken(builder, transition.SourceStageId);
                AppendToken(builder, transition.TargetStageId);
                AppendToken(builder, transition.Label);
                AppendToken(builder, transition.EndReason);
                AppendToken(builder, transition.PolicyText);
                AppendToken(builder, transition.TargetText);
                AppendToken(builder, transition.StatusText);
            }
            AppendBehaviorCardRuntimeSignature(builder, viewModel.BehaviorCardRuntimeControl);
            AppendIdentityProjection(builder, viewModel.BehaviorCardIdentity);
        }

        private static void AppendBehaviorCardRuntimeSignature(
            StringBuilder builder,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtimeControl)
        {
            if (runtimeControl == null)
                return;
            builder.Append(runtimeControl.Available ? '1' : '0')
                .Append(runtimeControl.Paused ? '1' : '0')
                .Append(runtimeControl.ReplayInProgress ? '1' : '0')
                .Append(runtimeControl.HistoryPlaybackActive ? '1' : '0')
                .Append(runtimeControl.FutureAdvanceActive ? '1' : '0')
                .Append(runtimeControl.BranchRecomputeArmed ? '1' : '0')
                .Append(runtimeControl.BranchConfirmationPending ? '1' : '0')
                .Append(runtimeControl.BranchConfirmationGeneration).Append('|')
                .Append(runtimeControl.SelectedFrame).Append('|')
                .Append(runtimeControl.LatestFrame).Append('|')
                .Append(runtimeControl.FrameLimit).Append('|');
            AppendBehaviorCardFrameSelectionSignature(builder, runtimeControl);
            AppendToken(builder, runtimeControl.PlayPauseLabel);
            AppendToken(builder, runtimeControl.FrameInputText);
            AppendToken(builder, runtimeControl.FrameSummaryText);
            AppendToken(builder, runtimeControl.SeekStatusText);
            AppendToken(builder, runtimeControl.BranchRiskText);
            AppendToken(builder, runtimeControl.ActiveCardName);
        }

        private static void AppendBehaviorCardFrameSelectionSignature(
            StringBuilder builder,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtimeControl)
        {
            builder.Append((int)runtimeControl.SelectionPhase).Append('|')
                .Append(runtimeControl.CommitGeneration).Append('|')
                .Append(runtimeControl.AuthorityHash).Append('|')
                .Append(runtimeControl.PendingTargetFrame).Append('|')
                .Append(runtimeControl.QueuedTargetFrame).Append('|')
                .Append(runtimeControl.BranchConfirmationPending ? '1' : '0')
                .Append(runtimeControl.BranchConfirmationGeneration).Append('|')
                .Append(runtimeControl.CanFirst ? '1' : '0')
                .Append(runtimeControl.CanLatest ? '1' : '0')
                .Append(runtimeControl.CanJump ? '1' : '0').Append('|');
            AppendToken(builder, runtimeControl.FirstDisabledReasonCode);
            AppendToken(builder, runtimeControl.FirstDisabledReasonText);
            AppendToken(builder, runtimeControl.BackwardDisabledReasonCode);
            AppendToken(builder, runtimeControl.BackwardDisabledReasonText);
            AppendToken(builder, runtimeControl.ForwardDisabledReasonCode);
            AppendToken(builder, runtimeControl.ForwardDisabledReasonText);
            AppendToken(builder, runtimeControl.LatestDisabledReasonCode);
            AppendToken(builder, runtimeControl.LatestDisabledReasonText);
            AppendToken(builder, runtimeControl.PlayDisabledReasonCode);
            AppendToken(builder, runtimeControl.PlayDisabledReasonText);
            AppendToken(builder, runtimeControl.JumpDisabledReasonCode);
            AppendToken(builder, runtimeControl.JumpDisabledReasonText);
            AppendToken(builder, runtimeControl.BranchDisabledReasonCode);
            AppendToken(builder, runtimeControl.BranchDisabledReasonText);
        }

        private static void AppendFactsSignature(
            StringBuilder builder,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            AppendToken(builder, viewModel.FrameSummary);
            AppendToken(builder, viewModel.FactsText);
            AppendToken(builder, viewModel.PathText);
            AppendToken(builder, viewModel.UtilityText);
            AppendToken(builder, viewModel.CommandText);
            AppendFactsWorkspaceSignature(builder, viewModel.Facts);
        }

        private static void AppendRuntimeTimelineSignature(
            StringBuilder builder,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            AppendToken(builder, viewModel.FrameSummary);
            AppendToken(builder, viewModel.TimelineText);
            AppendToken(builder, viewModel.TimelineStatusText);
            AppendToken(builder, viewModel.TimelineSelectedVisualStableKey);
            builder.Append(viewModel.TimelineStartFrame).Append('|')
                .Append(viewModel.TimelineEndFrame).Append('|')
                .Append(viewModel.TimelineSelectedFrame).Append('|')
                .Append(viewModel.TimelineRetainedStartFrame).Append('|')
                .Append(viewModel.TimelineFutureAdvanceTargetFrame).Append('|')
                .Append(viewModel.TimelineFutureAdvanceActive ? '1' : '0')
                .Append(viewModel.BranchRecomputeSelected ? '1' : '0')
                .Append(viewModel.CanToggleBranchRecompute ? '1' : '0');
            AppendToken(builder, viewModel.BranchRecomputeLabel);
            AppendToken(builder, viewModel.BranchRecomputeRiskText);
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel runtimeFrame =
                viewModel.RuntimeFrame;
            builder.Append(runtimeFrame?.AgentEntityId ?? 0).Append('|')
                .Append(runtimeFrame?.SelectedFrame ?? 0).Append('|')
                .Append(runtimeFrame?.LatestFrame ?? 0).Append('|')
                .Append(runtimeFrame?.FrameLimit ?? 0).Append('|');
            AppendToken(builder, runtimeFrame?.AgentSlotLabel);
            AppendToken(builder, runtimeFrame?.AgentDisplayName);
            CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel[] agentOptions =
                runtimeFrame?.AgentOptions;
            for (int i = 0; agentOptions != null && i < agentOptions.Length; i++)
            {
                CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel option = agentOptions[i];
                builder.Append(option?.AgentIndex ?? -1).Append('|')
                    .Append(option?.EntityId ?? 0).Append('|')
                    .Append(option?.TeamId ?? 0).Append('|')
                    .Append(option?.SlotIndex ?? -1).Append('|')
                    .Append(option != null && option.Selected ? '1' : '0').Append('|');
                AppendToken(builder, option?.SlotLabel);
                AppendToken(builder, option?.DisplayName);
            }
            CharacterTestRuntimeAiLabTimelineProjection projection = viewModel.TimelineProjection;
            if (projection == null)
                return;
            builder.Append(projection.ContentRevision).Append('|')
                .Append((int)projection.Validity).Append('|')
                .Append(projection.Domain.SessionStartFrame).Append('|')
                .Append(projection.Domain.RetainedStartFrame).Append('|')
                .Append(projection.Domain.LatestFrame).Append('|')
                .Append(projection.Domain.SelectedFrame).Append('|')
                .Append(projection.Domain.FrameLimit).Append('|')
                .Append(projection.Viewport.StartFrame).Append('|')
                .Append(projection.Viewport.EndFrame).Append('|')
                .Append(projection.Viewport.ZoomLevel).Append('|');
            AppendToken(builder, projection.StatusMessage);
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track = projection.Tracks[trackIndex];
                builder.Append((int)track.Kind).Append('|')
                    .Append(track.Available ? '1' : '0');
                AppendToken(builder, track.Label);
                AppendToken(builder, track.UnavailableMessage);
                for (int itemIndex = 0; itemIndex < track.Items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = track.Items[itemIndex];
                    AppendToken(builder, item.StableKey);
                    builder.Append(item.StartFrame).Append('|')
                        .Append(item.EndFrame).Append('|')
                        .Append((int)item.ItemKind).Append('|')
                        .Append((int)item.Severity).Append('|');
                    AppendToken(builder, item.Label);
                }
            }
        }

    }
}
