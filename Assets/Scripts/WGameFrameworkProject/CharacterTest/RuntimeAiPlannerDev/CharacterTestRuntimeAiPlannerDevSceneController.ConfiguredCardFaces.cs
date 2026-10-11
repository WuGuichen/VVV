using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestConfiguredCardFace
    {
        internal string Id, Name, Owner, Description, Details, Issue, Token, IconKey;
        internal bool Dirty, BehaviorWorkspaceAvailable;
        internal CharacterTestTacticalActionLocation[] Actions;
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestConfiguredCardFace[] _configuredFaces = Array.Empty<CharacterTestConfiguredCardFace>();
        private string _configuredFacesKey;
        private long _configuredFacesChecked;

        private CharacterTestConfiguredCardRecovery ConfiguredWorkingCopy(string id, CharacterTestConfiguredCard existing = null)
        {
            if (_configuredCard?.Id == id && _configuredRole == TestCardRole)
                return new CharacterTestConfiguredCardRecovery { Card = CaptureConfiguredCard(),
                    ExpectedHash = _configuredExpectedHash, ExpectedRevision = _configuredExpectedRevision };
            var saved = existing ?? ConfiguredCards().FirstOrDefault(c => c.Id == id);
            if (saved == null) throw new InvalidOperationException("测试卡已不存在，请刷新卡库。");
            return _testCardLibrary.ReadRecovery(TestCardRole, id) ?? new CharacterTestConfiguredCardRecovery {
                Card = saved.Clone(), ExpectedHash = saved.FileHash, ExpectedRevision = saved.Revision };
        }

        private static string ConfiguredFaceToken(CharacterTestConfiguredCardRecovery copy)
            => CharacterTestActionBrowserCatalog.Hash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(copy)));

        internal static CharacterTestTacticalActionLocation[] ConfiguredActionLocations(CharacterTestConfiguredCard card)
        {
            var result = new List<CharacterTestTacticalActionLocation>();
            var snapshot = CharacterTestTacticalPublishedSource.Read(card.Source);
            void Add(CharacterTestTacticalAuthoringSource source, string prefix, string context)
            {
                foreach (var slot in source.Slots.Where(s => !s.IsFunctional))
                {
                    string path = prefix + slot.SlotId;
                    card.Actions.TryGetValue(path, out int id);
                    string label = TacticalActionUseLabel(source, slot.SlotId);
                    result.Add(new CharacterTestTacticalActionLocation { Path = path,
                        Label = context + (label == "未关联事件" ? "动作配置" : label),
                        ActionId = id > 0 ? AuthoringActionKey(id) : "" });
                }
            }
            if (snapshot.IsLegacy) Add(snapshot.CloneLegacy(), "", "");
            else
            {
                var definitions = snapshot.CloneDefinitions();
                var references = CharacterTestTacticalCompositionEdits.Nodes(snapshot.CloneComposition().Root)
                    .Where(n => n.Kind == "Reference").ToArray();
                for (int i = 0; i < references.Length; i++)
                {
                    var node = references[i];
                    var definition = definitions.First(d => d.DefinitionId == node.DefinitionId);
                    Add(definition.Source, node.NodeId + "/", references.Length > 1 ? "第 " + (i + 1) + " 项 · " : "");
                }
            }
            return result.ToArray();
        }

        private string ConfiguredFaceDescription(CharacterTestConfiguredCard card, CharacterTestActionBrowserData catalog)
        {
            if (!string.IsNullOrWhiteSpace(card.Metadata.ShortDescription)) return card.Metadata.ShortDescription;
            if (!string.IsNullOrWhiteSpace(card.Metadata.UseSummary)) return card.Metadata.UseSummary;
            var snapshot = CharacterTestTacticalPublishedSource.Read(card.Source);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var location in ConfiguredActionLocations(card))
            {
                int id = ResolveAuthoringActionId(location.ActionId);
                var action = catalog.Actions.FirstOrDefault(a => a.ActionId == id);
                if (action != null) names[(snapshot.IsLegacy ? "preview-legacy/" : "") + location.Path] = action.Name;
            }
            return CharacterTestTacticalDslDescription.GenerateLocalized(CardUiPreviewDocument(card, snapshot), null,
                attempted: true, cardFace: true, selectedActions: names, textProvider: CardDescriptionTextProvider,
                frameSeconds: CardDescriptionFrameSeconds).Text;
        }

        internal CharacterTestConfiguredCardFace[] ConfiguredCardFaces()
        {
            string context = TestCardRole + ":" + CharacterTestTacticalPreparationCatalog.Revision + ":" + _tacticalActorRevision
                + ":" + _configuredCard?.Id + ":" + (IsConfiguredTestCardEditing ? RecoverySignature() : "")
                + ":" + CharacterTestUiText.Signature(CardDescriptionTextProvider);
            long now = DateTime.UtcNow.Ticks;
            if (_configuredFacesKey == context && now - _configuredFacesChecked < TimeSpan.TicksPerSecond) return _configuredFaces;
            _configuredFacesChecked = now;
            var catalog = CharacterTestActionBrowserCatalog.Get();
            var faces = new List<CharacterTestConfiguredCardFace>();
            foreach (var saved in ConfiguredCards())
            {
                try
                {
                    var copy = ConfiguredWorkingCopy(saved.Id, saved); var card = copy.Card;
                    bool behaviorWorkspaceAvailable = CanOpenTacticalBehaviorWorkspace(
                        CharacterTestTacticalPublishedSource.Read(card.Source), out string workspaceIssue);
                    var actions = ConfiguredActionLocations(card);
                    string issue = workspaceIssue;
                    string guidance = behaviorWorkspaceAvailable ? "" : "此卡保留为机制试用数据，不能进入意图行为卡工作区。";
                    foreach (var action in actions)
                    {
                        int id = ResolveAuthoringActionId(action.ActionId);
                        action.ActionName = catalog.Actions.FirstOrDefault(a => a.ActionId == id)?.Name ?? (id > 0 ? "失效动作" : "选择动作");
                        var compatibility = id > 0 ? EvaluateBrowserAction(id) : default;
                        if (issue.Length == 0 && (id <= 0 || !compatibility.CanSelect))
                        {
                            issue = id <= 0 ? "有动作尚未选择" : compatibility.Reason;
                            guidance = id <= 0 ? "请在卡面为这个动作选择可用的战术动作。" : compatibility.Guidance;
                        }
                    }
                    bool active = _configuredCard?.Id == card.Id && _configuredRole == TestCardRole;
                    bool dirty = copy.ExpectedRevision == 0 || (active ? RecoverySignature() != _configuredSavedSignature
                        : _testCardLibrary.ReadRecovery(TestCardRole, card.Id) != null);
                    faces.Add(new CharacterTestConfiguredCardFace { Id = card.Id, Name = card.Name,
                        Owner = card.Shared ? "共用" : "当前角色", Dirty = dirty, Actions = actions,
                        BehaviorWorkspaceAvailable = behaviorWorkspaceAvailable,
                        Token = ConfiguredFaceToken(copy), IconKey = card.Metadata.IconKey,
                        Description = ConfiguredFaceDescription(card, catalog),
                        Details = (string.IsNullOrEmpty(issue) ? "动作已配置，出牌前校验整卡。" : "问题：" + issue
                            + (string.IsNullOrEmpty(guidance) ? "" : "\n处理：" + guidance))
                            + "\n来源：" + (card.BaseRevision ?? card.BaseKey) + "\n" + (copy.ExpectedRevision == 0 ? "尚未保存到库" : "库版本：r" + copy.ExpectedRevision),
                        Issue = issue });
                }
                catch (Exception error)
                {
                    faces.Add(new CharacterTestConfiguredCardFace { Id = saved.Id, Name = saved.Name,
                        Owner = saved.Shared ? "共用" : "当前角色", Issue = error.Message, Details = error.Message,
                        Actions = Array.Empty<CharacterTestTacticalActionLocation>() });
                }
            }
            _configuredFacesKey = context;
            // Preserve projection identity when polling unchanged files, so focused inputs and scrolling survive.
            if (JsonConvert.SerializeObject(faces.Select(f => new { f.Id, f.Token, f.Issue, f.Dirty, f.Description, names = f.Actions.Select(a => a.ActionName) }))
                != JsonConvert.SerializeObject(_configuredFaces.Select(f => new { f.Id, f.Token, f.Issue, f.Dirty, f.Description, names = f.Actions.Select(a => a.ActionName) })))
                _configuredFaces = faces.ToArray();
            return _configuredFaces;
        }

        internal bool EditConfiguredCardFace(string id, string role, string token, string name, string path, string action, out string issue)
        {
            issue = "";
            try
            {
                if (role != TestCardRole || IsHistoricalFrameSelected) throw new InvalidOperationException("当前角色或编辑状态已变化，请重新操作。");
                var copy = ConfiguredWorkingCopy(id);
                if (ConfiguredFaceToken(copy) != token) throw new InvalidOperationException("这张卡已被修改，请刷新后再操作。");
                if (name != null && (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 128)) throw new InvalidOperationException("名称不能为空且不能超过 128 字。");
                bool active = _configuredCard?.Id == id && _configuredRole == role;
                if (path != null)
                {
                    if (!ConfiguredActionLocations(copy.Card).Any(a => a.Path == path)) throw new InvalidOperationException("此动作配置已不存在。");
                    int number = ResolveAuthoringActionId(action);
                    var compatibility = EvaluateBrowserAction(number, force: true);
                    if (!compatibility.CanSelect) throw new InvalidOperationException(compatibility.Reason);
                    if (active && !SetTacticalPreviewSlot(_configuredDraftId, path, action)) throw new InvalidOperationException(StatusMessage);
                    copy.Card.Actions[path] = number;
                }
                if (name != null)
                {
                    copy.Card.Name = name.Trim();
                    if (active) DraftWorkspace.Cards[_configuredDraftId].DisplayName = copy.Card.Name;
                }
                if (active) { if (!SaveConfiguredRecovery(out issue)) return false; RefreshTacticalPreview(); }
                else _testCardLibrary.Recover(role, copy.Card, copy.ExpectedRevision, copy.ExpectedHash);
                _configuredFacesKey = _configuredRowsIdentity = null;
                return true;
            }
            catch (Exception error) { issue = error.Message; return false; }
        }

        internal bool SaveConfiguredCardFace(string id, string role, out string issue)
        {
            issue = "";
            try
            {
                if (role != TestCardRole || IsHistoricalFrameSelected) throw new InvalidOperationException("当前角色或编辑状态已变化。");
                if (_configuredCard?.Id == id && _configuredRole == role) return SaveConfiguredCard(out issue);
                var copy = ConfiguredWorkingCopy(id);
                if (!_testCardLibrary.Save(copy.Card, copy.ExpectedRevision, copy.ExpectedHash, out issue)) return false;
                _testCardLibrary.ClearRecovery(role, id); issue = "已保存到" + (copy.Card.Shared ? "共用" : "角色") + "库";
                return true;
            }
            catch (Exception error) { issue = error.Message; return false; }
            finally { _configuredFacesKey = _configuredRowsIdentity = null; }
        }

        internal bool CopyConfiguredCardFace(string id, string name, bool shared, out string key, out string issue)
        {
            key = null; issue = "";
            try
            {
                if (IsHistoricalFrameSelected || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 128)
                    throw new InvalidOperationException("请在当前编辑状态填写有效名称。");
                var copy = ConfiguredWorkingCopy(id).Card;
                copy.Id = Guid.NewGuid().ToString("N"); copy.Name = name.Trim(); copy.Revision = 0; copy.FileHash = null;
                copy.OwnerCharacter = shared ? "" : TestCardRole;
                _testCardLibrary.Recover(TestCardRole, copy, 0, null);
                _configuredFacesKey = _configuredRowsIdentity = null; key = "test:" + copy.Id;
                return true;
            }
            catch (Exception error) { issue = error.Message; return false; }
        }
    }
}
