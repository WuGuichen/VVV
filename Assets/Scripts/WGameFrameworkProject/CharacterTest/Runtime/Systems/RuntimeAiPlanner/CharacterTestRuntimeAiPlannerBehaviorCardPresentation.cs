using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardView
    {
        public string Title { get; set; } = string.Empty;
        public string CategoryText { get; set; } = string.Empty;
        public string IntentText { get; set; } = string.Empty;
        public string TriggerText { get; set; } = string.Empty;
        public string OutputText { get; set; } = string.Empty;
        public string CapabilityText { get; set; } = string.Empty;
        public string ParameterText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
        public string ForecastText { get; set; } = string.Empty;
        public string DiagnosticsText { get; set; } = string.Empty;
        public bool Disabled { get; set; }
        public bool HasIssue { get; set; }
        public bool CountsAsExecutable { get; set; }

        public string TemplateLabel => "行为卡 | " + Title + " [" + CategoryText + "]";
        public string InstanceLabel => "实例卡 | " + Title + " [" + StatusText + "]";
        public string TemplateDetail => "触发: "
            + TriggerText
            + "\n输出: "
            + OutputText
            + " | 参数: "
            + ParameterText
            + " | 风险: "
            + RiskText;
        public string InstanceDetail => "触发: "
            + TriggerText
            + "\n输出: "
            + OutputText
            + " | 预估: "
            + ForecastText
            + " | 状态: "
            + StatusText;
    }

    internal static class CharacterTestRuntimeAiPlannerBehaviorCardPresentation
    {
        public static CharacterTestRuntimeAiPlannerBehaviorCardView CreateTemplateCard(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            return CreateCard(null, definition, default, string.Empty, isTemplate: true);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardView CreateInstanceCard(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            string forecastText)
        {
            return CreateCard(entry, definition, summary, forecastText, isTemplate: false);
        }

        public static string FormatBehaviorName(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null)
                return "未知行为";
            return string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.BehaviorId
                : definition.DisplayName;
        }

        public static string FormatCategoryLabel(
            CharacterTestRuntimeAiPlannerBehaviorModuleCategory category)
        {
            switch (category)
            {
                case CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Targeting:
                    return "目标";
                case CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Movement:
                    return "移动";
                case CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Survival:
                    return "生存";
                case CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Combat:
                    return "战斗";
                default:
                    return "保底";
            }
        }

        public static string FormatActionPrimitive(
            CharacterTestRuntimeAiPlannerBehaviorActionPrimitive primitive)
        {
            switch (primitive)
            {
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.MoveToTarget:
                    return "向目标移动";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Strafe:
                    return "侧移绕位";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Retreat:
                    return "后撤";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.PrimaryAttack:
                    return "普通攻击";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.CastAbilitySlot:
                    return "释放技能";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Dodge:
                    return "闪避";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Wait:
                    return "等待/保持";
                default:
                    return "更新目标事实";
            }
        }

        public static string FormatIntent(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null)
                return "未知行为。";
            return string.IsNullOrWhiteSpace(definition.Intent)
                ? "未提供策略说明。"
                : definition.Intent;
        }

        public static string FormatConditions(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (definition == null || definition.Conditions.Count == 0)
                return "始终";

            int displayCount = Math.Min(definition.Conditions.Count, 3);
            var parts = new string[displayCount];
            for (int i = 0; i < displayCount; i++)
                parts[i] = FormatCondition(definition.Conditions[i], definition, entry);

            string text = string.Join("，", parts);
            if (definition.Conditions.Count > displayCount)
            {
                text += "，等 "
                    + definition.Conditions.Count.ToString(CultureInfo.InvariantCulture)
                    + " 项";
            }

            return text;
        }

        public static string FormatRequiredFacts(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null || definition.RequiredFacts.Count == 0)
                return "无";

            var facts = new string[definition.RequiredFacts.Count];
            for (int i = 0; i < definition.RequiredFacts.Count; i++)
                facts[i] = FormatFactLabel(definition.RequiredFacts[i]);
            return string.Join("，", facts);
        }

        public static string FormatCapabilities(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null
                || definition.PrimaryCapability == CharacterTestRuntimeAiPlannerBehaviorCapability.None)
            {
                return "none";
            }

            var parts = new List<string>(4)
            {
                CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatCapabilityLabel(
                    definition.PrimaryCapability)
            };
            for (int i = 0; i < definition.SecondaryCapabilities.Count; i++)
            {
                string label = CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatCapabilityLabel(
                    definition.SecondaryCapabilities[i]);
                if (!parts.Contains(label))
                    parts.Add(label);
            }

            string text = "provides " + string.Join(", ", parts);
            if (definition.RequiredCapabilities.Count > 0)
                text += " | requires " + FormatCapabilityList(definition.RequiredCapabilities);
            if (definition.CounterCapabilities.Count > 0)
                text += " | counters " + FormatCapabilityList(definition.CounterCapabilities);
            return text;
        }

        public static string FormatParameterName(
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            switch (parameter.Key)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey:
                    return "优先级";
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey:
                    return "代价";
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MinDistanceKey:
                    return "最小距离";
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MaxDistanceKey:
                    return "最大距离";
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.AbilitySlotKey:
                    return "技能槽";
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.SpeedScaleKey:
                    return "移动速度";
                case CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.DirectionHoldFramesKey:
                    return "方向保持";
                default:
                    return string.IsNullOrWhiteSpace(parameter.DisplayName)
                        ? parameter.Key
                        : parameter.DisplayName;
            }
        }

        public static string FormatParameterValue(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            if (definition == null)
                return string.Empty;

            switch (parameter.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                    return ResolveBool(entry, definition, parameter) ? "开启" : "关闭";
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                    int intValue = ResolveInt(entry, definition, parameter);
                    if (string.Equals(
                        parameter.Key,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.AbilitySlotKey,
                        StringComparison.Ordinal))
                    {
                        return "技能槽 " + intValue.ToString(CultureInfo.InvariantCulture);
                    }

                    if (string.Equals(
                        parameter.Key,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.DirectionHoldFramesKey,
                        StringComparison.Ordinal))
                    {
                        return intValue.ToString(CultureInfo.InvariantCulture) + " 帧";
                    }

                    return intValue.ToString(CultureInfo.InvariantCulture);
                default:
                    float floatValue = ResolveFloat(entry, definition, parameter);
                    if (string.Equals(
                        parameter.Key,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey,
                        StringComparison.Ordinal))
                    {
                        return FormatScoreBand(floatValue);
                    }

                    if (string.Equals(
                        parameter.Key,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey,
                        StringComparison.Ordinal))
                    {
                        return FormatCostBand(floatValue);
                    }

                    if (string.Equals(
                        parameter.Key,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.SpeedScaleKey,
                        StringComparison.Ordinal))
                    {
                        return (floatValue * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                    }

                    if (string.Equals(
                        parameter.Key,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MinDistanceKey,
                        StringComparison.Ordinal)
                        || string.Equals(
                            parameter.Key,
                            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MaxDistanceKey,
                            StringComparison.Ordinal))
                    {
                        return floatValue.ToString("0.##", CultureInfo.InvariantCulture) + "m";
                    }

                    return floatValue.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardView CreateCard(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            string forecastText,
            bool isTemplate)
        {
            string diagnostics = summary.DiagnosticsText ?? string.Empty;
            string title = definition != null
                ? FormatBehaviorName(definition)
                : !string.IsNullOrWhiteSpace(summary.DisplayName)
                    ? TrimEntrySuffix(summary.DisplayName)
                    : entry != null
                        ? entry.BehaviorId
                        : "未知行为";
            bool disabled = entry != null && entry.Disabled;
            bool noActionIssue = !isTemplate
                && definition != null
                && definition.ActionPrimitive != CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None
                && summary.GeneratedActionCount <= 0
                && !disabled;
            bool hasIssue = disabled
                || definition == null
                || !string.IsNullOrWhiteSpace(diagnostics)
                || noActionIssue;

            return new CharacterTestRuntimeAiPlannerBehaviorCardView
            {
                Title = title,
                CategoryText = definition != null ? FormatCategoryLabel(definition.Category) : "未知",
                IntentText = FormatIntent(definition),
                TriggerText = ResolveTriggerText(entry, definition, summary, disabled),
                OutputText = ResolveOutputText(definition, summary, isTemplate, disabled),
                CapabilityText = FormatCapabilities(definition),
                ParameterText = FormatParameterSummary(entry, definition),
                StatusText = ResolveStatusText(entry, definition, summary, isTemplate),
                RiskText = ResolveRiskText(entry, definition, summary, isTemplate),
                ForecastText = string.IsNullOrWhiteSpace(forecastText) ? "未在典型情境命中" : forecastText,
                DiagnosticsText = diagnostics,
                Disabled = disabled,
                HasIssue = hasIssue,
                CountsAsExecutable = !disabled && definition != null && string.IsNullOrWhiteSpace(diagnostics)
            };
        }

        private static string ResolveTriggerText(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            bool disabled)
        {
            if (disabled)
                return "组内临时禁用";
            if (definition == null)
                return "行为定义缺失";
            if (definition.Conditions.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(summary.RequiredFactsText)
                    && !string.Equals(summary.RequiredFactsText, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return "依赖感知: " + summary.RequiredFactsText;
                }

                return "始终可尝试";
            }

            return FormatConditions(definition, entry);
        }

        private static string ResolveOutputText(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            bool isTemplate,
            bool disabled)
        {
            if (disabled)
                return "不输出";
            if (!isTemplate
                && !string.IsNullOrWhiteSpace(summary.GeneratedActionsText)
                && !string.Equals(summary.GeneratedActionsText, "no action", StringComparison.Ordinal)
                && !string.Equals(summary.GeneratedActionsText, "disabled", StringComparison.Ordinal))
            {
                return FormatGeneratedActions(summary.GeneratedActionsText);
            }

            if (definition == null)
                return "无法生成";
            if (definition.ActionPrimitive == CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
            {
                return definition.Category == CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Targeting
                    ? "选择目标 / 更新感知事实"
                    : "不直接输出动作";
            }

            return FormatActionPrimitive(definition.ActionPrimitive);
        }

        private static string ResolveStatusText(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            bool isTemplate)
        {
            if (isTemplate)
                return "模板卡";
            if (entry != null && entry.Disabled)
                return "已禁用";
            if (definition == null)
                return "定义缺失";
            if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                return "有诊断";
            if (definition.ActionPrimitive == CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
                return "准备事实";
            if (summary.GeneratedActionCount <= 0)
                return "未生成动作";
            return "可执行";
        }

        private static string ResolveRiskText(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            bool isTemplate)
        {
            if (!isTemplate && entry != null && entry.Disabled)
                return "临时禁用，不进入可执行候选";
            if (definition == null)
                return "缺少行为定义，无法编译";
            if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                return summary.DiagnosticsText;
            if (!isTemplate
                && definition.ActionPrimitive != CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None
                && summary.GeneratedActionCount <= 0)
            {
                return "没有生成 Runtime AI Planner action";
            }

            if (definition.RiskTags.Count > 0)
                return FormatRiskTags(definition.RiskTags);

            if (HasCondition(definition, CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard, true))
                return "危险事实成立时会抢占战斗/移动";
            if (HasCondition(definition, CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, false))
                return "动作硬直时不会触发";
            if (HasCondition(definition, CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable, true))
                return "路径不可用时不会触发";
            if (HasTargetFact(definition) && definition.ActionPrimitive != CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
                return "需要目标选择卡先提供目标事实";
            if (definition.ActionPrimitive == CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
                return "只准备事实，不直接产生动作";
            if (definition.Conditions.Count == 0)
                return "保底卡，可能在无更高优先输出时触发";
            return "可能被更高优先级状态或更高分卡压住";
        }

        private static string FormatParameterSummary(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null || definition.Parameters.Count == 0)
                return "无可调参数";

            var parts = new List<string>(definition.Parameters.Count);
            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter = definition.Parameters[i];
                parts.Add(FormatParameterName(parameter)
                    + " "
                    + FormatParameterValue(entry, definition, parameter));
            }

            return string.Join("，", parts);
        }

        private static string FormatRiskTags(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> riskTags)
        {
            if (riskTags == null || riskTags.Count == 0)
                return "none";

            var parts = new string[riskTags.Count];
            for (int i = 0; i < riskTags.Count; i++)
            {
                parts[i] = CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatRiskLabel(
                    riskTags[i]);
            }

            return string.Join(", ", parts);
        }

        private static string FormatCapabilityList(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities)
        {
            if (capabilities == null || capabilities.Count == 0)
                return "none";

            var parts = new string[capabilities.Count];
            for (int i = 0; i < capabilities.Count; i++)
            {
                parts[i] = CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatCapabilityLabel(
                    capabilities[i]);
            }

            return string.Join(", ", parts);
        }

        private static string FormatCondition(
            CharacterTestRuntimeAiPlannerBehaviorConditionDefinition condition,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals)
                return FormatBoolCondition(condition.Fact, condition.BoolValue);
            if (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.NotEquals)
                return FormatBoolCondition(condition.Fact, !condition.BoolValue);

            float value = !string.IsNullOrWhiteSpace(condition.FloatParameterKey)
                && entry != null
                && definition != null
                ? entry.GetFloat(definition, condition.FloatParameterKey)
                : condition.FloatValue;
            string op = condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual
                ? ">="
                : "<=";
            return FormatFactLabel(condition.Fact)
                + " "
                + op
                + " "
                + value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatBoolCondition(CharacterTestRuntimeAiPlannerFactKind fact, bool value)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                    return value ? "有目标" : "无目标";
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                    return value ? "目标在攻击范围" : "目标不在攻击范围";
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                    return value ? "自身动作执行中" : "自身空闲";
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                    return value ? "路径可用" : "路径不可用";
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                    return value ? "目标可见" : "目标不可见";
                case CharacterTestRuntimeAiPlannerFactKind.TargetDead:
                    return value ? "目标死亡" : "目标存活";
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                    return value ? "视线被遮挡" : "视线未遮挡";
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                    return value ? "位于硬滑地表" : "不在硬滑地表";
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                    return value ? "附近有可移动物" : "附近无可移动物";
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                    return value ? "直线路径清晰" : "直线路径受阻";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle:
                    return value ? "附近有障碍" : "附近无障碍";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard:
                    return value ? "附近有危险" : "附近无危险";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject:
                    return value ? "附近有可用物" : "附近无可用物";
                default:
                    return FormatFactLabel(fact) + " = " + (value ? "true" : "false");
            }
        }

        private static string FormatFactLabel(CharacterTestRuntimeAiPlannerFactKind fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                    return "目标存在";
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                    return "目标在攻击范围";
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistance:
                    return "锁定受击框净空";
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable:
                    return "受击框距离可用";
                case CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance:
                    return "目标根距离";
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                    return "自身动作占用";
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                    return "路径可用";
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                    return "目标可见";
                case CharacterTestRuntimeAiPlannerFactKind.TargetDead:
                    return "目标死亡";
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                    return "视线遮挡";
                case CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames:
                    return "目标丢失帧数";
                case CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore:
                    return "目标威胁分";
                case CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind:
                    return "自身地表";
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                    return "硬滑地表";
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                    return "附近可移动物";
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                    return "直线路径";
                case CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex:
                    return "移动候选";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle:
                    return "附近障碍";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard:
                    return "附近危险";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject:
                    return "附近可用物";
                default:
                    return fact.ToString();
            }
        }

        private static string FormatGeneratedActions(string generatedActionsText)
        {
            if (string.IsNullOrWhiteSpace(generatedActionsText))
                return "无输出";

            string[] tokens = generatedActionsText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
                tokens[i] = FormatGeneratedActionToken(tokens[i].Trim());
            return string.Join(" / ", tokens);
        }

        private static string FormatGeneratedActionToken(string token)
        {
            int at = token.IndexOf('@');
            string kind = at >= 0 ? token.Substring(0, at) : token;
            switch (kind)
            {
                case "MoveToTarget":
                    return "向目标移动";
                case "Strafe":
                    return "侧移绕位";
                case "Retreat":
                    return "后撤";
                case "PrimaryAttack":
                    return "普通攻击";
                case "CastAbilitySlot":
                    return "释放技能";
                case "Wait":
                    return "等待/保持";
                default:
                    return token;
            }
        }

        private static bool HasCondition(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool boolValue)
        {
            for (int i = 0; definition != null && i < definition.Conditions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorConditionDefinition condition = definition.Conditions[i];
                if (condition.Fact == fact
                    && condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                    && condition.BoolValue == boolValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTargetFact(CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            for (int i = 0; definition != null && i < definition.RequiredFacts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerFactKind fact = definition.RequiredFacts[i];
                if (fact == CharacterTestRuntimeAiPlannerFactKind.TargetExists
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetDistance
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetVisible
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetDead
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatScoreBand(float value)
        {
            if (value >= 100f)
                return "高";
            if (value >= 50f)
                return "中";
            return "低";
        }

        private static string FormatCostBand(float value)
        {
            if (value <= 0.8f)
                return "低";
            if (value <= 1.5f)
                return "中";
            return "高";
        }

        private static bool ResolveBool(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            return entry != null
                ? entry.GetBool(definition, parameter.Key)
                : parameter.DefaultBool;
        }

        private static int ResolveInt(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            return entry != null
                ? entry.GetInt(definition, parameter.Key)
                : parameter.DefaultInt;
        }

        private static float ResolveFloat(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            return entry != null
                ? entry.GetFloat(definition, parameter.Key)
                : parameter.DefaultFloat;
        }

        private static string TrimEntrySuffix(string displayName)
        {
            int bracket = displayName.IndexOf(" [", StringComparison.Ordinal);
            return bracket > 0 ? displayName.Substring(0, bracket) : displayName;
        }
    }
}
