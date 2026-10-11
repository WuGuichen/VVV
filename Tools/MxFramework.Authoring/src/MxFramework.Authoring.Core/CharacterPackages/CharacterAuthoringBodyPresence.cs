using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public enum CharacterBodyPresenceDocumentState
    {
        Missing = 0,
        Current = 1,
        UnsupportedSchema = 2,
        Invalid = 3
    }

    [JsonConverter(typeof(CharacterBodyPresenceReferenceFrameJsonConverter))]
    public enum CharacterBodyPresenceReferenceFrame
    {
        Unknown = 0,
        ModelRoot = 1
    }

    [JsonConverter(typeof(CharacterBodyPresenceShapeJsonConverter))]
    public enum CharacterBodyPresenceShape
    {
        Unknown = 0,
        Sphere = 1,
        Capsule = 2,
        Box = 3
    }

    public static class CharacterBodyPresenceLimits
    {
        public const int MaximumVolumeCount = 4;
        public const int MaximumAnimatedHurtboxSourceCount = 4;
        public const float MaximumLocalCoordinateMeters = 1000f;
        public const float MaximumDimensionMeters = 1000f;
        public const int MaximumSkinMillimeters = 10000;
        public const int MaximumPressureScaleMilli = 100000;
    }

    public sealed class CharacterBodyPresenceAnimatedHurtboxSourceAuthoring
    {
        [JsonRequired]
        public string SourceId { get; set; } = string.Empty;
        [JsonRequired]
        public string ColliderId { get; set; } = string.Empty;
        [JsonRequired]
        public int SkinMillimeters { get; set; }
        [JsonRequired]
        public int PressureScaleMilli { get; set; }
        [JsonRequired]
        public bool Enabled { get; set; }
    }

    public sealed class CharacterBodyPresenceLocalPose
    {
        [JsonRequired]
        public CharacterAuthoringVector3 Position { get; set; } = new CharacterAuthoringVector3();
        [JsonRequired]
        public CharacterAuthoringQuaternion Rotation { get; set; } = new CharacterAuthoringQuaternion();
        [JsonRequired]
        public CharacterAuthoringVector3 Scale { get; set; } = new CharacterAuthoringVector3(1f, 1f, 1f);
    }

    public sealed class CharacterBodyPresenceVolumeAuthoring
    {
        [JsonRequired]
        public string VolumeId { get; set; } = string.Empty;
        [JsonRequired]
        public CharacterBodyPresenceReferenceFrame ReferenceFrame { get; set; }
        [JsonRequired]
        public CharacterBodyPresenceShape Shape { get; set; }
        [JsonRequired]
        public CharacterBodyPresenceLocalPose LocalPose { get; set; } = new CharacterBodyPresenceLocalPose();
        [JsonRequired]
        public float Radius { get; set; }
        [JsonRequired]
        public float Height { get; set; }
        [JsonRequired]
        public CharacterAuthoringVector3 Size { get; set; } = new CharacterAuthoringVector3();
        [JsonRequired]
        public int SkinMillimeters { get; set; }
        [JsonRequired]
        public int PressureScaleMilli { get; set; }
        [JsonRequired]
        public bool Enabled { get; set; }
    }

    public sealed class CharacterBodyPresenceDocument
    {
        public const string LegacyFormat = "mx.characterBodyPresence.v1";
        public const string LegacySchemaVersion = "1.0";
        public const string CurrentFormat = "mx.characterBodyPresence.v2";
        public const string CurrentSchemaVersion = "2.0";

        [JsonRequired]
        public string Format { get; set; } = CurrentFormat;
        [JsonRequired]
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        [JsonRequired]
        public int Revision { get; set; }
        [JsonRequired]
        public List<CharacterBodyPresenceVolumeAuthoring> Volumes { get; set; } =
            new List<CharacterBodyPresenceVolumeAuthoring>();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring> AnimatedHurtboxSources { get; set; }

        [JsonIgnore]
        public bool IsV2 => string.Equals(Format, CurrentFormat, StringComparison.Ordinal)
            && string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);
    }

    public sealed class CharacterBodyPresenceLoadResult
    {
        public CharacterBodyPresenceDocumentState State { get; set; }
        public CharacterBodyPresenceDocument Document { get; set; }
        public string RawPayload { get; set; } = string.Empty;
        public bool WriteEligible => State == CharacterBodyPresenceDocumentState.Current;
    }

    public static class CharacterBodyPresenceDocumentCodec
    {
        public static CharacterBodyPresenceLoadResult Missing()
        {
            return new CharacterBodyPresenceLoadResult
            {
                State = CharacterBodyPresenceDocumentState.Missing
            };
        }

        public static CharacterBodyPresenceLoadResult Read(string payload, JsonSerializerOptions options)
        {
            var result = new CharacterBodyPresenceLoadResult { RawPayload = payload ?? string.Empty };
            try
            {
                using JsonDocument json = JsonDocument.Parse(result.RawPayload);
                if (json.RootElement.ValueKind != JsonValueKind.Object
                    || !TryReadString(json.RootElement, "format", out string format)
                    || !TryReadString(json.RootElement, "schemaVersion", out string schemaVersion))
                {
                    result.State = CharacterBodyPresenceDocumentState.Invalid;
                    return result;
                }

                if (!IsSupportedSchema(format, schemaVersion))
                {
                    result.State = CharacterBodyPresenceDocumentState.UnsupportedSchema;
                    return result;
                }

                var codecOptions = new JsonSerializerOptions(options);
                codecOptions.Converters.Insert(0, new CharacterBodyPresenceReferenceFrameJsonConverter());
                codecOptions.Converters.Insert(0, new CharacterBodyPresenceShapeJsonConverter());
                result.Document = JsonSerializer.Deserialize<CharacterBodyPresenceDocument>(
                    result.RawPayload,
                    codecOptions);
                result.State = result.Document == null
                    ? CharacterBodyPresenceDocumentState.Invalid
                    : CharacterBodyPresenceDocumentState.Current;
            }
            catch (JsonException)
            {
                result.State = CharacterBodyPresenceDocumentState.Invalid;
            }

            return result;
        }

        public static CharacterBodyPresenceDocument CreateCanonicalDocument(CharacterBodyPresenceDocument source)
        {
            var result = new CharacterBodyPresenceDocument
            {
                Format = source?.Format ?? CharacterBodyPresenceDocument.CurrentFormat,
                SchemaVersion = source?.SchemaVersion ?? CharacterBodyPresenceDocument.CurrentSchemaVersion,
                Revision = source?.Revision ?? 0
            };
            if (source?.IsV2 == true)
            {
                var animatedSources = new List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring>(
                    source.AnimatedHurtboxSources ?? new List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring>());
                animatedSources.Sort((left, right) => string.CompareOrdinal(left?.SourceId, right?.SourceId));
                result.AnimatedHurtboxSources = new List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring>();
                for (int i = 0; i < animatedSources.Count; i++)
                {
                    CharacterBodyPresenceAnimatedHurtboxSourceAuthoring item = animatedSources[i];
                    if (item == null)
                        continue;
                    result.AnimatedHurtboxSources.Add(new CharacterBodyPresenceAnimatedHurtboxSourceAuthoring
                    {
                        SourceId = item.SourceId ?? string.Empty,
                        ColliderId = item.ColliderId ?? string.Empty,
                        SkinMillimeters = item.SkinMillimeters,
                        PressureScaleMilli = item.PressureScaleMilli,
                        Enabled = item.Enabled
                    });
                }
            }
            var volumes = new List<CharacterBodyPresenceVolumeAuthoring>(
                source?.Volumes ?? new List<CharacterBodyPresenceVolumeAuthoring>());
            volumes.Sort((left, right) => string.CompareOrdinal(left?.VolumeId, right?.VolumeId));
            for (int i = 0; i < volumes.Count; i++)
            {
                CharacterBodyPresenceVolumeAuthoring volume = volumes[i];
                if (volume == null)
                    continue;
                CharacterBodyPresenceLocalPose pose = volume.LocalPose ?? new CharacterBodyPresenceLocalPose();
                result.Volumes.Add(new CharacterBodyPresenceVolumeAuthoring
                {
                    VolumeId = volume.VolumeId ?? string.Empty,
                    ReferenceFrame = volume.ReferenceFrame,
                    Shape = volume.Shape,
                    LocalPose = new CharacterBodyPresenceLocalPose
                    {
                        Position = Copy(pose.Position),
                        Rotation = Copy(pose.Rotation),
                        Scale = Copy(pose.Scale)
                    },
                    Radius = CanonicalFloat(volume.Radius),
                    Height = CanonicalFloat(volume.Height),
                    Size = Copy(volume.Size),
                    SkinMillimeters = volume.SkinMillimeters,
                    PressureScaleMilli = volume.PressureScaleMilli,
                    Enabled = volume.Enabled
                });
            }
            return result;
        }

        public static string SerializeCanonical(CharacterBodyPresenceDocument source)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Serialize(CreateCanonicalDocument(source), options);
        }

        public static string ComputeContentHash(CharacterBodyPresenceDocument source)
        {
            return CharacterPackageHashUtility.ComputeTextSha256(SerializeCanonical(source));
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

        public static bool IsSupportedSchema(string format, string schemaVersion)
        {
            return (string.Equals(format, CharacterBodyPresenceDocument.LegacyFormat, StringComparison.Ordinal)
                    && string.Equals(schemaVersion, CharacterBodyPresenceDocument.LegacySchemaVersion, StringComparison.Ordinal))
                || (string.Equals(format, CharacterBodyPresenceDocument.CurrentFormat, StringComparison.Ordinal)
                    && string.Equals(schemaVersion, CharacterBodyPresenceDocument.CurrentSchemaVersion, StringComparison.Ordinal));
        }

        private static CharacterAuthoringVector3 Copy(CharacterAuthoringVector3 value)
        {
            return value == null
                ? null
                : new CharacterAuthoringVector3(
                    CanonicalFloat(value.X),
                    CanonicalFloat(value.Y),
                    CanonicalFloat(value.Z));
        }

        private static CharacterAuthoringQuaternion Copy(CharacterAuthoringQuaternion value)
        {
            return value == null
                ? null
                : new CharacterAuthoringQuaternion(
                    CanonicalFloat(value.X),
                    CanonicalFloat(value.Y),
                    CanonicalFloat(value.Z),
                    CanonicalFloat(value.W));
        }

        private static float CanonicalFloat(float value) => value == 0f ? 0f : value;
    }

    public sealed partial class CharacterAuthoringGeometry
    {
        public CharacterBodyPresenceDocument BodyPresence { get; set; }
        public CharacterBodyPresenceDocumentState BodyPresenceDocumentState { get; set; } =
            CharacterBodyPresenceDocumentState.Missing;
        public string BodyPresenceRawPayload { get; set; } = string.Empty;
        public bool BodyPresenceWriteEligible =>
            BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Current;
    }

    internal sealed class CharacterBodyPresenceReferenceFrameJsonConverter
        : JsonConverter<CharacterBodyPresenceReferenceFrame>
    {
        public override CharacterBodyPresenceReferenceFrame Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && string.Equals(reader.GetString(), "ModelRoot", StringComparison.Ordinal))
                return CharacterBodyPresenceReferenceFrame.ModelRoot;
            if (reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Number)
                return CharacterBodyPresenceReferenceFrame.Unknown;
            throw new JsonException("Body Presence referenceFrame must be a string.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            CharacterBodyPresenceReferenceFrame value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value == CharacterBodyPresenceReferenceFrame.ModelRoot ? "ModelRoot" : "Unknown");
        }
    }

    internal sealed class CharacterBodyPresenceShapeJsonConverter : JsonConverter<CharacterBodyPresenceShape>
    {
        public override CharacterBodyPresenceShape Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() switch
                {
                    "Sphere" => CharacterBodyPresenceShape.Sphere,
                    "Capsule" => CharacterBodyPresenceShape.Capsule,
                    "Box" => CharacterBodyPresenceShape.Box,
                    _ => CharacterBodyPresenceShape.Unknown
                };
            }
            if (reader.TokenType == JsonTokenType.Number)
                return CharacterBodyPresenceShape.Unknown;
            throw new JsonException("Body Presence shape must be a string.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            CharacterBodyPresenceShape value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value switch
            {
                CharacterBodyPresenceShape.Sphere => "Sphere",
                CharacterBodyPresenceShape.Capsule => "Capsule",
                CharacterBodyPresenceShape.Box => "Box",
                _ => "Unknown"
            });
        }
    }
}
