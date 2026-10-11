using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalAuthoringPreview
    {
        internal string FocusBlockId { get; private set; }
        internal int DisplayOrigin { get; private set; }
        internal CharacterTestTacticalEventBlock FocusBlock => Source.Timelines.SelectMany(t => t.EventBlocks).FirstOrDefault(b => b.BlockId == FocusBlockId);
        internal CharacterTestTacticalEventBlock FindBlock(string id) => Source.Timelines.SelectMany(t => t.EventBlocks).FirstOrDefault(b => b.BlockId == id);
        internal string DescribeTurnFacing(string eventId)
        {
            var ev = Source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == eventId);
            if (ev?.Operation.FacingSource == null) return "未配置";
            try
            {
                var facing = ev.Operation.ResolveFacing(_preparedInputs);
                return (facing.Center == 0 ? "" : facing.Center.ToString("0.###") + "° ") + "±" + facing.Tolerance.ToString("0.###") + "°";
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { return "待装配"; }
        }
        internal string DescribeMovementDistance(string eventId)
        {
            var ev = Source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == eventId);
            var movement = ev?.Operation?.Movement;
            if (movement == null || !movement.UsesDistance) return string.Empty;
            if (_preparedInputs == null) return "待装配";
            try
            {
                var parsed = movement.Compile(Source, _preparedInputs, ev.Operation.Speed, eventId);
                return parsed.IsOptimalRange ? parsed.Optimal.ToString("0.##") + " 米"
                    : movement.UsesBand ? parsed.Minimum.ToString("0.##") + "～" + parsed.Maximum.ToString("0.##") + " 米"
                    : parsed.Minimum.ToString("0.##") + " 米";
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { return "待解析"; }
        }
        internal string DescribeCompiledMovement(string eventId)
        {
            var program = Compilation?.Program;
            if (program == null) return "当前参数未能求值";
            for (int i = 0; i < program.Execution.Events.Length; i++)
            {
                var ev = program.Execution.Events[i];
                if (program.ExecutionDebugMap.Event(i) != eventId || ev.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget) continue;
                var move = program.Execution.Movements[ev.Parameter];
                return "当前解析：" + CharacterTestTacticalMovement.Label(move.Strategy) + " · " + move.Minimum.ToString("0.###")
                    + "～" + move.Maximum.ToString("0.###") + " 米 · 倍率 " + move.Speed.ToString("0.###");
            }
            return "";
        }
        internal void SetEventFocus(string blockId, int fallbackOrigin = 0)
        {
            FocusBlockId = blockId;
            DisplayOrigin = Compilation?.TryBlockFrames(blockId, out int start, out _) == true ? start : fallbackOrigin;
            DisplayPayload = BuildDisplayPayload();
        }
        private CharacterTestRuntimeAiLabBehaviorCardLogicPayload BuildExecutionDisplay()
        {
            var native = CharacterTestNativeExecutionView.Get(Compilation.Program).Payload;
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11, EntryTimelineId = native.EntryTimelineId };
            foreach (var nativeTimeline in native.Timelines)
            {
                var authored = Source.Timelines.First(t => t.TimelineId == nativeTimeline.TimelineId);
                var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = authored.TimelineId, Title = authored.Title, Order = authored.Order };
                foreach (var ev in nativeTimeline.Events)
                {
                    var source = authored.Events.First(e => e.EventId == ev.EventId);
                    timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = ev.EventId, TimelineId = ev.TimelineId,
                        Title = source.Title, StartFrame = ev.StartFrame, DurationFrames = ev.DurationFrames, TimingKind = ev.TimingKind,
                        ClockOrigin = ev.ClockOrigin, Priority = ev.Priority, MaxExecutionsPerPass = ev.MaxExecutionsPerPass,
                        CooldownFrames = ev.CooldownFrames, ConditionRoot = ev.ConditionRoot, Operation = ev.Operation });
                }
                payload.Timelines.Add(timeline);
            }
            ApplyBlockDisplay(payload);
            return payload;
        }
        private void ApplyBlockDisplay(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            foreach (var timeline in payload.Timelines.ToArray())
            {
                var authored = Source.Timelines.First(t => t.TimelineId == timeline.TimelineId);
                if (FocusBlockId != null)
                {
                    var block = authored.EventBlocks.FirstOrDefault(b => b.BlockId == FocusBlockId);
                    if (block == null) { payload.Timelines.Remove(timeline); continue; }
                    timeline.Title = block.Title;
                    timeline.Events.RemoveAll(e => !block.EventIds.Contains(e.EventId));
                    foreach (var ev in timeline.Events)
                    {
                        ev.StartFrame = Math.Max(0, ev.StartFrame - DisplayOrigin);
                        ev.Title = LowLevelTitle(authored.Events.First(e => e.EventId == ev.EventId));
                    }
                    continue;
                }
                foreach (var block in authored.EventBlocks)
                {
                    timeline.Events.RemoveAll(e => block.EventIds.Contains(e.EventId));
                    TryGetFrames(block.BlockId, out int start, out int end);
                    timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = block.BlockId,
                        TimelineId = timeline.TimelineId, Title = block.Title, StartFrame = start, DurationFrames = Math.Max(1, end - start),
                        TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                        Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation() });
                }
            }
        }
    }
}
