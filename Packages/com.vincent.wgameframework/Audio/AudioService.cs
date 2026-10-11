using System;

namespace MxFramework.Audio
{
    public sealed class AudioService : IAudioService, IAudioHandleStateProvider, IAudioSpatialControl, IAudioPauseControl, IAudioPerformanceDiagnostics, IDisposable
    {
        private readonly IAudioDefinitionProvider _definitions;
        private readonly IAudioBackend _backend;
        private bool _initialized;

        public AudioService(IAudioDefinitionProvider definitions, IAudioBackend backend)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));

            AudioResult result = _backend.Initialize(_definitions);
            _initialized = result.Success;
        }

        public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
        {
            AudioResult validation = ValidateRequest(request, AudioPlayMode.OneShot, out AudioPlayRequest backendRequest);
            if (validation.Failed)
            {
                return AudioPlayResult.Fail(validation.ErrorCode, validation.Message);
            }

            AudioHandle ignored;
            AudioPlayResult result = _backend.Play(backendRequest, out ignored);
            return result.Success ? AudioPlayResult.Ok(AudioHandle.Invalid) : result;
        }

        public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle)
        {
            handle = AudioHandle.Invalid;
            AudioResult validation = ValidateRequest(request, AudioPlayMode.StartEvent, out AudioPlayRequest backendRequest);
            if (validation.Failed)
            {
                return AudioPlayResult.Fail(validation.ErrorCode, validation.Message);
            }

            return _backend.Play(backendRequest, out handle);
        }

        public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
        {
            if (!_initialized)
            {
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            }

            if (!handle.IsValid)
            {
                return AudioResult.Fail(AudioErrorCode.InvalidHandle, "Audio handle is invalid.");
            }

            return _backend.Stop(handle, stopMode);
        }

        public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
        {
            if (!_initialized)
            {
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            }

            if (!handle.IsValid)
            {
                return AudioResult.Fail(AudioErrorCode.InvalidHandle, "Audio handle is invalid.");
            }

            if (!_definitions.TryGetParameter(handle.EventId, parameterId, out AudioParameterDefinition definition))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidParameter, "Unknown audio parameter id " + parameterId + " for event " + handle.EventId + ".");
            }

            AudioResult valueValidation = ValidateParameterValue(definition, value, handle.EventId);
            if (valueValidation.Failed)
            {
                return valueValidation;
            }

            return _backend.SetParameter(handle, parameterId, value);
        }

        public AudioResult SetBusVolume(int busId, float volume)
        {
            if (!_initialized)
            {
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            }

            if (!_definitions.TryGetBus(busId, out _))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".");
            }

            return _backend.SetBusVolume(busId, volume);
        }

        public AudioResult SetBusMuted(int busId, bool muted)
        {
            if (!_initialized)
            {
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            }

            if (!_definitions.TryGetBus(busId, out _))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".");
            }

            return _backend.SetBusMuted(busId, muted);
        }

        /// <remarks>NoAlloc.</remarks>
        public bool IsHandleActive(AudioHandle handle)
        {
            return _initialized
                && handle.IsValid
                && _backend is IAudioHandleStateProvider provider
                && provider.IsHandleActive(handle);
        }

        /// <remarks>NoAlloc on success; invalid-call diagnostics may allocate.</remarks>
        public AudioResult Update3DAttributes(AudioHandle handle, in AudioTransform transform)
        {
            if (!_initialized)
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            if (!handle.IsValid || !IsHandleActive(handle))
                return AudioResult.Fail(AudioErrorCode.InvalidHandle, "Audio handle is not active.");
            if (!_definitions.TryGetEvent(handle.EventId, out AudioEventDefinition definition) || !definition.Is3D)
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Only controlled 3D events accept emitter updates.");
            if (!(_backend is IAudioSpatialControl spatial))
                return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "Audio backend does not support spatial updates.");
            return spatial.Update3DAttributes(handle, transform);
        }

        /// <remarks>NoAlloc on success; invalid-call diagnostics may allocate.</remarks>
        public AudioResult SetListenerAttributes(int listenerIndex, in AudioTransform transform, float weight = 1f)
        {
            if (!_initialized)
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            if (!(_backend is IAudioSpatialControl spatial))
                return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "Audio backend does not support listener updates.");
            return spatial.SetListenerAttributes(listenerIndex, transform, weight);
        }

        /// <remarks>NoAlloc on success; invalid-call diagnostics may allocate.</remarks>
        public AudioResult SetBusPaused(int busId, bool paused)
        {
            if (!_initialized)
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            if (!_definitions.TryGetBus(busId, out _))
                return AudioResult.Fail(AudioErrorCode.InvalidBus, "Unknown audio bus id " + busId + ".");
            if (!(_backend is IAudioPauseControl pauseControl))
                return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "Audio backend does not support bus pause state.");
            return pauseControl.SetBusPaused(busId, paused);
        }

        public AudioDebugSnapshot CaptureSnapshot()
        {
            return _backend.CaptureSnapshot();
        }

        /// <remarks>AllocByDesign diagnostics path. Passing true may synchronously flush backend commands.</remarks>
        public AudioBackendPerformanceMetrics CapturePerformanceMetrics(bool flushCommands = false)
        {
            return _backend is IAudioPerformanceDiagnostics diagnostics
                ? diagnostics.CapturePerformanceMetrics(flushCommands)
                : default;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(float deltaTime)
        {
            if (_initialized)
            {
                _backend.Tick(deltaTime);
            }
        }

        public void Dispose()
        {
            _backend.Dispose();
            _initialized = false;
        }

        private AudioResult ValidateRequest(in AudioPlayRequest request, AudioPlayMode playMode, out AudioPlayRequest backendRequest)
        {
            backendRequest = request.WithPlayMode(playMode);
            if (!_initialized)
            {
                return AudioResult.Fail(AudioErrorCode.NotInitialized, "Audio service is not initialized.");
            }

            if (!_definitions.TryGetEvent(request.EventId, out AudioEventDefinition definition))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidEvent, "Unknown audio event id " + request.EventId + ".");
            }

            if (playMode == AudioPlayMode.OneShot && definition.IsLoop)
            {
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Looping audio event requires StartEvent: " + request.EventId + ".");
            }

            if (definition.Is3D != (request.SpatialMode == AudioSpatialMode.ThreeDimensional))
            {
                return AudioResult.Fail(
                    AudioErrorCode.RequestRejected,
                    definition.Is3D
                        ? "3D audio event requires AudioPlayRequest.Create3D: " + request.EventId + "."
                        : "2D audio event requires AudioPlayRequest.Create2D: " + request.EventId + ".");
            }

            if (definition.BusId > 0 && !_definitions.TryGetBus(definition.BusId, out _))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidBus, "Unknown audio bus id " + definition.BusId + " for event " + request.EventId + ".");
            }

            System.Collections.Generic.IReadOnlyList<AudioParameterValue> parameters = request.Parameters ?? AudioPlayRequest.EmptyParameters;
            for (int i = 0; i < request.ParameterCount; i++)
            {
                int parameterId = parameters[i].ParameterId;
                if (!_definitions.TryGetParameter(request.EventId, parameterId, out AudioParameterDefinition parameterDefinition))
                {
                    return AudioResult.Fail(AudioErrorCode.InvalidParameter, "Unknown audio parameter id " + parameterId + " for event " + request.EventId + ".");
                }

                AudioResult valueValidation = ValidateParameterValue(parameterDefinition, parameters[i].Value, request.EventId);
                if (valueValidation.Failed)
                {
                    return valueValidation;
                }
                for (int previous = 0; previous < i; previous++)
                {
                    if (parameters[previous].ParameterId == parameterId)
                    {
                        return AudioResult.Fail(
                            AudioErrorCode.InvalidParameter,
                            "Duplicate audio parameter id " + parameterId + " for event " + request.EventId + ".");
                    }
                }
            }

            return AudioResult.Ok();
        }

        private static AudioResult ValidateParameterValue(in AudioParameterDefinition definition, float value, int eventId)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return AudioResult.Fail(
                    AudioErrorCode.InvalidParameter,
                    "Audio parameter " + definition.Id + " for event " + eventId + " must be finite.");
            }
            if (value < definition.MinValue || value > definition.MaxValue)
            {
                return AudioResult.Fail(
                    AudioErrorCode.InvalidParameter,
                    "Audio parameter " + definition.Id + " for event " + eventId + " is outside [" + definition.MinValue + ", " + definition.MaxValue + "].");
            }
            if (definition.Kind != AudioParameterKind.Continuous && value != (float)Math.Round(value))
            {
                return AudioResult.Fail(
                    AudioErrorCode.InvalidParameter,
                    "Discrete or labeled audio parameter " + definition.Id + " for event " + eventId + " requires an integer value.");
            }
            return AudioResult.Ok();
        }
    }
}
