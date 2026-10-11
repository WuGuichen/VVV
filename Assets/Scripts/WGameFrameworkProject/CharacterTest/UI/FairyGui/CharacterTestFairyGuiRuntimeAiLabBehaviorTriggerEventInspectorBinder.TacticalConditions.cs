using System.Linq;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private sealed class TacticalConditionLease
        {
            internal CharacterTestTacticalEditToken Token;
            internal string Timeline, Event;
            internal CharacterTestTacticalEditingSession Editor;
            internal int LeafIndex = -1;
        }
        private TacticalConditionLease _tacticalConditionLease;
        private void CaptureTacticalConditionLease(Fgui.EventContext context = null)
        {
            if (_model?.TacticalEditor?.ConditionsOpen != true) { _tacticalConditionLease = null; return; }
            _tacticalConditionLease = new TacticalConditionLease { Token = _model.TacticalToken, Editor = _model.TacticalEditor,
                Timeline = _model.TacticalConditionTimeline, Event = _model.TacticalConditionEvent };
            var target = context?.sender as Fgui.GTextInput;
            for (Fgui.GObject row = target; row != null; row = row.parent)
                if (row.parent == _root.GetChild("conditionList") && row.data is int index && index >= 0 && index < _model.Conditions.Length)
                { _tacticalConditionLease.LeafIndex = _model.Conditions[index].LeafIndex; break; }
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode FindMovementQuery(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, string id)
        {
            if (node == null) return null;
            if (node.NodeId == id && node.MovementQuery != null) return node;
            foreach (var child in node.Children) { var found = FindMovementQuery(child, id); if (found != null) return found; }
            return null;
        }

        private void BindTacticalDataConditions()
        {
            var editor = _model.TacticalEditor;
            string timeline = _model.TacticalConditionTimeline, eventId = _model.TacticalConditionEvent;
            var list = _root.GetChild("conditionList")?.asList;
            if (list != null) foreach (var item in list.GetChildren())
            {
                if (!(item.data is int index) || index < 0 || index >= _model.Conditions.Length) continue;
                var query = FindMovementQuery(editor.Source.ActivationConditionRoot, _model.Conditions[index].NodeId);
                if (query != null)
                {
                    var queryRow = item.asCom;
                    foreach (var child in queryRow.GetChildren()) child.visible = false;
                    var label = queryRow.GetChild("btnFact")?.asButton;
                    if (label != null) { label.visible = true; label.enabled = false; label.title = (query.MovementQuery == "Arrived" ? "移动到位：" : "目标失效：") + query.SourceEventId;
                        label.SetXY(8, 6); label.SetSize(queryRow.width - 56, 32); }
                    var remove = queryRow.GetChild("btnDelete");
                    if (remove != null) remove.visible = true;
                    continue;
                }
                var binding = editor.Source.PredicateBindings.FirstOrDefault(b => b.NodeId == _model.Conditions[index].NodeId);
                var row = item.asCom;
                if (binding == null || row == null) continue;
                foreach (string name in new[] { "inputValue", "inputMaxValue", "inputValueBackground", "inputMaxValueBackground", "btnOperator" })
                { var child = row.GetChild(name); if (child != null) child.visible = false; }
                var button = row.GetChild("btnValueDropdown")?.asButton;
                if (button == null) continue;
                bool movementBinding = binding.Kind == "MovementArrived";
                bool turnBinding = binding.Kind == "TurnAligned";
                button.visible = true; button.enabled = editor.CanEdit && binding.Kind != "BoundActionOpportunity";
                string sourceId = movementBinding ? binding.SourceMovementEventId : turnBinding ? binding.SourceTurnEventId : binding.SourceActionEventId;
                var sourceEvent = editor.Source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == sourceId);
                button.title = "→ " + (sourceEvent?.Title ?? sourceId);
                if (turnBinding) button.tooltips = "引用转向事件：" + (sourceEvent?.Title ?? sourceId)
                    + "\n当前解析范围：" + editor.Preview.DescribeTurnFacing(binding.SourceTurnEventId);
                if (movementBinding && row.GetChild("btnFact")?.asButton is Fgui.GButton fact)
                { fact.title = "移动到位"; fact.tooltips = "引用移动事件的实际距离配置；不复制距离阈值。"; }
                if (turnBinding && row.GetChild("btnFact")?.asButton is Fgui.GButton turnFact)
                { turnFact.title = "面向达标"; turnFact.tooltips = "引用转向事件的面向参数；转向事件可以引用当前装配动作的朝向范围。"; }
                button.SetXY(210, 6); button.SetSize(UnityEngine.Mathf.Max(96, row.width - 258), 32);
                string nodeId = binding.NodeId;
                button.onClick.Set(() => {
                    var token = editor.Token;
                    var ids = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events.Where(e => e.Operation.Kind == (movementBinding ? "Movement" : turnBinding ? "TurnToTarget" : "CharacterAction")).Select(e => e.EventId).ToArray();
                    TacticalChoices(button, ids, selected => editor.Commit(token, "修改条件来源", source => {
                        var target = source.PredicateBindings.Single(b => b.NodeId == nodeId);
                        if (movementBinding) target.SourceMovementEventId = selected;
                        else if (turnBinding) target.SourceTurnEventId = selected; else target.SourceActionEventId = selected;
                    }));
                });
            }
            SetButtonTitle("btnAddConditionGroup", "逻辑组 / 动作数据");
            BindButton("btnAddConditionGroup", () => {
                var token = editor.Token;
                string parent = _selectedConditionNodeId;
                TacticalChoices(_root.GetChild("btnAddConditionGroup"), new[] { "All", "Any", "Not", "MovementArrived", "TurnAligned", "RootDistanceRange", "TargetFacing" }, kind => {
                    if (kind == "All" || kind == "Any" || kind == "Not")
                    {
                        var field = kind == "All" ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAllGroup
                            : kind == "Any" ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup : CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddNotGroup;
                        editor.Commit(token, "添加逻辑组", source => editor.ApplyCondition(source, timeline, eventId, field, -1,
                            new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: parent)));
                        return;
                    }
                    var ids = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events.Where(e => e.Operation.Kind == (kind == "MovementArrived" ? "Movement" : kind == "TurnAligned" ? "TurnToTarget" : "CharacterAction")).Select(e => e.EventId).ToArray();
                    TacticalChoices(_root.GetChild("btnAddConditionGroup"), ids, actionId => editor.Commit(token, "添加动作数据条件",
                        source => CharacterTestTacticalConditionEditing.AddBinding(source, timeline, eventId, actionId, kind, parent)));
                });
            });
        }

        private void BindTacticalConditionLeases(Fgui.GComponent parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (!child.visible) continue;
                if (child is Fgui.GTextInput input)
                {
                    input.onFocusIn.Remove(CaptureTacticalConditionLease);
                    input.onFocusIn.Add(CaptureTacticalConditionLease);
                }
                if (child.asCom != null) BindTacticalConditionLeases(child.asCom);
            }
            var list = _root.GetChild("conditionList");
            if (list != null) { list.onTouchBegin.RemoveCapture(CaptureTacticalConditionLease); list.onTouchBegin.AddCapture(CaptureTacticalConditionLease); }
        }
    }
}
