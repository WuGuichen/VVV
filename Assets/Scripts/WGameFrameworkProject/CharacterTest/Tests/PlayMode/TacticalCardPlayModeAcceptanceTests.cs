using System;
using System.Collections;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    /// <summary>Real scene lifecycle plus separately measured core/HUD and controlled timeout contracts.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalCardPlayModeAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/TacticalCardDemoScene.unity";
        private GameManager _manager;
        private TacticalCardHud _hud;
        private float _scale;

        [SetUp] public void SetUp() { _scale = Time.timeScale; Time.timeScale = 1; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager != null)
            {
                Scene scene = _manager.gameObject.scene;
                UnityEngine.Object.Destroy(_manager.gameObject);
                yield return null;
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("TacticalAcceptanceCleanup"));
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            Time.timeScale = _scale;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator G01_PlayMode_CompleteThreeTurnsToVictoryLifecycle()
        {
            yield return LoadDemo();
            var runtime = _manager.RuntimeContext.Slice.Services.Modules.TacticalStage;
            Assert.NotNull(runtime);
            // Story/Spawn is asynchronous. Observe ingress at one complete Host tick per rendered frame.
            _manager.RuntimeContext.FixedTickDriver.MaxStepsPerUpdate = 1;
            for (int frame = 0; frame < 600 && runtime.Stage.Phase == TacticalStagePhase.Booting; frame++) yield return null;
            Assert.AreEqual(TacticalStagePhase.AwaitingIngressSettle, runtime.Stage.Phase);
            Assert.IsTrue(runtime.Stage.CanStep);
            yield return WaitForPlanning(runtime);
            Assert.AreEqual(3, _hud.HandCount);
            Assert.AreEqual(50, _hud.CurrentMomentum);
            Assert.AreEqual(TacticalTurnPhase.ReadyPaused, runtime.Stage.Time.Phase);
            Assert.AreEqual(0, Time.timeScale);
            Assert.GreaterOrEqual(runtime.Stage.IngressElapsedTicks, 3);
            Assert.IsTrue(runtime.LastPlayerSample.HasStableSupport && runtime.LastOpponentSample.HasStableSupport);
            var names = new string[3];
            for (int i = 0; i < 3; i++) names[i] = _hud.GetHandSlot(i).Definition.Name;
            CollectionAssert.AreEquivalent(new[] { "蓄劲", "猛击", "调息" }, names);
            yield return CaptureFrame("planning");

            // Fixed acceptance initial durability, through the same attribute authority as combat.
            // No healing, damage injection or fake Finished is allowed after the first command.
            var world = _manager.RuntimeContext.Slice.CharacterRuntime.World;
            SetInitialDurability(world, runtime.Player, 1000);
            SetInitialDurability(world, runtime.Opponent, 140);

            int starts = 0, finishes = 0;
            runtime.Adapter.ActionStateChanged += (id, state) =>
            {
                if (state == CharacterActionInstanceState.Running) starts++;
                if (state == CharacterActionInstanceState.Finished) finishes++;
            };
            for (int turn = 1; turn <= 3; turn++)
            {
                Assert.AreEqual((ulong)turn, _hud.TurnIndex);
                int beforeHp = ReadAuthorityHp(world, runtime.Opponent);
                int beforeZ = runtime.Player.Transform.ZMillimeters;
                int slot = FindActionSlot(_hud);
                Assert.IsTrue(_hud.TrySelectHandSlot(slot));
                yield return null;
                Assert.NotNull(runtime.Stage.Deck.PendingAction);
                Assert.AreEqual(10, _hud.ReservedMomentum);
                Assert.IsTrue(_hud.TryConfirmExecution());
                yield return null;
                Assert.AreEqual(TacticalStagePhase.Executing, _hud.Phase, runtime.Stage.ExecutionIssue);
                Assert.AreEqual(1, Time.timeScale);
                bool damaged = false, moved = false;
                for (int frame = 0; frame < 1800 && _hud.Phase != TacticalStagePhase.Planning && _hud.Phase != TacticalStagePhase.StageCompleted; frame++)
                {
                    damaged |= ReadAuthorityHp(world, runtime.Opponent) < beforeHp;
                    moved |= runtime.Player.Transform.ZMillimeters != beforeZ;
                    Assert.AreNotEqual(TacticalStageHealth.SuspendedByFault, runtime.Stage.Health, runtime.Stage.ExecutionIssue);
                    yield return null;
                }
                TestContext.WriteLine("Turn " + turn + ": authority HP " + beforeHp + " -> " + ReadAuthorityHp(world, runtime.Opponent) +
                    ", phase=" + _hud.Phase + ", starts=" + starts + ", finishes=" + finishes);
                Assert.IsTrue(damaged || ReadAuthorityHp(world, runtime.Opponent) < beforeHp, "Formal combat must damage the opponent.");
                if (turn == 1) Assert.IsTrue(moved, "Formal action must approach from the level-authored spawn.");
                if (turn < 3)
                {
                    Assert.AreEqual(TacticalStagePhase.Planning, _hud.Phase);
                    Assert.AreEqual(turn, finishes);
                    Assert.AreEqual(3, _hud.HandCount);
                    Assert.IsNull(runtime.Stage.Deck.ResolvingAction);
                }
            }
            Assert.LessOrEqual(runtime.Opponent.CombatState.Health, 0);
            Assert.AreEqual(3, starts);
            Assert.AreEqual(TacticalStagePhase.StageCompleted, _hud.Phase);
            Assert.IsTrue(runtime.EndTriggers.HasStoryResult);
            Assert.AreEqual(450501, CharacterTestGameplayTriggerBattleVitalsCatalog.BattleVitalsSignalId);
            Assert.AreEqual(0, runtime.TriggerRuntime.ReliableOutboxRecordCount);
            Assert.IsTrue(runtime.Stage.Time.HasPauseReason(TacticalPauseReason.BattleEnded));
            Assert.IsTrue(_hud.HasOutcome && _hud.IsVictory);
            Assert.AreEqual("战斗胜利！", _hud.OutcomeMessage);
            yield return CaptureFrame("victory");
            Assert.IsTrue(_hud.GetComponent<UIDocument>().rootVisualElement.Q<Button>("restartBattle").enabledSelf);
            ulong oldRun = runtime.Stage.StageRunId;
            Assert.IsTrue(_hud.TryRequestRestart());
            for (int frame = 0; frame < 1800 && _manager.RuntimeContext.Slice.Services.Modules.TacticalStage.Stage.StageRunId == oldRun; frame++) yield return null;
            var restarted = _manager.RuntimeContext.Slice.Services.Modules.TacticalStage;
            Assert.Greater(restarted.Stage.StageRunId, oldRun);
            yield return WaitForPlanning(restarted);
            Assert.AreEqual(50, _hud.CurrentMomentum);
            Assert.IsFalse(_hud.HasOutcome);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator G01_PlayMode_DefeatOnPlayerDeath()
        {
            yield return Defeat(false);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator G01_PlayMode_SimultaneousDeathsChooseDefeatOnce()
        {
            yield return Defeat(true);
        }

        private IEnumerator Defeat(bool simultaneous)
        {
            yield return LoadDemo();
            var runtime = _manager.RuntimeContext.Slice.Services.Modules.TacticalStage;
            yield return WaitForPlanning(runtime);
            Assert.IsTrue(_hud.TrySelectHandSlot(FindActionSlot(_hud))); yield return null;
            Assert.IsTrue(_hud.TryConfirmExecution()); yield return null;
            // Controlled death through the existing attribute authority; outcome still uses the real atomic Trigger and Story.
            var world = _manager.RuntimeContext.Slice.CharacterRuntime.World;
            world.SetCurrentAttribute(runtime.Player.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0);
            world.SyncCombatStateHealth(runtime.Player);
            if (simultaneous)
            {
                world.SetCurrentAttribute(runtime.Opponent.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0);
                world.SyncCombatStateHealth(runtime.Opponent);
            }
            for (int frame = 0; frame < 600 && !_hud.HasOutcome; frame++) yield return null;
            Assert.AreEqual(TacticalStagePhase.StageCompleted, _hud.Phase);
            Assert.IsTrue(_hud.HasOutcome);
            Assert.IsFalse(_hud.IsVictory, "Defeat wins simultaneous deaths.");
            Assert.AreEqual("战斗失败！", _hud.OutcomeMessage);
            if (!simultaneous) yield return CaptureFrame("defeat");
            var flow = _manager.RuntimeContext.Slice.Services.Modules.StoryFlowBridge;
            Assert.AreEqual(CharacterTestBattleOutcome.Defeat, flow.LastBattleOutcome);
            Assert.AreEqual(0, runtime.TriggerRuntime.ReliableOutboxRecordCount);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(CharacterTestBattleOutcome.Defeat, flow.LastBattleOutcome);
        }

        [UnityTest]
        public IEnumerator G02_PlayMode_SteadyStateCoreLoop_ZeroGCAlloc()
        {
            using (var f = new CoreFixture())
            {
                yield return null;
                for (int i = 0; i < 16; i++) { f.CoreTurn(); f.CompleteTurn(); }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 200; i++) f.CoreTurn();
                long core = GC.GetAllocatedBytesForCurrentThread() - before;
                f.Hud.RefreshIfChanged();
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 200; i++) f.CompleteTurn();
                long coreAndHud = GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.WriteLine("Warmed pure core, 200 turns: " + core + " B; core + HUD synchronous refresh, 200 turns: " + coreAndHud +
                    " B. Excludes UI renderer, real world and Story cold operations.");
                Assert.AreEqual(0, core, "Pure core");
                Assert.AreEqual(0, coreAndHud);
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) f.Hud.RefreshIfChanged();
                Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "Unchanged HUD refresh");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator F01_PlayMode_ExecutionTimeoutTriggersFaultSuspended()
        {
            using (var f = new CoreFixture(momentumCost: 10))
            {
                yield return null;
                f.SelectAndConfirm();
                var resolving = f.Stage.Deck.ResolvingAction;
                ulong turn = f.Stage.TurnIndex;
                int momentum = f.Stage.Ledger.Current;
                Assert.AreEqual(40, momentum, "The execution must have paid its real reservation before timing out.");
                for (int tick = 1; tick <= 299; tick++) f.Tick(false);
                Assert.AreEqual(TacticalStageHealth.Running, f.Stage.Health);
                f.Tick(false); f.Hud.RefreshIfChanged();
                Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Hud.Health);
                Assert.AreSame(resolving, f.Stage.Deck.ResolvingAction);
                Assert.AreEqual(turn, f.Hud.TurnIndex);
                Assert.AreEqual(momentum, f.Hud.CurrentMomentum);
                Assert.AreEqual(2, f.Hud.HandCount);
                Assert.AreEqual(0, Time.timeScale);
                Assert.IsNotEmpty(f.Hud.FaultMessage);
                Assert.IsTrue(f.Hud.CanRequestRecovery && f.Hud.CanRequestRestart);
                var root = f.Hud.GetComponent<UIDocument>().rootVisualElement;
                Assert.IsTrue(root.Q<Button>("recoverBattle").enabledSelf);
                Assert.IsTrue(root.Q<Button>("resetBattle").enabledSelf);
                Assert.IsTrue(f.Hud.TryRequestRecovery()); f.Drain();
                Assert.AreEqual(TacticalStageHealth.Running, f.Stage.Health);
                f.Port.Finished = true;
                f.Tick(true); f.Tick(true); f.Tick(true);
                Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator F02_PlayMode_ExecutionAdmissionRejectionShowsActionableHudFeedback()
        {
            using (var f = new CoreFixture())
            {
                yield return null;
                Assert.IsTrue(f.Hud.TrySelectHandSlot(0));
                f.Drain();
                f.Port.PrepareIssue = "ActionSummary:Stale:MeasurementInputsChanged";
                Assert.IsTrue(f.Hud.TryConfirmExecution(), "The click should be accepted into the command pump.");
                f.Drain();

                Assert.AreEqual(TacticalStageRejection.ExecutionRejected, f.Stage.LastRejection);
                Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
                Assert.NotNull(f.Stage.Deck.PendingAction);
                StringAssert.Contains("动作测量摘要不可用", f.Hud.CommandFeedbackMessage);
                StringAssert.Contains(f.Port.PrepareIssue, f.Hud.CommandFeedbackMessage);
                yield return null;
                var feedback = f.Hud.GetComponent<UIDocument>().rootVisualElement.Q("commandFeedback");
                Assert.AreEqual(DisplayStyle.Flex, feedback.resolvedStyle.display);
                Assert.AreEqual(f.Hud.CommandFeedbackMessage,
                    feedback.Q<Label>("commandFeedbackMessage").text);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator G06_PlayMode_CursorRemainsReleasedAndVisibleDuringTacticalSession()
        {
            yield return LoadDemo();
            var runtime = _manager.RuntimeContext.Slice.Services.Modules.TacticalStage;
            Assert.NotNull(runtime);
            yield return WaitForPlanning(runtime);
            Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState);
            Assert.IsTrue(UnityEngine.Cursor.visible);
            int actionSlot = FindActionSlot(_hud);
            Assert.IsTrue(_hud.TrySelectHandSlot(actionSlot));
            yield return null;
            Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState);
            Assert.IsTrue(UnityEngine.Cursor.visible);
            Assert.IsTrue(_hud.TryConfirmExecution());
            yield return null;
            Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState);
            Assert.IsTrue(UnityEngine.Cursor.visible);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator G07_PlayMode_CurrentPreparationDeckConfirmsIntoRealExecution()
        {
            yield return LoadDemo();
            var runtime = _manager.RuntimeContext.Slice.Services.Modules.TacticalStage;
            yield return WaitForPlanning(runtime);

            Assert.IsTrue(_hud.TrySelectHandSlot(FindActionSlot(_hud)));
            yield return null;
            Assert.IsTrue(_hud.TryConfirmExecution());
            yield return null;

            Assert.AreEqual(TacticalStageRejection.None, runtime.Stage.LastRejection, runtime.Stage.ExecutionIssue);
            Assert.IsEmpty(runtime.Stage.ExecutionIssue);
            Assert.AreEqual(TacticalStagePhase.Executing, runtime.Stage.Phase);
            Assert.NotNull(runtime.Stage.Deck.ResolvingAction);
            Assert.IsEmpty(_hud.CommandFeedbackMessage);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator G08_PlayMode_PreparationSpawnsSavedEntitiesWithoutNarrativeUi()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("TacticalCardDemoScene");
#endif
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            for (int frame = 0; frame < 1800 && (CharacterTacticalServices.Preparation == null
                    || _manager.RuntimeContext?.Slice?.Services.CharacterEntities.Count
                        != CharacterTacticalServices.Preparation.Working.Entities.Count); frame++)
                yield return null;

            Assert.NotNull(CharacterTacticalServices.Preparation);
            Assert.AreEqual(CharacterTacticalServices.Preparation.Working.Entities.Count,
                _manager.RuntimeContext.Slice.Services.CharacterEntities.Count);
            Assert.Greater(_manager.RuntimeContext.Slice.Services.CharacterEntities.Count, 0);
            Assert.IsFalse(_manager.RuntimeContext.UiRuntime.FairyGuiStoryDialogHost?.IsOpen == true);
            Assert.IsFalse(_manager.RuntimeContext.UiRuntime.FairyGuiCombatHudHost?.IsOpen == true);
            Assert.IsTrue(_manager.RuntimeContext.UiRuntime.FairyGuiTacticalDeckPanelHost?.IsOpen == true);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator G09_PlayMode_MixedRosterPlayerHandPausesExecutesAndReturnsToPlanning()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("TacticalCardDemoScene");
#endif
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            for (int frame = 0; frame < 1800 && (CharacterTacticalServices.Preparation == null
                    || _manager.RuntimeContext?.Slice?.Services.CharacterEntities.Count == 0); frame++)
                yield return null;
            TacticalBattlePreparationService preparation = CharacterTacticalServices.Preparation;
            Assert.NotNull(preparation);
            string playerEntityId = preparation.Working.Entities.First(entity => entity.Loadout.Cards.Count > 0).EntityId;
            try
            {
                Assert.IsTrue(preparation.SetPlayerControlledEntity(playerEntityId, out string issue), issue);
                TacticalCardSliceCoordinator flow = _manager.GetComponent<TacticalCardSliceCoordinator>();
                Assert.IsTrue(flow.TryDeployPreparedBattle(out issue), issue);
                for (int frame = 0; frame < 1800 && (_manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage == null
                        || _manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage.Stage.Phase
                            != TacticalStagePhase.Planning); frame++)
                    yield return null;
                TacticalMixedPlayerCardRuntime mixed =
                    _manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage;
                Assert.NotNull(mixed);
                Assert.AreEqual(TacticalStagePhase.Planning, mixed.Stage.Phase);
                Assert.AreEqual(0f, Time.timeScale);
                Assert.AreEqual(1, _manager.RuntimeContext.Slice.Services.CharacterEntities.Count(entity =>
                    entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer));
                Assert.Greater(_manager.RuntimeContext.Slice.Services.CharacterEntities.Count(entity =>
                    entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled), 0);
                Assert.NotNull(mixed.CurrentTarget);

                _hud = _manager.GetComponent<TacticalCardHud>();
                Assert.NotNull(_hud);
                Assert.IsTrue(_hud.IsBound);
                Assert.Greater(_hud.HandCount, 0);
                Assert.IsTrue(_hud.TrySelectHandSlot(FindActionSlot(_hud)));
                yield return null;
                Assert.IsTrue(_hud.TryConfirmExecution());
                ulong firstTurn = _hud.TurnIndex;
                for (int frame = 0; frame < 1800 && _hud.TurnIndex == firstTurn; frame++) yield return null;
                Assert.Greater(_hud.TurnIndex, firstTurn);
                Assert.AreEqual(TacticalStagePhase.Planning, mixed.Stage.Phase);
                Assert.AreEqual(0f, Time.timeScale);
            }
            finally
            {
                preparation.SetPlayerControlledEntity(string.Empty, out _);
            }
        }

        private IEnumerator LoadDemo()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("TacticalCardDemoScene");
#endif
            _manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(_manager);
            _hud = _manager.GetComponent<TacticalCardHud>();
            Assert.NotNull(_hud);
            if (!_hud.IsBound)
            {
                for (int frame = 0; frame < 600 && CharacterTacticalServices.Player == null; frame++)
                    yield return null;
                var preparationFlow = _manager.GetComponent<TacticalCardSliceCoordinator>();
                Assert.NotNull(preparationFlow);
                preparationFlow.RequestStartOrContinue();
                for (int frame = 0; frame < 600 && preparationFlow.State != TacticalPreparationState.Preparation; frame++)
                    yield return null;
                Assert.AreEqual(TacticalPreparationState.Preparation, preparationFlow.State, preparationFlow.StatusMessage);
                var roster = TacticalDeckCatalog.HeroRoster;
                Assert.Greater(roster.Count, 0, "The imported roster needs a hero for the isolated 1v1 fixture.");
                int hero = roster[0].CharacterId;
                int opponent = 0;
                foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                    if (character.CharacterId != hero) { opponent = character.CharacterId; break; }
                Assert.Greater(opponent, 0, "The preparation roster needs a distinct opponent.");
                // This fixture exercises the existing 1v1 runtime directly, without the retired UI selector.
                Assert.IsTrue(preparationFlow.TryDeploy(hero, opponent, out string issue),
                    "The fixture must deploy before scene acceptance. " + issue);
                for (int frame = 0; frame < 1800 && !_hud.IsBound; frame++) yield return null;
            }
            Assert.IsTrue(_hud.IsBound);
        }

        private IEnumerator WaitForPlanning(TacticalCardStageRuntime runtime)
        {
            for (int frame = 0; frame < 1800 && runtime.Stage.Phase != TacticalStagePhase.Planning; frame++)
            {
                Assert.AreEqual(TacticalStageHealth.Running, runtime.Stage.Health, runtime.Stage.ExecutionIssue);
                yield return null;
            }
            Assert.AreEqual(TacticalStagePhase.Planning, runtime.Stage.Phase);
        }

        private static int FindActionSlot(TacticalCardHud hud)
        {
            for (int i = 0; i < 3; i++) if (hud.GetHandSlot(i)?.Definition.Kind == TacticalCardKind.Action) return i;
            Assert.Fail("The hand contains no Action card."); return -1;
        }

        private static void SetInitialDurability(CharacterTestCharacterRuntimeWorld world, CharacterTestCharacterEntity entity, int hp)
        {
            world.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, hp);
            world.SyncCombatStateHealth(entity);
            Assert.AreEqual(hp, ReadAuthorityHp(world, entity));
        }

        private static int ReadAuthorityHp(CharacterTestCharacterRuntimeWorld world, CharacterTestCharacterEntity entity)
        {
            // BattleVitals consumes this authority. CombatState's presentation-facing health is capped at the default maximum.
            if (!world.GameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributes) ||
                !attributes.TryGet(entity.GameplayEntityId, out var values) || !values.TryGet(CharacterTestCharacterAttributeIds.Hp, out var hp))
                throw new InvalidOperationException("Missing authority HP.");
            return hp.CurrentValue;
        }

        private static IEnumerator CaptureFrame(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Library/MxFramework/TacticalCardT4E"));
            System.IO.Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory, name + ".png"));
            yield return null;
            yield return null;
        }

        private sealed class CoreFixture : IDisposable
        {
            internal readonly TacticalStageFlowCoordinator Stage;
            internal readonly TacticalCommandPump Commands;
            internal readonly TacticalCardHud Hud;
            internal readonly ControlledPort Port = new ControlledPort();
            private readonly GameObject _root;
            private readonly UnityTacticalTimeAdapter _time = new UnityTacticalTimeAdapter();
            private long _frame;
            private ulong _coreSequence;
            internal CoreFixture(int momentumCost = 0)
            {
                _time.SetSimulationPaused(false);
                var deck = new TacticalDeckManager(); var ledger = new MomentumLedger();
                var time = new TacticalTimeCoordinator(_time, new SettleDetector(new SettleDetectorConfig(3)));
                var driver = new TacticalSingleCardDriver(Port, time, deck, ledger);
                var action = new TacticalActionCardDefinition("test", "猛击", "Core fixture", momentumCost,
                    TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy,
                    TacticalApproachMode.None);
                Stage = new TacticalStageFlowCoordinator(1, time, deck, ledger, driver,
                    new TacticalCardDefinition[] { action, action, action }, 42, IsCurrent, new TacticalStoryStepBinding(1, 1, 1));
                Commands = new TacticalCommandPump(Stage);
                var opening = new TacticalStoryStepToken(1, 1, 1, 1, 1); Stage.OnStoryStepStarted(in opening);
                Stage.OnSpawnBound(1, 0, new GameplayEntityId(1, 1), 0);
                Stage.OnSpawnBound(1, 1, new GameplayEntityId(2, 1), 0);
                for (int i = 0; i < 3; i++)
                {
                    var sample = Sample(++_frame, true, true);
                    Stage.OnCompletedSimulationTick(_frame, in sample, in sample);
                }
                _root = new GameObject("Tactical HUD core acceptance");
                Hud = _root.AddComponent<TacticalCardHud>(); Hud.Bind(Stage, Commands);
            }
            private static bool IsCurrent(GameplayEntityId id) => id.IsValid;
            internal void Drain() { Commands.Drain(); while (Commands.TryReadReceipt(out _)) { } Hud.RefreshIfChanged(); }
            internal void SelectAndConfirm()
            {
                Port.Finished = false;
                if (!Hud.TrySelectHandSlot(0)) throw new InvalidOperationException("select"); Drain();
                if (!Hud.TryConfirmExecution()) throw new InvalidOperationException("confirm"); Drain();
            }
            internal void Tick(bool settled)
            {
                var sample = Sample(++_frame, settled, false);
                Stage.OnCompletedSimulationTick(_frame, in sample, in sample);
                Hud.RefreshIfChanged();
            }
            internal void CompleteTurn()
            {
                SelectAndConfirm(); Port.Finished = true;
                Tick(true); Tick(true); Tick(true);
            }
            internal void CoreTurn()
            {
                Port.Finished = false;
                var card = Stage.Deck.Hand[0];
                var select = new TacticalStageCommand(1, Stage.TurnIndex, card.InstanceId, card.Version,
                    Stage.SnapshotRevision, TacticalStageCommandKind.SelectAction);
                Commands.TrySubmit(in select, 1, ++_coreSequence); Commands.Drain();
                while (Commands.TryReadReceipt(out _)) { }
                var confirm = new TacticalStageCommand(1, Stage.TurnIndex, card.InstanceId, card.Version,
                    Stage.SnapshotRevision, TacticalStageCommandKind.ConfirmExecution);
                Commands.TrySubmit(in confirm, 1, ++_coreSequence); Commands.Drain();
                while (Commands.TryReadReceipt(out _)) { }
                Port.Finished = true;
                for (int i = 0; i < 3; i++)
                {
                    var sample = Sample(++_frame, true, false);
                    Stage.OnCompletedSimulationTick(_frame, in sample, in sample);
                }
            }
            internal static SettleSample Sample(long frame, bool settled, bool ingress) => new SettleSample(frame,
                !ingress && settled, true, settled, false, false, false, true, true, true, false, settled ? 0 : 1, 0, ingress);
            public void Dispose() { Stage.ReleaseControl(); _time.Dispose(); UnityEngine.Object.Destroy(_root); }
        }

        private sealed class ControlledPort : ITacticalExecutionPort
        {
            internal bool Finished;
            internal string PrepareIssue = string.Empty;
            private ulong _id;
            public bool TryPrepare(TacticalCardInstance card, out string issue)
            {
                issue = PrepareIssue;
                return string.IsNullOrEmpty(issue);
            }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { issue = string.Empty; executionId = ++_id; return true; }
            public ExecutionObservation Observe(ulong id) => new ExecutionObservation(id, true, Finished, Finished, Finished, true);
            public bool TryAbortPreAction(ulong id, string reason) => false;
        }
    }
}
