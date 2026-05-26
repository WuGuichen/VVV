using System;
using System.Collections.Generic;
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
        private readonly IResourceManager _resourceManager;

        public MxFairyGuiResourceBridge(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        public int LoadedScopeCount { get; private set; }
        public int ReleasedScopeCount { get; private set; }

        public MxFairyGuiPackageLoadResult LoadPackage(MxFairyGuiPackageDescriptor descriptor)
        {
            if (!TryValidateDescriptor(descriptor, out MxFairyGuiPackageLoadResult invalid))
                return invalid;

            if (!_resourceManager.Contains(descriptor.PackageBytesKey))
            {
                return MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.MissingPackage,
                    descriptor.PackageBytesKey,
                    "Package bytes resource is not registered.");
            }

            for (int i = 0; i < descriptor.Resources.Count; i++)
            {
                MxFairyGuiPackageResourceDescriptor resource = descriptor.Resources[i];
                if (!resource.Required || _resourceManager.Contains(resource.Key))
                    continue;

                return MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.MissingResource,
                    resource.Key,
                    "Required package resource is not registered.");
            }

            ResourceLoadResult<ResourceHandle<object>> packageResult;
            MxFairyGuiPackageLoadResult packageOperationResult = LoadObject(descriptor.PackageBytesKey, out packageResult);
            if (packageOperationResult.Failed)
                return WithPackageStatus(packageOperationResult, descriptor.PackageBytesKey);

            if (!(packageResult.Value.Value is byte[]))
            {
                _resourceManager.Release(packageResult.Value);
                return MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.LoadFailed,
                    descriptor.PackageBytesKey,
                    "Package bytes resource must resolve to byte array data.");
            }

            var loadedResources = new List<ResourceHandle<object>>();
            for (int i = 0; i < descriptor.Resources.Count; i++)
            {
                MxFairyGuiPackageResourceDescriptor resource = descriptor.Resources[i];
                ResourceLoadResult<ResourceHandle<object>> resourceResult;
                MxFairyGuiPackageLoadResult resourceOperationResult = LoadObject(resource.Key, out resourceResult);
                if (resourceOperationResult.Failed)
                {
                    ReleaseLoaded(packageResult.Value, loadedResources);
                    return resourceOperationResult;
                }

                loadedResources.Add(resourceResult.Value);
            }

            var scope = new MxFairyGuiPackageLoadScope(this, descriptor.PackageId, packageResult.Value, loadedResources);
            LoadedScopeCount++;
            return MxFairyGuiPackageLoadResult.Loaded(scope);
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

        private bool TryValidateDescriptor(MxFairyGuiPackageDescriptor descriptor, out MxFairyGuiPackageLoadResult invalid)
        {
            if (descriptor == null)
            {
                invalid = MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.InvalidDescriptor,
                    default,
                    "Package descriptor is required.");
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

            for (int i = 0; i < descriptor.Resources.Count; i++)
            {
                if (!descriptor.Resources[i].Key.IsValid)
                {
                    invalid = MxFairyGuiPackageLoadResult.FailedResult(
                        MxFairyGuiResourceBridgeStatus.InvalidDescriptor,
                        descriptor.Resources[i].Key,
                        "Package resource key is invalid.");
                    return false;
                }
            }

            invalid = default;
            return true;
        }

        private MxFairyGuiPackageLoadResult LoadObject(ResourceKey key, out ResourceLoadResult<ResourceHandle<object>> result)
        {
            IResourceOperation<ResourceHandle<object>> operation = _resourceManager.LoadAsync<object>(key);
            if (!operation.IsDone)
            {
                result = default;
                return MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.LoadPending,
                    key,
                    "Package resource load is still pending.");
            }

            result = operation.Result;
            if (!result.Success)
            {
                return MxFairyGuiPackageLoadResult.FailedResult(
                    MxFairyGuiResourceBridgeStatus.LoadFailed,
                    key,
                    result.Error.Message,
                    result.Error);
            }

            return MxFairyGuiPackageLoadResult.Loaded(new MxFairyGuiPackageLoadScope(this, string.Empty, result.Value, null));
        }

        private static MxFairyGuiPackageLoadResult WithPackageStatus(MxFairyGuiPackageLoadResult result, ResourceKey packageKey)
        {
            if (result.Status != MxFairyGuiResourceBridgeStatus.LoadFailed)
                return result;

            return MxFairyGuiPackageLoadResult.FailedResult(
                MxFairyGuiResourceBridgeStatus.LoadFailed,
                packageKey,
                result.Failure.Message,
                result.Failure.ResourceError);
        }

        private void ReleaseLoaded(ResourceHandle<object> packageHandle, List<ResourceHandle<object>> resourceHandles)
        {
            for (int i = resourceHandles.Count - 1; i >= 0; i--)
                _resourceManager.Release(resourceHandles[i]);

            _resourceManager.Release(packageHandle);
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
