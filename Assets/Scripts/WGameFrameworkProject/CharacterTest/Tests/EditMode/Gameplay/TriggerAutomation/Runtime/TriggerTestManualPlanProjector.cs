using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal static class TriggerTestManualPlanProjector
    {
        internal static IReadOnlyList<TriggerTestManualOperationViewModel> Project(
            ITriggerTestScenario scenario)
        {
            if (scenario == null)
                return Array.Empty<TriggerTestManualOperationViewModel>();

            var builder = new TriggerTestScenarioBuilder(scenario.Descriptor);
            scenario.Build(builder);
            TriggerTestScenarioPlan plan = builder.BuildPlan();
            int[] callIds = plan.CopyCallIds();
            GameplayTriggerAuthoringDocument document =
                TriggerTestSignalFunctionFixtureFactory
                    .CreateAuthoringDocument(callIds);
            var operations =
                new List<TriggerTestManualOperationViewModel>(
                    3 +
                    (callIds.Length * 2) +
                    scenario.Descriptor.ExternalChecks.Count);

            GameplayTriggerDefinitionAuthoring[] definitions =
                document.Definitions ?? Array.Empty<GameplayTriggerDefinitionAuthoring>();
            for (int definitionIndex = 0;
                 definitionIndex < definitions.Length;
                 definitionIndex++)
            {
                GameplayTriggerDefinitionAuthoring definition =
                    definitions[definitionIndex];
                GameplayTriggerSourceAuthoring source =
                    FindSource(document, definition.SourceDefinitionId);
                GameplayTriggerBindingAuthoring binding =
                    FindBinding(document, definition.DefinitionId);
                operations.Add(new TriggerTestManualOperationViewModel(
                    0L,
                    0,
                    "事件",
                    source == null ? "未知来源" : source.SourceKind.ToString(),
                    string.Empty,
                    "source-" + definition.SourceDefinitionId,
                    true,
                    FormatSource(definition, source, binding)));

                GameplayTriggerRouteAuthoring[] routes =
                    definition.Routes ??
                    Array.Empty<GameplayTriggerRouteAuthoring>();
                for (int routeIndex = 0;
                     routeIndex < routes.Length;
                     routeIndex++)
                {
                    GameplayTriggerRouteAuthoring route = routes[routeIndex];
                    GameplayTriggerConditionAuthoring condition =
                        FindCondition(document, route.ConditionId);
                    operations.Add(new TriggerTestManualOperationViewModel(
                        0L,
                        1,
                        "触发过滤",
                        FormatCondition(
                            condition == null ? null : condition.Root),
                        string.Empty,
                        "condition-" + route.ConditionId,
                        condition != null && condition.Root != null,
                        "ConditionId=" + route.ConditionId +
                        " · " +
                        (condition == null || condition.Root == null
                            ? "未找到条件定义"
                            : condition.Root.Operation.ToString())));
                    operations.Add(new TriggerTestManualOperationViewModel(
                        0L,
                        2,
                        "触发方式",
                        FormatOccurrence(route.OccurrenceMask),
                        FormatNonDefaultRoutePolicy(route),
                        "route-" + route.RouteId,
                        true,
                        FormatRoutePolicy(route)));

                    GameplayTriggerActionAuthoring[] actions =
                        route.Actions ??
                        Array.Empty<GameplayTriggerActionAuthoring>();
                    for (int actionIndex = 0;
                         actionIndex < actions.Length;
                         actionIndex++)
                    {
                        AddAction(
                            operations,
                            plan,
                            actions[actionIndex],
                            route.RouteId,
                            actionIndex);
                    }
                }
            }

            for (int checkIndex = 0;
                 checkIndex < scenario.Descriptor.ExternalChecks.Count;
                 checkIndex++)
            {
                TriggerTestExternalCheck check =
                    scenario.Descriptor.ExternalChecks[checkIndex];
                operations.Add(new TriggerTestManualOperationViewModel(
                    scenario.Descriptor.MaxFrames - 1L,
                    5,
                    "验收",
                    FormatExternalCheckKind(check.Kind),
                    check.ExpectedEvidence,
                    check.CheckId,
                    true));
            }

            return operations;
        }

        private static string FormatOccurrence(
            GameplayTriggerOccurrenceKind occurrence)
        {
            switch (occurrence)
            {
                case GameplayTriggerOccurrenceKind.Pulsed:
                    return "每次 Signal 脉冲";
                case GameplayTriggerOccurrenceKind.Entered:
                    return "进入时";
                case GameplayTriggerOccurrenceKind.Stayed:
                    return "停留时";
                case GameplayTriggerOccurrenceKind.Exited:
                    return "离开时";
                default:
                    return occurrence.ToString();
            }
        }

        private static string FormatExternalCheckKind(
            TriggerTestExternalCheckKind kind)
        {
            switch (kind)
            {
                case TriggerTestExternalCheckKind.UnityCompile:
                    return "Unity 编译";
                case TriggerTestExternalCheckKind.ConsoleNoErrors:
                    return "Console 无错误";
                case TriggerTestExternalCheckKind.FocusedEditMode:
                    return "EditMode 测试";
                case TriggerTestExternalCheckKind.FocusedPlayMode:
                    return "PlayMode 测试";
                case TriggerTestExternalCheckKind.SceneState:
                    return "场景状态";
                case TriggerTestExternalCheckKind.ScreenshotReview:
                    return "画面检查";
                case TriggerTestExternalCheckKind.CustomToolEvidence:
                    return "工具验证";
                default:
                    return kind.ToString();
            }
        }

        private static GameplayTriggerSourceAuthoring FindSource(
            GameplayTriggerAuthoringDocument document,
            int sourceDefinitionId)
        {
            GameplayTriggerSourceAuthoring[] sources =
                document.Sources ??
                Array.Empty<GameplayTriggerSourceAuthoring>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].SourceDefinitionId == sourceDefinitionId)
                    return sources[i];
            }

            return null;
        }

        private static GameplayTriggerConditionAuthoring FindCondition(
            GameplayTriggerAuthoringDocument document,
            int conditionId)
        {
            GameplayTriggerConditionAuthoring[] conditions =
                document.Conditions ??
                Array.Empty<GameplayTriggerConditionAuthoring>();
            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i].ConditionId == conditionId)
                    return conditions[i];
            }

            return null;
        }

        private static GameplayTriggerBindingAuthoring FindBinding(
            GameplayTriggerAuthoringDocument document,
            int definitionId)
        {
            GameplayTriggerBindingAuthoring[] bindings =
                document.Bindings ??
                Array.Empty<GameplayTriggerBindingAuthoring>();
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].DefinitionId == definitionId)
                    return bindings[i];
            }

            return null;
        }

        private static string FormatSource(
            GameplayTriggerDefinitionAuthoring definition,
            GameplayTriggerSourceAuthoring source,
            GameplayTriggerBindingAuthoring binding)
        {
            string result =
                "DefinitionId=" + definition.DefinitionId +
                " · SourceDefinitionId=" +
                definition.SourceDefinitionId;
            if (source != null)
            {
                switch (source.SourceKind)
                {
                    case GameplayTriggerSourceKind.Signal:
                        result += " · " + source.SignalOrdering;
                        break;
                    case GameplayTriggerSourceKind.Threshold:
                        result +=
                            " · " +
                            source.ThresholdDirection +
                            " · Enter=" +
                            FormatFix64(source.EnterThreshold) +
                            " · Exit=" +
                            FormatFix64(source.ExitThreshold) +
                            " · " +
                            source.InitialSamplePolicy +
                            " · " +
                            source.DeparturePolicy;
                        break;
                    default:
                        result +=
                            " · " +
                            source.InitialSamplePolicy +
                            " · " +
                            source.DeparturePolicy;
                        break;
                }
            }

            if (binding != null)
            {
                result +=
                    " · SourceBindingId=" +
                    binding.SourceBindingId +
                    (binding.InitiallyEnabled
                        ? " · 初始启用"
                        : " · 初始禁用");
            }

            return result;
        }

        private static string FormatFix64(
            GameplayTriggerFix64Authoring value) =>
            value.Kind == GameplayTriggerFix64InputKind.InvariantDecimal
                ? value.InvariantDecimal
                : "Raw(" + value.RawValue + ")";

        private static string FormatCondition(
            GameplayTriggerConditionNodeAuthoring node)
        {
            if (node == null)
                return "未找到条件";

            switch (node.Operation)
            {
                case GameplayTriggerConditionOperation.PushTrue:
                    return "无";
                case GameplayTriggerConditionOperation.PushFalse:
                    return "全部拦截";
                case GameplayTriggerConditionOperation.OccurrenceMask:
                    return "发生类型包含 " +
                        (GameplayTriggerOccurrenceKind)node.Operand;
                case GameplayTriggerConditionOperation.SignalIdEquals:
                    return "SignalId = " + node.Operand;
                case GameplayTriggerConditionOperation.HasSubject:
                    return "存在 Subject";
                case GameplayTriggerConditionOperation.HasInstigator:
                    return "存在 Instigator";
                case GameplayTriggerConditionOperation.PreviousScalar:
                    return FormatComparison(
                        "PreviousScalar",
                        node.Comparison,
                        node.Operand);
                case GameplayTriggerConditionOperation.CurrentScalar:
                    return FormatComparison(
                        "CurrentScalar",
                        node.Comparison,
                        node.Operand);
                case GameplayTriggerConditionOperation.Payload0:
                    return FormatComparison(
                        "Payload0",
                        node.Comparison,
                        node.Operand);
                case GameplayTriggerConditionOperation.Payload1:
                    return FormatComparison(
                        "Payload1",
                        node.Comparison,
                        node.Operand);
                case GameplayTriggerConditionOperation.Payload2:
                    return FormatComparison(
                        "Payload2",
                        node.Comparison,
                        node.Operand);
                case GameplayTriggerConditionOperation.Not:
                    return "非（" + FormatChild(node, 0) + "）";
                case GameplayTriggerConditionOperation.All:
                    return JoinConditions(node.Children, " 且 ");
                case GameplayTriggerConditionOperation.Any:
                    return JoinConditions(node.Children, " 或 ");
                default:
                    return node.Operation.ToString();
            }
        }

        private static string FormatChild(
            GameplayTriggerConditionNodeAuthoring node,
            int index) =>
            node.Children != null && index >= 0 && index < node.Children.Length
                ? FormatCondition(node.Children[index])
                : "缺少子条件";

        private static string JoinConditions(
            GameplayTriggerConditionNodeAuthoring[] children,
            string separator)
        {
            if (children == null || children.Length == 0)
                return "无子条件";

            var values = new string[children.Length];
            for (int i = 0; i < children.Length; i++)
                values[i] = FormatCondition(children[i]);
            return "（" + string.Join(separator, values) + "）";
        }

        private static string FormatComparison(
            string field,
            GameplayTriggerComparison comparison,
            long operand) =>
            field +
            " " +
            GetComparisonSymbol(comparison) +
            " " +
            operand;

        private static string GetComparisonSymbol(
            GameplayTriggerComparison comparison)
        {
            switch (comparison)
            {
                case GameplayTriggerComparison.NotEqual:
                    return "!=";
                case GameplayTriggerComparison.Less:
                    return "<";
                case GameplayTriggerComparison.LessOrEqual:
                    return "<=";
                case GameplayTriggerComparison.Greater:
                    return ">";
                case GameplayTriggerComparison.GreaterOrEqual:
                    return ">=";
                default:
                    return "=";
            }
        }

        private static string FormatRoutePolicy(
            GameplayTriggerRouteAuthoring route)
        {
            string result =
                "RouteId=" + route.RouteId +
                " · Scope=" + route.StateScope +
                (route.MaxActivations <= 0
                    ? " · 次数不限"
                    : " · 最多触发 " + route.MaxActivations + " 次") +
                (route.CooldownFrames <= 0L
                    ? " · 无冷却"
                    : " · 冷却 " + route.CooldownFrames + " 帧");
            if (route.PresenceLimit != GameplayTriggerPresenceLimit.Ignore)
                result += " · " + route.PresenceLimit;
            if (route.StayIntervalFrames > 0L)
                result += " · Stay 间隔 " + route.StayIntervalFrames + " 帧";
            if (route.ExecutionOrder != 0)
                result += " · 顺序 " + route.ExecutionOrder;
            if (route.ExclusiveGroupId != 0)
            {
                result +=
                    " · 互斥组 " +
                    route.ExclusiveGroupId +
                    " / 优先级 " +
                    route.WinnerPriority +
                    " / " +
                    route.ArbitrationScope;
            }
            if (route.AllowRepeatingCycle)
            {
                result +=
                    " · 允许循环 / 最小间隔 " +
                    route.MinIntervalFrames +
                    " 帧";
            }

            return result;
        }

        private static string FormatNonDefaultRoutePolicy(
            GameplayTriggerRouteAuthoring route)
        {
            var values = new List<string>(4);
            if (route.MaxActivations > 0)
                values.Add("最多触发 " + route.MaxActivations + " 次");
            if (route.CooldownFrames > 0L)
                values.Add("冷却 " + route.CooldownFrames + " 帧");
            if (route.PresenceLimit != GameplayTriggerPresenceLimit.Ignore)
                values.Add("每次进入只触发一次");
            if (route.StayIntervalFrames > 0L)
                values.Add("间隔 " + route.StayIntervalFrames + " 帧");
            return values.Count == 0
                ? string.Empty
                : string.Join(" · ", values);
        }

        private static void AddAction(
            List<TriggerTestManualOperationViewModel> operations,
            TriggerTestScenarioPlan plan,
            GameplayTriggerActionAuthoring action,
            int routeId,
            int actionIndex)
        {
            bool isFunctionCall =
                action.ActionKindId ==
                TriggerTestActionKindIds.FunctionCallValue;
            string operation =
                (isFunctionCall
                    ? "FunctionCall"
                    : "ActionKind " + action.ActionKindId) +
                " → " +
                action.TargetSelector;
            string result =
                "ActionKindId=" + action.ActionKindId +
                " · RouteId=" + routeId;
            string technicalId =
                "route-" + routeId + "-action-" + actionIndex;
            bool succeeded = true;
            string behavior = string.Empty;
            string visibleArguments = string.Empty;
            string actionTechnicalDetails = string.Empty;
            string resolvedFunctionName = string.Empty;

            if (isFunctionCall &&
                action.Payload0 > 0L &&
                action.Payload0 <= int.MaxValue &&
                plan.TryGetCall(
                    (int)action.Payload0,
                    out TriggerTestFunctionCallSpec call))
            {
                plan.TryGetFunction(
                    call.FunctionId,
                    out ITriggerTestFunction function);
                string functionName =
                    GetStableIdSuffix(call.FunctionId.Value);
                resolvedFunctionName = functionName;
                operation += " · " + functionName;
                string formattedArguments =
                    FormatArguments(call.Arguments);
                result +=
                    " · CallId=" +
                    call.CallId +
                    " · Function=" +
                    functionName +
                    " · " +
                    (function == null
                        ? "UnknownCapability"
                        : function.Capability.ToString()) +
                    " · " +
                    formattedArguments;
                if (!string.Equals(
                        formattedArguments,
                        "无固定参数",
                        StringComparison.Ordinal))
                {
                    visibleArguments = "参数：" + formattedArguments;
                }
                technicalId = call.FunctionId.Value;

                var preconditions = new List<string>(5);
                string preconditionTechnicalDetails = string.Empty;
                string environment =
                    FormatExecutionProfile(plan.Descriptor.ExecutionProfile);
                if (!string.IsNullOrEmpty(environment))
                    preconditions.Add(environment);
                if (function is ITriggerTestFunctionPreviewProvider provider)
                {
                    TriggerTestFunctionPreview preview =
                        provider.DescribePreview(call.Arguments);
                    behavior = preview.Behavior;
                    preconditionTechnicalDetails =
                        preview.TechnicalDetails;
                    for (int i = 0; i < preview.Preconditions.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(preview.Preconditions[i]))
                            preconditions.Add(preview.Preconditions[i]);
                    }
                }

                if (preconditions.Count > 0)
                {
                    operations.Add(
                        new TriggerTestManualOperationViewModel(
                            0L,
                            3 + actionIndex,
                            "执行前提",
                            string.IsNullOrEmpty(behavior)
                                ? functionName
                                : behavior,
                            string.Join(" · ", preconditions),
                            technicalId + "-preconditions",
                            true,
                            preconditionTechnicalDetails));
                }
            }
            else
            {
                result +=
                    " · Payload=(" +
                    action.Payload0 +
                    ", " +
                    action.Payload1 +
                    ", " +
                    action.Payload2 +
                    ")";
                succeeded = !isFunctionCall;
            }

            if (action.TargetSlotId != 0)
                result += " · TargetSlotId=" + action.TargetSlotId;
            if (isFunctionCall &&
                (action.Payload1 != 0L || action.Payload2 != 0L))
            {
                result +=
                    " · ExtraPayload=(" +
                    action.Payload1 +
                    ", " +
                    action.Payload2 +
                    ")";
            }

            if (!string.IsNullOrEmpty(behavior))
            {
                operation =
                    behavior;
                if (action.TargetSelector !=
                    GameplayTriggerTargetSelector.Owner)
                {
                    operation += " → " + action.TargetSelector;
                }
                actionTechnicalDetails = "FunctionCall · " + result;
            }
            else if (isFunctionCall)
            {
                operation = string.IsNullOrEmpty(resolvedFunctionName)
                    ? "调用未解析 Function"
                    : "调用 " + resolvedFunctionName;
                if (action.TargetSelector !=
                    GameplayTriggerTargetSelector.Owner)
                {
                    operation += " → " + action.TargetSelector;
                }
                actionTechnicalDetails = result;
            }
            else
            {
                actionTechnicalDetails = result;
            }

            operations.Add(new TriggerTestManualOperationViewModel(
                0L,
                4 + actionIndex,
                "行为",
                operation,
                visibleArguments,
                technicalId,
                succeeded,
                actionTechnicalDetails));
        }

        private static string FormatExecutionProfile(
            TriggerTestExecutionProfile profile)
        {
            switch (profile)
            {
                case TriggerTestExecutionProfile.EditMode:
                    return "Unity 必须处于 Edit Mode";
                case TriggerTestExecutionProfile.PlayMode:
                    return "Unity 必须处于 Play Mode";
                default:
                    return string.Empty;
            }
        }

        private static string FormatArguments(
            ITriggerTestFunctionArguments arguments)
        {
            if (arguments == null)
                return "无固定参数";

            PropertyInfo[] properties = arguments
                .GetType()
                .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
            Array.Sort(properties, CompareProperties);
            var values = new List<string>(properties.Length);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetIndexParameters().Length != 0 ||
                    property.GetGetMethod(true) == null)
                {
                    continue;
                }

                object value;
                try
                {
                    value = property.GetValue(arguments, null);
                }
                catch (TargetInvocationException)
                {
                    value = "<读取失败>";
                }

                values.Add(
                    property.Name +
                    "=" +
                    FormatArgumentValue(value));
            }

            return values.Count == 0
                ? "无固定参数"
                : string.Join(" · ", values);
        }

        private static string FormatArgumentValue(object value)
        {
            if (value == null)
                return "null";
            if (value is string text)
                return "\"" + text + "\"";
            if (value is IFormattable formattable)
            {
                return formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        private static int CompareProperties(
            PropertyInfo left,
            PropertyInfo right) =>
            string.Compare(
                left.Name,
                right.Name,
                StringComparison.Ordinal);

        private static string GetStableIdSuffix(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
                return string.Empty;
            int separator = stableId.LastIndexOf('.');
            return separator >= 0 && separator + 1 < stableId.Length
                ? stableId.Substring(separator + 1)
                : stableId;
        }
    }
}
