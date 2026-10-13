using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterAction
{
    public static partial class CharacterActionJsonImporter
    {
        private static JObject ParseRoot(
            string json,
            string sourcePath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                AddError(diagnostics, sourcePath, "$", CharacterActionDiagnosticCodes.JsonInvalidDocument, "JSON document is empty.");
                return null;
            }

            try
            {
                JObject root = JObject.Parse(json);
                return root;
            }
            catch (JsonException)
            {
                AddError(diagnostics, sourcePath, "$", CharacterActionDiagnosticCodes.JsonInvalidDocument, "JSON document is malformed.");
                return null;
            }
        }

        private static void CheckIdentity(
            int id,
            string stableId,
            string sourcePath,
            string jsonPath,
            Dictionary<int, string> ids,
            Dictionary<string, string> stableIds,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (id <= 0)
            {
                AddError(diagnostics, sourcePath, jsonPath + ".id", CharacterActionDiagnosticCodes.JsonInvalidConfig, "Numeric id must be positive.");
            }
            else if (ids.ContainsKey(id))
            {
                AddError(diagnostics, sourcePath, jsonPath + ".id", CharacterActionDiagnosticCodes.JsonDuplicateId, "Duplicate numeric id " + id + ".");
            }
            else
            {
                ids.Add(id, jsonPath);
            }

            if (string.IsNullOrWhiteSpace(stableId))
            {
                AddError(diagnostics, sourcePath, jsonPath + ".stableId", CharacterActionDiagnosticCodes.JsonInvalidConfig, "Stable id must be non-empty.");
            }
            else if (stableIds.ContainsKey(stableId))
            {
                AddError(diagnostics, sourcePath, jsonPath + ".stableId", CharacterActionDiagnosticCodes.JsonDuplicateStableId, "Duplicate stable id '" + stableId + "'.");
            }
            else
            {
                stableIds.Add(stableId, jsonPath);
            }
        }

        private static CharacterActionId ReadActionId(JObject fields, string name)
        {
            return new CharacterActionId(ReadInt(fields, name));
        }

        private static CharacterActionId[] ReadActionIdArray(JArray array)
        {
            if (array == null)
                return Array.Empty<CharacterActionId>();

            var result = new CharacterActionId[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                int value = array[i]?.Type == JTokenType.Integer
                    ? array[i].Value<int>()
                    : 0;
                result[i] = new CharacterActionId(value);
            }
            return result;
        }

        private static void CheckActionIdentity(
            int actionId,
            string sourcePath,
            string jsonPath,
            Dictionary<int, string> actionIds,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (actionId <= 0)
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    jsonPath + ".actionId",
                    CharacterActionDiagnosticCodes.JsonInvalidConfig,
                    "Character action id must be positive.");
            }
            else if (actionIds.ContainsKey(actionId))
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    jsonPath + ".actionId",
                    CharacterActionDiagnosticCodes.JsonDuplicateId,
                    "Duplicate character action id " + actionId + ".");
            }
            else
            {
                actionIds.Add(actionId, jsonPath);
            }
        }

        private static bool TryReadEnum<TEnum>(
            JObject fields,
            string name,
            string sourcePath,
            string jsonPath,
            TEnum fallback,
            List<CharacterActionJsonDiagnostic> diagnostics,
            out TEnum value)
            where TEnum : struct
        {
            value = fallback;
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return true;

            string text = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            if (!Enum.TryParse(text, ignoreCase: true, out TEnum parsed)
                || !Enum.IsDefined(typeof(TEnum), parsed))
            {
                AddError(diagnostics, sourcePath, jsonPath, CharacterActionDiagnosticCodes.JsonInvalidEnum, "Enum value is invalid.");
                return false;
            }

            value = parsed;
            return true;
        }

        private static bool TryReadNullableEnum<TEnum>(
            JObject fields,
            string name,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics,
            out TEnum? value)
            where TEnum : struct
        {
            value = null;
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return true;

            if (token.Type == JTokenType.String && string.IsNullOrWhiteSpace(token.Value<string>()))
                return true;

            if (!TryReadEnum(fields, name, sourcePath, jsonPath, default(TEnum), diagnostics, out TEnum parsed))
                return false;

            value = parsed;
            return true;
        }

        private static string ReadString(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
        }

        private static string[] ReadStringArray(JArray array)
        {
            if (array == null)
                return Array.Empty<string>();

            var results = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
                results[i] = array[i] == null || array[i].Type == JTokenType.Null ? string.Empty : array[i].ToString();
            return results;
        }

        private static int ReadInt(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return 0;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();
            return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }

        private static long ReadLong(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return 0L;
            if (token.Type == JTokenType.Integer)
                return token.Value<long>();
            return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : 0L;
        }

        private static int? ReadNullableInt(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();
            return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }

        private static float ReadFloat(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            return ReadFloat(token, 0f);
        }

        private static float ReadFloat(JToken token, float fallback)
        {
            if (token == null || token.Type == JTokenType.Null)
                return fallback;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<float>();
            return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;
        }

        private static bool ReadBool(JObject fields, string name, bool fallback = false)
        {
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return fallback;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();
            return bool.TryParse(token.ToString(), out bool value) ? value : fallback;
        }

        private static bool? ReadNullableBool(JObject fields, string name)
        {
            JToken token = fields != null ? fields[name] : null;
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();
            return bool.TryParse(token.ToString(), out bool value) ? value : (bool?)null;
        }

        private static void Sort(CharacterActionSetConfig[] actionSets)
        {
            Array.Sort(actionSets, CompareActionSets);
        }

        private static void Sort(CharacterActionConfig[] actions)
        {
            Array.Sort(actions, CompareActions);
        }

        private static void Sort(CharacterReactionProfile[] reactionProfiles)
        {
            Array.Sort(reactionProfiles, CompareReactionProfiles);
        }

        private static void Sort(CharacterLandingProfile[] landingProfiles)
        {
            Array.Sort(landingProfiles, CompareLandingProfiles);
        }

        private static int CompareActionSets(CharacterActionSetConfig left, CharacterActionSetConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int compare = string.CompareOrdinal(left.StableId, right.StableId);
            return compare != 0 ? compare : left.Id.CompareTo(right.Id);
        }

        private static int CompareActions(CharacterActionConfig left, CharacterActionConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return left.ActionId.CompareTo(right.ActionId);
        }

        private static int CompareReactionProfiles(CharacterReactionProfile left, CharacterReactionProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.StableId, right.StableId);
        }

        private static int CompareLandingProfiles(CharacterLandingProfile left, CharacterLandingProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.StableId, right.StableId);
        }

        private static int CompareDiagnostics(CharacterActionJsonDiagnostic left, CharacterActionJsonDiagnostic right)
        {
            int compare = string.CompareOrdinal(left.SourcePath, right.SourcePath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.JsonPath, right.JsonPath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.Code, right.Code);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Message, right.Message);
        }

        private static bool IsConfigException(Exception ex)
        {
            return ex is ArgumentException || ex is InvalidOperationException || ex is JsonException || ex is FormatException;
        }

        private static void AddError(
            List<CharacterActionJsonDiagnostic> diagnostics,
            string sourcePath,
            string jsonPath,
            string code,
            string message)
        {
            diagnostics.Add(new CharacterActionJsonDiagnostic(
                sourcePath,
                jsonPath,
                code,
                CharacterActionDiagnosticSeverity.Error,
                message));
        }

        private static void AddWarning(
            List<CharacterActionJsonDiagnostic> diagnostics,
            string sourcePath,
            string jsonPath,
            string code,
            string message)
        {
            diagnostics.Add(new CharacterActionJsonDiagnostic(
                sourcePath,
                jsonPath,
                code,
                CharacterActionDiagnosticSeverity.Warning,
                message));
        }
    }
}
