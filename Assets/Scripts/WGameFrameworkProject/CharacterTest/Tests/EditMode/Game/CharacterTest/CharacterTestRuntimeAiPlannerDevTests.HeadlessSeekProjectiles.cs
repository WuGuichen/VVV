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
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileHomingVariantExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.FireballHomingIntentId,
                CharacterTestFireballProjectileAdapter.FireballHomingStableId,
                maxFrames: 48,
                useLockedTarget: true,
                expectTrackingTarget: true);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Fireball Homing to spawn an active projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(
                expected.Sample.Projectiles.Count,
                0,
                "Expected homing projectile to remain active at target frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FireballHomingStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Homing projectile checkpoint should still have the active projectile authority state.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.IsTrue(
                checkpointProjectile.HasTrackingTarget,
                "Homing projectile checkpoint must preserve tracking target state.");
            Assert.AreEqual(
                checkpointProjectile.SourceEntityId,
                expectedProjectile.SourceEntityId,
                "Homing projectile must keep the source entity identity while steering.");
            Assert.AreEqual(
                checkpointProjectile.TrackingTargetEntityId,
                expectedProjectile.TrackingTargetEntityId,
                "Homing projectile must keep the same tracking target identity while steering.");
            Assert.IsTrue(
                expectedProjectile.HasTrackingTarget,
                "Homing projectile sync sample should preserve tracking target state.");
            Assert.Greater(
                expectedProjectile.TrackingTargetEntityId,
                0,
                "Homing projectile sync sample should preserve a concrete tracking target entity id.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame,
                useLockedTarget: true);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Replayed homing projectile sample should remain active at target frame.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.IsTrue(
                replayedProjectile.HasTrackingTarget,
                "Replayed homing projectile sample should preserve tracking target state.");
            Assert.AreEqual(expectedProjectile.DefinitionStableId, replayedProjectile.DefinitionStableId);
            Assert.AreEqual(expectedProjectile.SourceEntityId, replayedProjectile.SourceEntityId);
            Assert.AreEqual(
                expectedProjectile.TrackingTargetEntityId,
                replayedProjectile.TrackingTargetEntityId,
                "Homing projectile checkpoint replay must keep the same tracking target entity id.");
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

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileBezierVariantExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.FireballBezierIntentId,
                CharacterTestFireballProjectileAdapter.FireballBezierStableId,
                maxFrames: 48);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Fireball Bezier to spawn an active projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(
                expected.Sample.Projectiles.Count,
                0,
                "Expected bezier projectile to remain active at target frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FireballBezierStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Bezier projectile checkpoint should still have the active projectile authority state.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.IsTrue(
                checkpointProjectile.HasTargetPoint,
                "Bezier projectile checkpoint must preserve its target point.");
            Assert.AreEqual(
                checkpointProjectile.SourceEntityId,
                expectedProjectile.SourceEntityId,
                "Bezier projectile must keep the source entity identity while sampling the curve.");
            Assert.AreEqual(
                checkpointProjectile.OriginXRaw,
                expectedProjectile.OriginXRaw,
                "Bezier projectile must keep its X origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.OriginYRaw,
                expectedProjectile.OriginYRaw,
                "Bezier projectile must keep its Y origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.OriginZRaw,
                expectedProjectile.OriginZRaw,
                "Bezier projectile must keep its Z origin anchor instead of recomputing it during replay.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointXRaw,
                expectedProjectile.TargetPointXRaw,
                "Bezier projectile target point X should remain fixed after spawn.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointYRaw,
                expectedProjectile.TargetPointYRaw,
                "Bezier projectile target point Y should remain fixed after spawn.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointZRaw,
                expectedProjectile.TargetPointZRaw,
                "Bezier projectile target point Z should remain fixed after spawn.");
            Assert.IsTrue(
                expectedProjectile.HasTargetPoint,
                "Bezier projectile sync sample should preserve its target point.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Expected replayed Bezier projectile to remain active at target frame.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.AreEqual(expectedProjectile.DefinitionStableId, replayedProjectile.DefinitionStableId);
            Assert.AreEqual(expectedProjectile.SourceEntityId, replayedProjectile.SourceEntityId);
            Assert.AreEqual(expectedProjectile.OriginXRaw, replayedProjectile.OriginXRaw);
            Assert.AreEqual(expectedProjectile.OriginYRaw, replayedProjectile.OriginYRaw);
            Assert.AreEqual(expectedProjectile.OriginZRaw, replayedProjectile.OriginZRaw);
            Assert.IsTrue(replayedProjectile.HasTargetPoint);
            Assert.AreEqual(expectedProjectile.TargetPointXRaw, replayedProjectile.TargetPointXRaw);
            Assert.AreEqual(expectedProjectile.TargetPointYRaw, replayedProjectile.TargetPointYRaw);
            Assert.AreEqual(expectedProjectile.TargetPointZRaw, replayedProjectile.TargetPointZRaw);
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

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileBallisticArcVariantExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.PoisonBottleIntentId,
                CharacterTestFireballProjectileAdapter.PoisonBottleStableId,
                maxFrames: 72,
                useLockedTarget: true);
            Assert.GreaterOrEqual(
                firstRun.FirstActiveRecordIndex,
                0,
                "Expected Poison Bottle to spawn an active ballistic projectile.");
            int checkpointIndex = Math.Min(firstRun.Records.Count - 2, firstRun.FirstActiveRecordIndex + 3);
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 10);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(
                expected.Sample.Projectiles.Count,
                0,
                "Expected ballistic projectile to remain active at target frame.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.PoisonBottleStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.IsTrue(
                expected.Sample.Projectiles[0].HasTargetPoint,
                "Ballistic projectile sync sample should preserve its solved target point.");
            Assert.Greater(
                expected.Sample.Projectiles[0].EffectiveLifetimeFrames,
                0,
                "Ballistic projectile sync sample should preserve its effective lifetime.");
            Assert.Greater(
                expected.Sample.Projectiles[0].LaunchSpeedPerFrameRaw,
                0L,
                "Ballistic projectile sync sample should preserve its solved launch speed.");

            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Expected ballistic checkpoint to contain an active projectile.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.IsTrue(
                checkpointProjectile.HasTargetPoint,
                "Ballistic checkpoint should preserve the solved target point.");
            Assert.AreEqual(
                checkpointProjectile.SourceEntityId,
                expectedProjectile.SourceEntityId,
                "Ballistic projectile source entity should remain stable between checkpoint and target frame.");
            Assert.AreEqual(
                checkpointProjectile.OriginXRaw,
                expectedProjectile.OriginXRaw,
                "Ballistic projectile origin X should remain anchored after spawn.");
            Assert.AreEqual(
                checkpointProjectile.OriginYRaw,
                expectedProjectile.OriginYRaw,
                "Ballistic projectile origin Y should remain anchored after spawn.");
            Assert.AreEqual(
                checkpointProjectile.OriginZRaw,
                expectedProjectile.OriginZRaw,
                "Ballistic projectile origin Z should remain anchored after spawn.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointXRaw,
                expectedProjectile.TargetPointXRaw,
                "Ballistic projectile target X should not be recomputed from the live target.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointYRaw,
                expectedProjectile.TargetPointYRaw,
                "Ballistic projectile target Y should not be recomputed from the live target.");
            Assert.AreEqual(
                checkpointProjectile.TargetPointZRaw,
                expectedProjectile.TargetPointZRaw,
                "Ballistic projectile target Z should not be recomputed from the live target.");
            Assert.AreEqual(
                checkpointProjectile.EffectiveLifetimeFrames,
                expectedProjectile.EffectiveLifetimeFrames,
                "Ballistic projectile lifetime should remain fixed after spawn.");
            Assert.AreEqual(
                checkpointProjectile.LaunchSpeedPerFrameRaw,
                expectedProjectile.LaunchSpeedPerFrameRaw,
                "Ballistic projectile solved launch speed should remain fixed after spawn.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame,
                useLockedTarget: true);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Expected replayed ballistic projectile to remain active at target frame.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.AreEqual(expectedProjectile.DefinitionStableId, replayedProjectile.DefinitionStableId);
            Assert.AreEqual(expectedProjectile.SourceEntityId, replayedProjectile.SourceEntityId);
            Assert.AreEqual(expectedProjectile.OriginXRaw, replayedProjectile.OriginXRaw);
            Assert.AreEqual(expectedProjectile.OriginYRaw, replayedProjectile.OriginYRaw);
            Assert.AreEqual(expectedProjectile.OriginZRaw, replayedProjectile.OriginZRaw);
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
        public void RuntimeAiLabHeadlessSeek_ReplaysProjectileAcceptedImpactHistoryExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileAcceptedImpactReplay(maxFrames: 40);
            Assert.GreaterOrEqual(
                firstRun.FirstAcceptedImpactRecordIndex,
                0,
                "Expected Flame Lance to keep accepted impact history while the projectile remains active.");
            int checkpointIndex = firstRun.FirstAcceptedImpactRecordIndex;
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, checkpointIndex + 5);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(
                expected.Sample.Projectiles.Count,
                0,
                "Expected Flame Lance projectile to remain active after accepted impacts.");
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FlameLanceStableId,
                expected.Sample.Projectiles[0].DefinitionStableId);
            Assert.AreEqual(
                2,
                expected.Sample.Projectiles[0].AcceptedImpactCount,
                "Expected Flame Lance accepted impact history to include both pierced targets.");

            Assert.Greater(
                checkpoint.Sample.Projectiles.Count,
                0,
                "Expected Flame Lance checkpoint to retain the active projectile after accepted impacts.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample checkpointProjectile = checkpoint.Sample.Projectiles[0];
            CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile = expected.Sample.Projectiles[0];
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.FlameLanceStableId,
                checkpointProjectile.DefinitionStableId);
            Assert.AreEqual(
                expectedProjectile.SourceEntityId,
                checkpointProjectile.SourceEntityId,
                "Flame Lance source entity should remain stable after accepted impacts.");
            Assert.AreEqual(
                2,
                checkpointProjectile.AcceptedImpactCount,
                "Checkpoint must already include both accepted impacts.");
            Assert.AreEqual(
                checkpointProjectile.AcceptedImpactCount,
                expectedProjectile.AcceptedImpactCount,
                "Accepted impact count should remain stable while the piercing projectile stays active.");
            Assert.AreEqual(
                checkpointProjectile.AcceptedImpactHash,
                expectedProjectile.AcceptedImpactHash,
                "Accepted impact target/frame history should not be recomputed or dropped after checkpoint.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileAcceptedImpactFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            Assert.Greater(
                replayed.Projectiles.Count,
                0,
                "Expected replayed Flame Lance projectile to remain active after accepted impacts.");
            CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile = replayed.Projectiles[0];
            Assert.AreEqual(expectedProjectile.DefinitionStableId, replayedProjectile.DefinitionStableId);
            Assert.AreEqual(expectedProjectile.SourceEntityId, replayedProjectile.SourceEntityId);
            Assert.AreEqual(expectedProjectile.AcceptedImpactCount, replayedProjectile.AcceptedImpactCount);
            Assert.AreEqual(expectedProjectile.AcceptedImpactHash, replayedProjectile.AcceptedImpactHash);
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
        public void RuntimeAiLabHeadlessSeek_ReplaysMeteorBarrageMultiProjectileStateExactly()
        {
            FrameSyncProjectileReplayResult firstRun = RunFrameSyncProjectileReplay(
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                CharacterTestFireballProjectileAdapter.MeteorBarrageStableId,
                maxFrames: 72);
            int multiProjectileIndex = FindFirstProjectileRecordWithActiveCount(
                firstRun.Records,
                minimumActiveCount: 2);
            Assert.GreaterOrEqual(
                multiProjectileIndex,
                0,
                "Expected Meteor Barrage to have multiple active meteors in flight.");
            int expectedIndex = Math.Min(firstRun.Records.Count - 1, multiProjectileIndex + 5);
            FrameSyncProjectileFrameRecord checkpoint = firstRun.Records[multiProjectileIndex];
            FrameSyncProjectileFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.GreaterOrEqual(
                expected.Sample.Projectiles.Count,
                2,
                "Expected multiple Meteor Barrage projectiles to remain active at the target frame.");
            for (int i = 0; i < expected.Sample.Projectiles.Count; i++)
            {
                Assert.AreEqual(
                    CharacterTestFireballProjectileAdapter.MeteorBarrageStableId,
                    expected.Sample.Projectiles[i].DefinitionStableId);
                Assert.IsTrue(
                    expected.Sample.Projectiles[i].HasTargetPoint,
                    "Meteor Barrage projectile sync sample should preserve its landing target point.");
            }

            CharacterTestRuntimeAiLabFrameSyncSample replayed = ReplayFrameSyncProjectileFromCheckpoint(
                checkpoint.ProjectileState,
                checkpoint.Frame,
                expected.Frame);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysInteractionExplosionFieldExactly()
        {
            FrameSyncMeteorBarrageRunnerReplayResult firstRun =
                RunFrameSyncMeteorBarrageRunnerReplay(maxFrames: 90);
            int checkpointIndex = FindMeteorBarrageRunnerContinuationCheckpoint(
                firstRun.Records,
                out _);
            Assert.GreaterOrEqual(
                checkpointIndex,
                0,
                "Expected a Meteor Barrage checkpoint with active runner state before validating interaction replay.");
            int expectedIndex = FindFirstInteractionRecordIndex(firstRun.Records, checkpointIndex + 1);
            Assert.GreaterOrEqual(
                expectedIndex,
                checkpointIndex + 1,
                "Expected Meteor Barrage to record an explosion interaction after a runner-restorable checkpoint.");
            FrameSyncMeteorBarrageRunnerFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncMeteorBarrageRunnerFrameRecord expected = firstRun.Records[expectedIndex];
            Assert.Greater(
                expected.Sample.Interaction.LastFrameEventCount,
                0,
                "Expected target frame to contain last-frame interaction events.");
            Assert.AreNotEqual(
                0L,
                expected.Sample.Interaction.LastFrameEventHash,
                "Expected target frame interaction events to contribute a stable hash.");

            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                ReplayFrameSyncMeteorBarrageRunnerFromCheckpoint(
                    checkpoint.CharacterSnapshot,
                    checkpoint.ProjectileState,
                    checkpoint.Frame,
                    expected.Frame);
            AssertInteractionReplay(expected.Sample.Interaction, replayed.Interaction);
            AssertAuthorityReplayMatches(expected.Sample, replayed);
        }

        private static void AssertInteractionReplay(
            CharacterTestRuntimeAiLabFrameSyncInteractionSample expected,
            CharacterTestRuntimeAiLabFrameSyncInteractionSample actual)
        {
            Assert.AreEqual(expected.LastFrameEventCount, actual.LastFrameEventCount, "Interaction event count must replay exactly.");
            Assert.AreEqual(expected.LastFrameEventHash, actual.LastFrameEventHash, "Interaction event hash must replay exactly.");
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncInteractionEventSample> expectedEvents =
                expected.LastFrameEvents;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncInteractionEventSample> actualEvents =
                actual.LastFrameEvents;
            int expectedCount = expectedEvents != null ? expectedEvents.Count : 0;
            int actualCount = actualEvents != null ? actualEvents.Count : 0;
            Assert.AreEqual(expectedCount, actualCount, "Interaction event sample count must replay exactly.");
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncInteractionEventSample left = expectedEvents[i];
                CharacterTestRuntimeAiLabFrameSyncInteractionEventSample right = actualEvents[i];
                string prefix = "Interaction event "
                    + i.ToString(CultureInfo.InvariantCulture)
                    + " must replay ";

                Assert.AreEqual(left.Frame, right.Frame, prefix + "frame.");
                Assert.AreEqual(left.Kind, right.Kind, prefix + "kind.");
                Assert.AreEqual(left.StableId, right.StableId, prefix + "stable id.");
                Assert.AreEqual(left.SourceOrder, right.SourceOrder, prefix + "source order.");
                Assert.AreEqual(left.SourceEntityId, right.SourceEntityId, prefix + "source entity.");
                Assert.AreEqual(left.TargetEntityId, right.TargetEntityId, prefix + "target entity.");
                Assert.AreEqual(left.AnchorXRaw, right.AnchorXRaw, prefix + "anchor X.");
                Assert.AreEqual(left.AnchorYRaw, right.AnchorYRaw, prefix + "anchor Y.");
                Assert.AreEqual(left.AnchorZRaw, right.AnchorZRaw, prefix + "anchor Z.");
                Assert.AreEqual(left.DirectionXRaw, right.DirectionXRaw, prefix + "direction X.");
                Assert.AreEqual(left.DirectionYRaw, right.DirectionYRaw, prefix + "direction Y.");
                Assert.AreEqual(left.DirectionZRaw, right.DirectionZRaw, prefix + "direction Z.");
                Assert.AreEqual(left.RadiusRaw, right.RadiusRaw, prefix + "radius.");
                Assert.AreEqual(left.Intensity, right.Intensity, prefix + "intensity.");
                Assert.AreEqual(left.TraceId, right.TraceId, prefix + "trace.");
                Assert.AreEqual(left.SourceDomain, right.SourceDomain, prefix + "source domain.");
                Assert.AreEqual(left.FieldShape, right.FieldShape, prefix + "field shape.");
                Assert.AreEqual(left.FieldForceMode, right.FieldForceMode, prefix + "field force mode.");
                Assert.AreEqual(left.FieldTargetFilter, right.FieldTargetFilter, prefix + "field target filter.");
                Assert.AreEqual(left.FieldCapsuleBottomOffsetRaw, right.FieldCapsuleBottomOffsetRaw, prefix + "capsule bottom offset.");
                Assert.AreEqual(left.FieldCapsuleTopOffsetRaw, right.FieldCapsuleTopOffsetRaw, prefix + "capsule top offset.");
                Assert.AreEqual(left.FieldImpulseSpeedMillimetersPerSecond, right.FieldImpulseSpeedMillimetersPerSecond, prefix + "impulse speed.");
                Assert.AreEqual(left.FieldAccelerationMillimetersPerSecondSquared, right.FieldAccelerationMillimetersPerSecondSquared, prefix + "acceleration.");
                Assert.AreEqual(left.FieldLinearFalloffMinimumScaleMilli, right.FieldLinearFalloffMinimumScaleMilli, prefix + "falloff.");
                Assert.AreEqual(left.FieldMaxSpeedMillimetersPerSecond, right.FieldMaxSpeedMillimetersPerSecond, prefix + "max speed.");
                Assert.AreEqual(left.FieldReferenceMassGrams, right.FieldReferenceMassGrams, prefix + "reference mass.");
                Assert.AreEqual(left.FieldMinimumMassScaleMilli, right.FieldMinimumMassScaleMilli, prefix + "min mass scale.");
                Assert.AreEqual(left.FieldMaximumMassScaleMilli, right.FieldMaximumMassScaleMilli, prefix + "max mass scale.");
                Assert.AreEqual(left.FieldUseMassScale, right.FieldUseMassScale, prefix + "mass scale flag.");
                Assert.AreEqual(left.FieldUsePushScale, right.FieldUsePushScale, prefix + "push scale flag.");
                Assert.AreEqual(left.FieldUseMaterialPushScale, right.FieldUseMaterialPushScale, prefix + "material push flag.");
            }
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysMeteorBarrageRunnerContinuedLaunchesExactly()
        {
            FrameSyncMeteorBarrageRunnerReplayResult firstRun =
                RunFrameSyncMeteorBarrageRunnerReplay(maxFrames: 72);
            int checkpointIndex = FindMeteorBarrageRunnerContinuationCheckpoint(
                firstRun.Records,
                out int expectedIndex);
            Assert.GreaterOrEqual(
                checkpointIndex,
                0,
                "Expected a Meteor Barrage checkpoint after an early launch and before later runner-driven launches.");

            FrameSyncMeteorBarrageRunnerFrameRecord checkpoint = firstRun.Records[checkpointIndex];
            FrameSyncMeteorBarrageRunnerFrameRecord expected = firstRun.Records[expectedIndex];
            int checkpointActiveCount = checkpoint.ProjectileState.ActiveCount;
            int expectedActiveCount = expected.ProjectileState.ActiveCount;
            Assert.Greater(
                expectedActiveCount,
                checkpointActiveCount,
                "The target frame must prove that restored action runner state continues launching later meteors.");
            Assert.AreEqual(
                checkpointActiveCount,
                checkpoint.Sample.Projectiles.Count,
                "Checkpoint sample should expose the active meteors that already exist before runner replay.");
            Assert.Greater(
                expected.Sample.ProjectileNextProjectileId,
                checkpoint.Sample.ProjectileNextProjectileId,
                "Meteor Barrage continuation must advance the projectile allocator when later meteors launch.");
            Assert.IsTrue(
                checkpoint.Sample.Characters[0].HasRunner,
                "Checkpoint must include active action runner state.");
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageActionId,
                checkpoint.Sample.Characters[0].RunnerActionId);
            Assert.IsTrue(
                expected.Sample.Characters[0].HasRunner,
                "Target frame must still expose the Meteor Barrage runner state.");
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageActionId,
                expected.Sample.Characters[0].RunnerActionId);
            Assert.Greater(
                expected.Sample.Characters[0].RunnerLocalFrame,
                checkpoint.Sample.Characters[0].RunnerLocalFrame,
                "Target frame must prove the restored runner advanced past the checkpoint.");
            Assert.AreEqual(
                expectedActiveCount,
                expected.Sample.Projectiles.Count,
                "Target sample should expose all active meteors.");
            for (int i = 0; i < expected.Sample.Projectiles.Count; i++)
            {
                Assert.AreEqual(
                    CharacterTestFireballProjectileAdapter.MeteorBarrageStableId,
                    expected.Sample.Projectiles[i].DefinitionStableId);
                Assert.IsTrue(
                    expected.Sample.Projectiles[i].HasTargetPoint,
                    "Meteor Barrage runner continuation must preserve each meteor landing point.");
            }

            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                ReplayFrameSyncMeteorBarrageRunnerFromCheckpoint(
                    checkpoint.CharacterSnapshot,
                    checkpoint.ProjectileState,
                    checkpoint.Frame,
                    expected.Frame);
            Assert.AreEqual(
                expected.Sample.ProjectileNextProjectileId,
                replayed.ProjectileNextProjectileId,
                "Replay should restore and advance the meteor projectile allocator to the same value.");
            Assert.AreEqual(
                expectedActiveCount,
                replayed.Projectiles.Count,
                "Replay should create the same number of active meteors after restoring the action runner.");
            for (int i = 0; i < expected.Sample.Projectiles.Count; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncProjectileSample expectedProjectile =
                    expected.Sample.Projectiles[i];
                Assert.IsTrue(
                    TryFindProjectileSample(
                        replayed.Projectiles,
                        expectedProjectile.ProjectileId,
                        out CharacterTestRuntimeAiLabFrameSyncProjectileSample replayedProjectile),
                    "Replay should preserve meteor projectile id "
                    + expectedProjectile.ProjectileId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                AssertProjectileSampleMatches(
                    "Meteor projectile "
                    + expectedProjectile.ProjectileId.ToString(CultureInfo.InvariantCulture),
                    expectedProjectile,
                    replayedProjectile);
            }

            AssertAuthorityReplayMatches(expected.Sample, replayed);
        }
    }
}
