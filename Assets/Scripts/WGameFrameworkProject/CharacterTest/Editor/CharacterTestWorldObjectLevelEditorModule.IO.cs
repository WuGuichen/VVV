using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private static CharacterTestWorldObjectSceneSource FindSceneSource()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            CharacterTestWorldObjectSceneSource[] sources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            if (sources == null || sources.Length == 0)
                return null;

            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null
                    && sources[i].gameObject.scene.handle == activeScene.handle
                    && sources[i].Enabled)
                    return sources[i];
            }

            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null
                    && sources[i].gameObject.scene.handle == activeScene.handle)
                    return sources[i];
            }

            return null;
        }

        private static SceneCacheTarget FindSceneCacheTarget(bool allowLegacySource = true)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            if (managers != null && managers.Length > 0)
            {
                for (int i = 0; i < managers.Length; i++)
                {
                    if (managers[i] != null
                        && managers[i].gameObject.scene.handle == activeScene.handle)
                        return SceneCacheTarget.FromGameManager(managers[i]);
                }
            }

            if (!allowLegacySource)
                return null;

            CharacterTestWorldObjectSceneSource source = FindSceneSource();
            return source == null || source.gameObject.scene.handle != activeScene.handle
                ? null
                : SceneCacheTarget.FromLegacySource(source);
        }

        private sealed class SceneCacheTarget
        {
            private SceneCacheTarget(
                GameManager gameManager,
                CharacterTestWorldObjectSceneSource legacySource,
                Transform root,
                CharacterTestWorldObjectSceneCache cache,
                Object dirtyObject,
                string displayName,
                bool isGameManager)
            {
                GameManager = gameManager;
                LegacySource = legacySource;
                Root = root;
                Cache = cache;
                DirtyObject = dirtyObject;
                DisplayName = displayName ?? string.Empty;
                IsGameManager = isGameManager;
            }

            public GameManager GameManager { get; }

            public CharacterTestWorldObjectSceneSource LegacySource { get; }

            public Transform Root { get; }

            public CharacterTestWorldObjectSceneCache Cache { get; }

            public Object DirtyObject { get; }

            public string DisplayName { get; }

            public bool IsGameManager { get; }

            public GameObject GameObject => GameManager != null
                ? GameManager.gameObject
                : Root == null
                    ? null
                    : Root.gameObject;

            public static SceneCacheTarget FromGameManager(GameManager gameManager)
            {
                if (gameManager == null)
                    return null;

                Transform root = gameManager.WorldObjectSceneRoot;
                return new SceneCacheTarget(
                    gameManager,
                    null,
                    root,
                    gameManager.WorldObjectSceneCache,
                    gameManager,
                    gameManager.name + " GameManager Scene Cache",
                    true);
            }

            public static SceneCacheTarget FromLegacySource(CharacterTestWorldObjectSceneSource source)
            {
                if (source == null)
                    return null;

                return new SceneCacheTarget(
                    null,
                    source,
                    source.transform,
                    null,
                    source,
                    source.name + " legacy scene source",
                    false);
            }
        }

        private static void WriteDocument(
            string assetPath,
            LevelWorldObjectAuthoringDocument document)
        {
            string fullPath = ToFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, LevelWorldObjectAuthoringJson.SaveToJson(document));
        }

        private static void WriteEnvelope(
            string assetPath,
            MxLevelAuthoringDocument envelope)
        {
            string fullPath = ToFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, MxLevelAuthoringJson.SaveToJson(envelope));
        }

        private static string ToFullPath(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return normalized;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        }

    }
}
