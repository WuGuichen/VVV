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
        public void RuntimeAiLabHeadlessSeek_ReplaysGuardAuraBuffExactly()
        {
            FrameSyncGuardAuraReplayResult firstRun = RunFrameSyncGuardAuraReplay(maxFrames: 32);
            Assert.GreaterOrEqual(
                firstRun.CheckpointRecordIndex,
                0,
                "Guard Aura replay needs a checkpoint before the aura request is emitted.");
            Assert.GreaterOrEqual(
                firstRun.FirstAppliedRecordIndex,
                0,
                "Guard Aura should refresh at least one aura buff before replay validation.");

            FrameSyncGuardAuraFrameRecord checkpoint = firstRun.Records[firstRun.CheckpointRecordIndex];
            FrameSyncGuardAuraFrameRecord expected = firstRun.Records[firstRun.FirstAppliedRecordIndex];
            Assert.Greater(expected.Sample.GuardAura.AppliedOrRefreshedCount, 0);
            Assert.Greater(expected.Sample.Characters.Count, 0);
            Assert.Greater(expected.Sample.Characters[0].BuffCount, 0);
            Assert.IsTrue(
                HasFrameSyncBuffEntry(
                    expected.Sample.Characters[0],
                    CharacterTestGuardAuraIds.BuffId),
                "Expected Guard Aura replay sample should include the field-level buff entry.");

            FrameSyncGuardAuraFrameRecord replayed =
                ReplayFrameSyncGuardAuraFromCheckpoint(checkpoint, expected.Frame);
            Assert.Greater(replayed.Sample.GuardAura.AppliedOrRefreshedCount, 0);
            Assert.Greater(replayed.Sample.Characters.Count, 0);
            Assert.Greater(replayed.Sample.Characters[0].BuffCount, 0);
            Assert.IsTrue(
                HasFrameSyncBuffEntry(
                    replayed.Sample.Characters[0],
                    CharacterTestGuardAuraIds.BuffId),
                "Replayed Guard Aura sample should include the field-level buff entry.");

            AssertAuthorityReplayMatches(expected.Sample, replayed.Sample);
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsAbilityCooldownMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                abilityCooldowns: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample(
                        casterEntityId: 1,
                        abilityId: CharacterTestAbilityIds.DashStrikeAbilityId,
                        endFrame: 120L)
                });
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                abilityCooldowns: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample(
                        casterEntityId: 1,
                        abilityId: CharacterTestAbilityIds.DashStrikeAbilityId,
                        endFrame: 132L)
                });

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "ability"
                    && row.EntityId == 1
                    && row.Field == "cooldown.endFrame"));
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysMovementAndActionRunnerExactly()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
            Assert.IsTrue(harness.Modules.ActionRuntime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                harness.CurrentFrame));

            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            var records = new List<RuntimeAiLabHeadlessFrameRecord>(24);
            for (int i = 0; i < 20; i++)
            {
                RuntimeFrame commandFrame = harness.CurrentFrame;
                RuntimeCommandValidationResult command = harness.Enqueue(
                    CharacterTestRuntimeCommandFactory.SetControlIntent(
                        commandFrame,
                        CharacterTestRuntimeCommandSources.PlayerInput,
                        new CharacterTestControlIntent(
                            entity.Id.Value,
                            Vector2.right,
                            Vector2.right,
                            jumpPressed: false,
                            jumpHeld: false,
                            jumpReleased: false,
                            attackPrimaryPressed: false),
                        new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0037)));
                Assert.IsTrue(command.Success, command.Error.Message);

                harness.Tick(1d / 60d);
                records.Add(CaptureHeadlessRecord(harness, participant, createCheckpoint: true));
            }

            RuntimeAiLabHeadlessFrameRecord checkpoint = records[9];
            RuntimeAiLabHeadlessFrameRecord expected = records[19];
            StringAssert.Contains("\"actionRunnerActionId\":\"light_attack\"", checkpoint.Checkpoint.PayloadJson);
            StringAssert.Contains("\"actionRunnerState\":\"Running\"", checkpoint.Checkpoint.PayloadJson);
            harness.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);
            CharacterTestRuntimeAiLabAuthoritySample restoredCheckpoint =
                CharacterTestRuntimeAiLabAuthorityProbe.Capture(harness.Slice, participant);
            CharacterTestRuntimeAiLabFrameSyncReport restoreReport =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(checkpoint.Authority, restoredCheckpoint);
            Assert.IsTrue(restoreReport.Passed, "Checkpoint restore mismatch: " + restoreReport.SummaryText);

            for (int i = 10; i < 20; i++)
            {
                RuntimeAiLabHeadlessFrameRecord record = records[i];
                for (int commandIndex = 0; commandIndex < record.Commands.Length; commandIndex++)
                {
                    RuntimeCommandValidationResult commandResult = harness.Enqueue(record.Commands[commandIndex]);
                    Assert.IsTrue(commandResult.Success, commandResult.Error.Message);
                }

                harness.Tick(1d / 60d);
            }

            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    harness.Slice,
                    participant,
                    selectedFrame: null,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("headless"));
            CharacterTestRuntimeAiLabFrameSyncReport authorityReport =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                    expected.Authority,
                    CharacterTestRuntimeAiLabAuthorityProbe.Capture(harness.Slice, participant));
            Assert.IsTrue(authorityReport.Passed, authorityReport.SummaryText);
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
        public void RuntimeAiLabHeadlessSeek_ReplaysRuntimeAiPlannerMovementStateExactly()
        {
            using var harness = new CharacterTestSliceTestHarness(CreateRuntimeAiPlannerFrameSyncSliceOptions());
            SpawnRuntimeAiPlannerFrameSyncAgent(
                harness,
                teamId: 1,
                x: -8f,
                z: 0f,
                traceId: new RuntimeTraceIdentity(918999));
            SpawnRuntimeAiPlannerFrameSyncAgent(
                harness,
                teamId: 2,
                x: 8f,
                z: 0f,
                traceId: new RuntimeTraceIdentity(996785));
            harness.Tick(1d / 60d);

            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            var records = new List<RuntimeAiLabHeadlessFrameRecord>(32);
            for (int i = 0; i < 28; i++)
            {
                harness.Tick(1d / 60d);
                records.Add(CaptureHeadlessPlannerRecord(harness, participant, createCheckpoint: true));
            }

            RuntimeAiLabHeadlessFrameRecord checkpoint = records[9];
            RuntimeAiLabHeadlessFrameRecord expected = records[19];
            AssertPlannerMovementSampleAvailable(checkpoint.Sample, "checkpoint");
            AssertPlannerMovementSampleAvailable(expected.Sample, "expected");

            harness.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);

            for (int i = 10; i <= 19; i++)
                harness.Tick(1d / 60d);

            RuntimeAiLabHeadlessFrameRecord replayed =
                CaptureHeadlessPlannerRecord(harness, participant, createCheckpoint: true);
            AssertPlannerMovementSampleAvailable(replayed.Sample, "replayed");
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed.Sample);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysCombatHitDamageExactly()
        {
            FrameSyncCombatReplayResult firstRun = RunFrameSyncDashStrikeCombatReplay(maxFrames: 60);
            Assert.GreaterOrEqual(firstRun.HitRecordIndex, 0, "Expected DashStrike to damage the target.");
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, firstRun.HitRecordIndex + 5);
            FrameSyncCombatFrameRecord expected = firstRun.Records[expectedIndex];
            CharacterTestRuntimeAiLabFrameSyncCharacterSample expectedTarget =
                expected.Sample.Characters.First(sample => sample.EntityId == firstRun.TargetEntityId);
            Assert.Less(expectedTarget.Health, expectedTarget.MaxHealth);
            Assert.GreaterOrEqual(expectedTarget.LastHitFrame, 0L);
            Assert.Greater(expectedTarget.LastDamage, 0);

            FrameSyncCombatReplayResult replayRun = RunFrameSyncDashStrikeCombatReplay(maxFrames: expectedIndex + 1);
            Assert.GreaterOrEqual(replayRun.HitRecordIndex, 0, "Expected replayed DashStrike to damage the target.");
            CharacterTestRuntimeAiLabFrameSyncSample replayed = replayRun.Records[expectedIndex].Sample;
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysChainLightningTargetsAndArcEventsExactly()
        {
            FrameSyncChainLightningReplayResult firstRun =
                RunFrameSyncChainLightningReplay(maxFrames: 80);
            Assert.GreaterOrEqual(
                firstRun.ChainRecordIndex,
                0,
                "Expected Chain Lightning to resolve chained targets.");
            FrameSyncChainLightningFrameRecord expected =
                firstRun.Records[firstRun.ChainRecordIndex];
            Assert.AreEqual(4, expected.Sample.ChainLightning.LastTargetCount);
            Assert.AreEqual(4, expected.Sample.ChainLightning.ArcEventCount);
            Assert.AreEqual(
                expected.Sample.ChainLightning.ArcEventCount,
                expected.ChainTargetEntityIds.Count,
                "Chain Lightning target ids should be captured from the same arc events as the frame sync sample.");
            AssertChainLightningTargetsWereDamaged(expected);

            FrameSyncChainLightningReplayResult replayRun =
                RunFrameSyncChainLightningReplay(maxFrames: expected.Frame + 1);
            Assert.GreaterOrEqual(
                replayRun.ChainRecordIndex,
                0,
                "Expected replayed Chain Lightning to resolve chained targets.");
            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                replayRun.Records[expected.Frame].Sample;
            AssertChainLightningArcReplay(expected.Sample.ChainLightning, replayed.ChainLightning);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
            FrameSyncChainLightningFrameRecord replayedRecord = replayRun.Records[expected.Frame];
            AssertChainLightningTargetIdsMatch(expected, replayedRecord);
            AssertChainLightningTargetCombatSamplesMatch(expected, replayedRecord);
        }

        private static void AssertChainLightningArcReplay(
            CharacterTestRuntimeAiLabFrameSyncChainLightningSample expected,
            CharacterTestRuntimeAiLabFrameSyncChainLightningSample actual)
        {
            Assert.AreEqual(expected.ArcEventCount, actual.ArcEventCount, "Chain Lightning arc event count must replay exactly.");
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample> expectedEvents =
                expected.ArcEvents;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample> actualEvents =
                actual.ArcEvents;
            int expectedCount = expectedEvents != null ? expectedEvents.Count : 0;
            int actualCount = actualEvents != null ? actualEvents.Count : 0;
            Assert.AreEqual(expectedCount, actualCount, "Chain Lightning arc event sample count must replay exactly.");
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample left = expectedEvents[i];
                CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample right = actualEvents[i];
                string prefix = "Chain Lightning arc event "
                    + i.ToString(CultureInfo.InvariantCulture)
                    + " must replay ";

                Assert.AreEqual(left.Frame, right.Frame, prefix + "frame.");
                Assert.AreEqual(left.SourceEntityId, right.SourceEntityId, prefix + "source entity.");
                Assert.AreEqual(left.TargetEntityId, right.TargetEntityId, prefix + "target entity.");
                Assert.AreEqual(left.SegmentIndex, right.SegmentIndex, prefix + "segment index.");
                Assert.AreEqual(left.ActionInstanceId, right.ActionInstanceId, prefix + "action instance.");
                Assert.AreEqual(left.LocalFrame, right.LocalFrame, prefix + "local frame.");
                Assert.AreEqual(left.OriginXRaw, right.OriginXRaw, prefix + "origin X.");
                Assert.AreEqual(left.OriginYRaw, right.OriginYRaw, prefix + "origin Y.");
                Assert.AreEqual(left.OriginZRaw, right.OriginZRaw, prefix + "origin Z.");
                Assert.AreEqual(left.DirectionXRaw, right.DirectionXRaw, prefix + "direction X.");
                Assert.AreEqual(left.DirectionYRaw, right.DirectionYRaw, prefix + "direction Y.");
                Assert.AreEqual(left.DirectionZRaw, right.DirectionZRaw, prefix + "direction Z.");
                Assert.AreEqual(left.EndpointXRaw, right.EndpointXRaw, prefix + "endpoint X.");
                Assert.AreEqual(left.EndpointYRaw, right.EndpointYRaw, prefix + "endpoint Y.");
                Assert.AreEqual(left.EndpointZRaw, right.EndpointZRaw, prefix + "endpoint Z.");
                Assert.AreEqual(left.LengthMillimeters, right.LengthMillimeters, prefix + "length.");
                Assert.AreEqual(left.Hit, right.Hit, prefix + "hit flag.");
            }
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysLaserPathVfxExactly()
        {
            FrameSyncLaserPathReplayResult firstRun = RunFrameSyncLaserPathReplay(maxFrames: 80);
            Assert.GreaterOrEqual(
                firstRun.LaserPathRecordIndex,
                0,
                "Expected Laser Lance to emit a laser path event.");
            int checkpointIndex = Math.Max(0, firstRun.LaserPathRecordIndex - 5);
            FrameSyncLaserPathFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncLaserPathFrameRecord expected = firstRun.Records[firstRun.LaserPathRecordIndex];
            Assert.Greater(
                expected.Sample.LaserPath.EventCount,
                0,
                "Expected target frame to contain laser path events.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                ReplayFrameSyncLaserPathFromCheckpoint(
                    checkpoint.CharacterSnapshot,
                    checkpoint.ProjectileState,
                    checkpoint.Frame,
                    expected.Frame);
            AssertLaserPathReplay(expected.Sample.LaserPath, replayed.LaserPath);
            AssertAuthorityReplayMatches(expected.Sample, replayed);
        }

        private static void AssertLaserPathReplay(
            CharacterTestRuntimeAiLabFrameSyncLaserPathSample expected,
            CharacterTestRuntimeAiLabFrameSyncLaserPathSample actual)
        {
            Assert.AreEqual(expected.EventCount, actual.EventCount, "Laser Path event count must replay exactly.");
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample> expectedEvents = expected.Events;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample> actualEvents = actual.Events;
            int expectedCount = expectedEvents != null ? expectedEvents.Count : 0;
            int actualCount = actualEvents != null ? actualEvents.Count : 0;
            Assert.AreEqual(expectedCount, actualCount, "Laser Path event sample count must replay exactly.");
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample left = expectedEvents[i];
                CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample right = actualEvents[i];
                string prefix = "Laser Path event "
                    + i.ToString(CultureInfo.InvariantCulture)
                    + " must replay ";

                Assert.AreEqual(left.Frame, right.Frame, prefix + "frame.");
                Assert.AreEqual(left.SourceEntityId, right.SourceEntityId, prefix + "source entity.");
                Assert.AreEqual(left.TargetEntityId, right.TargetEntityId, prefix + "target entity.");
                Assert.AreEqual(left.ProjectileId, right.ProjectileId, prefix + "projectile id.");
                Assert.AreEqual(left.ActionInstanceId, right.ActionInstanceId, prefix + "action instance.");
                Assert.AreEqual(left.LocalFrame, right.LocalFrame, prefix + "local frame.");
                Assert.AreEqual(left.OriginXRaw, right.OriginXRaw, prefix + "origin X.");
                Assert.AreEqual(left.OriginYRaw, right.OriginYRaw, prefix + "origin Y.");
                Assert.AreEqual(left.OriginZRaw, right.OriginZRaw, prefix + "origin Z.");
                Assert.AreEqual(left.DirectionXRaw, right.DirectionXRaw, prefix + "direction X.");
                Assert.AreEqual(left.DirectionYRaw, right.DirectionYRaw, prefix + "direction Y.");
                Assert.AreEqual(left.DirectionZRaw, right.DirectionZRaw, prefix + "direction Z.");
                Assert.AreEqual(left.EndpointXRaw, right.EndpointXRaw, prefix + "endpoint X.");
                Assert.AreEqual(left.EndpointYRaw, right.EndpointYRaw, prefix + "endpoint Y.");
                Assert.AreEqual(left.EndpointZRaw, right.EndpointZRaw, prefix + "endpoint Z.");
                Assert.AreEqual(left.LengthMillimeters, right.LengthMillimeters, prefix + "length.");
                Assert.AreEqual(left.Hit, right.Hit, prefix + "hit flag.");
            }
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileStateExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.FireballIntentId,
                CharacterTestFireballProjectileAdapter.StableId,
                maxFrames: 48);
            Assert.GreaterOrEqual(firstRun.FirstActiveRecordIndex, 0, "Expected Fireball to spawn an active projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(expected.Sample.Projectiles.Count, 0, "Expected projectile to remain active at target frame.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Replayed bezier projectile sample should remain active at target frame.");
            Assert.IsTrue(
                replayed.Projectiles[0].HasTargetPoint,
                "Replayed bezier projectile sample should preserve its target point.");
            Assert.AreEqual(
                expected.Sample.Projectiles[0].TargetPointXRaw,
                replayed.Projectiles[0].TargetPointXRaw,
                "Bezier projectile checkpoint replay must keep target point X.");
            Assert.AreEqual(
                expected.Sample.Projectiles[0].TargetPointYRaw,
                replayed.Projectiles[0].TargetPointYRaw,
                "Bezier projectile checkpoint replay must keep target point Y.");
            Assert.AreEqual(
                expected.Sample.Projectiles[0].TargetPointZRaw,
                replayed.Projectiles[0].TargetPointZRaw,
                "Bezier projectile checkpoint replay must keep target point Z.");
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileSwayVariantExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.FireballSwayIntentId,
                CharacterTestFireballProjectileAdapter.FireballSwayStableId,
                maxFrames: 48);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Fireball Sway to spawn an active projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(expected.Sample.Projectiles.Count, 0, "Expected sway projectile to remain active at target frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FireballSwayStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Fireball Sway checkpoint should still have the active projectile authority state.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.AreEqual(
                checkpointProjectile.SourceEntityId,
                expectedProjectile.SourceEntityId,
                "Fireball Sway must keep its source entity identity while sampling the sway path.");
            Assert.AreEqual(
                checkpointProjectile.OriginXRaw,
                expectedProjectile.OriginXRaw,
                "Fireball Sway must keep its X origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.OriginYRaw,
                expectedProjectile.OriginYRaw,
                "Fireball Sway must keep its Y origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.OriginZRaw,
                expectedProjectile.OriginZRaw,
                "Fireball Sway must keep its Z origin anchor instead of recomputing it during replay.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Expected replayed sway projectile to remain active at target frame.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.AreEqual(
                expectedProjectile.LocalFrame,
                replayedProjectile.LocalFrame,
                "Fireball Sway replay must restore the same local frame because it drives sway phase.");
            Assert.AreEqual(
                expectedProjectile.OriginXRaw,
                replayedProjectile.OriginXRaw,
                "Fireball Sway replay must preserve origin X.");
            Assert.AreEqual(
                expectedProjectile.OriginYRaw,
                replayedProjectile.OriginYRaw,
                "Fireball Sway replay must preserve origin Y.");
            Assert.AreEqual(
                expectedProjectile.OriginZRaw,
                replayedProjectile.OriginZRaw,
                "Fireball Sway replay must preserve origin Z.");
            Assert.AreEqual(
                expectedProjectile.PositionXRaw,
                replayedProjectile.PositionXRaw,
                "Fireball Sway replay must preserve sampled position X.");
            Assert.AreEqual(
                expectedProjectile.PositionYRaw,
                replayedProjectile.PositionYRaw,
                "Fireball Sway replay must preserve sampled position Y.");
            Assert.AreEqual(
                expectedProjectile.PositionZRaw,
                replayedProjectile.PositionZRaw,
                "Fireball Sway replay must preserve sampled position Z.");
            Assert.AreEqual(
                expectedProjectile.DirectionXRaw,
                replayedProjectile.DirectionXRaw,
                "Fireball Sway replay must preserve sampled direction X.");
            Assert.AreEqual(
                expectedProjectile.DirectionYRaw,
                replayedProjectile.DirectionYRaw,
                "Fireball Sway replay must preserve sampled direction Y.");
            Assert.AreEqual(
                expectedProjectile.DirectionZRaw,
                replayedProjectile.DirectionZRaw,
                "Fireball Sway replay must preserve sampled direction Z.");
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileArcAreaVariantExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.FireballLobIntentId,
                CharacterTestFireballProjectileAdapter.FireballLobStableId,
                maxFrames: 48);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Fireball Lob to spawn an active projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(expected.Sample.Projectiles.Count, 0, "Expected lob projectile to remain active at target frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FireballLobStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.IsTrue(
                expected.Sample.Projectiles[0].HasTargetPoint,
                "Arc projectile sync sample should capture its target point.");
            Assert.Greater(
                expected.Sample.Projectiles[0].EffectiveLifetimeFrames,
                0,
                "Arc projectile sync sample should preserve its effective lifetime.");
            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Arc projectile checkpoint should still have the active projectile authority state.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.IsTrue(
                checkpointProjectile.HasTargetPoint,
                "Arc projectile checkpoint must preserve its spawn target point.");
            Assert.AreEqual(
                checkpointProjectile.SourceEntityId,
                expectedProjectile.SourceEntityId,
                "Arc projectile must keep the source entity identity while sampling the arc.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointXRaw,
                expectedProjectile.TargetPointXRaw,
                "Arc projectile target point X should remain fixed after spawn.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointYRaw,
                expectedProjectile.TargetPointYRaw,
                "Arc projectile target point Y should remain fixed after spawn.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointZRaw,
                expectedProjectile.TargetPointZRaw,
                "Arc projectile target point Z should remain fixed after spawn.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Expected replayed arc projectile to remain active at target frame.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.AreEqual(expectedProjectile.DefinitionStableId, replayedProjectile.DefinitionStableId);
            Assert.IsTrue(replayedProjectile.HasTargetPoint);
            Assert.AreEqual(expectedProjectile.TargetPointXRaw, replayedProjectile.TargetPointXRaw);
            Assert.AreEqual(expectedProjectile.TargetPointYRaw, replayedProjectile.TargetPointYRaw);
            Assert.AreEqual(expectedProjectile.TargetPointZRaw, replayedProjectile.TargetPointZRaw);
            Assert.AreEqual(expectedProjectile.EffectiveLifetimeFrames, replayedProjectile.EffectiveLifetimeFrames);
            Assert.AreEqual(expectedProjectile.LaunchSpeedPerFrameRaw, replayedProjectile.LaunchSpeedPerFrameRaw);
            Assert.AreEqual(expectedProjectile.LocalFrame, replayedProjectile.LocalFrame);
            Assert.AreEqual(expectedProjectile.PositionXRaw, replayedProjectile.PositionXRaw);
            Assert.AreEqual(expectedProjectile.PositionYRaw, replayedProjectile.PositionYRaw);
            Assert.AreEqual(expectedProjectile.PositionZRaw, replayedProjectile.PositionZRaw);
            Assert.AreEqual(expectedProjectile.DirectionXRaw, replayedProjectile.DirectionXRaw);
            Assert.AreEqual(expectedProjectile.DirectionYRaw, replayedProjectile.DirectionYRaw);
            Assert.AreEqual(expectedProjectile.DirectionZRaw, replayedProjectile.DirectionZRaw);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysStunBoltOnHitBuffExactly()
        {
            FrameSyncStunBoltReplayResult firstRun = RunFrameSyncStunBoltReplay(maxFrames: 96);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Stun Bolt should spawn an active projectile before replay validation.");
            Assert.GreaterOrEqual(
                firstRun.CheckpointRecordIndex,
                0,
                "Stun Bolt replay validation needs an active projectile checkpoint before impact.");
            Assert.GreaterOrEqual(
                firstRun.FirstStunnedRecordIndex,
                0,
                "Stun Bolt should apply the Stunned buff/status after projectile impact.");

            FrameSyncStunBoltFrameRecord checkpoint = firstRun.Records[firstRun.CheckpointRecordIndex];
            FrameSyncStunBoltFrameRecord expected = firstRun.Records[firstRun.FirstStunnedRecordIndex];
            AssertStunBoltTargetStunned(expected, firstRun.TargetEntityId);

            FrameSyncStunBoltFrameRecord replayed =
                ReplayFrameSyncStunBoltFromCheckpoint(checkpoint, expected.Frame);
            AssertStunBoltTargetStunned(replayed, firstRun.TargetEntityId);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed.Sample);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileBoomerangReturnVariantExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.FireballReturnIntentId,
                CharacterTestFireballProjectileAdapter.FireballReturnStableId,
                maxFrames: 48);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Fireball Return to spawn an active projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(
                expected.Sample.Projectiles.Count,
                0,
                "Expected return projectile to remain active at target frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FireballReturnStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Return projectile checkpoint should still have the same active projectile authority state.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.AreEqual(
                checkpointProjectile.SourceEntityId,
                expectedProjectile.SourceEntityId,
                "Boomerang return must keep its source entity identity across the return path.");
            Assert.AreEqual(
                checkpointProjectile.OriginXRaw,
                expectedProjectile.OriginXRaw,
                "Boomerang return must keep its X origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.OriginYRaw,
                expectedProjectile.OriginYRaw,
                "Boomerang return must keep its Y origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.OriginZRaw,
                expectedProjectile.OriginZRaw,
                "Boomerang return must keep its Z origin anchor instead of recomputing it during replay.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Expected replayed return projectile to remain active at target frame.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.AreEqual(expectedProjectile.SourceEntityId, replayedProjectile.SourceEntityId);
            Assert.AreEqual(expectedProjectile.OriginXRaw, replayedProjectile.OriginXRaw);
            Assert.AreEqual(expectedProjectile.OriginYRaw, replayedProjectile.OriginYRaw);
            Assert.AreEqual(expectedProjectile.OriginZRaw, replayedProjectile.OriginZRaw);
            Assert.AreEqual(expectedProjectile.EffectiveLifetimeFrames, replayedProjectile.EffectiveLifetimeFrames);
            Assert.AreEqual(expectedProjectile.LocalFrame, replayedProjectile.LocalFrame);
            Assert.AreEqual(expectedProjectile.PositionXRaw, replayedProjectile.PositionXRaw);
            Assert.AreEqual(expectedProjectile.PositionYRaw, replayedProjectile.PositionYRaw);
            Assert.AreEqual(expectedProjectile.PositionZRaw, replayedProjectile.PositionZRaw);
            Assert.AreEqual(expectedProjectile.DirectionXRaw, replayedProjectile.DirectionXRaw);
            Assert.AreEqual(expectedProjectile.DirectionYRaw, replayedProjectile.DirectionYRaw);
            Assert.AreEqual(expectedProjectile.DirectionZRaw, replayedProjectile.DirectionZRaw);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }
    }
}
