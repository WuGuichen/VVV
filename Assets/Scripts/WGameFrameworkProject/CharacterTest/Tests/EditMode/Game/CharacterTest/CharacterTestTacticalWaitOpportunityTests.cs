using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalWaitOpportunityTests
    {
        private static CharacterTestBehaviorExecutionProgram CompileFacing(int maximum = 3)
        {
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(
                CharacterTestTacticalAuthoringTemplates.CreateFacingTarget(5, maximum), Inputs(), out var compiled, out var issues),
                string.Join(";", issues));
            return compiled.Program.Execution;
        }

        [TestCase(0f, true)]
        [TestCase(4.99f, true)]
        [TestCase(5f, false)]
        public void FacingIntentUsesStrictThresholdAtEntry(float angle, bool complete)
        {
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { FacingAngle = angle };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(CompileFacing(), runtime);
            evaluator.Start(0); evaluator.Tick(1);
            Assert.AreEqual(complete ? CharacterTestRuntimeAiLabLogicRuntimeState.Completed : CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            Assert.AreEqual(Vector2.zero, runtime.Move); Assert.AreEqual(0, runtime.StartRequests);
            if (complete) Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
            else Assert.AreEqual(2, runtime.Control.FacingMode);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FacingIntentDeadlineAcceptsAlignmentOtherwiseTimesOut(bool aligned)
        {
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { FacingAngle = 90 };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(CompileFacing(), runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2); evaluator.Tick(3);
            runtime.FacingAngle = aligned ? 0 : 90; evaluator.Tick(4);
            Assert.AreEqual(aligned ? CharacterTestBehaviorExecutionEndReason.Completed : CharacterTestBehaviorExecutionEndReason.TimedOut, evaluator.EndReason);
            Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease); Assert.AreEqual(0, runtime.StartRequests); Assert.AreEqual(0, runtime.StopRequests);
        }

        [Test]
        public void FacingIntentTracksCurrentConditionAndFailsOnTargetLoss()
        {
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { FacingAngle = 90 };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(CompileFacing(), runtime);
            evaluator.Start(0); evaluator.Tick(1); runtime.TargetExists = false; runtime.FacingAngle = 0; evaluator.Tick(2);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TargetMissing, evaluator.EndReason);
            Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
        }

        [Test]
        public void FacingIntentAuthorDefinitionRoundTripsAndCompilesWithoutActions()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateFacingTargetDefinition();
            var restored = CharacterTestTacticalCompositionJson.ReadShared(CharacterTestTacticalCompositionJson.WriteShared(definition));
            Assert.AreEqual("面向目标", restored.Source.Name); Assert.AreEqual(0, restored.Source.Slots.Count);
            var events = restored.Source.Timelines[0].Events.ToArray();
            Assert.IsTrue(CharacterTestTacticalFacingTargetHint.TryParse("facing.use", restored.Source.Timelines[0].EventBlocks[0], events, out var hint));
            Assert.AreEqual("转向当前目标", hint.Text);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(restored.Source, Inputs(), out _, out var issues), string.Join(";", issues));
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            int cards = workspace.Cards.Count, groups = workspace.Groups.Count;
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(restored, workspace.CurrentRevision, out var issue), issue);
            Assert.AreEqual(cards, workspace.Cards.Count); Assert.AreEqual(groups, workspace.Groups.Count);
            var dsl = new CharacterTestTacticalDslSource { CardId = "facing.validation", Name = "面向目标",
                Definitions = workspace.CloneTacticalDefinitions().ToList(),
                Root = new CharacterTestTacticalDslNode { NodeId = "facing.root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode>() } };
            dsl.Root.Children.Add(new CharacterTestTacticalDslNode { NodeId = "facing.reference", Kind = "Reference", DefinitionId = restored.DefinitionId });
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(dsl, Inputs(), out var compilation, out issue), issue);
            Assert.AreEqual(CharacterTestBehaviorBlockCompletion.Facing, compilation.Program.Execution.Blocks[0].Completion);
        }

        [Test]
        public void FacingIntentUnavailableAngleDoesNotSucceedThroughNegation()
        {
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { FacingAngle = float.NaN };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(CompileFacing(1), runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TimedOut, evaluator.EndReason);
        }

        private static CharacterTestTacticalCardInputs Inputs()
            => CharacterTestTacticalCardInputs.BindSlots(1, new Dictionary<string, CharacterTestTacticalCardInputs>());
        private static CharacterTestBehaviorExecutionProgram Compile(int maximum = 3)
        {
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(
                CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(maximum), Inputs(), out var compiled, out var issues),
                string.Join(";", issues));
            return compiled.Program.Execution;
        }

        [Test]
        public void WaitingReleasesOnceAndDoesNotSubmitFurtherControlsOrActions()
        {
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1);
            Assert.AreEqual(Vector2.zero, runtime.ControlRuntime.Move); Assert.IsTrue(runtime.ControlRuntime.Control.IsOwnerOnlyRelease);
            evaluator.Tick(2); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            Assert.AreEqual(0, runtime.ControlRuntime.StartRequests); Assert.AreEqual(0, runtime.ControlRuntime.StopRequests);
            Assert.AreEqual(1, runtime.Captures);
            Assert.AreEqual(1, runtime.Controls.Count, "Waiting must not publish a movement/facing claim each frame.");
        }

        [Test]
        public void CapturedWindowExitCompletesEarlyAndReleasesOnlyOwnControl()
        {
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(60), runtime);
            evaluator.Start(0); evaluator.Tick(1); long owner = runtime.ControlRuntime.Control.OwnerToken;
            runtime.Active = false; evaluator.Tick(2);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
            Assert.IsTrue(runtime.ControlRuntime.Control.IsRelease); Assert.AreEqual(owner, runtime.ControlRuntime.Control.OwnerToken);
            Assert.AreEqual(0, runtime.ControlRuntime.StartRequests);
        }

        [Test]
        public void NoEffectiveWindowAtEntryKeepsEntryBoundaryWithoutClaimingNeutralControl()
        {
            var runtime = new WaitRuntime { Windows = Array.Empty<int>(), Instance = 0, Active = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            evaluator.Start(0); evaluator.Tick(1);
            Assert.IsTrue(runtime.ControlRuntime.Control.IsOwnerOnlyRelease, "Entry releases only matching owned input.");
            runtime.Instance = 102; runtime.Windows = new[] { 99 }; runtime.Active = true;
            evaluator.Tick(2);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(1, runtime.Captures);
            Assert.AreEqual(1, runtime.Controls.Count);
        }

        [Test]
        public void TimeoutIsExplicitAndDoesNotTriggerAttack()
        {
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            evaluator.Start(0); for (int frame = 1; frame <= 4; frame++) evaluator.Tick(frame);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Failed, evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TimedOut, evaluator.EndReason);
            Assert.AreEqual(0, runtime.ControlRuntime.StartRequests); Assert.IsTrue(runtime.ControlRuntime.Control.IsRelease);
        }

        [Test]
        public void WindowExitOnExactBudgetFrameWinsOverTimeout()
        {
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2); evaluator.Tick(3);
            runtime.Active = false; evaluator.Tick(4);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TargetLossAndRetargetAreFailuresEvenIfWindowEnded(bool replacement)
        {
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            evaluator.Start(0); evaluator.Tick(1); runtime.Active = false;
            if (replacement) runtime.Target = 23; else runtime.ControlRuntime.TargetExists = false;
            evaluator.Tick(2);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TargetMissing, evaluator.EndReason);
        }

        [Test]
        public void MissingObserverIsRejectedRatherThanFabricatingWindowCompletion()
        {
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            evaluator.Start(0); evaluator.Tick(1);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.ConditionRejected, evaluator.EndReason);
        }

        [Test]
        public void MissingCompletedFrameIsRejectedIndependentlyFromTargetLoss()
        {
            var runtime = new WaitRuntime { CanObserve = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), runtime);
            evaluator.Start(0); evaluator.Tick(1);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.ConditionRejected, evaluator.EndReason);
        }

        [Test]
        public void SnapshotRestoresCapturedSetWithoutRecaptureAndHasIndependentStorage()
        {
            var program = Compile(60); var runtime = new WaitRuntime();
            var first = new CharacterTestBehaviorExecutionEvaluator(program, runtime); first.Start(0); first.Tick(1);
            var snapshot = first.CreateSnapshot(); var secondRuntime = new WaitRuntime();
            var second = new CharacterTestBehaviorExecutionEvaluator(program, secondRuntime);
            Assert.IsTrue(second.TryRestoreSnapshot(snapshot)); snapshot.AttackWaitWindowIds[0] = 98;
            Assert.AreEqual(7, second.CreateSnapshot().AttackWaitWindowIds[0]);
            secondRuntime.Active = false; second.Tick(2);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, second.EndReason);
            Assert.AreEqual(0, secondRuntime.Captures);
        }

        [Test]
        public void WindowIdentityContributesToStateHash()
        {
            var program = Compile(); var first = new CharacterTestBehaviorExecutionEvaluator(program, new WaitRuntime());
            var second = new CharacterTestBehaviorExecutionEvaluator(program, new WaitRuntime { Windows = new[] { 8 } });
            first.Start(0); second.Start(0); first.Tick(1); second.Tick(1);
            var a = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1);
            var b = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1);
            first.AddHash(a); second.AddHash(b); Assert.AreNotEqual(a.ToHash(), b.ToHash());
        }

        [Test]
        public void PreviousSnapshotVersionAndMalformedCaptureAreRejected()
        {
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(), new WaitRuntime()); evaluator.Start(0); evaluator.Tick(1);
            var snapshot = evaluator.CreateSnapshot(); snapshot.Version = 5; Assert.IsFalse(evaluator.TryRestoreSnapshot(snapshot));
            snapshot.Version = CharacterTestBehaviorExecutionState.CurrentVersion;
            snapshot.AttackWaitWindowIds = new[] { 7, 7 }; Assert.IsFalse(evaluator.TryRestoreSnapshot(snapshot));
        }

        [Test]
        public void ExistingIntentKeepsV5JsonHashAndRestoreContract()
        {
            var builder = new CharacterTestBehaviorExecutionBuilder(null, "main");
            builder.AddTimeline("main", 10);
            var program = builder.Build().Execution;
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program, new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub());
            evaluator.Start(0); evaluator.Tick(1);
            var snapshot = evaluator.CreateSnapshot();
            Assert.AreEqual(5, snapshot.Version);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(snapshot);
            StringAssert.DoesNotContain("AttackWait", json);
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTestBehaviorExecutionState>(json);
            var second = new CharacterTestBehaviorExecutionEvaluator(program, new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub());
            Assert.IsTrue(second.TryRestoreSnapshot(restored));
            var firstHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1);
            var secondHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1);
            evaluator.AddHash(firstHash); second.AddHash(secondHash);
            Assert.AreEqual(firstHash.ToHash(), secondHash.ToHash());
            Assert.IsTrue(second.CanCopySnapshotTo(snapshot));
            snapshot.AttackWaitWindowIds = new[] { 1 }; Assert.IsFalse(second.TryRestoreSnapshot(snapshot));
        }

        [Test]
        public void AuthoringJsonAndPreviewPreserveConfigurableLimitAndCondition()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(90).Clone();
            Assert.IsEmpty(source.Slots); Assert.AreEqual("WaitForCondition", source.Timelines[0].Events.Single().Operation.Kind);
            Assert.AreEqual(90, source.Timelines[0].EventBlocks.Single().Timing.DurationFrames);
            Assert.IsEmpty(CharacterTestTacticalAuthoringCompiler.ValidateShared(source));
            var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, Inputs(), "");
            Assert.IsTrue(preview.Ready, preview.Diagnostic);
            Assert.AreEqual(90, Compile(90).Blocks[0].Duration);
            Assert.AreNotEqual(Compile(90).Identity, Compile(91).Identity);
        }

        [Test]
        public void CompositionUsesSameWaitBlockAndContinuesAfterIt()
        {
            var definition = new CharacterTestTacticalSharedDefinition { DefinitionId = "tactical.shared.wait-opportunity",
                Source = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(60) };
            var source = new CharacterTestTacticalDslSource { CardId = "wait-test", Root = new CharacterTestTacticalDslNode {
                NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new() { NodeId = "first", Kind = "Reference", DefinitionId = definition.DefinitionId },
                    new() { NodeId = "second", Kind = "Reference", DefinitionId = definition.DefinitionId } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { definition } };
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, Inputs(), out var result, out string issue), issue);
            Assert.AreEqual(2, result.Program.Execution.Blocks.Length);
            var runtime = new WaitRuntime { Active = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(result.Program.Execution, runtime);
            evaluator.Start(0);
            for (int frame = 1; frame <= 8; frame++)
            { evaluator.Tick(frame); runtime.ControlRuntime.Commit(); }
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(2, runtime.Captures); Assert.AreEqual(0, runtime.ControlRuntime.StartRequests);
        }

        [Test]
        public void CapturedWindowFactCannotBecomeAnActivationOracle()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity();
            source.ActivationConditionRoot = source.Timelines[0].Events[0].ConditionRoot;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Any(d => d.Code == "AttackWaitScope"));
        }

        [Test]
        public void LimitMustBePositive()
        { Assert.Throws<ArgumentOutOfRangeException>(() => CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(0)); }

        [Test]
        public void DescriptionIsOneLineAndFallsBackWhenConditionIsCustomized()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunityDefinition(90);
            var source = definition.Source; var timeline = source.Timelines.Single();
            var node = new CharacterTestTacticalDslNode { NodeId = "wait-instance", Kind = "Reference", DefinitionId = definition.DefinitionId };
            Assert.IsTrue(CharacterTestTacticalBlockDescription.TryParse(definition, node, timeline.Events.ToArray(), Inputs(),
                new Dictionary<string, string>(), null, null, 1.0 / 60, out var segments));
            Assert.IsNotNull(segments.Single().WaitOpportunity); Assert.AreEqual("等待指定条件成立后，继续后续行为", segments.Single().Text);
            timeline.Events[0].ConditionRoot.Children.RemoveAt(0);
            Assert.IsFalse(CharacterTestTacticalBlockDescription.TryParse(definition, node, timeline.Events.ToArray(), Inputs(),
                new Dictionary<string, string>(), null, null, 1.0 / 60, out _));
        }

        [Test]
        public void SharedDefinitionIsAddedByWorkspaceTransactionWithoutInstallingCards()
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            var cardsBefore = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Cards);
            var groupsBefore = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Groups);
            int revision = workspace.CurrentRevision;
            var definition = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunityDefinition();
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(definition, revision, out string issue), issue);
            Assert.AreEqual(revision + 1, workspace.CurrentRevision);
            Assert.IsTrue(workspace.CloneTacticalDefinitions().Any(d => d.DefinitionId == definition.DefinitionId && d.Revision == 2 && d.Source.Name == "等待条件"));
            Assert.AreEqual(cardsBefore, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Cards));
            Assert.AreEqual(groupsBefore, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Groups));
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(definition, workspace.CurrentRevision, out issue), issue);
            Assert.AreEqual(revision + 1, workspace.CurrentRevision, "Identical repeated registration must be a no-op.");
        }

        [Test]
        public void EditedConditionCannotBypassTheCapturedAttackWindowBarrier()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(3);
            source.Timelines[0].Events[0].ConditionRoot.Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, Inputs(), out var result, out var issues), string.Join(";", issues));
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(result.Program.Execution, runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            runtime.Active = false; evaluator.Tick(3);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void WaitingHasOneFixedMinimumBudgetAndOnlyMaximumIsEditable(int minimum)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity();
            source.Timelines[0].EventBlocks[0].Timing.MinimumFrames = minimum;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Any(d => d.Code == "BlockWaitContract"));
        }

        [Test]
        public void CapturedFactCannotBeMixedIntoDslConditionsWithEventSignals()
        {
            var definition = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunityDefinition();
            var rootCondition = definition.Source.Timelines[0].Events[0].ConditionRoot;
            rootCondition.Children.Add(new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "result", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                SourceEventId = "unused", Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed });
            var source = new CharacterTestTacticalDslSource { CardId = "wait-scope", Root = new CharacterTestTacticalDslNode {
                NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new() { NodeId = "wait", Kind = "Wait", Condition = rootCondition,
                        Children = new List<CharacterTestTacticalDslNode>(), MaximumWaitFrames = 60 } } } };
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, Inputs(), out _, out string issue));
            StringAssert.Contains("DSL_ATTACK_WAIT_SCOPE", issue);
        }

        [Test]
        public void OwnerQualifiedReleaseClearsOldMovementAndTurnButPreservesIndependentActionRequests()
        {
            var owned = new CharacterTestBehaviorControlInput(2, 1200, 3400, ownerToken: 77);
            var input = new CharacterTestCharacterInputComponent(Vector2.right, false, false, false, false, false,
                false, false, Vector2.right, true, abilitySlotPressed: 2, abilityIdPressed: 701,
                turnInPlace: true, turnInPlaceYawMilliDegrees: 30000, controlIntentSourceId: 88, behaviorControl: owned);
            Assert.IsTrue(input.TryReleaseOwnedBehaviorControl(77, 88, out var released));
            Assert.AreEqual(Vector2.zero, released.Move); Assert.IsFalse(released.HasWorldMove);
            Assert.IsFalse(released.TurnInPlace); Assert.IsFalse(released.BehaviorControl.IsPresent);
            Assert.AreEqual(0, released.ControlIntentSourceId);
            Assert.AreEqual(2, released.AbilitySlotPressed); Assert.AreEqual(701, released.AbilityIdPressed);
            Assert.IsFalse(released.AttackPrimaryPressed); Assert.IsFalse(released.AttackSecondaryPressed);
            Assert.IsFalse(released.DodgePressed); Assert.AreEqual(0, released.SkillSlotPressedMask);
        }

        [TestCase(76, 88)]
        [TestCase(77, 89)]
        public void OwnerQualifiedReleaseCannotOverwriteOtherController(long token, int source)
        {
            var input = new CharacterTestCharacterInputComponent(Vector2.left, false, false, false, true, false,
                false, false, Vector2.left, true, controlIntentSourceId: 88,
                behaviorControl: new CharacterTestBehaviorControlInput(2, 1000, 2000, ownerToken: 77));
            Assert.IsFalse(input.TryReleaseOwnedBehaviorControl(token, source, out var preserved));
            Assert.AreEqual(Vector2.left, preserved.Move); Assert.IsTrue(preserved.AttackPrimaryPressed);
            Assert.AreEqual(77, preserved.BehaviorControl.OwnerToken); Assert.AreEqual(2, preserved.BehaviorControl.FacingMode);
        }

        [Test]
        public void OrdinaryExternalControlAndBufferedRequestsAreNotClaimedByWait()
        {
            var external = new CharacterTestCharacterInputComponent(Vector2.left, false, false, false, true, false,
                false, false, controlIntentSourceId: 88);
            Assert.IsFalse(external.TryReleaseOwnedBehaviorControl(77, 88, out var preserved));
            Assert.AreEqual(Vector2.left, preserved.Move); Assert.IsTrue(preserved.AttackPrimaryPressed);
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(60), runtime);
            evaluator.Start(0); evaluator.Tick(1);
            for (int frame = 2; frame < 10; frame++) evaluator.Tick(frame);
            Assert.AreEqual(1, runtime.Controls.Count);
            Assert.IsTrue(runtime.Controls.All(c => c.IsOwnerOnlyRelease && c.IsRelease));
            Assert.AreEqual(0, runtime.ControlRuntime.StartRequests); Assert.AreEqual(0, runtime.ControlRuntime.StopRequests);
        }

        [Test]
        public void ExistingActionContinuesWhileConditionIsPending()
        {
            var runtime = new WaitRuntime();
            Assert.IsTrue(runtime.ControlRuntime.TryStartAction(301)); runtime.ControlRuntime.Commit();
            long instance = runtime.ActualActionInstanceId; runtime.ControlRuntime.CompleteActionCommand();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(60), runtime);
            evaluator.Start(0); for (int frame = 1; frame <= 10; frame++) evaluator.Tick(frame);
            Assert.AreEqual(instance, runtime.ActualActionInstanceId);
            Assert.AreEqual(0, runtime.ControlRuntime.StopRequests); Assert.AreEqual(1, runtime.ControlRuntime.StartRequests);
        }

        [Test]
        public void RestoredWaitDoesNotCaptureOrReleaseAgainAndIgnoresRepeatedSameFrame()
        {
            var runtime = new WaitRuntime(); var program = Compile(60);
            var first = new CharacterTestBehaviorExecutionEvaluator(program, runtime); first.Start(0); first.Tick(1);
            first.Tick(1); Assert.AreEqual(1, runtime.Controls.Count); Assert.AreEqual(1, runtime.Captures);
            var restoredRuntime = new WaitRuntime(); var restored = new CharacterTestBehaviorExecutionEvaluator(program, restoredRuntime);
            Assert.IsTrue(restored.TryRestoreSnapshot(first.CreateSnapshot())); restored.Tick(2);
            Assert.AreEqual(0, restoredRuntime.Captures); Assert.IsEmpty(restoredRuntime.Controls);
            restoredRuntime.Active = false; restored.Tick(3);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, restored.EndReason);
            Assert.IsEmpty(restoredRuntime.Controls);
        }

        [Test]
        public void LegacyR1RemainsNeutralControlWaitWithDistinctCompatibleIdentity()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(60);
            source.Name = "等待战机"; source.Timelines[0].Title = source.Name;
            source.Timelines[0].EventBlocks[0].Title = source.Name;
            source.Timelines[0].EventBlocks[0].PassiveWait = false;
            string json = CharacterTestTacticalAuthoringJson.Write(source);
            StringAssert.DoesNotContain("passiveWait", json);
            var legacy = CharacterTestTacticalAuthoringJson.Read(json);
            Assert.IsFalse(legacy.Timelines[0].EventBlocks[0].PassiveWait);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(legacy, Inputs(), out var compiled, out var issues), string.Join(";", issues));
            Assert.AreNotEqual(Compile(60).Identity, compiled.Program.Execution.Identity);
            var runtime = new WaitRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2);
            Assert.AreEqual(2, runtime.Controls.Count); Assert.IsFalse(runtime.Controls.Last().IsRelease);
            var control = new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: 77, isRelease: true);
            StringAssert.DoesNotContain("IsOwnerOnlyRelease", Newtonsoft.Json.JsonConvert.SerializeObject(control));
            var command = CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(1), 88, 1, Vector2.zero, 1, control);
            Assert.AreEqual(1, command.Envelope.SchemaVersion); Assert.IsTrue(CharacterTestRuntimeCommandFactory.IsBehaviorControlEnvelopeValid(command.Envelope));
        }

        [Test]
        public void OwnerOnlyReleaseRoundTripsWithVersionedEnvelopeAndStableSnapshot()
        {
            var control = new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: 77, isRelease: true, isOwnerOnlyRelease: true);
            var command = CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(1), 88, 1, Vector2.zero, 1, control);
            Assert.AreEqual(2, command.Envelope.SchemaVersion); Assert.IsTrue(CharacterTestRuntimeCommandFactory.IsBehaviorControlEnvelopeValid(command.Envelope));
            var decoded = CharacterTestRuntimeCommandFactory.DecodeBehaviorControl(command.Envelope);
            Assert.IsTrue(decoded.IsOwnerOnlyRelease); Assert.AreEqual(77, decoded.OwnerToken);
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTestBehaviorControlInput>(Newtonsoft.Json.JsonConvert.SerializeObject(control));
            Assert.IsTrue(restored.IsOwnerOnlyRelease); Assert.IsTrue(restored.IsRelease); Assert.AreEqual(77, restored.OwnerToken);
        }

        private sealed class WaitRuntime : ICharacterTestBehaviorExecutionRuntime, ICharacterTestBehaviorExecutionAttackWindowRuntime
        {
            internal readonly CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub ControlRuntime = new();
            internal readonly List<CharacterTestBehaviorControlInput> Controls = new();
            internal int Target = 22, Captures; internal long Instance = 101;
            internal int[] Windows = { 7 }; internal bool Active = true, CanObserve = true;
            public bool TryCaptureTargetAttackWindows(out int target, out long instance, out int[] windows)
            { Captures++; target = Target; instance = Instance; windows = (int[])Windows.Clone(); return CanObserve && ControlRuntime.TargetExists; }
            public bool IsCapturedTargetAvailable(int target) => ControlRuntime.TargetExists && target == Target;
            public bool AnyCapturedTargetAttackWindowActive(int target, long instance, int[] windows)
                => Active && windows.Length > 0 && instance == Instance && windows.Intersect(Windows).Any();
            public int EntityId => ControlRuntime.EntityId;
            public bool Evaluate(in CharacterTestBehaviorExecutionPredicate predicate) => ControlRuntime.Evaluate(predicate);
            public bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject) => ControlRuntime.IsSubjectAvailable(subject);
            public bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement movement, out CharacterTestRuntimeAiLabMovementTargetSample sample) => ControlRuntime.TrySampleMovement(movement, out sample);
            public bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn turn, out int x, out int z) => ControlRuntime.TrySampleTurn(turn, out x, out z);
            public bool TryStartAction(int id) => ControlRuntime.TryStartAction(id);
            public bool TryStopAction(long instance) => ControlRuntime.TryStopAction(instance);
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState => ControlRuntime.ActionCommandState;
            public CharacterTestRuntimeAiLabLogicActionState ActualActionState => ControlRuntime.ActualActionState;
            public long ActualActionInstanceId => ControlRuntime.ActualActionInstanceId;
            public long StartedActionInstanceId => ControlRuntime.StartedActionInstanceId;
            public void CompleteActionCommand() => ControlRuntime.CompleteActionCommand();
            public void ObserveOwnedAction(long instance) => ControlRuntime.ObserveOwnedAction(instance);
            public void SetExecutionControl(Vector2 move, float speed, in CharacterTestBehaviorControlInput control)
            { Controls.Add(control); ControlRuntime.SetExecutionControl(move, speed, control); }
        }
    }
}

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalPurposeWaitTests
    {
        private static CharacterTestTacticalAuthoringSource FixedDistance(float minimum = 0, float maximum = 2, int frames = 4, string metric = "RootPlanarDistance")
            => CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistance(new CharacterTestTacticalDistanceData {
                Minimum = minimum, Maximum = maximum, Metric = metric }, frames);
        private static CharacterTestRuntimeAiLabCompiledLogicProgram Compile(CharacterTestTacticalAuthoringSource source)
        {
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, CharacterTestTacticalCardInputs.Unbound(1),
                out var compiled, out var issues), string.Join(";", issues)); return compiled.Program;
        }

        [Test]
        public void TwoSecondsUsesFixedSimulationDurationAndCompletesNormally()
        {
            var program = Compile(CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds());
            var runtime = new PassiveRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(10)); evaluator.Tick(11);
            Assert.AreEqual(CharacterTestDeterministicUnits.DefaultTickRate * 2, program.Execution.Blocks[0].Duration);
            evaluator.Tick(130); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            evaluator.Tick(131); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
            Assert.AreEqual(1, runtime.Controls.Count); Assert.IsTrue(runtime.Controls[0].IsOwnerOnlyRelease);
            Assert.IsTrue(runtime.Controls[0].IsRelease); Assert.AreEqual(0, runtime.Stub.StartRequests); Assert.AreEqual(0, runtime.Stub.StopRequests);
        }

        [Test]
        public void FrozenSimulationFrameDoesNotAdvanceWaitAndRestorePreservesElapsedTime()
        {
            var program = Compile(CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds());
            var a = new CharacterTestBehaviorExecutionEvaluator(program.Execution, new PassiveRuntime()); a.Start(0); a.Tick(1); a.Tick(61);
            var before = a.CreateSnapshot(); for (int i = 0; i < 250; i++) a.Tick(61);
            Assert.AreEqual(before.BlockStarts[0], a.CreateSnapshot().BlockStarts[0]); Assert.AreEqual(before.LocalFrame, a.CreateSnapshot().LocalFrame);
            var runtime = new PassiveRuntime(); var b = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(b.TryRestoreSnapshot(a.CreateSnapshot())); b.Tick(120); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, b.State);
            b.Tick(121); Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, b.EndReason); Assert.IsEmpty(runtime.Controls);
        }

        [Test]
        public void FixedWaitHasNoTargetRequirementAndKeepsIndependentActionAndExternalControl()
        {
            var runtime = new PassiveRuntime(); runtime.Stub.TargetExists = false;
            runtime.Stub.TryStartAction(301); runtime.Stub.Commit(); runtime.Stub.CompleteActionCommand(); long instance = runtime.ActualActionInstanceId;
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds()).Execution, runtime);
            evaluator.Start(0); evaluator.Tick(1);
            var external = new CharacterTestCharacterInputComponent(Vector2.left, false, false, false, false, false, false, false,
                controlIntentSourceId: 88, behaviorControl: new CharacterTestBehaviorControlInput(2, 100, 200, ownerToken: 999));
            Assert.IsFalse(external.TryReleaseOwnedBehaviorControl(runtime.Controls[0].OwnerToken, 88, out var preserved));
            Assert.AreEqual(Vector2.left, preserved.Move); Assert.AreEqual(999, preserved.BehaviorControl.OwnerToken);
            evaluator.Tick(121); Assert.AreEqual(instance, runtime.ActualActionInstanceId); Assert.AreEqual(0, runtime.Stub.StopRequests);
        }

        [TestCase(1.99f, true)] [TestCase(2f, true)] [TestCase(2.01f, false)]
        public void DistanceWaitSamplesInclusiveConfigurationBoundary(float distance, bool completed)
        {
            var runtime = new PassiveRuntime(); runtime.Stub.Distance = distance;
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(FixedDistance()).Execution, runtime); evaluator.Start(0); evaluator.Tick(1);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State); evaluator.Tick(2);
            Assert.AreEqual(completed ? CharacterTestRuntimeAiLabLogicRuntimeState.Completed : CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            Assert.AreEqual(1, runtime.Controls.Count); Assert.AreEqual(0, runtime.Stub.StartRequests); Assert.AreEqual(0, runtime.Stub.StopRequests);
            Assert.IsFalse(evaluator.NeedsPrimaryAttackFact(2));
        }

        [TestCase(true)] [TestCase(false)]
        public void DistanceDeadlineSuccessWinsOtherwiseFailsWithoutContinuation(bool enters)
        {
            var runtime = new PassiveRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(FixedDistance()).Execution, runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(4); if (enters) runtime.Stub.Distance = 2; evaluator.Tick(5);
            Assert.AreEqual(enters ? CharacterTestBehaviorExecutionEndReason.Completed : CharacterTestBehaviorExecutionEndReason.TimedOut, evaluator.EndReason);
            Assert.AreEqual(enters ? CharacterTestRuntimeAiLabLogicRuntimeState.Completed : CharacterTestRuntimeAiLabLogicRuntimeState.Failed, evaluator.State);
        }

        [Test]
        public void MissingTargetFailsBeforeDistanceSuccess()
        {
            var runtime = new PassiveRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(Compile(FixedDistance()).Execution, runtime);
            evaluator.Start(0); evaluator.Tick(1); runtime.Stub.Distance = 0; runtime.Stub.TargetExists = false; evaluator.Tick(2);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TargetMissing, evaluator.EndReason);
        }

        [Test]
        public void DistanceRestoreUsesCurrentTargetDistanceAndSnapshotHashIncludesElapsedProgress()
        {
            var program = Compile(FixedDistance(frames: 60)); var a = new CharacterTestBehaviorExecutionEvaluator(program.Execution, new PassiveRuntime());
            a.Start(0); a.Tick(1); var start = a.CreateSnapshot(); a.Tick(10);
            var h1 = new RuntimeHashAccumulator(1001, 1); start.AddHash(h1); var h2 = new RuntimeHashAccumulator(1001, 1); a.CreateSnapshot().AddHash(h2);
            Assert.AreNotEqual(h1.ToHash(), h2.ToHash());
            var runtime = new PassiveRuntime(); runtime.Stub.Distance = 1; var b = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(b.TryRestoreSnapshot(a.CreateSnapshot())); b.Tick(11); Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, b.EndReason);
            Assert.IsEmpty(runtime.Controls);
        }

        [Test]
        public void FixedDurationAndDistanceSourceCannotBeSilentlyChanged()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds(); source.Timelines[0].EventBlocks[0].Timing.DurationFrames++;
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(source, CharacterTestTacticalCardInputs.Unbound(1), out _, out _));
            var missing = CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistanceReference(5);
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(missing, CharacterTestTacticalCardInputs.Unbound(1), out _, out _));
            var invalid = FixedDistance(); invalid.Timelines[0].EventBlocks[0].DistanceData.Kind = "ExecutionResult";
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(invalid, CharacterTestTacticalCardInputs.Unbound(1), out _, out _));
            invalid = FixedDistance(); invalid.PredicateBindings[0].SourceBlockId = "missing";
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(invalid, CharacterTestTacticalCardInputs.Unbound(1), out _, out _));
        }

        private static CharacterTestTacticalDslSource Composition()
        {
            var wait = new CharacterTestTacticalSharedDefinition { DefinitionId = "wait-ref", Source = CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistanceReference(4) };
            var provider = new CharacterTestTacticalSharedDefinition { DefinitionId = "distance-provider", Source = FixedDistance(maximum: 3) };
            var root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode>() };
            root.Children.Add(new CharacterTestTacticalDslNode { NodeId = "wait", Kind = "Reference", DefinitionId = wait.DefinitionId,
                DistanceBindings = new Dictionary<string, CharacterTestTacticalIntentDistanceReference> { ["wait.distance"] = new() { SourceNodeId = "provider", SourceBlockId = "purpose-wait" } } });
            root.Children.Add(new CharacterTestTacticalDslNode { NodeId = "provider", Kind = "Reference", DefinitionId = provider.DefinitionId });
            return new CharacterTestTacticalDslSource { CardId = "distance-fixture", Root = root, Definitions = new() { wait, provider } };
        }

        [Test]
        public void ForwardReferenceReadsPublishedConfigurationBeforeSourceIntentExecution()
        {
            var source = Composition(); var original = CharacterTestTacticalDslJson.Write(source);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, CharacterTestTacticalCardInputs.Unbound(1), out var result, out string issue), issue);
            Assert.AreEqual(original, CharacterTestTacticalDslJson.Write(source));
            var runtime = new PassiveRuntime(); runtime.Stub.Distance = 2.5f;
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(result.Program.Execution, runtime); evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2);
            Assert.AreEqual(2, evaluator.CreateSnapshot().BlockResults[Array.FindIndex(result.Program.Execution.Blocks.ToArray(), block => block.Timeline == result.Program.Execution.EntryTimeline)]);
            Assert.AreEqual(0, runtime.Stub.StartRequests);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(CharacterTestTacticalDslJson.Read(original), CharacterTestTacticalCardInputs.Unbound(1), out var roundtrip, out issue), issue);
            Assert.AreEqual(result.Program.Execution.Identity, roundtrip.Program.Execution.Identity);
        }

        [TestCase("missing")] [TestCase("result")] [TestCase("export")] [TestCase("unused")] [TestCase("override")]
        public void InvalidCompositionDistanceLinksAreRejected(string kind)
        {
            var source = Composition(); var reference = source.Root.Children[0].DistanceBindings["wait.distance"];
            if (kind == "missing") reference.SourceNodeId = "missing";
            if (kind == "result") reference.DataKind = "ExecutionResult";
            if (kind == "export") reference.SourceBlockId = "missing";
            if (kind == "unused") source.Root.Children[0].DistanceBindings["not-a-leaf"] = reference;
            if (kind == "override") source.Definitions[0].Source.PredicateBindings[0].SourceBlockId = "purpose-wait";
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, CharacterTestTacticalCardInputs.Unbound(1), out _, out _));
        }

        [Test]
        public void ConfigurationMetricAndDistanceChangeIdentityWithoutChangingOldWaitR2()
        {
            var a = Compile(FixedDistance()); var b = Compile(FixedDistance(maximum: 3)); var c = Compile(FixedDistance(metric: "HurtboxSurfaceClearance"));
            Assert.AreNotEqual(a.Execution.Identity, b.Execution.Identity); Assert.AreNotEqual(a.Execution.Identity, c.Execution.Identity);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardConditionFact.TargetDistance, c.Execution.Predicates.ToArray().Last().Fact);
            var old = CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunityDefinition(); Assert.AreEqual(2, old.Revision);
            Assert.AreEqual("Condition", old.Source.Timelines[0].EventBlocks[0].Completion);
            Assert.IsFalse(CharacterTestTacticalAuthoringJson.Write(old.Source).Contains("distanceData"));
            Assert.IsFalse(CharacterTestTacticalAuthoringJson.Write(old.Source).Contains("sourceBlockId"));
        }


        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void TwoSecondsDerivesActualSimulationFrequencyDuringPreparation(int rate)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds();
            var inputs = CharacterTestTacticalCardInputs.Unbound(1).WithSimulationTickRate(rate);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compilation, out var issues), string.Join(";", issues));
            Assert.AreEqual(rate * 2, compilation.Program.Execution.Blocks[0].Duration);
            var runtime = new PassiveRuntime(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(compilation.Program.Execution, runtime);
            evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(rate * 2); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            evaluator.Tick(rate * 2 + 1); Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
            Assert.AreEqual(120, source.Timelines[0].EventBlocks[0].Timing.DurationFrames);
            Assert.AreEqual(rate, inputs.ForInstance("instance").SimulationTickRate);
        }

        [Test]
        public void ExistingWorkspaceTransactionAddsBothWaitIntentsWithoutCreatingCards()
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            string cards = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Cards);
            string groups = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Groups);
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSecondsDefinition(), workspace.CurrentRevision, out string issue), issue);
            Assert.IsTrue(workspace.TryAddReusableTacticalDefinition(new CharacterTestTacticalSharedDefinition {
                DefinitionId = CharacterTestTacticalAuthoringTemplates.WaitTargetDistanceDefinitionId, Source = FixedDistance() }, workspace.CurrentRevision, out issue), issue);
            Assert.AreEqual(cards, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Cards));
            Assert.AreEqual(groups, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Groups));
        }

        [TestCase(false)] [TestCase(true)]
        public void PublishedLegacyWaitUsesExistingFreezeCatalogHashAndRecompile(bool distance)
        {
            var inputs = CharacterTestTacticalCardInputs.Unbound(1);
            var source = distance ? FixedDistance() : CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds();
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft("wait-fixture", "bc_qkjc3ka7za", "Wait fixture", "Tactical", null); draft.SetTacticalSource(source);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var c, out var issues), string.Join(";", issues));
            var compiled = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(c.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, c)), null);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata("Wait fixture", "fixture.icon", 0, "Wait fixture", "", "");
            var published = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(draft.PublishedStableId, 1,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, metadata, CharacterTestAuthoredContentIds.D417_Id3), null, compiled);
            Assert.IsTrue(published.Success);
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, new MxFramework.CharacterAction.CharacterActionConfigRegistry(
                Array.Empty<MxFramework.CharacterAction.CharacterActionSetConfig>(), Array.Empty<MxFramework.CharacterAction.CharacterActionConfig>()));
            var catalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("fixture", 1, new[] { published.Card }, references);
            Assert.IsTrue(catalog.Success);
            var json = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog.Catalog);
            var reopened = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(json, references); Assert.IsTrue(reopened.Success);
            Assert.AreEqual(published.Card.SemanticHash, reopened.Catalog.Cards[0].SemanticHash);
            Assert.AreEqual(published.Card.ContentHash, reopened.Catalog.Cards[0].ContentHash);
            Assert.IsTrue(reopened.Catalog.Cards[0].TacticalSource.TryCompile(draft.PublishedStableId, "Wait fixture", 1, inputs, out var restored, out string issue), issue);
            Assert.AreEqual(c.Program.Execution.Identity, restored.Execution.Identity);
            var tampered = Newtonsoft.Json.Linq.JObject.Parse(json);
            if (distance) tampered["cards"][0]["tacticalSource"]["legacy"]["timelines"][0]["eventBlocks"][0]["distanceData"]["maximum"] = 9;
            else tampered["cards"][0]["tacticalSource"]["legacy"]["timelines"][0]["eventBlocks"][0]["timing"]["durationFrames"] = 180;
            Assert.IsFalse(CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(tampered.ToString(), references).Success);
        }

        [Test]
        public void PublishedCompositionRetainsConfigurationLinksAndRejectsTamperedSource()
        {
            var source = Composition(); var inputs = CharacterTestTacticalCardInputs.Unbound(1);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, inputs, out var compiled, out string issue), issue);
            var frozen = CharacterTestTacticalFrozenSource.CapturePublished("fixture", 1, source, inputs, compiled);
            var published = CharacterTestTacticalPublishedSource.Capture(frozen);
            var reopened = CharacterTestTacticalPublishedSource.Read(published.ToJson()); Assert.AreEqual(published.SemanticHash, reopened.SemanticHash);
            Assert.IsTrue(reopened.TryCompile("fixture", "fixture", 1, inputs, out var program, out issue), issue);
            Assert.AreEqual(compiled.Program.Execution.Identity, program.Execution.Identity);
            var json = reopened.ToJson(); json["composition"]["root"]["children"][0]["distanceBindings"]["wait.distance"]["sourceNodeId"] = "missing";
            var invalid = CharacterTestTacticalPublishedSource.Read(json);
            Assert.IsFalse(invalid.TryCompile("fixture", "fixture", 1, inputs, out _, out _));
            Assert.AreNotEqual(published.SemanticHash, invalid.SemanticHash);
        }

        [Test]
        public void BoundActionDistanceExportRejectsMissingMemberOrUnpreparedData()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds(); var block = source.Timelines[0].EventBlocks[0];
            block.DistanceData = new CharacterTestTacticalDistanceData { Kind = "BoundActionRange", SourceActionEventId = "missing", Metric = "HurtboxSurfaceClearance" };
            Assert.Throws<ArgumentException>(() => CharacterTestTacticalAuthoringCompiler.ResolveDistanceData(source, CharacterTestTacticalCardInputs.Unbound(1), block.BlockId));
        }

        [Test]
        public void NonAttackIntentCanPublishConfigurationDistanceBeforeItExecutes()
        {
            var source = Composition(); var provider = CharacterTestTacticalAuthoringTemplates.CreateFacingTarget(5, 4);
            provider.Timelines[0].EventBlocks[0].DistanceData = new CharacterTestTacticalDistanceData { Maximum = 3 };
            source.Definitions[1].Source = provider;
            source.Root.Children[0].DistanceBindings["wait.distance"].SourceBlockId = provider.Timelines[0].EventBlocks[0].BlockId;
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, CharacterTestTacticalCardInputs.Unbound(1), out var result, out string issue), issue);
            Assert.AreEqual(0, result.Program.Execution.Actions.Length);
            var runtime = new PassiveRuntime(); runtime.Stub.Distance = 2.5f;
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(result.Program.Execution, runtime); evaluator.Start(0); evaluator.Tick(1); evaluator.Tick(2);
            Assert.AreEqual(2, evaluator.CreateSnapshot().BlockResults[Array.FindIndex(result.Program.Execution.Blocks.ToArray(), b => b.Timeline == result.Program.Execution.EntryTimeline)]);
        }

        [Test]
        public void PassiveWaitRejectsConcurrentActiveControlAndDescriptionsKeepDistinctPurposes()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds();
            var timeline = source.Timelines[0]; timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "turn", Start = CharacterTestTacticalAuthoringTemplates.Absolute(0),
                End = CharacterTestTacticalAuthoringTemplates.Absolute(10), Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" } });
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(source, CharacterTestTacticalCardInputs.Unbound(1), out _, out _));
            var simple = new CharacterTestTacticalDslSource { CardId = "text-fixture", Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new() {
                new() { NodeId = "wait", Kind = "Reference", DefinitionId = CharacterTestTacticalAuthoringTemplates.WaitTwoSecondsDefinitionId } } },
                Definitions = new() { CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSecondsDefinition() } };
            StringAssert.Contains("原地等待两秒", CharacterTestTacticalDslDescription.Generate(simple, CharacterTestTacticalCardInputs.Unbound(1)).Text);
            simple.Definitions[0] = CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistanceDefinition(new CharacterTestTacticalDistanceData { Maximum = 2 }, 4);
            simple.Root.Children[0].DefinitionId = simple.Definitions[0].DefinitionId;
            StringAssert.Contains("等待目标进入指定距离", CharacterTestTacticalDslDescription.Generate(simple, CharacterTestTacticalCardInputs.Unbound(1)).Text);
        }

        private sealed class PassiveRuntime : ICharacterTestBehaviorExecutionRuntime
        {
            internal readonly CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub Stub = new();
            internal readonly List<CharacterTestBehaviorControlInput> Controls = new();
            public int EntityId => Stub.EntityId;
            public bool Evaluate(in CharacterTestBehaviorExecutionPredicate p) => Stub.Evaluate(p);
            public bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind s) => Stub.IsSubjectAvailable(s);
            public bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement m, out CharacterTestRuntimeAiLabMovementTargetSample s) => Stub.TrySampleMovement(m, out s);
            public bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn t, out int x, out int z) => Stub.TrySampleTurn(t, out x, out z);
            public bool TryStartAction(int id) => Stub.TryStartAction(id);
            public bool TryStopAction(long id) => Stub.TryStopAction(id);
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState => Stub.ActionCommandState;
            public CharacterTestRuntimeAiLabLogicActionState ActualActionState => Stub.ActualActionState;
            public long ActualActionInstanceId => Stub.ActualActionInstanceId;
            public long StartedActionInstanceId => Stub.StartedActionInstanceId;
            public void CompleteActionCommand() => Stub.CompleteActionCommand();
            public void ObserveOwnedAction(long id) => Stub.ObserveOwnedAction(id);
            public void SetExecutionControl(Vector2 move, float speed, in CharacterTestBehaviorControlInput c) { Controls.Add(c); Stub.SetExecutionControl(move, speed, c); }
        }
    }
}
