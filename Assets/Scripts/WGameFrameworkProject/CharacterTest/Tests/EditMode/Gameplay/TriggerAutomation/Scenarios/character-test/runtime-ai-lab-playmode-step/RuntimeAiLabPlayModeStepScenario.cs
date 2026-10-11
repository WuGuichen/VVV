using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabPlayModeStepScenario :
        ITriggerTestScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-playmode-step";
        private const int FunctionCallId = 1;

        private static readonly TriggerTestDescriptor ScenarioDescriptor =
            new TriggerTestDescriptor(
                ScenarioId,
                "Runtime AI Lab advances one live PlayMode frame",
                "character-test",
                TriggerTestExecutionProfile.PlayMode,
                maxFrames: 1,
                tags: new[]
                {
                    "trigger",
                    "character-test",
                    "runtime-ai-lab",
                    "playmode",
                    "live-scene",
                    "manual",
                    "frame-step"
                });

        public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new RuntimeAiLabAdvanceLiveFrameFunction());
            builder.RegisterCall(
                FunctionCallId,
                RuntimeAiLabAdvanceLiveFrameFunction.Id,
                RuntimeAiLabAdvanceLiveFrameArguments.Instance);
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-live-frame-step",
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
                new RuntimeAiLabLiveFrameAdvancedAssertion());
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink)
        {
            ITriggerTestFixture fixture =
                TriggerTestSignalFunctionFixtureFactory.Create(
                    functionSink,
                    new[] { FunctionCallId });
            fixture.Services.Register(new RuntimeAiLabLiveFrameProbe());
            return fixture;
        }
    }

    internal sealed class RuntimeAiLabAdvanceLiveFrameArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly RuntimeAiLabAdvanceLiveFrameArguments Instance =
            new RuntimeAiLabAdvanceLiveFrameArguments();

        private RuntimeAiLabAdvanceLiveFrameArguments()
        {
        }
    }

    internal sealed class RuntimeAiLabAdvanceLiveFrameFunction :
        TriggerTestFunction<RuntimeAiLabAdvanceLiveFrameArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-advance-live-frame");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            RuntimeAiLabAdvanceLiveFrameArguments arguments)
        {
            if (!Application.isPlaying)
            {
                return TriggerTestOperationResult.Failed(
                    "Unity 未进入 Play Mode，无法驱动真实 Game View。");
            }
            if (!context.Fixture.Services.TryGet(
                    out RuntimeAiLabLiveFrameProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab PlayMode probe 不可用。");
            }
            if (!TryFindUniqueController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot before =
                controller.CaptureSnapshot();
            if (before.Phase !=
                CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab 尚未开始测试；请先在 Game View 点击开始测试。");
            }
            if (!before.Paused)
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab 正在播放；请先暂停后再执行单帧 Trigger。");
            }
            if (before.Timeline == null || before.Timeline.Count <= 0)
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab 尚未捕获可推进的时间轴帧。");
            }
            if (!before.Timeline.HasSelection ||
                before.Timeline.SelectedFrame != before.Timeline.LatestFrame)
            {
                return TriggerTestOperationResult.Failed(
                    "当前选中的是历史帧；请先定位到最新帧再执行单帧 Trigger。");
            }

            int beforeFrame = before.Timeline.LatestFrame;
            probe.Bind(controller, beforeFrame);
            if (!controller.StepFrame())
            {
                CharacterTestRuntimeAiPlannerDevSceneSnapshot failed =
                    controller.CaptureSnapshot();
                return TriggerTestOperationResult.Failed(
                    string.IsNullOrWhiteSpace(failed.StatusMessage)
                        ? "Runtime AI Lab 拒绝推进真实场景帧。"
                        : failed.StatusMessage);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot after =
                controller.CaptureSnapshot();
            int afterFrame = after.Timeline?.LatestFrame ?? beforeFrame;
            return TriggerTestOperationResult.Succeeded(
                "真实 Game View 已推进：帧 " +
                beforeFrame +
                " → " +
                afterFrame);
        }

        private static bool TryFindUniqueController(
            out CharacterTestRuntimeAiPlannerDevSceneController controller,
            out string diagnostic)
        {
            CharacterTestRuntimeAiPlannerDevSceneController[] controllers =
                Object.FindObjectsByType<
                    CharacterTestRuntimeAiPlannerDevSceneController>(
                    FindObjectsInactive.Exclude);
            controller = null;
            int liveCount = 0;
            for (int i = 0; i < controllers.Length; i++)
            {
                CharacterTestRuntimeAiPlannerDevSceneController candidate =
                    controllers[i];
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    !candidate.gameObject.scene.isLoaded)
                {
                    continue;
                }

                controller = candidate;
                liveCount++;
            }

            if (liveCount == 1)
            {
                diagnostic = string.Empty;
                return true;
            }

            controller = null;
            diagnostic = liveCount == 0
                ? "当前 Play 场景没有 Runtime AI Lab Controller。"
                : "当前 Play 场景存在多个 Runtime AI Lab Controller，无法确定目标。";
            return false;
        }
    }

    internal sealed class RuntimeAiLabLiveFrameProbe
    {
        internal CharacterTestRuntimeAiPlannerDevSceneController Controller
        {
            get;
            private set;
        }

        internal int BeforeFrame { get; private set; }
        internal int InvocationCount { get; private set; }

        internal void Bind(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int beforeFrame)
        {
            Controller = controller;
            BeforeFrame = beforeFrame;
            InvocationCount++;
        }
    }

    internal sealed class RuntimeAiLabLiveFrameAdvancedAssertion :
        ITriggerTestAssertion
    {
        public string AssertionId => "live-frame-advanced";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(
                    out RuntimeAiLabLiveFrameProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab PlayMode probe 不可用。");
            }
            if (probe.InvocationCount != 1 || probe.Controller == null)
            {
                return TriggerTestOperationResult.Failed(
                    "预期执行一次真实场景推进，实际执行 " +
                    probe.InvocationCount +
                    " 次。");
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot actual =
                probe.Controller.CaptureSnapshot();
            if (actual.Timeline == null)
            {
                return TriggerTestOperationResult.Failed(
                    "真实场景推进后时间轴不可用。");
            }

            int expectedFrame = probe.BeforeFrame + 1;
            if (actual.Timeline.LatestFrame != expectedFrame)
            {
                return TriggerTestOperationResult.Failed(
                    "真实场景帧应从 " +
                    probe.BeforeFrame +
                    " 推进到 " +
                    expectedFrame +
                    "，实际为 " +
                    actual.Timeline.LatestFrame +
                    "。");
            }
            if (!actual.Timeline.HasSelection ||
                actual.Timeline.SelectedFrame != expectedFrame)
            {
                return TriggerTestOperationResult.Failed(
                    "真实场景已推进，但时间轴游标没有同步到帧 " +
                    expectedFrame +
                    "。");
            }
            if (!actual.Paused)
            {
                return TriggerTestOperationResult.Failed(
                    "单帧 Trigger 执行后 Runtime AI Lab 未保持暂停。");
            }

            return TriggerTestOperationResult.Succeeded(
                "已验证真实场景、时间轴与游标同步到帧 " +
                expectedFrame);
        }
    }
}
