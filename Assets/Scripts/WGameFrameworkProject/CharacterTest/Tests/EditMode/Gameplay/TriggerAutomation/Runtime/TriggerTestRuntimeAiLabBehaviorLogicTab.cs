using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using MxFramework.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestRuntimeAiLabBehaviorLogicTabArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly TriggerTestRuntimeAiLabBehaviorLogicTabArguments Instance =
            new TriggerTestRuntimeAiLabBehaviorLogicTabArguments();

        private TriggerTestRuntimeAiLabBehaviorLogicTabArguments()
        {
        }
    }

    /// <summary>
    /// Opens the Behavior Logic mode through its stable semantic UI command.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This test-only adapter runs only after an
    /// explicit manual Trigger request.
    /// </remarks>
    internal sealed class TriggerTestRuntimeAiLabBehaviorLogicTabFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabBehaviorLogicTabArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-open-behavior-logic-tab");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabBehaviorLogicTabArguments arguments)
        {
            if (!Application.isPlaying)
                return TriggerTestOperationResult.Failed("Unity is not in Play Mode.");
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabBehaviorLogicTabProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab Behavior Logic tab probe is unavailable.");
            }
            probe.InvocationCount++;

            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return Fail(probe, diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot before = controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabShellState shellState =
                controller.ShellCoordinator?.CurrentState;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink commandSink =
                panelHost?.CommandSink;
            if (controller.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || before.Timeline == null
                || before.Timeline.Count <= 0)
            {
                return Fail(
                    probe,
                    "Behavior Logic requires a started Runtime AI Lab test with retained history.");
            }
            if (shellState?.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                || panelHost == null
                || !panelHost.IsOpen
                || panelHost.RootForTests == null
                || commandSink == null)
            {
                return Fail(
                    probe,
                    "The started Runtime AI Lab workbench is unavailable.");
            }

            probe.SelectedAgentIndex = before.SelectedAgentIndex;
            probe.SelectedFrame = before.Timeline.HasSelection
                ? before.Timeline.SelectedFrame
                : before.Timeline.LatestFrame;
            probe.Paused = before.Paused;
            UnityEngine.Camera gameCamera = UnityEngine.Camera.main;
            probe.CameraAvailable = gameCamera != null;
            if (gameCamera != null)
            {
                probe.CameraPosition = gameCamera.transform.position;
                probe.CameraRotation = gameCamera.transform.rotation;
            }

            if (!commandSink.TrySelectCurrentRuntimeBehaviorForPresentation(out diagnostic))
                return Fail(probe, diagnostic);

            commandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .TimelineModeBehaviorLogic,
                null));
            panelHost.Refresh();
            probe.CommandSubmitted = true;
            Debug.Log(
                "[TriggerTest][RuntimeAiLabBehaviorLogicTab][PASS] " +
                "Opened TimelineModeBehaviorLogic through its semantic command.");
            return TriggerTestOperationResult.Succeeded(
                "Opened the Runtime AI Lab Behavior Logic mode.");
        }

        private static TriggerTestOperationResult Fail(
            TriggerTestRuntimeAiLabBehaviorLogicTabProbe probe,
            string diagnostic)
        {
            probe.Failure = diagnostic ?? string.Empty;
            return TriggerTestOperationResult.Failed(probe.Failure);
        }
    }

    internal sealed class TriggerTestRuntimeAiLabBehaviorLogicTabProbe
    {
        internal int InvocationCount { get; set; }
        internal int SelectedAgentIndex { get; set; }
        internal int SelectedFrame { get; set; }
        internal bool Paused { get; set; }
        internal bool CameraAvailable { get; set; }
        internal Vector3 CameraPosition { get; set; }
        internal Quaternion CameraRotation { get; set; }
        internal bool CommandSubmitted { get; set; }
        internal string Failure { get; set; } = string.Empty;
    }

    internal sealed class TriggerTestRuntimeAiLabBehaviorLogicTabAssertion :
        ITriggerTestAssertion
    {
        public string AssertionId => "runtime-ai-lab-open-behavior-logic-tab";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabBehaviorLogicTabProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab Behavior Logic tab probe is unavailable.");
            }
            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return TriggerTestOperationResult.Failed(diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot after = controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabShellState shellState =
                controller.ShellCoordinator?.CurrentState;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink commandSink =
                panelHost?.CommandSink;
            Fgui.GComponent root = panelHost?.RootForTests;
            Fgui.GButton logicTab = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeBehaviorLogic)?.asButton;
            Fgui.GComponent behaviorTimeline = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .BehaviorCardTimeline)?.asCom;
            Fgui.GComponent runtimeWorkspace = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .RuntimeFrameWorkspace)?.asCom;
            UnityEngine.Camera gameCamera = UnityEngine.Camera.main;

            bool stableCamera = probe.CameraAvailable == (gameCamera != null)
                && (!probe.CameraAvailable
                    || (gameCamera.transform.position == probe.CameraPosition
                        && gameCamera.transform.rotation == probe.CameraRotation));
            int selectedFrame = after.Timeline != null && after.Timeline.HasSelection
                ? after.Timeline.SelectedFrame
                : after.Timeline?.LatestFrame ?? -1;
            bool passed = probe.InvocationCount == 1
                && probe.CommandSubmitted
                && controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && shellState?.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                && shellState.PageContext.TimelineMode ==
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic
                && commandSink?.TimelineMode ==
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic
                && logicTab?.selected == true
                && runtimeWorkspace?.visible == true
                && behaviorTimeline?.visible == true
                && after.SelectedAgentIndex == probe.SelectedAgentIndex
                && selectedFrame == probe.SelectedFrame
                && after.Paused == probe.Paused
                && stableCamera;
            return passed
                ? TriggerTestOperationResult.Succeeded(
                    "Verified the started test remains in TimelineModeBehaviorLogic with stable runtime context.")
                : TriggerTestOperationResult.Failed(
                    string.IsNullOrEmpty(probe.Failure)
                        ? "The started test did not remain in TimelineModeBehaviorLogic."
                        : probe.Failure);
        }
    }
}
