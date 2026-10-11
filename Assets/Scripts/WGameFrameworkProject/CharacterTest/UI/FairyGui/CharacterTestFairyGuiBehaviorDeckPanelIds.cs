using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiBehaviorDeckPanelIds
    {
        public const string ViewIdValue = "ui.character_test.runtime_ai_planner.behavior_deck";
        public const string PackageId = "MxCharacterTestBehaviorDeckUi";
        public const string ComponentName = "BehaviorDeckPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.behavior_deck_ui.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.behavior_deck_ui.atlas0";
        public const int LibraryCardCount = 8;
        public const int DeckCardCount = 6;

        public const string Title = "title";
        public const string Character = "character";
        public const string Budget = "budget";
        public const string CostBudget = "costBudget";
        public const string LockStatus = "lockStatus";
        public const string LibraryHeader = "libraryHeader";
        public const string LibrarySummary = "librarySummary";
        public const string PageStatus = "pageStatus";
        public const string DeckHeader = "deckHeader";
        public const string DeckSummary = "deckSummary";
        public const string Diagnostics = "diagnostics";
        public const string CommandStatus = "commandStatus";
        public const string DetailsBackdrop = "detailsBackdrop";
        public const string DetailsTitle = "detailsTitle";
        public const string DetailsBody = "detailsBody";
        public const string DetailsPageStatus = "detailsPageStatus";
        public const string PreviousDetailsPage = "btnPreviousDetailsPage";
        public const string NextDetailsPage = "btnNextDetailsPage";
        public const string CloseDetails = "btnCloseDetails";
        public const string Close = "btnClose";
        public const string PreviousPage = "btnPreviousPage";
        public const string NextPage = "btnNextPage";
        public const string PreviousCharacter = "btnPreviousCharacter";
        public const string NextCharacter = "btnNextCharacter";
        public const string PreviewRecommendedPreset = "btnPreviewRecommendedPreset";
        public const string UndoRecommendedPreset = "btnUndoRecommendedPreset";
        public const string PresetBackdrop = "presetBackdrop";
        public const string PresetPanel = "presetPanel";
        public const string PresetTitle = "presetTitle";
        public const string PresetSummary = "presetSummary";
        public const string PresetSourceBudget = "presetSourceBudget";
        public const string PresetCandidateBudget = "presetCandidateBudget";
        public const string PresetCostDelta = "presetCostDelta";
        public const string PresetDiagnostics = "presetDiagnostics";
        public const string CancelRecommendedPreset = "btnCancelRecommendedPreset";
        public const string ConfirmRecommendedPreset = "btnConfirmRecommendedPreset";
        public const string ContinueToSetup = "btnContinueToSetup";
        public const string SelectedBand = "selectedBand";
        public const string SelectedTitle = "selectedTitle";
        public const string SelectedTags = "selectedTags";
        public const string SelectedSummary = "selectedSummary";
        public const string SelectedCost = "selectedCost";
        public const string SelectedEdit = "btnSelectedEdit";
        public const string SelectedAction = "btnSelectedAction";
        public const string AddCard = "btnAddCard";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);

        public static string LibraryCard(int index) => "libraryCard" + index;
        public static string DeckCard(int index) => "deckCard" + index;
        public static string PresetAddedRow(int index) => "presetAdded" + index;
        public static string PresetRetainedRow(int index) => "presetRetained" + index;
        public static string PresetRemovedRow(int index) => "presetRemoved" + index;
    }

    public static class CharacterTestFairyGuiBehaviorDeckPanelCommandIds
    {
        public const string Close = "character_test.runtime_ai_planner.behavior_deck.close";
        public const string PreviousPage = "character_test.runtime_ai_planner.behavior_deck.previous_page";
        public const string NextPage = "character_test.runtime_ai_planner.behavior_deck.next_page";
        public const string CardAction = "character_test.runtime_ai_planner.behavior_deck.card_action";
        public const string PreviousCharacter = "character_test.runtime_ai_planner.behavior_deck.previous_character";
        public const string NextCharacter = "character_test.runtime_ai_planner.behavior_deck.next_character";
        public const string PreviewRecommendedPreset = "character_test.runtime_ai_planner.behavior_deck.preview_recommended_preset";
        public const string ConfirmRecommendedPreset = "character_test.runtime_ai_planner.behavior_deck.confirm_recommended_preset";
        public const string CancelRecommendedPreset = "character_test.runtime_ai_planner.behavior_deck.cancel_recommended_preset";
        public const string UndoRecommendedPreset = "character_test.runtime_ai_planner.behavior_deck.undo_recommended_preset";
        public const string ContinueToSetup = "character_test.runtime_ai_planner.behavior_deck.continue_to_setup";
        public const string CloseDetails = "character_test.runtime_ai_planner.behavior_deck.close_details";
        public const string PreviousDetailsPage = "character_test.runtime_ai_planner.behavior_deck.previous_details_page";
        public const string NextDetailsPage = "character_test.runtime_ai_planner.behavior_deck.next_details_page";
        public const string ToggleInstallSelection = "character_test.runtime_ai_planner.behavior_deck.toggle_install_selection";
    }
}
