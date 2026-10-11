using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.PackageConsumerSmoke
{
    public static class PackageConsumerPlayerBuild
    {
        public const string ScenePath =
            "Assets/PackageConsumerBuild/Scenes/PackageConsumerSmoke.unity";

        private const string BuildDirectoryEnvironmentVariable =
            "MXFRAMEWORK_CONSUMER_PLAYER_BUILD_DIRECTORY";
        private const string BuildReportEnvironmentVariable =
            "MXFRAMEWORK_CONSUMER_PLAYER_BUILD_REPORT";

        public static void GenerateFixtureScene()
        {
            string sceneDirectory = Path.GetDirectoryName(ScenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectory))
                throw new InvalidOperationException("Consumer smoke scene directory is invalid.");

            Directory.CreateDirectory(sceneDirectory);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var marker = new GameObject("MxFramework Package Consumer Smoke");
            SceneManager.MoveGameObjectToScene(marker, scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save the Consumer smoke scene.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void Build()
        {
            string buildDirectory = GetRequiredAbsolutePath(BuildDirectoryEnvironmentVariable);
            string reportPath = GetRequiredAbsolutePath(BuildReportEnvironmentVariable);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Consumer smoke scene is missing: " + ScenePath);

            Directory.CreateDirectory(buildDirectory);
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (string.IsNullOrWhiteSpace(reportDirectory))
                throw new InvalidOperationException("Consumer Player Build report directory is invalid.");
            Directory.CreateDirectory(reportDirectory);

            BuildTarget target;
            string executableName;
#if UNITY_EDITOR_WIN
            target = BuildTarget.StandaloneWindows64;
            executableName = "MxFrameworkPackageConsumerSmoke.exe";
#elif UNITY_EDITOR_OSX
            target = BuildTarget.StandaloneOSX;
            executableName = "MxFrameworkPackageConsumerSmoke.app";
#else
            target = BuildTarget.StandaloneLinux64;
            executableName = "MxFrameworkPackageConsumerSmoke";
#endif

            string outputPath = Path.Combine(buildDirectory, executableName);
            BuildReport buildReport;
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = target,
                    options = BuildOptions.StrictMode
                };
                buildReport = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception exception)
            {
                WriteEvidence(
                    reportPath,
                    new PlayerBuildEvidence
                    {
                        status = "Failed",
                        result = "Exception",
                        target = target.ToString(),
                        outputPath = outputPath,
                        exception = exception.ToString()
                    });
                throw;
            }

            BuildSummary summary = buildReport.summary;
            bool succeeded = summary.result == BuildResult.Succeeded;
            WriteEvidence(
                reportPath,
                new PlayerBuildEvidence
                {
                    status = succeeded ? "Passed" : "Failed",
                    result = summary.result.ToString(),
                    target = summary.platform.ToString(),
                    outputPath = outputPath,
                    totalSizeBytes = summary.totalSize.ToString(CultureInfo.InvariantCulture),
                    durationSeconds = summary.totalTime.TotalSeconds.ToString(
                        "F3",
                        CultureInfo.InvariantCulture),
                    errors = summary.totalErrors,
                    warnings = summary.totalWarnings
                });

            if (!succeeded)
            {
                throw new InvalidOperationException(
                    "Consumer Player Build failed with result: " + summary.result);
            }
        }

        private static string GetRequiredAbsolutePath(string environmentVariable)
        {
            string value = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(environmentVariable + " is required.");
            if (!Path.IsPathRooted(value))
                throw new InvalidOperationException(environmentVariable + " must be an absolute path.");

            return Path.GetFullPath(value);
        }

        private static void WriteEvidence(string reportPath, PlayerBuildEvidence evidence)
        {
            File.WriteAllText(reportPath, JsonUtility.ToJson(evidence, true));
        }

        [Serializable]
        private sealed class PlayerBuildEvidence
        {
            public string status;
            public string result;
            public string target;
            public string outputPath;
            public string totalSizeBytes;
            public string durationSeconds;
            public int errors;
            public int warnings;
            public string exception;
        }
    }
}
