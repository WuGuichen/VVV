using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using MxFramework.CharacterAction;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static void FillBehaviorCardRuntimeHeader(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection identity,
            bool hasSelectedAgent)
        {
            CharacterTestRuntimeAiLabEditCardIdentity edit = identity?.EditCard;
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime = identity?.Runtime;
            string activeCardName = runtime?.CommittedCard?.DisplayName
                ?? runtime?.PendingCard?.DisplayName
                ?? string.Empty;
            CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship activeRelationship =
                identity?.Relationship ?? CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.DesignOnly;
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel activeRuntimeControl =
                viewModel.BehaviorCardRuntimeControl;
            if (activeRuntimeControl != null)
            {
                activeRuntimeControl.HasDifferentActiveCard = hasSelectedAgent
                    && !string.IsNullOrEmpty(activeCardName)
                    && activeRelationship
                        != CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardSameSemantic
                    && activeRelationship
                        != CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardDifferentSemantic;
                activeRuntimeControl.ActiveCardName = activeCardName;
            }

            // D1 renders live state directly on the timeline cards. The retired header prose
            // duplicated that information and allocated strings on every panel refresh.
            viewModel.BehaviorCardRuntimeHeaderText = NonEmpty(edit?.DisplayName, "未命名行为卡");
            viewModel.BehaviorCardRuntimeStateText = string.Empty;
            viewModel.BehaviorCardExecutionSummaryText = string.Empty;
            viewModel.BehaviorCardRecentEventsText = string.Empty;
        }

        private static string FormatPlayerLegalityStatus(
            CharacterTestRuntimeAiLabPlayerLegalityState state)
        {
            if (state == null
                || state.Status == CharacterTestRuntimeAiLabPlayerLegalityStatus.Unavailable)
            {
                return "玩家合法性待接入";
            }
            return state.IsPlayerLegal
                ? "玩家合法构筑 · " + state.SlotCount.ToString(CultureInfo.InvariantCulture)
                    + "/" + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots
                    + " 槽 · " + state.TotalCost.ToString(CultureInfo.InvariantCulture)
                    + "/" + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost + " 费"
                : "非玩家合法构筑 · Lab 实验 · "
                    + CountPlayerLegalityErrors(state).ToString(CultureInfo.InvariantCulture) + " 项";
        }

        private static string FormatPlayerLegalityReasons(
            CharacterTestRuntimeAiLabPlayerLegalityState state)
        {
            if (state == null || state.Reasons.Count == 0)
                return "玩家构筑规则已通过；Lab 编辑不会改变已安装卡组。";
            var builder = new StringBuilder(192);
            int visible = 0;
            int eligible = 0;
            for (int i = 0; i < state.Reasons.Count; i++)
            {
                CharacterTestRuntimeAiLabPlayerLegalityReason reason = state.Reasons[i];
                if (reason.Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info)
                    continue;
                eligible++;
                if (visible >= 2)
                    continue;
                if (visible > 0)
                    builder.Append(" · ");
                builder.Append(reason.Message);
                visible++;
            }
            int remaining = eligible - visible;
            if (remaining > 0)
                builder.Append(" · 另有 ").Append(remaining).Append(" 项");
            return builder.Length > 0 ? builder.ToString() : "玩家构筑规则已通过。";
        }

        private static int CountPlayerLegalityErrors(
            CharacterTestRuntimeAiLabPlayerLegalityState state)
        {
            int count = 0;
            for (int i = 0; i < state.Reasons.Count; i++)
            {
                if (state.Reasons[i].Severity
                    == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                {
                    count++;
                }
            }
            return count;
        }

        private static string FormatPublishStatus(
            string status,
            int publishedRevision,
            bool hasPublishAttempt,
            bool publishSucceeded)
        {
            if (hasPublishAttempt)
            {
                if (!publishSucceeded)
                    return string.IsNullOrEmpty(status) ? "Publish 失败" : status;
                return "已发布 r"
                    + publishedRevision.ToString(CultureInfo.InvariantCulture)
                    + " · 会话/Replay 未改变";
            }
            return publishedRevision > 0
                ? "来源 r" + publishedRevision.ToString(CultureInfo.InvariantCulture) + " · 尚无新发布"
                : "尚未发布当前草稿";
        }

        private static float ResolveMovementCompletionDistance(
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            string behaviorId)
        {
            if (program == null
                || !string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                return -1f;
            }

            IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> conditions =
                program.ActivationConditions;
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledCondition condition = conditions[i];
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    && (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan
                        || condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual)
                    && condition.Value > 0f
                    && !float.IsNaN(condition.Value)
                    && !float.IsInfinity(condition.Value))
                {
                    return condition.Value;
                }
            }

            return -1f;
        }

        private static bool HasResponsibility(
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorEntryDraft> entries,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind)
        {
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].ResponsibilityKinds[0] == responsibilityKind)
                    return true;
            }
            return false;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> source)
        {
            var result = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[source?.Count ?? 0];
            for (int i = 0; i < result.Length; i++)
                result[i] = source[i];
            return CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(result);
        }

        private static int BuildResponsibilityCompatibilityMask(
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            int mask = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> ordered =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.OrderedResponsibilities;
            for (int i = 0; i < ordered.Count; i++)
                mask |= 1 << (int)ordered[i];
            return mask;
        }

        private static string FormatResponsibilitySelection(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities)
        {
            if (responsibilities == null || responsibilities.Count == 0)
                return CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other);
            string first = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(
                responsibilities[0]);
            return responsibilities.Count == 1
                ? first
                : first + " +" + (responsibilities.Count - 1).ToString(CultureInfo.InvariantCulture);
        }

        private static int[] BuildResponsibilityCounts(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows,
            int rowCount)
        {
            var counts = new int[5];
            for (int rowIndex = 0; rows != null && rowIndex < rowCount; rowIndex++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[rowIndex];
                for (int responsibilityIndex = 0;
                     row?.ResponsibilityKinds != null
                        && responsibilityIndex < row.ResponsibilityKinds.Length;
                     responsibilityIndex++)
                {
                    int kindIndex = (int)row.ResponsibilityKinds[responsibilityIndex];
                    if (kindIndex >= 0 && kindIndex < counts.Length)
                        counts[kindIndex]++;
                }
            }
            return counts;
        }

        private static CharacterTestRuntimeAiLabWorkbenchRowViewModel[] ArrangeResponsibilityRows(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] source,
            int cardRowCount,
            int headerCount)
        {
            var ordered = new CharacterTestRuntimeAiLabWorkbenchRowViewModel[cardRowCount + headerCount];
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities =
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
            };
            int output = 0;
            for (int responsibilityIndex = 0;
                 responsibilityIndex < responsibilities.Length && output < ordered.Length;
                 responsibilityIndex++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility =
                    responsibilities[responsibilityIndex];
                int total = 0;
                int enabled = 0;
                string first = string.Empty;
                string second = string.Empty;
                for (int i = 0; i < cardRowCount; i++)
                {
                    CharacterTestRuntimeAiLabWorkbenchRowViewModel card = source[i];
                    if (card == null || card.ResponsibilityKind != responsibility)
                        continue;
                    total++;
                    if (!card.Disabled)
                        enabled++;
                    if (first.Length == 0)
                        first = card.TitleText;
                    else if (second.Length == 0)
                        second = card.TitleText;
                }
                if (responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                    && total == 0)
                {
                    continue;
                }

                var header = new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                {
                    RowKind = CharacterTestRuntimeAiLabWorkbenchRowKind.ResponsibilityHeader,
                    EntryIndex = -1,
                    ResponsibilityKind = responsibility,
                    ResponsibilityTotalCount = total,
                    ResponsibilityEnabledCount = enabled,
                    ResponsibilityWarning = total == 0 || enabled == 0,
                    ResponsibilityColor = ResponsibilityColor(responsibility),
                    TitleText = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(
                        responsibility),
                    BadgeText = enabled.ToString(CultureInfo.InvariantCulture),
                    ResponsibilitySummaryText = total == 0
                        ? "尚未配置 · 仅提示"
                        : enabled == 0
                            ? "0 可用 · " + total.ToString(CultureInfo.InvariantCulture) + " 已禁用"
                            : FormatResponsibilitySummary(first, second, total),
                    Visible = true
                };
                ordered[output++] = header;
                for (int i = 0; i < cardRowCount; i++)
                {
                    CharacterTestRuntimeAiLabWorkbenchRowViewModel card = source[i];
                    if (card != null && card.ResponsibilityKind == responsibility)
                        ordered[output++] = card;
                }
            }
            viewModel.WorkbenchRows = ordered;
            viewModel.WorkbenchRowCount = output;
            return ordered;
        }

        private static string FormatResponsibilitySummary(string first, string second, int total)
        {
            string summary = first ?? string.Empty;
            if (!string.IsNullOrEmpty(second))
                summary += "、" + second;
            if (total > 2)
                summary += " +" + (total - 2).ToString(CultureInfo.InvariantCulture);
            return summary;
        }

        private static string ResponsibilityColor(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind)
        {
            switch (responsibilityKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting:
                    return "#D8A52A";
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure:
                    return "#36C7C5";
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage:
                    return "#E9634A";
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback:
                    return "#6E8194";
                default:
                    return "#77828C";
            }
        }

        private static int FirstBehaviorEntryIndex(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows,
            int fallback)
        {
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i] != null
                    && rows[i].RowKind == CharacterTestRuntimeAiLabWorkbenchRowKind.BehaviorCard
                    && rows[i].EntryIndex >= 0)
                {
                    return rows[i].EntryIndex;
                }
            }
            return fallback;
        }

    }
}
