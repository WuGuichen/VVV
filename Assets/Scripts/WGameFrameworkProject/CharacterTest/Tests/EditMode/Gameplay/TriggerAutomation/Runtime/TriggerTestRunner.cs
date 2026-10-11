using System;
using System.Collections.Generic;
using MxFramework.Diagnostics;
using MxFramework.Runtime;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestRunner
    {
        private const double DefaultDeltaTime = 1d / 60d;
        private readonly bool _writeLatestReport;

        internal TriggerTestRunner(bool writeLatestReport = true)
        {
            _writeLatestReport = writeLatestReport;
        }

        internal TriggerTestRunResult Run(ITriggerTestScenario scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            TriggerTestDescriptor descriptor = scenario.Descriptor ??
                throw new InvalidOperationException(
                    "Trigger test scenario returned no descriptor.");
            var events = new List<FrameworkSimulationTimelineEvent>();
            var failures = new List<FrameworkSimulationFailure>();
            var metrics = new List<FrameworkSimulationMetric>();
            ITriggerTestFixture fixture = null;
            int executedFrames = 0;
            int executedSteps = 0;
            int executedAssertions = 0;
            int executedFunctions = 0;

            try
            {
                var builder = new TriggerTestScenarioBuilder(descriptor);
                scenario.Build(builder);
                TriggerTestScenarioPlan plan = builder.BuildPlan();
                var sink = new TriggerTestFunctionActionSink(
                    plan.CopyCallIds(),
                    descriptor.MaxPendingFunctionCalls);
                fixture = plan.FixtureFactory(sink);
                if (fixture == null)
                    throw new InvalidOperationException(
                        "Trigger test fixture factory returned null.");
                if (fixture.TriggerRuntime == null)
                    throw new InvalidOperationException(
                        "Trigger test fixture returned no Trigger runtime.");

                fixture.Initialize();
                int stepIndex = 0;
                int assertionIndex = 0;
                for (long frame = 0L;
                     frame < descriptor.MaxFrames && failures.Count == 0;
                     frame++)
                {
                    RuntimeFrame runtimeFrame = new RuntimeFrame(frame);
                    while (stepIndex < plan.StepCount &&
                           plan.GetStep(stepIndex).Frame == frame)
                    {
                        TriggerTestScheduledStep scheduled =
                            plan.GetStep(stepIndex++);
                        TriggerTestOperationResult result = scheduled.Step.Execute(
                            new TriggerTestStepContext(runtimeFrame, fixture));
                        executedSteps++;
                        AddEvent(
                            events,
                            frame,
                            "TriggerTest.Step",
                            scheduled.Step.StepId,
                            result.Summary,
                            descriptor.ScenarioId.Value);
                        if (!result.Success)
                        {
                            AddFailure(
                                failures,
                                frame,
                                scheduled.Step.StepId,
                                result.Summary,
                                descriptor.ScenarioId.Value);
                            break;
                        }
                    }

                    if (failures.Count != 0)
                        break;

                    fixture.Tick(frame, DefaultDeltaTime);
                    executedFrames++;

                    while (sink.TryDequeue(
                               out TriggerTestFunctionCallRecord record))
                    {
                        if (!ExecuteFunction(
                                plan,
                                fixture,
                                record,
                                events,
                                failures,
                                descriptor.ScenarioId.Value))
                        {
                            break;
                        }

                        executedFunctions++;
                    }

                    if (failures.Count != 0)
                        break;

                    while (assertionIndex < plan.AssertionCount &&
                           plan.GetAssertion(assertionIndex).Frame == frame)
                    {
                        TriggerTestScheduledAssertion scheduled =
                            plan.GetAssertion(assertionIndex++);
                        TriggerTestOperationResult result =
                            scheduled.Assertion.Evaluate(
                                new TriggerTestAssertionContext(
                                    runtimeFrame,
                                    fixture));
                        executedAssertions++;
                        AddEvent(
                            events,
                            frame,
                            "TriggerTest.Assertion",
                            scheduled.Assertion.AssertionId,
                            result.Summary,
                            descriptor.ScenarioId.Value);
                        if (!result.Success)
                        {
                            AddFailure(
                                failures,
                                frame,
                                scheduled.Assertion.AssertionId,
                                result.Summary,
                                descriptor.ScenarioId.Value);
                            break;
                        }
                    }
                }

                if (failures.Count == 0 &&
                    executedFrames != descriptor.MaxFrames)
                {
                    AddFailure(
                        failures,
                        Math.Max(0, executedFrames),
                        "runner.frame-count",
                        "Scenario ended before its configured frame count.",
                        descriptor.ScenarioId.Value);
                }
            }
            catch (Exception exception)
            {
                AddFailure(
                    failures,
                    Math.Max(0, executedFrames),
                    "runner.exception",
                    exception.GetType().Name + ": " + exception.Message,
                    descriptor.ScenarioId.Value);
            }
            finally
            {
                if (fixture != null)
                {
                    try
                    {
                        fixture.Dispose();
                    }
                    catch (Exception exception)
                    {
                        AddFailure(
                            failures,
                            Math.Max(0, executedFrames),
                            "runner.dispose",
                            exception.GetType().Name + ": " + exception.Message,
                            descriptor.ScenarioId.Value);
                    }
                }
            }

            metrics.Add(new FrameworkSimulationMetric(
                "trigger-test",
                "steps",
                executedSteps));
            metrics.Add(new FrameworkSimulationMetric(
                "trigger-test",
                "functionCalls",
                executedFunctions));
            metrics.Add(new FrameworkSimulationMetric(
                "trigger-test",
                "assertions",
                executedAssertions));
            metrics.Add(new FrameworkSimulationMetric(
                "trigger-test",
                "externalChecks",
                descriptor.ExternalChecks.Count));

            var simulationResult = new FrameworkSimulationScenarioResult(
                descriptor.ScenarioId.Value,
                executedFrames,
                metrics,
                events,
                failures);
            TriggerTestRunStatus status = failures.Count != 0
                ? TriggerTestRunStatus.Failed
                : descriptor.ExternalChecks.Count != 0
                    ? TriggerTestRunStatus.PendingExternalEvidence
                    : TriggerTestRunStatus.Passed;
            var runResult = new TriggerTestRunResult(
                descriptor,
                status,
                simulationResult);
            if (_writeLatestReport)
                TriggerTestReportWriter.WriteLatest(runResult);
            return runResult;
        }

        private static bool ExecuteFunction(
            TriggerTestScenarioPlan plan,
            ITriggerTestFixture fixture,
            TriggerTestFunctionCallRecord record,
            List<FrameworkSimulationTimelineEvent> events,
            List<FrameworkSimulationFailure> failures,
            string traceId)
        {
            if (!plan.TryGetCall(record.CallId, out TriggerTestFunctionCallSpec call))
            {
                AddFailure(
                    failures,
                    record.Intent.Frame.Value,
                    "function.call." + record.CallId,
                    "Committed call id is not registered.",
                    traceId);
                return false;
            }
            if (!plan.TryGetFunction(
                    call.FunctionId,
                    out ITriggerTestFunction function))
            {
                AddFailure(
                    failures,
                    record.Intent.Frame.Value,
                    call.FunctionId.Value,
                    "Committed function id is not registered.",
                    traceId);
                return false;
            }

            try
            {
                var context = new TriggerTestFunctionContext(
                    record.CallId,
                    record.Intent.Frame,
                    fixture,
                    record.Intent);
                TriggerTestOperationResult result = function.Execute(
                    in context,
                    call.Arguments);
                AddEvent(
                    events,
                    record.Intent.Frame.Value,
                    "TriggerTest.Function",
                    function.FunctionId.Value,
                    result.Summary,
                    traceId);
                if (result.Success)
                    return true;

                AddFailure(
                    failures,
                    record.Intent.Frame.Value,
                    function.FunctionId.Value,
                    result.Summary,
                    traceId);
                return false;
            }
            catch (Exception exception)
            {
                AddFailure(
                    failures,
                    record.Intent.Frame.Value,
                    function.FunctionId.Value,
                    exception.GetType().Name + ": " + exception.Message,
                    traceId);
                return false;
            }
        }

        private static void AddEvent(
            List<FrameworkSimulationTimelineEvent> events,
            long frame,
            string source,
            string category,
            string summary,
            string traceId)
        {
            events.Add(new FrameworkSimulationTimelineEvent(
                frame,
                source,
                category,
                string.Empty,
                traceId,
                summary));
        }

        private static void AddFailure(
            List<FrameworkSimulationFailure> failures,
            long frame,
            string entityId,
            string message,
            string traceId)
        {
            failures.Add(new FrameworkSimulationFailure(
                Math.Max(0L, frame),
                entityId,
                traceId,
                message,
                string.Empty));
        }
    }

    internal sealed class TriggerTestSimulationScenarioAdapter :
        IFrameworkSimulationScenario
    {
        private readonly ITriggerTestScenario _scenario;
        private readonly TriggerTestRunner _runner;

        internal TriggerTestSimulationScenarioAdapter(
            ITriggerTestScenario scenario,
            bool writeLatestReport = true)
        {
            _scenario = scenario ??
                throw new ArgumentNullException(nameof(scenario));
            _runner = new TriggerTestRunner(writeLatestReport);
        }

        public string Name => _scenario.Descriptor.ScenarioId.Value;

        public FrameworkSimulationScenarioResult Run() =>
            _runner.Run(_scenario).SimulationResult;
    }
}
