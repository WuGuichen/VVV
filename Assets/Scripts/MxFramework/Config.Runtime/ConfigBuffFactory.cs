using MxFramework.Buffs;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public sealed class ConfigBuffFactory<TConfig> : IBuffFactory where TConfig : class, IBuffConfig
    {
        private readonly IConfigProvider _configs;
        private readonly System.Func<TConfig, IBuff> _create;

        public ConfigBuffFactory(IConfigProvider configs, System.Func<TConfig, IBuff> create = null)
        {
            _configs = configs;
            _create = create;
        }

        public bool TryCreate(int buffId, out IBuff buff)
        {
            buff = null;
            if (_configs == null || !_configs.TryGetConfig(buffId, out TConfig config))
                return false;

            buff = _create != null ? _create(config) : new ConfiguredBuff(config);
            return buff != null;
        }
    }
}
