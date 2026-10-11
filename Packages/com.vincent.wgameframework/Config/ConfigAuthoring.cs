using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Config
{
    public readonly struct ConfigAuthoringColumn
    {
        public ConfigAuthoringColumn(string name, ConfigFieldType fieldType, bool required, string referenceTarget, string enumId, string description)
        {
            Name = name ?? string.Empty;
            FieldType = fieldType;
            Required = required;
            ReferenceTarget = referenceTarget ?? string.Empty;
            EnumId = enumId ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Name { get; }
        public ConfigFieldType FieldType { get; }
        public bool Required { get; }
        public string ReferenceTarget { get; }
        public string EnumId { get; }
        public string Description { get; }
    }

    public sealed class ConfigAuthoringTemplate
    {
        private readonly List<ConfigAuthoringColumn> _columns;

        public ConfigAuthoringTemplate(
            string tableName,
            IReadOnlyList<ConfigAuthoringColumn> columns,
            string headerLine,
            string sampleLine,
            string text)
        {
            TableName = tableName ?? string.Empty;
            _columns = columns != null ? new List<ConfigAuthoringColumn>(columns) : new List<ConfigAuthoringColumn>();
            HeaderLine = headerLine ?? string.Empty;
            SampleLine = sampleLine ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string TableName { get; }
        public IReadOnlyList<ConfigAuthoringColumn> Columns => _columns;
        public string HeaderLine { get; }
        public string SampleLine { get; }
        public string Text { get; }
    }

    public readonly struct ConfigAuthoringIssue
    {
        public ConfigAuthoringIssue(
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

    public sealed class ConfigAuthoringReport
    {
        private readonly List<ConfigAuthoringIssue> _issues;

        public ConfigAuthoringReport(string tableName, IReadOnlyList<ConfigAuthoringIssue> issues)
        {
            TableName = tableName ?? string.Empty;
            _issues = issues != null ? new List<ConfigAuthoringIssue>(issues) : new List<ConfigAuthoringIssue>();
        }

        public string TableName { get; }
        public IReadOnlyList<ConfigAuthoringIssue> Issues => _issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == ConfigValidationSeverity.Error)
                        return true;
                }

                return false;
            }
        }
    }

    public static class ConfigAuthoring
    {
        public static ConfigAuthoringTemplate CreateTemplate(ConfigSchema schema)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            var columns = new List<ConfigAuthoringColumn>();
            var header = new StringBuilder();
            var sample = new StringBuilder();
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                string referenceTarget = field.ReferenceRule.IsValid ? field.ReferenceRule.GetTargetDisplayName() : string.Empty;
                columns.Add(new ConfigAuthoringColumn(
                    field.Name,
                    field.FieldType,
                    field.Required,
                    referenceTarget,
                    field.EnumId,
                    field.Description));

                if (i > 0)
                {
                    header.Append('\t');
                    sample.Append('\t');
                }

                header.Append(field.Name);
                sample.Append(CreateSampleValue(schema, field));
            }

            string headerLine = header.ToString();
            string sampleLine = sample.ToString();
            var text = new StringBuilder();
            text.Append("table: ").Append(schema.TableName).Append('\n');
            if (schema.IdRange.IsValid)
                text.Append("idRange: ").Append(schema.IdRange.MinInclusive).Append('-').Append(schema.IdRange.MaxInclusive).Append('\n');
            text.Append("format: tsv\n");
            text.Append("header: ").Append(headerLine).Append('\n');
            text.Append("sample: ").Append(sampleLine).Append('\n');
            text.Append("columns:\n");
            for (int i = 0; i < columns.Count; i++)
            {
                ConfigAuthoringColumn column = columns[i];
                text.Append("- ").Append(column.Name).Append(": ").Append(column.FieldType);
                if (column.Required)
                    text.Append(" required");
                if (!string.IsNullOrEmpty(column.ReferenceTarget))
                    text.Append(" -> ").Append(column.ReferenceTarget);
                if (!string.IsNullOrEmpty(column.EnumId))
                    text.Append(" enum=").Append(column.EnumId);
                if (!string.IsNullOrEmpty(column.Description))
                    text.Append(" # ").Append(column.Description);
                text.Append('\n');
            }

            if (schema.RequiredLocales.Count > 0)
            {
                text.Append("requiredLocales:");
                for (int i = 0; i < schema.RequiredLocales.Count; i++)
                    text.Append(' ').Append(schema.RequiredLocales[i]);
                text.Append('\n');
            }

            return new ConfigAuthoringTemplate(schema.TableName, columns, headerLine, sampleLine, text.ToString());
        }

        public static ConfigAuthoringReport ValidateTable<T>(
            IConfigTable<T> table,
            IConfigProvider resolver = null,
            ILocalizationProvider localizationProvider = null) where T : IConfigData
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            return FromValidationReport(ConfigTableValidator.Validate(table, resolver, localizationProvider));
        }

        public static ConfigAuthoringReport FromValidationReport(ConfigTableValidationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var issues = new List<ConfigAuthoringIssue>();
            string tableName = string.Empty;
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigTableValidationIssue issue = report.Issues[i];
                if (string.IsNullOrEmpty(tableName))
                    tableName = issue.TableName;
                issues.Add(new ConfigAuthoringIssue(
                    issue.Severity,
                    issue.Error,
                    issue.TableName,
                    issue.RowId,
                    issue.FieldName,
                    issue.Message));
            }

            return new ConfigAuthoringReport(tableName, issues);
        }

        public static string ExportReportText(ConfigAuthoringReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var builder = new StringBuilder();
            builder.Append("table: ").Append(report.TableName).Append('\n');
            builder.Append("hasErrors: ").Append(report.HasErrors ? "true" : "false").Append('\n');
            builder.Append("issues:\n");
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigAuthoringIssue issue = report.Issues[i];
                builder.Append("- severity: ").Append(issue.Severity)
                    .Append(", error: ").Append(issue.Error)
                    .Append(", rowId: ").Append(issue.RowId)
                    .Append(", field: ").Append(issue.FieldName)
                    .Append(", message: ").Append(issue.Message)
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateSampleValue(ConfigSchema schema, ConfigField field)
        {
            if (string.Equals(field.Name, "Id", StringComparison.Ordinal))
                return schema.IdRange.IsValid ? schema.IdRange.MinInclusive.ToString() : "1";

            switch (field.FieldType)
            {
                case ConfigFieldType.Integer:
                    return "0";
                case ConfigFieldType.Float:
                    return "1.0";
                case ConfigFieldType.Boolean:
                    return "false";
                case ConfigFieldType.Enum:
                    return "EnumValue";
                case ConfigFieldType.ConfigReference:
                    return "0";
                case ConfigFieldType.LocalizedText:
                    return schema.TableName.ToLowerInvariant() + "." + field.Name.ToLowerInvariant();
                case ConfigFieldType.AssetPath:
                    return "Assets/Path/Asset.asset";
                case ConfigFieldType.Custom:
                    return "custom";
                default:
                    return "text";
            }
        }
    }
}
