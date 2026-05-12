using MxFramework.Modifiers;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Default effect for <see cref="ConfiguredModifier"/>.
    /// Interprets <see cref="IModifierConfig.ParamIndex"/> as the target attribute ID
    /// and <see cref="IModifierConfig.Parameters"/>[0] as a flat additive delta.
    ///
    /// This is the framework-level convention for "config says apply a flat value to an attribute".
    /// Game projects can override by providing custom <see cref="IModifierEffect"/> instances
    /// to the <see cref="ConfiguredModifier"/> constructor.
    /// </summary>
    public sealed class ConfigAttributeEffect : IModifierEffect
    {
        private readonly IModifierConfig _config;

        public ConfigAttributeEffect(IModifierConfig config)
        {
            _config = config;
        }

        public void Execute(ModifierContext context)
        {
            if (context.Target == null || _config.Parameters == null || _config.Parameters.Length == 0)
                return;

            context.Target.AddAttribute(_config.ParamIndex, _config.Parameters[0], _config);
        }
    }
}
