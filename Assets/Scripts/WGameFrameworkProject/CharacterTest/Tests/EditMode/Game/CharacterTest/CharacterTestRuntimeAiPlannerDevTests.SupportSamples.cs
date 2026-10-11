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

        private static CharacterTestRuntimeAiPlannerSnapshot CreateSnapshot(string profileId)
        {
            return new CharacterTestRuntimeAiPlannerSnapshot(
                available: true,
                profileId: profileId,
                agents: Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>(),
                validationReport: "ok");
        }

        private static CharacterTestAuthorityRollbackCheckpoint CreateCheckpoint(int frame)
        {
            return new CharacterTestAuthorityRollbackCheckpoint(
                new RuntimeFrame(frame),
                new CharacterTestNetworkSnapshotDocument
                {
                    SchemaVersion = CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    Frame = frame
                });
        }

        private static void AddLoopTimelineRecord(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int frame,
            bool hasGameplayCheckpoint)
        {
            timeline.AddRecord(
                frame,
                frame / 60f,
                frame / 60d,
                CreateSnapshot("loop-" + frame),
                Array.Empty<RuntimeCommand>(),
                hasGameplayCheckpoint ? CreateCheckpoint(frame) : null,
                frame);
        }

        private static CharacterTestRuntimeAiPlannerAgentSnapshot CreatePreviewAgent(
            bool targetExists = true,
            bool targetVisible = true,
            bool targetLineOfSightBlocked = false,
            bool targetInAttackRange = true,
            bool movePathAvailable = true,
            bool moveDirectPathClear = true,
            bool environmentNearHazard = false,
            bool hasScenePreviewGeometry = true,
            bool hasDistanceWitness = true,
            string selectedActionName = "approach")
        {
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                entityId: 3,
                targetEntityId: 9,
                activeStatePath: "root/engage/movement",
                targetExists: targetExists,
                targetInAttackRange: targetInAttackRange,
                targetDistance: 2.25f,
                movePathAvailable: movePathAvailable,
                selectedUtilityScore: 12f,
                selectedActionCost: 3f,
                selectedActionId: 7,
                selectedActionName: selectedActionName,
                selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                lastCommand: "move",
                suppressedReason: string.Empty,
                rejectReason: string.Empty,
                transitionFromStateId: 0,
                transitionToStateId: 0,
                targetVisible: targetVisible,
                targetLineOfSightBlocked: targetLineOfSightBlocked,
                moveDirectPathClear: moveDirectPathClear,
                environmentNearHazard: environmentNearHazard,
                hasScenePreviewGeometry: hasScenePreviewGeometry,
                scenePreviewSourceCenterX: 1f,
                scenePreviewSourceCenterY: 0.8f,
                scenePreviewSourceCenterZ: 2f,
                scenePreviewTargetCenterX: 3f,
                scenePreviewTargetCenterY: 0.8f,
                scenePreviewTargetCenterZ: 2.5f,
                scenePreviewDecisionMoveX: 1f,
                scenePreviewDecisionMoveZ: 0.25f,
                scenePreviewAttackRange: 2.2f,
                targetDistanceAvailable: true,
                targetRootDistance: 2.06f,
                hasScenePreviewDistanceWitness:
                    hasScenePreviewGeometry && targetExists && hasDistanceWitness,
                scenePreviewDistanceSourceX: 1.4f,
                scenePreviewDistanceSourceY: 0.8f,
                scenePreviewDistanceSourceZ: 2.1f,
                scenePreviewDistanceTargetX: 2.6f,
                scenePreviewDistanceTargetY: 0.8f,
                scenePreviewDistanceTargetZ: 2.4f,
                scenePreviewDistanceGeometryFrame: 16L,
                targetLogicalHurtboxId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0029);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample CreateFrameSyncSample(
            int xMillimeters,
            int runnerLocalFrame,
            int overlayFrame,
            float overlaySourceX,
            int health = 100,
            int lastDamage = 0,
            long lastHitFrame = -1L,
            string specialHitState = "None",
            int attributeCount = 0,
            int attributeHash = 0,
            int buffCount = 0,
            int buffHash = 0,
            int statusCount = 0,
            int statusHash = 0,
            CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample[] attributeEntries = null,
            CharacterTestRuntimeAiLabFrameSyncBuffEntrySample[] buffEntries = null,
            CharacterTestRuntimeAiLabFrameSyncStatusEntrySample[] statusEntries = null,
            CharacterTestRuntimeAiLabFrameSyncProjectileSample[] projectiles = null,
            CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample[] abilityCooldowns = null,
            CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample poisonBottlePool = default,
            CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample physicalShield = default,
            CharacterTestRuntimeAiLabFrameSyncHookPullSample hookPull = default,
            CharacterTestRuntimeAiLabFrameSyncArcMineSample arcMine = default,
            CharacterTestRuntimeAiLabFrameSyncRuneTurretSample runeTurret = default,
            CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample areaConfinement = default,
            CharacterTestRuntimeAiLabFrameSyncTornadoSample tornado = default,
            CharacterTestRuntimeAiLabFrameSyncChainLightningSample chainLightning = default,
            CharacterTestRuntimeAiLabFrameSyncLaserPathSample laserPath = default,
            CharacterTestRuntimeAiLabFrameSyncGuardAuraSample guardAura = default,
            CharacterTestRuntimeAiLabFrameSyncInteractionSample interaction = default,
            CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample[] movableNaturalObjects = null,
            CharacterTestRuntimeAiLabFrameSyncCommandStateSample commandState = default,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample vfxRuntime = default,
            CharacterTestRuntimeAiLabFrameSyncAudioSample audio = default,
            CharacterTestRuntimeAiLabFrameSyncPlannerSample planner = default,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents = default,
            string controlOwner = "AiControlled",
            int runtimeAiPlannerProfileSlot = 1,
            bool runtimeAiBehaviorHasSelection = true,
            int runtimeAiBehaviorLoadoutId = 10010,
            int runtimeAiBehaviorVariantId = 1,
            long runtimeAiBehaviorCatalogHash = 123456L,
            int runtimeAiBehaviorProfileSlot = 1,
            int physicsCapsuleRadiusMillimeters = 350,
            int physicsCapsuleHeightMillimeters = 1800,
            int physicsCapsuleCenterYMillimeters = 900,
            int physicsContactCount = 0,
            int postureProfileId = 2,
            int postureHeightMillimeters = 1800,
            int postureRadiusMillimeters = 350,
            string postureSource = "Standing",
            string presenceState = "Active",
            long presenceRequestedFrame = -1L,
            long presenceDestroyedFrame = -1L,
            RuntimeTraceIdentity presenceTraceId = default,
            bool gameplayLifecyclePresent = true,
            string gameplayLifecycleState = "Alive",
            bool gameplayTeamPresent = true,
            int gameplayTeamId = 1,
            bool gameplayScatterPresent = false,
            int gameplayScatterCurrentPressure = 0,
            int gameplayScatterMaxPressure = 0,
            int gameplayScatterMaxDebt = 0,
            int gameplayScatterCurrentCohesionLimit = 0,
            int gameplayScatterRecoveryRate = 0,
            int gameplayScatterRecoveryDelayFrames = 0,
            int gameplayScatterBrokenRecoveryDelayFrames = 0,
            string gameplayScatterCurrentBand = "",
            long gameplayScatterLastPressureFrame = 0L,
            long gameplayScatterLastCohesionBreakFrame = -1L,
            bool gameplayScatterIsBroken = false,
            string battleLifeState = "Alive",
            string battleDominantReason = "None",
            string battleGateFlags =
                "CanBeTargeted, CanReceiveHpDamage, CanReceiveScatter, CanStartNormalAction, CanMoveByInput, CanRunRuntimeAiPlanner",
            long battleSinceFrame = 0L,
            long battleLastTransitionFrame = 0L,
            short inputMoveXMilli = 250,
            short inputMoveYMilli = 0,
            short inputWorldMoveXMilli = 250,
            short inputWorldMoveYMilli = 0,
            bool inputHasWorldMove = true,
            bool inputJumpPressed = false,
            bool inputJumpHeld = false,
            bool inputJumpReleased = false,
            bool inputAttackPrimaryPressed = false,
            bool inputAttackSecondaryPressed = false,
            bool inputDebugPrimaryPressed = false,
            bool inputDebugSecondaryPressed = false,
            int inputSkillSlotPressedMask = 0,
            bool inputDodgePressed = false,
            int inputAbilitySlotPressed = 0,
            int inputAbilityIdPressed = 0,
            short lastMoveInputXMilli = 250,
            short lastMoveInputYMilli = 0,
            int jumpBufferFramesRemaining = 0,
            int coyoteTimeFramesRemaining = 0,
            string movementMode = "Run",
            bool movementLocked = false,
            int movementControlScaleMilli = 1000,
            int rotationControlScaleMilli = 1000,
            int movementProfileId = 1202101,
            bool movementProfileFallback = false,
            bool physicsEnabled = true,
            string collisionFlags = "Grounded",
            int surfaceFrictionMilli = 1000,
            int surfaceBounceSpeedCapYMillimetersPerSecond = 0,
            string surfaceMaterialKind = "Default",
            int surfaceMaterialStableId = 0,
            long surfaceElasticityRaw = 0L,
            long surfaceFixedBounceHeightRaw = 0L,
            bool hasUnityView = false,
            int unityViewXMillimeters = 0,
            bool hasPhysicsBody = false,
            long physicsBodyXRaw = 0L,
            CharacterTestRuntimeCharacterPresentationState presentationState = null)
        {
            var character = new CharacterTestRuntimeAiLabFrameSyncCharacterSample(
                entityId: 1,
                xMillimeters: xMillimeters,
                yMillimeters: 1000,
                zMillimeters: 2000,
                yawMilliDegrees: 90000,
                horizontalVelocityXMillimetersPerSecond: 250,
                horizontalVelocityYMillimetersPerSecond: 0,
                desiredVelocityXMillimetersPerSecond: 250,
                desiredVelocityYMillimetersPerSecond: 0,
                verticalVelocityMillimetersPerSecond: 0,
                isGrounded: true,
                activeActionId: new CharacterActionId(301),
                actionState: "Running",
                actionPhase: "Startup",
                actionLocalFrame: runnerLocalFrame,
                lastRejectReason: string.Empty,
                animationActionKeyThisFrame: "light_attack",
                lastAnimationActionKey: "light_attack",
                hasRunner: true,
                runnerActionId: new CharacterActionId(301),
                runnerState: "Running",
                runnerPhase: "Startup",
                runnerLocalFrame: runnerLocalFrame,
                runnerActiveInstanceId: 12L,
                runnerPlanId: 34L,
                maxHealth: 100,
                health: health,
                hitStunFramesRemaining: lastHitFrame >= 0L ? 6 : 0,
                lastDamage: lastDamage,
                lastHitFrame: lastHitFrame,
                lastHitSourceEntityId: lastHitFrame >= 0L ? 2 : 0,
                lastHitTraceId: lastHitFrame >= 0L ? 3001 : 0,
                lifeState: health > 0 ? "Alive" : "Dead",
                isAlive: health > 0,
                isDead: health <= 0,
                scatterPressure: lastHitFrame >= 0L ? 34 : 0,
                maxScatterPressure: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                scatterPressureBand: "Stable",
                lastCohesionBreakFrame: -1L,
                specialHitState: specialHitState,
                specialHitPhase: specialHitState == "Launched" ? "Enter" : "None",
                specialHitSinceFrame: specialHitState == "Launched" ? lastHitFrame : -1L,
                specialHitSourceEntityId: specialHitState == "Launched" ? 2 : 0,
                specialHitTraceId: specialHitState == "Launched" ? 3001 : 0,
                specialHitLaunchVelocityYMillimetersPerSecond: specialHitState == "Launched" ? 4500 : 0,
                specialHitHorizontalImpulseMillimetersPerSecond: 0,
                attributeCount: attributeCount,
                attributeHash: attributeHash,
                buffCount: buffCount,
                buffHash: buffHash,
                statusCount: statusCount,
                statusHash: statusHash,
                attributeEntries: attributeEntries,
                buffEntries: buffEntries,
                statusEntries: statusEntries,
                controlOwner: controlOwner,
                runtimeAiPlannerProfileSlot: runtimeAiPlannerProfileSlot,
                runtimeAiBehaviorHasSelection: runtimeAiBehaviorHasSelection,
                runtimeAiBehaviorLoadoutId: runtimeAiBehaviorLoadoutId,
                runtimeAiBehaviorVariantId: runtimeAiBehaviorVariantId,
                runtimeAiBehaviorCatalogHash: runtimeAiBehaviorCatalogHash,
                runtimeAiBehaviorProfileSlot: runtimeAiBehaviorProfileSlot,
                physicsCapsuleRadiusMillimeters: physicsCapsuleRadiusMillimeters,
                physicsCapsuleHeightMillimeters: physicsCapsuleHeightMillimeters,
                physicsCapsuleCenterYMillimeters: physicsCapsuleCenterYMillimeters,
                physicsMassGrams: 70000,
                physicsMassScaleMilli: 1000,
                physicsRadiusScaleMilli: 1000,
                physicsPushScaleMilli: 1000,
                physicsBlocksCharacters: true,
                physicsCanBePushed: true,
                physicsContactVelocityXMillimetersPerSecond: physicsContactCount > 0 ? 120 : 0,
                physicsContactVelocityYMillimetersPerSecond: 0,
                physicsContactDisplacementXMillimeters: physicsContactCount > 0 ? 15 : 0,
                physicsContactDisplacementYMillimeters: 0,
                physicsContactCount: physicsContactCount,
                postureProfileId: postureProfileId,
                postureHeightMillimeters: postureHeightMillimeters,
                postureRadiusMillimeters: postureRadiusMillimeters,
                postureCenterYMillimeters: postureHeightMillimeters / 2,
                postureBlocksCharacters: true,
                postureCanBePushed: true,
                postureSource: postureSource,
                posturePriority: 0,
                postureSinceFrame: 0L,
                postureActionProfileId: 0,
                postureActionRestorePolicy: "OnActionEnd",
                postureActionTransitionFrames: 0,
                postureActionSourceActionId: CharacterActionId.None,
                postureActionStableEventId: 0,
                presenceState: presenceState,
                presenceRequestedFrame: presenceRequestedFrame,
                presenceDestroyedFrame: presenceDestroyedFrame,
                presenceSourceEntityId: presenceRequestedFrame >= 0L ? 2 : 0,
                presenceTraceId: presenceTraceId,
                gameplayLifecyclePresent: gameplayLifecyclePresent,
                gameplayLifecycleState: gameplayLifecycleState,
                gameplayTeamPresent: gameplayTeamPresent,
                gameplayTeamId: gameplayTeamId,
                gameplayScatterPresent: gameplayScatterPresent,
                gameplayScatterCurrentPressure: gameplayScatterCurrentPressure,
                gameplayScatterMaxPressure: gameplayScatterMaxPressure,
                gameplayScatterMaxDebt: gameplayScatterMaxDebt,
                gameplayScatterCurrentCohesionLimit: gameplayScatterCurrentCohesionLimit,
                gameplayScatterRecoveryRate: gameplayScatterRecoveryRate,
                gameplayScatterRecoveryDelayFrames: gameplayScatterRecoveryDelayFrames,
                gameplayScatterBrokenRecoveryDelayFrames: gameplayScatterBrokenRecoveryDelayFrames,
                gameplayScatterCurrentBand: gameplayScatterCurrentBand,
                gameplayScatterLastPressureFrame: gameplayScatterLastPressureFrame,
                gameplayScatterLastCohesionBreakFrame: gameplayScatterLastCohesionBreakFrame,
                gameplayScatterIsBroken: gameplayScatterIsBroken,
                battleLifeState: battleLifeState,
                battleDominantReason: battleDominantReason,
                battleGateFlags: battleGateFlags,
                battleSinceFrame: battleSinceFrame,
                battleLastTransitionFrame: battleLastTransitionFrame,
                battleSourceActionId: battleDominantReason == "None" ? string.Empty : "light_attack",
                battleSourceTraceId: battleDominantReason == "None" ? 0 : 3001,
                inputMoveXMilli: inputMoveXMilli,
                inputMoveYMilli: inputMoveYMilli,
                inputWorldMoveXMilli: inputWorldMoveXMilli,
                inputWorldMoveYMilli: inputWorldMoveYMilli,
                inputHasWorldMove: inputHasWorldMove,
                inputJumpPressed: inputJumpPressed,
                inputJumpHeld: inputJumpHeld,
                inputJumpReleased: inputJumpReleased,
                inputAttackPrimaryPressed: inputAttackPrimaryPressed,
                inputAttackSecondaryPressed: inputAttackSecondaryPressed,
                inputDebugPrimaryPressed: inputDebugPrimaryPressed,
                inputDebugSecondaryPressed: inputDebugSecondaryPressed,
                inputSkillSlotPressedMask: inputSkillSlotPressedMask,
                inputDodgePressed: inputDodgePressed,
                inputAbilitySlotPressed: inputAbilitySlotPressed,
                inputAbilityIdPressed: inputAbilityIdPressed,
                lastMoveInputXMilli: lastMoveInputXMilli,
                lastMoveInputYMilli: lastMoveInputYMilli,
                jumpBufferFramesRemaining: jumpBufferFramesRemaining,
                coyoteTimeFramesRemaining: coyoteTimeFramesRemaining,
                movementMode: movementMode,
                movementLocked: movementLocked,
                movementControlScaleMilli: movementControlScaleMilli,
                rotationControlScaleMilli: rotationControlScaleMilli,
                movementProfileId: movementProfileId,
                movementProfileFallback: movementProfileFallback,
                physicsEnabled: physicsEnabled,
                collisionFlags: collisionFlags,
                surfaceFrictionMilli: surfaceFrictionMilli,
                surfaceBounceSpeedCapYMillimetersPerSecond: surfaceBounceSpeedCapYMillimetersPerSecond,
                surfaceMaterialKind: surfaceMaterialKind,
                surfaceMaterialStableId: surfaceMaterialStableId,
                surfaceElasticityRaw: surfaceElasticityRaw,
                surfaceFixedBounceHeightRaw: surfaceFixedBounceHeightRaw,
                hasUnityView: hasUnityView,
                unityViewXMillimeters: unityViewXMillimeters,
                hasPhysicsBody: hasPhysicsBody,
                physicsBodyXRaw: physicsBodyXRaw,
                presentationState: presentationState);
            var overlay = new CharacterTestRuntimeAiLabFrameSyncOverlaySample(
                available: true,
                frame: overlayFrame,
                agentEntityId: 1,
                targetEntityId: 2,
                sourceCenter: new Vector3(overlaySourceX, 1f, 2f),
                targetCenter: new Vector3(4f, 1f, 2f),
                moveEnd: new Vector3(2f, 1f, 2f),
                attackRange: 2.2f);
            return new CharacterTestRuntimeAiLabFrameSyncSample(
                runtimeFrame: 20,
                stateHash: 12345L,
                selectedFrame: overlayFrame,
                previewFrame: overlayFrame,
                characters: new[] { character },
                projectileRuntimeAvailable: projectiles != null,
                projectileSchemaVersion: projectiles != null ? CombatProjectileRuntimeState.CurrentSchemaVersion : 0,
                projectileDefinitionHash: projectiles != null ? 1234567L : 0L,
                projectileNextProjectileId: projectiles != null ? 102 : 0,
                projectiles: projectiles,
                abilityRuntimeAvailable: abilityCooldowns != null,
                abilitySchemaVersion: abilityCooldowns != null ? AbilityRuntimeState.CurrentSchemaVersion : 0,
                abilityModuleId: abilityCooldowns != null ? "CharacterTest.AbilityRuntime" : string.Empty,
                abilityDefinitionHash: abilityCooldowns != null ? 7654321L : 0L,
                abilityCooldowns: abilityCooldowns,
                overlay: overlay,
                poisonBottlePool: poisonBottlePool,
                physicalShield: physicalShield,
                guardAura: guardAura,
                hookPull: hookPull,
                arcMine: arcMine,
                runeTurret: runeTurret,
                areaConfinement: areaConfinement,
                tornado: tornado,
                chainLightning: chainLightning,
                laserPath: laserPath,
                interaction: interaction,
                movableNaturalObjects: movableNaturalObjects,
                commandState: commandState,
                vfxRuntime: vfxRuntime,
                audio: audio,
                planner: planner,
                runtimeEvents: runtimeEvents);
        }

        private static CharacterTestRuntimeCharacterPresentationState CreateAnimationPresentationState(
            double clipTimeSeconds,
            Quaternion handRotation,
            Vector3 ragdollPosition)
        {
            var clipKey = new ResourceKey("character-test.animation.test", ResourceTypeIds.AnimationClip);
            var slot = new MxAnimationPlaybackSlotState(
                MxAnimationPlaybackSlotRole.Current,
                clipKey,
                clipTimeSeconds,
                playbackSpeed: 1f,
                loop: false,
                fallback: false,
                weight: 1f,
                targetWeight: 1f,
                fadeElapsedSeconds: 0f,
                fadeDurationSeconds: 0f);
            var layer = new MxAnimationLayerPlaybackState(
                MxAnimationLayerId.Base,
                MxAnimationLayerStatus.Playing,
                weight: 1f,
                startWeight: 1f,
                targetWeight: 1f,
                weightTransitionElapsedSeconds: 0f,
                weightTransitionDurationSeconds: 0f,
                weightTransitionPolicyId: string.Empty,
                weightTransitionCorrelationId: string.Empty,
                activeBlendKind: MxAnimationBlendKind.None,
                activeBlendId: string.Empty,
                blendParameter: default,
                blend2DParameterX: default,
                blend2DParameterY: default,
                blendFadeElapsedSeconds: 0f,
                blendFadeDurationSeconds: 0f,
                slots: new[] { slot });
            var backend = new MxAnimationPlaybackState(
                "UnityPlayables",
                "actor.test",
                "set.test",
                new[] { layer });
            var animation = new CharacterRuntimeAnimationPlaybackState(
                backend,
                fallbackPhase: 0f,
                blend: Vector2.zero,
                speed01: 0f,
                useExternalMotion: true,
                externalBlend: Vector2.zero,
                externalSpeed01: 0f,
                usingFallback: false,
                lastActionKey: "light_attack",
                lastQuantizedBlendX: 0,
                lastQuantizedBlendY: 0,
                playbackSpeedCompensation: 1f,
                hasAppliedLocomotionBlend: false,
                lastAppliedBlendId: string.Empty,
                lastAppliedBlendParameterScale: 1000,
                lastAppliedQuantizedBlendX: 0,
                lastAppliedQuantizedBlendY: 0,
                lastAppliedPlaybackSpeedCompensation: 1f,
                runtimeAnimationClock: (float)clipTimeSeconds,
                suppressLocomotionBlendUntilTime: 0f);
            var ragdollBody = new CharacterViewOnlyRagdollBodyPlaybackState(
                ragdollPosition,
                Quaternion.identity,
                Vector3.zero,
                Vector3.zero,
                isKinematic: false);
            var procedural = new CharacterProceduralBonePlaybackState(
                ikSolveSecondsRemaining: 0.1f,
                secondaryChains: Array.Empty<CharacterSecondaryBoneChainPlaybackState>(),
                ragdoll: new CharacterViewOnlyRagdollPlaybackState(
                    CharacterViewOnlyRagdollMode.DynamicPresentation,
                    dynamicSecondsRemaining: 0.25f,
                    bodies: new[] { ragdollBody }));
            var bonePose = new CharacterTestBonePoseState(new[]
            {
                new CharacterTestBonePoseEntry(
                    "RightHand",
                    Vector3.zero,
                    handRotation,
                    Vector3.one)
            });
            return new CharacterTestRuntimeCharacterPresentationState(
                entityId: 1,
                animation,
                procedural,
                bonePose);
        }

        private static CharacterTestRuntimeAiLabFrameSyncPlannerSample CreatePlannerSyncSample(
            int frame,
            int targetEntityId,
            string activeStatePath,
            int selectedActionId,
            string selectedActionName,
            CharacterTestRuntimeAiPlannerActionKind selectedActionKind,
            int targetDistanceMillimeters,
            string lastCommand,
            string suppressedReason,
            string rejectReason,
            int decisionMoveXMilli,
            int movementSpeedScaleMilli = 1000,
            int movementStrafeSide = 0,
            int movementDirectionHoldFrames = 0,
            int movementReactionDelayFrames = 0)
        {
            return new CharacterTestRuntimeAiLabFrameSyncPlannerSample(
                available: true,
                frame: frame,
                profileId: "character-test.runtime-ai-planner.default",
                agents: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample(
                        entityId: 1,
                        targetEntityId: targetEntityId,
                        activeStatePath: activeStatePath,
                        targetExists: targetEntityId > 0,
                        targetInAttackRange: targetDistanceMillimeters <= 2200,
                        targetDistanceMillimeters: targetDistanceMillimeters,
                        targetVisible: true,
                        targetLineOfSightBlocked: false,
                        targetLastSeenAgeFrames: 0,
                        targetThreatScoreMilli: 9000,
                        movePathAvailable: true,
                        moveDirectPathClear: true,
                        moveCandidateIndex: -1,
                        environmentNearObstacle: false,
                        environmentNearHazard: false,
                        environmentNearUsefulObject: false,
                        selectedActionId: selectedActionId,
                        selectedActionKind: (int)selectedActionKind,
                        selectedActionName: selectedActionName,
                        selectedUtilityScoreMilli: 120000,
                        selectedActionCostMilli: 1000,
                        selectedAbilitySlot: 1,
                        lastCommand: lastCommand,
                        suppressedReason: suppressedReason,
                        rejectReason: rejectReason,
                        transitionFromStateId: 0,
                        transitionToStateId: 0,
                        runtimeAiPlannerProfileSlot: 1,
                        runtimeAiBehaviorLoadoutId: 10,
                        runtimeAiBehaviorVariantId: 1,
                        runtimeAiBehaviorCatalogHash: 123456L,
                        runtimeAiBehaviorProfileSlot: 1,
                        movementSpeedScaleMilli: movementSpeedScaleMilli,
                        movementStrafeSide: movementStrafeSide,
                        movementDirectionHoldFrames: movementDirectionHoldFrames,
                        movementReactionDelayFrames: movementReactionDelayFrames,
                        hasScenePreviewGeometry: true,
                        sourceCenterXMillimeters: 1000,
                        targetCenterXMillimeters: 3200,
                        decisionMoveXMilli: decisionMoveXMilli,
                        attackRangeMillimeters: 2200)
                });
        }

        private static CharacterTestRuntimeAiLabFrameSyncAudioSample CreateAudioSyncSample(
            int totalPlayRequests,
            string activeEventName,
            int intentLastCueId,
            int interactionLastCueId,
            int physicalBodyPartPlayedCount,
            int weaponHitPlayedCount,
            string weaponHitLastCueKey)
        {
            return new CharacterTestRuntimeAiLabFrameSyncAudioSample(
                serviceAvailable: true,
                initialized: true,
                totalPlayRequests: totalPlayRequests,
                totalStopRequests: 1,
                activeEventCount: 1,
                activeEvents: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample(
                        handleId: 51,
                        eventId: 610,
                        emitterId: 1,
                        handleState: 1,
                        name: activeEventName,
                        busId: 2)
                },
                busStates: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAudioBusSample(
                        busId: 2,
                        name: "combat",
                        volume: 1f,
                        muted: false)
                },
                recentErrors: Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioErrorSample>(),
                intentDrain: new CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample(
                    frame: 20L,
                    requestCount: 1,
                    playedCount: 1,
                    failedCount: 0,
                    droppedCount: 0,
                    skippedCount: 0,
                    lastCueId: intentLastCueId,
                    lastEventId: 610,
                    lastEmitterId: 1),
                interaction: new CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample(
                    frame: 20L,
                    processedInteractionCount: 1,
                    audioPlayedCount: 1,
                    skippedCount: 0,
                     lastStableId: 7001,
                    lastForceMode: 1,
                    lastFieldShape: 1,
                    lastAudioCueKey: interactionLastCueId),
                physical: new CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample(
                    frame: 20L,
                    characterCount: 2,
                    movingGroundedCharacterCount: 2,
                    bodyPartContactCount: 2,
                    bodyPartContactPlayedCount: physicalBodyPartPlayedCount,
                    bodyPartProbeMissCount: 0,
                    bodyPartFilteredContactCount: 0,
                    leftFootContactCount: 1,
                    rightFootContactCount: 1,
                    landingPlayedCount: 0,
                    objectContactCount: 0,
                    objectCollisionPlayedCount: 0,
                    footProbeMissCount: 0,
                    cooldownSkipCount: 0,
                    presentationMotionResolvedCount: 1,
                    bakedMotionResolvedCount: 1,
                    staticMotionCount: 0,
                    motionFallbackCount: 0,
                    missingMotionSocketCount: 0,
                    missingMotionClipCount: 0,
                    missingMotionFrameCount: 0,
                    missingMotionSourceCount: 0,
                    missingPhysicsRuntimeSkipCount: 0,
                    missingContactPointConfigSkipCount: 0,
                    skippedCount: 0,
                    lastCueKey: "footstep",
                    lastEmitterId: 1),
                weaponHit: new CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample(
                    frame: 20L,
                    processedCount: 2,
                    playedCount: weaponHitPlayedCount,
                    damagePlayedCount: weaponHitPlayedCount,
                    blockedPlayedCount: 0,
                    ignoredResultCount: 0,
                    missingRuleSkipCount: 0,
                    missingAudioServiceSkipCount: 0,
                    missingCueSkipCount: 0,
                    cooldownSkipCount: 0,
                    playbackFailedSkipCount: 0,
                    lastCueKey: weaponHitLastCueKey,
                    lastEventId: 620,
                    lastEmitterId: 2));
        }

        private static CharacterTestRuntimeAiPlannerStateSnapshot CreatePlannerAuthorityState(
            string lastCommand)
        {
            return new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                Slots = new[]
                {
                    new CharacterTestRuntimeAiPlannerSlotStateSnapshot
                    {
                        Slot = 1,
                        Agents = new[]
                        {
                            new CharacterTestRuntimeAiPlannerAgentStateSnapshot
                            {
                                EntityId = 7,
                                CurrentStateId = 1,
                                HadSubmittedControlIntent = true,
                                LastCommand = lastCommand,
                                LastSeenTargetEntityId = 9,
                                TargetLastSeenAgeFrames = 2,
                                LastMovementActionId = 11,
                                LastDesiredMoveX = 1f,
                                LastOutputMoveX = 1f,
                                StrafeSide = 1,
                                NextStrafeSwitchFrame = 60,
                                DirectionHoldFramesRemaining = 3,
                                ReactionDelayFramesRemaining = 1,
                                LastMovementSpeedScale = 1f
                            }
                        }
                    }
                }
            };
        }

        private static CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample CreateVfxRuntimeSyncSample(
            int activeCount,
            CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample instance)
        {
            return new CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample(
                runtimeAvailable: true,
                moduleId: "mxframework.vfx.runtime",
                definitionHash: 7101001L,
                nextInstanceId: 104L,
                activeCount: activeCount,
                pendingSpawnCount: 1,
                pendingStopCount: 0,
                pendingEventCount: 0,
                activeInstances: new[] { instance });
        }

        private static CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample CreateVfxInstanceSyncSample(
            int definitionId,
            int localFrame,
            long anchorPositionXRaw,
             RuntimeTraceIdentity traceId)
        {
            return new CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample(
                instanceId: 101L,
                definitionId: definitionId,
                definitionHash: "vfx-hash",
                prefabResource: "character-test/vfx",
                startFrame: 10L,
                expireFrame: 40L,
                anchorKind: 0,
                anchorSourceId: 1,
                anchorTargetId: 2,
                anchorSocketId: 0,
                anchorPositionXRaw: anchorPositionXRaw,
                anchorPositionYRaw: 2000L,
                anchorPositionZRaw: 3000L,
                anchorForwardXRaw: 0L,
                anchorForwardYRaw: 0L,
                anchorForwardZRaw: 1000000L,
                parameter0: 77,
                parameter1: 1,
                parameter2: 2,
                parameter3: 3,
                sourceDomain: 10,
                sourceFrame: 20L,
                localFrame: localFrame,
                sourceOrder: 0,
                triggerId: 930020,
                sourceId: 1,
                targetId: 2,
                 traceId: traceId);
        }

        private static CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample
            CreateMovableNaturalObjectSyncSample(
                int numericId,
                int xMillimeters,
                int contactPressure,
                int currentPotencyMotion,
                long latestPotencyFrame,
                int latestPotencyResultKind)
        {
            return new CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample(
                numericId: numericId,
                stableId: "movable.test." + numericId.ToString(CultureInfo.InvariantCulture),
                category: CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                massGrams: CharacterTestMovableNaturalObjectSpawnSpec.DefaultMassGrams,
                materialKind: (int)CombatSurfaceMaterialKind.Default,
                shapeKind: (int)CharacterTestMovableNaturalObjectShapeKind.Aabb,
                halfExtentXMillimeters: 250,
                halfExtentYMillimeters: 500,
                halfExtentZMillimeters: 250,
                xMillimeters: xMillimeters,
                yMillimeters: 500,
                zMillimeters: 2200,
                velocityXMillimetersPerSecond: 120,
                velocityYMillimetersPerSecond: 0,
                velocityZMillimetersPerSecond: -40,
                contactVelocityXMillimetersPerSecond: 90,
                contactVelocityZMillimetersPerSecond: -30,
                contactDisplacementXMillimeters: 12,
                contactDisplacementZMillimeters: -4,
                contactPressure: contactPressure,
                contactCount: 1,
                contactPair: "character.1->movable.test." + numericId.ToString(CultureInfo.InvariantCulture),
                behaviorState: "sliding",
                basePotencyMotion: 0,
                basePotencyCohesion: 16,
                basePotencyScatter: 0,
                currentPotencyMotion: currentPotencyMotion,
                currentPotencyCohesion: 12,
                currentPotencyScatter: 4,
                pendingPotencyMotion: 0,
                pendingPotencyCohesion: 0,
                pendingPotencyScatter: 0,
                pendingPotencyInfluenceKind: (int)CharacterTestGenesisPotencyInfluenceKind.None,
                latestPotencyFrame: latestPotencyFrame,
                latestPotencyTargetNumericId: numericId,
                latestPotencyInfluenceKind: (int)CharacterTestGenesisPotencyInfluenceKind.Fire,
                latestPotencyAppliedMotion: currentPotencyMotion,
                latestPotencyAppliedCohesion: -4,
                latestPotencyAppliedScatter: 4,
                latestPotencyAfterMotion: currentPotencyMotion,
                latestPotencyAfterCohesion: 12,
                latestPotencyAfterScatter: 4,
                latestPotencyResultKind: latestPotencyResultKind,
                latestPotencyResultSource: "probe-test");
        }
    }
}
