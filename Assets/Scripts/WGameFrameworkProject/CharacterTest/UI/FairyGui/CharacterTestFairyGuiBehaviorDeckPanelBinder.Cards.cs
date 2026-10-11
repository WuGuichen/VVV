using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelBinder
    {
        private void BindCards(
            Fgui.GComponent root,
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards,
            int count,
            System.Func<int, string> resolveName,
            CharacterTestFairyGuiBehaviorDeckCardOrigin origin,
            string selectedStableCardId,
            bool installSelectionActive)
        {
            for (int i = 0; i < count; i++)
            {
                Fgui.GComponent visual = root.GetChild(resolveName(i))?.asCom;
                if (visual == null)
                    continue;
                CharacterTestFairyGuiBehaviorDeckCardViewModel card =
                    cards != null && i < cards.Count ? cards[i] : null;
                BindCard(
                    visual,
                    card,
                    origin,
                    i,
                    string.Equals(
                        card?.StableCardId,
                        selectedStableCardId,
                        System.StringComparison.Ordinal),
                    installSelectionActive);
            }
        }

        private void BindCard(
            Fgui.GComponent visual,
            CharacterTestFairyGuiBehaviorDeckCardViewModel card,
            CharacterTestFairyGuiBehaviorDeckCardOrigin origin,
            int slotIndex,
            bool selected,
            bool installSelectionActive)
        {
            bool visible = card != null && card.Visible;
            bool empty = card != null
                && card.State == CharacterTestFairyGuiBehaviorDeckCardState.Empty;
            visual.visible = visible && !empty;
            if (!visible)
            {
                BindTooltip(visual, string.Empty);
                return;
            }
            if (empty)
            {
                BindTooltip(visual, string.Empty);
                return;
            }

            BindCardIcon(visual, card.Icon, empty);
            BindCardText(visual, "title", empty ? string.Empty : card.Title);
            BindCardText(visual, "cost", card.CostText);
            BindCardText(visual, "state", card.StateText);
            BindCardText(visual, "tags", card.TagsText);
            BindCardText(visual, "description", card.DescriptionText);
            BindCardText(visual, "use", card.UseText);
            BindCardText(visual, "risk", card.RiskText);
            BindTooltip(visual, card.DetailsTooltipText);
            SetCardTextTooltip(visual, "description", card.DetailsTooltipText);
            SetCardTextTooltip(visual, "use", card.DetailsTooltipText);
            SetCardTextTooltip(visual, "risk", card.DetailsTooltipText);
            BindCardText(visual, "slot", string.Empty);
            BindCardText(visual, "blockReason", card.BlockReasonText);
            BindCardText(visual, "inlineFeedback", card.InlineFeedbackText);
            BindCardStatePresentation(visual, card.State, installSelectionActive, selected);
            SetVisible(visual, "state", false);
            SetVisible(visual, "slot", false);
            SetVisible(visual, "use", false);
            SetVisible(visual, "risk", false);
            SetVisible(visual, "cost", !empty);
            SetVisible(visual, "costIcon", !empty);
            SetVisible(visual, "tags", !empty);
            SetVisible(visual, "description", !empty);
            SetVisible(visual, "blockReason", !empty && !string.IsNullOrEmpty(card.BlockReasonText));
            SetVisible(visual, "selectionTop", selected);
            SetVisible(visual, "selectionRight", selected);
            SetVisible(visual, "selectionBottom", selected);
            SetVisible(visual, "selectionLeft", selected);
            SetFeedbackColor(
                visual.GetChild("feedbackBackground")?.asGraph,
                card.FeedbackTone,
                card.IsOperationTarget);

            Fgui.GButton action = visual.GetChild("btnAction")?.asButton;
            string stableCardId = card.StableCardId;
            if (action != null)
            {
                action.visible = false;
                action.text = card.ActionText ?? string.Empty;
                action.enabled = card.ActionEnabled;
                action.grayed = !card.ActionEnabled;
                CharacterTestFairyGuiBehaviorDeckCardAction cardAction = card.Action;
                bool enabled = card.ActionEnabled;
                action.onClick.Set(() =>
                {
                    if (!enabled)
                        return;
                    EnqueueCardAction(
                        stableCardId,
                        cardAction,
                        origin,
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
                        slotIndex);
                });
            }

            Fgui.GButton secondaryAction = visual.GetChild("btnSecondaryAction")?.asButton;
            if (secondaryAction != null)
            {
                secondaryAction.visible = false;
                secondaryAction.text = card.SecondaryActionText ?? string.Empty;
                secondaryAction.enabled = card.SecondaryActionEnabled;
                secondaryAction.grayed = !card.SecondaryActionEnabled;
                CharacterTestFairyGuiBehaviorDeckCardAction secondaryCardAction = card.SecondaryAction;
                bool secondaryEnabled = card.SecondaryActionEnabled;
                secondaryAction.onClick.Set(() =>
                {
                    if (!secondaryEnabled)
                        return;
                    EnqueueCardAction(
                        stableCardId,
                        secondaryCardAction,
                        origin,
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.Secondary,
                        slotIndex);
                });
            }

            Fgui.GButton edit = visual.GetChild("btnLabEdit")?.asButton;
            if (edit != null)
            {
                edit.visible = false;
                edit.enabled = card.CanEditInLab;
                edit.grayed = !card.CanEditInLab;
                edit.text = "Lab 编辑";
                edit.onClick.Set(() =>
                    EnqueueCardAction(
                        stableCardId,
                        CharacterTestFairyGuiBehaviorDeckCardAction.EditInLab,
                        origin,
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.LabEdit,
                        slotIndex));
            }

            Fgui.GButton details = visual.GetChild("btnDetails")?.asButton;
            if (details != null)
            {
                details.visible = false;
                details.enabled = details.visible;
                details.grayed = false;
                details.text = L("ui.character_test.behavior_deck.details", "详情");
                details.onClick.Set(() =>
                    EnqueueCardAction(
                        stableCardId,
                        CharacterTestFairyGuiBehaviorDeckCardAction.ShowDetails,
                        CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
                        slotIndex));
            }
            visual.focusable = card.State != CharacterTestFairyGuiBehaviorDeckCardState.Empty;
            visual.onClick.Set(() =>
            {
                if (card.State == CharacterTestFairyGuiBehaviorDeckCardState.Empty)
                    return;
                CharacterTestFairyGuiBehaviorDeckCardAction clickAction =
                    installSelectionActive
                    && origin == CharacterTestFairyGuiBehaviorDeckCardOrigin.Library
                    && card.Action == CharacterTestFairyGuiBehaviorDeckCardAction.Install
                    && card.ActionEnabled
                        ? CharacterTestFairyGuiBehaviorDeckCardAction.Install
                        : CharacterTestFairyGuiBehaviorDeckCardAction.Select;
                EnqueueCardAction(
                    stableCardId,
                    clickAction,
                    origin,
                    CharacterTestFairyGuiBehaviorDeckCardActionRole.None,
                    slotIndex);
            });
        }

        private void BindAddCardButton(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            Fgui.GButton button =
                root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.AddCard)?.asButton;
            if (button == null)
                return;

            bool visible = viewModel.ShowAddCard
                && !viewModel.DetailsVisible
                && !viewModel.PresetPreviewVisible;
            button.visible = visible;
            if (!visible)
            {
                BindTooltip(button, string.Empty);
                return;
            }

            int installedCount = CountInstalledCards(viewModel.DeckCards);
            button.y = 154f + installedCount * 96f;
            button.text = viewModel.InstallSelectionActive
                ? L("ui.character_test.behavior_deck.cancel_add", "取消添加")
                : L("ui.character_test.behavior_deck.add_card", "添加卡牌");
            button.icon = Fgui.UIPackage.GetItemURL(
                CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId,
                viewModel.InstallSelectionActive
                    ? "menu_close_32x32"
                    : "action_add_32x32");
            CenterButtonContent(button, 10f);
            BindTooltip(
                button,
                viewModel.InstallSelectionActive
                    ? L("ui.character_test.behavior_deck.cancel_add_tooltip", "退出添加卡牌模式")
                    : L("ui.character_test.behavior_deck.add_card_tooltip", "从可用卡牌中选择一张安装"));
            button.enabled = true;
            button.grayed = false;
            button.onClick.Set(() => _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ToggleInstallSelection,
                null)));
        }

        private static void CenterButtonContent(Fgui.GButton button, float gap)
        {
            Fgui.GObject icon = button?.GetChild("icon");
            Fgui.GTextField title = button?.GetChild("title")?.asTextField;
            if (icon == null || title == null)
                return;

            float availableTextWidth = Mathf.Max(0f, button.width - icon.width - gap - 24f);
            float textWidth = Mathf.Min(Mathf.Ceil(title.textWidth) + 2f, availableTextWidth);
            float groupWidth = icon.width + gap + textWidth;
            icon.x = Mathf.Round((button.width - groupWidth) * 0.5f);
            icon.y = Mathf.Round((button.height - icon.height) * 0.5f);
            title.x = icon.x + icon.width + gap;
            title.y = 0f;
            title.width = textWidth;
            title.height = button.height;
        }

        private void BindSelectionStrip(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            CharacterTestFairyGuiBehaviorDeckCardViewModel selected =
                FindCard(viewModel.LibraryCards, viewModel.SelectedStableCardId)
                ?? FindCard(viewModel.DeckCards, viewModel.SelectedStableCardId);
            bool visible = selected != null
                && selected.State != CharacterTestFairyGuiBehaviorDeckCardState.Empty
                && !viewModel.DetailsVisible
                && !viewModel.PresetPreviewVisible;
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedBand, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedTitle, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedTags, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedSummary, visible);
            SetVisible(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedCost, visible);
            SetVisible(root, "selectedCostIcon", visible);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedEdit,
                visible && !viewModel.InstallSelectionActive);
            SetVisible(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedAction,
                visible && !viewModel.InstallSelectionActive);
            BindSelectedCardIcon(
                root,
                selected != null
                    ? selected.Icon
                    : CharacterTestFairyGuiBehaviorDeckCardIcon.Fallback,
                visible);
            if (!visible)
                return;

            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedTitle, selected.Title);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedTags, selected.TagsText);
            BindCardText(
                root,
                CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedSummary,
                string.IsNullOrEmpty(selected.BlockReasonText)
                    ? selected.DescriptionText
                    : selected.BlockReasonText);
            BindCardText(root, CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedCost, selected.CostText);
            Fgui.GObject selectedBand =
                root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedBand);
            if (selectedBand != null)
                BindTooltip(selectedBand, selected.DetailsTooltipText);

            Fgui.GButton edit =
                root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedEdit)?.asButton;
            if (edit != null)
            {
                edit.text = string.Empty;
                BindTooltip(
                    edit,
                    L("ui.character_test.behavior_deck.lab_edit", "Lab 编辑"));
                edit.enabled = selected.CanEditInLab;
                edit.grayed = !selected.CanEditInLab;
                string stableCardId = selected.StableCardId;
                edit.onClick.Set(() =>
                    EnqueueCardAction(
                        stableCardId,
                        CharacterTestFairyGuiBehaviorDeckCardAction.EditInLab,
                        CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.LabEdit));
            }

            Fgui.GButton action =
                root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedAction)?.asButton;
            if (action != null)
            {
                action.text = string.Empty;
                action.icon = Fgui.UIPackage.GetItemURL(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId,
                    selected.Action == CharacterTestFairyGuiBehaviorDeckCardAction.Remove
                        ? "action_remove_32x32"
                        : "action_add_32x32");
                BindTooltip(
                    action,
                    selected.Action == CharacterTestFairyGuiBehaviorDeckCardAction.Remove
                        ? L("ui.character_test.behavior_deck.remove", "移出卡组")
                        : selected.ActionText ?? string.Empty);
                action.enabled = selected.ActionEnabled;
                action.grayed = !selected.ActionEnabled;
                string stableCardId = selected.StableCardId;
                CharacterTestFairyGuiBehaviorDeckCardAction selectedAction = selected.Action;
                action.onClick.Set(() =>
                {
                    if (!action.enabled)
                        return;
                    EnqueueCardAction(
                        stableCardId,
                        selectedAction,
                        CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary);
                });
            }
        }

        private static CharacterTestFairyGuiBehaviorDeckCardViewModel FindCard(
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards,
            string stableCardId)
        {
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = cards[i];
                if (card != null
                    && card.Visible
                    && string.Equals(
                        card.StableCardId,
                        stableCardId,
                        System.StringComparison.Ordinal))
                {
                    return card;
                }
            }
            return null;
        }

    }
}
