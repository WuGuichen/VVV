using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class CharacterAuthoringValidationCodes
    {
        public const string AuthorityPoseFutureSchema = "CHARPOSE001_FUTURE_SCHEMA";
        public const string AuthorityPoseReadOnlyDocument = "CHARPOSE002_READ_ONLY_DOCUMENT";
        public const string AuthorityPoseInvalidContract = "CHARPOSE003_INVALID_CONTRACT";
        public const string AuthorityPoseDuplicateIdentity = "CHARPOSE004_DUPLICATE_IDENTITY";
        public const string AuthorityPoseUnknownChannel = "CHARPOSE005_UNKNOWN_CHANNEL";
        public const string AuthorityPoseInvalidBoneChain = "CHARPOSE006_INVALID_BONE_CHAIN";
        public const string AuthorityPoseInvalidLength = "CHARPOSE007_INVALID_LENGTH";
        public const string AuthorityPoseInvalidProbe = "CHARPOSE008_INVALID_PROBE";
        public const string AuthorityPoseInvalidLayerMask = "CHARPOSE009_INVALID_LAYER_MASK";
        public const string AuthorityPoseMirrorMismatch = "CHARPOSE010_MIRROR_MISMATCH";
        public const string AuthorityPoseMissingSeveredMapping = "CHARPOSE011_MISSING_SEVERED_MAPPING";
        public const string AuthorityPoseChannelConflict = "CHARPOSE012_CHANNEL_CONFLICT";
        public const string AuthorityPoseInvalidAttachment = "CHARPOSE013_INVALID_ATTACHMENT";
    }

    public static partial class CharacterResourcePackageValidator
    {
        private const string AuthorityPoseRigSourcePath = "geometry/authority_pose_rig.json";

        private static void ValidateAuthorityPoseRig(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry)
        {
            if (geometry.AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Missing)
                return;
            if (geometry.AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Future)
            {
                PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseFutureSchema,
                    "authorityPoseRig", "format/schemaVersion",
                    "Authority-pose rig format or schema is unsupported; the original payload is read-only.",
                    "Use mx.characterAuthorityPoseRig.v1 schema 1.0 or an explicit migrator.");
                return;
            }

            CharacterAuthorityPoseRigDocument document = geometry.AuthorityPoseRig;
            if (geometry.AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Invalid
                || document == null)
            {
                PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseReadOnlyDocument,
                    "authorityPoseRig", string.Empty,
                    "Authority-pose rig could not be read without data loss and is write-ineligible.",
                    "Repair the JSON document before compiling the package.");
                return;
            }

            if (!string.Equals(document.Format, CharacterAuthorityPoseRigDocument.CurrentFormat, StringComparison.Ordinal)
                || !string.Equals(document.SchemaVersion, CharacterAuthorityPoseRigDocument.CurrentSchemaVersion, StringComparison.Ordinal)
                || document.Revision < 1
                || !string.Equals(document.CoordinateSpace, "LeftHandedModelRoot", StringComparison.Ordinal)
                || !string.Equals(document.DistanceUnit, "Millimeter", StringComparison.Ordinal)
                || document.NormalQuantizationScale != CharacterAuthorityPoseRigLimits.NormalQuantizationScale)
            {
                PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidContract,
                    "authorityPoseRig", "format/schemaVersion/revision/coordinateSpace/distanceUnit/normalQuantizationScale",
                    "Authority-pose rig requires v1, positive revision, LeftHandedModelRoot, millimeters and signed 1/32767 normals.",
                    "Use the frozen M4 coordinate and quantization contract.");
            }

            List<CharacterAuthorityPoseChannelAuthoring> channels =
                document.Channels ?? new List<CharacterAuthorityPoseChannelAuthoring>();
            List<CharacterAuthorityPoseLimbAuthoring> limbs =
                document.Limbs ?? new List<CharacterAuthorityPoseLimbAuthoring>();
            List<CharacterAuthorityPoseAttachmentAuthoring> attachments =
                document.Attachments ?? new List<CharacterAuthorityPoseAttachmentAuthoring>();
            if (channels.Count == 0 || channels.Count > CharacterAuthorityPoseRigLimits.MaximumChannelCount
                || limbs.Count == 0 || limbs.Count > CharacterAuthorityPoseRigLimits.MaximumLimbCount
                || attachments.Count > CharacterAuthorityPoseRigLimits.MaximumAttachmentCount)
            {
                PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidContract,
                    "authorityPoseRig", "channels/limbs/attachments",
                    "Authority-pose rig requires 1..12 channels, 1..4 limbs and at most 32 attachments.",
                    "Keep the first slice bounded to four limbs and twelve root/mid/tip channels.");
            }

            var knownParts = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; geometry.BodyParts != null && i < geometry.BodyParts.Count; i++)
            {
                string partId = geometry.BodyParts[i]?.PartId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(partId)) knownParts.Add(partId);
            }
            var knownStateIds = new HashSet<string>(StringComparer.Ordinal);
            var severedParts = new HashSet<string>(StringComparer.Ordinal);
            if (geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Current
                && geometry.BodyPartDamage?.PartStates != null)
            {
                for (int i = 0; i < geometry.BodyPartDamage.PartStates.Count; i++)
                {
                    CharacterBodyPartDamageAuthoring state = geometry.BodyPartDamage.PartStates[i];
                    if (state == null) continue;
                    if (!string.IsNullOrWhiteSpace(state.PartStateId)) knownStateIds.Add(state.PartStateId);
                    if (!state.Severable) continue;
                    for (int j = 0; j < (state.MemberPartIds?.Count ?? 0); j++)
                        severedParts.Add(state.MemberPartIds[j] ?? string.Empty);
                }
            }

            var channelsById = new Dictionary<string, CharacterAuthorityPoseChannelAuthoring>(StringComparer.Ordinal);
            for (int i = 0; i < channels.Count; i++)
            {
                CharacterAuthorityPoseChannelAuthoring channel = channels[i];
                string channelId = channel?.ChannelId ?? string.Empty;
                string objectPath = "authorityPoseRig/channels/" +
                    (!string.IsNullOrWhiteSpace(channelId) ? channelId : i.ToString(CultureInfo.InvariantCulture));
                if (!IsAuthorityPoseStableId(channelId) || !channelsById.TryAdd(channelId, channel))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseDuplicateIdentity,
                        objectPath, "channelId", "channelId must be a unique stable identity.",
                        "Use one lowercase package-local channel id.");
                if (channel == null || string.IsNullOrWhiteSpace(channel.BonePath)
                    || channel.BonePath.Contains("\\", StringComparison.Ordinal)
                    || channel.BonePath.Contains("..", StringComparison.Ordinal)
                    || !knownParts.Contains(channel.PartId ?? string.Empty))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidBoneChain,
                        objectPath, "bonePath/partId",
                        "Every channel requires one exact forward-slash bonePath and an existing body part.",
                        "Bind the channel to the imported skeleton and geometry/body_parts.json.");
            }

            var probesById = new Dictionary<string, CharacterAuthorityPoseLimbAuthoring>(StringComparer.Ordinal);
            var ikIds = new HashSet<string>(StringComparer.Ordinal);
            var controlledChannels = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < limbs.Count; i++)
            {
                CharacterAuthorityPoseLimbAuthoring limb = limbs[i];
                string probeId = limb?.ProbeId ?? string.Empty;
                string objectPath = "authorityPoseRig/limbs/" +
                    (!string.IsNullOrWhiteSpace(probeId) ? probeId : i.ToString(CultureInfo.InvariantCulture));
                if (!IsAuthorityPoseStableId(probeId) || !probesById.TryAdd(probeId, limb)
                    || !IsAuthorityPoseStableId(limb?.IkBindingId) || !ikIds.Add(limb.IkBindingId))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseDuplicateIdentity,
                        objectPath, "probeId/ikBindingId",
                        "probeId and ikBindingId must be unique stable identities.",
                        "Use one package-local identity for each probe and IK binding.");
                if (limb == null) continue;

                if (limb.Handedness != CharacterAuthorityPoseHandedness.Left
                    && limb.Handedness != CharacterAuthorityPoseHandedness.Right)
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseMirrorMismatch,
                        objectPath, "handedness", "Grounded limbs require explicit Left or Right handedness.",
                        "Author the side and reciprocal mirror peer.");

                bool hasRoot = channelsById.TryGetValue(limb.RootChannelId ?? string.Empty, out CharacterAuthorityPoseChannelAuthoring root);
                bool hasMid = channelsById.TryGetValue(limb.MidChannelId ?? string.Empty, out CharacterAuthorityPoseChannelAuthoring mid);
                bool hasTip = channelsById.TryGetValue(limb.TipChannelId ?? string.Empty, out CharacterAuthorityPoseChannelAuthoring tip);
                if (!hasRoot || !hasMid || !hasTip)
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseUnknownChannel,
                        objectPath, "rootChannelId/midChannelId/tipChannelId",
                        "A limb references an unknown authority channel.",
                        "Reference exact channelId values declared by this document.");
                else if (string.Equals(limb.RootChannelId, limb.MidChannelId, StringComparison.Ordinal)
                    || string.Equals(limb.RootChannelId, limb.TipChannelId, StringComparison.Ordinal)
                    || string.Equals(limb.MidChannelId, limb.TipChannelId, StringComparison.Ordinal)
                    || !IsDescendantBonePath(root.BonePath, mid.BonePath)
                    || !IsDescendantBonePath(mid.BonePath, tip.BonePath))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidBoneChain,
                        objectPath, "rootChannelId/midChannelId/tipChannelId",
                        "root/mid/tip must be distinct channels on one exact parent-to-child bone chain.",
                        "Bind the imported skeleton's upper, lower and terminal limb bones.");

                AddControlledChannel(report, controlledChannels, limb.RootChannelId, objectPath);
                AddControlledChannel(report, controlledChannels, limb.MidChannelId, objectPath);
                AddControlledChannel(report, controlledChannels, limb.TipChannelId, objectPath);

                long totalLength = (long)limb.RootToMidLengthMillimeters + limb.MidToTipLengthMillimeters;
                long minimumPhysicalReach = Math.Abs(
                    (long)limb.RootToMidLengthMillimeters - limb.MidToTipLengthMillimeters);
                if (!PositiveBounded(limb.RootToMidLengthMillimeters)
                    || !PositiveBounded(limb.MidToTipLengthMillimeters)
                    || limb.MinimumReachMillimeters < minimumPhysicalReach
                    || limb.MinimumReachMillimeters > limb.MaximumReachMillimeters
                    || limb.MaximumReachMillimeters > totalLength)
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidLength,
                        objectPath, "rootToMidLengthMillimeters/midToTipLengthMillimeters/minimumReachMillimeters/maximumReachMillimeters",
                        "Limb lengths must be positive and reach must stay inside the two-segment physical interval.",
                        "Use quantized measurements from the formal imported skeleton.");

                if (!NonNegativeBounded(limb.MaxUpDistanceMillimeters)
                    || !PositiveBounded(limb.MaxDownDistanceMillimeters)
                    || !PositiveBounded(limb.RadiusMillimeters)
                    || !NonNegativeBounded(limb.ClearanceMillimeters)
                    || !BoundedSigned(limb.WorldNormalTargetOffsetMillimeters)
                    || limb.MinimumSlopeCosineMilli < 0
                    || limb.MinimumSlopeCosineMilli > CharacterAuthorityPoseRigLimits.SlopeCosineScale
                    || !IsSnormVector(limb.TipProbeAxisSnorm)
                    || !IsSnormVector(limb.PoleDirectionSnorm)
                    || !BoundedVector(limb.TipProbeOriginMillimeters))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidProbe,
                        objectPath, "probeGeometry/pole/reach/slope",
                        "Probe geometry, slope, pole and target offset must be bounded quantized values with non-zero signed-normal vectors.",
                        "Use millimeters, slope cosine milli and signed 1/32767 vectors.");
                if (limb.StaticLayerMask <= 0)
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidLayerMask,
                        objectPath, "staticLayerMask", "staticLayerMask must contain at least one authored static layer.",
                        "Use the CombatPhysics static ground layer mask; dynamic character layers are not valid here.");
                if (!ValidHysteresis(limb.EnterFrames) || !ValidHysteresis(limb.ExitFrames)
                    || !ValidHysteresis(limb.LostFrames)
                    || !ValidStableList(limb.AllowedMotionIds)
                    || !ValidStableList(limb.AllowedPostureIds)
                    || !ValidStableList(limb.DisabledActionTags))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidProbe,
                        objectPath, "eligibility/hysteresis",
                        "Eligibility identities must be stable and hysteresis frames must be bounded.",
                        "Use unique stable ids and 0..600 deterministic frames.");

                if (!string.IsNullOrWhiteSpace(limb.PartStateId)
                    && !knownStateIds.Contains(limb.PartStateId))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseMissingSeveredMapping,
                        objectPath, "partStateId", "partStateId does not resolve to body_part_damage.json.",
                        "Reference the owning state or leave it empty for a non-severable limb.");
                if (string.IsNullOrWhiteSpace(limb.PartStateId)
                    && ((root != null && severedParts.Contains(root.PartId))
                        || (mid != null && severedParts.Contains(mid.PartId))
                        || (tip != null && severedParts.Contains(tip.PartId))))
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseMissingSeveredMapping,
                        objectPath, "partStateId",
                        "A limb intersecting a Severable body-part state requires an explicit partStateId.",
                        "Bind the exact M3 state so Severed disables probe, solve and contact.");
            }

            foreach (KeyValuePair<string, CharacterAuthorityPoseLimbAuthoring> pair in probesById)
            {
                CharacterAuthorityPoseLimbAuthoring limb = pair.Value;
                if (limb == null || !probesById.TryGetValue(limb.MirrorPeerProbeId ?? string.Empty, out CharacterAuthorityPoseLimbAuthoring peer)
                    || peer == null || !string.Equals(peer.MirrorPeerProbeId, limb.ProbeId, StringComparison.Ordinal)
                    || peer.Handedness == limb.Handedness
                    || peer.RootToMidLengthMillimeters != limb.RootToMidLengthMillimeters
                    || peer.MidToTipLengthMillimeters != limb.MidToTipLengthMillimeters
                    || peer.MinimumReachMillimeters != limb.MinimumReachMillimeters
                    || peer.MaximumReachMillimeters != limb.MaximumReachMillimeters)
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseMirrorMismatch,
                        "authorityPoseRig/limbs/" + pair.Key, "mirrorPeerProbeId",
                        "Mirror peers must exist, be reciprocal, use opposite handedness and share quantized lengths/reach.",
                        "Author one reciprocal left/right pair from the same imported skeleton measurement.");
            }

            var attachmentKeys = new HashSet<string>(StringComparer.Ordinal);
            var colliderIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; geometry.Colliders != null && i < geometry.Colliders.Count; i++)
                colliderIds.Add(geometry.Colliders[i]?.ColliderId ?? string.Empty);
            var socketIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; geometry.Sockets != null && i < geometry.Sockets.Count; i++)
                socketIds.Add(geometry.Sockets[i]?.SocketId ?? string.Empty);
            var contactIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; geometry.BodyPartContactPoints != null && i < geometry.BodyPartContactPoints.Count; i++)
                contactIds.Add(geometry.BodyPartContactPoints[i]?.StableId ?? string.Empty);
            for (int i = 0; i < attachments.Count; i++)
            {
                CharacterAuthorityPoseAttachmentAuthoring attachment = attachments[i];
                string stableId = attachment?.StableId ?? string.Empty;
                string objectPath = "authorityPoseRig/attachments/" +
                    (!string.IsNullOrWhiteSpace(stableId) ? stableId : i.ToString(CultureInfo.InvariantCulture));
                string key = (attachment?.Kind ?? CharacterAuthorityPoseAttachmentKind.Unknown) + ":" + stableId;
                bool targetExists = attachment != null && attachment.Kind switch
                {
                    CharacterAuthorityPoseAttachmentKind.Collider => colliderIds.Contains(stableId),
                    CharacterAuthorityPoseAttachmentKind.Socket => socketIds.Contains(stableId),
                    CharacterAuthorityPoseAttachmentKind.ContactPoint => contactIds.Contains(stableId),
                    _ => false
                };
                if (attachment == null || !IsAuthorityPoseReferenceId(stableId)
                    || !attachmentKeys.Add(key) || !channelsById.ContainsKey(attachment.ChannelId ?? string.Empty)
                    || !targetExists)
                    PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseInvalidAttachment,
                        objectPath, "kind/stableId/channelId",
                        "Attachment must uniquely map an existing collider, socket or contact point to an existing channel.",
                        "Use exact stable identities from the compiled geometry inputs.");
            }
        }

        private static void AddControlledChannel(
            CharacterAuthoringValidationReport report,
            HashSet<string> controlled,
            string channelId,
            string objectPath)
        {
            if (!string.IsNullOrWhiteSpace(channelId) && !controlled.Add(channelId))
                PoseIssue(report, CharacterAuthoringValidationCodes.AuthorityPoseChannelConflict,
                    objectPath, "rootChannelId/midChannelId/tipChannelId",
                    "A channel is controlled by more than one terrain-IK limb.",
                    "Give each limb an exclusive root/mid/tip chain.");
        }

        private static bool IsDescendantBonePath(string parent, string child) =>
            !string.IsNullOrWhiteSpace(parent)
            && !string.IsNullOrWhiteSpace(child)
            && child.StartsWith(parent + "/", StringComparison.Ordinal);

        private static bool IsAuthorityPoseStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-') return false;
            }
            return true;
        }

        private static bool IsAuthorityPoseReferenceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-') return false;
            }
            return true;
        }

        private static bool PositiveBounded(int value) =>
            value > 0 && value <= CharacterAuthorityPoseRigLimits.MaximumDistanceMillimeters;
        private static bool NonNegativeBounded(int value) =>
            value >= 0 && value <= CharacterAuthorityPoseRigLimits.MaximumDistanceMillimeters;
        private static bool BoundedSigned(int value) =>
            value >= -CharacterAuthorityPoseRigLimits.MaximumDistanceMillimeters
            && value <= CharacterAuthorityPoseRigLimits.MaximumDistanceMillimeters;
        private static bool BoundedVector(CharacterAuthoringIntVector3 value) =>
            value != null && BoundedSigned(value.X) && BoundedSigned(value.Y) && BoundedSigned(value.Z);
        private static bool IsSnormVector(CharacterAuthoringIntVector3 value) =>
            value != null
            && value.X >= -CharacterAuthorityPoseRigLimits.NormalQuantizationScale
            && value.X <= CharacterAuthorityPoseRigLimits.NormalQuantizationScale
            && value.Y >= -CharacterAuthorityPoseRigLimits.NormalQuantizationScale
            && value.Y <= CharacterAuthorityPoseRigLimits.NormalQuantizationScale
            && value.Z >= -CharacterAuthorityPoseRigLimits.NormalQuantizationScale
            && value.Z <= CharacterAuthorityPoseRigLimits.NormalQuantizationScale
            && (value.X != 0 || value.Y != 0 || value.Z != 0);
        private static bool ValidHysteresis(int value) =>
            value >= 0 && value <= CharacterAuthorityPoseRigLimits.MaximumHysteresisFrames;
        private static bool ValidStableList(List<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
                if (!IsAuthorityPoseStableId(values[i]) || !seen.Add(values[i])) return false;
            return true;
        }

        private static void PoseIssue(
            CharacterAuthoringValidationReport report,
            string code,
            string objectPath,
            string field,
            string message,
            string suggestedAction)
        {
            Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                code, AuthorityPoseRigSourcePath, objectPath, field, message, suggestedAction);
        }
    }
}
