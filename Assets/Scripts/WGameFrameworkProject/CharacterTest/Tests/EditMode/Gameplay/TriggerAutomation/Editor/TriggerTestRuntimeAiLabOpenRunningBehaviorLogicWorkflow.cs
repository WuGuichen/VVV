using System;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Game.CharacterTest;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using MxFramework.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    /// <summary>
    /// Opens the running Runtime AI Lab Behavior Logic workspace as one
    /// cross-domain-reload Trigger workflow.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This Editor-only workflow runs
    /// only after an explicit manual Trigger request.
    /// </remarks>
    [InitializeOnLoad]
    internal static class
        TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow
    {
        private const string SessionKey =
            "MxFramework.TriggerTests.RuntimeAiLabOpenRunningBehaviorLogic.v1";
        private const double WorkflowTimeoutSeconds = 30d;

        private enum WorkflowPhase
        {
            None = 0,
            RequestPlayMode = 1,
            AwaitSetup = 2,
            AwaitRunning = 3,
            AwaitBehaviorLogic = 4,
            AwaitTimelineBlankSelection = 5
        }

        static TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static TriggerTestRuntimeAiLabPlayModeSnapshot CaptureSnapshot()
        {
            WorkflowState state = Load();
            double elapsed = state.StartedAt <= 0d
                ? 0d
                : Math.Max(
                    0d,
                    (state.CompletedAt > 0d
                        ? state.CompletedAt
                        : EditorApplication.timeSinceStartup) - state.StartedAt);
            return new TriggerTestRuntimeAiLabPlayModeSnapshot(
                (TriggerTestEditorWorkflowStatus)state.Status,
                state.Message,
                elapsed);
        }

        internal static string CapturePhaseForEvidence()
        {
            WorkflowState state = Load();
            return ((WorkflowPhase)state.Phase).ToString();
        }

        internal static bool TryBegin(out string message)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "请先返回 Edit Mode，再运行一键打开 Trigger。";
                return false;
            }

            WorkflowState existing = Load();
            if (existing.Status ==
                (int)TriggerTestEditorWorkflowStatus.Running)
            {
                message = "一键打开运行中编辑逻辑的 Trigger 正在执行。";
                return false;
            }

            if (!TriggerTestRuntimeAiLabEditorActions.TryOpenTargetScene(
                    out message))
            {
                return false;
            }

            var state = new WorkflowState
            {
                Status = (int)TriggerTestEditorWorkflowStatus.Running,
                Phase = (int)WorkflowPhase.RequestPlayMode,
                Message = "已打开 Runtime AI Lab，准备进入 Play Mode。",
                StartedAt = EditorApplication.timeSinceStartup
            };
            Save(state);
            Debug.Log(
                "[TriggerTest][RuntimeAiLabOpenRunningBehaviorLogic][START] " +
                state.Message);
            message = state.Message;
            return true;
        }

        internal static bool ValidateScheduledState(out string message)
        {
            WorkflowState state = Load();
            bool valid =
                state.Status ==
                (int)TriggerTestEditorWorkflowStatus.Running &&
                state.Phase >= (int)WorkflowPhase.RequestPlayMode &&
                string.Equals(
                    SceneManager.GetActiveScene().path,
                    TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                    StringComparison.Ordinal);
            message = valid
                ? "已持久化一键打开工作流，等待跨 Domain Reload 继续执行。"
                : "一键打开工作流没有正确进入等待状态。";
            return valid;
        }

        internal static void ClearForTests()
        {
            SessionState.EraseString(SessionKey);
        }

        private static void Tick()
        {
            WorkflowState state = Load();
            if (state.Status !=
                (int)TriggerTestEditorWorkflowStatus.Running)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - state.StartedAt >
                WorkflowTimeoutSeconds)
            {
                string lastDiagnostic = state.Message;
                Fail(
                    state,
                    "一键打开超时；停在 " +
                    ((WorkflowPhase)state.Phase) +
                    "，最终界面没有在限定时间内达到可验证状态。最后状态：" +
                    lastDiagnostic);
                return;
            }

            switch ((WorkflowPhase)state.Phase)
            {
                case WorkflowPhase.RequestPlayMode:
                    RequestPlayMode(state);
                    break;
                case WorkflowPhase.AwaitSetup:
                    TryStartTest(state);
                    break;
                case WorkflowPhase.AwaitRunning:
                    TryOpenBehaviorLogic(state);
                    break;
                case WorkflowPhase.AwaitBehaviorLogic:
                    TryComplete(state);
                    break;
                case WorkflowPhase.AwaitTimelineBlankSelection:
                    TryCompleteTimelineBlankSelection(state);
                    break;
                default:
                    Fail(state, "一键打开工作流的阶段状态无效。");
                    break;
            }
        }

        private static void RequestPlayMode(WorkflowState state)
        {
            if (!TriggerTestRuntimeAiLabEditorActions.TryValidateTargetScene(
                    out string diagnostic))
            {
                Fail(state, diagnostic);
                return;
            }

            state.Phase = (int)WorkflowPhase.AwaitSetup;
            state.Message = "正在进入 Play Mode。";
            Save(state);
            EditorApplication.EnterPlaymode();
        }

        private static void TryStartTest(WorkflowState state)
        {
            if (!EditorApplication.isPlaying)
                return;
            if (!TryResolveController(out var controller))
                return;
            if (controller.Phase !=
                CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                return;
            }

            if (!controller.StartSessionThroughShell())
            {
                Fail(
                    state,
                    string.IsNullOrEmpty(controller.StatusMessage)
                        ? "Runtime AI Lab 拒绝开始测试。"
                        : controller.StatusMessage);
                return;
            }

            state.Phase = (int)WorkflowPhase.AwaitRunning;
            state.Message = "测试已开始，正在等待首个可检查帧。";
            Save(state);
        }

        private static void TryOpenBehaviorLogic(WorkflowState state)
        {
            if (!EditorApplication.isPlaying)
                return;
            if (!TryResolveController(out var controller))
                return;

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot =
                controller.CaptureSnapshot();
            if (snapshot.Phase !=
                    CharacterTestRuntimeAiPlannerDevScenePhase.Running ||
                !snapshot.Paused ||
                snapshot.Timeline == null ||
                snapshot.Timeline.Count <= 0)
            {
                return;
            }

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink commandSink =
                panelHost?.CommandSink;
            CharacterTestRuntimeAiLabShellState shellState =
                controller.ShellCoordinator?.CurrentState;
            if (panelHost == null ||
                !panelHost.IsOpen ||
                panelHost.RootForTests == null ||
                commandSink == null ||
                shellState?.ActiveWorkspace !=
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
            {
                return;
            }

            if (!commandSink.TrySelectCurrentRuntimeBehaviorForPresentation(
                    out string diagnostic))
            {
                Fail(state, diagnostic);
                return;
            }

            commandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .TimelineModeBehaviorLogic,
                null));
            panelHost.Refresh();
            state.Phase = (int)WorkflowPhase.AwaitBehaviorLogic;
            state.Message =
                "已提交 TimelineModeBehaviorLogic 语义命令，正在验证真实界面。";
            Save(state);
        }

        private static void TryComplete(WorkflowState state)
        {
            if (!EditorApplication.isPlaying)
                return;
            if (!TryResolveController(out var controller))
                return;

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot =
                controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabShellState shellState =
                controller.ShellCoordinator?.CurrentState;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink commandSink =
                panelHost?.CommandSink;
            Fgui.GComponent root = panelHost?.RootForTests;
            Fgui.GButton behaviorLogicTab = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .TimelineModeBehaviorLogic)?.asButton;
            Fgui.GComponent runtimeWorkspace = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .RuntimeFrameWorkspace)?.asCom;
            Fgui.GComponent behaviorTimeline = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .BehaviorCardTimeline)?.asCom;
            Fgui.GComponent recipeHost = behaviorTimeline?.GetChild(
                "timelineGroupHost")?.asCom;
            Fgui.GComponent recipeViewport = recipeHost?.GetChild(
                "recipeViewport")?.asCom;
            Fgui.GComponent recipeRows = recipeViewport?.GetChild(
                "recipeRows")?.asCom;
            Fgui.GComponent eventTrack0 = recipeRows?.GetChild(
                "recipeTrack0")?.asCom;
            Fgui.GComponent eventTrack1 = recipeRows?.GetChild(
                "recipeTrack1")?.asCom;
            Fgui.GComponent eventTrack2 = recipeRows?.GetChild(
                "recipeTrack2")?.asCom;
            Fgui.GComponent unifiedEventInspector = runtimeWorkspace?.GetChild(
                "behaviorRecipeInspector")?.asCom;
            Fgui.GObject legacyEventDetails = runtimeWorkspace?.GetChild(
                "timelineEventDetails");
            Fgui.GButton undoButton = behaviorTimeline?.GetChild(
                "btnTimelineUndo")?.asButton;
            Fgui.GButton redoButton = behaviorTimeline?.GetChild(
                "btnTimelineRedo")?.asButton;
            Fgui.GObject commandRail = behaviorTimeline?.GetChild("toolbar");
            Fgui.GObject saveButton = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft);
            Fgui.GList behaviorList = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList)?.asList;

            Fgui.GObject firstUnifiedEvent = eventTrack0?.GetChild("trackContent")?.asCom
                ?.GetChild("eventSpan");
            var tactical = panelHost?.LastViewModel?.BehaviorCardWorkspace?.Timeline?.TacticalPreview;
            if (tactical != null && !tactical.IsConditionProjection)
            {
                // Tactical authoring now keeps all event sections in one Inspector. The original
                // single-event/title/blank-selection checks below remain for native Recipe authoring.
                var sections = unifiedEventInspector?.GetChild("conditionList")?.asList;
                int expected = 0;
                foreach (var timeline in tactical.Source.Timelines) expected += timeline.Events.Count;
                if (snapshot.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running || !snapshot.Paused
                    || behaviorTimeline?.visible != true || unifiedEventInspector?.visible != true
                    || sections == null || sections.numChildren != expected) return;
                foreach (var timeline in tactical.Source.Timelines)
                foreach (var evt in timeline.Events)
                {
                    var section = sections.GetChild("eventSet:" + timeline.TimelineId + ":" + evt.EventId)?.asCom;
                    if (section?.GetChild("btnGroupAll")?.visible != true
                        || section.GetChild("inputLocalFrame")?.visible == true
                        || section.GetChild("inputDurationFrames")?.visible == true
                        || section.GetChild("inputPriority")?.visible == true) return;
                }
                state.Status = (int)TriggerTestEditorWorkflowStatus.Passed;
                state.Message = "已通过 Trigger 进入暂停测试的战术制作页，核对全部事件区域及隐藏的时间参数。";
                state.CompletedAt = EditorApplication.timeSinceStartup;
                Save(state);
                return;
            }
            bool eventInspectorSelected =
                unifiedEventInspector?.visible == true &&
                (unifiedEventInspector.GetChild("headerTitle")?.asTextField?.text?.Contains("事件") == true);
            if (!eventInspectorSelected && firstUnifiedEvent != null)
            {
                firstUnifiedEvent.onClick.Call();
                panelHost.Refresh();
                return;
            }

            bool passed =
                snapshot.Phase ==
                    CharacterTestRuntimeAiPlannerDevScenePhase.Running &&
                snapshot.Paused &&
                snapshot.Timeline != null &&
                snapshot.Timeline.Count > 0 &&
                shellState?.ActiveWorkspace ==
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame &&
                shellState.PageContext.TimelineMode ==
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic &&
                commandSink?.TimelineMode ==
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic &&
                behaviorLogicTab?.selected == true &&
                runtimeWorkspace?.visible == true &&
                behaviorTimeline?.visible == true &&
                Mathf.Approximately(behaviorTimeline.x, 348f) &&
                Mathf.Approximately(behaviorTimeline.y, 657.44f) &&
                Mathf.Approximately(behaviorTimeline.width, 1184f) &&
                Mathf.Approximately(behaviorTimeline.height, 406.56f) &&
                recipeHost?.visible == true &&
                Mathf.Approximately(recipeHost.x, 120f) &&
                Mathf.Approximately(recipeHost.width, 1064f) &&
                recipeViewport != null &&
                Mathf.Approximately(recipeViewport.width, 1064f) &&
                Mathf.Approximately(
                    recipeHost.GetChild("recipeVerticalScrollTrack")?.x ?? 0f,
                    1054f) &&
                recipeRows != null &&
                recipeRows?.GetChild("recipeTimelineHeader0") == null &&
                eventTrack0?.GetChild("trackLabel")?.visible == false &&
                (eventTrack1 == null || !eventTrack1.visible ||
                    eventTrack1.GetChild("trackLabel")?.visible == false) &&
                (eventTrack2 == null || !eventTrack2.visible ||
                    eventTrack2.GetChild("trackLabel")?.visible == false) &&
                eventTrack0.GetChild("trackContent")?.asCom
                    ?.GetChild("eventSpan") != null &&
                (eventTrack1 == null || !eventTrack1.visible ||
                    eventTrack1.GetChild("trackContent")?.asCom
                        ?.GetChild("eventSpan") != null) &&
                (eventTrack2 == null || !eventTrack2.visible ||
                    eventTrack2.GetChild("trackContent")?.asCom
                        ?.GetChild("eventSpan") != null) &&
                unifiedEventInspector?.visible == true &&
                Mathf.Approximately(unifiedEventInspector.x, 1544f) &&
                Mathf.Approximately(unifiedEventInspector.width, 360f) &&
                Mathf.Approximately(unifiedEventInspector.scaleX, 1f) &&
                root.GetChild("behaviorCardRuntimeHeader")?.asCom?.GetChild("btnRuntimeExpand") is Fgui.GButton &&
                root.GetChild("behaviorCardRuntimeHeader")?.asCom?.GetChild("previewTimelineDivider")?.visible == true &&
                legacyEventDetails?.visible == false &&
                (unifiedEventInspector.GetChild("headerTitle")?.asTextField?.text?.Contains("事件") == true) &&
                recipeHost.GetChild("recipeHorizontalScrollTrack") != null &&
                recipeHost.GetChild("recipeHorizontalScrollThumb") != null &&
                recipeHost.GetChild("recipeVerticalScrollTrack") != null &&
                recipeHost.GetChild("recipeVerticalScrollThumb") != null &&
                undoButton?.visible == true &&
                redoButton?.visible == true &&
                Mathf.Approximately(
                    undoButton.x,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailButtonX) &&
                Mathf.Approximately(
                    undoButton.y,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailUndoY) &&
                Mathf.Approximately(
                    redoButton.x,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailButtonX) &&
                Mathf.Approximately(
                    redoButton.y,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailRedoY) &&
                commandRail?.visible == true &&
                Mathf.Approximately(commandRail.width, 120f) &&
                saveButton?.visible == true &&
                Mathf.Approximately(
                    saveButton.x,
                    359f) &&
                Mathf.Approximately(
                    saveButton.y,
                    799.44f) &&
                Mathf.Approximately(
                    behaviorList?.width ?? 0f,
                    304f) &&
                undoButton.sortingOrder > recipeHost.sortingOrder &&
                redoButton.sortingOrder > recipeHost.sortingOrder;
            if (!passed)
            {
                string diagnostic = string.Format(
                    CultureInfo.InvariantCulture,
                    "等待 v9 行为逻辑界面：phase={0}, paused={1}, timeline={2}, " +
                    "shell={3}, sink={4}, tab={5}, runtime={6}, timelineUi={7}, " +
                    "timelineRect={8:0.#},{9:0.#},{10:0.#},{11:0.#}, " +
                    "host={12}, viewport={13}, rows={14}, header={15}, " +
                    "events={16}/{17}/{18}, labelsHidden={19}/{20}/{21}, " +
                    "inspector={22}, inspectorHeader={23}, legacyVisible={24}, " +
                    "undo={25}, redo={26}, rail={27}, save={28}, listWidth={29:0.#}",
                    snapshot.Phase,
                    snapshot.Paused,
                    snapshot.Timeline?.Count ?? 0,
                    shellState?.ActiveWorkspace,
                    commandSink?.TimelineMode,
                    behaviorLogicTab?.selected,
                    runtimeWorkspace?.visible,
                    behaviorTimeline?.visible,
                    behaviorTimeline?.x ?? -1f,
                    behaviorTimeline?.y ?? -1f,
                    behaviorTimeline?.width ?? -1f,
                    behaviorTimeline?.height ?? -1f,
                    recipeHost?.visible,
                    recipeViewport != null,
                    recipeRows != null,
                    recipeRows?.GetChild("recipeTimelineHeader0") != null,
                    eventTrack0?.GetChild("trackContent")?.asCom
                        ?.GetChild("eventSpan") != null,
                    eventTrack1?.GetChild("trackContent")?.asCom
                        ?.GetChild("eventSpan") != null,
                    eventTrack2?.GetChild("trackContent")?.asCom
                        ?.GetChild("eventSpan") != null,
                    eventTrack0?.GetChild("trackLabel")?.visible == false,
                    eventTrack1?.GetChild("trackLabel")?.visible == false,
                    eventTrack2?.GetChild("trackLabel")?.visible == false,
                    unifiedEventInspector?.visible,
                    unifiedEventInspector?.GetChild("headerTitle")?.asTextField?.text,
                    legacyEventDetails?.visible,
                    undoButton?.visible,
                    redoButton?.visible,
                    commandRail?.visible,
                    saveButton?.visible,
                    behaviorList?.width ?? -1f);
                if (!string.Equals(state.Message, diagnostic, StringComparison.Ordinal))
                {
                    state.Message = diagnostic;
                    Save(state);
                }
                return;
            }

            Fgui.GComponent firstTrackContent = eventTrack0?.GetChild("trackContent")?.asCom;
            if (firstTrackContent == null)
                return;
            firstTrackContent.onClick.Call();
            panelHost.Refresh();
            state.Phase = (int)WorkflowPhase.AwaitTimelineBlankSelection;
            state.Message = "已验证统一 Event；正在验证点击轨道空白选择 Timeline。";
            Save(state);
        }

        private static void TryCompleteTimelineBlankSelection(WorkflowState state)
        {
            if (!EditorApplication.isPlaying)
                return;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Fgui.GComponent root = panelHost?.RootForTests;
            Fgui.GComponent runtimeWorkspace = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)?.asCom;
            Fgui.GComponent behaviorTimeline = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)?.asCom;
            Fgui.GComponent recipeRows = behaviorTimeline?.GetChild("timelineGroupHost")?.asCom
                ?.GetChild("recipeViewport")?.asCom
                ?.GetChild("recipeRows")?.asCom;
            Fgui.GComponent inspector = runtimeWorkspace?.GetChild(
                "behaviorRecipeInspector")?.asCom;
            bool passed =
                recipeRows?.GetChild("recipeTimelineHeader0") == null &&
                inspector?.visible == true &&
                string.Equals(
                    inspector.GetChild("headerTitle")?.asTextField?.text,
                    "时间轴",
                    StringComparison.Ordinal);
            if (!passed)
                return;

            state.Status = (int)TriggerTestEditorWorkflowStatus.Passed;
            state.Message =
                "已通过 Trigger 验证无 Timeline Header Event、Event 选择，以及点击轨道空白切换到正式 Timeline Inspector。";
            state.CompletedAt = EditorApplication.timeSinceStartup;
            Save(state);
            Debug.Log(
                "[TriggerTest][RuntimeAiLabOpenRunningBehaviorLogic][PASS] " +
                state.Message);
        }

        private static bool TryResolveController(
            out CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            int count =
                TriggerTestRuntimeAiLabEditorActions.CountActiveControllers(
                    out controller);
            return count == 1;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
                return;

            WorkflowState state = Load();
            if (state.Status ==
                    (int)TriggerTestEditorWorkflowStatus.Running &&
                state.Phase >= (int)WorkflowPhase.AwaitSetup)
            {
                Fail(state, "Unity 已返回 Edit Mode，一键打开操作被中止。");
            }
        }

        private static void Fail(WorkflowState state, string message)
        {
            state.Status = (int)TriggerTestEditorWorkflowStatus.Failed;
            state.Message = message ?? string.Empty;
            state.CompletedAt = EditorApplication.timeSinceStartup;
            Save(state);
            Debug.LogError(
                "[TriggerTest][RuntimeAiLabOpenRunningBehaviorLogic][FAIL] " +
                state.Message);
        }

        private static WorkflowState Load()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new WorkflowState();

            try
            {
                return JsonUtility.FromJson<WorkflowState>(json) ??
                       new WorkflowState();
            }
            catch
            {
                return new WorkflowState();
            }
        }

        private static void Save(WorkflowState state)
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(state));
            WriteReport(state);
        }

        private static void WriteReport(WorkflowState state)
        {
            var scenarioId = new TriggerTestScenarioId(
                RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId);
            string jsonPath =
                TriggerTestReportWriter.GetLatestJsonPath(scenarioId);
            string directory = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            double elapsedSeconds = state.StartedAt <= 0d
                ? 0d
                : Math.Max(
                    0d,
                    (state.CompletedAt > 0d
                        ? state.CompletedAt
                        : EditorApplication.timeSinceStartup) -
                    state.StartedAt);
            var report = new EditorWorkflowReport
            {
                scenarioId =
                    RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId,
                status =
                    ((TriggerTestEditorWorkflowStatus)state.Status).ToString(),
                result = state.Message ?? string.Empty,
                elapsedSeconds = elapsedSeconds
            };
            File.WriteAllText(
                jsonPath,
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                TriggerTestReportWriter.GetLatestMarkdownPath(scenarioId),
                "# Trigger Test Editor Workflow\n\n- scenario: `" +
                report.scenarioId +
                "`\n- status: `" +
                report.status +
                "`\n- duration: `" +
                report.elapsedSeconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture) +
                " s`\n- result: " +
                report.result +
                "\n",
                new UTF8Encoding(false));
        }

        [Serializable]
        private sealed class EditorWorkflowReport
        {
            public string schema =
                "mx.trigger.test.editor-workflow-report.v1";
            public string scenarioId = string.Empty;
            public string status = string.Empty;
            public string result = string.Empty;
            public double elapsedSeconds;
        }

        [Serializable]
        private sealed class WorkflowState
        {
            public int Status;
            public int Phase;
            public string Message = string.Empty;
            public double StartedAt;
            public double CompletedAt;
        }
    }
}
