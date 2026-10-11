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
        public void RootBehaviorCardBuilderReusesProjectionWithoutAllocatingWhenSourceIsUnchanged()
        {
            var host = new GameObject("Behavior Card Root Builder Reuse Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                EnterBehaviorCardWorkspace(controller);
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel retained =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        string.Empty,
                        workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0);
                Assert.That(retained.HasBehaviorCardBuildSourceRevision, Is.True);

                for (int i = 0; i < 8; i++)
                {
                    Assert.That(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                            controller,
                            string.Empty,
                            workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                            selectedWorkbenchEntryIndex: 0,
                            behaviorCardWorkspace: retained.BehaviorCardWorkspace,
                            reusableViewModel: retained),
                        Is.SameAs(retained));
                }

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++)
                {
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel result =
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                            controller,
                            string.Empty,
                            workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                            selectedWorkbenchEntryIndex: 0,
                            behaviorCardWorkspace: retained.BehaviorCardWorkspace,
                            reusableViewModel: retained);
                    if (!ReferenceEquals(result, retained))
                        Assert.Fail("Behavior Card root projection was replaced.");
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.EqualTo(0L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ProjectBehaviorCardLibraryViewReusesSemanticProjectionAndBuildRevision()
        {
            var host = new GameObject("Project Behavior Card Library View Cache Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.IsProjectBehaviorCardLibraryActive, Is.True);

                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView first =
                    controller.CreateProjectBehaviorCardLibraryView();
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView second =
                    controller.CreateProjectBehaviorCardLibraryView();
                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.SameAs(first));

                long firstRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .ComputeBehaviorCardBuildSourceRevision(
                        controller,
                        string.Empty,
                        null,
                        0,
                        0,
                        CharacterTestRuntimeAiLabLogicElementKind.Stage,
                        0,
                        first,
                        null);
                long secondRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .ComputeBehaviorCardBuildSourceRevision(
                        controller,
                        string.Empty,
                        null,
                        0,
                        0,
                        CharacterTestRuntimeAiLabLogicElementKind.Stage,
                        0,
                        second,
                        null);
                Assert.That(secondRevision, Is.EqualTo(firstRevision));

                Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                    controller.SelectedSetupTeamId,
                    controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True);
                Assert.That(group.Entries.Count, Is.GreaterThan(0));
                group.Entries[0].Disabled = !group.Entries[0].Disabled;
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView changed =
                    controller.CreateProjectBehaviorCardLibraryView();
                Assert.That(changed, Is.Not.SameAs(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BehaviorCardSourceRevisionInvalidatesAgentEditGateFrameLimitAndBranchConfirmation()
        {
            var host = new GameObject("Behavior Card Source Revision Coverage Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                EnterBehaviorCardWorkspace(controller);
                long baseline = ComputeBehaviorCardSourceRevision(controller);

                FieldInfo selectedAgent = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_selectedAgentIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo editGate = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_debugEditGateEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectedAgent, Is.Not.Null);
                Assert.That(editGate, Is.Not.Null);

                selectedAgent.SetValue(controller, 1);
                long agentRevision = ComputeBehaviorCardSourceRevision(controller);
                Assert.That(agentRevision, Is.Not.EqualTo(baseline));

                editGate.SetValue(controller, true);
                long editGateRevision = ComputeBehaviorCardSourceRevision(controller);
                Assert.That(editGateRevision, Is.Not.EqualTo(agentRevision));

                Assert.That(controller.TrySetFrameLimit(300), Is.True);
                long frameLimitRevision = ComputeBehaviorCardSourceRevision(controller);
                Assert.That(frameLimitRevision, Is.Not.EqualTo(editGateRevision));

                long commandRevision = ComputeBehaviorCardSourceRevision(controller, 1L);
                Assert.That(commandRevision, Is.Not.EqualTo(frameLimitRevision));

                FieldInfo branchPending = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_branchConfirmationPending", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo branchGeneration = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_branchConfirmationGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(branchPending, Is.Not.Null);
                Assert.That(branchGeneration, Is.Not.Null);
                branchPending.SetValue(controller, true);
                branchGeneration.SetValue(controller, 1L);
                long branchRevision = ComputeBehaviorCardSourceRevision(controller, 1L);
                Assert.That(branchRevision, Is.Not.EqualTo(commandRevision));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BehaviorCardWorkspaceRevisionIncludesRuntimeControlAndBranchConfirmation()
        {
            var baseline = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            baseline.BehaviorCardWorkspace.WorkspaceRevision = 123L;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(baseline);

            var branchPending = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            branchPending.BehaviorCardWorkspace.WorkspaceRevision = 123L;
            branchPending.BehaviorCardRuntimeControl.BranchConfirmationPending = true;
            branchPending.BehaviorCardRuntimeControl.BranchConfirmationGeneration = 1L;
            branchPending.BehaviorCardRuntimeControl.BranchRiskText = "Frame 10 -> 20";
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(branchPending);

            var differentCommittedFrame = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            differentCommittedFrame.BehaviorCardWorkspace.WorkspaceRevision = 123L;
            differentCommittedFrame.BehaviorCardRuntimeControl.SelectedFrame = 10;
            differentCommittedFrame.BehaviorCardRuntimeControl.CommitGeneration = 2L;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(differentCommittedFrame);

            Assert.That(
                branchPending.BehaviorCardWorkspace.WorkspaceRevision,
                Is.Not.EqualTo(baseline.BehaviorCardWorkspace.WorkspaceRevision));
            Assert.That(
                differentCommittedFrame.BehaviorCardWorkspace.WorkspaceRevision,
                Is.Not.EqualTo(baseline.BehaviorCardWorkspace.WorkspaceRevision));
        }

        [Test]
        public void BehaviorPlanPresentationRevisionIncludesRuntimeBuildSourceOnlyInPlanMode()
        {
            var authoring = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                BehaviorCardBuildSourceRevision = 10L,
                HasBehaviorCardBuildSourceRevision = true
            };
            authoring.BehaviorCardWorkspace.WorkspaceRevision = 123L;
            long authoringRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
                .ComputeBehaviorCardPresentationRevision(authoring);
            authoring.BehaviorCardBuildSourceRevision = 11L;

            Assert.That(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
                    .ComputeBehaviorCardPresentationRevision(authoring),
                Is.EqualTo(authoringRevision),
                "Authoring workspace should continue to refresh from its structural revision.");

            authoring.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
            authoring.ShowBehaviorPlan = true;
            long runningRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
                .ComputeBehaviorCardPresentationRevision(authoring);
            authoring.BehaviorCardBuildSourceRevision = 12L;

            Assert.That(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
                    .ComputeBehaviorCardPresentationRevision(authoring),
                Is.Not.EqualTo(runningRevision),
                "Behavior Plan must invalidate when the runtime source advances.");
        }

        [Test]
        public void BehaviorLogicPresentationRevisionIncludesRecipeEventIdentity()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                ShowBehaviorLogic = true
            };
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            timeline.CachedStructureKey = 123L;
            timeline.SelectedElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            timeline.SelectedElementIndex = 0;
            timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.PrimaryAction;
            timeline.Presentation.SelectedRecipeEventStableKey = "event:approach:primary";
            long primaryRevision = CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
                .ComputeBehaviorCardPresentationRevision(viewModel);

            timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.StageEntry;
            timeline.Presentation.SelectedRecipeEventStableKey = "event:approach:entry";

            Assert.That(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
                    .ComputeBehaviorCardPresentationRevision(viewModel),
                Is.Not.EqualTo(primaryRevision),
                "Two visible Events that share one Stage must still invalidate the Inspector and selection outline.");
        }

        [Test]
        public void InvalidDraftProjectionReusesHighWaterBuffersWithoutAllocating()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateD1StressFixture();
            payload.EntryStageId = "stage.missing";
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.False);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            for (int i = 0; i < 8; i++)
            {
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                    viewModel,
                    "Invalid Stress Card",
                    compile,
                    payload,
                    0);
            }
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                viewModel.BehaviorCardWorkspace.Timeline.Stages;
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
                viewModel.BehaviorCardWorkspace.Timeline.Detectors;
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions =
                viewModel.BehaviorCardWorkspace.Timeline.Transitions;

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++)
            {
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                    viewModel,
                    "Invalid Stress Card",
                    compile,
                    payload,
                    0);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0L));
            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Stages, Is.SameAs(stages));
            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Detectors, Is.SameAs(detectors));
            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Transitions, Is.SameAs(transitions));
        }

        [Test]
        public void InvalidDraftProjectionMarksBothTemporallyOverlappingPrimaryActions()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateD1StressFixture();
            CharacterTestRuntimeAiLabLogicStage first = payload.Stages[0];
            CharacterTestRuntimeAiLabLogicStage second = payload.Stages[1];
            first.Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility;
            first.AbilityId = 910023;
            first.PlannedStartFrame = 0;
            first.PlannedDurationFrames = 60;
            second.Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility;
            second.AbilityId = 920021;
            second.PlannedStartFrame = 30;
            second.PlannedDurationFrames = 60;

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.False);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Invalid overlap",
                compile,
                payload,
                0);

            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                viewModel.BehaviorCardWorkspace.Timeline.Stages;
            Assert.That(stages[0].ActionConflict, Is.True);
            Assert.That(stages[1].ActionConflict, Is.True);
            Assert.That(stages[2].ActionConflict, Is.False);
        }

        [Test]
        public void InvalidDraftProjectionInvalidatesEveryVisibleSemanticField()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateD1StressFixture();
            payload.EntryStageId = "stage.missing";
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.False);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Invalid semantic cache",
                compile,
                payload,
                0);
            CharacterTestRuntimeAiLabLogicDetectorViewModel detectorRow =
                viewModel.BehaviorCardWorkspace.Timeline.Detectors[0];
            CharacterTestRuntimeAiLabLogicTransitionViewModel transitionRow =
                viewModel.BehaviorCardWorkspace.Timeline.Transitions[0];

            payload.Detectors[0].Conditions[0].Operator =
                CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual;
            payload.Detectors[0].Conditions[0].Value = 37.5f;
            payload.Detectors[0].Conditions[0].BoolValue = true;
            payload.Detectors[0].Conditions[0].AbilityId = 910023;
            payload.Detectors[0].Conditions[0].CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.cache.changed");
            payload.Transitions[0].EndReason = "cache-key-changed";

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Invalid semantic cache",
                compile,
                payload,
                0);

            Assert.That(detectorRow.Conditions[0].Operator,
                Is.EqualTo(CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual));
            Assert.That(detectorRow.Conditions[0].Value, Is.EqualTo(37.5f));
            Assert.That(detectorRow.Conditions[0].BoolValue, Is.True);
            Assert.That(detectorRow.Conditions[0].AbilityId, Is.EqualTo(910023));
            Assert.That(detectorRow.Conditions[0].CharacterActionId,
                Is.EqualTo(MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.cache.changed")));
            Assert.That(transitionRow.EndReason, Is.EqualTo("cache-key-changed"));
        }

        [Test]
        public void InvalidDraftProjectionRendersNullNodesAsStructuredPlaceholders()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateD1StressFixture();
            payload.Stages.Add(null);
            payload.Detectors.Add(null);
            payload.Transitions.Add(null);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.False);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            Assert.DoesNotThrow(() =>
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                    viewModel,
                    "Null-node diagnostics",
                    compile,
                    payload,
                    0));

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            StringAssert.StartsWith("invalid.stage.", timeline.Stages[timeline.StageCount - 1].StageId);
            StringAssert.StartsWith(
                "invalid.detector.",
                timeline.Detectors[timeline.DetectorCount - 1].DetectorId);
            StringAssert.StartsWith(
                "invalid.transition.",
                timeline.Transitions[timeline.TransitionCount - 1].TransitionId);
            Assert.That(timeline.HasBlockingDiagnostics, Is.True);
        }

        [Test]
        public void WarmWorkspaceProjectionReusesHighWaterBuffersWithoutAllocating()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.True);

            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView library = CreateWorkbenchView();
            var libraryState = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                "card.alpha",
                "card.alpha",
                0f);
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
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            if (compile.Program.Detectors.Count > 0)
            {
                events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                    20,
                    CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                    compile.Program.Stages[0].StageId,
                    compile.Program.Detectors[0].DetectorId,
                    string.Empty,
                    runtime.EntityId));
            }

            for (int i = 0; i < 8; i++)
            {
                ProjectWarmWorkspace(
                    viewModel,
                    compile,
                    payload,
                    runtime,
                    events,
                    library,
                    libraryState,
                    30 + i);
            }

            CharacterTestRuntimeAiLabLogicStageViewModel[] stageBuffer =
                viewModel.BehaviorCardWorkspace.Timeline.Stages;
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectorBuffer =
                viewModel.BehaviorCardWorkspace.Timeline.Detectors;
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel[] libraryBuffer =
                viewModel.BehaviorCardWorkspace.Library.Cards;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++)
            {
                ProjectWarmWorkspace(
                    viewModel,
                    compile,
                    payload,
                    runtime,
                    events,
                    library,
                    libraryState,
                    60 + i);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0L));
            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Stages, Is.SameAs(stageBuffer));
            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Detectors, Is.SameAs(detectorBuffer));
            Assert.That(viewModel.BehaviorCardWorkspace.Library.Cards, Is.SameAs(libraryBuffer));
        }

        [Test]
        public void StressFixtureProjectsDenseCardWithoutAllocatingAfterWarmup()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateD1StressFixture();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.True, JoinDiagnostics(compile));

            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView library = CreateWorkbenchView();
            var libraryState = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                "card.beta",
                "card.beta",
                1f);
            string displayName = new string('行', 64);
            for (int i = 0; i < 8; i++)
            {
                ProjectStressWorkspace(
                    viewModel,
                    displayName,
                    compile,
                    payload,
                    library,
                    libraryState);
            }

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            Assert.That(timeline.StageCount, Is.EqualTo(12));
            Assert.That(timeline.DetectorCount, Is.EqualTo(40));
            Assert.That(timeline.TransitionCount, Is.EqualTo(36));
            Assert.That(timeline.Stages[0].TransitionSummaryText.Length, Is.GreaterThan(512));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++)
            {
                ProjectStressWorkspace(
                    viewModel,
                    displayName,
                    compile,
                    payload,
                    library,
                    libraryState);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));
        }
    }
}
