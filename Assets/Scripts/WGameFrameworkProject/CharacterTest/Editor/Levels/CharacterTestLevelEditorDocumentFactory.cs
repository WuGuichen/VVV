using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MxFramework.Config.Levels;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestLevelEditorDocumentFactory
    {
        internal static void ValidateId(string id)
        {
            if (id == null || !Regex.IsMatch(id, "^[a-z][a-z0-9._-]{2,79}$"))
                throw new ArgumentException("关卡 ID 需为 3–80 位小写字母、数字、点、短横线或下划线，并以字母开头。");
        }

        internal static MxLevelAuthoringDocument Clone(MxLevelAuthoringDocument source, string id, bool emptyCharacters)
        {
            ValidateId(id);
            if (source == null || source.LevelId == id) throw new ArgumentException("复制需要不同的关卡 ID。");
            var json = JObject.Parse(MxLevelAuthoringJson.SaveToJson(source));
            var modules = ((JArray)json["modules"]).OfType<JObject>().ToArray();
            var identities = new Dictionary<string, string>(StringComparer.Ordinal);
            var ownedIds = new List<JValue>();
            foreach (var module in modules)
            {
                var payload = module["payload"] as JObject;
                if (payload == null) continue;
                foreach (string path in OwnedIdentityPaths((string)module["moduleId"]))
                foreach (var value in payload.SelectTokens(path).OfType<JValue>())
                {
                    if (value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value)) continue;
                    string old = (string)value;
                    identities[old] = old.StartsWith(source.LevelId + ".", StringComparison.Ordinal)
                        ? id + old.Substring(source.LevelId.Length) : id + ".instance." + CharacterTestLevelCatalog.Hash(old).Substring(0, 16);
                    ownedIds.Add(value);
                }
            }
            foreach (var value in ownedIds) value.Value = identities[(string)value];
            json["levelId"] = id;
            foreach (var module in modules)
            {
                var payload = module["payload"] as JObject;
                if (payload == null) continue;
                if ((string)payload["levelId"] == source.LevelId) payload["levelId"] = id;
                foreach (string path in InstanceReferencePaths((string)module["moduleId"]))
                foreach (var value in payload.SelectTokens(path).OfType<JValue>())
                    if (value.Type == JTokenType.String && identities.TryGetValue((string)value, out string replacement))
                        value.Value = replacement;
            }
            foreach (var property in json.Descendants().OfType<JProperty>().Where(p => p.Name == "inputFingerprint" || p.Name == "dependencyFingerprint"))
                property.Value = "";
            var characters = ((JArray)json["modules"]).OfType<JObject>().Single(m => (string)m["moduleId"] == "characterTest.characters");
            if (emptyCharacters)
            {
                characters["payload"]["characters"] = new JArray();
                foreach (var module in ((JArray)json["modules"]).OfType<JObject>())
                {
                    var payload = module["payload"] as JObject;
                    if (payload == null) continue;
                    if ((string)module["moduleId"] == "characterTest.guidance")
                    { if (payload["steps"] != null) payload["steps"] = new JArray(); if (payload["enabled"] != null) payload["enabled"] = false; }
                    if ((string)module["moduleId"] == "characterTest.genesisValidation" && payload["stations"] != null) payload["stations"] = new JArray();
                    if ((string)module["moduleId"] == "characterTest.gameplayTriggers" && payload["triggers"] != null) payload["triggers"] = new JArray();
                    if ((string)module["moduleId"] == "characterTest.tacticalStage")
                    { payload["playerSpawnStableId"] = ""; payload["opponentSpawnStableId"] = ""; }
                }
            }
            return MxLevelAuthoringJson.LoadFromJson(json.ToString());
        }

        // Ownership is declared by module field, never inferred from equal string values.
        private static string[] OwnedIdentityPaths(string moduleId)
        {
            switch (moduleId)
            {
                case "characterTest.characters": return new[] { "characters[*].stableId" };
                case "characterTest.worldObjects": return new[] { "points[*].stableId", "regions[*].stableId",
                    "movableInstances[*].instanceStableId", "singleStaticColliders[*].instanceStableId" };
                case "characterTest.genesisValidation": return new[] { "groups[*].stableId", "stations[*].stableId" };
                case "characterTest.guidance": return new[] { "steps[*].stableId" };
                case "characterTest.gameplayTriggers": return new[] { "entries[*].stableId", "triggers[*].stableId" };
                default: return Array.Empty<string>();
            }
        }

        private static string[] InstanceReferencePaths(string moduleId)
        {
            switch (moduleId)
            {
                case "characterTest.tacticalStage": return new[] { "playerSpawnStableId", "opponentSpawnStableId" };
                case "characterTest.guidance": return new[] { "steps[*].actor" };
                case "characterTest.gameplayTriggers": return new[] { "regionBindings[*].regionStableId",
                    "entries[*].actor", "triggers[*].actor" };
                case "characterTest.genesisValidation": return new[] { "stations[*].groupId",
                    "stations[*].bindings.subjectCharacter.stableId", "stations[*].bindings.targetMovableObject.instanceStableId",
                    "stations[*].bindings.startPoint.stableId", "stations[*].bindings.resetPoint.stableId",
                    "stations[*].bindings.observationRegion.stableId" };
                default: return Array.Empty<string>();
            }
        }
    }
}
