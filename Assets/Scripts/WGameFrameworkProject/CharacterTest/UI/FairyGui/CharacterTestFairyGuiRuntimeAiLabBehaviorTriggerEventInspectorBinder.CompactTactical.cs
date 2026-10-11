using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private static CharacterTestTacticalAuthoringEvent CompactSelectedEvent(BehaviorRecipeTriggerEventInspectorViewModel model)
        {
            if (model.InspectorMode != BehaviorRecipeInspectorMode.Event) return null;
            var preview = model.TacticalEditor.Preview;
            return preview.FocusBlock?.EventIds.Contains(model.EventId) == true ? preview.FindEvent(model.TimelineId, model.EventId) : null;
        }

        private static string CompactOperationName(string kind) => kind switch {
            "Movement" or "ApproachOptimalRange" or "RetreatFromTarget" => "移动到目标",
            "CharacterAction" => "执行动作", "TurnToTarget" => "转向目标", "WaitForCondition" => "等待条件",
            "ReturnToPlanner" => "退出行为卡", "CompleteBlock" => "结束当前意图段", "EnterTimeline" => "进入时间轴", "LoopRegion" => "循环区间", _ => kind };

        private float BindCompactFocusHeader(CharacterTestTacticalEditingSession editor, CharacterTestTacticalAuthoringEvent ev, float width)
        {
            var block = editor.Preview.FocusBlock;
            TacticalButton("btnCloseInspector", "←", 12, 12, 40, true, editor.CloseEventFocus);
            _root.GetChild("btnCloseInspector").tooltips = "返回封装";
            TacticalInput("inputTitle", ev == null ? block?.Title ?? "封装" : CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(ev),
                ev == null ? "封装项名称" : "事件名称", 60, 12, Math.Max(24, width - 240), editor, editor.CanEdit,
                (token, title) => editor.Commit(token, "修改名称", source => {
                    var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, editor.Composition.FocusTimelineId);
                    if (ev == null) timeline.EventBlocks.Single(b => b.BlockId == block.BlockId).Title = title;
                    else timeline.Events.Single(e => e.EventId == ev.EventId).Title = title;
                }));
            _root.GetChild("inputTitle").tooltips = editor.CardName + " → " + editor.Source.Name + " → " + block?.Title + (ev == null ? "" : " → " + ev.Title);
            TacticalButton("btnOutcomeReturn", "…", width - 176, 12, 40, true, () => ShowCompactEventMenu(editor, ev));
            _root.GetChild("btnOutcomeReturn").tooltips = "添加、另存与删除";
            SetVisible("summary", true); SetText("summary", editor.CanEdit ? "共享定义" : "只读 · 共享定义");
            Place("summary", 12, 52, width - 24, 24);
            var info = _root.GetChild("summary").asTextField;
            info.textFormat.size = 13; info.color = new Color32(142, 169, 181, 255); info.singleLine = true; info.alpha = 1;
            info.tooltips = editor.Composition?.ImpactText;
            if (ev != null) return 84;
            if (editor.Preview.TimingPreview != null)
            {
                SetVisible("timeLabel", true); SetText("timeLabel", "最小持续帧"); Place("timeLabel", 12, 94, 126, 32);
                TacticalInput("inputLocalFrame", editor.Preview.MinimumFrames.ToString(), "最小持续帧", 148, 94, width - 160, editor, editor.CanEdit,
                    (token, text) => IntEdit(editor, text, value => { editor.SetMinimum(token, value); return true; }));
                SetVisible("durationFramesLabel", true); SetText("durationFramesLabel", "模拟结束帧"); Place("durationFramesLabel", 12, 134, 126, 32);
                TacticalInput("inputDurationFrames", editor.Preview.SimulationEnd.ToString(), "仅改变预览", 148, 134, width - 160, editor, !editor.Preview.IsFrozen,
                    (token, text) => IntEdit(editor, text, value => { editor.SetSimulationEnd(value); return true; }));
            }
            TacticalButton("btnGroupAll", "＋ 添加事件", 12, 184, width - 24, editor.CanEdit,
                () => ShowTacticalOperationPicker(editor, editor.Composition.FocusTimelineId, null, _root.GetChild("btnGroupAll"), editor.Token));
            return 228;
        }

        private void ShowCompactEventMenu(CharacterTestTacticalEditingSession editor, CharacterTestTacticalAuthoringEvent ev)
        {
            var anchor = _root.GetChild("btnOutcomeReturn");
            var options = new List<string>();
            if (editor.CanEdit) options.Add("添加事件…");
            if (editor.Composition?.CanEdit == true) options.Add("另存独立封装");
            if (editor.CanEdit && ev != null) options.Add("删除当前事件…");
            if (options.Count == 0) { editor.Feedback("历史内容只读。"); return; }
            var token = editor.Token;
            TacticalChoices(anchor, options.ToArray(), choice => {
                if (choice == "添加事件…") ShowTacticalOperationPicker(editor, editor.Composition.FocusTimelineId, null, anchor, token);
                else if (choice == "另存独立封装") editor.Composition.DuplicateDefinition(token);
                else
                {
                    string[] impacts = CharacterTestTacticalAuthoringEdits.DeleteImpact(editor.Source, new[] { ev.EventId });
                    const string confirm = "确认删除，保留失效引用";
                    TacticalChoices(anchor, impacts.Select(i => "影响：" + i).Concat(new[] { "取消", confirm }).ToArray(), answer => {
                        if (answer == confirm) editor.Commit(token, "删除事件", source =>
                            CharacterTestTacticalAuthoringEdits.Delete(source, editor.Composition.FocusTimelineId, new[] { ev.EventId }, true));
                    });
                }
            });
        }

        private void BindCompactTimePoint(CharacterTestTacticalEditingSession editor, CharacterTestTacticalAuthoringEvent ev,
            string timeline, bool end, float y, float width)
        {
            var reference = end ? ev.End : ev.Start;
            string kind = reference?.Kind ?? "Absolute";
            string label = end ? "durationFramesLabel" : "timeLabel";
            string sourceButton = end ? "btnDurationMode" : "btnFrameAbsolute";
            string input = end ? "inputDurationFrames" : "inputLocalFrame";
            string resultName = end ? "cooldownHint" : "priorityLabel";
            SetVisible(label, true); SetText(label, end ? "结束" : ev.Operation.Kind == "CharacterAction" ? "位置" : ev.TimingKind == "Instant" ? "触发" : "开始");
            Place(label, 8, y, 48, 32);
            string target = "手填帧";
            if (kind != "Absolute")
            {
                if (reference.TargetKind == "Builtin") target = reference.Anchor == "Minimum" ? "最小持续帧" : "模拟结束帧";
                else
                {
                    target = editor.Preview.FindBlock(reference.SourceEventId)?.Title
                        ?? editor.Preview.FindEvent(timeline, reference.SourceEventId)?.Title ?? "缺失来源";
                    target += "·" + (reference.Anchor == "Start" ? "开始" : reference.Anchor == "ActionStartupEnd" ? "前摇结束" : "结束");
                }
                if (kind == "Ratio") target += " ×";
            }
            float targetWidth = Math.Max(80, width - 214);
            TacticalButton(sourceButton, target + " ▾", 60, y, targetWidth, editor.CanEdit, () => {
                var token = editor.Token;
                var choices = new[] { "手填帧", "固定间隔", "比例", "Builtin:SimulationEnd", "Builtin:Minimum" }
                    .Concat(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events.Where(e => end || e.EventId != ev.EventId).Select(e => "Event:" + e.EventId))
                    .Concat(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).EventBlocks.Where(b => b.BlockId != editor.Preview.FocusBlockId).Select(b => "Block:" + b.BlockId)).ToArray();
                TacticalChoices(_root.GetChild(sourceButton), choices, choice => {
                    if (choice == "手填帧" || choice == "固定间隔" || choice == "比例")
                    { editor.SetTimeRelation(token, timeline, ev.EventId, end, choice == "手填帧" ? "Absolute" : choice == "比例" ? "Ratio" : "Anchor"); return; }
                    string mode = kind == "Ratio" ? "Ratio" : "Anchor";
                    if (choice.StartsWith("Builtin:", StringComparison.Ordinal))
                    { editor.SetTimeRelation(token, timeline, ev.EventId, end, mode, choice); return; }
                    var sourceEvent = choice.StartsWith("Event:", StringComparison.Ordinal) ? editor.Preview.FindEvent(timeline, choice.Substring(6)) : null;
                    string[] anchors = sourceEvent?.TimingKind == "Instant" ? new[] { "Start" }
                        : sourceEvent?.Operation.Kind == "CharacterAction" ? new[] { "Start", "End", "ActionStartupEnd", "ActionEnd" } : new[] { "Start", "End" };
                    TacticalChoices(_root.GetChild(sourceButton), anchors, side => editor.SetTimeRelation(token, timeline, ev.EventId, end, mode, choice, side));
                });
            });
            _root.GetChild(sourceButton).tooltips = target + "；点击修改定位方式、来源或锚点";
            double number = kind == "Ratio" ? reference.Ratio * 100 : kind == "Anchor" ? reference.OffsetFrames : reference?.Frame ?? 0;
            string textValue = (kind == "Anchor" && number >= 0 ? "+" : "") + number.ToString("0.########", CultureInfo.InvariantCulture);
            TacticalInput(input, textValue, kind == "Ratio" ? "比例（%）" : "帧数", 68 + targetWidth, y, 60, editor, editor.CanEdit, (token, text) => {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)
                    || kind != "Ratio" && (value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue))
                { editor.Feedback("请输入有效" + (kind == "Ratio" ? "比例。" : "整数帧。")); return false; }
                return editor.Commit(token, "修改时间参数", source => {
                    var next = CharacterTestTacticalBlockEdits.Copy(CharacterTestTacticalBlockEdits.Reference(source, timeline, ev.EventId, end));
                    if (kind == "Ratio") next.Ratio = value / 100d;
                    else if (kind == "Anchor") next.OffsetFrames = (int)value;
                    else next.Frame = (int)value;
                    CharacterTestTacticalAuthoringEdits.SetReference(source, timeline, ev.EventId, end, next);
                });
            });
            bool resolved = editor.Preview.TryGetFrames(ev.EventId, out int start, out int finish);
            string result = kind == "Absolute" ? "帧" : (kind == "Ratio" ? "% " : "") + "→ " + (resolved ? ((end ? finish : start) - editor.Preview.DisplayOrigin) + "帧" : "待解析");
            SetVisible(resultName, true); SetText(resultName, result); Place(resultName, 134 + targetWidth, y, width - targetWidth - 142, 32);
            var field = _root.GetChild(resultName).asTextField; field.textFormat.size = 13; field.singleLine = true;
            field.color = editor.Preview.EventTimeInvalid(ev.EventId) ? new Color32(255, 154, 162, 255) : new Color32(151, 191, 201, 255);
            field.tooltips = editor.Preview.EventTimeInvalid(ev.EventId) ? editor.Preview.Diagnostic : result;
        }

        private float BindCompactAdvanced(EventSetRow row, CharacterTestTacticalAuthoringEvent ev,
            BehaviorRecipeTriggerEventInspectorViewModel model, float y, float width)
        {
            if (row.AdvancedRoot == null)
            {
                row.AdvancedRoot = Fgui.UIPackage.CreateObjectFromURL(row.Root.resourceURL)?.asCom;
                if (row.AdvancedRoot == null) return y;
                row.AdvancedRoot.name = "eventAdvanced"; row.Root.AddChild(row.AdvancedRoot);
                row.AdvancedBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(_commandSink, _viewId, _popupService, null, null, _bindTooltip);
                row.AdvancedBinder._root = row.AdvancedRoot;
            }
            var b = row.AdvancedBinder; var editor = row.Editor;
            b.ReconcileTacticalOwner(model); b._model = model;
            foreach (var child in row.AdvancedRoot.GetChildren()) child.visible = false;
            row.AdvancedRoot.visible = true; row.AdvancedRoot.SetXY(0, y);
            float position = 0;
            void Integer(string input, string label, string title, int value, Action<CharacterTestTacticalAuthoringEvent, int> assign)
            {
                b.SetVisible(label, true); b.SetText(label, title); b.Place(label, 8, position, 130, 32);
                b.TacticalInput(input, value.ToString(CultureInfo.InvariantCulture), title, 146, position, width - 154, editor, editor.CanEdit,
                    (token, text) => IntEdit(editor, text, number => editor.Commit(token, "修改" + title, source => assign(CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event), number))));
                position += 40;
            }
            Integer("inputPriority", "priorityLabel", "优先级", ev.Priority, (item, value) => item.Priority = value);
            if (ev.TimingKind == "Duration" && ev.Operation.Kind != "CharacterAction")
            {
                Integer("inputMaxExecutions", "repeatLabel", "次数（0 不限）", ev.MaxExecutionsPerPass, (item, value) => item.MaxExecutionsPerPass = value);
                Integer("inputCooldown", "cooldownHint", "冷却帧", ev.CooldownFrames, (item, value) => item.CooldownFrames = value);
            }
            if (ev.Operation.Kind == "CharacterAction")
            {
                b.TacticalButton("btnCharacterActionParameter", "动作配置关联…", 8, position, width - 16, editor.CanEdit,
                    () => b.ShowActionAssociationPicker(editor, row.Timeline, ev, row.AdvancedRoot.GetChild("btnCharacterActionParameter")));
                position += 40;
                b.TacticalButton("btnDeleteEvent", "清理未使用的动作引用", 8, position, width - 16, editor.CanEdit,
                    () => editor.Commit(editor.Token, "清理未引用动作槽", CharacterTestTacticalSlotEditing.RemoveUnused));
                position += 40;
            }
            row.AdvancedRoot.SetSize(width, position); return y + position;
        }
    }
}
