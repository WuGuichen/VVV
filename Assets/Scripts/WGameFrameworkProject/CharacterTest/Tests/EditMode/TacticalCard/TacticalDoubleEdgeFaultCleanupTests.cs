using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Real Driver/Deck/Time/payment path with either an already accepted or a delayed stop ACK.</summary>
    public sealed class TacticalDoubleEdgeFaultCleanupTests
    {
        private sealed class TimePort : ITacticalTimeAdapter
        {
            public bool IsSimulationPaused { get; private set; }
            public void SetSimulationPaused(bool value) => IsSimulationPaused = value;
        }
        private sealed class Port : ITacticalExecutionPort, ITacticalFixedExecutionPort, ITacticalFixedCleanupPort, ITacticalFixedTerminalPort
        {
            internal readonly TacticalInteractionEffectAuthority Effects = new TacticalInteractionEffectAuthority();
            internal readonly TacticalDoubleEdgeBattleState State = new TacticalDoubleEdgeBattleState();
            internal ExecutionObservation Observation;
            internal TacticalFixedCardPaymentCommit Paid;
            internal int CancelCount, FinishCount, AbortCount;
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return false; }
            public bool TryAdmit(TacticalCardInstance card, out ulong id, out string issue) { id = 0; issue = ""; return false; }
            public ExecutionObservation Observe(ulong id) => Observation;
            public bool IsNaturalFixedCompletion(ulong id) => false;
            public bool TryAbortPreAction(ulong id, string issue)
            { AbortCount++; Observation = new ExecutionObservation(id, false, false, true, false, false, issue); return true; }
            public bool TryCommitFixed(TacticalCardInstance card, TacticalDeckManager deck, TacticalTimeCoordinator time,
                MomentumLedger ledger, in TacticalExecutionInput input, out ulong id, out TacticalFixedCardPaymentCommit paid, out string issue)
            {
                var actor = new GameplayEntityId(1, 1); Effects.Bind(7, 1, actor); State.Bind(7, 1);
                var program = TacticalFixedPaymentCoreTests.OneAction();
                Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => 7, out var proof, out _));
                Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(card.Definition.FixedAssembly,
                    TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions, 7, out var fees));
                Assert.IsTrue(deck.TryPrepareFixedMove(card, ledger, out var move));
                Assert.IsTrue(time.TryPrepareFixedTurn(deck, out var turn));
                Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(card.Definition.FixedAssembly, fees, card, 7, 1,
                    actor, 1, 1, 0, 0, 1, deck.FixedPendingReservation(ledger), input, out var manifest));
                Assert.IsTrue(State.TryPrepare(manifest.DoubleEdgeKey, 1, default, card.Definition.FixedAssembly.DoubleEdgeParameters,
                    false, true, out var state));
                Assert.IsTrue(TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(manifest, card, ledger,
                    Effects, program, proof, out var core, out issue));
                Assert.IsTrue(core.TryPublish(out paid)); Assert.IsTrue(state.TryPublish()); paid.DoubleEdgeState = State;
                Paid = paid; move.Publish(paid); turn.Publish(); id = 1;
                Observation = new ExecutionObservation(id, true, false, false, false, false); return true;
            }
            public void CancelFixed(ulong id)
            { CancelCount++; State.CompleteHost(Paid.Record.DoubleEdgeKey, false, 1, 0, 0, 0); }
            public void SetFixedReleaseObserver(ulong id, Action retired) { }
            public ExecutionObservation ReconcileFixedRelease(ulong id) => Observation;
            public void FinishFixedRelease(ulong id) => FinishCount++;
            internal void ConsumeStopCommand() => Observation = new ExecutionObservation(1, true, false, true, true, true);
        }
        private sealed class Fixture
        {
            internal readonly Port Execution = new Port();
            internal readonly MomentumLedger Ledger = new MomentumLedger(30);
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager(2);
            internal readonly TacticalTimeCoordinator Time = new TacticalTimeCoordinator(new TimePort());
            internal readonly TacticalSingleCardDriver Driver;
            internal readonly TacticalStageFlowCoordinator Stage;
            internal readonly TacticalCardInstance Card, Other;
            internal Fixture(bool preActionTimeout = false)
            {
                var main = TacticalFixedAssemblyManifestTests.Main(); var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
                var definitions = TacticalDoubleEdgePublication.Create(null).Configuration.Accessories;
                snapshot.Accessories = new[] { definitions[1], definitions[2] }; snapshot.Assembly.Accessories.Clear();
                for (int i = 0; i < snapshot.Accessories.Length; i++) snapshot.Assembly.Accessories.Add(
                    new CharacterTacticalAccessorySlot { SlotIndex = i, AccessoryItemInstanceId = 81 + i, Reference = snapshot.Accessories[i].Reference });
                Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, main, 11, 7, 3,
                    new Dictionary<string, int> { { "main.action", 17 } }, out var assembly, out var issue), issue);
                main.AttachFixedAssembly(assembly);
                Driver = new TacticalSingleCardDriver(Execution, Time, Deck, Ledger, preActionBudgetTicks: 1,
                    executionBudgetTicks: preActionTimeout ? 10 : 1);
                Stage = new TacticalStageFlowCoordinator(7, Time, Deck, Ledger, Driver, new[] { main, main }, 1,
                    _ => true, new TacticalStoryStepBinding(1, 1, 1), allowConfigurableHandCapacity: true, configuredInitialHandCount: 2);
                Deck.InitializeDeck(new[] { main, main }, 1, 2); Time.Initialize(); Card = Deck.Hand[0]; Other = Deck.Hand[1];
                Assert.AreEqual(CardPlayFailure.None, Deck.SetPendingAction(Card, Ledger));
                Assert.IsTrue(Driver.TryCommit(0, out issue), issue);
            }
            internal void TickDriver(long tick)
            {
                var sample = new SettleSample(tick, false, false, false, true, false, false, false, false,
                    true, false, 0, 0, false);
                Assert.IsTrue(Stage.CanStep, "The real stage gate must allow the stop consumer to reach its next boundary.");
                Assert.IsFalse(Driver.OnCompletedSimulationTick(sample));
            }
        }

        [TestCase(false)][TestCase(true)]
        public void FreshStageReconcilesThroughIngressWithoutInventingAFault(bool pauseMenuDuringIngress)
        {
            var execution = new Port(); var ledger = new MomentumLedger(7); var deck = new TacticalDeckManager();
            var time = new TacticalTimeCoordinator(new TimePort());
            var driver = new TacticalSingleCardDriver(execution, time, deck, ledger);
            var main = TacticalFixedAssemblyManifestTests.Main();
            var stage = new TacticalStageFlowCoordinator(7, time, deck, ledger, driver, new[] { main, main, main }, 1,
                _ => true, new TacticalStoryStepBinding(1, 1, 1));
            Assert.AreEqual(0UL, time.CurrentTurnIndex); Assert.AreEqual(TacticalTurnPhase.Suspended, time.Phase);
            Assert.AreEqual(0, deck.Hand.Count); Assert.AreEqual(TacticalStagePhase.Booting, stage.Phase);
            for (int i = 0; i < 3; i++) {
                stage.ReconcileReleasedCleanup();
                Assert.AreEqual(TacticalStageHealth.Running, stage.Health); Assert.IsTrue(stage.CanStep);
            }
            var opening = new TacticalStoryStepToken(7, 1, 1, 1, 1); stage.OnStoryStepStarted(in opening);
            stage.OnSpawnBound(7, 0, new GameplayEntityId(1, 1), 0);
            stage.OnSpawnBound(7, 1, new GameplayEntityId(2, 1), 0);
            Assert.AreEqual(TacticalStagePhase.AwaitingIngressSettle, stage.Phase);
            if (pauseMenuDuringIngress) {
                time.RequestPause(TacticalPauseReason.UserMenu);
                for (int i = 0; i < 3; i++) {
                    stage.ReconcileReleasedCleanup();
                    Assert.AreEqual(TacticalStageHealth.Running, stage.Health); Assert.IsFalse(stage.CanStep);
                    Assert.IsTrue(time.HasPauseReason(TacticalPauseReason.UserMenu));
                }
                time.RequestResume(TacticalPauseReason.UserMenu); Assert.IsTrue(stage.CanStep);
            }
            for (long frame = 1; frame <= 3; frame++) {
                var sample = new SettleSample(frame, false, true, true, false, false, false, true, true,
                    true, false, 0, 0, noActionStarted: true);
                // Both real stage runtimes reconcile before delivering the completed world tick.
                stage.ReconcileReleasedCleanup(); stage.OnCompletedSimulationTick(frame, in sample, in sample);
                stage.ReconcileReleasedCleanup(); Assert.AreEqual(TacticalStageHealth.Running, stage.Health);
            }
            Assert.AreEqual(TacticalStagePhase.Planning, stage.Phase); Assert.AreEqual(TacticalTurnPhase.ReadyPaused, time.Phase);
            Assert.AreEqual(1UL, time.CurrentTurnIndex); Assert.AreEqual(3, deck.Hand.Count);
            Assert.AreEqual(TacticalStageRejection.None, stage.Fault); Assert.IsFalse(stage.HasReleasedCleanup);
            Assert.AreEqual(0, execution.CancelCount); Assert.AreEqual(0, execution.FinishCount);
            Assert.IsFalse(time.HasPauseReason(TacticalPauseReason.ExecutionFault));
            Assert.IsFalse(time.HasPauseReason(TacticalPauseReason.RecoveryTimeout));
        }

        [TestCase("timeout")][TestCase("mismatch")][TestCase("terminal")][TestCase("pre-action")]
        public void ActualDriverFaultWaitsForMatchingStopAckThenExhaustsAndSuspends(string trigger)
        {
            var f = new Fixture(trigger == "pre-action"); int paid = f.Ledger.Current;
            if (trigger == "mismatch") f.Execution.Observation = new ExecutionObservation(2, true, false, false, false, false);
            if (trigger == "terminal") f.Execution.Observation = new ExecutionObservation(1, true, false, true, false, false);
            if (trigger == "pre-action") f.Execution.Observation = new ExecutionObservation(1, false, false, false, false, false);
            f.TickDriver(1);
            if (trigger == "pre-action") { Assert.AreEqual(1, f.Execution.AbortCount); f.TickDriver(2); }
            Assert.IsTrue(f.Stage.HasReleasedCleanup); Assert.IsTrue(f.Stage.CanStep);
            Assert.AreEqual(1, f.Execution.CancelCount); Assert.AreEqual(7, f.Ledger.Reserved);
            Assert.AreEqual(CardZone.Resolving, f.Card.Zone); Assert.AreEqual(0, f.Deck.ExhaustPile.Count);
            f.Execution.Observation = new ExecutionObservation(2, true, false, true, true, true);
            f.Stage.ReconcileReleasedCleanup(); Assert.AreEqual(7, f.Ledger.Reserved); Assert.IsTrue(f.Stage.CanStep);
            f.Execution.ConsumeStopCommand(); f.Stage.ReconcileReleasedCleanup();
            Assert.IsFalse(f.Stage.HasReleasedCleanup); Assert.IsFalse(f.Stage.CanStep);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.ExecutionFault)); Assert.IsFalse(f.Stage.CanRequestRecovery);
            if (trigger == "timeout") Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.RecoveryTimeout));
            Assert.AreEqual(0UL, f.Driver.ExecutionId); Assert.AreEqual(0, f.Ledger.Reserved); Assert.AreEqual(paid, f.Ledger.Current);
            Assert.AreEqual(CardZone.ExhaustPile, f.Card.Zone); Assert.AreEqual(CardZone.Hand, f.Other.Zone);
            Assert.IsNull(f.Deck.ResolvingAction); Assert.AreEqual(1, f.Deck.Hand.Count); Assert.AreEqual(1UL, f.Time.CurrentTurnIndex);
            Assert.AreEqual(0, f.Execution.State.OccupiedFieldSlots);
            f.Stage.ReconcileReleasedCleanup(); Assert.AreEqual(1, f.Execution.FinishCount); Assert.AreEqual(1, f.Deck.ExhaustPile.Count);
        }

        [Test] public void FaultCleanupNeverReleasesAnIndependentMenuPause()
        {
            var f = new Fixture(); f.TickDriver(1); f.Time.RequestPause(TacticalPauseReason.UserMenu);
            f.Stage.ReconcileReleasedCleanup(); Assert.IsFalse(f.Stage.CanStep); Assert.AreEqual(7, f.Ledger.Reserved);
            f.Time.RequestResume(TacticalPauseReason.UserMenu); Assert.IsTrue(f.Stage.CanStep);
            f.Execution.ConsumeStopCommand(); f.Stage.ReconcileReleasedCleanup(); Assert.IsFalse(f.Stage.CanStep);
        }

        [Test] public void AlreadyAcknowledgedAbnormalTerminalCompletesCleanupInTheSameDriverCall()
        {
            var f = new Fixture(); int paid = f.Ledger.Current;
            // The real pre-action abort can finish before the Driver observes its non-natural terminal.
            f.Execution.ConsumeStopCommand(); f.TickDriver(1);
            Assert.IsFalse(f.Stage.HasReleasedCleanup, "A pending-cleanup state need not survive the Driver call.");
            Assert.AreEqual(0UL, f.Driver.ExecutionId); Assert.AreEqual(1, f.Execution.CancelCount);
            Assert.AreEqual(1, f.Execution.FinishCount); Assert.IsFalse(f.Stage.CanStep);
            f.Stage.ReconcileReleasedCleanup(); AssertFinalFaultAndRejectedReadmission(f, paid);
        }

        [TestCase(typeof(TacticalMixedPlayerCardRuntime))][TestCase(typeof(TacticalCardStageRuntime))]
        public void ActualStageExecutionPortExposesNaturalTerminalQualification(Type stagePort)
        {
            Assert.IsTrue(typeof(ITacticalExecutionPort).IsAssignableFrom(stagePort));
            Assert.IsTrue(typeof(ITacticalFixedTerminalPort).IsAssignableFrom(stagePort),
                "Driver receives the Stage wrapper. An adapter-only implementation classifies every natural terminal as abnormal.");
            var map = stagePort.GetInterfaceMap(typeof(ITacticalFixedTerminalPort));
            Assert.AreEqual(1, map.TargetMethods.Length);
            Assert.AreEqual(stagePort, map.TargetMethods[0].DeclaringType);
        }

        [TestCase("terminal")][TestCase("owned-intents")][TestCase("commands")]
        public void FaultCleanupRequiresEveryMatchingReceiptFact(string missingFact)
        {
            var f = new Fixture(); int paid = f.Ledger.Current; f.TickDriver(1);
            f.Execution.Observation = new ExecutionObservation(1, true, false,
                missingFact != "terminal", missingFact != "owned-intents", missingFact != "commands");
            f.Stage.ReconcileReleasedCleanup();
            Assert.IsTrue(f.Stage.HasReleasedCleanup); Assert.IsTrue(f.Stage.CanStep);
            Assert.AreEqual(1UL, f.Driver.ExecutionId); Assert.AreEqual(7, f.Ledger.Reserved);
            Assert.AreEqual(CardZone.Resolving, f.Card.Zone); Assert.AreEqual(0, f.Deck.ExhaustPile.Count);
            Assert.AreEqual(0, f.Execution.FinishCount);
            Assert.IsFalse(f.Time.HasPauseReason(TacticalPauseReason.ExecutionFault));
            Assert.IsFalse(f.Time.HasPauseReason(TacticalPauseReason.RecoveryTimeout));
            f.Execution.ConsumeStopCommand(); f.Stage.ReconcileReleasedCleanup();
            AssertFinalFaultAndRejectedReadmission(f, paid);
        }

        private static void AssertFinalFaultAndRejectedReadmission(Fixture f, int paid)
        {
            Assert.IsFalse(f.Stage.HasReleasedCleanup); Assert.AreEqual(0UL, f.Driver.ExecutionId);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health); Assert.IsFalse(f.Stage.CanStep);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.ExecutionFault)); Assert.IsFalse(f.Stage.CanRequestRecovery);
            Assert.AreEqual(CardZone.ExhaustPile, f.Card.Zone); Assert.IsNull(f.Deck.ResolvingAction);
            Assert.IsNull(f.Deck.PendingAction); Assert.AreEqual(1, f.Deck.ExhaustPile.Count);
            Assert.AreEqual(0, f.Ledger.Reserved); Assert.AreEqual(paid, f.Ledger.Current);
            Assert.AreEqual(1UL, f.Time.CurrentTurnIndex); Assert.AreEqual(1, f.Deck.Hand.Count);
            Assert.AreEqual(0, f.Execution.State.OccupiedFieldSlots);
            for (int i = 0; i < 3; i++) f.Stage.ReconcileReleasedCleanup();
            foreach (var kind in new[] { TacticalStageCommandKind.RequestRecovery,
                TacticalStageCommandKind.SelectAction, TacticalStageCommandKind.ConfirmExecution }) {
                var command = new TacticalStageCommand(f.Stage.StageRunId, f.Stage.TurnIndex, f.Other.InstanceId,
                    f.Other.Version, f.Stage.SnapshotRevision, kind);
                Assert.AreEqual(kind == TacticalStageCommandKind.RequestRecovery ? TacticalStageRejection.RecoveryRejected
                    : TacticalStageRejection.FaultSuspended, f.Stage.Apply(in command));
            }
            Assert.IsFalse(f.Driver.TryCommit(2, out var issue)); Assert.AreEqual("execution:NotReady", issue);
            Assert.AreEqual(1, f.Execution.CancelCount); Assert.AreEqual(1, f.Execution.FinishCount);
            Assert.AreEqual(1, f.Deck.ExhaustPile.Count); Assert.AreEqual(1, f.Deck.Hand.Count);
            Assert.AreEqual(0, f.Ledger.Reserved); Assert.AreEqual(paid, f.Ledger.Current);
            Assert.IsNull(f.Deck.PendingAction); Assert.IsNull(f.Deck.ResolvingAction);
            Assert.AreEqual(0UL, f.Driver.ExecutionId); Assert.AreEqual(1UL, f.Time.CurrentTurnIndex);
        }
    }
}
