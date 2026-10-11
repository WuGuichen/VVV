using System;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestPathStatus
    {
        None = 0,
        Success = 1,
        GraphUnavailable = 2,
        InvalidQuery = 3,
        StaleGraph = 4,
        SourceNodeUnavailable = 5,
        TargetNodeUnavailable = 6,
        SourceOutsideGraph = 7,
        TargetOutsideGraph = 8,
        Unreachable = 9,
        BudgetExceeded = 10,
        PathBufferTooSmall = 11,
        CostOverflow = 12
    }

    internal readonly struct CharacterTestPathQuery
    {
        public CharacterTestPathQuery(
            int expectedGraphRevision,
            long expectedGraphContentHash,
            FixVector3 sourcePosition,
            FixVector3 targetPosition,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters,
            int maxEndpointSnapDistanceMillimeters,
            int maxNodeExpansions,
            int maxPathNodeCount,
            int hardBlockedAreaTagMask = 0,
            int activeBlockedLayerMask = 0)
        {
            ExpectedGraphRevision = expectedGraphRevision;
            ExpectedGraphContentHash = expectedGraphContentHash;
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            ClearanceRadiusMillimeters = clearanceRadiusMillimeters;
            ClearanceHeightMillimeters = clearanceHeightMillimeters;
            MaxEndpointSnapDistanceMillimeters = maxEndpointSnapDistanceMillimeters;
            MaxNodeExpansions = maxNodeExpansions;
            MaxPathNodeCount = maxPathNodeCount;
            HardBlockedAreaTagMask = hardBlockedAreaTagMask;
            ActiveBlockedLayerMask = activeBlockedLayerMask;
        }

        public int ExpectedGraphRevision { get; }

        public long ExpectedGraphContentHash { get; }

        public FixVector3 SourcePosition { get; }

        public FixVector3 TargetPosition { get; }

        public int ClearanceRadiusMillimeters { get; }

        public int ClearanceHeightMillimeters { get; }

        public int MaxEndpointSnapDistanceMillimeters { get; }

        public int MaxNodeExpansions { get; }

        public int MaxPathNodeCount { get; }

        public int HardBlockedAreaTagMask { get; }

        public int ActiveBlockedLayerMask { get; }
    }

    /// <summary>Caller-owned bounded output storage for a navigation corridor.</summary>
    internal sealed class CharacterTestNavigationPathBuffer
    {
        private readonly int[] _nodeIds;
        private readonly int[] _edgeIds;

        /// <remarks>Initialization allocation only. Do not construct from a Tick path.</remarks>
        public CharacterTestNavigationPathBuffer(int nodeCapacity)
        {
            if (nodeCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(nodeCapacity));

            _nodeIds = new int[nodeCapacity];
            _edgeIds = new int[nodeCapacity - 1];
        }

        public int NodeCapacity => _nodeIds.Length;

        public int EdgeCapacity => _edgeIds.Length;

        public int NodeCount { get; private set; }

        public int EdgeCount { get; private set; }

        /// <remarks>NoAlloc after initialization.</remarks>
        public int GetNodeId(int index)
        {
            if ((uint)index >= (uint)NodeCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _nodeIds[index];
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public int GetEdgeId(int index)
        {
            if ((uint)index >= (uint)EdgeCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _edgeIds[index];
        }

        internal void Clear()
        {
            NodeCount = 0;
            EdgeCount = 0;
        }

        internal void BeginWrite(int nodeCount)
        {
            NodeCount = nodeCount;
            EdgeCount = nodeCount > 0 ? nodeCount - 1 : 0;
        }

        internal void SetNodeId(int index, int nodeId)
        {
            _nodeIds[index] = nodeId;
        }

        internal void SetEdgeId(int index, int edgeId)
        {
            _edgeIds[index] = edgeId;
        }
    }

    internal readonly struct CharacterTestPathfindingSample
    {
        internal CharacterTestPathfindingSample(
            CharacterTestPathStatus status,
            int graphRevision,
            long graphContentHash,
            int sourceNodeId,
            int targetNodeId,
            int expandedNodeCount,
            int pathNodeCount,
            int pathEdgeCount,
            long sourceSnapDistanceMillimeters,
            long targetSnapDistanceMillimeters,
            long remainingDistanceMillimeters,
            long totalCostMillimeters,
            bool hasNextWaypoint,
            FixVector3 nextWaypoint)
        {
            Status = status;
            GraphRevision = graphRevision;
            GraphContentHash = graphContentHash;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            ExpandedNodeCount = expandedNodeCount;
            PathNodeCount = pathNodeCount;
            PathEdgeCount = pathEdgeCount;
            SourceSnapDistanceMillimeters = sourceSnapDistanceMillimeters;
            TargetSnapDistanceMillimeters = targetSnapDistanceMillimeters;
            RemainingDistanceMillimeters = remainingDistanceMillimeters;
            TotalCostMillimeters = totalCostMillimeters;
            HasNextWaypoint = hasNextWaypoint;
            NextWaypoint = nextWaypoint;
        }

        public CharacterTestPathStatus Status { get; }

        public int GraphRevision { get; }

        public long GraphContentHash { get; }

        public int SourceNodeId { get; }

        public int TargetNodeId { get; }

        public int ExpandedNodeCount { get; }

        public int PathNodeCount { get; }

        public int PathEdgeCount { get; }

        public long SourceSnapDistanceMillimeters { get; }

        public long TargetSnapDistanceMillimeters { get; }

        public long RemainingDistanceMillimeters { get; }

        public long TotalCostMillimeters { get; }

        public bool HasNextWaypoint { get; }

        public FixVector3 NextWaypoint { get; }

        public bool Success => Status == CharacterTestPathStatus.Success;
    }

    /// <summary>
    /// CharacterTest-local deterministic bounded A*. The graph and all scratch buffers are fixed
    /// at construction; queries write only to caller-owned path storage.
    /// </summary>
    internal sealed class CharacterTestBoundedAStarPathfinder
    {
        private const byte Unseen = 0;
        private const byte Open = 1;
        private const byte Closed = 2;

        private readonly CharacterTestNavigationGraph _graph;
        private readonly long[] _costFromSource;
        private readonly long[] _heuristic;
        private readonly int[] _parentNodeIndices;
        private readonly int[] _parentEdgeIndices;
        private readonly byte[] _states;
        private readonly int[] _openHeap;
        private readonly int[] _heapPositions;
        private int _openCount;

        /// <remarks>Initialization allocation only. Construct outside Runtime Tick.</remarks>
        public CharacterTestBoundedAStarPathfinder(CharacterTestNavigationGraph graph)
        {
            _graph = graph;
            int capacity = graph?.NodeCount ?? 0;
            _costFromSource = new long[capacity];
            _heuristic = new long[capacity];
            _parentNodeIndices = new int[capacity];
            _parentEdgeIndices = new int[capacity];
            _states = new byte[capacity];
            _openHeap = new int[capacity];
            _heapPositions = new int[capacity];
        }

        public CharacterTestNavigationGraph Graph => _graph;

        /// <summary>Runs a deterministic bounded A* query into caller-owned storage.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryFindPath(
            in CharacterTestPathQuery query,
            CharacterTestNavigationPathBuffer pathBuffer,
            out CharacterTestPathfindingSample sample)
        {
            pathBuffer?.Clear();
            if (_graph == null)
                return Fail(CharacterTestPathStatus.GraphUnavailable, 0, 0, 0, out sample);
            if (!IsValid(query) || pathBuffer == null)
                return Fail(CharacterTestPathStatus.InvalidQuery, 0, 0, 0, out sample);
            if (query.ExpectedGraphRevision != _graph.Revision
                || query.ExpectedGraphContentHash != _graph.ContentHash)
            {
                return Fail(CharacterTestPathStatus.StaleGraph, 0, 0, 0, out sample);
            }

            int requiredPathCapacity = query.MaxPathNodeCount < _graph.NodeCount
                ? query.MaxPathNodeCount
                : _graph.NodeCount;
            if (pathBuffer.NodeCapacity < requiredPathCapacity)
                return Fail(CharacterTestPathStatus.PathBufferTooSmall, 0, 0, 0, out sample);

            if (!_graph.TryFindNearestNode(
                    query.SourcePosition,
                    query.ClearanceRadiusMillimeters,
                    query.ClearanceHeightMillimeters,
                    query.HardBlockedAreaTagMask,
                    out int sourceNodeIndex,
                    out Fix64 sourceDistanceSquared))
            {
                return Fail(CharacterTestPathStatus.SourceNodeUnavailable, 0, 0, 0, out sample);
            }

            if (!_graph.TryFindNearestNode(
                    query.TargetPosition,
                    query.ClearanceRadiusMillimeters,
                    query.ClearanceHeightMillimeters,
                    query.HardBlockedAreaTagMask,
                    out int targetNodeIndex,
                    out Fix64 targetDistanceSquared))
            {
                return Fail(
                    CharacterTestPathStatus.TargetNodeUnavailable,
                    _graph.GetNode(sourceNodeIndex).NodeId,
                    0,
                    0,
                    out sample);
            }

            long sourceSnapDistanceMillimeters = CharacterTestNavigationDistance
                .ToCeilingMillimeters(sourceDistanceSquared.Sqrt());
            long targetSnapDistanceMillimeters = CharacterTestNavigationDistance
                .ToCeilingMillimeters(targetDistanceSquared.Sqrt());
            int sourceNodeId = _graph.GetNode(sourceNodeIndex).NodeId;
            int targetNodeId = _graph.GetNode(targetNodeIndex).NodeId;
            if (sourceSnapDistanceMillimeters > query.MaxEndpointSnapDistanceMillimeters)
            {
                return Fail(
                    CharacterTestPathStatus.SourceOutsideGraph,
                    sourceNodeId,
                    targetNodeId,
                    0,
                    out sample);
            }
            if (targetSnapDistanceMillimeters > query.MaxEndpointSnapDistanceMillimeters)
            {
                return Fail(
                    CharacterTestPathStatus.TargetOutsideGraph,
                    sourceNodeId,
                    targetNodeId,
                    0,
                    out sample);
            }

            ResetScratch();
            _costFromSource[sourceNodeIndex] = sourceSnapDistanceMillimeters;
            _heuristic[sourceNodeIndex] = ComputeHeuristic(sourceNodeIndex, targetNodeIndex);
            _states[sourceNodeIndex] = Open;
            PushOpen(sourceNodeIndex);

            int expandedNodeCount = 0;
            while (_openCount > 0)
            {
                if (expandedNodeCount >= query.MaxNodeExpansions)
                {
                    return Fail(
                        CharacterTestPathStatus.BudgetExceeded,
                        sourceNodeId,
                        targetNodeId,
                        expandedNodeCount,
                        out sample);
                }

                int currentNodeIndex = PopOpen();
                _states[currentNodeIndex] = Closed;
                expandedNodeCount++;
                if (currentNodeIndex == targetNodeIndex)
                {
                    return TryWriteSuccess(
                        query,
                        pathBuffer,
                        sourceNodeIndex,
                        targetNodeIndex,
                        sourceSnapDistanceMillimeters,
                        targetSnapDistanceMillimeters,
                        expandedNodeCount,
                        out sample);
                }

                int outgoingCount = _graph.GetOutgoingEdgeCount(currentNodeIndex);
                for (int outgoingIndex = 0; outgoingIndex < outgoingCount; outgoingIndex++)
                {
                    CharacterTestNavigationGraphEdge edge =
                        _graph.GetOutgoingEdge(currentNodeIndex, outgoingIndex);
                    if (!CanTraverse(edge, query))
                        continue;

                    int neighborNodeIndex = edge.ToNodeIndex;
                    if (_states[neighborNodeIndex] == Closed)
                        continue;
                    CharacterTestNavigationGraphNode neighborNode =
                        _graph.GetNode(neighborNodeIndex);
                    if (neighborNode.ClearanceRadiusMillimeters < query.ClearanceRadiusMillimeters
                        || neighborNode.ClearanceHeightMillimeters < query.ClearanceHeightMillimeters
                        || (neighborNode.AreaTagMask & query.HardBlockedAreaTagMask) != 0)
                    {
                        continue;
                    }

                    if (!TryAddCost(
                            _costFromSource[currentNodeIndex],
                            edge.TotalCostMillimeters,
                            out long tentativeCost)
                        || !TryAddCost(
                            tentativeCost,
                            neighborNode.TraversalPenaltyMillimeters,
                            out tentativeCost))
                    {
                        return Fail(
                            CharacterTestPathStatus.CostOverflow,
                            sourceNodeId,
                            targetNodeId,
                            expandedNodeCount,
                            out sample);
                    }

                    if (!ShouldRelax(
                            neighborNodeIndex,
                            currentNodeIndex,
                            edge.EdgeId,
                            tentativeCost))
                    {
                        continue;
                    }

                    _costFromSource[neighborNodeIndex] = tentativeCost;
                    _heuristic[neighborNodeIndex] = ComputeHeuristic(
                        neighborNodeIndex,
                        targetNodeIndex);
                    _parentNodeIndices[neighborNodeIndex] = currentNodeIndex;
                    _parentEdgeIndices[neighborNodeIndex] = edge.CanonicalIndex;
                    if (_states[neighborNodeIndex] == Open)
                    {
                        SiftUp(_heapPositions[neighborNodeIndex]);
                    }
                    else
                    {
                        _states[neighborNodeIndex] = Open;
                        PushOpen(neighborNodeIndex);
                    }
                }
            }

            return Fail(
                CharacterTestPathStatus.Unreachable,
                sourceNodeId,
                targetNodeId,
                expandedNodeCount,
                out sample);
        }

        private bool TryWriteSuccess(
            in CharacterTestPathQuery query,
            CharacterTestNavigationPathBuffer pathBuffer,
            int sourceNodeIndex,
            int targetNodeIndex,
            long sourceSnapDistanceMillimeters,
            long targetSnapDistanceMillimeters,
            int expandedNodeCount,
            out CharacterTestPathfindingSample sample)
        {
            int pathNodeCount = 1;
            int cursor = targetNodeIndex;
            while (cursor != sourceNodeIndex)
            {
                cursor = _parentNodeIndices[cursor];
                if (cursor < 0)
                {
                    return Fail(
                        CharacterTestPathStatus.Unreachable,
                        _graph.GetNode(sourceNodeIndex).NodeId,
                        _graph.GetNode(targetNodeIndex).NodeId,
                        expandedNodeCount,
                        out sample);
                }

                pathNodeCount++;
                if (pathNodeCount > _graph.NodeCount)
                {
                    return Fail(
                        CharacterTestPathStatus.Unreachable,
                        _graph.GetNode(sourceNodeIndex).NodeId,
                        _graph.GetNode(targetNodeIndex).NodeId,
                        expandedNodeCount,
                        out sample);
                }
            }

            if (pathNodeCount > query.MaxPathNodeCount
                || pathNodeCount > pathBuffer.NodeCapacity)
            {
                return Fail(
                    CharacterTestPathStatus.PathBufferTooSmall,
                    _graph.GetNode(sourceNodeIndex).NodeId,
                    _graph.GetNode(targetNodeIndex).NodeId,
                    expandedNodeCount,
                    out sample);
            }

            long graphDistanceMillimeters = 0;
            pathBuffer.BeginWrite(pathNodeCount);
            cursor = targetNodeIndex;
            int writeNodeIndex = pathNodeCount - 1;
            while (true)
            {
                pathBuffer.SetNodeId(writeNodeIndex, _graph.GetNode(cursor).NodeId);
                if (cursor == sourceNodeIndex)
                    break;

                int canonicalEdgeIndex = _parentEdgeIndices[cursor];
                CharacterTestNavigationGraphEdge edge = _graph.GetEdge(canonicalEdgeIndex);
                pathBuffer.SetEdgeId(writeNodeIndex - 1, edge.EdgeId);
                if (!TryAddCost(
                        graphDistanceMillimeters,
                        edge.LengthMillimeters,
                        out graphDistanceMillimeters))
                {
                    pathBuffer.Clear();
                    return Fail(
                        CharacterTestPathStatus.CostOverflow,
                        _graph.GetNode(sourceNodeIndex).NodeId,
                        _graph.GetNode(targetNodeIndex).NodeId,
                        expandedNodeCount,
                        out sample);
                }

                cursor = _parentNodeIndices[cursor];
                writeNodeIndex--;
            }

            if (!TryAddCost(
                    sourceSnapDistanceMillimeters,
                    graphDistanceMillimeters,
                    out long remainingDistanceMillimeters)
                || !TryAddCost(
                    remainingDistanceMillimeters,
                    targetSnapDistanceMillimeters,
                    out remainingDistanceMillimeters)
                || !TryAddCost(
                    _costFromSource[targetNodeIndex],
                    targetSnapDistanceMillimeters,
                    out long totalCostMillimeters))
            {
                pathBuffer.Clear();
                return Fail(
                    CharacterTestPathStatus.CostOverflow,
                    _graph.GetNode(sourceNodeIndex).NodeId,
                    _graph.GetNode(targetNodeIndex).NodeId,
                    expandedNodeCount,
                    out sample);
            }

            FixVector3 nextWaypoint;
            if (pathNodeCount > 1
                && _graph.TryGetNodeIndex(pathBuffer.GetNodeId(1), out int nextNodeIndex))
            {
                nextWaypoint = _graph.GetNode(nextNodeIndex).Position;
            }
            else
            {
                nextWaypoint = query.TargetPosition;
            }

            sample = new CharacterTestPathfindingSample(
                CharacterTestPathStatus.Success,
                _graph.Revision,
                _graph.ContentHash,
                _graph.GetNode(sourceNodeIndex).NodeId,
                _graph.GetNode(targetNodeIndex).NodeId,
                expandedNodeCount,
                pathBuffer.NodeCount,
                pathBuffer.EdgeCount,
                sourceSnapDistanceMillimeters,
                targetSnapDistanceMillimeters,
                remainingDistanceMillimeters,
                totalCostMillimeters,
                remainingDistanceMillimeters > 0,
                nextWaypoint);
            return true;
        }

        private bool CanTraverse(
            CharacterTestNavigationGraphEdge edge,
            in CharacterTestPathQuery query)
        {
            return edge.ClearanceRadiusMillimeters >= query.ClearanceRadiusMillimeters
                && edge.ClearanceHeightMillimeters >= query.ClearanceHeightMillimeters
                && (edge.AreaTagMask & query.HardBlockedAreaTagMask) == 0
                && (edge.BlockedLayerMask & query.ActiveBlockedLayerMask) == 0;
        }

        private bool ShouldRelax(
            int nodeIndex,
            int parentNodeIndex,
            int incomingEdgeId,
            long tentativeCost)
        {
            if (_states[nodeIndex] == Unseen)
                return true;
            if (tentativeCost < _costFromSource[nodeIndex])
                return true;
            if (tentativeCost > _costFromSource[nodeIndex])
                return false;

            int existingEdgeIndex = _parentEdgeIndices[nodeIndex];
            if (existingEdgeIndex < 0)
                return true;
            int existingEdgeId = _graph.GetEdge(existingEdgeIndex).EdgeId;
            if (incomingEdgeId != existingEdgeId)
                return incomingEdgeId < existingEdgeId;

            int existingParentIndex = _parentNodeIndices[nodeIndex];
            return existingParentIndex < 0
                || _graph.GetNode(parentNodeIndex).NodeId
                    < _graph.GetNode(existingParentIndex).NodeId;
        }

        private long ComputeHeuristic(int nodeIndex, int targetNodeIndex)
        {
            FixVector3 delta = _graph.GetNode(targetNodeIndex).Position
                - _graph.GetNode(nodeIndex).Position;
            return CharacterTestNavigationDistance.ToFloorMillimeters(
                delta.LengthSquared().Sqrt());
        }

        private void ResetScratch()
        {
            _openCount = 0;
            for (int i = 0; i < _costFromSource.Length; i++)
            {
                _costFromSource[i] = long.MaxValue;
                _heuristic[i] = 0;
                _parentNodeIndices[i] = -1;
                _parentEdgeIndices[i] = -1;
                _states[i] = Unseen;
                _heapPositions[i] = -1;
            }
        }

        private void PushOpen(int nodeIndex)
        {
            int heapIndex = _openCount++;
            _openHeap[heapIndex] = nodeIndex;
            _heapPositions[nodeIndex] = heapIndex;
            SiftUp(heapIndex);
        }

        private int PopOpen()
        {
            int root = _openHeap[0];
            _openCount--;
            _heapPositions[root] = -1;
            if (_openCount > 0)
            {
                int replacement = _openHeap[_openCount];
                _openHeap[0] = replacement;
                _heapPositions[replacement] = 0;
                SiftDown(0);
            }

            return root;
        }

        private void SiftUp(int heapIndex)
        {
            while (heapIndex > 0)
            {
                int parentIndex = (heapIndex - 1) >> 1;
                if (ComparePriority(_openHeap[heapIndex], _openHeap[parentIndex]) >= 0)
                    break;
                SwapHeap(heapIndex, parentIndex);
                heapIndex = parentIndex;
            }
        }

        private void SiftDown(int heapIndex)
        {
            while (true)
            {
                int left = (heapIndex << 1) + 1;
                if (left >= _openCount)
                    return;
                int right = left + 1;
                int best = right < _openCount
                    && ComparePriority(_openHeap[right], _openHeap[left]) < 0
                    ? right
                    : left;
                if (ComparePriority(_openHeap[best], _openHeap[heapIndex]) >= 0)
                    return;
                SwapHeap(heapIndex, best);
                heapIndex = best;
            }
        }

        private int ComparePriority(int leftNodeIndex, int rightNodeIndex)
        {
            long leftTotal = SaturatingAdd(
                _costFromSource[leftNodeIndex],
                _heuristic[leftNodeIndex]);
            long rightTotal = SaturatingAdd(
                _costFromSource[rightNodeIndex],
                _heuristic[rightNodeIndex]);
            int comparison = leftTotal.CompareTo(rightTotal);
            if (comparison != 0)
                return comparison;
            comparison = _heuristic[leftNodeIndex].CompareTo(_heuristic[rightNodeIndex]);
            if (comparison != 0)
                return comparison;
            comparison = _graph.GetNode(leftNodeIndex).NodeId.CompareTo(
                _graph.GetNode(rightNodeIndex).NodeId);
            if (comparison != 0)
                return comparison;
            return ResolveIncomingEdgeId(leftNodeIndex).CompareTo(
                ResolveIncomingEdgeId(rightNodeIndex));
        }

        private int ResolveIncomingEdgeId(int nodeIndex)
        {
            int edgeIndex = _parentEdgeIndices[nodeIndex];
            return edgeIndex >= 0 ? _graph.GetEdge(edgeIndex).EdgeId : 0;
        }

        private void SwapHeap(int leftIndex, int rightIndex)
        {
            int leftNode = _openHeap[leftIndex];
            int rightNode = _openHeap[rightIndex];
            _openHeap[leftIndex] = rightNode;
            _openHeap[rightIndex] = leftNode;
            _heapPositions[leftNode] = rightIndex;
            _heapPositions[rightNode] = leftIndex;
        }

        private bool Fail(
            CharacterTestPathStatus status,
            int sourceNodeId,
            int targetNodeId,
            int expandedNodeCount,
            out CharacterTestPathfindingSample sample)
        {
            sample = new CharacterTestPathfindingSample(
                status,
                _graph?.Revision ?? 0,
                _graph?.ContentHash ?? 0,
                sourceNodeId,
                targetNodeId,
                expandedNodeCount,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                FixVector3.Zero);
            return false;
        }

        private static bool IsValid(in CharacterTestPathQuery query)
        {
            return query.ExpectedGraphRevision > 0
                && query.ExpectedGraphContentHash != 0
                && CharacterTestNavigationDistance.IsSupportedPosition(query.SourcePosition)
                && CharacterTestNavigationDistance.IsSupportedPosition(query.TargetPosition)
                && query.ClearanceRadiusMillimeters > 0
                && query.ClearanceHeightMillimeters > 0
                && query.MaxEndpointSnapDistanceMillimeters >= 0
                && query.MaxNodeExpansions > 0
                && query.MaxPathNodeCount > 0;
        }

        private static bool TryAddCost(long left, long right, out long sum)
        {
            if (left < 0 || right < 0 || left > long.MaxValue - right)
            {
                sum = long.MaxValue;
                return false;
            }

            sum = left + right;
            return true;
        }

        private static long SaturatingAdd(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }
    }
}
