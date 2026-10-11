using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public readonly struct LoopbackNetworkTransportOptions
    {
        public LoopbackNetworkTransportOptions(
            int latencyFrames,
            int jitterFrames = 0,
            double dropRate = 0d,
            bool reorderMessages = false,
            int randomSeed = 12345)
        {
            if (latencyFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(latencyFrames), "Loopback latency cannot be negative.");
            if (jitterFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(jitterFrames), "Loopback jitter cannot be negative.");
            if (dropRate < 0d || dropRate > 1d || double.IsNaN(dropRate))
                throw new ArgumentOutOfRangeException(nameof(dropRate), "Loopback drop rate must be between 0 and 1.");

            LatencyFrames = latencyFrames;
            JitterFrames = jitterFrames;
            DropRate = dropRate;
            ReorderMessages = reorderMessages;
            RandomSeed = randomSeed;
        }

        public int LatencyFrames { get; }
        public int JitterFrames { get; }
        public double DropRate { get; }
        public bool ReorderMessages { get; }
        public int RandomSeed { get; }

        public static LoopbackNetworkTransportOptions ReliableImmediate =>
            new LoopbackNetworkTransportOptions(0);
    }

    public sealed class LoopbackNetworkTransport : INetworkTransport
    {
        private readonly List<PendingMessage> _pending = new List<PendingMessage>();
        private readonly List<PendingMessage> _ready = new List<PendingMessage>();
        private readonly List<NetworkTransportEvent> _events = new List<NetworkTransportEvent>();
        private readonly LoopbackNetworkTransportOptions _options;
        private readonly Random _random;
        private long _sentCount;
        private long _deliveredCount;
        private long _droppedCount;
        private long _reorderedCount;

        public LoopbackNetworkTransport()
            : this(LoopbackNetworkTransportOptions.ReliableImmediate)
        {
        }

        public LoopbackNetworkTransport(LoopbackNetworkTransportOptions options)
        {
            _options = options;
            _random = new Random(options.RandomSeed);
        }

        public string TransportId => "loopback";

        public void Send(NetworkMessageEnvelope message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _sentCount++;
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
            _pending.Add(new PendingMessage(message, new RuntimeFrame(delivery), order));
        }

        public int Poll(string receiverPeerId, RuntimeFrame frame, List<NetworkMessageEnvelope> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            _ready.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingMessage pending = _pending[i];
                if (pending.DeliveryFrame > frame)
                    continue;
                if (!string.Equals(pending.Message.ReceiverPeerId, receiverPeerId ?? string.Empty, StringComparison.Ordinal))
                    continue;

                _pending.RemoveAt(i);
                _ready.Add(pending);
                _deliveredCount++;
            }

            if (_ready.Count > 0)
            {
                _ready.Sort(PendingMessageComparer.Instance);
                for (int i = 0; i < _ready.Count; i++)
                    output.Add(_ready[i].Message);
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
                if (string.Equals(_pending[i].Message.ReceiverPeerId, safePeerId, StringComparison.Ordinal))
                    pendingForPeer++;
            }

            return new NetworkTransportSnapshot(
                safePeerId,
                pendingForPeer,
                _sentCount,
                _deliveredCount,
                _droppedCount,
                _reorderedCount);
        }

        public void QueueEvent(NetworkTransportEvent evt)
        {
            _events.Add(evt);
        }

        public void Clear()
        {
            _pending.Clear();
            _ready.Clear();
            _events.Clear();
        }

        private readonly struct PendingMessage
        {
            public PendingMessage(NetworkMessageEnvelope message, RuntimeFrame deliveryFrame, long deliveryOrder)
            {
                Message = message;
                DeliveryFrame = deliveryFrame;
                DeliveryOrder = deliveryOrder;
            }

            public NetworkMessageEnvelope Message { get; }
            public RuntimeFrame DeliveryFrame { get; }
            public long DeliveryOrder { get; }
        }

        private sealed class PendingMessageComparer : IComparer<PendingMessage>
        {
            public static readonly PendingMessageComparer Instance = new PendingMessageComparer();

            public int Compare(PendingMessage left, PendingMessage right)
            {
                if (left.Message.Channel == NetworkChannel.ReliableOrdered
                    && right.Message.Channel == NetworkChannel.ReliableOrdered)
                {
                    return CompareMessages(left.Message, right.Message);
                }

                int delivery = left.DeliveryOrder.CompareTo(right.DeliveryOrder);
                if (delivery != 0)
                    return delivery;
                return CompareMessages(left.Message, right.Message);
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
}
