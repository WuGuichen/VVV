namespace MxFramework.Gameplay
{
    public static class AbilityGraphPhaseGate
    {
        public static bool IsOpen(
            AbilityGraphTimelineDefinition timeline,
            AbilityGraphTimelineState state,
            AbilityGraphPhaseGatePayload payload)
        {
            return payload != null && HasReachedPhase(timeline, state, payload.PhaseId);
        }

        public static bool HasReachedPhase(
            AbilityGraphTimelineDefinition timeline,
            AbilityGraphTimelineState state,
            string phaseId)
        {
            return HasReachedPhase(timeline, state, new AbilityGraphPhaseId(phaseId));
        }

        public static bool HasReachedPhase(
            AbilityGraphTimelineDefinition timeline,
            AbilityGraphTimelineState state,
            AbilityGraphPhaseId phaseId)
        {
            if (timeline == null || phaseId.IsEmpty || state.CurrentPhaseId.IsEmpty)
                return false;

            AbilityGraphPhaseId currentPhaseId = timeline.EntryPhaseId;
            int remainingSteps = timeline.Phases.Count + 1;
            while (remainingSteps-- > 0)
            {
                if (currentPhaseId == phaseId)
                    return true;

                if (!timeline.TryGetPhase(currentPhaseId, out AbilityGraphPhaseDefinition currentPhase))
                    return false;

                if (!state.IsCompleted && currentPhaseId == state.CurrentPhaseId)
                    return false;

                if (!currentPhase.HasNextPhase)
                    return false;

                currentPhaseId = currentPhase.NextPhaseId;
            }

            return false;
        }
    }

    /// <summary>Adapts a deterministic ability graph timeline state into the executor phase gate interface.</summary>
    public sealed class AbilityGraphTimelinePhaseGate : IAbilityGraphPhaseGate
    {
        private readonly AbilityGraphTimelineDefinition _timeline;
        private readonly AbilityGraphTimelineState _state;

        public AbilityGraphTimelinePhaseGate(
            AbilityGraphTimelineDefinition timeline,
            AbilityGraphTimelineState state)
        {
            _timeline = timeline;
            _state = state;
        }

        public AbilityGraphTimelineDefinition Timeline => _timeline;
        public AbilityGraphTimelineState State => _state;

        public bool IsPhaseActive(string phaseId)
        {
            return AbilityGraphPhaseGate.HasReachedPhase(_timeline, _state, phaseId);
        }

        public AbilityGraphTimelinePhaseGate WithState(AbilityGraphTimelineState state)
        {
            return new AbilityGraphTimelinePhaseGate(_timeline, state);
        }
    }
}
