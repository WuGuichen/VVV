using System.Collections.Generic;

namespace MxFramework.Audio
{
    public readonly struct AudioDebugActiveEvent
    {
        public AudioDebugActiveEvent(AudioHandle handle, string name, int busId, int emitterId)
        {
            Handle = handle;
            Name = name ?? string.Empty;
            BusId = busId;
            EmitterId = emitterId;
        }

        public AudioHandle Handle { get; }
        public string Name { get; }
        public int BusId { get; }
        public int EmitterId { get; }
    }

    public readonly struct AudioDebugBusState
    {
        public AudioDebugBusState(int busId, string name, float volume, bool muted)
        {
            BusId = busId;
            Name = name ?? string.Empty;
            Volume = volume;
            Muted = muted;
        }

        public int BusId { get; }
        public string Name { get; }
        public float Volume { get; }
        public bool Muted { get; }
    }

    public readonly struct AudioDebugError
    {
        public AudioDebugError(AudioErrorCode code, string message, long frame = 0, string traceId = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Frame = frame;
            TraceId = traceId ?? string.Empty;
        }

        public AudioErrorCode Code { get; }
        public string Message { get; }
        public long Frame { get; }
        public string TraceId { get; }
    }

    public sealed class AudioDebugSnapshot
    {
        private readonly List<AudioDebugActiveEvent> _activeEvents;
        private readonly List<AudioDebugBusState> _busStates;
        private readonly List<AudioDebugError> _recentErrors;

        public AudioDebugSnapshot(
            bool initialized,
            int totalPlayRequests,
            int totalStopRequests,
            int activeEventCount,
            IReadOnlyList<AudioDebugActiveEvent> activeEvents,
            IReadOnlyList<AudioDebugBusState> busStates,
            IReadOnlyList<AudioDebugError> recentErrors)
        {
            Initialized = initialized;
            TotalPlayRequests = totalPlayRequests;
            TotalStopRequests = totalStopRequests;
            ActiveEventCount = activeEventCount;
            _activeEvents = activeEvents != null ? new List<AudioDebugActiveEvent>(activeEvents) : new List<AudioDebugActiveEvent>();
            _busStates = busStates != null ? new List<AudioDebugBusState>(busStates) : new List<AudioDebugBusState>();
            _recentErrors = recentErrors != null ? new List<AudioDebugError>(recentErrors) : new List<AudioDebugError>();
        }

        public bool Initialized { get; }
        public int TotalPlayRequests { get; }
        public int TotalStopRequests { get; }
        public int ActiveEventCount { get; }
        public IReadOnlyList<AudioDebugActiveEvent> ActiveEvents => _activeEvents;
        public IReadOnlyList<AudioDebugBusState> BusStates => _busStates;
        public IReadOnlyList<AudioDebugError> RecentErrors => _recentErrors;

        public static AudioDebugSnapshot Empty => new AudioDebugSnapshot(false, 0, 0, 0, null, null, null);
    }
}
