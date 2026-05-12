using System.Threading;

namespace MxFramework.Resources
{
    public sealed class ResourceLoadContext
    {
        public ResourceLoadContext(ResourceKey key, ResourceCatalogEntry entry)
        {
            Key = key;
            Entry = entry;
        }

        public ResourceKey Key { get; }
        public ResourceCatalogEntry Entry { get; }
    }

    public sealed class ResourceReleaseContext
    {
        public ResourceReleaseContext(ResourceKey key, ResourceCatalogEntry entry, object value)
        {
            Key = key;
            Entry = entry;
            Value = value;
        }

        public ResourceKey Key { get; }
        public ResourceCatalogEntry Entry { get; }
        public object Value { get; }
    }

    public interface IResourceProvider
    {
        string ProviderId { get; }
        bool CanLoad(ResourceCatalogEntry entry);
        ResourceLoadResult<object> Load(ResourceLoadContext context);
        IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken = default);
        void Release(ResourceReleaseContext context);
    }
}
