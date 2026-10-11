using System;
using MxFramework.ApplicationStartup.Unity;
using MxFramework.ApplicationStartup.Unity.Validation;
using MxFramework.Platform.Unity;
using MxFramework.Tests.PlayMode.Application;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Application.Unity.Editor
{
    /// <summary>Creates the serialized Unity validation entry without hand-authored YAML.</summary>
    public static class ApplicationUnityBootValidationAssetGenerator
    {
        [MenuItem("MxFramework/Tests/Application Boot/Rebuild Validation Assets", priority = 210)]
        public static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            GenerateAndRestoreOpenScenes();
            Debug.Log("Rebuilt Application Unity Boot validation assets.");
        }

        /// <summary>BatchMode entry used by CI and evidence generation.</summary>
        public static void GenerateFromCommandLine()
        {
            if (!UnityEngine.Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            GenerateAndRestoreOpenScenes();
            Debug.Log("Generated Application Unity Boot validation assets from command line.");
        }

        private static void GenerateAndRestoreOpenScenes()
        {
            bool batchMode = UnityEngine.Application.isBatchMode;
            SceneSetup[] setup = null;
            if (batchMode)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                EnsureInteractiveScenesAreSafe();
                setup = EditorSceneManager.GetSceneManagerSetup();
            }
            try
            {
                EnsureAssetFolder("Assets/Scenes/Tests/Application");
                EnsureAssetFolder("Assets/UI/MxFramework/ApplicationBoot");

                PanelSettings panelSettings = LoadOrCreatePanelSettings();
                VisualTreeAsset visualTree = LoadRequiredAsset<VisualTreeAsset>(
                    ApplicationUnityBootValidationAssets.VisualTreePath);
                LoadRequiredAsset<StyleSheet>(ApplicationUnityBootValidationAssets.StyleSheetPath);

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject rootObject = new GameObject("ApplicationUnityBootRoot");
                rootObject.SetActive(false);
                UIDocument document = rootObject.AddComponent<UIDocument>();
                UnityApplicationLifecycleProxy lifecycleProxy =
                    rootObject.AddComponent<UnityApplicationLifecycleProxy>();
                ApplicationUnityStartupValidationSurface surface =
                    rootObject.AddComponent<ApplicationUnityStartupValidationSurface>();
                ApplicationUnityBootValidationCompositionFixture composition =
                    rootObject.AddComponent<ApplicationUnityBootValidationCompositionFixture>();
                ApplicationUnityBootRoot bootRoot = rootObject.AddComponent<ApplicationUnityBootRoot>();

                ConfigureDocument(document, panelSettings, visualTree);
                ConfigureSurface(surface, document, panelSettings);
                ConfigureBootRoot(bootRoot, composition, lifecycleProxy, surface);
                rootObject.SetActive(true);

                if (!EditorSceneManager.SaveScene(
                        scene,
                        ApplicationUnityBootValidationAssets.ScenePath,
                        false))
                    throw new InvalidOperationException(
                        "Failed to save Application Unity Boot validation scene.");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ApplicationUnityBootValidationAssetValidator.ValidateAssets();
            }
            finally
            {
                if (!batchMode && setup != null)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            ThemeStyleSheet theme = LoadRequiredAsset<ThemeStyleSheet>(
                ApplicationUnityBootValidationAssets.RuntimeThemePath);
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                ApplicationUnityBootValidationAssets.PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "ApplicationBootPanelSettings";
                AssetDatabase.CreateAsset(
                    settings,
                    ApplicationUnityBootValidationAssets.PanelSettingsPath);
            }

            // Unity 6 exposes this as the supported authoring API. No private-field fallback is used.
            settings.themeStyleSheet = theme;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void ConfigureDocument(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);
        }

        private static void ConfigureSurface(
            ApplicationUnityStartupValidationSurface surface,
            UIDocument document,
            PanelSettings panelSettings)
        {
            SerializedObject serialized = new SerializedObject(surface);
            serialized.UpdateIfRequiredOrScript();
            SetRequiredReference(serialized, "_document", document);
            SetRequiredReference(serialized, "_panelSettings", panelSettings);
            SetRequiredInteger(
                serialized,
                "_requestCapacity",
                ApplicationUnityBootValidationAssets.RequestCapacity);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(surface);
        }

        private static void ConfigureBootRoot(
            ApplicationUnityBootRoot bootRoot,
            ApplicationUnityBootValidationCompositionFixture composition,
            UnityApplicationLifecycleProxy lifecycleProxy,
            ApplicationUnityStartupValidationSurface surface)
        {
            SerializedObject serialized = new SerializedObject(bootRoot);
            serialized.UpdateIfRequiredOrScript();
            SetRequiredReference(serialized, "_compositionFactoryAdapter", composition);
            SetRequiredReference(serialized, "_lifecycleProxy", lifecycleProxy);
            SetRequiredReference(serialized, "_startupSurfaceAdapter", surface);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bootRoot);
        }

        private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Required validation asset is missing: " + path);
            return asset;
        }

        private static void SetRequiredReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().FullName +
                    " is missing serialized property " + propertyName + ".");
            property.objectReferenceValue = value;
        }

        private static void SetRequiredInteger(
            SerializedObject serialized,
            string propertyName,
            int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().FullName +
                    " is missing serialized property " + propertyName + ".");
            property.intValue = value;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separator = path.LastIndexOf('/');
            if (separator <= 0)
                throw new InvalidOperationException("Invalid asset folder path: " + path);
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureAssetFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Failed to create asset folder: " + path);
        }

        private static void EnsureInteractiveScenesAreSafe()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException(
                        "Interactive Boot validation generation requires every open scene " +
                        "to be saved, named, and clean.");
            }
        }
    }
}
