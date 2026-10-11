using System;
using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Persistence
{
    public enum InMemorySaveGameFaultPoint { BeforeCommitPublication = 0, BeforeDeletePublication = 1 }

    /// <summary>Optional deterministic fault seam for provider tests. Returning a non-empty error prevents publication.</summary>
    public interface IInMemorySaveGameStoreFaultInjector
    {
        SaveGameError Inject(InMemorySaveGameFaultPoint point, SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation);
    }

    /// <summary>Deterministic, thread-safe in-memory CAS provider retaining the current and one previous generation.</summary>
    public sealed class InMemorySaveGameStore : ISaveGameStore
    {
        private sealed class Entry
        {
            public Entry(SaveGameGeneration generation, SaveGameRecordKind kind, DateTime updatedAtUtc, SaveGameEncodedEnvelope envelope) { Generation = generation; Kind = kind; UpdatedAtUtc = updatedAtUtc; Envelope = envelope; }
            public readonly SaveGameGeneration Generation; public readonly SaveGameRecordKind Kind; public readonly DateTime UpdatedAtUtc; public readonly SaveGameEncodedEnvelope Envelope;
        }

        private sealed class SlotState
        {
            public readonly List<Entry> Entries = new List<Entry>(2);
            public Entry Head => Entries[Entries.Count - 1];
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, Dictionary<string, SlotState>> _profiles = new Dictionary<string, Dictionary<string, SlotState>>(StringComparer.Ordinal);
        private readonly IInMemorySaveGameStoreFaultInjector _faultInjector;
        private long _operationSequence;
        private int _activeOperations;

        public InMemorySaveGameStore(IInMemorySaveGameStoreFaultInjector faultInjector = null) { _faultInjector = faultInjector; }

        public ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request, CancellationToken cancellationToken = default)
        {
            var operationId = NextOperationId(); using (var operation = EnterOperation())
            {
            if (!operation.Entered) return Fail<SaveGameCommitResult>(operationId, SaveGameErrorCode.Busy, "operations", "Concurrent operation limit is reached.");
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameCommitResult>(operationId);
            var validation = ValidateRequest(request == null ? default : request.Profile, request == null ? default : request.Slot);
            if (!validation.IsNone) return SaveGameOperations.Failure<SaveGameCommitResult>(operationId, validation);
            if (request.Envelope == null) return Fail<SaveGameCommitResult>(operationId, SaveGameErrorCode.InvalidEnvelope, "envelope", "Commit envelope is null.");
            var decoded = SaveGameEnvelopeCodec.Decode(request.Envelope); if (!decoded.IsSuccess) return SaveGameOperations.Failure<SaveGameCommitResult>(operationId, decoded.Error);
            var envelope = decoded.Envelope;
            if (envelope.RecordKind != SaveGameRecordKind.Save || envelope.Profile != request.Profile || envelope.Slot != request.Slot)
                return Fail<SaveGameCommitResult>(operationId, SaveGameErrorCode.EnvelopePayloadMismatch, "envelope", "Commit envelope kind or identity does not match the request.");
            SaveGameGeneration next; if (!request.ExpectedHead.TryNext(out next)) return Fail<SaveGameCommitResult>(operationId, SaveGameErrorCode.GenerationExhausted, "expectedHead", "Generation cannot be incremented.");
            if (envelope.Generation != next) return Fail<SaveGameCommitResult>(operationId, SaveGameErrorCode.GenerationConflict, "generation", "Envelope generation is not expected head plus one.");
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameCommitResult>(operationId);
            lock (_gate)
            {
                var observed = GetSlotState(request.Profile, request.Slot, false);
                var observedGeneration = observed == null ? SaveGameGeneration.None : observed.Head.Generation;
                if (observedGeneration != request.ExpectedHead) return Conflict<SaveGameCommitResult>(operationId);
            }
            var fault = TryInject(InMemorySaveGameFaultPoint.BeforeCommitPublication, request.Profile, request.Slot, next);
            if (!fault.IsNone) return SaveGameOperations.Failure<SaveGameCommitResult>(operationId, fault);
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameCommitResult>(operationId);
            lock (_gate)
            {
                var state = GetSlotState(request.Profile, request.Slot, false); var actual = state == null ? SaveGameGeneration.None : state.Head.Generation;
                if (actual != request.ExpectedHead) return Conflict<SaveGameCommitResult>(operationId);
                if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameCommitResult>(operationId);
                if (state == null) state = GetSlotState(request.Profile, request.Slot, true);
                if (state == null) return Fail<SaveGameCommitResult>(operationId, SaveGameErrorCode.LimitExceeded, "slots", "Profile already contains the maximum number of slots.");
                Publish(state, new Entry(next, SaveGameRecordKind.Save, envelope.UpdatedAtUtc, decoded.Encoded));
                return SaveGameOperations.Success(operationId, new SaveGameCommitResult(next));
            }
            }
        }

        public ISaveGameOperation<SaveGameReadResult> Read(SaveGameReadRequest request, CancellationToken cancellationToken = default)
        {
            var operationId = NextOperationId(); using (var operation = EnterOperation())
            {
            if (!operation.Entered) return Fail<SaveGameReadResult>(operationId, SaveGameErrorCode.Busy, "operations", "Concurrent operation limit is reached.");
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameReadResult>(operationId);
            var validation = ValidateRequest(request == null ? default : request.Profile, request == null ? default : request.Slot); if (!validation.IsNone) return SaveGameOperations.Failure<SaveGameReadResult>(operationId, validation);
            if (!request.Selection.IsValid ||
                request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryOnly && request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryThenLastKnownGood ||
                request.Selection.Kind == SaveGameReadSelectionKind.ExactGeneration && request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryOnly)
                return Fail<SaveGameReadResult>(operationId, SaveGameErrorCode.InvalidSelection, "selection", "Read selection or recovery policy is invalid.");
            lock (_gate)
            {
                var state = GetSlotState(request.Profile, request.Slot, false); if (state == null) return Fail<SaveGameReadResult>(operationId, SaveGameErrorCode.SlotNotFound, "slot", "Slot does not exist.");
                var original = state.Head.Generation; Entry selected = null;
                if (request.Selection.Kind == SaveGameReadSelectionKind.Current) selected = state.Head;
                else for (var i = 0; i < state.Entries.Count; i++) if (state.Entries[i].Generation == request.Selection.Generation) { selected = state.Entries[i]; break; }
                if (selected == null) return Fail<SaveGameReadResult>(operationId, SaveGameErrorCode.GenerationNotFound, "generation", "Exact generation is not retained.");
                if (selected.Kind == SaveGameRecordKind.Tombstone && !request.IncludeDeleted) return Fail<SaveGameReadResult>(operationId, SaveGameErrorCode.SlotDeleted, "slot", "Selected generation is a tombstone.");
                return SaveGameOperations.Success(operationId, new SaveGameReadResult(new SaveGameEncodedEnvelope(selected.Envelope.Bytes), original, selected.Generation, SaveGameRecoverySource.Primary));
            }
            }
        }

        public ISaveGameOperation<SaveGameListResult> List(SaveGameListRequest request, CancellationToken cancellationToken = default)
        {
            var operationId = NextOperationId(); using (var operation = EnterOperation())
            {
            if (!operation.Entered) return Fail<SaveGameListResult>(operationId, SaveGameErrorCode.Busy, "operations", "Concurrent operation limit is reached.");
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameListResult>(operationId);
            if (request == null || !request.Profile.IsValid) return Fail<SaveGameListResult>(operationId, SaveGameErrorCode.InvalidProfile, "profile", "Profile id is invalid.");
            lock (_gate)
            {
                Dictionary<string, SlotState> slots; if (!_profiles.TryGetValue(request.Profile.Value, out slots)) return SaveGameOperations.Success(operationId, new SaveGameListResult(Array.Empty<SaveGameSlotSummary>()));
                var keys = new List<string>(slots.Count); foreach (var pair in slots) if (request.IncludeDeleted || pair.Value.Head.Kind != SaveGameRecordKind.Tombstone) keys.Add(pair.Key); keys.Sort(StringComparer.Ordinal);
                if (keys.Count > SaveGameLimits.MaximumListedSlots) return Fail<SaveGameListResult>(operationId, SaveGameErrorCode.LimitExceeded, "slots", "Slot listing exceeds the v1 limit.");
                var summaries = new SaveGameSlotSummary[keys.Count]; for (var i = 0; i < keys.Count; i++) { var head = slots[keys[i]].Head; summaries[i] = new SaveGameSlotSummary(new SaveGameSlotId(keys[i]), head.Generation, head.UpdatedAtUtc, head.Kind); }
                return SaveGameOperations.Success(operationId, new SaveGameListResult(summaries));
            }
            }
        }

        public ISaveGameOperation<SaveGameDeleteResult> Delete(SaveGameDeleteRequest request, CancellationToken cancellationToken = default)
        {
            var operationId = NextOperationId(); using (var operation = EnterOperation())
            {
            if (!operation.Entered) return Fail<SaveGameDeleteResult>(operationId, SaveGameErrorCode.Busy, "operations", "Concurrent operation limit is reached.");
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameDeleteResult>(operationId);
            var validation = ValidateRequest(request == null ? default : request.Profile, request == null ? default : request.Slot); if (!validation.IsNone) return SaveGameOperations.Failure<SaveGameDeleteResult>(operationId, validation);
            if (request.Build == null) return Fail<SaveGameDeleteResult>(operationId, SaveGameErrorCode.InvalidEnvelope, "build", "Tombstone build descriptor is null.");
            SaveGameGeneration next; if (!request.ExpectedHead.TryNext(out next)) return Fail<SaveGameDeleteResult>(operationId, SaveGameErrorCode.GenerationExhausted, "expectedHead", "Generation cannot be incremented.");
            SaveGameEnvelope tombstone;
            SaveGameError creationError;
            if (!SaveGameEnvelope.TryCreateTombstone(request.Profile, request.Slot, next, request.DeletedAtUtc, request.Build, out tombstone, out creationError))
                return SaveGameOperations.Failure<SaveGameDeleteResult>(operationId, creationError);
            SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(tombstone);
            if (!encoded.IsSuccess) return SaveGameOperations.Failure<SaveGameDeleteResult>(operationId, encoded.Error);
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameDeleteResult>(operationId);
            lock (_gate)
            {
                var observed = GetSlotState(request.Profile, request.Slot, false);
                var observedGeneration = observed == null ? SaveGameGeneration.None : observed.Head.Generation;
                if (observedGeneration != request.ExpectedHead) return Conflict<SaveGameDeleteResult>(operationId);
            }
            var fault = TryInject(InMemorySaveGameFaultPoint.BeforeDeletePublication, request.Profile, request.Slot, next);
            if (!fault.IsNone) return SaveGameOperations.Failure<SaveGameDeleteResult>(operationId, fault);
            if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameDeleteResult>(operationId);
            lock (_gate)
            {
                var state = GetSlotState(request.Profile, request.Slot, false); var actual = state == null ? SaveGameGeneration.None : state.Head.Generation;
                if (actual != request.ExpectedHead) return Conflict<SaveGameDeleteResult>(operationId);
                if (cancellationToken.IsCancellationRequested) return SaveGameOperations.Cancelled<SaveGameDeleteResult>(operationId);
                if (state == null) state = GetSlotState(request.Profile, request.Slot, true);
                if (state == null) return Fail<SaveGameDeleteResult>(operationId, SaveGameErrorCode.LimitExceeded, "slots", "Profile already contains the maximum number of slots.");
                Publish(state, new Entry(next, SaveGameRecordKind.Tombstone, request.DeletedAtUtc, encoded.Encoded));
                return SaveGameOperations.Success(operationId, new SaveGameDeleteResult(next));
            }
            }
        }

        private SaveGameOperationId NextOperationId() => new SaveGameOperationId("operation-" + Interlocked.Increment(ref _operationSequence).ToString(System.Globalization.CultureInfo.InvariantCulture));
        private OperationLease EnterOperation()
        {
            if (Interlocked.Increment(ref _activeOperations) <= SaveGameLimits.MaximumConcurrentOperations) return new OperationLease(this);
            Interlocked.Decrement(ref _activeOperations); return default;
        }
        private SaveGameError TryInject(InMemorySaveGameFaultPoint point, SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation)
        {
            if (_faultInjector == null) return SaveGameError.None;
            try { return _faultInjector.Inject(point, profile, slot, generation); }
            catch (Exception exception) { return new SaveGameError(SaveGameErrorCode.FaultInjected, SaveGameOperationPhase.Publication, "faultInjector", "Fault injector threw.", exception); }
        }
        private static void Publish(SlotState state, Entry entry) { state.Entries.Add(entry); if (state.Entries.Count > 2) state.Entries.RemoveAt(0); }
        private SlotState GetSlotState(SaveGameProfileId profile, SaveGameSlotId slot, bool create)
        {
            Dictionary<string, SlotState> slots; if (!_profiles.TryGetValue(profile.Value, out slots)) { if (!create) return null; slots = new Dictionary<string, SlotState>(StringComparer.Ordinal); _profiles.Add(profile.Value, slots); }
            SlotState state; if (!slots.TryGetValue(slot.Value, out state) && create) { if (slots.Count >= SaveGameLimits.MaximumListedSlots) return null; state = new SlotState(); slots.Add(slot.Value, state); } return state;
        }
        private static SaveGameError ValidateRequest(SaveGameProfileId profile, SaveGameSlotId slot)
        {
            if (!profile.IsValid) return new SaveGameError(SaveGameErrorCode.InvalidProfile, SaveGameOperationPhase.Validation, "profile", "Profile id is invalid.");
            if (!slot.IsValid) return new SaveGameError(SaveGameErrorCode.InvalidSlot, SaveGameOperationPhase.Validation, "slot", "Slot id is invalid."); return SaveGameError.None;
        }
        private static ISaveGameOperation<TResult> Conflict<TResult>(SaveGameOperationId id) => Fail<TResult>(id, SaveGameErrorCode.GenerationConflict, "expectedHead", "Expected generation does not match the authoritative head.");
        private static ISaveGameOperation<TResult> Fail<TResult>(SaveGameOperationId id, SaveGameErrorCode code, string path, string message) => SaveGameOperations.Failure<TResult>(id, new SaveGameError(code, SaveGameOperationPhase.Validation, path, message));

        private readonly struct OperationLease : IDisposable
        {
            private readonly InMemorySaveGameStore _owner;
            public OperationLease(InMemorySaveGameStore owner) { _owner = owner; }
            public bool Entered => _owner != null;
            public void Dispose() { if (_owner != null) Interlocked.Decrement(ref _owner._activeOperations); }
        }
    }
}
