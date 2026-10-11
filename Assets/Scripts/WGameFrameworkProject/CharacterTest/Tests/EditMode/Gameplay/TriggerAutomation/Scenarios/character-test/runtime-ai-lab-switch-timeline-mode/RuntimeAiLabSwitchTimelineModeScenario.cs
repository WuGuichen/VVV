using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSwitchTimelineModeScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-switch-timeline-mode";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Runtime AI Lab unified timeline mode switch",
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
                    "unified-workbench",
                    "route-alias"
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabTimelineModeFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabTimelineModeFunction.Id,
                TriggerTestRuntimeAiLabTimelineModeArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-unified-timeline-mode-switch",
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
                new TriggerTestRuntimeAiLabTimelineModeAssertion());
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink)
        {
            ITriggerTestFixture fixture =
                TriggerTestSignalFunctionFixtureFactory.Create(
                    functionSink,
                    new[] { FunctionCallId });
            fixture.Services.Register(
                new TriggerTestRuntimeAiLabTimelineModeProbe());
            return fixture;
        }
    }
}
