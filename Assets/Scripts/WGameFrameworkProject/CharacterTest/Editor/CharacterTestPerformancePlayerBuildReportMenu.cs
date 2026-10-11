using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.Profiling;
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
    public static class CharacterTestPerformancePlayerBuildReportMenu
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Build And Run Player Warm Run";
        public const string CharacterTestScenePath = "Assets/Scenes/CharacterTestScene.unity";
        // Unity clears project Temp when BatchMode exits. Keep reports and symbols
        // together in a unique machine-local output directory that survives exit.
        public static readonly string DefaultOutputRoot = Path.Combine(Path.GetTempPath(),
            "WGF-Warm-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        public static readonly string DefaultBuildDirectory = Path.Combine(DefaultOutputRoot, "Player");
        public const string DefaultAppBaseName = "CharacterTestPerformance";
        public const string DefaultWindowsAppName = DefaultAppBaseName + ".exe";
        public const string DefaultMacAppName = DefaultAppBaseName + ".app";
        public const string DefaultLinuxAppName = DefaultAppBaseName;
        public static readonly string DefaultReportDirectory = Path.Combine(DefaultOutputRoot, "Reports");
        public static readonly string DefaultEditorRunReportPath =
            Path.Combine(DefaultOutputRoot, "CharacterTestPerformancePlayerBuildRunReport.txt");

        private static CharacterTestPerformancePlayerBuildRun _activeRun;
        private static CharacterTestPerformancePlayerBuildResult _activeOverviewBuild;

        [MenuItem(MenuPath)]
        public static void BuildAndRunDefaultPlayerReport()
        {
            if (_activeRun != null && !_activeRun.IsDone)
            {
                Debug.LogWarning(
                    "CharacterTest Player performance build/run is already active. Report path: "
                    + _activeRun.EditorRunReportPath);
                return;
            }

            try
            {
                BuildTarget target = ResolveDefaultBuildTarget();
                Directory.CreateDirectory(DefaultOutputRoot);
                if (target == BuildTarget.StandaloneWindows64)
                {
                    string oldOverview = GetWarmRunOverviewPath();
                    if (File.Exists(oldOverview)) File.Delete(oldOverview);
                }
                string inputReceiptPath = ResolveReportPath(DefaultEditorRunReportPath) + ".inputs.txt";
                CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot inputs =
                    CharacterTestPackageTestCommand.CaptureReusedInputs(target, inputReceiptPath);
                CharacterTestPackageTestCommand.VerifyReusedInputs(
                    inputs, target, "beforeBuildPlayer", inputReceiptPath);
                CharacterTestPerformancePlayerBuildResult buildResult = BuildPlayer(
                    target,
                    DefaultBuildDirectory,
                    DefaultReportDirectory,
                    DefaultEditorRunReportPath,
                    configuration: CharacterTestWindowsBuildProfile.Development);
                CharacterTestPackageTestCommand.VerifyReusedInputs(
                    inputs, target, "afterBuildPlayer", inputReceiptPath);
                if (!buildResult.Succeeded)
                {
                    WriteWarmRunOverview(buildResult, "Failed", buildResult.Issue);
                    Debug.LogError("CharacterTest Player performance build failed: " + buildResult.Issue);
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                if (Application.isBatchMode)
                {
                    CharacterTestPerformancePlayerBuildRun run = CharacterTestPerformancePlayerBuildRun.RunSynchronous(
                        buildResult.EditorRunReportPath,
                        buildResult.AppPath,
                        buildResult.ExecutablePath,
                        buildResult.ReportDirectory,
                        buildResult.BuildSizeBytes,
                        buildResult.RuntimeCatalogArtifactReport);
                    WriteWarmRunOverview(buildResult, run.FinalVerdict.Passed ? "Passed" : "Failed",
                        run.FinalVerdict.Issue);
                    if (!run.FinalVerdict.Passed)
                    {
                        Debug.LogError("CharacterTest Player performance batch build/run failed: "
                            + run.FinalVerdict.Issue);
                        EditorApplication.Exit(1);
                        return;
                    }
                    Debug.Log("CharacterTest Player performance batch build/run finished. Report path: "
                        + buildResult.EditorRunReportPath);
                    EditorApplication.Exit(0);
                    return;
                }

                _activeRun = CharacterTestPerformancePlayerBuildRun.Start(
                    buildResult.EditorRunReportPath,
                    buildResult.AppPath,
                    buildResult.ExecutablePath,
                    buildResult.ReportDirectory,
                    buildResult.BuildSizeBytes,
                    buildResult.RuntimeCatalogArtifactReport);
                _activeOverviewBuild = buildResult;
                EditorApplication.update += CompleteActiveRunOverview;
                Debug.Log(
                    "CharacterTest Player performance build/run started. Report path: "
                    + buildResult.EditorRunReportPath);
            }
            catch (Exception ex)
            {
                EditorApplication.update -= CompleteActiveRunOverview;
                string editorRunReportPath = ResolveReportPath(DefaultEditorRunReportPath);
                WriteLaunchReport(editorRunReportPath, "launch-failed", null, null, null, ex.ToString());
                if (ResolveDefaultBuildTarget() == BuildTarget.StandaloneWindows64)
                {
                    try
                    {
                        CharacterTestPackageOverviewReport.Write(GetWarmRunOverviewPath(), null, false,
                            "Failed", "Exception", ex.Message, editorRunReportPath);
                    }
                    catch (Exception overviewEx)
                    {
                        Debug.LogError("CharacterTest overview report failed: " + overviewEx);
                    }
                }
                Debug.LogError("CharacterTest Player performance build/run failed: " + ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static string GetWarmRunOverviewPath() =>
            Path.Combine(ResolvePath(DefaultReportDirectory, DefaultReportDirectory),
                CharacterTestPackageOverviewReport.FileName);

        private static void WriteWarmRunOverview(
            CharacterTestPerformancePlayerBuildResult build,
            string acceptanceStatus,
            string issue)
        {
            if (build.BuildTarget != BuildTarget.StandaloneWindows64) return;
            string reportPath = GetWarmRunOverviewPath();
            CharacterTestPackageOverviewReport.Write(reportPath, build.BuildDirectory, build.Succeeded,
                acceptanceStatus, build.BuildResult, issue, build.EditorRunReportPath,
                ResolveReportPath(DefaultEditorRunReportPath) + ".inputs.txt",
                "Library/MxFramework/ResourceBundles/residue-report.txt");
            Debug.Log("CharacterTest Player overview report: " + reportPath);
        }

        private static void CompleteActiveRunOverview()
        {
            if (_activeRun == null || !_activeRun.IsDone) return;
            EditorApplication.update -= CompleteActiveRunOverview;
            try
            {
                WriteWarmRunOverview(_activeOverviewBuild,
                    _activeRun.FinalVerdict.Passed ? "Passed" : "Failed", _activeRun.FinalVerdict.Issue);
            }
            catch (Exception ex)
            {
                Debug.LogError("CharacterTest Player overview report failed: " + ex);
            }
        }

        public static string ResolveReportPath(string reportPath)
        {
            return ResolvePath(reportPath, DefaultEditorRunReportPath);
        }

        internal static string ResolvePath(string path, string fallbackPath)
        {
            string safePath = string.IsNullOrWhiteSpace(path)
                ? fallbackPath
                : path;
            return Path.GetFullPath(safePath);
        }

        internal static BuildTarget ResolveDefaultBuildTarget()
        {
#if UNITY_EDITOR_WIN
            return BuildTarget.StandaloneWindows64;
#elif UNITY_EDITOR_OSX
            return BuildTarget.StandaloneOSX;
#else
            return BuildTarget.StandaloneLinux64;
#endif
        }

        internal static void RequireActiveBuildTarget(BuildTarget expected)
        {
            ValidateActiveBuildTarget(expected, EditorUserBuildSettings.activeBuildTarget);
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(expected), expected))
                throw new InvalidOperationException("Unity build support module is unavailable for " + expected + ".");
        }

        internal static void ValidateActiveBuildTarget(BuildTarget expected, BuildTarget active)
        {
            if (expected == BuildTarget.NoTarget || active != expected)
                throw new InvalidOperationException("CharacterTest Player target mismatch. Expected " + expected
                    + ", Unity active target is " + active + ". Switch target before building.");
        }

        internal static string ResolveDefaultAppName(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows64:
                    return DefaultWindowsAppName;
                case BuildTarget.StandaloneOSX:
                    return DefaultMacAppName;
                case BuildTarget.StandaloneLinux64:
                    return DefaultLinuxAppName;
                default:
                    throw new NotSupportedException("Unsupported CharacterTest Player build target: " + buildTarget);
            }
        }

        internal static string ResolvePlayerLocationPath(string buildDirectory, BuildTarget buildTarget)
        {
            string safeDirectory = ResolvePath(buildDirectory, DefaultBuildDirectory);
            return Path.Combine(safeDirectory, ResolveDefaultAppName(buildTarget));
        }

        internal static string ResolvePlayerExecutable(string appPath, BuildTarget buildTarget)
        {
            if (string.IsNullOrWhiteSpace(appPath))
                throw new ArgumentException("Player app path is required.", nameof(appPath));

            switch (buildTarget)
            {
                case BuildTarget.StandaloneOSX:
                    return ResolveMacPlayerExecutable(appPath);
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                    if (File.Exists(appPath))
                        return appPath;
                    throw new FileNotFoundException("CharacterTest Player executable was not found.", appPath);
                default:
                    throw new NotSupportedException("Unsupported CharacterTest Player build target: " + buildTarget);
            }
        }

        internal static CharacterTestPerformancePlayerBuildResult BuildPlayer(
            BuildTarget buildTarget,
            string buildDirectoryPath,
            string reportDirectoryPath,
            string editorRunReportPath,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport artifactReport = null,
            CharacterTestWindowsBuildProfile configuration = CharacterTestWindowsBuildProfile.Release)
        {
            string editorRunReport = ResolvePath(editorRunReportPath, DefaultEditorRunReportPath);
            string buildDirectory = ResolvePath(buildDirectoryPath, DefaultBuildDirectory);
            string reportDirectory = ResolvePath(reportDirectoryPath, DefaultReportDirectory);
            string appPath = null;
            string configurationReceipt = CharacterTestWindowsBuildConfiguration.ReceiptPath(editorRunReport);
            if (File.Exists(configurationReceipt)) File.Delete(configurationReceipt);
            try
            {
                RequireActiveBuildTarget(buildTarget);
                if (buildTarget == BuildTarget.StandaloneWindows64)
                {
                    CharacterTestWindowsBuildConfiguration.RequireToolchain(configuration);
                    string streaming = Application.streamingAssetsPath;
                    CharacterTestWindowsBuildConfiguration.RequirePlayerPaths(buildDirectory,
                        System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(
                            Directory.GetFiles(streaming, "*", SearchOption.AllDirectories),
                            path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)),
                            path => Path.GetRelativePath(streaming, path)));
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before building CharacterTest Player.");
                CharacterTestCharacterActionPlayerArtifacts.RequireMirror();
                if (CharacterTestRuntimeCatalogMirrorValidator.ValidateDefault().HasIssues)
                    throw new InvalidOperationException(
                        "CharacterTest Runtime Catalog Player mirror is missing, changed or semantically invalid.");
                CharacterTestGuidanceSceneCacheFreshness.ThrowIfFormalSceneInvalid();
                CharacterTestAudioCatalogBuildPreflight.ThrowIfInvalid(Directory.GetCurrentDirectory());
                appPath = ResolvePlayerLocationPath(buildDirectory, buildTarget);
                PrepareOutputDirectory(buildDirectory);
                PrepareOutputDirectory(reportDirectory);
                WriteLaunchReport(
                    editorRunReport,
                    "building",
                    appPath,
                    reportDirectory,
                    null,
                    null,
                    buildTarget.ToString(),
                    null);
                WriteLaunchReport(
                    editorRunReport,
                    "building",
                    appPath,
                    reportDirectory,
                    null,
                    null,
                    buildTarget.ToString(),
                    null,
                    artifactReport);
                var resourcePlan = MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
                MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ValidatePublishedArtifacts(resourcePlan);
                UnityEditor.Build.Reporting.BuildReport buildReport;
                using (buildTarget == BuildTarget.StandaloneWindows64
                    ? new CharacterTestWindowsBuildConfiguration.Scope(configuration) : null)
                {
                    var options = new BuildPlayerOptions
                    {
                        scenes = new[] { CharacterTestScenePath },
                        locationPathName = appPath,
                        target = buildTarget,
                        assetBundleManifestPath = MxFramework.Editor.GlobalPlayerResourceBuildContext.RequirePlayerManifest(resourcePlan),
                        options = CharacterTestWindowsBuildConfiguration.Options(configuration)
                    };
                    using (new CharacterTestPlayerResourceInputs.PlayerScope(CharacterTestScenePath, Path.GetDirectoryName(editorRunReport)))
                        buildReport = BuildPipeline.BuildPlayer(options);
                    MxFramework.Editor.GlobalPlayerResourceBuildContext.CapturePlayer(buildReport);
                }

                UnityEditor.Build.Reporting.BuildSummary summary = buildReport.summary;
                if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    string buildResult = summary.result.ToString();
                    WriteLaunchReport(
                        editorRunReport,
                        "build-failed",
                        appPath,
                        reportDirectory,
                        buildResult,
                        null,
                        buildTarget.ToString(),
                        null);
                    return CharacterTestPerformancePlayerBuildResult.CreateFailed(
                        buildTarget,
                        editorRunReport,
                        buildDirectory,
                        appPath,
                        null,
                        reportDirectory,
                        buildResult,
                        "BuildPipeline result: " + buildResult,
                        artifactReport);
                }

                string executablePath = ResolvePlayerExecutable(appPath, buildTarget);
                if (buildTarget == BuildTarget.StandaloneWindows64)
                    CharacterTestWindowsBuildConfiguration.FinishBuild(buildReport, configuration,
                        buildDirectory, editorRunReport);
                WriteLaunchReport(
                    editorRunReport,
                    "build-succeeded",
                    appPath,
                    reportDirectory,
                    summary.result.ToString(),
                    null,
                    buildTarget.ToString(),
                    executablePath);
                return CharacterTestPerformancePlayerBuildResult.CreateSucceeded(
                    buildTarget,
                    editorRunReport,
                    buildDirectory,
                    appPath,
                    executablePath,
                    reportDirectory,
                    summary.totalSize,
                    artifactReport);
            }
            catch (Exception ex)
            {
                WriteLaunchReport(
                    editorRunReport,
                    "launch-failed",
                    appPath,
                    reportDirectory,
                    null,
                    ex.ToString(),
                    buildTarget.ToString(),
                    null);
                return CharacterTestPerformancePlayerBuildResult.CreateFailed(
                    buildTarget,
                    editorRunReport,
                    buildDirectory,
                    appPath,
                    null,
                    reportDirectory,
                    "Exception",
                    ex.Message);
            }
        }

        internal static void WriteLaunchReport(
            string editorRunReportPath,
            string status,
            string appPath,
            string reportDirectory,
            string buildResult,
            string issue,
            string buildTarget = null,
            string executablePath = null,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport artifactReport = null)
        {
            string directory = Path.GetDirectoryName(editorRunReportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var builder = new StringBuilder(1024);
            builder.AppendLine("scenario: character-test.player-development.editor-build-run");
            builder.Append("status: ").AppendLine(status);
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(buildTarget))
                builder.Append("buildTarget: ").AppendLine(buildTarget);
            if (!string.IsNullOrWhiteSpace(appPath))
                builder.Append("appPath: ").AppendLine(appPath);
            if (!string.IsNullOrWhiteSpace(executablePath))
                builder.Append("executablePath: ").AppendLine(executablePath);
            if (!string.IsNullOrWhiteSpace(reportDirectory))
                builder.Append("reportDirectory: ").AppendLine(reportDirectory);
            if (!string.IsNullOrWhiteSpace(buildResult))
                builder.Append("buildResult: ").AppendLine(buildResult);
            artifactReport?.AppendTo(builder, "runtimeCatalogArtifacts");
            if (!string.IsNullOrWhiteSpace(issue))
                builder.Append("issue: ").AppendLine(issue.Replace('\n', ' '));
            File.WriteAllText(editorRunReportPath, builder.ToString());
        }

        internal static void PrepareOutputDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return;

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
        }

        private static string ResolveMacPlayerExecutable(string appPath)
        {
            string macOsDirectory = Path.Combine(appPath, "Contents", "MacOS");
            if (!Directory.Exists(macOsDirectory))
                throw new DirectoryNotFoundException(macOsDirectory);

            string[] files = Directory.GetFiles(macOsDirectory);
            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i]))
                    return files[i];
            }

            throw new FileNotFoundException("CharacterTest Player executable was not found.", macOsDirectory);
        }
    }

    internal readonly struct CharacterTestPerformancePlayerBuildResult
    {
        private CharacterTestPerformancePlayerBuildResult(
            bool succeeded,
            BuildTarget buildTarget,
            string editorRunReportPath,
            string buildDirectory,
            string appPath,
            string executablePath,
            string reportDirectory,
            string buildResult,
            ulong buildSizeBytes,
            string issue,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport runtimeCatalogArtifactReport)
        {
            Succeeded = succeeded;
            BuildTarget = buildTarget;
            EditorRunReportPath = editorRunReportPath;
            BuildDirectory = buildDirectory;
            AppPath = appPath;
            ExecutablePath = executablePath;
            ReportDirectory = reportDirectory;
            BuildResult = buildResult;
            BuildSizeBytes = buildSizeBytes;
            Issue = issue;
            RuntimeCatalogArtifactReport = runtimeCatalogArtifactReport;
        }

        public bool Succeeded { get; }
        public BuildTarget BuildTarget { get; }
        public string EditorRunReportPath { get; }
        public string BuildDirectory { get; }
        public string AppPath { get; }
        public string ExecutablePath { get; }
        public string ReportDirectory { get; }
        public string BuildResult { get; }
        public ulong BuildSizeBytes { get; }
        public string Issue { get; }
        public CharacterTestRuntimeCatalogPlayerArtifactExportReport RuntimeCatalogArtifactReport { get; }

        public static CharacterTestPerformancePlayerBuildResult CreateSucceeded(
            BuildTarget buildTarget,
            string editorRunReportPath,
            string buildDirectory,
            string appPath,
            string executablePath,
            string reportDirectory,
            ulong buildSizeBytes,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport runtimeCatalogArtifactReport = null)
        {
            return new CharacterTestPerformancePlayerBuildResult(
                true,
                buildTarget,
                editorRunReportPath,
                buildDirectory,
                appPath,
                executablePath,
                reportDirectory,
                "Succeeded",
                buildSizeBytes,
                null,
                runtimeCatalogArtifactReport);
        }

        public static CharacterTestPerformancePlayerBuildResult CreateFailed(
            BuildTarget buildTarget,
            string editorRunReportPath,
            string buildDirectory,
            string appPath,
            string executablePath,
            string reportDirectory,
            string buildResult,
            string issue,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport runtimeCatalogArtifactReport = null)
        {
            return new CharacterTestPerformancePlayerBuildResult(
                false,
                buildTarget,
                editorRunReportPath,
                buildDirectory,
                appPath,
                executablePath,
                reportDirectory,
                buildResult,
                0UL,
                issue,
                runtimeCatalogArtifactReport);
        }
    }
}
