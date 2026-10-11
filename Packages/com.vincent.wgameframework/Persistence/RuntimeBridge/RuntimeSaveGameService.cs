using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.Persistence.RuntimeBridge
{
    /// <summary>Immutable request for one provider-neutral Runtime save read.</summary>
    /// <remarks>LowFreqAlloc construction; property reads are NoAlloc.</remarks>
    public sealed class RuntimeSaveGameLoadRequest
    {
        public RuntimeSaveGameLoadRequest(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameReadSelection selection,
            SaveGameRecoveryPolicy recoveryPolicy = SaveGameRecoveryPolicy.PrimaryOnly)
        {
            if (!profile.IsValid) throw new ArgumentException("Profile id is invalid.", nameof(profile));
            if (!slot.IsValid) throw new ArgumentException("Slot id is invalid.", nameof(slot));
            if (!selection.IsValid) throw new ArgumentException("Read selection is invalid.", nameof(selection));
            if (!Enum.IsDefined(typeof(SaveGameRecoveryPolicy), recoveryPolicy))
                throw new ArgumentOutOfRangeException(nameof(recoveryPolicy));
            if (selection.Kind == SaveGameReadSelectionKind.ExactGeneration &&
                recoveryPolicy != SaveGameRecoveryPolicy.PrimaryOnly)
                throw new ArgumentException("Exact-generation reads cannot request recovery.", nameof(recoveryPolicy));

            Profile = profile;
            Slot = slot;
            Selection = selection;
            RecoveryPolicy = recoveryPolicy;
        }

        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameReadSelection Selection { get; }
        public SaveGameRecoveryPolicy RecoveryPolicy { get; }
    }

    /// <summary>Async-shaped Runtime bridge operation, independent from provider operation result authority.</summary>
    public interface IRuntimeSaveGameOperation<TResult>
    {
        SaveGameOperationId OperationId { get; }
        SaveGameOperationStatus Status { get; }
        float Progress { get; }
        bool IsDone { get; }
        bool CanCancel { get; }
        RuntimeSaveGameBridgeErrorDetail ErrorDetail { get; }
        bool HasResult { get; }
        TResult Result { get; }
        bool TryGetResult(out TResult result);
        /// <remarks>NoAlloc after initialization on provider-conforming steady-state polls.</remarks>
        void Poll();
        /// <remarks>NoAlloc after initialization.</remarks>
        void RequestCancel();
    }

    /// <summary>Runtime checkpoint, persistence and candidate-restore bridge.</summary>
    public interface IRuntimeSaveGameService
    {
        /// <remarks>AllocByDesign. Captures an immutable checkpoint and reserves one profile/slot writer.</remarks>
        RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> PlanCheckpointSave(
            RuntimeSaveGameCheckpointRequest request);

        /// <remarks>AllocByDesign. Serializes the immutable plan and creates one provider-backed operation.</remarks>
        IRuntimeSaveGameOperation<SaveGameCommitResult> Commit(
            RuntimeSaveGameWritePlan writePlan,
            CancellationToken cancellationToken = default);

        /// <remarks>AllocByDesign. Creates a read operation; no Runtime restorer or live object is invoked.</remarks>
        IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> PrepareLoad(
            RuntimeSaveGameLoadRequest request,
            CancellationToken cancellationToken = default);

        /// <remarks>LowFreqAlloc. Restores only an exclusively leased unpublished candidate; never publishes or ticks it.</remarks>
        RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> RestorePrepared(
            PreparedRuntimeSaveGame prepared,
            IRuntimeSaveGameRestoreTarget candidateTarget);
    }

    /// <summary>Default noEngine Runtime SaveGame bridge.</summary>
    /// <remarks>
    /// LowFreqAlloc. Checkpoint planning, serialization, store I/O and restore are orchestration boundaries and never
    /// execute from Runtime Tick. The service never publishes a candidate and never advances its frame.
    /// </remarks>
    public sealed class RuntimeSaveGameService : IRuntimeSaveGameService
    {
        private const int RuntimeJsonDepth = 16;
        private readonly object _ownerToken = new object();
        private readonly SlotReservationRegistry _reservationRegistry = new SlotReservationRegistry();
        private readonly ISaveGameStore _store;
        private readonly RuntimeCheckpointBarrier _checkpointBarrier;
        private readonly SaveGameBuildDescriptor _build;
        private readonly RuntimeSaveStateRestoreProfile _restoreProfile;
        private readonly IRuntimeSaveGameCompatibilityChecker _compatibilityChecker;

        public RuntimeSaveGameService(
            ISaveGameStore store,
            RuntimeCheckpointBarrier checkpointBarrier,
            SaveGameBuildDescriptor build,
            RuntimeSaveStateRestoreProfile restoreProfile,
            IRuntimeSaveGameCompatibilityChecker compatibilityChecker)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _checkpointBarrier = checkpointBarrier ?? throw new ArgumentNullException(nameof(checkpointBarrier));
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _restoreProfile = restoreProfile ?? throw new ArgumentNullException(nameof(restoreProfile));
            _compatibilityChecker = compatibilityChecker ?? throw new ArgumentNullException(nameof(compatibilityChecker));
            if (!checkpointBarrier.Coordinator.IsAuthoritative ||
                !ReferenceEquals(checkpointBarrier.RestoreProfile, restoreProfile))
            {
                throw new ArgumentException(
                    "Checkpoint barrier must use an authoritative coordinator bound to the injected restore profile.",
                    nameof(checkpointBarrier));
            }
            RuntimeSaveStateRootHeaderDescriptor descriptor = restoreProfile.Descriptor;
            if (!string.Equals(build.FrameworkVersion, descriptor.FrameworkVersion, StringComparison.Ordinal) ||
                !string.Equals(build.ConfigVersion, descriptor.ConfigVersion, StringComparison.Ordinal) ||
                !string.Equals(build.ResourceCatalogVersion, descriptor.ResourceCatalogVersion, StringComparison.Ordinal))
            {
                throw new ArgumentException("Build identity must match the Runtime restore profile descriptor.", nameof(build));
            }
        }

        /// <summary>Validates the exact live Runtime composition retained by this service.</summary>
        /// <remarks>NoAlloc. Internal bridge composition validation only.</remarks>
        internal bool IsBoundTo(
            RuntimeHost runtimeHost,
            RuntimeSaveStateCoordinator coordinator,
            RuntimeSaveStateRestoreProfile restoreProfile) =>
            ReferenceEquals(_checkpointBarrier.RuntimeHost, runtimeHost) &&
            ReferenceEquals(_checkpointBarrier.Coordinator, coordinator) &&
            ReferenceEquals(_checkpointBarrier.RestoreProfile, restoreProfile) &&
            ReferenceEquals(_restoreProfile, restoreProfile);

        /// <remarks>AllocByDesign.</remarks>
        public RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> PlanCheckpointSave(
            RuntimeSaveGameCheckpointRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.ExpectedHead.TryNext(out SaveGameGeneration next))
            {
                return Fail<RuntimeSaveGameWritePlan>(RuntimeSaveGameBridgeErrorCode.PersistenceFailure,
                    RuntimeSaveGameBridgePhase.Checkpoint,
                    new SaveGameError(SaveGameErrorCode.GenerationExhausted, SaveGameOperationPhase.Validation,
                        "generation", "The slot generation is exhausted."));
            }

            string key = request.Profile.Value + "\n" + request.Slot.Value;
            if (!_reservationRegistry.TryReserve(key, out SlotReservation reservation))
            {
                return Fail<RuntimeSaveGameWritePlan>(RuntimeSaveGameBridgeErrorCode.PersistenceFailure,
                    RuntimeSaveGameBridgePhase.Checkpoint,
                    new SaveGameError(SaveGameErrorCode.Busy, SaveGameOperationPhase.Validation,
                        "slot", "A writer is already reserved for this profile and slot."));
            }

            try
            {
                RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture = _checkpointBarrier.CaptureCheckpoint();
                if (capture == null || !capture.Success || capture.Value == null || capture.Errors == null || capture.Errors.Count != 0)
                {
                    reservation.Release();
                    return CaptureFailure(capture);
                }

                RuntimeSaveState state = capture.Value;
                RuntimeSaveStateRootHeaderDescriptor descriptor = _restoreProfile.Descriptor;
                if (state.SchemaVersion != descriptor.SchemaVersion ||
                    !string.Equals(state.FrameworkVersion, _build.FrameworkVersion, StringComparison.Ordinal) ||
                    !string.Equals(state.ConfigVersion, _build.ConfigVersion, StringComparison.Ordinal) ||
                    !string.Equals(state.ResourceCatalogVersion, _build.ResourceCatalogVersion, StringComparison.Ordinal) ||
                    next.Value != request.ExpectedHead.Value + 1UL)
                {
                    reservation.Release();
                    return Fail<RuntimeSaveGameWritePlan>(RuntimeSaveGameBridgeErrorCode.InvalidProvenance,
                        RuntimeSaveGameBridgePhase.Checkpoint,
                        SaveGameError.None,
                        new RuntimeSaveStateError(RuntimeSaveStateErrorCode.RootHeaderMismatch, "$",
                            "Captured Runtime root facts do not match the service build/restore authority.",
                            state.SchemaVersion, descriptor.SchemaVersion));
                }

                return RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan>.Succeeded(
                    new RuntimeSaveGameWritePlan(_ownerToken, reservation, state, request));
            }
            catch (Exception exception)
            {
                reservation.Release();
                return Fail<RuntimeSaveGameWritePlan>(RuntimeSaveGameBridgeErrorCode.RuntimeFailure,
                    RuntimeSaveGameBridgePhase.Checkpoint, SaveGameError.None,
                    new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, "$.checkpoint",
                        "Checkpoint capture threw an exception.", -1, _restoreProfile.Descriptor.SchemaVersion, exception));
            }
        }

        /// <remarks>AllocByDesign.</remarks>
        public IRuntimeSaveGameOperation<SaveGameCommitResult> Commit(
            RuntimeSaveGameWritePlan writePlan,
            CancellationToken cancellationToken = default)
        {
            if (writePlan == null) throw new ArgumentNullException(nameof(writePlan));
            if (!writePlan.IsOwnedBy(_ownerToken))
                return BridgeOperation<SaveGameCommitResult>.Failed(OwnershipFailure(RuntimeSaveGameBridgePhase.Serialization));
            if (!writePlan.TryTransfer(_ownerToken))
                return BridgeOperation<SaveGameCommitResult>.Failed(ReleasedFailure(RuntimeSaveGameBridgePhase.Serialization));
            if (cancellationToken.IsCancellationRequested)
            {
                writePlan.TryReleaseTransferred(_ownerToken);
                return BridgeOperation<SaveGameCommitResult>.Cancelled();
            }

            try
            {
                RuntimeSaveStateResult<byte[]> serialized = RuntimeSaveStateJson.SaveToUtf8(
                    writePlan.SaveState, SaveGameLimits.MaximumPayloadBytes, RuntimeJsonDepth);
                if (!serialized.Success || serialized.Value == null)
                {
                    writePlan.TryReleaseTransferred(_ownerToken);
                    return BridgeOperation<SaveGameCommitResult>.Failed(RuntimeFailure(
                        RuntimeSaveGameBridgeErrorCode.InvalidRuntimePayload,
                        RuntimeSaveGameBridgePhase.Serialization,
                        serialized.Error));
                }

                RuntimeSaveGameCheckpointRequest request = writePlan.Request;
                if (!request.ExpectedHead.TryNext(out SaveGameGeneration generation))
                {
                    writePlan.TryReleaseTransferred(_ownerToken);
                    return BridgeOperation<SaveGameCommitResult>.Failed(PersistenceFailure(
                        RuntimeSaveGameBridgePhase.Serialization,
                        new SaveGameError(SaveGameErrorCode.GenerationExhausted, SaveGameOperationPhase.Validation,
                            "generation", "The slot generation is exhausted.")));
                }

                SaveGameKeyValue[] tags = CopyEntries(request, true);
                SaveGameKeyValue[] metadata = CopyEntries(request, false);
                if (!SaveGameEnvelope.TryCreateSave(request.Profile, request.Slot, generation,
                    request.CreatedAtUtc, request.UpdatedAtUtc, _build, tags,
                    SaveGameEnvelope.RuntimeSaveStateJsonCodecId, writePlan.SaveState.SchemaVersion,
                    writePlan.SaveState.Frame, metadata, serialized.Value, out SaveGameEnvelope envelope,
                    out SaveGameError envelopeError))
                {
                    writePlan.TryReleaseTransferred(_ownerToken);
                    return BridgeOperation<SaveGameCommitResult>.Failed(PersistenceFailure(
                        RuntimeSaveGameBridgePhase.Serialization, envelopeError));
                }

                SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(envelope);
                if (!encoded.IsSuccess || encoded.Encoded == null)
                {
                    writePlan.TryReleaseTransferred(_ownerToken);
                    return BridgeOperation<SaveGameCommitResult>.Failed(PersistenceFailure(
                        RuntimeSaveGameBridgePhase.Serialization, encoded.Error));
                }

                ISaveGameOperation<SaveGameCommitResult> provider = _store.Commit(
                    new SaveGameCommitRequest(request.Profile, request.Slot, request.ExpectedHead, encoded.Encoded),
                    cancellationToken);
                return new CommitBridgeOperation(provider, writePlan, _ownerToken);
            }
            catch (Exception exception)
            {
                writePlan.TryReleaseTransferred(_ownerToken);
                return BridgeOperation<SaveGameCommitResult>.Failed(PersistenceFailure(
                    RuntimeSaveGameBridgePhase.StoreCommit,
                    new SaveGameError(SaveGameErrorCode.StoreUnavailable, SaveGameOperationPhase.Publication,
                        "store", "Store commit creation failed.", exception)));
            }
        }

        /// <remarks>AllocByDesign.</remarks>
        public IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> PrepareLoad(
            RuntimeSaveGameLoadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (cancellationToken.IsCancellationRequested)
                return BridgeOperation<PreparedRuntimeSaveGame>.Cancelled();
            try
            {
                ISaveGameOperation<SaveGameReadResult> provider = _store.Read(
                    new SaveGameReadRequest(request.Profile, request.Slot, request.Selection, request.RecoveryPolicy),
                    cancellationToken);
                return new PrepareLoadBridgeOperation(provider, request, _ownerToken, _restoreProfile,
                    _compatibilityChecker);
            }
            catch (Exception exception)
            {
                return BridgeOperation<PreparedRuntimeSaveGame>.Failed(PersistenceFailure(
                    RuntimeSaveGameBridgePhase.StoreRead,
                    new SaveGameError(SaveGameErrorCode.StoreUnavailable, SaveGameOperationPhase.Read,
                        "store", "Store read creation failed.", exception)));
            }
        }

        /// <remarks>LowFreqAlloc on failures and participant restore paths; success does not publish or Tick.</remarks>
        public RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> RestorePrepared(
            PreparedRuntimeSaveGame prepared,
            IRuntimeSaveGameRestoreTarget candidateTarget)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            if (candidateTarget == null) throw new ArgumentNullException(nameof(candidateTarget));
            if (!prepared.IsOwnedBy(_ownerToken)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                OwnershipFailure(RuntimeSaveGameBridgePhase.CandidateAcquisition));
            if (prepared.State != PreparedRuntimeSaveGameState.Ready) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                ReleasedFailure(RuntimeSaveGameBridgePhase.CandidateAcquisition));

            IRuntimeSaveGameCandidateRestoreLease lease = null;
            string targetId = null;
            bool claimedPrepared = false;
            try
            {
                bool acquired = candidateTarget.TryAcquireRestoreLease(prepared.RestorePlan.Profile, out lease,
                    out RuntimeSaveGameTargetValidationError targetError);
                if (lease != null) targetId = lease.TargetId;
                if (!acquired || lease == null || !targetError.IsNone)
                {
                    if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateAcquisition, targetId));
                    return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(TargetFailure(
                        RuntimeSaveGameBridgePhase.CandidateAcquisition,
                        NormalizeTargetError(targetError, RuntimeSaveGameTargetValidationErrorCode.Unavailable,
                            "candidate.acquire"), targetId));
                }

                RuntimeSaveStateCoordinator coordinator = lease.Coordinator;
                RuntimeSaveStateRestoreProfile profile = lease.RestoreProfile;
                if (string.IsNullOrEmpty(targetId) || coordinator == null || !coordinator.IsAuthoritative ||
                    profile == null || !ReferenceEquals(profile, prepared.RestorePlan.Profile) ||
                    !ReferenceEquals(profile, coordinator.RestoreProfile))
                {
                    if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateAcquisition, targetId));
                    return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(TargetFailure(
                        RuntimeSaveGameBridgePhase.CandidateAcquisition,
                        new RuntimeSaveGameTargetValidationError(
                            RuntimeSaveGameTargetValidationErrorCode.InvalidComposition, "candidate.composition"), targetId));
                }

                if (!prepared.TryClaimRestore(_ownerToken))
                {
                    if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateAcquisition, targetId));
                    return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        ReleasedFailure(RuntimeSaveGameBridgePhase.CandidateAcquisition, targetId));
                }
                claimedPrepared = true;

                RuntimeSaveStateCoordinatorResult<bool> restored = coordinator.RestorePrepared(
                    prepared.RestorePlan);
                if (restored == null || !restored.Success || !restored.Value ||
                    restored.Errors == null || restored.Errors.Count != 0)
                {
                    prepared.TryReleaseClaimed(_ownerToken);
                    claimedPrepared = false;
                    if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateRestore, targetId));
                    return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(CoordinatorFailure(
                        RuntimeSaveGameBridgePhase.CandidateRestore, restored, targetId));
                }

                var completion = new RuntimeSaveGameRestoreCompletion(
                    prepared.Profile,
                    prepared.Slot,
                    prepared.OriginalGeneration,
                    prepared.SelectedGeneration,
                    prepared.RecoverySource,
                    prepared.CreatedAtUtc,
                    prepared.CompletedFrame,
                    prepared.NextFrame);
                bool completed = lease.TryCompleteRestore(
                    in completion,
                    out RuntimeSaveGameTargetValidationError completionError);
                if (!completed || !completionError.IsNone)
                {
                    prepared.TryReleaseClaimed(_ownerToken);
                    claimedPrepared = false;
                    if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateCompletion, targetId));
                    return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(TargetFailure(
                        RuntimeSaveGameBridgePhase.CandidateCompletion,
                        NormalizeTargetError(completionError,
                            RuntimeSaveGameTargetValidationErrorCode.CompletionRejected,
                            "candidate.complete"), targetId));
                }

                if (!prepared.TryMarkConsumed(_ownerToken))
                {
                    prepared.TryReleaseClaimed(_ownerToken);
                    claimedPrepared = false;
                    if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                        DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateCompletion, targetId));
                    return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(OwnershipFailure(
                        RuntimeSaveGameBridgePhase.CandidateCompletion, targetId));
                }
                claimedPrepared = false;

                if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                    DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateCompletion, targetId));
                return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Succeeded(
                    new RuntimeSaveGameRestoreResult(targetId, prepared.CompletedFrame, prepared.NextFrame));
            }
            catch (Exception exception)
            {
                if (claimedPrepared) prepared.TryReleaseClaimed(_ownerToken);
                if (!TryDispose(lease)) return RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult>.Failed(
                    DisposeContractFailure(RuntimeSaveGameBridgePhase.CandidateRestore, targetId));
                return Fail<RuntimeSaveGameRestoreResult>(RuntimeSaveGameBridgeErrorCode.RestoreTargetRejected,
                    RuntimeSaveGameBridgePhase.CandidateRestore, SaveGameError.None,
                    new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, "$.candidate",
                        "Candidate restore threw an exception.", prepared.RestorePlan.State.SchemaVersion,
                        prepared.RestorePlan.Profile.Descriptor.SchemaVersion, exception), targetId);
            }
        }

        private static RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> CaptureFailure(
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture)
        {
            RuntimeSaveStateParticipantError[] errors = CopyErrors(capture != null ? capture.Errors : null);
            RuntimeSaveStateError first = errors.Length != 0 ? errors[0].Error : new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument, "$.checkpoint", "Checkpoint returned a contradictory result.");
            return RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan>.Failed(new RuntimeSaveGameBridgeErrorDetail(
                errors.Length != 0 ? RuntimeSaveGameBridgeErrorCode.ParticipantFailure : RuntimeSaveGameBridgeErrorCode.CheckpointRejected,
                RuntimeSaveGameBridgePhase.Checkpoint,
                ContractFailure(SaveGameErrorCode.RuntimeCheckpointRejected, SaveGameOperationPhase.Validation,
                    "checkpoint", "Runtime checkpoint capture was rejected."), first, errors,
                RuntimeSaveGameTargetValidationError.None));
        }

        private static RuntimeSaveStateParticipantError[] CopyErrors(
            System.Collections.Generic.IReadOnlyList<RuntimeSaveStateParticipantError> errors)
        {
            if (errors == null || errors.Count == 0) return Array.Empty<RuntimeSaveStateParticipantError>();
            var copy = new RuntimeSaveStateParticipantError[errors.Count];
            for (int i = 0; i < errors.Count; i++) copy[i] = errors[i];
            return copy;
        }

        private static SaveGameKeyValue[] CopyEntries(RuntimeSaveGameCheckpointRequest request, bool tags)
        {
            int count = tags ? request.CompatibilityTagCount : request.DisplayMetadataCount;
            var copy = new SaveGameKeyValue[count];
            for (int i = 0; i < count; i++) copy[i] = tags
                ? request.GetCompatibilityTag(i)
                : request.GetDisplayMetadata(i);
            return copy;
        }

        private sealed class SlotReservation : IRuntimeSaveGameWriteReservation
        {
            private SlotReservationRegistry _registry;
            private readonly string _key;
            public SlotReservation(SlotReservationRegistry registry, string key) { _registry = registry; _key = key; }
            public void Release()
            {
                SlotReservationRegistry registry = Interlocked.Exchange(ref _registry, null);
                if (registry != null) registry.Release(_key);
            }
        }

        private sealed class SlotReservationRegistry
        {
            private readonly object _lock = new object();
            private readonly HashSet<string> _slots = new HashSet<string>(StringComparer.Ordinal);
            public bool TryReserve(string key, out SlotReservation reservation)
            {
                lock (_lock)
                {
                    if (!_slots.Add(key)) { reservation = null; return false; }
                    reservation = new SlotReservation(this, key);
                    return true;
                }
            }
            public void Release(string key) { lock (_lock) _slots.Remove(key); }
        }

        internal static RuntimeSaveGameBridgeErrorDetail PersistenceFailure(
            RuntimeSaveGameBridgePhase phase, SaveGameError error) =>
            new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.PersistenceFailure, phase,
                error, null, null, RuntimeSaveGameTargetValidationError.None);

        internal static RuntimeSaveGameBridgeErrorDetail RuntimeFailure(
            RuntimeSaveGameBridgeErrorCode code, RuntimeSaveGameBridgePhase phase, RuntimeSaveStateError error) =>
            new RuntimeSaveGameBridgeErrorDetail(code, phase, RuntimeContractFailure(code, phase, error),
                error ?? new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, "$", "Runtime returned no error."),
                null, RuntimeSaveGameTargetValidationError.None);

        private static RuntimeSaveGameBridgeErrorDetail OwnershipFailure(RuntimeSaveGameBridgePhase phase,
            string targetId = null) =>
            new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.LeaseOwnershipMismatch, phase,
                RuntimeContractFailure(RuntimeSaveGameBridgeErrorCode.LeaseOwnershipMismatch, phase),
                null, null, RuntimeSaveGameTargetValidationError.None, targetId);

        private static RuntimeSaveGameBridgeErrorDetail ReleasedFailure(RuntimeSaveGameBridgePhase phase,
            string targetId = null) =>
            new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.LeaseReleased, phase,
                RuntimeContractFailure(RuntimeSaveGameBridgeErrorCode.LeaseReleased, phase),
                null, null, RuntimeSaveGameTargetValidationError.None, targetId);

        private static RuntimeSaveGameBridgeErrorDetail TargetFailure(
            RuntimeSaveGameBridgePhase phase, RuntimeSaveGameTargetValidationError error, string targetId = null) =>
            new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.RestoreTargetRejected, phase,
                ContractFailure(SaveGameErrorCode.CandidateRestoreFailed, SaveGameOperationPhase.Validation,
                    "candidate", "Candidate restore target rejected the operation."), null, null, error, targetId);

        private static RuntimeSaveGameTargetValidationError NormalizeTargetError(
            RuntimeSaveGameTargetValidationError error,
            RuntimeSaveGameTargetValidationErrorCode fallback,
            string path)
        {
            return error.IsNone || !Enum.IsDefined(typeof(RuntimeSaveGameTargetValidationErrorCode), error.Code)
                ? new RuntimeSaveGameTargetValidationError(fallback, path)
                : error;
        }

        private static bool TryDispose(IRuntimeSaveGameCandidateRestoreLease lease)
        {
            if (lease == null) return true;
            try { lease.Dispose(); return true; }
            catch { return false; }
        }

        private static RuntimeSaveGameBridgeErrorDetail DisposeContractFailure(RuntimeSaveGameBridgePhase phase,
            string targetId = null) =>
            TargetFailure(phase, new RuntimeSaveGameTargetValidationError(
                RuntimeSaveGameTargetValidationErrorCode.ContractViolation, "candidate.dispose"), targetId);

        private static RuntimeSaveGameBridgeErrorDetail CoordinatorFailure(
            RuntimeSaveGameBridgePhase phase,
            RuntimeSaveStateCoordinatorResult<bool> result,
            string targetId)
        {
            RuntimeSaveStateParticipantError[] errors = CopyErrors(result != null ? result.Errors : null);
            RuntimeSaveStateError first = errors.Length != 0 ? errors[0].Error : new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument, "$.restore", "Runtime restore returned a contradictory result.");
            return new RuntimeSaveGameBridgeErrorDetail(
                errors.Length != 0 ? RuntimeSaveGameBridgeErrorCode.ParticipantFailure : RuntimeSaveGameBridgeErrorCode.RuntimeFailure,
                phase, ContractFailure(SaveGameErrorCode.CandidateRestoreFailed, SaveGameOperationPhase.Validation,
                    "candidate.restore", "Candidate Runtime restore was rejected."), first, errors,
                RuntimeSaveGameTargetValidationError.None, targetId);
        }

        internal static SaveGameError ContractFailure(SaveGameErrorCode code, SaveGameOperationPhase phase,
            string path, string message) => new SaveGameError(code, phase, path, message);

        private static SaveGameError RuntimeContractFailure(
            RuntimeSaveGameBridgeErrorCode code, RuntimeSaveGameBridgePhase phase,
            RuntimeSaveStateError runtimeError = null)
        {
            if (code == RuntimeSaveGameBridgeErrorCode.InvalidRuntimePayload)
            {
                if (runtimeError != null &&
                    (runtimeError.Code == RuntimeSaveStateErrorCode.UnsupportedVersion ||
                     runtimeError.Code == RuntimeSaveStateErrorCode.MissingMigration))
                    return ContractFailure(SaveGameErrorCode.RuntimeCompatibilityFailed,
                        SaveGameOperationPhase.Validation, "runtime.schema", "Runtime payload schema is incompatible.");
                return ContractFailure(SaveGameErrorCode.RuntimeSerializationFailed,
                    SaveGameOperationPhase.Serialization, "runtime", "Runtime payload serialization or parsing failed.");
            }
            if (code == RuntimeSaveGameBridgeErrorCode.FrameOverflow)
                return ContractFailure(SaveGameErrorCode.CandidateRestoreFailed,
                    SaveGameOperationPhase.Validation, "runtime.frame", "Restored frame cannot advance.");
            if (phase == RuntimeSaveGameBridgePhase.Checkpoint)
                return ContractFailure(SaveGameErrorCode.RuntimeCheckpointRejected,
                    SaveGameOperationPhase.Validation, "checkpoint", "Runtime checkpoint preparation failed.");
            if (phase == RuntimeSaveGameBridgePhase.Serialization)
                return ContractFailure(SaveGameErrorCode.RuntimeSerializationFailed,
                    SaveGameOperationPhase.Serialization, "runtime", "Runtime payload serialization failed.");
            if (phase == RuntimeSaveGameBridgePhase.CandidateAcquisition ||
                phase == RuntimeSaveGameBridgePhase.CandidateRestore ||
                phase == RuntimeSaveGameBridgePhase.CandidateCompletion)
                return ContractFailure(SaveGameErrorCode.CandidateRestoreFailed,
                    SaveGameOperationPhase.Validation, "candidate", "Candidate restore failed.");
            return ContractFailure(SaveGameErrorCode.RuntimeCompatibilityFailed,
                SaveGameOperationPhase.Validation, code == RuntimeSaveGameBridgeErrorCode.CompatibilityFailure
                    ? "compatibility" : "runtime", "Persisted Runtime state is incompatible or invalid.");
        }

        private static RuntimeSaveGameBridgeResult<T> Fail<T>(RuntimeSaveGameBridgeErrorCode code,
            RuntimeSaveGameBridgePhase phase, SaveGameError persistenceError,
            RuntimeSaveStateError runtimeError = null, string targetId = null) => RuntimeSaveGameBridgeResult<T>.Failed(
                new RuntimeSaveGameBridgeErrorDetail(code, phase,
                    persistenceError.IsNone ? RuntimeContractFailure(code, phase, runtimeError) : persistenceError,
                    runtimeError, null,
                    RuntimeSaveGameTargetValidationError.None, targetId));
    }
}
