using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    internal sealed class NetworkSnapshotRegistry
    {
        private readonly List<RuntimeCustomState> _snapshots = new List<RuntimeCustomState>();
        private readonly List<string> _participantIds = new List<string>();
        private readonly Dictionary<string, INetworkSnapshotParticipant> _participants =
            new Dictionary<string, INetworkSnapshotParticipant>(StringComparer.Ordinal);

        public int ApplyFailureCount { get; private set; }

        public void RegisterParticipant(INetworkSnapshotParticipant participant)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
            if (string.IsNullOrEmpty(participant.ParticipantId))
                throw new ArgumentException("Network snapshot participant id cannot be null or empty.", nameof(participant));
            if (_participants.ContainsKey(participant.ParticipantId))
                throw new ArgumentException("Duplicate network snapshot participant id: " + participant.ParticipantId, nameof(participant));

            _participants.Add(participant.ParticipantId, participant);
            _participantIds.Add(participant.ParticipantId);
            _participantIds.Sort(StringComparer.Ordinal);
        }

        public NetworkSnapshotPacket Capture(
            RuntimeFrame authoritativeFrame,
            RuntimeFrame ackedInputFrame,
            bool fullSnapshot,
            long resultHash,
            string receiverPeerId)
        {
            if (!fullSnapshot)
            {
                return new NetworkSnapshotPacket(
                    authoritativeFrame,
                    resultHash,
                    ackedInputFrame,
                    Array.Empty<RuntimeCustomState>(),
                    fullSnapshot: false);
            }

            _snapshots.Clear();
            for (int i = 0; i < _participantIds.Count; i++)
            {
                INetworkSnapshotParticipant participant = _participants[_participantIds[i]];
                if (participant is INetworkPeerSnapshotParticipant peerParticipant)
                    _snapshots.Add(peerParticipant.CaptureSnapshot(authoritativeFrame, receiverPeerId));
                else
                    _snapshots.Add(participant.CaptureSnapshot(authoritativeFrame));
            }

            return new NetworkSnapshotPacket(
                authoritativeFrame,
                resultHash,
                ackedInputFrame,
                _snapshots,
                fullSnapshot);
        }

        public bool Apply(NetworkSnapshotPacket packet, NetworkSessionDiagnostics diagnostics)
        {
            if (packet.FullSnapshot && !ValidateFullSnapshot(packet, diagnostics)) return false;
            bool success = true;
            for (int i = 0; i < packet.ModuleSnapshots.Count; i++)
            {
                RuntimeCustomState snapshot = packet.ModuleSnapshots[i];
                if (!_participants.TryGetValue(snapshot.TypeId, out INetworkSnapshotParticipant participant))
                {
                    success = false;
                    ApplyFailureCount++;
                    diagnostics.Add("missing snapshot participant: " + snapshot.TypeId);
                    continue;
                }

                NetworkSnapshotApplyResult result = participant.ApplySnapshot(packet.AuthoritativeFrame, snapshot);
                if (!result.Success)
                {
                    success = false;
                    ApplyFailureCount++;
                    diagnostics.Add(result.Message);
                }
            }

            return success;
        }

        public bool ValidateFullSnapshot(NetworkSnapshotPacket packet, NetworkSessionDiagnostics diagnostics)
        {
            bool valid = packet.ModuleSnapshots.Count == _participants.Count;
            for (int i = 0; valid && i < packet.ModuleSnapshots.Count; i++)
            {
                RuntimeCustomState state = packet.ModuleSnapshots[i];
                valid = state != null && _participants.ContainsKey(state.TypeId);
                for (int j = 0; valid && j < i; j++)
                    valid = !string.Equals(state.TypeId, packet.ModuleSnapshots[j].TypeId, StringComparison.Ordinal);
            }
            if (!valid)
            {
                ApplyFailureCount++;
                diagnostics.Add("full snapshot must contain every registered participant exactly once");
            }
            return valid;
        }
    }
}
