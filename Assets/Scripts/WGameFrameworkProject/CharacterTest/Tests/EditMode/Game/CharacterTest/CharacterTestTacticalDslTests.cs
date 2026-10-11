using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalDslTests
    {
        private CharacterTestTacticalCardRuntimeFixture _fixture;
        [OneTimeSetUp] public void Setup() => _fixture = new CharacterTestTacticalCardRuntimeFixture();
        [OneTimeTearDown] public void Cleanup() => _fixture?.Dispose();

        private static CharacterTestTacticalDslNode Reference(string id = "use") => new CharacterTestTacticalDslNode
        { NodeId = id, Kind = "Reference", DefinitionId = "shared.attack" };

        private static CharacterTestTacticalDslSource Source(bool helper = false)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            source.ActivationConditionRoot = null;
            if (helper)
            {
                source.PredicateBindings.Clear();
                source.Timelines[0].Events.RemoveAll(e => e.EventId != "facing");
                source.Timelines[0].Events[0].End = CharacterTestTacticalAuthoringTemplates.Absolute(4);
            }
            return new CharacterTestTacticalDslSource { CardId = "card", Root = new CharacterTestTacticalDslNode
                { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> { Reference() } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> {
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "shared.attack", Source = source } } };
        }

        private CharacterTestTacticalDslCompilation Compile(CharacterTestTacticalDslSource source)
        {
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, _fixture.Inputs, out var result, out string issue), issue);
            return result;
        }

        private static CharacterTestTacticalDslNode Conditional(string kind) => new CharacterTestTacticalDslNode
        { NodeId = "group", Kind = kind, Condition = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack().ActivationConditionRoot,
            Children = new List<CharacterTestTacticalDslNode> { Reference() } };

        [Test]
        public void SingleSegmentStartsAtActualEntryAndCompletesWithoutLifecycleEvents()
        {
            var compiled = Compile(Source(true)); var program = compiled.Program;
            Assert.AreEqual(1, program.Execution.Timelines.Length); Assert.AreEqual(1, program.Execution.Events.Length);
            Assert.AreEqual("use", program.ExecutionDebugMap.Timeline(program.Execution.EntryTimeline));
            Assert.AreEqual(4, program.Execution.Timelines[program.Execution.EntryTimeline].DurationFrames);
            Assert.IsEmpty(program.Execution.Flows.ToArray());
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(program.Execution, runtime, program.ExecutionDebugMap);
            Assert.IsTrue(evaluator.Start(0)); Assert.AreEqual("use", program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex));
            evaluator.Tick(1); runtime.Commit();
            Assert.AreEqual(1, evaluator.CreateSnapshot().Counts[0], "The first real event must sample on the first execution tick.");
            for (int frame = 2; frame <= 5; frame++) { evaluator.Tick(frame); runtime.Commit(); }
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.IsTrue(runtime.Control.IsRelease);
            Assert.IsFalse(compiled.SourceLocations.Values.Any(l => l.Field == "normalCompletion" || l.Field == "entry"));
        }

        [Test]
        public void EmptyCardHasNoSyntheticEventsAndUsesNaturalCompletion()
        {
            var source = Source(true); source.Root.Children.Clear(); var compiled = Compile(source);
            Assert.IsEmpty(compiled.Program.Execution.Events.ToArray()); Assert.AreEqual(1, compiled.Program.Execution.Timelines.Length);
            Assert.AreEqual(0, compiled.Program.Execution.Timelines[0].DurationFrames);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit();
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void SequenceOnlyEmitsTheTransitionBetweenActualSegments()
        {
            var source = Source(true); source.Root.Children.Add(Reference("after")); var compiled = Compile(source);
            Assert.AreEqual(2, compiled.Program.Execution.Timelines.Length);
            var jumps = compiled.Program.Execution.Events.ToArray().Where(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline).ToArray();
            Assert.AreEqual(1, jumps.Length); Assert.AreEqual(4, jumps[0].StartFrame);
            Assert.AreEqual("after", compiled.Program.ExecutionDebugMap.Timeline(compiled.Program.Execution.Flows[jumps[0].Parameter].TargetTimeline));
            Assert.IsFalse(compiled.Program.Execution.Events.ToArray().Any(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner));
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); var visited = new HashSet<string>();
            for (int f = 1; f <= 15; f++) { evaluator.Tick(f); runtime.Commit(); visited.Add(compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex)); }
            CollectionAssert.AreEquivalent(new[] { "use", "after" }, visited);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void TerminalFalseBranchCompletesWithoutAnExitFallbackEvent()
        {
            var source = Source(true); var branch = Conditional("If");
            source.Root.Children = new List<CharacterTestTacticalDslNode> { branch };
            var compiled = Compile(source); var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { TargetExists = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit(); evaluator.Tick(2); runtime.Commit();
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.IsFalse(compiled.Program.Execution.Events.ToArray().Any(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner));
            Assert.IsFalse(compiled.SourceLocations.Keys.Any(k => k.EndsWith("$no", StringComparison.Ordinal)));
        }

        [Test]
        public void TerminalEmptyWaitHoldsThenCompletesWithoutAnExitEvent()
        {
            var source = Source(true); var wait = Conditional("Wait"); wait.Children.Clear(); source.Root.Children = new List<CharacterTestTacticalDslNode> { wait };
            var compiled = Compile(source); Assert.AreEqual(1, compiled.Program.Execution.Events.Length);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { TargetExists = false }; var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); for (int f = 1; f <= 3; f++) { evaluator.Tick(f); runtime.Commit(); }
            Assert.AreEqual(0, evaluator.LocalFrame); Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            runtime.TargetExists = true; evaluator.Tick(4); runtime.Commit(); evaluator.Tick(5); runtime.Commit();
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void ExplicitTerminalLocalEndStillCompilesItsConfiguredExit()
        {
            var source = Source(true); source.Root.EndCondition = Conditional("If").Condition; var compiled = Compile(source);
            var exit = compiled.Program.Execution.Events.ToArray().Single(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner);
            Assert.GreaterOrEqual(exit.Condition, 0);
            Assert.AreEqual("endCondition", compiled.SourceLocations[compiled.Program.ExecutionDebugMap.Event(exit.Rank)].Field);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub(); var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0)); evaluator.Tick(1); runtime.Commit();
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void FullExamplesRoundTripAndInvalidExamplesReject()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../StorySource/玩法基石/任务/任务04DSL样例"));
            foreach (string name in new[] { "single-attack", "shared-variant" })
            {
                var source = CharacterTestTacticalDslJson.Read(File.ReadAllText(Path.Combine(directory, name + ".json")));
                string text = CharacterTestTacticalDslJson.Write(source);
                Assert.AreEqual(text, CharacterTestTacticalDslJson.Write(CharacterTestTacticalDslJson.Read(text)));
                Assert.That(Compile(source).EventSources.Values.Any(p => p.Contains("/shared.attack@1/")));
            }
            var invalid = CharacterTestTacticalDslJson.Read(File.ReadAllText(Path.Combine(directory, "invalid-missing-reference.json")));
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(invalid, _fixture.Inputs, out var partial, out string issue));
            Assert.IsNull(partial); StringAssert.Contains("DSL_MISSING_DEFINITION", issue);
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalDslJson.Read(File.ReadAllText(Path.Combine(directory, "invalid-instance-override.json"))));
        }

        [Test]
        public void NormalBoundaryTracksTailAndDoesNotMutateSharedSource()
        {
            var source = Source();
            var before = Compile(source);
            Assert.AreEqual(77, before.NormalBoundaries["use"]);
            source.Definitions[0].Source.Timelines[0].Events.Single(e => e.EventId == "facing").End = CharacterTestTacticalAuthoringTemplates.Absolute(83);
            string text = CharacterTestTacticalDslJson.Write(source);
            var after = Compile(source);
            Assert.AreEqual(83, after.NormalBoundaries["use"]);
            Assert.AreNotEqual(before.Program.Execution.Identity, after.Program.Execution.Identity);
            Assert.AreEqual(text, CharacterTestTacticalDslJson.Write(source));
            Assert.AreEqual(77, before.NormalBoundaries["use"], "The prior compiled product remains frozen.");
            Assert.AreEqual(83, after.Program.Execution.Timelines[after.Program.Execution.EntryTimeline].DurationFrames);
            Assert.IsFalse(after.Program.Execution.Events.ToArray().Any(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline));
        }

        [Test]
        public void SecondActualLoadoutDerivesItsOwnBoundary()
        {
            using (var second = new CharacterTestTacticalCardRuntimeFixture(offsetDummy: true, actionId: 940000,
                sourceCharacterId: CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                measurement: CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement()))
            {
                var source = Source();
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, second.Inputs, out var result, out string issue), issue);
                Assert.AreEqual(121, result.NormalBoundaries["use"]);
                Assert.AreEqual(940000, result.Program.Execution.Actions[0].ActionId);
                Assert.AreNotEqual(Compile(source).DependencyIdentity, result.DependencyIdentity);
            }
        }

        [TestCase("revision")]
        [TestCase("nodeId")]
        [TestCase("unknown")]
        public void CodecRejectsCoercionAndUnknownFields(string mutation)
        {
            var token = JObject.Parse(CharacterTestTacticalDslJson.Write(Source()));
            if (mutation == "revision") token["definitions"][0]["revision"] = "1";
            else if (mutation == "nodeId") token["root"]["nodeId"] = 12;
            else token["root"]["parameters"] = new JObject();
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalDslJson.Read(token.ToString()));
        }

        [Test]
        public void ReferencedContentInvalidatesWithoutRevisionChangeButUnrelatedDefinitionDoesNot()
        {
            var source = Source(true); var first = Compile(source);
            source.Definitions.Add(new CharacterTestTacticalSharedDefinition { DefinitionId = "unused", Source = Source().Definitions[0].Source });
            source.Definitions.Reverse(); source.Name = "renamed";
            Assert.AreEqual(first.Program.Execution.Identity, Compile(source).Program.Execution.Identity);
            var shared = source.Definitions.Single(d => d.DefinitionId == "shared.attack");
            shared.Source.Timelines[0].Events[0].End.Frame = 7;
            Assert.AreNotEqual(first.Program.Execution.Identity, Compile(source).Program.Execution.Identity);
            shared.Revision++;
            Assert.AreNotEqual(first.DependencyIdentity, Compile(source).DependencyIdentity);
        }

        [Test]
        public void DuplicateReferenceKeepsStableEventSourcesAfterSiblingInsertion()
        {
            var source = Source(true); var first = Compile(source);
            source.Root.Children.Insert(0, Reference("another"));
            var second = Compile(source);
            foreach (var mapping in first.EventSources) Assert.AreEqual(mapping.Value, second.EventSources[mapping.Key]);
            Assert.AreEqual(2, second.EventSources.Count);
            Assert.AreEqual(2, second.NormalBoundaries.Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConditionalGroupIsSampledOnceAndFalseSkipsWholeScope(bool enter)
        {
            var source = Source(true); source.Root.Children = new List<CharacterTestTacticalDslNode> { Conditional("If"), Reference("after") };
            var compiled = Compile(source);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { TargetExists = enter };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            var visited = new HashSet<string>(); int evaluationsAtEntry = -1;
            for (int frame = 1; frame < 35; frame++)
            {
                evaluator.Tick(frame); runtime.Commit();
                string timeline = compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex);
                visited.Add(timeline);
                if (timeline == "use")
                {
                    if (evaluationsAtEntry < 0) evaluationsAtEntry = runtime.EvaluateCount;
                    runtime.TargetExists = false;
                }
            }
            Assert.AreEqual(enter, visited.Contains("use"));
            Assert.IsTrue(visited.Contains("after"));
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            if (enter) { Assert.AreEqual(1, evaluationsAtEntry); Assert.AreEqual(evaluationsAtEntry, runtime.EvaluateCount); }
        }

        [Test]
        public void WaitHoldsAtZeroThenPassesOnceWithoutResamplingInsideBody()
        {
            var source = Source(true); source.Root.Children = new List<CharacterTestTacticalDslNode> { Conditional("Wait") };
            var compiled = Compile(source);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub { TargetExists = false };
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            for (int f = 1; f <= 12; f++) { evaluator.Tick(f); runtime.Commit(); }
            Assert.AreEqual("group", compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex));
            Assert.AreEqual(0, evaluator.LocalFrame);
            runtime.TargetExists = true;
            int sampled = -1; bool entered = false;
            for (int f = 13; f <= 35; f++)
            {
                evaluator.Tick(f); runtime.Commit();
                if (compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex) == "use")
                { entered = true; if (sampled < 0) sampled = runtime.EvaluateCount; runtime.TargetExists = false; }
            }
            Assert.IsTrue(entered); Assert.AreEqual(sampled, runtime.EvaluateCount);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void ExplicitExitMakesFollowingReferenceUnreachable()
        {
            var source = Source(true);
            source.Root.Children.Insert(0, new CharacterTestTacticalDslNode { NodeId = "exit", Kind = "Exit" });
            var compiled = Compile(source); var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestBehaviorExecutionEvaluator(compiled.Program.Execution, runtime);
            Assert.IsTrue(evaluator.Start(0));
            for (int f = 1; f < 10; f++) { evaluator.Tick(f); runtime.Commit(); }
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
            Assert.AreEqual(Vector2.zero, runtime.Move);
            Assert.AreEqual("exit", compiled.Program.ExecutionDebugMap.Timeline(evaluator.CurrentTimelineIndex));
        }

        [TestCase("duplicate")]
        [TestCase("scope")]
        [TestCase("shared-activation")]
        public void UnsupportedStructureNeverReturnsPartialProgram(string mutation)
        {
            var source = Source();
            if (mutation == "duplicate") source.Root.Children.Add(Reference());
            else if (mutation == "scope") source.Root.Children[0].Children = new List<CharacterTestTacticalDslNode>();
            else source.Definitions[0].Source.ActivationConditionRoot = Conditional("If").Condition;
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, _fixture.Inputs, out var result, out string issue));
            Assert.IsNull(result); StringAssert.Contains("DSL_", issue);
        }

        [Test]
        public void RealHostCommandsActionMotionAndCombatConsumeTrialProgramAndReleaseControls()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var source = Source();
                source.Definitions[0].Source.Timelines[0].Events.Single(e => e.EventId == "facing").End = CharacterTestTacticalAuthoringTemplates.Absolute(83);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, fixture.Inputs, out var compiled, out string issue), issue);
                fixture.PlaceTarget(fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value);
                fixture.AdvanceWorldOnly(); fixture.RefreshFacts();
                var host = new CharacterTestBehaviorExecutionHost(compiled.Program, fixture.Runtime, fixture.Runtime.ActionRuntime,
                    null, fixture.Commands.Enqueue, CharacterTestDirectAiNavigationService.Instance, fixture.Runtime.PerceptionFrameCache);
                Assert.IsTrue(host.Start(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)), host.LastFailureReason);
                var visited = new List<object>();
                try
                {
                    for (int i = 0; i < 220 && host.State != CharacterTestRuntimeAiLabLogicRuntimeState.Completed; i++)
                    {
                        fixture.RefreshFacts(); host.Tick(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                        visited.Add(new { world = fixture.NextFrame, local = host.LocalFrame, state = host.State.ToString(), action = host.OwnedActionInstanceId });
                        fixture.AdvanceWorldOnly();
                    }
                    Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, host.State, host.LastFailureReason);
                    Assert.AreEqual(1, fixture.Samples.Where(s => s.ActionInstance > 0).Select(s => s.ActionInstance).Distinct().Count());
                    Assert.IsTrue(fixture.Samples.Any(s => s.HitCount > 0), "Must consume the real Combat path.");
                    Assert.AreEqual(0, host.OwnedActionInstanceId);
                    Assert.IsFalse(fixture.Source.Input.HasActiveMove);
                    string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/DSL-2"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "real-host-tail83.json"), JsonConvert.SerializeObject(new
                    { compiled.DependencyIdentity, compiled.NormalBoundaries, program = compiled.Program.Execution.Identity,
                        observations = visited, samples = fixture.Samples, formalUiAccepted = false, formalPersistenceAccepted = false }, Formatting.Indented));
                }
                finally
                {
                    host.Stop(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                    for (int i = 0; i < 8; i++) { fixture.AdvanceWorldOnly(); host.Tick(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)); }
                }
            }
        }
    }
}
