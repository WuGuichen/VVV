using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestFairyGuiBehaviorDeckCardState
    {
        Available = 0,
        Installed = 1,
        Locked = 2,
        Unavailable = 3,
        Incompatible = 4,
        Empty = 5
    }

    public enum CharacterTestFairyGuiBehaviorDeckCardAction
    {
        None = 0,
        Install = 1,
        Remove = 2,
        BeginReplacement = 3,
        ChooseReplacement = 4,
        CancelReplacement = 5,
        EditInLab = 6,
        ShowDetails = 7,
        Select = 8
    }

    public enum CharacterTestFairyGuiBehaviorDeckCardOrigin
    {
        Library = 0,
        InstalledSlot = 1
    }

    internal enum CharacterTestFairyGuiBehaviorDeckCardIcon
    {
        Fallback = 0,
        Triangle = 1,
        Square = 2,
        Diamond = 3,
        Circle = 4
    }

    public enum CharacterTestFairyGuiBehaviorDeckCardActionRole
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
        LabEdit = 3
    }

    public enum CharacterTestFairyGuiBehaviorDeckFeedbackTone
    {
        None = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    public sealed class CharacterTestFairyGuiBehaviorDeckCardActionPayload
    {
        public string StableCardId { get; set; } = string.Empty;
        public CharacterTestFairyGuiBehaviorDeckCardAction Action { get; set; }
        public CharacterTestFairyGuiBehaviorDeckCardOrigin Origin { get; set; }
        public CharacterTestFairyGuiBehaviorDeckCardActionRole ActionRole { get; set; }
        public int SlotIndex { get; set; } = -1;
    }

    public sealed class CharacterTestFairyGuiBehaviorDeckCardViewModel
    {
        public string StableCardId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public string DescriptionText { get; set; } = string.Empty;
        public string UseText { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
        public string DetailsTooltipText { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public string SecondaryActionText { get; set; } = string.Empty;
        public string SlotText { get; set; } = string.Empty;
        public string BlockReasonText { get; set; } = string.Empty;
        public string InlineFeedbackText { get; set; } = string.Empty;
        public CharacterTestFairyGuiBehaviorDeckCardState State { get; set; }
        public CharacterTestFairyGuiBehaviorDeckCardAction Action { get; set; }
        public CharacterTestFairyGuiBehaviorDeckCardAction SecondaryAction { get; set; }
        public CharacterTestFairyGuiBehaviorDeckFeedbackTone FeedbackTone { get; set; }
        internal CharacterTestFairyGuiBehaviorDeckCardIcon Icon { get; set; }
        public bool Visible { get; set; }
        public bool ActionEnabled { get; set; }
        public bool SecondaryActionVisible { get; set; }
        public bool SecondaryActionEnabled { get; set; }
        public bool IsOperationTarget { get; set; }
        public bool CanEditInLab { get; set; }
    }

    public sealed class CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel
    {
        public string StableCardId { get; set; } = string.Empty;
        public string TitleText { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
        public CharacterTestFairyGuiBehaviorDeckFeedbackTone Tone { get; set; }
        public bool Visible { get; set; }
    }

    public sealed class CharacterTestFairyGuiBehaviorDeckPanelViewModel
    {
        private CharacterTestFairyGuiBehaviorDeckCardViewModel[] _libraryCards =
            Array.Empty<CharacterTestFairyGuiBehaviorDeckCardViewModel>();
        private CharacterTestFairyGuiBehaviorDeckCardViewModel[] _deckCards =
            Array.Empty<CharacterTestFairyGuiBehaviorDeckCardViewModel>();
        private CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] _presetAddedRows =
            Array.Empty<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel>();
        private CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] _presetRetainedRows =
            Array.Empty<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel>();
        private CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] _presetRemovedRows =
            Array.Empty<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel>();

        public string Title { get; set; } = "行为卡组";
        public string CharacterText { get; set; } = string.Empty;
        public string BudgetText { get; set; } = string.Empty;
        public string CostBudgetText { get; set; } = string.Empty;
        public string LockStatusText { get; set; } = string.Empty;
        public string LibrarySummaryText { get; set; } = string.Empty;
        public string PageStatusText { get; set; } = string.Empty;
        public string DeckSummaryText { get; set; } = string.Empty;
        public string DiagnosticsText { get; set; } = string.Empty;
        public string CommandStatusText { get; set; } = string.Empty;
        public string BudgetDeltaText { get; set; } = string.Empty;
        public CharacterTestFairyGuiBehaviorDeckFeedbackTone BudgetDeltaTone { get; set; }
        public bool BudgetDeltaVisible { get; set; }
        public long FeedbackVersion { get; set; }
        public bool FeedbackVisible { get; set; }
        public bool DetailsVisible { get; set; }
        public string DetailsStableCardId { get; set; } = string.Empty;
        public string SelectedStableCardId { get; set; } = string.Empty;
        public string DetailsTitleText { get; set; } = string.Empty;
        public string DetailsBodyText { get; set; } = string.Empty;
        public string DetailsPageStatusText { get; set; } = string.Empty;
        public int DetailsPageIndex { get; set; }
        public int DetailsPageCount { get; set; }
        public bool CanPreviousDetailsPage { get; set; }
        public bool CanNextDetailsPage { get; set; }
        public long FocusRequestSequence { get; set; }
        public CharacterTestFairyGuiBehaviorDeckCardOrigin FocusOrigin { get; set; }
        public string FocusStableCardId { get; set; } = string.Empty;
        public CharacterTestFairyGuiBehaviorDeckCardActionRole FocusActionRole { get; set; }
        public CharacterTestFairyGuiBehaviorDeckCardAction FocusAction { get; set; }
        public int FocusPageIndex { get; set; }
        public bool CanPreviousPage { get; set; }
        public bool CanNextPage { get; set; }
        public bool CanPreviousCharacter { get; set; }
        public bool CanNextCharacter { get; set; }
        public bool CanPreviewRecommendedPreset { get; set; }
        public bool CanUndoRecommendedPreset { get; set; }
        public bool CanContinueToSetup { get; set; }
        public string ContinueToSetupLabel { get; set; } = "实验设置";
        public bool InstallSelectionActive { get; set; }
        public bool ShowAddCard { get; set; }
        public bool PresetPreviewVisible { get; set; }
        public string PresetTitleText { get; set; } = string.Empty;
        public string PresetSummaryText { get; set; } = string.Empty;
        public string PresetSourceBudgetText { get; set; } = string.Empty;
        public string PresetCandidateBudgetText { get; set; } = string.Empty;
        public string PresetCostDeltaText { get; set; } = string.Empty;
        public string PresetDiagnosticsText { get; set; } = string.Empty;
        public bool CanConfirmRecommendedPreset { get; set; }
        public long PresetExitFocusSequence { get; set; }
        public string PresetExitFocusTargetId { get; set; } = string.Empty;
        public IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> LibraryCards => _libraryCards;
        public IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> DeckCards => _deckCards;
        public IReadOnlyList<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel> PresetAddedRows => _presetAddedRows;
        public IReadOnlyList<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel> PresetRetainedRows => _presetRetainedRows;
        public IReadOnlyList<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel> PresetRemovedRows => _presetRemovedRows;

        public void SetLibraryCards(CharacterTestFairyGuiBehaviorDeckCardViewModel[] cards) =>
            _libraryCards = cards ?? Array.Empty<CharacterTestFairyGuiBehaviorDeckCardViewModel>();

        public void SetDeckCards(CharacterTestFairyGuiBehaviorDeckCardViewModel[] cards) =>
            _deckCards = cards ?? Array.Empty<CharacterTestFairyGuiBehaviorDeckCardViewModel>();

        public void SetPresetRows(
            CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] added,
            CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] retained,
            CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel[] removed)
        {
            _presetAddedRows = added ?? Array.Empty<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel>();
            _presetRetainedRows = retained ?? Array.Empty<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel>();
            _presetRemovedRows = removed ?? Array.Empty<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel>();
        }
    }

    internal static class CharacterTestFairyGuiBehaviorDeckPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(512);
            Append(builder, viewModel.Title);
            Append(builder, viewModel.CharacterText);
            Append(builder, viewModel.BudgetText);
            Append(builder, viewModel.CostBudgetText);
            Append(builder, viewModel.LockStatusText);
            Append(builder, viewModel.LibrarySummaryText);
            Append(builder, viewModel.PageStatusText);
            Append(builder, viewModel.DeckSummaryText);
            Append(builder, viewModel.DiagnosticsText);
            Append(builder, viewModel.CommandStatusText);
            Append(builder, viewModel.BudgetDeltaText);
            Append(builder, viewModel.DetailsStableCardId);
            Append(builder, viewModel.SelectedStableCardId);
            Append(builder, viewModel.DetailsTitleText);
            Append(builder, viewModel.DetailsBodyText);
            Append(builder, viewModel.DetailsPageStatusText);
            Append(builder, viewModel.FocusStableCardId);
            Append(builder, viewModel.PresetTitleText);
            Append(builder, viewModel.PresetSummaryText);
            Append(builder, viewModel.PresetSourceBudgetText);
            Append(builder, viewModel.PresetCandidateBudgetText);
            Append(builder, viewModel.PresetCostDeltaText);
            Append(builder, viewModel.PresetDiagnosticsText);
            Append(builder, viewModel.PresetExitFocusTargetId);
            Append(builder, viewModel.ContinueToSetupLabel);
            builder.Append(viewModel.CanPreviousPage ? '1' : '0')
                .Append(viewModel.CanNextPage ? '1' : '0')
                .Append(viewModel.CanPreviousCharacter ? '1' : '0')
                .Append(viewModel.CanNextCharacter ? '1' : '0')
                .Append(viewModel.CanPreviewRecommendedPreset ? '1' : '0')
                .Append(viewModel.CanUndoRecommendedPreset ? '1' : '0')
                .Append(viewModel.CanContinueToSetup ? '1' : '0')
                .Append(viewModel.InstallSelectionActive ? '1' : '0')
                .Append(viewModel.ShowAddCard ? '1' : '0')
                .Append(viewModel.PresetPreviewVisible ? '1' : '0')
                .Append(viewModel.CanConfirmRecommendedPreset ? '1' : '0')
                .Append(viewModel.PresetExitFocusSequence)
                .Append(viewModel.FeedbackVersion)
                .Append(viewModel.FeedbackVisible ? '1' : '0')
                .Append(viewModel.DetailsVisible ? '1' : '0')
                .Append(viewModel.DetailsPageIndex)
                .Append(viewModel.DetailsPageCount)
                .Append(viewModel.CanPreviousDetailsPage ? '1' : '0')
                .Append(viewModel.CanNextDetailsPage ? '1' : '0')
                .Append((int)viewModel.BudgetDeltaTone)
                .Append(viewModel.BudgetDeltaVisible ? '1' : '0')
                .Append(viewModel.FocusRequestSequence)
                .Append((int)viewModel.FocusOrigin)
                .Append((int)viewModel.FocusActionRole)
                .Append((int)viewModel.FocusAction)
                .Append(viewModel.FocusPageIndex);
            AppendCards(builder, viewModel.LibraryCards);
            AppendCards(builder, viewModel.DeckCards);
            AppendPresetRows(builder, viewModel.PresetAddedRows);
            AppendPresetRows(builder, viewModel.PresetRetainedRows);
            AppendPresetRows(builder, viewModel.PresetRemovedRows);
            return builder.ToString();
        }

        private static void AppendPresetRows(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel> rows)
        {
            builder.Append('|').Append(rows != null ? rows.Count : 0);
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckPresetDiffRowViewModel row = rows[i];
                Append(builder, row.StableCardId);
                Append(builder, row.TitleText);
                Append(builder, row.CostText);
                Append(builder, row.StateText);
                builder.Append((int)row.Tone).Append(row.Visible ? '1' : '0');
            }
        }

        private static void AppendCards(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards)
        {
            builder.Append('|').Append(cards != null ? cards.Count : 0);
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                CharacterTestFairyGuiBehaviorDeckCardViewModel card = cards[i];
                Append(builder, card.StableCardId);
                Append(builder, card.Title);
                Append(builder, card.CostText);
                Append(builder, card.StateText);
                Append(builder, card.TagsText);
                Append(builder, card.DescriptionText);
                Append(builder, card.UseText);
                Append(builder, card.RiskText);
                Append(builder, card.DetailsTooltipText);
                Append(builder, card.ActionText);
                Append(builder, card.SecondaryActionText);
                Append(builder, card.SlotText);
                Append(builder, card.BlockReasonText);
                Append(builder, card.InlineFeedbackText);
                builder.Append((int)card.State)
                    .Append((int)card.Action)
                    .Append((int)card.SecondaryAction)
                    .Append((int)card.FeedbackTone)
                    .Append((int)card.Icon)
                    .Append(card.Visible ? '1' : '0')
                    .Append(card.ActionEnabled ? '1' : '0')
                    .Append(card.SecondaryActionVisible ? '1' : '0')
                    .Append(card.SecondaryActionEnabled ? '1' : '0')
                    .Append(card.IsOperationTarget ? '1' : '0')
                    .Append(card.CanEditInLab ? '1' : '0');
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append('|').Append(value.Length).Append(':').Append(value);
        }
    }
}
