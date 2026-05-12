using System.Collections.Generic;

namespace MxFramework.Config
{
    public sealed class ConfigTable<T> : IConfigTable<T> where T : IConfigData
    {
        private readonly MemoryConfigProvider _provider;

        public ConfigTable(ConfigSchema schema = null, ConfigDuplicatePolicy duplicatePolicy = ConfigDuplicatePolicy.Throw)
        {
            Schema = schema ?? new ConfigSchema<T>(typeof(T).Name);
            _provider = new MemoryConfigProvider(duplicatePolicy);
        }

        public ConfigSchema Schema { get; }
        public IReadOnlyCollection<T> Rows => _provider.GetAllConfigs<T>();

        public void Add(T row)
        {
            _provider.Register(row);
        }

        public void RegisterRange(IEnumerable<T> rows)
        {
            _provider.RegisterRange(rows);
        }

        public bool TryGetConfig<TConfig>(int id, out TConfig config) where TConfig : IConfigData
        {
            return _provider.TryGetConfig(id, out config);
        }

        public ConfigResult<TConfig> GetConfigResult<TConfig>(int id) where TConfig : IConfigData
        {
            return _provider.GetConfigResult<TConfig>(id);
        }

        public TConfig GetConfig<TConfig>(int id) where TConfig : IConfigData
        {
            return _provider.GetConfig<TConfig>(id);
        }

        public IReadOnlyCollection<TConfig> GetAllConfigs<TConfig>() where TConfig : IConfigData
        {
            return _provider.GetAllConfigs<TConfig>();
        }

        public bool ContainsConfig<TConfig>(int id) where TConfig : IConfigData
        {
            return _provider.ContainsConfig<TConfig>(id);
        }

        public void Reload()
        {
            _provider.Reload();
        }

        public ConfigTableValidationReport Validate(IConfigProvider resolver = null, ILocalizationProvider localizationProvider = null)
        {
            return ConfigTableValidator.Validate(this, resolver ?? this, localizationProvider);
        }
    }
}
