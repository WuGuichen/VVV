using System;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBehaviorExecutionEvaluatorTests
    {
        [Test][Category("DoubleEdgeNative")]
        public void EmptyWholeMainCompletesNaturallyButGenericStopDoesNot()
        {
            var runtime = new RuntimeStub();
            var program = Builder(1).Build().Execution;
            var natural = new CharacterTestBehaviorExecutionEvaluator(program, runtime);
            Assert.IsTrue(natural.Start(0)); Tick(natural, runtime, 1); Tick(natural, runtime, 2);
            Assert.IsTrue(natural.NaturallyCompleted);
            var cancelled = new CharacterTestBehaviorExecutionEvaluator(program, new RuntimeStub());
            Assert.IsTrue(cancelled.Start(0)); cancelled.Stop(false);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, cancelled.State);
            Assert.IsFalse(cancelled.NaturallyCompleted);
            var failed = new CharacterTestBehaviorExecutionEvaluator(program, new RuntimeStub());
            Assert.IsTrue(failed.Start(0)); failed.Stop(true); Assert.IsFalse(failed.NaturallyCompleted);
        }

        [Test][Category("DoubleEdgeNative")]
        public void NaturalTerminalProvenanceSurvivesTypedCopyAndChangesHash()
        {
            var runtime = new RuntimeStub(); var program = Builder(1).Build().Execution;
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program, runtime);
            Assert.IsTrue(evaluator.Start(0)); Tick(evaluator, runtime, 1); Tick(evaluator, runtime, 2);
            var state = evaluator.CreateSnapshot(); Assert.IsTrue(state.NaturalCompletionRequested);
            var restored = new CharacterTestBehaviorExecutionEvaluator(program, new RuntimeStub());
            Assert.IsTrue(restored.TryRestoreSnapshot(state)); Assert.IsTrue(restored.NaturallyCompleted);
            var a = new RuntimeHashAccumulator(1, 1); state.AddHash(a, 1);
            state.NaturalCompletionRequested = false;
            var b = new RuntimeHashAccumulator(1, 1); state.AddHash(b, 1);
            Assert.AreNotEqual(a.ToHash(), b.ToHash()); Assert.IsTrue(restored.NaturallyCompleted);
        }
        [TestCase(5f, 0f, 0f, false)]
        [TestCase(5f, 0f, 1f, true)]
        [TestCase(0.5f, 0f, 1f, false)]
        [TestCase(5f, 0.001f, 0f, false)]
        public void ApproachTarget_PreservesNavigationStopAndArrival(float distance, float navigationX, float navigationZ, bool expectMovement)
        {
            var builder = Builder();
            var movement = new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance,
                0, 0, 0, 1f, 1f, 1f, 0);
            builder.AddMovement("approach", "main", 0, 10, movement);
            var runtime = new RuntimeStub { Distance = distance, NavigationMove = new Vector2(navigationX, navigationZ) };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(builder.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            Tick(evaluator, runtime, 1);
            Assert.AreEqual(expectMovement ? runtime.NavigationMove : Vector2.zero, runtime.Move);
        }

        [TestCase(0.5f, 30f)]
        [TestCase(5f, 0f)]
        [Category("VisibleApproachCompletion")]
        public void VisibleApproachCompletion_RequiresDistanceAndFacingThenContinuesAction(float distance, float angle)
        {
            var b = Builder();
            var movement = new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance,
                0, 0, 0, 1f, 1f, 1f, 0);
            int arrived = b.AddMovementCondition("arrived", movement, false);
            int facing = b.AddFactCondition("facing", new CharacterTestRuntimeAiLabCardCondition {
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, Value = 5f });
            b.AddMovement("move", "main", 0, 10, movement);
            b.AddCompleteBlock("complete", "main", 0, 10, 10, b.AddAll("both", arrived, facing), 100);
            b.AddBlock("approach", "main", 0, 10, CharacterTestBehaviorBlockCompletion.Timed);
            b.AddAction("attack", "main", 10, 1, 1);
            var runtime = new RuntimeStub { Distance = distance, FacingAngle = angle };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(b.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            Tick(evaluator, runtime, 1);
            Assert.AreEqual(0, runtime.StartRequests);
            Assert.Less(evaluator.LocalFrame, 10);
            runtime.Distance = 0.5f; runtime.FacingAngle = 5f;
            Tick(evaluator, runtime, 2);
            Assert.AreEqual(10, evaluator.LocalFrame);
            Assert.AreEqual(Vector2.zero, runtime.Move);
            Tick(evaluator, runtime, 3);
            Assert.AreEqual(1, runtime.StartRequests);
        }

        [Test]
        [Category("VisibleApproachCompletion")]
        public void VisibleApproachCompletion_AuthoredEventSurvivesRoundTripAndUpgradeIsIdempotent()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateVisualCard("贴身逼近");
            Assert.AreEqual(0, CharacterTestTacticalApproachActionSplit.AddVisibleCompletionEvents(source));
            var restored = CharacterTestTacticalAuthoringJson.ReadSharedTrial(CharacterTestTacticalAuthoringJson.Write(source));
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(restored,
                CharacterTestTacticalCardInputs.Unbound(1), out var compiled, out var issues), string.Join(";", issues));
            int completions = 0;
            foreach (var evt in compiled.Program.Execution.Events)
                if (evt.Kind == CharacterTestBehaviorExecutionOperations.CompleteBlock) completions++;
            Assert.AreEqual(1, completions);
        }

        [Test, Category("VisibleApproachCompletion")]
        public void VisibleApproachCompletion_UsesReferencedTurnParametersAfterRecompile()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateVisualCard("贴身逼近");
            var turn = source.Timelines[0].Events.Find(e => e.Operation.Kind == "TurnToTarget");
            turn.Operation.FacingToleranceDegrees = 7f;
            var inputs = CharacterTestTacticalCardInputs.Unbound(1);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, inputs, out var first, out var issues), string.Join(";", issues));
            var runtime = new RuntimeStub { Distance = 0.5f, FacingAngle = 10f };
            var before = new CharacterTestBehaviorExecutionEvaluator(first.Program.Execution, runtime);
            Assert.IsTrue(before.Start(0)); Tick(before, runtime, 1);
            Assert.Less(before.LocalFrame, 30);
            turn.Operation.FacingToleranceDegrees = 13f;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, inputs, out var second, out issues), string.Join(";", issues));
            var after = new CharacterTestBehaviorExecutionEvaluator(second.Program.Execution, runtime);
            Assert.IsTrue(after.Start(0)); Tick(after, runtime, 1);
            Assert.AreEqual(30, after.LocalFrame);
            Assert.AreNotEqual(first.Program.Execution.Identity, second.Program.Execution.Identity);
        }

        [Test]
        public void FactDemand_WaitingAndMovementWithoutAttackConditionDoNotRequestAttackPreview()
        {
            var b = Builder();
            b.AddMovement("move", "main", 0, 10, LegacyMove());
            var runtime = new RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(b.Build().Execution, runtime);
            Assert.IsFalse(evaluator.NeedsPrimaryAttackFact(0));
            Assert.IsTrue(evaluator.Start(0));
            Assert.IsFalse(evaluator.NeedsPrimaryAttackFact(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LegacyOrdinaryArrivalBlockCannotPreventScheduledPlayback(bool targetExists)
        {
            var builder = Builder();
            builder.AddMovement("move", "main", 0, 3, LegacyMove());
            builder.AddBlock("old-arrival", "main", 0, 3, CharacterTestBehaviorBlockCompletion.Arrived, "move");
            builder.AddAction("play", "main", 3, 1, 1);
            var runtime = new RuntimeStub { TargetExists = targetExists, Distance = 100 };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(builder.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            for (int frame = 1; frame <= 4; frame++) Tick(evaluator, runtime, frame);
            Assert.AreEqual(1, runtime.StartRequests);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.None, evaluator.EndReason);
        }

        [Test]
        public void ExplicitWorldPointArrivalCompletesBeforeTheMaximumDuration()
        {
            var builder = Builder(180);
            var move = new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, CharacterTestBehaviorDistanceMetric.RootToWorldPointPlanarDistance,
                0, 1, 0, 0.2f, 0.2f, 1, 0);
            builder.AddMovement("move", "main", 0, 180, move);
            builder.AddBlock("point", "main", 0, 180, CharacterTestBehaviorBlockCompletion.Arrived, "move");
            var runtime = new RuntimeStub { Distance = 1f };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(builder.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            Tick(evaluator, runtime, 1);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            runtime.Distance = 0.1f;
            Tick(evaluator, runtime, 2);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(Vector2.zero, runtime.Move);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, evaluator.EndReason);
        }

        [Test]
        public void TerminalActionReleaseCompletesBehaviorWithoutStoppingPhysicalRecovery()
        {
            var builder = Builder();
            builder.AddAction("attack", "main", 0, 20, 1);
            builder.AddBlock("attack-block", "main", 0, 20, CharacterTestBehaviorBlockCompletion.Action, "attack", hasAction: true);
            var runtime = new RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(builder.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            Tick(evaluator, runtime, 1);
            Tick(evaluator, runtime, 2);
            long instance = runtime.ActualActionInstanceId;
            Assert.IsFalse(evaluator.TryCompleteTerminalAction(instance + 1));
            Assert.IsTrue(evaluator.TryCompleteTerminalAction(instance));
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(0, evaluator.OwnedActionInstanceId);
            Assert.AreEqual(instance, runtime.ActualActionInstanceId);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicActionState.Running, runtime.ActualActionState);
            Assert.AreEqual(0, runtime.StopRequests);
            Tick(evaluator, runtime, 3);
            Assert.AreEqual(instance, runtime.ActualActionInstanceId);
            Assert.AreEqual(0, runtime.StopRequests);
        }

        [Test]
        public void ActionReleaseDoesNotSkipAnAuthoredSuccessor()
        {
            var builder = Builder(30);
            builder.AddAction("attack", "main", 0, 20, 1);
            builder.AddBlock("attack-block", "main", 0, 20, CharacterTestBehaviorBlockCompletion.Action, "attack", hasAction: true);
            builder.AddMovement("follow-up", "main", 20, 10, LegacyMove());
            var runtime = new RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(builder.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            Tick(evaluator, runtime, 1);
            Tick(evaluator, runtime, 2);
            Assert.IsFalse(evaluator.TryCompleteTerminalAction(runtime.ActualActionInstanceId));
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            Assert.AreEqual(runtime.ActualActionInstanceId, evaluator.OwnedActionInstanceId);
            Assert.AreEqual(0, runtime.StopRequests);
        }

        [Test]
        public void FactDemand_ConditionWindowAndNegationKeepTheirAttackDependency()
        {
            var b = Builder();
            int fact = b.AddFactCondition("range", new CharacterTestRuntimeAiLabCardCondition
            { Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange, Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals, BoolValue = true });
            b.AddGate("gate", "main", 3, b.AddNot("outside", fact));
            var runtime = new RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(b.Build().Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            Assert.IsFalse(evaluator.NeedsPrimaryAttackFact(1));
            for (int frame = 1; frame <= 3; frame++) Tick(evaluator, runtime, frame);
            Assert.IsTrue(evaluator.NeedsPrimaryAttackFact(4));
        }

        [Test]
        public void FactDemand_UnobservedAttackFactIsAbsentFromTypedReadsAndSnapshot()
        {
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetPrimaryAttackFactObserved(false);
            Assert.IsFalse(facts.Contains(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange));
            Assert.IsFalse(facts.TryGetValue<bool>(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange, out _));
            Assert.IsFalse(facts.Snapshot().ContainsKey(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange));
            facts.SetNoTarget(false);
            Assert.IsTrue(facts.TryGetValue<bool>(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange, out bool inRange));
            Assert.IsFalse(inRange);
        }

        [Test]
        public void PlaybackEventDoesNotRequestAttackFactsOrEvaluateItsLegacyCondition()
        {
            var b = Builder();
            int condition = b.AddFactCondition("range", new CharacterTestRuntimeAiLabCardCondition
            { Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange, BoolValue = true });
            b.AddAction("action", "main", 0, 1, 1, condition: condition);
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(b.Build().Execution, new RuntimeStub());
            Assert.IsTrue(evaluator.Start(0));
            Assert.IsFalse(evaluator.NeedsPrimaryAttackFact(1));
        }

        [Test]
        public void FactDemand_CompiledProgramRetainsOnlyItsEnvironmentAndNavigationDependencies()
        {
            var waiting = Builder();
            waiting.AddReturn("done", "main", 5, 1);
            Assert.AreEqual(CharacterTestRuntimeAiPerceptionRequirements.None, waiting.Build().BackgroundPerceptionRequirements);
            var movement = Builder();
            movement.AddMovement("move", "main", 0, 10, LegacyMove());
            Assert.AreEqual(CharacterTestRuntimeAiPerceptionRequirements.Navigation, movement.Build().BackgroundPerceptionRequirements);
            var environment = Builder();
            int condition = environment.AddFactCondition("hazard", new CharacterTestRuntimeAiLabCardCondition
            { Fact = CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard, BoolValue = true });
            environment.AddGate("gate", "main", 0, condition);
            Assert.AreEqual(CharacterTestRuntimeAiPerceptionRequirements.Environment, environment.Build().BackgroundPerceptionRequirements);
        }

        [Test]
        public void FactDemand_UnrequestedEnvironmentAndNavigationAreAbsent()
        {
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetPerceptionRequirements(CharacterTestRuntimeAiPerceptionRequirements.None);
            Assert.IsFalse(facts.Contains(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable));
            Assert.IsFalse(facts.TryGetValue<bool>(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard, out _));
            var snapshot = facts.Snapshot();
            Assert.IsFalse(snapshot.ContainsKey(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX));
            Assert.IsFalse(snapshot.ContainsKey(CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind));
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog References()
        {
            var actions = new[] { Action(1), Action(2) };
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
                new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), actions));
        }
        private static CharacterActionConfig Action(int id) => new CharacterActionConfig(new CharacterActionId(id), "test",
            CharacterActionCategory.BasicAttack, CharacterActionTimelineAuthority.CharacterAuthored, Array.Empty<string>(), 0, 8, null, null, null, null);
        private static CharacterTestBehaviorExecutionBuilder Builder(int duration = 20)
        { var b = new CharacterTestBehaviorExecutionBuilder(References()); b.AddTimeline("main", duration); return b; }
        private static CharacterTestBehaviorExecutionMovement LegacyMove(CharacterTestRuntimeAiLabMovementStrategy strategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget)
            => new CharacterTestBehaviorExecutionMovement(strategy, CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance, 0, 0, 0, 0, 0, 1, 0);
        private static void Tick(CharacterTestBehaviorExecutionEvaluator evaluator, RuntimeStub runtime, int frame)
        { evaluator.Tick(frame); runtime.Commit(); }

        [Test]
        public void SameFrame_LastActionAndParallelControlsDoNotReadCandidateAsActual()
        {
            var b = Builder();
            b.AddMovement("move", "main", 0, 10, LegacyMove(), priority: 10);
            b.AddTurn("turn", "main", 0, 10, new CharacterTestBehaviorExecutionTurn(speed: 1), priority: 20);
            b.AddAction("action.a", "main", 0, 8, 1, priority: 100);
            b.AddAction("action.b", "main", 0, 8, 2, priority: 0);
            int actual = b.AddFactCondition("actual", new CharacterTestRuntimeAiLabCardCondition
            { Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning, Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals, BoolValue = true });
            b.AddGate("gate", "main", 0, actual, priority: -100);
            var program = b.Build(); var runtime = new RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime, program.ExecutionDebugMap);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1);
            Assert.AreEqual(1, runtime.StartRequests); Assert.AreEqual(2, runtime.LastRequestedAction);
            Assert.AreEqual(0, runtime.ActualActionInstanceId, "Commands have not reached the world phase yet.");
            Assert.AreEqual(0, evaluator.LocalFrame, "Gate must read actual action state, not the selected candidate.");
            Assert.AreEqual(Vector2.right, runtime.Move); Assert.AreEqual(2, runtime.Control.FacingMode);
            runtime.Commit(); evaluator.Tick(2); Assert.AreEqual(1, runtime.StartRequests);
            Assert.AreEqual(1, evaluator.LocalFrame);
        }

        [Test]
        public void SameFrame_ExitDiscardsActionAndAllPendingControlCandidates()
        {
            var b = Builder(); b.AddMovement("move", "main", 0, 10, LegacyMove(), priority: 100);
            b.AddAction("action", "main", 0, 8, 1, priority: 50);
            b.AddReturn("exit", "main", 0, 10, priority: -100);
            var program = b.Build(); var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); e.Tick(1);
            Assert.AreEqual(0, runtime.StartRequests); Assert.AreEqual(Vector2.zero, runtime.Move);
            Assert.IsTrue(runtime.Control.IsRelease); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, e.State);
        }

        [Test]
        public void PlaybackRequestsOnceWithoutLegacyConditionAndDoesNotReplayAfterRestore()
        {
            var b = Builder(); int condition = b.AddBoundActionOpportunityCondition("opportunity", 1);
            b.AddAction("action", "main", 0, 8, 1, condition); b.AddGate("gate", "main", 0, -1);
            var program = b.Build(); var runtime = new RuntimeStub { Opportunity = false };
            var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime); Assert.IsTrue(e.Start(0)); Tick(e, runtime, 1);
            Assert.AreEqual(1,runtime.StartRequests);
            Assert.AreEqual(-1,e.LastActionConditionRejectedEvent);
            var snapshot = e.CreateSnapshot(); var restored = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot)); runtime.Opportunity = true;
            Tick(restored, runtime, 2); Assert.AreEqual(1, runtime.StartRequests);
            Assert.AreEqual(-1,restored.LastActionConditionRejectedEvent);
        }

        [Test]
        public void PendingPlaybackDoesNotRecheckTargetDistanceFacingOrEventCondition()
        {
            var b=Builder();int condition=b.AddBoundActionOpportunityCondition("legacy",1);
            b.AddAction("play","main",0,8,1,condition);
            var runtime=new RuntimeStub{TargetExists=false,Opportunity=false,Facing=false,ActionAllowed=false};
            var evaluator=new CharacterTestBehaviorExecutionEvaluator(b.Build().Execution,runtime);
            Assert.IsTrue(evaluator.Start(0));evaluator.Tick(1);
            Assert.AreEqual(1,runtime.StartRequests);Assert.AreEqual(0,runtime.EvaluateCount);
            Assert.IsTrue(evaluator.CanCommitPendingAction(1));
            Assert.IsFalse(evaluator.CanCommitPendingAction(2),"A receipt cannot change which action this event requested.");
            Assert.AreEqual(0,runtime.EvaluateCount);
        }

        [TestCase(1, 1, 0, 3)]
        [TestCase(2, 2, 0, 3)]
        [TestCase(2, 2, 4, 5)]
        public void LoopEntryRequiresARealPositionChange(int length, int expectedStarts, int cooldown, int frames)
        {
            var b = Builder(length); b.AddAction("action", "main", 0, length, 1);
            var baseProgram = b.Build().Execution;
            var actionEvent = new CharacterTestBehaviorExecutionEvent(0, 0, length, -1, 0, 1, cooldown, 0, 0,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline, false);
            var events = new[] { actionEvent, new CharacterTestBehaviorExecutionEvent(0, 0, length, -1, 0, 1, 0, -1, 1,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline, false) };
            var program = new CharacterTestBehaviorExecutionProgram(0, -1, baseProgram.DependencyFingerprint,
                new[] { new CharacterTestBehaviorExecutionTimeline(0, 2, length, -1) }, events, new[] { 0, 1 }, baseProgram.Actions.ToArray(),
                Array.Empty<CharacterTestBehaviorExecutionMovement>(), new[] { new CharacterTestBehaviorExecutionFlow(-1, 0) },
                Array.Empty<CharacterTestBehaviorExecutionCondition>(), Array.Empty<CharacterTestBehaviorExecutionPredicate>(), Array.Empty<int>());
            var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program, runtime);
            Assert.IsTrue(e.Start(0)); for (int frame = 1; frame <= frames; frame++) Tick(e, runtime, frame);
            Assert.AreEqual(expectedStarts, runtime.StartRequests);
        }

        [Test]
        public void ReplacementAtExpiredBoundaryDoesNotInsertATerminationFrame()
        {
            var b = Builder(); b.AddAction("a", "main", 0, 1, 1); b.AddAction("b", "main", 1, 8, 2);
            var program = b.Build(); var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); Tick(e, runtime, 1); Tick(e, runtime, 2);
            Assert.AreEqual(2, runtime.StartRequests); Assert.AreEqual(2, runtime.LastRequestedAction); Assert.AreEqual(0, runtime.StopRequests);
        }

        [Test]
        public void ShortActionReceiptCanCompleteBeforeTheNextSampleWithoutReplaying()
        {
            var b = Builder(); b.AddAction("action", "main", 0, 8, 1);
            var program = b.Build(); var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); Tick(e, runtime, 1); runtime.CompleteActualAction(); Tick(e, runtime, 2);
            Assert.AreEqual(1, runtime.StartRequests); Assert.AreEqual(0, e.OwnedActionInstanceId);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, e.State);
            Assert.AreEqual((int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed, e.CreateSnapshot().Results[0]);
        }

        [Test]
        public void EnteringActionMiddleDoesNotStartIt()
        {
            var b = Builder(); b.AddAction("action", "main", 0, 8, 1);
            var program = b.Build(); var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); var state = e.CreateSnapshot(); state.LocalFrame = 3; state.LastSampledFrame = 2;
            Assert.IsTrue(e.TryRestoreSnapshot(state)); Tick(e, runtime, 1); Assert.AreEqual(0, runtime.StartRequests);
        }

        [Test]
        public void OptimalMovementStopsAtPointHoldsBandAndCannotYieldZeroToLowerPriority()
        {
            var b = Builder(); b.AddMovement("optimal", "main", 0, 20,
                CharacterTestBehaviorExecutionMovement.ApproachOptimalRange(1, 3, 1.25f), priority: 100);
            b.AddMovement("lower", "main", 0, 20, LegacyMove(CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard), priority: 0);
            var program = b.Build(); var runtime = new RuntimeStub { Distance = 2f }; var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); Tick(e, runtime, 1); Assert.AreEqual(Vector2.right, runtime.Move);
            runtime.Distance = 1.25f; Tick(e, runtime, 2); Assert.AreEqual(Vector2.zero, runtime.Move); Assert.IsFalse(runtime.Control.IsRelease);
            runtime.Distance = 2f; Tick(e, runtime, 3); Assert.AreEqual(Vector2.zero, runtime.Move);
            runtime.Distance = 3.2f; Tick(e, runtime, 4); Assert.AreEqual(Vector2.right, runtime.Move);
            runtime.Distance = 0.5f; Tick(e, runtime, 5); Assert.AreEqual(Vector2.left, runtime.Move);
            Assert.AreEqual(0, e.CreateSnapshot().Counts[1], "A lower candidate must not consume an execution.");
        }

        [Test]
        public void HigherPriorityPreemptsWithoutAutomaticallyResumingCancelledOperation()
        {
            var b = Builder(); b.AddMovement("low", "main", 0, 10, LegacyMove(CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard), priority: 0);
            b.AddMovement("high", "main", 1, 1, LegacyMove(), priority: 100);
            b.AddTurn("turn", "main", 0, 10, new CharacterTestBehaviorExecutionTurn(speed: 1), priority: 200);
            var program = b.Build(); var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); Tick(e, runtime, 1); Assert.AreEqual(Vector2.left, runtime.Move);
            Tick(e, runtime, 2); Assert.AreEqual(Vector2.right, runtime.Move);
            Tick(e, runtime, 3); Assert.AreEqual(Vector2.zero, runtime.Move); Assert.AreEqual(2, runtime.Control.FacingMode);
        }

        [Test]
        public void MissingTargetReleasesOutputWithoutConsumingAnotherActivation()
        {
            var b = Builder(); b.AddMovement("move", "main", 0, 10, LegacyMove());
            var program = b.Build(); var runtime = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(e.Start(0)); Tick(e, runtime, 1); runtime.TargetExists = false; Tick(e, runtime, 2);
            Assert.AreEqual(Vector2.zero, runtime.Move); Assert.IsTrue(runtime.Control.IsRelease);
            runtime.TargetExists = true; Tick(e, runtime, 3); Assert.AreEqual(Vector2.right, runtime.Move);
            Assert.AreEqual(1, e.CreateSnapshot().Counts[0]);
        }

        [Test]
        public void EqualPriorityUsesOrdinalIdentityAndBothChannelsHandoffWithoutZeroFrame()
        {
            var b = Builder();
            b.AddMovement("z.move", "main", 0, 5, LegacyMove());
            b.AddMovement("a.move", "main", 0, 1, LegacyMove(CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard));
            b.AddTurn("z.turn", "main", 0, 5, new CharacterTestBehaviorExecutionTurn(CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, x: -1), priority: 100);
            b.AddTurn("a.turn", "main", 0, 1, new CharacterTestBehaviorExecutionTurn(CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, x: 1), priority: 100);
            var r = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(b.Build().Execution, r);
            Assert.IsTrue(e.Start(0)); Tick(e, r, 1); Assert.AreEqual(Vector2.left, r.Move); Assert.AreEqual(1000, r.Control.TargetXMillimeters);
            Tick(e, r, 2); Assert.AreEqual(Vector2.right, r.Move); Assert.AreEqual(-1000, r.Control.TargetXMillimeters); Assert.IsFalse(r.Control.IsRelease);
        }

        [Test]
        public void SnapshotRejectsWrongProgramAndDoesNotShareMutableState()
        {
            var b = Builder(); b.AddMovement("move", "main", 0, 20, CharacterTestBehaviorExecutionMovement.ApproachOptimalRange(1, 3, 1.25f));
            var program = b.Build(); var runtime = new RuntimeStub { Distance = 1.25f }; var first = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(first.Start(0)); Tick(first, runtime, 1); var snapshot = first.CreateSnapshot();
            var secondRuntime = new RuntimeStub { Distance = 2f }; var second = new CharacterTestBehaviorExecutionEvaluator(program.Execution, secondRuntime);
            Assert.IsTrue(second.TryRestoreSnapshot(snapshot)); snapshot.Stopped[0] = false;
            Tick(second, secondRuntime, 2); Assert.AreEqual(Vector2.zero, secondRuntime.Move);
            var invalid = first.CreateSnapshot(); invalid.ProgramIdentity++;
            Assert.IsFalse(second.TryRestoreSnapshot(invalid)); Assert.IsTrue(second.CreateSnapshot().Stopped[0]);
            var otherEntity = new CharacterTestBehaviorExecutionEvaluator(program.Execution, new RuntimeStub(2));
            Assert.IsFalse(otherEntity.TryRestoreSnapshot(first.CreateSnapshot()));
        }

        [Test]
        public void DebugMapDoesNotChangeStateHash()
        {
            var b = Builder(); b.AddMovement("move", "main", 0, 10, LegacyMove());
            var withMap = b.Build(); var withoutMap = b.Build(false);
            var a = new CharacterTestBehaviorExecutionEvaluator(withMap.Execution, new RuntimeStub(), withMap.ExecutionDebugMap);
            var c = new CharacterTestBehaviorExecutionEvaluator(withoutMap.Execution, new RuntimeStub());
            Assert.IsTrue(a.Start(0)); Assert.IsTrue(c.Start(0)); a.Tick(1); c.Tick(1);
            var first = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1); var second = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1); a.AddHash(first); c.AddHash(second);
            Assert.AreEqual(first.ToHash(), second.ToHash());
        }

        [Test]
        public void WarmParallelChannelAndGateSamplingAllocatesNothing()
        {
            var b = Builder(); b.AddMovement("move", "main", 0, 20, LegacyMove());
            b.AddTurn("turn", "main", 0, 20, new CharacterTestBehaviorExecutionTurn(speed: 1)); b.AddGate("gate", "main", 0, -1);
            var p = b.Build(); var e = new CharacterTestBehaviorExecutionEvaluator(p.Execution, new RuntimeStub()); Assert.IsTrue(e.Start(0));
            for (int frame = 1; frame <= 128; frame++) e.Tick(frame);
            using (var positive = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024,
                ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            { var bytes = new byte[8192]; GC.KeepAlive(bytes); positive.Stop(); Assert.Greater(positive.Count, 0); }
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024,
                ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int frame = 129; frame <= 1152; frame++) e.Tick(frame);
            recorder.Stop(); Assert.AreEqual(0, recorder.Count);
        }

        [Test]
        public void ConditionsPreserveShortCircuitNotAndUnavailableScope()
        {
            var b = Builder(); int falseLeaf = b.AddBoundActionOpportunityCondition("false", 1);
            int trueLeaf = b.AddTargetAliveCondition("true");
            int all = b.AddAll("all", falseLeaf, trueLeaf); int any = b.AddAny("any", trueLeaf, falseLeaf); int not = b.AddNot("not", falseLeaf);
            var p = b.Build().Execution; var r = new RuntimeStub { Opportunity = false };
            Assert.IsFalse(CharacterTestBehaviorExecutionEvaluator.EvaluateCondition(p, all, r)); Assert.AreEqual(1, r.EvaluateCount);
            r.EvaluateCount = 0; Assert.IsTrue(CharacterTestBehaviorExecutionEvaluator.EvaluateCondition(p, any, r)); Assert.AreEqual(1, r.EvaluateCount);
            Assert.IsTrue(CharacterTestBehaviorExecutionEvaluator.EvaluateCondition(p, not, r));
            var scoped = new CharacterTestBehaviorExecutionProgram(0, 0, "", new[] { new CharacterTestBehaviorExecutionTimeline(0, 0, 1, -1) },
                Array.Empty<CharacterTestBehaviorExecutionEvent>(), Array.Empty<int>(), Array.Empty<CharacterTestBehaviorExecutionAction>(),
                Array.Empty<CharacterTestBehaviorExecutionMovement>(), Array.Empty<CharacterTestBehaviorExecutionFlow>(),
                new[] {
                    new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                        CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget, -1, 0, 1, -1, default),
                    new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate, default, 0, 1, 0, -1, default) },
                new[] { p.Predicates[1] }, new[] { 1 });
            r.TargetExists = false; r.EvaluateCount = 0;
            Assert.IsFalse(CharacterTestBehaviorExecutionEvaluator.EvaluateCondition(scoped, 0, r)); Assert.AreEqual(0, r.EvaluateCount);
        }

        [Test]
        public void LaterEventReadsTheControlCancellationCommittedEarlierInTheSameSample()
        {
            var source = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11, EntryTimelineId = "main" };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "main" }; source.Timelines.Add(timeline);
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "low", TimelineId = "main", DurationFrames = 10,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget } });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "high", TimelineId = "main", StartFrame = 1, DurationFrames = 1, Priority = 100,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget } });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "exit", TimelineId = "main", StartFrame = 1, DurationFrames = 9, Priority = 50,
                ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "cancelled", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                    SourceEventId = "low", Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Cancelled },
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner } });
            var compiled = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(source);
            Assert.IsTrue(compiled.Success); var r = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, r);
            Assert.IsTrue(e.Start(0)); Tick(e, r, 1); Tick(e, r, 2);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, e.State); Assert.AreEqual(Vector2.zero, r.Move);
        }

        [Test]
        public void TimelineJumpDiscardsCurrentCandidatesAndStartsTargetOnLaterSample()
        {
            var source = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11, EntryTimelineId = "a" };
            var a = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "a" };
            var b = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "b", Order = 1 };
            source.Timelines.Add(a); source.Timelines.Add(b);
            a.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "move", TimelineId = "a", DurationFrames = 5, Priority = 100,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget } });
            a.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "jump", TimelineId = "a", DurationFrames = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, TargetTimelineId = "b" } });
            b.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "back", TimelineId = "b", DurationFrames = 5,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard } });
            var compiled = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(source); Assert.IsTrue(compiled.Success);
            var r = new RuntimeStub(); var e = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, r);
            Assert.IsTrue(e.Start(0)); Tick(e, r, 1); Assert.AreEqual(Vector2.zero, r.Move);
            Tick(e, r, 2); Assert.AreEqual(1, e.CurrentTimelineIndex); Assert.AreEqual(Vector2.zero, r.Move);
            Tick(e, r, 3); Assert.AreEqual(Vector2.left, r.Move);
        }

        internal sealed class RuntimeStub : ICharacterTestBehaviorExecutionRuntime, ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime
        {
            private int _pendingAction; private bool _pendingStop; private long _stopExpected, _next = 100;
            public int EntityId { get; }
            public bool TargetExists = true, Opportunity = true, Facing = true, PathAvailable = true;
            public float? FacingAngle;
            public bool ActionAllowed = true, AcceptAction = true;
            public float Distance = 5f;
            public Vector2 NavigationMove = Vector2.right;
            public int StartRequests, StopRequests, LastRequestedAction, EvaluateCount;
            public Vector2 Move; public CharacterTestBehaviorControlInput Control;
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState { get; private set; }
            public long ActualActionInstanceId { get; private set; }
            public long StartedActionInstanceId { get; private set; }
            public CharacterTestRuntimeAiLabLogicActionState ActualActionState => ActualActionInstanceId > 0 ? CharacterTestRuntimeAiLabLogicActionState.Running : CharacterTestRuntimeAiLabLogicActionState.Completed;
            public CharacterTestRuntimeAiLabLogicActionState PrimaryActionState => ActualActionState;
            public RuntimeStub(int entityId = 1) { EntityId = entityId; }
            public void SetBool(CharacterTestRuntimeAiLabCardConditionFact fact, bool value)
            { if (fact == CharacterTestRuntimeAiLabCardConditionFact.TargetExists) TargetExists = value; if (fact == CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable) PathAvailable = value; }
            public bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                EvaluateCount++;
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle)
                    return FacingAngle.HasValue ? condition.Operator switch {
                        CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => FacingAngle.Value >= condition.Value,
                        CharacterTestRuntimeAiLabCardConditionOperator.Between => FacingAngle.Value >= condition.Value && FacingAngle.Value <= condition.MaxValue,
                        _ => FacingAngle.Value <= condition.Value } : Facing;
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible) return ActionAllowed == condition.BoolValue;
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState)
                    return CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(TargetExists ? CharacterTestRuntimeAiPlannerTargetStateMask.Alive : CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget,
                        condition.AllowedTargetPhases, condition.RequiredTargetFlags, condition.ForbiddenTargetFlags);
                bool value = condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning ? ActualActionInstanceId > 0 : TargetExists;
                return value == condition.BoolValue;
            }
            public bool Evaluate(in CharacterTestBehaviorExecutionPredicate predicate)
            {
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.Fact) return Evaluate(predicate.AsFactRequest());
                EvaluateCount++;
                if (!TargetExists) return false;
                return predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.BoundActionOpportunity ? Opportunity
                    : predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.TargetFacing ? FacingAngle.HasValue
                        ? Math.Abs(FacingAngle.Value - predicate.Value) <= predicate.Maximum : Facing
                        : Distance >= predicate.Value && Distance <= predicate.Maximum;
            }
            public bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject) => subject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget || TargetExists;
            public bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement movement, out CharacterTestRuntimeAiLabMovementTargetSample sample)
            { sample = new CharacterTestRuntimeAiLabMovementTargetSample(TargetExists, Distance, Vector2.right, NavigationMove); return TargetExists; }
            public bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn turn, out int x, out int z)
            { x = turn.Target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition ? CharacterTestDeterministicUnits.MetersToMillimeters(turn.X) : 1000;
                z = turn.Target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition ? CharacterTestDeterministicUnits.MetersToMillimeters(turn.Z) : 1000; return TargetExists; }
            public bool TryStartAction(int id) { StartRequests++; LastRequestedAction = id; if (!AcceptAction) return false; _pendingAction = id; _pendingStop = false; ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending; return true; }
            public bool TryStopAction(long expected) { StopRequests++; _pendingStop = true; _stopExpected = expected; ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending; return true; }
            public void Commit()
            {
                if (ActionCommandState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending) return;
                if (_pendingStop) { if (ActualActionInstanceId == _stopExpected) ActualActionInstanceId = 0; }
                else if (_pendingAction > 0) StartedActionInstanceId = ActualActionInstanceId = ++_next;
                ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded;
            }
            public void CompleteActualAction() { ActualActionInstanceId = 0; }
            public void CompleteActionCommand() { ActionCommandState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None; _pendingAction = 0; _pendingStop = false; StartedActionInstanceId = 0; }
            public void ObserveOwnedAction(long id) { }
            public void SetExecutionControl(Vector2 move, float speed, in CharacterTestBehaviorControlInput control) { Move = move; Control = control; }
            public bool TryEnterStage(CharacterTestRuntimeAiLabCompiledLogicStage stage) => throw new InvalidOperationException("Native execution must not enter Stage.");
            public bool TryInterruptPrimaryAction() => throw new InvalidOperationException("Native execution must use instance-owned termination.");
        }
    }
}

namespace MxFramework.Tests.Game.CharacterTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MxFramework.Game.CharacterTest;
    using UnityEngine;
    using NUnit.Framework;
    public sealed class CharacterTestBoundActionCooldownTests
    {
        private static MxFramework.Gameplay.AbilityDefinitionConfig Owner(int id, int actionId, int frames = 20)
            => new MxFramework.Gameplay.AbilityDefinitionConfig(id,"cpu.owner."+id,"CPU owner",null,
                new MxFramework.Gameplay.AbilityCooldownConfig(frames,0),Array.Empty<MxFramework.Gameplay.AbilityCostConfig>(),0,
                new MxFramework.Gameplay.AbilityActionBindingConfig(new MxFramework.CharacterAction.CharacterActionId(actionId),1),default,0,0);
        private static MxFramework.CharacterAction.CharacterActionConfig Undeclared(int id = 301)
            => new MxFramework.CharacterAction.CharacterActionConfig(new MxFramework.CharacterAction.CharacterActionId(id),
                "CPU actual-style action",MxFramework.CharacterAction.CharacterActionCategory.BasicAttack,default,null,0,10,
                new[] {new MxFramework.CharacterAction.CharacterActionRequirement(MxFramework.CharacterAction.CharacterActionRequirementKind.WeaponTypeIndependent)},null,null,null);

        [Test]
        public void UndeclaredOwnerRefreshesAfterRegistrationAndReadsCurrentState()
        {
            var registry=new MxFramework.Gameplay.AbilityDefinitionRegistry();
            var abilities=new MxFramework.Gameplay.AbilityRuntimeModule(registry);
            var authority=new CharacterTestActionUseAuthority(null,abilities);var action=Undeclared();
            Assert.IsTrue(authority.EvaluateCooldown(1,action,default,out _).Available);
            var owner=Owner(93001,301);Assert.IsTrue(registry.TryRegister(owner,out _));
            abilities.CommitActivation(1,owner,default);
            Assert.IsFalse(authority.EvaluateCooldown(1,action,default,out long remaining).Available);Assert.AreEqual(20,remaining);
            Assert.IsTrue(authority.EvaluateCooldown(2,action,default,out _).Available);
            Assert.IsTrue(authority.EvaluateCooldown(1,action,new MxFramework.Runtime.RuntimeFrame(20),out _).Available);
            Assert.IsTrue(authority.EvaluateCooldown(1,Undeclared(302),default,out _).Available,"Unrelated action must not borrow this cooldown.");
        }
        [Test]
        public void AmbiguousOwnersRequireAnExplicitValidDeclaration()
        {
            var abilities=Abilities(out var first);var authority=new CharacterTestActionUseAuthority(null,abilities);
            Assert.IsTrue(abilities.Definitions.TryRegister(Owner(93002,301),out _));
            Assert.AreEqual(CharacterTestActionUseStatus.AbilityOwnerMismatch,authority.EvaluateCooldown(1,Undeclared(),default,out _).Status);
            Assert.IsTrue(authority.EvaluateCooldown(1,Action(),default,out _).Available);
            abilities.CommitActivation(1,first,default);
            Assert.IsFalse(authority.EvaluateCooldown(1,Action(),default,out _).Available);
            Assert.IsTrue(authority.EvaluateCooldown(1,Undeclared(302),default,out _).Available);
        }
        [Test]
        public void ExplicitMismatchedOwnerCannotFallBackToAnotherAbility()
        {
            var registry=new MxFramework.Gameplay.AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(Owner(93001,302),out _));
            var authority=new CharacterTestActionUseAuthority(null,new MxFramework.Gameplay.AbilityRuntimeModule(registry));
            Assert.AreEqual(CharacterTestActionUseStatus.AbilityOwnerMismatch,authority.EvaluateCooldown(1,Action(),default,out _).Status);
        }

        private static CharacterTestTacticalCardInputs Slot(MxFramework.CharacterAction.CharacterActionConfig action)
        {
            var summary=new MxFramework.CharacterAction.CharacterActionSpatialSummary(action.ActionId,"cpu-action-"+action.ActionId.Value,"cpu-dummy",
                new MxFramework.CharacterAction.CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version,50,0,5000,
                    new MxFramework.CharacterAction.CharacterActionReferenceDummy(1,1800,300,0,900,0)),
                MxFramework.CharacterAction.CharacterActionProberStatus.Valid,
                new[]{new MxFramework.CharacterAction.CharacterActionSpatialInterval(2000,3000)},2500,false,false,
                MxFramework.CharacterAction.CharacterActionTacticalSector.Fallback);
            var metrics=new MxFramework.CharacterAction.CharacterActionTacticalMetrics(action,summary.ActionContentHash,summary);
            var constructor=typeof(CharacterTestTacticalCardInputs).GetConstructors(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Single();
            return (CharacterTestTacticalCardInputs)constructor.Invoke(new object[]{action,metrics,"cpu-slot-"+action.ActionId.Value,"",1,true,
                null,null,summary,1,null,CharacterTestTacticalInputRequirements.PrimaryAction|CharacterTestTacticalInputRequirements.SpatialSummary});
        }
        [TestCase(false)] [TestCase(true)]
        public void AutomaticAdmissionIncludesDistanceOnlyAndUnusedConfiguredSlots(bool maintain)
        {
            var primary=Undeclared();var spare=Undeclared(302);
            var inputs=CharacterTestTacticalCardInputs.BindSlots(1,new Dictionary<string,CharacterTestTacticalCardInputs>
                {{"primary",Slot(primary)},{"unused",Slot(spare)}});
            var source=CharacterTestTacticalAuthoringTemplates.CreateBehindTargetDefinition(maintain,20,25,2,3,0.05f,0.1f,12,0.65f,1,2,
                "ActionBand","primary").Source;
            source.Slots.Add(new CharacterTestTacticalAuthoringSlot{SlotId="unused"});
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source,inputs,out var compiled,out var errors),string.Join(";",errors));
            var card=new CharacterTestRuntimeAiLabBehaviorCardDraft("cpu.range", "cpu.range","CPU range", "Published",null);
            card.CurrentRevision=1;card.SetTacticalSource(source);
            var frozen=CharacterTestTacticalFrozenSource.CaptureLegacy(null,card,inputs,compiled);
            var program=compiled.Program.WithTacticalSource(frozen);
            var used=CharacterTestBehaviorExecutionUsage.Analyze(program).ActionIds;
            Assert.AreEqual(0,used.Count,"This card only borrows range and never emits an action.");
            CollectionAssert.AreEqual(new[]{301,302},frozen.ConfiguredActionIds);
            var cooldownIds=CharacterTestTacticalFrozenSource.CaptureCooldownActionIds(program,used);
            CollectionAssert.AreEqual(new[]{301,302},cooldownIds);
            var registry=new MxFramework.CharacterAction.CharacterActionConfigRegistry(Array.Empty<MxFramework.CharacterAction.CharacterActionSetConfig>(),new[]{primary,spare});
            var definitions=new MxFramework.Gameplay.AbilityDefinitionRegistry();var first=Owner(93001,301);var second=Owner(93002,302);
            Assert.IsTrue(definitions.TryRegister(first,out _));Assert.IsTrue(definitions.TryRegister(second,out _));
            var abilities=new MxFramework.Gameplay.AbilityRuntimeModule(definitions);var authority=new CharacterTestActionUseAuthority(null,abilities);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(cooldownIds,authority,registry,1,default).Available);
            abilities.CommitActivation(1,second,default);
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(cooldownIds,authority,registry,1,default).Available,
                "Unused configured slots also block the whole card before its first movement.");
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(cooldownIds,authority,registry,2,default).Available);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(cooldownIds,authority,registry,1,new MxFramework.Runtime.RuntimeFrame(20)).Available);
            abilities.CommitActivation(1,first,new MxFramework.Runtime.RuntimeFrame(20));
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.EvaluateConfiguredActionCooldowns(cooldownIds,authority,registry,1,new MxFramework.Runtime.RuntimeFrame(20)).Available);
            CollectionAssert.IsEmpty(CharacterTestTacticalFrozenSource.CaptureCooldownActionIds(compiled.Program,used),"Non-tactical programs retain their existing admission semantics.");
            Assert.AreEqual(0,authority.CommitCount);
        }

        private sealed class PreviewTime : MxFramework.Game.CharacterTest.TacticalCard.ITacticalTimeAdapter
        { public bool IsSimulationPaused{get;private set;} public void SetSimulationPaused(bool paused){IsSimulationPaused=paused;} }
        private sealed class PreviewPort : MxFramework.Game.CharacterTest.TacticalCard.ITacticalExecutionPort,
            MxFramework.Game.CharacterTest.TacticalCard.ITacticalBehindPreviewPort
        {
            internal int Preparations,Samples,ExecutionCalls;internal bool Available=true;
            public bool TryPrepareBehindPreview(MxFramework.Game.CharacterTest.TacticalCard.TacticalCardInstance card,out CharacterTestBehaviorExecutionMovement movement,out string issue)
            {Preparations++;movement=default;issue=Available?"":"preview:Unavailable";return Available;}
            public bool TrySampleBehindPreview(MxFramework.Game.CharacterTest.TacticalCard.TacticalCardInstance card,in CharacterTestBehaviorExecutionMovement movement,out MxFramework.Game.CharacterTest.TacticalCard.TacticalWorldPoint origin,out CharacterTestRuntimeAiLabMovementTargetSample sample)
            {Samples++;origin=default;sample=default;return Available;}
            public bool TryPrepare(MxFramework.Game.CharacterTest.TacticalCard.TacticalCardInstance card,out string issue)
            {ExecutionCalls++;issue="unexpected";return false;}
            public bool TryAdmit(MxFramework.Game.CharacterTest.TacticalCard.TacticalCardInstance card,out ulong id,out string issue)
            {ExecutionCalls++;id=0;issue="unexpected";return false;}
            public MxFramework.Game.CharacterTest.TacticalCard.ExecutionObservation Observe(ulong id){ExecutionCalls++;return default;}
            public bool TryAbortPreAction(ulong id,string reason){ExecutionCalls++;return false;}
        }
        private static MxFramework.Game.CharacterTest.TacticalCard.TacticalSingleCardDriver Driver(MxFramework.Game.CharacterTest.TacticalCard.ITacticalExecutionPort port)
            => new MxFramework.Game.CharacterTest.TacticalCard.TacticalSingleCardDriver(port,
                new MxFramework.Game.CharacterTest.TacticalCard.TacticalTimeCoordinator(new PreviewTime()),
                new MxFramework.Game.CharacterTest.TacticalCard.TacticalDeckManager(5),new MxFramework.Game.CharacterTest.TacticalCard.MomentumLedger());
        [Test]
        public void DriverUsesReadOnlyPreviewCapabilityWithoutExecuting()
        {
            var port=new PreviewPort();var driver=Driver(port);
            Assert.IsTrue(driver.TryPrepareBehindPreview(null,out var movement,out _));
            Assert.IsTrue(driver.TrySampleBehindPreview(null,movement,out _,out _));
            port.Available=false;
            Assert.IsFalse(driver.TryPrepareBehindPreview(null,out _,out var issue));Assert.AreEqual("preview:Unavailable",issue);
            Assert.IsFalse(driver.TrySampleBehindPreview(null,movement,out _,out _));
            port.Available=true;
            Assert.IsTrue(driver.TrySampleBehindPreview(null,movement,out _,out _),"Availability is sampled again without rebuilding the selected movement.");
            Assert.AreEqual(2,port.Preparations);Assert.AreEqual(3,port.Samples);Assert.AreEqual(0,port.ExecutionCalls);Assert.AreEqual(0,driver.ExecutionId);
        }
        [TestCase(false)] [TestCase(true)]
        public void ActualStageWrappersExposePreviewBeforeActorBinding(bool mixed)
        {
            // Skip native world construction, but invoke the real wrapper and real Driver forwarding path.
            var type=mixed?typeof(MxFramework.Game.CharacterTest.TacticalCard.TacticalMixedPlayerCardRuntime)
                :typeof(MxFramework.Game.CharacterTest.TacticalCard.TacticalCardStageRuntime);
            var wrapper=(MxFramework.Game.CharacterTest.TacticalCard.ITacticalExecutionPort)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            var driver=Driver(wrapper);
            Assert.IsFalse(driver.TryPrepareBehindPreview(null,out var movement,out var issue));
            Assert.AreEqual("preview:ActorNotBound",issue,"The request must reach the actual runtime wrapper, not fail a concrete Adapter cast.");
            Assert.IsFalse(driver.TrySampleBehindPreview(null,movement,out _,out _));
        }

        private static MxFramework.CharacterAction.CharacterActionConfig Action(int owner = 93001)
            => new MxFramework.CharacterAction.CharacterActionConfig(new MxFramework.CharacterAction.CharacterActionId(301),
                "CPU cooldown fixture", MxFramework.CharacterAction.CharacterActionCategory.BasicAttack, default, null, 0, 10,
                new[] {new MxFramework.CharacterAction.CharacterActionRequirement(MxFramework.CharacterAction.CharacterActionRequirementKind.WeaponTypeIndependent),
                    new MxFramework.CharacterAction.CharacterActionRequirement(MxFramework.CharacterAction.CharacterActionRequirementKind.CooldownReady, referenceId:owner),
                    new MxFramework.CharacterAction.CharacterActionRequirement(MxFramework.CharacterAction.CharacterActionRequirementKind.ResourceAvailable,value:999,referenceId:1)},
                null,null,null);
        private static MxFramework.Gameplay.AbilityRuntimeModule Abilities(out MxFramework.Gameplay.AbilityDefinitionConfig definition)
        {
            definition=new MxFramework.Gameplay.AbilityDefinitionConfig(93001,"cpu.cooldown","CPU cooldown fixture",null,
                new MxFramework.Gameplay.AbilityCooldownConfig(20,0),Array.Empty<MxFramework.Gameplay.AbilityCostConfig>(),0,
                new MxFramework.Gameplay.AbilityActionBindingConfig(new MxFramework.CharacterAction.CharacterActionId(301),1),default,0,0);
            var registry=new MxFramework.Gameplay.AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(definition,out var issue),issue);
            return new MxFramework.Gameplay.AbilityRuntimeModule(registry);
        }
        [TestCase(0,20,false)] [TestCase(19,1,false)] [TestCase(20,0,true)]
        public void ConfiguredActionCooldownGateUsesExistingOwnerAndExactDeadline(int frame,int remaining,bool available)
        {
            var abilities=Abilities(out var definition);abilities.CommitActivation(1,definition,MxFramework.Runtime.RuntimeFrame.Zero);
            var authority=new CharacterTestActionUseAuthority(null,abilities);
            string before=Newtonsoft.Json.JsonConvert.SerializeObject(abilities.CreateStateSnapshot());
            var result=authority.EvaluateCooldown(1,Action(),new MxFramework.Runtime.RuntimeFrame(frame),out long actual);
            Assert.AreEqual(available,result.Available);Assert.AreEqual(remaining,actual);
            Assert.AreEqual(before,Newtonsoft.Json.JsonConvert.SerializeObject(abilities.CreateStateSnapshot()));
            Assert.AreEqual(0,authority.CommitCount);Assert.AreEqual(0,authority.CommitQueryCount);
            Assert.IsTrue(authority.EvaluateCooldown(2,Action(),new MxFramework.Runtime.RuntimeFrame(frame),out _).Available);
        }
        [Test]
        public void MissingCooldownOwnerIsExplicitAndOtherResourceRequirementsAreNotMoved()
        {
            var authority=new CharacterTestActionUseAuthority(null,Abilities(out _));
            Assert.AreEqual(CharacterTestActionUseStatus.AbilityOwnerMissing,authority.EvaluateCooldown(1,Action(93002),default,out _).Status);
            Assert.IsTrue(authority.EvaluateCooldown(1,Action(),default,out _).Available,
                "No resource world is supplied: a cooldown-only query must not evaluate or pay other requirements.");
        }
        [Test]
        public void ConfiguredBindingsAcrossTemplatesReadCurrentActionCooldown()
        {
            var abilities=Abilities(out var definition);var authority=new CharacterTestActionUseAuthority(null,abilities);
            var registry=new MxFramework.CharacterAction.CharacterActionConfigRegistry(Array.Empty<MxFramework.CharacterAction.CharacterActionSetConfig>(),new[]{Action()});
            var first=new Dictionary<string,int>{{"attack",301}};var second=new Dictionary<string,int>{{"different-slot",301}};
            Assert.IsTrue(MxFramework.Game.CharacterTest.TacticalCard.CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(
                first,registry,authority,1,default,out _));
            abilities.CommitActivation(1,definition,MxFramework.Runtime.RuntimeFrame.Zero);
            Assert.IsFalse(MxFramework.Game.CharacterTest.TacticalCard.CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(
                first,registry,authority,1,default,out var issue));Assert.AreEqual("execution:BoundActionOnCooldown",issue);
            Assert.IsFalse(MxFramework.Game.CharacterTest.TacticalCard.CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(
                second,registry,authority,1,default,out issue));Assert.AreEqual("execution:BoundActionOnCooldown",issue);
            Assert.IsTrue(MxFramework.Game.CharacterTest.TacticalCard.CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(
                new Dictionary<string,int>(),registry,authority,1,default,out _));
            Assert.IsTrue(MxFramework.Game.CharacterTest.TacticalCard.CharacterTestTacticalExecutionAdapter.ValidateBoundActionCooldowns(
                first,registry,authority,1,new MxFramework.Runtime.RuntimeFrame(20),out _));
            Assert.AreEqual(0,authority.CommitCount,"Selection must not commit an action, resources or cooldown.");
        }
    }
    public sealed class CharacterTestBehindTargetTests
    {
        // CPU fixture values only, never approved production parameters.
        private static CharacterTestTacticalSharedDefinition Definition(bool maintain = false, int frames = 12)
            => CharacterTestTacticalAuthoringTemplates.CreateBehindTargetDefinition(maintain,20,25,2,3,
                0.05f,0.1f,frames,0.65f,1,2);
        private static CharacterTestBehaviorExecutionProgram Compile(bool maintain = false,int frames = 12)
        {
            var source=Definition(maintain,frames).Source;
            var roundTrip=CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(source));
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(roundTrip,
                CharacterTestTacticalCardInputs.BindSlots(1,new Dictionary<string,CharacterTestTacticalCardInputs>()),
                out var compiled,out var issues),string.Join(";",issues));
            return compiled.Program.Execution;
        }
        private static CharacterTestRuntimeAiLabMovementTargetSample Pose(double bearing,int targetYaw=0,float distance=2.5f,int targetId=22)
        {
            double radians=bearing*Math.PI/180;
            var toward=new Vector2((float)-Math.Sin(radians),(float)-Math.Cos(radians));
            return new CharacterTestRuntimeAiLabMovementTargetSample(true,distance,toward,toward,true,targetYaw,distance+0.5f,targetId);
        }
        private sealed class Runtime : ICharacterTestBehaviorExecutionRuntime, ICharacterTestBehaviorExecutionAttackWindowRuntime
        {
            public CharacterTestRuntimeAiLabMovementTargetSample Sample=Pose(0);
            public bool Exists=true;
            public Vector2 Move;
            public CharacterTestBehaviorControlInput Control;
            public int Controls;
            public bool WindowsActive = true;
            public int Captures;
            public bool TryCaptureTargetAttackWindows(out int target, out long instance, out int[] windows)
            { Captures++; target=22; instance=101; windows=new[]{7}; return Exists; }
            public bool IsCapturedTargetAvailable(int target) => Exists && target==22;
            public bool AnyCapturedTargetAttackWindowActive(int target, long instance, int[] windows)
                => WindowsActive && target==22 && instance==101 && windows.Contains(7);
            public int EntityId=>1;
            public bool Evaluate(in CharacterTestBehaviorExecutionPredicate p)=>Exists;
            public bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)=>Exists;
            public bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement m,out CharacterTestRuntimeAiLabMovementTargetSample sample)
            {sample=Sample;return Exists;}
            public bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn turn,out int x,out int z)
            {x=0;z=0;return Exists;}
            public bool TryStartAction(int action)=>false;
            public bool TryStopAction(long owner) { ActionCommandState=CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded; return true; }
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState { get; private set; }
            public CharacterTestRuntimeAiLabLogicActionState ActualActionState=>CharacterTestRuntimeAiLabLogicActionState.None;
            public long ActualActionInstanceId=>0;
            public long StartedActionInstanceId=>0;
            public void CompleteActionCommand(){ActionCommandState=CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None;}
            public void ObserveOwnedAction(long instance){}
            public void SetExecutionControl(Vector2 move,float speed,in CharacterTestBehaviorControlInput control)
            {Move=move;Control=control;Controls++;}
        }
        private static CharacterTestBehaviorExecutionEvaluator Start(CharacterTestBehaviorExecutionProgram program,Runtime runtime)
        {
            var evaluator=new CharacterTestBehaviorExecutionEvaluator(program,runtime);
            Assert.IsTrue(evaluator.Start(-1));evaluator.Tick(0);return evaluator;
        }
        [TestCase(false)] [TestCase(true)]
        public void TwoModesRoundTripWithoutProductSideOptions(bool maintain)
        {
            var program=Compile(maintain);
            Assert.AreEqual(0,program.Movements[0].ExplicitSide);
            Assert.AreEqual(maintain,program.Movements[0].BehindTarget.Maintain);
            Assert.AreEqual(maintain?CharacterTestBehaviorBlockCompletion.Timed:CharacterTestBehaviorBlockCompletion.Arrived,program.Blocks[0].Completion);
            Assert.AreEqual(2,program.Events.Length);
            Assert.AreEqual(7,Start(program,new Runtime()).CreateSnapshot().Version);
        }
        [Test]
        public void OnlyTwoDefinitionsWithDistinctModeIdentities()
        {
            Assert.AreNotEqual(Definition().DefinitionId,Definition(true).DefinitionId);
            Assert.AreNotEqual(Compile().Identity,Compile(true).Identity);
            Assert.IsFalse(Definition().DefinitionId.Contains("left"));Assert.IsFalse(Definition().DefinitionId.Contains("right"));
        }
        [Test]
        public void AlreadyBehindArrivesOnFirstSampleWithoutMoving()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(),runtime);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed,evaluator.State);
            Assert.AreEqual(Vector2.zero,runtime.Move);Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
        }
        [Test]
        public void AlreadyBehindHoldStopsAndContinuesFacing()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true),runtime);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running,evaluator.State);
            Assert.AreEqual(Vector2.zero,runtime.Move);Assert.AreEqual(0,runtime.Control.MaxMoveStepMillimeters);
            Assert.AreEqual(2,runtime.Control.FacingMode);
        }
        [TestCase(30,1)] [TestCase(-30,-1)]
        public void InitialRearEntryUsesShorterArc(int bearing,int side)
        {
            var runtime=new Runtime{Sample=Pose(bearing)};Start(Compile(),runtime);
            var tangent=new Vector2(-runtime.Sample.TowardTarget.y,runtime.Sample.TowardTarget.x)*side;
            Assert.Greater(Vector2.Dot(runtime.Move,tangent),0.99f);
            Assert.AreEqual(0,Vector2.Dot(runtime.Move,runtime.Sample.TowardTarget),0.00001f);
        }
        [TestCase(30000,1)] [TestCase(-30000,-1)]
        public void BothTargetTurnDirectionsResumeViaNearRearEntry(int yaw,int side)
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true),runtime);
            runtime.Sample=Pose(180,yaw);evaluator.Tick(1);
            var tangent=new Vector2(-runtime.Sample.TowardTarget.y,runtime.Sample.TowardTarget.x)*side;
            Assert.Greater(Vector2.Dot(runtime.Move,tangent),0.99f);
            Assert.Greater(runtime.Control.MaxMoveStepMillimeters,0);
            Assert.Less(runtime.Control.MaxMoveStepMillimeters,1000); // 10 degree entry cap at fixture root radius 3m.
            runtime.Sample=Pose(180+yaw/1000.0,yaw);evaluator.Tick(2);
            Assert.AreEqual(Vector2.zero,runtime.Move);
        }
        [Test]
        public void ExactOppositeTieSelectsDeterministicPositiveOrbit()
        {
            var runtime=new Runtime();var evaluator=Start(Compile(),runtime);
            Assert.AreEqual(1,Math.Sign(runtime.Move.x));Assert.AreEqual(1,evaluator.CreateSnapshot().RearOrbitSides.Single(s=>s!=0));
            var other=new Runtime();Start(Compile(),other);Assert.AreEqual(runtime.Move,other.Move);
        }
        [Test]
        public void NearOppositeTieRetainsSideUntilLeavingHysteresis()
        {
            var runtime=new Runtime();var evaluator=Start(Compile(),runtime);
            runtime.Sample=Pose(-0.5);evaluator.Tick(1);
            Assert.AreEqual(1,evaluator.CreateSnapshot().RearOrbitSides.Single(s=>s!=0));
            runtime.Sample=Pose(0.5);evaluator.Tick(2);
            Assert.AreEqual(1,evaluator.CreateSnapshot().RearOrbitSides.Single(s=>s!=0));
            runtime.Sample=Pose(-5);evaluator.Tick(3);
            Assert.AreEqual(-1,evaluator.CreateSnapshot().RearOrbitSides.Single(s=>s!=0));
        }
        [Test]
        public void TargetMovesAndRadialCorrectionStopsAtDistance()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true),runtime);
            runtime.Sample=Pose(180,0,4);evaluator.Tick(1);
            Assert.Greater(Vector2.Dot(runtime.Move,runtime.Sample.TowardTarget),0.99f);
            Assert.AreEqual(950,runtime.Control.MaxMoveStepMillimeters);
            runtime.Sample=Pose(180);evaluator.Tick(2);Assert.AreEqual(Vector2.zero,runtime.Move);
        }
        [Test]
        public void HoldingAngleAndDistanceHysteresisPreventBoundaryChatter()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true),runtime);
            runtime.Sample=Pose(203,0,3.075f);evaluator.Tick(1);Assert.AreEqual(Vector2.zero,runtime.Move);
            runtime.Sample=Pose(207,0,3.2f);evaluator.Tick(2);Assert.Greater(runtime.Move.sqrMagnitude,0.1f);
        }
        [TestCase(false)] [TestCase(true)]
        public void MissingTargetFailsAndReleasesOwnedControl(bool maintain)
        {
            var runtime=new Runtime();var evaluator=Start(Compile(maintain),runtime);
            runtime.Exists=false;evaluator.Tick(1);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Failed,evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TargetMissing,evaluator.EndReason);
            Assert.IsTrue(runtime.Control.IsRelease);Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
        }
        [TestCase(false)] [TestCase(true)]
        public void InvalidOrientationRefusesInventedRear(bool maintain)
        {
            var runtime=new Runtime{Sample=new CharacterTestRuntimeAiLabMovementTargetSample(true,2.5f,Vector2.up,Vector2.up)};
            var evaluator=Start(Compile(maintain),runtime);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Failed,evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.ConditionRejected,evaluator.EndReason);
        }
        [TestCase(false)] [TestCase(true)]
        public void BlockedOrUnreachableMotionFailsAtBudget(bool maintain)
        {
            var evaluator=Start(Compile(maintain,3),new Runtime());
            for(int i=1;i<=5;i++)evaluator.Tick(i);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Failed,evaluator.State);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TimedOut,evaluator.EndReason);
        }
        [Test]
        public void PauseDoesNotAdvanceAndHoldEndsAtExplicitLimit()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true,3),runtime);
            evaluator.Tick(1);int local=evaluator.LocalFrame,controls=runtime.Controls;
            for(int i=0;i<20;i++)evaluator.Tick(1);
            Assert.AreEqual(local,evaluator.LocalFrame);Assert.AreEqual(controls,runtime.Controls);
            evaluator.Tick(2);evaluator.Tick(3);evaluator.Tick(4);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed,evaluator.State);
            Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
        }
        [Test]
        public void CancellationReleasesMovementAndTurn()
        {
            var runtime=new Runtime();var evaluator=Start(Compile(true),runtime);evaluator.Stop();
            Assert.AreEqual(Vector2.zero,runtime.Move);Assert.IsTrue(runtime.Control.IsRelease);
            Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
        }
        [Test]
        public void SnapshotRestoresDirectionLatchAndRejectsInvalidSides()
        {
            var program=Compile(true);var evaluator=Start(program,new Runtime());var snapshot=evaluator.CreateSnapshot();
            var runtime=new Runtime{Sample=Pose(-0.5)};
            var restored=new CharacterTestBehaviorExecutionEvaluator(program,runtime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));restored.Tick(1);
            Assert.AreEqual(1,restored.CreateSnapshot().RearOrbitSides.Single(s=>s!=0));
            var invalid=snapshot.Clone();invalid.RearOrbitSides[0]=2;
            Assert.IsFalse(new CharacterTestBehaviorExecutionEvaluator(program,new Runtime()).TryRestoreSnapshot(invalid));
            invalid=snapshot.Clone();invalid.RearOrbitSides=Array.Empty<int>();
            Assert.IsFalse(new CharacterTestBehaviorExecutionEvaluator(program,new Runtime()).TryRestoreSnapshot(invalid));
            Assert.AreEqual(1,snapshot.RearOrbitSides.Single(s=>s!=0));
        }
        [Test]
        public void SnapshotRestoresHoldHysteresisWithoutMoving()
        {
            var program=Compile(true);var original=Start(program,new Runtime{Sample=Pose(180)});
            var runtime=new Runtime{Sample=Pose(203)};
            var restored=new CharacterTestBehaviorExecutionEvaluator(program,runtime);
            Assert.IsTrue(restored.TryRestoreSnapshot(original.CreateSnapshot()));restored.Tick(1);
            Assert.AreEqual(Vector2.zero,runtime.Move);
        }
        [Test]
        public void CurrentTargetChangesUseCurrentPose()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true),runtime);
            runtime.Sample=Pose(0);evaluator.Tick(1);Assert.Greater(runtime.Move.sqrMagnitude,0.1f);
            runtime.Sample=Pose(180);evaluator.Tick(2);Assert.AreEqual(Vector2.zero,runtime.Move);
        }
        [Test]
        public void RetargetClearsPriorHoldToleranceAndDirectionLatch()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true),runtime);
            runtime.Sample=Pose(203,targetId:23);evaluator.Tick(1);
            Assert.Greater(runtime.Move.sqrMagnitude,0.1f,"A new target must enter its own rear band before holding.");
            Assert.AreEqual(-1,evaluator.CreateSnapshot().RearOrbitSides.Single(s=>s!=0));
            runtime.Sample=Pose(0.5,targetId:24);evaluator.Tick(2);
            Assert.AreEqual(1,evaluator.CreateSnapshot().RearOrbitSides.Single(s=>s!=0),"The old target's tie latch must not choose the new target's side.");
        }
        [Test]
        public void RetargetAtDeadlineCannotReuseOldHoldSuccess()
        {
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(Compile(true,1),runtime);
            runtime.Sample=Pose(203,targetId:23);evaluator.Tick(1);
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.TimedOut,evaluator.EndReason);
        }
        [Test]
        public void RestoredTargetIdentityKeepsSameTargetHysteresisAndRejectsInvalidCapture()
        {
            var program=Compile(true);var evaluator=Start(program,new Runtime{Sample=Pose(180)});
            var snapshot=evaluator.CreateSnapshot();var runtime=new Runtime{Sample=Pose(203)};
            var restored=new CharacterTestBehaviorExecutionEvaluator(program,runtime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));restored.Tick(1);Assert.AreEqual(Vector2.zero,runtime.Move);
            var altered=snapshot.Clone();altered.RearTargetIds[0]=-1;
            Assert.IsFalse(restored.TryRestoreSnapshot(altered));
            altered=snapshot.Clone();altered.RearTargetIds=Array.Empty<int>();Assert.IsFalse(restored.TryRestoreSnapshot(altered));
            altered=snapshot.Clone();altered.RearTargetIds[Array.FindIndex(snapshot.RearTargetIds,id=>id==0)]=23;Assert.IsFalse(restored.TryRestoreSnapshot(altered));
            Assert.AreEqual(22,snapshot.RearTargetIds.Single(id=>id!=0));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidSpatialSamplesFailWithoutInventingRearOrArrow(int kind)
        {
            var direction=kind==2?new Vector2(float.NaN,1):Vector2.up;
            var sample=new CharacterTestRuntimeAiLabMovementTargetSample(true,
                kind==0?float.NaN:kind==1?float.PositiveInfinity:2.5f,direction,direction,true,0,kind==3?0:3,22);
            Assert.AreEqual(CharacterTestBehindTargetPreviewStatus.Unavailable,CharacterTestBehindTargetPreview.Create(Compile().Movements[0],sample).Status);
            var evaluator=Start(Compile(),new Runtime{Sample=sample});
            Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.ConditionRejected,evaluator.EndReason);
        }
        [TestCase(false)] [TestCase(true)]
        public void InitialPreviewSelectsOnlyActualInitialBehindMovement(bool maintain)
        {
            Assert.IsTrue(CharacterTestBehindTargetPreview.TrySelectInitialMovement(Compile(maintain),out var move));
            Assert.AreEqual(maintain,move.BehindTarget.Maintain);
            var source=Definition(maintain).Source;source.Timelines[0].EventBlocks[0].Start.Frame=2;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source,
                CharacterTestTacticalCardInputs.Unbound(1),out var delayed,out var issues),string.Join(";",issues));
            Assert.IsFalse(CharacterTestBehindTargetPreview.TrySelectInitialMovement(delayed.Program.Execution,out _));
        }

        private static CharacterTestBehaviorExecutionProgram Sequence(params CharacterTestTacticalSharedDefinition[] definitions)
        {
            var source=new CharacterTestTacticalDslSource {CardId="cpu.behind-waits",Name="CPU integration fixture",
                Definitions=definitions.ToList(),Root=new CharacterTestTacticalDslNode {NodeId="root",Kind="Sequence",
                    Children=definitions.Select((definition,index)=>new CharacterTestTacticalDslNode {NodeId="use."+index,
                        Kind="Reference",DefinitionId=definition.DefinitionId}).ToList()}};
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source,CharacterTestTacticalCardInputs.Unbound(1),out var compiled,out var issue),issue);
            return compiled.Program.Execution;
        }
        [Test]
        public void BehindAndBothPassiveWaitsShareOneOwnerAndCompleteInSequence()
        {
            var program=Sequence(Definition(),CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSecondsDefinition(),
                CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistanceDefinition(new CharacterTestTacticalDistanceData{Maximum=3},8));
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(program,runtime);
            long? owner=runtime.Controls>0?runtime.Control.OwnerToken:(long?)null;
            for(int frame=1;frame<150 && (evaluator.State==CharacterTestRuntimeAiLabLogicRuntimeState.Running
                || evaluator.State==CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning);frame++)
            {
                evaluator.Tick(frame); Assert.AreEqual(Vector2.zero,runtime.Move);
                if(runtime.Controls==0)continue;
                Assert.IsTrue(runtime.Control.IsOwnerOnlyRelease);
                if(owner.HasValue)Assert.AreEqual(owner.Value,runtime.Control.OwnerToken);else owner=runtime.Control.OwnerToken;
            }
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed,evaluator.State);
            Assert.That(evaluator.CreateSnapshot().BlockResults,Is.All.EqualTo(2));
        }
        [Test]
        public void CombinedRearAndAttackWaitSnapshotPreservesBothStatesAndHashes()
        {
            var program=Sequence(Definition(),CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunityDefinition(12));
            var runtime=new Runtime{Sample=Pose(180)};var evaluator=Start(program,runtime);
            for(int frame=1;frame<5 && evaluator.CreateSnapshot().AttackWaitBlock<0;frame++)evaluator.Tick(frame);
            var snapshot=evaluator.CreateSnapshot();Assert.GreaterOrEqual(snapshot.AttackWaitBlock,0);Assert.AreEqual(7,snapshot.Version);
            Assert.AreEqual(22,snapshot.RearTargetIds.Single(id=>id!=0));Assert.AreEqual(22,snapshot.AttackWaitTargetId);
            var restoredRuntime=new Runtime{Sample=Pose(180)};
            var restored=new CharacterTestBehaviorExecutionEvaluator(program,restoredRuntime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));Assert.AreEqual(Hash(snapshot),Hash(restored.CreateSnapshot()));
            var changed=snapshot.Clone();changed.RearTargetIds[0]=23;Assert.AreNotEqual(Hash(snapshot),Hash(changed));
            changed=snapshot.Clone();changed.AttackWaitWindowIds[0]=8;Assert.AreNotEqual(Hash(snapshot),Hash(changed));
            restoredRuntime.WindowsActive=false;
            for(int frame=snapshot.LastWorldFrame+1;frame<snapshot.LastWorldFrame+6;frame++)restored.Tick(frame);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed,restored.State);Assert.AreEqual(0,restoredRuntime.Captures);
        }
        private static long Hash(CharacterTestBehaviorExecutionState state)
        { var hash=new MxFramework.Runtime.RuntimeHashAccumulator(1001,1);state.AddHash(hash);return hash.ToHash(); }
        [Test]
        public void CompletionModesCannotBeSwapped()
        {
            var source=Definition(true).Source;source.Timelines[0].EventBlocks[0].Completion="Arrived";
            source.Timelines[0].EventBlocks[0].CompletionEventId="move";
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Any(d=>d.Code=="BehindTargetContract"));
        }
        [Test]
        public void ProductSidesAndInvalidHysteresisAreRejected()
        {
            var source=Definition().Source;source.Timelines[0].Events[0].Operation.Movement.Side=1;
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Any(d=>d.Code=="MovementInvalid"));
            Assert.Throws<ArgumentException>(()=>CharacterTestTacticalAuthoringTemplates.CreateBehindTargetDefinition(
                true,20,25,2,3,0,0,12,0.65f,1,float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Definition(true,0));
        }
        [TestCase(false)] [TestCase(true)]
        public void DescriptionHasShorterArcAndCorrectCompletion(bool maintain)
        {
            var definition=Definition(maintain);var timeline=definition.Source.Timelines[0];
            Assert.IsTrue(CharacterTestTacticalOrbitHint.TryParse(definition,"legacy-use",timeline.EventBlocks[0],
                timeline.Events.ToArray(),12,1.0/60,null,null,out var hint));
            Assert.IsTrue(hint.Text.Contains("较短环绕弧"));
            Assert.IsFalse(hint.Text.Contains("左侧"));Assert.IsFalse(hint.Text.Contains("右侧"));
            Assert.IsTrue(hint.Text.Contains(maintain?"到位停步并持续保持":"到达后停止并完成"));
        }
        [Test]
        public void LegacyOrbitKeepsOriginalJsonWithoutNewFields()
        {
            var source=CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            Assert.IsFalse(CharacterTestTacticalAuthoringJson.Write(source).Contains("behindTarget"));
        }
        [Test]
        public void NearEnteringBoundaryDoesNotFreezeOnRounding()
        {
            bool stopped=false;int side=0;
            var vector=CharacterTestBehindTargetResolver.Resolve(Compile().Movements[0],Pose(180,20001),ref stopped,ref side,out int limit);
            Assert.IsFalse(stopped);Assert.AreEqual(1,limit);Assert.Greater(vector.sqrMagnitude,0.1f);
        }
        [TestCase(false)] [TestCase(true)]
        public void PreviewMatchesInitialRuntimeIncludingRadialCorrection(bool maintain)
        {
            var program=Compile(maintain);var sample=Pose(30,0,1.5f);
            var preview=CharacterTestBehindTargetPreview.Create(program.Movements[0],sample);
            var runtime=new Runtime{Sample=sample};Start(program,runtime);
            Assert.AreEqual(CharacterTestBehindTargetPreviewStatus.Moving,preview.Status);
            Assert.AreEqual(runtime.Move,preview.Direction);
            Assert.Less(Vector2.Dot(preview.Direction,sample.TowardTarget),-0.1f);
        }
        [TestCase(false)] [TestCase(true)]
        public void PreviewAtRearDoesNotDrawFalseMovement(bool maintain)
        {
            var preview=CharacterTestBehindTargetPreview.Create(Compile(maintain).Movements[0],Pose(180));
            Assert.AreEqual(maintain?CharacterTestBehindTargetPreviewStatus.Holding:CharacterTestBehindTargetPreviewStatus.Arrived,preview.Status);
            Assert.AreEqual(Vector2.zero,preview.Direction);
        }
        [Test]
        public void PreviewInvalidTargetHasNoDirectionAndFreshTargetRecomputes()
        {
            var move=Compile(true).Movements[0];
            Assert.AreEqual(CharacterTestBehindTargetPreviewStatus.Unavailable,CharacterTestBehindTargetPreview.Create(move,default).Status);
            var first=CharacterTestBehindTargetPreview.Create(move,Pose(180,30000));
            var second=CharacterTestBehindTargetPreview.Create(move,Pose(180,-30000));
            Assert.Less(Vector2.Dot(first.Direction,second.Direction),-0.99f);
            Assert.AreEqual(CharacterTestBehindTargetPreviewStatus.Unavailable,CharacterTestBehindTargetPreview.Create(move,default).Status);
        }
        [Test]
        public void FeetArrowUsesResolvedDirectionAndContainsNoPathPrediction()
        {
            var move=Compile().Movements[0];var preview=CharacterTestBehindTargetPreview.Create(move,Pose(30,0,1.5f));
            var origin=new MxFramework.Game.CharacterTest.TacticalCard.TacticalWorldPoint(1000,2000,3000);
            var vertices=new Vector3[5];
            Assert.IsTrue(TacticalPointSelectionPreview.TryBuildDirectionArrow(origin,preview.Direction,vertices));
            Assert.AreEqual(new Vector3(1,2.06f,3),vertices[0]);
            var shaft=vertices[1]-vertices[0];
            Assert.AreEqual(0.9f,shaft.magnitude,0.00001f);
            Assert.Greater(Vector2.Dot(new Vector2(shaft.x,shaft.z).normalized,preview.Direction.normalized),0.99999f);
            Assert.AreEqual(vertices[1],vertices[3]);
        }
        [Test]
        public void FeetArrowRefusesStoppedInvalidOrMissingDirection()
        {
            var vertices=new Vector3[5];
            Assert.IsFalse(TacticalPointSelectionPreview.TryBuildDirectionArrow(default,Vector2.zero,vertices));
            Assert.IsFalse(TacticalPointSelectionPreview.TryBuildDirectionArrow(default,new Vector2(float.NaN,1),vertices));
            Assert.IsFalse(TacticalPointSelectionPreview.TryBuildDirectionArrow(default,Vector2.right,null));
        }
    }
}
