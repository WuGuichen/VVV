using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Resources.Unity
{
    public sealed class ResourceSceneFlowDriver : ISceneFlowDriver, IDisposable
    {
        private static readonly HashSet<ResourceSceneFlowDriver> RetiringDrivers = new HashSet<ResourceSceneFlowDriver>();
        private static readonly HashSet<string> LoadingScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly IResourceCatalogQuery _catalogQuery;
        private readonly SceneAssetBundleProvider _sceneProvider;
        private readonly Dictionary<string, SceneAssetBundleHandle> _loadedScenes = new Dictionary<string, SceneAssetBundleHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, Scene> _ownedScenes = new Dictionary<string, Scene>(StringComparer.Ordinal);
        private readonly Dictionary<string, ResourceSceneFlowOperation> _unloadingScenes = new Dictionary<string, ResourceSceneFlowOperation>(StringComparer.Ordinal);
        private readonly List<ResourceSceneFlowOperation> _pendingOperations = new List<ResourceSceneFlowOperation>();
        private readonly List<string> _retirementKeys = new List<string>();
        private readonly Func<Scene, AsyncOperation> _unloadScene;
        private bool _retiring;
        private bool _draining;
        private bool _drainAgain;
        private SceneFlowError _retirementError;

        public ResourceSceneFlowDriver(IResourceCatalogQuery catalogQuery, SceneAssetBundleProvider sceneProvider)
            : this(catalogQuery, sceneProvider, null)
        {
        }

        public ResourceSceneFlowDriver(IResourceCatalogQuery catalogQuery, SceneAssetBundleProvider sceneProvider,
            Func<Scene, AsyncOperation> unloadScene)
        {
            _catalogQuery = catalogQuery ?? throw new ArgumentNullException(nameof(catalogQuery));
            _sceneProvider = sceneProvider ?? throw new ArgumentNullException(nameof(sceneProvider));
            _unloadScene = unloadScene ?? (scene => SceneManager.UnloadSceneAsync(scene));
        }

        public int LoadedSceneCount => _loadedScenes.Count;
        public bool IsRetiring => _retiring;
        public bool IsRetired => _retiring && _loadedScenes.Count == 0 && _pendingOperations.Count == 0;
        public SceneFlowError RetirementError => _retirementError;

        /// <summary>Begins asynchronous retirement; repeated calls retry an outstanding unload failure.</summary>
        /// <remarks>LowFreqAlloc. Unity completion callbacks retain and drain ownership without a session Tick.</remarks>
        public void Dispose()
        {
            if (IsRetired) return;
            _retiring = true;
            _retirementError = SceneFlowError.None;
            RetiringDrivers.Add(this);
            DrainRetirement();
        }

        public ISceneFlowOperation LoadScene(SceneFlowRequest request)
        {
            if (_retiring || _loadedScenes.ContainsKey(request.SceneKey ?? string.Empty))
                return ResourceSceneFlowOperation.Failed(request.SceneKey, SceneFlowErrorCode.Busy,
                    "Scene driver is retiring or already owns this scene key.");
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
            if (!LoadingScenePaths.Add(handle.ScenePath))
            {
                _sceneProvider.Release(handle);
                return ResourceSceneFlowOperation.Failed(request.SceneKey, SceneFlowErrorCode.Busy,
                    "Another resource scene load is already using this scene path.");
            }
            Scene ownedScene = default;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> captureScene = (scene, mode) =>
            {
                if (string.Equals(scene.path, handle.ScenePath, StringComparison.OrdinalIgnoreCase)) ownedScene = scene;
            };
            SceneManager.sceneLoaded += captureScene;
            Action stopCapture = () =>
            {
                SceneManager.sceneLoaded -= captureScene;
                LoadingScenePaths.Remove(handle.ScenePath);
            };
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(handle.ScenePath, GetLoadSceneMode(request.LoadMode));
                if (operation == null)
                {
                    stopCapture();
                    _sceneProvider.Release(handle);
                    return ResourceSceneFlowOperation.Failed(
                        request.SceneKey,
                        SceneFlowErrorCode.DriverFailure,
                        "Unity SceneManager.LoadSceneAsync returned null for resource scene '" + handle.ScenePath + "'.");
                }

                var tracked = ResourceSceneFlowOperation.FromAsync(
                    request.SceneKey,
                    operation,
                    () => { stopCapture(); CompleteLoad(request, handle, ownedScene); },
                    () =>
                    {
                        stopCapture();
                        if (!_loadedScenes.TryGetValue(request.SceneKey, out SceneAssetBundleHandle retained)
                            || !ReferenceEquals(retained, handle)) _sceneProvider.Release(handle);
                    });
                TrackOperation(tracked);
                return tracked;
            }
            catch (Exception exception)
            {
                stopCapture();
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
            if (_unloadingScenes.TryGetValue(sceneKey, out ResourceSceneFlowOperation pending)) return pending;
            Scene scene = handle != null && _ownedScenes.TryGetValue(sceneKey, out Scene ownedScene)
                ? ownedScene
                : SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(sceneKey));

            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (handle != null && SceneManager.GetSceneByPath(handle.ScenePath).isLoaded)
                    return ResourceSceneFlowOperation.Failed(sceneKey, SceneFlowErrorCode.DriverFailure,
                        "Scene ownership could not be confirmed; its bundle remains retained.");
                if (handle != null)
                    CompleteUnload(sceneKey, handle);
                return ResourceSceneFlowOperation.Succeeded(sceneKey);
            }

            try
            {
                AsyncOperation operation = _unloadScene(scene);
                if (operation == null)
                {
                    return ResourceSceneFlowOperation.Failed(sceneKey, SceneFlowErrorCode.DriverFailure,
                        "Unity SceneManager.UnloadSceneAsync returned null; scene ownership remains retained.");
                }

                var tracked = ResourceSceneFlowOperation.FromAsync(
                    sceneKey,
                    operation,
                    () => CompleteUnload(sceneKey, handle),
                    null);
                _unloadingScenes[sceneKey] = tracked;
                TrackOperation(tracked);
                return tracked;
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

        private void CompleteLoad(SceneFlowRequest request, SceneAssetBundleHandle handle, Scene ownedScene)
        {
            _loadedScenes[request.SceneKey] = handle;
            _ownedScenes[request.SceneKey] = ownedScene;
            if (!ownedScene.IsValid() || !ownedScene.isLoaded)
                throw new InvalidOperationException("Loaded scene ownership could not be confirmed; its bundle remains retained.");
            if (request.LoadMode == SceneFlowLoadMode.Single)
                ReleasePreviousLoadedScenes(request.SceneKey);
        }

        private void CompleteUnload(string sceneKey, SceneAssetBundleHandle handle)
        {
            if (handle != null && _loadedScenes.TryGetValue(sceneKey, out SceneAssetBundleHandle current)
                && ReferenceEquals(current, handle))
            {
                if (_ownedScenes.TryGetValue(sceneKey, out Scene scene) && scene.isLoaded)
                    throw new InvalidOperationException("Scene is still loaded; its bundle cannot be released.");
                _sceneProvider.Release(handle);
                _loadedScenes.Remove(sceneKey);
                _ownedScenes.Remove(sceneKey);
            }
            _unloadingScenes.Remove(sceneKey);
        }

        private void ReleasePreviousLoadedScenes(string currentSceneKey)
        {
            var keys = new List<string>(_loadedScenes.Keys);
            for (int i = 0; i < keys.Count; i++)
                if (!string.Equals(keys[i], currentSceneKey, StringComparison.Ordinal))
                    CompleteUnload(keys[i], _loadedScenes[keys[i]]);
        }

        private void TrackOperation(ResourceSceneFlowOperation operation)
        {
            _pendingOperations.Add(operation);
            operation.ObserveCompletion(DrainRetirement);
        }

        private void DrainRetirement()
        {
            if (_draining) { _drainAgain = true; return; }
            do
            {
                _drainAgain = false;
                _draining = true;
                try
                {
                    for (int i = _pendingOperations.Count - 1; i >= 0; i--)
                    {
                        ResourceSceneFlowOperation operation = _pendingOperations[i];
                        if (!operation.IsDone) continue;
                        _pendingOperations.RemoveAt(i);
                        if (!operation.Success)
                        {
                            _unloadingScenes.Remove(operation.SceneKey);
                            if (_retiring && _loadedScenes.ContainsKey(operation.SceneKey))
                                _retirementError = operation.Error;
                        }
                    }
                    if (_retiring && _retirementError.IsNone)
                    {
                        _retirementKeys.Clear();
                        foreach (string key in _loadedScenes.Keys) _retirementKeys.Add(key);
                        for (int i = 0; i < _retirementKeys.Count; i++)
                        {
                            string key = _retirementKeys[i];
                            if (_unloadingScenes.ContainsKey(key)) continue;
                            ISceneFlowOperation unload = UnloadScene(key);
                            if (unload.IsDone && !unload.Success) { _retirementError = unload.Error; break; }
                        }
                    }
                    if (IsRetired) RetiringDrivers.Remove(this);
                }
                finally { _draining = false; }
            } while (_drainAgain);
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
            private Action _completion;

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

            public static ResourceSceneFlowOperation FromAsync(
                string sceneKey,
                AsyncOperation operation,
                Action onSucceeded,
                Action onFailed)
            {
                return new ResourceSceneFlowOperation(sceneKey, operation, onSucceeded, onFailed);
            }

            public void ObserveCompletion(Action completion)
            {
                _completion = completion;
                _operation.completed += OnCompleted;
                if (IsDone) OnCompleted(_operation);
            }

            private void OnCompleted(AsyncOperation operation)
            {
                operation.completed -= OnCompleted;
                FinalizeIfDone();
                _completion?.Invoke();
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
                    try
                    {
                        _onSucceeded?.Invoke();
                        _error = SceneFlowError.None;
                        return;
                    }
                    catch (Exception exception)
                    {
                        _success = false;
                        _error = new SceneFlowError(SceneFlowErrorCode.DriverFailure, SceneKey,
                            "Resource scene completion failed: " + exception.Message, exception);
                    }
                }
                else
                    _error = _immediateError.IsNone
                        ? new SceneFlowError(SceneFlowErrorCode.OperationFailed, SceneKey, "Resource scene flow operation failed.")
                        : _immediateError;
                try { _onFailed?.Invoke(); }
                catch (Exception exception)
                {
                    _error = new SceneFlowError(SceneFlowErrorCode.DriverFailure, SceneKey,
                        "Resource scene failure cleanup failed: " + exception.Message, exception);
                }
            }
        }
    }
}
