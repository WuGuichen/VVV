using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Gameplay
{
    /// <summary>Common output port tokens used by runtime ability graph edges.</summary>
    public static class AbilityGraphPorts
    {
        public const string Next = "next";
        public const string Success = "success";
        public const string Failure = "failure";
    }

    public enum AbilityGraphNodeKind
    {
        Entry = 0,
        Sequence = 1,
        TargetQuery = 2,
        ApplyEffect = 3,
        EmitEvent = 4,
        PhaseGate = 5,
    }

    public enum AbilityGraphValidationErrorCode
    {
        NullGraphDefinition = 0,
        EmptyNodeId = 1,
        DuplicateNodeId = 2,
        MissingEntryNode = 3,
        InvalidEntryNodeKind = 4,
        EmptyEdgeEndpoint = 5,
        MissingEdgeOutputPort = 6,
        UnresolvedEdgeEndpoint = 7,
        CycleDetected = 8,
        InvalidNodeKind = 9,
        InvalidNodePayload = 10,
    }

    public interface IAbilityGraphNodePayload
    {
        AbilityGraphNodeKind NodeKind { get; }
    }

    public readonly struct AbilityGraphNode
    {
        public AbilityGraphNode(string nodeId, AbilityGraphNodeKind kind, object payload = null)
        {
            NodeId = nodeId ?? string.Empty;
            Kind = kind;
            Payload = payload;
        }

        public string NodeId { get; }
        public AbilityGraphNodeKind Kind { get; }
        public object Payload { get; }
    }

    public readonly struct AbilityGraphEdge
    {
        public AbilityGraphEdge(string fromNodeId, string outputPort, string toNodeId)
        {
            FromNodeId = fromNodeId ?? string.Empty;
            OutputPort = outputPort ?? string.Empty;
            ToNodeId = toNodeId ?? string.Empty;
        }

        public string FromNodeId { get; }
        public string OutputPort { get; }
        public string ToNodeId { get; }
    }

    public sealed class AbilityGraphDefinition
    {
        public const int CurrentVersion = 1;

        private readonly AbilityGraphNode[] _nodes;
        private readonly AbilityGraphEdge[] _edges;
        private readonly ReadOnlyCollection<AbilityGraphNode> _nodesView;
        private readonly ReadOnlyCollection<AbilityGraphEdge> _edgesView;

        public AbilityGraphDefinition(
            string graphId,
            string entryNodeId,
            IReadOnlyList<AbilityGraphNode> nodes,
            IReadOnlyList<AbilityGraphEdge> edges)
            : this(graphId, CurrentVersion, entryNodeId, nodes, edges)
        {
        }

        public AbilityGraphDefinition(
            string graphId,
            int version,
            string entryNodeId,
            IReadOnlyList<AbilityGraphNode> nodes,
            IReadOnlyList<AbilityGraphEdge> edges)
        {
            GraphId = graphId ?? string.Empty;
            Version = version;
            EntryNodeId = entryNodeId ?? string.Empty;
            _nodes = CopyAndSortNodes(nodes);
            _edges = CopyAndSortEdges(edges);
            _nodesView = Array.AsReadOnly(_nodes);
            _edgesView = Array.AsReadOnly(_edges);
        }

        public string GraphId { get; }
        public int Version { get; }
        public string EntryNodeId { get; }
        public IReadOnlyList<AbilityGraphNode> Nodes => _nodesView;
        public IReadOnlyList<AbilityGraphEdge> Edges => _edgesView;

        public AbilityGraphValidationResult Validate()
        {
            return AbilityGraphValidator.Validate(this);
        }

        private static AbilityGraphNode[] CopyAndSortNodes(IReadOnlyList<AbilityGraphNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return Array.Empty<AbilityGraphNode>();

            var entries = new NodeSortEntry[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                entries[i] = new NodeSortEntry(nodes[i], i);

            Array.Sort(entries, CompareNodeEntries);

            var copy = new AbilityGraphNode[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                copy[i] = entries[i].Node;

            return copy;
        }

        private static AbilityGraphEdge[] CopyAndSortEdges(IReadOnlyList<AbilityGraphEdge> edges)
        {
            if (edges == null || edges.Count == 0)
                return Array.Empty<AbilityGraphEdge>();

            var entries = new EdgeSortEntry[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                entries[i] = new EdgeSortEntry(edges[i], i);

            Array.Sort(entries, CompareEdgeEntries);

            var copy = new AbilityGraphEdge[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                copy[i] = entries[i].Edge;

            return copy;
        }

        private static int CompareNodeEntries(NodeSortEntry left, NodeSortEntry right)
        {
            int nodeId = string.Compare(left.Node.NodeId ?? string.Empty, right.Node.NodeId ?? string.Empty, StringComparison.Ordinal);
            return nodeId != 0 ? nodeId : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int CompareEdgeEntries(EdgeSortEntry left, EdgeSortEntry right)
        {
            int from = string.Compare(left.Edge.FromNodeId ?? string.Empty, right.Edge.FromNodeId ?? string.Empty, StringComparison.Ordinal);
            if (from != 0)
                return from;

            int port = string.Compare(left.Edge.OutputPort ?? string.Empty, right.Edge.OutputPort ?? string.Empty, StringComparison.Ordinal);
            if (port != 0)
                return port;

            int to = string.Compare(left.Edge.ToNodeId ?? string.Empty, right.Edge.ToNodeId ?? string.Empty, StringComparison.Ordinal);
            return to != 0 ? to : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private readonly struct NodeSortEntry
        {
            public NodeSortEntry(AbilityGraphNode node, int originalIndex)
            {
                Node = node;
                OriginalIndex = originalIndex;
            }

            public AbilityGraphNode Node { get; }
            public int OriginalIndex { get; }
        }

        private readonly struct EdgeSortEntry
        {
            public EdgeSortEntry(AbilityGraphEdge edge, int originalIndex)
            {
                Edge = edge;
                OriginalIndex = originalIndex;
            }

            public AbilityGraphEdge Edge { get; }
            public int OriginalIndex { get; }
        }
    }

    public sealed class AbilityGraphTargetQueryPayload : IAbilityGraphNodePayload
    {
        private readonly int[] _requiredTags;
        private readonly int[] _blockedStatuses;
        private readonly ReadOnlyCollection<int> _requiredTagsView;
        private readonly ReadOnlyCollection<int> _blockedStatusesView;

        public AbilityGraphTargetQueryPayload(
            GameplayTargetRelationFilter relationFilter = GameplayTargetRelationFilter.Any,
            bool requireAlive = true,
            IReadOnlyList<int> requiredTags = null,
            IReadOnlyList<int> blockedStatuses = null,
            int maxTargets = 0)
        {
            RelationFilter = relationFilter;
            RequireAlive = requireAlive;
            MaxTargets = maxTargets;
            _requiredTags = CopyIds(requiredTags);
            _blockedStatuses = CopyIds(blockedStatuses);
            _requiredTagsView = Array.AsReadOnly(_requiredTags);
            _blockedStatusesView = Array.AsReadOnly(_blockedStatuses);
        }

        public AbilityGraphNodeKind NodeKind => AbilityGraphNodeKind.TargetQuery;
        public GameplayTargetRelationFilter RelationFilter { get; }
        public bool RequireAlive { get; }
        public int MaxTargets { get; }
        public IReadOnlyList<int> RequiredTags => _requiredTagsView;
        public IReadOnlyList<int> BlockedStatuses => _blockedStatusesView;

        private static int[] CopyIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i];

            return copy;
        }
    }

    public sealed class AbilityGraphApplyEffectPayload : IAbilityGraphNodePayload
    {
        public AbilityGraphApplyEffectPayload(int effectId)
        {
            EffectId = effectId;
        }

        public AbilityGraphNodeKind NodeKind => AbilityGraphNodeKind.ApplyEffect;
        public int EffectId { get; }
    }

    public sealed class AbilityGraphEmitEventPayload : IAbilityGraphNodePayload
    {
        public AbilityGraphEmitEventPayload(AbilityEventType eventType)
        {
            EventType = eventType;
        }

        public AbilityGraphNodeKind NodeKind => AbilityGraphNodeKind.EmitEvent;
        public AbilityEventType EventType { get; }
    }

    public sealed class AbilityGraphPhaseGatePayload : IAbilityGraphNodePayload
    {
        public AbilityGraphPhaseGatePayload(string phaseId)
        {
            PhaseId = phaseId ?? string.Empty;
        }

        public AbilityGraphNodeKind NodeKind => AbilityGraphNodeKind.PhaseGate;
        public string PhaseId { get; }
    }

    public readonly struct AbilityGraphValidationError
    {
        public AbilityGraphValidationError(
            AbilityGraphValidationErrorCode code,
            string message,
            string nodeId = null,
            int edgeIndex = -1,
            string fieldPath = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            EdgeIndex = edgeIndex;
            FieldPath = fieldPath ?? string.Empty;
        }

        public AbilityGraphValidationErrorCode Code { get; }
        public string Message { get; }
        public string NodeId { get; }
        public int EdgeIndex { get; }
        public string FieldPath { get; }
    }

    public sealed class AbilityGraphValidationResult
    {
        private readonly AbilityGraphValidationError[] _errors;
        private readonly ReadOnlyCollection<AbilityGraphValidationError> _errorsView;

        public AbilityGraphValidationResult(IReadOnlyList<AbilityGraphValidationError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                _errors = Array.Empty<AbilityGraphValidationError>();
                _errorsView = Array.AsReadOnly(_errors);
                return;
            }

            _errors = new AbilityGraphValidationError[errors.Count];
            for (int i = 0; i < errors.Count; i++)
                _errors[i] = errors[i];

            _errorsView = Array.AsReadOnly(_errors);
        }

        public bool IsValid => _errors.Length == 0;
        public int ErrorCount => _errors.Length;
        public IReadOnlyList<AbilityGraphValidationError> Errors => _errorsView;

        public bool Contains(AbilityGraphValidationErrorCode code)
        {
            for (int i = 0; i < _errors.Length; i++)
            {
                if (_errors[i].Code == code)
                    return true;
            }

            return false;
        }
    }

    public static class AbilityGraphValidator
    {
        public static AbilityGraphValidationResult Validate(AbilityGraphDefinition graph)
        {
            var errors = new List<AbilityGraphValidationError>();
            if (graph == null)
            {
                Add(errors, AbilityGraphValidationErrorCode.NullGraphDefinition, "Ability graph definition is null.");
                return new AbilityGraphValidationResult(errors);
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var orderedNodeIds = new List<string>();
            var nodeKinds = new Dictionary<string, AbilityGraphNodeKind>(StringComparer.Ordinal);
            string lastDuplicateNodeId = null;

            IReadOnlyList<AbilityGraphNode> nodes = graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                AbilityGraphNode node = nodes[i];
                string nodeId = Normalize(node.NodeId);
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    Add(errors, AbilityGraphValidationErrorCode.EmptyNodeId, "Ability graph node id cannot be empty.", fieldPath: "Nodes[" + i + "].NodeId");
                }
                else if (!nodeIds.Add(nodeId))
                {
                    if (!string.Equals(lastDuplicateNodeId, nodeId, StringComparison.Ordinal))
                    {
                        Add(errors, AbilityGraphValidationErrorCode.DuplicateNodeId, "Ability graph node id is duplicated: " + nodeId + ".", nodeId);
                        lastDuplicateNodeId = nodeId;
                    }
                }
                else
                {
                    orderedNodeIds.Add(nodeId);
                    nodeKinds.Add(nodeId, node.Kind);
                }

                ValidateNodePayload(node, errors);
            }

            ValidateEntryNode(graph.EntryNodeId, nodeKinds, errors);
            ValidateEdges(graph.Edges, nodeIds, errors);
            ValidateAcyclic(graph.Edges, orderedNodeIds, errors);
            return new AbilityGraphValidationResult(errors);
        }

        private static void ValidateEntryNode(
            string entryNodeId,
            Dictionary<string, AbilityGraphNodeKind> nodeKinds,
            List<AbilityGraphValidationError> errors)
        {
            string normalizedEntryNodeId = Normalize(entryNodeId);
            if (string.IsNullOrWhiteSpace(normalizedEntryNodeId))
            {
                Add(errors, AbilityGraphValidationErrorCode.MissingEntryNode, "Ability graph entry node id is missing.", fieldPath: "EntryNodeId");
                return;
            }

            if (!nodeKinds.TryGetValue(normalizedEntryNodeId, out AbilityGraphNodeKind kind))
            {
                Add(errors, AbilityGraphValidationErrorCode.MissingEntryNode, "Ability graph entry node does not exist: " + normalizedEntryNodeId + ".", normalizedEntryNodeId, fieldPath: "EntryNodeId");
                return;
            }

            if (kind != AbilityGraphNodeKind.Entry)
                Add(errors, AbilityGraphValidationErrorCode.InvalidEntryNodeKind, "Ability graph entry node must use Entry kind.", normalizedEntryNodeId, fieldPath: "EntryNodeId");
        }

        private static void ValidateEdges(
            IReadOnlyList<AbilityGraphEdge> edges,
            HashSet<string> nodeIds,
            List<AbilityGraphValidationError> errors)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                AbilityGraphEdge edge = edges[i];
                string fromNodeId = Normalize(edge.FromNodeId);
                string toNodeId = Normalize(edge.ToNodeId);

                if (string.IsNullOrWhiteSpace(fromNodeId))
                    Add(errors, AbilityGraphValidationErrorCode.EmptyEdgeEndpoint, "Ability graph edge from node id cannot be empty.", edgeIndex: i, fieldPath: "Edges[" + i + "].FromNodeId");
                else if (!nodeIds.Contains(fromNodeId))
                    Add(errors, AbilityGraphValidationErrorCode.UnresolvedEdgeEndpoint, "Ability graph edge references missing from node: " + fromNodeId + ".", fromNodeId, i, "Edges[" + i + "].FromNodeId");

                if (string.IsNullOrWhiteSpace(edge.OutputPort))
                    Add(errors, AbilityGraphValidationErrorCode.MissingEdgeOutputPort, "Ability graph edge output port cannot be empty.", fromNodeId, i, "Edges[" + i + "].OutputPort");

                if (string.IsNullOrWhiteSpace(toNodeId))
                    Add(errors, AbilityGraphValidationErrorCode.EmptyEdgeEndpoint, "Ability graph edge to node id cannot be empty.", edgeIndex: i, fieldPath: "Edges[" + i + "].ToNodeId");
                else if (!nodeIds.Contains(toNodeId))
                    Add(errors, AbilityGraphValidationErrorCode.UnresolvedEdgeEndpoint, "Ability graph edge references missing to node: " + toNodeId + ".", toNodeId, i, "Edges[" + i + "].ToNodeId");
            }
        }

        private static void ValidateNodePayload(AbilityGraphNode node, List<AbilityGraphValidationError> errors)
        {
            string nodeId = Normalize(node.NodeId);
            if (!Enum.IsDefined(typeof(AbilityGraphNodeKind), node.Kind))
            {
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodeKind, "Ability graph node kind is not supported.", nodeId, fieldPath: FieldPath(nodeId, "Kind"));
                return;
            }

            switch (node.Kind)
            {
                case AbilityGraphNodeKind.Entry:
                case AbilityGraphNodeKind.Sequence:
                    if (node.Payload != null)
                        Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "Ability graph node kind does not accept payload.", nodeId, fieldPath: FieldPath(nodeId, "Payload"));
                    break;

                case AbilityGraphNodeKind.TargetQuery:
                    ValidateTargetQueryPayload(nodeId, node.Payload, errors);
                    break;

                case AbilityGraphNodeKind.ApplyEffect:
                    ValidateApplyEffectPayload(nodeId, node.Payload, errors);
                    break;

                case AbilityGraphNodeKind.EmitEvent:
                    ValidateEmitEventPayload(nodeId, node.Payload, errors);
                    break;

                case AbilityGraphNodeKind.PhaseGate:
                    ValidatePhaseGatePayload(nodeId, node.Payload, errors);
                    break;
            }
        }

        private static void ValidateTargetQueryPayload(string nodeId, object payload, List<AbilityGraphValidationError> errors)
        {
            if (!(payload is AbilityGraphTargetQueryPayload targetQuery))
            {
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "TargetQuery node requires AbilityGraphTargetQueryPayload.", nodeId, fieldPath: FieldPath(nodeId, "Payload"));
                return;
            }

            if (!Enum.IsDefined(typeof(GameplayTargetRelationFilter), targetQuery.RelationFilter))
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "TargetQuery relation filter is not supported.", nodeId, fieldPath: FieldPath(nodeId, "Payload.RelationFilter"));

            if (targetQuery.MaxTargets < 0)
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "TargetQuery max targets cannot be negative.", nodeId, fieldPath: FieldPath(nodeId, "Payload.MaxTargets"));

            ValidatePositiveIds(targetQuery.RequiredTags, "RequiredTags", nodeId, errors);
            ValidatePositiveIds(targetQuery.BlockedStatuses, "BlockedStatuses", nodeId, errors);
        }

        private static void ValidateApplyEffectPayload(string nodeId, object payload, List<AbilityGraphValidationError> errors)
        {
            if (!(payload is AbilityGraphApplyEffectPayload applyEffect))
            {
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "ApplyEffect node requires AbilityGraphApplyEffectPayload.", nodeId, fieldPath: FieldPath(nodeId, "Payload"));
                return;
            }

            if (applyEffect.EffectId <= 0)
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "ApplyEffect effect id must be positive.", nodeId, fieldPath: FieldPath(nodeId, "Payload.EffectId"));
        }

        private static void ValidateEmitEventPayload(string nodeId, object payload, List<AbilityGraphValidationError> errors)
        {
            if (!(payload is AbilityGraphEmitEventPayload emitEvent))
            {
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "EmitEvent node requires AbilityGraphEmitEventPayload.", nodeId, fieldPath: FieldPath(nodeId, "Payload"));
                return;
            }

            if (!Enum.IsDefined(typeof(AbilityEventType), emitEvent.EventType))
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "EmitEvent event type is not supported.", nodeId, fieldPath: FieldPath(nodeId, "Payload.EventType"));
        }

        private static void ValidatePhaseGatePayload(string nodeId, object payload, List<AbilityGraphValidationError> errors)
        {
            if (!(payload is AbilityGraphPhaseGatePayload phaseGate))
            {
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "PhaseGate node requires AbilityGraphPhaseGatePayload.", nodeId, fieldPath: FieldPath(nodeId, "Payload"));
                return;
            }

            if (string.IsNullOrWhiteSpace(phaseGate.PhaseId))
                Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "PhaseGate phase id cannot be empty.", nodeId, fieldPath: FieldPath(nodeId, "Payload.PhaseId"));
        }

        private static void ValidatePositiveIds(
            IReadOnlyList<int> ids,
            string fieldName,
            string nodeId,
            List<AbilityGraphValidationError> errors)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] <= 0)
                    Add(errors, AbilityGraphValidationErrorCode.InvalidNodePayload, "TargetQuery " + fieldName + " id must be positive.", nodeId, fieldPath: FieldPath(nodeId, "Payload." + fieldName + "[" + i + "]"));
            }
        }

        private static void ValidateAcyclic(
            IReadOnlyList<AbilityGraphEdge> edges,
            IReadOnlyList<string> orderedNodeIds,
            List<AbilityGraphValidationError> errors)
        {
            if (orderedNodeIds.Count == 0 || edges.Count == 0)
                return;

            var nodeIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < orderedNodeIds.Count; i++)
                nodeIndexes.Add(orderedNodeIds[i], i);

            var adjacency = new List<CycleEdge>[orderedNodeIds.Count];
            for (int i = 0; i < adjacency.Length; i++)
                adjacency[i] = new List<CycleEdge>();

            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                AbilityGraphEdge edge = edges[edgeIndex];
                if (!nodeIndexes.TryGetValue(Normalize(edge.FromNodeId), out int fromIndex))
                    continue;
                if (!nodeIndexes.TryGetValue(Normalize(edge.ToNodeId), out int toIndex))
                    continue;

                adjacency[fromIndex].Add(new CycleEdge(toIndex, edgeIndex));
            }

            var state = new int[orderedNodeIds.Count];
            for (int i = 0; i < orderedNodeIds.Count; i++)
            {
                if (state[i] == 0 && TryFindCycle(i, orderedNodeIds, adjacency, state, errors))
                    return;
            }
        }

        private static bool TryFindCycle(
            int nodeIndex,
            IReadOnlyList<string> orderedNodeIds,
            IReadOnlyList<List<CycleEdge>> adjacency,
            int[] state,
            List<AbilityGraphValidationError> errors)
        {
            state[nodeIndex] = 1;
            List<CycleEdge> edges = adjacency[nodeIndex];
            for (int i = 0; i < edges.Count; i++)
            {
                CycleEdge edge = edges[i];
                if (state[edge.ToNodeIndex] == 1)
                {
                    string nodeId = orderedNodeIds[edge.ToNodeIndex];
                    Add(errors, AbilityGraphValidationErrorCode.CycleDetected, "Ability graph contains a directed cycle.", nodeId, edge.EdgeIndex);
                    return true;
                }

                if (state[edge.ToNodeIndex] == 0 && TryFindCycle(edge.ToNodeIndex, orderedNodeIds, adjacency, state, errors))
                    return true;
            }

            state[nodeIndex] = 2;
            return false;
        }

        private static string Normalize(string value)
        {
            return value ?? string.Empty;
        }

        private static string FieldPath(string nodeId, string member)
        {
            return string.IsNullOrEmpty(nodeId)
                ? "Nodes[]." + member
                : "Nodes[" + nodeId + "]." + member;
        }

        private static void Add(
            List<AbilityGraphValidationError> errors,
            AbilityGraphValidationErrorCode code,
            string message,
            string nodeId = null,
            int edgeIndex = -1,
            string fieldPath = null)
        {
            errors.Add(new AbilityGraphValidationError(code, message, nodeId, edgeIndex, fieldPath));
        }

        private readonly struct CycleEdge
        {
            public CycleEdge(int toNodeIndex, int edgeIndex)
            {
                ToNodeIndex = toNodeIndex;
                EdgeIndex = edgeIndex;
            }

            public int ToNodeIndex { get; }
            public int EdgeIndex { get; }
        }
    }
}
