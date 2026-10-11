using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public enum NetworkRoomStatus
    {
        Closed = 0,
        Open = 1,
        InGame = 2
    }

    public enum NetworkRoomCommandKind
    {
        Join = 0,
        Leave = 1,
        SetReady = 2,
        StartGame = 3
    }

    public readonly struct NetworkRoomConfig
    {
        public NetworkRoomConfig(
            string roomId,
            string roomName = "",
            int maxMembers = 4,
            int minReadyMembers = 1,
            bool requireAllReady = true,
            bool allowLateJoin = false,
            NetworkSessionConfig? gameplaySessionConfig = null)
        {
            if (maxMembers <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxMembers), "Network room max members must be greater than zero.");
            if (minReadyMembers <= 0)
                throw new ArgumentOutOfRangeException(nameof(minReadyMembers), "Network room min ready members must be greater than zero.");
            if (minReadyMembers > maxMembers)
                throw new ArgumentOutOfRangeException(nameof(minReadyMembers), "Network room min ready members cannot exceed max members.");

            RoomId = string.IsNullOrWhiteSpace(roomId) ? "room" : roomId;
            RoomName = roomName ?? string.Empty;
            MaxMembers = maxMembers;
            MinReadyMembers = minReadyMembers;
            RequireAllReady = requireAllReady;
            AllowLateJoin = allowLateJoin;
            GameplaySessionConfig = gameplaySessionConfig ?? NetworkSessionConfig.Default;
        }

        public string RoomId { get; }
        public string RoomName { get; }
        public int MaxMembers { get; }
        public int MinReadyMembers { get; }
        public bool RequireAllReady { get; }
        public bool AllowLateJoin { get; }
        public NetworkSessionConfig GameplaySessionConfig { get; }

        public static NetworkRoomConfig Default => new NetworkRoomConfig("room");
    }

    public sealed class NetworkRoomMember
    {
        public NetworkRoomMember(
            string peerId,
            string displayName,
            int slotIndex,
            bool ready,
            bool connected,
            RuntimeFrame joinedFrame)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Network room slot index cannot be negative.");

            PeerId = peerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SlotIndex = slotIndex;
            Ready = ready;
            Connected = connected;
            JoinedFrame = joinedFrame;
        }

        public string PeerId { get; }
        public string DisplayName { get; }
        public int SlotIndex { get; }
        public bool Ready { get; }
        public bool Connected { get; }
        public RuntimeFrame JoinedFrame { get; }

        public NetworkRoomMember WithReady(bool ready)
        {
            return new NetworkRoomMember(PeerId, DisplayName, SlotIndex, ready, Connected, JoinedFrame);
        }

        public NetworkRoomMember WithConnected(bool connected)
        {
            return new NetworkRoomMember(PeerId, DisplayName, SlotIndex, connected ? Ready : false, connected, JoinedFrame);
        }

        public NetworkRoomMember WithDisplayName(string displayName)
        {
            return new NetworkRoomMember(PeerId, displayName, SlotIndex, Ready, Connected, JoinedFrame);
        }
    }

    public sealed class NetworkRoomState
    {
        private readonly ReadOnlyCollection<NetworkRoomMember> _members;

        public NetworkRoomState(
            NetworkRoomStatus status,
            NetworkRoomConfig config,
            string serverPeerId,
            string gameplaySessionId,
            IReadOnlyList<NetworkRoomMember> members,
            long stateVersion,
            string lastReason)
        {
            if (stateVersion < 0L)
                throw new ArgumentOutOfRangeException(nameof(stateVersion), "Network room state version cannot be negative.");

            Status = status;
            Config = config;
            ServerPeerId = serverPeerId ?? string.Empty;
            GameplaySessionId = gameplaySessionId ?? string.Empty;
            StateVersion = stateVersion;
            LastReason = lastReason ?? string.Empty;
            _members = CopyMembers(members);
        }

        public NetworkRoomStatus Status { get; }
        public NetworkRoomConfig Config { get; }
        public string ServerPeerId { get; }
        public string GameplaySessionId { get; }
        public long StateVersion { get; }
        public string LastReason { get; }
        public IReadOnlyList<NetworkRoomMember> Members => _members;

        public int ConnectedMemberCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].Connected)
                        count++;
                }

                return count;
            }
        }

        public int ReadyMemberCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].Connected && _members[i].Ready)
                        count++;
                }

                return count;
            }
        }

        public NetworkRoomMember FindMember(string peerId)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].PeerId, peerId ?? string.Empty, StringComparison.Ordinal))
                    return _members[i];
            }

            return null;
        }

        private static ReadOnlyCollection<NetworkRoomMember> CopyMembers(IReadOnlyList<NetworkRoomMember> members)
        {
            if (members == null || members.Count == 0)
                return new ReadOnlyCollection<NetworkRoomMember>(new List<NetworkRoomMember>());

            var copy = new List<NetworkRoomMember>(members.Count);
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null)
                    copy.Add(members[i]);
            }

            copy.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            return new ReadOnlyCollection<NetworkRoomMember>(copy);
        }
    }

    public sealed class NetworkRoomCommandPacket : INetworkMessagePayload
    {
        public NetworkRoomCommandPacket(
            NetworkRoomCommandKind commandKind,
            string peerId,
            string displayName = "",
            bool ready = false,
            long requestId = 0L)
        {
            if (requestId < 0L)
                throw new ArgumentOutOfRangeException(nameof(requestId), "Network room request id cannot be negative.");

            CommandKind = commandKind;
            PeerId = peerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Ready = ready;
            RequestId = requestId;
        }

        public NetworkRoomCommandKind CommandKind { get; }
        public string PeerId { get; }
        public string DisplayName { get; }
        public bool Ready { get; }
        public long RequestId { get; }
    }

    public sealed class NetworkRoomStatePacket : INetworkMessagePayload
    {
        public NetworkRoomStatePacket(
            NetworkRoomState state,
            bool accepted,
            string reason = "",
            long requestId = 0L)
        {
            if (requestId < 0L)
                throw new ArgumentOutOfRangeException(nameof(requestId), "Network room request id cannot be negative.");

            State = state ?? throw new ArgumentNullException(nameof(state));
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            RequestId = requestId;
        }

        public NetworkRoomState State { get; }
        public bool Accepted { get; }
        public string Reason { get; }
        public long RequestId { get; }
    }

    public readonly struct NetworkRoomDiagnostics
    {
        public NetworkRoomDiagnostics(
            int joinCount,
            int leaveCount,
            int readyCount,
            int startCount,
            int rejectionCount,
            int disconnectCount,
            long stateVersion,
            string lastReason,
            string lastGameplaySessionId)
        {
            JoinCount = joinCount;
            LeaveCount = leaveCount;
            ReadyCount = readyCount;
            StartCount = startCount;
            RejectionCount = rejectionCount;
            DisconnectCount = disconnectCount;
            StateVersion = stateVersion;
            LastReason = lastReason ?? string.Empty;
            LastGameplaySessionId = lastGameplaySessionId ?? string.Empty;
        }

        public int JoinCount { get; }
        public int LeaveCount { get; }
        public int ReadyCount { get; }
        public int StartCount { get; }
        public int RejectionCount { get; }
        public int DisconnectCount { get; }
        public long StateVersion { get; }
        public string LastReason { get; }
        public string LastGameplaySessionId { get; }
    }
}
