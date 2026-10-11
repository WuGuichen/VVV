using System;

namespace MxFramework.Config
{
    public class ConfigException : Exception
    {
        public ConfigException(string message)
            : base(message)
        {
        }
    }

    public sealed class ConfigNotFoundException : ConfigException
    {
        public ConfigNotFoundException(Type configType, int id)
            : base($"Config not found. Type={configType.FullName}, Id={id}.")
        {
            ConfigType = configType;
            Id = id;
        }

        public Type ConfigType { get; }
        public int Id { get; }
    }

    public sealed class DuplicateConfigException : ConfigException
    {
        public DuplicateConfigException(Type configType, int id)
            : base($"Duplicate config id. Type={configType.FullName}, Id={id}.")
        {
            ConfigType = configType;
            Id = id;
        }

        public Type ConfigType { get; }
        public int Id { get; }
    }
}
