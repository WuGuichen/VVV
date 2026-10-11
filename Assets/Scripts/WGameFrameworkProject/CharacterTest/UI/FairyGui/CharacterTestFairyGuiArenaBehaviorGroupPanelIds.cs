using System;
using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiArenaBehaviorGroupPanelIds
    {
        public const string ViewIdValue = "ui.character_test.arena.behavior_group";
        public const string PackageId = CharacterTestFairyGuiLegacyPanelPackageIds.PackageId;
        public const string ComponentName = "ArenaBehaviorGroupPanel";
        public const string PackageBytesResourceId = CharacterTestFairyGuiLegacyPanelPackageIds.PackageBytesResourceId;
        public const string Atlas0ResourceId = CharacterTestFairyGuiLegacyPanelPackageIds.Atlas0ResourceId;
        public const int CapabilitySlotCount = 10;
        public const int LibraryRowCount = 5;
        public const int EntryRowCount = 6;
        public const int ParameterRowCount = 4;
        public const int ForecastRowCount = 7;

        public const string Scrim = "scrim";
        public const string Frame = "frame";
        public const string Title = "title";
        public const string Status = "status";
        public const string Message = "message";
        public const string Back = "btnBack";
        public const string DraftPrev = "btnDraftPrev";
        public const string DraftNext = "btnDraftNext";
        public const string DraftLabel = "draftLabel";
        public const string FilterPrev = "btnFilterPrev";
        public const string FilterNext = "btnFilterNext";
        public const string FilterLabel = "filterLabel";
        public const string SearchInput = "searchInput";
        public const string SearchButton = "btnSearch";
        public const string ClearSearch = "btnClearSearch";
        public const string FillBasic = "btnFillBasic";
        public const string CoverageHeadline = "coverageHeadline";
        public const string LibrarySummary = "librarySummary";
        public const string LibraryPrev = "btnLibraryPrev";
        public const string LibraryNext = "btnLibraryNext";
        public const string EntrySummary = "entrySummary";
        public const string EntryPrev = "btnEntryPrev";
        public const string EntryNext = "btnEntryNext";
        public const string AddEntry = "btnAddEntry";
        public const string DuplicateEntry = "btnDuplicateEntry";
        public const string MoveEntryUp = "btnMoveEntryUp";
        public const string MoveEntryDown = "btnMoveEntryDown";
        public const string ToggleEntryDisabled = "btnToggleEntryDisabled";
        public const string DeleteEntry = "btnDeleteEntry";
        public const string ResetParameters = "btnResetParameters";
        public const string InspectorTitle = "inspectorTitle";
        public const string InspectorText = "inspectorText";
        public const string SelectedCardDetail = "selectedCardDetail";
        public const string DecisionTitle = "decisionTitle";
        public const string DecisionText = "decisionText";
        public const string ActionSummary = "actionSummary";
        public const string DiagnosticsText = "diagnosticsText";
        public const string CoverageSummary = "coverageSummary";
        public const string CoverageRequired = "coverageRequired";
        public const string CoverageRisk = "coverageRisk";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);
        public static ResourceKey PackageBytesKey => CharacterTestFairyGuiLegacyPanelPackageIds.PackageBytesKey;
        public static ResourceKey Atlas0Key => CharacterTestFairyGuiLegacyPanelPackageIds.Atlas0Key;

        public static string LibraryRow(int rowIndex)
        {
            return "libraryRow" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string LibraryRowTitle(int rowIndex)
        {
            return LibraryRow(rowIndex) + "Title";
        }

        public static string LibraryRowIntent(int rowIndex)
        {
            return LibraryRow(rowIndex) + "Intent";
        }

        public static string LibraryRowTrigger(int rowIndex)
        {
            return LibraryRow(rowIndex) + "Trigger";
        }

        public static string LibraryRowOutput(int rowIndex)
        {
            return LibraryRow(rowIndex) + "Output";
        }

        public static string LibraryRowBadges(int rowIndex)
        {
            return LibraryRow(rowIndex) + "Badges";
        }

        public static string LibraryRowRisk(int rowIndex)
        {
            return LibraryRow(rowIndex) + "Risk";
        }

        public static string CapabilitySlot(int rowIndex)
        {
            return "capabilitySlot" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string CapabilitySlotName(int rowIndex)
        {
            return CapabilitySlot(rowIndex) + "Name";
        }

        public static string CapabilitySlotRole(int rowIndex)
        {
            return CapabilitySlot(rowIndex) + "Role";
        }

        public static string CapabilitySlotState(int rowIndex)
        {
            return CapabilitySlot(rowIndex) + "State";
        }

        public static string CapabilitySlotHint(int rowIndex)
        {
            return CapabilitySlot(rowIndex) + "Hint";
        }

        public static string EntryRow(int rowIndex)
        {
            return "entryRow" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string EntryRowOrder(int rowIndex)
        {
            return EntryRow(rowIndex) + "Order";
        }

        public static string EntryRowTitle(int rowIndex)
        {
            return EntryRow(rowIndex) + "Title";
        }

        public static string EntryRowOutput(int rowIndex)
        {
            return EntryRow(rowIndex) + "Output";
        }

        public static string EntryRowForecast(int rowIndex)
        {
            return EntryRow(rowIndex) + "Forecast";
        }

        public static string EntryRowState(int rowIndex)
        {
            return EntryRow(rowIndex) + "State";
        }

        public static string ForecastRow(int rowIndex)
        {
            return "forecastRow" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ForecastScenario(int rowIndex)
        {
            return ForecastRow(rowIndex) + "Scenario";
        }

        public static string ForecastCard(int rowIndex)
        {
            return ForecastRow(rowIndex) + "Card";
        }

        public static string ForecastOutput(int rowIndex)
        {
            return ForecastRow(rowIndex) + "Output";
        }

        public static string ForecastRisk(int rowIndex)
        {
            return ForecastRow(rowIndex) + "Risk";
        }

        public static string ParameterLabel(int rowIndex)
        {
            return "parameter" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "Label";
        }

        public static string ParameterPrev(int rowIndex)
        {
            return "parameter" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "Prev";
        }

        public static string ParameterNext(int rowIndex)
        {
            return "parameter" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "Next";
        }
    }

    public static class CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds
    {
        public const string Close = "character_test.arena.behavior_group.close";
        public const string DraftPrev = "character_test.arena.behavior_group.draft_prev";
        public const string DraftNext = "character_test.arena.behavior_group.draft_next";
        public const string FilterPrev = "character_test.arena.behavior_group.filter_prev";
        public const string FilterNext = "character_test.arena.behavior_group.filter_next";
        public const string SelectCapabilitySlot = "character_test.arena.behavior_group.select_capability";
        public const string ApplySearch = "character_test.arena.behavior_group.apply_search";
        public const string ClearSearch = "character_test.arena.behavior_group.clear_search";
        public const string FillBasic = "character_test.arena.behavior_group.fill_basic";
        public const string LibraryPrev = "character_test.arena.behavior_group.library_prev";
        public const string LibraryNext = "character_test.arena.behavior_group.library_next";
        public const string EntryPrev = "character_test.arena.behavior_group.entry_prev";
        public const string EntryNext = "character_test.arena.behavior_group.entry_next";
        public const string SelectLibraryRow = "character_test.arena.behavior_group.select_library";
        public const string SelectEntryRow = "character_test.arena.behavior_group.select_entry";
        public const string AddSelectedBehavior = "character_test.arena.behavior_group.add_selected";
        public const string DuplicateEntry = "character_test.arena.behavior_group.duplicate";
        public const string MoveEntryUp = "character_test.arena.behavior_group.move_up";
        public const string MoveEntryDown = "character_test.arena.behavior_group.move_down";
        public const string ToggleEntryDisabled = "character_test.arena.behavior_group.toggle_disabled";
        public const string DeleteEntry = "character_test.arena.behavior_group.delete";
        public const string ResetParameters = "character_test.arena.behavior_group.reset_parameters";
        public const string ParameterPrev = "character_test.arena.behavior_group.parameter_prev";
        public const string ParameterNext = "character_test.arena.behavior_group.parameter_next";

        public static string ForRow(string commandId, int rowIndex)
        {
            return commandId
                + ".row"
                + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Normalize(string commandId)
        {
            if (MatchesRowCommand(commandId, SelectLibraryRow))
                return SelectLibraryRow;
            if (MatchesRowCommand(commandId, SelectCapabilitySlot))
                return SelectCapabilitySlot;
            if (MatchesRowCommand(commandId, SelectEntryRow))
                return SelectEntryRow;
            if (MatchesRowCommand(commandId, ParameterPrev))
                return ParameterPrev;
            if (MatchesRowCommand(commandId, ParameterNext))
                return ParameterNext;
            return commandId ?? string.Empty;
        }

        private static bool MatchesRowCommand(string commandId, string baseCommandId)
        {
            if (string.Equals(commandId, baseCommandId, StringComparison.Ordinal))
                return true;
            if (string.IsNullOrEmpty(commandId) || commandId.Length <= baseCommandId.Length)
                return false;
            return commandId[baseCommandId.Length] == '.'
                && commandId.StartsWith(baseCommandId, StringComparison.Ordinal);
        }
    }
}
