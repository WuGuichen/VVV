using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    /// <summary>
    /// Socket bake catalog v3 codec tests. The expected byte layouts here are built by hand from the
    /// protocol definition so a writer/reader pair that shares the same mistake cannot pass.
    /// </summary>
    public sealed class CharacterSocketBakeBinaryCatalogTests
    {
        private const string PackageId = "000042";

        // ------------------------------------------------------------------ golden bytes

        [Test]
        public void Serialize_MatchesHandBuiltGoldenBytes()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack(
                "clip.a",
                7,
                1000,
                new[]
                {
                    Frame(0, 7, "bone.root", 1, 2, 3, 0, 0, 0),
                    Frame(1, 7, "bone.root", 4, 5, 6, 3, 3, 3)
                });

            byte[] actual = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
            byte[] expected = GoldenSingleTrackPayload();

            Assert.That(actual.Length, Is.EqualTo(expected.Length), "golden payload length");
            Assert.That(actual.Length, Is.EqualTo(189), "documented golden payload size");
            for (int i = 0; i < expected.Length; i++)
                Assert.That(actual[i], Is.EqualTo(expected[i]), "byte " + i);
        }

        [Test]
        public void GoldenBytes_AreAcceptedByTheStrictReader()
        {
            byte[] payload = GoldenSingleTrackPayload();
            string index = IndexJsonFor(PackageId, payload, 1, 2, 0);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out CharacterSocketBakeCatalog catalog, out string[] issues),
                Is.True,
                string.Join(" | ", issues));
            Assert.That(catalog.TrackCount, Is.EqualTo(1));
            Assert.That(catalog.TryGetFrame("clip.a", 7, 1, out MxAnimationBakedSocketFrame frame), Is.True);
            Assert.That(frame.LocalFrame, Is.EqualTo(1));
            Assert.That(frame.SocketId, Is.EqualTo(7));
            Assert.That(frame.SocketPath, Is.EqualTo("bone.root"));
            Assert.That(frame.Position.X, Is.EqualTo(4));
            Assert.That(frame.DeltaPosition.Z, Is.EqualTo(3));
        }

        // ------------------------------------------------------------------ round trip

        [Test]
        public void RoundTrip_PreservesEveryPositionDeltaPathAndScale()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack(
                "clip.attack",
                41,
                1000000,
                new[]
                {
                    Frame(0, 41, "bone.weapon", -123456789012345, 0, 987654321, 0, 0, 0),
                    Frame(1, 41, "bone.weapon", 2147483648L, -2147483649L, long.MaxValue, 7, -8, 9),
                    Frame(2, 41, "bone.weapon", long.MinValue, 1, -1, -1, 1, 0)
                });

            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
            string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out CharacterSocketBakeCatalog loaded, out string[] issues),
                Is.True,
                string.Join(" | ", issues));

            Assert.That(loaded.TrackCount, Is.EqualTo(1));
            Assert.That(loaded.TryGetTrack("clip.attack", 41, out CharacterSocketBakeTrack track), Is.True);
            Assert.That(track.QuantizationScale, Is.EqualTo(1000000));
            Assert.That(track.HasBasis, Is.False);
            Assert.That(track.Frames.Count, Is.EqualTo(3));

            Assert.That(track.Frames[0].Position.X, Is.EqualTo(-123456789012345L));
            Assert.That(track.Frames[0].Position.Y, Is.EqualTo(0L));
            Assert.That(track.Frames[0].Position.Z, Is.EqualTo(987654321L));
            Assert.That(track.Frames[1].Position.X, Is.EqualTo(2147483648L));
            Assert.That(track.Frames[1].Position.Y, Is.EqualTo(-2147483649L));
            Assert.That(track.Frames[1].Position.Z, Is.EqualTo(long.MaxValue));
            Assert.That(track.Frames[1].DeltaPosition.X, Is.EqualTo(7L));
            Assert.That(track.Frames[1].DeltaPosition.Y, Is.EqualTo(-8L));
            Assert.That(track.Frames[2].Position.X, Is.EqualTo(long.MinValue));
            Assert.That(track.Frames[2].DeltaPosition.Y, Is.EqualTo(1L));
            for (int i = 0; i < track.Frames.Count; i++)
            {
                Assert.That(track.Frames[i].LocalFrame, Is.EqualTo(i));
                Assert.That(track.Frames[i].SocketId, Is.EqualTo(41));
                Assert.That(track.Frames[i].SocketPath, Is.EqualTo("bone.weapon"));
            }
        }

        [Test]
        public void RoundTrip_PreservesBasisFramesExactly()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack(
                "clip.pose",
                113,
                1000000,
                new[] { Frame(0, 113, "Bone_Spine", 10, 20, 30, 0, 0, 0), Frame(1, 113, "Bone_Spine", 11, 21, 31, 1, 1, 1) },
                32767,
                new[] { Basis(0, 32767), Basis(1, 32767) });

            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
            string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out CharacterSocketBakeCatalog loaded, out string[] issues),
                Is.True,
                string.Join(" | ", issues));
            Assert.That(loaded.TryGetTrack("clip.pose", 113, out CharacterSocketBakeTrack track), Is.True);
            Assert.That(track.HasBasis, Is.True);
            Assert.That(track.BasisQuantizationScale, Is.EqualTo(32767));
            Assert.That(track.BasisFrames.Count, Is.EqualTo(2));
            Assert.That(track.BasisFrames[0].LocalFrame, Is.EqualTo(0));
            Assert.That(track.BasisFrames[1].Right.X, Is.EqualTo(32767L));
            Assert.That(track.BasisFrames[1].Up.Y, Is.EqualTo(32767L));
            Assert.That(track.BasisFrames[1].Forward.Z, Is.EqualTo(32767L));
            Assert.That(track.BasisFrames[0].Right, Is.EqualTo(new MxAnimationBakedVector3(32767, 0, 0)));
            Assert.That(track.BasisFrames[0].Up, Is.EqualTo(new MxAnimationBakedVector3(0, 32767, 0)));
            Assert.That(track.BasisFrames[0].Forward, Is.EqualTo(new MxAnimationBakedVector3(0, 0, 32767)));
        }

        [Test]
        public void RoundTrip_PreservesPerFrameSocketPaths()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack(
                "clip.mixed",
                3,
                1000,
                new[]
                {
                    Frame(0, 3, "bone.a", 1, 0, 0, 0, 0, 0),
                    Frame(1, 3, "bone.b", 2, 0, 0, 1, 0, 0),
                    Frame(2, 3, string.Empty, 3, 0, 0, 1, 0, 0),
                    Frame(3, 3, "bone.a", 4, 0, 0, 1, 0, 0)
                });

            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
            string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out CharacterSocketBakeCatalog loaded, out string[] issues),
                Is.True,
                string.Join(" | ", issues));
            Assert.That(loaded.TryGetTrack("clip.mixed", 3, out CharacterSocketBakeTrack track), Is.True);
            Assert.That(track.Frames[0].SocketPath, Is.EqualTo("bone.a"));
            Assert.That(track.Frames[1].SocketPath, Is.EqualTo("bone.b"));
            Assert.That(track.Frames[2].SocketPath, Is.EqualTo(string.Empty));
            Assert.That(track.Frames[3].SocketPath, Is.EqualTo("bone.a"));
        }

        [Test]
        public void EmptyCatalog_ProducesValidHeaderAndLoads()
        {
            var catalog = new CharacterSocketBakeCatalog();
            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);

            Assert.That(payload.Length, Is.EqualTo(28 + 4 + PackageId.Length), "header plus the packageId string only");
            Assert.That(BitConverter.ToUInt32(payload, 0), Is.EqualTo(CharacterSocketBakeCatalogBinary.MagicValue));
            Assert.That(BitConverter.ToUInt32(payload, 12), Is.EqualTo(1u), "string count");
            Assert.That(BitConverter.ToUInt32(payload, 16), Is.EqualTo(0u), "track count");

            string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out CharacterSocketBakeCatalog loaded, out string[] issues),
                Is.True,
                string.Join(" | ", issues));
            Assert.That(loaded.TrackCount, Is.EqualTo(0));
        }

        [Test]
        public void ZeroFrameTrack_IsAllowedButZeroFrameTrackWithBasisIsRejected()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.empty", 5, 1000, Array.Empty<MxAnimationBakedSocketFrame>());
            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
            string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out CharacterSocketBakeCatalog loaded, out string[] issues),
                Is.True,
                string.Join(" | ", issues));
            Assert.That(loaded.TrackCount, Is.EqualTo(1));

            var withBasis = new CharacterSocketBakeCatalog();
            withBasis.RegisterTrack(
                "clip.empty",
                5,
                1000,
                Array.Empty<MxAnimationBakedSocketFrame>(),
                32767,
                new[] { Basis(0, 32767) });
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(PackageId, withBasis));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.BasisInvalid));
        }

        [Test]
        public void RegistrationOrder_DoesNotAffectBytesOrIndex()
        {
            var forward = new CharacterSocketBakeCatalog();
            forward.RegisterTrack("clip.b", 9, 1000, new[] { Frame(0, 9, "p", 1, 1, 1, 0, 0, 0) });
            forward.RegisterTrack("clip.a", 2, 1000, new[] { Frame(0, 2, "p", 2, 2, 2, 0, 0, 0) });
            forward.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 3, 3, 3, 0, 0, 0) });

            var backward = new CharacterSocketBakeCatalog();
            backward.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 3, 3, 3, 0, 0, 0) });
            backward.RegisterTrack("clip.a", 2, 1000, new[] { Frame(0, 2, "p", 2, 2, 2, 0, 0, 0) });
            backward.RegisterTrack("clip.b", 9, 1000, new[] { Frame(0, 9, "p", 1, 1, 1, 0, 0, 0) });

            byte[] first = CharacterSocketBakeCatalogBinary.Serialize(PackageId, forward);
            byte[] second = CharacterSocketBakeCatalogBinary.Serialize(PackageId, backward);
            Assert.That(second, Is.EqualTo(first), "payload must be insertion-order independent");
            Assert.That(
                CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, backward, second),
                Is.EqualTo(CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, forward, first)),
                "index must be insertion-order independent");
        }

        [Test]
        public void Index_DeclaresFixedPathHashSizeAndCounts()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 1, 0, 0, 0, 0, 0) });
            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
            string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);

            Assert.That(index.IndexOf("\"format\": \"" + CharacterSocketBakeCatalogBinary.Format + "\"", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.IndexOf("\"binaryPath\": \"" + CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath + "\"", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.IndexOf("\"binaryHash\": \"" + CharacterSocketBakeCatalogBinary.ComputeSha256(payload) + "\"", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.IndexOf("\"binarySize\": " + payload.Length, StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.IndexOf("\"trackCount\": 1", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.IndexOf("\"frameCount\": 1", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.IndexOf("\"basisFrameCount\": 0", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.EndsWith("\n", StringComparison.Ordinal), Is.True);
            // The index must not depend on the host newline convention: CRLF would change indexHash per platform.
            Assert.That(index.IndexOf('\r'), Is.EqualTo(-1), "index must use LF newlines only");
            Assert.That(index.EndsWith("\n\n", StringComparison.Ordinal), Is.False, "index must end with exactly one LF");
            Assert.That(Encoding.UTF8.GetByteCount(index), Is.LessThanOrEqualTo(CharacterSocketBakeCatalogBinary.MaxIndexBytes));
        }

        // ------------------------------------------------------------------ writer rejection

        [Test]
        public void Serialize_RejectsNullFrame()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.a", 1, 1000, new MxAnimationBakedSocketFrame[] { Frame(0, 1, "p", 1, 0, 0, 0, 0, 0), null });
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.TrackInvalid));
        }

        [Test]
        public void Serialize_RejectsFrameSocketIdThatDoesNotMatchTheTrack()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 2, "p", 1, 0, 0, 0, 0, 0) });
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.TrackInvalid));
        }

        [Test]
        public void Serialize_RejectsNonContiguousFrameIndex()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 1, 0, 0, 0, 0, 0), Frame(2, 1, "p", 2, 0, 0, 1, 0, 0) });
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.TrackInvalid));
        }

        [Test]
        public void Serialize_RejectsBasisCountMismatch()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack(
                "clip.a",
                1,
                1000,
                new[] { Frame(0, 1, "p", 1, 0, 0, 0, 0, 0), Frame(1, 1, "p", 2, 0, 0, 1, 0, 0) },
                32767,
                new[] { Basis(0, 32767) });
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.BasisInvalid));
        }

        [Test]
        public void Serialize_RejectsNonOrthogonalAndNonUnitBasis()
        {
            var nonOrthogonal = new CharacterSocketBakeCatalog();
            nonOrthogonal.RegisterTrack(
                "clip.a",
                1,
                1000,
                new[] { Frame(0, 1, "p", 0, 0, 0, 0, 0, 0) },
                32767,
                new[]
                {
                    new CharacterSocketBakeBasisFrame(
                        0,
                        new MxAnimationBakedVector3(32767, 0, 0),
                        new MxAnimationBakedVector3(32767, 0, 0),
                        new MxAnimationBakedVector3(0, 0, 32767))
                });
            Assert.That(
                Assert.Throws<CharacterSocketBakeBinaryException>(() => CharacterSocketBakeCatalogBinary.Serialize(PackageId, nonOrthogonal)).Code,
                Is.EqualTo(CharacterSocketBakeDiagnosticCodes.BasisInvalid));

            var wrongHandedness = new CharacterSocketBakeCatalog();
            wrongHandedness.RegisterTrack(
                "clip.a",
                1,
                1000,
                new[] { Frame(0, 1, "p", 0, 0, 0, 0, 0, 0) },
                32767,
                new[]
                {
                    new CharacterSocketBakeBasisFrame(
                        0,
                        new MxAnimationBakedVector3(32767, 0, 0),
                        new MxAnimationBakedVector3(0, 32767, 0),
                        new MxAnimationBakedVector3(0, 0, -32767))
                });
            Assert.That(
                Assert.Throws<CharacterSocketBakeBinaryException>(() => CharacterSocketBakeCatalogBinary.Serialize(PackageId, wrongHandedness)).Code,
                Is.EqualTo(CharacterSocketBakeDiagnosticCodes.BasisInvalid));
        }

        [Test]
        public void Serialize_RejectsBasisScaleWithoutBasisFrames()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 0, 0, 0, 0, 0, 0) }, 32767, null);
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.BasisInvalid));
        }

        [Test]
        public void Serialize_RejectsEmptyPackageId()
        {
            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.Serialize(string.Empty, new CharacterSocketBakeCatalog()));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.IdentityMismatch));
        }

        [Test]
        public void SerializeIndex_RejectsCountsThatDisagreeWithThePayload()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 0, 0, 0, 0, 0, 0) });
            byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);

            var different = new CharacterSocketBakeCatalog();
            different.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 0, 0, 0, 0, 0, 0) });
            different.RegisterTrack("clip.b", 2, 1000, new[] { Frame(0, 2, "p", 0, 0, 0, 0, 0, 0) });

            CharacterSocketBakeBinaryException exception = Assert.Throws<CharacterSocketBakeBinaryException>(
                () => CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, different, payload));
            Assert.That(exception.Code, Is.EqualTo(CharacterSocketBakeDiagnosticCodes.PayloadInvalid));
        }

        // ------------------------------------------------------------------ reader rejection

        [Test]
        public void TryLoad_RejectsLegacyV2IndexWithReimportRequired()
        {
            string legacy = "{\"format\":\"mx.character.socketBakeCatalog.v2\",\"packageId\":\"" + PackageId + "\",\"tracks\":[]}";
            Assert.That(CharacterSocketBakeCatalogBinary.IsLegacyFormat(legacy), Is.True);
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(legacy, new byte[64], PackageId, out CharacterSocketBakeCatalog catalog, out string[] issues),
                Is.False);
            Assert.That(catalog, Is.Null);
            AssertCode(issues, CharacterSocketBakeDiagnosticCodes.ReimportRequired);
        }

        [Test]
        public void TryLoad_RejectsUnknownFormatAndVersionWithoutFallback()
        {
            byte[] payload = GoldenSingleTrackPayload();

            string future = IndexJsonFor(PackageId, payload, 1, 2, 0).Replace(
                CharacterSocketBakeCatalogBinary.Format, "mx.character.socketBakeCatalog.v9");
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(future, payload, PackageId, out _, out string[] futureIssues),
                Is.False);
            AssertCode(futureIssues, CharacterSocketBakeDiagnosticCodes.FormatUnsupported);

            string wrongVersion = IndexJsonFor(PackageId, payload, 1, 2, 0).Replace("\"binaryVersion\": 3", "\"binaryVersion\": 4");
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(wrongVersion, payload, PackageId, out _, out string[] versionIssues),
                Is.False);
            AssertCode(versionIssues, CharacterSocketBakeDiagnosticCodes.FormatUnsupported);
        }

        [Test]
        public void TryLoad_RejectsMissingPayloadAndIdentityMismatch()
        {
            byte[] payload = GoldenSingleTrackPayload();
            string index = IndexJsonFor(PackageId, payload, 1, 2, 0);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, null, PackageId, out _, out string[] missingIssues),
                Is.False);
            AssertCode(missingIssues, CharacterSocketBakeDiagnosticCodes.PayloadMissing);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(index, payload, "000043", out _, out string[] identityIssues),
                Is.False);
            AssertCode(identityIssues, CharacterSocketBakeDiagnosticCodes.IdentityMismatch);
        }

        [Test]
        public void TryLoad_RejectsWrongSizeHashMagicAndTrailingBytes()
        {
            byte[] payload = GoldenSingleTrackPayload();
            string index = IndexJsonFor(PackageId, payload, 1, 2, 0);

            string wrongSize = index.Replace("\"binarySize\": " + payload.Length, "\"binarySize\": " + (payload.Length + 1));
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(wrongSize, payload, PackageId, out _, out string[] sizeIssues), Is.False);
            AssertCode(sizeIssues, CharacterSocketBakeDiagnosticCodes.HashMismatch);

            string wrongHash = index.Replace(CharacterSocketBakeCatalogBinary.ComputeSha256(payload), "sha256:" + new string('0', 64));
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(wrongHash, payload, PackageId, out _, out string[] hashIssues), Is.False);
            AssertCode(hashIssues, CharacterSocketBakeDiagnosticCodes.HashMismatch);

            byte[] badMagic = (byte[])payload.Clone();
            badMagic[0] = 0x00;
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, badMagic, 1, 2, 0), badMagic, PackageId, out _, out string[] magicIssues),
                Is.False);
            AssertCode(magicIssues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);

            byte[] trailing = Append(payload, 0x00);
            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, trailing, 1, 2, 0), trailing, PackageId, out _, out string[] trailingIssues),
                Is.False);
            AssertCode(trailingIssues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);
        }

        [Test]
        public void TryLoad_RejectsTruncationEvenWhenEarlierTracksDecoded()
        {
            // Two valid tracks, then cut inside the last track's frames: the decoder must not
            // return the first track as a partial catalog.
            byte[] payload = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 2, 3, 0, 0, 0) }, null)
                .Track("clip.b", 2, 1000, 0, new[] { RF("p", 4, 5, 6, 1, 1, 1), RF("p", 7, 8, 9, 3, 3, 3) }, null)
                .Build();
            byte[] truncated = new byte[payload.Length - 10];
            Array.Copy(payload, truncated, truncated.Length);

            Assert.That(
                CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, truncated, 2, 3, 0), truncated, PackageId, out CharacterSocketBakeCatalog catalog, out string[] issues),
                Is.False);
            Assert.That(catalog, Is.Null, "a failed load must never expose a partially decoded catalog");
            AssertCode(issues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);
        }

        [Test]
        public void TryLoad_RejectsUnorderedAndDuplicateTrackKeys()
        {
            byte[] unordered = new PayloadBuilder(PackageId)
                .Track("clip.b", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, unordered, 2, 2, 0), unordered, PackageId, out _, out string[] unorderedIssues), Is.False);
            AssertCode(unorderedIssues, CharacterSocketBakeDiagnosticCodes.TrackInvalid);

            byte[] duplicate = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, duplicate, 2, 2, 0), duplicate, PackageId, out _, out string[] duplicateIssues), Is.False);
            AssertCode(duplicateIssues, CharacterSocketBakeDiagnosticCodes.TrackInvalid);
        }

        [Test]
        public void TryLoad_RejectsInvalidQuantizationAndBasisFlags()
        {
            byte[] zeroScale = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 0, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, zeroScale, 1, 1, 0), zeroScale, PackageId, out _, out string[] scaleIssues), Is.False);
            AssertCode(scaleIssues, CharacterSocketBakeDiagnosticCodes.TrackInvalid);

            byte[] unknownFlag = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 4, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, unknownFlag, 1, 1, 0), unknownFlag, PackageId, out _, out string[] flagIssues), Is.False);
            AssertCode(flagIssues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);

            byte[] basisOnZeroFrames = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 1, Array.Empty<RawFrame>(), 32767)
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, basisOnZeroFrames, 1, 0, 0), basisOnZeroFrames, PackageId, out _, out string[] basisIssues), Is.False);
            AssertCode(basisIssues, CharacterSocketBakeDiagnosticCodes.BasisInvalid);
        }

        [Test]
        public void TryLoad_RejectsBadStringIndexAndInvalidUtf8()
        {
            byte[] badIndex = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            // The socket path index lives at the first byte of the first frame record.
            int frameOffset = 28 + StringTableBytes(new[] { PackageId, "clip.a", "p" }) + 24;
            badIndex[frameOffset] = 99;
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, badIndex, 1, 1, 0), badIndex, PackageId, out _, out string[] indexIssues), Is.False);
            AssertCode(indexIssues, CharacterSocketBakeDiagnosticCodes.TrackInvalid);

            byte[] invalidUtf8 = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            int lastStringOffset = 28 + StringTableBytes(new[] { PackageId, "clip.a", "p" }) - 1; // final byte of "p"
            Assert.That(invalidUtf8[lastStringOffset], Is.EqualTo((byte)'p'));
            invalidUtf8[lastStringOffset] = 0xFF;
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, invalidUtf8, 1, 1, 0), invalidUtf8, PackageId, out _, out string[] utf8Issues), Is.False);
            AssertCode(utf8Issues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);
        }

        [Test]
        public void TryLoad_RejectsBasisViolations()
        {
            // Wrong basis scale with basis flag set.
            byte[] badScale = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 1, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, 0)
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, badScale, 1, 1, 1), badScale, PackageId, out _, out string[] scaleIssues), Is.False);
            AssertCode(scaleIssues, CharacterSocketBakeDiagnosticCodes.BasisInvalid);

            // Component outside +/- quantization scale.
            byte[] outOfRange = new PayloadBuilder(PackageId)
                .Track(
                    "clip.a",
                    1,
                    1000,
                    1,
                    new[] { RF("p", 1, 0, 0, 0, 0, 0) },
                    32767,
                    RawBasis(32767 * 4, 0, 0, 0, 32767, 0, 0, 0, 32767))
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, outOfRange, 1, 1, 1), outOfRange, PackageId, out _, out string[] rangeIssues), Is.False);
            AssertCode(rangeIssues, CharacterSocketBakeDiagnosticCodes.BasisInvalid);

            // Non-orthogonal basis inside the legal component range.
            byte[] nonOrthogonal = new PayloadBuilder(PackageId)
                .Track(
                    "clip.a",
                    1,
                    1000,
                    1,
                    new[] { RF("p", 1, 0, 0, 0, 0, 0) },
                    32767,
                    RawBasis(32767, 0, 0, 32767, 0, 0, 0, 0, 32767))
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, nonOrthogonal, 1, 1, 1), nonOrthogonal, PackageId, out _, out string[] orthogonalIssues), Is.False);
            AssertCode(orthogonalIssues, CharacterSocketBakeDiagnosticCodes.BasisInvalid);

            // Wrong handedness.
            byte[] wrongHandedness = new PayloadBuilder(PackageId)
                .Track(
                    "clip.a",
                    1,
                    1000,
                    1,
                    new[] { RF("p", 1, 0, 0, 0, 0, 0) },
                    32767,
                    RawBasis(32767, 0, 0, 0, 32767, 0, 0, 0, -32767))
                .Build();
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, wrongHandedness, 1, 1, 1), wrongHandedness, PackageId, out _, out string[] handedIssues), Is.False);
            AssertCode(handedIssues, CharacterSocketBakeDiagnosticCodes.BasisInvalid);
        }

        [Test]
        public void TryLoad_RejectsHeaderCountsThatDoNotMatchTheDecodedTracks()
        {
            byte[] payload = new PayloadBuilder(PackageId)
                .Track("clip.a", 1, 1000, 0, new[] { RF("p", 1, 0, 0, 0, 0, 0) }, null)
                .Build();
            // Header totalFrameCount claims 2 while the track carries 1.
            payload[20] = 2;
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(IndexJsonFor(PackageId, payload, 1, 1, 0), payload, PackageId, out _, out string[] issues), Is.False);
            AssertCode(issues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);
        }

        [Test]
        public void TryLoad_RejectsIndexCountsThatDoNotMatchThePayload()
        {
            byte[] payload = GoldenSingleTrackPayload();
            string index = IndexJsonFor(PackageId, payload, 1, 2, 0).Replace("\"frameCount\": 2", "\"frameCount\": 3");
            Assert.That(CharacterSocketBakeCatalogBinary.TryLoad(index, payload, PackageId, out _, out string[] issues), Is.False);
            AssertCode(issues, CharacterSocketBakeDiagnosticCodes.PayloadInvalid);
        }

        // ------------------------------------------------------------------ directory loader

        [Test]
        public void TryLoadFromDirectory_VerifiesThePairAndReportsEvidence()
        {
            string root = CreateFixtureDirectory();
            try
            {
                var catalog = new CharacterSocketBakeCatalog();
                catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 1, 2, 3, 0, 0, 0) });
                byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
                string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);
                WritePair(root, index, payload);

                Assert.That(
                    CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(root, PackageId, out CharacterSocketBakeCatalog loaded, out CharacterSocketBakeCatalogEvidence evidence, out string[] issues),
                    Is.True,
                    string.Join(" | ", issues));
                Assert.That(loaded.TrackCount, Is.EqualTo(1));
                Assert.That(evidence.Format, Is.EqualTo(CharacterSocketBakeCatalogBinary.Format));
                Assert.That(evidence.BinaryHash, Is.EqualTo(CharacterSocketBakeCatalogBinary.ComputeSha256(payload)));
                Assert.That(evidence.BinarySize, Is.EqualTo(payload.Length));
                Assert.That(evidence.TrackCount, Is.EqualTo(1));
                Assert.That(evidence.FrameCount, Is.EqualTo(1));
                Assert.That(evidence.BasisFrameCount, Is.EqualTo(0));
                Assert.That(evidence.IndexHash, Does.StartWith("sha256:"));
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Test]
        public void TryLoadFromDirectory_RejectsMissingFilesAndPairMismatch()
        {
            string root = CreateFixtureDirectory();
            try
            {
                Assert.That(
                    CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(root, PackageId, out _, out CharacterSocketBakeCatalogEvidence missingEvidence, out string[] missingIssues),
                    Is.False);
                AssertCode(missingIssues, CharacterSocketBakeDiagnosticCodes.PayloadMissing);
                Assert.That(missingEvidence.IsValid, Is.False, "failed loads must not expose evidence");

                var catalog = new CharacterSocketBakeCatalog();
                catalog.RegisterTrack("clip.a", 1, 1000, new[] { Frame(0, 1, "p", 1, 2, 3, 0, 0, 0) });
                byte[] payload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, catalog);
                string index = CharacterSocketBakeCatalogBinary.SerializeIndex(PackageId, catalog, payload);
                WritePair(root, index, payload);

                // Index from A, payload from B.
                var other = new CharacterSocketBakeCatalog();
                other.RegisterTrack("clip.b", 2, 1000, new[] { Frame(0, 2, "q", 9, 9, 9, 0, 0, 0) });
                byte[] otherPayload = CharacterSocketBakeCatalogBinary.Serialize(PackageId, other);
                File.WriteAllBytes(Path.Combine(root, "generated", "character_socket_bake_catalog.bytes"), otherPayload);
                Assert.That(
                    CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(root, PackageId, out _, out _, out string[] mismatchIssues),
                    Is.False);
                AssertCode(mismatchIssues, CharacterSocketBakeDiagnosticCodes.HashMismatch);

                // Missing payload only.
                File.Delete(Path.Combine(root, "generated", "character_socket_bake_catalog.bytes"));
                Assert.That(
                    CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(root, PackageId, out _, out _, out string[] payloadIssues),
                    Is.False);
                AssertCode(payloadIssues, CharacterSocketBakeDiagnosticCodes.PayloadMissing);
            }
            finally
            {
                Cleanup(root);
            }
        }

        [TestCase(0, false)]
        [TestCase(9000, false)]
        [TestCase(9000, true)]
        public void TryLoadFromDirectory_ReportsReimportRequiredForLegacyFullJson(int paddingBytes, bool hasStaleBinary)
        {
            string root = CreateFixtureDirectory();
            try
            {
                var legacy = new StringBuilder();
                legacy.Append("{\"format\":\"mx.character.socketBakeCatalog.v2\",\"packageId\":\"").Append(PackageId).Append("\",\"tracks\":[");
                legacy.Append(new string(' ', paddingBytes));
                legacy.Append("]}");
                WritePair(root, legacy.ToString(), new byte[] { 1, 2, 3 });
                if (!hasStaleBinary)
                    File.Delete(Path.Combine(root, "generated", "character_socket_bake_catalog.bytes"));

                Assert.That(
                    CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(root, PackageId,
                        out CharacterSocketBakeCatalog catalog, out CharacterSocketBakeCatalogEvidence evidence, out string[] issues),
                    Is.False);
                AssertCode(issues, CharacterSocketBakeDiagnosticCodes.ReimportRequired);
                Assert.That(catalog, Is.Null);
                Assert.That(evidence.IsValid, Is.False);
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Test]
        public void TryLoadFromDirectory_RejectsOversizedIndexThatIsNotLegacy()
        {
            string root = CreateFixtureDirectory();
            try
            {
                WritePair(root, "{\"format\":\"" + CharacterSocketBakeCatalogBinary.Format + "\",\"pad\":\"" + new string('x', 9000) + "\"}", new byte[] { 1, 2, 3 });
                Assert.That(
                    CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(root, PackageId, out _, out _, out string[] issues),
                    Is.False);
                AssertCode(issues, CharacterSocketBakeDiagnosticCodes.LimitExceeded);
            }
            finally
            {
                Cleanup(root);
            }
        }

        // ------------------------------------------------------------------ production scan gate

        [Test]
        public void ProductionSources_ContainNoLegacyFullJsonSocketBakeReader()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string[] scanRoots =
            {
                "Packages/com.vincent.wgameframework",
                "Assets/Scripts/WGameFrameworkProject"
            };
            string binaryCodec = "Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterSocketBakeCatalogBinary.cs";
            var typeOffenders = new List<string>();
            var v2Mentions = new List<string>();

            for (int i = 0; i < scanRoots.Length; i++)
            {
                string root = Path.Combine(projectRoot, scanRoots[i].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root))
                    continue;
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int f = 0; f < files.Length; f++)
                {
                    string normalized = files[f].Replace('\\', '/');
                    string relative = normalized.Substring(projectRoot.Replace('\\', '/').Length).TrimStart('/');
                    if (relative.Contains("/Tests/") || relative.Contains("/tests/"))
                        continue;
                    string text = File.ReadAllText(files[f]);
                    if (text.Contains("CharacterSocketBakeCatalogJson"))
                        typeOffenders.Add(relative);
                    if (text.Contains("mx.character.socketBakeCatalog.v2"))
                        v2Mentions.Add(relative);
                }
            }

            CollectionAssert.IsEmpty(
                typeOffenders,
                "production code must not reintroduce a full JSON socket bake reader: " + string.Join(", ", typeOffenders));
            CollectionAssert.AreEquivalent(
                new[] { binaryCodec },
                v2Mentions,
                "the v2 format marker may only appear in the binary codec's legacy detection list, but was found in: "
                + string.Join(", ", v2Mentions));
        }

        // ------------------------------------------------------------------ helpers

        private static byte[] GoldenSingleTrackPayload()
        {
            var bytes = new List<byte>();
            Le(bytes, CharacterSocketBakeCatalogBinary.MagicValue, 4);
            Le(bytes, 3, 2);
            Le(bytes, 0, 2);
            Le(bytes, 0, 4); // packageId string index
            Le(bytes, 3, 4); // string count
            Le(bytes, 1, 4); // track count
            Le(bytes, 2, 4); // total frame count
            Le(bytes, 0, 4); // total basis frame count

            Le(bytes, 6, 4);
            bytes.AddRange(Encoding.ASCII.GetBytes("000042"));
            Le(bytes, 9, 4);
            bytes.AddRange(Encoding.ASCII.GetBytes("bone.root"));
            Le(bytes, 6, 4);
            bytes.AddRange(Encoding.ASCII.GetBytes("clip.a"));

            Le(bytes, 2, 4);  // clipKey string index
            Le(bytes, 7, 4);  // socketId
            Le(bytes, 1000, 4); // quantization scale
            Le(bytes, 0, 4);  // basis quantization scale
            Le(bytes, 2, 4);  // frame count
            Le(bytes, 0, 4);  // flags

            Le(bytes, 1, 4);
            Le(bytes, 1, 8);
            Le(bytes, 2, 8);
            Le(bytes, 3, 8);
            Le(bytes, 0, 8);
            Le(bytes, 0, 8);
            Le(bytes, 0, 8);

            Le(bytes, 1, 4);
            Le(bytes, 4, 8);
            Le(bytes, 5, 8);
            Le(bytes, 6, 8);
            Le(bytes, 3, 8);
            Le(bytes, 3, 8);
            Le(bytes, 3, 8);
            return bytes.ToArray();
        }

        private static void Le(List<byte> bytes, long value, int byteCount)
        {
            for (int i = 0; i < byteCount; i++)
                bytes.Add((byte)((value >> (8 * i)) & 0xFF));
        }

        private static string IndexJsonFor(string packageId, byte[] payload, int trackCount, int frameCount, int basisFrameCount)
        {
            return "{\n"
                + "  \"format\": \"" + CharacterSocketBakeCatalogBinary.Format + "\",\n"
                + "  \"packageId\": \"" + packageId + "\",\n"
                + "  \"binaryPath\": \"" + CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath + "\",\n"
                + "  \"binaryVersion\": 3,\n"
                + "  \"binaryHash\": \"" + CharacterSocketBakeCatalogBinary.ComputeSha256(payload) + "\",\n"
                + "  \"binarySize\": " + payload.Length + ",\n"
                + "  \"trackCount\": " + trackCount + ",\n"
                + "  \"frameCount\": " + frameCount + ",\n"
                + "  \"basisFrameCount\": " + basisFrameCount + "\n"
                + "}\n";
        }

        private static int StringTableBytes(string[] strings)
        {
            int total = 0;
            for (int i = 0; i < strings.Length; i++)
                total += 4 + Encoding.UTF8.GetByteCount(strings[i]);
            return total;
        }

        private static byte[] Append(byte[] source, byte value)
        {
            var result = new byte[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = value;
            return result;
        }

        private static void AssertCode(string[] issues, string expectedCode)
        {
            Assert.That(issues, Is.Not.Empty, "a failure must report at least one diagnostic");
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i].StartsWith(expectedCode, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("expected diagnostic '" + expectedCode + "' but got: " + string.Join(" | ", issues));
        }

        private static MxAnimationBakedSocketFrame Frame(
            int localFrame,
            int socketId,
            string socketPath,
            long x,
            long y,
            long z,
            long dx,
            long dy,
            long dz)
        {
            return new MxAnimationBakedSocketFrame(
                localFrame,
                socketId,
                socketPath,
                new MxAnimationBakedVector3(x, y, z),
                new MxAnimationBakedVector3(dx, dy, dz));
        }

        private static CharacterSocketBakeBasisFrame Basis(int localFrame, int scale)
        {
            return new CharacterSocketBakeBasisFrame(
                localFrame,
                new MxAnimationBakedVector3(scale, 0, 0),
                new MxAnimationBakedVector3(0, scale, 0),
                new MxAnimationBakedVector3(0, 0, scale));
        }

        private static string CreateFixtureDirectory()
        {
            string root = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Temp",
                "CharacterSocketBakeMigration",
                "test-fixtures",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "generated"));
            return root;
        }

        private static void WritePair(string root, string index, byte[] payload)
        {
            Directory.CreateDirectory(Path.Combine(root, "generated"));
            File.WriteAllText(Path.Combine(root, "generated", "character_socket_bake_catalog.json"), index, new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(root, "generated", "character_socket_bake_catalog.bytes"), payload);
        }

        private static void Cleanup(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // Fixture cleanup is best effort; the directory lives under Temp/.
            }
        }

        // ------------------------------------------------------------------ raw payload builder

        private readonly struct RawFrame
        {
            public RawFrame(string socketPath, long x, long y, long z, long dx, long dy, long dz)
            {
                SocketPath = socketPath;
                X = x;
                Y = y;
                Z = z;
                Dx = dx;
                Dy = dy;
                Dz = dz;
            }

            public string SocketPath { get; }
            public long X { get; }
            public long Y { get; }
            public long Z { get; }
            public long Dx { get; }
            public long Dy { get; }
            public long Dz { get; }
        }

        private static RawFrame RF(string socketPath, long x, long y, long z, long dx, long dy, long dz) =>
            new RawFrame(socketPath, x, y, z, dx, dy, dz);

        private static long[] RawBasis(
            long rx, long ry, long rz,
            long ux, long uy, long uz,
            long fx, long fy, long fz) =>
            new[] { rx, ry, rz, ux, uy, uz, fx, fy, fz };

        /// <summary>
        /// Independent payload writer used to craft malformed inputs. It is deliberately separate
        /// from <see cref="CharacterSocketBakeCatalogBinary"/>.
        /// </summary>
        private sealed class PayloadBuilder
        {
            private readonly string _packageId;
            private readonly List<RawTrack> _tracks = new List<RawTrack>();

            public PayloadBuilder(string packageId)
            {
                _packageId = packageId;
            }

            public PayloadBuilder Track(string clipKey, int socketId, int quantizationScale, uint flags, RawFrame[] frames, int? basisScale, long[] basis = null)
            {
                _tracks.Add(new RawTrack(clipKey, socketId, quantizationScale, flags, frames ?? Array.Empty<RawFrame>(), basisScale, basis));
                return this;
            }

            public byte[] Build()
            {
                var strings = new SortedSet<string>(StringComparer.Ordinal) { _packageId };
                for (int i = 0; i < _tracks.Count; i++)
                {
                    strings.Add(_tracks[i].ClipKey);
                    for (int f = 0; f < _tracks[i].Frames.Length; f++)
                        strings.Add(_tracks[i].Frames[f].SocketPath);
                }
                var ordered = new List<string>(strings);
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < ordered.Count; i++)
                    index[ordered[i]] = i;

                int totalFrames = 0;
                int totalBasis = 0;
                for (int i = 0; i < _tracks.Count; i++)
                {
                    totalFrames += _tracks[i].Frames.Length;
                    if ((_tracks[i].Flags & 1u) != 0)
                        totalBasis += _tracks[i].Frames.Length;
                }

                var bytes = new List<byte>();
                Le(bytes, CharacterSocketBakeCatalogBinary.MagicValue, 4);
                Le(bytes, 3, 2);
                Le(bytes, 0, 2);
                Le(bytes, index[_packageId], 4);
                Le(bytes, ordered.Count, 4);
                Le(bytes, _tracks.Count, 4);
                Le(bytes, totalFrames, 4);
                Le(bytes, totalBasis, 4);

                for (int i = 0; i < ordered.Count; i++)
                {
                    byte[] raw = Encoding.UTF8.GetBytes(ordered[i]);
                    Le(bytes, raw.Length, 4);
                    bytes.AddRange(raw);
                }

                for (int i = 0; i < _tracks.Count; i++)
                {
                    RawTrack track = _tracks[i];
                    Le(bytes, index[track.ClipKey], 4);
                    Le(bytes, track.SocketId, 4);
                    Le(bytes, track.QuantizationScale, 4);
                    Le(bytes, track.BasisScale ?? 0, 4);
                    Le(bytes, track.Frames.Length, 4);
                    Le(bytes, track.Flags, 4);
                    for (int f = 0; f < track.Frames.Length; f++)
                    {
                        RawFrame frame = track.Frames[f];
                        Le(bytes, index[frame.SocketPath], 4);
                        Le(bytes, frame.X, 8);
                        Le(bytes, frame.Y, 8);
                        Le(bytes, frame.Z, 8);
                        Le(bytes, frame.Dx, 8);
                        Le(bytes, frame.Dy, 8);
                        Le(bytes, frame.Dz, 8);
                    }
                    if (track.Basis == null)
                        continue;
                    for (int b = 0; b < track.Basis.Length; b++)
                        Le(bytes, track.Basis[b], 8);
                }
                return bytes.ToArray();
            }

            private readonly struct RawTrack
            {
                public RawTrack(string clipKey, int socketId, int quantizationScale, uint flags, RawFrame[] frames, int? basisScale, long[] basis)
                {
                    ClipKey = clipKey;
                    SocketId = socketId;
                    QuantizationScale = quantizationScale;
                    Flags = flags;
                    Frames = frames;
                    BasisScale = basisScale;
                    Basis = basis;
                }

                public string ClipKey { get; }
                public int SocketId { get; }
                public int QuantizationScale { get; }
                public uint Flags { get; }
                public RawFrame[] Frames { get; }
                public int? BasisScale { get; }
                public long[] Basis { get; }
            }
        }
    }
}
