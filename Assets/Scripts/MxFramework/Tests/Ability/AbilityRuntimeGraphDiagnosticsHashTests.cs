using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class AbilityRuntimeGraphDiagnosticsHashTests
    {
        [Test]
        public void HashContributor_SameDefinition_IsStableAcrossInputOrder()
        {
            AbilityGraphDefinition first = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);
            AbilityGraphDefinition second = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "effect",
                reversedInputOrder: true);

            long firstHash = AbilityGraphHashContributor.ComputeDefinitionHash(first);
            long firstHashAgain = AbilityGraphHashContributor.ComputeDefinitionHash(first);
            long secondHash = AbilityGraphHashContributor.ComputeDefinitionHash(second);

            Assert.AreEqual(firstHash, firstHashAgain);
            Assert.AreEqual(firstHash, secondHash);
        }

        [Test]
        public void HashContributor_PayloadChange_ChangesDefinitionHash()
        {
            AbilityGraphDefinition first = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);
            AbilityGraphDefinition second = CreateStrikeGraph(
                effectId: 1002,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);

            long firstHash = AbilityGraphHashContributor.ComputeDefinitionHash(first);
            long secondHash = AbilityGraphHashContributor.ComputeDefinitionHash(second);

            Assert.AreNotEqual(firstHash, secondHash);
        }

        [Test]
        public void HashContributor_EdgeTargetChange_ChangesDefinitionHash()
        {
            AbilityGraphDefinition first = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);
            AbilityGraphDefinition second = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "event",
                reversedInputOrder: false);

            long firstHash = AbilityGraphHashContributor.ComputeDefinitionHash(first);
            long secondHash = AbilityGraphHashContributor.ComputeDefinitionHash(second);

            Assert.AreNotEqual(firstHash, secondHash);
        }

        [Test]
        public void HashContributor_RuntimeCombinerContribution_IsStable()
        {
            AbilityGraphDefinition graph = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);
            var contributor = new AbilityGraphHashContributor(graph);

            long firstHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { contributor });
            long secondHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { contributor });

            Assert.AreEqual(AbilityGraphHashContributor.StableContributorId, contributor.ContributorId);
            Assert.AreEqual(firstHash, secondHash);
        }

        [Test]
        public void ExecutionTrace_ToStableString_UsesRecordedNodeOrder()
        {
            AbilityGraphDefinition graph = CreateStrikeGraph(
                effectId: 1001,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);

            AbilityGraphExecutionTrace first = CreateSuccessfulTrace(graph);
            AbilityGraphExecutionTrace second = CreateSuccessfulTrace(graph);

            Assert.AreEqual(first.ToStableString(), second.ToStableString());
            Assert.AreEqual(4, first.Nodes.Count);
            Assert.AreEqual("entry", first.Nodes[0].NodeId);
            Assert.AreEqual("target", first.Nodes[1].NodeId);
            Assert.AreEqual("effect", first.Nodes[2].NodeId);
            Assert.AreEqual("event", first.Nodes[3].NodeId);
            Assert.AreEqual(1, first.TargetDecisions.Count);
            Assert.AreEqual(1, first.EmittedEvents.Count);
            StringAssert.Contains("node[2]=2|effect|3:ApplyEffect|port=next|result=applied", first.ToStableString());
        }

        [Test]
        public void ExecutionTrace_TargetRejectedAndMissingEffect_AreStructured()
        {
            AbilityGraphDefinition graph = CreateStrikeGraph(
                effectId: 404,
                targetOutputNodeId: "effect",
                reversedInputOrder: false);

            AbilityGraphExecutionTrace trace = new AbilityGraphExecutionTraceBuilder(graph)
                .RecordNode("entry", AbilityGraphNodeKind.Entry, AbilityGraphPorts.Next, "ok")
                .RecordNode("target", AbilityGraphNodeKind.TargetQuery, AbilityGraphPorts.Failure, "no-target")
                .RecordTargetRejected("target", 7, GameplayTargetRejectReason.Dead, message: "candidate is dead")
                .RecordNode("effect", AbilityGraphNodeKind.ApplyEffect, AbilityGraphPorts.Failure, "missing-effect")
                .RecordMissingEffect("effect", 404)
                .Build();

            Assert.IsFalse(trace.Success);
            Assert.AreEqual(AbilityGraphTraceFailureCode.MissingEffect, trace.FailureCode);
            Assert.AreEqual("effect", trace.FailureNodeId);
            Assert.AreEqual(1, trace.TargetDecisions.Count);
            Assert.AreEqual(GameplayTargetRejectReason.Dead, trace.TargetDecisions[0].RejectReason);
            StringAssert.Contains("target[0]=0|target|entity=7|accepted=0|reason=2:Dead", trace.ToStableString());
            StringAssert.Contains("missing effect id 404", trace.ToStableString());
        }

        [Test]
        public void DiagnosticSnapshot_ValidationFailure_EntersDiagnostics()
        {
            var graph = new AbilityGraphDefinition(
                "invalid-graph",
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
            var builder = new AbilityGraphDiagnosticSnapshotBuilder();

            AbilityGraphDiagnosticSnapshot snapshot = builder.Build(graph);

            Assert.AreEqual("invalid-graph", snapshot.GraphId);
            Assert.AreEqual(AbilityGraphDefinition.CurrentVersion, snapshot.GraphVersion);
            Assert.AreEqual("entry", snapshot.EntryNodeId);
            Assert.AreEqual(2, snapshot.NodeCount);
            Assert.AreEqual(1, snapshot.EdgeCount);
            Assert.IsFalse(snapshot.Validation.IsValid);
            Assert.AreEqual(1, snapshot.Validation.ErrorCount);
            Assert.AreEqual(1, snapshot.ValidationErrors.Count);
            Assert.AreEqual(AbilityGraphValidationErrorCode.InvalidNodePayload, snapshot.ValidationErrors[0].Code);
            Assert.AreEqual("effect", snapshot.ValidationErrors[0].NodeId);
            Assert.AreEqual("Nodes[effect].Payload.EffectId", snapshot.ValidationErrors[0].FieldPath);
        }

        private static AbilityGraphExecutionTrace CreateSuccessfulTrace(AbilityGraphDefinition graph)
        {
            return new AbilityGraphExecutionTraceBuilder(graph)
                .RecordNode("entry", AbilityGraphNodeKind.Entry, AbilityGraphPorts.Next, "ok")
                .RecordNode("target", AbilityGraphNodeKind.TargetQuery, AbilityGraphPorts.Next, "selected")
                .RecordTargetDecision("target", 2, true)
                .RecordNode("effect", AbilityGraphNodeKind.ApplyEffect, AbilityGraphPorts.Next, "applied")
                .RecordNode("event", AbilityGraphNodeKind.EmitEvent, string.Empty, "emitted")
                .RecordEvent("event", AbilityEventType.CastFinished, abilityId: 11, casterEntityId: 1, targetEntityId: 2)
                .Build();
        }

        private static AbilityGraphDefinition CreateStrikeGraph(
            int effectId,
            string targetOutputNodeId,
            bool reversedInputOrder)
        {
            var nodes = new[]
            {
                Node("target", AbilityGraphNodeKind.TargetQuery, new AbilityGraphTargetQueryPayload(
                    GameplayTargetRelationFilter.Enemy,
                    requireAlive: true,
                    requiredTags: new[] { 20, 10 },
                    blockedStatuses: new[] { 300 },
                    maxTargets: 1)),
                Node("event", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.CastFinished)),
                Node("entry", AbilityGraphNodeKind.Entry),
                Node("effect", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(effectId)),
            };
            var reversedNodes = new[]
            {
                nodes[3],
                nodes[2],
                nodes[1],
                nodes[0],
            };
            var edges = new[]
            {
                Edge("target", AbilityGraphPorts.Next, targetOutputNodeId),
                Edge("entry", AbilityGraphPorts.Next, "target"),
                Edge("effect", AbilityGraphPorts.Next, "event"),
            };
            var reversedEdges = new[]
            {
                edges[2],
                edges[1],
                edges[0],
            };

            return new AbilityGraphDefinition(
                "strike",
                "entry",
                reversedInputOrder ? reversedNodes : nodes,
                reversedInputOrder ? reversedEdges : edges);
        }

        private static AbilityGraphNode Node(string nodeId, AbilityGraphNodeKind kind, object payload = null)
        {
            return new AbilityGraphNode(nodeId, kind, payload);
        }

        private static AbilityGraphEdge Edge(string fromNodeId, string outputPort, string toNodeId)
        {
            return new AbilityGraphEdge(fromNodeId, outputPort, toNodeId);
        }
    }
}
