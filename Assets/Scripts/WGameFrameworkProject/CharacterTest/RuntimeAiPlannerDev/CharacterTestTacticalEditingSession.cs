using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Presentation session; source ownership and history remain in DraftWorkspace.</summary>
    internal sealed partial class CharacterTestTacticalEditingSession
    {
        private readonly CharacterTestRuntimeAiPlannerDevSceneController _controller;
        internal CharacterTestTacticalEditingSession(CharacterTestRuntimeAiPlannerDevSceneController controller, string cardId)
        { _controller = controller; CardId = cardId; }
        internal CharacterTestRuntimeAiPlannerDevSceneController Controller => _controller;
        internal string CardId { get; }
        internal string CardName { get; private set; }
        internal int PointCost { get; private set; }
        internal float PointCooldownSeconds { get; private set; }
        internal Action<string, string> Locate { get; set; }
        internal CharacterTestTacticalEditToken Token { get; private set; }
        internal CharacterTestTacticalAuthoringPreview Preview { get; private set; }
        internal CharacterTestTacticalAuthoringSource Source => Preview.Source;
        internal CharacterTestTacticalCompositionEditingSession Composition => _controller?.CompositionEditingSession;
        internal bool Focused => Composition?.EventFocusActive == true && Token.Target == CharacterTestTacticalEditTarget.SharedDefinition;
        internal bool CanEdit => _controller != null && Preview?.IsFrozen != true && !_controller.IsHistoricalFrameSelected
            && _controller.DraftWorkspace?.IsReadOnly == false
            && Token.Target != CharacterTestTacticalEditTarget.Composition
            && (Token.Target != CharacterTestTacticalEditTarget.SharedDefinition || _controller.CompositionEditingSession?.SharedConfirmed == true);
        internal bool ConditionsOpen { get; set; }
        internal float InspectorHeight { get; set; } = 600f;
        internal string ConditionTimeline { get; private set; }
        internal string ConditionEvent { get; private set; }
        internal void BindEmbeddedEvent(CharacterTestTacticalEditingSession owner, string timeline, string eventId)
        {
            Token = owner.Token; Preview = owner.Preview; CardName = owner.CardName;
            ConditionsOpen = true; ConditionTimeline = timeline; ConditionEvent = eventId;
        }
        internal void OpenConditions(string timeline, string eventId) { ConditionTimeline = timeline; ConditionEvent = eventId; ShowConditions(true); }
        internal void OpenCardActivation()
        {
            var composition = _controller.CompositionEditingSession;
            if (composition != null) composition.Select(composition.Document.Root.NodeId, CharacterTestTacticalEditTarget.CardActivation);
            else OpenConditions(Source.EntryTimelineId, "$activation");
        }
        internal HashSet<string> Selection { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal BehaviorRecipeTriggerEventSignalOptionViewModel[] CompositionSignalOptions()
        {
            var composition = _controller.CompositionEditingSession;
            if (composition?.Document == null || Token.Target == CharacterTestTacticalEditTarget.CardActivation)
                return Array.Empty<BehaviorRecipeTriggerEventSignalOptionViewModel>();
            var result = new List<BehaviorRecipeTriggerEventSignalOptionViewModel>();
            foreach (var use in CharacterTestTacticalCompositionEdits.Nodes(composition.Document.Root).Where(n => n.Kind == "Reference"))
            {
                var definition = composition.Document.Definitions.FirstOrDefault(d => d.DefinitionId == use.DefinitionId);
                if (definition == null) continue;
                foreach (var evt in definition.Source.Timelines.SelectMany(t => t.Events))
                    result.Add(new BehaviorRecipeTriggerEventSignalOptionViewModel { EventId = use.NodeId + "/" + evt.EventId,
                        DisplayText = definition.Source.Name + " / " + evt.EventId });
            }
            return result.ToArray();
        }
        internal void ApplyCondition(CharacterTestTacticalAuthoringSource source, string timeline, string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field, int index, CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value)
        {
            IEnumerable<string> sources = null;
            if (Preview.IsConditionProjection && Token.Target != CharacterTestTacticalEditTarget.CardActivation)
            {
                var ids = CompositionSignalOptions().Select(o => o.EventId).ToList();
                foreach (string id in ids.ToArray())
                    if (id.StartsWith(Token.NodeId + "/", StringComparison.Ordinal)) ids.Add(id.Substring(Token.NodeId.Length + 1));
                sources = ids;
            }
            CharacterTestTacticalConditionEditing.Apply(source, timeline, eventId, field, index, value, sources);
        }
        internal string Message { get; private set; } = string.Empty;

        internal void Bind(CharacterTestRuntimeAiLabDraftWorkspace workspace, CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestTacticalAuthoringPreview preview, CharacterTestTacticalEditToken? token = null)
        {
            var next = token ?? new CharacterTestTacticalEditToken(workspace, card);
            if (Token.ContextSerial != next.ContextSerial || Token.Target != next.Target || Token.NodeId != next.NodeId)
            { Selection.Clear(); ConditionsOpen = false; ConditionTimeline = null; ConditionEvent = null; }
            Token = next; Preview = preview; CardName = card.DisplayName;
            PointCost = card.Cost; PointCooldownSeconds = card.CooldownSeconds;
            if (next.Target == CharacterTestTacticalEditTarget.CardActivation || next.Target == CharacterTestTacticalEditTarget.NodeCondition
                || next.Target == CharacterTestTacticalEditTarget.NodeEnd)
            { ConditionsOpen = true; ConditionTimeline = preview.Source.EntryTimelineId; ConditionEvent = "$activation"; }
        }

        internal bool Commit(CharacterTestTacticalEditToken token, string label, Action<CharacterTestTacticalAuthoringSource> edit, string name = null)
        {
            var focus = Focused ? Composition : null;
            string timeline = focus?.FocusTimelineId, eventId = focus?.FocusEventId, blockId = focus?.FocusBlockId;
            bool success = _controller.EditTacticalSource(token, label, source => {
                if (blockId != null)
                {
                    var block = CharacterTestTacticalBlockEdits.Ensure(source, timeline, eventId, blockId);
                    if (block.Timing == null && Preview.FocusBlock?.Timing is CharacterTestTacticalBlockTiming timing)
                        block.Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = timing.MinimumFrames, DurationFrames = timing.DurationFrames };
                    source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4;
                }
                edit?.Invoke(source);
                var cycle = CharacterTestTacticalAuthoringCompiler.ValidateShared(source).FirstOrDefault(d => d.Code == "AnchorCycle");
                if (cycle.Code != null) throw new ArgumentException(cycle.Message);
            }, out string message, name);
            Message = message;
            if (!success) _controller.RefreshTacticalPreview();
            return success;
        }
        internal bool EditPointField(CharacterTestTacticalEditToken token, string field, string text)
        {
            if (!CharacterTestTacticalPointAuthoringFields.TryEdit(Source, PointCost, PointCooldownSeconds,
                field, text, out var contract, out int cost, out float cooldown, out string message))
            { Feedback(message); return false; }
            bool success = _controller.EditPointMove(token, contract, cost, cooldown, out message);
            Message = message;
            if (!success) _controller.RefreshTacticalPreview();
            return success;
        }
        internal void Feedback(string value) { Message = value; _controller.RefreshTacticalPreview(); }
        internal void ShowConditions(bool value) { ConditionsOpen = value; _controller.RefreshTacticalPreview(); }
        internal void Save() { _controller.SaveTacticalSource(Token, out string message); Message = message; }
        internal void Undo(bool redo) { _controller.UndoTacticalSource(Token, redo, out string message); Message = message; }
        internal void Duplicate() { _controller.DuplicateTacticalSource(Token, out string message); Message = message; }
        internal string[] Selected(string eventId) => Selection.Count > 0 ? Selection.ToArray()
            : string.IsNullOrWhiteSpace(eventId) ? Array.Empty<string>() : new[] { eventId };
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private string _activeTacticalCardId = string.Empty;
        private CharacterTestTacticalEditingSession _tacticalEditingSession;

        private bool TacticalContextValid(CharacterTestTacticalEditToken token)
            => !IsHistoricalFrameSelected && DraftWorkspace != null && !DraftWorkspace.IsReadOnly
                && DraftWorkspace.WorkspaceId == token.WorkspaceId && DraftWorkspace.CurrentRevision == token.WorkspaceRevision
                && token.CardId == _activeTacticalCardId
                && DraftWorkspace.Cards.TryGetValue(token.CardId, out var card) && card.CurrentRevision == token.CardRevision
                && (token.Target == CharacterTestTacticalEditTarget.LegacySource || _compositionEditingSession?.Accepts(token) == true
                    || _compositionEditingSession?.AcceptsActivation(token) == true);

        internal bool EditTacticalSource(CharacterTestTacticalEditToken token, string label,
            Action<CharacterTestTacticalAuthoringSource> edit, out string message, string name = null)
        {
            if (!TacticalContextValid(token)) { message = "编辑对象或版本已变化，请重新选择。"; return false; }
            if (token.Target == CharacterTestTacticalEditTarget.SharedDefinition && name != null)
            {
                string sharedName = name.Trim();
                if (sharedName.Length == 0) { message = "封装名称不能为空。"; return false; }
                edit = source => source.Name = sharedName;
            }
            bool result = token.Target == CharacterTestTacticalEditTarget.CardActivation && _compositionEditingSession != null
                ? _compositionEditingSession.EditActivation(token, edit, out message)
                : token.Target == CharacterTestTacticalEditTarget.LegacySource
                ? DraftWorkspace.TryEditTacticalSource(token, label, edit, out message, name)
                : _compositionEditingSession.EditInternal(token, edit, out message);
            if (result) _draftPersistence.MarkDirty();
            _statusMessage = message;
            TacticalPresentationRevision++;
            return result;
        }

        internal bool EditPointMove(CharacterTestTacticalEditToken token, TacticalCard.TacticalPointMoveAuthoring contract,
            int cost, float cooldown, out string message)
        {
            if (!TacticalContextValid(token) || token.Target != CharacterTestTacticalEditTarget.LegacySource)
            { message = "地点作者输入的选择或版本已变化，请重新选择。"; return false; }
            bool result = DraftWorkspace.TryEditPointMove(token, contract, cost, cooldown, out message);
            if (result) _draftPersistence.MarkDirty();
            _statusMessage = message; TacticalPresentationRevision++;
            return result;
        }

        internal bool SaveTacticalSource(CharacterTestTacticalEditToken token, out string message)
        {
            if (!TacticalContextValid(token)) { message = "保存上下文已变化。"; return false; }
            if (IsConfiguredTestCardEditing)
            { bool saved = SaveConfiguredCard(out message); _statusMessage = message; return saved; }
            int previous = DraftWorkspace.LastExplicitSaveRevision;
            DraftWorkspace.LastExplicitSaveRevision = DraftWorkspace.CurrentRevision;
            _draftPersistence.MarkDirty();
            bool result = _draftPersistence.Flush();
            if (!result) DraftWorkspace.LastExplicitSaveRevision = previous;
            message = result ? "作者源已保存；错误关联仍保留供修复。" : _draftPersistence.LastStatus;
            _statusMessage = message;
            TacticalPresentationRevision++;
            return result;
        }

        internal bool UndoTacticalSource(CharacterTestTacticalEditToken token, bool redo, out string message)
        {
            if (!TacticalContextValid(token)) { message = "撤销上下文已变化。"; return false; }
            bool result = redo ? DraftWorkspace.TryRedo(out message) : DraftWorkspace.TryUndo(out message);
            if (result) _draftPersistence.MarkDirty();
            TacticalPresentationRevision++;
            return result;
        }

        internal bool DuplicateTacticalSource(CharacterTestTacticalEditToken token, out string message)
        {
            if (!TacticalContextValid(token) || !DraftWorkspace.Cards.TryGetValue(token.CardId, out var card))
            { message = "另存上下文已变化。"; return false; }
            if (ShowTestCardDialog != null)
            { ShowTestCardDialog(IsConfiguredTestCardEditing ? "test:" + ConfiguredTestCardId : "draft:" + card.CardDraftId, true); message = "请选择测试库及名称。"; return true; }
            bool result = card.HasCompositionSource
                ? DraftWorkspace.TryDuplicateCompositionCard(token, SelectedSetupTeamId, SelectedSetupSlotIndex, out _, out message)
                : DraftWorkspace.TryDuplicateTacticalSource(token, SelectedSetupTeamId, SelectedSetupSlotIndex, out _, out message);
            if (result) _draftPersistence.MarkDirty();
            TacticalPresentationRevision++;
            return result;
        }

        internal void CreateTacticalSource(bool template)
        {
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly || IsHistoricalFrameSelected) return;
            const string initialDefinitionId = "tactical.shared.single-attack";
            var document = new CharacterTestTacticalDslSource { CardId = "new-card", Name = template ? "攻击战术卡" : "空白战术卡",
                Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence") };
            if (template)
            {
                var definition = DraftWorkspace.CloneTacticalDefinitions().FirstOrDefault(d => d.DefinitionId == initialDefinitionId);
                if (definition == null)
                {
                    var source = CharacterTestTacticalAuthoringTemplates.CreateVisualAttack(); source.ActivationConditionRoot = null;
                    definition = new CharacterTestTacticalSharedDefinition { DefinitionId = initialDefinitionId, Source = source };
                }
                document.Definitions.Add(definition);
                document.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", definition.DefinitionId));
            }
            if (DraftWorkspace.TryImportComposition(SelectedSetupTeamId, SelectedSetupSlotIndex,
                DraftWorkspace.CurrentRevision, document, out string createdCardId, out string message))
            {
                _draftPersistence.MarkDirty();
                if (DraftWorkspace.TryGetGroupForSlot(SelectedSetupTeamId, SelectedSetupSlotIndex, out var group))
                {
                    int index = group.Entries.FindIndex(e => e.CardDraftId == createdCardId);
                    foreach (var host in UnityEngine.Resources.FindObjectsOfTypeAll<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>())
                        if (host.gameObject.scene == gameObject.scene && host.CommandSink != null)
                            host.CommandSink.Enqueue(new MxFramework.UI.MxUiCommand(default(MxFramework.UI.MxUiViewId),
                                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(index), null));
                }
            }
            _statusMessage = message;
            TacticalPresentationRevision++;
        }
    }
}
