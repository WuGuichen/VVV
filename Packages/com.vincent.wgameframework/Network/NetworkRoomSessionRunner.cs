using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public sealed class NetworkRoomSessionRunner
    {
        private readonly INetworkTransport _transport;
        private readonly IRuntimeLogger _logger;
        private readonly List<NetworkRoomMember> _members = new List<NetworkRoomMember>();
        private readonly List<NetworkMessageEnvelope> _inbox = new List<NetworkMessageEnvelope>();
        private readonly List<NetworkTransportEvent> _events = new List<NetworkTransportEvent>();
        private readonly NetworkSessionDiagnostics _diagnostics = new NetworkSessionDiagnostics(32);
        private readonly NetworkReliableSequenceTracker _sequenceTracker = new NetworkReliableSequenceTracker();
        private long _nextSequence;
        private long _nextRequestId;
        private int _joinCount;
        private int _leaveCount;
        private int _readyCount;
        private int _startCount;
        private int _rejectionCount;
        private int _disconnectCount;

        public NetworkRoomSessionRunner(
            NetworkSessionRole role,
            string peerId,
            string serverPeerId,
            NetworkRoomConfig config,
            INetworkTransport transport,
            IRuntimeLogger logger = null)
        {
            if (role == NetworkSessionRole.Observer)
                throw new ArgumentException("Network room session MVP supports Server and Client roles only.", nameof(role));

            Role = role;
            PeerId = string.IsNullOrEmpty(peerId) ? role.ToString().ToLowerInvariant() : peerId;
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            Config = config.RoomId == null ? NetworkRoomConfig.Default : config;
            SessionId = Config.RoomId;
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _logger = logger ?? NullRuntimeLogger.Instance;
            State = CreateState(NetworkRoomStatus.Closed, string.Empty, string.Empty);
        }

        public NetworkSessionRole Role { get; }
        public string PeerId { get; }
        public string ServerPeerId { get; }
        public string SessionId { get; }
        public NetworkRoomConfig Config { get; }
        public NetworkRoomState State { get; private set; }
        public NetworkRoomStatus Status => State.Status;
        public bool IsInGame => State.Status == NetworkRoomStatus.InGame;
        public bool IsOpen => State.Status == NetworkRoomStatus.Open;
        public bool LastResponseAccepted { get; private set; } = true;
        public string LastResponseReason { get; private set; } = string.Empty;
        public int DuplicateMessageCount => _sequenceTracker.DuplicateMessageCount;
        public int OutOfOrderMessageCount => _sequenceTracker.OutOfOrderMessageCount;
        public IReadOnlyList<string> RecentDiagnostics => _diagnostics.RecentMessages;

        public NetworkRoomDiagnostics Diagnostics => new NetworkRoomDiagnostics(
            _joinCount,
            _leaveCount,
            _readyCount,
            _startCount,
            _rejectionCount,
            _disconnectCount,
            State.StateVersion,
            LastResponseReason,
            State.GameplaySessionId);

        public void OpenRoom(RuntimeFrame frame)
        {
            if (Role != NetworkSessionRole.Server)
                throw new InvalidOperationException("Only server room sessions can open rooms.");
            if (State.Status == NetworkRoomStatus.InGame)
                return;

            State = CreateState(NetworkRoomStatus.Open, string.Empty, "room opened");
            AddDiagnostic("room opened id=" + Config.RoomId, RuntimeLogLevel.Info);
        }

        public void JoinRoom(RuntimeFrame frame, string displayName = "")
        {
            if (Role == NetworkSessionRole.Server)
                throw new InvalidOperationException("Server room sessions do not join their own room.");

            Send(
                ServerPeerId,
                NetworkMessageKind.RoomCommand,
                frame,
                new NetworkRoomCommandPacket(
                    NetworkRoomCommandKind.Join,
                    PeerId,
                    displayName,
                    ready: false,
                    requestId: NextRequestId()));
        }

        public void SetReady(RuntimeFrame frame, bool ready)
        {
            if (Role == NetworkSessionRole.Server)
                throw new InvalidOperationException("Server room sessions set readiness through authoritative room state.");

            Send(
                ServerPeerId,
                NetworkMessageKind.RoomCommand,
                frame,
                new NetworkRoomCommandPacket(
                    NetworkRoomCommandKind.SetReady,
                    PeerId,
                    ready: ready,
                    requestId: NextRequestId()));
        }

        public void LeaveRoom(RuntimeFrame frame)
        {
            if (Role == NetworkSessionRole.Server)
                return;

            Send(
                ServerPeerId,
                NetworkMessageKind.RoomCommand,
                frame,
                new NetworkRoomCommandPacket(
                    NetworkRoomCommandKind.Leave,
                    PeerId,
                    requestId: NextRequestId()));
        }

        public bool RequestStartGame(RuntimeFrame frame)
        {
            if (Role == NetworkSessionRole.Server)
                return TryStartGame(frame, out _);

            Send(
                ServerPeerId,
                NetworkMessageKind.RoomCommand,
                frame,
                new NetworkRoomCommandPacket(
                    NetworkRoomCommandKind.StartGame,
                    PeerId,
                    requestId: NextRequestId()));
            return false;
        }

        public bool CanStartGame(out string reason)
        {
            return CanStart(out reason);
        }

        public bool TryStartGame(RuntimeFrame frame, out string reason)
        {
            if (Role != NetworkSessionRole.Server)
            {
                reason = "only server can start room";
                return false;
            }

            if (!CanStart(out reason))
            {
                RejectLocal(reason);
                return false;
            }

            string gameplaySessionId = CreateGameplaySessionId(frame);
            State = CreateState(NetworkRoomStatus.InGame, gameplaySessionId, "game started");
            _startCount++;
            LastResponseAccepted = true;
            LastResponseReason = string.Empty;
            BroadcastState(frame, accepted: true, reason: string.Empty, requestId: 0L);
            AddDiagnostic("room game started session=" + gameplaySessionId, RuntimeLogLevel.Info);
            return true;
        }

        public void ProcessIncoming(RuntimeFrame frame)
        {
            ProcessTransportEvents(frame);

            _inbox.Clear();
            _transport.Poll(PeerId, frame, _inbox);
            _inbox.Sort(CompareMessages);
            for (int i = 0; i < _inbox.Count; i++)
                ProcessMessage(_inbox[i], frame);
        }

        public NetworkTransportSnapshot CreateTransportSnapshot()
        {
            return _transport.CreateSnapshot(PeerId);
        }

        private void ProcessTransportEvents(RuntimeFrame frame)
        {
            _events.Clear();
            _transport.PollEvents(PeerId, frame, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                NetworkTransportEvent evt = _events[i];
                switch (evt.Kind)
                {
                    case NetworkTransportEventKind.Disconnected:
                    case NetworkTransportEventKind.Timeout:
                        HandleDisconnected(evt.PeerId, frame, evt.Message);
                        break;
                    case NetworkTransportEventKind.Error:
                        LastResponseAccepted = false;
                        LastResponseReason = evt.Message;
                        AddDiagnostic("room transport error peer=" + evt.PeerId + " " + evt.Message);
                        break;
                    case NetworkTransportEventKind.Connected:
                        AddDiagnostic("room transport connected peer=" + evt.PeerId, RuntimeLogLevel.Info);
                        break;
                }
            }
        }

        private void ProcessMessage(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (!string.Equals(message.SessionId, SessionId, StringComparison.Ordinal))
                return;
            if (message.Kind != NetworkMessageKind.RoomCommand && message.Kind != NetworkMessageKind.RoomState)
                return;
            if (!_sequenceTracker.ShouldProcess(message, _diagnostics))
                return;

            switch (message.Kind)
            {
                case NetworkMessageKind.RoomCommand:
                    ProcessRoomCommand(message, frame);
                    break;
                case NetworkMessageKind.RoomState:
                    ProcessRoomState(message);
                    break;
            }
        }

        private void ProcessRoomCommand(NetworkMessageEnvelope message, RuntimeFrame frame)
        {
            if (Role != NetworkSessionRole.Server)
                return;
            if (!message.TryGetPayload(out NetworkRoomCommandPacket packet))
            {
                AddDiagnostic("room command payload mismatch");
                return;
            }

            string peerId = ResolvePeerId(packet.PeerId, message.SenderPeerId);
            switch (packet.CommandKind)
            {
                case NetworkRoomCommandKind.Join:
                    HandleJoin(peerId, packet.DisplayName, frame, packet.RequestId);
                    break;
                case NetworkRoomCommandKind.Leave:
                    HandleLeave(peerId, frame, packet.RequestId);
                    break;
                case NetworkRoomCommandKind.SetReady:
                    HandleSetReady(peerId, packet.Ready, frame, packet.RequestId);
                    break;
                case NetworkRoomCommandKind.StartGame:
                    SendState(peerId, frame, accepted: false, reason: "only server can start room", packet.RequestId);
                    _rejectionCount++;
                    break;
                default:
                    SendState(peerId, frame, accepted: false, reason: "unsupported room command", packet.RequestId);
                    _rejectionCount++;
                    break;
            }
        }

        private void ProcessRoomState(NetworkMessageEnvelope message)
        {
            if (Role == NetworkSessionRole.Server)
                return;
            if (!message.TryGetPayload(out NetworkRoomStatePacket packet))
            {
                AddDiagnostic("room state payload mismatch");
                return;
            }

            State = packet.State;
            LastResponseAccepted = packet.Accepted;
            LastResponseReason = packet.Reason;
            if (!packet.Accepted)
                _rejectionCount++;
        }

        private void HandleJoin(string peerId, string displayName, RuntimeFrame frame, long requestId)
        {
            if (string.IsNullOrEmpty(peerId))
            {
                SendState(peerId, frame, accepted: false, reason: "room join peer id is empty", requestId);
                _rejectionCount++;
                return;
            }

            if (State.Status == NetworkRoomStatus.InGame && !Config.AllowLateJoin)
            {
                SendState(peerId, frame, accepted: false, reason: "room already in game", requestId);
                _rejectionCount++;
                return;
            }

            if (State.Status == NetworkRoomStatus.Closed)
            {
                SendState(peerId, frame, accepted: false, reason: "room is closed", requestId);
                _rejectionCount++;
                return;
            }

            int index = FindMemberIndex(peerId);
            if (index >= 0)
            {
                NetworkRoomMember member = _members[index]
                    .WithConnected(true)
                    .WithReady(false)
                    .WithDisplayName(displayName);
                _members[index] = member;
                _joinCount++;
                State = CreateState(State.Status, State.GameplaySessionId, "member rejoined");
                BroadcastState(frame, accepted: true, reason: string.Empty, requestId);
                return;
            }

            if (_members.Count >= Config.MaxMembers)
            {
                SendState(peerId, frame, accepted: false, reason: "room is full", requestId);
                _rejectionCount++;
                return;
            }

            _members.Add(new NetworkRoomMember(
                peerId,
                displayName,
                AllocateSlotIndex(),
                ready: false,
                connected: true,
                joinedFrame: frame));
            _joinCount++;
            State = CreateState(State.Status, State.GameplaySessionId, "member joined");
            BroadcastState(frame, accepted: true, reason: string.Empty, requestId);
        }

        private void HandleLeave(string peerId, RuntimeFrame frame, long requestId)
        {
            int index = FindMemberIndex(peerId);
            if (index < 0)
            {
                SendState(peerId, frame, accepted: false, reason: "member is not in room", requestId);
                _rejectionCount++;
                return;
            }

            _members.RemoveAt(index);
            _leaveCount++;
            State = CreateState(State.Status, State.GameplaySessionId, "member left");
            BroadcastState(frame, accepted: true, reason: string.Empty, requestId);
        }

        private void HandleSetReady(string peerId, bool ready, RuntimeFrame frame, long requestId)
        {
            int index = FindMemberIndex(peerId);
            if (index < 0 || !_members[index].Connected)
            {
                SendState(peerId, frame, accepted: false, reason: "member is not connected", requestId);
                _rejectionCount++;
                return;
            }

            _members[index] = _members[index].WithReady(ready);
            _readyCount++;
            State = CreateState(State.Status, State.GameplaySessionId, ready ? "member ready" : "member not ready");
            BroadcastState(frame, accepted: true, reason: string.Empty, requestId);
        }

        private void HandleDisconnected(string peerId, RuntimeFrame frame, string reason)
        {
            if (Role != NetworkSessionRole.Server)
            {
                LastResponseAccepted = false;
                LastResponseReason = string.IsNullOrEmpty(reason) ? "room transport disconnected" : reason;
                AddDiagnostic("room disconnected: " + LastResponseReason);
                return;
            }

            int index = FindMemberIndex(peerId);
            if (index < 0)
                return;

            _members[index] = _members[index]
                .WithConnected(false)
                .WithReady(false);
            _sequenceTracker.ResetSender(peerId);
            _disconnectCount++;
            State = CreateState(State.Status, State.GameplaySessionId, "member disconnected");
            BroadcastState(frame, accepted: true, reason: string.Empty, requestId: 0L);
        }

        private bool CanStart(out string reason)
        {
            if (State.Status != NetworkRoomStatus.Open)
            {
                reason = State.Status == NetworkRoomStatus.InGame ? "room already in game" : "room is not open";
                return false;
            }

            int connected = State.ConnectedMemberCount;
            int ready = State.ReadyMemberCount;
            if (connected < Config.MinReadyMembers)
            {
                reason = "not enough connected members";
                return false;
            }

            if (ready < Config.MinReadyMembers)
            {
                reason = "not enough ready members";
                return false;
            }

            if (Config.RequireAllReady && ready < connected)
            {
                reason = "not all connected members are ready";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void RejectLocal(string reason)
        {
            LastResponseAccepted = false;
            LastResponseReason = reason ?? string.Empty;
            _rejectionCount++;
            AddDiagnostic("room start rejected: " + LastResponseReason);
        }

        private void BroadcastState(RuntimeFrame frame, bool accepted, string reason, long requestId)
        {
            bool sent = false;
            for (int i = 0; i < _members.Count; i++)
            {
                if (!_members[i].Connected)
                    continue;

                SendState(_members[i].PeerId, frame, accepted, reason, requestId);
                sent = true;
            }

            if (!sent)
                LastResponseReason = reason ?? string.Empty;
        }

        private void SendState(string receiverPeerId, RuntimeFrame frame, bool accepted, string reason, long requestId)
        {
            Send(
                receiverPeerId,
                NetworkMessageKind.RoomState,
                frame,
                new NetworkRoomStatePacket(State, accepted, reason, requestId));
            LastResponseAccepted = accepted;
            LastResponseReason = reason ?? string.Empty;
        }

        private void Send(
            string receiverPeerId,
            NetworkMessageKind kind,
            RuntimeFrame frame,
            object payload)
        {
            _transport.Send(new NetworkMessageEnvelope(
                SessionId,
                PeerId,
                receiverPeerId ?? string.Empty,
                _nextSequence++,
                NetworkChannel.ReliableOrdered,
                kind,
                frame,
                payload));
        }

        private NetworkRoomState CreateState(NetworkRoomStatus status, string gameplaySessionId, string reason)
        {
            return new NetworkRoomState(
                status,
                Config,
                ServerPeerId,
                gameplaySessionId,
                _members,
                State != null ? State.StateVersion + 1L : 0L,
                reason);
        }

        private int FindMemberIndex(string peerId)
        {
            string safePeerId = peerId ?? string.Empty;
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].PeerId, safePeerId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private int AllocateSlotIndex()
        {
            int slot = 0;
            while (IsSlotUsed(slot))
                slot++;
            return slot;
        }

        private bool IsSlotUsed(int slot)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].SlotIndex == slot)
                    return true;
            }

            return false;
        }

        private string CreateGameplaySessionId(RuntimeFrame frame)
        {
            return Config.RoomId + "-game";
        }

        private long NextRequestId()
        {
            return ++_nextRequestId;
        }

        private void AddDiagnostic(string message, RuntimeLogLevel level = RuntimeLogLevel.Warning)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _diagnostics.Add(message);
            _logger.Log(level, "Network.Room." + Role, message);
        }

        private static string ResolvePeerId(string peerId, string fallbackPeerId)
        {
            if (!string.IsNullOrEmpty(peerId))
                return peerId;
            return fallbackPeerId ?? string.Empty;
        }

        private static int CompareMessages(NetworkMessageEnvelope left, NetworkMessageEnvelope right)
        {
            int sequence = left.Sequence.CompareTo(right.Sequence);
            if (sequence != 0)
                return sequence;
            return string.CompareOrdinal(left.SenderPeerId, right.SenderPeerId);
        }
    }
}
