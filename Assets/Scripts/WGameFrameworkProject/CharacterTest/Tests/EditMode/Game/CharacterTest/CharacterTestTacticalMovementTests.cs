using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.CharacterAction;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalMovementTests
    {
        private CharacterActionReferenceProbeSession _probe;
        private CharacterTestTacticalCardInputs _inputs;
        [OneTimeSetUp] public void Setup()
        {
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            _probe = new CharacterActionReferenceProbeSession(new MxFramework.CharacterAction.CharacterActionId(301), measurement.ReferenceDummy);
            var store = new CharacterActionTacticalMetricsStore(Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Library/TacticalMovementTests/metrics", Guid.NewGuid().ToString("N"))));
            var measured = store.Refresh(_probe, measurement);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryPrepare(_probe.Runtime.ActionRuntime, _probe.Source, _probe.Runtime.PhysicsRuntime,
                _probe.Runtime.CombatTraceSystem.BakedTraceSource, _probe.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                store, measurement, out _inputs, out string issue, testActionId: 301), issue);
        }
        [OneTimeTearDown] public void Cleanup() => _probe?.Dispose();

        private static CharacterTestTacticalDslSource Source(CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            return new CharacterTestTacticalDslSource { CardId = "movement-card", Root = new CharacterTestTacticalDslNode {
                NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new CharacterTestTacticalDslNode { NodeId = "move-use", Kind = "Reference", DefinitionId = "shared.move" } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> {
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "shared.move", Source = CharacterTestTacticalAuthoringTemplates.CreateMovement(strategy) } } };
        }
        private CharacterTestTacticalDslCompilation Compile(CharacterTestTacticalDslSource source)
        {
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, _inputs, out var compiled, out var issue), issue);
            return compiled;
        }

        [TestCase("常规移动接敌出招")]
        [TestCase("绕侧进攻")]
        [TestCase("撤步还击")]
        [TestCase("贴身逼近")]
        public void OrdinaryMovementTemplatesHaveNoImplicitArrivalCompletion(string name)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateVisualCard(name);
            Assert.IsFalse(source.Timelines.SelectMany(t => t.EventBlocks).Any(b => b.Completion == "Arrived"));
            Assert.IsEmpty(CharacterTestTacticalAuthoringCompiler.ValidateShared(source));
        }
        private static CharacterTestTacticalMovement Movement(CharacterTestTacticalDslSource source)
            => source.Definitions[0].Source.Timelines[0].Events[0].Operation.Movement;
        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Query(string kind = "Arrived")
            => new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "movement-query", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                SourceEventId = "move-use/move", MovementQuery = kind };

        [TestCase(0, 5f, 1, 0)] [TestCase(0, 1f, 0, 0)]
        [TestCase(1, 5f, 1, 0)] [TestCase(1, 2f, 0, 0)] [TestCase(1, 1f, -1, 0)]
        [TestCase(2, 1f, -1, 0)] [TestCase(2, 5f, -1, 0)]
        [TestCase(3, 5f, 0, 1)]
        [TestCase(4, 1f, -1, 0)] [TestCase(4, 2f, 0, 0)]
        [TestCase(5, 5f, 1, 1)] [TestCase(5, 2f, 0, 1)] [TestCase(5, 1f, -1, 1)]
        public void SixStrategiesExecuteFromTacticalComposition(int strategy, float distance, int radial, int lateral)
        {
            var source = Source((CharacterTestRuntimeAiLabMovementStrategy)strategy);
            if (Movement(source).UsesSide) Movement(source).Side = 1;
            var program = Compile(source).Program;
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Distance = distance };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1);
            Assert.AreEqual(radial, Math.Sign(runtime.Move.x)); Assert.AreEqual(lateral, Math.Sign(runtime.Move.y));
            Assert.AreEqual(2, runtime.Control.FacingMode, "Explicit turning remains independent of movement arbitration.");
        }

        [TestCase(0, 0f, 0)] [TestCase(1, 0f, 0)] [TestCase(1, 2f, 1)]
        [TestCase(4, 0f, 0)] [TestCase(5, 2f, 1)]
        public void AuthoredZeroDistanceIsAnActualThresholdRatherThanLegacyUnconfigured(int strategy, float distance, int radial)
        {
            var source = Source((CharacterTestRuntimeAiLabMovementStrategy)strategy);
            Movement(source).Minimum = Movement(source).Maximum = 0;
            var program = Compile(source).Program;
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Distance = distance };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); Assert.AreEqual(radial, Math.Sign(runtime.Move.x));
        }

        [Test]
        public void DirectionWeightAndTargetSurviveStrictRoundtripAndChangeIdentity()
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            long before = Compile(source).Program.SemanticHash;
            var m = Movement(source); m.Side = -1; m.RadialWeight = 1.25f;
            m.Target = CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition; m.X = -4; m.Z = 12;
            var restored = CharacterTestTacticalDslJson.Read(CharacterTestTacticalDslJson.Write(source));
            var program = Compile(restored).Program;
            Assert.AreNotEqual(before, program.SemanticHash);
            var frozen = program.Execution.Movements[0];
            Assert.AreEqual(-1, frozen.ResolveSide(1)); Assert.AreEqual(-1, frozen.ResolveSide(2));
            Assert.AreEqual(1.25f, frozen.RadialWeight); Assert.AreEqual(-4, frozen.X); Assert.AreEqual(12, frozen.Z);
            m.RadialWeight = 3f; Assert.AreEqual(1.25f, frozen.RadialWeight, "Editing does not change the frozen table.");
        }

        [TestCase("ActionBand")] [TestCase("ActionOptimal")]
        public void IndependentMovementBindsAttackSlotWithoutDummyAction(string distanceSource)
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            Movement(source).DistanceSource = distanceSource; Movement(source).ActionSlotId = "primary";
            source.Root.Children[0].EndCondition = Query();
            var program = Compile(source).Program;
            Assert.IsEmpty(program.Execution.Actions.ToArray());
            var move = program.Execution.Movements[0];
            if (distanceSource == "ActionBand")
            { Assert.AreEqual((float)_inputs.SpatialSummary.MinDistance.Value, move.Minimum); Assert.AreEqual((float)_inputs.SpatialSummary.MaxDistance.Value, move.Maximum); }
            else Assert.That((float)_inputs.SpatialSummary.OptimalDistance.Value, Is.InRange(move.Minimum, move.Maximum));
            var predicate = program.Execution.Predicates.ToArray().Single(p => p.Kind == CharacterTestBehaviorExecutionPredicateKind.MovementArrived);
            Assert.AreEqual(move.Minimum, predicate.Movement.Minimum); Assert.AreEqual(move.Maximum, predicate.Movement.Maximum);
        }

        [Test]
        public void KeepDistanceStopsWithoutEndingAndResumesWhenTargetMoves()
        {
            var program = Compile(Source(CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance)).Program;
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Distance = 2f };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); Assert.AreEqual(Vector2.zero, runtime.Move);
            runtime.Distance = 5f; evaluator.Tick(2); Assert.Greater(runtime.Move.x, 0);
            Assert.AreNotEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void ArrivalTransfersToNextMovementAndSnapshotRemainsCompatible()
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            source.Root.Children[0].EndCondition = Query();
            source.Definitions.Add(new CharacterTestTacticalSharedDefinition { DefinitionId = "shared.retreat", Source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard) });
            source.Root.Children.Add(new CharacterTestTacticalDslNode { NodeId = "retreat-use", Kind = "Reference", DefinitionId = "shared.retreat" });
            var program = Compile(CharacterTestTacticalDslJson.Read(CharacterTestTacticalDslJson.Write(source))).Program;
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { Distance = 5f };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime, program.ExecutionDebugMap);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit(); Assert.Greater(runtime.Move.x, 0);
            var snapshot = evaluator.CreateSnapshot();
            var restored = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime, program.ExecutionDebugMap);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));
            runtime.Distance = 2f;
            for (int frame = 2; frame <= 5; frame++) { restored.Tick(frame); runtime.Commit(); }
            Assert.AreEqual("retreat-use", program.ExecutionDebugMap.Timeline(restored.CurrentTimelineIndex));
            Assert.Less(runtime.Move.x, 0);
        }

        [Test]
        public void MissingTargetEndsOnlyWhenConfiguredAndMissingReferenceRejects()
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget); source.Root.Children[0].EndCondition = Query("TargetMissing");
            var program = Compile(source).Program;
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { TargetExists = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            source.Root.Children[0].EndCondition.SourceEventId = "move-use/deleted";
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, _inputs, out _, out string issue));
            StringAssert.Contains("DSL_MOVEMENT_EVENT", issue);
        }

        [Test]
        public void InvalidParametersAndUnsupportedBindingsAreRejected()
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            Movement(source).RadialWeight = float.NaN;
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, _inputs, out _, out _));
            Movement(source).RadialWeight = 0.65f; Movement(source).Minimum = 5; Movement(source).Maximum = 2;
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, _inputs, out _, out _));
            Movement(source).Minimum = 1; Movement(source).DistanceSource = "ActionBand"; Movement(source).ActionSlotId = "missing";
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, _inputs, out _, out _));
            source = Source(CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked); source.Root.Children[0].EndCondition = Query();
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, _inputs, out _, out string issue)); StringAssert.Contains("DSL_MOVEMENT_ARRIVAL", issue);
        }

        [Test]
        public void ActualBridgeSeparatesUnavailableDistanceFromMissingTargetAndResolvesWorldPoint()
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            source.Root.Children[0].EndCondition = Query("TargetMissing");
            var program = Compile(source).Program;
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetTarget(_probe.Target.Id.Value, 0, targetDistanceAvailable: false, targetRootDistance: 5f,
                navigation: default, selfActionRunning: false, targetVisible: true, targetLineOfSightBlocked: false, targetLastSeenAgeFrames: 0, targetThreatScore: 1f);
            var bridge = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge(_probe.Source.Id.Value,
                "movement-test", program, _probe.Runtime.Entities, CharacterTestDirectAiNavigationService.Instance,
                _probe.Runtime.PerceptionFrameCache, _probe.Runtime, null);
            _probe.Runtime.TryGetLastCompletedRuntimeFrame(out var frame);
            bridge.SetFrame(_probe.Source, facts, frame, _probe.Runtime.ActionRuntime, null);
            Assert.IsFalse(bridge.TrySampleMovement(program.Execution.Movements[0], out _));
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, bridge);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1);
            Assert.AreNotEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State, "Missing distance does not end a target-missing range.");
            var world = new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition, CharacterTestBehaviorDistanceMetric.RootToWorldPointPlanarDistance,
                0, _probe.Source.Transform.X + 3, _probe.Source.Transform.Z + 4, 0.5f, 1f, 1, 0, independentFacing: true);
            Assert.IsTrue(bridge.TrySampleMovement(world, out var sample)); Assert.AreEqual(5f, sample.Distance, 0.001f);
            Assert.AreEqual(0.6f, sample.TowardTarget.x, 0.001f); Assert.AreEqual(0.8f, sample.TowardTarget.y, 0.001f);
            evaluator.Stop(true);
        }

        [Test]
        public void CompositionConditionGroupingAndUndoKeepMovementBinding()
        {
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget); source.Root.Children[0].EndCondition = Query();
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, source, out string id, out string issue), issue);
            var original = workspace.Cards[id].CompositionSourceJson;
            var token = new CharacterTestTacticalEditToken(workspace, workspace.Cards[id]);
            Assert.IsTrue(workspace.TryEditComposition(token, card => {
                var all = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "all", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All };
                all.Children.Add(card.Root.Children[0].EndCondition); card.Root.Children[0].EndCondition = all;
            }, "group", out issue), issue);
            Compile(workspace.ResolveComposition(workspace.Cards[id]));
            Assert.IsTrue(workspace.TryUndo(out issue), issue); Assert.AreEqual(original, workspace.Cards[id].CompositionSourceJson);
            Assert.IsTrue(workspace.TryRedo(out issue), issue);
            Assert.AreEqual("Arrived", workspace.Cards[id].CloneCompositionSource().Root.Children[0].EndCondition.Children[0].MovementQuery);
        }

        [Test]
        public void WorkspaceSaveReloadKeepsMovementAndBoundExit()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TacticalMovementTests", Guid.NewGuid().ToString("N")));
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
            var workspace = persistence.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
            var source = Source(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget);
            source.Root.Children[0].EndCondition = Query(); Movement(source).Side = -1;
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, source, out string id, out string issue), issue);
            var before = Compile(workspace.ResolveComposition(workspace.Cards[id])).Program.SemanticHash;
            persistence.MarkDirty(); Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
            var reloaded = new CharacterTestRuntimeAiLabDraftPersistence(root);
            Assert.IsTrue(reloaded.TryLoad(workspace.WorkspaceId, out var restored, out issue), issue);
            Assert.AreEqual(before, Compile(restored.ResolveComposition(restored.Cards[id])).Program.SemanticHash);
        }
    }
}
