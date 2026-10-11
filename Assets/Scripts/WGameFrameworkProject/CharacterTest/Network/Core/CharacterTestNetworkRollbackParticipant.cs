using System;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestNetworkRollbackParticipant : INetworkRollbackParticipant
    {
        public const string ParticipantIdValue = "mxframework.character-test.rollback";

        private readonly CharacterTestSliceNetworkServices _networkServices;
        private readonly CharacterTestNetworkSnapshotParticipant _snapshotParticipant;

        public CharacterTestNetworkRollbackParticipant(
            CharacterTestSliceNetworkServices networkServices,
            CharacterTestNetworkSnapshotParticipant snapshotParticipant)
        {
            _networkServices = networkServices ?? throw new ArgumentNullException(nameof(networkServices));
            _snapshotParticipant = snapshotParticipant ?? throw new ArgumentNullException(nameof(snapshotParticipant));
        }

        public string ParticipantId => ParticipantIdValue;

        public object CaptureRollbackState(RuntimeFrame frame)
        {
            return _networkServices.CaptureRollbackState(frame, _snapshotParticipant);
        }

        public bool RestoreRollbackState(RuntimeFrame frame, object state, out string failure)
        {
            failure = string.Empty;
            return _networkServices.RestoreRollbackState(
                frame,
                state as CharacterTestNetworkRollbackState,
                _snapshotParticipant,
                out failure);
        }
    }
}
