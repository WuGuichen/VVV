using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // Explicit policy inputs only. No default chooses a new fee/refund rule for gameplay.
    internal enum TacticalFixedCardPaymentTiming { Unspecified, AllAtConfirmation, CardAndAccessoriesThenActualActions }

    internal sealed class TacticalFixedCardFeePlan
    {
        private TacticalFixedCardFeePlan(TacticalFixedAssemblyManifest assembly, TacticalFixedCardPaymentTiming timing, int actionBudget)
        {
            Assembly = assembly; Timing = timing; MainCost = assembly.MainConfirmationCost;
            AccessoryCost = assembly.AccessoryConfirmationCost; MaximumActionBudget = actionBudget;
            MaximumAdmissionCost = checked(MainCost + AccessoryCost + actionBudget);
            ConfirmationCost = timing == TacticalFixedCardPaymentTiming.AllAtConfirmation
                ? MaximumAdmissionCost : checked(MainCost + AccessoryCost);
            ReservedAfterConfirmation = MaximumAdmissionCost - ConfirmationCost;
        }
        internal TacticalFixedAssemblyManifest Assembly { get; }
        internal TacticalFixedCardPaymentTiming Timing { get; }
        internal int MainCost { get; }
        internal int AccessoryCost { get; }
        internal int MaximumActionBudget { get; }
        internal int MaximumAdmissionCost { get; }
        internal int ConfirmationCost { get; }
        internal int ReservedAfterConfirmation { get; }

        /// <remarks>LowFreqAlloc. The caller supplies a proven bounded compiled-action budget; this type computes
        /// the explicit payment layout, but neither compiles a budget nor authorizes a reservation/payment.</remarks>
        internal static bool TryCreate(TacticalFixedAssemblyManifest assembly, TacticalFixedCardPaymentTiming timing,
            int provenActionBudget, out TacticalFixedCardFeePlan plan)
        {
            plan = null;
            if (assembly == null || provenActionBudget < 0
                || (timing != TacticalFixedCardPaymentTiming.AllAtConfirmation
                    && timing != TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions)) return false;
            try { plan = new TacticalFixedCardFeePlan(assembly, timing, provenActionBudget); return true; }
            catch (OverflowException) { return false; }
        }
        internal object CanonicalData() => new { Timing, MainCost, AccessoryCost, MaximumActionBudget,
            MaximumAdmissionCost, ConfirmationCost, ReservedAfterConfirmation };
    }

    /// <summary>Immutable prospective/confirmed joint identity. Only the real joint payment owner may confirm it.
    /// Serialized incoming state never constructs its own trusted authority from this record.</summary>
    internal sealed class TacticalFixedCardCommitManifest
    {
        internal const int FormatVersion = 1;
        private readonly TacticalAccessoryCombatState[] _activations;
        private TacticalFixedCardCommitManifest(TacticalFixedAssemblyManifest assembly, TacticalFixedCardFeePlan fees,
            ulong stage, ulong turn, GameplayEntityId actor, ulong battleCardInstance, uint cardVersion,
            ulong commitId, ulong executionId, long frame, ulong receiptCursor, ulong reservationToken,
            TacticalExecutionInput input, TacticalAccessoryCombatState[] activations, bool confirmed)
        {
            Assembly = assembly; Fees = fees; Stage = stage; Turn = turn; Actor = actor;
            BattleCardInstance = battleCardInstance; CardVersion = cardVersion; CommitId = commitId; ExecutionId = executionId;
            Frame = frame; ReceiptCursor = receiptCursor; ReservationToken = reservationToken; Input = input;
            _activations = activations; IsConfirmed = confirmed;
            var canonical = new { FormatVersion, Assembly = assembly.Fingerprint, Fees = fees.CanonicalData(),
                Stage, Turn, ActorIndex = actor.Index, ActorGeneration = actor.Generation, BattleCardInstance,
                CardVersion, CommitId, ExecutionId, Frame, ReceiptCursor, ReservationToken, IsConfirmed,
                Input = new { input.Kind, input.Source, input.Binding.StageRun, input.Binding.Turn,
                    input.Binding.CardInstance, input.Binding.CardVersion, ActorIndex = input.Binding.Actor.Index,
                    ActorGeneration = input.Binding.Actor.Generation, TargetIndex = input.TargetCharacter.Index,
                    TargetGeneration = input.TargetCharacter.Generation, PointX = input.Point.X, PointY = input.Point.Y,
                    PointZ = input.Point.Z, OriginX = input.SelectionOrigin.X, OriginY = input.SelectionOrigin.Y, OriginZ = input.SelectionOrigin.Z },
                Activations = activations.Select(value => TacticalAccessoryActivationData.From(value)).ToArray() };
            Digest = RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(canonical));
        }
        internal TacticalFixedAssemblyManifest Assembly { get; }
        internal TacticalFixedCardFeePlan Fees { get; }
        internal ulong Stage { get; }
        internal ulong Turn { get; }
        internal GameplayEntityId Actor { get; }
        internal ulong BattleCardInstance { get; }
        internal uint CardVersion { get; }
        internal ulong CommitId { get; }
        internal ulong ExecutionId { get; }
        internal long Frame { get; }
        internal ulong ReceiptCursor { get; }
        internal ulong ReservationToken { get; }
        internal TacticalExecutionInput Input { get; }
        internal bool IsConfirmed { get; }
        internal RuntimeHashDigest Digest { get; }
        internal int ActivationCount => _activations.Length;
        internal TacticalAccessoryCombatState ActivationAt(int index) => _activations[index];

        /// <remarks>LowFreqAlloc. Pure identity/parameter preparation; does not increment counters, reserve,
        /// pay, activate, start execution or change the deck. IDs are proposed by the owning joint transaction.</remarks>
        internal static bool TryPrepare(TacticalFixedAssemblyManifest assembly, TacticalFixedCardFeePlan fees,
            TacticalCardInstance card, ulong stage, ulong turn, GameplayEntityId actor, ulong commitId,
            ulong executionId, long frame, ulong receiptCursor, ulong firstActivationId, ulong reservationToken,
            TacticalExecutionInput input, out TacticalFixedCardCommitManifest record)
        {
            record = null;
            if (assembly == null || fees?.Assembly != assembly || card?.Definition.FixedAssembly != assembly
                || card.Zone != CardZone.PendingAction || card.Version == 0 || card.Version == uint.MaxValue
                || stage == 0 || turn == 0 || !actor.IsValid
                || commitId == 0 || executionId == 0 || frame < 0 || reservationToken == 0 || receiptCursor == ulong.MaxValue
                || assembly.Accessories.Count > 0 && (firstActivationId == 0
                    || firstActivationId > ulong.MaxValue - checked((ulong)assembly.Accessories.Count - 1))) return false;
            if (input.Kind != TacticalExecutionInputKind.None
                && (!input.Binding.Equals(new TacticalExecutionInputBinding(stage, turn, card.InstanceId, card.Version, actor))
                    || input.Source < TacticalExecutionInputSource.Manual || input.Source > TacticalExecutionInputSource.Automatic
                    || input.Kind == TacticalExecutionInputKind.Character && !input.TargetCharacter.IsValid
                    || input.Kind != TacticalExecutionInputKind.Character && input.Kind != TacticalExecutionInputKind.WorldPoint)) return false;
            var activations = new TacticalAccessoryCombatState[assembly.Accessories.Count];
            for (int i = 0; i < activations.Length; i++)
            {
                var accessory = assembly.Accessories[i];
                if (!accessory.TryGetLastTurn(turn, out var lastTurn)) return false;
                var identity = new TacticalAccessoryActivation(stage, turn, lastTurn, actor, assembly.HeldCardInstance,
                    accessory.ItemInstance, commitId, firstActivationId + (ulong)i, frame, receiptCursor);
                if (!TacticalAccessoryCombatState.TryArm(accessory.Kind, identity, accessory.MomentumAmount,
                    accessory.Push, out activations[i])) return false;
            }
            record = new TacticalFixedCardCommitManifest(assembly, fees, stage, turn, actor, card.InstanceId, card.Version,
                commitId, executionId, frame, receiptCursor, reservationToken, input, activations, false);
            return true;
        }

        /// <summary>LowFreqAlloc. Trusted joint-payment receipt promotion only; no ledger is changed here.
        /// The caller must already have paid the exact plan on this token in its non-reentrant commit boundary.</summary>
        internal bool TryConfirmPayment(ulong paidReservationToken, int paidConfirmationAmount,
            out TacticalFixedCardCommitManifest confirmed)
        {
            confirmed = null;
            if (IsConfirmed || paidReservationToken != ReservationToken || paidConfirmationAmount != Fees.ConfirmationCost) return false;
            confirmed = new TacticalFixedCardCommitManifest(Assembly, Fees, Stage, Turn, Actor, BattleCardInstance,
                CardVersion, CommitId, ExecutionId, Frame, ReceiptCursor, ReservationToken, Input, _activations, true);
            return true;
        }
    }

    /// <summary>Validation view over trusted immutable records. A provisional view can prepare a batch;
    /// it cannot authorize restoration. The joint owner publishes only the confirmed view.</summary>
    internal sealed class TacticalFixedAccessoryAdmissionAuthority : ITacticalAccessoryAdmissionAuthority
    {
        // Registered optional character extension fields in network schema 41.
        private static class HashFields
        {
            internal const int Format = 362, Ordering = 363, Restorable = 364, Commits = 365, CommitDigest = 1;
        }
        private readonly TacticalFixedCardCommitManifest[] _commits;
        private readonly bool _restorable;
        internal bool IsRestorable => _restorable;
        internal int CommitCount => _commits.Length;
        internal TacticalFixedCardCommitManifest CommitAt(int index) => _commits[index];
        internal bool Extends(TacticalFixedAccessoryAdmissionAuthority previous)
        {
            if (!_restorable || previous == null || !previous._restorable || _commits.Length < previous._commits.Length) return false;
            for (int i = 0; i < previous._commits.Length; i++)
                if (!_commits[i].Digest.Equals(previous._commits[i].Digest)) return false;
            return true;
        }
        private TacticalFixedAccessoryAdmissionAuthority(TacticalFixedCardCommitManifest[] commits, bool restorable)
        { _commits = commits; _restorable = restorable; }

        /// <remarks>LowFreqAlloc. Creates an ordered private view from known trusted commit-owner records.
        /// This factory intentionally has no JSON/DTO/inventory reconstruction overload.</remarks>
        internal static bool TryCreate(IReadOnlyList<TacticalFixedCardCommitManifest> trustedCommits,
            bool forRestoration, out TacticalFixedAccessoryAdmissionAuthority authority)
        {
            authority = null;
            if (trustedCommits == null) return false;
            var frozen = new TacticalFixedCardCommitManifest[trustedCommits.Count];
            ulong previousCommit = 0, previousActivation = 0, stage = 0; GameplayEntityId actor = default;
            for (int i = 0; i < frozen.Length; i++)
            {
                var commit = trustedCommits[i];
                if (commit == null || commit.CommitId <= previousCommit || forRestoration && !commit.IsConfirmed
                    || i > 0 && (commit.Stage != stage || commit.Actor != actor || commit.Turn < frozen[i - 1].Turn
                        || commit.Frame < frozen[i - 1].Frame || commit.ReceiptCursor < frozen[i - 1].ReceiptCursor)) return false;
                for (int j = 0; j < commit.ActivationCount; j++)
                {
                    ulong activation = commit.ActivationAt(j).Identity.ActivationId;
                    if (activation <= previousActivation) return false; previousActivation = activation;
                }
                frozen[i] = commit; previousCommit = commit.CommitId; stage = commit.Stage; actor = commit.Actor;
            }
            authority = new TacticalFixedAccessoryAdmissionAuthority(frozen, forRestoration); return true;
        }

        public bool ValidateActivation(in TacticalAccessoryCombatState candidate)
        {
            if (candidate.Status != TacticalAccessoryCombatStatus.Armed || candidate.ConsumedSequence != 0) return false;
            foreach (var commit in _commits)
                for (int i = 0; i < commit.ActivationCount; i++)
                {
                    var expected = commit.ActivationAt(i);
                    if (expected.Identity.ActivationId == candidate.Identity.ActivationId)
                        return Same(expected, TacticalAccessoryActivationData.From(candidate));
                }
            return false;
        }

        public bool ValidateRestoration(TacticalAccessoryAuthoritySnapshot snapshot, ulong stage, ulong turn, GameplayEntityId actor)
        {
            if (!_restorable) return false;
            int expectedCount = 0;
            foreach (var commit in _commits)
            {
                if (commit.Stage != stage || commit.Actor != actor || commit.Turn > turn) return false;
                expectedCount = checked(expectedCount + commit.ActivationCount);
            }
            if (snapshot == null) return expectedCount == 0;
            if (snapshot.Activations == null || snapshot.Activations.Length != expectedCount) return false;
            int index = 0;
            foreach (var commit in _commits)
                for (int i = 0; i < commit.ActivationCount; i++)
                    if (snapshot.Activations[index] == null || !Same(commit.ActivationAt(i), snapshot.Activations[index++])) return false;
            return true;
        }

        // Runtime mutable status and consumption are validated by the effect snapshot itself. These fields are
        // the immutable source contract and must match the trusted commit manifest for every state, including expiry.
        private static bool Same(in TacticalAccessoryCombatState expected, TacticalAccessoryActivationData actual)
        {
            var id = expected.Identity; var push = expected.Push;
            return actual.Stage == id.Stage && actual.FirstTurn == id.FirstTurn && actual.LastTurn == id.LastTurn
                && actual.ActorIndex == id.Actor.Index && actual.ActorGeneration == id.Actor.Generation
                && actual.MainCardInstance == id.MainCardInstance && actual.AccessoryItemInstanceId == id.AccessoryItemInstanceId
                && actual.CommitId == id.CommitId && actual.ActivationId == id.ActivationId && actual.ArmedFrame == id.ArmedFrame
                && actual.ArmedAfterSequence == id.ArmedAfterReceiptSequence && actual.Kind == (int)expected.Kind
                && actual.MomentumAmount == expected.MomentumAmount && actual.ReferenceSpeed == push.ReferenceSpeedMillimetersPerSecond
                && actual.ReferenceMass == push.ReferenceMassGrams && actual.MinimumMassScale == push.MinimumMassScaleMilli
                && actual.MaximumMassScale == push.MaximumMassScaleMilli && actual.MaximumSpeed == push.MaximumSpeedMillimetersPerSecond;
        }

        /// <summary>NoAlloc. Fixed owner-local extension fields; publication/source digests are prepared cold.</summary>
        internal void AddHash(ref RuntimeHashAccumulator hash)
        {
            hash.AddInt(HashFields.Format, TacticalFixedCardCommitManifest.FormatVersion);
            hash.AddInt(HashFields.Ordering, TacticalFixedAssemblyManifest.OrderingVersion);
            hash.AddInt(HashFields.Restorable, _restorable ? 1 : 0);
            hash.BeginArray(HashFields.Commits, _commits.Length);
            for (int i = 0; i < _commits.Length; i++) { hash.BeginElement(i); hash.AddDigest(HashFields.CommitDigest, _commits[i].Digest); hash.EndElement(); }
            hash.EndArray();
        }
    }
}
