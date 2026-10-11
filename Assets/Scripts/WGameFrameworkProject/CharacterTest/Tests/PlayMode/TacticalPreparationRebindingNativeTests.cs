using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    // Real scene publication and published UI. Persistence is isolated in memory.
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalPreparationRebindingNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameManager _manager;
        private CharacterTacticalServiceOpening _opening;
        private CharacterTacticalServiceSet _previous;
        private Mouse _mouse, _previousMouse;
        private float _scale;

        [UnityTest, Timeout(180000)]
        public IEnumerator RemoveThenPlaceTemplate_AbandonsRetiredBindAndRendersReplacement()
        {
            _scale = Time.timeScale;
            _opening = (CharacterTacticalServiceOpening)typeof(CharacterTacticalServices)
                .GetField("Opening", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _previous = _opening.Published;
            var setup = BuildServices();
            while (!setup.IsCompleted) yield return null;
            _opening.Publish(setup.GetAwaiter().GetResult());
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/TacticalCardDemoScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            for (int i = 0; i < 360 && _manager.RuntimeContext?.UiRuntime?.FairyGuiTacticalDeckPanelHost?.IsOpen != true; i++) yield return null;
            var controller = _manager.GetComponent<TacticalBattlePreparationController>();
            var service = CharacterTacticalServices.Preparation;
            Assert.NotNull(controller);
            Assert.IsNull(_opening.Published.Progress, "Fixture must never use file-backed player progress.");
            // Unity's test runner focuses the Editor. Placement deliberately retains the production focus gate.
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            UnityEditor.EditorWindow.GetWindow(gameView).Focus();
            for (int i = 0; i < 60 && !Application.isFocused; i++) yield return null;
            Assert.IsTrue(Application.isFocused, "Native placement requires the focused Editor.");
            _previousMouse = Mouse.current;
            _mouse = InputSystem.AddDevice<Mouse>(); _mouse.MakeCurrent();
            var friendly = service.Working.Entities.First(value => value.TeamId == 1);
            int character = friendly.CharacterId;
            var item = new MxFramework.Items.ItemInstanceId(friendly.CharacterItemInstanceId.Value);
            ValidateBindingBoundaries(controller, service);
            int placements = 0;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // First remove the selected friendly; then test removal of every entity, including the last one.
                var removals = cycle == 0 ? service.Working.Entities.Where(value => value.TeamId == 1).ToArray()
                    : service.Working.Entities.ToArray();
                foreach (var entity in removals)
                {
                    controller.Select(entity.EntityId);
                    controller.RemoveSelected();
                    Assert.AreEqual("", controller.SelectedEntityId);
                    Assert.IsNull(service.Find(entity.EntityId));
                    yield return null;
                }
                if (cycle > 0) Assert.AreEqual(0, service.Working.Entities.Count);
                controller.Select(""); controller.CollapseDrawer();
                var host = _manager.RuntimeContext.UiRuntime.FairyGuiTacticalDeckPanelHost;
                Assert.IsTrue(host.IsOpen);
                var binder = (CharacterTestFairyGuiTacticalDeckPanelBinder)typeof(CharacterTestFairyGuiTacticalDeckPanelHost)
                    .GetField("_binder", Private).GetValue(host);
                var view = (CharacterTestFairyGuiTacticalPreparationView)typeof(CharacterTestFairyGuiTacticalDeckPanelBinder)
                    .GetField("_view", Private).GetValue(binder);
                var root = (Fgui.GComponent)typeof(CharacterTestFairyGuiTacticalDeckPanelBinder).GetField("_root", Private).GetValue(binder);
                binder.Tick(); binder.Tick();
                Vector2 screen = FindPlacement(controller, view, root);
                var definition = CharacterTestPlayableCharacterCatalog.Discover().Single(value => value.CharacterId == character);
                Assert.IsTrue(CharacterTacticalServices.Player.TryReadCharacterItem(item, CharacterTacticalServices.InventoryOwner, out var ownedRole, out var roleIssue), roleIssue);
                typeof(CharacterTestFairyGuiTacticalPreparationView).GetMethod("BeginCharacterItemPlacement", Private)
                    .Invoke(view, new object[] { ownedRole, definition, true });
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
                InputSystem.Update();
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }); InputSystem.Update();
                Assert.IsTrue(_mouse.leftButton.wasReleasedThisFrame);
                int before = service.Working.Entities.Count;
                int generation = (int)typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_generation", Private).GetValue(view);
                // Original failure is synchronous inside this exact production Bind/placement/publication stack.
                Assert.DoesNotThrow(() => view.Bind(root, controller, service, CharacterTacticalServices.Player, CharacterTacticalServices.InventoryOwner, CharacterTacticalServices.RoleBindings));
                Assert.AreEqual(before + 1, service.Working.Entities.Count, "Placement must commit, not silently cancel.");
                Assert.Greater((int)typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_generation", Private).GetValue(view), generation);
                Assert.IsNull(typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_controller", Private).GetValue(view));
                Assert.IsNull(typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_preparation", Private).GetValue(view));
                Assert.IsTrue(root.isDisposed, "Session publication must retire the old published UI root.");
                Assert.IsNotEmpty(controller.SelectedEntityId);
                placements++;
                yield return null;
                host = _manager.RuntimeContext.UiRuntime.FairyGuiTacticalDeckPanelHost;
                Assert.IsTrue(host.IsOpen);
                var replacement = (CharacterTestFairyGuiTacticalDeckPanelBinder)typeof(CharacterTestFairyGuiTacticalDeckPanelHost)
                    .GetField("_binder", Private).GetValue(host);
                replacement.Tick(); replacement.Tick();
                var nextRoot = (Fgui.GComponent)typeof(CharacterTestFairyGuiTacticalDeckPanelBinder).GetField("_root", Private).GetValue(replacement);
                Assert.IsFalse(nextRoot.isDisposed);
                var selected = service.Find(controller.SelectedEntityId);
                Assert.NotNull(selected);
                Assert.AreEqual(selected.Loadout.Cards.Count, nextRoot.GetChild("deckList").asList.numItems);
                Assert.Greater(nextRoot.GetChild("deckList").asList.numItems, 0, "Replacement must still bind the real card content.");
                Assert.IsTrue(nextRoot.GetChild("entityHeader").visible);
                if (!ReferenceEquals(binder, replacement))
                    Assert.IsNull(typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_controller", Private).GetValue(view));
            }
            string directory = Path.GetFullPath("Library/TacticalPreparationRebinding");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "native-scope.json"), JsonConvert.SerializeObject(new {
                scene = "TacticalCardDemoScene", placements, removeSelectedFriendly = true, removeToZero = true,
                repeatedBind = true, realPublishedUi = true, replacementDeckContent = true,
                memoryStore = true, productionSaveTouched = false, osMouseAcceptance = false
            }, Formatting.Indented));
            // Unity Test Runner still fails on unexpected errors/exceptions; ordinary scene Info logs are expected.
            yield return new WaitForEndOfFrame();
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(directory, "native-ui.png"), screenshot.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(screenshot); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PreparationRemoval_IgnoresRightClickAndRequiresMoreConfirmation()
        {
            _scale = Time.timeScale;
            _opening = (CharacterTacticalServiceOpening)typeof(CharacterTacticalServices)
                .GetField("Opening", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _previous = _opening.Published;
            var setup = BuildServices();
            while (!setup.IsCompleted) yield return null;
            _opening.Publish(setup.GetAwaiter().GetResult());
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/TacticalCardDemoScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            for (int i = 0; i < 360 && _manager.RuntimeContext?.UiRuntime?.FairyGuiTacticalDeckPanelHost?.IsOpen != true; i++) yield return null;
            var controller = _manager.GetComponent<TacticalBattlePreparationController>();
            _manager.RuntimeContext.SessionBuilder.RebuildTacticalPreparationSession("MemoryStore removal input fixture");
            Assert.AreEqual(TacticalPreparationState.Preparation, _manager.GetComponent<TacticalCardSliceCoordinator>().State);
            var service = CharacterTacticalServices.Preparation;
            var selected = service.Working.Entities.First(value => value.TeamId == 1);
            controller.Select(selected.EntityId);
            var host = _manager.RuntimeContext.UiRuntime.FairyGuiTacticalDeckPanelHost;
            var binder = (CharacterTestFairyGuiTacticalDeckPanelBinder)typeof(CharacterTestFairyGuiTacticalDeckPanelHost)
                .GetField("_binder", Private).GetValue(host);
            binder.Tick();
            var root = (Fgui.GComponent)typeof(CharacterTestFairyGuiTacticalDeckPanelBinder).GetField("_root", Private).GetValue(binder);
            var view = (CharacterTestFairyGuiTacticalPreparationView)typeof(CharacterTestFairyGuiTacticalDeckPanelBinder)
                .GetField("_view", Private).GetValue(binder);
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            UnityEditor.EditorWindow.GetWindow(gameView).Focus();
            for (int i = 0; i < 60 && !Application.isFocused; i++) yield return null;
            Assert.IsTrue(Application.isFocused, "Native input requires the focused Editor.");
            _previousMouse = Mouse.current; _mouse = InputSystem.AddDevice<Mouse>(); _mouse.MakeCurrent();
            var screen = FindPlacement(controller, view, root);
            string snapshot = null;
            bool touchable = root.touchable;
            try
            {
                // Exercise the production Update route outside UI, rather than merely checking for removed source text.
                root.touchable = false;
                Assert.IsFalse((bool)typeof(TacticalBattlePreparationController).GetMethod("PointerOverFairyGui",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
                // Keep the remote shared hit-test/cursor route covered by real left-click and drag input.
                for (int i = 0; i < 360 && _manager.RuntimeContext.Slice.Services.CharacterEntities.Count < service.Working.Entities.Count; i++) yield return null;
                var configured = service.Find(selected.EntityId);
                int index = service.Working.Entities.FindIndex(value => value.EntityId == selected.EntityId);
                var runtime = (CharacterTestCharacterEntity)typeof(TacticalBattlePreparationController).GetMethod("ResolveRuntimeEntity",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {
                        configured, index, _manager.RuntimeContext.Slice.Services.CharacterEntities });
                Assert.NotNull(runtime);
                var camera = UnityEngine.Camera.main;
                Assert.NotNull(camera);
                var center = new Vector3(runtime.Transform.X, runtime.Transform.Y, runtime.Transform.Z)
                    + Vector3.up * runtime.Physics.EffectiveCenterY;
                Vector2 pick = camera.WorldToScreenPoint(center);
                var pickArgs = new object[] { pick, "", Vector3.zero };
                Assert.IsTrue((bool)typeof(TacticalBattlePreparationController).GetMethod("TryPickSceneEntity", Private).Invoke(controller, pickArgs));
                Assert.AreEqual(selected.EntityId, pickArgs[1]);
                var update = typeof(TacticalBattlePreparationController).GetMethod("Update", Private);
                controller.Select("");
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = pick }.WithButton(MouseButton.Left)); InputSystem.Update();
                update.Invoke(controller, null);
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = pick }); InputSystem.Update(); update.Invoke(controller, null);
                Assert.AreEqual(selected.EntityId, controller.SelectedEntityId, "Left click must still select the scene entity.");
                Vector3 target = configured.Position + new Vector3(1.2f, -configured.Position.y, 0);
                Vector2 drop = camera.WorldToScreenPoint(target);
                Assert.IsTrue(controller.CanPlaceTemplate(drop), "Drag target must be valid and free.");
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = pick }.WithButton(MouseButton.Left)); InputSystem.Update(); update.Invoke(controller, null);
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = drop }.WithButton(MouseButton.Left)); InputSystem.Update(); update.Invoke(controller, null);
                Assert.IsTrue((bool)typeof(TacticalBattlePreparationController).GetField("_dragging", Private).GetValue(controller));
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = drop }); InputSystem.Update(); update.Invoke(controller, null);
                Assert.Less(Vector3.Distance(target, service.Find(selected.EntityId).Position), .05f, "Left drag must commit the new pose.");
                Assert.AreEqual(selected.EntityId, controller.SelectedEntityId);
                snapshot = JsonConvert.SerializeObject(service.Working);
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Right));
                InputSystem.Update(); Assert.IsTrue(_mouse.rightButton.wasPressedThisFrame);
                typeof(TacticalBattlePreparationController).GetMethod("Update", Private).Invoke(controller, null);
                Assert.AreEqual(snapshot, JsonConvert.SerializeObject(service.Working));
                Assert.AreEqual(selected.EntityId, controller.SelectedEntityId);
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }); InputSystem.Update();
            }
            finally { root.touchable = touchable; }

            var more = root.GetChild("topBar").asCom.GetChild("more").asButton;
            StringAssert.Contains("确认", more.tooltips);
            Action openConfirmation = () =>
            {
                more.FireClick(false, true);
                var list = root.GetChild("popupList").asList;
                Assert.AreEqual(4, list.numItems);
                var row = list.GetFromPool(list.defaultItem);
                try
                {
                    list.itemRenderer(3, row);
                    Assert.AreEqual("移除选中角色…", row.asButton.title);
                    StringAssert.Contains("确认", row.tooltips);
                    row.asButton.FireClick(false, true);
                }
                finally { row.Dispose(); }
            };
            openConfirmation();
            var popup = root.GetChild("popupPanel").asCom;
            Assert.IsTrue(popup.visible);
            Assert.AreEqual("移除角色？", popup.GetChild("title").asTextField.text);
            Assert.AreEqual(snapshot, JsonConvert.SerializeObject(service.Working), "Opening confirmation must not remove anything.");
            popup.GetChild("cancel").asButton.FireClick(false, true);
            Assert.IsFalse(popup.visible);
            Assert.AreEqual(snapshot, JsonConvert.SerializeObject(service.Working), "Cancelling must retain the character and loadout.");
            Assert.AreEqual(selected.EntityId, controller.SelectedEntityId);
            openConfirmation();
            popup.GetChild("confirm").asButton.FireClick(false, true);
            Assert.IsNull(service.Find(selected.EntityId));
            Assert.AreEqual("", controller.SelectedEntityId);
            yield return null;
            Assert.IsTrue(_manager.RuntimeContext.UiRuntime.FairyGuiTacticalDeckPanelHost.IsOpen);
            Assert.IsNull(_opening.Published.Progress, "No file-backed progress is allowed in this fixture.");
        }

        private static void ValidateBindingBoundaries(TacticalBattlePreparationController controller,
            TacticalBattlePreparationService service)
        {
            var view = new CharacterTestFairyGuiTacticalPreparationView();
            var root = Fgui.UIPackage.CreateObject("MxCharacterTestTacticalDeckUi", "TacticalDeckPanel").asCom;
            root.SetSize(1920, 1080);
            try
            {
                // Services may be published after the UI handle is created. No partial binding is allowed.
                Assert.DoesNotThrow(() => view.Bind(root, controller, null, CharacterTacticalServices.Player));
                Assert.DoesNotThrow(() => view.Bind(root, null, service, CharacterTacticalServices.Player, CharacterTacticalServices.InventoryOwner, CharacterTacticalServices.RoleBindings));
                Assert.DoesNotThrow(() => view.Bind(root, controller, service, null));
                Assert.IsNull(typeof(CharacterTestFairyGuiTacticalPreparationView).GetField("_root", Private).GetValue(view));
                controller.Select(service.Working.Entities.First(value => value.TeamId == 1).EntityId);
                view.Bind(root, controller, service, CharacterTacticalServices.Player, CharacterTacticalServices.InventoryOwner, CharacterTacticalServices.RoleBindings);
                int cards = service.Find(controller.SelectedEntityId).Loadout.Cards.Count;
                Assert.Greater(cards, 0);
                Assert.AreEqual(cards, root.GetChild("deckList").asList.numItems);
                view.Bind(root, controller, service, CharacterTacticalServices.Player, CharacterTacticalServices.InventoryOwner, CharacterTacticalServices.RoleBindings);
                Assert.AreEqual(cards, root.GetChild("deckList").asList.numItems);
                Assert.IsTrue(root.GetChild("entityHeader").visible);
                view.Dispose(); root.Dispose();
                root = Fgui.UIPackage.CreateObject("MxCharacterTestTacticalDeckUi", "TacticalDeckPanel").asCom;
                root.SetSize(1920, 1080);
                view.Bind(root, controller, service, CharacterTacticalServices.Player, CharacterTacticalServices.InventoryOwner, CharacterTacticalServices.RoleBindings);
                Assert.AreEqual(cards, root.GetChild("deckList").asList.numItems, "A disposed view must bind a rebuilt root.");
                Assert.IsTrue(root.GetChild("entityHeader").visible);
            }
            finally { view.Dispose(); root.Dispose(); }
        }

        private static Vector2 FindPlacement(TacticalBattlePreparationController controller,
            CharacterTestFairyGuiTacticalPreparationView view, Fgui.GComponent root)
        {
            var inside = typeof(CharacterTestFairyGuiTacticalPreparationView).GetMethod("PointerInside", Private);
            for (float y = .3f; y < .9f; y += .1f)
                for (float x = .25f; x < .9f; x += .1f)
                {
                    var screen = new Vector2(Screen.width * x, Screen.height * y);
                    var point = new Vector2(screen.x, Screen.height - screen.y);
                    if (controller.CanPlaceTemplate(screen) && new[] { "librarySurface", "deckSurface", "entitySurface", "topBar" }
                        .All(name => !(bool)inside.Invoke(view, new object[] { root.GetChild(name), point }))) return screen;
                }
            Assert.Fail("No valid visible placement point found."); return default;
        }

        private static Task<CharacterTacticalServiceSet> BuildServices() =>
            TacticalNativeRoleFixture.Build(new MemoryStore(), new MemoryStore());

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _previousMouse?.MakeCurrent();
            if (_manager != null) UnityEngine.Object.Destroy(_manager.gameObject);
            yield return null;
            if (_opening != null) typeof(CharacterTacticalServiceOpening).GetProperty("Published", Private).SetValue(_opening, _previous);
            Time.timeScale = _scale;
        }
        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            private string _value;
            public Task<string> LoadAsync() => Task.FromResult(_value);
            public Task SaveAsync(string value) { _value = value; return Task.CompletedTask; }
            public Task ResetAsync(string value) => SaveAsync(value);
        }
    }
}
