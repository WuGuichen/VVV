using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Gameplay
{
    /// <summary>Read-only diagnostic view of an ability graph definition and its validation state.</summary>
    public sealed class AbilityGraphDiagnosticSnapshot
    {
        private readonly AbilityGraphValidationDiagnostic[] _validationErrors;
        private readonly ReadOnlyCollection<AbilityGraphValidationDiagnostic> _validationErrorsView;

        public AbilityGraphDiagnosticSnapshot(
            string graphId,
            int graphVersion,
            string entryNodeId,
            int nodeCount,
            int edgeCount,
            AbilityGraphValidationDiagnosticSummary validation,
            IReadOnlyList<AbilityGraphValidationDiagnostic> validationErrors,
            AbilityGraphExecutionTrace executionTrace = null,
            string timelineSummary = null)
        {
            if (nodeCount < 0)
                throw new ArgumentOutOfRangeException(nameof(nodeCount), "Ability graph node count cannot be negative.");
            if (edgeCount < 0)
                throw new ArgumentOutOfRangeException(nameof(edgeCount), "Ability graph edge count cannot be negative.");

            GraphId = graphId ?? string.Empty;
            GraphVersion = graphVersion;
            EntryNodeId = entryNodeId ?? string.Empty;
            NodeCount = nodeCount;
            EdgeCount = edgeCount;
            Validation = validation;
            _validationErrors = Copy(validationErrors);
            _validationErrorsView = Array.AsReadOnly(_validationErrors);
            ExecutionTrace = executionTrace;
            TimelineSummary = timelineSummary ?? string.Empty;
        }

        public string GraphId { get; }
        public int GraphVersion { get; }
        public string EntryNodeId { get; }
        public int NodeCount { get; }
        public int EdgeCount { get; }
        public AbilityGraphValidationDiagnosticSummary Validation { get; }
        public IReadOnlyList<AbilityGraphValidationDiagnostic> ValidationErrors => _validationErrorsView;
        public AbilityGraphExecutionTrace ExecutionTrace { get; }
        public bool HasExecutionTrace => ExecutionTrace != null;
        public string TimelineSummary { get; }

        private static AbilityGraphValidationDiagnostic[] Copy(IReadOnlyList<AbilityGraphValidationDiagnostic> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<AbilityGraphValidationDiagnostic>();

            var copy = new AbilityGraphValidationDiagnostic[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];

            return copy;
        }
    }

    public readonly struct AbilityGraphValidationDiagnosticSummary
    {
        public AbilityGraphValidationDiagnosticSummary(bool isValid, int errorCount)
        {
            if (errorCount < 0)
                throw new ArgumentOutOfRangeException(nameof(errorCount), "Ability graph validation error count cannot be negative.");

            IsValid = isValid;
            ErrorCount = errorCount;
        }

        public bool IsValid { get; }
        public int ErrorCount { get; }
    }

    public readonly struct AbilityGraphValidationDiagnostic
    {
        public AbilityGraphValidationDiagnostic(
            AbilityGraphValidationErrorCode code,
            string message,
            string nodeId = null,
            int edgeIndex = -1,
            string fieldPath = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            EdgeIndex = edgeIndex;
            FieldPath = fieldPath ?? string.Empty;
        }

        public AbilityGraphValidationErrorCode Code { get; }
        public string CodeToken => Code.ToString();
        public string Message { get; }
        public string NodeId { get; }
        public int EdgeIndex { get; }
        public string FieldPath { get; }
    }

    /// <summary>Builds immutable ability graph diagnostics from public graph contract APIs.</summary>
    public sealed class AbilityGraphDiagnosticSnapshotBuilder
    {
        public AbilityGraphDiagnosticSnapshot Build(
            AbilityGraphDefinition graph,
            AbilityGraphExecutionTrace executionTrace = null,
            string timelineSummary = null)
        {
            return Build(graph, AbilityGraphValidator.Validate(graph), executionTrace, timelineSummary);
        }

        public AbilityGraphDiagnosticSnapshot Build(
            AbilityGraphDefinition graph,
            AbilityGraphValidationResult validation,
            AbilityGraphExecutionTrace executionTrace = null,
            string timelineSummary = null)
        {
            AbilityGraphValidationResult validationResult = validation ?? AbilityGraphValidator.Validate(graph);
            AbilityGraphValidationDiagnostic[] validationErrors = BuildValidationDiagnostics(validationResult);
            var summary = new AbilityGraphValidationDiagnosticSummary(validationResult.IsValid, validationResult.ErrorCount);

            return new AbilityGraphDiagnosticSnapshot(
                graph == null ? string.Empty : graph.GraphId,
                graph == null ? 0 : graph.Version,
                graph == null ? string.Empty : graph.EntryNodeId,
                graph == null ? 0 : graph.Nodes.Count,
                graph == null ? 0 : graph.Edges.Count,
                summary,
                validationErrors,
                executionTrace,
                timelineSummary);
        }

        private static AbilityGraphValidationDiagnostic[] BuildValidationDiagnostics(AbilityGraphValidationResult validation)
        {
            if (validation == null || validation.ErrorCount == 0)
                return Array.Empty<AbilityGraphValidationDiagnostic>();

            var diagnostics = new AbilityGraphValidationDiagnostic[validation.ErrorCount];
            for (int i = 0; i < validation.ErrorCount; i++)
            {
                AbilityGraphValidationError error = validation.Errors[i];
                diagnostics[i] = new AbilityGraphValidationDiagnostic(
                    error.Code,
                    error.Message,
                    error.NodeId,
                    error.EdgeIndex,
                    error.FieldPath);
            }

            return diagnostics;
        }
    }
}
