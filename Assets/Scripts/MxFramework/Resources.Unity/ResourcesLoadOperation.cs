using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public sealed class ResourcesLoadOperation : IResourceOperation<object>
    {
        private readonly ResourceKey _key;
        private readonly ResourceCatalogEntry _entry;
        private readonly ResourceRequest _request;
        private bool _cancelled;

        public ResourcesLoadOperation(ResourceKey key, ResourceCatalogEntry entry, ResourceRequest request)
        {
            _key = key;
            _entry = entry;
            _request = request;
        }

        public bool IsDone => _cancelled || _request == null || _request.isDone;
        public bool IsCancelled => _cancelled;
        public float Progress => _cancelled || _request == null ? 1f : _request.progress;

        public ResourceLoadResult<object> Result
        {
            get
            {
                if (_cancelled)
                {
                    return ResourceLoadResult<object>.Failed(new ResourceError(
                        ResourceErrorCode.Cancelled,
                        _key,
                        ResourcesProvider.Id,
                        "Resources operation was cancelled.",
                        _entry != null ? _entry.Address : string.Empty));
                }

                if (_request == null || !_request.isDone)
                {
                    return ResourceLoadResult<object>.Failed(new ResourceError(
                        ResourceErrorCode.ProviderFailed,
                        _key,
                        ResourcesProvider.Id,
                        "Resources operation has not completed.",
                        _entry != null ? _entry.Address : string.Empty));
                }

                if (_request.asset == null)
                {
                    return ResourceLoadResult<object>.Failed(new ResourceError(
                        ResourceErrorCode.NotFound,
                        _key,
                        ResourcesProvider.Id,
                        "Unity Resources asset was not found.",
                        _entry != null ? _entry.Address : string.Empty));
                }

                return ResourceLoadResult<object>.Loaded(_request.asset);
            }
        }

        public void Cancel()
        {
            _cancelled = true;
        }
    }
}
