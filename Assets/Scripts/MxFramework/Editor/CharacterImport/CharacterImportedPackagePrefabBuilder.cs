using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Editor.CharacterImport
{
    public static class CharacterImportedPackagePrefabBuilder
    {
        private const string DefaultImportedPackageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard";
        private const string PreviewScenePath = "Assets/Scenes/MxFramework/CharacterImportedPreview.unity";
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
            AnimationClip[] animationClips = LoadAnimationClips(package);
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

                Transform bodyModel = CreateBodyModel(package, resources, modelRoot);
                var boneBindings = new List<TransformBoneBindingInfo>();
                Dictionary<string, Transform> sockets = CreateSockets(package, socketsRoot, bodyModel, boneBindings);
                CreateWeapons(package, resources, weaponsRoot, sockets);
                CreateColliders(package, collidersRoot, bodyModel, boneBindings);
                AddBoneRuntimeSync(rootObject, boneBindings);
                AddRuntimeControllerBinding(rootObject, package, spawnResult);
                AddDefaultEquipmentRuntimeBinder(rootObject, package, spawnResult, socketsRoot, weaponsRoot, sockets, weaponPrefabs, animationClips);

                string prefabFolder = root + "/prefabs";
                EnsureFolder(root, "prefabs");
                string prefabPath = prefabFolder + "/" + package.PackageId + "_character_preview.prefab";
                rootObject.SetActive(true);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
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
                ResourcePreviewInfo resource = FindResource(resources, usage: "weaponModel", resourceKey: attachment.PreviewResourceKey);
                if (resource == null || !resource.IsImportReady)
                    continue;

                string prefabName = package.PackageId + "_" + SanitizeName(attachment.EquipSlot) + "_" + SanitizeName(attachment.WeaponId);
                string prefabPath = weaponFolder + "/" + prefabName + ".prefab";
                var root = new GameObject(prefabName);
                try
                {
                    root.SetActive(false);
                    CreateModelWrapper("Model", resource, root.transform);
                    root.SetActive(true);
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
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
            weapons.arraySize = package.Geometry.WeaponAttachments.Length;
            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
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

        private static AnimationClip[] LoadAnimationClips(CharacterImportedPackage package)
        {
            var clips = new List<AnimationClip>();
            if (package == null || package.ResourceMapping == null)
                return clips.ToArray();

            for (int i = 0; i < package.ResourceMapping.Entries.Length; i++)
            {
                CharacterImportedResourceMappingEntry entry = package.ResourceMapping.Entries[i];
                if (!string.Equals(entry.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                    continue;
                if (string.IsNullOrWhiteSpace(entry.ImportTargetPath))
                    continue;

                AddAnimationClipsAtPath(entry.ImportTargetPath, clips);
            }

            return clips.ToArray();
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

        private static GameObject CreateRuntimeResourceBootstrap(GameObject characterPrefab, string characterPrefabPath)
        {
            var loader = new GameObject("CharacterRuntimeResourceBootstrap");
            var bootstrap = loader.AddComponent<CharacterRuntimeResourceBootstrap>();
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(DefaultImportedPackageRoot));
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_catalogId").stringValue = "character.runtime." + package.PackageId;
            serialized.FindProperty("_packageId").stringValue = package.PackageId;
            serialized.FindProperty("_characterResourceId").stringValue = GetCharacterPrefabResourceId(package);
            serialized.FindProperty("_characterResourceVariant").stringValue = "default";
            serialized.FindProperty("_loadOnStart").boolValue = true;

            SerializedProperty resources = serialized.FindProperty("_resources");
            resources.arraySize = 1 + package.Geometry.WeaponAttachments.Length;
            SetSerializedResource(
                resources.GetArrayElementAtIndex(0),
                GetCharacterPrefabResourceId(package),
                ResourceTypeIds.GameObject,
                "default",
                package.PackageId,
                characterPrefabPath,
                characterPrefab);

            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                string weaponPrefabPath = DefaultImportedPackageRoot + "/prefabs/weapons/"
                    + package.PackageId + "_" + SanitizeName(attachment.EquipSlot) + "_" + SanitizeName(attachment.WeaponId) + ".prefab";
                GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPrefabPath);
                SetSerializedResource(
                    resources.GetArrayElementAtIndex(i + 1),
                    GetWeaponPrefabResourceId(package, attachment),
                    ResourceTypeIds.GameObject,
                    "default",
                    package.PackageId,
                    weaponPrefabPath,
                    weaponPrefab);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return loader;
        }

        private static void SetSerializedResource(
            SerializedProperty property,
            string id,
            string typeId,
            string variant,
            string packageId,
            string address,
            UnityEngine.Object asset)
        {
            property.FindPropertyRelative("_id").stringValue = id;
            property.FindPropertyRelative("_typeId").stringValue = typeId;
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
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.name = "MissingModelPlaceholder";
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

        private static GameObject InstantiateAsset(ResourcePreviewInfo resource)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.AssetPath))
                return null;

            if (!resource.IsImportReady)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset is not ready for prefab instantiation. Reimport the character package before building the prefab: " + resource.AssetPath + " status=" + resource.ImportStatus + FormatAssetGuid(resource.AssetGuid));
                return null;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(resource.AssetPath);
            if (asset == null)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset could not be loaded. Install/enable a GLB importer or reimport the asset: " + resource.AssetPath + FormatAssetGuid(resource.AssetGuid));
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
            Dictionary<string, ResourcePreviewInfo> result = ReadResourceMappingPreviewInfos(importedPackageRoot);
            AddImportedCatalogPreviewInfos(package?.UnityResourceCatalog, result);
            return result;
        }

        private static Dictionary<string, ResourcePreviewInfo> ReadResourceMappingPreviewInfos(string importedPackageRoot)
        {
            var result = new Dictionary<string, ResourcePreviewInfo>(StringComparer.Ordinal);
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

                result[key] = new ResourcePreviewInfo(
                    key,
                    ReadString(entry, "usage"),
                    ReadString(entry, "importTargetPath"),
                    string.Empty,
                    string.Empty,
                    false,
                    ParsePose(entry["modelWrapperPose"] as JObject));
            }

            return result;
        }

        private static void AddImportedCatalogPreviewInfos(ResourceCatalog catalog, Dictionary<string, ResourcePreviewInfo> result)
        {
            if (catalog == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (!string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
                    continue;

                string assetGuid = GetProviderData(entry, "assetGuid");
                if (string.IsNullOrWhiteSpace(assetGuid))
                    assetGuid = GetProviderData(entry, "unityAssetGuid");
                if (string.IsNullOrWhiteSpace(assetGuid))
                    assetGuid = GetProviderData(entry, "assetGUID");
                if (string.IsNullOrWhiteSpace(assetGuid))
                    assetGuid = GetProviderData(entry, "guid");

                string assetPath = ResolveImportedGameObjectAssetPath(entry, assetGuid);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                string key = GetProviderData(entry, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(key))
                    key = entry.Id;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                CharacterRuntimePose wrapperPose = result.TryGetValue(key, out ResourcePreviewInfo fallback)
                    ? fallback.WrapperPose
                    : new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));
                string usage = GetProviderData(entry, "usage");
                if (string.IsNullOrWhiteSpace(usage))
                    usage = InferUsage(entry);
                if (string.IsNullOrWhiteSpace(usage) && fallback != null)
                    usage = fallback.Usage;

                result[key] = new ResourcePreviewInfo(key, usage, assetPath, assetGuid, GetProviderData(entry, "importStatus"), true, wrapperPose);
            }
        }

        private static string ResolveImportedGameObjectAssetPath(ResourceCatalogEntry entry, string assetGuid)
        {
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (CanLoadGameObject(guidPath))
                    return guidPath;
            }

            string assetPath = GetProviderData(entry, "assetPath");
            if (CanLoadGameObject(assetPath))
                return assetPath;

            if (CanLoadGameObject(entry.Address))
                return entry.Address;

            return string.Empty;
        }

        private static bool CanLoadGameObject(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        private static string GetProviderData(ResourceCatalogEntry entry, string key)
        {
            return entry != null
                && entry.ProviderData != null
                && entry.ProviderData.TryGetValue(key, out string value)
                ? value
                : string.Empty;
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
                if (resource.IsImportReady || string.IsNullOrWhiteSpace(resource.AssetPath))
                    continue;
                if (!string.Equals(resource.Usage, "characterModel", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(resource.Usage, "weaponModel", StringComparison.OrdinalIgnoreCase))
                    continue;

                Debug.LogError("MxFramework Character Preview: imported model is not ready for prefab generation. Reimport the package and wait for Unity AssetDatabase import: " + resource.AssetPath + " status=" + resource.ImportStatus + FormatAssetGuid(resource.AssetGuid));
                isValid = false;
            }

            return isValid;
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

        private sealed class ResourcePreviewInfo
        {
            public ResourcePreviewInfo(string key, string usage, string assetPath, string assetGuid, string importStatus, bool isImportReady, CharacterRuntimePose wrapperPose)
            {
                Key = key ?? string.Empty;
                Usage = usage ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                AssetGuid = assetGuid ?? string.Empty;
                ImportStatus = importStatus ?? string.Empty;
                IsImportReady = isImportReady;
                WrapperPose = wrapperPose;
            }

            public string Key { get; }
            public string Usage { get; }
            public string AssetPath { get; }
            public string AssetGuid { get; }
            public string ImportStatus { get; }
            public bool IsImportReady { get; }
            public CharacterRuntimePose WrapperPose { get; }
        }
    }
}
