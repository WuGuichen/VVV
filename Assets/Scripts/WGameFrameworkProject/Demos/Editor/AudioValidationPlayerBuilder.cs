using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MxFramework.Demo
{
    public static class AudioValidationPlayerBuilder
    {
        public const string DefaultOutputPath = "Temp/AudioValidationPlayer/MxAudioValidation.app";

        [MenuItem("MxFramework/Audio/Build Validation Player (macOS)", priority = 127)]
        public static void BuildMacOSDevelopment()
        {
            string outputPath = Path.GetFullPath(DefaultOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { CreateAudioValidationScene.ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.StrictMode
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Audio validation Player build failed: " + report.summary.result);

            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(outputPath), "build-report.txt"),
                "status: Passed\n"
                + "target: " + report.summary.platform + "\n"
                + "output: " + outputPath + "\n"
                + "sizeBytes: " + report.summary.totalSize + "\n"
                + "durationSeconds: " + report.summary.totalTime.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\n"
                + "errors: " + report.summary.totalErrors + "\n"
                + "warnings: " + report.summary.totalWarnings + "\n");
        }
    }
}
