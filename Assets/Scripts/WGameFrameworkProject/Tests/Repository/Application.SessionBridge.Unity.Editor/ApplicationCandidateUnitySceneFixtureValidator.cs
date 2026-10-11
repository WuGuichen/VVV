using System;
using System.IO;
using MxFramework.Tests.PlayMode.Application;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.ApplicationStartup.SessionBridge.Unity.Editor
{
    /// <summary>Validates generated fixture evidence without entering Play Mode.</summary>
    public static class ApplicationCandidateUnitySceneFixtureValidator
    {
        [Serializable]
        private sealed class FixtureManifestDocument
        {
            public string schema;
            public string sceneKey;
            public string exactScenePath;
            public string artifactId;
            public string markerId;
            public long revision;
            public int expectedRootCount;
            public string dependencyHash;
            public bool approved;
        }

        public static void ValidateOrThrow()
        {
            ValidateManifest(ApplicationCandidateUnitySceneFixture.ManifestPath,
                ApplicationCandidateUnitySceneFixture.ScenePath, true,
                ApplicationCandidateUnitySceneFixture.ArtifactId,
                ApplicationCandidateUnitySceneFixture.ExpectedRootCount);
            ValidateManifest(ApplicationCandidateUnitySceneFixture.UnapprovedManifestPath,
                ApplicationCandidateUnitySceneFixture.UnapprovedActiveRootScenePath, false,
                ApplicationCandidateUnitySceneFixture.ArtifactId,
                ApplicationCandidateUnitySceneFixture.ExpectedRootCount);
            ValidateManifest(ApplicationCandidateUnitySceneFixture.DecoyManifestPath,
                ApplicationCandidateUnitySceneFixture.DecoyScenePath, true,
                ApplicationCandidateUnitySceneFixture.DecoyArtifactId, 1);
            ValidateScene(ApplicationCandidateUnitySceneFixture.ScenePath, false);
            ValidateScene(ApplicationCandidateUnitySceneFixture.UnapprovedActiveRootScenePath, true);
            ValidateDecoyScene();
        }

        private static void ValidateManifest(
            string manifestPath,
            string scenePath,
            bool approved,
            string artifactId,
            int expectedRootCount)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Fixture manifest is missing.", manifestPath);
            FixtureManifestDocument document = JsonUtility.FromJson<FixtureManifestDocument>(File.ReadAllText(manifestPath));
            if (document == null || document.schema != ApplicationCandidateUnitySceneFixture.Schema ||
                document.sceneKey != ApplicationCandidateUnitySceneFixture.SceneKey ||
                document.exactScenePath != scenePath ||
                document.artifactId != artifactId ||
                document.markerId != ApplicationCandidateUnitySceneFixture.MarkerId ||
                document.revision != ApplicationCandidateUnitySceneFixture.Revision ||
                document.expectedRootCount != expectedRootCount ||
                document.approved != approved ||
                document.dependencyHash != AssetDatabase.GetAssetDependencyHash(scenePath).ToString())
                throw new InvalidDataException("Fixture manifest does not match its exact generated scene evidence: " + manifestPath);
        }

        private static void ValidateScene(string scenePath, bool expectActiveRoot)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                if (roots.Length != ApplicationCandidateUnitySceneFixture.ExpectedRootCount)
                    throw new InvalidDataException("Fixture root count mismatch: " + scenePath);
                bool activeObserved = false;
                for (int i = 0; i < roots.Length; i++)
                {
                    activeObserved |= roots[i].activeSelf;
                    var markers = roots[i].GetComponents<ApplicationCandidateUnitySceneMarker>();
                    if (markers.Length != 1 || markers[0].MarkerId != ApplicationCandidateUnitySceneFixture.MarkerId ||
                        markers[0].Revision != ApplicationCandidateUnitySceneFixture.Revision)
                        throw new InvalidDataException("Fixture marker mismatch: " + scenePath);
                }
                if (activeObserved != expectActiveRoot)
                    throw new InvalidDataException("Fixture active-root evidence mismatch: " + scenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateDecoyScene()
        {
            string candidateName = Path.GetFileName(ApplicationCandidateUnitySceneFixture.ScenePath);
            string decoyName = Path.GetFileName(ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            if (!string.Equals(candidateName, decoyName, StringComparison.Ordinal) ||
                string.Equals(ApplicationCandidateUnitySceneFixture.ScenePath,
                    ApplicationCandidateUnitySceneFixture.DecoyScenePath, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Decoy fixture must use the same basename at a different exact path.");

            Scene scene = EditorSceneManager.OpenScene(
                ApplicationCandidateUnitySceneFixture.DecoyScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                if (roots.Length != 1 || roots[0].activeSelf)
                    throw new InvalidDataException("Decoy fixture root count mismatch.");
                ApplicationCandidateUnitySceneMarker[] markers =
                    roots[0].GetComponents<ApplicationCandidateUnitySceneMarker>();
                if (markers.Length != 1 ||
                    !string.Equals(markers[0].MarkerId,
                        ApplicationCandidateUnitySceneFixture.MarkerId, StringComparison.Ordinal) ||
                    markers[0].Revision != ApplicationCandidateUnitySceneFixture.Revision)
                    throw new InvalidDataException("Decoy fixture marker mismatch.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
