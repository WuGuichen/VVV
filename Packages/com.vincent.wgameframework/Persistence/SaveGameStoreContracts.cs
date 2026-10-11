using System;
using System.Threading;

namespace MxFramework.Persistence
{
    public enum SaveGameReadSelectionKind { Current = 0, ExactGeneration = 1 }
    public enum SaveGameRecoveryPolicy { PrimaryOnly = 0, PrimaryThenLastKnownGood = 1 }
    public enum SaveGameRecoverySource { None = 0, Primary = 1, LastKnownGood = 2 }

    /// <summary>Selects either the authoritative current head or one exact retained generation.</summary>
    public readonly struct SaveGameReadSelection
    {
        private SaveGameReadSelection(SaveGameReadSelectionKind kind, SaveGameGeneration generation) { Kind = kind; Generation = generation; }
        public SaveGameReadSelectionKind Kind { get; }
        public SaveGameGeneration Generation { get; }
        public static SaveGameReadSelection Current => new SaveGameReadSelection(SaveGameReadSelectionKind.Current, SaveGameGeneration.None);
        public static SaveGameReadSelection Exact(SaveGameGeneration generation) { if (!generation.IsPublished) throw new ArgumentException("Exact selection requires a published generation.", nameof(generation)); return new SaveGameReadSelection(SaveGameReadSelectionKind.ExactGeneration, generation); }
        public bool IsValid => Kind == SaveGameReadSelectionKind.Current && !Generation.IsPublished || Kind == SaveGameReadSelectionKind.ExactGeneration && Generation.IsPublished;
    }

    /// <summary>Requests one validated encoded save record to be published with compare-and-swap semantics.</summary>
    public sealed class SaveGameCommitRequest
    {
        public SaveGameCommitRequest(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration expectedHead, SaveGameEncodedEnvelope envelope) { Profile = profile; Slot = slot; ExpectedHead = expectedHead; Envelope = envelope; }
        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration ExpectedHead { get; }
        public SaveGameEncodedEnvelope Envelope { get; }
    }

    /// <summary>Requests a read without authorizing fallback for exact-generation selection.</summary>
    public sealed class SaveGameReadRequest
    {
        public SaveGameReadRequest(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameReadSelection selection, SaveGameRecoveryPolicy recoveryPolicy = SaveGameRecoveryPolicy.PrimaryOnly)
            : this(profile, slot, selection, recoveryPolicy, includeDeleted: false) { }
        /// <summary>Requests an optional fully validated tombstone record for explicit generation-aware recreation.</summary>
        /// <remarks>LowFreqAlloc. IncludeDeleted never permits recovery across a tombstone or changes exact-selection recovery rules.</remarks>
        public SaveGameReadRequest(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameReadSelection selection, SaveGameRecoveryPolicy recoveryPolicy, bool includeDeleted) { Profile = profile; Slot = slot; Selection = selection; RecoveryPolicy = recoveryPolicy; IncludeDeleted = includeDeleted; }
        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameReadSelection Selection { get; }
        public SaveGameRecoveryPolicy RecoveryPolicy { get; }
        public bool IncludeDeleted { get; }
    }

    /// <summary>Requests a stable ordinal slot listing for one profile.</summary>
    public sealed class SaveGameListRequest
    {
        public SaveGameListRequest(SaveGameProfileId profile, bool includeDeleted = false) { Profile = profile; IncludeDeleted = includeDeleted; }
        public SaveGameProfileId Profile { get; }
        public bool IncludeDeleted { get; }
    }

    /// <summary>Requests publication of a tombstone at expected head plus one.</summary>
    public sealed class SaveGameDeleteRequest
    {
        public SaveGameDeleteRequest(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration expectedHead, DateTime deletedAtUtc, SaveGameBuildDescriptor build) { Profile = profile; Slot = slot; ExpectedHead = expectedHead; DeletedAtUtc = deletedAtUtc; Build = build; }
        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration ExpectedHead { get; }
        public DateTime DeletedAtUtc { get; }
        public SaveGameBuildDescriptor Build { get; }
    }

    /// <summary>Successful commit publication result, optionally carrying a non-fatal post-publication warning.</summary>
    /// <remarks>LowFreqAlloc construction; property reads are NoAlloc.</remarks>
    public readonly struct SaveGameCommitResult
    {
        public SaveGameCommitResult(SaveGameGeneration generation) : this(generation, SaveGameWarning.None) { }
        public SaveGameCommitResult(SaveGameGeneration generation, SaveGameWarning warning) { Generation = generation; Warning = warning; }
        public SaveGameGeneration Generation { get; }
        public SaveGameWarning Warning { get; }
    }

    /// <summary>Successful read result with explicit original and selected generations.</summary>
    public sealed class SaveGameReadResult
    {
        public SaveGameReadResult(SaveGameEncodedEnvelope envelope, SaveGameGeneration originalGeneration, SaveGameGeneration selectedGeneration, SaveGameRecoverySource recoverySource) { Envelope = envelope; OriginalGeneration = originalGeneration; SelectedGeneration = selectedGeneration; RecoverySource = recoverySource; }
        public SaveGameEncodedEnvelope Envelope { get; }
        public SaveGameGeneration OriginalGeneration { get; }
        public SaveGameGeneration SelectedGeneration { get; }
        public SaveGameRecoverySource RecoverySource { get; }
    }

    /// <summary>One immutable slot listing summary.</summary>
    public readonly struct SaveGameSlotSummary
    {
        public SaveGameSlotSummary(SaveGameSlotId slot, SaveGameGeneration generation, DateTime updatedAtUtc, SaveGameRecordKind recordKind) { Slot = slot; Generation = generation; UpdatedAtUtc = updatedAtUtc; RecordKind = recordKind; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration Generation { get; }
        public DateTime UpdatedAtUtc { get; }
        public SaveGameRecordKind RecordKind { get; }
        public bool IsDeleted => RecordKind == SaveGameRecordKind.Tombstone;
    }

    /// <summary>Stable ordinal slot list. Copying the summary array is AllocByDesign.</summary>
    public sealed class SaveGameListResult
    {
        private readonly SaveGameSlotSummary[] _slots;
        public SaveGameListResult(SaveGameSlotSummary[] slots) { _slots = slots == null ? throw new ArgumentNullException(nameof(slots)) : (SaveGameSlotSummary[])slots.Clone(); }
        public int Count => _slots.Length;
        public SaveGameSlotSummary GetSlot(int index) => _slots[index];
        public SaveGameSlotSummary[] ToArray() => (SaveGameSlotSummary[])_slots.Clone();
    }

    /// <summary>Successful tombstone publication result, optionally carrying a non-fatal post-publication warning.</summary>
    /// <remarks>LowFreqAlloc construction; property reads are NoAlloc.</remarks>
    public readonly struct SaveGameDeleteResult
    {
        public SaveGameDeleteResult(SaveGameGeneration generation) : this(generation, SaveGameWarning.None) { }
        public SaveGameDeleteResult(SaveGameGeneration generation, SaveGameWarning warning) { Generation = generation; Warning = warning; }
        public SaveGameGeneration Generation { get; }
        public SaveGameWarning Warning { get; }
    }

    /// <summary>Zero-engine persistence store contract with async-shaped operations.</summary>
    public interface ISaveGameStore
    {
        ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request, CancellationToken cancellationToken = default);
        ISaveGameOperation<SaveGameReadResult> Read(SaveGameReadRequest request, CancellationToken cancellationToken = default);
        ISaveGameOperation<SaveGameListResult> List(SaveGameListRequest request, CancellationToken cancellationToken = default);
        ISaveGameOperation<SaveGameDeleteResult> Delete(SaveGameDeleteRequest request, CancellationToken cancellationToken = default);
    }
}
