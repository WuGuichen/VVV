using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder
    {
        private static string StateText(
            CharacterTestFairyGuiBehaviorDeckCardState state,
            IMxUiTextProvider textProvider)
        {
            switch (state)
            {
                case CharacterTestFairyGuiBehaviorDeckCardState.Installed:
                    return L(textProvider, "ui.character_test.behavior_deck.state.installed", "已安装");
                case CharacterTestFairyGuiBehaviorDeckCardState.Locked:
                    return L(textProvider, "ui.character_test.behavior_deck.state.locked", "未解锁");
                case CharacterTestFairyGuiBehaviorDeckCardState.Unavailable:
                    return L(textProvider, "ui.character_test.behavior_deck.state.unavailable", "内容不可用");
                case CharacterTestFairyGuiBehaviorDeckCardState.Incompatible:
                    return L(textProvider, "ui.character_test.behavior_deck.state.incompatible", "版本不兼容");
                case CharacterTestFairyGuiBehaviorDeckCardState.Empty:
                    return L(textProvider, "ui.character_test.behavior_deck.state.empty", "空卡槽");
                default:
                    return L(textProvider, "ui.character_test.behavior_deck.state.available", "可安装");
            }
        }

        private static string BlockReasonText(
            CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason reason,
            IMxUiTextProvider textProvider)
        {
            switch (reason)
            {
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.SessionLocked:
                    return L(textProvider, "ui.character_test.behavior_deck.block.session_locked", "会话已锁定");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.NotUnlocked:
                    return L(textProvider, "ui.character_test.behavior_deck.block.not_unlocked", "尚未解锁");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.AlreadyInstalled:
                    return L(textProvider, "ui.character_test.behavior_deck.block.installed", "已安装");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.SlotLimit:
                    return L(textProvider, "ui.character_test.behavior_deck.block.slot_limit", "卡槽已满");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.CostLimit:
                    return L(textProvider, "ui.character_test.behavior_deck.block.cost_limit", "费用不足");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Unavailable:
                    return L(textProvider, "ui.character_test.behavior_deck.block.unavailable", "内容不可用");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Incompatible:
                    return L(textProvider, "ui.character_test.behavior_deck.block.incompatible", "版本不兼容");
                case CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.DeckUnresolved:
                    return L(textProvider, "ui.character_test.behavior_deck.block.unresolved", "先修复卡组");
                default:
                    return L(textProvider, "ui.character_test.behavior_deck.block.disabled", "不可操作");
            }
        }

        private static string JoinTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return string.Empty;
            var builder = new StringBuilder(48);
            int count = Math.Min(3, tags.Count);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append(" · ");
                builder.Append(tags[i]);
            }
            return builder.ToString();
        }

        private static CharacterTestFairyGuiBehaviorDeckCardIcon ResolveCardIcon(
            IReadOnlyList<string> tags)
        {
            for (int i = 0; tags != null && i < tags.Count; i++)
            {
                string tag = tags[i] ?? string.Empty;
                if (ContainsAny(tag, "连段", "突进", "combo", "gap", "chain"))
                    return CharacterTestFairyGuiBehaviorDeckCardIcon.Diamond;
                if (ContainsAny(tag, "反应", "生存", "闪避", "reaction", "survival", "dodge", "defense"))
                    return CharacterTestFairyGuiBehaviorDeckCardIcon.Circle;
                if (ContainsAny(tag, "接敌", "移动", "持续", "move", "sustain", "approach"))
                    return CharacterTestFairyGuiBehaviorDeckCardIcon.Triangle;
                if (ContainsAny(tag, "基础", "攻击", "attack", "basic"))
                    return CharacterTestFairyGuiBehaviorDeckCardIcon.Square;
            }
            return CharacterTestFairyGuiBehaviorDeckCardIcon.Fallback;
        }

        private static bool ContainsAny(
            string value,
            string first,
            string second,
            string third,
            string fourth,
            string fifth = "",
            string sixth = "")
        {
            return value.IndexOf(first, StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf(second, StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf(third, StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf(fourth, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrEmpty(fifth)
                    && value.IndexOf(fifth, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(sixth)
                    && value.IndexOf(sixth, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ContainsAny(
            string value,
            string first,
            string second,
            string third,
            string fourth,
            string fifth,
            string sixth,
            string seventh)
        {
            return ContainsAny(value, first, second, third, fourth, fifth, sixth)
                || value.IndexOf(seventh, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatBlockReason(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            IMxUiTextProvider textProvider)
        {
            if (card.BlockReason == CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.CostLimit
                && source != null)
            {
                int remaining = Math.Max(0, source.MaximumCost - source.KnownCost);
                int shortage = Math.Max(0, card.Cost - remaining);
                return L(textProvider, "ui.character_test.behavior_deck.block.cost_detail", "费用不足")
                    + " · 还差" + shortage.ToString(CultureInfo.InvariantCulture) + "费";
            }
            return BlockReasonText(card.BlockReason, textProvider);
        }

        private static string FormatBudgetDelta(CharacterTestBehaviorDeckOperationFeedback feedback)
        {
            int slotDelta = feedback.AfterSlotCount - feedback.BeforeSlotCount;
            int costDelta = feedback.AfterCost - feedback.BeforeCost;
            if (slotDelta == 0 && costDelta == 0)
                return string.Empty;
            return Signed(slotDelta) + "槽 / " + Signed(costDelta) + "费";
        }

        private static string FormatFeedback(
            CharacterTestBehaviorDeckOperationFeedback feedback,
            IMxUiTextProvider textProvider)
        {
            if (feedback.Outcome == CharacterTestBehaviorDeckOperationOutcome.Rejected)
                return feedback.Message;
            return feedback.Kind == CharacterTestBehaviorDeckOperationKind.Install
                ? L(textProvider, "ui.character_test.behavior_deck.command.installed", "已安装行为卡")
                    + (feedback.InstalledSlotIndex >= 0
                        ? " · 槽位" + (feedback.InstalledSlotIndex + 1).ToString(CultureInfo.InvariantCulture)
                        : string.Empty)
                : L(textProvider, "ui.character_test.behavior_deck.command.removed", "已移除行为卡");
        }

        private static string FormatCardFeedback(
            CharacterTestBehaviorDeckOperationFeedback feedback,
            IMxUiTextProvider textProvider)
        {
            if (feedback.Outcome == CharacterTestBehaviorDeckOperationOutcome.Rejected)
                return feedback.Message;
            int costDelta = feedback.AfterCost - feedback.BeforeCost;
            if (feedback.Kind == CharacterTestBehaviorDeckOperationKind.Install)
            {
                return L(textProvider, "ui.character_test.behavior_deck.feedback.installed", "已安装")
                    + " · " + Signed(costDelta) + "费"
                    + (feedback.InstalledSlotIndex >= 0
                        ? " · 槽位" + (feedback.InstalledSlotIndex + 1).ToString(CultureInfo.InvariantCulture)
                        : string.Empty);
            }
            return L(textProvider, "ui.character_test.behavior_deck.feedback.removed", "已移除")
                + " · " + Signed(costDelta) + "费";
        }

        private static CharacterTestFairyGuiBehaviorDeckFeedbackTone ResolveFeedbackTone(
            CharacterTestBehaviorDeckOperationFeedback feedback)
        {
            if (feedback.Outcome == CharacterTestBehaviorDeckOperationOutcome.Success)
                return CharacterTestFairyGuiBehaviorDeckFeedbackTone.Success;
            return feedback.Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning
                ? CharacterTestFairyGuiBehaviorDeckFeedbackTone.Warning
                : CharacterTestFairyGuiBehaviorDeckFeedbackTone.Error;
        }

        private static string Signed(int value) => value >= 0
            ? "+" + value.ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

        private static string FormatDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            IMxUiTextProvider textProvider)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return L(textProvider, "ui.character_test.behavior_deck.diagnostics.passed", "✓ 通过");
            var builder = new StringBuilder(256);
            int shown = 0;
            AppendDiagnosticsOfSeverity(
                builder,
                diagnostics,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                textProvider,
                ref shown);
            AppendDiagnosticsOfSeverity(
                builder,
                diagnostics,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                textProvider,
                ref shown);
            AppendDiagnosticsOfSeverity(
                builder,
                diagnostics,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info,
                textProvider,
                ref shown);
            if (diagnostics.Count > shown)
            {
                builder.Append('\n').Append("另有 ")
                    .Append(diagnostics.Count - shown)
                    .Append(" 项");
            }
            return builder.ToString();
        }

        internal static string FormatDiagnosticsForTests(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            IMxUiTextProvider textProvider = null) =>
            FormatDiagnostics(diagnostics, textProvider ?? MxUiNullTextProvider.Instance);

        private static void AppendDiagnosticsOfSeverity(
            StringBuilder builder,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity,
            IMxUiTextProvider textProvider,
            ref int shown)
        {
            if (shown >= 3)
                return;
            for (int i = 0; i < diagnostics.Count && shown < 3; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.Severity != severity)
                    continue;
                if (shown > 0)
                    builder.Append('\n');
                string prefix = severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error
                    ? L(textProvider, "ui.character_test.behavior_deck.diagnostics.error", "错误")
                    : severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning
                        ? L(textProvider, "ui.character_test.behavior_deck.diagnostics.warning", "警告")
                        : L(textProvider, "ui.character_test.behavior_deck.diagnostics.info", "提示");
                builder.Append(prefix).Append('：').Append(PlayerFacingDiagnosticMessage(diagnostic, textProvider));
                shown++;
            }
        }

        private static string PlayerFacingDiagnosticMessage(
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic,
            IMxUiTextProvider textProvider)
        {
            switch (diagnostic.Code)
            {
                case "PLAYER_DECK_CARD_UNAVAILABLE":
                case "INSTALLED_DECK_CARD_UNAVAILABLE":
                    return L(
                        textProvider,
                        "ui.character_test.behavior_deck.diagnostics.card_unavailable",
                        "卡组中有一张行为卡当前不可用，请替换或移除。");
                case "PLAYER_DECK_CARD_INCOMPATIBLE":
                    return L(
                        textProvider,
                        "ui.character_test.behavior_deck.diagnostics.card_incompatible",
                        "卡组中有一张行为卡与当前版本不兼容，请替换或移除。");
                case "INSTALLED_DECK_CHARACTER_CAPABILITY_MISMATCH":
                    return L(
                        textProvider,
                        "ui.character_test.behavior_deck.diagnostics.character_capability_mismatch",
                        "角色缺少一张已安装行为卡所需的能力，该卡可能无法正常执行。");
                case "INSTALLED_DECK_FALLBACK_ONLY":
                    return L(
                        textProvider,
                        "ui.character_test.behavior_deck.diagnostics.fallback_only",
                        "当前卡组仅依靠免费基础行为，战斗能力较弱。");
                case "INSTALLED_DECK_TARGETING_MISSING":
                case "INSTALLED_DECK_ENGAGE_MISSING":
                case "INSTALLED_DECK_DAMAGE_MISSING":
                case "INSTALLED_DECK_DISENGAGE_MISSING":
                case "INSTALLED_DECK_SURVIVAL_MISSING":
                    return L(
                        textProvider,
                        "ui.character_test.behavior_deck.diagnostics.basic_capability_missing",
                        "当前卡组缺少一项基础战斗能力。");
                default:
                    return diagnostic.Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error
                        ? L(
                            textProvider,
                            "ui.character_test.behavior_deck.diagnostics.generic_error",
                            "卡组存在无法应用的问题，请检查后重试。")
                        : diagnostic.Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning
                            ? L(
                                textProvider,
                                "ui.character_test.behavior_deck.diagnostics.generic_warning",
                                "卡组需要检查。")
                            : L(
                                textProvider,
                                "ui.character_test.behavior_deck.diagnostics.generic_info",
                                "卡组状态已更新。");
            }
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback) =>
            CharacterTestUiText.Resolve(provider, key, fallback);
    }
}
