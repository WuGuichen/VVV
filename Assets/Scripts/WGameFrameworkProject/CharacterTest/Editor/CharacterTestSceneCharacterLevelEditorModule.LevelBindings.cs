using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        private void DrawLevelBehaviorBinding(VisualElement inspector, CharacterTestSceneCharacterDefinition character)
        {
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!state.Success) { inspector.Add(new Label(state.Diagnostic)); return; }
            var groups = state.GroupCatalog.Groups.OrderBy(g => g.DisplayName).ThenBy(g => g.Revision).ToArray();
            var labels = new[] { "无正式行为组" }.Concat(groups.Select(g => g.DisplayName + " / " + g.StableGroupId + " @" + g.Revision)).ToList();
            int current = character.BehaviorBinding == null ? 0 : Array.FindIndex(groups, g => g.StableGroupId == character.BehaviorBinding.GroupId && g.Revision == character.BehaviorBinding.GroupRevision) + 1;
            if (character.BehaviorBinding != null && current == 0)
            {
                labels.Add("缺失版本：" + character.BehaviorBinding.GroupId + " @" + character.BehaviorBinding.GroupRevision);
                current = labels.Count - 1;
            }
            var picker = new PopupField<string>("正式行为组版本", labels, current) { name = "mx-level-behavior-group" };
            picker.RegisterValueChangedCallback(evt => {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index > groups.Length) return;
                if (index != 0)
                {
                    string unsupported = KnownLevelGroupConsumerIssue(character, groups[index - 1], state);
                    if (!string.IsNullOrEmpty(unsupported))
                    {
                        _status = "未绑定，原配置保留：" + unsupported;
                        int retained = character.BehaviorBinding == null ? 0 : Array.FindIndex(groups,
                            g => g.StableGroupId == character.BehaviorBinding.GroupId && g.Revision == character.BehaviorBinding.GroupRevision) + 1;
                        picker.SetValueWithoutNotify(labels[Math.Max(0, retained)]);
                        _activeContext?.RequestRefresh?.Invoke();
                        return;
                    }
                }
                EnsureUndoSession();
                using var change = _undoSession.BeginTransaction("选择角色行为组版本");
                character.BehaviorBinding = index == 0 ? null : CharacterTestLevelBehaviorCompiler.Select(groups[index - 1]);
                if (index != 0) character.RuntimeAiPlannerProfilePath = string.Empty;
                MarkDirty(); change.Commit(); _activeContext?.RequestRefresh?.Invoke(); });
            inspector.Add(picker);
            inspector.Add(new Button(() => {
                if (CharacterTestLevelUpperTacticalGroupPreset.TryGetOrCreate(out var prepared, out var issue))
                    _status = "已准备正式战术组：" + prepared.DisplayName + "；请明确选择版本并填写动作槽。";
                else _status = "战术组准备失败，当前绑定保留：" + issue;
                _activeContext?.RequestRefresh?.Invoke();
            }) { text = "准备可试玩战术组", name = "mx-level-prepare-upper-group" });
            if (character.BehaviorBinding != null)
            {
                var binding = character.BehaviorBinding;
                inspector.Add(new Label("锁定组 hash: " + binding.GroupContentHash));
                var group = groups.SingleOrDefault(g => g.StableGroupId == binding.GroupId && g.Revision == binding.GroupRevision);
                DrawBehaviorGroupActions(inspector, character, group);
                if (group != null)
                {
                    string unsupported = KnownLevelGroupConsumerIssue(character, group, state);
                    if (!string.IsNullOrEmpty(unsupported)) inspector.Add(new HelpBox(
                        "已存版本引用保留。当前关卡消费能力边界：" + unsupported,
                        HelpBoxMessageType.Warning) { name = "mx-level-ai-capability" });
                }
                if (group != null) foreach (var card in group.Cards.Where(c => c.Enabled).OrderBy(c => c.Order))
                {
                    if (!state.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published))
                    {
                        inspector.Add(new HelpBox("缺失正式卡内容：" + card.StableCardId + " @" + card.CardRevision, HelpBoxMessageType.Error));
                        continue;
                    }
                    inspector.Add(new Label(published.Metadata.DisplayName + "：" + published.Metadata.UseSummary));
                    if (published.TacticalSource == null)
                    {
                        inspector.Add(new HelpBox("底层行为卡（已存兼容引用）；新的切片 AI 组需组织上层战术卡。底层定义与编辑入口保留。", HelpBoxMessageType.Info));
                        continue;
                    }
                    foreach (string slot in CharacterTestLevelBehaviorCompiler.Slots(published.TacticalSource))
                    {
                        var action = binding.Actions.SingleOrDefault(a => a.EntryId == card.EntryId && a.SlotPath == slot);
                        var available = CharacterTestActionBrowserCatalog.Get().Actions.ToArray();
                        var choices = new[] { "请选择动作" }.Concat(available.Select(a => a.Name + " / " + a.ActionId)).ToList();
                        int selectedAction = Array.FindIndex(available, a => a.ActionId == (action?.ActionId ?? 0)) + 1;
                        if (action != null && action.ActionId != 0 && selectedAction == 0)
                        { choices.Add("缺失动作：" + action.ActionId); selectedAction = choices.Count - 1; }
                        var field = new PopupField<string>(published.Metadata.DisplayName + " / " + slot, choices, selectedAction)
                            { name = "mx-level-slot-" + card.EntryId + "-" + slot };
                        field.RegisterValueChangedCallback(evt => {
                            int chosen = choices.IndexOf(evt.newValue);
                            if (chosen < 0 || chosen > available.Length) return;
                            EnsureUndoSession();
                            using var change = _undoSession.BeginTransaction("绑定角色卡牌动作槽");
                            var selected = binding.Actions.SingleOrDefault(a => a.EntryId == card.EntryId && a.SlotPath == slot);
                            if (selected == null)
                            {
                                selected = new CharacterTestLevelActionBinding { EntryId = card.EntryId, SlotPath = slot };
                                binding.Actions.Add(selected);
                            }
                            selected.ActionId = chosen == 0 ? 0 : available[chosen - 1].ActionId;
                            selected.DependencyFingerprint = "";
                            MarkDirty(); change.Commit();
                        });
                        inspector.Add(field);
                    }
                }
            }
            inspector.Add(CreateButton("当前关卡、检查与试玩", () => MxFramework.Editor.Levels.MxLevelEditorWindow.OpenWindow()));
        }

        // Read-only capability projection of current consumers, never a second gameplay rule owner.
        private static string KnownLevelGroupConsumerIssue(CharacterTestSceneCharacterDefinition actor,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            foreach (var card in group.Cards.Where(c => c.Enabled).OrderBy(c => c.Order))
            {
                if (!state.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published))
                    return "缺失发布版本：" + card.StableCardId + " @" + card.CardRevision;
                string name = published.Metadata.DisplayName;
                var source = published.TacticalSource;
                if (source == null)
                {
                    if (actor.ControlOwner == CharacterTestCharacterControlOwner.AiControlled)
                        return name + "是底层兼容卡；新的上层 AI 组可选择已准备的关卡战术组。";
                    continue;
                }
                if (source.InteractionPreparation != null) return name + "的准备效果尚未接入当前关卡组消费器。";
                if (source.PositionRecall != null) return name + "的记录／回返尚未接入当前关卡组消费器。";
                if (source.PointMove != null) return name + "尚未接入当前关卡组的选点执行 owner。";
                if (actor.ControlOwner == CharacterTestCharacterControlOwner.AiControlled && published.Metadata.Cost != 0)
                    return name + "的基础卡费尚未接入 AI 共用事务。";
                // Ordinary physical-action CD is already owned by ActionUseAuthority;
                // UsesCardCooldown applies only to PointMove, rejected above by this consumer.
            }
            return string.Empty;
        }
    }
}
