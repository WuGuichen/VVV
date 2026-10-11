using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>Restores authoring identities for changed Common actions while preserving every other source action.</summary>
    internal static class CharacterActionCommonSourceProjection
    {
        internal static string MergeChangedActions(
            string sourceJson,
            string generatedBaselineJson,
            string generatedCurrentJson,
            IReadOnlyCollection<int> forcedActionIds = null)
        {
            var source = JObject.Parse(sourceJson);
            var baseline = JObject.Parse(generatedBaselineJson);
            var current = JObject.Parse(generatedCurrentJson);
            var sourceActions = source["actions"] as JArray
                ?? throw new FormatException("Common source has no actions array.");
            var currentActions = current["actions"] as JArray
                ?? throw new FormatException("Common projection has no actions array.");
            var baselineById = Index(baseline["actions"] as JArray);
            var sourceById = Index(sourceActions);
            var currentById = Index(currentActions);
            foreach (int actionId in baselineById.Keys)
            {
                if (!currentById.ContainsKey(actionId)
                    && sourceById.TryGetValue(actionId, out JObject deletedAction))
                    deletedAction.Remove();
            }
            foreach (JObject generatedAction in currentActions.Children<JObject>())
            {
                int actionId = generatedAction.Value<int>("actionId");
                bool changed = forcedActionIds?.Contains(actionId) == true
                    || !baselineById.TryGetValue(actionId, out JObject oldAction)
                    || !JToken.DeepEquals(Comparable(oldAction.DeepClone()), Comparable(generatedAction.DeepClone()));
                if (!changed) continue;
                JObject candidate = (JObject)generatedAction.DeepClone();
                MxFramework.CharacterAction.CharacterActionBladeTrailJsonProjection.RemoveGeneratedEvents(candidate);
                JObject restored = (JObject)Restore(candidate);
                if (sourceById.TryGetValue(actionId, out JObject original))
                    original.Replace(restored);
                else
                    sourceActions.Add(restored);
            }
            source["schemaVersion"] = "0.3.0";
            return source.ToString(Formatting.Indented) + "\n";
        }

        private static Dictionary<int, JObject> Index(JArray actions)
        {
            var result = new Dictionary<int, JObject>();
            foreach (JObject action in actions?.OfType<JObject>() ?? Array.Empty<JObject>())
            {
                int id = action.Value<int>("actionId");
                if (id <= 0 || result.ContainsKey(id))
                    throw new FormatException("Duplicate or invalid Common action id: " + id);
                result.Add(id, action);
            }
            return result;
        }

        private static JToken Restore(JToken token)
        {
            if (token is JArray array)
            {
                foreach (JToken child in array)
                    Restore(child);
                return array;
            }
            if (!(token is JObject obj)) return token;
            bool damage = obj["baseDamage"] != null || obj["damageTypeId"] != null;
            var names = new List<string>();
            foreach (JProperty property in obj.Properties()) names.Add(property.Name);
            foreach (string name in names)
            {
                JToken value = obj[name];
                if (name == "formulaId" || name == "formulaLabel"
                    || name == "stableEventOrder" || name == "numericSocketId"
                    || name == "vfxDefinitionId")
                {
                    obj.Remove(name);
                    continue;
                }
                if (name == "cueId" && value?.Type == JTokenType.Integer)
                {
                    int id = value.Value<int>();
                    obj[name] = id <= 0 ? string.Empty
                        : string.Equals(obj.Value<string>("kind"), "PlayAudioCue", StringComparison.Ordinal)
                            ? CatalogIdentity("Audio/audio_runtime_catalog.json", "cues", "id", "audioCueId", id)
                            : SourceKey(401, id);
                    obj.Remove("cueLabel");
                    continue;
                }
                if (name == "abilityStableId" && value?.Type == JTokenType.Integer)
                {
                    int id = value.Value<int>();
                    obj[name] = id <= 0 ? string.Empty
                        : CatalogIdentity("Abilities/ability_catalog.json", "abilities", "abilityId", "abilityStableId", id);
                    obj.Remove("abilityStableLabel");
                    continue;
                }
                int domain = Domain(name);
                if (domain > 0 && value?.Type == JTokenType.Integer)
                {
                    int id = value.Value<int>();
                    if (id > 0)
                        obj[name] = SourceKey(domain, id);
                    else
                        obj[name] = string.Empty;
                    obj.Remove(LabelName(name));
                    continue;
                }
                if (value is JArray identities &&
                    ((damage && name == "tags") || name == "allowedPostureIds"
                     || name == "occupiesSlots" || name == "slotIds"
                     || name == "requiredFilledSlots" || name == "requiredEmptySlots"))
                {
                    int arrayDomain = damage && name == "tags" ? 410
                        : name == "allowedPostureIds" ? 402 : 405;
                    var labels = new JArray();
                    foreach (JToken item in identities)
                        labels.Add(item.Type == JTokenType.Integer && item.Value<int>() > 0
                            ? SourceKey(arrayDomain, item.Value<int>())
                            : item.DeepClone());
                    obj[name] = labels;
                    obj.Remove(LabelName(name));
                    continue;
                }
                if (name.EndsWith("Label", StringComparison.Ordinal)
                    && obj[name.Substring(0, name.Length - 5) + "Id"] != null)
                {
                    obj.Remove(name);
                    continue;
                }
                Restore(value);
            }
            return obj;
        }

        private static JToken Comparable(JToken token)
        {
            if (token is JArray array)
            {
                foreach (JToken child in array) Comparable(child);
                return array;
            }
            if (!(token is JObject obj)) return token;
            var names = new List<string>();
            foreach (JProperty property in obj.Properties()) names.Add(property.Name);
            foreach (string name in names)
            {
                if (name == "formulaId" || name == "formulaLabel" || name == "stableEventOrder"
                    || name == "requiredPartStateLabel"
                    || name == "cueLabel" || name == "abilityStableLabel")
                    obj.Remove(name);
                else
                    Comparable(obj[name]);
            }
            return obj;
        }

        private static int Domain(string name)
        {
            switch (name)
            {
                case "stableEventId": case "bladeTrailSourceEventId": case "sourceAnimationEventId": case "eventId":
                case "requestId": case "targetTimelineId": case "effectTimelineId": return 401;
                case "collisionProfileId": case "movementProfileId": return 402;
                case "traceId": case "traceProfileId": return 403;
                case "intentId": return 404;
                case "slotId": case "equipmentSlotId": case "equipSlot": case "mountedSlot": return 405;
                case "targetPolicyId": return 406;
                case "presentationProfileId": return 407;
                case "damageTypeId": return 409;
                case "analysisCategory": return 411;
                case "combatActionId": return 412;
                case "requiredPartStateId": case "startSocketId": case "endSocketId": return 400;
                default: return 0;
            }
        }

        private static string SourceKey(int domain, int id)
        {
            string key = CharacterContentIdentityAuthoringCatalog.GetSourceKey(domain, id);
            if (string.IsNullOrWhiteSpace(key)
                || string.Equals(key, id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                throw new FormatException("Missing source identity " + domain + ":" + id);
            return key;
        }

        private static string CatalogIdentity(
            string catalogRelativePath,
            string arrayName,
            string idName,
            string keyName,
            int id)
        {
            string path = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Assets", "MxFrameworkGenerated", catalogRelativePath.Replace('/', Path.DirectorySeparatorChar));
            JObject catalog = JObject.Parse(File.ReadAllText(path));
            IEnumerable<JObject> entries = catalog[arrayName] is JArray array
                ? array.Children<JObject>()
                : Array.Empty<JObject>();
            foreach (JObject entry in entries)
                if (entry.Value<int>(idName) == id)
                {
                    string key = entry.Value<string>(keyName);
                    if (!string.IsNullOrWhiteSpace(key)) return key;
                }
            throw new FormatException("Missing source identity " + catalogRelativePath + ":" + id);
        }

        private static string LabelName(string name)
        {
            if (name.EndsWith("Ids", StringComparison.Ordinal))
                return name.Substring(0, name.Length - 3) + "Labels";
            if (name.EndsWith("Id", StringComparison.Ordinal))
                return name.Substring(0, name.Length - 2) + "Label";
            return name + "Label";
        }
    }
}
