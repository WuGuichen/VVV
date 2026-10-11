using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Resources.Unity
{
    public sealed class ResourceSceneFlowDriver : ISceneFlowDriver
    {
        private readonly IResourceCatalogQuery _catalogQuery;
        private readonly SceneAssetBundleProvider _sceneProvider;
        private readonly Dictionary<string, SceneAssetBundleHandle> _loadedScenes = new Dictionary<string, SceneAssetBundleHandle>(StringComparer.Ordinal);

        public ResourceSceneFlowDriver(IResourceCatalogQuery catalogQuery, SceneAssetBundleProvider sceneProvider)
        {
            _catalogQuery = catalogQuery ?? throw new ArgumentNullException(nameof(catalogQuery));
            _sceneProvider = sceneProvider ?? throw new ArgumentNullException(nameof(sceneProvider));
        }

        public int LoadedSceneCount => _loadedScenes.Count;

        public ISceneFlowOperation LoadScene(SceneFlowRequest request)
        {
            ResourceKey key = CreateSceneKey(request.SceneKey);
            if (!key.IsValid)
            {
                return ResourceSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.InvalidRequest,
                    "Scene resource key is invalid.");
            }

            if (!_catalogQuery.TryGetEntry(key, out ResourceCatalogEntry entry))
            {
                return ResourceSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Scene resource was not found in catalog: " + key + ".");
            }

            if (!_sceneProvider.CanLoad(entry))
            {
                return ResourceSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Scene catalog entry must use provider '" + SceneAssetBundleProvider.Id + "'.");
            }

            ResourceLoadResult<SceneAssetBundleHandle> bundleLoad = _sceneProvider.LoadSceneBundle(key, entry);
            if (!bundleLoad.Success)
                return ResourceSceneFlowOperation.Failed(request.SceneKey, ToSceneFlowError(bundleLoad.Error, request.SceneKey));

            SceneAssetBundleHandle handle = bundleLoad.Value;
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(handle.ScenePath, GetLoadSceneMode(request.LoadMode));
                if (operation == null)
                {
                    _sceneProvider.Release(handle);
                    return ResourceSceneFlowOperation.Failed(
                        request.SceneKey,
                        SceneFlowErrorCode.DriverFailure,
                        "Unity SceneManager.LoadSceneAsync returned null for resource scene '" + handle.ScenePath + "'.");
                }

                return ResourceSceneFlowOperation.FromAsync(
                    request.SceneKey,
                    operation,
                    () => CompleteLoad(request, handle),
                    () => _sceneProvider.Release(handle));
            }
            catch (Exception exception)
            {
                _sceneProvider.Release(handle);
                return ResourceSceneFlowOperation.Failed(
                    request.SceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Unity SceneManager.LoadSceneAsync failed for resource scene '" + handle.ScenePath + "': " + exception.Message,
                    exception);
            }
        }

        public ISceneFlowOperation UnloadScene(string sceneKey)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                return ResourceSceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.InvalidRequest,
                    "Scene key is missing.");
            }

            _loadedScenes.TryGetValue(sceneKey, out SceneAssetBundleHandle handle);
            Scene scene = handle != null
                ? SceneManager.GetSceneByPath(handle.ScenePath)
                : SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(sceneKey));

            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (handle != null)
                    CompleteUnload(sceneKey, handle);
                return ResourceSceneFlowOperation.Succeeded(sceneKey);
            }

            try
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null)
                {
                    if (handle != null)
                        CompleteUnload(sceneKey, handle);
                    return ResourceSceneFlowOperation.Succeeded(sceneKey);
                }

                return ResourceSceneFlowOperation.FromAsync(
                    sceneKey,
                    operation,
                    () => CompleteUnload(sceneKey, handle),
                    null);
            }
            catch (Exception exception)
            {
                return ResourceSceneFlowOperation.Failed(
                    sceneKey,
                    SceneFlowErrorCode.DriverFailure,
                    "Unity SceneManager.UnloadSceneAsync failed for resource scene '" + sceneKey + "': " + exception.Message,
                    exception);
            }
        }

        private static ResourceKey CreateSceneKey(string sceneKey)
        {
            return new ResourceKey(sceneKey, ResourceTypeIds.Scene);
        }

        private void CompleteLoad(SceneFlowRequest request, SceneAssetBundleHandle handle)
        {
            if (request.LoadMode == SceneFlowLoadMode.Single)
                ReleaseAllLoadedScenes();

            if (_loadedScenes.TryGetValue(request.SceneKey, out SceneAssetBundleHandle existing))
                _sceneProvider.Release(existing);

            _loadedScenes[request.SceneKey] = handle;
        }

        private void CompleteUnload(string sceneKey, SceneAssetBundleHandle handle)
        {
            _loadedScenes.Remove(sceneKey);
            if (handle != null)
                _sceneProvider.Release(handle);
        }

        private void ReleaseAllLoadedScenes()
        {
            foreach (SceneAssetBundleHandle handle in _loadedScenes.Values)
                _sceneProvider.Release(handle);

            _loadedScenes.Clear();
        }

        private static LoadSceneMode GetLoadSceneMode(SceneFlowLoadMode loadMode)
        {
            return loadMode == SceneFlowLoadMode.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;
        }

        private static SceneFlowError ToSceneFlowError(ResourceError error, string sceneKey)
        {
            SceneFlowErrorCode code = error.Code == ResourceErrorCode.InvalidKey
                ? SceneFlowErrorCode.InvalidRequest
                : SceneFlowErrorCode.DriverFailure;
            return new SceneFlowError(code, sceneKey, error.Message);
        }

        private sealed class ResourceSceneFlowOperation : ISceneFlowOperation
        {
            private readonly AsyncOperation _operation;
            private readonly Action _onSucceeded;
            private readonly Action _onFailed;
            private readonly SceneFlowError _immediateError;
            private readonly bool _failedImmediately;
            private bool _finalized;
            private bool _success;
            private SceneFlowError _error;

            private ResourceSceneFlowOperation(
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

            private ResourceSceneFlowOperation(string sceneKey, SceneFlowError error, bool success)
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
                return new ResourceSceneFlowOperation(sceneKey, operation, onSucceeded, onFailed);
            }

            public static ISceneFlowOperation Succeeded(string sceneKey)
            {
                return new ResourceSceneFlowOperation(sceneKey, SceneFlowError.None, true);
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
                    error = new SceneFlowError(SceneFlowErrorCode.OperationFailed, sceneKey, "Resource scene flow operation failed.");
                return new ResourceSceneFlowOperation(sceneKey, error, false);
            }

            private void FinalizeIfDone()
            {
                if (_finalized)
                    return;
                if (!IsDone)
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
                    ? new SceneFlowError(SceneFlowErrorCode.OperationFailed, SceneKey, "Resource scene flow operation failed.")
                    : _immediateError;
                _onFailed?.Invoke();
            }
        }
    }
}
