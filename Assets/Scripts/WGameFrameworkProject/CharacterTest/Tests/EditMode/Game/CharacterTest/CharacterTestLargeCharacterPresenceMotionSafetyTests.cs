using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLargeCharacterPresenceMotionSafetyTests
    {
        [Test]
        public void TranslationPressureScaleMassPushabilityAndZeroLimit_AreBounded()
        {
            SurfaceRunResult full = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000);
            SurfaceRunResult half = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 500);
            SurfaceRunResult opposingIntent = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                sourceAttemptedVelocityX: -6000);
            SurfaceRunResult heavy = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                sourceMassScale: 4f);
            SurfaceRunResult light = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                sourceMassScale: 0.25f);
            SurfaceRunResult approaching = RunSurfacePair(
                sourcePreviousXMillimeters: 0,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                targetAttemptedVelocityX: -1000);
            SurfaceRunResult unpushable = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                targetCanBePushed: false);
            SurfaceRunResult zeroLimit = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                targetAttemptedVelocityX: -1000,
                runSpeed: 0,
                acceleration: 0);

            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, full.Status);
            Assert.Greater(full.TargetVelocityX, 0);
            Assert.Less(half.TargetVelocityX, full.TargetVelocityX);
            Assert.AreEqual(full.TargetVelocityX, opposingIntent.TargetVelocityX,
                "Presence surface motion is derived from actual root delta, not attempted input or a double count.");
            Assert.Greater(heavy.TargetVelocityX, light.TargetVelocityX);
            Assert.Greater(approaching.TargetVelocityX, 0,
                "A Hard Root walking into static Presence receives an outward next-tick velocity.");
            Assert.Greater(unpushable.TargetContactCount, 0);
            Assert.AreEqual(0, unpushable.TargetVelocityX);
            Assert.Greater(zeroLimit.TargetContactCount, 0);
            Assert.AreEqual(0, zeroLimit.TargetVelocityX);
            Assert.AreEqual(0, zeroLimit.SourceVelocityX,
                "A zero profile limit must not fall back to the legacy unbounded reverse path.");
            Assert.LessOrEqual(Math.Abs(full.TargetVelocityX), 6100);
            Assert.IsTrue(full.TransformsUnchanged);
        }

        [Test]
        public void YawNinetyHalfTurnAndWrap_UseShortestAngleAndOmegaCrossRadiusSign()
        {
            SurfaceRunResult positiveQuarter = RunYawPair(
                previousYawMilliDegrees: -90000,
                currentYawMilliDegrees: 0,
                targetXMillimeters: 1000,
                targetZMillimeters: -1000);
            SurfaceRunResult negativeQuarter = RunYawPair(
                previousYawMilliDegrees: 90000,
                currentYawMilliDegrees: 0,
                targetXMillimeters: 1000,
                targetZMillimeters: 1000);
            SurfaceRunResult positiveHalfTurnTie = RunYawPair(
                previousYawMilliDegrees: 0,
                currentYawMilliDegrees: 180000,
                targetXMillimeters: -1000,
                targetZMillimeters: 1000);
            SurfaceRunResult positiveWrap = RunYawPair(
                previousYawMilliDegrees: 350000,
                currentYawMilliDegrees: 10000,
                targetXMillimeters: 810,
                targetZMillimeters: -1160);

            Assert.Less(positiveQuarter.TargetVelocityZ, 0);
            Assert.Greater(negativeQuarter.TargetVelocityZ, 0);
            Assert.Greater(positiveHalfTurnTie.TargetVelocityZ, 0,
                "The existing ShortestAngleDelta +180 degree tie remains positive.");
            Assert.Less(positiveWrap.TargetVelocityX, 0);
            Assert.Less(positiveWrap.TargetVelocityZ, 0);
            Assert.LessOrEqual(positiveQuarter.TargetSpeed, 6100);
            Assert.LessOrEqual(negativeQuarter.TargetSpeed, 6100);
            Assert.LessOrEqual(positiveHalfTurnTie.TargetSpeed, 6100);
            Assert.LessOrEqual(positiveWrap.TargetSpeed, 6100);
        }

        [Test]
        public void InvalidRotationWitnessSameCenterAndPresenceOff_PreserveFailureBoundaries()
        {
            SurfaceRunResult firstFrame = RunYawPair(
                previousYawMilliDegrees: -90000,
                currentYawMilliDegrees: 0,
                targetXMillimeters: 1000,
                targetZMillimeters: -1040,
                rotationWitnessValid: false);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, firstFrame.Status);
            Assert.AreEqual(0, firstFrame.TargetVelocityX);
            Assert.AreEqual(0, firstFrame.TargetVelocityZ,
                "Spawn/restore/binding-switch/teleport rotation baselines contribute zero.");

            SurfaceRunResult sameCenter = RunSurfacePair(
                sourcePreviousXMillimeters: 0,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 0,
                pressureScaleMilli: 1000,
                localSphereXMillimeters: 0);
            Assert.Greater(sameCenter.TargetContactCount, 0);
            Assert.LessOrEqual(sameCenter.TargetSpeed, 6100);
            Assert.IsTrue(sameCenter.TransformsUnchanged);

            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            CharacterTestCharacterEntity source = CreateEntity(1, 0, 0, 0);
            CharacterTestCharacterEntity target = CreateEntity(2, 500, 0, 0);
            CharacterTestCharacterEntity[] entities = { source, target };
            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.RecordAttemptedContactVelocity(source.Id.Value, 1000, 0);
            resolver.ResolveCharacterContactPressure(
                entities,
                physics,
                bodyPresenceReady: false,
                bodyPresenceMotionSamples: null,
                bodyPresenceMotionSampleCount: 0,
                bodyPresenceMotionReady: false,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared: 6000,
                turnSpeedMilliDegreesPerSecond: 10800000,
                tickRate: 60);
            resolver.ApplyCharacterContactState(entities);

            Assert.AreEqual(
                CharacterTestBodyPresenceContactBuildStatus.SourceUnavailable,
                resolver.LastBodyPresenceContactBuildStatus);
            Assert.Greater(target.Physics.ContactVelocityXMillimetersPerSecond, 0,
                "Presence-off keeps the existing Hard Root pressure behavior.");
        }

        [Test]
        public void RosterAndSampleOrderPerturbation_ReplaysIdentically()
        {
            SurfaceRunResult baseline = RunSurfacePair(
                sourcePreviousXMillimeters: -100,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters: 2000,
                pressureScaleMilli: 1000,
                reverseOrder: false);
            for (int i = 0; i < 16; i++)
            {
                SurfaceRunResult replay = RunSurfacePair(
                    sourcePreviousXMillimeters: -100,
                    sourceCurrentXMillimeters: 0,
                    targetXMillimeters: 2000,
                    pressureScaleMilli: 1000,
                    reverseOrder: (i & 1) != 0);
                Assert.AreEqual(baseline.Status, replay.Status);
                Assert.AreEqual(baseline.TargetVelocityX, replay.TargetVelocityX);
                Assert.AreEqual(baseline.TargetVelocityZ, replay.TargetVelocityZ);
                Assert.AreEqual(baseline.TargetContactCount, replay.TargetContactCount);
            }
        }

        [Test]
        public void PresenceVelocity_IsConsumedByNextTickStaticWallSweepWithoutDirectDisplacement()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateWallPhysics();
            var world = new CharacterTestCharacterRuntimeWorld();
            var definition = new CharacterTestCharacterDefinition(
                characterId: 1,
                displayName: "Presence Wall Safety",
                prefabKey: default(ResourceKey),
                defaultTeamId: 1,
                animationBinding: default(CharacterRuntimeAnimationBinding),
                physicsProfile: CharacterTestCharacterPhysicsComponent.Default);
            CharacterTestCharacterEntity source = world.Spawn(
                CharacterTestCharacterSpawnRequest.FromDeterministic(
                    1, 1, 0, 0, 0, CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity target = world.Spawn(
                CharacterTestCharacterSpawnRequest.FromDeterministic(
                    1, 1, 1400, 0, 0, CharacterTestCharacterControlOwner.None),
                definition);
            source.SetMotionStateDeterministic(0, 0, 0, isGrounded: true);
            target.SetMotionStateDeterministic(0, 0, 0, isGrounded: true);
            CharacterImportedGeometryBinding sourceBinding = CreateBinding(
                1,
                SphereAt("body", 1000, 0, 700, 1000));
            CharacterImportedGeometryBinding[] bindings = { sourceBinding, null };
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            Assert.IsTrue(presence.SyncAll(world.Entities, physics, bindings));
            CharacterTestBodyPresenceMotionSample[] samples =
            {
                CreateSample(source, -100, 0, 0, sourceBinding, true),
                CreateSample(target, 1400, 0, 0, null, false)
            };
            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.ResolveCharacterContactPressure(
                world.Entities,
                physics,
                bodyPresenceReady: true,
                samples,
                samples.Length,
                bodyPresenceMotionReady: true,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared: 6000,
                turnSpeedMilliDegreesPerSecond: 10800000,
                tickRate: 60);
            resolver.ApplyCharacterContactState(world.Entities);

            Assert.Greater(target.Physics.ContactVelocityXMillimetersPerSecond, 0);
            Assert.AreEqual(1400, target.Transform.XMillimeters,
                "Presence pressure must not directly displace the authority root.");

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(physics);
            motion.Tick(world, tickRate: 60);

            Assert.Greater(target.Transform.XMillimeters, 1400);
            Assert.LessOrEqual(target.Transform.XMillimeters, 1660,
                "The next-tick static sweep keeps the capsule outside the wall at x=2m.");
        }

        [Test]
        public void WarmedEightByFourSurfaceConstraintPressurePath_AllocatesZeroBytes()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            var resolver = new CharacterTestCharacterContactResolver(null);
            CharacterTestCharacterEntity[] entities = new CharacterTestCharacterEntity[8];
            CharacterImportedGeometryBinding[] bindings = new CharacterImportedGeometryBinding[8];
            CharacterTestBodyPresenceMotionSample[] samples =
                new CharacterTestBodyPresenceMotionSample[8];
            CharacterImportedGeometryBinding binding = CreateBinding(
                1,
                SphereAt("a", 0, 0, 20000, 1000),
                SphereAt("b", 0, 0, 20000, 1000),
                SphereAt("c", 0, 0, 20000, 1000),
                SphereAt("d", 0, 0, 20000, 1000));
            for (int i = 0; i < entities.Length; i++)
            {
                int x = i * 2000;
                entities[i] = CreateEntity(i + 1, x, 0, 0);
                bindings[i] = binding;
                samples[i] = CreateSample(entities[i], x - 10, 0, 0, binding, true);
            }

            Assert.IsTrue(presence.SyncAll(entities, physics, bindings));
            for (int i = 0; i < 8; i++)
                RunSurfaceFrame(resolver, entities, physics, samples);
            Assert.AreEqual(28, resolver.LastBodyPresencePairCount);
            Assert.AreEqual(224, resolver.LastBodyPresenceConstraintCount);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allSucceeded = true;
            for (int i = 0; i < 128; i++)
            {
                RunSurfaceFrame(resolver, entities, physics, samples);
                allSucceeded &= resolver.LastBodyPresenceContactBuildStatus
                    == CharacterTestBodyPresenceContactBuildStatus.Success;
                allSucceeded &= resolver.LastBodyPresencePairCount == 28;
                allSucceeded &= resolver.LastBodyPresenceConstraintCount == 224;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(allSucceeded);
            Assert.AreEqual(0L, allocated);
        }

        private static SurfaceRunResult RunYawPair(
            int previousYawMilliDegrees,
            int currentYawMilliDegrees,
            int targetXMillimeters,
            int targetZMillimeters,
            bool rotationWitnessValid = true)
        {
            return RunSurfacePair(
                sourcePreviousXMillimeters: 0,
                sourceCurrentXMillimeters: 0,
                targetXMillimeters,
                pressureScaleMilli: 1000,
                sourcePreviousZMillimeters: 0,
                sourceCurrentZMillimeters: 0,
                targetZMillimeters,
                sourcePreviousYawMilliDegrees: previousYawMilliDegrees,
                sourceCurrentYawMilliDegrees: currentYawMilliDegrees,
                rotationWitnessValid: rotationWitnessValid);
        }

        private static SurfaceRunResult RunSurfacePair(
            int sourcePreviousXMillimeters,
            int sourceCurrentXMillimeters,
            int targetXMillimeters,
            int pressureScaleMilli,
            int sourcePreviousZMillimeters = 0,
            int sourceCurrentZMillimeters = 0,
            int targetZMillimeters = 0,
            int sourcePreviousYawMilliDegrees = 0,
            int sourceCurrentYawMilliDegrees = 0,
            int sourceAttemptedVelocityX = 0,
            int targetAttemptedVelocityX = 0,
            bool targetCanBePushed = true,
            float sourceMassScale = 1f,
            int runSpeed = 6000,
            int acceleration = 6000,
            int localSphereXMillimeters = 1000,
            bool rotationWitnessValid = true,
            bool reverseOrder = false)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity source = CreateEntity(
                1,
                sourceCurrentXMillimeters,
                sourceCurrentZMillimeters,
                sourceCurrentYawMilliDegrees);
            CharacterTestCharacterEntity target = CreateEntity(
                2,
                targetXMillimeters,
                targetZMillimeters,
                0);
            SetPhysics(source, sourceMassScale, canBePushed: true);
            SetPhysics(target, 1f, targetCanBePushed);
            CharacterImportedGeometryBinding sourceBinding = CreateBinding(
                1,
                SphereAt("body", localSphereXMillimeters, 0, 700, pressureScaleMilli));
            CharacterTestCharacterEntity[] entities = { source, target };
            CharacterImportedGeometryBinding[] bindings = { sourceBinding, null };
            CharacterTestBodyPresenceMotionSample[] samples =
            {
                CreateSample(
                    source,
                    sourcePreviousXMillimeters,
                    sourcePreviousZMillimeters,
                    sourcePreviousYawMilliDegrees,
                    sourceBinding,
                    rotationWitnessValid),
                CreateSample(target, targetXMillimeters, targetZMillimeters, 0, null, false)
            };
            if (reverseOrder)
            {
                Array.Reverse(entities);
                Array.Reverse(bindings);
                Array.Reverse(samples);
            }

            Assert.IsTrue(presence.SyncAll(entities, physics, bindings));
            int sourceXBefore = source.Transform.XMillimeters;
            int sourceZBefore = source.Transform.ZMillimeters;
            int targetXBefore = target.Transform.XMillimeters;
            int targetZBefore = target.Transform.ZMillimeters;
            var resolver = new CharacterTestCharacterContactResolver(null);
            if (sourceAttemptedVelocityX != 0)
                resolver.RecordAttemptedContactVelocity(source.Id.Value, sourceAttemptedVelocityX, 0);
            if (targetAttemptedVelocityX != 0)
                resolver.RecordAttemptedContactVelocity(target.Id.Value, targetAttemptedVelocityX, 0);
            resolver.ResolveCharacterContactPressure(
                entities,
                physics,
                bodyPresenceReady: true,
                samples,
                samples.Length,
                bodyPresenceMotionReady: true,
                runSpeed,
                acceleration,
                turnSpeedMilliDegreesPerSecond: 10800000,
                tickRate: 60);
            resolver.ApplyCharacterContactState(entities);
            return new SurfaceRunResult(
                resolver.LastBodyPresenceContactBuildStatus,
                source.Physics.ContactVelocityXMillimetersPerSecond,
                source.Physics.ContactVelocityYMillimetersPerSecond,
                target.Physics.ContactVelocityXMillimetersPerSecond,
                target.Physics.ContactVelocityYMillimetersPerSecond,
                target.Physics.ContactCount,
                source.Transform.XMillimeters == sourceXBefore
                    && source.Transform.ZMillimeters == sourceZBefore
                    && target.Transform.XMillimeters == targetXBefore
                    && target.Transform.ZMillimeters == targetZBefore);
        }

        private static void RunSurfaceFrame(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterEntity[] entities,
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestBodyPresenceMotionSample[] samples)
        {
            resolver.Clear();
            resolver.ResolveCharacterContactPressure(
                entities,
                physics,
                bodyPresenceReady: true,
                samples,
                samples.Length,
                bodyPresenceMotionReady: true,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared: 6000,
                turnSpeedMilliDegreesPerSecond: 10800000,
                tickRate: 60);
            resolver.ApplyCharacterContactState(entities);
        }

        private static CharacterTestBodyPresenceMotionSample CreateSample(
            CharacterTestCharacterEntity entity,
            int previousXMillimeters,
            int previousZMillimeters,
            int previousYawMilliDegrees,
            CharacterImportedGeometryBinding binding,
            bool rotationWitnessValid)
        {
            return new CharacterTestBodyPresenceMotionSample(
                entity.Id.Value,
                previousXMillimeters,
                entity.Transform.YMillimeters,
                previousZMillimeters,
                previousYawMilliDegrees,
                entity.Transform.XMillimeters,
                entity.Transform.YMillimeters,
                entity.Transform.ZMillimeters,
                entity.Transform.YawMilliDegrees,
                binding,
                rotationWitnessValid);
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
                sourceDescription: "test.body-presence-motion-safety");
        }

        private static CharacterTestCharacterPhysicsRuntime CreateWallPhysics()
        {
            var world = new CombatPhysicsWorld();
            RegisterStaticAabb(
                world,
                entityId: 100,
                bodyIdValue: 100,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
            RegisterStaticAabb(
                world,
                entityId: 101,
                bodyIdValue: 101,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(3), Fix64.FromInt(2)));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 2,
                sourceDescription: "test.body-presence-next-tick-wall");
        }

        private static void RegisterStaticAabb(
            CombatPhysicsWorld world,
            int entityId,
            int bodyIdValue,
            FixVector3 localMin,
            FixVector3 localMax)
        {
            var bodyId = new CombatBodyId(bodyIdValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entityId),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin,
                localMax));
        }

        private static CharacterTestCharacterEntity CreateEntity(
            int id,
            int xMillimeters,
            int zMillimeters,
            int yawMilliDegrees)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    characterId: 1,
                    displayName: "Presence Motion Safety Test Character",
                    prefabKey: default(ResourceKey),
                    animationBinding: default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId: 1, defaultTeamId: 1),
                CharacterTestCharacterTransformComponent.FromDeterministic(
                    xMillimeters,
                    0,
                    zMillimeters,
                    yawMilliDegrees),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static void SetPhysics(
            CharacterTestCharacterEntity entity,
            float massScale,
            bool canBePushed)
        {
            CharacterTestCharacterPhysicsComponent physics = entity.Physics.WithRuntimeModifiers(
                massScale,
                radiusScale: 1f,
                pushScale: 1f,
                canBePushed: canBePushed);
            entity.SetPhysics(physics);
            entity.SetColliderPosture(CharacterTestColliderPostureComponent.FromPhysics(physics, frame: 0L));
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            int revision,
            params CharacterBodyPresenceRuntimeBinding[] volumes)
        {
            return new CharacterImportedGeometryBinding(
                "mx.characterGeometryBinding.v4",
                "4.0",
                "test.body-presence-motion-safety",
                null,
                Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                Array.Empty<CharacterHitZoneRuntimeBinding>(),
                Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                Array.Empty<CharacterSocketRuntimeBinding>(),
                Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPresenceVolumes: volumes,
                bodyPresenceRevision: revision,
                bodyPresenceContentHash: Hash(revision),
                bodyPresenceMode: CharacterBodyPresenceMode.Authored);
        }

        private static CharacterBodyPresenceRuntimeBinding SphereAt(
            string volumeId,
            int xMillimeters,
            int zMillimeters,
            int radiusMillimeters,
            int pressureScaleMilli)
        {
            return new CharacterBodyPresenceRuntimeBinding(
                volumeId,
                CharacterBodyPresenceReferenceFrame.ModelRoot,
                CharacterBodyPresenceShape.Sphere,
                new CharacterRuntimePose(
                    "ModelRoot",
                    string.Empty,
                    new CharacterRuntimeVector3(
                        xMillimeters / 1000f,
                        0.93f,
                        zMillimeters / 1000f),
                    new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                    new CharacterRuntimeVector3(1f, 1f, 1f)),
                radiusMillimeters / 1000f,
                height: 0f,
                size: default,
                skinMillimeters: 0,
                pressureScaleMilli,
                enabled: true);
        }

        private static string Hash(int revision)
        {
            char digit = (char)('0' + revision % 10);
            return "sha256:" + new string(digit, 64);
        }

        private readonly struct SurfaceRunResult
        {
            public SurfaceRunResult(
                CharacterTestBodyPresenceContactBuildStatus status,
                int sourceVelocityX,
                int sourceVelocityZ,
                int targetVelocityX,
                int targetVelocityZ,
                int targetContactCount,
                bool transformsUnchanged)
            {
                Status = status;
                SourceVelocityX = sourceVelocityX;
                SourceVelocityZ = sourceVelocityZ;
                TargetVelocityX = targetVelocityX;
                TargetVelocityZ = targetVelocityZ;
                TargetContactCount = targetContactCount;
                TransformsUnchanged = transformsUnchanged;
            }

            public CharacterTestBodyPresenceContactBuildStatus Status { get; }
            public int SourceVelocityX { get; }
            public int SourceVelocityZ { get; }
            public int TargetVelocityX { get; }
            public int TargetVelocityZ { get; }
            public int TargetContactCount { get; }
            public bool TransformsUnchanged { get; }
            public int TargetSpeed => CharacterTestDeterministicMath.Magnitude(
                TargetVelocityX,
                TargetVelocityZ);
        }
    }
}
