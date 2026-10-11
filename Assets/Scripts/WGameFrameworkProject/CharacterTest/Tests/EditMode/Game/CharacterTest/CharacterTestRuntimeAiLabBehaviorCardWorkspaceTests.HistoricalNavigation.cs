using System;
using System.Reflection;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {
        [Test]
        public void HistoricalCommandPolicyClassifiesEveryRegisteredWorkspaceCommandAndDeniesUnknown()
        {
            FieldInfo[] fields = typeof(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds)
                .GetFields(BindingFlags.Static | BindingFlags.NonPublic);
            int commandConstantCount = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (!fields[i].IsLiteral
                    || fields[i].FieldType != typeof(string)
                    || string.Equals(fields[i].Name, "Prefix", StringComparison.Ordinal))
                {
                    continue;
                }
                commandConstantCount++;
                string commandId = (string)fields[i].GetRawConstantValue();
                bool found = false;
                for (int registered = 0;
                     registered < CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                         .RegisteredWorkspaceCommandCount;
                     registered++)
                {
                    if (string.Equals(
                            commandId,
                            CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                                .GetRegisteredWorkspaceCommandId(registered),
                            StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }
                Assert.That(found, Is.True, fields[i].Name);
            }

            Assert.That(
                commandConstantCount,
                Is.EqualTo(
                    CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                        .RegisteredWorkspaceCommandCount));
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .IsWorkspaceCommandReadOnlyAllowed(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage),
                Is.True);
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .IsWorkspaceCommandReadOnlyAllowed(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ToggleStageSnap),
                Is.False);
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .IsWorkspaceCommandReadOnlyAllowed(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.Prefix + "future_write"),
                Is.False);
        }

        [Test]
        public void HistoricalWorkspaceHandlerRejectsAuthoringBeforeAnyDraftBoundary()
        {
            var workspace = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel();
            workspace.Timeline.Historical = true;
            workspace.Timeline.CanEdit = false;
            var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
            handler.Configure(
                null,
                null,
                null,
                null,
                workspaceView: () => workspace);
            bool snapBefore = handler.StageSnapEnabled;

            Assert.That(handler.TryHandle(
                new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ToggleStageSnap,
                    null),
                out string message), Is.True);
            Assert.That(
                message,
                Is.EqualTo(
                    CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                        .ReadOnlyDiagnostic));
            Assert.That(handler.StageSnapEnabled, Is.EqualTo(snapBefore));
            Assert.That(handler.BeginStageGesture(
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                "stage.test",
                0f,
                1f,
                CharacterTestRuntimeAiLabStageSelectionModifier.None,
                out _), Is.False);
            Assert.That(
                handler.LastMessage,
                Is.EqualTo(
                    CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                        .ReadOnlyDiagnostic));
        }

        [Test]
        public void HistoricalShellPolicyAllowsExplicitTimelineModeRoundTrip()
        {
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy.ClassifyShellCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace),
                Is.EqualTo(CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.ReadOnlyAllowed));
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy.ClassifyShellCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan),
                Is.EqualTo(CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.ReadOnlyAllowed));
        }

        [Test]
        public void HistoricalShellPolicyClassifiesEveryRootCommandAndDeniesUnknown()
        {
            FieldInfo[] fields = typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds)
                .GetFields(BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!fields[i].IsLiteral || fields[i].FieldType != typeof(string))
                    continue;
                string commandId = (string)fields[i].GetRawConstantValue();
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                        .ClassifyShellCommand(commandId),
                    Is.Not.EqualTo(
                        CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.UnknownDenied),
                    fields[i].Name);
            }
            string[] readOnlyCommands =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateBehaviorNode,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateRuntimeFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorTransitionLocateRuntimeFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineSelectItem,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineToggleExpanded,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomIn,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanRight,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubUpdate,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecompute,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecomputeCancel
            };
            for (int i = 0; i < readOnlyCommands.Length; i++)
            {
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                        .ClassifyShellCommand(readOnlyCommands[i]),
                    Is.EqualTo(CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.ReadOnlyAllowed),
                    readOnlyCommands[i]);
            }

            string[] sideEffectCommands =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RuntimeTestSelectAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleStateCapture,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SyncRecordedState,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellRestore,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellReset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FrameLimit,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEditGate,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupBehaviorGroupPrefix,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupCharacterPrefix,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.AddWorkbenchCard,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaceCoreSlot,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft
            };
            for (int i = 0; i < sideEffectCommands.Length; i++)
            {
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                        .ClassifyShellCommand(sideEffectCommands[i]),
                    Is.EqualTo(CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.AuthoringDenied),
                    sideEffectCommands[i]);
            }
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .ClassifyShellCommand("character_test.runtime_ai_planner.dev.future_write"),
                Is.EqualTo(CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.UnknownDenied));
        }

        [Test]
        public void HistoricalPresentationCapturesLatestContextOnceAndRestoresItAtomically()
        {
            var host = new GameObject("Historical Behavior Card Presentation Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                EnterBehaviorCardWorkspace(controller);

                CharacterTestRuntimeAiLabShellCoordinator coordinator = controller.ShellCoordinator;
                CharacterTestRuntimeAiLabShellState shell = coordinator.CurrentState;
                var latestViewport = new CharacterTestRuntimeAiLabBehaviorCardViewport(
                    120,
                    720,
                    1.5f,
                    96f,
                    true);
                Assert.That(coordinator.TryUpdateBehaviorCardContext(
                    shell.Generation,
                    shell.Revision,
                    "group.test",
                    "entry.test",
                    "card.test",
                    CharacterTestRuntimeAiLabSelectedElementKind.Stage,
                    "stage.saved",
                    latestViewport).Success, Is.True);
                shell = coordinator.CurrentState;
                var latestLibrary = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "saved query",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.CurrentDraft,
                    "card.saved.first",
                    "card.saved.focus",
                    64f);
                Assert.That(coordinator.TryUpdateBehaviorCardLibraryState(
                    shell.Generation,
                    shell.Revision,
                    latestLibrary).Success, Is.True);

                CharacterTestRuntimeAiLabPageContext captured = coordinator.CurrentState.PageContext;
                controller.BeginHistoricalBehaviorPresentation();
                var historicalViewport = new CharacterTestRuntimeAiLabBehaviorCardViewport(
                    900,
                    1500,
                    2f,
                    240f,
                    false);
                var historicalLibrary = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "history query",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.RuntimeActive,
                    "card.history.first",
                    "card.history.focus",
                    192f);
                controller.UpdateHistoricalBehaviorCardViewport(historicalViewport);
                controller.UpdateHistoricalBehaviorCardLibraryState(historicalLibrary);

                controller.BeginHistoricalBehaviorPresentation();
                Assert.That(controller.HistoricalBehaviorPresentationActive, Is.True);
                Assert.That(
                    controller.BehaviorCardPresentationViewport.HorizontalStartFrame,
                    Is.EqualTo(historicalViewport.HorizontalStartFrame));
                Assert.That(
                    controller.BehaviorCardPresentationLibraryState.Query,
                    Is.EqualTo(historicalLibrary.Query));
                Assert.That(
                    coordinator.CurrentState.PageContext.BehaviorCardViewport.HorizontalStartFrame,
                    Is.EqualTo(captured.BehaviorCardViewport.HorizontalStartFrame),
                    "Historical navigation must not overwrite the latest Draft viewport.");
                Assert.That(
                    coordinator.CurrentState.PageContext.BehaviorCardLibraryState.Query,
                    Is.EqualTo(captured.BehaviorCardLibraryState.Query),
                    "Historical navigation must not overwrite the latest Draft library state.");

                Assert.That(
                    controller.EndHistoricalBehaviorPresentation(out string diagnostic),
                    Is.True,
                    diagnostic);
                CharacterTestRuntimeAiLabPageContext restored = coordinator.CurrentState.PageContext;
                Assert.That(controller.HistoricalBehaviorPresentationActive, Is.False);
                Assert.That(restored.GroupDraftId, Is.EqualTo(captured.GroupDraftId));
                Assert.That(restored.EntryId, Is.EqualTo(captured.EntryId));
                Assert.That(restored.CardDraftId, Is.EqualTo(captured.CardDraftId));
                Assert.That(restored.SelectedElementKind, Is.EqualTo(captured.SelectedElementKind));
                Assert.That(
                    restored.SelectedElementStableId,
                    Is.EqualTo(captured.SelectedElementStableId));
                Assert.That(
                    restored.BehaviorCardViewport.HorizontalStartFrame,
                    Is.EqualTo(latestViewport.HorizontalStartFrame));
                Assert.That(
                    restored.BehaviorCardViewport.HorizontalEndFrame,
                    Is.EqualTo(latestViewport.HorizontalEndFrame));
                Assert.That(
                    restored.BehaviorCardViewport.VerticalScrollOffset,
                    Is.EqualTo(latestViewport.VerticalScrollOffset));
                Assert.That(restored.BehaviorCardLibraryState.Query, Is.EqualTo(latestLibrary.Query));
                Assert.That(
                    restored.BehaviorCardLibraryState.FocusedStableCardId,
                    Is.EqualTo(latestLibrary.FocusedStableCardId));
                Assert.That(
                    controller.EndHistoricalBehaviorPresentation(out diagnostic),
                    Is.True,
                    diagnostic);

                controller.BeginHistoricalBehaviorPresentation();
                shell = coordinator.CurrentState;
                Assert.That(coordinator.Navigate(
                    shell.Generation,
                    shell.Revision,
                    CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                    CharacterTestRuntimeAiLabWorkspace.Facts).Success, Is.True);
                Assert.That(
                    controller.EndHistoricalBehaviorPresentation(out diagnostic),
                    Is.True,
                    diagnostic);
                Assert.That(
                    coordinator.CurrentState.ActiveWorkspace,
                    Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Facts));
                Assert.That(
                    coordinator.CurrentState.PageContext.CardDraftId,
                    Is.EqualTo(captured.CardDraftId),
                    "Leaving Behavior Card during history must preserve the latest Draft identity.");
                Assert.That(
                    coordinator.CurrentState.PageContext.BehaviorCardViewport.HorizontalStartFrame,
                    Is.EqualTo(latestViewport.HorizontalStartFrame));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HistoricalElementSelectionNeverOverwritesLatestDraftContextAcrossTabs()
        {
            var host = new GameObject("Historical Behavior Card Selection Isolation Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(
                    controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True,
                    presetMessage);
                EnterBehaviorCardWorkspace(controller);

                const int entryIndex = 2;
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, entryIndex);
                Assert.That(workspace.Timeline.StageCount, Is.GreaterThan(0));
                Assert.That(workspace.Timeline.DetectorCount, Is.GreaterThan(0));
                Assert.That(workspace.Timeline.TransitionCount, Is.GreaterThan(0));
                Assert.That(controller.TryResolveStageEditIdentity(
                    entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity route,
                    out string routeDiagnostic), Is.True, routeDiagnostic);

                CharacterTestRuntimeAiLabShellCoordinator coordinator = controller.ShellCoordinator;
                CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
                Assert.That(coordinator.TryUpdateBehaviorCardContext(
                    state.Generation,
                    state.Revision,
                    route.GroupDraftId,
                    route.EntryId,
                    route.CardDraftId,
                    CharacterTestRuntimeAiLabSelectedElementKind.Stage,
                    workspace.Timeline.Stages[workspace.Timeline.StageCount - 1].StageId,
                    state.PageContext.BehaviorCardViewport).Success, Is.True);
                CharacterTestRuntimeAiLabPageContext latest = coordinator.CurrentState.PageContext;

                controller.BeginHistoricalBehaviorPresentation();
                workspace.Timeline.Historical = true;
                workspace.Timeline.CanEdit = false;
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    () => entryIndex,
                    workspaceView: () => workspace);

                AssertHistoricalSelectionDoesNotMutateLatest(
                    handler,
                    coordinator,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage,
                    workspace.Timeline.Stages[0].StageId,
                    latest);
                Assert.That(handler.SelectStage(
                    workspace.Timeline.Stages[0].StageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);
                AssertLatestSelectionUnchanged(coordinator, latest);
                Assert.That(handler.ToggleStageExpanded(workspace.Timeline.Stages[0].StageId), Is.True);
                AssertLatestSelectionUnchanged(coordinator, latest);
                handler.ClearStageSelection();
                AssertLatestSelectionUnchanged(coordinator, latest);
                AssertHistoricalSelectionDoesNotMutateLatest(
                    handler,
                    coordinator,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                    workspace.Timeline.Detectors[0].DetectorId,
                    latest);
                AssertHistoricalSelectionDoesNotMutateLatest(
                    handler,
                    coordinator,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition,
                    workspace.Timeline.Transitions[0].TransitionId,
                    latest);

                NavigateAndAssertLatestSelection(
                    coordinator,
                    CharacterTestRuntimeAiLabWorkspace.Facts,
                    latest);
                NavigateAndAssertLatestSelection(
                    coordinator,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    latest);
                NavigateAndAssertLatestSelection(
                    coordinator,
                    CharacterTestRuntimeAiLabWorkspace.Replay,
                    latest);
                NavigateAndAssertLatestSelection(
                    coordinator,
                    CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                    latest);

                Assert.That(
                    controller.EndHistoricalBehaviorPresentation(out string diagnostic),
                    Is.True,
                    diagnostic);
                AssertLatestSelectionUnchanged(coordinator, latest);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HistoricalExactCommitEmphasizesPathWithoutReplacingSnapshotStage()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.True);
            int committedTransitionIndex = -1;
            for (int i = 0; i < compile.Program.Transitions.Count; i++)
            {
                if (!compile.Program.Transitions[i].EndsCard
                    && compile.Program.Transitions[i].TargetStageIndex != 0)
                {
                    committedTransitionIndex = i;
                    break;
                }
            }
            Assert.That(committedTransitionIndex, Is.GreaterThanOrEqualTo(0));
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 17,
                CurrentStageIndex = 0,
                StageEnteredFrame = 10,
                PendingTransitionIndex = -1,
                DetectorArmed = new bool[compile.Program.Detectors.Count],
                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                PrimaryActionStageIndex = 0
            };
            var historical = new CharacterTestRuntimeAiLabHistoricalBehaviorProjection
            {
                Status = CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Resolved,
                EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.Complete,
                RuntimeState = runtime,
                FrozenProgram = compile.Program,
                ExactCommittedTransitionIndex = committedTransitionIndex,
                SelectedFrame = 40,
                LatestFrame = 41
            };
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Historical snapshot authority",
                compile,
                payload,
                0,
                runtimeState: runtime,
                runtimeFrame: 40,
                historical: historical);

            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Stages[0].Current, Is.True);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Timeline.Stages[
                    compile.Program.Transitions[committedTransitionIndex].TargetStageIndex].Current,
                Is.False);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Timeline.Transitions[committedTransitionIndex].Committed,
                Is.True);

            historical.ExactCommittedTransitionIndex = -1;
            historical.PreviousCommittedTransitionIndex = committedTransitionIndex;
            historical.PreviousCommittedRuntimeFrame = 39;
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Historical snapshot authority",
                compile,
                payload,
                0,
                runtimeState: runtime,
                runtimeFrame: 40,
                historical: historical);

            CharacterTestRuntimeAiLabLogicTransitionViewModel previous =
                viewModel.BehaviorCardWorkspace.Timeline.Transitions[committedTransitionIndex];
            Assert.That(previous.Committed, Is.False);
            Assert.That(previous.PreviousCommitted, Is.True);
            Assert.That(previous.StatusText, Is.EqualTo("历史路径 · F39"));
        }
    }
}
