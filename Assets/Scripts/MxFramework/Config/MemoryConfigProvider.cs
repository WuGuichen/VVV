using System;
using System.Collections.Generic;

namespace MxFramework.Config
{
    public sealed class MemoryConfigProvider : IConfigProvider
    {
        private readonly Dictionary<Type, Dictionary<int, IConfigData>> _tables;

        public MemoryConfigProvider(ConfigDuplicatePolicy duplicatePolicy = ConfigDuplicatePolicy.Throw)
        {
            DuplicatePolicy = duplicatePolicy;
            _tables = new Dictionary<Type, Dictionary<int, IConfigData>>();
        }

        public ConfigDuplicatePolicy DuplicatePolicy { get; set; }

        public void Register<T>(T config) where T : IConfigData
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            EnsureValidId(config.Id);

            Type type = typeof(T);
            Dictionary<int, IConfigData> table = GetOrCreateTable(type);
            if (table.ContainsKey(config.Id))
            {
                if (DuplicatePolicy == ConfigDuplicatePolicy.Ignore)
                    return;
                if (DuplicatePolicy == ConfigDuplicatePolicy.Throw)
                    throw new DuplicateConfigException(type, config.Id);
            }

            table[config.Id] = config;
        }

        public void RegisterRange<T>(IEnumerable<T> configs) where T : IConfigData
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            foreach (T config in configs)
                Register(config);
        }

        public bool TryGetConfig<T>(int id, out T config) where T : IConfigData
        {
            config = default;
            if (!IsValidId(id))
                return false;

            if (!_tables.TryGetValue(typeof(T), out Dictionary<int, IConfigData> table))
                return false;

            if (!table.TryGetValue(id, out IConfigData data))
                return false;

            config = (T)data;
            return true;
        }

        public ConfigResult<T> GetConfigResult<T>(int id) where T : IConfigData
        {
            if (!IsValidId(id))
                return ConfigResult<T>.Failed(ConfigError.InvalidId, $"Invalid config id: {id}.");

            if (!_tables.ContainsKey(typeof(T)))
                return ConfigResult<T>.Failed(ConfigError.TypeNotRegistered, $"Config type is not registered: {typeof(T).FullName}.");

            return TryGetConfig(id, out T config)
                ? ConfigResult<T>.Found(config)
                : ConfigResult<T>.Failed(ConfigError.NotFound, $"Config not found. Type={typeof(T).FullName}, Id={id}.");
        }

        public T GetConfig<T>(int id) where T : IConfigData
        {
            if (TryGetConfig(id, out T config))
                return config;

            throw new ConfigNotFoundException(typeof(T), id);
        }

        public IReadOnlyCollection<T> GetAllConfigs<T>() where T : IConfigData
        {
            if (!_tables.TryGetValue(typeof(T), out Dictionary<int, IConfigData> table))
                return Array.Empty<T>();

            var values = new T[table.Count];
            int index = 0;
            foreach (IConfigData config in table.Values)
                values[index++] = (T)config;
            return values;
        }

        public bool ContainsConfig<T>(int id) where T : IConfigData
        {
            return TryGetConfig<T>(id, out _);
        }

        public void Remove<T>(int id) where T : IConfigData
        {
            if (_tables.TryGetValue(typeof(T), out Dictionary<int, IConfigData> table))
                table.Remove(id);
        }

        public void Clear<T>() where T : IConfigData
        {
            _tables.Remove(typeof(T));
        }

        public void ClearAll()
        {
            _tables.Clear();
        }

        public void Reload()
        {
        }

        private Dictionary<int, IConfigData> GetOrCreateTable(Type type)
        {
            if (!_tables.TryGetValue(type, out Dictionary<int, IConfigData> table))
            {
                table = new Dictionary<int, IConfigData>();
                _tables.Add(type, table);
            }

            return table;
        }

        private static void EnsureValidId(int id)
        {
            if (!IsValidId(id))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Config id must be greater than 0.");
        }

        private static bool IsValidId(int id)
        {
            return id > 0;
        }
    }

    public enum ConfigDuplicatePolicy
    {
        Throw,
        Replace,
        Ignore
    }
}
