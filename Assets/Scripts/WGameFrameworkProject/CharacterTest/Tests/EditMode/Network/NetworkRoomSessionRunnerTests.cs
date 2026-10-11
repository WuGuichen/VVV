using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Network
{
    public sealed class NetworkRoomSessionRunnerTests
    {
        [Test]
        public void RoomJoin_CachesAuthoritativeStateOnClient()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room");
            var server = CreateServer(config, transport);
            var client = CreateClient(config, transport, "client-a");

            server.OpenRoom(RuntimeFrame.Zero);
            client.JoinRoom(RuntimeFrame.Zero, "Alice");
            Pump(server, client, RuntimeFrame.Zero);

            Assert.AreEqual(NetworkRoomStatus.Open, client.State.Status);
            Assert.AreEqual(1, client.State.Members.Count);
            Assert.AreEqual("client-a", client.State.Members[0].PeerId);
            Assert.AreEqual("Alice", client.State.Members[0].DisplayName);
            Assert.AreEqual(0, client.State.Members[0].SlotIndex);
            Assert.IsTrue(client.LastResponseAccepted);
            Assert.AreEqual(1, server.Diagnostics.JoinCount);
        }

        [Test]
        public void DuplicatePeerReconnect_ReusesSlotAndResetsReady()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room");
            var server = CreateServer(config, transport);
            var client = CreateClient(config, transport, "client-a");

            server.OpenRoom(RuntimeFrame.Zero);
            client.JoinRoom(RuntimeFrame.Zero, "Alice");
            Pump(server, client, RuntimeFrame.Zero);
            client.SetReady(new RuntimeFrame(1), true);
            Pump(server, client, new RuntimeFrame(1));

            Assert.IsTrue(client.State.Members[0].Ready);

            transport.QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Disconnected,
                "client-a",
                new RuntimeFrame(2),
                "test disconnect"));
            server.ProcessIncoming(new RuntimeFrame(2));
            client.ProcessIncoming(new RuntimeFrame(2));

            Assert.IsFalse(server.State.Members[0].Connected);
            Assert.IsFalse(server.State.Members[0].Ready);

            client.JoinRoom(new RuntimeFrame(3), "Alice 2");
            Pump(server, client, new RuntimeFrame(3));

            Assert.AreEqual(1, client.State.Members.Count);
            Assert.AreEqual(0, client.State.Members[0].SlotIndex);
            Assert.AreEqual("Alice 2", client.State.Members[0].DisplayName);
            Assert.IsTrue(client.State.Members[0].Connected);
            Assert.IsFalse(client.State.Members[0].Ready);
            Assert.AreEqual(1, server.Diagnostics.DisconnectCount);
        }

        [Test]
        public void RoomJoin_RejectsWhenFull()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room", maxMembers: 1);
            var server = CreateServer(config, transport);
            var clientA = CreateClient(config, transport, "client-a");
            var clientB = CreateClient(config, transport, "client-b");

            server.OpenRoom(RuntimeFrame.Zero);
            clientA.JoinRoom(RuntimeFrame.Zero, "Alice");
            Pump(server, clientA, RuntimeFrame.Zero);

            clientB.JoinRoom(new RuntimeFrame(1), "Bob");
            server.ProcessIncoming(new RuntimeFrame(1));
            clientB.ProcessIncoming(new RuntimeFrame(1));

            Assert.IsFalse(clientB.LastResponseAccepted);
            StringAssert.Contains("full", clientB.LastResponseReason);
            Assert.AreEqual(1, server.State.Members.Count);
            Assert.AreEqual(1, server.Diagnostics.RejectionCount);
        }

        [Test]
        public void ReadyAndStart_GameStartsOnlyAfterRequiredMembersReady()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room", maxMembers: 4, minReadyMembers: 2);
            var server = CreateServer(config, transport);
            var clientA = CreateClient(config, transport, "client-a");
            var clientB = CreateClient(config, transport, "client-b");

            server.OpenRoom(RuntimeFrame.Zero);
            clientA.JoinRoom(RuntimeFrame.Zero, "Alice");
            clientB.JoinRoom(RuntimeFrame.Zero, "Bob");
            Pump(server, clientA, clientB, RuntimeFrame.Zero);

            clientA.SetReady(new RuntimeFrame(1), true);
            Pump(server, clientA, clientB, new RuntimeFrame(1));

            Assert.IsFalse(server.TryStartGame(new RuntimeFrame(2), out string reason));
            StringAssert.Contains("ready", reason);

            clientB.SetReady(new RuntimeFrame(3), true);
            Pump(server, clientA, clientB, new RuntimeFrame(3));
            Assert.IsTrue(server.TryStartGame(new RuntimeFrame(4), out reason), reason);
            clientA.ProcessIncoming(new RuntimeFrame(4));
            clientB.ProcessIncoming(new RuntimeFrame(4));

            Assert.AreEqual(NetworkRoomStatus.InGame, server.State.Status);
            Assert.AreEqual(NetworkRoomStatus.InGame, clientA.State.Status);
            Assert.AreEqual(NetworkRoomStatus.InGame, clientB.State.Status);
            Assert.AreEqual("room-a-game", clientA.State.GameplaySessionId);
            Assert.AreEqual(1, server.Diagnostics.StartCount);
        }

        [Test]
        public void ClientStartRequest_IsRejectedByServer()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room");
            var server = CreateServer(config, transport);
            var client = CreateClient(config, transport, "client-a");

            server.OpenRoom(RuntimeFrame.Zero);
            client.JoinRoom(RuntimeFrame.Zero, "Alice");
            Pump(server, client, RuntimeFrame.Zero);

            client.RequestStartGame(new RuntimeFrame(1));
            Pump(server, client, new RuntimeFrame(1));

            Assert.AreEqual(NetworkRoomStatus.Open, server.State.Status);
            Assert.IsFalse(client.LastResponseAccepted);
            StringAssert.Contains("server", client.LastResponseReason);
        }

        [Test]
        public void LateJoin_DefaultRejectedAfterGameStarted()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room");
            var server = CreateServer(config, transport);
            var clientA = CreateClient(config, transport, "client-a");
            var clientB = CreateClient(config, transport, "client-b");

            server.OpenRoom(RuntimeFrame.Zero);
            clientA.JoinRoom(RuntimeFrame.Zero, "Alice");
            Pump(server, clientA, RuntimeFrame.Zero);
            clientA.SetReady(new RuntimeFrame(1), true);
            Pump(server, clientA, new RuntimeFrame(1));
            Assert.IsTrue(server.TryStartGame(new RuntimeFrame(2), out string reason), reason);
            clientA.ProcessIncoming(new RuntimeFrame(2));

            clientB.JoinRoom(new RuntimeFrame(3), "Bob");
            server.ProcessIncoming(new RuntimeFrame(3));
            clientB.ProcessIncoming(new RuntimeFrame(3));

            Assert.IsFalse(clientB.LastResponseAccepted);
            StringAssert.Contains("in game", clientB.LastResponseReason);
        }

        [Test]
        public void LateJoin_CanBeAllowedAfterGameStarted()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkRoomConfig("room-a", "LAN Room", allowLateJoin: true);
            var server = CreateServer(config, transport);
            var clientA = CreateClient(config, transport, "client-a");
            var clientB = CreateClient(config, transport, "client-b");

            server.OpenRoom(RuntimeFrame.Zero);
            clientA.JoinRoom(RuntimeFrame.Zero, "Alice");
            Pump(server, clientA, RuntimeFrame.Zero);
            clientA.SetReady(new RuntimeFrame(1), true);
            Pump(server, clientA, new RuntimeFrame(1));
            Assert.IsTrue(server.TryStartGame(new RuntimeFrame(2), out string reason), reason);
            clientA.ProcessIncoming(new RuntimeFrame(2));

            clientB.JoinRoom(new RuntimeFrame(3), "Bob");
            server.ProcessIncoming(new RuntimeFrame(3));
            clientB.ProcessIncoming(new RuntimeFrame(3));

            Assert.IsTrue(clientB.LastResponseAccepted, clientB.LastResponseReason);
            Assert.AreEqual(NetworkRoomStatus.InGame, clientB.State.Status);
            Assert.AreEqual("room-a-game", clientB.State.GameplaySessionId);
        }

        private static NetworkRoomSessionRunner CreateServer(NetworkRoomConfig config, INetworkTransport transport)
        {
            return new NetworkRoomSessionRunner(
                NetworkSessionRole.Server,
                "server",
                "server",
                config,
                transport);
        }

        private static NetworkRoomSessionRunner CreateClient(
            NetworkRoomConfig config,
            INetworkTransport transport,
            string peerId)
        {
            return new NetworkRoomSessionRunner(
                NetworkSessionRole.Client,
                peerId,
                "server",
                config,
                transport);
        }

        private static void Pump(
            NetworkRoomSessionRunner server,
            NetworkRoomSessionRunner client,
            RuntimeFrame frame)
        {
            server.ProcessIncoming(frame);
            client.ProcessIncoming(frame);
        }

        private static void Pump(
            NetworkRoomSessionRunner server,
            NetworkRoomSessionRunner clientA,
            NetworkRoomSessionRunner clientB,
            RuntimeFrame frame)
        {
            server.ProcessIncoming(frame);
            clientA.ProcessIncoming(frame);
            clientB.ProcessIncoming(frame);
        }
    }
}
