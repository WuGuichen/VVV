using System;
using System.Collections.Generic;
using System.Reflection;

namespace MxFramework.Config
{
    public readonly struct ConfigTableValidationIssue
    {
        public ConfigTableValidationIssue(
            ConfigValidationSeverity severity,
            ConfigError error,
            string tableName,
            int rowId,
            string fieldName,
            string message)
        {
            Severity = severity;
            Error = error;
            TableName = tableName ?? string.Empty;
            RowId = rowId;
            FieldName = fieldName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ConfigValidationSeverity Severity { get; }
        public ConfigError Error { get; }
        public string TableName { get; }
        public int RowId { get; }
        public string FieldName { get; }
        public string Message { get; }
    }

    public sealed class ConfigTableValidationReport
    {
        private readonly List<ConfigTableValidationIssue> _issues = new List<ConfigTableValidationIssue>();

        public IReadOnlyList<ConfigTableValidationIssue> Issues => _issues;
        public bool HasErrors { get; private set; }

        public void Add(ConfigTableValidationIssue issue)
        {
            _issues.Add(issue);
            if (issue.Severity == ConfigValidationSeverity.Error)
                HasErrors = true;
        }
    }

    public static class ConfigTableValidator
    {
        public static ConfigTableValidationReport Validate<T>(
            IConfigTable<T> table,
            IConfigProvider resolver = null,
            ILocalizationProvider localizationProvider = null) where T : IConfigData
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var report = new ConfigTableValidationReport();
            ConfigSchema schema = table.Schema;
            resolver = resolver ?? table;

            ValidateSchema(schema, report);
            foreach (T row in table.Rows)
            {
                ValidateId(schema, row, report);
                ValidateReferences(schema, row, resolver, report);
                ValidateLocalization(schema, row, localizationProvider, report);
            }

            return report;
        }

        private static void ValidateSchema(ConfigSchema schema, ConfigTableValidationReport report)
        {
            if (schema == null)
            {
                report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.TypeNotRegistered, string.Empty, 0, string.Empty, "Config schema is missing."));
                return;
            }

            if (string.IsNullOrWhiteSpace(schema.TableName))
            {
                report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.TypeNotRegistered, string.Empty, 0, string.Empty, "Config table name is missing."));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.TypeMismatch, schema.TableName, 0, string.Empty, "Config field name is missing."));
                    continue;
                }

                if (!names.Add(field.Name))
                {
                    report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.DuplicateId, schema.TableName, 0, field.Name, $"Duplicate config field: {field.Name}."));
                }
            }
        }

        private static void ValidateId<T>(ConfigSchema schema, T row, ConfigTableValidationReport report) where T : IConfigData
        {
            if (row.Id <= 0)
            {
                report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.InvalidId, schema.TableName, row.Id, "Id", $"Invalid config id: {row.Id}."));
                return;
            }

            if (schema.IdRange.IsValid && !schema.IdRange.Contains(row.Id))
            {
                report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.InvalidId, schema.TableName, row.Id, "Id", $"Config id out of range: {row.Id}. Expected {schema.IdRange.MinInclusive}-{schema.IdRange.MaxInclusive}."));
            }
        }

        private static void ValidateReferences<T>(ConfigSchema schema, T row, IConfigProvider resolver, ConfigTableValidationReport report) where T : IConfigData
        {
            for (int i = 0; i < schema.ReferenceRules.Count; i++)
            {
                ConfigReferenceRule rule = schema.ReferenceRules[i];
                if (!rule.HasRuntimeTarget)
                    continue;

                if (!TryGetIntValue(row, rule.FieldName, out int targetId))
                    continue;

                if (targetId <= 0)
                {
                    if (rule.Required)
                    {
                        report.Add(new ConfigTableValidationIssue(rule.Severity, ConfigError.InvalidId, schema.TableName, row.Id, rule.FieldName, $"Invalid config reference id: {targetId}."));
                    }

                    continue;
                }

                if (!Contains(resolver, rule.TargetType, targetId))
                {
                    report.Add(new ConfigTableValidationIssue(rule.Severity, ConfigError.NotFound, schema.TableName, row.Id, rule.FieldName, $"Missing config reference. Field={rule.FieldName}, Target={rule.TargetType.FullName}:{targetId}."));
                }
            }
        }

        private static void ValidateLocalization<T>(ConfigSchema schema, T row, ILocalizationProvider localizationProvider, ConfigTableValidationReport report) where T : IConfigData
        {
            if (schema.RequiredLocales.Count == 0)
                return;

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                if (field.FieldType != ConfigFieldType.LocalizedText)
                    continue;

                if (!TryGetLocalizedKey(row, field.Name, out LocalizedTextKey key) || !key.IsValid)
                {
                    report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.InvalidId, schema.TableName, row.Id, field.Name, "Localized text key is missing."));
                    continue;
                }

                if (localizationProvider == null)
                    continue;

                for (int j = 0; j < schema.RequiredLocales.Count; j++)
                {
                    LocaleId locale = schema.RequiredLocales[j];
                    if (!localizationProvider.TryGetText(key, locale, out _))
                    {
                        report.Add(new ConfigTableValidationIssue(ConfigValidationSeverity.Error, ConfigError.NotFound, schema.TableName, row.Id, field.Name, $"Missing localized text. Key={key}, Locale={locale}."));
                    }
                }
            }
        }

        private static bool TryGetIntValue(object row, string memberName, out int value)
        {
            value = 0;
            object raw = GetMemberValue(row, memberName);
            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            return false;
        }

        private static bool TryGetLocalizedKey(object row, string memberName, out LocalizedTextKey key)
        {
            key = default;
            object raw = GetMemberValue(row, memberName);
            if (raw is LocalizedTextKey typed)
            {
                key = typed;
                return true;
            }

            if (raw is string text)
            {
                key = new LocalizedTextKey(text);
                return true;
            }

            return false;
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

        private static bool Contains(IConfigProvider provider, Type type, int id)
        {
            MethodInfo method = typeof(IConfigProvider).GetMethod(nameof(IConfigProvider.GetAllConfigs));
            MethodInfo generic = method.MakeGenericMethod(type);
            var all = (System.Collections.IEnumerable)generic.Invoke(provider, null);
            foreach (object item in all)
            {
                if (item is IConfigData data && data.Id == id)
                    return true;
            }

            return false;
        }
    }
}
