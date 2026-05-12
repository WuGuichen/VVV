using System.Threading;

namespace MxFramework.Resources
{
    public interface IResourceManager
    {
        IResourceManager RegisterProvider(IResourceProvider provider);
        IResourceManager AddCatalog(ResourceCatalog catalog);
        bool Contains(ResourceKey key);
        ResourceLoadResult<ResourceHandle<T>> Load<T>(ResourceKey key);
        IResourceOperation<ResourceHandle<T>> LoadAsync<T>(ResourceKey key, CancellationToken cancellationToken = default);
        void Release<T>(ResourceHandle<T> handle);
        ResourceDebugSnapshot CreateDebugSnapshot();
    }
}
