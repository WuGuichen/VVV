using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestFairyGuiCombatHudViewModelBuilder
    {
        private static void EnsureReusableBuffViewModels(
            List<CharacterTestFairyGuiCombatHudBuffViewModel> buffs,
            int count)
        {
            if (count <= 0)
            {
                buffs.Clear();
                return;
            }

            while (buffs.Count < count)
                buffs.Add(new CharacterTestFairyGuiCombatHudBuffViewModel());
            if (buffs.Count > count)
                buffs.RemoveRange(count, buffs.Count - count);
        }

        private static string ResolveBuffDisplayName(
            GameplayBuffPresentationItem item,
            IMxUiTextProvider textProvider)
        {
            return string.IsNullOrWhiteSpace(item.NameKey)
                ? item.DisplayName
                : L(textProvider, item.NameKey, item.DisplayName);
        }

        private static string ResolveBuffDescription(
            GameplayBuffPresentationItem item,
            GameplayBuffDefinitionRegistry buffDefinitions,
            IMxUiTextProvider textProvider,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            if (textProvider != null
                && string.Equals(item.DescriptionMode, "Auto", StringComparison.OrdinalIgnoreCase)
                && buffDefinitions != null
                && buffDefinitions.TryGet(item.BuffId, out GameplayBuffDefinition definition))
            {
                return BuildLocalizedAutoBuffDescription(definition, textProvider, item.Description, buffers);
            }

            string descriptionKey = ResolveBuffDescriptionKey(item.NameKey);
            return string.IsNullOrWhiteSpace(descriptionKey)
                ? item.Description
                : L(textProvider, descriptionKey, item.Description);
        }

        private static string ResolveBuffDescriptionKey(string nameKey)
        {
            if (string.IsNullOrWhiteSpace(nameKey))
                return string.Empty;

            const string nameSuffix = ".name";
            return nameKey.EndsWith(nameSuffix, StringComparison.Ordinal)
                ? nameKey.Substring(0, nameKey.Length - nameSuffix.Length) + ".description"
                : nameKey + ".description";
        }

        private static string BuildLocalizedAutoBuffDescription(
            GameplayBuffDefinition definition,
            IMxUiTextProvider textProvider,
            string fallback,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            List<string> parts = buffers?.AutoDescriptionParts ?? new List<string>(3);
            parts.Clear();
            string effects = FormatBuffEffects(definition, textProvider, buffers);
            if (!string.IsNullOrWhiteSpace(effects))
                parts.Add(effects);

            string duration = FormatBuffDuration(definition, textProvider);
            if (!string.IsNullOrWhiteSpace(duration))
                parts.Add(duration);

            string stack = FormatBuffStack(definition, textProvider);
            if (!string.IsNullOrWhiteSpace(stack))
                parts.Add(stack);

            if (parts.Count == 0)
                return fallback ?? string.Empty;

            string separator = ApplyTemplate(
                L(textProvider, "buff.auto_description.separator", IsEnglishLocale(textProvider) ? ", " : "，"),
                SpaceTemplateToken,
                " ");
            return string.Join(separator, parts);
        }

        private static string FormatBuffEffects(
            GameplayBuffDefinition definition,
            IMxUiTextProvider textProvider,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            int effectCount = definition.EffectCount;
            if (effectCount == 0)
                return L(textProvider, "buff.auto_description.effect.none", IsEnglishLocale(textProvider) ? "No effect" : "无效果");

            List<string> parts = buffers?.EffectDescriptionParts ?? new List<string>(effectCount);
            parts.Clear();
            bool english = IsEnglishLocale(textProvider);
            for (int i = 0; i < effectCount; i++)
            {
                GameplayBuffEffectDefinition effect = definition.GetEffectAt(i);
                switch (effect.Kind)
                {
                    case GameplayBuffEffectKind.AttributeModifier:
                    {
                        string template = L(
                            textProvider,
                            "buff.auto_description.effect.attribute_modifier",
                            english ? "{attribute} {value}" : "{attribute}{value}");
                        string value = (effect.AddValue >= 0 ? "+" : string.Empty)
                            + effect.AddValue.ToString(CultureInfo.InvariantCulture);
                        parts.Add(ApplyTemplate(
                            template,
                            AttributeTemplateToken,
                            FormatBuffAttribute(effect.AttributeId, textProvider),
                            ValueTemplateToken,
                            value));
                        break;
                    }
                    case GameplayBuffEffectKind.Status:
                    {
                        string template = L(textProvider, "buff.auto_description.effect.status", "{status}");
                        parts.Add(ApplyTemplate(
                            template,
                            StatusTemplateToken,
                            FormatBuffStatus(effect.StatusId, textProvider),
                            StatusIdTemplateToken,
                            effect.StatusId.Value.ToString(CultureInfo.InvariantCulture)));
                        break;
                    }
                    default:
                    {
                        string template = L(
                            textProvider,
                            "buff.auto_description.effect.unknown",
                            english ? "Effect {effectId}" : "效果 {effectId}");
                        parts.Add(ApplyTemplate(
                            template,
                            EffectIdTemplateToken,
                            effect.EffectId.ToString(CultureInfo.InvariantCulture),
                            KindTemplateToken,
                            effect.Kind.ToString()));
                        break;
                    }
                }
            }

            return string.Join(english ? "; " : "；", parts);
        }

        private static string FormatBuffDuration(
            GameplayBuffDefinition definition,
            IMxUiTextProvider textProvider)
        {
            if (definition.IsPermanent)
                return L(textProvider, "buff.auto_description.duration.permanent", IsEnglishLocale(textProvider) ? "permanent" : "永久");

            const int frameRate = 60;
            const int instantFrameRateBase = 30;
            int instantFrames = Math.Max(1, (int)Math.Ceiling(frameRate / (double)instantFrameRateBase));
            if (definition.DefaultDurationFrames <= instantFrames)
                return L(textProvider, "buff.auto_description.duration.instant", IsEnglishLocale(textProvider) ? "instant" : "瞬时");

            string template = L(
                textProvider,
                "buff.auto_description.duration.timed",
                IsEnglishLocale(textProvider) ? "lasts {seconds}s" : "持续{seconds}秒");
            return ApplyTemplate(
                template,
                FramesTemplateToken,
                definition.DefaultDurationFrames.ToString(CultureInfo.InvariantCulture),
                SecondsTemplateToken,
                FormatSeconds(definition.DefaultDurationFrames / (double)frameRate));
        }

        private static string FormatBuffStack(
            GameplayBuffDefinition definition,
            IMxUiTextProvider textProvider)
        {
            bool stacking = definition.StackPolicy == GameplayBuffStackPolicy.Stack
                && definition.MaxStackCount > 1;
            if (!stacking)
                return L(textProvider, "buff.auto_description.stack.single", IsEnglishLocale(textProvider) ? "not stackable" : "不可叠加");

            string template = L(
                textProvider,
                "buff.auto_description.stack.stacking",
                IsEnglishLocale(textProvider) ? "up to {maxStack} stacks" : "最多{maxStack}层");
            return ApplyTemplate(
                template,
                MaxStackTemplateToken,
                definition.MaxStackCount.ToString(CultureInfo.InvariantCulture));
        }

        private static string FormatBuffAttribute(int attributeId, IMxUiTextProvider textProvider)
        {
            return L(
                textProvider,
                "buff.attribute." + attributeId.ToString(CultureInfo.InvariantCulture),
                DefaultBuffAttributeName(attributeId, IsEnglishLocale(textProvider)));
        }

        private static string FormatBuffStatus(GameplayStatusId statusId, IMxUiTextProvider textProvider)
        {
            bool english = IsEnglishLocale(textProvider);
            return L(
                textProvider,
                "buff.status." + statusId.Value.ToString(CultureInfo.InvariantCulture),
                english
                    ? "Status " + statusId.Value.ToString(CultureInfo.InvariantCulture)
                    : "状态" + statusId.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static string DefaultBuffAttributeName(int attributeId, bool english)
        {
            switch (attributeId)
            {
                case 1:
                    return english ? "HP" : "生命";
                case 2:
                    return english ? "Attack" : "攻击";
                case 3:
                    return english ? "Defense" : "防御";
                case 4:
                    return english ? "ScatterPressure" : "韧性";
                default:
                    return english
                        ? "Attribute " + attributeId.ToString(CultureInfo.InvariantCulture)
                        : "属性" + attributeId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static bool IsEnglishLocale(IMxUiTextProvider textProvider)
        {
            return textProvider != null
                && textProvider.CurrentLocale.IsValid
                && textProvider.CurrentLocale.Value.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        private static string ApplyTemplate(string template, string token, string value)
        {
            string result = template ?? string.Empty;
            return result.Replace(token, value ?? string.Empty);
        }

        private static string ApplyTemplate(
            string template,
            string firstKey,
            string firstValue,
            string secondKey,
            string secondValue)
        {
            return ApplyTemplate(
                ApplyTemplate(template, firstKey, firstValue),
                secondKey,
                secondValue);
        }

        private static string FormatSeconds(double seconds)
        {
            double rounded = Math.Round(seconds * 1000d) / 1000d;
            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }

    }
}
