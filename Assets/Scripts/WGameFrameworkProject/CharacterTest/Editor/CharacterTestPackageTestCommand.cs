using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json.Linq;
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
    public static class CharacterTestPackageTestCommand
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Run Windows Package Test";
        public static readonly string DefaultOutputRoot = Path.Combine(Path.GetTempPath(),
            "WGF-Pkg-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        public const string SummaryFileName = "CharacterTestPackageTestSummary.txt";
        public const string OutputRootArgument = "-character-test-package-test-output";
        public const string SkipResourceBuildArgument = "-character-test-package-test-skip-resource-build";
        public const string NoRunArgument = "-character-test-package-test-no-run";
        private const string GeneratedPlayerContentRoot = "Assets/StreamingAssets/MxFramework/Generated";
        private static readonly string[] RequiredDefaultCharacterGeneratedPackageFiles =
        {
            "config/animation_set_definition.json",
            "config/animation_clip_registry.json",
            "config/character_config_patch.json",
            "config/geometry_binding.json",
            "config/resource_catalog_mapping.json",
            "config/unity_resource_catalog.json",
            "package_cache/import_report.json"
        };

        /// <summary>
        /// 不进入 Player StreamingAssets 暂存的导出产物：已废弃的完整编译快照与轻量摘要。
        /// 只按规范化后的完整包相对路径逐项匹配，不做文件名通配、不做整目录过滤。
        /// </summary>
        private static readonly string[] ExcludedPlayerGeneratedPackageFiles =
        {
            "package_cache/compile_result.json",
            "package_cache/animation_compile_result.json",
            "package_cache/compile_summary.json",
            "generated/character_application_config_patch.json",
            "generated/character_geometry_binding.json",
            "generated/character_resource_mapping.json"
        };

        [MenuItem(MenuPath)]
        public static void RunDefaultPackageTest()
        {
            CharacterTestPackageTestResult result = Run(
                ResolveOptionsFromCommandLine(Environment.GetCommandLineArgs()));
            if (result.Passed)
            {
                Debug.Log("CharacterTest package test passed. Summary: " + result.SummaryPath);
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("CharacterTest package test failed: " + result.Issue);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        [MenuItem("MxFramework/Build/Windows64/Build Release And Verify")]
        public static void RunReleasePackageTest() => RunConfiguredPackageTest(CharacterTestWindowsBuildProfile.Release);

        [MenuItem("MxFramework/Build/Windows64/Build Development And Verify")]
        public static void RunDevelopmentPackageTest() => RunConfiguredPackageTest(CharacterTestWindowsBuildProfile.Development);

        private static void RunConfiguredPackageTest(CharacterTestWindowsBuildProfile profile)
        {
            var result = Run(new CharacterTestPackageTestOptions(
                Path.Combine(DefaultOutputRoot, profile.ToString()), false, false, profile));
            if (result.Passed) Debug.Log("Windows IL2CPP " + profile + " passed: " + result.SummaryPath);
            else Debug.LogError("Windows IL2CPP " + profile + " failed: " + result.Issue);
        }

        /// <remarks>AllocByDesign: command-line diagnostics option parsing.</remarks>
        internal static CharacterTestPackageTestOptions ResolveOptionsFromCommandLine(string[] args)
        {
            TryGetArgumentValue(args, OutputRootArgument, out string outputRoot);
            TryGetArgumentValue(args, CharacterTestWindowsBuildConfiguration.Argument, out string configuration);
            return new CharacterTestPackageTestOptions(
                string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot,
                HasArgument(args, SkipResourceBuildArgument),
                HasArgument(args, NoRunArgument), CharacterTestWindowsBuildConfiguration.Parse(configuration));
        }

        /// <remarks>AllocByDesign: Editor package build/test orchestration and report generation.</remarks>
        internal static CharacterTestPackageTestResult Run(CharacterTestPackageTestOptions options)
        {
            string outputRoot = CharacterTestPerformancePlayerBuildReportMenu.ResolvePath(
                options.OutputRoot,
                DefaultOutputRoot);
            using var resourceBuildContext = new MxFramework.Editor.GlobalPlayerResourceBuildContext.Scope(options.Configuration.ToString(), outputRoot);
            using var configurationScope = new CharacterTestWindowsBuildConfiguration.Scope(options.Configuration);
            string summaryPath = Path.Combine(outputRoot, SummaryFileName);
            string overviewPath = Path.Combine(outputRoot, CharacterTestPackageOverviewReport.FileName);
            if (File.Exists(overviewPath)) File.Delete(overviewPath);
            string previousConfiguration = CharacterTestWindowsBuildConfiguration.ReceiptPath(
                Path.Combine(outputRoot, "CharacterTestPerformancePlayerBuildRunReport.txt"));
            if (File.Exists(previousConfiguration)) File.Delete(previousConfiguration);
            BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
            string resourceBuildResult = "NotRun";
            string inputReceiptPath = Path.Combine(outputRoot, "CharacterTestPackageInputReceipt.txt");
            string attemptId = Guid.NewGuid().ToString("N");
            CharacterTestPerformancePlayerBuildResult playerBuildResult =
                CharacterTestPerformancePlayerBuildResult.CreateFailed(
                    buildTarget,
                    Path.Combine(outputRoot, "CharacterTestPerformancePlayerBuildRunReport.txt"),
                    Path.Combine(outputRoot, "Player"),
                    null,
                    null,
                    Path.Combine(outputRoot, "Reports"),
                    "NotRun",
                    "Player build did not run.");
            CharacterTestPackageTestPlayerRunVerdict playerRunVerdict =
                CharacterTestPackageTestPlayerRunVerdict.CreateSkipped("Player run did not start.");
            bool passed = false;
            string issue = null;
            try
            {
                CharacterTestPerformancePlayerBuildReportMenu.RequireActiveBuildTarget(buildTarget);
                CharacterTestWindowsBuildConfiguration.RequireToolchain(options.Configuration);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before preparing CharacterTest package inputs.");
                Directory.CreateDirectory(outputRoot);
                CharacterTestWindowsBuildConfiguration.RequireExperimentPolicy(options.Configuration);
                CharacterTestJsonPreservation.RequireCurrent(outputRoot);
                CharacterTestPlayerResourceInputs.PublishPrefabs();
                MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan resourcePlan =
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(
                        MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
                if (resourcePlan.HasErrors)
                    throw new InvalidOperationException(
                        MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateReportText(resourcePlan.Report));
                ThrowIfRelevantDirtyScenes(resourcePlan);
                CharacterTestPlayerCharacterPackageSelection selectedCharacters =
                    CharacterTestPlayerCharacterPackageSelection.Resolve(validateSceneCache: true);
                CharacterTestPackageInputSnapshot before = CharacterTestPackageInputSnapshot.CaptureSources(
                    resourcePlan, selectedCharacters);
                Directory.CreateDirectory(outputRoot);
                WriteInputReceipt(inputReceiptPath, attemptId, "beforePreparation", buildTarget, before);
                if (options.SkipResourceBuild)
                {
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ValidatePublishedArtifacts(resourcePlan);
                    resourceBuildResult = "ReusedValidated";
                }

                if (!options.SkipResourceBuild)
                {
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.Build(resourcePlan);
                    resourceBuildResult = "Succeeded";
                }

                foreach (CharacterTestPlayerCharacterPackageSelection.Package selected in selectedCharacters.Packages)
                    Debug.Log("CharacterTest generated Player content staged: "
                        + StageGeneratedPlayerContent(selected.SourceDirectory));
                CharacterTestRuntimeCatalogPlayerArtifactExportReport artifactReport =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.RefreshDefault();
                if (artifactReport.HasBlockingIssues)
                    throw new InvalidOperationException("Runtime Catalog mirror preparation failed: "
                        + string.Join(" | ", artifactReport.Issues));
                CharacterTestCharacterActionPlayerArtifacts.Refresh();
                AssetDatabase.Refresh();
                CharacterTestPackageInputSnapshot prepared = CharacterTestPackageInputSnapshot.CapturePrepared(buildTarget);
                before.RequireSameSources(prepared, "preparation");
                WriteInputReceipt(inputReceiptPath, attemptId, "afterPreparation", buildTarget, prepared);
                CharacterTestPackageInputSnapshot preBuild = CharacterTestPackageInputSnapshot.CapturePrepared(buildTarget);
                prepared.RequireSameAll(preBuild, "before BuildPlayer");
                WriteInputReceipt(inputReceiptPath, attemptId, "beforeBuildPlayer", buildTarget, preBuild);

                string buildDirectory = Path.Combine(outputRoot, "Player");
                string reportDirectory = Path.Combine(outputRoot, "Reports");
                string editorRunReportPath = Path.Combine(outputRoot, "CharacterTestPerformancePlayerBuildRunReport.txt");
                playerBuildResult = CharacterTestPerformancePlayerBuildReportMenu.BuildPlayer(
                    buildTarget,
                    buildDirectory,
                    reportDirectory,
                    editorRunReportPath,
                    artifactReport, options.Configuration);
                CharacterTestPackageInputSnapshot postBuild = CharacterTestPackageInputSnapshot.CapturePrepared(buildTarget);
                preBuild.RequireSameAll(postBuild, "after BuildPlayer");
                WriteInputReceipt(inputReceiptPath, attemptId, "afterBuildPlayer", buildTarget, postBuild);
                if (!playerBuildResult.Succeeded)
                {
                    issue = playerBuildResult.Issue;
                    playerRunVerdict = CharacterTestPackageTestPlayerRunVerdict.CreateSkipped(
                        "Player run skipped because build failed.");
                }
                else if (options.NoRun)
                {
                    passed = true;
                    playerRunVerdict = CharacterTestPackageTestPlayerRunVerdict.CreateSkipped(
                        "Player run skipped by command-line option.");
                }
                else
                {
                    CharacterTestPerformancePlayerBuildRun run = CharacterTestPerformancePlayerBuildRun.RunSynchronous(
                        playerBuildResult.EditorRunReportPath,
                        playerBuildResult.AppPath,
                        playerBuildResult.ExecutablePath,
                        playerBuildResult.ReportDirectory,
                        playerBuildResult.BuildSizeBytes,
                        playerBuildResult.RuntimeCatalogArtifactReport);
                    playerRunVerdict = run.FinalVerdict;
                    passed = playerRunVerdict.Passed;
                    issue = playerRunVerdict.Issue;
                }
            }
            catch (Exception ex)
            {
                issue = ex.Message;
                if (resourceBuildResult == "NotRun")
                    resourceBuildResult = "Failed";
                playerRunVerdict = CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    "Exception",
                    ex.Message,
                    null,
                    null,
                    null);
            }
            string overviewStatus = "Succeeded";
            try
            {
                CharacterTestPackageOverviewReport.Write(
                    overviewPath,
                    playerBuildResult.BuildDirectory,
                    playerBuildResult.Succeeded,
                    options.NoRun && passed ? "BuildOnly" : passed ? "Passed" : "Failed",
                    playerBuildResult.BuildResult,
                    issue,
                    playerBuildResult.EditorRunReportPath,
                    inputReceiptPath,
                    "Library/MxFramework/ResourceBundles/residue-report.txt");
            }
            catch (Exception ex)
            {
                overviewStatus = "Failed";
                passed = false;
                issue = (string.IsNullOrWhiteSpace(issue) ? string.Empty : issue + " | ")
                    + "Package overview report failed: " + ex.Message;
            }
            CharacterTestPackageTestResult result = new CharacterTestPackageTestResult(
                passed,
                summaryPath,
                outputRoot,
                resourceBuildResult,
                playerBuildResult,
                playerRunVerdict,
                issue,
                overviewPath,
                overviewStatus,
                options.NoRun,
                options.Configuration);
            WriteSummary(result);
            return result;
        }

        internal static CharacterTestPackageTestPlayerRunVerdict EvaluatePlayerRunReports(
            string reportDirectory, string editorRunReportPath = null)
        {
            string expectedDevelopment = null;
            string safeReportDirectory = CharacterTestPerformancePlayerBuildReportMenu.ResolvePath(
                reportDirectory,
                Path.Combine(DefaultOutputRoot, "Reports"));
            if (!string.IsNullOrWhiteSpace(editorRunReportPath))
            {
                if (!File.Exists(editorRunReportPath))
                    return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                        "Missing", "Editor build-run report was not found.", null, null, null);
                Dictionary<string, string> editorValues = ReadKeyValueFile(editorRunReportPath);
                editorValues.TryGetValue("buildConfiguration", out string configuration);
                editorValues.TryGetValue("scriptingBackend", out string backend);
                if (backend != "IL2CPP" || (configuration != "Release" && configuration != "Development"))
                    return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                        "Configuration", "Editor report lacks a verified Windows IL2CPP build configuration.", null, null, null);
                expectedDevelopment = configuration == "Development" ? "true" : "false";
                editorValues.TryGetValue("status", out string editorStatus);
                editorValues.TryGetValue("processExited", out string processExited);
                editorValues.TryGetValue("processExitCode", out string processExitCode);
                if (editorStatus != "finished" || processExited != "true" || processExitCode != "0")
                    return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                        string.IsNullOrWhiteSpace(editorStatus) ? "Unknown" : editorStatus,
                        "Player process did not complete normally: status=" + editorStatus
                            + " exited=" + processExited + " exitCode=" + processExitCode,
                        null, null, null);
            }
            string playerRunReportPath = Path.Combine(
                safeReportDirectory,
                CharacterTestPerformancePlayerReportRunner.RunReportFileName);
            if (!File.Exists(playerRunReportPath))
            {
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    "Missing",
                    "Player run report was not found.",
                    playerRunReportPath,
                    null,
                    null);
            }

            Dictionary<string, string> values = ReadKeyValueFile(playerRunReportPath);
            values.TryGetValue("status", out string status);
            values.TryGetValue("timingReport", out string timingReport);
            values.TryGetValue("profilerRecorderReport", out string profilerRecorderReport);
            values.TryGetValue("animationReport", out string animationReport);
            string timingReportPath = ResolveReportPath(
                safeReportDirectory,
                timingReport,
                CharacterTestPerformancePlayerReportRunner.TimingReportFileName);
            string profilerRecorderReportPath = ResolveReportPath(
                safeReportDirectory,
                profilerRecorderReport,
                CharacterTestPerformancePlayerReportRunner.ProfilerRecorderReportFileName);
            string animationReportPath = ResolveReportPath(
                safeReportDirectory,
                animationReport,
                CharacterTestPerformancePlayerReportRunner.AnimationReportFileName);

            if (!string.Equals(status, "finished", StringComparison.Ordinal))
            {
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    string.IsNullOrWhiteSpace(status) ? "Unknown" : status,
                    "Player run report status was not finished.",
                    playerRunReportPath,
                    timingReportPath,
                    profilerRecorderReportPath,
                    animationReportPath);
            }

            if (!values.TryGetValue("scriptingBackend", out string runtimeBackend) || runtimeBackend != "IL2CPP"
                || !values.TryGetValue("developmentBuild", out string runtimeDevelopment)
                || (runtimeDevelopment != "true" && runtimeDevelopment != "false")
                || (expectedDevelopment != null && runtimeDevelopment != expectedDevelopment))
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(status,
                    "Player runtime backend/development flag does not match the IL2CPP build configuration.",
                    playerRunReportPath, timingReportPath, profilerRecorderReportPath, animationReportPath);

            if (!File.Exists(timingReportPath))
            {
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    status,
                    "Timing report was not found.",
                    playerRunReportPath,
                    timingReportPath,
                    profilerRecorderReportPath,
                    animationReportPath);
            }

            if (!File.Exists(profilerRecorderReportPath))
            {
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    status,
                    "ProfilerRecorder report was not found.",
                    playerRunReportPath,
                    timingReportPath,
                    profilerRecorderReportPath,
                    animationReportPath);
            }

            if (!File.Exists(animationReportPath))
            {
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    status,
                    "Animation report was not found.",
                    playerRunReportPath,
                    timingReportPath,
                    profilerRecorderReportPath,
                    animationReportPath);
            }

            Dictionary<string, string> animationValues = ReadKeyValueFile(animationReportPath);
            animationValues.TryGetValue("status", out string animationStatus);
            if (!string.Equals(animationStatus, "ready", StringComparison.Ordinal))
            {
                animationValues.TryGetValue("issue", out string animationIssue);
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    string.IsNullOrWhiteSpace(animationStatus) ? "AnimationUnknown" : animationStatus,
                    string.IsNullOrWhiteSpace(animationIssue)
                        ? "Animation report status was not ready."
                        : "Animation report status was not ready: " + animationIssue,
                    playerRunReportPath,
                    timingReportPath,
                    profilerRecorderReportPath,
                    animationReportPath);
            }

            try
            {
                CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(File.ReadAllText(playerRunReportPath));
                RequireGameplayProbe(values);
            }
            catch (Exception ex)
            {
                return CharacterTestPackageTestPlayerRunVerdict.CreateFailed(
                    status, "Player content/gameplay acceptance failed: " + ex.Message,
                    playerRunReportPath, timingReportPath, profilerRecorderReportPath, animationReportPath);
            }

            return CharacterTestPackageTestPlayerRunVerdict.CreatePassed(
                status,
                playerRunReportPath,
                timingReportPath,
                profilerRecorderReportPath,
                animationReportPath);
        }

        internal static void RequireGameplayProbe(IReadOnlyDictionary<string, string> values)
        {
            if (!values.TryGetValue("gameplayProbe.status", out string status) || status != "Passed")
                throw new InvalidDataException("Gameplay movement/attack/jump probe did not pass.");
            foreach (string flag in new[] { "actionsReady", "moved", "attack", "attackAnimation", "jump", "jumpAnimation", "airborne", "landed" })
                if (!values.TryGetValue("gameplayProbe." + flag, out string value) || value != "true")
                    throw new InvalidDataException("Gameplay probe evidence missing or failed: " + flag);
            float distance = ReadProbeFloat(values, "moveDistanceMeters");
            float jump = ReadProbeFloat(values, "jumpHeightMeters");
            float baseline = ReadProbeFloat(values, "baselineHipsAboveRoot");
            float minimum = ReadProbeFloat(values, "minimumHipsAboveRoot");
            if (distance <= 0.2f || jump <= 0.15f || baseline <= 0f || minimum < baseline * 0.7f)
                throw new InvalidDataException("Gameplay probe measurements contradict movement, jump or stable grounded pose.");
        }

        private static float ReadProbeFloat(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue("gameplayProbe." + key, out string text)
                || !float.TryParse(text, System.Globalization.NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("Invalid gameplay probe measurement: " + key);
            return value;
        }

        /// <remarks>AllocByDesign: editor-only package build staging for Player StreamingAssets.</remarks>
        internal static string StageGeneratedPlayerContent()
        {
            return StageGeneratedPlayerContent(CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot);
        }

        /// <summary>
        /// 参数化入口：供 000008 fixture 验证使用，不改变无参入口默认 000001 的行为。
        /// </summary>
        internal static string StageGeneratedPlayerContent(string sourcePackageDirectory)
        {
            return StageGeneratedPlayerContent(sourcePackageDirectory, GeneratedPlayerContentRoot);
        }

        /// <remarks>AllocByDesign: an explicit staging root keeps regression fixtures isolated from Player content.</remarks>
        internal static string StageGeneratedPlayerContent(string sourcePackageDirectory, string generatedContentRoot)
        {
            if (!Directory.Exists(sourcePackageDirectory))
            {
                throw new InvalidOperationException(
                    "CharacterTest generated package directory was not found: " + sourcePackageDirectory);
            }

            for (int i = 0; i < RequiredDefaultCharacterGeneratedPackageFiles.Length; i++)
            {
                string sourcePath = Path.Combine(
                    sourcePackageDirectory,
                    RequiredDefaultCharacterGeneratedPackageFiles[i]);
                if (!File.Exists(sourcePath))
                {
                    throw new InvalidOperationException(
                        "CharacterTest generated package file was not found: " + sourcePath);
                }
            }

            string packageMirror = ResolveGeneratedPackageMirror(sourcePackageDirectory, generatedContentRoot);
            string packageId = Path.GetFileName(
                Path.GetFullPath(sourcePackageDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // The socket bake pair must verify before staging; a legacy or mismatched pair blocks the package.
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    sourcePackageDirectory,
                    packageId,
                    out _,
                    out CharacterSocketBakeCatalogEvidence sourceEvidence,
                    out string[] sourceIssues))
            {
                throw new InvalidOperationException(
                    "Character socket bake is not stageable for package " + packageId + ": " + string.Join(" | ", sourceIssues));
            }

            List<string> generatedFiles = SelectPlayerGeneratedPackageFiles(sourcePackageDirectory);
            if (generatedFiles.Count == 0)
            {
                throw new InvalidOperationException(
                    "CharacterTest generated package directory has no stageable files: " + sourcePackageDirectory);
            }

            if (Directory.Exists(packageMirror))
                AssetDatabase.DeleteAsset(packageMirror);
            if (Directory.Exists(packageMirror))
                throw new IOException("Could not replace character package staging directory: " + packageMirror);

            for (int i = 0; i < generatedFiles.Count; i++)
            {
                string relativePath = Path.GetRelativePath(sourcePackageDirectory, generatedFiles[i]);
                string targetPath = Path.Combine(packageMirror, relativePath);
                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                File.Copy(generatedFiles[i], targetPath, overwrite: true);
                AssetDatabase.ImportAsset(ToUnityAssetPath(targetPath), ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.ImportAsset(packageMirror, ImportAssetOptions.ForceUpdate);

            // Reading back from the real staging directory proves the pair survived the copy.
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    packageMirror,
                    packageId,
                    out _,
                    out CharacterSocketBakeCatalogEvidence stagedEvidence,
                    out string[] stagedIssues)
                || !string.Equals(stagedEvidence.IndexHash, sourceEvidence.IndexHash, StringComparison.Ordinal)
                || !string.Equals(stagedEvidence.BinaryHash, sourceEvidence.BinaryHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Staged character socket bake pair does not match the source package " + packageId + ": "
                    + string.Join(" | ", stagedIssues));
            }

            return packageMirror;
        }

        internal static string ResolveGeneratedPackageMirror(string sourcePackageDirectory, string generatedContentRoot)
        {
            string sourceRoot = Path.GetFullPath(sourcePackageDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string stageRoot = Path.GetFullPath(generatedContentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!stageRoot.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Character package staging must be inside Assets.", nameof(generatedContentRoot));

            JObject report = JObject.Parse(File.ReadAllText(Path.Combine(sourceRoot, "package_cache", "import_report.json")));
            if (!string.Equals((string)report["format"], "mx.characterUnityImportReport.v1", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported character import report format for Player staging.");
            string packageId = (string)report["packageId"];
            if (string.IsNullOrWhiteSpace(packageId) || packageId != packageId.Trim() ||
                packageId == "." || packageId == ".." || packageId.EndsWith(".", StringComparison.Ordinal) ||
                packageId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || packageId.Contains("/") || packageId.Contains("\\"))
                throw new InvalidOperationException("Character package import report has an invalid packageId.");
            if (!string.Equals(Path.GetFileName(sourceRoot), packageId, StringComparison.Ordinal))
                throw new InvalidOperationException("Character package directory does not match import report packageId: " + packageId);

            string target = Path.GetFullPath(Path.Combine(stageRoot, "CharacterPackages", packageId));
            if (string.Equals(target, sourceRoot, StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                sourceRoot.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Character package source and staging directory must not overlap.");
            for (var current = new DirectoryInfo(target); current != null; current = current.Parent)
            {
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Character package staging cannot cross a linked directory: " + current.FullName);
            }
            return "Assets/" + Path.GetRelativePath(assetsRoot, target).Replace('\\', '/');
        }

        /// <summary>
        /// 选择进入 Player 暂存的导出文件：全部 <c>.json</c> 与 <c>.bytes</c>，排除
        /// <see cref="ExcludedPlayerGeneratedPackageFiles"/>。旧布局（仍含完整快照）同样被过滤。
        /// </summary>
        internal static List<string> SelectPlayerGeneratedPackageFiles(string sourcePackageDirectory)
        {
            var selected = new List<string>();
            string[] jsonFiles = Directory.GetFiles(sourcePackageDirectory, "*.json", SearchOption.AllDirectories);
            Array.Sort(jsonFiles, StringComparer.Ordinal);
            string[] binaryFiles = Directory.GetFiles(sourcePackageDirectory, "*.bytes", SearchOption.AllDirectories);
            Array.Sort(binaryFiles, StringComparer.Ordinal);

            AppendStageableFiles(selected, sourcePackageDirectory, jsonFiles);
            AppendStageableFiles(selected, sourcePackageDirectory, binaryFiles);
            return selected;
        }

        private static void AppendStageableFiles(
            List<string> selected,
            string sourcePackageDirectory,
            string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                string relativePath = Path.GetRelativePath(sourcePackageDirectory, files[i]);
                if (IsExcludedPlayerGeneratedPackageFile(relativePath))
                    continue;

                selected.Add(files[i]);
            }
        }

        /// <summary>按规范化后的完整包相对路径逐项判定；不做文件名通配或整目录过滤。</summary>
        internal static bool IsExcludedPlayerGeneratedPackageFile(string packageRelativePath)
        {
            if (string.IsNullOrEmpty(packageRelativePath))
                return false;

            string normalized = packageRelativePath.Replace('\\', '/');
            for (int i = 0; i < ExcludedPlayerGeneratedPackageFiles.Length; i++)
            {
                if (string.Equals(normalized, ExcludedPlayerGeneratedPackageFiles[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void ThrowIfRelevantDirtyScenes(
            MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan)
        {
            var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                CharacterTestPerformancePlayerBuildReportMenu.CharacterTestScenePath
            };
            for (int i = 0; i < plan.Input.ResolvedSources.Count; i++)
                relevant.Add(plan.Input.ResolvedSources[i].UnityAssetPath.Replace('\\', '/'));
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && relevant.Contains((scene.path ?? string.Empty).Replace('\\', '/')))
                    throw new InvalidOperationException("Save the relevant dirty scene before packaging: " + scene.path);
            }
        }

        internal static CharacterTestPackageInputSnapshot CaptureReusedInputs(
            BuildTarget target, string receiptPath)
        {
            CharacterTestPerformancePlayerBuildReportMenu.RequireActiveBuildTarget(target);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before preparing CharacterTest Player inputs.");
            MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            if (plan.HasErrors)
                throw new InvalidOperationException(
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateReportText(plan.Report));
            ThrowIfRelevantDirtyScenes(plan);
            CharacterTestPlayerCharacterPackageSelection selectedCharacters =
                CharacterTestPlayerCharacterPackageSelection.Resolve(validateSceneCache: true);
            CharacterTestPackageInputSnapshot before = CharacterTestPackageInputSnapshot.CaptureSources(
                plan, selectedCharacters);
            string attempt = Guid.NewGuid().ToString("N");
            WriteInputReceipt(receiptPath, attempt, "beforePreparation", target, before);
            CharacterTestPackageInputSnapshot prepared = CharacterTestPackageInputSnapshot.CapturePrepared(target);
            before.RequireSameSources(prepared, "reuse preparation");
            WriteInputReceipt(receiptPath, attempt, "afterPreparation", target, prepared);
            return prepared;
        }

        internal static void VerifyReusedInputs(
            CharacterTestPackageInputSnapshot expected, BuildTarget target, string checkpoint, string receiptPath)
        {
            CharacterTestPackageInputSnapshot observed = CharacterTestPackageInputSnapshot.CapturePrepared(target);
            expected.RequireSameAll(observed, checkpoint);
            WriteInputReceipt(receiptPath, string.Empty, checkpoint, target, observed);
        }

        private static void WriteInputReceipt(
            string path, string attemptId, string checkpoint, BuildTarget target,
            CharacterTestPackageInputSnapshot snapshot)
        {
            var builder = new StringBuilder();
            if (checkpoint == "beforePreparation")
            {
                builder.Append("attemptId: ").AppendLine(attemptId);
                builder.Append("expectedTarget: ").AppendLine(target.ToString());
                builder.AppendLine("scope: G global resources; C scene-selected character packages plus default dependency; R runtime catalog mirror");
                builder.AppendLine("limitation: checkpoint identity does not prevent write-and-restore between checks");
            }
            builder.Append("checkpoint: ").AppendLine(checkpoint);
            snapshot.AppendTo(builder);
            if (checkpoint == "beforePreparation") File.WriteAllText(path, builder.ToString());
            else File.AppendAllText(path, builder.ToString());
        }

        internal sealed class CharacterTestPackageInputSnapshot
        {
            private string _globalSource, _characterSource, _runtimeSource;
            private string _globalOutput, _characterOutput, _runtimeOutput;

            public static CharacterTestPackageInputSnapshot CaptureSources(
                MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan,
                CharacterTestPlayerCharacterPackageSelection selection = null)
            {
                selection ??= CharacterTestPlayerCharacterPackageSelection.Resolve();
                CharacterTestCharacterActionPlayerArtifacts.ValidateSources(selection);
                return new CharacterTestPackageInputSnapshot
                {
                    _globalSource = MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.DescribeBuildInput(plan),
                    _characterSource = CaptureCharacterSet(selection, source: true),
                    _runtimeSource = CaptureRuntime(Directory.GetCurrentDirectory(), source: true)
                        + CharacterTestCharacterActionPlayerArtifacts.Capture(CharacterTestCharacterActionPlayerArtifacts.SourceRoot)
                };
            }

            public static CharacterTestPackageInputSnapshot CapturePrepared(BuildTarget expectedTarget)
            {
                CharacterTestPerformancePlayerBuildReportMenu.RequireActiveBuildTarget(expectedTarget);
                MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                    MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(
                        MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
                if (plan.HasErrors)
                    throw new InvalidOperationException(
                        MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.CreateReportText(plan.Report));
                MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ValidatePublishedArtifacts(plan);
                CharacterTestPlayerCharacterPackageSelection selection =
                    CharacterTestPlayerCharacterPackageSelection.Resolve();
                var snapshot = CaptureSources(plan, selection);
                snapshot._globalOutput = CaptureGlobalOutput();
                snapshot._characterOutput = CaptureCharacterSet(selection, source: false);
                snapshot._runtimeOutput = CaptureRuntime(Application.streamingAssetsPath, source: false)
                    + CharacterTestCharacterActionPlayerArtifacts.Capture(CharacterTestCharacterActionPlayerArtifacts.MirrorRoot);
                if (snapshot._characterSource != snapshot._characterOutput)
                    throw new InvalidOperationException("C character source and Player staging identities differ.");
                if (snapshot._runtimeSource != snapshot._runtimeOutput)
                    throw new InvalidOperationException("R runtime catalog source and Player mirror identities differ.");
                if (CharacterTestRuntimeCatalogMirrorValidator.ValidateDefault().HasIssues)
                    throw new InvalidOperationException("R runtime catalog mirror semantic validation failed.");
                return snapshot;
            }

            public void RequireSameSources(CharacterTestPackageInputSnapshot other, string checkpoint)
            {
                RequireSame(_globalSource, other._globalSource, "G global source", checkpoint);
                RequireSame(_characterSource, other._characterSource, "C character source", checkpoint);
                RequireSame(_runtimeSource, other._runtimeSource, "R runtime catalog source", checkpoint);
            }

            public void RequireSameAll(CharacterTestPackageInputSnapshot other, string checkpoint)
            {
                RequireSameSources(other, checkpoint);
                RequireSame(_globalOutput, other._globalOutput, "G published output", checkpoint);
                RequireSame(_characterOutput, other._characterOutput, "C staged output", checkpoint);
                RequireSame(_runtimeOutput, other._runtimeOutput, "R mirrored output", checkpoint);
            }

            private static void RequireSame(string left, string right, string domain, string checkpoint)
            {
                if (!string.Equals(left, right, StringComparison.Ordinal))
                    throw new InvalidOperationException(domain + " changed " + checkpoint + ". Restart packaging.");
            }

            public void AppendTo(StringBuilder builder)
            {
                AppendDomain(builder, "G.source", _globalSource);
                AppendDomain(builder, "C.source", _characterSource);
                AppendDomain(builder, "R.source", _runtimeSource);
                AppendDomain(builder, "G.output", _globalOutput);
                AppendDomain(builder, "C.output", _characterOutput);
                AppendDomain(builder, "R.output", _runtimeOutput);
            }

            private static void AppendDomain(StringBuilder builder, string domain, string value)
            {
                if (value == null) return;
                builder.Append('[').Append(domain).AppendLine("]");
                builder.Append(value);
            }

            internal static string CaptureCharacter(string packageDirectory)
            {
                if (!Directory.Exists(packageDirectory))
                    throw new DirectoryNotFoundException("C character package is missing: " + packageDirectory);
                string packageId = Path.GetFileName(Path.GetFullPath(packageDirectory).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(packageDirectory, packageId,
                        out _, out _, out string[] issues))
                    throw new InvalidOperationException("C character socket bake is invalid: " + string.Join(" | ", issues));
                List<string> files = SelectPlayerGeneratedPackageFiles(packageDirectory);
                var builder = new StringBuilder("packageId=" + packageId + "\n");
                for (int i = 0; i < files.Count; i++)
                {
                    string relative = Path.GetRelativePath(packageDirectory, files[i]).Replace('\\', '/');
                    builder.Append(relative).Append('|').AppendLine(FileIdentity(files[i]));
                }
                return builder.ToString();
            }

            private static string CaptureCharacterSet(
                CharacterTestPlayerCharacterPackageSelection selection, bool source)
            {
                var builder = new StringBuilder(selection.SourceEvidence);
                string stagedRoot = Path.Combine(GeneratedPlayerContentRoot, "CharacterPackages");
                if (!source && Directory.Exists(stagedRoot))
                {
                    var expected = new HashSet<string>(StringComparer.Ordinal);
                    foreach (CharacterTestPlayerCharacterPackageSelection.Package package in selection.Packages)
                        expected.Add(package.PackageId);
                    foreach (string directory in Directory.GetDirectories(stagedRoot))
                    {
                        string packageId = Path.GetFileName(directory);
                        if (!expected.Contains(packageId))
                            throw new InvalidOperationException("C Player staging contains an unselected character package "
                                + packageId + ". Review its ownership before packaging.");
                    }
                }
                foreach (CharacterTestPlayerCharacterPackageSelection.Package package in selection.Packages)
                {
                    string directory = source ? package.SourceDirectory
                        : ResolveGeneratedPackageMirror(package.SourceDirectory, GeneratedPlayerContentRoot);
                    builder.Append(CaptureCharacter(directory));
                }
                return builder.ToString();
            }

            internal static string CaptureRuntime(string root, bool source)
            {
                var builder = new StringBuilder();
                foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
                {
                    builder.Append(artifact.CatalogKind).Append('|')
                        .Append(artifact.GeneratedJsonPath).Append('|')
                        .Append(artifact.PlayerMirrorRelativeJsonPath).Append('|')
                        .AppendLine(artifact.RequiresPlayerBinary ? "binary" : "jsonOnly");
                    string json = source
                        ? CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(artifact.GeneratedJsonPath, root)
                        : CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                            artifact.PlayerMirrorRelativeJsonPath, root);
                    builder.Append(artifact.CatalogKind).Append(".json|").AppendLine(FileIdentity(json));
                    if (!artifact.RequiresPlayerBinary) continue;
                    string binary = source
                        ? CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(artifact.GeneratedBinaryPath, root)
                        : CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                            artifact.PlayerMirrorRelativeBinaryPath, root);
                    builder.Append(artifact.CatalogKind).Append(".bytes|").AppendLine(FileIdentity(binary));
                }
                return builder.ToString();
            }

            private static string CaptureGlobalOutput()
            {
                string markerPath = MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ArtifactSetPath;
                string reportPath = MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.BuildReportPath;
                JObject marker = JObject.Parse(File.ReadAllText(markerPath));
                var builder = new StringBuilder();
                builder.Append("marker|").AppendLine(FileIdentity(markerPath));
                builder.Append("report|").AppendLine(FileIdentity(reportPath));
                foreach (JToken artifact in marker["artifacts"] ?? new JArray())
                    builder.Append((string)artifact["relativePath"]).Append('|')
                        .Append((string)artifact["sha256"]).AppendLine();
                return builder.ToString();
            }

            private static string FileIdentity(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new FileNotFoundException("Package input is missing: " + path, path);
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    return stream.Length.ToString(CultureInfo.InvariantCulture) + "|sha256:"
                        + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void WriteSummary(CharacterTestPackageTestResult result)
        {
            string directory = Path.GetDirectoryName(result.SummaryPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var builder = new StringBuilder(2048);
            builder.AppendLine("scenario: character-test.windows-package-test");
            builder.Append("status: ").AppendLine(result.Passed ? "Passed" : "Failed");
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("outputRoot: ").AppendLine(result.OutputRoot);
            builder.Append("inputReceipt: ").AppendLine(Path.Combine(result.OutputRoot, "CharacterTestPackageInputReceipt.txt"));
            builder.Append("buildTarget: ").AppendLine(result.PlayerBuild.BuildTarget.ToString());
            builder.Append("resourceBuildResult: ").AppendLine(result.ResourceBuildResult);
            builder.Append("acceptanceMode: ").AppendLine(result.NoRun ? "BuildOnly" : "BuildAndRun");
            builder.Append("playerBuildResult: ").AppendLine(result.PlayerBuild.BuildResult ?? string.Empty);
            builder.Append("playerRunStatus: ").AppendLine(result.PlayerRun.Status ?? string.Empty);
            builder.Append("buildConfiguration: ").AppendLine(result.Configuration.ToString());
            builder.Append("scriptingBackend: ").AppendLine(result.PlayerBuild.Succeeded ? "IL2CPP" : "NotBuilt");
            builder.Append("buildConfigurationReport: ").AppendLine(
                CharacterTestWindowsBuildConfiguration.ReceiptPath(result.PlayerBuild.EditorRunReportPath));
            builder.Append("overviewReportStatus: ").AppendLine(result.OverviewReportStatus);
            builder.Append("overviewReport: ").AppendLine(result.OverviewReportPath);
            if (!string.IsNullOrWhiteSpace(result.PlayerBuild.AppPath))
                builder.Append("playerPath: ").AppendLine(result.PlayerBuild.AppPath);
            if (!string.IsNullOrWhiteSpace(result.PlayerBuild.ExecutablePath))
                builder.Append("executablePath: ").AppendLine(result.PlayerBuild.ExecutablePath);
            if (!string.IsNullOrWhiteSpace(result.PlayerBuild.EditorRunReportPath))
                builder.Append("editorRunReport: ").AppendLine(result.PlayerBuild.EditorRunReportPath);
            if (!string.IsNullOrWhiteSpace(result.PlayerRun.PlayerRunReportPath))
                builder.Append("playerRunReport: ").AppendLine(result.PlayerRun.PlayerRunReportPath);
            if (!string.IsNullOrWhiteSpace(result.PlayerRun.TimingReportPath))
                builder.Append("timingReport: ").AppendLine(result.PlayerRun.TimingReportPath);
            if (!string.IsNullOrWhiteSpace(result.PlayerRun.ProfilerRecorderReportPath))
                builder.Append("profilerRecorderReport: ").AppendLine(result.PlayerRun.ProfilerRecorderReportPath);
            if (!string.IsNullOrWhiteSpace(result.PlayerRun.AnimationReportPath))
                builder.Append("animationReport: ").AppendLine(result.PlayerRun.AnimationReportPath);
            if (!string.IsNullOrWhiteSpace(result.Issue))
                builder.Append("issue: ").AppendLine(result.Issue.Replace('\n', ' '));
            File.WriteAllText(result.SummaryPath, builder.ToString());
        }

        private static Dictionary<string, string> ReadKeyValueFile(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf(':');
                if (separator <= 0)
                    continue;

                string key = lines[i].Substring(0, separator).Trim();
                string value = lines[i].Substring(separator + 1).Trim();
                if (key.Length > 0)
                {
                    if (!values.TryAdd(key, value) && IsAcceptanceReportField(key))
                        throw new FormatException("Duplicate Player report field: " + key);
                }
            }

            return values;
        }

        private static bool IsAcceptanceReportField(string key)
        {
            return key == "status" || key == "issue"
                || key == "buildConfiguration" || key == "scriptingBackend" || key == "developmentBuild"
                || key == "timingReport" || key == "profilerRecorderReport" || key == "animationReport"
                || key == "processExited" || key == "processExitCode"
                || key == "acceptanceStatus" || key == "acceptanceIssue"
                || key.StartsWith("runtimeCatalogLoad.", StringComparison.Ordinal)
                || key.StartsWith("gameplayProbe.", StringComparison.Ordinal)
                || key.StartsWith("runtimeCatalogGate.", StringComparison.Ordinal);
        }

        private static string ResolveReportPath(
            string reportDirectory,
            string reportPath,
            string fallbackFileName)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
                return Path.Combine(reportDirectory, fallbackFileName);
            return Path.IsPathRooted(reportPath)
                ? reportPath
                : Path.GetFullPath(reportPath);
        }

        private static string ToUnityAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }

        private static bool HasArgument(string[] args, string argument)
        {
            if (args == null || string.IsNullOrWhiteSpace(argument))
                return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool TryGetArgumentValue(string[] args, string argument, out string value)
        {
            value = null;
            if (args == null || string.IsNullOrWhiteSpace(argument))
                return false;

            string assignmentPrefix = argument + "=";
            for (int i = 0; i < args.Length; i++)
            {
                string current = args[i];
                if (string.Equals(current, argument, StringComparison.Ordinal))
                {
                    if (i + 1 >= args.Length)
                        return false;
                    value = args[i + 1];
                    return !string.IsNullOrWhiteSpace(value);
                }

                if (current != null
                    && current.StartsWith(assignmentPrefix, StringComparison.Ordinal))
                {
                    value = current.Substring(assignmentPrefix.Length);
                    return !string.IsNullOrWhiteSpace(value);
                }
            }

            return false;
        }
    }

    internal readonly struct CharacterTestPackageTestOptions
    {
        public CharacterTestPackageTestOptions(
            string outputRoot,
            bool skipResourceBuild,
            bool noRun,
            CharacterTestWindowsBuildProfile configuration = CharacterTestWindowsBuildProfile.Release)
        {
            OutputRoot = string.IsNullOrWhiteSpace(outputRoot)
                ? CharacterTestPackageTestCommand.DefaultOutputRoot
                : outputRoot;
            SkipResourceBuild = skipResourceBuild;
            NoRun = noRun;
            Configuration = configuration;
        }

        public string OutputRoot { get; }
        public bool SkipResourceBuild { get; }
        public bool NoRun { get; }
        public CharacterTestWindowsBuildProfile Configuration { get; }
    }

    internal readonly struct CharacterTestPackageTestPlayerRunVerdict
    {
        private CharacterTestPackageTestPlayerRunVerdict(
            bool passed,
            string status,
            string issue,
            string playerRunReportPath,
            string timingReportPath,
            string profilerRecorderReportPath,
            string animationReportPath)
        {
            Passed = passed;
            Status = status;
            Issue = issue;
            PlayerRunReportPath = playerRunReportPath;
            TimingReportPath = timingReportPath;
            ProfilerRecorderReportPath = profilerRecorderReportPath;
            AnimationReportPath = animationReportPath;
        }

        public bool Passed { get; }
        public string Status { get; }
        public string Issue { get; }
        public string PlayerRunReportPath { get; }
        public string TimingReportPath { get; }
        public string ProfilerRecorderReportPath { get; }
        public string AnimationReportPath { get; }

        public static CharacterTestPackageTestPlayerRunVerdict CreatePassed(
            string status,
            string playerRunReportPath,
            string timingReportPath,
            string profilerRecorderReportPath,
            string animationReportPath)
        {
            return new CharacterTestPackageTestPlayerRunVerdict(
                true,
                status,
                null,
                playerRunReportPath,
                timingReportPath,
                profilerRecorderReportPath,
                animationReportPath);
        }

        public static CharacterTestPackageTestPlayerRunVerdict CreateFailed(
            string status,
            string issue,
            string playerRunReportPath,
            string timingReportPath,
            string profilerRecorderReportPath,
            string animationReportPath = null)
        {
            return new CharacterTestPackageTestPlayerRunVerdict(
                false,
                status,
                issue,
                playerRunReportPath,
                timingReportPath,
                profilerRecorderReportPath,
                animationReportPath);
        }

        public static CharacterTestPackageTestPlayerRunVerdict CreateSkipped(string issue)
        {
            return new CharacterTestPackageTestPlayerRunVerdict(
                false,
                "Skipped",
                issue,
                null,
                null,
                null,
                null);
        }
    }

    internal readonly struct CharacterTestPackageTestResult
    {
        public CharacterTestPackageTestResult(
            bool passed,
            string summaryPath,
            string outputRoot,
            string resourceBuildResult,
            CharacterTestPerformancePlayerBuildResult playerBuild,
            CharacterTestPackageTestPlayerRunVerdict playerRun,
            string issue,
            string overviewReportPath,
            string overviewReportStatus,
            bool noRun,
            CharacterTestWindowsBuildProfile configuration)
        {
            Passed = passed;
            SummaryPath = summaryPath;
            OutputRoot = outputRoot;
            ResourceBuildResult = resourceBuildResult;
            PlayerBuild = playerBuild;
            PlayerRun = playerRun;
            Issue = issue;
            OverviewReportPath = overviewReportPath;
            OverviewReportStatus = overviewReportStatus;
            NoRun = noRun;
            Configuration = configuration;
        }

        public bool Passed { get; }
        public string SummaryPath { get; }
        public string OutputRoot { get; }
        public string ResourceBuildResult { get; }
        public CharacterTestPerformancePlayerBuildResult PlayerBuild { get; }
        public CharacterTestPackageTestPlayerRunVerdict PlayerRun { get; }
        public string Issue { get; }
        public string OverviewReportPath { get; }
        public string OverviewReportStatus { get; }
        public bool NoRun { get; }
        public CharacterTestWindowsBuildProfile Configuration { get; }
    }
}
