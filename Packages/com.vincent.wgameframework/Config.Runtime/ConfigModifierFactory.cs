using MxFramework.Config;
using MxFramework.Modifiers;

namespace MxFramework.Config.Runtime
{
    public sealed class ConfigModifierFactory<TConfig> : IModifierFactory where TConfig : class, IModifierConfig
    {
        private readonly IConfigProvider _configs;
        private readonly System.Func<TConfig, IModifier> _create;

        public ConfigModifierFactory(IConfigProvider configs, System.Func<TConfig, IModifier> create = null)
        {
            _configs = configs;
            _create = create;
        }

        public bool TryCreate(int modifierId, out IModifier modifier)
        {
            modifier = null;
            if (_configs == null || !_configs.TryGetConfig(modifierId, out TConfig config))
                return false;

            modifier = _create != null ? _create(config) : new ConfiguredModifier(config);
            return modifier != null;
        }
    }
}
