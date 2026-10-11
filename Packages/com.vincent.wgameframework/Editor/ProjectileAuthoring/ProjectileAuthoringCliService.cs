using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Asynchronous process boundary to the noEngine authoring authority.</summary>
    public sealed class ProjectileAuthoringCliService : IProjectileAuthoringService
    {
        private const string UnavailableErrorPrefix = "PROJAUTH_CLI_UNAVAILABLE:";

        private Process process;
        private EditorApplication.CallbackFunction pendingCompletion;
        private readonly StringBuilder stdout = new StringBuilder();
        private readonly StringBuilder stderr = new StringBuilder();
        private readonly object outputGate = new object();
        private bool stdoutDrained;
        private bool stderrDrained;
        private string temporaryJobDirectory;
        private readonly HashSet<string> retainedJobDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsBusy => process != null;

        public void LoadWorkspace(string packagePath, Action<ProjectileAuthoringWorkspaceView, string> completed)
        {
            Run("projectile workspace --package " + Quote(packagePath) + VfxDefinitionsArgument(), text => completed(Parse<ProjectileAuthoringWorkspaceView>(text), null), error => completed(null, error));
        }

        public void Validate(string packagePath, Action<ProjectileAuthoringProblemView[], string> completed)
        {
            Run("projectile workspace --package " + Quote(packagePath) + VfxDefinitionsArgument(), text => completed(Parse<ProjectileAuthoringWorkspaceView>(text)?.diagnostics ?? Array.Empty<ProjectileAuthoringProblemView>(), null), error => completed(Array.Empty<ProjectileAuthoringProblemView>(), error));
        }

        public void Mutate(string packagePath, ProjectileAuthoringMutationRequestView request, Action<ProjectileAuthoringMutationResultView, string, string> completed)
        {
            MutateSequence(packagePath, new[] { request }, completed);
        }

        public void MutateSequence(string packagePath, ProjectileAuthoringMutationRequestView[] commands, Action<ProjectileAuthoringMutationResultView, string, string> completed)
        {
            string root = CreateJobDirectory();
            string requestPath = Path.Combine(root, "mutation.json");
            string draftPath = Path.Combine(root, "draft.json");
            var sequence = new ProjectileAuthoringMutationSequenceView { commands = commands ?? Array.Empty<ProjectileAuthoringMutationRequestView>() };
            File.WriteAllText(requestPath, JsonUtility.ToJson(sequence, true), new UTF8Encoding(false));
            bool retainDraft = commands != null && commands.Length > 0 && !commands[commands.Length - 1].planOnly;
            Run("projectile mutate --package " + Quote(packagePath) + " --request " + Quote(requestPath) + " --draft-out " + Quote(draftPath) + VfxDefinitionsArgument(), text => completed(Parse<ProjectileAuthoringMutationResultView>(text), retainDraft ? draftPath : string.Empty, null), error => completed(null, string.Empty, error), root, retainDraft);
        }

        public void Apply(string packagePath, string draftPath, Action<ProjectileAuthoringApplyResult, string> completed)
        {
            string generated = Path.Combine(packagePath, "generated");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Run("projectile apply --package " + Quote(packagePath) + " --draft " + Quote(draftPath) + " --out " + Quote(generated) + " --project-root " + Quote(projectRoot) + VfxDefinitionsArgument(), text => { completed(Parse<ProjectileAuthoringApplyResult>(text), null); CleanupRetainedDirectories(); }, error => { completed(null, error); });
        }

        private void Run(string arguments, Action<string> success, Action<string> failure, string jobDirectory = null, bool retainJobOnSuccess = false)
        {
            if (IsBusy) { failure("Projectile authoring CLI is busy."); return; }
            stdout.Length = 0; stderr.Length = 0; stdoutDrained = false; stderrDrained = false;
            temporaryJobDirectory = jobDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string cliProject = Path.Combine(projectRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj");
            if (!File.Exists(cliProject))
            {
                CleanupTemporaryJobDirectory();
                failure(
                    UnavailableErrorPrefix
                    + " Projectile Authoring requires an explicitly configured Authoring CLI host.");
                return;
            }

            process = new Process { StartInfo = new ProcessStartInfo("dotnet", "run --project " + Quote(cliProject) + " -- " + arguments) { WorkingDirectory = projectRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            process.OutputDataReceived += (_, e) => { lock (outputGate) { if (e.Data == null) stdoutDrained = true; else stdout.AppendLine(e.Data); } };
            process.ErrorDataReceived += (_, e) => { lock (outputGate) { if (e.Data == null) stderrDrained = true; else stderr.AppendLine(e.Data); } };
            process.EnableRaisingEvents = true;
            pendingCompletion = () =>
            {
                if (process == null || !process.HasExited) return;
                lock (outputGate) { if (!stdoutDrained || !stderrDrained) return; }
                EditorApplication.update -= pendingCompletion;
                int code = process.ExitCode; process.Dispose(); process = null;
                string output; string error; lock (outputGate) { output = stdout.ToString(); error = stderr.ToString(); }
                pendingCompletion = null;
                if (code == 0 && retainJobOnSuccess && !string.IsNullOrEmpty(temporaryJobDirectory))
                {
                    retainedJobDirectories.Add(temporaryJobDirectory);
                    temporaryJobDirectory = null;
                }
                else CleanupTemporaryJobDirectory();
                if (code == 0) success(output); else failure(string.IsNullOrWhiteSpace(error) ? output : error);
            };
            try { process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); EditorApplication.update += pendingCompletion; }
            catch (Exception ex) { process?.Dispose(); process = null; pendingCompletion = null; CleanupTemporaryJobDirectory(); UnityEngine.Debug.LogException(ex); failure(ex.Message); }
        }

        public void Cancel()
        {
            if (pendingCompletion != null) EditorApplication.update -= pendingCompletion;
            pendingCompletion = null;
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
                process.Dispose(); process = null;
            }
            CleanupTemporaryJobDirectory();
            CleanupRetainedDirectories();
        }

        public void Dispose() => Cancel();
        internal static bool IsUnavailableError(string error)
        {
            return !string.IsNullOrEmpty(error)
                && error.StartsWith(UnavailableErrorPrefix, StringComparison.Ordinal);
        }

        private static string VfxDefinitionsArgument()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json");
            return " --vfx-definitions " + Quote(path);
        }
        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        private static T Parse<T>(string json) where T : class { int start = json.IndexOf('{'); int end = json.LastIndexOf('}'); return start >= 0 && end >= start ? JsonUtility.FromJson<T>(json.Substring(start, end - start + 1)) : null; }
        private static string CreateJobDirectory() { string path = Path.Combine(Path.GetTempPath(), "MxFramework.ProjectileAuthoring.Editor", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
        private void CleanupTemporaryJobDirectory()
        {
            string path = temporaryJobDirectory; temporaryJobDirectory = null;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            try { Directory.Delete(path, true); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[Projectile Authoring] Failed to remove temporary job directory: " + ex.Message); }
        }

        private void CleanupRetainedDirectories()
        {
            foreach (string path in retainedJobDirectories)
            {
                if (!Directory.Exists(path)) continue;
                try { Directory.Delete(path, true); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[Projectile Authoring] Failed to remove retained job directory: " + ex.Message); }
            }
            retainedJobDirectories.Clear();
        }
    }
}
