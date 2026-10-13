using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Resources
{
    public sealed class ResourceGroupHandle
    {
        private readonly List<ResourceHandle<object>> _handles;
        private readonly ReadOnlyCollection<ResourceHandle<object>> _readOnlyHandles;
        private readonly IResourceManager _resourceManager;
        private readonly List<ResourceKey> _acquiredKeys;

        internal ResourceGroupHandle(string groupId, IEnumerable<ResourceHandle<object>> handles)
            : this(groupId, handles, null, null)
        {
        }

        internal ResourceGroupHandle(
            string groupId,
            IEnumerable<ResourceHandle<object>> handles,
            IResourceManager resourceManager,
            IEnumerable<ResourceKey> acquiredKeys)
        {
            GroupId = groupId ?? string.Empty;
            _handles = handles != null
                ? new List<ResourceHandle<object>>(handles)
                : new List<ResourceHandle<object>>();
            _readOnlyHandles = _handles.AsReadOnly();
            _resourceManager = resourceManager;
            _acquiredKeys = acquiredKeys != null ? new List<ResourceKey>(acquiredKeys) : new List<ResourceKey>();
        }

        public string GroupId { get; }
        public IReadOnlyList<ResourceHandle<object>> Handles => _readOnlyHandles;
        public bool IsReleased { get; private set; }
        internal bool IsTransferred { get; private set; }

        internal bool IsOwnedBy(IResourceManager resourceManager)
        {
            return _resourceManager != null && ReferenceEquals(_resourceManager, resourceManager);
        }

        // Keys follow successful completion order, each paired with its actual manager acquisition.
        // Resolved keys may coincide; the handle objects and original request keys must remain distinct.
        internal bool HasCompleteAcquisitions(IResourceManager resourceManager, IReadOnlyList<ResourceKey> requestedKeys)
        {
            if (!IsOwnedBy(resourceManager) || IsReleased || IsTransferred ||
                requestedKeys == null || _acquiredKeys.Count != _handles.Count ||
                requestedKeys.Count != _handles.Count)
                return false;

            for (int i = 0; i < _handles.Count; i++)
            {
                ResourceHandle<object> handle = _handles[i];
                if (handle == null || handle.State != ResourceHandleState.Loaded)
                    return false;

                for (int j = 0; j < i; j++)
                {
                    if (ReferenceEquals(handle, _handles[j]) || _acquiredKeys[i] == _acquiredKeys[j])
                        return false;
                }

                int matchingRequests = 0;
                for (int j = 0; j < requestedKeys.Count; j++)
                {
                    if (_acquiredKeys[i] == requestedKeys[j])
                        matchingRequests++;
                }
                if (matchingRequests != 1)
                    return false;
            }

            return true;
        }

        internal bool TryMarkReleased()
        {
            if (IsReleased || IsTransferred)
                return false;

            IsReleased = true;
            return true;
        }

        internal bool TryTransferOwnership()
        {
            if (IsReleased || IsTransferred)
                return false;

            IsTransferred = true;
            return true;
        }
    }
}
