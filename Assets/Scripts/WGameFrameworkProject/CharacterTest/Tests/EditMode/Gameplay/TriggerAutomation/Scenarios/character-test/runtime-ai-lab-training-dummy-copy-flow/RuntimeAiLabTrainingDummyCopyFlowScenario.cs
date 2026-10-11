using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabTrainingDummyCopyFlowScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-training-dummy-copy-flow";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Save and run Runtime AI Lab training-dummy copy",
                "character-test",
                TriggerTestExecutionProfile.PlayMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger",
                    "character-test",
                    "runtime-ai-lab",
                    "playmode",
                    "manual",
                    "setup",
                    "behavior-group-copy",
                    "training-dummy"
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabTrainingDummyCopyFlowFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabTrainingDummyCopyFlowFunction.Id,
                TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-training-dummy-copy-flow",
                    new GameplayTriggerSourceBindingId(
                        TriggerTestSignalFunctionFixtureFactory.SourceBindingId),
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(1) }));
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
                    1));
            builder.AssertAtFrame(
                0L,
                new TriggerTestRuntimeAiLabTrainingDummyCopyFlowAssertion());
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink)
        {
            ITriggerTestFixture fixture = TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
            fixture.Services.Register(
                new TriggerTestRuntimeAiLabTrainingDummyCopyFlowProbe());
            return fixture;
        }
    }
}
