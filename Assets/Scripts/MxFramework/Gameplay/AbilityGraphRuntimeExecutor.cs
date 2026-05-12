using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Deterministic executor for the v0 Ability Runtime Graph node set.</summary>
    public sealed class AbilityGraphRuntimeExecutor
    {
        public const int DefaultStepBudget = 256;
        public const string InvalidContextFailureReason = "InvalidContext";
        public const string ValidationFailedFailureReason = "ValidationFailed";
        public const string MissingCasterFailureReason = "MissingCaster";
        public const string MissingTargetFailureReason = "MissingTarget";
        public const string MissingEffectFailureReason = "MissingEffect";
        public const string StepBudgetExceededFailureReason = "StepBudgetExceeded";
        public const string PhaseGateInactiveFailureReason = "PhaseGateInactive";
        public const string EffectApplicationFailedFailureReason = "EffectApplicationFailed";
        public const string EventEmissionFailedFailureReason = "EventEmissionFailed";

        public AbilityGraphExecutionResult Execute(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context)
        {
            return Execute(graph, context, DefaultStepBudget);
        }

        public AbilityGraphExecutionResult Execute(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            int stepBudget)
        {
            AbilityGraphValidationResult validation = AbilityGraphValidator.Validate(graph);
            if (!validation.IsValid)
                return Fail(graph, context, AbilityGraphExecutionFailureCode.ValidationFailed, ValidationFailedFailureReason, null, validation);

            if (context == null)
                return Fail(graph, null, AbilityGraphExecutionFailureCode.InvalidContext, InvalidContextFailureReason, null, validation);

            if (stepBudget <= 0)
                return Fail(graph, context, AbilityGraphExecutionFailureCode.StepBudgetExceeded, StepBudgetExceededFailureReason, null, validation);

            if (!context.TryGetEntity(context.CasterEntityId, out IRuntimeEntity caster))
                return Fail(graph, context, AbilityGraphExecutionFailureCode.MissingCaster, MissingCasterFailureReason, null, validation);

            Dictionary<string, AbilityGraphNode> nodes = BuildNodeMap(graph.Nodes);
            var state = new ExecutionState(graph, context, caster);
            var pendingNodeIds = new List<string> { graph.EntryNodeId };
            int pendingIndex = 0;
            int executedSteps = 0;

            while (pendingIndex < pendingNodeIds.Count)
            {
                if (executedSteps >= stepBudget)
                    return Fail(graph, context, AbilityGraphExecutionFailureCode.StepBudgetExceeded, StepBudgetExceededFailureReason, state, validation);

                string nodeId = pendingNodeIds[pendingIndex++];
                if (!nodes.TryGetValue(nodeId, out AbilityGraphNode node))
                    return Fail(graph, context, AbilityGraphExecutionFailureCode.ValidationFailed, ValidationFailedFailureReason, state, validation);

                NodeExecutionOutcome outcome = ExecuteNode(graph, context, state, node, executedSteps);
                executedSteps++;

                if (!outcome.Succeeded)
                    return Fail(graph, context, outcome.FailureCode, outcome.FailureReason, state, validation);

                IReadOnlyList<string> nextNodeIds = outcome.NextNodeIds;
                for (int i = 0; i < nextNodeIds.Count; i++)
                    pendingNodeIds.Add(nextNodeIds[i]);
            }

            return new AbilityGraphExecutionResult(
                succeeded: true,
                failureCode: AbilityGraphExecutionFailureCode.None,
                failureReason: null,
                casterEntityId: context.CasterEntityId,
                abilityId: context.AbilityId,
                selectedTargets: state.SelectedTargets,
                emittedEvents: state.EmittedEvents,
                trace: state.Trace,
                rejectedTargets: state.RejectedTargets,
                executionTrace: state.BuildExecutionTrace(AbilityGraphExecutionFailureCode.None, null),
                validationResult: validation);
        }

        private static NodeExecutionOutcome ExecuteNode(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex)
        {
            switch (node.Kind)
            {
                case AbilityGraphNodeKind.Entry:
                case AbilityGraphNodeKind.Sequence:
                    return Complete(graph, state, node, stepIndex, AbilityGraphPorts.Next);

                case AbilityGraphNodeKind.TargetQuery:
                    return ExecuteTargetQuery(graph, context, state, node, stepIndex);

                case AbilityGraphNodeKind.ApplyEffect:
                    return ExecuteApplyEffect(graph, context, state, node, stepIndex);

                case AbilityGraphNodeKind.EmitEvent:
                    return ExecuteEmitEvent(graph, context, state, node, stepIndex);

                case AbilityGraphNodeKind.PhaseGate:
                    return ExecutePhaseGate(graph, context, state, node, stepIndex);

                default:
                    return CompleteFailure(
                        state,
                        node,
                        stepIndex,
                        AbilityGraphExecutionFailureCode.ValidationFailed,
                        ValidationFailedFailureReason);
            }
        }

        private static NodeExecutionOutcome ExecuteTargetQuery(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex)
        {
            var payload = node.Payload as AbilityGraphTargetQueryPayload;
            if (payload == null)
                return CompleteFailure(state, node, stepIndex, AbilityGraphExecutionFailureCode.ValidationFailed, ValidationFailedFailureReason);

            var query = new GameplayTargetQuery(
                state.Caster.EntityId,
                state.Caster.TeamId,
                payload.RequireAlive,
                payload.RelationFilter,
                payload.RequiredTags,
                payload.BlockedStatuses,
                payload.MaxTargets);

            GameplayTargetingResult targetingResult = context.HasExplicitTargetCandidates
                ? context.TargetingService.Select(query, context.TargetCandidates)
                : context.TargetingService.Select(query, context.BuildTargetingRuntimeCandidates());

            state.RecordTargetDecisions(node.NodeId, targetingResult);
            state.AddRejectedTargets(targetingResult.RejectedTargets);

            IReadOnlyList<IRuntimeEntity> selectedTargets = ResolveSelectedTargets(context, targetingResult.SelectedTargets);
            if (targetingResult.HasTargets && selectedTargets.Count == 0)
            {
                return CompleteFailure(
                    state,
                    node,
                    stepIndex,
                    AbilityGraphExecutionFailureCode.MissingTarget,
                    MissingTargetFailureReason);
            }

            state.ReplaceSelectedTargets(selectedTargets);

            if (selectedTargets.Count == 0)
            {
                state.LastBranchFailureReason = MissingTargetFailureReason;
                if (!HasOutgoingEdge(graph.Edges, node.NodeId, AbilityGraphPorts.Failure))
                {
                    return CompleteFailure(
                        state,
                        node,
                        stepIndex,
                        AbilityGraphExecutionFailureCode.MissingTarget,
                        MissingTargetFailureReason,
                        AbilityGraphPorts.Failure);
                }

                return Complete(graph, state, node, stepIndex, AbilityGraphPorts.Failure, MissingTargetFailureReason);
            }

            string outputPort = HasOutgoingEdge(graph.Edges, node.NodeId, AbilityGraphPorts.Success)
                ? AbilityGraphPorts.Success
                : AbilityGraphPorts.Next;
            state.LastBranchFailureReason = null;
            return Complete(graph, state, node, stepIndex, outputPort);
        }

        private static NodeExecutionOutcome ExecuteApplyEffect(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex)
        {
            var payload = node.Payload as AbilityGraphApplyEffectPayload;
            if (payload == null)
                return CompleteFailure(state, node, stepIndex, AbilityGraphExecutionFailureCode.ValidationFailed, ValidationFailedFailureReason);

            if (state.SelectedTargets.Count == 0)
            {
                return CompleteFailure(
                    state,
                    node,
                    stepIndex,
                    AbilityGraphExecutionFailureCode.MissingTarget,
                    MissingTargetFailureReason,
                    effectId: payload.EffectId);
            }

            if (context.EffectResolver == null ||
                !context.EffectResolver.TryResolve(payload.EffectId, out IAbilityEffect effect) ||
                effect == null)
            {
                return CompleteFailure(
                    state,
                    node,
                    stepIndex,
                    AbilityGraphExecutionFailureCode.MissingEffect,
                    MissingEffectFailureReason,
                    effectId: payload.EffectId);
            }

            var abilityContext = new AbilityContext(state.Caster, state.AbilityContextCandidates);
            for (int i = 0; i < state.SelectedTargets.Count; i++)
            {
                IRuntimeEntity target = state.SelectedTargets[i];
                if (target == null)
                {
                    return CompleteFailure(
                        state,
                        node,
                        stepIndex,
                        AbilityGraphExecutionFailureCode.MissingTarget,
                        MissingTargetFailureReason,
                        effectId: payload.EffectId);
                }

                try
                {
                    effect.Apply(abilityContext, target);
                }
                catch (Exception exception)
                {
                    return CompleteFailure(
                        state,
                        node,
                        stepIndex,
                        AbilityGraphExecutionFailureCode.EffectApplicationFailed,
                        EffectApplicationFailedFailureReason + ":" + exception.GetType().Name,
                        effectId: payload.EffectId);
                }
            }

            return Complete(graph, state, node, stepIndex, AbilityGraphPorts.Next, effectId: payload.EffectId);
        }

        private static NodeExecutionOutcome ExecuteEmitEvent(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex)
        {
            var payload = node.Payload as AbilityGraphEmitEventPayload;
            if (payload == null)
                return CompleteFailure(state, node, stepIndex, AbilityGraphExecutionFailureCode.ValidationFailed, ValidationFailedFailureReason);

            int beforeCount = state.EmittedEvents.Count;
            try
            {
                if (IsTargetEvent(payload.EventType))
                {
                    if (state.SelectedTargets.Count == 0)
                    {
                        return CompleteFailure(
                            state,
                            node,
                            stepIndex,
                            AbilityGraphExecutionFailureCode.MissingTarget,
                            MissingTargetFailureReason);
                    }

                    for (int i = 0; i < state.SelectedTargets.Count; i++)
                        state.Emit(context, node.NodeId, new AbilityEvent(payload.EventType, context.AbilityId, state.Caster, state.SelectedTargets[i]));
                }
                else
                {
                    string failureReason = payload.EventType == AbilityEventType.CastFailed
                        ? state.LastBranchFailureReason
                        : null;
                    state.Emit(context, node.NodeId, new AbilityEvent(payload.EventType, context.AbilityId, state.Caster, failureReason: failureReason));
                }
            }
            catch (Exception exception)
            {
                return CompleteFailure(
                    state,
                    node,
                    stepIndex,
                    AbilityGraphExecutionFailureCode.EventEmissionFailed,
                    EventEmissionFailedFailureReason + ":" + exception.GetType().Name,
                    emittedEventCount: state.EmittedEvents.Count - beforeCount);
            }

            return Complete(
                graph,
                state,
                node,
                stepIndex,
                AbilityGraphPorts.Next,
                emittedEventCount: state.EmittedEvents.Count - beforeCount);
        }

        private static NodeExecutionOutcome ExecutePhaseGate(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex)
        {
            var payload = node.Payload as AbilityGraphPhaseGatePayload;
            if (payload == null)
                return CompleteFailure(state, node, stepIndex, AbilityGraphExecutionFailureCode.ValidationFailed, ValidationFailedFailureReason);

            bool isActive = context.PhaseGate == null || context.PhaseGate.IsPhaseActive(payload.PhaseId);
            if (isActive)
            {
                string outputPort = HasOutgoingEdge(graph.Edges, node.NodeId, AbilityGraphPorts.Success)
                    ? AbilityGraphPorts.Success
                    : AbilityGraphPorts.Next;
                return Complete(graph, state, node, stepIndex, outputPort, phaseId: payload.PhaseId);
            }

            state.LastBranchFailureReason = PhaseGateInactiveFailureReason;
            if (!HasOutgoingEdge(graph.Edges, node.NodeId, AbilityGraphPorts.Failure))
            {
                return CompleteFailure(
                    state,
                    node,
                    stepIndex,
                    AbilityGraphExecutionFailureCode.PhaseGateInactive,
                    PhaseGateInactiveFailureReason,
                    AbilityGraphPorts.Failure,
                    phaseId: payload.PhaseId);
            }

            return Complete(graph, state, node, stepIndex, AbilityGraphPorts.Failure, PhaseGateInactiveFailureReason, phaseId: payload.PhaseId);
        }

        private static NodeExecutionOutcome Complete(
            AbilityGraphDefinition graph,
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex,
            string outputPort,
            string message = null,
            int emittedEventCount = 0,
            int effectId = 0,
            string phaseId = null)
        {
            state.Trace.Add(new AbilityGraphExecutionTraceEntry(
                stepIndex,
                node.NodeId,
                node.Kind,
                outputPort,
                AbilityGraphExecutionFailureCode.None,
                message,
                state.SelectedTargets.Count,
                emittedEventCount,
                effectId,
                phaseId));
            state.TraceBuilder.RecordNode(node, outputPort, "Ok", message);

            return NodeExecutionOutcome.Success(CollectOutgoingNodeIds(graph.Edges, node.NodeId, outputPort));
        }

        private static NodeExecutionOutcome CompleteFailure(
            ExecutionState state,
            AbilityGraphNode node,
            int stepIndex,
            AbilityGraphExecutionFailureCode failureCode,
            string failureReason,
            string outputPort = null,
            int emittedEventCount = 0,
            int effectId = 0,
            string phaseId = null)
        {
            state.Trace.Add(new AbilityGraphExecutionTraceEntry(
                stepIndex,
                node.NodeId,
                node.Kind,
                outputPort,
                failureCode,
                failureReason,
                state.SelectedTargets.Count,
                emittedEventCount,
                effectId,
                phaseId));
            state.TraceBuilder.RecordNode(node, outputPort, failureCode.ToString(), failureReason);

            return NodeExecutionOutcome.Fail(failureCode, failureReason);
        }

        private static AbilityGraphExecutionResult Fail(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionContext context,
            AbilityGraphExecutionFailureCode failureCode,
            string failureReason,
            ExecutionState state,
            AbilityGraphValidationResult validation)
        {
            AbilityGraphExecutionTrace executionTrace = state == null
                ? BuildFailureTrace(graph, failureCode, failureReason)
                : state.BuildExecutionTrace(failureCode, failureReason);

            return new AbilityGraphExecutionResult(
                succeeded: false,
                failureCode: failureCode,
                failureReason: failureReason,
                casterEntityId: context == null ? 0 : context.CasterEntityId,
                abilityId: context == null ? 0 : context.AbilityId,
                selectedTargets: state == null ? null : state.SelectedTargets,
                emittedEvents: state == null ? null : state.EmittedEvents,
                trace: state == null ? null : state.Trace,
                rejectedTargets: state == null ? null : state.RejectedTargets,
                executionTrace: executionTrace,
                validationResult: validation);
        }

        private static AbilityGraphExecutionTrace BuildFailureTrace(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionFailureCode failureCode,
            string failureReason)
        {
            var builder = new AbilityGraphExecutionTraceBuilder(graph);
            AbilityGraphTraceFailureCode traceFailureCode = ToTraceFailureCode(failureCode);
            if (traceFailureCode != AbilityGraphTraceFailureCode.None)
                builder.Fail(traceFailureCode, failureReason, null);

            return builder.Build();
        }

        private static AbilityGraphTraceFailureCode ToTraceFailureCode(AbilityGraphExecutionFailureCode failureCode)
        {
            switch (failureCode)
            {
                case AbilityGraphExecutionFailureCode.None:
                    return AbilityGraphTraceFailureCode.None;
                case AbilityGraphExecutionFailureCode.ValidationFailed:
                    return AbilityGraphTraceFailureCode.ValidationFailed;
                case AbilityGraphExecutionFailureCode.MissingTarget:
                    return AbilityGraphTraceFailureCode.TargetRejected;
                case AbilityGraphExecutionFailureCode.MissingEffect:
                    return AbilityGraphTraceFailureCode.MissingEffect;
                case AbilityGraphExecutionFailureCode.EffectApplicationFailed:
                case AbilityGraphExecutionFailureCode.EventEmissionFailed:
                case AbilityGraphExecutionFailureCode.PhaseGateInactive:
                    return AbilityGraphTraceFailureCode.NodeFailed;
                default:
                    return AbilityGraphTraceFailureCode.ExecutionFailed;
            }
        }

        private static Dictionary<string, AbilityGraphNode> BuildNodeMap(IReadOnlyList<AbilityGraphNode> nodes)
        {
            var map = new Dictionary<string, AbilityGraphNode>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                AbilityGraphNode node = nodes[i];
                if (!map.ContainsKey(node.NodeId))
                    map.Add(node.NodeId, node);
            }

            return map;
        }

        private static IReadOnlyList<IRuntimeEntity> ResolveSelectedTargets(
            AbilityGraphExecutionContext context,
            IReadOnlyList<GameplayTargetCandidate> selectedTargets)
        {
            if (selectedTargets == null || selectedTargets.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var resolvedTargets = new List<IRuntimeEntity>(selectedTargets.Count);
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                if (context.TryResolveTargetCandidate(selectedTargets[i], out IRuntimeEntity entity))
                    resolvedTargets.Add(entity);
            }

            return resolvedTargets.Count == 0 ? Array.Empty<IRuntimeEntity>() : resolvedTargets.ToArray();
        }

        private static bool HasOutgoingEdge(
            IReadOnlyList<AbilityGraphEdge> edges,
            string nodeId,
            string outputPort)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                AbilityGraphEdge edge = edges[i];
                if (string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.OutputPort, outputPort, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> CollectOutgoingNodeIds(
            IReadOnlyList<AbilityGraphEdge> edges,
            string nodeId,
            string outputPort)
        {
            var nodeIds = new List<string>();
            for (int i = 0; i < edges.Count; i++)
            {
                AbilityGraphEdge edge = edges[i];
                if (string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.OutputPort, outputPort, StringComparison.Ordinal))
                {
                    nodeIds.Add(edge.ToNodeId);
                }
            }

            return nodeIds.Count == 0 ? Array.Empty<string>() : nodeIds.ToArray();
        }

        private static bool IsTargetEvent(AbilityEventType eventType)
        {
            return eventType == AbilityEventType.TargetSelected ||
                eventType == AbilityEventType.EffectApplied;
        }

        private sealed class ExecutionState
        {
            private readonly List<IRuntimeEntity> _selectedTargets = new List<IRuntimeEntity>();
            private readonly List<AbilityEvent> _emittedEvents = new List<AbilityEvent>();
            private readonly List<AbilityGraphExecutionTraceEntry> _trace = new List<AbilityGraphExecutionTraceEntry>();
            private readonly List<GameplayTargetRejectedTarget> _rejectedTargets = new List<GameplayTargetRejectedTarget>();

            public ExecutionState(AbilityGraphDefinition graph, AbilityGraphExecutionContext context, IRuntimeEntity caster)
            {
                Caster = caster;
                AbilityContextCandidates = context.BuildAbilityContextCandidates();
                TraceBuilder = new AbilityGraphExecutionTraceBuilder(graph);
            }

            public IRuntimeEntity Caster { get; }
            public IReadOnlyList<IRuntimeEntity> AbilityContextCandidates { get; }
            public IReadOnlyList<IRuntimeEntity> SelectedTargets => _selectedTargets;
            public IReadOnlyList<AbilityEvent> EmittedEvents => _emittedEvents;
            public List<AbilityGraphExecutionTraceEntry> Trace => _trace;
            public IReadOnlyList<GameplayTargetRejectedTarget> RejectedTargets => _rejectedTargets;
            public AbilityGraphExecutionTraceBuilder TraceBuilder { get; }
            public string LastBranchFailureReason { get; set; }

            public void ReplaceSelectedTargets(IReadOnlyList<IRuntimeEntity> targets)
            {
                _selectedTargets.Clear();
                if (targets == null)
                    return;

                for (int i = 0; i < targets.Count; i++)
                    _selectedTargets.Add(targets[i]);
            }

            public void AddRejectedTargets(IReadOnlyList<GameplayTargetRejectedTarget> targets)
            {
                if (targets == null)
                    return;

                for (int i = 0; i < targets.Count; i++)
                    _rejectedTargets.Add(targets[i]);
            }

            public void RecordTargetDecisions(string nodeId, GameplayTargetingResult result)
            {
                if (result == null)
                    return;

                IReadOnlyList<GameplayTargetCandidate> selectedTargets = result.SelectedTargets;
                for (int i = 0; i < selectedTargets.Count; i++)
                    TraceBuilder.RecordTargetDecision(nodeId, selectedTargets[i].EntityId, true);

                IReadOnlyList<GameplayTargetRejectedTarget> rejectedTargets = result.RejectedTargets;
                for (int i = 0; i < rejectedTargets.Count; i++)
                {
                    GameplayTargetRejectedTarget target = rejectedTargets[i];
                    TraceBuilder.RecordTargetRejected(
                        nodeId,
                        target.EntityId,
                        target.Reason,
                        target.DetailId,
                        target.Label);
                }
            }

            public void Emit(AbilityGraphExecutionContext context, string nodeId, AbilityEvent evt)
            {
                _emittedEvents.Add(evt);
                TraceBuilder.RecordEvent(
                    nodeId,
                    evt.Type,
                    evt.AbilityId,
                    evt.Caster == null ? 0 : evt.Caster.EntityId,
                    evt.Target == null ? 0 : evt.Target.EntityId,
                    evt.FailureReason);

                if (context.EventSink != null)
                    context.EventSink.Publish(evt);
            }

            public AbilityGraphExecutionTrace BuildExecutionTrace(
                AbilityGraphExecutionFailureCode failureCode,
                string failureReason)
            {
                if (failureCode != AbilityGraphExecutionFailureCode.None)
                {
                    string nodeId = _trace.Count == 0 ? null : _trace[_trace.Count - 1].NodeId;
                    TraceBuilder.Fail(ToTraceFailureCode(failureCode), failureReason, nodeId);
                }

                return TraceBuilder.Build();
            }
        }

        private readonly struct NodeExecutionOutcome
        {
            private NodeExecutionOutcome(
                bool succeeded,
                IReadOnlyList<string> nextNodeIds,
                AbilityGraphExecutionFailureCode failureCode,
                string failureReason)
            {
                Succeeded = succeeded;
                NextNodeIds = nextNodeIds ?? Array.Empty<string>();
                FailureCode = failureCode;
                FailureReason = failureReason ?? string.Empty;
            }

            public bool Succeeded { get; }
            public IReadOnlyList<string> NextNodeIds { get; }
            public AbilityGraphExecutionFailureCode FailureCode { get; }
            public string FailureReason { get; }

            public static NodeExecutionOutcome Success(IReadOnlyList<string> nextNodeIds)
            {
                return new NodeExecutionOutcome(true, nextNodeIds, AbilityGraphExecutionFailureCode.None, null);
            }

            public static NodeExecutionOutcome Fail(AbilityGraphExecutionFailureCode failureCode, string failureReason)
            {
                return new NodeExecutionOutcome(false, null, failureCode, failureReason);
            }
        }
    }
}
