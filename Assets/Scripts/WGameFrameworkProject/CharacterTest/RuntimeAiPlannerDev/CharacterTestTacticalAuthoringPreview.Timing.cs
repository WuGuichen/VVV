using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalAuthoringPreview
    {
        internal CharacterTestTacticalAuthoringPreview WithBlockDuration(string blockId, int duration,
            IReadOnlyDictionary<string, int> instanceDurations)
        {
            return WithEdits(source => {
                foreach (var timeline in source.Timelines)
                    foreach (var block in timeline.EventBlocks)
                    {
                        if (block.Timing == null) continue;
                        if (instanceDurations != null && instanceDurations.TryGetValue(block.BlockId, out int value))
                            block.Timing.DurationFrames = value;
                        if (block.BlockId != blockId) continue;
                        if (duration < block.Timing.MinimumFrames)
                            throw new ArgumentException("制作长度不能小于 " + block.Timing.MinimumFrames + " 帧。");
                        block.Timing.DurationFrames = duration;
                    }
            });
        }

        internal CharacterTestTacticalTimingEvaluation TimingPreview { get; private set; }
        internal int SimulationEnd { get; private set; }
        internal int MinimumFrames => FocusBlock?.Timing?.MinimumFrames ?? 0;
        internal bool HasFramePositions => WholeCard != null ? WholeCard.Program != null : TimingPreview != null || Compilation != null;
        internal void EnableTimingSimulation(int end)
        {
            if (IsFrozen || FocusBlock?.Timing == null) return;
            SimulationEnd = Math.Max(MinimumFrames, end < 0 ? FocusBlock.Timing.DurationFrames : end);
            TimingPreview = new CharacterTestTacticalTimingEvaluation(Source, _preparedInputs, true,
                new Dictionary<string, int> { [FocusBlockId] = SimulationEnd });
            TimingPreview.Evaluate();
            if (TimingPreview.TryFrames(FocusBlockId, out int origin, out _)) DisplayOrigin = origin;
            Diagnostic = string.Join("\n", TimingPreview.Issues);
            DisplayPayload = BuildDisplayPayload();
        }
        internal static string LowLevelTitle(CharacterTestTacticalAuthoringEvent ev)
        {
            string kind = ev.Operation?.Kind switch {
                "Movement" or "ApproachOptimalRange" or "RetreatFromTarget" => "移动到目标",
                "TurnToTarget" => "转向目标", "CharacterAction" => "执行动作",
                "WaitForCondition" => "等待条件", "CompleteBlock" => "结束本段", "ReturnToPlanner" => "退出行为卡", "LoopRegion" => "循环区间", "EnterTimeline" => "进入时间轴", _ => ev.Operation?.Kind };
            string title = ev.Operation?.Kind == "CharacterAction" ? CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle(ev) : ev.Title;
            return string.IsNullOrEmpty(title) || title == kind ? kind : kind + " · " + title;
        }
        internal bool EventTimeInvalid(string id)
        {
            if (!TryGetFrames(id, out int start, out int end)) return true;
            if (TimingPreview == null) return false;
            int last = end;
            foreach (var timeline in Source.Timelines)
                foreach (var ev in timeline.Events) if (ev.EventId == id && ev.TimingKind == "Instant") last = start;
            return start < DisplayOrigin || end <= start || last > DisplayOrigin + SimulationEnd;
        }
    }
}
