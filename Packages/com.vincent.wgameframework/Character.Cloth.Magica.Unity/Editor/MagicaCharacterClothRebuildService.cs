using System;
using System.IO;
using MagicaCloth2;
using MxFramework.Editor.CharacterImport;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public sealed class MagicaCharacterClothRebuildResult
    {
        public bool Success { get; internal set; }
        public int ExitCode { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
        public bool ReceiptReady { get; internal set; }
        public int PreviewClothCount { get; internal set; }
        public int RuntimeClothCount { get; internal set; }
        public int PreviewColliderCount { get; internal set; }
        public int RuntimeColliderCount { get; internal set; }
        public bool UsedFastPath { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
    }

    public static class MagicaCharacterClothRebuildService
    {
        public static MagicaCharacterClothRebuildResult Rebuild(string packagePath, string packageId)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string fallbackReason = string.Empty;
            if (CanUseFastPath(packagePath, packageId))
            {
                try
                {
                    MagicaCharacterClothRebuildResult fast = RebuildFast(packagePath, packageId);
                    if (fast.Success)
                    {
                        stopwatch.Stop();
                        fast.UsedFastPath = true;
                        fast.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        fast.Message += $" Fast path {fast.ElapsedMilliseconds} ms.";
                        return fast;
                    }
                    fallbackReason = fast.Message;
                }
                catch (Exception exception) when (
                    exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is InvalidDataException
                    || exception is InvalidOperationException
                    || exception is ArgumentException)
                {
                    fallbackReason = exception.Message;
                }
            }

            MagicaCharacterClothRebuildResult full = RebuildFull(packagePath, packageId);
            stopwatch.Stop();
            full.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            if (!string.IsNullOrWhiteSpace(fallbackReason))
                full.Message += " Cloth fast path fell back to full import: " + fallbackReason;
            full.Message += $" Full path {full.ElapsedMilliseconds} ms.";
            return full;
        }

        private static MagicaCharacterClothRebuildResult RebuildFull(
            string packagePath,
            string packageId)
        {
            var result = new MagicaCharacterClothRebuildResult();
            result.ExitCode = CharacterPackageImportCommand.ImportPackage(packagePath);
            if (result.ExitCode != 0)
            {
                result.Message = "Unity Import/Rebuild failed with exit code " + result.ExitCode + ".";
                return result;
            }

            string importedRoot = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId;
            CharacterImportedPackagePrefabBuildResult forcedBuild =
                CharacterImportedPackagePrefabBuilder.ForceSynchronizeRuntimeEntryPointsWithReport(
                    importedRoot,
                    selectPrefab: false);
            if (!string.Equals(forcedBuild.Status, "Built", StringComparison.Ordinal))
            {
                result.Message = "Forced prefab rebuild failed: " + forcedBuild.Message;
                return result;
            }

            string previewPath = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
                + "/prefabs/" + packageId + "_character_preview.prefab";
            string runtimePath = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
                + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/" + packageId
                + "/prefabs/" + packageId + "_character_preview.prefab";
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(previewPath);
            GameObject runtime = AssetDatabase.LoadAssetAtPath<GameObject>(runtimePath);
            if (preview == null || runtime == null)
            {
                result.Message = "Preview or Runtime Resources prefab is missing after rebuild.";
                return result;
            }
            result.PreviewClothCount = preview.GetComponentsInChildren<MagicaCloth>(true).Length;
            result.RuntimeClothCount = runtime.GetComponentsInChildren<MagicaCloth>(true).Length;
            result.PreviewColliderCount = preview.GetComponentsInChildren<ColliderComponent>(true).Length;
            result.RuntimeColliderCount = runtime.GetComponentsInChildren<ColliderComponent>(true).Length;
            MagicaCharacterClothAuthoringDocument expected =
                MagicaCharacterClothAuthoringRepository.Load(importedRoot).Document;
            int expectedCloths = expected.enabled ? (expected.cloths?.Length ?? 0) : 0;
            int expectedColliders = 0;
            if (expected.enabled && expected.cloths != null)
            {
                for (int clothIndex = 0; clothIndex < expected.cloths.Length; clothIndex++)
                    expectedColliders += expected.cloths[clothIndex]?.colliders?.Length ?? 0;
            }
            int expectedSystems = expected.enabled ? 1 : 0;
            bool parity = result.PreviewClothCount == expectedCloths
                && result.RuntimeClothCount == expectedCloths
                && result.PreviewColliderCount == expectedColliders
                && result.RuntimeColliderCount == expectedColliders
                && preview.GetComponents<MagicaCharacterClothController>().Length == expectedSystems
                && runtime.GetComponents<MagicaCharacterClothController>().Length == expectedSystems
                && preview.GetComponents<MagicaCharacterClothSystem>().Length == expectedSystems
                && runtime.GetComponents<MagicaCharacterClothSystem>().Length == expectedSystems;

            string receiptPath = Path.GetFullPath(
                "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
                + "/package_cache/unity_editor_import_receipt.json");
            if (File.Exists(receiptPath))
            {
                try
                {
                    JObject receipt = JObject.Parse(File.ReadAllText(receiptPath));
                    result.ReceiptReady = (bool?)receipt["ready"] == true;
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            result.Success = parity && result.ReceiptReady;
            result.Message = result.Success
                ? $"Rebuild Ready. enabled={expected.enabled}, Cloth={result.PreviewClothCount}, Collider={result.PreviewColliderCount}, receipt=Ready."
                : $"Rebuild completed but parity or receipt validation failed. expected={expectedCloths}/{expectedColliders}, preview={result.PreviewClothCount}/{result.PreviewColliderCount}, runtime={result.RuntimeClothCount}/{result.RuntimeColliderCount}, receipt={result.ReceiptReady}.";
            return result;
        }

        private static bool CanUseFastPath(string packagePath, string packageId)
        {
            if (Application.isPlaying
                || string.IsNullOrWhiteSpace(packagePath)
                || string.IsNullOrWhiteSpace(packageId))
                return false;
            string importedRoot = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId;
            return Directory.Exists(packagePath)
                && File.Exists(Path.Combine(packagePath, MagicaCharacterClothAuthoringRepository.ConfigRelativePath))
                && File.Exists(ProjectPath(PreviewPrefabPath(packageId)))
                && File.Exists(ProjectPath(RuntimePrefabPath(packageId)))
                && Directory.Exists(ProjectPath(importedRoot));
        }

        private static MagicaCharacterClothRebuildResult RebuildFast(
            string packagePath,
            string packageId)
        {
            var result = new MagicaCharacterClothRebuildResult();
            result.ExitCode = CharacterPackageImportCommand.ImportPackageFilesOnly(packagePath);
            if (result.ExitCode != 0)
            {
                result.Message = "Cloth file synchronization failed with exit code " + result.ExitCode + ".";
                return result;
            }

            string importedRoot = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId;
            string configAssetPath = importedRoot + "/" + MagicaCharacterClothAuthoringRepository.ConfigRelativePath;
            AssetDatabase.ImportAsset(
                configAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            MagicaCharacterClothAuthoringDocument document =
                MagicaCharacterClothAuthoringRepository.Load(importedRoot).Document;
            ProjectClothOntoPrefab(PreviewPrefabPath(packageId), document, packageId);
            ProjectClothOntoPrefab(RuntimePrefabPath(packageId), document, packageId);
            PopulateParity(result, packageId);
            if (!HasProjectionParity(result, packageId, document))
            {
                result.Message = ParityFailureMessage(result, document, receiptReady: false);
                return result;
            }
            if (!RefreshReceiptIdentity(importedRoot, packageId, out string receiptError))
            {
                result.Message = "Cloth fast rebuild could not refresh the Unity receipt: " + receiptError;
                return result;
            }
            result.ReceiptReady = ReadReceiptReady(packageId);
            result.Success = result.ReceiptReady;
            result.Message = result.Success
                ? $"Fast Rebuild Ready. enabled={document.enabled}, Cloth={result.PreviewClothCount}, Collider={result.PreviewColliderCount}, receipt=Ready."
                : ParityFailureMessage(result, document, result.ReceiptReady);
            return result;
        }

        private static void ProjectClothOntoPrefab(
            string prefabPath,
            MagicaCharacterClothAuthoringDocument document,
            string packageId)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform bodyModel = FindByName(root != null ? root.transform : null, "BodyModel");
                if (root == null || bodyModel == null)
                    throw new InvalidDataException("Cloth fast rebuild could not find BodyModel in " + prefabPath + ".");
                if (!MagicaCharacterClothMotionPreviewHotApply.TryApply(
                        root,
                        bodyModel,
                        document,
                        out string error))
                    throw new InvalidDataException("Cloth projection failed for " + prefabPath + ": " + error);

                if (document.enabled)
                {
                    root.GetComponent<MagicaCharacterClothController>()?.RefreshClothComponents();
                    root.GetComponent<MagicaCharacterClothSystem>()?.RefreshControllers();
                }
                else
                {
                    DestroyComponent<MagicaCharacterClothController>(root);
                    DestroyComponent<MagicaCharacterClothSystem>(root);
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new IOException("Cloth fast rebuild could not save " + prefabPath + ".");
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.ImportAsset(
                prefabPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void DestroyComponent<T>(GameObject root) where T : Component
        {
            T component = root != null ? root.GetComponent<T>() : null;
            if (component != null) Object.DestroyImmediate(component);
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static bool RefreshReceiptIdentity(
            string importedRoot,
            string packageId,
            out string error)
        {
            error = string.Empty;
            string reportPath = ProjectPath(importedRoot + "/package_cache/import_report.json");
            string receiptAssetPath = importedRoot + "/package_cache/unity_editor_import_receipt.json";
            string receiptPath = ProjectPath(receiptAssetPath);
            try
            {
                JObject report = JObject.Parse(File.ReadAllText(reportPath));
                JObject receipt = JObject.Parse(File.ReadAllText(receiptPath));
                JObject hashes = receipt["hashes"] as JObject ?? new JObject();
                receipt["hashes"] = hashes;
                string[] keys =
                {
                    "sourcePackageHash",
                    "generatedConfigHash",
                    "geometryBindingHash",
                    "resourceMappingHash",
                    "writePlanHash",
                };
                for (int i = 0; i < keys.Length; i++)
                    hashes[keys[i]] = (string)report[keys[i]] ?? string.Empty;
                receipt["ready"] = true;
                WriteTextAtomicallyIfChanged(receiptPath, receipt.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
                AssetDatabase.ImportAsset(
                    receiptAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                return true;
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

        private static void WriteTextAtomicallyIfChanged(string path, string text)
        {
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
                return;
            string temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false));
                AssetDatabase.ReleaseCachedFileHandles();
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void PopulateParity(
            MagicaCharacterClothRebuildResult result,
            string packageId)
        {
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath(packageId));
            GameObject runtime = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath(packageId));
            if (preview == null || runtime == null) return;
            result.PreviewClothCount = preview.GetComponentsInChildren<MagicaCloth>(true).Length;
            result.RuntimeClothCount = runtime.GetComponentsInChildren<MagicaCloth>(true).Length;
            result.PreviewColliderCount = preview.GetComponentsInChildren<ColliderComponent>(true).Length;
            result.RuntimeColliderCount = runtime.GetComponentsInChildren<ColliderComponent>(true).Length;
        }

        private static bool HasProjectionParity(
            MagicaCharacterClothRebuildResult result,
            string packageId,
            MagicaCharacterClothAuthoringDocument expected)
        {
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath(packageId));
            GameObject runtime = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath(packageId));
            if (preview == null || runtime == null) return false;
            int expectedCloths = expected.enabled ? expected.cloths?.Length ?? 0 : 0;
            int expectedColliders = ExpectedColliderCount(expected);
            int expectedSystems = expected.enabled ? 1 : 0;
            return result.PreviewClothCount == expectedCloths
                && result.RuntimeClothCount == expectedCloths
                && result.PreviewColliderCount == expectedColliders
                && result.RuntimeColliderCount == expectedColliders
                && preview.GetComponents<MagicaCharacterClothController>().Length == expectedSystems
                && runtime.GetComponents<MagicaCharacterClothController>().Length == expectedSystems
                && preview.GetComponents<MagicaCharacterClothSystem>().Length == expectedSystems
                && runtime.GetComponents<MagicaCharacterClothSystem>().Length == expectedSystems;
        }

        private static int ExpectedColliderCount(MagicaCharacterClothAuthoringDocument expected)
        {
            if (!expected.enabled || expected.cloths == null) return 0;
            int count = 0;
            for (int i = 0; i < expected.cloths.Length; i++)
                count += expected.cloths[i]?.colliders?.Length ?? 0;
            return count;
        }

        private static string ParityFailureMessage(
            MagicaCharacterClothRebuildResult result,
            MagicaCharacterClothAuthoringDocument expected,
            bool receiptReady)
        {
            return $"Cloth rebuild parity failed. expected={expected.enabled switch { true => expected.cloths?.Length ?? 0, false => 0 }}/{ExpectedColliderCount(expected)}, preview={result.PreviewClothCount}/{result.PreviewColliderCount}, runtime={result.RuntimeClothCount}/{result.RuntimeColliderCount}, receipt={receiptReady}.";
        }

        private static bool ReadReceiptReady(string packageId)
        {
            string path = ProjectPath(
                "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
                + "/package_cache/unity_editor_import_receipt.json");
            if (!File.Exists(path)) return false;
            try
            {
                return (bool?)JObject.Parse(File.ReadAllText(path))["ready"] == true;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        private static string PreviewPrefabPath(string packageId) =>
            "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
            + "/prefabs/" + packageId + "_character_preview.prefab";

        private static string RuntimePrefabPath(string packageId) =>
            "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
            + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/" + packageId
            + "/prefabs/" + packageId + "_character_preview.prefab";

        private static string ProjectPath(string assetPath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(root, assetPath));
        }
    }
}
