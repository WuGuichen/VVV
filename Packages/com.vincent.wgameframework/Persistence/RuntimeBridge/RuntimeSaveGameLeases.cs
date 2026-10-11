using System;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.Persistence.RuntimeBridge
{
    /// <summary>Independent registry reservation retained by one write plan.</summary>
    /// <remarks>
    /// NoAlloc after initialization. Implementations must not retain a checkpoint barrier, RuntimeHost, coordinator,
    /// or other live Runtime object. Release must not throw; the write plan invokes it exactly once.
    /// </remarks>
    internal interface IRuntimeSaveGameWriteReservation
    {
        void Release();
    }

    public enum RuntimeSaveGameWritePlanState { Ready = 0, Transferred = 1, Released = 2 }

    /// <summary>Caller-owned immutable checkpoint lease accepted by a later store operation.</summary>
    /// <remarks>
    /// LowFreqAlloc construction. It retains only an immutable Runtime snapshot and request facts, never live Runtime
    /// objects. All property reads and ownership transitions are NoAlloc after construction.
    /// </remarks>
    public sealed class RuntimeSaveGameWritePlan : IDisposable
    {
        private readonly object _ownerToken;
        private readonly IRuntimeSaveGameWriteReservation _reservation;
        private int _state;

        internal RuntimeSaveGameWritePlan(
            object ownerToken,
            IRuntimeSaveGameWriteReservation reservation,
            RuntimeSaveState saveState,
            RuntimeSaveGameCheckpointRequest request)
        {
            _ownerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
            _reservation = reservation ?? throw new ArgumentNullException(nameof(reservation));
            SaveState = saveState ?? throw new ArgumentNullException(nameof(saveState));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public RuntimeSaveState SaveState { get; }
        public RuntimeSaveGameCheckpointRequest Request { get; }
        public RuntimeSaveGameWritePlanState State => (RuntimeSaveGameWritePlanState)Volatile.Read(ref _state);

        /// <summary>Atomically transfers ownership from the caller to one commit operation.</summary>
        /// <remarks>NoAlloc. Exactly one caller can transition Ready to Transferred.</remarks>
        internal bool TryTransfer(object ownerToken) => IsOwnedBy(ownerToken) && Interlocked.CompareExchange(ref _state, 1, 0) == 0;

        /// <remarks>NoAlloc.</remarks>
        internal bool IsOwnedBy(object ownerToken) => ReferenceEquals(_ownerToken, ownerToken);

        /// <remarks>NoAlloc. Only the accepting service owner may release a transferred plan.</remarks>
        internal bool TryReleaseTransferred(object ownerToken)
        {
            if (!IsOwnedBy(ownerToken) || Interlocked.CompareExchange(ref _state, 2, 1) != 1)
            {
                return false;
            }

            _reservation.Release();
            return true;
        }

        /// <remarks>NoAlloc and idempotent. Caller disposal releases only an untransferred Ready plan.</remarks>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
            {
                _reservation.Release();
            }
        }
    }

    public enum PreparedRuntimeSaveGameState { Ready = 0, RestoreClaimed = 1, Consumed = 2, Released = 3 }

    /// <summary>Caller-owned immutable prepare-load result bound to one Runtime restore plan.</summary>
    /// <remarks>
    /// LowFreqAlloc construction. Claim and terminal transitions are atomic; restore can be claimed only once.
    /// All property reads and ownership transitions are NoAlloc after construction.
    /// </remarks>
    public sealed class PreparedRuntimeSaveGame : IDisposable
    {
        private readonly object _ownerToken;
        private int _state;

        internal PreparedRuntimeSaveGame(
            object ownerToken,
            RuntimeSaveStateRestorePlan restorePlan,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration originalGeneration,
            SaveGameGeneration selectedGeneration,
            SaveGameRecoverySource recoverySource,
            DateTime createdAtUtc,
            long completedFrame,
            long nextFrame)
        {
            _ownerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
            RestorePlan = restorePlan ?? throw new ArgumentNullException(nameof(restorePlan));
            if (!profile.IsValid) throw new ArgumentException("Profile id is invalid.", nameof(profile));
            if (!slot.IsValid) throw new ArgumentException("Slot id is invalid.", nameof(slot));
            if (!originalGeneration.IsPublished) throw new ArgumentException("Original generation must be published.", nameof(originalGeneration));
            if (!selectedGeneration.IsPublished) throw new ArgumentException("Selected generation must be published.", nameof(selectedGeneration));
            if (recoverySource == SaveGameRecoverySource.None || !Enum.IsDefined(typeof(SaveGameRecoverySource), recoverySource))
                throw new ArgumentOutOfRangeException(nameof(recoverySource), "Prepared loads require a known non-None recovery source.");
            if (selectedGeneration.CompareTo(originalGeneration) > 0)
                throw new ArgumentException("Selected generation cannot be newer than the original head generation.", nameof(selectedGeneration));
            if (createdAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Created timestamp must be UTC.", nameof(createdAtUtc));
            if (completedFrame < 0) throw new ArgumentOutOfRangeException(nameof(completedFrame));
            if (restorePlan.State.Frame != completedFrame) throw new ArgumentException("Completed frame must match the prepared Runtime state.", nameof(completedFrame));
            if (completedFrame == long.MaxValue || nextFrame != completedFrame + 1L) throw new ArgumentException("Next frame must equal completed frame plus one.", nameof(nextFrame));

            Profile = profile;
            Slot = slot;
            OriginalGeneration = originalGeneration;
            SelectedGeneration = selectedGeneration;
            RecoverySource = recoverySource;
            CreatedAtUtc = createdAtUtc;
            CompletedFrame = completedFrame;
            NextFrame = nextFrame;
        }

        public RuntimeSaveStateRestorePlan RestorePlan { get; }
        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration OriginalGeneration { get; }
        public SaveGameGeneration SelectedGeneration { get; }
        public SaveGameRecoverySource RecoverySource { get; }
        public DateTime CreatedAtUtc { get; }
        public long CompletedFrame { get; }
        public long NextFrame { get; }
        public PreparedRuntimeSaveGameState State => (PreparedRuntimeSaveGameState)Volatile.Read(ref _state);

        /// <remarks>NoAlloc. Exactly one caller can claim this prepared restore.</remarks>
        internal bool TryClaimRestore(object ownerToken) =>
            IsOwnedBy(ownerToken) && Interlocked.CompareExchange(ref _state, 1, 0) == 0;

        /// <remarks>NoAlloc.</remarks>
        internal bool IsOwnedBy(object ownerToken) => ReferenceEquals(_ownerToken, ownerToken);

        /// <summary>Marks a successfully restored prepared value as consumed.</summary>
        /// <remarks>NoAlloc. Only the current restore owner can transition RestoreClaimed to Consumed.</remarks>
        internal bool TryMarkConsumed(object ownerToken) =>
            IsOwnedBy(ownerToken) && Interlocked.CompareExchange(ref _state, 2, 1) == 1;

        /// <remarks>NoAlloc. Every failed restore attempt transitions RestoreClaimed to Released and is never reusable.</remarks>
        internal bool TryReleaseClaimed(object ownerToken) =>
            IsOwnedBy(ownerToken) && Interlocked.CompareExchange(ref _state, 3, 1) == 1;

        /// <remarks>NoAlloc and idempotent. Caller disposal releases only an unclaimed Ready plan.</remarks>
        public void Dispose() => Interlocked.CompareExchange(ref _state, 3, 0);
    }
}
