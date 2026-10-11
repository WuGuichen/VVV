using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MxFramework.Gameplay
{
    public static class GameplayBuffDefinitionJsonImporter
    {
        public const string CurrentSchemaVersion = "1";

        public static GameplayBuffDefinitionRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new GameplayBuffDefinitionRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static GameplayBuffDefinitionRegistry Import(string json, string sourcePath = "")
        {
            if (string.IsNullOrWhiteSpace(json))
                return new GameplayBuffDefinitionRegistry();

            JObject root = JObject.Parse(json);
            ValidateSchema(root, sourcePath);
            JArray definitions = root["definitions"] as JArray;
            if (definitions == null || definitions.Count == 0)
                return new GameplayBuffDefinitionRegistry();

            var parsed = new List<GameplayBuffDefinition>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                if (!(definitions[i] is JObject fields))
                    continue;

                try
                {
                    parsed.Add(ReadDefinition(fields));
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    throw new InvalidOperationException(
                        "Invalid buff definition at " + (sourcePath ?? string.Empty) + "#" + i + ": " + ex.Message,
                        ex);
                }
            }

            try
            {
                return new GameplayBuffDefinitionRegistry(parsed.ToArray());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Invalid buff definition catalog at " + (sourcePath ?? string.Empty) + ": " + ex.Message,
                    ex);
            }
        }

        private static GameplayBuffDefinition ReadDefinition(JObject fields)
        {
            return new GameplayBuffDefinition(
                ReadInt(fields, "buffId"),
                ReadString(fields, "stableId"),
                ReadLong(fields, "defaultDurationFrames"),
                ReadInt(fields, "maxStackCount", 1),
                ReadStackPolicy(fields, "stackPolicy"),
                ReadEffects(fields["effects"] as JArray),
                ReadInt(fields, "applyStackCount", 1),
                ReadBool(fields, "isPermanent"));
        }

        private static void ValidateSchema(JObject root, string sourcePath)
        {
            string schemaVersion = root["schemaVersion"]?.Value<string>() ?? string.Empty;
            if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsupported buff definition schemaVersion at " + (sourcePath ?? string.Empty) + ": " + schemaVersion + ".");
            }
        }

        private static GameplayBuffStackPolicy ReadStackPolicy(JObject fields, string name)
        {
            string value = ReadString(fields, name, "RefreshDuration");
            if (!Enum.TryParse(value, ignoreCase: false, out GameplayBuffStackPolicy policy)
                || !Enum.IsDefined(typeof(GameplayBuffStackPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(name, "Unsupported gameplay buff stack policy: " + value + ".");
            }

            return policy;
        }

        private static GameplayBuffEffectDefinition[] ReadEffects(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<GameplayBuffEffectDefinition>();

            var effects = new List<GameplayBuffEffectDefinition>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject fields))
                    continue;

                string kind = ReadString(fields, "kind");
                int effectId = ReadInt(fields, "effectId");
                if (string.Equals(kind, "AttributeModifier", StringComparison.Ordinal))
                {
                    effects.Add(GameplayBuffEffectDefinition.AttributeModifier(
                        effectId,
                        ReadInt(fields, "attributeId"),
                        ReadInt(fields, "addValue")));
                }
                else if (string.Equals(kind, "Status", StringComparison.Ordinal))
                {
                    effects.Add(GameplayBuffEffectDefinition.Status(
                        effectId,
                        new GameplayStatusId(ReadInt(fields, "statusId"))));
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported gameplay buff effect kind: " + kind + ".");
                }
            }

            return effects.ToArray();
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;

            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name, int fallback = 0)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<int>();
        }

        private static long ReadLong(JObject fields, string name, long fallback = 0L)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<long>();
        }

        private static bool ReadBool(JObject fields, string name, bool fallback = false)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<bool>();
        }
    }
}
