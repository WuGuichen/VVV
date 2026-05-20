using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
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
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                Selection.activeGameObject = instance;
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

            Dictionary<string, ResourcePreviewInfo> resources = ReadResourcePreviewInfos(root);
            EnsureFolder(root, "preview_materials");
            _previewMaterialFolder = root + "/preview_materials";
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

                var weaponsRoot = new GameObject("Weapons").transform;
                weaponsRoot.SetParent(rootObject.transform, false);

                Transform bodyModel = CreateBodyModel(package, resources, modelRoot);
                Dictionary<string, Transform> sockets = CreateSockets(package, socketsRoot, bodyModel);
                CreateWeapons(package, resources, weaponsRoot, sockets);
                CreateColliders(package, collidersRoot, bodyModel);

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

        private static Dictionary<string, Transform> CreateSockets(CharacterImportedPackage package, Transform socketsRoot, Transform bodyModel)
        {
            var sockets = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (int i = 0; i < package.Geometry.Sockets.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = package.Geometry.Sockets[i];
                Transform parent = FindBodyChild(bodyModel, socket.BonePath);
                if (parent == null)
                    parent = socketsRoot;

                var marker = new GameObject("Socket_" + socket.SocketId).transform;
                marker.SetParent(parent, false);
                ApplyPose(marker, socket.LocalPose);
                AddMarker(marker.gameObject, new Color(0.08f, 0.55f, 0.55f, 0.55f), 0.05f);
                sockets[socket.SocketId] = marker;
            }

            return sockets;
        }

        private static void CreateColliders(CharacterImportedPackage package, Transform collidersRoot, Transform bodyModel)
        {
            for (int i = 0; i < package.Geometry.BodyColliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding binding = package.Geometry.BodyColliders[i];
                var node = new GameObject("Collider_" + binding.ColliderId);
                Transform parent = FindBodyChild(bodyModel, binding.LocalPose.ParentPath);
                node.transform.SetParent(parent != null ? parent : collidersRoot, false);
                ApplyPose(node.transform, binding.LocalPose);
                AddCollider(node, binding);
            }
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

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(resource.AssetPath);
            if (asset == null)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset could not be loaded. Install/enable a GLB importer or reimport the asset: " + resource.AssetPath);
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

        private static Transform FindBodyChild(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            string normalized = path.StartsWith("bone.", StringComparison.Ordinal)
                ? path.Substring("bone.".Length)
                : path;
            Transform direct = root.Find(normalized);
            if (direct != null)
                return direct;

            string last = normalized.Contains("/") ? normalized.Substring(normalized.LastIndexOf("/", StringComparison.Ordinal) + 1) : normalized;
            return FindChildByName(root, last);
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

        private static Dictionary<string, ResourcePreviewInfo> ReadResourcePreviewInfos(string importedPackageRoot)
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
                    ParsePose(entry["modelWrapperPose"] as JObject));
            }

            return result;
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

        private sealed class ResourcePreviewInfo
        {
            public ResourcePreviewInfo(string key, string usage, string assetPath, CharacterRuntimePose wrapperPose)
            {
                Key = key ?? string.Empty;
                Usage = usage ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                WrapperPose = wrapperPose;
            }

            public string Key { get; }
            public string Usage { get; }
            public string AssetPath { get; }
            public CharacterRuntimePose WrapperPose { get; }
        }
    }
}
