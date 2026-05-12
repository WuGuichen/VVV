using MxFramework.Buffs;

namespace MxFramework.Config.Runtime
{
    public sealed class ConfiguredBuff : BuffBase
    {
        public ConfiguredBuff(IBuffConfig config)
            : base(config.Id, config.Duration, config.MaxLayers, config.IsPermanent)
        {
            Config = config;
        }

        public IBuffConfig Config { get; }
    }
}
