using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalSourceWorkspaceTests
    {
        [Test]
        public void ImportIsExclusiveAndUndoRestoresOriginalCardsWithoutIdentityChurn()
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            var ordinary = workspace.Cards.ToDictionary(p => p.Key,
                p => CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(p.Value.LogicPayload));
            Assert.IsTrue(workspace.TryImportTacticalSource(1, 0, workspace.CurrentRevision,
                CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(), out string id, out string issue), issue);
            var card = workspace.Cards[id];
            Assert.IsNull(card.LogicPayload);
            Assert.IsNull(card.Payload);
            string source = card.TacticalSourceJson;
            var clone = card.Clone(false);
            var editable = clone.CloneTacticalSource();
            editable.Timelines[0].Events.Clear();
            Assert.AreEqual(source, card.TacticalSourceJson);
            Assert.AreEqual(source, clone.TacticalSourceJson);
            Assert.Throws<InvalidOperationException>(() => card.LogicPayload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload());
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out var group));
            Assert.IsFalse(workspace.TryReplaceCardLogicPayload(1, 0, group.Entries.Count - 1,
                new CharacterTestRuntimeAiLabBehaviorCardLogicPayload(), out issue));
            Assert.IsTrue(workspace.TryUndo(out issue), issue);
            Assert.IsFalse(workspace.Cards.ContainsKey(id));
            foreach (var entry in ordinary)
                Assert.AreEqual(entry.Value, CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(workspace.Cards[entry.Key].LogicPayload));
            Assert.IsTrue(workspace.TryRedo(out issue), issue);
            Assert.AreEqual(source, workspace.Cards[id].TacticalSourceJson);
        }

        [Test]
        public void ExistingPersistenceRoundTripsBrokenReferencesAndExclusiveSchema()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/T4-3-source", Guid.NewGuid().ToString("N")));
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
            var workspace = persistence.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            source.Timelines[0].Events.RemoveAll(e => e.EventId == "attack");
            Assert.IsTrue(workspace.TryImportTacticalSource(1, 0, workspace.CurrentRevision, source, out string id, out string issue), issue);
            string expected = workspace.Cards[id].TacticalSourceJson;
            persistence.MarkDirty();
            Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
            Assert.IsTrue(persistence.TryLoad(workspace.WorkspaceId, out var loaded, out issue), issue);
            Assert.AreEqual(expected, loaded.Cards[id].TacticalSourceJson);
            Assert.IsNull(loaded.Cards[id].LogicPayload);
            Assert.IsNull(loaded.Cards[id].Payload);
            Assert.IsTrue(loaded.TryGetGroupForSlot(1, 0, out var group));
            Assert.IsTrue(group.Entries.Any(e => e.CardDraftId == id));
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.Validate(loaded.Cards[id].CloneTacticalSource()).Count > 0);
        }

        [Test]
        public void PreviewUsesActualCompilerAndNeverWritesResolvedFramesBack()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, "", "source", "", null);
                card.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreateSingleAttack());
                string before = card.TacticalSourceJson;
                var preview = CharacterTestTacticalAuthoringPreview.Create(card, fixture.Inputs, null);
                Assert.IsTrue(preview.Ready, preview.Diagnostic);
                Assert.IsTrue(preview.TryGetFrames("approach", out _, out int end));
                Assert.AreEqual(19, end);
                Assert.AreEqual(301, preview.Compilation.ActionId);
                preview.DisplayPayload.Timelines[0].Events.Clear();
                Assert.AreEqual(before, card.TacticalSourceJson);
                var missing = CharacterTestTacticalAuthoringPreview.Create(card, null, "ActionSummary:Missing");
                Assert.IsFalse(missing.Ready);
                Assert.AreEqual(card.CloneTacticalSource().Timelines[0].Events.Count, missing.DisplayPayload.Timelines[0].Events.Count);
                StringAssert.Contains("未", missing.DescribeEvent(card.CloneTacticalSource().EntryTimelineId, "attack"));
                Assert.AreEqual(before, card.TacticalSourceJson);
            }
        }

        [Test]
        public void StaleImportRevisionDoesNotModifyWorkspace()
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            int count = workspace.Cards.Count;
            Assert.IsFalse(workspace.TryImportTacticalSource(1, 0, workspace.CurrentRevision - 1,
                CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(), out _, out _));
            Assert.AreEqual(count, workspace.Cards.Count);
            Assert.IsFalse(workspace.CanUndo);
        }
    }
}
