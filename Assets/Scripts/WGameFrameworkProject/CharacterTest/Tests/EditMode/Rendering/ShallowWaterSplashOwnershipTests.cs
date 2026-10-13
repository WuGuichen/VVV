using System;
using System.IO;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Rendering
{
    public sealed class ShallowWaterSplashOwnershipTests
    {
        private static GameObject PreparedPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestShallowWaterSplashAssetBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "Prepare the formal water-splash resource before native ownership regression tests.");
            return prefab;
        }

        [Test] public void MissingPrefabDoesNotSaveAnExistingDirtyMaterial()
        {
            string folderName = "__MxWaterSplashOwnership_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);
            try
            {
                string materialPath = folder + "/Existing.mat", prefabPath = folder + "/Missing.prefab";
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterDroplets.shader");
                Assert.IsNotNull(shader);
                var material = new Material(shader) { name = "Existing" };
                AssetDatabase.CreateAsset(material, materialPath);
                string absoluteMaterialPath = Path.Combine(Application.dataPath, materialPath.Substring("Assets/".Length));
                string saved = File.ReadAllText(absoluteMaterialPath);
                material.renderQueue = 3123; EditorUtility.SetDirty(material);
                Assert.IsTrue(EditorUtility.IsDirty(material));
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));

                var prefab = CharacterTestShallowWaterSplashAssetBuilder.EnsurePrefab(prefabPath, materialPath);
                Assert.IsNotNull(prefab);
                Assert.IsTrue(material == prefab.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                    "Unity may return different managed wrappers for the same native material.");
                Assert.AreEqual(3123, material.renderQueue, "Do not replace the user's in-memory material edits.");
                Assert.IsTrue(EditorUtility.IsDirty(material), "Existing material edits remain unsaved.");
                Assert.AreEqual(saved, File.ReadAllText(absoluteMaterialPath), "The existing material file must not be rewritten.");
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test] public void DestroyedActiveViewDoesNotBlockOtherViewOrHandleCleanup()
        {
            var pool = new CharacterTestShallowWaterSplashPool(PreparedPrefab(), SceneManager.GetActiveScene(), 2);
            try
            {
                Assert.IsTrue(pool.Ready, pool.Issue);
                Assert.IsTrue(pool.Emit(Vector3.zero, 1, 1, 10, 1));
                UnityEngine.Object.DestroyImmediate(pool.GetViewRoot(0));
                Assert.DoesNotThrow(() => pool.Tick(1.1));
                Assert.AreEqual(0, pool.ActiveCount);
                Assert.IsTrue(pool.Emit(Vector3.one, 1, 1, 10, 1.1), "Use the remaining live view without expanding.");
                Assert.AreEqual(2, pool.Capacity); Assert.AreEqual(1, pool.ResourceLoadCount);
                Assert.DoesNotThrow(pool.Clear); Assert.AreEqual(0, pool.ActiveCount);
            }
            finally { pool.Dispose(); }
            Assert.IsTrue(pool.IsDisposed); Assert.AreEqual(1, pool.ResourceReleaseCount);
            Assert.AreEqual(0, pool.CleanupFailures);
        }

        [Test] public void DisposeAfterSceneDestroyedItsObjectsReleasesExactlyOnce()
        {
            var pool = new CharacterTestShallowWaterSplashPool(PreparedPrefab(), SceneManager.GetActiveScene(), 1);
            Assert.IsTrue(pool.Ready, pool.Issue);
            Assert.IsTrue(pool.Emit(Vector3.zero, 1, 1, 10, 1));
            UnityEngine.Object.DestroyImmediate(pool.GetViewRoot(0));
            try
            {
                Assert.DoesNotThrow(pool.Dispose);
                Assert.IsTrue(pool.IsDisposed); Assert.IsFalse(pool.Ready);
                Assert.AreEqual(0, pool.ActiveCount); Assert.AreEqual(0, pool.ParticleCount);
                Assert.AreEqual(1, pool.ResourceReleaseCount); Assert.AreEqual(0, pool.CleanupFailures);
                Assert.DoesNotThrow(pool.Dispose); Assert.AreEqual(1, pool.ResourceReleaseCount);
            }
            finally { pool.Dispose(); }
        }

        [TestCase(ParticleSystemStopAction.Destroy)]
        [TestCase(ParticleSystemStopAction.Disable)]
        public void SelfStoppingPrefabIsRejectedBeforeResourceAcquisition(ParticleSystemStopAction stopAction)
        {
            var source = UnityEngine.Object.Instantiate(PreparedPrefab());
            try
            {
                var main = source.GetComponent<ParticleSystem>().main; main.stopAction = stopAction;
                using (var pool = new CharacterTestShallowWaterSplashPool(source, SceneManager.GetActiveScene(), 1))
                {
                    Assert.IsFalse(pool.Ready); StringAssert.Contains("stopAction", pool.Issue);
                    Assert.AreEqual(0, pool.ResourceLoadCount); Assert.AreEqual(0, pool.Capacity);
                }
                Assert.IsTrue(source != null, "A rejected input is borrowed, never destroyed by the pool.");
            }
            finally { if (source != null) UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
