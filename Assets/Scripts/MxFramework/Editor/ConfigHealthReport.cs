using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Editor
{
    public sealed class ConfigHealthReport
    {
        private readonly List<ConfigSourceHealth> _sources = new List<ConfigSourceHealth>();
        private readonly List<ConfigIssueStat> _issueStats = new List<ConfigIssueStat>();
        private readonly List<ConfigAuthoringIssue> _sampleIssues = new List<ConfigAuthoringIssue>();

        public IReadOnlyList<ConfigSourceHealth> Sources => _sources;
        public IReadOnlyList<ConfigIssueStat> IssueStats => _issueStats;
        public IReadOnlyList<ConfigAuthoringIssue> SampleIssues => _sampleIssues;
        public int SourceCount { get; private set; }
        public int TotalRows { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ProblemSourceCount { get; private set; }
        public int MissingReferenceCount { get; private set; }
        public int MissingLocalizationCount { get; private set; }
        public int InvalidIdCount { get; private set; }
        public int SchemaIssueCount { get; private set; }
        public int SourceIndexCount { get; private set; }
        public int SourceKeyCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;
        public bool HasWarnings => WarningCount > 0;

        public void AddSource(ConfigSourceHealth source)
        {
            if (source == null)
                return;

            _sources.Add(source);
            SourceCount++;
            TotalRows += source.RowCount;
            if (source.HasIssues)
                ProblemSourceCount++;
        }

        public void AddSourceIndex(ConfigSourceEntry source)
        {
            if (source == null)
                return;

            SourceIndexCount++;
            SourceKeyCount += source.KeyCount;
        }

        public void AddIssue(ConfigAuthoringIssue issue)
        {
            if (issue.Severity == ConfigValidationSeverity.Error)
                ErrorCount++;
            else if (issue.Severity == ConfigValidationSeverity.Warning)
                WarningCount++;

            CountCategory(issue);
            AddIssueStat(issue.Error);
            if (_sampleIssues.Count < 10)
                _sampleIssues.Add(issue);
        }

        private void CountCategory(ConfigAuthoringIssue issue)
        {
            if (issue.Error == ConfigError.InvalidId)
            {
                InvalidIdCount++;
                return;
            }

            if (issue.Error == ConfigError.DuplicateId || issue.Error == ConfigError.TypeMismatch || issue.Error == ConfigError.TypeNotRegistered)
            {
                SchemaIssueCount++;
                return;
            }

            if (issue.Error == ConfigError.NotFound)
            {
                if (issue.Message.Contains("localized text"))
                    MissingLocalizationCount++;
                else if (issue.Message.Contains("config reference") || issue.Message.Contains("cross-source reference"))
                    MissingReferenceCount++;
            }
        }

        private void AddIssueStat(ConfigError error)
        {
            for (int i = 0; i < _issueStats.Count; i++)
            {
                if (_issueStats[i].Error == error)
                {
                    _issueStats[i].Increment();
                    return;
                }
            }

            _issueStats.Add(new ConfigIssueStat(error));
        }
    }

    public sealed class ConfigSourceHealth
    {
        public ConfigSourceHealth(string sourceName, string tableName, string sourceType, int rowCount, int errorCount, int warningCount)
        {
            SourceName = sourceName ?? string.Empty;
            TableName = tableName ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            RowCount = rowCount;
            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        public string SourceName { get; }
        public string TableName { get; }
        public string SourceType { get; }
        public int RowCount { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool HasIssues => ErrorCount > 0 || WarningCount > 0;
        public string Status => ErrorCount > 0 ? "错误" : WarningCount > 0 ? "警告" : "正常";
    }

    public sealed class ConfigIssueStat
    {
        public ConfigIssueStat(ConfigError error)
        {
            Error = error;
            Count = 1;
        }

        public ConfigError Error { get; }
        public int Count { get; private set; }

        public void Increment()
        {
            Count++;
        }
    }
}
