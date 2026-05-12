using System;

namespace MxFramework.Runtime
{
    public enum SceneFlowLoadMode
    {
        Single = 0,
        Additive = 1
    }

    public enum SceneFlowOperationType
    {
        None = 0,
        Load = 1,
        Unload = 2
    }

    public enum SceneFlowResultStatus
    {
        None = 0,
        Accepted = 1,
        Succeeded = 2,
        Failed = 3
    }

    public enum SceneFlowErrorCode
    {
        None = 0,
        InvalidRequest = 1,
        Busy = 2,
        DriverFailure = 3,
        DriverFailed = DriverFailure,
        OperationFailed = 4
    }

    public readonly struct SceneFlowRequest
    {
        public SceneFlowRequest(
            string sceneKey,
            SceneFlowLoadMode loadMode = SceneFlowLoadMode.Single,
            bool unloadPreviousScene = false)
        {
            SceneKey = sceneKey ?? string.Empty;
            LoadMode = loadMode;
            UnloadPreviousScene = unloadPreviousScene;
        }

        public string SceneKey { get; }
        public SceneFlowLoadMode LoadMode { get; }
        public bool UnloadPreviousScene { get; }
    }

    public readonly struct SceneFlowError
    {
        public SceneFlowError(
            SceneFlowErrorCode code,
            string sceneKey,
            string message,
            Exception exception = null)
        {
            Code = code;
            SceneKey = sceneKey ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public SceneFlowErrorCode Code { get; }
        public string SceneKey { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsNone => Code == SceneFlowErrorCode.None;

        public static SceneFlowError None => new SceneFlowError(SceneFlowErrorCode.None, string.Empty, string.Empty);

        public override string ToString()
        {
            if (IsNone)
            {
                return "None";
            }

            return Code + " SceneKey='" + SceneKey + "' " + Message;
        }
    }

    public readonly struct SceneFlowResult
    {
        private SceneFlowResult(
            SceneFlowResultStatus status,
            SceneFlowOperationType operationType,
            string sceneKey,
            SceneFlowError error)
        {
            Status = status;
            OperationType = operationType;
            SceneKey = sceneKey ?? string.Empty;
            Error = error;
        }

        public SceneFlowResultStatus Status { get; }
        public SceneFlowOperationType OperationType { get; }
        public string SceneKey { get; }
        public SceneFlowError Error { get; }
        public bool Success => Status == SceneFlowResultStatus.Accepted || Status == SceneFlowResultStatus.Succeeded;
        public bool IsAccepted => Status == SceneFlowResultStatus.Accepted;
        public bool IsFinal => Status == SceneFlowResultStatus.Succeeded || Status == SceneFlowResultStatus.Failed;

        public static SceneFlowResult None => new SceneFlowResult(
            SceneFlowResultStatus.None,
            SceneFlowOperationType.None,
            string.Empty,
            SceneFlowError.None);

        public static SceneFlowResult Accepted(string sceneKey, SceneFlowOperationType operationType)
        {
            return new SceneFlowResult(
                SceneFlowResultStatus.Accepted,
                operationType,
                sceneKey,
                SceneFlowError.None);
        }

        public static SceneFlowResult Succeeded(string sceneKey, SceneFlowOperationType operationType)
        {
            return new SceneFlowResult(
                SceneFlowResultStatus.Succeeded,
                operationType,
                sceneKey,
                SceneFlowError.None);
        }

        public static SceneFlowResult Succeeded(string sceneKey)
        {
            return Succeeded(sceneKey, SceneFlowOperationType.None);
        }

        public static SceneFlowResult Failed(
            string sceneKey,
            SceneFlowOperationType operationType,
            SceneFlowError error)
        {
            if (error.IsNone)
            {
                error = new SceneFlowError(
                    SceneFlowErrorCode.OperationFailed,
                    sceneKey,
                    "Scene flow operation failed without an error.");
            }

            return new SceneFlowResult(
                SceneFlowResultStatus.Failed,
                operationType,
                sceneKey,
                error);
        }

        public static SceneFlowResult Failed(
            string sceneKey,
            SceneFlowErrorCode errorCode,
            string message)
        {
            return Failed(
                sceneKey,
                SceneFlowOperationType.None,
                new SceneFlowError(errorCode, sceneKey, message));
        }
    }

    public interface ISceneFlowDriver
    {
        ISceneFlowOperation LoadScene(SceneFlowRequest request);
        ISceneFlowOperation UnloadScene(string sceneKey);
    }

    public interface ISceneFlowOperation
    {
        string SceneKey { get; }
        bool IsDone { get; }
        float Progress { get; }
        bool Success { get; }
        SceneFlowError Error { get; }
    }

    public sealed class SceneFlowSnapshot
    {
        public SceneFlowSnapshot(
            string activeSceneKey,
            bool isBusy,
            SceneFlowOperationType currentOperationType,
            string currentSceneKey,
            float progress,
            SceneFlowResult lastResult)
        {
            ActiveSceneKey = activeSceneKey ?? string.Empty;
            IsBusy = isBusy;
            CurrentOperationType = currentOperationType;
            CurrentSceneKey = currentSceneKey ?? string.Empty;
            Progress = Clamp01(progress);
            LastResult = lastResult;
        }

        public string ActiveSceneKey { get; }
        public bool IsBusy { get; }
        public SceneFlowOperationType CurrentOperationType { get; }
        public string CurrentSceneKey { get; }
        public float Progress { get; }
        public SceneFlowResult LastResult { get; }

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

    public sealed class SceneFlowController
    {
        private readonly ISceneFlowDriver _driver;
        private string _activeSceneKey;
        private ISceneFlowOperation _currentOperation;
        private SceneFlowOperationType _currentOperationType;
        private string _currentSceneKey;
        private SceneFlowRequest _currentRequest;
        private SceneFlowResult _lastResult;

        public SceneFlowController(ISceneFlowDriver driver, string initialActiveSceneKey = "")
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            _driver = driver;
            _activeSceneKey = initialActiveSceneKey ?? string.Empty;
            _currentOperationType = SceneFlowOperationType.None;
            _currentSceneKey = string.Empty;
            _currentRequest = default;
            _lastResult = SceneFlowResult.None;
        }

        public string ActiveSceneKey => _activeSceneKey;
        public bool IsBusy => _currentOperation != null;
        public SceneFlowResult LastResult => _lastResult;

        public SceneFlowResult RequestLoad(SceneFlowRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SceneKey))
            {
                return FailAndRemember(
                    request.SceneKey,
                    SceneFlowOperationType.Load,
                    new SceneFlowError(
                        SceneFlowErrorCode.InvalidRequest,
                        request.SceneKey,
                        "Scene key cannot be empty."));
            }

            if (IsBusy)
            {
                return FailAndRemember(
                    request.SceneKey,
                    SceneFlowOperationType.Load,
                    new SceneFlowError(
                        SceneFlowErrorCode.Busy,
                        request.SceneKey,
                        "Scene flow is already processing " + _currentOperationType + "."));
            }

            ISceneFlowOperation operation;
            try
            {
                operation = _driver.LoadScene(request);
            }
            catch (Exception exception)
            {
                return FailAndRemember(
                    request.SceneKey,
                    SceneFlowOperationType.Load,
                    new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure,
                        request.SceneKey,
                        "Scene flow driver threw while starting load: " + exception.Message,
                        exception));
            }

            if (operation == null)
            {
                return FailAndRemember(
                    request.SceneKey,
                    SceneFlowOperationType.Load,
                    new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure,
                        request.SceneKey,
                        "Scene flow driver returned a null load operation."));
            }

            _currentOperation = operation;
            _currentOperationType = SceneFlowOperationType.Load;
            _currentSceneKey = request.SceneKey;
            _currentRequest = request;
            _lastResult = SceneFlowResult.Accepted(request.SceneKey, SceneFlowOperationType.Load);
            return _lastResult;
        }

        public void Tick()
        {
            if (_currentOperation == null || !_currentOperation.IsDone)
            {
                return;
            }

            if (_currentOperationType == SceneFlowOperationType.Load)
            {
                CompleteLoad();
                return;
            }

            if (_currentOperationType == SceneFlowOperationType.Unload)
            {
                CompleteUnload();
            }
        }

        public SceneFlowSnapshot CaptureSnapshot()
        {
            float progress = _currentOperation != null ? _currentOperation.Progress : 0f;
            return new SceneFlowSnapshot(
                _activeSceneKey,
                IsBusy,
                _currentOperationType,
                _currentSceneKey,
                progress,
                _lastResult);
        }

        private void CompleteLoad()
        {
            ISceneFlowOperation operation = _currentOperation;
            SceneFlowRequest request = _currentRequest;
            string previousSceneKey = _activeSceneKey;

            if (!operation.Success)
            {
                SceneFlowError error = NormalizeOperationError(
                    operation.Error,
                    request.SceneKey,
                    SceneFlowOperationType.Load);
                _lastResult = SceneFlowResult.Failed(request.SceneKey, SceneFlowOperationType.Load, error);
                ClearCurrentOperation();
                return;
            }

            _activeSceneKey = request.SceneKey;
            _lastResult = SceneFlowResult.Succeeded(request.SceneKey, SceneFlowOperationType.Load);
            ClearCurrentOperation();

            if (request.UnloadPreviousScene
                && !string.IsNullOrEmpty(previousSceneKey)
                && !string.Equals(previousSceneKey, request.SceneKey, StringComparison.Ordinal))
            {
                StartUnload(previousSceneKey);
            }
        }

        private void CompleteUnload()
        {
            ISceneFlowOperation operation = _currentOperation;
            string sceneKey = _currentSceneKey;

            if (operation.Success)
            {
                _lastResult = SceneFlowResult.Succeeded(sceneKey, SceneFlowOperationType.Unload);
            }
            else
            {
                SceneFlowError error = NormalizeOperationError(
                    operation.Error,
                    sceneKey,
                    SceneFlowOperationType.Unload);
                _lastResult = SceneFlowResult.Failed(sceneKey, SceneFlowOperationType.Unload, error);
            }

            ClearCurrentOperation();
        }

        private void StartUnload(string sceneKey)
        {
            ISceneFlowOperation operation;
            try
            {
                operation = _driver.UnloadScene(sceneKey);
            }
            catch (Exception exception)
            {
                _lastResult = SceneFlowResult.Failed(
                    sceneKey,
                    SceneFlowOperationType.Unload,
                    new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure,
                        sceneKey,
                        "Scene flow driver threw while starting unload: " + exception.Message,
                        exception));
                return;
            }

            if (operation == null)
            {
                _lastResult = SceneFlowResult.Failed(
                    sceneKey,
                    SceneFlowOperationType.Unload,
                    new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure,
                        sceneKey,
                        "Scene flow driver returned a null unload operation."));
                return;
            }

            _currentOperation = operation;
            _currentOperationType = SceneFlowOperationType.Unload;
            _currentSceneKey = sceneKey;
            _currentRequest = default;
        }

        private SceneFlowResult FailAndRemember(
            string sceneKey,
            SceneFlowOperationType operationType,
            SceneFlowError error)
        {
            _lastResult = SceneFlowResult.Failed(sceneKey, operationType, error);
            return _lastResult;
        }

        private static SceneFlowError NormalizeOperationError(
            SceneFlowError error,
            string sceneKey,
            SceneFlowOperationType operationType)
        {
            if (!error.IsNone)
            {
                return error;
            }

            return new SceneFlowError(
                SceneFlowErrorCode.OperationFailed,
                sceneKey,
                "Scene flow " + operationType + " operation failed.");
        }

        private void ClearCurrentOperation()
        {
            _currentOperation = null;
            _currentOperationType = SceneFlowOperationType.None;
            _currentSceneKey = string.Empty;
            _currentRequest = default;
        }
    }

    public sealed class SceneFlowRuntimeModule : RuntimeModule
    {
        private readonly SceneFlowController _controller;

        public SceneFlowRuntimeModule(
            SceneFlowController controller,
            string moduleId = "scene-flow",
            RuntimeTickStage tickStage = RuntimeTickStage.PostSimulation,
            int priority = 0)
            : base(moduleId, tickStage, priority)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            _controller = controller;
        }

        public SceneFlowRuntimeModule(
            ISceneFlowDriver driver,
            string moduleId = "scene-flow",
            RuntimeTickStage tickStage = RuntimeTickStage.PostSimulation,
            int priority = 0)
            : this(new SceneFlowController(driver), moduleId, tickStage, priority)
        {
        }

        public SceneFlowController Controller => _controller;

        public override void Tick(RuntimeTickContext context)
        {
            _controller.Tick();
        }

        public SceneFlowSnapshot CaptureSnapshot()
        {
            return _controller.CaptureSnapshot();
        }
    }
}
