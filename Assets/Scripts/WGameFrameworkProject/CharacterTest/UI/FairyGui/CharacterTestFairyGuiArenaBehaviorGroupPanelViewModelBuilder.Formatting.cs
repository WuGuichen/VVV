using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelBuilder
    {
        private static string FormatDefinitionBadges(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            bool recommended)
        {
            if (definition == null)
                return string.Empty;
            string text = "能力: " + CapabilityDisplayName(definition.PrimaryCapability);
            if (definition.SecondaryCapabilities.Count > 0)
            {
                text += " +" + definition.SecondaryCapabilities.Count.ToString(CultureInfo.InvariantCulture);
            }

            return recommended ? "推荐 | " + text : text;
        }

        private static string FormatEntryStateText(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            if (entry == null)
                return string.Empty;
            if (entry.Disabled)
                return "临时禁用";
            if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                return "有诊断";
            return "可执行";
        }

        private static string FormatEntrySeverity(CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            return string.IsNullOrWhiteSpace(summary.DiagnosticsText) ? "ok" : "warning";
        }

        private static string FormatForecastNotice(CharacterTestArenaBehaviorGroupOutcomeRow row)
        {
            if (row == null)
                return string.Empty;
            if (!row.HasExpectedAction)
                return "无可执行输出，检查条件或兜底";
            if (!string.IsNullOrWhiteSpace(row.SuppressedBehavior))
                return "压制: " + row.SuppressedBehavior;
            if (string.IsNullOrWhiteSpace(row.Notice))
                return "正常";
            return "有注意项，详见高级诊断";
        }

        private static string FormatDefinitionCardDetail(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null)
                return string.Empty;
            return "Trigger: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatConditions(definition, null)
                + " | Output: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                    definition.ActionPrimitive)
                + " | "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatCapabilities(definition);
        }

        private static string FormatEntryCardDetail(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            string forecastText)
        {
            if (entry == null)
                return string.Empty;
            string output = definition != null
                ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(definition.ActionPrimitive)
                : "Unknown";
            string generated = !string.IsNullOrWhiteSpace(summary.GeneratedActionsText)
                ? summary.GeneratedActionsText
                : output;
            return "Order "
                + entry.Order.ToString(CultureInfo.InvariantCulture)
                + " | "
                + output
                + " | "
                + forecastText
                + " | "
                + generated;
        }

        private static string FormatSelectedDefinitionDetail(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            if (definition == null)
                return "Select a card from the "
                    + CapabilityDisplayName(capability)
                    + " slot, then Add Card to put it into the deck.";
            return DisplayBehaviorName(definition)
                + "\nCapability: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatCapabilities(definition)
                + "\nWhen: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatConditions(definition, null)
                + "\nOutput: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                    definition.ActionPrimitive)
                + "\nIntent: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatIntent(definition)
                + "\nRisk: "
                + FormatDefinitionRisks(definition);
        }

        private static string FormatSelectedEntryDetail(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            string forecastText)
        {
            if (entry == null)
                return string.Empty;
            return DisplayBehaviorName(definition)
                + " instance"
                + "\nState: "
                + (entry.Disabled ? "Disabled in this Arena deck." : "Executable when conditions match.")
                + "\nWhen: "
                + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatConditions(definition, entry)
                + "\nOutput: "
                + (definition != null
                    ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                        definition.ActionPrimitive)
                    : "Unknown")
                + "\nForecast: "
                + forecastText
                + "\nGenerated: "
                + (string.IsNullOrWhiteSpace(summary.GeneratedActionsText)
                    ? "No runtime action generated."
                    : summary.GeneratedActionsText)
                + "\nDiagnostics: "
                + (string.IsNullOrWhiteSpace(summary.DiagnosticsText) ? "none" : summary.DiagnosticsText);
        }

        private static string FormatInspector(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            string forecastText)
        {
            return entry != null
                ? FormatSelectedEntryDetail(entry, definition, summary, forecastText)
                : FormatSelectedDefinitionDetail(definition, CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting);
        }

        private static string FormatEntryForecast(
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (forecast == null || entry == null)
                return "Not forecast";
            for (int i = 0; i < forecast.Rows.Count; i++)
            {
                CharacterTestArenaBehaviorGroupOutcomeRow row = forecast.Rows[i];
                if (string.Equals(row.ExpectedEntryId, entry.EntryId, StringComparison.Ordinal))
                    return "hits in " + row.ScenarioName;
                if (string.Equals(row.SuppressedEntryId, entry.EntryId, StringComparison.Ordinal))
                    return "suppressed in " + row.ScenarioName;
            }

            return entry.Disabled ? "disabled" : "not selected in sample scenarios";
        }

        private static string FormatCoverageHeadline(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            if (report == null)
                return "Capability coverage unavailable.";
            return report.SummaryText + " | " + report.CoverageText + " | " + report.RiskText;
        }

        private static string FormatCoverageSummary(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            if (report == null)
                return "能力覆盖不可用";
            return report.ArenaCombatReady ? "最小可运行组合已满足" : "最小可运行组合缺能力";
        }

        private static string FormatCoverageRequiredText(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            if (report == null)
                return "必备能力: 未知";
            if (report.MissingCapabilities.Count == 0)
                return "必备能力: 已覆盖";

            var parts = new string[report.MissingCapabilities.Count];
            for (int i = 0; i < report.MissingCapabilities.Count; i++)
                parts[i] = CapabilityDisplayName(report.MissingCapabilities[i]);
            return "缺少: " + string.Join(", ", parts);
        }

        private static string FormatCoverageRiskText(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            if (report == null)
                return "风险: 未知";
            if (string.IsNullOrWhiteSpace(report.RiskText))
                return "风险: 暂无明显追逃死循环";
            return "风险: " + report.RiskText;
        }

        private static string FormatActionSummary(
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            int entries = draft != null ? draft.EntryCount : 0;
            int actions = compile != null && compile.Profile != null && compile.Profile.Actions != null
                ? compile.Profile.Actions.Count
                : 0;
            return "卡组 "
                + entries.ToString(CultureInfo.InvariantCulture)
                + " 张 | 生成 action "
                + actions.ToString(CultureInfo.InvariantCulture)
                + " | "
                + (report != null && report.ArenaCombatReady ? "最小组合通过" : "需要补卡");
        }

        private static string FormatDecisionText(
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report,
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            var builder = new StringBuilder(512);
            if (forecast != null)
                builder.AppendLine(forecast.Text);
            if (report != null)
            {
                builder.AppendLine(report.CoverageText);
                if (!string.IsNullOrWhiteSpace(report.RiskText))
                    builder.AppendLine(report.RiskText);
            }

            string chain = FormatDecisionChain(draft, store, compile);
            if (!string.IsNullOrWhiteSpace(chain))
            {
                builder.AppendLine("Deck order:");
                builder.Append(chain);
            }

            return builder.ToString();
        }

        private static string FormatDecisionChain(
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            if (draft == null || draft.Profile.Entries.Count == 0)
                return "No cards in deck.";
            var builder = new StringBuilder(256);
            for (int i = 0; i < draft.Profile.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = draft.Profile.Entries[i];
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = ResolveDefinition(store, entry);
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary = FindSummary(compile, entry);
                builder.Append(i + 1)
                    .Append(". ")
                    .Append(DisplayBehaviorName(definition))
                    .Append(" -> ")
                    .Append(definition != null
                        ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                            definition.ActionPrimitive)
                        : "Unknown")
                    .Append(" | ")
                    .Append(entry.Disabled ? "disabled" : "enabled");
                if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                    builder.Append(" | ").Append(summary.DiagnosticsText);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string FormatCompileStatus(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            if (compile == null)
                return "编译状态不可用";
            return compile.Success ? "编译通过" : "编译失败";
        }

        private static string FormatDiagnostics(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            if (compile == null || compile.Report == null)
                return "Diagnostics unavailable.";
            if (compile.Report.Success)
                return "Diagnostics: none.";

            var builder = new StringBuilder(256);
            for (int i = 0; i < compile.Report.Diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = compile.Report.Diagnostics[i];
                builder.Append(diagnostic.Severity)
                    .Append(" | ")
                    .Append(diagnostic.Code)
                    .Append(" | ")
                    .AppendLine(diagnostic.Message);
            }

            return builder.ToString();
        }

        private static string FormatDraftLabel(
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestArenaBehaviorGroupDraft draft)
        {
            if (draft == null)
                return "当前草稿: 不可用";
            int draftCount = store != null ? store.Drafts.Count : 0;
            return "当前草稿: "
                + FormatBehaviorGroupDisplayName(draft.DisplayName)
                + " | 草稿 "
                + draftCount.ToString(CultureInfo.InvariantCulture)
                + " | 卡 "
                + draft.EntryCount.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatBehaviorGroupDisplayName(string displayName)
        {
            return string.Equals(displayName, "Training dummy", StringComparison.Ordinal)
                ? "木桩"
                : displayName;
        }

        private static string FormatCapabilityFilterLabel(
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            string searchText)
        {
            string text = "当前能力: " + CapabilityDisplayName(capability);
            if (!string.IsNullOrWhiteSpace(searchText))
                text += " | 搜索: " + searchText;
            return text;
        }

        private static string FormatLibrarySummary(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> filteredDefinitions,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            int page,
            string searchText)
        {
            string text = "可添加行为卡 | "
                + CapabilityDisplayName(capability)
                + " | 第 "
                + (page + 1).ToString(CultureInfo.InvariantCulture)
                + " 页 | 结果 "
                + (filteredDefinitions != null ? filteredDefinitions.Count : 0).ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(searchText))
                text += " | 搜索 \"" + searchText + "\"";
            return text;
        }

        private static string FormatEntrySummary(
            CharacterTestArenaBehaviorGroupDraft draft,
            int page)
        {
            return "当前卡组 | 第 "
                + (page + 1).ToString(CultureInfo.InvariantCulture)
                + " 页 | 卡 "
                + (draft != null ? draft.EntryCount : 0).ToString(CultureInfo.InvariantCulture);
        }

        private static string DisplayBehaviorName(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            return definition == null
                ? "Unknown behavior"
                : CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatBehaviorName(definition);
        }

        private static bool HasCapabilityRisk(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            if (report == null)
                return false;
            if (capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Kite
                || capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Defense)
            {
                return report.HasOnlyStallLoopRisk;
            }

            if (capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit
                || capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Control)
            {
                return report.HasRisk(CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingKiteCounter);
            }

            return false;
        }

        private static string FormatCapabilityState(bool missing, bool disabledOnly, bool risk)
        {
            if (missing)
                return "缺失";
            if (disabledOnly)
                return "仅禁用卡提供";
            if (risk)
                return "有风险";
            return "已满足";
        }

        private static string FormatCapabilityRole(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            switch (capability)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting:
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Damage:
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback:
                    return "必备";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Engage:
                    return "必备或压制";
                default:
                    return "可选";
            }
        }

        private static string FormatCapabilityHint(
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            bool missing,
            bool disabledOnly,
            bool risk)
        {
            if (missing)
                return "点击后从卡库补齐";
            if (disabledOnly)
                return "已有卡被临时禁用";
            if (risk)
                return "可能导致追逃僵持";
            return CapabilityDescription(capability);
        }

        private static string FormatCapabilitySeverity(bool missing, bool disabledOnly, bool risk)
        {
            if (missing)
                return "missing";
            if (disabledOnly)
                return "disabled";
            if (risk)
                return "risk";
            return "ready";
        }

        private static string CapabilityDisplayName(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            switch (capability)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting:
                    return "Targeting / 索敌";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Engage:
                    return "Engage / 接战";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Damage:
                    return "Damage / 输出";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback:
                    return "Fallback / 兜底";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit:
                    return "Pursuit / 追击";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Control:
                    return "Control / 控制";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Kite:
                    return "Kite / 拉扯";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Defense:
                    return "Defense / 防御";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation:
                    return "Navigation / 路径";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Support:
                    return "Support / 协同";
                default:
                    return "None";
            }
        }

        private static string CapabilityDescription(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            switch (capability)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting:
                    return "Find a target and refresh target facts.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Engage:
                    return "Reach or pressure a target so damage can happen.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Damage:
                    return "Produce attacks or ability casts that can end the fight.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback:
                    return "Keep behavior stable when no higher card can run.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit:
                    return "Catch kiting or fleeing opponents.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Control:
                    return "Limit, interrupt, pull or stop the opponent.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Kite:
                    return "Keep distance while staying relevant.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Defense:
                    return "Survive hazards or pressure.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation:
                    return "Recover when blocked or line of sight is bad.";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Support:
                    return "Team follow, protect, heal or buff hooks.";
                default:
                    return string.Empty;
            }
        }

        private static string FormatDefinitionRisks(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null || definition.RiskTags.Count == 0)
                return "none";

            var parts = new string[definition.RiskTags.Count];
            for (int i = 0; i < definition.RiskTags.Count; i++)
            {
                parts[i] = CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatRiskLabel(
                    definition.RiskTags[i]);
            }

            return string.Join(", ", parts);
        }

        private static string L(IMxUiTextProvider textProvider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(textProvider, key, fallback);
        }
    }
}
