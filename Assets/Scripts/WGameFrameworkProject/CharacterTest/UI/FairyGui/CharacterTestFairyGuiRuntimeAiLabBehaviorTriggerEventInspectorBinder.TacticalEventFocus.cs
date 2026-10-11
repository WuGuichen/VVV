using System;
using System.Collections.Generic;
using System.Linq;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private static IEnumerable<(CharacterTestTacticalAuthoringTimeline timeline, CharacterTestTacticalAuthoringEvent ev)> VisibleEventItems(CharacterTestTacticalEditingSession editor)
        {
            foreach (var timeline in editor.Source.Timelines)
            {
                var shown = new HashSet<string>();
                foreach (var ev in timeline.Events)
                {
                    var block = CharacterTestTacticalBlockEdits.Owner(timeline, ev.EventId);
                    if (editor.Preview.FocusBlockId != null)
                    { if (block?.BlockId == editor.Preview.FocusBlockId) yield return (timeline, ev); continue; }
                    if (block == null) yield return (timeline, ev);
                    else if (shown.Add(block.BlockId)) yield return (timeline, BlockRow(block));
                }
                if (editor.Preview.FocusBlockId == null)
                    foreach (var block in timeline.EventBlocks) if (shown.Add(block.BlockId)) yield return (timeline, BlockRow(block));
            }
        }
        private static CharacterTestTacticalAuthoringEvent BlockRow(CharacterTestTacticalEventBlock block)
            => new CharacterTestTacticalAuthoringEvent { EventId = block.BlockId, Title = block.Title, Start = block.Start,
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "EventBlock" } };

        private float BindEventFocusHeader(CharacterTestTacticalEditingSession editor, float content)
        {
            if (editor.Preview.FocusBlockId == null) return 94;
            SetVisible("summary", true);
            var start = editor.Preview.FocusBlock?.Start;
            string origin = start?.Kind == "Anchor" ? TacticalChoiceLabel(start.TargetKind + ":" + start.SourceEventId)
                + " · " + TacticalChoiceLabel(start.Anchor) + " " + (start.OffsetFrames >= 0 ? "+" : "") + start.OffsetFrames + "f"
                : (start?.Frame ?? 0) + "f（封装坐标）";
            SetText("summary", editor.CardName + " → " + editor.Source.Name + " → " + editor.Preview.FocusBlock?.Title + "\n本段起点：" + origin);
            Place("summary", 12, 94, content, 66);
            var label = _root.GetChild("summary").asTextField;
            label.textFormat.size = 15; label.color = new UnityEngine.Color32(153, 213, 201, 255); label.alpha = 1; label.singleLine = false;
            TacticalButton("btnCloseInspector", "← 返回封装", 12, 162, content, true, editor.CloseEventFocus);
            if (editor.Preview.TimingPreview == null) return 204;
            SetVisible("timeLabel", true); SetText("timeLabel", "最小持续帧"); Place("timeLabel", 12, 204, 126, 32);
            TacticalInput("inputLocalFrame", editor.Preview.MinimumFrames.ToString(), "最小持续帧", 148, 204, content - 136, editor,
                editor.CanEdit && editor.Preview.FocusBlock?.Completion != "Condition",
                (token, text) => IntEdit(editor, text, value => { editor.SetMinimum(token, value); return true; }));
            SetVisible("durationFramesLabel", true); SetText("durationFramesLabel", "模拟结束帧"); Place("durationFramesLabel", 12, 244, 126, 32);
            TacticalInput("inputDurationFrames", editor.Preview.SimulationEnd.ToString(), "仅改变预览", 148, 244, content - 136, editor, !editor.Preview.IsFrozen,
                (token, text) => IntEdit(editor, text, value => { editor.SetSimulationEnd(value); return true; }));
            return 288;
        }

        private void BindEventBlockRow(EventSetRow row, CharacterTestTacticalEventBlock block, float width)
        {
            var binder = row.Binder; var editor = row.Editor;
            binder._root = row.Root;
            editor.ConditionsOpen = false;
            binder._model = new BehaviorRecipeTriggerEventInspectorViewModel { TacticalEditor = editor, TacticalToken = editor.Token };
            binder.ReconcileTacticalOwner(binder._model);
            foreach (var child in row.Root.GetChildren()) child.visible = false;
            float bottom = block.Start?.Kind == "Anchor" ? 268 : 228;
            binder.SetVisible("background", true); binder.Place("background", 0, 0, width, bottom);
            binder.TacticalInput("inputTitle", block.Title, "事件段名称", 8, 12, width - 16, editor, editor.CanEdit,
                (token, text) => editor.Commit(token, "修改事件段名称", source => CharacterTestTacticalAuthoringEdits.Timeline(source, row.Timeline)
                    .EventBlocks.Single(b => b.BlockId == block.BlockId).Title = text));
            binder.TacticalButton("btnOutcomeReturn", "编辑编译结果", 8, 56, width - 16, true, () => editor.OpenEventFocus(row.Timeline, block.BlockId));
            binder.BindTacticalReference(editor, row.Timeline, block.BlockId, block.Start, false, editor.CanEdit, 100, width - 24);
            editor.Preview.TryGetFrames(block.BlockId, out int start, out int end);
            bool conditionWait = block.Completion == "Condition";
            binder.SetVisible("summary", true); binder.SetText("summary", block.EventIds.Count + " 个事件 · " + start + "–" + end + " 帧\n"
                + (conditionWait ? "等待上限；窗口结束可提前完成，超时独立返回。上方按钮编辑条件。" : "拖动右边缘改时长；上方按钮编辑内部"));
            binder.Place("summary", 12, bottom - 76, width - 24, 70);
            var summary = row.Root.GetChild("summary").asTextField;
            summary.textFormat.size = 15; summary.color = new UnityEngine.Color32(153, 193, 211, 255); summary.singleLine = false; summary.alpha = 1;
            row.Root.SetSize(width, bottom); row.ExpandedHeight = bottom;
            if (block.Timing != null && editor.Composition != null)
            {
                int length = editor.BlockDuration(block);
                binder.SetVisible("durationFramesLabel", true); binder.SetText("durationFramesLabel", conditionWait ? "本次等待上限" : "本次制作长度");
                binder.Place("durationFramesLabel", 12, bottom, 130, 32);
                binder.TacticalInput("inputDurationFrames", length.ToString(), conditionWait ? "等待上限（帧）" : "制作长度", 150, bottom, width - 162, editor, editor.Composition.CanEdit,
                    (token, text) => IntEdit(editor, text, value => editor.SetBlockDuration(token, block.BlockId, value)));
                bottom += 40; row.Root.SetSize(width, bottom); row.ExpandedHeight = bottom;
                binder.Place("background", 0, 0, width, bottom);
            }
        }

        private void ShowTacticalOperationPicker(CharacterTestTacticalEditingSession editor, string timeline, string eventId,
            Fgui.GObject anchor, CharacterTestTacticalEditToken token)
        {
            var types = new[] { "CharacterAction", "Movement", "TurnToTarget", "WaitForCondition", "ReturnToPlanner" };
            if (editor.Source.HasTimingVersion) types = types.Concat(new[] { "CompleteBlock", "LoopRegion", "EnterTimeline", "ReturnToPlanner:Instant", "EnterTimeline:Instant" }).ToArray();
            if (eventId != null && editor.Source.HasTimingVersion)
            {
                bool instant = CharacterTestTacticalAuthoringEdits.Event(editor.Source, timeline, eventId).TimingKind == "Instant";
                types = types.Where(kind => (kind == "WaitForCondition" || kind.EndsWith(":Instant", StringComparison.Ordinal)) == instant).ToArray();
            }
            TacticalChoices(anchor, types, kind => {
                bool Apply(string slot, bool create = false)
                {
                    return eventId == null ? editor.AddEvent(token, timeline, kind, 0, actionSlot: slot, createSlot: create)
                        : editor.ReplaceEvent(token, timeline, eventId, kind, slot, create);
                }
                if (kind != "CharacterAction") { Apply(null); return; }
                ShowActionLibrary(editor, token, null, anchor, true, action => {
                    var existing = eventId == null ? null : editor.Preview.FindEvent(timeline, eventId);
                    if (existing?.Operation.Kind == "CharacterAction"
                        && editor.Source.Slots.Any(s => s.SlotId == existing.Operation.SlotId))
                    {
                        editor.Controller.SetTacticalPreviewSlot(editor.CardId, ActionBindingPath(editor, existing.Operation.SlotId), action);
                        return;
                    }
                    string slot = "action." + Guid.NewGuid().ToString("N");
                    if (Apply(slot, true)) editor.Controller.SetTacticalPreviewSlot(editor.CardId, ActionBindingPath(editor, slot), action);
                });
            });
        }
    }
}
