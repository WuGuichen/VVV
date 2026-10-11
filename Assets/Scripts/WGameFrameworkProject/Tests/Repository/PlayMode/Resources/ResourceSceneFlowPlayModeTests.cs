using System;
using System.Collections;
using System.IO;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MxFramework.Tests.PlayMode.Resources
{
    public class ResourceSceneFlowPlayModeTests
    {
        private const string SceneResourceKey = "scene.character_test";
        private const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";

        [UnityTest]
        [PrebuildSetup(typeof(GlobalResourcePlayModeArtifactSetup))]
        [PostBuildCleanup(typeof(GlobalResourcePlayModeArtifactSetup))]
        public IEnumerator OfflineSceneFlow_LoadsSceneFromResourceCatalogBundle()
        {
            var options = new GlobalResourceRuntimeOptions
            {
                Mode = GlobalResourceRuntimeMode.Offline,
                BuildTargetName = GetBuildTargetName(),
                AllowMissingPreloadGroupInEditor = false
            };

            using (GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options))
            {
                Assert.IsFalse(services.HasBootstrapError, services.BootstrapErrorMessage);
                Assert.NotNull(services.SceneFlowController);

                SceneFlowController controller = services.SceneFlowController;
                SceneFlowResult load = controller.RequestLoad(new SceneFlowRequest(SceneResourceKey, SceneFlowLoadMode.Additive));
                Assert.AreEqual(SceneFlowResultStatus.Accepted, load.Status);

                yield return TickUntilIdle(controller);

                Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
                Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                Assert.IsTrue(loaded.IsValid());
                Assert.IsTrue(loaded.isLoaded);
                Assert.AreEqual(-1, loaded.buildIndex);

                SceneFlowResult unload = controller.RequestUnload(SceneResourceKey);
                Assert.AreEqual(SceneFlowResultStatus.Accepted, unload.Status);

                yield return TickUntilIdle(controller);

                Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
                Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isLoaded);
            }
        }

        [UnityTest]
        public IEnumerator EditorSceneFlow_LoadsSceneFromProjectAsset()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Editor scene flow is only available in Unity Editor Play Mode.");
            yield break;
#else
            Assert.NotNull(GlobalResourceRuntimeServices.EditorRuntimeFactory, "Editor runtime factory was not registered.");
            var options = new GlobalResourceRuntimeOptions
            {
                Mode = GlobalResourceRuntimeMode.Editor,
                AllowMissingPreloadGroupInEditor = false
            };

            using (GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options))
            {
                Assert.AreEqual(GlobalResourceRuntimeMode.Editor, services.Mode);
                Assert.IsFalse(services.HasBootstrapError, services.BootstrapErrorMessage);
                Assert.NotNull(services.SceneFlowController);

                SceneFlowController controller = services.SceneFlowController;
                SceneFlowResult load = controller.RequestLoad(new SceneFlowRequest(SceneResourceKey, SceneFlowLoadMode.Additive));
                Assert.AreEqual(SceneFlowResultStatus.Accepted, load.Status);

                yield return TickUntilIdle(controller);

                Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
                Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                Assert.IsTrue(loaded.IsValid());
                Assert.IsTrue(loaded.isLoaded);
                Assert.AreEqual(-1, loaded.buildIndex);

                SceneFlowResult unload = controller.RequestUnload(SceneResourceKey);
                Assert.AreEqual(SceneFlowResultStatus.Accepted, unload.Status);

                yield return TickUntilIdle(controller);

                Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
                Assert.IsFalse(SceneManager.GetSceneByPath(ScenePath).isLoaded);
            }
#endif
        }

        private static IEnumerator TickUntilIdle(SceneFlowController controller)
        {
            int frame = 0;
            while (controller.IsBusy && frame < 600)
            {
                controller.Tick();
                frame++;
                yield return null;
            }

            controller.Tick();
            Assert.IsFalse(controller.IsBusy, "SceneFlowController did not finish within the frame budget.");
        }

        private static string GetBuildTargetName()
        {
#if UNITY_STANDALONE_OSX
            return "StandaloneOSX";
#elif UNITY_STANDALONE_WIN
            return "StandaloneWindows64";
#elif UNITY_STANDALONE_LINUX
            return "StandaloneLinux64";
#else
            return string.Empty;
#endif
        }
    }

    /// <summary>Builds machine-local Offline artifacts for the test and restores the prior local state.</summary>
    public sealed class GlobalResourcePlayModeArtifactSetup : IPrebuildSetup, IPostBuildCleanup
    {
#if UNITY_EDITOR
        private const string BuildMenuPath = "MxFramework/Resources/Build Global Player Resources";
        private const string BackupSessionKey = "MxFramework.ResourceSceneFlowPlayMode.BackupRoot";
        private const string ResourceArtifactsPath = "Assets/StreamingAssets/MxFramework/Resources";
        private const string BuildReportsPath = "Assets/Config/MxFramework/ResourceBuildReports";
        private const string ArtifactSetPath = ResourceArtifactsPath + "/global_resource_artifact_set.json";
#endif

        public void Setup()
        {
#if UNITY_EDITOR
            RestoreBackup();

            string backupRoot = Path.GetFullPath(Path.Combine(
                "Temp",
                "MxFramework",
                "Tests",
                "ResourceSceneFlow",
                Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(backupRoot);
            BackupPath(ResourceArtifactsPath, Path.Combine(backupRoot, "Resources"));
            BackupPath(BuildReportsPath, Path.Combine(backupRoot, "ResourceBuildReports"));
            SessionState.SetString(BackupSessionKey, backupRoot);

            try
            {
                if (!EditorApplication.ExecuteMenuItem(BuildMenuPath))
                    throw new InvalidOperationException("Global Player resource build menu is unavailable: " + BuildMenuPath);
                if (!File.Exists(Path.GetFullPath(ArtifactSetPath)))
                    throw new InvalidOperationException("Global Player resource build did not publish " + ArtifactSetPath + ".");
            }
            catch
            {
                RestoreBackup();
                throw;
            }
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            RestoreBackup();
#endif
        }

#if UNITY_EDITOR
        private static void BackupPath(string sourcePath, string backupPath)
        {
            if (Directory.Exists(sourcePath))
                FileUtil.CopyFileOrDirectory(sourcePath, backupPath);
            if (File.Exists(sourcePath + ".meta"))
                FileUtil.CopyFileOrDirectory(sourcePath + ".meta", backupPath + ".meta");
        }

        private static void RestoreBackup()
        {
            string backupRoot = SessionState.GetString(BackupSessionKey, string.Empty);
            if (string.IsNullOrEmpty(backupRoot))
                return;

            RestorePath(Path.Combine(backupRoot, "Resources"), ResourceArtifactsPath);
            RestorePath(Path.Combine(backupRoot, "ResourceBuildReports"), BuildReportsPath);
            FileUtil.DeleteFileOrDirectory(backupRoot);
            SessionState.EraseString(BackupSessionKey);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RestorePath(string backupPath, string targetPath)
        {
            FileUtil.DeleteFileOrDirectory(targetPath);
            FileUtil.DeleteFileOrDirectory(targetPath + ".meta");
            if (Directory.Exists(backupPath))
                FileUtil.CopyFileOrDirectory(backupPath, targetPath);
            if (File.Exists(backupPath + ".meta"))
                FileUtil.CopyFileOrDirectory(backupPath + ".meta", targetPath + ".meta");
        }
#endif
    }
}
