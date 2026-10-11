using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class BehaviorRecipeTimelineProjection
    {
        private static int ResolveLegacyLocalPlayhead(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source)
        {
            if (source == null
                || source.RuntimeFrame < 0
                || source.StageEnteredFrame < 0
                || source.RuntimeFrame < source.StageEnteredFrame)
                return -1;
            return source.RuntimeFrame - source.StageEnteredFrame;
        }

        private static bool IsSelected(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            BehaviorRecipeEventViewModel recipeEvent,
            bool sourceSelected)
        {
            string selectedStableKey = source?.Presentation.SelectedRecipeEventStableKey;
            if (!string.IsNullOrEmpty(selectedStableKey))
                return string.Equals(selectedStableKey, recipeEvent.StableKey, StringComparison.Ordinal);
            if (!sourceSelected)
                return false;
            BehaviorRecipeEventKind selectedKind = source?.Presentation.SelectedRecipeEventKind
                ?? BehaviorRecipeEventKind.None;
            if (selectedKind != BehaviorRecipeEventKind.None)
                return selectedKind == recipeEvent.Kind;
            return recipeEvent.SelectionKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                ? recipeEvent.Kind == BehaviorRecipeEventKind.PrimaryAction
                : recipeEvent.Kind is BehaviorRecipeEventKind.Branch
                    or BehaviorRecipeEventKind.LegacySharedBranch;
        }

        private static bool HasInvalidTransition(
            BehaviorRecipeEventViewModel recipeEvent,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions)
        {
            IReadOnlyList<int> indices = recipeEvent.TransitionIndices;
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index >= 0 && index < transitions.Length && transitions[index]?.Invalid == true)
                    return true;
            }
            return false;
        }

        private static void CollectTransitions(
            BehaviorRecipeEventViewModel recipeEvent,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            int effective = -1;
            for (int i = 0; i < transitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = transitions[i];
                if (transition == null || transition.DetectorIndex != detector.Index)
                    continue;
                recipeEvent.AddTransition(i);
                if (effective < 0 || CompareTransitionPriority(transition, transitions[effective]) < 0)
                    effective = i;
            }
            recipeEvent.EffectiveTransitionIndex = effective;
            recipeEvent.AdditionalResultCount = Math.Max(0, recipeEvent.TransitionIndices.Count - 1);
        }

        private static int CompareTransitionPriority(
            CharacterTestRuntimeAiLabLogicTransitionViewModel left,
            CharacterTestRuntimeAiLabLogicTransitionViewModel right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0
                ? priority
                : string.CompareOrdinal(left.TransitionId, right.TransitionId);
        }

        private void BuildStageOrder(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            for (int i = 0; i < stageCount; i++)
            {
                _stageOrder[i] = i;
                int cursor = i;
                while (cursor > 0
                    && CompareStages(stages[_stageOrder[cursor]], stages[_stageOrder[cursor - 1]]) < 0)
                {
                    (_stageOrder[cursor - 1], _stageOrder[cursor]) =
                        (_stageOrder[cursor], _stageOrder[cursor - 1]);
                    cursor--;
                }
            }
        }

        private void BuildDetectorOrder(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int stageIndex,
            bool cardScope)
        {
            for (int i = 0; i < detectorCount; i++)
                _detectorOrder[i] = -1;
            int used = 0;
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[i];
                bool included = detector != null && (cardScope
                    ? detector.CardScope
                    : !detector.CardScope && detector.StageIndex == stageIndex);
                if (!included)
                    continue;
                _detectorOrder[used] = i;
                int cursor = used;
                used++;
                while (cursor > 0
                    && CompareDetectors(
                        detectors[_detectorOrder[cursor]],
                        detectors[_detectorOrder[cursor - 1]]) < 0)
                {
                    (_detectorOrder[cursor - 1], _detectorOrder[cursor]) =
                        (_detectorOrder[cursor], _detectorOrder[cursor - 1]);
                    cursor--;
                }
            }
        }

        private static int CompareStages(
            CharacterTestRuntimeAiLabLogicStageViewModel left,
            CharacterTestRuntimeAiLabLogicStageViewModel right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.StageId, right.StageId);
        }

        private static int CompareDetectors(
            CharacterTestRuntimeAiLabLogicDetectorViewModel left,
            CharacterTestRuntimeAiLabLogicDetectorViewModel right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0
                ? priority
                : string.CompareOrdinal(left.DetectorId, right.DetectorId);
        }

        private long ComputeStructureKey(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source)
        {
            unchecked
            {
                long key = source.CachedStructureKey;
                key = key * 397L ^ _timelineCount;
                key = key * 397L ^ MaximumLocalFrame;
                key = key * 397L ^ source.HorizontalStartFrame;
                key = key * 397L ^ source.HorizontalEndFrame;
                key = key * 397L ^ StableHash(source.Presentation.SelectedRecipeEventStableKey);
                for (int i = 0; i < _timelineCount; i++)
                {
                    BehaviorRecipeTimelineViewModel timeline = _timelinePool[i];
                    key = key * 397L ^ StableHash(timeline.StageId);
                    key = key * 397L ^ timeline.TrackCount;
                    for (int trackIndex = 0; trackIndex < timeline.TrackCount; trackIndex++)
                    {
                        BehaviorRecipeEventViewModel recipeEvent = timeline.GetTrack(trackIndex)?.Event;
                        key = key * 397L ^ StableHash(recipeEvent?.StableKey);
                        key = key * 397L ^ (recipeEvent?.StartFrame ?? 0);
                        key = key * 397L ^ (recipeEvent?.DurationFrames ?? 0);
                    }
                }
                return key;
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; value != null && i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        private void EnsureOrderCapacity(int stageCount, int detectorCount)
        {
            if (_stageOrder.Length < stageCount)
                Array.Resize(ref _stageOrder, Math.Max(stageCount, _stageOrder.Length * 2 + 4));
            if (_detectorOrder.Length < detectorCount)
                Array.Resize(ref _detectorOrder, Math.Max(detectorCount, _detectorOrder.Length * 2 + 8));
        }

        private BehaviorRecipeTimelineViewModel GetOrCreateTimeline(int index)
        {
            while (_timelinePool.Count <= index)
                _timelinePool.Add(new BehaviorRecipeTimelineViewModel());
            return _timelinePool[index];
        }

        private BehaviorRecipeTrackViewModel GetOrCreateTrack()
        {
            while (_trackPool.Count <= _trackCount)
                _trackPool.Add(new BehaviorRecipeTrackViewModel());
            BehaviorRecipeTrackViewModel result = _trackPool[_trackCount++];
            result.Event = null;
            result.OwnsLaneBackground = false;
            return result;
        }

        private BehaviorRecipeEventViewModel GetOrCreateEvent()
        {
            while (_eventPool.Count <= _eventCount)
                _eventPool.Add(new BehaviorRecipeEventViewModel());
            return _eventPool[_eventCount++];
        }

        private static void ResetEvent(BehaviorRecipeEventViewModel recipeEvent)
        {
            recipeEvent.ResetTransitions();
            recipeEvent.StableKey = string.Empty;
            recipeEvent.Kind = BehaviorRecipeEventKind.None;
            recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Marker;
            recipeEvent.SelectionKind = CharacterTestRuntimeAiLabLogicElementKind.None;
            recipeEvent.SelectionIndex = -1;
            recipeEvent.StageIndex = -1;
            recipeEvent.DetectorIndex = -1;
            recipeEvent.StageId = string.Empty;
            recipeEvent.DetectorId = string.Empty;
            recipeEvent.EventId = string.Empty;
            recipeEvent.Title = string.Empty;
            recipeEvent.ConditionLabel = string.Empty;
            recipeEvent.Detail = string.Empty;
            recipeEvent.ResultLabel = string.Empty;
            recipeEvent.StartFrame = 0;
            recipeEvent.DurationFrames = 1;
            recipeEvent.Priority = 0;
            recipeEvent.CompiledEventIndex = -1;
            recipeEvent.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            recipeEvent.OperationKind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured;
            recipeEvent.CharacterActionEvent = false;
            recipeEvent.CharacterActionId = default;
            recipeEvent.CharacterActionDisplayName = string.Empty;
            recipeEvent.ProgressFrames = -1;
            recipeEvent.Selected = false;
            recipeEvent.Running = false;
            recipeEvent.Invalid = false;
            recipeEvent.DurationReviewRequired = false;
            recipeEvent.Shared = false;
            recipeEvent.CanMove = false;
            recipeEvent.CanResize = false;
        }

        private static string NormalizeEventId(string detectorId)
        {
            const string prefix = "event.";
            string value = detectorId ?? string.Empty;
            return value.StartsWith(prefix, StringComparison.Ordinal)
                ? value.Substring(prefix.Length)
                : value;
        }
    }
}
