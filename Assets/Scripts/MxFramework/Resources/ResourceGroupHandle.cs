using System.Collections.Generic;

namespace MxFramework.Resources
{
    public sealed class ResourceGroupHandle
    {
        private readonly List<ResourceHandle<object>> _handles;

        internal ResourceGroupHandle(string groupId, IEnumerable<ResourceHandle<object>> handles)
        {
            GroupId = groupId ?? string.Empty;
            _handles = handles != null
                ? new List<ResourceHandle<object>>(handles)
                : new List<ResourceHandle<object>>();
        }

        public string GroupId { get; }
        public IReadOnlyList<ResourceHandle<object>> Handles => _handles;
        public bool IsReleased { get; private set; }

        internal bool TryMarkReleased()
        {
            if (IsReleased)
                return false;

            IsReleased = true;
            return true;
        }
    }
}
