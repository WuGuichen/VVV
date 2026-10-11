using System;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(2048);
            CharacterTestRuntimeAiLabWorkspaceMode workspace = viewModel.Workspace != null
                ? viewModel.Workspace.ActiveWorkspace
                : viewModel.WorkspaceMode;
            CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab = viewModel.Workspace != null
                ? viewModel.Workspace.ActiveDiagnosticsTab
                : viewModel.DiagnosticsTab;
            CharacterTestRuntimeAiLabTimelineMode timelineMode = viewModel.Workspace != null
                ? viewModel.Workspace.ActiveTimelineMode
                : viewModel.TimelineMode;
            builder.Append((int)workspace).Append('|')
                .Append((int)diagnosticsTab).Append('|')
                .Append((int)timelineMode).Append('|')
                .Append((int)viewModel.PresentationContext).Append('|');
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.PhaseText);
            AppendToken(builder, viewModel.TimelineModeDisabledReasonText);
            AppendAuthoringUxStatusSignature(builder, viewModel.AuthoringStatus);
            builder.Append(viewModel.ShowAuthoringContextBar ? '1' : '0')
                .Append(viewModel.ShowAuthoringWorkspaceRoleControl ? '1' : '0')
                .Append(viewModel.ShowAuthoringWriteActions ? '1' : '0')
                .Append(viewModel.CanTestCurrentChanges ? '1' : '0')
                .Append(viewModel.CanPublishCurrentChanges ? '1' : '0');
            AppendToken(builder, viewModel.AuthoringWorkspaceRoleText);
            AppendToken(builder, viewModel.AuthoringGroupContextText);
            AppendToken(builder, viewModel.AuthoringCardContextText);
            AppendToken(builder, viewModel.AuthoringPublishedStateText);
            AppendToken(builder, viewModel.AuthoringProblemText);
            AppendToken(builder, viewModel.TestCurrentChangesDisabledReasonText);
            AppendToken(builder, viewModel.PublishCurrentChangesDisabledReasonText);
            AppendToken(builder, viewModel.PublishCurrentChangesLabel);
            switch (workspace)
            {
                case CharacterTestRuntimeAiLabWorkspaceMode.Setup:
                    AppendSetupSignature(builder, viewModel);
                    break;
                case CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup:
                    AppendWorkbenchSignature(builder, viewModel, includeCardDetails: false);
                    break;
                case CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard:
                    AppendWorkbenchSignature(builder, viewModel, includeCardDetails: false);
                    AppendBehaviorCardWorkspaceSignature(builder, viewModel);
                    break;
                case CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics:
                    if (diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Facts)
                        AppendFactsSignature(builder, viewModel);
                    else if (diagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Replay)
                    {
                        AppendReplaySignature(builder, viewModel);
                        AppendRuntimeTimelineSignature(builder, viewModel);
                    }
                    else if (viewModel.ShowBehaviorPlan || viewModel.ShowBehaviorLogic)
                        AppendBehaviorCardWorkspaceSignature(builder, viewModel);
                    else
                        AppendRuntimeTimelineSignature(builder, viewModel);
                    break;
            }

            return builder.ToString();
        }

        private static string CreateLegacy(CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(2048);
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.PhaseText);
            AppendToken(builder, viewModel.FrameSummary);
            AppendToken(builder, viewModel.PreviewHeader);
            AppendToken(builder, viewModel.PreviewInstructionsText);
            AppendToken(builder, viewModel.PreviewStatusText);
            AppendToken(builder, viewModel.SetupHeader);
            AppendToken(builder, viewModel.SetupInstructionsText);
            AppendToken(builder, viewModel.SetupSelectedSlotText);
            AppendToken(builder, viewModel.SetupValidationText);
            AppendToken(builder, viewModel.SetupCharacterText);
            AppendToken(builder, viewModel.SetupBehaviorGroupText);
            AppendToken(builder, viewModel.SetupPositionText);
            AppendToken(builder, viewModel.SetupTopValidationText);
            AppendToken(builder, viewModel.SetupTeam1CountText);
            AppendToken(builder, viewModel.SetupTeam1ValidationText);
            AppendToken(builder, viewModel.SetupTeam2CountText);
            AppendToken(builder, viewModel.SetupTeam2ValidationText);
            AppendToken(builder, viewModel.SetupAgentBadgeText);
            AppendToken(builder, viewModel.SetupDistanceText);
            AppendToken(builder, viewModel.SetupReadyTitleText);
            AppendToken(builder, viewModel.SetupReadyDescriptionText);
            AppendToken(builder, viewModel.SetupSceneText);
            AppendToken(builder, viewModel.SetupAgentCountText);
            AppendToken(builder, viewModel.SetupBehaviorGroupCountText);
            AppendToken(builder, viewModel.SetupFrameLimitStatusText);
            AppendToken(builder, viewModel.TimelineText);
            AppendToken(builder, viewModel.AgentListText);
            AppendToken(builder, viewModel.SelectedAgentText);
            AppendToken(builder, viewModel.FactsText);
            AppendToken(builder, viewModel.PathText);
            AppendToken(builder, viewModel.UtilityText);
            AppendToken(builder, viewModel.CommandText);
            AppendToken(builder, viewModel.DiagnosticsText);
            AppendToken(builder, viewModel.EditGateText);
            AppendToken(builder, viewModel.StateValidationText);
            AppendToken(builder, viewModel.WorkbenchTitleText);
            AppendToken(builder, viewModel.WorkbenchEntryCountText);
            AppendToken(builder, viewModel.WorkbenchSummaryText);
            AppendToken(builder, viewModel.WorkbenchDetailText);
            AppendToken(builder, viewModel.WorkbenchDiagnosticText);
            AppendToken(builder, viewModel.WorkbenchGroupIdentity);
            for (int i = 0; i < viewModel.WorkbenchResponsibilityCounts.Length; i++)
                builder.Append(viewModel.WorkbenchResponsibilityCounts[i]).Append('|');
            AppendToken(builder, viewModel.CandidatePreviewTitleText);
            AppendToken(builder, viewModel.CandidatePreviewBodyText);
            AppendToken(builder, viewModel.DraftStatusText);
            AppendToken(builder, viewModel.PlayerLegalityStatusText);
            AppendToken(builder, viewModel.PlayerLegalityReasonText);
            AppendToken(builder, viewModel.DraftHashText);
            AppendToken(builder, viewModel.ApplyDraftDisabledReasonText);
            AppendToken(builder, viewModel.PublishDraftStatusText);
            AppendToken(builder, viewModel.PublishDraftDisabledReasonText);
            AppendToken(builder, viewModel.SaveBehaviorGroupDisabledReasonText);
            AppendToken(builder, viewModel.TimelineRevisionTrackText);
            AppendToken(builder, viewModel.TimelineDecisionTrackText);
            AppendToken(builder, viewModel.TimelineActionTrackText);
            AppendToken(builder, viewModel.TimelineCombatTrackText);
            AppendToken(builder, viewModel.TimelineWarningTrackText);
            AppendToken(builder, viewModel.TimelineStatusText);
            AppendToken(builder, viewModel.TimelineSelectedVisualStableKey);
            AppendToken(builder, viewModel.TimelineSelectedItem?.StableKey);
            AppendToken(builder, viewModel.ToggleStateCaptureLabel);
            AppendToken(builder, viewModel.ValidateStateLabel);
            AppendToken(builder, viewModel.SyncRecordedStateLabel);
            AppendToken(builder, viewModel.PauseResumeLabel);
            AppendToken(builder, viewModel.BranchRecomputeLabel);
            AppendToken(builder, viewModel.BranchRecomputeRiskText);
            AppendToken(builder, viewModel.StepBackLabel);
            AppendToken(builder, viewModel.StepBack10Label);
            AppendToken(builder, viewModel.StepForward10Label);
            AppendToken(builder, viewModel.JumpFrameLabel);
            AppendToken(builder, viewModel.JumpFrameInputText);
            AppendToken(builder, viewModel.SpeedLabel);
            AppendToken(builder, viewModel.LoopStartLabel);
            AppendToken(builder, viewModel.LoopEndLabel);
            AppendToken(builder, viewModel.ToggleLoopLabel);
            AppendToken(builder, viewModel.ToggleSetupSnapLabel);
            AppendToken(builder, viewModel.MirrorSpawnsLabel);
            AppendToken(builder, viewModel.ResetSpawnDefaultsLabel);
            AppendToken(builder, viewModel.FitSetupViewLabel);
            AppendToken(builder, viewModel.ColdBootstrapVerifyLabel);
            AppendToken(builder, viewModel.ToggleSetupSlotLabel);
            for (int i = 0; i < viewModel.SetupSlotRows.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupSlotRowViewModel row = viewModel.SetupSlotRows[i];
                AppendToken(builder, row?.Label);
                builder.Append(row != null && row.Enabled ? '1' : '0')
                    .Append(row != null && row.Selected ? '1' : '0')
                    .Append(row != null && row.HasError ? '1' : '0')
                    .Append('|');
            }
            for (int i = 0; i < viewModel.SetupBehaviorGroupOptions.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option =
                    viewModel.SetupBehaviorGroupOptions[i];
                builder.Append(option != null ? option.DraftId : 0).Append('|')
                    .Append(option != null ? option.ArenaBehaviorGroupDraftId : 0).Append('|')
                    .Append(option != null && option.IsLocalDraft ? '1' : '0')
                    .Append(option != null && option.Available ? '1' : '0')
                    .Append(option != null && option.Selected ? '1' : '0')
                    .Append(option != null ? option.CardCount : 0)
                    .Append(option != null ? (int)option.PublishState : 0).Append('|')
                    .Append(option != null && option.HasValidationProblems ? '1' : '0')
                    .Append('|');
                AppendToken(builder, option?.DisplayName);
                AppendToken(builder, option?.GroupDraftId);
                AppendToken(builder, option?.MonogramText);
                AppendToken(builder, option?.UnavailableReasonText);
                AppendToken(builder, option?.PublishStatusText);
                AppendToken(builder, option?.ValidationStatusText);
            }
            for (int i = 0; i < viewModel.SetupCharacterOptions.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupCharacterOptionViewModel option =
                    viewModel.SetupCharacterOptions[i];
                builder.Append(option != null ? option.CharacterId : 0).Append('|')
                    .Append(option != null && option.Selected ? '1' : '0')
                    .Append('|');
                AppendToken(builder, option?.DisplayName);
                AppendToken(builder, option?.MonogramText);
            }
            for (int i = 0; i < viewModel.WorkbenchRows.Length; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = viewModel.WorkbenchRows[i];
                builder.Append(row != null ? row.EntryIndex : -1).Append('|')
                    .Append(row != null ? (int)row.RowKind : 0).Append('|')
                    .Append(row != null ? (int)row.PublishState : 0).Append('|')
                    .Append(row != null && row.HasValidationProblems ? '1' : '0').Append('|')
                    .Append(row != null ? (int)row.ResponsibilityKind : 0).Append('|')
                    .Append(row != null ? row.ResponsibilityTotalCount : 0).Append('|')
                    .Append(row != null ? row.ResponsibilityEnabledCount : 0).Append('|')
                    .Append(row != null && row.ResponsibilityExpanded ? '1' : '0')
                    .Append(row != null && row.ResponsibilityWarning ? '1' : '0')
                    .Append(row != null && row.Visible ? '1' : '0')
                    .Append(row != null && row.Selected ? '1' : '0')
                    .Append(row != null && row.Disabled ? '1' : '0')
                    .Append('|');
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities =
                    row?.ResponsibilityKinds
                    ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
                for (int responsibilityIndex = 0;
                    responsibilityIndex < responsibilities.Length;
                    responsibilityIndex++)
                {
                    builder.Append((int)responsibilities[responsibilityIndex]).Append(',');
                }
                builder.Append('|');
                AppendToken(builder, row?.CardDraftId);
                AppendToken(builder, row?.PublishedStableId);
                AppendToken(builder, row?.Label);
                AppendToken(builder, row?.TitleText);
                AppendToken(builder, row?.BadgeText);
                AppendToken(builder, row?.PublishStatusText);
                AppendToken(builder, row?.ValidationStatusText);
                AppendToken(builder, row?.TriggerText);
                AppendToken(builder, row?.ActionText);
                AppendToken(builder, row?.OutcomeText);
                AppendToken(builder, row?.PriorityText);
                AppendToken(builder, row?.ExecutionDetailText);
                AppendToken(builder, row?.ResponsibilitySummaryText);
                AppendToken(builder, row?.ResponsibilityColor);
                builder.Append(row != null ? (int)row.ExecutionState : 0).Append('|');
            }
            builder.Append(viewModel.WorkbenchRowCount).Append('|')
                .Append(viewModel.WorkbenchScrollOffset).Append('|')
                .Append(viewModel.WorkbenchMaxScrollOffset).Append('|');
            for (int i = 0; i < viewModel.WorkbenchLibraryRows.Length; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel row =
                    viewModel.WorkbenchLibraryRows[i];
                AppendToken(builder, row?.CardDraftId);
                AppendToken(builder, row?.DisplayName);
                AppendToken(builder, row?.CategoryText);
                AppendToken(builder, row?.StatusText);
                builder.Append(row != null && row.Installed ? '1' : '0')
                    .Append(row != null && row.Available ? '1' : '0')
                    .Append('|');
            }
            AppendToken(builder, viewModel.BehaviorCardRuntimeHeaderText);
            AppendToken(builder, viewModel.BehaviorCardRuntimeStateText);
            AppendToken(builder, viewModel.BehaviorCardExecutionSummaryText);
            AppendToken(builder, viewModel.BehaviorCardRecentEventsText);
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtimeControl =
                viewModel.BehaviorCardRuntimeControl;
            if (runtimeControl != null)
            {
                builder.Append(runtimeControl.Available ? '1' : '0')
                    .Append(runtimeControl.Paused ? '1' : '0')
                    .Append(runtimeControl.ReplayInProgress ? '1' : '0')
                    .Append(runtimeControl.HistoryPlaybackActive ? '1' : '0')
                    .Append(runtimeControl.FutureAdvanceActive ? '1' : '0')
                    .Append(runtimeControl.BranchRecomputeArmed ? '1' : '0')
                    .Append(runtimeControl.CanSeek ? '1' : '0')
                    .Append(runtimeControl.CanStepBackward ? '1' : '0')
                    .Append(runtimeControl.CanStepForward ? '1' : '0')
                    .Append(runtimeControl.CanPlayPause ? '1' : '0')
                    .Append(runtimeControl.CanBranchRecompute ? '1' : '0')
                    .Append(runtimeControl.HasDifferentActiveCard ? '1' : '0')
                    .Append('|')
                    .Append(runtimeControl.SessionStartFrame).Append('|')
                    .Append(runtimeControl.RetainedStartFrame).Append('|')
                    .Append(runtimeControl.SelectedFrame).Append('|')
                    .Append(runtimeControl.LatestFrame).Append('|')
                    .Append(runtimeControl.SessionEndFrame).Append('|')
                    .Append(runtimeControl.FrameLimit).Append('|')
                    .Append(runtimeControl.FutureAdvanceTargetFrame).Append('|');
                AppendBehaviorCardFrameSelectionSignature(builder, runtimeControl);
                AppendToken(builder, runtimeControl.PlayPauseLabel);
                AppendToken(builder, runtimeControl.FrameInputText);
                AppendToken(builder, runtimeControl.FrameSummaryText);
                AppendToken(builder, runtimeControl.SeekStatusText);
                AppendToken(builder, runtimeControl.BranchActionLabel);
                AppendToken(builder, runtimeControl.BranchRiskText);
                AppendToken(builder, runtimeControl.ActiveCardName);
            }
            AppendIdentityProjection(builder, viewModel.BehaviorCardIdentity);
            AppendBehaviorCardWorkspaceSignature(builder, viewModel);
            AppendFactsWorkspaceSignature(builder, viewModel.Facts);
            AppendToken(builder, viewModel.TimelineStartFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineEndFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineSelectedFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineRetainedStartFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineLoopStartFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineLoopEndFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineFutureAdvanceStartFrame.ToString(CultureInfo.InvariantCulture));
            AppendToken(builder, viewModel.TimelineFutureAdvanceTargetFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(viewModel.CanStart ? '1' : '0').Append('|')
                .Append(viewModel.CanPauseResume ? '1' : '0').Append('|')
                .Append(viewModel.CanStep ? '1' : '0').Append('|')
                .Append(viewModel.CanToggleBranchRecompute ? '1' : '0').Append('|')
                .Append(viewModel.BranchRecomputeSelected ? '1' : '0').Append('|')
                .Append(viewModel.CanStepBack ? '1' : '0').Append('|')
                .Append(viewModel.CanStep10 ? '1' : '0').Append('|')
                .Append(viewModel.CanJumpFrame ? '1' : '0').Append('|')
                .Append(viewModel.CanChangeSpeed ? '1' : '0').Append('|')
                .Append(viewModel.CanSetLoop ? '1' : '0').Append('|')
                .Append(viewModel.CanSetLoopRange ? '1' : '0').Append('|')
                .Append(viewModel.CanReset ? '1' : '0').Append('|')
                .Append(viewModel.CanSelectFrame ? '1' : '0').Append('|')
                .Append(viewModel.CanSelectAgent ? '1' : '0').Append('|')
                .Append(viewModel.CanToggleEditGate ? '1' : '0').Append('|')
                .Append(viewModel.CanTogglePreviewOverlay ? '1' : '0').Append('|')
                .Append(viewModel.CanToggleDetails ? '1' : '0').Append('|')
                .Append(viewModel.CanToggleStateCapture ? '1' : '0').Append('|')
                .Append(viewModel.CanValidateState ? '1' : '0').Append('|')
                .Append(viewModel.CanSyncRecordedState ? '1' : '0').Append('|')
                .Append(viewModel.StateCaptureSelected ? '1' : '0').Append('|')
                .Append(viewModel.CanColdBootstrapVerify ? '1' : '0').Append('|')
                .Append(viewModel.CanEditSetupPlacement ? '1' : '0').Append('|')
                .Append(viewModel.CanFitSetupView ? '1' : '0').Append('|')
                .Append(viewModel.SetupSnapSelected ? '1' : '0').Append('|')
                .Append(viewModel.EditGateSelected ? '1' : '0').Append('|')
                .Append(viewModel.PreviewOverlaySelected ? '1' : '0').Append('|')
                .Append(viewModel.DetailsExpanded ? '1' : '0').Append('|')
                .Append(viewModel.LoopSelected ? '1' : '0').Append('|')
                .Append((int)viewModel.PresentationContext).Append('|')
                .Append(viewModel.ShowSetupToolbar ? '1' : '0').Append('|')
                .Append(viewModel.ShowLiveToolbar ? '1' : '0').Append('|')
                .Append(viewModel.ShowPausedToolbar ? '1' : '0').Append('|')
                .Append(viewModel.ShowReviewState ? '1' : '0').Append('|')
                .Append(viewModel.ShowTimeline ? '1' : '0').Append('|')
                .Append(viewModel.ShowTimelineModeHost ? '1' : '0')
                .Append(viewModel.ShowBehaviorPlan ? '1' : '0')
                .Append(viewModel.ShowBehaviorLogic ? '1' : '0')
                .Append(viewModel.CanSwitchTimelineMode ? '1' : '0')
                .Append(viewModel.RuntimeTraceModeSelected ? '1' : '0')
                .Append(viewModel.BehaviorPlanModeSelected ? '1' : '0')
                .Append(viewModel.BehaviorLogicModeSelected ? '1' : '0')
                .Append(viewModel.RuntimeTraceExpanded ? '1' : '0')
                .Append(viewModel.BehaviorPlanExpanded ? '1' : '0')
                .Append('|')
                .Append(viewModel.TimelineExpanded ? '1' : '0').Append('|')
                .Append(viewModel.CanToggleTimelineExpanded ? '1' : '0').Append('|')
                .Append(viewModel.CanZoomTimeline ? '1' : '0').Append('|')
                .Append(viewModel.CanPanTimeline ? '1' : '0').Append('|')
                .Append(viewModel.TimelineHasLoopRange ? '1' : '0').Append('|')
                .Append(viewModel.TimelineFutureAdvanceActive ? '1' : '0').Append('|')
                .Append(viewModel.ShowAdvancedDiagnostics ? '1' : '0').Append('|')
                .Append(viewModel.ShowWorkbench ? '1' : '0').Append('|')
                .Append(viewModel.CanEditWorkbench ? '1' : '0').Append('|')
                .Append(viewModel.CanUndoDraft ? '1' : '0').Append('|')
                .Append(viewModel.CanSaveDraft ? '1' : '0').Append('|')
                .Append(viewModel.CanApplyDraft ? '1' : '0').Append('|')
                .Append(viewModel.CanPublishDraft ? '1' : '0').Append('|')
                .Append(viewModel.CanSaveBehaviorGroup ? '1' : '0').Append('|')
                .Append(viewModel.CanReloadProjectDraft ? '1' : '0').Append('|')
                .Append(viewModel.CanReapplyProjectDraft ? '1' : '0').Append('|')
                .Append(viewModel.CanOpenBehaviorGroup ? '1' : '0').Append('|')
                .Append(viewModel.CanCreateSetupBehaviorGroup ? '1' : '0').Append('|')
                .Append(viewModel.CanDeleteSetupBehaviorGroup ? '1' : '0').Append('|')
                .Append(viewModel.SetupBehaviorGroupDeleteConfirming ? '1' : '0').Append('|')
                .Append(viewModel.SetupBehaviorGroupDeleteConfirmingDraftId).Append('|')
                .Append(viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText).Append('|')
                .Append(viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText).Append('|')
                .Append(viewModel.ShowProjectCardMetadata ? '1' : '0').Append('|')
                .Append(viewModel.CanEditProjectCardMetadata ? '1' : '0').Append('|')
                .Append((int)viewModel.WorkspaceMode).Append('|')
                .Append((int)viewModel.DiagnosticsTab).Append('|')
                .Append((int)viewModel.TimelineMode).Append('|')
                .Append(viewModel.IsSelectedCardExpanded ? '1' : '0');
            return builder.ToString();
        }

        private static void AppendSetupSignature(
            StringBuilder builder,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            AppendToken(builder, viewModel.SetupHeader);
            AppendToken(builder, viewModel.SetupInstructionsText);
            AppendToken(builder, viewModel.SetupSelectedSlotText);
            AppendToken(builder, viewModel.SetupValidationText);
            AppendToken(builder, viewModel.SetupCharacterText);
            AppendToken(builder, viewModel.SetupBehaviorGroupText);
            AppendToken(builder, viewModel.SetupPositionText);
            AppendToken(builder, viewModel.SetupTopValidationText);
            AppendToken(builder, viewModel.SetupTeam1CountText);
            AppendToken(builder, viewModel.SetupTeam1ValidationText);
            AppendToken(builder, viewModel.SetupTeam2CountText);
            AppendToken(builder, viewModel.SetupTeam2ValidationText);
            AppendToken(builder, viewModel.SetupAgentBadgeText);
            AppendToken(builder, viewModel.SetupDistanceText);
            AppendToken(builder, viewModel.SetupReadyTitleText);
            AppendToken(builder, viewModel.SetupReadyDescriptionText);
            AppendToken(builder, viewModel.SetupSceneText);
            AppendToken(builder, viewModel.SetupAgentCountText);
            AppendToken(builder, viewModel.SetupBehaviorGroupCountText);
            AppendToken(builder, viewModel.SetupBehaviorGroupPickerCreateLabel);
            AppendToken(builder, viewModel.SetupBehaviorGroupPickerDeleteLabel);
            AppendToken(builder, viewModel.SetupBehaviorGroupPickerDeleteConfirmLabel);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorGroupDraftId);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorStableId);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorIdentityText);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorDisplayName);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorDescription);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorTagsText);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorDisabledReasonText);
            AppendToken(builder, viewModel.SetupBehaviorGroupEditorSaveLabel);
            builder.Append(viewModel.SetupBehaviorGroupEditorIsLocalDraft ? '1' : '0');
            AppendToken(builder, viewModel.SetupFrameLimitStatusText);
            builder.Append(viewModel.CanStart ? '1' : '0')
                .Append(viewModel.CanReset ? '1' : '0')
                .Append(viewModel.CanEditSetupPlacement ? '1' : '0')
                .Append(viewModel.CanFitSetupView ? '1' : '0')
                .Append(viewModel.SetupSnapSelected ? '1' : '0')
                .Append(viewModel.CanEditSetupBehaviorGroupBasicInfo ? '1' : '0')
                .Append(viewModel.SetupBehaviorGroupEditorSourceRevision).Append('|')
                .Append(viewModel.SetupBehaviorGroupEditorSourceContentHash).Append('|');
            CharacterTestRuntimeAiLabSetupSlotRowViewModel[] rows =
                viewModel.SetupSlotRows ?? Array.Empty<CharacterTestRuntimeAiLabSetupSlotRowViewModel>();
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupSlotRowViewModel row = rows[i];
                AppendToken(builder, row?.Label);
                builder.Append(row != null && row.Enabled ? '1' : '0')
                    .Append(row != null && row.Selected ? '1' : '0')
                    .Append(row != null && row.HasError ? '1' : '0');
            }
            CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel[] options =
                viewModel.SetupBehaviorGroupOptions
                ?? Array.Empty<CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel>();
            for (int i = 0; i < options.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option = options[i];
                builder.Append(option != null ? option.DraftId : 0).Append('|')
                    .Append(option != null ? option.ArenaBehaviorGroupDraftId : 0).Append('|')
                    .Append(option != null && option.IsLocalDraft ? '1' : '0')
                    .Append(option != null && option.Available ? '1' : '0')
                    .Append(option != null && option.Selected ? '1' : '0')
                    .Append(option != null ? option.CardCount : 0)
                    .Append(option != null ? (int)option.PublishState : 0).Append('|')
                    .Append(option != null && option.HasValidationProblems ? '1' : '0');
                AppendToken(builder, option?.DisplayName);
                AppendToken(builder, option?.GroupDraftId);
                AppendToken(builder, option?.MonogramText);
                AppendToken(builder, option?.UnavailableReasonText);
                AppendToken(builder, option?.PublishStatusText);
                AppendToken(builder, option?.ValidationStatusText);
            }
            CharacterTestRuntimeAiLabSetupCharacterOptionViewModel[] characters =
                viewModel.SetupCharacterOptions
                ?? Array.Empty<CharacterTestRuntimeAiLabSetupCharacterOptionViewModel>();
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupCharacterOptionViewModel option = characters[i];
                builder.Append(option != null ? option.CharacterId : 0).Append('|')
                    .Append(option != null && option.Selected ? '1' : '0');
                AppendToken(builder, option?.DisplayName);
                AppendToken(builder, option?.MonogramText);
            }
        }

        private static void AppendWorkbenchSignature(
            StringBuilder builder,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool includeCardDetails)
        {
            AppendToken(builder, viewModel.WorkbenchTitleText);
            AppendToken(builder, viewModel.WorkbenchEntryCountText);
            AppendToken(builder, viewModel.WorkbenchSummaryText);
            AppendToken(builder, viewModel.WorkbenchDetailText);
            AppendToken(builder, viewModel.WorkbenchDiagnosticText);
            AppendToken(builder, viewModel.WorkbenchGroupIdentity);
            for (int i = 0; i < viewModel.WorkbenchResponsibilityCounts.Length; i++)
                builder.Append(viewModel.WorkbenchResponsibilityCounts[i]).Append('|');
            AppendToken(builder, viewModel.DraftStatusText);
            AppendToken(builder, viewModel.PlayerLegalityStatusText);
            AppendToken(builder, viewModel.PlayerLegalityReasonText);
            AppendToken(builder, viewModel.DraftHashText);
            AppendToken(builder, viewModel.ApplyDraftDisabledReasonText);
            AppendToken(builder, viewModel.PublishDraftStatusText);
            AppendToken(builder, viewModel.PublishDraftDisabledReasonText);
            AppendToken(builder, viewModel.SaveBehaviorGroupDisabledReasonText);
            AppendToken(builder, viewModel.ProjectCardMetadataTitle);
            AppendToken(builder, viewModel.ProjectCardDisplayName);
            AppendToken(builder, viewModel.ProjectCardCost);
            AppendToken(builder, viewModel.ProjectCardDescription);
            AppendToken(builder, viewModel.ProjectCardUseSummary);
            AppendToken(builder, viewModel.ProjectCardRiskSummary);
            AppendToken(builder, viewModel.ProjectCardTags);
            builder.Append(viewModel.CanEditWorkbench ? '1' : '0')
                .Append(viewModel.CanUndoDraft ? '1' : '0')
                .Append(viewModel.CanSaveDraft ? '1' : '0')
                .Append(viewModel.CanApplyDraft ? '1' : '0')
                .Append(viewModel.CanPublishDraft ? '1' : '0')
                .Append(viewModel.CanSaveBehaviorGroup ? '1' : '0')
                .Append(viewModel.CanReloadProjectDraft ? '1' : '0')
                .Append(viewModel.CanReapplyProjectDraft ? '1' : '0');
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows =
                viewModel.WorkbenchRows ?? Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[i];
                builder.Append(row != null ? row.EntryIndex : -1).Append('|')
                    .Append(row != null ? (int)row.RowKind : 0).Append('|')
                    .Append(row != null ? (int)row.PublishState : 0).Append('|')
                    .Append(row != null && row.HasValidationProblems ? '1' : '0').Append('|')
                    .Append(row != null ? (int)row.ResponsibilityKind : 0).Append('|')
                    .Append(row != null ? row.ResponsibilityTotalCount : 0).Append('|')
                    .Append(row != null ? row.ResponsibilityEnabledCount : 0).Append('|')
                    .Append(row != null && row.ResponsibilityExpanded ? '1' : '0')
                    .Append(row != null && row.ResponsibilityWarning ? '1' : '0')
                    .Append(row != null && row.Visible ? '1' : '0')
                    .Append(row != null && row.Selected ? '1' : '0')
                    .Append(row != null && row.Disabled ? '1' : '0')
                    .Append(row != null && row.Invalid ? '1' : '0');
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities =
                    row?.ResponsibilityKinds
                    ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
                for (int responsibilityIndex = 0;
                    responsibilityIndex < responsibilities.Length;
                    responsibilityIndex++)
                {
                    builder.Append((int)responsibilities[responsibilityIndex]).Append(',');
                }
                builder.Append('|');
                AppendToken(builder, row?.CardDraftId);
                AppendToken(builder, row?.PublishedStableId);
                AppendToken(builder, row?.Label);
                AppendToken(builder, row?.TitleText);
                AppendToken(builder, row?.BadgeText);
                AppendToken(builder, row?.PublishStatusText);
                AppendToken(builder, row?.ValidationStatusText);
                AppendToken(builder, row?.TriggerText);
                AppendToken(builder, row?.ActionText);
                AppendToken(builder, row?.OutcomeText);
                AppendToken(builder, row?.PriorityText);
                AppendToken(builder, row?.ExecutionDetailText);
                AppendToken(builder, row?.ResponsibilitySummaryText);
                AppendToken(builder, row?.ResponsibilityColor);
                builder.Append(row != null ? (int)row.ExecutionState : 0).Append('|');
            }
            CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel[] libraryRows =
                viewModel.WorkbenchLibraryRows
                ?? Array.Empty<CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel>();
            builder.Append(viewModel.WorkbenchCoreReplacementActive ? '1' : '0');
            for (int i = 0; i < libraryRows.Length; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel row = libraryRows[i];
                AppendToken(builder, row?.CardDraftId);
                AppendToken(builder, row?.BehaviorId);
                AppendToken(builder, row?.DisplayName);
                AppendToken(builder, row?.CategoryText);
                AppendToken(builder, row?.StatusText);
                builder.Append(row != null && row.Installed ? '1' : '0')
                    .Append(row != null && row.Available ? '1' : '0')
                    .Append('|');
            }
            if (!includeCardDetails)
                return;

            AppendToken(builder, viewModel.TimelineStatusText);
            AppendToken(builder, viewModel.BehaviorCardRuntimeHeaderText);
            AppendToken(builder, viewModel.BehaviorCardRuntimeStateText);
            AppendToken(builder, viewModel.BehaviorCardExecutionSummaryText);
            AppendToken(builder, viewModel.BehaviorCardRecentEventsText);
            AppendBehaviorCardWorkspaceSignature(builder, viewModel);
            AppendBehaviorCardRuntimeSignature(builder, viewModel.BehaviorCardRuntimeControl);
            AppendIdentityProjection(builder, viewModel.BehaviorCardIdentity);
        }

    }
}
