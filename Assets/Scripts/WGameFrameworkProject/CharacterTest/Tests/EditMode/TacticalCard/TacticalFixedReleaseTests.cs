using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalFixedReleaseTests
    {
        private sealed class TimePort : ITacticalTimeAdapter {
            public bool IsSimulationPaused { get; private set; }
            public float SimulationTimeScale => 1;
            public void SetSimulationPaused(bool value) => IsSimulationPaused = value;
            public void SetSimulationTimeScale(float value) { }
        }
        private sealed class Port : ITacticalExecutionPort, ITacticalFixedExecutionPort, ITacticalFixedCleanupPort {
            internal readonly TacticalInteractionEffectAuthority Effects = new TacticalInteractionEffectAuthority();
            internal ExecutionObservation Observation; internal int CancelCount; internal Action Retired;
            public bool TryPrepare(TacticalCardInstance c, out string issue) { issue = ""; return false; }
            public bool TryAdmit(TacticalCardInstance c, out ulong id, out string issue) { id = 0; issue = ""; return false; }
            public ExecutionObservation Observe(ulong id) => Observation;
            public bool TryAbortPreAction(ulong id, string reason) => false;
            public bool TryCommitFixed(TacticalCardInstance card, TacticalDeckManager deck, TacticalTimeCoordinator time,
                MomentumLedger ledger, in TacticalExecutionInput input, out ulong id, out TacticalFixedCardPaymentCommit paid, out string issue) {
                id = 0; paid = null; issue = "";
                var program = TacticalFixedPaymentCoreTests.OneAction();
                Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => 7, out var proof, out _));
                Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(card.Definition.FixedAssembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions, 7, out var fees));
                Assert.IsTrue(deck.TryPrepareFixedMove(card, ledger, out var move)); Assert.IsTrue(time.TryPrepareFixedTurn(deck, out var turn));
                Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(card.Definition.FixedAssembly, fees, card, 7, 1,
                    new GameplayEntityId(1, 1), 1, 1, 0, 0, 1, deck.FixedPendingReservation(ledger), input, out var manifest));
                Assert.IsTrue(TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(manifest, card, ledger,
                    Effects, program, proof, out var core, out issue)); Assert.IsTrue(core.TryPublish(out paid));
                move.Publish(paid); turn.Publish(); id = 1; Observation = new ExecutionObservation(1, true, false, false, false, false); return true;
            }
            public void CancelFixed(ulong id) => CancelCount++;
            public void SetFixedReleaseObserver(ulong id, Action retired) => Retired = retired;
            public ExecutionObservation ReconcileFixedRelease(ulong id) => Observation;
            public void FinishFixedRelease(ulong id) => Retired = null;
        }
        private sealed class Fixture {
            internal readonly Port Execution = new Port();
            internal readonly MomentumLedger Ledger = new MomentumLedger(20);
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager(1);
            internal readonly TacticalTimeCoordinator Time = new TacticalTimeCoordinator(new TimePort());
            internal readonly TacticalSingleCardDriver Driver;
            internal readonly TacticalStageFlowCoordinator Stage;
            internal Fixture() {
                var main = TacticalFixedAssemblyManifestTests.Main();
                Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(TacticalFixedAssemblyManifestTests.Snapshot(), main,
                    11, 7, 3, new Dictionary<string, int> { { "main.action", 17 } }, out var assembly, out _)); main.AttachFixedAssembly(assembly);
                Driver = new TacticalSingleCardDriver(Execution, Time, Deck, Ledger);
                Stage = new TacticalStageFlowCoordinator(7, Time, Deck, Ledger, Driver, new[] { main }, 1, _ => true,
                    new TacticalStoryStepBinding(1, 1, 1), allowConfigurableHandCapacity: true, configuredInitialHandCount: 1);
                Deck.InitializeDeck(new[] { main }, 1, 1); Time.Initialize();
                Assert.AreEqual(CardPlayFailure.None, Deck.SetPendingAction(Deck.Hand[0], Ledger));
                Execution.Effects.Bind(7, 1, new GameplayEntityId(1, 1));
                Assert.IsTrue(Driver.TryCommit(0, out var issue), issue);
            }
        }
        [Test] public void ReleasedStageRetainsPositiveBudgetUntilCleanupAcknowledgesAndNeverRefillsOrRewards() {
            var f = new Fixture(); f.Stage.ReleaseControl();
            Assert.IsTrue(f.Stage.IsReleased); Assert.IsTrue(f.Stage.HasReleasedCleanup); Assert.AreEqual(7, f.Ledger.Reserved);
            f.Stage.ReconcileReleasedCleanup(); Assert.AreEqual(7, f.Ledger.Reserved); Assert.AreEqual(15, f.Ledger.Current);
            f.Execution.Observation = new ExecutionObservation(1, true, false, true, true, true);
            f.Stage.ReconcileReleasedCleanup();
            Assert.AreEqual(0, f.Ledger.Reserved); Assert.AreEqual(15, f.Ledger.Current); Assert.AreEqual(0ul, f.Driver.ExecutionId);
            Assert.AreSame(f.Deck.ResolvingAction.Definition.FixedAssembly, f.Execution.Effects.ConfirmedFixedAssembly.CommitAt(0).Assembly);
            Assert.AreEqual(1ul, f.Time.CurrentTurnIndex); Assert.AreEqual(0, f.Deck.Hand.Count);
        }
        [Test] public void ActualRetirementNotificationCanFinishAfterNormalStageTicksStopAndDoesNotReleaseMenuPause() {
            var f = new Fixture(); f.Time.RequestPause(TacticalPauseReason.UserMenu); f.Stage.ReleaseControl();
            Assert.AreEqual(7, f.Ledger.Reserved);
            f.Execution.Observation = new ExecutionObservation(1, true, false, true, true, true); f.Execution.Retired();
            Assert.AreEqual(0, f.Ledger.Reserved); Assert.AreEqual(0ul, f.Driver.ExecutionId);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.UserMenu)); Assert.IsNull(f.Execution.Retired);
            Assert.AreEqual(15, f.Ledger.Current);
        }
        [Test] public void RepeatedReleaseRequestsCancelOnceAndWrongExecutionCannotReleaseBudget() {
            var f = new Fixture(); f.Stage.ReleaseControl(); f.Driver.ReleaseControl();
            Assert.AreEqual(1, f.Execution.CancelCount);
            f.Execution.Observation = new ExecutionObservation(2, true, true, true, true, true);
            f.Stage.ReconcileReleasedCleanup(); Assert.AreEqual(7, f.Ledger.Reserved); Assert.AreEqual(1ul, f.Driver.ExecutionId);
            f.Execution.Observation = new ExecutionObservation(1, true, false, true, true, true);
            f.Stage.ReconcileReleasedCleanup(); f.Stage.ReleaseControl(); f.Driver.ReleaseControl();
            Assert.AreEqual(1, f.Execution.CancelCount); Assert.AreEqual(15, f.Ledger.Current);
        }
    }
}
