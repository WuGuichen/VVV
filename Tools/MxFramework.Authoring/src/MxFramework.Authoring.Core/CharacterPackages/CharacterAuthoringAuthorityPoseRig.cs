using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public enum CharacterAuthorityPoseRigDocumentState
    {
        Missing = 0,
        Current = 1,
        Future = 2,
        Invalid = 3
    }

    public enum CharacterAuthorityPoseHandedness
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    public enum CharacterAuthorityPoseAttachmentKind
    {
        Unknown = 0,
        Collider = 1,
        Socket = 2,
        ContactPoint = 3
    }

    public static class CharacterAuthorityPoseRigLimits
    {
        public const int MaximumLimbCount = 4;
        public const int MaximumChannelCount = 12;
        public const int MaximumAttachmentCount = 32;
        public const int MaximumDistanceMillimeters = 100000;
        public const int NormalQuantizationScale = 32767;
        public const int SlopeCosineScale = 1000;
        public const int MaximumHysteresisFrames = 600;
    }

    public sealed class CharacterAuthorityPoseChannelAuthoring
    {
        [JsonRequired] public string ChannelId { get; set; } = string.Empty;
        [JsonRequired] public string BonePath { get; set; } = string.Empty;
        [JsonRequired] public string PartId { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthorityPoseLimbAuthoring
    {
        [JsonRequired] public string ProbeId { get; set; } = string.Empty;
        [JsonRequired] public string IkBindingId { get; set; } = string.Empty;
        public string PartStateId { get; set; } = string.Empty;
        [JsonRequired] public CharacterAuthorityPoseHandedness Handedness { get; set; }
        [JsonRequired] public string MirrorPeerProbeId { get; set; } = string.Empty;
        [JsonRequired] public string RootChannelId { get; set; } = string.Empty;
        [JsonRequired] public string MidChannelId { get; set; } = string.Empty;
        [JsonRequired] public string TipChannelId { get; set; } = string.Empty;
        [JsonRequired] public CharacterAuthoringIntVector3 TipProbeOriginMillimeters { get; set; } =
            new CharacterAuthoringIntVector3();
        [JsonRequired] public CharacterAuthoringIntVector3 TipProbeAxisSnorm { get; set; } =
            new CharacterAuthoringIntVector3(0, -CharacterAuthorityPoseRigLimits.NormalQuantizationScale, 0);
        [JsonRequired] public int MaxUpDistanceMillimeters { get; set; }
        [JsonRequired] public int MaxDownDistanceMillimeters { get; set; }
        [JsonRequired] public int RadiusMillimeters { get; set; }
        [JsonRequired] public int ClearanceMillimeters { get; set; }
        [JsonRequired] public int StaticLayerMask { get; set; }
        [JsonRequired] public int MinimumSlopeCosineMilli { get; set; }
        [JsonRequired] public CharacterAuthoringIntVector3 PoleDirectionSnorm { get; set; } =
            new CharacterAuthoringIntVector3(CharacterAuthorityPoseRigLimits.NormalQuantizationScale, 0, 0);
        [JsonRequired] public int RootToMidLengthMillimeters { get; set; }
        [JsonRequired] public int MidToTipLengthMillimeters { get; set; }
        [JsonRequired] public int MinimumReachMillimeters { get; set; }
        [JsonRequired] public int MaximumReachMillimeters { get; set; }
        [JsonRequired] public int WorldNormalTargetOffsetMillimeters { get; set; }
        [JsonRequired] public List<string> AllowedMotionIds { get; set; } = new List<string>();
        [JsonRequired] public List<string> AllowedPostureIds { get; set; } = new List<string>();
        [JsonRequired] public List<string> DisabledActionTags { get; set; } = new List<string>();
        [JsonRequired] public int EnterFrames { get; set; }
        [JsonRequired] public int ExitFrames { get; set; }
        [JsonRequired] public int LostFrames { get; set; }
    }

    public sealed class CharacterAuthorityPoseAttachmentAuthoring
    {
        [JsonRequired] public CharacterAuthorityPoseAttachmentKind Kind { get; set; }
        [JsonRequired] public string StableId { get; set; } = string.Empty;
        [JsonRequired] public string ChannelId { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthorityPoseRigDocument
    {
        public const string CurrentFormat = "mx.characterAuthorityPoseRig.v1";
        public const string CurrentSchemaVersion = "1.0";

        [JsonRequired] public string Format { get; set; } = CurrentFormat;
        [JsonRequired] public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        [JsonRequired] public int Revision { get; set; }
        [JsonRequired] public string CoordinateSpace { get; set; } = "LeftHandedModelRoot";
        [JsonRequired] public string DistanceUnit { get; set; } = "Millimeter";
        [JsonRequired] public int NormalQuantizationScale { get; set; } =
            CharacterAuthorityPoseRigLimits.NormalQuantizationScale;
        [JsonRequired] public List<CharacterAuthorityPoseChannelAuthoring> Channels { get; set; } =
            new List<CharacterAuthorityPoseChannelAuthoring>();
        [JsonRequired] public List<CharacterAuthorityPoseLimbAuthoring> Limbs { get; set; } =
            new List<CharacterAuthorityPoseLimbAuthoring>();
        [JsonRequired] public List<CharacterAuthorityPoseAttachmentAuthoring> Attachments { get; set; } =
            new List<CharacterAuthorityPoseAttachmentAuthoring>();
    }

    public sealed class CharacterAuthorityPoseRigLoadResult
    {
        public CharacterAuthorityPoseRigDocumentState State { get; set; }
        public CharacterAuthorityPoseRigDocument Document { get; set; }
        public string RawPayload { get; set; } = string.Empty;
        public bool WriteEligible => State == CharacterAuthorityPoseRigDocumentState.Current;
    }

    public static class CharacterAuthorityPoseRigDocumentCodec
    {
        public static CharacterAuthorityPoseRigLoadResult Missing() =>
            new CharacterAuthorityPoseRigLoadResult { State = CharacterAuthorityPoseRigDocumentState.Missing };

        public static CharacterAuthorityPoseRigLoadResult Read(
            string payload,
            JsonSerializerOptions options)
        {
            var result = new CharacterAuthorityPoseRigLoadResult
            {
                RawPayload = payload ?? string.Empty
            };
            try
            {
                using JsonDocument json = JsonDocument.Parse(result.RawPayload);
                if (json.RootElement.ValueKind != JsonValueKind.Object
                    || !TryReadString(json.RootElement, "format", out string format)
                    || !TryReadString(json.RootElement, "schemaVersion", out string schemaVersion))
                {
                    result.State = CharacterAuthorityPoseRigDocumentState.Invalid;
                    return result;
                }

                if (!string.Equals(format, CharacterAuthorityPoseRigDocument.CurrentFormat, StringComparison.Ordinal))
                {
                    result.State = format.StartsWith("mx.characterAuthorityPoseRig.", StringComparison.Ordinal)
                        ? CharacterAuthorityPoseRigDocumentState.Future
                        : CharacterAuthorityPoseRigDocumentState.Invalid;
                    return result;
                }

                if (!string.Equals(schemaVersion, CharacterAuthorityPoseRigDocument.CurrentSchemaVersion, StringComparison.Ordinal))
                {
                    result.State = CharacterAuthorityPoseRigDocumentState.Future;
                    return result;
                }

                result.Document = JsonSerializer.Deserialize<CharacterAuthorityPoseRigDocument>(
                    result.RawPayload,
                    new JsonSerializerOptions(options));
                result.State = result.Document == null
                    ? CharacterAuthorityPoseRigDocumentState.Invalid
                    : CharacterAuthorityPoseRigDocumentState.Current;
            }
            catch (JsonException)
            {
                result.State = CharacterAuthorityPoseRigDocumentState.Invalid;
            }
            return result;
        }

        public static CharacterAuthorityPoseRigDocument CreateCanonicalDocument(
            CharacterAuthorityPoseRigDocument source)
        {
            var result = new CharacterAuthorityPoseRigDocument
            {
                Format = source?.Format ?? CharacterAuthorityPoseRigDocument.CurrentFormat,
                SchemaVersion = source?.SchemaVersion ?? CharacterAuthorityPoseRigDocument.CurrentSchemaVersion,
                Revision = source?.Revision ?? 0,
                CoordinateSpace = source?.CoordinateSpace ?? string.Empty,
                DistanceUnit = source?.DistanceUnit ?? string.Empty,
                NormalQuantizationScale = source?.NormalQuantizationScale ?? 0
            };

            var channels = new List<CharacterAuthorityPoseChannelAuthoring>(
                source?.Channels ?? new List<CharacterAuthorityPoseChannelAuthoring>());
            channels.Sort((left, right) => string.CompareOrdinal(left?.ChannelId, right?.ChannelId));
            for (int i = 0; i < channels.Count; i++)
            {
                CharacterAuthorityPoseChannelAuthoring channel = channels[i];
                if (channel == null) continue;
                result.Channels.Add(new CharacterAuthorityPoseChannelAuthoring
                {
                    ChannelId = channel.ChannelId ?? string.Empty,
                    BonePath = channel.BonePath ?? string.Empty,
                    PartId = channel.PartId ?? string.Empty
                });
            }

            var limbs = new List<CharacterAuthorityPoseLimbAuthoring>(
                source?.Limbs ?? new List<CharacterAuthorityPoseLimbAuthoring>());
            limbs.Sort((left, right) => string.CompareOrdinal(left?.ProbeId, right?.ProbeId));
            for (int i = 0; i < limbs.Count; i++)
            {
                CharacterAuthorityPoseLimbAuthoring limb = limbs[i];
                if (limb == null) continue;
                result.Limbs.Add(new CharacterAuthorityPoseLimbAuthoring
                {
                    ProbeId = limb.ProbeId ?? string.Empty,
                    IkBindingId = limb.IkBindingId ?? string.Empty,
                    PartStateId = limb.PartStateId ?? string.Empty,
                    Handedness = limb.Handedness,
                    MirrorPeerProbeId = limb.MirrorPeerProbeId ?? string.Empty,
                    RootChannelId = limb.RootChannelId ?? string.Empty,
                    MidChannelId = limb.MidChannelId ?? string.Empty,
                    TipChannelId = limb.TipChannelId ?? string.Empty,
                    TipProbeOriginMillimeters = Copy(limb.TipProbeOriginMillimeters),
                    TipProbeAxisSnorm = Copy(limb.TipProbeAxisSnorm),
                    MaxUpDistanceMillimeters = limb.MaxUpDistanceMillimeters,
                    MaxDownDistanceMillimeters = limb.MaxDownDistanceMillimeters,
                    RadiusMillimeters = limb.RadiusMillimeters,
                    ClearanceMillimeters = limb.ClearanceMillimeters,
                    StaticLayerMask = limb.StaticLayerMask,
                    MinimumSlopeCosineMilli = limb.MinimumSlopeCosineMilli,
                    PoleDirectionSnorm = Copy(limb.PoleDirectionSnorm),
                    RootToMidLengthMillimeters = limb.RootToMidLengthMillimeters,
                    MidToTipLengthMillimeters = limb.MidToTipLengthMillimeters,
                    MinimumReachMillimeters = limb.MinimumReachMillimeters,
                    MaximumReachMillimeters = limb.MaximumReachMillimeters,
                    WorldNormalTargetOffsetMillimeters = limb.WorldNormalTargetOffsetMillimeters,
                    AllowedMotionIds = Sort(limb.AllowedMotionIds),
                    AllowedPostureIds = Sort(limb.AllowedPostureIds),
                    DisabledActionTags = Sort(limb.DisabledActionTags),
                    EnterFrames = limb.EnterFrames,
                    ExitFrames = limb.ExitFrames,
                    LostFrames = limb.LostFrames
                });
            }

            var attachments = new List<CharacterAuthorityPoseAttachmentAuthoring>(
                source?.Attachments ?? new List<CharacterAuthorityPoseAttachmentAuthoring>());
            attachments.Sort((left, right) =>
            {
                int kind = (left?.Kind ?? CharacterAuthorityPoseAttachmentKind.Unknown)
                    .CompareTo(right?.Kind ?? CharacterAuthorityPoseAttachmentKind.Unknown);
                return kind != 0 ? kind : string.CompareOrdinal(left?.StableId, right?.StableId);
            });
            for (int i = 0; i < attachments.Count; i++)
            {
                CharacterAuthorityPoseAttachmentAuthoring attachment = attachments[i];
                if (attachment == null) continue;
                result.Attachments.Add(new CharacterAuthorityPoseAttachmentAuthoring
                {
                    Kind = attachment.Kind,
                    StableId = attachment.StableId ?? string.Empty,
                    ChannelId = attachment.ChannelId ?? string.Empty
                });
            }
            return result;
        }

        public static string SerializeCanonical(CharacterAuthorityPoseRigDocument source)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Serialize(CreateCanonicalDocument(source), options);
        }

        public static string ComputeContentHash(CharacterAuthorityPoseRigDocument source) =>
            CharacterPackageHashUtility.ComputeTextSha256(SerializeCanonical(source));

        private static CharacterAuthoringIntVector3 Copy(CharacterAuthoringIntVector3 value) =>
            value == null ? null : new CharacterAuthoringIntVector3(value.X, value.Y, value.Z);

        private static List<string> Sort(List<string> values)
        {
            var result = new List<string>(values ?? new List<string>());
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static bool TryReadString(JsonElement root, string name, out string value)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                    || property.Value.ValueKind != JsonValueKind.String)
                    continue;
                value = property.Value.GetString() ?? string.Empty;
                return true;
            }
            value = string.Empty;
            return false;
        }
    }

    public sealed partial class CharacterAuthoringGeometry
    {
        public CharacterAuthorityPoseRigDocument AuthorityPoseRig { get; set; }
        public CharacterAuthorityPoseRigDocumentState AuthorityPoseRigDocumentState { get; set; } =
            CharacterAuthorityPoseRigDocumentState.Missing;
        public string AuthorityPoseRigRawPayload { get; set; } = string.Empty;
        public bool AuthorityPoseRigWriteEligible =>
            AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Current;
    }
}
