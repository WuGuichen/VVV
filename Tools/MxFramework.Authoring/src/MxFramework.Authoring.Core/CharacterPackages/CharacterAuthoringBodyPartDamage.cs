using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public enum CharacterBodyPartDamageDocumentState
    {
        Missing = 0,
        Current = 1,
        Future = 2,
        Invalid = 3
    }

    public static class CharacterBodyPartDamageLimits
    {
        public const int MaximumDefinitionCount = 8;
        public const int MaximumAxisCapacity = 1000000;
        public const int MaximumScaleMilli = 4000;
        public const int MinimumPriorityDelta = -1000;
        public const int MaximumPriorityDelta = 1000;
    }

    /// <summary>Low-frequency authoring definition for one stable group of body-part identities.</summary>
    public sealed class CharacterBodyPartDamageAuthoring
    {
        [JsonRequired]
        public string PartStateId { get; set; } = string.Empty;

        [JsonRequired]
        public List<string> MemberPartIds { get; set; } = new List<string>();

        public string DamageZoneId { get; set; } = string.Empty;

        [JsonRequired]
        public int WoundCapacity { get; set; }

        [JsonRequired]
        public int OpenDamageScaleMilli { get; set; }

        public string FocusConsumeTag { get; set; } = string.Empty;

        [JsonRequired]
        public int MaxIntegrity { get; set; }

        [JsonRequired]
        public int IntegrityDamageScaleMilli { get; set; }

        public string BreakReactionId { get; set; } = string.Empty;

        [JsonRequired]
        public int AuthoredPriorityDeltaOpen { get; set; }

        [JsonRequired]
        public int AuthoredPriorityDeltaBroken { get; set; }

        /// <summary>
        /// Enables the fixed Broken-at-frame-start plus explicit-tag sever policy.
        /// Missing legacy fields remain false and retain the M2 state model.
        /// </summary>
        public bool Severable { get; set; }

        public string SeverRequiredTag { get; set; } = string.Empty;

        public string PresentationEventId { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPartDamageDocument
    {
        public const string CurrentFormat = "mx.characterBodyPartDamage.v1";
        public const string CurrentSchemaVersion = "1.0";

        [JsonRequired]
        public string Format { get; set; } = CurrentFormat;

        [JsonRequired]
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonRequired]
        public int Revision { get; set; }

        [JsonRequired]
        public List<CharacterBodyPartDamageAuthoring> PartStates { get; set; } =
            new List<CharacterBodyPartDamageAuthoring>();
    }

    public sealed class CharacterBodyPartDamageLoadResult
    {
        public CharacterBodyPartDamageDocumentState State { get; set; }
        public CharacterBodyPartDamageDocument Document { get; set; }
        public string RawPayload { get; set; } = string.Empty;
        public bool WriteEligible => State == CharacterBodyPartDamageDocumentState.Current;
    }

    /// <summary>LowFreqAlloc codec for the optional body-part-damage authoring document.</summary>
    public static class CharacterBodyPartDamageDocumentCodec
    {
        public static CharacterBodyPartDamageLoadResult Missing()
        {
            return new CharacterBodyPartDamageLoadResult
            {
                State = CharacterBodyPartDamageDocumentState.Missing
            };
        }

        public static CharacterBodyPartDamageLoadResult Read(
            string payload,
            JsonSerializerOptions options)
        {
            var result = new CharacterBodyPartDamageLoadResult
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
                    result.State = CharacterBodyPartDamageDocumentState.Invalid;
                    return result;
                }

                if (!string.Equals(
                        format,
                        CharacterBodyPartDamageDocument.CurrentFormat,
                        StringComparison.Ordinal))
                {
                    result.State = format.StartsWith(
                            "mx.characterBodyPartDamage.v",
                            StringComparison.Ordinal)
                        ? CharacterBodyPartDamageDocumentState.Future
                        : CharacterBodyPartDamageDocumentState.Invalid;
                    return result;
                }

                if (!string.Equals(
                        schemaVersion,
                        CharacterBodyPartDamageDocument.CurrentSchemaVersion,
                        StringComparison.Ordinal))
                {
                    result.State = CharacterBodyPartDamageDocumentState.Future;
                    return result;
                }

                result.Document = JsonSerializer.Deserialize<CharacterBodyPartDamageDocument>(
                    result.RawPayload,
                    options);
                result.State = result.Document == null
                    ? CharacterBodyPartDamageDocumentState.Invalid
                    : CharacterBodyPartDamageDocumentState.Current;
            }
            catch (JsonException)
            {
                result.State = CharacterBodyPartDamageDocumentState.Invalid;
            }

            return result;
        }

        public static CharacterBodyPartDamageDocument CreateCanonicalDocument(
            CharacterBodyPartDamageDocument source)
        {
            var result = new CharacterBodyPartDamageDocument
            {
                Format = source?.Format ?? CharacterBodyPartDamageDocument.CurrentFormat,
                SchemaVersion = source?.SchemaVersion ?? CharacterBodyPartDamageDocument.CurrentSchemaVersion,
                Revision = source?.Revision ?? 0
            };
            var definitions = new List<CharacterBodyPartDamageAuthoring>(
                source?.PartStates ?? new List<CharacterBodyPartDamageAuthoring>());
            definitions.Sort((left, right) =>
                string.CompareOrdinal(left?.PartStateId, right?.PartStateId));
            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterBodyPartDamageAuthoring definition = definitions[i];
                if (definition == null)
                    continue;
                var memberPartIds = new List<string>(
                    definition.MemberPartIds ?? new List<string>());
                memberPartIds.Sort(StringComparer.Ordinal);
                result.PartStates.Add(new CharacterBodyPartDamageAuthoring
                {
                    PartStateId = definition.PartStateId ?? string.Empty,
                    MemberPartIds = memberPartIds,
                    DamageZoneId = definition.DamageZoneId ?? string.Empty,
                    WoundCapacity = definition.WoundCapacity,
                    OpenDamageScaleMilli = definition.OpenDamageScaleMilli,
                    FocusConsumeTag = definition.FocusConsumeTag ?? string.Empty,
                    MaxIntegrity = definition.MaxIntegrity,
                    IntegrityDamageScaleMilli = definition.IntegrityDamageScaleMilli,
                    BreakReactionId = definition.BreakReactionId ?? string.Empty,
                    AuthoredPriorityDeltaOpen = definition.AuthoredPriorityDeltaOpen,
                    AuthoredPriorityDeltaBroken = definition.AuthoredPriorityDeltaBroken,
                    Severable = definition.Severable,
                    SeverRequiredTag = definition.SeverRequiredTag ?? string.Empty,
                    PresentationEventId = definition.PresentationEventId ?? string.Empty
                });
            }
            return result;
        }

        public static string SerializeCanonical(CharacterBodyPartDamageDocument source)
        {
            return JsonSerializer.Serialize(
                CreateCanonicalDocument(source),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
        }

        public static string ComputeContentHash(CharacterBodyPartDamageDocument source)
        {
            return CharacterPackageHashUtility.ComputeTextSha256(BuildCanonicalHashText(source));
        }

        public static string BuildCanonicalHashText(CharacterBodyPartDamageDocument source)
        {
            CharacterBodyPartDamageDocument canonical = CreateCanonicalDocument(source);
            var builder = new StringBuilder();
            builder.Append("format|").Append(canonical.Format).Append('\n')
                .Append("schemaVersion|").Append(canonical.SchemaVersion).Append('\n')
                .Append("revision|").Append(canonical.Revision).Append('\n');
            for (int i = 0; i < canonical.PartStates.Count; i++)
            {
                CharacterBodyPartDamageAuthoring definition = canonical.PartStates[i];
                builder.Append("partState|").Append(definition.PartStateId).Append('|')
                    .Append(definition.DamageZoneId).Append('|')
                    .Append(definition.WoundCapacity).Append('|')
                    .Append(definition.OpenDamageScaleMilli).Append('|')
                    .Append(definition.FocusConsumeTag).Append('|')
                    .Append(definition.MaxIntegrity).Append('|')
                    .Append(definition.IntegrityDamageScaleMilli).Append('|')
                    .Append(definition.BreakReactionId).Append('|')
                    .Append(definition.AuthoredPriorityDeltaOpen).Append('|')
                    .Append(definition.AuthoredPriorityDeltaBroken).Append('|')
                    .Append(definition.Severable).Append('|')
                    .Append(definition.SeverRequiredTag).Append('|')
                    .Append(definition.PresentationEventId).Append('\n');
                for (int memberIndex = 0; memberIndex < definition.MemberPartIds.Count; memberIndex++)
                    builder.Append("member|").Append(definition.MemberPartIds[memberIndex]).Append('\n');
            }
            return builder.ToString();
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
        public CharacterBodyPartDamageDocument BodyPartDamage { get; set; }
        public CharacterBodyPartDamageDocumentState BodyPartDamageDocumentState { get; set; } =
            CharacterBodyPartDamageDocumentState.Missing;
        public string BodyPartDamageRawPayload { get; set; } = string.Empty;
    }
}
