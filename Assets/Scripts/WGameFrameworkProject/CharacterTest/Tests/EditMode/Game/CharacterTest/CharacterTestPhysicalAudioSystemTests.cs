using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestPhysicalAudioSystemTests
    {
        private const int GroundLayer = 1;

        [Test]
        public void CharacterTestAudioCueCatalog_Default_ResolvesPhysicalAudioCues()
        {
            MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog catalog =
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.CreateDefault();

            Assert.IsTrue(catalog.TryGetCue(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactDefaultCueKey,
                out AudioCueDefinition soleContact));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactDefaultCueId,
                soleContact.Id);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactDefaultEventId,
                soleContact.EventId);
            Assert.IsTrue(catalog.TryGetEvent(soleContact.EventId, out AudioEventDefinition soleContactEvent));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactDefaultEventPath,
                soleContactEvent.FmodEventPath);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioEventGuid,
                soleContactEvent.FmodEventGuid);
            Assert.IsTrue(soleContactEvent.Is3D);
            Assert.IsTrue(catalog.TryGetParameter(
                soleContact.EventId,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId,
                out AudioParameterDefinition volumeParameter));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterName,
                volumeParameter.Name);
            Assert.IsTrue(catalog.TryGetParameter(
                soleContact.EventId,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioPitchParameterId,
                out AudioParameterDefinition pitchParameter));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioPitchParameterName,
                pitchParameter.Name);

            Assert.IsTrue(catalog.TryGetCue(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingDefaultCueKey,
                out AudioCueDefinition landing));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingDefaultCueId,
                landing.Id);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingDefaultEventId,
                landing.EventId);
            Assert.IsTrue(catalog.TryGetEvent(landing.EventId, out AudioEventDefinition landingEvent));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingDefaultEventPath,
                landingEvent.FmodEventPath);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionDefaultEventPath,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingDefaultEventPath);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionRoughEventPath,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingRoughEventPath);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionHardSlickEventPath,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingHardSlickEventPath);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionElasticEventPath,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.LandingElasticEventPath);
            Assert.IsTrue(landingEvent.Is3D);

            Assert.IsTrue(catalog.TryGetCue(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionDefaultCueKey,
                out AudioCueDefinition collision));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionDefaultCueId,
                collision.Id);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionDefaultEventId,
                collision.EventId);
            Assert.IsTrue(catalog.TryGetEvent(collision.EventId, out AudioEventDefinition collisionEvent));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionDefaultEventPath,
                collisionEvent.FmodEventPath);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioEventGuid,
                collisionEvent.FmodEventGuid);
            Assert.IsTrue(collisionEvent.Is3D);
            Assert.IsTrue(catalog.TryGetParameter(
                collision.EventId,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioScrapeParameterId,
                out AudioParameterDefinition scrapeParameter));
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioScrapeParameterName,
                scrapeParameter.Name);
        }

        [TestCase(CombatPhysicalContactKind.BodyPartContact, 600, PhysicalAudioInteractionKind.Footstep, PhysicalAudioInteractionMode.Step)]
        [TestCase(CombatPhysicalContactKind.BodyPartContact, 900, PhysicalAudioInteractionKind.Footstep, PhysicalAudioInteractionMode.Step)]
        [TestCase(CombatPhysicalContactKind.Landing, 0, PhysicalAudioInteractionKind.Landing, PhysicalAudioInteractionMode.Landing)]
        [TestCase(CombatPhysicalContactKind.ObjectImpact, 500, PhysicalAudioInteractionKind.ObjectImpact, PhysicalAudioInteractionMode.Impact)]
        public void PhysicalFactBuilder_UsesCommonBodyPartContactFactAndKeepsExplicitLandingCompatibility(
            CombatPhysicalContactKind contactKind,
            int normalSpeed,
            PhysicalAudioInteractionKind expectedKind,
            PhysicalAudioInteractionMode expectedMode)
        {
            CombatPhysicalContactEvent contactEvent = CreatePhysicalContactEvent(
                contactKind,
                CombatSurfaceMaterial.HardSlick,
                normalSpeed);

            bool success = MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioFactBuilder.TryCreate(
                contactEvent,
                out PhysicalAudioInteractionFact fact);

            Assert.IsTrue(success);
            Assert.AreEqual(expectedKind, fact.Kind);
            Assert.AreEqual(expectedMode, fact.Mode);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAcousticMaterialMapper.Stone,
                fact.Receiver.MaterialId);
            Assert.AreEqual(0f, fact.ContactNormal.X, 0.0001f);
            Assert.AreEqual(1f, fact.ContactNormal.Y, 0.0001f);
            Assert.AreEqual(0f, fact.ContactNormal.Z, 0.0001f);
            Assert.AreEqual(0f, fact.Transform.ForwardX, 0.0001f);
            Assert.AreEqual(0f, fact.Transform.ForwardY, 0.0001f);
            Assert.AreEqual(1f, fact.Transform.ForwardZ, 0.0001f);
        }

        [Test]
        public void PhysicalAudio_InitiallyGroundedContactOnlyEstablishesBaseline()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);
            Tick(system, world, physicsRuntime, 0);

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(2, system.LastSummary.ContactDynamicsBaselineSuppressCount);
            Assert.AreEqual(0, system.LastSummary.ContactPointEstimatedCount);
            Assert.AreEqual(0, system.LastSummary.SolverVelocityDeltaCount);
        }

        [Test]
        public void PhysicalAudio_LeftFootProbeContactEdgePlaysOnceAtHitPoint()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);
            Assert.AreEqual(2, system.LastSummary.BodyPartContactPlayedCount);
            Assert.AreEqual(1, system.LastSummary.LeftFootContactCount);
            Assert.AreEqual(1, system.LastSummary.RightFootContactCount);
            Assert.AreEqual(2, system.LastSummary.ContactPointEstimatedCount);
            Assert.AreEqual(0, system.LastSummary.SolverVelocityDeltaCount);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(system.BodyPartContactDebugSnapshot, contactPointId: 1);
            Assert.AreEqual(
                PhysicalAudioContactDynamicsSource.ContactPointEstimated,
                left.DynamicsSource);
            Assert.Greater(left.DynamicsNormalSpeedMetersPerSecond, 0f);
            Assert.Greater(left.NormalImpulseNewtonSeconds, 0f);
            Assert.Greater(left.AveragePressurePascals, 0f);

            Tick(system, world, physicsRuntime, 2);
            Tick(system, world, physicsRuntime, 3);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactDefaultEventId,
                audioService.LastPlayRequest.EventId);
            AssertAnyRequestMatchesContactPoint(entity, audioService, contactPointId: 1);
            AssertAnyRequestMatchesContactPoint(entity, audioService, contactPointId: 2);
        }

        [Test]
        public void PhysicalAudio_FasterFootDescentProducesHigherImpulseAndVolume()
        {
            float slowVolume = ResolveFootstepDropVolume(
                dropHeightMillimeters: 10,
                out float slowImpulse);
            float fastVolume = ResolveFootstepDropVolume(
                dropHeightMillimeters: 100,
                out float fastImpulse);

            Assert.Greater(fastImpulse, slowImpulse);
            Assert.Greater(fastVolume, slowVolume);
        }

        [Test]
        public void PhysicalAudio_MovablePlatformUsesFootAndPlatformRelativeVelocity()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                MxFramework.Game.CharacterTest.CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.footstep.platform",
                    numericId: 1,
                    MxFramework.Game.CharacterTest.CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    massKg: 40f,
                    CombatSurfaceMaterial.Default,
                    new UnityEngine.Vector3(10f, 0.11f, 10f),
                    new UnityEngine.Vector3(0f, -0.09f, 0f))
            });
            world.MovableNaturalObjects[0].SetVelocity(
                velocityXMillimetersPerSecond: 2000,
                velocityZMillimetersPerSecond: 0);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateMovablePlatformPhysicsRuntime();
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);

            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(system.BodyPartContactDebugSnapshot, contactPointId: 1);
            Assert.AreEqual(
                new CombatBodyId(
                    MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime
                        .DynamicMovableObjectBodyIdStart + 1),
                left.TargetBodyId);
            Assert.AreEqual(
                PhysicalAudioContactDynamicsSource.ContactPointEstimated,
                left.DynamicsSource);
            Assert.AreEqual(2f, left.DynamicsTangentialSpeedMetersPerSecond, 0.001f);
            Assert.AreEqual(0, system.LastSummary.DynamicTargetFallbackCount);
        }

        [Test]
        public void PhysicalAudio_TranslationWithoutPhysicalRecontactDoesNotReplay()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 300);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            MoveCharacterX(entity, 3600);
            Tick(system, world, physicsRuntime, 2);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
        }

        [Test]
        public void PhysicalAudio_RightFootContactUsesRightHitPoint()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateRightFootOnlyPhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactDefaultEventId,
                audioService.LastPlayRequest.EventId);
            AssertLastRequestMatchesContactPoint(
                entity,
                audioService.LastPlayRequest,
                contactPointId: 2,
                minHitXMillimeters: 50,
                maxHitXMillimeters: 250,
                minHitZMillimeters: -220,
                maxHitZMillimeters: -20);
        }

        [Test]
        public void PhysicalAudio_DebugSnapshotRecordsProbeCenterAndHitPoint()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot =
                system.BodyPartContactDebugSnapshot;
            Assert.GreaterOrEqual(snapshot.Count, 2);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(snapshot, contactPointId: 1);
            MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig config =
                FindContactPointConfig(entity, contactPointId: 1);

            Assert.IsTrue(left.Active);
            Assert.IsTrue(left.HasHit);
            Assert.IsTrue(left.ContactAllowed);
            Assert.IsTrue(left.AudioQueued);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, left.Phase);
            Assert.AreEqual(config.LocalOffsetXMillimeters / 1000f, ToFloatMeters(left.Center.X), 0.005f);
            Assert.AreEqual(config.LocalOffsetYMillimeters / 1000f, ToFloatMeters(left.Center.Y), 0.005f);
            Assert.AreEqual(config.LocalOffsetZMillimeters / 1000f, ToFloatMeters(left.Center.Z), 0.005f);
            Assert.AreEqual(config.RadiusMillimeters / 1000f, ToFloatMeters(left.Radius), 0.005f);
            AudioPlayRequest leftRequest = FindRequestMatchingContactPoint(entity, audioService, contactPointId: 1);
            Assert.AreEqual(leftRequest.Transform.X, ToFloatMeters(left.HitPoint.X), 0.005f);
            Assert.AreEqual(leftRequest.Transform.Y, ToFloatMeters(left.HitPoint.Y), 0.005f);
            Assert.AreEqual(leftRequest.Transform.Z, ToFloatMeters(left.HitPoint.Z), 0.005f);
            Assert.AreEqual(1200, left.TangentialSpeedMillimetersPerSecond);
            Assert.Greater(left.Pressure, 0);
        }

        [Test]
        public void PhysicalAudio_BakedSocketResolvedCenterDrivesProbe()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(
                    out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                    MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: true));

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot =
                system.BodyPartContactDebugSnapshot;
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(snapshot, contactPointId: 1);
            MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig config =
                FindContactPointConfig(entity, contactPointId: 1);

            Assert.AreEqual(
                MxFramework.CharacterApplication.CharacterBodyPartContactMotionMode.BakedSocket,
                left.MotionMode);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactMotionState.BakedSocket,
                left.MotionState);
            Assert.AreEqual("foot.L", left.MotionSocketId);
            Assert.GreaterOrEqual(system.LastSummary.BodyPartContactPlayedCount, 1);
            Assert.GreaterOrEqual(system.LastSummary.BakedMotionResolvedCount, 2);
            Assert.AreEqual(0, system.LastSummary.MotionFallbackCount);
            Assert.AreEqual(
                0.25f + (config.LocalOffsetXMillimeters / 1000f),
                ToFloatMeters(left.Center.X),
                0.005f);
            Assert.AreEqual(
                config.LocalOffsetYMillimeters / 1000f,
                ToFloatMeters(left.Center.Y),
                0.005f);
            Assert.AreEqual(
                0.35f + (config.LocalOffsetZMillimeters / 1000f),
                ToFloatMeters(left.Center.Z),
                0.005f);
        }

        [Test]
        public void PhysicalAudio_TickUsesPrecompiledSocketTrackWithoutLegacyStringLookupOrAllocation()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(
                    out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                    MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system = CreateSystem(audioService);
            var source = new FakeBakedMotionSource(hasSocketFrame: true, throwOnLegacyLookup: true);
            system.SetBakedMotionSource(source);
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            system.RefreshContactRegistration(entity, physicsRuntime);
            system.Tick(world, new RuntimeFrame(0), 60, physicsRuntime);
            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();

            system.Tick(world, new RuntimeFrame(1), 60, physicsRuntime);

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, source.LegacySocketLookupCount);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void PhysicalAudio_PresentationPoseProviderOverridesBakedSocket()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(
                    out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                    MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: true));
            system.SetPresentationPoseProvider(new FakePresentationPoseProvider());

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            Tick(system, world, physicsRuntime, 0);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot =
                system.BodyPartContactDebugSnapshot;
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(snapshot, contactPointId: 1);

            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactMotionState.PresentationPose,
                left.MotionState);
            Assert.GreaterOrEqual(system.LastSummary.PresentationMotionResolvedCount, 2);
            Assert.AreEqual(0, system.LastSummary.BakedMotionResolvedCount);
            Assert.AreEqual(0.42f, ToFloatMeters(left.Center.X), 0.005f);
            Assert.AreEqual(0.06f, ToFloatMeters(left.Center.Y), 0.005f);
            Assert.AreEqual(0.18f, ToFloatMeters(left.Center.Z), 0.005f);
        }

        [Test]
        public void PhysicalAudio_PresentationContactPointRetriggersOnlyAfterLiftAndRecontact()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(
                    out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                    MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);
            var poseProvider = new MutablePresentationPoseProvider();
            system.SetPresentationPoseProvider(poseProvider);
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);

            Tick(system, world, physicsRuntime, 0);
            Assert.AreEqual(0, audioService.PlayOneShotCount);

            poseProvider.LeftYMillimeters = 60;
            Tick(system, world, physicsRuntime, 1);
            Tick(system, world, physicsRuntime, 2);
            Assert.AreEqual(1, audioService.PlayOneShotCount);

            poseProvider.LeftYMillimeters = 400;
            Tick(system, world, physicsRuntime, 3);
            for (int frame = 4; frame < 10; frame++)
                Tick(system, world, physicsRuntime, frame);
            poseProvider.LeftYMillimeters = 60;
            Tick(system, world, physicsRuntime, 10);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(system.BodyPartContactDebugSnapshot, contactPointId: 1);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, left.Phase);
            Assert.IsTrue(left.AudioQueued);
        }

        [Test]
        public void PhysicalAudio_BakedSocketMissingFrameRecordsFallback()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(
                    out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                    MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: false));

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            Tick(system, world, physicsRuntime, 0);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot =
                system.BodyPartContactDebugSnapshot;
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(snapshot, contactPointId: 1);

            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactMotionState.MissingFrame,
                left.MotionState);
            Assert.GreaterOrEqual(system.LastSummary.MissingMotionFrameCount, 2);
            Assert.GreaterOrEqual(system.LastSummary.MotionFallbackCount, 2);
        }

        [Test]
        public void PhysicalAudio_DebugSnapshotRecordsProbeMissWithoutAudioQueue()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default, groundUnderProbe: false);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            Tick(system, world, physicsRuntime, 0);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot =
                system.BodyPartContactDebugSnapshot;
            Assert.GreaterOrEqual(snapshot.Count, 2);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(snapshot, contactPointId: 1);

            Assert.IsTrue(left.Active);
            Assert.IsFalse(left.HasHit);
            Assert.IsFalse(left.ContactAllowed);
            Assert.IsFalse(left.AudioQueued);
            Assert.AreEqual(CombatPhysicalContactPhase.None, left.Phase);
            Assert.AreEqual(0, audioService.PlayOneShotCount);
        }

        [Test]
        public void PhysicalAudio_AirborneOrProbeMissDoesNotPlaySoleContact()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: false, horizontalSpeedMillimetersPerSecond: 1200);
            MoveCharacterY(entity, 1000);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            Tick(system, world, physicsRuntime, 0);
            Tick(system, world, physicsRuntime, 1);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            MoveCharacterY(entity, 0);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime missPhysicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default, groundUnderProbe: false);
            MoveCharacterX(entity, 1200);
            Tick(system, world, missPhysicsRuntime, 2);

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.GreaterOrEqual(system.LastSummary.FootProbeMissCount, 2);
        }

        [Test]
        public void PhysicalAudio_LowSpeedGroundedPhysicalContactPlaysSoleContact()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 300);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(1, system.LastSummary.LeftFootContactCount);
            Assert.AreEqual(1, system.LastSummary.RightFootContactCount);
        }

        [Test]
        public void PhysicalAudio_BodyPartContactRuntimeStateIsPerCharacter()
        {
            const string MissingPackageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/missing_physical_audio_test";
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity first);
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity missingConfig =
                SpawnCharacter(
                    world,
                    teamId: 1,
                    x: 2f,
                    generatedPackageRoot: MissingPackageRoot);
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity second =
                SpawnCharacter(world, teamId: 1, x: -2f);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(first, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            SetMotion(missingConfig, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            SetMotion(second, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);

            for (int i = 0; i < 8; i++)
                Tick(system, world, physicsRuntime, i);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot warmSnapshot =
                system.BodyPartContactDebugSnapshot;
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(warmSnapshot, first.Id.Value, contactPointId: 1).Phase);
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(warmSnapshot, second.Id.Value, contactPointId: 1).Phase);
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(warmSnapshot, first.Id.Value, contactPointId: 2).Phase);
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(warmSnapshot, second.Id.Value, contactPointId: 2).Phase);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++)
                Tick(system, world, physicsRuntime, 100 + i);
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot =
                system.BodyPartContactDebugSnapshot;
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(snapshot, first.Id.Value, contactPointId: 1).Phase);
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(snapshot, second.Id.Value, contactPointId: 1).Phase);
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(snapshot, first.Id.Value, contactPointId: 2).Phase);
            Assert.AreEqual(
                CombatPhysicalContactPhase.Stayed,
                FindBodyPartContactDebugEntry(snapshot, second.Id.Value, contactPointId: 2).Phase);
            Assert.AreEqual(0, FindDebugEntryCount(snapshot, missingConfig.Id.Value));
        }

        [Test]
        public void PhysicalAudio_AfterWarmup_AllocatesZeroBytesFor4096Ticks()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(new RecordingAudioService());
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);
            for (int frame = 0; frame < 8; frame++)
                Tick(system, world, physicsRuntime, frame);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 8; frame < 4104; frame++)
                Tick(system, world, physicsRuntime, frame);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(0, system.LastSummary.BodyPartContactPlayedCount);
        }

        [Test]
        public void PhysicalAudio_FirstContactForPreparedNewEmitter_DoesNotAllocate()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity first);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(first, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            Tick(system, world, physicsRuntime, 0);

            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity second =
                SpawnCharacter(world, teamId: 1, x: 2f);
            SetMotion(second, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            MoveCharacterY(second, 1000);
            system.PrepareContactRegistrations(world, physicsRuntime);
            system.Tick(
                world,
                new RuntimeFrame(1),
                MxFramework.Game.CharacterTest.CharacterTestDeterministicUnits.DefaultTickRate,
                physicsRuntime);
            MoveCharacterY(second, 0);
            system.PrepareContactRegistrations(world, physicsRuntime);
            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();

            system.Tick(
                world,
                new RuntimeFrame(2),
                MxFramework.Game.CharacterTest.CharacterTestDeterministicUnits.DefaultTickRate,
                physicsRuntime);

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(2, system.LastSummary.BodyPartContactPlayedCount);
            Assert.AreEqual(0, system.LastSummary.RegistrationCapacitySkipCount);
        }

        [Test]
        public void CharacterRuntimeModule_NewEntityRegistersAtSpawnAndPresentationHotPathDoesNotAllocate()
        {
            var definitions = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(definitions.TryGet(
                MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition definition));
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            var module = new MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                definitions: definitions,
                physicsRuntime: physicsRuntime,
                createViews: false);

            try
            {
                Assert.IsTrue(module.EnqueueSpawn(CreateSpawnRequest(definition.CharacterId, teamId: 1, x: 0f)));
                module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
                Assert.AreEqual(1, module.Entities.Count);
                Assert.AreEqual(0, module.PhysicalAudioSummary.RegistrationCapacitySkipCount);

                Assert.IsTrue(module.EnqueueSpawn(CreateSpawnRequest(definition.CharacterId, teamId: 1, x: 2f)));
                module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.Simulation));
                Assert.AreEqual(2, module.Entities.Count);
                Assert.AreEqual(0, module.PhysicalAudioSummary.RegistrationCapacitySkipCount);

                for (int i = 0; i < 8; i++)
                    module.TickPresentation(0d, new RuntimeFrame(10 + i));

                _ = System.GC.GetAllocatedBytesForCurrentThread();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 32; i++)
                    module.TickPresentation(0d, new RuntimeFrame(100 + i));
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(0L, allocated);
                Assert.AreEqual(0, module.PhysicalAudioSummary.RegistrationCapacitySkipCount);
            }
            finally
            {
                module.Dispose();
            }
        }

        [Test]
        public void CharacterRuntimeModule_PresentationPhysicalContactDrainsAudioInSameFrame()
        {
            var definitions = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(definitions.TryGet(
                MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition definition));
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            var audioService = new RecordingAudioService();
            var module = new MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                definitions: definitions,
                physicsRuntime: physicsRuntime,
                createViews: false,
                audioService: audioService,
                audioCueCatalog: MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.CreateDefault());

            try
            {
                Assert.IsTrue(module.EnqueueSpawn(CreateSpawnRequest(definition.CharacterId, teamId: 1, x: 0f)));
                module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
                Assert.AreEqual(0, audioService.PlayOneShotCount);
                var poseProvider = new MutablePresentationPoseProvider();
                module.SetBodyPartContactPresentationPoseProvider(poseProvider);

                module.TickPresentation(0d, RuntimeFrame.Zero);

                Assert.AreEqual(0, audioService.PlayOneShotCount);
                poseProvider.LeftYMillimeters = 60;
                poseProvider.RightYMillimeters = 60;
                module.TickPresentation(0d, new RuntimeFrame(1));

                Assert.AreEqual(
                    2,
                    audioService.PlayOneShotCount,
                    module.PhysicalAudioSummary.ToSummaryLine());
                Assert.AreEqual(2, module.AudioIntentDrainSummary.RequestCount);
                Assert.AreEqual(2, module.AudioIntentDrainSummary.PlayedCount);

                module.TickPresentation(0d, new RuntimeFrame(2));
                Assert.AreEqual(2, audioService.PlayOneShotCount);
            }
            finally
            {
                module.Dispose();
            }
        }

        [Test]
        public void CharacterRuntimeModule_ReusesPhysicalAudioRegistrationAcrossRepeatedSpawnRemoveAndClear()
        {
            var definitions = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(definitions.TryGet(
                MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition definition));
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            var module = new MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeModule(
                resources: null,
                logger: NullRuntimeLogger.Instance,
                definitions: definitions,
                physicsRuntime: physicsRuntime,
                createViews: false);

            try
            {
                for (int i = 0; i < 80; i++)
                {
                    MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity = module.World.Spawn(
                        CreateSpawnRequest(definition.CharacterId, teamId: 1, x: i * 0.01f),
                        definition);
                    module.TickPresentation(0d, new RuntimeFrame(i));

                    Assert.AreEqual(0, module.PhysicalAudioSummary.RegistrationCapacitySkipCount, "iteration=" + i);
                    Assert.IsTrue(module.World.Remove(entity.Id), "iteration=" + i);
                    Assert.AreEqual(0, module.Entities.Count, "iteration=" + i);
                }

                module.World.Clear();
                MxFramework.Game.CharacterTest.CharacterTestCharacterEntity afterClear = module.World.Spawn(
                    CreateSpawnRequest(definition.CharacterId, teamId: 1, x: 0f),
                    definition);
                module.TickPresentation(0d, new RuntimeFrame(100));

                Assert.AreEqual(0, module.PhysicalAudioSummary.RegistrationCapacitySkipCount);
                Assert.IsTrue(module.World.Remove(afterClear.Id));
            }
            finally
            {
                module.Dispose();
            }
        }

        [Test]
        public void PhysicalAudio_RegistrationCapacitySkipsCharacterAndCountsDrop()
        {
            var world = new MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld();
            for (int i = 0; i < 65; i++)
                SpawnCharacter(world, teamId: 1, x: i * 0.1f);
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(new RecordingAudioService());
            system.PrepareContactRegistrations(world, physicsRuntime: null);

            system.Tick(
                world,
                RuntimeFrame.Zero,
                MxFramework.Game.CharacterTest.CharacterTestDeterministicUnits.DefaultTickRate,
                physicsRuntime: null);

            Assert.AreEqual(1, system.LastSummary.RegistrationCapacitySkipCount);
            Assert.AreEqual(65, system.LastSummary.SkippedCount);
        }

        [Test]
        public void PhysicalAudio_SustainedContactDoesNotRepeatFromTranslation()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);
            MoveCharacterX(entity, 1200);
            Tick(system, world, physicsRuntime, 2);
            MoveCharacterX(entity, 1200);
            Tick(system, world, physicsRuntime, 3);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(0, system.LastSummary.CooldownSkipCount);
        }

        [Test]
        public void PhysicalAudio_AirborneToGroundedFootContactUsesCommonBodyPartContactCue()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Rough);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(
                entity,
                grounded: false,
                horizontalSpeedMillimetersPerSecond: 0,
                surfaceMaterial: CombatSurfaceMaterial.Default,
                verticalVelocityMillimetersPerSecond: -1200);
            MoveCharacterY(entity, 1000);
            Tick(system, world, physicsRuntime, 0);
            SetMotion(
                entity,
                grounded: true,
                horizontalSpeedMillimetersPerSecond: 0,
                surfaceMaterial: CombatSurfaceMaterial.Default);
            MoveCharacterY(entity, 0);
            Tick(system, world, physicsRuntime, 1);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactRoughEventId,
                audioService.LastPlayRequest.EventId);
            Assert.AreEqual(0, system.LastSummary.LandingPlayedCount);
            Assert.AreEqual(2, system.LastSummary.BodyPartContactPlayedCount);
            Assert.AreEqual(0, system.LastSummary.ContactPointEstimatedCount);
            Assert.AreEqual(2, system.LastSummary.SolverVelocityDeltaCount);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(system.BodyPartContactDebugSnapshot, contactPointId: 1);
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry right =
                FindBodyPartContactDebugEntry(system.BodyPartContactDebugSnapshot, contactPointId: 2);
            Assert.AreEqual(PhysicalAudioContactDynamicsSource.SolverVelocityDelta, left.DynamicsSource);
            Assert.AreEqual(PhysicalAudioContactDynamicsSource.SolverVelocityDelta, right.DynamicsSource);
            float totalFootImpulse = left.NormalImpulseNewtonSeconds + right.NormalImpulseNewtonSeconds;
            float totalSolverImpulse = entity.Physics.EffectiveMassKg * 1.2f;
            Assert.Greater(totalFootImpulse, 0f);
            Assert.LessOrEqual(totalFootImpulse, totalSolverImpulse + 0.001f);
            Assert.AreEqual(5, audioService.LastPlayRequest.Parameters.Count);
            Assert.Greater(
                FindParameter(
                    audioService.LastPlayRequest.Parameters,
                    MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId),
                0.4f);
            Assert.Greater(
                FindParameter(
                    audioService.LastPlayRequest.Parameters,
                    MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioPitchParameterId),
                0.9f);
        }

        [Test]
        public void PhysicalAudio_LowImpactVerticalPhysicalContactStillUsesCommonCue()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(
                entity,
                grounded: false,
                horizontalSpeedMillimetersPerSecond: 0,
                verticalVelocityMillimetersPerSecond: -300);
            MoveCharacterY(entity, 1000);
            Tick(system, world, physicsRuntime, 0);
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);
            MoveCharacterY(entity, 0);
            Tick(system, world, physicsRuntime, 1);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(0, system.LastSummary.LandingPlayedCount);
            Assert.AreEqual(2, system.LastSummary.BodyPartContactPlayedCount);
        }

        [Test]
        public void PhysicalAudio_GroundedTransitionDoesNotRetriggerStayedFootContact()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);
            Tick(system, world, physicsRuntime, 0);
            Assert.AreEqual(0, audioService.PlayOneShotCount);

            SetMotion(
                entity,
                grounded: false,
                horizontalSpeedMillimetersPerSecond: 0,
                verticalVelocityMillimetersPerSecond: -1200);
            Tick(system, world, physicsRuntime, 1);
            Assert.AreEqual(0, audioService.PlayOneShotCount);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);
            Tick(system, world, physicsRuntime, 2);

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(0, system.LastSummary.LandingPlayedCount);
            Assert.AreEqual(0, system.LastSummary.BodyPartContactPlayedCount);
        }

        [Test]
        public void PhysicalAudio_HitColliderMaterialOverridesEntitySurfaceMaterial()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Rough);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(
                entity,
                grounded: true,
                horizontalSpeedMillimetersPerSecond: 1200,
                surfaceMaterial: CombatSurfaceMaterial.Default);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactRoughEventId,
                audioService.LastPlayRequest.EventId);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.SoleContactRoughCueKey,
                system.LastSummary.LastCueKey);
        }

        [Test]
        public void PhysicalAudio_MovableObjectCollisionUsesContactThresholdAndPairCooldown()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                new MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                MxFramework.Game.CharacterTest.CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.audio.rough",
                    501,
                    MxFramework.Game.CharacterTest.CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    massKg: 40f,
                    CombatSurfaceMaterial.Rough,
                    new UnityEngine.Vector3(0.25f, 0.25f, 0.25f),
                    new UnityEngine.Vector3(2f, 0.25f, 3f))
            });
            MxFramework.Game.CharacterTest.CharacterTestMovableNaturalObjectState movable =
                world.MovableNaturalObjects[0];
            movable.AccumulateInteractionFieldVelocity(
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0031,
                400,
                0,
                20000);
            movable.ApplyPendingVelocity();

            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            Tick(system, world, physicsRuntime: null, frame: 0);
            Tick(system, world, physicsRuntime: null, frame: 5);

            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.ObjectCollisionRoughEventId,
                audioService.LastPlayRequest.EventId);
            Assert.AreEqual(
                MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem.MovableNaturalObjectEmitterIdOffset + 501,
                audioService.LastPlayRequest.EmitterId);
            Assert.AreEqual(5, audioService.LastPlayRequest.Parameters.Count);
            Assert.Greater(
                FindParameter(
                    audioService.LastPlayRequest.Parameters,
                    MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId),
                0.2f);

            Tick(system, world, physicsRuntime: null, frame: 12);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(1, system.LastSummary.ObjectCollisionPlayedCount);
            Assert.AreEqual(0, system.LastSummary.SkippedCount);
        }

        [Test]
        public void PhysicalAudio_MissingPhysicsRuntimeRecordsSkipWithoutThrowing()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateSystem(audioService);

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            Tick(system, world, physicsRuntime: null, frame: 0);

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(1, system.LastSummary.MissingPhysicsRuntimeSkipCount);
            Assert.AreEqual(1, system.LastSummary.SkippedCount);
        }

        [Test]
        public void PhysicalAudio_MissingAudioServiceRecordsSkipWithoutThrowing()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            var playback = new MxFramework.Game.CharacterTest.CharacterTestAudioCuePlaybackSystem(
                null,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var system = new MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem(playback);
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: false));

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            Assert.AreEqual(2, system.LastSummary.SkippedCount);
        }

        [Test]
        public void PhysicalAudio_MissingCueRecordsSkipWithoutCallingAudioService()
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            var playback = new MxFramework.Game.CharacterTest.CharacterTestAudioCuePlaybackSystem(
                audioService,
                new MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog(),
                NullRuntimeLogger.Instance);
            var system = new MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem(playback);
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: false));

            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 1200);
            EstablishLiftedBaselineThenTouch(
                system,
                world,
                physicsRuntime,
                entity,
                baselineFrame: 0);

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(2, system.LastSummary.SkippedCount);
        }

        private static MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem CreateSystem(
            RecordingAudioService audioService)
        {
            var system = new MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem(
                new MxFramework.Game.CharacterTest.CharacterTestAudioCuePlaybackSystem(
                    audioService,
                    MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.CreateDefault(),
                    NullRuntimeLogger.Instance));
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: false));
            return system;
        }

        private static MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem
            CreateGeneratedLinkedSystem(RecordingAudioService audioService)
        {
            string generatedAudioRoot = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "MxFrameworkGenerated",
                "Audio");
            string audioJson = System.IO.File.ReadAllText(
                System.IO.Path.Combine(generatedAudioRoot, "audio_runtime_catalog.json"));
            string physicalJson = System.IO.File.ReadAllText(
                System.IO.Path.Combine(generatedAudioRoot, "audio_physical_runtime_catalog.json"));
            Assert.IsTrue(
                AudioRuntimeCatalogJson.TryLoad(
                    audioJson,
                    out AudioDefinitionCatalog audioCatalog,
                    out string audioError),
                audioError);
            Assert.IsTrue(
                PhysicalAudioRuntimeCatalogJson.TryLoad(
                    physicalJson,
                    out PhysicalAudioDefinitionCatalog physicalCatalog,
                    out string physicalError),
                physicalError);
            Assert.IsTrue(
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                    audioCatalog,
                    physicalCatalog,
                    out MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog linkedCatalog,
                    out string linkError),
                linkError);

            var system = new MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem(
                new MxFramework.Game.CharacterTest.CharacterTestAudioCuePlaybackSystem(
                    audioService,
                    linkedCatalog,
                    NullRuntimeLogger.Instance));
            system.SetBakedMotionSource(new FakeBakedMotionSource(hasSocketFrame: false));
            return system;
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld CreateWorldWithCharacter(
            out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            string generatedPackageRoot = "")
        {
            var world = new MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld();
            var registry = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition definition));
            if (!string.IsNullOrWhiteSpace(generatedPackageRoot))
            {
                definition = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition(
                    definition.CharacterId,
                    definition.DisplayName,
                    definition.PrefabKey,
                    definition.DefaultTeamId,
                    definition.AnimationBinding,
                    definition.PhysicsProfile,
                    generatedPackageRoot);
            }

            entity = world.Spawn(
                new MxFramework.Game.CharacterTest.CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    MxFramework.Game.CharacterTest.CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            return world;
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterEntity SpawnCharacter(
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world,
            int teamId,
            float x,
            string generatedPackageRoot = "")
        {
            var registry = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                MxFramework.Game.CharacterTest.CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition definition));
            if (!string.IsNullOrWhiteSpace(generatedPackageRoot))
            {
                definition = new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition(
                    definition.CharacterId,
                    definition.DisplayName,
                    definition.PrefabKey,
                    definition.DefaultTeamId,
                    definition.AnimationBinding,
                    definition.PhysicsProfile,
                    generatedPackageRoot);
            }

            return world.Spawn(
                new MxFramework.Game.CharacterTest.CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z: 0f,
                    MxFramework.Game.CharacterTest.CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterSpawnRequest CreateSpawnRequest(
            int characterId,
            int teamId,
            float x)
        {
            return new MxFramework.Game.CharacterTest.CharacterTestCharacterSpawnRequest(
                characterId,
                teamId,
                x,
                y: 0f,
                z: 0f,
                MxFramework.Game.CharacterTest.CharacterTestCharacterControlOwner.LocalPlayer);
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime(
            CombatSurfaceMaterial surfaceMaterial,
            bool groundUnderProbe = true)
        {
            int xOffsetMillimeters = groundUnderProbe ? 0 : 100000;
            return CreatePhysicsRuntimeWithGroundBounds(
                surfaceMaterial,
                xOffsetMillimeters - 10000,
                xOffsetMillimeters + 10000,
                -10000,
                10000);
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime
            CreateMovablePlatformPhysicsRuntime()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(
                MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime
                    .DynamicMovableObjectBodyIdStart + 1);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(bodyId.Value),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(
                    MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime
                        .DynamicMovableObjectColliderId),
                MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime
                    .DynamicMovableObjectLayer,
                new FixVector3(
                    ToFix64Meters(-10000),
                    ToFix64Meters(-200),
                    ToFix64Meters(-10000)),
                new FixVector3(
                    ToFix64Meters(10000),
                    ToFix64Meters(20),
                    ToFix64Meters(10000)),
                CombatSurfaceMaterial.Default));
            return new MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(GroundLayer),
                new FixVector3(
                    ToFix64Meters(300),
                    ToFix64Meters(900),
                    ToFix64Meters(300)),
                ToFix64Meters(20),
                ToFix64Meters(300),
                staticColliderCount: 1,
                sourceDescription: "physical-audio-moving-platform-test");
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime CreateRightFootOnlyPhysicsRuntime(
            CombatSurfaceMaterial surfaceMaterial)
        {
            return CreatePhysicsRuntimeWithGroundBounds(
                surfaceMaterial,
                minXMillimeters: 50,
                maxXMillimeters: 250,
                minZMillimeters: -220,
                maxZMillimeters: -20);
        }

        private static MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntimeWithGroundBounds(
            CombatSurfaceMaterial surfaceMaterial,
            int minXMillimeters,
            int maxXMillimeters,
            int minZMillimeters,
            int maxZMillimeters)
        {
            const int bodyNumericId = 100;
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(bodyNumericId);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(bodyNumericId),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                GroundLayer,
                new FixVector3(
                    ToFix64Meters(minXMillimeters),
                    ToFix64Meters(-200),
                    ToFix64Meters(minZMillimeters)),
                new FixVector3(
                    ToFix64Meters(maxXMillimeters),
                    ToFix64Meters(20),
                    ToFix64Meters(maxZMillimeters)),
                surfaceMaterial));

            return new MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(GroundLayer),
                new FixVector3(
                    ToFix64Meters(300),
                    ToFix64Meters(900),
                    ToFix64Meters(300)),
                ToFix64Meters(20),
                ToFix64Meters(300),
                staticColliderCount: 1,
                sourceDescription: "physical-audio-test");
        }

        private static void Tick(
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system,
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world,
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int frame)
        {
            system.PrepareContactRegistrations(world, physicsRuntime);
            system.Tick(
                world,
                new RuntimeFrame(frame),
                MxFramework.Game.CharacterTest.CharacterTestDeterministicUnits.DefaultTickRate,
                physicsRuntime);
        }

        private static void EstablishLiftedBaselineThenTouch(
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system,
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world,
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime,
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            int baselineFrame)
        {
            MoveCharacterY(entity, 1000);
            Tick(system, world, physicsRuntime, baselineFrame);
            MoveCharacterY(entity, 0);
            Tick(system, world, physicsRuntime, baselineFrame + 1);
        }

        private static float ResolveFootstepDropVolume(
            int dropHeightMillimeters,
            out float normalImpulseNewtonSeconds)
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterRuntimeWorld world =
                CreateWorldWithCharacter(out MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity);
            MxFramework.Game.CharacterTest.CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreatePhysicsRuntime(CombatSurfaceMaterial.Default);
            RecordingAudioService audioService = new RecordingAudioService();
            MxFramework.Game.CharacterTest.CharacterTestPhysicalAudioSystem system =
                CreateGeneratedLinkedSystem(audioService);
            SetMotion(entity, grounded: true, horizontalSpeedMillimetersPerSecond: 0);
            MoveCharacterY(entity, dropHeightMillimeters);
            Tick(system, world, physicsRuntime, 0);
            MoveCharacterY(entity, 0);
            Tick(system, world, physicsRuntime, 1);

            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry left =
                FindBodyPartContactDebugEntry(system.BodyPartContactDebugSnapshot, contactPointId: 1);
            normalImpulseNewtonSeconds = left.NormalImpulseNewtonSeconds;
            return FindParameter(
                audioService.LastPlayRequest.Parameters,
                MxFramework.Game.CharacterTest.CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId);
        }

        private static void SetMotion(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            bool grounded,
            int horizontalSpeedMillimetersPerSecond,
            CombatSurfaceMaterial? surfaceMaterial = null,
            int verticalVelocityMillimetersPerSecond = 0)
        {
            entity.SetMotionStateDeterministic(
                verticalVelocityMillimetersPerSecond: verticalVelocityMillimetersPerSecond,
                lastMoveInputXMilli: horizontalSpeedMillimetersPerSecond > 0 ? (short)1000 : (short)0,
                lastMoveInputYMilli: 0,
                isGrounded: grounded,
                horizontalVelocityXMillimetersPerSecond: horizontalSpeedMillimetersPerSecond,
                horizontalVelocityYMillimetersPerSecond: 0,
                desiredVelocityXMillimetersPerSecond: horizontalSpeedMillimetersPerSecond,
                desiredVelocityYMillimetersPerSecond: 0,
                movementMode: horizontalSpeedMillimetersPerSecond > 0
                    ? CharacterMovementMode.Run
                    : CharacterMovementMode.Idle,
                physicsEnabled: true,
                surfaceMaterial: surfaceMaterial ?? CombatSurfaceMaterial.Default);
        }

        private static void MoveCharacterX(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            int deltaMillimeters)
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterTransformComponent transform = entity.Transform;
            entity.SetTransform(MxFramework.Game.CharacterTest.CharacterTestCharacterTransformComponent.FromDeterministic(
                transform.XMillimeters + deltaMillimeters,
                transform.YMillimeters,
                transform.ZMillimeters,
                transform.YawMilliDegrees));
        }

        private static void MoveCharacterY(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            int yMillimeters)
        {
            MxFramework.Game.CharacterTest.CharacterTestCharacterTransformComponent transform = entity.Transform;
            entity.SetTransform(MxFramework.Game.CharacterTest.CharacterTestCharacterTransformComponent.FromDeterministic(
                transform.XMillimeters,
                yMillimeters,
                transform.ZMillimeters,
                transform.YawMilliDegrees));
        }

        private static Fix64 ToFix64Meters(int millimeters)
        {
            return MxFramework.Game.CharacterTest.CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                millimeters);
        }

        private static CombatPhysicalContactEvent CreatePhysicalContactEvent(
            CombatPhysicalContactKind kind,
            CombatSurfaceMaterial material,
            int normalSpeedMillimetersPerSecond)
        {
            return new CombatPhysicalContactEvent(
                new CombatFrame(1),
                sequence: 0,
                kind,
                CombatPhysicalContactPhase.Started,
                new CombatEntityId(10),
                new CombatBodyId(10),
                new CombatColliderId(1),
                new CombatBodyId(20),
                new CombatColliderId(2),
                CombatPhysicsShapeKind.Aabb,
                sourceSlotId: 1,
                contactPairKey: 456,
                new FixVector3(ToFix64Meters(100), ToFix64Meters(200), ToFix64Meters(300)),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                material,
                relativeSpeedMillimetersPerSecond: normalSpeedMillimetersPerSecond,
                normalSpeedMillimetersPerSecond,
                tangentialSpeedMillimetersPerSecond: 0,
                pressure: 20000,
                intensity: 1200,
                contactPointId: 1,
                bodyPartId: 701);
        }

        private static void AssertLastRequestMatchesContactPoint(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            AudioPlayRequest request,
            int contactPointId,
            int minHitXMillimeters = int.MinValue,
            int maxHitXMillimeters = int.MaxValue,
            int minHitZMillimeters = int.MinValue,
            int maxHitZMillimeters = int.MaxValue)
        {
            MxFramework.CharacterRuntimeSpawn.CharacterImportedPackage package =
                MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
            Assert.IsNotNull(package);
            MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig[] contactPoints =
                package.Configs.BodyPartContactPoints;
            for (int i = 0; i < contactPoints.Length; i++)
            {
                MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                if (contactPoint.ContactPointId != contactPointId)
                {
                    continue;
                }

                int expectedXMillimeters = ClampMillimeters(
                    contactPoint.LocalOffsetXMillimeters,
                    minHitXMillimeters,
                    maxHitXMillimeters);
                int expectedZMillimeters = ClampMillimeters(
                    contactPoint.LocalOffsetZMillimeters,
                    minHitZMillimeters,
                    maxHitZMillimeters);
                Assert.AreEqual(expectedXMillimeters / 1000f, request.Transform.X, 0.005f);
                Assert.AreEqual(expectedZMillimeters / 1000f, request.Transform.Z, 0.005f);
                return;
            }

            Assert.Fail("Body part contact point was not found: " + contactPointId);
        }

        private static void AssertAnyRequestMatchesContactPoint(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            RecordingAudioService audioService,
            int contactPointId,
            int minHitZMillimeters = int.MinValue,
            int maxHitZMillimeters = int.MaxValue)
        {
            FindRequestMatchingContactPoint(
                entity,
                audioService,
                contactPointId,
                minHitZMillimeters,
                maxHitZMillimeters);
        }

        private static AudioPlayRequest FindRequestMatchingContactPoint(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            RecordingAudioService audioService,
            int contactPointId,
            int minHitZMillimeters = int.MinValue,
            int maxHitZMillimeters = int.MaxValue)
        {
            MxFramework.CharacterRuntimeSpawn.CharacterImportedPackage package =
                MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
            Assert.IsNotNull(package);
            MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig[] contactPoints =
                package.Configs.BodyPartContactPoints;
            for (int i = 0; i < contactPoints.Length; i++)
            {
                MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                if (contactPoint.ContactPointId != contactPointId)
                    continue;

                int expectedZMillimeters = ClampMillimeters(
                    contactPoint.LocalOffsetZMillimeters,
                    minHitZMillimeters,
                    maxHitZMillimeters);
                for (int requestIndex = 0; requestIndex < audioService.Requests.Count; requestIndex++)
                {
                    AudioPlayRequest request = audioService.Requests[requestIndex];
                    if (System.Math.Abs(request.Transform.X - contactPoint.LocalOffsetXMillimeters / 1000f) <= 0.005f
                        && System.Math.Abs(request.Transform.Z - expectedZMillimeters / 1000f) <= 0.005f)
                    {
                        return request;
                    }
                }

                Assert.Fail("No audio request matched body part contact point: " + contactPointId);
            }

            Assert.Fail("Body part contact point was not found: " + contactPointId);
            return default;
        }

        private static MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry
            FindBodyPartContactDebugEntry(
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot,
                int contactPointId)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry entry = snapshot[i];
                if (entry.ContactPointId == contactPointId)
                    return entry;
            }

            Assert.Fail("Body part contact debug entry was not found: " + contactPointId);
            return default;
        }

        private static MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry
            FindBodyPartContactDebugEntry(
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot,
                int entityId,
                int contactPointId)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugEntry entry = snapshot[i];
                if (entry.EntityId == entityId && entry.ContactPointId == contactPointId)
                    return entry;
            }

            Assert.Fail(
                "Body part contact debug entry was not found: entity="
                + entityId
                + " contactPoint="
                + contactPointId);
            return default;
        }

        private static int FindDebugEntryCount(
            MxFramework.Game.CharacterTest.CharacterTestBodyPartContactDebugSnapshot snapshot,
            int entityId)
        {
            int count = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].EntityId == entityId)
                    count++;
            }

            return count;
        }

        private static MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig FindContactPointConfig(
            MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
            int contactPointId)
        {
            MxFramework.CharacterRuntimeSpawn.CharacterImportedPackage package =
                MxFramework.Game.CharacterTest.CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
            Assert.IsNotNull(package);
            MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig[] contactPoints =
                package.Configs.BodyPartContactPoints;
            for (int i = 0; i < contactPoints.Length; i++)
            {
                MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                if (contactPoint.ContactPointId == contactPointId)
                    return contactPoint;
            }

            Assert.Fail("Body part contact point was not found: " + contactPointId);
            return null;
        }

        private static int ClampMillimeters(int value, int minValue, int maxValue)
        {
            if (value <= minValue)
            {
                return minValue;
            }

            return value >= maxValue ? maxValue : value;
        }

        private static float ToFloatMeters(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        private static float FindParameter(IReadOnlyList<AudioParameterValue> parameters, int parameterId)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].ParameterId == parameterId)
                {
                    return parameters[i].Value;
                }
            }

            Assert.Fail("Audio parameter was not found: " + parameterId);
            return 0f;
        }

        private sealed class FakeBakedMotionSource :
            MxFramework.Game.CharacterTest.ICharacterTestBakedWeaponTraceSource
        {
            private readonly bool _hasSocketFrame;
            private readonly bool _throwOnLegacyLookup;

            public FakeBakedMotionSource(bool hasSocketFrame, bool throwOnLegacyLookup = false)
            {
                _hasSocketFrame = hasSocketFrame;
                _throwOnLegacyLookup = throwOnLegacyLookup;
            }

            public int LegacySocketLookupCount { get; private set; }

            public bool TryGetFrame(
                string clipKey,
                int traceProfileId,
                int localFrame,
                out MxAnimationBakedWeaponTraceFrame frame)
            {
                LegacySocketLookupCount++;
                if (_throwOnLegacyLookup)
                    throw new System.InvalidOperationException("Tick must use the precompiled socket track.");
                frame = null;
                return false;
            }

            public bool TryGetSocketFrame(
                string clipKey,
                int socketId,
                int localFrame,
                out MxAnimationBakedSocketFrame frame)
            {
                frame = null;
                if (!_hasSocketFrame || socketId <= 0)
                    return false;

                frame = new MxAnimationBakedSocketFrame(
                    localFrame < 0 ? 0 : localFrame,
                    socketId,
                    socketId.ToString(),
                    new MxAnimationBakedVector3(
                        QuantizeMeters(socketId == 1 ? 0.25f : -0.25f),
                        QuantizeMeters(0f),
                        QuantizeMeters(0.35f)),
                    MxAnimationBakedVector3.Zero);
                return true;
            }

            public bool TryResolveSocketTrack(
                string clipKey,
                int socketId,
                out MxFramework.Game.CharacterTest.CharacterTestBakedSocketTrack track)
            {
                track = null;
                if (!_hasSocketFrame || socketId <= 0)
                    return false;

                var frames = new MxAnimationBakedSocketFrame[60];
                for (int localFrame = 0; localFrame < frames.Length; localFrame++)
                {
                    frames[localFrame] = new MxAnimationBakedSocketFrame(
                        localFrame,
                        socketId,
                        socketId.ToString(),
                        new MxAnimationBakedVector3(
                            QuantizeMeters(socketId == 1 ? 0.25f : -0.25f),
                            QuantizeMeters(0f),
                            QuantizeMeters(0.35f)),
                        MxAnimationBakedVector3.Zero);
                }
                track = MxFramework.Game.CharacterTest.CharacterTestBakedSocketTrack.Create(
                    clipKey,
                    socketId,
                    frames);
                return track != null;
            }

            private static long QuantizeMeters(float value)
            {
                return (long)System.Math.Round(value * 1000000f);
            }
        }

        private sealed class FakePresentationPoseProvider :
            MxFramework.Game.CharacterTest.ICharacterTestBodyPartContactPresentationPoseProvider
        {
            public bool TryResolveBodyPartContactPointEntityLocalOffset(
                MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig config,
                out FixVector3 localOffset)
            {
                localOffset = new FixVector3(
                    ToFix64Meters(config.ContactPointId == 1 ? 420 : -420),
                    ToFix64Meters(60),
                    ToFix64Meters(180));
                return true;
            }
        }

        private sealed class MutablePresentationPoseProvider :
            MxFramework.Game.CharacterTest.ICharacterTestBodyPartContactPresentationPoseProvider
        {
            public int LeftYMillimeters { get; set; } = 400;
            public int RightYMillimeters { get; set; } = 400;

            public bool TryResolveBodyPartContactPointEntityLocalOffset(
                MxFramework.Game.CharacterTest.CharacterTestCharacterEntity entity,
                MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig config,
                out FixVector3 localOffset)
            {
                localOffset = new FixVector3(
                    ToFix64Meters(config.LocalOffsetXMillimeters),
                    ToFix64Meters(config.ContactPointId == 1 ? LeftYMillimeters : RightYMillimeters),
                    ToFix64Meters(config.LocalOffsetZMillimeters));
                return true;
            }
        }

        private sealed class RecordingAudioService : IAudioService
        {
            private readonly List<AudioPlayRequest> _requests = new List<AudioPlayRequest>();

            public int PlayOneShotCount { get; private set; }
            public AudioPlayRequest LastPlayRequest { get; private set; }
            public IReadOnlyList<AudioPlayRequest> Requests => _requests;

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                PlayOneShotCount++;
                LastPlayRequest = request;
                _requests.Add(request);
                return AudioPlayResult.Ok(AudioHandle.Invalid);
            }

            public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle)
            {
                handle = new AudioHandle(1, request.EventId, request.EmitterId, AudioHandleState.Playing);
                return AudioPlayResult.Ok(handle);
            }

            public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusVolume(int busId, float volume)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusMuted(int busId, bool muted)
            {
                return AudioResult.Ok();
            }

            public AudioDebugSnapshot CaptureSnapshot()
            {
                return new AudioDebugSnapshot(
                    initialized: true,
                    totalPlayRequests: PlayOneShotCount,
                    totalStopRequests: 0,
                    activeEventCount: 0,
                    activeEvents: null,
                    busStates: null,
                    recentErrors: null);
            }

            public void Tick(float deltaTime)
            {
            }
        }
    }
}
