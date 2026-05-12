using System;
using System.Collections.Generic;

namespace MxFramework.Audio
{
    public sealed class NullAudioBackend : IAudioBackend
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
                return RecordAndReturn(AudioErrorCode.BackendUnavailable, "Audio definitions are required.", 0, string.Empty);
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
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, string.Empty);
            }

            _totalStopRequests++;
            if (!handle.IsValid)
            {
                return RecordAndReturn(AudioErrorCode.InvalidHandle, "Audio handle is invalid.", 0, string.Empty);
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
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, string.Empty);
            }

            if (!handle.IsValid)
            {
                return RecordAndReturn(AudioErrorCode.InvalidHandle, "Audio handle is invalid.", 0, string.Empty);
            }

            if (!_definitions.TryGetParameter(handle.EventId, parameterId, out _))
            {
                return RecordAndReturn(AudioErrorCode.InvalidParameter, "Unknown audio parameter id " + parameterId + " for event " + handle.EventId + ".", 0, string.Empty);
            }

            return AudioResult.Ok();
        }

        public AudioResult SetBusVolume(int busId, float volume)
        {
            if (!_initialized)
            {
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, string.Empty);
            }

            if (!_definitions.TryGetBus(busId, out AudioBusDefinition bus))
            {
                return RecordAndReturn(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".", 0, string.Empty);
            }

            bool muted = _busStates.TryGetValue(busId, out AudioDebugBusState current) && current.Muted;
            _busStates[busId] = new AudioDebugBusState(busId, bus.Name, Clamp01(volume), muted);
            return AudioResult.Ok();
        }

        public AudioResult SetBusMuted(int busId, bool muted)
        {
            if (!_initialized)
            {
                return RecordAndReturn(AudioErrorCode.NotInitialized, "Null audio backend is not initialized.", 0, string.Empty);
            }

            if (!_definitions.TryGetBus(busId, out AudioBusDefinition bus))
            {
                return RecordAndReturn(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".", 0, string.Empty);
            }

            float volume = _busStates.TryGetValue(busId, out AudioDebugBusState current) ? current.Volume : Clamp01(bus.DefaultVolume);
            _busStates[busId] = new AudioDebugBusState(busId, bus.Name, volume, muted);
            return AudioResult.Ok();
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

        private AudioResult RecordAndReturn(AudioErrorCode code, string message, long frame, string traceId)
        {
            RecordError(code, message, frame, traceId);
            return AudioResult.Fail(code, message);
        }

        private AudioPlayResult RecordAndReturnPlay(AudioErrorCode code, string message, long frame, string traceId)
        {
            RecordError(code, message, frame, traceId);
            return AudioPlayResult.Fail(code, message);
        }

        private void RecordError(AudioErrorCode code, string message, long frame, string traceId)
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
