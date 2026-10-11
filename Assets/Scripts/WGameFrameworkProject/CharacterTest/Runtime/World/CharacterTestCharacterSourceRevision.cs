namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Deterministic source-pose identity owned by CharacterTest.
    ///
    /// These counters are write versions, not frame numbers or content hashes.
    /// A position/root-pose write advances <see cref="PoseRevision"/>; a yaw
    /// write advances <see cref="FacingRevision"/>. Authority-pose/socket
    /// writes advance the pose counter. The plan identity carries both
    /// counters and the quantized transform, so separating the write domains
    /// does not weaken stale detection. Reads never mutate this value.
    /// </summary>
    internal readonly struct CharacterTestCharacterSourceRevisionComponent
    {
        internal const int InitialRevision = 1;

        internal CharacterTestCharacterSourceRevisionComponent(
            int poseRevision,
            int facingRevision)
        {
            PoseRevision = Normalize(poseRevision);
            FacingRevision = Normalize(facingRevision);
        }

        internal int PoseRevision { get; }
        internal int FacingRevision { get; }

        internal CharacterTestCharacterSourceRevisionComponent TouchTransform(
            CharacterTestCharacterTransformComponent previous,
            CharacterTestCharacterTransformComponent next)
        {
            bool positionChanged = previous.XMillimeters != next.XMillimeters
                || previous.YMillimeters != next.YMillimeters
                || previous.ZMillimeters != next.ZMillimeters;
            bool facingChanged = previous.YawMilliDegrees != next.YawMilliDegrees;
            if (!positionChanged && !facingChanged)
                return this;

            return new CharacterTestCharacterSourceRevisionComponent(
                positionChanged ? Next(PoseRevision) : PoseRevision,
                facingChanged ? Next(FacingRevision) : FacingRevision);
        }

        internal CharacterTestCharacterSourceRevisionComponent TouchAuthorityPose()
        {
            return new CharacterTestCharacterSourceRevisionComponent(
                Next(PoseRevision),
                FacingRevision);
        }

        internal CharacterTestCharacterSourceRevisionComponent Restore(
            int poseRevision,
            int facingRevision)
        {
            return new CharacterTestCharacterSourceRevisionComponent(
                poseRevision,
                facingRevision);
        }

        private static int Normalize(int revision)
        {
            return revision > 0 ? revision : InitialRevision;
        }

        private static int Next(int revision)
        {
            // Saturation preserves monotonicity and makes exhaustion explicit
            // to the network/snapshot layer instead of wrapping to an old id.
            return revision >= int.MaxValue ? int.MaxValue : revision + 1;
        }
    }
}
