using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalEventBlockTests
    {
        private static CharacterTestTacticalCardInputs Inputs() => CharacterTestTacticalCardInputs.BindSlots(1,
            new Dictionary<string, CharacterTestTacticalCardInputs>());
        private static CharacterTestTacticalAuthoringSource Source(int start = 0)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose);
            source.Schema = CharacterTestTacticalAuthoringSource.CurrentSchema; source.Version = 2; source.Slots.Clear();
            var ev = source.Timelines[0].Events[0]; ev.EventId = "retreat"; ev.Title = "过近先后撤";
            ev.Start.Frame = start; ev.End.Frame = start + 12;
            source.Timelines[0].Events[1].End.SourceEventId = "retreat";
            return source;
        }
        private static CharacterTestTacticalAuthoringCompilation Compile(CharacterTestTacticalAuthoringSource source)
        {
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, Inputs(), out var result, out var issues), string.Join(";", issues));
            return result;
        }
        private static CharacterTestTacticalResolvedEvent Event(CharacterTestTacticalAuthoringCompilation result, string id)
            => result.Events.ToArray().Single(e => e.EventId == id);
        private static CharacterTestTacticalEventBlock Block(CharacterTestTacticalAuthoringSource source)
            => CharacterTestTacticalBlockEdits.Ensure(source, source.EntryTimelineId, "retreat", "block.retreat");
        private static CharacterTestTacticalDslSource Document(CharacterTestTacticalAuthoringSource source) => new CharacterTestTacticalDslSource {
            CardId = "block-test", Name = "事件段测试", Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence",
                Children = new List<CharacterTestTacticalDslNode> { new CharacterTestTacticalDslNode { NodeId = "first", Kind = "Reference", DefinitionId = "shared" } } },
            Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } } };

        [Test]
        public void ConversionPreservesLeafIdentityAndExecutionAtNonzeroOrigin()
        {
            var source = Source(20); string beforeJson = CharacterTestTacticalAuthoringJson.Write(source);
            StringAssert.DoesNotContain("eventBlocks", beforeJson); StringAssert.DoesNotContain("space", beforeJson);
            var before = Compile(source); var owned = source.Clone(); Block(owned); var after = Compile(owned);
            Assert.AreEqual(beforeJson, CharacterTestTacticalAuthoringJson.Write(source));
            Assert.AreEqual(3, owned.Version);
            Assert.AreEqual(0, owned.Timelines[0].Events[0].Start.Frame);
            Assert.AreEqual("Block", owned.Timelines[0].Events[0].Start.Space);
            Assert.AreEqual(12, owned.Timelines[0].Events[0].End.Frame);
            foreach (var ev in before.Events)
            {
                var changed = Event(after, ev.EventId);
                Assert.AreEqual(ev.StartFrame, changed.StartFrame); Assert.AreEqual(ev.EndFrame, changed.EndFrame);
            }
            Assert.AreEqual(before.Program.Execution.Events.Length, after.Program.Execution.Events.Length);
            Assert.IsTrue(after.TryBlockFrames("block.retreat", out int start, out int end));
            Assert.AreEqual(20, start); Assert.AreEqual(32, end);
            Assert.AreEqual("block.retreat", Event(after, "retreat").BlockId);
        }

        [Test]
        public void DynamicOriginKeepsAnOriginallyAbsoluteEndInTimelineCoordinates()
        {
            var source = Source(); var events = source.Timelines[0].Events;
            events[1].End = CharacterTestTacticalAuthoringTemplates.Absolute(2);
            events[0].Start = CharacterTestTacticalAuthoringTemplates.Anchor("face", "End", 3);
            events[0].End = CharacterTestTacticalAuthoringTemplates.Absolute(30);
            var before = Compile(source); Block(source); var after = Compile(source);
            Assert.AreEqual("Timeline", events[0].End.Space);
            Assert.AreEqual(Event(before, "retreat").StartFrame, Event(after, "retreat").StartFrame);
            Assert.AreEqual(30, Event(after, "retreat").EndFrame);
        }

        [Test]
        public void BlockEndFollowsAllMembersWhileAbsoluteSiblingsKeepTheirFrames()
        {
            var source = Source(); var block = Block(source); var timeline = source.Timelines[0];
            timeline.Events[1].Start = CharacterTestTacticalBlockEdits.BlockEnd(block.BlockId);
            timeline.Events[1].End = CharacterTestTacticalAuthoringTemplates.Anchor("face", "Start", 5);
            string added = CharacterTestTacticalBlockEdits.Add(source, timeline.TimelineId, block.BlockId, "Movement", 12);
            var child = timeline.Events.Single(e => e.EventId == added); child.End = CharacterTestTacticalBlockEdits.Local(46);
            CharacterTestTacticalAuthoringEdits.Add(source, timeline.TimelineId, "TurnToTarget");
            var independent = timeline.Events.Last(); independent.Start.Frame = 30; independent.End.Frame = 35;
            var result = Compile(source);
            Assert.AreEqual(46, Event(result, "face").StartFrame);
            Assert.AreEqual(30, Event(result, independent.EventId).StartFrame);
            var view = CharacterTestTacticalAuthoringPreview.FromSource(source, Inputs(), ""); view.SetEventFocus(block.BlockId);
            CollectionAssert.AreEquivalent(new[] { "retreat", added }, view.DisplayPayload.Timelines[0].Events.Select(e => e.EventId));
            Assert.AreEqual(0, view.DisplayOrigin);
            Assert.AreEqual(46, view.DisplayPayload.Timelines[0].Events.Single(e => e.EventId == added).StartFrame + view.DisplayPayload.Timelines[0].Events.Single(e => e.EventId == added).DurationFrames);
        }

        [Test]
        public void DraggingAnchoredMemberChangesOffsetAndPreservesDependency()
        {
            var source = Source(); var block = Block(source); var timeline = source.Timelines[0];
            string added = CharacterTestTacticalBlockEdits.Add(source, timeline.TimelineId, block.BlockId, "Movement", 0, "retreat");
            var child = timeline.Events.Single(e => e.EventId == added);
            CharacterTestTacticalAuthoringEdits.Move(source, timeline.TimelineId, new[] { added }, 7);
            Assert.AreEqual("retreat", child.Start.SourceEventId); Assert.AreEqual(7, child.Start.OffsetFrames);
            Assert.AreEqual(120, child.End.OffsetFrames);
            Assert.AreEqual(19, Event(Compile(source), added).StartFrame);
        }

        [TestCase("cycle", "AnchorCycle")]
        [TestCase("missing", "MissingBlock")]
        [TestCase("duplicate", "BlockMemberDuplicate")]
        [TestCase("before", "BeforeBlock")]
        public void InvalidBlockDependenciesAreDiagnosed(string edit, string code)
        {
            var source = Source(); var block = Block(source); var timeline = source.Timelines[0];
            if (edit == "cycle") block.Start = CharacterTestTacticalAuthoringTemplates.Anchor("retreat", "End");
            if (edit == "missing") timeline.Events[1].Start = CharacterTestTacticalBlockEdits.BlockEnd("absent");
            if (edit == "duplicate") timeline.EventBlocks.Add(new CharacterTestTacticalEventBlock { BlockId = "other", Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), EventIds = new List<string> { "retreat" } });
            if (edit == "before") { block.Start.Frame = 20; timeline.Events[0].Start = CharacterTestTacticalAuthoringTemplates.Absolute(0); }
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, Inputs(), out _, out var issues));
            Assert.IsTrue(issues.Any(i => i.Code == code), string.Join(";", issues));
        }

        [Test]
        public void ReplaceAndDeleteRetainIdentityAndExposeBrokenReferences()
        {
            var source = Source(); Block(source); string id = source.EntryTimelineId;
            CharacterTestTacticalBlockEdits.Replace(source, id, "retreat", "WaitForCondition", null, 12);
            var ev = source.Timelines[0].Events[0];
            Assert.AreEqual("retreat", ev.EventId); Assert.AreEqual("Instant", ev.TimingKind); Assert.IsNull(ev.End);
            Assert.IsNull(ev.Operation.Movement); Assert.AreEqual(1, Event(Compile(source), "retreat").EndFrame);
            CharacterTestTacticalAuthoringEdits.Delete(source, id, new[] { "retreat" }, true);
            Assert.IsEmpty(source.Timelines[0].EventBlocks[0].EventIds);
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(source, Inputs(), out _, out var issues));
            Assert.IsTrue(issues.Any(i => i.Code == "MissingEvent"));
            source.Timelines[0].Events.Clear();
            var empty = Compile(source); Assert.AreEqual(0, empty.Program.Execution.Events.Length);
            Assert.IsTrue(empty.TryBlockFrames("block.retreat", out int start, out int end)); Assert.AreEqual(start, end);
        }

        [Test]
        public void SaveReloadPublishAndFrozenCopiesPreserveBlockSourceAndRepeatedUseMapping()
        {
            var source = Source(); Block(source); var document = Document(source);
            document.Root.Children.Add(new CharacterTestTacticalDslNode { NodeId = "second", Kind = "Reference", DefinitionId = "shared" });
            string path = Path.Combine(Application.dataPath, "../Library/EventBlockTests", Guid.NewGuid().ToString("N"));
            var writer = new CharacterTestRuntimeAiLabDraftPersistence(path);
            var workspace = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string cardId, out string issue), issue);
            var card = workspace.Cards[cardId]; var owned = workspace.ResolveComposition(card);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(owned, Inputs(), out var compiled, out issue), issue);
            Assert.AreEqual(2, compiled.SourceLocations.Values.Count(l => l.EventId == "retreat" && l.BlockId == "block.retreat"));
            var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, card, owned, Inputs(), compiled);
            var published = CharacterTestTacticalPublishedSource.Capture(frozen);
            Assert.AreEqual("block.retreat", published.CloneDefinitions()[0].Source.Timelines[0].EventBlocks[0].BlockId);
            writer.MarkDirty(); Assert.IsTrue(writer.Flush(), writer.LastStatus);
            var reader = new CharacterTestRuntimeAiLabDraftPersistence(path);
            Assert.IsTrue(reader.TryLoad(workspace.WorkspaceId, out var loaded, out issue), issue);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(loaded.ResolveComposition(loaded.Cards[cardId]), Inputs(), out var restored, out issue), issue);
            Assert.AreEqual(compiled.Program.SemanticHash, restored.Program.SemanticHash);
            var changed = loaded.CloneTacticalDefinitions().Single().Source;
            changed.Timelines[0].Events[0].End.Frame = 46;
            Assert.IsTrue(loaded.TryReplaceTacticalDefinition(loaded.CaptureTacticalDefinitionEdit("shared"), changed, true, out issue), issue);
            Assert.AreEqual(12, frozen.CloneDocument().Definitions[0].Source.Timelines[0].Events[0].End.Frame);
        }
    }
}
