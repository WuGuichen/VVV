using System;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        private const int FactsAgentCapacity = 8;
        private const int FactsCausalCapacity = 4;
        private const int FactsRowCapacity = 12;
        private const int FactsCandidateCapacity = 9;
        private const int FactsDeltaCapacity = 4;
        private const int FactsCommitCapacity = 3;
        private Fgui.GComponent _factsScrollRoot;
        private CharacterTestRuntimeAiLabFactsWorkspaceViewModel _factsScrollViewModel;
        private int _factsFactScrollOffset;
        private int _factsCandidateScrollOffset;
        private int _factsDeltaScrollOffset;

        private void BindFactsWorkspace(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GComponent factsRoot = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsWorkspace)?.asCom;
            bool visible = factsRoot != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Facts;
            if (factsRoot == null)
                return;
            factsRoot.visible = visible;
            if (!visible)
                return;

            CharacterTestRuntimeAiLabFactsWorkspaceViewModel facts =
                viewModel.Facts ?? new CharacterTestRuntimeAiLabFactsWorkspaceViewModel();
            SetFactsText(factsRoot, "titleText", facts.HeaderText);
            SetFactsText(factsRoot, "contextText", facts.ContextText + " · " + facts.HistoryStateText);
            BindTooltip(
                factsRoot,
                facts.ContextText + (string.IsNullOrEmpty(facts.DiagnosticCode)
                    ? string.Empty
                    : "\n" + facts.DiagnosticCode));
            BindFactsFilters(factsRoot, facts);
            BindBehaviorCardRuntimeControls(factsRoot, viewModel);
            BindFactsAgents(factsRoot, facts.Agents);
            BindFactsCausalFlow(factsRoot, facts.CausalFlow);
            ConfigureFactsScrolling(factsRoot, facts);
            BindFactsRows(factsRoot, facts.FactRows, _factsFactScrollOffset);
            BindFactsCandidateRows(factsRoot, facts.CandidateRows, _factsCandidateScrollOffset);
            BindFactsDeltaRows(factsRoot, facts.DeltaRows, facts.DeltaExpanded, _factsDeltaScrollOffset);
            BindFactsCommitPhases(factsRoot, facts.CommitPhases);
            BindFactsButton(
                factsRoot,
                "btnLocateBehaviorNode",
                "Locate behavior node",
                facts.CanLocateBehaviorNode,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateBehaviorNode);
            BindFactsButton(
                factsRoot,
                "btnLocateRuntimeFrame",
                "Locate RuntimeFrame",
                facts.CanLocateRuntimeFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateRuntimeFrame);
            BindFactsButton(
                factsRoot,
                "btnReturnSourceFrame",
                "返回来源帧",
                facts.CanReturnSourceFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsReturnSourceFrame);
            ApplyFactsResponsiveTableLayout(factsRoot, facts);
        }

        private static void ApplyFactsResponsiveTableLayout(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel facts)
        {
            ResolveFactsResponsiveTableLayout(
                Screen.width,
                facts.ActiveTableTab,
                out bool showFacts,
                out bool showCandidates,
                out float candidateX,
                out float candidateWidth);
            Fgui.GObject factHeader = root.GetChild("factHeader");
            if (factHeader != null)
                factHeader.visible = showFacts;
            Fgui.GObject candidateHeader = root.GetChild("candidateHeader");
            if (candidateHeader != null)
            {
                candidateHeader.visible = showCandidates;
                candidateHeader.x = candidateX;
            }
            for (int i = 0; i < FactsRowCapacity; i++)
            {
                Fgui.GObject row = root.GetChild("factRow" + (i + 1));
                if (row != null && !showFacts)
                    row.visible = false;
            }
            for (int i = 0; i < FactsCandidateCapacity; i++)
            {
                Fgui.GObject row = root.GetChild("candidateRow" + (i + 1));
                if (row == null)
                    continue;
                row.x = candidateX;
                row.width = candidateWidth;
                if (!showCandidates)
                    row.visible = false;
            }
            Fgui.GObject deltaHeader = root.GetChild("deltaHeader");
            if (deltaHeader != null && !showFacts)
                deltaHeader.visible = false;
            for (int i = 0; i < FactsDeltaCapacity; i++)
            {
                Fgui.GObject row = root.GetChild("deltaRow" + (i + 1));
                if (row != null && !showFacts)
                    row.visible = false;
            }
        }

        internal static bool IsFactsCompactLayout(int screenWidth) => screenWidth <= 1920;

        internal static void ResolveFactsResponsiveTableLayout(
            int screenWidth,
            CharacterTestRuntimeAiLabFactsTableTab activeTab,
            out bool showFacts,
            out bool showCandidates,
            out float candidateX,
            out float candidateWidth)
        {
            bool compact = IsFactsCompactLayout(screenWidth);
            showFacts = !compact || activeTab == CharacterTestRuntimeAiLabFactsTableTab.Facts;
            showCandidates = !compact || activeTab == CharacterTestRuntimeAiLabFactsTableTab.Candidates;
            candidateX = compact ? 244f : 1260f;
            candidateWidth = compact ? 1000f : 612f;
        }

        private void BindFactsFilters(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel facts)
        {
            Fgui.GTextInput search = root.GetChild("searchInput") as Fgui.GTextInput;
            if (search != null)
            {
                if (!Application.isPlaying || !search.focused)
                    search.text = facts.SearchText;
                search.promptText = "Search facts or candidates";
                search.onChanged.Set(() => _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSearch,
                    search.text)));
            }
            BindButton(root, "btnFactsChanged", "Changed", true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleChanged,
                facts.ChangedOnly, true);
            BindButton(root, "btnFactsParticipated", "Participated", true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleParticipated,
                facts.ParticipatedOnly, true);
            BindButton(root, "btnFactsDelta", facts.DeltaExpanded ? "Hide delta" : "Delta", true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleDelta,
                facts.DeltaExpanded, true);
            BindButton(root, "btnFactsTab", "Facts", true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsShowFacts,
                facts.ActiveTableTab == CharacterTestRuntimeAiLabFactsTableTab.Facts, true);
            BindButton(root, "btnCandidatesTab", "Candidates", true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsShowCandidates,
                facts.ActiveTableTab == CharacterTestRuntimeAiLabFactsTableTab.Candidates, true);
        }

        private void BindFactsAgents(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsAgentViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            for (int index = 0; index < FactsAgentCapacity; index++)
            {
                Fgui.GComponent row = root.GetChild("agentRow" + (index + 1))?.asCom;
                if (row == null)
                    continue;
                bool shown = index < count;
                row.visible = shown;
                if (!shown)
                    continue;
                CharacterTestRuntimeAiLabFactsAgentViewModel item = rows[index];
                SetFactsText(row, "nameText", item.DisplayName);
                SetFactsText(row, "stateText", item.StateText);
                SetFactsRowTone(row, item.Selected ? CharacterTestRuntimeAiLabFactsStatus.Active : CharacterTestRuntimeAiLabFactsStatus.Neutral);
                int entityId = item.EntityId;
                row.onClick.Set(() => _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSelectAgent,
                    entityId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            }
        }

        private void BindFactsCausalFlow(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsCausalStepViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            for (int index = 0; index < FactsCausalCapacity; index++)
            {
                Fgui.GComponent row = root.GetChild("causalStep" + (index + 1))?.asCom;
                if (row == null)
                    continue;
                bool shown = index < count;
                row.visible = shown;
                if (!shown)
                    continue;
                CharacterTestRuntimeAiLabFactsCausalStepViewModel item = rows[index];
                SetFactsText(row, "stepText", item.StepNumber);
                SetFactsText(row, "titleText", item.Title);
                SetFactsText(row, "primaryText", item.PrimaryText);
                SetFactsText(row, "detailText", item.DetailText);
                SetFactsRowTone(row, item.Selected ? CharacterTestRuntimeAiLabFactsStatus.Active : item.Status);
                BindTooltip(row, item.PrimaryText + "\n" + item.DetailText);
            }
        }

        private void BindFactsRows(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsFactViewModel[] rows,
            int scrollOffset)
        {
            int source = 0;
            int skipped = 0;
            for (int slot = 0; slot < FactsRowCapacity; slot++)
            {
                while (rows != null && source < rows.Length && !rows[source].Visible)
                    source++;
                while (rows != null && source < rows.Length && skipped < scrollOffset)
                {
                    source++;
                    while (source < rows.Length && !rows[source].Visible)
                        source++;
                    skipped++;
                }
                Fgui.GComponent row = root.GetChild("factRow" + (slot + 1))?.asCom;
                if (row == null)
                    continue;
                bool shown = rows != null && source < rows.Length;
                row.visible = shown;
                if (!shown)
                    continue;
                CharacterTestRuntimeAiLabFactsFactViewModel item = rows[source++];
                SetFactsText(row, "nameText", item.DisplayName);
                SetFactsText(row, "valueText", item.ValueText);
                SetFactsText(row, "sourceText", item.SourceText);
                SetFactsText(row, "metaText", item.FreshnessText + " | " + item.AuthorityText + " | " + item.ParticipationText);
                SetFactsRowTone(row, item.Selected ? CharacterTestRuntimeAiLabFactsStatus.Active : item.Status);
                BindFactsSelection(row, item.StableId);
                Fgui.GButton pin = row.GetChild("btnPin")?.asButton;
                if (pin != null)
                {
                    pin.text = item.Pinned ? "Pinned" : "Pin";
                    pin.selected = item.Pinned;
                    string stableId = item.StableId;
                    pin.onClick.Set(() => _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsTogglePin,
                        stableId)));
                }
            }
        }

        private void BindFactsCandidateRows(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsCandidateViewModel[] rows,
            int scrollOffset)
        {
            int source = 0;
            int skipped = 0;
            for (int slot = 0; slot < FactsCandidateCapacity; slot++)
            {
                while (rows != null && source < rows.Length && !rows[source].Visible)
                    source++;
                while (rows != null && source < rows.Length && skipped < scrollOffset)
                {
                    source++;
                    while (source < rows.Length && !rows[source].Visible)
                        source++;
                    skipped++;
                }
                Fgui.GComponent row = root.GetChild("candidateRow" + (slot + 1))?.asCom;
                if (row == null)
                    continue;
                bool shown = rows != null && source < rows.Length;
                row.visible = shown;
                if (!shown)
                    continue;
                CharacterTestRuntimeAiLabFactsCandidateViewModel item = rows[source++];
                SetFactsText(row, "nameText", item.DisplayName);
                SetFactsText(row, "stateText", item.StateText);
                SetFactsText(row, "metricsText", "S " + item.ScoreText + "  P " + item.PriorityText + "  C " + item.CostText);
                SetFactsText(row, "reasonText", item.LayerText + " | " + item.ReasonCode + " | " + item.RelatedFactsText);
                BindTooltip(
                    row,
                    item.StableId + "\n" + item.ReasonCode + "\n" + item.RelatedFactsText);
                SetFactsRowTone(row, item.Selected ? CharacterTestRuntimeAiLabFactsStatus.Active : item.Status);
                BindFactsSelection(row, item.StableId);
            }
        }

        private void BindFactsDeltaRows(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsFactViewModel[] rows,
            bool expanded,
            int scrollOffset)
        {
            int available = Math.Max(0, (rows?.Length ?? 0) - scrollOffset);
            int count = expanded ? Math.Min(available, FactsDeltaCapacity) : 0;
            Fgui.GObject header = root.GetChild("deltaHeader");
            if (header != null)
                header.visible = expanded;
            for (int index = 0; index < FactsDeltaCapacity; index++)
            {
                Fgui.GComponent row = root.GetChild("deltaRow" + (index + 1))?.asCom;
                if (row == null)
                    continue;
                bool shown = index < count;
                row.visible = shown;
                if (!shown)
                    continue;
                CharacterTestRuntimeAiLabFactsFactViewModel item = rows[scrollOffset + index];
                SetFactsText(row, "nameText", item.DisplayName);
                SetFactsText(row, "valueText", item.PreviousValueText + " -> " + item.ValueText);
                SetFactsText(row, "metaText", item.ChangeText + " @F" + item.LastChangedFrame);
                SetFactsRowTone(row, item.Selected ? CharacterTestRuntimeAiLabFactsStatus.Active : item.Status);
                if (row.displayObject != null)
                    row.displayObject.onMouseWheel.Set(OnFactsDeltaMouseWheel);
            }
        }

        private void ConfigureFactsScrolling(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel viewModel)
        {
            if (_factsScrollRoot != null
                && _factsScrollRoot != root
                && _factsScrollRoot.displayObject != null)
            {
                _factsScrollRoot.displayObject.onMouseWheel.Set((Fgui.EventCallback1)null);
            }
            _factsScrollRoot = root;
            _factsScrollViewModel = viewModel;
            _factsFactScrollOffset = ClampFactsOffset(
                viewModel.FactScrollOffset,
                CountVisibleFacts(viewModel.FactRows),
                FactsRowCapacity);
            _factsCandidateScrollOffset = ClampFactsOffset(
                viewModel.CandidateScrollOffset,
                CountVisibleCandidates(viewModel.CandidateRows),
                FactsCandidateCapacity);
            _factsDeltaScrollOffset = ClampFactsOffset(
                viewModel.DeltaScrollOffset,
                viewModel.DeltaRows?.Length ?? 0,
                FactsDeltaCapacity);
            if (root?.displayObject != null)
                root.displayObject.onMouseWheel.Set(OnFactsMouseWheel);
        }

        private void OnFactsMouseWheel(Fgui.EventContext context)
        {
            float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
            if (delta == 0f || _factsScrollViewModel == null)
                return;
            int rowDelta = delta < 0f ? 1 : -1;
            bool changed = _factsScrollViewModel.ActiveTableTab == CharacterTestRuntimeAiLabFactsTableTab.Candidates
                ? ScrollFactsCandidates(rowDelta)
                : ScrollFacts(rowDelta);
            if (changed)
                context.StopPropagation();
        }

        private void OnFactsDeltaMouseWheel(Fgui.EventContext context)
        {
            float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
            if (delta != 0f && ScrollFactsDelta(delta < 0f ? 1 : -1))
                context.StopPropagation();
        }

        private bool ScrollFacts(int rowDelta)
        {
            int next = ClampFactsOffset(
                _factsFactScrollOffset + rowDelta,
                CountVisibleFacts(_factsScrollViewModel?.FactRows),
                FactsRowCapacity);
            if (next == _factsFactScrollOffset)
                return false;
            _factsFactScrollOffset = next;
            BindFactsRows(_factsScrollRoot, _factsScrollViewModel.FactRows, next);
            PersistFactsScrollOffsets();
            return true;
        }

        private bool ScrollFactsCandidates(int rowDelta)
        {
            int next = ClampFactsOffset(
                _factsCandidateScrollOffset + rowDelta,
                CountVisibleCandidates(_factsScrollViewModel?.CandidateRows),
                FactsCandidateCapacity);
            if (next == _factsCandidateScrollOffset)
                return false;
            _factsCandidateScrollOffset = next;
            BindFactsCandidateRows(_factsScrollRoot, _factsScrollViewModel.CandidateRows, next);
            PersistFactsScrollOffsets();
            return true;
        }

        private bool ScrollFactsDelta(int rowDelta)
        {
            int next = ClampFactsOffset(
                _factsDeltaScrollOffset + rowDelta,
                _factsScrollViewModel?.DeltaRows?.Length ?? 0,
                FactsDeltaCapacity);
            if (next == _factsDeltaScrollOffset)
                return false;
            _factsDeltaScrollOffset = next;
            BindFactsDeltaRows(
                _factsScrollRoot,
                _factsScrollViewModel.DeltaRows,
                _factsScrollViewModel.DeltaExpanded,
                next);
            PersistFactsScrollOffsets();
            return true;
        }

        private void PersistFactsScrollOffsets()
        {
            string payload = _factsFactScrollOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "," + _factsCandidateScrollOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "," + _factsDeltaScrollOffset.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsScroll,
                payload));
        }

        private static int ClampFactsOffset(int offset, int visibleCount, int capacity)
        {
            return Mathf.Clamp(offset, 0, Mathf.Max(0, visibleCount - capacity));
        }

        private static int CountVisibleFacts(CharacterTestRuntimeAiLabFactsFactViewModel[] rows)
        {
            int count = 0;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i].Visible)
                    count++;
            }
            return count;
        }

        private static int CountVisibleCandidates(CharacterTestRuntimeAiLabFactsCandidateViewModel[] rows)
        {
            int count = 0;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i].Visible)
                    count++;
            }
            return count;
        }

        private void BindFactsCommitPhases(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabFactsCommitPhaseViewModel[] rows)
        {
            int count = Math.Min(rows?.Length ?? 0, FactsCommitCapacity);
            for (int index = 0; index < FactsCommitCapacity; index++)
            {
                Fgui.GComponent row = root.GetChild("commitPhase" + (index + 1))?.asCom;
                if (row == null)
                    continue;
                bool shown = index < count;
                row.visible = shown;
                if (!shown)
                    continue;
                CharacterTestRuntimeAiLabFactsCommitPhaseViewModel item = rows[index];
                SetFactsText(row, "titleText", item.Title);
                SetFactsText(row, "stateText", item.StateCode);
                SetFactsText(row, "frameText", item.EvidenceFrame >= 0 ? "@F" + item.EvidenceFrame : "Not observed");
                SetFactsText(row, "reasonText", item.ReasonCode);
                SetFactsRowTone(row, item.Selected ? CharacterTestRuntimeAiLabFactsStatus.Active : item.Status);
                BindTooltip(row, item.ReasonCode);
            }
        }

        private void BindFactsSelection(Fgui.GComponent row, string stableId)
        {
            row.onClick.Set(() => _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSelect,
                stableId)));
        }

        private void BindFactsButton(
            Fgui.GComponent root,
            string childName,
            string label,
            bool enabled,
            string commandId)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.text = label;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.onClick.Set(() =>
            {
                if (enabled)
                    _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null));
            });
        }

        private static void SetFactsText(Fgui.GComponent root, string childName, string value)
        {
            Fgui.GTextField text = root.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static void SetFactsRowTone(
            Fgui.GComponent row,
            CharacterTestRuntimeAiLabFactsStatus status)
        {
            Fgui.GGraph background = row.GetChild("rowBackground")?.asGraph;
            if (background == null)
                return;
            string html = status switch
            {
                CharacterTestRuntimeAiLabFactsStatus.Active => "#153E4A",
                CharacterTestRuntimeAiLabFactsStatus.Success => "#123B33",
                CharacterTestRuntimeAiLabFactsStatus.Warning => "#44351A",
                CharacterTestRuntimeAiLabFactsStatus.Error => "#481D26",
                _ => "#0C1D27"
            };
            if (ColorUtility.TryParseHtmlString(html, out Color color))
                background.color = color;
        }
    }
}
