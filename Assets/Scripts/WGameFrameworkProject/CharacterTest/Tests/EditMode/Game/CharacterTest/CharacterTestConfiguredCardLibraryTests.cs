using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestConfiguredCardLibraryTests
    {
        private string _root;
        private CharacterTestConfiguredCardLibrary _library;
        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MxFramework/ConfiguredCardTests", Guid.NewGuid().ToString("N")));
            _library = new CharacterTestConfiguredCardLibrary(Path.Combine(_root, "project"), Path.Combine(_root, "recovery"));
        }
        [TearDown] public void TearDown()
        {
            string allowed = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MxFramework/ConfiguredCardTests")) + Path.DirectorySeparatorChar;
            if (_root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        private static CharacterTestConfiguredCard Card(string owner, int action = 301) => new CharacterTestConfiguredCard {
            Id = Guid.NewGuid().ToString("N"), Name = "同名测试卡", OwnerCharacter = owner, BaseKey = "base", BaseRevision = "基础 r1",
            Source = new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(CharacterTestTacticalAuthoringTemplates.CreateSingleAttack())) },
            Actions = new System.Collections.Generic.Dictionary<string, int> { ["attack"] = action }
        };

        [Test] public void RolesAndSharedLibraryKeepSameBaseVariantsDistinct()
        {
            var a = Card("char.a"); var b = Card("char.b", 455); var shared = Card("", 456);
            foreach (var card in new[] { a, b, shared }) Assert.IsTrue(_library.Save(card, 0, null, out string issue), issue);
            CollectionAssert.AreEquivalent(new[] { a.Id, shared.Id }, _library.List("char.a").Select(c => c.Id));
            CollectionAssert.AreEquivalent(new[] { b.Id, shared.Id }, _library.List("char.b").Select(c => c.Id));
            Assert.AreEqual(456, _library.List("char.a").Single(c => c.Id == shared.Id).Actions["attack"]);
        }

        [Test] public void RecoveryIsRoleLocalAndExplicitSaveIsRequired()
        {
            var card = Card(""); Assert.IsTrue(_library.Save(card, 0, null, out _));
            var draft = card.Clone(); draft.Name = "A 的未保存修改"; draft.Actions["attack"] = 455;
            _library.Recover("char.a", draft, card.Revision, card.FileHash);
            Assert.AreEqual(301, _library.List("char.b").Single().Actions["attack"]);
            Assert.IsNull(_library.ReadRecovery("char.b", card.Id));
            var recovered = _library.ReadRecovery("char.a", card.Id);
            Assert.AreEqual(455, recovered.Card.Actions["attack"]);
            Assert.IsTrue(_library.Save(recovered.Card, recovered.ExpectedRevision, recovered.ExpectedHash, out string issue), issue);
            Assert.AreEqual(455, _library.List("char.b").Single().Actions["attack"]);
        }

        [Test] public void ConflictsAndDeletesCannotSilentlyReplaceSavedCards()
        {
            var card = Card(""); Assert.IsTrue(_library.Save(card, 0, null, out _));
            var old = _library.List("char.a").Single(); string hash = old.FileHash;
            card.Name = "新库版本"; Assert.IsTrue(_library.Save(card, card.Revision, card.FileHash, out _));
            old.Name = "旧草稿";
            Assert.IsFalse(_library.Save(old, old.Revision, hash, out _));
            Assert.IsFalse(_library.Delete(old, out _));
            Assert.AreEqual("新库版本", _library.List("char.a").Single().Name);
            Assert.IsTrue(_library.Delete(card, out _));
            Assert.IsFalse(_library.Save(card, card.Revision, card.FileHash, out _));
        }

        [Test] public void OpeningAndCloningCannotMutateBaseOrSavedSource()
        {
            var original = Card("char.a"); string source = original.Source.ToString();
            Assert.IsTrue(_library.Save(original, 0, null, out _));
            var opened = _library.List("char.a").Single(); var copy = opened.Clone();
            copy.Actions["attack"] = 455; copy.Source["legacy"]["name"] = "本地改动";
            Assert.AreEqual(source, original.Source.ToString());
            Assert.AreEqual(301, _library.List("char.a").Single().Actions["attack"]);
            Assert.AreNotEqual(copy.Source.ToString(), _library.List("char.a").Single().Source.ToString());
        }

        [Test] public void UnsavedNewCardExistsOnlyInRoleRecovery()
        {
            var card = Card(""); card.Actions["attack"] = 0;
            _library.Recover("char.a", card, 0, null);
            Assert.IsEmpty(_library.List("char.a")); Assert.IsEmpty(_library.NewRecoveries("char.b"));
            Assert.AreEqual(card.Id, _library.NewRecoveries("char.a").Single().Id);
        }

        [Test] public void CreationFillsMissingActionsWithoutChangingSourceOrExistingChoices()
        {
            var card = Card("char.a"); card.Actions.Clear();
            string source = card.Source.ToString();
            var slots = CharacterTestTacticalPublishedSource.Read(card.Source).CloneLegacy().Slots.Where(s => s.Resolver != "stepBack").ToArray();
            Assert.IsNotEmpty(slots);
            CharacterTestConfiguredCardLibrary.FillMissingActions(card, spatial => 455);
            Assert.IsTrue(slots.All(s => card.Actions[s.SlotId] == 455));
            card.Actions[slots[0].SlotId] = 301;
            CharacterTestConfiguredCardLibrary.FillMissingActions(card, spatial => 456);
            Assert.AreEqual(301, card.Actions[slots[0].SlotId]);
            Assert.AreEqual(source, card.Source.ToString());
        }

        [Test] public void CreationWithoutDefaultCannotProduceAnEmptyConfiguredCard()
        {
            var card = Card("char.a"); card.Actions.Clear();
            Assert.Throws<InvalidOperationException>(() => CharacterTestConfiguredCardLibrary.FillMissingActions(card, spatial => 0));
            Assert.IsEmpty(_library.List("char.a"));
        }

        [Test] public void MaterializationIsolatesDefinitionsAndKeepsRepeatedReferencesSharedWithinCard()
        {
            var root = CharacterTestTacticalCompositionEdits.NewNode("Sequence");
            root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "base.shared"));
            root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "base.shared"));
            var definition = new CharacterTestTacticalSharedDefinition { DefinitionId = "base.shared", Source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack() };
            var record = Card("char.a");
            record.Metadata.Cost = 7; record.Metadata.TagsText = "测试标签";
            record.Source = new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["composition"] = JObject.Parse(CharacterTestTacticalCompositionJson.Write(new CharacterTestTacticalCompositionSource { Root = root })),
                ["definitions"] = new JArray(JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(definition))) };
            string original = record.Source.ToString();
            var other = record.Clone(); other.Id = Guid.NewGuid().ToString("N");
            var a = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            var b = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            var first = a.AddConfiguredTestCard(record); var second = b.AddConfiguredTestCard(other);
            Assert.AreEqual(7, first.Cost); Assert.AreEqual("测试标签", first.TagsText);
            var left = a.ResolveComposition(first, true); var right = b.ResolveComposition(second, true);
            Assert.AreEqual(left.Root.Children[0].DefinitionId, left.Root.Children[1].DefinitionId);
            Assert.AreNotEqual(left.Root.Children[0].DefinitionId, right.Root.Children[0].DefinitionId);
            string rightBefore = CharacterTestTacticalPublishedSource.CaptureDraft(b, second).ToJson().ToString();
            var changed = left.Definitions.Single().Source; changed.Name = "只改第一张";
            Assert.IsTrue(a.TryReplaceTacticalDefinition(a.CaptureTacticalDefinitionEdit(left.Definitions.Single().DefinitionId), changed, true, out string issue), issue);
            Assert.AreEqual(rightBefore, CharacterTestTacticalPublishedSource.CaptureDraft(b, second).ToJson().ToString());
            Assert.AreEqual(original, record.Source.ToString());
        }

        [Test] public void ConfiguredCardTestKeepsPeerSlotSelection()
        {
            var setup = CharacterTestArenaMatchConfig.CreateDefault();
            var source = CharacterTestRuntimeAiLabDraftWorkspace.Import(setup);
            Assert.IsTrue(source.TryCreateLocalGroupCopyForSlot(2, 0, out var peerGroup, out string issue), issue);
            Assert.IsTrue(source.TryGetGroupForSlot(1, 0, out var ownerGroup));
            Assert.AreNotEqual(ownerGroup.GroupDraftId, peerGroup.GroupDraftId);

            var testPersistence = new CharacterTestRuntimeAiLabDraftPersistence(Path.Combine(_root, "test-work"));
            var isolated = testPersistence.CreateFromWorkspace(source);
            isolated.AddConfiguredTestCard(Card("char.a"));

            Assert.AreNotEqual(source.WorkspaceId, isolated.WorkspaceId);
            Assert.IsTrue(isolated.TryGetGroupForSlot(2, 0, out var peer));
            Assert.AreEqual(peerGroup.GroupDraftId, peer.GroupDraftId);
            peer.DisplayName = "isolated change";
            Assert.AreNotEqual(peer.DisplayName, peerGroup.DisplayName);
        }
    }
}
