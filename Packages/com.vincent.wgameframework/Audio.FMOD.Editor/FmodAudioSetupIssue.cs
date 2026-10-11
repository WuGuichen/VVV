namespace MxFramework.Audio.FMOD.Editor
{
    public enum FmodAudioSetupIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct FmodAudioSetupIssue
    {
        public FmodAudioSetupIssue(FmodAudioSetupIssueSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public FmodAudioSetupIssueSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
    }
}
