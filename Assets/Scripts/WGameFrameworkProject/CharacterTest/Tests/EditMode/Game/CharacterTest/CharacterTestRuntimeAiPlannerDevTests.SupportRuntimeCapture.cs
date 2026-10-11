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

        private static CharacterTestRuntimeAiLabFrameSyncArcMineSample CreateArcMineSyncSample(
            long detonateFrame,
            long centerXRaw)
        {
            return new CharacterTestRuntimeAiLabFrameSyncArcMineSample(
                runtimeAvailable: true,
                schemaVersion: CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                moduleId: CharacterTestArcMineRuntimeState.DefaultModuleId,
                nextRuntimeId: 2,
                activeMines: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample(
                        runtimeId: 1,
                        casterEntityId: 1,
                        sourceCombatEntityId: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        actionInstanceId: 33,
                        sourceOrder: 2,
                        localFrame: 18,
                        createdFrame: 20L,
                        armedFrame: 20L,
                        expireFrame: 320L,
                        fuseStartedFrame: 70L,
                        detonateFrame: detonateFrame,
                        triggerTargetEntityId: 2,
                        centerXRaw: centerXRaw,
                        centerYRaw: 0L,
                        centerZRaw: 0L,
                        forwardXRaw: Fix64.One.RawValue,
                        forwardYRaw: 0L,
                        forwardZRaw: 0L,
                        traceId: CharacterTestCharacterActionConfigSource.ArcMineDeployStableEventId)
                });
        }

        private static CharacterTestRuntimeAiLabFrameSyncRuneTurretSample CreateRuneTurretSyncSample(
            long nextThinkFrame,
            int shotsFired)
        {
            return new CharacterTestRuntimeAiLabFrameSyncRuneTurretSample(
                runtimeAvailable: true,
                schemaVersion: CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                moduleId: CharacterTestRuneTurretRuntimeState.DefaultModuleId,
                nextRuntimeId: 2,
                activeTurrets: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample(
                        runtimeId: 1,
                        casterEntityId: 1,
                        sourceCombatEntityId: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        actionInstanceId: 44,
                        sourceOrder: 3,
                        localFrame: 24,
                        createdFrame: 20L,
                        activateFrame: 50L,
                        expireFrame: 420L,
                        nextThinkFrame: nextThinkFrame,
                        shotsFired: shotsFired,
                        centerXRaw: 1000L,
                        centerYRaw: 0L,
                        centerZRaw: 2000L,
                        forwardXRaw: Fix64.One.RawValue,
                        forwardYRaw: 0L,
                        forwardZRaw: 0L,
                        traceId: CharacterTestCharacterActionConfigSource.RuneTurretDeployStableEventId)
                });
        }

        private static CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample CreateAreaConfinementSyncSample(
            long activationFrame,
            long centerXRaw)
        {
            return new CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample(
                runtimeAvailable: true,
                schemaVersion: CharacterTestAreaConfinementRuntimeState.CurrentSchemaVersion,
                moduleId: CharacterTestAreaConfinementRuntimeState.DefaultModuleId,
                nextPendingChannelId: 2,
                pendingChannels: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample(
                        pendingChannelId: 1,
                        casterEntityId: 1,
                        sourceGameplayEntityIndex: 1,
                        sourceGameplayEntityGeneration: 1,
                        sourceCombatEntityId: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        centerXRaw: 1200L,
                        centerYRaw: 0L,
                        centerZRaw: 1600L,
                        forwardXRaw: Fix64.One.RawValue,
                        forwardYRaw: 0L,
                        forwardZRaw: 0L,
                        landingMode: (int)CharacterTestAreaConfinementLandingMode.LockedTarget,
                        startFrame: 20L,
                        activationFrame: activationFrame,
                        localFrame: 18,
                        sourceOrder: 2,
                        traceId: CharacterTestCharacterActionConfigSource.AreaConfinementStableEventId)
                },
                sourceRuntimeAvailable: true,
                sourceSchemaVersion: CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion,
                sourceModuleId: CharacterTestAreaConfinementRuntimeState.SourceRuntimeModuleId,
                sourceNextRuntimeSourceId: 2,
                activeSources: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample(
                        runtimeSourceId: 1,
                        sourceDefinitionId: CharacterTestAreaConfinementIds.MechanicSourceId,
                        sourceGameplayEntityIndex: 1,
                        sourceGameplayEntityGeneration: 1,
                        sourceCombatEntityId: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        centerXRaw: centerXRaw,
                        centerYRaw: 0L,
                        centerZRaw: 2000L,
                        startFrame: 80L,
                        endFrame: 260L,
                        traceId: CharacterTestCharacterActionConfigSource.AreaConfinementZoneVfxStableEventId)
                });
        }

        private static CharacterTestRuntimeAiLabFrameSyncHookPullSample CreateHookPullSyncSample(
            long endFrame,
            int stopDistanceMillimeters)
        {
            return new CharacterTestRuntimeAiLabFrameSyncHookPullSample(
                runtimeAvailable: true,
                schemaVersion: CharacterTestHookPullRuntimeState.CurrentSchemaVersion,
                moduleId: CharacterTestHookPullRuntimeState.DefaultModuleId,
                activePulls: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample(
                        casterEntityId: 1,
                        targetEntityId: 2,
                        startFrame: 20L,
                        endFrame: endFrame,
                        actionInstanceId: 33,
                        projectileId: 101,
                        stableEventId: CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId,
                        stopDistanceMillimeters: stopDistanceMillimeters,
                        anchorForwardOffsetMillimeters: 250,
                        accelerationMillimetersPerSecondSquared: 8000,
                        terminalSpeedMillimetersPerSecond: 12000)
                });
        }

        private static CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample CreatePoisonBottlePoolSyncSample(
            long centerXRaw,
            int appliedOrRefreshedCount,
            int lastPoisonTickTargetCount)
        {
            return new CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample(
                runtimeAvailable: true,
                schemaVersion: CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion,
                moduleId: CharacterTestPoisonBottlePoolRuntime.SourceRuntimeModuleId,
                nextRuntimeSourceId: 2,
                activeSources: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample(
                        runtimeSourceId: 1,
                        sourceDefinitionId: CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                        sourceGameplayEntityIndex: 1,
                        sourceGameplayEntityGeneration: 1,
                        sourceCombatEntityId: 1,
                        centerXRaw: centerXRaw,
                        centerYRaw: 0L,
                        centerZRaw: 0L,
                        startFrame: 10L,
                        endFrame: 130L,
                        traceId: CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventId)
                },
                lastTickActivePoolCount: 1,
                lastTickCreatedPoolCount: 0,
                lastTickExpiredPoolCount: 0,
                lastTickQueryCount: 1,
                lastTickQueryHitCount: appliedOrRefreshedCount > 0 ? 1 : 0,
                lastTickAppliedOrRefreshedCount: appliedOrRefreshedCount,
                lastTickRejectedTargetCount: 0,
                lastTickSkippedMissingGameplayTargetCount: 0,
                lastPoisonTickTargetCount: lastPoisonTickTargetCount);
        }

        private static CharacterTestMechanicSourceRuntimeState CreatePoisonBottlePoolRuntimeState(
            long centerXRaw)
        {
            return new CharacterTestMechanicSourceRuntimeState(
                CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion,
                CharacterTestPoisonBottlePoolRuntime.SourceRuntimeModuleId,
                nextRuntimeSourceId: 2,
                activeSources: new[]
                {
                    new CharacterTestMechanicSourceSnapshot(
                        runtimeSourceId: 1,
                        sourceDefinitionId: CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                        sourceGameplayEntityIndex: 1,
                        sourceGameplayEntityGeneration: 1,
                        sourceCombatEntityId: 1,
                        centerXRaw: centerXRaw,
                        centerYRaw: 0L,
                        centerZRaw: 0L,
                        startFrame: 10L,
                        endFrame: 130L,
                        traceId: CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventId)
                    });
        }

        private static CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample CreatePhysicalShieldSyncSample(
            long centerXRaw,
            int currentCohesion,
            int lastTickCreatedOrRefreshedCount)
        {
            int shieldEntityId = CharacterTestPhysicalShieldRuntime.ShieldEntityIdStart + 1;
            int bodyId = CharacterTestPhysicalShieldRuntime.ShieldBodyIdStart + 1;
            return new CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample(
                runtimeAvailable: true,
                schemaVersion: CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion,
                moduleId: CharacterTestPhysicalShieldRuntimeState.ModuleIdValue,
                activeShields: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample(
                        ownerId: 1,
                        shieldEntityId: shieldEntityId,
                        bodyId: bodyId,
                        centerXRaw: centerXRaw,
                        centerYRaw: Fix64.One.RawValue,
                        centerZRaw: 0L,
                        endFrame: 360L)
                },
                shieldRuntimeAvailable: true,
                shieldRuntimeSchemaVersion: CombatShieldRuntimeState.CurrentSchemaVersion,
                 shieldRuntimeModuleId: CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleIdentity,
                shields: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncShieldPotencySample(
                        shieldEntityId: shieldEntityId,
                        ownerEntityId: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        bodyId: bodyId,
                        colliderId: CharacterTestPhysicalShieldRuntime.ShieldColliderId,
                        baseMotion: 0,
                        baseCohesion: 64,
                        baseScatter: 0,
                        currentMotion: 0,
                        currentCohesion: currentCohesion,
                        currentScatter: 0,
                        baseCohesionLimit: CharacterTestPhysicalShieldRuntime.BaseCohesionLimit,
                        currentCohesionLimit: CharacterTestPhysicalShieldRuntime.BaseCohesionLimit,
                        recoveryRate: CharacterTestPhysicalShieldRuntime.RecoveryRate,
                        recoveryDelayFrames: CharacterTestPhysicalShieldRuntime.RecoveryDelayFrames,
                        lastImpactFrame: 42L,
                        currentBand: (int)CombatShieldPressureBand.Stable,
                        isBroken: false,
                        shieldPressure: Math.Max(0, 64 - currentCohesion))
                },
                lastTickActiveCount: 1,
                lastTickCreatedOrRefreshedCount: lastTickCreatedOrRefreshedCount,
                lastTickExpiredCount: 0);
        }

        private static CharacterTestPhysicalShieldRuntimeState CreatePhysicalShieldRuntimeState(
            long centerXRaw,
            int currentCohesion)
        {
            int shieldEntityId = CharacterTestPhysicalShieldRuntime.ShieldEntityIdStart + 1;
            int bodyId = CharacterTestPhysicalShieldRuntime.ShieldBodyIdStart + 1;
            var active = new[]
            {
                new CharacterTestPhysicalShieldActiveSnapshot(
                    ownerId: 1,
                    shieldEntityId: shieldEntityId,
                    bodyId: bodyId,
                    centerXRaw: centerXRaw,
                    centerYRaw: Fix64.One.RawValue,
                    centerZRaw: 0L,
                    endFrame: 360L)
            };
            var shieldRuntime = new CombatShieldRuntimeState(
                CombatShieldRuntimeState.CurrentSchemaVersion,
                CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleId,
                new[]
                {
                    new CombatShieldPotencyComponent(
                        new CombatEntityId(shieldEntityId),
                        new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1),
                        new CombatBodyId(bodyId),
                        new CombatColliderId(CharacterTestPhysicalShieldRuntime.ShieldColliderId),
                        new CombatShieldPotencyTriplet(0, 64, 0),
                        new CombatShieldPotencyTriplet(0, currentCohesion, 0),
                        CharacterTestPhysicalShieldRuntime.BaseCohesionLimit,
                        CharacterTestPhysicalShieldRuntime.BaseCohesionLimit,
                        CharacterTestPhysicalShieldRuntime.RecoveryRate,
                        CharacterTestPhysicalShieldRuntime.RecoveryDelayFrames,
                        lastImpactFrame: 42L,
                        CombatShieldPressureBand.Stable,
                        isBroken: false)
                });
            return new CharacterTestPhysicalShieldRuntimeState(
                CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion,
                CharacterTestPhysicalShieldRuntimeState.ModuleIdValue,
                active,
                shieldRuntime);
        }

        private static CharacterTestRuntimeAiLabFrameSyncProjectileSample CreateProjectileSyncSample(
            int localFrame,
            long positionXRaw)
        {
            return new CharacterTestRuntimeAiLabFrameSyncProjectileSample(
                projectileId: 101,
                spawnSequence: 1,
                definitionId: CharacterTestFireballProjectileAdapter.DefinitionId,
                definitionStableId: CharacterTestFireballProjectileAdapter.StableId,
                sourceEntityId: 1,
                actionId: CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId),
                actionInstanceId: 12,
                traceId: 3001,
                localFrame: localFrame,
                spawnFrame: 10,
                lastFrame: 10 + localFrame,
                ageFrames: localFrame,
                positionXRaw: positionXRaw,
                positionYRaw: Fix64.One.RawValue,
                positionZRaw: Fix64.Zero.RawValue,
                directionXRaw: Fix64.Zero.RawValue,
                directionYRaw: Fix64.Zero.RawValue,
                directionZRaw: Fix64.One.RawValue,
                originXRaw: 0L,
                originYRaw: Fix64.One.RawValue,
                originZRaw: 0L,
                hasTargetPoint: false,
                targetPointXRaw: 0L,
                targetPointYRaw: 0L,
                targetPointZRaw: 0L,
                effectiveLifetimeFrames: 60,
                launchSpeedPerFrameRaw: Fix64.FromRatio(35, 100).RawValue,
                hasTrackingTarget: false,
                trackingTargetEntityId: 0,
                acceptedImpactCount: 0,
                acceptedImpactHash: 17L);
        }

        private static bool TryFindProjectileSample(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncProjectileSample> samples,
            int projectileId,
            out CharacterTestRuntimeAiLabFrameSyncProjectileSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncProjectileSample candidate = samples[i];
                if (candidate.ProjectileId != projectileId)
                    continue;

                sample = candidate;
                return true;
            }

            sample = default;
            return false;
        }

        private static void AssertProjectileSampleMatches(
            string label,
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expected,
            CharacterTestRuntimeAiLabFrameSyncProjectileSample actual)
        {
            string prefix = label + " should replay ";
            Assert.AreEqual(expected.ProjectileId, actual.ProjectileId, prefix + "id.");
            Assert.AreEqual(expected.SpawnSequence, actual.SpawnSequence, prefix + "spawn sequence.");
            Assert.AreEqual(expected.DefinitionId, actual.DefinitionId, prefix + "definition id.");
            Assert.AreEqual(expected.DefinitionStableId, actual.DefinitionStableId, prefix + "definition stable id.");
            Assert.AreEqual(expected.SourceEntityId, actual.SourceEntityId, prefix + "source entity.");
            Assert.AreEqual(expected.ActionId, actual.ActionId, prefix + "action id.");
            Assert.AreEqual(expected.ActionInstanceId, actual.ActionInstanceId, prefix + "action instance id.");
            Assert.AreEqual(expected.TraceId, actual.TraceId, prefix + "trace id.");
            Assert.AreEqual(expected.LocalFrame, actual.LocalFrame, prefix + "local frame.");
            Assert.AreEqual(expected.SpawnFrame, actual.SpawnFrame, prefix + "spawn frame.");
            Assert.AreEqual(expected.LastFrame, actual.LastFrame, prefix + "last frame.");
            Assert.AreEqual(expected.AgeFrames, actual.AgeFrames, prefix + "age.");
            Assert.AreEqual(expected.OriginXRaw, actual.OriginXRaw, prefix + "origin X.");
            Assert.AreEqual(expected.OriginYRaw, actual.OriginYRaw, prefix + "origin Y.");
            Assert.AreEqual(expected.OriginZRaw, actual.OriginZRaw, prefix + "origin Z.");
            Assert.AreEqual(expected.HasTargetPoint, actual.HasTargetPoint, prefix + "target point availability.");
            if (expected.HasTargetPoint)
            {
                Assert.AreEqual(expected.TargetPointXRaw, actual.TargetPointXRaw, prefix + "target point X.");
                Assert.AreEqual(expected.TargetPointYRaw, actual.TargetPointYRaw, prefix + "target point Y.");
                Assert.AreEqual(expected.TargetPointZRaw, actual.TargetPointZRaw, prefix + "target point Z.");
            }

            Assert.AreEqual(expected.EffectiveLifetimeFrames, actual.EffectiveLifetimeFrames, prefix + "lifetime.");
            Assert.AreEqual(expected.LaunchSpeedPerFrameRaw, actual.LaunchSpeedPerFrameRaw, prefix + "launch speed.");
            Assert.AreEqual(expected.PositionXRaw, actual.PositionXRaw, prefix + "position X.");
            Assert.AreEqual(expected.PositionYRaw, actual.PositionYRaw, prefix + "position Y.");
            Assert.AreEqual(expected.PositionZRaw, actual.PositionZRaw, prefix + "position Z.");
            Assert.AreEqual(expected.DirectionXRaw, actual.DirectionXRaw, prefix + "direction X.");
            Assert.AreEqual(expected.DirectionYRaw, actual.DirectionYRaw, prefix + "direction Y.");
            Assert.AreEqual(expected.DirectionZRaw, actual.DirectionZRaw, prefix + "direction Z.");
            Assert.AreEqual(expected.AcceptedImpactCount, actual.AcceptedImpactCount, prefix + "accepted impact count.");
            Assert.AreEqual(expected.AcceptedImpactHash, actual.AcceptedImpactHash, prefix + "accepted impact hash.");
        }

        private static RuntimeAiLabHeadlessFrameRecord CaptureHeadlessRecord(
            CharacterTestSliceTestHarness harness,
            CharacterTestNetworkSnapshotParticipant participant,
            bool createCheckpoint,
            bool includeRuntimeEvents = false)
        {
            int frame = harness.CurrentFrame.Value > int.MaxValue
                ? int.MaxValue
                : (int)harness.CurrentFrame.Value;
            RuntimeCommand[] commands = CopyCommands(harness.Modules.RuntimeCommands.LastDrainedCommands);
            RuntimeCustomState checkpoint = createCheckpoint
                ? participant.CaptureSnapshot(harness.CurrentFrame)
                : null;
            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    harness.Slice,
                    participant,
                    selectedFrame: null,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("headless"),
                    includeRuntimeEvents);
            CharacterTestRuntimeAiLabAuthoritySample authority =
                CharacterTestRuntimeAiLabAuthorityProbe.Capture(harness.Slice, participant);
            return new RuntimeAiLabHeadlessFrameRecord(
                frame,
                harness.Services.ElapsedSeconds,
                commands,
                checkpoint,
                sample,
                authority);
        }

        private static RuntimeAiLabHeadlessFrameRecord CaptureHeadlessPlannerRecord(
            CharacterTestSliceTestHarness harness,
            CharacterTestNetworkSnapshotParticipant participant,
            bool createCheckpoint)
        {
            int frame = harness.CurrentFrame.Value > int.MaxValue
                ? int.MaxValue
                : (int)harness.CurrentFrame.Value;
            RuntimeCommand[] commands = CopyCommands(harness.Modules.RuntimeCommands.LastDrainedCommands);
            RuntimeCustomState checkpoint = createCheckpoint
                ? participant.CaptureSnapshot(harness.CurrentFrame)
                : null;
            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            var selectedFrame = new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                frame,
                (float)harness.Services.ElapsedSeconds,
                snapshot);
            CharacterTestRuntimeAiPlannerDevPreviewItem preview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(
                    selectedFrame,
                    selectedAgentIndex: 0,
                    overlayEnabled: true);
            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    harness.Slice,
                    participant,
                    selectedFrame,
                    preview);
            CharacterTestRuntimeAiLabAuthoritySample authority =
                CharacterTestRuntimeAiLabAuthorityProbe.Capture(harness.Slice, participant);
            return new RuntimeAiLabHeadlessFrameRecord(
                frame,
                harness.Services.ElapsedSeconds,
                commands,
                checkpoint,
                sample,
                authority);
        }

        private static CharacterTestSliceOptions CreateRuntimeAiPlannerFrameSyncSliceOptions()
        {
            return new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>()
            };
        }

        private static void SpawnRuntimeAiPlannerFrameSyncAgent(
            CharacterTestSliceTestHarness harness,
            int teamId,
            float x,
            float z,
            RuntimeTraceIdentity traceId)
        {
            var spec = new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                teamId,
                x,
                1f,
                z,
                CharacterTestCharacterControlOwner.AiControlled);
            RuntimeCommandValidationResult spawn = harness.Enqueue(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    harness.CurrentFrame,
                    CharacterTestRuntimeCommandSources.System,
                    spec,
                    traceId));
            Assert.IsTrue(spawn.Success, spawn.Error.Message);
        }

        private static void AssertPlannerMovementSampleAvailable(
            CharacterTestRuntimeAiLabFrameSyncSample sample,
            string label)
        {
            Assert.IsTrue(sample.Planner.Available, label + " Runtime AI Planner snapshot should be available.");
            Assert.Greater(sample.Planner.AgentCount, 0, label + " should contain Runtime AI Planner agents.");
            CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample agent = sample.Planner.Agents[0];
            Assert.Greater(agent.EntityId, 0, label + " planner agent should keep a stable entity id.");
            Assert.Greater(agent.TargetEntityId, 0, label + " planner agent should have a selected target.");
            Assert.GreaterOrEqual(
                agent.MovementSpeedScaleMilli,
                0,
                label + " planner movement speed scale should be sampled.");
            Assert.GreaterOrEqual(
                agent.MovementDirectionHoldFrames,
                0,
                label + " planner movement hold frame count should be sampled.");
            Assert.GreaterOrEqual(
                agent.MovementReactionDelayFrames,
                0,
                label + " planner movement reaction delay should be sampled.");
        }

        private static CharacterTestSliceOptions CreateFrameSyncAudioSliceOptions()
        {
            CharacterTestAudioCueCatalog catalog = CharacterTestAudioCueCatalog.CreateDefault();
            return new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                AudioCueCatalog = catalog,
                AudioService = new AudioService(catalog, new NullAudioBackend()),
                DisposeAudioService = true
            };
        }

        private static CharacterActionAudioCueRequest CreateFrameSyncAudioCueRequest(
            RuntimeFrame frame,
            int entityId)
        {
            string targetActorId = entityId.ToString(CultureInfo.InvariantCulture);
            return new CharacterActionAudioCueRequest(
                new CharacterActionAdapterDispatchMetadata(
                    frame,
                    instanceId: entityId,
                    planId: 1,
                    actionId: CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    localFrame: 0,
                    stableEventId: CharacterTestAudioCueCatalog.LightAttackCueId,
                    sourceId: entityId,
                    traceId: new RuntimeTraceIdentity(990306)),
                targetActorId,
                CharacterTestAudioCueCatalog.LightAttackCueId);
        }

        private static VfxSpawnRequest EnqueueFrameSyncVfxSpawn(
            CharacterTestSliceTestHarness harness,
            RuntimeFrame frame,
            int sourceId,
            int targetId,
            int traceId)
        {
            var request = new VfxSpawnRequest(
                frame,
                VfxSourceDomains.Gameplay,
                frame.Value,
                localFrame: 7,
                sourceOrder: 3,
                triggerId: 4242,
                sourceId: sourceId,
                targetId: targetId,
                definitionId: new VfxDefinitionId(CharacterTestVfxIds.FireballExplosionImpactDefinitionId),
                anchor: VfxAnchor.World(new VfxPose(
                    VfxVector3.FromRaw(1_250_000L, 500_000L, -750_000L),
                    VfxVector3.FromRaw(0L, 0L, VfxVector3.Scale))),
                parameters: new VfxParameterBlock(11, 22, 33, 44),
                lifetimeFramesOverride: 32,
                traceId: new RuntimeTraceIdentity(traceId));

            return harness.Modules.VfxRuntime.EnqueueSpawnRequest(request);
        }

        private static RuntimeAiLabHeadlessFrameRecord CaptureHeadlessRecord(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant participant,
            bool createCheckpoint)
        {
            int frame = slice.CurrentFrame.Value > int.MaxValue
                ? int.MaxValue
                : (int)slice.CurrentFrame.Value;
            RuntimeCommand[] commands = CopyCommands(slice.RuntimeCommands.LastDrainedCommands);
            RuntimeCustomState checkpoint = createCheckpoint
                ? participant.CaptureSnapshot(slice.CurrentFrame)
                : null;
            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    slice,
                    participant,
                    selectedFrame: null,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("headless"));
            CharacterTestRuntimeAiLabAuthoritySample authority =
                CharacterTestRuntimeAiLabAuthorityProbe.Capture(slice, participant);
            return new RuntimeAiLabHeadlessFrameRecord(
                frame,
                slice.ElapsedSeconds,
                commands,
                checkpoint,
                sample,
                authority);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample CaptureSceneControllerFrameSyncSample(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            GameManager manager)
        {
            CharacterTestRuntimeAiPlannerDevSceneSnapshot sceneSnapshot = controller.CaptureSnapshot();
            CharacterTestRuntimeAiPlannerDevPreviewItem preview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(
                    sceneSnapshot.SelectedFrame,
                    sceneSnapshot.SelectedAgentIndex,
                    controller.PreviewOverlayEnabled);
            GameSlice slice = manager?.RuntimeContext?.Slice;
            CharacterTestNetworkSnapshotParticipant participant =
                slice?.Services.Network.CreateSnapshotParticipant();
            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                slice,
                participant,
                sceneSnapshot.SelectedFrame,
                preview);
        }

        private static CharacterTestRuntimeAiLabAuthoritySample CaptureSceneControllerAuthoritySample(
            GameManager manager)
        {
            GameSlice slice = manager?.RuntimeContext?.Slice;
            CharacterTestNetworkSnapshotParticipant participant =
                slice?.Services.Network.CreateSnapshotParticipant();
            return CharacterTestRuntimeAiLabAuthorityProbe.Capture(slice, participant);
        }

        private static RuntimeCommand[] CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            int count = commands != null ? commands.Count : 0;
            if (count <= 0)
                return Array.Empty<RuntimeCommand>();

            var copy = new RuntimeCommand[count];
            for (int i = 0; i < count; i++)
                copy[i] = commands[i];
            return copy;
        }
    }
}
