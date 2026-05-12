using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public enum AbilityGraphExecutionFailureCode
    {
        None = 0,
        InvalidContext = 1,
        ValidationFailed = 2,
        MissingCaster = 3,
        MissingTarget = 4,
        MissingEffect = 5,
        StepBudgetExceeded = 6,
        PhaseGateInactive = 7,
        EffectApplicationFailed = 8,
        EventEmissionFailed = 9,
    }

    public readonly struct AbilityGraphExecutionTraceEntry
    {
        public AbilityGraphExecutionTraceEntry(
            int stepIndex,
            string nodeId,
            AbilityGraphNodeKind nodeKind,
            string outputPort,
            AbilityGraphExecutionFailureCode failureCode = AbilityGraphExecutionFailureCode.None,
            string message = null,
            int selectedTargetCount = 0,
            int emittedEventCount = 0,
            int effectId = 0,
            string phaseId = null)
        {
            StepIndex = stepIndex;
            NodeId = nodeId ?? string.Empty;
            NodeKind = nodeKind;
            OutputPort = outputPort ?? string.Empty;
            FailureCode = failureCode;
            Message = message ?? string.Empty;
            SelectedTargetCount = selectedTargetCount;
            EmittedEventCount = emittedEventCount;
            EffectId = effectId;
            PhaseId = phaseId ?? string.Empty;
        }

        public int StepIndex { get; }
        public string NodeId { get; }
        public AbilityGraphNodeKind NodeKind { get; }
        public string OutputPort { get; }
        public AbilityGraphExecutionFailureCode FailureCode { get; }
        public string Message { get; }
        public int SelectedTargetCount { get; }
        public int EmittedEventCount { get; }
        public int EffectId { get; }
        public string PhaseId { get; }
        public bool Succeeded => FailureCode == AbilityGraphExecutionFailureCode.None;
    }

    public sealed class AbilityGraphExecutionResult
    {
        private readonly IRuntimeEntity[] _selectedTargets;
        private readonly int[] _targetEntityIds;
        private readonly AbilityEvent[] _emittedEvents;
        private readonly AbilityGraphExecutionTraceEntry[] _trace;
        private readonly GameplayTargetRejectedTarget[] _rejectedTargets;

        internal AbilityGraphExecutionResult(
            bool succeeded,
            AbilityGraphExecutionFailureCode failureCode,
            string failureReason,
            int casterEntityId,
            int abilityId,
            IReadOnlyList<IRuntimeEntity> selectedTargets,
            IReadOnlyList<AbilityEvent> emittedEvents,
            IReadOnlyList<AbilityGraphExecutionTraceEntry> trace,
            IReadOnlyList<GameplayTargetRejectedTarget> rejectedTargets,
            AbilityGraphExecutionTrace executionTrace,
            AbilityGraphValidationResult validationResult)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            FailureReason = failureReason ?? string.Empty;
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            _selectedTargets = CopyEntities(selectedTargets);
            _targetEntityIds = BuildEntityIds(_selectedTargets);
            _emittedEvents = CopyEvents(emittedEvents);
            _trace = CopyTrace(trace);
            _rejectedTargets = CopyRejectedTargets(rejectedTargets);
            ExecutionTrace = executionTrace;
            ValidationResult = validationResult;
        }

        public bool Succeeded { get; }
        public AbilityGraphExecutionFailureCode FailureCode { get; }
        public string FailureReason { get; }
        public int CasterEntityId { get; }
        public int AbilityId { get; }
        public IReadOnlyList<IRuntimeEntity> SelectedTargets => _selectedTargets;
        public IReadOnlyList<int> TargetEntityIds => _targetEntityIds;
        public IReadOnlyList<AbilityEvent> EmittedEvents => _emittedEvents;
        public IReadOnlyList<AbilityGraphExecutionTraceEntry> Trace => _trace;
        public IReadOnlyList<GameplayTargetRejectedTarget> RejectedTargets => _rejectedTargets;
        public AbilityGraphExecutionTrace ExecutionTrace { get; }
        public AbilityGraphValidationResult ValidationResult { get; }

        private static IRuntimeEntity[] CopyEntities(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var copy = new IRuntimeEntity[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                copy[i] = entities[i];

            return copy;
        }

        private static int[] BuildEntityIds(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<int>();

            int[] ids = new int[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                ids[i] = entities[i] == null ? 0 : entities[i].EntityId;

            return ids;
        }

        private static AbilityEvent[] CopyEvents(IReadOnlyList<AbilityEvent> events)
        {
            if (events == null || events.Count == 0)
                return Array.Empty<AbilityEvent>();

            var copy = new AbilityEvent[events.Count];
            for (int i = 0; i < events.Count; i++)
                copy[i] = events[i];

            return copy;
        }

        private static AbilityGraphExecutionTraceEntry[] CopyTrace(IReadOnlyList<AbilityGraphExecutionTraceEntry> trace)
        {
            if (trace == null || trace.Count == 0)
                return Array.Empty<AbilityGraphExecutionTraceEntry>();

            var copy = new AbilityGraphExecutionTraceEntry[trace.Count];
            for (int i = 0; i < trace.Count; i++)
                copy[i] = trace[i];

            return copy;
        }

        private static GameplayTargetRejectedTarget[] CopyRejectedTargets(IReadOnlyList<GameplayTargetRejectedTarget> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<GameplayTargetRejectedTarget>();

            var copy = new GameplayTargetRejectedTarget[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                copy[i] = targets[i];

            return copy;
        }
    }
}
