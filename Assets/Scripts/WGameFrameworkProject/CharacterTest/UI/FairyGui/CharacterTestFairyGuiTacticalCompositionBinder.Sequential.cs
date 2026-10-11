using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalCompositionBinder
    {
        private string _revealedCompositionNode;
        private void ShowGroupOperations()
        {
            var token = _session.Token;
            string[] selection = _session.Selection.ToArray();
            ShowChoices(new List<(string, Action)> {
                ("添加顺序组", () => Insert(token, "Sequence")),
                ("添加条件组", () => Insert(token, "If")),
                ("添加等待组", () => Insert(token, "Wait")),
                ("添加退出整卡", () => Insert(token, "Exit")),
                ("选中内容组成顺序组", () => _session.Edit(token, "成组", s => CharacterTestTacticalCompositionEdits.Group(s, selection, "Sequence"))),
                ("选中内容组成条件组", () => _session.Edit(token, "条件组", s => CharacterTestTacticalCompositionEdits.Group(s, selection, "If"))),
                ("解组", () => _session.Edit(token, "解组", s => CharacterTestTacticalCompositionEdits.Ungroup(s, token.NodeId))),
                ("向前移动", () => _session.Edit(token, "移动", s => CharacterTestTacticalCompositionEdits.Move(s, token.NodeId, -1))),
                ("向后移动", () => _session.Edit(token, "移动", s => CharacterTestTacticalCompositionEdits.Move(s, token.NodeId, 1))) }, anchor: Control("btnGroup"));
        }

        private void ShowConditionOperations()
        {
            var token = _session.Token;
            var options = new List<(string, Action)>();
            if (_session.SelectedNode?.Kind == "If" || _session.SelectedNode?.Kind == "Wait")
                options.Add(("编辑入口／等待条件", () => _session.Select(token.NodeId, CharacterTestTacticalEditTarget.NodeCondition)));
            if (_session.SelectedNode?.Kind == "Sequence" || _session.SelectedNode?.Kind == "Reference")
            {
                options.Add(("移动到位时结束此范围…", () => ShowMovementEnd(token, false)));
                options.Add(("移动目标失效时结束此范围…", () => ShowMovementEnd(token, true)));
                options.Add(("编辑局部结束条件", () => _session.Select(token.NodeId, CharacterTestTacticalEditTarget.NodeEnd)));
                options.Add(("动作提交失败时结束此范围", () => ShowCommitFailureEnd(token)));
            }
            options.Add(("添加条件组", () => _session.Edit(token, "条件组",
                s => CharacterTestTacticalCompositionEdits.Group(s, new[] { token.NodeId }, "If"))));
            ShowChoices(options);
        }

        private void BeginSharedEditing()
        {
            var token = _session.Token;
            _session.ConfirmShared(token);
        }

        internal void ArrangeSequential(float width, float height, Fgui.GComponent root)
        {
            if (_panel == null || _session == null) return;
            _panel.GetChild("background").SetSize(width, 134);
            _panel.GetChild("compositionTitle").visible = false;
            _panel.GetChild("sharedImpactText").visible = false;
            string[] rail = { "btnInsert", "btnGroup", "btnEntrance", "btnCompositionUndo", "btnCompositionRedo", "btnDelete" };
            for (int i = 0; i < rail.Length; i++)
            {
                var button = Control(rail[i]);
                if (button.parent != _panel) _panel.AddChild(button);
                button.visible = true;
                button.SetXY(4, 6 + i * 60); button.SetSize(60, 52);
                if (button.asButton != null) button.asButton.titleFontSize = 14;
            }
            foreach (string name in new[] { "btnActivation", "btnDescriptionDetails", "btnExpandShared", "btnCloseInternal" })
                if (Control(name) != null) Control(name).visible = false;
            var list = _panel.GetChild("compositionList")?.asList;
            list.layout = Fgui.ListLayoutType.SingleRow;
            list.columnGap = 0;
            list.SetXY(76, 16); list.SetSize(width - 84, 106);
            int depth = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var row = list.GetChildAt(i)?.asCom;
                if (row == null) continue;
                if (item.close) depth = Math.Max(0, depth - 1);
                bool group = item.node.Children != null;
                var top = row.GetChild("groupTop"); var bottom = row.GetChild("groupBottom"); var edge = row.GetChild("groupEdge");
                Color line = new Color32(75, 129, 144, 255);
                if (top != null) { top.visible = depth > 0 || group; top.SetXY(0, 8); top.SetSize(row.width, 1); top.asGraph?.DrawRect(row.width, 1, 0, Color.clear, line); }
                if (bottom != null) { bottom.visible = depth > 0 || group; bottom.SetXY(0, 83); bottom.SetSize(row.width, 1); bottom.asGraph?.DrawRect(row.width, 1, 0, Color.clear, line); }
                if (edge != null) { edge.visible = group; edge.SetXY(item.close ? row.width - 2 : 0, 8); edge.SetSize(1, 76); edge.asGraph?.DrawRect(1, 76, 0, Color.clear, line); }
                if (group && !item.close) depth++;
            }
            string reveal = _session.CardId + ":" + _session.SelectedNodeId;
            if (_revealedCompositionNode != reveal)
            {
                _revealedCompositionNode = reveal;
                for (int i = 0; i < _items.Count; i++)
                    if (!_items[i].close && _items[i].node.NodeId == _session.SelectedNodeId)
                    {
                        list.EnsureBoundsCorrect();
                        list.scrollPane?.ScrollToView(list.GetChildAt(i), false);
                        break;
                    }
            }
            MoveToRoot("btnCompositionSave", new Rect(root.width - 250, 10, 84, 40));
            MoveToRoot("btnCompositionCopy", new Rect(root.width - 158, 10, 98, 40));
            var inspector = root.GetChild("runtimeFrameWorkspace")?.asCom?.GetChild("behaviorRecipeInspector")?.asCom;
            var inspectorOrigin = inspector == null ? new Vector2(root.width - 432, 76)
                : root.GlobalToLocal(inspector.LocalToGlobal(Vector2.zero));
            float inspectorWidth = inspector?.width ?? 432;
            MoveToRoot("btnConfirmShared", new Rect(inspectorOrigin.x + inspectorWidth - 128, inspectorOrigin.y + 12, 116, 32));
            MoveToRoot("btnCopyShared", new Rect(inspectorOrigin.x + 12, inspectorOrigin.y + 54, (inspectorWidth - 32) * 0.5f, 32));
            if (Control("btnCopyShared") != null) Control("btnCopyShared").visible = !_session.EventFocusActive;
            void MoveToRoot(string name, Rect rect)
            {
                var control = Control(name);
                if (control == null) return;
                if (control.parent != root) root.AddChild(control);
                control.SetXY(rect.x, rect.y); control.SetSize(rect.width, rect.height); control.sortingOrder = rect.y < 60 ? 85 : 65;
            }
        }
    }
}
