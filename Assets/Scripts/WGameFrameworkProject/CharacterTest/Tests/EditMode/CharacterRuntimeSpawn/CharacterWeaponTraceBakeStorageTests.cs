using System;
using System.IO;
using System.Linq;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterWeaponTraceBakeStorageTests
    {
        private const string Clip = "test.clip";
        private const int Trace = 7, MainHandSocket = 12, LeftFootSocket = 13;
        private static readonly MxAnimationBakedVector3 A = new MxAnimationBakedVector3(long.MinValue, 1234567, long.MaxValue);
        private static readonly MxAnimationBakedVector3 B = new MxAnimationBakedVector3(long.MaxValue, -1234568, long.MinValue);

        private static CharacterWeaponTraceBakeCatalog Fixture()
        {
            var profile = new MxAnimationBakeProfile("test.profile", new ResourceKey(Clip, "AnimationClip"),
                "clip-hash", "rig", "rig-hash", 60, 1000000, MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest, "fingerprint");
            var frames = new[] {
                new MxAnimationBakedWeaponTraceFrame(0, Trace, MainHandSocket, A, B, B, A),
                new MxAnimationBakedWeaponTraceFrame(1, Trace, MainHandSocket, B, A, A, B),
                new MxAnimationBakedWeaponTraceFrame(20, Trace, MainHandSocket, A, A, B, B) };
            var sockets = new[] {
                new MxAnimationBakedSocketFrame(0, LeftFootSocket, "骨/左脚", A, B),
                new MxAnimationBakedSocketFrame(1, LeftFootSocket, "骨/左脚", B, new MxAnimationBakedVector3(1, -1, 1)),
                new MxAnimationBakedSocketFrame(20, LeftFootSocket, "changed/path", A, A) };
            var catalog = new CharacterWeaponTraceBakeCatalog();
            catalog.RegisterArtifact(Clip, Trace, new MxAnimationBakeArtifact(profile, frames, socketFrames: sockets));
            return catalog;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoundTrip_PreservesFullInt64RangeSparseFramesAndIndependentSocketDeltas(bool contacts)
        {
            var original = Fixture();
            byte[] bytes = CharacterWeaponTraceBakeStorage.Encode("p", original, contacts);
            CollectionAssert.AreEqual(bytes, CharacterWeaponTraceBakeStorage.Encode("p", original, contacts));
            var decoded = CharacterWeaponTraceBakeStorage.Decode(bytes, "p", contacts, out var report);
            Assert.IsTrue(report.Success, string.Join(";", report.Issues.Select(i => i.Message)));
            Assert.IsTrue(original.TryGetArtifact(Clip, Trace, out var a));
            Assert.IsTrue(decoded.TryGetArtifact(Clip, Trace, out var b));
            Assert.AreEqual(a.Profile.ProfileHash, b.Profile.ProfileHash);
            if (contacts)
            {
                Assert.AreEqual(0, b.WeaponTraceFrames.Count);
                for (int i = 0; i < a.SocketFrames.Count; i++)
                {
                    Assert.AreEqual(a.SocketFrames[i].LocalFrame, b.SocketFrames[i].LocalFrame);
                    Assert.AreEqual(a.SocketFrames[i].SocketPath, b.SocketFrames[i].SocketPath);
                    Assert.AreEqual(a.SocketFrames[i].Position, b.SocketFrames[i].Position);
                    Assert.AreEqual(a.SocketFrames[i].DeltaPosition, b.SocketFrames[i].DeltaPosition);
                }
            }
            else
            {
                Assert.AreEqual(0, b.SocketFrames.Count);
                foreach (var frame in a.WeaponTraceFrames)
                {
                    Assert.IsTrue(decoded.TryGetFrame(Clip, Trace, frame.LocalFrame, out var actual));
                    Assert.AreEqual(frame.RootPrev, actual.RootPrev); Assert.AreEqual(frame.TipPrev, actual.TipPrev);
                    Assert.AreEqual(frame.RootNow, actual.RootNow); Assert.AreEqual(frame.TipNow, actual.TipNow);
                }
                Assert.IsFalse(decoded.TryGetFrame(Clip, Trace, 2, out _));
                Assert.IsFalse(decoded.TryGetFrame(Clip, Trace, -1, out _));
            }
            Assert.AreEqual(MxAnimationBakeHasher.ComputeArtifactHash(b), b.ArtifactHash);
        }

        [TestCase("package")]
        [TestCase("kind")]
        [TestCase("version")]
        [TestCase("legacyVersion")]
        [TestCase("checksum")]
        [TestCase("truncated")]
        [TestCase("trailing")]
        [TestCase("oversized")]
        public void InvalidPayload_FailsClosed(string damage)
        {
            byte[] bytes = CharacterWeaponTraceBakeStorage.Encode("p", Fixture(), false);
            if (damage == "version") bytes[4] = 255;
            if (damage == "legacyVersion") bytes[4] = 2;
            if (damage == "checksum") bytes[12] ^= 1;
            if (damage == "truncated") Array.Resize(ref bytes, bytes.Length - 3);
            if (damage == "trailing") Array.Resize(ref bytes, bytes.Length + 3);
            if (damage == "oversized") Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, bytes, 6, 4);
            var catalog = CharacterWeaponTraceBakeStorage.Decode(bytes, damage == "package" ? "other" : "p",
                damage == "kind", out var report);
            Assert.IsFalse(report.Success); Assert.AreEqual(0, catalog.Count);
        }

        [Test]
        public void WindowSelection_PreservesPreviousEndpointAcrossGap_AndLookupAllocatesNothing()
        {
            var requests = new[] {
                new CharacterWeaponTraceBakeRequest(new CharacterActionId(1), Clip, Trace, 1, 1, 20),
                new CharacterWeaponTraceBakeRequest(new CharacterActionId(1), Clip, Trace, 20, 20, 20) };
            var selected = CharacterWeaponTraceBakeStorage.SelectWeaponWindows(Fixture(), requests);
            Assert.IsFalse(selected.TryGetFrame(Clip, Trace, 0, out _));
            Assert.IsTrue(selected.TryGetFrame(Clip, Trace, 1, out var first));
            Assert.AreEqual(B, first.RootPrev);
            for (int i = 0; i < 64; i++) selected.TryGetFrame(Clip, Trace, 20, out _);
            const Unity.Profiling.ProfilerRecorderOptions options = Unity.Profiling.ProfilerRecorderOptions.StartImmediately
                | Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using (var positive = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 1024, options))
            { GC.KeepAlive(new byte[8192]); positive.Stop(); Assert.Greater(positive.Count, 0, "The allocation recorder must detect a real allocation."); }
            int found = 0;
            using (var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 65536, options))
            {
                for (int i = 0; i < 10000; i++) if (selected.TryGetFrame(Clip, Trace, 20, out _)) found++;
                recorder.Stop(); Assert.AreEqual(0, recorder.Count);
            }
            Assert.AreEqual(10000, found);
        }

        [Test]
        public void IndexLoadsOnlyRequestedPayload_AndNeverFallsBackWhenItIsCorrupt()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-weapon-storage-" + Guid.NewGuid().ToString("N"));
            try
            {
                CharacterWeaponTraceBakeStorage.Write(directory, "p", Fixture(), Fixture());
                File.Delete(Path.Combine(directory, CharacterWeaponTraceBakeStorage.ContactPath));
                var weapons = CharacterWeaponTraceBakeStorage.Load(directory, "p", false, out var weaponReport);
                Assert.IsTrue(weaponReport.Success); Assert.AreEqual(1, weapons.Count);
                CharacterWeaponTraceBakeStorage.Load(directory, "p", true, out var contactReport);
                Assert.IsFalse(contactReport.Success);
                File.WriteAllBytes(Path.Combine(directory, CharacterWeaponTraceBakeStorage.WeaponPath), new byte[] { 0 });
                CharacterWeaponTraceBakeStorage.Load(directory, "p", false, out weaponReport);
                Assert.IsFalse(weaponReport.Success);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [TestCase("000001")]
        [TestCase("000002")]
        [TestCase("000004")]
        [TestCase("000008")]
        public void FormalMigratedPayloads_HaveConsistentNumericArtifactHashes(string packageId)
        {
            string directory = Path.Combine(UnityEngine.Application.dataPath,
                "MxFrameworkGenerated/CharacterPackages", packageId);
            foreach (bool contacts in new[] { false, true })
            {
                var catalog = CharacterWeaponTraceBakeStorage.Load(directory, packageId, contacts, out var report);
                Assert.IsTrue(report.Success, string.Join(";", report.Issues.Select(i => i.Message)));
                Assert.Greater(catalog.Count, 0);
                foreach (var pair in catalog.EnumerateArtifacts())
                {
                    Assert.Greater(pair.Key.TraceId, 0);
                    Assert.AreEqual(MxAnimationBakeHasher.ComputeArtifactHash(pair.Value), pair.Value.ArtifactHash,
                        packageId + "/" + pair.Key.ClipKey + (contacts ? "/contacts" : "/weapons"));
                }
            }
        }
    }
}
