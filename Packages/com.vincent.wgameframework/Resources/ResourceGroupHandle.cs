using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Resources
{
    public sealed class ResourceGroupHandle
    {
        private readonly List<ResourceHandle<object>> _handles;
        private readonly ReadOnlyCollection<ResourceHandle<object>> _readOnlyHandles;

        internal ResourceGroupHandle(string groupId, IEnumerable<ResourceHandle<object>> handles)
        {
            GroupId = groupId ?? string.Empty;
            _handles = handles != null
                ? new List<ResourceHandle<object>>(handles)
                : new List<ResourceHandle<object>>();
            _readOnlyHandles = _handles.AsReadOnly();
        }

        public string GroupId { get; }
        public IReadOnlyList<ResourceHandle<object>> Handles => _readOnlyHandles;
        public bool IsReleased { get; private set; }
        internal bool IsTransferred { get; private set; }

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
