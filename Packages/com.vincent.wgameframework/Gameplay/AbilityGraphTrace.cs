using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MxFramework.Gameplay
{
    public enum AbilityGraphTraceFailureCode
    {
        None = 0,
        ValidationFailed = 1,
        TargetRejected = 2,
        MissingEffect = 3,
        NodeFailed = 4,
        ExecutionFailed = 5,
    }

    /// <summary>Ordered, immutable trace of an ability graph execution attempt.</summary>
    public sealed class AbilityGraphExecutionTrace
    {
        private readonly AbilityGraphNodeTraceEntry[] _nodes;
        private readonly AbilityGraphTargetTraceEntry[] _targetDecisions;
        private readonly AbilityGraphEventTraceEntry[] _emittedEvents;
        private readonly ReadOnlyCollection<AbilityGraphNodeTraceEntry> _nodesView;
        private readonly ReadOnlyCollection<AbilityGraphTargetTraceEntry> _targetDecisionsView;
        private readonly ReadOnlyCollection<AbilityGraphEventTraceEntry> _emittedEventsView;

        public AbilityGraphExecutionTrace(
            string graphId,
            int graphVersion,
            IReadOnlyList<AbilityGraphNodeTraceEntry> nodes,
            IReadOnlyList<AbilityGraphTargetTraceEntry> targetDecisions,
            IReadOnlyList<AbilityGraphEventTraceEntry> emittedEvents,
            AbilityGraphTraceFailureCode failureCode = AbilityGraphTraceFailureCode.None,
            string failureMessage = null,
            string failureNodeId = null)
        {
            GraphId = graphId ?? string.Empty;
            GraphVersion = graphVersion;
            _nodes = Copy(nodes);
            _targetDecisions = Copy(targetDecisions);
            _emittedEvents = Copy(emittedEvents);
            _nodesView = Array.AsReadOnly(_nodes);
            _targetDecisionsView = Array.AsReadOnly(_targetDecisions);
            _emittedEventsView = Array.AsReadOnly(_emittedEvents);
            FailureCode = failureCode;
            FailureMessage = failureMessage ?? string.Empty;
            FailureNodeId = failureNodeId ?? string.Empty;
        }

        public string GraphId { get; }
        public int GraphVersion { get; }
        public IReadOnlyList<AbilityGraphNodeTraceEntry> Nodes => _nodesView;
        public IReadOnlyList<AbilityGraphTargetTraceEntry> TargetDecisions => _targetDecisionsView;
        public IReadOnlyList<AbilityGraphEventTraceEntry> EmittedEvents => _emittedEventsView;
        public AbilityGraphTraceFailureCode FailureCode { get; }
        public string FailureMessage { get; }
        public string FailureNodeId { get; }
        public bool Success => FailureCode == AbilityGraphTraceFailureCode.None;

        public string ToStableString()
        {
            var builder = new StringBuilder();
            builder.Append("graph=").Append(Normalize(GraphId))
                .Append(";version=").Append(GraphVersion)
                .Append(";success=").Append(Success ? "1" : "0")
                .Append(";failure=").Append((int)FailureCode).Append(":").Append(FailureCode)
                .Append(";failureNode=").Append(Normalize(FailureNodeId))
                .Append(";message=").Append(Normalize(FailureMessage));

            for (int i = 0; i < _nodes.Length; i++)
            {
                AbilityGraphNodeTraceEntry node = _nodes[i];
                builder.Append('\n')
                    .Append("node[").Append(i).Append("]=")
                    .Append(node.Order).Append("|")
                    .Append(Normalize(node.NodeId)).Append("|")
                    .Append((int)node.Kind).Append(":").Append(node.Kind).Append("|")
                    .Append("port=").Append(Normalize(node.OutputPort)).Append("|")
                    .Append("result=").Append(Normalize(node.ResultCode)).Append("|")
                    .Append("message=").Append(Normalize(node.Message));
            }

            for (int i = 0; i < _targetDecisions.Length; i++)
            {
                AbilityGraphTargetTraceEntry target = _targetDecisions[i];
                builder.Append('\n')
                    .Append("target[").Append(i).Append("]=")
                    .Append(target.Order).Append("|")
                    .Append(Normalize(target.NodeId)).Append("|")
                    .Append("entity=").Append(target.CandidateEntityId).Append("|")
                    .Append("accepted=").Append(target.Accepted ? "1" : "0").Append("|")
                    .Append("reason=").Append((int)target.RejectReason).Append(":").Append(target.RejectReason).Append("|")
                    .Append("detail=").Append(target.DetailId).Append("|")
                    .Append("message=").Append(Normalize(target.Message));
            }

            for (int i = 0; i < _emittedEvents.Length; i++)
            {
                AbilityGraphEventTraceEntry emittedEvent = _emittedEvents[i];
                builder.Append('\n')
                    .Append("event[").Append(i).Append("]=")
                    .Append(emittedEvent.Order).Append("|")
                    .Append(Normalize(emittedEvent.NodeId)).Append("|")
                    .Append((int)emittedEvent.EventType).Append(":").Append(emittedEvent.EventType).Append("|")
                    .Append("ability=").Append(emittedEvent.AbilityId).Append("|")
                    .Append("caster=").Append(emittedEvent.CasterEntityId).Append("|")
                    .Append("target=").Append(emittedEvent.TargetEntityId).Append("|")
                    .Append("failure=").Append(Normalize(emittedEvent.FailureReason));
            }

            return builder.ToString();
        }

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<T>();

            var copy = new T[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];

            return copy;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace('\r', ' ').Replace('\n', ' ');
        }
    }

    public readonly struct AbilityGraphNodeTraceEntry
    {
        public AbilityGraphNodeTraceEntry(
            int order,
            string nodeId,
            AbilityGraphNodeKind kind,
            string outputPort = null,
            string resultCode = null,
            string message = null)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), "Ability graph trace order cannot be negative.");

            Order = order;
            NodeId = nodeId ?? string.Empty;
            Kind = kind;
            OutputPort = outputPort ?? string.Empty;
            ResultCode = resultCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Order { get; }
        public string NodeId { get; }
        public AbilityGraphNodeKind Kind { get; }
        public string OutputPort { get; }
        public string ResultCode { get; }
        public string Message { get; }
    }

    public readonly struct AbilityGraphTargetTraceEntry
    {
        public AbilityGraphTargetTraceEntry(
            int order,
            string nodeId,
            int candidateEntityId,
            bool accepted,
            GameplayTargetRejectReason rejectReason = GameplayTargetRejectReason.None,
            int detailId = 0,
            string message = null)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), "Ability graph trace order cannot be negative.");

            Order = order;
            NodeId = nodeId ?? string.Empty;
            CandidateEntityId = candidateEntityId;
            Accepted = accepted;
            RejectReason = rejectReason;
            DetailId = detailId;
            Message = message ?? string.Empty;
        }

        public int Order { get; }
        public string NodeId { get; }
        public int CandidateEntityId { get; }
        public bool Accepted { get; }
        public GameplayTargetRejectReason RejectReason { get; }
        public int DetailId { get; }
        public string Message { get; }
    }

    public readonly struct AbilityGraphEventTraceEntry
    {
        public AbilityGraphEventTraceEntry(
            int order,
            string nodeId,
            AbilityEventType eventType,
            int abilityId = 0,
            int casterEntityId = 0,
            int targetEntityId = 0,
            string failureReason = null)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), "Ability graph trace order cannot be negative.");

            Order = order;
            NodeId = nodeId ?? string.Empty;
            EventType = eventType;
            AbilityId = abilityId;
            CasterEntityId = casterEntityId;
            TargetEntityId = targetEntityId;
            FailureReason = failureReason ?? string.Empty;
        }

        public int Order { get; }
        public string NodeId { get; }
        public AbilityEventType EventType { get; }
        public int AbilityId { get; }
        public int CasterEntityId { get; }
        public int TargetEntityId { get; }
        public string FailureReason { get; }
    }

    /// <summary>Small adapter-friendly builder for graph executors that want to emit deterministic traces.</summary>
    public sealed class AbilityGraphExecutionTraceBuilder
    {
        private readonly string _graphId;
        private readonly int _graphVersion;
        private readonly List<AbilityGraphNodeTraceEntry> _nodes;
        private readonly List<AbilityGraphTargetTraceEntry> _targetDecisions;
        private readonly List<AbilityGraphEventTraceEntry> _emittedEvents;
        private AbilityGraphTraceFailureCode _failureCode;
        private string _failureMessage;
        private string _failureNodeId;

        public AbilityGraphExecutionTraceBuilder(AbilityGraphDefinition graph)
            : this(graph == null ? string.Empty : graph.GraphId, graph == null ? 0 : graph.Version)
        {
        }

        public AbilityGraphExecutionTraceBuilder(string graphId, int graphVersion)
        {
            _graphId = graphId ?? string.Empty;
            _graphVersion = graphVersion;
            _nodes = new List<AbilityGraphNodeTraceEntry>();
            _targetDecisions = new List<AbilityGraphTargetTraceEntry>();
            _emittedEvents = new List<AbilityGraphEventTraceEntry>();
            _failureCode = AbilityGraphTraceFailureCode.None;
            _failureMessage = string.Empty;
            _failureNodeId = string.Empty;
        }

        public AbilityGraphExecutionTraceBuilder RecordNode(
            AbilityGraphNode node,
            string outputPort = null,
            string resultCode = null,
            string message = null)
        {
            return RecordNode(node.NodeId, node.Kind, outputPort, resultCode, message);
        }

        public AbilityGraphExecutionTraceBuilder RecordNode(
            string nodeId,
            AbilityGraphNodeKind kind,
            string outputPort = null,
            string resultCode = null,
            string message = null)
        {
            _nodes.Add(new AbilityGraphNodeTraceEntry(
                _nodes.Count,
                nodeId,
                kind,
                outputPort,
                resultCode,
                message));
            return this;
        }

        public AbilityGraphExecutionTraceBuilder RecordTargetDecision(
            string nodeId,
            int candidateEntityId,
            bool accepted,
            GameplayTargetRejectReason rejectReason = GameplayTargetRejectReason.None,
            int detailId = 0,
            string message = null)
        {
            _targetDecisions.Add(new AbilityGraphTargetTraceEntry(
                _targetDecisions.Count,
                nodeId,
                candidateEntityId,
                accepted,
                rejectReason,
                detailId,
                message));
            return this;
        }

        public AbilityGraphExecutionTraceBuilder RecordTargetRejected(
            string nodeId,
            int candidateEntityId,
            GameplayTargetRejectReason rejectReason,
            int detailId = 0,
            string message = null)
        {
            return RecordTargetDecision(nodeId, candidateEntityId, false, rejectReason, detailId, message);
        }

        public AbilityGraphExecutionTraceBuilder RecordEvent(
            string nodeId,
            AbilityEventType eventType,
            int abilityId = 0,
            int casterEntityId = 0,
            int targetEntityId = 0,
            string failureReason = null)
        {
            _emittedEvents.Add(new AbilityGraphEventTraceEntry(
                _emittedEvents.Count,
                nodeId,
                eventType,
                abilityId,
                casterEntityId,
                targetEntityId,
                failureReason));
            return this;
        }

        public AbilityGraphExecutionTraceBuilder RecordMissingEffect(string nodeId, int effectId)
        {
            return Fail(AbilityGraphTraceFailureCode.MissingEffect, "missing effect id " + effectId, nodeId);
        }

        public AbilityGraphExecutionTraceBuilder Fail(
            AbilityGraphTraceFailureCode failureCode,
            string message,
            string nodeId = null)
        {
            if (failureCode == AbilityGraphTraceFailureCode.None)
                throw new ArgumentException("Ability graph trace failure code cannot be None.", nameof(failureCode));

            _failureCode = failureCode;
            _failureMessage = message ?? string.Empty;
            _failureNodeId = nodeId ?? string.Empty;
            return this;
        }

        public AbilityGraphExecutionTrace Build()
        {
            return new AbilityGraphExecutionTrace(
                _graphId,
                _graphVersion,
                _nodes,
                _targetDecisions,
                _emittedEvents,
                _failureCode,
                _failureMessage,
                _failureNodeId);
        }
    }
}
