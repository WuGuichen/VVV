using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Selection and edit leases only; all persistent writes are workspace transactions.</summary>
    internal sealed partial class CharacterTestTacticalCompositionEditingSession
    {
        private readonly CharacterTestRuntimeAiPlannerDevSceneController _controller;
        internal CharacterTestRuntimeAiPlannerDevSceneController Controller => _controller;
        private CharacterTestRuntimeAiLabDraftWorkspace _workspace;
        private CharacterTestRuntimeAiLabBehaviorCardDraft _card;
        private long _serial;
        private int _confirmedRevision = -1;
        private CharacterTestTacticalEditingSession _activationEditor;
        internal CharacterTestTacticalAuthoringPreview BuildActivationOverview()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateBlank(Document.Name);
            source.ActivationConditionRoot = Document.ActivationConditionRoot;
            source = source.Clone();
            var preview = CharacterTestTacticalAuthoringPreview.FromConditionProjection(source, true, string.Empty,
                Frozen != null, Frozen?.CharacterId ?? 0);
            if (_activationEditor == null || _activationEditor.CardId != CardId)
                _activationEditor = new CharacterTestTacticalEditingSession(_controller, CardId);
            var token = new CharacterTestTacticalEditToken(_workspace, _card, CharacterTestTacticalEditTarget.CardActivation,
                Document.Root.NodeId, contextSerial: _serial);
            _activationEditor.Bind(_workspace, _card, preview, token);
            preview.Editor = _activationEditor;
            return preview;
        }

        internal bool AcceptsActivation(CharacterTestTacticalEditToken token)
            => CanEdit && token.Target == CharacterTestTacticalEditTarget.CardActivation
                && token.NodeId == Document.Root.NodeId && token.ContextSerial == _serial
                && token.WorkspaceId == _workspace.WorkspaceId && token.WorkspaceRevision == _workspace.CurrentRevision
                && token.CardId == CardId && token.CardRevision == _card.CurrentRevision;

        internal bool EditActivation(CharacterTestTacticalEditToken token, Action<CharacterTestTacticalAuthoringSource> edit, out string message)
        {
            if (!AcceptsActivation(token)) { message = "整卡条件编辑上下文已变化。"; return false; }
            var source = BuildActivationOverview().Source.Clone();
            try { edit?.Invoke(source); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
            { message = error.Message; return false; }
            bool changed = _workspace.TryEditComposition(token, card => card.ActivationConditionRoot = source.ActivationConditionRoot,
                "修改整卡启用条件", out message);
            Complete(changed, message);
            return changed;
        }
        internal CharacterTestTacticalCompositionEditingSession(CharacterTestRuntimeAiPlannerDevSceneController controller) { _controller = controller; }
        internal string CardId => _card?.CardDraftId;
        internal CharacterTestTacticalDslSource Document { get; private set; }
        internal CharacterTestTacticalFrozenSource Frozen { get; private set; }
        internal int FrozenSourceFrame => _controller.TacticalSourceInspectionFrame;
        internal CharacterTestTacticalEditToken Token { get; private set; }
        internal string SelectedNodeId { get; private set; } = string.Empty;
        internal string SelectedCompiledEventId { get; set; } = string.Empty;
        internal bool NativeExecutionView => _controller.NativeTacticalExecutionView;
        internal void SetNativeExecutionView(bool enabled)
        {
            if (NativeExecutionView == enabled) return;
            _controller.NativeTacticalExecutionView = enabled;
            _controller.RefreshTacticalPreview();
        }
        internal CharacterTestTacticalEditTarget Target { get; private set; } = CharacterTestTacticalEditTarget.Composition;
        internal HashSet<string> Selection { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal bool CanEdit => Frozen == null && !_controller.IsHistoricalFrameSelected && _workspace?.IsReadOnly == false;
        internal bool SharedConfirmed => CanEdit && Target == CharacterTestTacticalEditTarget.SharedDefinition && _confirmedRevision == _workspace.CurrentRevision;
        internal bool EditingSourceTimeline => (SharedConfirmed || EventFocusActive) && !NativeExecutionView;
        internal bool DetailsOpen { get; set; }
        internal string Message { get; private set; } = string.Empty;
        internal CharacterTestTacticalDslDescription Description { get; set; }
        internal CharacterTestTacticalSummaryRequest SummaryRequest { get; set; }
        internal string DescriptionText => Frozen?.Description ?? Description?.Text ?? "当前源尚未生成说明。";
        internal string DescriptionDetails => Frozen?.DescriptionDetails ?? Description?.Details ?? string.Empty;
        internal string DescriptionWarnings => string.Join("\n", Frozen != null ? Frozen.DescriptionWarnings : Description?.Warnings ?? Array.Empty<string>());
        internal void Save() { _controller.SaveTacticalSource(Token, out string message); Feedback(message); }
        internal void Undo(bool redo)
        {
            bool wasConfirmed = SharedConfirmed;
            _controller.UndoTacticalSource(Token, redo, out string message);
            if (wasConfirmed && _workspace != null)
                _confirmedRevision = _workspace.CurrentRevision;
            else
                _confirmedRevision = -1;
            Feedback(message);
        }
        internal void CopyCard() { _controller.DuplicateTacticalSource(Token, out string message); Feedback(message); }
        internal void RenameCard(CharacterTestTacticalEditToken token, string name)
        {
            if (!Accepts(token)) { Feedback("卡名称编辑上下文已变化，本次输入未写入。"); return; }
            bool changed = _workspace.TryRenameCompositionCard(token, name, out string message); Complete(changed, message);
        }
        internal CharacterTestTacticalDslNode SelectedNode => CharacterTestTacticalCompositionEdits.Nodes(Document?.Root).FirstOrDefault(n => n.NodeId == SelectedNodeId);
        internal CharacterTestTacticalSharedDefinition SelectedDefinition => Document?.Definitions.FirstOrDefault(d => d.DefinitionId == SelectedNode?.DefinitionId);
        internal string ImpactText => SelectedDefinition == null ? string.Empty : "共享封装：" + SelectedDefinition.Source.Name
            + " · 版本 " + SelectedDefinition.Revision + "\n引用影响：" + string.Join("、", AffectedReferences());

        private int _documentWorkspaceRevision = -1;
        internal void Bind(CharacterTestRuntimeAiLabDraftWorkspace workspace, CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestTacticalFrozenSource frozen = null)
        {
            bool changed = _workspace?.WorkspaceId != workspace.WorkspaceId || CardId != card.CardDraftId || Frozen != frozen;
            _workspace = workspace; _card = card; Frozen = frozen;
            if (changed || Document == null || frozen == null && _documentWorkspaceRevision != workspace.CurrentRevision)
            { Document = frozen != null ? frozen.CloneDocument() : workspace.ResolveComposition(card); _documentWorkspaceRevision = workspace.CurrentRevision; }
            if (changed)
            {
                ClearEventFocus();
                var first = CharacterTestTacticalCompositionEdits.Nodes(Document.Root).FirstOrDefault(n => n.Kind == "Reference");
                SelectedNodeId = first?.NodeId ?? Document.Root.NodeId;
                Target = first != null ? CharacterTestTacticalEditTarget.SharedDefinition : CharacterTestTacticalEditTarget.Composition;
                ResetSelection();
            }
            if (SelectedNode == null) { SelectedNodeId = Document.Root.NodeId; Target = CharacterTestTacticalEditTarget.Composition; ResetSelection(); }
            RefreshToken();
        }

        private void ResetSelection() { _serial++; _confirmedRevision = -1; SelectedCompiledEventId = string.Empty; Selection.Clear(); Selection.Add(SelectedNodeId); }
        private void RefreshToken()
        {
            var definition = Target == CharacterTestTacticalEditTarget.SharedDefinition ? SelectedDefinition : null;
            Token = new CharacterTestTacticalEditToken(_workspace, _card, Target, SelectedNodeId,
                definition?.DefinitionId ?? string.Empty, definition?.Revision ?? 0, _serial);
        }
        internal void Select(string nodeId, CharacterTestTacticalEditTarget target = CharacterTestTacticalEditTarget.Composition, bool additive = false)
        {
            SelectedCompiledEventId = string.Empty;
            if (!CharacterTestTacticalCompositionEdits.Nodes(Document.Root).Any(n => n.NodeId == nodeId)) return;
            var selected = CharacterTestTacticalCompositionEdits.Nodes(Document.Root).First(n => n.NodeId == nodeId);
            if (target == CharacterTestTacticalEditTarget.NodeCondition && selected.Kind != "If" && selected.Kind != "Wait")
            { Feedback("请选择入口条件组或等待组；单项入口可先将该项放入条件组。"); return; }
            if (target == CharacterTestTacticalEditTarget.NodeEnd && selected.Kind != "Sequence" && selected.Kind != "Reference")
            { Feedback("局部结束请设置在顺序组或封装引用上。"); return; }
            if (SelectedNodeId != nodeId || Target != target) { ClearEventFocus(); _serial++; _confirmedRevision = -1; }
            SelectedNodeId = nodeId; Target = target;
            if (!additive) Selection.Clear();
            if (!Selection.Add(nodeId) && additive) Selection.Remove(nodeId);
            RefreshToken(); _controller.RefreshTacticalPreview();
        }
        internal bool Accepts(CharacterTestTacticalEditToken token)
            => CanEdit && AcceptsView(token);
        internal bool AcceptsView(CharacterTestTacticalEditToken token)
            => _workspace != null && token.WorkspaceId == _workspace.WorkspaceId && token.WorkspaceRevision == _workspace.CurrentRevision
                && token.CardId == CardId && token.CardRevision == _card.CurrentRevision && token.ContextSerial == _serial
                && token.Target == Target && token.NodeId == SelectedNodeId && token.DefinitionId == Token.DefinitionId
                && token.DefinitionRevision == Token.DefinitionRevision;
        internal bool ConfirmShared(CharacterTestTacticalEditToken token)
        {
            if (!Accepts(token) || Target != CharacterTestTacticalEditTarget.SharedDefinition || SelectedDefinition == null) return false;
            _confirmedRevision = _workspace.CurrentRevision; _controller.RefreshTacticalPreview(); return true;
        }
        internal void FinishSharedEditing()
        {
            ClearEventFocus();
            _confirmedRevision = -1;
            _serial++;
            RefreshToken();
            Feedback("已返回整卡预览；修改保留在工作副本，可继续保存或测试。");
        }
        internal string[] AffectedReferences()
        {
            if (SelectedDefinition == null) return Array.Empty<string>();
            if (Frozen != null) return new[] { "历史冻结版本，只读" };
            string id = SelectedDefinition.DefinitionId;
            var result = new List<string>();
            void Visit(CharacterTestTacticalDslNode node, string path)
            {
                if (node.Kind == "Reference" && node.DefinitionId == id) result.Add(path);
                if (node.Children == null) return;
                for (int i = 0; i < node.Children.Count; i++) Visit(node.Children[i], path + " / 第 " + (i + 1) + (node.Children[i].Children != null ? " 组" : " 项"));
            }
            foreach (var card in _workspace.Cards.Values.Where(c => c.HasCompositionSource)) Visit(card.CloneCompositionSource().Root, card.DisplayName);
            return result.ToArray();
        }
        internal bool Edit(CharacterTestTacticalEditToken token, string label, Action<CharacterTestTacticalCompositionSource> edit)
        {
            if (!Accepts(token)) { Feedback("编辑对象、字段或版本已变化，本次修改未写入。"); return false; }
            bool result = _workspace.TryEditComposition(token, edit, label, out string message);
            Complete(result, message); return result;
        }
        internal bool EditInternal(CharacterTestTacticalEditToken token, Action<CharacterTestTacticalAuthoringSource> edit, out string message)
        {
            if (!Accepts(token)) { message = "引用、共享版本或目标字段已变化，本次修改未写入。"; return false; }
            bool result;
            if (Target == CharacterTestTacticalEditTarget.SharedDefinition)
            {
                if (!SharedConfirmed) { message = "请先确认共享修改影响。"; return false; }
                var source = SelectedDefinition.Source.Clone();
                try { edit?.Invoke(source); }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
                { message = error.Message; return false; }
                result = _workspace.TryReplaceTacticalDefinition(new CharacterTestTacticalSharedEditToken(token.WorkspaceId,
                    token.WorkspaceRevision, token.DefinitionId, token.DefinitionRevision), source, true, out message);
                if (result) _confirmedRevision = _workspace.CurrentRevision;
            }
            else if (Target == CharacterTestTacticalEditTarget.CardActivation || Target == CharacterTestTacticalEditTarget.NodeCondition || Target == CharacterTestTacticalEditTarget.NodeEnd)
            {
                var projection = ConditionProjection();
                try { edit?.Invoke(projection); }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
                { message = error.Message; return false; }
                result = _workspace.TryEditComposition(token, source => CharacterTestTacticalCompositionEdits.SetCondition(source,
                    token.Target, token.NodeId, projection.ActivationConditionRoot), "修改编排条件", out message);
            }
            else { message = "请选择共享内部或卡层条件。"; return false; }
            Complete(result, message); return result;
        }
        internal CharacterTestTacticalAuthoringSource ConditionProjection()
        {
            var projection = CharacterTestTacticalAuthoringTemplates.CreateBlank("编排条件");
            var source = new CharacterTestTacticalCompositionSource { Root = Document.Root, ActivationConditionRoot = Document.ActivationConditionRoot }.Clone();
            projection.ActivationConditionRoot = CharacterTestTacticalCompositionEdits.GetCondition(source, Target, SelectedNodeId);
            return projection;
        }
        internal bool DuplicateDefinition(CharacterTestTacticalEditToken token)
        {
            if (!Accepts(token) || SelectedNode?.Kind != "Reference") { Feedback("另存封装上下文已变化。"); return false; }
            bool result = _workspace.TryDuplicateTacticalDefinition(token, SelectedNodeId, "shared-" + Guid.NewGuid().ToString("N"), out string message);
            _confirmedRevision = -1; Complete(result, message); return result;
        }
        internal bool CreateDefinition(CharacterTestTacticalEditToken token, string insertionTargetNodeId = null, CharacterTestTacticalAuthoringSource initialContent = null)
        {
            if (!Accepts(token)) { Feedback("新建封装上下文已变化。"); return false; }
            var before = new HashSet<string>(CharacterTestTacticalCompositionEdits.Nodes(Document.Root).Select(n => n.NodeId));
            bool result = _workspace.TryCreateTacticalDefinitionReference(token, out string message, initialContent: initialContent, insertionTargetNodeId: insertionTargetNodeId);
            Complete(result, message);
            if (result)
            {
                var inserted = CharacterTestTacticalCompositionEdits.Nodes(Document.Root).FirstOrDefault(n => !before.Contains(n.NodeId) && n.Kind == "Reference");
                if (inserted != null) Select(inserted.NodeId, CharacterTestTacticalEditTarget.SharedDefinition);
            }
            return result;
        }
        internal void Feedback(string message) { Message = message; _controller.RefreshTacticalPreview(); }
        private void Complete(bool changed, string message)
        {
            Message = message;
            if (changed) { Bind(_workspace, _workspace.Cards[CardId]); _controller.MarkTacticalCompositionDirty(); }
            _controller.RefreshTacticalPreview();
        }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestTacticalCompositionEditingSession _compositionEditingSession;
        internal CharacterTestTacticalCompositionEditingSession CompositionEditingSession => _compositionEditingSession;
        internal void MarkTacticalCompositionDirty() => _draftPersistence?.MarkDirty();
    }
}
