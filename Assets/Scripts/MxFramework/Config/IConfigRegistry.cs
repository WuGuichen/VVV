namespace MxFramework.Config
{
    public interface IConfigRegistry : IConfigProvider
    {
        void RegisterProvider<T>(IConfigProvider provider) where T : IConfigData;
        bool TryGetProvider<T>(out IConfigProvider provider) where T : IConfigData;
        IConfigProvider GetProvider<T>() where T : IConfigData;
        void ClearProviders();
    }
}
