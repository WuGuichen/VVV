using System.Collections.Generic;

namespace MxFramework.Resources
{
    public sealed class ResourcePreloadResult
    {
        private readonly List<ResourceKey> _requestedKeys;
        private readonly List<ResourceError> _errors;

        public ResourcePreloadResult(
            string groupId,
            ResourceGroupHandle handle,
            IEnumerable<ResourceKey> requestedKeys,
            IEnumerable<ResourceError> errors)
        {
            GroupId = groupId ?? string.Empty;
            Handle = handle;
            _requestedKeys = requestedKeys != null
                ? new List<ResourceKey>(requestedKeys)
                : new List<ResourceKey>();
            _errors = errors != null
                ? new List<ResourceError>(errors)
                : new List<ResourceError>();
        }

        public string GroupId { get; }
        public ResourceGroupHandle Handle { get; }
        public IReadOnlyList<ResourceKey> RequestedKeys => _requestedKeys;
        public IReadOnlyList<ResourceError> Errors => _errors;
        public int RequestedCount => _requestedKeys.Count;
        public int LoadedCount => Handle != null ? Handle.Handles.Count : 0;
        public int FailedCount => _errors.Count;
        public bool Success => FailedCount == 0;
    }
}
