using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalEditingSession
    {
        internal void OpenEventFocus(string timeline, string id) => Composition?.OpenEventFocus(timeline, id);
        internal void CloseEventFocus() => Composition?.CloseEventFocus();
        internal bool AddEvent(CharacterTestTacticalEditToken token, string timeline, string kind, int frame,
            string afterEvent = null, string actionSlot = null, bool createSlot = false)
        {
            string selected = null;
            bool changed = Commit(token, "添加事件", source => {
                if (createSlot) source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = actionSlot, Resolver = "binding" });
                if (Focused) selected = CharacterTestTacticalBlockEdits.Add(source, timeline, Composition.FocusBlockId, kind, frame, afterEvent, actionSlot);
                else
                {
                    CharacterTestTacticalAuthoringEdits.Add(source, timeline, kind);
                    var ev = CharacterTestTacticalAuthoringEdits.Timeline(source, timeline).Events.Last();
                    ev.Start = CharacterTestTacticalAuthoringTemplates.Absolute(Math.Max(0, frame));
                    if (ev.End != null) ev.End = CharacterTestTacticalAuthoringTemplates.Anchor(ev.EventId, "Start", kind == "Movement" ? 120 : 1);
                    if (kind == "CharacterAction") ev.Operation.SlotId = actionSlot ?? string.Empty;
                    selected = ev.EventId;
                }
            });
            if (changed) { Selection.Clear(); Selection.Add(selected); Locate?.Invoke(timeline, selected); }
            return changed;
        }
        internal bool ReplaceEvent(CharacterTestTacticalEditToken token, string timeline, string eventId, string kind, string actionSlot = null, bool createSlot = false)
        {
            Preview.TryGetFrames(eventId, out int start, out int end);
            return Commit(token, "替换事件类型", source => {
                if (createSlot) source.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = actionSlot, Resolver = "binding" });
                CharacterTestTacticalBlockEdits.Replace(source, timeline, eventId, kind, actionSlot, end - start);
            });
        }
        internal int FrameOrigin(string timeline, string eventId)
        {
            var block = CharacterTestTacticalBlockEdits.Owner(CharacterTestTacticalAuthoringEdits.Timeline(Source, timeline), eventId);
            return block != null && Preview.TryGetFrames(block.BlockId, out int start, out _) ? start : 0;
        }
    }
}
