using System;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalTimeCoordinatorTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void NormalTurnPauseRequiresTerminalBehaviorAndFreePlayerWithoutPhysicalRest(bool terminal, bool free)
        {
            var sample = new SettleSample(1, actionTimelineCompleted: true, behaviorTerminal: terminal,
                neutralPoseReached: true, hasReactionOrRecovery: false, hasPendingLanding: true,
                hasPendingMotionOrImpulse: false, ownedIntentsReleased: true, commandsAcknowledged: true,
                hasStableSupport: false, hasNewDestabilizingContact: false, relativeLinearSpeed: 10f,
                relativeAngularSpeed: 10f, playerCanActFreely: free);
            Assert.That(new TacticalTurnPauseDetector().Sample(sample), Is.EqualTo(terminal && free));
        }

        private sealed class TimeAdapter : ITacticalTimeAdapter
        {
            public bool IsSimulationPaused { get; private set; }
            public void SetSimulationPaused(bool paused) => IsSimulationPaused = paused;
        }

        private static SettleSample Sample(long tick, int blockedFact = -1, float linear = 0,
            float angular = 0, bool noActionStarted = false)
        {
            return new SettleSample(tick, blockedFact != 0, blockedFact != 1, blockedFact != 2,
                blockedFact == 3, blockedFact == 4, blockedFact == 5, blockedFact != 6,
                blockedFact != 7, blockedFact != 8, blockedFact == 9, linear, angular, noActionStarted);
        }

        private static TacticalDeckManager Deck()
        {
            var action = new TacticalActionCardDefinition("attack", "Attack", "", 0,
                TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.None, TacticalApproachMode.None,
                20);
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new[] { action, action, action, action, action }, 23);
            return deck;
        }

        private static TacticalTimeCoordinator Coordinator()
        {
            var coordinator = new TacticalTimeCoordinator(new TimeAdapter());
            coordinator.Initialize();
            return coordinator;
        }

        private static void Commit(TacticalTimeCoordinator coordinator, TacticalDeckManager deck, IMomentumLedger ledger)
        {
            Assert.That(deck.SetPendingAction(deck.Hand[0], ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(coordinator.TryCommitTurn(deck, ledger, out _), Is.True);
        }

        [Test]
        public void Settle_RequiresEightConsecutiveTicks_AndResetStartsFresh()
        {
            ISettleDetector detector = new SettleDetector();
            for (int tick = 1; tick < 8; tick++) Assert.That(detector.Sample(Sample(tick)), Is.False);
            Assert.That(detector.Sample(Sample(8)), Is.True);
            Assert.That(detector.Sample(Sample(9)), Is.True);
            detector.Reset();
            Assert.That(detector.Sample(Sample(1)), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void Settle_EverySemanticBlockOnSeventhTick_RestartsWindow(int blockedFact)
        {
            var detector = new SettleDetector();
            for (int tick = 1; tick <= 6; tick++) detector.Sample(Sample(tick));
            Assert.That(detector.Sample(Sample(7, blockedFact)), Is.False);
            for (int tick = 8; tick < 15; tick++) Assert.That(detector.Sample(Sample(tick)), Is.False);
            Assert.That(detector.Sample(Sample(15)), Is.True);
        }

        [TestCase(0.151f, 0f)] [TestCase(0f, 0.301f)]
        [TestCase(float.NaN, 0f)] [TestCase(0f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0f)] [TestCase(0f, float.PositiveInfinity)]
        [TestCase(-1f, 0f)] [TestCase(0f, -1f)]
        public void Settle_ExcessiveOrInvalidSpeedOnSeventhTick_RestartsWindow(float linear, float angular)
        {
            var detector = new SettleDetector();
            for (int tick = 1; tick <= 6; tick++) detector.Sample(Sample(tick));
            Assert.That(detector.Sample(Sample(7, linear: linear, angular: angular)), Is.False);
            for (int tick = 8; tick < 15; tick++) Assert.That(detector.Sample(Sample(tick)), Is.False);
            Assert.That(detector.Sample(Sample(15)), Is.True);
        }

        [Test]
        public void Settle_AirborneApexNeverSettlesDespiteZeroSpeed()
        {
            var detector = new SettleDetector();
            for (int tick = 1; tick <= 100; tick++) Assert.That(detector.Sample(Sample(tick, 4)), Is.False);
        }

        [Test]
        public void Settle_HysteresisUsesStrictEntryAndInclusiveExitThresholds()
        {
            var detector = new SettleDetector();
            for (int tick = 1; tick <= 8; tick++)
                Assert.That(detector.Sample(Sample(tick, linear: 0.1f, angular: 0.2f)), Is.False);
            Assert.That(detector.Sample(Sample(9, linear: 0.08f, angular: 0.15f)), Is.False);
            for (int tick = 10; tick < 16; tick++)
                Assert.That(detector.Sample(Sample(tick, linear: 0.15f, angular: 0.30f)), Is.False);
            Assert.That(detector.Sample(Sample(16, linear: 0.15f, angular: 0.30f)), Is.True);
        }

        [TestCase(6)] [TestCase(5)] [TestCase(8)]
        public void Settle_DuplicateReversedOrMissingTickCannotCompleteOldWindow(long nextTick)
        {
            var detector = new SettleDetector();
            for (int tick = 1; tick <= 6; tick++) detector.Sample(Sample(tick));
            Assert.That(detector.Sample(Sample(nextTick)), Is.False);
            Assert.That(detector.Sample(Sample(9)), Is.False);
        }

        [Test]
        public void Settle_InvalidConfigurationRejected_AndCustomWindowHonored()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetectorConfig(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetectorConfig(enterLinearSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetectorConfig(exitLinearSpeed: 0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetectorConfig(enterAngularSpeed: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettleDetectorConfig(exitAngularSpeed: float.PositiveInfinity));
            Assert.That(new SettleDetector(new SettleDetectorConfig(1)).Sample(Sample(1)), Is.True);
        }

        [Test]
        public void CompleteTurn_PausesRefillsRetainsCardsAndAdvancesExactlyOnce()
        {
            var deck = Deck();
            var ledger = new MomentumLedger(40);
            var coordinator = Coordinator();
            var retained = deck.Hand[1];
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.ReadyPaused));
            Assert.That(coordinator.IsWorldPaused, Is.True);
            Commit(coordinator, deck, ledger);
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.Executing));
            Assert.That(coordinator.IsWorldPaused, Is.False);
            Assert.That(deck.Hand.Count, Is.EqualTo(2));
            Assert.That(coordinator.TryCommitTurn(deck, ledger, out _), Is.False);
            Assert.That(coordinator.EvaluateSettle(Sample(1), deck), Is.False);
            coordinator.NotifyActionStarted();
            coordinator.NotifyExecutionTerminal();
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.AwaitingSettle));
            for (int tick = 1; tick < 8; tick++) Assert.That(coordinator.EvaluateSettle(Sample(tick), deck), Is.False);
            Assert.That(coordinator.EvaluateSettle(Sample(8), deck), Is.True);
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.ReadyPaused));
            Assert.That(coordinator.IsWorldPaused, Is.True);
            Assert.That(coordinator.CurrentTurnIndex, Is.EqualTo(2UL));
            Assert.That(deck.Hand.Count, Is.EqualTo(3));
            Assert.That(deck.Hand[0], Is.SameAs(retained));
            Assert.That(deck.ResolvingAction, Is.Null);
            Assert.That(ledger.Current, Is.EqualTo(40));
            Assert.That(coordinator.EvaluateSettle(Sample(9), deck), Is.False);
            coordinator.NotifyExecutionTerminal();
            Assert.That(coordinator.CurrentTurnIndex, Is.EqualTo(2UL));
        }

        [Test]
        public void MenuOwnerSurvivesReadyResumeAndCommit_AndCannotFabricateSettleTicks()
        {
            var coordinator = Coordinator();
            var deck = Deck();
            coordinator.RequestPause(TacticalPauseReason.UserMenu);
            coordinator.RequestPause(TacticalPauseReason.UserMenu);
            coordinator.RequestResume(TacticalPauseReason.Ready);
            Assert.That(coordinator.IsWorldPaused, Is.True);
            Commit(coordinator, deck, new MomentumLedger());
            Assert.That(coordinator.IsWorldPaused, Is.True);
            coordinator.NotifyExecutionTerminal();
            for (int tick = 1; tick <= 8; tick++) Assert.That(coordinator.EvaluateSettle(Sample(tick), deck), Is.False);
            coordinator.RequestResume(TacticalPauseReason.UserMenu);
            Assert.That(coordinator.IsWorldPaused, Is.False);
            Assert.That(coordinator.EvaluateSettle(Sample(9), deck), Is.False);
        }

        [TestCase(TacticalPauseReason.RecoveryTimeout)]
        [TestCase(TacticalPauseReason.ExecutionFault)]
        [TestCase(TacticalPauseReason.BattleEnded)]
        public void SuspensionPreservesReceiptQuotaAndTurn_UntilItsOwnerResumes(TacticalPauseReason reason)
        {
            var coordinator = Coordinator();
            var deck = Deck();
            var ledger = new MomentumLedger();
            Commit(coordinator, deck, ledger);
            var resolving = deck.ResolvingAction;
            coordinator.NotifyExecutionTerminal();
            for (int tick = 1; tick <= 7; tick++) coordinator.EvaluateSettle(Sample(tick), deck);
            coordinator.ForceSuspend(reason);
            coordinator.RequestResume(TacticalPauseReason.Ready);
            coordinator.Initialize(100);
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.Suspended));
            Assert.That(coordinator.IsWorldPaused, Is.True);
            Assert.That(coordinator.EvaluateSettle(Sample(8), deck), Is.False);
            Assert.That(coordinator.TryCommitTurn(deck, ledger, out _), Is.False);
            Assert.That(deck.ResolvingAction, Is.SameAs(resolving));
            Assert.That(deck.Hand.Count, Is.EqualTo(2));
            Assert.That(coordinator.CurrentTurnIndex, Is.EqualTo(1UL));
            coordinator.RequestResume(reason);
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.AwaitingSettle));
            for (int tick = 9; tick < 16; tick++) Assert.That(coordinator.EvaluateSettle(Sample(tick), deck), Is.False);
            Assert.That(coordinator.EvaluateSettle(Sample(16), deck), Is.True);
        }

        [Test]
        public void FailedCommitAndReadyResumeDoNotUnfreezeOrSpend()
        {
            var coordinator = Coordinator();
            var deck = Deck();
            var ledger = new MomentumLedger(40);
            Assert.That(coordinator.TryCommitTurn(deck, ledger, out var card), Is.False);
            Assert.That(card, Is.Null);
            coordinator.RequestResume(TacticalPauseReason.Ready);
            Assert.That(coordinator.IsWorldPaused, Is.True);
            Assert.That(deck.Hand.Count, Is.EqualTo(3));
            Assert.That(ledger.Current, Is.EqualTo(40));
        }

        [Test]
        public void WrongDeckAndStaleCompletionReceiptCannotCompleteTurn()
        {
            var coordinator = Coordinator();
            var deck = Deck();
            var foreign = Deck();
            var ledger = new MomentumLedger();
            Commit(coordinator, deck, ledger);
            coordinator.NotifyExecutionTerminal();
            for (int tick = 1; tick <= 8; tick++) Assert.That(coordinator.EvaluateSettle(Sample(tick), foreign), Is.False);
            var old = deck.ResolvingAction;
            deck.CompleteTurn(old.InstanceId, old.Version);
            deck.SetPendingAction(deck.Hand[0], ledger);
            deck.CommitTurn(ledger, out var next);
            for (int tick = 9; tick <= 16; tick++) Assert.That(coordinator.EvaluateSettle(Sample(tick), deck), Is.False);
            Assert.That(coordinator.Phase, Is.EqualTo(TacticalTurnPhase.Suspended));
            Assert.That(deck.ResolvingAction, Is.SameAs(next));
            Assert.That(coordinator.CurrentTurnIndex, Is.EqualTo(1UL));
        }

        [TestCase(false)] [TestCase(true)]
        public void NoActionStartedRequiresExplicitFact_AndIsRejectedAfterRealStart(bool started)
        {
            var coordinator = Coordinator();
            var deck = Deck();
            Commit(coordinator, deck, new MomentumLedger());
            if (started) coordinator.NotifyActionStarted();
            coordinator.NotifyExecutionTerminal();
            for (int tick = 1; tick <= 8; tick++)
                Assert.That(coordinator.EvaluateSettle(Sample(tick, 0), deck), Is.False);
            for (int tick = 9; tick <= 16; tick++)
                Assert.That(coordinator.EvaluateSettle(Sample(tick, 0, noActionStarted: true), deck),
                    Is.EqualTo(!started && tick == 16));
        }

        [Test]
        public void WarmSamplingAndFullTurnLoops_AllocateZeroBytes()
        {
            var deck = Deck();
            var ledger = new MomentumLedger();
            var coordinator = Coordinator();
            Assert.That(RunTurns(coordinator, deck, ledger, 10), Is.True);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool success = RunTurns(coordinator, deck, ledger, 1000);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(success, Is.True);
            Assert.That(allocated, Is.Zero);
        }

        private static bool RunTurns(TacticalTimeCoordinator coordinator, TacticalDeckManager deck,
            IMomentumLedger ledger, int count)
        {
            for (int turn = 0; turn < count; turn++)
            {
                if (deck.SetPendingAction(deck.Hand[0], ledger) != CardPlayFailure.None) return false;
                coordinator.RequestPause(TacticalPauseReason.UserMenu);
                if (!coordinator.TryCommitTurn(deck, ledger, out _)) return false;
                coordinator.RequestResume(TacticalPauseReason.UserMenu);
                coordinator.NotifyActionStarted();
                coordinator.NotifyExecutionTerminal();
                coordinator.ForceSuspend(TacticalPauseReason.RecoveryTimeout);
                coordinator.RequestResume(TacticalPauseReason.RecoveryTimeout);
                for (int tick = 1; tick <= 8; tick++)
                    if (coordinator.EvaluateSettle(Sample(tick), deck) != (tick == 8)) return false;
            }
            return true;
        }
    }
}
