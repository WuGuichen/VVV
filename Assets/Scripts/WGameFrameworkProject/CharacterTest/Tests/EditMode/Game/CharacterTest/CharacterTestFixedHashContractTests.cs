using System;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Diagnostics;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestFixedHashContractTests
    {
        [Test]
        public void NormalizationIsIdempotentAndPreservesEveryOtherSetting()
        {
            var original = new NetworkSessionConfig(7, 30, 3, 4, "content", "resources", NetworkStateSyncPolicy.BootstrapOnly, 17);
            var normalized = CharacterTestSessionHashContract.Normalize(original);
            Assert.AreEqual("content" + CharacterTestSessionHashContract.Marker
                + CharacterTestSessionHashContract.RuntimeHashMarker
                + CharacterTestSessionHashContract.PlayableCatalogMarker, normalized.ConfigHash);
            Assert.AreEqual(normalized.ConfigHash, CharacterTestSessionHashContract.Normalize(normalized).ConfigHash);
            Assert.AreEqual(original.ProtocolVersion, normalized.ProtocolVersion);
            Assert.AreEqual(original.TickRate, normalized.TickRate);
            Assert.AreEqual(original.InputDelayFrames, normalized.InputDelayFrames);
            Assert.AreEqual(original.SnapshotIntervalFrames, normalized.SnapshotIntervalFrames);
            Assert.AreEqual(original.ResourceCatalogHash, normalized.ResourceCatalogHash);
            Assert.AreEqual(original.StateSyncPolicy, normalized.StateSyncPolicy);
            Assert.AreEqual(original.HeartbeatIntervalFrames, normalized.HeartbeatIntervalFrames);
            Assert.IsFalse(original.IsCompatibleWith(normalized, out _));
            var room = new NetworkRoomConfig("room", "name", 8, 3, false, true, original);
            var normalizedRoom = CharacterTestSessionHashContract.Normalize(room);
            Assert.AreEqual(normalized.ConfigHash, normalizedRoom.GameplaySessionConfig.ConfigHash);
            Assert.AreEqual(room.RoomId, normalizedRoom.RoomId);
            Assert.AreEqual(room.RoomName, normalizedRoom.RoomName);
            Assert.AreEqual(room.MaxMembers, normalizedRoom.MaxMembers);
            Assert.AreEqual(room.MinReadyMembers, normalizedRoom.MinReadyMembers);
            Assert.AreEqual(room.RequireAllReady, normalizedRoom.RequireAllReady);
            Assert.AreEqual(room.AllowLateJoin, normalizedRoom.AllowLateJoin);
        }

        [Test]
        public void ConflictingOrRepeatedMarkersAreRejected()
        {
            foreach (string value in new[] { "content|ct.session-hash=999:1001:1", "content|ct.session-hash=2:1001:999",
                "content" + CharacterTestSessionHashContract.Marker + CharacterTestSessionHashContract.Marker,
                "content" + CharacterTestSessionHashContract.Marker + "trailing",
                "content" + CharacterTestSessionHashContract.RuntimeHashMarker,
                "content" + CharacterTestSessionHashContract.RuntimeHashMarker + CharacterTestSessionHashContract.Marker,
                "content" + CharacterTestSessionHashContract.Marker + "|ct.runtime-hash=" + RuntimeHashAccumulator.EncodingVersion + ":999",
                "content" + CharacterTestSessionHashContract.Marker + CharacterTestSessionHashContract.RuntimeHashMarker + CharacterTestContractRuntimeHashMarkerWithWrongAlgorithm(),
                "content" + CharacterTestSessionHashContract.Marker + CharacterTestSessionHashContract.RuntimeHashMarker + CharacterTestSessionHashContract.RuntimeHashMarker,
                "content" + CharacterTestSessionHashContract.Marker + CharacterTestSessionHashContract.RuntimeHashMarker + "|ct.playable-catalog=999:sha256:bad",
                "content" + CharacterTestSessionHashContract.Marker + CharacterTestSessionHashContract.RuntimeHashMarker + CharacterTestSessionHashContract.PlayableCatalogMarker + CharacterTestSessionHashContract.PlayableCatalogMarker })
                Assert.Throws<ArgumentException>(() => CharacterTestSessionHashContract.Normalize(
                    new NetworkSessionConfig(1, 60, 2, 2, value, "resources")));
        }

        [Test]
        public void RuntimeHashMarkerCarriesAccumulatorEncodingAndAlgorithm()
        {
            Assert.AreEqual("|ct.runtime-hash=" + RuntimeHashAccumulator.EncodingVersion + ":" + RuntimeHashAccumulator.AlgorithmId,
                CharacterTestSessionHashContract.RuntimeHashMarker);
            var oldFixedMarker = new NetworkSessionConfig(1, 60, 2, 2,
                "content" + CharacterTestSessionHashContract.Marker, "resources");
            var normalized = CharacterTestSessionHashContract.Normalize(oldFixedMarker);
            Assert.AreEqual(oldFixedMarker.ConfigHash + CharacterTestSessionHashContract.RuntimeHashMarker
                + CharacterTestSessionHashContract.PlayableCatalogMarker, normalized.ConfigHash);
            Assert.AreEqual(normalized.ConfigHash, CharacterTestSessionHashContract.Normalize(normalized).ConfigHash);
        }

        [Test]
        public void NonEmptyResourceCatalogBaseIsPreservedWhilePlayableMarkerIsNormalized()
        {
            var original = new NetworkSessionConfig(1, 60, 0, 1, "base-config", "custom-directory-catalog");
            var normalized = CharacterTestSessionHashContract.Normalize(original);
            Assert.AreEqual(original.ResourceCatalogHash, normalized.ResourceCatalogHash);
            Assert.AreEqual(normalized.ConfigHash, CharacterTestSessionHashContract.Normalize(normalized).ConfigHash);
            StringAssert.EndsWith(CharacterTestSessionHashContract.PlayableCatalogMarker, normalized.ConfigHash);
        }

        [Test]
        public void LegacySessionAndRuntimeMarkersUpgradeToTheCompleteCatalogMarkerIdempotently()
        {
            string legacyHash = "content" + CharacterTestSessionHashContract.Marker
                + CharacterTestSessionHashContract.RuntimeHashMarker;
            var normalized = CharacterTestSessionHashContract.Normalize(
                new NetworkSessionConfig(1, 60, 0, 1, legacyHash, "custom-directory-catalog"));
            string expected = legacyHash + CharacterTestSessionHashContract.PlayableCatalogMarker;
            Assert.AreEqual(expected, normalized.ConfigHash);
            Assert.AreEqual(expected, CharacterTestSessionHashContract.Normalize(normalized).ConfigHash);
        }

        [Test]
        public void ProductionServerRejectsOldPeerBeforeSnapshotSynchronization()
        {
            var oldConfig = NetworkSessionConfig.Default;
            var transport = new LoopbackNetworkTransport();
            using var server = new CharacterTestUnityTransportServerRunner(transport, NullRuntimeLogger.Instance,
                config: oldConfig, sessionId: "hash-test", serverPeerId: "server", createServerViews: false, disposeTransport: false);
            var client = new NetworkSessionRunner(NetworkSessionRole.Client, "hash-test", "old", "server", oldConfig, transport, null);
            var snapshots = new SnapshotProbe(server.Slice.Services.Network.CreateSnapshotParticipant().ParticipantId);
            client.RegisterParticipant(snapshots);
            client.Connect(RuntimeFrame.Zero);
            server.Session.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Rejected, client.Status);
            StringAssert.Contains("config hash", client.RejectionReason);
            Assert.IsFalse(server.Session.IsPeerConnected("old"));
            Assert.AreEqual(0, snapshots.Applied);
        }

        [Test]
        public void ProductionServerRejectsPeerWithDifferentRuntimeHashAlgorithmBeforeSnapshotSynchronization()
        {
            var serverConfig = CharacterTestSessionHashContract.Normalize(NetworkSessionConfig.Default);
            int incompatibleAlgorithm = RuntimeHashAccumulator.AlgorithmId == 1 ? 2 : 1;
            string incompatibleHash = serverConfig.ConfigHash.Replace(
                CharacterTestSessionHashContract.RuntimeHashMarker,
                "|ct.runtime-hash=" + RuntimeHashAccumulator.EncodingVersion + ":" + incompatibleAlgorithm);
            var clientConfig = new NetworkSessionConfig(serverConfig.ProtocolVersion, serverConfig.TickRate,
                serverConfig.InputDelayFrames, serverConfig.SnapshotIntervalFrames, incompatibleHash,
                serverConfig.ResourceCatalogHash, serverConfig.StateSyncPolicy, serverConfig.HeartbeatIntervalFrames);
            var transport = new LoopbackNetworkTransport();
            using var server = new CharacterTestUnityTransportServerRunner(transport, NullRuntimeLogger.Instance,
                config: serverConfig, sessionId: "hash-algorithm-test", serverPeerId: "server", createServerViews: false, disposeTransport: false);
            var client = new NetworkSessionRunner(NetworkSessionRole.Client, "hash-algorithm-test", "wrong-algorithm",
                "server", clientConfig, transport, null);
            var snapshots = new SnapshotProbe(server.Slice.Services.Network.CreateSnapshotParticipant().ParticipantId);
            client.RegisterParticipant(snapshots);
            client.Connect(RuntimeFrame.Zero);
            server.Session.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Rejected, client.Status);
            StringAssert.Contains("config hash", client.RejectionReason);
            Assert.IsFalse(server.Session.IsPeerConnected("wrong-algorithm"));
            Assert.AreEqual(0, snapshots.Applied);
        }

        [Test]
        public void ProductionServerRejectsPeerWithDifferentPlayableCatalogMarkerBeforeSnapshotSynchronization()
        {
            var serverConfig = CharacterTestSessionHashContract.Normalize(
                new NetworkSessionConfig(1, 60, 0, 1, "base-config", "same-custom-resource-catalog"));
            string incompatibleHash = serverConfig.ConfigHash.Replace(
                CharacterTestSessionHashContract.PlayableCatalogMarker,
                "|ct.playable-catalog=999:sha256:incompatible");
            var clientConfig = new NetworkSessionConfig(serverConfig.ProtocolVersion, serverConfig.TickRate,
                serverConfig.InputDelayFrames, serverConfig.SnapshotIntervalFrames, incompatibleHash,
                serverConfig.ResourceCatalogHash, serverConfig.StateSyncPolicy, serverConfig.HeartbeatIntervalFrames);
            var transport = new LoopbackNetworkTransport();
            using var server = new CharacterTestUnityTransportServerRunner(transport, NullRuntimeLogger.Instance,
                config: serverConfig, sessionId: "catalog-marker-test", serverPeerId: "server", createServerViews: false, disposeTransport: false);
            var client = new NetworkSessionRunner(NetworkSessionRole.Client, "catalog-marker-test", "wrong-catalog",
                "server", clientConfig, transport, null);
            var snapshots = new SnapshotProbe(server.Slice.Services.Network.CreateSnapshotParticipant().ParticipantId);
            client.RegisterParticipant(snapshots);
            client.Connect(RuntimeFrame.Zero);
            server.Session.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Rejected, client.Status);
            StringAssert.Contains("config hash", client.RejectionReason);
            Assert.IsFalse(server.Session.IsPeerConnected("wrong-catalog"));
            Assert.AreEqual(0, snapshots.Applied);
        }

        private static string CharacterTestContractRuntimeHashMarkerWithWrongAlgorithm()
        {
            int incompatibleAlgorithm = RuntimeHashAccumulator.AlgorithmId == 1 ? 2 : 1;
            return "|ct.runtime-hash=" + RuntimeHashAccumulator.EncodingVersion + ":" + incompatibleAlgorithm;
        }

        [Test]
        public void ProductionHostAndBothEndpointsExposeOneNormalizedConfig()
        {
            var transport = new LoopbackNetworkTransport();
            using var host = new CharacterTestUnityTransportHostRunner(transport, transport, NullRuntimeLogger.Instance,
                config: NetworkSessionConfig.Default, createClientViews: false, disposeTransports: false);
            string expected = CharacterTestSessionHashContract.Normalize(NetworkSessionConfig.Default).ConfigHash;
            Assert.AreEqual(expected, host.Config.ConfigHash);
            Assert.AreEqual(expected, host.ServerRunner.Config.ConfigHash);
            Assert.AreEqual(expected, host.LocalClientRunner.Config.ConfigHash);
            for (int i = 0; i < 16; i++) host.Tick(1d / 60d);
            Assert.AreEqual(NetworkSessionStatus.Connected, host.LocalClientSession.Status);
        }

        [Test]
        public void DirectNormalizationPreservesResourceCatalogBaseWhileAddingPlayableMarker()
        {
            NetworkSessionConfig normalized = CharacterTestSessionHashContract.Normalize(
                new NetworkSessionConfig(1, 60, 0, 1, "content", string.Empty));
            Assert.IsEmpty(normalized.ResourceCatalogHash);
            StringAssert.EndsWith(CharacterTestSessionHashContract.PlayableCatalogMarker, normalized.ConfigHash);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealLoopbackUsesTheProductionResultAndReplays(bool withAuthority)
        {
            using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
            var inner = withAuthority ? FixedHashBenchmark.PrepareAuthority(runner) : null;
            // Match the connected, initialized session boundary used by the authority fixture.
            // The existing v5 bootstrap at untouched frame 1 also fails replay (separate baseline issue).
            if (!withAuthority) for (int i = 0; i < 4; i++) runner.Tick(1d / 60d);
            Assert.AreEqual(runner.Config.ConfigHash, runner.ServerSession.Config.ConfigHash);
            Assert.AreEqual(runner.Config.ConfigHash, runner.ClientSession.Config.ConfigHash);
            Assert.IsTrue(runner.BeginReplayRecording().Recording);
            for (int i = 0; i < 16; i++) runner.Tick(1d / 60d);
            var capture = runner.CreateReplayCapture();
            var record = capture.RuntimeReplaySnapshot.Records[capture.FrameCount - 1];
            long gameplay = runner.ServerSlice.Services.Network.CreateSnapshotParticipant().ComputeStateHash(record.Frame);
            long expected = withAuthority
                ? CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(gameplay, inner.ServerSessionAuthorityParticipant.AuthorityHash)
                : gameplay;
            Assert.AreEqual(expected, record.ResultHash);
            Assert.AreEqual(7, capture.RuntimeReplaySnapshot.Header.SchemaVersion);
            var playback = CharacterTestReplayPlayback.Play(capture);
            Assert.IsTrue(playback.Success, playback.FailureMessage);
        }

        [Test]
        public void OldReplayIsRejectedBeforeCreatingOrReplacingAWorld()
        {
            var oldHeader = new RuntimeReplayHeader(5, "character-test-replay-v0.5", "", "", RuntimeFrame.Zero);
            var capture = new CharacterTestReplayCapture(new RuntimeReplaySnapshot(oldHeader,
                new[] { new RuntimeReplayFrameRecord(RuntimeFrame.Zero, Array.Empty<RuntimeCommand>(), 0, "") }),
                null, 60, NetworkStateSyncPolicy.BootstrapOnly, Array.Empty<RuntimeCommand>());
            int worldsCreated = 0;
            using var driver = new CharacterTestReplayFrameDriver(capture, () => { worldsCreated++; return null; });
            StringAssert.Contains("v5", Assert.Throws<NotSupportedException>(() => driver.Reset(oldHeader)).Message);
            Assert.Throws<NotSupportedException>(() => driver.BeginResetAndRestoreSessionAuthority(oldHeader));
            Assert.Throws<NotSupportedException>(() => driver.RestoreGameplayBootstrap(oldHeader));
            Assert.AreEqual(0, worldsCreated);
            Assert.IsNull(driver.Slice);
        }

        private sealed class SnapshotProbe : INetworkSnapshotParticipant
        {
            public SnapshotProbe(string id) { ParticipantId = id; }
            public string ParticipantId { get; }
            public int Applied { get; private set; }
            public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame) => null;
            public NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot)
            {
                Applied++;
                return NetworkSnapshotApplyResult.Succeeded();
            }
        }
    }
}
