using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MxFramework.Tests.PlayMode.Application
{
    /// <summary>Temporarily exposes committed test scenes to the PlayMode test build only.</summary>
    public sealed class ApplicationUnityBootValidationPlayModeBuildSettings :
        IPrebuildSetup,
        IPostBuildCleanup
    {
#if UNITY_EDITOR
        private const string SessionKey =
            "MxFramework.ApplicationUnityBootValidation.OriginalBuildScenes";

        [Serializable]
        private sealed class SceneList
        {
            public SceneEntry[] scenes;
        }

        [Serializable]
        private struct SceneEntry
        {
            public string path;
            public bool enabled;
        }
#endif

        public void Setup()
        {
#if UNITY_EDITOR
            EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            var snapshot = new SceneList { scenes = new SceneEntry[original.Length] };
            for (int i = 0; i < original.Length; i++)
            {
                snapshot.scenes[i].path = original[i].path;
                snapshot.scenes[i].enabled = original[i].enabled;
            }
            SessionState.SetString(SessionKey, JsonUtility.ToJson(snapshot));

            var scenes = new List<EditorBuildSettingsScene>(original);
            AddOrEnable(scenes, ApplicationUnityBootValidationPlayModeTests.ScenePath);
            AddOrEnable(scenes, ApplicationCandidateUnitySceneFixture.ScenePath);
            AddOrEnable(scenes, ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            string json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            SceneList snapshot = JsonUtility.FromJson<SceneList>(json);
            var restored = new EditorBuildSettingsScene[snapshot.scenes.Length];
            for (int i = 0; i < snapshot.scenes.Length; i++)
                restored[i] = new EditorBuildSettingsScene(
                    snapshot.scenes[i].path,
                    snapshot.scenes[i].enabled);
            EditorBuildSettings.scenes = restored;
            SessionState.EraseString(SessionKey);
#endif
        }

#if UNITY_EDITOR
        private static void AddOrEnable(List<EditorBuildSettingsScene> scenes, string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (!string.Equals(scenes[i].path, path, StringComparison.Ordinal)) continue;
                scenes[i] = new EditorBuildSettingsScene(path, true);
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
#endif
    }
}
