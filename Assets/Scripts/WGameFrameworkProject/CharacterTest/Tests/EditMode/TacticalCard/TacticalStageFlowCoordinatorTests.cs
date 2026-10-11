using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalStageFlowCoordinatorTests
    {
        private float _originalScale;
        [SetUp] public void SetUp() { _originalScale = Time.timeScale; Time.timeScale = 1f; }
        [TearDown] public void TearDown() { Time.timeScale = _originalScale; }

        [Test]
        public void SmoothPlanningPauseBlocksCardsUntilWorldStopsAndStillObservesWorldTicks()
        {
            using var adapter = new UnityTacticalTimeAdapter(1f, .15f, .25f);
            var f = new Fixture(adapter: adapter);
            f.Ready();
            var card = f.Find("a");
            Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            Assert.AreEqual(TacticalStageRejection.WrongPhase, f.Apply(TacticalStageCommandKind.SelectAction, card));
            adapter.TickTransition(.125f);
            Assert.AreEqual(.875f, Time.timeScale, .0001f);
            f.Tick(4);
            Assert.AreEqual(4, f.Stage.LastCompletedFrame);
            f.Stage.UpdatePauseTransition();
            Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            adapter.TickTransition(.125f);
            f.Stage.UpdatePauseTransition();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.SelectAction, card));
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.ConfirmExecution, card));
            Assert.AreEqual(0f, Time.timeScale, "Acceptance must not jump immediately to full speed.");
            adapter.TickTransition(.075f);
            Assert.AreEqual(.125f, Time.timeScale, .0001f);
            adapter.TickTransition(.075f);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void SmoothTurnCompletionWaitsForDecelerationBeforeNextDecision()
        {
            using var adapter = new UnityTacticalTimeAdapter(1f, .15f, .25f);
            var f = new Fixture(adapter: adapter); f.Ready();
            adapter.TickTransition(.25f); f.Stage.UpdatePauseTransition();
            var card = f.Find("a"); f.Apply(TacticalStageCommandKind.SelectAction, card);
            f.Apply(TacticalStageCommandKind.ConfirmExecution, card); adapter.TickTransition(.15f);
            f.Port.Observation = new ExecutionObservation(1, true, true, true, true, true);
            f.Tick(4); f.Tick(5); f.Tick(6);
            Assert.AreEqual(2ul, f.Stage.TurnIndex);
            Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            Assert.AreEqual(1f, Time.timeScale);
            adapter.TickTransition(.125f); f.Tick(7);
            Assert.AreEqual(.875f, Time.timeScale, .0001f);
            adapter.TickTransition(.125f); f.Stage.UpdatePauseTransition();
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(2ul, f.Stage.TurnIndex, "Transition completion must not complete a second turn.");
        }

        [Test]
        public void SmoothTransitionRepeatedRequestsDoNotRestartAndMenuCancelsImmediately()
        {
            using var adapter = new UnityTacticalTimeAdapter(.5f, .15f, .25f);
            adapter.SetSimulationPaused(false);
            var f = new Fixture(adapter: adapter); f.Ready();
            for (int i = 0; i < 5; i++)
            {
                f.Time.RequestResume(TacticalPauseReason.UserMenu);
                adapter.TickTransition(.05f);
            }
            Assert.AreEqual(0f, Time.timeScale);
            f.Stage.UpdatePauseTransition();
            var card = f.Find("a"); f.Apply(TacticalStageCommandKind.SelectAction, card);
            f.Apply(TacticalStageCommandKind.ConfirmExecution, card); adapter.TickTransition(.05f);
            Assert.Greater(Time.timeScale, 0f);
            Assert.Less(Time.timeScale, .5f);
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            Assert.AreEqual(0f, Time.timeScale); Assert.IsFalse(adapter.IsTransitioning);
            adapter.TickTransition(1f); Assert.AreEqual(0f, Time.timeScale);
            f.Time.RequestResume(TacticalPauseReason.UserMenu); adapter.TickTransition(.15f);
            Assert.AreEqual(.5f, Time.timeScale);
            f.Time.ForceSuspend(TacticalPauseReason.ExecutionFault);
            Assert.AreEqual(0f, Time.timeScale); Assert.IsFalse(adapter.IsTransitioning);
        }

        [Test]
        public void B01_FallingActorsRunUntilTheLastActorHasThreeSupportedTicks()
        {
            var f = new Fixture(); f.Start(); f.BindBoth();
            var fixedTicks = new CharacterTestFixedTickDriver { CanStepPredicate = () => f.Stage.CanStep };
            int frame = 0;
            Action<double> tick = _ =>
            {
                frame++;
                var player = Sample(frame, support: frame >= 5, linear: frame < 5 ? 5 : 0);
                var opponent = Sample(frame, support: frame >= 8, linear: frame < 8 ? 8 : 0);
                f.Stage.OnCompletedSimulationTick(frame, in player, in opponent);
            };
            for (int i = 1; i <= 9; i++)
            {
                Assert.AreEqual(1, fixedTicks.Step(1d / 60, 1d / 60, tick));
                Assert.AreEqual(1f, Time.timeScale);
                Assert.AreEqual(0ul, f.Time.CurrentTurnIndex);
                Assert.AreEqual(0, f.Deck.Hand.Count);
                Assert.AreEqual(7, f.Ledger.Current, "Do not initialize resources during ingress.");
            }
            Assert.AreEqual(1, fixedTicks.Step(1d / 60, 1d / 60, tick));
            Assert.AreEqual(10, frame);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.AreEqual(TacticalTurnPhase.ReadyPaused, f.Time.Phase);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(50, f.Ledger.Current);
            Assert.AreEqual(3, f.Deck.Hand.Count);
            Assert.AreEqual(0, fixedTicks.Step(1d, 1d / 60, tick));
        }

        [TestCase(0)] [TestCase(1)]
        public void B02_OneMissingSlotTimesOutWithoutInitializing(int missingSlot)
        {
            var f = new Fixture(budget: 3); f.Start();
            f.Stage.OnSpawnBound(1, 1 - missingSlot, missingSlot == 0 ? f.Opponent : f.Player, 0);
            f.Tick(1); f.Tick(2);
            Assert.AreEqual(TacticalStagePhase.Booting, f.Stage.Phase);
            Assert.IsTrue(f.Stage.CanStep);
            f.Tick(3);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.SpawnTimeout, f.Stage.Fault);
            Assert.AreEqual(0ul, f.Stage.TurnIndex);
            Assert.AreEqual(0, f.Deck.Hand.Count);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void B02_InvalidDefaultStaleGenerationAndDuplicateEntityFailClosed(int invalidKind)
        {
            var f = new Fixture(); f.Start();
            f.Stage.OnSpawnBound(1, 0, f.Player, 0);
            GameplayEntityId invalid = invalidKind == 0 ? default : invalidKind == 1
                ? new GameplayEntityId(f.Opponent.Index, 2) : f.Player;
            f.Stage.OnSpawnBound(1, 1, invalid, 0);
            Assert.AreEqual(TacticalStageRejection.InvalidSpawnBinding, f.Stage.Fault);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            f.Tick(1); f.Tick(2); f.Tick(3);
            Assert.AreEqual(0ul, f.Stage.TurnIndex);
        }

        [Test]
        public void B02_EntityGenerationCanExpireAfterBinding()
        {
            var f = new Fixture(); f.Start(); f.BindBoth(); f.Tick(1); f.Tick(2);
            f.EntitiesAlive = false; f.Tick(3);
            Assert.AreEqual(TacticalStageRejection.InvalidSpawnBinding, f.Stage.Fault);
            Assert.AreEqual(0, f.Deck.Hand.Count);
        }

        [Test]
        public void B03_DuplicateOpeningAndSpawnReceiptsDoNotResetTheSession()
        {
            var f = new Fixture(); f.Start(); f.BindBoth(); f.Tick(1);
            ulong revision = f.Stage.SnapshotRevision;
            f.Start();
            Assert.AreEqual(TacticalStageRejection.DuplicateStoryStep, f.Stage.LastRejection);
            f.Stage.OnSpawnBound(1, 0, f.Player, 1);
            Assert.AreEqual(revision, f.Stage.SnapshotRevision);
            f.Tick(2); f.Tick(3);
            var originalCard = f.Deck.Hand[0];
            f.Ledger.Grant(19); revision = f.Stage.SnapshotRevision;
            f.Start(); f.Tick(4);
            Assert.AreEqual(69, f.Ledger.Current);
            Assert.AreSame(originalCard, f.Deck.Hand[0]);
            Assert.AreEqual(revision, f.Stage.SnapshotRevision);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
        }

        [Test]
        public void B03_OriginalStorySpawnOwnerDoesNotRepeatPartiallyAcceptedSpawns()
        {
            var manager = new ResourceManager();
            using (var resources = new GlobalResourceRuntimeServices(GlobalResourceRuntimeMode.Editor,
                manager, new ResourcePreloadService(manager)))
            {
                var director = new StoryDirector();
                var story = new StoryRuntimeModule(director);
                var spawn = new SpawnPort();
                using (var bridge = new CharacterTestStoryFlowBridge(story, resources, NullRuntimeLogger.Instance,
                    () => RuntimeFrame.Zero, "", spawn, new[]
                    { CharacterTestCharacterSpawnSpec.TemporaryPlayer, CharacterTestCharacterSpawnSpec.TemporaryAiOpponent }))
                {
                    Publish(director, StoryEventKind.StepStarted, CharacterTestStoryIds.BootstrapGraph,
                        CharacterTestStoryIds.Steps.OpenStartupUi, CharacterTestStoryIds.Actions.OpenStartupUi);
                    Publish(director, StoryEventKind.StepStarted, CharacterTestStoryIds.BootstrapGraph,
                        CharacterTestStoryIds.Steps.OpenStartupUi, CharacterTestStoryIds.Actions.OpenStartupUi);
                    Assert.AreEqual(2, spawn.Requests, "Do not spawn slot zero again when slot one was rejected.");
                }
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void C01_ReadyOnFirstSubstepStopsTheSameUpdate(bool interactive)
        {
            var f = new Fixture(); f.Start(); f.BindBoth(); f.Tick(1); f.Tick(2);
            var ticks = new CharacterTestFixedTickDriver { CanStepPredicate = () => f.Stage.CanStep, MaxStepsPerUpdate = 8 };
            int calls = 0;
            Action<double> tick = _ => { calls++; f.Tick(2 + calls); };
            int executed = interactive ? ticks.StepInteractive(8d / 60, 1d / 60, tick, 8, 0)
                : ticks.Step(8d / 60, 1d / 60, tick);
            Assert.AreEqual(1, executed);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0d, ticks.AccumulatorSeconds);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
        }

        [Test]
        public void C02_PausedOrderedSelectionSwitchCancelUtilityAndConfirmCommitExactlyOnce()
        {
            var f = new Fixture(); f.Ready();
            var a = f.Find("a"); var b = f.Find("b"); var utility = f.Find("utility");
            var pump = new TacticalCommandPump(f.Stage);
            ulong revision = f.Stage.SnapshotRevision;
            var commands = new[]
            {
                f.Command(TacticalStageCommandKind.SelectAction, a, revision),
                f.Command(TacticalStageCommandKind.SelectAction, b, revision + 1),
                new TacticalStageCommand(1, 1, b.InstanceId, b.Version + 1, revision + 2, TacticalStageCommandKind.CancelPending),
                f.Command(TacticalStageCommandKind.PlayUtility, utility, revision + 3),
                new TacticalStageCommand(1, 1, a.InstanceId, a.Version + 2, revision + 4, TacticalStageCommandKind.SelectAction),
                new TacticalStageCommand(1, 1, a.InstanceId, a.Version + 3, revision + 5, TacticalStageCommandKind.ConfirmExecution)
            };
            for (int i = 0; i < commands.Length; i++)
                Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(commands[i], 0, (ulong)i + 1));
            Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(commands[5], 0, 7));
            for (int i = 0; i < commands.Length; i++)
            {
                Assert.AreEqual(0f, Time.timeScale, "Every control remains reachable while paused.");
                Assert.AreEqual(1, pump.Drain(1));
                Assert.IsTrue(pump.TryReadReceipt(out var receipt));
                Assert.AreEqual(TacticalStageRejection.None, receipt.Rejection);
                Assert.AreEqual(4, receipt.Submitted.WorldBoundary);
                Assert.AreEqual((ulong)i + 1, receipt.Submitted.Sequence);
                if (i == 0) { Assert.AreSame(a, f.Deck.PendingAction); Assert.AreEqual(20, f.Ledger.Reserved); }
                if (i == 1) { Assert.AreSame(b, f.Deck.PendingAction); Assert.AreEqual(10, f.Ledger.Reserved); }
                if (i == 2) { Assert.IsNull(f.Deck.PendingAction); Assert.AreEqual(0, f.Ledger.Reserved); }
                if (i == 3) Assert.AreEqual(80, f.Ledger.Current);
            }
            Assert.AreEqual(1, pump.Drain());
            Assert.IsTrue(pump.TryReadReceipt(out var duplicate));
            Assert.AreEqual(TacticalStageRejection.StaleSnapshot, duplicate.Rejection);
            Assert.AreEqual(1, f.Port.Admissions);
            Assert.AreEqual(60, f.Ledger.Current);
            Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.AreEqual(TacticalStagePhase.Executing, f.Stage.Phase);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void C03_MenuAndReadyOwnIndependentReasons()
        {
            var f = new Fixture(); f.Ready();
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            f.Time.RequestResume(TacticalPauseReason.UserMenu);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.Ready));
            Assert.AreEqual(0f, Time.timeScale);
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            var a = f.Find("a");
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.SelectAction, a));
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.ConfirmExecution, a));
            Assert.IsFalse(f.Time.HasPauseReason(TacticalPauseReason.Ready));
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.UserMenu));
            Assert.AreEqual(0f, Time.timeScale);
            f.Time.RequestResume(TacticalPauseReason.UserMenu);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [TestCase(false)] [TestCase(true)]
        public void C04_TenSecondsPausedNeverBecomeSixHundredWorldTicks(bool manyUpdates)
        {
            var f = new Fixture(); f.Ready();
            var ticks = new CharacterTestFixedTickDriver { CanStepPredicate = () => f.Stage.CanStep, MaxStepsPerUpdate = 1000 };
            int calls = 0; Action<double> tick = _ => calls++;
            if (manyUpdates)
                for (int i = 0; i < 600; i++) Assert.AreEqual(0, ticks.Step(1d / 60, 1d / 60, tick));
            else Assert.AreEqual(0, ticks.Step(10d, 1d / 60, tick));
            Assert.AreEqual(0d, ticks.AccumulatorSeconds);
            var a = f.Find("a"); f.Apply(TacticalStageCommandKind.SelectAction, a); f.Apply(TacticalStageCommandKind.ConfirmExecution, a);
            Assert.AreEqual(1, ticks.Step(1d / 60, 1d / 60, tick));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void PauseOnFinalPermittedSubstepDiscardsFractionalBacklog()
        {
            bool canStep = true;
            var ticks = new CharacterTestFixedTickDriver { CanStepPredicate = () => canStep, MaxStepsPerUpdate = 1 };
            Assert.AreEqual(1, ticks.Step(1.5d / 60, 1d / 60, _ => canStep = false));
            Assert.AreEqual(0d, ticks.AccumulatorSeconds);
            ticks.Step(0.25d / 60, 1d / 60, _ => Assert.Fail("Paused"));
            Assert.AreEqual(0d, ticks.AccumulatorSeconds);
            canStep = true;
            Assert.AreEqual(0, ticks.Step(0.5d / 60, 1d / 60, _ => Assert.Fail("No full frame")));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void IngressRejectsMissingPhysicalAndNoActionFacts(int invalidKind)
        {
            var f = new Fixture(); f.Start(); f.BindBoth(); f.Tick(1); f.Tick(2);
            var bad = new SettleSample(3, invalidKind == 5, true, true, invalidKind == 3, false,
                invalidKind == 4, true, true, invalidKind != 0, false,
                invalidKind == 1 ? float.NaN : 0, 0, invalidKind != 2);
            var good = Sample(3);
            f.Stage.OnCompletedSimulationTick(3, in good, in bad);
            Assert.AreEqual(0ul, f.Stage.TurnIndex);
            f.Tick(4); f.Tick(5);
            Assert.AreEqual(0ul, f.Stage.TurnIndex);
            f.Tick(6);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
        }

        [TestCase(false)] [TestCase(true)]
        public void DuplicateAndMismatchedFramesRestartBothStabilityWindows(bool mismatched)
        {
            var f = new Fixture(); f.Start(); f.BindBoth(); f.Tick(1); f.Tick(2);
            if (mismatched)
            {
                var player = Sample(3); var opponent = Sample(2);
                f.Stage.OnCompletedSimulationTick(3, in player, in opponent);
            }
            else f.Tick(2);
            long next = mismatched ? 4 : 3;
            f.Tick(next); f.Tick(next + 1);
            Assert.AreEqual(0ul, f.Stage.TurnIndex);
            f.Tick(next + 2); Assert.AreEqual(1ul, f.Stage.TurnIndex);
        }

        [Test]
        public void IngressTimeoutRecoveryPreservesMenuAndRequiresFreshWindow()
        {
            var f = new Fixture(budget: 3); f.Start(); f.BindBoth();
            for (int i = 1; i <= 3; i++) { var falling = Sample(i, false, 4); f.Stage.OnCompletedSimulationTick(i, in falling, in falling); }
            Assert.AreEqual(TacticalStageRejection.IngressTimeout, f.Stage.Fault);
            Assert.AreEqual(TacticalStagePhase.AwaitingIngressSettle, f.Stage.Phase);
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.RequestRecovery));
            Assert.AreEqual(0f, Time.timeScale);
            f.Time.RequestResume(TacticalPauseReason.UserMenu);
            Assert.AreEqual(1f, Time.timeScale, "Recovery before Initialize must be able to release its own pause.");
            f.Tick(4); f.Tick(5); Assert.AreEqual(0ul, f.Stage.TurnIndex);
            f.Tick(6); Assert.AreEqual(1ul, f.Stage.TurnIndex);
        }

        [Test]
        public void MenuDuringIngressDoesNotInitializeOrConsumeBudget()
        {
            var f = new Fixture(); f.Start(); f.BindBoth();
            f.Time.RequestPause(TacticalPauseReason.UserMenu); f.Tick(1000);
            Assert.AreEqual(0, f.Stage.IngressElapsedTicks);
            f.Time.RequestResume(TacticalPauseReason.UserMenu);
            Assert.AreEqual(1f, Time.timeScale);
            f.Tick(1); f.Tick(2); f.Tick(3); Assert.AreEqual(1ul, f.Stage.TurnIndex);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void StaleCommandsDoNotMutateCardsOrLedger(int invalidKind)
        {
            var f = new Fixture(); f.Ready(); var card = f.Find("a");
            var cmd = new TacticalStageCommand(invalidKind == 0 ? 2ul : 1ul, invalidKind == 1 ? 2ul : 1ul,
                invalidKind == 2 ? ulong.MaxValue : card.InstanceId, invalidKind == 3 ? card.Version + 1 : card.Version,
                invalidKind == 4 ? 0 : f.Stage.SnapshotRevision, TacticalStageCommandKind.SelectAction);
            var expected = new[] { TacticalStageRejection.WrongStage, TacticalStageRejection.WrongTurn,
                TacticalStageRejection.InvalidCard, TacticalStageRejection.StaleCardVersion, TacticalStageRejection.StaleSnapshot };
            Assert.AreEqual(expected[invalidKind], f.Stage.Apply(in cmd));
            Assert.IsNull(f.Deck.PendingAction); Assert.AreEqual(50, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
        }

        [Test]
        public void RejectedAdmissionRetainsPendingVersionAndReservation()
        {
            var f = new Fixture(); f.Ready(); var card = f.Find("a"); f.Apply(TacticalStageCommandKind.SelectAction, card);
            f.Port.Admit = false; ulong revision = f.Stage.SnapshotRevision; uint version = card.Version;
            Assert.AreEqual(TacticalStageRejection.ExecutionRejected, f.Apply(TacticalStageCommandKind.ConfirmExecution, card));
            Assert.AreEqual(revision, f.Stage.SnapshotRevision); Assert.AreEqual(version, card.Version);
            Assert.AreSame(card, f.Deck.PendingAction); Assert.AreEqual(20, f.Ledger.Reserved);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void ExecutingUsesOnlyPlayerFactsAndReturnsThroughTheExistingDriver()
        {
            var f = new Fixture(); f.Ready(); var card = f.Find("a");
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            f.Port.Observation = new ExecutionObservation(1, true, true, true, true, true);
            for (int frame = 4; frame <= 6; frame++)
            {
                var player = Sample(frame); var opponent = Sample(frame, false, 50);
                f.Stage.OnCompletedSimulationTick(frame, in player, in opponent);
                if (frame == 4) Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            }
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.AreEqual(2ul, f.Stage.TurnIndex); Assert.AreEqual(3, f.Deck.Hand.Count);
            Assert.AreEqual(30, f.Ledger.Current);
        }

        [Test]
        public void RestartRequiresAcceptedStoryCommandAndDoesNotResetCards()
        {
            var f = new Fixture(); f.Ready(); var card = f.Find("a");
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            Assert.AreEqual(TacticalStageRejection.RestartRejected, f.Apply(TacticalStageCommandKind.RequestRestart));
            Assert.AreEqual(0f, Time.timeScale);
            f.AcceptRestart = true;
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.RequestRestart));
            Assert.AreEqual(4, f.RestartBoundary);
            Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health);
            Assert.AreSame(card, f.Deck.PendingAction); Assert.AreEqual(20, f.Ledger.Reserved);
            Assert.AreEqual(0f, Time.timeScale);
            f.Time.RequestResume(TacticalPauseReason.UserMenu);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(TacticalStageRejection.CriticalFlowPending, f.Apply(TacticalStageCommandKind.ConfirmExecution, card));
        }

        [Test]
        public void StoryBridgeCopiesFiltersDeduplicatesAndUnsubscribes()
        {
            var f = new Fixture(); var director = new StoryDirector();
            using (var bridge = new TacticalStoryBridge(director, f.Stage))
            {
                Publish(director, StoryEventKind.StepCompleted, 11, 21, 31);
                Publish(director, StoryEventKind.StepStarted, 12, 21, 31);
                Publish(director, StoryEventKind.StepStarted, 11, 21, 32);
                Assert.AreEqual(0, bridge.PendingCount);
                Publish(director, StoryEventKind.StepStarted, 11, 21, 31);
                Publish(director, StoryEventKind.StepStarted, 11, 21, 31);
                Assert.AreEqual(1, bridge.PendingCount);
                Assert.AreEqual(1ul, f.Stage.SnapshotRevision, "Director callback must only copy records.");
                Assert.AreEqual(1, bridge.Drain());
                f.BindBoth(); f.Tick(1); f.Tick(2); f.Tick(3);
                ulong revision = f.Stage.SnapshotRevision;
                Publish(director, StoryEventKind.StepStarted, 11, 21, 31); bridge.Drain();
                Assert.AreEqual(revision, f.Stage.SnapshotRevision);
                bridge.Dispose(); bridge.Dispose();
                Publish(director, StoryEventKind.StepStarted, 11, 22, 32);
                Assert.AreEqual(0, bridge.PendingCount);
            }
        }

        [Test]
        public void GameSliceSubscriptionsExistBeforeBootstrapAndAreReleasedWithTheSlice()
        {
            int events = 0;
            StoryDirector director = null;
            var options = new CharacterTestSliceOptions
            {
                EnableCharacterSimulation = false,
                CreateCharacterViews = false,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true,
                SubscribeBeforeStoryBootstrap = slice =>
                {
                    director = slice.StoryModule.Director;
                    return director.SubscribeEvents(evt => events++);
                }
            };
            using (var slice = new GameSlice(options))
                Assert.Greater(events, 0, "The constructor's first bootstrap Tick must already have a subscriber.");
            int before = events;
            Publish(director, StoryEventKind.StepStarted, 11, 21, 31);
            Assert.AreEqual(before, events);
        }

        [Test]
        public void ReleasingTacticalControlPreservesTheMenuOwnerAndResources()
        {
            var f = new Fixture(); f.Ready(); var card = f.Find("a");
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            f.Stage.ReleaseControl();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.UserMenu));
            Assert.AreSame(card, f.Deck.PendingAction);
            Assert.AreEqual(20, f.Ledger.Reserved);
            f.Time.RequestResume(TacticalPauseReason.UserMenu);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(TacticalStageRejection.CriticalFlowPending, f.Apply(TacticalStageCommandKind.ConfirmExecution, card));
        }

        [Test]
        public void ReleasedSessionCannotChangeAnotherSessionsGateThroughLateCallbacks()
        {
            var old = new Fixture(); old.Ready(); old.Stage.ReleaseControl();
            var current = new Fixture(); current.Ready();
            old.Stage.ReleaseControl();
            Assert.AreEqual(0f, Time.timeScale, "Repeated disposal must not unpause the current session.");
            var card = current.Find("a");
            current.Apply(TacticalStageCommandKind.SelectAction, card);
            current.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            var terminal = new TacticalStoryStepToken(1, 11, 1, 22, 32);
            old.Stage.OnStoryStepStarted(in terminal); old.Tick(4);
            old.Stage.ReportFault(TacticalStageRejection.InvalidSpawnBinding);
            Assert.AreEqual(1f, Time.timeScale, "Old completion/fault callbacks must not freeze a new execution.");
        }

        [Test]
        public void StoryTerminalFreezesOnlyAtTheCompleteFrameAndNeverRefills()
        {
            var f = new Fixture(); f.Ready(); var card = f.Find("a");
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            var director = new StoryDirector();
            using (var bridge = new TacticalStoryBridge(director, f.Stage))
            {
                Publish(director, StoryEventKind.StepStarted, 11, 22, 32); bridge.Drain();
                Assert.AreEqual(1f, Time.timeScale);
                Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health);
                f.Tick(4);
                Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
                Assert.AreEqual(0f, Time.timeScale);
                Assert.AreSame(card, f.Deck.ResolvingAction); Assert.AreEqual(2, f.Deck.Hand.Count);
                f.Tick(5); f.Start(); Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            }
        }

        [Test]
        public void StoryBufferOverflowIsAnExplicitFaultOutsideTheCallback()
        {
            var f = new Fixture(); var director = new StoryDirector();
            using (var bridge = new TacticalStoryBridge(director, f.Stage, 1))
            {
                Publish(director, StoryEventKind.StepStarted, 11, 21, 31);
                Publish(director, StoryEventKind.StepStarted, 11, 22, 32);
                Assert.AreEqual(TacticalStageHealth.Running, f.Stage.Health);
                Assert.AreEqual(0, bridge.Drain());
                Assert.AreEqual(TacticalStageRejection.StoryBufferOverflow, f.Stage.Fault);
            }
        }

        [Test]
        public void CommandAndReceiptCapacityNeverOverwriteAcceptedCommands()
        {
            var f = new Fixture(); f.Ready(); var pump = new TacticalCommandPump(f.Stage, 2);
            var cmd = f.Command(TacticalStageCommandKind.RequestRecovery);
            Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(in cmd, 0, 1));
            Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(in cmd, 0, 2));
            Assert.AreEqual(TacticalStageRejection.QueueFull, pump.TrySubmit(in cmd, 0, 3));
            Assert.AreEqual(2ul, pump.GetLastAcceptedSequence(0));
            Assert.AreEqual(2, pump.Drain());
            Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(in cmd, 0, 3));
            Assert.AreEqual(0, pump.Drain(), "Unread receipts apply backpressure.");
            Assert.AreEqual(1, pump.PendingCount);
            Assert.IsTrue(pump.TryReadReceipt(out var first)); Assert.AreEqual(1ul, first.Submitted.Sequence);
            Assert.AreEqual(1, pump.Drain());
            Assert.IsTrue(pump.TryReadReceipt(out var second)); Assert.AreEqual(2ul, second.Submitted.Sequence);
            Assert.IsTrue(pump.TryReadReceipt(out var third)); Assert.AreEqual(3ul, third.Submitted.Sequence);
            Assert.AreEqual(TacticalStageRejection.InvalidSequence, pump.TrySubmit(in cmd, 0, 3));
        }

        [Test]
        public void CommandOrderAndWideIdentitiesArePreservedWithoutWallTime()
        {
            var f = new Fixture(); f.Ready(); var pump = new TacticalCommandPump(f.Stage);
            var command = new TacticalStageCommand(ulong.MaxValue, ulong.MaxValue - 1, ulong.MaxValue - 2,
                uint.MaxValue, ulong.MaxValue - 3, TacticalStageCommandKind.SelectAction);
            ulong sequence = (ulong)int.MaxValue + 10;
            var later = new TacticalQueuedCommand(5, 0, sequence, in command);
            var second = new TacticalQueuedCommand(4, 1, sequence, in command);
            var first = new TacticalQueuedCommand(4, 0, sequence + 1, in command);
            Assert.AreEqual(TacticalStageRejection.None, pump.TryEnqueue(in later));
            Assert.AreEqual(TacticalStageRejection.None, pump.TryEnqueue(in second));
            Assert.AreEqual(TacticalStageRejection.None, pump.TryEnqueue(in first));
            Assert.AreEqual(2, pump.Drain());
            Assert.IsTrue(pump.TryReadReceipt(out var receipt));
            Assert.AreEqual(0, receipt.Submitted.Source); Assert.AreEqual(sequence + 1, receipt.Submitted.Sequence);
            Assert.AreEqual(ulong.MaxValue - 2, receipt.Submitted.Command.CardInstanceId);
            Assert.AreEqual(uint.MaxValue, receipt.Submitted.Command.CardVersion);
            Assert.IsTrue(pump.TryReadReceipt(out receipt)); Assert.AreEqual(1, receipt.Submitted.Source);
            Assert.AreEqual(1, pump.PendingCount); Assert.AreEqual(5, pump.GetPending(0).WorldBoundary);
        }

        [Test]
        public void ColdSetupRejectsUsedSessionsAndDriverFromAnotherLedger()
        {
            var f = new Fixture();
            var foreign = new TacticalSingleCardDriver(f.Port, f.Time, f.Deck, new MomentumLedger());
            Assert.Throws<ArgumentException>(() => f.CreateStage(foreign));
            f.Ready(); Assert.Throws<ArgumentException>(() => f.CreateStage(f.Driver));
        }

        private static void Publish(StoryDirector director, StoryEventKind kind, int graph, int step, int action)
        {
            var evt = new StoryEvent(kind, graphId: graph, beatInstanceId: 1, stepId: step, auxId: action);
            director.Events.Publish(in evt);
        }

        private static SettleSample Sample(long frame, bool support = true, float linear = 0)
            => new SettleSample(frame, false, true, true, false, !support, false, true, true,
                support, false, linear, 0, noActionStarted: true);

        private sealed class Fixture
        {
            public readonly GameplayEntityId Player = new GameplayEntityId(1, 1);
            public readonly GameplayEntityId Opponent = new GameplayEntityId(2, 1);
            public readonly TacticalDeckManager Deck = new TacticalDeckManager();
            public readonly MomentumLedger Ledger = new MomentumLedger(7);
            public readonly TacticalTimeCoordinator Time;
            public readonly Port Port = new Port();
            public readonly TacticalSingleCardDriver Driver;
            public readonly TacticalStageFlowCoordinator Stage;
            public bool EntitiesAlive = true, AcceptRestart;
            public long RestartBoundary;
            private readonly int _budget;
            private readonly TacticalCardDefinition[] _definitions =
            {
                new TacticalActionCardDefinition("a", "A", "", 20,
                    TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange),
                new TacticalActionCardDefinition("b", "B", "", 10,
                    TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange),
                new TacticalUtilityCardDefinition("utility", "Utility", "", 0, new AddMomentumPayload(30))
            };

            public Fixture(int budget = 300, ITacticalTimeAdapter adapter = null)
            {
                _budget = budget;
                Time = new TacticalTimeCoordinator(adapter ?? new UnityTacticalTimeAdapter(), new SettleDetector(new SettleDetectorConfig(requiredStableTicks: 3)));
                Driver = new TacticalSingleCardDriver(Port, Time, Deck, Ledger, executionBudgetTicks: 300);
                Stage = CreateStage(Driver);
            }

            public TacticalStageFlowCoordinator CreateStage(TacticalSingleCardDriver driver)
                => new TacticalStageFlowCoordinator(1, Time, Deck, Ledger, driver, _definitions, 17,
                    id => EntitiesAlive && (id == Player || id == Opponent), new TacticalStoryStepBinding(11, 21, 31),
                    _budget, new[] { new TacticalStoryStepBinding(11, 22, 32) },
                    (run, boundary) => { RestartBoundary = boundary; return AcceptRestart; });
            public void Start() { var token = new TacticalStoryStepToken(1, 11, 1, 21, 31); Stage.OnStoryStepStarted(in token); }
            public void BindBoth() { Stage.OnSpawnBound(1, 0, Player, 0); Stage.OnSpawnBound(1, 1, Opponent, 0); }
            public void Tick(long frame) { var sample = Sample(frame); Stage.OnCompletedSimulationTick(frame, in sample, in sample); }
            public void Ready() { Start(); BindBoth(); Tick(1); Tick(2); Tick(3); }
            public TacticalCardInstance Find(string id)
            {
                for (int i = 0; i < Deck.Hand.Count; i++) if (Deck.Hand[i].Definition.Id == id) return Deck.Hand[i];
                throw new InvalidOperationException(id);
            }
            public TacticalStageCommand Command(TacticalStageCommandKind kind, TacticalCardInstance card = null, ulong revision = 0)
                => new TacticalStageCommand(1, Stage.TurnIndex, card?.InstanceId ?? 0, card?.Version ?? 0,
                    revision == 0 ? Stage.SnapshotRevision : revision, kind);
            public TacticalStageRejection Apply(TacticalStageCommandKind kind, TacticalCardInstance card = null)
            { var cmd = Command(kind, card); return Stage.Apply(in cmd); }
        }

        private sealed class Port : ITacticalExecutionPort
        {
            public bool Admit = true;
            public int Admissions;
            public ExecutionObservation Observation = new ExecutionObservation(1, false, false, false, false, false);
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue)
            { issue = Admit ? "" : "test:Rejected"; executionId = Admit ? (ulong)++Admissions : 0; return Admit; }
            public ExecutionObservation Observe(ulong executionId) => Observation;
            public bool TryAbortPreAction(ulong executionId, string reason) => true;
        }

        private sealed class SpawnPort : ICharacterTestCharacterSpawnFlow
        {
            public int Requests;
            public bool RequestSpawn(CharacterTestCharacterSpawnSpec spec) => ++Requests == 1;
        }
    }
}
