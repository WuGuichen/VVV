using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction
{
    internal static class CharacterActionDamageAuthoringText
    {
        internal const string DefaultDamageTypeId = "slash";
        internal const string DefaultDamageFormulaId = "ct.soft_defense_v1";
        internal const int BaselineLightAttackBaseDamage = 6;
        internal const float BaselineLightAttackAttackScale = 0.65f;
        internal const int BaselineDefaultAttack = 20;
        internal const int BaselineLightAttackOffense = 19;
        internal const int BaselineLightAttackImpactPower = 34;
        internal const int BaselineBaseCohesion = 100;
        internal const int BaselineLightAttackBreakHitCount = 3;

        internal const string DamageTuningSemanticsHelpText =
            "基础伤害和攻击力收益共同影响 HP；冲击强度影响散势/击退。伤害性质仅用于日志、分析、筛选和后续扩展，不参与 CharacterTest 当前伤害公式。公式由 CharacterTest 运行时统一解析。";

        internal static string DamageTuningBaselineHelpText =>
            "调参基准：同级普通目标 BaseCohesion "
            + BaselineBaseCohesion.ToString(CultureInfo.InvariantCulture)
            + "；轻击身体命中基础 HP "
            + BaselineLightAttackBaseDamage.ToString(CultureInfo.InvariantCulture)
            + " + Attack "
            + BaselineDefaultAttack.ToString(CultureInfo.InvariantCulture)
            + " x"
            + BaselineLightAttackAttackScale.ToString("0.###", CultureInfo.InvariantCulture)
            + " = 攻击侧 "
            + BaselineLightAttackOffense.ToString(CultureInfo.InvariantCulture)
            + " / 冲击 "
            + BaselineLightAttackImpactPower.ToString(CultureInfo.InvariantCulture)
            + "，连续第 "
            + BaselineLightAttackBreakHitCount.ToString(CultureInfo.InvariantCulture)
            + " 次轻击破聚。";

        internal static string BaseDamageFieldLabel =>
            "基础伤害 (轻击基准 " + BaselineLightAttackBaseDamage.ToString(CultureInfo.InvariantCulture) + ")";

        internal static string AttackScaleFieldLabel =>
            "攻击力收益 (轻击基准 x"
            + BaselineLightAttackAttackScale.ToString("0.###", CultureInfo.InvariantCulture)
            + ")";

        internal static string ImpactPowerFieldLabel =>
            "冲击强度 (轻击基准 " + BaselineLightAttackImpactPower.ToString(CultureInfo.InvariantCulture) + ")";

        internal static string LaunchPowerFieldLabel =>
            "击飞效果值 (0=无，标准人形 100)";

        internal static List<(string Value, string Label)> BuildDamageTypeChoices(string currentValue)
        {
            var choices = new List<(string Value, string Label)>
            {
                ("slash", FormatKnownChoice("slash")),
                ("blunt", FormatKnownChoice("blunt")),
                ("force", FormatKnownChoice("force")),
                ("fire", FormatKnownChoice("fire")),
                ("light", FormatKnownChoice("light")),
                ("toxic", FormatKnownChoice("toxic")),
                ("wind", FormatKnownChoice("wind")),
                ("control", FormatKnownChoice("control")),
            };
            AddPreservedChoice(choices, currentValue);
            return choices;
        }

        internal static List<(string Value, string Label)> BuildAnalysisCategoryChoices(string currentValue)
        {
            var choices = new List<(string Value, string Label)>
            {
                ("BasicAttack", "普通攻击"),
                ("AreaPulse", "范围冲击"),
                ("HeavyAttack", "重攻击"),
                ("Scripted", "脚本伤害"),
            };
            AddPreservedChoice(choices, currentValue);
            return choices;
        }

        internal static string FormatDamageTypeLabel(string damageTypeId)
        {
            damageTypeId = damageTypeId ?? string.Empty;
            switch (damageTypeId)
            {
                case "slash": return "斩击";
                case "blunt": return "钝击";
                case "force": return "冲击";
                case "fire": return "火焰";
                case "light": return "雷光";
                case "toxic": return "毒性";
                case "wind": return "风压";
                case "control": return "控制";
                default:
                    return string.IsNullOrWhiteSpace(damageTypeId)
                        ? "未设置"
                        : "保留旧值：" + damageTypeId;
            }
        }

        internal static string FormatDamageSummary(
            string damageTypeId,
            int baseDamage,
            int impactPower,
            float attackScale = 0f,
            int launchPower = 0)
        {
            return FormatDamageTypeLabel(damageTypeId)
                + " / HP " + Math.Max(0, baseDamage).ToString(CultureInfo.InvariantCulture)
                + FormatAttackScaleSummary(attackScale)
                + " / 冲击 " + Math.Max(0, impactPower).ToString(CultureInfo.InvariantCulture)
                + FormatLaunchPowerSummary(launchPower);
        }

        internal static string FormatDamageSummary(CharacterActionHitDamageJsonDto damage)
        {
            if (!HasDamageData(damage))
                return "鏈缃?";

            return FormatDamageSummary(
                CharacterActionAuthoringIds.Label(damage.DamageTypeId, 409, damage.DamageTypeLabel),
                damage.BaseDamage,
                damage.ImpactPower,
                CharacterActionHitEventAuthoringModel.ResolveAttackScale(damage.SourceAttributeScales),
                damage.LaunchPower);
        }

        internal static bool HasDamageData(CharacterActionHitDamageJsonDto damage)
        {
            if (damage == null)
                return false;

            return damage.ShouldSerializeDamageTypeId()
                || damage.ShouldSerializeFormulaId()
                || damage.ShouldSerializeBaseDamage()
                || damage.ShouldSerializeSourceAttributeScales()
                || damage.ShouldSerializeImpactPower()
                || damage.ShouldSerializeLaunchPower()
                || damage.ShouldSerializeTags()
                || damage.ShouldSerializeAnalysisCategory();
        }

        internal static string FormatAttackScaleSummary(float attackScale)
        {
            return attackScale > 0.0001f
                ? " + 攻击 x" + attackScale.ToString("0.###", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        internal static string FormatLaunchPowerSummary(int launchPower)
        {
            return launchPower > 0
                ? " / 击飞 " + launchPower.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        internal static string FormatDamageTagLabel(string tag)
        {
            switch (tag)
            {
                case "melee": return "近战";
                case "weapon": return "武器";
                case "light": return "轻击";
                case "heavy": return "重击";
                case "area": return "范围";
                case "shockwave": return "冲击波";
                case "scatter": return "破聚/散势";
                case "projectile": return "projectile";
                case "control": return "控制";
                default: return tag;
            }
        }

        private static string FormatKnownChoice(string damageTypeId)
        {
            return FormatDamageTypeLabel(damageTypeId) + " (" + damageTypeId + ")";
        }

        private static void AddPreservedChoice(List<(string Value, string Label)> choices, string value)
        {
            value = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return;
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Value, value, StringComparison.Ordinal))
                    return;
            }

            choices.Add((value, "保留旧值：" + value));
        }
    }
}
