using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardCompilerTests
    {
        private string _directory;
        private CharacterActionReferenceProbeSession _session;
        private CharacterTestTacticalCardInputs _inputs;

        // Mutation tests restore shared loaded arrays in finally; this fixture must execute sequentially.
        [OneTimeSetUp]
        public void PrepareMeasuredInputs()
        {
            _directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/MxFramework/TacticalCardCompilerTests", Guid.NewGuid().ToString("N")));
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            _session = CharacterActionReferenceProbeSession.ForCurrentOpener(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, measurement.ReferenceDummy);
            var store = new CharacterActionTacticalMetricsStore(_directory);
            var measured = store.Refresh(_session, measurement);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
            Assert.IsTrue(CharacterTestTacticalCardInputTests.Prepare(_session, store, measurement, out _inputs, out string issue), issue);
        }

        [OneTimeTearDown]
        public void DisposeMeasuredInputs()
        {
            _session?.Dispose();
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/MxFramework/TacticalCardCompilerTests")) + Path.DirectorySeparatorChar;
            if (_directory != null && _directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void RealActionCompilesDirectlyIntoSharedExecutionWithInclusiveStartupCoverage()
        {
            var source = new CharacterTestTacticalCardDefinition("Measured first card", CharacterTestTacticalCardApproach.OptimalRange);
            string sourceBefore = source.Write();
            string actionBefore = JsonConvert.SerializeObject(_inputs.Action);
            var result = Compile(source);
            var program = result.Program;
            Assert.IsNotNull(program.Execution);
            Assert.AreEqual(0, program.Stages.Count);
            Assert.IsNull(program.RecipeV6, "The tactical source must not be lowered through a persisted Stage/Recipe representation.");
            Assert.AreEqual(1, program.Execution.Timelines.Length);
            Assert.AreEqual(1 + _session.Action.DurationFrames.Value, program.Execution.Timelines[0].DurationFrames);
            Assert.AreEqual(4, program.Execution.Events.Length);
            var gate = Event(program.Execution, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition);
            var action = Event(program.Execution, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction);
            var movement = Event(program.Execution, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget);
            var turn = Event(program.Execution, CharacterTestBehaviorExecutionOperations.TurnToTarget);
            Assert.AreEqual(0, gate.StartFrame);
            Assert.AreEqual(1, gate.EndFrame);
            Assert.AreEqual(1, action.StartFrame);
            Assert.AreEqual(1 + _session.Action.DurationFrames.Value, action.EndFrame);
            Assert.AreEqual(_session.Action.ActionId.Value, program.Execution.Actions[action.Parameter].ActionId);
            Assert.AreEqual(0, movement.StartFrame);
            Assert.AreEqual(2 + Array.Find(_session.Action.Phases, p => p.Kind == CharacterActionPhaseKind.Startup).EndFrame, movement.EndFrame, "The full inclusive authored startup retains approach ownership.");
            Assert.AreEqual(0, turn.StartFrame);
            Assert.AreEqual(1 + _session.Action.DurationFrames.Value, turn.EndFrame, "Turn has its own complete-card range.");
            var movementParameters = program.Execution.Movements[movement.Parameter];
            Assert.IsTrue(movementParameters.IsOptimalRange);
            Assert.AreEqual(CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance, movementParameters.Metric);
            Assert.AreEqual((float)_inputs.SelectedRange.MinDistance.Value, movementParameters.Minimum);
            Assert.AreEqual((float)_inputs.SelectedRange.MaxDistance.Value, movementParameters.Maximum);
            Assert.AreEqual((float)_inputs.SelectedRange.OptimalDistance.Value, movementParameters.Optimal,
                "The compiler consumes the selected first run's verified approach point.");
            Assert.AreEqual(3, program.Execution.Conditions[gate.Condition].ChildCount);
            Assert.AreEqual(program.Execution.ActivationCondition, action.Condition, "Entry rechecks the same alive predicate; the Gateway owns resource admission.");
            Assert.AreNotEqual(gate.Condition, action.Condition, "Surface range and facing release the gate; they are not an aggregate hit-opportunity entry guard.");
            Assert.AreEqual(sourceBefore, source.Write());
            Assert.AreEqual(actionBefore, JsonConvert.SerializeObject(_inputs.Action));
        }

        [Test]
        public void MeasuredFirstRangeControlsBothMovementAndDistanceGate()
        {
            var result = Compile(new CharacterTestTacticalCardDefinition("First measured range", CharacterTestTacticalCardApproach.OptimalRange));
            var first = _inputs.SpatialSummary.Intervals[0];
            Assert.AreSame(_inputs.SpatialSummary, result.Summary, "Compilation retains complete original evidence.");
            var execution = result.Program.Execution;
            var move = execution.Movements[0];
            Assert.AreEqual(first.FirstHitMillimeters / 1000f, move.Minimum);
            Assert.AreEqual(first.LastHitMillimeters / 1000f, move.Maximum);
            int distancePredicates = 0;
            foreach (var predicate in execution.Predicates)
            {
                if (predicate.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance) continue;
                distancePredicates++;
                Assert.AreEqual(first.FirstHitMillimeters / 1000f, predicate.Value);
                Assert.AreEqual(first.LastHitMillimeters / 1000f, predicate.Maximum);
                for (int i = 1; i < result.Summary.Intervals.Count; i++)
                    Assert.Greater(result.Summary.Intervals[i].FirstHitMillimeters / 1000f, predicate.Maximum,
                        "Later runs and the separating gap cannot release the first-range gate.");
            }
            Assert.AreEqual(1, distancePredicates);
        }

        [Test]
        public void DebugMetadataAndDisplayNameCannotChangeExecutionIdentityOrFormalDependencies()
        {
            var source = new CharacterTestTacticalCardDefinition("First title", CharacterTestTacticalCardApproach.OptimalRange);
            var first = Compile(source);
            var repeat = Compile(source);
            var renamed = Compile(new CharacterTestTacticalCardDefinition("Second title", source.Approach));
            Assert.IsTrue(CharacterTestTacticalCardCompiler.TryCompile(source, _inputs, out var compact, out string issue,
                includeDebugMap: false), issue);
            Assert.AreEqual(first.Program.Execution.Identity, repeat.Program.Execution.Identity);
            Assert.AreEqual(first.Program.Execution.Identity, renamed.Program.Execution.Identity);
            Assert.AreEqual(first.Program.Execution.Identity, compact.Program.Execution.Identity);
            Assert.AreEqual(first.SourceFingerprint, renamed.SourceFingerprint);
            Assert.AreEqual(_inputs.DependencyFingerprint, first.DependencyFingerprint);
            Assert.IsNotNull(first.Program.ExecutionDebugMap);
            Assert.IsNull(compact.Program.ExecutionDebugMap);
            Assert.AreEqual(0, compact.SourceMapping.Length);
            Assert.Greater(first.SourceMapping.Length, 0);
            bool slotMapped = false;
            bool eligibilityMapped = false;
            foreach (var mapping in first.SourceMapping)
            {
                if (mapping.SourcePath == "slot")
                {
                    Assert.AreEqual("attack", mapping.EventId);
                    StringAssert.Contains(_inputs.SlotSource, mapping.Derivation);
                    StringAssert.Contains(_inputs.ActionSetId, mapping.Derivation);
                    slotMapped = true;
                }
                if (mapping.SourcePath == "eligibility") eligibilityMapped = true;
            }
            Assert.IsTrue(slotMapped);
            Assert.IsTrue(eligibilityMapped);
            string description = first.Describe();
            StringAssert.Contains(first.Summary.ActionContentHash, description);
            StringAssert.Contains(first.Summary.ReferenceDummyHash, description);
            StringAssert.Contains(first.DependencyFingerprint, description);
            StringAssert.Contains(first.Summary.ProberVersion, description);
        }

        [Test]
        public void DisablingApproachRemovesOnlyBaseMovementAndRetainsEligibilityActionAndTurn()
        {
            var approach = Compile(new CharacterTestTacticalCardDefinition("First card", CharacterTestTacticalCardApproach.OptimalRange));
            var stationary = Compile(new CharacterTestTacticalCardDefinition("First card", CharacterTestTacticalCardApproach.None));
            Assert.AreEqual(approach.DependencyFingerprint, stationary.DependencyFingerprint);
            Assert.AreNotEqual(approach.SourceFingerprint, stationary.SourceFingerprint);
            Assert.AreNotEqual(approach.Program.Execution.Identity, stationary.Program.Execution.Identity);
            var execution = stationary.Program.Execution;
            Assert.AreEqual(0, execution.Movements.Length);
            Assert.AreEqual(3, execution.Events.Length);
            foreach (var evt in execution.Events)
                Assert.AreNotEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget, evt.Kind);
            var gate = Event(execution, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition);
            var action = Event(execution, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction);
            var turn = Event(execution, CharacterTestBehaviorExecutionOperations.TurnToTarget);
            Assert.AreEqual(0, gate.StartFrame);
            Assert.AreEqual(1, action.StartFrame);
            Assert.AreEqual(1 + _session.Action.DurationFrames.Value, action.EndFrame);
            Assert.AreEqual(1 + _session.Action.DurationFrames.Value, turn.EndFrame);
            Assert.AreEqual(3, execution.Conditions[gate.Condition].ChildCount);
            Assert.AreEqual(execution.ActivationCondition, action.Condition);
            Assert.AreEqual(_session.Action.ActionId.Value, execution.Actions[action.Parameter].ActionId);
        }

        [Test]
        public void PreparedActionMutationCannotMixNewPhasesWithOldSummaryAndTiming()
        {
            var source = new CharacterTestTacticalCardDefinition("Mutation rejection", CharacterTestTacticalCardApproach.OptimalRange);
            Assert.IsTrue(_inputs.TryValidatePreparedAction(out string issue), issue);
            int startupIndex = Array.FindIndex(_inputs.Action.Phases, p => p.Kind == CharacterActionPhaseKind.Startup);
            int activeIndex = Array.FindIndex(_inputs.Action.Phases, p => p.Kind == CharacterActionPhaseKind.Active);
            Assert.GreaterOrEqual(startupIndex, 0);
            Assert.GreaterOrEqual(activeIndex, 0);
            var startup = _inputs.Action.Phases[startupIndex];
            var active = _inputs.Action.Phases[activeIndex];
            int? duration = _inputs.Action.DurationFrames;
            try
            {
                // Still a valid contiguous action with the same duration and hit window, but a different windup.
                _inputs.Action.Phases[startupIndex] = new CharacterActionPhase(CharacterActionPhaseKind.Startup,
                    startup.StartFrame, startup.EndFrame - 1, startup.CombatPhaseAnchor, startup.RequiresCombatPhaseMatch);
                _inputs.Action.Phases[activeIndex] = new CharacterActionPhase(CharacterActionPhaseKind.Active,
                    active.StartFrame - 1, active.EndFrame, active.CombatPhaseAnchor, active.RequiresCombatPhaseMatch);
                Assert.AreEqual(duration, _inputs.Action.DurationFrames);
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateAction(_inputs.Action, out issue), issue);
                Assert.IsFalse(_inputs.TryValidatePreparedAction(out issue));
                Assert.AreEqual("Action:PreparedContentChanged", issue);
                Assert.IsFalse(CharacterTestTacticalCardCompiler.TryCompile(source, _inputs, out var rejected, out issue));
                Assert.IsNull(rejected);
                StringAssert.Contains("PreparedContentChanged", issue);
            }
            finally
            {
                _inputs.Action.Phases[startupIndex] = startup;
                _inputs.Action.Phases[activeIndex] = active;
            }
            Assert.IsTrue(_inputs.TryValidatePreparedAction(out issue), issue);
            Assert.IsTrue(CharacterTestTacticalCardCompiler.TryCompile(source, _inputs, out var restored, out issue), issue);
            Assert.AreEqual(2 + startup.EndFrame, Event(restored.Program.Execution,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget).EndFrame);
        }

        private CharacterTestTacticalCardCompilation Compile(CharacterTestTacticalCardDefinition source)
        {
            Assert.IsTrue(CharacterTestTacticalCardCompiler.TryCompile(source, _inputs, out var result, out string issue), issue);
            return result;
        }

        private static CharacterTestBehaviorExecutionEvent Event(CharacterTestBehaviorExecutionProgram execution,
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind)
        {
            int count = 0;
            CharacterTestBehaviorExecutionEvent found = default;
            foreach (var evt in execution.Events)
                if (evt.Kind == kind) { found = evt; count++; }
            Assert.AreEqual(1, count, "Expected one " + kind + " event.");
            return found;
        }
    }
}
