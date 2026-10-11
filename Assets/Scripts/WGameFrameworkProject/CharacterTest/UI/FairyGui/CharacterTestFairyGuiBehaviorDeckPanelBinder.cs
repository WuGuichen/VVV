using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelBinder
    {
        private readonly List<string> _cardFocusPaths = new List<string>(20);
        private readonly MxFairyGuiTooltipService _tooltipService =
            new MxFairyGuiTooltipService();
        private Fgui.GComponent _focusRoot;
        private long _lastAppliedFocusRequestSequence;
        private long _lastAppliedPresetExitFocusSequence;
        private bool _detailsWasVisible;
        private bool _presetWasVisible;

        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            BindCardText(root, "budgetDelta", viewModel.BudgetDeltaText);
            BindCardText(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.CostBudget,
                viewModel.CostBudgetText);
            BindCardText(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryHeader,
                "可用卡牌 · " + CountVisibleCards(viewModel.LibraryCards));
            BindCardText(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader,
                "卡组 · " + CountInstalledCards(viewModel.DeckCards) + "/"
                    + CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibrarySummary,
                !string.IsNullOrEmpty(viewModel.LibrarySummaryText));
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckSummary,
                !string.IsNullOrEmpty(viewModel.DeckSummaryText));
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.CommandStatus,
                !string.IsNullOrEmpty(viewModel.CommandStatusText));
            ConfigureHeaderButtons(root, viewModel);
            ConfigureConditionalNavigationVisibility(root, viewModel);
            Fgui.GTextField budgetDelta = root.GetChild("budgetDelta")?.asTextField;
            if (budgetDelta != null)
            {
                budgetDelta.visible = viewModel.BudgetDeltaVisible;
                budgetDelta.color = FeedbackTextColor(viewModel.BudgetDeltaTone);
            }
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBackdrop, viewModel.DetailsVisible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsTitle, viewModel.DetailsVisible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBody, viewModel.DetailsVisible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsPageStatus, viewModel.DetailsVisible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousDetailsPage, viewModel.DetailsVisible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.NextDetailsPage, viewModel.DetailsVisible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails, viewModel.DetailsVisible);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsTitle, viewModel.DetailsTitleText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBody, viewModel.DetailsBodyText);
            BindPresetPreview(root, viewModel);
            BindCards(
                root,
                viewModel.LibraryCards,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard,
                CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                viewModel.SelectedStableCardId,
                viewModel.InstallSelectionActive);
            BindCards(
                root,
                viewModel.DeckCards,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCardCount,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCard,
                CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot,
                viewModel.SelectedStableCardId,
                installSelectionActive: false);
            BindAddCardButton(root, viewModel);
            BindSelectionStrip(root, viewModel);
            DisableUnderlyingActionsWhileModalIsOpen(
                root,
                viewModel.DetailsVisible || viewModel.PresetPreviewVisible);
            ConfigureCardFocus(root, viewModel);
        }

        private void ConfigureHeaderButtons(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            ConfigureIconButton(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter,
                L("ui.character_test.behavior_deck.previous_character", "Previous character"));
            ConfigureIconButton(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter,
                L("ui.character_test.behavior_deck.next_character", "Next character"));
            ConfigureIconButton(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage,
                L("ui.character_test.behavior_deck.previous_page", "Previous page"));
            ConfigureIconButton(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage,
                L("ui.character_test.behavior_deck.next_page", "Next page"));
            ConfigureIconButton(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.Close,
                L("ui.character_test.behavior_deck.close", "隐藏"));
            ConfigureIconButton(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset,
                L("ui.character_test.behavior_deck.preview_preset", "预览推荐预设"));
            Fgui.GButton continueButton =
                root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup)?.asButton;
            if (continueButton != null)
                continueButton.text = viewModel?.ContinueToSetupLabel ?? "实验设置";
        }

        private void ConfigureIconButton(
            Fgui.GComponent root,
            string childName,
            string tooltip)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.text = string.Empty;
            BindTooltip(button, tooltip);
        }

        private static void ConfigureConditionalNavigationVisibility(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter,
                viewModel.CanPreviousCharacter);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter,
                viewModel.CanNextCharacter);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage,
                viewModel.CanPreviousPage);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage,
                viewModel.CanNextPage);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PageStatus,
                viewModel.CanPreviousPage || viewModel.CanNextPage);
        }

        private static int CountVisibleCards(
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards)
        {
            int count = 0;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].Visible)
                    count++;
            }
            return count;
        }

        private static int CountInstalledCards(
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards)
        {
            int count = 0;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = cards[i];
                if (card != null
                    && card.Visible
                    && card.State != CharacterTestFairyGuiBehaviorDeckCardState.Empty)
                {
                    count++;
                }
            }
            return count;
        }

        private static void DisableUnderlyingActionsWhileModalIsOpen(
            Fgui.GComponent root,
            bool modalVisible)
        {
            if (!modalVisible)
                return;

            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.Close)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset)?.asButton);
            DisableButton(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup)?.asButton);
            DisableCardButtons(root, CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard);
            DisableCardButtons(root, CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCardCount,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCard);
        }

        private static void BindPresetPreview(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            bool visible = viewModel.PresetPreviewVisible;
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetBackdrop, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetPanel, visible);
            SetVisible(root, "presetBudgetArrow", visible);
            SetVisible(root, "presetAddedHeader", visible);
            SetVisible(root, "presetRetainedHeader", visible);
            SetVisible(root, "presetRemovedHeader", visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetTitle, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSummary, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSourceBudget, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCandidateBudget, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCostDelta, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetDiagnostics, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.ConfirmRecommendedPreset, visible);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset,
                viewModel.CanUndoRecommendedPreset && !visible && !viewModel.DetailsVisible);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetTitle, viewModel.PresetTitleText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSummary, viewModel.PresetSummaryText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSourceBudget, viewModel.PresetSourceBudgetText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCandidateBudget, viewModel.PresetCandidateBudgetText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCostDelta, viewModel.PresetCostDeltaText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetDiagnostics, viewModel.PresetDiagnosticsText);
            BindPresetRows(root, viewModel.PresetAddedRows, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetAddedRow, visible);
            BindPresetRows(root, viewModel.PresetRetainedRows, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetRetainedRow, visible);
            BindPresetRows(root, viewModel.PresetRemovedRows, CharacterTestFairyGuiBehaviorDeckPanelIds.PresetRemovedRow, visible);
        }

        private static void BindPresetRows(
            Fgui.GComponent root,
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel> rows,
            System.Func<int, string> resolveName,
            bool modalVisible)
        {
            for (int i = 0; i < CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots; i++)
            {
                Fgui.GComponent row = root.GetChild(resolveName(i))?.asCom;
                if (row == null)
                    continue;
                CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel source =
                    rows != null && i < rows.Count ? rows[i] : null;
                row.visible = modalVisible && source != null && source.Visible;
                if (!row.visible)
                    continue;
                BindCardText(row, "title", source.TitleText);
                BindCardText(row, "cost", source.CostText);
                BindCardText(row, "state", source.StateText);
                Color toneColor = FeedbackTextColor(source.Tone);
                Fgui.GGraph stripe = row.GetChild("stateStripe")?.asGraph;
                if (stripe != null)
                    stripe.color = toneColor;
                Fgui.GTextField state = row.GetChild("state")?.asTextField;
                if (state != null)
                    state.color = toneColor;
            }
        }

        private static void DisableCardButtons(
            Fgui.GComponent root,
            int count,
            System.Func<int, string> resolveName)
        {
            for (int i = 0; i < count; i++)
            {
                Fgui.GComponent card = root.GetChild(resolveName(i))?.asCom;
                if (card == null)
                    continue;
                DisableButton(card.GetChild("btnAction")?.asButton);
                DisableButton(card.GetChild("btnSecondaryAction")?.asButton);
                DisableButton(card.GetChild("btnLabEdit")?.asButton);
                DisableButton(card.GetChild("btnDetails")?.asButton);
            }
        }

        private static void DisableButton(Fgui.GButton button)
        {
            if (button != null)
                button.enabled = false;
        }

    }
}
