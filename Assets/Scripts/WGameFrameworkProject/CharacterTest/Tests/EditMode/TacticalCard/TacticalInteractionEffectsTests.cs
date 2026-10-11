using System;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class TacticalInteractionEffectsTests
    {
        [Test] public void ReturnedActiveSnapshotCannotReplayReturnAndLeavesLiveStateUntouched()
            => RejectTerminalReplay(TacticalReturnResult.Returned);
        [Test] public void DestinationBlockedActiveSnapshotCannotReplayReturnAndLeavesLiveStateUntouched()
            => RejectTerminalReplay(TacticalReturnResult.DestinationBlocked);
        [Test] public void ExpiredActiveSnapshotCannotReplayReturnAndLeavesLiveStateUntouched()
            => RejectTerminalReplay(TacticalReturnResult.Expired);
        [Test] public void TerminalReturnSnapshotRejectsRestoredDestinationCoordinates()
        {
            var owner=Owner();Arm(owner,TacticalInteractionKind.ReturnMark);owner.CommitReceipt(Hit(),1,true,false,false,null);
            owner.CompleteReturnAttempt(true);var saved=owner.Capture();Assert.IsTrue(saved.IsValid(11));
            saved.Slots[3].X=100;Assert.IsFalse(saved.IsValid(11));long before=Hash(owner);
            Assert.IsFalse(owner.Restore(saved,11,Actor));Assert.AreEqual(before,Hash(owner));
        }
        private static void RejectTerminalReplay(TacticalReturnResult terminal)
        {
            TacticalInteractionTrialCards.TryCreate("lab.return-mark",out var definition);
            var deck=new TacticalDeckManager(3,true);var ledger=new MomentumLedger(50);deck.InitializeDeck(new[]{definition},73);
            var card=deck.Hand[0];Assert.AreEqual(CardPlayFailure.None,deck.SetPendingAction(card,ledger));uint version=card.Version;
            Assert.AreEqual(CardPlayFailure.None,deck.CommitPendingTestPreparation(ledger));
            var owner=Owner();owner.ActivateValidated(TacticalInteractionKind.ReturnMark,10,default,100,200,300,card.InstanceId,version);
            long frame=terminal==TacticalReturnResult.Expired?611:11;
            if(terminal==TacticalReturnResult.Expired)owner.Expire(frame);
            else {owner.CommitReceipt(Hit(),1,true,false,false,ledger);owner.CompleteReturnAttempt(terminal==TacticalReturnResult.Returned);}
            var valid=owner.Capture();Assert.AreEqual(terminal,valid.ReturnResult);Assert.IsTrue(valid.IsValid(frame));
            Assert.IsTrue(TacticalInteractionEffectAuthority.ValidateSessionJoin(valid,deck,7,1,frame,Actor));
            var restored=Owner();Assert.IsTrue(restored.RestoreJoined(valid,deck,7,1,frame,Actor));
            long before=Hash(restored);var forged=valid.Clone();forged.Slots[3].Active=true; // exact R2 counterexample: only flip this field
            Assert.IsFalse(forged.IsValid(frame));Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(forged,deck,7,1,frame,Actor));
            Assert.IsFalse(restored.RestoreJoined(forged,deck,7,1,frame,Actor));Assert.AreEqual(before,Hash(restored));
            restored.CommitReceipt(Hit(frame+1,2),1,true,false,false,ledger);Assert.IsFalse(restored.ReturnPending);
            Assert.AreEqual(terminal,restored.ReturnResult);Assert.AreEqual(45,ledger.Current);Assert.AreEqual(5,deck.PaidCardMomentum);
        }
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 1), Enemy = new GameplayEntityId(2, 1), Other = new GameplayEntityId(3, 1);
        private static TacticalInteractionEffectAuthority Owner()
        { var owner = new TacticalInteractionEffectAuthority(); owner.Bind(7, 1, Actor); return owner; }
        private static void Arm(TacticalInteractionEffectAuthority owner, TacticalInteractionKind kind, long frame = 10)
        {
            Assert.That(owner.Validate(kind, frame, Enemy, true, true), Is.EqualTo(TacticalInteractionFailure.None));
            owner.ActivateValidated(kind, frame, Enemy, 100, 200, 300, 1, 1);
        }
        private static TacticalInteractionReceipt Hit(long frame = 11, int trace = 1, GameplayEntityId target = default)
            => new TacticalInteractionReceipt(7, 1, frame, Actor, target.IsValid ? target : Enemy, 99, trace);
        private static long Hash(TacticalInteractionEffectAuthority owner)
        { var hash = new RuntimeHashAccumulator(1, 1); owner.AddHash(ref hash); return hash.ToHash(); }

        [Test] public void RecoveryConsumesFirstActualHitOnly()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(), 5, true, false, false, ledger);
            owner.CommitReceipt(Hit(12, 2), 5, true, false, false, ledger);
            Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void DuplicateReceiptDoesNotGrantOrAppendDamageAgain()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.PursuitMark); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            Assert.That(owner.CommitReceipt(Hit(), 5, true, false, false, ledger), Is.EqualTo(8));
            Assert.That(owner.CommitReceipt(Hit(), 5, true, false, false, ledger), Is.Zero); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void DerivedDamageCannotConsumeOrReward()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery); Arm(owner, TacticalInteractionKind.PursuitMark);
            Assert.That(owner.CommitReceipt(Hit(), 8, true, false, true, ledger), Is.Zero); Assert.That(ledger.Current, Is.EqualTo(50));
            Assert.That(owner.CommitReceipt(Hit(), 5, true, false, false, ledger), Is.EqualTo(8)); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void BlockedDoesNotConsumeHitReward()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(), 0, false, true, false, ledger); owner.CommitReceipt(Hit(12, 2), 5, true, false, false, ledger);
            Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void EvadedOrParriedDoesNotConsumeHitReward()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(), 0, false, false, false, ledger); owner.CommitReceipt(Hit(12, 2), 5, true, false, false, ledger);
            Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void ZeroIncomingDamageDoesNotCountAsHit()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(), 0, false, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(50));
            owner.CommitReceipt(Hit(12, 2), 1, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void AcceptedArmoredHitCountsWithoutHpLoss()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery); Arm(owner, TacticalInteractionKind.PursuitMark);
            Assert.That(owner.CommitReceipt(Hit(), 0, true, false, false, ledger), Is.EqualTo(8)); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void WrongMarkedTargetPreservesMarkForCorrectTarget()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark);
            Assert.That(owner.CommitReceipt(Hit(target: Other), 10, true, false, false, null), Is.Zero);
            Assert.That(owner.CommitReceipt(Hit(target: Enemy), 10, true, false, false, null), Is.EqualTo(8));
        }
        [Test] public void TargetGenerationIsPartOfMarkIdentity()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark);
            Assert.That(owner.CommitReceipt(Hit(target: new GameplayEntityId(2, 2)), 5, true, false, false, null), Is.Zero);
            Assert.That(owner.CommitReceipt(Hit(12, 2), 5, true, false, false, null), Is.EqualTo(8));
        }
        [Test] public void MultipleTargetsGrantOneRecovery()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(target: Enemy), 5, true, false, false, ledger); owner.CommitReceipt(Hit(target: Other), 5, true, false, false, ledger);
            Assert.That(ledger.Current, Is.EqualTo(53)); Assert.That(owner.Capture().Receipts.Length, Is.EqualTo(2));
        }
        [Test] public void MultipleSegmentsConsumeOneMark()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark);
            Assert.That(owner.CommitReceipt(Hit(), 5, true, false, false, null), Is.EqualTo(8));
            Assert.That(owner.CommitReceipt(Hit(11, 2), 5, true, false, false, null), Is.Zero);
        }
        [Test] public void ActivationFrameCannotTrigger()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(10), 5, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(50));
            owner.CommitReceipt(Hit(11), 5, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void FinalDurationFrameIsInclusive()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(610), 5, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void FirstExpiredFrameRejectsTrigger()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(611), 5, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(50));
        }
        [Test] public void NoSimulationCallPreservesEffectDuringPause()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); long before = Hash(owner);
            for (int i = 0; i < 100; i++) owner.Validate(TacticalInteractionKind.ReturnMark, 10, Enemy, true, false);
            Assert.That(Hash(owner), Is.EqualTo(before));
        }
        [Test] public void TurnAdvanceClearsEffectsAndReceipts()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); owner.AdvanceTurn(7, 2, Actor);
            Assert.That(owner.CommitReceipt(Hit(), 5, true, false, false, null), Is.Zero); Assert.That(owner.UsedMask, Is.Zero);
        }
        [Test] public void SameTypeCannotRearmEvenAfterConsumption()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); owner.CommitReceipt(Hit(), 5, true, false, false, null);
            Assert.That(owner.Validate(TacticalInteractionKind.PursuitMark, 11, Enemy, true, true), Is.EqualTo(TacticalInteractionFailure.AlreadyUsedThisTurn));
        }
        [Test] public void InvalidDefenseResourceRejectsBeforePayment()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50);
            Assert.That(owner.Validate(TacticalInteractionKind.DefensePreparation, 10, Enemy, true, false), Is.EqualTo(TacticalInteractionFailure.DefenseResourceUnavailable));
            Assert.That(ledger.Current, Is.EqualTo(50)); Assert.That(owner.UsedMask, Is.Zero);
        }
        [Test] public void DefensePreparationConsumesOnlyValidatedOrdinaryBlockPressure()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.DefensePreparation);
            owner.CommitReceipt(Hit(), 0, false, true, false, null, false); Assert.That(owner.Capture().Slots[2].Active, Is.True);
            owner.CommitReceipt(Hit(12, 2), 0, false, true, false, null, true); Assert.That(owner.Capture().Slots[2].Active, Is.False);
        }
        [Test] public void DefenseResponseCommitsRealScatterAuthorityOnceAndCrossesCohesionLimit()
        {
            var world = new GameplayComponentWorld(); var actor = world.CreateEntity(); var enemy = world.CreateEntity();
            var store = world.GetOrCreateStore<GameplayScatterPressureComponent>();
            store.Set(enemy, new GameplayScatterPressureComponent(100, currentScatterPressure: 80));
            var owner = new TacticalInteractionEffectAuthority(); owner.Bind(7, 1, actor);
            owner.ActivateValidated(TacticalInteractionKind.DefensePreparation, 10, default, 0, 0, 0, 1, 1);
            var receipt = new TacticalInteractionReceipt(7, 1, 11, actor, enemy, 99, 1);
            owner.CommitReceipt(receipt, 0, false, true, false, null, out int extra, true);
            Assert.AreEqual(12, extra);
            var system = new GameplayScatterPressureSystem(); int breaks = 0;
            system.CohesionBreakEvents.Subscribe(_ => breaks++);
            system.Enqueue(new GameplayScatterPressureRequest(enemy, 10, actor.Index));
            system.Enqueue(new GameplayScatterPressureRequest(enemy, extra, actor.Index));
            var context = new GameplaySystemContext(new RuntimeFrame(11), 0d, 0d, new GameplayWorld(), Array.Empty<RuntimeCommand>(), world.Events, componentWorld: world);
            system.Tick(context); Assert.IsTrue(store.TryGet(enemy, out var pressure));
            Assert.AreEqual(100, pressure.CurrentScatterPressure); Assert.IsTrue(pressure.IsBroken); Assert.AreEqual(1, breaks);
            owner.CommitReceipt(receipt, 0, false, true, false, null, out int duplicate, true); Assert.AreEqual(0, duplicate);
            owner.CommitReceipt(new TacticalInteractionReceipt(7, 1, 11, actor, enemy, 99, 2), 0, false, true, false, null, out int next, true);
            Assert.AreEqual(0, next); Assert.AreEqual(0, system.PendingRequestCount);
        }
        [Test] public void OrdinaryBlockConsumesPressureOnlyAndLeavesHitEffectsArmed()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50);
            Arm(owner, TacticalInteractionKind.PursuitMark); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            Arm(owner, TacticalInteractionKind.ReturnMark); Arm(owner, TacticalInteractionKind.DefensePreparation);
            Assert.AreEqual(0, owner.CommitReceipt(Hit(), 0, false, true, false, ledger, out int extra, true));
            Assert.AreEqual(12, extra); Assert.AreEqual(50, ledger.Current); Assert.IsFalse(owner.ReturnPending);
            Assert.AreEqual(11, owner.ActiveMask); // pursuit, recovery, return remain armed
            Assert.AreEqual(8, owner.CommitReceipt(Hit(12, 2), 0, true, false, false, ledger));
            Assert.AreEqual(53, ledger.Current); Assert.IsTrue(owner.ReturnPending);
        }
        [Test] public void DefensePressureFirstTargetConsumesAllSegmentsAndTargetsOnlyOnce()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.DefensePreparation);
            owner.CommitReceipt(Hit(), 0, false, true, false, null, out int first, true);
            owner.CommitReceipt(Hit(11, 1, Other), 0, false, true, false, null, out int second, true);
            owner.CommitReceipt(Hit(11, 2), 0, false, true, false, null, out int third, true);
            Assert.AreEqual(12, first); Assert.AreEqual(0, second); Assert.AreEqual(0, third);
        }
        [Test] public void DefensePressureRejectsEvadeParryZeroHitDerivedAndSelf()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.DefensePreparation);
            owner.CommitReceipt(Hit(), 0, false, false, false, null, out int miss, true); Assert.AreEqual(0, miss);
            owner.CommitReceipt(Hit(12, 2), 0, false, true, true, null, out int derived, true); Assert.AreEqual(0, derived);
            owner.CommitReceipt(Hit(13, 3, Actor), 0, false, true, false, null, out int self, true); Assert.AreEqual(0, self);
            Assert.IsTrue(owner.Capture().Slots[2].Active);
        }
        [Test] public void DefenseConsumedSnapshotRestoresWithoutRepeatingPressure()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.DefensePreparation);
            owner.CommitReceipt(Hit(), 0, false, true, false, null, out _, true);
            var saved = JsonConvert.DeserializeObject<TacticalInteractionSnapshot>(JsonConvert.SerializeObject(owner.Capture()));
            var restored = Owner(); Assert.IsTrue(restored.Restore(saved, 11, Actor)); Assert.AreEqual(Hash(owner), Hash(restored));
            restored.CommitReceipt(Hit(), 0, false, true, false, null, out int repeat, true); Assert.AreEqual(0, repeat);
            restored.CommitReceipt(Hit(12, 2), 0, false, true, false, null, out int next, true); Assert.AreEqual(0, next);
        }
        [Test] public void AllFiveCardsShareFrozenCostAndImmutableAuthoredPayload()
        {
            foreach (string id in TacticalInteractionTrialCards.Ids)
            {
                Assert.IsTrue(TacticalInteractionTrialCards.TryCreate(id, out var card)); Assert.AreEqual(5, card.BaseMomentumCost);
                Assert.AreEqual(TacticalCardKind.Utility, card.Kind); Assert.AreEqual(CardDestination.Exhaust, card.DestinationAfterUse);
                Assert.IsTrue(InteractionPreparationPayload.IsPreparation(card)); Assert.IsTrue(card.Name.Contains("试用"));
            }
        }
        [Test] public void UnifiedSessionJoinUsesPaidExhaustCardAndRestoresConsumedDefenseOnce()
        {
            Assert.IsTrue(TacticalInteractionTrialCards.TryCreate("lab.defense-preparation", out var definition));
            var deck = new TacticalDeckManager(3, true); var ledger = new MomentumLedger(50);
            deck.InitializeDeck(new[] { definition }, 73); var card = deck.Hand[0];
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(card, ledger));
            uint version = card.Version;
            Assert.AreEqual(CardPlayFailure.None, deck.CommitPendingTestPreparation(ledger));
            var owner = Owner(); owner.ActivateValidated(TacticalInteractionKind.DefensePreparation, 10, default, 0, 0, 0, card.InstanceId, version);
            owner.CommitReceipt(Hit(), 0, false, true, false, ledger, out _, true);
            var saved = owner.Capture(); Assert.IsTrue(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 1, 11, Actor));
            var restored = Owner(); Assert.IsTrue(restored.RestoreJoined(saved, deck, 7, 1, 11, Actor));
            restored.CommitReceipt(Hit(), 0, false, true, false, ledger, out int repeat, true);
            Assert.AreEqual(0, repeat); Assert.AreEqual(45, ledger.Current); Assert.AreEqual(5, deck.PaidCardMomentum);
            saved.Slots[2].SourceVersion++; Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 1, 11, Actor));
        }
        [Test] public void UnifiedSessionJoinRejectsUnpaidCardWrongStageTurnAndGenerationWithoutMutation()
        {
            TacticalInteractionTrialCards.TryCreate("lab.defense-preparation", out var card);
            var deck = new TacticalDeckManager(3, true); var ledger = new MomentumLedger(50); deck.InitializeDeck(new[] {card}, 73);
            var owner = Owner(); Arm(owner, TacticalInteractionKind.DefensePreparation); var saved = owner.Capture(); long before = Hash(owner);
            Assert.IsFalse(owner.RestoreJoined(saved, deck, 7, 1, 10, Actor)); Assert.AreEqual(before, Hash(owner));
            Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 8, 1, 10, Actor));
            Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 2, 10, Actor));
            Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 1, 10, new GameplayEntityId(1, 2)));
            Assert.AreEqual(50, ledger.Current); Assert.AreEqual(0, ledger.Reserved);
        }
        [Test] public void ResourceGainPreservesActionReservation()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Assert.That(ledger.TryReserve(40, out var reservation), Is.True);
            Arm(owner, TacticalInteractionKind.MomentumRecovery); owner.CommitReceipt(Hit(), 5, true, false, false, ledger);
            Assert.That(ledger.Reserved, Is.EqualTo(40)); Assert.That(ledger.Spendable, Is.EqualTo(13));
            Assert.That(ledger.CommitReservation(reservation), Is.True); Assert.That(ledger.Current, Is.EqualTo(13));
        }
        [Test] public void GrantSaturatesAndStillConsumesOnce()
        {
            var owner = Owner(); var ledger = new MomentumLedger(99); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(), 5, true, false, false, ledger); ledger.TrySpend(5); owner.CommitReceipt(Hit(12, 2), 5, true, false, false, ledger);
            Assert.That(ledger.Current, Is.EqualTo(95));
        }
        [Test] public void FirstActualInjuryGrantsOnce()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.HurtCharge);
            var hit = new TacticalInteractionReceipt(7, 1, 11, Enemy, Actor, 9, 1);
            owner.CommitReceipt(hit, 0, false, true, false, ledger); Assert.That(ledger.Current, Is.EqualTo(50));
            owner.CommitReceipt(new TacticalInteractionReceipt(7, 1, 12, Enemy, Actor, 9, 2), 1, false, false, false, ledger);
            owner.CommitReceipt(new TacticalInteractionReceipt(7, 1, 13, Enemy, Actor, 9, 3), 10, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void SelfCostDoesNotGrantHurtCharge()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.HurtCharge);
            owner.CommitReceipt(new TacticalInteractionReceipt(7, 1, 11, Actor, Actor, 9, 1), 5, false, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(50));
        }
        [Test] public void ReturnWaitsWithoutConsumingRecordedPointAgain()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.ReturnMark); owner.CommitReceipt(Hit(), 1, true, false, false, null);
            Assert.That(owner.TryGetReturnPoint(12, out var first), Is.True); Assert.That(owner.TryGetReturnPoint(13, out var second), Is.True);
            Assert.That(first.X, Is.EqualTo(100)); Assert.That(second.Z, Is.EqualTo(300));
            owner.CompleteReturnAttempt(true); Assert.That(owner.ReturnResult, Is.EqualTo(TacticalReturnResult.Returned)); Assert.That(owner.TryGetReturnPoint(14, out _), Is.False);
        }
        [Test] public void FailedReturnClearsPointAndCannotRetry()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.ReturnMark); owner.CommitReceipt(Hit(), 1, true, false, false, null);
            owner.CompleteReturnAttempt(false); Assert.That(owner.ReturnResult, Is.EqualTo(TacticalReturnResult.DestinationBlocked));
            Assert.That(owner.TryGetReturnPoint(12, out _), Is.False); Assert.That(owner.Capture().Slots[3].X, Is.Zero);
        }
        [Test] public void PendingReturnExpiresOnTurnEnd()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.ReturnMark); owner.CommitReceipt(Hit(), 1, true, false, false, null);
            owner.AdvanceTurn(7, 2, Actor); Assert.That(owner.TryGetReturnPoint(12, out _), Is.False);
        }
        [Test] public void SnapshotIsIndependentAndRoundTripsWithReceiptDedupe()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(Hit(), 5, true, false, false, ledger); var snapshot = owner.Capture();
            var restored = Owner(); Assert.That(restored.Restore(JsonConvert.DeserializeObject<TacticalInteractionSnapshot>(JsonConvert.SerializeObject(snapshot)), 11, Actor), Is.True);
            Assert.That(Hash(restored), Is.EqualTo(Hash(owner))); restored.CommitReceipt(Hit(), 5, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(53));
            snapshot.Slots[1].Active = true; Assert.That(Hash(restored), Is.EqualTo(Hash(owner)));
        }
        [Test] public void SnapshotRejectsFutureFramesAndWrongGeneration()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); var snapshot = owner.Capture();
            Assert.That(Owner().Restore(snapshot, 9, Actor), Is.False); Assert.That(Owner().Restore(snapshot, 10, new GameplayEntityId(1, 2)), Is.False);
        }
        [Test] public void SnapshotRejectsDuplicateReceiptsAndMissingSlots()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); owner.CommitReceipt(Hit(), 1, true, false, false, null); var snapshot = owner.Capture();
            snapshot.Receipts = new[] { snapshot.Receipts[0], snapshot.Receipts[0] }; Assert.That(snapshot.IsValid(11), Is.False);
            snapshot = owner.Capture(); snapshot.Slots = new TacticalInteractionSlot[4]; Assert.That(snapshot.IsValid(11), Is.False);
        }
        [Test] public void SnapshotHashIncludesPointAndConsumption()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.ReturnMark); long before = Hash(owner); owner.CommitReceipt(Hit(), 1, true, false, false, null);
            Assert.That(Hash(owner), Is.Not.EqualTo(before)); var snapshot = owner.Capture(); snapshot.Slots[3].X++;
            var restored = Owner(); Assert.That(restored.Restore(snapshot, 11, Actor), Is.True); Assert.That(Hash(restored), Is.Not.EqualTo(Hash(owner)));
        }
        [Test] public void RealDeckPreparationPaymentExhaustsWithoutActionOrDraw()
        {
            Assert.That(TacticalInteractionTrialCards.TryCreate("lab.momentum-recovery", out var card), Is.True);
            var ledger = new MomentumLedger(50); var deck = new TacticalDeckManager(1, true); deck.InitializeDeck(new[] { card }, 1, 1);
            var instance = deck.Hand[0]; Assert.That(deck.SetPendingAction(instance, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(ledger.Reserved, Is.EqualTo(5)); Assert.That(deck.CommitPendingTestPreparation(ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(ledger.Current, Is.EqualTo(45)); Assert.That(ledger.Reserved, Is.Zero); Assert.That(deck.ResolvingAction, Is.Null);
            Assert.That(deck.Hand.Count, Is.Zero); Assert.That(deck.ExhaustPile.Count, Is.EqualTo(1)); Assert.That(deck.PaidCardMomentum, Is.EqualTo(5));
            Assert.That(deck.CommitPendingTestPreparation(ledger), Is.EqualTo(CardPlayFailure.NoPendingAction));
        }
        [Test] public void UnsupportedStandaloneDeckRejectsWithoutSpending()
        {
            TacticalInteractionTrialCards.TryCreate("lab.hurt-charge", out var card); var ledger = new MomentumLedger(50);
            var deck = new TacticalDeckManager(1); deck.InitializeDeck(new[] { card }, 1, 1);
            Assert.That(deck.SetPendingAction(deck.Hand[0], ledger), Is.EqualTo(CardPlayFailure.UnsupportedPayload)); Assert.That(ledger.Current, Is.EqualTo(50));
        }
        [Test] public void TrialCostAndRewardCannotBeEditedByPayload()
        {
            foreach (var id in TacticalInteractionTrialCards.Ids) {
                Assert.That(TacticalInteractionTrialCards.TryCreate(id, out var card), Is.True); Assert.That(card.BaseMomentumCost, Is.EqualTo(5)); Assert.That(card.DestinationAfterUse, Is.EqualTo(CardDestination.Exhaust));
                Assert.That(card.Kind, Is.EqualTo(TacticalCardKind.Utility));
            }
        }
        [Test] public void DeathOrUnbindClearsAllEligibility()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); owner.Clear();
            Assert.That(owner.CommitReceipt(Hit(), 1, true, false, false, null), Is.Zero); Assert.That(owner.UsedMask, Is.Zero); Assert.That(owner.Stage, Is.Zero);
        }
        [Test] public void WrongStageOrTurnReceiptCannotConsumeNewPreparation()
        {
            var owner = Owner(); var ledger = new MomentumLedger(50); Arm(owner, TacticalInteractionKind.MomentumRecovery);
            owner.CommitReceipt(new TacticalInteractionReceipt(8, 1, 11, Actor, Enemy, 99, 1), 1, true, false, false, ledger);
            owner.CommitReceipt(new TacticalInteractionReceipt(7, 2, 11, Actor, Enemy, 99, 1), 1, true, false, false, ledger);
            Assert.That(ledger.Current, Is.EqualTo(50)); Assert.That(owner.Capture().Slots[1].Active, Is.True);
            owner.CommitReceipt(Hit(), 1, true, false, false, ledger); Assert.That(ledger.Current, Is.EqualTo(53));
        }
        [Test] public void SnapshotPreservesSourceCardIdentityAndRejectsMissingSource()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.PursuitMark); var snapshot = owner.Capture();
            Assert.That(snapshot.Slots[0].CardInstance, Is.EqualTo(1)); Assert.That(snapshot.Slots[0].SourceVersion, Is.EqualTo(1));
            snapshot.Slots[0].CardInstance = 0; Assert.That(snapshot.IsValid(10), Is.False);
        }
        [Test] public void ExpiredPendingReturnClearsCoordinates()
        {
            var owner = Owner(); Arm(owner, TacticalInteractionKind.ReturnMark); owner.CommitReceipt(Hit(), 1, true, false, false, null); owner.Expire(611);
            Assert.That(owner.ReturnResult, Is.EqualTo(TacticalReturnResult.Expired)); Assert.That(owner.Capture().Slots[3].X, Is.Zero);
        }
    }
}
