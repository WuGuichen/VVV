using System;
using System.Collections.Generic;

namespace MxFramework.Settings.Input
{
    public sealed class MxInputBindingListResult
    {
        private readonly List<MxInputBindingInfo> _bindings;
        private readonly List<MxSettingsIssue> _issues;

        private MxInputBindingListResult(
            IReadOnlyList<MxInputBindingInfo> bindings,
            IReadOnlyList<MxSettingsIssue> issues)
        {
            _bindings = new List<MxInputBindingInfo>();
            if (bindings != null)
            {
                for (int i = 0; i < bindings.Count; i++)
                    _bindings.Add(bindings[i]);
            }

            _issues = CopyIssues(issues);
        }

        public bool Success => _issues.Count == 0;
        public IReadOnlyList<MxInputBindingInfo> Bindings => _bindings;
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;

        public static MxInputBindingListResult Succeeded(IReadOnlyList<MxInputBindingInfo> bindings)
        {
            return new MxInputBindingListResult(bindings, Array.Empty<MxSettingsIssue>());
        }

        public static MxInputBindingListResult WithIssues(
            IReadOnlyList<MxInputBindingInfo> bindings,
            IReadOnlyList<MxSettingsIssue> issues)
        {
            return new MxInputBindingListResult(bindings, issues);
        }

        public static MxInputBindingListResult Failed(MxSettingsIssue issue)
        {
            return new MxInputBindingListResult(
                Array.Empty<MxInputBindingInfo>(),
                new[] { issue });
        }

        private static List<MxSettingsIssue> CopyIssues(IReadOnlyList<MxSettingsIssue> issues)
        {
            var copy = new List<MxSettingsIssue>();
            if (issues == null)
                return copy;

            for (int i = 0; i < issues.Count; i++)
            {
                if (!issues[i].IsNone)
                    copy.Add(issues[i]);
            }

            return copy;
        }
    }

    public sealed class MxInputSettingsOperationResult
    {
        private readonly List<MxSettingsIssue> _issues;

        private MxInputSettingsOperationResult(IReadOnlyList<MxSettingsIssue> issues)
        {
            _issues = CopyIssues(issues);
        }

        public bool Success => _issues.Count == 0;
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;

        public static MxInputSettingsOperationResult Succeeded()
        {
            return new MxInputSettingsOperationResult(Array.Empty<MxSettingsIssue>());
        }

        public static MxInputSettingsOperationResult Failed(MxSettingsIssue issue)
        {
            return new MxInputSettingsOperationResult(new[] { issue });
        }

        private static List<MxSettingsIssue> CopyIssues(IReadOnlyList<MxSettingsIssue> issues)
        {
            var copy = new List<MxSettingsIssue>();
            if (issues == null)
                return copy;

            for (int i = 0; i < issues.Count; i++)
            {
                if (!issues[i].IsNone)
                    copy.Add(issues[i]);
            }

            return copy;
        }
    }
}
