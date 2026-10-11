using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using MxFramework.Network;
using MxFramework.Network.UnityTransport;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Network
{
    [Parallelizable(ParallelScope.None)]
    public sealed class UnityTransportNetworkTransportTests
    {
        private static class TestTraceIds
        {
            internal static readonly RuntimeTraceIdentity ClientACommand = new RuntimeTraceIdentity(91022001); // client-a-command
            internal static readonly RuntimeTraceIdentity ClientBAfterDisconnect = new RuntimeTraceIdentity(91022002); // client-b-after-disconnect
            internal static readonly RuntimeTraceIdentity ClientBCommand = new RuntimeTraceIdentity(91022003); // client-b-command
            internal static readonly RuntimeTraceIdentity UtpCommand = new RuntimeTraceIdentity(91022004); // utp-command
        }

        [Test]
        public void UnityTransport_RunsHandshakeCommandAndSnapshot()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                var buffer = new RuntimeCommandBuffer();
                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var serverState = new RecordingSnapshotParticipant("mx.test", "server");
                var clientState = new RecordingSnapshotParticipant("mx.test", "client");
                var server = CreateServer(config, serverTransport, buffer, serverState, frame => 7);
                var client = CreateClient(config, clientTransport, clientState, frame => 1);

                ConnectSession(server, client, 16);

                client.SubmitCommands(
                    new RuntimeFrame(80),
                    new[] { new RuntimeCommand(new RuntimeFrame(80), 10, 20, 30, traceId: TestTraceIds.UtpCommand) });
                server.ProcessIncoming(new RuntimeFrame(80));

                RuntimeCommandBufferSnapshot pending = buffer.CaptureSnapshot();
                Assert.AreEqual(
                    1,
                    pending.PendingCount,
                    DescribeTransportFailure(server, serverTransport, clientTransport, "client"));
                IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(
                    pending.PendingCommands[0].Frame);
                Assert.AreEqual(TestTraceIds.UtpCommand, commands[0].TraceId);

                server.BroadcastSnapshot(new RuntimeFrame(96));
                client.ProcessIncoming(new RuntimeFrame(96));

                Assert.AreEqual("server", clientState.Value);
                Assert.AreEqual(7, client.LastSnapshotHash);
                Assert.AreEqual(1, client.ResyncCount);
                Assert.AreEqual(0, clientTransport.CreateSnapshot("client").CodecErrorCount);
            }
        }

        [Test]
        public void UnityTransport_BoundConnectionRejectsForgedEnvelopeSender()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport, "client-a");

                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var server = CreateServer(config, serverTransport, new RuntimeCommandBuffer());
                var client = CreateClient(config, clientTransport, null, peerId: "client-a");
                ConnectSession(server, client, 16);
                int handled = 0;
                server.SetApplicationControlHandler((peerId, request) =>
                {
                    handled++;
                    return new NetworkApplicationControlResponsePacket(
                        request.TypeId,
                        request.SchemaVersion,
                        request.RequestId,
                        accepted: true,
                        reason: string.Empty);
                });

                clientTransport.Send(new NetworkMessageEnvelope(
                    "session",
                    "client-victim",
                    "server",
                    99L,
                    NetworkChannel.ReliableOrdered,
                    NetworkMessageKind.ApplicationControlRequest,
                    new RuntimeFrame(100),
                    new NetworkApplicationControlRequestPacket(
                        "mx.test.control",
                        1,
                        9L,
                        "{}")));
                server.ProcessIncoming(new RuntimeFrame(100));

                Assert.AreEqual(0, handled);
                Assert.IsFalse(server.IsPeerConnected("client-victim"));
                Assert.IsFalse(server.IsPeerConnected("client-a"));
            }
        }

        [Test]
        public void UnityTransport_ServerHandlesTwoClientsAndOneDisconnect()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientATransport = new UnityTransportNetworkTransport())
            using (var clientBTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientATransport.StartClient("127.0.0.1", port));
                Assert.IsTrue(clientBTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientATransport, "client-a");
                WaitForTransportConnection(serverTransport, clientBTransport, "client-b");

                var buffer = new RuntimeCommandBuffer();
                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var server = CreateServer(config, serverTransport, buffer);
                var clientA = CreateClient(config, clientATransport, null, peerId: "client-a");
                var clientB = CreateClient(config, clientBTransport, null, peerId: "client-b");
                ConnectSessions(server, clientA, clientB, 16);

                Assert.AreEqual(2, server.ConnectedPeerCount);

                clientA.SubmitCommands(
                    new RuntimeFrame(80),
                    new[] { new RuntimeCommand(new RuntimeFrame(80), 10, 20, 1, traceId: TestTraceIds.ClientACommand) });
                clientB.SubmitCommands(
                    new RuntimeFrame(80),
                    new[] { new RuntimeCommand(new RuntimeFrame(80), 10, 20, 2, traceId: TestTraceIds.ClientBCommand) });
                server.ProcessIncoming(new RuntimeFrame(80));

                RuntimeCommandBufferSnapshot pending = buffer.CaptureSnapshot();
                Assert.AreEqual(
                    2,
                    pending.PendingCount,
                    DescribeTransportFailure(server, serverTransport, clientATransport, "client-a"));
                RuntimeFrame scheduledFrame = pending.PendingCommands[0].Frame;
                Assert.AreEqual(scheduledFrame, pending.PendingCommands[1].Frame);
                IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(scheduledFrame);
                Assert.AreEqual(new RuntimeFrame(80), server.GetLastAckedInputFrame("client-a"));
                Assert.AreEqual(new RuntimeFrame(80), server.GetLastAckedInputFrame("client-b"));

                clientATransport.Disconnect("server");
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = new RuntimeFrame(90 + i);
                    server.ProcessIncoming(frame);
                    clientB.ProcessIncoming(frame);
                    if (server.ConnectedPeerCount == 1)
                        break;
                }

                Assert.AreEqual(1, server.ConnectedPeerCount);
                Assert.IsTrue(server.IsPeerConnected("client-b"));

                clientB.SubmitCommands(
                    new RuntimeFrame(180),
                    new[] { new RuntimeCommand(new RuntimeFrame(180), 10, 20, 2, traceId: TestTraceIds.ClientBAfterDisconnect) });
                server.ProcessIncoming(new RuntimeFrame(180));

                RuntimeCommandBufferSnapshot afterDisconnectPending = buffer.CaptureSnapshot();
                Assert.AreEqual(1, afterDisconnectPending.PendingCount);
                IReadOnlyList<RuntimeCommand> afterDisconnect = buffer.DrainForFrame(
                    afterDisconnectPending.PendingCommands[0].Frame);
                Assert.AreEqual(1, afterDisconnect.Count);
                Assert.AreEqual(TestTraceIds.ClientBAfterDisconnect, afterDisconnect[0].TraceId);
            }
        }

        [Test]
        public void UnityTransport_RoomSessionHandlesTwoClientsReadyAndStart()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientATransport = new UnityTransportNetworkTransport())
            using (var clientBTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientATransport.StartClient("127.0.0.1", port));
                Assert.IsTrue(clientBTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientATransport, "client-a");
                WaitForTransportConnection(serverTransport, clientBTransport, "client-b");

                var gameplayConfig = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
                var roomConfig = new NetworkRoomConfig(
                    "utp-room",
                    "UTP Room",
                    maxMembers: 4,
                    minReadyMembers: 2,
                    requireAllReady: true,
                    allowLateJoin: false,
                    gameplaySessionConfig: gameplayConfig);
                var server = new NetworkRoomSessionRunner(
                    NetworkSessionRole.Server,
                    "server",
                    "server",
                    roomConfig,
                    serverTransport);
                var clientA = new NetworkRoomSessionRunner(
                    NetworkSessionRole.Client,
                    "client-a",
                    "server",
                    roomConfig,
                    clientATransport);
                var clientB = new NetworkRoomSessionRunner(
                    NetworkSessionRole.Client,
                    "client-b",
                    "server",
                    roomConfig,
                    clientBTransport);

                server.OpenRoom(RuntimeFrame.Zero);
                clientA.JoinRoom(new RuntimeFrame(10), "Alice");
                clientB.JoinRoom(new RuntimeFrame(10), "Bob");
                WaitForRoomMembers(server, clientA, clientB, expectedMembers: 2, startFrame: 10);

                clientA.SetReady(new RuntimeFrame(80), true);
                clientB.SetReady(new RuntimeFrame(80), true);
                WaitForRoomStart(server, clientA, clientB, startFrame: 80);

                Assert.AreEqual(NetworkRoomStatus.InGame, server.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, clientA.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, clientB.Status);
                Assert.AreEqual("utp-room-game", clientA.State.GameplaySessionId);
                Assert.AreEqual(2, server.State.ReadyMemberCount);
                Assert.Greater(serverTransport.CreateSnapshot("server").RoomSentBytes, 0);
                Assert.Greater(clientATransport.CreateSnapshot("client-a").RoomSentBytes, 0);
            }
        }

        [Test]
        public void UnityTransport_DisconnectEventUpdatesRunnerStatus()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var server = CreateServer(config, serverTransport, new RuntimeCommandBuffer());
                var client = CreateClient(config, clientTransport, null);
                ConnectSession(server, client, 16);

                clientTransport.Disconnect("server");
                client.ProcessIncoming(new RuntimeFrame(90));

                Assert.AreEqual(NetworkSessionStatus.Disconnected, client.Status);
                StringAssert.Contains("disconnect", client.DisconnectReason);
            }
        }

        [Test]
        public void UnityTransport_ServerDropsPeerAfterClientDisconnect()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var serverState = new RecordingSnapshotParticipant("mx.test", "server");
                var server = CreateServer(config, serverTransport, new RuntimeCommandBuffer(), serverState, frame => 7);
                var client = CreateClient(config, clientTransport, null);
                ConnectSession(server, client, 16);

                Assert.AreEqual(1, server.ConnectedPeerCount);

                clientTransport.Disconnect("server");
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = new RuntimeFrame(90 + i);
                    server.ProcessIncoming(frame);
                    if (server.ConnectedPeerCount == 0)
                        break;
                }

                Assert.AreEqual(0, server.ConnectedPeerCount);
                server.BroadcastSnapshot(new RuntimeFrame(160));

                var events = new List<NetworkTransportEvent>();
                serverTransport.PollEvents("server", new RuntimeFrame(160), events);
                AssertNoTransportErrors(events);
            }
        }

        [Test]
        public void UnityTransport_ServerDropsPeerAfterClientTransportDispose()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var serverState = new RecordingSnapshotParticipant("mx.test", "server");
                var server = CreateServer(config, serverTransport, new RuntimeCommandBuffer(), serverState, frame => 7);
                var client = CreateClient(config, clientTransport, null);
                ConnectSession(server, client, 16);

                Assert.AreEqual(1, server.ConnectedPeerCount);

                clientTransport.Dispose();
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = new RuntimeFrame(90 + i);
                    server.ProcessIncoming(frame);
                    if (server.ConnectedPeerCount == 0)
                        break;
                }

                Assert.AreEqual(0, server.ConnectedPeerCount);
                server.BroadcastSnapshot(new RuntimeFrame(160));

                var events = new List<NetworkTransportEvent>();
                serverTransport.PollEvents("server", new RuntimeFrame(160), events);
                AssertNoTransportErrors(events);
            }
        }

        [Test]
        public void UnityTransport_RestartClientCreatesNewTransportConnection()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                clientTransport.Disconnect("server");
                PumpTransportEvents(serverTransport, clientTransport, 90, 16);

                Assert.IsTrue(clientTransport.RestartClient());
                WaitForTransportConnection(serverTransport, clientTransport);
            }
        }

        [Test]
        public void UnityTransport_SamePeerCanHandshakeAfterClientProcessRestart()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
                var server = CreateServer(config, serverTransport, new RuntimeCommandBuffer());
                var firstClient = CreateClient(config, clientTransport, null);
                ConnectSession(server, firstClient, 16);

                Assert.AreEqual(1, server.ConnectedPeerCount);

                clientTransport.Dispose();
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = new RuntimeFrame(90 + i);
                    server.ProcessIncoming(frame);
                    if (server.ConnectedPeerCount == 0)
                        break;
                }

                Assert.AreEqual(0, server.ConnectedPeerCount);

                using (var secondClientTransport = new UnityTransportNetworkTransport())
                {
                    Assert.IsTrue(secondClientTransport.StartClient("127.0.0.1", port));
                    WaitForTransportConnection(serverTransport, secondClientTransport);

                    var secondClient = CreateClient(config, secondClientTransport, null);
                    ConnectSession(server, secondClient, 180);

                    Assert.AreEqual(NetworkSessionStatus.Connected, secondClient.Status);
                    Assert.AreEqual(1, server.ConnectedPeerCount);
                }
            }
        }

        [Test]
        public void UnityTransport_QueuesSendsUntilConnectionIsReady()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));

                clientTransport.Send(CreateHandshakeEnvelope("client", "server"));

                var events = new List<NetworkTransportEvent>();
                var messages = new List<NetworkMessageEnvelope>();
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = new RuntimeFrame(i);
                    events.Clear();
                    serverTransport.PollEvents("server", frame, events);
                    clientTransport.PollEvents("client", frame, events);
                    AssertNoTransportErrors(events);

                    messages.Clear();
                    serverTransport.Poll("server", frame, messages);
                    if (messages.Count == 0)
                        continue;

                    Assert.AreEqual(1, messages.Count);
                    Assert.AreEqual(NetworkMessageKind.HandshakeRequest, messages[0].Kind);
                    Assert.AreEqual("client", messages[0].SenderPeerId);
                    Assert.AreEqual("server", messages[0].ReceiverPeerId);
                    Assert.AreEqual(0, clientTransport.CreateSnapshot("client").DroppedCount);
                    return;
                }

                Assert.Fail("Queued Unity Transport send was not delivered after connection became ready.");
            }
        }

        [Test]
        public void UnityTransport_PendingSendFlushHandlesSendCleanup()
        {
            ushort port = GetAvailableUdpPort();
            var clientCodec = new PendingSendCleanupCodec();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport(clientCodec))
            {
                clientCodec.Attach(clientTransport);
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port));
                WaitForTransportConnection(serverTransport, clientTransport);

                QueuePendingSendForTest(clientTransport, CreateHandshakeEnvelope("client", "server"));

                var events = new List<NetworkTransportEvent>();
                Assert.DoesNotThrow(() => clientTransport.PollEvents("client", new RuntimeFrame(100), events));
                Assert.AreEqual(0, clientTransport.CreateSnapshot("client").PendingMessageCount);
            }
        }

        [Test]
        public void UnityTransport_SendErrorsAreStructuredTransportEvents()
        {
            ushort oversizedPort = GetAvailableUdpPort();
            using (var transport = new UnityTransportNetworkTransport(
                new UnityTransportNetworkOptions(maxPayloadBytes: 1)))
            {
                Assert.IsTrue(transport.StartServer(oversizedPort, "127.0.0.1"));
                transport.Send(CreateHandshakeEnvelope("client", "server"));

                var events = new List<NetworkTransportEvent>();
                transport.PollEvents("server", RuntimeFrame.Zero, events);

                AssertErrorContains(events, "maxPayloadBytes");
                Assert.Greater(transport.CreateSnapshot("server").DroppedCount, 0);
            }

            ushort missingPeerPort = GetAvailableUdpPort();
            using (var transport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(transport.StartServer(missingPeerPort, "127.0.0.1"));
                transport.Send(CreateHandshakeEnvelope("server", "missing"));

                var events = new List<NetworkTransportEvent>();
                transport.PollEvents("server", RuntimeFrame.Zero, events);

                AssertErrorContains(events, "no connection");
                Assert.Greater(transport.CreateSnapshot("server").DroppedCount, 0);
            }
        }

        private static void WaitForTransportConnection(
            UnityTransportNetworkTransport serverTransport,
            UnityTransportNetworkTransport clientTransport,
            string clientPeerId = "client")
        {
            var events = new List<NetworkTransportEvent>();
            for (int i = 0; i < 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                events.Clear();
                serverTransport.PollEvents("server", frame, events);
                clientTransport.PollEvents(clientPeerId, frame, events);
                for (int j = 0; j < events.Count; j++)
                {
                    if (events[j].Kind == NetworkTransportEventKind.Connected
                        && string.Equals(events[j].PeerId, "server", System.StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            Assert.Fail("Unity Transport client did not connect to loopback server.");
        }

        private static void PumpTransportEvents(
            UnityTransportNetworkTransport serverTransport,
            UnityTransportNetworkTransport clientTransport,
            int startFrame,
            int count)
        {
            var events = new List<NetworkTransportEvent>();
            for (int i = 0; i < count; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                events.Clear();
                serverTransport.PollEvents("server", frame, events);
                clientTransport.PollEvents("client", frame, events);
            }
        }

        private static void ConnectSession(
            NetworkSessionRunner server,
            NetworkSessionRunner client,
            int startFrame)
        {
            client.Connect(new RuntimeFrame(startFrame));
            for (int i = 0; i < 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                server.ProcessIncoming(frame);
                client.ProcessIncoming(frame);
                if (client.Status == NetworkSessionStatus.Connected)
                    return;
            }

            Assert.Fail("Network session did not complete handshake. Reason: " + client.RejectionReason);
        }

        private static void ConnectSessions(
            NetworkSessionRunner server,
            NetworkSessionRunner clientA,
            NetworkSessionRunner clientB,
            int startFrame)
        {
            clientA.Connect(new RuntimeFrame(startFrame));
            clientB.Connect(new RuntimeFrame(startFrame));
            for (int i = 0; i < 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                server.ProcessIncoming(frame);
                clientA.ProcessIncoming(frame);
                clientB.ProcessIncoming(frame);
                if (clientA.Status == NetworkSessionStatus.Connected
                    && clientB.Status == NetworkSessionStatus.Connected)
                {
                    return;
                }
            }

            Assert.Fail(
                "Network sessions did not complete handshake. A="
                + clientA.Status
                + " B="
                + clientB.Status);
        }

        private static void WaitForRoomMembers(
            NetworkRoomSessionRunner server,
            NetworkRoomSessionRunner clientA,
            NetworkRoomSessionRunner clientB,
            int expectedMembers,
            int startFrame)
        {
            for (int i = 0; i < 128; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                server.ProcessIncoming(frame);
                clientA.ProcessIncoming(frame);
                clientB.ProcessIncoming(frame);
                if (clientA.State.Members.Count == expectedMembers
                    && clientB.State.Members.Count == expectedMembers)
                {
                    return;
                }
            }

            Assert.Fail("Room members did not converge.");
        }

        private static void WaitForRoomStart(
            NetworkRoomSessionRunner server,
            NetworkRoomSessionRunner clientA,
            NetworkRoomSessionRunner clientB,
            int startFrame)
        {
            for (int i = 0; i < 128; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                server.ProcessIncoming(frame);
                server.TryStartGame(frame, out _);
                clientA.ProcessIncoming(frame);
                clientB.ProcessIncoming(frame);
                if (clientA.Status == NetworkRoomStatus.InGame
                    && clientB.Status == NetworkRoomStatus.InGame)
                {
                    return;
                }
            }

            Assert.Fail("Room did not start.");
        }

        private static void AssertErrorContains(List<NetworkTransportEvent> events, string expected)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == NetworkTransportEventKind.Error
                    && events[i].Message.IndexOf(expected, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
            }

            Assert.Fail("Expected Unity Transport error containing: " + expected);
        }

        private static void QueuePendingSendForTest(
            UnityTransportNetworkTransport transport,
            NetworkMessageEnvelope message)
        {
            var pendingSends = (List<NetworkMessageEnvelope>)PendingSendsField.GetValue(transport);
            pendingSends.Add(message);
        }

        private static void AssertNoTransportErrors(List<NetworkTransportEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == NetworkTransportEventKind.Error)
                    Assert.Fail("Unexpected Unity Transport error: " + events[i].Message);
            }
        }

        private static NetworkSessionRunner CreateServer(
            NetworkSessionConfig config,
            INetworkTransport transport,
            RuntimeCommandBuffer buffer,
            INetworkSnapshotParticipant participant = null,
            System.Func<RuntimeFrame, long> hashProvider = null)
        {
            return new NetworkSessionRunner(
                NetworkSessionRole.Server,
                "session",
                "server",
                "server",
                config,
                transport,
                buffer,
                participant == null ? null : new[] { participant },
                hashProvider);
        }

        private static NetworkSessionRunner CreateClient(
            NetworkSessionConfig config,
            INetworkTransport transport,
            INetworkSnapshotParticipant participant,
            System.Func<RuntimeFrame, long> hashProvider = null,
            string peerId = "client")
        {
            return new NetworkSessionRunner(
                NetworkSessionRole.Client,
                "session",
                peerId,
                "server",
                config,
                transport,
                null,
                participant == null ? null : new[] { participant },
                hashProvider);
        }

        private static NetworkMessageEnvelope CreateHandshakeEnvelope(string senderPeerId, string receiverPeerId)
        {
            return new NetworkMessageEnvelope(
                "session",
                senderPeerId,
                receiverPeerId,
                0,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.HandshakeRequest,
                RuntimeFrame.Zero,
                new NetworkHandshakeRequest(
                    senderPeerId,
                    NetworkSessionRole.Client,
                    NetworkSessionConfig.Default));
        }

        private static string DescribeTransportFailure(
            NetworkSessionRunner server,
            UnityTransportNetworkTransport serverTransport,
            UnityTransportNetworkTransport clientTransport,
            string clientPeerId)
        {
            NetworkTransportSnapshot serverSnapshot = serverTransport.CreateSnapshot("server");
            NetworkTransportSnapshot clientSnapshot = clientTransport.CreateSnapshot(clientPeerId);
            return "server diagnostics: " + string.Join(" | ", server.RecentDiagnostics)
                + "; server delivered=" + serverSnapshot.DeliveredCount
                + " dropped=" + serverSnapshot.DroppedCount
                + "; client sent=" + clientSnapshot.SentCount
                + " dropped=" + clientSnapshot.DroppedCount;
        }

        private static ushort GetAvailableUdpPort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        private static readonly FieldInfo PendingSendsField = typeof(UnityTransportNetworkTransport).GetField(
            "_pendingSends",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo RemovePendingSendsForPeerMethod = typeof(UnityTransportNetworkTransport).GetMethod(
            "RemovePendingSendsForPeer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class PendingSendCleanupCodec : INetworkMessageCodec
        {
            private readonly INetworkMessageCodec _inner = new JsonNetworkMessageCodec();
            private UnityTransportNetworkTransport _transport;

            public string CodecId => _inner.CodecId;

            public void Attach(UnityTransportNetworkTransport transport)
            {
                _transport = transport;
            }

            public NetworkCodecEncodeResult Encode(NetworkMessageEnvelope message)
            {
                RemovePendingSendsForPeerMethod.Invoke(_transport, new object[] { message.ReceiverPeerId });
                return _inner.Encode(message);
            }

            public NetworkCodecDecodeResult Decode(byte[] bytes)
            {
                return _inner.Decode(bytes);
            }
        }

        private sealed class RecordingSnapshotParticipant : INetworkSnapshotParticipant
        {
            public RecordingSnapshotParticipant(string participantId, string value)
            {
                ParticipantId = participantId;
                Value = value;
            }

            public string ParticipantId { get; }
            public string Value { get; private set; }

            public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame)
            {
                return new RuntimeCustomState(TestCustomStateTypeIds.NetworkParticipant, ParticipantId, 0, Value);
            }

            public NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot)
            {
                if (snapshot.SchemaVersion != 0)
                    return NetworkSnapshotApplyResult.Failed("unsupported schema");

                Value = snapshot.PayloadJson;
                return NetworkSnapshotApplyResult.Succeeded();
            }
        }
    }
}
