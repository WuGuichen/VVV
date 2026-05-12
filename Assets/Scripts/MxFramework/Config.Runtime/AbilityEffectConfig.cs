using System;

namespace MxFramework.Config.Runtime
{
    /// <summary>Named parameter block for config-defined ability effects.</summary>
    public readonly struct AbilityEffectParameters
    {
        public AbilityEffectParameters(
            int attackAttributeId = 0,
            int defenseAttributeId = 0,
            int hpAttributeId = 0,
            int buffId = 0)
        {
            AttackAttributeId = attackAttributeId;
            DefenseAttributeId = defenseAttributeId;
            HpAttributeId = hpAttributeId;
            BuffId = buffId;
        }

        public int AttackAttributeId { get; }
        public int DefenseAttributeId { get; }
        public int HpAttributeId { get; }
        public int BuffId { get; }

        public static AbilityEffectParameters DamageByAttackDefense(
            int attackAttributeId,
            int defenseAttributeId,
            int hpAttributeId)
        {
            return new AbilityEffectParameters(
                attackAttributeId: attackAttributeId,
                defenseAttributeId: defenseAttributeId,
                hpAttributeId: hpAttributeId);
        }

        public static AbilityEffectParameters ApplyBuff(int buffId)
        {
            return new AbilityEffectParameters(buffId: buffId);
        }

        public int[] ToLegacyParameters(AbilityEffectKind kind)
        {
            switch (kind)
            {
                case AbilityEffectKind.DamageByAttackDefense:
                    return new[] { AttackAttributeId, DefenseAttributeId, HpAttributeId };
                case AbilityEffectKind.ApplyBuff:
                    return new[] { BuffId };
                default:
                    return Array.Empty<int>();
            }
        }
    }

    /// <summary>One config-defined ability effect and its parameters.</summary>
    public readonly struct AbilityEffectConfig
    {
        public AbilityEffectConfig(AbilityEffectKind kind, int[] parameters)
        {
            Kind = kind;
            Parameters = parameters ?? Array.Empty<int>();
            NamedParameters = FromLegacyParameters(kind, Parameters);
        }

        public AbilityEffectConfig(AbilityEffectKind kind, AbilityEffectParameters parameters)
        {
            Kind = kind;
            NamedParameters = parameters;
            Parameters = parameters.ToLegacyParameters(kind);
        }

        public static AbilityEffectConfig DamageByAttackDefense(
            int attackAttributeId,
            int defenseAttributeId,
            int hpAttributeId)
        {
            return new AbilityEffectConfig(
                AbilityEffectKind.DamageByAttackDefense,
                AbilityEffectParameters.DamageByAttackDefense(
                    attackAttributeId,
                    defenseAttributeId,
                    hpAttributeId));
        }

        public static AbilityEffectConfig ApplyBuff(int buffId)
        {
            return new AbilityEffectConfig(
                AbilityEffectKind.ApplyBuff,
                AbilityEffectParameters.ApplyBuff(buffId));
        }

        public AbilityEffectKind Kind { get; }

        public AbilityEffectParameters NamedParameters { get; }

        /// <summary>
        /// Legacy positional parameters kept for compatibility with existing demo data and tests.
        /// New code should prefer <see cref="NamedParameters"/> or the static factory methods.
        ///
        /// Parameter mapping:
        /// DamageByAttackDefense: [0]=attackAttributeId, [1]=defenseAttributeId, [2]=hpAttributeId.
        /// ApplyBuff: [0]=buffId.
        /// </summary>
        public int[] Parameters { get; }

        private static AbilityEffectParameters FromLegacyParameters(
            AbilityEffectKind kind,
            int[] parameters)
        {
            int Get(int index)
            {
                return parameters != null && parameters.Length > index ? parameters[index] : 0;
            }

            switch (kind)
            {
                case AbilityEffectKind.DamageByAttackDefense:
                    return AbilityEffectParameters.DamageByAttackDefense(Get(0), Get(1), Get(2));
                case AbilityEffectKind.ApplyBuff:
                    return AbilityEffectParameters.ApplyBuff(Get(0));
                default:
                    return default;
            }
        }
    }
}
