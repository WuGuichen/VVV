using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using Newtonsoft.Json.Linq;

namespace MxFramework.Combat.GameplayBridge
{
    public static class CombatGameplayAuraBuffDefinitionJsonImporter
    {
        public const string CurrentSchemaVersion = "1";

        public static CombatGameplayAuraBuffDefinitionRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new CombatGameplayAuraBuffDefinitionRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static CombatGameplayAuraBuffDefinitionRegistry Import(string json, string sourcePath = "")
        {
            if (string.IsNullOrWhiteSpace(json))
                return new CombatGameplayAuraBuffDefinitionRegistry();

            JObject root = JObject.Parse(json);
            ValidateSchema(root, sourcePath);
            JArray definitions = root["definitions"] as JArray;
            if (definitions == null || definitions.Count == 0)
                return new CombatGameplayAuraBuffDefinitionRegistry();

            var parsed = new List<CombatGameplayAuraBuffDefinition>(definitions.Count);
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
                        "Invalid combat gameplay aura buff definition at " + (sourcePath ?? string.Empty) + "#" + i + ": " + ex.Message,
                        ex);
                }
            }

            try
            {
                return new CombatGameplayAuraBuffDefinitionRegistry(parsed.ToArray());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Invalid combat gameplay aura buff definition catalog at " + (sourcePath ?? string.Empty) + ": " + ex.Message,
                    ex);
            }
        }

        private static CombatGameplayAuraBuffDefinition ReadDefinition(JObject fields)
        {
            return new CombatGameplayAuraBuffDefinition(
                ReadInt(fields, "auraId"),
                ReadInt(fields, "buffId"),
                ReadFix64(fields, "radiusMeters"),
                new CombatPhysicsLayerMask(ReadLayerMask(fields, "layerMask")),
                ReadRelationFilter(fields, "relationFilter"),
                ReadBool(fields, "includeCaster", true));
        }

        private static void ValidateSchema(JObject root, string sourcePath)
        {
            string schemaVersion = root["schemaVersion"]?.Value<string>() ?? string.Empty;
            if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsupported combat gameplay aura buff schemaVersion at " + (sourcePath ?? string.Empty) + ": " + schemaVersion + ".");
            }
        }

        private static int ReadLayerMask(JObject fields, string name)
        {
            int layerMask = ReadInt(fields, name);
            if (layerMask <= 0)
                throw new ArgumentOutOfRangeException(name, "Combat gameplay aura layerMask must be positive.");
            return layerMask;
        }

        private static GameplayTargetRelationFilter ReadRelationFilter(JObject fields, string name)
        {
            string value = ReadString(fields, name, "Any");
            if (!Enum.TryParse(value, ignoreCase: false, out GameplayTargetRelationFilter filter)
                || !Enum.IsDefined(typeof(GameplayTargetRelationFilter), filter))
            {
                throw new ArgumentOutOfRangeException(name, "Unsupported gameplay target relation filter: " + value + ".");
            }

            return filter;
        }

        private static Fix64 ReadFix64(JObject fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return Fix64.Zero;

            JToken token = fields[name];
            if (token == null)
                return Fix64.Zero;

            decimal value = token.Value<decimal>();
            decimal raw = value * Fix64.Scale;
            return Fix64.FromRaw(checked((long)raw));
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


