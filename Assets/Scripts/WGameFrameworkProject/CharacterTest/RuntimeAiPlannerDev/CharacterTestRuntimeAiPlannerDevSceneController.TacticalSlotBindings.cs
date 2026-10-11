using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalActionLocation
    {
        internal string Path, Label, ActionId, ActionName;
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly Dictionary<string, Dictionary<string, string>> _tacticalSlotPreviews = new(StringComparer.Ordinal);
        private long _tacticalSlotPreviewRevision;
        private string TacticalSlotPreviewKey(string cardId) => IsConfiguredTestCardEditing && cardId == _configuredDraftId
            ? "configured:" + _configuredCard.Id + ":" + _configuredRole : SelectedTacticalTestActionKey + ":" + cardId;
        internal string TacticalPreviewBindingContextKey => SelectedTacticalTestActionKey;
        internal long TacticalActionBindingRevision => _tacticalSlotPreviewRevision;
        internal static string TacticalActionEventTitle(CharacterTestTacticalAuthoringEvent ev)
            => string.IsNullOrWhiteSpace(ev.Title) || ev.Title == ev.EventId || ev.Title == ev.Operation.Kind || ev.Title == "主攻击槽"
                ? ev.Operation.Kind switch {
                    "CharacterAction" => "执行动作", "Movement" => "移动", "TurnToTarget" => "面向目标",
                    "WaitForCondition" => "等待条件", "ReturnToPlanner" => "结束", "EnterTimeline" => "进入时间轴",
                    "LoopRegion" => "循环区间", "ApproachOptimalRange" => "接近最佳位置", "RetreatFromTarget" => "后撤", _ => "事件" }
                : ev.Title;

        internal static string TacticalActionUseLabel(CharacterTestTacticalAuthoringSource source, string slot)
        {
            if (string.IsNullOrWhiteSpace(slot)) return "未关联事件";
            var events = source.Timelines.SelectMany(t => t.Events).Where(e =>
                e.Operation.Kind == "CharacterAction" && e.Operation.SlotId == slot
                || e.Operation.Kind == "TurnToTarget" && e.Operation.FacingSource == "ActionFacing" && e.Operation.FacingActionSlotId == slot
                || e.Operation.Movement?.ActionSlotId == slot && e.Operation.Movement.DistanceSource != "Manual").ToArray();
            var names = events.Select(e => TacticalActionEventTitle(e)
                + (e.Operation.Kind == "CharacterAction" ? "" : e.Operation.Kind == "TurnToTarget" ? "（朝向参考）" : "（距离参考）")).ToArray();
            return names.Length == 0 ? "未关联事件" : string.Join("、", names)
                + (names.Length > 1 ? "（共用配置）" : "");
        }

        internal CharacterTestTacticalActionLocation[] TacticalActionLocations(string cardId)
        {
            var result = new List<CharacterTestTacticalActionLocation>();
            if (DraftWorkspace == null || !DraftWorkspace.Cards.TryGetValue(cardId ?? "", out var card)) return result.ToArray();
            void Add(CharacterTestTacticalAuthoringSource source, string prefix, string context)
            {
                foreach (var slot in source.Slots.Where(s => !s.IsFunctional))
                {
                    string label = TacticalActionUseLabel(source, slot.SlotId);
                    if (label == "未关联事件") continue;
                    string path = prefix + slot.SlotId;
                    var action = TacticalPreviewAction(cardId, path);
                    result.Add(new CharacterTestTacticalActionLocation { Path = path, Label = context + label,
                        ActionId = action.id, ActionName = action.label });
                }
            }
            if (card.HasTacticalSource) Add(card.CloneTacticalSource(), "", "");
            else if (card.HasCompositionSource)
            {
                var document = DraftWorkspace.ResolveComposition(card, referencedOnly: true);
                var references = CharacterTestTacticalCompositionEdits.Nodes(document.Root).Where(n => n.Kind == "Reference").ToArray();
                for (int i = 0; i < references.Length; i++)
                {
                    var node = references[i];
                    var definition = document.Definitions.First(d => d.DefinitionId == node.DefinitionId);
                    Add(definition.Source, node.NodeId + "/", (references.Length > 1 ? "第 " + (i + 1) + " 项 · " : "")
                        + definition.Source.Name + " / ");
                }
            }
            return result.ToArray();
        }

        internal (string id, string label) TacticalPreviewAction(string cardId, string path)
        {
            if (DraftWorkspace == null || !DraftWorkspace.Cards.TryGetValue(cardId ?? "", out var card)) return (null, "未选择动作");
            var paths = TacticalPreviewSlots(cardId).ToArray();
            if (!paths.Contains(path)) return (null, "未关联动作配置");
            int character = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            var player = CharacterTacticalServices.Player;
            var definition = CharacterTestPlayableCharacterCatalog.Discover().FirstOrDefault(c => c.CharacterId == character);
            var owned = definition.CharacterId <= 0 ? null : player?.Read(definition.TacticalDefaults?.CharacterStableId);
            string id = ResolveTacticalPreviewActionId(card, path, paths, owned, player);
            int number = ResolveAuthoringActionId(id);
            var action = CharacterTestActionBrowserCatalog.Get().Actions.FirstOrDefault(a => a.ActionId == number);
            return (number > 0 ? AuthoringActionKey(number) : null, action?.Name ?? player?.Catalog.Action(id)?.DisplayName ?? "未选择动作");
        }

        internal bool ResetTacticalPreviewAction(string cardId, string path)
        {
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly || IsHistoricalFrameSelected
                || !TacticalPreviewSlots(cardId).Contains(path)) return false;
            if (_tacticalSlotPreviews.TryGetValue(TacticalSlotPreviewKey(cardId), out var values)) values.Remove(path);
            _tacticalSlotPreviewRevision++; _tacticalCompiled.Clear(); _tacticalPrepared.Clear(); RefreshTacticalPreview();
            _statusMessage = "已恢复此处的默认动作，下次出牌／测试生效。";
            return true;
        }

        private string ResolveTacticalPreviewActionId(CharacterTestRuntimeAiLabBehaviorCardDraft card, string path,
            string[] paths, CharacterTacticalDefaults owned, CharacterTacticalPlayerService player)
        {
            if (_tacticalSlotPreviews.TryGetValue(TacticalSlotPreviewKey(card.CardDraftId), out var preview)
                && preview.TryGetValue(path, out string overridden)) return overridden;
            if (IsConfiguredTestCardEditing && card.CardDraftId == _configuredDraftId)
                return _configuredCard.Actions.TryGetValue(path, out int bound) && bound > 0 ? AuthoringActionKey(bound) : null;
            if (owned == null || player == null) return SelectedTacticalTestAction > 0 ? AuthoringActionKey(SelectedTacticalTestAction) : null;
            string id = (owned.Bindings.Find(b => b.CardId == card.PublishedStableId && b.SlotPath == path)
                ?? (paths.Length == 1 ? owned.Bindings.Find(b => b.CardId == card.PublishedStableId && b.SlotPath == "action") : null))?.TacticalActionId;
            if (string.IsNullOrEmpty(id) && SelectedTacticalTestAction > 0)
                id = owned.ActionIds.Select(player.Catalog.Action).FirstOrDefault(a => a != null && a.ActionId == SelectedTacticalTestAction)?.Id;
            if (string.IsNullOrEmpty(id))
                id = (paths.Length == 1 ? owned.Bindings.FirstOrDefault(b => player.Catalog.ContainsAction(owned.ActionIds, b.TacticalActionId))
                    : owned.Bindings.Find(b => b.SlotPath == path && player.Catalog.ContainsAction(owned.ActionIds, b.TacticalActionId)))?.TacticalActionId
                    ?? owned.Bindings.FirstOrDefault(b => player.Catalog.ContainsAction(owned.ActionIds, b.TacticalActionId))?.TacticalActionId;
            return string.IsNullOrEmpty(id) ? owned.ActionIds.FirstOrDefault(a => player.Catalog.Action(a) != null) : id;
        }
        internal IEnumerable<string> TacticalPreviewSlots(string cardId)
        {
            if (DraftWorkspace == null || !DraftWorkspace.Cards.TryGetValue(cardId ?? string.Empty, out var card)) return Array.Empty<string>();
            if (card.HasTacticalSource) return card.CloneTacticalSource().Slots.Where(s => !s.IsFunctional).Select(s => s.SlotId).ToArray();
            if (!card.HasCompositionSource) return Array.Empty<string>();
            var document = DraftWorkspace.ResolveComposition(card, referencedOnly: true);
            return CharacterTestTacticalCompositionEdits.Nodes(document.Root).Where(n => n.Kind == "Reference")
                .SelectMany(n => document.Definitions.First(d => d.DefinitionId == n.DefinitionId).Source.Slots.Where(s => !s.IsFunctional).Select(s => n.NodeId + "/" + s.SlotId)).ToArray();
        }

        internal IEnumerable<(string id, string label)> TacticalPreviewOwnedActions()
        {
            int characterId = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            var player = CharacterTacticalServices.Player;
            var definition = CharacterTestPlayableCharacterCatalog.Discover().FirstOrDefault(c => c.CharacterId == characterId);
            var state = player?.Read(definition.TacticalDefaults?.CharacterStableId);
            return state == null ? Array.Empty<(string, string)>() : state.ActionIds.Select(id =>
                (id, player.Catalog.Action(id)?.DisplayName ?? id + "（名称待完善）")).ToArray();
        }

        internal bool SetTacticalPreviewSlot(string cardId, string path, string action)
        {
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly || IsHistoricalFrameSelected || !TacticalPreviewSlots(cardId).Contains(path)) return false;
            if (!string.IsNullOrEmpty(action))
            {
                int id = ResolveAuthoringActionId(action);
                var compatible = EvaluateBrowserAction(id, force: true);
                if (!compatible.CanSelect) { _statusMessage = compatible.Reason; return false; }
                action = AuthoringActionKey(id);
            }
            string key = TacticalSlotPreviewKey(cardId);
            if (!_tacticalSlotPreviews.TryGetValue(key, out var values)) _tacticalSlotPreviews.Add(key, values = new Dictionary<string, string>(StringComparer.Ordinal));
            values[path] = action ?? string.Empty;
            _tacticalSlotPreviewRevision++; _tacticalCompiled.Clear(); _tacticalPrepared.Clear();
            RefreshTacticalPreview();
            _statusMessage = "已修改此处使用的动作，下次出牌／测试生效。";
            return true;
        }

        private bool TryPrepareCardSlotBindings(CharacterTestRuntimeAiLabBehaviorCardDraft card, int character,
            out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null; issue = string.Empty;
            var paths = TacticalPreviewSlots(card.CardDraftId).ToArray();
            var prepared = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            var definition = CharacterTestPlayableCharacterCatalog.Discover().FirstOrDefault(c => c.CharacterId == character);
            if (definition.CharacterId != character)
            {
                issue = "当前角色 " + character + " 不在角色目录，请在编队设置中重新选择角色。";
                return false;
            }
            var role = CharacterTestTacticalPreparationCatalog.Role(character, _context?.Slice?.CharacterRuntime?.ActionRuntime);
            if (paths.Length == 0)
            {
                CharacterActionConfig stepBack = null;
                string stepBackHash = null;
                CharacterActionConfig stepForward = null;
                string stepForwardHash = null;
                if (role != null)
                {
                    CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                        CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                        new CharacterActionTacticalMetricsStore(), CharacterTestTacticalSummaryPreparation.StandardMeasurement(),
                        out var dummy, out _, 0, CharacterTestTacticalInputRequirements.FunctionalActions);
                    stepBack = dummy?.StepBackAction;
                    stepBackHash = dummy?.StepBackContentHash;
                    stepForward = dummy?.StepForwardAction; stepForwardHash = dummy?.StepForwardContentHash;
                }
                inputs = CharacterTestTacticalCardInputs.BindSlots(character, prepared, stepBack, stepBackHash, stepForward, stepForwardHash);
                return true;
            }
            var player = CharacterTacticalServices.Player;
            var owned = player?.Read(definition.TacticalDefaults?.CharacterStableId);
            foreach (string path in paths)
            {
                string id = ResolveTacticalPreviewActionId(card, path, paths, owned, player);
                int actionId = ResolveAuthoringActionId(id);
                if (actionId <= 0)
                { issue = "请选择可用动作：" + (TacticalActionLocations(card.CardDraftId).FirstOrDefault(p => p.Path == path)?.Label ?? card.DisplayName); return false; }
                if (!CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                    CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                    new CharacterActionTacticalMetricsStore(), CharacterTestTacticalSummaryPreparation.StandardMeasurement(),
                    out var value, out issue, actionId,
                    CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary | CharacterTestTacticalInputRequirements.FunctionalActions)) return false;
                prepared.Add(path, value);
            }
            inputs = CharacterTestTacticalCardInputs.BindSlots(character, prepared); return true;
        }
    }
}
