using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MxFramework.Config;

namespace MxFramework.Editor
{
    public sealed class MemoryConfigEditorSource<T> : IConfigEditorSource, IConfigEditorSourceIndexProvider, IConfigEditorTablePreviewProvider, IConfigEditorEnumProvider where T : IConfigData
    {
        private readonly IConfigTable<T> _table;
        private readonly IConfigProvider _resolver;
        private readonly ILocalizationProvider _localizationProvider;
        private readonly ConfigEnumRegistry _enumRegistry;

        public MemoryConfigEditorSource(
            string name,
            IConfigTable<T> table,
            IConfigProvider resolver = null,
            ILocalizationProvider localizationProvider = null,
            ConfigEnumRegistry enumRegistry = null,
            string sourceType = "Memory")
        {
            Name = name ?? string.Empty;
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _resolver = resolver;
            _localizationProvider = localizationProvider;
            _enumRegistry = enumRegistry;
            SourceType = sourceType ?? string.Empty;
        }

        public string Name { get; }
        public string SourceType { get; }
        public ConfigSchema Schema => _table.Schema;
        public int RowCount => _table.Rows.Count;

        public ConfigAuthoringTemplate CreateTemplate()
        {
            return ConfigAuthoring.CreateTemplate(Schema);
        }

        public ConfigAuthoringReport Validate()
        {
            return ConfigAuthoring.ValidateTable(_table, _resolver, _localizationProvider);
        }

        public ConfigAuthoringReport Validate(ConfigSourceIndex sourceIndex)
        {
            ConfigAuthoringReport tableReport = Validate();
            if (sourceIndex == null)
                return tableReport;

            ConfigAuthoringReport crossSourceReport = ConfigAuthoring.FromValidationReport(sourceIndex.ValidateCrossSourceReferences(_table));
            if (crossSourceReport.Issues.Count == 0)
                return tableReport;

            var issues = new List<ConfigAuthoringIssue>();
            issues.AddRange(tableReport.Issues);
            issues.AddRange(crossSourceReport.Issues);
            return new ConfigAuthoringReport(Schema.TableName, issues);
        }

        public ConfigSourceEntry CreateSourceEntry()
        {
            var entry = new ConfigSourceEntry(Schema);
            foreach (T row in _table.Rows)
                entry.AddKey(row.Id);
            return entry;
        }

        public string CreateTsvPreview(int maxRows)
        {
            ConfigAuthoringTemplate template = CreateTemplate();
            var builder = new StringBuilder();
            builder.Append(template.HeaderLine).Append('\n');

            int count = 0;
            foreach (T row in _table.Rows)
            {
                if (count >= maxRows)
                    break;

                AppendRow(builder, row);
                count++;
            }

            if (count == 0)
                builder.Append(template.SampleLine).Append('\n');

            return builder.ToString();
        }

        public IReadOnlyList<ConfigEditorRowPreview> CreateRowPreview(int maxRows)
        {
            var rows = new List<ConfigEditorRowPreview>();
            int count = 0;
            foreach (T row in _table.Rows)
            {
                if (count >= maxRows)
                    break;

                var cells = new List<ConfigEditorCellPreview>();
                IReadOnlyList<ConfigField> fields = Schema.Fields;
                for (int i = 0; i < fields.Count; i++)
                {
                    ConfigField field = fields[i];
                    cells.Add(ConfigEditorControlHints.CreateCellPreview(field, GetFieldText(row, field.Name), this));
                }

                rows.Add(new ConfigEditorRowPreview(row.Id, cells.ToArray()));
                count++;
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
            builder.Append("MxFramework Config Source Report\n");
            builder.Append("source: ").Append(Name).Append('\n');
            builder.Append("sourceType: ").Append(SourceType).Append('\n');
            builder.Append("table: ").Append(Schema.TableName).Append('\n');
            builder.Append("rowCount: ").Append(RowCount).Append('\n');
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

        private void AppendRow(StringBuilder builder, T row)
        {
            IReadOnlyList<ConfigField> fields = Schema.Fields;
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    builder.Append('\t');

                builder.Append(GetFieldText(row, fields[i].Name));
            }

            builder.Append('\n');
        }

        private static string GetFieldText(T row, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            PropertyInfo property = typeof(T).GetProperty(fieldName, flags);
            if (property != null)
                return FormatValue(property.GetValue(row, null));

            FieldInfo field = typeof(T).GetField(fieldName, flags);
            return field != null ? FormatValue(field.GetValue(row)) : string.Empty;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is Array array)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < array.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append(array.GetValue(i));
                }

                return builder.ToString();
            }

            return value.ToString();
        }
    }
}
