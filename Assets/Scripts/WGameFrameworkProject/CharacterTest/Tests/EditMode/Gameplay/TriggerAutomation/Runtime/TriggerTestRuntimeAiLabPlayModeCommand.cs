using System;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal enum TriggerTestRuntimeAiLabCommandKind
    {
        StartTest = 0,
        ResumeSimulation = 1,
        PauseSimulation = 2,
        ResetTest = 3
    }

    internal sealed class TriggerTestRuntimeAiLabCommandArguments :
        ITriggerTestFunctionArguments
    {
        internal TriggerTestRuntimeAiLabCommandArguments(
            TriggerTestRuntimeAiLabCommandKind commandKind)
        {
            CommandKind = commandKind;
        }

        internal TriggerTestRuntimeAiLabCommandKind CommandKind { get; }
    }

    /// <summary>
    /// Routes an explicit manual Trigger command through the live Runtime AI
    /// Lab controller.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This test-only Unity adapter
    /// runs only after an explicit manual request.
    /// </remarks>
    internal sealed class TriggerTestRuntimeAiLabCommandFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabCommandArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-command");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabCommandArguments arguments)
        {
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot before =
                controller.CaptureSnapshot();
            if (!TryValidatePrecondition(
                    arguments.CommandKind,
                    before,
                    out diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }

            bool succeeded;
            switch (arguments.CommandKind)
            {
                case TriggerTestRuntimeAiLabCommandKind.StartTest:
                    succeeded = controller.StartSessionThroughShell();
                    break;
                case TriggerTestRuntimeAiLabCommandKind.ResumeSimulation:
                case TriggerTestRuntimeAiLabCommandKind.PauseSimulation:
                    succeeded = controller.TogglePause();
                    break;
                case TriggerTestRuntimeAiLabCommandKind.ResetTest:
                    succeeded = controller.ResetSession();
                    break;
                default:
                    return TriggerTestOperationResult.Failed(
                        "未知的 Runtime AI Lab Trigger 命令。");
            }

            if (!succeeded)
            {
                return TriggerTestOperationResult.Failed(
                    string.IsNullOrEmpty(controller.StatusMessage)
                        ? "Runtime AI Lab 拒绝了操作。"
                        : controller.StatusMessage);
            }

            string message = GetSuccessMessage(arguments.CommandKind);
            Debug.Log(
                "[TriggerTest][RuntimeAiLab][" +
                arguments.CommandKind +
                "][PASS] " +
                message);
            return TriggerTestOperationResult.Succeeded(message);
        }

        private static bool TryValidatePrecondition(
            TriggerTestRuntimeAiLabCommandKind commandKind,
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            out string diagnostic)
        {
            switch (commandKind)
            {
                case TriggerTestRuntimeAiLabCommandKind.StartTest:
                    if (snapshot.Phase ==
                        CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                    {
                        diagnostic = string.Empty;
                        return true;
                    }

                    diagnostic = "开始测试前必须处于实验设置阶段。";
                    return false;
                case TriggerTestRuntimeAiLabCommandKind.ResumeSimulation:
                    if (snapshot.Phase !=
                        CharacterTestRuntimeAiPlannerDevScenePhase.Running)
                    {
                        diagnostic = "继续模拟前必须先开始测试。";
                        return false;
                    }
                    if (!snapshot.Paused)
                    {
                        diagnostic = "模拟已经处于运行状态。";
                        return false;
                    }

                    diagnostic = string.Empty;
                    return true;
                case TriggerTestRuntimeAiLabCommandKind.PauseSimulation:
                    if (snapshot.Phase !=
                        CharacterTestRuntimeAiPlannerDevScenePhase.Running)
                    {
                        diagnostic = "暂停模拟前必须先开始测试。";
                        return false;
                    }
                    if (snapshot.Paused)
                    {
                        diagnostic = "模拟已经处于暂停状态。";
                        return false;
                    }

                    diagnostic = string.Empty;
                    return true;
                case TriggerTestRuntimeAiLabCommandKind.ResetTest:
                    if (snapshot.Phase !=
                        CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                    {
                        diagnostic = string.Empty;
                        return true;
                    }

                    diagnostic = "重置测试前必须存在已开始或已完成的测试会话。";
                    return false;
                default:
                    diagnostic = "未知的 Runtime AI Lab Trigger 命令。";
                    return false;
            }
        }

        private static string GetSuccessMessage(
            TriggerTestRuntimeAiLabCommandKind commandKind)
        {
            switch (commandKind)
            {
                case TriggerTestRuntimeAiLabCommandKind.StartTest:
                    return "测试会话已启动，并停在首个可检查帧。";
                case TriggerTestRuntimeAiLabCommandKind.ResumeSimulation:
                    return "模拟已继续运行。";
                case TriggerTestRuntimeAiLabCommandKind.PauseSimulation:
                    return "模拟已暂停。";
                case TriggerTestRuntimeAiLabCommandKind.ResetTest:
                    return "测试已重置并返回实验设置。";
                default:
                    return "操作已完成。";
            }
        }
    }

    internal sealed class TriggerTestRuntimeAiLabCommandAssertion :
        ITriggerTestAssertion
    {
        private readonly TriggerTestRuntimeAiLabCommandKind _commandKind;

        internal TriggerTestRuntimeAiLabCommandAssertion(
            TriggerTestRuntimeAiLabCommandKind commandKind)
        {
            _commandKind = commandKind;
        }

        public string AssertionId =>
            "runtime-ai-lab-" +
            _commandKind.ToString().ToLowerInvariant() +
            "-state";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            return TriggerTestRuntimeAiLabCommandProbe.TryValidateResult(
                    _commandKind,
                    out string message)
                ? TriggerTestOperationResult.Succeeded(message)
                : TriggerTestOperationResult.Failed(message);
        }
    }

    internal static class TriggerTestRuntimeAiLabCommandProbe
    {
        internal static bool TryResolveController(
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
                ? "Play Mode 中没有活动的 Runtime AI Lab Controller。"
                : "Play Mode 中存在多个 Runtime AI Lab Controller。";
            return false;
        }

        internal static bool TryValidateResult(
            TriggerTestRuntimeAiLabCommandKind commandKind,
            out string diagnostic)
        {
            if (!TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out diagnostic))
            {
                return false;
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot =
                controller.CaptureSnapshot();
            switch (commandKind)
            {
                case TriggerTestRuntimeAiLabCommandKind.StartTest:
                    if (snapshot.Phase ==
                            CharacterTestRuntimeAiPlannerDevScenePhase.Running &&
                        snapshot.Paused &&
                        snapshot.FrameBuffer.Count > 0 &&
                        snapshot.Timeline.Count > 0)
                    {
                        diagnostic =
                            "已验证 Running、暂停状态和首个时间轴帧。";
                        return true;
                    }

                    diagnostic =
                        "测试启动后没有进入暂停的 Running 状态，或未捕获首帧。";
                    return false;
                case TriggerTestRuntimeAiLabCommandKind.ResumeSimulation:
                    if (snapshot.Phase ==
                            CharacterTestRuntimeAiPlannerDevScenePhase.Running &&
                        !snapshot.Paused)
                    {
                        diagnostic = "已验证模拟处于运行状态。";
                        return true;
                    }

                    diagnostic = "模拟没有进入运行状态。";
                    return false;
                case TriggerTestRuntimeAiLabCommandKind.PauseSimulation:
                    if (snapshot.Phase ==
                            CharacterTestRuntimeAiPlannerDevScenePhase.Running &&
                        snapshot.Paused)
                    {
                        diagnostic = "已验证模拟处于暂停状态。";
                        return true;
                    }

                    diagnostic = "模拟没有进入暂停状态。";
                    return false;
                case TriggerTestRuntimeAiLabCommandKind.ResetTest:
                    if (snapshot.Phase ==
                            CharacterTestRuntimeAiPlannerDevScenePhase.Setup &&
                        snapshot.Paused &&
                        snapshot.FrameBuffer.Count == 0 &&
                        snapshot.Timeline.Count == 0)
                    {
                        diagnostic =
                            "已验证返回 Setup，且帧缓存和时间轴均已清空。";
                        return true;
                    }

                    diagnostic =
                        "重置后未返回干净的暂停 Setup 状态。";
                    return false;
                default:
                    diagnostic = "未知的 Runtime AI Lab Trigger 命令。";
                    return false;
            }
        }
    }

    internal abstract class TriggerTestRuntimeAiLabCommandScenarioBase :
        ITriggerTestScenario
    {
        private const int FunctionCallId = 1;
        private readonly TriggerTestDescriptor _descriptor;
        private readonly TriggerTestRuntimeAiLabCommandKind _commandKind;

        protected TriggerTestRuntimeAiLabCommandScenarioBase(
            string scenarioId,
            string displayName,
            string operationTag,
            TriggerTestRuntimeAiLabCommandKind commandKind)
        {
            _commandKind = commandKind;
            _descriptor = new TriggerTestDescriptor(
                scenarioId,
                displayName,
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
                    operationTag
                });
        }

        public TriggerTestDescriptor Descriptor => _descriptor;

        public void Build(TriggerTestScenarioBuilder builder)
        {
            builder.RegisterFunction(
                new TriggerTestRuntimeAiLabCommandFunction());
            builder.RegisterCall(
                FunctionCallId,
                TriggerTestRuntimeAiLabCommandFunction.Id,
                new TriggerTestRuntimeAiLabCommandArguments(_commandKind));
            builder.UseFixture(CreateFixture);
            builder.AtFrame(
                0L,
                new TriggerTestSubmitSignalsStep(
                    "request-" +
                    _commandKind.ToString().ToLowerInvariant(),
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
                new TriggerTestRuntimeAiLabCommandAssertion(_commandKind));
        }

        private static ITriggerTestFixture CreateFixture(
            TriggerTestFunctionActionSink functionSink) =>
            TriggerTestSignalFunctionFixtureFactory.Create(
                functionSink,
                new[] { FunctionCallId });
    }
}
