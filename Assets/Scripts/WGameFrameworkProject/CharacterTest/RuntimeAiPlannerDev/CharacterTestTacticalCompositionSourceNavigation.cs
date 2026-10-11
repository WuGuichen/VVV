using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalRuntimeSourceTarget
    {
        internal string Label;
        internal CharacterTestTacticalSourceLocation Location;
        internal CharacterTestTacticalFrozenSource Source;
        internal string EventKey;
        internal int Frame, Profile, SessionGeneration;
        internal long AuthorityHash, ProgramHash;
    }

    internal sealed partial class CharacterTestTacticalCompositionEditingSession
    {
        internal CharacterTestTacticalFrozenSource RuntimeSource { get; private set; }
        internal int RuntimeSourceFrame { get; private set; }
        internal List<CharacterTestTacticalRuntimeSourceTarget> RuntimeTargets { get; } = new List<CharacterTestTacticalRuntimeSourceTarget>();
        internal CharacterTestTacticalSourceLocation PendingLocation { get; private set; }
        private (int wait, int action, int move, int turn, int triggered) _runtimeTargetEvents;
        private bool _hasRuntimeTargets;

        internal void BindRuntimeSources(CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime)
        {
            var program = runtime?.CommittedProgram;
            var state = runtime?.LogicAgent?.ExecutionState;
            if (runtime?.Status != CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved || program?.TacticalSource?.CardId != CardId || state == null)
            { RuntimeTargets.Clear(); RuntimeSource = null; _hasRuntimeTargets = false; return; }
            var events = (state.WaitEvent, state.ActionOwner, state.MovementOwner, state.TurnOwner, state.LastTriggeredEvent);
            if (_hasRuntimeTargets && ReferenceEquals(RuntimeSource, program.TacticalSource)
                && RuntimeSourceFrame == runtime.Frame && _runtimeTargetEvents == events) return;
            RuntimeTargets.Clear(); RuntimeSource = program.TacticalSource; RuntimeSourceFrame = runtime.Frame;
            _runtimeTargetEvents = events; _hasRuntimeTargets = true;
            var used = new HashSet<string>(StringComparer.Ordinal);
            void Add(string label, int index)
            {
                string id = program.ExecutionDebugMap?.Event(index);
                if (string.IsNullOrEmpty(id) || !used.Add(id) || !RuntimeSource.Locations.TryGetValue(id, out var location)) return;
                RuntimeTargets.Add(new CharacterTestTacticalRuntimeSourceTarget { Label = label + " · "
                    + (string.IsNullOrEmpty(location.EventId) ? location.Field == "endCondition" ? "局部结束" : "卡层控制" : location.EventId),
                    Location = location, Source = RuntimeSource, EventKey = id, Frame = runtime.Frame, Profile = runtime.ProfileSlot,
                    SessionGeneration = _controller.TacticalRuntimeSessionGeneration, AuthorityHash = runtime.AuthorityHash, ProgramHash = program.SemanticHash });
            }
            Add("正在等待", state.WaitEvent); Add("当前动作", state.ActionOwner);
            Add("当前移动", state.MovementOwner); Add("当前转向", state.TurnOwner);
            Add("最近触发", state.LastTriggeredEvent);
        }

        internal void InspectRuntimeSource(CharacterTestTacticalRuntimeSourceTarget target)
        {
            if (target?.Location == null || target.Source?.CardId != CardId || target.SessionGeneration != _controller.TacticalRuntimeSessionGeneration
                || !_controller.TryResolveFrozenTacticalLocation(target.Frame, target.Profile, target.AuthorityHash, target.ProgramHash,
                    target.EventKey, out var source, out var location) || !ReferenceEquals(source, target.Source))
            { Feedback("运行来源已变化，请重新打开定位列表。"); return; }
            _controller.InspectTacticalRuntimeSource(source, target.Frame, location);
        }
        internal void SelectLocation(CharacterTestTacticalSourceLocation location)
        {
            string node = string.IsNullOrEmpty(location.NodeId) ? Document.Root.NodeId : location.NodeId;
            var target = !string.IsNullOrEmpty(location.DefinitionId) ? CharacterTestTacticalEditTarget.SharedDefinition
                : location.Field == "activationConditionRoot" ? CharacterTestTacticalEditTarget.CardActivation
                : location.Field == "endCondition" ? CharacterTestTacticalEditTarget.NodeEnd
                : location.Field == "condition" ? CharacterTestTacticalEditTarget.NodeCondition : CharacterTestTacticalEditTarget.Composition;
            Select(node, target); PendingLocation = location;
        }
        internal void ApplyPendingLocation(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            var location = PendingLocation; PendingLocation = null;
            var preview = timeline?.TacticalPreview;
            if (location == null || preview == null) return;
            if (string.IsNullOrEmpty(location.EventId)) { timeline.Presentation.SelectRecipe(); return; }
            var owner = preview.Source.Timelines.FirstOrDefault(t => t.Events.Any(e => e.EventId == location.EventId)
                || t.EventBlocks.Any(b => b.BlockId == location.EventId));
            if (owner == null) { Feedback("冻结内部事件来源未能解析。"); return; }
            timeline.Presentation.SelectEvent(owner.TimelineId, location.EventId);
            timeline.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
            timeline.Presentation.SelectedRecipeEventStableKey = BehaviorRecipeTimelineProjection.BuildV6EventStableKey(owner.TimelineId, location.EventId);
            if (!string.IsNullOrEmpty(location.ConditionNodeId)) preview.Editor?.OpenConditions(owner.TimelineId, location.EventId);
        }
        internal void ReturnToCurrentSource() => _controller.ReturnToCurrentTacticalSource();
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestTacticalFrozenSource _inspectedTacticalRuntimeSource;
        private int _inspectedTacticalRuntimeFrame;
        private int _inspectedTacticalRuntimeGeneration;
        private CharacterTestTacticalSourceLocation _inspectedTacticalLocation, _pendingCurrentTacticalLocation;
        internal int TacticalRuntimeSessionGeneration => _timelineSessionGeneration;
        internal int TacticalSourceInspectionFrame => _inspectedTacticalRuntimeSource != null ? _inspectedTacticalRuntimeFrame : _timeline?.SelectedFrame ?? -1;

        internal void InspectTacticalRuntimeSource(CharacterTestTacticalFrozenSource source, int frame, CharacterTestTacticalSourceLocation location)
        {
            if (source == null || _compositionEditingSession == null || source.CardId != _compositionEditingSession.CardId
                || !DraftWorkspace.Cards.TryGetValue(source.CardId, out var card)) return;
            _inspectedTacticalRuntimeSource = source; _inspectedTacticalRuntimeFrame = frame;
            _inspectedTacticalRuntimeGeneration = _timelineSessionGeneration;
            _inspectedTacticalLocation = location;
            _compositionEditingSession.Bind(DraftWorkspace, card, source);
            _compositionEditingSession.SelectLocation(location);
            RefreshTacticalPreview();
        }
        internal void ReturnToCurrentTacticalSource()
        {
            _pendingCurrentTacticalLocation = _inspectedTacticalLocation; _inspectedTacticalLocation = null;
            _inspectedTacticalRuntimeSource = null;
            if (IsHistoricalFrameSelected) JumpToFrame(_timeline.LatestFrame);
            RefreshTacticalPreview();
        }
    }
}
