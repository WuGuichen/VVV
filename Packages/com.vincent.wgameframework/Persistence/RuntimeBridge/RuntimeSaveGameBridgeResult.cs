using System;
using MxFramework.Runtime;

namespace MxFramework.Persistence.RuntimeBridge
{
    /// <summary>Stable bridge-level failure categories. The default value is a fail-closed uninitialized result.</summary>
    public enum RuntimeSaveGameBridgeErrorCode
    {
        None = 0,
        Uninitialized = 1,
        PersistenceFailure = 2,
        RuntimeFailure = 3,
        ParticipantFailure = 4,
        CompatibilityFailure = 5,
        LeaseReleased = 6,
        LeaseOwnershipMismatch = 7,
        RestoreTargetRejected = 8,
        CheckpointRejected = 9,
        InvalidProviderOperation = 10,
        InvalidProvenance = 11,
        InvalidRuntimePayload = 12,
        FrameOverflow = 13
    }

    public enum RuntimeSaveGameBridgePhase
    {
        None = 0,
        Checkpoint = 1,
        Serialization = 2,
        StoreCommit = 3,
        StoreRead = 4,
        EnvelopeValidation = 5,
        Compatibility = 6,
        RuntimePreparation = 7,
        CandidateAcquisition = 8,
        CandidateRestore = 9,
        CandidateCompletion = 10
    }

    /// <summary>Structured bridge failure detail preserving Core and Runtime authority errors verbatim.</summary>
    /// <remarks>AllocByDesign. Participant error references are defensively copied without interpretation or reordering.</remarks>
    public sealed class RuntimeSaveGameBridgeErrorDetail
    {
        private readonly RuntimeSaveStateParticipantError[] _participantErrors;

        public RuntimeSaveGameBridgeErrorDetail(
            RuntimeSaveGameBridgeErrorCode code,
            SaveGameError persistenceError,
            RuntimeSaveStateError runtimeError,
            RuntimeSaveStateParticipantError[] participantErrors)
            : this(code, RuntimeSaveGameBridgePhase.None, persistenceError, runtimeError, participantErrors,
                RuntimeSaveGameTargetValidationError.None)
        {
        }

        public RuntimeSaveGameBridgeErrorDetail(
            RuntimeSaveGameBridgeErrorCode code,
            RuntimeSaveGameBridgePhase phase,
            SaveGameError persistenceError,
            RuntimeSaveStateError runtimeError,
            RuntimeSaveStateParticipantError[] participantErrors,
            RuntimeSaveGameTargetValidationError targetValidationError,
            string targetId = null)
        {
            if (code == RuntimeSaveGameBridgeErrorCode.None || code == RuntimeSaveGameBridgeErrorCode.Uninitialized)
            {
                throw new ArgumentException("Explicit bridge failures require a stable non-default error code.", nameof(code));
            }

            Code = code;
            Phase = phase;
            PersistenceError = persistenceError;
            RuntimeError = runtimeError;
            TargetValidationError = targetValidationError;
            TargetId = targetId ?? string.Empty;
            _participantErrors = participantErrors == null || participantErrors.Length == 0
                ? Array.Empty<RuntimeSaveStateParticipantError>()
                : (RuntimeSaveStateParticipantError[])participantErrors.Clone();
        }

        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveGameBridgeErrorCode Code { get; }
        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveGameBridgePhase Phase { get; }
        /// <remarks>NoAlloc.</remarks>
        public SaveGameError PersistenceError { get; }
        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveStateError RuntimeError { get; }
        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveGameTargetValidationError TargetValidationError { get; }
        /// <summary>Gets the acquired candidate identity, or an empty string when acquisition never completed.</summary>
        /// <remarks>NoAlloc.</remarks>
        public string TargetId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int ParticipantErrorCount => _participantErrors.Length;

        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveStateParticipantError GetParticipantError(int index) => _participantErrors[index];
    }

    /// <summary>Immutable auditable outcome of restoring one unpublished Runtime candidate.</summary>
    public readonly struct RuntimeSaveGameRestoreResult
    {
        internal RuntimeSaveGameRestoreResult(string targetId, long completedFrame, long nextFrame)
        {
            if (string.IsNullOrEmpty(targetId)) throw new ArgumentException("Target id cannot be empty.", nameof(targetId));
            if (completedFrame < 0 || completedFrame == long.MaxValue || nextFrame != completedFrame + 1L)
                throw new ArgumentOutOfRangeException(nameof(nextFrame));
            TargetId = targetId;
            CompletedFrame = completedFrame;
            NextFrame = nextFrame;
        }

        /// <remarks>NoAlloc.</remarks>
        public string TargetId { get; }
        /// <remarks>NoAlloc.</remarks>
        public long CompletedFrame { get; }
        /// <remarks>NoAlloc.</remarks>
        public long NextFrame { get; }
    }

    /// <summary>Fail-closed result of a Persistence/Runtime bridge operation.</summary>
    /// <remarks>NoAlloc. Factory calls and property reads do not allocate on their normal paths.</remarks>
    public readonly struct RuntimeSaveGameBridgeResult<T>
    {
        private RuntimeSaveGameBridgeResult(bool success, T value, RuntimeSaveGameBridgeErrorDetail errorDetail)
        {
            Success = success;
            Value = value;
            ErrorDetail = errorDetail;
        }

        public bool Success { get; }
        public T Value { get; }
        public RuntimeSaveGameBridgeErrorDetail ErrorDetail { get; }
        public RuntimeSaveGameBridgeErrorCode ErrorCode => Success
            ? RuntimeSaveGameBridgeErrorCode.None
            : ErrorDetail != null ? ErrorDetail.Code : RuntimeSaveGameBridgeErrorCode.Uninitialized;
        public bool HasErrorDetail => ErrorDetail != null;

        /// <remarks>NoAlloc.</remarks>
        public static RuntimeSaveGameBridgeResult<T> Succeeded(T value)
        {
            if (!typeof(T).IsValueType && ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new RuntimeSaveGameBridgeResult<T>(true, value, null);
        }

        /// <remarks>NoAlloc on the normal failure construction path.</remarks>
        public static RuntimeSaveGameBridgeResult<T> Failed(RuntimeSaveGameBridgeErrorDetail detail)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));
            return new RuntimeSaveGameBridgeResult<T>(false, default, detail);
        }
    }
}
