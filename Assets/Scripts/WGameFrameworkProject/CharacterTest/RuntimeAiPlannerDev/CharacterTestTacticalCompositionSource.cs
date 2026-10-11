using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Formal card payload. Shared definitions and card metadata belong to their owners.</summary>
    internal sealed class CharacterTestTacticalCompositionSource
    {
        internal const string CurrentSchema = "mx.character-test.tactical-card-composition.v1";
        [JsonProperty(Required = Required.Always)] public string Schema { get; set; } = CurrentSchema;
        [JsonProperty(Required = Required.Always)] public int Version { get; set; } = 1;
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ActivationConditionRoot { get; set; }
        [JsonProperty(Required = Required.Always)] public CharacterTestTacticalDslNode Root { get; set; }
        internal CharacterTestTacticalCompositionSource Clone() => CharacterTestTacticalCompositionJson.Read( CharacterTestTacticalCompositionJson.Write(this));
    }

    internal static class CharacterTestTacticalCompositionJson
    {
        internal const string SharedSchema = "mx.character-test.tactical-shared-definition.v1";
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented, MaxDepth = 256
        };
        internal static string Write(CharacterTestTacticalCompositionSource source) => JsonConvert.SerializeObject(source, Settings);
        internal static CharacterTestTacticalCompositionSource Read(string json)
        {
            var token = ReadObject(json);
            if ((string)token["schema"] != CharacterTestTacticalCompositionSource.CurrentSchema
                || token["version"]?.Type != JTokenType.Integer || (int)token["version"] != 1)
                throw new JsonSerializationException("Unsupported tactical composition schema/version.");
            var result = token.ToObject<CharacterTestTacticalCompositionSource>(JsonSerializer.Create(Settings));
            // Reuse the strict node codec without giving it ownership of shared definitions.
            var projection = new JObject { ["schema"] = CharacterTestTacticalDslSource.TrialSchema,
                ["cardId"] = "codec-validation", ["root"] = token["root"].DeepClone(),
                ["activationConditionRoot"] = token["activationConditionRoot"]?.DeepClone(), ["definitions"] = new JArray() };
            CharacterTestTacticalDslJson.Read(projection.ToString(Formatting.None));
            return result;
        }
        internal static string WriteShared(CharacterTestTacticalSharedDefinition definition)
            => new JObject { ["schema"] = SharedSchema, ["version"] = 1,
                ["definitionId"] = definition.DefinitionId, ["revision"] = definition.Revision,
                ["source"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(definition.Source)) }.ToString(Formatting.Indented);
        internal static CharacterTestTacticalSharedDefinition ReadShared(string json)
        {
            var token = ReadObject(json);
            foreach (var property in token.Properties())
                if (property.Name != "schema" && property.Name != "version" && property.Name != "definitionId" && property.Name != "revision" && property.Name != "source")
                    throw new JsonSerializationException("Unknown shared definition field: " + property.Name);
            if ((string)token["schema"] != SharedSchema || token["version"]?.Type != JTokenType.Integer || (int)token["version"] != 1
                || token["definitionId"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token["definitionId"])
                || ((string)token["definitionId"]).Length > 128 || token["revision"]?.Type != JTokenType.Integer || (int)token["revision"] <= 0)
                throw new JsonSerializationException("Invalid shared definition identity or version.");
            var source = CharacterTestTacticalAuthoringJson.ReadSharedTrial(token["source"]?.ToString(Formatting.None));
            if (source.Schema == CharacterTestTacticalAuthoringSource.EventSetTrialSchema)
                throw new JsonSerializationException("Import the trial event-set explicitly before formal workspace persistence.");
            return new CharacterTestTacticalSharedDefinition { DefinitionId = (string)token["definitionId"], Revision = (int)token["revision"], Source = source };
        }
        private static JObject ReadObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024) throw new JsonSerializationException("Invalid or oversized tactical JSON.");
            CharacterTestTacticalAuthoringJson.ValidateLexicalJson(json);
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 256, DateParseHandling = DateParseHandling.None })
            {
                var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonSerializationException("Trailing JSON content.");
                return result;
            }
        }
    }
}
