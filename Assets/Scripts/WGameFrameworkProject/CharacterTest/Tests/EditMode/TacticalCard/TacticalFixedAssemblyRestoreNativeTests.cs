using System;
using System.Linq;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Tests.Items;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Detached actual pause/hash integration. Explicit fixture parameters and seeded trusted records,
    /// never formal content, player SaveGame, a production budget proof or joint payment transaction.</summary>
    public sealed class TacticalFixedAssemblyRestoreNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = UnityEngine.Time.timeScale;
        [TearDown] public void Teardown() => UnityEngine.Time.timeScale = _scale;
        private sealed class FixtureMotionAck : ITacticalAccessoryMotionSink
        {
            public bool TryEnqueue(in CharacterMotionRequest request) => true;
        }
        private static JointInteractionPausedSessionNativeTests.Fixture Fixture()
            => new JointInteractionPausedSessionNativeTests.Fixture(transformPlayerDefinition: source => source.Kind != TacticalCardKind.Action ? source :
                new TacticalActionCardDefinition(source.Id, source.Name, source.Description, source.BaseMomentumCost,
                    source.PublishedCardId, source.PublishedRevision, source.PublishedContentHash,
                    source.DeckEntryInstanceId == 0 ? 90 : source.DeckEntryInstanceId, source.IconKey));

        // Exercise this codec's existing idle capability after real natural action completion. A free-recovery
        // planning pause with an active action remains unsupported. Only this detached fixture advances here.
        private static void CompleteActualIdleTurn(JointInteractionPausedSessionNativeTests.Fixture f)
        {
            f.CompleteActualTurn();
            for (int i = 0; i < 600 && f.Mixed.Player.Action.State == CharacterTestActionStateId.Running; i++) {
                f.Mixed.PausedWorldTime.SetSimulationPaused(false); f.Tick();
            }
            f.Mixed.PausedWorldTime.SetSimulationPaused(true);
            // The detached fixture has no presentation event reader. Complete that reader boundary explicitly;
            // action costs/effects have already applied through their real owners, not through these records.
            var observed = new System.Collections.Generic.List<GameplayRuntimeEvent>();
            f.Slice.CharacterRuntime.World.GameplayComponentWorld.DrainEvents(new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame), observed);
            Assert.AreNotEqual(CharacterTestActionStateId.Running, f.Mixed.Player.Action.State);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Mixed.Stage.Phase);
        }

        private static void Seed(JointInteractionPausedSessionNativeTests.Fixture f, int status = 1, bool zeroSlots = false)
        {
            if (status == 3) {
                CompleteActualIdleTurn(f);
                Assert.IsTrue(f.Mixed.PausedWorldTime.IsSimulationPaused);
            }
            var stage = f.Mixed.Stage; var actor = f.Mixed.Player;
            var all = stage.Deck.Hand.Concat(stage.Deck.DiscardPile).Concat(stage.Deck.DrawPile).Concat(stage.Deck.ExhaustPile);
            var actual = all.First(card => card.Definition.Kind == TacticalCardKind.Action);
            var definition = actual.Definition;
            var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            snapshot.Assembly.MainCard.CardId = definition.PublishedCardId;
            snapshot.Assembly.MainCard.Revision = definition.PublishedRevision;
            snapshot.Assembly.MainCard.ContentHash = definition.PublishedContentHash;
            if (zeroSlots) { snapshot.Assembly.Accessories.Clear(); snapshot.Accessories = Array.Empty<CharacterTacticalAccessoryDefinition>(); }
            foreach (var accessory in snapshot.Accessories) if (status == 3) {
                accessory.Execution.Lifetime = TacticalAccessoryLifetimeKind.LimitedTurns; accessory.Execution.DurationTurns = 1;
            }
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, definition, 11, 7, 3,
                f.Deployment.Participants.Single(p => p.IsPlayerControlled).Bindings[definition.DeckEntryInstanceId], out var assembly, out var issue), issue);
            definition.AttachFixedAssembly(assembly);
            // Historical card version is separate from the paused version (especially after real turn completion).
            var historical = new TacticalCardInstance(actual.InstanceId, definition); historical.MoveTo(CardZone.Hand); historical.MoveTo(CardZone.PendingAction);
            ulong turn = status == 3 ? stage.TurnIndex - 1 : stage.TurnIndex;
            actor.InteractionEffects.Bind(stage.StageRunId, turn, actor.GameplayEntityId);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.AllAtConfirmation, 0, out var fees));
            var ledger = (MomentumLedger)stage.Ledger;
            Assert.IsTrue(ledger.TryReserve(fees.ConfirmationCost, out var token));
            Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, historical, stage.StageRunId, turn,
                actor.GameplayEntityId, 1, 1, stage.LastCompletedFrame, 0, 1, token, default, out var prepared));
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { prepared }, false, out var prospective));
            var states = Enumerable.Range(0, prepared.ActivationCount).Select(prepared.ActivationAt).ToArray();
            if (states.Length != 0) { Assert.IsTrue(actor.InteractionEffects.TryPrepareAccessoryBatch(states, prospective, out var batch)); batch.Publish(); }
            Assert.IsTrue(ledger.CommitReservation(token)); // Explicit fixture setup; production joint owner is still unavailable.
            Assert.IsTrue(prepared.TryConfirmPayment(token, fees.ConfirmationCost, out var confirmed));
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { confirmed }, true, out var trusted));
            Assert.IsTrue(actor.InteractionEffects.TryBindConfirmedFixedAssembly(trusted));
            if (status == 3) actor.InteractionEffects.AdvanceTurn(stage.StageRunId, stage.TurnIndex, actor.GameplayEntityId);
            if (status == 2) {
                // Seed a qualified root receipt and acknowledgement on the actual effect authority.
                // This verifies pause consumption preservation, not a real combat/motion playable acceptance.
                var receipt = new TacticalAccessoryCombatReceipt(stage.StageRunId, stage.TurnIndex, stage.LastCompletedFrame, 1,
                    actor.GameplayEntityId, f.Mixed.CurrentTarget.GameplayEntityId, new CharacterActionId(17), 12, 13,
                    DamageProfileSourceKind.WeaponTrace, TacticalAccessoryDamageOrigin.DirectAttack, HitResolveKind.Damage,
                    true, true, true, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), 80000, 1000);
                Assert.AreEqual(1, actor.InteractionEffects.CommitAccessoryReceipt(receipt, ledger, null, new FixtureMotionAck()));
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ActualPauseRestoresExactStatusAndJournalTwiceWithoutPaymentOrRearming(int status)
        {
            using var f = Fixture(); Seed(f, status, status == 4); f.Freeze();
            var before = CharacterTestPausedSessionCodec.Read(f.Owner.Capture());
            long hash = f.Slice.Services.Network.CreateSnapshotParticipant().ComputeStateHash(new RuntimeFrame(before.CompletedFrame));
            string json = f.Owner.Capture();
            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
                Assert.AreEqual(before.StateHash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash);
                Assert.AreEqual(hash, f.Slice.Services.Network.CreateSnapshotParticipant().ComputeStateHash(new RuntimeFrame(before.CompletedFrame)));
                Assert.IsNotNull(f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly);
                var saved = f.Mixed.Player.InteractionEffects.Capture().Accessories;
                if (status == 4) Assert.IsNull(saved);
                else Assert.AreEqual(status == 1 ? TacticalAccessoryCombatStatus.Armed : status == 2 ? TacticalAccessoryCombatStatus.Consumed : TacticalAccessoryCombatStatus.Expired,
                    (TacticalAccessoryCombatStatus)saved.Activations.Single(a => a.Kind == (int)TacticalAccessoryCombatKind.PushAway).Status);
            }
        }

        [Test] public void ExpiredWitnessAlsoRestoresPureStateWithoutRearming()
        {
            using var f = Fixture(); Seed(f, 3);
            var stage = f.Mixed.Stage; var effects = f.Mixed.Player.InteractionEffects;
            var ledger = (MomentumLedger)stage.Ledger;
            Assert.IsTrue(TacticalFixedAssemblyRestoreWitness.TryCapture(effects, ledger, stage.LastCompletedFrame,
                stage.Deck, stage.SnapshotDefinitions(), out var witness));
            var state = effects.Capture(); var money = ledger.CaptureSessionState();
            var deck = stage.Deck.CaptureSessionState(stage.SnapshotDefinitions());
            for (int i = 0; i < 2; i++) {
                var restored = new TacticalInteractionEffectAuthority();
                Assert.IsTrue(restored.RestoreJoined(state, stage.Deck, stage.StageRunId, stage.TurnIndex,
                    stage.LastCompletedFrame, f.Mixed.Player.GameplayEntityId, witness.Admission));
                Assert.IsTrue(restored.TryBindConfirmedFixedAssembly(witness.Admission));
                Assert.IsTrue(witness.Matches(restored.Capture(), money, deck, stage.LastCompletedFrame));
                Assert.IsTrue(restored.Capture().Accessories.Activations.All(a => a.Status == (int)TacticalAccessoryCombatStatus.Expired));
            }
            f.Freeze(); // The actual full aggregate capture now also accepts the retired real execution.
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void RehashedTamperingRejectsBeforeFactoryAndKeepsLiveAuthority(int mutation)
        {
            using var f = Fixture(); Seed(f, 2); f.Freeze(targetLedger: true);
            var original = f.Slice; string baseline = f.Owner.Capture();
            var document = CharacterTestPausedSessionCodec.Read(baseline);
            var row = document.Interactions.Single(r => r.Runtime == document.PlayerRuntime);
            if (mutation == 0) row.State.Accessories = null;
            if (mutation == 1) {
                row.State.Accessories.Activations[1].Status = (int)TacticalAccessoryCombatStatus.Armed;
                row.State.Accessories.Activations[1].ConsumedSequence = 0;
                row.State.Accessories.Consumptions = Array.Empty<TacticalAccessoryConsumptionData>();
            }
            if (mutation == 2) document.Motion.Single(r => r.Actor == row.Actor).Ledger.Current--;
            if (mutation == 3) document.Deck.Hand[0].Version++;
            var network = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(document.Network);
            if (mutation == 4) network.FixedAssemblyState = null;
            if (mutation == 5) network.FixedAssemblyState[0].ActorGeneration++;
            if (mutation == 6) network.FixedAssemblyState[0].CommitFingerprint = "forged";
            if (mutation == 7) document.Motion.Single(r => r.Actor != row.Actor).Ledger.Current--;
            document.Network = JsonConvert.SerializeObject(network);
            document.StateHash = CharacterTestPausedSessionCodec.ComputeContentHash(document);
            Assert.IsFalse(f.Owner.TryRestore(JsonConvert.SerializeObject(document), out _));
            Assert.AreEqual(0, f.FactoryCalls); Assert.AreSame(original, f.Slice); Assert.AreEqual(baseline, f.Owner.Capture());
        }

        [Test] public void CompletedBareExecutionRestoresTwiceAndNeverReusesHistoricalIdOrReward()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture(); CompleteActualIdleTurn(f);
            f.Freeze(); string json = f.Owner.Capture(); var before = CharacterTestPausedSessionCodec.Read(json);
            Assert.IsNotNull(before.ExecutionIdentity); Assert.IsTrue(before.ExecutionIdentity.Terminal);
            Assert.AreEqual(0, f.Mixed.PausedAdapter.CurrentExecutionId);
            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
                Assert.AreEqual(before.StateHash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash);
                Assert.IsFalse(f.Mixed.PausedAdapter.TryAbortPreAction(before.ExecutionIdentity.LastId, "late"));
                Assert.IsFalse(f.Mixed.PausedAdapter.TryApplyHitReward(before.ExecutionIdentity.LastId, 99, 2, f.Mixed.Stage.Ledger));
                Assert.IsFalse(f.Mixed.PausedAdapter.Tick());
            }
            // Failed preparation must not erase the frozen terminal receipt or its copied admission version.
            Assert.IsFalse(f.Mixed.PausedAdapter.TryPrepare(null, out _));
            Assert.AreEqual(JsonConvert.SerializeObject(before.ExecutionIdentity),
                JsonConvert.SerializeObject(f.Mixed.PausedAdapter.CaptureIdleExecutionIdentity()));
            var next = f.Mixed.Stage.Deck.Hand.First(c => c.Definition.Kind == TacticalCardKind.Action);
            f.Apply(TacticalStageCommandKind.SelectAction, next); f.Apply(TacticalStageCommandKind.ConfirmExecution, next);
            Assert.Greater(f.Mixed.PausedAdapter.CurrentExecutionId, before.ExecutionIdentity.NextId);
            Assert.AreEqual(0, f.Slice.PendingRuntimeCommandCount);
            Assert.IsFalse(f.Mixed.PausedAdapter.TryApplyHitReward(before.ExecutionIdentity.LastId, 99, 2, f.Mixed.Stage.Ledger));
        }

        [Test] public void ActualPreActionCancellationRetainsHistoricalReasonAndRestoresWithoutRefundReplay()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture(); var stage = f.Mixed.Stage;
            ulong turn = stage.TurnIndex; var card = stage.Deck.Hand.First(c => c.Definition.Kind == TacticalCardKind.Action);
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            Assert.IsTrue(f.Mixed.PausedAdapter.TryAbortPreAction(f.Mixed.PausedAdapter.CurrentExecutionId, "fixture:pre-action-cancel"));
            for (int i = 0; i < 600 && (stage.TurnIndex == turn || stage.Phase != TacticalStagePhase.Planning); i++) f.Tick();
            Assert.Greater(stage.TurnIndex, turn); f.Mixed.PausedWorldTime.SetSimulationPaused(true);
            var events = new System.Collections.Generic.List<GameplayRuntimeEvent>();
            f.Slice.CharacterRuntime.World.GameplayComponentWorld.DrainEvents(new RuntimeFrame(stage.LastCompletedFrame), events);
            f.Freeze(); string json = f.Owner.Capture(); var saved = CharacterTestPausedSessionCodec.Read(json);
            Assert.IsTrue(saved.ExecutionIdentity.Aborted); Assert.IsFalse(saved.ExecutionIdentity.ActionStarted);
            Assert.AreEqual("fixture:pre-action-cancel", saved.ExecutionIdentity.Issue);
            int balance = stage.Ledger.Current;
            for (int i = 0; i < 2; i++) {
                Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
                Assert.AreEqual(balance, f.Mixed.Stage.Ledger.Current);
                Assert.AreEqual(saved.StateHash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash);
                Assert.IsFalse(f.Mixed.PausedAdapter.TryAbortPreAction(saved.ExecutionIdentity.LastId, "late"));
            }
        }

        [Test] public void FrozenZeroAccessoryJournalRejectsLiveJournalOnlyAppendBeforeFactory()
        {
            using var f = Fixture(); Seed(f, 4, true); f.Freeze(); string json = f.Owner.Capture();
            var authority = f.Mixed.Player.InteractionEffects; var old = authority.ConfirmedFixedAssembly.CommitAt(0);
            var card = new TacticalCardInstance(old.BattleCardInstance, f.Mixed.Stage.Deck.Hand.First(c => c.InstanceId == old.BattleCardInstance).Definition);
            card.MoveTo(CardZone.Hand); card.MoveTo(CardZone.PendingAction);
            Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(old.Assembly, old.Fees, card, old.Stage, old.Turn, old.Actor,
                2, 2, old.Frame, old.ReceiptCursor, 1, 2, default, out var prepared));
            // Trusted-fixture journal construction isolates the journal predicate. This promotion is not payment proof.
            Assert.IsTrue(prepared.TryConfirmPayment(2, old.Fees.ConfirmationCost, out var appended));
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { old, appended }, true, out var next));
            int money = f.Mixed.Stage.Ledger.Current; var source = f.Slice;
            Assert.IsTrue(authority.TryBindConfirmedFixedAssembly(next));
            Assert.IsFalse(f.Owner.TryRestore(json, out _)); Assert.AreEqual(0, f.FactoryCalls);
            Assert.AreSame(source, f.Slice); Assert.AreEqual(money, f.Mixed.Stage.Ledger.Current);
            Assert.AreEqual(2, authority.ConfirmedFixedAssembly.CommitCount);
        }

        [Test] public void ActualLifecycleInterruptionCannotBecomeIdleHistoryOrCapturedHealthyPause()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            var card = f.Mixed.Stage.Deck.Hand.First(c => c.Definition.Kind == TacticalCardKind.Action);
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            ulong id = f.Mixed.PausedAdapter.CurrentExecutionId;
            for (int i = 0; i < 600 && !f.Mixed.PausedAdapter.Observe(id).ActionStarted; i++) f.Tick();
            Assert.IsTrue(f.Mixed.PausedAdapter.Observe(id).ActionStarted);
            Assert.IsTrue(f.Slice.ActionRuntime.TerminateForLifecycle(f.Mixed.Player.Id, f.Slice.CurrentFrame,
                CharacterActionLifecycleReason.ConfigurationReload, default));
            Assert.AreEqual(0, f.Mixed.PausedAdapter.Observe(id).ExecutionId);
            Assert.IsTrue(f.Mixed.PausedAdapter.HasExecutionFault);
            Assert.Throws<InvalidOperationException>(() => f.Mixed.PausedAdapter.CaptureIdleExecutionIdentity());
            Assert.Throws<InvalidOperationException>(() => f.Freeze());
            Assert.AreEqual(id, f.Mixed.PausedAdapter.CurrentExecutionId);
            Assert.AreSame(card, f.Mixed.Stage.Deck.ResolvingAction);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void RehashedIdleIdentityTamperingOrOmissionRejectsBeforeFactory(int mutation)
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture(); CompleteActualIdleTurn(f);
            f.Freeze(); string baseline = f.Owner.Capture(); var source = f.Slice;
            var document = CharacterTestPausedSessionCodec.Read(baseline);
            if (mutation == 0) document.ExecutionIdentity = null;
            if (mutation == 1) document.ExecutionIdentity.NextId++;
            if (mutation == 2) document.ExecutionIdentity.Terminal = false;
            if (mutation == 3) document.ExecutionIdentity.CardVersion++;
            if (mutation == 4) {
                var cards = document.Deck.Hand.Concat(document.Deck.Draw).Concat(document.Deck.Discard).Concat(document.Deck.Exhaust).ToArray();
                var historical = cards.Single(c => c.Instance == document.ExecutionIdentity.CardInstance);
                var other = cards.First(c => c.Definition != historical.Definition);
                int definition = historical.Definition; historical.Definition = other.Definition; other.Definition = definition;
            }
            if (mutation == 5) document.Deck.Hand[0].Version++;
            document.StateHash = CharacterTestPausedSessionCodec.ComputeContentHash(document);
            Assert.IsFalse(f.Owner.TryRestore(JsonConvert.SerializeObject(document), out _));
            Assert.AreEqual(0, f.FactoryCalls); Assert.AreSame(source, f.Slice); Assert.AreEqual(baseline, f.Owner.Capture());
        }

        [Test] public void ProductionHashesObserveRealLedgerAndAllOwnershipFilters()
        {
            using var f = Fixture(); Seed(f);
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant(); var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            int player = f.Mixed.Player.Id.Value, target = f.Mixed.CurrentTarget.Id.Value;
            var full = participant.ComputeStateHash(frame); var owned = participant.ComputeOwnedStateHash(frame, player);
            var nonOwned = participant.ComputeNonOwnedStateHash(frame, target); var excluded = participant.ComputeNonOwnedStateHash(frame, player);
            var ledger = (MomentumLedger)f.Mixed.Stage.Ledger; ledger.Grant(1);
            Assert.AreNotEqual(full, participant.ComputeStateHash(frame)); Assert.AreNotEqual(owned, participant.ComputeOwnedStateHash(frame, player));
            Assert.AreNotEqual(nonOwned, participant.ComputeNonOwnedStateHash(frame, target)); Assert.AreEqual(excluded, participant.ComputeNonOwnedStateHash(frame, player));
            var current = participant.ComputeStateHash(frame); Assert.IsTrue(ledger.TryReserve(1, out var token));
            Assert.AreNotEqual(current, participant.ComputeStateHash(frame)); Assert.IsTrue(ledger.ReleaseReservation(token));
            Assert.AreNotEqual(current, participant.ComputeStateHash(frame)); // NextToken survives release.
        }

        [Test] public void ProductionFixedHashPathsAllocateNothingAfterWarmup()
        {
            using var f = Fixture(); Seed(f);
            f.Slice.CharacterRuntime.World.BindTacticalMotion(f.Mixed.CurrentTarget.Id.Value, new MomentumLedger(37));
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant();
            var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame); int player = f.Mixed.Player.Id.Value;
            for (int i = 0; i < 16; i++) {
                participant.ComputeStateHash(frame); participant.ComputeOwnedStateHash(frame, player);
                participant.ComputeNonOwnedStateHash(frame, player);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) {
                participant.ComputeStateHash(frame); participant.ComputeOwnedStateHash(frame, player);
                participant.ComputeNonOwnedStateHash(frame, player);
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EachActualLedgerFieldChangesProductionHashEvenForDefendingActorWithoutAccessories(int field)
        {
            using var f = Fixture(); Seed(f); var world = f.Slice.CharacterRuntime.World;
            var defender = new MomentumLedger(37); Assert.IsTrue(defender.TryReserve(0, out _));
            world.BindTacticalMotion(f.Mixed.CurrentTarget.Id.Value, defender);
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant(); var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var full = participant.ComputeStateHash(frame); var owned = participant.ComputeOwnedStateHash(frame, f.Mixed.CurrentTarget.Id.Value);
            var nonOwned = participant.ComputeNonOwnedStateHash(frame, f.Mixed.Player.Id.Value);
            var next = defender.CaptureSessionState();
            if (field == 0) next.Current--; if (field == 1) next.Reserved++;
            if (field == 2) next.NextToken++; if (field == 3) next.ActiveToken = 0;
            defender.RestoreSessionState(next);
            Assert.AreNotEqual(full, participant.ComputeStateHash(frame));
            Assert.AreNotEqual(owned, participant.ComputeOwnedStateHash(frame, f.Mixed.CurrentTarget.Id.Value));
            Assert.AreNotEqual(nonOwned, participant.ComputeNonOwnedStateHash(frame, f.Mixed.Player.Id.Value));
        }

        [Test] public void FirstPlayerJournalCannotChangeExcludedDefenderHashLayout()
        {
            using var f = Fixture();
            f.Slice.CharacterRuntime.World.BindTacticalMotion(f.Mixed.CurrentTarget.Id.Value, new MomentumLedger(37));
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant(); var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            long owned = participant.ComputeOwnedStateHash(frame, f.Mixed.CurrentTarget.Id.Value);
            long excluded = participant.ComputeNonOwnedStateHash(frame, f.Mixed.Player.Id.Value);
            Seed(f);
            Assert.AreEqual(owned, participant.ComputeOwnedStateHash(frame, f.Mixed.CurrentTarget.Id.Value));
            Assert.AreEqual(excluded, participant.ComputeNonOwnedStateHash(frame, f.Mixed.Player.Id.Value));
        }

        [TestCase(39)] [TestCase(42)]
        public void UnsupportedSchemaPreflightRejectsBeforeClockChange(int schema)
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant(); var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var rollback = f.Slice.Services.Network.CaptureRollbackState(frame, participant); rollback.Document.SchemaVersion = schema;
            long before = f.Slice.CurrentFrame.Value; double elapsed = f.Slice.ElapsedSeconds;
            Assert.IsFalse(f.Slice.Services.Network.RestoreRollbackState(RuntimeFrame.Zero, rollback, participant, out _));
            Assert.AreEqual(before, f.Slice.CurrentFrame.Value); Assert.AreEqual(elapsed, f.Slice.ElapsedSeconds);
            var carrier = new RuntimeCustomState(CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                CharacterTestNetworkSnapshotParticipant.ParticipantIdValue, CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                JsonConvert.SerializeObject(rollback.Document));
            Assert.IsFalse(participant.PreflightFixedAssemblyRollback(carrier).Success);
        }

        [Test] public void SupportedEnvelopeAndDocumentSchemaMismatchCannotReachRollbackMutation()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant();
            var document = participant.CaptureRollbackDocument(new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame));
            document.SchemaVersion = CharacterTestNetworkSnapshotParticipant.PositionRecallSchemaVersionValue;
            var carrier = new RuntimeCustomState(CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                CharacterTestNetworkSnapshotParticipant.ParticipantIdValue, CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                JsonConvert.SerializeObject(document));
            Assert.IsFalse(participant.PreflightFixedAssemblyRollback(carrier).Success);
            Assert.IsFalse(participant.ApplySnapshot(new RuntimeFrame(document.Frame), carrier).Success);
        }

        [Test] public void CloneAndNetworkGatesCannotEraseP2OrChangeClock()
        {
            using var f = Fixture(); Seed(f);
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant(); var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var document = participant.CaptureRollbackDocument(frame); var clone = document.CloneForRollback();
            clone.FixedAssemblyState[0].Effects.Accessories.Activations[0].MomentumAmount++;
            Assert.AreNotEqual(clone.FixedAssemblyState[0].Effects.Accessories.Activations[0].MomentumAmount,
                document.FixedAssemblyState[0].Effects.Accessories.Activations[0].MomentumAmount);
            long hash = participant.ComputeStateHash(frame); long nextFrame = f.Slice.CurrentFrame.Value; double elapsed = f.Slice.ElapsedSeconds;
            var rollback = f.Slice.Services.Network.CaptureRollbackState(frame, participant);
            Assert.IsFalse(f.Slice.Services.Network.RestoreRollbackState(RuntimeFrame.Zero, rollback, participant, out _));
            Assert.AreEqual(nextFrame, f.Slice.CurrentFrame.Value); Assert.AreEqual(elapsed, f.Slice.ElapsedSeconds);
            document.FixedAssemblyState = null;
            Assert.IsFalse(participant.ApplyRollbackDocument(frame, document, false).Success);
            Assert.IsFalse(f.Slice.CharacterRuntime.ApplyNetworkCharacters(Array.Empty<CharacterTestNetworkCharacterSnapshot>(), frame).Success);
            Assert.AreEqual(hash, participant.ComputeStateHash(frame)); Assert.AreEqual(2, f.Slice.CharacterEntities.Count);
        }

        [TestCase(40)] [TestCase(41)]
        public void LedgerOnlyRollbackRejectsBeforeClockOrActorWrites(int schema)
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant();
            var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var rollback = f.Slice.Services.Network.CaptureRollbackState(frame, participant);
            rollback.Document.SchemaVersion = schema;
            Assert.IsNull(rollback.Document.FixedAssemblyState); // Known capture gap; absence never permits partial restore.
            Assert.IsFalse(f.Mixed.Player.InteractionEffects.HasFixedAssemblyState);
            var player = f.Mixed.Player; var ledger = (MomentumLedger)f.Mixed.Stage.Ledger;
            long savedHash = participant.ComputeStateHash(frame);
            ledger.Grant(1);
            long changedHash = participant.ComputeStateHash(frame); Assert.AreNotEqual(savedHash, changedHash);
            string changedLedger = JsonConvert.SerializeObject(ledger.CaptureSessionState());
            long clock = f.Slice.CurrentFrame.Value; double elapsed = f.Slice.ElapsedSeconds;
            Assert.Greater(clock, RuntimeFrame.Zero.Value);
            Assert.IsFalse(f.Slice.Services.Network.RestoreRollbackState(RuntimeFrame.Zero, rollback, participant, out var issue));
            Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, issue);
            Assert.AreEqual(clock, f.Slice.CurrentFrame.Value); Assert.AreEqual(elapsed, f.Slice.ElapsedSeconds);
            Assert.AreEqual(changedLedger, JsonConvert.SerializeObject(ledger.CaptureSessionState()));
            Assert.AreEqual(changedHash, participant.ComputeStateHash(frame));
            Assert.AreSame(player, f.Mixed.Player); Assert.AreEqual(2, f.Slice.CharacterEntities.Count);
        }

        [TestCase(40)] [TestCase(41)]
        public void LedgerOnlyNetworkApplyRejectsMissingExtensionBeforeActorWrites(int schema)
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant();
            var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var document = participant.CaptureRollbackDocument(frame); document.SchemaVersion = schema;
            Assert.IsNull(document.FixedAssemblyState); Assert.IsFalse(f.Mixed.Player.InteractionEffects.HasFixedAssemblyState);
            var player = f.Mixed.Player; long hash = participant.ComputeStateHash(frame);
            var rollbackResult = participant.ApplyRollbackDocument(frame, document, false);
            Assert.IsFalse(rollbackResult.Success); Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, rollbackResult.Message);
            var carrier = new RuntimeCustomState(CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                CharacterTestNetworkSnapshotParticipant.ParticipantIdValue, schema, JsonConvert.SerializeObject(document));
            var carrierPreflight = participant.PreflightFixedAssemblyRollback(carrier);
            Assert.IsFalse(carrierPreflight.Success); Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, carrierPreflight.Message);
            var applyResult = participant.ApplySnapshot(frame, carrier);
            Assert.IsFalse(applyResult.Success); Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, applyResult.Message);
            Assert.AreSame(player, f.Mixed.Player); Assert.AreEqual(2, f.Slice.CharacterEntities.Count);
            Assert.AreEqual(hash, participant.ComputeStateHash(frame));
        }

        [Test] public void LedgerOnlyDirectCharacterApplyCannotEraseActorsOrLedger()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant(); var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var player = f.Mixed.Player; var ledger = f.Mixed.Stage.Ledger; long hash = participant.ComputeStateHash(frame);
            Assert.IsFalse(player.InteractionEffects.HasFixedAssemblyState);
            var result = f.Slice.CharacterRuntime.ApplyNetworkCharacters(Array.Empty<CharacterTestNetworkCharacterSnapshot>(), frame);
            Assert.IsFalse(result.Success); Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, result.Message);
            Assert.AreSame(player, f.Mixed.Player); Assert.AreEqual(2, f.Slice.CharacterEntities.Count);
            Assert.IsTrue(f.Slice.CharacterRuntime.World.TryGetTacticalMotion(player.Id.Value, out var actual));
            Assert.AreSame(ledger, actual); Assert.AreEqual(hash, participant.ComputeStateHash(frame));
        }

        [TestCase(40)] [TestCase(41)]
        public void NoLedgerNoAssemblyKeepsLegacyPreflightAndDirectEmptyApply(int schema)
        {
            var runtime = new CharacterTestCharacterRuntimeModule(null, null, createViews: false);
            try
            {
                var participant = new CharacterTestNetworkSnapshotParticipant(runtime, null);
                var document = participant.CaptureRollbackDocument(RuntimeFrame.Zero); document.SchemaVersion = schema;
                Assert.IsNull(document.FixedAssemblyState);
                Assert.IsTrue(participant.PreflightFixedAssemblyRollback(document).Success);
                Assert.IsTrue(runtime.ApplyNetworkCharacters(Array.Empty<CharacterTestNetworkCharacterSnapshot>(), RuntimeFrame.Zero).Success);
            }
            finally { runtime.Dispose(); }
        }

        [Test] public void LedgerOnlyPrivatePausedOwnerRestoresBothActualLedgersTwice()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            f.Freeze(targetLedger: true); string json = f.Owner.Capture(); var saved = CharacterTestPausedSessionCodec.Read(json);
            Assert.IsNull(JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(saved.Network).FixedAssemblyState);
            Assert.AreEqual(2, saved.Motion.Length);
            for (int i = 0; i < 2; i++) {
                var previous = f.Slice; var previousLedger = f.Mixed.Stage.Ledger;
                Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
                Assert.AreNotSame(previous, f.Slice); Assert.AreNotSame(previousLedger, f.Mixed.Stage.Ledger);
                var restored = CharacterTestPausedSessionCodec.Read(f.Owner.Capture());
                Assert.AreEqual(saved.StateHash, restored.StateHash);
                Assert.AreEqual(JsonConvert.SerializeObject(saved.Motion), JsonConvert.SerializeObject(restored.Motion));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ReplayGameplayBootstrapRejectsBeforeAdvancingKnownLedgerOrAssembly(bool fixedAssembly)
        {
            using var f = fixedAssembly ? Fixture() : new JointInteractionPausedSessionNativeTests.Fixture();
            if (fixedAssembly) Seed(f);
            var participant = f.Slice.Services.Network.CreateSnapshotParticipant();
            var frame = new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame);
            var bootstrap = participant.CaptureSnapshot(frame);
            var start = new RuntimeFrame(checked(f.Slice.CurrentFrame.Value + 3));
            var header = new RuntimeReplayHeader(CharacterTestReplayRecorder.SchemaVersion,
                CharacterTestReplayRecorder.FrameworkVersion, "", "", start);
            var capture = new CharacterTestReplayCapture(new RuntimeReplaySnapshot(header,
                new[] { new RuntimeReplayFrameRecord(start, Array.Empty<RuntimeCommand>(), 0, "preflight fixture") }),
                bootstrap, 60, MxFramework.Network.NetworkStateSyncPolicy.BootstrapOnly, Array.Empty<RuntimeCommand>());
            int factoryCalls = 0;
            using var driver = new CharacterTestReplayFrameDriver(capture, () => { factoryCalls++; return f.Slice; });
            Assert.IsTrue(driver.BeginResetAndRestoreSessionAuthority(header).Success);
            Assert.AreEqual(1, factoryCalls); Assert.AreSame(f.Slice, driver.Slice);
            // The detached replay world can advance if called: the new preflight must return before its first tick.
            f.Mixed.PausedWorldTime.SetSimulationPaused(false);
            long clock = f.Slice.CurrentFrame.Value; double elapsed = f.Slice.ElapsedSeconds;
            Assert.Less(clock, start.Value);
            var player = f.Mixed.Player; long hash = participant.ComputeStateHash(frame);
            string ledger = JsonConvert.SerializeObject(((MomentumLedger)f.Mixed.Stage.Ledger).CaptureSessionState());
            var result = driver.RestoreGameplayBootstrap(header);
            Assert.IsFalse(result.Success); Assert.AreEqual(CharacterTestReplayValidationCodes.GameplayBootstrapRestoreFailed, result.Code);
            Assert.AreEqual(fixedAssembly ? "network:FixedAssemblyAggregateRestoreUnavailable" :
                CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, result.Message);
            Assert.AreEqual(clock, f.Slice.CurrentFrame.Value); Assert.AreEqual(elapsed, f.Slice.ElapsedSeconds);
            Assert.AreSame(player, f.Mixed.Player); Assert.AreEqual(2, f.Slice.CharacterEntities.Count);
            Assert.AreEqual(hash, participant.ComputeStateHash(frame));
            Assert.AreEqual(ledger, JsonConvert.SerializeObject(((MomentumLedger)f.Mixed.Stage.Ledger).CaptureSessionState()));
        }

        [Test] public void LegacyEmptyAssemblyNetworkDocumentKeepsAbsentProjectionAndRestores()
        {
            using var f = new JointInteractionPausedSessionNativeTests.Fixture();
            f.Freeze(); var saved = CharacterTestPausedSessionCodec.Read(f.Owner.Capture());
            var network = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(saved.Network);
            Assert.IsNull(network.FixedAssemblyState); Assert.IsNull(JObject.Parse(saved.Network)["FixedAssemblyState"]);
            network.SchemaVersion = CharacterTestNetworkSnapshotParticipant.PositionRecallSchemaVersionValue;
            saved.Network = JsonConvert.SerializeObject(network); saved.StateHash = CharacterTestPausedSessionCodec.ComputeContentHash(saved);
            // Schema 40 remains supported by the private aggregate owner, not the generic ledger-blind importer.
            var ordinary = f.Slice.Services.Network.CreateSnapshotParticipant().ApplyRollbackDocument(new RuntimeFrame(saved.CompletedFrame), network, false);
            Assert.IsFalse(ordinary.Success); Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage, ordinary.Message);
            Assert.IsTrue(f.Owner.TryRestore(JsonConvert.SerializeObject(saved), out var issue), issue);
            var upgraded = CharacterTestPausedSessionCodec.Read(f.Owner.Capture());
            Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(upgraded.Network).SchemaVersion);
            Assert.IsNull(upgraded.Interactions.Single(row => row.Runtime == upgraded.PlayerRuntime).State.Accessories);
        }
    }
}
