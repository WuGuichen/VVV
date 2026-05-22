using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterImport
{
    public static class CharacterImportedPackagePrefabBuilder
    {
        private const string DefaultImportedPackageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard";
        private const string PreviewScenePath = "Assets/Scenes/MxFramework/CharacterImportedPreview.unity";
        private const string CalibrationScenePath = "Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity";
        private const string RuntimeResourcesRootName = "runtime_resources";
        private const string UnityResourcesFolderName = "Resources";
        private const string RuntimeResourcesAddressRoot = "MxFrameworkGenerated/CharacterPackages";
        private const string RuntimeDebugPanelSettingsPath = "Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokePanelSettings.asset";
        private static readonly string[] HumanoidAvatarSearchRoots =
        {
            "Assets/MxFrameworkGenerated/CharacterPackages",
            "Assets/Art/MxFramework/Samples/Characters"
        };

        private static string _previewMaterialFolder;

        [MenuItem("MxFramework/Character/Create Preview Prefab For Iron Vanguard", priority = 220)]
        public static void CreateDefaultPreviewPrefab()
        {
            BuildPrefab(DefaultImportedPackageRoot, selectPrefab: true);
        }

        [MenuItem("MxFramework/Character/Create Preview Prefab From Imported Package...", priority = 221)]
        public static void CreatePreviewPrefabFromFolder()
        {
            string root = EditorUtility.OpenFolderPanel("选择已导入的 Character Package", Path.GetFullPath("Assets/MxFrameworkGenerated/CharacterPackages"), string.Empty);
            if (string.IsNullOrWhiteSpace(root))
                return;

            BuildPrefab(ToProjectRelativePath(root), selectPrefab: true);
        }

        [MenuItem("MxFramework/Character/Create Preview Scene For Iron Vanguard", priority = 222)]
        public static void CreateDefaultPreviewScene()
        {
            string prefabPath = BuildPrefab(DefaultImportedPackageRoot, selectPrefab: false);
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/Scenes", "MxFramework");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.64f);
            CreatePreviewCamera();
            CreatePreviewLight();
            CreatePreviewFloor();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject loader = CreateRuntimeResourceBootstrap(prefab, prefabPath);
                Selection.activeGameObject = loader;
            }

            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MxFramework Character preview scene created: " + PreviewScenePath);
        }

        [MenuItem("MxFramework/Character/Create Locomotion Calibration Scene For Iron Vanguard", priority = 223)]
        public static void CreateDefaultLocomotionCalibrationScene()
        {
            string prefabPath = BuildPrefab(DefaultImportedPackageRoot, selectPrefab: false);
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/Scenes", "MxFramework");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.64f);
            CreatePreviewCamera();
            CreatePreviewLight();
            CreatePreviewFloor();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject loader = CreateRuntimeResourceBootstrap(prefab, prefabPath);
                loader.name = "CharacterLocomotionCalibrationRunner";
                CharacterLocomotionCalibrationRunner runner = loader.AddComponent<CharacterLocomotionCalibrationRunner>();
                ConfigureLocomotionCalibrationRunner(runner, loader.GetComponent<CharacterRuntimeResourceBootstrap>());
                Selection.activeGameObject = loader;
            }

            EditorSceneManager.SaveScene(scene, CalibrationScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MxFramework Character locomotion calibration scene created: " + CalibrationScenePath);
        }

        public static string BuildPrefab(string importedPackageRoot, bool selectPrefab)
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

            if (!spawnResult.IsSuccess)
            {
                Debug.LogError("MxFramework Character Preview: runtime binding failed: " + FormatIssues(spawnResult));
                return string.Empty;
            }

            Dictionary<string, ResourcePreviewInfo> resources = ReadResourcePreviewInfos(package, root);
            if (!ValidatePreviewResources(resources))
                return string.Empty;

            EnsureFolder(root, "preview_materials");
            _previewMaterialFolder = root + "/preview_materials";
            Dictionary<string, GameObject> weaponPrefabs = BuildWeaponPrefabs(package, resources, root);
            AnimationClip[] animationClips = LoadAnimationClips(package, root);
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

                var weaponsRoot = new GameObject("PreviewWeapons").transform;
                weaponsRoot.SetParent(rootObject.transform, false);

                var diagnosticsRoot = new GameObject("Diagnostics").transform;
                diagnosticsRoot.SetParent(rootObject.transform, false);

                Transform bodyModel = CreateBodyModel(package, resources, modelRoot);
                var boneBindings = new List<TransformBoneBindingInfo>();
                Dictionary<string, Transform> sockets = CreateSockets(package, socketsRoot, bodyModel, boneBindings);
                CreateWeapons(package, resources, weaponsRoot, sockets);
                CreateColliders(package, collidersRoot, bodyModel, boneBindings);
                CreateResourceDiagnostics(resources, diagnosticsRoot);
                AddBoneRuntimeSync(rootObject, boneBindings);
                AddRuntimeControllerBinding(rootObject, package, spawnResult);
                AddRuntimeInputMotionController(rootObject);
                AddRuntimeLocomotionBlendController(rootObject, modelRoot, bodyModel, animationClips);
                AddDefaultEquipmentRuntimeBinder(rootObject, package, spawnResult, socketsRoot, weaponsRoot, sockets, weaponPrefabs, animationClips);

                string prefabFolder = root + "/prefabs";
                EnsureFolder(root, "prefabs");
                string prefabPath = prefabFolder + "/" + package.PackageId + "_character_preview.prefab";
                rootObject.SetActive(true);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
                MirrorPrefabToRuntimeResources(root, package.PackageId, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
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

        private static Transform CreateBodyModel(CharacterImportedPackage package, Dictionary<string, ResourcePreviewInfo> resources, Transform parent)
        {
            ResourcePreviewInfo body = FindResource(resources, usage: "characterModel", resourceKey: string.Empty);
            Transform wrapper = CreateModelWrapper("BodyModel", body, parent);
            return wrapper;
        }

        private static Dictionary<string, GameObject> BuildWeaponPrefabs(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            string importedPackageRoot)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (package == null || package.Geometry == null || package.Geometry.WeaponAttachments.Length == 0)
                return result;

            string prefabFolder = importedPackageRoot + "/prefabs";
            EnsureFolder(importedPackageRoot, "prefabs");
            EnsureFolder(prefabFolder, "weapons");
            string weaponFolder = prefabFolder + "/weapons";

            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                if (string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                    continue;

                ResourcePreviewInfo resource = FindResource(resources, usage: "weaponModel", resourceKey: attachment.PreviewResourceKey);
                if (resource == null || !resource.IsImportReady)
                {
                    Debug.LogWarning("MxFramework Character Preview: skipping generated weapon prefab and runtime default mount. weapon="
                        + attachment.WeaponId + " slot=" + attachment.EquipSlot + " " + FormatResourceDiagnostic(resource));
                    continue;
                }

                string prefabName = package.PackageId + "_" + SanitizeName(attachment.EquipSlot) + "_" + SanitizeName(attachment.WeaponId);
                string prefabPath = weaponFolder + "/" + prefabName + ".prefab";
                var root = new GameObject(prefabName);
                try
                {
                    root.SetActive(false);
                    CreateModelWrapper("Model", resource, root.transform);
                    root.SetActive(true);
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    MirrorPrefabToRuntimeResources(importedPackageRoot, package.PackageId, prefabPath);
                    if (prefab != null)
                        result[GetAttachmentKey(attachment)] = prefab;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            return result;
        }

        private static void AddDefaultEquipmentRuntimeBinder(
            GameObject rootObject,
            CharacterImportedPackage package,
            CharacterRuntimeSpawnResult spawnResult,
            Transform socketsRoot,
            Transform authoringPreviewWeaponsRoot,
            Dictionary<string, Transform> sockets,
            Dictionary<string, GameObject> weaponPrefabs,
            AnimationClip[] animationClips)
        {
            var binder = rootObject.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("_packageId").stringValue = package.PackageId;
            serialized.FindProperty("_characterId").stringValue = spawnResult.Binding.ResolvedProfile.CharacterId.Value.ToString();
            serialized.FindProperty("_loadoutId").stringValue = spawnResult.Binding.SpawnPlan.LoadoutId.Value.ToString();
            serialized.FindProperty("_socketsRoot").objectReferenceValue = socketsRoot;
            serialized.FindProperty("_authoringPreviewWeaponsRoot").objectReferenceValue = authoringPreviewWeaponsRoot;
            serialized.FindProperty("_instantiateDefaultWeaponsOnAwake").boolValue = false;
            serialized.FindProperty("_playFirstAnimationOnStart").boolValue = false;

            SerializedProperty weapons = serialized.FindProperty("_defaultWeapons");
            CharacterWeaponAttachmentRuntimeBinding[] mountedWeapons = GetRuntimeMountedWeaponAttachments(
                package.Geometry.WeaponAttachments,
                weaponPrefabs);
            weapons.arraySize = mountedWeapons.Length;
            for (int i = 0; i < mountedWeapons.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = mountedWeapons[i];
                SerializedProperty item = weapons.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("_weaponId").stringValue = attachment.WeaponId;
                item.FindPropertyRelative("_equipSlot").stringValue = attachment.EquipSlot;
                item.FindPropertyRelative("_socketId").stringValue = attachment.AttachSocketId;
                item.FindPropertyRelative("_traceId").stringValue = attachment.TraceId;
                item.FindPropertyRelative("_socketTransform").objectReferenceValue = sockets.TryGetValue(attachment.AttachSocketId, out Transform socket) ? socket : null;
                item.FindPropertyRelative("_resourceId").stringValue = GetWeaponPrefabResourceId(package, attachment);
                item.FindPropertyRelative("_resourceTypeId").stringValue = ResourceTypeIds.GameObject;
                item.FindPropertyRelative("_resourceVariant").stringValue = "default";
                item.FindPropertyRelative("_resourcePackageId").stringValue = package.PackageId;
                item.FindPropertyRelative("_prefab").objectReferenceValue = null;
                item.FindPropertyRelative("_localPosition").vector3Value = ToVector3(attachment.LocalGripPose.Position, Vector3.zero);
                item.FindPropertyRelative("_localRotation").quaternionValue = ToQuaternion(attachment.LocalGripPose.Rotation);
                item.FindPropertyRelative("_localScale").vector3Value = ToVector3(attachment.LocalGripPose.Scale, Vector3.one);
            }

            SerializedProperty clips = serialized.FindProperty("_animationClips");
            clips.arraySize = animationClips.Length;
            for (int i = 0; i < animationClips.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = animationClips[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddRuntimeControllerBinding(
            GameObject rootObject,
            CharacterImportedPackage package,
            CharacterRuntimeSpawnResult spawnResult)
        {
            if (spawnResult == null || !spawnResult.IsSuccess)
                return;

            CharacterRuntimeControllerBinding binding = rootObject.AddComponent<CharacterRuntimeControllerBinding>();
            binding.ConfigureFromRuntimeBinding(package.PackageId, spawnResult.Binding);
            EditorUtility.SetDirty(binding);
        }

        private static void AddRuntimeInputMotionController(GameObject rootObject)
        {
            CharacterRuntimeInputMotionController controller = rootObject.AddComponent<CharacterRuntimeInputMotionController>();
            EditorUtility.SetDirty(controller);
        }

        private static void AddRuntimeLocomotionBlendController(
            GameObject rootObject,
            Transform modelRoot,
            Transform bodyModel,
            AnimationClip[] animationClips)
        {
            CharacterRuntimeLocomotionBlendController controller = rootObject.AddComponent<CharacterRuntimeLocomotionBlendController>();
            Animator animator = bodyModel == null ? null : bodyModel.GetComponentInChildren<Animator>(includeInactive: true);
            if (animator == null && bodyModel != null)
            {
                animator = bodyModel.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
            }

            AssignHumanoidAvatarIfNeeded(animator, animationClips);
            controller.Configure(modelRoot, animator, animationClips);
            EditorUtility.SetDirty(controller);
        }

        private static void AssignHumanoidAvatarIfNeeded(Animator animator, AnimationClip[] animationClips)
        {
            if (animator == null)
                return;
            if (animator.avatar != null && animator.avatar.isValid)
                return;
            if (!RequiresHumanoidAvatar(animationClips))
                return;

            Avatar avatar = FindHumanoidAvatar();
            if (avatar == null)
            {
                Debug.LogWarning("MxFramework Character Preview: Humanoid animation clips are configured, but no valid Humanoid Avatar was found. Runtime animation may not drive the model.");
                return;
            }

            animator.avatar = avatar;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
        }

        private static bool RequiresHumanoidAvatar(AnimationClip[] animationClips)
        {
            for (int i = 0; animationClips != null && i < animationClips.Length; i++)
            {
                AnimationClip clip = animationClips[i];
                if (clip != null && clip.humanMotion)
                    return true;
            }

            return false;
        }

        private static Avatar FindHumanoidAvatar()
        {
            string[] guids = AssetDatabase.FindAssets("t:Avatar", HumanoidAvatarSearchRoots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is Avatar avatar && avatar.isValid && avatar.isHuman)
                        return avatar;
                }
            }

            return null;
        }

        private static AnimationClip[] LoadAnimationClips(CharacterImportedPackage package, string importedPackageRoot)
        {
            var clips = new List<AnimationClip>();
            if (package != null && package.UnityResourceCatalog != null)
            {
                for (int i = 0; i < package.UnityResourceCatalog.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = package.UnityResourceCatalog.Entries[i];
                    if (!IsAnimationResource(entry))
                        continue;
                    if (!IsUnityCatalogEntryImported(entry))
                        continue;

                    string assetPath = ResolveUnityAssetPath(entry, GetUnityAssetGuid(entry), requireGameObject: false);
                    if (string.IsNullOrWhiteSpace(assetPath))
                    {
                        Debug.LogWarning("MxFramework Character Preview: animation catalog entry is imported but the Unity asset could not be resolved. resource="
                            + entry.Id + " status=" + GetImportStatus(entry) + " address=" + entry.Address + FormatAssetGuid(GetUnityAssetGuid(entry)));
                        continue;
                    }

                    AddAnimationClipsAtPath(assetPath, clips);
                }
            }

            AddAnimationClipsFromRegistry(importedPackageRoot, clips);
            return clips.ToArray();
        }

        private static void AddAnimationClipsFromRegistry(string importedPackageRoot, List<AnimationClip> clips)
        {
            string registryPath = NormalizeProjectPath(importedPackageRoot) + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
                return;

            JObject root = JObject.Parse(File.ReadAllText(registryPath));
            JArray registryClips = root["clips"] as JArray;
            for (int i = 0; registryClips != null && i < registryClips.Count; i++)
            {
                JObject clip = registryClips[i] as JObject;
                if (clip == null)
                    continue;

                string resourceKey = ReadString(clip, "runtimeResourceKey");
                string sourceClipName = ReadString(clip, "sourceClipName");
                string assetPath = ReadString(clip["sourceSelection"] as JObject, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = FindProjectRuntimeCatalogAssetPath(resourceKey);

                AnimationClip animationClip = LoadAnimationClipAsset(assetPath, sourceClipName);
                if (animationClip == null || clips.Contains(animationClip))
                    continue;

                clips.Add(animationClip);
            }
        }

        private static bool IsAnimationResource(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return false;

            return string.Equals(entry.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal)
                || string.Equals(entry.TypeId, "AnimationClipGroup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.TypeId, "animation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetProviderData(entry, "usage"), "animationClipGroup", StringComparison.OrdinalIgnoreCase)
                || HasLabel(entry, "character.animation")
                || HasLabel(entry, "character.usage.animationclipgroup");
        }

        private static void AddAnimationClipsAtPath(string assetPath, List<AnimationClip> clips)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clips.Contains(clip))
                    clips.Add(clip);
            }
        }

        private static void CreateWeapons(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            Transform weaponsRoot,
            Dictionary<string, Transform> sockets)
        {
            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                if (string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                    continue;

                Transform parent = sockets.TryGetValue(attachment.AttachSocketId, out Transform socket)
                    ? socket
                    : weaponsRoot;

                var grip = new GameObject(attachment.EquipSlot + "_" + attachment.WeaponId).transform;
                grip.SetParent(parent, false);
                ApplyPose(grip, attachment.LocalGripPose);

                ResourcePreviewInfo resource = FindResource(resources, usage: "weaponModel", resourceKey: attachment.PreviewResourceKey);
                CreateModelWrapper("Model_" + SanitizeName(attachment.WeaponId), resource, grip);
            }
        }

        private static string GetAttachmentKey(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return (attachment?.EquipSlot ?? string.Empty) + "::" + (attachment?.WeaponId ?? string.Empty);
        }

        private static string GetCharacterPrefabResourceId(CharacterImportedPackage package)
        {
            return "char." + package.PackageId + ".prefab.character_preview";
        }

        private static string GetWeaponPrefabResourceId(CharacterImportedPackage package, CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return "char." + package.PackageId + ".prefab.weapon."
                + SanitizeResourceIdSegment(attachment.EquipSlot) + "." + SanitizeResourceIdSegment(attachment.WeaponId);
        }

        private static GameObject MirrorPrefabToRuntimeResources(string importedPackageRoot, string packageId, string sourcePrefabPath)
        {
            if (string.IsNullOrWhiteSpace(importedPackageRoot)
                || string.IsNullOrWhiteSpace(packageId)
                || string.IsNullOrWhiteSpace(sourcePrefabPath))
            {
                return null;
            }

            string targetPath = GetRuntimeResourcesAssetPath(importedPackageRoot, packageId, sourcePrefabPath);
            EnsureFolderPath(Path.GetDirectoryName(targetPath)?.Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) != null)
                AssetDatabase.DeleteAsset(targetPath);

            if (!AssetDatabase.CopyAsset(sourcePrefabPath, targetPath))
            {
                Debug.LogWarning("MxFramework Character Preview: failed to mirror prefab into Resources for runtime loading. source="
                    + sourcePrefabPath + " target=" + targetPath);
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
        }

        private static string GetRuntimeResourcesAssetPath(string importedPackageRoot, string packageId, string sourcePrefabPath)
        {
            string relative = GetPackageRelativePath(importedPackageRoot, sourcePrefabPath);
            return importedPackageRoot + "/" + RuntimeResourcesRootName + "/" + UnityResourcesFolderName + "/"
                + RuntimeResourcesAddressRoot + "/" + packageId + "/" + relative;
        }

        private static string GetRuntimeResourcesAddress(string importedPackageRoot, string packageId, string sourcePrefabPath)
        {
            string relative = GetPackageRelativePath(importedPackageRoot, sourcePrefabPath);
            string path = RuntimeResourcesAddressRoot + "/" + packageId + "/" + relative;
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - ".prefab".Length)
                : path;
        }

        private static string GetPackageRelativePath(string importedPackageRoot, string assetPath)
        {
            string root = NormalizeProjectPath(importedPackageRoot).TrimEnd('/');
            string normalized = NormalizeProjectPath(assetPath);
            string prefix = root + "/";
            return normalized.StartsWith(prefix, StringComparison.Ordinal)
                ? normalized.Substring(prefix.Length)
                : Path.GetFileName(normalized);
        }

        private static GameObject CreateRuntimeResourceBootstrap(GameObject characterPrefab, string characterPrefabPath)
        {
            var loader = new GameObject("CharacterRuntimeResourceBootstrap");
            var bootstrap = loader.AddComponent<CharacterRuntimeResourceBootstrap>();
            Component debugPanel = AddRuntimeAnimationDebugPanel(loader);
            Component debugOverlay = debugPanel != null ? AddDebugUiOverlayController(loader) : null;
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(DefaultImportedPackageRoot));
            ConfigureRuntimeAnimationDebugPanel(debugPanel, debugOverlay);
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_catalogId").stringValue = "character.runtime." + package.PackageId;
            serialized.FindProperty("_packageId").stringValue = package.PackageId;
            serialized.FindProperty("_characterResourceId").stringValue = GetCharacterPrefabResourceId(package);
            serialized.FindProperty("_characterResourceVariant").stringValue = "default";
            serialized.FindProperty("_loadOnStart").boolValue = true;

            SerializedProperty resources = serialized.FindProperty("_resources");
            var runtimeResources = new List<RuntimeResourceBootstrapEntry>
            {
                new RuntimeResourceBootstrapEntry(
                    GetCharacterPrefabResourceId(package),
                    ResourceTypeIds.GameObject,
                    ResourcesProvider.Id,
                    "default",
                    package.PackageId,
                    GetRuntimeResourcesAddress(DefaultImportedPackageRoot, package.PackageId, characterPrefabPath))
            };
            var runtimeResourceIds = new HashSet<string>(StringComparer.Ordinal)
            {
                GetCharacterPrefabResourceId(package)
            };

            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                if (string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                    continue;

                string weaponPrefabPath = DefaultImportedPackageRoot + "/prefabs/weapons/"
                    + package.PackageId + "_" + SanitizeName(attachment.EquipSlot) + "_" + SanitizeName(attachment.WeaponId) + ".prefab";
                if (!File.Exists(weaponPrefabPath))
                    continue;

                string weaponResourceId = GetWeaponPrefabResourceId(package, attachment);
                if (!runtimeResourceIds.Add(weaponResourceId))
                    continue;

                runtimeResources.Add(new RuntimeResourceBootstrapEntry(
                    weaponResourceId,
                    ResourceTypeIds.GameObject,
                    ResourcesProvider.Id,
                    "default",
                    package.PackageId,
                    GetRuntimeResourcesAddress(DefaultImportedPackageRoot, package.PackageId, weaponPrefabPath)));
            }

            AddAnimationRuntimeResources(DefaultImportedPackageRoot, package.PackageId, runtimeResources, runtimeResourceIds);

            resources.arraySize = runtimeResources.Count;
            for (int i = 0; i < runtimeResources.Count; i++)
            {
                RuntimeResourceBootstrapEntry entry = runtimeResources[i];
                SetSerializedResource(
                    resources.GetArrayElementAtIndex(i),
                    entry.Id,
                    entry.TypeId,
                    entry.ProviderId,
                    entry.Variant,
                    entry.PackageId,
                    entry.Address,
                    entry.Asset);
            }

            string animationSetPath = DefaultImportedPackageRoot + "/config/animation_set_definition.json";
            string animationClipRegistryPath = DefaultImportedPackageRoot + "/config/animation_clip_registry.json";
            TextAsset animationSet = AssetDatabase.LoadAssetAtPath<TextAsset>(animationSetPath);
            TextAsset animationClipRegistry = AssetDatabase.LoadAssetAtPath<TextAsset>(animationClipRegistryPath);
            serialized.FindProperty("_animationSetDefinitionJson").objectReferenceValue = animationSet;
            serialized.FindProperty("_animationClipRegistryJson").objectReferenceValue = animationClipRegistry;
            serialized.FindProperty("_animationSetId").stringValue = ReadFirstAnimationSetId(animationSetPath);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return loader;
        }

        private static void ConfigureRuntimeAnimationDebugPanel(
            Component debugPanel,
            Component debugOverlay)
        {
            if (debugPanel == null)
                return;

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimeDebugPanelSettingsPath);
            if (panelSettings == null)
                return;

            var serialized = new SerializedObject(debugPanel);
            serialized.FindProperty("_panelSettings").objectReferenceValue = panelSettings;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (debugOverlay == null)
                return;

            var overlaySerialized = new SerializedObject(debugOverlay);
            SerializedProperty panelSettingsProperty = overlaySerialized.FindProperty("_panelSettings");
            if (panelSettingsProperty != null)
                panelSettingsProperty.objectReferenceValue = panelSettings;
            overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLocomotionCalibrationRunner(
            CharacterLocomotionCalibrationRunner runner,
            CharacterRuntimeResourceBootstrap bootstrap)
        {
            if (runner == null)
                return;

            var serialized = new SerializedObject(runner);
            serialized.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("_loadOnStart").boolValue = true;
            serialized.FindProperty("_keepInputMotionEnabled").boolValue = true;
            SerializedProperty panelSettingsProperty = serialized.FindProperty("_panelSettings");
            if (panelSettingsProperty != null)
                panelSettingsProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimeDebugPanelSettingsPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component AddDebugUiOverlayController(GameObject owner)
        {
            if (owner == null)
                return null;

            Type overlayType = Type.GetType("MxFramework.DebugUI.Toolkit.DebugUiOverlayController, MxFramework.DebugUI.Toolkit");
            return overlayType != null ? owner.AddComponent(overlayType) : null;
        }

        private static Component AddRuntimeAnimationDebugPanel(GameObject owner)
        {
            if (owner == null)
                return null;

            Type panelType = Type.GetType(
                "MxFramework.CharacterRuntimeSpawn.DebugUI.Unity.CharacterRuntimeAnimationDebugPanel, MxFramework.Character.RuntimeSpawn.DebugUI.Unity");
            return panelType != null ? owner.AddComponent(panelType) : null;
        }

        private static void AddAnimationRuntimeResources(
            string importedPackageRoot,
            string packageId,
            List<RuntimeResourceBootstrapEntry> runtimeResources,
            HashSet<string> runtimeResourceIds)
        {
            string registryPath = importedPackageRoot + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
                return;

            JObject root = JObject.Parse(File.ReadAllText(registryPath));
            JArray clips = root["clips"] as JArray;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                if (clip == null)
                    continue;

                string resourceKey = ReadString(clip, "runtimeResourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey) || !runtimeResourceIds.Add(resourceKey))
                    continue;

                string sourceClipName = ReadString(clip, "sourceClipName");
                string assetPath = ReadString(clip["sourceSelection"] as JObject, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = FindProjectRuntimeCatalogAssetPath(resourceKey);

                AnimationClip animationClip = LoadAnimationClipAsset(assetPath, sourceClipName);
                if (animationClip == null)
                {
                    Debug.LogWarning("MxFramework Character Runtime: animation clip could not be registered for runtime playback. resource="
                        + resourceKey + " sourceClip=" + sourceClipName + " assetPath=" + assetPath);
                    continue;
                }

                runtimeResources.Add(new RuntimeResourceBootstrapEntry(
                    resourceKey,
                    ResourceTypeIds.AnimationClip,
                    "memory",
                    string.Empty,
                    packageId,
                    resourceKey,
                    animationClip));
            }
        }

        private static AnimationClip LoadAnimationClipAsset(string assetPath, string sourceClipName)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            AnimationClip direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (direct != null && (string.IsNullOrWhiteSpace(sourceClipName) || string.Equals(direct.name, sourceClipName, StringComparison.Ordinal)))
                return direct;
            if (direct != null && !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !assetPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                return direct;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip first = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip))
                    continue;
                if (first == null)
                    first = clip;
                if (!string.IsNullOrWhiteSpace(sourceClipName) && string.Equals(clip.name, sourceClipName, StringComparison.Ordinal))
                    return clip;
            }

            return first;
        }

        private static string FindProjectRuntimeCatalogAssetPath(string resourceKey)
        {
            string root = "Assets/Config/MxFramework/ResourceCatalogs";
            if (string.IsNullOrWhiteSpace(resourceKey) || !Directory.Exists(root))
                return string.Empty;

            foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                JObject catalog = JObject.Parse(File.ReadAllText(path));
                JArray entries = catalog["entries"] as JArray;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    if (!string.Equals(ReadString(entry, "id"), resourceKey, StringComparison.Ordinal))
                        continue;

                    string assetPath = ReadString(entry["providerData"] as JObject, "assetPath");
                    if (!string.IsNullOrWhiteSpace(assetPath))
                        return assetPath;
                }
            }

            return string.Empty;
        }

        private static string ReadFirstAnimationSetId(string animationSetPath)
        {
            if (string.IsNullOrWhiteSpace(animationSetPath) || !File.Exists(animationSetPath))
                return string.Empty;

            JObject root = JObject.Parse(File.ReadAllText(animationSetPath));
            JArray sets = root["sets"] as JArray;
            JObject first = sets != null && sets.Count > 0 ? sets[0] as JObject : null;
            return ReadString(first, "setId");
        }

        private static void EnsureFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string[] segments = path.Split('/');
            if (segments.Length == 0 || segments[0] != "Assets")
                return;

            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void SetSerializedResource(
            SerializedProperty property,
            string id,
            string typeId,
            string providerId,
            string variant,
            string packageId,
            string address,
            UnityEngine.Object asset)
        {
            property.FindPropertyRelative("_id").stringValue = id;
            property.FindPropertyRelative("_typeId").stringValue = typeId;
            property.FindPropertyRelative("_providerId").stringValue = providerId;
            property.FindPropertyRelative("_variant").stringValue = variant;
            property.FindPropertyRelative("_packageId").stringValue = packageId;
            property.FindPropertyRelative("_address").stringValue = address;
            property.FindPropertyRelative("_asset").objectReferenceValue = asset;
        }

        private static Dictionary<string, Transform> CreateSockets(
            CharacterImportedPackage package,
            Transform socketsRoot,
            Transform bodyModel,
            List<TransformBoneBindingInfo> boneBindings)
        {
            var sockets = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (int i = 0; i < package.Geometry.Sockets.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = package.Geometry.Sockets[i];
                var marker = new GameObject("Socket_" + socket.SocketId).transform;
                marker.SetParent(socketsRoot, false);
                ApplyPose(marker, socket.LocalPose);
                AddMarker(marker.gameObject, new Color(0.08f, 0.55f, 0.55f, 0.55f), 0.05f);

                Transform bone = FindBodyChild(bodyModel, socket.BonePath, socket.LocalPose.ParentPath);
                if (bone != null)
                {
                    ApplyBoneRelativePose(marker, bone, socket.LocalPose);
                    boneBindings.Add(TransformBoneBindingInfo.FromPose(
                        "socket:" + socket.SocketId,
                        socket.BonePath,
                        marker,
                        bone,
                        socket.LocalPose));
                }
                else
                {
                    Debug.LogWarning("MxFramework Character Preview: socket bone was not found; socket will not follow animation. socket="
                        + socket.SocketId + " bonePath=" + socket.BonePath);
                }

                sockets[socket.SocketId] = marker;
            }

            return sockets;
        }

        private static void CreateColliders(
            CharacterImportedPackage package,
            Transform collidersRoot,
            Transform bodyModel,
            List<TransformBoneBindingInfo> boneBindings)
        {
            for (int i = 0; i < package.Geometry.BodyColliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding binding = package.Geometry.BodyColliders[i];
                var node = new GameObject("Collider_" + binding.ColliderId);
                node.transform.SetParent(collidersRoot, false);
                ApplyPose(node.transform, binding.LocalPose);
                AddCollider(node, binding);

                Transform bone = FindBodyChild(bodyModel, binding.LocalPose.ParentPath, binding.PartId);
                if (bone != null)
                {
                    boneBindings.Add(TransformBoneBindingInfo.FromWorld(
                        "collider:" + binding.ColliderId,
                        binding.LocalPose.ParentPath,
                        node.transform,
                        bone));
                }
                else
                {
                    Debug.LogWarning("MxFramework Character Preview: collider bone was not found; collider will not follow animation. collider="
                        + binding.ColliderId + " parentPath=" + binding.LocalPose.ParentPath + " part=" + binding.PartId);
                }
            }
        }

        private static void AddBoneRuntimeSync(GameObject rootObject, List<TransformBoneBindingInfo> boneBindings)
        {
            if (boneBindings == null || boneBindings.Count == 0)
                return;

            var sync = rootObject.AddComponent<CharacterBoneRuntimeSync>();
            var serialized = new SerializedObject(sync);
            SerializedProperty bindings = serialized.FindProperty("_bindings");
            bindings.arraySize = boneBindings.Count;
            for (int i = 0; i < boneBindings.Count; i++)
            {
                TransformBoneBindingInfo binding = boneBindings[i];
                SerializedProperty item = bindings.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("_bindingId").stringValue = binding.BindingId;
                item.FindPropertyRelative("_bonePath").stringValue = binding.BonePath;
                item.FindPropertyRelative("_target").objectReferenceValue = binding.Target;
                item.FindPropertyRelative("_bone").objectReferenceValue = binding.Bone;
                item.FindPropertyRelative("_localPosition").vector3Value = binding.LocalPosition;
                item.FindPropertyRelative("_localRotation").quaternionValue = binding.LocalRotation;
                item.FindPropertyRelative("_localScale").vector3Value = binding.LocalScale;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            sync.ApplyBindings();
        }

        private static Transform CreateModelWrapper(string name, ResourcePreviewInfo resource, Transform parent)
        {
            var wrapper = new GameObject(name).transform;
            wrapper.SetParent(parent, false);
            if (resource != null)
                ApplyPose(wrapper, resource.WrapperPose);

            GameObject instance = InstantiateAsset(resource);
            if (instance == null)
            {
                Debug.LogWarning("MxFramework Character Preview: using placeholder model for " + name + ". " + FormatResourceDiagnostic(resource));
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.name = "MissingModelPlaceholder_" + SanitizeName(resource != null ? resource.Key : name);
                instance.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
                UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
                AssignMaterial(instance, new Color(0.82f, 0.78f, 0.16f, 0.55f));
            }

            instance.transform.SetParent(wrapper, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return wrapper;
        }

        private static void CreateResourceDiagnostics(Dictionary<string, ResourcePreviewInfo> resources, Transform parent)
        {
            if (resources == null || parent == null)
                return;

            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (resource == null || resource.IsImportReady || string.IsNullOrWhiteSpace(resource.Diagnostic))
                    continue;

                var item = new GameObject("Resource_" + SanitizeName(resource.Key)).transform;
                item.SetParent(parent, false);
                item.gameObject.SetActive(false);

                var status = new GameObject("status_" + SanitizeName(string.IsNullOrWhiteSpace(resource.ImportStatus) ? "unknown" : resource.ImportStatus)).transform;
                status.SetParent(item, false);

                var path = new GameObject("asset_" + SanitizeName(string.IsNullOrWhiteSpace(resource.AssetPath) ? "unresolved" : resource.AssetPath)).transform;
                path.SetParent(item, false);
            }
        }

        private static GameObject InstantiateAsset(ResourcePreviewInfo resource)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.AssetPath))
                return null;

            if (!resource.IsImportReady)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset is not ready for prefab instantiation. "
                    + FormatResourceDiagnostic(resource));
                return null;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(resource.AssetPath);
            if (asset == null)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset could not be loaded. "
                    + FormatResourceDiagnostic(resource));
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (instance != null)
                return instance;

            return UnityEngine.Object.Instantiate(asset);
        }

        private static void AddCollider(GameObject node, CharacterBodyColliderRuntimeBinding binding)
        {
            string shape = binding.Shape ?? string.Empty;
            if (shape.Equals("Box", StringComparison.OrdinalIgnoreCase))
            {
                BoxCollider collider = node.AddComponent<BoxCollider>();
                collider.size = ToVector3(binding.Size, Vector3.one);
            }
            else if (shape.Equals("Sphere", StringComparison.OrdinalIgnoreCase))
            {
                SphereCollider collider = node.AddComponent<SphereCollider>();
                collider.radius = Mathf.Max(0.01f, binding.Radius);
            }
            else
            {
                CapsuleCollider collider = node.AddComponent<CapsuleCollider>();
                collider.radius = Mathf.Max(0.01f, binding.Radius);
                collider.height = Mathf.Max(collider.radius * 2f, binding.Height);
                collider.direction = 1;
            }
        }

        private static Transform FindBodyChild(Transform root, string path, string fallbackKey = "")
        {
            if (root == null)
                return null;

            string normalized = (path ?? string.Empty).StartsWith("bone.", StringComparison.Ordinal)
                ? path.Substring("bone.".Length)
                : path ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                Transform direct = root.Find(normalized);
                if (direct != null)
                    return direct;
            }

            string last = normalized.Contains("/") ? normalized.Substring(normalized.LastIndexOf("/", StringComparison.Ordinal) + 1) : normalized;
            foreach (string candidate in GetBoneNameCandidates(last, fallbackKey))
            {
                Transform match = FindChildByName(root, candidate);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildByName(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static IEnumerable<string> GetBoneNameCandidates(string name, string fallbackKey)
        {
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;

            string key = (name + " " + fallbackKey).ToLowerInvariant();
            if (key.Contains("righthand") || key.Contains("right_hand"))
            {
                yield return "hand.R";
                yield return "RightHand";
                yield return "right_hand";
            }
            if (key.Contains("lefthand") || key.Contains("left_hand"))
            {
                yield return "hand.L";
                yield return "LeftHand";
                yield return "left_hand";
            }
            if (key.Contains("head"))
            {
                yield return "spine.006_end";
                yield return "spine.006";
                yield return "Head";
            }
            if (key.Contains("chest"))
            {
                yield return "spine.004";
                yield return "spine.003";
                yield return "spine.002";
            }
            if (key.Contains("spine") || key.Contains("torso"))
            {
                yield return "spine.003";
                yield return "spine.002";
                yield return "spine";
            }
        }

        private static void ApplyBoneRelativePose(Transform target, Transform bone, CharacterRuntimePose pose)
        {
            target.position = bone.TransformPoint(ToVector3(pose.Position, Vector3.zero));
            target.rotation = bone.rotation * ToQuaternion(pose.Rotation);
            target.localScale = ToVector3(pose.Scale, Vector3.one);
        }

        private static Dictionary<string, ResourcePreviewInfo> ReadResourcePreviewInfos(CharacterImportedPackage package, string importedPackageRoot)
        {
            Dictionary<string, CharacterRuntimePose> wrapperPoses = ReadResourceMappingWrapperPoses(importedPackageRoot);
            var result = new Dictionary<string, ResourcePreviewInfo>(StringComparer.Ordinal);
            AddImportedCatalogPreviewInfos(package?.UnityResourceCatalog, wrapperPoses, result);
            return result;
        }

        private static Dictionary<string, CharacterRuntimePose> ReadResourceMappingWrapperPoses(string importedPackageRoot)
        {
            var result = new Dictionary<string, CharacterRuntimePose>(StringComparer.Ordinal);
            string mappingPath = importedPackageRoot + "/config/resource_catalog_mapping.json";
            if (!File.Exists(mappingPath))
                return result;

            JObject root = JObject.Parse(File.ReadAllText(mappingPath));
            JArray entries = root["entries"] as JArray;
            if (entries == null)
                return result;

            for (int i = 0; i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                if (entry == null)
                    continue;

                string key = ReadString(entry, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                result[key] = ParsePose(entry["modelWrapperPose"] as JObject);
            }

            return result;
        }

        private static void AddImportedCatalogPreviewInfos(
            ResourceCatalog catalog,
            Dictionary<string, CharacterRuntimePose> wrapperPoses,
            Dictionary<string, ResourcePreviewInfo> result)
        {
            if (catalog == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (!string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
                    continue;

                string assetGuid = GetUnityAssetGuid(entry);
                string assetPath = ResolveUnityAssetPath(entry, assetGuid, requireGameObject: true);
                string importStatus = GetImportStatus(entry);
                bool isImportReady = IsUnityCatalogEntryImported(entry) && CanLoadGameObject(assetPath);

                string key = GetProviderData(entry, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(key))
                    key = entry.Id;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                CharacterRuntimePose wrapperPose = wrapperPoses != null && wrapperPoses.TryGetValue(key, out CharacterRuntimePose pose)
                    ? pose
                    : CreateDefaultPose();
                string usage = GetProviderData(entry, "usage");
                if (string.IsNullOrWhiteSpace(usage))
                    usage = InferUsage(entry);

                result[key] = new ResourcePreviewInfo(
                    key,
                    usage,
                    assetPath,
                    assetGuid,
                    importStatus,
                    isImportReady,
                    wrapperPose,
                    CreateResourceDiagnostic(entry, assetPath, assetGuid, importStatus, isImportReady));
            }
        }

        private static string ResolveUnityAssetPath(ResourceCatalogEntry entry, string assetGuid, bool requireGameObject)
        {
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (CanUseUnityAssetPath(guidPath, requireGameObject))
                    return guidPath;
            }

            string assetPath = GetProviderData(entry, "unityAssetPath");
            if (CanUseUnityAssetPath(assetPath, requireGameObject))
                return assetPath;

            assetPath = GetProviderData(entry, "assetPath");
            if (CanUseUnityAssetPath(assetPath, requireGameObject))
                return assetPath;

            if (CanUseUnityAssetPath(entry.Address, requireGameObject))
                return entry.Address;

            return string.Empty;
        }

        private static bool CanUseUnityAssetPath(string assetPath, bool requireGameObject)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return requireGameObject
                ? CanLoadGameObject(assetPath)
                : AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
        }

        private static bool CanLoadGameObject(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        private static bool IsUnityCatalogEntryImported(ResourceCatalogEntry entry)
        {
            return string.Equals(GetImportStatus(entry), "Imported", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetImportStatus(ResourceCatalogEntry entry)
        {
            string status = GetProviderData(entry, "importStatus");
            return !string.IsNullOrWhiteSpace(status) ? status : string.Empty;
        }

        private static string GetUnityAssetGuid(ResourceCatalogEntry entry)
        {
            string assetGuid = GetProviderData(entry, "unityAssetGuid");
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = GetProviderData(entry, "assetGuid");
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = GetProviderData(entry, "assetGUID");
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = GetProviderData(entry, "guid");
            return assetGuid;
        }

        private static string GetProviderData(ResourceCatalogEntry entry, string key)
        {
            return entry != null
                && entry.ProviderData != null
                && entry.ProviderData.TryGetValue(key, out string value)
                ? value
                : string.Empty;
        }

        private static bool HasLabel(ResourceCatalogEntry entry, string value)
        {
            if (entry == null || entry.Labels == null || string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string InferUsage(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                string label = entry.Labels[i] ?? string.Empty;
                const string usagePrefix = "character.usage.";
                if (label.StartsWith(usagePrefix, StringComparison.OrdinalIgnoreCase))
                    return label.Substring(usagePrefix.Length);
                if (label.Equals("character.model", StringComparison.OrdinalIgnoreCase))
                    return "characterModel";
                if (label.Equals("weapon.model", StringComparison.OrdinalIgnoreCase))
                    return "weaponModel";
            }

            return string.Empty;
        }

        private static ResourcePreviewInfo FindResource(Dictionary<string, ResourcePreviewInfo> resources, string usage, string resourceKey)
        {
            if (!string.IsNullOrWhiteSpace(resourceKey) && resources.TryGetValue(resourceKey, out ResourcePreviewInfo exact))
                return exact;
            if (!string.IsNullOrWhiteSpace(resourceKey))
                return null;

            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (string.Equals(resource.Usage, usage, StringComparison.OrdinalIgnoreCase))
                    return resource;
            }

            return null;
        }

        private static bool ValidatePreviewResources(Dictionary<string, ResourcePreviewInfo> resources)
        {
            bool isValid = true;
            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (resource.IsImportReady)
                    continue;
                if (!string.Equals(resource.Usage, "characterModel", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(resource.Usage, "weaponModel", StringComparison.OrdinalIgnoreCase))
                    continue;

                Debug.LogWarning("MxFramework Character Preview: imported model is not ready; prefab generation will use placeholders or skip default weapon mounting. "
                    + FormatResourceDiagnostic(resource));
            }

            return isValid;
        }

        private static CharacterWeaponAttachmentRuntimeBinding[] GetRuntimeMountedWeaponAttachments(
            CharacterWeaponAttachmentRuntimeBinding[] attachments,
            Dictionary<string, GameObject> weaponPrefabs)
        {
            if (attachments == null || attachments.Length == 0)
                return Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();

            var result = new List<CharacterWeaponAttachmentRuntimeBinding>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (ShouldMountWeaponAttachment(attachment, weaponPrefabs))
                    result.Add(attachment);
            }

            return result.ToArray();
        }

        private static bool ShouldMountWeaponAttachment(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            Dictionary<string, GameObject> weaponPrefabs)
        {
            return attachment != null
                && !string.IsNullOrWhiteSpace(attachment.PreviewResourceKey)
                && weaponPrefabs != null
                && weaponPrefabs.ContainsKey(GetAttachmentKey(attachment));
        }

        private static CharacterRuntimePose CreateDefaultPose()
        {
            return new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));
        }

        private static string CreateResourceDiagnostic(
            ResourceCatalogEntry entry,
            string assetPath,
            string assetGuid,
            string importStatus,
            bool isImportReady)
        {
            if (entry == null)
                return "resource=<missing catalog entry>";
            if (isImportReady)
                return string.Empty;

            string candidatePath = !string.IsNullOrWhiteSpace(assetPath)
                ? assetPath
                : FirstNonEmpty(
                    GetProviderData(entry, "unityAssetPath"),
                    GetProviderData(entry, "assetPath"),
                    entry.Address);
            string status = string.IsNullOrWhiteSpace(importStatus) ? "MissingImportStatus" : importStatus;
            return "resource=" + entry.Id
                + " status=" + status
                + " assetPath=" + (string.IsNullOrWhiteSpace(candidatePath) ? "<unresolved>" : candidatePath)
                + FormatAssetGuid(assetGuid);
        }

        private static string FormatResourceDiagnostic(ResourcePreviewInfo resource)
        {
            if (resource == null)
                return "resource=<missing catalog entry>; source=unity_resource_catalog";
            if (!string.IsNullOrWhiteSpace(resource.Diagnostic))
                return resource.Diagnostic;

            return "resource=" + resource.Key
                + " status=" + (string.IsNullOrWhiteSpace(resource.ImportStatus) ? "Imported" : resource.ImportStatus)
                + " assetPath=" + (string.IsNullOrWhiteSpace(resource.AssetPath) ? "<unresolved>" : resource.AssetPath)
                + FormatAssetGuid(resource.AssetGuid);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static CharacterRuntimePose ParsePose(JObject obj)
        {
            if (obj == null)
                return new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));

            return new CharacterRuntimePose(
                ReadString(obj, "parentKind"),
                ReadString(obj, "parentPath"),
                ParseVector3(obj["position"] as JObject, Vector3.zero),
                ParseQuaternion(obj["rotation"] as JObject),
                ParseVector3(obj["scale"] as JObject, Vector3.one));
        }

        private static CharacterRuntimeVector3 ParseVector3(JObject obj, Vector3 fallback)
        {
            if (obj == null)
                return new CharacterRuntimeVector3(fallback.x, fallback.y, fallback.z);
            return new CharacterRuntimeVector3(ReadFloat(obj, "x", fallback.x), ReadFloat(obj, "y", fallback.y), ReadFloat(obj, "z", fallback.z));
        }

        private static CharacterRuntimeQuaternion ParseQuaternion(JObject obj)
        {
            if (obj == null)
                return new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f);
            return new CharacterRuntimeQuaternion(ReadFloat(obj, "x", 0f), ReadFloat(obj, "y", 0f), ReadFloat(obj, "z", 0f), ReadFloat(obj, "w", 1f));
        }

        private static void ApplyPose(Transform transform, CharacterRuntimePose pose)
        {
            transform.localPosition = ToVector3(pose.Position, Vector3.zero);
            transform.localRotation = ToQuaternion(pose.Rotation);
            transform.localScale = ToVector3(pose.Scale, Vector3.one);
        }

        private static Vector3 ToVector3(CharacterRuntimeVector3 value, Vector3 fallback)
        {
            Vector3 vector = new Vector3(value.X, value.Y, value.Z);
            return vector == Vector3.zero && fallback == Vector3.one ? fallback : vector;
        }

        private static Quaternion ToQuaternion(CharacterRuntimeQuaternion value)
        {
            Quaternion q = new Quaternion(value.X, value.Y, value.Z, value.W);
            return q == default ? Quaternion.identity : q;
        }

        private static void AddMarker(GameObject node, Color color, float size)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Marker";
            sphere.transform.SetParent(node.transform, false);
            sphere.transform.localScale = Vector3.one * size;
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            AssignMaterial(sphere, color);
        }

        private static void AssignMaterial(GameObject gameObject, Color color)
        {
            Renderer renderer = gameObject.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return;

            Material material = GetOrCreatePreviewMaterial(color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            renderer.sharedMaterial = material;
        }

        private static Material GetOrCreatePreviewMaterial(Color color)
        {
            string materialName = "CharacterPreview_" + ColorUtility.ToHtmlStringRGBA(color);
            string materialPath = string.IsNullOrWhiteSpace(_previewMaterialFolder)
                ? string.Empty
                : _previewMaterialFolder + "/" + materialName + ".mat";
            Material material = !string.IsNullOrWhiteSpace(materialPath)
                ? AssetDatabase.LoadAssetAtPath<Material>(materialPath)
                : null;
            if (material != null)
                return material;

            material = new Material(FindPreviewShader())
            {
                name = materialName
            };

            if (!string.IsNullOrWhiteSpace(materialPath))
                AssetDatabase.CreateAsset(material, materialPath);

            return material;
        }

        private static Shader FindPreviewShader()
        {
            string[] names =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Standard"
            };

            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null)
                    return shader;
            }

            return Shader.Find("Standard");
        }

        private static void CreatePreviewCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.35f, -4.2f);
            camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            camera.fieldOfView = 45f;
        }

        private static void CreatePreviewLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static void CreatePreviewFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PreviewFloor";
            floor.transform.position = new Vector3(0f, -0.06f, 0f);
            floor.transform.localScale = new Vector3(4f, 0.08f, 4f);
            AssignMaterial(floor, new Color(0.25f, 0.28f, 0.3f));
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private static string ToProjectRelativePath(string path)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : path.Replace('\\', '/');
        }

        private static string NormalizeProjectPath(string path)
        {
            return ToProjectRelativePath(path).TrimEnd('/');
        }

        private static string FormatIssues(CharacterRuntimeSpawnResult result)
        {
            if (result == null || result.Issues.Length == 0)
                return "unknown error";

            var parts = new List<string>();
            for (int i = 0; i < result.Issues.Length; i++)
                parts.Add(result.Issues[i].Code + ": " + result.Issues[i].Message);
            return string.Join("; ", parts);
        }

        private static string FormatAssetGuid(string assetGuid)
        {
            return string.IsNullOrWhiteSpace(assetGuid) ? string.Empty : " guid=" + assetGuid;
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj == null ? string.Empty : (obj[key]?.Value<string>() ?? string.Empty);
        }

        private static float ReadFloat(JObject obj, string key, float fallback)
        {
            return obj == null ? fallback : (obj[key]?.Value<float?>() ?? fallback);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('.', '_').Replace('/', '_');
        }

        private static string SanitizeResourceIdSegment(string value)
        {
            return SanitizeName(value).ToLowerInvariant();
        }

        private readonly struct TransformBoneBindingInfo
        {
            private TransformBoneBindingInfo(
                string bindingId,
                string bonePath,
                Transform target,
                Transform bone,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                BindingId = bindingId ?? string.Empty;
                BonePath = bonePath ?? string.Empty;
                Target = target;
                Bone = bone;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale == Vector3.zero ? Vector3.one : localScale;
            }

            public string BindingId { get; }
            public string BonePath { get; }
            public Transform Target { get; }
            public Transform Bone { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }

            public static TransformBoneBindingInfo FromPose(
                string bindingId,
                string bonePath,
                Transform target,
                Transform bone,
                CharacterRuntimePose pose)
            {
                return new TransformBoneBindingInfo(
                    bindingId,
                    bonePath,
                    target,
                    bone,
                    ToVector3(pose.Position, Vector3.zero),
                    ToQuaternion(pose.Rotation),
                    ToVector3(pose.Scale, Vector3.one));
            }

            public static TransformBoneBindingInfo FromWorld(string bindingId, string bonePath, Transform target, Transform bone)
            {
                return new TransformBoneBindingInfo(
                    bindingId,
                    bonePath,
                    target,
                    bone,
                    bone.InverseTransformPoint(target.position),
                    Quaternion.Inverse(bone.rotation) * target.rotation,
                    target.localScale);
            }
        }

        private readonly struct RuntimeResourceBootstrapEntry
        {
            public RuntimeResourceBootstrapEntry(
                string id,
                string typeId,
                string providerId,
                string variant,
                string packageId,
                string address,
                UnityEngine.Object asset = null)
            {
                Id = id ?? string.Empty;
                TypeId = typeId ?? string.Empty;
                ProviderId = providerId ?? string.Empty;
                Variant = variant ?? string.Empty;
                PackageId = packageId ?? string.Empty;
                Address = address ?? string.Empty;
                Asset = asset;
            }

            public string Id { get; }
            public string TypeId { get; }
            public string ProviderId { get; }
            public string Variant { get; }
            public string PackageId { get; }
            public string Address { get; }
            public UnityEngine.Object Asset { get; }
        }

        private sealed class ResourcePreviewInfo
        {
            public ResourcePreviewInfo(
                string key,
                string usage,
                string assetPath,
                string assetGuid,
                string importStatus,
                bool isImportReady,
                CharacterRuntimePose wrapperPose,
                string diagnostic)
            {
                Key = key ?? string.Empty;
                Usage = usage ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                AssetGuid = assetGuid ?? string.Empty;
                ImportStatus = importStatus ?? string.Empty;
                IsImportReady = isImportReady;
                WrapperPose = wrapperPose;
                Diagnostic = diagnostic ?? string.Empty;
            }

            public string Key { get; }
            public string Usage { get; }
            public string AssetPath { get; }
            public string AssetGuid { get; }
            public string ImportStatus { get; }
            public bool IsImportReady { get; }
            public CharacterRuntimePose WrapperPose { get; }
            public string Diagnostic { get; }
        }
    }
}
