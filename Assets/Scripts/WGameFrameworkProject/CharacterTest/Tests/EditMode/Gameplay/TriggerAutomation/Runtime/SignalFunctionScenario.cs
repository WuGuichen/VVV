using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    /// <summary>
    /// Non-discoverable unit-test fixture for Trigger runner coverage.
    /// </summary>
    internal sealed class SignalFunctionScenario : ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.framework.signal-function";
        internal const int FunctionCallId = 1;
        internal const int ScenarioLocalFunctionCallId = 2;
        internal const int SourceDefinitionId =
            TriggerTestSignalFunctionFixtureFactory.SourceDefinitionId;
        internal const int SourceBindingId =
            TriggerTestSignalFunctionFixtureFactory.SourceBindingId;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Signal dispatches a registered test function",
                "framework",
                TriggerTestExecutionProfile.NoEngine,
                maxFrames: 1,
                tags: new[] { "trigger", "signal", "function-call", "fast" });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestIncrementCounterFunction());
            builder.RegisterFunction(
                new SignalFunctionScenarioMarkerFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestIncrementCounterFunction.Id,
                new TriggerTestIncrementCounterArguments(1L));
            builder.RegisterCall(
                ScenarioLocalFunctionCallId,
                SignalFunctionScenarioMarkerFunction.Id,
                SignalFunctionScenarioMarkerArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "submit-signal",
                    new GameplayTriggerSourceBindingId(SourceBindingId),
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(1001) }));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeCountAssertion(
                    "activation-count",
                    TriggerTestRuntimeCountKind.Activation,
                    1));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeCountAssertion(
                    "intent-count",
                    TriggerTestRuntimeCountKind.Intent,
                    2));
            builder.AssertAtFrame(
                0L,
                new TriggerTestCounterAssertion(
                    "function-side-effect",
                    1L));
            builder.AssertAtFrame(
                0L,
                new SignalFunctionScenarioMarkerAssertion(
                    "scenario-local-function-side-effect",
                    1));
        }

        internal static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            CreateFixture(
                functionSink,
                new[] { FunctionCallId, ScenarioLocalFunctionCallId });

        internal static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink,
            int actionCount)
        {
            var callIds = new int[actionCount];
            for (int i = 0; i < callIds.Length; i++)
                callIds[i] = FunctionCallId;
            return CreateFixture(functionSink, callIds);
        }

        internal static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink,
            int[] functionCallIds)
        {
            ITriggerTestFixture fixture =
                TriggerTestSignalFunctionFixtureFactory.Create(
                    functionSink,
                    functionCallIds);
            fixture.Services.Register(new TriggerTestCounter());
            fixture.Services.Register(new SignalFunctionScenarioMarker());
            return fixture;
        }
    }
}
