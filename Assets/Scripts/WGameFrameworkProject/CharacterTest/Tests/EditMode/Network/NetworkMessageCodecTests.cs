using System.Text;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Network
{
    public sealed class NetworkMessageCodecTests
    {
        [Test]
        public void JsonCodec_HandshakeRoundTripPreservesEnvelopeAndConfig()
        {
            var codec = new JsonNetworkMessageCodec();
            var config = new NetworkSessionConfig(
                7,
                30,
                2,
                5,
                "config",
                "resources",
                NetworkStateSyncPolicy.BootstrapOnly,
                heartbeatIntervalFrames: 15);
            var message = new NetworkMessageEnvelope(
                "session",
                "client",
                "server",
                42,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.HandshakeRequest,
                new RuntimeFrame(3),
                new NetworkHandshakeRequest("client", NetworkSessionRole.Client, config));

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            Assert.AreEqual("session", roundTrip.Message.SessionId);
            Assert.AreEqual("client", roundTrip.Message.SenderPeerId);
            Assert.AreEqual("server", roundTrip.Message.ReceiverPeerId);
            Assert.AreEqual(42, roundTrip.Message.Sequence);
            Assert.AreEqual(NetworkMessageKind.HandshakeRequest, roundTrip.Message.Kind);
            var payload = (NetworkHandshakeRequest)roundTrip.Message.Payload;
            Assert.AreEqual(NetworkSessionRole.Client, payload.Role);
            Assert.AreEqual(7, payload.Config.ProtocolVersion);
            Assert.AreEqual("resources", payload.Config.ResourceCatalogHash);
            Assert.AreEqual(NetworkStateSyncPolicy.BootstrapOnly, payload.Config.StateSyncPolicy);
            Assert.AreEqual(15, payload.Config.HeartbeatIntervalFrames);
        }

        [Test]
        public void JsonCodec_CommandPacketRoundTripPreservesCommands()
        {
            var codec = new JsonNetworkMessageCodec();
            var packet = new NetworkRuntimeCommandPacket(
                "client",
                new RuntimeFrame(11),
                new[]
                {
                    new RuntimeCommand(new RuntimeFrame(11), 3, 100, 7, 1, 2, 3,
                        new RuntimeTraceIdentity(TestCustomStateTypeIds.NetworkTest, 7, long.MaxValue, 9), 9)
                });
            var message = new NetworkMessageEnvelope(
                "session",
                "client",
                "server",
                1,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RuntimeCommands,
                new RuntimeFrame(11),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkRuntimeCommandPacket)roundTrip.Message.Payload;
            Assert.AreEqual("client", decoded.InputPeerId);
            Assert.AreEqual(new RuntimeFrame(11), decoded.ClientFrame);
            Assert.AreEqual(1, decoded.Commands.Count);
            Assert.AreEqual(packet.Commands[0], decoded.Commands[0]);
        }

        [Test]
        public void JsonCodec_TurnInPlaceControlIntentRoundTripPreservesEnvelopeAndQuantizedYaw()
        {
            var codec = new JsonNetworkMessageCodec();
            var intent = new CharacterTestControlIntent(
                targetEntityId: 7,
                move: new Vector2(1f, 0f),
                worldMove: new Vector2(0f, -1f),
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: true,
                turnInPlace: true,
                turnInPlaceYawMilliDegrees: 450123);
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                new RuntimeFrame(11),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                intent,
                new RuntimeTraceIdentity(970001));
            var packet = new NetworkRuntimeCommandPacket(
                "planner",
                new RuntimeFrame(11),
                new[] { command });
            var message = new NetworkMessageEnvelope(
                "session",
                "planner",
                "server",
                3,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RuntimeCommands,
                new RuntimeFrame(11),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decodedPacket = (NetworkRuntimeCommandPacket)roundTrip.Message.Payload;
            RuntimeCommand decodedCommand = decodedPacket.Commands[0];
            CharacterTestControlIntent decodedIntent =
                CharacterTestRuntimeCommandFactory.DecodeControlIntent(decodedCommand);
            Assert.IsTrue(decodedIntent.TurnInPlace);
            Assert.AreEqual(
                CharacterTestDeterministicUnits.NormalizeMilliDegrees(450123),
                decodedIntent.TurnInPlaceYawMilliDegrees);
            Assert.IsFalse(decodedIntent.AttackPrimaryPressed,
                "TurnInPlace is a zero-translation, no-attack control semantic.");
            Assert.AreEqual(Vector2.zero, decodedIntent.Move);
            Assert.AreEqual(Vector2.zero, decodedIntent.WorldMove);
            Assert.AreEqual(command.Envelope, decodedCommand.Envelope);
        }

        [Test]
        public void JsonCodec_RejectsLegacyStringTraceSchemaBeforePayloadDecode()
        {
            var codec = new JsonNetworkMessageCodec();
            var decoded = codec.Decode(Encoding.UTF8.GetBytes("{\"schemaVersion\":0,\"payload\":null}"));
            Assert.IsFalse(decoded.Success);
            Assert.AreEqual(NetworkCodecErrorCode.UnsupportedSchema, decoded.Error.Code);
        }

        [Test]
        public void JsonCodec_MalformedTypedEnvelopeFailsClosedAsInvalidPayload()
        {
            var codec = new JsonNetworkMessageCodec();
            string json = "{\"schemaVersion\":1,\"sessionId\":\"s\",\"senderPeerId\":\"a\",\"receiverPeerId\":\"b\",\"sequence\":1,\"channel\":0,\"kind\":2,\"frame\":1,\"payload\":{\"inputPeerId\":\"a\",\"clientFrame\":1,\"commands\":[{\"frame\":1,\"sourceId\":8,\"commandId\":100,\"targetId\":0,\"payload0\":0,\"payload1\":0,\"payload2\":0,\"traceId\":{},\"sequence\":1,\"envelope\":{\"typeId\":0,\"schemaVersion\":0,\"provenance\":99}}]}}";

            NetworkCodecDecodeResult decoded = codec.Decode(
                System.Text.Encoding.UTF8.GetBytes(json));

            Assert.IsFalse(decoded.Success);
            Assert.AreEqual(NetworkCodecErrorCode.InvalidPayload, decoded.Error.Code);
        }

        [Test]
        public void JsonCodec_AuthoritativeCommandPacketRoundTripPreservesScheduledCommands()
        {
            var codec = new JsonNetworkMessageCodec();
            var packet = new NetworkAuthoritativeCommandPacket(
                "client-a",
                new RuntimeFrame(12),
                new RuntimeFrame(9),
                new[]
                {
                    new RuntimeCommand(new RuntimeFrame(14), 3, 100, 7, 1, 2, 3, new RuntimeTraceIdentity(970002), 9)
                });
            var message = new NetworkMessageEnvelope(
                "session",
                "server",
                "client-b",
                2,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.AuthoritativeCommands,
                new RuntimeFrame(12),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkAuthoritativeCommandPacket)roundTrip.Message.Payload;
            Assert.AreEqual("client-a", decoded.InputPeerId);
            Assert.AreEqual(new RuntimeFrame(12), decoded.ServerFrame);
            Assert.AreEqual(new RuntimeFrame(9), decoded.AckedInputFrame);
            Assert.AreEqual(1, decoded.Commands.Count);
            Assert.AreEqual(packet.Commands[0], decoded.Commands[0]);
        }

        [Test]
        public void JsonCodec_SnapshotRoundTripPreservesCustomStatePayloadJson()
        {
            var codec = new JsonNetworkMessageCodec();
            string payloadJson = "{\"nested\":{\"value\":3},\"text\":\"keep me\"}";
            var packet = new NetworkSnapshotPacket(
                new RuntimeFrame(20),
                991,
                new RuntimeFrame(19),
                new[] { new RuntimeCustomState(TestCustomStateTypeIds.NetworkTest, "mx.test", 4, payloadJson) },
                fullSnapshot: true);
            var message = new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                5,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.Snapshot,
                new RuntimeFrame(20),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkSnapshotPacket)roundTrip.Message.Payload;
            Assert.AreEqual(991, decoded.ResultHash);
            Assert.AreEqual(1, decoded.ModuleSnapshots.Count);
            Assert.AreEqual(TestCustomStateTypeIds.NetworkTest, decoded.ModuleSnapshots[0].NumericTypeId);
            Assert.AreEqual("mx.test", decoded.ModuleSnapshots[0].TypeId);
            Assert.AreEqual(4, decoded.ModuleSnapshots[0].SchemaVersion);
            Assert.AreEqual(payloadJson, decoded.ModuleSnapshots[0].PayloadJson);
        }

        [Test]
        public void JsonCodec_DigestSnapshotRoundTripPreservesHashWithoutModuleSnapshots()
        {
            var codec = new JsonNetworkMessageCodec();
            var packet = new NetworkSnapshotPacket(
                new RuntimeFrame(20),
                991,
                new RuntimeFrame(19),
                moduleSnapshots: null,
                fullSnapshot: false);
            var message = new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                5,
                NetworkChannel.Unreliable,
                NetworkMessageKind.Snapshot,
                new RuntimeFrame(20),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkSnapshotPacket)roundTrip.Message.Payload;
            Assert.IsFalse(decoded.FullSnapshot);
            Assert.IsTrue(decoded.IsDigestOnly);
            Assert.AreEqual(991, decoded.ResultHash);
            Assert.AreEqual(0, decoded.ModuleSnapshots.Count);
        }

        [Test]
        public void JsonCodec_SnapshotRequestRoundTripPreservesMismatchDiagnostics()
        {
            var codec = new JsonNetworkMessageCodec();
            var packet = new NetworkSnapshotRequestPacket(
                "client",
                new RuntimeFrame(30),
                localHash: 10,
                remoteHash: 20,
                consecutiveMismatchCount: 3,
                reason: "snapshot hash mismatch");
            var message = new NetworkMessageEnvelope(
                "session",
                "client",
                "server",
                6,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.SnapshotRequest,
                new RuntimeFrame(32),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkSnapshotRequestPacket)roundTrip.Message.Payload;
            Assert.AreEqual("client", decoded.PeerId);
            Assert.AreEqual(new RuntimeFrame(30), decoded.AuthoritativeFrame);
            Assert.AreEqual(10, decoded.LocalHash);
            Assert.AreEqual(20, decoded.RemoteHash);
            Assert.AreEqual(3, decoded.ConsecutiveMismatchCount);
            Assert.AreEqual("snapshot hash mismatch", decoded.Reason);
        }

        [Test]
        public void JsonCodec_RoomCommandRoundTripPreservesJoinRequest()
        {
            var codec = new JsonNetworkMessageCodec();
            var packet = new NetworkRoomCommandPacket(
                NetworkRoomCommandKind.Join,
                "client-a",
                displayName: "Alice",
                requestId: 3);
            var message = new NetworkMessageEnvelope(
                "room-a",
                "client-a",
                "server",
                7,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RoomCommand,
                new RuntimeFrame(2),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkRoomCommandPacket)roundTrip.Message.Payload;
            Assert.AreEqual(NetworkRoomCommandKind.Join, decoded.CommandKind);
            Assert.AreEqual("client-a", decoded.PeerId);
            Assert.AreEqual("Alice", decoded.DisplayName);
            Assert.AreEqual(3, decoded.RequestId);
        }

        [Test]
        public void JsonCodec_RoomStateRoundTripPreservesMembersAndGameplaySession()
        {
            var codec = new JsonNetworkMessageCodec();
            var gameplayConfig = new NetworkSessionConfig(
                2,
                60,
                2,
                4,
                "config",
                "resources",
                NetworkStateSyncPolicy.BootstrapOnly,
                heartbeatIntervalFrames: 20);
            var roomConfig = new NetworkRoomConfig(
                "room-a",
                "LAN Room",
                maxMembers: 4,
                minReadyMembers: 2,
                requireAllReady: true,
                allowLateJoin: false,
                gameplaySessionConfig: gameplayConfig);
            var state = new NetworkRoomState(
                NetworkRoomStatus.InGame,
                roomConfig,
                "server",
                "room-a-game-10",
                new[]
                {
                    new NetworkRoomMember("client-a", "Alice", 0, ready: true, connected: true, joinedFrame: new RuntimeFrame(1)),
                    new NetworkRoomMember("client-b", "Bob", 1, ready: true, connected: true, joinedFrame: new RuntimeFrame(2))
                },
                stateVersion: 5,
                lastReason: "game started");
            var message = new NetworkMessageEnvelope(
                "room-a",
                "server",
                "client-a",
                9,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RoomState,
                new RuntimeFrame(10),
                new NetworkRoomStatePacket(state, accepted: true, requestId: 4));

            NetworkCodecDecodeResult roundTrip = RoundTrip(codec, message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkRoomStatePacket)roundTrip.Message.Payload;
            Assert.IsTrue(decoded.Accepted);
            Assert.AreEqual(4, decoded.RequestId);
            Assert.AreEqual(NetworkRoomStatus.InGame, decoded.State.Status);
            Assert.AreEqual("room-a-game-10", decoded.State.GameplaySessionId);
            Assert.AreEqual(NetworkStateSyncPolicy.BootstrapOnly, decoded.State.Config.GameplaySessionConfig.StateSyncPolicy);
            Assert.AreEqual(20, decoded.State.Config.GameplaySessionConfig.HeartbeatIntervalFrames);
            Assert.AreEqual(2, decoded.State.Members.Count);
            Assert.AreEqual("client-a", decoded.State.Members[0].PeerId);
            Assert.AreEqual("Alice", decoded.State.Members[0].DisplayName);
            Assert.AreEqual("client-b", decoded.State.Members[1].PeerId);
            Assert.AreEqual("resources", decoded.State.Config.GameplaySessionConfig.ResourceCatalogHash);
        }

        [Test]
        public void JsonCodec_ReturnsStructuredErrorsForInvalidWireData()
        {
            var codec = new JsonNetworkMessageCodec();
            var wrongPayload = new NetworkMessageEnvelope(
                "session",
                "client",
                "server",
                1,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RuntimeCommands,
                RuntimeFrame.Zero,
                new object());

            NetworkCodecEncodeResult encode = codec.Encode(wrongPayload);
            Assert.IsFalse(encode.Success);
            Assert.AreEqual(NetworkCodecErrorCode.PayloadTypeMismatch, encode.Error.Code);

            NetworkCodecDecodeResult badJson = codec.Decode(Encoding.UTF8.GetBytes("{not-json"));
            Assert.IsFalse(badJson.Success);
            Assert.AreEqual(NetworkCodecErrorCode.InvalidJson, badJson.Error.Code);

            NetworkCodecDecodeResult unknownKind = codec.Decode(Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"sessionId\":\"s\",\"senderPeerId\":\"a\",\"receiverPeerId\":\"b\",\"sequence\":0,\"channel\":0,\"kind\":999,\"frame\":0,\"payload\":{}}"));
            Assert.IsFalse(unknownKind.Success);
            Assert.AreEqual(NetworkCodecErrorCode.UnsupportedMessageKind, unknownKind.Error.Code);

            NetworkCodecDecodeResult schemaMismatch = codec.Decode(Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":99,\"sessionId\":\"s\",\"senderPeerId\":\"a\",\"receiverPeerId\":\"b\",\"sequence\":0,\"channel\":0,\"kind\":0,\"frame\":0,\"payload\":{}}"));
            Assert.IsFalse(schemaMismatch.Success);
            Assert.AreEqual(NetworkCodecErrorCode.UnsupportedSchema, schemaMismatch.Error.Code);

            NetworkCodecDecodeResult invalidRoomCommand = codec.Decode(Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"sessionId\":\"room\",\"senderPeerId\":\"client\",\"receiverPeerId\":\"server\",\"sequence\":0,\"channel\":0,\"kind\":7,\"frame\":0,\"payload\":{\"commandKind\":999,\"peerId\":\"client\",\"displayName\":\"\",\"ready\":false,\"requestId\":0}}"));
            Assert.IsFalse(invalidRoomCommand.Success);
            Assert.AreEqual(NetworkCodecErrorCode.InvalidPayload, invalidRoomCommand.Error.Code);
        }

        [Test]
        public void JsonCodec_ApplicationControlRequestRoundTripPreservesTypedPayload()
        {
            var packet = new NetworkApplicationControlRequestPacket(
                "character-test.deck",
                2,
                17L,
                "{\"characterStableId\":\"hero\"}");
            var message = new NetworkMessageEnvelope(
                "session",
                "client",
                "server",
                5L,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.ApplicationControlRequest,
                new RuntimeFrame(2),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(new JsonNetworkMessageCodec(), message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkApplicationControlRequestPacket)roundTrip.Message.Payload;
            Assert.AreEqual(packet.TypeId, decoded.TypeId);
            Assert.AreEqual(packet.SchemaVersion, decoded.SchemaVersion);
            Assert.AreEqual(packet.RequestId, decoded.RequestId);
            Assert.AreEqual(packet.PayloadJson, decoded.PayloadJson);
        }

        [Test]
        public void JsonCodec_ApplicationControlResponseRoundTripPreservesDecision()
        {
            var packet = new NetworkApplicationControlResponsePacket(
                "character-test.deck",
                2,
                17L,
                accepted: false,
                reason: "locked",
                payloadJson: "{\"code\":\"LOCKED\"}");
            var message = new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                6L,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.ApplicationControlResponse,
                new RuntimeFrame(2),
                packet);

            NetworkCodecDecodeResult roundTrip = RoundTrip(new JsonNetworkMessageCodec(), message);

            Assert.IsTrue(roundTrip.Success, roundTrip.Error.ToString());
            var decoded = (NetworkApplicationControlResponsePacket)roundTrip.Message.Payload;
            Assert.IsFalse(decoded.Accepted);
            Assert.AreEqual("locked", decoded.Reason);
            Assert.AreEqual(packet.PayloadJson, decoded.PayloadJson);
        }

        private static NetworkCodecDecodeResult RoundTrip(INetworkMessageCodec codec, NetworkMessageEnvelope message)
        {
            NetworkCodecEncodeResult encode = codec.Encode(message);
            Assert.IsTrue(encode.Success, encode.Error.ToString());
            return codec.Decode(encode.Bytes);
        }
    }
}
