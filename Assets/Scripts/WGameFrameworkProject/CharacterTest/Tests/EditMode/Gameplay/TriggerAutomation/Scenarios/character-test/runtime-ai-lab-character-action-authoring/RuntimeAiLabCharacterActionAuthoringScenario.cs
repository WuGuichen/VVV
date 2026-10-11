using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabCharacterActionAuthoringScenario : ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-character-action-authoring";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Open embedded CharacterAction Action Composition",
                "character-test",
                TriggerTestExecutionProfile.EditMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger", "character-test", "runtime-ai-lab", "character-action",
                    "editmode", "editor-workflow", "manual", "one-click"
                },
                externalChecks: new[]
                {
                    new TriggerTestExternalCheck(
                        "runtime-ai-lab-character-action-authoring-ready",
                        TriggerTestExternalCheckKind.SceneState,
                        "进入 Play，打开完整动作编排，验证共享会话、七轨、Inspector、Undo 和当前角色临时预览重置。")
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(new RuntimeAiLabCharacterActionAuthoringFunction());
            builder.RegisterCall(
                FunctionCallId,
                RuntimeAiLabCharacterActionAuthoringFunction.Id,
                RuntimeAiLabCharacterActionAuthoringArguments.Instance);
            builder.UseFixture(sink => TriggerTestSignalFunctionFixtureFactory.Create(
                sink,
                new[] { FunctionCallId }));
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "open-character-action-authoring",
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
                RuntimeAiLabCharacterActionAuthoringScheduledAssertion.Instance);
        }
    }

    internal sealed class RuntimeAiLabCharacterActionAuthoringArguments : ITriggerTestFunctionArguments
    {
        internal static readonly RuntimeAiLabCharacterActionAuthoringArguments Instance =
            new RuntimeAiLabCharacterActionAuthoringArguments();
        private RuntimeAiLabCharacterActionAuthoringArguments() { }
    }

    internal sealed class RuntimeAiLabCharacterActionAuthoringFunction :
        TriggerTestFunction<RuntimeAiLabCharacterActionAuthoringArguments>,
        ITriggerTestFunctionPreviewProvider
    {
        internal static readonly TriggerTestFunctionId Id = new TriggerTestFunctionId(
            "mx.trigger.test.function.character-test.runtime-ai-lab-character-action-authoring");
        public override TriggerTestFunctionId FunctionId => Id;
        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        public TriggerTestFunctionPreview DescribePreview(ITriggerTestFunctionArguments arguments) =>
            new TriggerTestFunctionPreview(
                "打开完整 CharacterAction 动作编排",
                "Edit Mode · 场景无未保存修改",
                "打开 Runtime AI Lab 并进入运行中的行为逻辑",
                "验证内嵌工作区、共享命令、临时预览与 frame-0 重置");

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            RuntimeAiLabCharacterActionAuthoringArguments arguments) =>
            TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow.TryBegin(out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
    }

    internal sealed class RuntimeAiLabCharacterActionAuthoringScheduledAssertion : ITriggerTestAssertion
    {
        internal static readonly RuntimeAiLabCharacterActionAuthoringScheduledAssertion Instance =
            new RuntimeAiLabCharacterActionAuthoringScheduledAssertion();
        public string AssertionId => "character-action-authoring-scheduled";
        public TriggerTestOperationResult Evaluate(in TriggerTestAssertionContext context) =>
            TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow.ValidateScheduledState(out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
    }
}
