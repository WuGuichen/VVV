using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestWeaponHitAudioSystemTests
    {
        [Test]
        public void PhysicalFactBuilder_CarriesHitNormalSeparatelyFromPlaybackOrientation()
        {
            CharacterTestWeaponHitAudioEvent evt = CreateEvent(
                HitResolveKind.Damage,
                CombatSurfaceMaterial.HardSlick);

            Assert.IsTrue(CharacterTestPhysicalAudioFactBuilder.TryCreate(evt, out PhysicalAudioInteractionFact fact));
            Assert.AreEqual(0f, fact.ContactNormal.X, 0.0001f);
            Assert.AreEqual(1f, fact.ContactNormal.Y, 0.0001f);
            Assert.AreEqual(0f, fact.ContactNormal.Z, 0.0001f);
            Assert.AreEqual(0f, fact.Transform.ForwardX, 0.0001f);
            Assert.AreEqual(0f, fact.Transform.ForwardY, 0.0001f);
            Assert.AreEqual(1f, fact.Transform.ForwardZ, 0.0001f);
        }

        [Test]
        public void Catalog_Default_ResolvesWeaponHitCues()
        {
            CharacterTestAudioCueCatalog catalog = CharacterTestAudioCueCatalog.CreateDefault();

            AssertWeaponHitCue(
                catalog,
                CharacterTestAudioCueCatalog.WeaponHitDefaultCueKey,
                CharacterTestAudioCueCatalog.WeaponHitDefaultCueId,
                CharacterTestAudioCueCatalog.WeaponHitDefaultEventId,
                CharacterTestAudioCueCatalog.WeaponHitDefaultEventPath);
            AssertWeaponHitCue(
                catalog,
                CharacterTestAudioCueCatalog.WeaponHitBlockedCueKey,
                CharacterTestAudioCueCatalog.WeaponHitBlockedCueId,
                CharacterTestAudioCueCatalog.WeaponHitBlockedEventId,
                CharacterTestAudioCueCatalog.WeaponHitBlockedEventPath);
            AssertWeaponHitCue(
                catalog,
                CharacterTestAudioCueCatalog.WeaponHitHardSlickCueKey,
                CharacterTestAudioCueCatalog.WeaponHitHardSlickCueId,
                CharacterTestAudioCueCatalog.WeaponHitHardSlickEventId,
                CharacterTestAudioCueCatalog.WeaponHitHardSlickEventPath);
        }

        [Test]
        public void DamageHit_Plays3DAtResolvedHitPointWithMaterialCue()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioSystem system = CreateSystem(audioService);
            CharacterTestWeaponHitAudioEvent evt = CreateEvent(
                HitResolveKind.Damage,
                CombatSurfaceMaterial.HardSlick,
                hitPoint: new FixVector3(ToFix(1200), ToFix(800), ToFix(-400)),
                finalDamage: 18,
                impactPower: 24);

            system.Tick(new[] { evt }, new RuntimeFrame(10));

            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(
                CharacterTestAudioCueCatalog.WeaponHitHardSlickEventId,
                audioService.LastPlayRequest.EventId);
            Assert.AreEqual(1.2f, audioService.LastPlayRequest.Transform.X, 0.001f);
            Assert.AreEqual(0.8f, audioService.LastPlayRequest.Transform.Y, 0.001f);
            Assert.AreEqual(-0.4f, audioService.LastPlayRequest.Transform.Z, 0.001f);
            Assert.AreEqual(5, audioService.LastPlayRequest.Parameters.Count);
            Assert.AreEqual(10, audioService.LastPlayRequest.Frame);
             Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.WeaponHit), audioService.LastPlayRequest.TraceId);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
            Assert.AreEqual(1, system.LastSummary.DamagePlayedCount);
        }

        [Test]
        public void BlockedHit_PlaysBlockedCueWithoutHpDamage()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioSystem system = CreateSystem(audioService);
            CharacterTestWeaponHitAudioEvent evt = CreateEvent(
                HitResolveKind.Blocked,
                CombatSurfaceMaterial.Rough,
                finalDamage: 0,
                impactPower: 36);

            system.Tick(new[] { evt }, new RuntimeFrame(20));

            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(
                CharacterTestAudioCueCatalog.WeaponHitBlockedEventId,
                audioService.LastPlayRequest.EventId);
            Assert.AreEqual(1, system.LastSummary.BlockedPlayedCount);
            Assert.Greater(
                FindParameter(
                    audioService.LastPlayRequest.Parameters,
                    CharacterTestAudioCueCatalog.PhysicalAudioBrightnessParameterId),
                0.6f);
        }

        [Test]
        public void NonResolvedDamageOrBlockedResultsDoNotPlay()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioSystem system = CreateSystem(audioService);

            system.Tick(new[]
            {
                CreateEvent(HitResolveKind.Duplicate, CombatSurfaceMaterial.Default),
                CreateEvent(HitResolveKind.Friendly, CombatSurfaceMaterial.Default),
                CreateEvent(HitResolveKind.TargetDead, CombatSurfaceMaterial.Default)
            }, new RuntimeFrame(30));

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(3, system.LastSummary.IgnoredResultCount);
        }

        [Test]
        public void CooldownBlocksSameSourceTargetTraceActionInstanceUntilElapsed()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioSystem system = CreateSystem(audioService);
            CharacterTestWeaponHitAudioEvent evt = CreateEvent(
                HitResolveKind.Damage,
                CombatSurfaceMaterial.Default);

            system.Tick(new[] { evt }, new RuntimeFrame(40));
            system.Tick(new[] { evt }, new RuntimeFrame(43));
            system.Tick(new[] { evt }, new RuntimeFrame(46));

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
            Assert.AreEqual(0, system.LastSummary.CooldownSkipCount);

            system.Tick(new[] { evt }, new RuntimeFrame(47));
            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(1, system.LastSummary.CooldownSkipCount);
        }

        [Test]
        public void SpecificRuleOverridesFallbackFields()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioRule[] rules =
            {
                new CharacterTestWeaponHitAudioRule(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterialKind.Default,
                    CharacterTestAudioCueCatalog.WeaponHitDefaultCueKey,
                    cooldownFrames: 0,
                    priority: 1),
                new CharacterTestWeaponHitAudioRule(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterialKind.Default,
                    CharacterTestAudioCueCatalog.WeaponHitElasticCueKey,
                    cooldownFrames: 0,
                    priority: 1,
                    weaponId: 780001,
                    equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1,
                    damageTypeId: CharacterTestAuthoredContentIds.D409_Id1,
                    hitZoneId: CharacterTestAuthoredContentIds.D400_Id104,
                    bodyPartId: CharacterTestAuthoredContentIds.D400_Id3)
            };
            CharacterTestWeaponHitAudioSystem system = CreateSystem(audioService, rules);
            CharacterTestWeaponHitAudioEvent evt = CreateEvent(
                HitResolveKind.Damage,
                CombatSurfaceMaterial.HardSlick,
                weaponId: "weapon.exact",
                equipmentSlotId: "mainHand",
                damageTypeId: "slash",
                hitZoneId: "head",
                bodyPartId: "head");

            system.Tick(new[] { evt }, new RuntimeFrame(50));

            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(
                CharacterTestAudioCueCatalog.WeaponHitElasticEventId,
                audioService.LastPlayRequest.EventId);
            Assert.AreEqual(
                CharacterTestAudioCueCatalog.WeaponHitElasticCueKey,
                system.LastSummary.LastCueKey);
        }

        [Test]
        public void MissingAudioServiceOrCueOnlyRecordsSkips()
        {
            CharacterTestWeaponHitAudioSystem missingServiceSystem = CreateSystem(null);
            CharacterTestWeaponHitAudioEvent evt = CreateEvent(
                HitResolveKind.Damage,
                CombatSurfaceMaterial.Default);

            missingServiceSystem.Tick(new[] { evt }, new RuntimeFrame(60));
            Assert.AreEqual(1, missingServiceSystem.LastSummary.PlayedCount);
            Assert.AreEqual(1, missingServiceSystem.LastIntentDrainSummary.FailedCount);

            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioRule[] missingCueRules =
            {
                new CharacterTestWeaponHitAudioRule(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterialKind.Default,
                    "missing.weapon.hit.cue",
                    cooldownFrames: 0,
                    priority: 1)
            };
            CharacterTestWeaponHitAudioSystem missingCueSystem = CreateSystem(audioService, missingCueRules);

            missingCueSystem.Tick(new[] { evt }, new RuntimeFrame(61));

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(1, missingCueSystem.LastSummary.MissingCueSkipCount);
        }

        [Test]
        public void AudioIntentQueue_DropsWhenCapacityExceededAndClearResets()
        {
            var queue = new CharacterTestAudioIntentQueue(1);

            Assert.IsTrue(queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.ActionCue,
                CharacterTestAudioCueCatalog.LightAttackCueId,
                AudioTransform.Origin,
                emitterId: 1,
                new RuntimeFrame(1),
                 new AudioTraceIdentity(AudioTraceKindIds.WeaponHit))));
            Assert.IsFalse(queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.ActionCue,
                CharacterTestAudioCueCatalog.FireballExplosionCueId,
                AudioTransform.Origin,
                emitterId: 2,
                new RuntimeFrame(1),
                 new AudioTraceIdentity(AudioTraceKindIds.WeaponHit))));

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue.DroppedCount);

            queue.Clear();

            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, queue.DroppedCount);
        }

        [Test]
        public void AudioIntentDrainSystem_PlaysQueuedIntentsInSubmitOrder()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            var playback = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(4);
            var drain = new CharacterTestAudioIntentDrainSystem(playback);

            playback.BeginFrame();
            queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.ActionCue,
                CharacterTestAudioCueCatalog.LightAttackCueId,
                AudioTransform.Origin,
                emitterId: 10,
                new RuntimeFrame(70),
                 new AudioTraceIdentity(AudioTraceKindIds.WeaponHit)));
            queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.Interaction,
                CharacterTestAudioCueCatalog.FireballExplosionCueId,
                AudioTransform.Origin,
                emitterId: 20,
                new RuntimeFrame(70),
                 new AudioTraceIdentity(AudioTraceKindIds.GameplayInteraction)));

            CharacterTestAudioIntentDrainSummary summary = drain.Tick(queue, new RuntimeFrame(70));

            Assert.AreEqual(2, summary.RequestCount);
            Assert.AreEqual(2, summary.PlayedCount);
            Assert.AreEqual(0, summary.FailedCount);
            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackEventId, audioService.Requests[0].EventId);
            Assert.AreEqual(CharacterTestAudioCueCatalog.FireballExplosionEventId, audioService.Requests[1].EventId);
             Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.WeaponHit), audioService.Requests[0].TraceId);
             Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.GameplayInteraction), audioService.Requests[1].TraceId);
        }

        [Test]
        public void AudioIntentDrainSystem_CopiesParametersBeforeProducerReusesArray()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            var playback = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(2);
            var drain = new CharacterTestAudioIntentDrainSystem(playback);
            AudioParameterValue[] parameters =
            {
                new AudioParameterValue(CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId, 0.25f)
            };

            playback.BeginFrame();
            queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.WeaponHit,
                CharacterTestAudioCueCatalog.WeaponHitDefaultCueId,
                AudioTransform.Origin,
                emitterId: 30,
                new RuntimeFrame(71),
                 new AudioTraceIdentity(AudioTraceKindIds.WeaponHit),
                parameters));
            parameters[0] = new AudioParameterValue(
                CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId,
                0.9f);

            CharacterTestAudioIntentDrainSummary summary = drain.Tick(queue, new RuntimeFrame(71));

            Assert.AreEqual(1, summary.PlayedCount);
            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(0.25f, audioService.LastPlayRequest.Parameters[0].Value, 0.0001f);
        }

        [Test]
        public void AudioIntentDrainSystem_ReportsMissingServiceAsFailure()
        {
            var playback = new CharacterTestAudioCuePlaybackSystem(
                null,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(2);
            var drain = new CharacterTestAudioIntentDrainSystem(playback);

            playback.BeginFrame();
            queue.Enqueue(CharacterTestAudioIntent.CreateCue(
                CharacterTestAudioIntentSource.ActionCue,
                CharacterTestAudioCueCatalog.LightAttackCueId,
                AudioTransform.Origin,
                emitterId: 1,
                new RuntimeFrame(72),
                 new AudioTraceIdentity(AudioTraceKindIds.WeaponHit)));

            CharacterTestAudioIntentDrainSummary summary = drain.Tick(queue, new RuntimeFrame(72));

            Assert.AreEqual(1, summary.RequestCount);
            Assert.AreEqual(0, summary.PlayedCount);
            Assert.AreEqual(1, summary.FailedCount);
            Assert.AreEqual(1, playback.LastFrameFailureCount);
        }

        [Test]
        public void SurfaceMaterialResolverUsesHitColliderMaterial()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(12);
            var colliderId = new CombatColliderId(34);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(20),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                layer: 1,
                localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half),
                surfaceMaterial: CombatSurfaceMaterial.Rough));
            CombatQueryResult hit = CreateQueryResult(
                bodyId,
                colliderId,
                CombatPhysicsShapeKind.Aabb);

            CombatSurfaceMaterial resolved =
                CharacterTestCombatDamageResolver.ResolveWeaponHitAudioSurfaceMaterial(
                    world,
                    hit,
                    CombatSurfaceMaterial.HardSlick);

            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, resolved.Kind);

            CombatSurfaceMaterial fallback =
                CharacterTestCombatDamageResolver.ResolveWeaponHitAudioSurfaceMaterial(
                    world,
                    CreateQueryResult(new CombatBodyId(88), new CombatColliderId(99), CombatPhysicsShapeKind.Aabb),
                    CombatSurfaceMaterial.HardSlick);
            Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, fallback.Kind);
        }

        [Test]
        public void GeneratedCatalog_UsesPhysicalResolverCueAndResponseParameters()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioSystem system = CreateGeneratedSystem(
                audioService,
                responseCurveCount: 2);

            system.Tick(new[]
            {
                CreateEvent(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterial.HardSlick,
                    impactPower: 40)
            }, new RuntimeFrame(80));

            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(600001, audioService.LastPlayRequest.EventId);
            Assert.AreEqual(2, audioService.LastPlayRequest.Parameters.Count);
            Assert.AreEqual(500251, audioService.LastPlayRequest.Parameters[0].ParameterId);
            Assert.AreEqual(500252, audioService.LastPlayRequest.Parameters[1].ParameterId);
            Assert.AreEqual(-20, audioService.LastPlayRequest.Priority);
             Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.ResolvedWeaponHit), audioService.LastPlayRequest.TraceId);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
        }

        [Test]
        public void GeneratedCatalog_ResponseBeyondIntentCapacityIsRejectedDuringStartupLink()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            System.ArgumentException exception = Assert.Throws<System.ArgumentException>(() =>
                CreateGeneratedSystem(
                    audioService,
                    responseCurveCount: CharacterTestAudioIntent.MaxParameterCount + 1));

            StringAssert.Contains("exceeding CharacterTest audio intent capacity", exception.Message);
            Assert.AreEqual(0, audioService.PlayOneShotCount);
        }

        [Test]
        public void GeneratedCatalog_CooldownCapacityEvictsOldestAndPlaysNewKeyWithoutAllocation()
        {
            CharacterTestAudioCueCatalog catalog = CreateGeneratedCatalog(
                responseCurveCount: 0,
                cooldownFrames: 100);
            var playback = new CharacterTestAudioCuePlaybackSystem(
                null,
                catalog,
                NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(1);
            var system = new CharacterTestWeaponHitAudioSystem(playback, queue);

            for (int i = 0; i < 64; i++)
            {
                system.Tick(new[]
                {
                    CreateEvent(
                        HitResolveKind.Damage,
                        CombatSurfaceMaterial.Default,
                        sourceEntityId: i + 1,
                        targetEntityId: 1000 + i,
                        actionInstanceId: 2000 + i,
                        traceId: 3000 + i)
                }, new RuntimeFrame(i));
                Assert.AreEqual(1, queue.Count);
                 Assert.AreEqual(0, queue[0].CueId);
                Assert.AreEqual(600001, queue[0].EventId);
                queue.Clear();
            }

            CharacterTestWeaponHitAudioEvent[] overflowEvent =
            {
                CreateEvent(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterial.Default,
                    sourceEntityId: 100,
                    targetEntityId: 1100,
                    actionInstanceId: 2100,
                    traceId: 3100)
            };
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            system.Tick(overflowEvent, new RuntimeFrame(100));

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
            Assert.AreEqual(0, system.LastSummary.PlaybackFailedSkipCount);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void GeneratedCatalog_CooldownBurstPreservesAllActiveKeysAndReusesExpiredSlotWithoutAllocation()
        {
            CharacterTestAudioCueCatalog catalog = CreateGeneratedCatalog(
                responseCurveCount: 0,
                cooldownFrames: 100);
            var playback = new CharacterTestAudioCuePlaybackSystem(null, catalog, NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(1);
            var system = new CharacterTestWeaponHitAudioSystem(playback, queue);
            for (int i = 0; i < 64; i++)
            {
                system.Tick(new[]
                {
                    CreateEvent(
                        HitResolveKind.Damage,
                        CombatSurfaceMaterial.Default,
                        sourceEntityId: i + 1,
                        targetEntityId: 1000 + i)
                }, RuntimeFrame.Zero);
                Assert.AreEqual(1, queue.Count);
                queue.Clear();
            }

            CharacterTestWeaponHitAudioEvent[] overflow =
            {
                CreateEvent(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterial.Default,
                    sourceEntityId: 100,
                    targetEntityId: 1100)
            };
            CharacterTestWeaponHitAudioEvent[] first =
            {
                CreateEvent(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterial.Default,
                    sourceEntityId: 1,
                    targetEntityId: 1000)
            };
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            system.Tick(overflow, RuntimeFrame.Zero);
            int capacitySkip = system.LastSummary.CooldownCapacitySkipCount;
            system.Tick(first, RuntimeFrame.Zero);
            int cooldownSkip = system.LastSummary.CooldownSkipCount;
            system.Tick(overflow, new RuntimeFrame(100));

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(1, capacitySkip);
            Assert.AreEqual(1, cooldownSkip, "The first active key must not be evicted by the 65th key.");
            Assert.AreEqual(1, queue.Count, "An expired slot must be reusable by a later frame.");
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void FixedCooldownCache_MixedDurationSlotsUseTheirOwnExpiry()
        {
            var cache = new CharacterTestFixedCooldownCache<int>(1);
            Assert.AreEqual(CharacterTestCooldownAdmission.Available, cache.Evaluate(1, 0, 100));
            Assert.IsTrue(cache.Record(1, 0, 100));
            Assert.AreEqual(CharacterTestCooldownAdmission.CapacityFull, cache.Evaluate(2, 10, 1));
            Assert.AreEqual(CharacterTestCooldownAdmission.Available, cache.Evaluate(2, 100, 1));
            Assert.IsTrue(cache.Record(2, 100, 1));
        }

        [Test]
        public void GeneratedCatalog_PropertyProviderAndCombatSurfaceSelectDifferentBasisWithoutTickAllocation()
        {
            CharacterTestAudioCueCatalog catalog = CreateMaterialPairGeneratedCatalog();
            var playback = new CharacterTestAudioCuePlaybackSystem(null, catalog, NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(1);
            var properties = new CharacterTestAcousticBodyPropertyProvider(8);
            int weaponSubject = CharacterTestAcousticSubjectId.ForCharacter(
                1,
                CharacterTestAcousticSubjectRole.Weapon);
            Assert.IsTrue(properties.RegisterProperties(
                weaponSubject,
                new AcousticBodyProperties(
                    CharacterTestAcousticMaterialMapper.Metal,
                    0.5f,
                    0.25f,
                    AcousticSurfaceKind.Smooth,
                    AcousticConstructionKind.Solid)));
            var system = new CharacterTestWeaponHitAudioSystem(playback, queue, properties);

            system.Tick(new[] { CreateEvent(HitResolveKind.Damage, CombatSurfaceMaterial.HardSlick) }, RuntimeFrame.Zero);
            Assert.AreEqual(610001, queue[0].EventId);
            queue.Clear();
            system.Tick(new[] { CreateEvent(HitResolveKind.Damage, CombatSurfaceMaterial.Elastic) }, new RuntimeFrame(1));
            Assert.AreEqual(610002, queue[0].EventId);
            queue.Clear();

            Assert.IsTrue(properties.RegisterProperties(
                weaponSubject,
                new AcousticBodyProperties(
                    CharacterTestAcousticMaterialMapper.Rubber,
                    0.35f,
                    0.3f,
                    AcousticSurfaceKind.Normal,
                    AcousticConstructionKind.SoftFilled)));
            CharacterTestWeaponHitAudioEvent[] rubberWeaponStoneTarget =
            {
                CreateEvent(HitResolveKind.Damage, CombatSurfaceMaterial.HardSlick)
            };
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            system.Tick(rubberWeaponStoneTarget, new RuntimeFrame(2));

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(610003, queue[0].EventId);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void LegacyCatalog_CooldownCapacityEvictsOldestAndPlaysNewKeyWithoutAllocation()
        {
            RecordingAudioService audioService = new RecordingAudioService();
            CharacterTestWeaponHitAudioSystem system = CreateSystem(audioService);
            for (int i = 0; i < 64; i++)
            {
                system.Tick(new[]
                {
                    CreateEvent(
                        HitResolveKind.Damage,
                        CombatSurfaceMaterial.Default,
                        sourceEntityId: i + 1,
                        targetEntityId: 1000 + i,
                        actionInstanceId: 2000 + i,
                        traceId: 3000 + i)
                }, new RuntimeFrame(i));
            }
            CharacterTestWeaponHitAudioEvent[] overflowEvent =
            {
                CreateEvent(
                    HitResolveKind.Damage,
                    CombatSurfaceMaterial.Default,
                    sourceEntityId: 100,
                    targetEntityId: 1100,
                    actionInstanceId: 2100,
                    traceId: 3100)
            };
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            system.Tick(overflowEvent, new RuntimeFrame(100));

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(65, audioService.PlayOneShotCount);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
            Assert.AreEqual(0, system.LastSummary.PlaybackFailedSkipCount);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestWeaponHitAudioSystem CreateSystem(
            IAudioService audioService,
            CharacterTestWeaponHitAudioRule[] rules = null)
        {
            var playback = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            return rules == null
                ? new CharacterTestWeaponHitAudioSystem(playback)
                : new CharacterTestWeaponHitAudioSystem(playback, rules);
        }

        private static CharacterTestWeaponHitAudioSystem CreateGeneratedSystem(
            IAudioService audioService,
            int responseCurveCount)
        {
            var playback = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CreateGeneratedCatalog(responseCurveCount),
                NullRuntimeLogger.Instance);
            return new CharacterTestWeaponHitAudioSystem(playback);
        }

        private static CharacterTestAudioCueCatalog CreateGeneratedCatalog(
            int responseCurveCount,
            int cooldownFrames = 0)
        {
            const int busId = 600000;
            const int cueId = 600001;
            const int eventId = 600001;
            var parameterDefinitions = new AudioParameterDefinition[responseCurveCount];
            var responseCurves = new PhysicalAudioResponseCurveDefinition[responseCurveCount];
            for (int i = 0; i < responseCurveCount; i++)
            {
                int parameterId = 500251 + i;
                parameterDefinitions[i] = new AudioParameterDefinition(
                    parameterId,
                    "GeneratedParameter" + i,
                    AudioParameterKind.Continuous,
                    0f,
                    0f,
                    1f);
                responseCurves[i] = new PhysicalAudioResponseCurveDefinition(
                    parameterId,
                    PhysicalAudioResponseInput.Intensity,
                    0f,
                    1f,
                    new[]
                    {
                        new PhysicalAudioResponsePoint(0f, 0f),
                        new PhysicalAudioResponsePoint(1f, 1f)
                    });
            }

            var audioCatalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(busId, "generated", "bus:/") },
                new[]
                {
                    new AudioEventDefinition(
                        eventId,
                        "generated.weapon_hit",
                        "event:/MxFramework/Generated/Physical/WeaponHit/530221",
                        string.Empty,
                        AudioEventKind.Event,
                        busId,
                        is3D: true,
                        isLoop: false,
                        maxDistance: 24f,
                        parameterDefinitions)
                },
                new[]
                {
                    new AudioCueDefinition(
                        cueId,
                        "sfx.generated.weapon_hit",
                        eventId,
                        AudioCuePlayMode.OneShot)
                },
                "audio-hash",
                "snapshot-hash");

            PhysicalAudioResponseProfileId responseId = responseCurveCount > 0
                ? new PhysicalAudioResponseProfileId(540001)
                : PhysicalAudioResponseProfileId.None;
            PhysicalAudioResponseProfileDefinition[] responses = responseCurveCount > 0
                ? new[] { new PhysicalAudioResponseProfileDefinition(responseId, responseCurves) }
                : Array.Empty<PhysicalAudioResponseProfileDefinition>();
            var unitRange = new PhysicalAudioRange(0f, 1f, 0.5f);
            var physicalCatalog = new PhysicalAudioDefinitionCatalog(
                new[]
                {
                    new AcousticMaterialProfileDefinition(
                        CharacterTestAcousticMaterialMapper.Metal,
                        0.9f,
                        0.8f,
                        0.2f,
                        0.2f,
                        0.8f)
                },
                new[]
                {
                    new PhysicalAudioFamilyDefinition(
                        new PhysicalAudioFamilyId(520004),
                        PhysicalAudioInteractionKind.WeaponHit,
                        PhysicalAudioInteractionMode.Hit,
                        PhysicalAudioFamilyId.None,
                        AudioBasisId.None,
                        responseId,
                        priority: 40,
                        cooldownFrames,
                        new PhysicalAudioSelectionWeights(4f, 1f, 1f, 0.5f, 0.5f, 1f, 1f, 0.5f))
                },
                new[]
                {
                    new PhysicalAudioBasisDefinition(
                        new AudioBasisId(530221),
                        new PhysicalAudioFamilyId(520004),
                        cueId,
                        priority: -20,
                        new PhysicalAudioBodyAnchorDefinition(
                            CharacterTestAcousticMaterialMapper.Metal,
                            unitRange,
                            unitRange,
                            AcousticSurfaceKind.Any,
                            AcousticConstructionKind.Any),
                        new PhysicalAudioBodyAnchorDefinition(
                            AcousticMaterialId.None,
                            unitRange,
                            unitRange,
                            AcousticSurfaceKind.Any,
                            AcousticConstructionKind.Any),
                        new PhysicalAudioRange(0f, 100f, 10f),
                        unitRange,
                        new PhysicalAudioRange(0f, 180f, 90f))
                },
                responses,
                "physical-hash",
                "audio-hash");
            return CharacterTestAudioCueCatalog.FromRuntimeCatalog(audioCatalog, physicalCatalog);
        }

        private static CharacterTestAudioCueCatalog CreateMaterialPairGeneratedCatalog()
        {
            const int busId = 610000;
            AudioEventDefinition Event(int id) => new AudioEventDefinition(
                id,
                "generated.material_pair." + id,
                "event:/MxFramework/Generated/Physical/WeaponHit/" + id,
                string.Empty,
                AudioEventKind.Event,
                busId,
                is3D: true,
                isLoop: false,
                maxDistance: 24f,
                Array.Empty<AudioParameterDefinition>());
            var audioCatalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(busId, "generated", "bus:/") },
                new[] { Event(610001), Event(610002), Event(610003) },
                new[]
                {
                    new AudioCueDefinition(610001, "sfx.generated.metal_stone", 610001, AudioCuePlayMode.OneShot),
                    new AudioCueDefinition(610002, "sfx.generated.metal_rubber", 610002, AudioCuePlayMode.OneShot),
                    new AudioCueDefinition(610003, "sfx.generated.rubber_stone", 610003, AudioCuePlayMode.OneShot)
                });
            var unit = new PhysicalAudioRange(0f, 1f, 0.5f);
            PhysicalAudioBodyAnchorDefinition Anchor(AcousticMaterialId material) =>
                new PhysicalAudioBodyAnchorDefinition(
                    material,
                    unit,
                    unit,
                    AcousticSurfaceKind.Any,
                    AcousticConstructionKind.Any);
            PhysicalAudioBasisDefinition Basis(
                int basisId,
                int cueId,
                AcousticMaterialId impactor,
                AcousticMaterialId receiver) => new PhysicalAudioBasisDefinition(
                    new AudioBasisId(basisId),
                    new PhysicalAudioFamilyId(620001),
                    cueId,
                    priority: 1,
                    Anchor(impactor),
                    Anchor(receiver),
                    new PhysicalAudioRange(0f, 100f, 10f),
                    unit,
                    new PhysicalAudioRange(0f, 180f, 90f));
            var physicalCatalog = new PhysicalAudioDefinitionCatalog(
                new[]
                {
                    new AcousticMaterialProfileDefinition(CharacterTestAcousticMaterialMapper.Metal, 0.9f, 0.8f, 0.2f, 0.2f, 0.8f),
                    new AcousticMaterialProfileDefinition(CharacterTestAcousticMaterialMapper.Stone, 0.9f, 0.9f, 0.2f, 0.2f, 0.8f),
                    new AcousticMaterialProfileDefinition(CharacterTestAcousticMaterialMapper.Rubber, 0.3f, 0.4f, 0.5f, 0.9f, 0.2f)
                },
                new[]
                {
                    new PhysicalAudioFamilyDefinition(
                        new PhysicalAudioFamilyId(620001),
                        PhysicalAudioInteractionKind.WeaponHit,
                        PhysicalAudioInteractionMode.Hit,
                        PhysicalAudioFamilyId.None,
                        AudioBasisId.None,
                        PhysicalAudioResponseProfileId.None,
                        priority: 1,
                        cooldownFrames: 0,
                        new PhysicalAudioSelectionWeights(4f, 1f, 1f, 1f, 1f, 1f, 1f, 1f))
                },
                new[]
                {
                    Basis(630001, 610001, CharacterTestAcousticMaterialMapper.Metal, CharacterTestAcousticMaterialMapper.Stone),
                    Basis(630002, 610002, CharacterTestAcousticMaterialMapper.Metal, CharacterTestAcousticMaterialMapper.Rubber),
                    Basis(630003, 610003, CharacterTestAcousticMaterialMapper.Rubber, CharacterTestAcousticMaterialMapper.Stone)
                },
                Array.Empty<PhysicalAudioResponseProfileDefinition>());
            return CharacterTestAudioCueCatalog.FromRuntimeCatalog(audioCatalog, physicalCatalog);
        }

        private static CharacterTestWeaponHitAudioEvent CreateEvent(
            HitResolveKind kind,
            CombatSurfaceMaterial material,
            FixVector3 hitPoint = default,
            int finalDamage = 12,
            int impactPower = 18,
            string weaponId = "weapon.main_hand_katana",
            string equipmentSlotId = "mainHand",
            string damageTypeId = "slash",
            string hitZoneId = "torso",
            string bodyPartId = "torso",
            int sourceEntityId = 1,
            int targetEntityId = 2,
            int actionInstanceId = 2002,
            int traceId = 3003)
        {
            return new CharacterTestWeaponHitAudioEvent(
                new RuntimeFrame(0),
                sourceEntityId: sourceEntityId,
                targetEntityId: targetEntityId,
                actionId: 1001,
                actionInstanceId: actionInstanceId,
                traceId: traceId,
                resultKind: kind,
                weaponId: ToWeaponId(weaponId),
                equipmentSlotId: ToEquipmentSlotId(equipmentSlotId),
                damageTypeId: ToDamageTypeId(damageTypeId),
                hitZoneId: ToHitZoneId(hitZoneId),
                bodyPartId: ToBodyPartId(bodyPartId),
                hitPointWorld: hitPoint.Equals(default(FixVector3))
                    ? new FixVector3(ToFix(500), ToFix(900), ToFix(250))
                    : hitPoint,
                hitNormalWorld: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                targetBodyId: new CombatBodyId(2),
                targetColliderId: new CombatColliderId(3),
                targetShapeKind: CombatPhysicsShapeKind.Aabb,
                surfaceMaterial: material,
                finalDamage: finalDamage,
                impactPower: impactPower,
                effectiveImpact: 0f);
        }

        private static int ToWeaponId(string value) => 780001;

        private static int ToEquipmentSlotId(string value) => CharacterTestAuthoredContentIds.D405_Id1;

        private static int ToDamageTypeId(string value) => CharacterTestAuthoredContentIds.D409_Id1;

        private static int ToHitZoneId(string value) =>
            string.Equals(value, "head", StringComparison.Ordinal)
                ? CharacterTestAuthoredContentIds.D400_Id104
                : CharacterTestAuthoredContentIds.D400_Id55;

        private static int ToBodyPartId(string value) =>
            string.Equals(value, "head", StringComparison.Ordinal)
                ? CharacterTestAuthoredContentIds.D400_Id3
                : CharacterTestAuthoredContentIds.D400_Id10;

        private static CombatQueryResult CreateQueryResult(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            CombatPhysicsShapeKind shapeKind)
        {
            var query = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: new CombatEntityId(1),
                traceId: 3003,
                actionId: 1001,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.All);

            return new CombatQueryResult(
                query,
                new CombatEntityId(2),
                bodyId,
                colliderId,
                shapeKind,
                Fix64.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
        }

        private static void AssertWeaponHitCue(
            CharacterTestAudioCueCatalog catalog,
            string cueKey,
            int expectedCueId,
            int expectedEventId,
            string expectedEventPath)
        {
            Assert.IsTrue(catalog.TryGetCue(cueKey, out AudioCueDefinition cue));
            Assert.AreEqual(expectedCueId, cue.Id);
            Assert.AreEqual(expectedEventId, cue.EventId);
            Assert.IsTrue(catalog.TryGetEvent(cue.EventId, out AudioEventDefinition audioEvent));
            Assert.AreEqual(expectedEventPath, audioEvent.FmodEventPath);
            Assert.IsTrue(audioEvent.Is3D);
            Assert.IsTrue(catalog.TryGetParameter(
                cue.EventId,
                CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterId,
                out _));
        }

        private static float FindParameter(IReadOnlyList<AudioParameterValue> parameters, int parameterId)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].ParameterId == parameterId)
                    return parameters[i].Value;
            }

            Assert.Fail("Audio parameter was not found: " + parameterId);
            return 0f;
        }

        private static Fix64 ToFix(int millimeters)
        {
            return Fix64.FromRatio(millimeters, 1000);
        }

        private sealed class RecordingAudioService : IAudioService
        {
            private readonly List<AudioPlayRequest> _requests = new List<AudioPlayRequest>(128);

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
