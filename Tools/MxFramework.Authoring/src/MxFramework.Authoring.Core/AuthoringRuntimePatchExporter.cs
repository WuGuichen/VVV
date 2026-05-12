using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Converts Authoring patch entries (PatchDocument/PatchEntry) into
    /// Runtime Config Patch v1 (mx.runtimeConfigPatch.v1) JSON text.
    ///
    /// This is a pure JSON DTO conversion layer. It does NOT reference
    /// MxFramework.Config.Runtime or any Unity assembly.
    /// The output JSON is parsed by Unity-side RuntimeConfigPatchJsonLoader.
    /// </summary>
    public static class AuthoringRuntimePatchExporter
    {
        private const string ExpectedFormat = "mx.runtimeConfigPatch.v1";
        private const int ModifierIdOffset = 100000;
        private const int ModifierIdMin = 200000;
        private const int ModifierIdMax = 299999;

        /// <summary>
        /// Result of an export attempt.
        /// </summary>
        public sealed class ExportResult
        {
            public bool Success { get; set; }
            public string Json { get; set; } = string.Empty;
            public List<ExportError> Errors { get; set; } = new List<ExportError>();
        }

        public sealed class ExportError
        {
            public string EntryId { get; set; } = string.Empty;
            public string Field { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        /// <summary>
        /// Export a list of Authoring <see cref="PatchDocument"/> entries
        /// to Runtime Config Patch v1 JSON text.
        /// </summary>
        /// <param name="patches">Authoring patch documents.</param>
        /// <param name="sourceId">Source identifier for the patch (e.g. packageId or "authoring_export").</param>
        /// <param name="layer">Target layer: "Patch" or "Mod".</param>
        /// <param name="packageId">Optional package ID; used to derive sourceId if empty.</param>
        /// <returns>Export result with JSON text or field-level errors.</returns>
        public static ExportResult Export(
            IReadOnlyList<PatchDocument> patches,
            string sourceId = "authoring_export",
            string layer = "Patch",
            string packageId = "")
        {
            var result = new ExportResult();

            if (string.IsNullOrEmpty(sourceId))
                sourceId = string.IsNullOrEmpty(packageId) ? "authoring_export" : packageId;

            var buffsList = new List<Dictionary<string, object>>();
            var modsList = new List<Dictionary<string, object>>();
            bool hasErrors = false;

            for (int i = 0; i < patches.Count; i++)
            {
                PatchDocument doc = patches[i];
                if (doc == null) continue;

                for (int j = 0; j < doc.Entries.Count; j++)
                {
                    PatchEntry entry = doc.Entries[j];
                    if (entry == null) continue;

                    int buffId;
                    if (!int.TryParse(entry.Id, out buffId) || buffId <= 0)
                    {
                        result.Errors.Add(new ExportError
                        {
                            EntryId = entry.Id ?? "?",
                            Field = "Id",
                            Message = "Buff ID must be a positive integer."
                        });
                        hasErrors = true;
                        continue;
                    }

                    string entryLayer = string.IsNullOrEmpty(entry.Layer) ? layer : entry.Layer;

                    if (entry.Operation == PatchOperation.Remove)
                    {
                        buffsList.Add(new Dictionary<string, object>
                        {
                            ["operation"] = "Remove",
                            ["id"] = buffId
                        });
                        continue;
                    }

                    // Only process DamageByAttr buffs for now
                    string buffType = entry.Fields.GetScalar("Type");
                    if (buffType != "DamageByAttr")
                        continue;

                    ExportBuff(entry, buffId, entryLayer, sourceId, result, buffsList, modsList, ref hasErrors);
                }
            }

            if (hasErrors)
            {
                result.Success = false;
                return result;
            }

            // Build the runtime patch v1 JSON
            var root = new Dictionary<string, object>
            {
                ["format"] = ExpectedFormat,
                ["sourceId"] = sourceId,
                ["layer"] = layer,
                ["modifiers"] = modsList,
                ["buffs"] = buffsList
            };

            result.Json = SerializeJson(root);
            result.Success = true;
            return result;
        }

        private static void ExportBuff(
            PatchEntry entry,
            int buffId,
            string entryLayer,
            string sourceId,
            ExportResult result,
            List<Dictionary<string, object>> buffsList,
            List<Dictionary<string, object>> modsList,
            ref bool hasErrors)
        {
            // --- Buff fields ---

            // Name (localized key)
            string name = entry.Fields.GetScalar("Name");
            if (string.IsNullOrEmpty(name))
                name = "buff." + buffId + ".name";

            // Description (localized key)
            string desc = entry.Fields.GetScalar("Desc");
            if (string.IsNullOrEmpty(desc))
                desc = "buff." + buffId + ".desc";

            // Duration: Authoring is ms, Runtime is seconds
            string durationStr = entry.Fields.GetScalar("Duration");
            float durationSec;
            if (!float.TryParse(durationStr, out float durationMs) || durationMs <= 0)
            {
                result.Errors.Add(new ExportError
                {
                    EntryId = buffId.ToString(),
                    Field = "Duration",
                    Message = "Duration must be a positive number (milliseconds)."
                });
                hasErrors = true;
                return;
            }
            durationSec = durationMs / 1000f;

            // MaxLayers (AddNum)
            string addNumStr = entry.Fields.GetScalar("AddNum");
            int maxLayers = 1;
            if (!string.IsNullOrEmpty(addNumStr))
            {
                if (!int.TryParse(addNumStr, out maxLayers) || maxLayers <= 0)
                {
                    result.Errors.Add(new ExportError
                    {
                        EntryId = buffId.ToString(),
                        Field = "AddNum",
                        Message = "AddNum must be a positive integer."
                    });
                    hasErrors = true;
                    return;
                }
            }

            // Values: only pure numeric strings supported
            string valuesStr = entry.Fields.GetScalar("Values");
            int paramValue;
            if (string.IsNullOrEmpty(valuesStr) || !int.TryParse(valuesStr, out paramValue))
            {
                string actual = string.IsNullOrEmpty(valuesStr) ? "(empty)" : "'" + valuesStr + "'";
                result.Errors.Add(new ExportError
                {
                    EntryId = buffId.ToString(),
                    Field = "Values",
                    Message = "Values must be a pure numeric string, e.g. \"80\". Got " + actual + ". Formula-based values are not supported yet."
                });
                hasErrors = true;
                return;
            }

            // Modifier ID: derived from buffId
            int modifierId = buffId + ModifierIdOffset;
            if (modifierId < ModifierIdMin || modifierId > ModifierIdMax)
            {
                result.Errors.Add(new ExportError
                {
                    EntryId = buffId.ToString(),
                    Field = "Id",
                    Message = "Derived modifier ID " + modifierId + " is outside valid range [" + ModifierIdMin + "-" + ModifierIdMax + "]. "
                        + "Buff ID " + buffId + " produces modifier ID " + modifierId + " which exceeds the BasicModifierConfig range."
                });
                hasErrors = true;
                return;
            }

            // Generate modifier entry
            var modEntry = new Dictionary<string, object>
            {
                ["operation"] = "Upsert",
                ["id"] = modifierId,
                ["nameText"] = "modifier.buff." + buffId + ".name",
                ["descriptionText"] = "modifier.buff." + buffId + ".desc",
                ["paramIndex"] = 2, // Demo's AttrAttack
                ["parameters"] = new[] { paramValue }
            };
            modsList.Add(modEntry);

            // Generate buff entry
            var buffEntry = new Dictionary<string, object>
            {
                ["operation"] = "Upsert",
                ["id"] = buffId,
                ["nameText"] = name,
                ["descriptionText"] = desc,
                ["duration"] = durationSec,
                ["maxLayers"] = maxLayers,
                ["modifierId"] = modifierId
            };
            buffsList.Add(buffEntry);
        }

        private static string SerializeJson(Dictionary<string, object> root)
        {
            // Minimal JSON serializer that produces stable key-order output (for SVN diff).
            // Uses System.Text.Json if available (netstandard2.1), with ordered output.
            var sb = new StringBuilder();
            SerializeObject(sb, root, 0);
            return sb.ToString();
        }

        private static void SerializeObject(StringBuilder sb, Dictionary<string, object> obj, int indent)
        {
            string prefix = new string(' ', indent * 2);
            string inner = new string(' ', (indent + 1) * 2);

            sb.Append("{\n");
            bool first = true;
            foreach (var kv in obj)
            {
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append(inner).Append('"').Append(EscapeJson(kv.Key)).Append("\": ");
                SerializeValue(sb, kv.Value, indent + 1);
            }
            sb.Append('\n').Append(prefix).Append('}');
        }

        private static void SerializeValue(StringBuilder sb, object value, int indent)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string s)
            {
                sb.Append('"').Append(EscapeJson(s)).Append('"');
                return;
            }

            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }

            if (value is int i)
            {
                sb.Append(i);
                return;
            }

            if (value is float f)
            {
                sb.Append(f.ToString("0.0############", System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (value is double d)
            {
                sb.Append(d.ToString("0.0############", System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (value is System.Collections.IList list)
            {
                sb.Append('[');
                for (int j = 0; j < list.Count; j++)
                {
                    if (j > 0) sb.Append(", ");
                    SerializeValue(sb, list[j], indent);
                }
                sb.Append(']');
                return;
            }

            if (value is Dictionary<string, object> dict)
            {
                SerializeObject(sb, dict, indent);
                return;
            }

            sb.Append('"').Append(EscapeJson(value.ToString())).Append('"');
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
