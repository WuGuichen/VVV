using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestConfiguredCardFaceTests
    {
        private GameObject _owner;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private CharacterTestConfiguredCardLibrary _store;
        private string _root, _preference;

        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ConfiguredFaceTests", Guid.NewGuid().ToString("N")));
            _store = new CharacterTestConfiguredCardLibrary(Path.Combine(_root, "project"), Path.Combine(_root, "recovery"));
            _owner = new GameObject("Configured card face tests"); _owner.SetActive(false);
            _controller = _owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            _controller.PrepareInitialSetupState(restorePersistedDraft: false);
            typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_testCardLibrary", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, _store);
            _preference = PlayerPrefs.GetString("RuntimeAiLab.TestCard.Last." + _controller.TestCardRole, "");
        }

        [TearDown] public void TearDown()
        {
            string pref = "RuntimeAiLab.TestCard.Last." + _controller.TestCardRole;
            if (_preference.Length == 0) PlayerPrefs.DeleteKey(pref); else PlayerPrefs.SetString(pref, _preference);
            UnityEngine.Object.DestroyImmediate(_owner);
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ConfiguredFaceTests")) + Path.DirectorySeparatorChar;
            if (_root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private CharacterTestConfiguredCard Create()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance);
            source.Schema = CharacterTestTacticalAuthoringSource.CurrentSchema; source.Version = 2;
            source.Slots.Clear();
            var card = new CharacterTestConfiguredCard { Id = Guid.NewGuid().ToString("N"), Name = "测试卡", OwnerCharacter = "",
                Source = new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                    ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) } };
            Assert.IsTrue(_store.Save(card, 0, null, out string issue), issue);
            return card;
        }

        [Test] public void InlineRenameStaysInLibraryAndRequiresExplicitSave()
        {
            var card = Create(); var workspace = _controller.DraftWorkspace;
            var face = _controller.ConfiguredCardFaces().Single();
            Assert.IsFalse(face.Dirty); Assert.IsEmpty(face.Actions);
            Assert.IsTrue(_controller.EditConfiguredCardFace(card.Id, _controller.TestCardRole, face.Token, "新名称", null, null, out string issue), issue);
            Assert.AreSame(workspace, _controller.DraftWorkspace); Assert.IsNull(_controller.TacticalCardContext);
            Assert.AreEqual("测试卡", _store.List("other-role").Single().Name);
            Assert.IsNull(_store.ReadRecovery("other-role", card.Id));
            Assert.IsTrue(_controller.ConfiguredCardFaces().Single().Dirty);
            Assert.IsTrue(_controller.SaveConfiguredCardFace(card.Id, _controller.TestCardRole, out issue), issue);
            Assert.AreEqual("新名称", _store.List("other-role").Single().Name);
            Assert.IsFalse(_controller.ConfiguredCardFaces().Single().Dirty);
        }

        [Test] public void InlineRenamePreservesUnsavedTimelineAndReopensTheSameCopy()
        {
            var card = Create(); string originalSource = card.Source.ToString();
            Assert.IsTrue(_controller.BeginStandaloneTacticalEditing("test:" + card.Id, out string issue), issue);
            var workspace = _controller.DraftWorkspace;
            var draft = workspace.Cards[_controller.TacticalCardContext.CardId];
            Assert.IsTrue(workspace.TryEditTacticalSource(new CharacterTestTacticalEditToken(workspace, draft), "编辑事件名称",
                source => source.Timelines[0].Events[0].Title = "保留时间轴改动", out issue), issue);
            var face = _controller.ConfiguredCardFaces().Single();
            Assert.IsTrue(_controller.EditConfiguredCardFace(card.Id, _controller.TestCardRole, face.Token, "卡面改名", null, null, out issue), issue);
            Assert.AreSame(workspace, _controller.DraftWorkspace);
            Assert.IsTrue(_controller.LeaveConfiguredCard());
            Assert.IsTrue(_controller.BeginStandaloneTacticalEditing("test:" + card.Id, out issue), issue);
            draft = _controller.DraftWorkspace.Cards[_controller.TacticalCardContext.CardId];
            Assert.AreEqual("卡面改名", draft.DisplayName);
            Assert.AreEqual("保留时间轴改动", draft.CloneTacticalSource().Timelines[0].Events[0].Title);
            Assert.AreEqual(originalSource, _store.List(_controller.TestCardRole).Single().Source.ToString());
        }

        [Test] public void StaleRoleOrCardAndSaveConflictsNeverOverwriteWork()
        {
            var card = Create(); var face = _controller.ConfiguredCardFaces().Single();
            Assert.IsFalse(_controller.EditConfiguredCardFace(card.Id, "other-role", face.Token, "错误角色", null, null, out _));
            Assert.IsTrue(_controller.EditConfiguredCardFace(card.Id, _controller.TestCardRole, face.Token, "本地名称", null, null, out _));
            Assert.IsFalse(_controller.EditConfiguredCardFace(card.Id, _controller.TestCardRole, face.Token, "过期回调", null, null, out _));
            card.Name = "远端新版本"; Assert.IsTrue(_store.Save(card, card.Revision, card.FileHash, out _));
            Assert.IsFalse(_controller.SaveConfiguredCardFace(card.Id, _controller.TestCardRole, out _));
            Assert.AreEqual("本地名称", _store.ReadRecovery(_controller.TestCardRole, card.Id).Card.Name);
            Assert.AreEqual("远端新版本", _store.List(_controller.TestCardRole).Single().Name);
        }

        [Test] public void RepeatedReferencesHaveIndependentCardActionRows()
        {
            var document = new CharacterTestTacticalDslSource { CardId = "repeat", Name = "重复引用",
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new CharacterTestTacticalDslNode { NodeId = "first", Kind = "Reference", DefinitionId = "shared" },
                    new CharacterTestTacticalDslNode { NodeId = "second", Kind = "Reference", DefinitionId = "shared" } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition {
                    DefinitionId = "shared", Source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack() } } };
            Assert.IsTrue(_controller.DraftWorkspace.TryImportComposition(1, 0, _controller.DraftWorkspace.CurrentRevision, document, out string id, out string issue), issue);
            var draft = _controller.DraftWorkspace.Cards[id];
            var card = new CharacterTestConfiguredCard { Source = CharacterTestTacticalPublishedSource.CaptureDraft(_controller.DraftWorkspace, draft).ToJson() };
            var rows = CharacterTestRuntimeAiPlannerDevSceneController.ConfiguredActionLocations(card);
            Assert.AreEqual(2, rows.Length); Assert.AreNotEqual(rows[0].Path, rows[1].Path); Assert.AreNotEqual(rows[0].Label, rows[1].Label);
            card.Actions[rows[0].Path] = 301; card.Actions[rows[1].Path] = 455;
            rows = CharacterTestRuntimeAiPlannerDevSceneController.ConfiguredActionLocations(card);
            Assert.AreEqual("action:301", rows[0].ActionId); Assert.AreEqual("action:455", rows[1].ActionId);
            StringAssert.DoesNotContain("primary", rows[0].Label);
        }
    }
}
