using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MxFramework.Config
{
    public sealed class ConfigSourceEntry
    {
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

        public ConfigSourceEntry(
            ConfigSchema schema,
            string keyField = "Id",
            string sourcePath = "",
            string contentHash = "")
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            KeyField = string.IsNullOrWhiteSpace(keyField) ? "Id" : keyField;
            SourcePath = sourcePath ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
        }

        public ConfigSchema Schema { get; }
        public string KeyField { get; }
        public string SourcePath { get; }
        public string ContentHash { get; }
        public int KeyCount => _keys.Count;
        public string SourceId => BuildSourceId(Schema.StructureKind, Schema.TableName);

        public ConfigSourceEntry AddKey(object key)
        {
            if (TryNormalizeKey(key, out string normalized))
                _keys.Add(normalized);
            return this;
        }

        public ConfigSourceEntry AddKeys(IEnumerable<object> keys)
        {
            if (keys == null)
                return this;

            foreach (object key in keys)
                AddKey(key);
            return this;
        }

        public bool ContainsKey(object key)
        {
            return TryNormalizeKey(key, out string normalized) && _keys.Contains(normalized);
        }

        public static string BuildSourceId(ConfigStructureKind structureKind, string schemaName)
        {
            return structureKind + ":" + (schemaName ?? string.Empty);
        }

        internal static bool TryNormalizeKey(object key, out string normalized)
        {
            normalized = string.Empty;
            if (key == null)
                return false;

            switch (key)
            {
                case string text:
                    normalized = text;
                    return !string.IsNullOrWhiteSpace(normalized);
                case LocalizedTextKey localizedTextKey:
                    normalized = localizedTextKey.Value;
                    return localizedTextKey.IsValid;
                case IFormattable formattable:
                    normalized = formattable.ToString(null, CultureInfo.InvariantCulture);
                    return !string.IsNullOrWhiteSpace(normalized);
                default:
                    normalized = key.ToString();
                    return !string.IsNullOrWhiteSpace(normalized);
            }
        }
    }

    public sealed class ConfigSourceIndex
    {
        private readonly Dictionary<string, ConfigSourceEntry> _sources = new Dictionary<string, ConfigSourceEntry>(StringComparer.Ordinal);

        public IReadOnlyCollection<ConfigSourceEntry> Sources => _sources.Values;

        public bool Register(ConfigSourceEntry source, bool replace = false)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Schema.TableName))
                return false;

            if (_sources.ContainsKey(source.SourceId) && !replace)
                return false;

            _sources[source.SourceId] = source;
            return true;
        }

        public bool TryGet(ConfigStructureKind structureKind, string schemaName, out ConfigSourceEntry source)
        {
            return _sources.TryGetValue(ConfigSourceEntry.BuildSourceId(structureKind, schemaName), out source);
        }

        public ConfigTableValidationReport ValidateCrossSourceReferences<T>(IConfigTable<T> table) where T : IConfigData
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var report = new ConfigTableValidationReport();
            ConfigSchema schema = table.Schema;
            if (schema == null)
            {
                report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.TypeNotRegistered, string.Empty, 0, string.Empty, "Config schema is missing."));
                return report;
            }

            for (int i = 0; i < schema.ReferenceRules.Count; i++)
            {
                ConfigReferenceRule rule = schema.ReferenceRules[i];
                if (!rule.IsValid || rule.HasRuntimeTarget)
                    continue;

                if (!TryGet(rule.TargetStructureKind, rule.TargetSchemaName, out ConfigSourceEntry targetSource))
                {
                    report.Add(new ConfigTableValidationIssue(
                        rule.Severity,
                        ConfigError.TypeNotRegistered,
                        schema.TableName,
                        0,
                        rule.FieldName,
                        "Missing config source: " + rule.GetTargetDisplayName() + "."));
                    continue;
                }

                foreach (T row in table.Rows)
                    ValidateRowReference(schema, row, rule, targetSource, report);
            }

            return report;
        }

        private static void ValidateRowReference<T>(
            ConfigSchema ownerSchema,
            T row,
            ConfigReferenceRule rule,
            ConfigSourceEntry targetSource,
            ConfigTableValidationReport report) where T : IConfigData
        {
            object key = GetMemberValue(row, rule.FieldName);
            if (!ConfigSourceEntry.TryNormalizeKey(key, out _))
            {
                if (rule.Required)
                {
                    report.Add(new ConfigTableValidationIssue(
                        rule.Severity,
                        ConfigError.InvalidId,
                        ownerSchema.TableName,
                        row.Id,
                        rule.FieldName,
                        "Cross-source reference key is missing."));
                }

                return;
            }

            if (!targetSource.ContainsKey(key))
            {
                report.Add(new ConfigTableValidationIssue(
                    rule.Severity,
                    ConfigError.NotFound,
                    ownerSchema.TableName,
                    row.Id,
                    rule.FieldName,
                    "Missing cross-source reference. Target=" + rule.GetTargetDisplayName() + "."));
            }
        }

        private static object GetMemberValue(object row, string memberName)
        {
            Type type = row.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
                return property.GetValue(row, null);

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            return field != null ? field.GetValue(row) : null;
        }
    }
}
