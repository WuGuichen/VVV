using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using MxFramework.Game.CharacterTest;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    [McpForUnityTool(
        "runtime-ai-lab-open-scene",
        Description = "Run the Runtime AI Lab open-scene Trigger scenario and verify the target scene and controller.",
        Group = "core")]
    internal static class TriggerTestRuntimeAiLabOpenSceneMcpTool
    {
        public static object HandleCommand(JObject parameters)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return new ErrorResponse("Runtime AI Lab scene setup requires Edit Mode.");

            var service = new TriggerTestManualExecutionService(
                typeof(TriggerTestRuntimeAiLabOpenSceneMcpTool).Assembly);
            TriggerTestManualExecutionResult execution = service.Run(
                RuntimeAiLabOpenSceneScenario.ScenarioId);
            TriggerTestRunResult run = execution.FindResult(
                RuntimeAiLabOpenSceneScenario.ScenarioId);
            bool targetValid = TriggerTestRuntimeAiLabEditorActions
                .TryValidateTargetScene(out string diagnostic);
            bool passed = execution.Discovery.Succeeded
                          && run != null
                          && run.InternalSucceeded
                          && execution.FailedCount == 0
                          && targetValid;
            object evidence = new
            {
                scenario_id = RuntimeAiLabOpenSceneScenario.ScenarioId,
                trigger_succeeded = run?.InternalSucceeded ?? false,
                scene_path = TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                controller_count = TriggerTestRuntimeAiLabEditorActions.CountActiveControllers(
                    out CharacterTestRuntimeAiPlannerDevSceneController _),
                diagnostic
            };
            return passed
                ? new SuccessResponse("Runtime AI Lab scene Trigger passed.", evidence)
                : new ErrorResponse(
                    string.IsNullOrWhiteSpace(execution.ErrorMessage)
                        ? diagnostic
                        : execution.ErrorMessage,
                    evidence);
        }
    }

    [McpForUnityTool(
        "runtime-ai-lab-enter-play-mode",
        Description = "Run the Runtime AI Lab enter-Play-Mode Trigger workflow and verify the published FairyGUI panel is configured and open.",
        Group = "core",
        RequiresPolling = true,
        PollAction = "status",
        MaxPollSeconds = 60)]
    internal static class TriggerTestRuntimeAiLabEnterPlayModeMcpTool
    {
        private const string JobId = "runtime-ai-lab-enter-play-mode";

        public static object HandleCommand(JObject parameters)
        {
            string action = parameters?["action"]?.ToString() ?? "start";
            return string.Equals(action, "status", System.StringComparison.OrdinalIgnoreCase)
                ? Poll()
                : string.Equals(action, "start", System.StringComparison.OrdinalIgnoreCase)
                    ? Start()
                    : new ErrorResponse("Unknown action. Valid actions are 'start' and 'status'.");
        }

        private static object Start()
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot existing =
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow.CaptureSnapshot();
            if (existing.IsRunning)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return Pending(existing.Message);
                if (existing.ElapsedSeconds <= 15d)
                    return Pending(existing.Message);

                TriggerTestRuntimeAiLabEnterPlayModeWorkflow.ClearForTests();
            }
            else if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return VerifyPublishedPanel(existing);
            }

            var service = new TriggerTestManualExecutionService(
                typeof(TriggerTestRuntimeAiLabEnterPlayModeMcpTool).Assembly);
            TriggerTestManualExecutionResult execution = service.Run(
                RuntimeAiLabEnterPlayModeScenario.ScenarioId);
            TriggerTestRunResult run = execution.FindResult(
                RuntimeAiLabEnterPlayModeScenario.ScenarioId);
            if (!execution.Discovery.Succeeded
                || run == null
                || !run.InternalSucceeded
                || execution.FailedCount != 0
                || !TriggerTestRuntimeAiLabEnterPlayModeWorkflow.IsRunning)
            {
                return new ErrorResponse(
                    string.IsNullOrWhiteSpace(execution.ErrorMessage)
                        ? "Runtime AI Lab enter-Play-Mode Trigger was not scheduled."
                        : execution.ErrorMessage,
                    BuildEvidence());
            }

            EditorApplication.EnterPlaymode();
            return Pending("Runtime AI Lab is entering Play Mode.");
        }

        private static object Poll()
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow.CaptureSnapshot();
            if (EditorApplication.isPlaying)
            {
                object panelVerification = VerifyPublishedPanel(snapshot);
                if (panelVerification is SuccessResponse)
                    return panelVerification;
                if (snapshot.IsRunning)
                    return Pending(snapshot.Message);
            }
            switch (snapshot.Status)
            {
                case TriggerTestEditorWorkflowStatus.Running:
                    return Pending(snapshot.Message);
                case TriggerTestEditorWorkflowStatus.Passed:
                    return VerifyPublishedPanel(snapshot);
                case TriggerTestEditorWorkflowStatus.Failed:
                    return new ErrorResponse(snapshot.Message, BuildEvidence());
                default:
                    return new ErrorResponse(
                        "Runtime AI Lab enter-Play-Mode workflow state is unavailable.",
                        BuildEvidence());
            }
        }

        private static object VerifyPublishedPanel(
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot)
        {
            int controllerCount = TriggerTestRuntimeAiLabEditorActions.CountActiveControllers(
                out CharacterTestRuntimeAiPlannerDevSceneController controller);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            bool configured = panelHost != null && panelHost.IsConfigured;
            bool opened = panelHost != null && panelHost.IsOpen;
            bool rootLoaded = panelHost?.RootForTests != null;
            bool commandBindingLoaded = panelHost?.CommandSink != null;
            bool passed = controllerCount == 1
                          && controller != null
                          && controller.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                          && configured
                          && opened
                          && rootLoaded
                          && commandBindingLoaded;
            if (passed)
            {
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow
                    .CompleteFromExternalVerification(
                        "Runtime AI Lab Play Mode and published panel checks passed.");
            }
            object evidence = BuildEvidence(
                controllerCount,
                configured,
                opened,
                rootLoaded,
                commandBindingLoaded);
            return passed
                ? new SuccessResponse(
                    "Runtime AI Lab Play Mode Trigger and published panel checks passed.",
                    evidence)
                : new ErrorResponse(
                    "Runtime AI Lab entered Play Mode, but the published panel was not fully configured and open.",
                    evidence);
        }

        private static PendingResponse Pending(string message) =>
            new PendingResponse(
                message,
                pollIntervalSeconds: 0.5d,
                data: new
                {
                    job_id = JobId,
                    workflow = BuildEvidence()
                });

        private static object BuildEvidence(
            int controllerCount = -1,
            bool configured = false,
            bool opened = false,
            bool rootLoaded = false,
            bool commandBindingLoaded = false)
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow.CaptureSnapshot();
            return new
            {
                job_id = JobId,
                scenario_id = RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                workflow_status = snapshot.Status.ToString(),
                workflow_message = snapshot.Message,
                elapsed_seconds = snapshot.ElapsedSeconds,
                scene_path = TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                controller_count = controllerCount,
                panel_configured = configured,
                panel_open = opened,
                panel_root_loaded = rootLoaded,
                command_binding_loaded = commandBindingLoaded,
                package_id = "MxCharacterTestRuntimeAiLabUi"
            };
        }
    }

    [McpForUnityTool(
        "runtime-ai-lab-open-running-behavior-logic",
        Description = "Open Runtime AI Lab, start the test and verify the v9 numeric-CharacterAction Event timeline in the real published Game View.",
        Group = "core",
        RequiresPolling = true,
        PollAction = "status",
        MaxPollSeconds = 60)]
    internal static class TriggerTestRuntimeAiLabOpenRunningBehaviorLogicMcpTool
    {
        private const string JobId = "runtime-ai-lab-open-running-behavior-logic";

        public static object HandleCommand(JObject parameters)
        {
            string action = parameters?["action"]?.ToString() ?? "start";
            return string.Equals(action, "status", System.StringComparison.OrdinalIgnoreCase)
                ? Poll()
                : string.Equals(action, "start", System.StringComparison.OrdinalIgnoreCase)
                    ? Start()
                    : new ErrorResponse("Unknown action. Valid actions are 'start' and 'status'.");
        }

        private static object Start()
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot existing =
                TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.CaptureSnapshot();
            if (existing.IsRunning)
                return Pending(existing.Message);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return new ErrorResponse(
                    "The v7 Behavior Logic workflow must start in Edit Mode.",
                    BuildEvidence(existing));

            var service = new TriggerTestManualExecutionService(
                typeof(TriggerTestRuntimeAiLabOpenRunningBehaviorLogicMcpTool).Assembly);
            TriggerTestManualExecutionResult execution = service.Run(
                RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId);
            TriggerTestRunResult run = execution.FindResult(
                RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId);
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.CaptureSnapshot();
            if (!execution.Discovery.Succeeded
                || run == null
                || !run.InternalSucceeded
                || execution.FailedCount != 0
                || !snapshot.IsRunning)
            {
                return new ErrorResponse(
                    string.IsNullOrWhiteSpace(execution.ErrorMessage)
                        ? snapshot.Message
                        : execution.ErrorMessage,
                    BuildEvidence(snapshot));
            }
            return Pending(snapshot.Message);
        }

        private static object Poll()
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.CaptureSnapshot();
            switch (snapshot.Status)
            {
                case TriggerTestEditorWorkflowStatus.Running:
                    return Pending(snapshot.Message);
                case TriggerTestEditorWorkflowStatus.Passed:
                    return new SuccessResponse(
                        "Runtime AI Lab v9 numeric-CharacterAction Event timeline passed real Game View verification.",
                        BuildEvidence(snapshot));
                case TriggerTestEditorWorkflowStatus.Failed:
                    return new ErrorResponse(snapshot.Message, BuildEvidence(snapshot));
                default:
                    return new ErrorResponse(
                        "Runtime AI Lab v7 Behavior Logic workflow state is unavailable.",
                        BuildEvidence(snapshot));
            }
        }

        private static PendingResponse Pending(string message) =>
            new PendingResponse(
                message,
                pollIntervalSeconds: 0.5d,
                data: new { job_id = JobId, workflow = BuildEvidence(
                    TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.CaptureSnapshot()) });

        private static object BuildEvidence(
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot) => new
        {
            job_id = JobId,
            scenario_id = RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId,
            workflow_status = snapshot.Status.ToString(),
            workflow_phase =
                TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow
                    .CapturePhaseForEvidence(),
            workflow_message = snapshot.Message,
            elapsed_seconds = snapshot.ElapsedSeconds,
            scene_path = TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
            behavior_recipe_schema = CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema,
            runtime_contract = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                .CurrentRuntimeContractVersion,
            package_id = "MxCharacterTestRuntimeAiLabUi"
        };
    }

    [McpForUnityTool(
        "runtime-ai-lab-responsibility-tab-hit-test",
        Description = "Inspect the real FairyGUI hit target at each Runtime AI Lab responsibility tab center.",
        Group = "core")]
    internal static class TriggerTestRuntimeAiLabResponsibilityTabHitTestMcpTool
    {
        public static object HandleCommand(JObject parameters)
        {
            if (!EditorApplication.isPlaying)
                return new ErrorResponse("Responsibility tab hit testing requires Play Mode.");

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Fgui.GComponent root = panelHost?.RootForTests;
            if (root == null)
                return new ErrorResponse("Runtime AI Lab FairyGUI root is unavailable.");

            string[] names =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther
            };
            object[] evidence = new object[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Fgui.GObject tab = root.GetChild(names[i]);
                Vector2 center = tab != null
                    ? tab.LocalToGlobal(new Vector2(tab.width * 0.5f, tab.height * 0.5f))
                    : Vector2.zero;
                Fgui.DisplayObject hit = tab != null
                    ? Fgui.Stage.inst.HitTest(center, true)
                    : null;
                Fgui.GObject hitOwner = hit?.gOwner;
                evidence[i] = new
                {
                    tab = names[i],
                    exists = tab != null,
                    visible = tab?.visible ?? false,
                    touchable = tab?.touchable ?? false,
                    x = tab?.x ?? 0f,
                    y = tab?.y ?? 0f,
                    width = tab?.width ?? 0f,
                    height = tab?.height ?? 0f,
                    sorting_order = tab?.sortingOrder ?? 0,
                    hit_owner = hitOwner?.name ?? string.Empty,
                    hit_owner_parent = hitOwner?.parent?.name ?? string.Empty,
                    reaches_tab = IsOwnedBy(hitOwner, tab)
                };
            }

            return new SuccessResponse(
                "Runtime AI Lab responsibility tab hit targets captured.",
                new { tabs = evidence });
        }

        private static bool IsOwnedBy(Fgui.GObject candidate, Fgui.GObject expected)
        {
            for (Fgui.GObject current = candidate; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, expected))
                    return true;
            }
            return false;
        }
    }
}
