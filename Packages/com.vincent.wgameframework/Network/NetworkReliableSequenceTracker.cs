using System.Collections.Generic;

namespace MxFramework.Network
{
    internal sealed class NetworkReliableSequenceTracker
    {
        private readonly Dictionary<string, long> _lastSequenceBySender =
            new Dictionary<string, long>(System.StringComparer.Ordinal);

        public long LastReceivedSequence { get; private set; } = -1L;
        public int DuplicateMessageCount { get; private set; }
        public int OutOfOrderMessageCount { get; private set; }

        public void ResetSender(string senderPeerId)
        {
            _lastSequenceBySender.Remove(senderPeerId ?? string.Empty);
        }

        public bool ShouldProcess(NetworkMessageEnvelope message, NetworkSessionDiagnostics diagnostics)
        {
            if (message.Channel != NetworkChannel.ReliableOrdered)
                return true;

            string sender = message.SenderPeerId ?? string.Empty;
            if (_lastSequenceBySender.TryGetValue(sender, out long lastSequence))
            {
                if (message.Sequence == lastSequence)
                {
                    DuplicateMessageCount++;
                    diagnostics.Add("duplicate reliable message dropped sender=" + sender + " sequence=" + message.Sequence);
                    return false;
                }

                if (message.Sequence < lastSequence)
                {
                    OutOfOrderMessageCount++;
                    diagnostics.Add(
                        "out-of-order reliable message dropped sender=" + sender
                        + " sequence=" + message.Sequence
                        + " last=" + lastSequence);
                    return false;
                }

                // Sender sequences are global, not receiver-scoped. A client can
                // legitimately observe gaps when the same sender sends reliable
                // messages to other peers between messages for this receiver.
            }

            _lastSequenceBySender[sender] = message.Sequence;
            if (message.Sequence > LastReceivedSequence)
                LastReceivedSequence = message.Sequence;
            return true;
        }
    }
}
