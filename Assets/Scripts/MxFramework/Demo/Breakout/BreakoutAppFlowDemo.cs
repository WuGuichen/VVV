using System;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Demo.Breakout
{
    [AddComponentMenu("MxFramework/Demo/Breakout App Flow Demo")]
    public sealed class BreakoutAppFlowDemo : MonoBehaviour
    {
        public const string BootStateId = "Boot";
        public const string MenuStateId = "Menu";
        public const string LoadingStateId = "Loading";
        public const string GameplayStateId = "Gameplay";
        public const string GameOverStateId = "GameOver";

        [SerializeField] private BreakoutPlayableDemo _playable = null;
        [SerializeField] private string _initialSceneKey = "BreakoutBoot";
        [SerializeField] private string _gameplaySceneKey = "BreakoutGameplay";
        [SerializeField] private bool _useUnitySceneFlowDriver = false;
        [SerializeField] [Min(0f)] private float _bootHoldSeconds = 0.25f;
        [SerializeField] [Min(0.05f)] private float _fallbackSceneLoadSeconds = 0.65f;
        [SerializeField] [Min(1)] private int _targetFrameRate = 60;

        private RuntimeHost _host;
        private AppFlowController _appFlowController;
        private SceneFlowController _sceneFlowController;
        private long _frame;
        private double _elapsedSeconds;
        private int _previousTargetFrameRate;
        private bool _hasPreviousTargetFrameRate;

        public AppFlowSnapshot AppSnapshot => _appFlowController != null
            ? _appFlowController.CaptureSnapshot()
            : null;

        public SceneFlowSnapshot SceneSnapshot => _sceneFlowController != null
            ? _sceneFlowController.CaptureSnapshot()
            : null;

        private void OnEnable()
        {
            ApplyTargetFrameRate();
            EnsurePlayable();
            BuildRuntime();
            RegisterPlayableCallbacks();
            PublishSnapshots();
        }

        private void OnDisable()
        {
            UnregisterPlayableCallbacks();
            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }

            _appFlowController = null;
            _sceneFlowController = null;
            RestoreTargetFrameRate();
        }

        private void Update()
        {
            if (_host == null)
            {
                BuildRuntime();
            }

            double deltaTime = Mathf.Max(0f, Time.deltaTime);
            _elapsedSeconds += deltaTime;
            _host.Tick(_frame, deltaTime, _elapsedSeconds);
            _frame++;
            PublishSnapshots();
        }

        private void EnsurePlayable()
        {
            if (_playable != null)
            {
                return;
            }

            _playable = GetComponent<BreakoutPlayableDemo>();
            if (_playable == null)
            {
                _playable = gameObject.AddComponent<BreakoutPlayableDemo>();
            }
        }

        private void BuildRuntime()
        {
            if (_host != null)
            {
                _host.Dispose();
            }

            ISceneFlowDriver sceneDriver = CreateSceneFlowDriver();
            _sceneFlowController = new SceneFlowController(sceneDriver, _initialSceneKey);
            _appFlowController = new AppFlowController();
            _appFlowController.RegisterState(new BootState(this));
            _appFlowController.RegisterState(new MenuState(this));
            _appFlowController.RegisterState(new LoadingState(this));
            _appFlowController.RegisterState(new GameplayState(this));
            _appFlowController.RegisterState(new GameOverState(this));

            _host = new RuntimeHost();
            _host.RegisterModule(new AppFlowRuntimeModule(_appFlowController));
            _host.RegisterModule(new SceneFlowRuntimeModule(_sceneFlowController));
            _host.Initialize();
            _host.Start();

            _frame = 0;
            _elapsedSeconds = 0d;
            _appFlowController.Start(BootStateId, "Breakout demo enabled.");
        }

        private ISceneFlowDriver CreateSceneFlowDriver()
        {
            if (_useUnitySceneFlowDriver && TryCreateUnitySceneFlowDriver(out ISceneFlowDriver driver))
            {
                return driver;
            }

            return new TimedSceneFlowDriver(_fallbackSceneLoadSeconds);
        }

        private static bool TryCreateUnitySceneFlowDriver(out ISceneFlowDriver driver)
        {
            driver = null;
            Type driverType = Type.GetType("MxFramework.Runtime.Unity.UnitySceneFlowDriver, MxFramework.Runtime.Unity");
            if (driverType == null)
            {
                return false;
            }

            try
            {
                driver = Activator.CreateInstance(driverType) as ISceneFlowDriver;
                return driver != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Breakout AppFlow demo could not create UnitySceneFlowDriver: " + exception.Message);
                return false;
            }
        }

        private void RegisterPlayableCallbacks()
        {
            if (_playable == null)
            {
                return;
            }

            _playable.StartRequested += OnStartRequested;
            _playable.RestartRequested += OnRestartRequested;
            _playable.MenuRequested += OnMenuRequested;
        }

        private void UnregisterPlayableCallbacks()
        {
            if (_playable == null)
            {
                return;
            }

            _playable.StartRequested -= OnStartRequested;
            _playable.RestartRequested -= OnRestartRequested;
            _playable.MenuRequested -= OnMenuRequested;
        }

        private void OnStartRequested()
        {
            RequestTransition(LoadingStateId, "Start selected.");
        }

        private void OnRestartRequested()
        {
            RequestTransition(LoadingStateId, "Restart selected.");
        }

        private void OnMenuRequested()
        {
            RequestTransition(MenuStateId, "Menu selected.");
        }

        private void RequestTransition(string stateId, string reason)
        {
            if (_appFlowController == null || !_appFlowController.IsStarted)
            {
                return;
            }

            if (string.Equals(_appFlowController.CurrentStateId, stateId, StringComparison.Ordinal)
                || _appFlowController.HasPendingTransition)
            {
                return;
            }

            _appFlowController.RequestTransition(stateId, reason);
        }

        private void RequestGameplaySceneLoad()
        {
            if (_sceneFlowController == null || _sceneFlowController.IsBusy)
            {
                return;
            }

            _sceneFlowController.RequestLoad(new SceneFlowRequest(
                _gameplaySceneKey,
                SceneFlowLoadMode.Single,
                unloadPreviousScene: false));
        }

        private bool IsGameplaySceneReady()
        {
            if (_sceneFlowController == null)
            {
                return false;
            }

            SceneFlowSnapshot snapshot = _sceneFlowController.CaptureSnapshot();
            return !snapshot.IsBusy
                && snapshot.LastResult.Status == SceneFlowResultStatus.Succeeded
                && string.Equals(snapshot.LastResult.SceneKey, _gameplaySceneKey, StringComparison.Ordinal);
        }

        private bool IsSceneFlowFailed()
        {
            return _sceneFlowController != null
                && _sceneFlowController.LastResult.Status == SceneFlowResultStatus.Failed;
        }

        private void PublishSnapshots()
        {
            if (_playable == null || _appFlowController == null || _sceneFlowController == null)
            {
                return;
            }

            _playable.SetFlowSnapshots(
                _appFlowController.CaptureSnapshot(),
                _sceneFlowController.CaptureSnapshot());
        }

        private void ApplyTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
            {
                _previousTargetFrameRate = Application.targetFrameRate;
                _hasPreviousTargetFrameRate = true;
            }

            Application.targetFrameRate = Math.Max(1, _targetFrameRate);
        }

        private void RestoreTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
            {
                return;
            }

            Application.targetFrameRate = _previousTargetFrameRate;
            _hasPreviousTargetFrameRate = false;
        }

        private sealed class BootState : FlowState
        {
            private readonly BreakoutAppFlowDemo _owner;
            private double _enterElapsedTime;

            public BootState(BreakoutAppFlowDemo owner)
                : base(BootStateId)
            {
                _owner = owner;
            }

            public override void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _enterElapsedTime = context.ElapsedTime;
                _owner._playable.SetMode(BreakoutPlayableDemo.BreakoutUiMode.Boot);
            }

            public override void Tick(AppFlowTickContext context)
            {
                if (context.ElapsedTime - _enterElapsedTime >= _owner._bootHoldSeconds)
                {
                    context.RequestTransition(MenuStateId, "Boot complete.");
                }
            }
        }

        private sealed class MenuState : FlowState
        {
            private readonly BreakoutAppFlowDemo _owner;

            public MenuState(BreakoutAppFlowDemo owner)
                : base(MenuStateId)
            {
                _owner = owner;
            }

            public override void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _owner._playable.SetMode(BreakoutPlayableDemo.BreakoutUiMode.Menu);
            }
        }

        private sealed class LoadingState : FlowState
        {
            private readonly BreakoutAppFlowDemo _owner;
            private bool _requestedLoad;

            public LoadingState(BreakoutAppFlowDemo owner)
                : base(LoadingStateId)
            {
                _owner = owner;
            }

            public override void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _requestedLoad = false;
                _owner._playable.SetMode(BreakoutPlayableDemo.BreakoutUiMode.Loading);
                _owner._playable.ResetGame();
            }

            public override void Tick(AppFlowTickContext context)
            {
                if (!_requestedLoad)
                {
                    _owner.RequestGameplaySceneLoad();
                    _requestedLoad = true;
                    return;
                }

                if (_owner.IsGameplaySceneReady())
                {
                    context.RequestTransition(GameplayStateId, "Gameplay scene ready.");
                }
                else if (_owner.IsSceneFlowFailed())
                {
                    _owner._playable.SetDependencyStatus("Scene flow failed: " + _owner._sceneFlowController.LastResult.Error);
                }
            }
        }

        private sealed class GameplayState : FlowState
        {
            private readonly BreakoutAppFlowDemo _owner;

            public GameplayState(BreakoutAppFlowDemo owner)
                : base(GameplayStateId)
            {
                _owner = owner;
            }

            public override void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _owner._playable.SetMode(BreakoutPlayableDemo.BreakoutUiMode.Gameplay);
            }

            public override void Tick(AppFlowTickContext context)
            {
                if (_owner._playable.IsGameOver)
                {
                    context.RequestTransition(GameOverStateId, "Breakout game over.");
                }
            }
        }

        private sealed class GameOverState : FlowState
        {
            private readonly BreakoutAppFlowDemo _owner;

            public GameOverState(BreakoutAppFlowDemo owner)
                : base(GameOverStateId)
            {
                _owner = owner;
            }

            public override void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _owner._playable.SetMode(BreakoutPlayableDemo.BreakoutUiMode.GameOver);
            }
        }

        private abstract class FlowState : IAppFlowState
        {
            protected FlowState(string stateId)
            {
                StateId = stateId;
            }

            public string StateId { get; }
            public virtual void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
            public virtual void Tick(AppFlowTickContext context) { }
            public virtual void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }

        private sealed class TimedSceneFlowDriver : ISceneFlowDriver
        {
            private readonly float _loadSeconds;

            public TimedSceneFlowDriver(float loadSeconds)
            {
                _loadSeconds = Mathf.Max(0.05f, loadSeconds);
            }

            public ISceneFlowOperation LoadScene(SceneFlowRequest request)
            {
                return new TimedSceneFlowOperation(request.SceneKey, _loadSeconds);
            }

            public ISceneFlowOperation UnloadScene(string sceneKey)
            {
                return new TimedSceneFlowOperation(sceneKey, 0.05f);
            }
        }

        private sealed class TimedSceneFlowOperation : ISceneFlowOperation
        {
            private readonly float _startedAt;
            private readonly float _duration;

            public TimedSceneFlowOperation(string sceneKey, float duration)
            {
                SceneKey = sceneKey ?? string.Empty;
                _duration = Mathf.Max(0.01f, duration);
                _startedAt = Time.realtimeSinceStartup;
            }

            public string SceneKey { get; }
            public bool IsDone => Progress >= 1f;
            public float Progress => Mathf.Clamp01((Time.realtimeSinceStartup - _startedAt) / _duration);
            public bool Success => IsDone;
            public SceneFlowError Error => SceneFlowError.None;
        }
    }
}
