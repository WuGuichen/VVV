using System;
using System.Collections.Generic;

namespace MxFramework.Settings
{
    public sealed class MxSettingsLoadResult
    {
        private readonly List<MxSettingsIssue> _issues;

        private MxSettingsLoadResult(MxSettingsProfile profile, IReadOnlyList<MxSettingsIssue> issues)
        {
            Profile = profile ?? MxSettingsProfile.CreateDefault();
            _issues = CopyIssues(issues);
        }

        public MxSettingsProfile Profile { get; }
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;
        public bool Success => Profile != null;
        public bool HasIssues => _issues.Count > 0;

        public static MxSettingsLoadResult Succeeded(MxSettingsProfile profile)
        {
            return new MxSettingsLoadResult(profile, Array.Empty<MxSettingsIssue>());
        }

        public static MxSettingsLoadResult WithIssues(
            MxSettingsProfile profile,
            IReadOnlyList<MxSettingsIssue> issues)
        {
            return new MxSettingsLoadResult(profile, issues);
        }

        public static MxSettingsLoadResult WithIssue(MxSettingsProfile profile, MxSettingsIssue issue)
        {
            return new MxSettingsLoadResult(profile, new[] { issue });
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

    public sealed class MxSettingsSaveResult
    {
        private readonly List<MxSettingsIssue> _issues;

        private MxSettingsSaveResult(bool success, IReadOnlyList<MxSettingsIssue> issues)
        {
            Success = success;
            _issues = CopyIssues(issues);
        }

        public bool Success { get; }
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;
        public bool HasIssues => _issues.Count > 0;

        public static MxSettingsSaveResult Succeeded()
        {
            return new MxSettingsSaveResult(true, Array.Empty<MxSettingsIssue>());
        }

        public static MxSettingsSaveResult Failed(IReadOnlyList<MxSettingsIssue> issues)
        {
            return new MxSettingsSaveResult(false, issues);
        }

        public static MxSettingsSaveResult Failed(MxSettingsIssue issue)
        {
            return Failed(new[] { issue });
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

    public sealed class MxSettingsApplyResult
    {
        private readonly List<MxSettingsIssue> _issues;

        private MxSettingsApplyResult(IReadOnlyList<MxSettingsIssue> issues)
        {
            _issues = CopyIssues(issues);
        }

        public bool Success => _issues.Count == 0;
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;
        public bool HasIssues => _issues.Count > 0;

        public static MxSettingsApplyResult Succeeded()
        {
            return new MxSettingsApplyResult(Array.Empty<MxSettingsIssue>());
        }

        public static MxSettingsApplyResult Failed(IReadOnlyList<MxSettingsIssue> issues)
        {
            return new MxSettingsApplyResult(issues);
        }

        public static MxSettingsApplyResult Failed(MxSettingsIssue issue)
        {
            return Failed(new[] { issue });
        }

        public static MxSettingsApplyResult Combine(IReadOnlyList<MxSettingsApplyResult> results)
        {
            var issues = new List<MxSettingsIssue>();
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    IReadOnlyList<MxSettingsIssue> resultIssues = results[i]?.Issues;
                    if (resultIssues == null)
                        continue;

                    for (int j = 0; j < resultIssues.Count; j++)
                    {
                        if (!resultIssues[j].IsNone)
                            issues.Add(resultIssues[j]);
                    }
                }
            }

            return new MxSettingsApplyResult(issues);
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
