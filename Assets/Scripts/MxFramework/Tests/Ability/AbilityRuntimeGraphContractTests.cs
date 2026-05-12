using System;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class AbilityRuntimeGraphContractTests
    {
        [Test]
        public void Validate_MinimalGraph_IsValidAndExposesStableOrder()
        {
            var graph = new AbilityGraphDefinition(
                "strike",
                "entry",
                new[]
                {
                    Node("target", AbilityGraphNodeKind.TargetQuery, new AbilityGraphTargetQueryPayload(GameplayTargetRelationFilter.Enemy, maxTargets: 1)),
                    Node("event", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.CastFinished)),
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("effect", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(1001)),
                },
                new[]
                {
                    Edge("target", AbilityGraphPorts.Next, "effect"),
                    Edge("entry", AbilityGraphPorts.Next, "target"),
                    Edge("effect", AbilityGraphPorts.Next, "event"),
                });

            AbilityGraphValidationResult result = graph.Validate();

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.ErrorCount);
            Assert.AreEqual("effect", graph.Nodes[0].NodeId);
            Assert.AreEqual("entry", graph.Nodes[1].NodeId);
            Assert.AreEqual("event", graph.Nodes[2].NodeId);
            Assert.AreEqual("target", graph.Nodes[3].NodeId);
            Assert.AreEqual("effect", graph.Edges[0].FromNodeId);
            Assert.AreEqual("entry", graph.Edges[1].FromNodeId);
            Assert.AreEqual("target", graph.Edges[2].FromNodeId);
        }

        [Test]
        public void Validate_DuplicateNodeId_ReturnsStructuredError()
        {
            var graph = new AbilityGraphDefinition(
                "duplicate-node",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("entry", AbilityGraphNodeKind.Sequence),
                },
                Array.Empty<AbilityGraphEdge>());

            AbilityGraphValidationResult result = graph.Validate();

            AssertSingleError(result, AbilityGraphValidationErrorCode.DuplicateNodeId);
            Assert.AreEqual("entry", result.Errors[0].NodeId);
            Assert.AreEqual(-1, result.Errors[0].EdgeIndex);
        }

        [Test]
        public void Validate_MissingEntry_ReturnsStructuredError()
        {
            var graph = new AbilityGraphDefinition(
                "missing-entry",
                "entry",
                new[]
                {
                    Node("sequence", AbilityGraphNodeKind.Sequence),
                },
                Array.Empty<AbilityGraphEdge>());

            AbilityGraphValidationResult result = graph.Validate();

            AssertSingleError(result, AbilityGraphValidationErrorCode.MissingEntryNode);
            Assert.AreEqual("entry", result.Errors[0].NodeId);
            Assert.AreEqual("EntryNodeId", result.Errors[0].FieldPath);
        }

        [Test]
        public void Validate_UnresolvedEdgeEndpoint_ReturnsStructuredError()
        {
            var graph = new AbilityGraphDefinition(
                "unresolved-edge",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("effect", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(1001)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "missing"),
                });

            AbilityGraphValidationResult result = graph.Validate();

            AssertSingleError(result, AbilityGraphValidationErrorCode.UnresolvedEdgeEndpoint);
            Assert.AreEqual("missing", result.Errors[0].NodeId);
            Assert.AreEqual(0, result.Errors[0].EdgeIndex);
            Assert.AreEqual("Edges[0].ToNodeId", result.Errors[0].FieldPath);
        }

        [Test]
        public void Validate_Cycle_ReturnsStableStructuredError()
        {
            var graph = new AbilityGraphDefinition(
                "cycle",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "entry"),
                });

            AbilityGraphValidationResult first = graph.Validate();
            AbilityGraphValidationResult second = graph.Validate();

            AssertSingleError(first, AbilityGraphValidationErrorCode.CycleDetected);
            AssertSingleError(second, AbilityGraphValidationErrorCode.CycleDetected);
            Assert.AreEqual(first.Errors[0].Code, second.Errors[0].Code);
            Assert.AreEqual(first.Errors[0].NodeId, second.Errors[0].NodeId);
            Assert.AreEqual(first.Errors[0].EdgeIndex, second.Errors[0].EdgeIndex);
            Assert.AreEqual("entry", first.Errors[0].NodeId);
            Assert.AreEqual(0, first.Errors[0].EdgeIndex);
        }

        [Test]
        public void Validate_InvalidNodePayload_ReturnsStructuredError()
        {
            var graph = new AbilityGraphDefinition(
                "invalid-payload",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("effect", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(0)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "effect"),
                });

            AbilityGraphValidationResult result = graph.Validate();

            AssertSingleError(result, AbilityGraphValidationErrorCode.InvalidNodePayload);
            Assert.AreEqual("effect", result.Errors[0].NodeId);
            Assert.AreEqual("Nodes[effect].Payload.EffectId", result.Errors[0].FieldPath);
        }

        private static AbilityGraphNode Node(string nodeId, AbilityGraphNodeKind kind, object payload = null)
        {
            return new AbilityGraphNode(nodeId, kind, payload);
        }

        private static AbilityGraphEdge Edge(string fromNodeId, string outputPort, string toNodeId)
        {
            return new AbilityGraphEdge(fromNodeId, outputPort, toNodeId);
        }

        private static void AssertSingleError(AbilityGraphValidationResult result, AbilityGraphValidationErrorCode code)
        {
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.ErrorCount);
            Assert.AreEqual(code, result.Errors[0].Code);
        }
    }
}
