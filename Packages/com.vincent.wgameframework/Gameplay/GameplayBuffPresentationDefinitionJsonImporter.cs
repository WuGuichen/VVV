using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MxFramework.Gameplay
{
    public static class GameplayBuffPresentationDefinitionJsonImporter
    {
        public const string CurrentSchemaVersion = "1";

        public static GameplayBuffPresentationDefinitionRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new GameplayBuffPresentationDefinitionRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static GameplayBuffPresentationDefinitionRegistry Import(string json, string sourcePath = "")
        {
            if (string.IsNullOrWhiteSpace(json))
                return new GameplayBuffPresentationDefinitionRegistry();

            JObject root = JObject.Parse(json);
            ValidateSchema(root, sourcePath);
            JArray array = root["definitions"] as JArray;
            if (array == null || array.Count == 0)
                return new GameplayBuffPresentationDefinitionRegistry();

            var parsed = new List<GameplayBuffPresentationDefinition>();
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject fields))
                    continue;

                try
                {
                    parsed.Add(ReadDefinition(fields));
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    throw new InvalidOperationException(
                        "Invalid buff presentation definition at "
                        + (sourcePath ?? string.Empty)
                        + "#"
                        + i
                        + ": "
                        + ex.Message,
                        ex);
                }
            }

            try
            {
                return new GameplayBuffPresentationDefinitionRegistry(parsed.ToArray());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Invalid buff presentation definition catalog at "
                    + (sourcePath ?? string.Empty)
                    + ": "
                    + ex.Message,
                    ex);
            }
        }

        private static GameplayBuffPresentationDefinition ReadDefinition(JObject fields)
        {
            return new GameplayBuffPresentationDefinition(
                ReadInt(fields, "buffId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "nameKey"),
                ReadString(fields, "shortName"),
                ReadString(fields, "description"),
                ReadString(fields, "descriptionMode", "Manual"),
                ReadString(fields, "iconResourceKey"),
                ReadPolarity(fields, "polarity"),
                ReadInt(fields, "priority"),
                ReadBool(fields, "showOnPlayerHud", fallback: true),
                ReadBool(fields, "showOnTargetHud", fallback: true),
                ReadBool(fields, "showOnHeadPad", fallback: true),
                ReadString(fields, "effectSummary"));
        }

        private static void ValidateSchema(JObject root, string sourcePath)
        {
            string schemaVersion = root["schemaVersion"]?.Value<string>() ?? string.Empty;
            if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsupported buff presentation definition schemaVersion at "
                    + (sourcePath ?? string.Empty)
                    + ": "
                    + schemaVersion
                    + ".");
            }
        }

        private static GameplayBuffPresentationPolarity ReadPolarity(JObject fields, string name)
        {
            string value = ReadString(fields, name, "Neutral");
            if (Enum.TryParse(value, ignoreCase: false, out GameplayBuffPresentationPolarity polarity)
                && Enum.IsDefined(typeof(GameplayBuffPresentationPolarity), polarity))
            {
                return polarity;
            }

            throw new ArgumentOutOfRangeException(name, "Unsupported buff presentation polarity: " + value + ".");
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

        private static bool ReadBool(JObject fields, string name, bool fallback = false)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<bool>();
        }
    }
}
