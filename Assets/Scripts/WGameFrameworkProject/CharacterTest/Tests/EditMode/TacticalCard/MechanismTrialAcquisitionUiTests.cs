using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class MechanismTrialAcquisitionUiTests
    {
        private const string Package = "MxCharacterTestTacticalDeckUi";
        private const string Prefix = "Assets/Bundles/FGUI/MxCharacterTestTacticalDeckUi/MxCharacterTestTacticalDeckUi";
        private static readonly string[] TrialIds = { "bc_gyj7wk6j1v", "bc_3gdyznnddp", "bc_j6919g51v2" };
        private GameObject _owner;
        private Fgui.GComponent _root;
        private CharacterTestFairyGuiTacticalPreparationView _view;
        private TacticalBattlePreparationController _controller;
        private TacticalBattlePreparationService _preparation;
        private CharacterTacticalPlayerService _player;
        private CharacterTacticalContentCatalog _catalog;
        private CharacterTacticalDefaults[] _defaults;
        private MemoryStore _playerStore;
        private string _entity, _character, _otherEntity, _otherCharacter;
        private bool _ownsPackage;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return new EnterPlayMode();
            var task = SetupAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        private async Task SetupAsync()
        {
            CharacterTestFairyGuiFonts.Initialize();
            _ownsPackage = Fgui.UIPackage.GetByName(Package) == null;
            if (_ownsPackage) Fgui.UIPackage.AddPackage(File.ReadAllBytes(Prefix + "_fui.bytes"), Prefix, LoadAtlas);
            _root = Fgui.UIPackage.CreateObject(Package, "TacticalDeckPanel").asCom;
            Assert.NotNull(_root); _root.SetSize(1920, 1080);
            var heroes = CharacterTestPlayableCharacterCatalog.Discover().Where(hero => hero.TacticalDefaults != null)
                .GroupBy(hero => hero.TacticalDefaults.CharacterStableId).Select(group => group.First()).Take(2).ToArray();
            Assert.AreEqual(2, heroes.Length);
            var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(published.Success, published.Diagnostic);
            _catalog = new CharacterTacticalContentCatalog();
            _catalog.SetPublishedCards(published.CardCatalog.Cards.Where(card => TrialIds.Contains(card.StableCardId) && card.Revision == 1));
            Assert.AreEqual(3, _catalog.Cards.Length, "Tests use the actual three formally published trial revisions.");
            _defaults = heroes.Select(hero => new CharacterTacticalDefaults { CharacterStableId = hero.TacticalDefaults.CharacterStableId }).ToArray();
            _playerStore = new MemoryStore();
            _player = await CharacterTacticalPlayerService.OpenAsync(_catalog, _defaults, _playerStore);
            _preparation = await TacticalBattlePreparationService.OpenAsync(_player, new MemoryStore(), heroes);
            _entity = _preparation.Working.Entities.First().EntityId;
            _character = _preparation.Find(_entity).CharacterStableId;
            var other = heroes.First(hero => hero.TacticalDefaults.CharacterStableId != _character);
            _otherCharacter = other.TacticalDefaults.CharacterStableId;
            Assert.IsTrue(_preparation.AddEntity(other.CharacterId, new Vector3(4f, 1f, 2f), 0f, 2, out _otherEntity, out var issue), issue);
            Assert.IsTrue(_preparation.SetPlayerControlledEntity(_entity, out issue), issue);
            _owner = new GameObject("MechanismTrialAcquisitionUiTest"); _owner.SetActive(false);
            _controller = _owner.AddComponent<TacticalBattlePreparationController>();
            _controller.Select(_entity); _controller.ToggleTop();
            _view = new CharacterTestFairyGuiTacticalPreparationView(); Bind();
        }

        private static object LoadAtlas(string name, string extension, Type type, out Fgui.DestroyMethod destroy)
        { destroy = Fgui.DestroyMethod.None; return AssetDatabase.LoadAssetAtPath<Texture2D>(name + extension); }
        private void Bind() => _view.Bind(_root, _controller, _preparation, _player);
        private static void Click(Fgui.GComponent parent, string name) => parent.GetChild(name).asButton.FireClick(false, true);
        private Fgui.GComponent Row(string listName, int index)
        {
            var list = _root.GetChild(listName).asList;
            var item = list.GetFromPool(list.defaultItem); list.itemRenderer(index, item);
            return item.asCom.GetChild("content")?.asCom ?? item.asCom;
        }
        private void OpenTrials()
        {
            Click(_root.GetChild("topBar").asCom, "more");
            var entry = Row("popupList", 2).asButton;
            Assert.AreEqual("领取机制试用卡…", entry.title); Assert.IsTrue(entry.enabled);
            entry.FireClick(false, true);
            Assert.AreEqual("领取机制试用卡", _root.GetChild("popupPanel").asCom.GetChild("title").text);
            Assert.AreEqual(3, _root.GetChild("popupList").asList.numItems);
        }

        [UnityTest]
        public IEnumerator PublishedTrialsCanBeClaimedOnceReloadedAndEquippedFromCurrentUi()
        {
            OpenTrials();
            foreach (int index in Enumerable.Range(0, 3))
            {
                var row = Row("popupList", index).asButton;
                Assert.IsTrue(row.enabled); row.FireClick(false, true); row.onClick.Call();
                Assert.IsFalse(Row("popupList", index).enabled);
            }
            Assert.AreEqual(3, _player.Read(_character).Cards.Count);
            Assert.AreEqual(0, _player.Read(_otherCharacter).Cards.Count);
            Assert.IsFalse(_player.HasUnsavedChanges);
            Assert.IsTrue(_defaults.All(value => value.Cards.Count == 0), "Developer claim must not rewrite initial defaults.");
            var reload = CharacterTacticalPlayerService.OpenAsync(_catalog, _defaults, _playerStore);
            while (!reload.IsCompleted) yield return null;
            CollectionAssert.AreEquivalent(TrialIds, reload.Result.Read(_character).Cards.Select(card => card.CardId));
            Click(_root.GetChild("popupPanel").asCom, "cancel");
            Click(_root, "deckAddCard"); Bind();
            Assert.AreEqual(3, _root.GetChild("libraryList").asList.numItems);
            int before = _preparation.Find(_entity).Loadout.Cards.Count;
            foreach (int index in Enumerable.Range(0, 3))
            {
                var card = Row("libraryList", index); Assert.IsTrue(card.GetChild("add").enabled);
                Click(card, "add");
            }
            var equipped = _preparation.Find(_entity).Loadout;
            Assert.AreEqual(before + 3, equipped.Cards.Count);
            CollectionAssert.AreEquivalent(TrialIds, equipped.Cards.Skip(before).Select(card => card.CardId));
            Assert.IsTrue(equipped.Cards.Skip(before).All(card => _catalog.ExactCard(card) != null));
            Assert.IsTrue(_catalog.Cards.All(card => !card.HasActionSlot));
            Assert.AreEqual(0, equipped.Bindings.Count, "These zero-action mechanisms need no fabricated action binding.");
        }

        [Test]
        public void StaleClaimRowCannotGrantAfterSwitchingCharacterOrReopeningPopup()
        {
            OpenTrials(); var stale = Row("popupList", 0).asButton;
            _controller.Select(_otherEntity); stale.onClick.Call();
            Assert.AreEqual(0, _player.Read(_character).Cards.Count);
            Assert.AreEqual(0, _player.Read(_otherCharacter).Cards.Count);
            Bind(); OpenTrials(); stale.onClick.Call();
            Assert.AreEqual(0, _player.Read(_character).Cards.Count);
            Assert.AreEqual(0, _player.Read(_otherCharacter).Cards.Count);
            Row("popupList", 0).asButton.FireClick(false, true);
            Assert.AreEqual(0, _player.Read(_character).Cards.Count);
            Assert.AreEqual(1, _player.Read(_otherCharacter).Cards.Count);
        }

        [Test]
        public void FailedCheckpointCanRetryWithoutGrantingAnotherInstance()
        {
            OpenTrials(); _playerStore.Fail = true;
            Row("popupList", 1).asButton.FireClick(false, true);
            Assert.AreEqual(1, _player.Read(_character).Cards.Count);
            Assert.IsTrue(_player.HasUnsavedChanges);
            var popup = _root.GetChild("popupPanel").asCom;
            Assert.IsTrue(popup.GetChild("confirm").visible);
            Assert.IsFalse(Row("popupList", 1).enabled);
            _playerStore.Fail = false; Click(popup, "confirm");
            Assert.IsFalse(_player.HasUnsavedChanges); Assert.AreEqual("", _player.SaveIssue);
            Assert.AreEqual(1, _player.Read(_character).Cards.Count);
            StringAssert.Contains("已保存", popup.GetChild("issue").text);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _view?.Dispose(); _root?.Dispose();
            if (_ownsPackage) Fgui.UIPackage.RemovePackage(Package);
            if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
            yield return new ExitPlayMode();
        }

        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            private string _json;
            internal bool Fail;
            public Task<string> LoadAsync() => Task.FromResult(_json);
            public Task SaveAsync(string value)
            { if (Fail) throw new InvalidOperationException("trial checkpoint test failure"); _json = value; return Task.CompletedTask; }
            public Task ResetAsync(string value) => SaveAsync(value);
        }
    }
}
