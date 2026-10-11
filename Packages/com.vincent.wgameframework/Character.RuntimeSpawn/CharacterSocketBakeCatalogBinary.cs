using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>Stable diagnostic codes for socket bake v3 loading. Logic must branch on these, never on message text.</summary>
    public static class CharacterSocketBakeDiagnosticCodes
    {
        public const string ReimportRequired = "SOCKET_BAKE_REIMPORT_REQUIRED";
        public const string FormatUnsupported = "SOCKET_BAKE_FORMAT_UNSUPPORTED";
        public const string PayloadMissing = "SOCKET_BAKE_PAYLOAD_MISSING";
        public const string IdentityMismatch = "SOCKET_BAKE_IDENTITY_MISMATCH";
        public const string HashMismatch = "SOCKET_BAKE_HASH_MISMATCH";
        public const string PayloadInvalid = "SOCKET_BAKE_PAYLOAD_INVALID";
        public const string LimitExceeded = "SOCKET_BAKE_LIMIT_EXCEEDED";
        public const string TrackInvalid = "SOCKET_BAKE_TRACK_INVALID";
        public const string BasisInvalid = "SOCKET_BAKE_BASIS_INVALID";
    }

    /// <summary>Blocking failure raised by the writer or the strict reader. <see cref="Code"/> is one of the diagnostic codes.</summary>
    public sealed class CharacterSocketBakeBinaryException : Exception
    {
        public CharacterSocketBakeBinaryException(string code, string message)
            : base((code ?? string.Empty) + ": " + (message ?? string.Empty))
        {
            Code = code ?? string.Empty;
        }

        public string Code { get; }
    }

    /// <summary>
    /// Verified product identity of a socket bake pair. Only produced by a successful load;
    /// never trust <c>default</c> as evidence, always branch on the load return value.
    /// </summary>
    public readonly struct CharacterSocketBakeCatalogEvidence
    {
        public CharacterSocketBakeCatalogEvidence(
            string format,
            string indexHash,
            string binaryHash,
            long binarySize,
            int trackCount,
            int frameCount,
            int basisFrameCount)
        {
            Format = format ?? string.Empty;
            IndexHash = indexHash ?? string.Empty;
            BinaryHash = binaryHash ?? string.Empty;
            BinarySize = binarySize;
            TrackCount = trackCount;
            FrameCount = frameCount;
            BasisFrameCount = basisFrameCount;
        }

        public string Format { get; }
        public string IndexHash { get; }
        public string BinaryHash { get; }
        public long BinarySize { get; }
        public int TrackCount { get; }
        public int FrameCount { get; }
        public int BasisFrameCount { get; }

        public bool IsValid => !string.IsNullOrEmpty(Format) && !string.IsNullOrEmpty(BinaryHash);
    }

    /// <summary>
    /// Socket bake catalog v3 persistence: a small JSON index plus one lossless little-endian
    /// binary payload. No compression, no delta/VarInt encoding; every integer is preserved exactly.
    /// </summary>
    public static class CharacterSocketBakeCatalogBinary
    {
        public const string Format = "mx.character.socketBakeCatalog.v3";
        public const string DefaultIndexRelativePath = "generated/character_socket_bake_catalog.json";
        public const string DefaultBinaryRelativePath = "generated/character_socket_bake_catalog.bytes";

        public const int BinaryVersion = 3;

        /// <summary>Four ASCII bytes "MSB3" read as a little-endian UInt32.</summary>
        public const uint MagicValue = 0x3342534D;

        public const int HeaderSize = 28;
        public const int TrackHeaderSize = 24;
        public const int PositionRecordSize = 52;
        public const int BasisRecordSize = 72;

        public const int MaxIndexBytes = 8 * 1024;
        public const long MaxBinaryBytes = 256L * 1024 * 1024;
        public const int MaxStringCount = 65536;
        public const int MaxStringBytes = 65535;
        public const int MaxStringContentBytes = 8 * 1024 * 1024;
        public const int MaxTrackCount = 4096;
        public const int MaxTrackFrameCount = 65536;
        public const int MaxTotalFrameCount = 1048576;

        public const uint FlagHasBasis = 1u;

        /// <summary>Legacy formats that must be re-imported rather than silently accepted.</summary>
        private static readonly string[] LegacyFormatMarkers =
        {
            "mx.character.socketBakeCatalog.v1",
            "mx.character.socketBakeCatalog.v2"
        };

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // ---------------------------------------------------------------- writing

        /// <summary>
        /// Encodes <paramref name="catalog"/> into the v3 payload. Throws
        /// <see cref="CharacterSocketBakeBinaryException"/> when the catalog cannot be represented
        /// losslessly; callers must turn that into a failed socket bake result and must not publish
        /// a success receipt.
        /// </summary>
        public static byte[] Serialize(string packageId, CharacterSocketBakeCatalog catalog)
        {
            List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>> ordered = OrderTracks(catalog);
            if (string.IsNullOrEmpty(packageId))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.IdentityMismatch,
                    "packageId is required to serialize a socket bake catalog.");
            if (ordered.Count > MaxTrackCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "track count " + ordered.Count + " exceeds " + MaxTrackCount + ".");

            ValidateTracks(ordered);

            int totalFrames = 0;
            int totalBasisFrames = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                CharacterSocketBakeTrack track = ordered[i].Value;
                totalFrames = checked(totalFrames + track.Frames.Count);
                if (track.HasBasis)
                    totalBasisFrames = checked(totalBasisFrames + track.BasisFrames.Count);
            }
            if (totalFrames > MaxTotalFrameCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "total frame count " + totalFrames + " exceeds " + MaxTotalFrameCount + ".");
            if (totalBasisFrames > totalFrames)
                throw Limit(CharacterSocketBakeDiagnosticCodes.BasisInvalid,
                    "basis frame count exceeds position frame count.");

            List<string> strings = CollectStrings(packageId, ordered);
            var stringIndex = new Dictionary<string, int>(strings.Count, StringComparer.Ordinal);
            for (int i = 0; i < strings.Count; i++)
                stringIndex[strings[i]] = i;

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, StrictUtf8, leaveOpen: true))
            {
                writer.Write(MagicValue);
                writer.Write((ushort)BinaryVersion);
                writer.Write((ushort)0);
                writer.Write((uint)stringIndex[packageId]);
                writer.Write((uint)strings.Count);
                writer.Write((uint)ordered.Count);
                writer.Write((uint)totalFrames);
                writer.Write((uint)totalBasisFrames);

                for (int i = 0; i < strings.Count; i++)
                    WriteString(writer, strings[i]);

                for (int trackIndex = 0; trackIndex < ordered.Count; trackIndex++)
                {
                    KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack> pair = ordered[trackIndex];
                    CharacterSocketBakeTrack track = pair.Value;
                    IReadOnlyList<MxAnimationBakedSocketFrame> frames = track.Frames;
                    bool hasBasis = track.HasBasis;

                    writer.Write((uint)stringIndex[pair.Key.ClipKey]);
                    writer.Write(pair.Key.SocketId);
                    writer.Write(track.QuantizationScale);
                    writer.Write(hasBasis ? track.BasisQuantizationScale : 0);
                    writer.Write((uint)frames.Count);
                    writer.Write(hasBasis ? FlagHasBasis : 0u);

                    for (int i = 0; i < frames.Count; i++)
                    {
                        MxAnimationBakedSocketFrame frame = frames[i];
                        writer.Write((uint)stringIndex[frame.SocketPath]);
                        WriteVector(writer, frame.Position);
                        WriteVector(writer, frame.DeltaPosition);
                    }

                    if (!hasBasis)
                        continue;
                    IReadOnlyList<CharacterSocketBakeBasisFrame> basis = track.BasisFrames;
                    for (int i = 0; i < basis.Count; i++)
                    {
                        WriteVector(writer, basis[i].Right);
                        WriteVector(writer, basis[i].Up);
                        WriteVector(writer, basis[i].Forward);
                    }
                }
            }

            byte[] payload = stream.ToArray();
            if (payload.LongLength > MaxBinaryBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "binary size " + payload.LongLength + " exceeds " + MaxBinaryBytes + ".");
            return payload;
        }

        /// <summary>
        /// Builds the v3 index for an already serialized payload. The hash and size come from
        /// <paramref name="binary"/> itself, so callers cannot declare an unverified hash or size.
        /// </summary>
        public static string SerializeIndex(string packageId, CharacterSocketBakeCatalog catalog, byte[] binary)
        {
            if (string.IsNullOrEmpty(packageId))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.IdentityMismatch,
                    "packageId is required to serialize a socket bake index.");
            if (binary == null || binary.Length < HeaderSize)
                throw Limit(CharacterSocketBakeDiagnosticCodes.PayloadMissing,
                    "binary payload is missing or truncated.");
            if (binary.LongLength > MaxBinaryBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "binary size " + binary.LongLength + " exceeds " + MaxBinaryBytes + ".");

            // Re-read the payload we are about to describe, so index and payload cannot disagree.
            CharacterSocketBakeCatalog decoded = Decode(binary, packageId, out int trackCount, out int frameCount, out int basisFrameCount);

            int catalogTrackCount = 0;
            int catalogFrameCount = 0;
            int catalogBasisFrameCount = 0;
            foreach (KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack> pair in catalog?.EnumerateTracks()
                ?? Array.Empty<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>>())
            {
                catalogTrackCount++;
                catalogFrameCount = checked(catalogFrameCount + pair.Value.Frames.Count);
                if (pair.Value.HasBasis)
                    catalogBasisFrameCount = checked(catalogBasisFrameCount + pair.Value.BasisFrames.Count);
            }
            if (decoded.TrackCount != catalogTrackCount
                || frameCount != catalogFrameCount
                || basisFrameCount != catalogBasisFrameCount)
            {
                throw Limit(CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "binary header counts do not match the catalog being described.");
            }

            return FormatIndexJson(new JObject
            {
                ["format"] = Format,
                ["packageId"] = packageId,
                ["binaryPath"] = DefaultBinaryRelativePath,
                ["binaryVersion"] = BinaryVersion,
                ["binaryHash"] = ComputeSha256(binary),
                ["binarySize"] = binary.LongLength,
                ["trackCount"] = trackCount,
                ["frameCount"] = frameCount,
                ["basisFrameCount"] = basisFrameCount
            });
        }

        /// <summary>
        /// Renders the index with a fixed LF newline. Newtonsoft indented output defaults to
        /// Environment.NewLine, which would make the index bytes - and therefore the receipt
        /// indexHash - differ between platforms for identical content.
        /// </summary>
        private static string FormatIndexJson(JObject index)
        {
            // Newtonsoft indented output uses Environment.NewLine, so normalise explicitly:
            // identical content must produce identical bytes (and therefore an identical indexHash)
            // on every platform.
            string json = index.ToString(Formatting.Indented).Replace("\r\n", "\n").Replace('\r', '\n');
            return json + "\n";
        }

        // ---------------------------------------------------------------- reading

        /// <summary>
        /// Strictly decodes a v3 index plus payload. Any error rejects the whole payload: the method
        /// returns false with <paramref name="catalog"/> null and never a partially loaded catalog.
        /// </summary>
        public static bool TryLoad(
            string indexJson,
            byte[] binary,
            string expectedPackageId,
            out CharacterSocketBakeCatalog catalog,
            out string[] issues)
        {
            var diagnostics = new List<string>();
            catalog = null;
            try
            {
                catalog = LoadCore(indexJson, binary, expectedPackageId, diagnostics);
            }
            catch (CharacterSocketBakeBinaryException ex)
            {
                diagnostics.Add(ex.Code + ": " + ex.Message);
                catalog = null;
            }
            catch (JsonException ex)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": index JSON is invalid: " + ex.Message);
                catalog = null;
            }
            catch (OverflowException ex)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.LimitExceeded + ": size arithmetic overflowed: " + ex.Message);
                catalog = null;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is DecoderFallbackException)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": " + ex.Message);
                catalog = null;
            }

            if (catalog == null && diagnostics.Count == 0)
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": socket bake payload could not be loaded.");
            issues = diagnostics.ToArray();
            return catalog != null;
        }

        /// <summary>True when the text looks like a legacy full JSON socket bake document.</summary>
        public static bool IsLegacyFormat(string indexJson)
        {
            if (string.IsNullOrEmpty(indexJson))
                return false;
            int probeLength = Math.Min(indexJson.Length, 4096);
            for (int i = 0; i < LegacyFormatMarkers.Length; i++)
            {
                if (indexJson.IndexOf(LegacyFormatMarkers[i], 0, probeLength, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            var builder = new StringBuilder("sha256:", 7 + hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static CharacterSocketBakeCatalog LoadCore(
            string indexJson,
            byte[] binary,
            string expectedPackageId,
            List<string> diagnostics)
        {
            if (string.IsNullOrEmpty(indexJson))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadMissing, "socket bake index is missing.");
            if (string.IsNullOrEmpty(expectedPackageId))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.IdentityMismatch, "an expected packageId is required.");
            if (binary == null || binary.Length == 0)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadMissing, "socket bake binary payload is missing.");

            JObject index;
            try
            {
                index = JObject.Parse(indexJson);
            }
            catch (JsonException)
            {
                if (IsLegacyFormat(indexJson))
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.ReimportRequired,
                        "the package still carries the legacy full socket bake JSON and must be re-imported.");
                throw;
            }

            string indexFormat = (string)index["format"];
            if (string.IsNullOrEmpty(indexFormat))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "socket bake index format is missing.");
            if (string.Equals(indexFormat, LegacyFormatMarkers[0], StringComparison.Ordinal)
                || string.Equals(indexFormat, LegacyFormatMarkers[1], StringComparison.Ordinal))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.ReimportRequired,
                    "the package carries socket bake format '" + indexFormat + "' and must be re-imported.");
            if (!string.Equals(indexFormat, Format, StringComparison.Ordinal))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.FormatUnsupported,
                    "socket bake index format '" + indexFormat + "' is not supported by this build.");

            int indexVersion = (int?)index["binaryVersion"] ?? -1;
            if (indexVersion != BinaryVersion)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.FormatUnsupported,
                    "socket bake binaryVersion " + indexVersion + " is not supported; this build reads version " + BinaryVersion + ".");

            string indexPackageId = (string)index["packageId"];
            if (!string.Equals(indexPackageId, expectedPackageId, StringComparison.Ordinal))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.IdentityMismatch,
                    "socket bake index packageId '" + indexPackageId + "' does not match expected '" + expectedPackageId + "'.");

            string binaryPath = (string)index["binaryPath"];
            if (!string.Equals(binaryPath, DefaultBinaryRelativePath, StringComparison.Ordinal))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "socket bake index binaryPath '" + binaryPath + "' is not the fixed package path.");

            long declaredSize = (long?)index["binarySize"] ?? -1L;
            if (declaredSize != binary.LongLength)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.HashMismatch,
                    "socket bake index declares binarySize " + declaredSize + " but the payload is " + binary.LongLength + " bytes.");

            string declaredHash = (string)index["binaryHash"];
            string actualHash = ComputeSha256(binary);
            if (!string.Equals(declaredHash, actualHash, StringComparison.Ordinal))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.HashMismatch,
                    "socket bake binary hash does not match the index declaration.");

            CharacterSocketBakeCatalog catalog = Decode(binary, expectedPackageId, out int trackCount, out int frameCount, out int basisFrameCount);

            int declaredTrackCount = (int?)index["trackCount"] ?? -1;
            int declaredFrameCount = (int?)index["frameCount"] ?? -1;
            int declaredBasisFrameCount = (int?)index["basisFrameCount"] ?? -1;
            if (declaredTrackCount != trackCount
                || declaredFrameCount != frameCount
                || declaredBasisFrameCount != basisFrameCount)
            {
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "socket bake index counts (tracks=" + declaredTrackCount + ", frames=" + declaredFrameCount
                    + ", basis=" + declaredBasisFrameCount + ") do not match the decoded payload (tracks="
                    + trackCount + ", frames=" + frameCount + ", basis=" + basisFrameCount + ").");
            }

            return catalog;
        }

        /// <summary>
        /// Decodes a payload that has already been authenticated against its index.
        /// </summary>
        private static CharacterSocketBakeCatalog Decode(
            byte[] binary,
            string expectedPackageId,
            out int trackCount,
            out int frameCount,
            out int basisFrameCount)
        {
            trackCount = 0;
            frameCount = 0;
            basisFrameCount = 0;
            if (binary == null || binary.LongLength < HeaderSize)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "socket bake binary is truncated before the header.");
            if (binary.LongLength > MaxBinaryBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "binary size " + binary.LongLength + " exceeds " + MaxBinaryBytes + ".");

            var catalog = new CharacterSocketBakeCatalog();
            using var stream = new MemoryStream(binary, writable: false);
            using var reader = new BinaryReader(stream, StrictUtf8, leaveOpen: true);

            if (reader.ReadUInt32() != MagicValue)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "socket bake binary magic is invalid.");
            if (reader.ReadUInt16() != BinaryVersion)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.FormatUnsupported, "socket bake binary version is not supported.");
            if (reader.ReadUInt16() != 0)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "socket bake binary reserved field must be zero.");

            uint packageIdStringIndex = reader.ReadUInt32();
            uint stringCount = reader.ReadUInt32();
            uint headerTrackCount = reader.ReadUInt32();
            uint headerFrameCount = reader.ReadUInt32();
            uint headerBasisFrameCount = reader.ReadUInt32();

            if (stringCount > MaxStringCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "string count " + stringCount + " exceeds " + MaxStringCount + ".");
            if (headerTrackCount > MaxTrackCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "track count " + headerTrackCount + " exceeds " + MaxTrackCount + ".");
            if (headerFrameCount > MaxTotalFrameCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "total frame count " + headerFrameCount + " exceeds " + MaxTotalFrameCount + ".");
            if (headerBasisFrameCount > headerFrameCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.BasisInvalid,
                    "basis frame count exceeds position frame count.");
            if (packageIdStringIndex >= stringCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "packageId string index is out of range.");
            if (Remaining(stream, binary) < (long)stringCount * sizeof(uint))
                throw Limit(CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "string table is truncated for the declared string count.");
            if (Remaining(stream, binary) < (long)headerTrackCount * TrackHeaderSize)
                throw Limit(CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "track section is truncated for the declared track count.");

            var strings = new string[(int)stringCount];
            int totalStringBytes = 0;
            for (int i = 0; i < strings.Length; i++)
                strings[i] = ReadString(reader, stream, binary, ref totalStringBytes);

            string packageId = strings[packageIdStringIndex];
            if (!string.Equals(packageId, expectedPackageId, StringComparison.Ordinal))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.IdentityMismatch,
                    "socket bake binary packageId '" + packageId + "' does not match expected '" + expectedPackageId + "'.");

            int decodedFrames = 0;
            int decodedBasisFrames = 0;
            string previousClipKey = null;
            int previousSocketId = 0;
            for (int trackIndex = 0; trackIndex < headerTrackCount; trackIndex++)
            {
                if (Remaining(stream, binary) < TrackHeaderSize)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "track header " + trackIndex + " is truncated.");

                uint clipKeyStringIndex = reader.ReadUInt32();
                int socketId = reader.ReadInt32();
                int quantizationScale = reader.ReadInt32();
                int basisQuantizationScale = reader.ReadInt32();
                uint trackFrameCount = reader.ReadUInt32();
                uint flags = reader.ReadUInt32();

                if (clipKeyStringIndex >= strings.Length)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.TrackInvalid, "clipKey string index is out of range.");
                if (socketId <= 0)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.TrackInvalid, "socket id must be positive.");
                if (quantizationScale <= 0)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.TrackInvalid, "quantization scale must be positive.");
                if ((flags & ~FlagHasBasis) != 0)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "track flags contain unknown bits.");
                if (trackFrameCount > MaxTrackFrameCount)
                    throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                        "track frame count " + trackFrameCount + " exceeds " + MaxTrackFrameCount + ".");

                bool hasBasis = (flags & FlagHasBasis) != 0;
                if (hasBasis && basisQuantizationScale <= 0)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.BasisInvalid, "basis quantization scale must be positive when basis is present.");
                if (!hasBasis && basisQuantizationScale != 0)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.BasisInvalid, "basis quantization scale must be zero without basis frames.");
                if (hasBasis && trackFrameCount == 0)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.BasisInvalid, "a track with basis must carry position frames.");

                string clipKey = strings[clipKeyStringIndex];
                if (previousClipKey != null)
                {
                    int order = string.CompareOrdinal(previousClipKey, clipKey);
                    if (order > 0 || (order == 0 && socketId <= previousSocketId))
                        throw new CharacterSocketBakeBinaryException(
                            CharacterSocketBakeDiagnosticCodes.TrackInvalid,
                            "tracks are not ordered by (clipKey, socketId) or contain a duplicate key.");
                }
                previousClipKey = clipKey;
                previousSocketId = socketId;

                long positionBytes = checked((long)trackFrameCount * PositionRecordSize);
                long basisBytes = hasBasis ? checked((long)trackFrameCount * BasisRecordSize) : 0L;
                long requiredBytes = checked(positionBytes + basisBytes);
                if (Remaining(stream, binary) < requiredBytes)
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                        "track " + clipKey + "/" + socketId + " is truncated.");

                decodedFrames = checked(decodedFrames + (int)trackFrameCount);
                if (decodedFrames > MaxTotalFrameCount)
                    throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                        "total frame count exceeds " + MaxTotalFrameCount + ".");
                if (hasBasis)
                    decodedBasisFrames = checked(decodedBasisFrames + (int)trackFrameCount);

                var frames = new MxAnimationBakedSocketFrame[(int)trackFrameCount];
                for (int i = 0; i < frames.Length; i++)
                {
                    uint socketPathIndex = reader.ReadUInt32();
                    if (socketPathIndex >= strings.Length)
                        throw new CharacterSocketBakeBinaryException(
                            CharacterSocketBakeDiagnosticCodes.TrackInvalid,
                            "socket path string index is out of range at " + clipKey + "/" + socketId + " frame " + i + ".");
                    MxAnimationBakedVector3 position = ReadVector(reader);
                    MxAnimationBakedVector3 delta = ReadVector(reader);
                    frames[i] = new MxAnimationBakedSocketFrame(i, socketId, strings[socketPathIndex], position, delta);
                }

                if (!hasBasis)
                {
                    catalog.RegisterTrack(clipKey, socketId, quantizationScale, frames);
                    continue;
                }

                var basisFrames = new CharacterSocketBakeBasisFrame[(int)trackFrameCount];
                for (int i = 0; i < basisFrames.Length; i++)
                {
                    MxAnimationBakedVector3 right = ReadVector(reader);
                    MxAnimationBakedVector3 up = ReadVector(reader);
                    MxAnimationBakedVector3 forward = ReadVector(reader);
                    ValidateBasis(right, up, forward, basisQuantizationScale, clipKey, socketId, i);
                    basisFrames[i] = new CharacterSocketBakeBasisFrame(i, right, up, forward);
                }
                catalog.RegisterTrack(clipKey, socketId, quantizationScale, frames, basisQuantizationScale, basisFrames);
            }

            if (Remaining(stream, binary) != 0)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "socket bake binary has trailing bytes after the last track.");
            if (decodedFrames != headerFrameCount)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "decoded frame count " + decodedFrames + " does not match the header value " + headerFrameCount + ".");
            if (decodedBasisFrames != headerBasisFrameCount)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "decoded basis frame count " + decodedBasisFrames + " does not match the header value " + headerBasisFrameCount + ".");

            trackCount = (int)headerTrackCount;
            frameCount = decodedFrames;
            basisFrameCount = decodedBasisFrames;
            return catalog;
        }

        private static void ValidateBasis(
            MxAnimationBakedVector3 right,
            MxAnimationBakedVector3 up,
            MxAnimationBakedVector3 forward,
            int quantizationScale,
            string clipKey,
            int socketId,
            int frame)
        {
            if (!CharacterSocketBakeBasisValidation.IsComponentInRange(right.X, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(right.Y, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(right.Z, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(up.X, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(up.Y, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(up.Z, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(forward.X, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(forward.Y, quantizationScale)
                || !CharacterSocketBakeBasisValidation.IsComponentInRange(forward.Z, quantizationScale))
            {
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.BasisInvalid,
                    "basis component range is invalid at " + clipKey + "/" + socketId + " frame " + frame + ".");
            }
            string issue = CharacterSocketBakeBasisValidation.ValidateFrame(right, up, forward, quantizationScale);
            if (!string.IsNullOrEmpty(issue))
            {
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.BasisInvalid,
                    "basis " + issue + " is invalid at " + clipKey + "/" + socketId + " frame " + frame + ".");
            }
        }

        private static void ValidateTracks(List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>> ordered)
        {
            for (int trackIndex = 0; trackIndex < ordered.Count; trackIndex++)
            {
                CharacterSocketBakeKey key = ordered[trackIndex].Key;
                CharacterSocketBakeTrack track = ordered[trackIndex].Value;
                if (string.IsNullOrWhiteSpace(key.ClipKey))
                    throw Track(key, "clipKey is required.");
                if (key.SocketId <= 0)
                    throw Track(key, "socket id must be positive.");
                if (track == null)
                    throw Track(key, "track payload is missing.");
                if (track.QuantizationScale <= 0)
                    throw Track(key, "quantization scale must be positive.");

                IReadOnlyList<MxAnimationBakedSocketFrame> frames = track.Frames;
                if (frames == null)
                    throw Track(key, "frame list is missing.");
                if (frames.Count > MaxTrackFrameCount)
                    throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                        "track " + key.ClipKey + "/" + key.SocketId + " frame count " + frames.Count + " exceeds " + MaxTrackFrameCount + ".");
                for (int i = 0; i < frames.Count; i++)
                {
                    MxAnimationBakedSocketFrame frame = frames[i];
                    if (frame == null)
                        throw Track(key, "frame " + i + " is empty.");
                    if (frame.LocalFrame != i)
                        throw Track(key, "frame index " + i + " is not contiguous (declares " + frame.LocalFrame + ").");
                    if (frame.SocketId != key.SocketId)
                        throw Track(key, "frame " + i + " socketId " + frame.SocketId + " does not match the track.");
                    if (frame.SocketPath == null)
                        throw Track(key, "frame " + i + " socket path is missing.");
                }

                IReadOnlyList<CharacterSocketBakeBasisFrame> basis = track.BasisFrames;
                if (!track.HasBasis)
                {
                    if (track.BasisQuantizationScale != 0)
                        throw Basis(key, "basis quantization scale must be zero without basis frames.");
                    if (basis != null && basis.Count != 0)
                        throw Basis(key, "basis frames are present without a quantization scale.");
                    continue;
                }

                if (track.BasisQuantizationScale <= 0)
                    throw Basis(key, "basis quantization scale must be positive.");
                if (basis == null || basis.Count != frames.Count)
                    throw Basis(key, "basis frame count does not match the position frame count.");
                for (int i = 0; i < basis.Count; i++)
                {
                    CharacterSocketBakeBasisFrame frame = basis[i];
                    if (frame == null)
                        throw Basis(key, "basis frame " + i + " is empty.");
                    if (frame.LocalFrame != i)
                        throw Basis(key, "basis frame index " + i + " is not contiguous (declares " + frame.LocalFrame + ").");
                    if (!CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Right.X, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Right.Y, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Right.Z, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Up.X, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Up.Y, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Up.Z, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Forward.X, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Forward.Y, track.BasisQuantizationScale)
                        || !CharacterSocketBakeBasisValidation.IsComponentInRange(frame.Forward.Z, track.BasisQuantizationScale))
                    {
                        throw Basis(key, "basis frame " + i + " component range is invalid.");
                    }
                    string issue = CharacterSocketBakeBasisValidation.ValidateFrame(
                        frame.Right, frame.Up, frame.Forward, track.BasisQuantizationScale);
                    if (!string.IsNullOrEmpty(issue))
                        throw Basis(key, "basis frame " + i + " " + issue + " is invalid.");
                }
            }
        }

        private static List<string> CollectStrings(
            string packageId,
            List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>> ordered)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal) { packageId };
            for (int i = 0; i < ordered.Count; i++)
            {
                KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack> pair = ordered[i];
                unique.Add(pair.Key.ClipKey);
                IReadOnlyList<MxAnimationBakedSocketFrame> frames = pair.Value.Frames;
                for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                    unique.Add(frames[frameIndex].SocketPath);
            }

            var strings = new List<string>(unique);
            strings.Sort(StringComparer.Ordinal);
            if (strings.Count > MaxStringCount)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "string count " + strings.Count + " exceeds " + MaxStringCount + ".");

            long contentBytes = 0;
            for (int i = 0; i < strings.Count; i++)
            {
                int byteCount;
                try
                {
                    byteCount = StrictUtf8.GetByteCount(strings[i]);
                }
                catch (EncoderFallbackException ex)
                {
                    throw new CharacterSocketBakeBinaryException(
                        CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                        "socket bake string cannot be encoded as strict UTF-8: " + ex.Message);
                }
                if (byteCount > MaxStringBytes)
                    throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                        "string '" + strings[i] + "' exceeds " + MaxStringBytes + " UTF-8 bytes.");
                contentBytes = checked(contentBytes + byteCount);
            }
            if (contentBytes > MaxStringContentBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "string content " + contentBytes + " bytes exceeds " + MaxStringContentBytes + ".");
            return strings;
        }

        private static List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>> OrderTracks(
            CharacterSocketBakeCatalog catalog)
        {
            var ordered = new List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>>();
            if (catalog != null)
                ordered.AddRange(catalog.EnumerateTracks());
            ordered.Sort((left, right) =>
            {
                int clip = string.CompareOrdinal(left.Key.ClipKey, right.Key.ClipKey);
                return clip != 0 ? clip : left.Key.SocketId.CompareTo(right.Key.SocketId);
            });
            return ordered;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value ?? string.Empty);
            if (bytes.Length > MaxStringBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "string exceeds " + MaxStringBytes + " UTF-8 bytes.");
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteVector(BinaryWriter writer, MxAnimationBakedVector3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        private static MxAnimationBakedVector3 ReadVector(BinaryReader reader)
        {
            long x = reader.ReadInt64();
            long y = reader.ReadInt64();
            long z = reader.ReadInt64();
            return new MxAnimationBakedVector3(x, y, z);
        }

        private static string ReadString(BinaryReader reader, Stream stream, byte[] binary, ref int totalStringBytes)
        {
            if (Remaining(stream, binary) < sizeof(uint))
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "string table is truncated.");
            uint byteCount = reader.ReadUInt32();
            if (byteCount > MaxStringBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "string length " + byteCount + " exceeds " + MaxStringBytes + " bytes.");
            if (Remaining(stream, binary) < byteCount)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "string payload is truncated.");
            byte[] bytes = reader.ReadBytes((int)byteCount);
            if (bytes.Length != (int)byteCount)
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "string payload is truncated.");
            totalStringBytes = checked(totalStringBytes + (int)byteCount);
            if (totalStringBytes > MaxStringContentBytes)
                throw Limit(CharacterSocketBakeDiagnosticCodes.LimitExceeded,
                    "string content exceeds " + MaxStringContentBytes + " bytes.");
            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid, "string is not valid UTF-8.");
            }
        }

        private static long Remaining(Stream stream, byte[] binary) => binary.LongLength - stream.Position;

        private static CharacterSocketBakeBinaryException Track(CharacterSocketBakeKey key, string message) =>
            new CharacterSocketBakeBinaryException(
                CharacterSocketBakeDiagnosticCodes.TrackInvalid,
                "track " + key.ClipKey + "/" + key.SocketId + ": " + message);

        private static CharacterSocketBakeBinaryException Basis(CharacterSocketBakeKey key, string message) =>
            new CharacterSocketBakeBinaryException(
                CharacterSocketBakeDiagnosticCodes.BasisInvalid,
                "track " + key.ClipKey + "/" + key.SocketId + ": " + message);

        private static CharacterSocketBakeBinaryException Limit(string code, string message) =>
            new CharacterSocketBakeBinaryException(code, message);
    }
}
