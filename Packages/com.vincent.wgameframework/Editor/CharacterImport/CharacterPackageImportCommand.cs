using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static class CharacterPackageImportCommand
    {
        public const string DefaultGeneratedRoot = "Assets/MxFrameworkGenerated/CharacterPackages";

        internal const string LastPackagePreferenceKey = "MxFramework.CharacterImport.LastPackagePath";
        private const string ImportMenuPath = "MxFramework/Character Package/Import Package...";
        private const string ReimportMenuPath = "MxFramework/Character Package/Reimport Last Package";

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
            return ImportPackageInternal(
                packagePath,
                generatedRoot,
                checkFiles,
                checkHashes,
                synchronizeUnityAssets: true);
        }

        public static int ImportPackageFilesOnly(
            string packagePath,
            string generatedRoot = DefaultGeneratedRoot,
            bool checkFiles = true,
            bool checkHashes = true)
        {
            return ImportPackageInternal(
                packagePath,
                generatedRoot,
                checkFiles,
                checkHashes,
                synchronizeUnityAssets: false);
        }

        private static int ImportPackageInternal(
            string packagePath,
            string generatedRoot,
            bool checkFiles,
            bool checkHashes,
            bool synchronizeUnityAssets)
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
            AppendArg(args, "--property:OutputPath=" + GetIsolatedCliOutputPath(repoRoot));
            AppendArg(args, "--");
            AppendArg(args, "character");
            AppendArg(args, "import-unity");
            AppendArg(args, "--package");
            AppendArg(args, packagePath);
            AppendArg(args, "--project-root");
            AppendArg(args, repoRoot);
            AppendArg(args, "--unity-root");
            AppendArg(args, generatedRoot);
            AppendArg(args, "--build-target");
            AppendArg(args, EditorUserBuildSettings.activeBuildTarget.ToString());
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

                if (process.ExitCode == 0)
                {
                    EditorPrefs.SetString(LastPackagePreferenceKey, packagePath);
                    if (!synchronizeUnityAssets)
                    {
                        UnityEngine.Debug.Log(
                            "MxFramework Character Import: generated files synchronized without Unity prefab rebuild for "
                            + packagePath);
                        return 0;
                    }
                    CharacterImportedPackagePrefabBuildResult buildResult =
                        SynchronizeImportedAssetsWithReport(packagePath, generatedRoot);
                    if (buildResult == null
                        || !string.Equals(buildResult.Status, "Built", StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(buildResult.PrefabPath))
                    {
                        UnityEngine.Debug.LogError("MxFramework Character Import: runtime preview entry-point sync failed for " + packagePath);
                        return 3;
                    }
                    if (!TryWriteImportReceipt(packagePath, generatedRoot, buildResult, out string receiptError))
                    {
                        UnityEngine.Debug.LogError("MxFramework Character Import: persistent Unity receipt failed: " + receiptError);
                        return 3;
                    }

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

        private static string GetIsolatedCliOutputPath(string repoRoot)
        {
            return Path.Combine(repoRoot, "Temp", "MxFrameworkAuthoring", "UnityCharacterImportCli");
        }

        private static string GetLastPackageParent()
        {
            string packagePath = EditorPrefs.GetString(LastPackagePreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(packagePath))
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            string parent = Path.GetDirectoryName(packagePath);
            return string.IsNullOrWhiteSpace(parent) ? packagePath : parent;
        }

        internal static string SynchronizeImportedAssets(string packagePath, string generatedRoot)
        {
            CharacterImportedPackagePrefabBuildResult result = SynchronizeImportedAssetsWithReport(packagePath, generatedRoot);
            return string.Equals(result.Status, "Built", StringComparison.Ordinal)
                ? result.PrefabPath
                : string.Empty;
        }

        internal static CharacterImportedPackagePrefabBuildResult SynchronizeImportedAssetsWithReport(string packagePath, string generatedRoot)
        {
            string packageId = ReadPackageId(packagePath);
            if (string.IsNullOrWhiteSpace(packageId))
            {
                UnityEngine.Debug.LogError("MxFramework Character Import: package manifest is missing packageId; runtime entry points were not synchronized.");
                return new CharacterImportedPackagePrefabBuildResult
                {
                    Status = "Failed",
                    Message = "Package manifest is missing packageId.",
                    Issues = new[] { "Package manifest is missing packageId." }
                };
            }

            string importedRoot = NormalizeProjectPath(generatedRoot.TrimEnd('/', '\\') + "/" + packageId);
            ImportChangedAssets(importedRoot);
            CharacterUnityResourceCatalogUpdater.RefreshCatalog(importedRoot);
            CommonWeaponWrapperPrefabBuildReport commonWeaponReport =
                CommonWeaponWrapperPrefabBuilder.SynchronizeFromGlobalProfile(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            if (commonWeaponReport.Errors.Count > 0)
            {
                UnityEngine.Debug.LogError(
                    "MxFramework Character Import: public weapon wrapper prefab sync failed. " +
                    string.Join(" | ", commonWeaponReport.Errors));
                return new CharacterImportedPackagePrefabBuildResult
                {
                    Status = "Failed",
                    Message = "Public weapon wrapper prefab sync failed.",
                    Issues = commonWeaponReport.Errors.ToArray()
                };
            }

            if (commonWeaponReport.Total > 0)
            {
                UnityEngine.Debug.Log(
                    "MxFramework Character Import: public weapon wrapper prefabs synchronized. built=" +
                    commonWeaponReport.Built +
                    " total=" +
                    commonWeaponReport.Total +
                    " materials=" +
                    commonWeaponReport.MaterialBindings +
                    ".");
            }

            CharacterImportedPackagePrefabBuildResult result = CharacterImportedPackagePrefabBuilder.SynchronizeRuntimeEntryPointsWithReport(
                importedRoot,
                selectPrefab: false,
                assetsAlreadyImported: true);
            string prefabPath = result.PrefabPath;
            if (!string.IsNullOrWhiteSpace(prefabPath))
            {
                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            if (!string.Equals(result.Status, "Built", StringComparison.Ordinal))
                return result;

            // 阶段 B：Unity 重建完成后，对延期保护的专用预览 GLB 做引用核对与定向删除。
            // 菜单/批处理入口与 CharacterPackageImportRequestBridge 都经过本方法，两条路径共享同一步骤。
            CharacterPreviewGlbDeferredCleanup.Result previewCleanup =
                CharacterPreviewGlbDeferredCleanup.Execute(importedRoot);
            if (previewCleanup != null)
            {
                UnityEngine.Debug.Log("MxFramework Character Import: " + previewCleanup.ToLogLine());
                for (int i = 0; i < previewCleanup.Messages.Count; i++)
                {
                    UnityEngine.Debug.Log(
                        "MxFramework Character Import: preview cleanup - " + previewCleanup.Messages[i]);
                }
                if (!previewCleanup.Succeeded)
                {
                    result.Status = "Failed";
                    result.ErrorCode = "CHAR_PREVIEW_GLB_CLEANUP_INCOMPLETE";
                    result.Message = "Preview GLB cleanup did not complete: " + previewCleanup.ToLogLine();
                    var issues = new List<string>(result.Issues ?? Array.Empty<string>());
                    issues.AddRange(previewCleanup.Messages);
                    result.Issues = issues.ToArray();
                }
            }

            return result;
        }

        private static void ImportChangedAssets(string importedRoot)
        {
            string reportPath = Path.Combine(importedRoot, "package_cache", "import_report.json");
            try
            {
                JObject report = File.Exists(reportPath)
                    ? JObject.Parse(File.ReadAllText(reportPath))
                    : null;
                JArray operations = report?["operations"] as JArray;
                if (operations == null)
                    throw new InvalidDataException("Unity import report has no operations array.");

                var imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < operations.Count; i++)
                {
                    JObject operation = operations[i] as JObject;
                    string action = (string)operation?["action"] ?? string.Empty;
                    if (!string.Equals(action, "Added", StringComparison.Ordinal)
                        && !string.Equals(action, "Updated", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string targetPath = NormalizeProjectPath((string)operation?["targetPath"] ?? string.Empty);
                    if (!targetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                        || !File.Exists(targetPath)
                        || !imported.Add(targetPath))
                    {
                        continue;
                    }
                    AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
                }
                return;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "MxFramework Character Import: changed-asset import plan unavailable; falling back to package import. "
                    + exception.Message);
            }

            AssetDatabase.ImportAsset(
                importedRoot,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool TryWriteImportReceipt(
            string packagePath,
            string generatedRoot,
            CharacterImportedPackagePrefabBuildResult buildResult,
            out string error)
        {
            error = string.Empty;
            string packageId = ReadPackageId(packagePath);
            string reportPath = Path.Combine(
                generatedRoot,
                packageId,
                "package_cache",
                "import_report.json");
            try
            {
                JObject report = JObject.Parse(File.ReadAllText(reportPath));
                var expected = new CharacterUnityImportExpectedIdentity
                {
                    PackageId = (string)report["packageId"] ?? packageId,
                    SourcePackageHash = (string)report["sourcePackageHash"] ?? string.Empty,
                    GeneratedConfigHash = (string)report["generatedConfigHash"] ?? string.Empty,
                    GeometryBindingHash = (string)report["geometryBindingHash"] ?? string.Empty,
                    ResourceMappingHash = (string)report["resourceMappingHash"] ?? string.Empty,
                    WritePlanHash = (string)report["writePlanHash"] ?? string.Empty,
                };
                return CharacterUnityEditorImportReceipt.TryValidateAndWrite(
                    expected,
                    buildResult,
                    out _,
                    out error);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is Newtonsoft.Json.JsonException)
            {
                error = exception.Message;
                return false;
            }
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
