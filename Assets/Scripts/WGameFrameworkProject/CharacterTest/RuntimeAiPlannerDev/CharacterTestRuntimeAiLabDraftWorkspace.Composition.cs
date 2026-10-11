using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardDraft
    {
        internal string CompositionSourceJson { get; private set; }
        internal bool HasCompositionSource => CompositionSourceJson != null;
        internal bool HasAuthoredSource => HasTacticalSource || HasCompositionSource;
        internal CharacterTestTacticalCompositionSource CloneCompositionSource()
            => HasCompositionSource ? CharacterTestTacticalCompositionJson.Read(CompositionSourceJson) : null;
        internal void SetCompositionSource(CharacterTestTacticalCompositionSource source)
        {
            CompositionSourceJson = CharacterTestTacticalCompositionJson.Write(source.Clone());
            EnsureReservedTacticalIdentity();
            TacticalSourceJson = null; _logicPayload = null; _payload = null; LogicPayloadMigratedFromV1 = false;
        }
    }

    internal readonly struct CharacterTestTacticalSharedEditToken
    {
        internal readonly string WorkspaceId, DefinitionId;
        internal readonly int WorkspaceRevision, DefinitionRevision;
        internal CharacterTestTacticalSharedEditToken(string workspace, int revision, string definition, int definitionRevision)
        { WorkspaceId = workspace; WorkspaceRevision = revision; DefinitionId = definition; DefinitionRevision = definitionRevision; }
    }

    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        private readonly Dictionary<string, CharacterTestTacticalSharedDefinition> _tacticalDefinitions =
            new Dictionary<string, CharacterTestTacticalSharedDefinition>(StringComparer.Ordinal);
        internal bool HasCompositionData => _tacticalDefinitions.Count > 0 || _cards.Values.Any(c => c.HasCompositionSource);

        private void ImportPublishedTacticalDependencies(CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            var dependencies = card?.TacticalSource?.CloneDefinitions();
            if (dependencies == null) return;
            foreach (var definition in dependencies)
                if (_tacticalDefinitions.TryGetValue(definition.DefinitionId, out var existing)
                    && CharacterTestTacticalCompositionJson.WriteShared(existing) != CharacterTestTacticalCompositionJson.WriteShared(definition))
                    throw new InvalidOperationException("正式版本与当前共享定义冲突，请显式处理：" + definition.DefinitionId);
            foreach (var definition in dependencies)
                if (!_tacticalDefinitions.ContainsKey(definition.DefinitionId)) AddLoadedTacticalDefinition(definition);
        }

        internal CharacterTestTacticalSharedDefinition[] CloneTacticalDefinitions()
            => _tacticalDefinitions.Values.OrderBy(d => d.DefinitionId, StringComparer.Ordinal).Select(CloneDefinition).ToArray();
        private static CharacterTestTacticalSharedDefinition CloneDefinition(CharacterTestTacticalSharedDefinition source)
            => CharacterTestTacticalCompositionJson.ReadShared(CharacterTestTacticalCompositionJson.WriteShared(source));
        internal void AddLoadedTacticalDefinition(CharacterTestTacticalSharedDefinition definition)
        {
            var owned = CloneDefinition(definition);
            if (!_tacticalDefinitions.TryAdd(owned.DefinitionId, owned)) throw new ArgumentException("Duplicate shared tactical definition: " + owned.DefinitionId);
        }
        internal CharacterTestTacticalDslSource ResolveComposition(CharacterTestRuntimeAiLabBehaviorCardDraft card, bool referencedOnly = false)
        {
            if (card?.HasCompositionSource != true) throw new ArgumentException("A composition card is required.");
            var source = card.CloneCompositionSource();
            return new CharacterTestTacticalDslSource { CardId = card.CardDraftId, Name = card.DisplayName,
                ActivationConditionRoot = source.ActivationConditionRoot, Root = source.Root,
                Definitions = referencedOnly ? CompositionNodes(source.Root).Where(n => n.Kind == "Reference").Select(n => n.DefinitionId)
                    .Distinct(StringComparer.Ordinal).Where(id => _tacticalDefinitions.ContainsKey(id)).Select(id => CloneDefinition(_tacticalDefinitions[id])).ToList()
                    : CloneTacticalDefinitions().ToList() };
        }
        private static IEnumerable<CharacterTestTacticalDslNode> CompositionNodes(CharacterTestTacticalDslNode root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (var child in root.Children ?? new List<CharacterTestTacticalDslNode>())
                foreach (var node in CompositionNodes(child)) yield return node;
        }
        internal string[] TacticalDefinitionImpact(string id)
            => _cards.Values.Where(c => c.HasCompositionSource && CompositionNodes(c.CloneCompositionSource().Root).Any(n => n.DefinitionId == id))
                .OrderBy(c => c.CardDraftId, StringComparer.Ordinal).Select(c => c.CardDraftId).ToArray();
        internal CharacterTestTacticalSharedEditToken CaptureTacticalDefinitionEdit(string id)
        {
            var definition = _tacticalDefinitions[id];
            return new CharacterTestTacticalSharedEditToken(WorkspaceId, CurrentRevision, id, definition.Revision);
        }

        /// <summary>Adds a reusable definition to the workspace without inserting it into a card.</summary>
        internal bool TryAddReusableTacticalDefinition(CharacterTestTacticalSharedDefinition definition,
            int expectedRevision, out string message)
        {
            message = string.Empty;
            if (IsReadOnly || expectedRevision != CurrentRevision)
            { message = "共享封装工作副本已变化或只读。"; return false; }
            WorkspaceState before = null;
            try
            {
                var owned = CloneDefinition(definition);
                if (_tacticalDefinitions.TryGetValue(owned.DefinitionId, out var existing))
                {
                    bool same = CharacterTestTacticalCompositionJson.WriteShared(existing) == CharacterTestTacticalCompositionJson.WriteShared(owned);
                    message = same ? "共享封装已存在。" : "共享封装身份已存在且内容不同。";
                    return same;
                }
                before = CaptureState(); AddLoadedTacticalDefinition(owned); TouchWorkspace();
                PushUndo(before, "添加可复用封装"); message = "共享封装已加入可选列表。"; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }

        internal bool TryCreateStandaloneTacticalCard(out string cardId, out string message)
        {
            cardId = string.Empty;
            if (IsReadOnly) { message = "当前工作区只读，不能添加战术卡。"; return false; }
            var before = CaptureState();
            try
            {
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, string.Empty, "空白战术卡", "Tactical", null);
                card.SetCompositionSource(new CharacterTestTacticalCompositionSource
                {
                    Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence")
                });
                _cards.Add(card.CardDraftId, card);
                TouchWorkspace();
                PushUndo(before, "添加基础战术卡");
                cardId = card.CardDraftId;
                message = "已添加空白战术卡草稿。";
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                RestoreState(before);
                message = "添加战术卡失败：" + error.Message;
                return false;
            }
        }

        internal bool TryImportComposition(int teamId, int slotIndex, int expectedRevision, CharacterTestTacticalDslSource document,
            out string cardId, out string message)
        {
            cardId = string.Empty; message = string.Empty;
            if (IsReadOnly || expectedRevision != CurrentRevision || !TryGetGroupForSlot(teamId, slotIndex, out var group)
                || group.Entries.Count >= MaximumBehaviorGroupEntries)
            { message = "工作副本已变化、只读或行为组不可用。"; return false; }
            WorkspaceState before = null;
            try
            {
                var owned = CharacterTestTacticalDslJson.Read(CharacterTestTacticalDslJson.Write(document));
                foreach (var definition in owned.Definitions)
                {
                    if (definition.Source.Schema == CharacterTestTacticalAuthoringSource.EventSetTrialSchema)
                        definition.Source.Schema = CharacterTestTacticalAuthoringSource.EventSetSchema;
                    CloneDefinition(definition);
                    if (_tacticalDefinitions.TryGetValue(definition.DefinitionId, out var existing)
                        && CharacterTestTacticalCompositionJson.WriteShared(existing) != CharacterTestTacticalCompositionJson.WriteShared(definition))
                        throw new ArgumentException("共享定义身份已存在且内容不同，请显式处理冲突：" + definition.DefinitionId);
                }
                if (owned.Definitions.Select(d => d.DefinitionId).Distinct(StringComparer.Ordinal).Count() != owned.Definitions.Count)
                    throw new ArgumentException("共享定义身份重复。");
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, string.Empty, owned.Name, "Tactical", null);
                card.SetCompositionSource(new CharacterTestTacticalCompositionSource { Root = owned.Root, ActivationConditionRoot = owned.ActivationConditionRoot });
                before = CaptureState();
                foreach (var definition in owned.Definitions)
                    if (!_tacticalDefinitions.ContainsKey(definition.DefinitionId)) AddLoadedTacticalDefinition(definition);
                if (CountGroupReferences(group.GroupDraftId) > 1)
                {
                    var slot = TryGetSlot(teamId, slotIndex); group = group.Clone(newIdentity: true);
                    _groups.Add(group.GroupDraftId, group); slot.GroupDraftId = group.GroupDraftId;
                }
                var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    enabled: true, order: (group.Entries.Count + 1) * 10) { EntryId = card.CardDraftId };
                _cards.Add(card.CardDraftId, card);
                group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(null, card.CardDraftId, runtimeEntry.BehaviorId,
                    runtimeEntry.Order, false, runtimeEntry));
                group.NormalizeOrders(); Touch(group); PushUndo(before, "导入共享战术编排");
                cardId = card.CardDraftId; message = "战术编排与共享定义已加入工作副本。"; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException || error is OverflowException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }

        internal bool TryEditComposition(CharacterTestTacticalEditToken token, Action<CharacterTestTacticalCompositionSource> edit,
            string label, out string message)
        {
            message = string.Empty;
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId, out var card) || !card.HasCompositionSource || card.CurrentRevision != token.CardRevision)
            { message = "编辑上下文已变化或只读。"; return false; }
            WorkspaceState before = null;
            try
            {
                var source = card.CloneCompositionSource(); edit(source);
                string json = CharacterTestTacticalCompositionJson.Write(source.Clone());
                if (json == card.CompositionSourceJson) { message = "内容未变化。"; return true; }
                before = CaptureState(); card.SetCompositionSource(source); TouchCompositionCards(new[] { card.CardDraftId });
                PushUndo(before, label); message = "编排已更新。"; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException || error is OverflowException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }

        internal bool TryReplaceTacticalDefinition(CharacterTestTacticalSharedEditToken token, CharacterTestTacticalAuthoringSource source,
            bool confirmedSharedImpact, out string message)
        {
            message = string.Empty;
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_tacticalDefinitions.TryGetValue(token.DefinitionId, out var definition) || definition.Revision != token.DefinitionRevision)
            { message = "共享编辑版本或工作副本已变化。"; return false; }
            var affected = TacticalDefinitionImpact(token.DefinitionId);
            if (affected.Length > 1 && !confirmedSharedImpact) { message = "请先核对并确认共享修改影响。"; return false; }
            WorkspaceState before = null;
            try
            {
                var replacement = CloneDefinition(new CharacterTestTacticalSharedDefinition { DefinitionId = definition.DefinitionId,
                    Revision = checked(definition.Revision + 1), Source = source });
                if (CharacterTestTacticalAuthoringJson.Write(replacement.Source) == CharacterTestTacticalAuthoringJson.Write(definition.Source))
                { message = "共享内容未变化。"; return true; }
                before = CaptureState(); _tacticalDefinitions[token.DefinitionId] = replacement;
                TouchCompositionCards(affected); PushUndo(before, "修改共享战术封装"); message = "共享封装已更新。"; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException || error is OverflowException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }

        /// <summary>Explicit shared-source upgrade, preserving identities, instance parameters and Undo history.</summary>
        internal int AddVisibleApproachCompletionEvents()
        {
            int added = 0;
            foreach (var definition in CloneTacticalDefinitions())
            {
                int count = CharacterTestTacticalApproachActionSplit.AddVisibleCompletionEvents(definition.Source);
                if (count == 0) continue;
                var token = new CharacterTestTacticalSharedEditToken(WorkspaceId, CurrentRevision, definition.DefinitionId, definition.Revision);
                if (!TryReplaceTacticalDefinition(token, definition.Source, true, out string issue)) throw new InvalidOperationException(issue);
                added += count;
            }
            foreach (var card in _cards.Values.Where(c => c.HasTacticalSource).ToArray())
            {
                var source = card.CloneTacticalSource();
                int count = CharacterTestTacticalApproachActionSplit.AddVisibleCompletionEvents(source);
                if (count == 0) continue;
                var token = new CharacterTestTacticalEditToken(this, card);
                if (!TryEditTacticalSource(token, "添加可见接近完成事件", target => {
                    if (CharacterTestTacticalApproachActionSplit.AddVisibleCompletionEvents(target) != count)
                        throw new InvalidOperationException("接近完成升级上下文已变化。");
                }, out string issue)) throw new InvalidOperationException(issue);
                added += count;
            }
            return added;
        }

        private void TouchCompositionCards(IEnumerable<string> cardIds)
        {
            var affected = new HashSet<string>(cardIds, StringComparer.Ordinal);
            foreach (string id in affected) { _cards[id].CurrentRevision++; _cards[id].LastCompileStatus = "CompositionChanged"; }
            foreach (var group in _groups.Values)
                if (group.Entries.Any(e => e != null && affected.Contains(e.CardDraftId))) group.CurrentRevision++;
            CurrentRevision++; UpdatedUtc = DateTime.UtcNow;
        }

        internal bool TryRenameCompositionCard(CharacterTestTacticalEditToken token, string name, out string message)
        {
            name = name?.Trim();
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId, out var card) || !card.HasCompositionSource || card.CurrentRevision != token.CardRevision)
            { message = "卡名称编辑上下文已变化。"; return false; }
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) { message = "请输入 1～128 字的卡名称。"; return false; }
            if (card.DisplayName == name) { message = "名称未变化。"; return true; }
            var before = CaptureState(); card.DisplayName = name; TouchCompositionCards(new[] { card.CardDraftId });
            PushUndo(before, "修改战术卡名称"); message = "卡名称已更新。"; return true;
        }

        internal bool TryDuplicateCompositionCard(CharacterTestTacticalEditToken token, int team, int slot,
            out string cardId, out string message)
        {
            cardId = string.Empty;
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId, out var card) || !card.HasCompositionSource || card.CurrentRevision != token.CardRevision)
            { message = "另存卡上下文已变化。"; return false; }
            var document = ResolveComposition(card); document.Name = card.DisplayName + " 副本";
            if (!TryImportComposition(team, slot, token.WorkspaceRevision, document, out cardId, out message)) return false;
            var copy = _cards[cardId]; copy.IconKey = card.IconKey; copy.Cost = card.Cost; copy.CooldownSeconds = card.CooldownSeconds;
            copy.ShortDescription = card.ShortDescription; copy.UseSummary = card.UseSummary;
            copy.RiskSummary = card.RiskSummary; copy.TagsText = card.TagsText;
            copy.RequiredCapabilities = (CharacterTestRuntimeAiPlannerBehaviorCapability[])card.RequiredCapabilities.Clone();
            copy.ResponsibilityKinds = (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[])card.ResponsibilityKinds.Clone();
            return true;
        }

        internal bool TryDuplicateTacticalDefinition(CharacterTestTacticalEditToken token, string referenceNodeId,
            string newDefinitionId, out string message)
        {
            message = string.Empty;
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId, out var card) || !card.HasCompositionSource || card.CurrentRevision != token.CardRevision)
            { message = "另存封装上下文已变化。"; return false; }
            WorkspaceState before = null;
            try
            {
                if (_tacticalDefinitions.ContainsKey(newDefinitionId)) throw new ArgumentException("封装身份已存在。");
                var source = card.CloneCompositionSource();
                var reference = CompositionNodes(source.Root).Single(n => n.NodeId == referenceNodeId && n.Kind == "Reference");
                var copy = CloneDefinition(_tacticalDefinitions[reference.DefinitionId]);
                copy.DefinitionId = newDefinitionId; copy.Revision = 1; copy = CloneDefinition(copy);
                reference.DefinitionId = newDefinitionId; source = source.Clone();
                before = CaptureState(); AddLoadedTacticalDefinition(copy); card.SetCompositionSource(source);
                TouchCompositionCards(new[] { card.CardDraftId }); PushUndo(before, "另存封装并替换指定引用");
                message = "新封装已创建，仅替换指定引用。"; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException || error is OverflowException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }

        internal bool TryCreateTacticalDefinitionReference(CharacterTestTacticalEditToken token, out string message,
            CharacterTestTacticalAuthoringSource initialContent = null, string insertionTargetNodeId = null)
        {
            message = string.Empty;
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId, out var card) || !card.HasCompositionSource || card.CurrentRevision != token.CardRevision)
            { message = "新建封装上下文已变化。"; return false; }
            WorkspaceState before = null;
            try
            {
                var source = card.CloneCompositionSource();
                var selected = CharacterTestTacticalCompositionEdits.Find(source, insertionTargetNodeId ?? token.NodeId);
                var parent = selected.Children != null ? selected : CharacterTestTacticalCompositionEdits.Parent(source, selected.NodeId);
                int index = ReferenceEquals(parent, selected) ? parent.Children.Count : parent.Children.IndexOf(selected) + 1;
                var content = initialContent?.Clone() ?? CharacterTestTacticalAuthoringTemplates.CreateBlank("新共享封装");
                if (!content.HasBlocksVersion)
                { content.Schema = CharacterTestTacticalAuthoringSource.EventSetSchema; content.Version = 1; }
                var definition = new CharacterTestTacticalSharedDefinition { DefinitionId = "shared-" + Guid.NewGuid().ToString("N"), Source = content };
                CharacterTestTacticalCompositionEdits.Insert(source, parent.NodeId, index, CharacterTestTacticalCompositionEdits.NewNode("Reference", definition.DefinitionId));
                source = source.Clone(); definition = CloneDefinition(definition);
                before = CaptureState(); AddLoadedTacticalDefinition(definition); card.SetCompositionSource(source);
                TouchCompositionCards(new[] { card.CardDraftId }); PushUndo(before, "新建封装并引用");
                message = initialContent == null ? "已新建空白共享封装并加入当前卡，可展开添加事件。"
                    : "已新建“" + content.Name + "”共享封装并加入当前卡。"; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }
    }
}
