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

        private static FrameSyncArcMineReplayResult RunFrameSyncArcMineReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncArcMineWorld(
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
                CharacterTestCharacterActionConfigSource.ArcMineIntentId,
                RuntimeFrame.Zero),
                "Arc Mine intent should start for replay validation. reject=" + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncArcMineFrameRecord>(Math.Max(1, maxFrames));
            int firstCarrierRecordIndex = -1;
            int firstDropRecordIndex = -1;
            int firstActiveMineRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                CharacterTestArcMineRuntimeState arcMineState = combat.CreateArcMineStateSnapshot();
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        entities: null,
                        actionRuntime: null,
                        projectileState: projectileState);
                records.Add(new FrameSyncArcMineFrameRecord(
                    i,
                    projectileState,
                    arcMineState,
                    sample,
                    target.Transform,
                    ComputeArcMineStateHash(arcMineState)));

                for (int projectileIndex = 0;
                     projectileState != null && projectileIndex < projectileState.ActiveCount;
                     projectileIndex++)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[projectileIndex];
                    if (firstCarrierRecordIndex < 0
                        && projectile.DefinitionId == CharacterTestFireballProjectileAdapter.ArcMineDefinitionId)
                    {
                        firstCarrierRecordIndex = records.Count - 1;
                    }

                    if (firstDropRecordIndex < 0
                        && projectile.DefinitionId == CharacterTestFireballProjectileAdapter.ArcMineDropDefinitionId)
                    {
                        firstDropRecordIndex = records.Count - 1;
                    }
                }

                if (firstActiveMineRecordIndex < 0 && arcMineState.ActiveCount > 0)
                    firstActiveMineRecordIndex = records.Count - 1;
            }

            return new FrameSyncArcMineReplayResult(
                firstCarrierRecordIndex,
                firstDropRecordIndex,
                firstActiveMineRecordIndex,
                records);
        }

        private static FrameSyncArcMineFrameRecord ReplayFrameSyncArcMineFromCheckpoint(
            FrameSyncArcMineFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncArcMineWorld(
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
                combat.TryApplyArcMineStateSnapshot(checkpoint.ArcMineState, out string arcMineFailure),
                arcMineFailure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            CharacterTestArcMineRuntimeState arcMineState = combat.CreateArcMineStateSnapshot();
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
                arcMineState = combat.CreateArcMineStateSnapshot();
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    new RuntimeFrame(targetFrame),
                    stateHash: 0L,
                    entities: null,
                    actionRuntime: null,
                    projectileState: projectileState);
            return new FrameSyncArcMineFrameRecord(
                targetFrame,
                projectileState,
                arcMineState,
                sample,
                target.Transform,
                ComputeArcMineStateHash(arcMineState));
        }

        private static void AssertArcMineReplayMatches(
            FrameSyncArcMineFrameRecord expected,
            FrameSyncArcMineFrameRecord actual)
        {
            Assert.AreEqual(
                expected.ArcMineState.ActiveCount,
                actual.ArcMineState.ActiveCount,
                "Arc Mine active mine count should replay exactly.");
            Assert.AreEqual(
                expected.ArcMineHash,
                actual.ArcMineHash,
                "Arc Mine deployed mine state hash should replay exactly.");
        }

        private static FrameSyncRuneTurretReplayResult RunFrameSyncRuneTurretReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncRuneTurretWorld(
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
                CharacterTestCharacterActionConfigSource.RuneTurretIntentId,
                RuntimeFrame.Zero),
                "Rune Turret intent should start for replay validation. reject=" + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncRuneTurretFrameRecord>(Math.Max(1, maxFrames));
            int firstDeployCarrierRecordIndex = -1;
            int firstActiveTurretRecordIndex = -1;
            int firstShotProjectileRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                CharacterTestRuneTurretRuntimeState runeTurretState = combat.CreateRuneTurretStateSnapshot();
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        entities: null,
                        actionRuntime: null,
                        projectileState: projectileState,
                        runeTurretState: runeTurretState);
                records.Add(new FrameSyncRuneTurretFrameRecord(
                    i,
                    projectileState,
                    runeTurretState,
                    sample,
                    target.Transform,
                    ComputeRuneTurretStateHash(runeTurretState)));

                for (int projectileIndex = 0;
                     projectileState != null && projectileIndex < projectileState.ActiveCount;
                     projectileIndex++)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[projectileIndex];
                    if (firstDeployCarrierRecordIndex < 0
                        && projectile.DefinitionId == CharacterTestFireballProjectileAdapter.RuneTurretDeployDefinitionId)
                    {
                        firstDeployCarrierRecordIndex = records.Count - 1;
                    }

                    if (firstShotProjectileRecordIndex < 0
                        && projectile.DefinitionId == CharacterTestFireballProjectileAdapter.RuneTurretShotDefinitionId)
                    {
                        firstShotProjectileRecordIndex = records.Count - 1;
                    }
                }

                if (firstActiveTurretRecordIndex < 0 && runeTurretState.ActiveCount > 0)
                    firstActiveTurretRecordIndex = records.Count - 1;
            }

            return new FrameSyncRuneTurretReplayResult(
                firstDeployCarrierRecordIndex,
                firstActiveTurretRecordIndex,
                firstShotProjectileRecordIndex,
                records);
        }

        private static FrameSyncRuneTurretFrameRecord ReplayFrameSyncRuneTurretFromCheckpoint(
            FrameSyncRuneTurretFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncRuneTurretWorld(
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
                combat.TryApplyRuneTurretStateSnapshot(checkpoint.RuneTurretState, out string runeTurretFailure),
                runeTurretFailure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            CharacterTestRuneTurretRuntimeState runeTurretState = combat.CreateRuneTurretStateSnapshot();
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
                runeTurretState = combat.CreateRuneTurretStateSnapshot();
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    new RuntimeFrame(targetFrame),
                    stateHash: 0L,
                    entities: null,
                    actionRuntime: null,
                    projectileState: projectileState,
                    runeTurretState: runeTurretState);
            return new FrameSyncRuneTurretFrameRecord(
                targetFrame,
                projectileState,
                runeTurretState,
                sample,
                target.Transform,
                ComputeRuneTurretStateHash(runeTurretState));
        }

        private static void AssertRuneTurretReplayMatches(
            FrameSyncRuneTurretFrameRecord expected,
            FrameSyncRuneTurretFrameRecord actual)
        {
            Assert.AreEqual(
                expected.RuneTurretState.ActiveCount,
                actual.RuneTurretState.ActiveCount,
                "Rune Turret active turret count should replay exactly.");
            Assert.AreEqual(
                expected.RuneTurretHash,
                actual.RuneTurretHash,
                "Rune Turret deployed turret state hash should replay exactly.");
        }

        private static FrameSyncAreaConfinementReplayResult RunFrameSyncAreaConfinementReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncAreaConfinementWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, attacker);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(actionRuntime.TryStartIntent(
                    attacker,
                    CharacterTestCharacterActionConfigSource.AreaConfinementIntentId,
                    RuntimeFrame.Zero),
                "Area Confinement intent should start for replay validation. reject="
                + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncAreaConfinementFrameRecord>(Math.Max(1, maxFrames));
            int firstPendingRecordIndex = -1;
            int firstActiveZoneRecordIndex = -1;
            int firstRootedTargetRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);

                CharacterTestAreaConfinementRuntimeState areaState =
                    combat.CreateAreaConfinementStateSnapshot();
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        world.Entities,
                        actionRuntime,
                        gameplayComponentWorld: world.GameplayComponentWorld,
                        areaConfinementState: areaState);
                CharacterTestNetworkCharacterSnapshot characterSnapshot =
                    CharacterTestNetworkCharacterSnapshot.FromEntity(attacker, actionRuntime);
                CaptureFrameSyncGameplayEffects(
                    world.GameplayComponentWorld,
                    target.GameplayEntityId,
                    out bool hasTargetBuffs,
                    out GameplayComponentBuffSetComponent targetBuffs,
                    out bool hasTargetStatuses,
                    out GameplayStatusComponent targetStatuses);
                records.Add(new FrameSyncAreaConfinementFrameRecord(
                    i,
                    characterSnapshot,
                    CharacterTestNetworkCharacterSnapshot.FromEntity(target, actionRuntime),
                    areaState,
                    sample,
                    target.Id.Value,
                    hasTargetBuffs,
                    targetBuffs,
                    hasTargetStatuses,
                    targetStatuses,
                    ComputeAreaConfinementStateHash(areaState)));

                if (firstPendingRecordIndex < 0 && areaState.PendingCount > 0)
                    firstPendingRecordIndex = records.Count - 1;
                if (firstActiveZoneRecordIndex < 0 && areaState.ActiveZoneCount > 0)
                    firstActiveZoneRecordIndex = records.Count - 1;
                if (firstRootedTargetRecordIndex < 0
                    && TryFindFrameSyncCharacterSample(
                        sample.Characters,
                        target.Id.Value,
                        out CharacterTestRuntimeAiLabFrameSyncCharacterSample targetSample)
                    && targetSample.BuffCount > 0
                    && targetSample.StatusCount > 0)
                {
                    firstRootedTargetRecordIndex = records.Count - 1;
                }
            }

            return new FrameSyncAreaConfinementReplayResult(
                firstPendingRecordIndex,
                firstActiveZoneRecordIndex,
                firstRootedTargetRecordIndex,
                records);
        }

        private static FrameSyncAreaConfinementFrameRecord ReplayFrameSyncAreaConfinementFromCheckpoint(
            FrameSyncAreaConfinementFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncAreaConfinementWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(
                attacker,
                actionRuntime,
                checkpoint.CharacterSnapshot);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(
                target,
                actionRuntime,
                checkpoint.TargetCharacterSnapshot);
            RestoreFrameSyncGameplayEffects(
                world.GameplayComponentWorld,
                target.GameplayEntityId,
                checkpoint.HasTargetBuffs,
                checkpoint.TargetBuffs,
                checkpoint.HasTargetStatuses,
                checkpoint.TargetStatuses);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, attacker);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);
            Assert.IsTrue(
                combat.TryApplyAreaConfinementStateSnapshot(
                    checkpoint.AreaConfinementState,
                    out string areaFailure),
                areaFailure);

            CharacterTestAreaConfinementRuntimeState areaState =
                combat.CreateAreaConfinementStateSnapshot();
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);
                areaState = combat.CreateAreaConfinementStateSnapshot();
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    new RuntimeFrame(targetFrame),
                    stateHash: 0L,
                    world.Entities,
                    actionRuntime,
                    gameplayComponentWorld: world.GameplayComponentWorld,
                    areaConfinementState: areaState);
            CaptureFrameSyncGameplayEffects(
                world.GameplayComponentWorld,
                target.GameplayEntityId,
                out bool hasTargetBuffs,
                out GameplayComponentBuffSetComponent targetBuffs,
                out bool hasTargetStatuses,
                out GameplayStatusComponent targetStatuses);
            return new FrameSyncAreaConfinementFrameRecord(
                targetFrame,
                CharacterTestNetworkCharacterSnapshot.FromEntity(attacker, actionRuntime),
                CharacterTestNetworkCharacterSnapshot.FromEntity(target, actionRuntime),
                areaState,
                sample,
                target.Id.Value,
                hasTargetBuffs,
                targetBuffs,
                hasTargetStatuses,
                targetStatuses,
                ComputeAreaConfinementStateHash(areaState));
        }

        private static FrameSyncPoisonBottlePoolReplayResult RunFrameSyncPoisonBottlePoolReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncPoisonBottlePoolWorld(
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
                    CharacterTestCharacterActionConfigSource.PoisonBottleIntentId,
                    RuntimeFrame.Zero),
                "Poison Bottle intent should start for replay validation. reject="
                + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncPoisonBottlePoolFrameRecord>(Math.Max(1, maxFrames));
            int firstProjectileRecordIndex = -1;
            int firstActivePoolRecordIndex = -1;
            int firstPoisonTickRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                CharacterTestMechanicSourceRuntimeState poolState =
                    combat.CreatePoisonBottlePoolStateSnapshot();
                FrameSyncPoisonBottlePoolFrameRecord record =
                    CreatePoisonBottlePoolFrameRecord(
                        i,
                        projectileState,
                        poolState,
                        combat.PoisonBottlePoolLastTickSummary,
                        combat.LastPoisonTickTargetCount,
                        world.Entities,
                        world.GameplayComponentWorld,
                        attacker,
                        target,
                        actionRuntime);
                records.Add(record);

                if (firstProjectileRecordIndex < 0
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
                    Assert.AreEqual(
                        CharacterTestFireballProjectileAdapter.PoisonBottleStableId,
                        projectile.DefinitionStableId);
                    firstProjectileRecordIndex = records.Count - 1;
                }

                if (firstActivePoolRecordIndex < 0 && poolState.ActiveCount > 0)
                    firstActivePoolRecordIndex = records.Count - 1;
                if (firstPoisonTickRecordIndex < 0
                    && combat.LastPoisonTickTargetCount > 0
                    && TryFindFrameSyncCharacterSample(
                        record.Sample.Characters,
                        target.Id.Value,
                        out CharacterTestRuntimeAiLabFrameSyncCharacterSample targetSample)
                    && targetSample.BuffCount > 0
                    && targetSample.StatusCount > 0)
                {
                    firstPoisonTickRecordIndex = records.Count - 1;
                }
            }

            return new FrameSyncPoisonBottlePoolReplayResult(
                firstProjectileRecordIndex,
                firstActivePoolRecordIndex,
                firstPoisonTickRecordIndex,
                records);
        }

        private static FrameSyncPoisonBottlePoolFrameRecord ReplayFrameSyncPoisonBottlePoolFromCheckpoint(
            FrameSyncPoisonBottlePoolFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncPoisonBottlePoolWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(
                attacker,
                actionRuntime,
                checkpoint.AttackerSnapshot);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(
                target,
                actionRuntime,
                checkpoint.TargetSnapshot);
            RestoreFrameSyncGameplayEffects(
                world.GameplayComponentWorld,
                target.GameplayEntityId,
                checkpoint.HasTargetBuffs,
                checkpoint.TargetBuffs,
                checkpoint.HasTargetStatuses,
                checkpoint.TargetStatuses);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpoint.ProjectileState, out string projectileFailure),
                projectileFailure);
            Assert.IsTrue(
                combat.TryApplyPoisonBottlePoolStateSnapshot(
                    checkpoint.PoisonBottlePoolState,
                    out string poolFailure),
                poolFailure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            CharacterTestMechanicSourceRuntimeState poolState =
                combat.CreatePoisonBottlePoolStateSnapshot();
            CharacterTestPoisonBottlePoolTickSummary lastTickSummary = default;
            int lastPoisonTickTargetCount = 0;
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                actionRuntime.TickEntity(attacker);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
                poolState = combat.CreatePoisonBottlePoolStateSnapshot();
                lastTickSummary = combat.PoisonBottlePoolLastTickSummary;
                lastPoisonTickTargetCount = combat.LastPoisonTickTargetCount;
            }

            return CreatePoisonBottlePoolFrameRecord(
                targetFrame,
                projectileState,
                poolState,
                lastTickSummary,
                lastPoisonTickTargetCount,
                world.Entities,
                world.GameplayComponentWorld,
                attacker,
                target,
                actionRuntime);
        }

        private static FrameSyncPoisonBottlePoolFrameRecord CreatePoisonBottlePoolFrameRecord(
            int frame,
            CombatProjectileRuntimeState projectileState,
            CharacterTestMechanicSourceRuntimeState poolState,
            CharacterTestPoisonBottlePoolTickSummary lastTickSummary,
            int lastPoisonTickTargetCount,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            GameplayComponentWorld gameplayComponentWorld,
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    new RuntimeFrame(frame),
                    stateHash: 0L,
                    entities: entities,
                    actionRuntime: null,
                    projectileState: projectileState,
                    poisonBottlePoolState: poolState,
                    poisonBottlePoolLastTickSummary: lastTickSummary,
                    lastPoisonTickTargetCount: lastPoisonTickTargetCount,
                    gameplayComponentWorld: gameplayComponentWorld);
            CaptureFrameSyncGameplayEffects(
                gameplayComponentWorld,
                target.GameplayEntityId,
                out bool hasTargetBuffs,
                out GameplayComponentBuffSetComponent targetBuffs,
                out bool hasTargetStatuses,
                out GameplayStatusComponent targetStatuses);
            return new FrameSyncPoisonBottlePoolFrameRecord(
                frame,
                projectileState,
                poolState,
                sample,
                CharacterTestNetworkCharacterSnapshot.FromEntity(attacker, actionRuntime),
                CharacterTestNetworkCharacterSnapshot.FromEntity(target, actionRuntime),
                target.Id.Value,
                hasTargetBuffs,
                targetBuffs,
                hasTargetStatuses,
                targetStatuses,
                ComputePoisonBottlePoolStateHash(poolState));
        }

        private static void AssertPoisonBottlePoolReplayMatches(
            FrameSyncPoisonBottlePoolFrameRecord expected,
            FrameSyncPoisonBottlePoolFrameRecord actual)
        {
            Assert.AreEqual(
                expected.PoisonBottlePoolState.ActiveCount,
                actual.PoisonBottlePoolState.ActiveCount,
                "Poison Bottle Pool active source count should replay exactly.");
            Assert.AreEqual(
                expected.PoisonBottlePoolHash,
                actual.PoisonBottlePoolHash,
                "Poison Bottle Pool mechanic source state hash should replay exactly.");
            AssertAuthorityReplayMatches(expected.Sample, actual.Sample);
        }

        private static void AssertPoisonBottlePoolTargetPoisoned(FrameSyncPoisonBottlePoolFrameRecord record)
        {
            Assert.IsTrue(
                TryFindFrameSyncCharacterSample(
                    record.Sample.Characters,
                    record.TargetEntityId,
                    out CharacterTestRuntimeAiLabFrameSyncCharacterSample target),
                "Poison Bottle Pool target sample should be present.");
            Assert.Greater(
                target.BuffCount,
                0,
                "Poison Bottle Pool target should have Poisoned buff in the frame sync sample.");
            Assert.Greater(
                target.StatusCount,
                0,
                "Poison Bottle Pool target should project Poisoned status in the frame sync sample.");
        }
    }
}
