using System;
using System.Collections;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Resources
{
    public sealed class BasicGrassResourceLifecyclePlayModeTests
    {
        private static readonly ResourceKey GrassKey =
            new ResourceKey("environment.grass.basic_clump", ResourceTypeIds.GameObject, packageId: "wgame");

        [UnityTest]
        public IEnumerator EditorResource_LoadsSharedPrefabAndReleasesAfterOwningSceneUnloads()
        {
#if UNITY_EDITOR
            return VerifyLifecycle(GlobalResourceRuntimeMode.Editor);
#else
            Assert.Ignore("Editor provider validation requires the Editor.");
            return Empty();
#endif
        }

        [UnityTest]
        [PrebuildSetup(typeof(GlobalResourcePlayModeArtifactSetup))]
        [PostBuildCleanup(typeof(GlobalResourcePlayModeArtifactSetup))]
        public IEnumerator OfflineResource_LoadsSharedPrefabAndReleasesAfterOwningSceneUnloads()
        {
            return VerifyLifecycle(GlobalResourceRuntimeMode.Offline);
        }

        private static IEnumerator VerifyLifecycle(GlobalResourceRuntimeMode mode)
        {
            using (var services = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions { Mode = mode }))
            {
                Assert.IsFalse(services.HasBootstrapError, services.BootstrapErrorMessage);
                ResourceManager manager = services.ResourceManager;
                manager.SetRetainPolicy(ResourceRetainPolicy.None);
                ResourceDebugSnapshot baseline = manager.CreateDebugSnapshot();
                Scene scene = default;
                ResourceHandle<GameObject> first = null;
                ResourceHandle<GameObject> second = null;
                try
                {
                    var loaded = manager.Load<GameObject>(GrassKey);
                    Assert.IsTrue(loaded.Success, loaded.Error.Message);
                    first = loaded.Value;
                    var again = manager.Load<GameObject>(GrassKey);
                    Assert.IsTrue(again.Success, again.Error.Message);
                    second = again.Value;
                    Assert.AreSame(first.Value, second.Value);
                    Assert.AreEqual(baseline.TotalRefCount + 2, manager.CreateDebugSnapshot().TotalRefCount);
                    scene = SceneManager.CreateScene("BasicGrassLifecycle_" + Guid.NewGuid().ToString("N"));
                    GameObject instance = UnityEngine.Object.Instantiate(first.Value);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    Assert.NotNull(instance.GetComponent<MeshRenderer>().sharedMaterial);
                    Assert.IsEmpty(instance.GetComponentsInChildren<Collider>(true));
                    yield return null;
                    Assert.IsTrue(instance.GetComponent<MeshRenderer>().enabled);
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                    Assert.NotNull(unload);
                    yield return unload;
                    Assert.IsTrue(instance == null, "The scene-owned grass instance survived scene unload.");
                    Assert.IsFalse(scene.isLoaded);
                    manager.Release(first);
                    first = null;
                    Assert.AreEqual(baseline.TotalRefCount + 1, manager.CreateDebugSnapshot().TotalRefCount);
                    manager.Release(second);
                    second = null;
                    ResourceDebugSnapshot released = manager.CreateDebugSnapshot();
                    Assert.AreEqual(baseline.TotalRefCount, released.TotalRefCount);
                    Assert.AreEqual(baseline.LoadedCount, released.LoadedCount);
                    Assert.AreEqual(baseline.BundleCount, released.BundleCount);
                    Assert.AreEqual(baseline.RecentErrors.Count, released.RecentErrors.Count);
                }
                finally
                {
                    if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                    if (first != null) manager.Release(first);
                    if (second != null) manager.Release(second);
                }
            }
        }

#if !UNITY_EDITOR
        private static IEnumerator Empty() { yield break; }
#endif
    }
}
