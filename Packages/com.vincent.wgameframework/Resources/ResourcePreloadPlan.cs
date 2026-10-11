using System.Collections.Generic;

namespace MxFramework.Resources
{
    public sealed class ResourcePreloadPlan
    {
        private readonly List<string> _labels;
        private readonly List<ResourceKey> _explicitKeys;

        public ResourcePreloadPlan(
            string groupId,
            IEnumerable<ResourceKey> explicitKeys = null,
            IEnumerable<string> labels = null,
            bool failFast = false,
            int maxConcurrentLoads = 1)
        {
            GroupId = groupId ?? string.Empty;
            FailFast = failFast;
            MaxConcurrentLoads = maxConcurrentLoads < 1 ? 1 : maxConcurrentLoads;
            _explicitKeys = explicitKeys != null
                ? new List<ResourceKey>(explicitKeys)
                : new List<ResourceKey>();
            _labels = labels != null
                ? new List<string>(labels)
                : new List<string>();
        }

        public string GroupId { get; }
        public IReadOnlyList<ResourceKey> ExplicitKeys => _explicitKeys;
        public IReadOnlyList<string> Labels => _labels;
        public bool FailFast { get; }
        public int MaxConcurrentLoads { get; }
    }
}
