using System;
using System.Globalization;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiArenaBehaviorGroupPanelCommandSink : IMxUiCommandSink
    {
        private readonly CharacterTestArenaSessionController _controller;
        private readonly Func<bool> _refreshPanel;
        private readonly Func<bool> _closePanel;

        internal CharacterTestFairyGuiArenaBehaviorGroupPanelCommandSink(
            CharacterTestArenaSessionController controller,
            Func<bool> refreshPanel = null,
            Func<bool> closePanel = null)
        {
            _controller = controller;
            _refreshPanel = refreshPanel;
            _closePanel = closePanel;
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId,
                -1,
                0,
                0,
                0,
                0);
        }

        public int CommandCount { get; private set; }
        public string LastStatusMessage { get; private set; } = string.Empty;
        public MxUiCommand LastCommand { get; private set; }
        internal CharacterTestFairyGuiArenaBehaviorGroupPanelState State { get; private set; }

        internal void SelectDraft(int draftId)
        {
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                draftId > 0 ? draftId : CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId,
                State.FilterIndex,
                0,
                0,
                0,
                0,
                State.SearchText);
            LastStatusMessage = "Behavior group selected.";
        }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;
            bool handled = true;
            string commandId = CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.Normalize(
                command.CommandId);
            switch (commandId)
            {
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.Close:
                    LastStatusMessage = "Back to team setup.";
                    _closePanel?.Invoke();
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.DraftPrev:
                    SelectDraftByDirection(-1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.DraftNext:
                    SelectDraftByDirection(1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.FilterPrev:
                    SelectFilter(-1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.FilterNext:
                    SelectFilter(1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.SelectCapabilitySlot:
                    SelectCapabilitySlot(command.Payload as CharacterTestFairyGuiArenaBehaviorGroupPanelPayload);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ApplySearch:
                    ApplySearch(command.Payload as CharacterTestFairyGuiArenaBehaviorGroupPanelPayload);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ClearSearch:
                    ClearSearch();
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.FillBasic:
                    FillBasicGroup();
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.LibraryPrev:
                    PageLibrary(-1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.LibraryNext:
                    PageLibrary(1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.EntryPrev:
                    PageEntries(-1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.EntryNext:
                    PageEntries(1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.SelectLibraryRow:
                    SelectLibraryRow(command.Payload as CharacterTestFairyGuiArenaBehaviorGroupPanelPayload);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.SelectEntryRow:
                    SelectEntryRow(command.Payload as CharacterTestFairyGuiArenaBehaviorGroupPanelPayload);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.AddSelectedBehavior:
                    AddSelectedBehavior();
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.DuplicateEntry:
                    MutateEntry((
                        CharacterTestArenaBehaviorGroupDraftStore store,
                        int draftId,
                        int entryIndex,
                        out string message) =>
                        store.TryDuplicateEntry(draftId, entryIndex, out message));
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.MoveEntryUp:
                    MoveEntry(-1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.MoveEntryDown:
                    MoveEntry(1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ToggleEntryDisabled:
                    MutateEntry((
                        CharacterTestArenaBehaviorGroupDraftStore store,
                        int draftId,
                        int entryIndex,
                        out string message) =>
                        store.TryToggleEntryDisabled(draftId, entryIndex, out message));
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.DeleteEntry:
                    DeleteEntry();
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ResetParameters:
                    MutateEntry((
                        CharacterTestArenaBehaviorGroupDraftStore store,
                        int draftId,
                        int entryIndex,
                        out string message) =>
                        store.TryResetEntryParameters(draftId, entryIndex, out message));
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ParameterPrev:
                    AdjustParameter(
                        command.Payload as CharacterTestFairyGuiArenaBehaviorGroupPanelPayload,
                        -1);
                    break;
                case CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ParameterNext:
                    AdjustParameter(
                        command.Payload as CharacterTestFairyGuiArenaBehaviorGroupPanelPayload,
                        1);
                    break;
                default:
                    LastStatusMessage = "Unsupported behavior group command: " + commandId;
                    handled = false;
                    break;
            }

            if (handled
                && !string.Equals(
                    commandId,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.Close,
                    StringComparison.Ordinal))
            {
                _refreshPanel?.Invoke();
            }
        }

        private CharacterTestArenaBehaviorGroupDraftStore Store => _controller?.Config?.BehaviorGroups;

        private void SelectDraftByDirection(int direction)
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null)
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            SelectDraft(store.SelectNextDraft(State.DraftId, direction));
        }

        private void SelectFilter(int direction)
        {
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelBuilder.NormalizeFilterIndex(
                    State.FilterIndex + (direction < 0 ? -1 : 1)),
                0,
                State.EntryPage,
                0,
                State.SelectedEntryIndex,
                State.SearchText);
            LastStatusMessage = "Capability slot changed.";
        }

        private void SelectCapabilitySlot(CharacterTestFairyGuiArenaBehaviorGroupPanelPayload payload)
        {
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                ClampRow(payload, CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotCount),
                0,
                State.EntryPage,
                0,
                State.SelectedEntryIndex,
                State.SearchText);
            LastStatusMessage = "Capability slot selected.";
        }

        private void ApplySearch(CharacterTestFairyGuiArenaBehaviorGroupPanelPayload payload)
        {
            string text = payload != null ? payload.Text : string.Empty;
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                State.FilterIndex,
                0,
                State.EntryPage,
                0,
                State.SelectedEntryIndex,
                text);
            LastStatusMessage = string.IsNullOrWhiteSpace(text)
                ? "Card search cleared."
                : "Card search applied.";
        }

        private void ClearSearch()
        {
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                State.FilterIndex,
                0,
                State.EntryPage,
                0,
                State.SelectedEntryIndex,
                string.Empty);
            LastStatusMessage = "Card search cleared.";
        }

        private void PageLibrary(int direction)
        {
            int page = State.LibraryPage + (direction < 0 ? -1 : 1);
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                State.FilterIndex,
                page < 0 ? 0 : page,
                State.EntryPage,
                0,
                State.SelectedEntryIndex,
                State.SearchText);
            LastStatusMessage = "Card library page changed.";
        }

        private void PageEntries(int direction)
        {
            int page = State.EntryPage + (direction < 0 ? -1 : 1);
            int selectedEntry = Math.Max(0, page) * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount;
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                State.FilterIndex,
                State.LibraryPage,
                page < 0 ? 0 : page,
                State.SelectedLibraryRow,
                selectedEntry,
                State.SearchText);
            LastStatusMessage = "Deck page changed.";
        }

        private void SelectLibraryRow(CharacterTestFairyGuiArenaBehaviorGroupPanelPayload payload)
        {
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                State.FilterIndex,
                State.LibraryPage,
                State.EntryPage,
                ClampRow(payload, CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowCount),
                State.SelectedEntryIndex,
                State.SearchText);
            LastStatusMessage = "Behavior card selected.";
        }

        private void SelectEntryRow(CharacterTestFairyGuiArenaBehaviorGroupPanelPayload payload)
        {
            int row = ClampRow(payload, CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount);
            int selectedEntry = State.EntryPage
                * CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount
                + row;
            State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                State.DraftId,
                State.FilterIndex,
                State.LibraryPage,
                State.EntryPage,
                State.SelectedLibraryRow,
                selectedEntry,
                State.SearchText);
            LastStatusMessage = "Deck card selected.";
        }

        private void AddSelectedBehavior()
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null)
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition =
                CharacterTestFairyGuiArenaBehaviorGroupPanelViewModelBuilder.ResolveSelectedDefinition(
                    store,
                    State);
            if (definition == null)
            {
                LastStatusMessage = "No behavior card selected.";
                return;
            }

            bool ok = store.TryAddEntry(State.DraftId, definition.BehaviorId, out string message);
            LastStatusMessage = ok ? "Card added to current deck." : message;
            if (ok && store.TryGetDraft(State.DraftId, out CharacterTestArenaBehaviorGroupDraft draft))
            {
                int index = Math.Max(0, draft.Profile.Entries.Count - 1);
                State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                    State.DraftId,
                    State.FilterIndex,
                    State.LibraryPage,
                    index / CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount,
                    State.SelectedLibraryRow,
                    index,
                    State.SearchText);
            }
        }

        private void FillBasicGroup()
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null || !store.TryGetDraft(State.DraftId, out CharacterTestArenaBehaviorGroupDraft draft))
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report =
                CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.Analyze(
                    draft.Profile,
                    store.DefinitionCatalog);
            int added = 0;
            added += AddIfMissing(
                store,
                report,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);
            added += AddIfMissing(
                store,
                report,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);
            added += AddIfMissing(
                store,
                report,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle);
            added += AddIfMissing(
                store,
                report,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);

            LastStatusMessage = added > 0
                ? "Basic deck filled. Added " + added.ToString(CultureInfo.InvariantCulture) + " card(s)."
                : "Basic deck already has required cards.";
            if (added > 0 && store.TryGetDraft(State.DraftId, out draft))
            {
                int index = Math.Max(0, draft.Profile.Entries.Count - 1);
                State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                    State.DraftId,
                    State.FilterIndex,
                    State.LibraryPage,
                    index / CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount,
                    State.SelectedLibraryRow,
                    index,
                    State.SearchText);
            }
        }

        private int AddIfMissing(
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport report,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            string behaviorId)
        {
            if (report != null
                && !report.HasMissing(capability)
                && !report.HasDisabledOnly(capability))
            {
                return 0;
            }

            return store.TryAddEntry(State.DraftId, behaviorId, out _) ? 1 : 0;
        }

        private void DeleteEntry()
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null)
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            bool ok = store.TryRemoveEntry(State.DraftId, State.SelectedEntryIndex, out string message);
            LastStatusMessage = ok ? "Deck card deleted." : message;
            if (ok)
            {
                int next = Math.Max(0, State.SelectedEntryIndex - 1);
                State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                    State.DraftId,
                    State.FilterIndex,
                    State.LibraryPage,
                    State.EntryPage,
                    State.SelectedLibraryRow,
                    next,
                    State.SearchText);
            }
        }

        private void MoveEntry(int direction)
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null)
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            bool ok = store.TryMoveEntry(
                State.DraftId,
                State.SelectedEntryIndex,
                direction,
                out string message);
            LastStatusMessage = ok ? "Deck card order changed." : message;
            if (ok)
            {
                int next = Math.Max(0, State.SelectedEntryIndex + (direction < 0 ? -1 : 1));
                State = new CharacterTestFairyGuiArenaBehaviorGroupPanelState(
                    State.DraftId,
                    State.FilterIndex,
                    State.LibraryPage,
                    next / CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount,
                    State.SelectedLibraryRow,
                    next,
                    State.SearchText);
            }
        }

        private void AdjustParameter(
            CharacterTestFairyGuiArenaBehaviorGroupPanelPayload payload,
            int direction)
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null)
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            bool ok = store.TryAdjustParameter(
                State.DraftId,
                State.SelectedEntryIndex,
                ClampRow(payload, CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ParameterRowCount),
                direction,
                out string message);
            LastStatusMessage = ok ? "Card parameter updated." : message;
        }

        private void MutateEntry(EntryMutation mutation)
        {
            CharacterTestArenaBehaviorGroupDraftStore store = Store;
            if (store == null)
            {
                LastStatusMessage = "Behavior group drafts are unavailable.";
                return;
            }

            bool ok = mutation(store, State.DraftId, State.SelectedEntryIndex, out string message);
            LastStatusMessage = ok
                ? (string.IsNullOrWhiteSpace(message) ? "Deck updated." : TranslateStoreMessage(message))
                : message;
        }

        private static string TranslateStoreMessage(string message)
        {
            switch (message)
            {
                case "Entry disabled.":
                    return "Deck card disabled.";
                case "Entry enabled.":
                    return "Deck card enabled.";
                case "Entry parameters reset.":
                    return "Card parameters reset.";
                default:
                    return message;
            }
        }

        private static int ClampRow(
            CharacterTestFairyGuiArenaBehaviorGroupPanelPayload payload,
            int rowCount)
        {
            int row = payload != null ? payload.RowIndex : 0;
            if (row < 0)
                return 0;
            if (row >= rowCount)
                return rowCount - 1;
            return row;
        }

        private delegate bool EntryMutation(
            CharacterTestArenaBehaviorGroupDraftStore store,
            int draftId,
            int entryIndex,
            out string message);
    }
}
