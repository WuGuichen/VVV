using System.Collections.Generic;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public sealed class AbilityGraphConfigMappingTests
    {
        [Test]
        public void Map_ValidSyntheticConfig_CreatesRuntimeDefinition()
        {
            AbilityGraphConfig config = CreateStrikeConfig();

            AbilityGraphConfigMappingResult result = AbilityGraphConfigMapper.Map(config);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.DiagnosticCount);
            Assert.IsNotNull(result.Definition);
            Assert.AreEqual("strike", result.Definition.GraphId);
            Assert.AreEqual(AbilityGraphConfig.CurrentVersion, result.Definition.Version);
            Assert.AreEqual("entry", result.Definition.EntryNodeId);

            AbilityGraphNode targetNode = FindNode(result.Definition.Nodes, "target");
            Assert.AreEqual(AbilityGraphNodeKind.TargetQuery, targetNode.Kind);
            var targetPayload = (AbilityGraphTargetQueryPayload)targetNode.Payload;
            Assert.AreEqual(GameplayTargetRelationFilter.Enemy, targetPayload.RelationFilter);
            Assert.IsTrue(targetPayload.RequireAlive);
            Assert.AreEqual(1, targetPayload.MaxTargets);
            CollectionAssert.AreEqual(new[] { 101, 102 }, targetPayload.RequiredTags);
            CollectionAssert.AreEqual(new[] { 201 }, targetPayload.BlockedStatuses);

            AbilityGraphNode effectNode = FindNode(result.Definition.Nodes, "effect");
            Assert.AreEqual(9001, ((AbilityGraphApplyEffectPayload)effectNode.Payload).EffectId);

            AbilityGraphNode eventNode = FindNode(result.Definition.Nodes, "event");
            Assert.AreEqual(AbilityEventType.CastFinished, ((AbilityGraphEmitEventPayload)eventNode.Payload).EventType);
        }

        [Test]
        public void Map_NodeAndEdgeOrder_IsStable()
        {
            AbilityGraphConfig firstConfig = CreateStrikeConfig();
            AbilityGraphConfig secondConfig = CreateStrikeConfig();

            AbilityGraphConfigMappingResult first = AbilityGraphConfigMapper.Map(firstConfig);
            AbilityGraphConfigMappingResult second = AbilityGraphConfigMapper.Map(secondConfig);

            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            CollectionAssert.AreEqual(
                new[] { "effect", "entry", "event", "target" },
                NodeIds(first.Definition.Nodes));
            CollectionAssert.AreEqual(NodeIds(first.Definition.Nodes), NodeIds(second.Definition.Nodes));
            CollectionAssert.AreEqual(
                new[] { "effect", "entry", "target" },
                EdgeFromNodeIds(first.Definition.Edges));
            CollectionAssert.AreEqual(EdgeFromNodeIds(first.Definition.Edges), EdgeFromNodeIds(second.Definition.Edges));
        }

        [Test]
        public void Map_UnresolvedNodeId_ReturnsDiagnosticWithConfigPath()
        {
            var config = new AbilityGraphConfig(
                "broken-edge",
                "entry",
                new[]
                {
                    AbilityGraphNodeConfig.Entry("entry"),
                    AbilityGraphNodeConfig.CreateTargetQuery(
                        "target",
                        new AbilityGraphTargetQueryPayloadConfig(GameplayTargetRelationFilter.Enemy, maxTargets: 1)),
                    AbilityGraphNodeConfig.CreateApplyEffect("effect", new AbilityGraphApplyEffectPayloadConfig(9001))
                },
                new[]
                {
                    new AbilityGraphEdgeConfig("target", AbilityGraphPorts.Next, "effect"),
                    new AbilityGraphEdgeConfig("entry", AbilityGraphPorts.Next, "missing")
                });

            AbilityGraphConfigMappingResult result = AbilityGraphConfigMapper.Map(config, "synthetic://broken-edge");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.DiagnosticCount);
            AbilityGraphConfigMappingDiagnostic diagnostic = result.Diagnostics[0];
            Assert.AreEqual(AbilityGraphConfigMappingDiagnosticCode.RuntimeValidationError, diagnostic.Code);
            Assert.IsTrue(diagnostic.HasRuntimeValidationError);
            Assert.AreEqual(AbilityGraphValidationErrorCode.UnresolvedEdgeEndpoint, diagnostic.RuntimeValidationCode);
            Assert.AreEqual("synthetic://broken-edge", diagnostic.SourcePath);
            Assert.AreEqual("Edges[1].ToNodeId", diagnostic.FieldPath);
            Assert.AreEqual("Edges[0].ToNodeId", diagnostic.RuntimeFieldPath);
            Assert.AreEqual("missing", diagnostic.NodeId);
        }

        [Test]
        public void Map_InvalidPayload_ReturnsDiagnosticWithConfigPath()
        {
            var config = new AbilityGraphConfig(
                "invalid-payload",
                "entry",
                new[]
                {
                    AbilityGraphNodeConfig.Entry("entry"),
                    AbilityGraphNodeConfig.CreateApplyEffect("effect", new AbilityGraphApplyEffectPayloadConfig(0))
                },
                new[]
                {
                    new AbilityGraphEdgeConfig("entry", AbilityGraphPorts.Next, "effect")
                },
                sourcePath: "synthetic://invalid-payload");

            AbilityGraphConfigMappingResult result = AbilityGraphConfigMapper.Map(config);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.DiagnosticCount);
            AbilityGraphConfigMappingDiagnostic diagnostic = result.Diagnostics[0];
            Assert.AreEqual(AbilityGraphValidationErrorCode.InvalidNodePayload, diagnostic.RuntimeValidationCode);
            Assert.AreEqual("synthetic://invalid-payload", diagnostic.SourcePath);
            Assert.AreEqual("Nodes[1].ApplyEffect.EffectId", diagnostic.FieldPath);
            Assert.AreEqual("Nodes[effect].Payload.EffectId", diagnostic.RuntimeFieldPath);
            Assert.AreEqual("effect", diagnostic.NodeId);
        }

        [Test]
        public void Map_DoesNotChangeInputConfigCollections()
        {
            var requiredTags = new[] { 101, 102 };
            var blockedStatuses = new[] { 201 };
            var targetPayload = new AbilityGraphTargetQueryPayloadConfig(
                GameplayTargetRelationFilter.Enemy,
                requiredTags: requiredTags,
                blockedStatuses: blockedStatuses,
                maxTargets: 1);
            var nodes = new[]
            {
                AbilityGraphNodeConfig.CreateTargetQuery("target", targetPayload),
                AbilityGraphNodeConfig.Entry("entry"),
                AbilityGraphNodeConfig.CreateApplyEffect("effect", new AbilityGraphApplyEffectPayloadConfig(9001))
            };
            var edges = new[]
            {
                new AbilityGraphEdgeConfig("target", AbilityGraphPorts.Next, "effect"),
                new AbilityGraphEdgeConfig("entry", AbilityGraphPorts.Next, "target")
            };
            var config = new AbilityGraphConfig
            {
                SourcePath = "synthetic://mutable",
                Id = "mutable",
                EntryNodeId = "entry",
                Nodes = nodes,
                Edges = edges
            };

            AbilityGraphConfigMapper.Map(config);

            Assert.AreSame(nodes, config.Nodes);
            Assert.AreSame(edges, config.Edges);
            Assert.AreSame(targetPayload, config.Nodes[0].TargetQuery);
            CollectionAssert.AreEqual(new[] { "target", "entry", "effect" }, ConfigNodeIds(config.Nodes));
            CollectionAssert.AreEqual(new[] { "target", "entry" }, ConfigEdgeFromNodeIds(config.Edges));
            CollectionAssert.AreEqual(new[] { 101, 102 }, targetPayload.RequiredTags);
            CollectionAssert.AreEqual(new[] { 201 }, targetPayload.BlockedStatuses);
        }

        private static AbilityGraphConfig CreateStrikeConfig()
        {
            return new AbilityGraphConfig(
                "strike",
                "entry",
                new[]
                {
                    AbilityGraphNodeConfig.CreateTargetQuery(
                        "target",
                        new AbilityGraphTargetQueryPayloadConfig(
                            GameplayTargetRelationFilter.Enemy,
                            requiredTags: new[] { 101, 102 },
                            blockedStatuses: new[] { 201 },
                            maxTargets: 1)),
                    AbilityGraphNodeConfig.CreateEmitEvent(
                        "event",
                        new AbilityGraphEmitEventPayloadConfig(AbilityEventType.CastFinished)),
                    AbilityGraphNodeConfig.Entry("entry"),
                    AbilityGraphNodeConfig.CreateApplyEffect("effect", new AbilityGraphApplyEffectPayloadConfig(9001))
                },
                new[]
                {
                    new AbilityGraphEdgeConfig("target", AbilityGraphPorts.Next, "effect"),
                    new AbilityGraphEdgeConfig("entry", AbilityGraphPorts.Next, "target"),
                    new AbilityGraphEdgeConfig("effect", AbilityGraphPorts.Next, "event")
                },
                sourcePath: "synthetic://strike");
        }

        private static AbilityGraphNode FindNode(IReadOnlyList<AbilityGraphNode> nodes, string nodeId)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].NodeId == nodeId)
                    return nodes[i];
            }

            Assert.Fail("Node not found: " + nodeId + ".");
            return default;
        }

        private static string[] NodeIds(IReadOnlyList<AbilityGraphNode> nodes)
        {
            var ids = new string[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                ids[i] = nodes[i].NodeId;
            return ids;
        }

        private static string[] EdgeFromNodeIds(IReadOnlyList<AbilityGraphEdge> edges)
        {
            var ids = new string[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                ids[i] = edges[i].FromNodeId;
            return ids;
        }

        private static string[] ConfigNodeIds(IReadOnlyList<AbilityGraphNodeConfig> nodes)
        {
            var ids = new string[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                ids[i] = nodes[i].NodeId;
            return ids;
        }

        private static string[] ConfigEdgeFromNodeIds(IReadOnlyList<AbilityGraphEdgeConfig> edges)
        {
            var ids = new string[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                ids[i] = edges[i].FromNodeId;
            return ids;
        }
    }
}
