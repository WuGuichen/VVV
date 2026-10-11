using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly CharacterTestConfiguredCardLibrary _testCardLibrary = new();
        private CharacterTestConfiguredCard _configuredCard;
        private string _configuredRole, _configuredDraftId, _configuredExpectedHash, _configuredRecoverySignature, _configuredSavedSignature;
        private int _configuredExpectedRevision;
        private CharacterTestRuntimeAiLabDraftWorkspace _baseCardWorkspace;
        private CharacterTestRuntimeAiLabDraftPersistence _baseCardPersistence;
        private CharacterTestTacticalLibraryEntry[] _configuredRows = Array.Empty<CharacterTestTacticalLibraryEntry>();
        private string _configuredRowsIdentity;
        private long _configuredRowsChecked;
        private string _configuredContentKey, _configuredContentSignature, _configuredPendingRecovery;
        private long _configuredRecoveryDue;
        internal bool IsConfiguredTestCardEditing => _configuredCard != null;
        internal string ConfiguredTestCardId => _configuredCard?.Id;
        internal string ConfiguredCardName => DraftWorkspace?.Cards.TryGetValue(_configuredDraftId ?? _activeTacticalCardId ?? "", out var c) == true ? c.DisplayName : "测试卡";
        internal bool TestCardLibraryMode { get; set; }
        internal bool TestCardTemplatePicking { get; set; }
        internal string TestCardLibraryFilter { get; set; } = "all";
        internal Action<string, bool> ShowTestCardDialog;
        internal Action<string> ShowTestCardCommand;
        private CharacterTestRuntimeAiLabDraftWorkspace BaseCardWorkspace => _baseCardWorkspace ?? DraftWorkspace;
        internal string TestCardRole
        {
            get {
                int id = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
                return id > 0 ? "character.definition." + id.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            }
        }
        internal string TestCardOwnerLabel => _configuredCard == null ? "基础战术卡" : _configuredCard.Shared ? "共用测试卡" : "角色测试卡";
        internal string ConfiguredCardSaveStatus => !IsConfiguredTestCardEditing ? "" : _configuredExpectedRevision == 0
            || _configuredSavedSignature != RecoverySignature() ? "未保存到测试库（恢复草稿保留）" : "已保存到" + TestCardOwnerLabel + "库 · r" + _configuredExpectedRevision;

        internal CharacterTestConfiguredCard[] ConfiguredCards()
        {
            string role = TestCardRole; if (role.Length == 0) return Array.Empty<CharacterTestConfiguredCard>();
            var cards = _testCardLibrary.List(role).ToList();
            cards.AddRange(_testCardLibrary.NewRecoveries(role).Where(r => cards.All(c => c.Id != r.Id)));
            if (!string.IsNullOrEmpty(_testCardLibrary.Issue)) TacticalLibraryIssue = _testCardLibrary.Issue;
            return cards.ToArray();
        }

        internal CharacterTestTacticalLibraryEntry[] ConfiguredCardRows()
        {
            string owner = TestCardRole + ":" + TestCardLibraryFilter + ":" + CharacterTestTacticalPreparationCatalog.Revision + ":" + _tacticalActorRevision;
            long now = DateTime.UtcNow.Ticks;
            if (_configuredRowsIdentity?.StartsWith(owner + "|", StringComparison.Ordinal) == true && now - _configuredRowsChecked < TimeSpan.TicksPerSecond) return _configuredRows;
            _configuredRowsChecked = now;
            var records = ConfiguredCards();
            string identity = owner + "|" + string.Join("|", records.Select(c => c.Id + ":" + c.Revision + ":" + c.Name + ":" + c.FileHash
                + ":" + string.Join(",", c.Actions.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value))));
            if (identity == _configuredRowsIdentity) return _configuredRows;
            _configuredRowsIdentity = identity;
            var browser = CharacterTestActionBrowserCatalog.Get();
            return _configuredRows = records.Where(c => TestCardLibraryFilter == "all" || (TestCardLibraryFilter == "shared" ? c.Shared : !c.Shared))
                .Select(c => new CharacterTestTacticalLibraryEntry {
                    Key = "test:" + c.Id, Name = c.Name, Draft = true,
                    Revision = (c.Shared ? "共用" : "当前角色") + " · " + (c.Revision == 0 ? "待保存" : "r" + c.Revision),
                    Description = ConfiguredReadiness(c) + "\n来源：" + (c.BaseRevision ?? c.BaseKey) + "\n动作：" + (c.Actions.Count == 0 ? "无需绑定或待配置"
                        : string.Join("、", c.Actions.Values.Distinct().Select(id => id <= 0 ? "待配置" : browser.Actions.FirstOrDefault(a => a.ActionId == id)?.Name ?? "失效动作 " + id)))
                }).ToArray();
        }

        private string ConfiguredReadiness(CharacterTestConfiguredCard card)
        {
            var source = CharacterTestTacticalPublishedSource.Read(card.Source);
            IEnumerable<string> paths = source.IsLegacy ? source.CloneLegacy().Slots.Where(s => !s.IsFunctional).Select(s => s.SlotId)
                : CharacterTestTacticalCompositionEdits.Nodes(source.CloneComposition().Root).Where(n => n.Kind == "Reference")
                    .SelectMany(n => source.CloneDefinitions().First(d => d.DefinitionId == n.DefinitionId).Source.Slots.Where(s => !s.IsFunctional).Select(s => n.NodeId + "/" + s.SlotId));
            foreach (var path in paths)
            {
                if (!card.Actions.TryGetValue(path, out int id) || id <= 0) return "待配置：有动作尚未选择";
                var result = EvaluateBrowserAction(id);
                if (!result.CanSelect) return "待检查：" + result.Reason;
            }
            return "动作已配置 · 出牌前校验整卡";
        }

        internal bool CreateConfiguredCard(string baseKey, string name, bool shared, bool copyCurrent, out string key, out string issue)
        {
            key = null; issue = "";
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 128 || TestCardRole.Length == 0)
            { issue = "请填写名称并选择测试角色。"; return false; }
            try
            {
                CharacterTestConfiguredCard record;
                if (copyCurrent && IsConfiguredTestCardEditing) record = CaptureConfiguredCard();
                else
                {
                    var workspace = BaseCardWorkspace;
                    CharacterTestRuntimeAiLabBehaviorCardDraft card;
                    if (baseKey?.StartsWith("published:", StringComparison.Ordinal) == true)
                    {
                        if (!ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(baseKey.Substring(10),
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var published))
                            throw new InvalidOperationException("基础卡版本不存在。");
                        record = new CharacterTestConfiguredCard { Source = published.TacticalSource.ToJson(), BaseKey = baseKey,
                            Metadata = CharacterTestConfiguredCardMetadata.From(CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published)),
                            BaseRevision = published.Metadata.DisplayName + " r" + published.Revision };
                    }
                    else
                    {
                        string id = baseKey?.StartsWith("draft:", StringComparison.Ordinal) == true ? baseKey.Substring(6) : baseKey;
                        if (!workspace.Cards.TryGetValue(id ?? "", out card)) throw new InvalidOperationException("基础卡已不存在。");
                        record = new CharacterTestConfiguredCard { Source = CharacterTestTacticalPublishedSource.CaptureDraft(workspace, card).ToJson(),
                            Metadata = CharacterTestConfiguredCardMetadata.From(card),
                            BaseKey = "draft:" + id, BaseRevision = card.DisplayName + " 草稿 r" + card.CurrentRevision };
                        if (ReferenceEquals(workspace, DraftWorkspace))
                            foreach (string path in TacticalPreviewSlots(id)) record.Actions[path] = ResolveAuthoringActionId(TacticalPreviewAction(id, path).id);
                    }
                }
                record.Id = Guid.NewGuid().ToString("N"); record.Name = name.Trim(); record.OwnerCharacter = shared ? "" : TestCardRole;
                if (string.IsNullOrEmpty(record.BaseFingerprint)) record.BaseFingerprint = CharacterTestActionBrowserCatalog.Hash(
                    System.Text.Encoding.UTF8.GetBytes(record.Source.ToString(Newtonsoft.Json.Formatting.None)));
                record.Revision = 0; record.FileHash = null;
                FillConfiguredCardDefaults(record);
                _testCardLibrary.Recover(TestCardRole, record, 0, null);
                _configuredFacesKey = _configuredRowsIdentity = null;
                TestCardTemplatePicking = false; TestCardLibraryMode = true;
                key = "test:" + record.Id;
                return true;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException)
            { issue = error.Message; return false; }
        }

        private void FillConfiguredCardDefaults(CharacterTestConfiguredCard record)
        {
            var browser = CharacterTestActionBrowserCatalog.Get();
            var preferred = new[] { SelectedTacticalTestAction }.Concat(TacticalPreviewOwnedActions().Select(a => ResolveAuthoringActionId(a.id)));
            var preferredIds = new HashSet<int>(preferred.Where(id => id > 0));
            var candidates = preferred.Concat(browser.Actions.OrderBy(a => a.Category == MxFramework.CharacterAction.CharacterActionCategory.BasicAttack ? 0
                : a.Category == MxFramework.CharacterAction.CharacterActionCategory.Skill ? 1 : 2).ThenBy(a => a.ActionId).Select(a => a.ActionId))
                .Where(id => id > 0).Distinct().ToArray();
            var chosen = new Dictionary<bool, int>();
            CharacterTestConfiguredCardLibrary.FillMissingActions(record, spatial => {
                if (chosen.TryGetValue(spatial, out int cached)) return cached;
                int pendingSummary = 0;
                foreach (int id in candidates)
                {
                    var action = browser.Actions.FirstOrDefault(a => a.ActionId == id);
                    if (action == null || spatial && action.Category == MxFramework.CharacterAction.CharacterActionCategory.Dodge) continue;
                    var compatible = EvaluateBrowserAction(id);
                    if (compatible.CanSelect) { chosen[spatial] = id; return id; }
                    if (compatible.Diagnostic.StartsWith("ActionSummary:", StringComparison.Ordinal))
                    {
                        if (preferredIds.Contains(id)) { chosen[spatial] = id; return id; }
                        if (pendingSummary == 0) pendingSummary = id;
                    }
                }
                // An explicit, structurally supported action can be prefilled while its derived summary is still pending.
                chosen[spatial] = pendingSummary; return pendingSummary;
            });
        }

        private bool OpenConfiguredCard(string id, out string draftKey, out string issue)
        {
            draftKey = null; issue = "";
            string role = TestCardRole;
            try
            {
                if (IsConfiguredTestCardEditing && _configuredCard.Id == id && _configuredRole == role)
                {
                    var latest = _testCardLibrary.List(role).FirstOrDefault(c => c.Id == id);
                    if (RecoverySignature() == _configuredSavedSignature && latest != null && latest.FileHash != _configuredExpectedHash)
                        LeaveConfiguredCard(force: true);
                    else { draftKey = "draft:" + _configuredDraftId; return true; }
                }
                if (!LeaveConfiguredCard()) { issue = _statusMessage; return false; }
                var saved = ConfiguredCards().FirstOrDefault(c => c.Id == id);
                var recovery = _testCardLibrary.ReadRecovery(role, id);
                if (saved == null && recovery == null) throw new InvalidOperationException("测试卡已不存在。");
                if (saved != null && !saved.Shared && saved.OwnerCharacter != role) throw new InvalidOperationException("该测试卡属于其它角色。");
                _configuredCard = recovery?.Card ?? saved.Clone(); _configuredRole = role;
                _configuredExpectedRevision = recovery?.ExpectedRevision ?? saved.Revision;
                _configuredExpectedHash = recovery?.ExpectedHash ?? saved.FileHash;
                _baseCardWorkspace = _draftWorkspace; _baseCardPersistence = _draftPersistence;
                string workspaceFolder = CharacterTestActionBrowserCatalog.Hash(System.Text.Encoding.UTF8.GetBytes(role + ":" + id)).Substring(0, 24);
                _draftPersistence = new CharacterTestRuntimeAiLabDraftPersistence(Path.Combine("UserSettings/RuntimeAiLab/TestWork", workspaceFolder));
                _draftWorkspace = _draftPersistence.CreateFromWorkspace(_baseCardWorkspace);
                var card = DraftWorkspace.AddConfiguredTestCard(_configuredCard); _configuredDraftId = card.CardDraftId;
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string path in TacticalPreviewSlots(card.CardDraftId))
                    values[path] = _configuredCard.Actions.TryGetValue(path, out int action) && action > 0 ? AuthoringActionKey(action) : "";
                _tacticalSlotPreviews[TacticalSlotPreviewKey(card.CardDraftId)] = values;
                _tacticalSlotPreviewRevision++; _tacticalCompiled.Clear(); _tacticalPrepared.Clear();
                _configuredRecoverySignature = RecoverySignature();
                _configuredSavedSignature = recovery == null ? _configuredRecoverySignature : null;
                ResetBehaviorRecipeSaveCheckpoint(_draftWorkspace);
                _behaviorCardCompileCache.Clear();
                TestCardLibraryMode = true; TestCardTemplatePicking = false;
                draftKey = "draft:" + card.CardDraftId;
                return true;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException)
            { issue = error.Message; LeaveConfiguredCard(force: true); return false; }
        }

        private CharacterTestConfiguredCard CaptureConfiguredCard()
        {
            var record = _configuredCard.Clone(); var card = DraftWorkspace.Cards[_configuredDraftId];
            record.Name = card.DisplayName;
            record.Metadata = CharacterTestConfiguredCardMetadata.From(card);
            record.Source = CharacterTestTacticalPublishedSource.CaptureDraft(DraftWorkspace, card).ToJson();
            record.Actions.Clear();
            foreach (string path in TacticalPreviewSlots(card.CardDraftId)) record.Actions[path] = ResolveAuthoringActionId(TacticalPreviewAction(card.CardDraftId, path).id);
            return record;
        }

        internal bool SaveConfiguredCard(out string issue)
        {
            issue = "当前没有打开测试卡。"; if (!IsConfiguredTestCardEditing) return false;
            try
            {
                var card = CaptureConfiguredCard();
                if (!_testCardLibrary.Save(card, _configuredExpectedRevision, _configuredExpectedHash, out issue)) return false;
                _configuredCard = card; _configuredExpectedRevision = card.Revision; _configuredExpectedHash = card.FileHash;
                _configuredFacesKey = _configuredRowsIdentity = null;
                _testCardLibrary.ClearRecovery(_configuredRole, card.Id);
                _configuredRecoverySignature = RecoverySignature();
                _configuredSavedSignature = _configuredRecoverySignature;
                DraftWorkspace.LastExplicitSaveRevision = DraftWorkspace.CurrentRevision;
                issue = "已保存到" + TestCardOwnerLabel + "库 · r" + card.Revision; RefreshTacticalPreview(); return true;
            }
            catch (Exception error) { issue = "保存失败，草稿仍保留：" + error.Message; return false; }
        }
        internal bool ConfiguredCardCanPlay(out string issue)
        {
            issue = "请选择测试单卡。";
            if (!IsConfiguredTestCardEditing || !DraftWorkspace.Cards.TryGetValue(_configuredDraftId, out var card)) return false;
            foreach (string path in TacticalPreviewSlots(card.CardDraftId))
            {
                int id = ResolveAuthoringActionId(TacticalPreviewAction(card.CardDraftId, path).id);
                if (id <= 0) { issue = "有动作尚未选择，请完成配置后出牌。"; return false; }
                var compatibility = EvaluateBrowserAction(id);
                if (!compatibility.CanSelect) { issue = compatibility.Reason; return false; }
            }
            var result = CurrentTacticalCompilation(card)?.Result;
            if (result?.Success != true)
            { issue = result?.Diagnostics.FirstOrDefault().Message ?? "等待整卡编译，请检查时间轴及关联。"; return false; }
            issue = ""; return true;
        }
        private string RecoverySignature()
        {
            string key = DraftWorkspace.WorkspaceId + ":" + _configuredCard.Id + ":" + DraftWorkspace.CurrentRevision + ":" + _tacticalSlotPreviewRevision + ":" + DraftWorkspace.Cards[_configuredDraftId].DisplayName;
            if (_configuredContentKey != key)
            {
                var content = CaptureConfiguredCard(); content.Revision = 0;
                _configuredContentSignature = CharacterTestActionBrowserCatalog.Hash(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(content)));
                _configuredContentKey = key;
            }
            return _configuredContentSignature;
        }
        internal bool SaveConfiguredRecovery(out string issue, bool force = true)
        {
            issue = ""; if (!IsConfiguredTestCardEditing) return true;
            try
            {
                string signature = RecoverySignature(); if (_configuredRecoverySignature == signature) return true;
                if (signature == _configuredSavedSignature)
                { _testCardLibrary.ClearRecovery(_configuredRole, _configuredCard.Id); _configuredRecoverySignature = signature; return true; }
                if (!force)
                {
                    if (_configuredPendingRecovery != signature)
                    { _configuredPendingRecovery = signature; _configuredRecoveryDue = DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond / 2; return true; }
                    if (DateTime.UtcNow.Ticks < _configuredRecoveryDue) return true;
                }
                _testCardLibrary.Recover(_configuredRole, CaptureConfiguredCard(), _configuredExpectedRevision, _configuredExpectedHash);
                _configuredRecoverySignature = signature; return true;
            }
            catch (Exception error) { issue = "恢复草稿保存失败：" + error.Message; _statusMessage = issue; return false; }
        }
        internal bool LeaveConfiguredCard(bool force = false)
        {
            if (_configuredCard == null) return true;
            if (!force && !SaveConfiguredRecovery(out _)) return false;
            if (_baseCardWorkspace != null) { _draftWorkspace = _baseCardWorkspace; _draftPersistence = _baseCardPersistence; }
            _baseCardWorkspace = null; _baseCardPersistence = null; _configuredCard = null; _configuredDraftId = null;
            _configuredRecoverySignature = null; TacticalCardContext = null;
            _tacticalCompiled.Clear(); _tacticalPrepared.Clear();
            return true;
        }
        internal bool DeleteConfiguredCard(string id, out string issue, int expectedRevision = -1, string expectedHash = null)
        {
            var card = ConfiguredCards().FirstOrDefault(c => c.Id == id);
            issue = "测试卡已不存在。"; if (card == null) return false;
            if (IsHistoricalFrameSelected || TacticalCardTestInFlight) { issue = "请在当前卡结束后删除测试卡。"; return false; }
            if (expectedRevision >= 0 && (card.Revision != expectedRevision || card.FileHash != expectedHash))
            { issue = "测试卡版本已变化，请重新确认删除。"; return false; }
            if (IsConfiguredTestCardEditing && _configuredCard.Id == id && !LeaveConfiguredCard()) { issue = _statusMessage; return false; }
            if (card.Revision > 0 && !_testCardLibrary.Delete(card, out issue)) return false;
            _testCardLibrary.ClearRecovery(TestCardRole, id);
            _configuredFacesKey = _configuredRowsIdentity = null;
            issue = "已删除测试卡，基础卡保持不变。"; return true;
        }
        internal void RenameConfiguredCard(string name)
        {
            if (!IsHistoricalFrameSelected && IsConfiguredTestCardEditing && !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 128)
            { DraftWorkspace.Cards[_configuredDraftId].DisplayName = name.Trim(); SaveConfiguredRecovery(out _); RefreshTacticalPreview(); }
        }
        internal bool DiscardConfiguredEdits(out string key, out string issue)
        {
            key = null; issue = "当前没有可重新载入的测试卡。";
            if (!IsConfiguredTestCardEditing || IsHistoricalFrameSelected || TacticalCardTestInFlight) return false;
            string id = _configuredCard.Id;
            if (!_testCardLibrary.List(_configuredRole).Any(c => c.Id == id)) return false;
            _testCardLibrary.ClearRecovery(_configuredRole, id); LeaveConfiguredCard(force: true);
            key = "test:" + id; _configuredFacesKey = _configuredRowsIdentity = null; return true;
        }
    }
}
