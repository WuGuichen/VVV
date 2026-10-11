using System;
using System.Linq;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [Serializable]
    internal sealed class CharacterTestTacticalSummaryReturn
    {
        public string WorkspaceId, CardId, NodeId, DefinitionId, EventId, Field;
        public string ConfiguredTestCardId;
        public int TeamId, SlotIndex;
        public bool NativeExecutionView;
        public bool StandaloneCard, ManualCardTest, CardLibrary;
        // This bookmark is serialized by Json.NET, not Unity's field serializer.
        public System.Collections.Generic.Dictionary<string, int> TestActions { get; set; }
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> SlotPreviews { get; set; }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal bool TrySaveTacticalSummaryReturn(out CharacterTestTacticalSummaryReturn bookmark, out string issue)
        {
            bookmark = null; issue = "当前制作上下文不可用。";
            bool library = IsConfiguredTestCardEditing && ShellCoordinator?.CurrentState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.TacticalLibrary;
            string cardId = library ? _configuredDraftId : _activeTacticalCardId;
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly || IsHistoricalFrameSelected
                || !DraftWorkspace.Cards.TryGetValue(cardId ?? string.Empty, out var card)) return false;
            var token = card.HasCompositionSource && !library ? _compositionEditingSession.Token : new CharacterTestTacticalEditToken(DraftWorkspace, card);
            if (IsConfiguredTestCardEditing ? !SaveConfiguredRecovery(out issue) : !SaveTacticalSource(token, out issue)) return false;
            var session = card.HasCompositionSource && !library ? _compositionEditingSession : null;
            bookmark = new CharacterTestTacticalSummaryReturn { WorkspaceId = DraftWorkspace.WorkspaceId, CardId = card.CardDraftId,
                TeamId = SelectedSetupTeamId, SlotIndex = SelectedSetupSlotIndex, NodeId = session?.SelectedNodeId,
                NativeExecutionView = NativeTacticalExecutionView,
                StandaloneCard = HasStandaloneTacticalCard, ManualCardTest = TacticalCardTestMode,
                CardLibrary = library,
                ConfiguredTestCardId = this.ConfiguredTestCardId,
                TestActions = new System.Collections.Generic.Dictionary<string, int>(_tacticalTestActions),
                SlotPreviews = _tacticalSlotPreviews.ToDictionary(p => p.Key,
                    p => new System.Collections.Generic.Dictionary<string, string>(p.Value, StringComparer.Ordinal), StringComparer.Ordinal),
                DefinitionId = session?.Target == CharacterTestTacticalEditTarget.SharedDefinition ? session.SelectedDefinition?.DefinitionId : null,
                EventId = _tacticalEditingSession?.Selection.FirstOrDefault(),
                Field = session?.Target == CharacterTestTacticalEditTarget.CardActivation ? "activationConditionRoot"
                    : session?.Target == CharacterTestTacticalEditTarget.NodeEnd ? "endCondition"
                    : session?.Target == CharacterTestTacticalEditTarget.NodeCondition ? "condition" : "events" };
            return true;
        }

        internal bool TryRestoreTacticalSummaryReturn(CharacterTestTacticalSummaryReturn bookmark, out string issue)
        {
            issue = "等待原制作工作副本和界面就绪。";
            if (bookmark?.ConfiguredTestCardId != null && !IsConfiguredTestCardEditing)
            {
                if (!SelectSetupSpawnSlot(bookmark.TeamId, bookmark.SlotIndex)
                    || !OpenConfiguredCard(bookmark.ConfiguredTestCardId, out _, out issue)) return false;
                bookmark.WorkspaceId = DraftWorkspace.WorkspaceId; bookmark.CardId = _configuredDraftId;
            }
            if (bookmark == null || DraftWorkspace?.WorkspaceId != bookmark.WorkspaceId
                || !DraftWorkspace.Cards.TryGetValue(bookmark.CardId, out var card)) return false;
            var host = UnityEngine.Resources.FindObjectsOfTypeAll<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>().FirstOrDefault(h => h.gameObject.scene == gameObject.scene && h.CommandSink != null);
            if (host == null || !SelectSetupSpawnSlot(bookmark.TeamId, bookmark.SlotIndex)) return false;
            _tacticalTestActions.Clear();
            if (bookmark.TestActions != null) foreach (var pair in bookmark.TestActions) _tacticalTestActions[pair.Key] = pair.Value;
            _tacticalSlotPreviews.Clear();
            if (bookmark.SlotPreviews != null)
                foreach (var pair in bookmark.SlotPreviews)
                    _tacticalSlotPreviews[pair.Key] = new System.Collections.Generic.Dictionary<string, string>(pair.Value, StringComparer.Ordinal);
            _tacticalSlotPreviewRevision++; _tacticalPrepared.Clear(); _tacticalCompiled.Clear();
            NativeTacticalExecutionView = bookmark.NativeExecutionView;
            if (bookmark.StandaloneCard)
            {
                TacticalLibrarySelectedKey = bookmark.ConfiguredTestCardId == null ? "draft:" + bookmark.CardId : "test:" + bookmark.ConfiguredTestCardId;
                if (bookmark.ManualCardTest && bookmark.ConfiguredTestCardId != null) host.CommandSink.OpenTacticalCardTest();
                else if (!host.CommandSink.OpenTacticalLibraryCard(TacticalLibrarySelectedKey)) return false;
                if (!HasStandaloneTacticalCard || TacticalCardContext.CardId != bookmark.CardId) return false;
            }
            else
            {
                if (!DraftWorkspace.TryGetGroupForSlot(bookmark.TeamId, bookmark.SlotIndex, out var group)) return false;
                int index = group.Entries.FindIndex(e => e.CardDraftId == bookmark.CardId);
                if (index < 0) { issue = "原卡引用已变化，请在保存的工作副本中重新选择。"; return false; }
                host.CommandSink.SetWorkbenchEntryIndex(index);
                host.CommandSink.Enqueue(new MxFramework.UI.MxUiCommand(default(MxFramework.UI.MxUiViewId),
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab, null));
            }
            if (card.HasCompositionSource)
                _pendingCurrentTacticalLocation = new CharacterTestTacticalSourceLocation(card.CardDraftId, bookmark.NodeId,
                    bookmark.Field, bookmark.DefinitionId, evt: bookmark.EventId);
            BuildTacticalPreview(card); RefreshTacticalPreview();
            if (bookmark.CardLibrary) host.CommandSink.OpenTacticalLibrary();
            issue = "已返回原卡和编辑位置；重新编译后可开始新测试。";
            _statusMessage = issue; return true;
        }
    }
}
