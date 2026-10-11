using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalBlockEdits
    {
        internal static CharacterTestTacticalFrameReference Copy(CharacterTestTacticalFrameReference value) => value == null ? null :
            new CharacterTestTacticalFrameReference { Kind = value.Kind, Frame = value.Frame, Space = value.Space,
                TargetKind = value.TargetKind, SourceEventId = value.SourceEventId, Anchor = value.Anchor, OffsetFrames = value.OffsetFrames, Ratio = value.Ratio };
        internal static CharacterTestTacticalEventBlock Owner(CharacterTestTacticalAuthoringTimeline timeline, string eventId)
            => timeline.EventBlocks.FirstOrDefault(block => block.EventIds.Contains(eventId));
        internal static CharacterTestTacticalFrameReference Local(int frame)
            => new CharacterTestTacticalFrameReference { Kind = "Absolute", Frame = frame, Space = "Block" };
        internal static CharacterTestTacticalFrameReference BlockEnd(string blockId)
            => new CharacterTestTacticalFrameReference { Kind = "Anchor", TargetKind = "Block", SourceEventId = blockId, Anchor = "End" };
        internal static CharacterTestTacticalFrameReference Reference(CharacterTestTacticalAuthoringSource source, string timelineId, string id, bool end)
        {
            var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, timelineId);
            var block = timeline.EventBlocks.FirstOrDefault(b => b.BlockId == id);
            if (block != null) return end ? null : block.Start;
            var ev = CharacterTestTacticalAuthoringEdits.Event(source, timelineId, id);
            return end ? ev.End : ev.Start;
        }

        internal static CharacterTestTacticalEventBlock Ensure(CharacterTestTacticalAuthoringSource source, string timelineId,
            string eventId, string blockId)
        {
            var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, timelineId);
            var existing = timeline.EventBlocks.FirstOrDefault(b => b.BlockId == blockId) ?? Owner(timeline, eventId);
            if (existing != null) return existing;
            var ev = CharacterTestTacticalAuthoringEdits.Event(source, timelineId, eventId);
            var origin = Copy(ev.Start);
            if (origin.Space != "Timeline") throw new ArgumentException("Cannot nest event blocks.");
            var block = new CharacterTestTacticalEventBlock { BlockId = blockId,
                Title = string.IsNullOrWhiteSpace(ev.Title) ? ev.EventId : ev.Title, Start = origin,
                EventIds = new List<string> { ev.EventId } };
            // Preserve global anchors. Convert an absolute end only when the origin is also absolute.
            if (ev.End?.Kind == "Absolute" && origin.Kind == "Absolute")
                ev.End = Local(checked(ev.End.Frame - origin.Frame));
            ev.Start = Local(0);
            timeline.EventBlocks.Add(block);
            if (!source.HasTimingVersion) { source.Schema = CharacterTestTacticalAuthoringSource.BlockSchema; source.Version = 3; }
            return block;
        }

        internal static string Add(CharacterTestTacticalAuthoringSource source, string timelineId, string blockId,
            string kind, int frame, string afterEvent = null, string actionSlot = null)
        {
            var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, timelineId);
            var block = timeline.EventBlocks.Single(b => b.BlockId == blockId);
            CharacterTestTacticalAuthoringEdits.Add(source, timelineId, kind);
            var ev = timeline.Events[timeline.Events.Count - 1];
            ev.Start = string.IsNullOrEmpty(afterEvent) ? Local(Math.Max(0, frame))
                : CharacterTestTacticalAuthoringTemplates.Anchor(afterEvent, "End");
            if (ev.End != null) ev.End = CharacterTestTacticalAuthoringTemplates.Anchor(ev.EventId, "Start", kind == "Movement" ? 120 : 1);
            if (kind == "CharacterAction") ev.Operation.SlotId = actionSlot ?? string.Empty;
            block.EventIds.Add(ev.EventId);
            return ev.EventId;
        }

        internal static void Replace(CharacterTestTacticalAuthoringSource source, string timelineId, string eventId,
            string kind, string actionSlot, int previousDuration)
        {
            var ev = CharacterTestTacticalAuthoringEdits.Event(source, timelineId, eventId);
            if (source.HasTimingVersion && (ev.TimingKind == "Instant") != (kind == "WaitForCondition" || kind.EndsWith(":Instant", StringComparison.Ordinal)))
                throw new ArgumentException("单帧和持续事件不能直接互换，请删除后重新创建；原引用会保留为缺失。");
            if (ev.Operation.Kind == kind && (kind != "CharacterAction" || ev.Operation.SlotId == actionSlot)) return;
            if (kind == "CharacterAction" && !source.Slots.Any(s => s.SlotId == actionSlot))
                throw new ArgumentException("请先选择动作槽。");
            var old = ev.Operation;
            // Reuse the supported-operation defaults and validation instead of carrying unrelated fields.
            CharacterTestTacticalAuthoringEdits.Add(source, timelineId, kind);
            var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, timelineId);
            var defaults = timeline.Events[timeline.Events.Count - 1]; timeline.Events.RemoveAt(timeline.Events.Count - 1);
            ev.Operation = defaults.Operation;
            if (kind == "CharacterAction") ev.Operation.SlotId = actionSlot;
            if (kind == "ApproachOptimalRange") ev.Operation.SourceActionEventId = timeline.Events.FirstOrDefault(e => e.EventId != eventId && e.Operation.Kind == "CharacterAction")?.EventId ?? string.Empty;
            if (kind == "Movement" || kind == "ApproachOptimalRange" || kind == "TurnToTarget" || kind == "RetreatFromTarget") ev.Operation.Speed = old.Speed;
            ev.TimingKind = defaults.TimingKind;
            ev.End = kind == "CharacterAction" || defaults.TimingKind == "Instant" ? null
                : ev.End ?? CharacterTestTacticalAuthoringTemplates.Anchor(eventId, "Start", Math.Max(1, previousDuration));
            if (ev.TimingKind == "Instant") { ev.MaxExecutionsPerPass = 1; ev.CooldownFrames = 0; }
        }

        internal static void Move(CharacterTestTacticalAuthoringSource source, string timelineId, string[] ids, int delta)
        {
            var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, timelineId);
            var selected = new HashSet<string>(ids ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (selected.Count == 0 || selected.Count != ids.Length) throw new ArgumentException("移动选择无效。");
            bool Affected(string id, string side, string kind, HashSet<string> visiting)
            {
                string key = kind + ":" + id + ":" + side;
                if (!visiting.Add(key)) throw new ArgumentException("时间关联成环。");
                try
                {
                    if (kind == "Event" && selected.Contains(id)) return true;
                    CharacterTestTacticalFrameReference reference;
                    if (kind == "Block")
                    {
                        var block = timeline.EventBlocks.SingleOrDefault(b => b.BlockId == id) ?? throw new ArgumentException("事件段关联失效。");
                        if (side == "End") return block.EventIds.Any(member => Affected(member, "End", "Event", visiting));
                        reference = block.Start;
                    }
                    else
                    {
                        var ev = CharacterTestTacticalAuthoringEdits.Event(source, timelineId, id);
                        reference = side != "Start" && (side == "ActionEnd" || side == "ActionStartupEnd" || ev.Operation.Kind == "CharacterAction" || ev.TimingKind == "Instant")
                            ? ev.Start : side == "Start" ? ev.Start : ev.End;
                    }
                    return reference?.Kind == "Anchor" && Affected(reference.SourceEventId, reference.Anchor, reference.TargetKind, visiting);
                }
                finally { visiting.Remove(key); }
            }
            foreach (string id in selected)
            {
                var ev = CharacterTestTacticalAuthoringEdits.Event(source, timelineId, id);
                void Shift(CharacterTestTacticalFrameReference reference)
                {
                    if (reference == null) throw new ArgumentException("时间引用缺失。");
                    if (reference.Kind == "Absolute")
                    { reference.Frame = checked(reference.Frame + delta); if (reference.Frame < 0) throw new ArgumentException("移动后帧不能为负数。"); }
                    else if (!Affected(reference.SourceEventId, reference.Anchor, reference.TargetKind, new HashSet<string>(StringComparer.Ordinal)))
                        reference.OffsetFrames = checked(reference.OffsetFrames + delta);
                }
                Shift(ev.Start);
                if (ev.End != null) Shift(ev.End);
            }
        }

        internal static void LinkRetreatCounterSource(CharacterTestTacticalAuthoringSource source)
        {
            var timeline = source.Timelines.Single(t => t.Events.Any(e => e.EventId == "retreat"));
            var block = Ensure(source, timeline.TimelineId, "retreat", "block.retreat");
            CharacterTestTacticalAuthoringEdits.Event(source, timeline.TimelineId, "approach").Start = BlockEnd(block.BlockId);
            CharacterTestTacticalAuthoringEdits.Event(source, timeline.TimelineId, "release").Start = BlockEnd(block.BlockId);
            CharacterTestTacticalAuthoringEdits.Event(source, timeline.TimelineId, "attack").Start = CharacterTestTacticalAuthoringTemplates.Anchor("release", "End");
        }
    }
}
