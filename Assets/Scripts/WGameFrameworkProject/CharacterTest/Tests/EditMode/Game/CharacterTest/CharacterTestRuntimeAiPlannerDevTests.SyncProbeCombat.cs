using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsProjectileMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                projectiles: new[]
                {
                    CreateProjectileSyncSample(localFrame: 4, positionXRaw: 1000L)
                });
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                projectiles: new[]
                {
                    CreateProjectileSyncSample(localFrame: 5, positionXRaw: 2000L)
                });

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "projectile" && row.EntityId == 101 && row.Field == "localFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "projectile" && row.EntityId == 101 && row.Field == "positionXRaw"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsTornadoMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                tornado: new CharacterTestRuntimeAiLabFrameSyncTornadoSample(
                    lastDamageTickTargetCount: 1,
                    interactionLastFrameCount: 1,
                    interactionLastFrameHash: 1001L));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                tornado: new CharacterTestRuntimeAiLabFrameSyncTornadoSample(
                    lastDamageTickTargetCount: 0,
                    interactionLastFrameCount: 2,
                    interactionLastFrameHash: 2002L));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "tornado" && row.EntityId == 0 && row.Field == "lastDamageTickTargetCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "tornado" && row.EntityId == 0 && row.Field == "interactionLastFrameCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "tornado" && row.EntityId == 0 && row.Field == "interactionLastFrameHash"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsChainLightningMismatch()
        {
            var expectedArcEvents = new[]
            {
                new CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample(
                    frame: 20,
                    sourceEntityId: 1,
                    targetEntityId: 2,
                    segmentIndex: 0,
                    actionInstanceId: 7,
                    localFrame: 3,
                    originXRaw: 100,
                    originYRaw: 200,
                    originZRaw: 300,
                    directionXRaw: 1000,
                    directionYRaw: 0,
                    directionZRaw: 0,
                    endpointXRaw: 400,
                    endpointYRaw: 200,
                    endpointZRaw: 300,
                    lengthMillimeters: 300,
                    hit: true),
            };
            var actualArcEvents = new[]
            {
                new CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample(
                    frame: 20,
                    sourceEntityId: 1,
                    targetEntityId: 2,
                    segmentIndex: 0,
                    actionInstanceId: 7,
                    localFrame: 3,
                    originXRaw: 101,
                    originYRaw: 200,
                    originZRaw: 300,
                    directionXRaw: 1000,
                    directionYRaw: 0,
                    directionZRaw: 0,
                    endpointXRaw: 400,
                    endpointYRaw: 200,
                    endpointZRaw: 300,
                    lengthMillimeters: 300,
                    hit: true),
            };
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                chainLightning: new CharacterTestRuntimeAiLabFrameSyncChainLightningSample(
                    lastTargetCount: 4,
                    arcEventCount: 1,
                    arcEventHash: 1004L,
                    arcEvents: expectedArcEvents));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                chainLightning: new CharacterTestRuntimeAiLabFrameSyncChainLightningSample(
                    lastTargetCount: 4,
                    arcEventCount: 1,
                    arcEventHash: 1004L,
                    arcEvents: actualArcEvents));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "chainLightning" && row.EntityId == 0 && row.Field == "arcEvent.originXRaw"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsLaserPathMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                laserPath: new CharacterTestRuntimeAiLabFrameSyncLaserPathSample(
                    eventCount: 1,
                    eventHash: 1001L,
                    events: new[]
                    {
                        new CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample(
                            frame: 20,
                            sourceEntityId: 1,
                            targetEntityId: 2,
                            projectileId: 10,
                            actionInstanceId: 30,
                            localFrame: 8,
                            originXRaw: 1000,
                            originYRaw: 0,
                            originZRaw: 2000,
                            directionXRaw: 1,
                            directionYRaw: 0,
                            directionZRaw: 0,
                            endpointXRaw: 5000,
                            endpointYRaw: 0,
                            endpointZRaw: 2000,
                            lengthMillimeters: 4000,
                            hit: true)
                    }));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                laserPath: new CharacterTestRuntimeAiLabFrameSyncLaserPathSample(
                    eventCount: 1,
                    eventHash: 1001L,
                    events: new[]
                    {
                        new CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample(
                            frame: 20,
                            sourceEntityId: 1,
                            targetEntityId: 2,
                            projectileId: 10,
                            actionInstanceId: 30,
                            localFrame: 8,
                            originXRaw: 3000,
                            originYRaw: 0,
                            originZRaw: 2000,
                            directionXRaw: 1,
                            directionYRaw: 0,
                            directionZRaw: 0,
                            endpointXRaw: 5000,
                            endpointYRaw: 0,
                            endpointZRaw: 2000,
                            lengthMillimeters: 4000,
                            hit: true)
                    }));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "laserPath" && row.EntityId == 0 && row.Field == "event.originXRaw"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsInteractionMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                interaction: new CharacterTestRuntimeAiLabFrameSyncInteractionSample(
                    lastFrameEventCount: 1,
                    lastFrameEventHash: 1001L,
                    vfxFrame: 20,
                    vfxProcessedInteractionCount: 1,
                    vfxSpawnedCount: 1,
                    vfxSkippedCount: 0,
                    vfxLastStableId: CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                    vfxLastForceMode: (int)CharacterTestInteractionForceMode.RadialImpulse,
                    vfxLastFieldShape: (int)CharacterTestInteractionFieldShape.Sphere,
                    vfxLastVfxDefinitionId: CharacterTestVfxIds.FireballExplosionImpactDefinitionId,
                    lastFrameEvents: new[]
                    {
                        new CharacterTestRuntimeAiLabFrameSyncInteractionEventSample(
                            frame: 20,
                            kind: (int)CharacterTestInteractionKind.PhysicalField,
                            stableId: CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                            sourceOrder: 1,
                            sourceEntityId: 1,
                            targetEntityId: 2,
                            anchorXRaw: 1000,
                            anchorYRaw: 0,
                            anchorZRaw: 2000,
                            directionXRaw: 0,
                            directionYRaw: 0,
                            directionZRaw: 1000000,
                            radiusRaw: 2500000,
                            intensity: 3000,
                            traceId: 44,
                            sourceDomain: CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse,
                            fieldShape: (int)CharacterTestInteractionFieldShape.Sphere,
                            fieldForceMode: (int)CharacterTestInteractionForceMode.RadialImpulse,
                            fieldTargetFilter: (int)CharacterTestInteractionTargetFilter.DynamicCharacters,
                            fieldCapsuleBottomOffsetRaw: 0,
                            fieldCapsuleTopOffsetRaw: 0,
                            fieldImpulseSpeedMillimetersPerSecond: 3000,
                            fieldAccelerationMillimetersPerSecondSquared: 0,
                            fieldLinearFalloffMinimumScaleMilli: 1000,
                            fieldMaxSpeedMillimetersPerSecond: 5000,
                            fieldReferenceMassGrams: 70000,
                            fieldMinimumMassScaleMilli: 500,
                            fieldMaximumMassScaleMilli: 1500,
                            fieldUseMassScale: true,
                            fieldUsePushScale: true,
                            fieldUseMaterialPushScale: false)
                    }));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                interaction: new CharacterTestRuntimeAiLabFrameSyncInteractionSample(
                    lastFrameEventCount: 2,
                    lastFrameEventHash: 2002L,
                    vfxFrame: 21,
                    vfxProcessedInteractionCount: 2,
                    vfxSpawnedCount: 0,
                    vfxSkippedCount: 1,
                    vfxLastStableId: CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                    vfxLastForceMode: (int)CharacterTestInteractionForceMode.AttractionAcceleration,
                    vfxLastFieldShape: (int)CharacterTestInteractionFieldShape.Capsule,
                    vfxLastVfxDefinitionId: CharacterTestVfxIds.MeteorBarrageImpactDefinitionId,
                    lastFrameEvents: new[]
                    {
                        new CharacterTestRuntimeAiLabFrameSyncInteractionEventSample(
                            frame: 21,
                            kind: (int)CharacterTestInteractionKind.PhysicalField,
                            stableId: CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                            sourceOrder: 1,
                            sourceEntityId: 1,
                            targetEntityId: 2,
                            anchorXRaw: 1000,
                            anchorYRaw: 0,
                            anchorZRaw: 2500,
                            directionXRaw: 0,
                            directionYRaw: 0,
                            directionZRaw: 1000000,
                            radiusRaw: 3000000,
                            intensity: 3000,
                            traceId: 44,
                            sourceDomain: CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse,
                            fieldShape: (int)CharacterTestInteractionFieldShape.Capsule,
                            fieldForceMode: (int)CharacterTestInteractionForceMode.AttractionAcceleration,
                            fieldTargetFilter: (int)CharacterTestInteractionTargetFilter.DynamicCharacters,
                            fieldCapsuleBottomOffsetRaw: -1000000,
                            fieldCapsuleTopOffsetRaw: 2000000,
                            fieldImpulseSpeedMillimetersPerSecond: 0,
                            fieldAccelerationMillimetersPerSecondSquared: 4500,
                            fieldLinearFalloffMinimumScaleMilli: 1000,
                            fieldMaxSpeedMillimetersPerSecond: 5000,
                            fieldReferenceMassGrams: 70000,
                            fieldMinimumMassScaleMilli: 500,
                            fieldMaximumMassScaleMilli: 1500,
                            fieldUseMassScale: true,
                            fieldUsePushScale: false,
                            fieldUseMaterialPushScale: false)
                    }));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "lastFrameEventCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "lastFrameEventHash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "vfx.spawnedCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "vfx.lastStableId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "event.frame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "event.anchorZRaw"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "interaction" && row.EntityId == 0 && row.Field == "event.fieldForceMode"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsHookPullMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                hookPull: CreateHookPullSyncSample(
                    endFrame: 90L,
                    stopDistanceMillimeters: 1000));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                hookPull: CreateHookPullSyncSample(
                    endFrame: 94L,
                    stopDistanceMillimeters: 1500));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "hookPull" && row.EntityId == 2 && row.Field == "pull.endFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "hookPull" && row.EntityId == 2 && row.Field == "pull.stopDistanceMillimeters"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsArcMineMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                arcMine: CreateArcMineSyncSample(
                    detonateFrame: 90L,
                    centerXRaw: 1000L));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                arcMine: CreateArcMineSyncSample(
                    detonateFrame: 94L,
                    centerXRaw: 2000L));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "arcMine" && row.EntityId == 1 && row.Field == "mine.detonateFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "arcMine" && row.EntityId == 1 && row.Field == "mine.centerXRaw"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsRuneTurretMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                runeTurret: CreateRuneTurretSyncSample(
                    nextThinkFrame: 140L,
                    shotsFired: 1));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                runeTurret: CreateRuneTurretSyncSample(
                    nextThinkFrame: 148L,
                    shotsFired: 2));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runeTurret" && row.EntityId == 1 && row.Field == "turret.nextThinkFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runeTurret" && row.EntityId == 1 && row.Field == "turret.shotsFired"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsAreaConfinementMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                areaConfinement: CreateAreaConfinementSyncSample(
                    activationFrame: 80L,
                    centerXRaw: 1000L));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                areaConfinement: CreateAreaConfinementSyncSample(
                    activationFrame: 86L,
                    centerXRaw: 2000L));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "areaConfinement"
                    && row.EntityId == 1
                    && row.Field == "pending.activationFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "areaConfinement"
                    && row.EntityId == 1
                    && row.Field == "source.centerXRaw"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsPoisonBottlePoolMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                poisonBottlePool: CreatePoisonBottlePoolSyncSample(
                    centerXRaw: 1000L,
                    appliedOrRefreshedCount: 1,
                    lastPoisonTickTargetCount: 0));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                poisonBottlePool: CreatePoisonBottlePoolSyncSample(
                    centerXRaw: 2000L,
                    appliedOrRefreshedCount: 0,
                    lastPoisonTickTargetCount: 1));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "poisonPool" && row.EntityId == 1 && row.Field == "source.centerXRaw"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "poisonPool" && row.EntityId == 0 && row.Field == "lastTick.appliedOrRefreshedCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "poisonPool" && row.EntityId == 0 && row.Field == "lastPoisonTickTargetCount"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsPhysicalShieldMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                physicalShield: CreatePhysicalShieldSyncSample(
                    centerXRaw: 1000L,
                    currentCohesion: 64,
                    lastTickCreatedOrRefreshedCount: 1));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                physicalShield: CreatePhysicalShieldSyncSample(
                    centerXRaw: 2000L,
                    currentCohesion: 42,
                    lastTickCreatedOrRefreshedCount: 0));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "physicalShield"
                    && row.EntityId == CharacterTestPhysicalShieldRuntime.ShieldEntityIdStart + 1
                    && row.Field == "active.centerXRaw"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "physicalShield"
                    && row.EntityId == CharacterTestPhysicalShieldRuntime.ShieldEntityIdStart + 1
                    && row.Field == "shield.currentCohesion"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "physicalShield"
                    && row.EntityId == 0
                    && row.Field == "lastTick.createdOrRefreshedCount"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsGuardAuraMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                guardAura: new CharacterTestRuntimeAiLabFrameSyncGuardAuraSample(
                    sourceCount: 1,
                    queryCount: 1,
                    appliedOrRefreshedCount: 1,
                    rejectedTargetCount: 0,
                    skippedMissingDefinitionCount: 0,
                    skippedMissingCombatMapCount: 0,
                    skippedMissingAnchorCount: 0,
                    skippedMissingGameplayTargetCount: 0,
                    onHitResultCount: 1,
                    onHitAcceptedDamageCount: 1,
                    onHitRequestedBuffCount: 1,
                    onHitSkippedResultCount: 0,
                    onHitSkippedMissingGameplayTargetCount: 0));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                guardAura: new CharacterTestRuntimeAiLabFrameSyncGuardAuraSample(
                    sourceCount: 1,
                    queryCount: 1,
                    appliedOrRefreshedCount: 0,
                    rejectedTargetCount: 1,
                    skippedMissingDefinitionCount: 0,
                    skippedMissingCombatMapCount: 0,
                    skippedMissingAnchorCount: 0,
                    skippedMissingGameplayTargetCount: 1,
                    onHitResultCount: 1,
                    onHitAcceptedDamageCount: 0,
                    onHitRequestedBuffCount: 0,
                    onHitSkippedResultCount: 1,
                    onHitSkippedMissingGameplayTargetCount: 1));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "guardAura"
                    && row.EntityId == 0
                    && row.Field == "lastTick.appliedOrRefreshedCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "guardAura"
                    && row.EntityId == 0
                    && row.Field == "lastTick.rejectedTargetCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "guardAura"
                    && row.EntityId == 0
                    && row.Field == "onHit.acceptedDamageCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "guardAura"
                    && row.EntityId == 0
                    && row.Field == "onHit.requestedBuffCount"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_CapturesGuardAuraRefreshSummary()
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity caster);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, caster);
            caster.EnqueueGameplayRequest(new CharacterActionGameplayRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(6),
                    instanceId: 1,
                    planId: 1,
                    actionId: CharacterTestCharacterActionConfigSource.GuardAuraActionId,
                    localFrame: 6,
                    stableEventId: CharacterTestCharacterActionConfigSource.GuardAuraStableEventId,
                    sourceId: caster.Id.Value,
                    traceId: new RuntimeTraceIdentity(931776)),
                caster.GameplayEntityId,
                CharacterActionTrackEventKind.SendGameplayRequest,
                CharacterTestCharacterActionConfigSource.GuardAuraGameplayRequestId,
                CharacterTestAbilityIds.GuardAuraAbilityId));

            combat.Tick(world, null, new RuntimeFrame(6));
            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    new RuntimeFrame(6),
                    stateHash: 0L,
                    world.Entities,
                    actionRuntime: null,
                    gameplayComponentWorld: world.GameplayComponentWorld,
                    guardAuraLastTickSummary: combat.GuardAuraLastTickSummary,
                    onHitBuffLastSummary: combat.OnHitBuffLastSummary);

            Assert.AreEqual(1, sample.GuardAura.SourceCount);
            Assert.AreEqual(1, sample.GuardAura.QueryCount);
            Assert.AreEqual(1, sample.GuardAura.AppliedOrRefreshedCount);
            Assert.AreEqual(0, sample.GuardAura.RejectedTargetCount);
            Assert.AreEqual(0, sample.GuardAura.OnHitRequestedBuffCount);
            Assert.AreEqual(1, sample.Characters.Count);
            Assert.AreEqual(caster.Id.Value, sample.Characters[0].EntityId);
            Assert.AreEqual(1, sample.Characters[0].BuffCount);
            Assert.IsTrue(
                HasFrameSyncBuffEntry(
                    sample.Characters[0],
                    CharacterTestGuardAuraIds.BuffId),
                "Guard Aura buff should be captured as a field-level buff entry.");

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(sample, sample);
            Assert.IsTrue(report.Passed, report.SummaryText);
        }
    }
}
