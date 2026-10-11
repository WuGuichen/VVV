using System;
using System.Collections;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestMovableNaturalObjectPlayableTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";
        private const int TickRate = CharacterTestDeterministicUnits.DefaultTickRate;

        [TearDown]
        public void TearDown()
        {
            if (!Application.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void WorldObjectDefinitionCatalog_ValidatesDuplicatesAndMissingMovableFacet()
        {
            CharacterTestWorldObjectDefinition first = TestDefinition(
                9001,
                "movable.natural.catalog.first",
                new CharacterTestMovableNaturalObjectFacet(
                    CharacterTestDeterministicUnits.KilogramsToGrams(20f)));
            CharacterTestWorldObjectDefinition duplicateId = TestDefinition(
                9001,
                "movable.natural.catalog.duplicate_id",
                new CharacterTestMovableNaturalObjectFacet(
                    CharacterTestDeterministicUnits.KilogramsToGrams(30f)));
            CharacterTestWorldObjectDefinition duplicateStableId = TestDefinition(
                9002,
                "movable.natural.catalog.first",
                new CharacterTestMovableNaturalObjectFacet(
                    CharacterTestDeterministicUnits.KilogramsToGrams(40f)));

            Assert.Throws<ArgumentException>(() =>
                new CharacterTestWorldObjectDefinitionCatalog(new[] { first, duplicateId }));
            Assert.Throws<ArgumentException>(() =>
                new CharacterTestWorldObjectDefinitionCatalog(new[] { first, duplicateStableId }));

            CharacterTestWorldObjectDefinition missingMovable = TestDefinition(
                9003,
                "movable.natural.catalog.missing_movable",
                CharacterTestMovableNaturalObjectFacet.Missing);
            var catalog = new CharacterTestWorldObjectDefinitionCatalog(new[] { missingMovable });

            Assert.IsTrue(catalog.TryGetDefinition(9003, out CharacterTestWorldObjectDefinition resolved));
            Assert.IsFalse(resolved.CanSpawnMovableNaturalObject);
            Assert.IsFalse(resolved.TryCreateMovableNaturalObjectSpawnSpec(
                1,
                string.Empty,
                250,
                250,
                250,
                0,
                250,
                0,
                out _));
        }

        [Test]
        public void WorldObjectDefinitionCatalog_BuiltInMovableDefinitionsMatchSceneAuthoringValues()
        {
            CharacterTestWorldObjectDefinitionCatalog catalog =
                CharacterTestWorldObjectDefinitionCatalog.CreateCharacterTestSceneCatalog();

            Assert.GreaterOrEqual(catalog.Definitions.Count, 15);
            AssertBuiltInMovableDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.MovableSlickLight,
                "movable.natural.slick_light",
                CombatSurfaceMaterialKind.HardSlick,
                1000000L,
                0L,
                CharacterTestMovableNaturalObjectShapeKind.Aabb,
                18000,
                new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                expectsBurning: true,
                expectsCrack: false,
                expectsCondense: false);
            AssertBuiltInMovableDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy,
                "movable.natural.rough_heavy",
                CombatSurfaceMaterialKind.Rough,
                CombatSurfaceMaterial.Rough.Friction.RawValue,
                0L,
                CharacterTestMovableNaturalObjectShapeKind.Aabb,
                90000,
                new CharacterTestGenesisPotencyTriplet(0, 90, 0),
                expectsBurning: false,
                expectsCrack: true,
                expectsCondense: false);
            AssertBuiltInMovableDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.MovableSphere,
                "movable.natural.sphere",
                CombatSurfaceMaterialKind.Elastic,
                6000000L,
                750000L,
                CharacterTestMovableNaturalObjectShapeKind.Sphere,
                25000,
                new CharacterTestGenesisPotencyTriplet(4, 18, 2),
                expectsBurning: false,
                expectsCrack: false,
                expectsCondense: true);
            AssertBuiltInMovableDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.MovableCapsule,
                "movable.natural.capsule",
                CombatSurfaceMaterialKind.Rough,
                18000000L,
                0L,
                CharacterTestMovableNaturalObjectShapeKind.Capsule,
                65000,
                new CharacterTestGenesisPotencyTriplet(0, 90, 0),
                expectsBurning: false,
                expectsCrack: true,
                expectsCondense: false);
            AssertBuiltInStaticDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                "static.boundary.default",
                CharacterTestMovableNaturalObjectShapeKind.Aabb,
                CombatSurfaceMaterialKind.Default,
                10000000L,
                0L);
            AssertBuiltInStaticDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.StaticSphereProbe,
                "static.probe.sphere",
                CharacterTestMovableNaturalObjectShapeKind.Sphere,
                CombatSurfaceMaterialKind.Elastic,
                8000000L,
                600000L);
            AssertBuiltInStaticDefinition(
                catalog,
                CharacterTestWorldObjectDefinitionIds.StaticCapsuleProbe,
                "static.probe.capsule",
                CharacterTestMovableNaturalObjectShapeKind.Capsule,
                CombatSurfaceMaterialKind.Rough,
                20000000L,
                0L);
        }

        [Test]
        public void WorldObjectInstanceMarker_CreatesSpawnSpecEquivalentToLegacyMarker()
        {
            var root = new GameObject("world-object-definition-test");
            try
            {
                root.transform.position = new Vector3(1.25f, 0.45f, -0.75f);
                root.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                CharacterTestWorldObjectInstanceMarker instance =
                    root.AddComponent<CharacterTestWorldObjectInstanceMarker>();
                instance.Configure(
                    CharacterTestWorldObjectDefinitionIds.MovableSphere,
                    110,
                    "movable.natural.sphere");
                CharacterTestMovableNaturalObjectMarker legacy =
                    root.AddComponent<CharacterTestMovableNaturalObjectMarker>();
                legacy.Configure(
                    "movable.natural.sphere",
                    110,
                    "natural",
                    25f,
                    CombatSurfaceMaterialKind.Elastic,
                    6f,
                    0.75f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Sphere,
                    new CharacterTestGenesisPotencyTriplet(4, 18, 2),
                    CondenseProfile());

                Assert.IsTrue(instance.TryCreateMovableNaturalObjectSpawnSpec(
                    CharacterTestWorldObjectDefinitionCatalog.CreateCharacterTestSceneCatalog(),
                    out CharacterTestMovableNaturalObjectSpawnSpec fromDefinition,
                    out string diagnostic), diagnostic);
                CharacterTestMovableNaturalObjectSpawnSpec fromLegacy = legacy.CreateSpawnSpec();

                Assert.AreEqual(fromLegacy.StableId, fromDefinition.StableId);
                Assert.AreEqual(fromLegacy.NumericId, fromDefinition.NumericId);
                Assert.AreEqual(fromLegacy.Category, fromDefinition.Category);
                Assert.AreEqual(fromLegacy.MassGrams, fromDefinition.MassGrams);
                Assert.AreEqual(fromLegacy.Material, fromDefinition.Material);
                Assert.AreEqual(fromLegacy.ShapeKind, fromDefinition.ShapeKind);
                Assert.AreEqual(fromLegacy.BasePotency, fromDefinition.BasePotency);
                Assert.AreEqual(fromLegacy.PotencyProfile, fromDefinition.PotencyProfile);
                Assert.AreEqual(fromLegacy.HalfExtentXMillimeters, fromDefinition.HalfExtentXMillimeters);
                Assert.AreEqual(fromLegacy.XMillimeters, fromDefinition.XMillimeters);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WorldObjectSceneSource_CreatesMovableAndStaticSpecsFromCentralRecords()
        {
            var root = new GameObject("world-object-scene-source-test");
            var movableObject = new GameObject("MovableNaturalObject_movable.natural.sphere_visual");
            var floorObject = new GameObject("Static Floor");
            var sphereObject = new GameObject("PhysicsProbe_SphereTarget");
            try
            {
                movableObject.transform.SetParent(root.transform, worldPositionStays: false);
                floorObject.transform.SetParent(root.transform, worldPositionStays: false);
                sphereObject.transform.SetParent(root.transform, worldPositionStays: false);
                movableObject.transform.position = new Vector3(1.25f, 0.45f, -0.75f);
                movableObject.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                floorObject.transform.position = new Vector3(0f, -1f, 0f);
                floorObject.transform.localScale = new Vector3(4f, 1f, 4f);
                sphereObject.transform.position = new Vector3(2f, 0f, 0f);
                sphereObject.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

                CharacterTestWorldObjectSceneSource source =
                    root.AddComponent<CharacterTestWorldObjectSceneSource>();
                source.Configure(
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.MovableInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.MovableSphere,
                            110,
                            "movable.natural.sphere",
                            movableObject.transform,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    },
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            floorObject.transform,
                            12000,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform),
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticSphereProbe,
                            sphereObject.transform,
                            13010,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestMovableNaturalObjectSpawnSpec[] movableSpecs =
                    source.CollectMovableSpawnSpecs(out string[] movableDiagnostics);
                CharacterTestWorldObjectStaticColliderSpec[] staticSpecs =
                    source.CollectStaticColliderSpecs(out string[] staticDiagnostics);

                Assert.AreEqual(0, movableDiagnostics.Length);
                Assert.AreEqual(0, staticDiagnostics.Length);
                Assert.AreEqual(1, movableSpecs.Length);
                Assert.AreEqual("movable.natural.sphere", movableSpecs[0].StableId);
                Assert.AreEqual(CharacterTestMovableNaturalObjectShapeKind.Sphere, movableSpecs[0].ShapeKind);
                Assert.AreEqual(2, staticSpecs.Length);
                CharacterTestWorldObjectStaticColliderSpec aabbSpec =
                    FindStaticSpec(staticSpecs, CharacterTestMovableNaturalObjectShapeKind.Aabb);
                CharacterTestWorldObjectStaticColliderSpec sphereSpec =
                    FindStaticSpec(staticSpecs, CharacterTestMovableNaturalObjectShapeKind.Sphere);
                Assert.AreEqual(12000, aabbSpec.BodyId);
                Assert.AreEqual(13010, sphereSpec.BodyId);
                Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, sphereSpec.SurfaceMaterial.Kind);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CharacterTestWorldObjectStaticColliderSpec FindStaticSpec(
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> specs,
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].ShapeKind == shapeKind)
                    return specs[i];
            }

            Assert.Fail("Expected static collider spec with shape " + shapeKind);
            return default;
        }

        [Test]
        public void RuntimeMovableNaturalObjects_SpawnSortAndResetDeterministically()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.b", 20, CombatSurfaceMaterial.Rough, 40f, 2f, 0f),
                Spec("movable.natural.a", 10, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f)
            });

            Assert.AreEqual(2, world.MovableNaturalObjects.Count);
            Assert.AreEqual(10, world.MovableNaturalObjects[0].NumericId);
            Assert.AreEqual("movable.natural.a", world.MovableNaturalObjects[0].StableId);
            Assert.AreEqual(CharacterTestMovableNaturalObjectShapeKind.Aabb, world.MovableNaturalObjects[0].ShapeKind);

            CharacterTestMovableNaturalObjectState first = world.MovableNaturalObjects[0];
            first.SetHorizontalPosition(first.XMillimeters + 500, first.ZMillimeters + 250);
            first.SetVelocity(1200, 300);
            int resetCount = world.ResetMovableNaturalObjects();

            Assert.AreEqual(2, resetCount);
            Assert.AreEqual(first.SpawnSpec.XMillimeters, first.XMillimeters);
            Assert.AreEqual(first.SpawnSpec.ZMillimeters, first.ZMillimeters);
            Assert.AreEqual(0, first.VelocityXMillimetersPerSecond);
            Assert.AreEqual(0, first.VelocityZMillimetersPerSecond);
            StringAssert.Contains("stateModel=runtime-authority", CharacterTestMovableNaturalObjectSystem
                .CaptureSummary(world.MovableNaturalObjects)
                .ToSummaryLine());
        }

        [Test]
        public void RuntimeMovableNaturalObjects_ShapeKindsRegisterDynamicColliders()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.aabb", 10, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f),
                Spec("movable.natural.sphere", 20, CombatSurfaceMaterial.Elastic, 20f, 1f, 0f,
                    CharacterTestMovableNaturalObjectShapeKind.Sphere),
                Spec("movable.natural.capsule", 30, CombatSurfaceMaterial.Rough, 20f, 2f, 0f,
                    CharacterTestMovableNaturalObjectShapeKind.Capsule)
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateEmptyPhysicsRuntime();

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);

            Assert.IsTrue(physicsRuntime.World.TryGetAabbCollider(
                physicsRuntime.GetMovableObjectBodyId(10),
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId),
                out _));
            Assert.IsTrue(physicsRuntime.World.TryGetSphereCollider(
                physicsRuntime.GetMovableObjectBodyId(20),
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId),
                out CombatPhysicsSphereCollider sphere));
            Assert.IsTrue(physicsRuntime.World.TryGetCapsuleCollider(
                physicsRuntime.GetMovableObjectBodyId(30),
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId),
                out CombatPhysicsCapsuleCollider capsule));
            Assert.AreEqual(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(250),
                sphere.Radius);
            Assert.Greater(capsule.Radius.RawValue, 0L);
            Assert.AreEqual(3, physicsRuntime.World.CreateStats().ColliderCount);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_CombatColliderSyncSkipsUnchangedState()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.dirty_sync", 40, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f)
            });
            CharacterTestMovableNaturalObjectState state = world.MovableNaturalObjects[0];
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateEmptyPhysicsRuntime();
            CombatBodyId bodyId = physicsRuntime.GetMovableObjectBodyId(state.NumericId);
            var colliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId);

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
            CombatPhysicsWorldStats firstStats = physicsRuntime.World.CreateStats();

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
            Assert.AreEqual(firstStats, physicsRuntime.World.CreateStats());

            state.SetHorizontalPosition(state.XMillimeters + 100, state.ZMillimeters);
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
            CombatPhysicsWorldStats movedStats = physicsRuntime.World.CreateStats();
            Assert.Greater(movedStats.Revision, firstStats.Revision);
            Assert.AreEqual(firstStats.BodyCount, movedStats.BodyCount);
            Assert.AreEqual(firstStats.ColliderCount, movedStats.ColliderCount);
            Assert.IsTrue(physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body));
            Assert.AreEqual(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(state.XMillimeters),
                body.Position.X);

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
            Assert.AreEqual(movedStats, physicsRuntime.World.CreateStats());

            Assert.IsTrue(physicsRuntime.World.RemoveCollider(bodyId, colliderId));
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
            Assert.IsTrue(physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _));

            state.Reset(Spec(
                "movable.natural.dirty_sync",
                40,
                CombatSurfaceMaterial.Elastic,
                20f,
                1f,
                0f,
                CharacterTestMovableNaturalObjectShapeKind.Sphere));
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
            Assert.IsFalse(physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _));
            Assert.IsTrue(physicsRuntime.World.TryGetSphereCollider(
                bodyId,
                colliderId,
                out CombatPhysicsSphereCollider restoredSphere));
            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, restoredSphere.SurfaceMaterialKind);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_StaticContactScratchDoesNotAllocateAfterWarmup()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.perf.1", 101, CombatSurfaceMaterial.HardSlick, 20f, -4f, -4f),
                Spec("movable.natural.perf.2", 102, CombatSurfaceMaterial.HardSlick, 20f, -2f, -4f),
                Spec("movable.natural.perf.3", 103, CombatSurfaceMaterial.HardSlick, 20f, 0f, -4f),
                Spec("movable.natural.perf.4", 104, CombatSurfaceMaterial.HardSlick, 20f, 2f, -4f),
                Spec("movable.natural.perf.5", 105, CombatSurfaceMaterial.HardSlick, 20f, 4f, -4f),
                Spec("movable.natural.perf.6", 106, CombatSurfaceMaterial.HardSlick, 20f, -4f, -2f),
                Spec("movable.natural.perf.7", 107, CombatSurfaceMaterial.HardSlick, 20f, -2f, -2f),
                Spec("movable.natural.perf.8", 108, CombatSurfaceMaterial.HardSlick, 20f, 0f, -2f)
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateStaticObstacleGridRuntime(320);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CharacterTestCharacterMovementProfileSource.Load(null, null));
            motion.SetPhysicsRuntime(physicsRuntime);

            motion.Tick(world, TickRate, RuntimeFrame.Zero);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++)
            {
                motion.Tick(world, TickRate, new RuntimeFrame(i + 1));
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_StaticContactScratchQueriesNearbyObstaclesOnly()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateStaticObstacleRuntimeWithNearbyAndFarObstacles(96);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            var state = new CharacterTestMovableNaturalObjectState(
                Spec("movable.natural.query_grid", 301, CombatSurfaceMaterial.HardSlick, 20f, 1f, 1f));

            scratch.BuildStaticObstacles(physicsRuntime);
            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> candidates =
                scratch.QueryStaticObstacles(state);

            Assert.AreEqual(98, scratch.StaticObstacleCount);
            Assert.AreEqual(2, candidates.Count);
            Assert.IsFalse(scratch.LastStaticObstacleQueryCacheHit);
            Assert.AreEqual(2, scratch.LastStaticObstacleRawCandidateCount);
            Assert.AreEqual(2, scratch.LastStaticObstacleCandidateCount);
            Assert.AreEqual(1, scratch.LastStaticObstacleQueryCellCount);
            Assert.AreEqual(0, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(1, scratch.StaticObstacleObjectQueryCacheMissCount);
            Assert.AreEqual(
                CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 2000,
                candidates[0].Collider.BodyId.Value);
            Assert.AreEqual(
                CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 2001,
                candidates[1].Collider.BodyId.Value);

            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> cachedCandidates =
                scratch.QueryStaticObstacles(state);
            Assert.IsTrue(scratch.LastStaticObstacleQueryCacheHit);
            Assert.AreEqual(2, cachedCandidates.Count);
            Assert.AreEqual(0, scratch.LastStaticObstacleRawCandidateCount);
            Assert.AreEqual(2, scratch.LastStaticObstacleCandidateCount);
            Assert.AreEqual(0, scratch.LastStaticObstacleQueryCellCount);
            Assert.AreEqual(1, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(1, scratch.StaticObstacleObjectQueryCacheMissCount);

            state.SetHorizontalPosition(state.XMillimeters + 10, state.ZMillimeters);
            _ = scratch.QueryStaticObstacles(state);
            Assert.IsFalse(scratch.LastStaticObstacleQueryCacheHit);
            Assert.AreEqual(1, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(2, scratch.StaticObstacleObjectQueryCacheMissCount);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                _ = scratch.QueryStaticObstacles(state);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.GreaterOrEqual(scratch.StaticObstacleObjectQueryCacheHitCount, 64);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_NumericInteractionContactDoesNotAllocateAfterWarmup()
        {
            CharacterTestMovableNaturalObjectState movable =
                new CharacterTestMovableNaturalObjectState(Spec(
                    "movable.natural.perf.contact_pair",
                    109,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    0f,
                    0f));
            const int sourceStableId = 1201830;

            for (int i = 0; i < 8; i++)
            {
                movable.AccumulateInteractionFieldVelocity(
                    sourceStableId,
                    120,
                    0,
                    pressure: 10);
                movable.ApplyPendingVelocity();
                Assert.AreEqual(
                    sourceStableId,
                    movable.ContactAId);
                movable.BeginFrame();
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                movable.AccumulateInteractionFieldVelocity(
                    sourceStableId,
                    120,
                    0,
                    pressure: 10);
                movable.ApplyPendingVelocity();
                _ = movable.ContactAId;
                _ = movable.ContactBId;
                _ = movable.ContactKind;
                movable.BeginFrame();
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_StaticContactScratchCachesUntilTopologyChanges()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.cache.1", 201, CombatSurfaceMaterial.HardSlick, 20f, -4f, -4f),
                Spec("movable.natural.cache.2", 202, CombatSurfaceMaterial.HardSlick, 20f, -2f, -4f)
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateStaticObstacleGridRuntime(320);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);

            CharacterTestMovableNaturalObjectSystem.Tick(
                world,
                physicsRuntime,
                TickRate,
                RuntimeFrame.Zero,
                scratch);

            Assert.AreEqual(320, scratch.StaticObstacleCount);
            Assert.AreEqual(1, scratch.StaticObstacleRebuildCount);
            Assert.AreEqual(0, scratch.StaticObstacleCacheHitCount);
            Assert.AreEqual(0, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(2, scratch.StaticObstacleObjectQueryCacheMissCount);
            Assert.AreEqual(physicsRuntime.World.TopologyRevision, scratch.CachedTopologyRevision);

            for (int i = 0; i < 4; i++)
            {
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(i + 1),
                    scratch);
            }

            Assert.AreEqual(320, scratch.StaticObstacleCount);
            Assert.AreEqual(1, scratch.StaticObstacleRebuildCount);
            Assert.AreEqual(4, scratch.StaticObstacleCacheHitCount);
            Assert.AreEqual(8, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(2, scratch.StaticObstacleObjectQueryCacheMissCount);

            AddStaticObstacle(physicsRuntime.World, 320);

            CharacterTestMovableNaturalObjectSystem.Tick(
                world,
                physicsRuntime,
                TickRate,
                new RuntimeFrame(5),
                scratch);

            Assert.AreEqual(321, scratch.StaticObstacleCount);
            Assert.AreEqual(2, scratch.StaticObstacleRebuildCount);
            Assert.AreEqual(4, scratch.StaticObstacleCacheHitCount);
            Assert.AreEqual(8, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(4, scratch.StaticObstacleObjectQueryCacheMissCount);
            Assert.AreEqual(physicsRuntime.World.TopologyRevision, scratch.CachedTopologyRevision);

            CharacterTestMovableNaturalObjectSystem.Tick(
                world,
                physicsRuntime,
                TickRate,
                new RuntimeFrame(6),
                scratch);

            Assert.AreEqual(2, scratch.StaticObstacleRebuildCount);
            Assert.AreEqual(5, scratch.StaticObstacleCacheHitCount);
            Assert.AreEqual(10, scratch.StaticObstacleObjectQueryCacheHitCount);
            Assert.AreEqual(4, scratch.StaticObstacleObjectQueryCacheMissCount);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_ObjectContactScratchMatchesFullScanPairResolution()
        {
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
            {
                Spec("movable.natural.object_grid.a", 301, CombatSurfaceMaterial.HardSlick, 40f, 0f, 0f),
                Spec("movable.natural.object_grid.b", 302, CombatSurfaceMaterial.HardSlick, 40f, 0.45f, 0f),
                Spec("movable.natural.object_grid.c", 303, CombatSurfaceMaterial.HardSlick, 40f, 0.9f, 0f)
            };
            var fullScanWorld = new CharacterTestCharacterRuntimeWorld();
            fullScanWorld.SetMovableNaturalObjectSpawnSpecs(specs);
            var indexedWorld = new CharacterTestCharacterRuntimeWorld();
            indexedWorld.SetMovableNaturalObjectSpawnSpecs(specs);
            fullScanWorld.MovableNaturalObjects[0].SetVelocity(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(6f),
                0);
            indexedWorld.MovableNaturalObjects[0].SetVelocity(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(6f),
                0);

            CharacterTestMovableNaturalObjectSystem.Tick(fullScanWorld, null, TickRate);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            CharacterTestMovableNaturalObjectSystem.Tick(
                indexedWorld,
                null,
                TickRate,
                RuntimeFrame.Zero,
                scratch);

            Assert.AreEqual(3, scratch.LastObjectContactActiveCount);
            Assert.Greater(scratch.LastObjectContactIndexRebuildCount, 0);
            Assert.Greater(scratch.LastObjectContactCandidateCount, 0);
            for (int i = 0; i < specs.Length; i++)
            {
                CharacterTestMovableNaturalObjectState expected = fullScanWorld.MovableNaturalObjects[i];
                CharacterTestMovableNaturalObjectState actual = indexedWorld.MovableNaturalObjects[i];
                Assert.AreEqual(expected.XMillimeters, actual.XMillimeters, "X " + i);
                Assert.AreEqual(expected.ZMillimeters, actual.ZMillimeters, "Z " + i);
                Assert.AreEqual(
                    expected.VelocityXMillimetersPerSecond,
                    actual.VelocityXMillimetersPerSecond,
                    "VelocityX " + i);
                Assert.AreEqual(
                    expected.VelocityZMillimetersPerSecond,
                    actual.VelocityZMillimetersPerSecond,
                    "VelocityZ " + i);
                Assert.AreEqual(expected.ContactDisplacementXMillimeters, actual.ContactDisplacementXMillimeters, "ContactX " + i);
                Assert.AreEqual(expected.ContactDisplacementZMillimeters, actual.ContactDisplacementZMillimeters, "ContactZ " + i);
                Assert.AreEqual(expected.ContactPair, actual.ContactPair, "ContactPair " + i);
            }
        }

        [Test]
        public void RuntimeMovableNaturalObjects_ObjectContactScratchQueriesNearbyPairsOnlyAndDoesNotAllocateAfterWarmup()
        {
            const int farObjectCount = 40;
            var specs = new CharacterTestMovableNaturalObjectSpawnSpec[farObjectCount + 2];
            specs[0] = Spec("movable.natural.object_grid.near_a", 401, CombatSurfaceMaterial.HardSlick, 40f, 0f, 0f);
            specs[1] = Spec("movable.natural.object_grid.near_b", 402, CombatSurfaceMaterial.HardSlick, 40f, 0.45f, 0f);
            for (int i = 0; i < farObjectCount; i++)
            {
                specs[i + 2] = Spec(
                    "movable.natural.object_grid.far_" + i.ToString("D2"),
                    500 + i,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    20f + i * 8f,
                    20f);
            }

            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(specs);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            for (int i = 0; i < 8; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    null,
                    TickRate,
                    new RuntimeFrame(i),
                    scratch);
            }

            int fullPairCount = specs.Length * (specs.Length - 1) / 2;
            Assert.AreEqual(specs.Length, scratch.LastObjectContactActiveCount);
            Assert.Greater(scratch.LastObjectContactIndexRebuildCount, 0);
            Assert.Less(scratch.LastObjectContactCandidateCount, fullPairCount);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    null,
                    TickRate,
                    new RuntimeFrame(100 + i),
                    scratch);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Less(scratch.LastObjectContactCandidateCount, fullPairCount);
            Assert.Less(scratch.LastObjectContactRawCandidateCount, fullPairCount);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_ObjectContactScratchSkipsCellsOutsideQueryEnvelopeWithoutAllocation()
        {
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
            {
                SpecWithHalfExtents(
                    "movable.natural.object_grid.boundary_source",
                    601,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    new Vector3(0.35f, 0.25f, 0.25f),
                    3.7f,
                    0f),
                Spec(
                    "movable.natural.object_grid.boundary_target",
                    602,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    3.25f,
                    0f),
                Spec(
                    "movable.natural.object_grid.boundary_far_a",
                    603,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    7.5f,
                    0f),
                Spec(
                    "movable.natural.object_grid.boundary_far_b",
                    604,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    7.6f,
                    0.8f)
            };
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(specs);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();

            for (int i = 0; i < 4; i++)
                QueryBoundaryObjectContactSample(scratch, world);

            long before = GC.GetAllocatedBytesForCurrentThread();
            IReadOnlyList<int> candidates = null;
            for (int i = 0; i < 64; i++)
                candidates = QueryBoundaryObjectContactSample(scratch, world);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.IsNotNull(candidates);
            CollectionAssert.Contains(candidates, 1);
            CollectionAssert.DoesNotContain(candidates, 2);
            CollectionAssert.DoesNotContain(candidates, 3);
            Assert.Greater(
                scratch.LastObjectContactSkippedCellCount,
                0,
                "Object contact grid should skip occupied cells whose aggregate bounds cannot overlap the source query envelope.");
        }

        [Test]
        public void RuntimeMovableNaturalObjects_ObjectContactScratchSkipsVerticalCandidateBoundsWithoutAllocation()
        {
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.object_grid.vertical_source",
                    611,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 0.25f, 0f)),
                SpecWithHalfExtentsAt(
                    "movable.natural.object_grid.vertical_target",
                    612,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0.45f, 0.25f, 0f)),
                SpecWithHalfExtentsAt(
                    "movable.natural.object_grid.vertical_high_aabb",
                    613,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0.45f, 1.5f, 0f)),
                SpecWithHalfExtentsAt(
                    "movable.natural.object_grid.vertical_high_sphere",
                    614,
                    CombatSurfaceMaterial.HardSlick,
                    40f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0.1f, 1.5f, 0.1f),
                    CharacterTestMovableNaturalObjectShapeKind.Sphere)
            };
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(specs);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();

            for (int i = 0; i < 4; i++)
                QueryObjectContactSample(scratch, world);

            long before = GC.GetAllocatedBytesForCurrentThread();
            IReadOnlyList<int> candidates = null;
            for (int i = 0; i < 64; i++)
                candidates = QueryObjectContactSample(scratch, world);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.IsNotNull(candidates);
            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(1, candidates[0]);
            Assert.Greater(scratch.LastObjectContactRawCandidateCount, scratch.LastObjectContactCandidateCount);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_MaterialsMassBoundaryAndObjectContactBehaviors()
        {
            CharacterTestMovableNaturalObjectState slick = RunPushSample(
                Spec("movable.natural.slick", 10, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f));
            CharacterTestMovableNaturalObjectState rough = RunPushSample(
                Spec("movable.natural.rough", 20, CombatSurfaceMaterial.Rough, 20f, 0f, 0f));

            Assert.Greater(
                slick.XMillimeters,
                rough.XMillimeters + CharacterTestDeterministicUnits.MetersToMillimeters(0.15f),
                "HardSlick movable object should keep more push travel than Rough.");
            Assert.Greater(
                Math.Abs(slick.VelocityXMillimetersPerSecond),
                Math.Abs(rough.VelocityXMillimetersPerSecond) + 250,
                "HardSlick movable object should preserve more horizontal speed than Rough.");

            CharacterTestMovableNaturalObjectState lightPressure = RunWallPressureSample(
                Spec("movable.natural.light", 30, CombatSurfaceMaterial.Rough, 10f, 0.24f, 0f),
                CombatSurfaceMaterial.Rough);
            CharacterTestMovableNaturalObjectState heavyPressure = RunWallPressureSample(
                Spec("movable.natural.heavy", 40, CombatSurfaceMaterial.Rough, 100f, 0.24f, 0f),
                CombatSurfaceMaterial.Rough);

            Assert.Greater(heavyPressure.ContactPressure, lightPressure.ContactPressure);
            StringAssert.Contains("boundary:rough", heavyPressure.ContactPair);

            CharacterTestMovableNaturalObjectState elastic = RunWallPressureSample(
                Spec("movable.natural.elastic", 50, CombatSurfaceMaterial.Elastic, 30f, 0.24f, 0f),
                CombatSurfaceMaterial.Rough);

            Assert.Less(elastic.VelocityXMillimetersPerSecond, -1000);
            Assert.AreEqual("rebound-returned-force", elastic.BehaviorState);

            var contactWorld = new CharacterTestCharacterRuntimeWorld();
            contactWorld.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.contact.a", 60, CombatSurfaceMaterial.HardSlick, 40f, 0f, 0f),
                Spec("movable.natural.contact.b", 70, CombatSurfaceMaterial.HardSlick, 40f, 0.55f, 0f)
            });
            CharacterTestMovableNaturalObjectState a = contactWorld.MovableNaturalObjects[0];
            CharacterTestMovableNaturalObjectState b = contactWorld.MovableNaturalObjects[1];
            a.SetVelocity(CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(6f), 0);
            CharacterTestMovableNaturalObjectSystem.Tick(contactWorld, null, TickRate);

            Assert.Greater(b.VelocityXMillimetersPerSecond, 500);
            Assert.Greater(b.ContactDisplacementXMillimeters, 0);
            Assert.AreEqual(
                "movable.natural.contact.a->movable.natural.contact.b",
                b.ContactPair);
            Assert.AreEqual("object-contact-received", b.BehaviorState);

            var shapeWorld = new CharacterTestCharacterRuntimeWorld();
            shapeWorld.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec("movable.natural.shape.sphere", 80, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f,
                    CharacterTestMovableNaturalObjectShapeKind.Sphere),
                Spec("movable.natural.shape.capsule", 90, CombatSurfaceMaterial.HardSlick, 20f, 0.45f, 0f,
                    CharacterTestMovableNaturalObjectShapeKind.Capsule)
            });
            CharacterTestMovableNaturalObjectState sphereObject = shapeWorld.MovableNaturalObjects[0];
            CharacterTestMovableNaturalObjectState capsuleObject = shapeWorld.MovableNaturalObjects[1];
            sphereObject.SetVelocity(CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(5f), 0);
            CharacterTestMovableNaturalObjectSystem.Tick(shapeWorld, null, TickRate);

            Assert.Greater(capsuleObject.VelocityXMillimetersPerSecond, 300);
            Assert.Greater(capsuleObject.ContactDisplacementXMillimeters, 0);
            Assert.AreEqual(
                "movable.natural.shape.capsule->movable.natural.shape.sphere",
                capsuleObject.ContactPair);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_GravityLandsAllSupportedShapesOnStaticFloor()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.aabb",
                    701,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(-2f, 2f, 0f)),
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.sphere",
                    702,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.35f, 0.35f, 0.35f),
                    new Vector3(0f, 2f, 0f),
                    CharacterTestMovableNaturalObjectShapeKind.Sphere),
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.capsule",
                    703,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.3f, 0.75f, 0.3f),
                    new Vector3(2f, 2f, 0f),
                    CharacterTestMovableNaturalObjectShapeKind.Capsule)
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateVerticalContactRuntime(
                CombatSurfaceMaterial.HardSlick);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();

            for (int i = 0; i < 120; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(i),
                    scratch);
            }

            Assert.AreEqual(250, world.MovableNaturalObjects[0].YMillimeters);
            Assert.AreEqual(350, world.MovableNaturalObjects[1].YMillimeters);
            Assert.AreEqual(750, world.MovableNaturalObjects[2].YMillimeters);
            for (int i = 0; i < world.MovableNaturalObjects.Count; i++)
            {
                Assert.AreEqual(0, world.MovableNaturalObjects[i].VelocityYMillimetersPerSecond);
                Assert.AreEqual("idle", world.MovableNaturalObjects[i].BehaviorState);
            }
        }

        [Test]
        public void RuntimeMovableNaturalObjects_GravitySelectsHighestCrossedPlatformWithoutHorizontalPush()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.platform",
                    704,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 3f, 0f))
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateVerticalContactRuntime(
                CombatSurfaceMaterial.HardSlick,
                addPlatform: true);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();

            for (int i = 0; i < 120; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(i),
                    scratch);
            }

            CharacterTestMovableNaturalObjectState state = world.MovableNaturalObjects[0];
            Assert.AreEqual(1250, state.YMillimeters);
            Assert.AreEqual(0, state.XMillimeters);
            Assert.AreEqual(0, state.ZMillimeters);
            Assert.AreEqual(0, state.VelocityYMillimetersPerSecond);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_GravityUsesMaterialElasticityAndStopsLowSpeedBounce()
        {
            var dampedElastic = new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Elastic,
                CombatSurfaceMaterial.Elastic.Friction,
                Fix64.FromRatio(1, 2));
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.elastic",
                    705,
                    dampedElastic,
                    20f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 2f, 0f))
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateVerticalContactRuntime(
                CombatSurfaceMaterial.HardSlick);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            bool observedRebound = false;

            for (int i = 0; i < 300; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(i),
                    scratch);
                CharacterTestMovableNaturalObjectState state = world.MovableNaturalObjects[0];
                observedRebound |= state.VelocityYMillimetersPerSecond > 0
                    && state.BehaviorState == "vertical-rebound";
            }

            CharacterTestMovableNaturalObjectState settled = world.MovableNaturalObjects[0];
            Assert.IsTrue(observedRebound);
            Assert.AreEqual(250, settled.YMillimeters);
            Assert.AreEqual(0, settled.VelocityYMillimetersPerSecond);
            Assert.AreEqual("idle", settled.BehaviorState);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_UpwardMotionStopsAtStaticCeiling()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.ceiling",
                    706,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 0.5f, 0f))
            });
            CharacterTestMovableNaturalObjectState state = world.MovableNaturalObjects[0];
            state.SetVerticalVelocity(12000);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateVerticalContactRuntime(
                CombatSurfaceMaterial.HardSlick,
                addCeiling: true);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            bool contactedCeiling = false;

            for (int i = 0; i < 30; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(i),
                    scratch);
                if (state.ContactCount > 0 && state.VelocityYMillimetersPerSecond == 0)
                {
                    contactedCeiling = true;
                    Assert.AreEqual(1250, state.YMillimeters);
                    break;
                }
            }

            Assert.IsTrue(contactedCeiling);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_GravityRequiresPhysicsAuthorityAndFallsWithoutSupport()
        {
            CharacterTestMovableNaturalObjectSpawnSpec spec = SpecWithHalfExtentsAt(
                "movable.natural.gravity.authority",
                707,
                CombatSurfaceMaterial.HardSlick,
                20f,
                new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(20f, 2f, 0f));
            var noPhysicsWorld = new CharacterTestCharacterRuntimeWorld();
            noPhysicsWorld.SetMovableNaturalObjectSpawnSpecs(new[] { spec });

            CharacterTestMovableNaturalObjectSystem.Tick(noPhysicsWorld, null, TickRate);

            Assert.AreEqual(2000, noPhysicsWorld.MovableNaturalObjects[0].YMillimeters);
            Assert.AreEqual(0, noPhysicsWorld.MovableNaturalObjects[0].VelocityYMillimetersPerSecond);

            var fallingWorld = new CharacterTestCharacterRuntimeWorld();
            fallingWorld.SetMovableNaturalObjectSpawnSpecs(new[] { spec });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateVerticalContactRuntime(
                CombatSurfaceMaterial.HardSlick);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            CharacterTestMovableNaturalObjectSystem.Tick(
                fallingWorld,
                physicsRuntime,
                TickRate,
                new RuntimeFrame(1),
                scratch);

            Assert.Less(fallingWorld.MovableNaturalObjects[0].YMillimeters, 2000);
            Assert.Less(fallingWorld.MovableNaturalObjects[0].VelocityYMillimetersPerSecond, 0);
            Assert.AreEqual("falling", fallingWorld.MovableNaturalObjects[0].BehaviorState);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_GravitySteadyStateDoesNotAllocateAfterWarmup()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.noalloc",
                    708,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(20f, 20f, 0f))
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateVerticalContactRuntime(
                CombatSurfaceMaterial.HardSlick);
            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            for (int i = 0; i < 8; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(i),
                    scratch);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    TickRate,
                    new RuntimeFrame(100 + i),
                    scratch);
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void RuntimeMovableNaturalObjects_GravitySnapshotRestoreContinuesDeterministically()
        {
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
            {
                SpecWithHalfExtentsAt(
                    "movable.natural.gravity.snapshot",
                    709,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(20f, 2f, 0f))
            };
            using (var source = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateVerticalContactRuntime(CombatSurfaceMaterial.HardSlick),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            using (var restored = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateVerticalContactRuntime(CombatSurfaceMaterial.HardSlick),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            {
                Tick(source, 4);
                Tick(restored, 4);
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(
                    source.CharacterRuntime,
                    source.RuntimeCommands);
                var restoredParticipant = new CharacterTestNetworkSnapshotParticipant(
                    restored.CharacterRuntime,
                    restored.RuntimeCommands);
                RuntimeCustomState snapshot = sourceParticipant.CaptureSnapshot(source.CurrentFrame);
                NetworkSnapshotApplyResult apply = restoredParticipant.ApplyRollbackSnapshot(
                    restored.CurrentFrame,
                    snapshot,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);

                for (int i = 0; i < 4; i++)
                {
                    RuntimeFrame frame = source.CurrentFrame;
                    source.Tick(1d / TickRate);
                    restored.Tick(1d / TickRate);
                    CharacterTestMovableNaturalObjectState expected = source.MovableNaturalObjects[0];
                    CharacterTestMovableNaturalObjectState actual = restored.MovableNaturalObjects[0];
                    Assert.AreEqual(expected.YMillimeters, actual.YMillimeters, "Y frame=" + frame.Value);
                    Assert.AreEqual(
                        expected.VelocityYMillimetersPerSecond,
                        actual.VelocityYMillimetersPerSecond,
                        "VelocityY frame=" + frame.Value);
                    Assert.AreEqual(expected.BehaviorState, actual.BehaviorState, "Behavior frame=" + frame.Value);
                    Assert.AreEqual(
                        sourceParticipant.ComputeStateHash(frame),
                        restoredParticipant.ComputeStateHash(frame),
                        "Hash frame=" + frame.Value);
                }
            }
        }

        [Test]
        public void RuntimeMovableNaturalObjects_PotencyInfluencesClampImmunityThresholdsAndReset()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                Spec(
                    "movable.natural.potency.grass",
                    10,
                    CombatSurfaceMaterial.HardSlick,
                    18f,
                    0f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                    BurningCrushProfile()),
                Spec(
                    "movable.natural.potency.stone",
                    20,
                    CombatSurfaceMaterial.Rough,
                    90f,
                    1f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CharacterTestGenesisPotencyTriplet(0, 90, 0),
                    StoneCrackProfile()),
                Spec(
                    "movable.natural.potency.condense",
                    30,
                    CombatSurfaceMaterial.Elastic,
                    30f,
                    2f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CharacterTestGenesisPotencyTriplet(4, 18, 2),
                    CondenseProfile()),
                Spec(
                    "movable.natural.potency.clamp",
                    40,
                    CombatSurfaceMaterial.HardSlick,
                    10f,
                    3f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CharacterTestGenesisPotencyTriplet(995, 1, 995),
                    BurningCrushProfile())
            });

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                10,
                CharacterTestGenesisPotencyInfluenceKind.Fire,
                new RuntimeFrame(12),
                out int affected,
                out string message), message);
            Assert.AreEqual(1, affected);
            CharacterTestMovableNaturalObjectState grass = world.MovableNaturalObjects[0];
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(18, 20, 22), grass.CurrentPotency);
            Assert.AreEqual(CharacterTestGenesisPotencyResultKind.Burning, grass.LatestPotencyApplication.Result);
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(18, 0, 22), grass.LatestPotencyApplication.AppliedDelta);

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                20,
                CharacterTestGenesisPotencyInfluenceKind.Hammer,
                new RuntimeFrame(13),
                out _,
                out message), message);
            CharacterTestMovableNaturalObjectState stone = world.MovableNaturalObjects[1];
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(70, 40, 0), stone.CurrentPotency);
            Assert.AreEqual(CharacterTestGenesisPotencyResultKind.Cracked, stone.LatestPotencyApplication.Result);
            Assert.IsTrue(stone.LatestPotencyApplication.ImmuneScatter);
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(0, 0, 6), stone.LatestPotencyApplication.IgnoredDelta);

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                30,
                CharacterTestGenesisPotencyInfluenceKind.Condense,
                new RuntimeFrame(14),
                out _,
                out message), message);
            CharacterTestMovableNaturalObjectState condense = world.MovableNaturalObjects[2];
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(2, 42, 2), condense.CurrentPotency);
            Assert.AreEqual(
                CharacterTestGenesisPotencyResultKind.CondensedLayer,
                condense.LatestPotencyApplication.Result);

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                40,
                CharacterTestGenesisPotencyInfluenceKind.Fire,
                new RuntimeFrame(15),
                out _,
                out message), message);
            CharacterTestMovableNaturalObjectState clamp = world.MovableNaturalObjects[3];
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(1000, 1, 1000), clamp.CurrentPotency);
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(5, 0, 5), clamp.LatestPotencyApplication.AppliedDelta);
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(13, 0, 17), clamp.LatestPotencyApplication.IgnoredDelta);

            Assert.AreEqual(1, world.ResetMovableNaturalObjects(10));
            Assert.AreEqual(grass.BasePotency, grass.CurrentPotency);
            Assert.AreEqual(
                CharacterTestGenesisPotencyInfluenceKind.None,
                grass.LatestPotencyApplication.InfluenceKind);
        }

        [Test]
        public void MovableNaturalObjectResetCommand_ValidatesDecodesAndAppliesThroughGameSlice()
        {
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.MovableNaturalObjectReset(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.DebugUi,
                targetNumericId: 10,
                traceId: new RuntimeTraceIdentity(977036));
            RuntimeCommandRegistry registry = CharacterTestRuntimeCommandRegistry.Create();

            RuntimeCommandValidationResult validation =
                new RuntimeCommandRegistryValidator(registry).Validate(command);
            CharacterTestMovableNaturalObjectResetRequest request =
                CharacterTestRuntimeCommandFactory.DecodeMovableNaturalObjectReset(command);

            Assert.IsTrue(validation.Success, validation.Error.Message);
            Assert.AreEqual(10, request.TargetNumericId);
            Assert.AreEqual(CharacterTestRuntimeCommandFactory.MovableNaturalObjectResetSchemaVersion, request.SchemaVersion);

            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                enableCharacterSimulation: false,
                movableNaturalObjectSpawnSpecs: new[]
                {
                    Spec("movable.natural.reset", 10, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f)
                }))
            {
                CharacterTestMovableNaturalObjectState state = slice.MovableNaturalObjects[0];
                state.SetHorizontalPosition(state.XMillimeters + 400, state.ZMillimeters);
                RuntimeCommandValidationResult enqueue = slice.EnqueueMovableNaturalObjectReset(10);

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / TickRate);

                Assert.AreEqual(state.SpawnSpec.XMillimeters, state.XMillimeters);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.MovableNaturalObjectResetApplied
                        && evt.TargetId == 10));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
            }
        }

        [Test]
        public void MovableNaturalObjectPotencyInfluenceCommand_ValidatesDecodesAndAppliesThroughGameSlice()
        {
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluence(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.DebugUi,
                targetNumericId: 10,
                CharacterTestGenesisPotencyInfluenceKind.Fire,
                traceId: new RuntimeTraceIdentity(992043));
            RuntimeCommandRegistry registry = CharacterTestRuntimeCommandRegistry.Create();
            RuntimeCommandValidationResult validation =
                new RuntimeCommandRegistryValidator(registry).Validate(command);
            CharacterTestMovableNaturalObjectPotencyInfluenceRequest request =
                CharacterTestRuntimeCommandFactory.DecodeMovableNaturalObjectPotencyInfluence(command);

            Assert.IsTrue(validation.Success, validation.Error.Message);
            Assert.AreEqual(10, request.TargetNumericId);
            Assert.AreEqual(CharacterTestGenesisPotencyInfluenceKind.Fire, request.InfluenceKind);
            Assert.AreEqual(
                CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluenceSchemaVersion,
                request.SchemaVersion);

            RuntimeCommand invalid = CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluence(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.DebugUi,
                10,
                CharacterTestGenesisPotencyInfluenceKind.None);
            Assert.IsFalse(new RuntimeCommandRegistryValidator(registry).Validate(invalid).Success);

            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                enableCharacterSimulation: false,
                movableNaturalObjectSpawnSpecs: new[]
                {
                    Spec(
                        "movable.natural.potency.command.a",
                        10,
                        CombatSurfaceMaterial.HardSlick,
                        20f,
                        0f,
                        0f,
                        CharacterTestMovableNaturalObjectShapeKind.Aabb,
                        new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                        BurningCrushProfile()),
                    Spec(
                        "movable.natural.potency.command.b",
                        20,
                        CombatSurfaceMaterial.Rough,
                        40f,
                        1f,
                        0f,
                        CharacterTestMovableNaturalObjectShapeKind.Aabb,
                        new CharacterTestGenesisPotencyTriplet(4, 18, 2),
                        CondenseProfile())
                }))
            {
                RuntimeCommandValidationResult all = slice.EnqueueMovableNaturalObjectPotencyInfluence(
                    CharacterTestGenesisPotencyInfluenceKind.Condense,
                    0);
                Assert.IsTrue(all.Success, all.Error.Message);
                slice.Tick(1d / TickRate);
                Assert.AreEqual(44, slice.MovableNaturalObjects[0].CurrentPotency.Cohesion);
                Assert.AreEqual(42, slice.MovableNaturalObjects[1].CurrentPotency.Cohesion);

                RuntimeCommandValidationResult single = slice.EnqueueMovableNaturalObjectPotencyInfluence(
                    CharacterTestGenesisPotencyInfluenceKind.Fire,
                    10);
                Assert.IsTrue(single.Success, single.Error.Message);
                slice.Tick(1d / TickRate);

                Assert.AreEqual(18, slice.MovableNaturalObjects[0].CurrentPotency.Motion);
                Assert.AreEqual(2, slice.MovableNaturalObjects[1].CurrentPotency.Motion);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.MovableNaturalObjectPotencyInfluenceApplied
                        && evt.TargetId == 10));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
            }
        }

        [Test]
        public void MovableNaturalObjects_SnapshotHashRollbackCloneAndLegacySchemaNormalize()
        {
            var specs = new[]
            {
                Spec("movable.natural.snapshot", 10, CombatSurfaceMaterial.HardSlick, 20f, 0f, 0f,
                    CharacterTestMovableNaturalObjectShapeKind.Sphere,
                    new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                    BurningCrushProfile())
            };
            using (var source = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            using (var target = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            {
                Tick(source, 4);
                Tick(target, 4);
                RuntimeFrame frame = source.CurrentFrame;
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(
                    source.CharacterRuntime,
                    source.RuntimeCommands);
                long hashBefore = sourceParticipant.ComputeStateHash(frame);

                CharacterTestMovableNaturalObjectState state = source.MovableNaturalObjects[0];
                state.SetHorizontalPosition(state.XMillimeters + 750, state.ZMillimeters);
                state.SetVelocity(900, 0);
                state.ApplyPotencyInfluence(CharacterTestGenesisPotencyInfluenceKind.Fire, frame);
                long hashAfter = sourceParticipant.ComputeStateHash(frame);

                Assert.AreNotEqual(hashBefore, hashAfter);

                RuntimeCustomState snapshot = sourceParticipant.CaptureSnapshot(frame);
                Assert.AreEqual(
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    snapshot.SchemaVersion);
                var targetParticipant = new CharacterTestNetworkSnapshotParticipant(
                    target.CharacterRuntime,
                    target.RuntimeCommands);
                NetworkSnapshotApplyResult apply = targetParticipant.ApplySnapshot(frame, snapshot);

                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(hashAfter, targetParticipant.ComputeStateHash(frame));

                CharacterTestNetworkSnapshotDocument document =
                    JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(snapshot.PayloadJson);
                Assert.AreEqual(
                    (int)CharacterTestMovableNaturalObjectShapeKind.Sphere,
                    document.MovableNaturalObjects[0].ShapeKind);
                Assert.AreEqual(18, document.MovableNaturalObjects[0].CurrentPotencyMotion);
                Assert.AreEqual(
                    (int)CharacterTestGenesisPotencyResultKind.Burning,
                    document.MovableNaturalObjects[0].LatestPotencyResultKind);
                CharacterTestNetworkSnapshotDocument rollback = document.CloneForRollback();
                Assert.NotNull(rollback.MovableNaturalObjects);
                Assert.AreEqual(1, rollback.MovableNaturalObjects.Length);
                Assert.AreNotSame(document.MovableNaturalObjects[0], rollback.MovableNaturalObjects[0]);

                document.SchemaVersion = CharacterTestNetworkSnapshotParticipant.SurfaceMaterialSchemaVersionValue;
                document.MovableNaturalObjects = null;
                string legacyPayload = JsonConvert.SerializeObject(document);
                RuntimeCustomState legacyState = new RuntimeCustomState(
                    CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                    CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                    CharacterTestNetworkSnapshotParticipant.SurfaceMaterialSchemaVersionValue,
                    legacyPayload);
                NetworkSnapshotApplyResult legacyApply =
                    targetParticipant.ApplySnapshot(frame, legacyState);

                Assert.IsTrue(legacyApply.Success, legacyApply.Message);
                Assert.AreEqual(0, target.MovableNaturalObjects.Count);

                document = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(snapshot.PayloadJson);
                document.SchemaVersion = CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectSchemaVersionValue;
                document.MovableNaturalObjects[0].ShapeKind = 0;
                string schema3Payload = JsonConvert.SerializeObject(document);
                RuntimeCustomState schema3State = new RuntimeCustomState(
                    CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                    CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                    CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectSchemaVersionValue,
                    schema3Payload);
                NetworkSnapshotApplyResult schema3Apply =
                    targetParticipant.ApplySnapshot(frame, schema3State);

                Assert.IsTrue(schema3Apply.Success, schema3Apply.Message);
                Assert.AreEqual(CharacterTestMovableNaturalObjectShapeKind.Aabb, target.MovableNaturalObjects[0].ShapeKind);
                Assert.AreEqual(CharacterTestGenesisPotencyTriplet.Zero, target.MovableNaturalObjects[0].CurrentPotency);

                document = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(snapshot.PayloadJson);
                document.SchemaVersion =
                    CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectShapeSchemaVersionValue;
                string schema4Payload = JsonConvert.SerializeObject(document);
                RuntimeCustomState schema4State = new RuntimeCustomState(
                    CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                    CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                    CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectShapeSchemaVersionValue,
                    schema4Payload);
                NetworkSnapshotApplyResult schema4Apply =
                    targetParticipant.ApplySnapshot(frame, schema4State);

                Assert.IsTrue(schema4Apply.Success, schema4Apply.Message);
                Assert.AreEqual(
                    CharacterTestMovableNaturalObjectShapeKind.Sphere,
                    target.MovableNaturalObjects[0].ShapeKind);
                Assert.AreEqual(CharacterTestGenesisPotencyTriplet.Zero, target.MovableNaturalObjects[0].CurrentPotency);
            }
        }

        [Test]
        public void MovableNaturalObjects_ReplayPotencyCommandsProduceMatchingHash()
        {
            CharacterTestReplayCapture capture = CreateMovablePotencyReplay();

            RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);

            Assert.IsTrue(playback.Success, playback.FailureMessage);
            Assert.Greater(capture.FrameCount, 0);
        }

        [Test]
        public void MovableNaturalObjects_ReplayPushCommandsProduceMatchingHashAndMismatchReports()
        {
            CharacterTestReplayCapture capture = CreateMovablePushReplay();

            RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);

            Assert.IsTrue(playback.Success, playback.FailureMessage);
            Assert.Greater(capture.FrameCount, 0);

            var records = new List<RuntimeReplayFrameRecord>(capture.RuntimeReplaySnapshot.Records);
            RuntimeReplayFrameRecord last = records[records.Count - 1];
            records[records.Count - 1] = new RuntimeReplayFrameRecord(
                last.Frame,
                last.Commands,
                last.ResultHash + 1L,
                last.DiagnosticsSummary);
            var mutated = new CharacterTestReplayCapture(
                new RuntimeReplaySnapshot(capture.RuntimeReplaySnapshot.Header, records),
                capture.BootstrapSnapshot,
                capture.TickRate,
                capture.StateSyncPolicy,
                capture.AuthoritativeCommands,
                capture.ReplaySliceFactory);

            RuntimeReplayPlaybackResult mismatch = CharacterTestReplayPlayback.Play(mutated);

            Assert.IsFalse(mismatch.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, mismatch.FailureCode);
            Assert.AreEqual(last.Frame, mismatch.FailureFrame);
        }

        [TestCase(CharacterTestMovableNaturalObjectShapeKind.Sphere)]
        [TestCase(CharacterTestMovableNaturalObjectShapeKind.Capsule)]
        public void MovableNaturalObjects_PlayerCommandsPushShapeMovables(
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            var specs = new[]
            {
                Spec(
                    "movable.natural.command." + shapeKind.ToString().ToLowerInvariant(),
                    10,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    0f,
                    0f,
                    shapeKind)
            };
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateReadyDynamicOnlyPhysicsRuntime(),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            {
                Tick(slice, 4);
                CharacterTestCharacterEntity entity = FindLocalPlayer(slice);
                ResetForPhysicsPush(entity);
                CharacterTestMovableNaturalObjectState movable = slice.MovableNaturalObjects[0];
                movable.SetHorizontalPosition(
                    entity.Transform.XMillimeters
                    + entity.Physics.EffectiveRadiusMillimeters
                    + movable.HalfExtentXMillimeters
                    + 40,
                    entity.Transform.ZMillimeters);
                int startX = movable.XMillimeters;
                int playerStartX = entity.Transform.XMillimeters;
                int iterationLimitFrames = 0;

                for (int i = 0; i < 45; i++)
                {
                    RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                        CharacterTestRuntimeCommandFactory.SetControlIntent(
                            slice.CurrentFrame,
                            CharacterTestRuntimeCommandSources.PlayerInput,
                            entity.Id.Value,
                            Vector2.right,
                            jumpPressed: false,
                            jumpHeld: false,
                            jumpReleased: false,
                            attackPrimaryPressed: false,
                            traceId: new RuntimeTraceIdentity(930243)));
                    Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                    slice.Tick(1d / TickRate);
                    if ((entity.CollisionFlags & CombatMotionCollisionFlags.IterationLimit) != 0)
                        iterationLimitFrames++;
                }

                Assert.Greater(
                    movable.XMillimeters,
                    startX + CharacterTestDeterministicUnits.MetersToMillimeters(0.05f),
                    shapeKind + " movable should move after player command push.");
                Assert.Greater(
                    entity.Transform.XMillimeters,
                    playerStartX + CharacterTestDeterministicUnits.MetersToMillimeters(0.02f),
                    shapeKind + " push should not leave the player stuck at the initial contact point.");
                Assert.AreEqual(0, iterationLimitFrames, shapeKind + " push should not hit the collision iteration limit.");
                Assert.AreEqual(shapeKind, movable.ShapeKind);
            }
        }

        [Test]
        public void CharacterTestScene_MovableNaturalObjectSandboxBuildsUniqueSpawnSpecs()
        {
            CharacterTestWorldObjectSceneCache cache = OpenSceneWorldObjectCache();
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
                cache.CollectMovableSpawnSpecs(out string[] diagnostics);
            CharacterTestWorldObjectSceneSource[] worldSources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            CharacterTestMovableNaturalObjectSceneSource[] movableSources =
                Object.FindObjectsByType<CharacterTestMovableNaturalObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            CharacterTestWorldObjectInstanceMarker[] instanceMarkers =
                Object.FindObjectsByType<CharacterTestWorldObjectInstanceMarker>(UnityEngine.FindObjectsInactive.Exclude);
            CharacterTestMovableNaturalObjectMarker[] legacyMarkers =
                Object.FindObjectsByType<CharacterTestMovableNaturalObjectMarker>(UnityEngine.FindObjectsInactive.Exclude);

            Assert.GreaterOrEqual(specs.Length, 7);
            Assert.AreEqual(
                0,
                worldSources.Length,
                "Official CharacterTestScene should use GameManager scene cache instead of a world object scene source.");
            Assert.AreEqual(
                0,
                movableSources.Length,
                "Official CharacterTestScene should not need a separate movable object scene source.");
            Assert.AreEqual(8, cache.MovableInstanceCount);
            Assert.GreaterOrEqual(cache.StaticColliderInstanceCount, 32);
            Assert.GreaterOrEqual(
                specs.Length,
                cache.MovableInstanceCount,
                "GameManager scene cache should be the official movable authoring path.");
            Assert.AreEqual(
                0,
                instanceMarkers.Length,
                "CharacterTestScene movable objects should not depend on per-object instance markers after source consolidation.");
            Assert.AreEqual(
                0,
                legacyMarkers.Length,
                "CharacterTestScene should not depend on legacy movable authoring markers after catalog migration.");
            Assert.That(
                diagnostics,
                Has.None.EqualTo("legacy-movable-marker:movable.natural.slick_light"));
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var numericIds = new HashSet<int>();
            bool hasSphere = false;
            bool hasCapsule = false;
            int configuredProfileCount = 0;
            for (int i = 0; i < specs.Length; i++)
            {
                Assert.Greater(specs[i].NumericId, 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(specs[i].StableId));
                Assert.IsTrue(stableIds.Add(specs[i].StableId), specs[i].StableId);
                Assert.IsTrue(numericIds.Add(specs[i].NumericId), specs[i].NumericId.ToString());
                Assert.Greater(specs[i].MassGrams, 0);
                Assert.Greater(specs[i].HalfExtentXMillimeters, 0);
                Assert.Greater(specs[i].HalfExtentYMillimeters, 0);
                Assert.Greater(specs[i].HalfExtentZMillimeters, 0);
                hasSphere |= specs[i].ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere;
                hasCapsule |= specs[i].ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule;
                if (IsPotencyProfileConfigured(specs[i].PotencyProfile))
                    configuredProfileCount++;
            }

            Assert.IsTrue(hasSphere, "CharacterTestScene should contain a Sphere movable natural object.");
            Assert.IsTrue(hasCapsule, "CharacterTestScene should contain a Capsule movable natural object.");
            Assert.GreaterOrEqual(
                configuredProfileCount,
                7,
                "CharacterTestScene movable objects should expose readable potency profiles.");
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeAppliesPotencyInfluenceAndReset()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            bool managerFound = false;
            bool startAccepted = false;
            bool sliceFound = false;
            bool movableFound = false;
            bool fireQueued = false;
            bool fireApplied = false;
            bool resetQueued = false;
            bool resetApplied = false;
            bool noCommandErrors = false;
            string issue = string.Empty;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            managerFound = manager != null;
            if (manager != null)
            {
                CharacterTestMainMenuCommandResult start = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                startAccepted = start.Success;
                issue = start.Message;
                for (int i = 0; i < 20; i++)
                {
                    GameSlice waitingSlice = manager.RuntimeContext.Slice;
                    if (waitingSlice != null
                        && FindFirstMovableWithBurningProfile(waitingSlice.MovableNaturalObjects) != null)
                    {
                        break;
                    }

                    yield return null;
                }

                GameSlice slice = manager.RuntimeContext.Slice;
                sliceFound = slice != null;
                CharacterTestMovableNaturalObjectState movable = slice != null
                    ? FindFirstMovableWithBurningProfile(slice.MovableNaturalObjects)
                    : null;
                movableFound = movable != null;

                if (slice != null && movable != null)
                {
                    CharacterTestGenesisPotencyTriplet basePotency = movable.BasePotency;
                    RuntimeCommandValidationResult fire = slice.EnqueueMovableNaturalObjectPotencyInfluence(
                        CharacterTestGenesisPotencyInfluenceKind.Fire,
                        movable.NumericId);
                    fireQueued = fire.Success;
                    if (!fireQueued)
                        issue = fire.Error.Message;
                    slice.Tick(1d / TickRate);
                    yield return null;

                    fireApplied = !movable.CurrentPotency.Equals(basePotency)
                        && movable.LatestPotencyApplication.InfluenceKind ==
                            CharacterTestGenesisPotencyInfluenceKind.Fire;

                    RuntimeCommandValidationResult reset = slice.EnqueueMovableNaturalObjectReset(movable.NumericId);
                    resetQueued = reset.Success;
                    if (!resetQueued)
                        issue = reset.Error.Message;
                    slice.Tick(1d / TickRate);
                    yield return null;

                    resetApplied = movable.CurrentPotency.Equals(basePotency)
                        && movable.LatestPotencyApplication.InfluenceKind ==
                            CharacterTestGenesisPotencyInfluenceKind.None;
                    noCommandErrors = slice.RuntimeCommands.LastCommandErrors.Count == 0;
                    if (!noCommandErrors && slice.RuntimeCommands.LastCommandErrors.Count > 0)
                        issue = slice.RuntimeCommands.LastCommandErrors[0].Message;
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(startAccepted, issue);
            Assert.IsTrue(sliceFound, "CharacterTestScene did not create a GameSlice in Play Mode.");
            Assert.IsTrue(movableFound, "CharacterTestScene did not collect a burning-profile movable object.");
            Assert.IsTrue(fireQueued, issue);
            Assert.IsTrue(fireApplied, "Potency Fire command did not change runtime movable potency.");
            Assert.IsTrue(resetQueued, issue);
            Assert.IsTrue(resetApplied, "Reset did not restore movable potency to base.");
            Assert.IsTrue(noCommandErrors, issue);
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModePushesMovableObjectAndSyncsViewTransform()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            bool managerFound = false;
            bool startAccepted = false;
            bool sliceFound = false;
            bool movableFound = false;
            bool playerFound = false;
            bool viewFound = false;
            bool placementApplied = false;
            bool commandsAccepted = true;
            bool runtimeFell = false;
            bool fallingViewSynced = false;
            bool runtimeMoved = false;
            bool viewMoved = false;
            string issue = string.Empty;
            string ySyncIssue = string.Empty;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            managerFound = manager != null;
            if (manager != null)
            {
                CharacterTestMainMenuCommandResult start = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                startAccepted = start.Success;
                issue = start.Message;
                for (int i = 0; i < 20; i++)
                {
                    GameSlice waitingSlice = manager.RuntimeContext.Slice;
                    if (waitingSlice != null && FindLocalPlayerOrNull(waitingSlice) != null)
                        break;

                    yield return null;
                }

                GameSlice slice = manager.RuntimeContext.Slice;
                sliceFound = slice != null;
                CharacterTestCharacterEntity player = slice != null ? FindLocalPlayerOrNull(slice) : null;
                playerFound = player != null;
                CharacterTestMovableNaturalObjectState movable = slice != null
                    ? FindFirstMovableByShape(
                        slice.MovableNaturalObjects,
                        CharacterTestMovableNaturalObjectShapeKind.Sphere)
                    : null;
                if (movable == null && slice != null && slice.MovableNaturalObjects.Count > 0)
                    movable = slice.MovableNaturalObjects[0];
                movableFound = movable != null;
                Transform movableView = movable != null
                    ? FindMovableViewTransform(movable.NumericId, movable.StableId)
                    : null;
                viewFound = movableView != null;

                if (slice != null && player != null && movable != null && movableView != null)
                {
                    var inputProvider = new FakeInputProvider();
                    inputProvider.SetContext(InputContext.Gameplay);
                    slice.SetLocalInputProvider(inputProvider);

                    CharacterTestMovableNaturalObjectSnapshot[] snapshots =
                        slice.CharacterRuntime.CaptureNetworkMovableNaturalObjects();
                    for (int i = 0; i < snapshots.Length; i++)
                    {
                        if (snapshots[i].NumericId != movable.NumericId)
                        {
                            snapshots[i].XMillimeters = player.Transform.XMillimeters;
                            snapshots[i].YMillimeters = player.Transform.YMillimeters
                                + snapshots[i].HalfExtentYMillimeters;
                            snapshots[i].ZMillimeters = player.Transform.ZMillimeters
                                + CharacterTestDeterministicUnits.MetersToMillimeters(10f + i);
                            snapshots[i].VelocityXMillimetersPerSecond = 0;
                            snapshots[i].VelocityYMillimetersPerSecond = 0;
                            snapshots[i].VelocityZMillimetersPerSecond = 0;
                            continue;
                        }

                        snapshots[i].XMillimeters = player.Transform.XMillimeters
                            + player.Physics.EffectiveRadiusMillimeters
                            + snapshots[i].HalfExtentXMillimeters
                            + 40;
                        snapshots[i].YMillimeters = player.Transform.YMillimeters
                            + snapshots[i].HalfExtentYMillimeters
                            + CharacterTestDeterministicUnits.MetersToMillimeters(2f);
                        snapshots[i].ZMillimeters = player.Transform.ZMillimeters;
                        snapshots[i].VelocityXMillimetersPerSecond = 0;
                        snapshots[i].VelocityYMillimetersPerSecond = 0;
                        snapshots[i].VelocityZMillimetersPerSecond = 0;
                        break;
                    }

                    NetworkSnapshotApplyResult placeResult =
                        slice.CharacterRuntime.ApplyNetworkMovableNaturalObjects(snapshots);
                    placementApplied = placeResult.Success;
                    if (!placementApplied)
                        issue = placeResult.Message;
                    yield return null;
                    int targetNumericId = movable.NumericId;
                    if (!slice.CharacterRuntime.World.TryFindMovableNaturalObject(targetNumericId, out movable))
                    {
                        movable = null;
                        movableFound = false;
                        issue = "Could not resolve live movable natural object after snapshot apply.";
                    }

                    if (movable != null)
                    {
                        int airborneStartY = movable.YMillimeters;
                        for (int i = 0; i < 60; i++)
                        {
                            slice.Tick(1d / TickRate);
                            yield return null;
                        }

                        slice.Services.Presentation.TickPresentation(0d);
                        runtimeFell = movable.YMillimeters < airborneStartY - 1000;
                        fallingViewSynced = Math.Abs(
                            movableView.position.y
                            - CharacterTestDeterministicUnits.MillimetersToMeters(movable.YMillimeters)) < 0.01f;
                        ySyncIssue = "runtimeY="
                            + movable.YMillimeters.ToString()
                            + " viewY="
                            + movableView.position.y.ToString();
                        int startX = movable.XMillimeters;
                        int playerStartX = player.Transform.XMillimeters;
                        int playerStartZ = player.Transform.ZMillimeters;
                        float viewStartX = movableView.position.x;
                        inputProvider.SetSnapshot(CreateMoveInputSnapshot(Vector2.right));
                        for (int i = 0; i < 45; i++)
                        {
                            slice.Tick(1d / TickRate);
                            yield return null;
                        }

                        inputProvider.SetSnapshot(InputSnapshot.Empty);
                        commandsAccepted = slice.RuntimeCommands.LastCommandErrors.Count == 0;
                        if (!commandsAccepted && slice.RuntimeCommands.LastCommandErrors.Count > 0)
                            issue = slice.RuntimeCommands.LastCommandErrors[0].Message;
                        runtimeMoved = movable.XMillimeters > startX + 20;
                        viewMoved = movableView.position.x > viewStartX + 0.02f;
                        issue = "playerDelta=("
                            + (player.Transform.XMillimeters - playerStartX).ToString()
                            + ","
                            + (player.Transform.ZMillimeters - playerStartZ).ToString()
                            + ") movableDelta="
                            + (movable.XMillimeters - startX).ToString()
                            + " input=("
                            + player.Input.MoveXMilli.ToString()
                            + ","
                            + player.Input.MoveYMilli.ToString()
                            + ";world="
                            + player.Input.WorldMoveXMilli.ToString()
                            + ","
                            + player.Input.WorldMoveYMilli.ToString()
                            + ") actionLocked="
                            + player.Action.MovementLocked.ToString()
                            + " actionScale="
                            + player.Action.MovementControlScaleMilli.ToString()
                            + " motionLocked="
                            + player.MovementLocked.ToString()
                            + " motionScale="
                            + player.MovementControlScaleMilli.ToString()
                            + " drained="
                            + slice.RuntimeCommands.LastDrainedCommands.Count.ToString()
                            + " errors="
                            + slice.RuntimeCommands.LastCommandErrors.Count.ToString()
                            + " contactPair="
                            + movable.ContactPair
                            + " behavior="
                            + movable.BehaviorState
                            + " contactCount="
                            + movable.ContactCount.ToString()
                            + " frame="
                            + slice.CurrentFrame.Value.ToString()
                            + " paused="
                            + manager.RuntimeContext.IsPaused.ToString();
                    }
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(startAccepted, issue);
            Assert.IsTrue(sliceFound, "CharacterTestScene did not create a GameSlice in Play Mode.");
            Assert.IsTrue(movableFound, "CharacterTestScene did not collect movable natural objects.");
            Assert.IsTrue(playerFound, "CharacterTestScene did not spawn the local player.");
            Assert.IsTrue(viewFound, "Movable natural object view was not found.");
            Assert.IsTrue(placementApplied, issue);
            Assert.IsTrue(commandsAccepted, issue);
            Assert.IsTrue(runtimeFell, "Movable natural object did not fall from its airborne placement.");
            Assert.IsTrue(
                fallingViewSynced,
                "Movable natural object view did not sync the runtime Y position. " + ySyncIssue);
            Assert.IsTrue(runtimeMoved, "Movable natural object runtime state did not move after player push. " + issue);
            Assert.IsTrue(viewMoved, "Movable natural object view transform did not sync after player push. " + issue);
        }

        private static CharacterTestMovableNaturalObjectState RunPushSample(
            CharacterTestMovableNaturalObjectSpawnSpec spec)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[] { spec });
            CharacterTestMovableNaturalObjectState state = world.MovableNaturalObjects[0];

            CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
            state.AccumulateContactPush(
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0015,
                sourceMassGrams: CharacterTestDeterministicUnits.KilogramsToGrams(80f),
                sourcePushScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                sourceVelocityXMillimetersPerSecond: CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(6f),
                sourceVelocityZMillimetersPerSecond: 0);
            CharacterTestMovableNaturalObjectSystem.Tick(world, null, TickRate);
            for (int i = 1; i < 45; i++)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(world, null, TickRate);
            }

            return state;
        }

        private static CharacterTestMovableNaturalObjectState RunWallPressureSample(
            CharacterTestMovableNaturalObjectSpawnSpec spec,
            CombatSurfaceMaterial wallMaterial)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[] { spec });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateWallRuntime(wallMaterial);
            CharacterTestMovableNaturalObjectState state = world.MovableNaturalObjects[0];

            CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
            state.AccumulateContactPush(
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0015,
                sourceMassGrams: CharacterTestDeterministicUnits.KilogramsToGrams(80f),
                sourcePushScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                sourceVelocityXMillimetersPerSecond: CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(6f),
                sourceVelocityZMillimetersPerSecond: 0);
            CharacterTestMovableNaturalObjectSystem.Tick(world, physicsRuntime, TickRate);
            return state;
        }

        private static CharacterTestCharacterPhysicsRuntime CreateWallRuntime(CombatSurfaceMaterial material)
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 700);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 700),
                bodyId,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(ToFix64(0.5f), ToFix64(-1f), ToFix64(-1f)),
                localMax: new FixVector3(ToFix64(0.7f), ToFix64(2f), ToFix64(1f)),
                surfaceMaterial: material));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(ToFix64(0.35f), ToFix64(0.9f), ToFix64(0.35f)),
                ToFix64(0.02f),
                ToFix64(0.25f),
                staticColliderCount: 1,
                sourceDescription: "test.wall");
        }

        private static CharacterTestCharacterPhysicsRuntime CreateVerticalContactRuntime(
            CombatSurfaceMaterial material,
            bool addPlatform = false,
            bool addCeiling = false)
        {
            var world = new CombatPhysicsWorld();
            AddStaticBox(
                world,
                800,
                new FixVector3(ToFix64(-10f), ToFix64(-1f), ToFix64(-10f)),
                new FixVector3(ToFix64(10f), Fix64.Zero, ToFix64(10f)),
                material);
            int staticColliderCount = 1;
            if (addPlatform)
            {
                AddStaticBox(
                    world,
                    801,
                    new FixVector3(ToFix64(-1f), ToFix64(0.5f), ToFix64(-1f)),
                    new FixVector3(ToFix64(1f), ToFix64(1f), ToFix64(1f)),
                    material);
                staticColliderCount++;
            }

            if (addCeiling)
            {
                AddStaticBox(
                    world,
                    802,
                    new FixVector3(ToFix64(-1f), ToFix64(1.5f), ToFix64(-1f)),
                    new FixVector3(ToFix64(1f), ToFix64(1.75f), ToFix64(1f)),
                    material);
                staticColliderCount++;
            }

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(ToFix64(0.35f), ToFix64(0.9f), ToFix64(0.35f)),
                ToFix64(0.02f),
                ToFix64(0.25f),
                staticColliderCount,
                "test.vertical-contact");
        }

        private static void AddStaticBox(
            CombatPhysicsWorld world,
            int idOffset,
            FixVector3 min,
            FixVector3 max,
            CombatSurfaceMaterial material)
        {
            var bodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + idOffset);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + idOffset),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: min,
                localMax: max,
                surfaceMaterial: material));
        }

        private static CharacterTestCharacterPhysicsRuntime CreateStaticObstacleGridRuntime(int obstacleCount)
        {
            var world = new CombatPhysicsWorld();
            int safeCount = Math.Max(1, obstacleCount);
            for (int i = 0; i < safeCount; i++)
            {
                AddStaticObstacle(world, i);
            }

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(ToFix64(0.35f), ToFix64(0.9f), ToFix64(0.35f)),
                ToFix64(0.02f),
                ToFix64(0.25f),
                staticColliderCount: safeCount,
                sourceDescription: "test.static-obstacle-grid");
        }

        private static CharacterTestCharacterPhysicsRuntime CreateStaticObstacleRuntimeWithNearbyAndFarObstacles(
            int farObstacleCount)
        {
            var world = new CombatPhysicsWorld();
            AddStaticObstacleAt(world, 0, 1f, 1f);
            AddStaticObstacleAt(world, 1, 1.3f, 1f);
            int safeFarObstacleCount = Math.Max(0, farObstacleCount);
            for (int i = 0; i < safeFarObstacleCount; i++)
                AddStaticObstacleAt(world, i + 2, 50f + (i % 6) * 3f, 50f + (i / 6) * 3f);

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(ToFix64(0.35f), ToFix64(0.9f), ToFix64(0.35f)),
                ToFix64(0.02f),
                ToFix64(0.25f),
                staticColliderCount: safeFarObstacleCount + 2,
                sourceDescription: "test.static-obstacle-grid-query");
        }

        private static void AddStaticObstacle(CombatPhysicsWorld world, int obstacleIndex)
        {
            int row = obstacleIndex / 6;
            int column = obstacleIndex % 6;
            AddStaticObstacleAt(world, obstacleIndex, 50 + column * 3, 50 + row * 3);
        }

        private static void AddStaticObstacleAt(CombatPhysicsWorld world, int obstacleIndex, float x, float z)
        {
            var bodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 2000 + obstacleIndex);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 2000 + obstacleIndex),
                bodyId,
                new FixVector3(ToFix64(x), Fix64.Zero, ToFix64(z))));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.Half, Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.FromInt(2), Fix64.Half),
                surfaceMaterial: CombatSurfaceMaterial.Rough));
        }

        private static CharacterTestCharacterPhysicsRuntime CreateEmptyPhysicsRuntime()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(ToFix64(0.35f), ToFix64(0.9f), ToFix64(0.35f)),
                ToFix64(0.02f),
                ToFix64(0.25f),
                staticColliderCount: 0,
                sourceDescription: "test.empty");
        }

        private static CharacterTestCharacterPhysicsRuntime CreateReadyDynamicOnlyPhysicsRuntime()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 900);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 900),
                bodyId,
                new FixVector3(Fix64.FromInt(100), Fix64.FromInt(100), Fix64.FromInt(100))));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half),
                surfaceMaterial: CombatSurfaceMaterial.Default));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(ToFix64(0.35f), ToFix64(0.9f), ToFix64(0.35f)),
                ToFix64(0.02f),
                ToFix64(0.25f),
                staticColliderCount: 1,
                sourceDescription: "test.dynamic-only");
        }

        private static void ResetForPhysicsPush(CharacterTestCharacterEntity entity)
        {
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                entity.Transform.XMillimeters,
                0,
                entity.Transform.ZMillimeters,
                entity.Transform.YawMilliDegrees));
            entity.SetMotionStateDeterministic(
                verticalVelocityMillimetersPerSecond: 0,
                lastMoveInputXMilli: 0,
                lastMoveInputYMilli: 0,
                isGrounded: true,
                physicsEnabled: true,
                collisionFlags: CombatMotionCollisionFlags.Grounded);
        }

        private static CharacterTestReplayCapture CreateMovablePushReplay()
        {
            var specs = new[]
            {
                Spec(
                    "movable.natural.replay.sphere",
                    10,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    0f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Sphere)
            };
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateReadyDynamicOnlyPhysicsRuntime(),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            {
                Tick(slice, 4);
                CharacterTestCharacterEntity entity = FindLocalPlayer(slice);
                ResetForPhysicsPush(entity);
                CharacterTestMovableNaturalObjectState movable = slice.MovableNaturalObjects[0];
                movable.SetHorizontalPosition(
                    entity.Transform.XMillimeters + entity.Physics.EffectiveRadiusMillimeters + movable.HalfExtentXMillimeters + 40,
                    entity.Transform.ZMillimeters);

                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                var recorder = new CharacterTestReplayRecorder(new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: TickRate,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 2,
                    configHash: "character-test-movable-replay",
                    resourceCatalogHash: "character-test-resources",
                    stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly));
                recorder.Begin(slice.CurrentFrame, participant.CaptureSnapshot(slice.CurrentFrame));

                for (int i = 0; i < 30; i++)
                    TickRecorded(slice, participant, recorder, entity.Id.Value, Vector2.right);

                return recorder.CreateCapture(() => new GameSlice(
                    NullRuntimeLogger.Instance,
                    physicsRuntime: CreateReadyDynamicOnlyPhysicsRuntime(),
                    createCharacterViews: false,
                    enableLocalInputCommands: false,
                    movableNaturalObjectSpawnSpecs: specs));
            }
        }

        private static CharacterTestReplayCapture CreateMovablePotencyReplay()
        {
            var specs = new[]
            {
                Spec(
                    "movable.natural.replay.potency",
                    10,
                    CombatSurfaceMaterial.HardSlick,
                    20f,
                    0f,
                    0f,
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                    CharacterTestGenesisPotencyProfile.WithDefaultThresholds(
                        immuneMotion: false,
                        immuneCohesion: false,
                        immuneScatter: false,
                        burningEnabled: true,
                        crushEnabled: true,
                        crackEnabled: false,
                        condensedLayerEnabled: true))
            };
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs))
            {
                Tick(slice, 4);
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                var recorder = new CharacterTestReplayRecorder(new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: TickRate,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 2,
                    configHash: "character-test-movable-potency-replay",
                    resourceCatalogHash: "character-test-resources",
                    stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly));
                recorder.Begin(slice.CurrentFrame, participant.CaptureSnapshot(slice.CurrentFrame));

                TickPotencyRecorded(slice, participant, recorder, 10, CharacterTestGenesisPotencyInfluenceKind.Fire);
                TickPotencyRecorded(slice, participant, recorder, 10, CharacterTestGenesisPotencyInfluenceKind.Hammer);
                TickPotencyRecorded(slice, participant, recorder, 0, CharacterTestGenesisPotencyInfluenceKind.Condense);

                return recorder.CreateCapture(() => new GameSlice(
                    NullRuntimeLogger.Instance,
                    createCharacterViews: false,
                    enableLocalInputCommands: false,
                    movableNaturalObjectSpawnSpecs: specs));
            }
        }

        private static void TickRecorded(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant participant,
            CharacterTestReplayRecorder recorder,
            int entityId,
            Vector2 move)
        {
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    frame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    entityId,
                    move,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    traceId: new RuntimeTraceIdentity(910854)));
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

            slice.Tick(1d / TickRate);
            long hash = participant.ComputeStateHash(frame);
            recorder.RecordAuthoritativeCommands(new NetworkAuthoritativeCommandPacket(
                "local",
                frame,
                frame,
                slice.RuntimeCommands.LastDrainedCommands));
            recorder.RecordFrame(
                frame,
                slice.RuntimeCommands.LastDrainedCommands,
                hash,
                CharacterTestReplayDiagnostics.CreateFrameSummary(
                    frame,
                    slice.RuntimeCommands.LastDrainedCommands.Count,
                    slice.CharacterEntities.Count,
                    hash));
        }

        private static void TickPotencyRecorded(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant participant,
            CharacterTestReplayRecorder recorder,
            int targetNumericId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind)
        {
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluence(
                    frame,
                    CharacterTestRuntimeCommandSources.DebugUi,
                    targetNumericId,
                    influenceKind,
                    traceId: new RuntimeTraceIdentity(965208)));
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

            slice.Tick(1d / TickRate);
            long hash = participant.ComputeStateHash(frame);
            recorder.RecordAuthoritativeCommands(new NetworkAuthoritativeCommandPacket(
                "local",
                frame,
                frame,
                slice.RuntimeCommands.LastDrainedCommands));
            recorder.RecordFrame(
                frame,
                slice.RuntimeCommands.LastDrainedCommands,
                hash,
                CharacterTestReplayDiagnostics.CreateFrameSummary(
                    frame,
                    slice.RuntimeCommands.LastDrainedCommands.Count,
                    slice.CharacterEntities.Count,
                    hash));
        }

        private static CharacterTestWorldObjectSceneCache OpenSceneWorldObjectCache()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "CharacterTestScene should open for movable object tests.");
            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            Assert.AreEqual(1, managers.Length, "CharacterTestScene should contain one GameManager scene entry.");
            return managers[0].WorldObjectSceneCache;
        }

        private static CharacterTestMovableNaturalObjectState FindFirstMovableByShape(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> movables,
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            if (movables == null)
                return null;

            for (int i = 0; i < movables.Count; i++)
            {
                CharacterTestMovableNaturalObjectState movable = movables[i];
                if (movable != null && movable.ShapeKind == shapeKind)
                    return movable;
            }

            return null;
        }

        private static CharacterTestMovableNaturalObjectState FindFirstMovableWithBurningProfile(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> movables)
        {
            if (movables == null)
                return null;

            for (int i = 0; i < movables.Count; i++)
            {
                CharacterTestMovableNaturalObjectState movable = movables[i];
                if (movable != null && movable.PotencyProfile.BurningEnabled)
                    return movable;
            }

            return null;
        }

        private static Transform FindMovableViewTransform(int numericId, string stableId)
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < managers.Length; i++)
            {
                GameManager manager = managers[i];
                CharacterTestWorldObjectSceneCache cache = manager == null ? null : manager.WorldObjectSceneCache;
                if (cache == null || !cache.Enabled)
                    continue;

                IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> instances =
                    cache.MovableInstances;
                for (int j = 0; j < instances.Count; j++)
                {
                    CharacterTestWorldObjectSceneSource.MovableInstance instance = instances[j];
                    if (instance != null
                        && instance.NumericInstanceId == numericId
                        && instance.Transform != null)
                    {
                        return instance.Transform;
                    }
                }
            }

            CharacterTestWorldObjectSceneSource[] worldSources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < worldSources.Length; i++)
            {
                CharacterTestWorldObjectSceneSource source = worldSources[i];
                if (source == null || !source.Enabled)
                    continue;

                IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> instances =
                    source.MovableInstances;
                for (int j = 0; j < instances.Count; j++)
                {
                    CharacterTestWorldObjectSceneSource.MovableInstance instance = instances[j];
                    if (instance != null
                        && instance.NumericInstanceId == numericId
                        && instance.Transform != null)
                    {
                        return instance.Transform;
                    }
                }
            }

            CharacterTestWorldObjectInstanceMarker[] instanceMarkers =
                Object.FindObjectsByType<CharacterTestWorldObjectInstanceMarker>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < instanceMarkers.Length; i++)
            {
                if (instanceMarkers[i] != null && instanceMarkers[i].NumericInstanceId == numericId)
                    return instanceMarkers[i].transform;
            }

            CharacterTestMovableNaturalObjectMarker[] markers =
                Object.FindObjectsByType<CharacterTestMovableNaturalObjectMarker>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null && markers[i].NumericId == numericId)
                    return markers[i].transform;
            }

            string expectedName = "MovableNaturalObject_" + stableId + "_visual";
            Transform[] transforms = Object.FindObjectsByType<Transform>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && string.Equals(transforms[i].name, expectedName, StringComparison.Ordinal))
                    return transforms[i];
            }

            return null;
        }

        private static InputSnapshot CreateMoveInputSnapshot(Vector2 move)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);
        }

        private static T FindSingle<T>() where T : Object
        {
            T[] values = Object.FindObjectsByType<T>(UnityEngine.FindObjectsInactive.Exclude);
            Assert.AreEqual(1, values.Length, typeof(T).Name);
            return values[0];
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(GameSlice slice)
        {
            CharacterTestCharacterEntity entity = FindLocalPlayerOrNull(slice);
            if (entity != null)
                return entity;

            Assert.Fail("Expected CharacterTest local player entity.");
            return null;
        }

        private static CharacterTestCharacterEntity FindLocalPlayerOrNull(GameSlice slice)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return null;
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / TickRate);
        }

        private static CharacterTestMovableNaturalObjectSpawnSpec Spec(
            string stableId,
            int numericId,
            CombatSurfaceMaterial material,
            float massKg,
            float x,
            float z,
            CharacterTestMovableNaturalObjectShapeKind shapeKind = CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestGenesisPotencyTriplet basePotency = default,
            CharacterTestGenesisPotencyProfile potencyProfile = default)
        {
            return CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                stableId,
                numericId,
                CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                massKg,
                material,
                new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(x, 0.25f, z),
                shapeKind,
                basePotency,
                potencyProfile);
        }

        private static CharacterTestMovableNaturalObjectSpawnSpec SpecWithHalfExtents(
            string stableId,
            int numericId,
            CombatSurfaceMaterial material,
            float massKg,
            Vector3 halfExtents,
            float x,
            float z,
            CharacterTestMovableNaturalObjectShapeKind shapeKind = CharacterTestMovableNaturalObjectShapeKind.Aabb)
        {
            return SpecWithHalfExtentsAt(
                stableId,
                numericId,
                material,
                massKg,
                halfExtents,
                new Vector3(x, halfExtents.y, z),
                shapeKind);
        }

        private static CharacterTestMovableNaturalObjectSpawnSpec SpecWithHalfExtentsAt(
            string stableId,
            int numericId,
            CombatSurfaceMaterial material,
            float massKg,
            Vector3 halfExtents,
            Vector3 position,
            CharacterTestMovableNaturalObjectShapeKind shapeKind = CharacterTestMovableNaturalObjectShapeKind.Aabb)
        {
            return CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                stableId,
                numericId,
                CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                massKg,
                material,
                halfExtents,
                position,
                shapeKind);
        }

        private static IReadOnlyList<int> QueryBoundaryObjectContactSample(
            CharacterTestMovableNaturalObjectContactScratch scratch,
            CharacterTestCharacterRuntimeWorld world)
        {
            return QueryObjectContactSample(scratch, world);
        }

        private static IReadOnlyList<int> QueryObjectContactSample(
            CharacterTestMovableNaturalObjectContactScratch scratch,
            CharacterTestCharacterRuntimeWorld world)
        {
            scratch.PrepareObjectContactCandidates(world.MovableNaturalObjects);
            scratch.RebuildObjectContactIndex();
            return scratch.QueryObjectContactCandidateIndices(0, 1);
        }

        private static CharacterTestGenesisPotencyProfile BurningCrushProfile()
        {
            return CharacterTestGenesisPotencyProfile.WithDefaultThresholds(
                immuneMotion: false,
                immuneCohesion: false,
                immuneScatter: false,
                burningEnabled: true,
                crushEnabled: true,
                crackEnabled: false,
                condensedLayerEnabled: false);
        }

        private static CharacterTestGenesisPotencyProfile StoneCrackProfile()
        {
            return CharacterTestGenesisPotencyProfile.WithDefaultThresholds(
                immuneMotion: false,
                immuneCohesion: false,
                immuneScatter: true,
                burningEnabled: false,
                crushEnabled: false,
                crackEnabled: true,
                condensedLayerEnabled: false);
        }

        private static CharacterTestGenesisPotencyProfile CondenseProfile()
        {
            return CharacterTestGenesisPotencyProfile.WithDefaultThresholds(
                immuneMotion: false,
                immuneCohesion: false,
                immuneScatter: false,
                burningEnabled: false,
                crushEnabled: false,
                crackEnabled: false,
                condensedLayerEnabled: true);
        }

        private static bool IsPotencyProfileConfigured(CharacterTestGenesisPotencyProfile profile)
        {
            return profile.BurningEnabled
                || profile.CrushEnabled
                || profile.CrackEnabled
                || profile.CondensedLayerEnabled
                || profile.ImmuneMotion
                || profile.ImmuneCohesion
                || profile.ImmuneScatter;
        }

        private static CharacterTestWorldObjectDefinition TestDefinition(
            int definitionId,
            string stableId,
            CharacterTestMovableNaturalObjectFacet movableFacet)
        {
            return new CharacterTestWorldObjectDefinition(
                definitionId,
                stableId,
                stableId,
                CharacterTestWorldObjectKind.MovableNaturalObject,
                CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                new CharacterTestWorldObjectMaterialFacet(CombatSurfaceMaterial.HardSlick),
                CharacterTestWorldObjectShapeFacet.DefaultAabb,
                CharacterTestWorldObjectPotencyFacet.Disabled,
                movableFacet);
        }

        private static void AssertBuiltInMovableDefinition(
            CharacterTestWorldObjectDefinitionCatalog catalog,
            int definitionId,
            string stableId,
            CombatSurfaceMaterialKind materialKind,
            long frictionRaw,
            long elasticityRaw,
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            int massGrams,
            CharacterTestGenesisPotencyTriplet basePotency,
            bool expectsBurning,
            bool expectsCrack,
            bool expectsCondense)
        {
            Assert.IsTrue(catalog.TryGetDefinition(definitionId, out CharacterTestWorldObjectDefinition definition));
            Assert.IsTrue(catalog.TryGetDefinitionByStableId(stableId, out CharacterTestWorldObjectDefinition byStableId));
            Assert.AreEqual(definition, byStableId);
            Assert.IsTrue(definition.CanSpawnMovableNaturalObject);
            Assert.AreEqual(stableId, definition.StableId);
            Assert.AreEqual(materialKind, definition.Material.Kind);
            Assert.AreEqual(frictionRaw, definition.Material.Friction.RawValue);
            Assert.AreEqual(elasticityRaw, definition.Material.Elasticity.RawValue);
            Assert.AreEqual(shapeKind, definition.Shape.ShapeKind);
            Assert.AreEqual(massGrams, definition.MovableNaturalObject.MassGrams);
            Assert.AreEqual(basePotency, definition.Potency.BasePotency);
            Assert.AreEqual(expectsBurning, definition.Potency.Profile.BurningEnabled);
            Assert.AreEqual(expectsCrack, definition.Potency.Profile.CrackEnabled);
            Assert.AreEqual(expectsCondense, definition.Potency.Profile.CondensedLayerEnabled);
        }

        private static void AssertBuiltInStaticDefinition(
            CharacterTestWorldObjectDefinitionCatalog catalog,
            int definitionId,
            string stableId,
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            CombatSurfaceMaterialKind materialKind,
            long frictionRaw,
            long elasticityRaw)
        {
            Assert.IsTrue(catalog.TryGetDefinition(definitionId, out CharacterTestWorldObjectDefinition definition));
            Assert.IsTrue(definition.CanSpawnStaticCollider);
            Assert.AreEqual(stableId, definition.StableId);
            Assert.AreEqual(CharacterTestWorldObjectKind.StaticBoundary, definition.ObjectKind);
            Assert.AreEqual(shapeKind, definition.Shape.ShapeKind);
            Assert.AreEqual(materialKind, definition.Material.Kind);
            Assert.AreEqual(frictionRaw, definition.Material.Friction.RawValue);
            Assert.AreEqual(elasticityRaw, definition.Material.Elasticity.RawValue);
            Assert.AreEqual(1, definition.StaticCollider.CombatLayer);
            Assert.AreEqual(1, definition.StaticCollider.ColliderId);
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRatio(
                CharacterTestDeterministicUnits.MetersToMillimeters(value),
                CharacterTestDeterministicUnits.PositionScale);
        }
    }
}
