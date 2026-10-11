using System.Collections.Generic;

namespace MxFramework.Config
{
    public interface IConfigProvider
    {
        bool TryGetConfig<T>(int id, out T config) where T : IConfigData;
        ConfigResult<T> GetConfigResult<T>(int id) where T : IConfigData;
        T GetConfig<T>(int id) where T : IConfigData;
        IReadOnlyCollection<T> GetAllConfigs<T>() where T : IConfigData;
        bool ContainsConfig<T>(int id) where T : IConfigData;
        void Reload();
    }
}
