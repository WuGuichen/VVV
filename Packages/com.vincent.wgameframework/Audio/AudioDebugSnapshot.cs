using System.Collections.Generic;

namespace MxFramework.Audio
{
    public readonly struct AudioBackendPerformanceMetrics
    {
        public AudioBackendPerformanceMetrics(
            int channels,
            int realChannels,
            int memoryBytes,
            int sampleMemoryBytes,
            float studioUpdateCpuPercent,
            float dspCpuPercent,
            float streamCpuPercent,
            float coreUpdateCpuPercent)
        {
            Channels = channels;
            RealChannels = realChannels;
            MemoryBytes = memoryBytes;
            SampleMemoryBytes = sampleMemoryBytes;
            StudioUpdateCpuPercent = studioUpdateCpuPercent;
            DspCpuPercent = dspCpuPercent;
            StreamCpuPercent = streamCpuPercent;
            CoreUpdateCpuPercent = coreUpdateCpuPercent;
        }

        public int Channels { get; }
        public int RealChannels { get; }
        public int MemoryBytes { get; }
        public int SampleMemoryBytes { get; }
        public float StudioUpdateCpuPercent { get; }
        public float DspCpuPercent { get; }
        public float StreamCpuPercent { get; }
        public float CoreUpdateCpuPercent { get; }
    }

    public readonly struct AudioDebugActiveEvent
    {
        public AudioDebugActiveEvent(AudioHandle handle, string name, int busId, int emitterId, int priority = 0, bool controlled = false, float ageSeconds = 0f)
        {
            Handle = handle;
            Name = name ?? string.Empty;
            BusId = busId;
            EmitterId = emitterId;
            Priority = priority;
            Controlled = controlled;
            AgeSeconds = ageSeconds;
        }

        public AudioHandle Handle { get; }
        public string Name { get; }
        public int BusId { get; }
        public int EmitterId { get; }
        public int Priority { get; }
        public bool Controlled { get; }
        public float AgeSeconds { get; }
    }

    public readonly struct AudioDebugBusState
    {
        public AudioDebugBusState(int busId, string name, float volume, bool muted, bool paused = false, float effectiveVolume = -1f)
        {
            BusId = busId;
            Name = name ?? string.Empty;
            Volume = volume;
            Muted = muted;
            Paused = paused;
            EffectiveVolume = effectiveVolume < 0f ? volume : effectiveVolume;
        }

        public int BusId { get; }
        public string Name { get; }
        public float Volume { get; }
        public bool Muted { get; }
        public bool Paused { get; }
        public float EffectiveVolume { get; }
    }

    public readonly struct AudioDebugError
    {
        public AudioDebugError(AudioErrorCode code, string message, long frame = 0, AudioTraceIdentity traceId = default)
        {
            Code = code;
            Message = message ?? string.Empty;
            Frame = frame;
            TraceId = traceId;
        }

        public AudioErrorCode Code { get; }
        public string Message { get; }
        public long Frame { get; }
        public AudioTraceIdentity TraceId { get; }
    }

    public readonly struct AudioDebugListenerState
    {
        public AudioDebugListenerState(int index, in AudioTransform transform, float weight)
        {
            Index = index;
            Transform = transform;
            Weight = weight;
        }

        public int Index { get; }
        public AudioTransform Transform { get; }
        public float Weight { get; }
    }

    public sealed class AudioDebugSnapshot
    {
        private readonly List<AudioDebugActiveEvent> _activeEvents;
        private readonly List<AudioDebugBusState> _busStates;
        private readonly List<AudioDebugError> _recentErrors;
        private readonly List<AudioBankRuntimeState> _banks;
        private readonly List<AudioDebugListenerState> _listeners;
        private readonly List<string> _recentDecisions;

        public AudioDebugSnapshot(
            bool initialized,
            int totalPlayRequests,
            int totalStopRequests,
            int activeEventCount,
            IReadOnlyList<AudioDebugActiveEvent> activeEvents,
            IReadOnlyList<AudioDebugBusState> busStates,
            IReadOnlyList<AudioDebugError> recentErrors,
            AudioBackendPerformanceMetrics performanceMetrics = default,
            int totalVoiceRejections = 0,
            int totalVoiceSteals = 0,
            IReadOnlyList<AudioBankRuntimeState> banks = null,
            IReadOnlyList<AudioDebugListenerState> listeners = null,
            IReadOnlyList<string> recentDecisions = null)
        {
            Initialized = initialized;
            TotalPlayRequests = totalPlayRequests;
            TotalStopRequests = totalStopRequests;
            ActiveEventCount = activeEventCount;
            _activeEvents = activeEvents != null ? new List<AudioDebugActiveEvent>(activeEvents) : new List<AudioDebugActiveEvent>();
            _busStates = busStates != null ? new List<AudioDebugBusState>(busStates) : new List<AudioDebugBusState>();
            _recentErrors = recentErrors != null ? new List<AudioDebugError>(recentErrors) : new List<AudioDebugError>();
            _banks = banks != null ? new List<AudioBankRuntimeState>(banks) : new List<AudioBankRuntimeState>();
            _listeners = listeners != null ? new List<AudioDebugListenerState>(listeners) : new List<AudioDebugListenerState>();
            _recentDecisions = recentDecisions != null ? new List<string>(recentDecisions) : new List<string>();
            PerformanceMetrics = performanceMetrics;
            TotalVoiceRejections = totalVoiceRejections;
            TotalVoiceSteals = totalVoiceSteals;
        }

        public bool Initialized { get; }
        public int TotalPlayRequests { get; }
        public int TotalStopRequests { get; }
        public int ActiveEventCount { get; }
        public IReadOnlyList<AudioDebugActiveEvent> ActiveEvents => _activeEvents;
        public IReadOnlyList<AudioDebugBusState> BusStates => _busStates;
        public IReadOnlyList<AudioDebugError> RecentErrors => _recentErrors;
        public AudioBackendPerformanceMetrics PerformanceMetrics { get; }
        public int TotalVoiceRejections { get; }
        public int TotalVoiceSteals { get; }
        public IReadOnlyList<AudioBankRuntimeState> Banks => _banks;
        public IReadOnlyList<AudioDebugListenerState> Listeners => _listeners;
        public IReadOnlyList<string> RecentDecisions => _recentDecisions;

        public static AudioDebugSnapshot Empty => new AudioDebugSnapshot(false, 0, 0, 0, null, null, null);
    }
}
