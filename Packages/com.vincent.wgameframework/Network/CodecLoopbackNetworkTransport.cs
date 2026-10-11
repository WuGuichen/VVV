using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public sealed class CodecLoopbackNetworkTransport : INetworkTransport, INetworkBoundedPacketTransport
    {
        private readonly List<PendingWireMessage> _pending = new List<PendingWireMessage>();
        private readonly List<PendingWireMessage> _ready = new List<PendingWireMessage>();
        private readonly List<NetworkTransportEvent> _events = new List<NetworkTransportEvent>();
        private readonly LoopbackNetworkTransportOptions _options;
        private readonly INetworkMessageCodec _codec;
        private readonly Random _random;
        private long _sentCount;
        private long _deliveredCount;
        private long _droppedCount;
        private long _reorderedCount;
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
        private BoundedPendingPacket[] _boundedPending;
        private byte[] _boundedSlab;
        private int _boundedPacketBytes;
        private int _boundedPendingCount;

        public CodecLoopbackNetworkTransport()
            : this(new JsonNetworkMessageCodec(), LoopbackNetworkTransportOptions.ReliableImmediate)
        {
        }

        public CodecLoopbackNetworkTransport(INetworkMessageCodec codec)
            : this(codec, LoopbackNetworkTransportOptions.ReliableImmediate)
        {
        }

        public CodecLoopbackNetworkTransport(INetworkMessageCodec codec, LoopbackNetworkTransportOptions options)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _options = options;
            _random = new Random(options.RandomSeed);
        }

        public string TransportId => "codec-loopback:" + _codec.CodecId;

        /// <summary>Preallocates the protocol-2 bounded loopback queue.</summary>
        /// <remarks>AllocByDesign initialization operation. Existing protocol-1 behavior is unchanged.</remarks>
        public void ConfigureBounded(int pendingCapacity, int maxPacketBytes)
        {
            if (pendingCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(pendingCapacity));
            if (maxPacketBytes < NetworkBoundedWire.HeaderBytes
                || maxPacketBytes > NetworkBoundedWire.SnapshotTotalLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }

            _boundedPending = new BoundedPendingPacket[pendingCapacity];
            _boundedSlab = new byte[checked(pendingCapacity * maxPacketBytes)];
            _boundedPacketBytes = maxPacketBytes;
            _boundedPendingCount = 0;
        }

        /// <remarks>NoAlloc after <see cref="ConfigureBounded"/>.</remarks>
        public NetworkBoundedResult TrySendBounded(
            ReadOnlySpan<byte> packet,
            in NetworkBoundedAuthenticatedMetadata authenticatedMetadata,
            RuntimeFrame deliveryFrame)
        {
            if (_boundedPending == null)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            if (!authenticatedMetadata.IsValid)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            if (_boundedPendingCount >= _boundedPending.Length)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CapacityExceeded);
            if (packet.Length < NetworkBoundedWire.HeaderBytes)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidLength);
            if (packet.Length > _boundedPacketBytes)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.PacketTooLarge);

            int slot = FindFreeBoundedSlot();
            if (slot < 0)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CapacityExceeded);
            packet.CopyTo(_boundedSlab.AsSpan(slot * _boundedPacketBytes, packet.Length));
            _boundedPending[slot] = new BoundedPendingPacket(
                true,
                authenticatedMetadata,
                deliveryFrame,
                packet.Length,
                _sentCount++);
            _boundedPendingCount++;
            RecordBoundedSentBytes(packet.Length);
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc after <see cref="ConfigureBounded"/>. Copies one complete packet into caller storage.</remarks>
        public NetworkBoundedResult TryPollBounded(
            uint receiverPeerHandle,
            RuntimeFrame frame,
            Span<byte> destination,
            out int packetBytes,
            out NetworkBoundedAuthenticatedMetadata authenticatedMetadata)
        {
            packetBytes = 0;
            authenticatedMetadata = default;
            if (_boundedPending == null)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            int selected = -1;
            long selectedOrder = long.MaxValue;
            for (int i = 0; i < _boundedPending.Length; i++)
            {
                BoundedPendingPacket pending = _boundedPending[i];
                if (!pending.Occupied
                    || pending.AuthenticatedMetadata.LocalPeerHandle != receiverPeerHandle
                    || pending.DeliveryFrame > frame
                    || pending.Order >= selectedOrder)
                {
                    continue;
                }
                selected = i;
                selectedOrder = pending.Order;
            }
            if (selected < 0)
                return NetworkBoundedResult.Succeeded();
            BoundedPendingPacket packet = _boundedPending[selected];
            if (destination.Length < packet.Length)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CapacityExceeded);
            _boundedSlab.AsSpan(selected * _boundedPacketBytes, packet.Length).CopyTo(destination);
            packetBytes = packet.Length;
            authenticatedMetadata = packet.AuthenticatedMetadata;
            _boundedPending[selected] = default;
            _boundedPendingCount--;
            _deliveredCount++;
            _deliveredBytes += packet.Length;
            return NetworkBoundedResult.Succeeded();
        }

        public void Send(NetworkMessageEnvelope message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _sentCount++;
            NetworkCodecEncodeResult encode = _codec.Encode(message);
            if (!encode.Success)
            {
                _codecErrorCount++;
                QueueEvent(new NetworkTransportEvent(
                    NetworkTransportEventKind.Error,
                    message.SenderPeerId,
                    message.Frame,
                    encode.Error.ToString()));
                return;
            }

            RecordSentBytes(message.Kind, encode.Bytes.Length);
            if (_options.DropRate > 0d && _random.NextDouble() < _options.DropRate)
            {
                _droppedCount++;
                return;
            }

            long jitter = _options.JitterFrames <= 0
                ? 0L
                : _random.Next(-_options.JitterFrames, _options.JitterFrames + 1);
            long delivery = message.Frame.Value + _options.LatencyFrames + jitter;
            if (delivery < message.Frame.Value)
                delivery = message.Frame.Value;

            long order = _options.ReorderMessages ? -_sentCount : _sentCount;
            if (_options.ReorderMessages)
                _reorderedCount++;

            _pending.Add(new PendingWireMessage(
                message.ReceiverPeerId,
                message.SenderPeerId,
                message.Sequence,
                message.Channel,
                encode.Bytes,
                new RuntimeFrame(delivery),
                order));
        }

        public int Poll(string receiverPeerId, RuntimeFrame frame, List<NetworkMessageEnvelope> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            string safePeerId = receiverPeerId ?? string.Empty;
            _ready.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingWireMessage pending = _pending[i];
                if (pending.DeliveryFrame > frame)
                    continue;
                if (!string.Equals(pending.ReceiverPeerId, safePeerId, StringComparison.Ordinal))
                    continue;

                _pending.RemoveAt(i);
                _ready.Add(pending);
            }

            if (_ready.Count > 0)
            {
                _ready.Sort(PendingWireMessageComparer.Instance);
                for (int i = 0; i < _ready.Count; i++)
                {
                    PendingWireMessage pending = _ready[i];
                    NetworkCodecDecodeResult decode = _codec.Decode(pending.Bytes);
                    if (!decode.Success)
                    {
                        _codecErrorCount++;
                        QueueEvent(new NetworkTransportEvent(
                            NetworkTransportEventKind.Error,
                            safePeerId,
                            frame,
                            decode.Error.ToString()));
                        continue;
                    }

                    output.Add(decode.Message);
                    _deliveredCount++;
                    _deliveredBytes += pending.Bytes.Length;
                }
            }

            return output.Count - start;
        }

        public int PollEvents(string peerId, RuntimeFrame frame, List<NetworkTransportEvent> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                NetworkTransportEvent evt = _events[i];
                if (evt.Frame > frame)
                    continue;

                _events.RemoveAt(i);
                output.Add(evt);
            }

            return output.Count - start;
        }

        public NetworkTransportSnapshot CreateSnapshot(string peerId)
        {
            int pendingForPeer = 0;
            string safePeerId = peerId ?? string.Empty;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (string.Equals(_pending[i].ReceiverPeerId, safePeerId, StringComparison.Ordinal))
                    pendingForPeer++;
            }

            return new NetworkTransportSnapshot(
                safePeerId,
                pendingForPeer,
                _sentCount,
                _deliveredCount,
                _droppedCount,
                _reorderedCount,
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

        public void QueueEvent(NetworkTransportEvent evt)
        {
            _events.Add(evt);
        }

        public void InjectRaw(
            string receiverPeerId,
            byte[] bytes,
            RuntimeFrame deliveryFrame,
            NetworkChannel channel = NetworkChannel.ReliableOrdered,
            long sequence = 0L,
            string senderPeerId = "")
        {
            _pending.Add(new PendingWireMessage(
                receiverPeerId,
                senderPeerId,
                sequence,
                channel,
                bytes ?? Array.Empty<byte>(),
                deliveryFrame,
                _sentCount));
        }

        public void Clear()
        {
            _pending.Clear();
            _ready.Clear();
            _events.Clear();
            if (_boundedPending != null)
                Array.Clear(_boundedPending, 0, _boundedPending.Length);
            _boundedPendingCount = 0;
        }

        private int FindFreeBoundedSlot()
        {
            for (int i = 0; i < _boundedPending.Length; i++)
            {
                if (!_boundedPending[i].Occupied)
                    return i;
            }
            return -1;
        }

        private readonly struct BoundedPendingPacket
        {
            public BoundedPendingPacket(
                bool occupied,
                in NetworkBoundedAuthenticatedMetadata authenticatedMetadata,
                RuntimeFrame deliveryFrame,
                int length,
                long order)
            {
                Occupied = occupied;
                AuthenticatedMetadata = authenticatedMetadata;
                DeliveryFrame = deliveryFrame;
                Length = length;
                Order = order;
            }

            public bool Occupied { get; }
            public NetworkBoundedAuthenticatedMetadata AuthenticatedMetadata { get; }
            public RuntimeFrame DeliveryFrame { get; }
            public int Length { get; }
            public long Order { get; }
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
                case NetworkMessageKind.BoundedRuntimeCommands:
                case NetworkMessageKind.BoundedAuthoritativeCommands:
                    _runtimeCommandSentBytes += byteCount;
                    break;
                case NetworkMessageKind.Snapshot:
                case NetworkMessageKind.BoundedStateSnapshot:
                    _snapshotSentBytes += byteCount;
                    break;
                case NetworkMessageKind.SnapshotRequest:
                case NetworkMessageKind.BoundedStateRequest:
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

        private void RecordBoundedSentBytes(int byteCount)
        {
            _sentBytes += byteCount;
            _lastSentPayloadBytes = byteCount;
            if (byteCount > _maxSentPayloadBytes)
                _maxSentPayloadBytes = byteCount;
        }

        private readonly struct PendingWireMessage
        {
            public PendingWireMessage(
                string receiverPeerId,
                string senderPeerId,
                long sequence,
                NetworkChannel channel,
                byte[] bytes,
                RuntimeFrame deliveryFrame,
                long deliveryOrder)
            {
                ReceiverPeerId = receiverPeerId ?? string.Empty;
                SenderPeerId = senderPeerId ?? string.Empty;
                Sequence = sequence;
                Channel = channel;
                Bytes = bytes ?? Array.Empty<byte>();
                DeliveryFrame = deliveryFrame;
                DeliveryOrder = deliveryOrder;
            }

            public string ReceiverPeerId { get; }
            public string SenderPeerId { get; }
            public long Sequence { get; }
            public NetworkChannel Channel { get; }
            public byte[] Bytes { get; }
            public RuntimeFrame DeliveryFrame { get; }
            public long DeliveryOrder { get; }
        }

        private sealed class PendingWireMessageComparer : IComparer<PendingWireMessage>
        {
            public static readonly PendingWireMessageComparer Instance = new PendingWireMessageComparer();

            public int Compare(PendingWireMessage left, PendingWireMessage right)
            {
                if (left.Channel == NetworkChannel.ReliableOrdered
                    && right.Channel == NetworkChannel.ReliableOrdered)
                {
                    return CompareMessages(left, right);
                }

                int delivery = left.DeliveryOrder.CompareTo(right.DeliveryOrder);
                if (delivery != 0)
                    return delivery;
                return CompareMessages(left, right);
            }

            private static int CompareMessages(PendingWireMessage left, PendingWireMessage right)
            {
                int sequence = left.Sequence.CompareTo(right.Sequence);
                if (sequence != 0)
                    return sequence;
                return string.CompareOrdinal(left.SenderPeerId, right.SenderPeerId);
            }
        }
    }
}
