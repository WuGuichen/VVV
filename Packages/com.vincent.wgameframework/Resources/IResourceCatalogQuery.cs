using System.Collections.Generic;

namespace MxFramework.Resources
{
    public interface IResourceCatalogQuery
    {
        IReadOnlyList<ResourceKey> FindKeysByLabel(string label);
        bool TryGetEntry(ResourceKey key, out ResourceCatalogEntry entry);
    }
}
