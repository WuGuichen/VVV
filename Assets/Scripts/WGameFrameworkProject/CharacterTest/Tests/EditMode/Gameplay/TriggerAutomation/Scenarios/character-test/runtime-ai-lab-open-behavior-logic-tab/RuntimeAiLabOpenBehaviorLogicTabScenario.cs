using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabOpenBehaviorLogicTabScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-open-behavior-logic-tab";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Open Runtime AI Lab Behavior Logic mode",
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
                    "timeline",
                    "behavior-logic",
                    "behavior-logic-mode"
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabBehaviorLogicTabFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabBehaviorLogicTabFunction.Id,
                TriggerTestRuntimeAiLabBehaviorLogicTabArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-open-behavior-logic-tab",
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
                new TriggerTestRuntimeAiLabBehaviorLogicTabAssertion());
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink)
        {
            ITriggerTestFixture fixture =
                TriggerTestSignalFunctionFixtureFactory.Create(
                    functionSink,
                    new[] { FunctionCallId });
            fixture.Services.Register(
                new TriggerTestRuntimeAiLabBehaviorLogicTabProbe());
            return fixture;
        }
    }
}
