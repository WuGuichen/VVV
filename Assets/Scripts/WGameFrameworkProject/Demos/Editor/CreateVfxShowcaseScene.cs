using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Demo.VfxShowcase;
using MxFramework.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class CreateVfxShowcaseScene
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        private const string ScenePath = VfxShowcaseDemoBootstrap.ScenePath;
        private const string UiFolderPath = "Assets/UI/MxFramework/VfxShowcase";
        private const string PanelSettingsPath = UiFolderPath + "/VfxShowcasePanelSettings.asset";
        private const string UxmlPath = UiFolderPath + "/VfxShowcaseHud.uxml";
        private const string UssPath = UiFolderPath + "/VfxShowcaseHud.uss";
        private const string CharacterPreviewPrefabPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/000001/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview.prefab";
        private const float CharacterPreviewScale = 0.60f;
        private static readonly Vector3 CasterPreviewPosition = new Vector3(0f, 0f, -3f);
        private static readonly Vector3 TargetPreviewPosition = new Vector3(0f, 0f, 5f);

        [MenuItem("MxFramework/VFX/Generate VFX Showcase Scene", priority = 126)]
        public static void Create()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/UI", "MxFramework");
            EnsureFolder("Assets/UI/MxFramework", "VfxShowcase");
            AssetDatabase.ImportAsset(UxmlPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceSynchronousImport);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            VfxShowcaseSerializedAsset[] vfxAssets = LoadSerializedVfxAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.42f, 0.47f, 0.48f);

            UnityEngine.Camera stageCamera = CreateCamera();
            CreateLight();
            CreateStage(
                out Transform previewRoot,
                out Transform muzzle,
                out Transform target,
                out Transform endpoint,
                out Transform ground,
                out Transform casterPreview,
                out Transform targetPreview);
            GameObject root = CreateRoot(panelSettings, stageCamera, visualTree, styleSheet, previewRoot, muzzle, target, endpoint, ground, casterPreview, targetPreview, vfxAssets);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { ScenePath }, ForceReserializeAssetsOptions.ReserializeAssets);
            RebindPersistedSceneReferences(ScenePath);
            Debug.Log("VFX showcase scene created: " + ScenePath + " (" + vfxAssets.Length + " VFX prefab refs)");
        }

        public static void ValidateSmoke()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("VFX showcase scene did not load: " + ScenePath);

            UnityEngine.Camera camera = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();
            if (camera == null || !camera.CompareTag("MainCamera"))
                throw new InvalidOperationException("VFX showcase scene must contain a Main Camera tagged MainCamera.");

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>();
            bool hasDirectionalLight = false;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    hasDirectionalLight = true;
                    break;
                }
            }

            if (!hasDirectionalLight)
                throw new InvalidOperationException("VFX showcase scene must contain a Directional Light.");

            VfxShowcaseDemoBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<VfxShowcaseDemoBootstrap>();
            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            if (bootstrap == null || document == null)
                throw new InvalidOperationException("VFX showcase scene is missing bootstrap or UIDocument.");
            if (document.panelSettings == null || document.visualTreeAsset == null)
                throw new InvalidOperationException("VFX showcase UIDocument must bind PanelSettings and UXML.");

            RequireSerializedReference<VisualTreeAsset>(bootstrap, "_visualTree");
            RequireSerializedReference<UnityEngine.Camera>(bootstrap, "_stageCamera");
            RequireSerializedReference<StyleSheet>(bootstrap, "_styleSheet");
            RequireSerializedReference<Transform>(bootstrap, "_previewRoot");
            RequireSerializedReference<Transform>(bootstrap, "_muzzleAnchor");
            RequireSerializedReference<Transform>(bootstrap, "_targetAnchor");
            RequireSerializedReference<Transform>(bootstrap, "_endpointAnchor");
            RequireSerializedReference<Transform>(bootstrap, "_groundAnchor");
            RequireSerializedReference<Transform>(bootstrap, "_casterPreviewRoot");
            RequireSerializedReference<Transform>(bootstrap, "_targetPreviewRoot");
            RequireSerializedAssetArray(bootstrap);
            if (GameObject.Find("VfxShowcaseCaster") == null || GameObject.Find("VfxShowcaseTarget") == null)
                throw new InvalidOperationException("VFX showcase scene must contain caster and target character preview models.");

            VisualElement tree = document.visualTreeAsset.CloneTree();
            RequireElement<TextField>(tree, "search-field");
            RequireElement<VisualElement>(tree, "group-row");
            RequireElement<ScrollView>(tree, "entry-list");
            RequireElement<Image>(tree, "stage-view");
            RequireElement<Button>(tree, "play-button");
            RequireElement<Button>(tree, "replay-button");
            RequireElement<Button>(tree, "clear-button");
            RequireElement<Toggle>(tree, "loop-toggle");
            RequireElement<Toggle>(tree, "auto-advance-toggle");
            RequireElement<Slider>(tree, "length-slider");
            RequireElement<Slider>(tree, "target-distance-slider");
            RequireElement<VisualElement>(tree, "background-swatch-row");
            RequireElement<Label>(tree, "definition-value");
            RequireElement<Label>(tree, "resource-value");
            RequireElement<Label>(tree, "asset-path-value");

            Debug.Log("VFX showcase smoke validation passed.");
        }

        public static void ValidatePlayableSmoke()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("VFX showcase scene did not load: " + ScenePath);

            VfxShowcaseDemoBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<VfxShowcaseDemoBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException("VFX showcase scene is missing bootstrap.");

            bootstrap.InitializeForValidation();
            if (!bootstrap.IsInitialized || bootstrap.EntryCount <= 0)
                throw new InvalidOperationException("VFX showcase did not initialize VFX entries.");
            if (bootstrap.RuntimeDefinitionCount <= 0 || bootstrap.CatalogPrefabCount <= 0)
                throw new InvalidOperationException("VFX showcase must contain runtime definitions and catalog-only prefabs.");

            RequirePlayableEntry(bootstrap, "vfx.magic.fireball", true);
            RequirePlayableEntry(bootstrap, "vfx.status_aura.burn", false);
            bootstrap.SetLengthForValidation(8f);
            RequirePlayableEntry(bootstrap, "vfx.magic.chain_lightning.arc", true);
            RequirePlayableEntry(bootstrap, "vfx.magic.poison_bottle.pool", true);

            bootstrap.ClearForValidation();
            bootstrap.DisposeForValidation();
            Debug.Log("VFX showcase playable smoke validation passed.");
        }

        private static void RequirePlayableEntry(VfxShowcaseDemoBootstrap bootstrap, string resourceId, bool runtimeDefinition)
        {
            if (!bootstrap.SelectEntryForValidation(resourceId))
                throw new InvalidOperationException("VFX showcase entry missing: " + resourceId);
            if (!bootstrap.ReplayForValidation())
                throw new InvalidOperationException("VFX showcase entry did not replay: " + resourceId);
            bootstrap.RunFrameForValidation(0.034f);
            if (runtimeDefinition && bootstrap.ActiveRuntimeVfxCount <= 0)
                throw new InvalidOperationException("RuntimeDefinition VFX did not create a runtime instance: " + resourceId);
            if (!runtimeDefinition && bootstrap.ActiveDirectPrefabCount <= 0)
                throw new InvalidOperationException("CatalogPrefab VFX did not instantiate a prefab: " + resourceId);
            bootstrap.ClearForValidation();
        }

        private static GameObject CreateRoot(
            PanelSettings panelSettings,
            UnityEngine.Camera stageCamera,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform previewRoot,
            Transform muzzle,
            Transform target,
            Transform endpoint,
            Transform ground,
            Transform casterPreview,
            Transform targetPreview,
            VfxShowcaseSerializedAsset[] vfxAssets)
        {
            var root = new GameObject("MxVfxShowcaseRoot");
            UIDocument document = root.AddComponent<UIDocument>();
            ConfigureDocument(document, panelSettings, visualTree);
            VfxShowcaseDemoBootstrap bootstrap = root.AddComponent<VfxShowcaseDemoBootstrap>();
            bootstrap.ConfigureSceneReferences(document, stageCamera, visualTree, styleSheet, previewRoot, muzzle, target, endpoint, ground, casterPreview, targetPreview, vfxAssets);
            SetBootstrapReferences(bootstrap, document, stageCamera, visualTree, styleSheet, previewRoot, muzzle, target, endpoint, ground, casterPreview, targetPreview, vfxAssets);
            return root;
        }

        private static UnityEngine.Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.062f, 0.064f);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.05f;
            cameraObject.transform.position = new Vector3(0f, 3.8f, -10.5f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.9f, 3.2f), Vector3.up);
            return camera;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fillObject = new GameObject("VFX Showcase Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.5f;
            fill.range = 16f;
            fillObject.transform.position = new Vector3(0f, 3.2f, -3.8f);
        }

        private static void CreateStage(
            out Transform previewRoot,
            out Transform muzzle,
            out Transform target,
            out Transform endpoint,
            out Transform ground,
            out Transform casterPreview,
            out Transform targetPreview)
        {
            GameObject stage = new GameObject("MxVfxShowcaseStage");
            previewRoot = new GameObject("PreviewRoot").transform;
            previewRoot.SetParent(stage.transform, false);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "VfxShowcaseFloor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.06f, 4f);
            floor.transform.localScale = new Vector3(14f, 0.08f, 18f);
            AssignMaterial(floor, new Color(0.13f, 0.16f, 0.16f));
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());

            GameObject centerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerLine.name = "VfxShowcaseForwardLine";
            centerLine.transform.SetParent(stage.transform, false);
            centerLine.transform.localPosition = new Vector3(0f, 0.01f, 4f);
            centerLine.transform.localScale = new Vector3(0.035f, 0.02f, 15.5f);
            AssignMaterial(centerLine, new Color(0.18f, 0.55f, 0.50f));
            UnityEngine.Object.DestroyImmediate(centerLine.GetComponent<Collider>());

            GameObject caster = InstantiateCharacterPreview(stage.transform, "VfxShowcaseCaster", CasterPreviewPosition, Quaternion.identity);
            GameObject targetCharacter = InstantiateCharacterPreview(stage.transform, "VfxShowcaseTarget", TargetPreviewPosition, Quaternion.Euler(0f, 180f, 0f));
            casterPreview = caster.transform;
            targetPreview = targetCharacter.transform;
            CreateRoleRing(stage.transform, "CasterPreviewRing", CasterPreviewPosition, new Color(0.16f, 0.62f, 0.55f, 0.9f));
            CreateRoleRing(stage.transform, "TargetPreviewRing", TargetPreviewPosition, new Color(0.82f, 0.42f, 0.18f, 0.9f));

            muzzle = CreateMuzzleAnchor(caster.transform);
            target = CreateAnchor(targetCharacter.transform, "TargetAnchor", new Vector3(0f, 1.75f, 0f));
            endpoint = CreateAnchor(stage.transform, "EndpointAnchor", muzzle.position + Vector3.forward * 8f);
            ground = CreateAnchor(stage.transform, "GroundAnchor", new Vector3(TargetPreviewPosition.x, 0.04f, TargetPreviewPosition.z));
            muzzle.forward = Vector3.forward;
            target.forward = Vector3.forward;
            endpoint.forward = Vector3.forward;
            ground.forward = Vector3.forward;
        }

        private static GameObject InstantiateCharacterPreview(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = LoadRequired<GameObject>(CharacterPreviewPrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Character preview prefab could not be instantiated: " + CharacterPreviewPrefabPath);

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;
            instance.transform.localScale = Vector3.one * CharacterPreviewScale;
            DisablePreviewHelpers(instance.transform);
            return instance;
        }

        private static Transform CreateMuzzleAnchor(Transform caster)
        {
            Transform socket = FindDescendant(caster, "Socket_mainHand");
            Vector3 localPosition = socket != null
                ? caster.InverseTransformPoint(socket.position)
                : new Vector3(0.46f, 0.82f, 0f);
            return CreateAnchor(caster, "MuzzleAnchor", localPosition);
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = Quaternion.identity;
            anchor.transform.localScale = Vector3.one;
            return anchor.transform;
        }

        private static void CreateRoleRing(Transform parent, string name, Vector3 position, Color color)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = name;
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(position.x, 0.012f, position.z);
            ring.transform.localScale = new Vector3(0.86f, 0.012f, 0.86f);
            AssignMaterial(ring, color);
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
        }

        private static void DisablePreviewHelpers(Transform root)
        {
            SetDescendantActive(root, "Marker", false);
            SetDescendantActive(root, "Diagnostics", false);
            SetDescendantActive(root, "AuthoringColliders", false);
        }

        private static void SetDescendantActive(Transform root, string childName, bool active)
        {
            Transform child = FindDescendant(root, childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (string.Equals(root.name, childName, StringComparison.Ordinal))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static VfxShowcaseSerializedAsset[] LoadSerializedVfxAssets()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            var assets = new List<VfxShowcaseSerializedAsset>(catalog.Entries.Count);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (!IsVfxGameObjectEntry(entry))
                    continue;

                if (!entry.ProviderData.TryGetValue(TempImportedResourceCatalog.ProviderDataAssetPathKey, out string assetPath))
                    throw new InvalidOperationException("VFX catalog entry is missing assetPath: " + entry.Id);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    throw new InvalidOperationException("VFX prefab could not be loaded: " + assetPath);

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                assets.Add(new VfxShowcaseSerializedAsset(key.Id, key.TypeId, key.Variant, key.PackageId, assetPath, prefab));
            }

            return assets.ToArray();
        }

        private static bool IsVfxGameObjectEntry(ResourceCatalogEntry entry)
        {
            if (entry == null || !string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
                return false;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], TempImportedResourceCatalog.DomainVfxLabel, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "VfxShowcasePanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 130f;
            StyleSheet runtimeTheme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            if (runtimeTheme != null)
            {
                var serialized = new SerializedObject(settings);
                Set(serialized, "themeUss", runtimeTheme);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            DisableNoThemeWarningField?.SetValue(settings, true);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void ConfigureDocument(UIDocument document, PanelSettings panelSettings, VisualTreeAsset visualTree)
        {
            var serialized = new SerializedObject(document);
            serialized.UpdateIfRequiredOrScript();
            SetRequired(serialized, "m_PanelSettings", panelSettings);
            SetRequired(serialized, "sourceAsset", visualTree);
            serialized.ApplyModifiedProperties();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static void SetBootstrapReferences(
            VfxShowcaseDemoBootstrap bootstrap,
            UIDocument document,
            UnityEngine.Camera stageCamera,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform previewRoot,
            Transform muzzle,
            Transform target,
            Transform endpoint,
            Transform ground,
            Transform casterPreview,
            Transform targetPreview,
            VfxShowcaseSerializedAsset[] vfxAssets)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.UpdateIfRequiredOrScript();
            SetRequired(serialized, "_document", document);
            SetRequired(serialized, "_stageCamera", stageCamera);
            SetRequired(serialized, "_visualTree", visualTree);
            SetRequired(serialized, "_styleSheet", styleSheet);
            SetRequired(serialized, "_previewRoot", previewRoot);
            SetRequired(serialized, "_muzzleAnchor", muzzle);
            SetRequired(serialized, "_targetAnchor", target);
            SetRequired(serialized, "_endpointAnchor", endpoint);
            SetRequired(serialized, "_groundAnchor", ground);
            SetRequired(serialized, "_casterPreviewRoot", casterPreview);
            SetRequired(serialized, "_targetPreviewRoot", targetPreview);
            SetSerializedAssets(serialized, "_serializedAssets", vfxAssets);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void RebindPersistedSceneReferences(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            VfxShowcaseDemoBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<VfxShowcaseDemoBootstrap>();
            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            UnityEngine.Camera stageCamera = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();
            Transform previewRoot = GameObject.Find("PreviewRoot")?.transform;
            Transform muzzle = GameObject.Find("MuzzleAnchor")?.transform;
            Transform target = GameObject.Find("TargetAnchor")?.transform;
            Transform endpoint = GameObject.Find("EndpointAnchor")?.transform;
            Transform ground = GameObject.Find("GroundAnchor")?.transform;
            Transform casterPreview = GameObject.Find("VfxShowcaseCaster")?.transform;
            Transform targetPreview = GameObject.Find("VfxShowcaseTarget")?.transform;
            if (bootstrap == null || document == null || stageCamera == null || previewRoot == null || muzzle == null || target == null || endpoint == null || ground == null || casterPreview == null || targetPreview == null)
                throw new InvalidOperationException("VFX showcase scene references could not be rebound.");

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            VisualTreeAsset visualTree = LoadRequired<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = LoadRequired<StyleSheet>(UssPath);
            VfxShowcaseSerializedAsset[] vfxAssets = LoadSerializedVfxAssets();
            ConfigureDocument(document, panelSettings, visualTree);
            bootstrap.ConfigureSceneReferences(document, stageCamera, visualTree, styleSheet, previewRoot, muzzle, target, endpoint, ground, casterPreview, targetPreview, vfxAssets);
            SetBootstrapReferences(bootstrap, document, stageCamera, visualTree, styleSheet, previewRoot, muzzle, target, endpoint, ground, casterPreview, targetPreview, vfxAssets);
            RequireSerializedReference<VisualTreeAsset>(bootstrap, "_visualTree");
            RequireSerializedReference<UnityEngine.Camera>(bootstrap, "_stageCamera");
            RequireSerializedReference<Transform>(bootstrap, "_casterPreviewRoot");
            RequireSerializedReference<Transform>(bootstrap, "_targetPreviewRoot");
            RequireSerializedAssetArray(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AssignMaterial(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Material material = target.name.IndexOf("Floor", System.StringComparison.Ordinal) >= 0
                ? WGameDemoMaterialFactory.CreateEnvironment(target.name + "_VfxShowcaseMaterial", color)
                : WGameDemoMaterialFactory.CreateMarker(target.name + "_VfxShowcaseMaterial", color);
            renderer.sharedMaterial = material;
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Required asset could not be loaded: " + path);

            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetRequired(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException("Serialized property missing: " + propertyName);

            property.objectReferenceValue = value;
        }

        private static void SetSerializedAssets(SerializedObject serialized, string propertyName, VfxShowcaseSerializedAsset[] values)
        {
            SerializedProperty array = serialized.FindProperty(propertyName);
            if (array == null || !array.isArray)
                throw new InvalidOperationException("Serialized VFX asset array property missing: " + propertyName);

            values = values ?? Array.Empty<VfxShowcaseSerializedAsset>();
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                SetString(element, "_resourceId", values[i].ResourceId);
                SetString(element, "_typeId", values[i].TypeId);
                SetString(element, "_variant", values[i].Variant);
                SetString(element, "_packageId", values[i].PackageId);
                SetString(element, "_assetPath", values[i].AssetPath);
                SetObject(element, "_prefab", values[i].Prefab);
            }
        }

        private static void SetString(SerializedProperty root, string relativePath, string value)
        {
            SerializedProperty property = root.FindPropertyRelative(relativePath);
            if (property == null)
                throw new InvalidOperationException("Serialized property missing: " + relativePath);

            property.stringValue = value ?? string.Empty;
        }

        private static void SetObject(SerializedProperty root, string relativePath, UnityEngine.Object value)
        {
            SerializedProperty property = root.FindPropertyRelative(relativePath);
            if (property == null)
                throw new InvalidOperationException("Serialized property missing: " + relativePath);

            property.objectReferenceValue = value;
        }

        private static void RequireSerializedReference<T>(UnityEngine.Object owner, string propertyName)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
                throw new InvalidOperationException("Serialized reference missing: " + owner.name + "." + propertyName);
            if (!(property.objectReferenceValue is T))
                throw new InvalidOperationException("Serialized reference has wrong type: " + owner.name + "." + propertyName);
        }

        private static void RequireSerializedAssetArray(VfxShowcaseDemoBootstrap bootstrap)
        {
            var serialized = new SerializedObject(bootstrap);
            SerializedProperty array = serialized.FindProperty("_serializedAssets");
            if (array == null || !array.isArray || array.arraySize <= 0)
                throw new InvalidOperationException("VFX showcase serialized asset array is empty.");

            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                SerializedProperty resourceId = element.FindPropertyRelative("_resourceId");
                SerializedProperty prefab = element.FindPropertyRelative("_prefab");
                if (resourceId == null || string.IsNullOrWhiteSpace(resourceId.stringValue) || prefab == null || prefab.objectReferenceValue == null)
                    throw new InvalidOperationException("VFX showcase serialized asset entry is incomplete at index " + i + ".");
            }
        }

        private static T RequireElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException("VFX showcase UXML element missing: " + name);

            return element;
        }
    }
}
