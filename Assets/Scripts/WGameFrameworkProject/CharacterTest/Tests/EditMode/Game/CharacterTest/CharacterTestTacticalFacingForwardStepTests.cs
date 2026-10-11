using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalFacingForwardStepTests
    {
        private static CharacterTestTacticalCardInputs Inputs(int frames = 46)
        {
            var action = new CharacterActionConfig(new CharacterActionId(950008), "forward step",
                CharacterActionCategory.Dodge, CharacterActionTimelineAuthority.CharacterAuthored,
                Array.Empty<string>(), 0, frames, null, null, null, null);
            return CharacterTestTacticalCardInputs.BindSlots(1, new Dictionary<string, CharacterTestTacticalCardInputs>(),
                stepForward: action, stepForwardHash: "test-forward-" + frames);
        }

        private static CharacterTestTacticalAuthoringCompilation Compile(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalCardInputs inputs = null)
        {
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, inputs ?? Inputs(),
                out var result, out var issues), string.Join(";", issues));
            return result;
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void ForwardStepShortTextKeepsTimeoutPolicyInDetails(bool continueOnTimeout, bool localized)
        {
            const string path = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";
            var action = CharacterActionJsonImporter.ImportActions(File.ReadAllText(path), path)
                .Actions.Single(a => a.ActionId.Value == CharacterTestCombatEvadeRuntime.ForwardStepActionId);
            var inputs = CharacterTestTacticalCardInputs.BindSlots(0,
                new Dictionary<string, CharacterTestTacticalCardInputs>(), stepForward: action);
            var source = CharacterTestTacticalAuthoringTemplates.CreateFacingForwardStep();
            var timeline = source.Timelines.Single(); var block = timeline.EventBlocks.Single();
            block.ContinueToActionOnPreparationTimeout = continueOnTimeout;
            if (!continueOnTimeout)
            {
                var facing = timeline.Events.Single(e => e.EventId == "aligned").ConditionRoot.Children[1].Clone();
                facing.NodeId = "step.angle.scope"; facing.Children[0].NodeId = "step.angle";
                facing.Children[0].Predicate.ConditionId = "step.angle";
                timeline.Events.Single(e => e.EventId == "step").ConditionRoot = facing;
            }
            var definition = new CharacterTestTacticalSharedDefinition {
                DefinitionId = "forward-short-test", Revision = 2, Source = source };
            var document = new CharacterTestTacticalDslSource { CardId = "forward-short-test",
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new() { NodeId = "forward", Kind = "Reference", DefinitionId = definition.DefinitionId } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { definition } };
            string before = CharacterTestTacticalDslJson.Write(document);
            var provider = localized ? CharacterTestLocalizationRuntime.Create(
                "Assets/StreamingAssets/MxFramework/Localization/character_test.tsv").TextProvider : null;
            var description = CharacterTestTacticalDslDescription.GenerateLocalized(document, inputs,
                cardFace: true, textProvider: provider);
            Assert.IsTrue(description.HasStructuredDescription, description.CompilationIssue);
            var hint = description.Segments.Single().FacingStep;
            Assert.AreEqual("向前垫步", description.Text);
            Assert.AreEqual("向前垫步", hint.Text);
            StringAssert.Contains(continueOnTimeout ? "达标或超时" : "转向超时则结束", hint.DetailsText);
            StringAssert.Contains(hint.DetailsText, description.Details);
            StringAssert.Contains("准备上限 0.5 秒", hint.DetailsText);
            StringAssert.Contains("执行上限 5 秒", hint.DetailsText);
            StringAssert.Contains("0.5", hint.PreparationText);
            StringAssert.Contains("5", hint.FacingText);
            Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(document));
            Assert.AreEqual(action.ActionId, inputs.StepForwardAction.ActionId);
        }

        [TestCase(-1, 31)]
        [TestCase(6, 7)]
        [TestCase(30, 31)]
        [TestCase(31, 31)]
        public void AlignmentOrItsBudgetLeadsToExactlyOneStep(int facingFrame, int expectedStart)
        {
            var compiled = Compile(CharacterTestTacticalAuthoringTemplates.CreateFacingForwardStep());
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Facing = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            for (int frame = 1; frame <= 35; frame++)
            {
                if (frame == facingFrame) runtime.Facing = true;
                evaluator.Tick(frame);
                Assert.AreEqual(frame < expectedStart ? 0 : 1, runtime.StartRequests, "world frame " + frame);
                runtime.Commit();
            }
            Assert.AreEqual(950008, runtime.LastRequestedAction);
            Assert.AreNotEqual(CharacterTestBehaviorExecutionEndReason.TimedOut, evaluator.EndReason);
            runtime.CompleteActualAction(); evaluator.Tick(36);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
        }

        [TestCase("target", "None", 1)]
        [TestCase("permission", "None", 1)]
        [TestCase("admission", "ActionRejected", 1)]
        public void PlaybackEntryDoesNotAddTargetOrPermissionConditions(string failure, string reason, int requests)
        {
            var compiled = Compile(CharacterTestTacticalAuthoringTemplates.CreateFacingForwardStep());
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Facing = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            for (int frame = 1; frame <= 30; frame++) evaluator.Tick(frame);
            if (failure == "target") runtime.TargetExists = false;
            if (failure == "permission") runtime.ActionAllowed = false;
            if (failure == "admission") runtime.AcceptAction = false;
            evaluator.Tick(31); evaluator.Tick(32);
            Assert.AreEqual(requests, runtime.StartRequests);
            Assert.AreEqual(reason, evaluator.EndReason.ToString());
        }

        [Test]
        public void OtherActionBlocksKeepTheirFailOnPreparationTimeoutPolicy()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateFacingForwardStep();
            source.Timelines[0].EventBlocks[0].ContinueToActionOnPreparationTimeout = false;
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Facing = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(source).Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            for (int frame = 1; frame <= 31; frame++) evaluator.Tick(frame);
            Assert.AreEqual(0, runtime.StartRequests);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TimedOut, evaluator.EndReason);
        }

        [Test]
        public void LegacyPlaybackConditionsDoNotCompileOrDemandSpatialSummary()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            var timeline = source.Timelines[0];
            var action = timeline.Events.Single(e => e.EventId == "attack");
            action.ConditionRoot = timeline.Events.Single(e => e.EventId == "release").ConditionRoot;
            action.Operation.SlotId = "forward";
            action.Start = CharacterTestTacticalAuthoringTemplates.Absolute(0);
            source.Slots.Clear();
            source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "forward", Resolver = "stepForward" });
            source.ActivationConditionRoot = null;
            timeline.Events.RemoveAll(e => e != action);
            Assert.AreEqual(CharacterTestTacticalInputRequirements.StepForward, CharacterTestTacticalDependencies.Analyze(source));
            var compiled = Compile(source);
            Assert.AreEqual(-1, compiled.Program.Execution.Events.ToArray().Single(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction).Condition);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { TargetExists = false, Facing = false, Opportunity = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            evaluator.Tick(1);
            Assert.AreEqual(1, runtime.StartRequests);
            Assert.AreEqual(0, runtime.EvaluateCount);
        }

        [TestCase(.5f, 92)]
        [TestCase(1f, 46)]
        [TestCase(2f, 23)]
        public void FunctionalActionSpanUsesRetimedDurationInPreviewAndCompilation(float rate, int frames)
        {
            var dto = new CharacterActionJsonDto { ActionId = 950008, DurationFrames = 46,
                TimelineAuthority = "CharacterAuthored", Phases = Array.Empty<CharacterActionPhaseJsonDto>() };
            Assert.IsTrue(CharacterActionRateUtility.TryCreateScaledAction(dto, rate, out var scaled, out string error), error);
            Assert.AreEqual(frames, scaled.DurationFrames);
            var source = CharacterTestTacticalAuthoringTemplates.CreateFacingForwardStep();
            var inputs = Inputs(scaled.DurationFrames.Value);
            Assert.IsNull(source.Timelines[0].EventBlocks[0].Timing, "No editable block-length padding for this functional intent.");
            var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, inputs, "");
            Assert.IsTrue(preview.Ready, preview.Diagnostic);
            preview.SetEventFocus("facing-forward-step");
            Assert.AreEqual(frames, preview.DisplayPayload.Timelines[0].Events.Single(e => e.EventId == "step").DurationFrames);
            var timing = new CharacterTestTacticalTimingEvaluation(source, inputs, true); timing.Evaluate();
            Assert.IsEmpty(timing.Issues);
            Assert.AreEqual(frames, timing.Position("step", "End") - timing.Position("step", "Start"));
            Assert.AreEqual(frames + 1, timing.Position("facing-forward-step", "End"));
            var compiled = Compile(source, inputs);
            var step = compiled.Events.ToArray().Single(e => e.EventId == "step");
            Assert.AreEqual(frames, step.EndFrame - step.StartFrame);
            Assert.AreEqual(30, compiled.Program.Execution.Blocks[0].PreparationTimeoutFrames);
            Assert.AreEqual(300, compiled.Program.Execution.Blocks[0].CompletionTimeoutFrames);
        }

        [Test]
        public void TimeoutPolicyRoundTripsAndSurvivesCompositionWithDistinctIdentity()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateFacingForwardStep();
            Assert.IsTrue(source.Clone().Timelines[0].EventBlocks[0].ContinueToActionOnPreparationTimeout);
            long identity = Compile(source).Program.Execution.Identity;
            source.Timelines[0].EventBlocks[0].ContinueToActionOnPreparationTimeout = false;
            Assert.AreNotEqual(identity, Compile(source).Program.Execution.Identity);
            source.Timelines[0].EventBlocks[0].ContinueToActionOnPreparationTimeout = true;
            var document = new CharacterTestTacticalDslSource { CardId = "forward-test", Root = new CharacterTestTacticalDslNode {
                NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new() { NodeId = "step-ref", Kind = "Reference", DefinitionId = "forward" } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new() { DefinitionId = "forward", Source = source } } };
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, Inputs(), out var compiled, out string error), error);
            Assert.IsTrue(compiled.Program.Execution.Blocks[0].ContinueToActionOnPreparationTimeout);
        }
    }
}
