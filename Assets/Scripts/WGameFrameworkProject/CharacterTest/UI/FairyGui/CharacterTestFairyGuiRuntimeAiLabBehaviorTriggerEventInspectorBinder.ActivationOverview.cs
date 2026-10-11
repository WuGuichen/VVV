using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private bool _activationOverview;
        private float _activationOverviewWidth;
        internal float OverviewContentHeight { get; private set; }

        internal void BindActivationOverview(Fgui.GComponent root, BehaviorRecipeTriggerEventInspectorViewModel model, float width, float height)
        {
            _activationOverview = true;
            _activationOverviewWidth = width;
            root.SetSize(width, height);
            Bind(root, model);
            root.SetSize(width, height);
            foreach (var child in root.GetChildren())
            {
                if (child.name == "conditionList" || child.name == "btnAddCondition" || child.name == "btnAddConditionGroup"
                    || child.name == "ruleWhenLabel" || child.name == "summary") continue;
                // Keep active picker surfaces; they manage their own visibility and bounds.
                if (child == _conditionDropdown || child == _characterActionPicker) continue;
                child.visible = false;
            }
            SetText("ruleWhenLabel", "启用条件");
            Place("ruleWhenLabel", 8, 2, width - 116, 30);
            var heading = root.GetChild("ruleWhenLabel")?.asTextField;
            if (heading != null) { heading.align = Fgui.AlignType.Left; heading.color = new Color32(221, 232, 238, 255); heading.textFormat.size = 17; }
            var list = root.GetChild("conditionList")?.asList;
            float contentHeight = 0;
            if (list != null)
            {
                list.autoResizeItem = false; list.lineGap = 4;
                foreach (var item in list.GetChildren())
                {
                    if (item is not Fgui.GComponent row || item.data is not int index
                        || index < 0 || index >= model.Conditions.Length) continue;
                    ArrangeOverviewCondition(row, model.Conditions[index]);
                    contentHeight += row.height + list.lineGap;
                }
                if (contentHeight > 0) contentHeight -= list.lineGap;
            }
            float listHeight = Mathf.Min(contentHeight, Mathf.Max(60, height - 48));
            Place("conditionList", 4, 40, width - 8, listHeight);
            list?.EnsureBoundsCorrect();
            SetVisible("btnAddConditionGroup", false);
            var add = root.GetChild("btnAddCondition");
            TacticalButton("btnAddCondition", "＋ 添加", width - 96, 0, 88, model.CanEdit, () =>
                TacticalChoices(add, new[] { "判断条件", "逻辑组 / 动作数据" }, choice => {
                    if (choice == "判断条件") ShowConditionSubjectPicker(add);
                    else root.GetChild("btnAddConditionGroup").onClick.Call();
                }));
            bool empty = model.Conditions.Length == 0;
            SetVisible("summary", empty);
            if (empty)
            {
                SetText("summary", "无附加条件");
                Place("summary", 8, 42, width - 16, 28);
                if (root.GetChild("summary")?.asTextField is Fgui.GTextField hint)
                { hint.textFormat.size = 13; hint.color = new Color32(137, 164, 177, 255); }
            }
            OverviewContentHeight = 40 + (empty ? 28 : listHeight);
        }

        private void ArrangeOverviewCondition(Fgui.GComponent item, BehaviorRecipeTriggerConditionViewModel row)
        {
            if (!_activationOverview) return;
            float width = Mathf.Max(240, _activationOverviewWidth - 24);
            item.width = width;
            item.SetScale(1, 1);
            bool scope = row.NodeKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope;
            bool group = row.NodeKind is CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
                or CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope;
            float indent = Mathf.Min(24, row.Depth * 8);
            var fact = item.GetChild("btnFact")?.asButton;
            if (fact != null)
            {
                fact.SetXY(4 + indent, 4); fact.SetSize(width - 38 - indent, 28);
                fact.titleFontSize = 14;
                if (fact.GetTextField() is Fgui.GTextField label) label.align = Fgui.AlignType.Left;
            }
            var delete = item.GetChild("btnDelete");
            if (delete != null)
            {
                delete.SetXY(width - 29, 5); delete.SetSize(24, 24); delete.alpha = 0.55f;
                delete.tooltips = group ? "删除逻辑组" : "删除条件";
                delete.onRollOver.Set(() => delete.alpha = 1);
                delete.onRollOut.Set(() => delete.alpha = 0.55f);
            }
            item.height = scope ? 24 : group ? 36 : 72;
            if (scope && item.GetChild("subjectScopeTitle") is Fgui.GTextField caption)
            {
                caption.SetXY(8 + indent, 2); caption.SetSize(width - 16 - indent, 20);
                caption.textFormat.size = 13; caption.color = new Color32(119, 176, 175, 255);
                caption.verticalAlign = Fgui.VertAlignType.Middle;
            }
            bool range = !group && row.ValueKind == BehaviorRecipeTriggerConditionValueKind.Range
                && item.GetChild("rangeTrack")?.visible == true && item.GetChild("btnValueDropdown")?.visible != true;
            if (!range && row.ValueKind == BehaviorRecipeTriggerConditionValueKind.Range)
                foreach (string name in new[] { "rangeTrack", "rangeSelection", "rangeMinimumHandle", "rangeMaximumHandle",
                    "rangeMinimumLabel", "rangeMaximumLabel", "rangeCurrentSummary" })
                    if (item.GetChild(name) is Fgui.GObject extra) extra.visible = false;
            if (!group && !range)
            {
                var op = item.GetChild("btnOperator");
                bool signal = row.NodeKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal;
                if (op != null) { op.SetXY(4 + indent, 38); op.SetSize(signal ? width - 12 - indent : 72, 28); }
                float valueX = 4 + indent + (op?.visible == true && !signal ? 80 : 0);
                foreach (string name in new[] { "inputValue", "inputValueBackground", "btnValueDropdown" })
                {
                    var value = item.GetChild(name);
                    if (value != null)
                    {
                        value.SetXY(valueX, 38); value.SetSize(width - valueX - 8, 28);
                        if (value is Fgui.GButton choice)
                        {
                            choice.titleFontSize = 13;
                            if (row.ValueKind == BehaviorRecipeTriggerConditionValueKind.TargetState && choice.title.StartsWith("要求："))
                                choice.title = choice.title.Substring(3).Trim();
                            choice.tooltips = row.EffectiveSummary;
                        }
                    }
                }
                if (op?.visible != true && item.GetChild("inputValue")?.visible != true
                    && item.GetChild("btnValueDropdown")?.visible != true) item.height = 36;
            }
            if (range)
            {
                PlaceOverviewChild(item, "btnOperator", 4 + indent, 38, 72, 28);
                PlaceOverviewChild(item, "rangeCurrentSummary", 84 + indent, 38, width - 92 - indent, 28);
                PlaceOverviewChild(item, "rangeMinimumLabel", 4 + indent, 70, 64, 20);
                PlaceOverviewChild(item, "rangeMaximumLabel", width - 72, 70, 64, 20);
                PlaceOverviewChild(item, "inputValueBackground", 4 + indent, 92, 64, 28);
                PlaceOverviewChild(item, "inputValue", 7 + indent, 92, 58, 28);
                PlaceOverviewChild(item, "inputMaxValueBackground", width - 72, 92, 64, 28);
                PlaceOverviewChild(item, "inputMaxValue", width - 69, 92, 58, 28);
                PlaceOverviewChild(item, "rangeTrack", 80 + indent, 104, width - 164 - indent, 6);
                var track = item.GetChild("rangeTrack");
                foreach (string name in new[] { "rangeSelection", "rangeMinimumHandle", "rangeMaximumHandle" })
                {
                    var value = item.GetChild(name);
                    if (value != null) value.y = 107 - value.height / 2;
                }
                var domain = ResolveRangeEditorDomain(row);
                ApplyRangeVisual(track, item.GetChild("rangeSelection"), item.GetChild("rangeMinimumHandle"),
                    item.GetChild("rangeMaximumHandle"), row.Value, row.MaxValue, domain.Minimum, domain.Maximum);
                item.height = 126;
            }
            foreach (string name in new[] { "validation", "observation" })
            {
                var note = item.GetChild(name)?.asTextField;
                if (note?.visible != true) continue;
                note.SetXY(8 + indent, item.height); note.SetSize(width - 16 - indent, 24);
                note.textFormat.size = 12; note.singleLine = true; note.tooltips = note.text;
                item.height += 24;
            }
            foreach (string name in new[] { "background", "rowBackground" })
                if (item.GetChild(name) is Fgui.GObject background)
                {
                    background.visible = !scope; background.SetSize(width, item.height);
                    if (background is Fgui.GGraph surface)
                        surface.DrawRect(width, item.height, 1, new Color32(32, 58, 69, 255), new Color32(9, 30, 40, 255));
                }
        }

        private static void PlaceOverviewChild(Fgui.GComponent item, string name, float x, float y, float width, float height)
        {
            var child = item.GetChild(name);
            if (child == null) return;
            child.SetXY(x, y); child.SetSize(width, height);
        }
    }
}
