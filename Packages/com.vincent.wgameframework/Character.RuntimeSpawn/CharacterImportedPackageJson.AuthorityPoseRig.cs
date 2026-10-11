using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static partial class CharacterImportedPackageJson
    {
        private static CharacterAuthorityPoseRigRuntimeBinding ParseAuthorityPoseRig(
            JObject root,
            CharacterBodyPartDamageRuntimeBinding[] partStates,
            CharacterBodyPartDamageLookupRuntimeBinding[] partLookup)
        {
            JObject rig = root?["authorityPoseRig"] as JObject;
            if (rig == null)
                throw new CharacterImportedPackageJsonException("Geometry binding v6 requires authorityPoseRig.");
            string format = ReadString(rig, "format");
            string schemaVersion = ReadString(rig, "schemaVersion");
            int revision = ReadInt(rig, "revision");
            string contentHash = ReadString(rig, "contentHash");
            string coordinateSpace = ReadString(rig, "coordinateSpace");
            string distanceUnit = ReadString(rig, "distanceUnit");
            int normalScale = ReadInt(rig, "normalQuantizationScale");
            if (!string.Equals(format, "mx.characterAuthorityPoseRig.v2", StringComparison.Ordinal)
                || !string.Equals(schemaVersion, "2.0", StringComparison.Ordinal)
                || revision < 1
                || !IsSha256(contentHash)
                || !string.Equals(coordinateSpace, "LeftHandedModelRoot", StringComparison.Ordinal)
                || !string.Equals(distanceUnit, "Millimeter", StringComparison.Ordinal)
                || normalScale != 32767)
                throw new CharacterImportedPackageJsonException("Authority-pose rig header is invalid.");

            CharacterAuthorityPoseChannelRuntimeBinding[] channels =
                ParseArray(rig["channels"] as JArray, ParseAuthorityPoseChannel);
            CharacterAuthorityPoseLimbRuntimeBinding[] limbs =
                ParseArray(rig["limbs"] as JArray, ParseAuthorityPoseLimb);
            CharacterAuthorityPoseAttachmentRuntimeBinding[] attachments =
                ParseArray(rig["attachments"] as JArray, ParseAuthorityPoseAttachment);
            if (channels.Length == 0 || channels.Length > CharacterAuthorityPoseRigRuntimeBinding.MaximumChannelCount
                || limbs.Length == 0 || limbs.Length > CharacterAuthorityPoseRigRuntimeBinding.MaximumLimbCount
                || attachments.Length > CharacterAuthorityPoseRigRuntimeBinding.MaximumAttachmentCount)
                throw new CharacterImportedPackageJsonException("Authority-pose rig capacity is invalid.");

            var channelsById = new Dictionary<int, CharacterAuthorityPoseChannelRuntimeBinding>();
            int previousChannelId = 0;
            for (int i = 0; i < channels.Length; i++)
            {
                CharacterAuthorityPoseChannelRuntimeBinding channel = channels[i];
                if (channel == null || channel.Ordinal != i || !IsRuntimeStableId(channel.ChannelId)
                    || string.IsNullOrWhiteSpace(channel.BonePath) || channel.BonePath.Contains("\\")
                    || channel.BonePath.Contains("..") || !IsRuntimeStableId(channel.PartId)
                    || !channelsById.TryAdd(channel.ChannelId, channel))
                    throw new CharacterImportedPackageJsonException("Authority-pose channel identity or ordinal is invalid.");
                previousChannelId = channel.ChannelId;
            }

            var limbsByProbe = new Dictionary<int, CharacterAuthorityPoseLimbRuntimeBinding>();
            var ikIds = new HashSet<int>();
            var controlledChannels = new HashSet<int>();
            int previousProbeId = 0;
            for (int i = 0; i < limbs.Length; i++)
            {
                CharacterAuthorityPoseLimbRuntimeBinding limb = limbs[i];
                if (limb == null || limb.Ordinal != i || !IsRuntimeStableId(limb.ProbeId)
                    || !IsRuntimeStableId(limb.IkBindingId) || !limbsByProbe.TryAdd(limb.ProbeId, limb)
                    || !ikIds.Add(limb.IkBindingId)
                    || !channelsById.TryGetValue(limb.RootChannelId, out CharacterAuthorityPoseChannelRuntimeBinding rootChannel)
                    || !channelsById.TryGetValue(limb.MidChannelId, out CharacterAuthorityPoseChannelRuntimeBinding midChannel)
                    || !channelsById.TryGetValue(limb.TipChannelId, out CharacterAuthorityPoseChannelRuntimeBinding tipChannel)
                    || !controlledChannels.Add(limb.RootChannelId)
                    || !controlledChannels.Add(limb.MidChannelId)
                    || !controlledChannels.Add(limb.TipChannelId)
                    || !midChannel.BonePath.StartsWith(rootChannel.BonePath + "/", StringComparison.Ordinal)
                    || !tipChannel.BonePath.StartsWith(midChannel.BonePath + "/", StringComparison.Ordinal)
                    || limb.Handedness == CharacterAuthorityPoseRigHandedness.None
                    || !ValidReach(limb)
                    || !BoundedVector(limb.TipProbeOriginMillimeters)
                    || !Snorm(limb.TipProbeAxisSnorm) || !Snorm(limb.PoleDirectionSnorm)
                    || limb.MaxUpDistanceMillimeters < 0 || limb.MaxUpDistanceMillimeters > 100000
                    || limb.MaxDownDistanceMillimeters <= 0 || limb.MaxDownDistanceMillimeters > 100000
                    || limb.RadiusMillimeters <= 0 || limb.RadiusMillimeters > 100000
                    || limb.ClearanceMillimeters < 0 || limb.ClearanceMillimeters > 100000
                    || limb.StaticLayerMask <= 0
                    || limb.MinimumSlopeCosineMilli < 0 || limb.MinimumSlopeCosineMilli > 1000
                    || limb.WorldNormalTargetOffsetMillimeters < -100000
                    || limb.WorldNormalTargetOffsetMillimeters > 100000
                    || !StableSorted(limb.AllowedMotionIds)
                    || !StableSorted(limb.AllowedPostureIds)
                    || !StableSorted(limb.DisabledActionTags)
                    || limb.EnterFrames < 0 || limb.EnterFrames > 600
                    || limb.ExitFrames < 0 || limb.ExitFrames > 600
                    || limb.LostFrames < 0 || limb.LostFrames > 600)
                    throw new CharacterImportedPackageJsonException("Authority-pose limb binding is invalid.");

                if ((limb.PartStateId > 0)
                    && !ContainsPartState(partStates, limb.PartStateId))
                    throw new CharacterImportedPackageJsonException("Authority-pose limb references an unknown partStateId.");
                if ((limb.PartStateId <= 0)
                    && (IsSeverablePart(rootChannel.PartId, partStates, partLookup)
                        || IsSeverablePart(midChannel.PartId, partStates, partLookup)
                        || IsSeverablePart(tipChannel.PartId, partStates, partLookup)))
                    throw new CharacterImportedPackageJsonException("Authority-pose limb is missing its Severed state mapping.");
                previousProbeId = limb.ProbeId;
            }

            foreach (KeyValuePair<int, CharacterAuthorityPoseLimbRuntimeBinding> pair in limbsByProbe)
            {
                CharacterAuthorityPoseLimbRuntimeBinding limb = pair.Value;
                if (!limbsByProbe.TryGetValue(limb.MirrorPeerProbeId, out CharacterAuthorityPoseLimbRuntimeBinding peer)
                    || !(peer.MirrorPeerProbeId == limb.ProbeId)
                    || peer.Handedness == limb.Handedness
                    || peer.RootToMidLengthMillimeters != limb.RootToMidLengthMillimeters
                    || peer.MidToTipLengthMillimeters != limb.MidToTipLengthMillimeters
                    || peer.MinimumReachMillimeters != limb.MinimumReachMillimeters
                    || peer.MaximumReachMillimeters != limb.MaximumReachMillimeters)
                    throw new CharacterImportedPackageJsonException("Authority-pose mirror mapping is invalid.");
            }

            var attachmentIds = new HashSet<long>();
            CharacterAuthorityPoseRigAttachmentKind previousAttachmentKind = CharacterAuthorityPoseRigAttachmentKind.Unknown;
            int previousAttachmentId = 0;
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterAuthorityPoseAttachmentRuntimeBinding attachment = attachments[i];
                long key = attachment == null ? 0L : ((long)attachment.Kind << 32) | (uint)attachment.StableId;
                if (attachment == null || attachment.Ordinal != i
                    || attachment.Kind == CharacterAuthorityPoseRigAttachmentKind.Unknown
                    || attachment.StableId <= 0
                    || (i > 0 && attachment.Kind < previousAttachmentKind)
                    || !channelsById.ContainsKey(attachment.ChannelId)
                    || !attachmentIds.Add(key))
                    throw new CharacterImportedPackageJsonException("Authority-pose attachment binding is invalid.");
                previousAttachmentKind = attachment.Kind;
                previousAttachmentId = attachment.StableId;
            }

            var result = new CharacterAuthorityPoseRigRuntimeBinding(
                format,
                schemaVersion,
                revision,
                contentHash,
                coordinateSpace,
                distanceUnit,
                normalScale,
                channels,
                limbs,
                attachments);
            if (!string.Equals(contentHash, ComputeAuthorityPoseRigContentHash(result), StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException(
                    "Authority-pose rig content hash does not match the canonical binding.");
            return result;
        }

        private static CharacterAuthorityPoseChannelRuntimeBinding ParseAuthorityPoseChannel(JObject value) =>
            new CharacterAuthorityPoseChannelRuntimeBinding(
                ReadInt(value, "ordinal", -1),
                ReadNumericIdentity(value, "channelId"),
                ReadString(value, "bonePath"),
                ReadNumericIdentity(value, "partId"),
                channelLabel: ReadString(value, "channelLabel"),
                partLabel: ReadString(value, "partLabel"));

        private static CharacterAuthorityPoseLimbRuntimeBinding ParseAuthorityPoseLimb(JObject value) =>
            new CharacterAuthorityPoseLimbRuntimeBinding(
                ReadInt(value, "ordinal", -1),
                ReadNumericIdentity(value, "probeId"),
                ReadNumericIdentity(value, "ikBindingId"),
                ReadNumericIdentity(value, "partStateId"),
                ParseHandedness(ReadString(value, "handedness")),
                ReadNumericIdentity(value, "mirrorPeerProbeId"),
                ReadNumericIdentity(value, "rootChannelId"),
                ReadNumericIdentity(value, "midChannelId"),
                ReadNumericIdentity(value, "tipChannelId"),
                ReadQuantizedVector(value["tipProbeOriginMillimeters"] as JObject),
                ReadQuantizedVector(value["tipProbeAxisSnorm"] as JObject),
                ReadInt(value, "maxUpDistanceMillimeters"),
                ReadInt(value, "maxDownDistanceMillimeters"),
                ReadInt(value, "radiusMillimeters"),
                ReadInt(value, "clearanceMillimeters"),
                ReadInt(value, "staticLayerMask"),
                ReadInt(value, "minimumSlopeCosineMilli"),
                ReadQuantizedVector(value["poleDirectionSnorm"] as JObject),
                ReadInt(value, "rootToMidLengthMillimeters"),
                ReadInt(value, "midToTipLengthMillimeters"),
                ReadInt(value, "minimumReachMillimeters"),
                ReadInt(value, "maximumReachMillimeters"),
                ReadInt(value, "worldNormalTargetOffsetMillimeters"),
                ReadAuthorityPoseStringArray(value["allowedMotionIds"] as JArray),
                ReadAuthorityPoseIntegerArray(value["allowedPostureIds"] as JArray),
                ReadAuthorityPoseStringArray(value["disabledActionTags"] as JArray),
                ReadInt(value, "enterFrames"),
                ReadInt(value, "exitFrames"),
                ReadInt(value, "lostFrames"),
                probeLabel: ReadString(value, "probeLabel"),
                ikBindingLabel: ReadString(value, "ikBindingLabel"),
                partStateLabel: ReadString(value, "partStateLabel"),
                mirrorPeerProbeLabel: ReadString(value, "mirrorPeerProbeLabel"),
                rootChannelLabel: ReadString(value, "rootChannelLabel"),
                midChannelLabel: ReadString(value, "midChannelLabel"),
                tipChannelLabel: ReadString(value, "tipChannelLabel"),
                allowedPostureLabels: ReadAuthorityPoseStringArray(value["allowedPostureLabels"] as JArray));

        private static CharacterAuthorityPoseAttachmentRuntimeBinding ParseAuthorityPoseAttachment(JObject value) =>
            new CharacterAuthorityPoseAttachmentRuntimeBinding(
                ReadInt(value, "ordinal", -1),
                ParseAttachmentKind(ReadString(value, "kind")),
                ReadNumericIdentity(value, "stableId"),
                ReadNumericIdentity(value, "channelId"),
                ReadString(value, "channelLabel"),
                ReadString(value, "stableLabel"));

        private static CharacterAuthorityPoseQuantizedVector3 ReadQuantizedVector(JObject value) =>
            new CharacterAuthorityPoseQuantizedVector3(
                ReadInt(value, "x"),
                ReadInt(value, "y"),
                ReadInt(value, "z"));

        private static int[] ReadAuthorityPoseIntegerArray(JArray values)
        {
            if (values == null) return Array.Empty<int>();
            var result = new int[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Type != JTokenType.Integer)
                    throw new ArgumentException("Authority pose identity arrays require positive integer tokens.");
                long value = values[i].Value<long>();
                if (value <= 0 || value > int.MaxValue)
                    throw new ArgumentException("Authority pose identity is outside the supported integer range.");
                result[i] = (int)value;
            }
            return result;
        }

        private static string[] ReadAuthorityPoseStringArray(JArray values)
        {
            if (values == null) return Array.Empty<string>();
            var result = new string[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = (string)values[i] ?? string.Empty;
            return result;
        }

        private static CharacterAuthorityPoseRigHandedness ParseHandedness(string value) => value switch
        {
            "Left" => CharacterAuthorityPoseRigHandedness.Left,
            "Right" => CharacterAuthorityPoseRigHandedness.Right,
            _ => CharacterAuthorityPoseRigHandedness.None
        };

        private static CharacterAuthorityPoseRigAttachmentKind ParseAttachmentKind(string value) => value switch
        {
            "Collider" => CharacterAuthorityPoseRigAttachmentKind.Collider,
            "Socket" => CharacterAuthorityPoseRigAttachmentKind.Socket,
            "ContactPoint" => CharacterAuthorityPoseRigAttachmentKind.ContactPoint,
            _ => CharacterAuthorityPoseRigAttachmentKind.Unknown
        };

        private static bool IsRuntimeStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-') return false;
            }
            return true;
        }

        private static bool IsRuntimeReferenceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-') return false;
            }
            return true;
        }

        private static bool BoundedVector(CharacterAuthorityPoseQuantizedVector3 value) =>
            value.X >= -100000 && value.X <= 100000
            && value.Y >= -100000 && value.Y <= 100000
            && value.Z >= -100000 && value.Z <= 100000;

        private static bool Snorm(CharacterAuthorityPoseQuantizedVector3 value) =>
            value.X >= -32767 && value.X <= 32767
            && value.Y >= -32767 && value.Y <= 32767
            && value.Z >= -32767 && value.Z <= 32767
            && (value.X != 0 || value.Y != 0 || value.Z != 0);

        private static bool ValidReach(CharacterAuthorityPoseLimbRuntimeBinding limb)
        {
            if (limb.RootToMidLengthMillimeters <= 0 || limb.RootToMidLengthMillimeters > 100000
                || limb.MidToTipLengthMillimeters <= 0 || limb.MidToTipLengthMillimeters > 100000)
                return false;
            long minimum = Math.Abs(
                (long)limb.RootToMidLengthMillimeters - limb.MidToTipLengthMillimeters);
            long maximum = (long)limb.RootToMidLengthMillimeters + limb.MidToTipLengthMillimeters;
            return limb.MinimumReachMillimeters >= minimum
                && limb.MinimumReachMillimeters <= limb.MaximumReachMillimeters
                && limb.MaximumReachMillimeters <= maximum;
        }

        private static bool StableSorted(int[] values)
        {
            int previous = 0;
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (values[i] <= 0 || (i > 0 && values[i] <= previous)) return false;
                previous = values[i];
            }
            return true;
        }

        private static bool StableSorted(string[] values)
        {
            string previous = string.Empty;
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!IsRuntimeStableId(values[i])
                    || (i > 0 && string.CompareOrdinal(previous, values[i]) >= 0)) return false;
                previous = values[i];
            }
            return true;
        }

        private static string ComputeAuthorityPoseRigContentHash(CharacterAuthorityPoseRigRuntimeBinding rig)
        {
            var channels = new JArray();
            for (int i = 0; i < rig.Channels.Length; i++)
            {
                CharacterAuthorityPoseChannelRuntimeBinding channel = rig.Channels[i];
                channels.Add(new JObject
                {
                    ["channelId"] = channel.ChannelLabel,
                    ["bonePath"] = channel.BonePath,
                    ["partId"] = channel.PartLabel
                });
            }
            var limbs = new JArray();
            for (int i = 0; i < rig.Limbs.Length; i++)
            {
                CharacterAuthorityPoseLimbRuntimeBinding limb = rig.Limbs[i];
                limbs.Add(new JObject
                {
                    ["probeId"] = limb.ProbeLabel,
                    ["ikBindingId"] = limb.IkBindingLabel,
                    ["partStateId"] = limb.PartStateLabel,
                    ["handedness"] = limb.Handedness.ToString(),
                    ["mirrorPeerProbeId"] = limb.MirrorPeerProbeLabel,
                    ["rootChannelId"] = limb.RootChannelLabel,
                    ["midChannelId"] = limb.MidChannelLabel,
                    ["tipChannelId"] = limb.TipChannelLabel,
                    ["tipProbeOriginMillimeters"] = QuantizedVector(limb.TipProbeOriginMillimeters),
                    ["tipProbeAxisSnorm"] = QuantizedVector(limb.TipProbeAxisSnorm),
                    ["maxUpDistanceMillimeters"] = limb.MaxUpDistanceMillimeters,
                    ["maxDownDistanceMillimeters"] = limb.MaxDownDistanceMillimeters,
                    ["radiusMillimeters"] = limb.RadiusMillimeters,
                    ["clearanceMillimeters"] = limb.ClearanceMillimeters,
                    ["staticLayerMask"] = limb.StaticLayerMask,
                    ["minimumSlopeCosineMilli"] = limb.MinimumSlopeCosineMilli,
                    ["poleDirectionSnorm"] = QuantizedVector(limb.PoleDirectionSnorm),
                    ["rootToMidLengthMillimeters"] = limb.RootToMidLengthMillimeters,
                    ["midToTipLengthMillimeters"] = limb.MidToTipLengthMillimeters,
                    ["minimumReachMillimeters"] = limb.MinimumReachMillimeters,
                    ["maximumReachMillimeters"] = limb.MaximumReachMillimeters,
                    ["worldNormalTargetOffsetMillimeters"] = limb.WorldNormalTargetOffsetMillimeters,
                    ["allowedMotionIds"] = StringArray(limb.AllowedMotionIds),
                    ["allowedPostureIds"] = StringArray(limb.AllowedPostureLabels),
                    ["disabledActionTags"] = StringArray(limb.DisabledActionTags),
                    ["enterFrames"] = limb.EnterFrames,
                    ["exitFrames"] = limb.ExitFrames,
                    ["lostFrames"] = limb.LostFrames
                });
            }
            var attachments = new JArray();
            for (int i = 0; i < rig.Attachments.Length; i++)
            {
                CharacterAuthorityPoseAttachmentRuntimeBinding attachment = rig.Attachments[i];
                attachments.Add(new JObject
                {
                    ["kind"] = attachment.Kind.ToString(),
                    ["stableId"] = attachment.StableLabel,
                    ["channelId"] = attachment.ChannelLabel
                });
            }
            var canonical = new JObject
            {
                ["format"] = "mx.characterAuthorityPoseRig.v1",
                ["schemaVersion"] = "1.0",
                ["revision"] = rig.Revision,
                ["coordinateSpace"] = rig.CoordinateSpace,
                ["distanceUnit"] = rig.DistanceUnit,
                ["normalQuantizationScale"] = rig.NormalQuantizationScale,
                ["channels"] = channels,
                ["limbs"] = limbs,
                ["attachments"] = attachments
            };
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString(Formatting.None)));
                var result = new StringBuilder(hash.Length * 2 + 7).Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static JObject QuantizedVector(CharacterAuthorityPoseQuantizedVector3 value) =>
            new JObject { ["x"] = value.X, ["y"] = value.Y, ["z"] = value.Z };

        private static JArray StringArray(string[] values)
        {
            var result = new JArray();
            for (int i = 0; values != null && i < values.Length; i++) result.Add(values[i]);
            return result;
        }

        private static bool ContainsPartState(CharacterBodyPartDamageRuntimeBinding[] states, int partStateId)
        {
            for (int i = 0; states != null && i < states.Length; i++)
                if ((states[i]?.PartStateId == partStateId)) return true;
            return false;
        }

        private static bool IsSeverablePart(
            int partId,
            CharacterBodyPartDamageRuntimeBinding[] states,
            CharacterBodyPartDamageLookupRuntimeBinding[] lookup)
        {
            for (int i = 0; lookup != null && i < lookup.Length; i++)
            {
                CharacterBodyPartDamageLookupRuntimeBinding entry = lookup[i];
                if (!(entry?.PartId == partId)) continue;
                int index = entry.DefinitionIndex;
                return index >= 0 && index < (states?.Length ?? 0) && states[index]?.Severable == true;
            }
            return false;
        }
    }
}
