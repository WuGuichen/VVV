using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class TacticalUiOverviewInspectScenario : ITriggerTestScenario
    {
        internal const string ScenarioId = "mx.trigger.test.character-test.tactical-ui-overview-inspect";
        public TriggerTestDescriptor Descriptor { get; } = new TriggerTestDescriptor(
            ScenarioId, "战术卡制作界面：真实点击与布局检查", "character-test", TriggerTestExecutionProfile.PlayMode,
            maxFrames: 1, tags: new[] { "manual", "runtime-ai-lab", "authoring", "tactical", "ui-input" },
            externalChecks: new[] {
                new TriggerTestExternalCheck("tactical-ui-v5-input", TriggerTestExternalCheckKind.SceneState,
                    "等待TacticalUiV5Workflow的实际鼠标／键盘流程完成，检查独立结果与源未变化。"),
                new TriggerTestExternalCheck("tactical-ui-v5-visual", TriggerTestExternalCheckKind.ScreenshotReview,
                    "检查Library/Task04UiReworkEvidence/overview-*内真实Game截图，与确认的v5对照；内部Trigger通过不代替视觉通过。") });

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(new StartOverview());
            builder.RegisterCall(1, StartOverview.Id, Arguments.Instance);
            builder.UseFixture(sink => TriggerTestSignalFunctionFixtureFactory.Create(sink, new[] { 1 }));
            builder.AtFrame(0, new TriggerTestSubmitSignalsStep("start-tactical-ui-input",
                new GameplayTriggerSourceBindingId(TriggerTestSignalFunctionFixtureFactory.SourceBindingId),
                GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { new GameplayTriggerSignal(1) }));
            builder.AssertAtFrame(0, new TriggerTestRuntimeCountAssertion("activation", TriggerTestRuntimeCountKind.Activation, 1));
            builder.AssertAtFrame(0, new StartedAssertion());
        }

        private sealed class Arguments : ITriggerTestFunctionArguments
        { internal static readonly Arguments Instance = new Arguments(); }

        private sealed class StartOverview : TriggerTestFunction<Arguments>, ITriggerTestFunctionPreviewProvider
        {
            internal static readonly TriggerTestFunctionId Id = new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.tactical-ui-overview-inspect");
            public override TriggerTestFunctionId FunctionId => Id;
            public override TriggerTestFunctionCapability Capability => TriggerTestFunctionCapability.UnityAdapter;
            public TriggerTestFunctionPreview DescribePreview(ITriggerTestFunctionArguments arguments)
                => new TriggerTestFunctionPreview("检查当前战术卡界面", "真实鼠标、键盘与Game截图；不写作者源",
                    "Play模式，已打开战术卡制作页，测试世界未在自由推进");
            protected override TriggerTestOperationResult ExecuteTyped(in TriggerTestFunctionContext context, Arguments arguments)
                => TacticalUiV5Workflow.TryBegin(out string message)
                    ? TriggerTestOperationResult.Succeeded(message) : TriggerTestOperationResult.Failed(message);
        }

        private sealed class StartedAssertion : ITriggerTestAssertion
        {
            public string AssertionId => "tactical-ui-input-started";
            public TriggerTestOperationResult Evaluate(in TriggerTestAssertionContext context)
                => TacticalUiV5Workflow.Running && TacticalUiV5Workflow.HasLiveRoot
                    ? TriggerTestOperationResult.Succeeded("独立检查确认真实输入流程已启动；结果和截图仍需外部验收。")
                    : TriggerTestOperationResult.Failed("真实界面流程未启动。");
        }
    }
}
