using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestNavigationNodeDefinition
    {
        public CharacterTestNavigationNodeDefinition(
            int nodeId,
            FixVector3 position,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters,
            int traversalPenaltyMillimeters = 0,
            int areaTagMask = 0)
        {
            NodeId = nodeId;
            Position = position;
            ClearanceRadiusMillimeters = clearanceRadiusMillimeters;
            ClearanceHeightMillimeters = clearanceHeightMillimeters;
            TraversalPenaltyMillimeters = traversalPenaltyMillimeters;
            AreaTagMask = areaTagMask;
        }

        public int NodeId { get; }

        public FixVector3 Position { get; }

        public int ClearanceRadiusMillimeters { get; }

        public int ClearanceHeightMillimeters { get; }

        public int TraversalPenaltyMillimeters { get; }

        public int AreaTagMask { get; }
    }

    internal readonly struct CharacterTestNavigationEdgeDefinition
    {
        public CharacterTestNavigationEdgeDefinition(
            int edgeId,
            int fromNodeId,
            int toNodeId,
            int lengthMillimeters,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters,
            int traversalPenaltyMillimeters = 0,
            int blockedLayerMask = 0,
            int areaTagMask = 0)
        {
            EdgeId = edgeId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            LengthMillimeters = lengthMillimeters;
            ClearanceRadiusMillimeters = clearanceRadiusMillimeters;
            ClearanceHeightMillimeters = clearanceHeightMillimeters;
            TraversalPenaltyMillimeters = traversalPenaltyMillimeters;
            BlockedLayerMask = blockedLayerMask;
            AreaTagMask = areaTagMask;
        }

        public int EdgeId { get; }

        public int FromNodeId { get; }

        public int ToNodeId { get; }

        public int LengthMillimeters { get; }

        public int ClearanceRadiusMillimeters { get; }

        public int ClearanceHeightMillimeters { get; }

        public int TraversalPenaltyMillimeters { get; }

        public int BlockedLayerMask { get; }

        public int AreaTagMask { get; }
    }

    internal readonly struct CharacterTestNavigationGraphNode
    {
        internal CharacterTestNavigationGraphNode(CharacterTestNavigationNodeDefinition definition)
        {
            NodeId = definition.NodeId;
            Position = definition.Position;
            ClearanceRadiusMillimeters = definition.ClearanceRadiusMillimeters;
            ClearanceHeightMillimeters = definition.ClearanceHeightMillimeters;
            TraversalPenaltyMillimeters = definition.TraversalPenaltyMillimeters;
            AreaTagMask = definition.AreaTagMask;
        }

        public int NodeId { get; }

        public FixVector3 Position { get; }

        public int ClearanceRadiusMillimeters { get; }

        public int ClearanceHeightMillimeters { get; }

        public int TraversalPenaltyMillimeters { get; }

        public int AreaTagMask { get; }
    }

    internal readonly struct CharacterTestNavigationGraphEdge
    {
        internal CharacterTestNavigationGraphEdge(
            CharacterTestNavigationEdgeDefinition definition,
            int canonicalIndex,
            int fromNodeIndex,
            int toNodeIndex)
        {
            CanonicalIndex = canonicalIndex;
            EdgeId = definition.EdgeId;
            FromNodeId = definition.FromNodeId;
            ToNodeId = definition.ToNodeId;
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            LengthMillimeters = definition.LengthMillimeters;
            ClearanceRadiusMillimeters = definition.ClearanceRadiusMillimeters;
            ClearanceHeightMillimeters = definition.ClearanceHeightMillimeters;
            TraversalPenaltyMillimeters = definition.TraversalPenaltyMillimeters;
            BlockedLayerMask = definition.BlockedLayerMask;
            AreaTagMask = definition.AreaTagMask;
        }

        public int CanonicalIndex { get; }

        public int EdgeId { get; }

        public int FromNodeId { get; }

        public int ToNodeId { get; }

        public int FromNodeIndex { get; }

        public int ToNodeIndex { get; }

        public int LengthMillimeters { get; }

        public int ClearanceRadiusMillimeters { get; }

        public int ClearanceHeightMillimeters { get; }

        public int TraversalPenaltyMillimeters { get; }

        public int BlockedLayerMask { get; }

        public int AreaTagMask { get; }

        public long TotalCostMillimeters =>
            (long)LengthMillimeters + TraversalPenaltyMillimeters;
    }

    internal enum CharacterTestNavigationGraphBuildStatus
    {
        None = 0,
        Success = 1,
        InvalidRevision = 2,
        MissingNodes = 3,
        InvalidNodeId = 4,
        DuplicateNodeId = 5,
        InvalidNodeClearance = 6,
        InvalidNodePenalty = 7,
        InvalidEdgeId = 8,
        DuplicateEdgeId = 9,
        MissingEdgeEndpoint = 10,
        SelfEdge = 11,
        InvalidEdgeLength = 12,
        InvalidEdgeClearance = 13,
        InvalidEdgePenalty = 14,
        EdgeLengthBelowGeometricDistance = 15,
        InvalidNodePosition = 16
    }

    internal readonly struct CharacterTestNavigationGraphBuildResult
    {
        internal CharacterTestNavigationGraphBuildResult(
            CharacterTestNavigationGraphBuildStatus status,
            int sourceIndex,
            int stableId)
        {
            Status = status;
            SourceIndex = sourceIndex;
            StableId = stableId;
        }

        public CharacterTestNavigationGraphBuildStatus Status { get; }

        public int SourceIndex { get; }

        public int StableId { get; }

        public bool Success => Status == CharacterTestNavigationGraphBuildStatus.Success;
    }

    /// <summary>
    /// Immutable CharacterTest-local static navigation topology. Canonical nodes and edges are
    /// sorted by stable id; outgoing adjacency is precomputed during the low-frequency build.
    /// </summary>
    internal sealed class CharacterTestNavigationGraph
    {
        private readonly CharacterTestNavigationGraphNode[] _nodes;
        private readonly CharacterTestNavigationGraphEdge[] _edges;
        private readonly int[] _outgoingEdgeIndices;
        private readonly int[] _outgoingStarts;
        private readonly int[] _outgoingCounts;

        internal CharacterTestNavigationGraph(
            int revision,
            long contentHash,
            CharacterTestNavigationGraphNode[] nodes,
            CharacterTestNavigationGraphEdge[] edges,
            int[] outgoingEdgeIndices,
            int[] outgoingStarts,
            int[] outgoingCounts)
        {
            Revision = revision;
            ContentHash = contentHash;
            _nodes = nodes;
            _edges = edges;
            _outgoingEdgeIndices = outgoingEdgeIndices;
            _outgoingStarts = outgoingStarts;
            _outgoingCounts = outgoingCounts;
        }

        public int Revision { get; }

        public long ContentHash { get; }

        public int NodeCount => _nodes.Length;

        public int EdgeCount => _edges.Length;

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterTestNavigationGraphNode GetNode(int nodeIndex)
        {
            if ((uint)nodeIndex >= (uint)_nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            return _nodes[nodeIndex];
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterTestNavigationGraphEdge GetEdge(int edgeIndex)
        {
            if ((uint)edgeIndex >= (uint)_edges.Length)
                throw new ArgumentOutOfRangeException(nameof(edgeIndex));

            return _edges[edgeIndex];
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryGetNodeIndex(int nodeId, out int nodeIndex)
        {
            int low = 0;
            int high = _nodes.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _nodes[middle].NodeId.CompareTo(nodeId);
                if (comparison == 0)
                {
                    nodeIndex = middle;
                    return true;
                }

                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            nodeIndex = -1;
            return false;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public int GetOutgoingEdgeCount(int nodeIndex)
        {
            if ((uint)nodeIndex >= (uint)_nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            return _outgoingCounts[nodeIndex];
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterTestNavigationGraphEdge GetOutgoingEdge(
            int nodeIndex,
            int outgoingEdgeIndex)
        {
            if ((uint)nodeIndex >= (uint)_nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            if ((uint)outgoingEdgeIndex >= (uint)_outgoingCounts[nodeIndex])
                throw new ArgumentOutOfRangeException(nameof(outgoingEdgeIndex));

            int canonicalEdgeIndex = _outgoingEdgeIndices[
                _outgoingStarts[nodeIndex] + outgoingEdgeIndex];
            return _edges[canonicalEdgeIndex];
        }

        /// <summary>
        /// Finds the nearest 3D node that can fit the requested capsule. Equal distances resolve
        /// to the lower stable node id because canonical node order is ascending.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryFindNearestNode(
            FixVector3 position,
            int requiredClearanceRadiusMillimeters,
            int requiredClearanceHeightMillimeters,
            out int nodeIndex,
            out Fix64 distanceSquared)
        {
            return TryFindNearestNode(
                position,
                requiredClearanceRadiusMillimeters,
                requiredClearanceHeightMillimeters,
                hardBlockedAreaTagMask: 0,
                out nodeIndex,
                out distanceSquared);
        }

        /// <summary>
        /// Finds the nearest 3D node that fits the capsule and does not intersect the supplied
        /// hard-blocked area tags. Equal distances resolve to the lower stable node id.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryFindNearestNode(
            FixVector3 position,
            int requiredClearanceRadiusMillimeters,
            int requiredClearanceHeightMillimeters,
            int hardBlockedAreaTagMask,
            out int nodeIndex,
            out Fix64 distanceSquared)
        {
            nodeIndex = -1;
            distanceSquared = Fix64.MaxValue;
            if (requiredClearanceRadiusMillimeters < 0
                || requiredClearanceHeightMillimeters < 0
                || !CharacterTestNavigationDistance.IsSupportedPosition(position))
            {
                return false;
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                CharacterTestNavigationGraphNode node = _nodes[i];
                if (node.ClearanceRadiusMillimeters < requiredClearanceRadiusMillimeters
                    || node.ClearanceHeightMillimeters < requiredClearanceHeightMillimeters
                    || (node.AreaTagMask & hardBlockedAreaTagMask) != 0)
                {
                    continue;
                }

                Fix64 candidateDistanceSquared = (node.Position - position).LengthSquared();
                if (nodeIndex >= 0 && candidateDistanceSquared >= distanceSquared)
                    continue;

                nodeIndex = i;
                distanceSquared = candidateDistanceSquared;
            }

            return nodeIndex >= 0;
        }
    }

    internal static class CharacterTestNavigationGraphBuilder
    {
        private const string ContentSchema = "mx.character-test.navigation-graph.v1";

        /// <summary>Builds and validates a canonical immutable graph.</summary>
        /// <remarks>LowFreqAlloc. Call only during session build or explicit level apply.</remarks>
        public static bool TryBuild(
            int revision,
            IReadOnlyList<CharacterTestNavigationNodeDefinition> nodes,
            IReadOnlyList<CharacterTestNavigationEdgeDefinition> edges,
            out CharacterTestNavigationGraph graph,
            out CharacterTestNavigationGraphBuildResult result)
        {
            graph = null;
            if (revision <= 0)
                return Fail(CharacterTestNavigationGraphBuildStatus.InvalidRevision, -1, revision, out result);
            if (nodes == null || nodes.Count == 0)
                return Fail(CharacterTestNavigationGraphBuildStatus.MissingNodes, -1, 0, out result);

            var canonicalNodeDefinitions =
                new CharacterTestNavigationNodeDefinition[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                canonicalNodeDefinitions[i] = nodes[i];
            Array.Sort(canonicalNodeDefinitions, NodeDefinitionComparer.Instance);

            var runtimeNodes = new CharacterTestNavigationGraphNode[canonicalNodeDefinitions.Length];
            for (int i = 0; i < canonicalNodeDefinitions.Length; i++)
            {
                CharacterTestNavigationNodeDefinition node = canonicalNodeDefinitions[i];
                if (node.NodeId <= 0)
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidNodeId, i, node.NodeId, out result);
                if (i > 0 && canonicalNodeDefinitions[i - 1].NodeId == node.NodeId)
                    return Fail(CharacterTestNavigationGraphBuildStatus.DuplicateNodeId, i, node.NodeId, out result);
                if (!CharacterTestNavigationDistance.IsSupportedPosition(node.Position))
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidNodePosition, i, node.NodeId, out result);
                if (node.ClearanceRadiusMillimeters <= 0
                    || node.ClearanceHeightMillimeters <= 0)
                {
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidNodeClearance, i, node.NodeId, out result);
                }
                if (node.TraversalPenaltyMillimeters < 0)
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidNodePenalty, i, node.NodeId, out result);

                runtimeNodes[i] = new CharacterTestNavigationGraphNode(node);
            }

            int edgeCount = edges?.Count ?? 0;
            var canonicalEdgeDefinitions = new CharacterTestNavigationEdgeDefinition[edgeCount];
            for (int i = 0; i < edgeCount; i++)
                canonicalEdgeDefinitions[i] = edges[i];
            Array.Sort(canonicalEdgeDefinitions, EdgeDefinitionComparer.Instance);

            var runtimeEdges = new CharacterTestNavigationGraphEdge[edgeCount];
            var adjacency = new AdjacencyEntry[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                CharacterTestNavigationEdgeDefinition edge = canonicalEdgeDefinitions[i];
                if (edge.EdgeId <= 0)
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidEdgeId, i, edge.EdgeId, out result);
                if (i > 0 && canonicalEdgeDefinitions[i - 1].EdgeId == edge.EdgeId)
                    return Fail(CharacterTestNavigationGraphBuildStatus.DuplicateEdgeId, i, edge.EdgeId, out result);
                if (edge.FromNodeId == edge.ToNodeId)
                    return Fail(CharacterTestNavigationGraphBuildStatus.SelfEdge, i, edge.EdgeId, out result);
                if (edge.LengthMillimeters <= 0)
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidEdgeLength, i, edge.EdgeId, out result);
                if (edge.ClearanceRadiusMillimeters <= 0
                    || edge.ClearanceHeightMillimeters <= 0)
                {
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidEdgeClearance, i, edge.EdgeId, out result);
                }
                if (edge.TraversalPenaltyMillimeters < 0)
                    return Fail(CharacterTestNavigationGraphBuildStatus.InvalidEdgePenalty, i, edge.EdgeId, out result);

                int fromNodeIndex = FindNodeIndex(canonicalNodeDefinitions, edge.FromNodeId);
                int toNodeIndex = FindNodeIndex(canonicalNodeDefinitions, edge.ToNodeId);
                if (fromNodeIndex < 0 || toNodeIndex < 0)
                    return Fail(CharacterTestNavigationGraphBuildStatus.MissingEdgeEndpoint, i, edge.EdgeId, out result);
                long geometricLengthMillimeters = CharacterTestNavigationDistance
                    .ToCeilingMillimeters((
                        canonicalNodeDefinitions[toNodeIndex].Position
                        - canonicalNodeDefinitions[fromNodeIndex].Position).LengthSquared().Sqrt());
                if (edge.LengthMillimeters < geometricLengthMillimeters)
                {
                    return Fail(
                        CharacterTestNavigationGraphBuildStatus.EdgeLengthBelowGeometricDistance,
                        i,
                        edge.EdgeId,
                        out result);
                }

                runtimeEdges[i] = new CharacterTestNavigationGraphEdge(
                    edge,
                    i,
                    fromNodeIndex,
                    toNodeIndex);
                adjacency[i] = new AdjacencyEntry(
                    edge.FromNodeId,
                    edge.EdgeId,
                    edge.ToNodeId,
                    i,
                    fromNodeIndex);
            }

            Array.Sort(adjacency, AdjacencyEntryComparer.Instance);
            var outgoingEdgeIndices = new int[edgeCount];
            var outgoingStarts = new int[runtimeNodes.Length];
            var outgoingCounts = new int[runtimeNodes.Length];
            int adjacencyCursor = 0;
            for (int nodeIndex = 0; nodeIndex < runtimeNodes.Length; nodeIndex++)
            {
                outgoingStarts[nodeIndex] = adjacencyCursor;
                while (adjacencyCursor < adjacency.Length
                    && adjacency[adjacencyCursor].FromNodeIndex == nodeIndex)
                {
                    outgoingEdgeIndices[adjacencyCursor] = adjacency[adjacencyCursor].CanonicalEdgeIndex;
                    outgoingCounts[nodeIndex]++;
                    adjacencyCursor++;
                }
            }

            long contentHash = ComputeContentHash(runtimeNodes, runtimeEdges);
            graph = new CharacterTestNavigationGraph(
                revision,
                contentHash == 0 ? 1 : contentHash,
                runtimeNodes,
                runtimeEdges,
                outgoingEdgeIndices,
                outgoingStarts,
                outgoingCounts);
            result = new CharacterTestNavigationGraphBuildResult(
                CharacterTestNavigationGraphBuildStatus.Success,
                -1,
                0);
            return true;
        }

        private static long ComputeContentHash(
            CharacterTestNavigationGraphNode[] nodes,
            CharacterTestNavigationGraphEdge[] edges)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("navigationGraph.schema", ContentSchema);
            hash.AddInt("navigationGraph.nodeCount", nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                CharacterTestNavigationGraphNode node = nodes[i];
                hash.AddInt("navigationGraph.node.id", node.NodeId);
                hash.AddLong("navigationGraph.node.x", node.Position.X.RawValue);
                hash.AddLong("navigationGraph.node.y", node.Position.Y.RawValue);
                hash.AddLong("navigationGraph.node.z", node.Position.Z.RawValue);
                hash.AddInt("navigationGraph.node.clearanceRadiusMm", node.ClearanceRadiusMillimeters);
                hash.AddInt("navigationGraph.node.clearanceHeightMm", node.ClearanceHeightMillimeters);
                hash.AddInt("navigationGraph.node.penaltyMm", node.TraversalPenaltyMillimeters);
                hash.AddInt("navigationGraph.node.areaTagMask", node.AreaTagMask);
            }

            hash.AddInt("navigationGraph.edgeCount", edges.Length);
            for (int i = 0; i < edges.Length; i++)
            {
                CharacterTestNavigationGraphEdge edge = edges[i];
                hash.AddInt("navigationGraph.edge.id", edge.EdgeId);
                hash.AddInt("navigationGraph.edge.from", edge.FromNodeId);
                hash.AddInt("navigationGraph.edge.to", edge.ToNodeId);
                hash.AddInt("navigationGraph.edge.lengthMm", edge.LengthMillimeters);
                hash.AddInt("navigationGraph.edge.clearanceRadiusMm", edge.ClearanceRadiusMillimeters);
                hash.AddInt("navigationGraph.edge.clearanceHeightMm", edge.ClearanceHeightMillimeters);
                hash.AddInt("navigationGraph.edge.penaltyMm", edge.TraversalPenaltyMillimeters);
                hash.AddInt("navigationGraph.edge.blockedLayerMask", edge.BlockedLayerMask);
                hash.AddInt("navigationGraph.edge.areaTagMask", edge.AreaTagMask);
            }

            return hash.ToHash();
        }

        private static int FindNodeIndex(
            CharacterTestNavigationNodeDefinition[] nodes,
            int nodeId)
        {
            int low = 0;
            int high = nodes.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = nodes[middle].NodeId.CompareTo(nodeId);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return -1;
        }

        private static bool Fail(
            CharacterTestNavigationGraphBuildStatus status,
            int sourceIndex,
            int stableId,
            out CharacterTestNavigationGraphBuildResult result)
        {
            result = new CharacterTestNavigationGraphBuildResult(status, sourceIndex, stableId);
            return false;
        }

        private readonly struct AdjacencyEntry
        {
            public AdjacencyEntry(
                int fromNodeId,
                int edgeId,
                int toNodeId,
                int canonicalEdgeIndex,
                int fromNodeIndex)
            {
                FromNodeId = fromNodeId;
                EdgeId = edgeId;
                ToNodeId = toNodeId;
                CanonicalEdgeIndex = canonicalEdgeIndex;
                FromNodeIndex = fromNodeIndex;
            }

            public int FromNodeId { get; }
            public int EdgeId { get; }
            public int ToNodeId { get; }
            public int CanonicalEdgeIndex { get; }
            public int FromNodeIndex { get; }
        }

        private sealed class NodeDefinitionComparer : IComparer<CharacterTestNavigationNodeDefinition>
        {
            public static readonly NodeDefinitionComparer Instance = new NodeDefinitionComparer();

            public int Compare(
                CharacterTestNavigationNodeDefinition left,
                CharacterTestNavigationNodeDefinition right)
            {
                return left.NodeId.CompareTo(right.NodeId);
            }
        }

        private sealed class EdgeDefinitionComparer : IComparer<CharacterTestNavigationEdgeDefinition>
        {
            public static readonly EdgeDefinitionComparer Instance = new EdgeDefinitionComparer();

            public int Compare(
                CharacterTestNavigationEdgeDefinition left,
                CharacterTestNavigationEdgeDefinition right)
            {
                return left.EdgeId.CompareTo(right.EdgeId);
            }
        }

        private sealed class AdjacencyEntryComparer : IComparer<AdjacencyEntry>
        {
            public static readonly AdjacencyEntryComparer Instance = new AdjacencyEntryComparer();

            public int Compare(AdjacencyEntry left, AdjacencyEntry right)
            {
                int comparison = left.FromNodeId.CompareTo(right.FromNodeId);
                if (comparison != 0)
                    return comparison;
                comparison = left.EdgeId.CompareTo(right.EdgeId);
                return comparison != 0
                    ? comparison
                    : left.ToNodeId.CompareTo(right.ToNodeId);
            }
        }
    }

    internal static class CharacterTestNavigationDistance
    {
        private const long Fix64RawPerMillimeter = Fix64.Scale / 1000L;
        private const long MaxCoordinateRaw = 1000L * Fix64.Scale;

        public static bool IsSupportedPosition(FixVector3 position)
        {
            return IsSupportedCoordinate(position.X.RawValue)
                && IsSupportedCoordinate(position.Y.RawValue)
                && IsSupportedCoordinate(position.Z.RawValue);
        }

        public static long ToFloorMillimeters(Fix64 distance)
        {
            return distance.RawValue <= 0
                ? 0
                : distance.RawValue / Fix64RawPerMillimeter;
        }

        public static long ToCeilingMillimeters(Fix64 distance)
        {
            if (distance.RawValue <= 0)
                return 0;

            long quotient = distance.RawValue / Fix64RawPerMillimeter;
            return distance.RawValue % Fix64RawPerMillimeter == 0
                ? quotient
                : quotient + 1;
        }

        private static bool IsSupportedCoordinate(long rawValue)
        {
            return rawValue >= -MaxCoordinateRaw && rawValue <= MaxCoordinateRaw;
        }
    }
}
