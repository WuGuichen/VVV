using System;

namespace MxFramework.Persistence.RuntimeBridge
{
    /// <summary>Immutable caller facts used to capture one Runtime checkpoint.</summary>
    /// <remarks>
    /// AllocByDesign. Construction defensively copies both metadata arrays. Build identity is supplied by the service.
    /// All property and indexed metadata reads are NoAlloc after construction.
    /// </remarks>
    public sealed class RuntimeSaveGameCheckpointRequest
    {
        private readonly SaveGameKeyValue[] _compatibilityTags;
        private readonly SaveGameKeyValue[] _displayMetadata;

        /// <remarks>AllocByDesign. Defensively copies both supplied metadata arrays.</remarks>
        public RuntimeSaveGameCheckpointRequest(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration expectedHead,
            DateTime createdAtUtc,
            DateTime updatedAtUtc,
            SaveGameKeyValue[] compatibilityTags,
            SaveGameKeyValue[] displayMetadata)
        {
            if (!profile.IsValid) throw new ArgumentException("Profile id is invalid.", nameof(profile));
            if (!slot.IsValid) throw new ArgumentException("Slot id is invalid.", nameof(slot));
            RequireUtc(createdAtUtc, nameof(createdAtUtc));
            RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
            if (updatedAtUtc < createdAtUtc) throw new ArgumentException("Updated timestamp precedes created timestamp.", nameof(updatedAtUtc));

            Profile = profile;
            Slot = slot;
            ExpectedHead = expectedHead;
            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = updatedAtUtc;
            _compatibilityTags = SaveGameEnvelope.CopyAndValidateCompatibilityTags(
                compatibilityTags ?? Array.Empty<SaveGameKeyValue>(), nameof(compatibilityTags));
            _displayMetadata = SaveGameEnvelope.CopyAndValidateDisplayMetadata(
                displayMetadata ?? Array.Empty<SaveGameKeyValue>(), nameof(displayMetadata));
        }

        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration ExpectedHead { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public int CompatibilityTagCount => _compatibilityTags.Length;
        public int DisplayMetadataCount => _displayMetadata.Length;

        /// <remarks>NoAlloc.</remarks>
        public SaveGameKeyValue GetCompatibilityTag(int index) => _compatibilityTags[index];

        /// <remarks>NoAlloc.</remarks>
        public SaveGameKeyValue GetDisplayMetadata(int index) => _displayMetadata[index];

        private static void RequireUtc(DateTime value, string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", parameterName);
        }
    }
}
