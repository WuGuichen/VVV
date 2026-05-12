using System;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.Config.Runtime
{
    public enum AbilityGraphConfigMappingDiagnosticCode
    {
        ConfigIsNull = 1,
        RuntimeValidationError = 2
    }

    public readonly struct AbilityGraphConfigMappingDiagnostic
    {
        public AbilityGraphConfigMappingDiagnostic(
            AbilityGraphConfigMappingDiagnosticCode code,
            string sourcePath,
            string fieldPath,
            string message,
            bool hasRuntimeValidationError = false,
            AbilityGraphValidationErrorCode runtimeValidationCode = AbilityGraphValidationErrorCode.NullGraphDefinition,
            string runtimeFieldPath = null,
            string nodeId = null,
            int edgeIndex = -1)
        {
            Code = code;
            SourcePath = sourcePath ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
            HasRuntimeValidationError = hasRuntimeValidationError;
            RuntimeValidationCode = runtimeValidationCode;
            RuntimeFieldPath = runtimeFieldPath ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            EdgeIndex = edgeIndex;
        }

        public AbilityGraphConfigMappingDiagnosticCode Code { get; }
        public string SourcePath { get; }
        public string FieldPath { get; }
        public string Message { get; }
        public bool HasRuntimeValidationError { get; }
        public AbilityGraphValidationErrorCode RuntimeValidationCode { get; }
        public string RuntimeFieldPath { get; }
        public string NodeId { get; }
        public int EdgeIndex { get; }
    }

    public sealed class AbilityGraphConfigMappingResult
    {
        private readonly AbilityGraphConfigMappingDiagnostic[] _diagnostics;

        public AbilityGraphConfigMappingResult(
            AbilityGraphDefinition definition,
            IReadOnlyList<AbilityGraphConfigMappingDiagnostic> diagnostics)
        {
            Definition = definition;

            if (diagnostics == null || diagnostics.Count == 0)
            {
                _diagnostics = Array.Empty<AbilityGraphConfigMappingDiagnostic>();
                return;
            }

            _diagnostics = new AbilityGraphConfigMappingDiagnostic[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                _diagnostics[i] = diagnostics[i];
        }

        public AbilityGraphDefinition Definition { get; }
        public IReadOnlyList<AbilityGraphConfigMappingDiagnostic> Diagnostics => _diagnostics;
        public bool IsValid => _diagnostics.Length == 0;
        public bool Succeeded => IsValid;
        public int DiagnosticCount => _diagnostics.Length;

        public bool ContainsRuntimeError(AbilityGraphValidationErrorCode code)
        {
            for (int i = 0; i < _diagnostics.Length; i++)
            {
                AbilityGraphConfigMappingDiagnostic diagnostic = _diagnostics[i];
                if (diagnostic.HasRuntimeValidationError && diagnostic.RuntimeValidationCode == code)
                    return true;
            }

            return false;
        }
    }

    public static class AbilityGraphConfigMapper
    {
        public static AbilityGraphConfigMappingResult Map(AbilityGraphConfig config, string sourcePath = null)
        {
            if (config == null)
            {
                return new AbilityGraphConfigMappingResult(
                    null,
                    new[]
                    {
                        new AbilityGraphConfigMappingDiagnostic(
                            AbilityGraphConfigMappingDiagnosticCode.ConfigIsNull,
                            sourcePath,
                            string.Empty,
                            "Ability graph config is null.")
                    });
            }

            string diagnosticSourcePath = ResolveSourcePath(config, sourcePath);
            AbilityGraphNodeConfig[] nodeConfigs = config.Nodes ?? Array.Empty<AbilityGraphNodeConfig>();
            AbilityGraphEdgeConfig[] edgeConfigs = config.Edges ?? Array.Empty<AbilityGraphEdgeConfig>();
            var pathResolver = new AbilityGraphConfigPathResolver(nodeConfigs, edgeConfigs);

            var nodes = new AbilityGraphNode[nodeConfigs.Length];
            for (int i = 0; i < nodeConfigs.Length; i++)
                nodes[i] = MapNode(nodeConfigs[i]);

            var edges = new AbilityGraphEdge[edgeConfigs.Length];
            for (int i = 0; i < edgeConfigs.Length; i++)
                edges[i] = MapEdge(edgeConfigs[i]);

            var definition = new AbilityGraphDefinition(
                config.Id,
                config.Version,
                config.EntryNodeId,
                nodes,
                edges);

            AbilityGraphValidationResult validation = definition.Validate();
            if (validation.IsValid)
                return new AbilityGraphConfigMappingResult(definition, Array.Empty<AbilityGraphConfigMappingDiagnostic>());

            var diagnostics = new List<AbilityGraphConfigMappingDiagnostic>(validation.ErrorCount);
            for (int i = 0; i < validation.Errors.Count; i++)
            {
                AbilityGraphValidationError error = validation.Errors[i];
                diagnostics.Add(new AbilityGraphConfigMappingDiagnostic(
                    AbilityGraphConfigMappingDiagnosticCode.RuntimeValidationError,
                    diagnosticSourcePath,
                    pathResolver.Resolve(error),
                    error.Message,
                    hasRuntimeValidationError: true,
                    runtimeValidationCode: error.Code,
                    runtimeFieldPath: error.FieldPath,
                    nodeId: error.NodeId,
                    edgeIndex: error.EdgeIndex));
            }

            return new AbilityGraphConfigMappingResult(definition, diagnostics);
        }

        public static bool TryMap(
            AbilityGraphConfig config,
            out AbilityGraphDefinition definition,
            out AbilityGraphConfigMappingResult result,
            string sourcePath = null)
        {
            result = Map(config, sourcePath);
            definition = result.Definition;
            return result.IsValid;
        }

        private static AbilityGraphNode MapNode(AbilityGraphNodeConfig config)
        {
            if (config == null)
                return new AbilityGraphNode(string.Empty, AbilityGraphNodeKind.Entry);

            return new AbilityGraphNode(config.NodeId, config.Kind, MapPayload(config));
        }

        private static object MapPayload(AbilityGraphNodeConfig config)
        {
            switch (config.Kind)
            {
                case AbilityGraphNodeKind.Entry:
                case AbilityGraphNodeKind.Sequence:
                    return null;
                case AbilityGraphNodeKind.TargetQuery:
                    return MapTargetQueryPayload(config.TargetQuery);
                case AbilityGraphNodeKind.ApplyEffect:
                    return config.ApplyEffect == null ? null : new AbilityGraphApplyEffectPayload(config.ApplyEffect.EffectId);
                case AbilityGraphNodeKind.EmitEvent:
                    return config.EmitEvent == null ? null : new AbilityGraphEmitEventPayload(config.EmitEvent.EventType);
                case AbilityGraphNodeKind.PhaseGate:
                    return config.PhaseGate == null ? null : new AbilityGraphPhaseGatePayload(config.PhaseGate.PhaseId);
                default:
                    return null;
            }
        }

        private static AbilityGraphTargetQueryPayload MapTargetQueryPayload(AbilityGraphTargetQueryPayloadConfig config)
        {
            if (config == null)
                return null;

            return new AbilityGraphTargetQueryPayload(
                config.RelationFilter,
                config.RequireAlive,
                config.RequiredTags,
                config.BlockedStatuses,
                config.MaxTargets);
        }

        private static AbilityGraphEdge MapEdge(AbilityGraphEdgeConfig config)
        {
            if (config == null)
                return new AbilityGraphEdge(string.Empty, string.Empty, string.Empty);

            return new AbilityGraphEdge(config.FromNodeId, config.OutputPort, config.ToNodeId);
        }

        private static string ResolveSourcePath(AbilityGraphConfig config, string sourcePath)
        {
            if (!string.IsNullOrEmpty(sourcePath))
                return sourcePath;

            return config.SourcePath ?? string.Empty;
        }

        private sealed class AbilityGraphConfigPathResolver
        {
            private readonly Dictionary<string, int> _firstNodeIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _duplicateNodeIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly int[] _sortedNodeConfigIndexes;
            private readonly int[] _sortedEdgeConfigIndexes;
            private readonly AbilityGraphNodeKind[] _nodeKindsByConfigIndex;

            public AbilityGraphConfigPathResolver(
                IReadOnlyList<AbilityGraphNodeConfig> nodes,
                IReadOnlyList<AbilityGraphEdgeConfig> edges)
            {
                _nodeKindsByConfigIndex = new AbilityGraphNodeKind[nodes.Count];
                var nodeEntries = new NodeSourceEntry[nodes.Count];
                for (int i = 0; i < nodes.Count; i++)
                {
                    AbilityGraphNodeConfig node = nodes[i];
                    string nodeId = node == null ? string.Empty : node.NodeId ?? string.Empty;
                    _nodeKindsByConfigIndex[i] = node == null ? AbilityGraphNodeKind.Entry : node.Kind;
                    if (!_firstNodeIndexById.ContainsKey(nodeId))
                        _firstNodeIndexById.Add(nodeId, i);
                    else if (!_duplicateNodeIndexById.ContainsKey(nodeId))
                        _duplicateNodeIndexById.Add(nodeId, i);

                    nodeEntries[i] = new NodeSourceEntry(nodeId, i);
                }

                Array.Sort(nodeEntries, CompareNodeEntries);
                _sortedNodeConfigIndexes = new int[nodeEntries.Length];
                for (int i = 0; i < nodeEntries.Length; i++)
                    _sortedNodeConfigIndexes[i] = nodeEntries[i].ConfigIndex;

                var edgeEntries = new EdgeSourceEntry[edges.Count];
                for (int i = 0; i < edges.Count; i++)
                {
                    AbilityGraphEdgeConfig edge = edges[i];
                    edgeEntries[i] = edge == null
                        ? new EdgeSourceEntry(string.Empty, string.Empty, string.Empty, i)
                        : new EdgeSourceEntry(edge.FromNodeId, edge.OutputPort, edge.ToNodeId, i);
                }

                Array.Sort(edgeEntries, CompareEdgeEntries);
                _sortedEdgeConfigIndexes = new int[edgeEntries.Length];
                for (int i = 0; i < edgeEntries.Length; i++)
                    _sortedEdgeConfigIndexes[i] = edgeEntries[i].ConfigIndex;
            }

            public string Resolve(AbilityGraphValidationError error)
            {
                if (error.Code == AbilityGraphValidationErrorCode.DuplicateNodeId
                    && _duplicateNodeIndexById.TryGetValue(error.NodeId, out int duplicateNodeIndex))
                {
                    return NodePath(duplicateNodeIndex, ".NodeId");
                }

                if (!string.IsNullOrEmpty(error.FieldPath))
                {
                    if (error.FieldPath.StartsWith("Edges[", StringComparison.Ordinal))
                        return ResolveEdgeFieldPath(error.FieldPath);

                    if (error.FieldPath.StartsWith("Nodes[", StringComparison.Ordinal))
                        return ResolveNodeFieldPath(error.FieldPath, error.NodeId);

                    return error.FieldPath;
                }

                if (error.EdgeIndex >= 0)
                    return ResolveEdgeIndex(error.EdgeIndex);

                if (!string.IsNullOrEmpty(error.NodeId)
                    && _firstNodeIndexById.TryGetValue(error.NodeId, out int nodeIndex))
                {
                    return NodePath(nodeIndex, ".NodeId");
                }

                return string.Empty;
            }

            private string ResolveEdgeFieldPath(string runtimeFieldPath)
            {
                if (!TryReadBracket(runtimeFieldPath, "Edges[", out string edgeIndexText, out int closeIndex)
                    || !int.TryParse(edgeIndexText, out int edgeIndex)
                    || edgeIndex < 0
                    || edgeIndex >= _sortedEdgeConfigIndexes.Length)
                {
                    return runtimeFieldPath;
                }

                int configIndex = _sortedEdgeConfigIndexes[edgeIndex];
                return "Edges[" + configIndex + "]" + runtimeFieldPath.Substring(closeIndex + 1);
            }

            private string ResolveEdgeIndex(int runtimeEdgeIndex)
            {
                if (runtimeEdgeIndex < 0 || runtimeEdgeIndex >= _sortedEdgeConfigIndexes.Length)
                    return "Edges[" + runtimeEdgeIndex + "]";

                return "Edges[" + _sortedEdgeConfigIndexes[runtimeEdgeIndex] + "]";
            }

            private string ResolveNodeFieldPath(string runtimeFieldPath, string fallbackNodeId)
            {
                if (!TryReadBracket(runtimeFieldPath, "Nodes[", out string nodeKey, out int closeIndex))
                    return runtimeFieldPath;

                if (!TryResolveConfigNodeIndex(nodeKey, fallbackNodeId, out int configIndex))
                    return runtimeFieldPath;

                string suffix = runtimeFieldPath.Substring(closeIndex + 1);
                return NodePath(configIndex, MapNodeSuffix(_nodeKindsByConfigIndex[configIndex], suffix));
            }

            private bool TryResolveConfigNodeIndex(string nodeKey, string fallbackNodeId, out int configIndex)
            {
                if (int.TryParse(nodeKey, out int sortedNodeIndex)
                    && sortedNodeIndex >= 0
                    && sortedNodeIndex < _sortedNodeConfigIndexes.Length)
                {
                    configIndex = _sortedNodeConfigIndexes[sortedNodeIndex];
                    return true;
                }

                if (_firstNodeIndexById.TryGetValue(nodeKey ?? string.Empty, out configIndex))
                    return true;

                if (!string.IsNullOrEmpty(fallbackNodeId)
                    && _firstNodeIndexById.TryGetValue(fallbackNodeId, out configIndex))
                {
                    return true;
                }

                configIndex = -1;
                return false;
            }

            private static string MapNodeSuffix(AbilityGraphNodeKind kind, string runtimeSuffix)
            {
                const string payloadPrefix = ".Payload";
                if (!runtimeSuffix.StartsWith(payloadPrefix, StringComparison.Ordinal))
                    return runtimeSuffix;

                return "." + PayloadConfigPropertyName(kind) + runtimeSuffix.Substring(payloadPrefix.Length);
            }

            private static string PayloadConfigPropertyName(AbilityGraphNodeKind kind)
            {
                switch (kind)
                {
                    case AbilityGraphNodeKind.TargetQuery:
                        return "TargetQuery";
                    case AbilityGraphNodeKind.ApplyEffect:
                        return "ApplyEffect";
                    case AbilityGraphNodeKind.EmitEvent:
                        return "EmitEvent";
                    case AbilityGraphNodeKind.PhaseGate:
                        return "PhaseGate";
                    default:
                        return "Payload";
                }
            }

            private static string NodePath(int configIndex, string suffix)
            {
                return "Nodes[" + configIndex + "]" + suffix;
            }

            private static bool TryReadBracket(
                string value,
                string prefix,
                out string content,
                out int closeIndex)
            {
                content = string.Empty;
                closeIndex = -1;

                if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal))
                    return false;

                closeIndex = value.IndexOf(']', prefix.Length);
                if (closeIndex < 0)
                    return false;

                content = value.Substring(prefix.Length, closeIndex - prefix.Length);
                return true;
            }

            private static int CompareNodeEntries(NodeSourceEntry left, NodeSourceEntry right)
            {
                int nodeId = string.Compare(left.NodeId, right.NodeId, StringComparison.Ordinal);
                return nodeId != 0 ? nodeId : left.ConfigIndex.CompareTo(right.ConfigIndex);
            }

            private static int CompareEdgeEntries(EdgeSourceEntry left, EdgeSourceEntry right)
            {
                int from = string.Compare(left.FromNodeId, right.FromNodeId, StringComparison.Ordinal);
                if (from != 0)
                    return from;

                int port = string.Compare(left.OutputPort, right.OutputPort, StringComparison.Ordinal);
                if (port != 0)
                    return port;

                int to = string.Compare(left.ToNodeId, right.ToNodeId, StringComparison.Ordinal);
                return to != 0 ? to : left.ConfigIndex.CompareTo(right.ConfigIndex);
            }

            private readonly struct NodeSourceEntry
            {
                public NodeSourceEntry(string nodeId, int configIndex)
                {
                    NodeId = nodeId ?? string.Empty;
                    ConfigIndex = configIndex;
                }

                public string NodeId { get; }
                public int ConfigIndex { get; }
            }

            private readonly struct EdgeSourceEntry
            {
                public EdgeSourceEntry(string fromNodeId, string outputPort, string toNodeId, int configIndex)
                {
                    FromNodeId = fromNodeId ?? string.Empty;
                    OutputPort = outputPort ?? string.Empty;
                    ToNodeId = toNodeId ?? string.Empty;
                    ConfigIndex = configIndex;
                }

                public string FromNodeId { get; }
                public string OutputPort { get; }
                public string ToNodeId { get; }
                public int ConfigIndex { get; }
            }
        }
    }
}
