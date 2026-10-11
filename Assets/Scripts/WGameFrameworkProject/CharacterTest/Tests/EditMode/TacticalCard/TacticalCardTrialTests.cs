using System;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalCardTrialTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(7)]
        public void TrialDealsConfiguredCapacityAndResetsDeterministically(int capacity)
        {
            var first = new Fixture(capacity, 50, 73);
            var second = new Fixture(capacity, 50, 73);
            Assert.AreEqual(capacity, first.Deck.Hand.Count);
            CollectionAssert.AreEqual(first.Deck.Hand.Select(c => c.Definition.Id), second.Deck.Hand.Select(c => c.Definition.Id));
            Assert.AreEqual(50, first.Ledger.Current);
            Assert.IsTrue(first.Time.IsWorldPaused);
        }

        [Test]
        public void ProductionStageStillRejectsNonDefaultCapacityWithoutTrialOptIn()
        {
            var deck = new TacticalDeckManager(4);
            var ledger = new MomentumLedger();
            var time = new TacticalTimeCoordinator(new Gate(), new SettleDetector(new SettleDetectorConfig(3)));
            var driver = new TacticalSingleCardDriver(new Port(), time, deck, ledger);
            Assert.Throws<ArgumentException>(() => new TacticalStageFlowCoordinator(1, time, deck, ledger, driver,
                Definitions(4), 73, _ => true, new TacticalStoryStepBinding(11, 21, 31)));
        }

        [Test]
        public void PreparationUsesReservationAndCommitsOnceWithoutWorldAdvanceOrRefill()
        {
            var f = new Fixture(); var card = f.Find("prepare");
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.SelectAction, card));
            Assert.AreEqual(50, f.Ledger.Current); Assert.AreEqual(5, f.Ledger.Reserved);
            Assert.AreEqual(45, f.Ledger.Spendable); Assert.AreEqual(0, f.Deck.PaidCardMomentum);
            var confirm = f.Command(TacticalStageCommandKind.ConfirmExecution, card);
            ulong turn = f.Stage.TurnIndex; long frame = f.Stage.LastCompletedFrame;
            Assert.AreEqual(TacticalStageRejection.None, f.Stage.Apply(in confirm));
            Assert.AreEqual(45, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.AreEqual(5, f.Deck.PaidCardMomentum); Assert.AreEqual(CardZone.ExhaustPile, card.Zone);
            Assert.AreEqual(3, f.Deck.Hand.Count); Assert.AreEqual(1, f.Deck.DrawPile.Count);
            Assert.AreEqual(turn, f.Stage.TurnIndex); Assert.AreEqual(frame, f.Stage.LastCompletedFrame);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase); Assert.IsTrue(f.Time.IsWorldPaused);
            Assert.AreEqual(0, f.Port.Admissions); Assert.IsTrue(f.Stage.TestPreparation.IsActive);
            Assert.AreEqual(f.Player, f.Stage.TestPreparation.Actor);
            Assert.AreEqual(TacticalStageRejection.StaleSnapshot, f.Stage.Apply(in confirm));
            Assert.AreEqual(45, f.Ledger.Current); Assert.AreEqual(1, f.Deck.ExhaustPile.Count);
        }

        [Test]
        public void CancellingPreparationReturnsItsReservationAndCard()
        {
            var f = new Fixture(); var card = f.Find("prepare");
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.CancelPending, card));
            Assert.AreEqual(50, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.AreEqual(CardZone.Hand, card.Zone); Assert.AreEqual(4, f.Deck.Hand.Count);
            Assert.IsFalse(f.Stage.TestPreparation.IsActive); Assert.AreEqual(0, f.Deck.PaidCardMomentum);
        }

        [Test]
        public void PreparationSurvivesActionCancellationAndNormalActionSettle()
        {
            var f = new Fixture(); f.Prepare(); var action = f.Find("action");
            var owner = f.Stage.TestPreparation;
            f.Apply(TacticalStageCommandKind.SelectAction, action);
            Assert.AreEqual(20, f.Ledger.Reserved);
            f.Apply(TacticalStageCommandKind.CancelPending, action);
            Assert.AreEqual(45, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.AreEqual(owner.ActivationRevision, f.Stage.TestPreparation.ActivationRevision);
            f.Apply(TacticalStageCommandKind.SelectAction, action);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.ConfirmExecution, action));
            Assert.IsFalse(f.Time.IsWorldPaused); Assert.AreEqual(25, f.Ledger.Current);
            Assert.AreEqual(25, f.Deck.PaidCardMomentum); Assert.AreEqual(1, f.Port.Admissions);
            f.Port.Observation = new ExecutionObservation(1, true, true, true, true, true);
            for (long frame = 4; frame <= 6; frame++) f.Tick(frame);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.IsTrue(f.Time.IsWorldPaused); Assert.AreEqual(4, f.Deck.Hand.Count);
            Assert.AreEqual(owner.ActivationRevision, f.Stage.TestPreparation.ActivationRevision);
        }

        [Test]
        public void InsufficientMomentumAndInvalidActorDoNotPayOrActivate()
        {
            var poor = new Fixture(momentum: 4); var card = poor.Find("prepare");
            Assert.AreEqual(TacticalStageRejection.InsufficientMomentum, poor.Apply(TacticalStageCommandKind.SelectAction, card));
            Assert.AreEqual(CardZone.Hand, card.Zone); Assert.AreEqual(4, poor.Ledger.Current);
            var stale = new Fixture(); card = stale.Find("prepare");
            stale.Apply(TacticalStageCommandKind.SelectAction, card); stale.ActorCurrent = false;
            Assert.AreEqual(TacticalStageRejection.InvalidSpawnBinding, stale.Apply(TacticalStageCommandKind.ConfirmExecution, card));
            Assert.AreEqual(50, stale.Ledger.Current); Assert.AreEqual(5, stale.Ledger.Reserved);
            Assert.IsFalse(stale.Stage.TestPreparation.IsActive); Assert.AreEqual(0, stale.Deck.PaidCardMomentum);
            stale.Apply(TacticalStageCommandKind.CancelPending, card);
            Assert.AreEqual(0, stale.Ledger.Reserved);
        }

        [Test]
        public void SecondPreparationAndDirectUtilityBypassAreRejected()
        {
            var f = new Fixture(); var card = f.Find("prepare");
            Assert.AreEqual(CardPlayFailure.UnsupportedPayload, f.Deck.PlayUtilityCard(card, f.Ledger));
            f.Prepare(); var duplicate = f.Find("prepare-again");
            Assert.AreEqual(TacticalStageRejection.PreparationAlreadyActive, f.Apply(TacticalStageCommandKind.SelectAction, duplicate));
            Assert.AreEqual(45, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.AreEqual(CardZone.Hand, duplicate.Zone); Assert.AreEqual(1, f.Deck.ExhaustPile.Count);
        }

        [Test]
        public void CommandPumpProcessesPreparationAtSameWorldBoundaryAndRejectsRepeatedSequence()
        {
            var f = new Fixture(); var pump = new TacticalCommandPump(f.Stage); var card = f.Find("prepare");
            var select = f.Command(TacticalStageCommandKind.SelectAction, card);
            Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(in select, 0, 1)); pump.Drain();
            var confirm = f.Command(TacticalStageCommandKind.ConfirmExecution, card);
            Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(in confirm, 0, 2)); pump.Drain();
            Assert.AreEqual(TacticalStageRejection.InvalidSequence, pump.TrySubmit(in confirm, 0, 2));
            Assert.AreEqual(3, f.Stage.LastCompletedFrame); Assert.AreEqual(45, f.Ledger.Current);
            Assert.IsTrue(pump.TryReadReceipt(out var a)); Assert.IsTrue(pump.TryReadReceipt(out var b));
            Assert.AreEqual(a.Submitted.WorldBoundary, b.Submitted.WorldBoundary);
            Assert.Greater(b.SnapshotRevision, a.SnapshotRevision);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SessionEndRebindAndReleaseClearPreparationWithoutLeakingToNewActor(int cleanup)
        {
            var f = new Fixture(); f.Prepare();
            if (cleanup == 0) f.Stage.CompleteExternalOutcome(3);
            else if (cleanup == 1) f.Stage.RebindDeck(Definitions(4));
            else f.Stage.ReleaseControl();
            Assert.IsFalse(f.Stage.TestPreparation.IsActive);
            var next = new Fixture(actorIndex: 7);
            Assert.IsFalse(next.Stage.TestPreparation.IsActive); next.Prepare();
            Assert.AreEqual(next.Player, next.Stage.TestPreparation.Actor);
        }

        private static TacticalCardDefinition[] Definitions(int capacity)
        {
            var definitions = new TacticalCardDefinition[Math.Max(5, capacity + 1)];
            definitions[0] = new TacticalUtilityCardDefinition("prepare", "测试准备（无保命效果）", "仅事务观察", 5,
                new TestPreparationPayload(), CardDestination.Exhaust);
            definitions[1] = new TacticalActionCardDefinition("action", "Action", "", 20, TacticalActionSlot.PrimaryAttack,
                0, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
            definitions[2] = new TacticalUtilityCardDefinition("prepare-again", "测试准备（重复实例）", "", 5, new TestPreparationPayload());
            for (int i = 3; i < definitions.Length; i++)
                definitions[i] = new TacticalUtilityCardDefinition("filler-" + i, "Utility", "", 0, new AddMomentumPayload(0));
            return definitions;
        }

        private sealed class Fixture
        {
            internal readonly GameplayEntityId Player, Opponent;
            internal readonly TacticalDeckManager Deck;
            internal readonly MomentumLedger Ledger = new MomentumLedger();
            internal readonly TacticalTimeCoordinator Time;
            internal readonly Port Port = new Port();
            internal readonly TacticalStageFlowCoordinator Stage;
            internal bool ActorCurrent = true;
            internal Fixture(int capacity = 4, int momentum = 50, int seed = 73, int actorIndex = 1)
            {
                Player = new GameplayEntityId(actorIndex, 1); Opponent = new GameplayEntityId(actorIndex + 1, 1);
                Deck = new TacticalDeckManager(capacity, enableTestPreparation: true);
                Time = new TacticalTimeCoordinator(new Gate(), new SettleDetector(new SettleDetectorConfig(3)));
                var driver = new TacticalSingleCardDriver(Port, Time, Deck, Ledger);
                Stage = new TacticalStageFlowCoordinator(1, Time, Deck, Ledger, driver, Definitions(capacity), seed,
                    id => ActorCurrent && (id == Player || id == Opponent), new TacticalStoryStepBinding(11, 21, 31),
                    initialMomentum: momentum, allowConfigurableHandCapacity: true, configuredInitialHandCount: capacity);
                var opening = new TacticalStoryStepToken(1, 11, 1, 21, 31); Stage.OnStoryStepStarted(in opening);
                Stage.OnSpawnBound(1, 0, Player, 0); Stage.OnSpawnBound(1, 1, Opponent, 0);
                Tick(1); Tick(2); Tick(3);
            }
            internal TacticalCardInstance Find(string id) => Deck.Hand.Single(c => c.Definition.Id == id);
            internal TacticalStageCommand Command(TacticalStageCommandKind kind, TacticalCardInstance card) =>
                new TacticalStageCommand(1, Stage.TurnIndex, card.InstanceId, card.Version, Stage.SnapshotRevision, kind);
            internal TacticalStageRejection Apply(TacticalStageCommandKind kind, TacticalCardInstance card)
            { var command = Command(kind, card); return Stage.Apply(in command); }
            internal void Prepare()
            { var card = Find("prepare"); Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.SelectAction, card));
                Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.ConfirmExecution, card)); }
            internal void Tick(long frame)
            { var sample = new SettleSample(frame, false, true, true, false, false, false, true, true, true,
                false, 0, 0, noActionStarted: true); Stage.OnCompletedSimulationTick(frame, in sample, in sample); }
        }
        private sealed class Gate : ITacticalTimeAdapter
        { public bool IsSimulationPaused { get; private set; } public void SetSimulationPaused(bool paused) => IsSimulationPaused = paused; }
        private sealed class Port : ITacticalExecutionPort
        {
            internal int Admissions;
            internal ExecutionObservation Observation = new ExecutionObservation(1, false, false, false, false, false);
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue)
            { issue = ""; executionId = (ulong)++Admissions; return true; }
            public ExecutionObservation Observe(ulong executionId) => Observation;
            public bool TryAbortPreAction(ulong executionId, string reason) => true;
        }
    }
}
