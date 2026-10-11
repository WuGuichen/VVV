using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed partial class CharacterActionProjectileAuthoringStore
    {
        private static int ReadInt(JObject fields, string name, int fallback = 0)
        {
            JToken token = fields?[name];
            return token == null ? fallback : token.Value<int>();
        }

        private static long ReadLong(JObject fields, string name, long fallback = 0L)
        {
            JToken token = fields?[name];
            return token == null ? fallback : token.Value<long>();
        }

        private static bool ReadBool(JObject fields, string name, bool fallback)
        {
            JToken token = fields?[name];
            return token == null ? fallback : token.Value<bool>();
        }

        private static float ReadFloat(JObject fields, string name, float fallback = 0f)
        {
            JToken token = fields?[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.String
                && float.TryParse(token.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return token.Value<float>();
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            JToken token = fields?[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static string ReadHitEventString(JObject hitEvent, JObject legacy, string name)
        {
            string value = ReadString(hitEvent, name);
            return string.IsNullOrWhiteSpace(value) ? ReadString(legacy, name) : value;
        }

        private static CharacterActionHitBuffJsonDto[] ReadOnHitBuffs(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<CharacterActionHitBuffJsonDto>();

            var values = new List<CharacterActionHitBuffJsonDto>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                if (item == null)
                    continue;
                values.Add(new CharacterActionHitBuffJsonDto
                {
                    BuffId = ReadInt(item, "buffId"),
                });
            }

            return CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(values.ToArray());
        }

        private static string ReadDamageTags(JObject damage)
        {
            JArray tags = damage?["tags"] as JArray;
            if (tags != null && tags.Count > 0)
                return string.Join(",", ReadStringArray(tags));
            return string.Empty;
        }

        private static string[] ReadStringArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<string>();

            var values = new List<string>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                string value = array[i]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value.Trim());
            }

            values.Sort(StringComparer.Ordinal);
            return values.ToArray();
        }

        private static string[] ParseTagList(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
                return Array.Empty<string>();

            string[] parts = tags.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i];
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value.Trim());
            }

            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static bool FloatEquals(float left, float right)
        {
            return Math.Abs(left - right) <= 0.0001f;
        }

        private static float ReadMaxLaunchAngleTangent(JObject motion)
        {
            float tangent = ReadFloat(motion, "maxLaunchAngleTangent");
            if (tangent > 0f)
                return tangent;

            float degrees = ReadFloat(motion, "maxLaunchAngleDegrees");
            return degrees > 0f ? CalculateLaunchAngleTangent(degrees) : 0f;
        }

        private static float CalculateLaunchAngleTangent(float degrees)
        {
            return (float)Math.Tan(degrees * Math.PI / 180.0);
        }

        private static bool IsZero(float x, float y, float z)
        {
            return FloatEquals(x, 0f)
                && FloatEquals(y, 0f)
                && FloatEquals(z, 0f);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatProjectileName(string stableId)
        {
            if (string.Equals(stableId, "character_test.fireball.projectile", StringComparison.Ordinal))
                return "火球";
            if (string.Equals(stableId, "character_test.fireball_burst.projectile", StringComparison.Ordinal))
                return "爆裂火球";
            if (string.Equals(stableId, "character_test.flame_lance.projectile", StringComparison.Ordinal))
                return "火焰枪";
            if (string.Equals(stableId, "character_test.fireball_lob.projectile", StringComparison.Ordinal))
                return "抛射火球";
            if (string.Equals(stableId, "character_test.fireball_surge.projectile", StringComparison.Ordinal))
                return "加速火球";
            if (string.Equals(stableId, "character_test.fireball_return.projectile", StringComparison.Ordinal))
                return "折返火球";
            if (string.Equals(stableId, "character_test.fireball_sway.projectile", StringComparison.Ordinal))
                return "摆动火球";
            if (string.Equals(stableId, "character_test.fireball_homing.projectile", StringComparison.Ordinal))
                return "追踪火球";
            if (string.Equals(stableId, "character_test.fireball_bezier.projectile", StringComparison.Ordinal))
                return "贝塞尔火球";

            if (string.IsNullOrWhiteSpace(stableId))
                return "未命名投射物";
            string value = stableId.Replace("character_test.", string.Empty).Replace(".projectile", string.Empty);
            return value.Replace('_', ' ');
        }
    }
}
