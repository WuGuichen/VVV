using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityTransportStatusCode = Unity.Networking.Transport.Error.StatusCode;

namespace MxFramework.Network.UnityTransport
{
    public sealed class UnityTransportNetworkTransport : INetworkTransport, IDisposable
    {
        private const int MaxPendingSends = 512;

        private readonly List<ConnectionRecord> _connections = new List<ConnectionRecord>();
        private readonly List<NetworkMessageEnvelope> _pendingSends = new List<NetworkMessageEnvelope>();
        private readonly List<NetworkMessageEnvelope> _pendingMessages = new List<NetworkMessageEnvelope>();
        private readonly List<NetworkTransportEvent> _events = new List<NetworkTransportEvent>();
        private readonly INetworkMessageCodec _codec;
        private readonly UnityTransportNetworkOptions _options;
        private NetworkDriver _driver;
        private NetworkPipeline _reliablePipeline;
        private NetworkPipeline _fragmentedUnreliablePipeline;
        private long _lastPumpFrame = long.MinValue;
        private long _sentCount;
        private long _deliveredCount;
        private long _droppedCount;
        private long _codecErrorCount;
        private long _sentBytes;
        private long _deliveredBytes;
        private long _handshakeSentBytes;
        private long _runtimeCommandSentBytes;
        private long _snapshotSentBytes;
        private long _snapshotRequestSentBytes;
        private long _heartbeatSentBytes;
        private long _roomSentBytes;
        private int _lastSentPayloadBytes;
        private int _maxSentPayloadBytes;
        private bool _started;
        private bool _server;
        private bool _hasClientEndpoint;
        private ushort _clientRemotePort;
        private string _clientRemoteAddress = string.Empty;
        private string _clientServerPeerId = string.Empty;

        public UnityTransportNetworkTransport()
            : this(new JsonNetworkMessageCodec(), UnityTransportNetworkOptions.Default)
        {
        }

        public UnityTransportNetworkTransport(INetworkMessageCodec codec)
            : this(codec, UnityTransportNetworkOptions.Default)
        {
        }

        public UnityTransportNetworkTransport(UnityTransportNetworkOptions options)
            : this(new JsonNetworkMessageCodec(), options)
        {
        }

        public UnityTransportNetworkTransport(INetworkMessageCodec codec, UnityTransportNetworkOptions options)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _options = NormalizeOptions(options);
        }

        public string TransportId => "unity-transport:" + _codec.CodecId;

        public bool StartServer(ushort port, string bindAddress = "0.0.0.0")
        {
            Clear();
            string safeBindAddress = string.IsNullOrEmpty(bindAddress) ? "0.0.0.0" : bindAddress;
            return StartServerInternal(port, safeBindAddress);
        }

        public bool StartClient(string remoteAddress, ushort remotePort, string serverPeerId = "server")
        {
            Clear();
            string safeServerPeerId = string.IsNullOrEmpty(serverPeerId) ? _options.ServerPeerId : serverPeerId;
            _clientRemoteAddress = string.IsNullOrEmpty(remoteAddress) ? "127.0.0.1" : remoteAddress;
            _clientRemotePort = remotePort;
            _clientServerPeerId = safeServerPeerId;
            _hasClientEndpoint = true;
            return StartClientInternal(_clientRemoteAddress, remotePort, safeServerPeerId);
        }

        public bool RestartClient()
        {
            if (_server || !_hasClientEndpoint)
                return false;

            return StartClient(_clientRemoteAddress, _clientRemotePort, _clientServerPeerId);
        }

        public bool HasActiveConnection(string peerId)
        {
            int index = FindConnectionIndex(peerId);
            if (index < 0)
                return false;

            NetworkConnection.State state = GetConnectionState(_connections[index].Connection);
            return state == NetworkConnection.State.Connected
                || state == NetworkConnection.State.Connecting;
        }

        private bool StartServerInternal(ushort port, string bindAddress)
        {
            _server = true;
            CreateDriver();

            if (!TryCreateEndpoint(bindAddress, port, out NetworkEndpoint endpoint))
            {
                QueueError(string.Empty, RuntimeFrame.Zero, "invalid Unity Transport bind endpoint: " + bindAddress + ":" + port);
                DisposeDriverOnly();
                return false;
            }

            int bindResult = _driver.Bind(endpoint);
            if (bindResult != 0)
            {
                QueueError(string.Empty, RuntimeFrame.Zero, "Unity Transport bind failed: " + bindResult);
                DisposeDriverOnly();
                return false;
            }

            int listenResult = _driver.Listen();
            if (listenResult != 0)
            {
                QueueError(string.Empty, RuntimeFrame.Zero, "Unity Transport listen failed: " + listenResult);
                DisposeDriverOnly();
                return false;
            }

            _started = true;
            return true;
        }

        private bool StartClientInternal(string remoteAddress, ushort remotePort, string serverPeerId)
        {
            _server = false;
            CreateDriver();

            string safeServerPeerId = string.IsNullOrEmpty(serverPeerId) ? _options.ServerPeerId : serverPeerId;
            if (!TryCreateEndpoint(remoteAddress, remotePort, out NetworkEndpoint endpoint))
            {
                QueueError(safeServerPeerId, RuntimeFrame.Zero, "invalid Unity Transport remote endpoint: " + remoteAddress + ":" + remotePort);
                DisposeDriverOnly();
                return false;
            }

            NetworkConnection connection = _driver.Connect(endpoint);
            if (!connection.IsCreated)
            {
                QueueError(safeServerPeerId, RuntimeFrame.Zero, "Unity Transport connect failed.");
                DisposeDriverOnly();
                return false;
            }

            _connections.Add(new ConnectionRecord(connection, safeServerPeerId));
            _started = true;
            return true;
        }

        public void Disconnect(string peerId)
        {
            int index = FindConnectionIndex(peerId);
            if (index < 0)
            {
                QueueError(peerId, RuntimeFrame.Zero, "Unity Transport disconnect failed: no connection for peer " + peerId);
                return;
            }

            ConnectionRecord record = _connections[index];
            if (_driver.IsCreated)
            {
                RequestDisconnect(record.Connection);
                CompleteDriverUpdateForDisconnect();
            }

            _connections.RemoveAt(index);
            QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Disconnected,
                record.PeerId,
                RuntimeFrame.Zero,
                "Unity Transport disconnect requested"));
        }

        public void Send(NetworkMessageEnvelope message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (!_started || !_driver.IsCreated)
            {
                _droppedCount++;
                QueueError(message.ReceiverPeerId, message.Frame, "Unity Transport send failed: transport is not started.");
                return;
            }

            TrySend(message, queueWhileConnecting: true);
        }

        private SendAttemptResult TrySend(NetworkMessageEnvelope message, bool queueWhileConnecting)
        {
            NetworkCodecEncodeResult encode = _codec.Encode(message);
            if (!encode.Success)
            {
                _codecErrorCount++;
                QueueError(message.SenderPeerId, message.Frame, encode.Error.ToString());
                return SendAttemptResult.Failed;
            }

            if (encode.Bytes.Length > _options.MaxPayloadBytes)
            {
                _droppedCount++;
                QueueError(
                    message.ReceiverPeerId,
                    message.Frame,
                    "Unity Transport payload exceeds maxPayloadBytes: " + encode.Bytes.Length + " > " + _options.MaxPayloadBytes);
                return SendAttemptResult.Failed;
            }

            int connectionIndex = FindConnectionIndex(message.ReceiverPeerId);
            if (connectionIndex < 0)
            {
                _droppedCount++;
                QueueError(message.ReceiverPeerId, message.Frame, "Unity Transport send failed: no connection for peer " + message.ReceiverPeerId);
                return SendAttemptResult.Failed;
            }

            NetworkConnection connection = _connections[connectionIndex].Connection;
            NetworkConnection.State state = GetConnectionState(connection);
            if (state != NetworkConnection.State.Connected)
            {
                if (state == NetworkConnection.State.Connecting && !_server)
                {
                    if (queueWhileConnecting)
                        return QueuePendingSend(message)
                            ? SendAttemptResult.Queued
                            : SendAttemptResult.Failed;

                    return SendAttemptResult.StillConnecting;
                }

                _droppedCount++;
                DisconnectForSendFailure(
                    connectionIndex,
                    message.Frame,
                    "Unity Transport send failed: connection is " + state);
                return SendAttemptResult.Failed;
            }

            NetworkPipeline pipeline = message.Channel == NetworkChannel.ReliableOrdered
                ? _reliablePipeline
                : _fragmentedUnreliablePipeline;
            int begin = _driver.BeginSend(pipeline, connection, out DataStreamWriter writer, encode.Bytes.Length);
            if (begin != 0)
            {
                if (begin == (int)UnityTransportStatusCode.NetworkStateMismatch && !_server)
                {
                    if (queueWhileConnecting)
                        return QueuePendingSend(message)
                            ? SendAttemptResult.Queued
                            : SendAttemptResult.Failed;

                    if (!queueWhileConnecting)
                        return SendAttemptResult.StillConnecting;
                }

                _droppedCount++;
                DisconnectForSendFailure(connectionIndex, message.Frame, FormatUnityTransportSendError("BeginSend", begin));
                return SendAttemptResult.Failed;
            }

            for (int i = 0; i < encode.Bytes.Length; i++)
                writer.WriteByte(encode.Bytes[i]);

            int end = _driver.EndSend(writer);
            if (end < 0)
            {
                _droppedCount++;
                DisconnectForSendFailure(connectionIndex, message.Frame, FormatUnityTransportSendError("EndSend", end));
                return SendAttemptResult.Failed;
            }

            _sentCount++;
            RecordSentBytes(message.Kind, encode.Bytes.Length);
            _driver.ScheduleFlushSend().Complete();
            return SendAttemptResult.Sent;
        }

        public int Poll(string receiverPeerId, RuntimeFrame frame, List<NetworkMessageEnvelope> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            Pump(frame);
            int start = output.Count;
            string safePeerId = receiverPeerId ?? string.Empty;
            for (int i = _pendingMessages.Count - 1; i >= 0; i--)
            {
                NetworkMessageEnvelope message = _pendingMessages[i];
                if (!string.IsNullOrEmpty(message.ReceiverPeerId)
                    && !string.Equals(message.ReceiverPeerId, safePeerId, StringComparison.Ordinal))
                {
                    continue;
                }

                _pendingMessages.RemoveAt(i);
                output.Add(message);
            }

            output.Sort(start, output.Count - start, NetworkMessageEnvelopeComparer.Instance);
            return output.Count - start;
        }

        public int PollEvents(string peerId, RuntimeFrame frame, List<NetworkTransportEvent> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            Pump(frame);
            int start = output.Count;
            for (int i = 0; i < _events.Count; i++)
                output.Add(_events[i]);
            _events.Clear();
            return output.Count - start;
        }

        public NetworkTransportSnapshot CreateSnapshot(string peerId)
        {
            int pending = _pendingSends.Count;
            string safePeerId = peerId ?? string.Empty;
            for (int i = 0; i < _pendingMessages.Count; i++)
            {
                NetworkMessageEnvelope message = _pendingMessages[i];
                if (string.IsNullOrEmpty(message.ReceiverPeerId)
                    || string.Equals(message.ReceiverPeerId, safePeerId, StringComparison.Ordinal))
                {
                    pending++;
                }
            }

            return new NetworkTransportSnapshot(
                safePeerId,
                pending,
                _sentCount,
                _deliveredCount,
                _droppedCount,
                0L,
                _codecErrorCount,
                _sentBytes,
                _deliveredBytes,
                _lastSentPayloadBytes,
                _maxSentPayloadBytes,
                _handshakeSentBytes,
                _runtimeCommandSentBytes,
                _snapshotSentBytes,
                _snapshotRequestSentBytes,
                _heartbeatSentBytes,
                _roomSentBytes);
        }

        public void Clear()
        {
            DisconnectAllBeforeDispose();
            _pendingMessages.Clear();
            _events.Clear();
            _connections.Clear();
            _pendingSends.Clear();
            _lastPumpFrame = long.MinValue;
            _started = false;
            if (_driver.IsCreated)
                _driver.Dispose();
        }

        public void Dispose()
        {
            Clear();
        }

        private void CreateDriver()
        {
            var settings = new NetworkSettings();
            try
            {
                settings.WithNetworkConfigParameters(
                    receiveQueueCapacity: _options.ReceiveQueueCapacity,
                    sendQueueCapacity: _options.SendQueueCapacity);
                settings.WithFragmentationStageParameters(payloadCapacity: _options.MaxPayloadBytes);
                settings.WithReliableStageParameters(windowSize: _options.ReliableWindowSize);
                _driver = NetworkDriver.Create(settings);
            }
            finally
            {
                settings.Dispose();
            }

            _reliablePipeline = _driver.CreatePipeline(
                typeof(FragmentationPipelineStage),
                typeof(ReliableSequencedPipelineStage));
            _fragmentedUnreliablePipeline = _driver.CreatePipeline(
                typeof(FragmentationPipelineStage));
        }

        private static UnityTransportNetworkOptions NormalizeOptions(UnityTransportNetworkOptions options)
        {
            if (options.MaxConnections <= 0
                || options.MaxPayloadBytes <= 0
                || options.ReliableWindowSize <= 0
                || options.ReceiveQueueCapacity <= 0
                || options.SendQueueCapacity <= 0)
            {
                return UnityTransportNetworkOptions.Default;
            }

            return options;
        }

        private void Pump(RuntimeFrame frame)
        {
            if (!_started || !_driver.IsCreated)
                return;
            if (_lastPumpFrame == frame.Value)
                return;

            _lastPumpFrame = frame.Value;
            if (!TryCompleteDriverUpdate(frame))
                return;

            AcceptConnections(frame);
            ProcessDriverEvents(frame);
            FlushPendingSends();
        }

        private void AcceptConnections(RuntimeFrame frame)
        {
            if (!_server)
                return;

            NetworkConnection connection;
            while ((connection = _driver.Accept()) != default(NetworkConnection))
            {
                if (_connections.Count >= _options.MaxConnections)
                {
                    RequestDisconnect(connection);
                    QueueError(string.Empty, frame, "Unity Transport rejected connection: maxConnections reached.");
                    continue;
                }

                _connections.Add(new ConnectionRecord(connection, string.Empty));
                QueueEvent(new NetworkTransportEvent(
                    NetworkTransportEventKind.Connected,
                    string.Empty,
                    frame,
                    "Unity Transport connection accepted"));
            }
        }

        private void ProcessDriverEvents(RuntimeFrame frame)
        {
            NetworkEvent.Type eventType;
            while ((eventType = _driver.PopEvent(out NetworkConnection connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    case NetworkEvent.Type.Connect:
                        HandleConnect(connection, frame);
                        break;
                    case NetworkEvent.Type.Data:
                        HandleData(connection, reader, frame);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        HandleDisconnect(connection, frame, "Unity Transport disconnected");
                        break;
                }
            }
        }

        private void HandleConnect(NetworkConnection connection, RuntimeFrame frame)
        {
            int index = FindConnectionIndex(connection);
            string peerId = index >= 0 ? _connections[index].PeerId : string.Empty;
            QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Connected,
                peerId,
                frame,
                "Unity Transport connected"));
        }

        private void HandleData(NetworkConnection connection, DataStreamReader reader, RuntimeFrame frame)
        {
            int length = reader.Length;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
                bytes[i] = reader.ReadByte();

            NetworkCodecDecodeResult decode = _codec.Decode(bytes);
            if (!decode.Success)
            {
                _codecErrorCount++;
                QueueError(GetPeerId(connection), frame, decode.Error.ToString());
                return;
            }

            if (!TryBindOrValidatePeerId(connection, decode.Message.SenderPeerId, frame))
                return;
            _pendingMessages.Add(decode.Message);
            _deliveredCount++;
            _deliveredBytes += length;
        }

        private void HandleDisconnect(NetworkConnection connection, RuntimeFrame frame, string reason)
        {
            int index = FindConnectionIndex(connection);
            string peerId = index >= 0 ? _connections[index].PeerId : string.Empty;
            if (index >= 0)
            {
                RemovePendingSendsForPeer(peerId);
                _connections.RemoveAt(index);
            }

            QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Disconnected,
                peerId,
                frame,
                reason));
        }

        private void DisconnectForSendFailure(int connectionIndex, RuntimeFrame frame, string reason)
        {
            if (connectionIndex < 0 || connectionIndex >= _connections.Count)
                return;

            ConnectionRecord record = _connections[connectionIndex];
            RemovePendingSendsForPeer(record.PeerId);
            RequestDisconnect(record.Connection);
            _connections.RemoveAt(connectionIndex);
            QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Disconnected,
                record.PeerId,
                frame,
                reason));
        }

        private bool TryBindOrValidatePeerId(
            NetworkConnection connection,
            string peerId,
            RuntimeFrame frame)
        {
            if (string.IsNullOrEmpty(peerId))
            {
                QueueError(string.Empty, frame, "Unity Transport rejected message with an empty sender peer id.");
                return false;
            }

            int index = FindConnectionIndex(connection);
            if (index < 0)
                return false;

            ConnectionRecord record = _connections[index];
            if (string.Equals(record.PeerId, peerId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrEmpty(record.PeerId))
            {
                DisconnectForSendFailure(
                    index,
                    frame,
                    "Unity Transport rejected sender peer id mismatch. bound="
                    + record.PeerId
                    + " received="
                    + peerId);
                return false;
            }

            int existingPeerIndex = FindConnectionIndex(peerId);
            if (existingPeerIndex >= 0 && existingPeerIndex != index)
            {
                DisconnectForSendFailure(
                    index,
                    frame,
                    "Unity Transport rejected duplicate peer id binding: " + peerId);
                return false;
            }

            _connections[index] = new ConnectionRecord(connection, peerId);
            QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Connected,
                peerId,
                frame,
                "Unity Transport peer identified"));
            return true;
        }

        private int FindConnectionIndex(string peerId)
        {
            string safePeerId = peerId ?? string.Empty;
            for (int i = 0; i < _connections.Count; i++)
            {
                if (string.Equals(_connections[i].PeerId, safePeerId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private int FindConnectionIndex(NetworkConnection connection)
        {
            for (int i = 0; i < _connections.Count; i++)
            {
                if (_connections[i].Connection == connection)
                    return i;
            }

            return -1;
        }

        private string GetPeerId(NetworkConnection connection)
        {
            int index = FindConnectionIndex(connection);
            return index >= 0 ? _connections[index].PeerId : string.Empty;
        }

        private bool TryCompleteDriverUpdate(RuntimeFrame frame)
        {
            try
            {
                _driver.ScheduleUpdate().Complete();
                return true;
            }
            catch (Exception exception)
            {
                _droppedCount++;
                QueueError(
                    string.Empty,
                    frame,
                    "Unity Transport update failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
                DropTrackedConnections(frame, "Unity Transport update failed");
                StopDriverAfterUpdateFailure();
                return false;
            }
        }

        private void DropTrackedConnections(RuntimeFrame frame, string reason)
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                string peerId = _connections[i].PeerId;
                RemovePendingSendsForPeer(peerId);
                QueueEvent(new NetworkTransportEvent(
                    NetworkTransportEventKind.Disconnected,
                    peerId,
                    frame,
                    reason));
                _connections.RemoveAt(i);
            }
        }

        private void StopDriverAfterUpdateFailure()
        {
            DisposeDriverOnly();
            _connections.Clear();
            _pendingMessages.Clear();
            _pendingSends.Clear();
            _lastPumpFrame = long.MinValue;
        }

        private NetworkConnection.State GetConnectionState(NetworkConnection connection)
        {
            if (!_driver.IsCreated || !connection.IsCreated)
                return NetworkConnection.State.Disconnected;

            try
            {
                return _driver.GetConnectionState(connection);
            }
            catch (Exception exception)
            {
                _droppedCount++;
                QueueError(
                    string.Empty,
                    RuntimeFrame.Zero,
                    "Unity Transport connection state failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
                return NetworkConnection.State.Disconnected;
            }
        }

        private bool RequestDisconnect(NetworkConnection connection)
        {
            if (!_driver.IsCreated || !connection.IsCreated)
                return false;

            NetworkConnection.State state = GetConnectionState(connection);
            if (state == NetworkConnection.State.Disconnected)
                return false;

            _driver.Disconnect(connection);
            return true;
        }

        private void DisconnectAllBeforeDispose()
        {
            if (!_driver.IsCreated || _connections.Count == 0)
                return;

            bool requestedDisconnect = false;
            for (int i = 0; i < _connections.Count; i++)
                requestedDisconnect |= RequestDisconnect(_connections[i].Connection);

            if (requestedDisconnect)
                CompleteDriverUpdateForDisconnect();
        }

        private void CompleteDriverUpdateForDisconnect()
        {
            if (!_driver.IsCreated)
                return;

            try
            {
                _driver.ScheduleUpdate().Complete();
            }
            catch (Exception exception)
            {
                _droppedCount++;
                QueueError(
                    string.Empty,
                    RuntimeFrame.Zero,
                    "Unity Transport disconnect flush failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }
        }

        private void DisposeDriverOnly()
        {
            _started = false;
            if (_driver.IsCreated)
                _driver.Dispose();
        }

        private void QueueError(string peerId, RuntimeFrame frame, string message)
        {
            QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Error,
                peerId,
                frame,
                message));
        }

        private void QueueEvent(NetworkTransportEvent evt)
        {
            _events.Add(evt);
        }

        private void RecordSentBytes(NetworkMessageKind kind, int byteCount)
        {
            if (byteCount < 0)
                byteCount = 0;

            _sentBytes += byteCount;
            _lastSentPayloadBytes = byteCount;
            if (byteCount > _maxSentPayloadBytes)
                _maxSentPayloadBytes = byteCount;

            switch (kind)
            {
                case NetworkMessageKind.HandshakeRequest:
                case NetworkMessageKind.HandshakeResponse:
                    _handshakeSentBytes += byteCount;
                    break;
                case NetworkMessageKind.RuntimeCommands:
                case NetworkMessageKind.AuthoritativeCommands:
                    _runtimeCommandSentBytes += byteCount;
                    break;
                case NetworkMessageKind.Snapshot:
                    _snapshotSentBytes += byteCount;
                    break;
                case NetworkMessageKind.SnapshotRequest:
                    _snapshotRequestSentBytes += byteCount;
                    break;
                case NetworkMessageKind.Ping:
                case NetworkMessageKind.Pong:
                    _heartbeatSentBytes += byteCount;
                    break;
                case NetworkMessageKind.RoomCommand:
                case NetworkMessageKind.RoomState:
                    _roomSentBytes += byteCount;
                    break;
            }
        }

        private bool QueuePendingSend(NetworkMessageEnvelope message)
        {
            if (_pendingSends.Count >= MaxPendingSends)
            {
                _droppedCount++;
                QueueError(
                    message.ReceiverPeerId,
                    message.Frame,
                    "Unity Transport send failed: pending send queue is full while connection is not ready.");
                return false;
            }

            _pendingSends.Add(message);
            return true;
        }

        private void RemovePendingSendsForPeer(string peerId)
        {
            string safePeerId = peerId ?? string.Empty;
            for (int i = _pendingSends.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_pendingSends[i].ReceiverPeerId, safePeerId, StringComparison.Ordinal))
                    _pendingSends.RemoveAt(i);
            }
        }

        private void FlushPendingSends()
        {
            for (int i = 0; i < _pendingSends.Count;)
            {
                NetworkMessageEnvelope message = _pendingSends[i];
                SendAttemptResult result = TrySend(message, queueWhileConnecting: false);
                if (result == SendAttemptResult.StillConnecting)
                    return;

                RemovePendingSendIfStillQueued(message, i);
            }
        }

        private void RemovePendingSendIfStillQueued(NetworkMessageEnvelope message, int preferredIndex)
        {
            if (preferredIndex >= 0
                && preferredIndex < _pendingSends.Count
                && ReferenceEquals(_pendingSends[preferredIndex], message))
            {
                _pendingSends.RemoveAt(preferredIndex);
                return;
            }

            for (int i = _pendingSends.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_pendingSends[i], message))
                {
                    _pendingSends.RemoveAt(i);
                    return;
                }
            }
        }

        private static string FormatUnityTransportSendError(string operation, int statusCode)
        {
            return "Unity Transport "
                + operation
                + " failed: "
                + statusCode
                + " ("
                + GetUnityTransportStatusName(statusCode)
                + ")";
        }

        private static string GetUnityTransportStatusName(int statusCode)
        {
            if (Enum.IsDefined(typeof(UnityTransportStatusCode), statusCode))
                return ((UnityTransportStatusCode)statusCode).ToString();

            return "unknown";
        }

        private static bool TryCreateEndpoint(string address, ushort port, out NetworkEndpoint endpoint)
        {
            if (string.IsNullOrEmpty(address)
                || string.Equals(address, "0.0.0.0", StringComparison.Ordinal)
                || string.Equals(address, "*", StringComparison.Ordinal))
            {
                endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
                return true;
            }

            if (string.Equals(address, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port);
                return true;
            }

            return NetworkEndpoint.TryParse(address, port, out endpoint, NetworkFamily.Ipv4);
        }

        private readonly struct ConnectionRecord
        {
            public ConnectionRecord(NetworkConnection connection, string peerId)
            {
                Connection = connection;
                PeerId = peerId ?? string.Empty;
            }

            public NetworkConnection Connection { get; }
            public string PeerId { get; }
        }

        private sealed class NetworkMessageEnvelopeComparer : IComparer<NetworkMessageEnvelope>
        {
            public static readonly NetworkMessageEnvelopeComparer Instance = new NetworkMessageEnvelopeComparer();

            public int Compare(NetworkMessageEnvelope left, NetworkMessageEnvelope right)
            {
                int sequence = left.Sequence.CompareTo(right.Sequence);
                if (sequence != 0)
                    return sequence;
                return string.CompareOrdinal(left.SenderPeerId, right.SenderPeerId);
            }
        }

        private enum SendAttemptResult
        {
            Sent,
            Queued,
            StillConnecting,
            Failed
        }
    }
}
