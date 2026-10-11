using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    [McpForUnityTool(
        "runtime-ai-lab-character-action-authoring",
        Description = "Open the embedded CharacterAction Action Composition workspace and verify shared editing, current-character preview, cleanup and frame-0 reset through Trigger Automation.",
        Group = "core",
        RequiresPolling = true,
        PollAction = "status",
        MaxPollSeconds = 150)]
    internal static class TriggerTestRuntimeAiLabCharacterActionAuthoringMcpTool
    {
        private const string JobId = "runtime-ai-lab-character-action-authoring";

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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return new ErrorResponse("CharacterAction authoring Trigger must start in Edit Mode.");
            TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow.ClearForTests();
            TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow.ClearForTests();
            var service = new TriggerTestManualExecutionService(
                typeof(TriggerTestRuntimeAiLabCharacterActionAuthoringMcpTool).Assembly);
            TriggerTestManualExecutionResult execution = service.Run(
                RuntimeAiLabCharacterActionAuthoringScenario.ScenarioId);
            TriggerTestRunResult run = execution.FindResult(
                RuntimeAiLabCharacterActionAuthoringScenario.ScenarioId);
            if (!execution.Discovery.Succeeded
                || run == null
                || !run.InternalSucceeded
                || execution.FailedCount != 0)
            {
                return new ErrorResponse(
                    string.IsNullOrWhiteSpace(execution.ErrorMessage)
                        ? "CharacterAction authoring Trigger was not scheduled."
                        : execution.ErrorMessage,
                    BuildEvidence());
            }
            return Pending("CharacterAction authoring Trigger is entering Play Mode.");
        }

        private static object Poll()
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow.CaptureSnapshot();
            return snapshot.Status switch
            {
                TriggerTestEditorWorkflowStatus.Running => Pending(snapshot.Message),
                TriggerTestEditorWorkflowStatus.Passed => new SuccessResponse(snapshot.Message, BuildEvidence()),
                TriggerTestEditorWorkflowStatus.Failed => new ErrorResponse(snapshot.Message, BuildEvidence()),
                _ => new ErrorResponse("CharacterAction authoring workflow state is unavailable.", BuildEvidence()),
            };
        }

        private static PendingResponse Pending(string message) => new PendingResponse(
            message,
            pollIntervalSeconds: 0.5d,
            data: BuildEvidence());

        private static object BuildEvidence()
        {
            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow.CaptureSnapshot();
            return new
            {
                job_id = JobId,
                scenario_id = RuntimeAiLabCharacterActionAuthoringScenario.ScenarioId,
                workflow_status = snapshot.Status.ToString(),
                workflow_message = snapshot.Message,
                elapsed_seconds = snapshot.ElapsedSeconds,
                package_id = "MxCharacterTestRuntimeAiLabUi",
                workspace = "RuntimeAiLabCharacterActionWorkspace",
            };
        }
    }
}
