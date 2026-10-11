using System;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.Config.Runtime
{
    /// <summary>Synthetic config DTO for runtime ability graph mapping.</summary>
    public sealed class AbilityGraphConfig
    {
        public const int CurrentVersion = AbilityGraphDefinition.CurrentVersion;

        public AbilityGraphConfig()
        {
            SourcePath = string.Empty;
            Id = string.Empty;
            Version = CurrentVersion;
            EntryNodeId = string.Empty;
            Nodes = Array.Empty<AbilityGraphNodeConfig>();
            Edges = Array.Empty<AbilityGraphEdgeConfig>();
        }

        public AbilityGraphConfig(
            string id,
            string entryNodeId,
            IReadOnlyList<AbilityGraphNodeConfig> nodes,
            IReadOnlyList<AbilityGraphEdgeConfig> edges,
            string sourcePath = null,
            int version = CurrentVersion)
        {
            SourcePath = sourcePath ?? string.Empty;
            Id = id ?? string.Empty;
            Version = version;
            EntryNodeId = entryNodeId ?? string.Empty;
            Nodes = CopyNodes(nodes);
            Edges = CopyEdges(edges);
        }

        public string SourcePath { get; set; }
        public string Id { get; set; }
        public int Version { get; set; }
        public string EntryNodeId { get; set; }
        public AbilityGraphNodeConfig[] Nodes { get; set; }
        public AbilityGraphEdgeConfig[] Edges { get; set; }

        private static AbilityGraphNodeConfig[] CopyNodes(IReadOnlyList<AbilityGraphNodeConfig> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return Array.Empty<AbilityGraphNodeConfig>();

            var copy = new AbilityGraphNodeConfig[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                copy[i] = nodes[i];
            return copy;
        }

        private static AbilityGraphEdgeConfig[] CopyEdges(IReadOnlyList<AbilityGraphEdgeConfig> edges)
        {
            if (edges == null || edges.Count == 0)
                return Array.Empty<AbilityGraphEdgeConfig>();

            var copy = new AbilityGraphEdgeConfig[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                copy[i] = edges[i];
            return copy;
        }
    }

    public sealed class AbilityGraphNodeConfig
    {
        public AbilityGraphNodeConfig()
        {
            NodeId = string.Empty;
        }

        public AbilityGraphNodeConfig(string nodeId, AbilityGraphNodeKind kind)
        {
            NodeId = nodeId ?? string.Empty;
            Kind = kind;
        }

        public string NodeId { get; set; }
        public AbilityGraphNodeKind Kind { get; set; }
        public AbilityGraphTargetQueryPayloadConfig TargetQuery { get; set; }
        public AbilityGraphApplyEffectPayloadConfig ApplyEffect { get; set; }
        public AbilityGraphEmitEventPayloadConfig EmitEvent { get; set; }
        public AbilityGraphPhaseGatePayloadConfig PhaseGate { get; set; }

        public static AbilityGraphNodeConfig Entry(string nodeId)
        {
            return new AbilityGraphNodeConfig(nodeId, AbilityGraphNodeKind.Entry);
        }

        public static AbilityGraphNodeConfig Sequence(string nodeId)
        {
            return new AbilityGraphNodeConfig(nodeId, AbilityGraphNodeKind.Sequence);
        }

        public static AbilityGraphNodeConfig CreateTargetQuery(
            string nodeId,
            AbilityGraphTargetQueryPayloadConfig payload)
        {
            return new AbilityGraphNodeConfig(nodeId, AbilityGraphNodeKind.TargetQuery)
            {
                TargetQuery = payload
            };
        }

        public static AbilityGraphNodeConfig CreateApplyEffect(
            string nodeId,
            AbilityGraphApplyEffectPayloadConfig payload)
        {
            return new AbilityGraphNodeConfig(nodeId, AbilityGraphNodeKind.ApplyEffect)
            {
                ApplyEffect = payload
            };
        }

        public static AbilityGraphNodeConfig CreateEmitEvent(
            string nodeId,
            AbilityGraphEmitEventPayloadConfig payload)
        {
            return new AbilityGraphNodeConfig(nodeId, AbilityGraphNodeKind.EmitEvent)
            {
                EmitEvent = payload
            };
        }

        public static AbilityGraphNodeConfig CreatePhaseGate(
            string nodeId,
            AbilityGraphPhaseGatePayloadConfig payload)
        {
            return new AbilityGraphNodeConfig(nodeId, AbilityGraphNodeKind.PhaseGate)
            {
                PhaseGate = payload
            };
        }
    }

    public sealed class AbilityGraphEdgeConfig
    {
        public AbilityGraphEdgeConfig()
        {
            FromNodeId = string.Empty;
            OutputPort = string.Empty;
            ToNodeId = string.Empty;
        }

        public AbilityGraphEdgeConfig(string fromNodeId, string outputPort, string toNodeId)
        {
            FromNodeId = fromNodeId ?? string.Empty;
            OutputPort = outputPort ?? string.Empty;
            ToNodeId = toNodeId ?? string.Empty;
        }

        public string FromNodeId { get; set; }
        public string OutputPort { get; set; }
        public string ToNodeId { get; set; }
    }

    public sealed class AbilityGraphTargetQueryPayloadConfig
    {
        public AbilityGraphTargetQueryPayloadConfig()
        {
            RequireAlive = true;
            RequiredTags = Array.Empty<int>();
            BlockedStatuses = Array.Empty<int>();
        }

        public AbilityGraphTargetQueryPayloadConfig(
            GameplayTargetRelationFilter relationFilter = GameplayTargetRelationFilter.Any,
            bool requireAlive = true,
            IReadOnlyList<int> requiredTags = null,
            IReadOnlyList<int> blockedStatuses = null,
            int maxTargets = 0)
        {
            RelationFilter = relationFilter;
            RequireAlive = requireAlive;
            RequiredTags = CopyIds(requiredTags);
            BlockedStatuses = CopyIds(blockedStatuses);
            MaxTargets = maxTargets;
        }

        public GameplayTargetRelationFilter RelationFilter { get; set; }
        public bool RequireAlive { get; set; }
        public int MaxTargets { get; set; }
        public int[] RequiredTags { get; set; }
        public int[] BlockedStatuses { get; set; }

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

    public sealed class AbilityGraphApplyEffectPayloadConfig
    {
        public AbilityGraphApplyEffectPayloadConfig()
        {
        }

        public AbilityGraphApplyEffectPayloadConfig(int effectId)
        {
            EffectId = effectId;
        }

        public int EffectId { get; set; }
    }

    public sealed class AbilityGraphEmitEventPayloadConfig
    {
        public AbilityGraphEmitEventPayloadConfig()
        {
        }

        public AbilityGraphEmitEventPayloadConfig(AbilityEventType eventType)
        {
            EventType = eventType;
        }

        public AbilityEventType EventType { get; set; }
    }

    public sealed class AbilityGraphPhaseGatePayloadConfig
    {
        public AbilityGraphPhaseGatePayloadConfig()
        {
            PhaseId = string.Empty;
        }

        public AbilityGraphPhaseGatePayloadConfig(string phaseId)
        {
            PhaseId = phaseId ?? string.Empty;
        }

        public string PhaseId { get; set; }
    }
}
