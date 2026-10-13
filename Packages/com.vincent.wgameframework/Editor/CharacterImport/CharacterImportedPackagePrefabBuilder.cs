using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterImport
{
    public sealed class CharacterImportedPackagePrefabBuildResult
    {
        public string Status { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string PrefabPath { get; set; } = string.Empty;
        public string RuntimePrefabPath { get; set; } = string.Empty;
        public string SourcePackageHash { get; set; } = string.Empty;
        public string GeneratedConfigHash { get; set; } = string.Empty;
        public string GeometryBindingHash { get; set; } = string.Empty;
        public string ResourceMappingHash { get; set; } = string.Empty;
        public string WritePlanHash { get; set; } = string.Empty;
        public string ArtifactHash { get; set; } = string.Empty;
        public string AvatarSourceStatus { get; set; } = string.Empty;
        public string AvatarSourceAssetPath { get; set; } = string.Empty;
        public string AvatarSourceGuid { get; set; } = string.Empty;
        public string RigType { get; set; } = string.Empty;
        public string AvatarSourceRigStatus { get; set; } = string.Empty;
        public string AvatarSourceHumanoidStatus { get; set; } = string.Empty;
        public string AvatarSourceMessage { get; set; } = string.Empty;
        public string BodyMaterialBindingStatus { get; set; } = string.Empty;
        public string BodyMaterialTemplateId { get; set; } = string.Empty;
        public string BodyMaterialPath { get; set; } = string.Empty;
        public string BodyMaterialTextureResourceKey { get; set; } = string.Empty;
        public int BodyMaterialRendererCount { get; set; }
        public string PaletteAtlasResourceKey { get; set; } = string.Empty;
        public string PaletteCatalogHash { get; set; } = string.Empty;
        public int PaletteColorCount { get; set; }
        public int SurfaceProfileCount { get; set; }
        public int PaletteInvalidTokenCount { get; set; }
        public int PaletteRendererCount { get; set; }
        public string PaletteDiagnostic { get; set; } = string.Empty;
        public string FaceDetailTextureResourceKey { get; set; } = string.Empty;
        public string FaceDetailTextureHash { get; set; } = string.Empty;
        public int FaceDetailTextureSize { get; set; }
        public string FaceDetailDiagnostic { get; set; } = string.Empty;
        public CharacterGeneratedMaterialBuildResult[] Materials { get; set; } =
            Array.Empty<CharacterGeneratedMaterialBuildResult>();
        public string BuilderVersion { get; set; } = string.Empty;
        public int SocketCount { get; set; }
        public string AbstractBoneStatus { get; set; } = string.Empty;
        public int AbstractBoneDeclaredCount { get; set; }
        public int AbstractBoneResolvedCount { get; set; }
        public int AbstractBoneIssueCount { get; set; }
        public string[] AbstractBoneRequiredIds { get; set; } = new[] { CharacterAbstractBoneIds.Head };
        public string[] AbstractBoneIssues { get; set; } = Array.Empty<string>();
        public int WeaponAttachmentCount { get; set; }
        public int MountedWeaponCount { get; set; }
        public bool CharacterActionProfileReady { get; set; }
        public bool CharacterActionActionSetReady { get; set; }
        public string CharacterActionProfilePath { get; set; } = string.Empty;
        public string CharacterActionSetPath { get; set; } = string.Empty;
        public string WeaponTraceBakeStatus { get; set; } = string.Empty;
        public string WeaponTraceBakeHash { get; set; } = string.Empty;
        public int WeaponTraceBakeRequestCount { get; set; }
        public int WeaponTraceBakeArtifactCount { get; set; }
        public int WeaponTraceBakeCheckedFrameCount { get; set; }
        public int WeaponTraceBakeIssueCount { get; set; }
        public bool WeaponTraceBakeReady { get; set; }
        public CharacterWeaponTraceBakeIssueReport[] WeaponTraceBakeIssues { get; set; } =
            Array.Empty<CharacterWeaponTraceBakeIssueReport>();
        public string SocketBakeStatus { get; set; } = string.Empty;
        public string SocketBakeHash { get; set; } = string.Empty;
        public int SocketBakeRequestCount { get; set; }
        public int SocketBakeClipCount { get; set; }
        public int SocketBakeSocketTrackCount { get; set; }
        public int SocketBakeCheckedFrameCount { get; set; }
        public int SocketBakeIssueCount { get; set; }
        public bool SocketBakeReady { get; set; }

        /// <summary>v3 product identity: format plus the verified index/binary pair.</summary>
        public string SocketBakeFormat { get; set; } = string.Empty;
        public string SocketBakeIndexPath { get; set; } = string.Empty;
        public string SocketBakeIndexHash { get; set; } = string.Empty;
        public string SocketBakeBinaryPath { get; set; } = string.Empty;
        public string SocketBakeBinaryHash { get; set; } = string.Empty;
        public long SocketBakeBinarySize { get; set; }
        public CharacterWeaponTraceBakeIssueReport[] SocketBakeIssues { get; set; } =
            Array.Empty<CharacterWeaponTraceBakeIssueReport>();
        public string HurtboxBakeStatus { get; set; } = string.Empty;
        public string HurtboxBakeHash { get; set; } = string.Empty;
        public int HurtboxBakeClipCount { get; set; }
        public int HurtboxBakeColliderCount { get; set; }
        public int HurtboxBakeShapeCount { get; set; }
        public int HurtboxBakeCheckedFrameCount { get; set; }
        public int HurtboxBakeIssueCount { get; set; }
        public bool HurtboxBakeReady { get; set; }
        public CharacterWeaponTraceBakeIssueReport[] HurtboxBakeIssues { get; set; } =
            Array.Empty<CharacterWeaponTraceBakeIssueReport>();
        public string[] DefaultWeaponResourceIds { get; set; } = Array.Empty<string>();
        public string[] Issues { get; set; } = Array.Empty<string>();
    }

    public sealed class CharacterGeneratedMaterialBuildResult
    {
        public string SourceMaterialName { get; set; } = string.Empty;
        public string MaterialPath { get; set; } = string.Empty;
        public string AlbedoTextureResourceKey { get; set; } = string.Empty;
        public string NormalTextureResourceKey { get; set; } = string.Empty;
        public string MaskTextureResourceKey { get; set; } = string.Empty;
        public string SurfaceMode { get; set; } = "Opaque";
        public float AlphaCutoff { get; set; } = 0.5f;
        public int AssignedSlotCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class CharacterWeaponTraceBakeIssueReport
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ActionId { get; set; }
        public string ClipKey { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public int Frame { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private const string BuilderVersion = "CharacterImportedPackagePrefabBuilder.v2.9";
        private const string DefaultImportedPackageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000001";
        private const string PreviewScenePath = "Assets/Scenes/MxFramework/CharacterImportedPreview.unity";
        private const string CalibrationScenePath = "Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity";
        private const string RuntimeResourcesRootName = "runtime_resources";
        private const string UnityResourcesFolderName = "Resources";
        private const string RuntimeResourcesAddressRoot = "MxFrameworkGenerated/CharacterPackages";
        private const string GeneratedCharacterActionsRoot = "Assets/MxFrameworkGenerated/CharacterActions";
        private const string DefaultCharacterActionLibraryId = "Common";
        private const string DefaultReactionProfileSlotId = "cohesionBreak";
        private const string DefaultDeathReactionAnimationResourceKey =
            "art.character.skeleton.animation.damage_ko_mid_2";
        private const string DefaultCharacterBodyMaterialTemplateId = "wgame.character_traditional_lit";
        private const string OpaqueMaterialSurfaceMode = "Opaque";
        private const string AlphaClipMaterialSurfaceMode = "AlphaClip";
        private const string AlphaClipKeyword = "_ALPHATEST_ON";
        private const string CharacterBodyMaterialRole = "character.body";
        private const string CharacterBodyAlbedoResourceSuffix = ".texture.body_albedo";
        private const string RuntimeDebugPanelSettingsPath = "Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokePanelSettings.asset";
        private const string DefaultResourceMappingProfilePath =
            "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";
        private static string _resourceMappingProfilePathOverride;
        private static string _previewMaterialFolder;

        private static string ResourceMappingProfilePath =>
            string.IsNullOrWhiteSpace(_resourceMappingProfilePathOverride)
                ? DefaultResourceMappingProfilePath
                : _resourceMappingProfilePathOverride;

        internal static void SetResourceMappingProfilePathOverrideForTests(string profilePath)
        {
            _resourceMappingProfilePathOverride = profilePath ?? string.Empty;
        }

        public static void CreateDefaultPreviewPrefab()
        {
            BuildPrefab(DefaultImportedPackageRoot, selectPrefab: true);
        }

        public static void CreatePreviewPrefabFromFolder()
        {
            string root = PromptForImportedPackageRoot();
            if (string.IsNullOrWhiteSpace(root))
                return;

            BuildPrefab(root, selectPrefab: true);
        }

        public static void CreateDefaultPreviewScene()
        {
            CreateOrOverwriteRuntimeScene(
                DefaultImportedPackageRoot,
                PreviewScenePath,
                ensureCalibrationRunner: false,
                newSceneMode: NewSceneMode.Single,
                selectLoader: true);
        }

        public static void CreateDefaultLocomotionCalibrationScene()
        {
            CreateOrOverwriteRuntimeScene(
                DefaultImportedPackageRoot,
                CalibrationScenePath,
                ensureCalibrationRunner: true,
                newSceneMode: NewSceneMode.Single,
                selectLoader: true);
        }

        public static void CreateLocomotionCalibrationScene(
            string importedPackageRoot,
            string scenePath,
            bool selectLoader = true)
        {
            if (string.IsNullOrWhiteSpace(importedPackageRoot))
                throw new ArgumentException("Imported package root is required.", nameof(importedPackageRoot));
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new ArgumentException("Calibration scene path is required.", nameof(scenePath));

            CreateOrOverwriteRuntimeScene(
                NormalizeProjectPath(importedPackageRoot),
                NormalizeProjectPath(scenePath),
                ensureCalibrationRunner: true,
                newSceneMode: NewSceneMode.Single,
                selectLoader: selectLoader);
        }

        [MenuItem("MxFramework/Character Package/Rebuild Runtime Entry Points", priority = 220)]
        public static void SyncDefaultRuntimeEntryPoints()
        {
            SynchronizeRuntimeEntryPoints(DefaultImportedPackageRoot, selectPrefab: true);
        }

        public static void SyncRuntimeEntryPointsFromFolder()
        {
            string root = PromptForImportedPackageRoot();
            if (string.IsNullOrWhiteSpace(root))
                return;

            SynchronizeRuntimeEntryPoints(root, selectPrefab: true);
        }

        public static string SynchronizeRuntimeEntryPoints(string importedPackageRoot, bool selectPrefab)
        {
            CharacterImportedPackagePrefabBuildResult result = SynchronizeRuntimeEntryPointsWithReport(importedPackageRoot, selectPrefab);
            return string.Equals(result.Status, "Built", StringComparison.Ordinal)
                ? result.PrefabPath
                : string.Empty;
        }

        public static CharacterImportedPackagePrefabBuildResult SynchronizeRuntimeEntryPointsWithReport(string importedPackageRoot, bool selectPrefab)
        {
            return SynchronizeRuntimeEntryPointsWithReport(importedPackageRoot, selectPrefab, assetsAlreadyImported: false);
        }

        /// <summary>
        /// LowFreqAlloc: rebuilds imported character runtime entry points without reusing the prefab build cache.
        /// </summary>
        public static CharacterImportedPackagePrefabBuildResult ForceSynchronizeRuntimeEntryPointsWithReport(
            string importedPackageRoot,
            bool selectPrefab)
        {
            return SynchronizeRuntimeEntryPointsWithReport(
                importedPackageRoot,
                selectPrefab,
                assetsAlreadyImported: false,
                forceRebuild: true);
        }

        internal static CharacterImportedPackagePrefabBuildResult SynchronizeRuntimeEntryPointsWithReport(
            string importedPackageRoot,
            bool selectPrefab,
            bool assetsAlreadyImported)
        {
            return SynchronizeRuntimeEntryPointsWithReport(
                importedPackageRoot,
                selectPrefab,
                assetsAlreadyImported,
                forceRebuild: false);
        }

        private static CharacterImportedPackagePrefabBuildResult SynchronizeRuntimeEntryPointsWithReport(
            string importedPackageRoot,
            bool selectPrefab,
            bool assetsAlreadyImported,
            bool forceRebuild)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackagePrefabBuildResult result = BuildPrefabWithReport(
                root,
                selectPrefab,
                assetsAlreadyImported,
                forceRebuild);
            string prefabPath = result.PrefabPath;
            if (!string.Equals(result.Status, "Built", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(prefabPath))
                return result;

            CharacterImportedPackage package = LoadImportedPackage(root, "runtime entry-point sync");
            if (package == null)
            {
                result.Status = "Failed";
                result.Message = "Imported package could not be loaded after prefab build.";
                result.Issues = AppendIssue(result.Issues, result.Message);
                return result;
            }

            SynchronizeRuntimeScene(root, package, prefabPath, PreviewScenePath, ensureCalibrationRunner: false);
            SynchronizeRuntimeScene(root, package, prefabPath, CalibrationScenePath, ensureCalibrationRunner: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MxFramework Character runtime entry points synchronized: " + root);
            return result;
        }

        public static string BuildPrefab(string importedPackageRoot, bool selectPrefab)
        {
            string prefabPath = BuildPrefabCore(importedPackageRoot, selectPrefab, importAssets: true);
            return prefabPath;
        }

        public static CharacterImportedPackagePrefabBuildResult BuildPrefabWithReport(string importedPackageRoot, bool selectPrefab)
        {
            return BuildPrefabWithReport(importedPackageRoot, selectPrefab, assetsAlreadyImported: false);
        }

        private static CharacterImportedPackagePrefabBuildResult BuildPrefabWithReport(
            string importedPackageRoot,
            bool selectPrefab,
            bool assetsAlreadyImported)
        {
            return BuildPrefabWithReport(
                importedPackageRoot,
                selectPrefab,
                assetsAlreadyImported,
                forceRebuild: false);
        }

        private static CharacterImportedPackagePrefabBuildResult BuildPrefabWithReport(
            string importedPackageRoot,
            bool selectPrefab,
            bool assetsAlreadyImported,
            bool forceRebuild)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackage package = LoadImportedPackage(root, "prefab build cache lookup");
            if (package == null)
            {
                string failMsg = !string.IsNullOrWhiteSpace(s_lastPackageLoadErrorMessage)
                    ? s_lastPackageLoadErrorMessage
                    : "Imported package could not be loaded.";
                return CreateFailedBuildResult(root, failMsg);
            }

            if (!ValidateGeneratedPackageMaterialTargets(package, root, out string materialTargetIssue))
                return CreateFailedBuildResult(root, materialTargetIssue);

            string prefabPath = GetPreviewPrefabPath(package);
            string prefabInputHash = ComputePrefabBuildInputHash(package, root);
            if (forceRebuild || !TryReusePrefabBuild(root, package, prefabPath, prefabInputHash))
            {
                prefabPath = BuildPrefabCore(root, selectPrefab, importAssets: !assetsAlreadyImported);
                if (!string.IsNullOrWhiteSpace(prefabPath))
                    WritePrefabBuildCache(root, package, prefabPath, prefabInputHash);
            }
            if (string.IsNullOrWhiteSpace(prefabPath))
                return CreateFailedBuildResult(root, "Prefab build did not produce a prefab path.");

            return CreatePrefabBuildResult(root, prefabPath);
        }

        private static string ComputePrefabBuildInputHash(
            CharacterImportedPackage package,
            string importedPackageRoot)
        {
            if (package == null)
                return string.Empty;
            return ComputeStringContentHash(string.Join(
                "|",
                BuilderVersion,
                package.ImportReport?.SourcePackageHash ?? string.Empty,
                package.ImportReport?.GeneratedConfigHash ?? string.Empty,
                package.ImportReport?.GeometryBindingHash ?? string.Empty,
                package.ImportReport?.ResourceMappingHash ?? string.Empty,
                package.ImportReport?.WritePlanHash ?? string.Empty,
                ComputeUnityArtifactAggregateHash(package.UnityResourceCatalog),
                ComputePrefabConfigAggregateHash(importedPackageRoot)));
        }

        internal static string ComputePrefabConfigAggregateHash(string importedPackageRoot)
        {
            string configRoot = Path.GetFullPath(Path.Combine(importedPackageRoot ?? string.Empty, "config"));
            if (!Directory.Exists(configRoot)) return string.Empty;
            string[] files = Directory.GetFiles(configRoot, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var values = new List<string>(files.Length);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string fullPath = Path.GetFullPath(files[fileIndex]);
                string relativePath = fullPath.Substring(configRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                string contents = File.ReadAllText(fullPath).Replace("\r\n", "\n");
                values.Add(relativePath + "=" + ComputeStringContentHash(contents));
            }
            return values.Count == 0
                ? string.Empty
                : ComputeStringContentHash(string.Join("\n", values));
        }

        private static bool TryReusePrefabBuild(
            string importedRoot,
            CharacterImportedPackage package,
            string prefabPath,
            string inputHash)
        {
            if (package == null || string.IsNullOrWhiteSpace(prefabPath) || string.IsNullOrWhiteSpace(inputHash))
                return false;
            string runtimePath = GetRuntimeResourcesAssetPath(importedRoot, package.PackageId, prefabPath);
            JObject cache = ReadEditorImportCache(importedRoot, "prefab");
            return string.Equals((string)cache?["inputHash"], inputHash, StringComparison.Ordinal)
                && CachedFileMatches(prefabPath, cache?["prefab"] as JObject)
                && CachedFileMatches(runtimePath, cache?["runtimePrefab"] as JObject)
                && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(runtimePath) != null;
        }

        private static void WritePrefabBuildCache(
            string importedRoot,
            CharacterImportedPackage package,
            string prefabPath,
            string inputHash)
        {
            string runtimePath = GetRuntimeResourcesAssetPath(importedRoot, package.PackageId, prefabPath);
            WriteEditorImportCache(importedRoot, "prefab", new JObject
            {
                ["inputHash"] = inputHash ?? string.Empty,
                ["prefab"] = CaptureCachedFile(prefabPath),
                ["runtimePrefab"] = CaptureCachedFile(runtimePath)
            });
        }

        private static JObject ReadEditorImportCache(string importedRoot, string name)
        {
            string path = GetEditorImportCachePath(importedRoot, name);
            if (!File.Exists(path))
                return null;
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (Exception) { return null; }
        }

        private static void WriteEditorImportCache(string importedRoot, string name, JObject payload)
        {
            string path = GetEditorImportCachePath(importedRoot, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            payload["format"] = "mx.characterEditorImportCache.v1";
            string json = payload.ToString(Formatting.Indented) + "\n";
            if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), json, StringComparison.Ordinal))
                File.WriteAllText(path, json);
        }

        private static string GetEditorImportCachePath(string importedRoot, string name)
        {
            string key = Hash128.Compute(Path.GetFullPath(importedRoot ?? string.Empty)).ToString();
            return Path.GetFullPath(Path.Combine(
                "Library",
                "MxFrameworkAuthoringCache",
                "CharacterImport",
                key,
                (name ?? "cache") + ".json"));
        }

        private static JObject CaptureCachedFile(string path)
        {
            var file = new FileInfo(path ?? string.Empty);
            return new JObject
            {
                ["length"] = file.Exists ? file.Length : -1L,
                ["lastWriteUtcTicks"] = file.Exists ? file.LastWriteTimeUtc.Ticks : 0L
            };
        }

        private static bool CachedFileMatches(string path, JObject cached)
        {
            var file = new FileInfo(path ?? string.Empty);
            return file.Exists
                && cached != null
                && (long?)cached["length"] == file.Length
                && (long?)cached["lastWriteUtcTicks"] == file.LastWriteTimeUtc.Ticks;
        }

        /// <summary>
        /// LowFreqAlloc: rebuilds only the package-local weapon trace bake catalog from the existing generated preview prefab.
        /// </summary>
        public static CharacterWeaponTraceBakeBuildReport GenerateWeaponTraceBakeCatalog(string importedPackageRoot)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackage package = LoadImportedPackage(root, "weapon trace bake catalog generation");
            if (package == null)
            {
                return new CharacterWeaponTraceBakeBuildReport(
                    0,
                    0,
                    0,
                    new[]
                    {
                        CreateWeaponTraceBakeIssue(
                            CharacterWeaponTraceBakeCatalogBuilder.IssuePackageMissing,
                            "Imported package could not be loaded before weapon trace bake catalog generation.",
                            CharacterActionId.None,
                            string.Empty,
                            0,
                            -1,
                            root,
                            "package")
                    });
            }

            Dictionary<string, ResourcePreviewInfo> resources = ReadResourcePreviewInfos(package, root);
            BuildWeaponPrefabs(package, resources, root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool hasActionRegistry = TryLoadCharacterActionRegistryForPackage(
                package,
                out CharacterActionConfigRegistry registry,
                out string registryIssue);
            if (!hasActionRegistry)
            {
                return new CharacterWeaponTraceBakeBuildReport(
                    0,
                    0,
                    0,
                    new[]
                    {
                        CreateWeaponTraceBakeIssue(
                            "WeaponTraceBakeActionRegistryMissing",
                            registryIssue,
                            CharacterActionId.None,
                            string.Empty,
                            0,
                            -1,
                            "config/character_actions.json",
                            "actions")
                    });
            }

            CharacterWeaponTraceBakeRequest[] requests = CreateWeaponTraceBakeRequests(package, registry);

            CharacterWeaponTraceBakeCatalog catalog = BuildSampledWeaponTraceBakeCatalog(
                package,
                requests,
                out CharacterWeaponTraceBakeCatalog contacts,
                out CharacterWeaponTraceBakeBuildReport report);
            if (report.Success)
            {
                WriteWeaponTraceBakeCatalog(package, requests, catalog, contacts);
                AssetDatabase.ImportAsset(
                    NormalizeProjectPath(package.RootPath) + "/" + CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath,
                    ImportAssetOptions.ForceUpdate);
            }

            return report;
        }

        private static string BuildPrefabCore(string importedPackageRoot, bool selectPrefab, bool importAssets)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
            {
                Debug.LogError("MxFramework Character Preview: imported package root was not found: " + root);
                return string.Empty;
            }

            CharacterImportedPackage package;
            CharacterRuntimeSpawnResult spawnResult;
            try
            {
                package = CharacterImportedPackageJson.LoadFromDirectory(fullRoot);
                spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            }
            catch (Exception ex)
            {
                Debug.LogError("MxFramework Character Preview: failed to load imported package: " + ex.Message);
                return string.Empty;
            }

            if (!ValidateGeneratedPackageMaterialTargets(package, root, out string materialTargetIssue))
            {
                Debug.LogError("MxFramework Character Preview: " + materialTargetIssue);
                return string.Empty;
            }

            if (importAssets)
            {
                AssetDatabase.ImportAsset(
                    root,
                    ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            }

            if (!spawnResult.IsSuccess)
            {
                if (!CanBuildPresentationOnlyPrefab(package, spawnResult))
                {
                    Debug.LogError("MxFramework Character Preview: runtime binding failed: " + FormatIssues(spawnResult));
                    return string.Empty;
                }

                Debug.Log(
                    "MxFramework Character Preview: building an import-ready presentation prefab without "
                    + "CharacterTest combat body parts. package=" + package.PackageId);
            }

            SynchronizeCharacterActionProfile(package);

            Dictionary<string, ResourcePreviewInfo> resources = ReadResourcePreviewInfos(package, root);
            if (!ValidatePreviewResources(resources))
                return string.Empty;

            EnsureFolder(root, "preview_materials");
            _previewMaterialFolder = root + "/preview_materials";
            BuildWeaponPrefabs(package, resources, root);
            AnimationClip[] animationClips = LoadAnimationClips(package, root);
            ResourcePreviewInfo bodyResource = FindResource(resources, usage: "characterModel", resourceKey: string.Empty);
            AvatarSourceStatus avatarSource = CollectAvatarSourceStatus(package);
            ResourcePreviewInfo runtimeBodyResource = ResolveRuntimeModelResource(bodyResource, root, animationClips, avatarSource.RigType);
            GameObject rootObject = new GameObject(package.PackageId + "_CharacterPreview");
            try
            {
                rootObject.SetActive(false);
                var modelRoot = new GameObject("ModelRoot").transform;
                modelRoot.SetParent(rootObject.transform, false);

                var socketsRoot = new GameObject("Sockets").transform;
                socketsRoot.SetParent(rootObject.transform, false);

                var collidersRoot = new GameObject("AuthoringColliders").transform;
                collidersRoot.SetParent(rootObject.transform, false);

                var diagnosticsRoot = new GameObject("Diagnostics").transform;
                diagnosticsRoot.SetParent(rootObject.transform, false);

                Transform bodyModel = CreateBodyModel(runtimeBodyResource, modelRoot);
                bool hideEmbeddedWeapons = package != null
                    && package.Geometry != null
                    && package.Geometry.WeaponAttachments != null
                    && package.Geometry.WeaponAttachments.Length > 0;
                ApplyBodyModelNodeVisibility(bodyModel, runtimeBodyResource, hideEmbeddedWeapons);
                EnsureBodyAnimator(bodyModel, animationClips, root, runtimeBodyResource?.AssetPath, avatarSource.RigType);
                ApplyBodyMaterialBinding(package, resources, root, bodyModel);
                AddPaletteBindingComponent(rootObject, bodyModel, root, resources);
                var boneBindings = new List<TransformBoneBindingInfo>();
                var resolvedSocketIds = new HashSet<int>();
                Dictionary<int, Transform> sockets = CreateSockets(
                    package,
                    socketsRoot,
                    bodyModel,
                    boneBindings,
                    resolvedSocketIds);
                AddAbstractBoneRuntimeMap(rootObject, package, sockets, resolvedSocketIds);
                CreateBodyPartContactPointMarkers(package, socketsRoot, sockets);
                // Runtime character prefabs must not persist concrete weapon model instances.
                // Weapon visuals are generated as independent prefabs and mounted by runtime equipment flow.
                CreateColliders(package, collidersRoot, bodyModel, boneBindings);
                CharacterImportedPackagePrefabPostprocessorRunner.Process(
                    new CharacterImportedPackagePrefabPostprocessContext(
                        rootObject,
                        modelRoot,
                        bodyModel,
                        root,
                        package));
                CreateResourceDiagnostics(resources, diagnosticsRoot);
                AddRuntimePrefabDescriptor(rootObject, package);

                string prefabFolder = root + "/prefabs";
                EnsureFolder(root, "prefabs");
                string prefabPath = prefabFolder + "/" + package.PackageId + "_character_preview.prefab";
                rootObject.SetActive(true);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
                MirrorPrefabToRuntimeResources(root, package.PackageId, prefabPath);
                AssetDatabase.SaveAssets();
                if (selectPrefab && prefab != null)
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }

                Debug.Log("MxFramework Character preview prefab created: " + prefabPath);
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static bool CanBuildPresentationOnlyPrefab(
            CharacterImportedPackage package,
            CharacterRuntimeSpawnResult spawnResult)
        {
            if (package?.Configs?.BodyParts == null || package.Configs.BodyParts.Length != 0 || spawnResult?.Issues == null)
                return false;

            for (int i = 0; i < spawnResult.Issues.Length; i++)
            {
                CharacterRuntimeSpawnIssue issue = spawnResult.Issues[i];
                if (!issue.IsError)
                    continue;
                if (string.Equals(issue.Code, "CHAR_INVALID_BODY_PART_SET", StringComparison.Ordinal)
                    || string.Equals(issue.Code, CharacterRuntimeSpawnIssueCodes.ResolverFailed, StringComparison.Ordinal))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static string PromptForImportedPackageRoot()
        {
            string root = EditorUtility.OpenFolderPanel(
                "选择已导入的 Character Package",
                Path.GetFullPath("Assets/MxFrameworkGenerated/CharacterPackages"),
                string.Empty);
            return string.IsNullOrWhiteSpace(root) ? string.Empty : ToProjectRelativePath(root);
        }

        private static string s_lastPackageLoadErrorCode = string.Empty;
        private static string s_lastPackageLoadErrorMessage = string.Empty;

        private static CharacterImportedPackage LoadImportedPackage(string importedPackageRoot, string operation)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            s_lastPackageLoadErrorCode = string.Empty;
            s_lastPackageLoadErrorMessage = string.Empty;
            try
            {
                return CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(root));
            }
            catch (CharacterImportedPackageJsonException jsonEx)
            {
                s_lastPackageLoadErrorCode = "UNITY_IMPORTED_PACKAGE_PARSE_FAILED";
                s_lastPackageLoadErrorMessage = jsonEx.Message;
                Debug.LogError("MxFramework Character Preview: failed to parse imported package during " + operation + ". root="
                    + root + " error=" + jsonEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                s_lastPackageLoadErrorCode = "UNITY_IMPORTED_PACKAGE_LOAD_FAILED";
                s_lastPackageLoadErrorMessage = ex.Message;
                Debug.LogError("MxFramework Character Preview: failed to load imported package during " + operation + ". root="
                    + root + " error=" + ex.Message);
                return null;
            }
        }

        private static CharacterImportedPackagePrefabBuildResult CreateFailedBuildResult(string importedPackageRoot, string message)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackagePrefabBuildResult result = CreateBaseBuildResult(root, null);
            result.Status = "Failed";
            result.Message = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(s_lastPackageLoadErrorCode))
            {
                result.ErrorCode = s_lastPackageLoadErrorCode;
                if (!string.IsNullOrWhiteSpace(s_lastPackageLoadErrorMessage))
                    result.Message = s_lastPackageLoadErrorMessage;
            }
            result.Issues = string.IsNullOrWhiteSpace(result.Message) ? Array.Empty<string>() : new[] { result.Message };
            return result;
        }

        private static CharacterImportedPackagePrefabBuildResult CreatePrefabBuildResult(string importedPackageRoot, string prefabPath)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackage package = LoadImportedPackage(root, "prefab build status collection");
            CharacterImportedPackagePrefabBuildResult result = CreateBaseBuildResult(root, package);
            result.Status = "Built";
            result.Message = "Unity Editor rebuilt imported character prefab.";
            result.PrefabPath = prefabPath ?? string.Empty;
            result.RuntimePrefabPath = package != null
                ? GetRuntimeResourcesAssetPath(root, package.PackageId, prefabPath)
                : string.Empty;

            GameObject prefab = string.IsNullOrWhiteSpace(prefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var issues = new List<string>();
            if (prefab == null)
            {
                issues.Add("Prefab asset could not be loaded after build: " + prefabPath);
            }
            else
            {
                CharacterRuntimePrefabDescriptor[] descriptors = prefab.GetComponents<CharacterRuntimePrefabDescriptor>();
                if (descriptors.Length != 1)
                    issues.Add("Generated prefab must contain exactly one CharacterRuntimePrefabDescriptor.");
                else if (!string.Equals(descriptors[0].CharacterResourceId, CharacterRuntimePrefabResourceCatalog.GetCharacterPrefabResourceId(package?.PackageId), StringComparison.Ordinal))
                    issues.Add("CharacterRuntimePrefabDescriptor has an unexpected character resource id.");

                AddSlimPrefabComponentIssues(prefab, issues);
                GameObject runtimePrefab = string.IsNullOrWhiteSpace(result.RuntimePrefabPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(result.RuntimePrefabPath);
                CollectAbstractBoneStatus(prefab, runtimePrefab, package, result);
                CharacterBodyMaterialBindingStatus bodyMaterialStatus = CollectBodyMaterialBindingStatus(root, package, prefab);
                ApplyBodyMaterialBindingStatus(result, bodyMaterialStatus);
                PopulatePaletteBuildResult(result, root, prefab);
                result.Materials = CollectGeneratedMaterialBuildResults(root, package, prefab);
                if (bodyMaterialStatus.ShouldWarn)
                    issues.Add("Body material binding status=" + bodyMaterialStatus.Status + ": " + bodyMaterialStatus.Message);

                Animator animator = prefab.GetComponentInChildren<Animator>(includeInactive: true);
                bool validRigAvatar = animator != null
                    && animator.avatar != null
                    && animator.avatar.isValid
                    && (string.Equals(result.RigType, "Generic", StringComparison.Ordinal)
                        ? !animator.avatar.isHuman
                        : animator.avatar.isHuman);
                if (validRigAvatar)
                {
                    result.AvatarSourceStatus = "Valid";
                    result.AvatarSourceRigStatus = string.Equals(result.RigType, "Generic", StringComparison.Ordinal)
                        ? "ValidGeneric"
                        : "ValidHuman";
                    result.AvatarSourceHumanoidStatus = string.Equals(result.RigType, "Humanoid", StringComparison.Ordinal)
                        ? "ValidHuman"
                        : string.Empty;
                    if (string.IsNullOrWhiteSpace(result.AvatarSourceAssetPath))
                        result.AvatarSourceAssetPath = NormalizeProjectPath(AssetDatabase.GetAssetPath(animator.avatar));
                    result.AvatarSourceMessage = "Generated prefab uses a valid " + result.RigType + " Avatar from the package Avatar source.";
                }
                else if (!string.IsNullOrWhiteSpace(result.RigType))
                {
                    issues.Add("Generated prefab Animator does not use a valid " + result.RigType + " Avatar from the package character model.");
                }

                result.DefaultWeaponResourceIds = CollectDefaultWeaponResourceIds(package);
                result.MountedWeaponCount = result.DefaultWeaponResourceIds.Length;
                if (package?.Configs?.BodyParts != null && package.Configs.BodyParts.Length > 0)
                    RunPrefabCompositionSmoke(prefab, package, issues);
            }

            if (package != null && package.Geometry != null && package.Geometry.WeaponAttachments.Length > 0 && result.MountedWeaponCount == 0)
                issues.Add("Package has weapon attachments but no default weapon runtime resources were resolved.");

            if (!result.WeaponTraceBakeReady)
            {
                result.Status = "Failed";
                issues.Add("Weapon trace bake is not ready; Unity import cannot produce a spawn-ready character.");
                for (int i = 0; i < result.WeaponTraceBakeIssues.Length; i++)
                {
                    CharacterWeaponTraceBakeIssueReport issue = result.WeaponTraceBakeIssues[i];
                    issues.Add("Weapon trace bake "
                        + issue.Code
                        + " action=#" + issue.ActionId
                        + " clip=" + issue.ClipKey
                        + " trace=" + issue.TraceId
                        + " frame=" + issue.Frame
                        + ": " + issue.Message);
                }
            }

            if (!result.SocketBakeReady)
            {
                result.Status = "Failed";
                issues.Add("Socket bake is not ready; baked socket hit volumes fail closed.");
                for (int i = 0; i < result.SocketBakeIssues.Length; i++)
                {
                    CharacterWeaponTraceBakeIssueReport issue = result.SocketBakeIssues[i];
                    issues.Add("Socket bake " + issue.Code
                        + " clip=" + issue.ClipKey
                        + " socket=" + issue.TraceId
                        + " frame=" + issue.Frame
                        + ": " + issue.Message);
                }
            }

            if (!result.HurtboxBakeReady)
            {
                result.Status = "Failed";
                if (result.HurtboxBakeIssues == null || result.HurtboxBakeIssues.Length == 0)
                {
                    issues.Add("Hurtbox bake is not ready; explicit character hurtboxes fail closed.");
                }
                else
                {
                    for (int i = 0; i < result.HurtboxBakeIssues.Length; i++)
                    {
                        CharacterWeaponTraceBakeIssueReport issue = result.HurtboxBakeIssues[i];
                        issues.Add("Hurtbox bake " + issue.Code
                            + " clip=" + issue.ClipKey
                            + " collider=" + issue.TraceId
                            + " frame=" + issue.Frame
                            + ": " + issue.Message);
                    }
                }
            }

            if (!string.Equals(result.AbstractBoneStatus, "Ready", StringComparison.Ordinal))
            {
                result.Status = "Failed";
                for (int i = 0; i < result.AbstractBoneIssues.Length; i++)
                    issues.Add(result.AbstractBoneIssues[i]);
            }

            result.Issues = issues.ToArray();
            if (string.Equals(result.Status, "Failed", StringComparison.Ordinal))
                result.Message = "Unity Editor could not produce a spawn-ready imported character.";
            else if (issues.Count > 0)
                result.Message = "Unity Editor rebuilt imported character prefab with diagnostics.";
            return result;
        }

        private static CharacterBodyMaterialBindingStatus CollectBodyMaterialBindingStatus(
            string importedPackageRoot,
            CharacterImportedPackage package,
            GameObject prefab)
        {
            Dictionary<string, ResourcePreviewInfo> resources = ReadResourcePreviewInfos(package, importedPackageRoot);
            CharacterBodyMaterialBindingStatus paletteStatus = CollectPaletteBodyMaterialBindingStatus(
                importedPackageRoot,
                package,
                prefab,
                out bool paletteHandled);
            if (paletteHandled)
                return paletteStatus;

            Transform bodyModel = prefab != null ? FindChildByName(prefab.transform, "BodyModel") : null;
            CharacterBodyMaterialBindingStatus descriptorStatus = CollectPackageMaterialDescriptorBindingStatus(
                package,
                importedPackageRoot,
                bodyModel);
            if (!string.Equals(descriptorStatus.Status, "noPackageMaterialDescriptors", StringComparison.Ordinal))
                return descriptorStatus;

            CharacterBodyMaterialBindingStatus status = ResolveBodyMaterialBindingInputs(
                package,
                resources,
                importedPackageRoot,
                bodyModel,
                out ResourcePreviewInfo albedoResource,
                out Texture2D albedoTexture,
                out CharacterMaterialTemplate template,
                out Renderer[] renderers);
            if (!status.CanBind)
                return status;

            string materialPath = GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyMaterialMissing",
                    template.TemplateId,
                    materialPath,
                    albedoResource?.Key,
                    0,
                    "Generated body material asset is missing.");
            }

            int assignedRendererCount = CountRenderersUsingMaterial(renderers, material, out bool allSlotsAssigned);
            if (!allSlotsAssigned)
            {
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyMaterialNotAssigned",
                    template.TemplateId,
                    materialPath,
                    albedoResource?.Key,
                    assignedRendererCount,
                    "Generated body material exists but is not assigned to every body renderer material slot.");
            }

            return CharacterBodyMaterialBindingStatus.Create(
                "bodyMaterialBound",
                template.TemplateId,
                materialPath,
                albedoResource?.Key,
                assignedRendererCount,
                "Generated body material is assigned to every body renderer material slot.");
        }

        private static void ApplyBodyMaterialBindingStatus(
            CharacterImportedPackagePrefabBuildResult result,
            CharacterBodyMaterialBindingStatus status)
        {
            if (result == null)
                return;

            result.BodyMaterialBindingStatus = status.Status;
            result.BodyMaterialTemplateId = status.TemplateId;
            result.BodyMaterialPath = status.MaterialPath;
            result.BodyMaterialTextureResourceKey = status.TextureResourceKey;
            result.BodyMaterialRendererCount = status.RendererCount;
        }

        private static int CountRenderersUsingMaterial(Renderer[] renderers, Material material, out bool allSlotsAssigned)
        {
            allSlotsAssigned = material != null;
            if (renderers == null || renderers.Length == 0 || material == null)
            {
                allSlotsAssigned = false;
                return 0;
            }

            int assignedCount = 0;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    allSlotsAssigned = false;
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    if (renderer.sharedMaterial == material)
                        assignedCount++;
                    else
                        allSlotsAssigned = false;
                    continue;
                }

                bool rendererAssigned = true;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != material)
                    {
                        rendererAssigned = false;
                        break;
                    }
                }

                if (rendererAssigned)
                    assignedCount++;
                else
                    allSlotsAssigned = false;
            }

            return assignedCount;
        }

        private static CharacterImportedPackagePrefabBuildResult CreateBaseBuildResult(string importedPackageRoot, CharacterImportedPackage package)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            string packageId = package != null ? package.PackageId : string.Empty;

            if (package == null)
            {
                return new CharacterImportedPackagePrefabBuildResult
                {
                    Status = "Failed",
                    Message = "Imported package could not be loaded.",
                    PackageId = packageId,
                    BuilderVersion = BuilderVersion,
                    AbstractBoneStatus = "BlockedByUpstream",
                    WeaponTraceBakeStatus = "BlockedByUpstream",
                    SocketBakeStatus = "BlockedByUpstream",
                    HurtboxBakeStatus = "BlockedByUpstream",
                    WeaponTraceBakeReady = false,
                    SocketBakeReady = false,
                    HurtboxBakeReady = false,
                    WeaponTraceBakeIssues = Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                    SocketBakeIssues = Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                    HurtboxBakeIssues = Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                    Issues = Array.Empty<string>()
                };
            }

            string profileDirectory = GeneratedCharacterActionsRoot + "/Profiles/" + SanitizeName(packageId);
            string actionSetDirectory = GeneratedCharacterActionsRoot + "/ActionSets/" + SanitizeName(packageId);
            string profilePath = string.IsNullOrWhiteSpace(packageId) ? string.Empty : profileDirectory + "/" + CharacterActionJsonImporter.DefaultProfilesSourcePath;
            string actionSetPath = string.IsNullOrWhiteSpace(packageId) ? string.Empty : actionSetDirectory + "/" + CharacterActionJsonImporter.DefaultActionSetsSourcePath;
            AvatarSourceStatus avatarSource = CollectAvatarSourceStatus(package);
            WeaponTraceBakeStatus weaponTraceBake = CollectWeaponTraceBakeStatus(package);
            SocketBakeStatus socketBake = CollectSocketBakeStatus(package);
            HurtboxBakeStatus hurtboxBake = CollectHurtboxBakeStatus(package);

            return new CharacterImportedPackagePrefabBuildResult
            {
                Status = string.Empty,
                Message = string.Empty,
                PackageId = packageId,
                SourcePackageHash = package != null && package.ImportReport != null ? package.ImportReport.SourcePackageHash : string.Empty,
                GeneratedConfigHash = package != null && package.ImportReport != null ? package.ImportReport.GeneratedConfigHash : string.Empty,
                GeometryBindingHash = package != null && package.ImportReport != null ? package.ImportReport.GeometryBindingHash : string.Empty,
                ResourceMappingHash = package != null && package.ImportReport != null ? package.ImportReport.ResourceMappingHash : string.Empty,
                WritePlanHash = package != null && package.ImportReport != null ? package.ImportReport.WritePlanHash : string.Empty,
                ArtifactHash = package != null ? ComputeUnityArtifactAggregateHash(package.UnityResourceCatalog) : string.Empty,
                AvatarSourceStatus = avatarSource.Status,
                AvatarSourceAssetPath = avatarSource.AssetPath,
                AvatarSourceGuid = avatarSource.Guid,
                RigType = avatarSource.RigType,
                AvatarSourceRigStatus = avatarSource.RigStatus,
                AvatarSourceHumanoidStatus = avatarSource.HumanoidStatus,
                AvatarSourceMessage = avatarSource.Message,
                BuilderVersion = BuilderVersion,
                SocketCount = package != null && package.Geometry != null ? package.Geometry.Sockets.Length : 0,
                AbstractBoneStatus = "Deferred",
                AbstractBoneRequiredIds = ResolveRequiredAbstractBoneIds(package),
                WeaponAttachmentCount = package != null && package.Geometry != null ? package.Geometry.WeaponAttachments.Length : 0,
                MountedWeaponCount = 0,
                CharacterActionProfileReady = !string.IsNullOrWhiteSpace(profilePath) && File.Exists(profilePath),
                CharacterActionActionSetReady = !string.IsNullOrWhiteSpace(actionSetPath) && File.Exists(actionSetPath),
                CharacterActionProfilePath = profilePath,
                CharacterActionSetPath = actionSetPath,
                WeaponTraceBakeStatus = weaponTraceBake.Status,
                WeaponTraceBakeHash = weaponTraceBake.Hash,
                WeaponTraceBakeRequestCount = weaponTraceBake.RequestCount,
                WeaponTraceBakeArtifactCount = weaponTraceBake.ArtifactCount,
                WeaponTraceBakeCheckedFrameCount = weaponTraceBake.CheckedFrameCount,
                WeaponTraceBakeIssueCount = weaponTraceBake.Issues.Length,
                WeaponTraceBakeReady = string.Equals(weaponTraceBake.Status, "Ready", StringComparison.Ordinal),
                WeaponTraceBakeIssues = weaponTraceBake.Issues,
                SocketBakeStatus = socketBake.Status,
                SocketBakeHash = socketBake.Hash,
                SocketBakeRequestCount = socketBake.RequestCount,
                SocketBakeClipCount = socketBake.ClipCount,
                SocketBakeSocketTrackCount = socketBake.SocketTrackCount,
                SocketBakeCheckedFrameCount = socketBake.CheckedFrameCount,
                SocketBakeIssueCount = socketBake.Issues.Length,
                SocketBakeReady = string.Equals(socketBake.Status, "Ready", StringComparison.Ordinal),
                SocketBakeIssues = socketBake.Issues,
                SocketBakeFormat = socketBake.Format,
                SocketBakeIndexPath = socketBake.IndexPath,
                SocketBakeIndexHash = socketBake.IndexHash,
                SocketBakeBinaryPath = socketBake.BinaryPath,
                SocketBakeBinaryHash = socketBake.BinaryHash,
                SocketBakeBinarySize = socketBake.BinarySize,
                HurtboxBakeStatus = hurtboxBake.Status,
                HurtboxBakeHash = hurtboxBake.Hash,
                HurtboxBakeClipCount = hurtboxBake.ClipCount,
                HurtboxBakeColliderCount = hurtboxBake.ColliderCount,
                HurtboxBakeShapeCount = hurtboxBake.ShapeCount,
                HurtboxBakeCheckedFrameCount = hurtboxBake.CheckedFrameCount,
                HurtboxBakeIssueCount = hurtboxBake.Issues.Length,
                HurtboxBakeReady = string.Equals(hurtboxBake.Status, "Ready", StringComparison.Ordinal),
                HurtboxBakeIssues = hurtboxBake.Issues,
                DefaultWeaponResourceIds = Array.Empty<string>(),
                Issues = Array.Empty<string>()
            };
        }

        private static string[] ResolveRequiredAbstractBoneIds(CharacterImportedPackage package)
        {
            var required = new List<string> { CharacterAbstractBoneIds.Head };
            CharacterConfig character = package?.Configs?.Characters != null && package.Configs.Characters.Length > 0
                ? package.Configs.Characters[0]
                : null;
            CharacterGameplayCameraProfile camera = character == null
                ? null
                : package.Configs.FindPresentationProfile(character.PresentationProfileId)?.GameplayCameraProfile;
            if (camera != null
                && !string.IsNullOrWhiteSpace(camera.FocusAbstractBoneId)
                && !required.Contains(camera.FocusAbstractBoneId))
                required.Add(camera.FocusAbstractBoneId);
            return required.ToArray();
        }

        private static void AddSlimPrefabComponentIssues(GameObject prefab, List<string> issues)
        {
            if (prefab.GetComponent<CharacterDefaultEquipmentRuntimeBinder>() != null)
                issues.Add("Generated prefab must not persist CharacterDefaultEquipmentRuntimeBinder.");
            if (prefab.GetComponent<CharacterRuntimeControllerBinding>() != null)
                issues.Add("Generated prefab must not persist CharacterRuntimeControllerBinding.");
            if (prefab.GetComponent<CharacterRuntimeInputMotionController>() != null)
                issues.Add("Generated prefab must not persist CharacterRuntimeInputMotionController.");
            if (prefab.GetComponent<CharacterRuntimeLocomotionBlendController>() != null)
                issues.Add("Generated prefab must not persist CharacterRuntimeLocomotionBlendController.");
            if (prefab.GetComponent<CharacterBoneRuntimeSync>() != null)
                issues.Add("Generated prefab must not persist CharacterBoneRuntimeSync.");
            if (FindChildByName(prefab.transform, "PreviewWeapons") != null)
                issues.Add("Generated prefab must not contain PreviewWeapons.");
        }

        private static void RunPrefabCompositionSmoke(
            GameObject prefab,
            CharacterImportedPackage package,
            List<string> issues)
        {
            GameObject instance = null;
            try
            {
                UnityEngine.Object created = PrefabUtility.InstantiatePrefab(prefab);
                instance = created as GameObject;
                if (instance == null)
                    instance = UnityEngine.Object.Instantiate(prefab);
                if (instance == null)
                {
                    issues.Add("Runtime prefab composition smoke failed: prefab instance could not be created.");
                    return;
                }

                instance.SetActive(false);
                CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
                if (spawnResult == null || !spawnResult.IsSuccess)
                {
                    issues.Add("Runtime prefab composition smoke failed: " + FormatIssues(spawnResult));
                    return;
                }

                ResourceManager resourceManager = CreateWeaponTraceBakeResourceManager(package);
                CharacterRuntimePrefabCompositionResult result = CharacterRuntimePrefabComposer.Compose(
                    instance,
                    new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                    {
                        ResourceManager = resourceManager,
                        InstantiateDefaultWeapons = false,
                        InstallInputMotionController = false,
                        EnableInputMotion = false,
                        UseExternalRuntimeTick = true
                    });
                if (!result.Success)
                    issues.Add("Runtime prefab composition smoke failed: " + string.Join("; ", result.Issues));
                if (result.DefaultEquipmentBinder == null)
                    issues.Add("Runtime prefab composition smoke did not install default equipment binder.");
                if (result.ControllerBinding == null)
                    issues.Add("Runtime prefab composition smoke did not install controller binding.");
                if (result.LocomotionController == null)
                    issues.Add("Runtime prefab composition smoke did not install locomotion controller.");
                if (result.BoneSync == null)
                    issues.Add("Runtime prefab composition smoke did not install bone sync.");
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static string[] CollectDefaultWeaponResourceIds(CharacterImportedPackage package)
        {
            CharacterWeaponAttachmentRuntimeBinding[] attachments = package?.Geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            if (attachments.Length == 0)
                return Array.Empty<string>();

            var result = new List<string>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null || string.IsNullOrWhiteSpace(CharacterRuntimePrefabResourceCatalog.GetWeaponVisualResourceKey(attachment)))
                    continue;

                string resourceId = CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceId(package, attachment);
                if (!string.IsNullOrWhiteSpace(resourceId))
                    result.Add(resourceId);
            }

            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static AvatarSourceStatus CollectAvatarSourceStatus(CharacterImportedPackage package)
        {
            if (package?.UnityResourceCatalog == null)
                return new AvatarSourceStatus();

            for (int i = 0; i < package.UnityResourceCatalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = package.UnityResourceCatalog.Entries[i];
                if (entry == null)
                    continue;
                if (!string.Equals(GetProviderData(entry, "usage"), "characterModel", StringComparison.OrdinalIgnoreCase))
                    continue;

                return new AvatarSourceStatus
                {
                    Status = FirstNonEmpty(GetProviderData(entry, "avatarSourceStatus"), GetProviderData(entry, "avatarSourceHumanoidStatus")),
                    AssetPath = GetProviderData(entry, "avatarSourceAssetPath"),
                    Guid = GetProviderData(entry, "avatarSourceGuid"),
                    RigType = ResolveImportedRigType(entry),
                    RigStatus = FirstNonEmpty(GetProviderData(entry, "avatarSourceRigStatus"), GetProviderData(entry, "avatarSourceHumanoidStatus")),
                    HumanoidStatus = GetProviderData(entry, "avatarSourceHumanoidStatus"),
                    Message = GetProviderData(entry, "avatarSourceMessage")
                };
            }

            return new AvatarSourceStatus();
        }

        private static string ResolveImportedRigType(ResourceCatalogEntry entry)
        {
            string declared = GetProviderData(entry, "rigType");
            if (string.Equals(declared, "Humanoid", StringComparison.OrdinalIgnoreCase))
                return "Humanoid";
            if (string.Equals(declared, "Generic", StringComparison.OrdinalIgnoreCase))
                return "Generic";
            return !string.IsNullOrWhiteSpace(GetProviderData(entry, "avatarSourceHumanoidStatus"))
                ? "Humanoid"
                : string.Empty;
        }

        private sealed class AvatarSourceStatus
        {
            public string Status { get; set; } = string.Empty;
            public string AssetPath { get; set; } = string.Empty;
            public string Guid { get; set; } = string.Empty;
            public string RigType { get; set; } = string.Empty;
            public string RigStatus { get; set; } = string.Empty;
            public string HumanoidStatus { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

    }
}
