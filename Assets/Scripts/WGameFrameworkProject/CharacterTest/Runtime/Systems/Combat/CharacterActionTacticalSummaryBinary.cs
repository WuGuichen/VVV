using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Versioned, bounded, little-endian encoding for derived tactical summary caches.</summary>
    internal static class CharacterActionTacticalSummaryBinary
    {
        internal const int MaximumFileBytes = 1024 * 1024;
        internal const int HeaderBytes = 10;
        internal const int ChecksumBytes = 32;
        private const uint Magic = 0x4D535443; // CTSM
        private const ushort Version = 1;
        private const int MaximumStringBytes = 4096;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal static byte[] Write(CharacterActionSpatialSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (summary.Intervals.Count > CharacterActionRangeProber.MaximumGridSamples)
                throw Invalid("IntervalLimit");
            using var payloadStream = new MemoryStream();
            using (var writer = new BinaryWriter(payloadStream, Utf8, true))
            {
                writer.Write((byte)(CharacterActionRangeProber.UsesHurtboxSurfaceMetric(summary.ProberVersion) ? 1 : 0));
                writer.Write(summary.ActionId.Value);
                WriteText(writer, summary.ActionContentHash); WriteText(writer, summary.ReferenceDummyHash);
                WriteText(writer, summary.ProberVersion);
                var measurement = summary.Measurement;
                writer.Write(measurement.StepMillimeters); writer.Write(measurement.SearchMinMillimeters); writer.Write(measurement.SearchMaxMillimeters);
                var dummy = measurement.ReferenceDummy;
                writer.Write(dummy.Version); writer.Write(dummy.HeightMillimeters); writer.Write(dummy.RadiusMillimeters);
                writer.Write(dummy.CenterXMillimeters); writer.Write(dummy.CenterYMillimeters); writer.Write(dummy.CenterZMillimeters);
                writer.Write((byte)summary.ProberStatus);
                writer.Write(summary.Intervals.Count);
                foreach (var interval in summary.Intervals) { writer.Write(interval.FirstHitMillimeters); writer.Write(interval.LastHitMillimeters); }
                writer.Write(summary.VerifiedOptimalMillimeters.HasValue);
                if (summary.VerifiedOptimalMillimeters.HasValue) writer.Write(summary.VerifiedOptimalMillimeters.Value);
                writer.Write(summary.IsLowerBoundOpen); writer.Write(summary.IsUpperBoundOpen);
                writer.Write(summary.Sector.CenterMilliDegrees); writer.Write(summary.Sector.ToleranceMilliDegrees); writer.Write((byte)summary.Sector.Source);
            }
            byte[] payload = payloadStream.ToArray();
            if (payload.Length > MaximumFileBytes - HeaderBytes - ChecksumBytes) throw Invalid("SizeLimit");
            using var output = new MemoryStream();
            using (var writer = new BinaryWriter(output, Utf8, true))
            {
                writer.Write(Magic); writer.Write(Version); writer.Write(payload.Length); writer.Write(payload);
                using var sha = SHA256.Create(); writer.Write(sha.ComputeHash(payload));
            }
            return output.ToArray();
        }

        internal static CharacterActionSpatialSummary ReadFile(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MaximumFileBytes || stream.Length < HeaderBytes + ChecksumBytes) throw Invalid("SizeLimit");
            using var reader = new BinaryReader(stream);
            byte[] bytes = reader.ReadBytes((int)stream.Length);
            if (stream.Position != stream.Length) throw Invalid("FileChanged");
            return Read(bytes);
        }

        internal static CharacterActionSpatialSummary Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes + ChecksumBytes || bytes.Length > MaximumFileBytes) throw Invalid("SizeLimit");
            try
            {
                using var stream = new MemoryStream(bytes, false);
                using var reader = new BinaryReader(stream, Utf8, true);
                if (reader.ReadUInt32() != Magic) throw Invalid("Magic");
                if (reader.ReadUInt16() != Version) throw Invalid("Version");
                int length = reader.ReadInt32();
                if (length < 0 || length != bytes.Length - HeaderBytes - ChecksumBytes) throw Invalid("PayloadLength");
                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(bytes, HeaderBytes, length);
                for (int i = 0; i < ChecksumBytes; i++)
                    if (hash[i] != bytes[HeaderBytes + length + i]) throw Invalid("Checksum");
                using var payloadStream = new MemoryStream(bytes, HeaderBytes, length, false);
                using var payload = new BinaryReader(payloadStream, Utf8, true);
                byte metric = payload.ReadByte();
                int action = payload.ReadInt32();
                string actionHash = ReadText(payload), dummyHash = ReadText(payload), prober = ReadText(payload);
                if (metric != (CharacterActionRangeProber.UsesHurtboxSurfaceMetric(prober) ? 1 : 0)) throw Invalid("DistanceMetric");
                int step = payload.ReadInt32(), min = payload.ReadInt32(), max = payload.ReadInt32();
                var dummy = new CharacterActionReferenceDummy(payload.ReadInt32(), payload.ReadInt32(), payload.ReadInt32(),
                    payload.ReadInt32(), payload.ReadInt32(), payload.ReadInt32());
                var measurement = new CharacterActionSpatialMeasurement(prober, step, min, max, dummy);
                var status = (CharacterActionProberStatus)payload.ReadByte();
                if (!Enum.IsDefined(typeof(CharacterActionProberStatus), status)) throw Invalid("Status");
                int count = payload.ReadInt32();
                if (count < 0 || count > CharacterActionRangeProber.MaximumGridSamples || count > (payloadStream.Length - payloadStream.Position) / 8)
                    throw Invalid("IntervalLimit");
                var intervals = new CharacterActionSpatialInterval[count];
                for (int i = 0; i < count; i++) intervals[i] = new CharacterActionSpatialInterval(payload.ReadInt32(), payload.ReadInt32());
                int? optimal = ReadBool(payload) ? payload.ReadInt32() : (int?)null;
                bool lowerOpen = ReadBool(payload), upperOpen = ReadBool(payload);
                int center = payload.ReadInt32(), tolerance = payload.ReadInt32();
                var sectorSource = (CharacterActionSectorSource)payload.ReadByte();
                if (!Enum.IsDefined(typeof(CharacterActionSectorSource), sectorSource)) throw Invalid("SectorSource");
                var sector = new CharacterActionTacticalSector(center, tolerance, sectorSource);
                if (sector.CenterMilliDegrees != center || sector.ToleranceMilliDegrees != tolerance) throw Invalid("SectorValues");
                if (payloadStream.Position != payloadStream.Length) throw Invalid("TrailingPayload");
                return new CharacterActionSpatialSummary(new CharacterActionId(action), actionHash, dummyHash, measurement,
                    status, intervals, optimal, lowerOpen, upperOpen, sector);
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("SummaryBinary:Truncated", error); }
            catch (ArgumentException error) { throw new InvalidDataException("SummaryBinary:InvalidValue", error); }
            catch (OverflowException error) { throw new InvalidDataException("SummaryBinary:Overflow", error); }
        }

        private static void WriteText(BinaryWriter writer, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw Invalid("EmptyString");
            byte[] bytes = Utf8.GetBytes(value);
            if (bytes.Length > MaximumStringBytes) throw Invalid("StringLimit");
            writer.Write((ushort)bytes.Length); writer.Write(bytes);
        }

        private static string ReadText(BinaryReader reader)
        {
            int count = reader.ReadUInt16();
            if (count == 0 || count > MaximumStringBytes || count > reader.BaseStream.Length - reader.BaseStream.Position) throw Invalid("StringLimit");
            string value = Utf8.GetString(reader.ReadBytes(count));
            if (string.IsNullOrWhiteSpace(value)) throw Invalid("EmptyString");
            return value;
        }

        private static bool ReadBool(BinaryReader reader)
        {
            byte value = reader.ReadByte();
            if (value > 1) throw Invalid("Boolean");
            return value == 1;
        }

        private static InvalidDataException Invalid(string issue) => new InvalidDataException("SummaryBinary:" + issue);
    }
}
