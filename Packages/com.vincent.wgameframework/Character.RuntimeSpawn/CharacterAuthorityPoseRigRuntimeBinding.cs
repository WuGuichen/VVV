using System;

namespace MxFramework.CharacterRuntimeSpawn
{
    public enum CharacterAuthorityPoseRigHandedness
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    public enum CharacterAuthorityPoseRigAttachmentKind
    {
        Unknown = 0,
        Collider = 1,
        Socket = 2,
        ContactPoint = 3
    }

    public readonly struct CharacterAuthorityPoseQuantizedVector3
    {
        public CharacterAuthorityPoseQuantizedVector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }
    }

    public sealed class CharacterAuthorityPoseChannelRuntimeBinding
    {
        public CharacterAuthorityPoseChannelRuntimeBinding(
            int ordinal,
            int channelId,
            string bonePath,
            int partId,
            string channelLabel = "",
            string partLabel = "")
        {
            Ordinal = ordinal;
            ChannelId = channelId;
            ChannelLabel = channelLabel ?? string.Empty;
            BonePath = bonePath ?? string.Empty;
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
        }

        public int Ordinal { get; }
        public int ChannelId { get; }
        public string ChannelLabel { get; }
        public string BonePath { get; }
        public int PartId { get; }
        public string PartLabel { get; }
    }

    public sealed class CharacterAuthorityPoseLimbRuntimeBinding
    {
        public CharacterAuthorityPoseLimbRuntimeBinding(
            int ordinal,
            int probeId,
            int ikBindingId,
            int partStateId,
            CharacterAuthorityPoseRigHandedness handedness,
            int mirrorPeerProbeId,
            int rootChannelId,
            int midChannelId,
            int tipChannelId,
            CharacterAuthorityPoseQuantizedVector3 tipProbeOriginMillimeters,
            CharacterAuthorityPoseQuantizedVector3 tipProbeAxisSnorm,
            int maxUpDistanceMillimeters,
            int maxDownDistanceMillimeters,
            int radiusMillimeters,
            int clearanceMillimeters,
            int staticLayerMask,
            int minimumSlopeCosineMilli,
            CharacterAuthorityPoseQuantizedVector3 poleDirectionSnorm,
            int rootToMidLengthMillimeters,
            int midToTipLengthMillimeters,
            int minimumReachMillimeters,
            int maximumReachMillimeters,
            int worldNormalTargetOffsetMillimeters,
            string[] allowedMotionIds,
            int[] allowedPostureIds,
            string[] disabledActionTags,
            int enterFrames,
            int exitFrames,
            int lostFrames,
            string probeLabel = "",
            string ikBindingLabel = "",
            string partStateLabel = "",
            string mirrorPeerProbeLabel = "",
            string rootChannelLabel = "",
            string midChannelLabel = "",
            string tipChannelLabel = "",
            string[] allowedPostureLabels = null)
        {
            Ordinal = ordinal;
            ProbeId = probeId;
            ProbeLabel = probeLabel ?? string.Empty;
            IkBindingId = ikBindingId;
            IkBindingLabel = ikBindingLabel ?? string.Empty;
            PartStateId = partStateId;
            PartStateLabel = partStateLabel ?? string.Empty;
            Handedness = handedness;
            MirrorPeerProbeId = mirrorPeerProbeId;
            MirrorPeerProbeLabel = mirrorPeerProbeLabel ?? string.Empty;
            RootChannelId = rootChannelId;
            RootChannelLabel = rootChannelLabel ?? string.Empty;
            MidChannelId = midChannelId;
            MidChannelLabel = midChannelLabel ?? string.Empty;
            TipChannelId = tipChannelId;
            TipChannelLabel = tipChannelLabel ?? string.Empty;
            TipProbeOriginMillimeters = tipProbeOriginMillimeters;
            TipProbeAxisSnorm = tipProbeAxisSnorm;
            MaxUpDistanceMillimeters = maxUpDistanceMillimeters;
            MaxDownDistanceMillimeters = maxDownDistanceMillimeters;
            RadiusMillimeters = radiusMillimeters;
            ClearanceMillimeters = clearanceMillimeters;
            StaticLayerMask = staticLayerMask;
            MinimumSlopeCosineMilli = minimumSlopeCosineMilli;
            PoleDirectionSnorm = poleDirectionSnorm;
            RootToMidLengthMillimeters = rootToMidLengthMillimeters;
            MidToTipLengthMillimeters = midToTipLengthMillimeters;
            MinimumReachMillimeters = minimumReachMillimeters;
            MaximumReachMillimeters = maximumReachMillimeters;
            WorldNormalTargetOffsetMillimeters = worldNormalTargetOffsetMillimeters;
            AllowedMotionIds = allowedMotionIds ?? Array.Empty<string>();
            AllowedPostureIds = allowedPostureIds ?? Array.Empty<int>();
            AllowedPostureLabels = allowedPostureLabels ?? Array.Empty<string>();
            DisabledActionTags = disabledActionTags ?? Array.Empty<string>();
            EnterFrames = enterFrames;
            ExitFrames = exitFrames;
            LostFrames = lostFrames;
        }

        public int Ordinal { get; }
        public int ProbeId { get; }
        public string ProbeLabel { get; }
        public int IkBindingId { get; }
        public string IkBindingLabel { get; }
        public int PartStateId { get; }
        public string PartStateLabel { get; }
        public CharacterAuthorityPoseRigHandedness Handedness { get; }
        public int MirrorPeerProbeId { get; }
        public string MirrorPeerProbeLabel { get; }
        public int RootChannelId { get; }
        public string RootChannelLabel { get; }
        public int MidChannelId { get; }
        public string MidChannelLabel { get; }
        public int TipChannelId { get; }
        public string TipChannelLabel { get; }
        public CharacterAuthorityPoseQuantizedVector3 TipProbeOriginMillimeters { get; }
        public CharacterAuthorityPoseQuantizedVector3 TipProbeAxisSnorm { get; }
        public int MaxUpDistanceMillimeters { get; }
        public int MaxDownDistanceMillimeters { get; }
        public int RadiusMillimeters { get; }
        public int ClearanceMillimeters { get; }
        public int StaticLayerMask { get; }
        public int MinimumSlopeCosineMilli { get; }
        public CharacterAuthorityPoseQuantizedVector3 PoleDirectionSnorm { get; }
        public int RootToMidLengthMillimeters { get; }
        public int MidToTipLengthMillimeters { get; }
        public int MinimumReachMillimeters { get; }
        public int MaximumReachMillimeters { get; }
        public int WorldNormalTargetOffsetMillimeters { get; }
        public string[] AllowedMotionIds { get; }
        public int[] AllowedPostureIds { get; }
        public string[] AllowedPostureLabels { get; }
        public string[] DisabledActionTags { get; }
        public int EnterFrames { get; }
        public int ExitFrames { get; }
        public int LostFrames { get; }
    }

    public sealed class CharacterAuthorityPoseAttachmentRuntimeBinding
    {
        public CharacterAuthorityPoseAttachmentRuntimeBinding(
            int ordinal,
            CharacterAuthorityPoseRigAttachmentKind kind,
            int stableId,
            int channelId,
            string channelLabel = "",
            string stableLabel = "")
        {
            Ordinal = ordinal;
            Kind = kind;
            StableId = stableId;
            ChannelId = channelId;
            ChannelLabel = channelLabel ?? string.Empty;
            StableLabel = stableLabel ?? string.Empty;
        }

        public int Ordinal { get; }
        public CharacterAuthorityPoseRigAttachmentKind Kind { get; }
        public int StableId { get; }
        public string StableLabel { get; }
        public int ChannelId { get; }
        public string ChannelLabel { get; }
    }

    public sealed class CharacterAuthorityPoseRigRuntimeBinding
    {
        public const int MaximumChannelCount = 12;
        public const int MaximumLimbCount = 4;
        public const int MaximumAttachmentCount = 32;

        public static CharacterAuthorityPoseRigRuntimeBinding Missing { get; } =
            new CharacterAuthorityPoseRigRuntimeBinding(
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                Array.Empty<CharacterAuthorityPoseChannelRuntimeBinding>(),
                Array.Empty<CharacterAuthorityPoseLimbRuntimeBinding>(),
                Array.Empty<CharacterAuthorityPoseAttachmentRuntimeBinding>());

        public CharacterAuthorityPoseRigRuntimeBinding(
            string format,
            string schemaVersion,
            int revision,
            string contentHash,
            string coordinateSpace,
            string distanceUnit,
            int normalQuantizationScale,
            CharacterAuthorityPoseChannelRuntimeBinding[] channels,
            CharacterAuthorityPoseLimbRuntimeBinding[] limbs,
            CharacterAuthorityPoseAttachmentRuntimeBinding[] attachments)
        {
            Format = format ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            Revision = revision;
            ContentHash = contentHash ?? string.Empty;
            CoordinateSpace = coordinateSpace ?? string.Empty;
            DistanceUnit = distanceUnit ?? string.Empty;
            NormalQuantizationScale = normalQuantizationScale;
            Channels = channels ?? Array.Empty<CharacterAuthorityPoseChannelRuntimeBinding>();
            Limbs = limbs ?? Array.Empty<CharacterAuthorityPoseLimbRuntimeBinding>();
            Attachments = attachments ?? Array.Empty<CharacterAuthorityPoseAttachmentRuntimeBinding>();
        }

        public string Format { get; }
        public string SchemaVersion { get; }
        public int Revision { get; }
        public string ContentHash { get; }
        public string CoordinateSpace { get; }
        public string DistanceUnit { get; }
        public int NormalQuantizationScale { get; }
        public CharacterAuthorityPoseChannelRuntimeBinding[] Channels { get; }
        public CharacterAuthorityPoseLimbRuntimeBinding[] Limbs { get; }
        public CharacterAuthorityPoseAttachmentRuntimeBinding[] Attachments { get; }
        public bool IsAuthored =>
            string.Equals(Format, "mx.characterAuthorityPoseRig.v2", StringComparison.Ordinal)
            && string.Equals(SchemaVersion, "2.0", StringComparison.Ordinal)
            && Revision > 0
            && !string.IsNullOrWhiteSpace(ContentHash);
    }
}
