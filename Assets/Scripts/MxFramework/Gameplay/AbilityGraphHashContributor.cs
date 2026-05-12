using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>Contributes a deterministic ability graph definition hash to runtime result hashing.</summary>
    public sealed class AbilityGraphHashContributor : IRuntimeHashContributor
    {
        public const string StableContributorId = "mxframework.gameplay.ability-graph.definition";

        private readonly AbilityGraphDefinition _graph;

        public AbilityGraphHashContributor(AbilityGraphDefinition graph)
            : this(StableContributorId, graph)
        {
        }

        public AbilityGraphHashContributor(string contributorId, AbilityGraphDefinition graph)
        {
            if (string.IsNullOrEmpty(contributorId))
                throw new ArgumentException("Ability graph hash contributor id cannot be null or empty.", nameof(contributorId));
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            ContributorId = contributorId;
            _graph = graph;
        }

        public string ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            ContributeDefinition(_graph, accumulator);
        }

        public static long ComputeDefinitionHash(AbilityGraphDefinition graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddStringStable("runtime.contributor.id", StableContributorId);
            ContributeDefinition(graph, accumulator);
            return accumulator.ToHash();
        }

        public static void ContributeDefinition(AbilityGraphDefinition graph, RuntimeHashAccumulator accumulator)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.AddStringStable("ability.graph.hash.schema", "AbilityGraphDefinition.v1");
            accumulator.AddStringStable("ability.graph.id", graph.GraphId);
            accumulator.AddInt("ability.graph.version", graph.Version);
            accumulator.AddStringStable("ability.graph.entry.node.id", graph.EntryNodeId);

            NodeHashEntry[] nodes = CopyAndSortNodes(graph.Nodes);
            accumulator.AddInt("ability.graph.node.count", nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
                AddNode(accumulator, i, nodes[i].Node);

            AbilityGraphEdge[] edges = CopyAndSortEdges(graph.Edges);
            accumulator.AddInt("ability.graph.edge.count", edges.Length);
            for (int i = 0; i < edges.Length; i++)
                AddEdge(accumulator, i, edges[i]);
        }

        private static void AddNode(RuntimeHashAccumulator accumulator, int index, AbilityGraphNode node)
        {
            accumulator.AddInt("ability.graph.node.index", index);
            accumulator.AddStringStable("ability.graph.node.id", node.NodeId);
            accumulator.AddInt("ability.graph.node.kind", (int)node.Kind);
            AddPayload(accumulator, node.Payload);
        }

        private static void AddEdge(RuntimeHashAccumulator accumulator, int index, AbilityGraphEdge edge)
        {
            accumulator.AddInt("ability.graph.edge.index", index);
            accumulator.AddStringStable("ability.graph.edge.from", edge.FromNodeId);
            accumulator.AddStringStable("ability.graph.edge.port", edge.OutputPort);
            accumulator.AddStringStable("ability.graph.edge.to", edge.ToNodeId);
        }

        private static void AddPayload(RuntimeHashAccumulator accumulator, object payload)
        {
            accumulator.AddInt("ability.graph.node.payload.present", payload == null ? 0 : 1);
            if (payload == null)
                return;

            accumulator.AddStringStable("ability.graph.node.payload.type", payload.GetType().FullName);

            if (payload is IAbilityGraphNodePayload graphPayload)
                accumulator.AddInt("ability.graph.node.payload.nodeKind", (int)graphPayload.NodeKind);
            else
                accumulator.AddInt("ability.graph.node.payload.nodeKind", -1);

            if (payload is AbilityGraphTargetQueryPayload targetQuery)
            {
                accumulator.AddStringStable("ability.graph.node.payload.shape", "target-query");
                accumulator.AddInt("ability.graph.node.payload.target.relation", (int)targetQuery.RelationFilter);
                accumulator.AddInt("ability.graph.node.payload.target.requireAlive", targetQuery.RequireAlive ? 1 : 0);
                accumulator.AddInt("ability.graph.node.payload.target.maxTargets", targetQuery.MaxTargets);
                AddSortedIds(accumulator, "ability.graph.node.payload.target.requiredTag", targetQuery.RequiredTags);
                AddSortedIds(accumulator, "ability.graph.node.payload.target.blockedStatus", targetQuery.BlockedStatuses);
                return;
            }

            if (payload is AbilityGraphApplyEffectPayload applyEffect)
            {
                accumulator.AddStringStable("ability.graph.node.payload.shape", "apply-effect");
                accumulator.AddInt("ability.graph.node.payload.effect.id", applyEffect.EffectId);
                return;
            }

            if (payload is AbilityGraphEmitEventPayload emitEvent)
            {
                accumulator.AddStringStable("ability.graph.node.payload.shape", "emit-event");
                accumulator.AddInt("ability.graph.node.payload.event.type", (int)emitEvent.EventType);
                return;
            }

            if (payload is AbilityGraphPhaseGatePayload phaseGate)
            {
                accumulator.AddStringStable("ability.graph.node.payload.shape", "phase-gate");
                accumulator.AddStringStable("ability.graph.node.payload.phase.id", phaseGate.PhaseId);
                return;
            }

            accumulator.AddStringStable("ability.graph.node.payload.shape", "unsupported");
        }

        private static void AddSortedIds(
            RuntimeHashAccumulator accumulator,
            string key,
            IReadOnlyList<int> values)
        {
            int[] sorted = CopySorted(values);
            accumulator.AddInt(key + ".count", sorted.Length);
            for (int i = 0; i < sorted.Length; i++)
            {
                accumulator.AddInt(key + ".index", i);
                accumulator.AddInt(key + ".value", sorted[i]);
            }
        }

        private static NodeHashEntry[] CopyAndSortNodes(IReadOnlyList<AbilityGraphNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return Array.Empty<NodeHashEntry>();

            var entries = new NodeHashEntry[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                entries[i] = new NodeHashEntry(nodes[i], BuildPayloadSortKey(nodes[i].Payload));

            Array.Sort(entries, CompareNodes);
            return entries;
        }

        private static AbilityGraphEdge[] CopyAndSortEdges(IReadOnlyList<AbilityGraphEdge> edges)
        {
            if (edges == null || edges.Count == 0)
                return Array.Empty<AbilityGraphEdge>();

            var copy = new AbilityGraphEdge[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                copy[i] = edges[i];

            Array.Sort(copy, CompareEdges);
            return copy;
        }

        private static int CompareNodes(NodeHashEntry left, NodeHashEntry right)
        {
            int nodeId = string.Compare(left.Node.NodeId ?? string.Empty, right.Node.NodeId ?? string.Empty, StringComparison.Ordinal);
            if (nodeId != 0)
                return nodeId;

            int kind = ((int)left.Node.Kind).CompareTo((int)right.Node.Kind);
            if (kind != 0)
                return kind;

            return string.Compare(left.PayloadSortKey, right.PayloadSortKey, StringComparison.Ordinal);
        }

        private static int CompareEdges(AbilityGraphEdge left, AbilityGraphEdge right)
        {
            int from = string.Compare(left.FromNodeId ?? string.Empty, right.FromNodeId ?? string.Empty, StringComparison.Ordinal);
            if (from != 0)
                return from;

            int port = string.Compare(left.OutputPort ?? string.Empty, right.OutputPort ?? string.Empty, StringComparison.Ordinal);
            if (port != 0)
                return port;

            return string.Compare(left.ToNodeId ?? string.Empty, right.ToNodeId ?? string.Empty, StringComparison.Ordinal);
        }

        private static string BuildPayloadSortKey(object payload)
        {
            if (payload == null)
                return string.Empty;

            if (payload is AbilityGraphTargetQueryPayload targetQuery)
            {
                return "target-query|" +
                    (int)targetQuery.RelationFilter + "|" +
                    (targetQuery.RequireAlive ? 1 : 0) + "|" +
                    targetQuery.MaxTargets + "|" +
                    FormatSortedIds(targetQuery.RequiredTags) + "|" +
                    FormatSortedIds(targetQuery.BlockedStatuses);
            }

            if (payload is AbilityGraphApplyEffectPayload applyEffect)
                return "apply-effect|" + applyEffect.EffectId;

            if (payload is AbilityGraphEmitEventPayload emitEvent)
                return "emit-event|" + (int)emitEvent.EventType;

            if (payload is AbilityGraphPhaseGatePayload phaseGate)
                return "phase-gate|" + (phaseGate.PhaseId ?? string.Empty);

            if (payload is IAbilityGraphNodePayload graphPayload)
                return "unsupported|" + payload.GetType().FullName + "|" + (int)graphPayload.NodeKind;

            return "unsupported|" + payload.GetType().FullName;
        }

        private static string FormatSortedIds(IReadOnlyList<int> values)
        {
            int[] sorted = CopySorted(values);
            if (sorted.Length == 0)
                return string.Empty;

            return string.Join(",", sorted);
        }

        private static int[] CopySorted(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<int>();

            var sorted = new int[values.Count];
            for (int i = 0; i < values.Count; i++)
                sorted[i] = values[i];

            Array.Sort(sorted);
            return sorted;
        }

        private readonly struct NodeHashEntry
        {
            public NodeHashEntry(AbilityGraphNode node, string payloadSortKey)
            {
                Node = node;
                PayloadSortKey = payloadSortKey ?? string.Empty;
            }

            public AbilityGraphNode Node { get; }
            public string PayloadSortKey { get; }
        }
    }
}
