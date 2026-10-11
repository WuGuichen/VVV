using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.Resources;

namespace MxFramework.UI.FairyGui
{
    public static class MxFairyGuiResourceTypeIds
    {
        public const string PackageBytes = "MxFairyGuiPackageBytes";
        public const string PackageAtlas = ResourceTypeIds.Texture2D;
        public const string PackageAudio = ResourceTypeIds.AudioClip;
        public const string PackageFont = ResourceTypeIds.Font;
        public const string PackageResource = ResourceTypeIds.Object;
    }

    public enum MxFairyGuiPackageResourceKind
    {
        Resource = 0,
        Atlas = 1,
        Audio = 2,
        Font = 3
    }

    public enum MxFairyGuiResourceBridgeStatus
    {
        None = 0,
        Loaded = 1,
        InvalidDescriptor = 2,
        MissingPackage = 3,
        MissingResource = 4,
        LoadFailed = 5,
        LoadPending = 6,
        Released = 7
    }

    public readonly struct MxFairyGuiPackageResourceDescriptor
    {
        public MxFairyGuiPackageResourceDescriptor(ResourceKey key, MxFairyGuiPackageResourceKind kind, bool required = true)
        {
            Key = key;
            Kind = kind;
            Required = required;
        }

        public ResourceKey Key { get; }
        public MxFairyGuiPackageResourceKind Kind { get; }
        public bool Required { get; }
    }

    public sealed class MxFairyGuiPackageDescriptor
    {
        private readonly List<MxFairyGuiPackageResourceDescriptor> _resources;

        public MxFairyGuiPackageDescriptor(
            string packageId,
            ResourceKey packageBytesKey,
            IEnumerable<MxFairyGuiPackageResourceDescriptor> resources = null)
        {
            PackageId = packageId ?? string.Empty;
            PackageBytesKey = packageBytesKey;
            _resources = resources != null
                ? new List<MxFairyGuiPackageResourceDescriptor>(resources)
                : new List<MxFairyGuiPackageResourceDescriptor>();
        }

        public string PackageId { get; }
        public ResourceKey PackageBytesKey { get; }
        public IReadOnlyList<MxFairyGuiPackageResourceDescriptor> Resources => _resources;
    }

    public readonly struct MxFairyGuiResourceFailure
    {
        public MxFairyGuiResourceFailure(
            MxFairyGuiResourceBridgeStatus status,
            ResourceKey key,
            string message,
            ResourceError resourceError = default)
        {
            Status = status;
            Key = key;
            Message = message ?? string.Empty;
            ResourceError = resourceError;
        }

        public MxFairyGuiResourceBridgeStatus Status { get; }
        public ResourceKey Key { get; }
        public string Message { get; }
        public ResourceError ResourceError { get; }
        public bool HasResourceError => !ResourceError.IsNone;
    }

    public readonly struct MxFairyGuiPackageLoadResult
    {
        private MxFairyGuiPackageLoadResult(
            bool success,
            MxFairyGuiResourceBridgeStatus status,
            MxFairyGuiPackageLoadScope scope,
            MxFairyGuiResourceFailure failure)
        {
            Success = success;
            Status = status;
            Scope = scope;
            Failure = failure;
        }

        public bool Success { get; }
        public bool Failed => !Success;
        public MxFairyGuiResourceBridgeStatus Status { get; }
        public MxFairyGuiPackageLoadScope Scope { get; }
        public MxFairyGuiResourceFailure Failure { get; }

        public static MxFairyGuiPackageLoadResult Loaded(MxFairyGuiPackageLoadScope scope)
        {
            if (scope == null)
                return FailedResult(MxFairyGuiResourceBridgeStatus.LoadFailed, default, "Loaded package scope is missing.");

            return new MxFairyGuiPackageLoadResult(true, MxFairyGuiResourceBridgeStatus.Loaded, scope, default);
        }

        public static MxFairyGuiPackageLoadResult FailedResult(MxFairyGuiResourceBridgeStatus status, ResourceKey key, string message, ResourceError resourceError = default)
        {
            if (status == MxFairyGuiResourceBridgeStatus.None || status == MxFairyGuiResourceBridgeStatus.Loaded)
                status = MxFairyGuiResourceBridgeStatus.LoadFailed;

            return new MxFairyGuiPackageLoadResult(
                false,
                status,
                null,
                new MxFairyGuiResourceFailure(status, key, message, resourceError));
        }
    }

    public sealed class MxFairyGuiPackageLoadScope : IDisposable
    {
        private readonly MxFairyGuiResourceBridge _bridge;
        private readonly List<ResourceHandle<object>> _resourceHandles;

        internal MxFairyGuiPackageLoadScope(
            MxFairyGuiResourceBridge bridge,
            string packageId,
            ResourceHandle<object> packageHandle,
            List<ResourceHandle<object>> resourceHandles)
        {
            _bridge = bridge;
            PackageId = packageId ?? string.Empty;
            PackageHandle = packageHandle;
            _resourceHandles = resourceHandles ?? new List<ResourceHandle<object>>();
        }

        public string PackageId { get; }
        public ResourceHandle<object> PackageHandle { get; private set; }
        public IReadOnlyList<ResourceHandle<object>> ResourceHandles => _resourceHandles;
        public bool IsReleased { get; private set; }

        public void Release()
        {
            _bridge.Release(this);
        }

        public void Dispose()
        {
            Release();
        }

        internal bool TryMarkReleased()
        {
            if (IsReleased)
                return false;

            IsReleased = true;
            return true;
        }
    }

    public sealed class MxFairyGuiResourceBridge
    {
        private IResourceManager _resourceManager;

        public MxFairyGuiResourceBridge(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        public void UpdateResourceManager(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        public int LoadedScopeCount { get; private set; }
        public int ReleasedScopeCount { get; private set; }

        public MxFairyGuiPackageLoadResult LoadPackage(MxFairyGuiPackageDescriptor descriptor)
        {
            IResourceOperation<MxFairyGuiPackageLoadResult> operation = LoadPackageAsync(descriptor);
            if (!operation.IsDone)
            {
                operation.Cancel();
                ResourceLoadResult<MxFairyGuiPackageLoadResult> cancelled = operation.Result;
                ResourceKey pendingKey = descriptor != null ? descriptor.PackageBytesKey : default;
                if (!cancelled.Success && cancelled.Error.Key.IsValid)
                    pendingKey = cancelled.Error.Key;
                return MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.LoadPending,
                    pendingKey,
                    "Package resource load is still pending.");
            }

            ResourceLoadResult<MxFairyGuiPackageLoadResult> result = operation.Result;
            return result.Success
                ? result.Value
                : MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.LoadFailed,
                    result.Error.Key,
                    result.Error.Message,
                    result.Error);
        }

        /// <remarks>LowFreqAlloc; the returned operation owns loaded handles until its scope is released.</remarks>
        public IResourceOperation<MxFairyGuiPackageLoadResult> LoadPackageAsync(
            MxFairyGuiPackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return new PackageLoadOperation(this, descriptor, cancellationToken);
        }

        public MxFairyGuiPackageLoadResult ToReleasedResult(MxFairyGuiPackageLoadScope scope)
        {
            ResourceKey key = scope != null && scope.PackageHandle != null ? scope.PackageHandle.Key : default;
            return MxFairyGuiPackageLoadResult.FailedResult(
                MxFairyGuiResourceBridgeStatus.Released,
                key,
                "Package load scope has been released.");
        }

        public void Release(MxFairyGuiPackageLoadScope scope)
        {
            if (scope == null || !scope.TryMarkReleased())
                return;

            for (int i = scope.ResourceHandles.Count - 1; i >= 0; i--)
                _resourceManager.Release(scope.ResourceHandles[i]);

            _resourceManager.Release(scope.PackageHandle);
            ReleasedScopeCount++;
        }

        private bool TryPrepareDescriptor(
            MxFairyGuiPackageDescriptor descriptor,
            out List<ResourceKey> keys,
            out MxFairyGuiPackageLoadResult invalid)
        {
            keys = null;
            if (descriptor == null)
            {
                invalid = MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.InvalidDescriptor,
                    default,
                    "Package descriptor is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.PackageId))
            {
                invalid = MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.InvalidDescriptor,
                    descriptor.PackageBytesKey,
                    "Package id is required.");
                return false;
            }

            if (!descriptor.PackageBytesKey.IsValid)
            {
                invalid = MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.InvalidDescriptor,
                    descriptor.PackageBytesKey,
                    "Package bytes resource key is invalid.");
                return false;
            }

            if (!_resourceManager.Contains(descriptor.PackageBytesKey))
            {
                invalid = MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.MissingPackage,
                    descriptor.PackageBytesKey,
                    "Package bytes resource is not registered.");
                return false;
            }

            keys = new List<ResourceKey>(descriptor.Resources.Count + 1)
            {
                descriptor.PackageBytesKey
            };
            for (int i = 0; i < descriptor.Resources.Count; i++)
            {
                MxFairyGuiPackageResourceDescriptor resource = descriptor.Resources[i];
                if (!resource.Key.IsValid)
                {
                    invalid = MxFairyGuiPackageLoadResult.FailedResult(
                        MxFairyGuiResourceBridgeStatus.InvalidDescriptor,
                        resource.Key,
                        "Package resource key is invalid.");
                    return false;
                }

                if (!_resourceManager.Contains(resource.Key))
                {
                    if (!resource.Required)
                        continue;

                    invalid = MxFairyGuiPackageLoadResult.FailedResult(
                        MxFairyGuiResourceBridgeStatus.MissingResource,
                        resource.Key,
                        "Required package resource is not registered.");
                    return false;
                }

                keys.Add(resource.Key);
            }

            invalid = default;
            return true;
        }

        private void ReleaseLoaded(ResourceHandle<object> packageHandle, List<ResourceHandle<object>> resourceHandles)
        {
            for (int i = resourceHandles.Count - 1; i >= 0; i--)
                _resourceManager.Release(resourceHandles[i]);

            _resourceManager.Release(packageHandle);
        }

        private sealed class PackageLoadOperation : IResourceOperation<MxFairyGuiPackageLoadResult>
        {
            private readonly MxFairyGuiResourceBridge _bridge;
            private readonly MxFairyGuiPackageDescriptor _descriptor;
            private readonly CancellationToken _cancellationToken;
            private readonly List<ResourceKey> _keys;
            private readonly List<ResourceHandle<object>> _resourceHandles;
            private IResourceOperation<ResourceHandle<object>> _current;
            private ResourceHandle<object> _packageHandle;
            private ResourceLoadResult<MxFairyGuiPackageLoadResult> _result;
            private int _nextKeyIndex;
            private bool _isDone;
            private bool _isCancelled;

            public PackageLoadOperation(
                MxFairyGuiResourceBridge bridge,
                MxFairyGuiPackageDescriptor descriptor,
                CancellationToken cancellationToken)
            {
                _bridge = bridge;
                _descriptor = descriptor;
                _cancellationToken = cancellationToken;
                _resourceHandles = new List<ResourceHandle<object>>(
                    descriptor != null ? descriptor.Resources.Count : 0);
                _result = ResourceLoadResult<MxFairyGuiPackageLoadResult>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    descriptor != null ? descriptor.PackageBytesKey : default,
                    string.Empty,
                    "FairyGUI package load is pending."));

                if (!_bridge.TryPrepareDescriptor(descriptor, out _keys, out MxFairyGuiPackageLoadResult invalid))
                    Complete(invalid);
            }

            public bool IsDone
            {
                get
                {
                    Pump();
                    return _isDone;
                }
            }

            public bool IsCancelled
            {
                get
                {
                    Pump();
                    return _isCancelled;
                }
            }

            public float Progress
            {
                get
                {
                    Pump();
                    if (_isDone || _keys == null || _keys.Count == 0)
                        return 1f;

                    float completed = _nextKeyIndex;
                    if (_current != null)
                        completed += Clamp01(_current.Progress);
                    return Clamp01(completed / _keys.Count);
                }
            }

            public ResourceLoadResult<MxFairyGuiPackageLoadResult> Result
            {
                get
                {
                    Pump();
                    return _result;
                }
            }

            public void Cancel()
            {
                if (_isDone)
                    return;

                _isCancelled = true;
                if (_current != null)
                {
                    if (_current.IsDone)
                    {
                        ResourceLoadResult<ResourceHandle<object>> currentResult = _current.Result;
                        if (currentResult.Success)
                            _bridge._resourceManager.Release(currentResult.Value);
                    }
                    else
                    {
                        _current.Cancel();
                    }

                    _current = null;
                }

                _bridge.ReleaseLoaded(_packageHandle, _resourceHandles);
                _packageHandle = null;
                _resourceHandles.Clear();
                ResourceError error = new ResourceError(
                    ResourceErrorCode.Cancelled,
                    CurrentKey,
                    string.Empty,
                    "FairyGUI package load was cancelled.");
                _result = ResourceLoadResult<MxFairyGuiPackageLoadResult>.Failed(error);
                _isDone = true;
            }

            private ResourceKey CurrentKey =>
                _keys != null && _nextKeyIndex < _keys.Count ? _keys[_nextKeyIndex] : default;

            private void Pump()
            {
                if (_isDone)
                    return;

                if (_cancellationToken.IsCancellationRequested)
                {
                    Cancel();
                    return;
                }

                while (!_isDone)
                {
                    if (_current == null)
                    {
                        if (_nextKeyIndex >= _keys.Count)
                        {
                            var scope = new MxFairyGuiPackageLoadScope(
                                _bridge,
                                _descriptor.PackageId,
                                _packageHandle,
                                _resourceHandles);
                            _bridge.LoadedScopeCount++;
                            Complete(MxFairyGuiPackageLoadResult.Loaded(scope));
                            return;
                        }

                        _current = _bridge._resourceManager.LoadAsync<object>(
                            _keys[_nextKeyIndex],
                            _cancellationToken);
                        if (_current == null)
                        {
                            CompleteFailure(new ResourceError(
                                ResourceErrorCode.ProviderFailed,
                                CurrentKey,
                                string.Empty,
                                "Resource manager returned no load operation."));
                            return;
                        }
                    }

                    if (!_current.IsDone)
                        return;

                    ResourceKey key = _keys[_nextKeyIndex];
                    ResourceLoadResult<ResourceHandle<object>> loaded = _current.Result;
                    _current = null;
                    if (!loaded.Success)
                    {
                        CompleteFailure(loaded.Error);
                        return;
                    }

                    if (_nextKeyIndex == 0)
                    {
                        _packageHandle = loaded.Value;
                        if (!MxFairyGuiPackageBytesUtility.TryGetBytes(_packageHandle.Value, out _))
                        {
                            CompleteFailure(new ResourceError(
                                ResourceErrorCode.TypeMismatch,
                                key,
                                _packageHandle.ProviderId,
                                "Package bytes resource must resolve to byte array data or Unity TextAsset bytes."));
                            return;
                        }
                    }
                    else
                    {
                        _resourceHandles.Add(loaded.Value);
                    }

                    _nextKeyIndex++;
                }
            }

            private void CompleteFailure(ResourceError error)
            {
                bool packageFailure = _nextKeyIndex == 0;
                _bridge.ReleaseLoaded(_packageHandle, _resourceHandles);
                _packageHandle = null;
                _resourceHandles.Clear();
                Complete(MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.LoadFailed,
                    packageFailure && _descriptor != null ? _descriptor.PackageBytesKey : error.Key,
                    error.Message,
                    error));
            }

            private void Complete(MxFairyGuiPackageLoadResult result)
            {
                _result = ResourceLoadResult<MxFairyGuiPackageLoadResult>.Loaded(result);
                _isDone = true;
            }

            private static float Clamp01(float value)
            {
                if (value < 0f)
                    return 0f;
                return value > 1f ? 1f : value;
            }
        }
    }

    internal static class MxFairyGuiPackageBytesUtility
    {
        public static bool TryGetBytes(object value, out byte[] bytes)
        {
            if (value is byte[] rawBytes)
            {
                bytes = rawBytes;
                return bytes != null;
            }

            if (value is UnityEngine.TextAsset textAsset)
            {
                bytes = textAsset.bytes;
                return bytes != null;
            }

            bytes = null;
            return false;
        }
    }

    public static class MxFairyGuiResourceBridgeUiMapping
    {
        public static MxUiOpenResult ToOpenFailure(MxFairyGuiPackageLoadResult result)
        {
            if (result.Success)
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "Package load result is successful.");

            return MxUiOpenResult.Fail(ToOpenErrorCode(result.Status), result.Failure.Message);
        }

        public static MxUiOpenErrorCode ToOpenErrorCode(MxFairyGuiResourceBridgeStatus status)
        {
            switch (status)
            {
                case MxFairyGuiResourceBridgeStatus.LoadPending:
                    return MxUiOpenErrorCode.ResourcesPending;
                case MxFairyGuiResourceBridgeStatus.Released:
                    return MxUiOpenErrorCode.OperationCancelled;
                case MxFairyGuiResourceBridgeStatus.MissingPackage:
                case MxFairyGuiResourceBridgeStatus.MissingResource:
                case MxFairyGuiResourceBridgeStatus.InvalidDescriptor:
                case MxFairyGuiResourceBridgeStatus.LoadFailed:
                default:
                    return MxUiOpenErrorCode.ViewCreateFailed;
            }
        }
    }
}
