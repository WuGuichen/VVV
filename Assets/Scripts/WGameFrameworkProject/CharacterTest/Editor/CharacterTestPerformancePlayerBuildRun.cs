using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal sealed class CharacterTestPerformancePlayerBuildRun
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan QuitGracePeriod = TimeSpan.FromSeconds(5);

        private readonly System.Diagnostics.Process _process;
        private readonly string _appPath;
        private readonly string _executablePath;
        private readonly string _reportDirectory;
        private readonly ulong _buildSizeBytes;
        private readonly CharacterTestRuntimeCatalogPlayerArtifactExportReport _artifactReport;
        private readonly DateTime _startedUtc;
        private DateTime? _playerRunReportDetectedUtc;

        private CharacterTestPerformancePlayerBuildRun(
            string editorRunReportPath,
            string appPath,
            string executablePath,
            string reportDirectory,
            ulong buildSizeBytes,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport artifactReport,
            System.Diagnostics.Process process)
        {
            EditorRunReportPath = editorRunReportPath;
            _appPath = appPath;
            _executablePath = executablePath;
            _reportDirectory = reportDirectory;
            _buildSizeBytes = buildSizeBytes;
            _artifactReport = artifactReport;
            _process = process;
            _startedUtc = DateTime.UtcNow;
        }

        public bool IsDone { get; private set; }
        public string EditorRunReportPath { get; }
        public string CompletionStatus { get; private set; }
        public string CompletionIssue { get; private set; }
        public CharacterTestPackageTestPlayerRunVerdict FinalVerdict { get; private set; }

        public static CharacterTestPerformancePlayerBuildRun Start(
            string editorRunReportPath,
            string appPath,
            string executablePath,
            string reportDirectory,
            ulong buildSizeBytes,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport artifactReport)
        {
            System.Diagnostics.Process process = StartProcess(executablePath, reportDirectory);
            var run = new CharacterTestPerformancePlayerBuildRun(
                editorRunReportPath,
                appPath,
                executablePath,
                reportDirectory,
                buildSizeBytes,
                artifactReport,
                process);
            EditorApplication.update += run.Poll;
            run.WriteEditorRunReport("running", null);
            return run;
        }

        public static CharacterTestPerformancePlayerBuildRun RunSynchronous(
            string editorRunReportPath,
            string appPath,
            string executablePath,
            string reportDirectory,
            ulong buildSizeBytes,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport artifactReport)
        {
            System.Diagnostics.Process process = StartProcess(executablePath, reportDirectory);
            var run = new CharacterTestPerformancePlayerBuildRun(
                editorRunReportPath,
                appPath,
                executablePath,
                reportDirectory,
                buildSizeBytes,
                artifactReport,
                process);
            run.WriteEditorRunReport("running", null);
            while (!run.IsDone)
            {
                run.Poll();
                if (!run.IsDone)
                    System.Threading.Thread.Sleep(250);
            }

            return run;
        }

        private static System.Diagnostics.Process StartProcess(
            string executablePath,
            string reportDirectory)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = CharacterTestPerformancePlayerReportRunner.ReportDirectoryArgument
                    + " "
                    + QuoteArgument(reportDirectory),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("CharacterTest Player process did not start.");

            return process;
        }

        private void Poll()
        {
            if (IsDone)
                return;

            string playerRunReportPath = PlayerRunReportPath;
            bool playerRunReportExists = File.Exists(playerRunReportPath);
            if (playerRunReportExists && !_playerRunReportDetectedUtc.HasValue)
                _playerRunReportDetectedUtc = DateTime.UtcNow;

            if (playerRunReportExists && HasExited())
            {
                string issue;
                WriteEditorRunReport(ResolvePlayerRunCompletionStatus("finished", out issue), issue);
                Finish(killProcess: false);
                return;
            }

            if (playerRunReportExists
                && _playerRunReportDetectedUtc.HasValue
                && DateTime.UtcNow - _playerRunReportDetectedUtc.Value >= QuitGracePeriod)
            {
                string issue;
                WriteEditorRunReport(
                    ResolvePlayerRunCompletionStatus("finished-player-still-running-killed", out issue),
                    issue);
                Finish(killProcess: true);
                return;
            }

            if (HasExited())
            {
                WriteEditorRunReport("process-exited-without-player-report", null);
                Finish(killProcess: false);
                return;
            }

            if (DateTime.UtcNow - _startedUtc >= Timeout)
            {
                WriteEditorRunReport("timeout", "Player did not finish within the build-run timeout.");
                Finish(killProcess: true);
            }
        }

        private string PlayerRunReportPath =>
            Path.Combine(_reportDirectory, CharacterTestPerformancePlayerReportRunner.RunReportFileName);

        private string TimingReportPath =>
            Path.Combine(_reportDirectory, CharacterTestPerformancePlayerReportRunner.TimingReportFileName);

        private string ProfilerRecorderReportPath =>
            Path.Combine(_reportDirectory, CharacterTestPerformancePlayerReportRunner.ProfilerRecorderReportFileName);

        private string ResolvePlayerRunCompletionStatus(string defaultStatus, out string issue)
        {
            issue = null;
            int exitCode = ExitCodeOrDefault();
            if (exitCode != 0)
            {
                issue = "Player exited with code " + exitCode.ToString(CultureInfo.InvariantCulture) + ".";
                return "process-exited-failed";
            }
            CharacterTestRuntimeCatalogLoadReportSummary summary;
            if (!TryReadPlayerRuntimeCatalogSummary(out summary, out string readIssue))
            {
                issue = readIssue;
                return "finished-binary-catalog-miss";
            }

            if (summary.AllExpectedBinary)
                return defaultStatus;

            string nonBinaryKinds = FormatKinds(summary.NonBinaryExpectedKinds);
            string missingKinds = FormatKinds(summary.MissingExpectedKinds);
            issue = "Expected CharacterTest Player runtime catalogs to load from Binary. nonBinary="
                + nonBinaryKinds
                + " missing="
                + missingKinds;
            return "finished-binary-catalog-miss";
        }

        private bool TryReadPlayerRuntimeCatalogSummary(
            out CharacterTestRuntimeCatalogLoadReportSummary summary,
            out string issue)
        {
            summary = default;
            issue = null;
            if (!File.Exists(PlayerRunReportPath))
            {
                issue = "Player run report is missing.";
                return false;
            }

            try
            {
                summary = CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(File.ReadAllText(PlayerRunReportPath));
                return true;
            }
            catch (Exception ex)
            {
                issue = "Player runtime catalog load report parse failed: " + ex.Message;
                return false;
            }
        }

        private bool HasExited()
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private int ExitCodeOrDefault()
        {
            try
            {
                return _process.HasExited ? _process.ExitCode : -1;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        /// <remarks>AllocByDesign: editor-only Player build/run evidence report generation.</remarks>
        private void WriteEditorRunReport(string status, string issue)
        {
            CompletionStatus = status;
            CompletionIssue = issue;
            string directory = Path.GetDirectoryName(EditorRunReportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var builder = new StringBuilder(1536);
            builder.AppendLine("scenario: character-test.player-development.editor-build-run");
            builder.Append("status: ").AppendLine(status);
            builder.Append("startedUtc: ").AppendLine(_startedUtc.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("updatedUtc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("durationSeconds: ")
                .AppendLine((DateTime.UtcNow - _startedUtc).TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.AppendLine("buildResult: Succeeded");
            CharacterTestWindowsBuildConfiguration.AppendReceiptFields(builder, EditorRunReportPath);
            builder.Append("buildSizeBytes: ").AppendLine(_buildSizeBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append("appPath: ").AppendLine(_appPath);
            builder.Append("executablePath: ").AppendLine(_executablePath);
            builder.Append("reportDirectory: ").AppendLine(_reportDirectory);
            builder.Append("playerRunReport: ").AppendLine(PlayerRunReportPath);
            builder.Append("timingReport: ").AppendLine(TimingReportPath);
            builder.Append("profilerRecorderReport: ").AppendLine(ProfilerRecorderReportPath);
            builder.Append("processExited: ").AppendLine(HasExited() ? "true" : "false");
            builder.Append("processExitCode: ").AppendLine(ExitCodeOrDefault().ToString(CultureInfo.InvariantCulture));
            builder.Append("playerRunReportExists: ").AppendLine(File.Exists(PlayerRunReportPath) ? "true" : "false");
            builder.Append("timingReportExists: ").AppendLine(File.Exists(TimingReportPath) ? "true" : "false");
            builder.Append("profilerRecorderReportExists: ")
                .AppendLine(File.Exists(ProfilerRecorderReportPath) ? "true" : "false");
            _artifactReport?.AppendTo(builder, "runtimeCatalogArtifacts");
            if (TryReadPlayerRuntimeCatalogSummary(
                    out CharacterTestRuntimeCatalogLoadReportSummary catalogSummary,
                    out string catalogSummaryIssue))
            {
                builder.Append("runtimeCatalogGate.expectedCount: ")
                    .AppendLine(catalogSummary.ExpectedCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("runtimeCatalogGate.binaryCount: ")
                    .AppendLine(catalogSummary.BinaryCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("runtimeCatalogGate.jsonCount: ")
                    .AppendLine(catalogSummary.JsonCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("runtimeCatalogGate.embeddedCount: ")
                    .AppendLine(catalogSummary.EmbeddedCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("runtimeCatalogGate.missingExpectedKinds: ")
                    .AppendLine(FormatKinds(catalogSummary.MissingExpectedKinds));
                builder.Append("runtimeCatalogGate.nonBinaryExpectedKinds: ")
                    .AppendLine(FormatKinds(catalogSummary.NonBinaryExpectedKinds));
                builder.Append("runtimeCatalogGate.allExpectedBinary: ")
                    .AppendLine(catalogSummary.AllExpectedBinary ? "true" : "false");
            }
            else if (!string.IsNullOrWhiteSpace(catalogSummaryIssue))
            {
                builder.Append("runtimeCatalogGate.issue: ")
                    .AppendLine(catalogSummaryIssue.Replace('\n', ' '));
            }

            if (!string.IsNullOrWhiteSpace(issue))
                builder.Append("issue: ").AppendLine(issue.Replace('\n', ' '));
            File.WriteAllText(EditorRunReportPath, builder.ToString());
        }

        private void Finish(bool killProcess)
        {
            IsDone = true;
            EditorApplication.update -= Poll;
            if (killProcess && !HasExited())
            {
                try
                {
                    _process.Kill();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("CharacterTest Player process kill failed: " + ex.Message);
                }
            }

            try
            {
                FinalVerdict = CharacterTestPackageTestCommand.EvaluatePlayerRunReports(
                    _reportDirectory, EditorRunReportPath);
            }
            catch (Exception ex)
            {
                FinalVerdict = CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    "Exception", "Player acceptance report is invalid: " + ex.Message,
                    PlayerRunReportPath, TimingReportPath, ProfilerRecorderReportPath);
            }
            File.AppendAllText(EditorRunReportPath,
                "acceptanceStatus: " + (FinalVerdict.Passed ? "Passed" : "Failed") + Environment.NewLine
                + (string.IsNullOrWhiteSpace(FinalVerdict.Issue) ? string.Empty
                    : "acceptanceIssue: " + FinalVerdict.Issue.Replace('\n', ' ') + Environment.NewLine));
            if (!FinalVerdict.Passed)
                Debug.LogError("CharacterTest Player acceptance failed: " + FinalVerdict.Issue);

            _process.Dispose();
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string FormatKinds(string[] kinds)
        {
            return kinds == null || kinds.Length == 0 ? "none" : string.Join(",", kinds);
        }
    }
}
