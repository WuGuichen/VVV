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
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private static void CreateOrOverwriteRuntimeScene(
            string importedPackageRoot,
            string scenePath,
            bool ensureCalibrationRunner,
            NewSceneMode newSceneMode,
            bool selectLoader)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            string prefabPath = BuildPrefab(root, selectPrefab: false);
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            CharacterImportedPackage package = LoadImportedPackage(root, "runtime scene creation");
            if (package == null)
                return;

            EnsureRuntimeSceneFolders();

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, newSceneMode);
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                InitializeRuntimeEntryScene();

                GameObject loader = CreateRuntimeResourceBootstrap(root, package, prefabPath);
                if (ensureCalibrationRunner)
                {
                    loader.name = "CharacterLocomotionCalibrationRunner";
                    EnsureLocomotionCalibrationRunner(loader, loader.GetComponent<CharacterRuntimeResourceBootstrap>());
                }

                if (selectLoader)
                    Selection.activeGameObject = loader;

                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("MxFramework Character runtime scene created: " + scenePath);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    EditorSceneManager.SetActiveScene(previousActiveScene);
            }
        }

        private static void SynchronizeRuntimeScene(
            string importedPackageRoot,
            CharacterImportedPackage package,
            string prefabPath,
            string scenePath,
            bool ensureCalibrationRunner)
        {
            if (package == null)
                return;

            bool allowCreate = IsDefaultImportedPackageRoot(importedPackageRoot);
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null && !allowCreate)
                return;

            EnsureRuntimeSceneFolders();

            Scene previousActiveScene = SceneManager.GetActiveScene();
            bool closeSceneAfterSync = false;
            Scene scene;
            if (sceneAsset != null)
            {
                scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    closeSceneAfterSync = true;
                }
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                closeSceneAfterSync = true;
            }
            try
            {
                EditorSceneManager.SetActiveScene(scene);
                if (sceneAsset == null)
                    InitializeRuntimeEntryScene();

                CharacterRuntimeResourceBootstrap bootstrap = FindRuntimeResourceBootstrap(scene);
                if (bootstrap != null
                    && !string.IsNullOrWhiteSpace(bootstrap.PackageId)
                    && !string.Equals(bootstrap.PackageId, package.PackageId, StringComparison.Ordinal))
                {
                    Debug.Log("MxFramework Character runtime sync skipped for scene " + scenePath
                        + " because it is bound to package " + bootstrap.PackageId + " instead of " + package.PackageId + ".");
                    return;
                }

                if (bootstrap == null)
                {
                    GameObject loader = CreateRuntimeResourceBootstrap(importedPackageRoot, package, prefabPath);
                    if (ensureCalibrationRunner)
                    {
                        loader.name = "CharacterLocomotionCalibrationRunner";
                        EnsureLocomotionCalibrationRunner(loader, loader.GetComponent<CharacterRuntimeResourceBootstrap>());
                    }

                    bootstrap = loader.GetComponent<CharacterRuntimeResourceBootstrap>();
                }
                else
                {
                    ConfigureRuntimeResourceBootstrap(bootstrap, importedPackageRoot, package, prefabPath);
                    if (ensureCalibrationRunner)
                    {
                        bootstrap.gameObject.name = "CharacterLocomotionCalibrationRunner";
                        EnsureLocomotionCalibrationRunner(bootstrap.gameObject, bootstrap);
                    }
                    else if (string.Equals(bootstrap.gameObject.name, "CharacterLocomotionCalibrationRunner", StringComparison.Ordinal))
                    {
                        bootstrap.gameObject.name = "CharacterRuntimeResourceBootstrap";
                    }
                }

                EditorUtility.SetDirty(bootstrap);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    EditorSceneManager.SetActiveScene(previousActiveScene);
                if (closeSceneAfterSync && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureRuntimeSceneFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/Scenes", "MxFramework");
        }

        private static void InitializeRuntimeEntryScene()
        {
            RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.64f);
            CreatePreviewCamera();
            CreatePreviewLight();
            CreatePreviewFloor();
        }

        private static bool IsDefaultImportedPackageRoot(string importedPackageRoot)
        {
            string root = NormalizeProjectPath(importedPackageRoot).TrimEnd('/');
            return string.Equals(root, DefaultImportedPackageRoot, StringComparison.Ordinal);
        }

        private static CharacterRuntimeResourceBootstrap FindRuntimeResourceBootstrap(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                CharacterRuntimeResourceBootstrap bootstrap =
                    rootObjects[i].GetComponentInChildren<CharacterRuntimeResourceBootstrap>(includeInactive: true);
                if (bootstrap != null)
                    return bootstrap;
            }

            return null;
        }
    }
}
