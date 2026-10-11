using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Read-only projection of the exact execution table. Each timeline retains its local clock.</summary>
    internal sealed partial class CharacterTestTacticalWholeCardTimeline
    {
        internal CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardLogicPayload DisplayPayload { get; }
        internal string Diagnostic { get; }
        private readonly Dictionary<string, CharacterTestTacticalSourceLocation> _locations;
        private readonly CharacterTestTacticalCompositionEditingSession _session;
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterTestTacticalAuthoringEvent> _authors = new Dictionary<string, CharacterTestTacticalAuthoringEvent>(StringComparer.Ordinal);
        private readonly HashSet<string> _highlighted = new HashSet<string>(StringComparer.Ordinal);

        internal CharacterTestTacticalWholeCardTimeline(CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestTacticalDslSource document, IReadOnlyDictionary<string, CharacterTestTacticalSourceLocation> locations,
            CharacterTestTacticalCompositionEditingSession session = null, string diagnostic = null)
        {
            Program = program; _locations = locations?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) ?? new Dictionary<string, CharacterTestTacticalSourceLocation>();
            _session = session; Diagnostic = diagnostic ?? "整卡执行数据不可用。";
            DisplayPayload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11 };
            if (program?.Execution == null || program.ExecutionDebugMap == null) return;
            // Old frozen programs can still contain these wrappers; never synthesize them for a new program.
            var orders = new Dictionary<string, int>(StringComparer.Ordinal) { ["$entry"] = 0, ["$done"] = int.MaxValue };
            _names["$entry"] = "旧版入口中转"; _names["$done"] = "旧版结束中转";
            int order = 1;
            foreach (var node in CharacterTestTacticalCompositionEdits.Nodes(document.Root))
            {
                orders[node.NodeId] = order++;
                var definition = document.Definitions.FirstOrDefault(d => d.DefinitionId == node.DefinitionId);
                _names[node.NodeId] = orders[node.NodeId] + " · " + (definition?.Source.Name ?? (node.Kind == "Wait" ? "等待组" : node.Kind == "If" ? "条件组" : node.Kind == "Exit" ? "退出" : "顺序组"));
                if (session?.Selection.Contains(node.NodeId) == true)
                    foreach (var child in CharacterTestTacticalCompositionEdits.Nodes(node)) _highlighted.Add(child.NodeId);
            }
            if (locations != null)
                foreach (var pair in locations)
                {
                    var location = pair.Value;
                    if (string.IsNullOrEmpty(location.EventId)) continue;
                    var definition = document.Definitions.FirstOrDefault(d => d.DefinitionId == location.DefinitionId);
                    var authored = definition?.Source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == location.EventId);
                    if (authored != null) _authors[pair.Key] = authored;
                }
            var execution = program.Execution; var map = program.ExecutionDebugMap;
            DisplayPayload.EntryTimelineId = map.Timeline(execution.EntryTimeline);
            for (int i = 0; i < execution.Timelines.Length; i++)
            {
                string id = map.Timeline(i);
                var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline {
                    TimelineId = id, Title = Name(id) + " · 局部帧", Order = orders.TryGetValue(id, out int n) ? n : int.MaxValue - 1 };
                var table = execution.Timelines[i];
                for (int j = 0; j < table.EventCount; j++)
                {
                    int index = execution.TimelineEvents[table.EventStart + j]; var ev = execution.Events[index];
                    string key = map.Event(index);
                    var operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = ev.Kind };
                    if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                        operation.CharacterActionId = new CharacterActionId(execution.Actions[ev.Parameter].ActionId);
                    if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                        operation.TargetTimelineId = map.Timeline(execution.Flows[ev.Parameter].TargetTimeline);
                    timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent {
                        EventId = key, TimelineId = id, Title = EventTitle(key, ev.Kind)
                            + (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline ? " → " + Name(operation.TargetTimelineId) : string.Empty), StartFrame = ev.StartFrame,
                        DurationFrames = ev.DurationFrames, TimingKind = ev.Timing, Priority = ev.Priority,
                        Operation = operation });
                }
                DisplayPayload.Timelines.Add(timeline);
            }
            AggregateBlocks(document);
        }

        internal string Name(string timeline) => _names.TryGetValue(timeline, out var name) ? name : timeline;
        internal CharacterTestTacticalSourceLocation Location(string key) =>
            _locations != null && _locations.TryGetValue(key, out var value) ? value : null;
        internal bool Highlighted(string key) => Location(key) is CharacterTestTacticalSourceLocation location && _highlighted.Contains(location.NodeId);
        internal bool Selected(string key, string sourceEvent) => _session?.SelectedCompiledEventId == key || Location(key) is CharacterTestTacticalSourceLocation location
            && location.NodeId == _session?.SelectedNodeId && !string.IsNullOrEmpty(sourceEvent) && sourceEvent == location.EventId;
        internal string OperationKind(string key) => Location(key)?.Field == "eventBlock" ? "EventBlock" : _authors.TryGetValue(key, out var author) ? author.Operation?.Kind : null;
        internal void Select(string key)
        {
            var location = Location(key);
            if (_session == null || location == null) return;
            _session.SelectLocation(location);
            _session.SelectedCompiledEventId = key;
        }
        internal bool TryGetSelectedControl(out string title, out string description)
        {
            title = description = null;
            string key = _session?.SelectedCompiledEventId;
            if (string.IsNullOrEmpty(key) || !string.IsNullOrEmpty(Location(key)?.EventId)) return false;
            var item = DisplayPayload.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == key);
            if (item == null) return false;
            title = item.Title;
            description = "所属：" + Name(item.TimelineId) + "\n开始：" + item.StartFrame + "f（局部帧）"
                + (item.DurationFrames > 1 ? "\n结束：" + (item.StartFrame + item.DurationFrames) + "f" : "\n执行：单帧事件")
                + (string.IsNullOrEmpty(item.Operation.TargetTimelineId) ? "\n结果：退出行为卡" : "\n后续：" + Name(item.Operation.TargetTimelineId));
            return true;
        }
        private string EventTitle(string key, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind)
        {
            if (_authors.TryGetValue(key, out var author) && !string.IsNullOrWhiteSpace(author.Title) && author.Title != author.EventId)
                return author.Title;
            if (author?.Operation.Kind == "ApproachOptimalRange") return "接近目标";
            if (author?.Operation.Kind == "Movement") return author.Operation.Movement?.Describe() ?? "移动";
            if (author?.Operation.Kind == "RetreatFromTarget") return "后撤";
            var field = Location(key)?.Field;
            if (field == "endCondition") return "局部结束";
            if (field == "normalCompletion") return "完成";
            if (field == "sequenceContinuation") return "继续后续编排";
            if (field == "condition" && kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline) return "条件分支";
            return kind switch {
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction => "动作起手",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget => "移动",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition => "等待条件",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline => "跳转",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner => "显式退出行为卡",
                CharacterTestBehaviorExecutionOperations.TurnToTarget => "持续转向", _ => kind.ToString() };
        }
        internal string Describe(string key, string timelineId)
        {
            if (Program?.ExecutionDebugMap == null) return Diagnostic;
            var location = Location(key);
            var item = DisplayPayload.Timelines.FirstOrDefault(t => t.TimelineId == timelineId)?.Events.FirstOrDefault(e => e.EventId == key);
            string destination = string.IsNullOrEmpty(item?.Operation.TargetTimelineId) ? string.Empty : "\n跳转到：" + Name(item.Operation.TargetTimelineId);
            return Name(timelineId) + "\n底层时间轴：" + timelineId + "\n事件：" + key
                + "\n局部帧：" + item?.StartFrame + " → " + (item?.StartFrame + item?.DurationFrames)
                + "\n各时间轴从进入时的0帧起算；等待时长、条件分支与局部结束由运行时决定。"
                + destination + "\n来源：" + location?.DefinitionId + "/" + location?.EventId + " " + location?.Field;
        }
    }
}
