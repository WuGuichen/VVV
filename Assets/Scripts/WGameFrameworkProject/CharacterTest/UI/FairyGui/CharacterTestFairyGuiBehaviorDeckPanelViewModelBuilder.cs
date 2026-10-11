using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder
    {
        private const int DetailsCharactersPerLine = 46;
        private const int DetailsLinesPerPage = 8;

        public static CharacterTestFairyGuiBehaviorDeckPanelViewModel Build(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            int pageIndex,
            string replacementSourceStableCardId,
            string commandStatus,
            IMxUiTextProvider textProvider = null,
            bool canPreviousCharacter = false,
            bool canNextCharacter = false,
            int selectedCharacterIndex = 0,
            int characterCount = 1,
            string characterDisplayName = "",
            bool canPreviewRecommendedPreset = false,
            bool canUndoRecommendedPreset = false,
            bool canAuthorInLab = false,
            bool canContinueToSetup = false,
            CharacterTestBehaviorDeckOperationFeedback feedback = default,
            CharacterTestBehaviorDeckFocusIntent focusIntent = default,
            long feedbackVersion = 0,
            string detailsStableCardId = "",
            int detailsPageIndex = 0,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview presetPreview = null,
            long presetExitFocusSequence = 0,
            string presetExitFocusTargetId = "",
            bool installSelectionActive = false,
            bool hasActiveRuntimeAiLabSession = false)
        {
            if (source == null)
            {
                return new CharacterTestFairyGuiBehaviorDeckPanelViewModel
                {
                    Title = L(textProvider, "ui.character_test.behavior_deck.title", "行为卡组"),
                    LockStatusText = L(textProvider, "ui.character_test.behavior_deck.unavailable", "行为卡组不可用"),
                    DiagnosticsText = L(textProvider, "ui.character_test.behavior_deck.no_data", "没有可用的卡组数据")
                };
            }

            int cardCount = source.Cards.Count;
            int pageCount = Math.Max(1, (cardCount + CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount - 1)
                / CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount);
            int normalizedPage = Math.Max(0, Math.Min(pageIndex, pageCount - 1));
            bool choosingReplacement = !string.IsNullOrEmpty(replacementSourceStableCardId);
            bool feedbackVisible = feedback.Visible
                && string.Equals(feedback.CharacterStableId, source.CharacterStableId, StringComparison.Ordinal);
            var viewModel = new CharacterTestFairyGuiBehaviorDeckPanelViewModel
            {
                Title = L(textProvider, "ui.character_test.behavior_deck.title", "行为卡组"),
                CharacterText = ResolveCharacterDisplayName(characterDisplayName, source.CharacterStableId),
                BudgetText = "▦ " + source.SlotCount.ToString(CultureInfo.InvariantCulture) + "/"
                    + source.MaximumSlots.ToString(CultureInfo.InvariantCulture) + "   ◉ "
                    + (source.IsKnownCostComplete
                        ? source.KnownCost.ToString(CultureInfo.InvariantCulture)
                        : "已知 " + source.KnownCost.ToString(CultureInfo.InvariantCulture) + " + 未知") + " / "
                    + source.MaximumCost.ToString(CultureInfo.InvariantCulture),
                LockStatusText = source.IsSessionLocked
                    ? L(textProvider, "ui.character_test.behavior_deck.locked", "战斗会话已锁定")
                    : source.RequiresReplacement
                        ? L(textProvider, "ui.character_test.behavior_deck.repair", "卡组需要修复")
                        : L(textProvider, "ui.character_test.behavior_deck.editable", "✓ 合法"),
                LibrarySummaryText = choosingReplacement
                    ? L(textProvider, "ui.character_test.behavior_deck.choose_replacement", "选择一张可用卡替换失效卡")
                    : string.Empty,
                PageStatusText = (normalizedPage + 1).ToString(CultureInfo.InvariantCulture) + " / "
                    + pageCount.ToString(CultureInfo.InvariantCulture),
                DeckSummaryText = source.IsResolved
                    ? string.Empty
                    : L(textProvider, "ui.character_test.behavior_deck.deck_unresolved", "失效引用不会进入 Runtime；请替换或移除"),
                DiagnosticsText = FormatDiagnostics(source.DeckDiagnostics, textProvider),
                CommandStatusText = feedbackVisible
                    ? FormatFeedback(feedback, textProvider)
                    : string.IsNullOrWhiteSpace(commandStatus)
                    ? string.Empty
                    : commandStatus,
                BudgetDeltaText = feedbackVisible ? FormatBudgetDelta(feedback) : string.Empty,
                BudgetDeltaTone = feedbackVisible ? ResolveFeedbackTone(feedback) : CharacterTestFairyGuiBehaviorDeckFeedbackTone.None,
                BudgetDeltaVisible = feedbackVisible,
                FeedbackVersion = feedbackVersion,
                FeedbackVisible = feedbackVisible,
                FocusRequestSequence = focusIntent.Sequence,
                FocusOrigin = focusIntent.Origin,
                FocusStableCardId = focusIntent.StableCardId,
                FocusActionRole = focusIntent.ActionRole,
                FocusAction = focusIntent.Action,
                FocusPageIndex = focusIntent.PageIndex,
                CanPreviousPage = normalizedPage > 0,
                CanNextPage = normalizedPage + 1 < pageCount,
                CanPreviousCharacter = canPreviousCharacter,
                CanNextCharacter = canNextCharacter,
                CanPreviewRecommendedPreset = canPreviewRecommendedPreset,
                CanUndoRecommendedPreset = canUndoRecommendedPreset,
                CanContinueToSetup = canContinueToSetup,
                ContinueToSetupLabel = hasActiveRuntimeAiLabSession
                    ? L(textProvider, "ui.character_test.behavior_deck.return_to_debug", "返回调试")
                    : L(textProvider, "ui.character_test.behavior_deck.open_setup", "实验设置")
            };
            viewModel.BudgetText = source.SlotCount.ToString(CultureInfo.InvariantCulture) + "/"
                + source.MaximumSlots.ToString(CultureInfo.InvariantCulture);
            viewModel.CostBudgetText = (source.IsKnownCostComplete
                    ? source.KnownCost.ToString(CultureInfo.InvariantCulture)
                    : source.KnownCost.ToString(CultureInfo.InvariantCulture) + "+?")
                + "/" + source.MaximumCost.ToString(CultureInfo.InvariantCulture);
            viewModel.InstallSelectionActive = installSelectionActive;
            viewModel.ShowAddCard = !source.IsSessionLocked
                && !choosingReplacement
                && source.SlotCount < source.MaximumSlots;
            viewModel.SetLibraryCards(BuildLibraryCards(
                source,
                normalizedPage,
                replacementSourceStableCardId,
                textProvider,
                canAuthorInLab,
                feedbackVisible ? feedback : default));
            viewModel.SetDeckCards(BuildDeckCards(
                source.Cards,
                source.IsSessionLocked,
                replacementSourceStableCardId,
                textProvider,
                canAuthorInLab,
                feedbackVisible ? feedback : default));
            viewModel.SelectedStableCardId = ResolveSelectedStableCardId(
                viewModel,
                focusIntent.StableCardId);
            FillDetails(viewModel, source, detailsStableCardId, detailsPageIndex, textProvider);
            FillPresetPreview(
                viewModel,
                presetPreview,
                presetExitFocusSequence,
                presetExitFocusTargetId,
                textProvider);
            return viewModel;
        }

        private static string ResolveSelectedStableCardId(
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel,
            string requestedStableCardId)
        {
            if (!string.IsNullOrEmpty(requestedStableCardId)
                && ContainsVisibleCard(viewModel.LibraryCards, requestedStableCardId))
            {
                return requestedStableCardId;
            }
            if (!string.IsNullOrEmpty(requestedStableCardId)
                && ContainsVisibleCard(viewModel.DeckCards, requestedStableCardId))
            {
                return requestedStableCardId;
            }
            for (int i = 0; i < viewModel.DeckCards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = viewModel.DeckCards[i];
                if (card != null
                    && card.Visible
                    && card.State != CharacterTestFairyGuiBehaviorDeckCardState.Empty)
                {
                    return card.StableCardId;
                }
            }
            for (int i = 0; i < viewModel.LibraryCards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = viewModel.LibraryCards[i];
                if (card != null && card.Visible)
                    return card.StableCardId;
            }
            return string.Empty;
        }

        private static bool ContainsVisibleCard(
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards,
            string stableCardId)
        {
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = cards[i];
                if (card != null
                    && card.Visible
                    && string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void FillPresetPreview(
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview,
            long exitFocusSequence,
            string exitFocusTargetId,
            IMxUiTextProvider textProvider)
        {
            viewModel.PresetExitFocusSequence = exitFocusSequence;
            viewModel.PresetExitFocusTargetId = exitFocusTargetId ?? string.Empty;
            if (preview == null)
                return;

            viewModel.PresetPreviewVisible = true;
            viewModel.PresetTitleText = L(textProvider, "ui.character_test.behavior_deck.preset.title", "推荐预设差异");
            viewModel.PresetSourceBudgetText = "当前 " + FormatPresetSourceBudget(preview);
            viewModel.PresetCandidateBudgetText = "候选 " + preview.CandidateSlotCount.ToString(CultureInfo.InvariantCulture)
                + " / " + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots.ToString(CultureInfo.InvariantCulture)
                + " 槽 · " + preview.CandidateCost.ToString(CultureInfo.InvariantCulture)
                + " / " + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost.ToString(CultureInfo.InvariantCulture)
                + " 费";
            viewModel.PresetCostDeltaText = preview.DeltaKnown
                ? (preview.CostDelta >= 0 ? "+" : string.Empty)
                    + preview.CostDelta.ToString(CultureInfo.InvariantCulture) + " 费"
                : L(textProvider, "ui.character_test.behavior_deck.preset.delta_unknown", "费用变化无法精确计算");
            viewModel.PresetSummaryText = "+" + preview.Added.Count.ToString(CultureInfo.InvariantCulture)
                + " 新增 · =" + preview.Retained.Count.ToString(CultureInfo.InvariantCulture)
                + " 保留 · -" + preview.Removed.Count.ToString(CultureInfo.InvariantCulture) + " 移除";
            if (!preview.CanConfirm)
            {
                viewModel.PresetSummaryText = L(
                    textProvider,
                    "ui.character_test.behavior_deck.preset.no_changes",
                    "当前卡组已是推荐预设");
            }
            viewModel.PresetDiagnosticsText = FormatDiagnostics(preview.CandidateDiagnostics, textProvider);
            viewModel.CanConfirmRecommendedPreset = preview.CanConfirm;
            viewModel.SetPresetRows(
                BuildPresetRows(preview.Added, "新增", CharacterTestFairyGuiBehaviorDeckFeedbackTone.Success, textProvider),
                BuildPresetRows(preview.Retained, "保留", CharacterTestFairyGuiBehaviorDeckFeedbackTone.None, textProvider),
                BuildPresetRows(preview.Removed, "移除", CharacterTestFairyGuiBehaviorDeckFeedbackTone.Warning, textProvider));
        }

        private static string FormatPresetSourceBudget(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview)
        {
            string cost = preview.SourceBudgetComplete
                ? preview.SourceKnownCost.ToString(CultureInfo.InvariantCulture)
                : "已知 " + preview.SourceKnownCost.ToString(CultureInfo.InvariantCulture) + " + 未知";
            return preview.SourceSlotCount.ToString(CultureInfo.InvariantCulture)
                + " / " + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots.ToString(CultureInfo.InvariantCulture)
                + " 槽 · " + cost
                + " / " + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost.ToString(CultureInfo.InvariantCulture)
                + " 费";
        }

        private static CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] BuildPresetRows(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetDiffEntry> source,
            string stateText,
            CharacterTestFairyGuiBehaviorDeckFeedbackTone tone,
            IMxUiTextProvider textProvider)
        {
            const int RowCount = CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots;
            var rows = new CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[RowCount];
            for (int i = 0; i < rows.Length; i++)
            {
                if (source != null && i < source.Count)
                {
                    CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetDiffEntry entry = source[i];
                    rows[i] = new CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel
                    {
                        StableCardId = entry.StableCardId,
                        TitleText = entry.DisplayName,
                        CostText = entry.CostKnown
                            ? entry.Cost.ToString(CultureInfo.InvariantCulture) + " 费"
                            : L(textProvider, "ui.character_test.behavior_deck.preset.cost_unknown", "费用未知"),
                        StateText = stateText,
                        Tone = tone,
                        Visible = true
                    };
                }
                else
                {
                    rows[i] = new CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel
                    {
                        TitleText = i == 0 && (source == null || source.Count == 0)
                            ? L(textProvider, "ui.character_test.behavior_deck.preset.none", "无")
                            : string.Empty,
                        StateText = i == 0 && (source == null || source.Count == 0) ? stateText : string.Empty,
                        Tone = tone,
                        Visible = i == 0 && (source == null || source.Count == 0)
                    };
                }
            }
            return rows;
        }

        private static void FillDetails(
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            string stableCardId,
            int requestedPageIndex,
            IMxUiTextProvider textProvider)
        {
            if (string.IsNullOrWhiteSpace(stableCardId))
                return;
            stableCardId = stableCardId.Trim();
            for (int i = 0; i < source.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = source.Cards[i];
                if (!string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                    continue;
                string fullText = BuildDetailsText(card, textProvider);
                int normalizedPageIndex;
                int pageCount;
                string page = BuildDetailsPage(
                    fullText,
                    requestedPageIndex,
                    out normalizedPageIndex,
                    out pageCount);
                viewModel.DetailsVisible = true;
                viewModel.DetailsStableCardId = stableCardId;
                viewModel.DetailsTitleText = ResolvePlayerCardTitle(card, textProvider);
                viewModel.DetailsBodyText = page;
                viewModel.DetailsPageIndex = normalizedPageIndex;
                viewModel.DetailsPageCount = pageCount;
                viewModel.DetailsPageStatusText = (normalizedPageIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " / " + pageCount.ToString(CultureInfo.InvariantCulture);
                viewModel.CanPreviousDetailsPage = normalizedPageIndex > 0;
                viewModel.CanNextDetailsPage = normalizedPageIndex + 1 < pageCount;
                return;
            }
        }

        internal static int CountDetailsPages(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            IMxUiTextProvider textProvider)
        {
            return card == null ? 1 : CountDetailsPages(BuildDetailsText(card, textProvider));
        }

        private static string BuildDetailsText(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            IMxUiTextProvider textProvider)
        {
            string unspecified = L(textProvider, "ui.character_test.behavior_deck.unspecified", "未说明");
            string description = string.IsNullOrEmpty(card.ShortDescription)
                ? unspecified
                : card.ShortDescription;
            string use = string.IsNullOrEmpty(card.UseSummary)
                ? unspecified
                : card.UseSummary;
            string risk = string.IsNullOrEmpty(card.RiskSummary)
                ? unspecified
                : card.RiskSummary;
            return description + "\n\n"
                + L(textProvider, "ui.character_test.behavior_deck.use", "用途：") + use + "\n\n"
                + L(textProvider, "ui.character_test.behavior_deck.risk", "风险：") + risk;
        }

        private static string BuildDetailsPage(
            string text,
            int requestedPageIndex,
            out int normalizedPageIndex,
            out int pageCount)
        {
            text = text ?? string.Empty;
            pageCount = CountDetailsPages(text);
            normalizedPageIndex = Math.Max(0, Math.Min(requestedPageIndex, pageCount - 1));
            int pageStart = FindDetailsPageStart(text, normalizedPageIndex);
            int pageEnd = normalizedPageIndex + 1 < pageCount
                ? FindDetailsPageStart(text, normalizedPageIndex + 1)
                : text.Length;
            return text.Substring(pageStart, pageEnd - pageStart);
        }

        private static int CountDetailsPages(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 1;

            int pageCount = 1;
            int lineCount = 0;
            int column = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                if (value == '\r')
                    continue;
                if (value == '\n')
                {
                    lineCount++;
                    column = 0;
                }
                else
                {
                    if (char.IsHighSurrogate(value)
                        && i + 1 < text.Length
                        && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                    }
                    column++;
                    if (column >= DetailsCharactersPerLine)
                    {
                        lineCount++;
                        column = 0;
                    }
                }

                if (lineCount < DetailsLinesPerPage || i + 1 >= text.Length)
                    continue;
                pageCount++;
                lineCount = 0;
                column = 0;
            }
            return pageCount;
        }

        private static int FindDetailsPageStart(string text, int pageIndex)
        {
            if (pageIndex <= 0 || string.IsNullOrEmpty(text))
                return 0;

            int currentPage = 0;
            int lineCount = 0;
            int column = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                if (value == '\r')
                    continue;
                if (value == '\n')
                {
                    lineCount++;
                    column = 0;
                }
                else
                {
                    if (char.IsHighSurrogate(value)
                        && i + 1 < text.Length
                        && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                    }
                    column++;
                    if (column >= DetailsCharactersPerLine)
                    {
                        lineCount++;
                        column = 0;
                    }
                }

                if (lineCount < DetailsLinesPerPage || i + 1 >= text.Length)
                    continue;
                currentPage++;
                if (currentPage == pageIndex)
                    return i + 1;
                lineCount = 0;
                column = 0;
            }
            return text.Length;
        }

        private static string ResolveCharacterDisplayName(string displayName, string stableId)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();
            const string Prefix = "character.definition.";
            return stableId != null && stableId.StartsWith(Prefix, StringComparison.Ordinal)
                ? "角色 " + stableId.Substring(Prefix.Length)
                : "当前角色";
        }

    }
}
