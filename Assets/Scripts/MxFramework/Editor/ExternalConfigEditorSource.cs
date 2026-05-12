using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Config;

namespace MxFramework.Editor
{
    public sealed class ExternalConfigEditorSource : IConfigEditorSource, IConfigEditorSourceIndexProvider, IConfigEditorTablePreviewProvider, IConfigEditorEnumProvider
    {
        private readonly List<object> _keys;
        private readonly List<ConfigAuthoringIssue> _issues;
        private readonly string _templateText;
        private readonly string _previewText;
        private readonly string _sourcePath;
        private readonly string _contentHash;
        private readonly string _keyField;
        private readonly ConfigEnumRegistry _enumRegistry;

        public ExternalConfigEditorSource(
            string name,
            string sourceType,
            ConfigSchema schema,
            int rowCount,
            IEnumerable<object> keys = null,
            IEnumerable<ConfigAuthoringIssue> issues = null,
            string templateText = "",
            string previewText = "",
            string sourcePath = "",
            string contentHash = "",
            string keyField = "Id",
            ConfigEnumRegistry enumRegistry = null)
        {
            Name = name ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            RowCount = rowCount < 0 ? 0 : rowCount;
            _keys = keys != null ? new List<object>(keys) : new List<object>();
            _issues = issues != null ? new List<ConfigAuthoringIssue>(issues) : new List<ConfigAuthoringIssue>();
            _templateText = templateText ?? string.Empty;
            _previewText = previewText ?? string.Empty;
            _sourcePath = sourcePath ?? string.Empty;
            _contentHash = contentHash ?? string.Empty;
            _keyField = string.IsNullOrWhiteSpace(keyField) ? "Id" : keyField;
            _enumRegistry = enumRegistry;
        }

        public string Name { get; }
        public string SourceType { get; }
        public ConfigSchema Schema { get; }
        public int RowCount { get; }

        public ConfigAuthoringTemplate CreateTemplate()
        {
            if (string.IsNullOrEmpty(_templateText))
                return ConfigAuthoring.CreateTemplate(Schema);

            ConfigAuthoringTemplate fallback = ConfigAuthoring.CreateTemplate(Schema);
            return new ConfigAuthoringTemplate(
                Schema.TableName,
                fallback.Columns,
                fallback.HeaderLine,
                fallback.SampleLine,
                _templateText);
        }

        public ConfigAuthoringReport Validate()
        {
            return new ConfigAuthoringReport(Schema.TableName, _issues);
        }

        public ConfigAuthoringReport Validate(ConfigSourceIndex sourceIndex)
        {
            ConfigAuthoringReport sourceReport = Validate();
            if (sourceIndex == null)
                return sourceReport;

            ConfigAuthoringReport crossSourceReport = ConfigAuthoring.FromValidationReport(ValidateCrossSourceReferences(sourceIndex));
            if (crossSourceReport.Issues.Count == 0)
                return sourceReport;

            var issues = new List<ConfigAuthoringIssue>();
            issues.AddRange(sourceReport.Issues);
            issues.AddRange(crossSourceReport.Issues);
            return new ConfigAuthoringReport(Schema.TableName, issues);
        }

        public string CreateTsvPreview(int maxRows)
        {
            if (!string.IsNullOrEmpty(_previewText))
                return _previewText;

            return CreateTemplate().HeaderLine + "\n" + CreateTemplate().SampleLine + "\n";
        }

        public IReadOnlyList<ConfigEditorRowPreview> CreateRowPreview(int maxRows)
        {
            var rows = new List<ConfigEditorRowPreview>();
            string preview = CreateTsvPreview(maxRows);
            if (string.IsNullOrWhiteSpace(preview))
                return rows;

            string[] lines = preview.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return rows;

            string[] headers = lines[0].Split('\t');
            int rowLimit = maxRows < 0 ? 0 : maxRows;
            for (int i = 1; i < lines.Length && rows.Count < rowLimit; i++)
            {
                string[] values = lines[i].Split('\t');
                var cells = new List<ConfigEditorCellPreview>();
                int rowId = 0;
                for (int j = 0; j < headers.Length; j++)
                {
                    string fieldName = headers[j];
                    string value = j < values.Length ? values[j] : string.Empty;
                    ConfigField field = FindField(fieldName);
                    cells.Add(ConfigEditorControlHints.CreateCellPreview(field, value, this));
                    if (fieldName == "Id")
                        int.TryParse(value, out rowId);
                }

                rows.Add(new ConfigEditorRowPreview(rowId, cells.ToArray()));
            }

            return rows;
        }

        public bool TryGetEnumDomain(string enumId, out ConfigEnumDomain domain)
        {
            if (_enumRegistry != null)
                return _enumRegistry.TryGet(enumId, out domain);

            domain = null;
            return false;
        }

        public string CreateReport()
        {
            ConfigAuthoringReport report = Validate();
            var builder = new StringBuilder();
            builder.Append("MxFramework External Config Source Report\n");
            builder.Append("source: ").Append(Name).Append('\n');
            builder.Append("sourceType: ").Append(SourceType).Append('\n');
            builder.Append("structure: ").Append(Schema.StructureKind).Append('\n');
            builder.Append("table: ").Append(Schema.TableName).Append('\n');
            builder.Append("rowCount: ").Append(RowCount).Append('\n');
            builder.Append("keyCount: ").Append(_keys.Count).Append('\n');
            builder.Append("sourcePath: ").Append(_sourcePath).Append('\n');
            builder.Append("contentHash: ").Append(_contentHash).Append('\n');
            builder.Append("hasErrors: ").Append(report.HasErrors ? "true" : "false").Append('\n');
            builder.Append("issues:\n");

            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigAuthoringIssue issue = report.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(" row=")
                    .Append(issue.RowId)
                    .Append(" field=")
                    .Append(issue.FieldName)
                    .Append(FormatFieldDisplayName(issue.FieldName))
                    .Append(" error=")
                    .Append(issue.Error)
                    .Append(": ")
                    .Append(issue.Message)
                    .Append('\n');
            }

            if (report.Issues.Count == 0)
                builder.Append("- none\n");

            return builder.ToString();
        }

        private string FormatFieldDisplayName(string fieldName)
        {
            string displayName = FindFieldDisplayName(fieldName);
            return string.IsNullOrEmpty(displayName) ? string.Empty : " fieldDisplay=" + displayName;
        }

        private string FindFieldDisplayName(string fieldName)
        {
            for (int i = 0; i < Schema.Fields.Count; i++)
            {
                if (Schema.Fields[i].Name == fieldName)
                    return Schema.Fields[i].DisplayName;
            }

            return string.Empty;
        }

        public ConfigSourceEntry CreateSourceEntry()
        {
            return new ConfigSourceEntry(Schema, _keyField, _sourcePath, _contentHash).AddKeys(_keys);
        }

        private ConfigTableValidationReport ValidateCrossSourceReferences(ConfigSourceIndex sourceIndex)
        {
            var report = new ConfigTableValidationReport();
            for (int i = 0; i < Schema.ReferenceRules.Count; i++)
            {
                ConfigReferenceRule rule = Schema.ReferenceRules[i];
                if (!rule.IsValid || rule.HasRuntimeTarget)
                    continue;

                if (!sourceIndex.TryGet(rule.TargetStructureKind, rule.TargetSchemaName, out _))
                {
                    report.Add(new ConfigTableValidationIssue(
                        ConfigValidationSeverity.Error,
                        ConfigError.TypeNotRegistered,
                        Schema.TableName,
                        0,
                        rule.FieldName,
                        "Missing config source: " + rule.GetTargetDisplayName() + "."));
                    continue;
                }

                ValidatePreviewReferenceRows(rule, report, sourceIndex);
            }

            return report;
        }

        private void ValidatePreviewReferenceRows(
            ConfigReferenceRule rule,
            ConfigTableValidationReport report,
            ConfigSourceIndex sourceIndex)
        {
            if (string.IsNullOrWhiteSpace(_previewText))
                return;

            if (!sourceIndex.TryGet(rule.TargetStructureKind, rule.TargetSchemaName, out ConfigSourceEntry targetSource))
                return;

            string[] lines = _previewText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return;

            string[] headers = lines[0].Split('\t');
            int referenceColumn = FindColumn(headers, rule.FieldName);
            if (referenceColumn < 0)
                return;

            int idColumn = FindColumn(headers, _keyField);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = lines[i].Split('\t');
                string value = referenceColumn < values.Length ? values[referenceColumn] : string.Empty;
                int rowId = TryReadRowId(values, idColumn);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (rule.Required)
                    {
                        report.Add(new ConfigTableValidationIssue(
                            ConfigValidationSeverity.Error,
                            ConfigError.InvalidId,
                            Schema.TableName,
                            rowId,
                            rule.FieldName,
                            "Preview reference key is missing."));
                    }

                    continue;
                }

                if (!targetSource.ContainsKey(value))
                {
                    report.Add(new ConfigTableValidationIssue(
                        ConfigValidationSeverity.Error,
                        ConfigError.NotFound,
                        Schema.TableName,
                        rowId,
                        rule.FieldName,
                        "Missing preview reference. Target=" + rule.GetTargetDisplayName() + "."));
                }
            }
        }

        private static int FindColumn(string[] headers, string fieldName)
        {
            if (headers == null)
                return -1;

            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] == fieldName)
                    return i;
            }

            return -1;
        }

        private static int TryReadRowId(string[] values, int idColumn)
        {
            if (values == null || idColumn < 0 || idColumn >= values.Length)
                return 0;

            return int.TryParse(values[idColumn], out int rowId) ? rowId : 0;
        }

        private ConfigField FindField(string fieldName)
        {
            for (int i = 0; i < Schema.Fields.Count; i++)
            {
                if (Schema.Fields[i].Name == fieldName)
                    return Schema.Fields[i];
            }

            return new ConfigField(fieldName, ConfigFieldType.String);
        }
    }
}
