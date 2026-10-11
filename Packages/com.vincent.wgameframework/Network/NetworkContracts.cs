using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public enum NetworkSessionRole
    {
        Server = 0,
        Client = 1,
        Observer = 2
    }

    public enum NetworkChannel
    {
        ReliableOrdered = 0,
        Unreliable = 1
    }

    public enum NetworkMessageKind
    {
        HandshakeRequest = 0,
        HandshakeResponse = 1,
        RuntimeCommands = 2,
        Snapshot = 3,
        Ping = 4,
        Pong = 5,
        SnapshotRequest = 6,
        RoomCommand = 7,
        RoomState = 8,
        AuthoritativeCommands = 9,
        BoundedRuntimeCommands = 10,
        BoundedAuthoritativeCommands = 11,
        BoundedStateSnapshot = 12,
        BoundedStateRequest = 13,
        ApplicationControlRequest = 14,
        ApplicationControlResponse = 15
    }

    public enum NetworkSessionStatus
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Rejected = 3,
        Resynchronizing = 4,
        RecoveryFailed = 5
    }

    public enum NetworkSnapshotApplyPolicy
    {
        ApplyOnMismatch = 0,
        ApplyFullSnapshotOnly = 1
    }

    public enum NetworkStateSyncPolicy
    {
        SnapshotVerification = 0,
        BootstrapOnly = 1
    }

    public readonly struct NetworkSnapshotHashLookupResult
    {
        private NetworkSnapshotHashLookupResult(bool hasHash, long hash)
        {
            HasHash = hasHash;
            Hash = hash;
        }

        public bool HasHash { get; }
        public long Hash { get; }

        public static NetworkSnapshotHashLookupResult Found(long hash)
        {
            return new NetworkSnapshotHashLookupResult(true, hash);
        }

        public static NetworkSnapshotHashLookupResult Missing()
        {
            return new NetworkSnapshotHashLookupResult(false, 0L);
        }
    }

    public enum NetworkTransportEventKind
    {
        Connected = 0,
        Disconnected = 1,
        Error = 2,
        Timeout = 3
    }

    public interface INetworkMessagePayload
    {
    }

    /// <summary>
    /// Carries one application-owned, schema-versioned control request over the reliable session channel.
    /// The Network module treats <see cref="PayloadJson"/> as opaque data and never interprets gameplay semantics.
    /// </summary>
    /// <remarks>AllocByDesign: created only for low-frequency application control at a session boundary.</remarks>
    public sealed class NetworkApplicationControlRequestPacket : INetworkMessagePayload
    {
        /// <remarks>AllocByDesign: stores normalized strings for a low-frequency request.</remarks>
        public NetworkApplicationControlRequestPacket(
            string typeId,
            int schemaVersion,
            long requestId,
            string payloadJson)
        {
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (requestId < 0L)
                throw new ArgumentOutOfRangeException(nameof(requestId));
            TypeId = typeId?.Trim() ?? string.Empty;
            SchemaVersion = schemaVersion;
            RequestId = requestId;
            PayloadJson = payloadJson ?? string.Empty;
        }

        public string TypeId { get; }
        public int SchemaVersion { get; }
        public long RequestId { get; }
        public string PayloadJson { get; }
    }

    /// <summary>
    /// Returns the authoritative decision for an application control request without exposing its semantics to Network.
    /// </summary>
    /// <remarks>AllocByDesign: created only for low-frequency application control at a session boundary.</remarks>
    public sealed class NetworkApplicationControlResponsePacket : INetworkMessagePayload
    {
        /// <remarks>AllocByDesign: stores normalized response strings for a low-frequency request.</remarks>
        public NetworkApplicationControlResponsePacket(
            string typeId,
            int schemaVersion,
            long requestId,
            bool accepted,
            string reason,
            string payloadJson = "")
        {
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (requestId < 0L)
                throw new ArgumentOutOfRangeException(nameof(requestId));
            TypeId = typeId?.Trim() ?? string.Empty;
            SchemaVersion = schemaVersion;
            RequestId = requestId;
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            PayloadJson = payloadJson ?? string.Empty;
        }

        public string TypeId { get; }
        public int SchemaVersion { get; }
        public long RequestId { get; }
        public bool Accepted { get; }
        public string Reason { get; }
        public string PayloadJson { get; }
    }

    public readonly struct NetworkSessionConfig
    {
        public const int DefaultHeartbeatIntervalFrames = 30;

        public NetworkSessionConfig(
            int protocolVersion,
            int tickRate,
            int inputDelayFrames,
            int snapshotIntervalFrames,
            string configHash,
            string resourceCatalogHash,
            NetworkStateSyncPolicy stateSyncPolicy = NetworkStateSyncPolicy.SnapshotVerification,
            int heartbeatIntervalFrames = DefaultHeartbeatIntervalFrames)
        {
            if (protocolVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(protocolVersion), "Network protocol version cannot be negative.");
            if (tickRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRate), "Network tick rate must be greater than zero.");
            if (inputDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(inputDelayFrames), "Network input delay cannot be negative.");
            if (snapshotIntervalFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(snapshotIntervalFrames), "Network snapshot interval must be greater than zero.");
            if (!Enum.IsDefined(typeof(NetworkStateSyncPolicy), stateSyncPolicy))
                throw new ArgumentOutOfRangeException(nameof(stateSyncPolicy), "Network state sync policy is not supported.");
            if (heartbeatIntervalFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(heartbeatIntervalFrames), "Network heartbeat interval must be greater than zero.");

            ProtocolVersion = protocolVersion;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
            SnapshotIntervalFrames = snapshotIntervalFrames;
            ConfigHash = configHash ?? string.Empty;
            ResourceCatalogHash = resourceCatalogHash ?? string.Empty;
            StateSyncPolicy = stateSyncPolicy;
            HeartbeatIntervalFrames = heartbeatIntervalFrames;
        }

        public int ProtocolVersion { get; }
        public int TickRate { get; }
        public int InputDelayFrames { get; }
        public int SnapshotIntervalFrames { get; }
        public string ConfigHash { get; }
        public string ResourceCatalogHash { get; }
        public NetworkStateSyncPolicy StateSyncPolicy { get; }
        public int HeartbeatIntervalFrames { get; }

        public static NetworkSessionConfig Default =>
            new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 2,
                configHash: string.Empty,
                resourceCatalogHash: string.Empty);

        public bool IsCompatibleWith(NetworkSessionConfig other, out string reason)
        {
            if (ProtocolVersion != other.ProtocolVersion)
            {
                reason = "protocol version mismatch";
                return false;
            }

            if (!string.Equals(ConfigHash, other.ConfigHash, StringComparison.Ordinal))
            {
                reason = "config hash mismatch";
                return false;
            }

            if (!string.Equals(ResourceCatalogHash, other.ResourceCatalogHash, StringComparison.Ordinal))
            {
                reason = "resource catalog hash mismatch";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    public sealed class NetworkMessageEnvelope
    {
        public NetworkMessageEnvelope(
            string sessionId,
            string senderPeerId,
            string receiverPeerId,
            long sequence,
            NetworkChannel channel,
            NetworkMessageKind kind,
            RuntimeFrame frame,
            object payload)
        {
            if (sequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "Network message sequence cannot be negative.");

            SessionId = sessionId ?? string.Empty;
            SenderPeerId = senderPeerId ?? string.Empty;
            ReceiverPeerId = receiverPeerId ?? string.Empty;
            Sequence = sequence;
            Channel = channel;
            Kind = kind;
            Frame = frame;
            Payload = payload;
        }

        public string SessionId { get; }
        public string SenderPeerId { get; }
        public string ReceiverPeerId { get; }
        public long Sequence { get; }
        public NetworkChannel Channel { get; }
        public NetworkMessageKind Kind { get; }
        public RuntimeFrame Frame { get; }
        public object Payload { get; }

        public bool TryGetPayload<TPayload>(out TPayload payload)
            where TPayload : class, INetworkMessagePayload
        {
            payload = Payload as TPayload;
            return payload != null;
        }
    }

    public sealed class NetworkRuntimeCommandPacket : INetworkMessagePayload
    {
        private readonly ReadOnlyCollection<RuntimeCommand> _commands;

        public NetworkRuntimeCommandPacket(
            string inputPeerId,
            RuntimeFrame clientFrame,
            IReadOnlyList<RuntimeCommand> commands)
        {
            InputPeerId = inputPeerId ?? string.Empty;
            ClientFrame = clientFrame;
            _commands = CopyCommands(commands);
        }

        public string InputPeerId { get; }
        public RuntimeFrame ClientFrame { get; }
        public IReadOnlyList<RuntimeCommand> Commands => _commands;

        private static ReadOnlyCollection<RuntimeCommand> CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return new ReadOnlyCollection<RuntimeCommand>(new List<RuntimeCommand>());

            var copy = new List<RuntimeCommand>(commands.Count);
            for (int i = 0; i < commands.Count; i++)
                copy.Add(commands[i]);
            copy.Sort(CompareCommands);
            return new ReadOnlyCollection<RuntimeCommand>(copy);
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;
            int source = left.SourceId.CompareTo(right.SourceId);
            if (source != 0)
                return source;
            int command = left.CommandId.CompareTo(right.CommandId);
            if (command != 0)
                return command;
            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0)
                return target;
            return left.Sequence.CompareTo(right.Sequence);
        }
    }

    public sealed class NetworkAuthoritativeCommandPacket : INetworkMessagePayload
    {
        private readonly ReadOnlyCollection<RuntimeCommand> _commands;

        public NetworkAuthoritativeCommandPacket(
            string inputPeerId,
            RuntimeFrame serverFrame,
            RuntimeFrame ackedInputFrame,
            IReadOnlyList<RuntimeCommand> commands)
        {
            InputPeerId = inputPeerId ?? string.Empty;
            ServerFrame = serverFrame;
            AckedInputFrame = ackedInputFrame;
            _commands = CopyCommands(commands);
        }

        public string InputPeerId { get; }
        public RuntimeFrame ServerFrame { get; }
        public RuntimeFrame AckedInputFrame { get; }
        public IReadOnlyList<RuntimeCommand> Commands => _commands;

        private static ReadOnlyCollection<RuntimeCommand> CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return new ReadOnlyCollection<RuntimeCommand>(new List<RuntimeCommand>());

            var copy = new List<RuntimeCommand>(commands.Count);
            for (int i = 0; i < commands.Count; i++)
                copy.Add(commands[i]);
            copy.Sort(CompareCommands);
            return new ReadOnlyCollection<RuntimeCommand>(copy);
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;
            int source = left.SourceId.CompareTo(right.SourceId);
            if (source != 0)
                return source;
            int command = left.CommandId.CompareTo(right.CommandId);
            if (command != 0)
                return command;
            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0)
                return target;
            return left.Sequence.CompareTo(right.Sequence);
        }
    }

    public sealed class NetworkSnapshotPacket : INetworkMessagePayload
    {
        private readonly ReadOnlyCollection<RuntimeCustomState> _moduleSnapshots;

        public NetworkSnapshotPacket(
            RuntimeFrame authoritativeFrame,
            long resultHash,
            RuntimeFrame ackedInputFrame,
            IReadOnlyList<RuntimeCustomState> moduleSnapshots,
            bool fullSnapshot)
        {
            AuthoritativeFrame = authoritativeFrame;
            ResultHash = resultHash;
            AckedInputFrame = ackedInputFrame;
            FullSnapshot = fullSnapshot;
            _moduleSnapshots = CopySnapshots(moduleSnapshots);
        }

        public RuntimeFrame AuthoritativeFrame { get; }
        public long ResultHash { get; }
        public RuntimeFrame AckedInputFrame { get; }
        public bool FullSnapshot { get; }
        public bool IsDigestOnly => !FullSnapshot && _moduleSnapshots.Count == 0;
        public IReadOnlyList<RuntimeCustomState> ModuleSnapshots => _moduleSnapshots;

        private static ReadOnlyCollection<RuntimeCustomState> CopySnapshots(IReadOnlyList<RuntimeCustomState> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return new ReadOnlyCollection<RuntimeCustomState>(new List<RuntimeCustomState>());

            var copy = new List<RuntimeCustomState>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
                copy.Add(snapshots[i]);
            return new ReadOnlyCollection<RuntimeCustomState>(copy);
        }
    }

    public sealed class NetworkSnapshotRequestPacket : INetworkMessagePayload
    {
        public NetworkSnapshotRequestPacket(
            string peerId,
            RuntimeFrame authoritativeFrame,
            long localHash,
            long remoteHash,
            int consecutiveMismatchCount,
            string reason)
        {
            if (consecutiveMismatchCount < 0)
                throw new ArgumentOutOfRangeException(nameof(consecutiveMismatchCount), "Network snapshot request mismatch count cannot be negative.");

            PeerId = peerId ?? string.Empty;
            AuthoritativeFrame = authoritativeFrame;
            LocalHash = localHash;
            RemoteHash = remoteHash;
            ConsecutiveMismatchCount = consecutiveMismatchCount;
            Reason = reason ?? string.Empty;
        }

        public string PeerId { get; }
        public RuntimeFrame AuthoritativeFrame { get; }
        public long LocalHash { get; }
        public long RemoteHash { get; }
        public int ConsecutiveMismatchCount { get; }
        public string Reason { get; }
    }

    public sealed class NetworkHandshakeRequest : INetworkMessagePayload
    {
        public NetworkHandshakeRequest(string peerId, NetworkSessionRole role, NetworkSessionConfig config)
        {
            PeerId = peerId ?? string.Empty;
            Role = role;
            Config = config;
        }

        public string PeerId { get; }
        public NetworkSessionRole Role { get; }
        public NetworkSessionConfig Config { get; }
    }

    public sealed class NetworkHandshakeResponse : INetworkMessagePayload
    {
        public NetworkHandshakeResponse(bool accepted, string reason, NetworkSessionConfig serverConfig)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            ServerConfig = serverConfig;
        }

        public bool Accepted { get; }
        public string Reason { get; }
        public NetworkSessionConfig ServerConfig { get; }
    }

    public sealed class NetworkHeartbeatPacket : INetworkMessagePayload
    {
        public NetworkHeartbeatPacket(string peerId, RuntimeFrame sentFrame, long pingSequence)
        {
            if (pingSequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(pingSequence), "Network heartbeat sequence cannot be negative.");

            PeerId = peerId ?? string.Empty;
            SentFrame = sentFrame;
            PingSequence = pingSequence;
        }

        public string PeerId { get; }
        public RuntimeFrame SentFrame { get; }
        public long PingSequence { get; }
    }

    public readonly struct NetworkSnapshotApplyResult
    {
        private NetworkSnapshotApplyResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        public static NetworkSnapshotApplyResult Succeeded()
        {
            return new NetworkSnapshotApplyResult(true, string.Empty);
        }

        public static NetworkSnapshotApplyResult Failed(string message)
        {
            return new NetworkSnapshotApplyResult(false, message);
        }
    }

    public interface INetworkSnapshotParticipant
    {
        string ParticipantId { get; }
        RuntimeCustomState CaptureSnapshot(RuntimeFrame frame);
        NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot);
    }

    public interface INetworkPeerSnapshotParticipant : INetworkSnapshotParticipant
    {
        RuntimeCustomState CaptureSnapshot(RuntimeFrame frame, string receiverPeerId);
    }

    public readonly struct NetworkCommandAuthorizationResult
    {
        private NetworkCommandAuthorizationResult(bool accepted, string reason)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public bool Accepted { get; }
        public string Reason { get; }

        public static NetworkCommandAuthorizationResult Accept()
        {
            return new NetworkCommandAuthorizationResult(true, string.Empty);
        }

        public static NetworkCommandAuthorizationResult Reject(string reason)
        {
            return new NetworkCommandAuthorizationResult(false, reason);
        }
    }

    public interface INetworkTransport
    {
        string TransportId { get; }
        void Send(NetworkMessageEnvelope message);
        int Poll(string receiverPeerId, RuntimeFrame frame, List<NetworkMessageEnvelope> output);
        int PollEvents(string peerId, RuntimeFrame frame, List<NetworkTransportEvent> output);
        NetworkTransportSnapshot CreateSnapshot(string peerId);
        void Clear();
    }

    public readonly struct NetworkTransportEvent
    {
        public NetworkTransportEvent(
            NetworkTransportEventKind kind,
            string peerId,
            RuntimeFrame frame,
            string message = "")
        {
            Kind = kind;
            PeerId = peerId ?? string.Empty;
            Frame = frame;
            Message = message ?? string.Empty;
        }

        public NetworkTransportEventKind Kind { get; }
        public string PeerId { get; }
        public RuntimeFrame Frame { get; }
        public string Message { get; }
    }

    public readonly struct NetworkTransportSnapshot
    {
        public NetworkTransportSnapshot(
            string peerId,
            int pendingMessageCount,
            long sentCount,
            long deliveredCount,
            long droppedCount,
            long reorderedCount,
            long codecErrorCount = 0L,
            long sentBytes = 0L,
            long deliveredBytes = 0L,
            int lastSentPayloadBytes = 0,
            int maxSentPayloadBytes = 0,
            long handshakeSentBytes = 0L,
            long runtimeCommandSentBytes = 0L,
            long snapshotSentBytes = 0L,
            long snapshotRequestSentBytes = 0L,
            long heartbeatSentBytes = 0L,
            long roomSentBytes = 0L)
        {
            PeerId = peerId ?? string.Empty;
            PendingMessageCount = pendingMessageCount;
            SentCount = sentCount;
            DeliveredCount = deliveredCount;
            DroppedCount = droppedCount;
            ReorderedCount = reorderedCount;
            CodecErrorCount = codecErrorCount;
            SentBytes = sentBytes;
            DeliveredBytes = deliveredBytes;
            LastSentPayloadBytes = lastSentPayloadBytes < 0 ? 0 : lastSentPayloadBytes;
            MaxSentPayloadBytes = maxSentPayloadBytes < 0 ? 0 : maxSentPayloadBytes;
            HandshakeSentBytes = handshakeSentBytes;
            RuntimeCommandSentBytes = runtimeCommandSentBytes;
            SnapshotSentBytes = snapshotSentBytes;
            SnapshotRequestSentBytes = snapshotRequestSentBytes;
            HeartbeatSentBytes = heartbeatSentBytes;
            RoomSentBytes = roomSentBytes;
        }

        public string PeerId { get; }
        public int PendingMessageCount { get; }
        public long SentCount { get; }
        public long DeliveredCount { get; }
        public long DroppedCount { get; }
        public long ReorderedCount { get; }
        public long CodecErrorCount { get; }
        public long SentBytes { get; }
        public long DeliveredBytes { get; }
        public int LastSentPayloadBytes { get; }
        public int MaxSentPayloadBytes { get; }
        public long HandshakeSentBytes { get; }
        public long RuntimeCommandSentBytes { get; }
        public long SnapshotSentBytes { get; }
        public long SnapshotRequestSentBytes { get; }
        public long HeartbeatSentBytes { get; }
        public long RoomSentBytes { get; }
    }
}
