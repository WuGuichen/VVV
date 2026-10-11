using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalEditingTests
    {
        private static CharacterTestRuntimeAiLabDraftWorkspace Create(out string id)
        {
            var w = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(w.TryImportTacticalSource(1, 0, w.CurrentRevision, CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(), out id, out string issue), issue);
            return w;
        }
        private static CharacterTestTacticalEditToken Token(CharacterTestRuntimeAiLabDraftWorkspace w, string id) => new CharacterTestTacticalEditToken(w, w.Cards[id]);

        [Test]
        public void InspectorModeInvalidatesPresentationWithoutChangingAuthoringData()
        {
            var model = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel { ShowBehaviorLogic = true, TacticalPresentationRevision = 1 };
            long before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost.ComputeBehaviorCardPresentationRevision(model);
            model.TacticalPresentationRevision++;
            Assert.AreNotEqual(before, CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost.ComputeBehaviorCardPresentationRevision(model));
        }

        [Test]
        public void MultiMoveFollowsSelectedAnchorOnlyOnceAndUndoRestoresExactSource()
        {
            var w = Create(out string id); string before = w.Cards[id].TacticalSourceJson;
            Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "移动", s => CharacterTestTacticalAuthoringEdits.Move(s, s.EntryTimelineId, new[] { "attack", "approach" }, 4), out string issue), issue);
            var source = w.Cards[id].CloneTacticalSource();
            var approach = source.Timelines[0].Events.Single(e => e.EventId == "approach");
            Assert.AreEqual(4, approach.Start.Frame); Assert.AreEqual(0, approach.End.OffsetFrames);
            Assert.AreEqual(5, source.Timelines[0].Events.Single(e => e.EventId == "attack").Start.Frame);
            string after = w.Cards[id].TacticalSourceJson;
            Assert.IsTrue(w.TryUndo(out issue), issue); Assert.AreEqual(before, w.Cards[id].TacticalSourceJson);
            Assert.IsTrue(w.TryRedo(out issue), issue); Assert.AreEqual(after, w.Cards[id].TacticalSourceJson);
        }

        [Test]
        public void PreviewAndCancellationNeverMutateOwnerAndOnlyFinalCommitAddsHistory()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var w = Create(out string id); string before = w.Cards[id].TacticalSourceJson; int revision = w.CurrentRevision;
                var preview = CharacterTestTacticalAuthoringPreview.Create(w.Cards[id], fixture.Inputs, null);
                for (int delta = 1; delta < 5; delta++)
                {
                    var ghost = preview.WithEdits(s => CharacterTestTacticalAuthoringEdits.Move(s, s.EntryTimelineId, new[] { "attack", "approach" }, delta));
                    Assert.IsTrue(ghost.Ready, ghost.Diagnostic);
                }
                Assert.AreEqual(revision, w.CurrentRevision); Assert.AreEqual(before, w.Cards[id].TacticalSourceJson);
                Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "松开提交", s => CharacterTestTacticalAuthoringEdits.Move(s, s.EntryTimelineId, new[] { "attack", "approach" }, 4), out _));
                Assert.AreEqual(revision + 1, w.CurrentRevision);
                Assert.IsTrue(w.TryUndo(out _)); Assert.AreEqual(before, w.Cards[id].TacticalSourceJson);
            }
        }

        [Test]
        public void InvalidMixedMoveAndStaleTokensNeverPartiallyCommit()
        {
            var w = Create(out string id); string before = w.Cards[id].TacticalSourceJson; int revision = w.CurrentRevision;
            var old = Token(w, id);
            Assert.IsFalse(w.TryEditTacticalSource(old, "移动", s => CharacterTestTacticalAuthoringEdits.Move(s, s.EntryTimelineId, new[] { "attack", "release" }, -1), out _));
            Assert.AreEqual(before, w.Cards[id].TacticalSourceJson); Assert.AreEqual(revision, w.CurrentRevision);
            Assert.IsTrue(w.TryEditTacticalSource(old, "优先级", s => s.Timelines[0].Events[0].Priority = 9, out _));
            Assert.IsFalse(w.TryEditTacticalSource(old, "过期", s => s.Timelines[0].Events.Clear(), out _));
            Assert.IsFalse(w.TryEditTacticalSource(Token(w, id), "动作长度", s => CharacterTestTacticalAuthoringEdits.SetReference(s, s.EntryTimelineId, "attack", true,
                CharacterTestTacticalAuthoringTemplates.Absolute(4)), out _));
            var other = Create(out string otherId);
            Assert.IsFalse(other.TryEditTacticalSource(Token(w, id), "跨工作副本", s => s.Timelines.Clear(), out _));
            Assert.IsTrue(other.Cards[otherId].CloneTacticalSource().Timelines.Count > 0);
        }

        [Test]
        public void DeleteReportsDependenciesAndKeepsBrokenReferencesUntilUndo()
        {
            var w = Create(out string id); var source = w.Cards[id].CloneTacticalSource(); string before = w.Cards[id].TacticalSourceJson;
            var impact = CharacterTestTacticalAuthoringEdits.DeleteImpact(source, new[] { "attack" });
            Assert.IsTrue(impact.Any(x => x.Contains("approach"))); Assert.IsTrue(impact.Any(x => x.Contains("gate.range")));
            Assert.IsFalse(w.TryEditTacticalSource(Token(w, id), "删除", s => CharacterTestTacticalAuthoringEdits.Delete(s, s.EntryTimelineId, new[] { "attack" }, false), out _));
            Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "删除", s => CharacterTestTacticalAuthoringEdits.Delete(s, s.EntryTimelineId, new[] { "attack" }, true), out _));
            source = w.Cards[id].CloneTacticalSource();
            Assert.AreEqual("attack", source.Timelines[0].Events.Single(e => e.EventId == "approach").End.SourceEventId);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.Validate(source).Count > 0);
            Assert.IsTrue(w.TryUndo(out _)); Assert.AreEqual(before, w.Cards[id].TacticalSourceJson);
        }

        [Test]
        public void ConditionEditorTargetsActivationAndEventSeparatelyAndPreservesOtherBindings()
        {
            var w = Create(out string id); var original = w.Cards[id].CloneTacticalSource();
            string attackRoot = original.Timelines[0].Events.Single(e => e.EventId == "attack").ConditionRoot.NodeId;
            Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "事件条件", s => CharacterTestTacticalConditionEditing.Apply(s, s.EntryTimelineId, "attack",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveConditionNode, -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: attackRoot)), out string issue), issue);
            var source = w.Cards[id].CloneTacticalSource();
            Assert.IsNull(source.Timelines[0].Events.Single(e => e.EventId == "attack").ConditionRoot);
            Assert.AreEqual(original.ActivationConditionRoot.NodeId, source.ActivationConditionRoot.NodeId);
            Assert.IsTrue(source.PredicateBindings.Any(b => b.NodeId == "gate.range"));
            Assert.IsFalse(source.PredicateBindings.Any(b => b.NodeId == "attack.opportunity"));
            Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "启用条件", s => CharacterTestTacticalConditionEditing.Apply(s, s.EntryTimelineId, "$activation",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.RemoveConditionNode, -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: s.ActivationConditionRoot.NodeId)), out issue), issue);
            Assert.IsNull(w.Cards[id].CloneTacticalSource().ActivationConditionRoot);
        }

        [Test]
        public void AddedActionDataConditionIsScopedAndNamesActualSourceEvent()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var w = Create(out string id);
                Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "动作数据", s => CharacterTestTacticalConditionEditing.AddBinding(s,
                    s.EntryTimelineId, "attack", "attack", "RootDistanceRange", null), out string issue), issue);
                var source = w.Cards[id].CloneTacticalSource();
                Assert.IsTrue(source.PredicateBindings.Any(b => b.Kind == "RootDistanceRange" && b.SourceActionEventId == "attack" && b.NodeId.StartsWith("bound-")));
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, fixture.Inputs, out _, out var diagnostics), string.Join("\n", diagnostics));
            }
        }

        [Test]
        public void SharedReferencesTrackOneDefinitionAndIndependentVariantRetainsInternalIds()
        {
            var w = Create(out string id);
            Assert.IsTrue(w.TryAddExistingCardEntry(1, 0, id, out _, out string issue), issue);
            Assert.IsTrue(w.TryDuplicateTacticalSource(Token(w, id), 1, 0, out string copyId, out issue), issue);
            Assert.AreNotEqual(id, copyId);
            Assert.AreEqual(w.Cards[id].TacticalSourceJson, w.Cards[copyId].TacticalSourceJson);
            string original = w.Cards[id].TacticalSourceJson;
            Assert.IsTrue(w.TryEditTacticalSource(Token(w, copyId), "变体", s => s.Timelines[0].Events[0].End.OffsetFrames = 4, out issue), issue);
            Assert.AreEqual(original, w.Cards[id].TacticalSourceJson);
            Assert.IsTrue(w.TryGetGroupForSlot(1, 0, out var group));
            Assert.AreEqual(2, group.Entries.Count(e => e.CardDraftId == id));
            Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "共享定义", s => s.Timelines[0].Events[0].Priority = 8, out issue), issue);
            foreach (var entry in group.Entries.Where(e => e.CardDraftId == id)) Assert.AreEqual(8, w.Cards[entry.CardDraftId].CloneTacticalSource().Timelines[0].Events[0].Priority);
        }

        [Test]
        public void RealSaveReloadPreservesEditedFieldsIdentitiesAndCompiledSemantics()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/T4-4", Guid.NewGuid().ToString("N")));
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var w = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(w.TryImportTacticalSource(1, 0, w.CurrentRevision, CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(), out string id, out _));
                Assert.IsTrue(w.TryEditTacticalSource(Token(w, id), "修改关联和槽", s => {
                    s.Timelines[0].Events.Single(e => e.EventId == "approach").End.OffsetFrames = 4;
                    s.Timelines[0].Order = 7; s.Timelines[0].Title = "Edited timeline";
                    s.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "secondary", Resolver = "lightAttackOpener" });
                    s.Timelines[0].Events.Single(e => e.EventId == "attack").Operation.SlotId = "secondary";
                }, out string issue), issue);
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(w.Cards[id].CloneTacticalSource(), fixture.Inputs, out var compiled, out var diagnostics), string.Join("\n", diagnostics));
                string expected = w.Cards[id].TacticalSourceJson, workspaceId = w.WorkspaceId;
                writer.MarkDirty(); Assert.IsTrue(writer.Flush(), writer.LastStatus);
                w = null; writer = null;
                var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                Assert.IsTrue(reader.TryLoad(workspaceId, out var loaded, out issue), issue);
                Assert.AreEqual(expected, loaded.Cards[id].TacticalSourceJson);
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(loaded.Cards[id].CloneTacticalSource(), fixture.Inputs, out var recompiled, out diagnostics), string.Join("\n", diagnostics));
                Assert.AreEqual(compiled.Program.SemanticHash, recompiled.Program.SemanticHash);
                Assert.AreEqual(23, recompiled.Events.ToArray().Single(e => e.EventId == "approach").EndFrame);
                Assert.IsTrue(loaded.TryGetGroupForSlot(1, 0, out var group)); Assert.IsTrue(group.Entries.Any(e => e.CardDraftId == id));
            }
        }

        [Test]
        public void RealWriteFailureStopsProductionTransactionBeforeValidationAndKeepsRunningWorld()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                Assert.IsTrue(fixture.Start()); fixture.Step(); int frame = fixture.NextFrame, local = fixture.Card.LocalFrame;
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/T4-4", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(Path.GetDirectoryName(root)); File.WriteAllText(root, "blocked directory");
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var w = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(w.TryImportTacticalSource(1, 0, w.CurrentRevision, CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(), out string id, out _));
                string source = w.Cards[id].TacticalSourceJson;
                var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
                var request = new CharacterTestRuntimeAiLabTestCurrentChangesRequest("tactical-save-fail", w.WorkspaceId, w.CurrentRevision, 1, 0, "group", "entry", id, "", null);
                Assert.IsTrue(tracker.TryBegin(request, out string issue), issue);
                int calls = 0;
                bool Flush(out string message) { bool ok = writer.Flush(); message = writer.LastStatus; return ok; }
                CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action restarting) { calls++; restarting(); fixture.Step(); return default; }
                var result = CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(tracker, Flush, Apply);
                Assert.IsFalse(result.Success); Assert.AreEqual(CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Save, result.FailureStage);
                Assert.AreEqual(0, calls); Assert.IsTrue(w.Dirty); Assert.AreEqual(source, w.Cards[id].TacticalSourceJson);
                Assert.AreEqual(frame, fixture.NextFrame); Assert.AreEqual(local, fixture.Card.LocalFrame);
            }
        }
    }
}
