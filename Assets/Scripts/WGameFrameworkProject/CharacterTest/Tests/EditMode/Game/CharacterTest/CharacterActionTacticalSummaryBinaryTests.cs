using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterActionTacticalSummaryBinaryTests
    {
        [Test]
        public void PreviousSurfaceCacheDecodesWithoutChangingUnitsButStillRequiresRemeasure()
        {
            var original = Summary(2);
            var oldMeasurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.PreviousHurtboxSurfaceVersion,
                original.Measurement.StepMillimeters, original.Measurement.SearchMinMillimeters,
                original.Measurement.SearchMaxMillimeters, original.Measurement.ReferenceDummy);
            var old = new CharacterActionSpatialSummary(original.ActionId, original.ActionContentHash, original.ReferenceDummyHash,
                oldMeasurement, original.ProberStatus, original.Intervals.ToArray(), original.VerifiedOptimalMillimeters,
                original.IsLowerBoundOpen, original.IsUpperBoundOpen, original.Sector);
            byte[] bytes = CharacterActionTacticalSummaryBinary.Write(old);
            Assert.AreEqual(1, bytes[CharacterActionTacticalSummaryBinary.HeaderBytes], "v4 already used surface clearance.");
            var decoded = CharacterActionTacticalSummaryBinary.Read(bytes);
            Assert.AreEqual(old.ProberVersion, decoded.ProberVersion);
            Assert.AreEqual(CharacterActionTacticalSummaryJson.Write(old), CharacterActionTacticalSummaryJson.Write(decoded));
            StringAssert.Contains("HurtboxSurfaceClearance", CharacterActionTacticalSummaryJson.Write(old));
            Assert.AreEqual(old.ProberVersion, CharacterActionTacticalSummaryJson.Read(CharacterActionTacticalSummaryJson.Write(old)).ProberVersion);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateSummary(decoded, out _), "Decoding must not upgrade the old measurement into usable evidence.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RoundTripPreservesEveryField(int variant)
        {
            var original = Summary(variant);
            byte[] bytes = CharacterActionTacticalSummaryBinary.Write(original);
            var decoded = CharacterActionTacticalSummaryBinary.Read(bytes);
            Assert.AreEqual(CharacterActionTacticalSummaryJson.Write(original), CharacterActionTacticalSummaryJson.Write(decoded));
            CollectionAssert.AreEqual(bytes, CharacterActionTacticalSummaryBinary.Write(decoded));
        }

        [TestCase("magic")] [TestCase("version")] [TestCase("length")] [TestCase("checksum")]
        [TestCase("truncated")] [TestCase("trailing")] [TestCase("oversized")]
        public void RejectsInvalidEnvelope(string kind)
        {
            byte[] bytes = CharacterActionTacticalSummaryBinary.Write(Summary(1));
            switch (kind)
            {
                case "magic": bytes[0] ^= 1; break;
                case "version": bytes[4] = 127; break;
                case "length": WriteInt(bytes, 6, int.MaxValue); break;
                case "checksum": bytes[bytes.Length - 1] ^= 1; break;
                case "truncated": Array.Resize(ref bytes, bytes.Length - 1); break;
                case "trailing": Array.Resize(ref bytes, bytes.Length + 1); break;
                case "oversized": bytes = new byte[CharacterActionTacticalSummaryBinary.MaximumFileBytes + 1]; break;
            }
            Assert.Throws<InvalidDataException>(() => CharacterActionTacticalSummaryBinary.Read(bytes));
        }

        [TestCase("metric")] [TestCase("status")] [TestCase("intervals")]
        [TestCase("string")] [TestCase("utf8")] [TestCase("boolean")] [TestCase("sector")]
        public void ValidChecksumCannotHideInvalidPayload(string kind)
        {
            byte[] bytes = CharacterActionTacticalSummaryBinary.Write(Summary(1));
            var offsets = Offsets(bytes);
            switch (kind)
            {
                case "metric": bytes[10] = 9; break;
                case "status": bytes[offsets["status"]] = 255; break;
                case "intervals": WriteInt(bytes, offsets["intervals"], CharacterActionRangeProber.MaximumGridSamples + 1); break;
                case "string": bytes[15] = 255; bytes[16] = 255; break;
                case "utf8": bytes[17] = 255; break;
                case "boolean": bytes[offsets["boolean"]] = 2; break;
                case "sector": bytes[offsets["sector"]] = 255; break;
            }
            Rehash(bytes);
            Assert.Throws<InvalidDataException>(() => CharacterActionTacticalSummaryBinary.Read(bytes));
        }

        [Test]
        public void RejectsTrailingPayloadEvenWhenEnvelopeAndChecksumMatch()
        {
            byte[] original = CharacterActionTacticalSummaryBinary.Write(Summary(1));
            byte[] bytes = new byte[original.Length + 1];
            Array.Copy(original, bytes, original.Length - CharacterActionTacticalSummaryBinary.ChecksumBytes);
            WriteInt(bytes, 6, bytes.Length - CharacterActionTacticalSummaryBinary.HeaderBytes - CharacterActionTacticalSummaryBinary.ChecksumBytes);
            Rehash(bytes);
            Assert.Throws<InvalidDataException>(() => CharacterActionTacticalSummaryBinary.Read(bytes));
        }

        private static CharacterActionSpatialSummary Summary(int variant)
        {
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50,
                variant == 3 ? 100 : 0, 2000, new CharacterActionReferenceDummy(1, 1800, 300, -2, 900, 4));
            var intervals = variant == 0 ? Array.Empty<CharacterActionSpatialInterval>() : variant == 2
                ? new[] { new CharacterActionSpatialInterval(100, 200), new CharacterActionSpatialInterval(400, 600) }
                : new[] { new CharacterActionSpatialInterval(100, variant == 3 ? 2000 : 600) };
            return new CharacterActionSpatialSummary(new CharacterActionId(455), "action-hash", "dummy-hash", measurement,
                variant == 0 ? CharacterActionProberStatus.NoHit : variant == 2 ? CharacterActionProberStatus.Discontinuous
                : variant == 3 ? CharacterActionProberStatus.OpenBounded : CharacterActionProberStatus.Valid,
                intervals, variant == 0 || variant == 1 ? (int?)null : 450, variant == 3, variant == 3,
                new CharacterActionTacticalSector(-1000, 12000, CharacterActionSectorSource.Measured));
        }

        private static Dictionary<string, int> Offsets(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);
            stream.Position = 15;
            for (int i = 0; i < 3; i++) { int count = reader.ReadUInt16(); stream.Position += count; }
            stream.Position += 9 * 4;
            var offsets = new Dictionary<string, int> { ["status"] = (int)stream.Position };
            reader.ReadByte(); offsets["intervals"] = (int)stream.Position;
            int intervals = reader.ReadInt32(); stream.Position += intervals * 8;
            if (reader.ReadBoolean()) reader.ReadInt32();
            offsets["boolean"] = (int)stream.Position;
            stream.Position += 2 + 8; offsets["sector"] = (int)stream.Position;
            return offsets;
        }

        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            using var stream = new MemoryStream(bytes);
            using var writer = new BinaryWriter(stream);
            stream.Position = offset; writer.Write(value);
        }

        private static void Rehash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes, CharacterActionTacticalSummaryBinary.HeaderBytes,
                bytes.Length - CharacterActionTacticalSummaryBinary.HeaderBytes - CharacterActionTacticalSummaryBinary.ChecksumBytes);
            Array.Copy(hash, 0, bytes, bytes.Length - hash.Length, hash.Length);
        }
    }
}
