using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private static void CompareCharacters(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncCharacterSample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncCharacterSample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "entity", 0, "count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncCharacterSample left = expected[i];
                if (!TryFindCharacter(actual, left.EntityId, out CharacterTestRuntimeAiLabFrameSyncCharacterSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "entity",
                        left.EntityId,
                        "missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "transform", left.EntityId, "x", left.XMillimeters, right.XMillimeters);
                AddMismatch(rows, "transform", left.EntityId, "y", left.YMillimeters, right.YMillimeters);
                AddMismatch(rows, "transform", left.EntityId, "z", left.ZMillimeters, right.ZMillimeters);
                AddMismatch(rows, "transform", left.EntityId, "yaw", left.YawMilliDegrees, right.YawMilliDegrees);
                AddMismatch(rows, "physicsProxy", left.EntityId, "available", left.HasPhysicsBody, right.HasPhysicsBody);
                AddMismatch(rows, "physicsProxy", left.EntityId, "xRaw", left.PhysicsBodyXRaw, right.PhysicsBodyXRaw);
                AddMismatch(rows, "physicsProxy", left.EntityId, "yRaw", left.PhysicsBodyYRaw, right.PhysicsBodyYRaw);
                AddMismatch(rows, "physicsProxy", left.EntityId, "zRaw", left.PhysicsBodyZRaw, right.PhysicsBodyZRaw);
                AddMismatch(rows, "motion", left.EntityId, "horizontalVelocityX", left.HorizontalVelocityXMillimetersPerSecond, right.HorizontalVelocityXMillimetersPerSecond);
                AddMismatch(rows, "motion", left.EntityId, "horizontalVelocityY", left.HorizontalVelocityYMillimetersPerSecond, right.HorizontalVelocityYMillimetersPerSecond);
                AddMismatch(rows, "motion", left.EntityId, "desiredVelocityX", left.DesiredVelocityXMillimetersPerSecond, right.DesiredVelocityXMillimetersPerSecond);
                AddMismatch(rows, "motion", left.EntityId, "desiredVelocityY", left.DesiredVelocityYMillimetersPerSecond, right.DesiredVelocityYMillimetersPerSecond);
                AddMismatch(rows, "motion", left.EntityId, "verticalVelocity", left.VerticalVelocityMillimetersPerSecond, right.VerticalVelocityMillimetersPerSecond);
                AddMismatch(rows, "motion", left.EntityId, "grounded", left.IsGrounded, right.IsGrounded);
                AddMismatch(rows, "input", left.EntityId, "moveX", left.InputMoveXMilli, right.InputMoveXMilli);
                AddMismatch(rows, "input", left.EntityId, "moveY", left.InputMoveYMilli, right.InputMoveYMilli);
                AddMismatch(rows, "input", left.EntityId, "worldMoveX", left.InputWorldMoveXMilli, right.InputWorldMoveXMilli);
                AddMismatch(rows, "input", left.EntityId, "worldMoveY", left.InputWorldMoveYMilli, right.InputWorldMoveYMilli);
                AddMismatch(rows, "input", left.EntityId, "hasWorldMove", left.InputHasWorldMove, right.InputHasWorldMove);
                AddMismatch(rows, "input", left.EntityId, "jumpPressed", left.InputJumpPressed, right.InputJumpPressed);
                AddMismatch(rows, "input", left.EntityId, "jumpHeld", left.InputJumpHeld, right.InputJumpHeld);
                AddMismatch(rows, "input", left.EntityId, "jumpReleased", left.InputJumpReleased, right.InputJumpReleased);
                AddMismatch(rows, "input", left.EntityId, "attackPrimaryPressed", left.InputAttackPrimaryPressed, right.InputAttackPrimaryPressed);
                AddMismatch(rows, "input", left.EntityId, "attackSecondaryPressed", left.InputAttackSecondaryPressed, right.InputAttackSecondaryPressed);
                AddMismatch(rows, "input", left.EntityId, "debugPrimaryPressed", left.InputDebugPrimaryPressed, right.InputDebugPrimaryPressed);
                AddMismatch(rows, "input", left.EntityId, "debugSecondaryPressed", left.InputDebugSecondaryPressed, right.InputDebugSecondaryPressed);
                AddMismatch(rows, "input", left.EntityId, "skillSlotPressedMask", left.InputSkillSlotPressedMask, right.InputSkillSlotPressedMask);
                AddMismatch(rows, "input", left.EntityId, "dodgePressed", left.InputDodgePressed, right.InputDodgePressed);
                AddMismatch(rows, "input", left.EntityId, "abilitySlotPressed", left.InputAbilitySlotPressed, right.InputAbilitySlotPressed);
                AddMismatch(rows, "input", left.EntityId, "abilityIdPressed", left.InputAbilityIdPressed, right.InputAbilityIdPressed);
                AddMismatch(rows, "movement", left.EntityId, "lastMoveInputX", left.LastMoveInputXMilli, right.LastMoveInputXMilli);
                AddMismatch(rows, "movement", left.EntityId, "lastMoveInputY", left.LastMoveInputYMilli, right.LastMoveInputYMilli);
                AddMismatch(rows, "movement", left.EntityId, "jumpBufferFramesRemaining", left.JumpBufferFramesRemaining, right.JumpBufferFramesRemaining);
                AddMismatch(rows, "movement", left.EntityId, "coyoteTimeFramesRemaining", left.CoyoteTimeFramesRemaining, right.CoyoteTimeFramesRemaining);
                AddMismatch(rows, "movement", left.EntityId, "movementMode", left.MovementMode, right.MovementMode);
                AddMismatch(rows, "movement", left.EntityId, "movementLocked", left.MovementLocked, right.MovementLocked);
                AddMismatch(rows, "movement", left.EntityId, "movementControlScaleMilli", left.MovementControlScaleMilli, right.MovementControlScaleMilli);
                AddMismatch(rows, "movement", left.EntityId, "rotationControlScaleMilli", left.RotationControlScaleMilli, right.RotationControlScaleMilli);
                AddMismatch(rows, "movement", left.EntityId, "movementProfileId", left.MovementProfileId, right.MovementProfileId);
                AddMismatch(rows, "movement", left.EntityId, "movementProfileFallback", left.MovementProfileFallback, right.MovementProfileFallback);
                AddMismatch(rows, "movement", left.EntityId, "physicsEnabled", left.PhysicsEnabled, right.PhysicsEnabled);
                AddMismatch(rows, "movement", left.EntityId, "collisionFlags", left.CollisionFlags, right.CollisionFlags);
                AddMismatch(rows, "movement", left.EntityId, "surfaceFrictionMilli", left.SurfaceFrictionMilli, right.SurfaceFrictionMilli);
                AddMismatch(rows, "movement", left.EntityId, "surfaceBounceSpeedCapY", left.SurfaceBounceSpeedCapYMillimetersPerSecond, right.SurfaceBounceSpeedCapYMillimetersPerSecond);
                AddMismatch(rows, "movement", left.EntityId, "surfaceMaterialKind", left.SurfaceMaterialKind, right.SurfaceMaterialKind);
                AddMismatch(rows, "movement", left.EntityId, "surfaceMaterialStableId", left.SurfaceMaterialStableId, right.SurfaceMaterialStableId);
                AddMismatch(rows, "movement", left.EntityId, "surfaceElasticityRaw", left.SurfaceElasticityRaw, right.SurfaceElasticityRaw);
                AddMismatch(rows, "movement", left.EntityId, "surfaceFixedBounceHeightRaw", left.SurfaceFixedBounceHeightRaw, right.SurfaceFixedBounceHeightRaw);
                AddMismatch(rows, "physics", left.EntityId, "capsuleRadius", left.PhysicsCapsuleRadiusMillimeters, right.PhysicsCapsuleRadiusMillimeters);
                AddMismatch(rows, "physics", left.EntityId, "capsuleHeight", left.PhysicsCapsuleHeightMillimeters, right.PhysicsCapsuleHeightMillimeters);
                AddMismatch(rows, "physics", left.EntityId, "capsuleCenterY", left.PhysicsCapsuleCenterYMillimeters, right.PhysicsCapsuleCenterYMillimeters);
                AddMismatch(rows, "physics", left.EntityId, "massGrams", left.PhysicsMassGrams, right.PhysicsMassGrams);
                AddMismatch(rows, "physics", left.EntityId, "massScaleMilli", left.PhysicsMassScaleMilli, right.PhysicsMassScaleMilli);
                AddMismatch(rows, "physics", left.EntityId, "radiusScaleMilli", left.PhysicsRadiusScaleMilli, right.PhysicsRadiusScaleMilli);
                AddMismatch(rows, "physics", left.EntityId, "pushScaleMilli", left.PhysicsPushScaleMilli, right.PhysicsPushScaleMilli);
                AddMismatch(rows, "physics", left.EntityId, "blocksCharacters", left.PhysicsBlocksCharacters, right.PhysicsBlocksCharacters);
                AddMismatch(rows, "physics", left.EntityId, "canBePushed", left.PhysicsCanBePushed, right.PhysicsCanBePushed);
                AddMismatch(rows, "physics", left.EntityId, "contactVelocityX", left.PhysicsContactVelocityXMillimetersPerSecond, right.PhysicsContactVelocityXMillimetersPerSecond);
                AddMismatch(rows, "physics", left.EntityId, "contactVelocityY", left.PhysicsContactVelocityYMillimetersPerSecond, right.PhysicsContactVelocityYMillimetersPerSecond);
                AddMismatch(rows, "physics", left.EntityId, "contactDisplacementX", left.PhysicsContactDisplacementXMillimeters, right.PhysicsContactDisplacementXMillimeters);
                AddMismatch(rows, "physics", left.EntityId, "contactDisplacementY", left.PhysicsContactDisplacementYMillimeters, right.PhysicsContactDisplacementYMillimeters);
                AddMismatch(rows, "physics", left.EntityId, "contactCount", left.PhysicsContactCount, right.PhysicsContactCount);
                AddMismatch(rows, "posture", left.EntityId, "profileId", left.PostureProfileId, right.PostureProfileId);
                AddMismatch(rows, "posture", left.EntityId, "height", left.PostureHeightMillimeters, right.PostureHeightMillimeters);
                AddMismatch(rows, "posture", left.EntityId, "radius", left.PostureRadiusMillimeters, right.PostureRadiusMillimeters);
                AddMismatch(rows, "posture", left.EntityId, "centerY", left.PostureCenterYMillimeters, right.PostureCenterYMillimeters);
                AddMismatch(rows, "posture", left.EntityId, "blocksCharacters", left.PostureBlocksCharacters, right.PostureBlocksCharacters);
                AddMismatch(rows, "posture", left.EntityId, "canBePushed", left.PostureCanBePushed, right.PostureCanBePushed);
                AddMismatch(rows, "posture", left.EntityId, "source", left.PostureSource, right.PostureSource);
                AddMismatch(rows, "posture", left.EntityId, "priority", left.PosturePriority, right.PosturePriority);
                AddMismatch(rows, "posture", left.EntityId, "sinceFrame", left.PostureSinceFrame, right.PostureSinceFrame);
                AddMismatch(rows, "posture", left.EntityId, "actionProfileId", left.PostureActionProfileId, right.PostureActionProfileId);
                AddMismatch(rows, "posture", left.EntityId, "actionRestorePolicy", left.PostureActionRestorePolicy, right.PostureActionRestorePolicy);
                AddMismatch(rows, "posture", left.EntityId, "actionTransitionFrames", left.PostureActionTransitionFrames, right.PostureActionTransitionFrames);
                AddMismatch(rows, "posture", left.EntityId, "actionSourceActionId", left.PostureActionSourceActionId, right.PostureActionSourceActionId);
                AddMismatch(rows, "posture", left.EntityId, "actionStableEventId", left.PostureActionStableEventId, right.PostureActionStableEventId);
                AddMismatch(rows, "presence", left.EntityId, "state", left.PresenceState, right.PresenceState);
                AddMismatch(rows, "presence", left.EntityId, "requestedFrame", left.PresenceRequestedFrame, right.PresenceRequestedFrame);
                AddMismatch(rows, "presence", left.EntityId, "destroyedFrame", left.PresenceDestroyedFrame, right.PresenceDestroyedFrame);
                AddMismatch(rows, "presence", left.EntityId, "sourceEntityId", left.PresenceSourceEntityId, right.PresenceSourceEntityId);
                AddMismatch(rows, "presence", left.EntityId, "traceId", left.PresenceTraceId.ToString(), right.PresenceTraceId.ToString());
                AddMismatch(rows, "gameplay", left.EntityId, "lifecyclePresent", left.GameplayLifecyclePresent, right.GameplayLifecyclePresent);
                AddMismatch(rows, "gameplay", left.EntityId, "lifecycleState", left.GameplayLifecycleState, right.GameplayLifecycleState);
                AddMismatch(rows, "gameplay", left.EntityId, "teamPresent", left.GameplayTeamPresent, right.GameplayTeamPresent);
                AddMismatch(rows, "gameplay", left.EntityId, "teamId", left.GameplayTeamId, right.GameplayTeamId);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "present", left.GameplayScatterPresent, right.GameplayScatterPresent);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "currentPressure", left.GameplayScatterCurrentPressure, right.GameplayScatterCurrentPressure);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "maxPressure", left.GameplayScatterMaxPressure, right.GameplayScatterMaxPressure);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "maxDebt", left.GameplayScatterMaxDebt, right.GameplayScatterMaxDebt);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "currentCohesionLimit", left.GameplayScatterCurrentCohesionLimit, right.GameplayScatterCurrentCohesionLimit);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "recoveryRate", left.GameplayScatterRecoveryRate, right.GameplayScatterRecoveryRate);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "recoveryDelayFrames", left.GameplayScatterRecoveryDelayFrames, right.GameplayScatterRecoveryDelayFrames);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "brokenRecoveryDelayFrames", left.GameplayScatterBrokenRecoveryDelayFrames, right.GameplayScatterBrokenRecoveryDelayFrames);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "currentBand", left.GameplayScatterCurrentBand, right.GameplayScatterCurrentBand);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "lastPressureFrame", left.GameplayScatterLastPressureFrame, right.GameplayScatterLastPressureFrame);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "lastCohesionBreakFrame", left.GameplayScatterLastCohesionBreakFrame, right.GameplayScatterLastCohesionBreakFrame);
                AddMismatch(rows, "gameplayScatter", left.EntityId, "isBroken", left.GameplayScatterIsBroken, right.GameplayScatterIsBroken);
                AddMismatch(rows, "battle", left.EntityId, "lifeState", left.BattleLifeState, right.BattleLifeState);
                AddMismatch(rows, "battle", left.EntityId, "dominantReason", left.BattleDominantReason, right.BattleDominantReason);
                AddMismatch(rows, "battle", left.EntityId, "gateFlags", left.BattleGateFlags, right.BattleGateFlags);
                AddMismatch(rows, "battle", left.EntityId, "sinceFrame", left.BattleSinceFrame, right.BattleSinceFrame);
                AddMismatch(rows, "battle", left.EntityId, "lastTransitionFrame", left.BattleLastTransitionFrame, right.BattleLastTransitionFrame);
                AddMismatch(rows, "battle", left.EntityId, "sourceActionId", left.BattleSourceActionId, right.BattleSourceActionId);
                AddMismatch(rows, "battle", left.EntityId, "sourceTraceId", left.BattleSourceTraceId, right.BattleSourceTraceId);
                AddMismatch(rows, "control", left.EntityId, "owner", left.ControlOwner, right.ControlOwner);
                AddMismatch(rows, "control", left.EntityId, "runtimeAiPlannerProfileSlot", left.RuntimeAiPlannerProfileSlot, right.RuntimeAiPlannerProfileSlot);
                AddMismatch(rows, "control", left.EntityId, "runtimeAiBehaviorHasSelection", left.RuntimeAiBehaviorHasSelection, right.RuntimeAiBehaviorHasSelection);
                AddMismatch(rows, "control", left.EntityId, "runtimeAiBehaviorLoadoutId", left.RuntimeAiBehaviorLoadoutId, right.RuntimeAiBehaviorLoadoutId);
                AddMismatch(rows, "control", left.EntityId, "runtimeAiBehaviorVariantId", left.RuntimeAiBehaviorVariantId, right.RuntimeAiBehaviorVariantId);
                AddMismatch(rows, "control", left.EntityId, "runtimeAiBehaviorCatalogHash", left.RuntimeAiBehaviorCatalogHash, right.RuntimeAiBehaviorCatalogHash);
                AddMismatch(rows, "control", left.EntityId, "runtimeAiBehaviorProfileSlot", left.RuntimeAiBehaviorProfileSlot, right.RuntimeAiBehaviorProfileSlot);
                AddMismatch(rows, "actionComponent", left.EntityId, "activeActionId", left.ActiveActionId.ToString(), right.ActiveActionId.ToString());
                AddMismatch(rows, "actionComponent", left.EntityId, "state", left.ActionState, right.ActionState);
                AddMismatch(rows, "actionComponent", left.EntityId, "phase", left.ActionPhase, right.ActionPhase);
                AddMismatch(rows, "actionComponent", left.EntityId, "localFrame", left.ActionLocalFrame, right.ActionLocalFrame);
                AddMismatch(rows, "actionComponent", left.EntityId, "lastRejectReason", left.LastRejectReason, right.LastRejectReason);
                AddMismatch(rows, "actionComponent", left.EntityId, "animationActionKeyThisFrame", left.AnimationActionKeyThisFrame, right.AnimationActionKeyThisFrame);
                AddMismatch(rows, "actionComponent", left.EntityId, "lastAnimationActionKey", left.LastAnimationActionKey, right.LastAnimationActionKey);
                AddMismatch(rows, "runner", left.EntityId, "hasRunner", left.HasRunner, right.HasRunner);
                AddMismatch(rows, "runner", left.EntityId, "actionId", left.RunnerActionId.ToString(), right.RunnerActionId.ToString());
                AddMismatch(rows, "runner", left.EntityId, "state", left.RunnerState, right.RunnerState);
                AddMismatch(rows, "runner", left.EntityId, "phase", left.RunnerPhase, right.RunnerPhase);
                AddMismatch(rows, "runner", left.EntityId, "localFrame", left.RunnerLocalFrame, right.RunnerLocalFrame);
                AddMismatch(rows, "runner", left.EntityId, "activeInstanceId", left.RunnerActiveInstanceId, right.RunnerActiveInstanceId);
                AddMismatch(rows, "runner", left.EntityId, "planId", left.RunnerPlanId, right.RunnerPlanId);
                AddMismatch(rows, "combat", left.EntityId, "maxHealth", left.MaxHealth, right.MaxHealth);
                AddMismatch(rows, "combat", left.EntityId, "health", left.Health, right.Health);
                AddMismatch(rows, "combat", left.EntityId, "hitStunFramesRemaining", left.HitStunFramesRemaining, right.HitStunFramesRemaining);
                AddMismatch(rows, "combat", left.EntityId, "lastDamage", left.LastDamage, right.LastDamage);
                AddMismatch(rows, "combat", left.EntityId, "lastHitFrame", left.LastHitFrame, right.LastHitFrame);
                AddMismatch(rows, "combat", left.EntityId, "lastHitSourceEntityId", left.LastHitSourceEntityId, right.LastHitSourceEntityId);
                AddMismatch(rows, "combat", left.EntityId, "lastHitTraceId", left.LastHitTraceId, right.LastHitTraceId);
                AddMismatch(rows, "combat", left.EntityId, "lifeState", left.LifeState, right.LifeState);
                AddMismatch(rows, "combat", left.EntityId, "isAlive", left.IsAlive, right.IsAlive);
                AddMismatch(rows, "combat", left.EntityId, "isDead", left.IsDead, right.IsDead);
                AddMismatch(rows, "combat", left.EntityId, "scatterPressure", left.ScatterPressure, right.ScatterPressure);
                AddMismatch(rows, "combat", left.EntityId, "maxScatterPressure", left.MaxScatterPressure, right.MaxScatterPressure);
                AddMismatch(rows, "combat", left.EntityId, "scatterPressureBand", left.ScatterPressureBand, right.ScatterPressureBand);
                AddMismatch(rows, "combat", left.EntityId, "lastCohesionBreakFrame", left.LastCohesionBreakFrame, right.LastCohesionBreakFrame);
                AddMismatch(rows, "combat", left.EntityId, "specialHitState", left.SpecialHitState, right.SpecialHitState);
                AddMismatch(rows, "combat", left.EntityId, "specialHitPhase", left.SpecialHitPhase, right.SpecialHitPhase);
                AddMismatch(rows, "combat", left.EntityId, "specialHitSinceFrame", left.SpecialHitSinceFrame, right.SpecialHitSinceFrame);
                AddMismatch(rows, "combat", left.EntityId, "specialHitSourceEntityId", left.SpecialHitSourceEntityId, right.SpecialHitSourceEntityId);
                AddMismatch(rows, "combat", left.EntityId, "specialHitTraceId", left.SpecialHitTraceId, right.SpecialHitTraceId);
                AddMismatch(rows, "combat", left.EntityId, "specialHitLaunchVelocityY", left.SpecialHitLaunchVelocityYMillimetersPerSecond, right.SpecialHitLaunchVelocityYMillimetersPerSecond);
                AddMismatch(rows, "combat", left.EntityId, "specialHitHorizontalImpulse", left.SpecialHitHorizontalImpulseMillimetersPerSecond, right.SpecialHitHorizontalImpulseMillimetersPerSecond);
                AddMismatch(rows, "attribute", left.EntityId, "count", left.AttributeCount, right.AttributeCount);
                AddMismatch(rows, "attribute", left.EntityId, "hash", left.AttributeHash, right.AttributeHash);
                CompareAttributeEntries(left.EntityId, left.AttributeEntries, right.AttributeEntries, rows);
                AddMismatch(rows, "buff", left.EntityId, "count", left.BuffCount, right.BuffCount);
                AddMismatch(rows, "buff", left.EntityId, "hash", left.BuffHash, right.BuffHash);
                CompareBuffEntries(left.EntityId, left.BuffEntries, right.BuffEntries, rows);
                AddMismatch(rows, "status", left.EntityId, "count", left.StatusCount, right.StatusCount);
                AddMismatch(rows, "status", left.EntityId, "hash", left.StatusHash, right.StatusHash);
                CompareStatusEntries(left.EntityId, left.StatusEntries, right.StatusEntries, rows);
            }
        }

        private static void CompareAttributeEntries(
            int entityId,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "attribute", entityId, "entry.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample left = expected[i];
                if (!TryFindAttributeEntry(actual, left.AttributeId, out CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "attribute",
                        entityId,
                        "entry." + left.AttributeId + ".missing",
                        "present",
                        "missing"));
                    continue;
                }

                string prefix = "entry." + left.AttributeId + ".";
                AddMismatch(rows, "attribute", entityId, prefix + "baseValue", left.BaseValue, right.BaseValue);
                AddMismatch(rows, "attribute", entityId, prefix + "currentValue", left.CurrentValue, right.CurrentValue);
            }
        }

        private static bool TryFindAttributeEntry(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample> samples,
            int attributeId,
            out CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.AttributeId == attributeId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareBuffEntries(
            int entityId,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "buff", entityId, "entry.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncBuffEntrySample left = expected[i];
                if (!TryFindBuffEntry(actual, left.BuffId, out CharacterTestRuntimeAiLabFrameSyncBuffEntrySample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "buff",
                        entityId,
                        "entry." + left.BuffId + ".missing",
                        "present",
                        "missing"));
                    continue;
                }

                string prefix = "entry." + left.BuffId + ".";
                AddMismatch(rows, "buff", entityId, prefix + "stackCount", left.StackCount, right.StackCount);
                AddMismatch(rows, "buff", entityId, prefix + "startFrame", left.StartFrame, right.StartFrame);
                AddMismatch(rows, "buff", entityId, prefix + "endFrame", left.EndFrame, right.EndFrame);
                AddMismatch(rows, "buff", entityId, prefix + "isPermanent", left.IsPermanent, right.IsPermanent);
                AddMismatch(rows, "buff", entityId, prefix + "sourceId", left.SourceId, right.SourceId);
            }
        }

        private static bool TryFindBuffEntry(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample> samples,
            int buffId,
            out CharacterTestRuntimeAiLabFrameSyncBuffEntrySample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.BuffId == buffId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareStatusEntries(
            int entityId,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "status", entityId, "entry.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncStatusEntrySample left = expected[i];
                if (!TryFindStatusEntry(actual, left.StatusId, out CharacterTestRuntimeAiLabFrameSyncStatusEntrySample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "status",
                        entityId,
                        "entry." + left.StatusId + ".missing",
                        "present",
                        "missing"));
                    continue;
                }

                string prefix = "entry." + left.StatusId + ".";
                AddMismatch(rows, "status", entityId, prefix + "sourceBuffId", left.SourceBuffId, right.SourceBuffId);
            }
        }

        private static bool TryFindStatusEntry(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample> samples,
            int statusId,
            out CharacterTestRuntimeAiLabFrameSyncStatusEntrySample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.StatusId == statusId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static bool TryFindCharacter(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncCharacterSample> samples,
            int entityId,
            out CharacterTestRuntimeAiLabFrameSyncCharacterSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.EntityId == entityId)
                    return true;
            }

            sample = default;
            return false;
        }


    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncCharacterSample
    {
        public CharacterTestRuntimeAiLabFrameSyncCharacterSample(
            int entityId,
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            int yawMilliDegrees,
            int horizontalVelocityXMillimetersPerSecond,
            int horizontalVelocityYMillimetersPerSecond,
            int desiredVelocityXMillimetersPerSecond,
            int desiredVelocityYMillimetersPerSecond,
            int verticalVelocityMillimetersPerSecond,
            bool isGrounded,
            CharacterActionId activeActionId,
            string actionState,
            string actionPhase,
            int actionLocalFrame,
            string lastRejectReason,
            string animationActionKeyThisFrame,
            string lastAnimationActionKey,
            bool hasRunner,
            CharacterActionId runnerActionId,
            string runnerState,
            string runnerPhase,
            int runnerLocalFrame,
            long runnerActiveInstanceId,
            long runnerPlanId,
            int maxHealth = 0,
            int health = 0,
            int hitStunFramesRemaining = 0,
            int lastDamage = 0,
            long lastHitFrame = -1L,
            int lastHitSourceEntityId = 0,
            int lastHitTraceId = 0,
            string lifeState = "",
            bool isAlive = false,
            bool isDead = false,
            int scatterPressure = 0,
            int maxScatterPressure = 0,
            string scatterPressureBand = "",
            long lastCohesionBreakFrame = -1L,
            string specialHitState = "",
            string specialHitPhase = "",
            long specialHitSinceFrame = -1L,
            int specialHitSourceEntityId = 0,
            int specialHitTraceId = 0,
            int specialHitLaunchVelocityYMillimetersPerSecond = 0,
            int specialHitHorizontalImpulseMillimetersPerSecond = 0,
            int attributeCount = 0,
            int attributeHash = 0,
            int buffCount = 0,
            int buffHash = 0,
            int statusCount = 0,
            int statusHash = 0,
            CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample[] attributeEntries = null,
            CharacterTestRuntimeAiLabFrameSyncBuffEntrySample[] buffEntries = null,
            CharacterTestRuntimeAiLabFrameSyncStatusEntrySample[] statusEntries = null,
            string controlOwner = "",
            int runtimeAiPlannerProfileSlot = 0,
            bool runtimeAiBehaviorHasSelection = false,
            int runtimeAiBehaviorLoadoutId = 0,
            int runtimeAiBehaviorVariantId = 0,
            long runtimeAiBehaviorCatalogHash = 0L,
            int runtimeAiBehaviorProfileSlot = 0,
            int physicsCapsuleRadiusMillimeters = 0,
            int physicsCapsuleHeightMillimeters = 0,
            int physicsCapsuleCenterYMillimeters = 0,
            int physicsMassGrams = 0,
            int physicsMassScaleMilli = 0,
            int physicsRadiusScaleMilli = 0,
            int physicsPushScaleMilli = 0,
            bool physicsBlocksCharacters = false,
            bool physicsCanBePushed = false,
            int physicsContactVelocityXMillimetersPerSecond = 0,
            int physicsContactVelocityYMillimetersPerSecond = 0,
            int physicsContactDisplacementXMillimeters = 0,
            int physicsContactDisplacementYMillimeters = 0,
            int physicsContactCount = 0,
            int postureProfileId = 0,
            int postureHeightMillimeters = 0,
            int postureRadiusMillimeters = 0,
            int postureCenterYMillimeters = 0,
            bool postureBlocksCharacters = false,
            bool postureCanBePushed = false,
            string postureSource = "",
            int posturePriority = 0,
            long postureSinceFrame = 0L,
            int postureActionProfileId = 0,
            string postureActionRestorePolicy = "",
            int postureActionTransitionFrames = 0,
            CharacterActionId postureActionSourceActionId = default,
            int postureActionStableEventId = 0,
            string presenceState = "",
            long presenceRequestedFrame = -1L,
            long presenceDestroyedFrame = -1L,
            int presenceSourceEntityId = 0,
            RuntimeTraceIdentity presenceTraceId = default,
            string battleLifeState = "",
            string battleDominantReason = "",
            string battleGateFlags = "",
            long battleSinceFrame = 0L,
            long battleLastTransitionFrame = 0L,
            string battleSourceActionId = "",
            int battleSourceTraceId = 0,
            short inputMoveXMilli = 0,
            short inputMoveYMilli = 0,
            short inputWorldMoveXMilli = 0,
            short inputWorldMoveYMilli = 0,
            bool inputHasWorldMove = false,
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
            short lastMoveInputXMilli = 0,
            short lastMoveInputYMilli = 0,
            int jumpBufferFramesRemaining = 0,
            int coyoteTimeFramesRemaining = 0,
            string movementMode = "",
            bool movementLocked = false,
            int movementControlScaleMilli = 0,
            int rotationControlScaleMilli = 0,
            int movementProfileId = 0,
            bool movementProfileFallback = false,
            bool physicsEnabled = false,
            string collisionFlags = "",
            int surfaceFrictionMilli = 0,
            int surfaceBounceSpeedCapYMillimetersPerSecond = 0,
            string surfaceMaterialKind = "",
            int surfaceMaterialStableId = 0,
            long surfaceElasticityRaw = 0L,
            long surfaceFixedBounceHeightRaw = 0L,
            bool gameplayLifecyclePresent = false,
            string gameplayLifecycleState = "",
            bool gameplayTeamPresent = false,
            int gameplayTeamId = 0,
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
            bool hasUnityView = false,
            int unityViewXMillimeters = 0,
            int unityViewYMillimeters = 0,
            int unityViewZMillimeters = 0,
            int unityViewYawMilliDegrees = 0,
            bool hasPhysicsBody = false,
            long physicsBodyXRaw = 0L,
            long physicsBodyYRaw = 0L,
            long physicsBodyZRaw = 0L,
            CharacterTestRuntimeCharacterPresentationState presentationState = null,
            int animationBlendXMilli = 0,
            int animationBlendYMilli = 0,
            int animationSpeed01Milli = 0,
            string animationActionKey = "",
            bool animationGrounded = false,
            bool animationAirborne = false)
        {
            EntityId = entityId;
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
            YawMilliDegrees = yawMilliDegrees;
            HorizontalVelocityXMillimetersPerSecond = horizontalVelocityXMillimetersPerSecond;
            HorizontalVelocityYMillimetersPerSecond = horizontalVelocityYMillimetersPerSecond;
            DesiredVelocityXMillimetersPerSecond = desiredVelocityXMillimetersPerSecond;
            DesiredVelocityYMillimetersPerSecond = desiredVelocityYMillimetersPerSecond;
            VerticalVelocityMillimetersPerSecond = verticalVelocityMillimetersPerSecond;
            IsGrounded = isGrounded;
            ControlOwner = controlOwner ?? string.Empty;
            RuntimeAiPlannerProfileSlot = runtimeAiPlannerProfileSlot;
            RuntimeAiBehaviorHasSelection = runtimeAiBehaviorHasSelection;
            RuntimeAiBehaviorLoadoutId = runtimeAiBehaviorLoadoutId;
            RuntimeAiBehaviorVariantId = runtimeAiBehaviorVariantId;
            RuntimeAiBehaviorCatalogHash = runtimeAiBehaviorCatalogHash;
            RuntimeAiBehaviorProfileSlot = runtimeAiBehaviorProfileSlot;
            PhysicsCapsuleRadiusMillimeters = physicsCapsuleRadiusMillimeters;
            PhysicsCapsuleHeightMillimeters = physicsCapsuleHeightMillimeters;
            PhysicsCapsuleCenterYMillimeters = physicsCapsuleCenterYMillimeters;
            PhysicsMassGrams = physicsMassGrams;
            PhysicsMassScaleMilli = physicsMassScaleMilli;
            PhysicsRadiusScaleMilli = physicsRadiusScaleMilli;
            PhysicsPushScaleMilli = physicsPushScaleMilli;
            PhysicsBlocksCharacters = physicsBlocksCharacters;
            PhysicsCanBePushed = physicsCanBePushed;
            PhysicsContactVelocityXMillimetersPerSecond = physicsContactVelocityXMillimetersPerSecond;
            PhysicsContactVelocityYMillimetersPerSecond = physicsContactVelocityYMillimetersPerSecond;
            PhysicsContactDisplacementXMillimeters = physicsContactDisplacementXMillimeters;
            PhysicsContactDisplacementYMillimeters = physicsContactDisplacementYMillimeters;
            PhysicsContactCount = physicsContactCount;
            PostureProfileId = postureProfileId;
            PostureHeightMillimeters = postureHeightMillimeters;
            PostureRadiusMillimeters = postureRadiusMillimeters;
            PostureCenterYMillimeters = postureCenterYMillimeters;
            PostureBlocksCharacters = postureBlocksCharacters;
            PostureCanBePushed = postureCanBePushed;
            PostureSource = postureSource ?? string.Empty;
            PosturePriority = posturePriority;
            PostureSinceFrame = postureSinceFrame;
            PostureActionProfileId = postureActionProfileId;
            PostureActionRestorePolicy = postureActionRestorePolicy ?? string.Empty;
            PostureActionTransitionFrames = postureActionTransitionFrames;
            PostureActionSourceActionId = postureActionSourceActionId;
            PostureActionStableEventId = postureActionStableEventId;
            PresenceState = presenceState ?? string.Empty;
            PresenceRequestedFrame = presenceRequestedFrame;
            PresenceDestroyedFrame = presenceDestroyedFrame;
            PresenceSourceEntityId = presenceSourceEntityId;
            PresenceTraceId = presenceTraceId;
            GameplayLifecyclePresent = gameplayLifecyclePresent;
            GameplayLifecycleState = gameplayLifecycleState ?? string.Empty;
            GameplayTeamPresent = gameplayTeamPresent;
            GameplayTeamId = gameplayTeamId;
            GameplayScatterPresent = gameplayScatterPresent;
            GameplayScatterCurrentPressure = gameplayScatterCurrentPressure;
            GameplayScatterMaxPressure = gameplayScatterMaxPressure;
            GameplayScatterMaxDebt = gameplayScatterMaxDebt;
            GameplayScatterCurrentCohesionLimit = gameplayScatterCurrentCohesionLimit;
            GameplayScatterRecoveryRate = gameplayScatterRecoveryRate;
            GameplayScatterRecoveryDelayFrames = gameplayScatterRecoveryDelayFrames;
            GameplayScatterBrokenRecoveryDelayFrames = gameplayScatterBrokenRecoveryDelayFrames;
            GameplayScatterCurrentBand = gameplayScatterCurrentBand ?? string.Empty;
            GameplayScatterLastPressureFrame = gameplayScatterLastPressureFrame;
            GameplayScatterLastCohesionBreakFrame = gameplayScatterLastCohesionBreakFrame;
            GameplayScatterIsBroken = gameplayScatterIsBroken;
            HasUnityView = hasUnityView;
            UnityViewXMillimeters = unityViewXMillimeters;
            UnityViewYMillimeters = unityViewYMillimeters;
            UnityViewZMillimeters = unityViewZMillimeters;
            UnityViewYawMilliDegrees = unityViewYawMilliDegrees;
            HasPhysicsBody = hasPhysicsBody;
            PhysicsBodyXRaw = physicsBodyXRaw;
            PhysicsBodyYRaw = physicsBodyYRaw;
            PhysicsBodyZRaw = physicsBodyZRaw;
            PresentationState = presentationState;
            AnimationBlendXMilli = animationBlendXMilli;
            AnimationBlendYMilli = animationBlendYMilli;
            AnimationSpeed01Milli = animationSpeed01Milli;
            AnimationActionKey = animationActionKey ?? string.Empty;
            AnimationGrounded = animationGrounded;
            AnimationAirborne = animationAirborne;
            BattleLifeState = battleLifeState ?? string.Empty;
            BattleDominantReason = battleDominantReason ?? string.Empty;
            BattleGateFlags = battleGateFlags ?? string.Empty;
            BattleSinceFrame = battleSinceFrame;
            BattleLastTransitionFrame = battleLastTransitionFrame;
            BattleSourceActionId = battleSourceActionId ?? string.Empty;
            BattleSourceTraceId = battleSourceTraceId;
            InputMoveXMilli = inputMoveXMilli;
            InputMoveYMilli = inputMoveYMilli;
            InputWorldMoveXMilli = inputWorldMoveXMilli;
            InputWorldMoveYMilli = inputWorldMoveYMilli;
            InputHasWorldMove = inputHasWorldMove;
            InputJumpPressed = inputJumpPressed;
            InputJumpHeld = inputJumpHeld;
            InputJumpReleased = inputJumpReleased;
            InputAttackPrimaryPressed = inputAttackPrimaryPressed;
            InputAttackSecondaryPressed = inputAttackSecondaryPressed;
            InputDebugPrimaryPressed = inputDebugPrimaryPressed;
            InputDebugSecondaryPressed = inputDebugSecondaryPressed;
            InputSkillSlotPressedMask = inputSkillSlotPressedMask;
            InputDodgePressed = inputDodgePressed;
            InputAbilitySlotPressed = inputAbilitySlotPressed;
            InputAbilityIdPressed = inputAbilityIdPressed;
            LastMoveInputXMilli = lastMoveInputXMilli;
            LastMoveInputYMilli = lastMoveInputYMilli;
            JumpBufferFramesRemaining = jumpBufferFramesRemaining;
            CoyoteTimeFramesRemaining = coyoteTimeFramesRemaining;
            MovementMode = movementMode ?? string.Empty;
            MovementLocked = movementLocked;
            MovementControlScaleMilli = movementControlScaleMilli;
            RotationControlScaleMilli = rotationControlScaleMilli;
            MovementProfileId = movementProfileId;
            MovementProfileFallback = movementProfileFallback;
            PhysicsEnabled = physicsEnabled;
            CollisionFlags = collisionFlags ?? string.Empty;
            SurfaceFrictionMilli = surfaceFrictionMilli;
            SurfaceBounceSpeedCapYMillimetersPerSecond = surfaceBounceSpeedCapYMillimetersPerSecond;
            SurfaceMaterialKind = surfaceMaterialKind ?? string.Empty;
            SurfaceMaterialStableId = surfaceMaterialStableId;
            SurfaceElasticityRaw = surfaceElasticityRaw;
            SurfaceFixedBounceHeightRaw = surfaceFixedBounceHeightRaw;
            ActiveActionId = activeActionId;
            ActionState = actionState ?? string.Empty;
            ActionPhase = actionPhase ?? string.Empty;
            ActionLocalFrame = actionLocalFrame;
            LastRejectReason = lastRejectReason ?? string.Empty;
            AnimationActionKeyThisFrame = animationActionKeyThisFrame ?? string.Empty;
            LastAnimationActionKey = lastAnimationActionKey ?? string.Empty;
            HasRunner = hasRunner;
            RunnerActionId = runnerActionId;
            RunnerState = runnerState ?? string.Empty;
            RunnerPhase = runnerPhase ?? string.Empty;
            RunnerLocalFrame = runnerLocalFrame;
            RunnerActiveInstanceId = runnerActiveInstanceId;
            RunnerPlanId = runnerPlanId;
            MaxHealth = maxHealth;
            Health = health;
            HitStunFramesRemaining = hitStunFramesRemaining;
            LastDamage = lastDamage;
            LastHitFrame = lastHitFrame;
            LastHitSourceEntityId = lastHitSourceEntityId;
            LastHitTraceId = lastHitTraceId;
            LifeState = lifeState ?? string.Empty;
            IsAlive = isAlive;
            IsDead = isDead;
            ScatterPressure = scatterPressure;
            MaxScatterPressure = maxScatterPressure;
            ScatterPressureBand = scatterPressureBand ?? string.Empty;
            LastCohesionBreakFrame = lastCohesionBreakFrame;
            SpecialHitState = specialHitState ?? string.Empty;
            SpecialHitPhase = specialHitPhase ?? string.Empty;
            SpecialHitSinceFrame = specialHitSinceFrame;
            SpecialHitSourceEntityId = specialHitSourceEntityId;
            SpecialHitTraceId = specialHitTraceId;
            SpecialHitLaunchVelocityYMillimetersPerSecond = specialHitLaunchVelocityYMillimetersPerSecond;
            SpecialHitHorizontalImpulseMillimetersPerSecond = specialHitHorizontalImpulseMillimetersPerSecond;
            AttributeCount = attributeCount;
            AttributeHash = attributeHash;
            BuffCount = buffCount;
            BuffHash = buffHash;
            StatusCount = statusCount;
            StatusHash = statusHash;
            AttributeEntries = attributeEntries ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample>();
            BuffEntries = buffEntries ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample>();
            StatusEntries = statusEntries ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample>();
        }

        public int EntityId { get; }
        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }
        public int YawMilliDegrees { get; }
        public bool HasUnityView { get; }
        public int UnityViewXMillimeters { get; }
        public int UnityViewYMillimeters { get; }
        public int UnityViewZMillimeters { get; }
        public int UnityViewYawMilliDegrees { get; }
        public bool HasPhysicsBody { get; }
        public long PhysicsBodyXRaw { get; }
        public long PhysicsBodyYRaw { get; }
        public long PhysicsBodyZRaw { get; }
        public CharacterTestRuntimeCharacterPresentationState PresentationState { get; }
        public int AnimationBlendXMilli { get; }
        public int AnimationBlendYMilli { get; }
        public int AnimationSpeed01Milli { get; }
        public string AnimationActionKey { get; }
        public bool AnimationGrounded { get; }
        public bool AnimationAirborne { get; }
        public int HorizontalVelocityXMillimetersPerSecond { get; }
        public int HorizontalVelocityYMillimetersPerSecond { get; }
        public int DesiredVelocityXMillimetersPerSecond { get; }
        public int DesiredVelocityYMillimetersPerSecond { get; }
        public int VerticalVelocityMillimetersPerSecond { get; }
        public bool IsGrounded { get; }
        public string ControlOwner { get; }
        public int RuntimeAiPlannerProfileSlot { get; }
        public bool RuntimeAiBehaviorHasSelection { get; }
        public int RuntimeAiBehaviorLoadoutId { get; }
        public int RuntimeAiBehaviorVariantId { get; }
        public long RuntimeAiBehaviorCatalogHash { get; }
        public int RuntimeAiBehaviorProfileSlot { get; }
        public int PhysicsCapsuleRadiusMillimeters { get; }
        public int PhysicsCapsuleHeightMillimeters { get; }
        public int PhysicsCapsuleCenterYMillimeters { get; }
        public int PhysicsMassGrams { get; }
        public int PhysicsMassScaleMilli { get; }
        public int PhysicsRadiusScaleMilli { get; }
        public int PhysicsPushScaleMilli { get; }
        public bool PhysicsBlocksCharacters { get; }
        public bool PhysicsCanBePushed { get; }
        public int PhysicsContactVelocityXMillimetersPerSecond { get; }
        public int PhysicsContactVelocityYMillimetersPerSecond { get; }
        public int PhysicsContactDisplacementXMillimeters { get; }
        public int PhysicsContactDisplacementYMillimeters { get; }
        public int PhysicsContactCount { get; }
        public int PostureProfileId { get; }
        public int PostureHeightMillimeters { get; }
        public int PostureRadiusMillimeters { get; }
        public int PostureCenterYMillimeters { get; }
        public bool PostureBlocksCharacters { get; }
        public bool PostureCanBePushed { get; }
        public string PostureSource { get; }
        public int PosturePriority { get; }
        public long PostureSinceFrame { get; }
        public int PostureActionProfileId { get; }
        public string PostureActionRestorePolicy { get; }
        public int PostureActionTransitionFrames { get; }
        public CharacterActionId PostureActionSourceActionId { get; }
        public int PostureActionStableEventId { get; }
        public string PresenceState { get; }
        public long PresenceRequestedFrame { get; }
        public long PresenceDestroyedFrame { get; }
        public int PresenceSourceEntityId { get; }
        public RuntimeTraceIdentity PresenceTraceId { get; }
        public bool GameplayLifecyclePresent { get; }
        public string GameplayLifecycleState { get; }
        public bool GameplayTeamPresent { get; }
        public int GameplayTeamId { get; }
        public bool GameplayScatterPresent { get; }
        public int GameplayScatterCurrentPressure { get; }
        public int GameplayScatterMaxPressure { get; }
        public int GameplayScatterMaxDebt { get; }
        public int GameplayScatterCurrentCohesionLimit { get; }
        public int GameplayScatterRecoveryRate { get; }
        public int GameplayScatterRecoveryDelayFrames { get; }
        public int GameplayScatterBrokenRecoveryDelayFrames { get; }
        public string GameplayScatterCurrentBand { get; }
        public long GameplayScatterLastPressureFrame { get; }
        public long GameplayScatterLastCohesionBreakFrame { get; }
        public bool GameplayScatterIsBroken { get; }
        public string BattleLifeState { get; }
        public string BattleDominantReason { get; }
        public string BattleGateFlags { get; }
        public long BattleSinceFrame { get; }
        public long BattleLastTransitionFrame { get; }
        public string BattleSourceActionId { get; }
        public int BattleSourceTraceId { get; }
        public short InputMoveXMilli { get; }
        public short InputMoveYMilli { get; }
        public short InputWorldMoveXMilli { get; }
        public short InputWorldMoveYMilli { get; }
        public bool InputHasWorldMove { get; }
        public bool InputJumpPressed { get; }
        public bool InputJumpHeld { get; }
        public bool InputJumpReleased { get; }
        public bool InputAttackPrimaryPressed { get; }
        public bool InputAttackSecondaryPressed { get; }
        public bool InputDebugPrimaryPressed { get; }
        public bool InputDebugSecondaryPressed { get; }
        public int InputSkillSlotPressedMask { get; }
        public bool InputDodgePressed { get; }
        public int InputAbilitySlotPressed { get; }
        public int InputAbilityIdPressed { get; }
        public short LastMoveInputXMilli { get; }
        public short LastMoveInputYMilli { get; }
        public int JumpBufferFramesRemaining { get; }
        public int CoyoteTimeFramesRemaining { get; }
        public string MovementMode { get; }
        public bool MovementLocked { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public int MovementProfileId { get; }
        public bool MovementProfileFallback { get; }
        public bool PhysicsEnabled { get; }
        public string CollisionFlags { get; }
        public int SurfaceFrictionMilli { get; }
        public int SurfaceBounceSpeedCapYMillimetersPerSecond { get; }
        public string SurfaceMaterialKind { get; }
        public int SurfaceMaterialStableId { get; }
        public long SurfaceElasticityRaw { get; }
        public long SurfaceFixedBounceHeightRaw { get; }
        public CharacterActionId ActiveActionId { get; }
        public string ActionState { get; }
        public string ActionPhase { get; }
        public int ActionLocalFrame { get; }
        public string LastRejectReason { get; }
        public string AnimationActionKeyThisFrame { get; }
        public string LastAnimationActionKey { get; }
        public bool HasRunner { get; }
        public CharacterActionId RunnerActionId { get; }
        public string RunnerState { get; }
        public string RunnerPhase { get; }
        public int RunnerLocalFrame { get; }
        public long RunnerActiveInstanceId { get; }
        public long RunnerPlanId { get; }
        public int MaxHealth { get; }
        public int Health { get; }
        public int HitStunFramesRemaining { get; }
        public int LastDamage { get; }
        public long LastHitFrame { get; }
        public int LastHitSourceEntityId { get; }
        public int LastHitTraceId { get; }
        public string LifeState { get; }
        public bool IsAlive { get; }
        public bool IsDead { get; }
        public int ScatterPressure { get; }
        public int MaxScatterPressure { get; }
        public string ScatterPressureBand { get; }
        public long LastCohesionBreakFrame { get; }
        public string SpecialHitState { get; }
        public string SpecialHitPhase { get; }
        public long SpecialHitSinceFrame { get; }
        public int SpecialHitSourceEntityId { get; }
        public int SpecialHitTraceId { get; }
        public int SpecialHitLaunchVelocityYMillimetersPerSecond { get; }
        public int SpecialHitHorizontalImpulseMillimetersPerSecond { get; }
        public int AttributeCount { get; }
        public int AttributeHash { get; }
        public int BuffCount { get; }
        public int BuffHash { get; }
        public int StatusCount { get; }
        public int StatusHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample> AttributeEntries { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample> BuffEntries { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample> StatusEntries { get; }

        public static CharacterTestRuntimeAiLabFrameSyncCharacterSample Capture(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterActionRuntime actionRuntime,
            GameplayComponentWorld gameplayComponentWorld = null,
            CharacterTestCharacterUnityViewBridge viewBridge = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null)
        {
            if (entity == null)
                return default;

            CharacterTestCharacterTransformComponent transform = entity.Transform;
            CharacterTestCharacterActionComponent action = entity.Action;
            CharacterTestCharacterCombatStateComponent combat = entity.CombatState;
            CharacterTestSpecialHitStateComponent specialHit = entity.SpecialHitState;
            CharacterTestCharacterControlComponent control = entity.Control;
            CharacterTestRuntimeAiBehaviorSelection selection = control.RuntimeAiBehaviorSelection;
            CharacterTestCharacterPhysicsComponent physics = entity.Physics;
            CharacterTestColliderPostureComponent posture = entity.ColliderPosture;
            CharacterTestEntityPresenceComponent presence = entity.Presence;
            CharacterTestBattleStateComponent battle = entity.BattleState;
            CharacterTestCharacterInputComponent input = entity.Input;
            CombatSurfaceMaterial surface = entity.SurfaceMaterial;
            CharacterActionExecutionState runner = default;
            bool hasRunner = actionRuntime != null
                && actionRuntime.TryCreateRunnerRestoreState(entity.Id, out runner)
                && runner.HasActiveAction;
            Vector3 unityViewPosition = Vector3.zero;
            Quaternion unityViewRotation = Quaternion.identity;
            bool hasUnityView = viewBridge != null
                && viewBridge.TryGetViewPose(entity.Id, out unityViewPosition, out unityViewRotation);
            int unityViewXMillimeters = hasUnityView
                ? CharacterTestDeterministicUnits.MetersToMillimeters(unityViewPosition.x)
                : 0;
            int unityViewYMillimeters = hasUnityView
                ? CharacterTestDeterministicUnits.MetersToMillimeters(unityViewPosition.y)
                : 0;
            int unityViewZMillimeters = hasUnityView
                ? CharacterTestDeterministicUnits.MetersToMillimeters(unityViewPosition.z)
                : 0;
            int unityViewYawMilliDegrees = hasUnityView
                ? CharacterTestDeterministicUnits.DegreesToMilliDegrees(unityViewRotation.eulerAngles.y)
                : 0;
            CombatPhysicsBody physicsBody = default;
            bool hasPhysicsBody = physicsRuntime != null
                && physicsRuntime.World != null
                && physicsRuntime.World.TryGetBody(
                    physicsRuntime.GetBodyId(entity),
                    out physicsBody);
            CharacterTestRuntimeCharacterPresentationState presentationState = null;
            viewBridge?.TryCaptureCharacterPresentationState(entity.Id, out presentationState);
            CaptureGameplayAttributes(
                gameplayComponentWorld,
                entity.GameplayEntityId,
                out int attributeCount,
                out int attributeHash,
                out CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample[] attributeEntries);
            CaptureGameplayBuffStatus(
                gameplayComponentWorld,
                entity.GameplayEntityId,
                out int buffCount,
                out int buffHash,
                out CharacterTestRuntimeAiLabFrameSyncBuffEntrySample[] buffEntries,
                out int statusCount,
                out int statusHash,
                out CharacterTestRuntimeAiLabFrameSyncStatusEntrySample[] statusEntries);
            CaptureGameplayIdentity(
                gameplayComponentWorld,
                entity.GameplayEntityId,
                out bool lifecyclePresent,
                out string lifecycleState,
                out bool teamPresent,
                out int teamId);
            CaptureGameplayScatterPressure(
                gameplayComponentWorld,
                entity.GameplayEntityId,
                out bool gameplayScatterPresent,
                out int gameplayScatterCurrentPressure,
                out int gameplayScatterMaxPressure,
                out int gameplayScatterMaxDebt,
                out int gameplayScatterCurrentCohesionLimit,
                out int gameplayScatterRecoveryRate,
                out int gameplayScatterRecoveryDelayFrames,
                out int gameplayScatterBrokenRecoveryDelayFrames,
                out string gameplayScatterCurrentBand,
                out long gameplayScatterLastPressureFrame,
                out long gameplayScatterLastCohesionBreakFrame,
                out bool gameplayScatterIsBroken);
            return new CharacterTestRuntimeAiLabFrameSyncCharacterSample(
                entity.Id.Value,
                transform.XMillimeters,
                transform.YMillimeters,
                transform.ZMillimeters,
                transform.YawMilliDegrees,
                entity.HorizontalVelocityXMillimetersPerSecond,
                entity.HorizontalVelocityYMillimetersPerSecond,
                entity.DesiredVelocityXMillimetersPerSecond,
                entity.DesiredVelocityYMillimetersPerSecond,
                entity.VerticalVelocityMillimetersPerSecond,
                entity.IsGrounded,
                action.ActiveActionId,
                CharacterTestCharacterActionComponent.ResolveStateText(action.State),
                CharacterTestCharacterActionComponent.ResolvePhaseText(action.Phase),
                action.LocalFrame,
                action.LastRejectReason,
                action.AnimationActionKeyThisFrame,
                action.LastAnimationActionKey,
                hasRunner,
                hasRunner ? runner.ActionId : CharacterActionId.None,
                hasRunner ? runner.State.ToString() : string.Empty,
                hasRunner ? runner.CurrentPhase.ToString() : string.Empty,
                hasRunner ? runner.LocalFrame : 0,
                hasRunner ? runner.ActiveActionInstanceId : 0L,
                hasRunner ? runner.PlanId : 0L,
                combat.MaxHealth,
                combat.Health,
                combat.HitStunFramesRemaining,
                combat.LastDamage,
                combat.LastHitFrame,
                combat.LastHitSourceEntityId,
                combat.LastHitTraceId,
                combat.LifeState.ToString(),
                combat.IsAlive,
                combat.IsDead,
                combat.ScatterPressure,
                combat.MaxScatterPressure,
                combat.ScatterPressureBand.ToString(),
                combat.LastCohesionBreakFrame,
                specialHit.State.ToString(),
                specialHit.Phase.ToString(),
                specialHit.SinceFrame,
                specialHit.SourceEntityId,
                specialHit.TraceId,
                specialHit.LaunchVelocityYMillimetersPerSecond,
                specialHit.HorizontalImpulseMillimetersPerSecond,
                attributeCount,
                attributeHash,
                buffCount,
                buffHash,
                statusCount,
                statusHash,
                attributeEntries,
                buffEntries,
                statusEntries,
                control.Owner.ToString(),
                control.RuntimeAiPlannerProfileSlot,
                selection.HasSelection,
                selection.LoadoutId,
                selection.VariantId,
                selection.CatalogHash,
                selection.ProfileSlot,
                physics.CapsuleRadiusMillimeters,
                physics.CapsuleHeightMillimeters,
                physics.CapsuleCenterYMillimeters,
                physics.MassGrams,
                physics.MassScaleMilli,
                physics.RadiusScaleMilli,
                physics.PushScaleMilli,
                physics.BlocksCharacters,
                physics.CanBePushed,
                physics.ContactVelocityXMillimetersPerSecond,
                physics.ContactVelocityYMillimetersPerSecond,
                physics.ContactDisplacementXMillimeters,
                physics.ContactDisplacementYMillimeters,
                physics.ContactCount,
                posture.ProfileId,
                posture.HeightMillimeters,
                posture.RadiusMillimeters,
                posture.CenterYMillimeters,
                posture.BlocksCharacters,
                posture.CanBePushed,
                posture.Source.ToString(),
                posture.Priority,
                posture.SinceFrame,
                posture.ActionProfileId,
                posture.ActionRestorePolicy.ToString(),
                posture.ActionTransitionFrames,
                posture.ActionSourceActionId,
                posture.ActionStableEventId,
                presence.State.ToString(),
                presence.RequestedFrame,
                presence.DestroyedFrame,
                presence.SourceEntityId,
                presence.TraceId,
                battle.LifeState.ToString(),
                battle.DominantReason.ToString(),
                battle.GateFlags.ToString(),
                battle.SinceFrame,
                battle.LastTransitionFrame,
                (battle.SourceActionId).ToString(),
                battle.SourceTraceId,
                input.MoveXMilli,
                input.MoveYMilli,
                input.WorldMoveXMilli,
                input.WorldMoveYMilli,
                input.HasWorldMove,
                input.JumpPressed,
                input.JumpHeld,
                input.JumpReleased,
                input.AttackPrimaryPressed,
                input.AttackSecondaryPressed,
                input.DebugPrimaryPressed,
                input.DebugSecondaryPressed,
                input.SkillSlotPressedMask,
                input.DodgePressed,
                input.AbilitySlotPressed,
                input.AbilityIdPressed,
                entity.LastMoveInputXMilli,
                entity.LastMoveInputYMilli,
                entity.JumpBufferFramesRemaining,
                entity.CoyoteTimeFramesRemaining,
                entity.MovementMode.ToString(),
                entity.MovementLocked,
                entity.MovementControlScaleMilli,
                entity.RotationControlScaleMilli,
                entity.MovementProfileId,
                entity.MovementProfileFallback,
                entity.PhysicsEnabled,
                entity.CollisionFlags.ToString(),
                entity.SurfaceFrictionMilli,
                entity.SurfaceBounceSpeedCapYMillimetersPerSecond,
                surface.Kind.ToString(),
                (int)surface.Kind,
                surface.Elasticity.RawValue,
                surface.FixedBounceHeight.RawValue,
                lifecyclePresent,
                lifecycleState,
                teamPresent,
                teamId,
                gameplayScatterPresent,
                gameplayScatterCurrentPressure,
                gameplayScatterMaxPressure,
                gameplayScatterMaxDebt,
                gameplayScatterCurrentCohesionLimit,
                gameplayScatterRecoveryRate,
                gameplayScatterRecoveryDelayFrames,
                gameplayScatterBrokenRecoveryDelayFrames,
                gameplayScatterCurrentBand,
                gameplayScatterLastPressureFrame,
                gameplayScatterLastCohesionBreakFrame,
                gameplayScatterIsBroken,
                hasUnityView,
                unityViewXMillimeters,
                unityViewYMillimeters,
                unityViewZMillimeters,
                unityViewYawMilliDegrees,
                hasPhysicsBody,
                hasPhysicsBody ? physicsBody.Position.X.RawValue : 0L,
                hasPhysicsBody ? physicsBody.Position.Y.RawValue : 0L,
                hasPhysicsBody ? physicsBody.Position.Z.RawValue : 0L,
                presentationState,
                CharacterTestDeterministicUnits.AxisToMilli(entity.Animation.LocomotionBlend.x),
                CharacterTestDeterministicUnits.AxisToMilli(entity.Animation.LocomotionBlend.y),
                CharacterTestDeterministicUnits.ScaleToMilli(entity.Animation.LocomotionSpeed01),
                entity.Animation.ActionKey,
                entity.Animation.Grounded,
                entity.Animation.Airborne);
        }

        private static void CaptureGameplayIdentity(
            GameplayComponentWorld gameplayComponentWorld,
            GameplayEntityId gameplayEntityId,
            out bool lifecyclePresent,
            out string lifecycleState,
            out bool teamPresent,
            out int teamId)
        {
            lifecyclePresent = false;
            lifecycleState = string.Empty;
            teamPresent = false;
            teamId = 0;
            if (gameplayComponentWorld == null || !gameplayEntityId.IsValid)
                return;

            if (gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycleStore)
                && lifecycleStore.TryGet(gameplayEntityId, out GameplayLifecycleComponent lifecycle))
            {
                lifecyclePresent = true;
                lifecycleState = lifecycle.State.ToString();
            }

            if (gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teamStore)
                && teamStore.TryGet(gameplayEntityId, out GameplayTeamComponent team))
            {
                teamPresent = true;
                teamId = team.TeamId;
            }
        }

        private static void CaptureGameplayScatterPressure(
            GameplayComponentWorld gameplayComponentWorld,
            GameplayEntityId gameplayEntityId,
            out bool present,
            out int currentPressure,
            out int maxPressure,
            out int maxDebt,
            out int currentCohesionLimit,
            out int recoveryRate,
            out int recoveryDelayFrames,
            out int brokenRecoveryDelayFrames,
            out string currentBand,
            out long lastPressureFrame,
            out long lastCohesionBreakFrame,
            out bool isBroken)
        {
            present = false;
            currentPressure = 0;
            maxPressure = 0;
            maxDebt = 0;
            currentCohesionLimit = 0;
            recoveryRate = 0;
            recoveryDelayFrames = 0;
            brokenRecoveryDelayFrames = 0;
            currentBand = string.Empty;
            lastPressureFrame = 0L;
            lastCohesionBreakFrame = -1L;
            isBroken = false;
            if (gameplayComponentWorld == null
                || !gameplayEntityId.IsValid
                || !gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayScatterPressureComponent> scatterStore)
                || !scatterStore.TryGet(gameplayEntityId, out GameplayScatterPressureComponent scatter))
            {
                return;
            }

            present = true;
            currentPressure = scatter.CurrentScatterPressure;
            maxPressure = scatter.MaxScatterPressure;
            maxDebt = scatter.MaxScatterDebt;
            currentCohesionLimit = scatter.CurrentCohesionLimit;
            recoveryRate = scatter.RecoveryRate;
            recoveryDelayFrames = scatter.RecoveryDelayFrames;
            brokenRecoveryDelayFrames = scatter.BrokenRecoveryDelayFrames;
            currentBand = scatter.CurrentBand.ToString();
            lastPressureFrame = scatter.LastPressureFrame;
            lastCohesionBreakFrame = scatter.LastCohesionBreakFrame;
            isBroken = scatter.IsBroken;
        }

        private static void CaptureGameplayAttributes(
            GameplayComponentWorld gameplayComponentWorld,
            GameplayEntityId gameplayEntityId,
            out int attributeCount,
            out int attributeHash,
            out CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample[] attributeEntries)
        {
            attributeCount = 0;
            attributeHash = 0;
            attributeEntries = Array.Empty<CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample>();
            if (gameplayComponentWorld == null
                || !gameplayEntityId.IsValid
                || !gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributeStore)
                || !attributeStore.TryGet(gameplayEntityId, out GameplayAttributeSetComponent attributes))
            {
                return;
            }

            GameplayAttributeValue[] values = attributes.ToArray();
            attributeEntries = new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample[values.Length];
            unchecked
            {
                int hash = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    attributeEntries[i] = CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample.Capture(values[i]);
                    hash = (hash * 397) ^ values[i].AttributeId;
                    hash = (hash * 397) ^ values[i].BaseValue;
                    hash = (hash * 397) ^ values[i].CurrentValue;
                }

                attributeHash = hash;
            }

            attributeCount = attributeEntries.Length;
        }

        private static void CaptureGameplayBuffStatus(
            GameplayComponentWorld gameplayComponentWorld,
            GameplayEntityId gameplayEntityId,
            out int buffCount,
            out int buffHash,
            out CharacterTestRuntimeAiLabFrameSyncBuffEntrySample[] buffEntries,
            out int statusCount,
            out int statusHash,
            out CharacterTestRuntimeAiLabFrameSyncStatusEntrySample[] statusEntries)
        {
            buffCount = 0;
            buffHash = 0;
            buffEntries = Array.Empty<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample>();
            statusCount = 0;
            statusHash = 0;
            statusEntries = Array.Empty<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample>();
            if (gameplayComponentWorld == null || !gameplayEntityId.IsValid)
                return;

            if (gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore)
                && buffStore.TryGet(gameplayEntityId, out GameplayComponentBuffSetComponent buffs))
            {
                GameplayComponentBuffEntry[] entries = buffs.ToArray();
                buffEntries = new CharacterTestRuntimeAiLabFrameSyncBuffEntrySample[entries.Length];
                unchecked
                {
                    int hash = entries.Length;
                    for (int i = 0; i < entries.Length; i++)
                    {
                        buffEntries[i] = CharacterTestRuntimeAiLabFrameSyncBuffEntrySample.Capture(entries[i]);
                        hash = (hash * 397) ^ entries[i].BuffId;
                        hash = (hash * 397) ^ entries[i].StackCount;
                        hash = (hash * 397) ^ entries[i].StartFrame.GetHashCode();
                        hash = (hash * 397) ^ entries[i].EndFrame.GetHashCode();
                        hash = (hash * 397) ^ (entries[i].IsPermanent ? 1 : 0);
                        hash = (hash * 397) ^ entries[i].SourceId;
                    }

                    buffHash = hash;
                }

                buffCount = buffEntries.Length;
            }

            if (gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore)
                && statusStore.TryGet(gameplayEntityId, out GameplayStatusComponent statuses))
            {
                GameplayStatusEntry[] entries = statuses.ToEntries();
                statusEntries = new CharacterTestRuntimeAiLabFrameSyncStatusEntrySample[entries.Length];
                unchecked
                {
                    int hash = entries.Length;
                    for (int i = 0; i < entries.Length; i++)
                    {
                        statusEntries[i] = CharacterTestRuntimeAiLabFrameSyncStatusEntrySample.Capture(entries[i]);
                        hash = (hash * 397) ^ entries[i].StatusId.Value;
                        hash = (hash * 397) ^ entries[i].SourceBuffId;
                    }

                    statusHash = hash;
                }

                statusCount = statusEntries.Length;
            }
        }
    }

    /// <summary>
    /// Field-level Gameplay Attribute authority sample for Runtime AI Lab diagnostics.
    /// </summary>
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample
    {
        public CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
            int attributeId,
            int baseValue,
            int currentValue)
        {
            AttributeId = attributeId;
            BaseValue = baseValue;
            CurrentValue = currentValue;
        }

        public int AttributeId { get; }
        public int BaseValue { get; }
        public int CurrentValue { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample Capture(
            GameplayAttributeValue value)
        {
            return new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                value.AttributeId,
                value.BaseValue,
                value.CurrentValue);
        }
    }

    /// <summary>
    /// Field-level Buff authority sample for Runtime AI Lab diagnostics.
    /// </summary>
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncBuffEntrySample
    {
        public CharacterTestRuntimeAiLabFrameSyncBuffEntrySample(
            int buffId,
            int stackCount,
            long startFrame,
            long endFrame,
            bool isPermanent,
            int sourceId)
        {
            BuffId = buffId;
            StackCount = stackCount;
            StartFrame = startFrame;
            EndFrame = endFrame;
            IsPermanent = isPermanent;
            SourceId = sourceId;
        }

        public int BuffId { get; }
        public int StackCount { get; }
        public long StartFrame { get; }
        public long EndFrame { get; }
        public bool IsPermanent { get; }
        public int SourceId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncBuffEntrySample Capture(
            GameplayComponentBuffEntry entry)
        {
            return new CharacterTestRuntimeAiLabFrameSyncBuffEntrySample(
                entry.BuffId,
                entry.StackCount,
                entry.StartFrame,
                entry.EndFrame,
                entry.IsPermanent,
                entry.SourceId);
        }
    }

    /// <summary>
    /// Field-level projected Status authority sample for Runtime AI Lab diagnostics.
    /// </summary>
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncStatusEntrySample
    {
        public CharacterTestRuntimeAiLabFrameSyncStatusEntrySample(int statusId, int sourceBuffId)
        {
            StatusId = statusId;
            SourceBuffId = sourceBuffId;
        }

        public int StatusId { get; }
        public int SourceBuffId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncStatusEntrySample Capture(
            GameplayStatusEntry entry)
        {
            return new CharacterTestRuntimeAiLabFrameSyncStatusEntrySample(
                entry.StatusId.Value,
                entry.SourceBuffId);
        }
    }

    /// <summary>
    /// Read-only active projectile authority sample for Runtime AI Lab diagnostics.
    /// </summary>
}
