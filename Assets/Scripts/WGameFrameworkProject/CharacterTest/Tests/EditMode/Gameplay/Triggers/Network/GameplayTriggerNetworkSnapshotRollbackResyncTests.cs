using System;
using System.Buffers.Binary;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.NetworkBridge;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers.Network
{
    public sealed class GameplayTriggerNetworkSnapshotRollbackResyncTests
    {
        [Test]
        public void EndOfFrameSnapshotRestoresGameplayBeforeTriggerAndSuffixConverges()
        {
            RuntimeFixture server = RuntimeFixture.Create();
            server.RunSignalFrame(0, 1);
            GameplayTriggerNetworkProfile profile = server.CreateProfile(NetworkHashComparability.FullComparable);
            var serverBundle = new GameplayTriggerNetworkStateBundle(profile, server.World, server.Runtime, 8);
            var payload = new byte[serverBundle.MaximumSnapshotPayloadBytes];
            Assert.IsTrue(serverBundle.TryCaptureSnapshotPayload(0, new RuntimeFrame(0), RuntimeFrame.Zero,
                server.Hash(0), payload, out int payloadBytes).Success);
            Assert.Less(payloadBytes, NetworkBoundedWire.SnapshotTotalLimit - NetworkBoundedWire.HeaderBytes);

            RuntimeFixture client = RuntimeFixture.Create(createOwner: false);
            var clientBundle = new GameplayTriggerNetworkStateBundle(profile, client.World, client.Runtime, 8);
            Assert.IsTrue(clientBundle.TryApplySnapshotPayload(payload.AsSpan(0, payloadBytes), out RuntimeFrame frame, out long resultHash).Success);
            Assert.AreEqual(new RuntimeFrame(0), frame);
            Assert.AreEqual(server.Hash(0), resultHash);
            Assert.IsTrue(client.World.IsAlive(server.Owner));
            Assert.AreEqual(server.Hash(0), client.Hash(0));
            Assert.IsTrue(clientBundle.TryGetResumeFrame(frame, out RuntimeFrame resume).Success);
            Assert.AreEqual(new RuntimeFrame(1), resume);

            for (int suffixFrame = 1; suffixFrame <= 3; suffixFrame++)
            {
                server.RunSignalFrame(suffixFrame, suffixFrame + 1);
                client.RunSignalFrame(suffixFrame, suffixFrame + 1);
                Assert.AreEqual(server.Hash(suffixFrame), client.Hash(suffixFrame), "suffix frame " + suffixFrame);
            }
        }

        [Test]
        public void UnknownTriggerEntityFailsCandidateWithoutPublishingGameplay()
        {
            RuntimeFixture server = RuntimeFixture.Create();
            server.RunSignalFrame(0, 1);
            GameplayTriggerNetworkProfile profile = server.CreateProfile(NetworkHashComparability.FullComparable);
            var serverBundle = new GameplayTriggerNetworkStateBundle(profile, server.World, server.Runtime, 8);
            var payload = new byte[serverBundle.MaximumSnapshotPayloadBytes];
            Assert.IsTrue(serverBundle.TryCaptureSnapshotPayload(0, RuntimeFrame.Zero, RuntimeFrame.Zero, server.Hash(0), payload, out int bytes).Success);

            int gameplayLength = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(124 + 12, 4));
            int triggerLength = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(124 + 24 + 12, 4));
            int triggerStart = 124 + 48 + gameplayLength;
            int firstRouteSubjectIndex = triggerStart
                + GameplayTriggerAuthorityBinaryState.HeaderBytes
                + GameplayTriggerAuthorityBinaryState.InstanceRecordBytes
                + 16;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(firstRouteSubjectIndex, 4), 999);
            long changedTriggerHash = HashBytes(payload.AsSpan(triggerStart, triggerLength));
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(124 + 24 + 16, 8), changedTriggerHash);

            RuntimeFixture client = RuntimeFixture.Create(createOwner: false);
            var clientBundle = new GameplayTriggerNetworkStateBundle(profile, client.World, client.Runtime, 8);
            GameplayTriggerNetworkResult result = clientBundle.TryApplySnapshotPayload(payload.AsSpan(0, bytes), out _, out _);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.ResyncRequired, result.ErrorCode);
            Assert.AreEqual(0, client.World.CountAlive, "Gameplay candidate must not publish when Trigger validation fails");
            Assert.AreEqual(-1L, clientBundle.LastAppliedSnapshotId);
        }

        [Test]
        public void AuthenticatedSnapshotIngressIsIdempotentAndCorruptionRequestsResync()
        {
            RuntimeFixture server = RuntimeFixture.Create();
            server.RunSignalFrame(0, 1);
            GameplayTriggerNetworkProfile profile = server.CreateProfile(NetworkHashComparability.FullComparable);
            var serverBundle = new GameplayTriggerNetworkStateBundle(profile, server.World, server.Runtime, 8);
            var payload = new byte[serverBundle.MaximumSnapshotPayloadBytes];
            Assert.IsTrue(serverBundle.TryCaptureSnapshotPayload(0, RuntimeFrame.Zero, RuntimeFrame.Zero, server.Hash(0), payload, out int payloadBytes).Success);

            RuntimeFixture client = RuntimeFixture.Create(createOwner: false);
            var clientBundle = new GameplayTriggerNetworkStateBundle(profile, client.World, client.Runtime, 8);
            var ingress = new GameplayTriggerNetworkSnapshotIngress(profile, clientBundle, serverPeerHandle: 1);
            var metadata = new NetworkBoundedAuthenticatedMetadata(
                55,
                1,
                2,
                NetworkBoundedPacketDirection.ServerToClient);
            var context = new NetworkBoundedIngressContext(metadata, NetworkSessionRole.Client,
                NetworkSessionStatus.Connected, RuntimeFrame.Zero, profile.NetworkProfileHash);
            byte[] first = EncodeSnapshotPacket(payload.AsSpan(0, payloadBytes), outerSequence: 0, sender: 1, receiver: 2);
            Assert.IsTrue(ingress.TryAdmit(first, context).Success);
            long hashAfterFirst = client.Hash(0);
            byte[] duplicate = EncodeSnapshotPacket(payload.AsSpan(0, payloadBytes), outerSequence: 1, sender: 1, receiver: 2);
            Assert.IsTrue(ingress.TryAdmit(duplicate, context).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.AlreadyApplied, ingress.LastResult.ErrorCode);
            Assert.AreEqual(hashAfterFirst, client.Hash(0));
            Assert.AreEqual(2L, ingress.ExpectedOuterSequence);

            byte[] alteredPayload = payload.AsSpan(0, payloadBytes).ToArray();
            alteredPayload[alteredPayload.Length - 1] ^= 1;
            byte[] corrupt = EncodeSnapshotPacket(alteredPayload, outerSequence: 2, sender: 1, receiver: 2);
            Assert.IsFalse(ingress.TryAdmit(corrupt, context).Success,
                "a duplicate snapshot id must also be byte-identical after wire CRC validation");
            Assert.IsTrue(ingress.ResyncRequested);
            Assert.AreEqual(GameplayTriggerNetworkResyncReason.CorruptSnapshot, ingress.LastResyncReason);
            Assert.AreEqual(hashAfterFirst, client.Hash(0));
        }

        [Test]
        public void ForgedServerHeaderCannotOverrideAuthenticatedSnapshotRemotePeer()
        {
            RuntimeFixture server = RuntimeFixture.Create();
            server.RunSignalFrame(0, 1);
            GameplayTriggerNetworkProfile profile = server.CreateProfile(NetworkHashComparability.FullComparable);
            var serverBundle = new GameplayTriggerNetworkStateBundle(profile, server.World, server.Runtime, 8);
            var payload = new byte[serverBundle.MaximumSnapshotPayloadBytes];
            Assert.IsTrue(serverBundle.TryCaptureSnapshotPayload(
                0,
                RuntimeFrame.Zero,
                RuntimeFrame.Zero,
                server.Hash(0),
                payload,
                out int payloadBytes).Success);

            RuntimeFixture client = RuntimeFixture.Create(createOwner: false);
            var clientBundle = new GameplayTriggerNetworkStateBundle(profile, client.World, client.Runtime, 8);
            var ingress = new GameplayTriggerNetworkSnapshotIngress(profile, clientBundle, serverPeerHandle: 1);
            byte[] forged = EncodeSnapshotPacket(
                payload.AsSpan(0, payloadBytes),
                outerSequence: 0,
                sender: 1,
                receiver: 2);
            var authenticated = new NetworkBoundedAuthenticatedMetadata(
                55,
                9,
                2,
                NetworkBoundedPacketDirection.ServerToClient);
            var context = new NetworkBoundedIngressContext(
                authenticated,
                NetworkSessionRole.Client,
                NetworkSessionStatus.Connected,
                RuntimeFrame.Zero,
                profile.NetworkProfileHash);

            Assert.IsFalse(ingress.TryAdmit(forged, context).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.IdentityMismatch, ingress.LastResult.ErrorCode);
            Assert.AreEqual(GameplayTriggerNetworkResyncReason.UnauthorizedSnapshot, ingress.LastResyncReason);
            Assert.AreEqual(0, client.World.CountAlive);
            Assert.AreEqual(-1L, clientBundle.LastAppliedSnapshotId);
            Assert.AreEqual(0L, ingress.ExpectedOuterSequence);
        }

        [Test]
        public void BoundedRollbackRestoresAndReplaysParticipatingClient()
        {
            RuntimeFixture baseline = RuntimeFixture.Create();
            RuntimeFixture predicted = RuntimeFixture.Create();
            GameplayTriggerNetworkProfile profile = baseline.CreateProfile(NetworkHashComparability.FullComparable);
            var bundle = new GameplayTriggerNetworkStateBundle(profile, predicted.World, predicted.Runtime, 8);
            var coordinator = new NetworkPredictionRollbackCoordinator();
            var rollback = new GameplayTriggerNetworkRollbackResync(coordinator, bundle, historyCapacity: 4);

            baseline.RunSignalFrame(0, 1);
            predicted.RunSignalFrame(0, 1);
            Assert.IsTrue(rollback.TryRecord(RuntimeFrame.Zero).Success);
            for (int frame = 1; frame <= 2; frame++)
            {
                baseline.RunSignalFrame(frame, frame + 1);
                predicted.RunEmptySignalFrame(frame);
            }
            Assert.AreNotEqual(baseline.Hash(2), predicted.Hash(2));
            Assert.IsTrue(rollback.TryRollbackAndReplay(RuntimeFrame.Zero, new RuntimeFrame(2), replayFrame =>
            {
                predicted.RunSignalFrame(replayFrame.Value, replayFrame.Value + 1);
                return GameplayTriggerNetworkResult.Succeeded();
            }).Success);
            Assert.AreEqual(baseline.Hash(2), predicted.Hash(2));
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.ResyncRequired,
                rollback.TryRollbackAndReplay(new RuntimeFrame(99), new RuntimeFrame(100), _ => GameplayTriggerNetworkResult.Succeeded()).ErrorCode);
        }

        [Test]
        public void PartialClientRefusesFullHashAndStateRequestUsesFrozen56Bytes()
        {
            RuntimeFixture fixture = RuntimeFixture.Create();
            GameplayTriggerNetworkProfile profile = fixture.CreateProfile(NetworkHashComparability.PartialNonComparable);
            var bundle = new GameplayTriggerNetworkStateBundle(profile, fixture.World, fixture.Runtime, 8);
            var ingress = new GameplayTriggerNetworkSnapshotIngress(profile, bundle, 1);
            Assert.IsFalse(ingress.TryCompareFullHash(10, 10, out bool equal).Success);
            Assert.IsFalse(equal);

            var request = new GameplayTriggerNetworkStateRequest(0, new RuntimeFrame(7), -1, 10, 11,
                GameplayTriggerNetworkResyncReason.HashMismatch, NetworkHashComparability.PartialNonComparable);
            var payload = new byte[56];
            Assert.IsTrue(GameplayTriggerNetworkStateRequestCodec.TryEncode(profile, request, payload).Success);
            Assert.IsTrue(GameplayTriggerNetworkStateRequestCodec.TryDecode(profile, payload, out GameplayTriggerNetworkStateRequest decoded).Success);
            Assert.AreEqual(7L, decoded.RequestedFrame.Value);
            Assert.AreEqual(-1L, decoded.LastSnapshotId);
            payload[51] = 1;
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.InvalidPacket,
                GameplayTriggerNetworkStateRequestCodec.TryDecode(profile, payload, out _).ErrorCode);
        }

        [Test]
        public void FullComparableComponentCodecRoundtripsAndMissingDuplicateOrMismatchedCodecFailsClosed()
        {
            RuntimeFixture server = RuntimeFixture.Create();
            GameplayComponentStore<NetworkTestComponent> serverStore =
                server.World.GetOrCreateStore<NetworkTestComponent>();
            serverStore.Set(server.Owner, new NetworkTestComponent(42));
            server.RunSignalFrame(0, 1);
            GameplayTriggerNetworkProfile profile = server.CreateProfile(NetworkHashComparability.FullComparable);
            var serverCodec = new NetworkTestComponentCodec(serverStore, new[] { server.Owner });
            var serverBundle = new GameplayTriggerNetworkStateBundle(
                profile,
                server.World,
                server.Runtime,
                new IGameplayComponentNetworkStateCodec[] { serverCodec },
                8);
            var payload = new byte[serverBundle.MaximumSnapshotPayloadBytes];
            Assert.IsTrue(serverBundle.TryCaptureSnapshotPayload(
                0, RuntimeFrame.Zero, RuntimeFrame.Zero, server.Hash(0), payload, out int payloadBytes).Success);

            RuntimeFixture client = RuntimeFixture.Create(createOwner: false);
            GameplayComponentStore<NetworkTestComponent> clientStore =
                client.World.GetOrCreateStore<NetworkTestComponent>();
            var clientCodec = new NetworkTestComponentCodec(clientStore, new[] { server.Owner });
            var clientBundle = new GameplayTriggerNetworkStateBundle(
                profile,
                client.World,
                client.Runtime,
                new IGameplayComponentNetworkStateCodec[] { clientCodec },
                8);
            Assert.IsTrue(clientBundle.TryApplySnapshotPayload(
                payload.AsSpan(0, payloadBytes), out _, out _).Success);
            Assert.IsTrue(clientStore.TryGet(server.Owner, out NetworkTestComponent restored));
            Assert.AreEqual(42, restored.Value);

            Assert.Throws<InvalidOperationException>(() => new GameplayTriggerNetworkStateBundle(
                profile, server.World, server.Runtime, 8));
            Assert.Throws<ArgumentException>(() => new GameplayTriggerNetworkStateBundle(
                profile,
                server.World,
                server.Runtime,
                new IGameplayComponentNetworkStateCodec[] { serverCodec, serverCodec },
                8));
            var wrongTypeCodec = new NetworkTestComponentCodec(
                serverStore,
                new[] { server.Owner },
                componentType: typeof(NetworkWrongTestComponent));
            Assert.Throws<InvalidOperationException>(() => new GameplayTriggerNetworkStateBundle(
                profile,
                server.World,
                server.Runtime,
                new IGameplayComponentNetworkStateCodec[] { wrongTypeCodec },
                8));
            Assert.IsTrue(server.World.IsAlive(server.Owner));
            Assert.IsTrue(serverStore.TryGet(server.Owner, out NetworkTestComponent unchanged));
            Assert.AreEqual(42, unchanged.Value,
                "same-count wrong-CLR-type composition must fail before live mutation");

            AssertComponentSnapshotRejectedWithoutMutation(
                profile,
                payload.AsSpan(0, payloadBytes),
                server.Owner,
                row => BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(0, 4), 999U));
            AssertComponentSnapshotRejectedWithoutMutation(
                profile,
                payload.AsSpan(0, payloadBytes),
                server.Owner,
                row => BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(4, 2), 2));
            AssertComponentSnapshotRejectedWithoutMutation(
                profile,
                payload.AsSpan(0, payloadBytes),
                server.Owner,
                row => BinaryPrimitives.WriteInt32LittleEndian(row.Slice(8, 4), 1));
            AssertComponentSnapshotRejectedWithoutMutation(
                profile,
                payload.AsSpan(0, payloadBytes),
                server.Owner,
                null,
                maximumCodecBytes: 15);
        }

        [Test]
        public void LateJoinSnapshotExecutesPendingInternalAndReliableWorkExactlyOnceAtNextFrame()
        {
            MixedDeferredReliableFixture server = MixedDeferredReliableFixture.Create();
            server.QueueSignal(RuntimeFrame.Zero);
            Assert.AreEqual(1, server.Runtime.DeferredRecordCount);
            Assert.AreEqual(1, server.Runtime.ReliableOutboxRecordCount);
            GameplayTriggerNetworkProfile profile = server.CreateProfile();
            var serverBundle = new GameplayTriggerNetworkStateBundle(
                profile,
                server.World,
                server.Runtime,
                8);
            var payload = new byte[serverBundle.MaximumSnapshotPayloadBytes];
            Assert.IsTrue(serverBundle.TryCaptureSnapshotPayload(
                0,
                RuntimeFrame.Zero,
                RuntimeFrame.Zero,
                server.Hash(),
                payload,
                out int payloadBytes).Success);

            MixedDeferredReliableFixture client = MixedDeferredReliableFixture.Create(createOwner: false);
            var clientBundle = new GameplayTriggerNetworkStateBundle(
                profile,
                client.World,
                client.Runtime,
                8);
            Assert.IsTrue(clientBundle.TryApplySnapshotPayload(
                payload.AsSpan(0, payloadBytes),
                out RuntimeFrame snapshotFrame,
                out _).Success);
            Assert.AreEqual(RuntimeFrame.Zero, snapshotFrame);
            Assert.AreEqual(1, client.Runtime.DeferredRecordCount);
            Assert.AreEqual(1, client.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(0L, client.Runtime.LastDeferredExecutionResult.Sequence,
                "derived execution results must not be injected by the snapshot");

            var transfer = new CountingReliableTransfer(
                MixedDeferredReliableFixture.ReliableActionKind,
                "target.network-late-join");
            var host = new RuntimeHost();
            host.RegisterModule(new GameplayTriggerReliableOutboxModule(
                client.Runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer }));
            host.RegisterModule(new TargetModule("target.network-late-join"));
            host.Initialize();
            host.Start();

            var deferred = new GameplayTriggerDeferredSystem(client.Runtime);
            deferred.Tick(client.Context(new RuntimeFrame(1)));
            host.Tick(1L, 0d);

            Assert.AreEqual(0, client.Runtime.DeferredRecordCount);
            Assert.AreEqual(0, client.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, client.Runtime.LastDeferredExecutionResult.Sequence,
                "the derived internal result must be regenerated at F+1");
            Assert.AreEqual(1, transfer.CallCount);

            deferred.Tick(client.Context(new RuntimeFrame(2)));
            host.Tick(2L, 0d);
            Assert.AreEqual(1L, client.Runtime.LastDeferredExecutionResult.Sequence);
            Assert.AreEqual(1, transfer.CallCount);
            host.Dispose();
        }

        private static byte[] EncodeSnapshotPacket(ReadOnlySpan<byte> payload, long outerSequence, uint sender, uint receiver)
        {
            var packet = new byte[NetworkBoundedWire.HeaderBytes + payload.Length];
            var header = new NetworkBoundedPacketHeader(NetworkMessageKind.BoundedStateSnapshot, 55, sender, receiver,
                outerSequence, BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(8, 8)), payload.Length);
            Assert.IsTrue(NetworkBoundedWire.TryEncode(header, payload, packet, out _).Success);
            return packet;
        }

        private static long HashBytes(ReadOnlySpan<byte> bytes)
        { unchecked { ulong h = 14695981039346656037UL; for (int i = 0; i < bytes.Length; i++) { h ^= bytes[i]; h *= 1099511628211UL; } return (long)h; } }

        private static void AssertComponentSnapshotRejectedWithoutMutation(
            GameplayTriggerNetworkProfile profile,
            ReadOnlySpan<byte> sourcePayload,
            GameplayEntityId owner,
            DirectoryRowMutation mutateDirectoryRow,
            int maximumCodecBytes = -1)
        {
            byte[] payload = sourcePayload.ToArray();
            int gameplayLength = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(124 + 12, 4));
            int gameplayStateStart = 124 + 48;
            int componentDirectoryStart = gameplayStateStart
                + GameplayComponentWorldNetworkState.HeaderBytes
                + GameplayComponentWorldNetworkState.EntityRecordBytes;
            mutateDirectoryRow?.Invoke(payload.AsSpan(
                componentDirectoryStart,
                GameplayComponentWorldNetworkState.CodecDirectoryRowBytes));
            BinaryPrimitives.WriteInt64LittleEndian(
                payload.AsSpan(124 + 16, 8),
                HashBytes(payload.AsSpan(gameplayStateStart, gameplayLength)));

            RuntimeFixture guarded = RuntimeFixture.Create(createOwner: false);
            GameplayComponentStore<NetworkTestComponent> guardedStore =
                guarded.World.GetOrCreateStore<NetworkTestComponent>();
            guardedStore.Set(owner, new NetworkTestComponent(7));
            var guardedCodec = new NetworkTestComponentCodec(
                guardedStore,
                new[] { owner },
                maximumCodecBytes);
            var guardedBundle = new GameplayTriggerNetworkStateBundle(
                profile,
                guarded.World,
                guarded.Runtime,
                new IGameplayComponentNetworkStateCodec[] { guardedCodec },
                8);
            GameplayTriggerNetworkResult mismatch = guardedBundle.TryApplySnapshotPayload(
                payload, out _, out _);
            Assert.IsFalse(mismatch.Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.ResyncRequired, mismatch.ErrorCode);
            Assert.IsFalse(guarded.World.IsAlive(owner),
                "entity lifecycle must not publish on component codec mismatch");
            Assert.IsTrue(guardedStore.TryGet(owner, out NetworkTestComponent preserved));
            Assert.AreEqual(7, preserved.Value);
        }

        private delegate void DirectoryRowMutation(Span<byte> row);

        private sealed class RuntimeFixture
        {
            private readonly GameplayTriggerRuntimeHashContributor _hash;
            private RuntimeFixture(GameplayComponentWorld world, GameplayTriggerRuntime runtime, GameplayTriggerCatalog catalog,
                GameplayTriggerRuntimeOptions options, GameplayEntityId owner)
            { World = world; Runtime = runtime; Catalog = catalog; Options = options; Owner = owner; _hash = new GameplayTriggerRuntimeHashContributor(runtime); }
            public GameplayComponentWorld World { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public GameplayTriggerCatalog Catalog { get; }
            public GameplayTriggerRuntimeOptions Options { get; }
            public GameplayEntityId Owner { get; }

            public static RuntimeFixture Create(bool createOwner = true)
            {
                var world = new GameplayComponentWorld();
                GameplayEntityId owner = createOwner ? world.CreateEntity() : new GameplayEntityId(1, 1);
                var source = new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Signal,
                    GameplayTriggerSignalOrdering.Ordered);
                var route = new GameplayTriggerRouteDefinition(new GameplayTriggerRouteId(1), GameplayTriggerOccurrenceKind.Pulsed,
                    GameplayTriggerConditionProgram.Always,
                    new[] { new GameplayTriggerActionInstruction(new GameplayTriggerActionKindId(1), GameplayTriggerTargetSelector.Subject) });
                var definition = new GameplayTriggerDefinition(new GameplayTriggerDefinitionId(1), 1, source.SourceDefinitionId, new[] { route });
                var binding = new GameplayTriggerBinding(new GameplayTriggerBindingId(1), definition.DefinitionId,
                    new GameplayTriggerSourceBindingId(1), owner);
                Assert.IsTrue(GameplayTriggerCatalog.TryCreate(new[] { source }, new[] { definition }, new[] { binding },
                    new IGameplayTriggerActionSink[] { new Sink() }, out GameplayTriggerCatalog catalog,
                    out GameplayTriggerCatalogValidationResult validation), validation.ErrorCode.ToString());
                var options = new GameplayTriggerRuntimeOptions(1, 4, 8, 4, 4, 4, 4, 4, 4, 1, 0, 0);
                return new RuntimeFixture(world, new GameplayTriggerRuntime(catalog, options), catalog, options, owner);
            }

            public GameplayTriggerNetworkProfile CreateProfile(NetworkHashComparability comparability) =>
                new GameplayTriggerNetworkProfile(Catalog.DefinitionCatalogHash, Catalog.BindingCatalogHash,
                    Catalog.GameplayTriggerCatalogHash, Options, 4444, comparability, Array.Empty<GameplayTriggerNetworkSourcePolicy>());

            public void RunSignalFrame(long frame, long sequence)
            {
                var runtimeFrame = new RuntimeFrame(frame);
                Assert.IsTrue(Runtime.TrySubmitSignals(runtimeFrame, new GameplayTriggerSourceBindingId(1),
                    GameplayTriggerSignalOrdering.Ordered,
                    new[] { new GameplayTriggerSignal(1, Owner, sequence: sequence) }, out GameplayTriggerResult submit), submit.ErrorCode.ToString());
                Assert.IsTrue(Runtime.Evaluate(runtimeFrame).Success);
            }

            public void RunEmptySignalFrame(long frame)
            {
                var runtimeFrame = new RuntimeFrame(frame);
                Assert.IsTrue(Runtime.TrySubmitSignals(runtimeFrame, new GameplayTriggerSourceBindingId(1),
                    GameplayTriggerSignalOrdering.Ordered, Array.Empty<GameplayTriggerSignal>(),
                    out GameplayTriggerResult submit), submit.ErrorCode.ToString());
                Assert.IsTrue(Runtime.Evaluate(runtimeFrame).Success);
            }

            public long Hash(long frame)
            {
                var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
                _hash.Contribute(new RuntimeHashContext(new RuntimeFrame(frame)), accumulator);
                return accumulator.ToHash();
            }
        }

        private sealed class Sink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId => new GameplayTriggerActionKindId(1);
            public GameplayTriggerDeliveryContract DeliveryContract => GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token) { token = 1; return true; }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
            public void Abort(int token) { }
        }

        private sealed class MixedDeferredReliableFixture
        {
            private static readonly GameplayTriggerSourceBindingId SourceBinding =
                new GameplayTriggerSourceBindingId(101);
            private static readonly GameplayTriggerActionKindId InternalActionKind =
                new GameplayTriggerActionKindId(1001);
            public static readonly GameplayTriggerActionKindId ReliableActionKind =
                new GameplayTriggerActionKindId(1002);

            private readonly GameplayTriggerRuntimeHashContributor _hash;

            private MixedDeferredReliableFixture(
                GameplayComponentWorld world,
                GameplayTriggerRuntime runtime,
                GameplayTriggerCatalog catalog,
                GameplayTriggerRuntimeOptions options,
                GameplayEntityId owner)
            {
                World = world;
                Runtime = runtime;
                Catalog = catalog;
                Options = options;
                Owner = owner;
                _hash = new GameplayTriggerRuntimeHashContributor(runtime);
            }

            public GameplayComponentWorld World { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public GameplayTriggerCatalog Catalog { get; }
            public GameplayTriggerRuntimeOptions Options { get; }
            public GameplayEntityId Owner { get; }

            public static MixedDeferredReliableFixture Create(bool createOwner = true)
            {
                var world = new GameplayComponentWorld();
                GameplayEntityId owner = createOwner
                    ? world.CreateEntity()
                    : new GameplayEntityId(1, 1);
                var source = new GameplayTriggerSourceDefinition(
                    new GameplayTriggerSourceDefinitionId(1),
                    GameplayTriggerSourceKind.Signal,
                    GameplayTriggerSignalOrdering.UnorderedMultiset);
                var reliableSink = new ReliableSink(ReliableActionKind);
                var route = new GameplayTriggerRouteDefinition(
                    new GameplayTriggerRouteId(1),
                    GameplayTriggerOccurrenceKind.Pulsed,
                    GameplayTriggerConditionProgram.Always,
                    new[]
                    {
                        new GameplayTriggerActionInstruction(
                            InternalActionKind,
                            GameplayTriggerTargetSelector.ExplicitBindingTarget,
                            targetSlotId: 1),
                        new GameplayTriggerActionInstruction(
                            ReliableActionKind,
                            GameplayTriggerTargetSelector.Owner)
                    });
                var definition = new GameplayTriggerDefinition(
                    new GameplayTriggerDefinitionId(1),
                    1,
                    source.SourceDefinitionId,
                    new[] { route });
                var binding = new GameplayTriggerBinding(
                    new GameplayTriggerBindingId(1),
                    definition.DefinitionId,
                    SourceBinding,
                    owner,
                    explicitTargets: new[]
                    {
                        new GameplayTriggerBindingTarget(
                            1,
                            GameplayTriggerTargetRef.FromTriggerSourceBinding(SourceBinding))
                    });
                var descriptors = new[]
                {
                    new GameplayTriggerInternalDeferredActionDescriptor(
                        InternalActionKind,
                        GameplayTriggerInternalDeferredOperationKind.Reset)
                };
                Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                    new[] { source },
                    new[] { definition },
                    new[] { binding },
                    new IGameplayTriggerActionSink[] { reliableSink },
                    descriptors,
                    out GameplayTriggerCatalog catalog,
                    out GameplayTriggerCatalogValidationResult validation),
                    validation.ErrorCode.ToString());
                var options = new GameplayTriggerRuntimeOptions(
                    8, 16, 16, 16, 16, 16, 16, 16, 8, 8, 2, 2);
                return new MixedDeferredReliableFixture(
                    world,
                    new GameplayTriggerRuntime(catalog, options),
                    catalog,
                    options,
                    owner);
            }

            public GameplayTriggerNetworkProfile CreateProfile() =>
                new GameplayTriggerNetworkProfile(
                    Catalog.DefinitionCatalogHash,
                    Catalog.BindingCatalogHash,
                    Catalog.GameplayTriggerCatalogHash,
                    Options,
                    4444,
                    NetworkHashComparability.FullComparable,
                    Array.Empty<GameplayTriggerNetworkSourcePolicy>());

            public void QueueSignal(RuntimeFrame frame)
            {
                Assert.IsTrue(Runtime.TrySubmitSignals(
                    frame,
                    SourceBinding,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(31, Owner, Owner, 101, 102, 103) },
                    out GameplayTriggerResult result),
                    result.ErrorCode.ToString());
                Assert.IsTrue(Runtime.Evaluate(frame).Success);
            }

            public GameplaySystemContext Context(RuntimeFrame frame) =>
                new GameplaySystemContext(
                    frame,
                    0d,
                    0d,
                    new GameplayWorld(),
                    Array.Empty<RuntimeCommand>(),
                    World.Events,
                    new GameplayCommandExecutionState(),
                    World);

            public long Hash()
            {
                var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
                _hash.Contribute(new RuntimeHashContext(Runtime.AuthorityLastEvaluatedFrame), accumulator);
                return accumulator.ToHash();
            }
        }

        private sealed class ReliableSink : IGameplayTriggerActionSink
        {
            public ReliableSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.ReliableDeferred;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token) =>
                throw new InvalidOperationException("Reliable action must enter the bounded outbox.");
            public void Commit(int token, in GameplayTriggerActionIntent intent) =>
                throw new InvalidOperationException("Reliable action must enter the bounded outbox.");
            public void Abort(int token) =>
                throw new InvalidOperationException("Reliable action must enter the bounded outbox.");
        }

        private sealed class CountingReliableTransfer : IGameplayTriggerReliableTargetTransfer
        {
            public CountingReliableTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public string TargetModuleId { get; }
            public bool IsEnabled => true;
            public int CallCount { get; private set; }

            public GameplayTriggerReliableTransferResult TryTransfer(
                RuntimeFrame currentFrame,
                in GameplayTriggerReliableOutboxRecord record)
            {
                CallCount++;
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.Accepted);
            }
        }

        private sealed class TargetModule : RuntimeModule
        {
            public TargetModule(string moduleId)
                : base(moduleId, RuntimeTickStage.Simulation, 0)
            {
            }
        }

        private readonly struct NetworkTestComponent : IGameplayComponent
        {
            public NetworkTestComponent(int value) { Value = value; }
            public int Value { get; }
        }

        private readonly struct NetworkWrongTestComponent : IGameplayComponent
        {
        }

        private sealed class NetworkTestComponentCodec : IGameplayComponentNetworkStateCodec
        {
            private const int HeaderBytes = 4;
            private const int RecordBytes = 12;
            private readonly GameplayComponentStore<NetworkTestComponent> _store;
            private readonly GameplayEntityId[] _tracked;
            private readonly GameplayEntityId[] _candidateEntities;
            private readonly int[] _candidateValues;
            private readonly int _maximumEncodedBytes;
            private readonly Type _componentType;
            private int _candidateCount;
            private bool _prepared;

            public NetworkTestComponentCodec(
                GameplayComponentStore<NetworkTestComponent> store,
                GameplayEntityId[] tracked,
                int maximumEncodedBytes = -1,
                Type componentType = null)
            {
                _store = store;
                _tracked = (GameplayEntityId[])tracked.Clone();
                Array.Sort(_tracked);
                _candidateEntities = new GameplayEntityId[_tracked.Length];
                _candidateValues = new int[_tracked.Length];
                _maximumEncodedBytes = maximumEncodedBytes >= 0
                    ? maximumEncodedBytes
                    : HeaderBytes + (_tracked.Length * RecordBytes);
                _componentType = componentType ?? typeof(NetworkTestComponent);
            }

            public uint TypeId => 9001U;
            public ushort Schema => 1;
            public Type ComponentType => _componentType;
            public int MaximumEncodedBytes => _maximumEncodedBytes;

            public GameplayComponentNetworkStateResult TryCapture(Span<byte> destination, out int bytesWritten)
            {
                bytesWritten = 0;
                int count = 0;
                for (int i = 0; i < _tracked.Length; i++)
                {
                    if (_store.Contains(_tracked[i])) count++;
                }
                int required = HeaderBytes + (count * RecordBytes);
                if (destination.Length < required)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.DestinationTooSmall);
                BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, 4), count);
                int offset = HeaderBytes;
                for (int i = 0; i < _tracked.Length; i++)
                {
                    GameplayEntityId entity = _tracked[i];
                    if (!_store.TryGet(entity, out NetworkTestComponent component)) continue;
                    BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), entity.Index);
                    BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset + 4, 4), entity.Generation);
                    BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset + 8, 4), component.Value);
                    offset += RecordBytes;
                }
                bytesWritten = required;
                return GameplayComponentNetworkStateResult.Succeeded();
            }

            public GameplayComponentNetworkStateResult TryPrepare(
                ReadOnlySpan<byte> state,
                IGameplayComponentNetworkEntityCandidate entityCandidate)
            {
                DiscardPrepared();
                if (state.Length < HeaderBytes)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.InvalidHeader);
                int count = BinaryPrimitives.ReadInt32LittleEndian(state.Slice(0, 4));
                if (count < 0 || count > _candidateEntities.Length || state.Length != HeaderBytes + (count * RecordBytes))
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.CapacityExceeded);
                int previousIndex = 0;
                int offset = HeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    var entity = new GameplayEntityId(
                        BinaryPrimitives.ReadInt32LittleEndian(state.Slice(offset, 4)),
                        BinaryPrimitives.ReadInt32LittleEndian(state.Slice(offset + 4, 4)));
                    if (entity.Index <= previousIndex || !entityCandidate.Contains(entity))
                        return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.InvalidEntity);
                    _candidateEntities[i] = entity;
                    _candidateValues[i] = BinaryPrimitives.ReadInt32LittleEndian(state.Slice(offset + 8, 4));
                    previousIndex = entity.Index;
                    offset += RecordBytes;
                }
                _candidateCount = count;
                _prepared = true;
                return GameplayComponentNetworkStateResult.Succeeded();
            }

            public void CommitPrepared()
            {
                if (!_prepared) throw new InvalidOperationException();
                _store.Clear();
                for (int i = 0; i < _candidateCount; i++)
                    _store.Set(_candidateEntities[i], new NetworkTestComponent(_candidateValues[i]));
                DiscardPrepared();
            }

            public void DiscardPrepared()
            {
                _candidateCount = 0;
                _prepared = false;
            }
        }
    }
}
