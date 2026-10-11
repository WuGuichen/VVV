using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct BehaviorPlanTimeRange
    {
        internal BehaviorPlanTimeRange(
            int plannedStartRelativeFrame,
            int plannedDurationFrames,
            int presentationEndRelativeFrame)
        {
            PlannedStartRelativeFrame = Math.Max(0, plannedStartRelativeFrame);
            PlannedDurationFrames = Math.Max(0, plannedDurationFrames);
            int plannedEndRelativeFrame = PlannedStartRelativeFrame + PlannedDurationFrames;
            PresentationEndRelativeFrame = Math.Max(
                Math.Max(PlannedStartRelativeFrame + 1, plannedEndRelativeFrame),
                presentationEndRelativeFrame);
        }

        internal int PlannedStartRelativeFrame { get; }
        internal int PlannedDurationFrames { get; }
        internal int PlannedEndRelativeFrame => PlannedStartRelativeFrame + PlannedDurationFrames;
        internal int PresentationEndRelativeFrame { get; }
    }

    internal readonly struct BehaviorRuntimeTimeSnapshot
    {
        internal BehaviorRuntimeTimeSnapshot(
            int runtimeFrame,
            int cardStartedRuntimeFrame,
            int stageEnteredRuntimeFrame,
            int actionEnteredRuntimeFrame)
        {
            RuntimeFrame = runtimeFrame;
            CardStartedRuntimeFrame = cardStartedRuntimeFrame;
            StageEnteredRuntimeFrame = stageEnteredRuntimeFrame;
            ActionEnteredRuntimeFrame = actionEnteredRuntimeFrame;
        }

        internal int RuntimeFrame { get; }
        internal int CardStartedRuntimeFrame { get; }
        internal int StageEnteredRuntimeFrame { get; }
        internal int ActionEnteredRuntimeFrame { get; }

        internal int GetCardElapsedFrame() =>
            GetElapsedFrame(RuntimeFrame, CardStartedRuntimeFrame);

        internal int GetStageElapsedFrame() =>
            GetElapsedFrame(RuntimeFrame, StageEnteredRuntimeFrame);

        internal int GetActionElapsedFrame() =>
            GetElapsedFrame(RuntimeFrame, ActionEnteredRuntimeFrame);

        private static int GetElapsedFrame(int runtimeFrame, int originRuntimeFrame)
        {
            return runtimeFrame >= 0
                && originRuntimeFrame >= 0
                && runtimeFrame >= originRuntimeFrame
                    ? runtimeFrame - originRuntimeFrame
                    : -1;
        }
    }

    internal readonly struct BehaviorTimelineViewport
    {
        internal BehaviorTimelineViewport(
            int startRelativeFrame,
            int endRelativeFrame,
            float zoomLevel,
            float verticalScrollOffset,
            bool followCurrent,
            int snapFrames)
        {
            StartRelativeFrame = Math.Max(0, startRelativeFrame);
            EndRelativeFrame = Math.Max(StartRelativeFrame + 1, endRelativeFrame);
            ZoomLevel = Math.Max(0.1f, zoomLevel);
            VerticalScrollOffset = Math.Max(0f, verticalScrollOffset);
            FollowCurrent = followCurrent;
            SnapFrames = Math.Max(1, snapFrames);
        }

        internal int StartRelativeFrame { get; }
        internal int EndRelativeFrame { get; }
        internal int SpanFrames => EndRelativeFrame - StartRelativeFrame;
        internal float ZoomLevel { get; }
        internal float VerticalScrollOffset { get; }
        internal bool FollowCurrent { get; }
        internal int SnapFrames { get; }
    }

    internal readonly struct BehaviorTimelineTransform
    {
        private readonly BehaviorTimelineViewport _viewport;
        private readonly float _canvasLeft;
        private readonly float _canvasWidth;

        internal BehaviorTimelineTransform(
            in BehaviorTimelineViewport viewport,
            float canvasLeft,
            float canvasWidth)
        {
            _viewport = viewport;
            _canvasLeft = canvasLeft;
            _canvasWidth = Math.Max(1f, canvasWidth);
        }

        internal float FrameToX(int relativeFrame)
        {
            float fraction = (relativeFrame - _viewport.StartRelativeFrame)
                / (float)_viewport.SpanFrames;
            if (fraction < 0f)
                fraction = 0f;
            else if (fraction > 1f)
                fraction = 1f;
            return _canvasLeft + _canvasWidth * fraction;
        }

        internal int XToFrame(float x)
        {
            float fraction = (x - _canvasLeft) / _canvasWidth;
            if (fraction < 0f)
                fraction = 0f;
            else if (fraction > 1f)
                fraction = 1f;
            return _viewport.StartRelativeFrame
                + (int)Math.Round(_viewport.SpanFrames * fraction, MidpointRounding.AwayFromZero);
        }

        internal int SnapRelativeFrame(int relativeFrame)
        {
            int clamped = Math.Max(0, relativeFrame);
            int snap = _viewport.SnapFrames;
            return ((clamped + snap / 2) / snap) * snap;
        }
    }

    internal sealed class BehaviorPlanStructureViewModel
    {
        internal CharacterTestRuntimeAiLabLogicStageViewModel[] Stages =
            Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
        internal CharacterTestRuntimeAiLabLogicDetectorViewModel[] Detectors =
            Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
        internal CharacterTestRuntimeAiLabLogicTransitionViewModel[] Transitions =
            Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
        internal int StageCount;
        internal int DetectorCount;
        internal int TransitionCount;
        internal int DurationFrames = 240;
        internal long CachedStructureKey;
        internal bool HasCachedStructure;
    }

    internal sealed class BehaviorPlanRuntimeOverlayViewModel
    {
        internal int RuntimeFrame = -1;
        internal int CardStartedRuntimeFrame = -1;
        internal int StageEnteredRuntimeFrame = -1;
        internal int ActionEnteredRuntimeFrame = -1;
        internal bool Historical;
        internal int HistoricalFrame = -1;
        internal int HistoricalLatestFrame = -1;
        internal bool HistoricalEventEvidenceComplete;
        internal string HistoricalDiagnosticCode = string.Empty;
        internal CharacterTestRuntimeAiLabActiveActionProgress ActiveActionProgress;
        internal int PrimaryActionState;
        internal int PrimaryActionStageIndex = -1;
        internal int ObservedTargetDistanceDecimeters = -1;
        internal CharacterTestRuntimeAiLabBehaviorCandidateViewModel[] Candidates =
            Array.Empty<CharacterTestRuntimeAiLabBehaviorCandidateViewModel>();
        internal int CandidateCount;

        internal BehaviorRuntimeTimeSnapshot Time => new BehaviorRuntimeTimeSnapshot(
            RuntimeFrame,
            CardStartedRuntimeFrame,
            StageEnteredRuntimeFrame,
            ActionEnteredRuntimeFrame);
    }

    internal enum BehaviorRecipeInspectorTargetKind
    {
        Recipe,
        Timeline,
        Event
    }

    internal sealed class BehaviorPlanPresentationState
    {
        internal CharacterTestRuntimeAiLabLogicElementKind SelectedElementKind;
        internal int SelectedElementIndex = -1;
        internal int HorizontalStartFrame;
        internal int HorizontalEndFrame = 240;
        internal float ZoomLevel = 1f;
        internal float VerticalScrollOffset;
        internal bool FollowCurrent;
        internal BehaviorRecipeEventKind SelectedRecipeEventKind;
        internal string SelectedRecipeEventStableKey = string.Empty;
        internal BehaviorRecipeInspectorTargetKind RecipeInspectorTargetKind =
            BehaviorRecipeInspectorTargetKind.Recipe;
        internal string SelectedRecipeTimelineId = string.Empty;
        internal string SelectedRecipeEventId = string.Empty;

        internal void SelectRecipe()
        {
            RecipeInspectorTargetKind = BehaviorRecipeInspectorTargetKind.Recipe;
            SelectedRecipeTimelineId = string.Empty;
            SelectedRecipeEventId = string.Empty;
        }

        internal void SelectTimeline(string timelineId)
        {
            RecipeInspectorTargetKind = BehaviorRecipeInspectorTargetKind.Timeline;
            SelectedRecipeTimelineId = timelineId ?? string.Empty;
            SelectedRecipeEventId = string.Empty;
        }

        internal void SelectEvent(string timelineId, string eventId)
        {
            RecipeInspectorTargetKind = BehaviorRecipeInspectorTargetKind.Event;
            SelectedRecipeTimelineId = timelineId ?? string.Empty;
            SelectedRecipeEventId = eventId ?? string.Empty;
        }

        internal BehaviorTimelineViewport Viewport => new BehaviorTimelineViewport(
            HorizontalStartFrame,
            HorizontalEndFrame,
            ZoomLevel,
            VerticalScrollOffset,
            FollowCurrent,
            1);
    }

    internal static class BehaviorPlanProjectionBuilder
    {
        internal static void BeginBuild(BehaviorPlanStructureViewModel structure)
        {
            if (structure == null)
                return;
            structure.StageCount = 0;
            structure.DetectorCount = 0;
            structure.TransitionCount = 0;
        }

        /// <remarks>LowFreqAlloc when the projection reaches a new structure high-water mark.</remarks>
        internal static void Rebuild(
            GameplayReadablePlanProjection projection,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            projection?.Rebuild(timeline);
        }
    }

    internal static class BehaviorPlanRuntimeOverlayBuilder
    {
        internal static void Populate(
            BehaviorPlanRuntimeOverlayViewModel overlay,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection historical,
            in CharacterTestRuntimeAiLabActiveActionProgress activeActionProgress,
            int runtimeFrame,
            float observedTargetDistance)
        {
            if (overlay == null)
                return;
            bool isHistorical = historical?.IsHistorical == true;
            overlay.Historical = isHistorical;
            overlay.RuntimeFrame = runtimeFrame;
            overlay.HistoricalEventEvidenceComplete = historical?.EventEvidenceComplete == true;
            overlay.HistoricalFrame = historical?.SelectedFrame ?? -1;
            overlay.HistoricalLatestFrame = historical?.LatestFrame ?? -1;
            overlay.HistoricalDiagnosticCode = historical?.DiagnosticCode ?? string.Empty;
            overlay.ActiveActionProgress = historical == null ? activeActionProgress : default;
            overlay.CardStartedRuntimeFrame = historical == null
                ? runtimeState?.CardStartedFrame ?? -1
                : -1;
            overlay.StageEnteredRuntimeFrame = historical == null
                ? runtimeState?.StageEnteredFrame ?? -1
                : -1;
            overlay.ActionEnteredRuntimeFrame = historical == null
                ? runtimeState?.ActionEnteredFrame ?? -1
                : -1;
            overlay.PrimaryActionState = historical == null ? runtimeState?.PrimaryActionState ?? 0 : 0;
            overlay.PrimaryActionStageIndex = historical == null
                ? runtimeState?.PrimaryActionStageIndex ?? -1
                : -1;
            overlay.ObservedTargetDistanceDecimeters = observedTargetDistance >= 0f
                && !float.IsNaN(observedTargetDistance)
                && !float.IsInfinity(observedTargetDistance)
                    ? Math.Min(
                        1000,
                        Math.Max(
                            0,
                            (int)Math.Round(
                                observedTargetDistance * 10f,
                                MidpointRounding.ToEven)))
                    : -1;
        }

        /// <remarks>No collection allocation after projection warm-up.</remarks>
        internal static void Update(
            GameplayReadablePlanProjection projection,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            projection?.UpdateRuntime(timeline);
        }
    }

    internal interface IBehaviorPlanInteractionSink
    {
        void SelectStage(string stableId);
        void SelectDetector(string stableId);
        void SelectTransition(string stableId);
        void LocateRuntimeFrame(string stableId, int runtimeFrame);
        void UpdateViewport(int horizontalDirection, int zoomDirection, int verticalDirection, bool fit);
    }

    internal sealed class BehaviorPlanInteractionAdapter : IBehaviorPlanInteractionSink
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly BehaviorPlanNavigationCommands _navigation;

        internal BehaviorPlanInteractionAdapter(IMxUiCommandSink commandSink, MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _navigation = new BehaviorPlanNavigationCommands(commandSink, viewId);
        }

        public void SelectStage(string stableId) => _navigation.SelectStage(stableId);

        public void SelectDetector(string stableId) => _navigation.SelectDetector(stableId);

        public void SelectTransition(string stableId) => _navigation.SelectTransition(stableId);

        public void LocateRuntimeFrame(string stableId, int runtimeFrame)
        {
            _navigation.LocateRuntimeFrame(stableId, runtimeFrame);
        }

        public void UpdateViewport(
            int horizontalDirection,
            int zoomDirection,
            int verticalDirection,
            bool fit)
        {
            if (!fit
                && horizontalDirection == 0
                && zoomDirection == 0
                && verticalDirection == 0)
            {
                return;
            }
            string commandId = fit
                ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.FitTimeline
                : zoomDirection > 0
                    ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineIn
                    : zoomDirection < 0
                        ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineOut
                        : horizontalDirection < 0
                            ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineLeft
                            : horizontalDirection > 0
                                ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineRight
                                : verticalDirection < 0
                                    ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineUp
                                    : CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineDown;
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null));
        }
    }

    internal sealed class BehaviorPlanNavigationCommands
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;

        internal BehaviorPlanNavigationCommands(IMxUiCommandSink commandSink, MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _viewId = viewId;
        }

        internal void SelectStage(string stableId) => EnqueueSelection(
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage,
            stableId,
            CharacterTestRuntimeAiLabLogicElementKind.Stage);

        internal void SelectDetector(string stableId) => EnqueueSelection(
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
            stableId,
            CharacterTestRuntimeAiLabLogicElementKind.Detector);

        internal void SelectTransition(string stableId) => EnqueueSelection(
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition,
            stableId,
            CharacterTestRuntimeAiLabLogicElementKind.Transition);

        internal void LocateRuntimeFrame(string stableId, int runtimeFrame)
        {
            if (runtimeFrame < 0)
                return;
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .BehaviorTransitionLocateRuntimeFrame,
                new CharacterTestRuntimeAiLabBehaviorTransitionNavigationCommand(
                    stableId,
                    runtimeFrame)));
        }

        private void EnqueueSelection(
            string commandId,
            string stableId,
            CharacterTestRuntimeAiLabLogicElementKind kind)
        {
            string normalized = stableId ?? string.Empty;
            if (normalized.Length == 0)
                return;
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                commandId,
                new CharacterTestRuntimeAiLabBehaviorCardElementCommand(normalized, 0, kind)));
        }
    }

    internal static class BehaviorPlanViewportCommands
    {
        internal static BehaviorTimelineViewport CalculateNext(
            in BehaviorTimelineViewport current,
            int contentFrames,
            int horizontalDirection,
            int zoomDirection,
            int verticalDirection,
            bool fit)
        {
            int span = Math.Max(60, current.SpanFrames);
            int boundedContentFrames = Math.Max(60, contentFrames);
            int nextSpan = fit
                ? boundedContentFrames
                : zoomDirection > 0
                    ? Math.Max(60, span * 4 / 5)
                    : zoomDirection < 0
                        ? Math.Min(3600, span * 5 / 4)
                        : span;
            int pan = horizontalDirection * Math.Max(15, nextSpan / 4);
            int center = current.StartRelativeFrame + span / 2;
            int start = fit ? 0 : Math.Max(0, center - nextSpan / 2 + pan);
            float vertical = fit
                ? 0f
                : Math.Max(0f, current.VerticalScrollOffset + verticalDirection * 240f);
            return new BehaviorTimelineViewport(
                start,
                start + nextSpan,
                240f / nextSpan,
                vertical,
                fit,
                current.SnapFrames);
        }
    }
}
