using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelBuilder
    {
        private static CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel[] CreateCapabilitySlotRows(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report,
            int selectedCapabilityIndex)
        {
            var rows = new CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel[
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotCount];
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCapability capability = CapabilitySlotOrder[i];
                bool missing = report != null && report.HasMissing(capability);
                bool disabledOnly = report != null && report.HasDisabledOnly(capability);
                bool risk = HasCapabilityRisk(report, capability);
                rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel
                {
                    RowIndex = i,
                    CapabilityIndex = i,
                    Label = CapabilityDisplayName(capability),
                    Detail = CapabilityDescription(capability),
                    StateText = FormatCapabilityState(missing, disabledOnly, risk),
                    Name = CapabilityDisplayName(capability),
                    RoleText = FormatCapabilityRole(capability),
                    Hint = FormatCapabilityHint(capability, missing, disabledOnly, risk),
                    Severity = FormatCapabilitySeverity(missing, disabledOnly, risk),
                    Selected = i == selectedCapabilityIndex,
                    Missing = missing,
                    DisabledOnly = disabledOnly,
                    HasRisk = risk,
                    Visible = true
                };
            }

            return rows;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel[] CreateLibraryRows(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> filteredDefinitions,
            CharacterTestFairyGuiArenaBehaviorGroupPanelState state,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report)
        {
            var rows = new CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel[
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowCount];
            int start = state.LibraryPage * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowCount;
            for (int i = 0; i < rows.Length; i++)
            {
                int definitionIndex = start + i;
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition =
                    filteredDefinitions != null && definitionIndex >= 0 && definitionIndex < filteredDefinitions.Count
                        ? filteredDefinitions[definitionIndex]
                        : null;
                if (definition == null)
                {
                    rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel
                    {
                        RowIndex = i,
                        DefinitionIndex = NoDefinitionIndex
                    };
                    continue;
                }

                bool recommended = report == null
                    || report.HasMissing(capability)
                    || report.HasDisabledOnly(capability);
                string title = DisplayBehaviorName(definition);
                string output = CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                    definition.ActionPrimitive);
                rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel
                {
                    RowIndex = i,
                    DefinitionIndex = definitionIndex,
                    BehaviorId = definition.BehaviorId,
                    Label = (recommended ? "[Recommended] " : string.Empty) + title,
                    Detail = FormatDefinitionCardDetail(definition),
                    Title = title,
                    Intent = CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatIntent(definition),
                    TriggerSummary = CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatConditions(
                        definition,
                        null),
                    OutputAction = output,
                    Badges = FormatDefinitionBadges(definition, recommended),
                    RiskText = FormatDefinitionRisks(definition),
                    Severity = recommended ? "recommended" : "normal",
                    Recommended = recommended,
                    Selected = i == state.SelectedLibraryRow,
                    Visible = true
                };
            }

            return rows;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel[] CreateEntryRows(
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast,
            CharacterTestFairyGuiArenaBehaviorGroupPanelState state)
        {
            var rows = new CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel[
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount];
            int start = state.EntryPage * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount;
            for (int i = 0; i < rows.Length; i++)
            {
                int entryIndex = start + i;
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                    ResolveSelectedEntry(draft, entryIndex);
                if (entry == null)
                {
                    rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel
                    {
                        RowIndex = i,
                        EntryIndex = -1
                    };
                    continue;
                }

                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = ResolveDefinition(store, entry);
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary = FindSummary(compile, entry);
                string forecastText = FormatEntryForecast(forecast, entry);
                string title = DisplayBehaviorName(definition);
                string output = definition != null
                    ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                        definition.ActionPrimitive)
                    : "Unknown";
                rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel
                {
                    RowIndex = i,
                    EntryIndex = entryIndex,
                    EntryId = entry.EntryId,
                    Label = (entryIndex + 1).ToString(CultureInfo.InvariantCulture)
                        + ". "
                        + title
                        + (entry.Disabled ? " [disabled]" : string.Empty),
                    Detail = FormatEntryCardDetail(entry, definition, summary, forecastText),
                    OrderText = "#"
                        + (entryIndex + 1).ToString(CultureInfo.InvariantCulture)
                        + " / "
                        + entry.Order.ToString(CultureInfo.InvariantCulture),
                    Title = title,
                    OutputAction = output,
                    ForecastHit = forecastText,
                    StateText = FormatEntryStateText(entry, summary),
                    Severity = entry.Disabled ? "disabled" : FormatEntrySeverity(summary),
                    Disabled = entry.Disabled,
                    Selected = entryIndex == state.SelectedEntryIndex,
                    Visible = true
                };
            }

            return rows;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel[] CreateParameterRows(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            var rows = new CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel[
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ParameterRowCount];
            for (int i = 0; i < rows.Length; i++)
            {
                if (definition == null || i >= definition.Parameters.Count)
                {
                    rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel
                    {
                        RowIndex = i,
                        ParameterIndex = -1
                    };
                    continue;
                }

                CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter = definition.Parameters[i];
                rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel
                {
                    RowIndex = i,
                    ParameterIndex = i,
                    Label = CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatParameterName(parameter)
                        + ": "
                        + CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatParameterValue(
                            entry,
                            definition,
                            parameter),
                    Visible = true
                };
            }

            return rows;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel[] CreateForecastRows(
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast)
        {
            var rows = new CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel[
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastRowCount];
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestArenaBehaviorGroupOutcomeRow row =
                    forecast != null && i < forecast.Rows.Count ? forecast.Rows[i] : null;
                rows[i] = new CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel
                {
                    RowIndex = i,
                    Scenario = row != null ? row.ScenarioName : string.Empty,
                    ExpectedCard = row != null && !string.IsNullOrWhiteSpace(row.ExpectedBehavior)
                        ? row.ExpectedBehavior
                        : "无输出",
                    OutputAction = row != null && !string.IsNullOrWhiteSpace(row.ExpectedAction)
                        ? row.ExpectedAction
                        : "无输出",
                    Reason = row != null ? row.Reason : string.Empty,
                    Notice = row != null ? FormatForecastNotice(row) : string.Empty,
                    RiskText = row != null ? FormatForecastNotice(row) : string.Empty,
                    Severity = row != null && row.HasExpectedAction ? "ok" : "warning",
                    Visible = row != null
                };
            }

            return rows;
        }

    }
}
