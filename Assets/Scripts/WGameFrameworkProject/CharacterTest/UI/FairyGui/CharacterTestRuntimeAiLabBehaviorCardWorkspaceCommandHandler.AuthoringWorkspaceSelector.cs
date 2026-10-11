#if UNITY_EDITOR
using System;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection(
            BehaviorRecipeInspectorTargetKind targetKind,
            string timelineId,
            string eventId)
        {
            TargetKind = targetKind;
            TimelineId = timelineId ?? string.Empty;
            EventId = eventId ?? string.Empty;
        }

        internal BehaviorRecipeInspectorTargetKind TargetKind { get; }
        internal string TimelineId { get; }
        internal string EventId { get; }
        internal bool RequiresStableRestore =>
            TargetKind != BehaviorRecipeInspectorTargetKind.Recipe;
    }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection
            CaptureAuthoringWorkspaceRecipeSelection()
        {
            BehaviorPlanPresentationState presentation =
                _workspaceView?.Invoke()?.Timeline?.Presentation;
            return presentation == null
                ? default
                : new CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection(
                    presentation.RecipeInspectorTargetKind,
                    presentation.SelectedRecipeTimelineId,
                    presentation.SelectedRecipeEventId);
        }

        internal bool TryRestoreAuthoringWorkspaceRecipeSelection(
            in CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection selection)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (timeline?.Presentation == null)
                return false;
            if (selection.TargetKind == BehaviorRecipeInspectorTargetKind.Recipe)
            {
                ApplyRecipeSelection(timeline.Presentation, selection);
                return true;
            }
            if (!TryFindTimeline(
                    timeline.RecipePayload,
                    selection.TimelineId,
                    out CharacterTestRuntimeAiLabBehaviorRecipeTimeline recipeTimeline))
            {
                return false;
            }
            if (selection.TargetKind == BehaviorRecipeInspectorTargetKind.Event
                && !ContainsEvent(recipeTimeline, selection.EventId))
            {
                return false;
            }

            ApplyRecipeSelection(timeline.Presentation, selection);
            return true;
        }

        private static void ApplyRecipeSelection(
            BehaviorPlanPresentationState presentation,
            in CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection selection)
        {
            if (selection.TargetKind == BehaviorRecipeInspectorTargetKind.Event)
            {
                presentation.SelectEvent(selection.TimelineId, selection.EventId);
                presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
                presentation.SelectedRecipeEventStableKey =
                    BehaviorRecipeTimelineProjection.BuildV6EventStableKey(
                        selection.TimelineId,
                        selection.EventId);
                return;
            }
            if (selection.TargetKind == BehaviorRecipeInspectorTargetKind.Timeline)
            {
                presentation.SelectTimeline(selection.TimelineId);
                presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                presentation.SelectedRecipeEventStableKey =
                    "condition-owner:timeline:" + selection.TimelineId;
                return;
            }

            presentation.SelectRecipe();
            presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
            presentation.SelectedRecipeEventStableKey = "condition-owner:activation";
        }

        private static bool TryFindTimeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            timeline = null;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate = payload.Timelines[i];
                if (candidate != null
                    && string.Equals(candidate.TimelineId, timelineId, StringComparison.Ordinal))
                {
                    timeline = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string eventId)
        {
            for (int i = 0; timeline != null && i < timeline.Events.Count; i++)
            {
                if (timeline.Events[i] != null
                    && string.Equals(
                        timeline.Events[i].EventId,
                        eventId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
