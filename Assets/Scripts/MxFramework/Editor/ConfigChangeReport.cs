using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Editor
{
    public sealed class ConfigChangeReport
    {
        private readonly List<ConfigSourceChange> _sourceChanges = new List<ConfigSourceChange>();
        private readonly List<ConfigIssueStatChange> _issueStatChanges = new List<ConfigIssueStatChange>();

        public IReadOnlyList<ConfigSourceChange> SourceChanges => _sourceChanges;
        public IReadOnlyList<ConfigIssueStatChange> IssueStatChanges => _issueStatChanges;
        public bool HasBaseline { get; private set; }
        public int AddedSourceCount { get; private set; }
        public int RemovedSourceCount { get; private set; }
        public int ChangedSourceCount { get; private set; }
        public int IssueTypeChangeCount => _issueStatChanges.Count;
        public bool HasChanges => AddedSourceCount > 0 || RemovedSourceCount > 0 || ChangedSourceCount > 0 || IssueTypeChangeCount > 0;

        public static ConfigChangeReport NoBaseline()
        {
            return new ConfigChangeReport();
        }

        public static ConfigChangeReport WithBaseline()
        {
            return new ConfigChangeReport { HasBaseline = true };
        }

        public void AddSourceChange(ConfigSourceChange change)
        {
            if (change == null)
                return;

            _sourceChanges.Add(change);
            if (change.ChangeType == ConfigSourceChangeType.Added)
                AddedSourceCount++;
            else if (change.ChangeType == ConfigSourceChangeType.Removed)
                RemovedSourceCount++;
            else if (change.ChangeType == ConfigSourceChangeType.Changed)
                ChangedSourceCount++;
        }

        public void AddIssueStatChange(ConfigIssueStatChange change)
        {
            if (change != null)
                _issueStatChanges.Add(change);
        }
    }

    public enum ConfigSourceChangeType
    {
        Added,
        Removed,
        Changed
    }

    public sealed class ConfigSourceChange
    {
        public ConfigSourceChange(
            ConfigSourceChangeType changeType,
            string sourceName,
            string tableName,
            int previousRows,
            int currentRows,
            int previousErrors,
            int currentErrors,
            int previousWarnings,
            int currentWarnings)
        {
            ChangeType = changeType;
            SourceName = sourceName ?? string.Empty;
            TableName = tableName ?? string.Empty;
            PreviousRows = previousRows;
            CurrentRows = currentRows;
            PreviousErrors = previousErrors;
            CurrentErrors = currentErrors;
            PreviousWarnings = previousWarnings;
            CurrentWarnings = currentWarnings;
        }

        public ConfigSourceChangeType ChangeType { get; }
        public string SourceName { get; }
        public string TableName { get; }
        public int PreviousRows { get; }
        public int CurrentRows { get; }
        public int PreviousErrors { get; }
        public int CurrentErrors { get; }
        public int PreviousWarnings { get; }
        public int CurrentWarnings { get; }
        public int RowDelta => CurrentRows - PreviousRows;
        public int ErrorDelta => CurrentErrors - PreviousErrors;
        public int WarningDelta => CurrentWarnings - PreviousWarnings;
    }

    public sealed class ConfigIssueStatChange
    {
        public ConfigIssueStatChange(ConfigError error, int previousCount, int currentCount)
        {
            Error = error;
            PreviousCount = previousCount;
            CurrentCount = currentCount;
        }

        public ConfigError Error { get; }
        public int PreviousCount { get; }
        public int CurrentCount { get; }
        public int Delta => CurrentCount - PreviousCount;
    }
}
