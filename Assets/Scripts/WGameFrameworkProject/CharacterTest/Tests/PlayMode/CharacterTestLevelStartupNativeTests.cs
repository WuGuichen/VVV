using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    /// <summary>Formal scene/levels and production progress services, with an isolated Editor save root.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestLevelStartupNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private GameManager _manager;
        private TacticalCardSliceCoordinator _coordinator;
        private CharacterTacticalServiceOpening _opening;
        private CharacterTacticalServiceSet _previousServices;
        private object _previousProgress, _previousOpening;
        private string _previousRoot;
        private string _previousIssue;
        private float _scale;
        private string _runtimeRoot;
        private byte[] _directoryBefore;
        private HashSet<string> _documentsBefore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scale = Time.timeScale; Time.timeScale = 1f;
            _runtimeRoot = Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/Levels");
            _directoryBefore = File.ReadAllBytes(Path.Combine(_runtimeRoot, "directory.json"));
            _documentsBefore = new HashSet<string>(Directory.GetFiles(Path.Combine(_runtimeRoot, "documents"), "*.level.json"));
            _previousRoot = Environment.GetEnvironmentVariable("MX_CHARACTERTEST_TACTICAL_SAVE_ROOT");
            Environment.SetEnvironmentVariable("MX_CHARACTERTEST_TACTICAL_SAVE_ROOT",
                Path.Combine(Path.GetTempPath(), "mx749-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            _opening = (CharacterTacticalServiceOpening)typeof(CharacterTacticalServices).GetField("Opening", StaticPrivate).GetValue(null);
            _previousServices = _opening.Published;
            _previousProgress = typeof(CharacterTacticalServices).GetField("_progress", StaticPrivate).GetValue(null);
            _previousOpening = typeof(CharacterTacticalServiceOpening).GetField("_opening", Private).GetValue(_opening);
            _previousIssue = _opening.Issue;
            typeof(CharacterTacticalServiceOpening).GetProperty("Issue", Private).SetValue(_opening, string.Empty);
            typeof(CharacterTacticalServiceOpening).GetProperty("Published", Private).SetValue(_opening, null);
            typeof(CharacterTacticalServiceOpening).GetField("_opening", Private).SetValue(_opening, null);
            typeof(CharacterTacticalServices).GetField("_progress", StaticPrivate).SetValue(null, null);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/TacticalCardDemoScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            _coordinator = _manager.GetComponent<TacticalCardSliceCoordinator>();
            // Scene activation completes before MonoBehaviour.Start; wait for the real opening owner.
            for (int i = 0; i < 900 && CharacterTacticalServices.Preparation == null
                && string.IsNullOrEmpty(CharacterTacticalServices.Issue); i++) yield return null;
            Assert.NotNull(CharacterTacticalServices.Preparation, CharacterTacticalServices.Issue);
            yield return WaitForIdle();
            Assert.AreEqual(2, CharacterTacticalServices.Preparation.Working.Entities.Count, "Use the real two-entity new-game recipe.");
            Assert.AreEqual(string.Empty, _manager.InitialLevelId);
        }

        private IEnumerator WaitForIdle()
        {
            for (int i = 0; i < 900 && (_coordinator.IsBusy || _coordinator.Loading.Running); i++) yield return null;
            Assert.IsFalse(_coordinator.IsBusy);
            Assert.AreEqual(TacticalLoadingStatus.Hidden, _coordinator.Loading.Status, _coordinator.StatusMessage);
        }

        private MxLevelAuthoringDocument Load(string id)
        {
            var catalog = CharacterTestLevelCatalog.Read(Path.Combine(_runtimeRoot, "directory.json"));
            var document = catalog.Load(id, _runtimeRoot, true, out var source);
            // The real Play button explicitly preflights/publishes current action pins. Mirror that
            // publication with the production compiler/generator, then restore exact assets in teardown.
            using (var candidate = _manager.RuntimeContext.SessionBuilder.PrepareLevelSession(document, source, publishPins: true))
            {
                document = candidate.Document;
                var entry = catalog.Levels.Single(level => level.LevelId == id);
                if (entry.DocumentHash != CharacterTestLevelCatalog.Hash(MxLevelAuthoringJson.SaveToJson(document)))
                    CharacterTestLevelCatalog.Publish(document, entry.DisplayName, _runtimeRoot);
            }
            _manager.LoadLevel(id);
            return document;
        }

        private void AssertLevel(MxLevelAuthoringDocument document, GameSlice slice, ulong run)
        {
            var context = _manager.RuntimeContext;
            Assert.AreSame(slice, context.Slice);
            Assert.AreEqual(document.LevelId, context.ActiveLevelId);
            Assert.AreEqual(MxLevelAuthoringJson.ComputeContentHash(document), context.ActiveLevelContentHash);
            Assert.NotNull(context.ActiveLevelWorldCache);
            Assert.AreEqual(run, slice.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId);
            Assert.AreEqual(TacticalPreparationState.Battle, _coordinator.State);
            Assert.IsFalse(context.MainMenuVisible);
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(document, out var actors, out _);
            var enabled = actors.Characters.Where(actor => actor.Enabled).ToArray();
            Assert.AreEqual(enabled.Length, slice.Services.CharacterEntities.Count);
            foreach (var actor in enabled)
            {
                var entity = slice.Services.CharacterRuntime.FindLevelActor(actor.StableId);
                Assert.NotNull(entity, actor.StableId);
                Assert.AreEqual(actor.TeamId, entity.Team.TeamId);
            }
            Assert.AreEqual(enabled.Length, _coordinator.BattleDeployment.Participants.Count);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ThreeAndTwoActorLevelsKeepPublishedIdentityAndExactOverlaysAfterDelayedOpening()
        {
            foreach (string id in new[] { "formal.sword.crossfire", "formal.sword.courtyard" })
            {
                var document = Load(id);
                var slice = _manager.RuntimeContext.Slice;
                var run = slice.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId;
                var deployment = _coordinator.BattleDeployment;
                var commands = slice.Services.Modules.RuntimeAiPlannerCommands;
                var overlays = (Array)commands.GetType().GetField("_behaviorLogicOverlays", Private).GetValue(commands);
                var installed = (Array)overlays.Clone();
                var delayed = new TaskCompletionSource<bool>();
                var opening = _coordinator.OpenInitialAsync(() => delayed.Task);
                for (int i = 0; i < 12; i++) yield return null;
                delayed.SetResult(true);
                while (!opening.IsCompleted) yield return null;
                opening.GetAwaiter().GetResult();
                for (int i = 0; i < 120 && slice.Services.Modules.MixedPlayerTacticalStage.Stage.Phase != TacticalStagePhase.Planning; i++) yield return null;
                AssertLevel(document, slice, run);
                Assert.AreSame(deployment, _coordinator.BattleDeployment, "Keep the exact frozen level deployment.");
                CollectionAssert.AreEqual(installed, overlays, "Do not reinstall ordinary auto-battle groups over authored conditions/bindings.");
                Assert.AreEqual(TacticalStagePhase.Planning, slice.Services.Modules.MixedPlayerTacticalStage.Stage.Phase);
                Assert.AreEqual(3, _manager.GetComponent<TacticalCardHud>().HandCount);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LateSuccessFailureAndCancellationCannotPublishOldPreparationAfterLevelHandoff()
        {
            foreach (int outcome in new[] { 0, 1, 2 })
            {
                _manager.RuntimeContext.SessionBuilder.RebuildTacticalPreparationSession();
                var old = _manager.RuntimeContext.Slice;
                var delayed = new TaskCompletionSource<bool>();
                var opening = _coordinator.OpenInitialAsync(() => delayed.Task);
                long attempt = _coordinator.Loading.Attempt;
                var document = Load("formal.sword.crossfire");
                var slice = _manager.RuntimeContext.Slice;
                var run = slice.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId;
                Assert.AreNotSame(old, slice);
                Assert.Greater(_coordinator.Loading.Attempt, attempt);
                if (outcome == 0) delayed.SetResult(true);
                else if (outcome == 1) delayed.SetException(new IOException("Delayed progress failure"));
                else delayed.SetCanceled();
                while (!opening.IsCompleted) yield return null;
                opening.GetAwaiter().GetResult();
                AssertLevel(document, slice, run);
                Assert.AreEqual(TacticalLoadingStatus.Hidden, _coordinator.Loading.Status);
                Assert.IsFalse(_coordinator.IsBusy);
                Assert.AreEqual(string.Empty, _coordinator.StatusMessage);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CurrentOpeningFailureKeepsLevelAndRetryDoesNotRebuildIt()
        {
            var document = Load("formal.sword.crossfire");
            var slice = _manager.RuntimeContext.Slice;
            ulong run = slice.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId;
            var opening = _coordinator.OpenInitialAsync(() => Task.FromException(new IOException("Isolated opening failure")));
            while (!opening.IsCompleted) yield return null;
            AssertLevel(document, slice, run);
            Assert.AreEqual(TacticalLoadingStatus.Failed, _coordinator.Loading.Status);
            StringAssert.Contains("Isolated opening failure", _coordinator.StatusMessage);
            Assert.IsTrue(_coordinator.RetryLoading());
            yield return WaitForIdle();
            AssertLevel(document, slice, run);
            Assert.AreEqual(string.Empty, _coordinator.StatusMessage);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator OrdinaryHomeContinueAndNewProgressStillUsePreparation()
        {
            Assert.AreEqual(TacticalPreparationState.Home, _coordinator.State);
            Assert.IsTrue(_manager.RuntimeContext.MainMenuVisible);
            string profile = CharacterTacticalServices.ActiveProfileId;
            _coordinator.RequestStartOrContinue();
            yield return WaitForIdle();
            Assert.AreEqual(TacticalPreparationState.Preparation, _coordinator.State);
            Assert.AreEqual(profile, CharacterTacticalServices.ActiveProfileId);
            for (int i = 0; i < 120 && _manager.RuntimeContext.Slice.Services.CharacterEntities.Count != 2; i++) yield return null;
            Assert.AreEqual(2, _manager.RuntimeContext.Slice.Services.CharacterEntities.Count);
            Assert.AreEqual(string.Empty, _manager.RuntimeContext.ActiveLevelId);
            _coordinator.RequestCloseOrExit();
            yield return WaitForIdle();
            Assert.AreEqual(TacticalPreparationState.Home, _coordinator.State);
            _coordinator.RequestNewGameOrRetry();
            yield return WaitForIdle();
            Assert.AreEqual(TacticalPreparationState.Preparation, _coordinator.State);
            Assert.AreNotEqual(profile, CharacterTacticalServices.ActiveProfileId);
            Assert.AreEqual(2, CharacterTacticalServices.Preparation.Working.Entities.Count);
            Assert.AreEqual(string.Empty, _manager.RuntimeContext.ActiveLevelId);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedSelectedLevelReportsFailureWithoutPublishingPreparation()
        {
            var original = _manager.RuntimeContext.Slice;
            Assert.Throws<InvalidDataException>(() => _manager.LoadLevel("issue749.missing"));
            _manager.SetInitialLevelId("issue749.missing");
            var opening = _coordinator.OpenInitialAsync(() => Task.CompletedTask);
            while (!opening.IsCompleted) yield return null;
            Assert.AreSame(original, _manager.RuntimeContext.Slice);
            Assert.AreEqual(TacticalLoadingStatus.Failed, _coordinator.Loading.Status);
            StringAssert.Contains("所选关卡未成功发布", _coordinator.StatusMessage);
            _manager.SetInitialLevelId(string.Empty);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CrossfireHudPlaysCardAndMixedOutcomeReachesCoordinatorAndLevelRematch()
        {
            var document = Load("formal.sword.crossfire");
            var slice = _manager.RuntimeContext.Slice;
            var mixed = slice.Services.Modules.MixedPlayerTacticalStage;
            var hud = _manager.GetComponent<TacticalCardHud>();
            for (int i = 0; i < 600 && mixed.Stage.Phase != TacticalStagePhase.Planning; i++) yield return null;
            Assert.AreEqual(TacticalStagePhase.Planning, mixed.Stage.Phase);
            for (int i = 0; i < 120 && !mixed.Stage.Time.IsWorldPaused; i++) yield return null;
            Assert.IsTrue(mixed.Stage.Time.IsWorldPaused);
            for (int i = 0; i < 12; i++) yield return null;
            int slot = Enumerable.Range(0, hud.HandCount).First(i => hud.CanSelectHandSlot(i)
                && hud.GetHandSlot(i).Definition.Name == "单手剑 · 主动接敌");
            Assert.IsTrue(hud.TrySelectHandSlot(slot)); yield return null;
            Assert.IsTrue(hud.TryConfirmExecution());
            for (int i = 0; i < 120 && mixed.Stage.Phase != TacticalStagePhase.Executing; i++) yield return null;
            Assert.AreEqual(TacticalStagePhase.Executing, mixed.Stage.Phase, mixed.Stage.ExecutionIssue);
            // Controlled deaths use the existing attribute authority; the formal mixed runtime owns the outcome.
            var world = slice.CharacterRuntime.World;
            foreach (var entity in slice.Services.CharacterEntities.Where(entity => entity.Team.TeamId != mixed.Player.Team.TeamId))
            { world.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0); world.SyncCombatStateHealth(entity); }
            for (int i = 0; i < 600 && _coordinator.State != TacticalPreparationState.Completed; i++) yield return null;
            Assert.IsTrue(mixed.OutcomeReady);
            Assert.AreEqual(TacticalPreparationState.Completed, _coordinator.State);
            yield return WaitForIdle();
            Assert.IsTrue(_coordinator.SettlementSaved);
            Assert.AreEqual(0, _coordinator.RewardState, "Authored level actors have no inventory-item reward ownership.");
            ulong run = mixed.Stage.StageRunId;
            _coordinator.RequestNewGameOrRetry();
            yield return WaitForIdle();
            Assert.AreNotSame(slice, _manager.RuntimeContext.Slice);
            ulong next = _manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId;
            Assert.Greater(next, run);
            AssertLevel(document, _manager.RuntimeContext.Slice, next);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager != null) UnityEngine.Object.Destroy(_manager.gameObject);
            yield return null;
            if (_directoryBefore != null)
            {
                File.WriteAllBytes(Path.Combine(_runtimeRoot, "directory.json"), _directoryBefore);
                foreach (string document in Directory.GetFiles(Path.Combine(_runtimeRoot, "documents"), "*.level.json"))
                    if (!_documentsBefore.Contains(document))
                        Assert.IsTrue(UnityEditor.AssetDatabase.DeleteAsset(Path.GetRelativePath(Directory.GetCurrentDirectory(), document).Replace('\\', '/')));
                CollectionAssert.AreEqual(_directoryBefore, File.ReadAllBytes(Path.Combine(_runtimeRoot, "directory.json")));
            }
            if (_opening != null)
            {
                typeof(CharacterTacticalServiceOpening).GetProperty("Published", Private).SetValue(_opening, _previousServices);
                typeof(CharacterTacticalServiceOpening).GetField("_opening", Private).SetValue(_opening, _previousOpening);
                typeof(CharacterTacticalServiceOpening).GetProperty("Issue", Private).SetValue(_opening, _previousIssue);
                typeof(CharacterTacticalServices).GetField("_progress", StaticPrivate).SetValue(null, _previousProgress);
            }
            Environment.SetEnvironmentVariable("MX_CHARACTERTEST_TACTICAL_SAVE_ROOT", _previousRoot);
            Time.timeScale = _scale;
        }
    }
}
