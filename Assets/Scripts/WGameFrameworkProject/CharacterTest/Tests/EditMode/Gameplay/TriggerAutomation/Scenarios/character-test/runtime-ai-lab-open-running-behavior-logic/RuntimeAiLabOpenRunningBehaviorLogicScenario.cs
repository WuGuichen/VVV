using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabOpenRunningBehaviorLogicScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-open-running-behavior-logic";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Open running Behavior Logic workspace",
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
                    "one-click",
                    "behavior-logic"
                },
                externalChecks: new[]
                {
                    new TriggerTestExternalCheck(
                        "runtime-ai-lab-running-behavior-logic-ready",
                        TriggerTestExternalCheckKind.SceneState,
                        "进入 Play、开始测试，并验证 TimelineModeBehaviorLogic 的真实状态。")
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new RuntimeAiLabOpenRunningBehaviorLogicFunction());
            builder.RegisterCall(
                FunctionCallId,
                RuntimeAiLabOpenRunningBehaviorLogicFunction.Id,
                RuntimeAiLabOpenRunningBehaviorLogicArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "open-running-behavior-logic",
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
                RuntimeAiLabOpenRunningBehaviorLogicScheduledAssertion.Instance);
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
    }

    internal sealed class RuntimeAiLabOpenRunningBehaviorLogicArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly RuntimeAiLabOpenRunningBehaviorLogicArguments
            Instance = new RuntimeAiLabOpenRunningBehaviorLogicArguments();

        private RuntimeAiLabOpenRunningBehaviorLogicArguments()
        {
        }
    }

    internal sealed class RuntimeAiLabOpenRunningBehaviorLogicFunction :
        TriggerTestFunction<RuntimeAiLabOpenRunningBehaviorLogicArguments>,
        ITriggerTestFunctionPreviewProvider
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-open-running-behavior-logic");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        public TriggerTestFunctionPreview DescribePreview(
            ITriggerTestFunctionArguments arguments) =>
            new TriggerTestFunctionPreview(
                "一键打开运行中的行为逻辑编辑",
                "Edit Mode · 场景无未保存修改 · 没有同类工作流正在执行",
                "打开 Runtime AI Lab 并进入 Play",
                "开始测试后选中当前行为，并进入 TimelineModeBehaviorLogic");

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            RuntimeAiLabOpenRunningBehaviorLogicArguments arguments)
        {
            return TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow
                    .TryBegin(out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }

    internal sealed class
        RuntimeAiLabOpenRunningBehaviorLogicScheduledAssertion :
        ITriggerTestAssertion
    {
        internal static readonly
            RuntimeAiLabOpenRunningBehaviorLogicScheduledAssertion Instance =
                new RuntimeAiLabOpenRunningBehaviorLogicScheduledAssertion();

        public string AssertionId =>
            "open-running-behavior-logic-scheduled";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            return TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow
                    .ValidateScheduledState(out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }
}
