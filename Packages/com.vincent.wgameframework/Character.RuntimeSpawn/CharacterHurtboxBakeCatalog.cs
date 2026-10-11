using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    public enum CharacterHurtboxBakeShapeKind
    {
        Sphere = 0,
        Capsule = 1,
        Obb = 2
    }

    /// <summary>Quantized root-space hurtbox pose. Positions and sizes use millimetres; axes use one-millionth units.</summary>
    public readonly struct CharacterHurtboxBakePose : IEquatable<CharacterHurtboxBakePose>
    {
        public const int MillimetresPerMetre = 1000;
        public const int AxisQuantizationScale = 1000000;

        public CharacterHurtboxBakePose(
            CharacterHurtboxBakeShapeKind shapeKind,
            CharacterHurtboxBakeVector3 pointA,
            CharacterHurtboxBakeVector3 pointB,
            CharacterHurtboxBakeVector3 halfExtents,
            CharacterHurtboxBakeVector3 axisX,
            CharacterHurtboxBakeVector3 axisY,
            CharacterHurtboxBakeVector3 axisZ,
            long radius)
        {
            ShapeKind = shapeKind;
            PointA = pointA;
            PointB = pointB;
            HalfExtents = halfExtents;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            Radius = radius;
        }

        public CharacterHurtboxBakeShapeKind ShapeKind { get; }
        public CharacterHurtboxBakeVector3 PointA { get; }
        public CharacterHurtboxBakeVector3 PointB { get; }
        public CharacterHurtboxBakeVector3 HalfExtents { get; }
        public CharacterHurtboxBakeVector3 AxisX { get; }
        public CharacterHurtboxBakeVector3 AxisY { get; }
        public CharacterHurtboxBakeVector3 AxisZ { get; }
        public long Radius { get; }

        public bool Equals(CharacterHurtboxBakePose other) =>
            ShapeKind == other.ShapeKind && PointA.Equals(other.PointA) && PointB.Equals(other.PointB)
            && HalfExtents.Equals(other.HalfExtents) && AxisX.Equals(other.AxisX)
            && AxisY.Equals(other.AxisY) && AxisZ.Equals(other.AxisZ) && Radius == other.Radius;
        public override bool Equals(object obj) => obj is CharacterHurtboxBakePose other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)ShapeKind, PointA, PointB, HalfExtents, AxisX, AxisY, AxisZ, Radius);
    }

    public readonly struct CharacterHurtboxBakeVector3 : IEquatable<CharacterHurtboxBakeVector3>
    {
        public CharacterHurtboxBakeVector3(long x, long y, long z) { X = x; Y = y; Z = z; }
        public long X { get; }
        public long Y { get; }
        public long Z { get; }
        public bool Equals(CharacterHurtboxBakeVector3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is CharacterHurtboxBakeVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }

    public readonly struct CharacterHurtboxBakeRun
    {
        public CharacterHurtboxBakeRun(int startFrame, int frameCount, CharacterHurtboxBakePose pose)
        {
            if (startFrame < 0) throw new ArgumentOutOfRangeException(nameof(startFrame));
            if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
            StartFrame = startFrame;
            FrameCount = frameCount;
            Pose = pose;
        }
        public int StartFrame { get; }
        public int FrameCount { get; }
        public int EndFrameExclusive => StartFrame + FrameCount;
        public CharacterHurtboxBakePose Pose { get; }
    }

    public sealed class CharacterHurtboxBakeTrack
    {
        private readonly CharacterHurtboxBakeRun[] _runs;

        public CharacterHurtboxBakeTrack(int frameCount, CharacterHurtboxBakeRun[] runs)
        {
            if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
            FrameCount = frameCount;
            _runs = runs ?? Array.Empty<CharacterHurtboxBakeRun>();
            ValidateRuns(_runs, frameCount);
        }

        public int FrameCount { get; }
        public IReadOnlyList<CharacterHurtboxBakeRun> Runs => _runs;

        public bool TryGetPose(int localFrame, out CharacterHurtboxBakePose pose)
        {
            int low = 0;
            int high = _runs.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                CharacterHurtboxBakeRun run = _runs[middle];
                if (localFrame < run.StartFrame) high = middle - 1;
                else if (localFrame >= run.EndFrameExclusive) low = middle + 1;
                else { pose = run.Pose; return true; }
            }
            pose = default;
            return false;
        }

        public static CharacterHurtboxBakeTrack Compress(CharacterHurtboxBakePose[] frames)
        {
            frames ??= Array.Empty<CharacterHurtboxBakePose>();
            if (frames.Length == 0) return new CharacterHurtboxBakeTrack(0, Array.Empty<CharacterHurtboxBakeRun>());
            var runs = new List<CharacterHurtboxBakeRun>();
            int start = 0;
            CharacterHurtboxBakePose current = frames[0];
            for (int i = 1; i <= frames.Length; i++)
            {
                if (i < frames.Length && frames[i].Equals(current)) continue;
                runs.Add(new CharacterHurtboxBakeRun(start, i - start, current));
                if (i < frames.Length) { start = i; current = frames[i]; }
            }
            return new CharacterHurtboxBakeTrack(frames.Length, runs.ToArray());
        }

        private static void ValidateRuns(CharacterHurtboxBakeRun[] runs, int frameCount)
        {
            int expectedStart = 0;
            for (int i = 0; i < runs.Length; i++)
            {
                if (runs[i].StartFrame != expectedStart || runs[i].EndFrameExclusive > frameCount)
                    throw new ArgumentException("Hurtbox bake runs must be contiguous, sorted, and inside frameCount.", nameof(runs));
                expectedStart = runs[i].EndFrameExclusive;
            }
            if (expectedStart != frameCount)
                throw new ArgumentException("Hurtbox bake runs must cover every frame.", nameof(runs));
        }
    }

    public readonly struct CharacterHurtboxBakeKey : IEquatable<CharacterHurtboxBakeKey>
    {
        public CharacterHurtboxBakeKey(string clipKey, int colliderId) { ClipKey = clipKey ?? string.Empty; ColliderId = colliderId; }
        public string ClipKey { get; }
        public int ColliderId { get; }
        public bool Equals(CharacterHurtboxBakeKey other) => string.Equals(ClipKey, other.ClipKey, StringComparison.Ordinal) && (ColliderId == other.ColliderId);
        public override bool Equals(object obj) => obj is CharacterHurtboxBakeKey other && Equals(other);
        public override int GetHashCode() => unchecked((StringComparer.Ordinal.GetHashCode(ClipKey) * 397) ^ ColliderId);
    }

    public sealed class CharacterHurtboxBakeCatalog
    {
        private readonly Dictionary<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack> _tracks = new Dictionary<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack>();
        public int TrackCount => _tracks.Count;

        /// <summary>LowFreqAlloc: prepare after catalog load/replacement; Tick must retain the returned snapshot.</summary>
        public CharacterPreparedHurtboxClip PrepareClip(string sourceClipKey)
        {
            if (string.IsNullOrWhiteSpace(sourceClipKey)) return null;
            return new CharacterPreparedHurtboxClip(sourceClipKey, _tracks);
        }

        public void RegisterTrack(string clipKey, int colliderId, CharacterHurtboxBakeTrack track)
        {
            if (string.IsNullOrWhiteSpace(clipKey)) throw new ArgumentException("Clip key is required.", nameof(clipKey));
            if ((colliderId <= 0)) throw new ArgumentException("Collider id is required.", nameof(colliderId));
            _tracks[new CharacterHurtboxBakeKey(clipKey, colliderId)] = track ?? throw new ArgumentNullException(nameof(track));
        }
        public bool TryGetPose(string clipKey, int colliderId, int localFrame, out CharacterHurtboxBakePose pose)
        {
            if (_tracks.TryGetValue(new CharacterHurtboxBakeKey(clipKey, colliderId), out CharacterHurtboxBakeTrack track))
                return track.TryGetPose(localFrame, out pose);
            pose = default;
            return false;
        }
        public bool TryGetTrack(string clipKey, int colliderId, out CharacterHurtboxBakeTrack track) => _tracks.TryGetValue(new CharacterHurtboxBakeKey(clipKey, colliderId), out track);
        public IEnumerable<KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack>> EnumerateTracks() => _tracks;
    }

    public static class CharacterHurtboxBakeCatalogJson
    {
        public const string Format = "mx.character.hurtboxBakeCatalog.v3";
        public const string DefaultRelativePath = "generated/character_hurtbox_bake_catalog.json";

        public static string Serialize(string packageId, CharacterHurtboxBakeCatalog catalog)
        {
            var ordered = new List<KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack>>();
            if (catalog != null) ordered.AddRange(catalog.EnumerateTracks());
            ordered.Sort((left, right) => { int c = string.CompareOrdinal(left.Key.ClipKey, right.Key.ClipKey); return c != 0 ? c : left.Key.ColliderId.CompareTo(right.Key.ColliderId); });
            var tracks = new JArray();
            for (int i = 0; i < ordered.Count; i++)
            {
                var runs = new JArray();
                IReadOnlyList<CharacterHurtboxBakeRun> source = ordered[i].Value.Runs;
                for (int j = 0; j < source.Count; j++)
                {
                    CharacterHurtboxBakeRun run = source[j];
                    runs.Add(new JObject { ["startFrame"] = run.StartFrame, ["frameCount"] = run.FrameCount, ["pose"] = Pose(run.Pose) });
                }
                tracks.Add(new JObject { ["clipKey"] = ordered[i].Key.ClipKey, ["colliderId"] = ordered[i].Key.ColliderId, ["frameCount"] = ordered[i].Value.FrameCount, ["runs"] = runs });
            }
            return new JObject { ["format"] = Format, ["packageId"] = packageId ?? string.Empty, ["positionQuantization"] = CharacterHurtboxBakePose.MillimetresPerMetre, ["axisQuantization"] = CharacterHurtboxBakePose.AxisQuantizationScale, ["tracks"] = tracks }.ToString(Formatting.Indented) + Environment.NewLine;
        }

        private static int ReadColliderId(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer || (long)token <= 0 || (long)token > int.MaxValue)
                throw new InvalidDataException("Hurtbox collider identity must be a positive authored integer.");
            return (int)token;
        }

        public static CharacterHurtboxBakeCatalog Load(string json, out string[] issues)
        {
            var result = new CharacterHurtboxBakeCatalog();
            var diagnostics = new List<string>();
            JObject root;
            try { root = JObject.Parse(json ?? string.Empty); }
            catch (JsonException ex) { issues = new[] { "Hurtbox bake catalog JSON is invalid: " + ex.Message }; return result; }
            if (!string.Equals((string)root["format"], Format, StringComparison.Ordinal)) { issues = new[] { "Hurtbox bake catalog format is unsupported." }; return result; }
            JArray tracks = root["tracks"] as JArray;
            for (int i = 0; tracks != null && i < tracks.Count; i++)
            {
                JObject item = tracks[i] as JObject;
                string clipKey = (string)item?["clipKey"] ?? string.Empty;
                int colliderId = ReadColliderId(item?["colliderId"]);
                int frameCount = (int?)item?["frameCount"] ?? -1;
                try
                {
                    JArray sourceRuns = item?["runs"] as JArray;
                    var runs = new CharacterHurtboxBakeRun[sourceRuns?.Count ?? 0];
                    for (int j = 0; sourceRuns != null && j < sourceRuns.Count; j++)
                    {
                        JObject run = sourceRuns[j] as JObject;
                        runs[j] = new CharacterHurtboxBakeRun((int?)run?["startFrame"] ?? -1, (int?)run?["frameCount"] ?? 0, ReadPose(run?["pose"] as JObject));
                    }
                    result.RegisterTrack(clipKey, colliderId, new CharacterHurtboxBakeTrack(frameCount, runs));
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    diagnostics.Add("Hurtbox bake track " + clipKey + "/" + colliderId + " is invalid: " + ex.Message);
                }
            }
            issues = diagnostics.ToArray();
            return result;
        }

        private static JObject Pose(CharacterHurtboxBakePose value) => new JObject { ["shape"] = value.ShapeKind.ToString(), ["pointA"] = Vector(value.PointA), ["pointB"] = Vector(value.PointB), ["halfExtents"] = Vector(value.HalfExtents), ["axisX"] = Vector(value.AxisX), ["axisY"] = Vector(value.AxisY), ["axisZ"] = Vector(value.AxisZ), ["radius"] = value.Radius };
        private static CharacterHurtboxBakePose ReadPose(JObject value) => new CharacterHurtboxBakePose(Enum.TryParse((string)value?["shape"], true, out CharacterHurtboxBakeShapeKind shape) ? shape : CharacterHurtboxBakeShapeKind.Sphere, ReadVector(value?["pointA"] as JObject), ReadVector(value?["pointB"] as JObject), ReadVector(value?["halfExtents"] as JObject), ReadVector(value?["axisX"] as JObject), ReadVector(value?["axisY"] as JObject), ReadVector(value?["axisZ"] as JObject), (long?)value?["radius"] ?? 0L);
        private static JObject Vector(CharacterHurtboxBakeVector3 value) => new JObject { ["x"] = value.X, ["y"] = value.Y, ["z"] = value.Z };
        private static CharacterHurtboxBakeVector3 ReadVector(JObject value) => new CharacterHurtboxBakeVector3((long?)value?["x"] ?? 0L, (long?)value?["y"] ?? 0L, (long?)value?["z"] ?? 0L);
    }

    /// <summary>
    /// Compact v2 persistence for runtime hurtbox samples. The JSON file is an index only; the
    /// payload is delta/VarInt encoded and deflated. v1 JSON remains supported by
    /// <see cref="CharacterHurtboxBakeCatalogJson"/>.
    /// </summary>
    public static class CharacterHurtboxBakeCatalogBinary
    {
        public const string Format = "mx.character.hurtboxBakeCatalog.v4";
        public const string DefaultBinaryRelativePath = "generated/character_hurtbox_bake_catalog.bytes";
        private const uint Magic = 0x3242484d; // MHB2
        private const ushort Version = 4;

        public static string SerializeIndex(
            string packageId,
            CharacterHurtboxBakeCatalog catalog,
            string binaryRelativePath,
            string binaryHash,
            int binarySize)
        {
            int frameCount = 0;
            int runCount = 0;
            var colliderIds = new SortedSet<int>();
            foreach (KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack> pair in catalog?.EnumerateTracks()
                ?? Array.Empty<KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack>>())
            {
                frameCount += pair.Value.FrameCount;
                runCount += pair.Value.Runs.Count;
                colliderIds.Add(pair.Key.ColliderId);
            }

            return new JObject
            {
                ["format"] = Format,
                ["packageId"] = packageId ?? string.Empty,
                ["positionQuantization"] = CharacterHurtboxBakePose.MillimetresPerMetre,
                ["axisQuantization"] = CharacterHurtboxBakePose.AxisQuantizationScale,
                ["binaryPath"] = (binaryRelativePath ?? DefaultBinaryRelativePath).Replace('\\', '/'),
                ["binaryHash"] = binaryHash ?? string.Empty,
                ["binarySize"] = binarySize,
                ["trackCount"] = catalog?.TrackCount ?? 0,
                ["colliderCount"] = colliderIds.Count,
                ["frameCount"] = frameCount,
                ["runCount"] = runCount
            }.ToString(Formatting.Indented) + Environment.NewLine;
        }

        public static byte[] Serialize(CharacterHurtboxBakeCatalog catalog)
        {
            var ordered = new List<KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack>>();
            if (catalog != null) ordered.AddRange(catalog.EnumerateTracks());
            ordered.Sort((left, right) =>
            {
                int result = string.CompareOrdinal(left.Key.ClipKey, right.Key.ClipKey);
                return result != 0 ? result : left.Key.ColliderId.CompareTo(right.Key.ColliderId);
            });

            using var output = new MemoryStream();
            using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                using var payload = new MemoryStream();
                WriteUnsigned(payload, (ulong)ordered.Count);
                for (int i = 0; i < ordered.Count; i++)
                {
                    WriteString(payload, ordered[i].Key.ClipKey);
                    WriteUnsigned(payload, (ulong)ordered[i].Key.ColliderId);
                    CharacterHurtboxBakeTrack track = ordered[i].Value;
                    WriteUnsigned(payload, (ulong)track.FrameCount);
                    WriteUnsigned(payload, (ulong)track.Runs.Count);
                    CharacterHurtboxBakePose previous = default;
                    for (int runIndex = 0; runIndex < track.Runs.Count; runIndex++)
                    {
                        CharacterHurtboxBakeRun run = track.Runs[runIndex];
                        WriteUnsigned(payload, (ulong)run.FrameCount);
                        payload.WriteByte((byte)run.Pose.ShapeKind);
                        WritePoseDelta(payload, run.Pose, previous);
                        previous = run.Pose;
                    }
                }

                payload.Position = 0;
                using var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true);
                payload.CopyTo(deflate);
            }
            return output.ToArray();
        }

        public static CharacterHurtboxBakeCatalog Load(string indexJson, byte[] binary, out string[] issues)
        {
            var diagnostics = new List<string>();
            var catalog = new CharacterHurtboxBakeCatalog();
            try
            {
                JObject index = JObject.Parse(indexJson ?? string.Empty);
                if (!string.Equals((string)index["format"], Format, StringComparison.Ordinal))
                    throw new InvalidDataException("Hurtbox bake catalog v2 index format is unsupported.");
                if (binary == null || binary.Length < sizeof(uint) + sizeof(ushort))
                    throw new InvalidDataException("Hurtbox bake catalog v2 binary payload is missing or truncated.");

                using var source = new MemoryStream(binary, writable: false);
                using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
                if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version)
                    throw new InvalidDataException("Hurtbox bake catalog v2 binary header is invalid.");
                using var deflate = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: false);
                int trackCount = checked((int)ReadUnsigned(deflate));
                for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
                {
                    string clipKey = ReadString(deflate);
                    int colliderId = checked((int)ReadUnsigned(deflate));
                    int frameCount = checked((int)ReadUnsigned(deflate));
                    int runCount = checked((int)ReadUnsigned(deflate));
                    var runs = new CharacterHurtboxBakeRun[runCount];
                    CharacterHurtboxBakePose previous = default;
                    int startFrame = 0;
                    for (int runIndex = 0; runIndex < runCount; runIndex++)
                    {
                        int count = checked((int)ReadUnsigned(deflate));
                        int shapeValue = deflate.ReadByte();
                        if (shapeValue < 0) throw new EndOfStreamException();
                        CharacterHurtboxBakePose pose = ReadPoseDelta(
                            deflate,
                            (CharacterHurtboxBakeShapeKind)shapeValue,
                            previous);
                        runs[runIndex] = new CharacterHurtboxBakeRun(startFrame, count, pose);
                        startFrame += count;
                        previous = pose;
                    }
                    catalog.RegisterTrack(clipKey, colliderId, new CharacterHurtboxBakeTrack(frameCount, runs));
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is OverflowException || ex is ArgumentException)
            {
                diagnostics.Add("Hurtbox bake catalog v2 is invalid: " + ex.Message);
            }
            issues = diagnostics.ToArray();
            return catalog;
        }

        public static bool IsV2Index(string indexJson)
        {
            if (string.IsNullOrWhiteSpace(indexJson)) return false;
            try { return string.Equals((string)JObject.Parse(indexJson)["format"], Format, StringComparison.Ordinal); }
            catch (JsonException) { return false; }
        }

        public static string ReadBinaryRelativePath(string indexJson)
        {
            try { return (string)JObject.Parse(indexJson ?? string.Empty)["binaryPath"] ?? DefaultBinaryRelativePath; }
            catch (JsonException) { return DefaultBinaryRelativePath; }
        }

        private static void WritePoseDelta(Stream stream, CharacterHurtboxBakePose value, CharacterHurtboxBakePose previous)
        {
            WriteVectorDelta(stream, value.PointA, previous.PointA);
            WriteVectorDelta(stream, value.PointB, previous.PointB);
            WriteVectorDelta(stream, value.HalfExtents, previous.HalfExtents);
            WriteVectorDelta(stream, value.AxisX, previous.AxisX);
            WriteVectorDelta(stream, value.AxisY, previous.AxisY);
            WriteVectorDelta(stream, value.AxisZ, previous.AxisZ);
            WriteSigned(stream, value.Radius - previous.Radius);
        }

        private static CharacterHurtboxBakePose ReadPoseDelta(Stream stream, CharacterHurtboxBakeShapeKind shape, CharacterHurtboxBakePose previous)
        {
            CharacterHurtboxBakeVector3 pointA = ReadVectorDelta(stream, previous.PointA);
            CharacterHurtboxBakeVector3 pointB = ReadVectorDelta(stream, previous.PointB);
            CharacterHurtboxBakeVector3 halfExtents = ReadVectorDelta(stream, previous.HalfExtents);
            CharacterHurtboxBakeVector3 axisX = ReadVectorDelta(stream, previous.AxisX);
            CharacterHurtboxBakeVector3 axisY = ReadVectorDelta(stream, previous.AxisY);
            CharacterHurtboxBakeVector3 axisZ = ReadVectorDelta(stream, previous.AxisZ);
            long radius = previous.Radius + ReadSigned(stream);
            return new CharacterHurtboxBakePose(shape, pointA, pointB, halfExtents, axisX, axisY, axisZ, radius);
        }

        private static void WriteVectorDelta(Stream stream, CharacterHurtboxBakeVector3 value, CharacterHurtboxBakeVector3 previous)
        {
            WriteSigned(stream, value.X - previous.X);
            WriteSigned(stream, value.Y - previous.Y);
            WriteSigned(stream, value.Z - previous.Z);
        }

        private static CharacterHurtboxBakeVector3 ReadVectorDelta(Stream stream, CharacterHurtboxBakeVector3 previous) =>
            new CharacterHurtboxBakeVector3(previous.X + ReadSigned(stream), previous.Y + ReadSigned(stream), previous.Z + ReadSigned(stream));

        private static void WriteString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteUnsigned(stream, (ulong)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string ReadString(Stream stream)
        {
            int length = checked((int)ReadUnsigned(stream));
            var bytes = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(bytes, offset, length - offset);
                if (read <= 0) throw new EndOfStreamException();
                offset += read;
            }
            return Encoding.UTF8.GetString(bytes);
        }

        private static void WriteSigned(Stream stream, long value) =>
            WriteUnsigned(stream, unchecked((ulong)((value << 1) ^ (value >> 63))));

        private static long ReadSigned(Stream stream)
        {
            ulong value = ReadUnsigned(stream);
            return unchecked((long)(value >> 1) ^ -((long)value & 1L));
        }

        private static void WriteUnsigned(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        private static ulong ReadUnsigned(Stream stream)
        {
            ulong result = 0;
            int shift = 0;
            while (shift < 64)
            {
                int next = stream.ReadByte();
                if (next < 0) throw new EndOfStreamException();
                result |= (ulong)(next & 0x7f) << shift;
                if ((next & 0x80) == 0) return result;
                shift += 7;
            }
            throw new InvalidDataException("VarInt exceeds 64 bits.");
        }
    }
}
