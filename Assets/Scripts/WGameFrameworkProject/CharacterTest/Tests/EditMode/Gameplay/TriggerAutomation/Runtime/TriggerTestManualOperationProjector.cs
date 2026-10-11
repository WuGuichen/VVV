using System;
using System.Collections.Generic;
using MxFramework.Diagnostics;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal static class TriggerTestManualOperationProjector
    {
        internal static IReadOnlyList<TriggerTestManualOperationViewModel> Project(
            FrameworkSimulationScenarioResult scenario)
        {
            if (scenario == null)
                return Array.Empty<TriggerTestManualOperationViewModel>();

            var operations =
                new List<TriggerTestManualOperationViewModel>(
                    scenario.Events.Count);
            for (int i = 0; i < scenario.Events.Count; i++)
            {
                FrameworkSimulationTimelineEvent source = scenario.Events[i];
                int order = GetOrder(source);
                operations.Add(new TriggerTestManualOperationViewModel(
                    source.Frame,
                    order,
                    GetStage(source, order),
                    GetOperation(source, order),
                    GetReadableResult(source.Summary),
                    source.Category,
                    !IsFailure(scenario, source)));
            }

            operations.Sort(Compare);
            return operations;
        }

        private static int GetOrder(
            FrameworkSimulationTimelineEvent operation)
        {
            if (string.Equals(
                    operation.Source,
                    "TriggerTest.Step",
                    StringComparison.Ordinal))
            {
                return 0;
            }
            if (operation.Category.IndexOf(
                    "activation",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1;
            }
            if (operation.Category.IndexOf(
                    "intent",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }
            if (string.Equals(
                    operation.Source,
                    "TriggerTest.Function",
                    StringComparison.Ordinal))
            {
                return 3;
            }

            return 4;
        }

        private static string GetStage(
            FrameworkSimulationTimelineEvent operation,
            int order)
        {
            switch (order)
            {
                case 0:
                    return "输入";
                case 1:
                case 2:
                    return "Trigger";
                case 3:
                    return "Function";
                default:
                    return "校验";
            }
        }

        private static string GetOperation(
            FrameworkSimulationTimelineEvent operation,
            int order)
        {
            switch (order)
            {
                case 0:
                    return "提交输入";
                case 1:
                    return "规则激活";
                case 2:
                    return "生成意图";
                case 3:
                    return "执行函数";
                default:
                    return "独立断言";
            }
        }

        private static string GetReadableResult(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return "无结果";
            if (summary.StartsWith(
                    "submitted ",
                    StringComparison.Ordinal))
            {
                int countEnd = summary.IndexOf(' ', "submitted ".Length);
                if (countEnd > "submitted ".Length)
                {
                    string count = summary.Substring(
                        "submitted ".Length,
                        countEnd - "submitted ".Length);
                    return "已提交 " + count + " 个 Signal";
                }
            }

            string result = ExtractQuoted(summary, "result=\"");
            if (!string.IsNullOrEmpty(result))
            {
                return result +
                       (summary.IndexOf(
                            "opaqueIdentifiers=0",
                            StringComparison.Ordinal) >= 0
                           ? " · 内部标识 0"
                           : string.Empty);
            }

            string title = ExtractQuoted(summary, "title=\"");
            return !string.IsNullOrEmpty(title) ? title : summary;
        }

        private static string ExtractQuoted(string value, string prefix)
        {
            int start = value.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;
            start += prefix.Length;
            int end = value.IndexOf('"', start);
            return end > start
                ? value.Substring(start, end - start)
                : string.Empty;
        }

        private static bool IsFailure(
            FrameworkSimulationScenarioResult scenario,
            FrameworkSimulationTimelineEvent operation)
        {
            for (int i = 0; i < scenario.Failures.Count; i++)
            {
                FrameworkSimulationFailure failure = scenario.Failures[i];
                if (failure.Frame == operation.Frame &&
                    string.Equals(
                        failure.EntityId,
                        operation.Category,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Compare(
            TriggerTestManualOperationViewModel left,
            TriggerTestManualOperationViewModel right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : string.Compare(
                    left.TechnicalId,
                    right.TechnicalId,
                    StringComparison.Ordinal);
        }
    }
}
