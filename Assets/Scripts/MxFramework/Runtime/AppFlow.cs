using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public interface IAppFlowState
    {
        string StateId { get; }
        void Enter(AppFlowStateContext context, AppFlowTransition transition);
        void Tick(AppFlowTickContext context);
        void Exit(AppFlowStateContext context, AppFlowTransition transition);
    }

    public enum AppFlowTransitionErrorCode
    {
        None = 0,
        NotStarted = 1001,
        AlreadyStarted = 1002,
        StateNotRegistered = 1003,
        PendingTransitionExists = 1004,
        TransitionInProgress = 1005
    }

    public readonly struct AppFlowTransition
    {
        public AppFlowTransition(string fromStateId, string toStateId, string reason = null)
        {
            FromStateId = fromStateId ?? string.Empty;
            ToStateId = toStateId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string FromStateId { get; }
        public string ToStateId { get; }
        public string Reason { get; }
        public bool HasSourceState => !string.IsNullOrEmpty(FromStateId);
        public bool HasTargetState => !string.IsNullOrEmpty(ToStateId);
    }

    public readonly struct AppFlowStateContext
    {
        public AppFlowStateContext(AppFlowController controller, long frameIndex, double deltaTime, double elapsedTime)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index cannot be negative.");
            }

            if (deltaTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            if (elapsedTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime), "Elapsed time cannot be negative.");
            }

            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            FrameIndex = frameIndex;
            DeltaTime = deltaTime;
            ElapsedTime = elapsedTime;
        }

        public AppFlowController Controller { get; }
        public long FrameIndex { get; }
        public double DeltaTime { get; }
        public double ElapsedTime { get; }
    }

    public readonly struct AppFlowTickContext
    {
        public AppFlowTickContext(AppFlowController controller, string currentStateId, long frameIndex, double deltaTime, double elapsedTime)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index cannot be negative.");
            }

            if (deltaTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            if (elapsedTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime), "Elapsed time cannot be negative.");
            }

            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            CurrentStateId = currentStateId ?? string.Empty;
            FrameIndex = frameIndex;
            DeltaTime = deltaTime;
            ElapsedTime = elapsedTime;
        }

        public AppFlowController Controller { get; }
        public string CurrentStateId { get; }
        public long FrameIndex { get; }
        public double DeltaTime { get; }
        public double ElapsedTime { get; }

        public AppFlowTransitionResult RequestTransition(string targetStateId, string reason = null)
        {
            return Controller.RequestTransition(targetStateId, reason);
        }
    }

    public sealed class AppFlowTransitionResult
    {
        private AppFlowTransitionResult(
            bool success,
            AppFlowTransitionErrorCode errorCode,
            AppFlowTransition transition,
            string message)
        {
            Success = success;
            ErrorCode = errorCode;
            Transition = transition;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public bool Succeeded => Success;
        public AppFlowTransitionErrorCode ErrorCode { get; }
        public AppFlowTransition Transition { get; }
        public string Message { get; }
        public string TargetStateId => Transition.ToStateId;
        public bool IsNone => !Success && ErrorCode == AppFlowTransitionErrorCode.None;

        public static AppFlowTransitionResult None { get; } =
            new AppFlowTransitionResult(false, AppFlowTransitionErrorCode.None, default, string.Empty);

        public static AppFlowTransitionResult SucceededResult(AppFlowTransition transition)
        {
            return new AppFlowTransitionResult(true, AppFlowTransitionErrorCode.None, transition, string.Empty);
        }

        public static AppFlowTransitionResult Failed(
            AppFlowTransitionErrorCode errorCode,
            AppFlowTransition transition,
            string message)
        {
            if (errorCode == AppFlowTransitionErrorCode.None)
            {
                throw new ArgumentException("Failure result requires an error code.", nameof(errorCode));
            }

            return new AppFlowTransitionResult(false, errorCode, transition, message);
        }
    }

    public sealed class AppFlowSnapshot
    {
        private readonly List<string> _registeredStateIds;

        public AppFlowSnapshot(
            bool isStarted,
            string currentStateId,
            bool hasPendingTransition,
            AppFlowTransition pendingTransition,
            long tickCount,
            long lastFrameIndex,
            IReadOnlyList<string> registeredStateIds,
            AppFlowTransitionResult lastResult)
        {
            IsStarted = isStarted;
            CurrentStateId = currentStateId ?? string.Empty;
            HasPendingTransition = hasPendingTransition;
            PendingTransition = pendingTransition;
            TickCount = tickCount;
            LastFrameIndex = lastFrameIndex;
            _registeredStateIds = registeredStateIds != null ? new List<string>(registeredStateIds) : new List<string>();
            LastResult = lastResult ?? AppFlowTransitionResult.None;
        }

        public bool IsStarted { get; }
        public string CurrentStateId { get; }
        public bool HasPendingTransition { get; }
        public AppFlowTransition PendingTransition { get; }
        public string PendingStateId => HasPendingTransition ? PendingTransition.ToStateId : string.Empty;
        public long TickCount { get; }
        public long LastFrameIndex { get; }
        public IReadOnlyList<string> RegisteredStateIds => _registeredStateIds;
        public AppFlowTransitionResult LastResult { get; }
    }

    public sealed class AppFlowController
    {
        private readonly Dictionary<string, IAppFlowState> _states = new Dictionary<string, IAppFlowState>(StringComparer.Ordinal);
        private readonly List<string> _registeredStateIds = new List<string>();
        private IAppFlowState _currentState;
        private AppFlowTransition _pendingTransition;
        private bool _hasPendingTransition;
        private bool _isApplyingTransition;
        private AppFlowTransitionResult _lastResult = AppFlowTransitionResult.None;
        private long _lastFrameIndex = -1;

        public bool IsStarted => _currentState != null;
        public string CurrentStateId => _currentState != null ? _currentState.StateId : string.Empty;
        public bool HasPendingTransition => _hasPendingTransition;
        public AppFlowTransition PendingTransition => _hasPendingTransition ? _pendingTransition : default;
        public long TickCount { get; private set; }
        public AppFlowTransitionResult LastResult => _lastResult;

        public void RegisterState(IAppFlowState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (string.IsNullOrWhiteSpace(state.StateId))
            {
                throw new ArgumentException("AppFlow state id cannot be empty.", nameof(state));
            }

            if (_states.ContainsKey(state.StateId))
            {
                throw new InvalidOperationException("AppFlow state is already registered: " + state.StateId);
            }

            _states.Add(state.StateId, state);
            _registeredStateIds.Add(state.StateId);
        }

        public AppFlowTransitionResult Start(string initialStateId, string reason = null)
        {
            var transition = new AppFlowTransition(string.Empty, initialStateId, reason);
            if (IsStarted)
            {
                return StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.AlreadyStarted,
                    transition,
                    "AppFlow is already started."));
            }

            IAppFlowState initialState;
            if (!TryGetState(initialStateId, out initialState))
            {
                return StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.StateNotRegistered,
                    transition,
                    "AppFlow state is not registered: " + (initialStateId ?? string.Empty)));
            }

            transition = new AppFlowTransition(string.Empty, initialState.StateId, reason);
            _hasPendingTransition = false;
            _pendingTransition = default;
            _currentState = initialState;
            _currentState.Enter(new AppFlowStateContext(this, 0, 0d, 0d), transition);

            return StoreResult(AppFlowTransitionResult.SucceededResult(transition));
        }

        public AppFlowTransitionResult RequestTransition(string targetStateId, string reason = null)
        {
            var transition = new AppFlowTransition(CurrentStateId, targetStateId, reason);
            if (!IsStarted)
            {
                return StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.NotStarted,
                    transition,
                    "AppFlow is not started."));
            }

            if (_isApplyingTransition)
            {
                return StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.TransitionInProgress,
                    transition,
                    "AppFlow transition is already in progress."));
            }

            if (_hasPendingTransition)
            {
                return StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.PendingTransitionExists,
                    transition,
                    "AppFlow already has a pending transition to: " + _pendingTransition.ToStateId));
            }

            IAppFlowState targetState;
            if (!TryGetState(targetStateId, out targetState))
            {
                return StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.StateNotRegistered,
                    transition,
                    "AppFlow state is not registered: " + (targetStateId ?? string.Empty)));
            }

            _pendingTransition = new AppFlowTransition(CurrentStateId, targetState.StateId, reason);
            _hasPendingTransition = true;

            return StoreResult(AppFlowTransitionResult.SucceededResult(_pendingTransition));
        }

        public void Tick(long frameIndex, double deltaTime, double elapsedTime)
        {
            ValidateTick(frameIndex, deltaTime, elapsedTime);
            _lastFrameIndex = frameIndex;

            if (!IsStarted)
            {
                StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.NotStarted,
                    new AppFlowTransition(string.Empty, string.Empty),
                    "AppFlow is not started."));
                return;
            }

            if (_hasPendingTransition)
            {
                ApplyPendingTransition(frameIndex, deltaTime, elapsedTime);
            }

            string currentStateId = CurrentStateId;
            _currentState.Tick(new AppFlowTickContext(this, currentStateId, frameIndex, deltaTime, elapsedTime));
            TickCount++;
        }

        public AppFlowSnapshot CaptureSnapshot()
        {
            return new AppFlowSnapshot(
                IsStarted,
                CurrentStateId,
                _hasPendingTransition,
                PendingTransition,
                TickCount,
                _lastFrameIndex,
                _registeredStateIds,
                _lastResult);
        }

        private bool TryGetState(string stateId, out IAppFlowState state)
        {
            if (string.IsNullOrWhiteSpace(stateId))
            {
                state = null;
                return false;
            }

            return _states.TryGetValue(stateId, out state);
        }

        private void ApplyPendingTransition(long frameIndex, double deltaTime, double elapsedTime)
        {
            AppFlowTransition transition = _pendingTransition;
            _hasPendingTransition = false;
            _pendingTransition = default;

            IAppFlowState targetState;
            if (!TryGetState(transition.ToStateId, out targetState))
            {
                StoreResult(AppFlowTransitionResult.Failed(
                    AppFlowTransitionErrorCode.StateNotRegistered,
                    transition,
                    "AppFlow state is not registered: " + transition.ToStateId));
                return;
            }

            var stateContext = new AppFlowStateContext(this, frameIndex, deltaTime, elapsedTime);
            _isApplyingTransition = true;
            try
            {
                _currentState.Exit(stateContext, transition);
                _currentState = targetState;
                _currentState.Enter(stateContext, transition);
            }
            finally
            {
                _isApplyingTransition = false;
            }

            StoreResult(AppFlowTransitionResult.SucceededResult(transition));
        }

        private AppFlowTransitionResult StoreResult(AppFlowTransitionResult result)
        {
            _lastResult = result ?? AppFlowTransitionResult.None;
            return _lastResult;
        }

        private static void ValidateTick(long frameIndex, double deltaTime, double elapsedTime)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index cannot be negative.");
            }

            if (deltaTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            if (elapsedTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime), "Elapsed time cannot be negative.");
            }
        }
    }

    public sealed class AppFlowRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "app-flow";

        public AppFlowRuntimeModule(
            AppFlowController controller,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.Simulation,
            int priority = 0)
            : base(moduleId, tickStage, priority)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public AppFlowController Controller { get; }

        public override void Tick(RuntimeTickContext context)
        {
            Controller.Tick(context.FrameIndex, context.DeltaTime, context.ElapsedTime);
        }

        public AppFlowSnapshot CaptureSnapshot()
        {
            return Controller.CaptureSnapshot();
        }
    }
}
