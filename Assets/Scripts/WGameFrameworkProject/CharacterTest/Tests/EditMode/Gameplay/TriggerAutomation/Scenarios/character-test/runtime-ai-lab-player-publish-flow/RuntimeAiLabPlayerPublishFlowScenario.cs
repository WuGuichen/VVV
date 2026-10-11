using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabPlayerPublishFlowScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-player-publish-flow";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Runtime AI Lab player authoring and publish flow",
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
                    "player-deck",
                    "authoring",
                    "publish",
                    "apply-f-plus-one"
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabPlayerPublishFlowFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabPlayerPublishFlowFunction.Id,
                TriggerTestRuntimeAiLabPlayerPublishFlowArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-player-publish-flow",
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
                new TriggerTestRuntimeAiLabPlayerPublishFlowAssertion());
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink)
        {
            ITriggerTestFixture fixture =
                TriggerTestSignalFunctionFixtureFactory.Create(
                    functionSink,
                    new[] { FunctionCallId });
            fixture.Services.Register(
                new TriggerTestRuntimeAiLabPlayerPublishFlowProbe());
            return fixture;
        }
    }
}
