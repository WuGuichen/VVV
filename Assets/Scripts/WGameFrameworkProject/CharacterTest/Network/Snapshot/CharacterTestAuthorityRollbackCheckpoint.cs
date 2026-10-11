using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Local sparse authority checkpoint used for rollback and Runtime AI Lab replay.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc. This local carrier reuses the authority document but never serializes through
    /// the network wire and does not contain presentation or diagnostic state.
    /// </remarks>
    internal sealed class CharacterTestAuthorityRollbackCheckpoint
    {
        internal CharacterTestAuthorityRollbackCheckpoint(
            RuntimeFrame frame,
            CharacterTestNetworkSnapshotDocument document)
        {
            Frame = frame;
            Document = document;
        }

        public RuntimeFrame Frame { get; }
        internal CharacterTestNetworkSnapshotDocument Document { get; }

        public static CharacterTestAuthorityRollbackCheckpoint Capture(
            CharacterTestNetworkSnapshotParticipant participant,
            RuntimeFrame frame)
        {
            if (participant == null)
                return null;

            return new CharacterTestAuthorityRollbackCheckpoint(
                frame,
                participant.CaptureRollbackDocument(frame));
        }

        public NetworkSnapshotApplyResult Restore(
            CharacterTestNetworkSnapshotParticipant participant,
            RuntimeFrame frame,
            bool preserveLocalPredictedEntity)
        {
            if (participant == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback participant is missing.");
            if (frame != Frame || Document == null || Document.Frame != frame.Value)
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback checkpoint frame mismatch.");

            return participant.ApplyRollbackDocument(
                frame,
                Document.CloneForRollback(),
                preserveLocalPredictedEntity);
        }
    }
}
