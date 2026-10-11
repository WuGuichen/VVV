using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalEditingSession
    {
        internal int BlockDuration(CharacterTestTacticalEventBlock block)
            => Composition?.SelectedNode?.BlockDurations?.TryGetValue(block.BlockId, out int value) == true
                ? value : block.Timing.DurationFrames;

        internal bool SetBlockDuration(CharacterTestTacticalEditToken token, string blockId, int value)
        {
            var block = Preview.FindBlock(blockId);
            if (block?.Timing == null || Composition == null) return false;
            if (value < block.Timing.MinimumFrames)
            { Feedback("制作长度不能小于 " + block.Timing.MinimumFrames + " 帧。"); return false; }
            var composition = Composition;
            bool confirmed = composition.SharedConfirmed;
            bool changed = composition.Edit(token, "修改本次封装制作长度", document => {
                var node = CharacterTestTacticalCompositionEdits.Nodes(document.Root).First(n => n.NodeId == token.NodeId);
                node.BlockDurations ??= new Dictionary<string, int>();
                node.BlockDurations[blockId] = value;
            });
            if (changed && confirmed) composition.ConfirmShared(composition.Token);
            return changed;
        }

        internal void SetSimulationEnd(int frame) => Composition?.SetSimulationEnd(frame, Preview.MinimumFrames);
        internal void SetMinimum(CharacterTestTacticalEditToken token, int frame)
        {
            if (frame < 0) { Feedback("最小持续帧不能为负数。"); return; }
            if (Commit(token, "修改最小持续帧", source => {
                var block = CharacterTestTacticalAuthoringEdits.Timeline(source, Composition.FocusTimelineId).EventBlocks.Single(b => b.BlockId == Composition.FocusBlockId);
                block.Timing.MinimumFrames = frame;
                block.Timing.DurationFrames = Math.Max(frame, block.Timing.DurationFrames);
            })) Composition.SetSimulationEnd(Math.Max(frame, Preview.SimulationEnd), frame);
        }
        internal void SetTimeRelation(CharacterTestTacticalEditToken token, string timeline, string id, bool end,
            string mode, string target = null, string anchor = null)
        {
            bool resolved = Preview.TryGetFrames(id, out int start, out int finish);
            if (!resolved && mode != "Absolute") { Feedback("当前位置不能求值，请先切换手填帧并输入时间，再建立关联。"); return; }
            var previous = CharacterTestTacticalBlockEdits.Reference(Source, timeline, id, end);
            var next = CharacterTestTacticalBlockEdits.Copy(previous);
            if (mode == "Absolute") next = CharacterTestTacticalBlockEdits.Local(resolved ? (end ? finish : start) - Preview.DisplayOrigin : 0);
            else
            {
                if (next == null || next.Kind == "Absolute") next = new CharacterTestTacticalFrameReference {
                    TargetKind = "Builtin", SourceEventId = Composition.FocusBlockId, Anchor = "SimulationEnd" };
                next.Kind = mode; next.Space = "Timeline"; next.Frame = 0;
                if (target != null)
                {
                    int split = target.IndexOf(':');
                    next.TargetKind = target.Substring(0, split);
                    next.SourceEventId = next.TargetKind == "Builtin" ? Composition.FocusBlockId : target.Substring(split + 1);
                    next.Anchor = next.TargetKind == "Builtin" ? target.Substring(split + 1) : "Start";
                }
                if (anchor != null) next.Anchor = anchor;
                try { SetResolvedPoint(next, end ? finish : start, Preview, 0); }
                catch (ArgumentException e) { Feedback(e.Message); return; }
            }
            bool changed = Commit(token, "修改时间关联", source => CharacterTestTacticalAuthoringEdits.SetReference(source, timeline, id, end, next));
            if (changed && !resolved) Feedback("原关联无法求值，已切换为手填 0 帧，请输入正确时间。");
        }
        internal static void SetResolvedPoint(CharacterTestTacticalFrameReference reference, int desired,
            CharacterTestTacticalAuthoringPreview preview, int targetDelta)
        {
            if (reference.Kind == "Absolute")
            { reference.Frame = desired - (reference.Space == "Block" ? preview.DisplayOrigin : 0); return; }
            int target;
            if (preview.TimingPreview != null)
                target = preview.TimingPreview.Position(reference.SourceEventId, reference.Anchor == "SimulationEnd" ? "End" : reference.Anchor);
            else if (preview.TryGetFrames(reference.SourceEventId, out int start, out int end)) target = reference.Anchor == "Start" ? start : end;
            else throw new ArgumentException("引用来源不能求值。");
            target = checked(target + targetDelta);
            if (reference.Kind == "Ratio")
            {
                int distance = target - preview.DisplayOrigin;
                if (distance == 0) throw new ArgumentException("目标位于 0 帧，不能通过除法确定比例；请选择固定间隔。");
                reference.Ratio = (desired - preview.DisplayOrigin) / (double)distance;
                reference.OffsetFrames = 0;
            }
            else reference.OffsetFrames = checked(desired - target);
        }
        internal static void MoveResolved(CharacterTestTacticalAuthoringSource source, string timelineId,
            string[] ids, int delta, CharacterTestTacticalAuthoringPreview preview)
        {
            var timeline = CharacterTestTacticalAuthoringEdits.Timeline(source, timelineId);
            var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            bool Moves(string id, string side, HashSet<string> visiting)
            {
                if (selected.Contains(id)) return true;
                if (!visiting.Add(id + side)) return false;
                var ev = timeline.Events.FirstOrDefault(e => e.EventId == id);
                if (ev == null) return false;
                var reference = side == "Start" || ev.Operation.Kind == "CharacterAction" || ev.TimingKind == "Instant" ? ev.Start : ev.End;
                return reference != null && reference.Kind != "Absolute" && reference.TargetKind == "Event"
                    && Moves(reference.SourceEventId, reference.Anchor, visiting);
            }
            foreach (string id in ids)
            {
                if (!preview.TryGetFrames(id, out int start, out int end)) throw new ArgumentException("事件位置不能求值。");
                var ev = timeline.Events.Single(e => e.EventId == id);
                void Shift(CharacterTestTacticalFrameReference r, int value)
                {
                    int targetDelta = r.TargetKind == "Event" && r.Kind != "Absolute" && Moves(r.SourceEventId, r.Anchor, new HashSet<string>()) ? delta : 0;
                    SetResolvedPoint(r, checked(value + delta), preview, targetDelta);
                }
                Shift(ev.Start, start); if (ev.End != null) Shift(ev.End, end);
            }
        }
    }
}
