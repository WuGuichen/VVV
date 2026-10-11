using System;
using System.Collections.Generic;

namespace MxFramework.Audio.FMOD.Editor
{
    public sealed class FmodAudioLibrarySourceData
    {
        public bool IsAvailable { get; set; }
        public bool IsCacheValid { get; set; }
        public string UnavailableReason { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime CacheTimeUtc { get; set; } = DateTime.MinValue;
        public List<FmodAudioLibrarySourceBank> Banks { get; } = new List<FmodAudioLibrarySourceBank>();
        public List<FmodAudioLibrarySourceEvent> Events { get; } = new List<FmodAudioLibrarySourceEvent>();
        public List<FmodAudioLibrarySourceParameter> GlobalParameters { get; } = new List<FmodAudioLibrarySourceParameter>();

        public static FmodAudioLibrarySourceData Unavailable(string reason)
        {
            return new FmodAudioLibrarySourceData
            {
                IsAvailable = false,
                IsCacheValid = false,
                Source = "Unavailable",
                UnavailableReason = reason ?? string.Empty
            };
        }
    }

    public sealed class FmodAudioLibrarySourceBank
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string StudioPath { get; set; } = string.Empty;
        public DateTime LastModifiedUtc { get; set; } = DateTime.MinValue;
        public List<FmodAudioLibrarySourceBankFileSize> FileSizes { get; } = new List<FmodAudioLibrarySourceBankFileSize>();
    }

    public sealed class FmodAudioLibrarySourceBankFileSize
    {
        public string Platform { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public sealed class FmodAudioLibrarySourceEvent
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
        public List<string> Banks { get; } = new List<string>();
        public List<FmodAudioLibrarySourceParameter> Parameters { get; } = new List<FmodAudioLibrarySourceParameter>();
    }

    public sealed class FmodAudioLibrarySourceParameter
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
        public List<string> Labels { get; } = new List<string>();
    }
}
