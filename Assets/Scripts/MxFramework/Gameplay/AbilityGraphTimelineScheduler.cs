using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Gameplay
{
    public enum AbilityGraphTimelineAdvanceStatus
    {
        Unchanged = 0,
        Advanced = 1,
        Completed = 2,
        InvalidTimeline = 3,
        InvalidState = 4,
        InvalidStep = 5,
        StepBudgetExceeded = 6,
    }

    public sealed class AbilityGraphTimelineAdvanceResult
    {
        private readonly AbilityGraphPhaseTransition[] _transitions;
        private readonly ReadOnlyCollection<AbilityGraphPhaseTransition> _transitionsView;

        internal AbilityGraphTimelineAdvanceResult(
            AbilityGraphTimelineAdvanceStatus status,
            AbilityGraphTimelineState startState,
            AbilityGraphTimelineState endState,
            int requestedFrames,
            int consumedFrames,
            IReadOnlyList<AbilityGraphPhaseTransition> transitions)
        {
            Status = status;
            StartState = startState;
            EndState = endState;
            RequestedFrames = requestedFrames;
            ConsumedFrames = consumedFrames;

            if (transitions == null || transitions.Count == 0)
            {
                _transitions = Array.Empty<AbilityGraphPhaseTransition>();
                _transitionsView = Array.AsReadOnly(_transitions);
                return;
            }

            _transitions = new AbilityGraphPhaseTransition[transitions.Count];
            for (int i = 0; i < transitions.Count; i++)
                _transitions[i] = transitions[i];

            _transitionsView = Array.AsReadOnly(_transitions);
        }

        public AbilityGraphTimelineAdvanceStatus Status { get; }
        public AbilityGraphTimelineState StartState { get; }
        public AbilityGraphTimelineState EndState { get; }
        public int RequestedFrames { get; }
        public int ConsumedFrames { get; }
        public int TransitionCount => _transitions.Length;
        public IReadOnlyList<AbilityGraphPhaseTransition> Transitions => _transitionsView;
        public bool IsSuccess => Status == AbilityGraphTimelineAdvanceStatus.Unchanged
            || Status == AbilityGraphTimelineAdvanceStatus.Advanced
            || Status == AbilityGraphTimelineAdvanceStatus.Completed;
    }

    public static class AbilityGraphTimelineScheduler
    {
        public const int DefaultTransitionBudget = 128;

        public static AbilityGraphTimelineState CreateInitialState(AbilityGraphTimelineDefinition timeline)
        {
            if (timeline == null)
                return default;

            return new AbilityGraphTimelineState(timeline.TimelineId, timeline.EntryPhaseId, 0, 0, false);
        }

        public static AbilityGraphTimelineAdvanceResult Advance(
            AbilityGraphTimelineDefinition timeline,
            AbilityGraphTimelineState state,
            int deltaFrames,
            int transitionBudget = DefaultTransitionBudget)
        {
            if (timeline == null || !timeline.Validate().IsValid)
                return CreateResult(AbilityGraphTimelineAdvanceStatus.InvalidTimeline, state, state, deltaFrames, 0, null);

            if (deltaFrames < 0 || transitionBudget <= 0)
                return CreateResult(AbilityGraphTimelineAdvanceStatus.InvalidStep, state, state, deltaFrames, 0, null);

            if (!IsStateCompatible(timeline, state))
                return CreateResult(AbilityGraphTimelineAdvanceStatus.InvalidState, state, state, deltaFrames, 0, null);

            if (state.IsCompleted)
                return CreateResult(AbilityGraphTimelineAdvanceStatus.Completed, state, state, deltaFrames, 0, null);

            int framesRemaining = deltaFrames;
            int consumedFrames = 0;
            var transitions = new List<AbilityGraphPhaseTransition>();
            AbilityGraphTimelineState current = state;
            bool changed = false;

            while (!current.IsCompleted)
            {
                if (!timeline.TryGetPhase(current.CurrentPhaseId, out AbilityGraphPhaseDefinition phase))
                    return CreateResult(AbilityGraphTimelineAdvanceStatus.InvalidState, state, current, deltaFrames, consumedFrames, transitions);

                if (current.ElapsedFramesInPhase < 0
                    || current.TotalElapsedFrames < 0
                    || phase.DurationFrames < 0
                    || current.ElapsedFramesInPhase > phase.DurationFrames)
                {
                    return CreateResult(AbilityGraphTimelineAdvanceStatus.InvalidState, state, current, deltaFrames, consumedFrames, transitions);
                }

                int framesToBoundary = phase.DurationFrames - current.ElapsedFramesInPhase;
                if (framesToBoundary > framesRemaining)
                {
                    if (framesRemaining > 0)
                    {
                        current = current.WithElapsed(current.ElapsedFramesInPhase + framesRemaining, current.TotalElapsedFrames + framesRemaining);
                        consumedFrames += framesRemaining;
                        changed = true;
                    }

                    return CreateResult(changed ? AbilityGraphTimelineAdvanceStatus.Advanced : AbilityGraphTimelineAdvanceStatus.Unchanged, state, current, deltaFrames, consumedFrames, transitions);
                }

                if (framesToBoundary > 0)
                {
                    framesRemaining -= framesToBoundary;
                    consumedFrames += framesToBoundary;
                    current = current.WithElapsed(phase.DurationFrames, current.TotalElapsedFrames + framesToBoundary);
                    changed = true;
                }

                if (!phase.HasNextPhase)
                {
                    current = current.WithCompleted(phase.DurationFrames, current.TotalElapsedFrames);
                    return CreateResult(AbilityGraphTimelineAdvanceStatus.Completed, state, current, deltaFrames, consumedFrames, transitions);
                }

                if (transitions.Count >= transitionBudget)
                    return CreateResult(AbilityGraphTimelineAdvanceStatus.StepBudgetExceeded, state, current, deltaFrames, consumedFrames, transitions);

                if (!timeline.TryGetPhase(phase.NextPhaseId, out AbilityGraphPhaseDefinition nextPhase))
                    return CreateResult(AbilityGraphTimelineAdvanceStatus.InvalidTimeline, state, current, deltaFrames, consumedFrames, transitions);

                transitions.Add(new AbilityGraphPhaseTransition(current.CurrentPhaseId, nextPhase.PhaseId, current.TotalElapsedFrames, framesToBoundary));
                current = current.WithCurrentPhase(nextPhase.PhaseId, current.TotalElapsedFrames);
                changed = true;
            }

            return CreateResult(AbilityGraphTimelineAdvanceStatus.Completed, state, current, deltaFrames, consumedFrames, transitions);
        }

        private static bool IsStateCompatible(AbilityGraphTimelineDefinition timeline, AbilityGraphTimelineState state)
        {
            return string.Equals(timeline.TimelineId, state.TimelineId, StringComparison.Ordinal)
                && !state.CurrentPhaseId.IsEmpty
                && timeline.TryGetPhase(state.CurrentPhaseId, out _);
        }

        private static AbilityGraphTimelineAdvanceResult CreateResult(
            AbilityGraphTimelineAdvanceStatus status,
            AbilityGraphTimelineState startState,
            AbilityGraphTimelineState endState,
            int requestedFrames,
            int consumedFrames,
            IReadOnlyList<AbilityGraphPhaseTransition> transitions)
        {
            return new AbilityGraphTimelineAdvanceResult(status, startState, endState, requestedFrames, consumedFrames, transitions);
        }
    }
}
