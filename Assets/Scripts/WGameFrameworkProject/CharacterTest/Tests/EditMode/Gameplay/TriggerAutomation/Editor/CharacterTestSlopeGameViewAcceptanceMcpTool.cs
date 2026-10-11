using System;
using System.Globalization;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using MxFramework.Game.CharacterTest;
using MxFramework.Tests.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    /// <summary>
    /// Pollable Editor lifecycle for the test-only slope Game View driver.
    /// SessionState survives the domain reload used by direct formal-scene Play.
    /// </summary>
    [InitializeOnLoad]
    internal static class CharacterTestSlopeGameViewAcceptanceWorkflow
    {
        private const string Prefix = "MxFramework.TriggerTests.SlopeGameView.v1.";
        private const string StatusKey = Prefix + "status";
        private const string MessageKey = Prefix + "message";
        private const string StartedKey = Prefix + "started";
        private const string ReportKey = Prefix + "report";
        private const double DriverAttachTimeoutSeconds = 20d;
        internal const string PanelSettingsPath =
            "Assets/UI/MxFramework/GameplayTriggers/GameplayTriggerPlayablePanelSettings.asset";

        static CharacterTestSlopeGameViewAcceptanceWorkflow()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        internal static CharacterTestSlopeGameViewRunStatus Status =>
            (CharacterTestSlopeGameViewRunStatus)SessionState.GetInt(
                StatusKey,
                (int)CharacterTestSlopeGameViewRunStatus.None);

        internal static string Message => SessionState.GetString(MessageKey, string.Empty);
        internal static string ReportPath => SessionState.GetString(ReportKey, DefaultReportPath());

        internal static double ElapsedSeconds
        {
            get
            {
                if (!double.TryParse(
                        SessionState.GetString(StartedKey, string.Empty),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double started))
                {
                    return 0d;
                }
                return Math.Max(0d, EditorApplication.timeSinceStartup - started);
            }
        }

        internal static bool TryStart(out string message)
        {
            if (Status == CharacterTestSlopeGameViewRunStatus.Running)
            {
                message = "Slope Game View acceptance is already running.";
                return false;
            }
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !string.Equals(
                    active.path,
                    CharacterTestSlopePlayableFixture.ScenePath,
                    StringComparison.Ordinal))
            {
                message = "Open Assets/Scenes/CharacterTestScene.unity as the active scene before starting.";
                return false;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && active.isDirty)
            {
                message = "The formal CharacterTestScene has unsaved changes; save or revert them before acceptance.";
                return false;
            }
            if (!TryDeletePreviousReport(out message))
                return false;

            Save(
                CharacterTestSlopeGameViewRunStatus.Running,
                EditorApplication.isPlaying
                    ? "Attaching the slope Game View driver in direct formal-scene Play Mode."
                    : "Entering direct formal-scene Play Mode for slope Game View acceptance.",
                resetStarted: true);
            if (EditorApplication.isPlaying)
            {
                TryAttachDriver(out message);
                return true;
            }

            EditorApplication.EnterPlaymode();
            message = Message;
            return true;
        }

        internal static bool TryAdvance(out string message)
        {
            CharacterTestSlopeGameViewAcceptanceDriver driver =
                CharacterTestSlopeGameViewAcceptanceDriver.Active;
            if (driver == null)
            {
                message = "The slope Game View driver is not available.";
                return false;
            }
            if (!driver.AcknowledgeScreenshot(out message))
                return false;
            Sync(driver);
            return true;
        }

        internal static void Cancel(string reason)
        {
            CharacterTestSlopeGameViewAcceptanceDriver driver =
                CharacterTestSlopeGameViewAcceptanceDriver.Active;
            driver?.Cancel(reason);
            Save(CharacterTestSlopeGameViewRunStatus.Canceled,
                string.IsNullOrWhiteSpace(reason) ? "Canceled by the caller." : reason);
            if (driver != null)
            {
                driver.CleanupAfterTerminal();
                driver.Dispose();
            }
            RequestExitPlayMode();
        }

        internal static void Clear()
        {
            SessionState.EraseInt(StatusKey);
            SessionState.EraseString(MessageKey);
            SessionState.EraseString(StartedKey);
            SessionState.EraseString(ReportKey);
        }

        private static void Tick()
        {
            if (Status != CharacterTestSlopeGameViewRunStatus.Running)
                return;

            CharacterTestSlopeGameViewAcceptanceDriver driver =
                CharacterTestSlopeGameViewAcceptanceDriver.Active;
            if (driver != null)
            {
                driver.TickDriver();
                Sync(driver);
                return;
            }
            if (EditorApplication.isPlaying)
            {
                if (!TryAttachDriver(out string diagnostic) &&
                    ElapsedSeconds > DriverAttachTimeoutSeconds)
                {
                    FailWithoutDriver(diagnostic);
                }
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                ElapsedSeconds > DriverAttachTimeoutSeconds)
            {
                FailWithoutDriver(
                    "Unity returned to Edit Mode before the slope Game View driver attached.");
            }
        }

        private static bool TryAttachDriver(out string message)
        {
            if (!EditorApplication.isPlaying)
            {
                message = "Waiting for direct formal-scene Play Mode.";
                return false;
            }
            Scene active = SceneManager.GetActiveScene();
            if (!string.Equals(
                    active.path,
                    CharacterTestSlopePlayableFixture.ScenePath,
                    StringComparison.Ordinal))
            {
                message = "Play Mode active scene is not the formal CharacterTestScene; neutral-scene bypass is forbidden.";
                return false;
            }
            GameManager[] managers = UnityEngine.Object.FindObjectsByType<GameManager>(
                FindObjectsInactive.Include);
            if (managers.Length != 1)
            {
                message = "Waiting for exactly one formal GameManager; current count=" + managers.Length + ".";
                return false;
            }
            if (managers[0].gameObject.scene.handle != active.handle)
            {
                message = "The unique GameManager is not owned by the active formal CharacterTestScene.";
                return false;
            }
            CharacterTestRuntimeContext context = managers[0].RuntimeContext;
            GameSlice slice = context?.Slice;
            CharacterTestCharacterRuntimeModule characterRuntime = slice?.CharacterRuntime;
            if (context?.MainMenuCoordinator == null
                || slice == null
                || characterRuntime?.ViewBridge == null
                || UnityEngine.Camera.main == null)
            {
                message = "Waiting for the formal runtime, menu coordinator, production view bridge and MainCamera.";
                return false;
            }
            try
            {
                PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
                if (panelSettings == null)
                {
                    message = "Validated Gameplay Trigger PanelSettings is missing: " + PanelSettingsPath;
                    return false;
                }
                CharacterTestSlopeGameViewAcceptanceDriver.Begin(managers[0], panelSettings);
                message = "Slope Game View acceptance driver attached to direct formal-scene Play Mode.";
                Save(CharacterTestSlopeGameViewRunStatus.Running, message);
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not attach the slope Game View driver: " + exception.Message;
                Debug.LogException(exception);
                FailWithoutDriver(message);
                return false;
            }
        }

        private static void Sync(CharacterTestSlopeGameViewAcceptanceDriver driver)
        {
            SessionState.SetString(ReportKey, driver.ReportPath);
            CharacterTestSlopeGameViewRunStatus status = driver.Status;
            Save(status, driver.Message);
            if (status == CharacterTestSlopeGameViewRunStatus.Passed
                || status == CharacterTestSlopeGameViewRunStatus.Failed
                || status == CharacterTestSlopeGameViewRunStatus.Canceled)
            {
                driver.CleanupAfterTerminal();
                driver.Dispose();
                RequestExitPlayMode();
            }
        }

        private static void FailWithoutDriver(string message)
        {
            Save(CharacterTestSlopeGameViewRunStatus.Failed, message);
            RequestExitPlayMode();
        }

        private static void RequestExitPlayMode()
        {
            EditorApplication.delayCall -= ExitPlayModeIfNeeded;
            EditorApplication.delayCall += ExitPlayModeIfNeeded;
        }

        private static void ExitPlayModeIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.ExitPlaymode();
        }

        private static bool TryDeletePreviousReport(out string message)
        {
            string path = DefaultReportPath();
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not remove the previous slope evidence report: "
                    + exception.Message;
                return false;
            }
        }

        private static void Save(
            CharacterTestSlopeGameViewRunStatus status,
            string message,
            bool resetStarted = false)
        {
            SessionState.SetInt(StatusKey, (int)status);
            SessionState.SetString(MessageKey, message ?? string.Empty);
            SessionState.SetString(ReportKey, ReportPath);
            if (resetStarted)
            {
                SessionState.SetString(
                    StartedKey,
                    EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static string DefaultReportPath()
        {
            return Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                CharacterTestSlopeGameViewAcceptanceDriver.ReportAssetRelativePath));
        }
    }

    [McpForUnityTool(
        "character-test-slope-game-view-acceptance",
        Description = "Run eight screenshot-gated static OBB slope segments in direct formal CharacterTestScene Play Mode and expose typed authority evidence.",
        Group = "core",
        RequiresPolling = true,
        PollAction = "status",
        MaxPollSeconds = 900)]
    internal static class CharacterTestSlopeGameViewAcceptanceMcpTool
    {
        private const string JobId = "character-test-slope-game-view-acceptance";

        public static object HandleCommand(JObject parameters)
        {
            string action = parameters?["action"]?.ToString() ?? "start";
            switch (action.ToLowerInvariant())
            {
                case "start":
                    return Start();
                case "status":
                    return Status();
                case "advance":
                case "ack":
                    return Advance();
                case "cancel":
                    CharacterTestSlopeGameViewAcceptanceWorkflow.Cancel(
                        parameters?["reason"]?.ToString() ?? "Canceled by MCP caller.");
                    return new SuccessResponse("Slope Game View acceptance canceled and cleanup requested.",
                        BuildEvidence());
                case "clear":
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        return new ErrorResponse("Cannot clear slope workflow state while Unity is in Play Mode.",
                            BuildEvidence());
                    CharacterTestSlopeGameViewAcceptanceWorkflow.Clear();
                    return new SuccessResponse("Slope Game View workflow state cleared.", BuildEvidence());
                default:
                    return new ErrorResponse(
                        "Unknown action. Valid actions are start, status, advance (ack), cancel, and clear.",
                        BuildEvidence());
            }
        }

        private static object Start()
        {
            CharacterTestSlopeGameViewRunStatus existing =
                CharacterTestSlopeGameViewAcceptanceWorkflow.Status;
            if (existing == CharacterTestSlopeGameViewRunStatus.Running)
                return Pending("Slope Game View acceptance is already running.");
            if (existing == CharacterTestSlopeGameViewRunStatus.Passed ||
                existing == CharacterTestSlopeGameViewRunStatus.Failed ||
                existing == CharacterTestSlopeGameViewRunStatus.Canceled)
            {
                CharacterTestSlopeGameViewAcceptanceWorkflow.Clear();
            }
            return CharacterTestSlopeGameViewAcceptanceWorkflow.TryStart(out string message)
                ? Pending(message)
                : new ErrorResponse(message, BuildEvidence());
        }

        private static object Status()
        {
            CharacterTestSlopeGameViewAcceptanceDriver driver =
                CharacterTestSlopeGameViewAcceptanceDriver.Active;
            CharacterTestSlopeGameViewRunStatus status = driver != null
                ? driver.Status
                : CharacterTestSlopeGameViewAcceptanceWorkflow.Status;
            switch (status)
            {
                case CharacterTestSlopeGameViewRunStatus.Running:
                    return Pending(driver != null && driver.ScreenshotReady
                        ? "Screenshot ready. Capture the real Game View framebuffer, then call action=advance."
                        : CharacterTestSlopeGameViewAcceptanceWorkflow.Message);
                case CharacterTestSlopeGameViewRunStatus.Passed:
                    return new SuccessResponse(
                        "All eight screenshot-gated slope Game View segments passed.",
                        BuildEvidence());
                case CharacterTestSlopeGameViewRunStatus.Failed:
                case CharacterTestSlopeGameViewRunStatus.Canceled:
                    return new ErrorResponse(
                        CharacterTestSlopeGameViewAcceptanceWorkflow.Message,
                        BuildEvidence());
                default:
                    return new ErrorResponse("Slope Game View workflow has not been started.", BuildEvidence());
            }
        }

        private static object Advance()
        {
            if (!CharacterTestSlopeGameViewAcceptanceWorkflow.TryAdvance(out string message))
                return new ErrorResponse(message, BuildEvidence());
            return Status();
        }

        private static PendingResponse Pending(string message)
        {
            return new PendingResponse(
                message,
                pollIntervalSeconds: 0.5d,
                data: BuildEvidence());
        }

        private static object BuildEvidence()
        {
            CharacterTestSlopeGameViewAcceptanceDriver driver =
                CharacterTestSlopeGameViewAcceptanceDriver.Active;
            return new
            {
                job_id = JobId,
                workflow_status = (driver != null
                    ? driver.Status
                    : CharacterTestSlopeGameViewAcceptanceWorkflow.Status).ToString(),
                workflow_message = driver?.Message
                    ?? CharacterTestSlopeGameViewAcceptanceWorkflow.Message,
                elapsed_seconds = CharacterTestSlopeGameViewAcceptanceWorkflow.ElapsedSeconds,
                editor_is_playing = EditorApplication.isPlaying,
                active_scene_path = SceneManager.GetActiveScene().path,
                formal_scene_required = CharacterTestSlopePlayableFixture.ScenePath,
                report_path = driver?.ReportPath
                    ?? CharacterTestSlopeGameViewAcceptanceWorkflow.ReportPath,
                report_written = driver?.ReportWritten
                    ?? File.Exists(CharacterTestSlopeGameViewAcceptanceWorkflow.ReportPath),
                screenshot_ready = driver?.ScreenshotReady ?? false,
                current_segment = driver?.CurrentSegmentId ?? string.Empty,
                current_phase = driver?.CurrentPhase ?? string.Empty,
                driver = driver?.BuildToolEvidence(),
                actions = new[] { "status", "advance", "cancel" },
                panel_settings = CharacterTestSlopeGameViewAcceptanceWorkflow.PanelSettingsPath,
                boundary = "Active formal Scene with exactly one live LocalPlayer and production view. Test-only direct authority setup establishes independently positioned segment/subphase starts; each 15/30 up-stop-down sequence remains continuous. Phase motion then uses FakeInputProvider, a formal CastAbilitySlot command, or a typed motion request, production command/fixed-tick authority, and formal presentation/camera bridges."
            };
        }
    }
}
