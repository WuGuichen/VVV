using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Trial workspace only; not a formal catalog schema or Runtime interpreter.</summary>
    internal sealed class CharacterTestTacticalDslSource
    {
        internal const string TrialSchema = "mx.character-test.tactical-dsl.trial.v1";
        [JsonProperty(Required = Required.Always)] public string Schema { get; set; } = TrialSchema;
        [JsonProperty(Required = Required.Always)] public string CardId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ActivationConditionRoot { get; set; }
        [JsonProperty(Required = Required.Always)] public CharacterTestTacticalDslNode Root { get; set; }
        [JsonProperty(Required = Required.Always)] public List<CharacterTestTacticalSharedDefinition> Definitions { get; set; } = new List<CharacterTestTacticalSharedDefinition>();
    }

    internal sealed class CharacterTestTacticalDslNode
    {
        [JsonProperty(Required = Required.Always)] public string NodeId { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public string Kind { get; set; } = "Sequence";
        public string DefinitionId { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Condition { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode EndCondition { get; set; }
        public List<CharacterTestTacticalDslNode> Children { get; set; }
        public Dictionary<string, CharacterTestTacticalIntentDistanceReference> DistanceBindings { get; set; }
        public Dictionary<string, int> BlockDurations { get; set; }
        public int? MaximumWaitFrames { get; set; }
    }

    internal sealed class CharacterTestTacticalIntentDistanceReference
    {
        [JsonProperty(Required = Required.Always)] public string SourceNodeId { get; set; }
        [JsonProperty(Required = Required.Always)] public string SourceBlockId { get; set; }
        [JsonProperty(Required = Required.Always)] public string DataKind { get; set; } = "Configuration";
    }

    internal sealed class CharacterTestTacticalSharedDefinition
    {
        [JsonProperty(Required = Required.Always)] public string DefinitionId { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public int Revision { get; set; } = 1;
        [JsonProperty(Required = Required.Always)] public CharacterTestTacticalAuthoringSource Source { get; set; }
    }

    internal static class CharacterTestTacticalDslJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Error, NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented, MaxDepth = 256
        };

        internal static string Write(CharacterTestTacticalDslSource source) => JsonConvert.SerializeObject(source, Settings);

        internal static CharacterTestTacticalDslSource Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024)
                throw new JsonSerializationException("Trial workspace must contain at most 4 MiB of JSON.");
            CharacterTestTacticalAuthoringJson.ValidateLexicalJson(json);
            JObject token;
            using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 256, DateParseHandling = DateParseHandling.None })
            {
                token = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonSerializationException("Trailing JSON content.");
            }
            RequireType(token["schema"], JTokenType.String);
            RequireType(token["cardId"], JTokenType.String);
            RequireType(token["definitions"], JTokenType.Array);
            ValidateNodeTokens(token["root"], 0);
            foreach (var definition in token["definitions"])
            {
                RequireType(definition, JTokenType.Object);
                RequireType(definition["definitionId"], JTokenType.String);
                RequireType(definition["revision"], JTokenType.Integer);
                RequireType(definition["source"], JTokenType.Object);
            }
            var source = token.ToObject<CharacterTestTacticalDslSource>(JsonSerializer.Create(Settings));
            if (source == null || source.Schema != CharacterTestTacticalDslSource.TrialSchema)
                throw new JsonSerializationException("Unsupported trial schema.");
            // Each embedded v2 source retains its strict codec and unchanged version semantics.
            if (source.Definitions != null)
                for (int i = 0; i < source.Definitions.Count; i++)
                    if (source.Definitions[i] != null)
                        source.Definitions[i].Source = CharacterTestTacticalAuthoringJson.ReadSharedTrial(token["definitions"][i]["source"].ToString());
            return source;
        }

        private static void RequireType(JToken token, JTokenType type)
        {
            if (token == null || token.Type != type) throw new JsonSerializationException("Invalid trial field type: " + token?.Path);
        }

        private static void ValidateNodeTokens(JToken token, int depth)
        {
            if (depth > 64) throw new JsonSerializationException("Trial composition exceeds 64 levels.");
            RequireType(token, JTokenType.Object);
            RequireType(token["nodeId"], JTokenType.String);
            RequireType(token["kind"], JTokenType.String);
            if (token["definitionId"] != null) RequireType(token["definitionId"], JTokenType.String);
            if (token["maximumWaitFrames"] != null) RequireType(token["maximumWaitFrames"], JTokenType.Integer);
            if (token["distanceBindings"] != null)
            {
                RequireType(token["distanceBindings"], JTokenType.Object);
                foreach (var field in ((JObject)token["distanceBindings"]).Properties())
                {
                    RequireType(field.Value, JTokenType.Object);
                    RequireType(field.Value["sourceNodeId"], JTokenType.String);
                    RequireType(field.Value["sourceBlockId"], JTokenType.String);
                    RequireType(field.Value["dataKind"], JTokenType.String);
                }
            }
            if (token["blockDurations"] != null)
            {
                RequireType(token["blockDurations"], JTokenType.Object);
                foreach (var field in ((JObject)token["blockDurations"]).Properties()) RequireType(field.Value, JTokenType.Integer);
            }
            if (token["children"] != null)
            {
                RequireType(token["children"], JTokenType.Array);
                foreach (var child in token["children"]) ValidateNodeTokens(child, depth + 1);
            }
        }

        internal static string Fingerprint(CharacterTestTacticalDslSource source, IEnumerable<CharacterTestTacticalSharedDefinition> dependencies)
        {
            var card = JObject.FromObject(source, JsonSerializer.Create(Settings));
            card.Remove("definitions"); card.Remove("name"); card.Remove("cardId");
            var refs = new JArray();
            foreach (var definition in dependencies.OrderBy(d => d.DefinitionId, StringComparer.Ordinal))
                refs.Add(new JObject { ["id"] = definition.DefinitionId, ["revision"] = definition.Revision,
                    ["source"] = CharacterTestTacticalAuthoringJson.SemanticFingerprint(definition.Source) });
            card["dependencies"] = refs;
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(card.ToString(Formatting.None))))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
