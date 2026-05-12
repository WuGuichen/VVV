using System.Collections.Generic;

namespace MxFramework.Resources
{
    public enum ResourceCatalogValidationSeverity
    {
        Error = 0,
        Warning = 1
    }

    public sealed class ResourceCatalogValidationIssue
    {
        public ResourceCatalogValidationIssue(
            ResourceCatalogValidationSeverity severity,
            string code,
            ResourceKey key,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Key = key;
            Message = message ?? string.Empty;
        }

        public ResourceCatalogValidationSeverity Severity { get; }
        public string Code { get; }
        public ResourceKey Key { get; }
        public string Message { get; }
    }

    public sealed class ResourceCatalogValidationReport
    {
        private readonly List<ResourceCatalogValidationIssue> _issues = new List<ResourceCatalogValidationIssue>();

        public IReadOnlyList<ResourceCatalogValidationIssue> Issues => _issues;
        public bool HasErrors => ErrorCount > 0;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public void AddError(string code, ResourceKey key, string message)
        {
            Add(ResourceCatalogValidationSeverity.Error, code, key, message);
        }

        public void AddWarning(string code, ResourceKey key, string message)
        {
            Add(ResourceCatalogValidationSeverity.Warning, code, key, message);
        }

        public void Merge(ResourceCatalogValidationReport report)
        {
            if (report == null)
                return;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = report.Issues[i];
                Add(issue.Severity, issue.Code, issue.Key, issue.Message);
            }
        }

        private void Add(ResourceCatalogValidationSeverity severity, string code, ResourceKey key, string message)
        {
            _issues.Add(new ResourceCatalogValidationIssue(severity, code, key, message));
            if (severity == ResourceCatalogValidationSeverity.Error)
                ErrorCount++;
            else
                WarningCount++;
        }
    }
}
