using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
    {
        private long _lastSetupBuildSourceRevision;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel _cachedSetupViewModel;
        private static readonly Unity.Profiling.ProfilerMarker BuildPanelModelMarker = new Unity.Profiling.ProfilerMarker("CharacterTest.UI.BuildAiLabModel");
        private static readonly Unity.Profiling.ProfilerMarker BindPanelModelMarker = new Unity.Profiling.ProfilerMarker("CharacterTest.UI.BindAiLabModel");

        private bool Refresh(bool openIfClosed, CharacterTestRuntimeAiLabShellState mountingState = null)
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _controller == null)
                return false;

            if (!openIfClosed && !IsWorkspaceOpen)
                return false;

            _behaviorCardPreviewHost?.PumpActiveSurface();
            if (_behaviorCardPreviewHost != null
                && _behaviorCardPreviewHost.TryConsumeContractFault(
                    out string previewFaultCode,
                    out string previewFaultMessage))
            {
                Warn(previewFaultCode + ": " + previewFaultMessage);
                Action<string, string> sink = _previewContractFaultSink;
                sink?.Invoke(previewFaultCode, previewFaultMessage);
                return false;
            }

            // Setup camera presentation is finalized after UI runtime composition. Rebind
            // immediately before opening so the FairyGUI overlay is attached to the actual
            // scene camera/output used by the pre-test view as well as the runtime view.
            if (openIfClosed && !IsWorkspaceOpen)
                ConfigureFairyGuiCameraStack();

            // A mount is evaluated before the shell commits CurrentState. Use its candidate
            // while mounting; normal refreshes still restore the committed navigation state.
            _commandSink?.SynchronizeShellNavigationState(mountingState);
            _commandSink?.SynchronizeLiveBehaviorPlanSelection();
            _commandSink?.SynchronizeDeferredBehaviorRecipeSaveStatus();

            CharacterTestRuntimeAiLabWorkspaceMode requestedWorkspace = _commandSink != null
                ? _commandSink.WorkspaceMode
                : CharacterTestRuntimeAiLabWorkspaceMode.Setup;
            string commandStatus = _commandSink != null ? _commandSink.LastStatusMessage : string.Empty;
            int selectedWorkbenchEntryIndex = _commandSink != null
                ? _commandSink.SelectedWorkbenchEntryIndex
                : 0;
            int selectedCompiledStepIndex = _commandSink != null
                ? _commandSink.SelectedCompiledStepIndex
                : 0;
            CharacterTestRuntimeAiLabLogicElementKind selectedLogicElementKind = _commandSink != null
                ? _commandSink.SelectedLogicElementKind
                : CharacterTestRuntimeAiLabLogicElementKind.Stage;
            int selectedLogicElementIndex = _commandSink != null
                ? _commandSink.SelectedLogicElementIndex
                : 0;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView behaviorCardLibrary =
                requestedWorkspace == CharacterTestRuntimeAiLabWorkspaceMode.Setup
                    ? null
                    : _behaviorCardWorkspaceCommands?.CurrentLibrary;
            CharacterTestRuntimeAiLabBehaviorCardLibraryState? behaviorCardLibraryState =
                _behaviorCardWorkspaceCommands?.LibraryState;
            long behaviorCardPresentationRevision = unchecked(
                ((_commandSink?.CommandCount ?? 0) * 397L)
                ^ (_behaviorCardWorkspaceCommands?.StagePresentationRevision ?? 0L));
            CharacterTestUiTextProviderSignature textSignature =
                CharacterTestUiTextProviderSignature.Create(_textProvider);
            bool requestedBehaviorCard = requestedWorkspace == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                || (_commandSink != null
                    && (_commandSink.IsSelectedCardExpanded
                        || (_commandSink.DiagnosticsTab
                                == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame
                            && _commandSink.TimelineMode
                                != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)));
            if (requestedBehaviorCard && IsWorkspaceOpen && _retainedBehaviorCardViewModel != null)
            {
                long sourceRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .ComputeBehaviorCardBuildSourceRevision(
                        _controller,
                        commandStatus,
                        _textProvider,
                        selectedWorkbenchEntryIndex,
                        selectedCompiledStepIndex,
                        selectedLogicElementKind,
                        selectedLogicElementIndex,
                        behaviorCardLibrary,
                        behaviorCardLibraryState,
                        behaviorCardPresentationRevision,
                        _behaviorCardWorkspaceViewModel.Timeline.Presentation);
                if (_retainedBehaviorCardViewModel.HasBehaviorCardBuildSourceRevision
                    && _retainedBehaviorCardViewModel.BehaviorCardBuildSourceRevision == sourceRevision
                    && _hasLastBehaviorCardWorkspaceRevision
                    && _lastBehaviorCardTextSignature == textSignature)
                {
                    SkippedRefreshCount++;
                    LastViewModel = _retainedBehaviorCardViewModel;
                    return true;
                }
            }

            bool cacheSetup = requestedWorkspace == CharacterTestRuntimeAiLabWorkspaceMode.Setup
                && !requestedBehaviorCard
                && _controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
            long setupSourceRevision = 0L;
            if (cacheSetup)
            {
                setupSourceRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .ComputeBehaviorCardBuildSourceRevision(
                        _controller, commandStatus, _textProvider,
                        selectedWorkbenchEntryIndex, selectedCompiledStepIndex,
                        selectedLogicElementKind, selectedLogicElementIndex,
                        behaviorCardLibrary, behaviorCardLibraryState, behaviorCardPresentationRevision);
                setupSourceRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .AppendSetupBuildSourceRevision(setupSourceRevision, _controller,
                        _commandSink != null ? _commandSink.PendingSetupBehaviorGroupDeleteDraftId : -1);
                if (IsWorkspaceOpen && _cachedSetupViewModel != null
                    && ReferenceEquals(LastViewModel, _cachedSetupViewModel)
                    && setupSourceRevision == _lastSetupBuildSourceRevision)
                {
                    SkippedRefreshCount++;
                    return true;
                }
            }
            // Only a successfully projected setup model may satisfy the next early-out.
            _cachedSetupViewModel = null;

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel;
            using (BuildPanelModelMarker.Auto())
            {
                viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        _controller,
                        commandStatus,
                        _textProvider,
                        requestedWorkspace,
                        selectedWorkbenchEntryIndex,
                        selectedCompiledStepIndex,
                        _commandSink != null && _commandSink.IsSelectedCardExpanded,
                        selectedLogicElementKind,
                        selectedLogicElementIndex,
                        _commandSink != null
                            ? _commandSink.DiagnosticsTab
                            : CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
                        behaviorCardLibrary,
                        behaviorCardLibraryState,
                        _behaviorCardWorkspaceViewModel,
                        _retainedBehaviorCardViewModel,
                        behaviorCardPresentationRevision,
                        _commandSink != null
                            ? _commandSink.TimelineMode
                            : CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                        _controller.ShellCoordinator?.CurrentState?.PageContext
                            .RuntimeTraceExpanded,
                        _controller.ShellCoordinator?.CurrentState?.PageContext
                            .BehaviorPlanExpanded,
                        _commandSink != null
                            ? _commandSink.PendingSetupBehaviorGroupDeleteDraftId
                            : -1,
                        _commandSink != null ? _commandSink.WorkbenchScrollOffset : 0);
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel behaviorTimeline =
                viewModel.BehaviorCardWorkspace?.Timeline;
            if (_commandSink != null
                && behaviorTimeline?.HasBlockingDiagnostics == true
                && behaviorTimeline.HasAutoFocusedDiagnosticKey
                && (selectedLogicElementKind != behaviorTimeline.SelectedElementKind
                    || selectedLogicElementIndex != behaviorTimeline.SelectedElementIndex))
            {
                _commandSink.SetBehaviorCardDiagnosticFocus(
                    behaviorTimeline.SelectedElementKind,
                    behaviorTimeline.SelectedElementIndex);
            }
            bool behaviorCardWorkspace =
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                || viewModel.ShowBehaviorPlan
                || viewModel.ShowBehaviorLogic;
            if (behaviorCardWorkspace)
                _retainedBehaviorCardViewModel = viewModel;
            long behaviorCardRevision = ComputeBehaviorCardPresentationRevision(viewModel);
            string signature = behaviorCardWorkspace
                ? string.Empty
                : CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel)
                    + "|text="
                    + CharacterTestUiText.Signature(_textProvider);
            bool unchanged = behaviorCardWorkspace
                ? _hasLastBehaviorCardWorkspaceRevision
                    && _lastBehaviorCardWorkspaceRevision == behaviorCardRevision
                    && _lastBehaviorCardTextSignature == textSignature
                : _hasLastViewModelSignature
                    && string.Equals(_lastViewModelSignature, signature, StringComparison.Ordinal);
            if (!behaviorCardWorkspace
                && LastViewModel != null
                && LastViewModel.TimelineExpanded != viewModel.TimelineExpanded)
            {
                unchanged = false;
            }
            if (IsWorkspaceOpen && unchanged)
            {
                SkippedRefreshCount++;
                LastViewModel = viewModel;
                if (cacheSetup)
                {
                    _cachedSetupViewModel = viewModel;
                    _lastSetupBuildSourceRevision = setupSourceRevision;
                }
                if (viewModel.ShowBehaviorLogic)
                    _workspaceBinder?.RefreshBehaviorLogicRuntimeOverlay(
                        RootForTests,
                        viewModel);
                return true;
            }

            MxUiOpenResult result;
            using (BindPanelModelMarker.Auto())
                result = _navigator.Open(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId, viewModel);
            if (result.Success)
            {
                LastViewModel = viewModel;
                if (cacheSetup)
                {
                    _cachedSetupViewModel = viewModel;
                    _lastSetupBuildSourceRevision = setupSourceRevision;
                }
                if (behaviorCardWorkspace)
                {
                    _lastBehaviorCardWorkspaceRevision = behaviorCardRevision;
                    _lastBehaviorCardTextSignature = textSignature;
                    _hasLastBehaviorCardWorkspaceRevision = true;
                    _lastViewModelSignature = string.Empty;
                    _hasLastViewModelSignature = false;
                }
                else
                {
                    _lastViewModelSignature = signature;
                    _hasLastViewModelSignature = true;
                    _hasLastBehaviorCardWorkspaceRevision = false;
                }
                SuccessfulRefreshCount++;
                _reportedOpenFailure = false;
                _lastStatusMessage = _commandSink != null ? _commandSink.LastStatusMessage : string.Empty;
                return true;
            }

            if (!_reportedOpenFailure)
            {
                _reportedOpenFailure = true;
                Warn("Runtime AI Planner dev panel open failed: " + result.Message);
            }

            return false;
        }

        /// <remarks>NoAlloc.</remarks>
        internal static long ComputeBehaviorCardPresentationRevision(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            bool behaviorCardWorkspace = viewModel != null
                && (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    || viewModel.ShowBehaviorPlan
                    || viewModel.ShowBehaviorLogic);
            if (!behaviorCardWorkspace)
                return 0L;

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            long workspaceRevision = viewModel.ShowBehaviorLogic
                ? unchecked(
                    ComputeBehaviorLogicEditorRevision(timeline) * 397L
                    ^ ComputeBehaviorLogicWorkspaceChromeRevision(viewModel))
                : viewModel.BehaviorCardWorkspace.WorkspaceRevision;
            long revision = unchecked(
                workspaceRevision * 397L
                + (long)viewModel.TimelineMode * 17L
                + (viewModel.ShowBehaviorPlan ? 1L : 0L)
                + (viewModel.ShowBehaviorLogic ? 2L : 0L));
            if ((viewModel.ShowBehaviorPlan || viewModel.ShowBehaviorLogic)
                && !viewModel.ShowBehaviorLogic
                && viewModel.HasBehaviorCardBuildSourceRevision)
            {
                revision = unchecked(
                    revision * 397L
                    ^ viewModel.BehaviorCardBuildSourceRevision);
            }

            revision = unchecked(revision * 397L ^ viewModel.TacticalPresentationRevision);
            return revision;
        }

        /// <summary>
        /// Returns the revision of the editable behavior-logic structure. Runtime frame,
        /// action progress, and observed distance deliberately do not participate because
        /// those values are updated in-place by the binder.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        internal static long ComputeBehaviorLogicEditorRevision(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            if (timeline == null)
                return 0L;

            unchecked
            {
                long revision = timeline.CachedStructureKey;
                revision = revision * 397L ^ (int)timeline.SelectedElementKind;
                revision = revision * 397L ^ timeline.SelectedElementIndex;
                revision = revision * 397L ^ (int)timeline.Presentation.SelectedRecipeEventKind;
                revision = revision * 397L ^ StringComparer.Ordinal.GetHashCode(
                    timeline.Presentation.SelectedRecipeEventStableKey ?? string.Empty);
                revision = revision * 397L ^ (int)timeline.Presentation.RecipeInspectorTargetKind;
                revision = revision * 397L ^ StringComparer.Ordinal.GetHashCode(
                    timeline.Presentation.SelectedRecipeTimelineId ?? string.Empty);
                revision = revision * 397L ^ StringComparer.Ordinal.GetHashCode(
                    timeline.Presentation.SelectedRecipeEventId ?? string.Empty);
                revision = revision * 397L ^ (timeline.Available ? 1 : 0);
                revision = revision * 397L ^ (timeline.CanEdit ? 1 : 0);
                revision = revision * 397L ^ (timeline.CanEditCardDisplayName ? 1 : 0);
                revision = revision * 397L ^ (timeline.CanEditResponsibilities ? 1 : 0);
                revision = revision * 397L ^ timeline.EntryIndex;
                revision = revision * 397L ^ timeline.ResponsibilityCompatibilityMask;
                for (int responsibilityIndex = 0;
                     timeline.ResponsibilityKinds != null
                     && responsibilityIndex < timeline.ResponsibilityKinds.Length;
                     responsibilityIndex++)
                {
                    revision = revision * 397L ^ (int)timeline.ResponsibilityKinds[responsibilityIndex];
                }
                revision = revision * 397L ^ (timeline.HasBlockingDiagnostics ? 1 : 0);
                revision = revision * 397L ^ (timeline.RequiresDetectorWindowCanonicalization ? 1 : 0);
                revision = revision * 397L ^ timeline.CardTitleText.GetHashCode();
                revision = revision * 397L ^ timeline.SemanticHashText.GetHashCode();
                revision = revision * 397L ^ timeline.ValidationBadgeText.GetHashCode();
                revision = revision * 397L ^ timeline.CommandFeedbackText.GetHashCode();
                return revision;
            }
        }

        /// <summary>
        /// Returns the non-runtime revision of the controls surrounding the behavior
        /// recipe. This keeps list/library changes visible without rebuilding the
        /// editable timeline for every runtime frame.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        private static long ComputeBehaviorLogicWorkspaceChromeRevision(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            unchecked
            {
                long revision = viewModel.WorkbenchRowCount;
                revision = revision * 397L ^ viewModel.WorkbenchScrollOffset;
                revision = revision * 397L ^ viewModel.WorkbenchMaxScrollOffset;
                CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows =
                    viewModel.WorkbenchRows ?? Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
                int rowCount = Math.Min(viewModel.WorkbenchRowCount, rows.Length);
                for (int i = 0; i < rowCount; i++)
                {
                    CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[i];
                    if (row == null)
                        continue;
                    revision = revision * 397L ^ row.EntryIndex;
                    revision = revision * 397L ^ row.DraftRevision;
                    revision = revision * 397L ^ row.CardDraftId.GetHashCode();
                    revision = revision * 397L ^ (row.Visible ? 1 : 0);
                    revision = revision * 397L ^ (row.Selected ? 1 : 0);
                    revision = revision * 397L ^ (row.Disabled ? 1 : 0);
                    revision = revision * 397L ^ (row.Invalid ? 1 : 0);
                }
                CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library =
                    viewModel.BehaviorCardWorkspace?.Library;
                if (library != null)
                {
                    revision = revision * 397L ^ library.CachedStructureKey;
                    revision = revision * 397L ^ library.ActiveCount;
                    revision = revision * 397L ^ library.FocusedIndex;
                    revision = revision * 397L ^ library.FirstVisibleIndex;
                }
                return revision;
            }
        }

    }
}
