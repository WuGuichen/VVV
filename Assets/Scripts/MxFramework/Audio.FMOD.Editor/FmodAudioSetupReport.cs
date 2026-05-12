using System.Collections.Generic;

namespace MxFramework.Audio.FMOD.Editor
{
    public sealed class FmodAudioSetupReport
    {
        private readonly List<FmodAudioSetupIssue> _issues = new List<FmodAudioSetupIssue>();

        public IReadOnlyList<FmodAudioSetupIssue> Issues => _issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void AddInfo(string code, string message)
        {
            Add(FmodAudioSetupIssueSeverity.Info, code, message);
        }

        public void AddWarning(string code, string message)
        {
            Add(FmodAudioSetupIssueSeverity.Warning, code, message);
        }

        public void AddError(string code, string message)
        {
            Add(FmodAudioSetupIssueSeverity.Error, code, message);
        }

        private void Add(FmodAudioSetupIssueSeverity severity, string code, string message)
        {
            _issues.Add(new FmodAudioSetupIssue(severity, code, message));
            if (severity == FmodAudioSetupIssueSeverity.Error)
            {
                ErrorCount++;
            }
            else if (severity == FmodAudioSetupIssueSeverity.Warning)
            {
                WarningCount++;
            }
        }
    }
}
