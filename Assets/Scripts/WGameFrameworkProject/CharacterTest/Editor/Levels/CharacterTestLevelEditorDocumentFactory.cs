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
            var identities = new Dictionary<string, string>(StringComparer.Ordinal) { [source.LevelId] = id };
            foreach (var property in json.Descendants().OfType<JProperty>().Where(p => p.Name == "stableId" && p.Value.Type == JTokenType.String))
            {
                string old = (string)property.Value;
                if (string.IsNullOrWhiteSpace(old)) continue;
                identities[old] = old.StartsWith(source.LevelId + ".", StringComparison.Ordinal)
                    ? id + old.Substring(source.LevelId.Length) : id + ".instance." + CharacterTestLevelCatalog.Hash(old).Substring(0, 16);
            }
            foreach (var value in json.Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.String).ToArray())
                if (identities.TryGetValue((string)value, out string replacement)) value.Value = replacement;
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
    }
}
