using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalWholeCardTimelineTests
    {
        private CharacterTestTacticalCardRuntimeFixture _fixture;
        [OneTimeSetUp] public void Setup() => _fixture = new CharacterTestTacticalCardRuntimeFixture();
        [OneTimeTearDown] public void Cleanup() => _fixture?.Dispose();

        private static CharacterTestTacticalDslSource Source()
        {
            var shared = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(); shared.ActivationConditionRoot = null;
            return new CharacterTestTacticalDslSource { CardId = "whole", Name = "Whole card",
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new CharacterTestTacticalDslNode { NodeId = "first", Kind = "Reference", DefinitionId = "shared" },
                    new CharacterTestTacticalDslNode { NodeId = "second", Kind = "Reference", DefinitionId = "shared" } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = shared } } };
        }
        private CharacterTestTacticalDslCompilation Compile(CharacterTestTacticalDslSource source)
        {
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, _fixture.Inputs, out var compiled, out string issue), issue);
            return compiled;
        }

        [Test]
        public void EntireExecutionTableIsProjectedWithoutChangingAnyFrameOrOmittingGeneratedEvents()
        {
            var source = Source(); string before = CharacterTestTacticalDslJson.Write(source); var compiled = Compile(source);
            var whole = new CharacterTestTacticalWholeCardTimeline(compiled.Program, source, compiled.SourceLocations);
            var events = whole.DisplayPayload.Timelines.SelectMany(t => t.Events).ToDictionary(e => e.EventId);
            Assert.AreEqual(compiled.Program.Execution.Events.Length, events.Count);
            Assert.AreEqual(compiled.Program.Execution.Timelines.Length, whole.DisplayPayload.Timelines.Count);
            for (int i = 0; i < compiled.Program.Execution.Events.Length; i++)
            {
                var actual = compiled.Program.Execution.Events[i]; var shown = events[compiled.Program.ExecutionDebugMap.Event(i)];
                Assert.AreEqual(actual.StartFrame, shown.StartFrame); Assert.AreEqual(actual.DurationFrames, shown.DurationFrames);
                Assert.AreEqual(actual.Kind, shown.Operation.Kind);
            }
            Assert.IsFalse(events.ContainsKey("$entry.next")); Assert.IsFalse(events.ContainsKey("$done.exit"));
            Assert.AreEqual("first", whole.DisplayPayload.EntryTimelineId);
            Assert.AreEqual(before, CharacterTestTacticalDslJson.Write(source));
        }

        [Test]
        public void RepeatedDefinitionKeepsDistinctUseIdentityAndOwnLocalZero()
        {
            var source = Source(); var compiled = Compile(source);
            var whole = new CharacterTestTacticalWholeCardTimeline(compiled.Program, source, compiled.SourceLocations);
            var attacks = whole.DisplayPayload.Timelines.SelectMany(t => t.Events).Where(e => whole.Location(e.EventId)?.EventId == "attack").ToArray();
            Assert.AreEqual(2, attacks.Length); Assert.AreNotEqual(attacks[0].EventId, attacks[1].EventId);
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, attacks.Select(e => whole.Location(e.EventId).NodeId));
            Assert.AreEqual(attacks[0].StartFrame, attacks[1].StartFrame);
            Assert.AreNotEqual(whole.Name("first"), whole.Name("second"));
            StringAssert.Contains("局部帧", whole.Describe(attacks[0].EventId, attacks[0].TimelineId));
        }

        [TestCase("If")]
        [TestCase("Wait")]
        public void ConditionalAndLocalEndControlEventsKeepTheirCompiledDestinations(string kind)
        {
            var source = Source(); var condition = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack().ActivationConditionRoot;
            source.Root.EndCondition = condition;
            source.Root.Children[0] = new CharacterTestTacticalDslNode { NodeId = "guard", Kind = kind,
                Condition = condition, Children = new List<CharacterTestTacticalDslNode> { source.Root.Children[0] } };
            var compiled = Compile(source); var whole = new CharacterTestTacticalWholeCardTimeline(compiled.Program, source, compiled.SourceLocations);
            var events = whole.DisplayPayload.Timelines.SelectMany(t => t.Events).ToArray();
            Assert.IsTrue(events.Any(e => whole.Location(e.EventId)?.Field == "endCondition"));
            foreach (var shown in events.Where(e => e.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline))
                Assert.IsTrue(whole.DisplayPayload.Timelines.Any(t => t.TimelineId == shown.Operation.TargetTimelineId));
            if (kind == "Wait") Assert.IsTrue(events.Any(e => e.TimelineId == "guard" && e.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition));
        }

        [Test]
        public void FrozenProjectionRetainsExactOldProgramAfterSourceChanges()
        {
            var source = Source(); var compiled = Compile(source);
            var frozen = CharacterTestTacticalFrozenSource.CapturePublished("whole", 1, source, _fixture.Inputs, compiled);
            source.Root.Children.Clear();
            var whole = new CharacterTestTacticalWholeCardTimeline(frozen.CompiledProgram, frozen.CloneDocument(), frozen.Locations);
            Assert.AreSame(compiled.Program.Execution, whole.Program.Execution);
            Assert.IsTrue(whole.DisplayPayload.Timelines.Any(t => t.TimelineId == "second"));
        }

        [Test]
        public void FailedWholeCompileNeverDisplaysAnOldOrPartiallyCompiledTimeline()
        {
            var whole = new CharacterTestTacticalWholeCardTimeline(null, Source(), null, diagnostic: "missing slot");
            Assert.IsNull(whole.Program); Assert.IsEmpty(whole.DisplayPayload.Timelines); Assert.AreEqual("missing slot", whole.Diagnostic);
        }

        [Test]
        public void SelectingARepeatedUseNavigatesItsSourceWithoutFilteringOtherUsesOrWriting()
        {
            var go = new UnityEngine.GameObject("Whole card selection test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, Source(), out string id, out string issue), issue);
                var source = workspace.ResolveComposition(workspace.Cards[id]); var compiled = Compile(source);
                var session = new CharacterTestTacticalCompositionEditingSession(controller); session.Bind(workspace, workspace.Cards[id]);
                int revision = workspace.CurrentRevision;
                var whole = new CharacterTestTacticalWholeCardTimeline(compiled.Program, source, compiled.SourceLocations, session);
                string first = compiled.SourceLocations.First(p => p.Value.NodeId == "first" && p.Value.EventId == "attack" && p.Value.Field == "events").Key;
                string second = compiled.SourceLocations.First(p => p.Value.NodeId == "second" && p.Value.EventId == "attack" && p.Value.Field == "events").Key;
                whole.Select(second);
                Assert.AreEqual("second", session.SelectedNodeId); Assert.AreEqual("attack", session.PendingLocation.EventId);
                var refreshed = new CharacterTestTacticalWholeCardTimeline(compiled.Program, source, compiled.SourceLocations, session);
                Assert.IsTrue(refreshed.Highlighted(second)); Assert.IsFalse(refreshed.Highlighted(first));
                Assert.AreEqual(whole.DisplayPayload.Timelines.Count, refreshed.DisplayPayload.Timelines.Count);
                Assert.AreEqual(revision, workspace.CurrentRevision);
                string continuation = compiled.SourceLocations.First(p => p.Value.Field == "sequenceContinuation").Key;
                whole.Select(continuation);
                Assert.IsTrue(whole.TryGetSelectedControl(out _, out string detail)); StringAssert.Contains("second", compiled.Program.ExecutionDebugMap.Timeline(
                    compiled.Program.Execution.Flows[compiled.Program.Execution.Events.ToArray().Single(e => compiled.Program.ExecutionDebugMap.Event(e.Rank) == continuation).Parameter].TargetTimeline));
                StringAssert.Contains("后续", detail);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
