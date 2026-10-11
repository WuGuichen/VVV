using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MxFramework.Persistence
{
    /// <summary>Encodes and strictly validates the exact canonical Persistence envelope v1. All methods are AllocByDesign.</summary>
    public static class SaveGameEnvelopeCodec
    {
        private const int PrefixLength = 24;
        private const int TrailerLength = 32;
        private const ushort TombstoneFlag = 1;
        private static readonly byte[] Magic = { 0x4d, 0x58, 0x53, 0x56, 0x47, 0x4d, 0x30, 0x31 };
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static SaveGameEnvelopeResult Encode(SaveGameEnvelope envelope)
        {
            if (envelope == null) return Failure(SaveGameErrorCode.InvalidEnvelope, "envelope", "Envelope is null.");
            try
            {
                var payloadHash = ComputeSha256(envelope.PayloadBytes);
                var headerText = BuildHeader(envelope, ToLowerHex(payloadHash));
                var headerBytes = StrictUtf8.GetBytes(headerText);
                if (headerBytes.Length > SaveGameLimits.MaximumHeaderBytes) return Failure(SaveGameErrorCode.LimitExceeded, "header", "Canonical header exceeds 64 KiB.");
                var payloadLength = envelope.RecordKind == SaveGameRecordKind.Save ? envelope.PayloadLength : 0;
                var totalLength = checked(PrefixLength + headerBytes.Length + payloadLength + TrailerLength);
                var bytes = new byte[totalLength];
                Buffer.BlockCopy(Magic, 0, bytes, 0, Magic.Length);
                WriteUInt16(bytes, 8, SaveGameEnvelope.CurrentEnvelopeVersion);
                WriteUInt16(bytes, 10, envelope.RecordKind == SaveGameRecordKind.Tombstone ? TombstoneFlag : (ushort)0);
                WriteUInt32(bytes, 12, (uint)headerBytes.Length);
                WriteUInt64(bytes, 16, (ulong)payloadLength);
                Buffer.BlockCopy(headerBytes, 0, bytes, PrefixLength, headerBytes.Length);
                if (payloadLength != 0) Buffer.BlockCopy(envelope.PayloadBytes, 0, bytes, PrefixLength + headerBytes.Length, payloadLength);
                var trailerOffset = totalLength - TrailerLength;
                var trailer = ComputeSha256(bytes, 0, trailerOffset);
                Buffer.BlockCopy(trailer, 0, bytes, trailerOffset, TrailerLength);
                return new SaveGameEnvelopeResult(new SaveGameEncodedEnvelope(bytes), envelope, SaveGameError.None);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException || exception is EncoderFallbackException || exception is CryptographicException)
            {
                return new SaveGameEnvelopeResult(null, null, new SaveGameError(SaveGameErrorCode.SerializeFailed, SaveGameOperationPhase.Serialization, "envelope", exception.Message, exception));
            }
        }

        public static SaveGameEnvelopeResult Decode(byte[] bytes)
        {
            if (bytes == null) return Failure(SaveGameErrorCode.InvalidEnvelope, "envelope", "Envelope bytes are null.");
            if (bytes.Length > SaveGameLimits.MaximumEnvelopeBytes) return Failure(SaveGameErrorCode.LimitExceeded, "envelope", "Envelope exceeds the hard v1 maximum.");
            return DecodeCore((byte[])bytes.Clone());
        }

        public static SaveGameEnvelopeResult Decode(SaveGameEncodedEnvelope encoded)
        {
            if (encoded == null) return Failure(SaveGameErrorCode.InvalidEnvelope, "envelope", "Encoded envelope is null.");
            if (encoded.Length > SaveGameLimits.MaximumEnvelopeBytes) return Failure(SaveGameErrorCode.LimitExceeded, "envelope", "Envelope exceeds the hard v1 maximum.");
            return DecodeCore(encoded.ToArray());
        }

        private static SaveGameEnvelopeResult DecodeCore(byte[] bytes)
        {
            if (bytes.Length > SaveGameLimits.MaximumEnvelopeBytes) return Failure(SaveGameErrorCode.LimitExceeded, "envelope", "Envelope exceeds the hard v1 maximum.");
            if (bytes.Length < PrefixLength + TrailerLength) return Failure(SaveGameErrorCode.InvalidEnvelope, "envelope", "Envelope is shorter than the v1 framing minimum.");
            for (var i = 0; i < Magic.Length; i++) if (bytes[i] != Magic[i]) return Failure(SaveGameErrorCode.InvalidEnvelope, "magic", "Envelope magic does not match MXSVGM01.");
            var version = ReadUInt16(bytes, 8);
            if (version != SaveGameEnvelope.CurrentEnvelopeVersion) return Failure(SaveGameErrorCode.UnsupportedEnvelopeVersion, "envelopeVersion", "Only envelope version 1 is supported.");
            var flags = ReadUInt16(bytes, 10);
            if ((flags & ~TombstoneFlag) != 0) return Failure(SaveGameErrorCode.UnsupportedEnvelopeFeature, "flags", "Envelope contains unsupported flag bits.");
            var headerLength = ReadUInt32(bytes, 12);
            var payloadLength = ReadUInt64(bytes, 16);
            if (headerLength > SaveGameLimits.MaximumHeaderBytes) return Failure(SaveGameErrorCode.LimitExceeded, "headerLength", "Header exceeds 64 KiB.");
            if (payloadLength > SaveGameLimits.MaximumPayloadBytes) return Failure(SaveGameErrorCode.LimitExceeded, "payloadLength", "Payload exceeds 64 MiB.");
            var expectedLength = (ulong)PrefixLength + headerLength + payloadLength + TrailerLength;
            if (expectedLength != (ulong)bytes.Length) return Failure(SaveGameErrorCode.InvalidEnvelope, "length", "Framing lengths do not equal the supplied byte length.");
            var trailerOffset = bytes.Length - TrailerLength;
            var expectedTrailer = ComputeSha256(bytes, 0, trailerOffset);
            if (!FixedTimeEquals(expectedTrailer, 0, bytes, trailerOffset, TrailerLength)) return Failure(SaveGameErrorCode.IntegrityFailed, "envelopeSha256", "Envelope SHA-256 trailer mismatch.");

            string headerText;
            try { headerText = StrictUtf8.GetString(bytes, PrefixLength, (int)headerLength); }
            catch (DecoderFallbackException exception) { return new SaveGameEnvelopeResult(null, null, new SaveGameError(SaveGameErrorCode.InvalidEnvelope, SaveGameOperationPhase.Integrity, "header", "Header is not strict UTF-8.", exception)); }

            ParsedHeader parsed;
            try { parsed = new CanonicalHeaderReader(headerText).Read(); }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                return new SaveGameEnvelopeResult(null, null, new SaveGameError(SaveGameErrorCode.InvalidEnvelope, SaveGameOperationPhase.Integrity, "header", exception.Message, exception));
            }

            var isTombstone = flags == TombstoneFlag;
            if (isTombstone != (parsed.RecordKind == SaveGameRecordKind.Tombstone)) return Failure(SaveGameErrorCode.EnvelopePayloadMismatch, "recordKind", "Flags and header record kind disagree.");
            if (isTombstone && payloadLength != 0 || !isTombstone && payloadLength == 0) return Failure(SaveGameErrorCode.EnvelopePayloadMismatch, "payloadLength", "Record kind and framed payload length disagree.");

            SaveGameEnvelope envelope;
            try
            {
                if (isTombstone)
                {
                    if (parsed.HasPayload) return Failure(SaveGameErrorCode.EnvelopePayloadMismatch, "payload", "Tombstone payload descriptor must be null.");
                    envelope = SaveGameEnvelope.CreateTombstone(parsed.Profile, parsed.Slot, parsed.Generation, parsed.CreatedAtUtc, parsed.Build);
                }
                else
                {
                    if (!parsed.HasPayload) return Failure(SaveGameErrorCode.EnvelopePayloadMismatch, "payload", "Save payload descriptor is required.");
                    if (parsed.StoredLength != payloadLength || parsed.UncompressedLength != payloadLength) return Failure(SaveGameErrorCode.EnvelopePayloadMismatch, "payload.storedLength", "Payload descriptor lengths do not match framing.");
                    if (parsed.TransformCount != 0) return Failure(SaveGameErrorCode.UnsupportedTransform, "payload.transformIds", "Envelope v1 supports only the identity transform chain.");
                    var payload = new byte[(int)payloadLength];
                    Buffer.BlockCopy(bytes, PrefixLength + (int)headerLength, payload, 0, payload.Length);
                    var payloadHash = ToLowerHex(ComputeSha256(payload));
                    if (!string.Equals(payloadHash, parsed.PayloadSha256, StringComparison.Ordinal)) return Failure(SaveGameErrorCode.IntegrityFailed, "payload.sha256", "Payload SHA-256 mismatch.");
                    envelope = SaveGameEnvelope.CreateSave(parsed.Profile, parsed.Slot, parsed.Generation, parsed.CreatedAtUtc, parsed.UpdatedAtUtc, parsed.Build, parsed.CompatibilityTags,
                        parsed.CodecId, parsed.RuntimeSchemaVersion, parsed.RuntimeFrame, parsed.DisplayMetadata, payload);
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                return new SaveGameEnvelopeResult(null, null, new SaveGameError(SaveGameErrorCode.InvalidEnvelope, SaveGameOperationPhase.Integrity, "header", exception.Message, exception));
            }

            var canonical = Encode(envelope);
            if (!canonical.IsSuccess) return canonical;
            var canonicalBytes = canonical.Encoded.Bytes;
            if ((int)headerLength != ReadUInt32(canonicalBytes, 12) || !FixedTimeEquals(bytes, PrefixLength, canonicalBytes, PrefixLength, (int)headerLength))
                return Failure(SaveGameErrorCode.InvalidEnvelope, "header", "Header is valid JSON but not exact canonical v1 JSON.");
            return new SaveGameEnvelopeResult(new SaveGameEncodedEnvelope(bytes), envelope, SaveGameError.None);
        }

        private static string BuildHeader(SaveGameEnvelope envelope, string payloadSha256)
        {
            var builder = new StringBuilder(512);
            builder.Append('{'); Property(builder, "format", SaveGameEnvelope.FormatId); builder.Append(',');
            Property(builder, "profile", envelope.Profile.Value); builder.Append(','); Property(builder, "slot", envelope.Slot.Value); builder.Append(',');
            Property(builder, "generation", envelope.Generation.ToString()); builder.Append(','); Property(builder, "recordKind", envelope.RecordKind == SaveGameRecordKind.Save ? "save" : "tombstone"); builder.Append(',');
            Property(builder, "createdAtUtc", FormatTimestamp(envelope.CreatedAtUtc)); builder.Append(','); Property(builder, "updatedAtUtc", FormatTimestamp(envelope.UpdatedAtUtc)); builder.Append(',');
            WriteString(builder, "build"); builder.Append(":{"); Property(builder, "frameworkVersion", envelope.Build.FrameworkVersion); builder.Append(','); Property(builder, "buildId", envelope.Build.BuildId); builder.Append(',');
            Property(builder, "configVersion", envelope.Build.ConfigVersion); builder.Append(','); Property(builder, "resourceCatalogVersion", envelope.Build.ResourceCatalogVersion); builder.Append(','); Property(builder, "contentSetId", envelope.Build.ContentSetId); builder.Append("},");
            WriteString(builder, "compatibilityTags"); builder.Append(':'); WriteEntries(builder, envelope, true); builder.Append(',');
            WriteString(builder, "payload"); builder.Append(':');
            if (envelope.RecordKind == SaveGameRecordKind.Tombstone) builder.Append("null");
            else
            {
                builder.Append('{'); Property(builder, "codecId", envelope.CodecId); builder.Append(',');
                WriteString(builder, "runtimeSchemaVersion"); builder.Append(':').Append(envelope.RuntimeSchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(',');
                Property(builder, "runtimeFrame", envelope.RuntimeFrame.ToString(CultureInfo.InvariantCulture)); builder.Append(',');
                WriteString(builder, "uncompressedLength"); builder.Append(':').Append(envelope.PayloadLength.ToString(CultureInfo.InvariantCulture)).Append(',');
                WriteString(builder, "storedLength"); builder.Append(':').Append(envelope.PayloadLength.ToString(CultureInfo.InvariantCulture)).Append(',');
                WriteString(builder, "transformIds"); builder.Append(":[],"); Property(builder, "sha256", payloadSha256); builder.Append('}');
            }
            builder.Append(','); WriteString(builder, "displayMetadata"); builder.Append(':'); WriteEntries(builder, envelope, false); builder.Append('}');
            return builder.ToString();
        }

        private static void WriteEntries(StringBuilder builder, SaveGameEnvelope envelope, bool tags)
        {
            builder.Append('['); var count = tags ? envelope.CompatibilityTagCount : envelope.DisplayMetadataCount;
            for (var i = 0; i < count; i++) { if (i != 0) builder.Append(','); var entry = tags ? envelope.GetCompatibilityTag(i) : envelope.GetDisplayMetadata(i); builder.Append('{'); Property(builder, "key", entry.Key); builder.Append(','); Property(builder, "value", entry.Value); builder.Append('}'); }
            builder.Append(']');
        }

        private static void Property(StringBuilder builder, string name, string value) { WriteString(builder, name); builder.Append(':'); WriteString(builder, value); }
        private static void WriteString(StringBuilder builder, string value)
        {
            SaveGameBuildDescriptor.ValidateUnicode(value, nameof(value)); builder.Append('"');
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '"') builder.Append("\\\""); else if (character == '\\') builder.Append("\\\\");
                else if (character <= 0x1f) { builder.Append("\\u00"); builder.Append(Hex(character >> 4)); builder.Append(Hex(character & 15)); }
                else builder.Append(character);
            }
            builder.Append('"');
        }

        private static char Hex(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);
        private static string FormatTimestamp(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        private static byte[] ComputeSha256(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static byte[] ComputeSha256(byte[] bytes, int offset, int count) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes, offset, count); }
        private static string ToLowerHex(byte[] bytes) { var chars = new char[bytes.Length * 2]; for (var i = 0; i < bytes.Length; i++) { chars[i * 2] = Hex(bytes[i] >> 4); chars[i * 2 + 1] = Hex(bytes[i] & 15); } return new string(chars); }
        private static bool FixedTimeEquals(byte[] left, int leftOffset, byte[] right, int rightOffset, int count) { var difference = 0; for (var i = 0; i < count; i++) difference |= left[leftOffset + i] ^ right[rightOffset + i]; return difference == 0; }
        private static void WriteUInt16(byte[] bytes, int offset, ushort value) { bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8); }
        private static void WriteUInt32(byte[] bytes, int offset, uint value) { for (var i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> i * 8); }
        private static void WriteUInt64(byte[] bytes, int offset, ulong value) { for (var i = 0; i < 8; i++) bytes[offset + i] = (byte)(value >> i * 8); }
        private static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        private static uint ReadUInt32(byte[] bytes, int offset) { uint value = 0; for (var i = 0; i < 4; i++) value |= (uint)bytes[offset + i] << i * 8; return value; }
        private static ulong ReadUInt64(byte[] bytes, int offset) { ulong value = 0; for (var i = 0; i < 8; i++) value |= (ulong)bytes[offset + i] << i * 8; return value; }
        private static SaveGameEnvelopeResult Failure(SaveGameErrorCode code, string path, string message) => new SaveGameEnvelopeResult(null, null, new SaveGameError(code, SaveGameOperationPhase.Integrity, path, message));

        private sealed class ParsedHeader
        {
            public SaveGameProfileId Profile; public SaveGameSlotId Slot; public SaveGameGeneration Generation; public SaveGameRecordKind RecordKind;
            public DateTime CreatedAtUtc; public DateTime UpdatedAtUtc; public SaveGameBuildDescriptor Build; public SaveGameKeyValue[] CompatibilityTags; public SaveGameKeyValue[] DisplayMetadata;
            public bool HasPayload; public string CodecId; public int RuntimeSchemaVersion; public long RuntimeFrame; public ulong UncompressedLength; public ulong StoredLength; public int TransformCount; public string PayloadSha256;
        }

        private sealed class CanonicalHeaderReader
        {
            private readonly string _text; private int _position;
            public CanonicalHeaderReader(string text) { _text = text ?? throw new ArgumentNullException(nameof(text)); }
            public ParsedHeader Read()
            {
                var result = new ParsedHeader(); Expect('{'); ReadProperty("format"); if (ReadString() != SaveGameEnvelope.FormatId) Fail("format must equal mx.save-game."); Comma();
                ReadProperty("profile"); result.Profile = new SaveGameProfileId(ReadString()); Comma(); ReadProperty("slot"); result.Slot = new SaveGameSlotId(ReadString()); Comma();
                ReadProperty("generation"); result.Generation = new SaveGameGeneration(ReadQuotedUInt64(false)); Comma(); ReadProperty("recordKind"); var kind = ReadString(); if (kind == "save") result.RecordKind = SaveGameRecordKind.Save; else if (kind == "tombstone") result.RecordKind = SaveGameRecordKind.Tombstone; else Fail("recordKind is invalid."); Comma();
                ReadProperty("createdAtUtc"); result.CreatedAtUtc = ReadTimestamp(); Comma(); ReadProperty("updatedAtUtc"); result.UpdatedAtUtc = ReadTimestamp(); Comma();
                ReadProperty("build"); Expect('{'); ReadProperty("frameworkVersion"); var frameworkVersion = ReadString(); Comma(); ReadProperty("buildId"); var buildId = ReadString(); Comma(); ReadProperty("configVersion"); var configVersion = ReadString(); Comma(); ReadProperty("resourceCatalogVersion"); var resourceCatalog = ReadString(); Comma(); ReadProperty("contentSetId"); var contentSet = ReadString(); Expect('}'); result.Build = new SaveGameBuildDescriptor(frameworkVersion, buildId, configVersion, resourceCatalog, contentSet); Comma();
                ReadProperty("compatibilityTags"); result.CompatibilityTags = ReadEntries(SaveGameLimits.MaximumCompatibilityTags); Comma(); ReadProperty("payload");
                if (TryReadNull()) { result.HasPayload = false; }
                else { result.HasPayload = true; Expect('{'); ReadProperty("codecId"); result.CodecId = ReadString(); Comma(); ReadProperty("runtimeSchemaVersion"); var schema = ReadUInt64(true); if (schema > int.MaxValue) Fail("runtimeSchemaVersion is out of range."); result.RuntimeSchemaVersion = (int)schema; Comma(); ReadProperty("runtimeFrame"); var runtimeFrame = ReadQuotedUInt64(true); if (runtimeFrame > long.MaxValue) Fail("runtimeFrame is out of range."); result.RuntimeFrame = (long)runtimeFrame; Comma(); ReadProperty("uncompressedLength"); result.UncompressedLength = ReadUInt64(true); Comma(); ReadProperty("storedLength"); result.StoredLength = ReadUInt64(true); Comma(); ReadProperty("transformIds"); result.TransformCount = ReadStringArray(); Comma(); ReadProperty("sha256"); result.PayloadSha256 = ReadSha256(); Expect('}'); }
                Comma(); ReadProperty("displayMetadata"); result.DisplayMetadata = ReadEntries(SaveGameLimits.MaximumDisplayMetadataEntries); Expect('}'); if (_position != _text.Length) Fail("Header has a trailing token.");
                if (result.RecordKind == SaveGameRecordKind.Tombstone && result.CreatedAtUtc != result.UpdatedAtUtc) Fail("Tombstone timestamps differ.");
                return result;
            }

            private SaveGameKeyValue[] ReadEntries(int maximum)
            {
                var entries = new List<SaveGameKeyValue>(); Expect('['); if (Take(']')) return entries.ToArray();
                while (true)
                {
                    if (entries.Count == maximum) Fail("Metadata entry count exceeds its v1 limit."); Expect('{'); ReadProperty("key"); var key = ReadString(); Comma(); ReadProperty("value"); var value = ReadString(); Expect('}');
                    if (entries.Count != 0 && string.Compare(entries[entries.Count - 1].Key, key, StringComparison.Ordinal) >= 0) Fail("Metadata keys are not unique ordinal ascending."); entries.Add(new SaveGameKeyValue(key, value));
                    if (Take(']')) break; Comma();
                }
                return entries.ToArray();
            }

            private int ReadStringArray()
            {
                var count = 0; Expect('['); if (Take(']')) return 0;
                while (true) { ReadString(); count++; if (Take(']')) return count; Comma(); }
            }

            private string ReadSha256()
            {
                var value = ReadString(); if (value.Length != 64) Fail("SHA-256 must contain 64 lowercase hexadecimal characters.");
                for (var i = 0; i < value.Length; i++) if (!(value[i] >= '0' && value[i] <= '9') && !(value[i] >= 'a' && value[i] <= 'f')) Fail("SHA-256 is not lowercase hexadecimal."); return value;
            }

            private DateTime ReadTimestamp()
            {
                var value = ReadString(); DateTime parsed;
                if (!DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed) || parsed.Kind != DateTimeKind.Utc) Fail("Timestamp is not exact UTC v1 format."); return parsed;
            }

            private ulong ReadQuotedUInt64(bool allowZero) { var value = ReadString(); return ParseUInt64(value, allowZero); }
            private ulong ReadUInt64(bool allowZero)
            {
                var start = _position; while (_position < _text.Length && _text[_position] >= '0' && _text[_position] <= '9') _position++;
                if (start == _position) Fail("Expected unsigned integer."); return ParseUInt64(_text.Substring(start, _position - start), allowZero);
            }
            private static ulong ParseUInt64(string value, bool allowZero)
            {
                if (value.Length == 0 || value.Length > 1 && value[0] == '0') throw new FormatException("Integer is not canonical decimal."); ulong parsed;
                if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || !allowZero && parsed == 0) throw new FormatException("Integer is outside its v1 range."); return parsed;
            }

            private string ReadString()
            {
                Expect('"'); var builder = new StringBuilder();
                while (_position < _text.Length)
                {
                    var character = _text[_position++]; if (character == '"') return builder.ToString();
                    if (character == '\\')
                    {
                        if (_position >= _text.Length) Fail("Unterminated escape."); var escaped = _text[_position++];
                        if (escaped == '"' || escaped == '\\') builder.Append(escaped);
                        else if (escaped == 'u') { if (_position + 4 > _text.Length || _text[_position] != '0' || _text[_position + 1] != '0') Fail("Only canonical lowercase control escapes are allowed."); var high = LowerHex(_text[_position + 2]); var low = LowerHex(_text[_position + 3]); if (high < 0 || low < 0) Fail("Escape is not lowercase hexadecimal."); var control = (char)(high * 16 + low); if (control > 0x1f) Fail("Non-control characters must not use unicode escapes."); builder.Append(control); _position += 4; }
                        else Fail("Short JSON escapes are not canonical v1.");
                    }
                    else
                    {
                        if (character <= 0x1f) Fail("Unescaped control character.");
                        if (char.IsHighSurrogate(character)) { if (_position >= _text.Length || !char.IsLowSurrogate(_text[_position])) Fail("Unpaired surrogate."); builder.Append(character); builder.Append(_text[_position++]); }
                        else if (char.IsLowSurrogate(character)) Fail("Unpaired surrogate."); else builder.Append(character);
                    }
                }
                Fail("Unterminated string."); return null;
            }

            private static int LowerHex(char value) { if (value >= '0' && value <= '9') return value - '0'; if (value >= 'a' && value <= 'f') return value - 'a' + 10; return -1; }
            private void ReadProperty(string expected) { var actual = ReadString(); if (!string.Equals(actual, expected, StringComparison.Ordinal)) Fail("Expected property '" + expected + "'."); Expect(':'); }
            private bool TryReadNull() { if (_position + 4 <= _text.Length && string.CompareOrdinal(_text, _position, "null", 0, 4) == 0) { _position += 4; return true; } return false; }
            private void Comma() { Expect(','); }
            private bool Take(char value) { if (_position < _text.Length && _text[_position] == value) { _position++; return true; } return false; }
            private void Expect(char value) { if (!Take(value)) Fail("Expected '" + value + "' at offset " + _position.ToString(CultureInfo.InvariantCulture) + "."); }
            private static void Fail(string message) { throw new FormatException(message); }
        }
    }
}
