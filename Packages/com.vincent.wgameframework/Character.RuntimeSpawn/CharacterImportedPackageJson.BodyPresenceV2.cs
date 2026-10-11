using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static partial class CharacterImportedPackageJson
    {
        private static CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[] ParseBodyPresenceV2AnimatedSources(
            JObject root,
            CharacterBodyColliderRuntimeBinding[] colliders,
            int revision,
            string contentHash,
            CharacterBodyPresenceRuntimeBinding[] volumes)
        {
            JArray source = ReadRequiredArray(root, "animatedHurtboxSources");
            if (source.Count > 4)
                throw BodyPresenceInvalid("animated Hurtbox source capacity is invalid.");
            var collidersById = new Dictionary<int, CharacterBodyColliderRuntimeBinding>();
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null && !collidersById.ContainsKey(colliders[i].ColliderId))
                    collidersById.Add(colliders[i].ColliderId, colliders[i]);
            var result = new CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[source.Count];
            var colliderIds = new HashSet<int>();
            string previousSourceId = string.Empty;
            for (int i = 0; i < source.Count; i++)
            {
                if (!(source[i] is JObject item))
                    throw BodyPresenceInvalid("every animated Hurtbox source must be an object.");
                string sourceId = ReadRequiredString(item, "sourceId");
                int colliderId = ReadNumericIdentity(item, "colliderId");
                int skin = ReadRequiredInt(item, "skinMillimeters");
                int pressure = ReadRequiredInt(item, "pressureScaleMilli");
                bool enabled = ReadRequiredBool(item, "enabled");
                if (!IsStableBodyPresenceId(sourceId)
                    || (i > 0 && string.CompareOrdinal(previousSourceId, sourceId) >= 0)
                    || !colliderIds.Add(colliderId)
                    || !collidersById.TryGetValue(colliderId, out CharacterBodyColliderRuntimeBinding collider)
                    || !string.Equals(collider.PhysicsLayer, "CharacterHurtbox", StringComparison.Ordinal)
                    || (collider.PartId <= 0)
                    || (collider.Shape != "Sphere" && collider.Shape != "Capsule" && collider.Shape != "Box")
                    || skin < 0 || skin > 10000 || pressure < 0 || pressure > 100000)
                    throw BodyPresenceInvalid("animated Hurtbox source identity, collider, shape, PartId, or policy is invalid.");
                previousSourceId = sourceId;
                result[i] = new CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding(
                    sourceId, colliderId, skin, pressure, enabled, ReadString(item, "colliderLabel"));
            }
            string computedHash = ComputeBodyPresenceV2ContentHash(revision, volumes, result);
            if (!string.Equals(contentHash, computedHash, StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException(
                    "CHARPRES102_CONTENT_HASH_MISMATCH: Body Presence v2 content hash does not match the canonical binding.");
            return result;
        }

        private static string ComputeBodyPresenceV2ContentHash(
            int revision,
            CharacterBodyPresenceRuntimeBinding[] volumes,
            CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[] sources)
        {
            var builder = new StringBuilder();
            builder.Append("{\"format\":\"mx.characterBodyPresence.v2\",\"schemaVersion\":\"2.0\",\"revision\":")
                .Append(revision).Append(",\"volumes\":[");
            for (int i = 0; i < volumes.Length; i++)
            {
                CharacterBodyPresenceRuntimeBinding volume = volumes[i];
                if (i > 0) builder.Append(',');
                builder.Append("{\"volumeId\":\"").Append(volume.VolumeId)
                    .Append("\",\"referenceFrame\":\"").Append(volume.ReferenceFrame)
                    .Append("\",\"shape\":\"").Append(volume.Shape)
                    .Append("\",\"localPose\":{\"position\":");
                AppendCanonicalPresenceVector(builder, volume.LocalPose.Position);
                builder.Append(",\"rotation\":");
                AppendCanonicalPresenceQuaternion(builder, volume.LocalPose.Rotation);
                builder.Append(",\"scale\":");
                AppendCanonicalPresenceVector(builder, volume.LocalPose.Scale);
                builder.Append("},\"radius\":"); AppendCanonicalPresenceFloat(builder, volume.Radius);
                builder.Append(",\"height\":"); AppendCanonicalPresenceFloat(builder, volume.Height);
                builder.Append(",\"size\":"); AppendCanonicalPresenceVector(builder, volume.Size);
                builder.Append(",\"skinMillimeters\":").Append(volume.SkinMillimeters)
                    .Append(",\"pressureScaleMilli\":").Append(volume.PressureScaleMilli)
                    .Append(",\"enabled\":").Append(volume.Enabled ? "true" : "false").Append('}');
            }
            builder.Append("],\"animatedHurtboxSources\":[");
            for (int i = 0; i < sources.Length; i++)
            {
                if (i > 0) builder.Append(',');
                CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding source = sources[i];
                builder.Append("{\"sourceId\":\"").Append(source.SourceId)
                    .Append("\",\"colliderId\":\"").Append(source.ColliderLabel)
                    .Append("\",\"skinMillimeters\":").Append(source.SkinMillimeters)
                    .Append(",\"pressureScaleMilli\":").Append(source.PressureScaleMilli)
                    .Append(",\"enabled\":").Append(source.Enabled ? "true" : "false").Append('}');
            }
            builder.Append("]}");
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(71).Append("sha256:");
                for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
