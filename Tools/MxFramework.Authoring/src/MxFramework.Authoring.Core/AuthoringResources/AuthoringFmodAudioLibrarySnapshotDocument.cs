using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class AuthoringFmodAudioLibrarySnapshotDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string CacheTimeUtc { get; set; } = string.Empty;
        public bool CacheValid { get; set; }
        public bool CacheStale { get; set; }
        public List<AuthoringFmodAudioLibraryBank> Banks { get; set; } = new List<AuthoringFmodAudioLibraryBank>();
        public List<AuthoringFmodAudioLibraryEvent> Events { get; set; } = new List<AuthoringFmodAudioLibraryEvent>();
        public List<AuthoringFmodAudioLibraryParameter> GlobalParameters { get; set; } = new List<AuthoringFmodAudioLibraryParameter>();
        public List<AuthoringFmodAudioLibraryDiagnostic> Diagnostics { get; set; } = new List<AuthoringFmodAudioLibraryDiagnostic>();
    }

    public sealed class AuthoringFmodAudioLibraryBank
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string StudioPath { get; set; } = string.Empty;
        public string LastModifiedUtc { get; set; } = string.Empty;
        public List<AuthoringFmodAudioLibraryBankFileSize> FileSizes { get; set; } = new List<AuthoringFmodAudioLibraryBankFileSize>();
    }

    public sealed class AuthoringFmodAudioLibraryBankFileSize
    {
        public string Platform { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public sealed class AuthoringFmodAudioLibraryEvent
    {
        public string Path { get; set; } = string.Empty;
        public string Guid { get; set; } = string.Empty;
        public string Kind { get; set; } = "Event";
        public bool Is3D { get; set; }
        public bool IsLoop { get; set; }
        public bool IsStream { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public int LengthMs { get; set; }
        public List<string> Banks { get; set; } = new List<string>();
        public List<AuthoringFmodAudioLibraryParameter> Parameters { get; set; } = new List<AuthoringFmodAudioLibraryParameter>();
    }

    public sealed class AuthoringFmodAudioLibraryParameter
    {
        public string Name { get; set; } = string.Empty;
        public string StudioPath { get; set; } = string.Empty;
        public uint IdData1 { get; set; }
        public uint IdData2 { get; set; }
        public string Kind { get; set; } = "Continuous";
        public float DefaultValue { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; } = 1f;
        public bool IsGlobal { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
    }

    public sealed class AuthoringFmodAudioLibraryDiagnostic
    {
        public string Severity { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EventPath { get; set; } = string.Empty;
        public string EventGuid { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }
}
