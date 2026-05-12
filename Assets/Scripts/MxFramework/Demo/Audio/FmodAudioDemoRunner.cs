using MxFramework.Audio;
using MxFramework.Audio.FMOD;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Demo
{
    [AddComponentMenu("MxFramework/Audio/FMOD Audio Demo Runner")]
    public sealed class FmodAudioDemoRunner : MonoBehaviour
    {
        private const int DemoBusId = 500001;
        private const int OneShotEventId = 500101;
        private const int LoopEventId = 500102;
        private const int DemoParameterId = 500201;

        [Header("FMOD Events")]
        [SerializeField] private string _oneShotEventPath = "event:/MxFramework/Demo/OneShot";
        [SerializeField] private string _oneShotEventGuid;
        [SerializeField] private bool _oneShotIs3D;
        [SerializeField] private string _loopEventPath = "event:/MxFramework/Demo/Loop";
        [SerializeField] private string _loopEventGuid;
        [SerializeField] private bool _loopIs3D;

        [Header("FMOD Bus")]
        [SerializeField] private string _busPath = "bus:/";
        [SerializeField] private string _vcaPath;
        [SerializeField, Range(0f, 1f)] private float _busVolume = 1f;

        [Header("Parameter")]
        [SerializeField] private bool _useParameter;
        [SerializeField] private string _parameterName = "Intensity";
        [SerializeField, Range(0f, 1f)] private float _parameterValue = 1f;

        [Header("Runtime")]
        [SerializeField] private bool _initializeOnAwake = true;
        [SerializeField] private bool _playOneShotOnStart;
        [SerializeField] private bool _startLoopOnStart;
        [SerializeField] private AudioStopMode _loopStopMode = AudioStopMode.AllowFadeout;

        private AudioService _audioService;
        private RuntimeHost _runtimeHost;
        private IInputProvider _input;
        private AudioHandle _loopHandle;
        private long _frameIndex;
        private double _elapsedTime;
        private string _lastResult = "Audio demo not initialized.";

        public string LastResult => _lastResult;
        public AudioDebugSnapshot Snapshot => _audioService != null ? _audioService.CaptureSnapshot() : AudioDebugSnapshot.Empty;

        private void Awake()
        {
            ResolveInput();
            if (_initializeOnAwake)
            {
                InitializeAudio();
            }
        }

        private void Start()
        {
            if (_playOneShotOnStart)
            {
                PlayOneShot();
            }

            if (_startLoopOnStart)
            {
                StartLoop();
            }
        }

        private void Update()
        {
            ResolveInput();
            if (_runtimeHost != null && _runtimeHost.State == RuntimeLifecycleState.Started)
            {
                float deltaTime = Time.deltaTime;
                _elapsedTime += deltaTime;
                _runtimeHost.Tick(_frameIndex++, deltaTime, _elapsedTime);
            }

            InputSnapshot input = _input != null ? _input.Snapshot : InputSnapshot.Empty;
            if (input.AudioPrimaryPressed)
            {
                PlayOneShot();
            }

            if (input.AudioSecondaryPressed)
            {
                ToggleLoop();
            }
        }

        private void ResolveInput()
        {
            if (_input == null)
                _input = InputProviderResolver.ResolveOrCreateDefault(this);
        }

        private void OnDestroy()
        {
            DisposeAudio();
        }

        [ContextMenu("Initialize Audio")]
        public void InitializeAudio()
        {
            DisposeAudio();

            var definitions = new DemoAudioDefinitions(
                CreateBusDefinition(),
                CreateEventDefinition(OneShotEventId, "demo.one_shot", _oneShotEventPath, _oneShotEventGuid, _oneShotIs3D, false),
                CreateEventDefinition(LoopEventId, "demo.loop", _loopEventPath, _loopEventGuid, _loopIs3D, true),
                CreateParameterDefinition());
            var backend = new FmodAudioBackend(new FmodAudioBackendOptions
            {
                FailOnMissingBus = false,
                PreloadBusIds = string.IsNullOrEmpty(_busPath) && string.IsNullOrEmpty(_vcaPath) ? new int[0] : new[] { DemoBusId }
            });

            _audioService = new AudioService(definitions, backend);
            var services = new RuntimeServiceRegistry();
            services.Register<IAudioService>(_audioService);
            _runtimeHost = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue,
                Services = services
            });
            _runtimeHost.RegisterModule(new AudioRuntimeModule());
            _runtimeHost.Initialize();
            _runtimeHost.Start();
            _frameIndex = 0L;
            _elapsedTime = 0d;

            AudioDebugSnapshot snapshot = _audioService.CaptureSnapshot();
            _lastResult = snapshot.Initialized ? "Audio initialized." : "Audio backend unavailable.";
            Debug.Log("[MxAudioDemo] " + _lastResult);
        }

        [ContextMenu("Play One Shot")]
        public void PlayOneShot()
        {
            EnsureAudio();
            AudioPlayResult result = _audioService.PlayOneShot(CreateRequest(OneShotEventId, _oneShotIs3D, "demo.one_shot"));
            Report("Play one-shot", result.Result);
        }

        [ContextMenu("Start Loop")]
        public void StartLoop()
        {
            EnsureAudio();
            if (_loopHandle.IsValid)
            {
                Report("Start loop", AudioResult.Ok(), "already playing handle=" + _loopHandle.Id);
                return;
            }

            AudioPlayResult result = _audioService.StartEvent(CreateRequest(LoopEventId, _loopIs3D, "demo.loop"), out _loopHandle);
            Report("Start loop", result.Result, result.Success ? "handle=" + _loopHandle.Id : null);
        }

        [ContextMenu("Stop Loop")]
        public void StopLoop()
        {
            EnsureAudio();
            if (!_loopHandle.IsValid)
            {
                Report("Stop loop", AudioResult.Fail(AudioErrorCode.InvalidHandle, "Loop is not playing."));
                return;
            }

            AudioResult result = _audioService.Stop(_loopHandle, _loopStopMode);
            if (result.Success)
            {
                _loopHandle = AudioHandle.Invalid;
            }

            Report("Stop loop", result);
        }

        [ContextMenu("Toggle Loop")]
        public void ToggleLoop()
        {
            if (_loopHandle.IsValid)
            {
                StopLoop();
            }
            else
            {
                StartLoop();
            }
        }

        [ContextMenu("Apply Bus Volume")]
        public void ApplyBusVolume()
        {
            EnsureAudio();
            AudioResult result = _audioService.SetBusVolume(DemoBusId, _busVolume);
            Report("Set bus volume", result);
        }

        [ContextMenu("Apply Loop Parameter")]
        public void ApplyLoopParameter()
        {
            EnsureAudio();
            if (!_useParameter)
            {
                Report("Set parameter", AudioResult.Fail(AudioErrorCode.InvalidParameter, "Parameter is disabled for this demo runner."));
                return;
            }

            AudioResult result = _audioService.SetParameter(_loopHandle, DemoParameterId, _parameterValue);
            Report("Set parameter", result);
        }

        private void EnsureAudio()
        {
            if (_audioService == null)
            {
                InitializeAudio();
            }
        }

        private void DisposeAudio()
        {
            if (_runtimeHost != null)
            {
                _runtimeHost.Dispose();
                _runtimeHost = null;
            }

            if (_audioService == null)
            {
                return;
            }

            _audioService.Dispose();
            _audioService = null;
            _loopHandle = AudioHandle.Invalid;
            _frameIndex = 0L;
            _elapsedTime = 0d;
        }

        private AudioBusDefinition CreateBusDefinition()
        {
            return new AudioBusDefinition(DemoBusId, "Demo", _busPath, _vcaPath, _busVolume, false);
        }

        private AudioEventDefinition CreateEventDefinition(int id, string name, string path, string guid, bool is3D, bool isLoop)
        {
            int busId = string.IsNullOrEmpty(_busPath) && string.IsNullOrEmpty(_vcaPath) ? 0 : DemoBusId;
            AudioParameterDefinition[] parameters = _useParameter && !string.IsNullOrEmpty(_parameterName)
                ? new[] { CreateParameterDefinition() }
                : AudioEventDefinition.EmptyParameters;

            return new AudioEventDefinition(
                id,
                name,
                path,
                guid,
                AudioEventKind.Event,
                busId,
                is3D,
                isLoop,
                30f,
                parameters);
        }

        private AudioParameterDefinition CreateParameterDefinition()
        {
            return new AudioParameterDefinition(DemoParameterId, _parameterName, AudioParameterKind.Continuous, _parameterValue, 0f, 1f);
        }

        private AudioPlayRequest CreateRequest(int eventId, bool is3D, string traceId)
        {
            AudioParameterValue[] parameters = _useParameter && !string.IsNullOrEmpty(_parameterName)
                ? new[] { new AudioParameterValue(DemoParameterId, _parameterValue) }
                : AudioPlayRequest.EmptyParameters;

            if (!is3D)
            {
                return AudioPlayRequest.Create2D(eventId, parameters, frame: Time.frameCount, traceId: traceId);
            }

            Vector3 position = transform.position;
            Vector3 forward = transform.forward;
            var audioTransform = new AudioTransform(position.x, position.y, position.z, forward.x, forward.y, forward.z);
            return AudioPlayRequest.Create3D(eventId, audioTransform, gameObject.GetInstanceID(), parameters, frame: Time.frameCount, traceId: traceId);
        }

        private void Report(string operation, AudioResult result, string detail = null)
        {
            _lastResult = string.IsNullOrEmpty(detail)
                ? operation + ": " + result
                : operation + ": " + result + " (" + detail + ")";

            if (result.Success)
            {
                Debug.Log("[MxAudioDemo] " + _lastResult);
            }
            else
            {
                Debug.LogWarning("[MxAudioDemo] " + _lastResult);
            }
        }

        private sealed class DemoAudioDefinitions : IAudioDefinitionProvider
        {
            private readonly AudioBusDefinition _bus;
            private readonly AudioEventDefinition _oneShot;
            private readonly AudioEventDefinition _loop;
            private readonly AudioParameterDefinition _parameter;

            public DemoAudioDefinitions(
                AudioBusDefinition bus,
                AudioEventDefinition oneShot,
                AudioEventDefinition loop,
                AudioParameterDefinition parameter)
            {
                _bus = bus;
                _oneShot = oneShot;
                _loop = loop;
                _parameter = parameter;
            }

            public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
            {
                if (eventId == _oneShot.Id)
                {
                    definition = _oneShot;
                    return true;
                }

                if (eventId == _loop.Id)
                {
                    definition = _loop;
                    return true;
                }

                definition = default;
                return false;
            }

            public bool TryGetBus(int busId, out AudioBusDefinition definition)
            {
                if (busId == _bus.Id)
                {
                    definition = _bus;
                    return true;
                }

                definition = default;
                return false;
            }

            public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
            {
                if ((eventId == _oneShot.Id || eventId == _loop.Id) && parameterId == _parameter.Id && !string.IsNullOrEmpty(_parameter.Name))
                {
                    definition = _parameter;
                    return true;
                }

                definition = default;
                return false;
            }
        }
    }
}
