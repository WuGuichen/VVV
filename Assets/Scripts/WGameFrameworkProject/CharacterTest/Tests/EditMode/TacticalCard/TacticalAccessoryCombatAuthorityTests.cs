using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessoryCombatAuthorityTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 1), Enemy = new GameplayEntityId(2, 1);
        private static readonly TacticalAccessoryPushParameters Push = new TacticalAccessoryPushParameters(4000, 80000, 100, 4000, 12000);
        // An immutable fixture contract, never a published card/catalog or a production P2 owner.
        private sealed class FixtureAdmission : ITacticalAccessoryAdmissionAuthority
        {
            private readonly Dictionary<ulong, TacticalAccessoryActivationData> _frozen = new Dictionary<ulong, TacticalAccessoryActivationData>();
            internal bool Reject;
            internal void Register(in TacticalAccessoryCombatState state) => _frozen.Add(state.Identity.ActivationId, TacticalAccessoryActivationData.From(state));
            public bool ValidateActivation(in TacticalAccessoryCombatState state)
                => !Reject && _frozen.TryGetValue(state.Identity.ActivationId, out var expected) && Same(expected, TacticalAccessoryActivationData.From(state));
            public bool ValidateRestoration(TacticalAccessoryAuthoritySnapshot snapshot, ulong stage, ulong turn, GameplayEntityId actor)
            {
                if (Reject) return false;
                if (snapshot == null) return _frozen.Count == 0;
                if (snapshot.Activations.Length != _frozen.Count) return false;
                foreach (var a in snapshot.Activations)
                    if (a.Stage != stage || a.ActorIndex != actor.Index || a.ActorGeneration != actor.Generation || a.FirstTurn > turn
                        || !_frozen.TryGetValue(a.ActivationId, out var expected) || !Same(expected, a)) return false;
                return true;
            }
            private static bool Same(TacticalAccessoryActivationData a, TacticalAccessoryActivationData b)
                => a.Stage == b.Stage && a.FirstTurn == b.FirstTurn && a.LastTurn == b.LastTurn
                    && a.ActorIndex == b.ActorIndex && a.ActorGeneration == b.ActorGeneration && a.MainCardInstance == b.MainCardInstance
                    && a.AccessoryItemInstanceId == b.AccessoryItemInstanceId && a.CommitId == b.CommitId && a.ActivationId == b.ActivationId
                    && a.ArmedFrame == b.ArmedFrame && a.ArmedAfterSequence == b.ArmedAfterSequence && a.Kind == b.Kind
                    && a.MomentumAmount == b.MomentumAmount && a.ReferenceSpeed == b.ReferenceSpeed && a.ReferenceMass == b.ReferenceMass
                    && a.MinimumMassScale == b.MinimumMassScale && a.MaximumMassScale == b.MaximumMassScale && a.MaximumSpeed == b.MaximumSpeed;
        }
        private static readonly Dictionary<TacticalInteractionEffectAuthority, FixtureAdmission> Admissions = new Dictionary<TacticalInteractionEffectAuthority, FixtureAdmission>();
        private static FixtureAdmission Admission(TacticalInteractionEffectAuthority owner) => Admissions[owner];
        private sealed class Sink : ITacticalAccessoryMotionSink
        {
            internal bool Accept = true;
            internal int Requests;
            internal CharacterMotionRequest Last;
            public bool TryEnqueue(in CharacterMotionRequest request)
            { if (!Accept) return false; Last = request; Requests++; return true; }
        }
        private static TacticalInteractionEffectAuthority Owner()
        { var owner = new TacticalInteractionEffectAuthority(); owner.Bind(7, 2, Actor); Admissions.Add(owner, new FixtureAdmission()); return owner; }
        private static void Arm(TacticalInteractionEffectAuthority owner, TacticalAccessoryCombatKind kind,
            ulong activation = 1, ulong lastTurn = 3)
        {
            var id = new TacticalAccessoryActivation(owner.Stage, owner.Turn, lastTurn, Actor, 40, 81, activation, activation, 100, owner.AccessoryReceiptSequence);
            TacticalAccessoryCombatState.TryArm(kind, id, kind == TacticalAccessoryCombatKind.PushAway ? 0 : 7,
                kind == TacticalAccessoryCombatKind.PushAway ? Push : default, out var frozen);
            Admission(owner).Register(frozen);
            Assert.IsTrue(owner.TryActivateAccessory(kind, id, kind == TacticalAccessoryCombatKind.PushAway ? 0 : 7,
                kind == TacticalAccessoryCombatKind.PushAway ? Push : default, Admission(owner)));
        }
        private static TacticalAccessoryCombatReceipt Hit(TacticalInteractionEffectAuthority owner, HitResolveKind kind = HitResolveKind.Damage,
            bool incoming = false, long frame = 100, int trace = 13, TacticalAccessoryDamageOrigin origin = TacticalAccessoryDamageOrigin.DirectAttack,
            DamageProfileSourceKind source = DamageProfileSourceKind.WeaponTrace, ulong? sequence = null)
            => new TacticalAccessoryCombatReceipt(owner.Stage, owner.Turn, frame, sequence ?? owner.AccessoryReceiptSequence + 1,
                incoming ? Enemy : Actor, incoming ? Actor : Enemy, new CharacterActionId(17), 12, trace,
                source, origin, kind, true, true, true, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), 80000, 1000);
        private static long Hash(TacticalInteractionEffectAuthority owner)
        { var hash = new RuntimeHashAccumulator(1, 1); owner.AddHash(ref hash); return hash.ToHash(); }

        private static TacticalAccessoryCombatState BatchCandidate(TacticalInteractionEffectAuthority owner,
            TacticalAccessoryCombatKind kind, ulong activation)
        {
            var id = new TacticalAccessoryActivation(owner.Stage, owner.Turn, 3, Actor, 40,
                checked(80 + (int)activation), 1, activation, 100, owner.AccessoryReceiptSequence);
            Assert.IsTrue(TacticalAccessoryCombatState.TryArm(kind, id, kind == TacticalAccessoryCombatKind.PushAway ? 0 : 7,
                kind == TacticalAccessoryCombatKind.PushAway ? Push : default, out var state));
            return state;
        }

        [Test] public void BatchSecondAdmissionRejectionLeavesNoFirstActivationOrReceiptMutation()
        {
            var owner = Owner();
            var first = BatchCandidate(owner, TacticalAccessoryCombatKind.BorrowMomentum, 1);
            var second = BatchCandidate(owner, TacticalAccessoryCombatKind.PushAway, 2);
            Admission(owner).Register(first); // The second exact contract is deliberately absent.
            long before = Hash(owner);
            Assert.IsFalse(owner.TryPrepareAccessoryBatch(new[] { first, second }, Admission(owner), out var prepared));
            Assert.IsNull(prepared); Assert.AreEqual(before, Hash(owner));
            Assert.AreEqual(0, owner.AccessoryActivationCount); Assert.AreEqual(0, owner.AccessoryReceiptSequence);
        }

        [Test] public void BatchPreparationFreezesAllCandidatesAndPublishesExactlyOnce()
        {
            var owner = Owner();
            var first = BatchCandidate(owner, TacticalAccessoryCombatKind.BorrowMomentum, 1);
            var second = BatchCandidate(owner, TacticalAccessoryCombatKind.PushAway, 2);
            Admission(owner).Register(first); Admission(owner).Register(second);
            var candidates = new[] { first, second }; long before = Hash(owner);
            Assert.IsTrue(owner.TryPrepareAccessoryBatch(candidates, Admission(owner), out var prepared));
            Assert.AreEqual(before, Hash(owner)); Assert.IsTrue(prepared.IsCurrent);
            candidates[0] = second; candidates[1] = first; // A writable detached caller array is not the authority.
            prepared.Publish();
            Assert.AreEqual(2, owner.AccessoryActivationCount);
            Assert.AreEqual(TacticalAccessoryCombatKind.BorrowMomentum, owner.AccessoryAt(0).Kind);
            Assert.AreEqual(TacticalAccessoryCombatKind.PushAway, owner.AccessoryAt(1).Kind);
            Assert.IsFalse(prepared.IsCurrent); long after = Hash(owner);
            Assert.Throws<InvalidOperationException>(() => prepared.Publish()); Assert.AreEqual(after, Hash(owner));
        }

        [Test] public void BatchNonMonotonicOrderRejectsBeforeAnyActivation()
        {
            var owner = Owner();
            var first = BatchCandidate(owner, TacticalAccessoryCombatKind.BorrowMomentum, 1);
            var second = BatchCandidate(owner, TacticalAccessoryCombatKind.PushAway, 2);
            Admission(owner).Register(first); Admission(owner).Register(second); long before = Hash(owner);
            Assert.IsFalse(owner.TryPrepareAccessoryBatch(new[] { second, first }, Admission(owner), out _));
            Assert.AreEqual(before, Hash(owner)); Assert.AreEqual(0, owner.AccessoryActivationCount);
        }

        [Test] public void BatchPreflightBecomesStaleWhenRootCursorChanges()
        {
            var owner = Owner(); var first = BatchCandidate(owner, TacticalAccessoryCombatKind.BorrowMomentum, 1);
            Admission(owner).Register(first);
            Assert.IsTrue(owner.TryPrepareAccessoryBatch(new[] { first }, Admission(owner), out var prepared));
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner), null, null, null));
            Assert.AreEqual(1, owner.AccessoryReceiptSequence); Assert.IsFalse(prepared.IsCurrent);
            long afterRoot = Hash(owner);
            Assert.Throws<InvalidOperationException>(() => prepared.Publish());
            Assert.AreEqual(afterRoot, Hash(owner)); Assert.AreEqual(0, owner.AccessoryActivationCount);
        }

        [Test] public void BatchCannotSurviveClearAndRebindOfSameActorStageAndTurn()
        {
            var owner = Owner(); var first = BatchCandidate(owner, TacticalAccessoryCombatKind.BorrowMomentum, 1);
            Admission(owner).Register(first);
            Assert.IsTrue(owner.TryPrepareAccessoryBatch(new[] { first }, Admission(owner), out var prepared));
            owner.Clear(); owner.Bind(7, 2, Actor);
            Assert.AreEqual(0, owner.AccessoryReceiptSequence); Assert.AreEqual(0, owner.AccessoryActivationCount);
            Assert.IsFalse(prepared.IsCurrent); long after = Hash(owner);
            Assert.Throws<InvalidOperationException>(() => prepared.Publish()); Assert.AreEqual(after, Hash(owner));
        }

        [Test] public void BatchCannotSurviveRestoreWithEqualCountsAndDifferentActivationIdentity()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum);
            var next = BatchCandidate(owner, TacticalAccessoryCombatKind.PushAway, 2); Admission(owner).Register(next);
            Assert.IsTrue(owner.TryPrepareAccessoryBatch(new[] { next }, Admission(owner), out var prepared));
            var replacement = Owner(); Arm(replacement, TacticalAccessoryCombatKind.BreakMomentum, 10);
            Assert.IsTrue(owner.Restore(replacement.Capture(), 100, Actor, Admission(replacement)));
            Assert.AreEqual(1, owner.AccessoryActivationCount); Assert.AreEqual(0, owner.AccessoryReceiptSequence);
            Assert.IsFalse(prepared.IsCurrent); long after = Hash(owner);
            Assert.Throws<InvalidOperationException>(() => prepared.Publish()); Assert.AreEqual(after, Hash(owner));
            Assert.AreEqual(10, owner.AccessoryAt(0).Identity.ActivationId);
        }

        [TestCase(HitResolveKind.Blocked)]
        [TestCase(HitResolveKind.Parried)]
        public void RealActorOwner_BorrowDefenseOnceWithConsumeReceipt(HitResolveKind kind)
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            var hit = Hit(owner, kind, true);
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(hit, ledger, null, null));
            Assert.AreEqual(27, ledger.Current); Assert.AreEqual(0, owner.CommitAccessoryReceipt(hit, ledger, null, null));
            var saved = owner.Capture(); Assert.IsTrue(saved.IsValid(100)); Assert.AreEqual(1, saved.Accessories.Consumptions.Length);
            Assert.AreEqual(7, saved.Accessories.Consumptions[0].MomentumGranted);
        }
        [Test] public void PushAdmissionFailureRetainsArmedAndNeverRecordsConsumption()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.PushAway); var sink = new Sink { Accept = false };
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner), null, null, sink));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, owner.AccessoryAt(0).Status);
            Assert.AreEqual(0, owner.Capture().Accessories.Consumptions.Length);
            Assert.IsTrue(owner.Capture().IsValid(100));
            sink.Accept = true;
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(Hit(owner, frame: 101, trace: 14), null, null, sink));
            Assert.AreEqual(1, sink.Requests); Assert.AreEqual(CharacterMotionRequestKind.Impulse, sink.Last.Kind);
        }
        [Test] public void AdmittedPushIsOnlyQueuedOnceAcrossDuplicateRootsAndRestoration()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.PushAway); var sink = new Sink();
            var hit = Hit(owner);
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(hit, null, null, sink));
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(hit, null, null, sink));
            var restored = Owner(); Assert.IsTrue(restored.Restore(owner.Capture(), 100, Actor, Admission(owner)));
            Assert.AreEqual(Hash(owner), Hash(restored)); Assert.AreEqual(0, restored.CommitAccessoryReceipt(hit, null, null, sink));
            Assert.AreEqual(1, sink.Requests); Assert.AreEqual(4000, sink.Last.XRaw);
        }
        [Test] public void SameFrameOldRootCannotConsumeNewActivationAfterRearm()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            var old = Hit(owner, HitResolveKind.Blocked, true); owner.CommitAccessoryReceipt(old, ledger, null, null);
            Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum, 2);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true), ledger, null, null));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, owner.AccessoryAt(1).Status);
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true, trace: 14), ledger, null, null));
            Assert.AreEqual(34, ledger.Current); Assert.IsTrue(owner.Capture().IsValid(100));
        }
        [Test] public void ActiveTurnLifetimeSurvivesLegacyTurnResetAndExpiresOnlyAtExplicitLastTurn()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum, lastTurn: 3);
            owner.ActivateValidated(TacticalInteractionKind.MomentumRecovery, 100, default, 0, 0, 0, 99, 1);
            owner.AdvanceTurn(7, 3, Actor); Assert.AreEqual(0, owner.UsedMask);
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, owner.AccessoryAt(0).Status); Assert.IsTrue(owner.Capture().IsValid(100));
            owner.AdvanceTurn(7, 4, Actor);
            Assert.AreEqual(TacticalAccessoryCombatStatus.Expired, owner.AccessoryAt(0).Status); Assert.IsTrue(owner.Capture().IsValid(100));
        }
        [Test] public void StageOrActorGenerationChangeAndDeathClearRemoveAllAccessoryState()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); owner.Bind(8, 1, Actor);
            Assert.AreEqual(0, owner.AccessoryActivationCount); Assert.IsNull(owner.Capture().Accessories);
            owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); owner.Bind(7, 2, new GameplayEntityId(1, 2));
            Assert.AreEqual(0, owner.AccessoryActivationCount);
            owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); owner.Clear(); Assert.IsNull(owner.Capture().Accessories);
        }
        [Test] public void JsonRoundTripRestoresActorOwnedStateReceiptHashAndLedgerSeparately()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true), ledger, null, null);
            var saved = JsonConvert.DeserializeObject<TacticalInteractionSnapshot>(JsonConvert.SerializeObject(owner.Capture()));
            var restored = Owner(); Assert.IsTrue(restored.Restore(saved, 100, Actor, Admission(owner))); Assert.AreEqual(Hash(owner), Hash(restored));
            var restoredLedger = new MomentumLedger(); restoredLedger.RestoreSessionState(ledger.CaptureSessionState());
            restored.CommitAccessoryReceipt(Hit(restored, HitResolveKind.Blocked, true, trace: 14), restoredLedger, null, null);
            Assert.AreEqual(27, restoredLedger.Current);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void CorruptSnapshotRejectsAtomically(int mutation)
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true), ledger, null, null); var saved = owner.Capture(); long hash = Hash(owner);
            switch (mutation)
            {
                case 0: saved.Accessories.Sequence++; break;
                case 1: saved.Accessories.Activations[0].ActorGeneration++; break;
                case 2: saved.Accessories.Activations[0].ConsumedSequence = 0; break;
                case 3: saved.Accessories.Consumptions = Array.Empty<TacticalAccessoryConsumptionData>(); break;
                case 4: saved.Accessories.Receipts[0].Origin = (int)TacticalAccessoryDamageOrigin.Periodic; break;
                case 5: saved.Accessories.Consumptions[0].MomentumGranted = 8; break;
            }
            Assert.IsFalse(saved.IsValid(100)); Assert.IsFalse(owner.Restore(saved, 100, Actor)); Assert.AreEqual(hash, Hash(owner));
        }
        [Test] public void CaptureDoesNotAliasActivationData()
        { var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var saved = owner.Capture(); long hash = Hash(owner); saved.Accessories.Activations[0].MomentumAmount++; Assert.AreEqual(hash, Hash(owner)); }
        [Test] public void JoinedRestoreRequiresAuthoritativeFrozenAccessoryIdentityValidation()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var saved = owner.Capture(); var deck = new TacticalDeckManager();
            Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 2, 100, Actor));
            Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 2, 100, Actor, new FixtureAdmission { Reject = true }));
            Assert.IsTrue(TacticalInteractionEffectAuthority.ValidateSessionJoin(saved, deck, 7, 2, 100, Actor, Admission(owner)));
            Assert.IsTrue(Owner().RestoreJoined(saved, deck, 7, 2, 100, Actor, Admission(owner)));
        }
        [TestCase((int)TacticalAccessoryDamageOrigin.Periodic)] [TestCase((int)TacticalAccessoryDamageOrigin.Derived)] [TestCase((int)TacticalAccessoryDamageOrigin.Unclassified)]
        public void NonRootProjectilePayloadDoesNotEvenAdvanceAccessoryReceiptSequence(int origin)
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true, origin: (TacticalAccessoryDamageOrigin)origin,
                source: DamageProfileSourceKind.ProjectileImpact), ledger, null, null));
            Assert.AreEqual(0, owner.AccessoryReceiptSequence); Assert.AreEqual(20, ledger.Current);
        }
        [Test] public void AmbiguousAreaProducerRemainsUnsupportedUntilItsSemanticContractIsConfirmed()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, source: DamageProfileSourceKind.AreaPulse), null, null, new Sink()));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, owner.AccessoryAt(0).Status);
        }
        [Test] public void SnapshotHashChangesWhenExplicitLifetimeOrConsumptionChanges()
        {
            var a = Owner(); Arm(a, TacticalAccessoryCombatKind.BorrowMomentum, lastTurn: 3);
            var b = Owner(); Arm(b, TacticalAccessoryCombatKind.BorrowMomentum, lastTurn: 4); Assert.AreNotEqual(Hash(a), Hash(b));
            long before = Hash(a); a.CommitAccessoryReceipt(Hit(a, HitResolveKind.Blocked, true), new MomentumLedger(20), null, null); Assert.AreNotEqual(before, Hash(a));
        }
        [Test] public void ProducerWitnessUsesActualDirectionAndMarksTicksAndChainsExplicitly()
        {
            var direct = CharacterTestAccessoryHitEvidence.DirectTrace(new CharacterActionId(17), new FixVector3(Fix64.FromRaw(1), Fix64.Zero, Fix64.Zero));
            Assert.AreEqual(TacticalAccessoryDamageOrigin.DirectAttack, direct.Origin); Assert.AreEqual(Fix64.One, direct.Direction.X);
            Assert.AreEqual(FixVector3.Zero, CharacterTestAccessoryHitEvidence.DirectTrace(new CharacterActionId(17), FixVector3.Zero).Direction);
            Assert.AreEqual(TacticalAccessoryDamageOrigin.Periodic, CharacterTestAccessoryHitEvidence.Periodic.Origin);
            Assert.AreEqual(TacticalAccessoryDamageOrigin.Derived, CharacterTestAccessoryHitEvidence.Derived.Origin);
            Assert.AreEqual(TacticalAccessoryDamageOrigin.Unclassified, default(CharacterTestAccessoryHitEvidence).Origin);
        }
        [TestCase(long.MinValue)] [TestCase(long.MaxValue)]
        public void ExtremeProducerDirectionFailsClosedWithoutOverflow(long raw)
        {
            var evidence = CharacterTestAccessoryHitEvidence.DirectTrace(new CharacterActionId(17), new FixVector3(Fix64.FromRaw(raw), Fix64.Zero, Fix64.Zero));
            Assert.AreEqual(FixVector3.Zero, evidence.Direction);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ForgedConsumedPushEvidenceCannotRestore(int field)
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.PushAway);
            owner.CommitAccessoryReceipt(Hit(owner), null, null, new Sink()); var saved = owner.Capture();
            switch (field)
            {
                case 0: saved.Accessories.Receipts[0].TargetMassGrams = 160000; break;
                case 1: saved.Accessories.Receipts[0].TargetPushScaleMilli = 0; break;
                case 2: saved.Accessories.Receipts[0].DirectionX = -Fix64.Scale; break;
                case 3: saved.Accessories.Receipts[0].TargetRemainsAlive = false; break;
            }
            Assert.IsFalse(saved.IsValid(100));
        }
        [Test] public void BreakOwnerProtectsReservedBalanceThroughSnapshotAndLaterReservationCommit()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BreakMomentum);
            var enemy = new MomentumLedger(20); Assert.IsTrue(enemy.TryReserve(17, out var token));
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked), null, enemy, null));
            Assert.AreEqual(17, enemy.Current); Assert.AreEqual(17, enemy.Reserved); Assert.IsTrue(owner.Capture().IsValid(100));
            Assert.AreEqual(3, owner.Capture().Accessories.Consumptions[0].MomentumSpent);
            var restored = new MomentumLedger(); restored.RestoreSessionState(enemy.CaptureSessionState());
            Assert.IsTrue(restored.CommitReservation(token)); Assert.AreEqual(0, restored.Current);
        }
        [Test] public void PositiveRawIdentityCannotActivateWithoutCommittedAdmissionAuthority()
        {
            var owner = Owner(); var id = new TacticalAccessoryActivation(7, 2, 3, Actor, 40, 81, 1, 1, 100, 0);
            Assert.IsFalse(owner.TryActivateAccessory(TacticalAccessoryCombatKind.BorrowMomentum, id, 7, default));
            Assert.AreEqual(0, owner.AccessoryActivationCount); Assert.IsNull(owner.Capture().Accessories);
        }
        [Test] public void RefusedActivationCannotPublishPartialState()
        {
            var owner = Owner(); var id = new TacticalAccessoryActivation(7, 2, 3, Actor, 40, 81, 1, 1, 100, 0); long before = Hash(owner);
            Assert.IsFalse(owner.TryActivateAccessory(TacticalAccessoryCombatKind.BorrowMomentum, id, 7, default, new FixtureAdmission { Reject = true }));
            Assert.AreEqual(before, Hash(owner)); Assert.AreEqual(0, owner.AccessoryActivationCount);
        }
        [Test] public void BareRestoreCannotBypassFrozenIdentityAdmission()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var target = Owner(); long before = Hash(target);
            Assert.IsFalse(target.Restore(owner.Capture(), 100, Actor)); Assert.AreEqual(before, Hash(target));
            Assert.IsTrue(target.Restore(owner.Capture(), 100, Actor, Admission(owner)));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void StructurallyValidButUnfrozenParametersOrIdentityCannotRestore(int mutation)
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var saved = owner.Capture(); var a = saved.Accessories.Activations[0];
            switch (mutation)
            {
                case 0: a.Kind = (int)TacticalAccessoryCombatKind.BreakMomentum; break;
                case 1: a.MomentumAmount++; break;
                case 2: a.LastTurn++; break;
                case 3: a.MainCardInstance++; break;
                case 4: a.AccessoryItemInstanceId++; break;
                case 5: a.CommitId++; break;
            }
            Assert.IsTrue(saved.IsValid(100)); var target = Owner(); long before = Hash(target);
            Assert.IsFalse(target.Restore(saved, 100, Actor, Admission(owner))); Assert.AreEqual(before, Hash(target));
        }
        [Test] public void OmittedFragmentCannotDiscardAnExpectedActivationManifest()
        {
            var owner = Owner(); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var saved = owner.Capture(); saved.Accessories = null;
            Assert.IsTrue(saved.IsValid(100)); Assert.IsFalse(Owner().Restore(saved, 100, Actor, Admission(owner)));
        }
        private static JObject PausedEnvelope(TacticalInteractionSnapshot state)
            => JObject.FromObject(new CharacterTestPausedSessionDocument {
                CompletedFrame = 100, NextFrame = 101, Scope = "lab",
                Interactions = new[] { new CharacterTestPausedInteractionSnapshot {
                    Runtime = 1, Actor = Actor.Index, Generation = Actor.Generation, State = state } } });
        private static TacticalInteractionSnapshot ReadPausedState(JObject envelope)
            => CharacterTestPausedSessionCodec.Read(envelope.ToString(Formatting.None)).Interactions[0].State;
        [Test] public void LegacyPausedCodecMissingAccessoryFragmentRestoresAsEmptyWithoutAuthority()
        {
            var source = Owner(); var envelope = PausedEnvelope(source.Capture());
            Assert.IsNull(envelope["Interactions"][0]["State"]["Accessories"]);
            var state = ReadPausedState(envelope); Assert.IsNull(state.Accessories);
            var target = Owner(); var deck = new TacticalDeckManager(6, true);
            Assert.IsTrue(target.RestoreJoined(state, deck, 7, 2, 100, Actor));
            Assert.AreEqual(0, target.AccessoryActivationCount); Assert.AreEqual(0, target.AccessoryReceiptSequence);
            Assert.AreEqual(Hash(source), Hash(target));
        }
        [Test] public void PresentPausedAccessoryFragmentRequiresAuthorityAndExactFrozenManifest()
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var state = ReadPausedState(PausedEnvelope(source.Capture()));
            Assert.IsTrue(state.IsValid(100)); var target = Owner(); var deck = new TacticalDeckManager(6, true);
            long before = Hash(target);
            Assert.IsFalse(target.RestoreJoined(state, deck, 7, 2, 100, Actor));
            Assert.AreEqual(before, Hash(target));
            Assert.IsFalse(target.RestoreJoined(state, deck, 7, 2, 100, Actor, new FixtureAdmission()));
            Assert.AreEqual(before, Hash(target));
            Assert.IsTrue(target.RestoreJoined(state, deck, 7, 2, 100, Actor, Admission(source)));
            Assert.AreEqual(Hash(source), Hash(target));
        }
        [TestCase("Version")] [TestCase("Sequence")] [TestCase("Activations")]
        [TestCase("Receipts")] [TestCase("Consumptions")]
        public void DeclaredPausedAccessoryFragmentRejectsMissingFields(string field)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture());
            Assert.IsTrue(((JObject)envelope["Interactions"][0]["State"]["Accessories"]).Remove(field));
            Assert.Throws<InvalidOperationException>(() => ReadPausedState(envelope));
        }
        [TestCase("CommitId")] [TestCase("MainCardInstance")] [TestCase("ArmedAfterSequence")]
        public void DeclaredPausedActivationRejectsMissingIdentityFields(string field)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture());
            Assert.IsTrue(((JObject)envelope["Interactions"][0]["State"]["Accessories"]["Activations"][0]).Remove(field));
            Assert.Throws<InvalidOperationException>(() => ReadPausedState(envelope));
        }
        [Test] public void RemovingWholeFragmentAfterCodecReadCannotEraseExpectedFrozenManifest()
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture());
            Assert.IsTrue(((JObject)envelope["Interactions"][0]["State"]).Remove("Accessories"));
            var state = ReadPausedState(envelope); Assert.IsNull(state.Accessories);
            var target = Owner(); Arm(target, TacticalAccessoryCombatKind.PushAway); long before = Hash(target);
            var deck = new TacticalDeckManager(6, true);
            Assert.IsFalse(target.RestoreJoined(state, deck, 7, 2, 100, Actor, Admission(source)));
            Assert.AreEqual(before, Hash(target));
            Assert.IsFalse(target.RestoreJoined(state, deck, 7, 2, 100, Actor));
            Assert.AreEqual(before, Hash(target));
        }
        [TestCase("CommitId")] [TestCase("MainCardInstance")]
        [TestCase("MomentumAmount")] [TestCase("LastTurn")]
        public void ReadableButTamperedPausedActivationCannotRestore(string field)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture());
            var activation = (JObject)envelope["Interactions"][0]["State"]["Accessories"]["Activations"][0];
            activation[field] = (long)activation[field] + 1;
            var state = ReadPausedState(envelope); Assert.IsTrue(state.IsValid(100));
            var target = Owner(); long before = Hash(target);
            Assert.IsFalse(target.RestoreJoined(state, new TacticalDeckManager(6, true), 7, 2, 100, Actor, Admission(source)));
            Assert.AreEqual(before, Hash(target));
        }
        [TestCase(false)] [TestCase(true)]
        public void DeclaredNullOrUnknownPausedAccessoryFieldIsRejected(bool unknown)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture()); var state = (JObject)envelope["Interactions"][0]["State"];
            if (unknown) ((JObject)state["Accessories"])["UnknownField"] = 1;
            else state["Accessories"] = JValue.CreateNull();
            Assert.Throws<InvalidOperationException>(() => ReadPausedState(envelope));
        }
        [TestCase("Activations")] [TestCase("Receipts")] [TestCase("Consumptions")]
        public void PresentPausedFragmentWithNullCollectionsCannotRestore(string field)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture());
            ((JObject)envelope["Interactions"][0]["State"]["Accessories"])[field] = JValue.CreateNull();
            var state = ReadPausedState(envelope); Assert.IsFalse(state.IsValid(100));
            var target = Owner(); long before = Hash(target);
            Assert.IsFalse(target.RestoreJoined(state, new TacticalDeckManager(6, true), 7, 2, 100, Actor, Admission(source)));
            Assert.AreEqual(before, Hash(target));
        }
        [TestCase("Version")] [TestCase("Activations")] [TestCase("Accessories")]
        public void DeclaredPausedAccessoryWrongTypesAreRejected(string field)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture()); var state = (JObject)envelope["Interactions"][0]["State"];
            if (field == "Accessories") state[field] = new JArray();
            else ((JObject)state["Accessories"])[field] = field == "Version" ? (JToken)new JValue("1") : new JObject();
            Assert.Throws<InvalidOperationException>(() => ReadPausedState(envelope));
        }
        [TestCase(false)] [TestCase(true)]
        public void PausedCodecRejectsDuplicateDeclaredAccessoryFields(bool duplicateWholeFragment)
        {
            var source = Owner(); Arm(source, TacticalAccessoryCombatKind.BorrowMomentum);
            var envelope = PausedEnvelope(source.Capture()); var state = (JObject)envelope["Interactions"][0]["State"];
            string fragment = state["Accessories"].ToString(Formatting.None);
            string json = envelope.ToString(Formatting.None);
            string replacement = duplicateWholeFragment
                ? "\"Accessories\":" + fragment + ",\"Accessories\":" + fragment
                : "\"Accessories\":" + fragment.Insert(1, "\"Version\":1,");
            json = json.Replace("\"Accessories\":" + fragment, replacement);
            Assert.Throws<JsonReaderException>(() => CharacterTestPausedSessionCodec.Read(json));
        }
        [TestCase((int)TacticalAccessoryCombatKind.BorrowMomentum)]
        [TestCase((int)TacticalAccessoryCombatKind.PushAway)]
        [TestCase((int)TacticalAccessoryCombatKind.BreakMomentum)]
        public void FirstActivationCannotConsumeSameFrameRootCommittedBeforeIt(int effect)
        {
            var kind = (TacticalAccessoryCombatKind)effect; var owner = Owner(); var actorLedger = new MomentumLedger(20);
            var defendingLedger = new MomentumLedger(20); var sink = new Sink();
            bool incoming = kind == TacticalAccessoryCombatKind.BorrowMomentum;
            var outcome = kind == TacticalAccessoryCombatKind.PushAway ? HitResolveKind.Damage : HitResolveKind.Blocked;
            var beforeArm = Hit(owner, outcome, incoming);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(beforeArm, actorLedger, defendingLedger, sink));
            Assert.AreEqual(1, owner.AccessoryReceiptSequence); Assert.AreEqual(0, owner.AccessoryActivationCount);
            Assert.AreEqual(20, actorLedger.Current); Assert.AreEqual(20, defendingLedger.Current); Assert.AreEqual(0, sink.Requests);
            Arm(owner, kind);
            Assert.AreEqual(1, owner.AccessoryAt(0).Identity.ArmedAfterReceiptSequence);
            // Model the production producer offering a new sequence for a replay of the OLD key.
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, outcome, incoming), actorLedger, defendingLedger, sink));
            Assert.AreEqual(1, owner.AccessoryReceiptSequence); Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, owner.AccessoryAt(0).Status);
            Assert.AreEqual(20, actorLedger.Current); Assert.AreEqual(20, defendingLedger.Current); Assert.AreEqual(0, sink.Requests);
            // A different root key at the SAME frame is genuinely after activation and must trigger.
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(Hit(owner, outcome, incoming, trace: 14), actorLedger, defendingLedger, sink));
            Assert.AreEqual(2, owner.AccessoryReceiptSequence); Assert.AreEqual(TacticalAccessoryCombatStatus.Consumed, owner.AccessoryAt(0).Status);
            Assert.AreEqual(incoming ? 27 : 20, actorLedger.Current);
            Assert.AreEqual(kind == TacticalAccessoryCombatKind.BreakMomentum ? 13 : 20, defendingLedger.Current);
            Assert.AreEqual(kind == TacticalAccessoryCombatKind.PushAway ? 1 : 0, sink.Requests);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, outcome, incoming, trace: 14), actorLedger, defendingLedger, sink));
            Assert.IsTrue(owner.Capture().IsValid(100));
        }
        [Test] public void CursorBeforeFirstActivationSurvivesJsonJoinedRestoreWithoutP2Effects()
        {
            var owner = Owner(); owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true), null, null, null);
            var snapshot = JsonConvert.DeserializeObject<TacticalInteractionSnapshot>(JsonConvert.SerializeObject(owner.Capture()));
            Assert.AreEqual(0, snapshot.Accessories.Activations.Length); Assert.IsTrue(snapshot.IsValid(100));
            var restored = Owner(); Assert.IsTrue(restored.RestoreJoined(snapshot, new TacticalDeckManager(), 7, 2, 100, Actor));
            Assert.AreEqual(Hash(owner), Hash(restored)); Assert.AreEqual(1, restored.AccessoryReceiptSequence);
            Arm(restored, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            Assert.AreEqual(0, restored.CommitAccessoryReceipt(Hit(restored, HitResolveKind.Blocked, true), ledger, null, null));
            Assert.AreEqual(20, ledger.Current); Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, restored.AccessoryAt(0).Status);
            Assert.AreEqual(1, restored.CommitAccessoryReceipt(Hit(restored, HitResolveKind.Blocked, true, trace: 14), ledger, null, null));
            Assert.AreEqual(27, ledger.Current);
        }
        [Test] public void CursorBeforeFirstActivationSurvivesTurnBoundary()
        {
            var owner = Owner(); owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true), null, null, null);
            owner.AdvanceTurn(7, 3, Actor); Arm(owner, TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(20);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true), ledger, null, null));
            Assert.AreEqual(1, owner.AccessoryReceiptSequence); Assert.AreEqual(20, ledger.Current);
            Assert.AreEqual(1, owner.CommitAccessoryReceipt(Hit(owner, HitResolveKind.Blocked, true, trace: 14), ledger, null, null));
            Assert.AreEqual(27, ledger.Current); Assert.IsTrue(owner.Capture().IsValid(100));
        }
        [TestCase(HitResolveKind.Evaded)] [TestCase(HitResolveKind.Invincible)]
        public void NonTriggerRootStillOccupiesCursorBeforeFirstActivation(HitResolveKind initialOutcome)
        {
            var owner = Owner(); var ledger = new MomentumLedger(20);
            Assert.AreEqual(0, owner.CommitAccessoryReceipt(Hit(owner, initialOutcome, true), ledger, null, null));
            Assert.AreEqual(1, owner.AccessoryReceiptSequence); Assert.IsTrue(owner.Capture().IsValid(100));
            var cursor = JsonConvert.DeserializeObject<TacticalInteractionSnapshot>(JsonConvert.SerializeObject(owner.Capture()));
            var restored = Owner(); Assert.IsTrue(restored.Restore(cursor, 100, Actor)); Arm(restored, TacticalAccessoryCombatKind.BorrowMomentum);
            Assert.AreEqual(0, restored.CommitAccessoryReceipt(Hit(restored, HitResolveKind.Blocked, true), ledger, null, null));
            Assert.AreEqual(1, restored.AccessoryReceiptSequence); Assert.AreEqual(20, ledger.Current);
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, restored.AccessoryAt(0).Status);
            Assert.AreEqual(1, restored.CommitAccessoryReceipt(Hit(restored, HitResolveKind.Blocked, true, trace: 14), ledger, null, null));
            Assert.AreEqual(27, ledger.Current); Assert.IsTrue(restored.Capture().IsValid(100));
        }
    }
}
