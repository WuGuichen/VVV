using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalCompositionEditingSession
    {
        internal string FocusTimelineId { get; private set; }
        internal string FocusEventId { get; private set; }
        internal string FocusBlockId { get; private set; }
        internal bool EventFocusActive => !string.IsNullOrEmpty(FocusBlockId);
        internal int SimulationEndFrame { get; private set; } = -1;
        internal void SetSimulationEnd(int frame, int minimum)
        {
            SimulationEndFrame = Math.Max(minimum, frame);
            _controller.RefreshTacticalPreview();
        }
        internal void OpenEventFocus(string timelineId, string eventOrBlockId)
        {
            var timeline = SelectedDefinition?.Source.Timelines.FirstOrDefault(t => t.TimelineId == timelineId);
            if (timeline == null) { Feedback("请先选择共享封装。"); return; }
            var block = timeline.EventBlocks.FirstOrDefault(b => b.BlockId == eventOrBlockId)
                ?? CharacterTestTacticalBlockEdits.Owner(timeline, eventOrBlockId);
            var ev = timeline.Events.FirstOrDefault(e => e.EventId == eventOrBlockId);
            if (block == null && ev == null) { Feedback("源事件已不存在。"); return; }
            FocusTimelineId = timelineId; FocusEventId = ev?.EventId ?? block.EventIds.FirstOrDefault();
            FocusBlockId = block?.BlockId ?? "block." + Guid.NewGuid().ToString("N");
            SimulationEndFrame = -1;
            PendingLocation = new CharacterTestTacticalSourceLocation(CardId, SelectedNodeId, "events", SelectedDefinition.DefinitionId,
                SelectedDefinition.Revision, FocusEventId);
            _controller.NativeTacticalExecutionView = false;
            SelectedCompiledEventId = string.Empty;
            _serial++; RefreshToken(); _controller.RefreshTacticalPreview();
        }
        internal void CloseEventFocus()
        {
            string timeline = FocusTimelineId, block = FocusBlockId, ev = FocusEventId;
            ClearEventFocus(); _serial++; RefreshToken(); _controller.RefreshTacticalPreview();
            var source = SelectedDefinition?.Source.Timelines.FirstOrDefault(t => t.TimelineId == timeline);
            PendingLocation = new CharacterTestTacticalSourceLocation(CardId, SelectedNodeId, "events", SelectedDefinition?.DefinitionId,
                SelectedDefinition?.Revision ?? 0, source?.EventBlocks.Any(b => b.BlockId == block) == true ? block : ev);
        }
        private void ClearEventFocus() { FocusTimelineId = FocusEventId = FocusBlockId = null; SimulationEndFrame = -1; }
        internal void EnsureFocusedBlock(CharacterTestTacticalAuthoringSource source)
        {
            if (EventFocusActive) CharacterTestTacticalBlockEdits.Ensure(source, FocusTimelineId, FocusEventId, FocusBlockId);
        }
    }
}
