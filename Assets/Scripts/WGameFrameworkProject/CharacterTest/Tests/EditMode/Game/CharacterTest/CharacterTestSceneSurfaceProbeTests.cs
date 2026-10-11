using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSceneSurfaceProbeTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";
        private const int TickRate = CharacterTestDeterministicUnits.DefaultTickRate;
        private const float SlideStartSpeed = 4f;
        private const int SlideTicks = 30;
        private const float ReboundStartOffsetY = 0.15f;
        private const float ReboundImpactVelocityY = -20f;

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void CharacterTestScene_SurfaceProbeMarkersBuildRuntimeWorldWithMaterials()
        {
            CharacterTestCharacterPhysicsRuntime runtime = OpenSceneRuntime();

            Assert.NotNull(runtime);
            StringAssert.Contains("level=", runtime.SourceDescription);
            StringAssert.Contains("hash=", runtime.SourceDescription);
            AssertProbeMaterial(runtime, "SurfaceProbe_HardSlick", CombatSurfaceMaterial.HardSlick);
            AssertProbeMaterial(runtime, "SurfaceProbe_Rough", CombatSurfaceMaterial.Rough);
            AssertProbeMaterial(runtime, "SurfaceProbe_Elastic", CombatSurfaceMaterial.Elastic);
            AssertProbeMaterial(runtime, "SurfaceProbe_BouncePad", CombatSurfaceMaterial.BouncePad);
        }

        [Test]
        public void CharacterTestScene_HardSlickKeepsMoreMomentumThanRough()
        {
            CharacterTestCharacterPhysicsRuntime runtime = OpenSceneRuntime();

            SlideSample hardSlick = RunSlideSample(runtime, "SurfaceProbe_HardSlick", CombatSurfaceMaterialKind.HardSlick);
            SlideSample rough = RunSlideSample(runtime, "SurfaceProbe_Rough", CombatSurfaceMaterialKind.Rough);

            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, hardSlick.MaterialKind, hardSlick.ToString());
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, rough.MaterialKind, rough.ToString());
            Assert.Greater(
                hardSlick.FinalSpeed,
                rough.FinalSpeed + 0.5f,
                "HardSlick should preserve clearly more momentum than Rough. hard=" + hardSlick + " rough=" + rough);
            Assert.Greater(
                hardSlick.Distance,
                rough.Distance + 0.25f,
                "HardSlick should slide farther than Rough. hard=" + hardSlick + " rough=" + rough);
            Assert.Less(rough.FinalSpeed, 0.5f, rough.ToString());
        }

        [Test]
        public void CharacterTestScene_ElasticAndBouncePadReboundFromFallingImpact()
        {
            CharacterTestCharacterPhysicsRuntime runtime = OpenSceneRuntime();

            ReboundSample elastic = RunReboundSample(runtime, "SurfaceProbe_Elastic", CombatSurfaceMaterialKind.Elastic);
            ReboundSample bouncePad = RunReboundSample(runtime, "SurfaceProbe_BouncePad", CombatSurfaceMaterialKind.BouncePad);
            float expectedBouncePadVelocity = Mathf.Sqrt(
                2f
                * Mathf.Abs(CharacterTestCharacterMovementProfileSource.DefaultProfile.Gravity)
                * ToFloat(CombatSurfaceMaterial.BouncePad.FixedBounceHeight));

            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, elastic.MaterialKind, elastic.ToString());
            Assert.Greater(elastic.VerticalVelocity, 1f, elastic.ToString());
            Assert.LessOrEqual(elastic.VerticalVelocity, 20.01f, elastic.ToString());
            Assert.IsFalse(elastic.Grounded, elastic.ToString());

            Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, bouncePad.MaterialKind, bouncePad.ToString());
            Assert.AreEqual(expectedBouncePadVelocity, bouncePad.VerticalVelocity, 0.1f, bouncePad.ToString());
            Assert.IsFalse(bouncePad.Grounded, bouncePad.ToString());
        }

        [Test]
        public void CharacterTestScene_SphereAndCapsuleMarkersBuildRuntimeWorld()
        {
            CharacterTestCharacterPhysicsRuntime runtime = OpenSceneRuntime();
            SceneShapeProbe sphere = FindShapeProbe("PhysicsProbe_SphereTarget", CharacterTestMovableNaturalObjectShapeKind.Sphere);
            SceneShapeProbe capsule = FindShapeProbe("PhysicsProbe_CapsuleTarget", CharacterTestMovableNaturalObjectShapeKind.Capsule);

            Assert.NotNull(runtime);
            StringAssert.Contains("sceneShapeColliders=2", runtime.SourceDescription);
            Assert.Less(Vector3.Distance(sphere.Transform.position, Vector3.zero), 12f);
            Assert.Less(Vector3.Distance(capsule.Transform.position, Vector3.zero), 12f);
            Assert.IsTrue(runtime.World.TryGetSphereCollider(
                new CombatBodyId(sphere.BodyId),
                new CombatColliderId(sphere.ColliderId),
                out CombatPhysicsSphereCollider sphereCollider));
            Assert.AreEqual(sphere.Layer, sphereCollider.Layer);
            Assert.AreEqual(sphere.SurfaceMaterialKind, sphereCollider.SurfaceMaterialKind);
            Assert.IsTrue(runtime.World.TryGetCapsuleCollider(
                new CombatBodyId(capsule.BodyId),
                new CombatColliderId(capsule.ColliderId),
                out CombatPhysicsCapsuleCollider capsuleCollider));
            Assert.AreEqual(capsule.Layer, capsuleCollider.Layer);
            Assert.AreEqual(capsule.SurfaceMaterialKind, capsuleCollider.SurfaceMaterialKind);

            var hits = new List<CombatQueryResult>();
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask
                .FromLayer(sphere.Layer)
                .AddLayer(capsule.Layer);
            int count = runtime.World.Query(CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, mask),
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(new Vector3(5.25f, -1.5f, 0.75f)),
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(new Vector3(11.5f, 3.5f, 4.25f)))), hits);

            Assert.GreaterOrEqual(count, 2);
            AssertHasTargetShape(hits, sphere.BodyId, sphere.ColliderId, CombatPhysicsShapeKind.Sphere);
            AssertHasTargetShape(hits, capsule.BodyId, capsule.ColliderId, CombatPhysicsShapeKind.Capsule);
        }

        [Test]
        public void CharacterTestScene_ShapeProbeRuntimeQueriesHitStaticTargets()
        {
            CharacterTestCharacterPhysicsRuntime runtime = OpenSceneRuntime();
            SceneShapeProbe sphere = FindShapeProbe("PhysicsProbe_SphereTarget", CharacterTestMovableNaturalObjectShapeKind.Sphere);
            SceneShapeProbe capsule = FindShapeProbe("PhysicsProbe_CapsuleTarget", CharacterTestMovableNaturalObjectShapeKind.Capsule);

            AssertShapeQueryHits(runtime, sphere, CombatPhysicsShapeKind.Sphere);
            AssertShapeQueryHits(runtime, capsule, CombatPhysicsShapeKind.Capsule);
        }

        private static CharacterTestCharacterPhysicsRuntime OpenSceneRuntime()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "CharacterTestScene should open for surface probe tests.");
            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            Assert.AreEqual(1, managers.Length, "CharacterTestScene should contain one GameManager scene entry.");
            CharacterTestCharacterPhysicsRuntime runtime =
                CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                    managers[0].WorldObjectSceneRoot,
                    managers[0].WorldObjectSceneCache,
                    managers[0].PhysicsSettings);
            Assert.NotNull(runtime, "GameManager scene cache should build CharacterTest framework physics runtime.");
            return runtime;
        }

        private static void AssertProbeMaterial(
            CharacterTestCharacterPhysicsRuntime runtime,
            string markerName,
            CombatSurfaceMaterial expected)
        {
            SceneAabbProbe marker = FindAabbProbe(markerName);
            Assert.AreEqual(expected.Kind, marker.SurfaceMaterialKind, markerName);
            Assert.IsTrue(runtime.World.TryGetAabbCollider(
                new CombatBodyId(marker.BodyId),
                new CombatColliderId(marker.ColliderId),
                out CombatPhysicsAabbCollider collider), markerName);
            Assert.AreEqual(expected.Kind, collider.SurfaceMaterialKind, markerName);
            Assert.AreEqual(expected.StableId, collider.SurfaceMaterialStableId, markerName);
            Assert.AreEqual(expected.Friction, collider.SurfaceFriction, markerName);
            Assert.AreEqual(expected.Elasticity, collider.SurfaceElasticity, markerName);
            Assert.AreEqual(expected.FixedBounceHeight, collider.SurfaceFixedBounceHeight, markerName);
        }

        private static SlideSample RunSlideSample(
            CharacterTestCharacterPhysicsRuntime runtime,
            string markerName,
            CombatSurfaceMaterialKind expectedKind)
        {
            SceneAabbProbe marker = FindAabbProbe(markerName);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(runtime);
            CharacterTestCharacterRuntimeWorld world = CreateWorldAt(
                marker.Transform.position.x,
                marker.Transform.position.y + marker.HalfExtents.y,
                marker.Transform.position.z,
                out CharacterTestCharacterEntity entity);

            entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: true,
                movementMode: CharacterMovementMode.Run,
                physicsEnabled: true,
                collisionFlags: CombatMotionCollisionFlags.Grounded);
            entity.SetInput(CharacterTestCharacterInputComponent.None);
            motion.Tick(world, TickRate, new RuntimeFrame(0));
            Assert.AreEqual(expectedKind, entity.SurfaceMaterialKind, markerName + " acquire");

            float startZ = entity.Transform.Z;
            entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: true,
                horizontalVelocity: Vector2.up * SlideStartSpeed,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Run,
                physicsEnabled: true,
                collisionFlags: CombatMotionCollisionFlags.Grounded,
                surfaceMaterial: entity.SurfaceMaterial);
            for (int i = 0; i < SlideTicks; i++)
            {
                entity.SetInput(CharacterTestCharacterInputComponent.None);
                motion.Tick(world, TickRate, new RuntimeFrame(i + 1));
            }

            return new SlideSample(
                entity.SurfaceMaterialKind,
                entity.SurfaceMaterialStableId,
                entity.SurfaceFriction,
                entity.HorizontalVelocity.magnitude,
                entity.Transform.Z - startZ);
        }

        private static ReboundSample RunReboundSample(
            CharacterTestCharacterPhysicsRuntime runtime,
            string markerName,
            CombatSurfaceMaterialKind expectedKind)
        {
            SceneAabbProbe marker = FindAabbProbe(markerName);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(runtime);
            CharacterTestCharacterRuntimeWorld world = CreateWorldAt(
                marker.Transform.position.x,
                marker.Transform.position.y + marker.HalfExtents.y + ReboundStartOffsetY,
                marker.Transform.position.z,
                out CharacterTestCharacterEntity entity);

            entity.SetMotionState(
                ReboundImpactVelocityY,
                Vector2.zero,
                isGrounded: false,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Airborne,
                physicsEnabled: true);
            entity.SetInput(CharacterTestCharacterInputComponent.None);
            motion.Tick(world, TickRate, RuntimeFrame.Zero);
            Assert.AreEqual(expectedKind, entity.SurfaceMaterialKind, markerName);

            return new ReboundSample(
                entity.SurfaceMaterialKind,
                entity.SurfaceMaterialStableId,
                entity.VerticalVelocity,
                entity.IsGrounded,
                entity.Transform.Y);
        }

        private static SceneAabbProbe FindAabbProbe(string markerName)
        {
            CharacterTestWorldObjectSceneCache cache = FindGameManagerCache();
            if (cache != null && cache.Enabled)
            {
                CharacterTestWorldObjectStaticColliderSpec[] specs = cache.CollectStaticColliderSpecs(out _);
                for (int i = 0; i < specs.Length; i++)
                {
                    CharacterTestWorldObjectStaticColliderSpec spec = specs[i];
                    if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb
                        && spec.Transform != null
                        && spec.Transform.name == markerName)
                    {
                        return SceneAabbProbe.FromSpec(spec);
                    }
                }
            }

            CharacterTestWorldObjectSceneSource[] sources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < sources.Length; i++)
            {
                CharacterTestWorldObjectSceneSource source = sources[i];
                if (source == null || !source.Enabled)
                    continue;

                CharacterTestWorldObjectStaticColliderSpec[] specs = source.CollectStaticColliderSpecs(out _);
                for (int j = 0; j < specs.Length; j++)
                {
                    CharacterTestWorldObjectStaticColliderSpec spec = specs[j];
                    if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb
                        && spec.Transform != null
                        && spec.Transform.name == markerName)
                    {
                        return SceneAabbProbe.FromSpec(spec);
                    }
                }
            }

            CharacterTestCombatPhysicsAabbMarker[] markers =
                Object.FindObjectsByType<CharacterTestCombatPhysicsAabbMarker>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < markers.Length; i++)
            {
                CharacterTestCombatPhysicsAabbMarker marker = markers[i];
                if (marker != null && marker.name == markerName)
                    return SceneAabbProbe.FromMarker(marker);
            }

            Assert.Fail("CharacterTestScene surface marker was not found: " + markerName);
            return default;
        }

        private static SceneShapeProbe FindShapeProbe(
            string markerName,
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            CharacterTestWorldObjectSceneCache cache = FindGameManagerCache();
            if (cache != null && cache.Enabled)
            {
                CharacterTestWorldObjectStaticColliderSpec[] specs = cache.CollectStaticColliderSpecs(out _);
                for (int i = 0; i < specs.Length; i++)
                {
                    CharacterTestWorldObjectStaticColliderSpec spec = specs[i];
                    if (spec.ShapeKind == shapeKind
                        && spec.Transform != null
                        && spec.Transform.name == markerName)
                    {
                        return SceneShapeProbe.FromSpec(spec);
                    }
                }
            }

            CharacterTestWorldObjectSceneSource[] sources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < sources.Length; i++)
            {
                CharacterTestWorldObjectSceneSource source = sources[i];
                if (source == null || !source.Enabled)
                    continue;

                CharacterTestWorldObjectStaticColliderSpec[] specs = source.CollectStaticColliderSpecs(out _);
                for (int j = 0; j < specs.Length; j++)
                {
                    CharacterTestWorldObjectStaticColliderSpec spec = specs[j];
                    if (spec.ShapeKind == shapeKind
                        && spec.Transform != null
                        && spec.Transform.name == markerName)
                    {
                        return SceneShapeProbe.FromSpec(spec);
                    }
                }
            }

            if (shapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere)
            {
                CharacterTestCombatPhysicsSphereMarker[] sphereMarkers =
                    Object.FindObjectsByType<CharacterTestCombatPhysicsSphereMarker>(UnityEngine.FindObjectsInactive.Exclude);
                for (int i = 0; i < sphereMarkers.Length; i++)
                {
                    CharacterTestCombatPhysicsSphereMarker marker = sphereMarkers[i];
                    if (marker != null && marker.name == markerName)
                        return SceneShapeProbe.FromSphereMarker(marker);
                }
            }

            if (shapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule)
            {
                CharacterTestCombatPhysicsCapsuleMarker[] capsuleMarkers =
                    Object.FindObjectsByType<CharacterTestCombatPhysicsCapsuleMarker>(UnityEngine.FindObjectsInactive.Exclude);
                for (int i = 0; i < capsuleMarkers.Length; i++)
                {
                    CharacterTestCombatPhysicsCapsuleMarker marker = capsuleMarkers[i];
                    if (marker != null && marker.name == markerName)
                        return SceneShapeProbe.FromCapsuleMarker(marker);
                }
            }

            Assert.Fail("CharacterTestScene shape probe was not found: " + markerName);
            return default;
        }

        private static CharacterTestWorldObjectSceneCache FindGameManagerCache()
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(UnityEngine.FindObjectsInactive.Exclude);
            return managers.Length == 1 ? managers[0].WorldObjectSceneCache : null;
        }

        private static void AssertShapeQueryHits(
            CharacterTestCharacterPhysicsRuntime runtime,
            SceneShapeProbe probe,
            CombatPhysicsShapeKind expectedShape)
        {
            var hits = new List<CombatQueryResult>();
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(probe.Layer);
            Vector3 center = probe.Transform.position;
            int count = runtime.World.Query(CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, mask),
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(center - Vector3.one * 0.1f),
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(center + Vector3.one * 0.1f))), hits);

            Assert.Greater(count, 0, probe.Transform.name);
            AssertHasTargetShape(hits, probe.BodyId, probe.ColliderId, expectedShape);
        }

        private static void AssertHasTargetShape(
            IReadOnlyList<CombatQueryResult> hits,
            int bodyId,
            int colliderId,
            CombatPhysicsShapeKind targetShapeKind)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                CombatQueryResult hit = hits[i];
                if (hit.TargetBodyId.Value == bodyId && hit.TargetColliderId.Value == colliderId)
                {
                    Assert.AreEqual(targetShapeKind, hit.TargetShapeKind);
                    return;
                }
            }

            Assert.Fail(
                "Expected hit for body "
                + bodyId
                + " collider "
                + colliderId
                + " shape "
                + targetShapeKind);
        }

        private static CombatQueryHeader Header(
            CombatQueryKind kind,
            CombatPhysicsLayerMask layerMask)
        {
            return new CombatQueryHeader(
                queryId: 1,
                kind,
                new CombatEntityId(1),
                traceId: 5,
                actionId: 6,
                sourceOrder: 7,
                layerMask);
        }

        private static CharacterTestCharacterRuntimeWorld CreateWorldAt(
            float x,
            float y,
            float z,
            out CharacterTestCharacterEntity entity)
        {
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(definitions.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));
            var world = new CharacterTestCharacterRuntimeWorld();
            entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    x,
                    y,
                    z,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            return world;
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }

        private readonly struct SlideSample
        {
            public SlideSample(
                CombatSurfaceMaterialKind materialKind,
                string materialStableId,
                float friction,
                float finalSpeed,
                float distance)
            {
                MaterialKind = materialKind;
                MaterialStableId = materialStableId ?? string.Empty;
                Friction = friction;
                FinalSpeed = finalSpeed;
                Distance = distance;
            }

            public CombatSurfaceMaterialKind MaterialKind { get; }
            public string MaterialStableId { get; }
            public float Friction { get; }
            public float FinalSpeed { get; }
            public float Distance { get; }

            public override string ToString()
            {
                return "material=" + MaterialKind
                    + "/" + MaterialStableId
                    + " friction=" + Friction.ToString("0.###")
                    + " finalSpeed=" + FinalSpeed.ToString("0.###")
                    + " distance=" + Distance.ToString("0.###");
            }
        }

        private readonly struct SceneAabbProbe
        {
            private SceneAabbProbe(
                Transform transform,
                int bodyId,
                int colliderId,
                Vector3 halfExtents,
                CombatSurfaceMaterial surfaceMaterial)
            {
                Transform = transform;
                BodyId = bodyId;
                ColliderId = colliderId;
                HalfExtents = halfExtents;
                SurfaceMaterial = surfaceMaterial;
            }

            public Transform Transform { get; }
            public int BodyId { get; }
            public int ColliderId { get; }
            public Vector3 HalfExtents { get; }
            public CombatSurfaceMaterial SurfaceMaterial { get; }
            public CombatSurfaceMaterialKind SurfaceMaterialKind => SurfaceMaterial.Kind;

            public static SceneAabbProbe FromSpec(CharacterTestWorldObjectStaticColliderSpec spec)
            {
                return new SceneAabbProbe(
                    spec.Transform,
                    spec.BodyId,
                    spec.ColliderId,
                    spec.HalfExtents,
                    spec.SurfaceMaterial);
            }

            public static SceneAabbProbe FromMarker(CharacterTestCombatPhysicsAabbMarker marker)
            {
                return new SceneAabbProbe(
                    marker.transform,
                    marker.BodyId,
                    marker.ColliderId,
                    marker.HalfExtents,
                    marker.SurfaceMaterial);
            }
        }

        private readonly struct SceneShapeProbe
        {
            private SceneShapeProbe(
                Transform transform,
                int bodyId,
                int colliderId,
                int layer,
                CombatSurfaceMaterialKind surfaceMaterialKind)
            {
                Transform = transform;
                BodyId = bodyId;
                ColliderId = colliderId;
                Layer = layer;
                SurfaceMaterialKind = surfaceMaterialKind;
            }

            public Transform Transform { get; }
            public int BodyId { get; }
            public int ColliderId { get; }
            public int Layer { get; }
            public CombatSurfaceMaterialKind SurfaceMaterialKind { get; }

            public static SceneShapeProbe FromSpec(CharacterTestWorldObjectStaticColliderSpec spec)
            {
                return new SceneShapeProbe(
                    spec.Transform,
                    spec.BodyId,
                    spec.ColliderId,
                    spec.Layer,
                    spec.SurfaceMaterial.Kind);
            }

            public static SceneShapeProbe FromSphereMarker(CharacterTestCombatPhysicsSphereMarker marker)
            {
                return new SceneShapeProbe(
                    marker.transform,
                    marker.BodyId,
                    marker.ColliderId,
                    marker.CombatLayer,
                    marker.SurfaceMaterialKind);
            }

            public static SceneShapeProbe FromCapsuleMarker(CharacterTestCombatPhysicsCapsuleMarker marker)
            {
                return new SceneShapeProbe(
                    marker.transform,
                    marker.BodyId,
                    marker.ColliderId,
                    marker.CombatLayer,
                    marker.SurfaceMaterialKind);
            }
        }

        private readonly struct ReboundSample
        {
            public ReboundSample(
                CombatSurfaceMaterialKind materialKind,
                string materialStableId,
                float verticalVelocity,
                bool grounded,
                float y)
            {
                MaterialKind = materialKind;
                MaterialStableId = materialStableId ?? string.Empty;
                VerticalVelocity = verticalVelocity;
                Grounded = grounded;
                Y = y;
            }

            public CombatSurfaceMaterialKind MaterialKind { get; }
            public string MaterialStableId { get; }
            public float VerticalVelocity { get; }
            public bool Grounded { get; }
            public float Y { get; }

            public override string ToString()
            {
                return "material=" + MaterialKind
                    + "/" + MaterialStableId
                    + " verticalVelocity=" + VerticalVelocity.ToString("0.###")
                    + " grounded=" + Grounded
                    + " y=" + Y.ToString("0.###");
            }
        }
    }
}
