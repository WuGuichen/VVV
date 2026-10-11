using MxFramework.Core.Math;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>One deterministic logical input submitted to the Gameplay Trigger playable.</summary>
    public readonly struct GameplayTriggerPlayableInput
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerPlayableInput(
            int moveX,
            int moveZ,
            bool capture = false,
            bool restore = false,
            bool reset = false)
        {
            MoveX = moveX < -1000 ? -1000 : moveX > 1000 ? 1000 : moveX;
            MoveZ = moveZ < -1000 ? -1000 : moveZ > 1000 ? 1000 : moveZ;
            Capture = capture;
            Restore = restore;
            Reset = reset;
        }

        /// <remarks>NoAlloc.</remarks>
        public int MoveX { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MoveZ { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool Capture { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool Restore { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool Reset { get; }
    }

    /// <summary>Authority plus explicitly presentation-only evidence exposed to the Unity root.</summary>
    public readonly struct GameplayTriggerPlayableSnapshot
    {
        /// <remarks>NoAlloc.</remarks>
        internal GameplayTriggerPlayableSnapshot(
            long frame,
            Fix64 playerX,
            Fix64 playerZ,
            bool checkpointAccepted,
            bool doorUnlocked,
            bool doorOpen,
            int trapActivations,
            int slowFramesRemaining,
            GameplayTriggerOccurrenceKind lastOccurrenceKind,
            int lastOccurrenceSourceBindingId,
            int lastActionKindId,
            GameplayTriggerRuntimeEventKind lastResultKind,
            GameplayTriggerRuntimeEventStatus lastResultStatus,
            GameplayTriggerOccurrenceKind firstOccurrenceKindThisTick,
            int occurrenceCountThisTick,
            int actionResultCountThisTick,
            int combatBodyCount,
            int combatColliderCount,
            int lastControlCommandId,
            bool hasCapture,
            long capturedFrame,
            bool lastControlSucceeded,
            long authorityHash)
        {
            Frame = frame;
            PlayerXRaw = playerX.RawValue;
            PlayerZRaw = playerZ.RawValue;
            CheckpointAccepted = checkpointAccepted;
            DoorUnlocked = doorUnlocked;
            DoorOpen = doorOpen;
            TrapActivations = trapActivations;
            SlowFramesRemaining = slowFramesRemaining;
            LastOccurrenceKind = lastOccurrenceKind;
            LastOccurrenceSourceBindingId = lastOccurrenceSourceBindingId;
            LastActionKindId = lastActionKindId;
            LastResultKind = lastResultKind;
            LastResultStatus = lastResultStatus;
            FirstOccurrenceKindThisTick = firstOccurrenceKindThisTick;
            OccurrenceCountThisTick = occurrenceCountThisTick;
            ActionResultCountThisTick = actionResultCountThisTick;
            CombatBodyCount = combatBodyCount;
            CombatColliderCount = combatColliderCount;
            LastControlCommandId = lastControlCommandId;
            HasCapture = hasCapture;
            CapturedFrame = capturedFrame;
            LastControlSucceeded = lastControlSucceeded;
            AuthorityHash = authorityHash;
        }

        /// <remarks>NoAlloc.</remarks>
        public long Frame { get; }
        /// <remarks>NoAlloc.</remarks>
        public long PlayerXRaw { get; }
        /// <remarks>NoAlloc.</remarks>
        public long PlayerZRaw { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool CheckpointAccepted { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool DoorUnlocked { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool DoorOpen { get; }
        /// <remarks>NoAlloc.</remarks>
        public int TrapActivations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SlowFramesRemaining { get; }
        /// <summary>Gets the latest presentation-only Trigger occurrence.</summary>
        /// <remarks>NoAlloc. Excluded from SaveState and authority hash.</remarks>
        public GameplayTriggerOccurrenceKind LastOccurrenceKind { get; }
        /// <remarks>NoAlloc. Presentation-only and excluded from SaveState/hash.</remarks>
        public int LastOccurrenceSourceBindingId { get; }
        /// <remarks>NoAlloc. Presentation-only and excluded from SaveState/hash.</remarks>
        public int LastActionKindId { get; }
        /// <remarks>NoAlloc. Presentation-only and excluded from SaveState/hash.</remarks>
        public GameplayTriggerRuntimeEventKind LastResultKind { get; }
        /// <remarks>NoAlloc. Presentation-only and excluded from SaveState/hash.</remarks>
        public GameplayTriggerRuntimeEventStatus LastResultStatus { get; }
        /// <summary>Gets the first routed Trigger occurrence during the latest logical Tick.</summary>
        /// <remarks>NoAlloc. Presentation-only and reset before every Tick.</remarks>
        public GameplayTriggerOccurrenceKind FirstOccurrenceKindThisTick { get; }
        /// <summary>Gets the number of routed Trigger occurrences during the latest logical Tick.</summary>
        /// <remarks>NoAlloc. Presentation-only and reset before every Tick.</remarks>
        public int OccurrenceCountThisTick { get; }
        /// <summary>Gets target-owned terminal action results routed during the latest logical Tick.</summary>
        /// <remarks>NoAlloc. Presentation-only and reset before every Tick.</remarks>
        public int ActionResultCountThisTick { get; }
        /// <remarks>NoAlloc.</remarks>
        public int CombatBodyCount { get; }
        /// <remarks>NoAlloc.</remarks>
        public int CombatColliderCount { get; }
        /// <summary>Gets the control command consumed by the latest logical Tick, or zero.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int LastControlCommandId { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool HasCapture { get; }
        /// <summary>Gets the completed Runtime frame stored in the in-memory slot, or -1.</summary>
        /// <remarks>NoAlloc.</remarks>
        public long CapturedFrame { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool LastControlSucceeded { get; }
        /// <remarks>NoAlloc.</remarks>
        public long AuthorityHash { get; }

        internal GameplayTriggerPlayableSnapshot WithSessionState(
            int lastControlCommandId,
            bool hasCapture,
            long capturedFrame,
            bool lastControlSucceeded)
        {
            return new GameplayTriggerPlayableSnapshot(
                Frame,
                Fix64.FromRaw(PlayerXRaw),
                Fix64.FromRaw(PlayerZRaw),
                CheckpointAccepted,
                DoorUnlocked,
                DoorOpen,
                TrapActivations,
                SlowFramesRemaining,
                LastOccurrenceKind,
                LastOccurrenceSourceBindingId,
                LastActionKindId,
                LastResultKind,
                LastResultStatus,
                FirstOccurrenceKindThisTick,
                OccurrenceCountThisTick,
                ActionResultCountThisTick,
                CombatBodyCount,
                CombatColliderCount,
                lastControlCommandId,
                hasCapture,
                capturedFrame,
                lastControlSucceeded,
                AuthorityHash);
        }
    }

    /// <summary>Result of admitting and advancing one logical playable input.</summary>
    public readonly struct GameplayTriggerPlayableTickResult
    {
        /// <remarks>NoAlloc.</remarks>
        internal GameplayTriggerPlayableTickResult(
            bool accepted,
            GameplayTriggerPlayableSnapshot snapshot)
        {
            Accepted = accepted;
            Snapshot = snapshot;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Accepted { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerPlayableSnapshot Snapshot { get; }
    }

    /// <summary>Result of one capture, fresh restore, or reset operation.</summary>
    public readonly struct GameplayTriggerPlayableControlResult
    {
        internal GameplayTriggerPlayableControlResult(
            bool success,
            GameplayTriggerPlayableSnapshot snapshot)
        {
            Success = success;
            Snapshot = snapshot;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Success { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerPlayableSnapshot Snapshot { get; }
    }
}
