using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using MxFramework.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestRuntimeAiLabTimelineModeArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly TriggerTestRuntimeAiLabTimelineModeArguments Instance =
            new TriggerTestRuntimeAiLabTimelineModeArguments();

        private TriggerTestRuntimeAiLabTimelineModeArguments()
        {
        }
    }

    /// <summary>
    /// Switches the persistent Runtime Test Workbench timeline host through the
    /// real FairyGUI tab click listeners and the legacy route aliases.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This test-only adapter runs only
    /// after an explicit manual Trigger request.
    /// </remarks>
    internal sealed class TriggerTestRuntimeAiLabTimelineModeFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabTimelineModeArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-switch-timeline-mode");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabTimelineModeArguments arguments)
        {
            if (!Application.isPlaying)
                return TriggerTestOperationResult.Failed("Unity is not in Play Mode.");
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabTimelineModeProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab timeline-mode probe is unavailable.");
            }
            probe.InvocationCount++;

            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
            {
                return Fail(probe, diagnostic);
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot before =
                controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                controller.ShellCoordinator;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink commandSink =
                panelHost?.CommandSink;
            if (before.Timeline == null || before.Timeline.Count <= 0)
            {
                return Fail(
                    probe,
                    "Timeline mode switching requires retained RuntimeFrame history.");
            }
            if (coordinator?.CurrentState?.ActiveWorkspace
                    != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                || panelHost == null
                || !panelHost.IsOpen
                || panelHost.RootForTests == null
                || commandSink == null)
            {
                return Fail(
                    probe,
                    "The persistent Runtime Test Workbench root is unavailable.");
            }

            // The Plan snapshot is intentionally identity-gated. Align the
            // presentation selection with the active typed behavior before
            // asserting the latest-frame snapshot instead of weakening that
            // production rule for Trigger automation.
            probe.RuntimeIdentityMatched =
                commandSink.TrySelectCurrentRuntimeBehaviorForPresentation(
                    out diagnostic);
            panelHost.Refresh();

            Fgui.GComponent root = panelHost.RootForTests;
            UnityEngine.Camera gameCamera = UnityEngine.Camera.main;
            probe.SelectedAgentIndex = before.SelectedAgentIndex;
            probe.SelectedFrame = before.Timeline != null
                && before.Timeline.HasSelection
                    ? before.Timeline.SelectedFrame
                    : before.Timeline?.LatestFrame ?? -1;
            probe.Paused = before.Paused;
            probe.CameraAvailable = gameCamera != null;
            if (gameCamera != null)
            {
                probe.CameraPosition = gameCamera.transform.position;
                probe.CameraRotation = gameCamera.transform.rotation;
            }

            if (!ClickTimelineModeButton(
                    root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeBehaviorPlan,
                    out diagnostic))
            {
                return Fail(probe, diagnostic);
            }
            panelHost.Refresh();
            probe.ExplicitPlanPassed = VerifyModeAndStableContext(
                controller,
                coordinator,
                panelHost,
                root,
                probe,
                CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                out diagnostic);
            if (!probe.ExplicitPlanPassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            if (!ClickTimelineModeButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .TimelineModeBehaviorLogic,
                    out diagnostic))
            {
                return Fail(probe, diagnostic);
            }
            panelHost.Refresh();
            probe.ExplicitLogicPassed = VerifyModeAndStableContext(
                controller,
                coordinator,
                panelHost,
                root,
                probe,
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                out diagnostic);
            if (!probe.ExplicitLogicPassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            if (!ClickTimelineModeButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .TimelineModeRuntimeTrace,
                    out diagnostic))
            {
                return Fail(probe, diagnostic);
            }
            panelHost.Refresh();
            probe.ExplicitRuntimePassed = VerifyModeAndStableContext(
                controller,
                coordinator,
                panelHost,
                root,
                probe,
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                out diagnostic);
            if (!probe.ExplicitRuntimePassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            Enqueue(
                commandSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .BehaviorCardTab);
            panelHost.Refresh();
            probe.BehaviorAliasPassed = VerifyModeAndStableContext(
                controller,
                coordinator,
                panelHost,
                root,
                probe,
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                out diagnostic);
            if (!probe.BehaviorAliasPassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            Enqueue(
                commandSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .DiagnosticsRuntimeFrameTab);
            panelHost.Refresh();
            probe.RuntimeAliasPassed = VerifyModeAndStableContext(
                controller,
                coordinator,
                panelHost,
                root,
                probe,
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                out diagnostic);
            if (!probe.RuntimeAliasPassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            probe.SessionControlsPassed = VerifySessionControls(
                controller,
                panelHost,
                root,
                out diagnostic);
            if (!probe.SessionControlsPassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            probe.RestartFromHistoricalPassed = VerifyRestartFromHistoricalFrame(
                controller,
                coordinator,
                panelHost,
                root,
                out diagnostic);
            if (!probe.RestartFromHistoricalPassed)
                return Fail(probe, diagnostic + " " + commandSink.LastStatusMessage);

            probe.Completed = true;
            const string success =
                "Unified timeline host used the real Runtime, Plan, and Logic tab click listeners, preserved runtime/camera context, and verified pause, single-frame, resume, history, latest-frame, and restart-from-history controls.";
            Debug.Log("[TriggerTest][RuntimeAiLabTimelineMode][PASS] " + success);
            return TriggerTestOperationResult.Succeeded(success);
        }

        private static bool VerifyModeAndStableContext(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            Fgui.GComponent expectedRoot,
            TriggerTestRuntimeAiLabTimelineModeProbe probe,
            CharacterTestRuntimeAiLabTimelineMode expectedMode,
            out string diagnostic)
        {
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot =
                controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabShellState shell = coordinator.CurrentState;
            int selectedFrame = snapshot.Timeline != null
                && snapshot.Timeline.HasSelection
                    ? snapshot.Timeline.SelectedFrame
                    : snapshot.Timeline?.LatestFrame ?? -1;
            if (shell.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                || shell.PageContext.TimelineMode != expectedMode)
            {
                diagnostic = "The shell did not retain RuntimeFrame authority in the requested mode.";
                return false;
            }
            if (!ReferenceEquals(panelHost.RootForTests, expectedRoot))
            {
                diagnostic = "The FairyGUI Runtime Test Workbench root was replaced.";
                return false;
            }
            if (!VerifyTimelineModeGeometry(
                    panelHost,
                    expectedRoot,
                    expectedMode,
                    out diagnostic))
            {
                return false;
            }
            if (expectedMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan)
            {
                Fgui.GComponent behaviorTimeline = expectedRoot.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .BehaviorCardTimeline)?.asCom;
                Fgui.GComponent planTrackLayer = behaviorTimeline?.GetChild(
                    "runtimeGameplayPlanLayer")?.asCom;
                bool tracksValid = planTrackLayer != null
                    && VerifyPlanTrackLabels(planTrackLayer);
                bool labelsValid = planTrackLayer != null
                    && VerifyChinesePrimaryPlanLabels(planTrackLayer);
                bool progressValid = planTrackLayer != null
                    && VerifyGameplayPlanConnectionsAndSnapshot(
                        planTrackLayer,
                        allowExplicitIdentityEmptyState: !probe.RuntimeIdentityMatched);
                bool actionAuthorityValid = VerifyBehaviorActionAuthority(panelHost.LastViewModel);
                if (behaviorTimeline == null
                    || !behaviorTimeline.visible
                    || planTrackLayer == null
                    || !planTrackLayer.visible
                    || !tracksValid
                    || !labelsValid
                    || !progressValid
                    || !actionAuthorityValid)
                {
                    diagnostic =
                        "Behavior Plan surface verification failed: timeline="
                        + (behaviorTimeline != null && behaviorTimeline.visible)
                        + ", layer="
                        + (planTrackLayer != null && planTrackLayer.visible)
                        + ", tracks="
                        + tracksValid
                        + ", labels="
                        + labelsValid
                        + ", progress="
                        + progressValid
                        + ", actionAuthority="
                        + actionAuthorityValid
                        + ".";
                    return false;
                }
            }
            if (snapshot.SelectedAgentIndex != probe.SelectedAgentIndex
                || selectedFrame != probe.SelectedFrame
                || snapshot.Paused != probe.Paused)
            {
                diagnostic = "Agent, frame, or playback context changed during the mode switch.";
                return false;
            }
            UnityEngine.Camera gameCamera = UnityEngine.Camera.main;
            if (probe.CameraAvailable
                && (gameCamera == null
                    || Vector3.Distance(
                            gameCamera.transform.position,
                            probe.CameraPosition)
                        > 0.001f
                    || Quaternion.Angle(
                            gameCamera.transform.rotation,
                            probe.CameraRotation)
                        > 0.01f))
            {
                diagnostic = "The game camera changed during the mode switch.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool VerifyRestartFromHistoricalFrame(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            Fgui.GComponent root,
            out string diagnostic)
        {
            CharacterTestRuntimeAiPlannerDevSceneSnapshot before =
                controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabTimelineMode timelineModeBeforeRestart =
                coordinator.CurrentState.PageContext.TimelineMode;
            if (before.Timeline == null || before.Timeline.Count < 2)
            {
                diagnostic = "Restart verification requires at least two retained RuntimeFrames.";
                return false;
            }

            Fgui.GComponent timeline = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .RuntimeFrameWorkspace)?.asCom;
            if (before.Timeline.SelectedFrame == before.Timeline.LatestFrame)
            {
                if (!ClickEnabledButton(timeline, "btnStepBack", out diagnostic))
                    return false;
                panelHost.Refresh();
                before = controller.CaptureSnapshot();
            }
            if (before.Timeline == null
                || !before.Timeline.HasSelection
                || before.Timeline.SelectedFrame >= before.Timeline.LatestFrame)
            {
                diagnostic = "The Trigger could not establish a historical frame before restart.";
                return false;
            }

            if (!ClickEnabledButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                    out diagnostic))
            {
                return false;
            }
            panelHost.Refresh();

            CharacterTestRuntimeAiPlannerDevSceneSnapshot restarted =
                controller.CaptureSnapshot();
            bool passed = panelHost.RootForTests != null
                && coordinator.CurrentState.ActiveWorkspace
                    == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                && coordinator.CurrentState.PageContext.TimelineMode
                    == timelineModeBeforeRestart
                && panelHost.CommandSink.TimelineMode == timelineModeBeforeRestart
                && restarted.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && restarted.Paused
                && restarted.Timeline != null
                && restarted.Timeline.Count > 0
                && restarted.Timeline.HasSelection
                && restarted.Timeline.SelectedFrame == restarted.Timeline.LatestFrame
                && panelHost.CommandSink.LastStatusMessage.IndexOf(
                    "测试已重新开始",
                    System.StringComparison.Ordinal) >= 0;
            diagnostic = passed
                ? string.Empty
                : "The right-side restart command did not preserve the selected tab while restarting a historical-frame session.";
            return passed;
        }

        private static bool VerifyTimelineModeGeometry(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabTimelineMode expectedMode,
            out string diagnostic)
        {
            Fgui.GComponent runtimeTimeline = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .RuntimeFrameWorkspace)?.asCom;
            Fgui.GComponent behaviorTimeline = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .BehaviorCardTimeline)?.asCom;
            Fgui.GObject modeHost = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeHost);
            Fgui.GComponent details = runtimeTimeline?.GetChild(
                "timelineEventDetails")?.asCom;
            Fgui.GButton runtimeModeButton = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeRuntimeTrace)?.asButton;
            Fgui.GButton behaviorModeButton = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeBehaviorPlan)?.asButton;
            Fgui.GButton logicModeButton = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeBehaviorLogic)?.asButton;
            if (runtimeTimeline == null
                || behaviorTimeline == null
                || modeHost == null
                || details == null
                || runtimeModeButton == null
                || behaviorModeButton == null
                || logicModeButton == null
                || !runtimeTimeline.visible
                || !details.visible
                || Mathf.Abs(runtimeTimeline.x) > 0.01f
                || Mathf.Abs(runtimeTimeline.y) > 0.01f
                || Mathf.Abs(runtimeTimeline.width - 1920f) > 0.01f
                || Mathf.Abs(runtimeTimeline.height - 1080f) > 0.01f
                || !VerifyRuntimeSessionBar(root, runtimeTimeline, details))
            {
                diagnostic =
                    "The permanent RuntimeFrame decision surface did not retain its full-workbench geometry.";
                return false;
            }
            if (!runtimeModeButton.visible
                || !behaviorModeButton.visible
                || !logicModeButton.visible
                || Mathf.Abs(runtimeModeButton.y - behaviorModeButton.y) > 0.01f
                || Mathf.Abs(behaviorModeButton.y - logicModeButton.y) > 0.01f
                || Mathf.Abs(runtimeModeButton.width - 148f) > 0.01f
                || Mathf.Abs(behaviorModeButton.width - 148f) > 0.01f
                || Mathf.Abs(logicModeButton.width - 148f) > 0.01f
                || Mathf.Abs(runtimeModeButton.height - 52f) > 0.01f
                || Mathf.Abs(behaviorModeButton.height - 52f) > 0.01f
                || Mathf.Abs(logicModeButton.height - 52f) > 0.01f
                || runtimeModeButton.titleFontSize != 24
                || behaviorModeButton.titleFontSize != 24
                || logicModeButton.titleFontSize != 24)
            {
                diagnostic =
                    "Timeline mode tabs did not retain one shared position, size, and typography.";
                return false;
            }

            if (expectedMode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)
            {
                Fgui.GComponent viewport = runtimeTimeline.GetChild(
                    "runtimeFrameFiveTrackViewport")?.asCom;
                if (viewport == null
                    || !viewport.visible
                    || behaviorTimeline.visible
                    || Mathf.Abs(viewport.y - 740f) > 0.01f
                    || Mathf.Abs(viewport.height - 340f) > 0.01f
                    || Mathf.Abs(details.height - 668f) > 0.01f
                    || Mathf.Abs(modeHost.y - 668f) > 0.01f
                    || Mathf.Abs(modeHost.height - 72f) > 0.01f)
                {
                    diagnostic =
                        "Runtime Trace did not use the shared timeline workbench shell and full Current Decision column.";
                    return false;
                }
            }
            else if (expectedMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic)
            {
                Fgui.GComponent library = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .BehaviorCardLibrary)?.asCom;
                Fgui.GComponent planTrackLayer = behaviorTimeline.GetChild(
                    "runtimeGameplayPlanLayer")?.asCom;
                Fgui.GTextField logicTitle = behaviorTimeline.GetChild(
                    "timelineTitle")?.asTextField;
                Fgui.GTextField decisionTitle = details.GetChild(
                    "detailTitle")?.asTextField;
                Fgui.GObject logicInspector = details.GetChild(
                    "planActionBackground");
                Fgui.GTextField selectedIdentity = details.GetChild(
                    "detailEntities")?.asTextField;
                if (!behaviorTimeline.visible
                    || library?.visible == true
                    || planTrackLayer?.visible == true
                    || Mathf.Abs(behaviorTimeline.x) > 0.01f
                    || Mathf.Abs(behaviorTimeline.y - 668f) > 0.01f
                    || Mathf.Abs(behaviorTimeline.width - 1920f) > 0.01f
                    || Mathf.Abs(behaviorTimeline.height - 412f) > 0.01f
                    || behaviorTimeline.GetChild("rulerBackground")?.visible == true
                    || behaviorTimeline.GetChild("timelineRuntimeStatus")?.visible == true
                    || behaviorTimeline.GetChild("timelineHorizontalScrollTrack")?.visible == true
                    || behaviorTimeline.GetChild("timelineVerticalScrollTrack")?.visible == true
                    || logicTitle?.visible == true
                    || decisionTitle?.text != "编辑逻辑"
                    || logicInspector?.visible != true
                    || selectedIdentity?.visible != true
                    || details.GetChild("memberRow1")?.visible == true
                    || !logicModeButton.selected)
                {
                    diagnostic =
                        "Behavior Logic did not use the shared stage-flow viewport and selected-element inspector.";
                    return false;
                }
            }
            else
            {
                Fgui.GComponent library = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .BehaviorCardLibrary)?.asCom;
                Fgui.GObject horizontalOverview = behaviorTimeline.GetChild(
                    "timelineHorizontalScrollTrack");
                Fgui.GObject verticalOverview = behaviorTimeline.GetChild(
                    "timelineVerticalScrollTrack");
                Fgui.GButton planModeButton = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .TimelineModeBehaviorPlan)?.asButton;
                Fgui.GTextField planTitle = behaviorTimeline.GetChild(
                    "timelineTitle")?.asTextField;
                Fgui.GTextField decisionTitle = details.GetChild(
                    "detailTitle")?.asTextField;
                Fgui.GObject planInspector = details.GetChild(
                    "planActionBackground");
                Fgui.GComponent firstCandidate = details.GetChild(
                    "behaviorPlanCandidateRow0")?.asCom;
                if (!behaviorTimeline.visible
                    || library?.visible == true
                    || Mathf.Abs(behaviorTimeline.x) > 0.01f
                    || Mathf.Abs(behaviorTimeline.y - 668f) > 0.01f
                    || Mathf.Abs(behaviorTimeline.width - 1920f) > 0.01f
                    || Mathf.Abs(behaviorTimeline.height - 412f) > 0.01f
                    || Mathf.Abs(details.height - 668f) > 0.01f
                    || Mathf.Abs((decisionTitle?.y ?? 0f) - 64f) > 0.01f
                    || Mathf.Abs((details.GetChild("causalCard1")?.y ?? 0f) - 116f) > 0.01f
                    || Mathf.Abs(modeHost.y - 668f) > 0.01f
                    || Mathf.Abs(modeHost.height - 72f) > 0.01f
                    || planModeButton == null
                    || Mathf.Abs(planModeButton.width - 148f) > 0.01f
                    || Mathf.Abs(planModeButton.height - 52f) > 0.01f
                    || planModeButton.titleFontSize != 24
                    || planTitle?.textFormat.size != 28
                    || decisionTitle?.textFormat.size != 36
                    || decisionTitle.text != "下一行为候选"
                    || planInspector?.visible == true
                    || details.GetChild("detailEntities")?.visible == true
                    || firstCandidate?.visible != true
                    || string.IsNullOrWhiteSpace(
                        firstCandidate.GetChildAt(2)?.asTextField?.text)
                    || details.GetChild("memberRow5")?.visible == true
                    || details.GetChild("causalCard1")?.visible == true
                    || details.GetChild("memberRow1")?.visible == true
                    || behaviorTimeline.GetChild("timelineRuntimeStatus")?.visible == true
                    || horizontalOverview?.visible == true
                    || verticalOverview?.visible == true)
                {
                    diagnostic =
                        "Behavior Plan did not show the real next-behavior candidate pool in the shared decision column.";
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool VerifyRuntimeSessionBar(
            Fgui.GComponent root,
            Fgui.GComponent runtimeTimeline,
            Fgui.GComponent details)
        {
            Fgui.GObject topBar = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar);
            Fgui.GObject slotBadge = root.GetChild("runtimeFrameSlotBadge");
            Fgui.GTextField title = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title)?.asTextField;
            Fgui.GTextField status = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary)?.asTextField;
            Fgui.GButton pause = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume)?.asButton;
            Fgui.GButton step = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step)?.asButton;
            Fgui.GButton reset = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset)?.asButton;
            Fgui.GButton back = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close)?.asButton;
            Fgui.GButton duplicatePlayPause = runtimeTimeline.GetChild(
                "btnTimelinePlayPause")?.asButton;
            return topBar != null
                && Mathf.Abs(topBar.x - 1400f) <= 0.01f
                && Mathf.Abs(topBar.y) <= 0.01f
                && Mathf.Abs(topBar.width - 520f) <= 0.01f
                && Mathf.Abs(topBar.height - 60f) <= 0.01f
                && slotBadge != null
                && Mathf.Abs(slotBadge.x - 1416f) <= 0.01f
                && Mathf.Abs(slotBadge.y - 10f) <= 0.01f
                && Mathf.Abs(slotBadge.width - 72f) <= 0.01f
                && Mathf.Abs(slotBadge.height - 40f) <= 0.01f
                && title != null
                && Mathf.Abs(title.x - 1500f) <= 0.01f
                && Mathf.Abs(title.y - 6f) <= 0.01f
                && Mathf.Abs(title.width - 180f) <= 0.01f
                && Mathf.Abs(title.height - 26f) <= 0.01f
                && title.textFormat.size == 22
                && status != null
                && Mathf.Abs(status.x - 1500f) <= 0.01f
                && Mathf.Abs(status.y - 32f) <= 0.01f
                && Mathf.Abs(status.width - 180f) <= 0.01f
                && Mathf.Abs(status.height - 20f) <= 0.01f
                && status.textFormat.size == 16
                && pause != null
                && step != null
                && reset != null
                && back != null
                && VerifySessionButtonGeometry(pause, 1688f)
                && VerifySessionButtonGeometry(step, 1744f)
                && VerifySessionButtonGeometry(reset, 1800f)
                && VerifySessionButtonGeometry(back, 1856f)
                && duplicatePlayPause != null
                && !duplicatePlayPause.visible
                && !duplicatePlayPause.touchable
                && !duplicatePlayPause.enabled
                && Mathf.Abs(details.height - 668f) <= 0.01f
                && (details.GetChild("detailTitle")?.y ?? 0f) >= 60f;
        }

        private static bool VerifySessionButtonGeometry(Fgui.GButton button, float x)
        {
            return Mathf.Abs(button.x - x) <= 0.01f
                && Mathf.Abs(button.y - 10f) <= 0.01f
                && Mathf.Abs(button.width - 48f) <= 0.01f
                && Mathf.Abs(button.height - 40f) <= 0.01f;
        }

        private static bool VerifySessionControls(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            Fgui.GComponent root,
            out string diagnostic)
        {
            CharacterTestRuntimeAiPlannerDevSceneSnapshot initial =
                controller.CaptureSnapshot();
            if (initial.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
            {
                Fgui.GButton pause = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume)?.asButton;
                Fgui.GButton step = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step)?.asButton;
                bool correctlyDisabled = pause != null
                    && step != null
                    && !pause.touchable
                    && !step.touchable;
                diagnostic = correctlyDisabled
                    ? string.Empty
                    : "Completed sessions must retain disabled session controls while the timeline tabs remain usable.";
                return correctlyDisabled;
            }

            bool initiallyPaused = initial.Paused;
            if (!ClickEnabledButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                    out diagnostic))
            {
                return false;
            }
            panelHost.Refresh();
            if (controller.CaptureSnapshot().Paused == initiallyPaused)
            {
                diagnostic = "The right-side pause/resume control did not toggle playback.";
                return false;
            }

            if (!ClickEnabledButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                    out diagnostic))
            {
                return false;
            }
            panelHost.Refresh();
            if (controller.CaptureSnapshot().Paused != initiallyPaused)
            {
                diagnostic = "The right-side pause/resume control did not restore playback.";
                return false;
            }

            if (!initiallyPaused)
            {
                if (!ClickEnabledButton(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                        out diagnostic))
                {
                    return false;
                }
                panelHost.Refresh();
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot paused =
                controller.CaptureSnapshot();
            if (!paused.Paused || paused.Timeline == null)
            {
                diagnostic = "The Trigger could not establish a paused timeline for single-frame verification.";
                return false;
            }
            if (!paused.Timeline.HasSelection
                || paused.Timeline.SelectedFrame != paused.Timeline.LatestFrame)
            {
                Fgui.GComponent runtimeTimeline = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .RuntimeFrameWorkspace)?.asCom;
                if (!ClickEnabledButton(runtimeTimeline, "btnLatestFrame", out diagnostic))
                    return false;
                panelHost.Refresh();
                paused = controller.CaptureSnapshot();
            }

            int latestBeforeStep = paused.Timeline.LatestFrame;
            if (!ClickEnabledButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step,
                    out diagnostic))
            {
                return false;
            }
            panelHost.Refresh();
            CharacterTestRuntimeAiPlannerDevSceneSnapshot stepped =
                controller.CaptureSnapshot();
            if (!stepped.Paused
                || stepped.Timeline == null
                || stepped.Timeline.LatestFrame != latestBeforeStep + 1
                || stepped.Timeline.SelectedFrame != stepped.Timeline.LatestFrame)
            {
                diagnostic = "The right-side single-frame control did not append and select exactly one RuntimeFrame.";
                return false;
            }

            Fgui.GComponent timeline = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .RuntimeFrameWorkspace)?.asCom;
            if (!ClickEnabledButton(timeline, "btnStepBack", out diagnostic))
                return false;
            panelHost.Refresh();
            CharacterTestRuntimeAiPlannerDevSceneSnapshot historical =
                controller.CaptureSnapshot();
            if (historical.Timeline == null
                || historical.Timeline.SelectedFrame
                    != historical.Timeline.LatestFrame - 1)
            {
                diagnostic = "The Runtime Trace history control did not select the previous frame.";
                return false;
            }

            if (!ClickEnabledButton(timeline, "btnLatestFrame", out diagnostic))
                return false;
            panelHost.Refresh();
            CharacterTestRuntimeAiPlannerDevSceneSnapshot latest =
                controller.CaptureSnapshot();
            if (latest.Timeline == null
                || latest.Timeline.SelectedFrame != latest.Timeline.LatestFrame)
            {
                diagnostic = "The Runtime Trace latest-frame control did not restore the latest selection.";
                return false;
            }

            if (!initiallyPaused)
            {
                if (!ClickEnabledButton(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                        out diagnostic))
                {
                    return false;
                }
                panelHost.Refresh();
            }
            if (controller.CaptureSnapshot().Paused != initiallyPaused)
            {
                diagnostic = "The session control verification did not restore the original playback state.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool VerifyPlanTrackLabels(Fgui.GComponent layer)
        {
            for (int i = 0; i < 2; i++)
            {
                Fgui.GTextField label = layer.GetChild("planTrackLabel" + i)?.asTextField;
                if (label == null
                    || label.visible)
                    return false;
            }
            if (layer.GetChild("planTrackLabel2") != null)
                return false;
            Fgui.GTextField ruler = layer.GetChild("planRulerLabel0")?.asTextField;
            Fgui.GObject rulerBackground = layer.GetChild("planRulerBackground");
            Fgui.GObject rulerTick = layer.GetChild("planRulerTick0");
            Fgui.GObject firstTrack = layer.GetChild("planTrackBackground0");
            Fgui.GObject actionTrack = layer.GetChild("planTrackBackground1");
            return ruler != null
                && ruler.textFormat.size == 24
                && !ruler.visible
                && rulerBackground?.visible == false
                && rulerTick?.visible == false
                && firstTrack != null
                && Mathf.Abs(firstTrack.y - 88f) <= 0.01f
                && Mathf.Abs(firstTrack.height - 64f) <= 0.01f
                && actionTrack != null
                && Mathf.Abs(actionTrack.y - 160f) <= 0.01f
                && Mathf.Abs(actionTrack.height - 72f) <= 0.01f
                && layer.GetChild("planTrackBackground2") == null;
        }

        private static bool VerifyBehaviorActionAuthority(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel?.BehaviorCardWorkspace?.Timeline;
            int stageCount = Mathf.Min(timeline?.StageCount ?? 0, timeline?.Stages?.Length ?? 0);
            if (timeline == null || !timeline.Available || stageCount <= 0)
                return false;

            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[i];
                if (stage == null
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
                {
                    return false;
                }
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    && string.IsNullOrWhiteSpace((stage.CharacterActionId).ToString()))
                {
                    return false;
                }
            }

            CharacterTestRuntimeAiLabPrimaryActionIdentity primary =
                viewModel.BehaviorCardIdentity?.Runtime?.PrimaryAction;
            if (primary?.Available != true
                || primary.StageKind != CharacterTestRuntimeAiLabCardStepKind.CharacterAction)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace((primary.CharacterActionId).ToString()))
                return false;
            if (!primary.ActionObserved)
                return primary.CommandPending || primary.CommandRejected;

            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[i];
                if (stage?.Current == true)
                {
                    return stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                        && stage.CharacterActionId == primary.CharacterActionId;
                }
            }
            return false;
        }

        private static bool VerifyChinesePrimaryPlanLabels(Fgui.GComponent layer)
        {
            Fgui.GObject stageObject = layer.GetChild("planStage0");
            bool stage = stageObject?.visible != true || HasChineseGameplayText(
                FindFirstVisibleText(stageObject.asCom));
            bool action = HasChineseGameplayText(
                FindFirstVisibleText(layer.GetChild("planAction0")?.asCom));
            bool successExit = false;
            bool failureExit = false;
            bool conditionExit = false;
            bool rawEnglishVisible = false;
            InspectChinesePrimaryPlanLabels(
                layer,
                ref stage,
                ref action,
                ref successExit,
                ref failureExit,
                ref conditionExit,
                ref rawEnglishVisible);
            return stage
                && action
                && !rawEnglishVisible;
        }

        private static string FindFirstVisibleText(Fgui.GComponent root)
        {
            if (root == null)
                return string.Empty;
            for (int index = 0; index < root.numChildren; index++)
            {
                Fgui.GObject child = root.GetChildAt(index);
                Fgui.GTextField text = child?.asTextField;
                if (child?.visible == true && !string.IsNullOrWhiteSpace(text?.text))
                    return text.text;
            }
            return string.Empty;
        }

        private static bool HasChineseGameplayText(string text)
        {
            for (int index = 0; !string.IsNullOrEmpty(text) && index < text.Length; index++)
            {
                char value = text[index];
                if (value >= '\u3400' && value <= '\u9fff')
                    return true;
            }
            return false;
        }

        private static void InspectChinesePrimaryPlanLabels(
            Fgui.GComponent root,
            ref bool stage,
            ref bool action,
            ref bool successExit,
            ref bool failureExit,
            ref bool conditionExit,
            ref bool rawEnglishVisible)
        {
            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GObject child = root.GetChildAt(i);
                Fgui.GTextField textField = child.asTextField;
                string text = textField?.text ?? string.Empty;
                string exitText = text.StartsWith("监听 · ", System.StringComparison.Ordinal)
                    ? text.Substring(5)
                    : text.StartsWith("触发 · ", System.StringComparison.Ordinal)
                        ? text.Substring(5)
                        : text;
                successExit |= exitText.StartsWith("成功 →", System.StringComparison.Ordinal);
                failureExit |= exitText.StartsWith("失败 →", System.StringComparison.Ordinal);
                conditionExit |= exitText.IndexOf(" → ", System.StringComparison.Ordinal) >= 0
                    && !exitText.StartsWith("成功 →", System.StringComparison.Ordinal)
                    && !exitText.StartsWith("失败 →", System.StringComparison.Ordinal);
                rawEnglishVisible |= child.visible && (text == "Targeting.NearestEnemy"
                    || text == "Complete"
                    || text == "Failed"
                    || text == "下一帧继续评估"
                    || text == "执行中"
                    || text == "卡级检测"
                    || text == "阶段检测"
                    || text == "事件脉搏");
                Fgui.GComponent component = child.asCom;
                if (component != null)
                {
                    InspectChinesePrimaryPlanLabels(
                        component,
                        ref stage,
                        ref action,
                        ref successExit,
                        ref failureExit,
                        ref conditionExit,
                        ref rawEnglishVisible);
                }
            }
        }

        private static bool VerifyGameplayPlanConnectionsAndSnapshot(
            Fgui.GComponent layer,
            bool allowExplicitIdentityEmptyState)
        {
            Fgui.GObject stage = layer.GetChild("planStage0");
            Fgui.GObject action = layer.GetChild("planAction0");
            Fgui.GObject decision = layer.GetChild("planBehaviorNodeBackground2");
            Fgui.GObject successExit = layer.GetChild("planBehaviorSuccessExit");
            Fgui.GObject failureExit = layer.GetChild("planBehaviorFailureExit");
            Fgui.GObject candidate = layer.GetChild("planBehaviorCandidate0");
            Fgui.GTextField executionStatus = layer.GetChild("planExecutionStatus")?.asTextField;
            Fgui.GTextField summaryPrefix = layer.GetChild("planSummaryPrefix")?.asTextField;
            Fgui.GTextField summaryCurrent = layer.GetChild("planSummaryCurrent")?.asTextField;
            Fgui.GTextField summaryFrame = layer.GetChild("planSummaryFrame")?.asTextField;
            Fgui.GTextField summaryNext = layer.GetChild("planSummaryNext")?.asTextField;
            bool blockLocalRuntimeState = HasVisibleBlockLocalRuntimeState(layer);
            bool compactDistanceMetric = HasVisibleCompactDistanceMetric(action?.asCom);
            bool compactExitVisible = HasVisibleCompactBehaviorExit(layer);
            bool snapshotVisible = executionStatus?.visible == false
                && summaryPrefix?.visible == false
                && summaryCurrent?.visible == false
                && summaryFrame?.visible == false
                && summaryNext?.visible == false
                && (blockLocalRuntimeState || compactDistanceMetric)
                && compactExitVisible
                && layer.GetChild("planPlayhead") == null
                && layer.GetChild("planExecutionOverrun") == null;
            bool explicitEmptyStateVisible = allowExplicitIdentityEmptyState
                && executionStatus?.visible == false
                && executionStatus.text == "未执行"
                && summaryPrefix?.visible == false
                && summaryCurrent?.visible == false
                && summaryFrame?.visible == false
                && summaryNext?.visible == false
                && !blockLocalRuntimeState
                && layer.GetChild("planPlayhead") == null
                && layer.GetChild("planExecutionOverrun") == null
                && layer.GetChild("planSnapshotProgress") == null
                && layer.GetChild("planSnapshotStartDot") == null
                && layer.GetChild("planSnapshotHeadDot") == null
                && layer.GetChild("planSessionActivityTrack") == null
                && layer.GetChild("planSessionActivityDot") == null;
            return stage?.visible != true
                && action?.visible == true
                && decision?.visible != true
                && successExit?.visible != true
                && failureExit?.visible != true
                && candidate?.visible != true
                && (snapshotVisible || explicitEmptyStateVisible);
        }

        private static bool HasVisibleCompactBehaviorExit(Fgui.GComponent layer)
        {
            for (int index = 0; index < layer.numChildren; index++)
            {
                Fgui.GObject child = layer.GetChildAt(index);
                if (child?.visible != true
                    || !child.name.StartsWith("planExit", System.StringComparison.Ordinal))
                {
                    continue;
                }
                Fgui.GTextField label = child.asCom?.GetChildAt(3)?.asTextField;
                if (label != null
                    && label.text.IndexOf("→", System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasVisibleCompactDistanceMetric(Fgui.GComponent action)
        {
            Fgui.GTextField current = action?.GetChild("planStateBadge")?.asTextField;
            Fgui.GTextField target = action?.GetChild("planMetricTargetBadge")?.asTextField;
            return current?.visible == true
                && current.text.StartsWith("距离 ", System.StringComparison.Ordinal)
                && current.text.EndsWith("m", System.StringComparison.Ordinal)
                && target?.visible == true
                && target.text.StartsWith("/ ≤", System.StringComparison.Ordinal)
                && target.text.EndsWith("m", System.StringComparison.Ordinal);
        }

        private static bool HasVisibleBlockLocalRuntimeState(Fgui.GComponent layer)
        {
            for (int i = 0; i < layer.numChildren; i++)
            {
                Fgui.GObject child = layer.GetChildAt(i);
                if (child?.visible != true
                    || (!child.name.StartsWith("planAction", System.StringComparison.Ordinal)
                        && !child.name.StartsWith("planExit", System.StringComparison.Ordinal)))
                {
                    continue;
                }

                Fgui.GComponent block = child.asCom;
                Fgui.GObject fill = block?.GetChild("planProgressFill");
                Fgui.GObject sweep = block?.GetChild("planIndeterminateSweep");
                Fgui.GTextField badge = block?.GetChild("planStateBadge")?.asTextField;
                bool frameBadgeVisible = badge?.visible == true
                    && badge.text.IndexOf("/", System.StringComparison.Ordinal) >= 0
                    && badge.text.IndexOf('%') < 0;
                bool fixedProgressVisible = fill?.visible == true;
                bool openEndedActiveStateVisible = fill?.visible == false
                    && sweep?.visible == false
                    && badge != null
                    && badge.text.IndexOf("∞", System.StringComparison.Ordinal) >= 0;
                if (frameBadgeVisible
                    && (fixedProgressVisible || openEndedActiveStateVisible))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Enqueue(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink,
            string commandId)
        {
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                null));
        }

        private static bool ClickTimelineModeButton(
            Fgui.GComponent root,
            string childName,
            out string diagnostic)
        {
            return ClickEnabledButton(root, childName, out diagnostic);
        }

        private static bool ClickEnabledButton(
            Fgui.GComponent parent,
            string childName,
            out string diagnostic)
        {
            Fgui.GButton button = parent?.GetChild(childName)?.asButton;
            if (button == null
                || !button.visible
                || !button.enabled
                || !button.touchable)
            {
                diagnostic = childName + " is not an enabled, touchable timeline tab.";
                return false;
            }

            Vector2 center = button.LocalToGlobal(
                new Vector2(button.width * 0.5f, button.height * 0.5f));
            Fgui.DisplayObject hit = Fgui.Stage.inst.HitTest(center, true);
            Fgui.GObject hitOwner = hit?.gOwner;
            bool hitBelongsToButton = false;
            for (Fgui.GObject current = hitOwner;
                current != null;
                current = current.parent)
            {
                if (!ReferenceEquals(current, button))
                    continue;
                hitBelongsToButton = true;
                break;
            }
            if (!hitBelongsToButton)
            {
                diagnostic = childName + " is covered at its visual center by "
                    + (hitOwner?.name ?? hit?.name ?? "no hit target") + ".";
                return false;
            }

            button.onClick.Call();
            diagnostic = string.Empty;
            return true;
        }

        private static TriggerTestOperationResult Fail(
            TriggerTestRuntimeAiLabTimelineModeProbe probe,
            string diagnostic)
        {
            probe.Failure = diagnostic ?? string.Empty;
            return TriggerTestOperationResult.Failed(probe.Failure);
        }
    }

    internal sealed class TriggerTestRuntimeAiLabTimelineModeProbe
    {
        internal int InvocationCount { get; set; }
        internal int SelectedAgentIndex { get; set; }
        internal int SelectedFrame { get; set; }
        internal bool Paused { get; set; }
        internal bool CameraAvailable { get; set; }
        internal Vector3 CameraPosition { get; set; }
        internal Quaternion CameraRotation { get; set; }
        internal bool ExplicitPlanPassed { get; set; }
        internal bool ExplicitLogicPassed { get; set; }
        internal bool ExplicitRuntimePassed { get; set; }
        internal bool RuntimeAliasPassed { get; set; }
        internal bool BehaviorAliasPassed { get; set; }
        internal bool RuntimeIdentityMatched { get; set; }
        internal bool SessionControlsPassed { get; set; }
        internal bool RestartFromHistoricalPassed { get; set; }
        internal bool Completed { get; set; }
        internal string Failure { get; set; } = string.Empty;
    }

    internal sealed class TriggerTestRuntimeAiLabTimelineModeAssertion :
        ITriggerTestAssertion
    {
        public string AssertionId => "runtime-ai-lab-switch-timeline-mode";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabTimelineModeProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab timeline-mode probe is unavailable.");
            }
            bool passed = probe.InvocationCount == 1
                && probe.ExplicitPlanPassed
                && probe.ExplicitLogicPassed
                && probe.ExplicitRuntimePassed
                && probe.RuntimeAliasPassed
                && probe.BehaviorAliasPassed
                && probe.SessionControlsPassed
                && probe.RestartFromHistoricalPassed
                && probe.Completed;
            return passed
                ? TriggerTestOperationResult.Succeeded(
                    "The real Runtime, Plan, and Logic tab click listeners and both legacy aliases preserve the unified Runtime Test Workbench context.")
                : TriggerTestOperationResult.Failed(
                    string.IsNullOrEmpty(probe.Failure)
                        ? "The unified timeline-mode flow did not complete."
                        : probe.Failure);
        }
    }
}
