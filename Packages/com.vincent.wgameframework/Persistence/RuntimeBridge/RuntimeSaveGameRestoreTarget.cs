using System;
using MxFramework.Runtime;

namespace MxFramework.Persistence.RuntimeBridge
{
    /// <summary>Validated persistence and frame provenance committed with one candidate restore.</summary>
    /// <remarks>Construction and all property reads are NoAlloc.</remarks>
    public readonly struct RuntimeSaveGameRestoreCompletion
    {
        public RuntimeSaveGameRestoreCompletion(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration originalGeneration,
            SaveGameGeneration selectedGeneration,
            SaveGameRecoverySource recoverySource,
            DateTime createdAtUtc,
            long completedFrame,
            long nextFrame)
        {
            if (!profile.IsValid) throw new ArgumentException("Profile id is invalid.", nameof(profile));
            if (!slot.IsValid) throw new ArgumentException("Slot id is invalid.", nameof(slot));
            if (!originalGeneration.IsPublished)
                throw new ArgumentException("Original generation must be published.", nameof(originalGeneration));
            if (!selectedGeneration.IsPublished || selectedGeneration.CompareTo(originalGeneration) > 0)
                throw new ArgumentException("Selected generation must be published and not newer than the original head.", nameof(selectedGeneration));
            if (recoverySource != SaveGameRecoverySource.Primary &&
                recoverySource != SaveGameRecoverySource.LastKnownGood)
                throw new ArgumentOutOfRangeException(nameof(recoverySource));
            if (createdAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Created timestamp must be UTC.", nameof(createdAtUtc));
            if (completedFrame < 0 || completedFrame == long.MaxValue || nextFrame != completedFrame + 1L)
                throw new ArgumentException("Next frame must equal a valid completed frame plus one.", nameof(nextFrame));

            Profile = profile;
            Slot = slot;
            OriginalGeneration = originalGeneration;
            SelectedGeneration = selectedGeneration;
            RecoverySource = recoverySource;
            CreatedAtUtc = createdAtUtc;
            CompletedFrame = completedFrame;
            NextFrame = nextFrame;
        }

        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration OriginalGeneration { get; }
        public SaveGameGeneration SelectedGeneration { get; }
        public SaveGameRecoverySource RecoverySource { get; }
        public DateTime CreatedAtUtc { get; }
        public long CompletedFrame { get; }
        public long NextFrame { get; }
    }

    /// <summary>Stable candidate acquisition and completion rejection categories.</summary>
    public enum RuntimeSaveGameTargetValidationErrorCode
    {
        None = 0,
        Unavailable = 1,
        AlreadyPublished = 2,
        WrongThread = 3,
        InvalidComposition = 4,
        CompletionRejected = 5,
        InvalidFrame = 6,
        ContractViolation = 7
    }

    /// <summary>Allocation-free structured candidate validation failure.</summary>
    public readonly struct RuntimeSaveGameTargetValidationError
    {
        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveGameTargetValidationError(RuntimeSaveGameTargetValidationErrorCode code, string path)
        {
            Code = code;
            Path = path ?? string.Empty;
        }

        public RuntimeSaveGameTargetValidationErrorCode Code { get; }
        public string Path { get; }
        public bool IsNone => Code == RuntimeSaveGameTargetValidationErrorCode.None;
        public static RuntimeSaveGameTargetValidationError None => default;
    }

    /// <summary>Candidate-session boundary that atomically validates and reserves restore publication authority.</summary>
    public interface IRuntimeSaveGameRestoreTarget
    {
        /// <summary>Acquires the exclusive restore lease in the same critical section as target validation.</summary>
        /// <remarks>
        /// NoAlloc after target initialization. Implementations must prevent publication between validation and acquisition.
        /// Success returns a non-null lease and None error. Failure returns a null lease and a non-None error.
        /// Callers must still reject contradictory third-party implementations fail-closed.
        /// </remarks>
        bool TryAcquireRestoreLease(
            RuntimeSaveStateRestoreProfile expectedRestoreProfile,
            out IRuntimeSaveGameCandidateRestoreLease lease,
            out RuntimeSaveGameTargetValidationError validationError);
    }

    /// <summary>Exclusive authority to restore one unpublished candidate and complete its frame handoff.</summary>
    public interface IRuntimeSaveGameCandidateRestoreLease : IDisposable
    {
        /// <remarks>NoAlloc. Must be non-null and non-empty for the lifetime of the lease.</remarks>
        string TargetId { get; }
        /// <remarks>NoAlloc. Must be non-null and authoritative for the lifetime of the lease.</remarks>
        RuntimeSaveStateCoordinator Coordinator { get; }
        /// <remarks>
        /// NoAlloc. Must be non-null and reference-equal to both the acquisition profile and Coordinator.RestoreProfile.
        /// </remarks>
        RuntimeSaveStateRestoreProfile RestoreProfile { get; }

        /// <summary>Commits the exact persistence provenance, completed frame F, and next Runtime frame F+1 once.</summary>
        /// <remarks>
        /// NoAlloc after target initialization. Implementations must reject a second completion. Disposing before a
        /// successful completion permanently poisons the candidate so it cannot later be published. Success returns
        /// true and None error. Failure returns false and a non-None error. Callers must reject contradictory results.
        /// </remarks>
        bool TryCompleteRestore(
            in RuntimeSaveGameRestoreCompletion completion,
            out RuntimeSaveGameTargetValidationError validationError);

        /// <summary>Releases the lease without throwing.</summary>
        /// <remarks>
        /// NoAlloc and no-throw. If completion has not succeeded, the implementation must atomically and permanently
        /// poison the candidate before any fallible cleanup so it can never be published or reacquired.
        /// </remarks>
        new void Dispose();
    }
}
