using System;

namespace MxFramework.Audio
{
    public sealed class AudioService : IAudioService, IDisposable
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

            if (!_definitions.TryGetParameter(handle.EventId, parameterId, out _))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidParameter, "Unknown audio parameter id " + parameterId + " for event " + handle.EventId + ".");
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

        public AudioDebugSnapshot CaptureSnapshot()
        {
            return _backend.CaptureSnapshot();
        }

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

            if (definition.BusId > 0 && !_definitions.TryGetBus(definition.BusId, out _))
            {
                return AudioResult.Fail(AudioErrorCode.InvalidBus, "Unknown audio bus id " + definition.BusId + " for event " + request.EventId + ".");
            }

            AudioParameterValue[] parameters = request.Parameters ?? AudioPlayRequest.EmptyParameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                int parameterId = parameters[i].ParameterId;
                if (!_definitions.TryGetParameter(request.EventId, parameterId, out _))
                {
                    return AudioResult.Fail(AudioErrorCode.InvalidParameter, "Unknown audio parameter id " + parameterId + " for event " + request.EventId + ".");
                }
            }

            return AudioResult.Ok();
        }
    }
}
