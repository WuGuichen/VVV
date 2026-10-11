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

        private static FrameSyncStunBoltReplayResult RunFrameSyncStunBoltReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target,
                includeTarget: true);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(actionRuntime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.StunBoltIntentId,
                RuntimeFrame.Zero),
                "Stun Bolt intent should start for replay validation. reject=" + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncStunBoltFrameRecord>(Math.Max(1, maxFrames));
            int firstActiveRecordIndex = -1;
            int checkpointRecordIndex = -1;
            int firstStunnedRecordIndex = -1;
            bool targetPlacedOnProjectileLine = false;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);
                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();

                if (!targetPlacedOnProjectileLine
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
                    Assert.AreEqual(
                        CharacterTestFireballProjectileAdapter.StunBoltStableId,
                        projectile.DefinitionStableId);
                    PlaceFrameSyncTargetOnProjectileLine(
                        physicsRuntime,
                        target,
                        projectile,
                        Fix64.FromInt(6));
                    targetPlacedOnProjectileLine = true;
                    checkpointRecordIndex = records.Count;
                }

                CharacterTestRuntimeAiLabFrameSyncSample sample = CaptureFrameSyncStunBoltSample(
                    frame,
                    world,
                    target,
                    projectileState,
                    combat.OnHitBuffLastSummary);
                CaptureFrameSyncStunBoltStatus(
                    world,
                    target,
                    out bool hasStunBuff,
                    out bool hasStunnedStatus);
                records.Add(new FrameSyncStunBoltFrameRecord(
                    i,
                    projectileState,
                    sample,
                    target.Transform,
                    hasStunBuff,
                    hasStunnedStatus));

                if (firstActiveRecordIndex < 0
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    firstActiveRecordIndex = records.Count - 1;
                }

                if (firstStunnedRecordIndex < 0 && hasStunBuff && hasStunnedStatus)
                    firstStunnedRecordIndex = records.Count - 1;

                if (firstStunnedRecordIndex >= 0 && records.Count >= firstStunnedRecordIndex + 4)
                    break;
            }

            return new FrameSyncStunBoltReplayResult(
                target.Id.Value,
                firstActiveRecordIndex,
                checkpointRecordIndex,
                firstStunnedRecordIndex,
                records);
        }

        private static FrameSyncStunBoltFrameRecord ReplayFrameSyncStunBoltFromCheckpoint(
            FrameSyncStunBoltFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out _,
                out CharacterTestCharacterEntity target,
                includeTarget: true);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            target.SetTransform(checkpoint.TargetTransform);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);
            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpoint.ProjectileState, out string failure),
                failure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample = CaptureFrameSyncStunBoltSample(
                new RuntimeFrame(targetFrame),
                world,
                target,
                projectileState,
                combat.OnHitBuffLastSummary);
            CaptureFrameSyncStunBoltStatus(
                world,
                target,
                out bool hasStunBuff,
                out bool hasStunnedStatus);
            return new FrameSyncStunBoltFrameRecord(
                targetFrame,
                projectileState,
                sample,
                target.Transform,
                hasStunBuff,
                hasStunnedStatus);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample CaptureFrameSyncStunBoltSample(
            RuntimeFrame frame,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target,
            CombatProjectileRuntimeState projectileState,
            CombatOnHitBuffApplicationSummary onHitBuffLastSummary)
        {
            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                frame,
                stateHash: 0L,
                entities: new[] { target },
                actionRuntime: null,
                projectileState: projectileState,
                gameplayComponentWorld: world.GameplayComponentWorld,
                onHitBuffLastSummary: onHitBuffLastSummary);
        }

        private static void CaptureFrameSyncStunBoltStatus(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target,
            out bool hasStunBuff,
            out bool hasStunnedStatus)
        {
            hasStunBuff = false;
            hasStunnedStatus = false;
            if (world?.GameplayComponentWorld == null || target == null)
                return;

            if (world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore)
                && buffStore.TryGet(target.GameplayEntityId, out GameplayComponentBuffSetComponent buffs))
            {
                hasStunBuff = buffs.TryGet(CharacterTestStunIds.StunBuffId, out _);
            }

            if (world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore)
                && statusStore.TryGet(target.GameplayEntityId, out GameplayStatusComponent statuses))
            {
                hasStunnedStatus = statuses.Contains(
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId));
            }
        }

        private static void AssertStunBoltTargetStunned(
            FrameSyncStunBoltFrameRecord record,
            int targetEntityId)
        {
            Assert.IsTrue(record.TargetHasStunBuff, "Stun Bolt target should have the Stunned buff.");
            Assert.IsTrue(record.TargetHasStunnedStatus, "Stun Bolt target should project the Stunned status.");
            Assert.IsTrue(
                TryFindFrameSyncCharacterSample(
                    record.Sample.Characters,
                    targetEntityId,
                    out CharacterTestRuntimeAiLabFrameSyncCharacterSample target),
                "Stun Bolt target sample should be present.");
            Assert.Greater(target.BuffCount, 0, "Stun Bolt target buff sample should be captured.");
            Assert.AreNotEqual(0, target.BuffHash, "Stun Bolt target buff hash should identify the Stunned buff state.");
            Assert.Greater(target.StatusCount, 0, "Stun Bolt target status sample should be captured.");
            Assert.AreNotEqual(0, target.StatusHash, "Stun Bolt target status hash should identify the Stunned status state.");
            Assert.IsTrue(
                HasFrameSyncBuffEntry(target, CharacterTestStunIds.StunBuffId),
                "Stun Bolt target Stunned buff should be captured as a field-level buff entry.");
            Assert.IsTrue(
                HasFrameSyncStatusEntry(target, CharacterTestStunIds.StunnedStatusId),
                "Stun Bolt target Stunned status should be captured as a field-level status entry.");
            Assert.Greater(
                record.Sample.GuardAura.OnHitRequestedBuffCount,
                0,
                "Stun Bolt impact frame should record an OnHitBuff request in the frame sync sample.");
        }

        private static bool HasFrameSyncBuffEntry(
            CharacterTestRuntimeAiLabFrameSyncCharacterSample sample,
            int buffId)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncBuffEntrySample> entries =
                sample.BuffEntries;
            int count = entries != null ? entries.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (entries[i].BuffId == buffId)
                    return true;
            }

            return false;
        }

        private static bool HasFrameSyncStatusEntry(
            CharacterTestRuntimeAiLabFrameSyncCharacterSample sample,
            int statusId)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncStatusEntrySample> entries =
                sample.StatusEntries;
            int count = entries != null ? entries.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (entries[i].StatusId == statusId)
                    return true;
            }

            return false;
        }

        private static bool TryFindFrameSyncCharacterSample(
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

        private static FrameSyncMeteorBarrageRunnerReplayResult RunFrameSyncMeteorBarrageRunnerReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(actionRuntime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                RuntimeFrame.Zero));

            var records = new List<FrameSyncMeteorBarrageRunnerFrameRecord>(Math.Max(1, maxFrames));
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);
                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        entities: world.Entities,
                        actionRuntime: actionRuntime,
                        projectileState: projectileState,
                        interactionHistory: combat.InteractionHistory);
                CharacterTestNetworkCharacterSnapshot characterSnapshot =
                    CharacterTestNetworkCharacterSnapshot.FromEntity(attacker, actionRuntime);
                records.Add(new FrameSyncMeteorBarrageRunnerFrameRecord(
                    i,
                    characterSnapshot,
                    projectileState,
                    sample));
            }

            return new FrameSyncMeteorBarrageRunnerReplayResult(records);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample ReplayFrameSyncMeteorBarrageRunnerFromCheckpoint(
            CharacterTestNetworkCharacterSnapshot characterSnapshot,
            CombatProjectileRuntimeState checkpointProjectileState,
            int checkpointFrame,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(attacker, actionRuntime, characterSnapshot);
            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpointProjectileState, out string failure),
                failure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            for (int frameValue = checkpointFrame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
            }

            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                new RuntimeFrame(targetFrame),
                stateHash: 0L,
                entities: world.Entities,
                actionRuntime: actionRuntime,
                projectileState: projectileState,
                interactionHistory: combat.InteractionHistory);
        }

        private static int FindFirstInteractionRecordIndex(
            IReadOnlyList<FrameSyncMeteorBarrageRunnerFrameRecord> records,
            int startIndex)
        {
            int count = records != null ? records.Count : 0;
            int firstIndex = Math.Max(0, startIndex);
            for (int i = firstIndex; i < count; i++)
            {
                if (records[i].Sample.Interaction.LastFrameEventCount > 0)
                    return i;
            }

            return -1;
        }

        private static void ApplyFrameSyncMeteorBarrageCharacterSnapshot(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterActionRuntime actionRuntime,
            CharacterTestNetworkCharacterSnapshot snapshot)
        {
            entity.SetControl(new CharacterTestCharacterControlComponent(
                snapshot.ControlOwner,
                snapshot.RuntimeAiPlannerProfileSlot,
                snapshot.CreateRuntimeAiBehaviorSelection()));
            entity.SetTransform(snapshot.CreateTransformComponent());
            entity.SetInput(snapshot.CreateInputComponent());
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                snapshot.ActiveActionId,
                snapshot.ActionState,
                snapshot.ActionPhase,
                snapshot.ActionLocalFrame,
                snapshot.LastRejection,
                snapshot.LastAnimationActionKey,
                snapshot.AnimationActionKeyThisFrame,
                snapshot.AnimationTransitionFrames,
                snapshot.AnimationHoldFrames,
                snapshot.JumpImpulseYMillimetersPerSecond,
                snapshot.ActionMovementMode,
                snapshot.ActionMovementLocked,
                snapshot.ActionMovementControlScaleMilli,
                snapshot.ActionRotationControlScaleMilli));
            Assert.IsTrue(actionRuntime.TryRestoreRunner(
                entity.ComponentWorld,
                entity.Id,
                snapshot.CreateActionExecutionState(),
                out string actionRestoreDiagnostic), actionRestoreDiagnostic);
            entity.SetAnimation(CharacterTestCharacterAnimationComponent.IdleGrounded);
            entity.SetMotionStateDeterministic(
                snapshot.VerticalVelocityMillimetersPerSecond,
                snapshot.LastMoveInputXMilli,
                snapshot.LastMoveInputYMilli,
                snapshot.IsGrounded,
                snapshot.HorizontalVelocityXMillimetersPerSecond,
                snapshot.HorizontalVelocityYMillimetersPerSecond,
                snapshot.DesiredVelocityXMillimetersPerSecond,
                snapshot.DesiredVelocityYMillimetersPerSecond,
                snapshot.JumpBufferFramesRemaining,
                snapshot.CoyoteTimeFramesRemaining,
                snapshot.MovementMode,
                snapshot.MovementLocked,
                snapshot.MovementControlScaleMilli,
                snapshot.RotationControlScaleMilli,
                snapshot.MovementProfileId,
                snapshot.MovementProfileFallback,
                snapshot.PhysicsEnabled,
                snapshot.CollisionFlags,
                snapshot.SurfaceFrictionMilli,
                snapshot.CreateSurfaceMaterial(),
                snapshot.SurfaceBounceSpeedCapYMillimetersPerSecond);
            CharacterTestCharacterPhysicsComponent physics =
                snapshot.CreatePhysicsComponent(entity.Physics);
            entity.SetPhysics(physics);
            entity.SetColliderPosture(snapshot.CreateColliderPostureComponent(physics));
            entity.SetCombatState(new CharacterTestCharacterCombatStateComponent(
                snapshot.MaxHealth,
                snapshot.Health,
                snapshot.HitStunFramesRemaining,
                snapshot.LastDamage,
                snapshot.LastHitFrame,
                snapshot.LastHitSourceEntityId,
                snapshot.LastHitTraceId));
            entity.SetLivingNaturalAttributes(snapshot.CreateLivingNaturalAttributesRuntimeState());
            if (snapshot.HasExactBattleState)
                entity.ComponentWorld.SetBattleState(
                    entity.Id,
                    snapshot.CreateBattleStateComponent());
        }

        private static void CaptureFrameSyncGameplayEffects(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            out bool hasBuffs,
            out GameplayComponentBuffSetComponent buffs,
            out bool hasStatuses,
            out GameplayStatusComponent statuses)
        {
            buffs = default;
            hasBuffs = world != null
                && world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore)
                && buffStore.TryGet(entityId, out buffs);

            statuses = default;
            hasStatuses = world != null
                && world.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore)
                && statusStore.TryGet(entityId, out statuses);
        }

        private static void RestoreFrameSyncGameplayEffects(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            bool hasBuffs,
            GameplayComponentBuffSetComponent buffs,
            bool hasStatuses,
            GameplayStatusComponent statuses)
        {
            GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore =
                world.GetOrCreateStore<GameplayComponentBuffSetComponent>();
            if (hasBuffs)
                buffStore.Set(entityId, buffs);
            else
                buffStore.Remove(entityId);

            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GetOrCreateStore<GameplayStatusComponent>();
            if (hasStatuses)
                statusStore.Set(entityId, statuses);
            else
                statusStore.Remove(entityId);
        }

        private static FrameSyncHookChainReplayResult RunFrameSyncHookChainReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncHookChainWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(actionRuntime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.HookChainIntentId,
                RuntimeFrame.Zero));

            var records = new List<FrameSyncHookChainFrameRecord>(Math.Max(1, maxFrames));
            int firstActiveRecordIndex = -1;
            int firstHookPullRecordIndex = -1;
            bool targetPlacedOnProjectileLine = false;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                if (!targetPlacedOnProjectileLine
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
                    Assert.AreEqual(
                        CharacterTestFireballProjectileAdapter.HookChainStableId,
                        projectile.DefinitionStableId);
                    PlaceFrameSyncTargetOnProjectileLine(
                        physicsRuntime,
                        target,
                        projectile,
                        Fix64.FromInt(6));
                    targetPlacedOnProjectileLine = true;
                }

                CharacterTestHookPullRuntimeState hookPullState = combat.CreateHookPullStateSnapshot();
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        entities: null,
                        actionRuntime: null,
                        projectileState: projectileState);
                records.Add(new FrameSyncHookChainFrameRecord(
                    i,
                    projectileState,
                    hookPullState,
                    sample,
                    target.Transform,
                    ComputeHookPullStateHash(hookPullState)));

                if (firstActiveRecordIndex < 0
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    firstActiveRecordIndex = records.Count - 1;
                }

                if (firstHookPullRecordIndex < 0 && hookPullState.ActiveCount > 0)
                    firstHookPullRecordIndex = records.Count - 1;
            }

            return new FrameSyncHookChainReplayResult(
                firstActiveRecordIndex,
                firstHookPullRecordIndex,
                records);
        }

        private static FrameSyncHookChainFrameRecord ReplayFrameSyncHookChainFromCheckpoint(
            FrameSyncHookChainFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncHookChainWorld(
                out _,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            target.SetTransform(checkpoint.TargetTransform);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpoint.ProjectileState, out string projectileFailure),
                projectileFailure);
            Assert.IsTrue(
                combat.TryApplyHookPullStateSnapshot(checkpoint.HookPullState, out string hookPullFailure),
                hookPullFailure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            CharacterTestHookPullRuntimeState hookPullState = combat.CreateHookPullStateSnapshot();
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
                hookPullState = combat.CreateHookPullStateSnapshot();
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    new RuntimeFrame(targetFrame),
                    stateHash: 0L,
                    entities: null,
                    actionRuntime: null,
                    projectileState: projectileState);
            return new FrameSyncHookChainFrameRecord(
                targetFrame,
                projectileState,
                hookPullState,
                sample,
                target.Transform,
                ComputeHookPullStateHash(hookPullState));
        }

        private static void AssertHookPullReplayMatches(
            FrameSyncHookChainFrameRecord expected,
            FrameSyncHookChainFrameRecord actual)
        {
            Assert.AreEqual(
                expected.HookPullState.ActiveCount,
                actual.HookPullState.ActiveCount,
                "Hook Chain active pull count should replay exactly.");
            Assert.AreEqual(
                expected.HookPullHash,
                actual.HookPullHash,
                "Hook Chain pull state hash should replay exactly.");
        }

        private static FrameSyncTornadoReplayResult RunFrameSyncTornadoReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncTornadoWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(actionRuntime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero),
                "Tornado intent should start for replay validation. reject=" + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncTornadoFrameRecord>(Math.Max(1, maxFrames));
            int firstActiveRecordIndex = -1;
            int firstDamageTickRecordIndex = -1;
            bool targetPlacedForTick = false;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                if (!targetPlacedForTick
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
                    Assert.AreEqual(
                        CharacterTestFireballProjectileAdapter.TornadoStableId,
                        projectile.DefinitionStableId);
                    PlaceFrameSyncTargetOnProjectileLine(
                        physicsRuntime,
                        target,
                        projectile,
                        Fix64.FromRatio(3, 2));
                    targetPlacedForTick = true;
                }

                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CaptureFrameSyncTornadoSample(frame, target, projectileState, combat);
                records.Add(new FrameSyncTornadoFrameRecord(
                    i,
                    projectileState,
                    sample,
                    target.Transform,
                    combat.LastTornadoDamageTickTargetCount,
                    combat.InteractionHistory.LastFrameEventCount,
                    ComputeInteractionLastFrameHash(combat.InteractionHistory)));

                if (firstActiveRecordIndex < 0
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    firstActiveRecordIndex = records.Count - 1;
                }

                if (firstDamageTickRecordIndex < 0 && combat.LastTornadoDamageTickTargetCount > 0)
                    firstDamageTickRecordIndex = records.Count - 1;
            }

            return new FrameSyncTornadoReplayResult(
                firstActiveRecordIndex,
                firstDamageTickRecordIndex,
                records);
        }

        private static FrameSyncTornadoFrameRecord ReplayFrameSyncTornadoFromCheckpoint(
            FrameSyncTornadoFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncTornadoWorld(
                out _,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            target.SetTransform(checkpoint.TargetTransform);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpoint.ProjectileState, out string projectileFailure),
                projectileFailure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            int tornadoDamageTickTargetCount = 0;
            int interactionLastFrameCount = 0;
            long interactionLastFrameHash = 0L;
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
                tornadoDamageTickTargetCount = combat.LastTornadoDamageTickTargetCount;
                interactionLastFrameCount = combat.InteractionHistory.LastFrameEventCount;
                interactionLastFrameHash = ComputeInteractionLastFrameHash(combat.InteractionHistory);
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CaptureFrameSyncTornadoSample(new RuntimeFrame(targetFrame), target, projectileState, combat);
            return new FrameSyncTornadoFrameRecord(
                targetFrame,
                projectileState,
                sample,
                target.Transform,
                tornadoDamageTickTargetCount,
                interactionLastFrameCount,
                interactionLastFrameHash);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample CaptureFrameSyncTornadoSample(
            RuntimeFrame frame,
            CharacterTestCharacterEntity target,
            CombatProjectileRuntimeState projectileState,
            CharacterTestCharacterCombatTraceSystem combat)
        {
            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                frame,
                stateHash: 0L,
                new[] { target },
                actionRuntime: null,
                projectileState: projectileState,
                tornadoDamageTickTargetCount: combat != null
                    ? combat.LastTornadoDamageTickTargetCount
                    : 0,
                tornadoInteractionHistory: combat?.InteractionHistory);
        }

        private static void AssertTornadoReplayMatches(
            FrameSyncTornadoFrameRecord expected,
            FrameSyncTornadoFrameRecord actual)
        {
            Assert.AreEqual(
                expected.TargetTransform.XMillimeters,
                actual.TargetTransform.XMillimeters,
                "Tornado target transform X should replay exactly.");
            Assert.AreEqual(
                expected.TargetTransform.YMillimeters,
                actual.TargetTransform.YMillimeters,
                "Tornado target transform Y should replay exactly.");
            Assert.AreEqual(
                expected.TargetTransform.ZMillimeters,
                actual.TargetTransform.ZMillimeters,
                "Tornado target transform Z should replay exactly.");
            Assert.AreEqual(
                expected.TargetTransform.YawMilliDegrees,
                actual.TargetTransform.YawMilliDegrees,
                "Tornado target yaw should replay exactly.");
            Assert.AreEqual(
                expected.TornadoDamageTickTargetCount,
                actual.TornadoDamageTickTargetCount,
                "Tornado damage tick target count should replay exactly.");
            Assert.AreEqual(
                expected.InteractionLastFrameCount,
                actual.InteractionLastFrameCount,
                "Tornado attraction interaction count should replay exactly.");
            Assert.AreEqual(
                expected.InteractionLastFrameHash,
                actual.InteractionLastFrameHash,
                "Tornado attraction interaction payload should replay exactly.");
            Assert.Greater(
                expected.Sample.Characters.Count,
                0,
                "Tornado expected sample should include the target combat sample.");
            CharacterTestRuntimeAiLabFrameSyncCharacterSample expectedTarget = expected.Sample.Characters[0];
            Assert.IsTrue(
                TryFindFrameSyncCharacterSample(
                    actual.Sample.Characters,
                    expectedTarget.EntityId,
                    out CharacterTestRuntimeAiLabFrameSyncCharacterSample actualTarget),
                "Tornado replay should preserve target entity sample "
                + expectedTarget.EntityId.ToString(CultureInfo.InvariantCulture)
                + ".");
            Assert.AreEqual(expectedTarget.Health, actualTarget.Health, "Tornado target health should replay exactly.");
            Assert.AreEqual(
                expectedTarget.LastDamage,
                actualTarget.LastDamage,
                "Tornado target last damage should replay exactly.");
            Assert.AreEqual(
                expectedTarget.LastHitFrame,
                actualTarget.LastHitFrame,
                "Tornado target last hit frame should replay exactly.");
            Assert.AreEqual(
                expectedTarget.LastHitTraceId,
                actualTarget.LastHitTraceId,
                "Tornado target last hit trace should replay exactly.");
        }
    }
}
