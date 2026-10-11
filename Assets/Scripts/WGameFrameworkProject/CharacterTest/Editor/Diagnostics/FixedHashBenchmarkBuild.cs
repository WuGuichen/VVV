using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static class FixedHashBenchmarkBuild
    {
        public static void RunBefore() => RunBatch("before");
        public static void RunAfter() => RunBatch("after");

        private static void RunBatch(string label)
        {
            try
            {
                UnityEditorInternal.ProfilerDriver.deepProfiling = false;
                Diagnostics.FixedHashBenchmark.CaptureSession(label);
                Diagnostics.FixedHashBenchmark.Run("editor-" + label, label == "before");
                Build(label);
                if (label == "after") Build(label, allocationsOnly: true);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Library/FixedHashOptimization");
                File.WriteAllText("Library/FixedHashOptimization/batch-error.txt", exception.ToString());
                UnityEngine.Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }

        public static string Build(string label, bool allocationsOnly = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building the diagnostic player.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64
                || PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("This benchmark requires the current Windows x64 Mono target.");
            if (label != "before" && label != "after") throw new ArgumentException("Expected before or after.", nameof(label));
            const string directory = "Assets/__FixedHashBenchmarkBuild";
            if (AssetDatabase.IsValidFolder(directory))
                throw new InvalidOperationException("The temporary benchmark asset directory already exists.");
            string output = Path.GetFullPath(".agent/FixedHashOptimization/player-" + label + (allocationsOnly ? "-allocations" : ""));
            Directory.CreateDirectory(output);
            Scene previous = SceneManager.GetActiveScene();
            Scene temporary = default;
            AssetDatabase.CreateFolder("Assets", "__FixedHashBenchmarkBuild");
            try
            {
                string scenePath = directory + "/FixedHashBenchmark.unity";
                if (string.IsNullOrEmpty(previous.path))
                {
                    // Keep the untitled scene, dirty flag and unsaved identity untouched.
                    if (!EditorSceneManager.SaveScene(previous, scenePath, saveAsCopy: true))
                        throw new IOException("Could not save the temporary benchmark scene copy.");
                }
                else
                {
                    temporary = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                    if (!EditorSceneManager.SaveScene(temporary, scenePath))
                        throw new IOException("Could not save temporary benchmark scene.");
                    EditorSceneManager.CloseScene(temporary, true);
                }
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { scenePath },
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = Path.Combine(output, "FixedHashBenchmark.exe"),
                    extraScriptingDefines = new[] { "CHARACTER_TEST_HASH_BENCHMARK" },
                    options = allocationsOnly ? BuildOptions.Development : BuildOptions.None,
                });
                if (report == null) throw new InvalidOperationException("Unity returned no build report; inspect Editor Console.");
                File.WriteAllText(Path.Combine(output, "build-summary.json"),
                    Newtonsoft.Json.JsonConvert.SerializeObject(new { result = report.summary.result.ToString(),
                        errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                        seconds = report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize,
                        target = report.summary.platform.ToString(), backend = "Mono", allocationsOnly }, Newtonsoft.Json.Formatting.Indented));
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Fixed hash benchmark Player build failed.");
                File.Copy("Tools/FixedHashBenchmark/xxHash-LICENSE.txt", Path.Combine(output, "xxHash-LICENSE.txt"), true);
                return Path.Combine(output, "FixedHashBenchmark.exe");
            }
            finally
            {
                if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(directory);
            }
        }
    }
}
