using System;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Game.CharacterTest;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal enum TriggerTestEditorWorkflowStatus
    {
        None = 0,
        Running = 1,
        Passed = 2,
        Failed = 3
    }

    internal readonly struct TriggerTestRuntimeAiLabPlayModeSnapshot
    {
        internal TriggerTestRuntimeAiLabPlayModeSnapshot(
            TriggerTestEditorWorkflowStatus status,
            string message,
            double elapsedSeconds)
        {
            Status = status;
            Message = message ?? string.Empty;
            ElapsedSeconds = elapsedSeconds;
        }

        internal TriggerTestEditorWorkflowStatus Status { get; }
        internal string Message { get; }
        internal double ElapsedSeconds { get; }
        internal bool HasState => Status != TriggerTestEditorWorkflowStatus.None;
        internal bool IsRunning =>
            Status == TriggerTestEditorWorkflowStatus.Running;
    }

    /// <summary>
    /// Completes the Enter Play Mode Trigger after domain reload.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This Editor-only workflow runs
    /// only after an explicit manual Trigger request.
    /// </remarks>
    [InitializeOnLoad]
    internal static class TriggerTestRuntimeAiLabEnterPlayModeWorkflow
    {
        private const string SessionKey =
            "MxFramework.TriggerTests.RuntimeAiLabEnterPlayMode.v1";
        private const double VerificationTimeoutSeconds = 10d;

        private static double _verificationDeadline;
        private static bool _verificationRegistered;

        internal static event Action StateChanged;

        static TriggerTestRuntimeAiLabEnterPlayModeWorkflow()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ResumePendingWorkflow;
        }

        internal static bool IsRunning => CaptureSnapshot().IsRunning;

        internal static TriggerTestRuntimeAiLabPlayModeSnapshot CaptureSnapshot()
        {
            WorkflowState state = Load();
            double elapsed = state.StartedAt <= 0d
                ? 0d
                : Math.Max(
                    0d,
                    (state.CompletedAt > 0d
                        ? state.CompletedAt
                        : EditorApplication.timeSinceStartup) -
                    state.StartedAt);
            return new TriggerTestRuntimeAiLabPlayModeSnapshot(
                (TriggerTestEditorWorkflowStatus)state.Status,
                state.Message,
                elapsed);
        }

        internal static bool TryBegin(out string message)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "Unity 已处于或正在进入 Play Mode。";
                return false;
            }
            if (IsRunning)
            {
                message = "进入 Play Mode Trigger 正在执行。";
                return false;
            }
            if (!TriggerTestRuntimeAiLabEditorActions
                    .TryValidateTargetScene(out message))
            {
                return false;
            }

            var state = new WorkflowState
            {
                Status = (int)TriggerTestEditorWorkflowStatus.Running,
                Message = "已提交进入 Play Mode 请求。",
                StartedAt = EditorApplication.timeSinceStartup
            };
            Save(state);
            Debug.Log(
                "[TriggerTest][RuntimeAiLabEnterPlayMode][START] " +
                state.Message);
            EditorApplication.delayCall += RequestEnterPlayMode;
            message = state.Message;
            return true;
        }

        internal static bool ValidateScheduledState(out string message)
        {
            WorkflowState state = Load();
            bool valid =
                state.Status ==
                (int)TriggerTestEditorWorkflowStatus.Running &&
                string.Equals(
                    SceneManager.GetActiveScene().path,
                    TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                    StringComparison.Ordinal);
            message = valid
                ? "已验证目标场景，并持久化 Play Mode 恢复状态。"
                : "进入 Play Mode 的恢复状态没有正确持久化。";
            return valid;
        }

        internal static void CompleteFromExternalVerification(string message)
        {
            WorkflowState state = Load();
            state.Status = (int)TriggerTestEditorWorkflowStatus.Passed;
            state.Message = message ?? string.Empty;
            state.CompletedAt = EditorApplication.timeSinceStartup;
            Save(state);
            StopVerification();
            Debug.Log(
                "[TriggerTest][RuntimeAiLabEnterPlayMode][PASS] " +
                state.Message);
        }

        internal static void ClearForTests()
        {
            StopVerification();
            SessionState.EraseString(SessionKey);
        }

        private static void RequestEnterPlayMode()
        {
            WorkflowState state = Load();
            if (state.Status !=
                (int)TriggerTestEditorWorkflowStatus.Running)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                BeginVerification();
                return;
            }

            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            WorkflowState workflow = Load();
            if (workflow.Status !=
                (int)TriggerTestEditorWorkflowStatus.Running)
            {
                return;
            }

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    BeginVerification();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    Fail(
                        workflow,
                        "Unity 未能保持 Play Mode，操作已中止。");
                    break;
            }
        }

        private static void ResumePendingWorkflow()
        {
            WorkflowState state = Load();
            if (state.Status ==
                (int)TriggerTestEditorWorkflowStatus.Running &&
                EditorApplication.isPlaying)
            {
                BeginVerification();
            }
        }

        private static void BeginVerification()
        {
            _verificationDeadline =
                EditorApplication.timeSinceStartup +
                VerificationTimeoutSeconds;
            if (_verificationRegistered)
                return;

            EditorApplication.update += VerifyPlayModeReady;
            _verificationRegistered = true;
        }

        private static void VerifyPlayModeReady()
        {
            WorkflowState state = Load();
            if (state.Status !=
                (int)TriggerTestEditorWorkflowStatus.Running)
            {
                StopVerification();
                return;
            }
            if (!EditorApplication.isPlaying)
                return;

            if (!string.Equals(
                    SceneManager.GetActiveScene().path,
                    TriggerTestRuntimeAiLabEditorActions.TargetScenePath,
                    StringComparison.Ordinal))
            {
                Fail(
                    state,
                    "Play Mode 的活动场景不是 Runtime AI Lab。");
                return;
            }

            int liveCount =
                TriggerTestRuntimeAiLabEditorActions.CountActiveControllers(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller);
            if (liveCount == 1 &&
                controller.Phase ==
                CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                state.Status =
                    (int)TriggerTestEditorWorkflowStatus.Passed;
                state.Message =
                    "Play Mode 已启动；Runtime AI Lab 处于准备测试状态。";
                state.CompletedAt = EditorApplication.timeSinceStartup;
                Save(state);
                StopVerification();
                Debug.Log(
                    "[TriggerTest][RuntimeAiLabEnterPlayMode][PASS] " +
                    state.Message);
                return;
            }

            if (EditorApplication.timeSinceStartup <
                _verificationDeadline)
            {
                return;
            }

            string diagnostic = liveCount == 0
                ? "Play Mode 中没有活动的 Runtime AI Lab Controller。"
                : liveCount > 1
                    ? "Play Mode 中存在多个 Runtime AI Lab Controller。"
                    : "Runtime AI Lab Controller 未进入准备测试状态。";
            Fail(state, diagnostic);
        }

        private static void Fail(
            WorkflowState state,
            string message)
        {
            state.Status =
                (int)TriggerTestEditorWorkflowStatus.Failed;
            state.Message = message;
            state.CompletedAt = EditorApplication.timeSinceStartup;
            Save(state);
            StopVerification();
            Debug.LogError(
                "[TriggerTest][RuntimeAiLabEnterPlayMode][FAIL] " +
                message);
        }

        private static void StopVerification()
        {
            if (!_verificationRegistered)
                return;

            EditorApplication.update -= VerifyPlayModeReady;
            _verificationRegistered = false;
        }

        private static WorkflowState Load()
        {
            string json = SessionState.GetString(
                SessionKey,
                string.Empty);
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
            SessionState.SetString(
                SessionKey,
                JsonUtility.ToJson(state));
            WriteReport(state);
            StateChanged?.Invoke();
        }

        private static void WriteReport(WorkflowState state)
        {
            var scenarioId = new TriggerTestScenarioId(
                RuntimeAiLabEnterPlayModeScenario.ScenarioId);
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
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                status =
                    ((TriggerTestEditorWorkflowStatus)state.Status)
                    .ToString(),
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
            public string Message = string.Empty;
            public double StartedAt;
            public double CompletedAt;
        }
    }
}
