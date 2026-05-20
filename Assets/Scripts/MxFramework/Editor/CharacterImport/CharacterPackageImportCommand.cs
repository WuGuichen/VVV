using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static class CharacterPackageImportCommand
    {
        public const string DefaultGeneratedRoot = "Assets/MxFrameworkGenerated/CharacterPackages";

        private const string LastPackagePreferenceKey = "MxFramework.CharacterImport.LastPackagePath";
        private const string ImportMenuPath = "MxFramework/Character/Import Character Package...";
        private const string ReimportMenuPath = "MxFramework/Character/Reimport Last Character Package";

        [MenuItem(ImportMenuPath)]
        public static void ImportFromMenu()
        {
            string packagePath = EditorUtility.OpenFolderPanel("导入 Character Resource Package", GetLastPackageParent(), string.Empty);
            if (string.IsNullOrWhiteSpace(packagePath))
                return;

            int exitCode = ImportPackage(packagePath);
            if (exitCode == 0)
                EditorPrefs.SetString(LastPackagePreferenceKey, packagePath);
        }

        [MenuItem(ReimportMenuPath)]
        public static void ReimportLast()
        {
            string packagePath = EditorPrefs.GetString(LastPackagePreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                UnityEngine.Debug.LogWarning("MxFramework Character Import: no last package path was recorded.");
                return;
            }

            ImportPackage(packagePath);
        }

        [MenuItem(ReimportMenuPath, true)]
        private static bool CanReimportLast()
        {
            string packagePath = EditorPrefs.GetString(LastPackagePreferenceKey, string.Empty);
            return !string.IsNullOrWhiteSpace(packagePath) && Directory.Exists(packagePath);
        }

        public static void Import()
        {
            string packagePath = GetCommandLineValue("-characterPackage");
            if (string.IsNullOrWhiteSpace(packagePath))
                packagePath = GetCommandLineValue("-characterPackagePath");

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                UnityEngine.Debug.LogError("MxFramework Character Import: -characterPackage <path> is required.");
                ExitBatchMode(2);
                return;
            }

            string generatedRoot = GetCommandLineValue("-characterImportRoot");
            if (string.IsNullOrWhiteSpace(generatedRoot))
                generatedRoot = GetCommandLineValue("-characterImportOutRoot");
            if (string.IsNullOrWhiteSpace(generatedRoot))
                generatedRoot = DefaultGeneratedRoot;

            bool checkHashes = !HasCommandLineFlag("-characterImportNoHashCheck");
            int exitCode = ImportPackage(packagePath, generatedRoot, checkFiles: true, checkHashes: checkHashes);
            ExitBatchMode(exitCode);
        }

        public static int ImportPackage(
            string packagePath,
            string generatedRoot = DefaultGeneratedRoot,
            bool checkFiles = true,
            bool checkHashes = true)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
            {
                UnityEngine.Debug.LogError("MxFramework Character Import: package directory was not found: " + packagePath);
                return 2;
            }

            string repoRoot = FindRepoRoot();
            string cliProject = Path.Combine(repoRoot, "Tools", "MxFramework.Authoring", "src", "MxFramework.Authoring.Cli", "MxFramework.Authoring.Cli.csproj");
            if (!File.Exists(cliProject))
            {
                UnityEngine.Debug.LogError("MxFramework Character Import: authoring CLI project was not found: " + cliProject);
                return 2;
            }

            var args = new StringBuilder();
            AppendArg(args, "run");
            AppendArg(args, "--project");
            AppendArg(args, cliProject);
            AppendArg(args, "--");
            AppendArg(args, "character");
            AppendArg(args, "import-unity");
            AppendArg(args, "--package");
            AppendArg(args, packagePath);
            AppendArg(args, "--project-root");
            AppendArg(args, repoRoot);
            AppendArg(args, "--unity-root");
            AppendArg(args, generatedRoot);
            if (checkFiles)
                AppendArg(args, "--check-files");
            if (checkHashes)
                AppendArg(args, "--check-hashes");

            var startInfo = new ProcessStartInfo(FindDotnetExecutable(), args.ToString())
            {
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("MxFramework Character Import: failed to launch authoring CLI: " + ex.Message);
                return 2;
            }

            if (process == null)
            {
                UnityEngine.Debug.LogError("MxFramework Character Import: failed to launch authoring CLI.");
                return 2;
            }

            using (process)
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                    UnityEngine.Debug.Log(output);
                if (!string.IsNullOrWhiteSpace(error))
                    UnityEngine.Debug.LogError(error);

                AssetDatabase.Refresh();
                if (process.ExitCode == 0)
                {
                    EditorPrefs.SetString(LastPackagePreferenceKey, packagePath);
                    RefreshImportedAssets(packagePath, generatedRoot);
                    UnityEngine.Debug.Log("MxFramework Character Import: completed for " + packagePath);
                }
                else
                {
                    UnityEngine.Debug.LogError("MxFramework Character Import: failed with exit code " + process.ExitCode + " for " + packagePath);
                }

                return process.ExitCode;
            }
        }

        private static string FindRepoRoot()
        {
            string current = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(current, "Tools", "MxFramework.Authoring", "MxFramework.Authoring.sln");
                if (File.Exists(candidate))
                    return current;

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent != null ? parent.FullName : string.Empty;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string FindDotnetExecutable()
        {
            string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrWhiteSpace(dotnetRoot))
            {
                string candidate = Path.Combine(dotnetRoot, "dotnet");
                if (File.Exists(candidate))
                    return candidate;
            }

            string[] candidates =
            {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                "/usr/local/bin/dotnet"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                    return candidates[i];
            }

            return "dotnet";
        }

        private static string GetLastPackageParent()
        {
            string packagePath = EditorPrefs.GetString(LastPackagePreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(packagePath))
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            string parent = Path.GetDirectoryName(packagePath);
            return string.IsNullOrWhiteSpace(parent) ? packagePath : parent;
        }

        private static void RefreshImportedAssets(string packagePath, string generatedRoot)
        {
            string packageId = ReadPackageId(packagePath);
            if (string.IsNullOrWhiteSpace(packageId))
                return;

            string importedRoot = NormalizeProjectPath(generatedRoot.TrimEnd('/', '\\') + "/" + packageId);
            AssetDatabase.ImportAsset(importedRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            CharacterUnityResourceCatalogUpdater.RefreshCatalog(importedRoot);
            AssetDatabase.Refresh();
        }

        private static string ReadPackageId(string packagePath)
        {
            string manifestPath = Path.Combine(packagePath, "manifest.json");
            if (!File.Exists(manifestPath))
                return string.Empty;

            try
            {
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                return manifest["packageId"]?.Value<string>() ?? string.Empty;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("MxFramework Character Import: failed to read package manifest: " + ex.Message);
                return string.Empty;
            }
        }

        private static string NormalizeProjectPath(string path)
        {
            string full = Path.GetFullPath(path ?? string.Empty).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : (path ?? string.Empty).Replace('\\', '/');
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return string.Empty;
        }

        private static bool HasCommandLineFlag(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AppendArg(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append('"').Append((value ?? string.Empty).Replace("\"", "\\\"")).Append('"');
        }

        private static void ExitBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }
    }
}
