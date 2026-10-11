using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal enum BehaviorRecipeEventGroupKind
    {
        Action = 0,
        Movement = 1,
        Trigger = 2,
        Flow = 3
    }

    internal enum BehaviorRecipeTrackKind
    {
        EventLane,
        Entry,
        PrimaryAction,
        Branch,
        LegacySharedBranch
    }

    internal enum BehaviorRecipeEventKind
    {
        None,
        Event,
        StageEntry,
        PrimaryAction,
        Branch,
        LegacySharedBranch
    }

    internal enum BehaviorRecipeEventPrimitive
    {
        Marker,
        Span,
        OpenSpan
    }

    internal sealed class BehaviorRecipeEventViewModel
    {
        private readonly List<int> _transitionIndices = new List<int>(4);

        internal string StableKey = string.Empty;
        internal BehaviorRecipeEventKind Kind;
        internal BehaviorRecipeEventPrimitive Primitive;
        internal BehaviorRecipeEventGroupKind GroupKind;
        internal string GroupDisplayName = string.Empty;
        internal CharacterTestRuntimeAiLabLogicElementKind SelectionKind;
        internal int SelectionIndex = -1;
        internal int StageIndex = -1;
        internal int DetectorIndex = -1;
        internal string StageId = string.Empty;
        internal string DetectorId = string.Empty;
        internal string EventId = string.Empty;
        internal string TacticalOperationKind;
        internal string TacticalSourceEventId;
        internal string TacticalSourceNodeId;
        internal bool SourceHighlighted;
        internal string Title = string.Empty;
        internal string ConditionLabel = string.Empty;
        internal string Detail = string.Empty;
        internal string ResultLabel = string.Empty;
        internal int StartFrame;
        internal int DurationFrames = 1;
        internal int EffectiveTransitionIndex = -1;
        internal int AdditionalResultCount;
        internal int Priority;
        internal int CompiledEventIndex = -1;
        internal CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind TimingKind;
        internal CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind OperationKind;
        internal bool CharacterActionEvent;
        internal MxFramework.CharacterAction.CharacterActionId CharacterActionId;
        internal string CharacterActionDisplayName = string.Empty;
        internal int ProgressFrames = -1;
        internal bool Selected;
        internal bool Running;
        internal bool Invalid;
        internal bool DurationReviewRequired;
        internal bool Shared;
        internal bool CanMove;
        internal bool CanResize;

        internal IReadOnlyList<int> TransitionIndices => _transitionIndices;

        internal void ResetTransitions()
        {
            _transitionIndices.Clear();
            EffectiveTransitionIndex = -1;
            AdditionalResultCount = 0;
        }

        internal void AddTransition(int transitionIndex)
        {
            _transitionIndices.Add(transitionIndex);
        }
    }

    internal sealed class BehaviorRecipeTrackViewModel
    {
        internal string StableKey = string.Empty;
        internal BehaviorRecipeTrackKind Kind;
        internal BehaviorRecipeEventGroupKind GroupKind;
        internal string DisplayName = string.Empty;
        internal int StageIndex = -1;
        internal int VerticalOrder;
        internal bool OwnsLaneBackground;
        internal BehaviorRecipeEventViewModel Event;
    }

    internal sealed class BehaviorRecipeTimelineViewModel
    {
        private readonly List<BehaviorRecipeTrackViewModel> _tracks =
            new List<BehaviorRecipeTrackViewModel>(8);

        internal string StageId = string.Empty;
        internal string Title = string.Empty;
        internal int StageIndex = -1;
        internal int Order;
        internal bool Entry;
        internal bool Current;
        internal bool Selected;
        internal string EntryConditionSummary = string.Empty;
        internal int DurationFrames;
        internal int LaneCount;

        internal int TrackCount => _tracks.Count;

        internal BehaviorRecipeTrackViewModel GetTrack(int index) =>
            index >= 0 && index < _tracks.Count ? _tracks[index] : null;

        internal void ClearTracks()
        {
            _tracks.Clear();
            LaneCount = 0;
        }

        internal void AddTrack(BehaviorRecipeTrackViewModel track)
        {
            if (track == null)
                return;
            if (track.Kind != BehaviorRecipeTrackKind.EventLane)
                track.OwnsLaneBackground = true;
            LaneCount = Math.Max(LaneCount, track.VerticalOrder + 1);
            _tracks.Add(track);
        }
    }

    /// <summary>
    /// Human-readable Timeline/Event projection over the native v6 authority.
    /// The existing Stage/Detector/Transition view models are compatibility adapters only; this
    /// Automatic lanes own no authoring data and never become a second runtime authority.
    /// </summary>
    internal sealed partial class BehaviorRecipeTimelineProjection
    {
        private readonly List<BehaviorRecipeTimelineViewModel> _timelinePool =
            new List<BehaviorRecipeTimelineViewModel>(16);
        private readonly List<BehaviorRecipeTrackViewModel> _trackPool =
            new List<BehaviorRecipeTrackViewModel>(48);
        private readonly List<BehaviorRecipeEventViewModel> _eventPool =
            new List<BehaviorRecipeEventViewModel>(48);
        private int[] _stageOrder = Array.Empty<int>();
        private int[] _detectorOrder = Array.Empty<int>();
        private int[] _laneEndFrames = Array.Empty<int>();
        private int _timelineCount;
        private int _trackCount;
        private int _eventCount;

        internal int TimelineCount => _timelineCount;
        internal int MaximumLocalFrame { get; private set; } = 60;
        internal int CurrentTimelineIndex { get; private set; } = -1;
        internal int LocalPlayheadFrame { get; private set; } = -1;
        internal long StructureKey { get; private set; }

        internal BehaviorRecipeTimelineViewModel GetTimeline(int index) =>
            index >= 0 && index < _timelineCount ? _timelinePool[index] : null;

        internal void Rebuild(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source)
        {
            _timelineCount = 0;
            _trackCount = 0;
            _eventCount = 0;
            MaximumLocalFrame = 60;
            CurrentTimelineIndex = -1;
            LocalPlayheadFrame = -1;
            StructureKey = source?.CachedStructureKey ?? 0L;
            // Invalid authored timing is still editable. Available also reflects executable
            // program readiness, which must not erase an owned source projection.
            if (source == null || !source.Available && (source.InspectCompiledExecution
                || source.TacticalPreview?.WholeCard != null || source.TacticalPreview?.DisplayPayload == null))
                return;

            if (source.RecipePayload != null
                && CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(source.RecipePayload))
            {
                RebuildV6(source, source.RecipePayload);
                return;
            }

            LocalPlayheadFrame = ResolveLegacyLocalPlayhead(source);

            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                source.Stages ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
                source.Detectors ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions =
                source.Transitions ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
            int stageCount = Math.Min(source.StageCount, stages.Length);
            int detectorCount = Math.Min(source.DetectorCount, detectors.Length);
            int transitionCount = Math.Min(source.TransitionCount, transitions.Length);
            EnsureOrderCapacity(stageCount, detectorCount);
            BuildStageOrder(stages, stageCount);

            for (int orderedStage = 0; orderedStage < stageCount; orderedStage++)
            {
                int stageIndex = _stageOrder[orderedStage];
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[stageIndex];
                if (stage == null)
                    continue;
                BehaviorRecipeTimelineViewModel timeline = GetOrCreateTimeline(_timelineCount++);
                timeline.ClearTracks();
                timeline.StageId = stage.StageId ?? string.Empty;
                timeline.Title = string.IsNullOrWhiteSpace(stage.Title)
                    ? "阶段 " + (orderedStage + 1)
                    : stage.Title.Trim();
                timeline.StageIndex = stageIndex;
                timeline.Order = stage.Order;
                timeline.Entry = stage.Entry;
                timeline.Current = stage.Current;
                timeline.Selected = stage.Selected;
                timeline.EntryConditionSummary = string.Empty;
                timeline.DurationFrames = 0;
                if (timeline.Current)
                    CurrentTimelineIndex = _timelineCount - 1;

                if (stage.EntryConditionCount > 0)
                    AddEntryTrack(source, timeline, stage);
                AddPrimaryActionTrack(source, timeline, stage);
                BuildDetectorOrder(detectors, detectorCount, stageIndex, cardScope: false);
                for (int detectorOrder = 0; detectorOrder < detectorCount; detectorOrder++)
                {
                    int detectorIndex = _detectorOrder[detectorOrder];
                    if (detectorIndex < 0)
                        break;
                    AddBranchTrack(
                        source,
                        timeline,
                        detectors[detectorIndex],
                        detectorIndex,
                        transitions,
                        transitionCount,
                        shared: false);
                }

                BuildDetectorOrder(detectors, detectorCount, stageIndex, cardScope: true);
                for (int detectorOrder = 0; detectorOrder < detectorCount; detectorOrder++)
                {
                    int detectorIndex = _detectorOrder[detectorOrder];
                    if (detectorIndex < 0)
                        break;
                    AddBranchTrack(
                        source,
                        timeline,
                        detectors[detectorIndex],
                        detectorIndex,
                        transitions,
                        transitionCount,
                        shared: true);
                }
            }

            StructureKey = ComputeStructureKey(source);
        }

        private const int InstantVisualSafeSpanFrames = 20;

        internal static BehaviorRecipeEventGroupKind ResolveEventGroupKind(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            if (evt == null)
                return BehaviorRecipeEventGroupKind.Flow;
            if (evt.Operation != null)
            {
                switch (evt.Operation.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability:
                        return BehaviorRecipeEventGroupKind.Action;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget:
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold:
                        return BehaviorRecipeEventGroupKind.Movement;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline:
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition:
                        return BehaviorRecipeEventGroupKind.Trigger;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner:
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion:
                        return BehaviorRecipeEventGroupKind.Flow;
                }
            }
            if (evt.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant)
                return BehaviorRecipeEventGroupKind.Trigger;
            return BehaviorRecipeEventGroupKind.Flow;
        }

        internal static string FormatEventGroupDisplayName(BehaviorRecipeEventGroupKind groupKind)
        {
            switch (groupKind)
            {
                case BehaviorRecipeEventGroupKind.Action:
                    return "动作 / 技能";
                case BehaviorRecipeEventGroupKind.Movement:
                    return "位移 / 控制";
                case BehaviorRecipeEventGroupKind.Trigger:
                    return "触发 / 分支";
                case BehaviorRecipeEventGroupKind.Flow:
                default:
                    return "流程 / 决策";
            }
        }

    }
}
