using System;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBoundedAStarPathfinderTests
    {
        [Test]
        public void FindPath_DirectCorridorWritesCallerOwnedResult()
        {
            CharacterTestNavigationGraph graph = BuildGraph(
                new[] { Node(1, 0, 0), Node(2, 2, 0) },
                new[] { Edge(10, 1, 2, 2000) });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(2);

            Assert.IsTrue(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(2, 0), maxPathNodes: 2),
                buffer,
                out CharacterTestPathfindingSample sample));

            Assert.AreEqual(CharacterTestPathStatus.Success, sample.Status);
            Assert.AreEqual(2, sample.ExpandedNodeCount);
            Assert.AreEqual(2, sample.PathNodeCount);
            Assert.AreEqual(1, sample.PathEdgeCount);
            Assert.AreEqual(1, buffer.GetNodeId(0));
            Assert.AreEqual(2, buffer.GetNodeId(1));
            Assert.AreEqual(10, buffer.GetEdgeId(0));
            Assert.AreEqual(2000, sample.RemainingDistanceMillimeters);
            Assert.AreEqual(2000, sample.TotalCostMillimeters);
            Assert.AreEqual(Position(2, 0), sample.NextWaypoint);
        }

        [Test]
        public void FindPath_IncludesEndpointSnapAndTraversalPenalties()
        {
            CharacterTestNavigationGraph graph = BuildGraph(
                new[]
                {
                    Node(1, 0, 0),
                    Node(2, 2, 0, traversalPenalty: 50)
                },
                new[] { Edge(10, 1, 2, 2000, traversalPenalty: 100) });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(2);

            Assert.IsTrue(pathfinder.TryFindPath(
                Query(
                    graph,
                    Position(Fix64.FromRatio(-1, 2), Fix64.Zero),
                    Position(Fix64.FromRatio(9, 4), Fix64.Zero),
                    maxPathNodes: 2),
                buffer,
                out CharacterTestPathfindingSample sample));

            Assert.AreEqual(500, sample.SourceSnapDistanceMillimeters);
            Assert.AreEqual(250, sample.TargetSnapDistanceMillimeters);
            Assert.AreEqual(2750, sample.RemainingDistanceMillimeters);
            Assert.AreEqual(2900, sample.TotalCostMillimeters);
            Assert.AreEqual(
                Position(2, 0),
                sample.NextWaypoint,
                "Steering must advance to the first traversed node instead of returning to the snapped source node.");
        }

        [Test]
        public void FindPath_EqualCostRoutesUseStableNodeAndEdgeOrder()
        {
            CharacterTestNavigationNodeDefinition[] nodes =
            {
                Node(40, 2, 0),
                Node(30, 1, -1),
                Node(10, 0, 0),
                Node(20, 1, 1)
            };
            CharacterTestNavigationEdgeDefinition[] edges =
            {
                Edge(40, 30, 40, 1415),
                Edge(30, 10, 30, 1415),
                Edge(20, 20, 40, 1415),
                Edge(10, 10, 20, 1415)
            };
            CharacterTestNavigationGraph first = BuildGraph(nodes, edges);
            Array.Reverse(nodes);
            Array.Reverse(edges);
            CharacterTestNavigationGraph reordered = BuildGraph(nodes, edges);

            var firstBuffer = new CharacterTestNavigationPathBuffer(3);
            var reorderedBuffer = new CharacterTestNavigationPathBuffer(3);
            Assert.IsTrue(new CharacterTestBoundedAStarPathfinder(first).TryFindPath(
                Query(first, Position(0, 0), Position(2, 0), maxPathNodes: 3),
                firstBuffer,
                out CharacterTestPathfindingSample firstSample));
            Assert.IsTrue(new CharacterTestBoundedAStarPathfinder(reordered).TryFindPath(
                Query(reordered, Position(0, 0), Position(2, 0), maxPathNodes: 3),
                reorderedBuffer,
                out CharacterTestPathfindingSample reorderedSample));

            Assert.AreEqual(20, firstBuffer.GetNodeId(1));
            Assert.AreEqual(10, firstBuffer.GetEdgeId(0));
            Assert.AreEqual(20, firstBuffer.GetEdgeId(1));
            Assert.AreEqual(firstSample.TotalCostMillimeters, reorderedSample.TotalCostMillimeters);
            Assert.AreEqual(firstBuffer.GetNodeId(1), reorderedBuffer.GetNodeId(1));
        }

        [Test]
        public void FindPath_HazardPenaltyChangesOptimalRouteWithoutHardBlocking()
        {
            const int Hazard = 1 << 2;
            CharacterTestNavigationGraph graph = BuildGraph(
                new[]
                {
                    Node(1, 0, 0),
                    Node(2, 1, 1, traversalPenalty: 4000, areaTagMask: Hazard),
                    Node(3, 1, -1),
                    Node(4, 2, 0)
                },
                new[]
                {
                    Edge(10, 1, 2, 1415),
                    Edge(20, 2, 4, 1415),
                    Edge(30, 1, 3, 1415),
                    Edge(40, 3, 4, 1415)
                });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(3);

            Assert.IsTrue(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(2, 0), maxPathNodes: 3),
                buffer,
                out CharacterTestPathfindingSample sample));

            Assert.AreEqual(3, buffer.GetNodeId(1));
            Assert.AreEqual(2830, sample.TotalCostMillimeters);
        }

        [Test]
        public void FindPath_RespectsClearanceAreaAndBlockedLayerMasks()
        {
            const int Hazard = 1 << 2;
            const int ClosedDoorLayer = 1 << 3;
            CharacterTestNavigationGraph graph = BuildGraph(
                new[]
                {
                    Node(1, 0, 0, clearance: 1000),
                    Node(2, 1, 1, clearance: 400),
                    Node(3, 1, -1, clearance: 1000, areaTagMask: Hazard),
                    Node(4, 2, 0, clearance: 1000)
                },
                new[]
                {
                    Edge(10, 1, 2, 1415, clearance: 400),
                    Edge(20, 2, 4, 1415, clearance: 400),
                    Edge(30, 1, 3, 1415, clearance: 1000, blockedLayerMask: ClosedDoorLayer),
                    Edge(40, 3, 4, 1415, clearance: 1000)
                });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(3);

            Assert.IsTrue(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(2, 0), maxPathNodes: 3, clearance: 700),
                buffer,
                out _));
            Assert.AreEqual(3, buffer.GetNodeId(1));

            Assert.IsFalse(pathfinder.TryFindPath(
                Query(
                    graph,
                    Position(0, 0),
                    Position(2, 0),
                    maxPathNodes: 3,
                    clearance: 700,
                    hardBlockedAreaTagMask: Hazard),
                buffer,
                out CharacterTestPathfindingSample areaBlocked));
            Assert.AreEqual(CharacterTestPathStatus.Unreachable, areaBlocked.Status);
            Assert.AreEqual(0, buffer.NodeCount);

            Assert.IsFalse(pathfinder.TryFindPath(
                Query(
                    graph,
                    Position(0, 0),
                    Position(2, 0),
                    maxPathNodes: 3,
                    clearance: 700,
                    activeBlockedLayerMask: ClosedDoorLayer),
                buffer,
                out CharacterTestPathfindingSample layerBlocked));
            Assert.AreEqual(CharacterTestPathStatus.Unreachable, layerBlocked.Status);
        }

        [Test]
        public void FindPath_ReportsGraphInvalidAndStaleStates()
        {
            var buffer = new CharacterTestNavigationPathBuffer(2);
            var unavailable = new CharacterTestBoundedAStarPathfinder(null);
            Assert.IsFalse(unavailable.TryFindPath(default, buffer, out CharacterTestPathfindingSample missing));
            Assert.AreEqual(CharacterTestPathStatus.GraphUnavailable, missing.Status);

            CharacterTestNavigationGraph graph = BuildGraph(
                new[] { Node(1, 0, 0), Node(2, 1, 0) },
                new[] { Edge(1, 1, 2, 1000) });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            CharacterTestPathQuery invalid = Query(
                graph,
                Position(0, 0),
                Position(1, 0),
                maxPathNodes: 2,
                maxExpansions: 0);
            Assert.IsFalse(pathfinder.TryFindPath(invalid, buffer, out CharacterTestPathfindingSample invalidSample));
            Assert.AreEqual(CharacterTestPathStatus.InvalidQuery, invalidSample.Status);

            CharacterTestPathQuery invalidPosition = new CharacterTestPathQuery(
                graph.Revision,
                graph.ContentHash,
                new FixVector3(Fix64.MaxValue, Fix64.Zero, Fix64.Zero),
                Position(1, 0),
                500,
                1800,
                1000,
                8,
                2);
            Assert.IsFalse(pathfinder.TryFindPath(
                invalidPosition,
                buffer,
                out CharacterTestPathfindingSample invalidPositionSample));
            Assert.AreEqual(CharacterTestPathStatus.InvalidQuery, invalidPositionSample.Status);

            CharacterTestPathQuery stale = new CharacterTestPathQuery(
                graph.Revision + 1,
                graph.ContentHash,
                Position(0, 0),
                Position(1, 0),
                500,
                1800,
                1000,
                8,
                2);
            Assert.IsFalse(pathfinder.TryFindPath(stale, buffer, out CharacterTestPathfindingSample staleSample));
            Assert.AreEqual(CharacterTestPathStatus.StaleGraph, staleSample.Status);
        }

        [Test]
        public void FindPath_ReportsEndpointOutsideGraph()
        {
            CharacterTestNavigationGraph graph = BuildGraph(
                new[] { Node(1, 0, 0), Node(2, 1, 0) },
                new[] { Edge(1, 1, 2, 1000) });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(2);

            Assert.IsFalse(pathfinder.TryFindPath(
                Query(graph, Position(-10, 0), Position(1, 0), maxPathNodes: 2, maxSnap: 1000),
                buffer,
                out CharacterTestPathfindingSample sourceOutside));
            Assert.AreEqual(CharacterTestPathStatus.SourceOutsideGraph, sourceOutside.Status);

            Assert.IsFalse(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(10, 0), maxPathNodes: 2, maxSnap: 1000),
                buffer,
                out CharacterTestPathfindingSample targetOutside));
            Assert.AreEqual(CharacterTestPathStatus.TargetOutsideGraph, targetOutside.Status);
        }

        [Test]
        public void FindPath_ReportsUnreachableAndClearsPreviousCorridor()
        {
            CharacterTestNavigationGraph graph = BuildGraph(
                new[] { Node(1, 0, 0), Node(2, 1, 0), Node(3, 2, 0) },
                new[] { Edge(1, 1, 2, 1000) });
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(3);

            Assert.IsTrue(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(1, 0), maxPathNodes: 3),
                buffer,
                out _));
            Assert.Greater(buffer.NodeCount, 0);
            Assert.IsFalse(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(2, 0), maxPathNodes: 3),
                buffer,
                out CharacterTestPathfindingSample sample));
            Assert.AreEqual(CharacterTestPathStatus.Unreachable, sample.Status);
            Assert.AreEqual(0, buffer.NodeCount);
        }

        [Test]
        public void FindPath_EnforcesExpansionBudget()
        {
            CharacterTestNavigationGraph graph = BuildLinearGraph(4);
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(4);

            Assert.IsFalse(pathfinder.TryFindPath(
                Query(
                    graph,
                    Position(0, 0),
                    Position(3, 0),
                    maxPathNodes: 4,
                    maxExpansions: 3),
                buffer,
                out CharacterTestPathfindingSample sample));

            Assert.AreEqual(CharacterTestPathStatus.BudgetExceeded, sample.Status);
            Assert.AreEqual(3, sample.ExpandedNodeCount);
            Assert.AreEqual(0, buffer.NodeCount);
        }

        [Test]
        public void FindPath_EnforcesCallerAndQueryPathCaps()
        {
            CharacterTestNavigationGraph graph = BuildLinearGraph(3);
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var undersizedBuffer = new CharacterTestNavigationPathBuffer(2);
            Assert.IsFalse(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(2, 0), maxPathNodes: 3),
                undersizedBuffer,
                out CharacterTestPathfindingSample callerCap));
            Assert.AreEqual(CharacterTestPathStatus.PathBufferTooSmall, callerCap.Status);

            var fullBuffer = new CharacterTestNavigationPathBuffer(3);
            Assert.IsFalse(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), Position(2, 0), maxPathNodes: 2),
                fullBuffer,
                out CharacterTestPathfindingSample queryCap));
            Assert.AreEqual(CharacterTestPathStatus.PathBufferTooSmall, queryCap.Status);
            Assert.AreEqual(0, fullBuffer.NodeCount);
        }

        [Test]
        public void FindPath_SameNodeReturnsExactTargetWaypoint()
        {
            CharacterTestNavigationGraph graph = BuildGraph(
                new[] { Node(1, 0, 0) },
                Array.Empty<CharacterTestNavigationEdgeDefinition>());
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(1);
            FixVector3 target = Position(Fix64.FromRatio(1, 4), Fix64.Zero);

            Assert.IsTrue(pathfinder.TryFindPath(
                Query(graph, Position(0, 0), target, maxPathNodes: 1),
                buffer,
                out CharacterTestPathfindingSample sample));

            Assert.AreEqual(1, buffer.NodeCount);
            Assert.AreEqual(0, buffer.EdgeCount);
            Assert.AreEqual(250, sample.TargetSnapDistanceMillimeters);
            Assert.AreEqual(target, sample.NextWaypoint);
        }

        [Test]
        public void FindPath_MatchesReferenceDijkstraAcrossDeterministicGrid()
        {
            const int Width = 5;
            const int Height = 5;
            var nodes = new CharacterTestNavigationNodeDefinition[Width * Height];
            var edges = new CharacterTestNavigationEdgeDefinition[
                ((Width - 1) * Height + (Height - 1) * Width) * 2];
            int edgeCursor = 0;
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int nodeId = z * Width + x + 1;
                    nodes[nodeId - 1] = Node(
                        nodeId,
                        x,
                        z,
                        traversalPenalty: (nodeId * 37) % 200);
                    if (x + 1 < Width)
                    {
                        int rightId = nodeId + 1;
                        edges[edgeCursor] = Edge(
                            edgeCursor + 1,
                            nodeId,
                            rightId,
                            1000,
                            traversalPenalty: ((edgeCursor + 1) * 17) % 100);
                        edgeCursor++;
                        edges[edgeCursor] = Edge(
                            edgeCursor + 1,
                            rightId,
                            nodeId,
                            1000,
                            traversalPenalty: ((edgeCursor + 1) * 17) % 100);
                        edgeCursor++;
                    }
                    if (z + 1 < Height)
                    {
                        int upId = nodeId + Width;
                        edges[edgeCursor] = Edge(
                            edgeCursor + 1,
                            nodeId,
                            upId,
                            1000,
                            traversalPenalty: ((edgeCursor + 1) * 17) % 100);
                        edgeCursor++;
                        edges[edgeCursor] = Edge(
                            edgeCursor + 1,
                            upId,
                            nodeId,
                            1000,
                            traversalPenalty: ((edgeCursor + 1) * 17) % 100);
                        edgeCursor++;
                    }
                }
            }

            CharacterTestNavigationGraph graph = BuildGraph(nodes, edges);
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(graph.NodeCount);
            int[] sources = { 1, 5, 7, 13, 21 };
            int[] targets = { 25, 21, 19, 3, 9 };
            for (int i = 0; i < sources.Length; i++)
            {
                Assert.IsTrue(graph.TryGetNodeIndex(sources[i], out int sourceIndex));
                Assert.IsTrue(graph.TryGetNodeIndex(targets[i], out int targetIndex));
                Assert.IsTrue(pathfinder.TryFindPath(
                    Query(
                        graph,
                        graph.GetNode(sourceIndex).Position,
                        graph.GetNode(targetIndex).Position,
                        maxPathNodes: graph.NodeCount,
                        maxExpansions: graph.NodeCount),
                    buffer,
                    out CharacterTestPathfindingSample sample));

                Assert.AreEqual(
                    ReferenceShortestCost(graph, sourceIndex, targetIndex),
                    sample.TotalCostMillimeters,
                    "Source=" + sources[i] + ", target=" + targets[i]);
                AssertCorridorConnected(graph, buffer, sources[i], targets[i]);
            }
        }

        [Test]
        public void FindPath_WarmedRepeatedQueriesAreNoAlloc()
        {
            CharacterTestNavigationGraph graph = BuildLinearGraph(6);
            var pathfinder = new CharacterTestBoundedAStarPathfinder(graph);
            var buffer = new CharacterTestNavigationPathBuffer(6);
            CharacterTestPathQuery query = Query(
                graph,
                Position(0, 0),
                Position(5, 0),
                maxPathNodes: 6,
                maxExpansions: 6);
            Assert.IsTrue(pathfinder.TryFindPath(query, buffer, out _));
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                Assert.IsTrue(pathfinder.TryFindPath(query, buffer, out _));

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static CharacterTestNavigationGraph BuildLinearGraph(int nodeCount)
        {
            var nodes = new CharacterTestNavigationNodeDefinition[nodeCount];
            var edges = new CharacterTestNavigationEdgeDefinition[nodeCount - 1];
            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = Node(i + 1, i, 0);
                if (i > 0)
                    edges[i - 1] = Edge(i, i, i + 1, 1000);
            }

            return BuildGraph(nodes, edges);
        }

        private static long ReferenceShortestCost(
            CharacterTestNavigationGraph graph,
            int sourceNodeIndex,
            int targetNodeIndex)
        {
            var costs = new long[graph.NodeCount];
            var visited = new bool[graph.NodeCount];
            for (int i = 0; i < costs.Length; i++)
                costs[i] = long.MaxValue;
            costs[sourceNodeIndex] = 0;
            for (int iteration = 0; iteration < graph.NodeCount; iteration++)
            {
                int current = -1;
                for (int i = 0; i < graph.NodeCount; i++)
                {
                    if (!visited[i]
                        && (current < 0
                            || costs[i] < costs[current]
                            || (costs[i] == costs[current]
                                && graph.GetNode(i).NodeId < graph.GetNode(current).NodeId)))
                    {
                        current = i;
                    }
                }

                if (current < 0 || costs[current] == long.MaxValue)
                    break;
                if (current == targetNodeIndex)
                    return costs[current];
                visited[current] = true;
                int outgoingCount = graph.GetOutgoingEdgeCount(current);
                for (int edgeIndex = 0; edgeIndex < outgoingCount; edgeIndex++)
                {
                    CharacterTestNavigationGraphEdge edge = graph.GetOutgoingEdge(current, edgeIndex);
                    long candidate = costs[current]
                        + edge.TotalCostMillimeters
                        + graph.GetNode(edge.ToNodeIndex).TraversalPenaltyMillimeters;
                    if (candidate < costs[edge.ToNodeIndex])
                        costs[edge.ToNodeIndex] = candidate;
                }
            }

            return long.MaxValue;
        }

        private static void AssertCorridorConnected(
            CharacterTestNavigationGraph graph,
            CharacterTestNavigationPathBuffer buffer,
            int expectedSourceNodeId,
            int expectedTargetNodeId)
        {
            Assert.AreEqual(expectedSourceNodeId, buffer.GetNodeId(0));
            Assert.AreEqual(expectedTargetNodeId, buffer.GetNodeId(buffer.NodeCount - 1));
            Assert.AreEqual(buffer.NodeCount - 1, buffer.EdgeCount);
            for (int i = 0; i < buffer.EdgeCount; i++)
            {
                int edgeId = buffer.GetEdgeId(i);
                CharacterTestNavigationGraphEdge edge = default;
                bool found = false;
                for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
                {
                    CharacterTestNavigationGraphEdge candidate = graph.GetEdge(edgeIndex);
                    if (candidate.EdgeId != edgeId)
                        continue;
                    edge = candidate;
                    found = true;
                    break;
                }

                Assert.IsTrue(found, "Missing edge " + edgeId);
                Assert.AreEqual(buffer.GetNodeId(i), edge.FromNodeId);
                Assert.AreEqual(buffer.GetNodeId(i + 1), edge.ToNodeId);
            }
        }

        private static CharacterTestNavigationGraph BuildGraph(
            CharacterTestNavigationNodeDefinition[] nodes,
            CharacterTestNavigationEdgeDefinition[] edges)
        {
            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                1,
                nodes,
                edges,
                out CharacterTestNavigationGraph graph,
                out CharacterTestNavigationGraphBuildResult result),
                "Graph build failed: " + result.Status);
            return graph;
        }

        private static CharacterTestPathQuery Query(
            CharacterTestNavigationGraph graph,
            FixVector3 source,
            FixVector3 target,
            int maxPathNodes,
            int maxExpansions = 32,
            int maxSnap = 2000,
            int clearance = 500,
            int hardBlockedAreaTagMask = 0,
            int activeBlockedLayerMask = 0)
        {
            return new CharacterTestPathQuery(
                graph.Revision,
                graph.ContentHash,
                source,
                target,
                clearance,
                clearanceHeightMillimeters: 1800,
                maxSnap,
                maxExpansions,
                maxPathNodes,
                hardBlockedAreaTagMask,
                activeBlockedLayerMask);
        }

        private static CharacterTestNavigationNodeDefinition Node(
            int id,
            int x,
            int z,
            int clearance = 1000,
            int traversalPenalty = 0,
            int areaTagMask = 0)
        {
            return new CharacterTestNavigationNodeDefinition(
                id,
                Position(x, z),
                clearance,
                clearanceHeightMillimeters: 2200,
                traversalPenalty,
                areaTagMask);
        }

        private static CharacterTestNavigationEdgeDefinition Edge(
            int id,
            int from,
            int to,
            int length,
            int clearance = 1000,
            int traversalPenalty = 0,
            int blockedLayerMask = 0,
            int areaTagMask = 0)
        {
            return new CharacterTestNavigationEdgeDefinition(
                id,
                from,
                to,
                length,
                clearance,
                clearanceHeightMillimeters: 2200,
                traversalPenalty,
                blockedLayerMask,
                areaTagMask);
        }

        private static FixVector3 Position(int x, int z)
        {
            return Position(Fix64.FromInt(x), Fix64.FromInt(z));
        }

        private static FixVector3 Position(Fix64 x, Fix64 z)
        {
            return new FixVector3(x, Fix64.Zero, z);
        }
    }
}
