using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Authoring
{
    public sealed partial class CharacterAuthoringGeometryBinding
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public CharacterAuthorityPoseRigBinding AuthorityPoseRig { get; set; }
    }

    public sealed class CharacterAuthorityPoseRigBinding
    {
        public string Format { get; set; } = CharacterAuthorityPoseRigDocument.CurrentFormat;
        public string SchemaVersion { get; set; } = CharacterAuthorityPoseRigDocument.CurrentSchemaVersion;
        public int Revision { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string CoordinateSpace { get; set; } = "LeftHandedModelRoot";
        public string DistanceUnit { get; set; } = "Millimeter";
        public int NormalQuantizationScale { get; set; } = CharacterAuthorityPoseRigLimits.NormalQuantizationScale;
        public List<CharacterAuthorityPoseChannelBinding> Channels { get; set; } =
            new List<CharacterAuthorityPoseChannelBinding>();
        public List<CharacterAuthorityPoseLimbBinding> Limbs { get; set; } =
            new List<CharacterAuthorityPoseLimbBinding>();
        public List<CharacterAuthorityPoseAttachmentBinding> Attachments { get; set; } =
            new List<CharacterAuthorityPoseAttachmentBinding>();
    }

    public sealed class CharacterAuthorityPoseChannelBinding
    {
        public int Ordinal { get; set; }
        public int ChannelId { get; set; }
        public string ChannelLabel { get; set; } = string.Empty;
        public string BonePath { get; set; } = string.Empty;
        public int PartId { get; set; }
        public string PartLabel { get; set; } = string.Empty;
        public string SourcePath { get; set; } = "geometry/authority_pose_rig.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthorityPoseLimbBinding
    {
        public int Ordinal { get; set; }
        public int ProbeId { get; set; }
        public string ProbeLabel { get; set; } = string.Empty;
        public int IkBindingId { get; set; }
        public string IkBindingLabel { get; set; } = string.Empty;
        public int PartStateId { get; set; }
        public string PartStateLabel { get; set; } = string.Empty;
        public CharacterAuthorityPoseHandedness Handedness { get; set; }
        public int MirrorPeerProbeId { get; set; }
        public string MirrorPeerProbeLabel { get; set; } = string.Empty;
        public int RootChannelId { get; set; }
        public string RootChannelLabel { get; set; } = string.Empty;
        public int MidChannelId { get; set; }
        public string MidChannelLabel { get; set; } = string.Empty;
        public int TipChannelId { get; set; }
        public string TipChannelLabel { get; set; } = string.Empty;
        public CharacterAuthoringIntVector3 TipProbeOriginMillimeters { get; set; } = new CharacterAuthoringIntVector3();
        public CharacterAuthoringIntVector3 TipProbeAxisSnorm { get; set; } = new CharacterAuthoringIntVector3();
        public int MaxUpDistanceMillimeters { get; set; }
        public int MaxDownDistanceMillimeters { get; set; }
        public int RadiusMillimeters { get; set; }
        public int ClearanceMillimeters { get; set; }
        public int StaticLayerMask { get; set; }
        public int MinimumSlopeCosineMilli { get; set; }
        public CharacterAuthoringIntVector3 PoleDirectionSnorm { get; set; } = new CharacterAuthoringIntVector3();
        public int RootToMidLengthMillimeters { get; set; }
        public int MidToTipLengthMillimeters { get; set; }
        public int MinimumReachMillimeters { get; set; }
        public int MaximumReachMillimeters { get; set; }
        public int WorldNormalTargetOffsetMillimeters { get; set; }
        public List<string> AllowedMotionIds { get; set; } = new List<string>();
        public List<int> AllowedPostureIds { get; set; } = new List<int>();
        public List<string> AllowedPostureLabels { get; set; } = new List<string>();
        public List<string> DisabledActionTags { get; set; } = new List<string>();
        public int EnterFrames { get; set; }
        public int ExitFrames { get; set; }
        public int LostFrames { get; set; }
        public string SourcePath { get; set; } = "geometry/authority_pose_rig.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthorityPoseAttachmentBinding
    {
        public int Ordinal { get; set; }
        public CharacterAuthorityPoseAttachmentKind Kind { get; set; }
        public int StableId { get; set; }
        public string StableLabel { get; set; } = string.Empty;
        public int ChannelId { get; set; }
        public string ChannelLabel { get; set; } = string.Empty;
        public string SourcePath { get; set; } = "geometry/authority_pose_rig.json";
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public static partial class CharacterAuthoringCompiler
    {
        private static bool HasBlockingAuthorityPoseRigIssue(CharacterAuthoringValidationReport validation)
        {
            List<CharacterAuthoringValidationIssue> issues = validation?.Issues;
            for (int i = 0; issues != null && i < issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = issues[i];
                if (issue != null
                    && issue.Severity == CharacterAuthoringValidationSeverity.Error
                    && string.Equals(issue.SourcePath, "geometry/authority_pose_rig.json", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ApplyAuthorityPoseRigBinding(
            CharacterAuthoringGeometryBinding binding,
            CharacterAuthorityPoseRigDocument source,
            CharacterContentIdentityResolver identityResolver,
            List<CharacterAuthoringValidationIssue> issues)
        {
            int Resolve(string key, string objectPath, string field, bool required = true) =>
                identityResolver.ResolveId(CharacterContentIdentityDomains.Pose, key,
                    "geometry/authority_pose_rig.json", objectPath, field, issues, required);
            CharacterAuthorityPoseRigDocument canonical =
                CharacterAuthorityPoseRigDocumentCodec.CreateCanonicalDocument(source);
            var runtime = new CharacterAuthorityPoseRigBinding
            {
                Format = "mx.characterAuthorityPoseRig.v2",
                SchemaVersion = "2.0",
                Revision = canonical.Revision,
                ContentHash = CharacterAuthorityPoseRigDocumentCodec.ComputeContentHash(canonical),
                CoordinateSpace = canonical.CoordinateSpace,
                DistanceUnit = canonical.DistanceUnit,
                NormalQuantizationScale = canonical.NormalQuantizationScale
            };
            for (int i = 0; i < canonical.Channels.Count; i++)
            {
                CharacterAuthorityPoseChannelAuthoring channel = canonical.Channels[i];
                runtime.Channels.Add(new CharacterAuthorityPoseChannelBinding
                {
                    Ordinal = i,
                    ChannelId = Resolve(channel.ChannelId, "authorityPoseRig/channels/" + channel.ChannelId, "channelId"),
                    ChannelLabel = channel.ChannelId,
                    BonePath = channel.BonePath,
                    PartId = Resolve(channel.PartId, "authorityPoseRig/channels/" + channel.ChannelId, "partId"),
                    PartLabel = channel.PartId,
                    SourceObjectPath = "authorityPoseRig/channels/" + channel.ChannelId
                });
            }
            for (int i = 0; i < canonical.Limbs.Count; i++)
            {
                CharacterAuthorityPoseLimbAuthoring limb = canonical.Limbs[i];
                runtime.Limbs.Add(new CharacterAuthorityPoseLimbBinding
                {
                    Ordinal = i,
                    ProbeId = Resolve(limb.ProbeId, "authorityPoseRig/limbs/" + limb.ProbeId, "probeId"),
                    ProbeLabel = limb.ProbeId,
                    IkBindingId = Resolve(limb.IkBindingId, "authorityPoseRig/limbs/" + limb.ProbeId, "ikBindingId"),
                    IkBindingLabel = limb.IkBindingId,
                    PartStateId = Resolve(limb.PartStateId, "authorityPoseRig/limbs/" + limb.ProbeId, "partStateId", required: false),
                    PartStateLabel = limb.PartStateId,
                    Handedness = limb.Handedness,
                    MirrorPeerProbeId = Resolve(limb.MirrorPeerProbeId, "authorityPoseRig/limbs/" + limb.ProbeId, "mirrorPeerProbeId"),
                    MirrorPeerProbeLabel = limb.MirrorPeerProbeId,
                    RootChannelId = Resolve(limb.RootChannelId, "authorityPoseRig/limbs/" + limb.ProbeId, "rootChannelId"),
                    RootChannelLabel = limb.RootChannelId,
                    MidChannelId = Resolve(limb.MidChannelId, "authorityPoseRig/limbs/" + limb.ProbeId, "midChannelId"),
                    MidChannelLabel = limb.MidChannelId,
                    TipChannelId = Resolve(limb.TipChannelId, "authorityPoseRig/limbs/" + limb.ProbeId, "tipChannelId"),
                    TipChannelLabel = limb.TipChannelId,
                    TipProbeOriginMillimeters = CloneIntVector(limb.TipProbeOriginMillimeters),
                    TipProbeAxisSnorm = CloneIntVector(limb.TipProbeAxisSnorm),
                    MaxUpDistanceMillimeters = limb.MaxUpDistanceMillimeters,
                    MaxDownDistanceMillimeters = limb.MaxDownDistanceMillimeters,
                    RadiusMillimeters = limb.RadiusMillimeters,
                    ClearanceMillimeters = limb.ClearanceMillimeters,
                    StaticLayerMask = limb.StaticLayerMask,
                    MinimumSlopeCosineMilli = limb.MinimumSlopeCosineMilli,
                    PoleDirectionSnorm = CloneIntVector(limb.PoleDirectionSnorm),
                    RootToMidLengthMillimeters = limb.RootToMidLengthMillimeters,
                    MidToTipLengthMillimeters = limb.MidToTipLengthMillimeters,
                    MinimumReachMillimeters = limb.MinimumReachMillimeters,
                    MaximumReachMillimeters = limb.MaximumReachMillimeters,
                    WorldNormalTargetOffsetMillimeters = limb.WorldNormalTargetOffsetMillimeters,
                    AllowedMotionIds = new List<string>(limb.AllowedMotionIds),
                    AllowedPostureIds = limb.AllowedPostureIds.ConvertAll(value =>
                        Resolve(value, "authorityPoseRig/limbs/" + limb.ProbeId, "allowedPostureIds")),
                    AllowedPostureLabels = new List<string>(limb.AllowedPostureIds),
                    DisabledActionTags = new List<string>(limb.DisabledActionTags),
                    EnterFrames = limb.EnterFrames,
                    ExitFrames = limb.ExitFrames,
                    LostFrames = limb.LostFrames,
                    SourceObjectPath = "authorityPoseRig/limbs/" + limb.ProbeId
                });
            }
            for (int i = 0; i < canonical.Attachments.Count; i++)
            {
                CharacterAuthorityPoseAttachmentAuthoring attachment = canonical.Attachments[i];
                runtime.Attachments.Add(new CharacterAuthorityPoseAttachmentBinding
                {
                    Ordinal = i,
                    Kind = attachment.Kind,
                    StableId = Resolve(attachment.StableId, "authorityPoseRig/attachments/" + attachment.StableId, "stableId"),
                    StableLabel = attachment.StableId,
                    ChannelId = Resolve(attachment.ChannelId, "authorityPoseRig/attachments/" + attachment.StableId, "channelId"),
                    ChannelLabel = attachment.ChannelId,
                    SourceObjectPath = "authorityPoseRig/attachments/" + attachment.Kind + "/" + attachment.StableId
                });
            }
            foreach (CharacterAuthorityPoseLimbBinding limb in runtime.Limbs)
                limb.AllowedPostureIds.Sort();
            binding.AuthorityPoseRig = runtime;
        }

        private static CharacterAuthoringIntVector3 CloneIntVector(CharacterAuthoringIntVector3 value) =>
            value == null ? new CharacterAuthoringIntVector3() : new CharacterAuthoringIntVector3(value.X, value.Y, value.Z);

        private static void AppendAuthorityPoseRigCanonical(
            StringBuilder builder,
            CharacterAuthorityPoseRigBinding rig)
        {
            if (rig == null) return;
            builder.Append("authorityPoseRig|").Append(rig.Format).Append('|')
                .Append(rig.SchemaVersion).Append('|').Append(rig.Revision).Append('|')
                .Append(rig.ContentHash).Append('|').Append(rig.CoordinateSpace).Append('|')
                .Append(rig.DistanceUnit).Append('|').Append(rig.NormalQuantizationScale).Append('\n');
            for (int i = 0; i < rig.Channels.Count; i++)
            {
                CharacterAuthorityPoseChannelBinding channel = rig.Channels[i];
                builder.Append("authorityChannel|").Append(channel.Ordinal).Append('|')
                    .Append(channel.ChannelId).Append('|').Append(channel.BonePath).Append('|')
                    .Append(channel.PartId).Append('\n');
            }
            for (int i = 0; i < rig.Limbs.Count; i++)
            {
                CharacterAuthorityPoseLimbBinding limb = rig.Limbs[i];
                builder.Append("authorityLimb|").Append(limb.Ordinal).Append('|')
                    .Append(limb.ProbeId).Append('|').Append(limb.IkBindingId).Append('|')
                    .Append(limb.PartStateId).Append('|').Append(limb.Handedness).Append('|')
                    .Append(limb.MirrorPeerProbeId).Append('|').Append(limb.RootChannelId).Append('|')
                    .Append(limb.MidChannelId).Append('|').Append(limb.TipChannelId).Append('|');
                AppendIntVector(builder, limb.TipProbeOriginMillimeters);
                AppendIntVector(builder, limb.TipProbeAxisSnorm);
                builder.Append(limb.MaxUpDistanceMillimeters).Append('|')
                    .Append(limb.MaxDownDistanceMillimeters).Append('|').Append(limb.RadiusMillimeters).Append('|')
                    .Append(limb.ClearanceMillimeters).Append('|').Append(limb.StaticLayerMask).Append('|')
                    .Append(limb.MinimumSlopeCosineMilli).Append('|');
                AppendIntVector(builder, limb.PoleDirectionSnorm);
                builder.Append(limb.RootToMidLengthMillimeters).Append('|')
                    .Append(limb.MidToTipLengthMillimeters).Append('|')
                    .Append(limb.MinimumReachMillimeters).Append('|').Append(limb.MaximumReachMillimeters).Append('|')
                    .Append(limb.WorldNormalTargetOffsetMillimeters).Append('|');
                AppendStringList(builder, limb.AllowedMotionIds);
                AppendIntList(builder, limb.AllowedPostureIds);
                AppendStringList(builder, limb.DisabledActionTags);
                builder.Append(limb.EnterFrames).Append('|').Append(limb.ExitFrames).Append('|')
                    .Append(limb.LostFrames).Append('\n');
            }
            for (int i = 0; i < rig.Attachments.Count; i++)
            {
                CharacterAuthorityPoseAttachmentBinding attachment = rig.Attachments[i];
                builder.Append("authorityAttachment|").Append(attachment.Ordinal).Append('|')
                    .Append(attachment.Kind).Append('|').Append(attachment.StableId).Append('|')
                    .Append(attachment.ChannelId).Append('\n');
            }
        }

        private static void AppendIntVector(StringBuilder builder, CharacterAuthoringIntVector3 value)
        {
            CharacterAuthoringIntVector3 safe = value ?? new CharacterAuthoringIntVector3();
            builder.Append(safe.X).Append(',').Append(safe.Y).Append(',').Append(safe.Z).Append('|');
        }

        private static void AddAuthorityPoseRigSourceMappings(
            List<CharacterPackageSourceMapping> mappings,
            CharacterAuthorityPoseRigBinding rig)
        {
            if (rig == null) return;
            mappings.Add(new CharacterPackageSourceMapping
            {
                SourcePath = "geometry/authority_pose_rig.json",
                SourceObjectPath = "authorityPoseRig",
                TargetKind = "GeometryBinding",
                TargetPath = "authorityPoseRig",
                TargetField = "set"
            });
            for (int i = 0; i < rig.Channels.Count; i++)
            {
                CharacterAuthorityPoseChannelBinding channel = rig.Channels[i];
                mappings.Add(new CharacterPackageSourceMapping
                {
                    SourcePath = channel.SourcePath,
                    SourceObjectPath = channel.SourceObjectPath,
                    TargetKind = "GeometryBinding",
                    TargetPath = "authorityPoseRig/channels/" + channel.Ordinal,
                    TargetField = "channel"
                });
            }
            for (int i = 0; i < rig.Limbs.Count; i++)
            {
                CharacterAuthorityPoseLimbBinding limb = rig.Limbs[i];
                mappings.Add(new CharacterPackageSourceMapping
                {
                    SourcePath = limb.SourcePath,
                    SourceObjectPath = limb.SourceObjectPath,
                    TargetKind = "GeometryBinding",
                    TargetPath = "authorityPoseRig/limbs/" + limb.Ordinal,
                    TargetField = "limb"
                });
            }
            for (int i = 0; i < rig.Attachments.Count; i++)
            {
                CharacterAuthorityPoseAttachmentBinding attachment = rig.Attachments[i];
                mappings.Add(new CharacterPackageSourceMapping
                {
                    SourcePath = attachment.SourcePath,
                    SourceObjectPath = attachment.SourceObjectPath,
                    TargetKind = "GeometryBinding",
                    TargetPath = "authorityPoseRig/attachments/" + attachment.Ordinal,
                    TargetField = "attachment"
                });
            }
        }
    }
}
