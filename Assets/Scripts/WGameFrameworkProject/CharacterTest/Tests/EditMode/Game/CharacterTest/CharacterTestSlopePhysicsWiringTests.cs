using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSlopePhysicsWiringTests
    {
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(1);
        private static readonly FixVector3 HalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [Test]
        public void SceneSettings_DefaultMissingExplicitZeroAndNegativeClampAreTyped()
        {
            var defaults = new CharacterTestScenePhysicsSettings();
            CharacterTestScenePhysicsSettings missing =
                JsonUtility.FromJson<CharacterTestScenePhysicsSettings>("{}");
            CharacterTestScenePhysicsSettings zero =
                JsonUtility.FromJson<CharacterTestScenePhysicsSettings>("{\"_groundSnapDistance\":0.0}");
            CharacterTestScenePhysicsSettings negative =
                JsonUtility.FromJson<CharacterTestScenePhysicsSettings>("{\"_groundSnapDistance\":-1.0}");

            Assert.AreEqual(0.10f, defaults.GroundSnapDistance, 0.000001f);
            Assert.AreEqual(0.10f, missing.GroundSnapDistance, 0.000001f);
            Assert.AreEqual(0f, zero.GroundSnapDistance);
            Assert.AreEqual(0f, negative.GroundSnapDistance);
            Assert.AreEqual(100000L, ToFix64(defaults.GroundSnapDistance).RawValue);
        }

        [Test]
        public void Runtime_OldAndNewConstructorSignaturesPreserveCompatibilityAndSnapRaw()
        {
            ConstructorInfo oldConstructor = typeof(CharacterTestCharacterPhysicsRuntime).GetConstructor(new[]
            {
                typeof(CombatPhysicsWorld), typeof(CombatPhysicsLayerMask), typeof(FixVector3),
                typeof(Fix64), typeof(Fix64), typeof(int), typeof(string)
            });
            ConstructorInfo newConstructor = typeof(CharacterTestCharacterPhysicsRuntime).GetConstructor(new[]
            {
                typeof(CombatPhysicsWorld), typeof(CombatPhysicsLayerMask), typeof(FixVector3),
                typeof(Fix64), typeof(Fix64), typeof(int), typeof(string), typeof(Fix64)
            });
            Assert.IsNotNull(oldConstructor);
            Assert.IsNotNull(newConstructor);

            CharacterTestCharacterPhysicsRuntime legacy = CreateRuntime(Fix64.Zero, "legacy", useLegacyConstructor: true);
            CharacterTestCharacterPhysicsRuntime formal = CreateRuntime(Fix64.FromRaw(100000), "formal");
            CharacterTestCharacterPhysicsRuntime clamped = CreateRuntime(Fix64.FromRaw(-1), "negative");

            Assert.AreEqual(Fix64.Zero, legacy.GroundSnapDistance);
            Assert.AreEqual(Fix64.FromRaw(100000), formal.GroundSnapDistance);
            Assert.AreEqual(Fix64.Zero, clamped.GroundSnapDistance);
        }

        [Test]
        public void FormalCacheBridgeUsesSettingsWhileLegacyComponentBridgeRemainsZero()
        {
            var root = new GameObject("Formal Physics Root");
            var box = new GameObject("Formal Ground");
            var legacyRoot = new GameObject("Legacy Physics Root");
            var legacyBox = new GameObject("Legacy Ground");
            try
            {
                box.transform.SetParent(root.transform, false);
                var cache = new CharacterTestWorldObjectSceneCache();
                cache.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            box.transform,
                            10080,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });
                CharacterTestCharacterPhysicsRuntime formal =
                    CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                        root.transform,
                        cache,
                        new CharacterTestScenePhysicsSettings());

                legacyBox.transform.SetParent(legacyRoot.transform, false);
                legacyBox.transform.localScale = new Vector3(4f, 1f, 4f);
                legacyBox.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                CharacterTestCombatPhysicsSceneBridge legacyBridge =
                    legacyRoot.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
                CharacterTestCharacterPhysicsRuntime legacy = legacyBridge.BuildRuntime();

                Assert.IsNotNull(formal);
                Assert.IsNotNull(legacy);
                Assert.AreEqual(Fix64.FromRaw(100000), formal.GroundSnapDistance);
                Assert.AreEqual(Fix64.Zero, legacy.GroundSnapDistance);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(legacyRoot);
            }
        }

        [Test]
        public void Runtime_AllMotionConfigOverloadsCarrySnapAndConfigIdentitySplitsMotorCache()
        {
            CharacterTestCharacterPhysicsRuntime legacy = CreateRuntime(Fix64.Zero, "same");
            CharacterTestCharacterPhysicsRuntime formal = CreateRuntime(Fix64.FromRaw(100000), "same");
            CombatMotionConfig simple = formal.CreateMotionConfig(60);
            CombatMotionConfig threshold = formal.CreateMotionConfig(60, Fix64.FromRaw(707107));
            CombatMotionConfig physics = formal.CreateMotionConfig(
                60, Fix64.FromRaw(707107), CharacterTestCharacterPhysicsComponent.Default);
            CombatMotionConfig posture = formal.CreateMotionConfig(
                60, Fix64.FromRaw(707107), default(CharacterTestColliderPostureComponent));
            CombatMotionConfig zero = legacy.CreateMotionConfig(60, Fix64.FromRaw(707107));

            Assert.AreEqual(Fix64.FromRaw(100000), simple.GroundSnapDistance);
            Assert.AreEqual(Fix64.FromRaw(100000), threshold.GroundSnapDistance);
            Assert.AreEqual(Fix64.FromRaw(100000), physics.GroundSnapDistance);
            Assert.AreEqual(Fix64.FromRaw(100000), posture.GroundSnapDistance);
            Assert.AreEqual(Fix64.Zero, zero.GroundSnapDistance);
            Assert.AreNotEqual(zero, threshold);
            Assert.AreNotEqual(zero.GetHashCode(), threshold.GetHashCode());

            var cache = new Dictionary<CombatMotionConfig, CombatKinematicMotor>();
            cache.Add(zero, new CombatKinematicMotor(zero));
            cache.Add(threshold, new CombatKinematicMotor(threshold));
            Assert.AreEqual(2, cache.Count);
            Assert.AreSame(cache[threshold], cache[formal.CreateMotionConfig(60, Fix64.FromRaw(707107))]);
        }

        [Test]
        public void NetworkCoreClonePreservesSnapObbNavigationAndAllRuntimeFields()
        {
            CharacterTestCharacterPhysicsRuntime source = CreateCloneSource(
                "snap=0; level=diagnostic-only");

            CharacterTestCharacterPhysicsRuntime clone =
                CharacterTestNetworkSessionRunner.CloneReplayPhysicsRuntime(source);

            AssertClone(source, clone);
        }

        [Test]
        public void UnityTransportClonePreservesSnapObbNavigationAndIgnoresSourceDescription()
        {
            CharacterTestCharacterPhysicsRuntime source = CreateCloneSource(
                "groundSnapDistance=999; diagnostic-only");

            CharacterTestCharacterPhysicsRuntime clone =
                CharacterTestUnityTransportServerRunner.CloneReplayPhysicsRuntime(source);

            AssertClone(source, clone);
            Assert.AreEqual(Fix64.FromRaw(100000), clone.GroundSnapDistance);
        }

        private static CharacterTestCharacterPhysicsRuntime CreateRuntime(
            Fix64 snap,
            string description,
            bool useLegacyConstructor = false)
        {
            var world = new CombatPhysicsWorld();
            var body = new CombatBodyId(2);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), body, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                body,
                new CombatColliderId(1),
                1,
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.One)));
            return useLegacyConstructor
                ? new CharacterTestCharacterPhysicsRuntime(
                    world, BlockingMask, HalfExtents, Fix64.FromRaw(10000),
                    Fix64.FromRaw(350000), 1, description)
                : new CharacterTestCharacterPhysicsRuntime(
                    world, BlockingMask, HalfExtents, Fix64.FromRaw(10000),
                    Fix64.FromRaw(350000), 1, description, snap);
        }

        private static CharacterTestCharacterPhysicsRuntime CreateCloneSource(string description)
        {
            var world = new CombatPhysicsWorld();
            var body = new CombatBodyId(2);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(3), body, R(1000000, 2000000, 3000000)));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                body,
                new CombatColliderId(7),
                1,
                R(100000, 200000, 300000),
                R(2000000, 1000000, 3000000),
                R(866025, 0, -500000),
                R(0, 1000000, 0),
                R(500000, 0, 866025),
                CombatSurfaceMaterial.Rough,
                priority: 4));
            var runtime = new CharacterTestCharacterPhysicsRuntime(
                world,
                BlockingMask,
                HalfExtents,
                Fix64.FromRaw(10000),
                Fix64.FromRaw(350000),
                1,
                description,
                Fix64.FromRaw(100000));
            runtime.SetNavigationBuildInput(
                new CharacterTestNavigationSessionBuildInput(
                    3, -2000, 2000, -2000, 2000, 0, 2000,
                    true, 64, 512, 4000, 64, 16),
                new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success,
                    2,
                    "bounds.formal"));
            return runtime;
        }

        private static void AssertClone(
            CharacterTestCharacterPhysicsRuntime source,
            CharacterTestCharacterPhysicsRuntime clone)
        {
            Assert.IsNotNull(clone);
            Assert.AreNotSame(source, clone);
            Assert.AreNotSame(source.World, clone.World);
            Assert.AreEqual(source.ObstacleLayerMask, clone.ObstacleLayerMask);
            Assert.AreEqual(source.StaticObstacleLayerMask, clone.StaticObstacleLayerMask);
            Assert.AreEqual(
                source.DynamicCharacterObstacleLayerMask,
                clone.DynamicCharacterObstacleLayerMask);
            Assert.AreEqual(
                source.DynamicMovableObjectObstacleLayerMask,
                clone.DynamicMovableObjectObstacleLayerMask);
            Assert.AreEqual(source.MotionObstacleLayerMask, clone.MotionObstacleLayerMask);
            Assert.AreEqual(source.CharacterHalfExtents, clone.CharacterHalfExtents);
            Assert.AreEqual(source.SkinWidth, clone.SkinWidth);
            Assert.AreEqual(source.MaxStepHeight, clone.MaxStepHeight);
            Assert.AreEqual(source.GroundSnapDistance, clone.GroundSnapDistance);
            Assert.AreEqual(source.StaticColliderCount, clone.StaticColliderCount);
            Assert.AreEqual(source.SourceDescription, clone.SourceDescription);
            Assert.AreEqual(source.NavigationBuildInput, clone.NavigationBuildInput);
            Assert.AreEqual(source.NavigationInputResult.Status, clone.NavigationInputResult.Status);
            Assert.AreEqual(source.NavigationInputResult.SourceIndex, clone.NavigationInputResult.SourceIndex);
            Assert.AreEqual(source.NavigationInputResult.StableId, clone.NavigationInputResult.StableId);
            Assert.AreEqual(
                source.GroundSnapDistance,
                clone.CreateMotionConfig(60, Fix64.FromRaw(707107)).GroundSnapDistance);
            Assert.IsTrue(source.World.TryGetObbCollider(
                new CombatBodyId(2), new CombatColliderId(7), out CombatPhysicsObbCollider expected));
            Assert.IsTrue(clone.World.TryGetObbCollider(
                new CombatBodyId(2), new CombatColliderId(7), out CombatPhysicsObbCollider actual));
            Assert.AreEqual(expected, actual);
        }

        private static Fix64 ToFix64(float value) =>
            Fix64.FromRaw((long)Math.Round((double)value * Fix64.Scale));

        private static FixVector3 R(long x, long y, long z) =>
            new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
    }
}
