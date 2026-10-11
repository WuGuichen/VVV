using MxFramework.Config;

namespace MxFramework.Editor
{
    public readonly struct ConfigIssueView
    {
        public ConfigIssueView(string sourceName, ConfigAuthoringIssue issue)
            : this(sourceName, issue, string.Empty)
        {
        }

        public ConfigIssueView(string sourceName, ConfigAuthoringIssue issue, string fieldDisplayName)
        {
            SourceName = sourceName ?? string.Empty;
            Issue = issue;
            FieldDisplayName = fieldDisplayName ?? string.Empty;
        }

        public string SourceName { get; }
        public ConfigAuthoringIssue Issue { get; }
        public string FieldDisplayName { get; }

        public string ToLine()
        {
            return "source=" + SourceName +
                " table=" + Issue.TableName +
                " row=" + Issue.RowId +
                " field=" + Issue.FieldName +
                FormatFieldDisplayName(FieldDisplayName) +
                " severity=" + Issue.Severity +
                " error=" + Issue.Error +
                " message=" + Issue.Message;
        }

        private static string FormatFieldDisplayName(string fieldDisplayName)
        {
            return string.IsNullOrEmpty(fieldDisplayName) ? string.Empty : " fieldDisplay=" + fieldDisplayName;
        }
    }
}
