using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalBlockTiming
    {
        public int MinimumFrames { get; set; }
        public int DurationFrames { get; set; }
    }

    /// <summary>Preparation-only endpoint graph. Runtime continues to consume the existing execution table.</summary>
    internal sealed class CharacterTestTacticalTimingEvaluation
    {
        private readonly CharacterTestTacticalAuthoringSource _source;
        private readonly CharacterTestTacticalCardInputs _inputs;
        private readonly bool _preview;
        private readonly IReadOnlyDictionary<string, int> _lengths;
        private readonly Dictionary<string, CharacterTestTacticalAuthoringEvent> _events = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterTestTacticalEventBlock> _blocks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _values = new(StringComparer.Ordinal);
        private readonly List<string> _active = new();
        private readonly Dictionary<string, int> _slotStarts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _actualLengths = new(StringComparer.Ordinal);
        private int _phase;
        internal readonly List<string> Issues = new();

        internal CharacterTestTacticalTimingEvaluation(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalCardInputs inputs, bool preview = false, IReadOnlyDictionary<string, int> lengths = null)
        {
            _source = source; _inputs = inputs; _preview = preview; _lengths = lengths;
            foreach (var timeline in source.Timelines)
            {
                foreach (var ev in timeline.Events) _events.Add(ev.EventId, ev);
                foreach (var block in timeline.EventBlocks)
                {
                    _blocks.Add(block.BlockId, block);
                    foreach (string id in block.EventIds) _owners.Add(id, block.BlockId);
                }
            }
        }

        internal void Evaluate()
        {
            if (!_preview)
            {
                // Freeze slot locations using the authored boundary. Dependencies on earlier slots
                // already see their installed duration; references to E still see the authored length.
                _phase = 1;
                foreach (var ev in _events.Values.Where(IsTimedAction))
                    Try(() => _slotStarts[ev.EventId] = Position(ev.EventId, "Start"));
                foreach (var block in _blocks.Values.Where(b => b.Timing != null))
                    Try(() => {
                        int length = Length(block);
                        if (block.Completion != "Delay" && length < block.Timing.MinimumFrames) throw new ArgumentException(block.Title + "：制作长度小于最小持续帧。");
                        var authored = new List<(int start, int end)>();
                        var installed = new List<(int start, int end)>();
                        foreach (string id in block.EventIds)
                        {
                            if (!_events.TryGetValue(id, out var ev) || !IsTimedAction(ev)) continue;
                            int start = _slotStarts[id];
                            authored.Add((start, checked(start + (IsFunctionalAction(ev) ? ActionDuration(ev) : 60))));
                            installed.Add((start, checked(start + ActionDuration(ev))));
                        }
                        _actualLengths[block.BlockId] = checked(length + UnionLength(installed) - UnionLength(authored));
                    });
            }
            _phase = 2; _values.Clear(); _active.Clear();
            foreach (var block in _blocks.Values) { Try(() => Position(block.BlockId, "Start")); Try(() => Position(block.BlockId, "End")); }
            foreach (var ev in _events.Values)
                Try(() => {
                    int start = Position(ev.EventId, "Start"), end = Position(ev.EventId, "End");
                    if (start < 0 || end <= start) throw new ArgumentException(ev.Title + "：非法时间区间 " + start + "～" + end + "。");
                    if (_owners.TryGetValue(ev.EventId, out string owner))
                    {
                        int origin = Position(owner, "Start"), limit = Position(owner, "End");
                        int last = ev.TimingKind == "Instant" ? start : end;
                        if (start < origin || _blocks[owner].Timing != null && last > limit)
                            throw new ArgumentException(ev.Title + "：事件超出封装范围，当前 " + (start - origin) + "～" + (last - origin) + "，结束 " + (limit - origin) + " 帧。");
                    }
                });
        }

        private void Try(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException || e is KeyNotFoundException)
            { if (!Issues.Contains(e.Message)) Issues.Add(e.Message); }
        }
        internal bool TryFrames(string id, out int start, out int end)
        {
            bool a = _values.TryGetValue(id + ".Start", out start);
            bool b = _values.TryGetValue(id + ".End", out end);
            return a && b;
        }
        private bool IsTimedAction(CharacterTestTacticalAuthoringEvent ev)
            => ev.Operation.Kind == "CharacterAction" && _owners.TryGetValue(ev.EventId, out string owner) && _blocks[owner].Timing != null;
        private int Length(CharacterTestTacticalEventBlock block)
            => block.Completion == "Delay" ? checked((_inputs?.SimulationTickRate ?? CharacterTestDeterministicUnits.DefaultTickRate) * 2)
                : _lengths != null && _lengths.TryGetValue(block.BlockId, out int length) ? length : block.Timing.DurationFrames;
        private CharacterActionConfig Action(CharacterTestTacticalAuthoringEvent ev)
        {
            var slot = _source.Slots.FirstOrDefault(s => s.SlotId == ev.Operation.SlotId);
            if (slot == null || _inputs == null) throw new ArgumentException(ev.Title + "：动作锚点等待安装动作后解析。");
            return _inputs.ResolveSlot(_inputs.HasExplicitBindings && !slot.IsFunctional || slot.Resolver == "binding" ? slot.SlotId : slot.Resolver);
        }
        private int ActionDuration(CharacterTestTacticalAuthoringEvent ev) => Action(ev).DurationFrames.Value;
        private bool IsFunctionalAction(CharacterTestTacticalAuthoringEvent ev)
            => _source.Slots.Any(s => s.SlotId == ev.Operation.SlotId && s.IsFunctional);

        internal int Position(string id, string side)
        {
            string key = id + "." + side;
            if (_values.TryGetValue(key, out int cached)) return cached;
            if (_active.Contains(key)) throw new ArgumentException("时间关联成环：" + string.Join(" → ", _active) + " → " + key);
            _active.Add(key);
            try
            {
                int value;
                if (_blocks.TryGetValue(id, out var block))
                {
                    if (side == "Start") value = Resolve(block.Start, null);
                    else if (side == "Minimum") value = checked(Position(id, "Start") + (block.Completion == "Delay" ? Length(block) : block.Timing?.MinimumFrames ?? 0));
                    else if (block.Timing != null)
                        value = checked(Position(id, "Start") + (!_preview && _phase == 2 && _actualLengths.TryGetValue(id, out int length) ? length : Length(block)));
                    else value = block.EventIds.Aggregate(Position(id, "Start"), (end, member) => Math.Max(end, Position(member, "End")));
                }
                else
                {
                    if (!_events.TryGetValue(id ?? "", out var ev)) throw new ArgumentException("关联事件已不存在：" + id);
                    if (!_preview && _phase == 2 && side == "Start" && _slotStarts.TryGetValue(id, out int slotStart)) value = slotStart;
                    else if (side == "ActionStartupEnd")
                    {
                        var phases = Action(ev).Phases;
                        CharacterActionPhase? startup = null;
                        foreach (var phase in phases) if (phase.Kind == CharacterActionPhaseKind.Startup) { startup = phase; break; }
                        if (!startup.HasValue) throw new ArgumentException(ev.Title + "：动作没有前摇结束锚点。");
                        value = checked(Position(id, "Start") + startup.Value.EndFrame + 1);
                    }
                    else if (side == "ActionEnd" || side == "End" && ev.Operation.Kind == "CharacterAction")
                        value = checked(Position(id, "Start") + (_preview && IsTimedAction(ev) && !IsFunctionalAction(ev) ? 60 : ActionDuration(ev)));
                    else if (side == "End" && ev.TimingKind == "Instant") value = checked(Position(id, "Start") + 1);
                    else value = Resolve(side == "Start" ? ev.Start : ev.End, id);
                }
                _values[key] = value; return value;
            }
            finally { _active.RemoveAt(_active.Count - 1); }
        }
        internal int Resolve(CharacterTestTacticalFrameReference frame, string eventId)
        {
            if (frame == null) throw new ArgumentException("时间引用缺失。");
            int origin = eventId != null && _owners.TryGetValue(eventId, out string block) ? Position(block, "Start") : 0;
            if (frame.Kind == "Absolute") return checked((frame.Space == "Block" ? origin : 0) + frame.Frame);
            int target = Position(frame.SourceEventId, frame.TargetKind == "Builtin" && frame.Anchor == "SimulationEnd" ? "End" : frame.Anchor);
            return frame.Kind == "Ratio" ? checked(origin + (int)Math.Round((target - origin) * frame.Ratio, MidpointRounding.AwayFromZero))
                : checked(target + frame.OffsetFrames);
        }
        private static int UnionLength(List<(int start, int end)> intervals)
        {
            if (intervals.Count == 0) return 0;
            intervals.Sort((a, b) => a.start.CompareTo(b.start));
            int sum = 0, start = intervals[0].start, end = intervals[0].end;
            for (int i = 1; i < intervals.Count; i++)
            {
                var next = intervals[i];
                if (next.start > end) { sum = checked(sum + end - start); start = next.start; end = next.end; }
                else end = Math.Max(end, next.end);
            }
            return checked(sum + end - start);
        }
    }
}
