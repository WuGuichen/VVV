using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Runtime
{
    public static class RuntimeSaveStateJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        public static string SaveToJson(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                throw new ArgumentNullException(nameof(saveState));
            }

            return JsonConvert.SerializeObject(saveState, Settings);
        }

        public static RuntimeSaveStateResult<RuntimeSaveState> LoadFromJson(string json)
        {
            return LoadFromJson(json, RuntimeSaveState.CurrentSchemaVersion, null);
        }

        public static RuntimeSaveStateResult<RuntimeSaveState> LoadFromJson(
            string json,
            int targetSchemaVersion,
            RuntimeSaveStateMigrationPipeline migrationPipeline)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json is null or empty.",
                    -1,
                    targetSchemaVersion));
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json is not a valid object: " + ex.Message,
                    -1,
                    targetSchemaVersion,
                    ex));
            }

            JToken schemaVersionToken = root["schemaVersion"];
            if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Save state document is missing an integer schemaVersion.",
                    -1,
                    targetSchemaVersion));
            }

            int schemaVersion = schemaVersionToken.Value<int>();
            if (schemaVersion > targetSchemaVersion)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.schemaVersion",
                    "Save state schema version is newer than this runtime can load.",
                    schemaVersion,
                    targetSchemaVersion));
            }

            RuntimeSaveState saveState;
            try
            {
                saveState = root.ToObject<RuntimeSaveState>(JsonSerializer.Create(Settings));
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json could not be deserialized: " + ex.Message,
                    schemaVersion,
                    targetSchemaVersion,
                    ex));
            }

            RuntimeSaveStateResult<RuntimeSaveState> validation = Validate(saveState, targetSchemaVersion);
            if (!validation.Success)
            {
                return validation;
            }

            if (saveState.SchemaVersion == targetSchemaVersion)
            {
                return validation;
            }

            if (migrationPipeline == null)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.MissingMigration,
                    "$.schemaVersion",
                    "Save state document requires migration, but no migration pipeline was provided.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            return migrationPipeline.Migrate(saveState, targetSchemaVersion);
        }

        private static RuntimeSaveStateResult<RuntimeSaveState> Validate(RuntimeSaveState saveState, int targetSchemaVersion)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json parsed to null.",
                    -1,
                    targetSchemaVersion));
            }

            if (saveState.SchemaVersion < 0)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Save state schema version cannot be negative.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            if (saveState.Frame < 0L)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.frame",
                    "Save state frame cannot be negative.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }
    }
}
