using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder
    {
        private static CharacterTestFairyGuiBehaviorDeckCardViewModel[] BuildLibraryCards(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            int pageIndex,
            string replacementSourceStableCardId,
            IMxUiTextProvider textProvider,
            bool canAuthorInLab,
            CharacterTestBehaviorDeckOperationFeedback feedback)
        {
            var cards = new CharacterTestFairyGuiBehaviorDeckCardViewModel[
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount];
            int start = pageIndex * cards.Length;
            int replacementSourceCost = ResolveCardCost(source.Cards, replacementSourceStableCardId);
            for (int i = 0; i < cards.Length; i++)
            {
                int sourceIndex = start + i;
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card =
                    sourceIndex < source.Cards.Count ? source.Cards[sourceIndex] : null;
                bool replacementFitsBudget = card == null
                    || string.IsNullOrEmpty(replacementSourceStableCardId)
                    || source.KnownCost - replacementSourceCost + card.Cost <= source.MaximumCost;
                cards[i] = card != null
                    ? BuildCard(
                        card,
                        replacementSourceStableCardId,
                        false,
                        -1,
                        replacementFitsBudget,
                        textProvider,
                        canAuthorInLab,
                        source,
                        feedback)
                    : EmptyCard();
            }
            return cards;
        }

        private static CharacterTestFairyGuiBehaviorDeckCardViewModel[] BuildDeckCards(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> source,
            bool sessionLocked,
            string replacementSourceStableCardId,
            IMxUiTextProvider textProvider,
            bool canAuthorInLab,
            CharacterTestBehaviorDeckOperationFeedback feedback)
        {
            var cards = new CharacterTestFairyGuiBehaviorDeckCardViewModel[
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCardCount];
            int target = 0;
            for (int i = 0; i < source.Count && target < cards.Length; i++)
            {
                if (!source[i].IsInstalled)
                    continue;
                cards[target] = BuildCard(
                    source[i],
                    replacementSourceStableCardId,
                    true,
                    target,
                    true,
                    textProvider,
                    canAuthorInLab,
                    null,
                    feedback);
                target++;
            }
            string emptyLabel = sessionLocked
                ? L(textProvider, "ui.character_test.behavior_deck.slot_locked", "已锁定")
                : L(textProvider, "ui.character_test.behavior_deck.slot_empty", "空卡槽");
            while (target < cards.Length)
            {
                cards[target] = EmptyCard(emptyLabel, target);
                target++;
            }
            return cards;
        }

        private static CharacterTestFairyGuiBehaviorDeckCardViewModel BuildCard(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            string replacementSourceStableCardId,
            bool isDeckSlot,
            int slotIndex,
            bool replacementFitsBudget,
            IMxUiTextProvider textProvider,
            bool canAuthorInLab,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            CharacterTestBehaviorDeckOperationFeedback feedback)
        {
            CharacterTestFairyGuiBehaviorDeckCardState state = ResolveState(card);
            bool choosingReplacement = !string.IsNullOrEmpty(replacementSourceStableCardId);
            bool actionEnabled;
            string actionText;
            CharacterTestFairyGuiBehaviorDeckCardAction action;
            CharacterTestFairyGuiBehaviorDeckCardAction secondaryAction =
                CharacterTestFairyGuiBehaviorDeckCardAction.None;
            string secondaryActionText = string.Empty;
            bool secondaryActionVisible = false;
            bool secondaryActionEnabled = false;
            string blockReasonOverride = string.Empty;
            if (choosingReplacement)
            {
                if (string.Equals(card.StableCardId, replacementSourceStableCardId, StringComparison.Ordinal))
                {
                    action = CharacterTestFairyGuiBehaviorDeckCardAction.CancelReplacement;
                    actionEnabled = true;
                    actionText = L(textProvider, "ui.character_test.behavior_deck.action.cancel_replace", "取消替换");
                    secondaryAction = card.CanRemove
                        ? CharacterTestFairyGuiBehaviorDeckCardAction.Remove
                        : CharacterTestFairyGuiBehaviorDeckCardAction.None;
                    secondaryActionVisible = card.CanRemove;
                    secondaryActionEnabled = card.CanRemove;
                    secondaryActionText = card.CanRemove
                        ? L(textProvider, "ui.character_test.behavior_deck.action.remove", "移除")
                        : string.Empty;
                }
                else if (isDeckSlot)
                {
                    action = CharacterTestFairyGuiBehaviorDeckCardAction.None;
                    actionEnabled = false;
                    actionText = L(
                        textProvider,
                        "ui.character_test.behavior_deck.action.replacement_active",
                        "替换中");
                    blockReasonOverride = L(
                        textProvider,
                        "ui.character_test.behavior_deck.block.finish_replacement",
                        "请先完成或取消当前替换");
                }
                else
                {
                    action = card.CanUseAsReplacement && replacementFitsBudget
                        ? CharacterTestFairyGuiBehaviorDeckCardAction.ChooseReplacement
                        : CharacterTestFairyGuiBehaviorDeckCardAction.None;
                    actionEnabled = card.CanUseAsReplacement && replacementFitsBudget;
                    actionText = actionEnabled
                        ? L(textProvider, "ui.character_test.behavior_deck.action.replace_with", "替换为此卡")
                        : BlockReasonText(
                            replacementFitsBudget
                                ? card.BlockReason
                                : CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.CostLimit,
                            textProvider);
                    if (!actionEnabled)
                        blockReasonOverride = actionText;
                }
            }
            else if (card.IsInstalled)
            {
                action = card.CanRemove
                    ? CharacterTestFairyGuiBehaviorDeckCardAction.Remove
                    : CharacterTestFairyGuiBehaviorDeckCardAction.None;
                actionEnabled = card.CanRemove;
                actionText = L(textProvider, "ui.character_test.behavior_deck.action.remove", "移除");
                secondaryAction = card.CanReplace
                    ? CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement
                    : CharacterTestFairyGuiBehaviorDeckCardAction.None;
                secondaryActionVisible = card.CanReplace;
                secondaryActionEnabled = card.CanReplace;
                secondaryActionText = card.CanReplace
                    ? L(textProvider, "ui.character_test.behavior_deck.action.choose_replace", "修复 / 替换")
                    : string.Empty;
            }
            else
            {
                action = card.CanInstall
                    ? CharacterTestFairyGuiBehaviorDeckCardAction.Install
                    : CharacterTestFairyGuiBehaviorDeckCardAction.None;
                actionEnabled = card.CanInstall;
                actionText = L(textProvider, "ui.character_test.behavior_deck.action.install", "安装");
            }

            bool isFeedbackTarget = feedback.Visible
                && string.Equals(feedback.StableCardId, card.StableCardId, StringComparison.Ordinal);

            string fullDescription = string.IsNullOrEmpty(card.ShortDescription)
                ? L(textProvider, "ui.character_test.behavior_deck.unspecified", "未说明")
                : card.ShortDescription;
            string fullUse = L(textProvider, "ui.character_test.behavior_deck.use", "用途：")
                + (string.IsNullOrEmpty(card.UseSummary)
                    ? L(textProvider, "ui.character_test.behavior_deck.unspecified", "未说明")
                    : card.UseSummary);
            string fullRisk = L(textProvider, "ui.character_test.behavior_deck.risk", "风险：")
                + (string.IsNullOrEmpty(card.RiskSummary)
                    ? L(textProvider, "ui.character_test.behavior_deck.unspecified", "未说明")
                    : card.RiskSummary);

            var viewModel = new CharacterTestFairyGuiBehaviorDeckCardViewModel
            {
                StableCardId = card.StableCardId,
                Title = ResolvePlayerCardTitle(card, textProvider),
                CostText = L(textProvider, "ui.character_test.behavior_deck.cost", "费用") + " "
                    + card.Cost.ToString(CultureInfo.InvariantCulture),
                StateText = StateText(state, textProvider),
                TagsText = JoinTags(card.Tags),
                DescriptionText = FormatCardSummaryLine(fullDescription, 34),
                UseText = FormatCardSummaryLine(fullUse, 34),
                RiskText = FormatCardSummaryLine(fullRisk, 34),
                DetailsTooltipText = fullDescription + "\n" + fullUse + "\n" + fullRisk,
                ActionText = actionText,
                SecondaryActionText = secondaryActionText,
                SlotText = isDeckSlot && slotIndex >= 0
                    ? L(textProvider, "ui.character_test.behavior_deck.slot", "槽位 ")
                        + (slotIndex + 1).ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                BlockReasonText = actionEnabled
                    ? string.Empty
                    : !string.IsNullOrEmpty(blockReasonOverride)
                        ? blockReasonOverride
                        : FormatBlockReason(card, source, textProvider),
                InlineFeedbackText = isFeedbackTarget
                    ? FormatCardFeedback(feedback, textProvider)
                    : string.Empty,
                State = state,
                Action = action,
                SecondaryAction = secondaryAction,
                FeedbackTone = isFeedbackTarget
                    ? ResolveFeedbackTone(feedback)
                    : CharacterTestFairyGuiBehaviorDeckFeedbackTone.None,
                Icon = ResolveCardIcon(card.Tags),
                Visible = true,
                ActionEnabled = actionEnabled,
                SecondaryActionVisible = secondaryActionVisible,
                SecondaryActionEnabled = secondaryActionEnabled,
                IsOperationTarget = isFeedbackTarget,
                CanEditInLab = canAuthorInLab
                    && state != CharacterTestFairyGuiBehaviorDeckCardState.Unavailable
                    && state != CharacterTestFairyGuiBehaviorDeckCardState.Incompatible
            };
            viewModel.CostText = card.Cost.ToString(CultureInfo.InvariantCulture);
            return viewModel;
        }

        private static string FormatCardSummaryLine(string value, int maximumVisualUnits)
        {
            value = value ?? string.Empty;
            int units = 0;
            int cutIndex = value.Length;
            int lastWhitespace = -1;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsWhiteSpace(character))
                    lastWhitespace = i;
                units += character <= 0x7f ? 1 : 2;
                if (units <= maximumVisualUnits)
                    continue;
                cutIndex = i;
                break;
            }
            if (cutIndex >= value.Length)
                return value;
            if (lastWhitespace >= maximumVisualUnits / 2)
                cutIndex = lastWhitespace;
            return value.Substring(0, cutIndex).TrimEnd() + "…";
        }

        private static CharacterTestFairyGuiBehaviorDeckCardState ResolveState(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card)
        {
            if (card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable)
                return CharacterTestFairyGuiBehaviorDeckCardState.Unavailable;
            if (card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible)
                return CharacterTestFairyGuiBehaviorDeckCardState.Incompatible;
            if (card.IsInstalled)
                return CharacterTestFairyGuiBehaviorDeckCardState.Installed;
            return card.IsUnlocked
                ? CharacterTestFairyGuiBehaviorDeckCardState.Available
                : CharacterTestFairyGuiBehaviorDeckCardState.Locked;
        }

        private static string ResolvePlayerCardTitle(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            IMxUiTextProvider textProvider)
        {
            if (!string.IsNullOrWhiteSpace(card.DisplayName))
                return card.DisplayName;
            return card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible
                ? L(textProvider, "ui.character_test.behavior_deck.card.incompatible", "不兼容行为卡")
                : L(textProvider, "ui.character_test.behavior_deck.card.unavailable", "未知行为卡");
        }

        private static int ResolveCardCost(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> cards,
            string stableCardId)
        {
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                if (string.Equals(cards[i].StableCardId, stableCardId, StringComparison.Ordinal))
                    return cards[i].Cost;
            }
            return 0;
        }

        private static CharacterTestFairyGuiBehaviorDeckCardViewModel EmptyCard(string label = "", int slotIndex = -1)
        {
            return new CharacterTestFairyGuiBehaviorDeckCardViewModel
            {
                Title = label,
                SlotText = slotIndex >= 0
                    ? "槽位 " + (slotIndex + 1).ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                State = CharacterTestFairyGuiBehaviorDeckCardState.Empty,
                Visible = !string.IsNullOrEmpty(label),
                ActionEnabled = false
            };
        }

    }
}
