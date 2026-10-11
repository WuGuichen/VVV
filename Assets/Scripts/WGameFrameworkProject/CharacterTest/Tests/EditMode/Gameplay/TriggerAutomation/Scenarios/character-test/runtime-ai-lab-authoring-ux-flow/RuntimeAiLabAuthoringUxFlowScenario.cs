using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabAuthoringUxFlowScenario : ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-authoring-ux-flow";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Runtime AI Lab authoring UX flow",
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
                    "authoring",
                    "workspace-role",
                    "test-current-changes",
                    "publish-current-changes",
                    "production-boundary"
                },
                externalChecks: new[]
                {
                    new TriggerTestExternalCheck(
                        "runtime-ai-lab-authoring-live-command-flow",
                        TriggerTestExternalCheckKind.SceneState,
                        "通过现有 open-scene / enter-play-mode Trigger 打开正式 Game View；" +
                        "在同一 live Controller 中驱动 TestCurrentChanges 延迟队列，并复读 Agent、" +
                        "Group、Card、Timeline、Event、zoom、scroll、playhead 和 Inspector 稳定身份。"),
                    new TriggerTestExternalCheck(
                        "runtime-ai-lab-authoring-timed-autosave-and-f0",
                        TriggerTestExternalCheckKind.CustomToolEvidence,
                        "等待真实 0.5 秒 autosave 边界，确认仅保存一次；随后证明新的 authority " +
                        "session 从 F0 基线建立，并区分 authority F0 与首个可见帧。",
                        "runtime-ai-lab-authoring-ux-flow"),
                    new TriggerTestExternalCheck(
                        "runtime-ai-lab-authoring-popup-input-and-layout",
                        TriggerTestExternalCheckKind.ScreenshotReview,
                        "在 1920x1080 与 2560x1440 Game View 验证 Workspace/Publish/Navigation " +
                        "弹层、Esc、外部点击、滚轮和快捷键不穿透，且上下文栏、时间轴与 Inspector 不重叠。")
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(new RuntimeAiLabAuthoringUxFlowFunction());
            builder.RegisterCall(
                FunctionCallId,
                RuntimeAiLabAuthoringUxFlowFunction.Id,
                RuntimeAiLabAuthoringUxFlowArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-runtime-ai-lab-authoring-ux-flow",
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
                RuntimeAiLabAuthoringUxFlowAssertion.Instance);
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink)
        {
            return new RuntimeAiLabAuthoringUxFlowFixture(
                TriggerTestSignalFunctionFixtureFactory.Create(
                    functionSink,
                    new[] { FunctionCallId }));
        }
    }
}
