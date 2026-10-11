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

        private static FrameSyncCombatReplayResult RunFrameSyncDashStrikeCombatReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncCombatWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var rootMotion = new CharacterTestCharacterRootMotionSystem();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateFrameSyncMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            // DashStrike is an explicit Skill action in the converged codex action
            // set; it is no longer a command binding in the generated fixture.
            Assert.IsTrue(actionRuntime.TryStartActionSlot(
                attacker,
                CharacterTestActionSlotIds.Skill,
                CharacterTestCharacterActionConfigSource.DashStrikeActionId,
                RuntimeFrame.Zero,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0038)),
                "DashStrike action should start for frame sync validation. reject="
                + attacker.Action.LastRejectReason);
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);

            var records = new List<FrameSyncCombatFrameRecord>(Math.Max(1, maxFrames));
            int hitRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    actionRuntime.TickEntity(entities[entityIndex]);

                rootMotion.Tick(world, actionRuntime, frame);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, actionRuntime, frame);

                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        world.Entities,
                        actionRuntime);
                records.Add(new FrameSyncCombatFrameRecord(i, sample));

                if (hitRecordIndex < 0
                    && target.CombatState.LastHitFrame >= 0L
                    && target.CombatState.LastDamage > 0)
                {
                    hitRecordIndex = records.Count - 1;
                }

                if (hitRecordIndex >= 0 && records.Count >= hitRecordIndex + 8)
                    break;
            }

            return new FrameSyncCombatReplayResult(target.Id.Value, hitRecordIndex, records);
        }

        private static FrameSyncChainLightningReplayResult RunFrameSyncChainLightningReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncChainLightningWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity lockedTarget);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateFrameSyncMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => lockedTarget.Id.Value);

            Assert.IsTrue(
                actionRuntime.TryStartIntent(
                    attacker,
                    CharacterTestCharacterActionConfigSource.ChainLightningIntentId,
                    RuntimeFrame.Zero),
                "Chain Lightning intent should start for frame sync validation. reject=" + attacker.Action.LastRejectReason);

            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            var records = new List<FrameSyncChainLightningFrameRecord>(Math.Max(1, maxFrames));
            int chainRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    actionRuntime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, actionRuntime, frame);
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        world.Entities,
                        actionRuntime,
                        chainLightningTargetCount: combat.LastChainLightningTargetCount,
                        chainLightningVfxEvents: combat.LastFrameChainLightningVfxEvents);
                records.Add(new FrameSyncChainLightningFrameRecord(
                    i,
                    sample,
                    CaptureChainLightningTargetEntityIds(combat.LastFrameChainLightningVfxEvents)));

                if (chainRecordIndex < 0
                    && sample.ChainLightning.LastTargetCount > 0
                    && sample.ChainLightning.ArcEventCount > 0)
                {
                    chainRecordIndex = records.Count - 1;
                    break;
                }
            }

            return new FrameSyncChainLightningReplayResult(chainRecordIndex, records);
        }

        private static int[] CaptureChainLightningTargetEntityIds(
            IReadOnlyList<CharacterTestChainLightningVfxEvent> arcEvents)
        {
            int count = arcEvents != null ? arcEvents.Count : 0;
            if (count <= 0)
                return Array.Empty<int>();

            var targetEntityIds = new int[count];
            for (int i = 0; i < count; i++)
                targetEntityIds[i] = arcEvents[i].TargetEntityId;

            return targetEntityIds;
        }

        private static void AssertChainLightningTargetsWereDamaged(
            FrameSyncChainLightningFrameRecord record)
        {
            for (int i = 0; i < record.ChainTargetEntityIds.Count; i++)
            {
                int targetEntityId = record.ChainTargetEntityIds[i];
                Assert.IsTrue(
                    TryFindFrameSyncCharacterSample(
                        record.Sample.Characters,
                        targetEntityId,
                        out CharacterTestRuntimeAiLabFrameSyncCharacterSample target),
                    "Chain Lightning target sample should exist for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.Greater(
                    target.LastDamage,
                    0,
                    "Chain Lightning target should record damage for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.GreaterOrEqual(
                    target.LastHitFrame,
                    0L,
                    "Chain Lightning target should record a last-hit frame for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.Greater(
                    target.LastHitSourceEntityId,
                    0,
                    "Chain Lightning target should record the source entity for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.Greater(
                    target.LastHitTraceId,
                    0,
                    "Chain Lightning target should record the hit trace for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static void AssertChainLightningTargetIdsMatch(
            FrameSyncChainLightningFrameRecord expected,
            FrameSyncChainLightningFrameRecord actual)
        {
            Assert.AreEqual(
                expected.ChainTargetEntityIds.Count,
                actual.ChainTargetEntityIds.Count,
                "Chain Lightning replay should produce the same target count.");
            for (int i = 0; i < expected.ChainTargetEntityIds.Count; i++)
            {
                Assert.AreEqual(
                    expected.ChainTargetEntityIds[i],
                    actual.ChainTargetEntityIds[i],
                    "Chain Lightning replay should preserve target order at arc "
                    + i.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static void AssertChainLightningTargetCombatSamplesMatch(
            FrameSyncChainLightningFrameRecord expected,
            FrameSyncChainLightningFrameRecord actual)
        {
            for (int i = 0; i < expected.ChainTargetEntityIds.Count; i++)
            {
                int targetEntityId = expected.ChainTargetEntityIds[i];
                Assert.IsTrue(
                    TryFindFrameSyncCharacterSample(
                        expected.Sample.Characters,
                        targetEntityId,
                        out CharacterTestRuntimeAiLabFrameSyncCharacterSample expectedTarget),
                    "Expected Chain Lightning target sample should exist for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.IsTrue(
                    TryFindFrameSyncCharacterSample(
                        actual.Sample.Characters,
                        targetEntityId,
                        out CharacterTestRuntimeAiLabFrameSyncCharacterSample actualTarget),
                    "Replayed Chain Lightning target sample should exist for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.AreEqual(
                    expectedTarget.Health,
                    actualTarget.Health,
                    "Chain Lightning target health should replay exactly for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.AreEqual(
                    expectedTarget.LastDamage,
                    actualTarget.LastDamage,
                    "Chain Lightning target last damage should replay exactly for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.AreEqual(
                    expectedTarget.LastHitFrame,
                    actualTarget.LastHitFrame,
                    "Chain Lightning target last hit frame should replay exactly for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.AreEqual(
                    expectedTarget.LastHitSourceEntityId,
                    actualTarget.LastHitSourceEntityId,
                    "Chain Lightning target last hit source should replay exactly for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Assert.AreEqual(
                    expectedTarget.LastHitTraceId,
                    actualTarget.LastHitTraceId,
                    "Chain Lightning target trace should replay exactly for entity "
                    + targetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static FrameSyncLaserPathReplayResult RunFrameSyncLaserPathReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncLaserPathWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);

            Assert.IsTrue(
                actionRuntime.TryStartIntent(
                    attacker,
                    CharacterTestCharacterActionConfigSource.LaserLanceIntentId,
                    RuntimeFrame.Zero),
                "Laser Lance intent should start for frame sync validation. reject=" + attacker.Action.LastRejectReason);

            var records = new List<FrameSyncLaserPathFrameRecord>(Math.Max(1, maxFrames));
            int laserPathRecordIndex = -1;
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
                        laserPathVfxEvents: combat.LastFrameLaserPathVfxEvents);
                CharacterTestNetworkCharacterSnapshot characterSnapshot =
                    CharacterTestNetworkCharacterSnapshot.FromEntity(attacker, actionRuntime);
                records.Add(new FrameSyncLaserPathFrameRecord(
                    i,
                    characterSnapshot,
                    projectileState,
                    sample));

                if (laserPathRecordIndex < 0 && sample.LaserPath.EventCount > 0)
                {
                    laserPathRecordIndex = records.Count - 1;
                    break;
                }
            }

            return new FrameSyncLaserPathReplayResult(laserPathRecordIndex, records);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample ReplayFrameSyncLaserPathFromCheckpoint(
            CharacterTestNetworkCharacterSnapshot characterSnapshot,
            CombatProjectileRuntimeState checkpointProjectileState,
            int checkpointFrame,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncLaserPathWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);
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
                laserPathVfxEvents: combat.LastFrameLaserPathVfxEvents);
        }

        private static FrameSyncGuardAuraReplayResult RunFrameSyncGuardAuraReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity caster);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, caster);

            Assert.IsTrue(
                actionRuntime.TryStartIntent(
                    caster,
                    CharacterTestCharacterActionConfigSource.GuardAuraIntentId,
                    RuntimeFrame.Zero),
                "Guard Aura intent should start for replay validation. reject=" + caster.Action.LastRejectReason);

            var records = new List<FrameSyncGuardAuraFrameRecord>(Math.Max(1, maxFrames));
            int checkpointRecordIndex = -1;
            int firstAppliedRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(caster);
                combat.Tick(world, actionRuntime, frame);
                CharacterTestRuntimeAiLabFrameSyncSample sample = CaptureFrameSyncGuardAuraSample(
                    frame,
                    world,
                    actionRuntime,
                    combat);
                CharacterTestNetworkCharacterSnapshot characterSnapshot =
                    CharacterTestNetworkCharacterSnapshot.FromEntity(caster, actionRuntime);
                records.Add(new FrameSyncGuardAuraFrameRecord(i, characterSnapshot, sample));

                if (checkpointRecordIndex < 0
                    && sample.GuardAura.AppliedOrRefreshedCount <= 0
                    && sample.Characters.Count > 0
                    && sample.Characters[0].BuffCount <= 0)
                {
                    checkpointRecordIndex = records.Count - 1;
                }

                if (firstAppliedRecordIndex < 0
                    && sample.GuardAura.AppliedOrRefreshedCount > 0
                    && sample.Characters.Count > 0
                    && sample.Characters[0].BuffCount > 0)
                {
                    firstAppliedRecordIndex = records.Count - 1;
                    break;
                }
            }

            return new FrameSyncGuardAuraReplayResult(
                checkpointRecordIndex,
                firstAppliedRecordIndex,
                records);
        }

        private static FrameSyncGuardAuraFrameRecord ReplayFrameSyncGuardAuraFromCheckpoint(
            FrameSyncGuardAuraFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity caster);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(
                caster,
                actionRuntime,
                checkpoint.CharacterSnapshot);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, caster);

            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                actionRuntime.TickEntity(caster);
                combat.Tick(world, actionRuntime, frame);
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample = CaptureFrameSyncGuardAuraSample(
                new RuntimeFrame(targetFrame),
                world,
                actionRuntime,
                combat);
            CharacterTestNetworkCharacterSnapshot characterSnapshot =
                CharacterTestNetworkCharacterSnapshot.FromEntity(caster, actionRuntime);
            return new FrameSyncGuardAuraFrameRecord(targetFrame, characterSnapshot, sample);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample CaptureFrameSyncGuardAuraSample(
            RuntimeFrame frame,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            CharacterTestCharacterCombatTraceSystem combat)
        {
            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                frame,
                stateHash: 0L,
                world.Entities,
                actionRuntime,
                gameplayComponentWorld: world.GameplayComponentWorld,
                guardAuraLastTickSummary: combat.GuardAuraLastTickSummary,
                onHitBuffLastSummary: combat.OnHitBuffLastSummary);
        }

        private static FrameSyncPhysicalShieldReplayResult RunFrameSyncPhysicalShieldReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity caster);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, caster);

            Assert.IsTrue(
                actionRuntime.TryStartIntent(
                    caster,
                    CharacterTestCharacterActionConfigSource.PhysicalShieldIntentId,
                    RuntimeFrame.Zero),
                "Physical Shield intent should start for replay validation. reject="
                + caster.Action.LastRejectReason);

            var records = new List<FrameSyncPhysicalShieldFrameRecord>(Math.Max(1, maxFrames));
            int checkpointRecordIndex = -1;
            int firstActiveShieldRecordIndex = -1;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(caster);
                combat.Tick(world, actionRuntime, frame);

                CharacterTestPhysicalShieldRuntimeState shieldState =
                    combat.CreatePhysicalShieldStateSnapshot();
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CaptureFrameSyncPhysicalShieldSample(
                        frame,
                        world,
                        actionRuntime,
                        shieldState,
                        combat.PhysicalShieldLastTickSummary);
                CharacterTestNetworkCharacterSnapshot characterSnapshot =
                    CharacterTestNetworkCharacterSnapshot.FromEntity(caster, actionRuntime);
                records.Add(new FrameSyncPhysicalShieldFrameRecord(
                    i,
                    characterSnapshot,
                    shieldState,
                    sample,
                    ComputePhysicalShieldStateHash(shieldState)));

                if (checkpointRecordIndex < 0
                    && sample.PhysicalShield.ActiveShieldCount <= 0
                    && sample.PhysicalShield.ShieldCount <= 0)
                {
                    checkpointRecordIndex = records.Count - 1;
                }

                if (firstActiveShieldRecordIndex < 0
                    && sample.PhysicalShield.ActiveShieldCount > 0
                    && sample.PhysicalShield.ShieldCount > 0)
                {
                    firstActiveShieldRecordIndex = records.Count - 1;
                    break;
                }
            }

            return new FrameSyncPhysicalShieldReplayResult(
                checkpointRecordIndex,
                firstActiveShieldRecordIndex,
                records);
        }

        private static FrameSyncPhysicalShieldFrameRecord ReplayFrameSyncPhysicalShieldFromCheckpoint(
            FrameSyncPhysicalShieldFrameRecord checkpoint,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity caster);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            ApplyFrameSyncMeteorBarrageCharacterSnapshot(
                caster,
                actionRuntime,
                checkpoint.CharacterSnapshot);
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, caster);
            Assert.IsTrue(
                combat.TryApplyPhysicalShieldStateSnapshot(
                    checkpoint.PhysicalShieldState,
                    out string shieldFailure),
                shieldFailure);

            CharacterTestPhysicalShieldRuntimeState shieldState =
                combat.CreatePhysicalShieldStateSnapshot();
            for (int frameValue = checkpoint.Frame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                actionRuntime.TickEntity(caster);
                combat.Tick(world, actionRuntime, frame);
                shieldState = combat.CreatePhysicalShieldStateSnapshot();
            }

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CaptureFrameSyncPhysicalShieldSample(
                    new RuntimeFrame(targetFrame),
                    world,
                    actionRuntime,
                    shieldState,
                    combat.PhysicalShieldLastTickSummary);
            CharacterTestNetworkCharacterSnapshot characterSnapshot =
                CharacterTestNetworkCharacterSnapshot.FromEntity(caster, actionRuntime);
            return new FrameSyncPhysicalShieldFrameRecord(
                targetFrame,
                characterSnapshot,
                shieldState,
                sample,
                ComputePhysicalShieldStateHash(shieldState));
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample CaptureFrameSyncPhysicalShieldSample(
            RuntimeFrame frame,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            CharacterTestPhysicalShieldRuntimeState shieldState,
            CharacterTestPhysicalShieldTickSummary shieldSummary)
        {
            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                frame,
                stateHash: 0L,
                world.Entities,
                actionRuntime,
                physicalShieldState: shieldState,
                physicalShieldLastTickSummary: shieldSummary);
        }

        private static void AssertPhysicalShieldReplayMatches(
            FrameSyncPhysicalShieldFrameRecord expected,
            FrameSyncPhysicalShieldFrameRecord actual)
        {
            Assert.AreEqual(
                expected.PhysicalShieldState.ActiveShieldCount,
                actual.PhysicalShieldState.ActiveShieldCount,
                "Physical Shield active shield count should replay exactly.");
            Assert.AreEqual(
                expected.PhysicalShieldHash,
                actual.PhysicalShieldHash,
                "Physical Shield runtime hash should replay exactly.");
        }

        private static FrameSyncProjectileReplayResult RunFrameSyncProjectileReplay(
            int intentId,
            string expectedProjectileStableId,
            int maxFrames,
            bool useLockedTarget = false,
            bool expectTrackingTarget = false)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target,
                useLockedTarget);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            if (useLockedTarget)
                combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(actionRuntime.TryStartIntent(
                attacker,
                intentId,
                RuntimeFrame.Zero));

            var records = new List<FrameSyncProjectileFrameRecord>(Math.Max(1, maxFrames));
            int firstActiveRecordIndex = -1;
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
                        entities: null,
                        actionRuntime: null,
                        projectileState: projectileState);
                records.Add(new FrameSyncProjectileFrameRecord(i, projectileState, sample));

                if (firstActiveRecordIndex < 0
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
                    Assert.AreEqual(expectedProjectileStableId, projectile.DefinitionStableId);
                    if (expectTrackingTarget)
                    {
                        Assert.IsTrue(projectile.HasTrackingTarget, "Expected projectile to keep a tracking target.");
                        Assert.AreEqual(physicsRuntime.GetEntityId(target), projectile.TrackingTargetEntityId);
                    }

                    firstActiveRecordIndex = records.Count - 1;
                }
            }

            return new FrameSyncProjectileReplayResult(firstActiveRecordIndex, records);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample ReplayFrameSyncProjectileFromCheckpoint(
            CombatProjectileRuntimeState checkpointState,
            int checkpointFrame,
            int targetFrame,
            bool useLockedTarget = false)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out _,
                out CharacterTestCharacterEntity target,
                useLockedTarget);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            if (useLockedTarget)
                combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpointState, out string failure),
                failure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            for (int frameValue = checkpointFrame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
            }

            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                new RuntimeFrame(targetFrame),
                stateHash: 0L,
                entities: null,
                actionRuntime: null,
                projectileState: projectileState);
        }
    }
}
