using System;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorRecipeV5Tests
    {
        [Test]
        public void ExecutionTables_CompileWithoutStageAndPreserveSource()
        {
            var source = CreateV6Recipe();
            string before = JsonConvert.SerializeObject(source);
            var compiled = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null, executionOnly: true);
            Assert.IsTrue(compiled.Success, FormatDiagnostics(compiled.Diagnostics));
            Assert.IsNull(compiled.Program.RecipeV6);
            Assert.IsEmpty(compiled.Program.Stages);
            Assert.AreEqual(before, JsonConvert.SerializeObject(source));
            var execution = compiled.Program.Execution;
            Assert.AreEqual(3, execution.Events.Length);
            Assert.AreEqual(1, execution.Movements.Length);
            Assert.Greater(execution.Conditions.Length, 0);
            Assert.Greater(execution.MaximumConditionDepth, 0);
            long identity = execution.Identity;
            source.Timelines[0].Events.Clear();
            Assert.AreEqual(3, execution.Events.Length);
            Assert.AreEqual(identity, execution.Identity);
        }

        [Test]
        public void ExecutionTables_SamplingOrderAndIdentityIgnoreSourceListOrderAndDebugMap()
        {
            var source = CreateExecutionBaselineSource(7, 211);
            var first = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null, executionOnly: true);
            source.Timelines[0].Events.Reverse();
            source.Timelines[0].Title = "renamed authoring title";
            var second = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null, executionOnly: true, includeDebugMap: false);
            Assert.IsTrue(first.Success, FormatDiagnostics(first.Diagnostics));
            Assert.IsTrue(second.Success, FormatDiagnostics(second.Diagnostics));
            Assert.IsNull(second.Program.ExecutionDebugMap);
            Assert.AreEqual(first.Program.Execution.Identity, second.Program.Execution.Identity);
            for (int i = 0; i < first.Program.Execution.Events.Length; i++)
                Assert.AreEqual(first.Program.Execution.Events[i].Rank, second.Program.Execution.Events[i].Rank);
        }

        [Test]
        public void ExecutionTables_NativeApproachAndAllDistanceTargetMetricsAreExplicit()
        {
            var source = CreateExecutionBaselineSource(4, 211);
            for (int i = 0; i < 3; i++)
            {
                var operation = source.Timelines[0].Events[i].Operation;
                operation.MovementTargetKind = (CharacterTestRuntimeAiLabMovementTargetKind)i;
                operation.MoveTargetEntityId = 17;
                operation.MoveMinDistance = 1;
                operation.MoveMaxDistance = 2;
            }
            var compiled = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null, executionOnly: true);
            Assert.IsTrue(compiled.Success, FormatDiagnostics(compiled.Diagnostics));
            foreach (var movement in compiled.Program.Execution.Movements)
            {
                Assert.AreEqual(0f, movement.Minimum);
                Assert.AreEqual(0f, movement.Maximum);
                Assert.AreEqual(movement.Target switch
                {
                    CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget => CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance,
                    CharacterTestRuntimeAiLabMovementTargetKind.Entity => CharacterTestBehaviorDistanceMetric.RootPlanarDistance,
                    _ => CharacterTestBehaviorDistanceMetric.RootToWorldPointPlanarDistance
                }, movement.Metric);
            }
        }

        [TestCase((int)CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked)]
        [TestCase((int)CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget)]
        public void ExecutionTables_SideParityMatchesExistingMovementForSourceAndEntityIdentities(int strategy)
        {
            var source = CreateExecutionBaselineSource(7, 211);
            for (int i = 0; i < 6; i++)
            {
                source.Timelines[0].Events[i].Operation.MovementStrategy = (CharacterTestRuntimeAiLabMovementStrategy)strategy;
                source.Timelines[0].Events[i].Operation.MoveMinDistance = 1;
                source.Timelines[0].Events[i].Operation.MoveMaxDistance = 2;
            }
            var legacy = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null);
            var compiled = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null, executionOnly: true);
            Assert.IsTrue(compiled.Success, FormatDiagnostics(compiled.Diagnostics));
            for (int i = 0; i < compiled.Program.Execution.Events.Length; i++)
            {
                var evt = compiled.Program.Execution.Events[i];
                if (evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget) continue;
                string id = compiled.Program.ExecutionDebugMap.Event(i);
                CharacterTestRuntimeAiLabCompiledLogicStage stage = null;
                foreach (var oldEvent in legacy.Program.RecipeV6.Events)
                    if (oldEvent.EventId == id) stage = oldEvent.ActionStage;
                Assert.NotNull(stage);
                for (int entity = 1; entity < 33; entity++)
                {
                    var sample = new CharacterTestRuntimeAiLabMovementTargetSample(true, 1.5f, Vector2.right, Vector2.right);
                    Vector2 expected = CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, sample, entity);
                    Assert.AreEqual(Mathf.Sign(expected.y), compiled.Program.Execution.Movements[evt.Parameter].ResolveSide(entity));
                }
            }
        }

        [Test]
        public void ExecutionTables_RejectMissingDependencyAndFingerprintFullActionContent()
        {
            var source = CreateExecutionBaselineSource(2, 211);
            var evt = source.Timelines[0].Events[0];
            evt.Operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
            evt.Operation.CharacterActionId = new CharacterActionId(900001);
            var missing = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, null, executionOnly: true);
            Assert.IsFalse(missing.Success);
            Assert.IsNull(missing.Program);
            var firstCatalog = ExecutionActionCatalog(1);
            var secondCatalog = ExecutionActionCatalog(2);
            Assert.AreEqual(firstCatalog.StableHash, secondCatalog.StableHash, "Old ID/duration hash deliberately cannot see the changed action priority.");
            var first = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, firstCatalog, executionOnly: true);
            var second = CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, secondCatalog, executionOnly: true);
            Assert.IsTrue(first.Success, FormatDiagnostics(first.Diagnostics));
            Assert.IsTrue(second.Success, FormatDiagnostics(second.Diagnostics));
            Assert.AreNotEqual(first.Program.Execution.Identity, second.Program.Execution.Identity);
        }

        [Test]
        public void ExecutionTables_LoadBoundaryRejectsConditionCycles()
        {
            Assert.Throws<ArgumentException>(() => new CharacterTestBehaviorExecutionProgram(0, 0, "",
                new[] { new CharacterTestBehaviorExecutionTimeline(0, 0, 0, 0) }, Array.Empty<CharacterTestBehaviorExecutionEvent>(),
                Array.Empty<int>(), Array.Empty<CharacterTestBehaviorExecutionAction>(), Array.Empty<CharacterTestBehaviorExecutionMovement>(),
                Array.Empty<CharacterTestBehaviorExecutionFlow>(),
                new[] { new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not,
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown, -1, 0, 1, -1, default) },
                Array.Empty<CharacterTestBehaviorExecutionPredicate>(), new[] { 0 }));
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog ExecutionActionCatalog(int priority)
        {
            var action = new CharacterActionConfig(new CharacterActionId(900001), "execution test", CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored, Array.Empty<string>(), priority, 211,
                null, null, null, null);
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
                new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), new[] { action }));
        }
    }
}
