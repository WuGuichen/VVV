using System;
using System.Collections.Generic;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestManualExecutionResult
    {
        private readonly TriggerTestRunResult[] _results;

        internal TriggerTestManualExecutionResult(
            TriggerTestDiscoveryResult discovery,
            TriggerTestRunResult[] results,
            TimeSpan elapsed,
            string errorMessage)
        {
            Discovery = discovery ??
                throw new ArgumentNullException(nameof(discovery));
            _results = results ?? Array.Empty<TriggerTestRunResult>();
            Elapsed = elapsed;
            ErrorMessage = errorMessage ?? string.Empty;

            int passed = 0;
            int pending = 0;
            int failed = 0;
            int operations = 0;
            int failures = 0;
            for (int i = 0; i < _results.Length; i++)
            {
                TriggerTestRunResult result = _results[i];
                switch (result.Status)
                {
                    case TriggerTestRunStatus.Passed:
                        passed++;
                        break;
                    case TriggerTestRunStatus.PendingExternalEvidence:
                        pending++;
                        break;
                    default:
                        failed++;
                        break;
                }

                operations += result.SimulationResult.Events.Count;
                failures += result.SimulationResult.Failures.Count;
            }

            PassedCount = passed;
            PendingCount = pending;
            FailedCount = failed;
            OperationCount = operations;
            FailureCount = failures;
        }

        internal TriggerTestDiscoveryResult Discovery { get; }
        internal IReadOnlyList<TriggerTestRunResult> Results => _results;
        internal TimeSpan Elapsed { get; }
        internal string ErrorMessage { get; }
        internal int PassedCount { get; }
        internal int PendingCount { get; }
        internal int FailedCount { get; }
        internal int OperationCount { get; }
        internal int FailureCount { get; }
        internal bool HasResults => _results.Length != 0;
        internal bool Succeeded =>
            Discovery.Succeeded &&
            string.IsNullOrEmpty(ErrorMessage) &&
            FailedCount == 0 &&
            PendingCount == 0 &&
            PassedCount == _results.Length &&
            _results.Length != 0;

        internal TriggerTestRunResult FindResult(string scenarioId)
        {
            for (int i = 0; i < _results.Length; i++)
            {
                TriggerTestRunResult result = _results[i];
                if (string.Equals(
                        result.Descriptor.ScenarioId.Value,
                        scenarioId,
                        StringComparison.Ordinal))
                {
                    return result;
                }
            }

            return null;
        }

        internal static TriggerTestManualExecutionResult DiscoveryFailed(
            TriggerTestDiscoveryResult discovery)
        {
            string message = discovery.Errors.Count == 0
                ? "Trigger 场景发现失败。"
                : discovery.Errors[0].TypeName +
                  ": " +
                  discovery.Errors[0].Message;
            return new TriggerTestManualExecutionResult(
                discovery,
                Array.Empty<TriggerTestRunResult>(),
                TimeSpan.Zero,
                message);
        }

        internal static TriggerTestManualExecutionResult InvalidSelection(
            TriggerTestDiscoveryResult discovery,
            string scenarioId) =>
            new TriggerTestManualExecutionResult(
                discovery,
                Array.Empty<TriggerTestRunResult>(),
                TimeSpan.Zero,
                "未找到 Trigger 场景：" + (scenarioId ?? string.Empty));

        internal static TriggerTestManualExecutionResult ProfileUnavailable(
            TriggerTestDiscoveryResult discovery,
            string message) =>
            new TriggerTestManualExecutionResult(
                discovery,
                Array.Empty<TriggerTestRunResult>(),
                TimeSpan.Zero,
                message ?? string.Empty);
    }
}
