using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabEnterPlayModeScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-enter-play-mode";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Enter Play Mode",
                "character-test",
                TriggerTestExecutionProfile.EditMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger",
                    "character-test",
                    "runtime-ai-lab",
                    "editmode",
                    "editor-workflow",
                    "manual",
                    "play-mode"
                },
                externalChecks: new[]
                {
                    new TriggerTestExternalCheck(
                        "runtime-ai-lab-playmode-ready",
                        TriggerTestExternalCheckKind.SceneState,
                        "进入 Play Mode，且 Runtime AI Lab 中有且仅有 1 个 Ready Controller。")
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new RuntimeAiLabEnterPlayModeFunction());
            builder.RegisterCall(
                FunctionCallId,
                RuntimeAiLabEnterPlayModeFunction.Id,
                RuntimeAiLabEnterPlayModeArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-enter-play-mode",
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
                RuntimeAiLabEnterPlayModeScheduledAssertion.Instance);
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
    }

    internal sealed class RuntimeAiLabEnterPlayModeArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly RuntimeAiLabEnterPlayModeArguments Instance =
            new RuntimeAiLabEnterPlayModeArguments();

        private RuntimeAiLabEnterPlayModeArguments()
        {
        }
    }

    internal sealed class RuntimeAiLabEnterPlayModeFunction :
        TriggerTestFunction<RuntimeAiLabEnterPlayModeArguments>,
        ITriggerTestFunctionPreviewProvider
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-enter-play-mode");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        public TriggerTestFunctionPreview DescribePreview(
            ITriggerTestFunctionArguments arguments) =>
            new TriggerTestFunctionPreview(
                "请求进入 Play Mode",
                "目标 Scene=" +
                TriggerTestRuntimeAiLabEditorActions.TargetScenePath +
                " · Unity 尚未处于或正在进入 Play Mode" +
                " · 没有同类进入 Play Mode 流程正在执行",
                "活动场景为 Runtime AI Lab",
                "场景内恰好 1 个活动 Controller");

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            RuntimeAiLabEnterPlayModeArguments arguments)
        {
            return TriggerTestRuntimeAiLabEnterPlayModeWorkflow.TryBegin(
                out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }

    internal sealed class RuntimeAiLabEnterPlayModeScheduledAssertion :
        ITriggerTestAssertion
    {
        internal static readonly
            RuntimeAiLabEnterPlayModeScheduledAssertion Instance =
                new RuntimeAiLabEnterPlayModeScheduledAssertion();

        public string AssertionId => "enter-play-mode-scheduled";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            return TriggerTestRuntimeAiLabEnterPlayModeWorkflow
                    .ValidateScheduledState(out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }
}
