using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    /// <summary>
    /// Discovers and executes test-only Trigger scenarios for manual Editor use.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. Discovery, report generation and
    /// result projection are Editor/test operations and never run in a gameplay
    /// Tick or Trigger action transaction.
    /// </remarks>
    internal sealed class TriggerTestManualExecutionService
    {
        private readonly Assembly _assembly;
        private readonly Func<bool> _isPlayModeProvider;

        internal TriggerTestManualExecutionService(
            Assembly assembly,
            Func<bool> isPlayModeProvider = null)
        {
            _assembly = assembly ??
                throw new ArgumentNullException(nameof(assembly));
            _isPlayModeProvider =
                isPlayModeProvider ?? (() => Application.isPlaying);
        }

        internal TriggerTestDiscoveryResult Discover()
        {
            TriggerTestDiscoveryResult discovery;
            try
            {
                discovery = TriggerTestScenarioDiscovery.Discover(_assembly);
            }
            catch (Exception exception)
            {
                discovery = new TriggerTestDiscoveryResult(
                    Array.Empty<ITriggerTestScenario>(),
                    new[]
                    {
                        new TriggerTestDiscoveryError(
                            _assembly.FullName,
                            exception.GetType().Name + ": " + exception.Message)
                    });
            }

            if (discovery.Succeeded)
                TriggerTestReportWriter.WriteIndex(discovery.Scenarios);
            return discovery;
        }

        internal TriggerTestManualExecutionResult Run(string scenarioId)
        {
            TriggerTestDiscoveryResult discovery = Discover();
            if (!discovery.Succeeded)
            {
                return TriggerTestManualExecutionResult.DiscoveryFailed(
                    discovery);
            }

            ITriggerTestScenario selected = null;
            for (int i = 0; i < discovery.Scenarios.Count; i++)
            {
                ITriggerTestScenario candidate = discovery.Scenarios[i];
                if (string.Equals(
                        candidate.Descriptor.ScenarioId.Value,
                        scenarioId,
                        StringComparison.Ordinal))
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected == null)
            {
                return TriggerTestManualExecutionResult.InvalidSelection(
                    discovery,
                    scenarioId);
            }
            if (!CanRun(selected.Descriptor, out string unavailableReason))
            {
                return TriggerTestManualExecutionResult.ProfileUnavailable(
                    discovery,
                    unavailableReason);
            }

            return RunScenarios(
                discovery,
                new[] { selected });
        }

        internal TriggerTestManualExecutionResult Run(
            ITriggerTestScenario scenario)
        {
            TriggerTestDiscoveryResult discovery = Discover();
            if (!discovery.Succeeded)
            {
                return TriggerTestManualExecutionResult.DiscoveryFailed(
                    discovery);
            }
            if (scenario == null)
            {
                return TriggerTestManualExecutionResult.InvalidSelection(
                    discovery,
                    string.Empty);
            }
            if (!CanRun(scenario.Descriptor, out string unavailableReason))
            {
                return TriggerTestManualExecutionResult.ProfileUnavailable(
                    discovery,
                    unavailableReason);
            }

            return RunScenarios(discovery, new[] { scenario });
        }

        internal TriggerTestManualExecutionResult RunAll()
        {
            TriggerTestDiscoveryResult discovery = Discover();
            if (!discovery.Succeeded)
                return TriggerTestManualExecutionResult.DiscoveryFailed(discovery);

            var compatible = new List<ITriggerTestScenario>(
                discovery.Scenarios.Count);
            for (int i = 0; i < discovery.Scenarios.Count; i++)
            {
                ITriggerTestScenario scenario = discovery.Scenarios[i];
                if (CanRun(scenario.Descriptor, out _))
                    compatible.Add(scenario);
            }

            return compatible.Count > 0
                ? RunScenarios(discovery, compatible)
                : TriggerTestManualExecutionResult.ProfileUnavailable(
                    discovery,
                    "当前 Editor 状态下没有可运行的 Trigger 场景。");
        }

        internal bool CanRun(
            TriggerTestDescriptor descriptor,
            out string unavailableReason)
        {
            if (descriptor == null)
            {
                unavailableReason = "Trigger 场景描述不可用。";
                return false;
            }

            bool isPlaying = _isPlayModeProvider();
            switch (descriptor.ExecutionProfile)
            {
                case TriggerTestExecutionProfile.PlayMode:
                    unavailableReason = isPlaying
                        ? string.Empty
                        : "该场景会驱动真实 Game View，请先进入 Play Mode。";
                    return isPlaying;
                case TriggerTestExecutionProfile.EditMode:
                    unavailableReason = isPlaying
                        ? "该场景只能在 Edit Mode 运行。"
                        : string.Empty;
                    return !isPlaying;
                default:
                    unavailableReason = string.Empty;
                    return true;
            }
        }

        private static TriggerTestManualExecutionResult RunScenarios(
            TriggerTestDiscoveryResult discovery,
            IReadOnlyList<ITriggerTestScenario> scenarios)
        {
            var stopwatch = Stopwatch.StartNew();
            var runner = new TriggerTestRunner(writeLatestReport: true);
            var results = new TriggerTestRunResult[scenarios.Count];
            for (int i = 0; i < scenarios.Count; i++)
                results[i] = runner.Run(scenarios[i]);
            stopwatch.Stop();
            return new TriggerTestManualExecutionResult(
                discovery,
                results,
                stopwatch.Elapsed,
                string.Empty);
        }
    }
}
