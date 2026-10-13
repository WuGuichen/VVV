using MxFramework.CharacterApplication;
using MxFramework.Combat.Core;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Rendering
{
    public sealed class ShallowWaterSplashResourceTests
    {
        private static GameObject PreparedPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestShallowWaterSplashAssetBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "Prepare the approved formal water-splash resource before running native resource tests.");
            return prefab;
        }
        [Test] public void PhysicalEntityNamespaceNormalizesToCharacterIdentity()
        {
            Assert.AreEqual(1, CharacterTestCombatEntityLookup.ResolveCharacterEntityId(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1)));
            Assert.AreEqual(0, CharacterTestCombatEntityLookup.ResolveCharacterEntityId(new CombatEntityId(1)));
        }
        [Test] public void RangerFeetResolveByAuthoredSemanticInsteadOfNumericEnum()
        {
            var package = CharacterTestGeneratedPackageSource.LoadPackage("Assets/MxFrameworkGenerated/CharacterPackages/000010");
            Assert.IsNotNull(package); Assert.IsNotNull(package.Configs);
            int feet = 0;
            foreach (var contact in package.Configs.BodyPartContactPoints)
            {
                var kind = CharacterTestPhysicalAudioSystem.ResolveRegisteredContactPartKind(contact, package.Configs.BodyParts, package.Geometry);
                if (kind != CharacterBodyPartKind.Foot) continue;
                feet++; Assert.AreNotEqual((int)CharacterBodyPartKind.Foot, contact.BodyPartId);
                Assert.AreEqual(CharacterBodyPartKind.Custom,
                    CharacterTestPhysicalAudioSystem.ResolveRegisteredContactPartKind(contact, package.Configs.BodyParts),
                    "Legacy Bone rows need declared abstract-foot geometry roles, never numeric IDs or name guesses.");
            }
            Assert.AreEqual(2, feet);
            Assert.AreEqual(CharacterBodyPartKind.Custom,
                CharacterTestPhysicalAudioSystem.ResolveRegisteredContactPartKind(null, package.Configs.BodyParts));
        }
        [Test] public void FormalPrefabLoadsOnceAndReusesThenReleasesHandle()
        {
            var pool = new CharacterTestShallowWaterSplashPool(PreparedPrefab(), SceneManager.GetActiveScene(), 1);
            try
            {
                Assert.IsTrue(pool.Ready, pool.Issue); Assert.AreEqual(1, pool.ResourceLoadCount);
                Assert.IsTrue(pool.Emit(Vector3.zero, 1, 1, 10, 1));
                Assert.That(pool.ParticleCount, Is.InRange(4, 24)); Assert.AreEqual(1, pool.ActiveCount);
                Assert.IsFalse(pool.Emit(Vector3.one, 1, 1, 10, 1), "Pool saturation must not instantiate or queue.");
                int count = pool.ParticleCount; pool.Tick(1); Assert.AreEqual(count, pool.ParticleCount, "Paused clock.");
                pool.Tick(2); Assert.AreEqual(0, pool.ActiveCount); Assert.AreEqual(0, pool.ParticleCount);
                Assert.IsTrue(pool.Emit(Vector3.one, 1, 1, 10, 2)); Assert.AreEqual(1, pool.ReusedBursts);
                Assert.AreEqual(1, pool.ResourceLoadCount);
            }
            finally { pool.Dispose(); }
            Assert.AreEqual(1, pool.ResourceReleaseCount); Assert.IsFalse(pool.Ready);
            pool.Dispose(); Assert.AreEqual(1, pool.ResourceReleaseCount);
        }
        [Test] public void RuntimeClockRewindAndDisableClearAllParticles()
        {
            using (var pool = new CharacterTestShallowWaterSplashPool(PreparedPrefab(), SceneManager.GetActiveScene(), 2))
            {
                Assert.IsTrue(pool.Ready, pool.Issue); Assert.IsTrue(pool.Emit(Vector3.zero, 1, 1, 10, 5));
                pool.Tick(4); Assert.AreEqual(0, pool.ActiveCount); Assert.AreEqual(0, pool.ParticleCount);
                Assert.IsTrue(pool.Emit(Vector3.zero, 2, 1, 16, 4)); Assert.That(pool.ParticleCount, Is.LessThanOrEqualTo(24));
                pool.Clear(); Assert.AreEqual(0, pool.ActiveCount); Assert.AreEqual(0, pool.ParticleCount);
            }
        }
    }
}
