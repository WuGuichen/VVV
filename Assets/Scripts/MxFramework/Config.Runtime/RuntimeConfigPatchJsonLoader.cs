using System;
using System.Collections.Generic;
using MxFramework.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Parsed result from a runtime config patch JSON file.
    /// Contains typed patch entries for both Buffs and Modifiers.
    /// </summary>
    public sealed class RuntimeConfigPatchBundle
    {
        public RuntimeConfigPatchBundle(
            IReadOnlyList<ConfigPatchEntry<BasicModifierConfig>> modifierPatches,
            IReadOnlyList<ConfigPatchEntry<BasicBuffConfig>> buffPatches,
            string sourceId,
            ConfigLayerKind layer)
        {
            ModifierPatches = modifierPatches ?? Array.Empty<ConfigPatchEntry<BasicModifierConfig>>();
            BuffPatches = buffPatches ?? Array.Empty<ConfigPatchEntry<BasicBuffConfig>>();
            SourceId = sourceId ?? string.Empty;
            Layer = layer;
        }

        public IReadOnlyList<ConfigPatchEntry<BasicModifierConfig>> ModifierPatches { get; }
        public IReadOnlyList<ConfigPatchEntry<BasicBuffConfig>> BuffPatches { get; }
        public string SourceId { get; }
        public ConfigLayerKind Layer { get; }
    }

    /// <summary>
    /// JSON loader for runtime config patch files (format: mx.runtimeConfigPatch.v1).
    /// Uses Newtonsoft.Json for parsing. Does not depend on UnityEngine or UnityEditor.
    /// </summary>
    public static class RuntimeConfigPatchJsonLoader
    {
        private const string ExpectedFormat = "mx.runtimeConfigPatch.v1";

        /// <summary>
        /// Load and parse a runtime config patch from JSON text.
        /// </summary>
        /// <param name="json">The raw JSON text.</param>
        /// <returns>A parsed bundle with typed patch entries.</returns>
        /// <exception cref="RuntimeConfigPatchParseException">Thrown when parsing fails or format is invalid.</exception>
        public static RuntimeConfigPatchBundle Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new RuntimeConfigPatchParseException("JSON input is null or empty.");

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonReaderException ex)
            {
                throw new RuntimeConfigPatchParseException($"Invalid JSON: {ex.Message}", ex);
            }

            // Validate format
            JToken formatToken = root["format"];
            if (formatToken == null)
                throw new RuntimeConfigPatchParseException("Missing 'format' field. Expected 'mx.runtimeConfigPatch.v1'.");

            string format = formatToken.ToString();
            if (!string.Equals(format, ExpectedFormat, StringComparison.Ordinal))
                throw new RuntimeConfigPatchParseException(
                    $"Unsupported format '{format}'. Expected '{ExpectedFormat}'.");

            // Parse sourceId
            string sourceId = root["sourceId"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new RuntimeConfigPatchParseException("Missing or empty 'sourceId' field.");

            // Parse layer
            ConfigLayerKind layer = ConfigLayerKind.Patch;
            JToken layerToken = root["layer"];
            if (layerToken != null)
            {
                string layerStr = layerToken.ToString();
                if (!Enum.TryParse(layerStr, ignoreCase: true, out layer))
                    throw new RuntimeConfigPatchParseException($"Unsupported layer '{layerStr}'. Expected 'Patch' or 'Mod'.");
            }

            // Parse modifier patches
            var modifierPatches = new List<ConfigPatchEntry<BasicModifierConfig>>();
            JArray modifiersArray = root["modifiers"] as JArray;
            if (modifiersArray != null)
            {
                foreach (JToken item in modifiersArray)
                {
                    ConfigPatchEntry<BasicModifierConfig> patch = ParseModifierPatch(item, sourceId, layer);
                    modifierPatches.Add(patch);
                }
            }

            // Parse buff patches
            var buffPatches = new List<ConfigPatchEntry<BasicBuffConfig>>();
            JArray buffsArray = root["buffs"] as JArray;
            if (buffsArray != null)
            {
                foreach (JToken item in buffsArray)
                {
                    ConfigPatchEntry<BasicBuffConfig> patch = ParseBuffPatch(item, sourceId, layer);
                    buffPatches.Add(patch);
                }
            }

            return new RuntimeConfigPatchBundle(modifierPatches, buffPatches, sourceId, layer);
        }

        private static ConfigPatchEntry<BasicModifierConfig> ParseModifierPatch(
            JToken item, string sourceId, ConfigLayerKind layer)
        {
            ConfigPatchOperation operation = ParseOperation(item);
            int id = item["id"]?.Value<int>() ?? 0;

            if (operation == ConfigPatchOperation.Remove)
                return ConfigPatchEntry<BasicModifierConfig>.Remove(id, layer, sourceId);

            return ConfigPatchEntry<BasicModifierConfig>.Upsert(new BasicModifierConfig(
                id: id,
                nameText: new LocalizedTextKey(item["nameText"]?.ToString() ?? string.Empty),
                descriptionText: new LocalizedTextKey(item["descriptionText"]?.ToString() ?? string.Empty),
                paramIndex: item["paramIndex"]?.Value<int>() ?? 0,
                parameters: item["parameters"]?.ToObject<int[]>() ?? new int[0]),
                layer, sourceId);
        }

        private static ConfigPatchEntry<BasicBuffConfig> ParseBuffPatch(
            JToken item, string sourceId, ConfigLayerKind layer)
        {
            ConfigPatchOperation operation = ParseOperation(item);
            int id = item["id"]?.Value<int>() ?? 0;

            if (operation == ConfigPatchOperation.Remove)
                return ConfigPatchEntry<BasicBuffConfig>.Remove(id, layer, sourceId);

            return ConfigPatchEntry<BasicBuffConfig>.Upsert(new BasicBuffConfig(
                id: id,
                nameText: new LocalizedTextKey(item["nameText"]?.ToString() ?? string.Empty),
                descriptionText: new LocalizedTextKey(item["descriptionText"]?.ToString() ?? string.Empty),
                duration: item["duration"]?.Value<float>() ?? 0f,
                maxLayers: item["maxLayers"]?.Value<int>() ?? 1,
                isPermanent: item["isPermanent"]?.Value<bool>() ?? false,
                modifierId: item["modifierId"]?.Value<int>() ?? 0),
                layer, sourceId);
        }

        private static ConfigPatchOperation ParseOperation(JToken item)
        {
            string opStr = item["operation"]?.ToString() ?? "Upsert";
            if (Enum.TryParse(opStr, ignoreCase: true, out ConfigPatchOperation result))
                return result;

            // Default to Upsert for unknown operations
            return ConfigPatchOperation.Upsert;
        }
    }

    /// <summary>
    /// Exception thrown when a runtime config patch file cannot be parsed.
    /// </summary>
    public sealed class RuntimeConfigPatchParseException : Exception
    {
        public RuntimeConfigPatchParseException(string message) : base(message) { }
        public RuntimeConfigPatchParseException(string message, Exception inner) : base(message, inner) { }
    }
}
