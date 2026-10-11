using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.ResourceMapping.Authoring
{
    public static class ResourceMappingCanonicalJson
    {
        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        /// <remarks>AllocByDesign. Canonical serialization is a low-frequency authoring/build operation.</remarks>
        public static string Serialize<T>(T value)
        {
            JsonElement root = JsonSerializer.SerializeToElement(value, SerializerOptions);
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.Default,
                    Indented = false,
                    SkipValidation = false
                }))
                {
                    WriteCanonicalElement(writer, root);
                }

                return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
            }
        }

        /// <remarks>AllocByDesign. Computes a deterministic SHA-256 over canonical UTF-8 without BOM.</remarks>
        public static string ComputeFingerprint(string canonicalText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText ?? string.Empty);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder("sha256:", 71);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }

        private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = new List<JsonProperty>();
                    foreach (JsonProperty property in element.EnumerateObject())
                        properties.Add(property);
                    properties.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
                    writer.WriteStartObject();
                    for (int i = 0; i < properties.Count; i++)
                    {
                        writer.WritePropertyName(properties[i].Name);
                        WriteCanonicalElement(writer, properties[i].Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement item in element.EnumerateArray())
                        WriteCanonicalElement(writer, item);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    element.WriteTo(writer);
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported JSON value kind: " + element.ValueKind + ".");
            }
        }
    }

    public sealed class ResourceMappingProfileReadResult
    {
        public bool Success { get; set; }
        public ResourceMappingProfileV2 Profile { get; set; }
        public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();
    }

    public static class ResourceMappingProfileJson
    {
        private static readonly JsonSerializerOptions StrictOptions = CreateStrictOptions();

        /// <remarks>AllocByDesign. Strict JSON parsing is a low-frequency authoring/build operation.</remarks>
        public static ResourceMappingProfileReadResult ReadStrict(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json ?? string.Empty))
                    ValidateRequiredShape(document.RootElement);
                ResourceMappingProfileV2 profile = JsonSerializer.Deserialize<ResourceMappingProfileV2>(json ?? string.Empty, StrictOptions);
                if (profile == null)
                    return Failure("Profile JSON did not contain an object.");
                return new ResourceMappingProfileReadResult { Success = true, Profile = profile };
            }
            catch (JsonException exception)
            {
                return Failure(exception.Message);
            }
        }

        private static void ValidateRequiredShape(JsonElement root)
        {
            RequireObject(root, "$", "schema", "profileId", "catalogId", "entries", "bundleRules", "catalogMounts", "preloadGroups", "metadata");
            RequireStrings(root, "$", "schema", "profileId", "catalogId");
            ValidateArray(root, "entries", "entries", ValidateEntry);
            ValidateArray(root, "bundleRules", "bundleRules", ValidateBundleRule);
            ValidateArray(root, "catalogMounts", "catalogMounts", ValidateCatalogMount);
            ValidateArray(root, "preloadGroups", "preloadGroups", ValidatePreloadGroup);
            ValidateStringMap(root.GetProperty("metadata"), "metadata");
        }

        private static void ValidateEntry(JsonElement value, string path)
        {
            RequireObject(value, path, "producerReceipt", "authoringRecordId", "resourceKey", "sourceLocator", "deliveryIntent", "overrideIntent", "externalCatalogBinding", "bundleIntent", "labels", "preloadGroups", "dependencies", "providerData");
            ValidateReceipt(value.GetProperty("producerReceipt"), path + ".producerReceipt");
            RequireString(value.GetProperty("authoringRecordId"), path + ".authoringRecordId");
            ValidateNullableObject(value.GetProperty("resourceKey"), path + ".resourceKey", ValidateKey);
            ValidateNullableObject(value.GetProperty("sourceLocator"), path + ".sourceLocator", ValidateSource);
            RequireString(value.GetProperty("deliveryIntent"), path + ".deliveryIntent");
            RequireStringOrNull(value.GetProperty("overrideIntent"), path + ".overrideIntent");
            ValidateNullableObject(value.GetProperty("externalCatalogBinding"), path + ".externalCatalogBinding", (item, itemPath) =>
            {
                RequireObject(item, itemPath, "catalogMountId");
                RequireString(item.GetProperty("catalogMountId"), itemPath + ".catalogMountId");
            });
            ValidateNullableObject(value.GetProperty("bundleIntent"), path + ".bundleIntent", (item, itemPath) =>
            {
                RequireObject(item, itemPath, "ruleId");
                RequireString(item.GetProperty("ruleId"), itemPath + ".ruleId");
            });
            ValidateStringArray(value.GetProperty("labels"), path + ".labels");
            ValidateStringArray(value.GetProperty("preloadGroups"), path + ".preloadGroups");
            ValidateArray(value, "dependencies", path + ".dependencies", ValidateKey);
            ValidateStringMap(value.GetProperty("providerData"), path + ".providerData");
        }

        private static void ValidateBundleRule(JsonElement value, string path)
        {
            RequireObject(value, path, "producerReceipt", "authoringRecordId", "id", "bundleName", "compression", "buildTarget", "includeDependencies", "allowEmpty", "providerData");
            ValidateReceipt(value.GetProperty("producerReceipt"), path + ".producerReceipt");
            RequireStrings(value, path, "authoringRecordId", "id", "bundleName", "compression", "buildTarget");
            RequireBoolean(value.GetProperty("includeDependencies"), path + ".includeDependencies");
            RequireBoolean(value.GetProperty("allowEmpty"), path + ".allowEmpty");
            ValidateStringMap(value.GetProperty("providerData"), path + ".providerData");
        }

        private static void ValidateCatalogMount(JsonElement value, string path)
        {
            RequireObject(value, path, "producerReceipt", "authoringRecordId", "catalogMountId", "catalogId", "bootstrapKind", "relativeCatalogPath", "mountOrder", "expectedCatalogFingerprint");
            ValidateReceipt(value.GetProperty("producerReceipt"), path + ".producerReceipt");
            RequireStrings(value, path, "authoringRecordId", "catalogMountId", "catalogId", "bootstrapKind", "relativeCatalogPath", "expectedCatalogFingerprint");
            RequireInteger(value.GetProperty("mountOrder"), path + ".mountOrder");
        }

        private static void ValidatePreloadGroup(JsonElement value, string path)
        {
            RequireObject(value, path, "producerReceipt", "authoringRecordId", "id", "explicitKeys", "labels", "failFast", "maxConcurrentLoads", "allowEmpty", "providerData");
            ValidateReceipt(value.GetProperty("producerReceipt"), path + ".producerReceipt");
            RequireStrings(value, path, "authoringRecordId", "id");
            ValidateArray(value, "explicitKeys", path + ".explicitKeys", ValidateKey);
            ValidateStringArray(value.GetProperty("labels"), path + ".labels");
            RequireBoolean(value.GetProperty("failFast"), path + ".failFast");
            RequireInteger(value.GetProperty("maxConcurrentLoads"), path + ".maxConcurrentLoads");
            RequireBoolean(value.GetProperty("allowEmpty"), path + ".allowEmpty");
            ValidateStringMap(value.GetProperty("providerData"), path + ".providerData");
        }

        private static void ValidateReceipt(JsonElement value, string path)
        {
            RequireObject(value, path, "producerId", "producerContractVersion", "sourceRecordFingerprint");
            RequireStrings(value, path, "producerId", "producerContractVersion", "sourceRecordFingerprint");
        }

        private static void ValidateKey(JsonElement value, string path)
        {
            RequireObject(value, path, "id", "typeId", "variant", "packageId");
            RequireStrings(value, path, "id", "typeId", "variant", "packageId");
        }

        private static void ValidateSource(JsonElement value, string path)
        {
            RequireObject(value, path, "kind", "unityGuid", "cachedPath", "generatedAssetId", "domainSourceId", "packageRelativePath", "contentHash", "providerData");
            RequireStrings(value, path, "kind", "unityGuid", "cachedPath", "generatedAssetId", "domainSourceId", "packageRelativePath", "contentHash");
            ValidateStringMap(value.GetProperty("providerData"), path + ".providerData");
        }

        private static void ValidateNullableObject(JsonElement value, string path, Action<JsonElement, string> validate)
        {
            if (value.ValueKind == JsonValueKind.Null) return;
            validate(value, path);
        }

        private static void ValidateArray(JsonElement owner, string property, string path, Action<JsonElement, string> validate)
        {
            JsonElement array = owner.GetProperty(property);
            if (array.ValueKind != JsonValueKind.Array)
                throw new JsonException(path + " must be an array.");
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
                validate(item, path + "[" + index++ + "]");
        }

        private static void ValidateStringArray(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Array) throw new JsonException(path + " must be an array.");
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray()) RequireString(item, path + "[" + index++ + "]");
        }

        private static void ValidateStringMap(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new JsonException(path + " must be an object.");
            foreach (JsonProperty property in value.EnumerateObject()) RequireString(property.Value, path + "." + property.Name);
        }

        private static void RequireStrings(JsonElement owner, string path, params string[] properties)
        {
            for (int i = 0; i < properties.Length; i++) RequireString(owner.GetProperty(properties[i]), path + "." + properties[i]);
        }

        private static void RequireString(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.String) throw new JsonException(path + " must be a string.");
        }

        private static void RequireStringOrNull(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.String && value.ValueKind != JsonValueKind.Null) throw new JsonException(path + " must be a string or null.");
        }

        private static void RequireBoolean(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False) throw new JsonException(path + " must be a boolean.");
        }

        private static void RequireInteger(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _)) throw new JsonException(path + " must be an integer.");
        }

        private static void RequireObject(JsonElement value, string path, params string[] properties)
        {
            if (value.ValueKind != JsonValueKind.Object)
                throw new JsonException(path + " must be an object.");
            for (int i = 0; i < properties.Length; i++)
                if (!value.TryGetProperty(properties[i], out _))
                    throw new JsonException(path + " is missing required property '" + properties[i] + "'.");
        }

        private static JsonSerializerOptions CreateStrictOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            return options;
        }

        private static ResourceMappingProfileReadResult Failure(string message)
        {
            var result = new ResourceMappingProfileReadResult { Success = false, Profile = null };
            result.Diagnostics.Add(new ResourceMappingDiagnostic
            {
                Severity = ResourceMappingDiagnosticSeverity.Error,
                Code = "ProfileJsonInvalid",
                Message = message ?? string.Empty,
                Path = "$"
            });
            return result;
        }
    }
}
