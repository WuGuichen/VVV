using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestFairyGuiArenaBehaviorGroupPanelState
    {
        public CharacterTestFairyGuiArenaBehaviorGroupPanelState(
            int draftId,
            int filterIndex,
            int libraryPage,
            int entryPage,
            int selectedLibraryRow,
            int selectedEntryIndex,
            string searchText = "")
        {
            DraftId = draftId > 0 ? draftId : CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId;
            FilterIndex = filterIndex < 0 ? -1 : NormalizeCapabilityIndex(filterIndex);
            LibraryPage = libraryPage < 0 ? 0 : libraryPage;
            EntryPage = entryPage < 0 ? 0 : entryPage;
            SelectedLibraryRow = selectedLibraryRow < 0 ? 0 : selectedLibraryRow;
            SelectedEntryIndex = selectedEntryIndex < 0 ? 0 : selectedEntryIndex;
            SearchText = NormalizeSearchText(searchText);
        }

        public int DraftId { get; }
        public int FilterIndex { get; }
        public int LibraryPage { get; }
        public int EntryPage { get; }
        public int SelectedLibraryRow { get; }
        public int SelectedEntryIndex { get; }
        public string SearchText { get; }

        private static int NormalizeCapabilityIndex(int index)
        {
            int count = CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelBuilder.CapabilitySlotOrder.Length;
            if (count <= 0)
                return 0;
            int value = index % count;
            return value < 0 ? value + count : value;
        }

        private static string NormalizeSearchText(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return string.Empty;
            string trimmed = searchText.Trim();
            return trimmed.Length <= 48 ? trimmed : trimmed.Substring(0, 48);
        }
    }

    internal static partial class CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelBuilder
    {
        internal static readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] CapabilitySlotOrder =
        {
            CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Control,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Kite,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Defense,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Support
        };

        private const int NoDefinitionIndex = -1;

        public static CharacterTestFairyGuiArenaBehaviorGroupPanelViewModel Build(
            CharacterTestArenaSessionController controller,
            CharacterTestFairyGuiArenaBehaviorGroupPanelState state,
            IMxUiTextProvider textProvider = null,
            string commandStatus = "")
        {
            CharacterTestArenaMatchConfig config = controller != null ? controller.Config : null;
            CharacterTestArenaBehaviorGroupDraftStore store = config?.BehaviorGroups;
            CharacterTestArenaBehaviorGroupDraft draft = ResolveDraft(store, state.DraftId);
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                store != null && draft != null ? store.CompileDraft(draft.DraftId) : null;
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast =
                CharacterTestArenaBehaviorGroupOutcomeEstimator.Estimate(
                    draft,
                    store?.DefinitionCatalog,
                    compile);
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport capabilityReport =
                CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.Analyze(
                    draft?.Profile,
                    store?.DefinitionCatalog);

            int selectedCapabilityIndex = ResolveSelectedCapabilityIndex(capabilityReport, state.FilterIndex);
            CharacterTestRuntimeAiPlannerBehaviorCapability selectedCapability =
                CapabilitySlotOrder[selectedCapabilityIndex];
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition[] filteredDefinitions =
                FilterDefinitions(
                    store?.DefinitionCatalog,
                    selectedCapability,
                    state.SearchText,
                    draft,
                    capabilityReport);
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition selectedLibraryDefinition =
                ResolveSelectedDefinition(filteredDefinitions, state);
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry selectedEntry =
                ResolveSelectedEntry(draft, state.SelectedEntryIndex);
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition selectedEntryDefinition =
                ResolveDefinition(store, selectedEntry);
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary selectedSummary =
                FindSummary(compile, selectedEntry);
            string selectedForecastText = FormatEntryForecast(forecast, selectedEntry);

            var viewModel = new CharacterTestFairyGuiArenaBehaviorGroupPanelViewModel
            {
                Title = L(textProvider, "ui.character_test.arena.behavior_group.title", "Arena 行为卡组设置"),
                StatusText = FormatCompileStatus(compile),
                Message = string.IsNullOrWhiteSpace(commandStatus)
                    ? "按能力补齐卡组，配置只作用于当前 Arena 测试会话。"
                    : commandStatus,
                BackLabel = L(textProvider, "ui.character_test.arena.behavior_group.back", "返回队伍设置"),
                AddEntryLabel = "添加到卡组",
                DuplicateEntryLabel = "复制",
                MoveUpLabel = "上移",
                MoveDownLabel = "下移",
                DeleteEntryLabel = "删除",
                ResetParametersLabel = "重置参数",
                InspectorTitle = "卡牌详情",
                DecisionTitle = "结果预估",
                SearchLabel = "搜索",
                ClearSearchLabel = "清空",
                FillBasicLabel = "补齐基础组",
                DraftId = draft != null ? draft.DraftId : CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId,
                DraftLabel = FormatDraftLabel(store, draft),
                FilterLabel = FormatCapabilityFilterLabel(selectedCapability, state.SearchText),
                SelectedLibraryRow = state.SelectedLibraryRow,
                SelectedEntryIndex = state.SelectedEntryIndex,
                SelectedCapabilityIndex = selectedCapabilityIndex,
                SearchText = state.SearchText,
                CompileSuccess = compile != null && compile.Success,
                CoverageHeadline = FormatCoverageHeadline(capabilityReport),
                CoverageSummary = FormatCoverageSummary(capabilityReport),
                CoverageRequiredText = FormatCoverageRequiredText(capabilityReport),
                CoverageRiskText = FormatCoverageRiskText(capabilityReport),
                OutcomeHeadline = forecast.Headline,
                OutcomeText = forecast.Text,
                DiagnosticsText = FormatDiagnostics(compile),
                ActionSummary = FormatActionSummary(draft, compile, capabilityReport)
            };

            viewModel.SetCapabilitySlotRows(CreateCapabilitySlotRows(capabilityReport, selectedCapabilityIndex));
            viewModel.SetLibraryRows(CreateLibraryRows(filteredDefinitions, state, selectedCapability, capabilityReport));
            viewModel.SetEntryRows(CreateEntryRows(draft, store, compile, forecast, state));
            viewModel.SetParameterRows(CreateParameterRows(selectedEntry, selectedEntryDefinition));
            viewModel.SetForecastRows(CreateForecastRows(forecast));

            viewModel.CanAddEntry = selectedLibraryDefinition != null;
            viewModel.CanEditEntry = selectedEntry != null;
            viewModel.CanPageLibraryPrev = state.LibraryPage > 0;
            viewModel.CanPageLibraryNext =
                (state.LibraryPage + 1) * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowCount
                < filteredDefinitions.Length;
            int entryCount = draft != null ? draft.Profile.Entries.Count : 0;
            viewModel.CanPageEntriesPrev = state.EntryPage > 0;
            viewModel.CanPageEntriesNext =
                (state.EntryPage + 1) * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount
                < entryCount;
            viewModel.EntrySummary = FormatEntrySummary(draft, state.EntryPage);
            viewModel.LibrarySummary = FormatLibrarySummary(
                filteredDefinitions,
                selectedCapability,
                state.LibraryPage,
                state.SearchText);
            viewModel.ToggleDisabledLabel = selectedEntry != null && selectedEntry.Disabled
                ? "启用"
                : "禁用";
            viewModel.InspectorText = FormatInspector(
                selectedEntry,
                selectedEntryDefinition,
                selectedSummary,
                selectedForecastText);
            viewModel.SelectedCardDetail = selectedEntry != null
                ? FormatSelectedEntryDetail(selectedEntry, selectedEntryDefinition, selectedSummary, selectedForecastText)
                : FormatSelectedDefinitionDetail(selectedLibraryDefinition, selectedCapability);
            viewModel.DecisionText = FormatDecisionText(forecast, capabilityReport, draft, store, compile);
            return viewModel;
        }

        internal static CharacterTestRuntimeAiPlannerBehaviorModuleDefinition ResolveSelectedDefinition(
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestFairyGuiArenaBehaviorGroupPanelState state)
        {
            CharacterTestRuntimeAiPlannerBehaviorCapability capability =
                CapabilitySlotOrder[NormalizeFilterIndex(state.FilterIndex)];
            CharacterTestArenaBehaviorGroupDraft draft = ResolveDraft(store, state.DraftId);
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report =
                CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.Analyze(
                    draft?.Profile,
                    store?.DefinitionCatalog);
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition[] filtered =
                FilterDefinitions(store?.DefinitionCatalog, capability, state.SearchText, draft, report);
            return ResolveSelectedDefinition(filtered, state);
        }

        internal static int NormalizeFilterIndex(int filterIndex)
        {
            int count = CapabilitySlotOrder.Length;
            int value = filterIndex % count;
            return value < 0 ? value + count : value;
        }

        internal static int ResolveSelectedCapabilityIndex(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report,
            int requestedIndex)
        {
            int normalized = NormalizeFilterIndex(requestedIndex);
            if (requestedIndex >= 0)
                return normalized;

            for (int i = 0; i < CapabilitySlotOrder.Length; i++)
            {
                if (report != null && report.HasMissing(CapabilitySlotOrder[i]))
                    return i;
            }

            for (int i = 0; i < CapabilitySlotOrder.Length; i++)
            {
                if (HasCapabilityRisk(report, CapabilitySlotOrder[i]))
                    return i;
            }

            for (int i = 0; i < CapabilitySlotOrder.Length; i++)
            {
                if (CapabilitySlotOrder[i] == CharacterTestRuntimeAiPlannerBehaviorCapability.Damage)
                    return i;
            }

            return normalized;
        }

        internal static bool DefinitionProvidesCapability(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            if (definition == null)
                return false;
            if (definition.PrimaryCapability == capability)
                return true;
            for (int i = 0; i < definition.SecondaryCapabilities.Count; i++)
            {
                if (definition.SecondaryCapabilities[i] == capability)
                    return true;
            }

            return false;
        }

        private static CharacterTestArenaBehaviorGroupDraft ResolveDraft(
            CharacterTestArenaBehaviorGroupDraftStore store,
            int draftId)
        {
            if (store == null)
                return null;
            if (draftId > 0 && store.TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft))
                return draft;
            return store.GetDefaultDraft();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleDefinition ResolveSelectedDefinition(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> filtered,
            CharacterTestFairyGuiArenaBehaviorGroupPanelState state)
        {
            int index = state.LibraryPage
                * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowCount
                + state.SelectedLibraryRow;
            return filtered != null && index >= 0 && index < filtered.Count
                ? filtered[index]
                : null;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry ResolveSelectedEntry(
            CharacterTestArenaBehaviorGroupDraft draft,
            int entryIndex)
        {
            return draft != null && entryIndex >= 0 && entryIndex < draft.Profile.Entries.Count
                ? draft.Profile.Entries[entryIndex]
                : null;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleDefinition ResolveDefinition(
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (store == null || entry == null)
                return null;
            return store.DefinitionCatalog.TryGetDefinition(
                entry.BehaviorId,
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                ? definition
                : null;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleSummary FindSummary(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (compile == null || entry == null)
                return default;
            for (int i = 0; i < compile.ModuleSummaries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary = compile.ModuleSummaries[i];
                if (string.Equals(summary.EntryId, entry.EntryId, StringComparison.Ordinal))
                    return summary;
            }

            return default;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleDefinition[] FilterDefinitions(
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            string searchText,
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> source =
                catalog != null
                    ? catalog.Definitions
                    : CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn().Definitions;
            var matches = new List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = source[i];
                if (!DefinitionProvidesCapability(definition, capability))
                    continue;
                if (!MatchesSearch(definition, searchText))
                    continue;
                matches.Add(definition);
            }

            matches.Sort((left, right) =>
            {
                bool leftRecommended = IsRecommended(left, draft, report, capability);
                bool rightRecommended = IsRecommended(right, draft, report, capability);
                if (leftRecommended != rightRecommended)
                    return leftRecommended ? -1 : 1;
                return string.Compare(
                    DisplayBehaviorName(left),
                    DisplayBehaviorName(right),
                    StringComparison.Ordinal);
            });
            return matches.ToArray();
        }

        private static bool IsRecommended(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            if (definition == null || !DefinitionProvidesCapability(definition, capability))
                return false;
            bool needsCapability = report == null
                || report.HasMissing(capability)
                || report.HasDisabledOnly(capability);
            return needsCapability && !HasEnabledDefinition(draft, definition.BehaviorId);
        }

        private static bool HasEnabledDefinition(
            CharacterTestArenaBehaviorGroupDraft draft,
            string behaviorId)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries = draft?.Profile?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[i];
                if (!entry.Disabled && string.Equals(entry.BehaviorId, behaviorId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool MatchesSearch(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            string value = searchText.Trim();
            return Contains(definition.BehaviorId, value)
                || Contains(definition.DisplayName, value)
                || Contains(definition.Intent, value)
                || Contains(definition.Category.ToString(), value)
                || Contains(CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatCapabilityLabel(
                    definition.PrimaryCapability), value)
                || Contains(CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                    definition.ActionPrimitive), value);
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && !string.IsNullOrEmpty(value)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
