using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Editor
{
    public sealed class EditorSceneFlowDriver : ISceneFlowDriver
    {
        private readonly IResourceCatalogQuery _catalogQuery;
        private readonly Dictionary<string, string> _loadedScenePaths = new Dictionary<string, string>(StringComparer.Ordinal);

        public EditorSceneFlowDriver(IResourceCatalogQuery catalogQuery)
        {
            _catalogQuery = catalogQuery ?? throw new ArgumentNullException(nameof(catalogQuery));
        }

        public int LoadedSceneCount => _loadedScenePaths.Count;

        public ISceneFlowOperation LoadScene(SceneFlowRequest request)
        {
            if (!EditorApplication.isPlaying)
            {
                return EditorSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Editor scene flow can only load scenes in Play Mode.");
            }

            if (!TryResolveScenePath(request.SceneKey, out string scenePath, out SceneFlowError error))
                return EditorSceneFlowOperation.Failed(request.SceneKey, error);

            if (AssetDatabase.GetMainAssetTypeAtPath(scenePath) != typeof(SceneAsset))
            {
                return EditorSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Editor scene flow path is not a Unity Scene asset: " + scenePath + ".");
            }

            try
            {
                var parameters = new LoadSceneParameters(GetLoadSceneMode(request.LoadMode));
                AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, parameters);
                if (operation == null)
                {
                    return EditorSceneFlowOperation.Failed(
                        request.SceneKey,
                        SceneFlowErrorCode.DriverFailure,
                        "EditorSceneManager.LoadSceneAsyncInPlayMode returned null for '" + scenePath + "'.");
                }

                return EditorSceneFlowOperation.FromAsync(
                    request.SceneKey,
                    operation,
                    () => CompleteLoad(request, scenePath),
                    null);
            }
            catch (Exception exception)
            {
                return EditorSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "EditorSceneManager.LoadSceneAsyncInPlayMode failed for '" + scenePath + "': " + exception.Message,
                    exception);
            }
        }

        public ISceneFlowOperation UnloadScene(string sceneKey)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                return EditorSceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.InvalidRequest,
                    "Scene key is missing.");
            }

            string scenePath = string.Empty;
            if (!_loadedScenePaths.TryGetValue(sceneKey, out scenePath))
                TryResolveScenePath(sceneKey, out scenePath, out _);

            Scene scene = !string.IsNullOrWhiteSpace(scenePath)
                ? SceneManager.GetSceneByPath(scenePath)
                : SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(sceneKey));

            if (!scene.IsValid() || !scene.isLoaded)
            {
                _loadedScenePaths.Remove(sceneKey);
                return EditorSceneFlowOperation.Succeeded(sceneKey);
            }

            try
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null)
                {
                    _loadedScenePaths.Remove(sceneKey);
                    return EditorSceneFlowOperation.Succeeded(sceneKey);
                }

                return EditorSceneFlowOperation.FromAsync(
                    sceneKey,
                    operation,
                    () => CompleteUnload(sceneKey),
                    null);
            }
            catch (Exception exception)
            {
                return EditorSceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Unity SceneManager.UnloadSceneAsync failed for editor scene '" + sceneKey + "': " + exception.Message,
                    exception);
            }
        }

        private bool TryResolveScenePath(string sceneKey, out string scenePath, out SceneFlowError error)
        {
            scenePath = string.Empty;
            ResourceKey key = new ResourceKey(sceneKey, ResourceTypeIds.Scene);
            if (!key.IsValid)
            {
                error = new SceneFlowError(
                    SceneFlowErrorCode.InvalidRequest,
                    sceneKey,
                    "Scene resource key is invalid.");
                return false;
            }

            if (!_catalogQuery.TryGetEntry(key, out ResourceCatalogEntry entry))
            {
                error = new SceneFlowError(
                    SceneFlowErrorCode.DriverFailure,
                    sceneKey,
                    "Scene resource was not found in editor catalog: " + key + ".");
                return false;
            }

            if (!string.Equals(entry.ProviderId, EditorSceneAssetDatabaseResourceProvider.Id, StringComparison.Ordinal))
            {
                error = new SceneFlowError(
                    SceneFlowErrorCode.DriverFailure,
                    sceneKey,
                    "Scene catalog entry must use provider '" + EditorSceneAssetDatabaseResourceProvider.Id + "'.");
                return false;
            }

            if (!EditorSceneAssetDatabaseResourceProvider.TryGetScenePath(entry, out scenePath))
            {
                error = new SceneFlowError(
                    SceneFlowErrorCode.DriverFailure,
                    sceneKey,
                    "Scene catalog entry does not contain a valid Unity scene path.");
                return false;
            }

            error = SceneFlowError.None;
            return true;
        }

        private void CompleteLoad(SceneFlowRequest request, string scenePath)
        {
            if (request.LoadMode == SceneFlowLoadMode.Single)
                _loadedScenePaths.Clear();

            _loadedScenePaths[request.SceneKey] = scenePath;
        }

        private void CompleteUnload(string sceneKey)
        {
            _loadedScenePaths.Remove(sceneKey);
        }

        private static LoadSceneMode GetLoadSceneMode(SceneFlowLoadMode loadMode)
        {
            return loadMode == SceneFlowLoadMode.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;
        }

        private sealed class EditorSceneFlowOperation : ISceneFlowOperation
        {
            private readonly AsyncOperation _operation;
            private readonly Action _onSucceeded;
            private readonly Action _onFailed;
            private readonly SceneFlowError _immediateError;
            private readonly bool _failedImmediately;
            private bool _finalized;
            private bool _success;
            private SceneFlowError _error;

            private EditorSceneFlowOperation(
                string sceneKey,
                AsyncOperation operation,
                Action onSucceeded,
                Action onFailed)
            {
                SceneKey = sceneKey ?? string.Empty;
                _operation = operation;
                _onSucceeded = onSucceeded;
                _onFailed = onFailed;
                _error = SceneFlowError.None;
            }

            private EditorSceneFlowOperation(string sceneKey, SceneFlowError error, bool success)
            {
                SceneKey = sceneKey ?? string.Empty;
                _immediateError = error;
                _error = error;
                _success = success;
                _failedImmediately = !success;
                _finalized = true;
            }

            public string SceneKey { get; }
            public bool IsDone => _failedImmediately || _operation == null || _operation.isDone;
            public float Progress => IsDone || _operation == null ? 1f : Mathf.Clamp01(_operation.progress);

            public bool Success
            {
                get
                {
                    FinalizeIfDone();
                    return _success;
                }
            }

            public SceneFlowError Error
            {
                get
                {
                    FinalizeIfDone();
                    return _success ? SceneFlowError.None : _error;
                }
            }

            public static ISceneFlowOperation FromAsync(
                string sceneKey,
                AsyncOperation operation,
                Action onSucceeded,
                Action onFailed)
            {
                return new EditorSceneFlowOperation(sceneKey, operation, onSucceeded, onFailed);
            }

            public static ISceneFlowOperation Succeeded(string sceneKey)
            {
                return new EditorSceneFlowOperation(sceneKey, SceneFlowError.None, true);
            }

            public static ISceneFlowOperation Failed(
                string sceneKey,
                SceneFlowErrorCode errorCode,
                string message,
                Exception exception = null)
            {
                return Failed(sceneKey, new SceneFlowError(errorCode, sceneKey, message, exception));
            }

            public static ISceneFlowOperation Failed(string sceneKey, SceneFlowError error)
            {
                if (error.IsNone)
                    error = new SceneFlowError(SceneFlowErrorCode.OperationFailed, sceneKey, "Editor scene flow operation failed.");
                return new EditorSceneFlowOperation(sceneKey, error, false);
            }

            private void FinalizeIfDone()
            {
                if (_finalized || !IsDone)
                    return;

                _finalized = true;
                _success = !_failedImmediately && _operation != null;
                if (_success)
                {
                    _onSucceeded?.Invoke();
                    _error = SceneFlowError.None;
                    return;
                }

                _error = _immediateError.IsNone
                    ? new SceneFlowError(SceneFlowErrorCode.OperationFailed, SceneKey, "Editor scene flow operation failed.")
                    : _immediateError;
                _onFailed?.Invoke();
            }
        }
    }
}
