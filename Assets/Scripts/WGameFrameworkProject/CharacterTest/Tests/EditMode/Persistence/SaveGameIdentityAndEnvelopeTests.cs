using System;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Persistence;
using NUnit.Framework;

namespace MxFramework.Tests.Persistence
{
    public sealed class SaveGameIdentityAndEnvelopeTests
    {
        [Test]
        public void DefaultEnvelopeResult_IsNotSuccess()
        {
            Assert.That(default(SaveGameEnvelopeResult).IsSuccess, Is.False);
        }

        [Test]
        public void ErrorAndPhaseEnumValues_AreStableAndProviderCodesAreAdditive()
        {
            Assert.That((int)SaveGameErrorCode.None, Is.EqualTo(0));
            Assert.That((int)SaveGameErrorCode.InvalidProfile, Is.EqualTo(1000));
            Assert.That((int)SaveGameErrorCode.InvalidSlot, Is.EqualTo(1001));
            Assert.That((int)SaveGameErrorCode.InvalidOperationId, Is.EqualTo(1002));
            Assert.That((int)SaveGameErrorCode.InvalidSelection, Is.EqualTo(1003));
            Assert.That((int)SaveGameErrorCode.Busy, Is.EqualTo(1004));
            Assert.That((int)SaveGameErrorCode.GenerationConflict, Is.EqualTo(1005));
            Assert.That((int)SaveGameErrorCode.GenerationExhausted, Is.EqualTo(1006));
            Assert.That((int)SaveGameErrorCode.SlotNotFound, Is.EqualTo(1007));
            Assert.That((int)SaveGameErrorCode.SlotDeleted, Is.EqualTo(1008));
            Assert.That((int)SaveGameErrorCode.GenerationNotFound, Is.EqualTo(1009));
            Assert.That((int)SaveGameErrorCode.LimitExceeded, Is.EqualTo(1010));
            Assert.That((int)SaveGameErrorCode.RuntimeCheckpointRejected, Is.EqualTo(1100));
            Assert.That((int)SaveGameErrorCode.InvalidTimestamp, Is.EqualTo(1200));
            Assert.That((int)SaveGameErrorCode.SerializeFailed, Is.EqualTo(1201));
            Assert.That((int)SaveGameErrorCode.RuntimeSerializationFailed, Is.EqualTo(1202));
            Assert.That((int)SaveGameErrorCode.StoreUnavailable, Is.EqualTo(1300));
            Assert.That((int)SaveGameErrorCode.FaultInjected, Is.EqualTo(1301));
            Assert.That((int)SaveGameErrorCode.InvalidLocation, Is.EqualTo(1302));
            Assert.That((int)SaveGameErrorCode.AtomicCommitUnsupported, Is.EqualTo(1303));
            Assert.That((int)SaveGameErrorCode.PermissionDenied, Is.EqualTo(1304));
            Assert.That((int)SaveGameErrorCode.QuotaExceeded, Is.EqualTo(1305));
            Assert.That((int)SaveGameErrorCode.IoFailure, Is.EqualTo(1306));
            Assert.That((int)SaveGameErrorCode.InvalidEnvelope, Is.EqualTo(1400));
            Assert.That((int)SaveGameErrorCode.IntegrityFailed, Is.EqualTo(1401));
            Assert.That((int)SaveGameErrorCode.UnsupportedEnvelopeVersion, Is.EqualTo(1402));
            Assert.That((int)SaveGameErrorCode.UnsupportedEnvelopeFeature, Is.EqualTo(1403));
            Assert.That((int)SaveGameErrorCode.UnsupportedTransform, Is.EqualTo(1404));
            Assert.That((int)SaveGameErrorCode.EnvelopePayloadMismatch, Is.EqualTo(1405));
            Assert.That((int)SaveGameErrorCode.RuntimeCompatibilityFailed, Is.EqualTo(1500));
            Assert.That((int)SaveGameErrorCode.CandidateRestoreFailed, Is.EqualTo(1600));
            Assert.That((int)SaveGameErrorCode.Timeout, Is.EqualTo(1700));

            Assert.That((int)SaveGameOperationPhase.None, Is.EqualTo(0));
            Assert.That((int)SaveGameOperationPhase.Validation, Is.EqualTo(1));
            Assert.That((int)SaveGameOperationPhase.Serialization, Is.EqualTo(2));
            Assert.That((int)SaveGameOperationPhase.Integrity, Is.EqualTo(3));
            Assert.That((int)SaveGameOperationPhase.Publication, Is.EqualTo(4));
            Assert.That((int)SaveGameOperationPhase.Read, Is.EqualTo(5));
            Assert.That((int)SaveGameOperationPhase.Listing, Is.EqualTo(6));
            Assert.That((int)SaveGameOperationPhase.Deletion, Is.EqualTo(7));
            Assert.That((int)SaveGameOperationPhase.Retention, Is.EqualTo(8));

            Assert.That((int)SaveGameOperationStatus.Pending, Is.EqualTo(0));
            Assert.That((int)SaveGameOperationStatus.Running, Is.EqualTo(1));
            Assert.That((int)SaveGameOperationStatus.Succeeded, Is.EqualTo(2));
            Assert.That((int)SaveGameOperationStatus.Failed, Is.EqualTo(3));
            Assert.That((int)SaveGameOperationStatus.Cancelled, Is.EqualTo(4));
            Assert.That((int)SaveGameOperationStatus.TimedOut, Is.EqualTo(5));

            Assert.That((int)SaveGameRecoveryPolicy.PrimaryOnly, Is.EqualTo(0));
            Assert.That((int)SaveGameRecoveryPolicy.PrimaryThenLastKnownGood, Is.EqualTo(1));

            Assert.That((int)SaveGameWarningCode.None, Is.EqualTo(0));
            Assert.That((int)SaveGameWarningCode.RetentionCleanupFailed, Is.EqualTo(1));
        }

        [Test]
        public void RetentionCleanupWarning_IsTypedLogicalAndNonFatal()
        {
            var warning = SaveGameWarning.RetentionCleanupFailed;
            Assert.That(warning.Code, Is.EqualTo(SaveGameWarningCode.RetentionCleanupFailed));
            Assert.That(warning.Phase, Is.EqualTo(SaveGameOperationPhase.Retention));
            Assert.That(warning.Path, Is.EqualTo("retention"));
            Assert.That(warning.IsNone, Is.False);
            Assert.That(SaveGameWarning.None.IsNone, Is.True);

            var commit = new SaveGameCommitResult(new SaveGameGeneration(2), warning);
            var delete = new SaveGameDeleteResult(new SaveGameGeneration(3), warning);
            Assert.That(commit.Generation.Value, Is.EqualTo(2UL));
            Assert.That(commit.Warning, Is.EqualTo(warning));
            Assert.That(delete.Generation.Value, Is.EqualTo(3UL));
            Assert.That(delete.Warning, Is.EqualTo(warning));
        }

        [Test]
        public void BuildDescriptorTryCreate_ReportsHardLimitWithoutThrowing()
        {
            bool created = SaveGameBuildDescriptor.TryCreate(
                new string('x', SaveGameLimits.MaximumDescriptorUtf8Bytes + 1),
                "build",
                "config",
                "catalog",
                string.Empty,
                out SaveGameBuildDescriptor descriptor,
                out SaveGameError error);

            Assert.That(created, Is.False);
            Assert.That(descriptor, Is.Null);
            Assert.That(error.Code, Is.EqualTo(SaveGameErrorCode.LimitExceeded));
        }

        private const string GoldenBase64 = "TVhTVkdNMDEBAAAAmQIAAAIAAAAAAAAAeyJmb3JtYXQiOiJteC5zYXZlLWdhbWUiLCJwcm9maWxlIjoicHJvZmlsZS1hIiwic2xvdCI6InNsb3QtMSIsImdlbmVyYXRpb24iOiIxIiwicmVjb3JkS2luZCI6InNhdmUiLCJjcmVhdGVkQXRVdGMiOiIyMDI2LTAxLTAyVDAzOjA0OjA1LjY3ODkwMTJaIiwidXBkYXRlZEF0VXRjIjoiMjAyNi0wMS0wMlQwMzowNDowNi4xMjM0NTY3WiIsImJ1aWxkIjp7ImZyYW1ld29ya1ZlcnNpb24iOiIxLjAuMCIsImJ1aWxkSWQiOiJidWlsZC03IiwiY29uZmlnVmVyc2lvbiI6ImNvbmZpZy0zIiwicmVzb3VyY2VDYXRhbG9nVmVyc2lvbiI6ImNhdGFsb2ctMiIsImNvbnRlbnRTZXRJZCI6IiJ9LCJjb21wYXRpYmlsaXR5VGFncyI6W3sia2V5IjoibW9kZSIsInZhbHVlIjoibm9ybWFsIn1dLCJwYXlsb2FkIjp7ImNvZGVjSWQiOiJydW50aW1lLXNhdmUtc3RhdGUtanNvbiIsInJ1bnRpbWVTY2hlbWFWZXJzaW9uIjozLCJydW50aW1lRnJhbWUiOiI0MiIsInVuY29tcHJlc3NlZExlbmd0aCI6Miwic3RvcmVkTGVuZ3RoIjoyLCJ0cmFuc2Zvcm1JZHMiOltdLCJzaGEyNTYiOiI0NDEzNmZhMzU1YjM2NzhhMTE0NmFkMTZmN2U4NjQ5ZTk0ZmI0ZmMyMWZlNzdlODMxMGMwNjBmNjFjYWFmZjhhIn0sImRpc3BsYXlNZXRhZGF0YSI6W3sia2V5IjoidGl0bGUiLCJ2YWx1ZSI6IkdvbGRlbiJ9XX17fdFNUUB1YYb4/+YLCj1trKJ6H4iBFdvO4RceiuT8PpIS";

        [TestCase("a", true)] [TestCase("A0._-z", true)] [TestCase("", false)] [TestCase(".slot", false)] [TestCase("../slot", false)]
        [TestCase("slot/name", false)] [TestCase("slot\\name", false)] [TestCase(" slot", false)] [TestCase("slot ", false)] [TestCase("é", false)]
        public void Identifiers_UseFrozenAsciiGrammar(string value, bool expected)
        {
            Assert.That(SaveGameSlotId.TryCreate(value, out _), Is.EqualTo(expected));
            Assert.That(SaveGameProfileId.TryCreate(value, out _), Is.EqualTo(expected));
            Assert.That(SaveGameOperationId.TryCreate(value, out _), Is.EqualTo(expected));
        }

        [Test]
        public void Identifier_LengthBoundary_IsExactAndDefaultInvalid()
        {
            Assert.That(SaveGameSlotId.TryCreate(new string('a', 64), out _), Is.True);
            Assert.That(SaveGameSlotId.TryCreate(new string('a', 65), out _), Is.False);
            Assert.That(default(SaveGameSlotId).IsValid, Is.False);
        }

        [Test]
        public void Generation_ZeroIsNoneAndMaximumCannotAdvance()
        {
            Assert.That(SaveGameGeneration.None.IsPublished, Is.False);
            Assert.That(SaveGameGeneration.None.TryNext(out var first), Is.True);
            Assert.That(first.Value, Is.EqualTo(1UL));
            Assert.That(new SaveGameGeneration(ulong.MaxValue).TryNext(out _), Is.False);
        }

        [Test]
        public void ExactV1GoldenVector_IsStableAndRoundTrips()
        {
            var encoded = SaveGameEnvelopeCodec.Encode(CreateGolden());
            Assert.That(encoded.IsSuccess, Is.True, encoded.Error.ToString());
            Assert.That(encoded.Encoded.Length, Is.EqualTo(723));
            Assert.That(Convert.ToBase64String(encoded.Encoded.ToArray()), Is.EqualTo(GoldenBase64));
            var decoded = SaveGameEnvelopeCodec.Decode(Convert.FromBase64String(GoldenBase64));
            Assert.That(decoded.IsSuccess, Is.True, decoded.Error.ToString());
            Assert.That(decoded.Envelope.Generation.Value, Is.EqualTo(1UL));
            Assert.That(Encoding.UTF8.GetString(decoded.Envelope.CopyPayload()), Is.EqualTo("{}"));
        }

        [Test]
        public void CreateSave_RuntimeSchemaZeroIsValidAndNegativeIsRejected()
        {
            var envelope = CreateSaveWithRuntimeSchema(0);

            Assert.That(envelope.RuntimeSchemaVersion, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSaveWithRuntimeSchema(-1));
        }

        [Test]
        public void TryCreateAndCodec_RuntimeSchemaZeroRoundTripsAndNegativeIsInvalid()
        {
            bool created = SaveGameEnvelope.TryCreateSave(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(1),
                DateTime.UnixEpoch, DateTime.UnixEpoch, Build(), Array.Empty<SaveGameKeyValue>(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 0, 0,
                Array.Empty<SaveGameKeyValue>(), new byte[] { 1 }, out var envelope, out var error);
            Assert.That(created, Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(SaveGameError.None));

            var decoded = SaveGameEnvelopeCodec.Decode(SaveGameEnvelopeCodec.Encode(envelope).Encoded);
            Assert.That(decoded.IsSuccess, Is.True, decoded.Error.ToString());
            Assert.That(decoded.Envelope.RuntimeSchemaVersion, Is.Zero);

            created = SaveGameEnvelope.TryCreateSave(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(1),
                DateTime.UnixEpoch, DateTime.UnixEpoch, Build(), Array.Empty<SaveGameKeyValue>(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId, -1, 0,
                Array.Empty<SaveGameKeyValue>(), new byte[] { 1 }, out _, out error);
            Assert.That(created, Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
        }

        [Test]
        public void Decode_RejectsTrailerAndPayloadIntegrityFaults()
        {
            var bytes = SaveGameEnvelopeCodec.Encode(CreateGolden()).Encoded.ToArray();
            bytes[bytes.Length - 1] ^= 1;
            Assert.That(SaveGameEnvelopeCodec.Decode(bytes).Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));

            bytes = SaveGameEnvelopeCodec.Encode(CreateGolden()).Encoded.ToArray();
            var headerLength = ReadUInt32(bytes, 12); bytes[24 + headerLength] ^= 1; RewriteTrailer(bytes);
            var payloadFault = SaveGameEnvelopeCodec.Decode(bytes);
            Assert.That(payloadFault.Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));
            Assert.That(payloadFault.Error.Path, Is.EqualTo("payload.sha256"));
        }

        [Test]
        public void Decode_RejectsUnknownHeaderPropertyEvenWithValidTrailer()
        {
            var bytes = SaveGameEnvelopeCodec.Encode(CreateGolden()).Encoded.ToArray();
            bytes[26] = (byte)'x'; RewriteTrailer(bytes);
            Assert.That(SaveGameEnvelopeCodec.Decode(bytes).Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
        }

        [Test]
        public void Tombstone_HasNoPayloadAndRoundTrips()
        {
            var timestamp = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            var tombstone = SaveGameEnvelope.CreateTombstone(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(2), timestamp, Build());
            var decoded = SaveGameEnvelopeCodec.Decode(SaveGameEnvelopeCodec.Encode(tombstone).Encoded);
            Assert.That(decoded.IsSuccess, Is.True, decoded.Error.ToString());
            Assert.That(decoded.Envelope.RecordKind, Is.EqualTo(SaveGameRecordKind.Tombstone));
            Assert.That(decoded.Envelope.RuntimeSchemaVersion, Is.Zero);
            Assert.That(decoded.Envelope.PayloadLength, Is.Zero);
        }

        [Test]
        public void TryCreateSave_ReportsLimitExceededWithoutThrowingAndOwnsInputs()
        {
            var payload = Encoding.UTF8.GetBytes("owned");
            var tags = new[] { new SaveGameKeyValue("key", "value") };
            bool created = SaveGameEnvelope.TryCreateSave(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(1),
                DateTime.UnixEpoch, DateTime.UnixEpoch, Build(), tags, SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 1, long.MaxValue,
                Array.Empty<SaveGameKeyValue>(), payload, out var envelope, out var error);
            Assert.That(created, Is.True, error.ToString());
            payload[0] = 0; tags[0] = new SaveGameKeyValue("changed", "changed");
            Assert.That(Encoding.UTF8.GetString(envelope.CopyPayload()), Is.EqualTo("owned"));
            Assert.That(envelope.GetCompatibilityTag(0).Key, Is.EqualTo("key"));
            var maxFrameRoundTrip = SaveGameEnvelopeCodec.Decode(SaveGameEnvelopeCodec.Encode(envelope).Encoded);
            Assert.That(maxFrameRoundTrip.IsSuccess, Is.True, maxFrameRoundTrip.Error.ToString());
            Assert.That(maxFrameRoundTrip.Envelope.RuntimeFrame, Is.EqualTo(long.MaxValue));

            created = SaveGameEnvelope.TryCreateSave(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(1),
                DateTime.UnixEpoch, DateTime.UnixEpoch, Build(), Array.Empty<SaveGameKeyValue>(), new string('x', 129), 1, 0,
                Array.Empty<SaveGameKeyValue>(), new byte[] { 1 }, out _, out error);
            Assert.That(created, Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveGameErrorCode.LimitExceeded));

            created = SaveGameEnvelope.TryCreateSave(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(1),
                DateTime.UnixEpoch, DateTime.UnixEpoch, Build(), new[] { default(SaveGameKeyValue) }, SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 1, 0,
                Array.Empty<SaveGameKeyValue>(), new byte[] { 1 }, out _, out error);
            Assert.That(created, Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
        }

        [Test]
        public void Decode_PreflightsHardLimitsBeforeFramingOrIntegrityWork()
        {
            var declaredHeaderTooLarge = new byte[56];
            Encoding.ASCII.GetBytes("MXSVGM01").CopyTo(declaredHeaderTooLarge, 0);
            declaredHeaderTooLarge[8] = 1;
            WriteUInt32(declaredHeaderTooLarge, 12, (uint)SaveGameLimits.MaximumHeaderBytes + 1U);
            Assert.That(SaveGameEnvelopeCodec.Decode(declaredHeaderTooLarge).Error.Code, Is.EqualTo(SaveGameErrorCode.LimitExceeded));

            var declaredPayloadTooLarge = new byte[56];
            Encoding.ASCII.GetBytes("MXSVGM01").CopyTo(declaredPayloadTooLarge, 0);
            declaredPayloadTooLarge[8] = 1;
            WriteUInt64(declaredPayloadTooLarge, 16, (ulong)SaveGameLimits.MaximumPayloadBytes + 1UL);
            Assert.That(SaveGameEnvelopeCodec.Decode(declaredPayloadTooLarge).Error.Code, Is.EqualTo(SaveGameErrorCode.LimitExceeded));

            var oversized = new byte[SaveGameLimits.MaximumEnvelopeBytes + 1];
            Assert.That(SaveGameEnvelopeCodec.Decode(oversized).Error.Code, Is.EqualTo(SaveGameErrorCode.LimitExceeded));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveGameEncodedEnvelope(oversized));
        }

        [Test]
        public void Decode_MapsNonEmptyTransformChainToUnsupportedTransform()
        {
            var bytes = SaveGameEnvelopeCodec.Encode(CreateGolden()).Encoded.ToArray();
            var transformed = ReplaceHeader(bytes, "\"transformIds\":[]", "\"transformIds\":[\"gzip\"]");
            var decoded = SaveGameEnvelopeCodec.Decode(transformed);
            Assert.That(decoded.Error.Code, Is.EqualTo(SaveGameErrorCode.UnsupportedTransform));
            Assert.That(decoded.Error.Path, Is.EqualTo("payload.transformIds"));
        }

        [Test]
        public void Decode_RejectsMalformedUtf8WithValidEnvelopeTrailer()
        {
            var bytes = SaveGameEnvelopeCodec.Encode(CreateGolden()).Encoded.ToArray();
            bytes[24] = 0xff;
            RewriteTrailer(bytes);
            Assert.That(SaveGameEnvelopeCodec.Decode(bytes).Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
        }

        private static SaveGameEnvelope CreateGolden()
        {
            return SaveGameEnvelope.CreateSave(new SaveGameProfileId("profile-a"), new SaveGameSlotId("slot-1"), new SaveGameGeneration(1),
                new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(6789012), new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc).AddTicks(1234567), Build(),
                new[] { new SaveGameKeyValue("mode", "normal") }, SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 3, 42,
                new[] { new SaveGameKeyValue("title", "Golden") }, Encoding.UTF8.GetBytes("{}"));
        }

        private static SaveGameEnvelope CreateSaveWithRuntimeSchema(int runtimeSchemaVersion)
        {
            return SaveGameEnvelope.CreateSave(new SaveGameProfileId("p"), new SaveGameSlotId("s"), new SaveGameGeneration(1),
                DateTime.UnixEpoch, DateTime.UnixEpoch, Build(), Array.Empty<SaveGameKeyValue>(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId,
                runtimeSchemaVersion, 0, Array.Empty<SaveGameKeyValue>(), new byte[] { 1 });
        }

        internal static SaveGameBuildDescriptor Build() => new SaveGameBuildDescriptor("1.0.0", "build-7", "config-3", "catalog-2", "");
        private static int ReadUInt32(byte[] bytes, int offset) => bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
        private static void WriteUInt32(byte[] bytes, int offset, uint value) { for (var index = 0; index < 4; index++) bytes[offset + index] = (byte)(value >> index * 8); }
        private static void WriteUInt64(byte[] bytes, int offset, ulong value) { for (var index = 0; index < 8; index++) bytes[offset + index] = (byte)(value >> index * 8); }
        private static void RewriteTrailer(byte[] bytes) { using (var sha = SHA256.Create()) { var hash = sha.ComputeHash(bytes, 0, bytes.Length - 32); Buffer.BlockCopy(hash, 0, bytes, bytes.Length - 32, 32); } }

        private static byte[] ReplaceHeader(byte[] source, string oldText, string newText)
        {
            var oldHeaderLength = ReadUInt32(source, 12);
            var payloadLength = source[16] | source[17] << 8;
            var header = Encoding.UTF8.GetString(source, 24, oldHeaderLength).Replace(oldText, newText);
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var result = new byte[24 + headerBytes.Length + payloadLength + 32];
            Buffer.BlockCopy(source, 0, result, 0, 24);
            WriteUInt32(result, 12, (uint)headerBytes.Length);
            Buffer.BlockCopy(headerBytes, 0, result, 24, headerBytes.Length);
            Buffer.BlockCopy(source, 24 + oldHeaderLength, result, 24 + headerBytes.Length, payloadLength);
            RewriteTrailer(result);
            return result;
        }
    }
}
