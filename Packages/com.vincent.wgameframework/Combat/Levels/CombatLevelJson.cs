using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Combat.Physics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Combat.Levels
{
    public static class CombatLevelJson
    {
        private static readonly JsonSerializerSettings Settings = CreateSettings();
        private static readonly HashSet<string> LevelV1Fields = Fields("schemaVersion", "levelStableId", "displayName", "aabbColliders", "spawnPoints", "checkpoints");
        private static readonly HashSet<string> LevelV2Fields = Fields("schemaVersion", "levelStableId", "displayName", "aabbColliders", "obbColliders", "spawnPoints", "checkpoints");
        private static readonly HashSet<string> AabbFields = Fields("stableId", "entityId", "bodyId", "colliderId", "layer", "position", "localMin", "localMax", "surfaceMaterial");
        private static readonly HashSet<string> ObbFields = Fields("stableId", "entityId", "bodyId", "colliderId", "layer", "position", "localCenter", "halfExtents", "axisX", "axisY", "axisZ", "surfaceMaterial");
        private static readonly HashSet<string> PointFields = Fields("stableId", "position");
        private static readonly HashSet<string> VectorFields = Fields("xRaw", "yRaw", "zRaw");
        private static readonly HashSet<string> MaterialFields = Fields("kind", "frictionRaw", "elasticityRaw", "fixedBounceHeightRaw");

        public static string SaveToJson(CombatLevelDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            CombatLevelValidationReport report = CombatLevelValidator.Validate(definition);
            if (report.HasErrors)
                throw new InvalidOperationException(CreateValidationFailureMessage(report));

            return JsonConvert.SerializeObject(definition, Settings);
        }

        public static CombatLevelDefinition LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Combat level json cannot be null or empty.", nameof(json));

            JToken token = ParseWithoutDuplicateProperties(json);
            if (token is not JObject root)
                throw new JsonSerializationException("Combat level root must be a JSON object.");

            int schemaVersion = ReadSchemaVersion(root);
            if (schemaVersion < 1 || schemaVersion > CombatLevelDefinition.CurrentSchemaVersion)
            {
                throw new NotSupportedException(
                    "Unsupported Combat level schema version: "
                    + root["schemaVersion"].ToString(Formatting.None)
                    + ".");
            }

            ValidateRawDocument(root, schemaVersion);
            JObject canonicalRoot = schemaVersion == 1 ? MigrateV1(root) : root;
            CombatLevelDefinition definition;
            try
            {
                definition = canonicalRoot.ToObject<CombatLevelDefinition>(JsonSerializer.Create(Settings));
            }
            catch (JsonException exception)
            {
                throw new JsonSerializationException(
                    "Combat level field is missing, null, or invalid. Path: $.",
                    exception);
            }

            CombatLevelValidationReport report = CombatLevelValidator.Validate(definition);
            if (report.HasErrors)
                throw new JsonSerializationException(CreateValidationFailureMessage(report));
            return definition;
        }

        private static JToken ParseWithoutDuplicateProperties(string json)
        {
            using var textReader = new StringReader(json);
            using var jsonReader = new JsonTextReader(textReader)
            {
                DateParseHandling = DateParseHandling.None
            };
            return JToken.Load(
                jsonReader,
                new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
        }

        private static int ReadSchemaVersion(JObject root)
        {
            JToken schema = root["schemaVersion"];
            if (schema == null || schema.Type != JTokenType.Integer)
                throw new JsonSerializationException("Combat level schemaVersion must be an integer.");

            string raw = schema.ToString(Formatting.None);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                || value < int.MinValue
                || value > int.MaxValue)
            {
                throw new NotSupportedException("Unsupported Combat level schema version: " + raw + ".");
            }

            return (int)value;
        }

        private static void ValidateRawDocument(JObject root, int schemaVersion)
        {
            if (schemaVersion == 1 && root.Property("obbColliders", StringComparison.Ordinal) != null)
                throw new JsonSerializationException("Combat level schema v1 must not contain $.obbColliders.");

            ValidateKnownFields(root, schemaVersion == 1 ? LevelV1Fields : LevelV2Fields, "$.");
            RequireString(root, "levelStableId", "$.levelStableId");
            RequireString(root, "displayName", "$.displayName");
            ValidateAabbArray(RequireArray(root, "aabbColliders", "$.aabbColliders"), "$.aabbColliders");
            if (schemaVersion == 2)
                ValidateObbArray(RequireArray(root, "obbColliders", "$.obbColliders"), "$.obbColliders");
            ValidatePointArray(RequireArray(root, "spawnPoints", "$.spawnPoints"), "$.spawnPoints");
            ValidatePointArray(RequireArray(root, "checkpoints", "$.checkpoints"), "$.checkpoints");
        }

        private static void ValidateAabbArray(JArray array, string path)
        {
            for (int i = 0; i < array.Count; i++)
            {
                string itemPath = path + "[" + i + "]";
                JObject item = RequireObject(array[i], itemPath);
                ValidateKnownFields(item, AabbFields, itemPath + ".");
                ValidateIdentity(item, itemPath);
                ValidateVector(RequireObjectField(item, "position", itemPath + ".position"), itemPath + ".position");
                ValidateVector(RequireObjectField(item, "localMin", itemPath + ".localMin"), itemPath + ".localMin");
                ValidateVector(RequireObjectField(item, "localMax", itemPath + ".localMax"), itemPath + ".localMax");
                ValidateMaterial(RequireObjectField(item, "surfaceMaterial", itemPath + ".surfaceMaterial"), itemPath + ".surfaceMaterial");
            }
        }

        private static void ValidateObbArray(JArray array, string path)
        {
            for (int i = 0; i < array.Count; i++)
            {
                string itemPath = path + "[" + i + "]";
                JObject item = RequireObject(array[i], itemPath);
                ValidateKnownFields(item, ObbFields, itemPath + ".");
                ValidateIdentity(item, itemPath);
                ValidateVector(RequireObjectField(item, "position", itemPath + ".position"), itemPath + ".position");
                ValidateVector(RequireObjectField(item, "localCenter", itemPath + ".localCenter"), itemPath + ".localCenter");
                ValidateVector(RequireObjectField(item, "halfExtents", itemPath + ".halfExtents"), itemPath + ".halfExtents");
                ValidateVector(RequireObjectField(item, "axisX", itemPath + ".axisX"), itemPath + ".axisX");
                ValidateVector(RequireObjectField(item, "axisY", itemPath + ".axisY"), itemPath + ".axisY");
                ValidateVector(RequireObjectField(item, "axisZ", itemPath + ".axisZ"), itemPath + ".axisZ");
                ValidateMaterial(RequireObjectField(item, "surfaceMaterial", itemPath + ".surfaceMaterial"), itemPath + ".surfaceMaterial");
            }
        }

        private static void ValidatePointArray(JArray array, string path)
        {
            for (int i = 0; i < array.Count; i++)
            {
                string itemPath = path + "[" + i + "]";
                JObject item = RequireObject(array[i], itemPath);
                ValidateKnownFields(item, PointFields, itemPath + ".");
                RequireString(item, "stableId", itemPath + ".stableId");
                ValidateVector(RequireObjectField(item, "position", itemPath + ".position"), itemPath + ".position");
            }
        }

        private static void ValidateIdentity(JObject item, string path)
        {
            RequireString(item, "stableId", path + ".stableId");
            RequireInt32(item, "entityId", path + ".entityId");
            RequireInt32(item, "bodyId", path + ".bodyId");
            RequireInt32(item, "colliderId", path + ".colliderId");
            RequireInt32(item, "layer", path + ".layer");
        }

        private static void ValidateVector(JObject value, string path)
        {
            ValidateKnownFields(value, VectorFields, path + ".");
            RequireInt64(value, "xRaw", path + ".xRaw");
            RequireInt64(value, "yRaw", path + ".yRaw");
            RequireInt64(value, "zRaw", path + ".zRaw");
        }

        private static void ValidateMaterial(JObject value, string path)
        {
            ValidateKnownFields(value, MaterialFields, path + ".");
            JToken kind = RequireToken(value, "kind", path + ".kind");
            if (kind.Type != JTokenType.String
                || !Enum.TryParse(kind.Value<string>(), false, out CombatSurfaceMaterialKind parsedKind)
                || !Enum.IsDefined(typeof(CombatSurfaceMaterialKind), parsedKind))
            {
                ThrowInvalidField(path + ".kind");
            }
            RequireInt64(value, "frictionRaw", path + ".frictionRaw");
            RequireInt64(value, "elasticityRaw", path + ".elasticityRaw");
            RequireInt64(value, "fixedBounceHeightRaw", path + ".fixedBounceHeightRaw");
        }

        private static JObject MigrateV1(JObject root)
        {
            var migrated = (JObject)root.DeepClone();
            migrated["schemaVersion"] = CombatLevelDefinition.CurrentSchemaVersion;
            migrated["obbColliders"] = new JArray();
            return migrated;
        }

        private static string CreateValidationFailureMessage(CombatLevelValidationReport report)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                CombatLevelValidationIssue issue = report.Issues[i];
                if (issue.Severity == CombatLevelValidationSeverity.Error)
                {
                    return "Combat level validation failed: " + issue.Code + " at " + issue.Path + ". " + issue.Message;
                }
            }
            return "Combat level validation failed: unknown error at $. Combat level definition is invalid.";
        }

        private static void ValidateKnownFields(JObject value, HashSet<string> knownFields, string pathPrefix)
        {
            foreach (JProperty property in value.Properties())
            {
                if (!knownFields.Contains(property.Name))
                    throw new JsonSerializationException("Unknown Combat level property: " + pathPrefix + property.Name + ".");
            }
        }

        private static JArray RequireArray(JObject owner, string field, string path)
        {
            if (owner[field] is not JArray array)
                throw new JsonSerializationException("Combat level " + path + " must be a JSON array.");
            return array;
        }

        private static JObject RequireObjectField(JObject owner, string field, string path) => RequireObject(owner[field], path);

        private static JObject RequireObject(JToken token, string path)
        {
            if (token is not JObject value)
                ThrowInvalidField(path);
            return (JObject)token;
        }

        private static void RequireString(JObject owner, string field, string path)
        {
            if (RequireToken(owner, field, path).Type != JTokenType.String)
                ThrowInvalidField(path);
        }

        private static void RequireInt32(JObject owner, string field, string path)
        {
            JToken token = RequireToken(owner, field, path);
            string raw = token.ToString(Formatting.None);
            if (token.Type != JTokenType.Integer
                || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                || value < int.MinValue || value > int.MaxValue)
                ThrowInvalidField(path);
        }

        private static void RequireInt64(JObject owner, string field, string path)
        {
            JToken token = RequireToken(owner, field, path);
            if (token.Type != JTokenType.Integer
                || !long.TryParse(token.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                ThrowInvalidField(path);
        }

        private static JToken RequireToken(JObject owner, string field, string path)
        {
            JToken token = owner[field];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                ThrowInvalidField(path);
            return token;
        }

        private static void ThrowInvalidField(string path)
        {
            throw new JsonSerializationException("Combat level field is missing, null, or invalid. Path: " + path + ".");
        }

        private static HashSet<string> Fields(params string[] names) => new HashSet<string>(names, StringComparer.Ordinal);

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }
    }
}
