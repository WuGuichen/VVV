using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLargeCharacterPresenceRuntimeTests
    {
        [Test]
        public void IdentityAndLayerMasks_AreCheckedBodyScopedAndIsolated()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();

            Assert.AreEqual(29999, CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumEntityId);
            Assert.IsTrue(physics.TryGetBodyPresenceBodyId(1, out CombatBodyId firstBody));
            Assert.AreEqual(100001, firstBody.Value);
            Assert.IsTrue(physics.TryGetBodyPresenceBodyId(29999, out CombatBodyId lastBody));
            Assert.AreEqual(129999, lastBody.Value);
            Assert.IsFalse(physics.TryGetBodyPresenceBodyId(0, out _));
            Assert.IsFalse(physics.TryGetBodyPresenceBodyId(30000, out _));
            Assert.IsTrue(CharacterTestCharacterPhysicsRuntime.TryGetBodyPresenceColliderId(0, out CombatColliderId firstCollider));
            Assert.AreEqual(1, firstCollider.Value);
            Assert.IsTrue(CharacterTestCharacterPhysicsRuntime.TryGetBodyPresenceColliderId(3, out CombatColliderId lastCollider));
            Assert.AreEqual(4, lastCollider.Value);
            Assert.IsFalse(CharacterTestCharacterPhysicsRuntime.TryGetBodyPresenceColliderId(4, out _));

            Assert.IsTrue(physics.BodyPresenceLayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            Assert.IsFalse(physics.StaticObstacleLayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            Assert.IsFalse(physics.MotionObstacleLayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            Assert.IsFalse(physics.DynamicCharacterObstacleLayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            Assert.IsFalse(physics.DynamicMovableObjectObstacleLayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            CombatPhysicsLayerMask hitMask = CharacterTestCombatHitEventResolver.ResolveQueryLayerMask(
                new CombatHitEventConfig(
                    CombatHitTargetTypes.HostileCharacter
                    | CombatHitTargetTypes.DamageableObject
                    | CombatHitTargetTypes.WorldStatic),
                physics);
            Assert.IsFalse(hitMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            var invalidPhysics = new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "test.invalid-presence-mask");
            var invalidSystem = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity invalidEntity = CreateEntity(1, 0f, 0f, 0f, 0f);
            Assert.IsFalse(invalidSystem.SyncAll(
                new[] { invalidEntity },
                invalidPhysics,
                new[] { CreateBinding(1, Sphere("a")) }));
            Assert.AreEqual(0, invalidPhysics.World.BodyCount);
        }

        [Test]
        public void SyncAll_ProjectsAllShapesStableOrdinalsRootPoseAndDirtyGate()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var system = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity entity = CreateEntity(1, 1f, 2f, 3f, 90f);
            CharacterImportedGeometryBinding binding = CreateBinding(
                1,
                CreateVolume(
                    "a.sphere",
                    CharacterBodyPresenceShape.Sphere,
                    new CharacterRuntimeVector3(1f, 0f, 0f),
                    radius: 0.5f,
                    skinMillimeters: 10),
                CreateVolume(
                    "b.disabled",
                    CharacterBodyPresenceShape.Sphere,
                    new CharacterRuntimeVector3(0f, 0f, 0f),
                    radius: 0.25f,
                    enabled: false),
                CreateVolume(
                    "c.capsule",
                    CharacterBodyPresenceShape.Capsule,
                    new CharacterRuntimeVector3(0f, 1f, 0f),
                    radius: 0.25f,
                    height: 2f,
                    pressureScaleMilli: 0),
                CreateVolume(
                    "d.box",
                    CharacterBodyPresenceShape.Box,
                    new CharacterRuntimeVector3(0f, 0f, 1f),
                    size: new CharacterRuntimeVector3(2f, 4f, 6f),
                    skinMillimeters: 100));
            CharacterTestCharacterEntity[] entities = { entity };
            CharacterImportedGeometryBinding[] bindings = { binding };

            Assert.IsTrue(system.SyncAll(entities, physics, bindings));
            Assert.IsTrue(physics.TryGetBodyPresenceBodyId(entity, out CombatBodyId bodyId));
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out CombatPhysicsBody body));
            Assert.AreEqual(new FixVector3(Meters(1f), Meters(2f), Meters(3f)), body.Position);
            Assert.AreEqual(3, physics.World.CompoundLeafCount);

            var spheres = new List<CombatPhysicsSphereCollider>();
            var capsules = new List<CombatPhysicsCapsuleCollider>();
            var boxes = new List<CombatPhysicsObbCollider>();
            Assert.AreEqual(1, physics.World.CopySphereCollidersTo(spheres));
            Assert.AreEqual(1, physics.World.CopyCapsuleCollidersTo(capsules));
            Assert.AreEqual(1, physics.World.CopyObbCollidersTo(boxes));
            Assert.AreEqual(1, spheres[0].ColliderId.Value);
            Assert.AreEqual(3, capsules[0].ColliderId.Value);
            Assert.AreEqual(4, boxes[0].ColliderId.Value);
            Assert.AreEqual(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer, spheres[0].Layer);
            AssertFixVectorNear(
                new FixVector3(Fix64.Zero, Fix64.Zero, -Fix64.One),
                spheres[0].LocalCenter,
                toleranceRaw: 4);
            Assert.AreEqual(Meters(0.51f), spheres[0].Radius);

            int unchangedRevision = physics.World.Revision;
            Assert.IsTrue(system.SyncAll(entities, physics, bindings));
            Assert.AreEqual(unchangedRevision, physics.World.Revision);

            entity.SetTransform(new CharacterTestCharacterTransformComponent(2f, 3f, 4f, 0f));
            Assert.IsTrue(system.SyncAll(entities, physics, bindings));
            Assert.Greater(physics.World.Revision, unchangedRevision);
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out body));
            Assert.AreEqual(new FixVector3(Meters(2f), Meters(3f), Meters(4f)), body.Position);
            spheres.Clear();
            physics.World.CopySphereCollidersTo(spheres);
            AssertFixVectorNear(
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                spheres[0].LocalCenter,
                toleranceRaw: 4);
        }

        [Test]
        public void Lifecycle_MissingDisableDespawnAndRestoreRebuildAreDerived()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var system = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity entity = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterImportedGeometryBinding authored = CreateBinding(
                1,
                CreateVolume(
                    "a.sphere",
                    CharacterBodyPresenceShape.Sphere,
                    new CharacterRuntimeVector3(0f, 1f, 0f),
                    radius: 1f));
            CharacterTestCharacterEntity[] entities = { entity };

            Assert.IsTrue(system.SyncAll(entities, physics, new[] { authored }));
            Assert.IsTrue(physics.TryGetBodyPresenceBodyId(entity, out CombatBodyId bodyId));
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out _));

            CharacterImportedGeometryBinding disabled = CreateBinding(2);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { disabled }));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));
            Assert.AreEqual(1, physics.SyncedBodyPresenceColliderCount);

            CharacterImportedGeometryBinding restored = CreateBinding(
                3,
                CreateVolume(
                    "a.sphere",
                    CharacterBodyPresenceShape.Sphere,
                    new CharacterRuntimeVector3(0f, 1f, 0f),
                    radius: 1f));
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { restored }));
            int beforeRebuild = physics.World.Revision;
            Assert.IsTrue(system.RebuildAll(entities, physics, new[] { restored }));
            Assert.Greater(physics.World.Revision, beforeRebuild);
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out _));

            Assert.IsTrue(system.SyncAll(
                Array.Empty<CharacterTestCharacterEntity>(),
                physics,
                Array.Empty<CharacterImportedGeometryBinding>()));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);

            Assert.IsTrue(system.SyncAll(entities, physics, new[] { restored }));
            Assert.IsTrue(system.SyncAll(
                entities,
                physics,
                new CharacterImportedGeometryBinding[] { null }));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);
        }

        [Test]
        public void BindingVersions_V4ThroughV7AuthoredSyncWhileStrictCompatibilityUsesMissing()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var system = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity entity = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterTestCharacterEntity[] entities = { entity };
            CharacterBodyPresenceRuntimeBinding[] volumes = { Sphere("a") };

            CharacterImportedGeometryBinding v4 = CreateBinding(
                "mx.characterGeometryBinding.v4",
                "4.0",
                1,
                Hash(1),
                volumes);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { v4 }));
            Assert.IsTrue(physics.TryGetBodyPresenceBodyId(entity, out CombatBodyId bodyId));
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out _));

            CharacterImportedGeometryBinding v5 = CreateBinding(
                "mx.characterGeometryBinding.v5",
                "5.0",
                2,
                Hash(2),
                volumes);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { v5 }));
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out _));

            CharacterImportedGeometryBinding v6 = CreateBinding(
                "mx.characterGeometryBinding.v6",
                "6.0",
                3,
                Hash(3),
                volumes);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { v6 }));
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out _));

            CharacterImportedGeometryBinding v7 = CreateBinding(
                "mx.characterGeometryBinding.v7",
                "7.0",
                4,
                Hash(4),
                volumes);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { v7 }));
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out _));

            CharacterImportedGeometryBinding compatibility = CreateBinding(
                "mx.characterGeometryBinding.v5",
                "5.0",
                revision: 0,
                hash: string.Empty,
                volumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                mode: CharacterBodyPresenceMode.CompatibilityHardRoot);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { compatibility }));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);

            CharacterImportedGeometryBinding invalidCompatibility = CreateBinding(
                "mx.characterGeometryBinding.v5",
                "5.0",
                revision: 1,
                hash: string.Empty,
                volumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                mode: CharacterBodyPresenceMode.CompatibilityHardRoot);
            Assert.IsFalse(system.SyncAll(entities, physics, new[] { invalidCompatibility }));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);

            CharacterImportedGeometryBinding v6Compatibility = CreateBinding(
                "mx.characterGeometryBinding.v6",
                "6.0",
                revision: 0,
                hash: string.Empty,
                volumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                mode: CharacterBodyPresenceMode.CompatibilityHardRoot);
            Assert.IsTrue(system.SyncAll(entities, physics, new[] { v6Compatibility }));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);

            CharacterImportedGeometryBinding invalidV6Compatibility = CreateBinding(
                "mx.characterGeometryBinding.v6",
                "6.0",
                revision: 1,
                hash: string.Empty,
                volumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>(),
                mode: CharacterBodyPresenceMode.CompatibilityHardRoot);
            Assert.IsFalse(system.SyncAll(entities, physics, new[] { invalidV6Compatibility }));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);
        }

        [Test]
        public void InvalidFutureStaleCapacityAndOutOfRangeRoster_FailClosedBeforeMutation()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var system = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity entity = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterImportedGeometryBinding revisionTwo = CreateBinding(
                2,
                CreateVolume(
                    "a.sphere",
                    CharacterBodyPresenceShape.Sphere,
                    new CharacterRuntimeVector3(0f, 0f, 0f),
                    radius: 1f));
            CharacterTestCharacterEntity[] oneEntity = { entity };

            Assert.IsTrue(system.SyncAll(oneEntity, physics, new[] { revisionTwo }));
            Assert.IsTrue(physics.TryGetBodyPresenceBodyId(entity, out CombatBodyId bodyId));
            CharacterTestCharacterEntity[] outOfRangeRoster =
            {
                entity,
                CreateEntity(30000, 0f, 0f, 0f, 0f)
            };
            Assert.IsFalse(system.SyncAll(
                outOfRangeRoster,
                physics,
                new CharacterImportedGeometryBinding[] { revisionTwo, null }));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));

            CharacterImportedGeometryBinding future = CreateBinding(
                "mx.characterGeometryBinding.v8",
                "8.0",
                3,
                Hash(3),
                revisionTwo.BodyPresenceVolumes);
            Assert.IsFalse(system.SyncAll(oneEntity, physics, new[] { future }));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));

            Assert.IsTrue(system.SyncAll(oneEntity, physics, new[] { revisionTwo }));
            CharacterImportedGeometryBinding stale = CreateBinding(
                1,
                revisionTwo.BodyPresenceVolumes);
            Assert.IsFalse(system.SyncAll(oneEntity, physics, new[] { stale }));
            Assert.IsFalse(physics.World.TryGetBody(bodyId, out _));

            CharacterBodyPresenceRuntimeBinding[] fiveVolumes =
            {
                Sphere("a"), Sphere("b"), Sphere("c"), Sphere("d"), Sphere("e")
            };
            Assert.IsFalse(system.SyncAll(oneEntity, physics, new[] { CreateBinding(4, fiveVolumes) }));
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);

            CharacterTestCharacterPhysicsRuntime occupiedPhysics = CreatePhysics();
            var occupiedSystem = new CharacterTestCharacterBodyPresenceSystem();
            Assert.IsTrue(occupiedPhysics.TryGetBodyPresenceBodyId(entity, out CombatBodyId occupiedBodyId));
            occupiedPhysics.World.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(777),
                occupiedBodyId,
                FixVector3.Zero));
            Assert.IsFalse(occupiedSystem.SyncAll(oneEntity, occupiedPhysics, new[] { revisionTwo }));
            Assert.IsTrue(occupiedPhysics.World.TryGetBody(occupiedBodyId, out CombatPhysicsBody foreignBody));
            Assert.AreEqual(777, foreignBody.EntityId.Value);
        }

        [Test]
        public void EnvelopeAndEntityOrder_KeepEightByFourBodyScopedIdentityAndRejectNinth()
        {
            CharacterTestCharacterEntity[] entities = new CharacterTestCharacterEntity[8];
            CharacterImportedGeometryBinding[] bindings = new CharacterImportedGeometryBinding[8];
            CharacterImportedGeometryBinding fourLeaves = CreateBinding(1, CreateFourSphereVolumes());
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = CreateEntity(i + 1, i, 0f, 0f, 0f);
                bindings[i] = fourLeaves;
            }

            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var system = new CharacterTestCharacterBodyPresenceSystem();
            Assert.IsTrue(system.SyncAll(entities, physics, bindings));
            Assert.AreEqual(8, physics.World.CompoundCount);
            Assert.AreEqual(32, physics.World.CompoundLeafCount);
            var colliders = new List<CombatPhysicsSphereCollider>();
            Assert.AreEqual(32, physics.World.CopySphereCollidersTo(colliders));
            int[] baselineKeys = CopyColliderKeys(colliders);
            Assert.AreEqual(1000010 + 1, baselineKeys[0]);
            Assert.AreEqual(1000080 + 4, baselineKeys[31]);

            Array.Reverse(entities);
            Array.Reverse(bindings);
            Assert.IsTrue(system.RebuildAll(entities, physics, bindings));
            colliders.Clear();
            physics.World.CopySphereCollidersTo(colliders);
            CollectionAssert.AreEqual(baselineKeys, CopyColliderKeys(colliders));

            var ninthEntities = new CharacterTestCharacterEntity[9];
            var ninthBindings = new CharacterImportedGeometryBinding[9];
            Array.Copy(entities, ninthEntities, entities.Length);
            Array.Copy(bindings, ninthBindings, bindings.Length);
            ninthEntities[8] = CreateEntity(9, 0f, 0f, 0f, 0f);
            ninthBindings[8] = fourLeaves;
            Assert.IsFalse(system.SyncAll(ninthEntities, physics, ninthBindings));
            Assert.AreEqual(0, physics.World.CompoundCount);
            Assert.AreEqual(0, physics.SyncedBodyPresenceColliderCount);
        }

        [Test]
        public void UnchangedSync_AfterWarmup_AllocatesZeroBytes()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var system = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity[] entities = { CreateEntity(1, 0f, 0f, 0f, 0f) };
            CharacterImportedGeometryBinding[] bindings =
            {
                CreateBinding(1, CreateFourSphereVolumes())
            };
            Assert.IsTrue(system.SyncAll(entities, physics, bindings));
            for (int i = 0; i < 8; i++)
                Assert.IsTrue(system.SyncAll(entities, physics, bindings));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allSucceeded = true;
            for (int i = 0; i < 4096; i++)
                allSucceeded &= system.SyncAll(entities, physics, bindings);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(allSucceeded);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysics()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "test.body-presence");
        }

        private static CharacterTestCharacterEntity CreateEntity(
            int id,
            float x,
            float y,
            float z,
            float yawDegrees)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    characterId: 1,
                    displayName: "Presence Test Character",
                    prefabKey: default(ResourceKey),
                    animationBinding: default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId: 1, defaultTeamId: 1),
                new CharacterTestCharacterTransformComponent(x, y, z, yawDegrees),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            int revision,
            params CharacterBodyPresenceRuntimeBinding[] volumes)
        {
            return CreateBinding(
                "mx.characterGeometryBinding.v4",
                "4.0",
                revision,
                Hash(revision),
                volumes);
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            string format,
            string schemaVersion,
            int revision,
            string hash,
            CharacterBodyPresenceRuntimeBinding[] volumes,
            CharacterBodyPresenceMode mode = CharacterBodyPresenceMode.Authored)
        {
            return new CharacterImportedGeometryBinding(
                format,
                schemaVersion,
                "test.body-presence",
                null,
                Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                Array.Empty<CharacterHitZoneRuntimeBinding>(),
                Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                Array.Empty<CharacterSocketRuntimeBinding>(),
                Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPresenceVolumes: volumes,
                bodyPresenceRevision: revision,
                bodyPresenceContentHash: hash,
                bodyPresenceMode: mode);
        }

        private static CharacterBodyPresenceRuntimeBinding CreateVolume(
            string volumeId,
            CharacterBodyPresenceShape shape,
            CharacterRuntimeVector3 position,
            float radius = 0f,
            float height = 0f,
            CharacterRuntimeVector3 size = default,
            int skinMillimeters = 0,
            int pressureScaleMilli = 1000,
            bool enabled = true)
        {
            return new CharacterBodyPresenceRuntimeBinding(
                volumeId,
                CharacterBodyPresenceReferenceFrame.ModelRoot,
                shape,
                new CharacterRuntimePose(
                    "ModelRoot",
                    string.Empty,
                    position,
                    new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                    new CharacterRuntimeVector3(1f, 1f, 1f)),
                radius,
                height,
                size,
                skinMillimeters,
                pressureScaleMilli,
                enabled);
        }

        private static CharacterBodyPresenceRuntimeBinding Sphere(string volumeId)
        {
            return CreateVolume(
                volumeId,
                CharacterBodyPresenceShape.Sphere,
                new CharacterRuntimeVector3(0f, 0f, 0f),
                radius: 0.5f);
        }

        private static CharacterBodyPresenceRuntimeBinding[] CreateFourSphereVolumes()
        {
            return new[]
            {
                Sphere("a"),
                Sphere("b"),
                Sphere("c"),
                Sphere("d")
            };
        }

        private static int[] CopyColliderKeys(List<CombatPhysicsSphereCollider> colliders)
        {
            var keys = new int[colliders.Count];
            for (int i = 0; i < colliders.Count; i++)
                keys[i] = checked(colliders[i].BodyId.Value * 10 + colliders[i].ColliderId.Value);
            return keys;
        }

        private static string Hash(int revision)
        {
            char digit = (char)('0' + revision % 10);
            return "sha256:" + new string(digit, 64);
        }

        private static Fix64 Meters(float value)
        {
            return CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                CharacterTestDeterministicUnits.MetersToMillimeters(value));
        }

        private static void AssertFixVectorNear(
            FixVector3 expected,
            FixVector3 actual,
            long toleranceRaw)
        {
            Assert.LessOrEqual(Math.Abs(expected.X.RawValue - actual.X.RawValue), toleranceRaw);
            Assert.LessOrEqual(Math.Abs(expected.Y.RawValue - actual.Y.RawValue), toleranceRaw);
            Assert.LessOrEqual(Math.Abs(expected.Z.RawValue - actual.Z.RawValue), toleranceRaw);
        }
    }
}
