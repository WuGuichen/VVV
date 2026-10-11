using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("MxFramework.Persistence.RuntimeBridge")]

namespace MxFramework.Persistence
{
    public enum SaveGameRecordKind { Save = 0, Tombstone = 1 }

    /// <summary>One canonical, ordinally ordered metadata entry.</summary>
    public readonly struct SaveGameKeyValue
    {
        public SaveGameKeyValue(string key, string value) { Key = key ?? throw new ArgumentNullException(nameof(key)); Value = value ?? throw new ArgumentNullException(nameof(value)); }
        public string Key { get; }
        public string Value { get; }
    }

    /// <summary>Build identity recorded in every save or tombstone envelope.</summary>
    public sealed class SaveGameBuildDescriptor
    {
        public SaveGameBuildDescriptor(string frameworkVersion, string buildId, string configVersion, string resourceCatalogVersion, string contentSetId)
        {
            FrameworkVersion = RequireDescriptor(frameworkVersion, nameof(frameworkVersion), false);
            BuildId = RequireDescriptor(buildId, nameof(buildId), false);
            ConfigVersion = RequireDescriptor(configVersion, nameof(configVersion), false);
            ResourceCatalogVersion = RequireDescriptor(resourceCatalogVersion, nameof(resourceCatalogVersion), false);
            ContentSetId = RequireDescriptor(contentSetId, nameof(contentSetId), true);
        }
        public string FrameworkVersion { get; }
        public string BuildId { get; }
        public string ConfigVersion { get; }
        public string ResourceCatalogVersion { get; }
        public string ContentSetId { get; }

        public static bool TryCreate(
            string frameworkVersion,
            string buildId,
            string configVersion,
            string resourceCatalogVersion,
            string contentSetId,
            out SaveGameBuildDescriptor descriptor,
            out SaveGameError error)
        {
            try
            {
                descriptor = new SaveGameBuildDescriptor(frameworkVersion, buildId, configVersion, resourceCatalogVersion, contentSetId);
                error = SaveGameError.None;
                return true;
            }
            catch (ArgumentException exception)
            {
                descriptor = null;
                bool exceeded = ExceedsLimit(frameworkVersion) || ExceedsLimit(buildId) || ExceedsLimit(configVersion) ||
                    ExceedsLimit(resourceCatalogVersion) || ExceedsLimit(contentSetId);
                error = new SaveGameError(
                    exceeded ? SaveGameErrorCode.LimitExceeded : SaveGameErrorCode.InvalidEnvelope,
                    SaveGameOperationPhase.Validation,
                    "build",
                    exception.Message,
                    exception);
                return false;
            }
        }

        private static string RequireDescriptor(string value, string parameterName, bool allowEmpty)
        {
            if (value == null || !allowEmpty && value.Length == 0 || Encoding.UTF8.GetByteCount(value ?? string.Empty) > SaveGameLimits.MaximumDescriptorUtf8Bytes)
                throw new ArgumentException("Descriptor is empty or exceeds its UTF-8 limit.", parameterName);
            ValidateUnicode(value, parameterName);
            return value;
        }

        internal static void ValidateUnicode(string value, string parameterName)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i])) { if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])) throw new ArgumentException("String contains an unpaired surrogate.", parameterName); i++; }
                else if (char.IsLowSurrogate(value[i])) throw new ArgumentException("String contains an unpaired surrogate.", parameterName);
            }
        }

        private static bool ExceedsLimit(string value)
        {
            return value != null && Encoding.UTF8.GetByteCount(value) > SaveGameLimits.MaximumDescriptorUtf8Bytes;
        }
    }

    /// <summary>Immutable logical v1 envelope. Construction and payload copying are AllocByDesign.</summary>
    public sealed class SaveGameEnvelope
    {
        public const string FormatId = "mx.save-game";
        public const ushort CurrentEnvelopeVersion = 1;
        public const string RuntimeSaveStateJsonCodecId = "runtime-save-state-json";
        private readonly SaveGameKeyValue[] _compatibilityTags;
        private readonly SaveGameKeyValue[] _displayMetadata;
        private readonly byte[] _payload;

        private SaveGameEnvelope(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation, SaveGameRecordKind recordKind,
            DateTime createdAtUtc, DateTime updatedAtUtc, SaveGameBuildDescriptor build, SaveGameKeyValue[] compatibilityTags,
            string codecId, int runtimeSchemaVersion, long runtimeFrame, SaveGameKeyValue[] displayMetadata, byte[] payload)
        {
            if (!profile.IsValid) throw new ArgumentException("Profile id is invalid.", nameof(profile));
            if (!slot.IsValid) throw new ArgumentException("Slot id is invalid.", nameof(slot));
            if (!generation.IsPublished) throw new ArgumentException("Generation zero is not publishable.", nameof(generation));
            ValidateUtc(createdAtUtc, nameof(createdAtUtc)); ValidateUtc(updatedAtUtc, nameof(updatedAtUtc));
            if (updatedAtUtc < createdAtUtc) throw new ArgumentException("Updated timestamp precedes created timestamp.", nameof(updatedAtUtc));
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Profile = profile; Slot = slot; Generation = generation; RecordKind = recordKind; CreatedAtUtc = createdAtUtc; UpdatedAtUtc = updatedAtUtc;
            _compatibilityTags = CopyAndValidateCompatibilityTags(compatibilityTags, nameof(compatibilityTags));
            _displayMetadata = CopyAndValidateDisplayMetadata(displayMetadata, nameof(displayMetadata));
            if (recordKind == SaveGameRecordKind.Tombstone)
            {
                if (_compatibilityTags.Length != 0 || _displayMetadata.Length != 0 || createdAtUtc != updatedAtUtc || payload != null || codecId != null || runtimeSchemaVersion != 0 || runtimeFrame != 0)
                    throw new ArgumentException("Tombstone fields do not satisfy the v1 contract.");
                _payload = Array.Empty<byte>(); CodecId = string.Empty; return;
            }
            if (payload == null || payload.Length == 0 || payload.Length > SaveGameLimits.MaximumPayloadBytes) throw new ArgumentException("Save payload length is outside v1 limits.", nameof(payload));
            ValidateDescriptor(codecId, nameof(codecId));
            if (runtimeSchemaVersion < 0) throw new ArgumentOutOfRangeException(nameof(runtimeSchemaVersion));
            if (runtimeFrame < 0) throw new ArgumentOutOfRangeException(nameof(runtimeFrame));
            CodecId = codecId; RuntimeSchemaVersion = runtimeSchemaVersion; RuntimeFrame = runtimeFrame; _payload = (byte[])payload.Clone();
        }

        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration Generation { get; }
        public SaveGameRecordKind RecordKind { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public SaveGameBuildDescriptor Build { get; }
        public string CodecId { get; }
        public int RuntimeSchemaVersion { get; }
        public long RuntimeFrame { get; }
        public int PayloadLength => _payload.Length;
        public int CompatibilityTagCount => _compatibilityTags.Length;
        public int DisplayMetadataCount => _displayMetadata.Length;
        public SaveGameKeyValue GetCompatibilityTag(int index) => _compatibilityTags[index];
        public SaveGameKeyValue GetDisplayMetadata(int index) => _displayMetadata[index];
        public byte[] CopyPayload() => (byte[])_payload.Clone();
        internal byte[] PayloadBytes => _payload;

        public static SaveGameEnvelope CreateSave(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation,
            DateTime createdAtUtc, DateTime updatedAtUtc, SaveGameBuildDescriptor build, SaveGameKeyValue[] compatibilityTags,
            string codecId, int runtimeSchemaVersion, long runtimeFrame, SaveGameKeyValue[] displayMetadata, byte[] payload)
            => new SaveGameEnvelope(profile, slot, generation, SaveGameRecordKind.Save, createdAtUtc, updatedAtUtc, build, compatibilityTags, codecId, runtimeSchemaVersion, runtimeFrame, displayMetadata, payload);

        public static bool TryCreateSave(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation,
            DateTime createdAtUtc, DateTime updatedAtUtc, SaveGameBuildDescriptor build, SaveGameKeyValue[] compatibilityTags,
            string codecId, int runtimeSchemaVersion, long runtimeFrame, SaveGameKeyValue[] displayMetadata, byte[] payload,
            out SaveGameEnvelope envelope, out SaveGameError error)
        {
            try
            {
                envelope = CreateSave(profile, slot, generation, createdAtUtc, updatedAtUtc, build, compatibilityTags, codecId, runtimeSchemaVersion, runtimeFrame, displayMetadata, payload);
                error = SaveGameError.None;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is EncoderFallbackException)
            {
                envelope = null;
                bool exceeded = payload != null && payload.Length > SaveGameLimits.MaximumPayloadBytes ||
                    compatibilityTags != null && compatibilityTags.Length > SaveGameLimits.MaximumCompatibilityTags ||
                    displayMetadata != null && displayMetadata.Length > SaveGameLimits.MaximumDisplayMetadataEntries ||
                    ExceedsDescriptorLimit(codecId) ||
                    EntriesExceed(compatibilityTags, SaveGameLimits.MaximumCompatibilityTagKeyUtf8Bytes, SaveGameLimits.MaximumCompatibilityTagValueUtf8Bytes) ||
                    EntriesExceed(displayMetadata, SaveGameLimits.MaximumDisplayMetadataKeyUtf8Bytes, SaveGameLimits.MaximumDisplayMetadataValueUtf8Bytes);
                error = new SaveGameError(exceeded ? SaveGameErrorCode.LimitExceeded : SaveGameErrorCode.InvalidEnvelope,
                    SaveGameOperationPhase.Validation, "envelope", exception.Message, exception);
                return false;
            }
        }

        public static SaveGameEnvelope CreateTombstone(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation,
            DateTime deletedAtUtc, SaveGameBuildDescriptor build)
            => new SaveGameEnvelope(profile, slot, generation, SaveGameRecordKind.Tombstone, deletedAtUtc, deletedAtUtc, build, Array.Empty<SaveGameKeyValue>(), null, 0, 0, Array.Empty<SaveGameKeyValue>(), null);

        public static bool TryCreateTombstone(SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation,
            DateTime deletedAtUtc, SaveGameBuildDescriptor build, out SaveGameEnvelope envelope, out SaveGameError error)
        {
            try
            {
                envelope = CreateTombstone(profile, slot, generation, deletedAtUtc, build);
                error = SaveGameError.None;
                return true;
            }
            catch (ArgumentException exception)
            {
                envelope = null;
                error = new SaveGameError(deletedAtUtc.Kind == DateTimeKind.Utc ? SaveGameErrorCode.InvalidEnvelope : SaveGameErrorCode.InvalidTimestamp,
                    SaveGameOperationPhase.Validation, "envelope", exception.Message, exception);
                return false;
            }
        }

        private static void ValidateUtc(DateTime value, string parameterName)
        {
            if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must have DateTimeKind.Utc.", parameterName);
        }

        private static void ValidateDescriptor(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) > SaveGameLimits.MaximumDescriptorUtf8Bytes) throw new ArgumentException("Descriptor is empty or exceeds its UTF-8 limit.", parameterName);
            SaveGameBuildDescriptor.ValidateUnicode(value, parameterName);
        }

        private static bool ExceedsDescriptorLimit(string value)
        {
            return value != null && Encoding.UTF8.GetByteCount(value) > SaveGameLimits.MaximumDescriptorUtf8Bytes;
        }

        private static bool EntriesExceed(SaveGameKeyValue[] entries, int maximumKeyBytes, int maximumValueBytes)
        {
            if (entries == null) return false;
            for (var index = 0; index < entries.Length; index++)
            {
                if (Encoding.UTF8.GetByteCount(entries[index].Key ?? string.Empty) > maximumKeyBytes ||
                    Encoding.UTF8.GetByteCount(entries[index].Value ?? string.Empty) > maximumValueBytes) return true;
            }
            return false;
        }

        internal static SaveGameKeyValue[] CopyAndValidateCompatibilityTags(
            SaveGameKeyValue[] source,
            string parameterName) => CopyAndValidate(
                source,
                SaveGameLimits.MaximumCompatibilityTags,
                SaveGameLimits.MaximumCompatibilityTagKeyUtf8Bytes,
                SaveGameLimits.MaximumCompatibilityTagValueUtf8Bytes,
                parameterName);

        internal static SaveGameKeyValue[] CopyAndValidateDisplayMetadata(
            SaveGameKeyValue[] source,
            string parameterName) => CopyAndValidate(
                source,
                SaveGameLimits.MaximumDisplayMetadataEntries,
                SaveGameLimits.MaximumDisplayMetadataKeyUtf8Bytes,
                SaveGameLimits.MaximumDisplayMetadataValueUtf8Bytes,
                parameterName);

        private static SaveGameKeyValue[] CopyAndValidate(SaveGameKeyValue[] source, int maximumCount, int maximumKeyBytes, int maximumValueBytes, string parameterName)
        {
            if (source == null || source.Length > maximumCount) throw new ArgumentException("Metadata collection is null or exceeds its count limit.", parameterName);
            var copy = (SaveGameKeyValue[])source.Clone();
            Array.Sort(copy, (left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
            for (var i = 0; i < copy.Length; i++)
            {
                if (copy[i].Key == null || copy[i].Value == null || copy[i].Key.Length == 0 || Encoding.UTF8.GetByteCount(copy[i].Key) > maximumKeyBytes || Encoding.UTF8.GetByteCount(copy[i].Value) > maximumValueBytes)
                    throw new ArgumentException("Metadata key/value exceeds its UTF-8 limit.", parameterName);
                SaveGameBuildDescriptor.ValidateUnicode(copy[i].Key, parameterName); SaveGameBuildDescriptor.ValidateUnicode(copy[i].Value, parameterName);
                if (i != 0 && string.Equals(copy[i - 1].Key, copy[i].Key, StringComparison.Ordinal)) throw new ArgumentException("Metadata keys must be unique.", parameterName);
            }
            return copy;
        }
    }

    /// <summary>Owns one immutable encoded envelope byte sequence. Construction and copying are AllocByDesign.</summary>
    public sealed class SaveGameEncodedEnvelope
    {
        private readonly byte[] _bytes;
        public SaveGameEncodedEnvelope(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > SaveGameLimits.MaximumEnvelopeBytes) throw new ArgumentOutOfRangeException(nameof(bytes), "Encoded envelope exceeds the hard v1 maximum.");
            _bytes = (byte[])bytes.Clone();
        }
        public int Length => _bytes.Length;
        public byte[] ToArray() => (byte[])_bytes.Clone();
        public void CopyTo(byte[] destination, int destinationOffset) { if (destination == null) throw new ArgumentNullException(nameof(destination)); Buffer.BlockCopy(_bytes, 0, destination, destinationOffset, _bytes.Length); }
        internal byte[] Bytes => _bytes;
    }

    /// <summary>Result of encoding or decoding a persistence envelope.</summary>
    public readonly struct SaveGameEnvelopeResult
    {
        internal SaveGameEnvelopeResult(SaveGameEncodedEnvelope encoded, SaveGameEnvelope envelope, SaveGameError error) { Encoded = encoded; Envelope = envelope; Error = error; }
        public bool IsSuccess => Error.IsNone && Encoded != null && Envelope != null;
        public SaveGameEncodedEnvelope Encoded { get; }
        public SaveGameEnvelope Envelope { get; }
        public SaveGameError Error { get; }
    }
}
