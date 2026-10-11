using System.Collections.Generic;

namespace MxFramework.Audio.FMOD.Editor
{
    public sealed class FmodAudioLibrarySnapshot
    {
        public int schemaVersion = 1;
        public string generatedAtUtc = string.Empty;
        public string source = string.Empty;
        public string cacheTimeUtc = string.Empty;
        public bool cacheValid;
        public bool cacheStale;
        public List<FmodAudioLibraryBank> banks = new List<FmodAudioLibraryBank>();
        public List<FmodAudioLibraryEvent> events = new List<FmodAudioLibraryEvent>();
        public List<FmodAudioLibraryParameter> globalParameters = new List<FmodAudioLibraryParameter>();
        public List<FmodAudioLibraryDiagnostic> diagnostics = new List<FmodAudioLibraryDiagnostic>();
    }

    public sealed class FmodAudioLibraryBank
    {
        public string name = string.Empty;
        public string path = string.Empty;
        public string studioPath = string.Empty;
        public string lastModifiedUtc = string.Empty;
        public List<FmodAudioLibraryBankFileSize> fileSizes = new List<FmodAudioLibraryBankFileSize>();
    }

    public sealed class FmodAudioLibraryBankFileSize
    {
        public string platform = string.Empty;
        public long sizeBytes;
    }

    public sealed class FmodAudioLibraryEvent
    {
        public string path = string.Empty;
        public string guid = string.Empty;
        public string kind = "Event";
        public bool is3D;
        public bool isLoop;
        public bool isStream;
        public float minDistance;
        public float maxDistance;
        public int lengthMs;
        public List<string> banks = new List<string>();
        public List<FmodAudioLibraryParameter> parameters = new List<FmodAudioLibraryParameter>();
    }

    public sealed class FmodAudioLibraryParameter
    {
        public string name = string.Empty;
        public string studioPath = string.Empty;
        public uint idData1;
        public uint idData2;
        public string kind = "Continuous";
        public float defaultValue;
        public float minValue;
        public float maxValue = 1f;
        public bool isGlobal;
        public List<string> labels = new List<string>();
    }

    public sealed class FmodAudioLibraryDiagnostic
    {
        public string severity = string.Empty;
        public string code = string.Empty;
        public string message = string.Empty;
        public string eventPath = string.Empty;
        public string eventGuid = string.Empty;
        public string bankName = string.Empty;
        public string parameterName = string.Empty;
        public string suggestedFix = string.Empty;
    }
}
