using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    /// <summary>Minimal config row that can create a runtime Gameplay ability.</summary>
    public sealed class BasicAbilityConfig : IConfigData, IConfigReferenceProvider
    {
        public BasicAbilityConfig(
            int id,
            LocalizedTextKey nameText,
            LocalizedTextKey descriptionText,
            AbilityTargetSelectorKind targetSelectorKind,
            AbilityEffectConfig[] effects)
        {
            Id = id;
            NameText = nameText;
            DescriptionText = descriptionText;
            TargetSelectorKind = targetSelectorKind;
            Effects = effects ?? Array.Empty<AbilityEffectConfig>();
        }

        public int Id { get; }
        public LocalizedTextKey NameText { get; }
        public LocalizedTextKey DescriptionText { get; }
        public AbilityTargetSelectorKind TargetSelectorKind { get; }
        public AbilityEffectConfig[] Effects { get; }

        public static ConfigSchema<BasicAbilityConfig> CreateSchema()
        {
            var schema = new ConfigSchema<BasicAbilityConfig>(
                "BasicAbilityConfig",
                displayName: "Basic Ability Config",
                description: "Config-backed generic gameplay ability.",
                idRange: new ConfigIdRange(300000, 399999));

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
                .AddField(new ConfigField("NameText", ConfigFieldType.LocalizedText, displayName: "名称文本", required: true))
                .AddField(new ConfigField("DescriptionText", ConfigFieldType.LocalizedText, displayName: "描述文本"))
                .AddField(new ConfigField(
                    "TargetSelectorKind",
                    ConfigFieldType.Enum,
                    displayName: "目标选择",
                    description: "Supported values: Self, SingleEnemy.",
                    required: true,
                    enumId: "ability.TargetSelectorKind"))
                .AddField(new ConfigField(
                    "Effects",
                    ConfigFieldType.Custom,
                    displayName: "效果列表",
                    description: "AbilityEffectConfig[]. Prefer named parameters: DamageByAttackDefense uses attackAttributeId/defenseAttributeId/hpAttributeId; ApplyBuff uses buffId. Legacy positional Parameters remain supported.",
                    required: true))
                .RequireLocale(LocaleId.ZhCN)
                .RequireLocale(LocaleId.EnUS);

            return schema;
        }

        public void CollectReferences(ICollection<ConfigReference> references)
        {
            if (references == null || Effects == null)
                return;

            for (int i = 0; i < Effects.Length; i++)
            {
                AbilityEffectConfig effect = Effects[i];
                if (effect.Kind != AbilityEffectKind.ApplyBuff || effect.NamedParameters.BuffId == 0)
                    continue;

                references.Add(new ConfigReference(
                    typeof(BasicAbilityConfig),
                    Id,
                    typeof(BasicBuffConfig),
                    effect.NamedParameters.BuffId,
                    "Effects[" + i + "].NamedParameters.BuffId"));
            }
        }
    }
}
