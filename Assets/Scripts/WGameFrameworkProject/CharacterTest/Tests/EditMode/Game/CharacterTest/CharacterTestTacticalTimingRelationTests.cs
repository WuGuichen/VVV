using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalTimingRelationTests
    {
        private static CharacterTestTacticalAuthoringSource Source()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateBlank("时间关联测试");
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
            CharacterTestTacticalAuthoringEdits.Add(source, source.EntryTimelineId, "Movement");
            var ev = source.Timelines[0].Events[0]; ev.EventId = "move"; ev.Title = "后撤";
            var block = CharacterTestTacticalBlockEdits.Ensure(source, source.EntryTimelineId, ev.EventId, "block");
            block.Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 12, DurationFrames = 100 };
            ev.End = Builtin("SimulationEnd", -10);
            return source;
        }
        private static CharacterTestTacticalFrameReference Builtin(string anchor, int offset = 0)
            => new() { Kind = "Anchor", TargetKind = "Builtin", SourceEventId = "block", Anchor = anchor, OffsetFrames = offset };
        private static CharacterTestTacticalCardInputs Inputs() => CharacterTestTacticalCardInputs.BindSlots(1, new Dictionary<string, CharacterTestTacticalCardInputs>());
        [Test]
        public void BlockResizePreviewReflowsDependentsAndPreservesOtherInstanceLengths()
        {
            var source = Source();
            var timeline = source.Timelines[0];
            timeline.Events[0].End.OffsetFrames = 0;
            CharacterTestTacticalAuthoringEdits.Add(source, timeline.TimelineId, "Movement");
            var next = timeline.Events.Last();
            var block = CharacterTestTacticalBlockEdits.Ensure(source, timeline.TimelineId, next.EventId, "next");
            block.Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = 20 };
            block.Start = new CharacterTestTacticalFrameReference { Kind = "Anchor", TargetKind = "Block", SourceEventId = "block", Anchor = "End" };
            next.Start = CharacterTestTacticalBlockEdits.Local(0);
            next.End = new CharacterTestTacticalFrameReference { Kind = "Anchor", TargetKind = "Builtin", SourceEventId = "next", Anchor = "SimulationEnd" };
            string original = CharacterTestTacticalAuthoringJson.Write(source);
            var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, Inputs(), "");
            var resized = preview.WithBlockDuration("block", 46, new Dictionary<string, int> { ["block"] = 100, ["next"] = 35 });
            Assert.IsTrue(resized.Ready, resized.Diagnostic);
            Assert.IsTrue(resized.TryGetFrames("block", out int start, out int end));
            Assert.AreEqual(0, start); Assert.AreEqual(46, end);
            Assert.IsTrue(resized.TryGetFrames("next", out start, out end));
            Assert.AreEqual(46, start); Assert.AreEqual(81, end);
            Assert.AreEqual(original, CharacterTestTacticalAuthoringJson.Write(source));
            Assert.Throws<System.ArgumentException>(() => preview.WithBlockDuration("block", 11, null));
        }

        [Test]
        public void BlockDurationCommitOnlyChangesSelectedReferenceAndKeepsEditing()
        {
            var owner = new UnityEngine.GameObject("Block duration transaction test"); owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                var workspace = controller.DraftWorkspace;
                var source = Source();
                var document = new CharacterTestTacticalDslSource { CardId = "resize", Name = "resize",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                        new CharacterTestTacticalDslNode { NodeId = "first", Kind = "Reference", DefinitionId = "shared" },
                        new CharacterTestTacticalDslNode { NodeId = "second", Kind = "Reference", DefinitionId = "shared" } } },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } } };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string cardId, out string issue), issue);
                var session = new CharacterTestTacticalCompositionEditingSession(controller);
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_compositionEditingSession",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(controller, session);
                session.Bind(workspace, workspace.Cards[cardId]); Assert.IsTrue(session.ConfirmShared(session.Token));
                var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, Inputs(), "");
                var editor = new CharacterTestTacticalEditingSession(controller, cardId);
                editor.Bind(workspace, workspace.Cards[cardId], preview, session.Token); preview.Editor = editor;
                int revision = workspace.CurrentRevision;
                Assert.IsTrue(editor.SetBlockDuration(editor.Token, "block", 46));
                Assert.AreEqual(revision + 1, workspace.CurrentRevision);
                Assert.IsTrue(session.SharedConfirmed); Assert.IsFalse(session.EventFocusActive);
                var saved = workspace.ResolveComposition(workspace.Cards[cardId]);
                Assert.AreEqual(46, saved.Root.Children[0].BlockDurations["block"]);
                Assert.IsNull(saved.Root.Children[1].BlockDurations);
                Assert.AreEqual(100, saved.Definitions[0].Source.Timelines[0].EventBlocks[0].Timing.DurationFrames);
                Assert.IsTrue(workspace.TryUndo(out issue), issue);
                Assert.IsNull(workspace.ResolveComposition(workspace.Cards[cardId]).Root.Children[0].BlockDurations);
                Assert.IsTrue(workspace.TryRedo(out issue), issue);
                Assert.AreEqual(46, workspace.ResolveComposition(workspace.Cards[cardId]).Root.Children[0].BlockDurations["block"]);
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
        private static CharacterTestTacticalTimingEvaluation Evaluate(CharacterTestTacticalAuthoringSource source, int length)
        {
            var result = new CharacterTestTacticalTimingEvaluation(source, Inputs(), true, new Dictionary<string, int> { ["block"] = length });
            result.Evaluate(); return result;
        }
        [Test] public void PreviewReflowsReferencesWithoutChangingSourceAndRoundTripsV4()
        {
            var source = Source(); var original = CharacterTestTacticalAuthoringJson.Write(source);
            var first = Evaluate(source, 100); var second = Evaluate(source, 140);
            Assert.AreEqual(90, first.Position("move", "End")); Assert.AreEqual(130, second.Position("move", "End"));
            Assert.AreEqual(original, CharacterTestTacticalAuthoringJson.Write(source));
            Assert.AreEqual(original, CharacterTestTacticalAuthoringJson.Write(source.Clone()));
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, Inputs(), out var compiled, out var issues), string.Join(";", issues));
            Assert.AreEqual(100, compiled.Program.Execution.Timelines[0].DurationFrames);
        }
        [Test] public void RatioUsesLocalZeroAndRoundHalfAwayFromZero()
        {
            var source = Source(); var block = source.Timelines[0].EventBlocks[0]; block.Start.Frame = 30;
            var end = source.Timelines[0].Events[0].End; end.Kind = "Ratio"; end.Ratio = .5; end.OffsetFrames = 0;
            Assert.AreEqual(61, Evaluate(source, 61).Position("move", "End"));
            Assert.AreEqual(100, Evaluate(source, 140).Position("move", "End"));
        }
        [Test] public void InvalidPreviewRetainsActualPositionsForRepair()
        {
            var source = Source(); source.Timelines[0].Events[0].Start.Frame = 90;
            var preview = Evaluate(source, 80);
            Assert.IsTrue(preview.TryFrames("move", out int start, out int end)); Assert.AreEqual(90, start); Assert.AreEqual(70, end);
            Assert.IsNotEmpty(preview.Issues);
        }
        [Test] public void MissingAndCyclicReferencesProduceDiagnostics()
        {
            var source = Source(); source.Timelines[0].Events[0].Start = CharacterTestTacticalAuthoringTemplates.Anchor("move", "End");
            source.Timelines[0].Events[0].End = CharacterTestTacticalAuthoringTemplates.Anchor("move", "Start", 1);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Any(i => i.Code == "AnchorCycle"));
            source.Timelines[0].Events[0].Start.SourceEventId = "deleted";
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Any(i => i.Code == "MissingEvent"));
        }
        [Test] public void DraggingRatioAndMovingRelatedSelectionPreserveTargets()
        {
            var source = Source(); var ev = source.Timelines[0].Events[0];
            ev.End.Kind = "Ratio"; ev.End.Ratio = .5; ev.End.OffsetFrames = 0;
            var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, Inputs(), ""); preview.SetEventFocus("block"); preview.EnableTimingSimulation(60);
            CharacterTestTacticalEditingSession.SetResolvedPoint(ev.End, 36, preview, 0);
            Assert.AreEqual(.6, ev.End.Ratio); Assert.AreEqual("block", ev.End.SourceEventId);
            Assert.AreEqual(60, Evaluate(source, 100).Position("move", "End"));
        }
        [Test] public void ActionSlotsStaySixtyInPreviewAndExpandOnceAfterBinding()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301);
            var source = Source(); source.Timelines[0].Events.Clear(); source.Timelines[0].EventBlocks[0].EventIds.Clear();
            source.Slots.Clear(); source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "primary", Resolver = "binding" });
            var timeline = source.Timelines[0];
            var actionId = CharacterTestTacticalBlockEdits.Add(source, timeline.TimelineId, "block", "CharacterAction", 40, actionSlot: "primary");
            timeline.Events[0].Start = Builtin("SimulationEnd", -60);
            var inputs = CharacterTestTacticalCardInputs.BindSlots(fixture.Source.Identity.CharacterId, new Dictionary<string, CharacterTestTacticalCardInputs> { ["primary"] = fixture.Inputs });
            var preview = new CharacterTestTacticalTimingEvaluation(source, inputs, true); preview.Evaluate();
            Assert.AreEqual(40, preview.Position(actionId, "Start")); Assert.AreEqual(100, preview.Position(actionId, "End"));
            var installed = new CharacterTestTacticalTimingEvaluation(source, inputs); installed.Evaluate();
            Assert.IsEmpty(installed.Issues); Assert.AreEqual(40, installed.Position(actionId, "Start"));
            Assert.AreEqual(40 + fixture.Inputs.Action.DurationFrames.Value, installed.Position("block", "End"));
            var second = CharacterTestTacticalBlockEdits.Add(source, timeline.TimelineId, "block", "CharacterAction", 40, actionSlot: "primary");
            installed = new CharacterTestTacticalTimingEvaluation(source, inputs); installed.Evaluate();
            Assert.AreEqual(40 + fixture.Inputs.Action.DurationFrames.Value, installed.Position("block", "End"), "Overlap must not count twice.");
        }
        [Test] public void RebuiltCardsCompileWithCurrentActionAndUseOnlyLowLevelEvents()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301);
            var inputs = CharacterTestTacticalCardInputs.BindSlots(fixture.Source.Identity.CharacterId,
                new Dictionary<string, CharacterTestTacticalCardInputs> { ["primary"] = fixture.Inputs });
            foreach (string name in new[] { "撤步还击", "绕侧进攻", "攻后脱离", "守距观望", "脱离目标", "贴身逼近", "残血撤离", "攻击战术卡", "常规移动接敌出招" })
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateVisualCard(name);
                Assert.IsTrue(source.HasTimingVersion);
                Assert.IsFalse(source.Timelines.SelectMany(t => t.Events).Any(e => e.Operation.Kind == "ApproachOptimalRange"));
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out _, out var issues), name + ": " + string.Join(";", issues));
            }
        }
        [Test] public void NativeLoopInstantReturnAndRepetitionSurviveCompositionImport()
        {
            var source = Source(); var timeline = source.Timelines[0];
            timeline.Events[0].MaxExecutionsPerPass = 3; timeline.Events[0].CooldownFrames = 2;
            string loop = CharacterTestTacticalBlockEdits.Add(source, timeline.TimelineId, "block", "LoopRegion", 0);
            timeline.Events.First(e => e.EventId == loop).End = CharacterTestTacticalBlockEdits.Local(10);
            string exit = CharacterTestTacticalBlockEdits.Add(source, timeline.TimelineId, "block", "ReturnToPlanner:Instant", 50);
            var root = CharacterTestTacticalCompositionEdits.NewNode("Sequence"); root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared"));
            var document = new CharacterTestTacticalDslSource { CardId = "native", Root = root,
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = source } } };
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, Inputs(), out var compiled, out string error), error);
            var events = compiled.Program.Execution.Events.ToArray();
            Assert.IsTrue(events.Any(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner && e.Timing == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant));
            Assert.IsTrue(events.Any(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget && e.MaxExecutionsPerPass == 3 && e.CooldownFrames == 2));
            Assert.IsTrue(events.Any(e => e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion));
            Assert.AreEqual("Instant", source.Clone().Timelines[0].Events.First(e => e.EventId == exit).TimingKind);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void FocusedProjectionKeepsEventsWhenAnEndpointExceedsSimulationEnd(int offset)
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301);
            var inputs = CharacterTestTacticalCardInputs.BindSlots(fixture.Source.Identity.CharacterId,
                new Dictionary<string, CharacterTestTacticalCardInputs> { ["primary"] = fixture.Inputs });
            var source = CharacterTestTacticalAuthoringTemplates.CreateVisualAttack();
            source.Timelines[0].Events.Single(e => e.EventId == "facing.action").End.OffsetFrames = offset;
            string before = CharacterTestTacticalAuthoringJson.Write(source);
            var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, inputs, "");
            preview.SetEventFocus("attack-block"); preview.EnableTimingSimulation(61);
            Assert.AreEqual(offset == 0, preview.Ready);
            if (offset != 0) Assert.IsFalse(preview.AsCompileResult().Success);
            var model = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel {
                Available = false, TacticalPreview = preview, RecipePayload = preview.DisplayPayload,
                CharacterActionOptions = new[] { new BehaviorRecipeCharacterActionOptionViewModel {
                    ActionId = fixture.Inputs.Action.ActionId, DurationFrames = 999, Valid = true } } };
            var projection = new BehaviorRecipeTimelineProjection(); projection.Rebuild(model);
            Assert.AreEqual(1, projection.TimelineCount, "A compile diagnostic must not erase the authoring timeline.");
            var timeline = projection.GetTimeline(0);
            var events = Enumerable.Range(0, timeline.TrackCount).Select(i => timeline.GetTrack(i).Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(61 + offset, events.Single(e => e.EventId == "facing.action").DurationFrames);
            Assert.AreEqual(offset != 0, events.Single(e => e.EventId == "facing.action").Invalid);
            Assert.IsFalse(events.Where(e => e.EventId != "facing.action").Any(e => e.Invalid));
            Assert.AreEqual(60, events.Single(e => e.EventId == "attack").DurationFrames, "Action option metadata cannot override the authoring placeholder.");
            Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.Write(source));
            model.TacticalPreview = null; projection.Rebuild(model);
            Assert.AreEqual(0, projection.TimelineCount, "Unavailable execution without an authoring projection stays unavailable.");
        }
    }
}
