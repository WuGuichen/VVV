using System;
using System.Collections.Generic;

namespace MxFramework.Config
{
    public sealed class ConfigRegistry : IConfigRegistry
    {
        private readonly Dictionary<Type, IConfigProvider> _providers;

        public ConfigRegistry()
        {
            _providers = new Dictionary<Type, IConfigProvider>();
        }

        public void RegisterProvider<T>(IConfigProvider provider) where T : IConfigData
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _providers[typeof(T)] = provider;
        }

        public bool TryGetProvider<T>(out IConfigProvider provider) where T : IConfigData
        {
            return _providers.TryGetValue(typeof(T), out provider);
        }

        public IConfigProvider GetProvider<T>() where T : IConfigData
        {
            if (TryGetProvider<T>(out IConfigProvider provider))
                return provider;

            throw new InvalidOperationException($"Config provider is not registered. Type={typeof(T).FullName}.");
        }

        public bool TryGetConfig<T>(int id, out T config) where T : IConfigData
        {
            config = default;
            return TryGetProvider<T>(out IConfigProvider provider) && provider.TryGetConfig(id, out config);
        }

        public ConfigResult<T> GetConfigResult<T>(int id) where T : IConfigData
        {
            if (!TryGetProvider<T>(out IConfigProvider provider))
                return ConfigResult<T>.Failed(ConfigError.TypeNotRegistered, $"Config provider is not registered. Type={typeof(T).FullName}.");

            return provider.GetConfigResult<T>(id);
        }

        public T GetConfig<T>(int id) where T : IConfigData
        {
            return GetProvider<T>().GetConfig<T>(id);
        }

        public IReadOnlyCollection<T> GetAllConfigs<T>() where T : IConfigData
        {
            return TryGetProvider<T>(out IConfigProvider provider)
                ? provider.GetAllConfigs<T>()
                : Array.Empty<T>();
        }

        public bool ContainsConfig<T>(int id) where T : IConfigData
        {
            return TryGetConfig<T>(id, out _);
        }

        public void Reload()
        {
            var reloaded = new HashSet<IConfigProvider>();
            foreach (IConfigProvider provider in _providers.Values)
            {
                if (reloaded.Add(provider))
                    provider.Reload();
            }
        }

        public void ClearProviders()
        {
            _providers.Clear();
        }
    }
}
