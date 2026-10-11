using System;

namespace MxFramework.Config
{
    public readonly struct ConfigKey : IEquatable<ConfigKey>
    {
        public ConfigKey(Type configType, int id)
        {
            ConfigType = configType;
            Id = id;
        }

        public Type ConfigType { get; }
        public int Id { get; }

        public bool Equals(ConfigKey other)
        {
            return ConfigType == other.ConfigType && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is ConfigKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ConfigType != null ? ConfigType.GetHashCode() : 0) * 397) ^ Id;
            }
        }
    }

    public readonly struct ConfigKey<T> where T : IConfigData
    {
        public ConfigKey(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public ConfigKey Untyped => new ConfigKey(typeof(T), Id);
    }
}
