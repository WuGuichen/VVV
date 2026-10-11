using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCompositionPersistenceTests
    {
        private static CharacterTestTacticalEditToken Token(CharacterTestRuntimeAiLabDraftWorkspace w, string id) => new CharacterTestTacticalEditToken(w, w.Cards[id]);
        private static string Root(string name)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/M2a", name + "-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.GetDirectoryName(root)); return root;
        }
        private static string Import(CharacterTestRuntimeAiLabDraftWorkspace w)
        {
            Assert.IsTrue(w.TryImportComposition(1, 0, w.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
            return id;
        }
        private static long Compile(CharacterTestRuntimeAiLabDraftWorkspace w, string id, CharacterTestTacticalCardRuntimeFixture fixture)
        {
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(w.ResolveComposition(w.Cards[id]), fixture.Inputs, out var compiled, out string issue), issue);
            return compiled.Program.SemanticHash;
        }

        [Test]
        public void RealFlushReloadKeepsOneSharedOwnerAndMixedPayloadsAndExecution()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                string root = Root("roundtrip"); var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var w = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
                var ordinary = w.Cards.ToDictionary(p => p.Key, p => CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(p.Value.LogicPayload));
                Assert.IsTrue(w.TryImportTacticalSource(1, 0, w.CurrentRevision, CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(), out string legacy, out _));
                string a = Import(w), b = Import(w);
                Assert.AreEqual(3, w.CloneTacticalDefinitions().Length);
                Assert.IsTrue(w.TryEditComposition(Token(w, a), s => s.Root.Children[0].EndCondition = null, "甲卡外层条件", out string issue), issue);
                Assert.IsNotNull(w.Cards[b].CloneCompositionSource().Root.Children[0].EndCondition);
                var definition = w.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack");
                definition.Source.Timelines[0].Events.Single(e => e.EventId == "approach").End.OffsetFrames = 4;
                var token = w.CaptureTacticalDefinitionEdit(definition.DefinitionId);
                int before = w.CurrentRevision;
                Assert.IsFalse(w.TryReplaceTacticalDefinition(token, definition.Source, false, out _)); Assert.AreEqual(before, w.CurrentRevision);
                Assert.IsTrue(w.TryReplaceTacticalDefinition(token, definition.Source, true, out issue), issue);
                Assert.AreEqual(before + 1, w.CurrentRevision);
                long aHash = Compile(w, a, fixture), bHash = Compile(w, b, fixture);
                string aJson = w.Cards[a].CompositionSourceJson, bJson = w.Cards[b].CompositionSourceJson;
                string legacyJson = w.Cards[legacy].TacticalSourceJson, workspaceId = w.WorkspaceId;
                writer.MarkDirty(); Assert.IsTrue(writer.Flush(), writer.LastStatus);
                string directory = Path.Combine(root, workspaceId);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(directory, "workspace.json")));
                Assert.AreEqual("mx.character-test.runtime-ai-lab.workspace.v4", (string)manifest["schema"]);
                Assert.AreEqual(3, ((JArray)manifest["tacticalDefinitions"]).Count);
                var savedCard = JObject.Parse(File.ReadAllText(Path.Combine(directory, "cards", a + ".json")));
                Assert.IsNull(savedCard["payload"]["definitions"], "Card payload must not own a private shared table.");
                w = null; writer = null;
                var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                Assert.IsTrue(reader.TryLoad(workspaceId, out var loaded, out issue), issue);
                Assert.AreEqual(aJson, loaded.Cards[a].CompositionSourceJson); Assert.AreEqual(bJson, loaded.Cards[b].CompositionSourceJson);
                Assert.AreEqual(legacyJson, loaded.Cards[legacy].TacticalSourceJson);
                Assert.AreEqual(aHash, Compile(loaded, a, fixture)); Assert.AreEqual(bHash, Compile(loaded, b, fixture));
                Assert.AreEqual(3, loaded.CloneTacticalDefinitions().Length);
                Assert.AreEqual(2, loaded.TacticalDefinitionImpact("shared.attack").Length);
                foreach (var pair in ordinary) Assert.AreEqual(pair.Value, CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(loaded.Cards[pair.Key].LogicPayload));
                File.WriteAllText(Path.Combine(root, "roundtrip-receipt.json"), new JObject { ["workspace"] = workspaceId,
                    ["a"] = a, ["b"] = b, ["legacy"] = legacy, ["aHash"] = aHash, ["bHash"] = bHash, ["definitions"] = 3 }.ToString());
            }
        }

        [Test]
        public void ExistingUndoCloneAndTwoCopiesPreserveSharedIdentity()
        {
            var w = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            string a = Import(w);
            Assert.IsTrue(w.TryDuplicateCompositionCard(Token(w, a), 1, 0, out string b, out string issue), issue);
            Assert.AreEqual(3, w.CloneTacticalDefinitions().Length);
            Assert.AreEqual(w.Cards[a].CompositionSourceJson, w.Cards[b].CompositionSourceJson);
            string original = w.Cards[a].CompositionSourceJson;
            w.Cards[a].CopyDefinitionFrom(w.Cards[a]); Assert.AreEqual(original, w.Cards[a].CompositionSourceJson);
            Assert.AreEqual(original, w.Cards[a].Clone(false).CompositionSourceJson);
            Assert.Throws<InvalidOperationException>(() => w.Cards[a].LogicPayload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload());
            Assert.IsTrue(w.TryDuplicateTacticalDefinition(Token(w, a), "attack", "shared.independent", out issue), issue);
            Assert.AreEqual(4, w.CloneTacticalDefinitions().Length);
            Assert.AreEqual("shared.independent", w.Cards[a].CloneCompositionSource().Root.Children[0].Children[0].DefinitionId);
            Assert.AreEqual("shared.attack", w.Cards[b].CloneCompositionSource().Root.Children[0].Children[0].DefinitionId);
            Assert.IsTrue(w.TryUndo(out issue), issue);
            Assert.AreEqual(original, w.Cards[a].CompositionSourceJson); Assert.AreEqual(3, w.CloneTacticalDefinitions().Length);
            Assert.IsTrue(w.TryRedo(out issue), issue); Assert.AreEqual(4, w.CloneTacticalDefinitions().Length);
            var edit = w.CaptureTacticalDefinitionEdit("shared.independent");
            var source = w.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.independent").Source;
            source.Timelines[0].Events[0].Priority = 123;
            Assert.IsTrue(w.TryReplaceTacticalDefinition(edit, source, true, out issue), issue);
            Assert.IsFalse(w.TryReplaceTacticalDefinition(edit, source, true, out _));
            Assert.AreNotEqual(123, w.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack").Source.Timelines[0].Events[0].Priority);
        }

        [Test]
        public void ReadableMissingReferenceSavesForRepairAndNeverCompiles()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                string root = Root("repair"); var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var w = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault()); string id = Import(w);
                Assert.IsTrue(w.TryEditComposition(Token(w, id), s => s.Root.Children[0].Children[0].DefinitionId = "missing",
                    "保留断引用", out string issue), issue);
                Assert.IsTrue(writer.Flush(), writer.LastStatus);
                Assert.IsTrue(new CharacterTestRuntimeAiLabDraftPersistence(root).TryLoad(w.WorkspaceId, out var loaded, out issue), issue);
                Assert.AreEqual(w.Cards[id].CompositionSourceJson, loaded.Cards[id].CompositionSourceJson);
                Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(loaded.ResolveComposition(loaded.Cards[id]), fixture.Inputs, out var partial, out issue));
                Assert.IsNull(partial); StringAssert.Contains("DSL_MISSING_DEFINITION", issue);
                Assert.IsTrue(loaded.TryEditComposition(Token(loaded, id), s => s.Root.Children[0].Children[0].DefinitionId = "shared.attack", "修复", out issue), issue);
                Compile(loaded, id, fixture);
            }
        }

        [Test]
        public void ActualFilesystemFailureBlocksExistingTestTransactionBeforeValidation()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                Assert.IsTrue(fixture.Start()); fixture.Step(); int frame = fixture.NextFrame, local = fixture.Card.LocalFrame;
                string root = Root("failure"); File.WriteAllText(root, "directory blocked");
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var w = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault()); string id = Import(w);
                string source = w.Cards[id].CompositionSourceJson;
                var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
                var request = new CharacterTestRuntimeAiLabTestCurrentChangesRequest("composition-save-fail", w.WorkspaceId, w.CurrentRevision,
                    1, 0, "group", "entry", id, "", null);
                Assert.IsTrue(tracker.TryBegin(request, out string issue), issue); int calls = 0;
                bool Save(out string diagnostic) { bool ok = writer.Flush(); diagnostic = writer.LastStatus; return ok; }
                CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action restarting) { calls++; restarting(); fixture.Step(); return default; }
                var result = CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(tracker, Save, Apply);
                Assert.IsFalse(result.Success); Assert.AreEqual(CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Save, result.FailureStage);
                Assert.AreEqual(0, calls); Assert.IsTrue(w.Dirty); Assert.AreEqual(source, w.Cards[id].CompositionSourceJson);
                Assert.AreEqual(3, w.CloneTacticalDefinitions().Length);
                Assert.AreEqual(frame, fixture.NextFrame); Assert.AreEqual(local, fixture.Card.LocalFrame);
            }
        }

        [Test]
        public void PreviousRecoveryIncludesSharedFilesAndTheirConfirmedVersion()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                string root = Root("previous"); var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var w = writer.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault()); string id = Import(w);
                long before = Compile(w, id, fixture); Assert.IsTrue(writer.Flush(), writer.LastStatus);
                var shared = w.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack");
                shared.Source.Timelines[0].Events.Single(e => e.EventId == "approach").End.OffsetFrames = 4;
                Assert.IsTrue(w.TryReplaceTacticalDefinition(w.CaptureTacticalDefinitionEdit(shared.DefinitionId), shared.Source, true, out string issue), issue);
                Assert.IsTrue(writer.Flush(), writer.LastStatus);
                File.WriteAllText(Path.Combine(root, w.WorkspaceId, "workspace.json"), "{}");
                Assert.IsTrue(new CharacterTestRuntimeAiLabDraftPersistence(root).TryLoad(w.WorkspaceId, out var recovered, out issue), issue);
                Assert.IsTrue(recovered.RecoveredFromPrevious);
                Assert.AreEqual(1, recovered.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack").Revision);
                Assert.AreEqual(before, Compile(recovered, id, fixture));
            }
        }
    }
}
