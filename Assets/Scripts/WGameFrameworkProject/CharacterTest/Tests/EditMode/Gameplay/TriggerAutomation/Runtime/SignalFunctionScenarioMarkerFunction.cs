namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class SignalFunctionScenarioMarker
    {
        internal int InvocationCount { get; private set; }

        internal void RecordInvocation()
        {
            InvocationCount++;
        }
    }

    internal sealed class SignalFunctionScenarioMarkerArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly SignalFunctionScenarioMarkerArguments Instance =
            new SignalFunctionScenarioMarkerArguments();

        private SignalFunctionScenarioMarkerArguments()
        {
        }
    }

    internal sealed class SignalFunctionScenarioMarkerFunction :
        TriggerTestFunction<SignalFunctionScenarioMarkerArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.framework.signal-function-marker");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.RuntimeMutation;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            SignalFunctionScenarioMarkerArguments arguments)
        {
            if (!context.Fixture.Services.TryGet(
                    out SignalFunctionScenarioMarker marker))
            {
                return TriggerTestOperationResult.Failed(
                    "Scenario-local signal marker service is unavailable.");
            }

            marker.RecordInvocation();
            return TriggerTestOperationResult.Succeeded(
                "Scenario-local signal marker recorded the invocation.");
        }
    }

    internal sealed class SignalFunctionScenarioMarkerAssertion :
        ITriggerTestAssertion
    {
        internal SignalFunctionScenarioMarkerAssertion(
            string assertionId,
            int expectedCount)
        {
            AssertionId = assertionId;
            ExpectedCount = expectedCount;
        }

        public string AssertionId { get; }
        private int ExpectedCount { get; }

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(
                    out SignalFunctionScenarioMarker marker))
            {
                return TriggerTestOperationResult.Failed(
                    "Scenario-local signal marker service is unavailable.");
            }

            return marker.InvocationCount == ExpectedCount
                ? TriggerTestOperationResult.Succeeded(
                    "Scenario-local signal marker matched expected count.")
                : TriggerTestOperationResult.Failed(
                    "Expected scenario-local signal marker count " +
                    ExpectedCount +
                    " but found " +
                    marker.InvocationCount +
                    ".");
        }
    }
}
