using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelBinder
    {
        private void ConfigureCardFocus(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            _cardFocusPaths.Clear();
            bool detailsOpened = viewModel.DetailsVisible && !_detailsWasVisible;
            bool presetOpened = viewModel.PresetPreviewVisible && !_presetWasVisible;
            _detailsWasVisible = viewModel.DetailsVisible;
            _presetWasVisible = viewModel.PresetPreviewVisible;
            if (viewModel.PresetPreviewVisible)
            {
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset);
                if (viewModel.CanConfirmRecommendedPreset)
                    _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.ConfirmRecommendedPreset);
                _focusNavigationMetadata = new MxFramework.UI.FairyGui.MxFairyGuiFocusNavigationMetadata(
                    _viewId,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset,
                    _cardFocusPaths);
                MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.Configure(root, _focusNavigationMetadata);
                _focusRoot = root;
                if (presetOpened)
                    MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
                return;
            }
            if (viewModel.DetailsVisible)
            {
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails);
                if (viewModel.CanPreviousDetailsPage)
                    _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousDetailsPage);
                if (viewModel.CanNextDetailsPage)
                    _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.NextDetailsPage);
                _focusNavigationMetadata = new MxFramework.UI.FairyGui.MxFairyGuiFocusNavigationMetadata(
                    _viewId,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails,
                    _cardFocusPaths);
                MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.Configure(root, _focusNavigationMetadata);
                _focusRoot = root;
                if (detailsOpened)
                    MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
                return;
            }
            AddCardFocusPaths(
                _cardFocusPaths,
                viewModel.LibraryCards,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount,
                CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard,
                includeDetails: true);
            AddCardFocusPaths(
                _cardFocusPaths,
                viewModel.DeckCards,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCardCount,
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCard,
                includeDetails: false);
            if (!viewModel.InstallSelectionActive)
            {
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedEdit);
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.SelectedAction);
            }
            if (viewModel.ShowAddCard)
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.AddCard);
            if (viewModel.CanPreviousPage)
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage);
            if (viewModel.CanNextPage)
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage);
            _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader);
            _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.Close);
            _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter);
            _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter);
            if (viewModel.CanPreviewRecommendedPreset)
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset);
            if (viewModel.CanUndoRecommendedPreset)
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset);
            if (viewModel.CanContinueToSetup)
                _cardFocusPaths.Add(CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup);

            string defaultFocus = _cardFocusPaths.Count > 0
                ? _cardFocusPaths[0]
                : CharacterTestFairyGuiBehaviorDeckPanelIds.Close;
            _focusNavigationMetadata = new MxFramework.UI.FairyGui.MxFairyGuiFocusNavigationMetadata(
                _viewId,
                defaultFocus,
                _cardFocusPaths);
            MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.Configure(
                root,
                _focusNavigationMetadata);
            bool newRoot = !ReferenceEquals(_focusRoot, root);
            _focusRoot = root;
            if (viewModel.PresetExitFocusSequence > _lastAppliedPresetExitFocusSequence)
            {
                _lastAppliedPresetExitFocusSequence = viewModel.PresetExitFocusSequence;
                Fgui.GObject target = ResolveFocusTarget(root, viewModel.PresetExitFocusTargetId);
                if (target != null && target.visible && target.enabled)
                    target.RequestFocus(true);
                else
                    MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
            }
            else if (viewModel.FocusRequestSequence > _lastAppliedFocusRequestSequence)
            {
                _lastAppliedFocusRequestSequence = viewModel.FocusRequestSequence;
                string intentPath = ResolveFocusIntentPath(viewModel);
                Fgui.GObject target = ResolveFocusTarget(root, intentPath);
                if (target != null && target.visible && target.enabled)
                    target.RequestFocus(true);
                else
                    MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
            }
            else if (newRoot)
            {
                MxFramework.UI.FairyGui.MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
            }
        }

        private static void AddCardFocusPaths(
            List<string> paths,
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards,
            int count,
            System.Func<int, string> resolveName,
            bool includeDetails)
        {
            for (int i = 0; i < count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card =
                    cards != null && i < cards.Count ? cards[i] : null;
                if (card == null || !card.Visible)
                    continue;
                if (card.State != CharacterTestFairyGuiBehaviorDeckCardState.Empty)
                    paths.Add(resolveName(i));
            }
        }

        private static string ResolveFocusIntentPath(
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            if (viewModel == null || string.IsNullOrEmpty(viewModel.FocusStableCardId))
                return CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader;

            if (viewModel.FocusAction == CharacterTestFairyGuiBehaviorDeckCardAction.Select)
                return FindCardRootPath(viewModel, viewModel.FocusStableCardId);

            if (viewModel.FocusAction == CharacterTestFairyGuiBehaviorDeckCardAction.ShowDetails)
            {
                for (int i = 0; viewModel.LibraryCards != null && i < viewModel.LibraryCards.Count; i++)
                {
                    CharacterTestFairyGuiBehaviorDeckCardViewModel card = viewModel.LibraryCards[i];
                    if (card != null && card.Visible
                        && string.Equals(card.StableCardId, viewModel.FocusStableCardId, System.StringComparison.Ordinal))
                    {
                        return CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard(i) + "/btnDetails";
                    }
                }
                return CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader;
            }

            bool removedFromSlot = viewModel.FocusOrigin == CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot
                && viewModel.FocusAction == CharacterTestFairyGuiBehaviorDeckCardAction.Remove;
            if (!removedFromSlot)
            {
                IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> originCards =
                    viewModel.FocusOrigin == CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot
                        ? viewModel.DeckCards
                        : viewModel.LibraryCards;
                string originPath = FindCardActionPath(
                    originCards,
                    viewModel.FocusOrigin == CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot,
                    viewModel.FocusStableCardId,
                    viewModel.FocusAction == CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement
                        ? CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                        : viewModel.FocusActionRole);
                if (!string.IsNullOrEmpty(originPath))
                    return originPath;
            }

            string libraryPath = FindCardActionPath(
                viewModel.LibraryCards,
                false,
                viewModel.FocusStableCardId,
                CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary);
            return string.IsNullOrEmpty(libraryPath)
                ? CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader
                : libraryPath;
        }

        private static string FindCardRootPath(
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel,
            string stableCardId)
        {
            for (int i = 0; viewModel.LibraryCards != null && i < viewModel.LibraryCards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = viewModel.LibraryCards[i];
                if (card != null
                    && card.Visible
                    && string.Equals(card.StableCardId, stableCardId, System.StringComparison.Ordinal))
                {
                    return CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard(i);
                }
            }
            for (int i = 0; viewModel.DeckCards != null && i < viewModel.DeckCards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = viewModel.DeckCards[i];
                if (card != null
                    && card.Visible
                    && string.Equals(card.StableCardId, stableCardId, System.StringComparison.Ordinal))
                {
                    return CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCard(i);
                }
            }
            return CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader;
        }

        internal static string ResolveFocusIntentPathForTests(
            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel) =>
            ResolveFocusIntentPath(viewModel);

        private static string FindCardActionPath(
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards,
            bool deck,
            string stableCardId,
            CharacterTestFairyGuiBehaviorDeckCardActionRole role)
        {
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = cards[i];
                if (card == null || !card.Visible
                    || !string.Equals(card.StableCardId, stableCardId, System.StringComparison.Ordinal))
                {
                    continue;
                }
                string cardPath = deck
                    ? CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCard(i)
                    : CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard(i);
                if (role == CharacterTestFairyGuiBehaviorDeckCardActionRole.Secondary
                    && card.SecondaryActionVisible
                    && card.SecondaryActionEnabled)
                {
                    return cardPath + "/btnSecondaryAction";
                }
                if (card.ActionEnabled)
                    return cardPath + "/btnAction";
                return string.Empty;
            }
            return string.Empty;
        }

        private static Fgui.GObject ResolveFocusTarget(Fgui.GComponent root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;
            int separator = path.IndexOf('/');
            if (separator < 0)
                return root.GetChild(path);
            Fgui.GComponent parent = root.GetChild(path.Substring(0, separator))?.asCom;
            return parent?.GetChild(path.Substring(separator + 1));
        }

    }
}
