using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    /// <summary>Published scene and UI with in-memory services; bypasses the persistence checkpoint entry.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalEnemyCardNativeAcceptanceTests
    {
        private GameManager _manager;
        private CharacterTacticalServiceOpening _opening;
        private CharacterTacticalServiceSet _previous;
        private float _scale;
        private readonly MemoryStore _playerStore = new();
        private readonly MemoryStore _preparationStore = new();
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest, Timeout(120000)]
        public IEnumerator PublishedEnemyMarkersPopupAndCleanup_UseMemoryStoreAndPreserveAttackTarget()
        {
            _scale = Time.timeScale;
            Assert.IsTrue(Application.isPlaying);
            var openingField = typeof(CharacterTacticalServices).GetField("Opening", BindingFlags.Static | BindingFlags.NonPublic);
            _opening = (CharacterTacticalServiceOpening)openingField.GetValue(null);
            _previous = _opening.Published;
            var setup = BuildServices();
            while (!setup.IsCompleted) yield return null;
            _opening.Publish(setup.GetAwaiter().GetResult());
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/TacticalCardDemoScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("This fixture consumes repository Editor content.");
#endif
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            for (int i = 0; i < 240 && _manager.RuntimeContext?.Slice?.Services.CharacterEntities.Count !=
                 CharacterTacticalServices.Preparation.Working.Entities.Count; i++) yield return null;
            var flow = _manager.GetComponent<TacticalCardSliceCoordinator>();
            _manager.RuntimeContext.SessionBuilder.RebuildTacticalPreparationSession("MemoryStore enemy presentation fixture");
            for (int i = 0; i < 360 && _manager.RuntimeContext.Slice.Services.CharacterEntities.Count !=
                 CharacterTacticalServices.Preparation.Working.Entities.Count; i++) yield return null;
            Assert.IsTrue(TacticalDeployment.TryCreate(CharacterTacticalServices.Preparation.Working, out var deployment, out var issue), issue);
            // Invoke the existing session publication core, without calling DeployPreparedAsync/SaveProgressAsync.
            var arguments = new object[] { deployment, null };
            Assert.IsTrue((bool)typeof(TacticalCardSliceCoordinator).GetMethod("TryDeployPreparedCore", PrivateInstance).Invoke(flow, arguments), arguments[1] as string);
            for (int i = 0; i < 360 && _manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage?.Stage.Phase !=
                 TacticalStagePhase.Planning; i++) yield return null;
            var mixed = _manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage;
            Assert.NotNull(mixed); Assert.AreEqual(TacticalStagePhase.Planning, mixed.Stage.Phase);
            Assert.AreEqual(0f, Time.timeScale);
            var hud = _manager.GetComponent<TacticalCardHud>();
            for (int i = 0; i < 6 && flow.State != TacticalPreparationState.Battle; i++) yield return null;
            // Initial Ready may precede the first AI evaluation. Show an actual selected runtime card,
            // then freeze its clock rather than inventing a pending entry before an agent exists.
            int actionSlot = -1;
            for (int i = 0; i < 3; i++) if (hud.GetHandSlot(i)?.Definition.Kind == TacticalCardKind.Action) { actionSlot = i; break; }
            Assert.GreaterOrEqual(actionSlot, 0);
            Assert.IsTrue(hud.TrySelectHandSlot(actionSlot)); yield return null;
            Assert.IsTrue(hud.TryConfirmExecution());
            for (int i = 0; i < 6; i++) yield return null;
            // Use the gameplay pause owner: GameManager reapplies that ownership every Update.
            _manager.RuntimeContext.MainMenuCoordinator.SetPaused(true);
            yield return null;
            Assert.IsTrue(hud.IsWorldPaused);
            long frozenFrame = _manager.RuntimeContext.Slice.CurrentFrame.Value;
            var view = (CharacterTestFairyGuiTacticalBattleView)typeof(TacticalCardHud).GetField("_fairyBattleView", PrivateInstance).GetValue(hud);
            Assert.NotNull(view); view.Sync(true); view.RefreshBattleOverlayAfterCamera();
            var enemies = (IList)typeof(CharacterTestFairyGuiTacticalBattleView).GetField("_visibleEnemies", PrivateInstance).GetValue(view);
            Assert.GreaterOrEqual(enemies.Count, 2, "The fixture supplies two independent enemy actors.");
            var target = mixed.CurrentTarget;
            var first = enemies[0]; var second = enemies[1];
            Fgui.GComponent Marker(object enemy) => (Fgui.GComponent)enemy.GetType().GetField("Marker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
            Assert.IsFalse(Marker(first).position == Marker(second).position);
            var viewed = typeof(CharacterTestFairyGuiTacticalBattleView).GetField("_viewedEnemy", PrivateInstance);
            var shownData = typeof(CharacterTestFairyGuiTacticalBattleView).GetField("_shownEnemyData", PrivateInstance);
            object Data(object enemy) => enemy.GetType().GetField("Data", PrivateInstance).GetValue(enemy);
            string Progress(object enemy)
            {
                var facts = (CharacterTestEnemyCardPresentation)enemy.GetType().GetField("Facts", PrivateInstance).GetValue(enemy);
                var data = (CharacterTestCardUiPreviewData)Data(enemy);
                return facts.EntryId + ":" + facts.State + ":" + facts.Pending + "|" + string.Join("|",
                    (data.Segments ?? Array.Empty<CharacterTestTacticalDescriptionSegment>())
                    .Select(segment => facts.Intent(segment) + ":" + facts.RemainingDelayFrames(segment)));
            }
            Marker(first).onClick.Call();
            var popup = view.Root.GetChild("enemyCard").asCom;
            Assert.IsTrue(popup.visible, "Click must show the full card without waiting for another camera pass.");
            Assert.AreSame(first, viewed.GetValue(view)); Assert.AreSame(Data(first), shownData.GetValue(view));
            Assert.AreSame(target, mixed.CurrentTarget); view.Sync(true);
            popup.GetChild("pin").onClick.Call(); view.Sync(true);
            Marker(second).onClick.Call();
            Assert.IsTrue(popup.visible); Assert.AreSame(target, mixed.CurrentTarget);
            Assert.AreSame(second, viewed.GetValue(view)); Assert.AreSame(Data(second), shownData.GetValue(view));
            string firstProgress = Progress(first), secondProgress = Progress(second);
            for (int i = 0; i < 3; i++)
            {
                yield return null; view.Sync(true); view.RefreshBattleOverlayAfterCamera();
                Assert.IsTrue(popup.visible); Assert.AreSame(second, viewed.GetValue(view));
                Assert.AreEqual(firstProgress, Progress(first)); Assert.AreEqual(secondProgress, Progress(second));
                Assert.AreEqual(frozenFrame, _manager.RuntimeContext.Slice.CurrentFrame.Value);
            }
            Assert.AreSame(target, mixed.CurrentTarget); Assert.AreEqual(0f, Time.timeScale);
            var directory = Path.GetFullPath("Library/MxFramework/EnemyCardNativeAcceptance");
            Directory.CreateDirectory(directory);
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(popup.visible, "Capture only an actually visible full card.");
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(directory, "published-two-enemies.png"), screenshot.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(screenshot); }
            view.Sync(false);
            Assert.IsFalse(popup.visible); Assert.IsNull(viewed.GetValue(view));
            view.Sync(true); view.RefreshBattleOverlayAfterCamera(); Assert.GreaterOrEqual(enemies.Count, 2);
            Assert.IsFalse(popup.visible, "A reopened HUD must discard the old pin and hover.");
            Assert.IsNull(_opening.Published.Progress, "No file-backed progress/checkpoint may exist in this fixture.");
            File.WriteAllText(Path.Combine(directory, "scope.json"), JsonConvert.SerializeObject(new {
                memoryStore = true, productionSaveTouched = false, checkpoints = false,
                scene = "TacticalCardDemoScene", publishedMarkers = enemies.Count,
                popup = true, pinAndReselectKeepAttackTarget = true, reopenClearsPin = true,
                paused = Time.timeScale == 0, playerMemoryWrites = _playerStore.Writes,
                preparationMemoryWrites = _preparationStore.Writes,
                clickBindsImmediately = true, pausedPresentationFactsUnchanged = true,
                osHover = false, visualNotCovered = new[] { "countdown", "death", "offscreen", "cardChange" }
            }, Formatting.Indented));
        }

        private Task<CharacterTacticalServiceSet> BuildServices() =>
            TacticalNativeRoleFixture.Build(_playerStore, _preparationStore, secondOpponent: true);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_manager != null) UnityEngine.Object.Destroy(_manager.gameObject);
            yield return null;
            if (_opening != null)
                typeof(CharacterTacticalServiceOpening).GetProperty("Published", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_opening, _previous);
            Time.timeScale = _scale;
        }
        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            private string _value;
            internal int Writes;
            public Task<string> LoadAsync() => Task.FromResult(_value);
            public Task SaveAsync(string value) { Writes++; _value = value; return Task.CompletedTask; }
            public Task ResetAsync(string value) => SaveAsync(value);
        }
    }
}
