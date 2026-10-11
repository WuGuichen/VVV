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
    /// <summary>Validates exact serialized Boot-scene ownership without invoking the composition factory.</summary>
    public static class ApplicationUnityBootValidationAssetValidator
    {
        [MenuItem("MxFramework/Tests/Application Boot/Validate Validation Assets", priority = 211)]
        public static void ValidateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ValidateAndRestoreOpenScenes();
            Debug.Log("Application Unity Boot validation assets are valid.");
        }

        /// <summary>BatchMode validation entry that throws on the first exact-contract mismatch.</summary>
        public static void ValidateFromCommandLine()
        {
            if (!UnityEngine.Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ValidateAndRestoreOpenScenes();
        }

        public static void ValidateAssets()
        {
            PanelSettings panelSettings = RequireAsset<PanelSettings>(
                ApplicationUnityBootValidationAssets.PanelSettingsPath);
            ThemeStyleSheet expectedTheme = RequireAsset<ThemeStyleSheet>(
                ApplicationUnityBootValidationAssets.RuntimeThemePath);
            VisualTreeAsset visualTree = RequireAsset<VisualTreeAsset>(
                ApplicationUnityBootValidationAssets.VisualTreePath);
            RequireAsset<StyleSheet>(ApplicationUnityBootValidationAssets.StyleSheetPath);
            string[] visualTreeDependencies = AssetDatabase.GetDependencies(
                ApplicationUnityBootValidationAssets.VisualTreePath, true);
            if (Array.IndexOf(visualTreeDependencies, ApplicationUnityBootValidationAssets.StyleSheetPath) < 0)
                throw new InvalidOperationException("Boot validation UXML must own the exact USS dependency.");

            RequireSame(
                expectedTheme,
                panelSettings.themeStyleSheet,
                "PanelSettings must use the exact Unity default runtime theme.");
            Scene scene = EditorSceneManager.OpenScene(
                ApplicationUnityBootValidationAssets.ScenePath,
                OpenSceneMode.Single);
            ValidateOpenedScene(scene, panelSettings, visualTree);
        }

        private static void ValidateAndRestoreOpenScenes()
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
                ValidateAssets();
            }
            finally
            {
                if (!batchMode && setup != null)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ValidateOpenedScene(
            Scene scene,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path, ApplicationUnityBootValidationAssets.ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("The exact Boot validation scene could not be opened.");

            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length != 1)
                throw new InvalidOperationException("Boot validation scene must contain exactly one root GameObject.");

            GameObject rootObject = roots[0];
            if (!rootObject.activeSelf)
                throw new InvalidOperationException("Boot validation root must be serialized active.");
            if (rootObject.transform.childCount != 0)
                throw new InvalidOperationException("Boot validation root must not own child objects.");

            RequireExactlyOne<ApplicationUnityBootRoot>(rootObject);
            RequireExactlyOne<UnityApplicationLifecycleProxy>(rootObject);
            RequireExactlyOne<ApplicationUnityStartupValidationSurface>(rootObject);
            RequireExactlyOne<ApplicationUnityBootValidationCompositionFixture>(rootObject);
            RequireExactlyOne<UIDocument>(rootObject);

            Component[] components = rootObject.GetComponents<Component>();
            if (components.Length != 6)
                throw new InvalidOperationException(
                    "Boot validation root must contain only Transform and the five required components.");
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    throw new InvalidOperationException("Boot validation root contains a missing script.");
            }

            ApplicationUnityBootRoot bootRoot = rootObject.GetComponent<ApplicationUnityBootRoot>();
            UnityApplicationLifecycleProxy lifecycleProxy =
                rootObject.GetComponent<UnityApplicationLifecycleProxy>();
            ApplicationUnityStartupValidationSurface surface =
                rootObject.GetComponent<ApplicationUnityStartupValidationSurface>();
            ApplicationUnityBootValidationCompositionFixture composition =
                rootObject.GetComponent<ApplicationUnityBootValidationCompositionFixture>();
            UIDocument document = rootObject.GetComponent<UIDocument>();

            if (!bootRoot.enabled || !lifecycleProxy.enabled || !surface.enabled ||
                !composition.enabled || !document.enabled)
                throw new InvalidOperationException("Every required Boot validation component must be enabled.");

            RequireSame(panelSettings, document.panelSettings, "UIDocument PanelSettings reference is not exact.");
            RequireSame(visualTree, document.visualTreeAsset, "UIDocument UXML reference is not exact.");

            SerializedObject surfaceSerialized = new SerializedObject(surface);
            RequireSerializedReference(surfaceSerialized, "_document", document);
            RequireSerializedReference(surfaceSerialized, "_panelSettings", panelSettings);
            RequireSerializedInteger(
                surfaceSerialized,
                "_requestCapacity",
                ApplicationUnityBootValidationAssets.RequestCapacity);

            SerializedObject rootSerialized = new SerializedObject(bootRoot);
            RequireSerializedReference(rootSerialized, "_compositionFactoryAdapter", composition);
            RequireSerializedReference(rootSerialized, "_lifecycleProxy", lifecycleProxy);
            RequireSerializedReference(rootSerialized, "_startupSurfaceAdapter", surface);

            if (!(composition is IApplicationUnityBootCompositionFactory))
                throw new InvalidOperationException(
                    "Boot validation composition component must implement the typed factory contract.");
            // Deliberately do not call TryCreate: validation must not claim drains, publishers, or drivers.
        }

        private static T RequireExactlyOne<T>(GameObject gameObject) where T : Component
        {
            T[] components = gameObject.GetComponents<T>();
            if (components.Length != 1)
                throw new InvalidOperationException(
                    "Boot validation root must contain exactly one " + typeof(T).FullName + ".");
            return components[0];
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Required validation asset is missing: " + path);
            return asset;
        }

        private static void RequireSerializedReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().FullName +
                    " does not bind exact serialized reference " + propertyName + ".");
        }

        private static void RequireSerializedInteger(
            SerializedObject serialized,
            string propertyName,
            int expected)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.intValue != expected)
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().FullName +
                    " does not bind exact serialized integer " + propertyName + ".");
        }

        private static void RequireSame<T>(T expected, T actual, string message)
            where T : UnityEngine.Object
        {
            if (expected != actual)
                throw new InvalidOperationException(message);
        }

        private static void EnsureInteractiveScenesAreSafe()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException(
                        "Interactive Boot validation requires every open scene " +
                        "to be saved, named, and clean.");
            }
        }
    }
}
