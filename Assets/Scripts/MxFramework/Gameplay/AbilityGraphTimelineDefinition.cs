using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Gameplay
{
    public sealed class AbilityGraphTimelineDefinition
    {
        private readonly AbilityGraphPhaseDefinition[] _phases;
        private readonly ReadOnlyCollection<AbilityGraphPhaseDefinition> _phasesView;

        public AbilityGraphTimelineDefinition(
            string timelineId,
            string entryPhaseId,
            IReadOnlyList<AbilityGraphPhaseDefinition> phases)
            : this(timelineId, new AbilityGraphPhaseId(entryPhaseId), phases)
        {
        }

        public AbilityGraphTimelineDefinition(
            string timelineId,
            AbilityGraphPhaseId entryPhaseId,
            IReadOnlyList<AbilityGraphPhaseDefinition> phases)
        {
            TimelineId = timelineId ?? string.Empty;
            EntryPhaseId = entryPhaseId;
            _phases = CopyPhases(phases);
            _phasesView = Array.AsReadOnly(_phases);
        }

        public string TimelineId { get; }
        public AbilityGraphPhaseId EntryPhaseId { get; }
        public IReadOnlyList<AbilityGraphPhaseDefinition> Phases => _phasesView;

        public AbilityGraphTimelineValidationResult Validate()
        {
            return AbilityGraphTimelineValidator.Validate(this);
        }

        public bool TryGetPhase(string phaseId, out AbilityGraphPhaseDefinition phase)
        {
            return TryGetPhase(new AbilityGraphPhaseId(phaseId), out phase, out _);
        }

        public bool TryGetPhase(AbilityGraphPhaseId phaseId, out AbilityGraphPhaseDefinition phase)
        {
            return TryGetPhase(phaseId, out phase, out _);
        }

        public bool TryGetPhase(AbilityGraphPhaseId phaseId, out AbilityGraphPhaseDefinition phase, out int phaseIndex)
        {
            for (int i = 0; i < _phases.Length; i++)
            {
                if (_phases[i].PhaseId == phaseId)
                {
                    phase = _phases[i];
                    phaseIndex = i;
                    return true;
                }
            }

            phase = default;
            phaseIndex = -1;
            return false;
        }

        private static AbilityGraphPhaseDefinition[] CopyPhases(IReadOnlyList<AbilityGraphPhaseDefinition> phases)
        {
            if (phases == null || phases.Count == 0)
                return Array.Empty<AbilityGraphPhaseDefinition>();

            var copy = new AbilityGraphPhaseDefinition[phases.Count];
            for (int i = 0; i < phases.Count; i++)
                copy[i] = phases[i];

            return copy;
        }
    }

    public enum AbilityGraphTimelineValidationErrorCode
    {
        NullTimelineDefinition = 0,
        EmptyPhaseId = 1,
        DuplicatePhaseId = 2,
        MissingEntryPhase = 3,
        InvalidPhaseReference = 4,
        NegativePhaseDuration = 5,
        CycleDetected = 6,
    }

    public readonly struct AbilityGraphTimelineValidationError
    {
        public AbilityGraphTimelineValidationError(
            AbilityGraphTimelineValidationErrorCode code,
            string message,
            string phaseId = null,
            string referencedPhaseId = null,
            int phaseIndex = -1,
            string fieldPath = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            PhaseId = phaseId ?? string.Empty;
            ReferencedPhaseId = referencedPhaseId ?? string.Empty;
            PhaseIndex = phaseIndex;
            FieldPath = fieldPath ?? string.Empty;
        }

        public AbilityGraphTimelineValidationErrorCode Code { get; }
        public string Message { get; }
        public string PhaseId { get; }
        public string ReferencedPhaseId { get; }
        public int PhaseIndex { get; }
        public string FieldPath { get; }
    }

    public sealed class AbilityGraphTimelineValidationResult
    {
        private readonly AbilityGraphTimelineValidationError[] _errors;
        private readonly ReadOnlyCollection<AbilityGraphTimelineValidationError> _errorsView;

        public AbilityGraphTimelineValidationResult(IReadOnlyList<AbilityGraphTimelineValidationError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                _errors = Array.Empty<AbilityGraphTimelineValidationError>();
                _errorsView = Array.AsReadOnly(_errors);
                return;
            }

            _errors = new AbilityGraphTimelineValidationError[errors.Count];
            for (int i = 0; i < errors.Count; i++)
                _errors[i] = errors[i];

            _errorsView = Array.AsReadOnly(_errors);
        }

        public bool IsValid => _errors.Length == 0;
        public int ErrorCount => _errors.Length;
        public IReadOnlyList<AbilityGraphTimelineValidationError> Errors => _errorsView;

        public bool Contains(AbilityGraphTimelineValidationErrorCode code)
        {
            for (int i = 0; i < _errors.Length; i++)
            {
                if (_errors[i].Code == code)
                    return true;
            }

            return false;
        }
    }

    public static class AbilityGraphTimelineValidator
    {
        public static AbilityGraphTimelineValidationResult Validate(AbilityGraphTimelineDefinition timeline)
        {
            var errors = new List<AbilityGraphTimelineValidationError>();
            if (timeline == null)
            {
                Add(errors, AbilityGraphTimelineValidationErrorCode.NullTimelineDefinition, "Ability graph timeline definition is null.");
                return new AbilityGraphTimelineValidationResult(errors);
            }

            Dictionary<AbilityGraphPhaseId, int> phaseIndexes = BuildPhaseIndex(timeline.Phases, errors);
            ValidateEntryPhase(timeline.EntryPhaseId, phaseIndexes, errors);
            ValidatePhaseReferences(timeline.Phases, phaseIndexes, errors);
            ValidateAcyclic(timeline.Phases, phaseIndexes, errors);
            return new AbilityGraphTimelineValidationResult(errors);
        }

        private static Dictionary<AbilityGraphPhaseId, int> BuildPhaseIndex(
            IReadOnlyList<AbilityGraphPhaseDefinition> phases,
            List<AbilityGraphTimelineValidationError> errors)
        {
            var phaseIndexes = new Dictionary<AbilityGraphPhaseId, int>();
            var reportedDuplicates = new HashSet<AbilityGraphPhaseId>();

            for (int i = 0; i < phases.Count; i++)
            {
                AbilityGraphPhaseDefinition phase = phases[i];
                if (phase.PhaseId.IsEmpty)
                {
                    Add(errors, AbilityGraphTimelineValidationErrorCode.EmptyPhaseId, "Ability graph phase id cannot be empty.", phaseIndex: i, fieldPath: "Phases[" + i + "].PhaseId");
                }
                else if (phaseIndexes.ContainsKey(phase.PhaseId))
                {
                    if (reportedDuplicates.Add(phase.PhaseId))
                        Add(errors, AbilityGraphTimelineValidationErrorCode.DuplicatePhaseId, "Ability graph phase id is duplicated: " + phase.PhaseId.Value + ".", phase.PhaseId.Value, phaseIndex: i, fieldPath: "Phases[" + i + "].PhaseId");
                }
                else
                {
                    phaseIndexes.Add(phase.PhaseId, i);
                }

                if (phase.DurationFrames < 0)
                    Add(errors, AbilityGraphTimelineValidationErrorCode.NegativePhaseDuration, "Ability graph phase duration cannot be negative.", phase.PhaseId.Value, phaseIndex: i, fieldPath: FieldPath(phase.PhaseId, i, "DurationFrames"));
            }

            return phaseIndexes;
        }

        private static void ValidateEntryPhase(
            AbilityGraphPhaseId entryPhaseId,
            Dictionary<AbilityGraphPhaseId, int> phaseIndexes,
            List<AbilityGraphTimelineValidationError> errors)
        {
            if (entryPhaseId.IsEmpty)
            {
                Add(errors, AbilityGraphTimelineValidationErrorCode.MissingEntryPhase, "Ability graph timeline entry phase id is missing.", fieldPath: "EntryPhaseId");
                return;
            }

            if (!phaseIndexes.ContainsKey(entryPhaseId))
                Add(errors, AbilityGraphTimelineValidationErrorCode.MissingEntryPhase, "Ability graph timeline entry phase does not exist: " + entryPhaseId.Value + ".", entryPhaseId.Value, fieldPath: "EntryPhaseId");
        }

        private static void ValidatePhaseReferences(
            IReadOnlyList<AbilityGraphPhaseDefinition> phases,
            Dictionary<AbilityGraphPhaseId, int> phaseIndexes,
            List<AbilityGraphTimelineValidationError> errors)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                AbilityGraphPhaseDefinition phase = phases[i];
                if (!phase.HasNextPhase)
                    continue;

                if (phase.NextPhaseId.IsEmpty || !phaseIndexes.ContainsKey(phase.NextPhaseId))
                {
                    Add(
                        errors,
                        AbilityGraphTimelineValidationErrorCode.InvalidPhaseReference,
                        "Ability graph phase references missing next phase: " + phase.NextPhaseId.Value + ".",
                        phase.PhaseId.Value,
                        phase.NextPhaseId.Value,
                        i,
                        FieldPath(phase.PhaseId, i, "NextPhaseId"));
                }
            }
        }

        private static void ValidateAcyclic(
            IReadOnlyList<AbilityGraphPhaseDefinition> phases,
            Dictionary<AbilityGraphPhaseId, int> phaseIndexes,
            List<AbilityGraphTimelineValidationError> errors)
        {
            if (phases.Count == 0)
                return;

            var state = new int[phases.Count];
            for (int i = 0; i < phases.Count; i++)
            {
                if (state[i] != 0 || !IsCanonicalPhase(phases[i], i, phaseIndexes))
                    continue;

                if (TryFindCycle(i, phases, phaseIndexes, state, errors))
                    return;
            }
        }

        private static bool TryFindCycle(
            int phaseIndex,
            IReadOnlyList<AbilityGraphPhaseDefinition> phases,
            Dictionary<AbilityGraphPhaseId, int> phaseIndexes,
            int[] state,
            List<AbilityGraphTimelineValidationError> errors)
        {
            state[phaseIndex] = 1;
            AbilityGraphPhaseDefinition phase = phases[phaseIndex];
            if (phase.HasNextPhase && phaseIndexes.TryGetValue(phase.NextPhaseId, out int nextPhaseIndex))
            {
                if (state[nextPhaseIndex] == 1)
                {
                    AbilityGraphPhaseDefinition cyclePhase = phases[nextPhaseIndex];
                    Add(errors, AbilityGraphTimelineValidationErrorCode.CycleDetected, "Ability graph timeline contains a phase cycle.", cyclePhase.PhaseId.Value, phase.PhaseId.Value, nextPhaseIndex, FieldPath(phase.PhaseId, phaseIndex, "NextPhaseId"));
                    return true;
                }

                if (state[nextPhaseIndex] == 0 && TryFindCycle(nextPhaseIndex, phases, phaseIndexes, state, errors))
                    return true;
            }

            state[phaseIndex] = 2;
            return false;
        }

        private static bool IsCanonicalPhase(
            AbilityGraphPhaseDefinition phase,
            int phaseIndex,
            Dictionary<AbilityGraphPhaseId, int> phaseIndexes)
        {
            return !phase.PhaseId.IsEmpty
                && phaseIndexes.TryGetValue(phase.PhaseId, out int firstIndex)
                && firstIndex == phaseIndex;
        }

        private static string FieldPath(AbilityGraphPhaseId phaseId, int phaseIndex, string member)
        {
            return phaseId.IsEmpty
                ? "Phases[" + phaseIndex + "]." + member
                : "Phases[" + phaseId.Value + "]." + member;
        }

        private static void Add(
            List<AbilityGraphTimelineValidationError> errors,
            AbilityGraphTimelineValidationErrorCode code,
            string message,
            string phaseId = null,
            string referencedPhaseId = null,
            int phaseIndex = -1,
            string fieldPath = null)
        {
            errors.Add(new AbilityGraphTimelineValidationError(code, message, phaseId, referencedPhaseId, phaseIndex, fieldPath));
        }
    }
}
