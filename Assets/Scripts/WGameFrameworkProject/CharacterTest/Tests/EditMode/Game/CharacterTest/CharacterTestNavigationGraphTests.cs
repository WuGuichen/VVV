using System;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNavigationGraphTests
    {
        [Test]
        public void Build_CanonicalizesInputAndOutgoingEdges()
        {
            CharacterTestNavigationNodeDefinition[] nodes =
            {
                Node(30, 3, 0),
                Node(10, 1, 0),
                Node(20, 2, 0)
            };
            CharacterTestNavigationEdgeDefinition[] edges =
            {
                Edge(300, 10, 30, 3000),
                Edge(100, 10, 20, 1000),
                Edge(200, 20, 30, 1000)
            };

            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                revision: 7,
                nodes,
                edges,
                out CharacterTestNavigationGraph graph,
                out CharacterTestNavigationGraphBuildResult result));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(7, graph.Revision);
            Assert.AreEqual(3, graph.NodeCount);
            Assert.AreEqual(3, graph.EdgeCount);
            Assert.AreEqual(10, graph.GetNode(0).NodeId);
            Assert.AreEqual(20, graph.GetNode(1).NodeId);
            Assert.AreEqual(30, graph.GetNode(2).NodeId);
            Assert.AreEqual(100, graph.GetEdge(0).EdgeId);
            Assert.AreEqual(200, graph.GetEdge(1).EdgeId);
            Assert.AreEqual(300, graph.GetEdge(2).EdgeId);
            Assert.AreEqual(2, graph.GetOutgoingEdgeCount(0));
            Assert.AreEqual(100, graph.GetOutgoingEdge(0, 0).EdgeId);
            Assert.AreEqual(300, graph.GetOutgoingEdge(0, 1).EdgeId);

            Array.Reverse(nodes);
            Array.Reverse(edges);
            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                revision: 7,
                nodes,
                edges,
                out CharacterTestNavigationGraph reordered,
                out _));
            Assert.AreEqual(graph.ContentHash, reordered.ContentHash);
            Assert.AreEqual(
                graph.GetOutgoingEdge(0, 0).EdgeId,
                reordered.GetOutgoingEdge(0, 0).EdgeId);
        }

        [Test]
        public void Build_CopiesInputAndKeepsRevisionSeparateFromContentHash()
        {
            CharacterTestNavigationNodeDefinition[] nodes =
            {
                Node(1, 0, 0),
                Node(2, 1, 0)
            };
            CharacterTestNavigationEdgeDefinition[] edges =
            {
                Edge(11, 1, 2, 1000)
            };

            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                3, nodes, edges, out CharacterTestNavigationGraph first, out _));
            nodes[0] = Node(99, 99, 99);
            edges[0] = Edge(99, 99, 99, 9999);

            Assert.AreEqual(1, first.GetNode(0).NodeId);
            Assert.AreEqual(11, first.GetEdge(0).EdgeId);

            CharacterTestNavigationNodeDefinition[] originalNodes =
            {
                Node(1, 0, 0),
                Node(2, 1, 0)
            };
            CharacterTestNavigationEdgeDefinition[] originalEdges =
            {
                Edge(11, 1, 2, 1000)
            };
            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                4, originalNodes, originalEdges, out CharacterTestNavigationGraph revised, out _));
            Assert.AreEqual(4, revised.Revision);
            Assert.AreEqual(first.ContentHash, revised.ContentHash);

            originalEdges[0] = new CharacterTestNavigationEdgeDefinition(
                11, 1, 2, 1000, 700, 2000, traversalPenaltyMillimeters: 1);
            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                4, originalNodes, originalEdges, out CharacterTestNavigationGraph changed, out _));
            Assert.AreNotEqual(first.ContentHash, changed.ContentHash);
        }

        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidRevision)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.MissingNodes)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidNodeId)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.DuplicateNodeId)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidNodeClearance)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidNodePenalty)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidEdgeId)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.DuplicateEdgeId)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.MissingEdgeEndpoint)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.SelfEdge)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidEdgeLength)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidEdgeClearance)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidEdgePenalty)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.EdgeLengthBelowGeometricDistance)]
        [TestCase((int)CharacterTestNavigationGraphBuildStatus.InvalidNodePosition)]
        public void Build_RejectsInvalidGraphWithTypedStatus(int expectedValue)
        {
            var expected = (CharacterTestNavigationGraphBuildStatus)expectedValue;
            CreateInvalidInput(
                expected,
                out int revision,
                out CharacterTestNavigationNodeDefinition[] nodes,
                out CharacterTestNavigationEdgeDefinition[] edges);

            Assert.IsFalse(CharacterTestNavigationGraphBuilder.TryBuild(
                revision,
                nodes,
                edges,
                out CharacterTestNavigationGraph graph,
                out CharacterTestNavigationGraphBuildResult result));
            Assert.IsNull(graph);
            Assert.AreEqual(expected, result.Status);
        }

        [Test]
        public void NearestNode_FiltersClearanceAndUsesStableIdTieBreak()
        {
            CharacterTestNavigationNodeDefinition[] nodes =
            {
                new CharacterTestNavigationNodeDefinition(
                    20, Position(1, 0), 800, 2000),
                new CharacterTestNavigationNodeDefinition(
                    10, Position(-1, 0), 800, 2000),
                new CharacterTestNavigationNodeDefinition(
                    5, Position(0, 0), 200, 1000)
            };
            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                1,
                nodes,
                Array.Empty<CharacterTestNavigationEdgeDefinition>(),
                out CharacterTestNavigationGraph graph,
                out _));

            Assert.IsTrue(graph.TryFindNearestNode(
                FixVector3.Zero,
                requiredClearanceRadiusMillimeters: 500,
                requiredClearanceHeightMillimeters: 1800,
                out int nodeIndex,
                out Fix64 distanceSquared));
            Assert.AreEqual(10, graph.GetNode(nodeIndex).NodeId);
            Assert.AreEqual(Fix64.One, distanceSquared);

            Assert.IsFalse(graph.TryFindNearestNode(
                FixVector3.Zero,
                requiredClearanceRadiusMillimeters: 900,
                requiredClearanceHeightMillimeters: 2100,
                out _,
                out _));
        }

        [Test]
        public void RuntimeReads_AreNoAllocAfterInitialization()
        {
            CharacterTestNavigationNodeDefinition[] nodes =
            {
                Node(1, 0, 0),
                Node(2, 1, 0),
                Node(3, 2, 0)
            };
            CharacterTestNavigationEdgeDefinition[] edges =
            {
                Edge(1, 1, 2, 1000),
                Edge(2, 2, 3, 1000)
            };
            Assert.IsTrue(CharacterTestNavigationGraphBuilder.TryBuild(
                1, nodes, edges, out CharacterTestNavigationGraph graph, out _));

            Assert.IsTrue(graph.TryGetNodeIndex(2, out _));
            Assert.IsTrue(graph.TryFindNearestNode(
                Position(1, 0), 500, 1800, out _, out _));
            _ = graph.GetOutgoingEdge(0, 0);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
            {
                Assert.IsTrue(graph.TryGetNodeIndex(2, out int nodeIndex));
                Assert.IsTrue(graph.TryFindNearestNode(
                    Position(1, 0), 500, 1800, out _, out _));
                _ = graph.GetNode(nodeIndex);
                _ = graph.GetOutgoingEdge(0, 0);
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static void CreateInvalidInput(
            CharacterTestNavigationGraphBuildStatus status,
            out int revision,
            out CharacterTestNavigationNodeDefinition[] nodes,
            out CharacterTestNavigationEdgeDefinition[] edges)
        {
            revision = 1;
            nodes = new[] { Node(1, 0, 0), Node(2, 1, 0) };
            edges = new[] { Edge(1, 1, 2, 1000) };
            switch (status)
            {
                case CharacterTestNavigationGraphBuildStatus.InvalidRevision:
                    revision = 0;
                    break;
                case CharacterTestNavigationGraphBuildStatus.MissingNodes:
                    nodes = Array.Empty<CharacterTestNavigationNodeDefinition>();
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidNodeId:
                    nodes[0] = Node(0, 0, 0);
                    break;
                case CharacterTestNavigationGraphBuildStatus.DuplicateNodeId:
                    nodes[1] = Node(1, 1, 0);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidNodeClearance:
                    nodes[0] = new CharacterTestNavigationNodeDefinition(1, Position(0, 0), 0, 2000);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidNodePenalty:
                    nodes[0] = new CharacterTestNavigationNodeDefinition(1, Position(0, 0), 700, 2000, -1);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidEdgeId:
                    edges[0] = Edge(0, 1, 2, 1000);
                    break;
                case CharacterTestNavigationGraphBuildStatus.DuplicateEdgeId:
                    edges = new[] { Edge(1, 1, 2, 1000), Edge(1, 2, 1, 1000) };
                    break;
                case CharacterTestNavigationGraphBuildStatus.MissingEdgeEndpoint:
                    edges[0] = Edge(1, 1, 99, 1000);
                    break;
                case CharacterTestNavigationGraphBuildStatus.SelfEdge:
                    edges[0] = Edge(1, 1, 1, 1000);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidEdgeLength:
                    edges[0] = Edge(1, 1, 2, 0);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidEdgeClearance:
                    edges[0] = new CharacterTestNavigationEdgeDefinition(1, 1, 2, 1000, 0, 2000);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidEdgePenalty:
                    edges[0] = new CharacterTestNavigationEdgeDefinition(1, 1, 2, 1000, 700, 2000, -1);
                    break;
                case CharacterTestNavigationGraphBuildStatus.EdgeLengthBelowGeometricDistance:
                    edges[0] = Edge(1, 1, 2, 999);
                    break;
                case CharacterTestNavigationGraphBuildStatus.InvalidNodePosition:
                    nodes[0] = new CharacterTestNavigationNodeDefinition(
                        1,
                        new FixVector3(Fix64.MaxValue, Fix64.Zero, Fix64.Zero),
                        700,
                        2000);
                    break;
                default:
                    Assert.Fail("Unsupported invalid graph fixture: " + status);
                    break;
            }
        }

        private static CharacterTestNavigationNodeDefinition Node(int id, int x, int z)
        {
            return new CharacterTestNavigationNodeDefinition(
                id,
                Position(x, z),
                clearanceRadiusMillimeters: 700,
                clearanceHeightMillimeters: 2000);
        }

        private static CharacterTestNavigationEdgeDefinition Edge(
            int id,
            int from,
            int to,
            int lengthMillimeters)
        {
            return new CharacterTestNavigationEdgeDefinition(
                id,
                from,
                to,
                lengthMillimeters,
                clearanceRadiusMillimeters: 700,
                clearanceHeightMillimeters: 2000);
        }

        private static FixVector3 Position(int x, int z)
        {
            return new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.FromInt(z));
        }
    }
}
