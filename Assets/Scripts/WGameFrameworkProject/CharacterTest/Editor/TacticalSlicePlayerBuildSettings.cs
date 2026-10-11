using System;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class TacticalSlicePlayerBuildSettings
    {
        internal const string ScenePath = "Assets/Scenes/TacticalCardDemoScene.unity";
        private const string LevelPath = "Assets/StreamingAssets/MxFramework/CharacterTest/tactical_slice.level.json";

        [MenuItem("MxFramework/Character Test/Tactical Slice/Configure Player Entry")]
        internal static void ConfigurePlayerEntry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("先在安全窗口结束 Play 并等待 Editor ready；本命令不会停止 Play 或保存场景。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存内容，请先由场景负责人处理；本命令不会自动保存。");
            GameManager manager = UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value.gameObject.scene.path == ScenePath);
            if (manager == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("请在安全窗口打开已发布的战术切片场景后配置玩家入口。");
            string source = manager.CharacterSceneCache.AuthoringJsonPath;
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) throw new InvalidOperationException("切片发布关卡源缺失。");
            string json = File.ReadAllText(source);
            MxLevelAuthoringDocument document = MxLevelAuthoringJson.LoadFromJson(json);
            if (MxLevelAuthoringJson.ComputeContentHash(document) != manager.CharacterSceneCache.AuthoringContentHash)
                throw new InvalidOperationException("场景缓存与关卡发布内容不一致，请先由关卡生成器完成发布。");
            Directory.CreateDirectory(Path.GetDirectoryName(LevelPath));
            File.WriteAllText(LevelPath, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(LevelPath, ImportAssetOptions.ForceSynchronousImport);
            PersistEntrySceneList();
            Debug.Log("战术切片已设为玩家启动场景，并发布运行时关卡快照。继续只恢复战斗外进度。");
        }

        // EditorBuildSettings.scenes can target an active BuildProfile and does not itself prove
        // ProjectSettings is on disk. Use Unity's own serialized singleton, never hand-write YAML.
        internal static void PersistEntrySceneList()
        {
            var owner = Unsupported.GetSerializedAssetInterfaceSingleton("EditorBuildSettings");
            if (owner == null) throw new InvalidOperationException("Unity build settings owner is unavailable.");
            Undo.RecordObject(owner, "Configure Tactical Slice Player Entry");
            var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.globalScenes.Where(scene => scene.path != ScenePath)
                    .Select(scene => new EditorBuildSettingsScene(scene.path, false))).ToArray();
            EditorBuildSettings.globalScenes = scenes;
            EditorBuildSettings.scenes = scenes;
            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if (profile != null && profile.overrideGlobalScenes)
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            EditorUtility.SetDirty(owner);
            InternalEditorUtility.SaveToSerializedFileAndForget(new[] { owner }, "ProjectSettings/EditorBuildSettings.asset", true);
            VerifyPlayerEntry();
        }

        internal static void VerifyPlayerEntry()
        {
            if (!EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.globalScenes).SequenceEqual(new[] { ScenePath })
                || !EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes).SequenceEqual(new[] { ScenePath }))
                throw new InvalidOperationException("Tactical slice is not the exclusive enabled player entry.");
            string disk = File.ReadAllText("ProjectSettings/EditorBuildSettings.asset").Replace("\r\n", "\n");
            if (!disk.Contains("m_Scenes:\n  - enabled: 1\n    path: " + ScenePath + "\n"))
                throw new InvalidOperationException("Player entry did not persist to ProjectSettings; configuration is incomplete.");
        }
    }
}
