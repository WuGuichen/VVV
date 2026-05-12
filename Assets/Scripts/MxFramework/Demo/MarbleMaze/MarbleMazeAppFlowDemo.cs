using System;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Demo.MarbleMaze
{
    [AddComponentMenu("MxFramework/Demo/Marble Maze App Flow Demo")]
    public sealed class MarbleMazeAppFlowDemo : MonoBehaviour
    {
        public const string BootStateId = "Boot";
        public const string MenuStateId = "Menu";
        public const string GameplayStateId = "Gameplay";
        public const string FinishedStateId = "Finished";

        [SerializeField] private MarbleMazePhysicsDemo _playable = null;
        [SerializeField] [Min(0f)] private float _bootHoldSeconds = 0.2f;

        private RuntimeHost _host;
        private AppFlowController _appFlowController;
        private long _frame;
        private double _elapsedSeconds;

        public AppFlowSnapshot AppSnapshot => _appFlowController != null
            ? _appFlowController.CaptureSnapshot()
            : null;

        private void OnEnable()
        {
            EnsurePlayable();
            BuildRuntime();
            if (_playable != null)
                _playable.ResetRequested += OnResetRequested;
        }

        private void OnDisable()
        {
            if (_playable != null)
                _playable.ResetRequested -= OnResetRequested;

            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }
        }

        private void Update()
        {
            if (_host == null)
                BuildRuntime();

            double deltaTime = Mathf.Max(0f, Time.deltaTime);
            _elapsedSeconds += deltaTime;
            _host.Tick(_frame, deltaTime, _elapsedSeconds);
            _frame++;

            if (_playable != null && _playable.Snapshot.IsFinished)
                RequestTransition(FinishedStateId, "Marble reached the finish trigger.");
        }

        private void EnsurePlayable()
        {
            if (_playable != null)
                return;

            _playable = GetComponent<MarbleMazePhysicsDemo>();
            if (_playable == null)
                _playable = gameObject.AddComponent<MarbleMazePhysicsDemo>();
        }

        private void BuildRuntime()
        {
            if (_host != null)
                _host.Dispose();

            _appFlowController = new AppFlowController();
            _appFlowController.RegisterState(new BootState(this));
            _appFlowController.RegisterState(new MenuState(this));
            _appFlowController.RegisterState(new GameplayState(this));
            _appFlowController.RegisterState(new FinishedState(this));

            _host = new RuntimeHost();
            _host.RegisterModule(new AppFlowRuntimeModule(_appFlowController));
            _host.Initialize();
            _host.Start();
            _frame = 0;
            _elapsedSeconds = 0d;
            _appFlowController.Start(BootStateId, "Marble Maze enabled.");
        }

        private void OnResetRequested()
        {
            RequestTransition(GameplayStateId, "Reset selected.");
        }

        private void RequestTransition(string stateId, string reason)
        {
            if (_appFlowController == null || !_appFlowController.IsStarted)
                return;

            if (string.Equals(_appFlowController.CurrentStateId, stateId, StringComparison.Ordinal)
                || _appFlowController.HasPendingTransition)
            {
                return;
            }

            _appFlowController.RequestTransition(stateId, reason);
        }

        private sealed class BootState : IAppFlowState
        {
            private readonly MarbleMazeAppFlowDemo _owner;
            private double _enterTime;

            public BootState(MarbleMazeAppFlowDemo owner)
            {
                _owner = owner;
            }

            public string StateId => BootStateId;

            public void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _enterTime = _owner._elapsedSeconds;
            }

            public void Tick(AppFlowTickContext context)
            {
                if (_owner._elapsedSeconds - _enterTime >= _owner._bootHoldSeconds)
                    _owner.RequestTransition(MenuStateId, "Boot hold complete.");
            }

            public void Exit(AppFlowStateContext context, AppFlowTransition transition)
            {
            }
        }

        private sealed class MenuState : IAppFlowState
        {
            private readonly MarbleMazeAppFlowDemo _owner;

            public MenuState(MarbleMazeAppFlowDemo owner)
            {
                _owner = owner;
            }

            public string StateId => MenuStateId;

            public void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                _owner.RequestTransition(GameplayStateId, "Single-scene demo starts gameplay immediately.");
            }

            public void Tick(AppFlowTickContext context)
            {
            }

            public void Exit(AppFlowStateContext context, AppFlowTransition transition)
            {
            }
        }

        private sealed class GameplayState : IAppFlowState
        {
            public GameplayState(MarbleMazeAppFlowDemo owner)
            {
            }

            public string StateId => GameplayStateId;
            public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
            public void Tick(AppFlowTickContext context) { }
            public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }

        private sealed class FinishedState : IAppFlowState
        {
            public FinishedState(MarbleMazeAppFlowDemo owner)
            {
            }

            public string StateId => FinishedStateId;
            public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
            public void Tick(AppFlowTickContext context) { }
            public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }
    }
}
