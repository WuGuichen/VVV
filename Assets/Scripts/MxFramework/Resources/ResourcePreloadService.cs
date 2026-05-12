using System;
using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Resources
{
    public interface IResourcePreloadService
    {
        IResourceOperation<ResourcePreloadResult> PreloadAsync(
            ResourcePreloadPlan plan,
            CancellationToken cancellationToken = default);

        void ReleaseGroup(ResourceGroupHandle handle);
    }

    public sealed class ResourcePreloadService : IResourcePreloadService
    {
        private readonly IResourceManager _resourceManager;
        private readonly IResourceCatalogQuery _catalogQuery;

        public ResourcePreloadService(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _catalogQuery = resourceManager as IResourceCatalogQuery;
        }

        public IResourceOperation<ResourcePreloadResult> PreloadAsync(
            ResourcePreloadPlan plan,
            CancellationToken cancellationToken = default)
        {
            if (plan == null)
            {
                return new ImmediateResourceOperation<ResourcePreloadResult>(
                    ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                        ResourceErrorCode.InvalidCatalog,
                        default,
                        string.Empty,
                        "Resource preload plan is missing.")));
            }

            var requestedKeys = CollectKeys(plan);
            var handles = new List<ResourceHandle<object>>();
            var errors = new List<ResourceError>();

            for (int i = 0; i < requestedKeys.Count; i++)
            {
                ResourceKey key = requestedKeys[i];
                if (cancellationToken.IsCancellationRequested)
                {
                    ReleaseHandles(handles);
                    return Cancelled(plan, requestedKeys, handles, errors, key);
                }

                ResourceLoadResult<ResourceHandle<object>> result = _resourceManager.LoadAsync<object>(key, cancellationToken).Result;
                if (result.Success)
                {
                    handles.Add(result.Value);
                    continue;
                }

                errors.Add(result.Error);
                if (plan.FailFast)
                    break;
            }

            var handle = new ResourceGroupHandle(plan.GroupId, handles);
            var preloadResult = new ResourcePreloadResult(plan.GroupId, handle, requestedKeys, errors);
            return new ImmediateResourceOperation<ResourcePreloadResult>(
                ResourceLoadResult<ResourcePreloadResult>.Loaded(preloadResult));
        }

        public void ReleaseGroup(ResourceGroupHandle handle)
        {
            if (handle == null || !handle.TryMarkReleased())
                return;

            for (int i = handle.Handles.Count - 1; i >= 0; i--)
                _resourceManager.Release(handle.Handles[i]);
        }

        private List<ResourceKey> CollectKeys(ResourcePreloadPlan plan)
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();

            for (int i = 0; i < plan.ExplicitKeys.Count; i++)
                AddKey(plan.ExplicitKeys[i], keys, unique);

            if (_catalogQuery == null)
                return keys;

            for (int i = 0; i < plan.Labels.Count; i++)
            {
                IReadOnlyList<ResourceKey> labelKeys = _catalogQuery.FindKeysByLabel(plan.Labels[i]);
                for (int j = 0; j < labelKeys.Count; j++)
                    AddKey(labelKeys[j], keys, unique);
            }

            return keys;
        }

        private static void AddKey(ResourceKey key, List<ResourceKey> keys, HashSet<ResourceKey> unique)
        {
            if (!unique.Add(key))
                return;

            keys.Add(key);
        }

        private void ReleaseHandles(List<ResourceHandle<object>> handles)
        {
            for (int i = handles.Count - 1; i >= 0; i--)
                _resourceManager.Release(handles[i]);

            handles.Clear();
        }

        private static IResourceOperation<ResourcePreloadResult> Cancelled(
            ResourcePreloadPlan plan,
            List<ResourceKey> requestedKeys,
            List<ResourceHandle<object>> handles,
            List<ResourceError> errors,
            ResourceKey key)
        {
            var error = new ResourceError(
                ResourceErrorCode.Cancelled,
                key,
                string.Empty,
                "Resource preload operation was cancelled.");
            errors.Add(error);

            var handle = new ResourceGroupHandle(plan.GroupId, handles);
            handle.TryMarkReleased();
            var result = new ResourcePreloadResult(plan.GroupId, handle, requestedKeys, errors);
            return new ImmediateResourceOperation<ResourcePreloadResult>(
                ResourceLoadResult<ResourcePreloadResult>.Failed(error));
        }
    }
}
