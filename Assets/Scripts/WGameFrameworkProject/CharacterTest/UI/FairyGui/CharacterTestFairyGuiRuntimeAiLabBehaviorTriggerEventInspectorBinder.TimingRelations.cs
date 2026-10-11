using System;
using System.Globalization;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private void BindTimingRelation(CharacterTestTacticalEditingSession editor, string timeline, string id,
            CharacterTestTacticalFrameReference reference, bool end, bool enabled, float y, float width)
        {
            string input = end ? "inputDurationFrames" : "inputLocalFrame";
            string mode = end ? "btnDurationMode" : "btnFrameAbsolute";
            string targetButton = end ? "btnTargetTimeline" : "btnFrameActionEnd";
            string anchorButton = end ? "btnSignal" : "btnSignalReason";
            string label = end ? "durationFramesLabel" : "timeLabel";
            string kind = reference?.Kind ?? "Absolute";
            string kindText = kind == "Ratio" ? "比例 %" : kind == "Anchor" ? "固定间隔" : "手填帧";
            SetVisible(label, true); SetText(label, end ? "结束" : "开始 / 位置"); Place(label, 12, y, 96, 32);
            TacticalButton(mode, kindText, 110, y, 100, enabled, () => {
                var token = editor.Token;
                TacticalChoices(_root.GetChild(mode), new[] { "手填帧", "固定间隔", "比例" }, value =>
                    editor.SetTimeRelation(token, timeline, id, end, value == "手填帧" ? "Absolute" : value == "比例" ? "Ratio" : "Anchor"));
            });
            double number = kind == "Ratio" ? reference.Ratio * 100d : kind == "Anchor" ? reference.OffsetFrames : reference?.Frame ?? 0;
            TacticalInput(input, number.ToString("0.########", CultureInfo.InvariantCulture), kindText, 220, y, width - 208, editor, enabled,
                (token, text) => {
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)
                        || kind != "Ratio" && (value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue))
                    { editor.Feedback("请输入有效" + (kind == "Ratio" ? "比例。" : "整数帧。")); return false; }
                    return editor.Commit(token, "修改时间参数", source => {
                        var next = CharacterTestTacticalBlockEdits.Copy(CharacterTestTacticalBlockEdits.Reference(source, timeline, id, end));
                        if (kind == "Ratio") next.Ratio = value / 100d;
                        else if (kind == "Anchor") next.OffsetFrames = (int)value;
                        else next.Frame = (int)value;
                        CharacterTestTacticalAuthoringEdits.SetReference(source, timeline, id, end, next);
                    });
                });
            if (kind == "Absolute") { SetVisible(targetButton, false); SetVisible(anchorButton, false); return; }
            string target = reference.TargetKind == "Builtin" ? reference.Anchor == "Minimum" ? "最小持续帧" : "模拟结束帧"
                : editor.Preview.FindBlock(reference.SourceEventId)?.Title
                    ?? editor.Preview.FindEvent(timeline, reference.SourceEventId)?.Title ?? "缺失：" + reference.SourceEventId;
            TacticalButton(targetButton, "关联：" + target, 12, y + 40, width - 100, enabled, () => {
                var token = editor.Token;
                var choices = new[] { "Builtin:SimulationEnd", "Builtin:Minimum" }
                    .Concat(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).Events.Where(e => end || e.EventId != id).Select(e => "Event:" + e.EventId))
                    .Concat(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline).EventBlocks.Where(b => b.BlockId != editor.Preview.FocusBlockId).Select(b => "Block:" + b.BlockId)).ToArray();
                TacticalChoices(_root.GetChild(targetButton), choices, value => editor.SetTimeRelation(token, timeline, id, end, kind, value));
            });
            if (reference.TargetKind == "Builtin") { SetVisible(anchorButton, false); return; }
            string side = reference.Anchor == "Start" ? "开始" : reference.Anchor == "ActionStartupEnd" ? "前摇结束" : "结束";
            TacticalButton(anchorButton, side, width - 80, y + 40, 92, enabled, () => {
                var token = editor.Token;
                var ev = editor.Preview.FindEvent(timeline, reference.SourceEventId);
                string[] choices = ev?.TimingKind == "Instant" ? new[] { "Start" }
                    : ev?.Operation.Kind == "CharacterAction" ? new[] { "Start", "End", "ActionStartupEnd", "ActionEnd" } : new[] { "Start", "End" };
                TacticalChoices(_root.GetChild(anchorButton), choices, value => editor.SetTimeRelation(token, timeline, id, end, kind, anchor: value));
            });
            _root.GetChild(targetButton).onRightClick.Set(() => editor.OpenEventFocus(timeline, reference.SourceEventId));
        }
    }
}
