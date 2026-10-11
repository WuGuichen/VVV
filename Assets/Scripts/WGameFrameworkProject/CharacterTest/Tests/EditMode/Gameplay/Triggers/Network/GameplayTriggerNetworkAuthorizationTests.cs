using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.NetworkBridge;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers.Network
{
    public sealed class GameplayTriggerNetworkAuthorizationTests
    {
        [Test]
        public void OwnedAuthenticatedSignalIsAcceptedAndFailuresAreAtomic()
        {
            const int sourceId = 77;
            var commands = new RuntimeCommand[1];
            var owned = new GameplayEntityId(1, 1);
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(10), sourceId, RuntimeCommandProvenanceKind.Exogenous,
                new GameplayTriggerSourceBindingId(1), GameplayTriggerSignalOrdering.Ordered, 1,
                new[] { new GameplayTriggerSignal(1, owned, sequence: 1) }, commands,
                out int written, out _));
            Assert.AreEqual(1, written);

            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(8);
            var sources = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(sources.TryRegister(sourceId, RuntimeCommandSourceKind.Exogenous,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch }, out _));
            var gate = new RuntimeCommandSubmissionGate(sources, buffer, RuntimeCommandIngressKind.External);
            GameplayTriggerNetworkProfile profile = CreateProfile(sourceId);
            var authority = new AuthorityFacts(connectedPeer: 7, owned);
            var ingress = new GameplayTriggerNetworkAuthorityIngress(profile, authority, authority, gate);
            byte[] packet = EncodePacket(profile, commands, outerSequence: 0, packetSequence: 0, sender: 7);
            var context = CreateServerContext(profile, remotePeerHandle: 7);

            Assert.IsTrue(ingress.TryAdmit(packet, context).Success);
            Assert.AreEqual(1, buffer.PendingCount);
            Assert.AreEqual(1L, ingress.ExpectedOuterSequence);
            Assert.AreEqual(1L, ingress.ExpectedClientPacketSequence);

            Assert.IsFalse(ingress.TryAdmit(packet, context).Success, "duplicate must fail");
            Assert.AreEqual(1, buffer.PendingCount);
            Assert.AreEqual(1L, ingress.ExpectedOuterSequence);

            var notOwned = new GameplayEntityId(2, 1);
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(10), sourceId, RuntimeCommandProvenanceKind.Exogenous,
                new GameplayTriggerSourceBindingId(1), GameplayTriggerSignalOrdering.Ordered, 2,
                new[] { new GameplayTriggerSignal(1, notOwned, sequence: 2) }, commands,
                out _, out _));
            byte[] wrongOwner = EncodePacket(profile, commands, outerSequence: 1, packetSequence: 1, sender: 7);
            Assert.IsFalse(ingress.TryAdmit(wrongOwner, context).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.OwnershipMismatch, ingress.LastResult.ErrorCode);
            Assert.AreEqual(1, buffer.PendingCount);
            Assert.AreEqual(1L, ingress.ExpectedOuterSequence, "failed authorization must not advance outer frontier");
            Assert.AreEqual(1L, ingress.ExpectedClientPacketSequence);
        }

        [Test]
        public void UnknownPeerLifecycleControlForgedProvenanceAndTraceAreRejected()
        {
            const int sourceId = 77;
            GameplayTriggerNetworkProfile profile = CreateProfile(sourceId);
            var owned = new GameplayEntityId(1, 1);
            var authority = new AuthorityFacts(7, owned);
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(8);
            var sources = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(sources.TryRegister(sourceId, RuntimeCommandSourceKind.Exogenous,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch }, out _));
            var ingress = new GameplayTriggerNetworkAuthorityIngress(
                profile, authority, authority,
                new RuntimeCommandSubmissionGate(sources, buffer, RuntimeCommandIngressKind.External));
            var context = CreateServerContext(profile, remotePeerHandle: 7);

            var commands = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(10), sourceId, RuntimeCommandProvenanceKind.Exogenous,
                new GameplayTriggerSourceBindingId(1), GameplayTriggerSignalOrdering.Ordered, 1,
                new[] { new GameplayTriggerSignal(1, owned, sequence: 1) }, commands, out _, out _));
            NetworkBoundedIngressContext unknownContext = CreateServerContext(profile, remotePeerHandle: 8);
            Assert.IsFalse(ingress.TryAdmit(EncodePacket(profile, commands, 0, 0, 8), unknownContext).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.UnknownPeer, ingress.LastResult.ErrorCode);

            commands[0] = new RuntimeCommand(new RuntimeFrame(10), sourceId,
                GameplayTriggerRuntimeCommandIds.SetEnabled, 1, sequence: 0,
                envelope: new RuntimeCommandEnvelope(GameplayTriggerRuntimeEnvelopeIds.SignalBatch, 1,
                    RuntimeCommandProvenanceKind.Exogenous, 1));
            Assert.IsFalse(ingress.TryAdmit(EncodePacket(profile, commands, 0, 0, 7), context).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.UnauthorizedCommand, ingress.LastResult.ErrorCode);

            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(10), sourceId, RuntimeCommandProvenanceKind.Derived,
                new GameplayTriggerSourceBindingId(1), GameplayTriggerSignalOrdering.Ordered, 1,
                new[] { new GameplayTriggerSignal(1, owned, sequence: 1) }, commands, out _, out _));
            Assert.IsFalse(ingress.TryAdmit(EncodePacket(profile, commands, 0, 0, 7), context).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.ForgedProvenance, ingress.LastResult.ErrorCode);

            RuntimeCommand traced = new RuntimeCommand(commands[0].Frame, commands[0].SourceId, commands[0].CommandId,
                commands[0].TargetId, commands[0].Payload0, commands[0].Payload1, commands[0].Payload2,
                new RuntimeTraceIdentity(981002), commands[0].Sequence, commands[0].Envelope);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.TraceIdNotSupported,
                GameplayTriggerNetworkCommandCodec.TryEncodeRecord(traced, new byte[116]).ErrorCode);
        }

        [Test]
        public void ForgedOwningHeaderSenderCannotOverrideAuthenticatedRemotePeer()
        {
            const int sourceId = 77;
            GameplayTriggerNetworkProfile profile = CreateProfile(sourceId);
            var owned = new GameplayEntityId(1, 1);
            var commands = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(10), sourceId, RuntimeCommandProvenanceKind.Exogenous,
                new GameplayTriggerSourceBindingId(1), GameplayTriggerSignalOrdering.Ordered, 1,
                new[] { new GameplayTriggerSignal(1, owned, sequence: 1) }, commands, out _, out _));

            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(8);
            var sources = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(sources.TryRegister(sourceId, RuntimeCommandSourceKind.Exogenous,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch }, out _));
            var authority = new SplitAuthorityFacts(owningPeer: 7, authenticatedPeer: 8, owned);
            var ingress = new GameplayTriggerNetworkAuthorityIngress(
                profile,
                authority,
                authority,
                new RuntimeCommandSubmissionGate(sources, buffer, RuntimeCommandIngressKind.External));

            byte[] forged = EncodePacket(profile, commands, 0, 0, sender: 7);
            NetworkBoundedIngressContext context = CreateServerContext(profile, remotePeerHandle: 8);
            Assert.IsFalse(ingress.TryAdmit(forged, context).Success);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.IdentityMismatch, ingress.LastResult.ErrorCode);
            Assert.AreEqual(0, buffer.PendingCount);
            Assert.AreEqual(0L, ingress.ExpectedOuterSequence);
            Assert.AreEqual(0L, ingress.ExpectedClientPacketSequence);
            Assert.AreEqual(0U, ingress.LastAcceptedPeerHandle);
        }

        [Test]
        public void CommandPayloadRoundtripUsesFrozen104ByteHeaderAndCanonicalEmptyTrace()
        {
            GameplayTriggerNetworkProfile profile = CreateProfile(77);
            var source = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(5), 77, RuntimeCommandProvenanceKind.Exogenous,
                new GameplayTriggerSourceBindingId(3), GameplayTriggerSignalOrdering.Ordered, 9,
                new[] { new GameplayTriggerSignal(4, new GameplayEntityId(2, 1), sequence: 8) },
                source, out _, out _));
            var payload = new byte[GameplayTriggerNetworkCommandCodec.MaximumRuntimePayloadBytes];
            Assert.IsTrue(GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                profile, new RuntimeFrame(5), 0, source, payload, out int bytes).Success);
            Assert.AreEqual(104 + 116, bytes);
            Assert.AreEqual(1, BitConverter.ToInt32(payload, 96));
            Assert.AreEqual(116, BitConverter.ToInt32(payload, 100));
            var decoded = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerNetworkCommandCodec.TryDecodeRuntimePayload(
                profile, payload.AsSpan(0, bytes), decoded, out RuntimeFrame frame, out long sequence, out int count).Success);
            Assert.AreEqual(new RuntimeFrame(5), frame);
            Assert.AreEqual(0L, sequence);
            Assert.AreEqual(1, count);
            Assert.AreEqual(string.Empty, decoded[0].TraceId);
            Assert.AreEqual(source[0], decoded[0]);
        }

        [Test]
        public void GameplayEntityStatePreparesBeforePublishing()
        {
            var source = new GameplayComponentWorld();
            GameplayEntityId first = source.CreateEntity();
            GameplayEntityId second = source.CreateEntity();
            var sourceState = new GameplayComponentWorldNetworkState(source, 4);
            var bytes = new byte[sourceState.MaximumEncodedBytes];
            Assert.IsTrue(sourceState.TryCapture(bytes, out int written).Success);

            var target = new GameplayComponentWorld();
            var targetState = new GameplayComponentWorldNetworkState(target, 4);
            Assert.IsTrue(targetState.TryPrepare(bytes.AsSpan(0, written)).Success);
            Assert.AreEqual(0, target.CountAlive, "prepare must not mutate live Gameplay state");
            Assert.IsTrue(targetState.PreparedContains(first));
            Assert.IsTrue(targetState.PreparedContains(second));
            targetState.CommitPrepared();
            Assert.AreEqual(2, target.CountAlive);
            Assert.IsTrue(target.IsAlive(first));
            Assert.IsTrue(target.IsAlive(second));
        }

        [Test]
        public void AcceptedWireTraceMatchesEquivalentLocalExogenousAuthorityHash()
        {
            const int sourceId = 77;
            GameplayEntityId owner = new GameplayEntityId(1, 1);
            GameplayTriggerRuntime networkRuntime = CreateSignalRuntime(owner, out GameplayTriggerCatalog catalog);
            GameplayTriggerRuntime localRuntime = CreateSignalRuntime(owner, out _);
            GameplayTriggerRuntimeOptions options = networkRuntime.AuthorityOptions;
            var profile = new GameplayTriggerNetworkProfile(
                catalog.DefinitionCatalogHash,
                catalog.BindingCatalogHash,
                catalog.GameplayTriggerCatalogHash,
                options,
                44,
                NetworkHashComparability.FullComparable,
                new[] { new GameplayTriggerNetworkSourcePolicy(sourceId, GameplayTriggerNetworkObservationMask.Signal) });
            var signals = new[]
            {
                new GameplayTriggerSignal(1, owner, sequence: 1),
                new GameplayTriggerSignal(1, owner, sequence: 2)
            };
            var commands = new RuntimeCommand[signals.Length];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(10), sourceId, RuntimeCommandProvenanceKind.Exogenous,
                new GameplayTriggerSourceBindingId(1), GameplayTriggerSignalOrdering.Ordered, 1,
                signals, commands, out int commandCount, out _));
            Assert.AreEqual(signals.Length, commandCount);

            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(8);
            var sources = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(sources.TryRegister(sourceId, RuntimeCommandSourceKind.Exogenous,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch }, out _));
            var gate = new RuntimeCommandSubmissionGate(sources, buffer, RuntimeCommandIngressKind.External);
            var authority = new AuthorityFacts(connectedPeer: 7, owner);
            var ingress = new GameplayTriggerNetworkAuthorityIngress(profile, authority, authority, gate);
            var context = CreateServerContext(profile, remotePeerHandle: 7);
            Assert.IsTrue(ingress.TryAdmit(EncodePacket(profile, commands, 0, 0, 7), context).Success);

            var drained = new List<RuntimeCommand>(commands.Length);
            Assert.AreEqual(commands.Length, buffer.DrainForFrame(new RuntimeFrame(10), drained));
            var commandSystem = new GameplayTriggerCommandSystem(networkRuntime);
            var gameplayWorld = new GameplayWorld();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            commandSystem.Tick(new GameplaySystemContext(
                new RuntimeFrame(10), 1d / 60d, 10d / 60d, gameplayWorld, drained, events));
            Assert.IsTrue(networkRuntime.Evaluate(new RuntimeFrame(10)).Success);

            Assert.IsTrue(localRuntime.TrySubmitSignals(
                new RuntimeFrame(10), new GameplayTriggerSourceBindingId(1),
                GameplayTriggerSignalOrdering.Ordered, signals, out GameplayTriggerResult localSubmit),
                localSubmit.ErrorCode.ToString());
            Assert.IsTrue(localRuntime.Evaluate(new RuntimeFrame(10)).Success);
            Assert.AreEqual(Hash(localRuntime, 10), Hash(networkRuntime, 10));
        }

        private static GameplayTriggerNetworkProfile CreateProfile(int sourceId)
        {
            var options = new GameplayTriggerRuntimeOptions(1, 64, 2048, 64, 512, 4, 256, 256, 128, 1, 0, 128);
            return new GameplayTriggerNetworkProfile(11, 22, 33, options, 44,
                NetworkHashComparability.FullComparable,
                new[] { new GameplayTriggerNetworkSourcePolicy(sourceId, GameplayTriggerNetworkObservationMask.Signal) });
        }

        private static NetworkBoundedIngressContext CreateServerContext(
            GameplayTriggerNetworkProfile profile,
            uint remotePeerHandle)
        {
            var metadata = new NetworkBoundedAuthenticatedMetadata(
                55,
                remotePeerHandle,
                1,
                NetworkBoundedPacketDirection.ClientToServer);
            return new NetworkBoundedIngressContext(
                metadata,
                NetworkSessionRole.Server,
                NetworkSessionStatus.Connected,
                new RuntimeFrame(10),
                profile.NetworkProfileHash);
        }

        private static GameplayTriggerRuntime CreateSignalRuntime(
            GameplayEntityId owner,
            out GameplayTriggerCatalog catalog)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.Ordered);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        new GameplayTriggerActionKindId(1),
                        GameplayTriggerTargetSelector.Subject)
                });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1), 1, source.SourceDefinitionId, new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1), definition.DefinitionId,
                new GameplayTriggerSourceBindingId(1), owner);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { new HashSink() },
                out catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(1, 4, 4, 4, 4, 2, 4, 4, 4, 1, 0, 0));
        }

        private static long Hash(GameplayTriggerRuntime runtime, long frame)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(new RuntimeFrame(frame)), accumulator);
            return accumulator.ToHash();
        }

        private static byte[] EncodePacket(
            GameplayTriggerNetworkProfile profile,
            RuntimeCommand[] commands,
            long outerSequence,
            long packetSequence,
            uint sender)
        {
            var payload = new byte[GameplayTriggerNetworkCommandCodec.MaximumRuntimePayloadBytes];
            Assert.IsTrue(GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                profile, new RuntimeFrame(10), packetSequence, commands, payload, out int payloadBytes).Success);
            var packet = new byte[NetworkBoundedWire.HeaderBytes + payloadBytes];
            var header = new NetworkBoundedPacketHeader(NetworkMessageKind.BoundedRuntimeCommands,
                55, sender, 1, outerSequence, 10, payloadBytes);
            Assert.IsTrue(NetworkBoundedWire.TryEncode(header, payload.AsSpan(0, payloadBytes), packet, out _).Success);
            return packet;
        }

        private sealed class AuthorityFacts : IGameplayTriggerNetworkPeerDirectory, IGameplayTriggerNetworkOwnershipProvider
        {
            private readonly uint _connectedPeer;
            private readonly GameplayEntityId _owned;
            public AuthorityFacts(uint connectedPeer, GameplayEntityId owned) { _connectedPeer = connectedPeer; _owned = owned; }
            public long IdentityHash => 123;
            public bool IsConnected(uint peerHandle) => peerHandle == _connectedPeer;
            public bool IsOwnedBy(uint peerHandle, GameplayEntityId entityId) => peerHandle == _connectedPeer && entityId == _owned;
        }

        private sealed class SplitAuthorityFacts : IGameplayTriggerNetworkPeerDirectory, IGameplayTriggerNetworkOwnershipProvider
        {
            private readonly uint _owningPeer;
            private readonly uint _authenticatedPeer;
            private readonly GameplayEntityId _owned;

            public SplitAuthorityFacts(uint owningPeer, uint authenticatedPeer, GameplayEntityId owned)
            {
                _owningPeer = owningPeer;
                _authenticatedPeer = authenticatedPeer;
                _owned = owned;
            }

            public long IdentityHash => 456;
            public bool IsConnected(uint peerHandle) => peerHandle == _owningPeer || peerHandle == _authenticatedPeer;
            public bool IsOwnedBy(uint peerHandle, GameplayEntityId entityId) => peerHandle == _owningPeer && entityId == _owned;
        }

        private sealed class HashSink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId => new GameplayTriggerActionKindId(1);
            public GameplayTriggerDeliveryContract DeliveryContract => GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            { token = preparation.ActionIndex + 1; return true; }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
            public void Abort(int token) { }
        }
    }
}
