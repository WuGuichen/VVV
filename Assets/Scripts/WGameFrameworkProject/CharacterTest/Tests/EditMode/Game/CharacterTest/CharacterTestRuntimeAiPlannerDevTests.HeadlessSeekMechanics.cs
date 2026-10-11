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
        public void RuntimeAiLabHeadlessSeek_ReplaysTornadoDamageTickAndAttractionExactly()
        {
            FrameSyncTornadoReplayResult firstRun = RunFrameSyncTornadoReplay(maxFrames: 96);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Tornado should spawn an active projectile before replay validation.");
            Assert.Greater(
                firstRun.FirstDamageTickRecordIndex,
                firstRun.FirstActiveRecordIndex,
                "Tornado replay validation needs a checkpoint before the first damage tick.");

            int checkpointIndex = Math.Max(
                firstRun.FirstActiveRecordIndex,
                firstRun.FirstDamageTickRecordIndex - 5);
            FrameSyncTornadoFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncTornadoFrameRecord expected = firstRun.Records[firstRun.FirstDamageTickRecordIndex];
            Assert.Greater(
                expected.Sample.Projectiles.Count,
                0,
                "Tornado projectile must still be active at the damage tick frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.TornadoStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.Greater(
                expected.TornadoDamageTickTargetCount,
                0,
                "Tornado damage tick must hit the target at the validation frame.");
            Assert.Greater(
                expected.InteractionLastFrameCount,
                0,
                "Tornado active attraction field must publish an interaction at the validation frame.");

            FrameSyncTornadoFrameRecord replayed =
                ReplayFrameSyncTornadoFromCheckpoint(checkpoint, expected.Frame);
            Assert.IsTrue(
                TryFindProjectileSample(
                    replayed.Sample.Projectiles,
                    expected.Sample.Projectiles[0].ProjectileId,
                    out CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile),
                "Replay should preserve Tornado projectile id "
                + expected.Sample.Projectiles[0].ProjectileId.ToString(CultureInfo.InvariantCulture)
                + ".");
            AssertProjectileSampleMatches(
                "Tornado projectile "
                + expected.Sample.Projectiles[0].ProjectileId.ToString(CultureInfo.InvariantCulture),
                expected.Sample.Projectiles[0],
                replayedProjectile);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed.Sample);
            Assert.IsTrue(report.Passed, report.SummaryText);
            AssertTornadoReplayMatches(expected, replayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysSwordWaveShortLifetimeProjectileExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.SwordWaveIntentId,
                CharacterTestFireballProjectileAdapter.SwordWaveStableId,
                maxFrames: CharacterTestCharacterActionConfigSource.SwordWaveProjectileFirstFrame
                    + CharacterTestFireballProjectileAdapter.SwordWaveLifetimeFrames
                    + 8);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Sword Wave to spawn an active projectile.");

            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 2);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Expected Sword Wave checkpoint to contain an active projectile.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.SwordWaveStableId,
                checkpoint.Sample.Projectiles[0].DefinitionStableId);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.SwordWaveLifetimeFrames,
                checkpoint.Sample.Projectiles[0].EffectiveLifetimeFrames,
                "Sword Wave checkpoint should preserve the per-shot effective lifetime.");

            int activeExpectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 8);
            FrameSyncProjectileFrameRecord activeExpected = firstRun.Records[activeExpectedIndex];
            Assert.Greater(
                activeExpected.Sample.Projectiles.Count,
                0,
                "Expected Sword Wave to remain active during its short lifetime.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.SwordWaveStableId,
                activeExpected.Sample.Projectiles[0].DefinitionStableId);
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.SwordWaveLifetimeFrames,
                activeExpected.Sample.Projectiles[0].EffectiveLifetimeFrames);

            CharacterTestRuntimeAiLabFrameSyncSample activeReplayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                activeExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport activeReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(activeExpected.Sample, activeReplayed);
            Assert.IsTrue(activeReport.Passed, activeReport.SummaryText);
            Assert.AreEqual(
                activeExpected.Sample.Projectiles.Count,
                activeReplayed.Projectiles.Count,
                "Sword Wave active projectile count should replay exactly.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.SwordWaveLifetimeFrames,
                activeReplayed.Projectiles[0].EffectiveLifetimeFrames,
                "Sword Wave active replay should preserve the per-shot effective lifetime.");

            int expiredExpectedIndex = Math.Min(
                firstRun.Records.Count - 1,
                firstRun.FirstActiveRecordIndex + CharacterTestFireballProjectileAdapter.SwordWaveLifetimeFrames + 2);
            FrameSyncProjectileFrameRecord expiredExpected = firstRun.Records[expiredExpectedIndex];
            Assert.AreEqual(
                0,
                expiredExpected.Sample.Projectiles.Count,
                "Expected Sword Wave projectile to expire before the next wave launch.");

            CharacterTestRuntimeAiLabFrameSyncSample expiredReplayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expiredExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport expiredReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expiredExpected.Sample, expiredReplayed);
            Assert.IsTrue(expiredReport.Passed, expiredReport.SummaryText);
            Assert.AreEqual(
                0,
                expiredReplayed.Projectiles.Count,
                "Sword Wave replay should remove the short-lived projectile at the same target frame.");
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysHookChainProjectileAndPullStateExactly()
        {
            FrameSyncHookChainReplayResult firstRun = RunFrameSyncHookChainReplay(maxFrames: 90);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Hook Chain should spawn a projectile before replay validation.");
            Assert.Greater(
                firstRun.FirstHookPullRecordIndex,
                firstRun.FirstActiveRecordIndex,
                "Hook Chain replay validation needs a checkpoint before the pull is registered.");

            FrameSyncHookChainFrameRecord projectileCheckpoint =
                firstRun.Records[firstRun.FirstActiveRecordIndex];
            FrameSyncHookChainFrameRecord pullExpected =
                firstRun.Records[firstRun.FirstHookPullRecordIndex];
            Assert.Greater(
                pullExpected.HookPullState.ActiveCount,
                0,
                "Hook Chain impact should register an active pull state.");

            FrameSyncHookChainFrameRecord pullReplayed =
                ReplayFrameSyncHookChainFromCheckpoint(
                    projectileCheckpoint,
                    pullExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport pullReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(pullExpected.Sample, pullReplayed.Sample);
            Assert.IsTrue(pullReport.Passed, pullReport.SummaryText);
            AssertHookPullReplayMatches(pullExpected, pullReplayed);

            int laterIndex = Math.Min(firstRun.Records.Count - 1, firstRun.FirstHookPullRecordIndex + 5);
            FrameSyncHookChainFrameRecord laterExpected = firstRun.Records[laterIndex];
            Assert.Greater(
                laterExpected.HookPullState.ActiveCount,
                0,
                "Hook Chain pull should still be active several frames after impact.");

            FrameSyncHookChainFrameRecord laterReplayed =
                ReplayFrameSyncHookChainFromCheckpoint(
                    pullExpected,
                    laterExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport laterReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(laterExpected.Sample, laterReplayed.Sample);
            Assert.IsTrue(laterReport.Passed, laterReport.SummaryText);
            AssertHookPullReplayMatches(laterExpected, laterReplayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysArcMineCarrierDropAndMineStateExactly()
        {
            FrameSyncArcMineReplayResult firstRun = RunFrameSyncArcMineReplay(maxFrames: 140);
            Assert.GreaterOrEqual(
                firstRun.FirstCarrierRecordIndex,
                0,
                "Arc Mine should spawn a carrier projectile before replay validation.");
            Assert.Greater(
                firstRun.FirstDropRecordIndex,
                firstRun.FirstCarrierRecordIndex,
                "Arc Mine replay validation needs a carrier checkpoint before the drop projectile is spawned.");
            Assert.Greater(
                firstRun.FirstActiveMineRecordIndex,
                firstRun.FirstDropRecordIndex,
                "Arc Mine replay validation needs a drop checkpoint before the mine is deployed.");

            FrameSyncArcMineFrameRecord carrierCheckpoint =
                firstRun.Records[firstRun.FirstCarrierRecordIndex];
            FrameSyncArcMineFrameRecord dropExpected =
                firstRun.Records[firstRun.FirstDropRecordIndex];
            FrameSyncArcMineFrameRecord dropReplayed =
                ReplayFrameSyncArcMineFromCheckpoint(
                    carrierCheckpoint,
                    dropExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport dropReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(dropExpected.Sample, dropReplayed.Sample);
            Assert.IsTrue(dropReport.Passed, dropReport.SummaryText);
            AssertArcMineReplayMatches(dropExpected, dropReplayed);

            FrameSyncArcMineFrameRecord mineExpected =
                firstRun.Records[firstRun.FirstActiveMineRecordIndex];
            FrameSyncArcMineFrameRecord mineReplayed =
                ReplayFrameSyncArcMineFromCheckpoint(
                    dropExpected,
                    mineExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport mineReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(mineExpected.Sample, mineReplayed.Sample);
            Assert.IsTrue(mineReport.Passed, mineReport.SummaryText);
            AssertArcMineReplayMatches(mineExpected, mineReplayed);

            int laterIndex = Math.Min(firstRun.Records.Count - 1, firstRun.FirstActiveMineRecordIndex + 5);
            FrameSyncArcMineFrameRecord laterExpected = firstRun.Records[laterIndex];
            Assert.Greater(
                laterExpected.ArcMineState.ActiveCount,
                0,
                "Arc Mine should still be active several frames after deployment.");

            FrameSyncArcMineFrameRecord laterReplayed =
                ReplayFrameSyncArcMineFromCheckpoint(
                    mineExpected,
                    laterExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport laterReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(laterExpected.Sample, laterReplayed.Sample);
            Assert.IsTrue(laterReport.Passed, laterReport.SummaryText);
            AssertArcMineReplayMatches(laterExpected, laterReplayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysRuneTurretDeployAndShotStateExactly()
        {
            FrameSyncRuneTurretReplayResult firstRun = RunFrameSyncRuneTurretReplay(maxFrames: 220);
            Assert.GreaterOrEqual(
                firstRun.FirstDeployCarrierRecordIndex,
                0,
                "Rune Turret should spawn a deploy carrier projectile before replay validation.");
            Assert.Greater(
                firstRun.FirstActiveTurretRecordIndex,
                firstRun.FirstDeployCarrierRecordIndex,
                "Rune Turret replay validation needs a deploy carrier checkpoint before the turret is active.");
            Assert.Greater(
                firstRun.FirstShotProjectileRecordIndex,
                firstRun.FirstActiveTurretRecordIndex,
                "Rune Turret replay validation needs an active turret checkpoint before a shot projectile is spawned.");

            FrameSyncRuneTurretFrameRecord deployCheckpoint =
                firstRun.Records[firstRun.FirstDeployCarrierRecordIndex];
            FrameSyncRuneTurretFrameRecord activeExpected =
                firstRun.Records[firstRun.FirstActiveTurretRecordIndex];
            FrameSyncRuneTurretFrameRecord activeReplayed =
                ReplayFrameSyncRuneTurretFromCheckpoint(
                    deployCheckpoint,
                    activeExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport activeReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(activeExpected.Sample, activeReplayed.Sample);
            Assert.IsTrue(activeReport.Passed, activeReport.SummaryText);
            AssertRuneTurretReplayMatches(activeExpected, activeReplayed);

            FrameSyncRuneTurretFrameRecord shotExpected =
                firstRun.Records[firstRun.FirstShotProjectileRecordIndex];
            FrameSyncRuneTurretFrameRecord shotReplayed =
                ReplayFrameSyncRuneTurretFromCheckpoint(
                    activeExpected,
                    shotExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport shotReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(shotExpected.Sample, shotReplayed.Sample);
            Assert.IsTrue(shotReport.Passed, shotReport.SummaryText);
            AssertRuneTurretReplayMatches(shotExpected, shotReplayed);

            int laterIndex = Math.Min(firstRun.Records.Count - 1, firstRun.FirstShotProjectileRecordIndex + 3);
            FrameSyncRuneTurretFrameRecord laterExpected = firstRun.Records[laterIndex];
            FrameSyncRuneTurretFrameRecord laterReplayed =
                ReplayFrameSyncRuneTurretFromCheckpoint(
                    shotExpected,
                    laterExpected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport laterReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(laterExpected.Sample, laterReplayed.Sample);
            Assert.IsTrue(laterReport.Passed, laterReport.SummaryText);
            AssertRuneTurretReplayMatches(laterExpected, laterReplayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysAreaConfinementChannelAndZoneExactly()
        {
            FrameSyncAreaConfinementReplayResult firstRun =
                RunFrameSyncAreaConfinementReplay(maxFrames: 110);
            Assert.GreaterOrEqual(
                firstRun.FirstPendingChannelRecordIndex,
                0,
                "Area Confinement should create a pending channel before the zone activates.");
            Assert.GreaterOrEqual(
                firstRun.FirstActiveZoneRecordIndex,
                0,
                "Area Confinement should activate a zone after the channel telegraph.");
            Assert.GreaterOrEqual(
                firstRun.FirstRootedTargetRecordIndex,
                0,
                "Area Confinement should apply Rooted buff/status to the target after the zone activates.");

            FrameSyncAreaConfinementFrameRecord pendingCheckpoint =
                firstRun.Records[firstRun.FirstPendingChannelRecordIndex];
            FrameSyncAreaConfinementFrameRecord activeExpected =
                firstRun.Records[firstRun.FirstActiveZoneRecordIndex];
            FrameSyncAreaConfinementFrameRecord activeReplayed =
                ReplayFrameSyncAreaConfinementFromCheckpoint(
                    pendingCheckpoint,
                    activeExpected.Frame);
            AssertAuthorityReplayMatches(activeExpected.Sample, activeReplayed.Sample);
            AssertAreaConfinementReplayMatches(activeExpected, activeReplayed);

            FrameSyncAreaConfinementFrameRecord rootedExpected =
                firstRun.Records[firstRun.FirstRootedTargetRecordIndex];
            FrameSyncAreaConfinementFrameRecord rootedReplayed =
                ReplayFrameSyncAreaConfinementFromCheckpoint(
                    pendingCheckpoint,
                    rootedExpected.Frame);
            AssertAreaConfinementReplayMatches(rootedExpected, rootedReplayed);
            AssertAreaConfinementTargetRooted(rootedExpected);
            AssertAreaConfinementTargetRooted(rootedReplayed);

            int laterIndex = Math.Min(
                firstRun.Records.Count - 1,
                firstRun.FirstActiveZoneRecordIndex + 8);
            FrameSyncAreaConfinementFrameRecord laterExpected = firstRun.Records[laterIndex];
            FrameSyncAreaConfinementFrameRecord laterReplayed =
                ReplayFrameSyncAreaConfinementFromCheckpoint(
                    activeExpected,
                    laterExpected.Frame);
            AssertAreaConfinementReplayMatches(laterExpected, laterReplayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_PoisonBottlePoolSnapshotRestoresMechanicSources()
        {
            CharacterTestMechanicSourceRuntimeState expectedState = CreatePoisonBottlePoolRuntimeState(
                centerXRaw: 123456L);
            var sourceCombat = new CharacterTestCharacterCombatTraceSystem();
            Assert.IsTrue(
                sourceCombat.TryApplyPoisonBottlePoolStateSnapshot(expectedState, out string sourceFailure),
                sourceFailure);

            CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot snapshot =
                CharacterTestNetworkPoisonBottlePoolSnapshotCodec.Capture(sourceCombat);
            Assert.NotNull(snapshot);
            Assert.AreEqual(1, snapshot.ActivePoolCount);

            var restoredCombat = new CharacterTestCharacterCombatTraceSystem();
            NetworkSnapshotApplyResult applyResult =
                CharacterTestNetworkPoisonBottlePoolSnapshotCodec.Apply(restoredCombat, snapshot);
            Assert.IsTrue(applyResult.Success, applyResult.Message);

            CharacterTestMechanicSourceRuntimeState restoredState =
                restoredCombat.CreatePoisonBottlePoolStateSnapshot();
            CharacterTestRuntimeAiLabFrameSyncSample expected = CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                RuntimeFrame.Zero,
                stateHash: 0L,
                entities: null,
                actionRuntime: null,
                poisonBottlePoolState: expectedState);
            CharacterTestRuntimeAiLabFrameSyncSample actual = CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                RuntimeFrame.Zero,
                stateHash: 0L,
                entities: null,
                actionRuntime: null,
                poisonBottlePoolState: restoredState);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsTrue(report.Passed, report.SummaryText);
            Assert.AreEqual(
                CharacterTestPoisonBottlePoolRuntime.SourceRuntimeModuleId,
                restoredState.ModuleId);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysPoisonBottlePoolSourceAndTickSummaryExactly()
        {
            FrameSyncPoisonBottlePoolReplayResult firstRun =
                RunFrameSyncPoisonBottlePoolReplay(maxFrames: 180);
            Assert.GreaterOrEqual(
                firstRun.FirstProjectileRecordIndex,
                0,
                "Poison Bottle should spawn a ballistic projectile before replay validation.");
            Assert.Greater(
                firstRun.FirstActivePoolRecordIndex,
                firstRun.FirstProjectileRecordIndex,
                "Poison Bottle Pool replay validation needs a projectile checkpoint before the pool is active.");
            Assert.GreaterOrEqual(
                firstRun.FirstPoisonTickRecordIndex,
                0,
                "Poison Bottle Pool should apply poison buff/status to a target before replay validation.");

            FrameSyncPoisonBottlePoolFrameRecord projectileCheckpoint =
                firstRun.Records[firstRun.FirstProjectileRecordIndex];
            FrameSyncPoisonBottlePoolFrameRecord poolExpected =
                firstRun.Records[firstRun.FirstActivePoolRecordIndex];
            FrameSyncPoisonBottlePoolFrameRecord poolReplayed =
                ReplayFrameSyncPoisonBottlePoolFromCheckpoint(
                    projectileCheckpoint,
                    poolExpected.Frame);
            AssertPoisonBottlePoolReplayMatches(poolExpected, poolReplayed);

            int laterIndex = Math.Min(
                firstRun.Records.Count - 1,
                firstRun.FirstActivePoolRecordIndex + 8);
            FrameSyncPoisonBottlePoolFrameRecord laterExpected = firstRun.Records[laterIndex];
            Assert.Greater(
                laterExpected.PoisonBottlePoolState.ActiveCount,
                0,
                "Poison Bottle Pool should still be active several frames after creation.");

            FrameSyncPoisonBottlePoolFrameRecord laterReplayed =
                ReplayFrameSyncPoisonBottlePoolFromCheckpoint(
                    projectileCheckpoint,
                    laterExpected.Frame);
            AssertPoisonBottlePoolReplayMatches(laterExpected, laterReplayed);

            FrameSyncPoisonBottlePoolFrameRecord tickExpected =
                firstRun.Records[firstRun.FirstPoisonTickRecordIndex];
            FrameSyncPoisonBottlePoolFrameRecord tickReplayed =
                ReplayFrameSyncPoisonBottlePoolFromCheckpoint(
                    projectileCheckpoint,
                    tickExpected.Frame);
            AssertPoisonBottlePoolReplayMatches(tickExpected, tickReplayed);
            AssertPoisonBottlePoolTargetPoisoned(tickExpected);
            AssertPoisonBottlePoolTargetPoisoned(tickReplayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_PhysicalShieldSnapshotRestoresActiveAndPotencyState()
        {
            CharacterTestPhysicalShieldRuntimeState expectedState = CreatePhysicalShieldRuntimeState(
                centerXRaw: 1000L,
                currentCohesion: 38);
            CharacterTestNetworkPhysicalShieldRuntimeSnapshot snapshot =
                CharacterTestNetworkPhysicalShieldRuntimeSnapshot.FromState(expectedState);
            CharacterTestPhysicalShieldRuntimeState restoredState = snapshot.ToState();
            CharacterTestRuntimeAiLabFrameSyncSample expected = CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                new RuntimeFrame(42),
                stateHash: 101L,
                entities: Array.Empty<CharacterTestCharacterEntity>(),
                actionRuntime: null,
                physicalShieldState: expectedState,
                physicalShieldLastTickSummary: new CharacterTestPhysicalShieldTickSummary(
                    activeCount: 1,
                    createdOrRefreshedCount: 1,
                    expiredCount: 0));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                new RuntimeFrame(42),
                stateHash: 101L,
                entities: Array.Empty<CharacterTestCharacterEntity>(),
                actionRuntime: null,
                physicalShieldState: restoredState,
                physicalShieldLastTickSummary: new CharacterTestPhysicalShieldTickSummary(
                    activeCount: 1,
                    createdOrRefreshedCount: 1,
                    expiredCount: 0));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsTrue(report.Passed, report.SummaryText);
            Assert.AreEqual(
                CharacterTestPhysicalShieldRuntimeState.ModuleIdValue,
                restoredState.ModuleId);
            Assert.AreEqual(1, restoredState.ActiveShieldCount);
            Assert.AreEqual(1, restoredState.ShieldRuntime.Shields.Count);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysPhysicalShieldAbilityExactly()
        {
            FrameSyncPhysicalShieldReplayResult firstRun =
                RunFrameSyncPhysicalShieldReplay(maxFrames: 72);
            Assert.GreaterOrEqual(
                firstRun.CheckpointRecordIndex,
                0,
                "Physical Shield replay needs a checkpoint before the shield request is emitted.");
            Assert.GreaterOrEqual(
                firstRun.FirstActiveShieldRecordIndex,
                0,
                "Physical Shield should create an active shield before replay validation.");

            FrameSyncPhysicalShieldFrameRecord checkpoint =
                firstRun.Records[firstRun.CheckpointRecordIndex];
            FrameSyncPhysicalShieldFrameRecord expected =
                firstRun.Records[firstRun.FirstActiveShieldRecordIndex];
            Assert.Greater(expected.PhysicalShieldState.ActiveShieldCount, 0);
            Assert.Greater(expected.Sample.PhysicalShield.ActiveShieldCount, 0);
            Assert.Greater(expected.Sample.PhysicalShield.ShieldCount, 0);

            FrameSyncPhysicalShieldFrameRecord replayed =
                ReplayFrameSyncPhysicalShieldFromCheckpoint(checkpoint, expected.Frame);
            Assert.Greater(replayed.PhysicalShieldState.ActiveShieldCount, 0);
            Assert.Greater(replayed.Sample.PhysicalShield.ActiveShieldCount, 0);
            Assert.Greater(replayed.Sample.PhysicalShield.ShieldCount, 0);

            AssertAuthorityReplayMatches(expected.Sample, replayed.Sample);
            AssertPhysicalShieldReplayMatches(expected, replayed);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysAbilityCooldownExactly()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
            Assert.IsTrue(harness.Modules.AbilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.DashStrikeAbilityId,
                out AbilityDefinitionConfig dashStrike));

            harness.Modules.AbilityRuntime.CommitActivation(entity.Id.Value, dashStrike, harness.CurrentFrame);
            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            var records = new List<RuntimeAiLabHeadlessFrameRecord>(24);
            for (int i = 0; i < 20; i++)
            {
                harness.Tick(1d / 60d);
                records.Add(CaptureHeadlessRecord(harness, participant, createCheckpoint: true));
            }

            RuntimeAiLabHeadlessFrameRecord checkpoint = records[4];
            RuntimeAiLabHeadlessFrameRecord expected = records[14];
            Assert.Greater(expected.Sample.AbilityCooldowns.Count, 0, "Expected active cooldown at target frame.");
            StringAssert.Contains("\"abilityRuntime\"", checkpoint.Checkpoint.PayloadJson);
            StringAssert.Contains("\"cooldowns\"", checkpoint.Checkpoint.PayloadJson);

            harness.Tick((int)CharacterTestAbilityIds.DashStrikeCooldownFrames + 10, 1d / 60d);
            AbilityRuntimeState expiredState = harness.Modules.AbilityRuntime.CreateStateSnapshot();
            Assert.AreEqual(0, expiredState.Cooldowns.Count, "Cooldown should expire before rollback restore.");

            harness.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);

            for (int frame = checkpoint.Frame + 1; frame <= expected.Frame; frame++)
                harness.Tick(1d / 60d);

            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    harness.Slice,
                    participant,
                    selectedFrame: null,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("headless"));
            CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample expectedDrain =
                expected.Sample.Audio.IntentDrain;
            CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample replayedDrain =
                replayed.Audio.IntentDrain;
            Assert.AreEqual(expectedDrain.Frame, replayedDrain.Frame);
            Assert.AreEqual(expectedDrain.RequestCount, replayedDrain.RequestCount);
            Assert.AreEqual(expectedDrain.PlayedCount, replayedDrain.PlayedCount);
            Assert.AreEqual(expectedDrain.FailedCount, replayedDrain.FailedCount);
            Assert.AreEqual(expectedDrain.DroppedCount, replayedDrain.DroppedCount);
            Assert.AreEqual(expectedDrain.SkippedCount, replayedDrain.SkippedCount);
            Assert.AreEqual(expectedDrain.LastCueId, replayedDrain.LastCueId);
            Assert.AreEqual(expectedDrain.LastEventId, replayedDrain.LastEventId);
            Assert.AreEqual(expectedDrain.LastEmitterId, replayedDrain.LastEmitterId);
        }

        [Test]
        public void CharacterTestAuthoritySnapshot_DoesNotCaptureOrHashPresentationState()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();

            RuntimeFrame frame = harness.CurrentFrame;
            long hashBefore = participant.ComputeStateHash(frame);
            RuntimeCustomState snapshotBefore = participant.CaptureSnapshot(frame);

            EnqueueFrameSyncVfxSpawn(
                harness,
                frame,
                sourceId: 101,
                targetId: 202,
                traceId: 966585);
            entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                new Vector2(0.75f, -0.5f),
                0.8f,
                grounded: false,
                airborne: true,
                actionActive: true,
                actionKey: "presentation.only",
                actionTransitionSeconds: 0.2f,
                actionHoldSeconds: 0.4f));
            long hashAfter = participant.ComputeStateHash(frame);
            RuntimeCustomState snapshotAfter = participant.CaptureSnapshot(frame);

            Assert.AreEqual(hashBefore, hashAfter, "Presentation-only state must not affect authority hash.");
            StringAssert.DoesNotContain("vfxRuntime", snapshotBefore.PayloadJson);
            StringAssert.DoesNotContain("vfxRuntime", snapshotAfter.PayloadJson);
            StringAssert.DoesNotContain("mxframework.vfx.runtime", snapshotAfter.PayloadJson);
            StringAssert.DoesNotContain("animationBlendXMilli", snapshotAfter.PayloadJson);
            StringAssert.DoesNotContain("animationSpeed01Milli", snapshotAfter.PayloadJson);
            StringAssert.DoesNotContain("animationGrounded", snapshotAfter.PayloadJson);
            StringAssert.DoesNotContain("animationActionActive", snapshotAfter.PayloadJson);
            StringAssert.DoesNotContain("presentation.only", snapshotAfter.PayloadJson);
        }
    }
}
