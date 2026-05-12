using MxFramework.Modifiers;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Config-backed modifier that auto-creates effects from <see cref="IModifierConfig"/>.
    ///
    /// Default behavior (when no effects are provided):
    /// - Applies <see cref="ConfigAttributeEffect"/>: adds <c>Parameters[0]</c>
    ///   to the attribute identified by <c>ParamIndex</c>.
    /// - Uses empty conditions (always applies).
    ///
    /// Custom conditions or effects can override the defaults via constructor arguments.
    /// </summary>
    public sealed class ConfiguredModifier : ModifierBase
    {
        public ConfiguredModifier(
            IModifierConfig config,
            IModifierCondition[] conditions = null,
            IModifierEffect[] effects = null)
            : base(config.Id, conditions, effects ?? CreateDefaultEffects(config), config.ParamIndex)
        {
            Config = config;
        }

        public IModifierConfig Config { get; }

        private static IModifierEffect[] CreateDefaultEffects(IModifierConfig config)
        {
            if (config.Parameters != null && config.Parameters.Length > 0)
                return new IModifierEffect[] { new ConfigAttributeEffect(config) };

            return System.Array.Empty<IModifierEffect>();
        }
    }
}
