using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardRuntimeTests
    {
        [Test]
        public void RangeEdgeReleasesBeforeOptimalAndRealGatewayHitsThenCleansUp()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var summary = fixture.Inputs.Metrics.SpatialSummary;
                fixture.PlaceTarget(summary.Intervals[0].LastHitMillimeters);
                int initialHealth = fixture.Target.CombatState.Health;
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                for (int i = 0; i < 180 && fixture.Card.IsExecuting; i++) fixture.Step();
                Assert.IsFalse(fixture.Card.IsExecuting, fixture.Card.LastIssue);
                Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, fixture.Card.State);
                var instances = new HashSet<long>();
                bool windupLastFrameBounded = false;
                bool activeFrameMovementReleased = false;
                bool enteredBeforeOptimal = false;
                foreach (var sample in fixture.Samples)
                {
                    if (sample.ActionInstance > 0) instances.Add(sample.ActionInstance);
                    if (sample.CursorBefore == 1 + fixture.Inputs.StartupEndFrame && sample.MoveLimit >= 0) windupLastFrameBounded = true;
                    if (sample.CursorBefore == 2 + fixture.Inputs.StartupEndFrame && sample.MoveLimit == -1) activeFrameMovementReleased = true;
                    if (sample.CursorBefore == 0 && sample.CursorAfter == 1)
                        enteredBeforeOptimal |= sample.RootDistance > summary.OptimalDistance.Value + 0.05;
                }
                Assert.IsTrue(enteredBeforeOptimal, "The range-edge Gate must release before approach reaches its optimum.");
                Assert.AreEqual(1, instances.Count, "One fixed card action creates exactly one real Gateway instance.");
                Assert.IsTrue(windupLastFrameBounded, "The last authored startup frame retains bounded movement ownership.");
                Assert.IsTrue(activeFrameMovementReleased, "The first active frame no longer receives the base approach bound.");
                Assert.Less(fixture.Target.CombatState.Health, initialHealth, "Formal Combat must apply the measured attack to the real target.");
                Assert.AreNotEqual(MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, fixture.Source.Action.State);
                Assert.IsFalse(fixture.Source.Input.HasActiveMove);
                Assert.AreNotEqual(2, fixture.Source.Input.BehaviorControl.FacingMode, "Natural completion releases independent turning.");
                WriteEvidence("range-edge-completion", fixture);
            }
        }

        [Test]
        public void NoApproachStillTurnsThroughFormalConsumerWithoutBaseMovement()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(CharacterTestTacticalCardApproach.None))
            {
                fixture.Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0, -90000));
                fixture.PlaceTarget(2000, 20000);
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                for (int i = 0; i < 12; i++) fixture.Step();
                Assert.AreEqual(0, fixture.Source.Transform.XMillimeters);
                Assert.AreEqual(0, fixture.Source.Transform.ZMillimeters);
                Assert.AreNotEqual(-90000, fixture.Source.Transform.YawMilliDegrees, "Independent facing changes real authority yaw.");
                Assert.AreEqual(0, fixture.Card.LocalFrame, "The high target keeps formal attack opportunity blocked.");
                foreach (var sample in fixture.Samples)
                {
                    Assert.IsFalse(sample.HasMove);
                    Assert.AreEqual(0, sample.ActionInstance);
                }
                fixture.StopAndDrain();
                WriteEvidence("none-independent-turn", fixture);
            }
        }

        [Test]
        public void SameWorldAndAssemblyApproachChoiceChangesActualDisplacement()
        {
            using (var approach = new CharacterTestTacticalCardRuntimeFixture())
            using (var stationary = new CharacterTestTacticalCardRuntimeFixture(CharacterTestTacticalCardApproach.None))
            {
                int distance = approach.Inputs.Metrics.SpatialSummary.Intervals[0].LastHitMillimeters + 1000;
                approach.PlaceTarget(distance, 20000);
                stationary.PlaceTarget(distance, 20000);
                Assert.AreEqual(approach.Inputs.DependencyFingerprint, stationary.Inputs.DependencyFingerprint);
                Assert.IsTrue(approach.Start(), approach.Card.LastIssue);
                Assert.IsTrue(stationary.Start(), stationary.Card.LastIssue);
                for (int i = 0; i < 12; i++) { approach.Step(); stationary.Step(); }
                Assert.Greater(approach.Source.Transform.ZMillimeters, 0);
                Assert.AreEqual(0, stationary.Source.Transform.ZMillimeters);
                Assert.AreEqual(0, stationary.Source.Transform.XMillimeters);
                Assert.AreEqual(0, approach.Card.LocalFrame);
                Assert.AreEqual(0, stationary.Card.LocalFrame);
                approach.StopAndDrain();
                stationary.StopAndDrain();
                WriteEvidence("choice-optimal", approach);
                WriteEvidence("choice-none", stationary);
            }
        }

        [Test]
        public void BlockedGateApproachesOptimalHoldsInsideRangeAndRestartsOutside()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var summary = fixture.Inputs.Metrics.SpatialSummary;
                fixture.PlaceTargetAtSurfaceDistance(summary.Intervals[0].LastHitMillimeters + 500);
                FaceAway(fixture);
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                // Keep the facing gate blocked while independently exercising real approach and hysteresis.
                for (int i = 0; i < 50; i++) { FaceAway(fixture); fixture.Step(); }
                Assert.AreEqual(0, fixture.Card.LocalFrame);
                Assert.IsTrue(fixture.Facts.TargetDistanceAvailable);
                Assert.That(fixture.Facts.TargetDistance, Is.EqualTo(summary.OptimalDistance.Value).Within(0.01));
                Assert.IsFalse(fixture.Source.Input.HasActiveMove);
                int stoppedZ = fixture.Source.Transform.ZMillimeters;
                fixture.PlaceTargetAtSurfaceDistance(summary.Intervals[0].LastHitMillimeters - 100);
                for (int i = 0; i < 4; i++) { FaceAway(fixture); fixture.Step(); }
                Assert.AreEqual(stoppedZ, fixture.Source.Transform.ZMillimeters,
                    "After reaching optimum, target movement within the admissible interval does not restart approach.");
                fixture.PlaceTargetAtSurfaceDistance(summary.Intervals[0].LastHitMillimeters + 300);
                for (int i = 0; i < 3; i++) { FaceAway(fixture); fixture.Step(); }
                Assert.Greater(fixture.Source.Transform.ZMillimeters, stoppedZ);
                Assert.AreEqual(0, fixture.Card.LocalFrame);
                fixture.StopAndDrain();
                WriteEvidence("optimal-hysteresis", fixture);
            }
        }

        [Test]
        public void PositiveMinimumFromOffsetReferenceProducesRealBackwardMovementWhenTooClose()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(offsetDummy: true))
            {
                int minimum = fixture.Inputs.Metrics.SpatialSummary.Intervals[0].FirstHitMillimeters;
                Assert.Greater(minimum, 200, "An explicitly offset reference geometry supplies a real positive lower bound.");
                fixture.PlaceTargetAtSurfaceDistance(minimum - 200);
                FaceAway(fixture);
                float initial = fixture.RootDistance;
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                for (int i = 0; i < 5; i++) { FaceAway(fixture); fixture.Step(); }
                Assert.Less(fixture.Source.Transform.ZMillimeters, 0);
                Assert.Greater(fixture.RootDistance, initial);
                Assert.AreEqual(0, fixture.Card.LocalFrame);
                fixture.StopAndDrain();
                WriteEvidence("too-close-retreat", fixture);
            }
        }

        [Test]
        public void PlaybackEntryDoesNotAddAnImplicitTargetLifeCondition()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(CharacterTestTacticalCardApproach.None))
            {
                int distance = fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value;
                fixture.PlaceTargetAtSurfaceDistance(distance);
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                var timeline = source.Timelines[0];
                var action = timeline.Events.Single(e => e.EventId == "attack");
                action.ConditionRoot = timeline.Events.Single(e => e.EventId == "release").ConditionRoot;
                source.ActivationConditionRoot = null;
                timeline.Events.RemoveAll(e => e != action);
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, fixture.Inputs, out var compiled, out var issues), string.Join(";", issues));
                var current = new SelectedInputs(new CharacterTestTacticalCardRuntimeInputs(fixture.Runtime, fixture.Source.Id.Value, fixture.Store, fixture.Measurement), fixture.Probe.Action.ActionId.Value);
                Assert.IsTrue(fixture.Card.TryCompilePublished("playback", CharacterTestTacticalAuthoringJson.SemanticFingerprint(source), compiled.Program, fixture.Inputs, current), fixture.Card.LastIssue);
                Assert.IsTrue(fixture.Card.TryStartPublished(fixture.Facts, new RuntimeFrame(fixture.NextFrame)), fixture.Card.LastIssue);
                bool changedBeforeCommit = false;
                for (int i = 0; i < 40 && !changedBeforeCommit; i++)
                    fixture.Step(_ => {
                        if (!fixture.Card.HasPendingActionCommand) return;
                        // Change target facts between the timeline request and command consumption.
                        fixture.Target.SyncCombatHealth(100, 0);
                        changedBeforeCommit = true;
                    });
                Assert.IsTrue(changedBeforeCommit, "The configured action must reach command submission.");
                fixture.Step(); // Observe the formal command receipt on the following card sample.
                Assert.AreEqual(-1,fixture.Card.LastActionConditionRejectedEvent);
                Assert.Greater(fixture.Card.OwnedActionInstanceId,0, fixture.Card.State + ":" + fixture.Card.EndReason + ":" + fixture.Card.LastIssue);
                fixture.PlaceTargetAtSurfaceDistance(distance);
                fixture.AdvanceWorldOnly();
                for (int i = 0; i < 100 && fixture.Card.IsExecuting; i++) fixture.Step();
                Assert.IsFalse(fixture.Card.IsExecuting, fixture.Card.LastIssue);
                Assert.IsTrue(fixture.Samples.Exists(sample=>sample.ActionInstance>0));
                WriteEvidence("playback-entry-no-implicit-target-condition", fixture);
            }
        }

        [TestCase(30, true)]
        [TestCase(300, true)]
        public void OrdinaryApproachDurationDoesNotGateFollowingPlayback(int approachFrames, bool plays)
        {
            var json = File.ReadAllText("Authoring/RuntimeAiLab/TestCards/Shared/c58e549368ba4e64976b10fafbc00992.json");
            var record = JsonConvert.DeserializeObject<CharacterTestConfiguredCard>(json);
            var source = CharacterTestTacticalPublishedSource.Read(record.Source).CloneDefinitions().Single().Source;
            source.Timelines.Single().EventBlocks.Single(b => b.Completion == "Arrived").Timing.DurationFrames = approachFrames;
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 455, sourceCharacterId: 668367837))
            {
                fixture.PlaceTarget(10000);
                fixture.AdvanceWorldOnly();
                fixture.RefreshFacts();
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, fixture.Inputs, out var compiled, out var issues), string.Join(";", issues));
                var current = new SelectedInputs(new CharacterTestTacticalCardRuntimeInputs(fixture.Runtime, fixture.Source.Id.Value, fixture.Store, fixture.Measurement), 455);
                Assert.IsTrue(fixture.Card.TryCompilePublished("single-card-budget", CharacterTestTacticalAuthoringJson.SemanticFingerprint(source), compiled.Program, fixture.Inputs, current), fixture.Card.LastIssue);
                Assert.IsTrue(fixture.Card.TryStartPublished(fixture.Facts, new RuntimeFrame(fixture.NextFrame)), fixture.Card.LastIssue);
                for (int i = 0; i < 500 && fixture.Card.IsExecuting; i++) fixture.Step();
                Assert.IsFalse(fixture.Card.IsExecuting, fixture.Card.LastIssue);
                Assert.AreEqual(plays, fixture.Samples.Exists(s => s.ActionInstance > 0 && s.ActionId == 455));
                Assert.AreEqual(plays ? CharacterTestBehaviorExecutionEndReason.Completed : CharacterTestBehaviorExecutionEndReason.TimedOut, fixture.Card.EndReason);
            }
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void ExplicitArrivalBranchControlsPlaybackAfterOrdinaryMovement(bool condition, bool near, bool plays)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateVisualAttack();
            source.ActivationConditionRoot = null;
            var moveSource = source.Clone();
            moveSource.Timelines[0].Events.RemoveAll(e => e.EventId == "attack" || e.EventId == "facing.action");
            moveSource.Timelines[0].EventBlocks.RemoveAll(b => b.BlockId == "attack-block");
            var actionSource = source.Clone();
            actionSource.Timelines[0].Events.RemoveAll(e => e.EventId != "attack" && e.EventId != "facing.action");
            actionSource.Timelines[0].EventBlocks.RemoveAll(b => b.BlockId != "attack-block");
            actionSource.Timelines[0].EventBlocks[0].Start = CharacterTestTacticalAuthoringTemplates.Absolute(0);
            var actionNode = new CharacterTestTacticalDslNode { NodeId = "play", Kind = "Reference", DefinitionId = "action" };
            var branch = condition ? new CharacterTestTacticalDslNode { NodeId = "distance-check", Kind = "If",
                Condition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "arrived",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    SourceEventId = "move/approach", MovementQuery = "Arrived" },
                Children = new List<CharacterTestTacticalDslNode> { actionNode } } : actionNode;
            var document = new CharacterTestTacticalDslSource { CardId = "explicit-distance-test",
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new() { NodeId = "move", Kind = "Reference", DefinitionId = "movement" }, branch } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> {
                    new() { DefinitionId = "movement", Source = moveSource }, new() { DefinitionId = "action", Source = actionSource } } };
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 455, sourceCharacterId: 668367837))
            {
                fixture.PlaceTargetAtSurfaceDistance(near ? fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value : 10000);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out string issue), issue);
                var current = new SelectedInputs(new CharacterTestTacticalCardRuntimeInputs(fixture.Runtime, fixture.Source.Id.Value, fixture.Store, fixture.Measurement), 455);
                Assert.IsTrue(fixture.Card.TryCompilePublished("explicit-distance", compiled.DependencyIdentity, compiled.Program, fixture.Inputs, current), fixture.Card.LastIssue);
                Assert.IsTrue(fixture.Card.TryStartPublished(fixture.Facts, new RuntimeFrame(fixture.NextFrame)), fixture.Card.LastIssue);
                for (int i = 0; i < 500 && fixture.Card.IsExecuting; i++) fixture.Step();
                Assert.AreEqual(CharacterTestBehaviorExecutionEndReason.Completed, fixture.Card.EndReason);
                Assert.AreEqual(plays, fixture.Samples.Exists(s => s.ActionInstance > 0 && s.ActionId == 455));
            }
        }

        private static void FaceAway(CharacterTestTacticalCardRuntimeFixture fixture)
        {
            var pose = fixture.Source.Transform;
            fixture.Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                pose.XMillimeters, pose.YMillimeters, pose.ZMillimeters, 180000));
        }

        [Test]
        public void MissingFactsReleasePreviouslyConsumedMovementAndFacing()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                fixture.PlaceTarget(fixture.Inputs.Metrics.SpatialSummary.Intervals[0].LastHitMillimeters + 1000, 20000);
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                for (int i = 0; i < 4; i++) fixture.Step();
                Assert.IsTrue(fixture.Source.Input.HasActiveMove);
                Assert.AreEqual(2, fixture.Source.Input.BehaviorControl.FacingMode);
                fixture.Card.Tick(null, new RuntimeFrame(fixture.NextFrame));
                fixture.AdvanceWorldOnly();
                Assert.IsFalse(fixture.Source.Input.HasActiveMove, "A missing fact frame must not leave the last approach command latched.");
                Assert.AreNotEqual(2, fixture.Source.Input.BehaviorControl.FacingMode);
                Assert.IsTrue(fixture.Card.IsExecuting, "Unavailable facts do not introduce an implicit tactical exit guard.");
                Assert.AreEqual(0, fixture.Card.LocalFrame);
                fixture.StopAndDrain();
                WriteEvidence("missing-facts-release", fixture);
            }
        }

        private sealed class SelectedInputs : ICharacterTestTacticalCardInputSource
        {
            private readonly ICharacterTestTacticalCardInputSource _source;
            private readonly int _action;
            internal SelectedInputs(ICharacterTestTacticalCardInputSource source, int action) { _source = source; _action = action; }
            public bool TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue) => _source.TryPrepare(_action, out inputs, out issue);
            public bool TryPrepare(int action, out CharacterTestTacticalCardInputs inputs, out string issue) => _source.TryPrepare(action, out inputs, out issue);
        }

        private static void WriteEvidence(string name, CharacterTestTacticalCardRuntimeFixture fixture)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task03ExecutionEvidence"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name + ".json"), JsonConvert.SerializeObject(new
            {
                test = name, evidence = "formal-card-session-command-gateway-combat",
                source = fixture.Definition.Write(), compilation = fixture.Card.Compilation.Describe(),
                samples = fixture.Samples
            }, Formatting.Indented));
        }
    }
}
