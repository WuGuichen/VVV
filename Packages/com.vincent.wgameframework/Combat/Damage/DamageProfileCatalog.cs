using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Core.Math;
using Newtonsoft.Json.Linq;

namespace MxFramework.Combat.Damage
{
    public sealed class MemoryDamageProfileCatalog : IDamageProfileCatalog
    {
        private readonly Dictionary<int, DamageProfileConfig> _profiles =
            new Dictionary<int, DamageProfileConfig>();

        public MemoryDamageProfileCatalog(params DamageProfileConfig[] profiles)
        {
            Register(profiles);
        }

        public int Count => _profiles.Count;

        public void Register(params DamageProfileConfig[] profiles)
        {
            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Length; i++)
            {
                DamageProfileConfig profile = profiles[i];
                if (profile == null || profile.DamageProfileId <= 0)
                    throw new ArgumentException("Damage profile requires an authored positive ID.", nameof(profiles));
                if (_profiles.ContainsKey(profile.DamageProfileId))
                    throw new ArgumentException("Duplicate damage profile ID.", nameof(profiles));
                _profiles.Add(profile.DamageProfileId, profile);
            }
        }

        public bool TryGet(int damageProfileId, out DamageProfileConfig profile)
        {
            if (damageProfileId <= 0)
            {
                profile = null;
                return false;
            }

            return _profiles.TryGetValue(damageProfileId, out profile);
        }

        public DamageProfileConfig[] CreateSnapshot()
        {
            var output = new DamageProfileConfig[_profiles.Count];
            int index = 0;
            foreach (DamageProfileConfig profile in _profiles.Values)
                output[index++] = profile;
            Array.Sort(output, CompareProfiles);
            return output;
        }

        private static int CompareProfiles(DamageProfileConfig left, DamageProfileConfig right)
        {
            return (left?.DamageProfileId ?? 0).CompareTo(right?.DamageProfileId ?? 0);
        }
    }

    public static class DamageProfileJsonImporter
    {
        public const string CurrentSchemaVersion = "2";

        public static MemoryDamageProfileCatalog ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new MemoryDamageProfileCatalog();

            return Import(File.ReadAllText(path), path);
        }

        public static MemoryDamageProfileCatalog Import(string json, string sourcePath = "")
        {
            if (string.IsNullOrWhiteSpace(json))
                return new MemoryDamageProfileCatalog();

            JObject root = JObject.Parse(json);
            JArray profiles = root["profiles"] as JArray;
            if (profiles == null || profiles.Count == 0)
                return new MemoryDamageProfileCatalog();

            ReadSoftDefenseDefaults(root, out int defenseReference, out float defenseWeight);
            var parsed = new List<DamageProfileConfig>(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] is JObject item)
                    parsed.Add(ReadProfile(item, sourcePath, i, defenseReference, defenseWeight));
            }

            return new MemoryDamageProfileCatalog(parsed.ToArray());
        }

        public static DamageProfileCatalogRow[] CreateCatalogRows(IDamageProfileCatalog catalog)
        {
            DamageProfileConfig[] profiles = catalog?.CreateSnapshot() ?? Array.Empty<DamageProfileConfig>();
            var rows = new DamageProfileCatalogRow[profiles.Length];
            for (int i = 0; i < profiles.Length; i++)
                rows[i] = new DamageProfileCatalogRow(profiles[i]);
            return rows;
        }

        private static DamageProfileConfig ReadProfile(
            JObject fields,
            string sourcePath,
            int index,
            int defenseReference,
            float defenseWeight)
        {
            return new DamageProfileConfig(
                ReadDefinitionId(fields, "damageProfileId"),
                stableId: ReadString(fields, "stableId"),
                displayName: ReadString(fields, "displayName"),
                damageTypeId: ReadDefinitionId(fields, "damageTypeId"),
                formulaId: ReadDefinitionId(fields, "formulaId", DamageFormulaIds.Flat),
                baseDamage: ReadInt(fields, "baseDamage"),
                sourceAttributeScales: ReadAttributeScales(fields["sourceAttributeScales"] as JArray),
                targetAttributeScales: ReadAttributeScales(fields["targetAttributeScales"] as JArray),
                damageMultiplier: ReadFloat(fields, "damageMultiplier", 1f),
                defenseReference: defenseReference,
                defenseWeight: defenseWeight,
                minDamage: ReadInt(fields, "minDamage"),
                maxDamage: ReadInt(fields, "maxDamage"),
                staggerFrames: ReadInt(fields, "staggerFrames"),
                impactForce: ReadInt(fields, "impactForce"),
                knockback: ReadVector(fields["knockback"] as JObject),
                impactPower: ReadInt(fields, "impactPower"),
                launchPower: ReadInt(fields, "launchPower"),
                guardPressure: ReadInt(fields, "guardPressure"),
                presentationProfileId: ReadDefinitionId(fields, "presentationProfileId"),
                tags: ReadIdArray(fields["tags"] as JArray),
                analysisCategory: ReadDefinitionId(fields, "analysisCategory"),
                damageComponents: ReadComponents(fields["damageComponents"] as JArray));
        }

        private static void ReadSoftDefenseDefaults(JObject root, out int defenseReference, out float defenseWeight)
        {
            defenseReference = 50;
            defenseWeight = 0.6f;

            JObject formulaDefaults = root["formulaDefaults"] as JObject;
            JObject softDefense = formulaDefaults?["ct.soft_defense_v1"] as JObject;
            if (softDefense == null)
                return;

            defenseReference = ReadInt(softDefense, "defenseReference", defenseReference);
            if (defenseReference <= 0)
                defenseReference = 50;

            defenseWeight = ReadFloat(softDefense, "defenseWeight", defenseWeight);
            if (defenseWeight < 0f)
                defenseWeight = 0f;
            else if (defenseWeight > 1f)
                defenseWeight = 1f;
        }

        private static DamageAttributeScale[] ReadAttributeScales(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<DamageAttributeScale>();

            var values = new List<DamageAttributeScale>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject item))
                    continue;
                values.Add(new DamageAttributeScale(
                    ReadInt(item, "attributeId"),
                    ReadFloat(item, "scale")));
            }

            return values.ToArray();
        }

        private static DamageComponentConfig[] ReadComponents(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<DamageComponentConfig>();

            var values = new List<DamageComponentConfig>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject item))
                    continue;
                values.Add(new DamageComponentConfig(
                    ReadDefinitionId(item, "componentId"),
                    ReadInt(item, "targetAttributeId"),
                    ReadInt(item, "baseValue")));
            }

            return values.ToArray();
        }

        private static int[] ReadIdArray(JArray array)
        {
            if (array == null || array.Count == 0) return Array.Empty<int>();
            var values = new int[array.Count];
            var seen = new HashSet<int>();
            for (int i = 0; i < values.Length; i++)
            {
                if (array[i].Type != JTokenType.Integer || (values[i] = array[i].Value<int>()) <= 0 || !seen.Add(values[i]))
                    throw new ArgumentException("Damage tags require unique authored positive IDs.");
            }
            return values;
        }

        private static int ReadDefinitionId(JObject fields, string name, int fallback = 0)
        {
            JToken token = fields?[name];
            if (token == null) return fallback;
            if (token.Type != JTokenType.Integer) throw new ArgumentException("Damage definition ID must be numeric: " + name);
            int value = token.Value<int>();
            if (value < 0) throw new ArgumentException("Damage definition ID cannot be negative: " + name);
            return value;
        }

        private static FixVector3 ReadVector(JObject fields)
        {
            if (fields == null)
                return FixVector3.Zero;

            return new FixVector3(
                ToFix64(ReadFloat(fields, "x")),
                ToFix64(ReadFloat(fields, "y")),
                ToFix64(ReadFloat(fields, "z")));
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;
            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name)
        {
            return ReadInt(fields, name, 0);
        }

        private static int ReadInt(JObject fields, string name, int fallback)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;
            JToken token = fields[name];
            return token == null ? fallback : token.Value<int>();
        }

        private static float ReadFloat(JObject fields, string name)
        {
            return ReadFloat(fields, name, 0f);
        }

        private static float ReadFloat(JObject fields, string name, float fallback)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;
            JToken token = fields[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.String
                && float.TryParse(token.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;
            return token.Value<float>();
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }
}


