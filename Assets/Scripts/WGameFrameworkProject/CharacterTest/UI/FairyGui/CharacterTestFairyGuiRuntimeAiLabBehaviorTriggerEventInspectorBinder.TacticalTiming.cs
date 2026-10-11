using System.Globalization;
using System.Linq;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        // Reuse the published Inspector in a separate field scope so movement controls cannot
        // overwrite the start/end inputs or their edit leases.
        private float BindEventSetTiming(EventSetRow row, CharacterTestTacticalAuthoringEvent ev,
            BehaviorRecipeTriggerEventInspectorViewModel model, float y, float width)
        {
            if (row.TimingRoot == null)
            {
                row.TimingRoot = Fgui.UIPackage.CreateObjectFromURL(row.Root.resourceURL)?.asCom;
                if (row.TimingRoot == null) return y;
                row.TimingRoot.name = "eventTiming";
                row.Root.AddChild(row.TimingRoot);
                row.TimingBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(
                    _commandSink, _viewId, _popupService, null, null, _bindTooltip);
                row.TimingBinder._root = row.TimingRoot;
            }
            var binder = row.TimingBinder;
            var editor = row.Editor;
            binder.ReconcileTacticalOwner(model);
            binder._model = model;
            foreach (var child in row.TimingRoot.GetChildren()) child.visible = false;
            row.TimingRoot.visible = true;
            row.TimingRoot.SetXY(0, y);
            float content = width - 24;
            float position = 0;
            if (editor.Preview.FocusBlockId != null)
            {
                binder.BindCompactTimePoint(editor, ev, row.Timeline, false, position, width); position += 40;
                if (ev.End != null) { binder.BindCompactTimePoint(editor, ev, row.Timeline, true, position, width); position += 40; }
                bool invalid = editor.Preview.EventTimeInvalid(row.Event);
                if (invalid || ev.Operation.Kind == "CharacterAction")
                {
                    binder.SetVisible("summary", true);
                    string length = editor.Preview.TimingPreview != null ? "长度：60 帧占位，安装动作后解析"
                        : editor.Preview.TryGetFrames(row.Event, out int actionStart, out int actionEnd) ? "长度：" + (actionEnd - actionStart) + " 帧" : "动作长度待解析";
                    binder.SetText("summary", invalid ? editor.Preview.Diagnostic : length);
                    binder.Place("summary", 8, position, width - 16, invalid ? 48 : 28);
                    var compactSummary = row.TimingRoot.GetChild("summary").asTextField;
                    compactSummary.textFormat.size = 13; compactSummary.singleLine = !invalid; compactSummary.alpha = 1;
                    compactSummary.color = invalid ? new UnityEngine.Color32(255, 154, 162, 255) : new UnityEngine.Color32(151, 191, 201, 255);
                    compactSummary.tooltips = compactSummary.text;
                    position += invalid ? 52 : 32;
                }
                row.TimingRoot.SetSize(width, position);
                return y + position;
            }
            binder.BindTacticalReference(editor, row.Timeline, row.Event, ev.Start, false, editor.CanEdit, position, content);
            position += ev.Start?.Kind == "Anchor" || ev.Start?.Kind == "Ratio" ? 80 : 40;
            bool authoredEnd = ev.Operation.Kind != "CharacterAction" && ev.TimingKind != "Instant";
            if (authoredEnd)
            {
                binder.BindTacticalReference(editor, row.Timeline, row.Event, ev.End, true, editor.CanEdit, position, content);
                position += ev.End?.Kind == "Anchor" || ev.End?.Kind == "Ratio" ? 80 : 40;
            }
            bool resolved = editor.Preview.TryGetFrames(row.Event, out int start, out int end);
            bool invalidTime = editor.Preview.EventTimeInvalid(row.Event);
            start -= editor.Preview.DisplayOrigin; end -= editor.Preview.DisplayOrigin;
            string interval = resolved ? "当前区间：" + start + "–" + end + " 帧" : "时间待求值；请检查时间关联与卡片诊断";
            if (!authoredEnd) interval += ev.Operation.Kind == "CharacterAction" ? editor.Preview.TimingPreview != null ? " · 占位 60 帧" : " · 时长随动作" : " · 单帧事件";
            if (resolved && invalidTime) interval += " · 时间配置无效";
            binder.SetVisible("summary", true);
            binder.SetText("summary", interval);
            binder.Place("summary", 12, position, content, 44);
            var summary = row.TimingRoot.GetChild("summary").asTextField;
            summary.textFormat.size = 15;
            summary.color = invalidTime ? new UnityEngine.Color32(255, 154, 162, 255) : new UnityEngine.Color32(153, 193, 211, 255);
            summary.alpha = 1;
            summary.singleLine = false;
            summary.touchable = true;
            summary.tooltips = resolved && !invalidTime ? interval : editor.Preview.Diagnostic;
            position += 48;
            binder.SetVisible("priorityLabel", true);
            binder.SetText("priorityLabel", "优先级");
            binder.Place("priorityLabel", 12, position, 76, 32);
            binder.TacticalInput("inputPriority", ev.Priority.ToString(CultureInfo.InvariantCulture), "优先级",
                96, position, content - 84, editor, editor.CanEdit,
                (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改事件优先级",
                    source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).Priority = value)));
            position += 40;
            if (editor.Source.HasTimingVersion && ev.TimingKind == "Duration" && ev.Operation.Kind != "CharacterAction")
            {
                binder.SetVisible("cooldownHint", true); binder.SetText("cooldownHint", "次数（0 不限） / 冷却帧"); binder.Place("cooldownHint", 12, position, content, 26); position += 28;
                binder.TacticalInput("inputMaxExecutions", ev.MaxExecutionsPerPass.ToString(), "每轮次数", 12, position, (content - 8) / 2, editor, editor.CanEdit,
                    (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改每轮执行次数", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).MaxExecutionsPerPass = value)));
                binder.TacticalInput("inputCooldown", ev.CooldownFrames.ToString(), "冷却帧", 20 + (content - 8) / 2, position, (content - 8) / 2, editor, editor.CanEdit,
                    (token, text) => IntEdit(editor, text, value => editor.Commit(token, "修改冷却帧", source => CharacterTestTacticalAuthoringEdits.Event(source, row.Timeline, row.Event).CooldownFrames = value)));
                position += 40;
            }
            row.TimingRoot.SetSize(width, position);
            return y + position;
        }

        private static void SetTacticalReferenceKind(CharacterTestTacticalEditingSession editor,
            CharacterTestTacticalEditToken token, string timeline, string id, bool end, string kind)
        {
            var ownerTimeline = CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline);
            var ev = ownerTimeline.Events.FirstOrDefault(e => e.EventId == id);
            var container = ownerTimeline.EventBlocks.FirstOrDefault(b => b.BlockId == id);
            var previous = CharacterTestTacticalBlockEdits.Reference(editor.Source, timeline, id, end);
            var block = CharacterTestTacticalBlockEdits.Owner(CharacterTestTacticalAuthoringEdits.Timeline(editor.Source, timeline), id);
            string space = kind == "TimelineAbsolute" || block == null ? "Timeline" : "Block";
            if (previous?.Kind == kind && (kind != "Absolute" || previous.Space == space)) return;
            bool resolved = editor.Preview.TryGetFrames(id, out int start, out int finish);
            int frame = resolved ? end ? finish : start : previous?.Kind == "Absolute" ? previous.Frame : 0;
            CharacterTestTacticalFrameReference next;
            if (kind == "Absolute" || kind == "TimelineAbsolute")
                next = new CharacterTestTacticalFrameReference { Kind = "Absolute", Space = space,
                    Frame = frame - (space == "Block" ? editor.FrameOrigin(timeline, id) : 0) };
            else
            {
                var anchor = end ? ev : ownerTimeline.Events.FirstOrDefault(candidate => candidate.EventId != id
                    && (container == null || !container.EventIds.Contains(candidate.EventId)));
                if (anchor == null)
                {
                    editor.Feedback("开始时间需要关联另一个事件；请先添加事件，或继续使用绝对帧。");
                    return;
                }
                editor.Preview.TryGetFrames(anchor.EventId, out int anchorStart, out _);
                next = CharacterTestTacticalAuthoringTemplates.Anchor(anchor.EventId, "Start", frame - anchorStart);
            }
            editor.Commit(token, "修改时间来源", source =>
                CharacterTestTacticalAuthoringEdits.SetReference(source, timeline, id, end, next));
        }
    }
}
