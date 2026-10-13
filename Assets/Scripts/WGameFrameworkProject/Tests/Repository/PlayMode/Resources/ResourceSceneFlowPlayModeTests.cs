using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MxFramework.Tests.PlayMode.Resources
{
    [PrebuildSetup(typeof(ResourceSceneRetirementArtifactSetup))]
    [PostBuildCleanup(typeof(ResourceSceneRetirementArtifactSetup))]
    public sealed class ResourceSceneRetirementPlayModeTests
    {
        private readonly List<ResourceSceneFlowDriver> _drivers = new List<ResourceSceneFlowDriver>();
        private bool _failUnload;
        private ResourceSceneRetirementFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = JsonUtility.FromJson<ResourceSceneRetirementFixture>(File.ReadAllText(ResourceSceneRetirementArtifactSetup.ReceiptPath));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _failUnload = false;
            foreach (ResourceSceneFlowDriver driver in _drivers) driver.Dispose();
            for (int frame = 0; frame < 600 && _drivers.Exists(driver => !driver.IsRetired); frame++) yield return null;
            Assert.IsFalse(_drivers.Exists(driver => !driver.IsRetired), "Scene retirement still holds ownership: "
                + string.Join("; ", _drivers.ConvertAll(driver => driver.RetirementError.ToString())));
            _drivers.Clear();
        }

        [UnityTest]
        public IEnumerator OwnedDispose_UnloadsSceneBeforeBundlesAndSupportsRebuild()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver);
            int memoryReleases = 0;
            var memory = new MemoryResourceProvider(value => memoryReleases++).RegisterOwned("ordinary", "ordinary");
            services.RegisterOwnedProvider(memory);
            services.ResourceManager.AddCatalog(new ResourceCatalog("ordinary", "", new[]
            {
                new ResourceCatalogEntry("ordinary", ResourceTypeIds.String, "memory", "ordinary")
            }));
            Assert.IsTrue(services.ResourceManager.Load<string>(new ResourceKey("ordinary", ResourceTypeIds.String)).Success);
            yield return Load(services, "scene.a");
            Assert.AreEqual(1, driver.LoadedSceneCount);
            Assert.AreEqual(2, provider.LoadedBundleCount);
            int bundlesAtSceneUnload = -1;
            UnityEngine.Events.UnityAction<Scene> observe = scene =>
            {
                if (string.Equals(scene.path, _fixture.SceneA, StringComparison.OrdinalIgnoreCase)) bundlesAtSceneUnload = provider.LoadedBundleCount;
            };
            SceneManager.sceneUnloaded += observe;
            try
            {
                services.Dispose();
                services.Dispose();
                yield return Retired(driver);
                Assert.AreEqual(2, bundlesAtSceneUnload, "Bundles must remain acquired until native scene unload.");
                Assert.AreEqual(0, provider.LoadedBundleCount);
                Assert.AreEqual(0, services.ResourceManager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, memoryReleases);
                Assert.IsFalse(SceneManager.GetSceneByPath(_fixture.SceneA).isLoaded);
            }
            finally { SceneManager.sceneUnloaded -= observe; }
            GlobalResourceRuntimeServices rebuilt = CreateServices(provider, true, out ResourceSceneFlowDriver next);
            yield return Load(rebuilt, "scene.a");
            rebuilt.Dispose();
            yield return Retired(next);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator ExplicitUnloadThenDispose_ReleasesExactlyOnce()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver);
            yield return Load(services, "scene.a");
            Assert.IsTrue(services.SceneFlowController.RequestUnload("scene.a").Success);
            yield return Idle(services.SceneFlowController);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            services.Dispose(); services.Dispose();
            yield return Retired(driver);
            Assert.AreEqual(0, driver.LoadedSceneCount);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator DisposeDuringLoad_DrainsLateSuccessWithoutControllerTicks()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver);
            Assert.IsTrue(services.SceneFlowController.RequestLoad(new SceneFlowRequest("scene.a", SceneFlowLoadMode.Additive)).Success);
            services.Dispose(); services.Dispose();
            yield return Retired(driver);
            Assert.AreEqual(0, provider.LoadedBundleCount);
            Assert.IsFalse(SceneManager.GetSceneByPath(_fixture.SceneA).isLoaded);
            Assert.IsFalse(driver.LoadScene(new SceneFlowRequest("scene.a")).Success);
        }

        [UnityTest]
        public IEnumerator DisposeDuringUnload_DrainsExistingOperationWithoutDoubleRelease()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver);
            yield return Load(services, "scene.a");
            Assert.IsTrue(services.SceneFlowController.RequestUnload("scene.a").Success);
            services.Dispose(); services.Dispose();
            yield return Retired(driver);
            services.SceneFlowController.Tick();
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, services.SceneFlowController.LastResult.Status);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator SharedDependencyAndLiveSession_RemainLoadedUntilTheirOwnRetirement()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices first = CreateServices(provider, true, out ResourceSceneFlowDriver firstDriver);
            GlobalResourceRuntimeServices second = CreateServices(provider, true, out ResourceSceneFlowDriver secondDriver);
            yield return Load(first, "scene.a");
            yield return Load(second, "scene.b");
            Assert.AreEqual(3, provider.LoadedBundleCount);
            first.Dispose();
            yield return Retired(firstDriver);
            Assert.AreEqual(2, provider.LoadedBundleCount);
            Scene live = SceneManager.GetSceneByPath(_fixture.SceneB);
            Assert.IsTrue(live.isLoaded);
            Assert.NotNull(live.GetRootGameObjects()[0].GetComponent<MeshRenderer>().sharedMaterial);
            Assert.AreEqual(1, secondDriver.LoadedSceneCount);
            second.Dispose();
            yield return Retired(secondDriver);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator ExternallyInjectedDriver_IsNotRetiredByServices()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices services = CreateServices(provider, false, out ResourceSceneFlowDriver driver);
            yield return Load(services, "scene.a");
            services.Dispose(); services.Dispose();
            Assert.IsFalse(driver.IsRetiring);
            Assert.AreEqual(1, driver.LoadedSceneCount);
            Assert.AreEqual(2, provider.LoadedBundleCount);
            Assert.IsTrue(SceneManager.GetSceneByPath(_fixture.SceneA).isLoaded);
            driver.Dispose();
            yield return Retired(driver);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator FailedUnload_RetainsDebtAndRepeatedDisposeRetries()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            _failUnload = true;
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver,
                scene => { if (_failUnload) throw new InvalidOperationException("fixture unload failure"); return SceneManager.UnloadSceneAsync(scene); });
            yield return Load(services, "scene.a");
            services.Dispose();
            Assert.IsFalse(driver.IsRetired);
            Assert.AreEqual(SceneFlowErrorCode.DriverFailure, driver.RetirementError.Code);
            Assert.AreEqual(1, driver.LoadedSceneCount);
            Assert.AreEqual(2, provider.LoadedBundleCount);
            Assert.IsTrue(SceneManager.GetSceneByPath(_fixture.SceneA).isLoaded);
            _failUnload = false;
            services.Dispose();
            yield return Retired(driver);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator FailedLoad_DoesNotLeaveSceneOrBundleOwnership()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver);
            Assert.IsTrue(services.SceneFlowController.RequestLoad(new SceneFlowRequest("scene.missing", SceneFlowLoadMode.Additive)).Success);
            yield return Idle(services.SceneFlowController);
            Assert.AreEqual(SceneFlowResultStatus.Failed, services.SceneFlowController.LastResult.Status);
            services.Dispose();
            yield return Retired(driver);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator NullUnloadOperation_RetainsSceneAndBundleDebt()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            _failUnload = true;
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver,
                scene => _failUnload ? null : SceneManager.UnloadSceneAsync(scene));
            yield return Load(services, "scene.a");
            services.Dispose();
            Assert.IsFalse(services.SceneRetirementComplete);
            Assert.AreEqual(SceneFlowErrorCode.DriverFailure, services.SceneRetirementError.Code);
            Assert.AreEqual(2, provider.LoadedBundleCount);
            Assert.IsTrue(SceneManager.GetSceneByPath(_fixture.SceneA).isLoaded);
            _failUnload = false;
            services.Dispose();
            yield return Retired(driver);
            Assert.IsTrue(services.SceneRetirementComplete);
            Assert.IsTrue(services.SceneRetirementError.IsNone);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        [UnityTest]
        public IEnumerator CompletedOperationWithLoadedScene_RetainsDebtAndReportsFailure()
        {
            SceneAssetBundleProvider provider = CreateProvider();
            _failUnload = true;
            GlobalResourceRuntimeServices services = CreateServices(provider, true, out ResourceSceneFlowDriver driver,
                scene => _failUnload ? UnityEngine.Resources.UnloadUnusedAssets() : SceneManager.UnloadSceneAsync(scene));
            yield return Load(services, "scene.a");
            services.Dispose();
            for (int frame = 0; frame < 600 && services.SceneRetirementError.IsNone; frame++) yield return null;
            Assert.AreEqual(SceneFlowErrorCode.DriverFailure, services.SceneRetirementError.Code);
            Assert.IsFalse(services.SceneRetirementComplete);
            Assert.AreEqual(1, driver.LoadedSceneCount);
            Assert.AreEqual(2, provider.LoadedBundleCount);
            Assert.IsTrue(SceneManager.GetSceneByPath(_fixture.SceneA).isLoaded);
            _failUnload = false;
            services.Dispose();
            yield return Retired(driver);
            Assert.AreEqual(0, provider.LoadedBundleCount);
        }

        private SceneAssetBundleProvider CreateProvider()
        {
            return new SceneAssetBundleProvider(_fixture.BundleRoot, new DictionaryAssetBundleDependencyProvider()
                .Register("scene765-a", "scene765-shared").Register("scene765-b", "scene765-shared"));
        }

        private GlobalResourceRuntimeServices CreateServices(SceneAssetBundleProvider provider, bool owned,
            out ResourceSceneFlowDriver driver, Func<Scene, AsyncOperation> unload = null)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("scene-retirement", "", new[]
            {
                new ResourceCatalogEntry("scene.a", ResourceTypeIds.Scene, SceneAssetBundleProvider.Id, "scene765-a|" + _fixture.SceneA),
                new ResourceCatalogEntry("scene.b", ResourceTypeIds.Scene, SceneAssetBundleProvider.Id, "scene765-b|" + _fixture.SceneB),
                new ResourceCatalogEntry("scene.missing", ResourceTypeIds.Scene, SceneAssetBundleProvider.Id, "scene765-a|Assets/Scenes/missing765.unity")
            }));
            driver = unload != null ? new ResourceSceneFlowDriver(manager, provider, unload) : new ResourceSceneFlowDriver(manager, provider);
            _drivers.Add(driver);
            return owned
                ? new GlobalResourceRuntimeServices(GlobalResourceRuntimeMode.Offline, manager, new ResourcePreloadService(manager),
                    ownsSceneFlowDriver: true, sceneFlowDriver: driver)
                : new GlobalResourceRuntimeServices(GlobalResourceRuntimeMode.Offline, manager, new ResourcePreloadService(manager), sceneFlowDriver: driver);
        }

        private static IEnumerator Load(GlobalResourceRuntimeServices services, string key)
        {
            Assert.IsTrue(services.SceneFlowController.RequestLoad(new SceneFlowRequest(key, SceneFlowLoadMode.Additive)).Success);
            yield return Idle(services.SceneFlowController);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, services.SceneFlowController.LastResult.Status,
                services.SceneFlowController.LastResult.Error.ToString());
        }

        private static IEnumerator Idle(SceneFlowController controller)
        {
            for (int frame = 0; frame < 600 && controller.IsBusy; frame++) { controller.Tick(); yield return null; }
            controller.Tick(); Assert.IsFalse(controller.IsBusy);
        }

        private static IEnumerator Retired(ResourceSceneFlowDriver driver)
        {
            for (int frame = 0; frame < 600 && !driver.IsRetired; frame++) yield return null;
            Assert.IsTrue(driver.IsRetired, driver.RetirementError.ToString());
            Assert.AreEqual(0, driver.LoadedSceneCount);
        }
    }

    [Serializable]
    public sealed class ResourceSceneRetirementFixture
    {
        public string AssetRoot;
        public string BundleRoot;
        public string SceneA;
        public string SceneB;
    }

    public sealed class ResourceSceneRetirementArtifactSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public const string ReceiptPath = "Temp/MxFramework/Tests/ResourceSceneRetirement/fixture.json";

        public void Setup()
        {
#if UNITY_EDITOR
            if (File.Exists(ReceiptPath)) return;
            var fixture = new ResourceSceneRetirementFixture
            {
                AssetRoot = "Assets/TestAssets/MxFramework/SceneRetirement765_" + Guid.NewGuid().ToString("N"),
                BundleRoot = Path.GetFullPath(Path.GetDirectoryName(ReceiptPath) + "/bundles")
            };
            AssetDatabase.CreateFolder("Assets/TestAssets/MxFramework", Path.GetFileName(fixture.AssetRoot));
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            string materialPath = fixture.AssetRoot + "/shared.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            Scene original = SceneManager.GetActiveScene();
            for (int index = 0; index < 2; index++)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.GetComponent<MeshRenderer>().sharedMaterial = material;
                string path = fixture.AssetRoot + (index == 0 ? "/A.unity" : "/B.unity");
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true);
                if (index == 0) fixture.SceneA = path; else fixture.SceneB = path;
            }
            SceneManager.SetActiveScene(original);
            AssetDatabase.SaveAssetIfDirty(material);
            Directory.CreateDirectory(fixture.BundleRoot);
            File.WriteAllText(ReceiptPath, JsonUtility.ToJson(fixture));
            AssetBundleManifest manifest;
            try
            {
                manifest = BuildPipeline.BuildAssetBundles(new BuildAssetBundlesParameters
                {
                    outputPath = fixture.BundleRoot,
                    bundleDefinitions = new[]
                    {
                        new AssetBundleBuild { assetBundleName = "scene765-a", assetNames = new[] { fixture.SceneA } },
                        new AssetBundleBuild { assetBundleName = "scene765-b", assetNames = new[] { fixture.SceneB } },
                        new AssetBundleBuild { assetBundleName = "scene765-shared", assetNames = new[] { materialPath } }
                    },
                    options = BuildAssetBundleOptions.UncompressedAssetBundle,
                    targetPlatform = EditorUserBuildSettings.activeBuildTarget,
                    // Unity 6000.6's NativeArray.ReadOnly API requires the safety-enabled Collections overload.
                    extraScriptingDefines = new[] { "ENABLE_UNITY_COLLECTIONS_CHECKS" }
                });
                if (manifest == null) throw new InvalidOperationException("Scene retirement fixture bundle build failed.");
                CollectionAssert.Contains(manifest.GetAllDependencies("scene765-a"), "scene765-shared");
                CollectionAssert.Contains(manifest.GetAllDependencies("scene765-b"), "scene765-shared");
            }
            catch { Cleanup(); throw; }
#else
            throw new NotSupportedException("Fixture bundles must be generated in the Unity Editor.");
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            if (!File.Exists(ReceiptPath)) return;
            ResourceSceneRetirementFixture fixture = JsonUtility.FromJson<ResourceSceneRetirementFixture>(File.ReadAllText(ReceiptPath));
            if (SceneManager.GetSceneByPath(fixture.SceneA).isLoaded || SceneManager.GetSceneByPath(fixture.SceneB).isLoaded)
                throw new InvalidOperationException("Fixture scenes are still loaded; cleanup cannot remove their assets.");
            AssetDatabase.DeleteAsset(fixture.AssetRoot);
            Directory.Delete(Path.GetDirectoryName(ReceiptPath), true);
#endif
        }
    }

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
