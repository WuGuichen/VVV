using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Runs script refresh, compilation, domain reload readiness, and Console error
    /// acceptance as one MCP call. The MCP polling middleware keeps the client call
    /// alive while this tool persists its evidence across Unity domain reloads.
    /// </summary>
    [McpForUnityTool(
        "validate_unity_scripts",
        Description = "Refresh Unity scripts, observe a real compilation and domain reload, wait for Editor readiness, then return Console errors in one call.",
        Group = "core",
        RequiresPolling = true,
        PollAction = "status",
        MaxPollSeconds = 180)]
    public static class ValidateUnityScripts
    {
        internal const string StateKey = "validate_unity_scripts";
        internal const int TimeoutSeconds = 120;

        public static object HandleCommand(JObject @params)
        {
            string action = @params?["action"]?.ToString() ?? "start";
            return string.Equals(action, "status", StringComparison.OrdinalIgnoreCase)
                ? HandleStatus(@params?["job_id"]?.ToString())
                : string.Equals(action, "start", StringComparison.OrdinalIgnoreCase)
                    ? HandleStart()
                    : new ErrorResponse("Unknown action. Valid actions are 'start' and 'status'.");
        }

        private static object HandleStart()
        {
            if (TestRunStatus.IsRunning)
                return new ErrorResponse("tests_running");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return new ErrorResponse("Unity must be in Edit Mode before script validation.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return new ErrorResponse("Unity is already compiling or importing assets. Retry when the Editor is ready.");

            UnityScriptValidationState active = LoadState();
            if (active != null
                && !active.Terminal
                && !active.IsTimedOut(DateTime.UtcNow, TimeoutSeconds))
            {
                return Pending(active, "Script validation is already running.");
            }

            var state = UnityScriptValidationState.Create(Guid.NewGuid().ToString("N"), DateTime.UtcNow);
            SaveState(state);

            BeginValidation(state.JobId);
            state = LoadState() ?? state;
            if (state.Terminal)
            {
                return new ErrorResponse(
                    "Script validation could not be started.",
                    BuildEvidence(state, IsEditorReady(), null));
            }

            return Pending(state, "Script refresh and compilation requested.");
        }

        private static object HandleStatus(string jobId)
        {
            UnityScriptValidationState state = LoadState();
            if (state == null)
                return new ErrorResponse("No script validation job is available.");
            if (!string.IsNullOrEmpty(jobId)
                && !string.Equals(jobId, state.JobId, StringComparison.Ordinal))
            {
                return new ErrorResponse($"Unknown script validation job '{jobId}'.");
            }

            bool editorReady = IsEditorReady();
            if (state.IsTimedOut(DateTime.UtcNow, TimeoutSeconds))
            {
                state.Terminal = true;
                state.Outcome = "timed_out";
                SaveState(state);
                return new ErrorResponse("Script validation timed out.", BuildEvidence(state, editorReady, null));
            }

            UnityScriptValidationDecision preConsoleDecision =
                UnityScriptValidationPolicy.Evaluate(state, editorReady, consoleErrorCount: 0);
            if (preConsoleDecision == UnityScriptValidationDecision.Pending)
                return Pending(state, DescribePendingState(state));
            if (preConsoleDecision == UnityScriptValidationDecision.CompilationFailed)
            {
                state.Terminal = true;
                state.Outcome = "compilation_failed";
                SaveState(state);
                return new ErrorResponse("Script compilation failed.", BuildEvidence(state, editorReady, null));
            }

            object consoleResponse = ReadConsole.HandleCommand(new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray("error"),
                ["format"] = "detailed",
                ["includeStacktrace"] = true
            });
            if (!(consoleResponse is SuccessResponse success))
            {
                state.Terminal = true;
                state.Outcome = "console_read_failed";
                SaveState(state);
                return new ErrorResponse("Script compilation completed, but Console errors could not be read.",
                    BuildEvidence(state, editorReady, consoleResponse));
            }

            JToken consoleEntries = success.Data == null
                ? new JArray()
                : JToken.FromObject(success.Data);
            int consoleErrorCount = consoleEntries is JArray array ? array.Count : 0;
            UnityScriptValidationDecision finalDecision =
                UnityScriptValidationPolicy.Evaluate(state, editorReady, consoleErrorCount);

            state.Terminal = true;
            state.ConsoleErrorCount = consoleErrorCount;
            state.CompletedAtUtc = DateTime.UtcNow.ToString("O");
            state.Outcome = finalDecision == UnityScriptValidationDecision.Passed
                ? "passed"
                : "console_errors";
            SaveState(state);

            object evidence = BuildEvidence(state, editorReady, consoleEntries);
            return finalDecision == UnityScriptValidationDecision.Passed
                ? new SuccessResponse("Unity scripts compiled and Console contains no errors.", evidence)
                : new ErrorResponse(
                    $"Unity scripts compiled, but Console contains {consoleErrorCount} error(s).",
                    evidence);
        }

        private static void BeginValidation(string jobId)
        {
            UnityScriptValidationState state = LoadState();
            if (state == null
                || state.Terminal
                || !string.Equals(state.JobId, jobId, StringComparison.Ordinal))
                return;

            try
            {
                state.RefreshRequested = true;
                state.RefreshStartedAtUtc = DateTime.UtcNow.ToString("O");
                state.Phase = "refreshing";
                SaveState(state);

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                state = LoadState() ?? state;
                state.RefreshCompleted = true;
                state.CompileRequested = true;
                state.Phase = "compilation_requested";
                SaveState(state);

                CompilationPipeline.RequestScriptCompilation();
            }
            catch (Exception ex)
            {
                state = LoadState() ?? state;
                state.Terminal = true;
                state.Outcome = "request_failed";
                state.RequestError = ex.Message;
                state.CompletedAtUtc = DateTime.UtcNow.ToString("O");
                SaveState(state);
            }
        }

        private static bool IsEditorReady()
        {
            return !EditorApplication.isCompiling
                && !EditorApplication.isUpdating
                && !EditorApplication.isPlayingOrWillChangePlaymode
                && !TestRunStatus.IsRunning;
        }

        private static PendingResponse Pending(UnityScriptValidationState state, string message)
        {
            return new PendingResponse(
                message,
                pollIntervalSeconds: 1.0,
                data: new
                {
                    job_id = state.JobId,
                    phase = state.Phase,
                    refresh_requested = state.RefreshRequested,
                    compile_requested = state.CompileRequested,
                    compilation_started = state.CompilationStarted,
                    compilation_finished = state.CompilationFinished,
                    domain_reload_observed = state.DomainReloadObserved
                });
        }

        private static string DescribePendingState(UnityScriptValidationState state)
        {
            if (!state.RefreshRequested)
                return "Waiting to start script refresh.";
            if (!state.CompilationStarted)
                return "Waiting for Unity compilation to start.";
            if (!state.CompilationFinished)
                return "Unity is compiling scripts.";
            if (!state.DomainReloadObserved)
                return "Waiting for Unity domain reload.";
            return "Waiting for the Unity Editor to become ready.";
        }

        private static object BuildEvidence(
            UnityScriptValidationState state,
            bool editorReady,
            object consoleErrors)
        {
            return new
            {
                job_id = state.JobId,
                outcome = state.Outcome,
                refresh = new
                {
                    requested = state.RefreshRequested,
                    completed = state.RefreshCompleted,
                    started_at_utc = state.RefreshStartedAtUtc
                },
                compilation = new
                {
                    requested = state.CompileRequested,
                    started = state.CompilationStarted,
                    finished = state.CompilationFinished,
                    started_at_utc = state.CompilationStartedAtUtc,
                    finished_at_utc = state.CompilationFinishedAtUtc,
                    assembly_count = state.AssemblyCount,
                    errors = state.CompilationErrors.ToArray(),
                    warnings = state.CompilationWarnings.ToArray()
                },
                domain_reload_observed = state.DomainReloadObserved,
                editor_ready = editorReady,
                console = new
                {
                    error_count = state.ConsoleErrorCount,
                    errors = consoleErrors
                },
                request_error = state.RequestError,
                started_at_utc = state.StartedAtUtc,
                completed_at_utc = state.CompletedAtUtc
            };
        }

        internal static UnityScriptValidationState LoadState()
        {
            return McpJobStateStore.LoadState<UnityScriptValidationState>(StateKey);
        }

        internal static void SaveState(UnityScriptValidationState state)
        {
            McpJobStateStore.SaveState(StateKey, state);
        }

        internal static void ClearStateForTests()
        {
            McpJobStateStore.ClearState(StateKey);
        }
    }

    [InitializeOnLoad]
    internal static class UnityScriptValidationCompilationObserver
    {
        static UnityScriptValidationCompilationObserver()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            UnityScriptValidationState state = ValidateUnityScripts.LoadState();
            if (state != null
                && !state.Terminal
                && state.CompilationFinished
                && state.CompilationErrors.Count == 0
                && !state.DomainReloadObserved)
            {
                state.DomainReloadObserved = true;
                state.DomainReloadObservedAtUtc = DateTime.UtcNow.ToString("O");
                state.Phase = "waiting_for_editor_ready";
                ValidateUnityScripts.SaveState(state);
            }
        }

        private static void OnCompilationStarted(object context)
        {
            UnityScriptValidationState state = ValidateUnityScripts.LoadState();
            if (state == null || state.Terminal || !state.RefreshRequested)
                return;

            state.CompilationStarted = true;
            state.CompilationStartedAtUtc = DateTime.UtcNow.ToString("O");
            state.Phase = "compiling";
            ValidateUnityScripts.SaveState(state);
        }

        private static void OnAssemblyCompilationFinished(
            string assemblyPath,
            CompilerMessage[] messages)
        {
            UnityScriptValidationState state = ValidateUnityScripts.LoadState();
            if (state == null || state.Terminal || !state.CompilationStarted)
                return;

            state.AssemblyCount++;
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    CompilerMessage message = messages[i];
                    string text = FormatCompilerMessage(assemblyPath, message);
                    if (message.type == CompilerMessageType.Error)
                        state.CompilationErrors.Add(text);
                    else if (message.type == CompilerMessageType.Warning)
                        state.CompilationWarnings.Add(text);
                }
            }

            ValidateUnityScripts.SaveState(state);
        }

        private static void OnCompilationFinished(object context)
        {
            UnityScriptValidationState state = ValidateUnityScripts.LoadState();
            if (state == null || state.Terminal || !state.CompilationStarted)
                return;

            state.CompilationFinished = true;
            state.CompilationFinishedAtUtc = DateTime.UtcNow.ToString("O");
            state.Phase = state.CompilationErrors.Count == 0
                ? "waiting_for_domain_reload"
                : "compilation_failed";
            ValidateUnityScripts.SaveState(state);
        }

        private static string FormatCompilerMessage(string assemblyPath, CompilerMessage message)
        {
            return $"{assemblyPath}: {message.file}({message.line},{message.column}): "
                + $"{message.type}: {message.message}";
        }
    }

    internal enum UnityScriptValidationDecision
    {
        Pending,
        CompilationFailed,
        ConsoleErrors,
        Passed
    }

    internal static class UnityScriptValidationPolicy
    {
        internal static UnityScriptValidationDecision Evaluate(
            UnityScriptValidationState state,
            bool editorReady,
            int consoleErrorCount)
        {
            if (state == null
                || !state.RefreshCompleted
                || !state.CompileRequested
                || !state.CompilationStarted
                || !state.CompilationFinished)
            {
                return UnityScriptValidationDecision.Pending;
            }

            if (state.CompilationErrors.Count > 0)
                return UnityScriptValidationDecision.CompilationFailed;
            if (!state.DomainReloadObserved || !editorReady)
                return UnityScriptValidationDecision.Pending;
            return consoleErrorCount > 0
                ? UnityScriptValidationDecision.ConsoleErrors
                : UnityScriptValidationDecision.Passed;
        }
    }

    [Serializable]
    internal sealed class UnityScriptValidationState
    {
        [JsonProperty] public string JobId { get; set; }
        [JsonProperty] public string Phase { get; set; }
        [JsonProperty] public string Outcome { get; set; }
        [JsonProperty] public string StartedAtUtc { get; set; }
        [JsonProperty] public string RefreshStartedAtUtc { get; set; }
        [JsonProperty] public string CompilationStartedAtUtc { get; set; }
        [JsonProperty] public string CompilationFinishedAtUtc { get; set; }
        [JsonProperty] public string DomainReloadObservedAtUtc { get; set; }
        [JsonProperty] public string CompletedAtUtc { get; set; }
        [JsonProperty] public string RequestError { get; set; }
        [JsonProperty] public bool RefreshRequested { get; set; }
        [JsonProperty] public bool RefreshCompleted { get; set; }
        [JsonProperty] public bool CompileRequested { get; set; }
        [JsonProperty] public bool CompilationStarted { get; set; }
        [JsonProperty] public bool CompilationFinished { get; set; }
        [JsonProperty] public bool DomainReloadObserved { get; set; }
        [JsonProperty] public bool Terminal { get; set; }
        [JsonProperty] public int AssemblyCount { get; set; }
        [JsonProperty] public int ConsoleErrorCount { get; set; }
        [JsonProperty] public List<string> CompilationErrors { get; set; } = new List<string>();
        [JsonProperty] public List<string> CompilationWarnings { get; set; } = new List<string>();

        internal static UnityScriptValidationState Create(string jobId, DateTime now)
        {
            return new UnityScriptValidationState
            {
                JobId = jobId,
                Phase = "scheduled",
                Outcome = "pending",
                StartedAtUtc = now.ToString("O")
            };
        }

        internal bool IsTimedOut(DateTime now, int timeoutSeconds)
        {
            return DateTime.TryParse(StartedAtUtc, out DateTime started)
                && now.ToUniversalTime() - started.ToUniversalTime()
                    > TimeSpan.FromSeconds(timeoutSeconds);
        }
    }
}
