using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public delegate bool BuffAutoDescriptionTextResolver(string key, string localeId, out string text);

    public static class BuffAutoDescriptionFormatter
    {
        public const string EffectAttributeModifierKey = "buff.auto_description.effect.attribute_modifier";
        public const string EffectStatusKey = "buff.auto_description.effect.status";
        public const string EffectUnknownKey = "buff.auto_description.effect.unknown";
        public const string NoEffectKey = "buff.auto_description.effect.none";
        public const string DurationInstantKey = "buff.auto_description.duration.instant";
        public const string DurationTimedKey = "buff.auto_description.duration.timed";
        public const string DurationPermanentKey = "buff.auto_description.duration.permanent";
        public const string StackSingleKey = "buff.auto_description.stack.single";
        public const string StackStackingKey = "buff.auto_description.stack.stacking";
        public const string SeparatorKey = "buff.auto_description.separator";
        public const string AttributeKeyPrefix = "buff.attribute.";
        public const string StatusKeyPrefix = "buff.status.";

        public static string Format(
            BuffDefinitionConfig buff,
            string localeId = "zh-CN",
            int frameRate = BuffAuthoringCompiler.DefaultDescriptionFrameRate,
            BuffAutoDescriptionTextResolver textResolver = null)
        {
            if (buff == null)
                return string.Empty;

            string locale = NormalizeLocale(localeId);
            bool english = IsEnglish(locale);
            string effectText = FormatEffects(buff.Effects, locale, english, textResolver);
            string durationText = FormatDuration(buff.DefaultDurationFrames, buff.IsPermanent, locale, english, frameRate, textResolver);
            string stackText = FormatStack(buff.StackPolicy, buff.MaxStackCount, locale, english, textResolver);
            string separator = ApplyTemplate(
                ResolveText(textResolver, SeparatorKey, locale, english ? ", " : "，"),
                new Dictionary<string, string> { ["space"] = " " });

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(effectText))
                parts.Add(effectText);
            if (!string.IsNullOrWhiteSpace(durationText))
                parts.Add(durationText);
            if (!string.IsNullOrWhiteSpace(stackText))
                parts.Add(stackText);
            return string.Join(separator, parts);
        }

        public static string NormalizeLocale(string localeId)
        {
            return string.IsNullOrWhiteSpace(localeId) ? "zh-CN" : localeId.Trim();
        }

        public static bool IsEnglish(string localeId)
        {
            return NormalizeLocale(localeId).StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveText(
            BuffAutoDescriptionTextResolver textResolver,
            string key,
            string localeId,
            string fallback)
        {
            if (textResolver != null && textResolver(key, NormalizeLocale(localeId), out string text) && !string.IsNullOrEmpty(text))
                return text;
            return fallback ?? string.Empty;
        }

        public static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> values)
        {
            string result = template ?? string.Empty;
            if (values == null)
                return result;

            foreach (KeyValuePair<string, string> pair in values)
                result = result.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
            return result;
        }

        private static string FormatEffects(
            List<BuffEffectConfig> effects,
            string localeId,
            bool english,
            BuffAutoDescriptionTextResolver textResolver)
        {
            if (effects == null || effects.Count == 0)
                return ResolveText(textResolver, NoEffectKey, localeId, english ? "No effect" : "无效果");

            var sorted = new List<BuffEffectConfig>();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null)
                    sorted.Add(effects[i]);
            }
            sorted.Sort(CompareEffects);

            string separator = english ? "; " : "；";
            var parts = new List<string>();
            for (int i = 0; i < sorted.Count; i++)
            {
                BuffEffectConfig effect = sorted[i];
                if (string.Equals(effect.Kind, "AttributeModifier", StringComparison.Ordinal))
                {
                    string template = ResolveText(
                        textResolver,
                        EffectAttributeModifierKey,
                        localeId,
                        english ? "{attribute} {value}" : "{attribute}{value}");
                    string value = (effect.AddValue >= 0 ? "+" : string.Empty)
                        + effect.AddValue.ToString(CultureInfo.InvariantCulture);
                    parts.Add(ApplyTemplate(template, new Dictionary<string, string>
                    {
                        ["attribute"] = FormatAttribute(effect.AttributeId, localeId, english, textResolver),
                        ["value"] = value
                    }));
                }
                else if (string.Equals(effect.Kind, "Status", StringComparison.Ordinal))
                {
                    string template = ResolveText(textResolver, EffectStatusKey, localeId, "{status}");
                    parts.Add(ApplyTemplate(template, new Dictionary<string, string>
                    {
                        ["status"] = FormatStatus(effect.StatusId, localeId, english, textResolver),
                        ["statusId"] = effect.StatusId.ToString(CultureInfo.InvariantCulture)
                    }));
                }
                else
                {
                    string template = ResolveText(textResolver, EffectUnknownKey, localeId, english ? "Effect {effectId}" : "效果 {effectId}");
                    parts.Add(ApplyTemplate(template, new Dictionary<string, string>
                    {
                        ["effectId"] = effect.EffectId.ToString(CultureInfo.InvariantCulture),
                        ["kind"] = effect.Kind ?? string.Empty
                    }));
                }
            }

            return string.Join(separator, parts);
        }

        private static string FormatDuration(
            long frames,
            bool isPermanent,
            string localeId,
            bool english,
            int frameRate,
            BuffAutoDescriptionTextResolver textResolver)
        {
            if (isPermanent)
                return ResolveText(textResolver, DurationPermanentKey, localeId, english ? "permanent" : "永久");

            int effectiveFrameRate = frameRate > 0 ? frameRate : BuffAuthoringCompiler.DefaultDescriptionFrameRate;
            int instantFrames = Math.Max(1, (int)Math.Ceiling(effectiveFrameRate / (double)BuffAuthoringCompiler.DefaultInstantFrameRateBase));
            if (frames <= instantFrames)
                return ResolveText(textResolver, DurationInstantKey, localeId, english ? "instant" : "瞬时");

            string template = ResolveText(textResolver, DurationTimedKey, localeId, english ? "lasts {seconds}s" : "持续{seconds}秒");
            return ApplyTemplate(template, new Dictionary<string, string>
            {
                ["frames"] = frames.ToString(CultureInfo.InvariantCulture),
                ["seconds"] = FormatSeconds(frames / (double)effectiveFrameRate)
            });
        }

        private static string FormatStack(
            string stackPolicy,
            int maxStackCount,
            string localeId,
            bool english,
            BuffAutoDescriptionTextResolver textResolver)
        {
            bool stacking = string.Equals(stackPolicy, "Stack", StringComparison.Ordinal) && maxStackCount > 1;
            if (stacking)
            {
                string template = ResolveText(textResolver, StackStackingKey, localeId, english ? "up to {maxStack} stacks" : "最多{maxStack}层");
                return ApplyTemplate(template, new Dictionary<string, string>
                {
                    ["maxStack"] = maxStackCount.ToString(CultureInfo.InvariantCulture)
                });
            }

            return ResolveText(textResolver, StackSingleKey, localeId, english ? "not stackable" : "不可叠加");
        }

        private static string FormatAttribute(
            int attributeId,
            string localeId,
            bool english,
            BuffAutoDescriptionTextResolver textResolver)
        {
            return ResolveText(
                textResolver,
                AttributeKeyPrefix + attributeId.ToString(CultureInfo.InvariantCulture),
                localeId,
                DefaultAttributeName(attributeId, english));
        }

        private static string FormatStatus(
            int statusId,
            string localeId,
            bool english,
            BuffAutoDescriptionTextResolver textResolver)
        {
            string fallback = english
                ? "Status " + statusId.ToString(CultureInfo.InvariantCulture)
                : "状态 " + statusId.ToString(CultureInfo.InvariantCulture);
            return ResolveText(
                textResolver,
                StatusKeyPrefix + statusId.ToString(CultureInfo.InvariantCulture),
                localeId,
                fallback);
        }

        private static string DefaultAttributeName(int attributeId, bool english)
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
                    return english ? "Posture" : "韧性";
                default:
                    return english
                        ? "Attribute " + attributeId.ToString(CultureInfo.InvariantCulture)
                        : "属性" + attributeId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string FormatSeconds(double seconds)
        {
            double rounded = Math.Round(seconds * 1000d) / 1000d;
            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int CompareEffects(BuffEffectConfig left, BuffEffectConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.EffectId.CompareTo(right.EffectId);
        }
    }
}
