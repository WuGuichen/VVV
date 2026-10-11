using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiArenaBehaviorGroupPanelPayload
    {
        public int RowIndex { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel
    {
        public int RowIndex { get; set; }
        public int CapabilityIndex { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RoleText { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public bool Missing { get; set; }
        public bool DisabledOnly { get; set; }
        public bool HasRisk { get; set; }
        public bool Visible { get; set; }
    }

    public sealed class CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel
    {
        public int RowIndex { get; set; }
        public int DefinitionIndex { get; set; } = -1;
        public string BehaviorId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string TriggerSummary { get; set; } = string.Empty;
        public string OutputAction { get; set; } = string.Empty;
        public string Badges { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool Recommended { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
    }

    public sealed class CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel
    {
        public int RowIndex { get; set; }
        public int EntryIndex { get; set; } = -1;
        public string EntryId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string OrderText { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string OutputAction { get; set; } = string.Empty;
        public string ForecastHit { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool Disabled { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
    }

    public sealed class CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel
    {
        public int RowIndex { get; set; }
        public int ParameterIndex { get; set; } = -1;
        public string Label { get; set; } = string.Empty;
        public bool Visible { get; set; }
    }

    public sealed class CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel
    {
        public int RowIndex { get; set; }
        public string Scenario { get; set; } = string.Empty;
        public string ExpectedCard { get; set; } = string.Empty;
        public string OutputAction { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Notice { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool Visible { get; set; }
    }

    public sealed class CharacterTestFairyGuiArenaBehaviorGroupPanelViewModel
    {
        private CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel[] _capabilitySlotRows =
            Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel>();
        private CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel[] _libraryRows =
            Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel>();
        private CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel[] _entryRows =
            Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel>();
        private CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel[] _parameterRows =
            Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel>();
        private CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel[] _forecastRows =
            Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel>();

        public string Title { get; set; } = "Arena Behavior Cards";
        public string StatusText { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string BackLabel { get; set; } = "Back";
        public string DraftLabel { get; set; } = string.Empty;
        public string FilterLabel { get; set; } = string.Empty;
        public string LibrarySummary { get; set; } = string.Empty;
        public string EntrySummary { get; set; } = string.Empty;
        public string AddEntryLabel { get; set; } = "Add Card";
        public string DuplicateEntryLabel { get; set; } = "Copy";
        public string MoveUpLabel { get; set; } = "Up";
        public string MoveDownLabel { get; set; } = "Down";
        public string ToggleDisabledLabel { get; set; } = "Disable";
        public string DeleteEntryLabel { get; set; } = "Delete";
        public string ResetParametersLabel { get; set; } = "Reset Params";
        public string InspectorTitle { get; set; } = "Card Detail";
        public string InspectorText { get; set; } = string.Empty;
        public string OutcomeHeadline { get; set; } = string.Empty;
        public string OutcomeText { get; set; } = string.Empty;
        public string DecisionTitle { get; set; } = "Outcome Forecast";
        public string DecisionText { get; set; } = string.Empty;
        public string ActionSummary { get; set; } = string.Empty;
        public string DiagnosticsText { get; set; } = string.Empty;
        public string CoverageHeadline { get; set; } = string.Empty;
        public string CoverageSummary { get; set; } = string.Empty;
        public string CoverageRequiredText { get; set; } = string.Empty;
        public string CoverageRiskText { get; set; } = string.Empty;
        public string SelectedCardDetail { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string SearchLabel { get; set; } = "Search";
        public string ClearSearchLabel { get; set; } = "Clear";
        public string FillBasicLabel { get; set; } = "Fill Basic";
        public int DraftId { get; set; }
        public int SelectedLibraryRow { get; set; }
        public int SelectedEntryIndex { get; set; }
        public int SelectedCapabilityIndex { get; set; }
        public bool CanAddEntry { get; set; }
        public bool CanEditEntry { get; set; }
        public bool CanPageLibraryPrev { get; set; }
        public bool CanPageLibraryNext { get; set; }
        public bool CanPageEntriesPrev { get; set; }
        public bool CanPageEntriesNext { get; set; }
        public bool CompileSuccess { get; set; }

        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel> CapabilitySlotRows =>
            _capabilitySlotRows;
        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel> FilteredCardRows =>
            _libraryRows;
        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel> LibraryRows =>
            _libraryRows;
        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel> DeckCardRows =>
            _entryRows;
        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel> EntryRows =>
            _entryRows;
        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel> ParameterRows =>
            _parameterRows;
        public IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel> ForecastRows =>
            _forecastRows;

        public void SetCapabilitySlotRows(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel> rows)
        {
            _capabilitySlotRows = Copy(rows);
        }

        public void SetLibraryRows(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel> rows)
        {
            _libraryRows = Copy(rows);
        }

        public void SetEntryRows(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel> rows)
        {
            _entryRows = Copy(rows);
        }

        public void SetParameterRows(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel> rows)
        {
            _parameterRows = Copy(rows);
        }

        public void SetForecastRows(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel> rows)
        {
            _forecastRows = Copy(rows);
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel[] Copy(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel>();

            var copy = new CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                copy[i] = rows[i];
            return copy;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel[] Copy(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel>();

            var copy = new CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                copy[i] = rows[i];
            return copy;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel[] Copy(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel>();

            var copy = new CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                copy[i] = rows[i];
            return copy;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel[] Copy(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel>();

            var copy = new CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                copy[i] = rows[i];
            return copy;
        }

        private static CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel[] Copy(
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel>();

            var copy = new CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                copy[i] = rows[i];
            return copy;
        }
    }

    internal static class CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiArenaBehaviorGroupPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(1024);
            builder.Append(viewModel.DraftId).Append('|')
                .Append(viewModel.SelectedLibraryRow).Append('|')
                .Append(viewModel.SelectedEntryIndex).Append('|')
                .Append(viewModel.SelectedCapabilityIndex).Append('|')
                .Append(viewModel.CanAddEntry ? '1' : '0').Append('|')
                .Append(viewModel.CanEditEntry ? '1' : '0').Append('|')
                .Append(viewModel.CompileSuccess ? '1' : '0').Append('|');
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.Message);
            AppendToken(builder, viewModel.BackLabel);
            AppendToken(builder, viewModel.DraftLabel);
            AppendToken(builder, viewModel.FilterLabel);
            AppendToken(builder, viewModel.LibrarySummary);
            AppendToken(builder, viewModel.EntrySummary);
            AppendToken(builder, viewModel.AddEntryLabel);
            AppendToken(builder, viewModel.DuplicateEntryLabel);
            AppendToken(builder, viewModel.MoveUpLabel);
            AppendToken(builder, viewModel.MoveDownLabel);
            AppendToken(builder, viewModel.ToggleDisabledLabel);
            AppendToken(builder, viewModel.DeleteEntryLabel);
            AppendToken(builder, viewModel.ResetParametersLabel);
            AppendToken(builder, viewModel.InspectorTitle);
            AppendToken(builder, viewModel.InspectorText);
            AppendToken(builder, viewModel.OutcomeHeadline);
            AppendToken(builder, viewModel.OutcomeText);
            AppendToken(builder, viewModel.DecisionTitle);
            AppendToken(builder, viewModel.DecisionText);
            AppendToken(builder, viewModel.ActionSummary);
            AppendToken(builder, viewModel.DiagnosticsText);
            AppendToken(builder, viewModel.CoverageHeadline);
            AppendToken(builder, viewModel.CoverageSummary);
            AppendToken(builder, viewModel.CoverageRequiredText);
            AppendToken(builder, viewModel.CoverageRiskText);
            AppendToken(builder, viewModel.SelectedCardDetail);
            AppendToken(builder, viewModel.SearchText);
            AppendToken(builder, viewModel.SearchLabel);
            AppendToken(builder, viewModel.ClearSearchLabel);
            AppendToken(builder, viewModel.FillBasicLabel);
            AppendCapabilityRows(builder, viewModel.CapabilitySlotRows);
            AppendLibraryRows(builder, viewModel.FilteredCardRows);
            AppendEntryRows(builder, viewModel.DeckCardRows);
            AppendParameterRows(builder, viewModel.ParameterRows);
            AppendForecastRows(builder, viewModel.ForecastRows);
            return builder.ToString();
        }

        private static void AppendCapabilityRows(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel> rows)
        {
            builder.Append("cap=").Append(rows != null ? rows.Count : 0).Append('|');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestFairyGuiArenaBehaviorGroupCapabilitySlotRowViewModel row = rows[i];
                builder.Append(row.RowIndex).Append('|')
                    .Append(row.CapabilityIndex).Append('|')
                    .Append(row.Selected ? '1' : '0').Append('|')
                    .Append(row.Missing ? '1' : '0').Append('|')
                    .Append(row.DisabledOnly ? '1' : '0').Append('|')
                    .Append(row.HasRisk ? '1' : '0').Append('|')
                    .Append(row.Visible ? '1' : '0').Append('|');
                AppendToken(builder, row.Label);
                AppendToken(builder, row.Detail);
                AppendToken(builder, row.StateText);
                AppendToken(builder, row.Name);
                AppendToken(builder, row.RoleText);
                AppendToken(builder, row.Hint);
                AppendToken(builder, row.Severity);
            }
        }

        private static void AppendLibraryRows(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel> rows)
        {
            builder.Append("lib=").Append(rows != null ? rows.Count : 0).Append('|');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestFairyGuiArenaBehaviorGroupLibraryRowViewModel row = rows[i];
                builder.Append(row.RowIndex).Append('|')
                    .Append(row.DefinitionIndex).Append('|')
                    .Append(row.Recommended ? '1' : '0').Append('|')
                    .Append(row.Selected ? '1' : '0').Append('|')
                    .Append(row.Visible ? '1' : '0').Append('|');
                AppendToken(builder, row.BehaviorId);
                AppendToken(builder, row.Label);
                AppendToken(builder, row.Detail);
                AppendToken(builder, row.Title);
                AppendToken(builder, row.Intent);
                AppendToken(builder, row.TriggerSummary);
                AppendToken(builder, row.OutputAction);
                AppendToken(builder, row.Badges);
                AppendToken(builder, row.RiskText);
                AppendToken(builder, row.Severity);
            }
        }

        private static void AppendEntryRows(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel> rows)
        {
            builder.Append("entry=").Append(rows != null ? rows.Count : 0).Append('|');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestFairyGuiArenaBehaviorGroupEntryRowViewModel row = rows[i];
                builder.Append(row.RowIndex).Append('|')
                    .Append(row.EntryIndex).Append('|')
                    .Append(row.Disabled ? '1' : '0').Append('|')
                    .Append(row.Selected ? '1' : '0').Append('|')
                    .Append(row.Visible ? '1' : '0').Append('|');
                AppendToken(builder, row.EntryId);
                AppendToken(builder, row.Label);
                AppendToken(builder, row.Detail);
                AppendToken(builder, row.OrderText);
                AppendToken(builder, row.Title);
                AppendToken(builder, row.OutputAction);
                AppendToken(builder, row.ForecastHit);
                AppendToken(builder, row.StateText);
                AppendToken(builder, row.Severity);
            }
        }

        private static void AppendParameterRows(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel> rows)
        {
            builder.Append("param=").Append(rows != null ? rows.Count : 0).Append('|');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestFairyGuiArenaBehaviorGroupParameterRowViewModel row = rows[i];
                builder.Append(row.RowIndex).Append('|')
                    .Append(row.ParameterIndex).Append('|')
                    .Append(row.Visible ? '1' : '0').Append('|');
                AppendToken(builder, row.Label);
            }
        }

        private static void AppendForecastRows(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel> rows)
        {
            builder.Append("forecast=").Append(rows != null ? rows.Count : 0).Append('|');
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestFairyGuiArenaBehaviorGroupForecastRowViewModel row = rows[i];
                builder.Append(row.RowIndex).Append('|')
                    .Append(row.Visible ? '1' : '0').Append('|');
                AppendToken(builder, row.Scenario);
                AppendToken(builder, row.ExpectedCard);
                AppendToken(builder, row.OutputAction);
                AppendToken(builder, row.Reason);
                AppendToken(builder, row.Notice);
                AppendToken(builder, row.RiskText);
                AppendToken(builder, row.Severity);
            }
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
