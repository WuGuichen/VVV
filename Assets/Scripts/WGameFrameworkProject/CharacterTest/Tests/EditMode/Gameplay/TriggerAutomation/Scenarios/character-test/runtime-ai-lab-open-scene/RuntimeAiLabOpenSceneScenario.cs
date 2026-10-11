using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabOpenSceneScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-open-scene";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Open Runtime AI Lab scene",
                "character-test",
                TriggerTestExecutionProfile.EditMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger",
                    "character-test",
                    "runtime-ai-lab",
                    "editmode",
                    "manual",
                    "scene-open"
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(new RuntimeAiLabOpenSceneFunction());
            builder.RegisterCall(
                FunctionCallId,
                RuntimeAiLabOpenSceneFunction.Id,
                RuntimeAiLabOpenSceneArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-runtime-ai-lab-scene",
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
                RuntimeAiLabSceneReadyAssertion.Instance);
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
    }

    internal sealed class RuntimeAiLabOpenSceneArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly RuntimeAiLabOpenSceneArguments Instance =
            new RuntimeAiLabOpenSceneArguments();

        private RuntimeAiLabOpenSceneArguments()
        {
        }
    }

    internal sealed class RuntimeAiLabOpenSceneFunction :
        TriggerTestFunction<RuntimeAiLabOpenSceneArguments>,
        ITriggerTestFunctionPreviewProvider
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-open-scene");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        public TriggerTestFunctionPreview DescribePreview(
            ITriggerTestFunctionArguments arguments) =>
            new TriggerTestFunctionPreview(
                "打开 Runtime AI Lab 场景",
                "目标 Scene=" +
                TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                "需要切换场景时，当前已加载场景必须没有未保存修改");

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            RuntimeAiLabOpenSceneArguments arguments)
        {
            return TriggerTestRuntimeAiLabEditorActions.TryOpenTargetScene(
                out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }

    internal sealed class RuntimeAiLabSceneReadyAssertion :
        ITriggerTestAssertion
    {
        internal static readonly RuntimeAiLabSceneReadyAssertion Instance =
            new RuntimeAiLabSceneReadyAssertion();

        public string AssertionId => "runtime-ai-lab-scene-ready";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            return TriggerTestRuntimeAiLabEditorActions.TryValidateTargetScene(
                out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }
}
