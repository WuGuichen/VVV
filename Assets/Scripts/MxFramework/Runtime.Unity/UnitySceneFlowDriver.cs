using System;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Runtime.Unity
{
    public sealed class UnitySceneFlowDriver : ISceneFlowDriver
    {
        public ISceneFlowOperation LoadScene(SceneFlowRequest request)
        {
            string sceneKey = request.SceneKey;
            if (!IsValidSceneKey(sceneKey))
            {
                return UnitySceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.InvalidRequest,
                    "Scene key is missing.");
            }

            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneKey, GetLoadSceneMode(request.LoadMode));
                if (operation == null)
                {
                    return UnitySceneFlowOperation.Failed(
                        sceneKey,
                        SceneFlowErrorCode.DriverFailure,
                        "Unity SceneManager.LoadSceneAsync returned null.");
                }

                return new UnitySceneFlowOperation(sceneKey, operation);
            }
            catch (Exception exception)
            {
                return UnitySceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Unity SceneManager.LoadSceneAsync failed: " + exception.Message,
                    exception);
            }
        }

        public ISceneFlowOperation UnloadScene(string sceneKey)
        {
            if (!IsValidSceneKey(sceneKey))
            {
                return UnitySceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.InvalidRequest,
                    "Scene key is missing.");
            }

            try
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneKey);
                if (operation == null)
                {
                    return UnitySceneFlowOperation.Failed(
                        sceneKey,
                        SceneFlowErrorCode.DriverFailure,
                        "Unity SceneManager.UnloadSceneAsync returned null.");
                }

                return new UnitySceneFlowOperation(sceneKey, operation);
            }
            catch (Exception exception)
            {
                return UnitySceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Unity SceneManager.UnloadSceneAsync failed: " + exception.Message,
                    exception);
            }
        }

        private static bool IsValidSceneKey(string sceneKey)
        {
            return !string.IsNullOrWhiteSpace(sceneKey);
        }

        private static LoadSceneMode GetLoadSceneMode(SceneFlowLoadMode loadMode)
        {
            return loadMode == SceneFlowLoadMode.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;
        }

        private sealed class UnitySceneFlowOperation : ISceneFlowOperation
        {
            private readonly AsyncOperation _operation;
            private readonly SceneFlowError _error;
            private readonly bool _failedImmediately;

            public UnitySceneFlowOperation(string sceneKey, AsyncOperation operation)
            {
                SceneKey = sceneKey ?? string.Empty;
                _operation = operation;
                _error = SceneFlowError.None;
            }

            private UnitySceneFlowOperation(string sceneKey, SceneFlowError error)
            {
                SceneKey = sceneKey ?? string.Empty;
                _error = error.IsNone
                    ? new SceneFlowError(SceneFlowErrorCode.OperationFailed, SceneKey, "Unity scene operation failed.")
                    : error;
                _failedImmediately = true;
            }

            public string SceneKey { get; }
            public bool IsDone => _failedImmediately || _operation == null || _operation.isDone;
            public float Progress => IsDone || _operation == null ? 1f : Mathf.Clamp01(_operation.progress);
            public bool Success => IsDone && !_failedImmediately;
            public SceneFlowError Error => Success || !IsDone ? SceneFlowError.None : _error;

            public static ISceneFlowOperation Failed(
                string sceneKey,
                SceneFlowErrorCode errorCode,
                string message,
                Exception exception = null)
            {
                return new UnitySceneFlowOperation(
                    sceneKey,
                    new SceneFlowError(errorCode, sceneKey, message, exception));
            }
        }
    }
}
