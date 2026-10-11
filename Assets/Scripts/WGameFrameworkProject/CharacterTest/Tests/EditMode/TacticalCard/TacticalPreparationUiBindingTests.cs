using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalPreparationUiBindingTests
    {
        private const string Package = "MxCharacterTestTacticalDeckUi";
        private const string Prefix = "Assets/Bundles/FGUI/MxCharacterTestTacticalDeckUi/MxCharacterTestTacticalDeckUi";
        private GameObject _owner;
        private Fgui.GComponent _root;
        private CharacterTestFairyGuiTacticalPreparationView _view;
        private TacticalBattlePreparationController _controller;
        private TacticalBattlePreparationService _preparation;
        private CharacterTacticalPlayerService _player;
        private MemoryStore _store;
        private MemoryStore _fixturePlayerStore;
        private string _entity;
        private bool _ownsPackage;
        private InventoryOwnerId _fixtureInventoryOwner;
        private CharacterTacticalRoleBindingQueue _fixtureRoleBindings;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // FairyGUI virtual lists own a runtime Timers object; exercise the real lifecycle.
            yield return new EnterPlayMode();
            var setup = SetupAsync();
            while (!setup.IsCompleted) yield return null;
            setup.GetAwaiter().GetResult();
        }

        private async Task SetupAsync()
        {
            CharacterTestFairyGuiFonts.Initialize();
            _ownsPackage = Fgui.UIPackage.GetByName(Package) == null;
            if (_ownsPackage)
                Fgui.UIPackage.AddPackage(File.ReadAllBytes(Prefix + "_fui.bytes"), Prefix, LoadAtlas);
            _root = Fgui.UIPackage.CreateObject(Package, "TacticalDeckPanel").asCom;
            Assert.NotNull(_root); _root.SetSize(1920, 1080);
            _owner = new GameObject("TacticalPreparationUiTest"); _owner.SetActive(false);
            _controller = _owner.AddComponent<TacticalBattlePreparationController>();
            var hero = CharacterTestPlayableCharacterCatalog.Discover().First(value => value.TacticalDefaults != null);
            string stable = hero.TacticalDefaults.CharacterStableId;
            var sourceCard = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load().CardCatalog.Cards
                .Where(value => value.StableCardId == "tactical.bound-action").OrderByDescending(value => value.Revision).First();
            var catalog = new CharacterTacticalContentCatalog
            {
                Actions = Enumerable.Range(0, 8).Select(i => new CharacterTacticalActionDefinition { Id = "action." + i,
                    DisplayName = "动作 " + i, ActionId = 301, ActionLibraryId = "Common", CharacterStableIds = new[] { stable } }).ToArray(),
                Cards = Enumerable.Range(0, 12).Select(i => new CharacterTacticalCardDefinition { CardId = "card." + i,
                    Revision = 1, ContentHash = "hash." + i, DisplayName = "行为卡 " + i, Description = "真实配置说明 " + i,
                    IconKey = "ui.character-test.behavior-card.primary-attack", MotionCost = 7 + i, Slots = (string[])typeof(CharacterTacticalContentCatalog).GetMethod("PublishedSlots", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { sourceCard.TacticalSource }),
                    PresentationSource = sourceCard.TacticalSource }).ToArray()
            };
            var defaults = new CharacterTacticalDefaults { CharacterStableId = stable,
                ActionIds = catalog.Actions.Select(value => value.Id).ToList(), Cards = new List<CharacterTacticalCardReference> {
                    Reference(catalog.Cards[0], 1), Reference(catalog.Cards[0], 2) } };
            _fixturePlayerStore = new MemoryStore();
            _player = await CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, _fixturePlayerStore);
            _store = new MemoryStore();
            _preparation = await TacticalBattlePreparationService.OpenAsync(_player, _store, new[] { hero });
            _entity = _preparation.Working.Entities.First().EntityId;
            foreach (var definition in catalog.Cards.Skip(1))
                Assert.IsTrue(_player.TryGrantCardForTesting(stable, Reference(definition, 0), out var grantIssue), grantIssue);

            _controller.Select(_entity); _view = new CharacterTestFairyGuiTacticalPreparationView();
            Bind();
        }
        private async Task<(ItemInstanceId A, ItemInstanceId B)> SetupInstanceUiAsync(bool placeBoth = true)
        {
            var hero = CharacterTestPlayableCharacterCatalog.Discover().First(value => value.TacticalDefaults != null);
            string stable = hero.TacticalDefaults.CharacterStableId;
            var catalog = _player.Catalog;
            catalog.Actions = catalog.Actions.Concat(new[] { new CharacterTacticalActionDefinition {
                Id = "instance.action.302", ActionId = 302, ActionLibraryId = "Common", DisplayName = "实例独立动作", CharacterStableIds = new[] { stable } } }).ToArray();
            catalog.Acquisition = new[] { new CharacterTacticalAcquisitionPool { CharacterStableId = stable,
                ActionIds = catalog.Actions.Select(value => value.Id).ToArray(), Cards = Array.Empty<CharacterTacticalCardReference>() } };
            var defaults = new CharacterTacticalDefaults { CharacterStableId = stable, ActionIds = new List<string> { "action.0" },
                Cards = new List<CharacterTacticalCardReference> { Reference(catalog.Cards[0], 1), Reference(catalog.Cards[0], 2) } };
            defaults.Bindings = defaults.Cards.SelectMany(card => catalog.Cards[0].Slots.Select(slot => new CharacterTacticalSlotBinding {
                CardInstanceId = card.InstanceId, CardId = card.CardId, SlotPath = slot, TacticalActionId = "action.0" })).ToList();
            _fixturePlayerStore = new MemoryStore();
            _player = await CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, _fixturePlayerStore);
            var definitions = new ItemDefinitionRegistry();
            Assert.IsTrue(definitions.Register(new ItemDefinition(new ItemDefinitionId(41001), "fixture.role-ui", default, default,
                ItemKind.Custom, default, new ItemStackPolicy(1))).Success);
            _fixtureInventoryOwner = new InventoryOwnerId(1);
            Assert.IsTrue(_player.ConfigureCharacterItemInventory(definitions, new[] {
                new InventoryContainer(new InventoryContainerId(1), _fixtureInventoryOwner, 4, "fixture.roles") },
                new[] { new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 41001, CharacterStableId = stable } }, out var issue), issue);
            var tick = new RuntimeTickContext(10, 1d / 60d, 10d / 60d, RuntimeTickStage.Simulation);
            ItemInstanceId Acquire(long request, string name)
            {
                var result = _player.AcquireCharacterItemAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(request),
                    ItemAcquisitionSourceKind.DirectGrant, null, null), new InventoryContainerId(1), _fixtureInventoryOwner,
                    new ItemDefinitionId(41001), name, _player.Revision, 10, tick, out var id, out var acquisitionIssue);
                Assert.IsTrue(result.Success, acquisitionIssue); return id;
            }
            var a = Acquire(1, "角色 A"); var b = Acquire(2, "角色 B");
            Assert.IsTrue(_player.TryReadCharacterItem(a, _fixtureInventoryOwner, out var roleA, out issue), issue);
            Assert.IsTrue(_player.LearnCharacterActionAtTick(new ItemAcquisitionRequestId(100), a, _fixtureInventoryOwner,
                "instance.action.302", roleA.Revision, _player.Revision, 10, tick, out issue).Success, issue);
            Assert.IsTrue(_player.TryReadCharacterItem(a, _fixtureInventoryOwner, out roleA, out issue), issue);
            Assert.IsTrue(_player.BindCharacterActionAtTick(a, _fixtureInventoryOwner, roleA.Cards[0].InstanceId, catalog.Cards[0].Slots[0],
                "instance.action.302", roleA.Revision, _player.Revision, 10, tick, out issue), issue);
            _store = new MemoryStore();
            _preparation = await TacticalBattlePreparationService.OpenAsync(_player, _store, new[] { hero });
            if (placeBoth)
            {
                Assert.IsTrue(_preparation.AddCharacterItemEntity(a, _fixtureInventoryOwner, new Vector3(-2f, 1f, 0f), 0f, 1,
                    _preparation.Revision, out _entity, out issue), issue);
                Assert.IsTrue(_preparation.AddCharacterItemEntity(b, _fixtureInventoryOwner, new Vector3(2f, 1f, 0f), 0f, 2,
                    _preparation.Revision, out _, out issue), issue);
            }
            else _entity = string.Empty;
            _fixtureRoleBindings = new CharacterTacticalRoleBindingQueue(_player, _preparation, _fixtureInventoryOwner);
            _controller.Select(_entity); Bind();
            if (_controller.DrawerMode != TacticalPreparationDrawerMode.Characters) _controller.ToggleCharacterDrawer();
            Bind(); return (a, b);
        }

        [Test]
        public async Task OwnedCharacterRowsSelectAAndBAndReadTheirOwnCardsAndActions()
        {
            var pair = await SetupInstanceUiAsync(); string before = _player.CaptureProgressJson(); long prepRevision = _preparation.Revision;
            Assert.AreEqual(2, _root.GetChild("characterList").asList.numItems);
            var a = RenderItem("characterList", 0).asButton; var b = RenderItem("characterList", 1).asButton;
            Assert.AreEqual(pair.A.Value, a.data); Assert.AreEqual(pair.B.Value, b.data);
            Assert.AreEqual("角色 A", a.title); Assert.AreEqual("角色 B", b.title);
            a.FireClick(false, true); Bind(); var selectedA = _preparation.Find(_controller.SelectedEntityId);
            Assert.AreEqual(pair.A.Value, selectedA.CharacterItemInstanceId); Assert.AreEqual("角色 A", _root.GetChild("entityHeader").asCom.GetChild("name").asTextField.text);
            Assert.AreEqual("instance.action.302", selectedA.Loadout.Bindings[0].TacticalActionId);
            ClickInlineAction(RenderItem("deckList", 0)); Assert.AreEqual(3, _root.GetChild("popupList").asList.numItems);
            Assert.IsTrue(RenderItem("popupList", 0).asButton.enabled);
            Click(_root.GetChild("popupPanel").asCom, "close");
            b.FireClick(false, true); Bind(); var selectedB = _preparation.Find(_controller.SelectedEntityId);
            Assert.AreEqual(pair.B.Value, selectedB.CharacterItemInstanceId); Assert.AreEqual("action.0", selectedB.Loadout.Bindings[0].TacticalActionId);
            Assert.AreNotEqual(selectedA.Loadout.Cards[0].InstanceId, selectedB.Loadout.Cards[0].InstanceId);
            ClickInlineAction(RenderItem("deckList", 0)); Assert.AreEqual(2, _root.GetChild("popupList").asList.numItems);
            Assert.AreEqual(before, _player.CaptureProgressJson()); Assert.AreEqual(prepRevision, _preparation.Revision);
            Assert.AreEqual(2, _preparation.Working.Entities.Count);
        }

        [Test]
        public async Task MissingInventoryOwnerShowsGateAndNeverListsTemplatesAsHeldCharacters()
        {
            await SetupInstanceUiAsync(); string before = _player.CaptureProgressJson();
            _fixtureInventoryOwner = default; Bind();
            Assert.AreEqual(0, _root.GetChild("characterList").asList.numItems);
            Assert.IsTrue(_root.GetChild("libraryEmpty").visible);
            StringAssert.Contains("库存归属", _root.GetChild("libraryEmpty").asTextField.text);
            Assert.AreEqual(before, _player.CaptureProgressJson());
        }

        [Test]
        public async Task RowPlacementCarriesItemsIdentityAndDuplicateClickOnlyFocusesExistingEntity()
        {
            var pair = await SetupInstanceUiAsync(false); long progressRevision = _player.Revision;
            var row = RenderItem("characterList", 0).asButton; row.FireClick(false, true);
            var field = typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_placementItem",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(pair.A, field.GetValue(_view));
            Assert.IsTrue(_controller.AddCharacterItemAtPosition(pair.A, _fixtureInventoryOwner, new Vector3(-2f, 1f, 0f),
                _preparation.Revision, _player.Revision));
            typeof(CharacterTestFairyGuiTacticalPreparationView).GetMethod("CancelTemplatePlacement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_view, null);
            Bind(); row = RenderItem("characterList", 0).asButton; string entity = _controller.SelectedEntityId;
            row.FireClick(false, true); Assert.AreEqual(entity, _controller.SelectedEntityId);
            Assert.AreEqual(pair.A.Value, _preparation.Find(entity).CharacterItemInstanceId);
            Assert.AreEqual(1, _preparation.Working.Entities.Count); Assert.AreEqual(progressRevision, _player.Revision);
        }

        [Test]
        public async Task InstancePlacementRejectsWrongOwnerAndStalePreviewWithoutAnyMutation()
        {
            var pair = await SetupInstanceUiAsync(false); string before = _preparation.CaptureProgressJson(), progress = _player.CaptureProgressJson();
            Assert.IsFalse(_controller.AddCharacterItemAtPosition(pair.A, new InventoryOwnerId(2), Vector3.zero,
                _preparation.Revision, _player.Revision));
            Assert.IsFalse(_controller.AddCharacterItemAtPosition(pair.A, _fixtureInventoryOwner, Vector3.zero,
                _preparation.Revision - 1, _player.Revision));
            Assert.AreEqual(before, _preparation.CaptureProgressJson()); Assert.AreEqual(progress, _player.CaptureProgressJson());
        }

        [Test]
        public async Task StaleOwnedRowCannotFocusAfterInventoryOwnerChanges()
        {
            await SetupInstanceUiAsync(); var row = RenderItem("characterList", 0).asButton;
            _controller.Select(string.Empty); _fixtureInventoryOwner = default; Bind();
            string before = _preparation.CaptureProgressJson(); row.FireClick(false, true);
            Assert.AreEqual(string.Empty, _controller.SelectedEntityId);
            Assert.AreEqual(before, _preparation.CaptureProgressJson());
        }

        private static object LoadAtlas(string name, string extension, Type type, out Fgui.DestroyMethod destroyMethod)
        {
            destroyMethod = Fgui.DestroyMethod.None;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(name + extension);
        }
        private static CharacterTacticalCardReference Reference(CharacterTacticalCardDefinition card, ulong instance) =>
            new CharacterTacticalCardReference { CardId = card.CardId, Revision = card.Revision, ContentHash = card.ContentHash, InstanceId = instance };
        [Test] public async Task ActualActionRowQueuesBindingAndUnbindingForOnlyTheSelectedRole()
        {
            var pair = await SetupInstanceUiAsync();
            var other = _preparation.Working.Entities.Single(entity => entity.CharacterItemInstanceId == pair.B.Value);
            string before = _player.CaptureProgressJson(); string otherBefore = JsonConvert.SerializeObject(other.Loadout);
            ClickInlineAction(RenderItem("deckList", 0)); RenderItem("popupList", 0).asButton.FireClick(false, true);
            Assert.AreEqual(1, _fixtureRoleBindings.PendingCount); Assert.AreEqual(before, _player.CaptureProgressJson());
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(() => _fixtureRoleBindings, () => true));
            host.Initialize(); host.Start(); host.Tick(11, 1d / 60d); Bind();
            Assert.AreEqual("action.0", _preparation.Find(_entity).Loadout.Bindings[0].TacticalActionId);
            Assert.AreEqual(otherBefore, JsonConvert.SerializeObject(_preparation.Find(other.EntityId).Loadout));
            ClickInlineAction(RenderItem("deckList", 0)); RenderItem("popupList", 2).asButton.FireClick(false, true);
            host.Tick(12, 1d / 60d); Bind();
            var role = _preparation.Find(_entity).Loadout; ulong first = role.Cards[0].InstanceId;
            Assert.IsFalse(role.Bindings.Any(binding => binding.CardInstanceId == first));
            Assert.AreEqual(otherBefore, JsonConvert.SerializeObject(_preparation.Find(other.EntityId).Loadout));
            Assert.IsTrue(await _player.FlushAsync());
        }

        [Test] public async Task RoleBindingSaveFailureShowsRetryAndDoesNotClaimSaved()
        {
            await SetupInstanceUiAsync(); _fixturePlayerStore.Fail = true;
            ClickInlineAction(RenderItem("deckList", 0)); RenderItem("popupList", 0).asButton.FireClick(false, true);
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(() => _fixtureRoleBindings, () => true));
            host.Initialize(); host.Start(); host.Tick(11, 1d / 60d); Bind();
            Assert.IsTrue(_player.HasUnsavedChanges); Assert.IsNotEmpty(_player.SaveIssue);
            StringAssert.Contains("保存失败", _root.GetChild("topBar").asCom.GetChild("status").asTextField.text);
            Click(_root.GetChild("topBar").asCom, "more"); Assert.AreEqual(5, _root.GetChild("popupList").asList.numItems);
            var retry = RenderItem("popupList", 4).asButton; Assert.AreEqual("重试保存进度", retry.title);
            _fixturePlayerStore.Fail = false; retry.FireClick(false, true); Bind();
            Assert.IsFalse(_player.HasUnsavedChanges); Assert.IsEmpty(_player.SaveIssue);
            Assert.AreEqual("action.0", _preparation.Find(_entity).Loadout.Bindings[0].TacticalActionId);
        }

        [Test] public async Task PreparationOnlySaveFailureShowsRetryAndPersistsLatestPoseWithoutPlayerChanges()
        {
            await SetupInstanceUiAsync();
            Assert.IsTrue(await _player.FlushAsync()); Assert.IsTrue(await _preparation.FlushAsync());
            string playerBefore = _player.CaptureProgressJson(); long playerRevision = _player.Revision;
            _store.Fail = true;
            Assert.IsTrue(_preparation.SetPose(_entity, new Vector3(-3f, 1f, .5f), 90f, out var issue), issue);
            Assert.IsFalse(await _preparation.FlushAsync()); Bind();
            Assert.IsFalse(_player.HasUnsavedChanges); Assert.IsEmpty(_player.SaveIssue);
            Assert.IsTrue(_preparation.HasUnsavedChanges); Assert.IsNotEmpty(_preparation.SaveIssue);
            StringAssert.Contains("保存失败", _root.GetChild("topBar").asCom.GetChild("status").asTextField.text);
            string preparationBefore = _preparation.CaptureProgressJson();
            Click(_root.GetChild("topBar").asCom, "more");
            Assert.AreEqual(5, _root.GetChild("popupList").asList.numItems);
            var retry = RenderItem("popupList", 4).asButton; Assert.AreEqual("重试保存进度", retry.title);
            _store.Fail = false; retry.FireClick(false, true); await Task.Yield(); Bind();
            Assert.IsFalse(_preparation.HasUnsavedChanges); Assert.IsEmpty(_preparation.SaveIssue);
            Assert.IsFalse(_root.GetChild("popupPanel").visible);
            Assert.AreEqual(preparationBefore, await _store.LoadAsync());
            Assert.AreEqual(playerBefore, _player.CaptureProgressJson()); Assert.AreEqual(playerRevision, _player.Revision);
            Click(_root.GetChild("topBar").asCom, "more");
            Assert.AreEqual(4, _root.GetChild("popupList").asList.numItems, "Retry must disappear after both services are durable.");
        }

        [Test] public async Task BothSaveFailuresKeepRetryUntilPreparationAlsoPersistsWithoutRepeatingBinding()
        {
            await SetupInstanceUiAsync();
            Assert.IsTrue(await _player.FlushAsync()); Assert.IsTrue(await _preparation.FlushAsync());
            _fixturePlayerStore.Fail = true; _store.Fail = true;
            ClickInlineAction(RenderItem("deckList", 0)); RenderItem("popupList", 0).asButton.FireClick(false, true);
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(() => _fixtureRoleBindings, () => true));
            host.Initialize(); host.Start(); host.Tick(11, 1d / 60d);
            Assert.IsTrue(_preparation.SetPose(_entity, new Vector3(-3f, 1f, .5f), 90f, out var issue), issue);
            Assert.IsFalse(await _player.FlushAsync()); Assert.IsFalse(await _preparation.FlushAsync()); Bind();
            Assert.IsTrue(_player.HasUnsavedChanges); Assert.IsNotEmpty(_player.SaveIssue);
            Assert.IsTrue(_preparation.HasUnsavedChanges); Assert.IsNotEmpty(_preparation.SaveIssue);
            string playerBefore = _player.CaptureProgressJson(), preparationBefore = _preparation.CaptureProgressJson();
            long playerRevision = _player.Revision, preparationRevision = _preparation.Revision;
            Click(_root.GetChild("topBar").asCom, "more");
            var retry = RenderItem("popupList", 4).asButton; Assert.AreEqual("重试保存进度", retry.title);
            _fixturePlayerStore.Fail = false; retry.FireClick(false, true); await Task.Yield(); Bind();
            Assert.IsFalse(_player.HasUnsavedChanges); Assert.IsEmpty(_player.SaveIssue);
            Assert.IsTrue(_preparation.HasUnsavedChanges); Assert.IsNotEmpty(_preparation.SaveIssue);
            Assert.IsTrue(_root.GetChild("popupPanel").visible, "A partial retry must not claim the whole progress is saved.");
            retry = RenderItem("popupList", 4).asButton; Assert.AreEqual("重试保存进度", retry.title);
            _store.Fail = false; retry.FireClick(false, true); await Task.Yield(); Bind();
            Assert.IsFalse(_player.HasUnsavedChanges); Assert.IsEmpty(_player.SaveIssue);
            Assert.IsFalse(_preparation.HasUnsavedChanges); Assert.IsEmpty(_preparation.SaveIssue);
            Assert.IsFalse(_root.GetChild("popupPanel").visible);
            Assert.AreEqual(playerBefore, await _fixturePlayerStore.LoadAsync()); Assert.AreEqual(preparationBefore, await _store.LoadAsync());
            Assert.AreEqual(playerRevision, _player.Revision); Assert.AreEqual(preparationRevision, _preparation.Revision);
            Assert.AreEqual(0, _fixtureRoleBindings.PendingCount);
            Assert.AreEqual("action.0", _preparation.Find(_entity).Loadout.Bindings[0].TacticalActionId);
        }

        private void Bind() => _view.Bind(_root, _controller, _preparation, _player, _fixtureInventoryOwner, _fixtureRoleBindings);
        private Fgui.GComponent RenderItem(string listName, int index)
        {
            var list = _root.GetChild(listName).asList;
            var item = list.GetFromPool(list.defaultItem); list.itemRenderer(index, item); return CardContent(item.asCom);
        }
        private static Fgui.GComponent CardContent(Fgui.GComponent item) => item.GetChild("content")?.asCom ?? item;
        private static void Click(Fgui.GComponent parent, string name) => parent.GetChild(name).asButton.FireClick(false, true);
        private static Fgui.GComponent InlineAction(Fgui.GComponent item)
        {
            var rows = item.GetChild("face").asCom.GetChild("behaviorHints").asList;
            rows.EnsureBoundsCorrect();
            return rows.GetChildren().Select(value => value.asCom.GetChild("action").asCom).First(value => value.visible);
        }
        private static void ClickInlineAction(Fgui.GComponent item) => InlineAction(item).onClick.Call();

        [TestCase(1280, 720)] [TestCase(1920, 1080)] [TestCase(2560, 1440)]
        public void PreparationDrawersDoNotOverlapToolbarOrDeckAndCardsKeepAspect(int width, int height)
        {
            _root.SetSize(width, height); _controller.ToggleTop(); _controller.OpenCardDrawer(); Bind();
            var top = _root.GetChild("topBar"); var library = _root.GetChild("libraryList");
            var deck = _root.GetChild("deckList"); var inspector = _root.GetChild("entityHeader");
            Assert.Greater(library.y, top.y + top.height * top.scaleY);
            Assert.GreaterOrEqual(deck.x, library.x + library.width);
            Assert.Less(inspector.y + inspector.height * inspector.scaleY, deck.y);
            var card = RenderItem("deckList", 0);
            Assert.AreEqual(card.scaleX, card.scaleY, .0001f);
            Assert.AreEqual(63f/88f, card.GetChild("face").width/card.GetChild("face").height, .0001f);
            Assert.IsFalse(_controller.DrawerPinned); Assert.IsFalse(_controller.TopPinned);
            Click(_root.GetChild("drawerHeader").asCom, "pin"); Assert.IsTrue(_controller.DrawerPinned);
            Click(_root.GetChild("topBar").asCom, "pin"); Assert.IsTrue(_controller.TopPinned);
        }

        [UnityTest]
        public IEnumerator EdgeHoverOpensAndLeavingClosesUnlessDraggingOrPopupIsActive()
        {
            var tick = typeof(TacticalBattlePreparationController).GetMethod("UpdateAutoCollapse",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            _root.GetChild("leftHandle").onRollOver.Call();
            Assert.AreEqual(TacticalPreparationDrawerMode.Characters, _controller.DrawerMode);
            _root.GetChild("topHandle").onRollOver.Call(); Assert.IsTrue(_controller.TopExpanded);
            _controller.HoldDrawer(false); _controller.HoldTop(false);
            yield return new WaitForSecondsRealtime(.4f);
            _controller.TemplateDragActive = true; tick.Invoke(_controller, null);
            Assert.AreEqual(TacticalPreparationDrawerMode.Characters, _controller.DrawerMode);
            _controller.TemplateDragActive = false; _controller.HoldPopup(true); tick.Invoke(_controller, null);
            Assert.IsTrue(_controller.TopExpanded);
            _controller.HoldPopup(false); tick.Invoke(_controller, null);
            Assert.AreEqual(TacticalPreparationDrawerMode.Collapsed, _controller.DrawerMode);
            Assert.IsFalse(_controller.TopExpanded);
            _controller.OpenCardDrawer(); _controller.CollapseDrawer();
            _controller.RevealDrawerFromPointer();
            Assert.AreEqual(TacticalPreparationDrawerMode.Cards, _controller.DrawerMode, "Hover restores the last library mode.");
        }

        [Test]
        public void QualityExamplesAreExplicitAndDoNotMutateGameplay()
        {
            string before = JsonConvert.SerializeObject(_preparation.Working);
            var resolve = typeof(CharacterTestFairyGuiTacticalPreparationView).GetMethod("ResolvePreviewQuality",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(0, resolve.Invoke(_view, new object[] { "tactical.bound-action" }));
            Assert.AreEqual(1, resolve.Invoke(_view, new object[] { "bc_2b0ehvtbzb" }));
            Assert.AreEqual(2, resolve.Invoke(_view, new object[] { "bc_vyj9212dvz" }));
            Assert.AreEqual(0, resolve.Invoke(_view, new object[] { "unconfigured.card" }));
            Assert.AreEqual(before, JsonConvert.SerializeObject(_preparation.Working));
        }

        [Test]
        public void ParameterHintsArePointerTransparentAndOnlyAltRetainsThem()
        {
            var previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            keyboard.MakeCurrent();
            var face = RenderItem("deckList", 0).GetChild("face").asCom;
            var windows = _root.GetChild("parameterWindows").asList;
            try
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                UnityEngine.InputSystem.InputSystem.Update();
                face.onRollOver.Call();
                Assert.IsTrue(windows.visible); Assert.IsFalse(windows.touchable);
                face.onRollOut.Call();
                Assert.IsFalse(windows.visible, "Leaving the card must hide immediately, without a hover grace timer.");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.LeftAlt));
                UnityEngine.InputSystem.InputSystem.Update();
                Assert.IsTrue(keyboard.leftAltKey.isPressed, "Alt input state");
                face.onRollOver.Call(); Assert.IsTrue(windows.visible, "Alt enter");
                Bind(); Assert.IsTrue(windows.visible, "Alt frame update");
                face.onRollOut.Call();
                Assert.IsTrue(windows.visible, "Alt leave card"); Assert.IsTrue(windows.touchable, "Alt permits interaction");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                UnityEngine.InputSystem.InputSystem.Update();
                Bind();
                Assert.IsFalse(windows.visible, "Releasing Alt outside the card must release the retained tooltip.");
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); previousKeyboard?.MakeCurrent(); }
        }

        [Test]
        public void ClickingCardBodyDoesNotOpenDetailsWhileActionNameStillWorks()
        {
            var deckCard = RenderItem("deckList", 0);
            deckCard.onClick.Call();
            Assert.IsFalse(_root.GetChild("popupPanel").visible);
            _controller.OpenCardDrawer(); Bind();
            int count = _preparation.Find(_entity).Loadout.Cards.Count;
            RenderItem("libraryList", 0).onClick.Call();
            Assert.IsFalse(_root.GetChild("popupPanel").visible);
            Assert.AreEqual(count, _preparation.Find(_entity).Loadout.Cards.Count);
            ClickInlineAction(deckCard);
            Assert.IsTrue(_root.GetChild("popupPanel").visible);
            Assert.AreEqual(8, _root.GetChild("popupList").asList.numItems);
        }

        private string AddAcquisitionCandidate()
        {
            string character = _preparation.Find(_entity).CharacterStableId;
            _player.Catalog.Actions = _player.Catalog.Actions.Concat(new[] { new CharacterTacticalActionDefinition {
                Id = "test.obtain", DisplayName = "测试新动作", ActionId = 455, ActionLibraryId = "Common",
                CharacterStableIds = new[] { character } } }).ToArray();
            _player.Catalog.Acquisition = new[] { new CharacterTacticalAcquisitionPool {
                CharacterStableId = character, ActionIds = new[] { "test.obtain" } } };
            return character;
        }

        private void OpenAcquisitionCandidate()
        {
            Click(_root.GetChild("topBar").asCom, "more");
            RenderItem("popupList", 1).asButton.FireClick(false, true);
            var input = _root.GetChild("popupPanel").asCom.GetChild("input").asTextInput;
            input.text = "测试新动作"; input.onChanged.Call();
        }

        [Test]
        public void TestAcquisitionGrantsOnceAndDoesNotAutoEquipOrChangePreparation()
        {
            string character = AddAcquisitionCandidate();
            string before = JsonConvert.SerializeObject(_preparation.Working);
            int count = _player.Read(character).ActionIds.Count;
            OpenAcquisitionCandidate(); Assert.AreEqual(1, _root.GetChild("popupList").asList.numItems);
            var row = RenderItem("popupList", 0).asButton; Assert.IsTrue(row.enabled);
            row.FireClick(false, true); row.FireClick(false, true);
            Assert.IsTrue(_player.Read(character).ActionIds.Contains("test.obtain"));
            Assert.AreEqual(count + 1, _player.Read(character).ActionIds.Count);
            Assert.IsFalse(_player.HasUnsavedChanges);
            Assert.AreEqual(before, JsonConvert.SerializeObject(_preparation.Working));
            Assert.IsFalse(RenderItem("popupList", 0).enabled);
            Click(_root.GetChild("popupPanel").asCom, "close");
            ClickInlineAction(RenderItem("deckList", 0));
            Assert.AreEqual(9, _root.GetChild("popupList").asList.numItems, "Newly owned actions immediately become equip candidates.");
        }

        [Test]
        public void TestAcquisitionRechecksEligibilityWhenClicked()
        {
            string character = AddAcquisitionCandidate(); OpenAcquisitionCandidate();
            var row = RenderItem("popupList", 0).asButton;
            _player.Catalog.Acquisition[0].ActionIds = Array.Empty<string>();
            row.FireClick(false, true);
            Assert.IsFalse(_player.Read(character).ActionIds.Contains("test.obtain"));
            Assert.IsNotEmpty(_root.GetChild("popupPanel").asCom.GetChild("issue").text);
            Assert.IsFalse(RenderItem("popupList", 0).enabled);
        }

        [Test]
        public void TestAcquisitionStaleRowCannotGrantAfterChangingSelection()
        {
            string character = AddAcquisitionCandidate(); OpenAcquisitionCandidate();
            var row = RenderItem("popupList", 0).asButton;
            _controller.Select(""); row.FireClick(false, true); Bind();
            Assert.IsFalse(_player.Read(character).ActionIds.Contains("test.obtain"));
            Assert.IsFalse(_root.GetChild("popupPanel").visible);
        }

        [Test]
        public void ShortFrostedHintAtTopFitsContentAndDoesNotCoverItsButton()
        {
            Fgui.GRoot.inst.AddChild(_root); _controller.ToggleTop(); Bind();
            var target = _root.GetChild("topBar").asCom.GetChild("preset");
            using var tooltip = new CharacterTestFairyGuiCardTooltip(true);
            tooltip.Bind(target, "开始战斗", new MxFramework.UI.FairyGui.MxFairyGuiTooltipOptions(
                MxFramework.UI.FairyGui.MxFairyGuiTooltipPlacement.Below, maximumTextWidth: 440, allowPin: false));
            target.onRollOver.Call();
            var window = Fgui.GRoot.inst.GetChildren().Last(child => child.name == "characterTestFrostedTooltip");
            Assert.NotNull(window); Assert.IsFalse(window.touchable);
            Assert.Less(window.width, 260, "A four-character hint must not expand to the configured maximum width.");
            var bottom = Fgui.GRoot.inst.GlobalToLocal(target.LocalToGlobal(new Vector2(0,target.height)));
            Assert.GreaterOrEqual(window.y, bottom.y, "A top-edge hint belongs below the button, outside its hit area.");
            tooltip.Bind(target, "", default); target.onRollOver.Call();
            Assert.IsFalse(window.visible, "Removing a redundant hint removes its hover binding and hides the old window.");
        }

        [Test]
        public void BattleHoverUsesPublishedFrostedParameterComponents()
        {
            var battle = Fgui.UIPackage.CreateObject(Package, "TacticalBattleHud").asCom;
            battle.name = "CharacterTest.TacticalBattleHud";
            Fgui.GRoot.inst.AddChild(battle);
            var source = RenderItem("deckList", 0).GetChild("face").asCom;
            var data = new CharacterTestCardUiPreviewData();
            var presentation = typeof(CharacterTestFairyGuiTacticalDescriptionHints).GetField("_data",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var hintsMap = (System.Collections.IDictionary)typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_cardHints",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_view);
            data = (CharacterTestCardUiPreviewData)presentation.GetValue(hintsMap[source]);
            var face = battle.GetChild("hand0").asCom;
            face.SetScale(.65f,.65f);face.SetXY(840,630);
            using var hints = new CharacterTestFairyGuiTacticalDescriptionHints(face, null, null, battle);
            hints.Bind(data, null, 23); face.onRollOver.Call();
            var windows = battle.GetChild("parameterWindows").asList;
            windows.EnsureBoundsCorrect(); Assert.Greater(windows.numChildren, 0);
            Assert.LessOrEqual(windows.y+windows.height*windows.scaleY,battle.GetChild("motionSurface").y-8+.01f,
                "Card details must leave the battle resource rail readable.");
            foreach (var item in windows.GetChildren())
            {
                Assert.That(item.packageItem.name, Does.StartWith("PreparationTactical"));
                Assert.NotNull(item.asCom.GetChild("background"));
                Assert.NotNull(item.asCom.GetChild("duration"));
            }
            hints.Dispose(); battle.Dispose();
        }

        [Test]
        public void PublishedBattleHudDirectPlayHidesConfirmAndMovesResolvingCardOutOfHand()
        {
            using var fixture = new TacticalDirectPlayTests.Fixture();
            var battle = Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;
            Fgui.GRoot.inst.AddChild(battle);
            using var view = new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);
            view.Sync(true);
            Assert.IsFalse(battle.GetChild("confirm").visible); Assert.IsFalse(battle.GetChild("cancel").visible);
            var card = battle.GetChild("hand0");
            Assert.AreEqual(63f/88f,card.width/card.height,.0001f);
            card.onClick.Call(); fixture.Pump.Drain(); fixture.Hud.TickDirectPlay(); fixture.Pump.Drain(); view.Sync(true);
            Assert.AreEqual(0,fixture.Port.Admissions,"A card click must not play it.");
            Assert.AreEqual(0,fixture.Pump.PendingCount);
            Assert.IsFalse(fixture.Hud.DirectPlayActive);
            Assert.AreEqual(CardZone.Hand,fixture.Hud.GetHandSlot(0).Zone);
            Assert.IsTrue(card.visible); Assert.IsFalse(battle.GetChild("executingCard").visible);
            Assert.That(battle.GetChild("inputHint").asTextField.text,Does.Not.Contain("点击"));
            Assert.IsTrue(fixture.Hud.TryDirectPlayHandSlot(0));
            fixture.Pump.Drain(); fixture.Hud.TickDirectPlay(); fixture.Pump.Drain(); view.Sync(true);
            Assert.AreEqual(1,fixture.Port.Admissions);
            Assert.IsFalse(card.visible); Assert.IsTrue(battle.GetChild("executingCard").visible);
            Assert.IsFalse(battle.GetChild("confirm").visible);
            view.Dispose(); battle.Dispose();
        }

        [Test]
        public void DisabledBattleHandShowsActualCooldownOnHoverAndClickWithoutDragging()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture(copies:2);
            long worldFrame=10;
            var ability=HandCooldownAbility(out var runtime);
            Assert.IsTrue(runtime.TryCommitActivation(1,ability,new RuntimeFrame(worldFrame),out _));
            fixture.Stage.Deck.DefinitionEvaluation=_=>HandCooldownEvaluation(runtime,ability,worldFrame);
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            try
            {
                using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
                var card=battle.GetChild("hand0");var second=battle.GetChild("hand1");
                Assert.IsTrue(card.touchable);Assert.IsFalse(fixture.Hud.CanDragHandSlot(0));
                string description=card.asCom.GetChild("description").text;
                card.onRollOver.Call();view.Sync(true);
                Assert.IsTrue(battle.GetChild("feedback").visible);
                Assert.That(battle.GetChild("feedback").text,Does.Contain("装配动作冷却中"));
                Assert.That(battle.GetChild("feedback").text,Does.Contain("2.0"));
                card.onRollOut.Call();view.Sync(true);
                card.onClick.Call();view.Sync(true);
                Assert.That(battle.GetChild("feedback").text,Does.Contain("2.0"));
                var begin=Pointer(battle,new Vector2(card.x+40,card.y+40));var end=Pointer(battle,new Vector2(960,500));
                card.onTouchBegin.Call(begin);card.onTouchMove.Call(end);card.onTouchEnd.Call(end);
                Assert.IsFalse(battle.GetChild("dragGhost").visible);
                Assert.AreEqual(0,fixture.Pump.PendingCount);Assert.AreEqual(0,fixture.Ledger.Reserved);
                Assert.AreEqual(0,fixture.Port.Admissions);Assert.AreEqual(50,fixture.Ledger.Current);
                Assert.AreEqual(description,card.asCom.GetChild("description").text);
                second.onRollOver.Call();view.Sync(true);
                Assert.AreEqual(fixture.Hud.GetHandSlotUseNotice(0),battle.GetChild("feedback").text,
                    "Two different cards share the real Ability owner query.");
            }
            finally{battle.Dispose();}
        }

        [Test]
        public void BattleHandNoticeRefreshesAtAuthorityFramesWithoutDefinitionOrStageRevisionChange()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture();
            long worldFrame=10;
            var ability=HandCooldownAbility(out var runtime);
            Assert.IsTrue(runtime.TryCommitActivation(1,ability,new RuntimeFrame(worldFrame),out _));
            fixture.Stage.Deck.DefinitionEvaluation=_=>HandCooldownEvaluation(runtime,ability,worldFrame);
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            try
            {
                using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
                var card=battle.GetChild("hand0");card.onRollOver.Call();view.Sync(true);
                ulong revision=fixture.Stage.SnapshotRevision;var definition=fixture.Hud.GetHandSlot(0).Definition;
                string paused=battle.GetChild("feedback").text;
                for(int renderFrame=0;renderFrame<120;renderFrame++)view.Sync(true);
                Assert.AreEqual(paused,battle.GetChild("feedback").text);
                Assert.AreEqual(60,runtime.GetRemainingCooldownFrames(1,ability,new RuntimeFrame(worldFrame)));
                // Advance only the fixture's authority frame. This is not a native world pause integration test.
                worldFrame+=15;view.Sync(true);
                Assert.That(battle.GetChild("feedback").text,Does.Contain("1.5"));
                Assert.AreEqual(revision,fixture.Stage.SnapshotRevision);Assert.AreSame(definition,fixture.Hud.GetHandSlot(0).Definition);
                worldFrame=70;view.Sync(true);
                Assert.That(battle.GetChild("feedback").text,Does.Not.Contain("冷却"));
                Assert.IsTrue(fixture.Hud.CanDragHandSlot(0));
                Assert.AreEqual(0,fixture.Pump.PendingCount);Assert.AreEqual(50,fixture.Ledger.Current);
                fixture.Hud.ShowDirectPlayIssue("动作数据校验失败");view.Sync(true);
                Assert.That(battle.GetChild("feedback").text,Does.Contain("动作数据校验失败"),
                    "Hovering a legal card must not hide existing execution failure feedback.");
            }
            finally{battle.Dispose();}
        }

        [TestCase(CardPlayFailure.EmptyActionSlot,"装配动作")]
        [TestCase(CardPlayFailure.BoundActionUnavailable,"冷却定义不可用")]
        [TestCase(CardPlayFailure.InsufficientMomentum,"动势不足")]
        public void DisabledBattleHandReasonIsVisibleWithoutSubmitting(CardPlayFailure failure,string expected)
        {
            using var fixture=new TacticalDirectPlayTests.Fixture(cost:80);
            if(failure!=CardPlayFailure.InsufficientMomentum)
                fixture.Stage.Deck.DefinitionEvaluation=_=>new CardPlayEvaluation(failure,0,0);
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            try
            {
                using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
                battle.GetChild("hand0").onClick.Call();view.Sync(true);
                Assert.IsTrue(battle.GetChild("feedback").visible);Assert.That(battle.GetChild("feedback").text,Does.Contain(expected));
                Assert.AreEqual(0,fixture.Pump.PendingCount);Assert.AreEqual(0,fixture.Ledger.Reserved);
            }
            finally{battle.Dispose();}
        }

        [Test]
        public void BattleHandInspectionDoesNotFollowAnotherCardIntoTheSameSlot()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture(copies:2);
            fixture.Stage.Deck.DefinitionEvaluation=_=>new CardPlayEvaluation(CardPlayFailure.BoundActionOnCooldown,0,0,302,60,30);
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            try
            {
                using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
                battle.GetChild("hand0").onClick.Call();view.Sync(true);
                Assert.That(battle.GetChild("feedback").text,Does.Contain("冷却"));
                using var replacement=new TacticalDirectPlayTests.Fixture();
                replacement.Stage.Deck.DefinitionEvaluation=_=>new CardPlayEvaluation(CardPlayFailure.BoundActionOnCooldown,0,0,302,60,30);
                fixture.Hud.Bind(replacement.Stage,replacement.Pump);view.Sync(true);
                Assert.IsFalse(battle.GetChild("feedback").visible);
                view.Sync(false);view.Sync(true);
                Assert.IsFalse(battle.GetChild("feedback").visible);
            }
            finally{battle.Dispose();}
        }

        private static MxFramework.Gameplay.AbilityDefinitionConfig HandCooldownAbility(out MxFramework.Gameplay.AbilityRuntimeModule runtime)
        {
            // Test-only definition; no production IDs or cost authority are allocated.
            var ability=new MxFramework.Gameplay.AbilityDefinitionConfig(71001,"fixture.hand-cooldown","Fixture",null,
                new MxFramework.Gameplay.AbilityCooldownConfig(60,0),Array.Empty<MxFramework.Gameplay.AbilityCostConfig>(),
                0,new MxFramework.Gameplay.AbilityActionBindingConfig(new MxFramework.CharacterAction.CharacterActionId(302),1),default,0,0);
            var registry=new MxFramework.Gameplay.AbilityDefinitionRegistry();Assert.IsTrue(registry.TryRegister(ability,out var issue),issue);
            runtime=new MxFramework.Gameplay.AbilityRuntimeModule(registry);return ability;
        }
        private static CardPlayEvaluation HandCooldownEvaluation(MxFramework.Gameplay.AbilityRuntimeModule runtime,
            MxFramework.Gameplay.AbilityDefinitionConfig ability,long worldFrame)
        {
            long remaining=runtime.GetRemainingCooldownFrames(1,ability,new RuntimeFrame(worldFrame));
            return new CardPlayEvaluation(remaining>0?CardPlayFailure.BoundActionOnCooldown:CardPlayFailure.None,0,0,302,remaining,30);
        }

        private static Fgui.InputEvent Pointer(Fgui.GComponent battle,Vector2 local)
        {
            var global=battle.LocalToGlobal(local);var input=new Fgui.InputEvent();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            typeof(Fgui.InputEvent).GetProperty("x",flags).SetValue(input,global.x);
            typeof(Fgui.InputEvent).GetProperty("y",flags).SetValue(input,global.y);
            typeof(Fgui.InputEvent).GetProperty("button",flags).SetValue(input,0);
            return input;
        }
        private static void AdmitDrop(TacticalDirectPlayTests.Fixture fixture)
        {
            for(int i=0;i<4;i++){fixture.Pump.Drain();fixture.Hud.TickDirectPlay();}
        }

        [Test]
        public void BattleDragReleaseUsesNativePointerEventsWithoutMovingOtherHandSlots()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture(copies:3);
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
            var card=battle.GetChild("hand0");var other=battle.GetChild("hand1");var position=other.xy;
            var begin=Pointer(battle,new Vector2(card.x+40,card.y+40));
            var end=Pointer(battle,new Vector2(960,500));
            card.onTouchBegin.Call(begin);card.onTouchMove.Call(end);
            Assert.AreEqual(0,fixture.Pump.PendingCount);Assert.AreEqual(0,fixture.Ledger.Reserved);
            var preview=battle.GetChild("dragGhost").asCom;
            Assert.IsTrue(preview.visible);Assert.IsFalse(preview.GetChild("background").visible);
            Assert.IsTrue(preview.GetChild("behaviorCardIconVisual").visible);
            card.onTouchEnd.Call(end);card.onTouchEnd.Call(end);
            Assert.AreEqual(1,fixture.Pump.PendingCount,"A rapid release submits exactly once.");
            AdmitDrop(fixture);view.Sync(true);
            Assert.AreEqual(1,fixture.Port.Admissions);
            Assert.AreEqual(position.x,other.x,"Remaining cards keep their horizontal slots.");
            Assert.AreEqual(position.y+6f,other.y,.001f,"The shared readiness lift relaxes when execution starts.");
            battle.Dispose();
        }

        [Test]
        public void BattleMomentumFeedbackAcceleratesThroughTheFinalQuarter()
        {
            float a=CharacterTestFairyGuiTacticalBattleView.MomentumProgress(.25f);
            float b=CharacterTestFairyGuiTacticalBattleView.MomentumProgress(.5f);
            float c=CharacterTestFairyGuiTacticalBattleView.MomentumProgress(.75f);
            float d=CharacterTestFairyGuiTacticalBattleView.MomentumProgress(1f);
            Assert.AreEqual(0f,CharacterTestFairyGuiTacticalBattleView.MomentumProgress(0f));
            Assert.AreEqual(1f,d);
            Assert.Greater(b-a,a);
            Assert.Greater(c-b,b-a);
            Assert.Greater(d-c,c-b,"The final quarter must accelerate instead of easing out.");
        }

        [UnityTest]
        public IEnumerator BattleTimeFeedbackTracksActualRateAndOnlyInvitesPlayAfterStopping()
        {
            float originalScale=Time.timeScale;
            Time.timeScale=1f;
            try
            {
                using var adapter=new UnityTacticalTimeAdapter(1f,1f,1f);
                using var fixture=new TacticalDirectPlayTests.Fixture(adapter:adapter);
                var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;
                Fgui.GRoot.inst.AddChild(battle);
                using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);
                view.Sync(true);
                Assert.AreEqual("正在减速",battle.GetChild("phaseName").asTextField.text);
                Assert.IsFalse(battle.GetChild("inputHint").visible);
                var card=battle.GetChild("hand0");float movingY=card.y;
                yield return new WaitForSecondsRealtime(.5f);
                adapter.TickTransition(.5f);view.Sync(true);
                Assert.AreEqual(.875f,fixture.Hud.WorldRateFraction,.001f);
                Assert.AreEqual(movingY-.75f,card.y,.001f);
                Assert.IsFalse(fixture.Hud.CanSelectHandSlot(0));
                yield return new WaitForSecondsRealtime(.5f);
                adapter.TickTransition(.5f);fixture.Stage.UpdatePauseTransition();view.Sync(true);
                Assert.AreEqual("选择行动",battle.GetChild("phaseName").asTextField.text);
                Assert.IsTrue(battle.GetChild("inputHint").visible);
                Assert.AreEqual(movingY-6f,card.y,.001f);
                Assert.IsTrue(fixture.Hud.TryDirectPlayHandSlot(0));
                AdmitDrop(fixture);view.Sync(true);
                Assert.AreEqual("正在加速",battle.GetChild("phaseName").asTextField.text);
                Assert.IsFalse(battle.GetChild("inputHint").visible);
                yield return new WaitForSecondsRealtime(1f);
                adapter.TickTransition(1f);view.Sync(true);
                Assert.AreEqual("执行中",battle.GetChild("phaseName").asTextField.text);
                fixture.Stage.ReportFault(TacticalStageRejection.ExecutionRejected);view.Sync(true);
                Assert.AreEqual("战斗暂停",battle.GetChild("phaseName").asTextField.text);
                Assert.IsFalse(battle.GetChild("inputHint").visible);
                view.Dispose();battle.Dispose();
            }
            finally {Time.timeScale=originalScale;}
        }

        [Test]
        public void BattleDraggingBackToHandOrOutsideScreenDoesNotReserveOrPlay()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture();
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
            var card=battle.GetChild("hand0");var begin=Pointer(battle,new Vector2(card.x+40,card.y+40));
            card.onTouchBegin.Call(begin);card.onTouchMove.Call(Pointer(battle,new Vector2(960,500)));
            card.onTouchEnd.Call(Pointer(battle,new Vector2(960,850)));
            Assert.AreEqual(0,fixture.Pump.PendingCount);Assert.AreEqual(0,fixture.Ledger.Reserved);
            card.onTouchBegin.Call(begin);card.onTouchEnd.Call(Pointer(battle,new Vector2(-500,-500)));
            AdmitDrop(fixture);
            Assert.AreEqual(0,fixture.Port.Admissions);Assert.AreEqual(50,fixture.Ledger.Current);
            battle.Dispose();
        }

        [Test]
        public void BattleInputFreeCardUsesFixedHudAndNeverPicksGround()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture();fixture.Port.GroundAvailable=false;
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
            var card=battle.GetChild("hand0");
            card.onTouchBegin.Call(Pointer(battle,new Vector2(card.x+40,card.y+40)));
            card.onTouchMove.Call(Pointer(battle,new Vector2(960,500)));
            var token=battle.GetChild("dragGhost");var tokenPosition=token.xy;
            var prompt=battle.GetChild("dragHintSurface");var promptPosition=prompt.xy;
            Assert.IsFalse(battle.GetChild("dropRing").visible);
            Assert.That(battle.GetChild("dragHint").asTextField.text,Does.Contain("松开使用"));
            var end=Pointer(battle,new Vector2(400,160));card.onTouchMove.Call(end);
            Assert.Less(Vector2.Distance(new Vector2(-560,-340),token.xy-tokenPosition),.1f,
                "The carried icon follows the pointer while the usage prompt stays in the HUD.");
            Assert.AreEqual(promptPosition,prompt.xy);
            Assert.AreEqual(0,fixture.Port.GroundPickCount,"An input-free card must not require a scene raycast.");
            Assert.AreEqual(0,fixture.Ledger.Reserved);card.onTouchEnd.Call(end);AdmitDrop(fixture);
            Assert.AreEqual(1,fixture.Port.Admissions);Assert.AreEqual(TacticalExecutionInputKind.None,fixture.Port.Input.Kind);
            Assert.IsFalse(battle.GetChild("dropRing").visible,"Use feedback belongs to the HUD, not the released scene position.");
            view.Dispose();battle.Dispose();
        }

        [Test]
        public void BattlePointDropUsesValidatedGroundAndRejectsOutOfRangeBeforeSelection()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture(point:true);
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
            var card=battle.GetChild("hand0");var begin=Pointer(battle,new Vector2(card.x+40,card.y+40));
            var end=Pointer(battle,new Vector2(960,500));
            fixture.Port.GroundPoint=new TacticalWorldPoint(6000,0,0);
            card.onTouchBegin.Call(begin);card.onTouchMove.Call(end);
            Assert.That(battle.GetChild("dragHint").asTextField.text,Does.Contain("不可释放"));
            card.onTouchEnd.Call(end);
            Assert.AreEqual(0,fixture.Pump.PendingCount);Assert.AreEqual(0,fixture.Ledger.Reserved);
            fixture.Port.GroundPoint=new TacticalWorldPoint(1200,0,400);
            var endScreen=new Vector2(end.x,Screen.height-end.y);
            Assert.IsTrue(fixture.Hud.TryPreviewCardPoint(0,endScreen,out _),"The read-only ground/input validation must accept this point.");
            Assert.IsFalse(view.ContainsScreenPoint(endScreen),"The proposed battlefield point must not be treated as UI.");
            card.onTouchBegin.Call(begin);card.onTouchMove.Call(end);
            Assert.That(battle.GetChild("dragHint").asTextField.text,Does.Contain("松开释放"));
            Assert.IsTrue(battle.GetChild("dropRing").visible);
            card.onTouchEnd.Call(end);
            Assert.AreEqual(1,fixture.Pump.PendingCount,"A valid release should submit selection.");
            AdmitDrop(fixture);
            Assert.AreEqual(1,fixture.Port.Admissions,"Phase="+fixture.Hud.Phase+" direct="+fixture.Hud.DirectPlayActive
                +" feedback="+fixture.Hud.CommandFeedbackMessage+" submission="+fixture.Hud.LastSubmission);
            Assert.AreEqual(fixture.Port.GroundPoint,fixture.Port.Input.Point);
            battle.Dispose();
        }

        [Test]
        public void BattleClosingDuringDragCancelsPreviewWithoutPlaying()
        {
            using var fixture=new TacticalDirectPlayTests.Fixture();
            var battle=Fgui.UIPackage.CreateObject(Package,"TacticalBattleHud").asCom;Fgui.GRoot.inst.AddChild(battle);
            using var view=new CharacterTestFairyGuiTacticalBattleView(fixture.Hud,battle);view.Sync(true);
            var card=battle.GetChild("hand0");
            card.onTouchBegin.Call(Pointer(battle,new Vector2(card.x+40,card.y+40)));
            card.onTouchMove.Call(Pointer(battle,new Vector2(960,500)));view.Sync(false);
            card.onTouchEnd.Call(Pointer(battle,new Vector2(960,500)));AdmitDrop(fixture);
            Assert.IsFalse(battle.GetChild("dragGhost").visible);Assert.AreEqual(0,fixture.Port.Admissions);
            Assert.AreEqual(0,fixture.Ledger.Reserved);battle.Dispose();
        }

        [Test]
        public void DeploymentFailureNotificationAppearsInPublishedPreparationPanel()
        {
            const string failure = "自动对战准备失败：测试角色 / 测试卡 / 动作 455：ActionSummary:ClosedContinuousVerifiedRangeRequired";
            _controller.ReportBattleIssue(failure);
            Bind();
            Assert.AreEqual(failure, _root.GetChild("toast").asTextField.text);
            Assert.IsTrue(_root.GetChild("toast").visible);
        }

        [Test]
        public void StartWithoutFlowIsDisabledAndReturnsUsefulFailure()
        {
            Bind();
            Assert.IsFalse(_root.GetChild("start").enabled);
            Assert.IsFalse(_controller.StartBattle());
            Bind();
            Assert.AreEqual("整备服务尚未准备就绪。", _root.GetChild("toast").asTextField.text);
        }

        [Test]
        public void UnstructuredCardKeepsSingleUsableBindingEntry()
        {
            _player.Catalog.Cards[0].PresentationSource = null;
            var item = RenderItem("deckList", 0);
            Assert.IsFalse(item.GetChild("face").asCom.GetChild("behaviorHints").visible);
            Assert.IsFalse(item.GetChild("action").visible);
            Assert.IsFalse(item.GetChild("action").touchable);
            item.onClick.Call();
            Assert.IsTrue(_root.GetChild("popupPanel").visible);
            var target = _preparation.Find(_entity).Loadout.Cards[0];
            RenderItem("popupList", 7).asButton.FireClick(false, true);
            Assert.AreEqual("action.7", _preparation.Find(_entity).Loadout.Bindings.Find(value => value.CardInstanceId == target.InstanceId).TacticalActionId);
        }

        [Test]
        public void InlinePickerCancelAndRepeatedCandidateClickDoNotRebind()
        {
            var item = RenderItem("deckList", 0); ClickInlineAction(item);
            var row = RenderItem("popupList", 7).asButton;
            var before = _preparation.Revision;
            Click(_root.GetChild("popupPanel").asCom, "cancel"); row.FireClick(false, true);
            Assert.AreEqual(before, _preparation.Revision);
            ClickInlineAction(item); row = RenderItem("popupList", 7).asButton;
            row.FireClick(false, true); var after = _preparation.Revision; row.FireClick(false, true);
            Assert.AreEqual(after, _preparation.Revision);
            Assert.IsFalse(item.GetChild("action").visible);
            Assert.IsFalse(item.GetChild("action").touchable);
            Assert.IsFalse(item.GetChild("remove").visible);
            item.onRollOver.Call(); Assert.IsTrue(item.GetChild("remove").visible);
            item.onRollOut.Call(); Assert.IsFalse(item.GetChild("remove").visible);
        }

        [UnityTest]
        public IEnumerator HoveredInlineSlotIsStageHitTargetAndOpensCorrectInstance()
        {
            _root.SetSize(Screen.width, Screen.height); _root.sortingOrder = 1000;
            Fgui.GRoot.inst.AddChild(_root); Bind(); yield return null;
            var deck = _root.GetChild("deckList").asList; deck.EnsureBoundsCorrect();
            var item = CardContent(deck.GetChildAt(0).asCom); var face = item.GetChild("face").asCom;
            face.onRollOver.Call(); yield return null;
            var slot = InlineAction(item); Assert.IsFalse(face.GetChild("focus").touchable);
            var point = slot.LocalToGlobal(new Vector2(slot.width / 2, slot.height / 2));
            var hit = Fgui.Stage.inst.HitTest(point, true);
            Assert.AreSame(slot.displayObject, hit, "Hover decoration must not intercept the real pointer hit target.");
            hit.BubbleEvent("onClick", null);
            Assert.IsTrue(_root.GetChild("popupPanel").visible, "Dispatch the hit target's real UI event, not the binding service.");
            Assert.AreEqual(8, _root.GetChild("popupList").asList.numItems);
            var target = _preparation.Find(_entity).Loadout.Cards[0]; var other = _preparation.Find(_entity).Loadout.Cards[1];
            RenderItem("popupList", 7).asButton.FireClick(false, true);
            Assert.AreEqual("action.7", _preparation.Find(_entity).Loadout.Bindings.Find(value => value.CardInstanceId == target.InstanceId).TacticalActionId);
            Assert.AreNotEqual("action.7", _preparation.Find(_entity).Loadout.Bindings.Find(value => value.CardInstanceId == other.InstanceId).TacticalActionId);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _view?.Dispose(); _root?.Dispose();
            if (_ownsPackage) Fgui.UIPackage.RemovePackage(Package);
            if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
            yield return new ExitPlayMode();
        }

        [Test]
        public void PublishedRefreshControlUsesAuthoredButtonAndRefusesUnavailableScene()
        {
            var refresh = _root.GetChild("topBar").asCom.GetChild("refreshCatalog").asButton;
            Assert.NotNull(refresh); Assert.AreEqual("刷新卡库", refresh.title);
            Assert.IsFalse(refresh.visible, "Refresh is grouped in More rather than occupying the main toolbar.");
            Click(_root.GetChild("topBar").asCom, "more");
            Assert.IsFalse(RenderItem("popupList", 0).enabled, "A controller without a preparation flow cannot refresh.");
            Assert.NotNull(refresh.GetChild("overBase")); Assert.NotNull(refresh.GetChild("downBase"));
        }

        [Test]
        public void CatalogRefreshInvalidatesLibraryWithoutChangingPreparationRevisionOrDeck()
        {
            _controller.OpenCardDrawer(); Bind();
            var library = _root.GetChild("libraryList").asList;
            int count = library.numItems;
            long revision = _preparation.Revision;
            string working = JsonConvert.SerializeObject(_preparation.Working);
            var cards = _player.Catalog.Cards.Concat(new[] { new CharacterTacticalCardDefinition {
                CardId = "published.fresh", Revision = 1, ContentHash = "fresh", DisplayName = "新发布卡", Slots = Array.Empty<string>() } }).ToArray();
            Assert.IsTrue(_player.TryRefreshPublishedCards(cards, out _, out var issue), issue);
            Bind();
            Assert.AreEqual(count, library.numItems, "Published catalog refresh must not grant unowned cards.");
            Assert.IsTrue(_player.TryGrantCardForTesting(_preparation.Find(_entity).CharacterStableId, Reference(cards.Last(), 0), out issue), issue);
            _view.Bind(_root, _controller, _preparation, _player);
            Assert.AreEqual(count + 1, library.numItems, "Only an explicit grant adds the new card to the library.");
            Assert.AreEqual(revision, _preparation.Revision);
            Assert.AreEqual(working, JsonConvert.SerializeObject(_preparation.Working));
        }

        [Test]
        public void PublishedCardUsesExactMetadataCostIconAndNeutralTypeWithoutExtraFooter()
        {
            var item = RenderItem("deckList", 0); var face = item.GetChild("face").asCom;
            Assert.AreEqual(63f / 88f, face.width / face.height, 0.00001f);
            Assert.AreEqual(378f, face.width); Assert.AreEqual(528f, face.height);
            Assert.AreEqual(1f, face.scaleX, 0.00001f); Assert.AreEqual(face.scaleX, face.scaleY);
            Assert.AreEqual(394f, item.width); Assert.AreEqual(594f, item.height);
            Assert.NotNull(face.GetChild("skinMask")); Assert.NotNull(face.GetChild("costPlate"));
            Assert.IsNull(face.GetChild("titleBar"));
            Assert.AreEqual(34, face.GetChild("name").asTextField.textFormat.size);
            Assert.GreaterOrEqual(face.GetChild("description").asTextField.textFormat.size, 30);
            Assert.IsFalse(face.GetChild("actionEngraving").visible); Assert.IsFalse(face.GetChild("utilityEngraving").visible);
            Assert.AreEqual("7", face.GetChild("cost").asTextField.text);
            Assert.AreEqual("行为卡 0", face.GetChild("name").asTextField.text);
            var reference = _preparation.Find(_entity).Loadout.Cards.First();
            var binding = _preparation.Find(_entity).Loadout.Bindings.Single(value => value.CardInstanceId == reference.InstanceId);
            var action = _player.Catalog.Action(binding.TacticalActionId);
            Assert.AreEqual("action.0", action.Id);
            var configuredAction = CharacterTestActionBrowserCatalog.Get().Actions.Single(value => value.ActionId == action.ActionId);
            Assert.That(face.GetChild("description").asTextField.text, Does.Contain(configuredAction.Name));
            Assert.IsTrue(face.GetChild("behaviorHints").visible, "Published source must drive the structured description.");
            Assert.IsFalse(face.GetChild("qualityDiamond").visible); Assert.IsFalse(face.GetChild("qualityCircle").visible);
            Assert.IsFalse(face.GetChild("icon").visible); Assert.IsNull(face.GetChild("footer"));
            Assert.IsFalse(item.draggable);
        }

        [Test]
        public void StaleHashDoesNotFallBackToAnotherDefinition()
        {
            _player.Catalog.Cards[0].ContentHash = "changed";
            var face = RenderItem("deckList", 0).GetChild("face").asCom;
            Assert.AreEqual("失效卡牌", face.GetChild("name").asTextField.text);
            Assert.AreEqual("—", face.GetChild("cost").asTextField.text);
            Assert.AreEqual("", face.GetChild("description").asTextField.text);
        }

        [Test]
        public void LibraryShowsAllCardsAndRepeatClickAddsDistinctInstances()
        {
            _controller.OpenCardDrawer(); Bind(); Assert.AreEqual(12, _root.GetChild("libraryList").asList.numItems);
            var item = RenderItem("libraryList", 0); int before = _preparation.Find(_entity).Loadout.Cards.Count;
            Click(item, "add"); Click(item, "add");
            var cards = _preparation.Find(_entity).Loadout.Cards;
            Assert.AreEqual(before + 2, cards.Count); Assert.AreNotEqual(cards[cards.Count - 1].InstanceId, cards[cards.Count - 2].InstanceId);
        }

        [Test]
        public void ActionPickerShowsAllEightCandidatesAndUpdatesOnlyTargetInstance()
        {
            var item = RenderItem("deckList", 0); ClickInlineAction(item);
            var list = _root.GetChild("popupList").asList; Assert.AreEqual(8, list.numItems);
            var target = _preparation.Find(_entity).Loadout.Cards[0]; var other = _preparation.Find(_entity).Loadout.Cards[1];
            var row = RenderItem("popupList", 7).asButton; row.FireClick(false, true);
            var bindings = _preparation.Find(_entity).Loadout.Bindings;
            Assert.AreEqual("action.7", bindings.Find(value => value.CardInstanceId == target.InstanceId).TacticalActionId);
            Assert.AreNotEqual("action.7", bindings.Find(value => value.CardInstanceId == other.InstanceId).TacticalActionId);
            Assert.IsFalse(_root.GetChild("popupPanel").visible);
        }

        [Test]
        public void RejectedBindingKeepsPickerAndShowsAuthorityError()
        {
            ClickInlineAction(RenderItem("deckList", 0)); var row = RenderItem("popupList", 7).asButton;
            _player.Catalog.Actions[7].CharacterStableIds = Array.Empty<string>(); row.FireClick(false, true);
            Assert.IsTrue(_root.GetChild("popupPanel").visible);
            Assert.IsNotEmpty(_root.GetChild("popupPanel").asCom.GetChild("issue").asTextField.text);
        }

        [Test]
        public void PresetNameValidationCancellationFailureAndRetryDoNotCreateDuplicates()
        {
            Click(_root.GetChild("topBar").asCom, "saveAs"); var popup = _root.GetChild("popupPanel").asCom;
            Click(popup, "confirm"); Assert.IsNotEmpty(popup.GetChild("issue").asTextField.text); Assert.AreEqual(0, _preparation.Presets.Count);
            popup.GetChild("input").asTextInput.text = "新预设"; _store.Fail = true;
            Click(popup, "confirm"); Click(popup, "confirm");
            Assert.IsTrue(popup.visible); Assert.AreEqual(1, _preparation.Presets.Count); Assert.IsTrue(_preparation.HasUnsavedChanges);
            _store.Fail = false; Click(popup, "confirm");
            Assert.IsFalse(popup.visible); Assert.AreEqual(1, _preparation.Presets.Count); Assert.IsFalse(_preparation.HasUnsavedChanges);
            Click(_root.GetChild("topBar").asCom, "saveAs"); Click(popup, "cancel"); Assert.AreEqual(1, _preparation.Presets.Count);
        }

        [Test]
        public void DirtyPresetSwitchRequiresChoiceAndRefreshesLoadedPreview()
        {
            Assert.IsTrue(_preparation.SaveAsPreset("first", out string first, out _));
            Assert.IsTrue(_preparation.AddCard(_entity, Reference(_player.Catalog.Cards[1], 0), out _, out _));
            Assert.IsTrue(_preparation.SaveAsPreset("second", out _, out _));
            Assert.IsTrue(_preparation.AddCard(_entity, Reference(_player.Catalog.Cards[2], 0), out _, out _));
            _controller.ToggleTop(); Bind(); Click(_root.GetChild("topBar").asCom, "preset");
            RenderItem("popupList", 1).asButton.FireClick(false, true);
            Assert.AreEqual(3, _root.GetChild("popupList").asList.numItems);
            Assert.AreEqual(4, _preparation.Find(_entity).Loadout.Cards.Count);
            RenderItem("popupList", 2).asButton.FireClick(false, true);
            Assert.AreEqual(4, _preparation.Find(_entity).Loadout.Cards.Count);
            Click(_root.GetChild("topBar").asCom, "preset");
            RenderItem("popupList", 1).asButton.FireClick(false, true);
            int refreshed = 0; _controller.Changed += () => refreshed++;
            RenderItem("popupList", 1).asButton.FireClick(false, true);
            Assert.AreEqual(first, _preparation.ActivePresetId);
            Assert.AreEqual(2, _preparation.Find(_entity).Loadout.Cards.Count);
            Assert.IsEmpty(_controller.SelectedEntityId); Assert.GreaterOrEqual(refreshed, 2);
        }

        [TestCase(1280, 720)] [TestCase(1920, 1080)] [TestCase(2560, 1440)]
        public void LayoutRefreshesWithoutDataChangeAndKeepsListsAndButtonsInside(int width, int height)
        {
            _root.SetSize(width, height); _controller.OpenCardDrawer(); _controller.ToggleTop(); Bind();
            Assert.IsNull(_root.GetChild("btnClose"), "The retired close control must not return in the published preparation panel.");
            foreach (string name in new[] { "deckList", "libraryList", "topBar" })
            {
                var control = _root.GetChild(name); Assert.GreaterOrEqual(control.x, 0); Assert.GreaterOrEqual(control.y, 0);
                Assert.LessOrEqual(control.x + control.width * control.scaleX, width + 1); Assert.LessOrEqual(control.y + control.height * control.scaleY, height + 1);
            }
            ClickInlineAction(RenderItem("deckList", 0)); var popup = _root.GetChild("popupPanel");
            Assert.GreaterOrEqual(popup.x, 0); Assert.LessOrEqual(popup.x + popup.width * popup.scaleX, width);
        }

        [UnityTest]
        public IEnumerator ActualConfiguredProducerAndPublishedPreparationRenderSameNativeCard()
        {
            const string aiPackage = "MxCharacterTestRuntimeAiLabUi";
            const string aiPrefix = "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi";
            bool ownsAiPackage = Fgui.UIPackage.GetByName(aiPackage) == null;
            Fgui.GComponent aiOwner = null, board = null;
            CharacterTestFairyGuiCardUiPreview preview = null;
            CharacterTestRuntimeAiPlannerDevSceneController devController = null;
            string configuredDirectory = null, preferenceKey = null, previousPreference = null;
            try
            {
                if (ownsAiPackage) Fgui.UIPackage.AddPackage(File.ReadAllBytes(aiPrefix + "_fui.bytes"), aiPrefix, LoadAtlas);
                var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
                Assert.IsTrue(published.Success, published.Diagnostic);
                var catalog = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTacticalContentCatalog>(
                    File.ReadAllText("Assets/StreamingAssets/MxFramework/CharacterTest/tactical_content.json"));
                CharacterTacticalActionEligibility.Load().Project(catalog, CharacterTestActionBrowserCatalog.Get());
                var sourceCard = published.CardCatalog.Cards.Where(value => value.StableCardId == "tactical.bound-action")
                    .OrderByDescending(value => value.Revision).First();
                catalog.SetPublishedCards(new[] { sourceCard });
                devController = _owner.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                devController.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(devController.SelectSetupCharacterId(618034977));
                var hero = CharacterTestPlayableCharacterCatalog.Discover().First(value => value.CharacterId == devController.CardDescriptionCharacterId);
                var selectedAction = catalog.Actions.First(value => value.ActionId == 301 && value.CharacterStableIds.Contains(hero.TacticalDefaults.CharacterStableId));
                var defaults = new CharacterTacticalDefaults { CharacterStableId = hero.TacticalDefaults.CharacterStableId,
                    ActionIds = new List<string> { selectedAction.Id }, Cards = new List<CharacterTacticalCardReference> {
                        new CharacterTacticalCardReference { InstanceId = 1, CardId = sourceCard.StableCardId, Revision = sourceCard.Revision,
                            ContentHash = sourceCard.ContentHash.ToString(System.Globalization.CultureInfo.InvariantCulture) } } };
                _view.Dispose();
                _player = AwaitForCapture(CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, new MemoryStore()));
                _preparation = AwaitForCapture(TacticalBattlePreparationService.OpenAsync(_player, new MemoryStore(), new[] { hero }));
                _entity = _preparation.Working.Entities.First().EntityId; _controller.Select(_entity);
                _view = new CharacterTestFairyGuiTacticalPreparationView();
                _root.SetSize(1920, 1080); ConfigureCaptureSize(new Vector2Int(1920, 1080));
                Fgui.GRoot.inst.AddChild(_root); Bind(); yield return null;
                var reference = _preparation.Find(_entity).Loadout.Cards.First();
                var definition = _player.Catalog.ExactCard(reference); Assert.NotNull(definition);
                var item = RenderItem("deckList", 0);
                var binding = _preparation.Find(_entity).Loadout.Bindings.Single(value => value.CardInstanceId == reference.InstanceId);
                var action = catalog.Action(binding.TacticalActionId); Assert.NotNull(action);
                configuredDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardUiPreviewTests", Guid.NewGuid().ToString("N")));
                var configuredStore = new CharacterTestConfiguredCardLibrary(Path.Combine(configuredDirectory, "cards"), Path.Combine(configuredDirectory, "recovery"));
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_testCardLibrary", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(devController, configuredStore);
                preferenceKey = "RuntimeAiLab.TestCard.Last." + devController.TestCardRole;
                previousPreference = PlayerPrefs.GetString(preferenceKey, "");
                var configured = new CharacterTestConfiguredCard { Id = Guid.NewGuid().ToString("N"), Name = sourceCard.Metadata.DisplayName,
                    Source = sourceCard.TacticalSource.ToJson(), BaseKey = sourceCard.StableCardId,
                    BaseRevision = sourceCard.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    BaseFingerprint = sourceCard.SemanticHash.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Metadata = new CharacterTestConfiguredCardMetadata { Cost = sourceCard.Metadata.Cost, IconKey = sourceCard.Metadata.IconKey },
                    Actions = new Dictionary<string, int> { [catalog.Cards.Single().ActionSlotPath] = action.ActionId } };
                Assert.IsTrue(configuredStore.Save(configured, 0, null, out string issue), issue);
                string savedHash = configuredStore.List(devController.TestCardRole).Single().FileHash;
                // Compare prepared input with prepared input. The default read-only preview
                // does not rebuild stale summaries, while the preparation UI explicitly does.
                Assert.IsTrue(devController.TryGetConfiguredCardUiPreview(configured.Id, null, out var data, out issue,
                    automaticallyPrepareSummaries: true), issue);
                Assert.IsTrue(data.Prepared, data.Notice);
                aiOwner = new Fgui.GComponent(); aiOwner.SetSize(1920, 1080); Fgui.GRoot.inst.AddChild(aiOwner);
                preview = new CharacterTestFairyGuiCardUiPreview(aiOwner, null, default);
                _root.visible = false;
                preview.Show(data, devController.TestCardRole, devController); yield return null;
                string directory = Path.GetFullPath(".agent/card-preview-wysiwyg/native-render"); Directory.CreateDirectory(directory);
                Capture(directory, new Vector2Int(1920, 1080), "01-ai-lab-existing-preview", false);
                var overlay = Fgui.GRoot.inst.GetChildren().OfType<Fgui.GComponent>()
                    .First(value => value.GetChild("panel")?.asCom?.GetChild("card") != null);
                var aiCard = overlay.GetChild("panel").asCom.GetChild("card").asCom;
                var prepCard = item.GetChild("face").asCom;
                foreach (string name in new[] { "name", "cost", "description" }) Assert.AreEqual(aiCard.GetChild(name).text, prepCard.GetChild(name).text, name);
                foreach (string name in new[] { "name", "cost", "description" })
                    Assert.AreEqual(aiCard.GetChild(name).asTextField.textFormat.font, prepCard.GetChild(name).asTextField.textFormat.font, name + " font");
                Assert.AreEqual(aiCard.GetChild("qualityDiamond").asImage.color, prepCard.GetChild("qualityDiamond").asImage.color);
                Assert.IsFalse(prepCard.GetChild("swordIllustration").visible, "Preparation uses a character-neutral pictogram over the approved shell.");
                Assert.AreEqual(aiCard.GetChild("behaviorHints").asList.numItems, prepCard.GetChild("behaviorHints").asList.numItems);
                aiCard.onRollOver.Call(); yield return null;
                var aiWindows = overlay.GetChild("parameterWindows").asList;
                Assert.IsTrue(aiWindows.visible, "Real AI Lab hover must show authored parameter windows.");
                aiWindows.EnsureBoundsCorrect();
                var aiWindowText = ParameterText(aiWindows);
                Capture(directory, new Vector2Int(1920, 1080), "04-ai-lab-parameter-hover", false);
                preview.Close();
                _root.visible = true; _controller.OpenCardDrawer(); Bind(); yield return null;
                Capture(directory, new Vector2Int(1920, 1080), "02-preparation-same-published-card");
                _root.GetChild("deckList").asList.EnsureBoundsCorrect();
                var visibleFace = CardContent(_root.GetChild("deckList").asList.GetChildAt(0).asCom).GetChild("face").asCom;
                Assert.AreEqual(aiCard.GetChild("behaviorHints").asList.numItems, visibleFace.GetChild("behaviorHints").asList.numItems,
                    "The frosted presentation preserves structured content while increasing its type size.");
                visibleFace.onRollOver.Call(); yield return null;
                var preparationWindows = _root.GetChild("parameterWindows").asList;
                Assert.IsTrue(preparationWindows.visible, "Formal card hover must show the shared native parameter windows.");
                preparationWindows.EnsureBoundsCorrect();
                CollectionAssert.AreEqual(aiWindowText, ParameterText(preparationWindows), "Same source/binding must produce the same parameter text.");
                Capture(directory, new Vector2Int(1920, 1080), "05-preparation-parameter-hover", false);
                visibleFace.onRollOut.Call(); preparationWindows.onRollOut.Call();
                yield return new WaitForSecondsRealtime(0.35f);
                Assert.IsFalse(preparationWindows.visible, "The shared hover layer must close after leaving the card.");
                Assert.AreEqual(savedHash, configuredStore.List(devController.TestCardRole).Single().FileHash);
                _root.visible = false;
                board = new Fgui.GComponent(); board.SetSize(1920, 1080); Fgui.GRoot.inst.AddChild(board);
                void Caption(string text, float x, float y, int size)
                {
                    var field = new Fgui.GTextField(); var format = field.textFormat;
                    format.font = "MxUI Sans SC"; format.size = size; format.color = new Color32(225, 223, 211, 255); field.textFormat = format;
                    field.SetXY(x, y); field.SetSize(1720, 70); field.text = text; board.AddChild(field);
                }
                Caption("原生 Unity/FairyGUI 同卡对照 · 只读发布内容 + 内存整备存储", 70, 32, 28);
                Caption("既有 AI Lab 预览（原组件 378×528）", 150, 132, 23);
                Caption("整备卡面（同一 recipe，原生尺寸）", 850, 132, 23);
                aiCard.RemoveFromParent(); board.AddChild(aiCard); aiCard.SetPivot(0, 0); aiCard.SetScale(1, 1); aiCard.SetXY(150, 206);
                board.AddChild(item); item.SetXY(850, 206);
                Caption($"{reference.CardId} @ r{reference.Revision} / hash {reference.ContentHash} / instance {reference.InstanceId}\n" +
                    $"费用 {definition.MotionCost} · 实际绑定 {action.DisplayName} · DisplayPrepared={data.Prepared}（不作为战斗准备验收）", 150, 806, 20);
                yield return null; Capture(directory, new Vector2Int(1920, 1080), "03-native-same-card-comparison", false);
                File.WriteAllText(Path.Combine(directory, "scope.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    reference.CardId, reference.Revision, reference.ContentHash, reference.InstanceId,
                    definition.DisplayName, definition.MotionCost, action = action.DisplayName,
                    nativeUnityRender = true, actualAiLabPreviewPresenter = true, actualPreparationPresenter = true,
                    storage = "MemoryStore", productionSaveTouched = false, osMouseTested = false, displayPrepared = data.Prepared,
                    realConfiguredProducer = true, configured.Id, configured.BaseKey, configured.BaseRevision, configured.BaseFingerprint,
                    characterId = hero.CharacterId, sourceSemanticHash = sourceCard.TacticalSource.SemanticHash,
                    configuredFileUnchanged = savedHash == configuredStore.List(devController.TestCardRole).Single().FileHash
                }, Newtonsoft.Json.Formatting.Indented));
            }
            finally
            {
                preview?.Dispose(); board?.Dispose(); aiOwner?.Dispose();
                if (preferenceKey != null) { if (string.IsNullOrEmpty(previousPreference)) PlayerPrefs.DeleteKey(preferenceKey); else PlayerPrefs.SetString(preferenceKey, previousPreference); }
                string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardUiPreviewTests")) + Path.DirectorySeparatorChar;
                if (configuredDirectory != null && configuredDirectory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(configuredDirectory)) Directory.Delete(configuredDirectory, true);
                if (ownsAiPackage) Fgui.UIPackage.RemovePackage(aiPackage);
            }
        }

        private static string[] InlineLayout(Fgui.GComponent card)
        {
            var layout = new List<string>();
            void Walk(Fgui.GObject item, string path)
            {
                if (!item.visible) return;
                string Metric(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                layout.Add(path + " xy=" + Metric(item.x) + "," + Metric(item.y)
                    + " size=" + Metric(item.width) + "," + Metric(item.height) + " text=" + (item.asTextField?.text ?? ""));
                if (item.asCom != null)
                    foreach (var child in item.asCom.GetChildren()) Walk(child, path + "/" + child.name);
            }
            foreach (var child in card.GetChild("behaviorHints").asCom.GetChildren()) Walk(child, "descriptionRows/");
            return layout.ToArray();
        }

        private static string[] ParameterText(Fgui.GComponent component)
        {
            var text = new List<string>();
            void Walk(Fgui.GComponent parent)
            {
                foreach (var child in parent.GetChildren())
                    if (child.visible)
                    {
                        if (child.asTextField != null) text.Add(child.name + "=" + child.text);
                        else if (child.asCom != null) Walk(child.asCom);
                    }
            }
            Walk(component); return text.ToArray();
        }

        private static T AwaitForCapture<T>(Task<T> task)
        {
            Assert.IsTrue(task.IsCompleted, "In-memory stores must complete synchronously; do not block Unity's context.");
            return task.GetAwaiter().GetResult();
        }

        private static void ConfigureCaptureSize(Vector2Int resolution)
        {
            var stageCamera = Fgui.StageCamera.main.GetComponent<Fgui.StageCamera>(); stageCamera.enabled = false;
            typeof(Fgui.StageCamera).GetMethod("OnScreenSizeChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(stageCamera, new object[] { resolution.x, resolution.y });
            Fgui.GRoot.inst.SetContentScaleFactor(1f);
        }

        [UnityTest]
        public IEnumerator PublishedPanelRendersSixStatesAtThreeResolutions()
        {
            Fgui.GRoot.inst.AddChild(_root);
            string directory = Path.GetFullPath(".agent/card-preview-wysiwyg/panel-render");
            Directory.CreateDirectory(directory);
            foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) })
            {
                _root.SetSize(resolution.x, resolution.y);
                var stageCamera = Fgui.StageCamera.main.GetComponent<Fgui.StageCamera>();
                // Keep the offscreen capture size stable across the real frame update.
                // Reapplying size after that update would reuse this frame's stale clip materials.
                stageCamera.enabled = false;
                typeof(Fgui.StageCamera).GetMethod("OnScreenSizeChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(stageCamera, new object[] { resolution.x, resolution.y });
                Fgui.GRoot.inst.SetContentScaleFactor(1f);
                _controller.Select(""); _controller.CollapseDrawer();
                if (_controller.TopExpanded) _controller.ToggleTop();
                Bind(); yield return null; Capture(directory, resolution, "01-collapsed");
                _controller.ToggleCharacterDrawer(); Bind(); yield return null; Capture(directory, resolution, "02-characters");
                _controller.CollapseDrawer(); _controller.Select(_entity); Bind(); yield return null; Capture(directory, resolution, "03-selected");
                _controller.OpenCardDrawer(); Bind(); yield return null; Capture(directory, resolution, "04-library");
                var deck = _root.GetChild("deckList").asList;
                deck.EnsureBoundsCorrect();
                Assert.Greater(deck.numChildren, 0, "Real virtual list must create visible cards.");
                ClickInlineAction(CardContent(deck.GetChildAt(0).asCom)); Bind(); yield return null; Capture(directory, resolution, "05-actions");
                Click(_root.GetChild("popupPanel").asCom, "close");
                _controller.Select(""); _controller.CollapseDrawer(); _controller.ToggleTop();
                Bind(); yield return null; Capture(directory, resolution, "06-presets");
            }
            File.WriteAllText(Path.Combine(directory, "scope.txt"),
                "Real published FairyGUI package + production preparation binder rendered by Unity/URP in isolated Play Mode. " +
                "Synthetic identity/cost/hash fixture and neutral backdrop. These are native UI render checks, not main-scene screenshots or OS mouse acceptance.\n");
        }

        private void Capture(string directory, Vector2Int resolution, string state, bool checkDeck = true)
        {
            var camera = Fgui.StageCamera.main;
            Assert.NotNull(camera);
            // Use FairyGUI's own screen-size path, including clipping and pixel scale.
            typeof(Fgui.StageCamera).GetMethod("OnScreenSizeChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(camera.GetComponent<Fgui.StageCamera>(), new object[] { resolution.x, resolution.y });
            Fgui.GRoot.inst.SetContentScaleFactor(1f);
            Fgui.Stage.inst.ForceUpdate();
            var target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousClear = camera.clearFlags; var previousBackground = camera.backgroundColor;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(42, 47, 43, 255);
                camera.aspect = (float)resolution.x / resolution.y;
                target.Create();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                else { camera.targetTexture = target; camera.Render(); camera.targetTexture = null; }
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0); texture.Apply();
                Assert.IsTrue(texture.GetPixels32().Any(pixel => Mathf.Abs(pixel.r - 42) > 24 || Mathf.Abs(pixel.g - 47) > 24), "Rendered image must contain UI pixels.");
                var deck = _root.GetChild("deckList").asList;
                File.WriteAllBytes(Path.Combine(directory, $"{resolution.x}x{resolution.y}-{state}.png"), texture.EncodeToPNG());
                File.WriteAllText(Path.Combine(directory, $"{resolution.x}x{resolution.y}-{state}.txt"),
                    $"frame={Time.frameCount} units={camera.GetComponent<Fgui.StageCamera>().unitsPerPixel} stageScale={Fgui.Stage.inst.cachedTransform.localScale} rootScale={Fgui.GRoot.inst.rootContainer.cachedTransform.localScale}\n" +
                    string.Join("\n", _root.displayObject.gameObject.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.sharedMaterial != null && renderer.sharedMaterial.IsKeywordEnabled("CLIPPED"))
                    .Take(12).Select(renderer => $"{renderer.name} pos={renderer.transform.position} scale={renderer.transform.lossyScale} clip=" + renderer.sharedMaterial.GetVector("_ClipBox"))));
                if (checkDeck && deck.visible && state != "05-actions")
                {
                    var face = CardContent(deck.GetChildAt(0).asCom).GetChild("face");
                    // The native card can scroll in a short viewport; sample its visible body margin.
                    var top = face.LocalToGlobal(Vector2.zero);
                    var clipBottom = deck.LocalToGlobal(new Vector2(0, deck.height)).y;
                    var localBottom = face.GlobalToLocal(new Vector2(top.x, clipBottom - 12)).y;
                    var point = face.LocalToGlobal(new Vector2(face.width * 0.965f, Mathf.Min(face.height * 0.88f, localBottom)));
                    var pixel = texture.GetPixel((int)point.x, resolution.y - 1 - (int)point.y);
                    Assert.Greater(pixel.r, 0.55f, "Published deck card body must render inside the native list clip.");
                }
            }
            finally
            {
                RenderTexture.active = previous; camera.clearFlags = previousClear; camera.backgroundColor = previousBackground;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            private string _json;
            internal bool Fail;
            public Task<string> LoadAsync() => Task.FromResult(_json);
            public Task SaveAsync(string value) { if (Fail) throw new InvalidOperationException("test storage failure"); _json = value; return Task.CompletedTask; }
            public Task ResetAsync(string value) => SaveAsync(value);
        }
    }
}

