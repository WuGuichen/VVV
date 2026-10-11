using System;
using System.Collections.Generic;

namespace MxFramework.Audio
{
    public sealed class NullAudioBackend : IAudioBackend, IAudioHandleStateProvider, IAudioSpatialControl, IAudioPauseControl
    {
        private const int MaxRecentErrors = 16;
        private readonly Dictionary<int, AudioHandle> _handles = new Dictionary<int, AudioHandle>();
        private readonly Dictionary<int, AudioDebugBusState> _busStates = new Dictionary<int, AudioDebugBusState>();
        private readonly List<AudioDebugError> _recentErrors = new List<AudioDebugError>();
        private IAudioDefinitionProvider _definitions;
        private int _nextHandleId = 1;
        private int _totalPlayRequests;
        private int _totalStopRequests;
        private bool _initialized;

        public AudioResult Initialize(IAudioDefinitionProvider definitions)
        {
            if (definitions == null)
            {
                return RecordAndReturn(AudioErrorCode.BackendUnavailable, "Audio definitions are required.", 0, AudioTraceIdentity.None);
            }

            _definitions = definitions;
            _initialized = true;
            return AudioResult.Ok();
        }

        public AudioPlayResult Play(in AudioPlayRequest request, out AudioHandle handle)
        {
            handle = AudioHandle.Invalid;
            if (!_initialized)
            {
                return RecordAndReturnPlay(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", request.Frame, request.TraceId);
            }

            if (!_definitions.TryGetEvent(request.EventId, out AudioEventDefinition definition))
            {
                return RecordAndReturnPlay(AudioErrorCode.InvalidEvent, "Unknown audio event id " + request.EventId + ".", request.Frame, request.TraceId);
            }

            _totalPlayRequests++;
            handle = new AudioHandle(_nextHandleId++, request.EventId, request.EmitterId, AudioHandleState.Playing);

            if (definition.IsLoop || request.PlayMode == AudioPlayMode.StartEvent)
            {
                _handles[handle.Id] = handle;
            }

            return AudioPlayResult.Ok(handle);
        }

        public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
        {
            if (!_initialized)
            {
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            _totalStopRequests++;
            if (!handle.IsValid)
            {
                return RecordAndReturn(AudioErrorCode.InvalidHandle, "Audio handle is invalid.", 0, AudioTraceIdentity.None);
            }

            if (_handles.ContainsKey(handle.Id))
            {
                _handles.Remove(handle.Id);
            }

            return AudioResult.Ok();
        }

        public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
        {
            if (!_initialized)
            {
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            if (!handle.IsValid)
            {
                return RecordAndReturn(AudioErrorCode.InvalidHandle, "Audio handle is invalid.", 0, AudioTraceIdentity.None);
            }

            if (!_definitions.TryGetParameter(handle.EventId, parameterId, out _))
            {
                return RecordAndReturn(AudioErrorCode.InvalidParameter, "Unknown audio parameter id " + parameterId + " for event " + handle.EventId + ".", 0, AudioTraceIdentity.None);
            }

            return AudioResult.Ok();
        }

        public AudioResult SetBusVolume(int busId, float volume)
        {
            if (!_initialized)
            {
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            if (!_definitions.TryGetBus(busId, out AudioBusDefinition bus))
            {
                return RecordAndReturn(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".", 0, AudioTraceIdentity.None);
            }

            bool muted = _busStates.TryGetValue(busId, out AudioDebugBusState current) && current.Muted;
            bool paused = _busStates.TryGetValue(busId, out current) && current.Paused;
            _busStates[busId] = new AudioDebugBusState(busId, bus.Name, Clamp01(volume), muted, paused);
            return AudioResult.Ok();
        }

        public AudioResult SetBusMuted(int busId, bool muted)
        {
            if (!_initialized)
            {
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            if (!_definitions.TryGetBus(busId, out AudioBusDefinition bus))
            {
                return RecordAndReturn(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".", 0, AudioTraceIdentity.None);
            }

            float volume = _busStates.TryGetValue(busId, out AudioDebugBusState current) ? current.Volume : Clamp01(bus.DefaultVolume);
            bool paused = _busStates.TryGetValue(busId, out current) && current.Paused;
            _busStates[busId] = new AudioDebugBusState(busId, bus.Name, volume, muted, paused);
            return AudioResult.Ok();
        }

        public AudioResult SetBusPaused(int busId, bool paused)
        {
            if (!_initialized)
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, AudioTraceIdentity.None);
            if (!_definitions.TryGetBus(busId, out AudioBusDefinition bus))
                return RecordAndReturn(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".", 0, AudioTraceIdentity.None);
            AudioDebugBusState current = _busStates.TryGetValue(busId, out AudioDebugBusState state)
                ? state
                : new AudioDebugBusState(busId, bus.Name, Clamp01(bus.DefaultVolume), bus.DefaultMuted);
            _busStates[busId] = new AudioDebugBusState(busId, bus.Name, current.Volume, current.Muted, paused);
            return AudioResult.Ok();
        }

        public bool IsHandleActive(AudioHandle handle)
        {
            return _initialized && handle.IsValid && _handles.ContainsKey(handle.Id);
        }

        public AudioResult Update3DAttributes(AudioHandle handle, in AudioTransform transform)
        {
            return IsHandleActive(handle)
                ? AudioResult.Ok()
                : RecordAndReturn(AudioErrorCode.InvalidHandle, "Audio handle is not active.", 0, AudioTraceIdentity.None);
        }

        public AudioResult SetListenerAttributes(int listenerIndex, in AudioTransform transform, float weight = 1f)
        {
            return _initialized
                ? AudioResult.Ok()
                : RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, AudioTraceIdentity.None);
        }

        public AudioDebugSnapshot CaptureSnapshot()
        {
            var activeEvents = new List<AudioDebugActiveEvent>(_handles.Count);
            foreach (AudioHandle handle in _handles.Values)
            {
                string name = string.Empty;
                int busId = 0;
                if (_definitions != null && _definitions.TryGetEvent(handle.EventId, out AudioEventDefinition definition))
                {
                    name = definition.Name;
                    busId = definition.BusId;
                }

                activeEvents.Add(new AudioDebugActiveEvent(handle, name, busId, handle.EmitterId));
            }

            return new AudioDebugSnapshot(
                _initialized,
                _totalPlayRequests,
                _totalStopRequests,
                _handles.Count,
                activeEvents,
                new List<AudioDebugBusState>(_busStates.Values),
                _recentErrors);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
            _handles.Clear();
            _busStates.Clear();
            _initialized = false;
            _definitions = null;
        }

        private AudioResult RecordAndReturn(AudioErrorCode code, string message, long frame, AudioTraceIdentity traceId)
        {
            RecordError(code, message, frame, traceId);
            return AudioResult.Fail(code, message);
        }

        private AudioPlayResult RecordAndReturnPlay(AudioErrorCode code, string message, long frame, AudioTraceIdentity traceId)
        {
            RecordError(code, message, frame, traceId);
            return AudioPlayResult.Fail(code, message);
        }

        private void RecordError(AudioErrorCode code, string message, long frame, AudioTraceIdentity traceId)
        {
            if (_recentErrors.Count == MaxRecentErrors)
            {
                _recentErrors.RemoveAt(0);
            }

            _recentErrors.Add(new AudioDebugError(code, message, frame, traceId));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
