using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Gameplay;

namespace MxFramework.Config.Runtime
{
    /// <summary>Creates Gameplay abilities from BasicAbilityConfig rows.</summary>
    public sealed class ConfigAbilityFactory
    {
        private readonly IConfigProvider _configs;
        private readonly IBuffFactory _buffFactory;

        public ConfigAbilityFactory(IConfigProvider configs, IBuffFactory buffFactory = null)
        {
            _configs = configs;
            _buffFactory = buffFactory;
        }

        public bool TryCreate(int abilityId, out IAbility ability, out string error)
        {
            ability = null;
            error = string.Empty;

            if (_configs == null)
            {
                error = "Config provider is null.";
                return false;
            }

            if (!_configs.TryGetConfig(abilityId, out BasicAbilityConfig config))
            {
                error = "Ability config not found: " + abilityId + ".";
                return false;
            }

            if (!TryCreateTargetSelector(config.TargetSelectorKind, out ITargetSelector selector, out error))
                return false;

            if (!TryCreateEffects(config, out IAbilityEffect[] effects, out error))
                return false;

            ability = new SimpleAbility(config.Id, selector, effects);
            return true;
        }

        private static bool TryCreateTargetSelector(
            AbilityTargetSelectorKind kind,
            out ITargetSelector selector,
            out string error)
        {
            selector = null;
            error = string.Empty;

            switch (kind)
            {
                case AbilityTargetSelectorKind.Self:
                    selector = new SelfTargetSelector();
                    return true;
                case AbilityTargetSelectorKind.SingleEnemy:
                    selector = new SingleEnemyTargetSelector();
                    return true;
                default:
                    error = "Unsupported ability target selector kind: " + kind + ".";
                    return false;
            }
        }

        private bool TryCreateEffects(
            BasicAbilityConfig config,
            out IAbilityEffect[] effects,
            out string error)
        {
            effects = null;
            error = string.Empty;

            if (config.Effects == null || config.Effects.Length == 0)
            {
                error = "Ability config has no effects: " + config.Id + ".";
                return false;
            }

            effects = new IAbilityEffect[config.Effects.Length];
            for (int i = 0; i < config.Effects.Length; i++)
            {
                if (!TryCreateEffect(config.Id, i, config.Effects[i], out effects[i], out error))
                    return false;
            }

            return true;
        }

        private bool TryCreateEffect(
            int abilityId,
            int effectIndex,
            AbilityEffectConfig config,
            out IAbilityEffect effect,
            out string error)
        {
            effect = null;
            error = string.Empty;

            switch (config.Kind)
            {
                case AbilityEffectKind.DamageByAttackDefense:
                    if (!RequireParameterCount(abilityId, effectIndex, config, 3, out error))
                        return false;

                    effect = new DamageEffect(
                        config.NamedParameters.AttackAttributeId,
                        config.NamedParameters.DefenseAttributeId,
                        config.NamedParameters.HpAttributeId);
                    return true;

                case AbilityEffectKind.ApplyBuff:
                    if (!RequireParameterCount(abilityId, effectIndex, config, 1, out error))
                        return false;
                    if (_buffFactory == null)
                    {
                        error = "ApplyBuff effect requires an IBuffFactory. Ability=" + abilityId + ", EffectIndex=" + effectIndex + ".";
                        return false;
                    }

                    int buffId = config.NamedParameters.BuffId;
                    if (!_buffFactory.TryCreate(buffId, out IBuff probe) || probe == null)
                    {
                        error = "BuffFactory failed to create buff: " + buffId + ". Ability=" + abilityId + ", EffectIndex=" + effectIndex + ".";
                        return false;
                    }

                    effect = new ApplyBuffEffect(() =>
                    {
                        _buffFactory.TryCreate(buffId, out IBuff buff);
                        return buff;
                    });
                    return true;

                default:
                    error = "Unsupported ability effect kind: " + config.Kind + ". Ability=" + abilityId + ", EffectIndex=" + effectIndex + ".";
                    return false;
            }
        }

        private static bool RequireParameterCount(
            int abilityId,
            int effectIndex,
            AbilityEffectConfig config,
            int requiredCount,
            out string error)
        {
            int actualCount = config.Parameters == null ? 0 : config.Parameters.Length;
            if (actualCount >= requiredCount)
            {
                error = string.Empty;
                return true;
            }

            error = "Ability effect parameters are insufficient. Ability=" + abilityId
                + ", EffectIndex=" + effectIndex
                + ", Kind=" + config.Kind
                + ", Required=" + requiredCount
                + ", Actual=" + actualCount + ".";
            return false;
        }
    }
}
