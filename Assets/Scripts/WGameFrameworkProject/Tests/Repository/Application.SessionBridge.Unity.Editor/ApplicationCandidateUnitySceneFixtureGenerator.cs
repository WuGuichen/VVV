using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Tests.PlayMode.Application;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.ApplicationStartup.SessionBridge.Unity.Editor
{
    /// <summary>Materializes the reviewed candidate-scene test fixtures through Unity serialization.</summary>
    public static class ApplicationCandidateUnitySceneFixtureGenerator
    {
        [Serializable]
        private sealed class FixtureManifestDocument
        {
            public string schema = ApplicationCandidateUnitySceneFixture.Schema;
            public string sceneKey;
            public string exactScenePath;
            public string artifactId;
            public string markerId;
            public long revision;
            public int expectedRootCount;
            public string dependencyHash;
            public bool approved;
        }

        [MenuItem("MxFramework/Tests/Rebuild Candidate Unity Scene Fixtures")]
        public static void Generate()
        {
            EnsureFolder("Assets/Scenes/Tests");
            EnsureFolder("Assets/Scenes/Tests/Decoy");
            bool batchMode = Application.isBatchMode;
            SceneSetup[] setup = null;
            if (batchMode)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    throw new OperationCanceledException("Candidate Unity fixture generation was cancelled.");
                EnsureInteractiveScenesAreSafe();
                setup = EditorSceneManager.GetSceneManagerSetup();
            }
            try
            {
                GenerateScene(ApplicationCandidateUnitySceneFixture.ScenePath, false);
                GenerateScene(ApplicationCandidateUnitySceneFixture.UnapprovedActiveRootScenePath, true);
                GenerateDecoyScene();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                WriteManifest(
                    ApplicationCandidateUnitySceneFixture.ManifestPath,
                    ApplicationCandidateUnitySceneFixture.ScenePath,
                    true);
                WriteManifest(
                    ApplicationCandidateUnitySceneFixture.UnapprovedManifestPath,
                    ApplicationCandidateUnitySceneFixture.UnapprovedActiveRootScenePath,
                    false);
                WriteManifest(
                    ApplicationCandidateUnitySceneFixture.DecoyManifestPath,
                    ApplicationCandidateUnitySceneFixture.DecoyScenePath,
                    true,
                    ApplicationCandidateUnitySceneFixture.DecoyArtifactId,
                    1);
                AddFixtureToBuildSettings();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ApplicationCandidateUnitySceneFixtureValidator.ValidateOrThrow();
            }
            finally
            {
                if (!batchMode && setup != null)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            Debug.Log("Generated candidate Unity scene fixtures.");
        }

        private static void GenerateScene(string scenePath, bool activeRoot)
        {
            Scene scene = CreateEmptyFixtureScene();
            try
            {
                CreateRoot(scene, "CandidateRootA", activeRoot);
                CreateRoot(scene, "CandidateRootB", false);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new InvalidOperationException("Failed to save generated fixture scene: " + scenePath);
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void GenerateDecoyScene()
        {
            Scene scene = CreateEmptyFixtureScene();
            try
            {
                CreateRoot(scene, "SameBasenameDecoyRoot", false);
                if (!EditorSceneManager.SaveScene(scene, ApplicationCandidateUnitySceneFixture.DecoyScenePath))
                    throw new InvalidOperationException(
                        "Failed to save generated decoy scene: " + ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Scene CreateEmptyFixtureScene()
        {
            Scene active = SceneManager.GetActiveScene();
            NewSceneMode mode = Application.isBatchMode &&
                (!active.IsValid() || string.IsNullOrEmpty(active.path))
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        }

        private static void CreateRoot(Scene scene, string name, bool active)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            var marker = root.AddComponent<ApplicationCandidateUnitySceneMarker>();
            var serializedMarker = new SerializedObject(marker);
            serializedMarker.FindProperty("markerId").stringValue = ApplicationCandidateUnitySceneFixture.MarkerId;
            serializedMarker.FindProperty("revision").longValue = ApplicationCandidateUnitySceneFixture.Revision;
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<ApplicationCandidateUnitySceneFixtureProbe>();
            root.SetActive(active);
        }

        private static void WriteManifest(
            string manifestPath,
            string scenePath,
            bool approved,
            string artifactId = ApplicationCandidateUnitySceneFixture.ArtifactId,
            int expectedRootCount = ApplicationCandidateUnitySceneFixture.ExpectedRootCount)
        {
            var document = new FixtureManifestDocument
            {
                sceneKey = ApplicationCandidateUnitySceneFixture.SceneKey,
                exactScenePath = scenePath,
                artifactId = artifactId,
                markerId = ApplicationCandidateUnitySceneFixture.MarkerId,
                revision = ApplicationCandidateUnitySceneFixture.Revision,
                expectedRootCount = expectedRootCount,
                dependencyHash = AssetDatabase.GetAssetDependencyHash(scenePath).ToString(),
                approved = approved
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(document, true) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static void AddFixtureToBuildSettings()
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            var next = new List<EditorBuildSettingsScene>(current.Length + 2);
            bool candidateFound = false;
            bool decoyFound = false;
            for (int i = 0; i < current.Length; i++)
            {
                EditorBuildSettingsScene entry = current[i];
                if (string.Equals(entry.path, ApplicationCandidateUnitySceneFixture.ScenePath, StringComparison.Ordinal))
                {
                    next.Add(new EditorBuildSettingsScene(entry.path, true));
                    candidateFound = true;
                }
                else if (string.Equals(entry.path, ApplicationCandidateUnitySceneFixture.DecoyScenePath, StringComparison.Ordinal))
                {
                    next.Add(new EditorBuildSettingsScene(entry.path, true));
                    decoyFound = true;
                }
                else next.Add(entry);
            }
            if (!candidateFound) next.Add(new EditorBuildSettingsScene(ApplicationCandidateUnitySceneFixture.ScenePath, true));
            if (!decoyFound) next.Add(new EditorBuildSettingsScene(ApplicationCandidateUnitySceneFixture.DecoyScenePath, true));
            EditorBuildSettings.scenes = next.ToArray();
        }

        private static void EnsureInteractiveScenesAreSafe()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException(
                        "Interactive fixture generation requires every open scene to be saved, named, and clean.");
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string child = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = child;
            }
        }
    }
}
