using System;
using System.Linq;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private CharacterTestFairyGuiTacticalActionPicker _actionLibraryPicker;
        private void CloseActionLibraryPicker() => _actionLibraryPicker?.Close();

        private void ShowActionLibrary(CharacterTestTacticalEditingSession editor, CharacterTestTacticalEditToken token,
            string current, Fgui.GObject anchor, bool creation, Action<string> selected)
        {
            _actionLibraryPicker ??= new CharacterTestFairyGuiTacticalActionPicker(_viewId, _popupService);
            _actionLibraryPicker.Show(editor.Controller, current, anchor, !creation,
                () => !editor.Preview.IsFrozen && (creation ? editor.CanEdit : editor.Composition?.CanEdit ?? editor.CanEdit)
                    && editor.Controller.DraftWorkspace.CurrentRevision == token.WorkspaceRevision
                    && (editor.Composition == null || editor.Composition.AcceptsView(token)), selected);
        }
        private static string ActionBindingPath(CharacterTestTacticalEditingSession editor, string slot)
            => editor.Token.Target == CharacterTestTacticalEditTarget.LegacySource ? slot : editor.Token.NodeId + "/" + slot;

        private string ActionBindingName(CharacterTestTacticalEditingSession editor, CharacterTestTacticalAuthoringEvent ev)
        {
            var functional = editor.Source.Slots.FirstOrDefault(s => s.SlotId == ev.Operation.SlotId);
            if (functional?.IsFunctional == true) return functional.Resolver == "stepForward" ? "前向垫步（固定）" : "后向垫步（固定）";
            if (editor.Preview.IsFrozen)
            {
                int id = editor.Preview.Compilation?.EventActionId(ev.EventId) ?? 0;
                return BehaviorRecipeTriggerEventInspectorProjection.FindCharacterActionOption(_model.CharacterActionOptions,
                    new MxFramework.CharacterAction.CharacterActionId(id))?.DisplayName ?? "历史动作";
            }
            return editor.Controller == null ? "未选择动作"
                : editor.Controller.TacticalPreviewAction(editor.CardId, ActionBindingPath(editor, ev.Operation.SlotId)).label;
        }

        private void ShowActionBindingPicker(CharacterTestTacticalEditingSession editor, CharacterTestTacticalAuthoringEvent ev, Fgui.GObject anchor)
        {
            var controller = editor.Controller;
            var token = editor.Token;
            string path = ActionBindingPath(editor, ev.Operation.SlotId);
            string current = controller.TacticalPreviewAction(editor.CardId, path).id;
            ShowActionLibrary(editor, token, current, anchor, false, value => {
                if (value == "$default") controller.ResetTacticalPreviewAction(editor.CardId, path);
                else controller.SetTacticalPreviewSlot(editor.CardId, path, value);
                editor.Feedback(controller.StatusMessage);
            });
        }

        private void ShowActionAssociationPicker(CharacterTestTacticalEditingSession editor, string timeline,
            CharacterTestTacticalAuthoringEvent ev, Fgui.GObject anchor)
        {
            var token = editor.Token;
            var slots = editor.Source.Slots.Where(s => !s.IsFunctional
                && CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionUseLabel(editor.Source, s.SlotId) != "未关联事件")
                .Select(s => s.SlotId).Concat(new[] { "$independent" }).ToArray();
            TacticalChoices(anchor, slots, selected => {
                bool independent = selected == "$independent";
                string slot = independent ? "action." + Guid.NewGuid().ToString("N") : selected;
                string action = editor.Controller.TacticalPreviewAction(editor.CardId, ActionBindingPath(editor, ev.Operation.SlotId)).id;
                if (editor.Commit(token, "修改动作配置关联", source => {
                    if (independent) source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = slot, Resolver = "binding" });
                    CharacterTestTacticalAuthoringEdits.Event(source, timeline, ev.EventId).Operation.SlotId = slot;
                }) && independent && !string.IsNullOrEmpty(action))
                    editor.Controller.SetTacticalPreviewSlot(editor.CardId, ActionBindingPath(editor, slot), action);
            }, display: value => value == "$independent" ? "改为独立配置"
                : (value == ev.Operation.SlotId ? "✓ " : "沿用：")
                    + CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionUseLabel(editor.Source, value));
        }
    }
}
