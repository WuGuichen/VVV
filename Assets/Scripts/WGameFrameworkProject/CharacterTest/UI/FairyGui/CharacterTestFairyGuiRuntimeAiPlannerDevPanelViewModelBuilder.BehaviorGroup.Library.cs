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
        private static string ResolveWorkbenchBehaviorIconKey(
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind coreSlotKind)
        {
            if (!string.IsNullOrWhiteSpace(card?.IconKey))
                return card.IconKey;
            if (coreSlotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting)
                return CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.TargetNearestIconKey;
            if (coreSlotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
                return CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.FallbackIconKey;
            return CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.FallbackIconKey;
        }

        private static string FormatCardDiagnostic(
            CharacterTestRuntimeAiLabCardDiagnostic diagnostic)
        {
            switch (diagnostic.Code)
            {
                case "CARD_PAYLOAD_MISSING":
                    return "行为卡数据缺失 · 请重新创建此卡，或复制一张有效行为卡";
                case "CARD_STEPS_EMPTY":
                    return "缺少执行步骤 · 点击「编辑逻辑」，添加移动、攻击或等待步骤";
                case "CARD_STEPS_LIMIT":
                    return "步骤过多 · 点击「编辑逻辑」，将步骤删减至 5 个以内";
                case "CARD_STEP_NULL":
                    return "存在损坏的步骤 · 点击「编辑逻辑」，删除无效步骤或重新创建";
                case "CARD_STEP_ID_MISSING":
                case "CARD_STEP_ID_DUPLICATE":
                    return "步骤标识无效 · 点击「编辑逻辑」，为每个步骤设置唯一标识";
                case "CARD_ENTRY_STEP_EMPTY":
                case "CARD_ENTRY_STEP_MISSING":
                    return "入口步骤无效 · 点击「编辑逻辑」，选择一个现有步骤作为入口";
                case "CARD_BRANCH_NULL":
                case "CARD_BRANCH_ID_MISSING":
                case "CARD_BRANCH_ID_DUPLICATE":
                case "CARD_BRANCH_TARGET_MISSING":
                    return "分支目标无效 · 点击「编辑逻辑」，重新选择分支目标";
                case "CARD_DEFAULT_BRANCH_COUNT":
                    return "成功分支数量错误 · 每个步骤必须且只能有一个默认成功分支";
                case "CARD_FAILURE_BRANCH_COUNT":
                    return "失败分支过多 · 每个步骤最多保留一个失败分支";
                case "CARD_BRANCH_WINDOW_INVALID":
                    return "分支时间窗口无效 · 点击「编辑逻辑」，确保结束帧不早于开始帧";
                case "CARD_ABILITY_ID_MISSING":
                    return "能力步骤尚未选择能力 · 点击「编辑逻辑」，为该步骤选择能力";
                case "CARD_ACTION_ID_MISSING":
                    return "动作步骤尚未选择动作 · 点击「编辑逻辑」，为该步骤选择动作";
                case "CARD_MOVE_BAND_INVALID":
                    return "移动距离范围无效 · 点击「编辑逻辑」，确保最大距离不小于最小距离";
                case "CARD_WAIT_FRAMES_INVALID":
                    return "等待时间无效 · 点击「编辑逻辑」，将等待帧数设为大于 0";
                case "CARD_CONDITION_NULL":
                case "CARD_CONDITION_ID_MISSING":
                case "CARD_CONDITION_ID_DUPLICATE":
                    return "存在无效条件 · 点击「编辑逻辑」，删除无效条件或为其设置唯一标识";
                case "CARD_CONDITION_OPERATOR_INVALID":
                    return "条件运算符不适用 · 点击「编辑逻辑」，重新选择该条件的比较方式";
                case "CARD_CONDITION_RANGE_INVALID":
                    return "条件范围无效 · 点击「编辑逻辑」，确保最大值不小于最小值";
                case "CARD_CONDITION_ABILITY_MISSING":
                    return "条件缺少能力 · 点击「编辑逻辑」，为该条件选择能力";
                case "CARD_CONDITION_ACTION_MISSING":
                    return "条件缺少动作 · 点击「编辑逻辑」，为该条件选择动作";
                case "CARD_GRAPH_CYCLE":
                    return "步骤形成死循环 · 点击「编辑逻辑」，修改分支以保证流程能够结束";
                case "CARD_END_UNREACHABLE":
                    return "流程无法结束 · 点击「编辑逻辑」，让至少一个分支指向结束";
                default:
                    return "行为逻辑配置无效 · 点击「编辑逻辑」，检查并修复高亮项";
            }
        }

        private static string FormatCardDiagnosticStatus(
            CharacterTestRuntimeAiLabCardDiagnostic diagnostic)
        {
            switch (diagnostic.Code)
            {
                case "CARD_STEPS_EMPTY":
                    return "缺少执行步骤";
                case "CARD_STEPS_LIMIT":
                    return "步骤过多";
                case "CARD_ENTRY_STEP_EMPTY":
                case "CARD_ENTRY_STEP_MISSING":
                    return "入口未配置";
                case "CARD_ABILITY_ID_MISSING":
                case "CARD_ACTION_ID_MISSING":
                    return "动作未配置";
                case "CARD_CONDITION_NULL":
                case "CARD_CONDITION_ID_MISSING":
                case "CARD_CONDITION_ID_DUPLICATE":
                case "CARD_CONDITION_OPERATOR_INVALID":
                case "CARD_CONDITION_RANGE_INVALID":
                case "CARD_CONDITION_ABILITY_MISSING":
                case "CARD_CONDITION_ACTION_MISSING":
                    return "条件配置错误";
                case "CARD_BRANCH_NULL":
                case "CARD_BRANCH_ID_MISSING":
                case "CARD_BRANCH_ID_DUPLICATE":
                case "CARD_BRANCH_TARGET_MISSING":
                case "CARD_DEFAULT_BRANCH_COUNT":
                case "CARD_FAILURE_BRANCH_COUNT":
                case "CARD_BRANCH_WINDOW_INVALID":
                    return "分支配置错误";
                case "CARD_GRAPH_CYCLE":
                case "CARD_END_UNREACHABLE":
                    return "流程结构错误";
                default:
                    return "配置错误";
            }
        }

        private static CharacterTestRuntimeAiLabWorkbenchRowViewModel[] EnsureWorkbenchRowCapacity(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            int required)
        {
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows = viewModel.WorkbenchRows
                ?? Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
            if (rows.Length < required)
            {
                Array.Resize(ref rows, required);
                viewModel.WorkbenchRows = rows;
            }
            for (int i = 0; i < required; i++)
            {
                if (rows[i] == null)
                    rows[i] = new CharacterTestRuntimeAiLabWorkbenchRowViewModel();
            }
            return rows;
        }

        private static bool IsBehaviorTestRuntimeSupportEntry(
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            string primaryBehaviorCardDraftId)
        {
            return entry != null
                && CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(entry.CoreSlotKind)
                && !string.Equals(
                    entry.CardDraftId,
                    primaryBehaviorCardDraftId,
                    StringComparison.Ordinal);
        }

        private static void FillWorkbenchLibraryRows(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorGroupDraft currentGroup,
            bool editable)
        {
            var cards = new List<CharacterTestRuntimeAiLabBehaviorCardDraft>(workspace.Cards.Count);
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in workspace.Cards.Values)
            {
                if (card != null)
                    cards.Add(card);
            }
            cards.Sort(CompareWorkbenchLibraryCard);

            bool groupFull =
                currentGroup.Entries.Count >= CharacterTestRuntimeAiLabDraftWorkspace.MaximumBehaviorGroupEntries;
            bool replacingCore = controller != null && controller.IsCoreSlotReplacementActive;
            if (replacingCore)
                cards = DeduplicateCoreReplacementCards(cards, workspace, currentGroup);
            var rows = new CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel[cards.Count];
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                controller?.MatchConfig?.BehaviorGroups?.DefinitionCatalog
                ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorCardDraft card = cards[i];
                bool installed = false;
                for (int entryIndex = 0; entryIndex < currentGroup.Entries.Count; entryIndex++)
                {
                    if (string.Equals(
                            currentGroup.Entries[entryIndex].CardDraftId,
                            card.CardDraftId,
                            StringComparison.Ordinal))
                    {
                        installed = true;
                        break;
                    }
                }
                string displayName = string.IsNullOrWhiteSpace(card.DisplayName)
                    ? "未命名行为卡"
                    : card.DisplayName;
                string behaviorId = FindBehaviorIdForCard(workspace, card.CardDraftId);
                bool compatible = !replacingCore
                    || (catalog.TryGetDefinition(
                            behaviorId,
                            out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition cardDefinition)
                        && CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCompatible(
                            controller.CoreSlotReplacementKind,
                            cardDefinition));
                bool available = editable
                    && compatible
                    && (replacingCore || (!installed && !groupFull));
                rows[i] = new CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel
                {
                    CardDraftId = card.CardDraftId,
                    BehaviorId = behaviorId,
                    DisplayName = displayName,
                    MonogramText = displayName.Substring(0, 1),
                    CategoryText = card.Category ?? string.Empty,
                    Installed = installed,
                    Available = available,
                    StatusText = !compatible
                        ? "槽位不兼容"
                        : replacingCore && available
                            ? "可替换"
                        : installed
                        ? "已在组内"
                        : groupFull ? "行为组已满" : available ? "可添加" : "不可添加"
                };
            }
            viewModel.WorkbenchLibraryRows = rows;
        }

        private static List<CharacterTestRuntimeAiLabBehaviorCardDraft>
            DeduplicateCoreReplacementCards(
                List<CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                CharacterTestRuntimeAiLabDraftWorkspace workspace,
                CharacterTestRuntimeAiLabBehaviorGroupDraft currentGroup)
        {
            var result = new List<CharacterTestRuntimeAiLabBehaviorCardDraft>(cards.Count);
            var behaviorIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorCardDraft card = cards[i];
                string behaviorId = FindBehaviorIdForCard(workspace, card.CardDraftId);
                string dedupeKey = string.IsNullOrWhiteSpace(behaviorId)
                    ? "card:" + card.CardDraftId
                    : "behavior:" + behaviorId;
                if (!behaviorIndexes.TryGetValue(dedupeKey, out int existingIndex))
                {
                    behaviorIndexes.Add(dedupeKey, result.Count);
                    result.Add(card);
                    continue;
                }

                CharacterTestRuntimeAiLabBehaviorCardDraft existing = result[existingIndex];
                if (IsCardInstalled(currentGroup, existing.CardDraftId)
                    && !IsCardInstalled(currentGroup, card.CardDraftId))
                {
                    result[existingIndex] = card;
                }
            }
            return result;
        }

        private static bool IsCardInstalled(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            string cardDraftId)
        {
            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (string.Equals(
                        group.Entries[i].CardDraftId,
                        cardDraftId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string FindBehaviorIdForCard(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string cardDraftId)
        {
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in workspace.Groups.Values)
            {
                for (int i = 0; i < group.Entries.Count; i++)
                {
                    if (string.Equals(
                        group.Entries[i].CardDraftId,
                        cardDraftId,
                        StringComparison.Ordinal))
                    {
                        return group.Entries[i].BehaviorId;
                    }
                }
            }
            return string.Empty;
        }

        private static int CompareWorkbenchLibraryCard(
            CharacterTestRuntimeAiLabBehaviorCardDraft left,
            CharacterTestRuntimeAiLabBehaviorCardDraft right)
        {
            int displayName = string.Compare(
                left?.DisplayName,
                right?.DisplayName,
                StringComparison.Ordinal);
            return displayName != 0
                ? displayName
                : string.Compare(
                    left?.CardDraftId,
                    right?.CardDraftId,
                    StringComparison.Ordinal);
        }

    }
}
