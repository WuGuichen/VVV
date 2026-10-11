using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private void ArrangeSequentialInspector(BehaviorRecipeTriggerEventInspectorViewModel model,
            CharacterTestTacticalAuthoringEvent ev, bool timelineMode)
        {
            float width = _root.width, content = width - 32;
            SetVisible("headerTitle", false);
            _root.SetChildIndex(_root.GetChild("background"), 0);
            _root.SetChildIndex(_root.GetChild("bodyBackground"), 1);
            Place("background", 0, 0, width, _root.height);
            Place("bodyBackground", 0, 0, width, _root.height);
            if (model.TacticalEditor?.Preview?.WholeCard is CharacterTestTacticalWholeCardTimeline whole
                && whole.TryGetSelectedControl(out string controlTitle, out string controlDetail)
                && model.TacticalEditor.ConditionsOpen == false)
            {
                foreach (var child in _root.GetChildren()) child.visible = false;
                SetVisible("background", true); SetVisible("bodyBackground", true);
                SetVisible("headerTitle", true); SetText("headerTitle", controlTitle);
                Place("headerTitle", 16, 16, content, 46);
                SetVisible("summary", true); SetText("summary", controlDetail);
                Place("summary", 16, 80, content, 300);
                var text = _root.GetChild("summary")?.asTextField;
                if (text != null)
                {
                    text.singleLine = false; text.autoSize = global::FairyGUI.AutoSizeType.None;
                    text.verticalAlign = global::FairyGUI.VertAlignType.Top;
                    text.color = new Color32(174, 198, 213, 255); text.textFormat.size = 14; text.alpha = 1;
                }
                return;
            }
            Place("inputTitle", 16, 16, Mathf.Max(140, content - 130), 40);
            SetText("summary", model.TacticalEditor.Source.Name + (ev == null ? string.Empty : " / " + ev.EventId));
            var sourceLabel = _root.GetChild("summary")?.asTextField;
            if (sourceLabel != null)
            {
                sourceLabel.textFormat.size = 14; sourceLabel.color = new Color32(174, 198, 213, 255);
                sourceLabel.singleLine = true;
                sourceLabel.alpha = 1; sourceLabel.align = global::FairyGUI.AlignType.Left;
            }
            var nameField = _root.GetChild("inputTitle")?.asTextField;
            if (nameField != null) { nameField.textFormat.size = 21; nameField.textFormat.bold = true; }
            Place("summary", 16, 68, content, 28);
            SetVisible("validationText", !string.IsNullOrWhiteSpace(model.TacticalEditor.Message));
            SetText("validationText", model.TacticalEditor.Message);
            Place("validationText", 16, 720, content, Mathf.Max(64, _root.height - 740));
            SetVisible("btnGroupAnyNot", false); // The permanent card-condition list owns this operation.
            var composition = model.TacticalEditor.Controller.CompositionEditingSession;
            var node = composition?.SelectedNode;
            if (composition?.Target == CharacterTestTacticalEditTarget.Composition && node?.Kind != "Reference")
            {
                foreach (var child in _root.GetChildren()) child.visible = false;
                SetVisible("background", true); SetVisible("bodyBackground", true);
                SetVisible("headerTitle", true);
                string title = node?.Kind == "If" ? "条件组" : node?.Kind == "Wait" ? "等待" : node?.Kind == "Exit" ? "退出整卡" : "顺序组";
                SetText("headerTitle", title); Place("headerTitle", 16, 16, content, 40);
                var caption = _root.GetChild("headerTitle")?.asTextField;
                if (caption != null) { caption.color = Color.white; caption.textFormat.size = 21; }
                if (node?.Kind == "If" || node?.Kind == "Wait")
                    TacticalButton("btnGroupAll", "编辑入口／等待条件", 16, 92, content, composition.CanEdit,
                        () => composition.Select(node.NodeId, CharacterTestTacticalEditTarget.NodeCondition));
                if (node?.Kind == "Sequence")
                    TacticalButton("btnGroupAll", "编辑局部结束条件", 16, 92, content, composition.CanEdit,
                        () => composition.Select(node.NodeId, CharacterTestTacticalEditTarget.NodeEnd));
                return;
            }
            if (ev == null)
            {
                Place("btnGroupAll", 16, 140, content, 36);
                Place("btnOutcomeReturn", 16, 190, content, 36);
                if (timelineMode)
                {
                    Place("priorityLabel", 16, 244, 110, 32);
                    Place("inputPriority", 136, 244, content - 120, 32);
                    Place("btnCharacterActionParameter", 16, 292, content, 36);
                }
                return;
            }
            Place("btnCharacterActionParameter", 16, 132, content, 38);
            bool action = ev.Operation.Kind == "CharacterAction";
            if (action)
            {
                SetVisible("operationParameterLabel", true); SetText("operationParameterLabel", "解析值");
                Place("operationParameterLabel", 16, 182, 90, 34);
                var value = _root.GetChild("inputOperationParameter");
                value.visible = true; value.text = string.IsNullOrWhiteSpace(model.SelectedCharacterActionDisplayName)
                    ? "装配未解析" : model.SelectedCharacterActionDisplayName;
                if (value.asTextInput != null) { value.asTextInput.editable = false; value.touchable = false; value.enabled = true; value.grayed = false; }
                Place("inputOperationParameter", 112, 182, content - 96, 34);
            }
            else
            {
                Place("operationParameterLabel", 16, 182, 90, 34);
                Place("inputOperationParameter", 112, 182, content - 96, 34);
            }
            SetText("timeLabel", "开始");
            SetText("durationFramesLabel", "结束");
            MoveReference(false, 252);
            MoveReference(true, 340);
            if (action)
            {
                SetButtonTitle("btnDurationMode", "随动作时长");
                if (model.TacticalEditor.Preview.TryGetFrames(ev.EventId, out _, out int end))
                    SetText("inputDurationFrames", end + " f");
            }
            SetText("priorityLabel", "事件优先级");
            Place("priorityLabel", 16, 430, 110, 34);
            Place("inputPriority", 136, 430, content - 120, 34);
            Place("btnGroupAll", 16, 490, content, 36);
            Place("btnViewCharacterActionTimeline", 16, 540, content, 36);
            Place("btnOutcomeReturn", 16, 590, (content - 8) * 0.5f, 36);
            Place("btnDeleteEvent", 24 + (content - 8) * 0.5f, 630, (content - 8) * 0.5f, 36);
            Place("btnLocateTarget", 16, 680, content, 32);
            foreach (string fieldName in new[] { "inputLocalFrame", "inputDurationFrames", "inputPriority", "inputOperationParameter" })
            {
                var field = _root.GetChild(fieldName);
                var frame = _root.GetChild(fieldName + "Frame")?.asGraph;
                if (field == null || frame == null) continue;
                frame.visible = field.visible;
                frame.SetXY(field.x - 4, field.y - 2); frame.SetSize(field.width + 8, field.height + 4);
                frame.sortingOrder = 5;
                field.sortingOrder = 10;
                frame.DrawRect(frame.width, frame.height, 1, new Color32(47, 78, 96, 255), new Color32(11, 28, 39, 255));
                frame.touchable = false;
            }
            void MoveReference(bool end, float y)
            {
                Place(end ? "durationFramesLabel" : "timeLabel", 16, y, 72, 34);
                Place(end ? "btnDurationMode" : "btnFrameAbsolute", 96, y, 142, 34);
                Place(end ? "inputDurationFrames" : "inputLocalFrame", 246, y, Mathf.Max(80, width - 262), 34);
                Place(end ? "btnTargetTimeline" : "btnFrameActionEnd", 96, y + 40, Mathf.Max(100, content - 186), 32);
                Place(end ? "btnSignal" : "btnSignalReason", width - 114, y + 40, 98, 32);
            }
        }
    }
}
