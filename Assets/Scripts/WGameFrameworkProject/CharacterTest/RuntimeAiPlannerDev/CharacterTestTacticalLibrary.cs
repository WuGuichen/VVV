using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalLibraryEntry
    {
        internal string Key, Name, Description, Revision;
        internal bool Draft;
    }

    internal sealed class CharacterTestTacticalCardEditContext
    {
        internal string WorkspaceId, CardId;
        internal string ConfiguredTestCardId;
        internal int TeamId, SlotIndex;
        // A compatibility presentation adapter, not a registered or equipped behavior group.
        internal CharacterTestRuntimeAiLabBehaviorGroupDraft Presentation;
    }

    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        internal bool TryAdoptStandaloneTacticalPublishedRevision(
            string cardDraftId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            out string message)
        {
            if (IsReadOnly
                || publishedCard == null
                || !_cards.TryGetValue(cardDraftId ?? string.Empty, out var card)
                || !card.HasAuthoredSource
                || !string.Equals(card.PublishedStableId, publishedCard.StableCardId, StringComparison.Ordinal))
            {
                message = "正式行为卡与当前战术卡草稿不匹配。";
                return false;
            }

            WorkspaceState before = CaptureState();
            try
            {
                ImportPublishedTacticalDependencies(publishedCard);
                card.CopyDefinitionFrom(
                    CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(publishedCard));
                card.LastCompileStatus = "Published";
                card.LastCompiledRevision = card.CurrentRevision;
                TouchWorkspace();
                _undo.Clear();
                _redo.Clear();
                message = "战术卡草稿已采用正式 r" + publishedCard.Revision + "。";
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                RestoreState(before);
                message = "正式版本已生成，但草稿采用失败：" + error.Message;
                return false;
            }
        }

        internal bool TryDeleteUnreferencedTacticalDraft(string cardId, out string issue)
        {
            issue = string.Empty;
            if (IsReadOnly) { issue = "当前工作区只读，不能删除。"; return false; }
            if (!_cards.TryGetValue(cardId ?? string.Empty, out var card) || !card.HasAuthoredSource)
            { issue = "战术卡草稿已不存在。"; return false; }
            var references = _groups.Values.Where(g => g.Entries.Any(e => e.CardDraftId == cardId))
                .Select(g => g.DisplayName + "（" + g.GroupDraftId + "）").ToArray();
            if (references.Length > 0)
            { issue = "请先从以下行为组移除该卡：\n" + string.Join("\n", references); return false; }
            var before = CaptureState();
            _cards.Remove(cardId);
            TouchWorkspace();
            PushUndo(before, "删除战术卡草稿");
            issue = "已删除草稿“" + card.DisplayName + "”。";
            return true;
        }

        internal bool TryOpenStandaloneTacticalCard(CharacterTestRuntimeAiPlannerPublishedBehaviorCard published,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card, out string issue)
        {
            card = null; issue = string.Empty;
            if (published?.TacticalSource == null) { issue = "正式版本缺少战术作者源。"; return false; }
            var existing = _cards.Values.Where(c => c.HasAuthoredSource && c.PublishedStableId == published.StableCardId).ToArray();
            if (existing.Length == 1) { card = existing[0]; return true; }
            if (existing.Length > 1) { issue = "该正式卡有多个工作区草稿，请选择具体草稿。"; return false; }
            if (IsReadOnly) { issue = "只读工作区不能创建编辑草稿。"; return false; }
            var before = CaptureState();
            try
            {
                card = CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published);
                ImportPublishedTacticalDependencies(published);
                _cards.Add(card.CardDraftId, card);
                TouchWorkspace(); PushUndo(before, "打开正式战术卡草稿");
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { RestoreState(before); card = null; issue = error.Message; return false; }
        }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal CharacterTestTacticalCardEditContext TacticalCardContext { get; private set; }
        internal bool HasStandaloneTacticalCard => TacticalCardContext != null && DraftWorkspace?.WorkspaceId == TacticalCardContext.WorkspaceId
            && DraftWorkspace.Cards.ContainsKey(TacticalCardContext.CardId);
        internal string TacticalLibraryQuery { get; set; } = string.Empty;
        internal int TacticalLibraryPage { get; set; }
        internal string TacticalLibrarySelectedKey { get; set; } = string.Empty;
        internal string TacticalLibraryIssue { get; set; } = string.Empty;
        internal bool TryCreateTacticalLibraryCard(out string key, out string message)
        {
            key = string.Empty;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            { message = "请先结束当前测试，再添加基础战术卡。"; return false; }
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly || IsConfiguredTestCardEditing)
            { message = "当前工作区不可编辑基础战术卡。"; return false; }
            if (!DraftWorkspace.TryCreateStandaloneTacticalCard(out string cardId, out message)) return false;
            _draftPersistence.MarkDirty();
            _libraryMetadataKey = null;
            key = "draft:" + cardId;
            TacticalLibrarySelectedKey = key;
            TacticalLibraryIssue = string.Empty;
            _statusMessage = message;
            return true;
        }
        internal CharacterTestRuntimeAiLabPageContext TacticalLibraryOrigin { get; set; }
        internal CharacterTestTacticalCardEditContext TacticalLibraryOriginCard { get; set; }
        private string _libraryMetadataKey;
        private CharacterTestTacticalLibraryEntry[] _libraryMetadata = Array.Empty<CharacterTestTacticalLibraryEntry>();
        private CharacterTestRuntimeAiLabCandidateRevision _runtimeCardPresentationOwner;
        private CharacterTestRuntimeAiLabBehaviorGroupDraft _runtimeCardPresentation;

        internal bool CanPublishTacticalLibraryCard(string key, out string message)
        {
            message = string.Empty;
            if (IsConfiguredTestCardEditing || key?.StartsWith("test:", StringComparison.Ordinal) == true)
            { message = "测试单卡只保存到测试库，基础卡发布请从基础战术卡库进入。"; return false; }
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            { message = "请先结束当前测试，再发布战术卡。"; return false; }
            if (!CanPublishFromActiveAuthoringWorkspace(out message)) return false;
            if (PublishCurrentChangesRecoveryRequired)
            { message = "上一次发布尚待恢复，请先核对正式目录。"; return false; }
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly)
            { message = "当前主工作区不可写。"; return false; }
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("draft:", StringComparison.Ordinal)
                || !DraftWorkspace.Cards.TryGetValue(key.Substring(6), out var card)
                || !card.HasAuthoredSource)
            { message = "请选择一张可编辑的战术卡草稿。"; return false; }
            if (_projectCatalogAuthoring == null && ProjectCatalogState?.Success == true)
                _projectCatalogAuthoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(ProjectCatalogState);
            if (_projectCatalogAuthoring?.CanWrite != true)
            { message = ProjectCatalogState?.Diagnostic ?? "项目行为卡目录当前不可写。"; return false; }
            EnsureBehaviorCardReferences();
            _projectCatalogAuthoring.ResolveTacticalSource = CaptureTacticalSourceForComparison;
            string diagnostic = string.Empty;
            if (card.HasPublishedSource
                && _projectCatalogAuthoring.TryHasUnpublishedCardChanges(
                    card, _behaviorCardReferences, out bool changed, out diagnostic)
                && !changed)
            { message = "当前战术卡已与正式版本同步。"; return false; }
            if (!string.IsNullOrWhiteSpace(diagnostic))
            { message = diagnostic; return false; }
            return true;
        }

        internal bool TryPublishTacticalLibraryCard(string key, out string message)
        {
            if (!CanPublishTacticalLibraryCard(key, out message)) return false;
            string cardDraftId = key.Substring(6);
            CharacterTestRuntimeAiLabBehaviorCardDraft card = DraftWorkspace.Cards[cardDraftId];
            if (_draftPersistence == null || !_draftPersistence.Flush())
            {
                message = "保存当前战术卡草稿失败；正式目录没有改变。"
                    + (_draftPersistence?.LastStatus ?? string.Empty);
                return false;
            }

            _projectCatalogAuthoring.CompileTacticalForPublish = draft => CompileAuthoredCard(draft, true);
            CharacterTestRuntimeAiLabPublishResult result =
                _projectCatalogAuthoring.TryPublishCard(card, _behaviorCardReferences);
            if (!result.Success || result.PublishedCard == null)
            {
                message = result.Diagnostics.Count > 0
                    ? result.Diagnostics[0].Message
                    : "战术行为卡发布失败。";
                TacticalLibraryIssue = message;
                return false;
            }

            _matchConfig.UpdateProjectCatalogState(_projectCatalogAuthoring.State);
            if (!DraftWorkspace.TryAdoptStandaloneTacticalPublishedRevision(
                    cardDraftId, result.PublishedCard, out string adoptionMessage))
            {
                message = adoptionMessage;
                TacticalLibraryIssue = message;
                return false;
            }
            _draftPersistence.MarkDirty();
            bool saved = _draftPersistence.Flush();
            TacticalLibraryIssue = string.Empty;
            _libraryMetadataKey = null;
            message = "已发布战术行为卡“" + result.PublishedCard.Metadata.DisplayName + "” r"
                + result.PublishedCard.Revision + "；行为组版本未改变。";
            if (!saved) message += " 草稿检查点尚未落盘：" + _draftPersistence.LastStatus;
            _statusMessage = message;
            return true;
        }

        private bool TryAddTacticalDraftToCurrentBehaviorGroup(string cardId,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out int entryIndex, out string message)
        {
            entryIndex = -1;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup || DraftWorkspace == null || DraftWorkspace.IsReadOnly)
            { message = "请在可编辑的实验设置中装配卡片。"; return false; }
            if (!DraftWorkspace.Cards.TryGetValue(cardId, out var card) || !card.HasAuthoredSource)
            { message = "战术卡草稿已不存在。"; return false; }
            if (!DraftWorkspace.TryGetGroupForSlot(_selectedSetupTeamId, _selectedSetupSlotIndex, out var group))
            { message = "请先选择要编辑的行为组。"; return false; }
            if (group.Entries.Any(e => e.CardDraftId == cardId))
            { message = "该战术卡已在当前行为组中。"; return false; }
            if (!DraftWorkspace.TryAddExistingCardEntry(_selectedSetupTeamId, _selectedSetupSlotIndex, cardId,
                responsibilities ?? card.ResponsibilityKinds, out entryIndex, out message)) return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        internal bool CanDeleteTacticalLibraryCard(string key, out string message)
        {
            message = string.Empty;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            { message = "请先结束当前测试，再删除卡片。"; return false; }
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly)
            { message = "当前工作区不可写。"; return false; }
            var entry = GetTacticalLibraryEntries().FirstOrDefault(x => x.Key == key);
            if (entry == null) { message = "卡片已不存在，请刷新卡库。"; return false; }
            if (entry.Draft)
            {
                string id = key.Substring(6);
                if (TacticalLibraryOriginCard?.CardId == id)
                { message = "该卡仍在制作页面中打开，请先关闭制作页再删除。"; return false; }
                var refs = DraftWorkspace.Groups.Values.Where(g => g.Entries.Any(e => e.CardDraftId == id))
                    .Select(g => g.DisplayName + "（" + g.GroupDraftId + "）").ToArray();
                if (refs.Length > 0)
                { message = "请先从以下行为组移除该卡：\n" + string.Join("\n", refs); return false; }
                message = "删除草稿“" + entry.Name + "”？\n仅删除当前工作区草稿，已有正式版本及共享封装会保留。";
                return true;
            }
            if (_projectCatalogAuthoring?.CanWrite != true)
            { message = "正式卡目录当前不可写。"; return false; }
            string stableId = key.Substring(10);
            var drafts = DraftWorkspace.Cards.Values.Where(c => c.PublishedStableId == stableId).Select(c => c.DisplayName).ToArray();
            if (drafts.Length > 0)
            { message = "请先处理当前工作区中的对应草稿：\n" + string.Join("\n", drafts); return false; }
            var groups = ProjectCatalogState.GroupCatalog.Groups.Where(g => g.Cards.Any(c => c.StableCardId == stableId))
                .Select(g => g.DisplayName + " r" + g.Revision).ToArray();
            if (groups.Length > 0)
            { message = "正式行为组仍引用该卡，不能删除：\n" + string.Join("\n", groups); return false; }
            message = "删除正式卡“" + entry.Name + "”？\n将删除该正式卡的所有版本。";
            return true;
        }

        internal bool TryDeleteTacticalLibraryCard(string key, string expectedWorkspaceId, int expectedRevision, out string message)
        {
            if (DraftWorkspace == null || DraftWorkspace.WorkspaceId != expectedWorkspaceId || DraftWorkspace.CurrentRevision != expectedRevision)
            { message = "工作区已变化，请重新确认删除。"; return false; }
            if (!CanDeleteTacticalLibraryCard(key, out message)) return false;
            if (key.StartsWith("draft:", StringComparison.Ordinal))
            {
                string id = key.Substring(6);
                if (!DraftWorkspace.TryDeleteUnreferencedTacticalDraft(id, out message)) return false;
                if (TacticalCardContext?.CardId == id) TacticalCardContext = null;
                _draftPersistence.MarkDirty();
                if (!_draftPersistence.Flush()) message += " 删除尚未保存：" + _draftPersistence.LastStatus;
            }
            else
            {
                EnsureBehaviorCardReferences();
                var result = _projectCatalogAuthoring.TryDeleteCard(key.Substring(10), _behaviorCardReferences);
                if (!result.Success) { message = result.Diagnostic; return false; }
                _matchConfig.UpdateProjectCatalogState(result.State);
                message = "正式卡已删除。";
            }
            TacticalLibrarySelectedKey = string.Empty;
            TacticalLibraryIssue = string.Empty;
            _libraryMetadataKey = null;
            return true;
        }

        internal CharacterTestTacticalLibraryEntry[] GetTacticalLibraryEntries()
        {
            if (TestCardLibraryMode && !TestCardTemplatePicking) return ConfiguredCardRows();
            return GetBaseTacticalLibraryEntries();
        }

        internal CharacterTestTacticalLibraryEntry[] GetBaseTacticalLibraryEntries()
        {
            var workspace = BaseCardWorkspace;
            if (workspace == null) return Array.Empty<CharacterTestTacticalLibraryEntry>();
            string key = workspace.WorkspaceId + ":" + workspace.CurrentRevision + ":" + ProjectCatalogState?.CardFileHash;
            if (_libraryMetadataKey == key) return _libraryMetadata;
            _libraryMetadata = BuildTacticalLibraryMetadata(workspace, ProjectCatalogState?.CardCatalog);
            _libraryMetadataKey = key;
            return _libraryMetadata;
        }

        internal static CharacterTestTacticalLibraryEntry[] BuildTacticalLibraryMetadata(CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            var rows = new List<CharacterTestTacticalLibraryEntry>();
            var represented = new HashSet<string>(StringComparer.Ordinal);
            var definitions = workspace.CloneTacticalDefinitions().ToDictionary(d => d.DefinitionId, StringComparer.Ordinal);
            string Brief(CharacterTestTacticalDslNode node)
            {
                if (node == null) return string.Empty;
                if (node.Kind == "Reference")
                {
                    if (!definitions.TryGetValue(node.DefinitionId ?? string.Empty, out var definition)) return "未解析封装";
                    var titles = definition.Source.Timelines.SelectMany(t => t.Events)
                        .Select(TacticalActionEventTitle).Where(title => !string.IsNullOrWhiteSpace(title)).Distinct().ToArray();
                    return titles.Length > 0 ? string.Join(" · ", titles) : definition.Source.Name;
                }
                if (node.Kind == "Exit") return "结束";
                if (node.Kind == "Wait") return "等待条件";
                string children = string.Join(" → ", (node.Children ?? new List<CharacterTestTacticalDslNode>()).Select(Brief));
                return node.Kind == "If" ? "条件成立时（" + children + "）" : children;
            }
            foreach (var card in workspace.Cards.Values.Where(c => c.HasAuthoredSource))
            {
                if (!string.IsNullOrEmpty(card.PublishedStableId)) represented.Add(card.PublishedStableId);
                string description = card.ShortDescription;
                if (string.IsNullOrWhiteSpace(description) || description == card.DisplayName)
                    try { description = card.HasCompositionSource ? Brief(card.CloneCompositionSource().Root) : card.CloneTacticalSource().Name; }
                    catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException || error is InvalidOperationException)
                    { description = "作者源需要检查：" + error.Message; }
                rows.Add(new CharacterTestTacticalLibraryEntry { Key = "draft:" + card.CardDraftId, Name = card.DisplayName,
                    Description = description, Draft = true,
                    Revision = "草稿 r" + card.CurrentRevision + (card.SourcePublishedRevision > 0 ? " · 源版本 r" + card.SourcePublishedRevision : " · 未发布") });
            }
            if (catalog != null)
                foreach (string id in catalog.Cards.Select(c => c.StableCardId).Distinct(StringComparer.Ordinal))
                    if (!represented.Contains(id) && catalog.TryResolveLatestCompatible(id,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var card)
                        && card.TacticalSource != null)
                        rows.Add(new CharacterTestTacticalLibraryEntry { Key = "published:" + id, Name = card.Metadata.DisplayName,
                            Description = card.Metadata.ShortDescription, Revision = "正式 r" + card.Revision });
            return rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Key, StringComparer.Ordinal).ToArray();
        }

        internal void EndStandaloneTacticalEditing() { if (!LeaveConfiguredCard()) return; EndTacticalCardTest(); TacticalCardContext = null; RefreshTacticalPreview(); }
        internal void RestoreStandaloneTacticalEditing(CharacterTestTacticalCardEditContext context)
        {
            if (context?.ConfiguredTestCardId != null)
            { BeginStandaloneTacticalEditing("test:" + context.ConfiguredTestCardId, out _); return; }
            if (!LeaveConfiguredCard()) return;
            TacticalCardContext = context; RefreshTacticalPreview();
        }

        internal bool BeginStandaloneTacticalEditing(string key, out string issue)
        {
            issue = string.Empty;
            string testKey = key?.StartsWith("test:", StringComparison.Ordinal) == true ? key : null;
            if (testKey != null)
            {
                try
                {
                    var source = CharacterTestTacticalPublishedSource.Read(ConfiguredWorkingCopy(testKey.Substring(5)).Card.Source);
                    if (!CanOpenTacticalBehaviorWorkspace(source, out issue)) return false;
                }
                catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException || error is InvalidOperationException)
                { issue = "作者源无法读取：" + error.Message; return false; }
                if (!OpenConfiguredCard(testKey.Substring(5), out key, out issue)) return false;
            }
            else if (IsConfiguredTestCardEditing && !LeaveConfiguredCard()) { issue = _statusMessage; return false; }
            if (DraftWorkspace == null || string.IsNullOrWhiteSpace(key)) { issue = "工作区或卡片身份不可用。"; return false; }
            CharacterTestRuntimeAiLabBehaviorCardDraft card;
            if (key.StartsWith("published:", StringComparison.Ordinal))
            {
                string stableId = key.Substring(10);
                if (ProjectCatalogState?.CardCatalog == null || !ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(stableId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var published)) return false;
                if (!CanOpenTacticalBehaviorWorkspace(published.TacticalSource, out issue)
                    || !DraftWorkspace.TryOpenStandaloneTacticalCard(published, out card, out issue)) return false;
                _draftPersistence?.MarkDirty();
            }
            else if (!DraftWorkspace.Cards.TryGetValue(key.StartsWith("draft:", StringComparison.Ordinal) ? key.Substring(6) : key, out card))
            { issue = "战术卡草稿已不存在，请刷新卡库。"; return false; }
            if (!card.HasAuthoredSource) { issue = "该卡不含战术作者源。"; return false; }
            try
            {
                if (card.HasCompositionSource) card.CloneCompositionSource();
                else if (!CanOpenTacticalBehaviorWorkspace(card.CloneTacticalSource(), out issue)) return false;
            }
            catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException || error is InvalidOperationException)
            { issue = "作者源无法读取：" + error.Message; return false; }
            var profile = DraftWorkspace.TryGetGroupForSlot(SelectedSetupTeamId, SelectedSetupSlotIndex, out var equipped)
                ? equipped.Profile.Clone() : new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("tactical-card-preview");
            profile.Entries.Clear();
            string presentationId;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(DraftWorkspace.WorkspaceId + ":tactical:" + card.CardDraftId));
                var bytes = new byte[16]; Array.Copy(digest, bytes, 16); presentationId = new Guid(bytes).ToString("D");
            }
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, 0) { EntryId = presentationId };
            profile.Entries.Add(runtimeEntry);
            var presentation = new CharacterTestRuntimeAiLabBehaviorGroupDraft(presentationId, string.Empty, card.DisplayName, profile);
            presentation.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(runtimeEntry.EntryId, card.CardDraftId,
                runtimeEntry.BehaviorId, 0, false, runtimeEntry));
            TacticalCardContext = new CharacterTestTacticalCardEditContext { WorkspaceId = DraftWorkspace.WorkspaceId, CardId = card.CardDraftId,
                ConfiguredTestCardId = testKey == null ? null : _configuredCard.Id,
                TeamId = SelectedSetupTeamId, SlotIndex = SelectedSetupSlotIndex, Presentation = presentation };
            TacticalLibrarySelectedKey = testKey ?? "draft:" + card.CardDraftId;
            if (testKey != null) UnityEngine.PlayerPrefs.SetString("RuntimeAiLab.TestCard.Last." + TestCardRole, testKey);
            RefreshTacticalPreview();
            return true;
        }

        internal static bool CanOpenTacticalBehaviorWorkspace(CharacterTestTacticalPublishedSource source, out string issue)
            => CanOpenTacticalBehaviorWorkspace(source?.CloneLegacy(), out issue);

        private static bool CanOpenTacticalBehaviorWorkspace(CharacterTestTacticalAuthoringSource source, out string issue)
        {
            issue = string.Empty;
            if (source?.ExecutionInput == null && source?.PositionRecall == null) return true;
            issue = "独立机制试用卡，尚未接入意图编排；不支持行为编辑和单卡行为测试。";
            return false;
        }

        internal bool TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            if (HasStandaloneTacticalCard) { group = TacticalCardContext.Presentation; return true; }
            return DraftWorkspace.TryGetGroupForSlot(SelectedSetupTeamId, SelectedSetupSlotIndex, out group);
        }

        internal bool TryGetAuthoringGroup(string id, out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            if (HasStandaloneTacticalCard && TacticalCardContext.Presentation.GroupDraftId == id)
            { group = TacticalCardContext.Presentation; return true; }
            if (_activeRevision?.IsSingleCardTest == true && _activeRevision.GroupDraftId == id)
            {
                if (!ReferenceEquals(_runtimeCardPresentationOwner, _activeRevision))
                {
                    var profile = _activeRevision.Profile;
                    _runtimeCardPresentation = new CharacterTestRuntimeAiLabBehaviorGroupDraft(id, string.Empty, profile.DisplayName, profile);
                    for (int i = 0; i < profile.Entries.Count; i++)
                    {
                        var entry = profile.Entries[i]; var identity = _activeRevision.CardIdentities[i];
                        _runtimeCardPresentation.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(entry.EntryId, identity.CardDraftId,
                            entry.BehaviorId, entry.Order, !entry.Enabled, entry));
                    }
                    _runtimeCardPresentationOwner = _activeRevision;
                }
                group = _runtimeCardPresentation; return true;
            }
            return DraftWorkspace.Groups.TryGetValue(id, out group);
        }
    }
}
