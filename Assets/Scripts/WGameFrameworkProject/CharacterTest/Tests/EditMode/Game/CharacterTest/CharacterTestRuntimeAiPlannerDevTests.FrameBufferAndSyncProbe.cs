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
        private const string RuntimeAiLabScenePath = "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity";

        [Test]
        public void RuntimeAiLabFrameBuffer_CapturesRingAndSelection()
        {
            var buffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(capacity: 2);
            var runtimeEvents = new CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample(
                pendingCount: 1,
                oldestFrame: 2,
                newestFrame: 2,
                nextSequence: 1L,
                eventTypeName: "CharacterTestRuntimeEvent",
                recentCount: 1,
                recentHash: 22L,
                lastFrame: 2,
                lastType: (int)CharacterTestRuntimeEventType.CameraModeChanged,
                lastCommandId: CharacterTestRuntimeCommandIds.SetCameraMode,
                lastSourceId: CharacterTestRuntimeCommandSources.DebugUi,
                lastTargetId: 0,
                lastEntityId: 0,
                lastPayload0: (int)CharacterTestCameraMode.MultiTargetAuto,
                lastPayload1: 0,
                lastPayload2: 0,
                lastMessage: string.Empty);

            buffer.Capture(1, 0.1f, CreateSnapshot("profile-a"));
            buffer.Capture(
                2,
                0.2f,
                CreateSnapshot("profile-b"),
                runtimeEvents: runtimeEvents);
            buffer.Capture(3, 0.3f, CreateSnapshot("profile-c"));

            Assert.AreEqual(2, buffer.Capacity);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1, buffer.SelectedIndex);
            Assert.AreEqual(2, buffer.GetAt(0).Frame);
            Assert.AreEqual(3, buffer.GetAt(1).Frame);
            Assert.AreEqual("profile-c", buffer.Latest.RuntimeAiPlannerSnapshot.ProfileId);

            Assert.IsTrue(buffer.SelectPrevious());
            Assert.IsTrue(buffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot selected));
            Assert.AreEqual(2, selected.Frame);
            Assert.AreEqual(22L, selected.RuntimeEvents.RecentHash);

            Assert.IsTrue(buffer.SelectNext());
            Assert.IsTrue(buffer.TryGetSelected(out selected));
            Assert.AreEqual(3, selected.Frame);
        }

        [Test]
        public void RuntimeAiLabFrameBuffer_RepeatedAuthorityFrameReplacesWithoutInflatingCount()
        {
            var buffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(capacity: 8);
            buffer.Capture(10, 0.1f, CreateSnapshot("before"));
            buffer.Capture(11, 0.2f, CreateSnapshot("first-pass"));

            buffer.Capture(11, 0.3f, CreateSnapshot("post-tick-refinement"));

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(11, buffer.Latest.Frame);
            Assert.AreEqual("post-tick-refinement", buffer.Latest.RuntimeAiPlannerSnapshot.ProfileId);
            Assert.AreEqual(1, buffer.SelectedIndex);
        }

        [Test]
        public void RuntimeAiLabFrameBuffer_ReviewReplacementPreservesLaterFrames()
        {
            var buffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(capacity: 8);
            buffer.Capture(10, 0.1f, CreateSnapshot("ten"));
            buffer.Capture(11, 0.2f, CreateSnapshot("eleven"));
            buffer.Capture(12, 0.3f, CreateSnapshot("twelve"));

            buffer.Capture(11, 0.4f, CreateSnapshot("eleven-refined"));

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual("eleven-refined", buffer.GetAt(1).RuntimeAiPlannerSnapshot.ProfileId);
            Assert.AreEqual(12, buffer.GetAt(2).Frame);
            Assert.AreEqual(1, buffer.SelectedIndex);
        }

        [Test]
        public void RuntimeAiLabTimeline_RecordsCheckpointsAndSelectsFrames()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(capacity: 5, checkpointInterval: 3);

            for (int frame = 1; frame <= 7; frame++)
            {
                CharacterTestAuthorityRollbackCheckpoint checkpoint = timeline.ShouldCreateCheckpoint(frame)
                    ? CreateCheckpoint(frame)
                    : null;
                timeline.AddRecord(
                    frame,
                    frame * 0.1f,
                    frame / 60d,
                    CreateSnapshot("profile-" + frame),
                    new[]
                    {
                        new RuntimeCommand(new RuntimeFrame(frame), sourceId: 1, commandId: 10, targetId: 2)
                    },
                    checkpoint,
                    frame * 100L);
            }

            Assert.AreEqual(5, timeline.Capacity);
            Assert.AreEqual(5, timeline.Count);
            Assert.AreEqual(7, timeline.LatestFrame);
            Assert.AreEqual(7, timeline.SelectedFrame);
            Assert.AreEqual(3, timeline.Records[0].Frame);
            Assert.IsTrue(timeline.SelectFrame(5));
            Assert.AreEqual(5, timeline.SelectedFrame);
            Assert.IsTrue(timeline.TryFindCheckpointAtOrBefore(7, out CharacterTestRuntimeAiPlannerDevTimelineRecord checkpointRecord));
            Assert.AreEqual(6, checkpointRecord.Frame);
            Assert.IsTrue(checkpointRecord.HasCheckpoint);
            Assert.AreEqual(1, checkpointRecord.DrainedCommands.Count);
        }

        [Test]
        public void RuntimeAiLabTimeline_LoopRangeRequiresContinuousAuthorityFrames()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(capacity: 8, checkpointInterval: 30);
            AddLoopTimelineRecord(timeline, frame: 10, hasGameplayCheckpoint: true);
            AddLoopTimelineRecord(timeline, frame: 11, hasGameplayCheckpoint: false);
            AddLoopTimelineRecord(timeline, frame: 12, hasGameplayCheckpoint: false);

            Assert.IsTrue(timeline.SelectFrame(10));
            Assert.IsTrue(timeline.SetLoopStartToSelection());
            Assert.IsTrue(timeline.SelectFrame(12));
            Assert.IsTrue(timeline.SetLoopEndToSelection());
            Assert.IsTrue(timeline.TryValidateLoopRange(out string validDiagnostic), validDiagnostic);
            Assert.IsTrue(timeline.ToggleLoop());

            var missingFrame = new CharacterTestRuntimeAiPlannerDevTimeline(capacity: 8, checkpointInterval: 30);
            AddLoopTimelineRecord(missingFrame, frame: 10, hasGameplayCheckpoint: true);
            AddLoopTimelineRecord(missingFrame, frame: 12, hasGameplayCheckpoint: false);
            Assert.IsTrue(missingFrame.SelectFrame(10));
            Assert.IsTrue(missingFrame.SetLoopStartToSelection());
            Assert.IsTrue(missingFrame.SelectFrame(12));
            Assert.IsTrue(missingFrame.SetLoopEndToSelection());
            Assert.IsFalse(missingFrame.TryValidateLoopRange(out string missingFrameDiagnostic));
            StringAssert.Contains("RuntimeFrame 11", missingFrameDiagnostic);
        }

        [Test]
        public void RuntimeAiLabTimeline_BranchFromReviewFrameTruncatesFuture()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(capacity: 8, checkpointInterval: 2);
            for (int frame = 1; frame <= 4; frame++)
            {
                timeline.AddRecord(
                    frame,
                    frame,
                    frame,
                    CreateSnapshot("before-" + frame),
                    Array.Empty<RuntimeCommand>(),
                    timeline.ShouldCreateCheckpoint(frame) ? CreateCheckpoint(frame) : null,
                    frame);
            }

            Assert.IsTrue(timeline.SelectFrame(2));
            timeline.MarkEditBranchStart();
            timeline.AddRecord(
                5,
                5f,
                5d,
                CreateSnapshot("after-branch"),
                Array.Empty<RuntimeCommand>(),
                CreateCheckpoint(5),
                500L);

            Assert.AreEqual(3, timeline.Count);
            Assert.AreEqual(1, timeline.Records[0].Frame);
            Assert.AreEqual(2, timeline.Records[1].Frame);
            Assert.AreEqual(5, timeline.Records[2].Frame);
            Assert.AreEqual(2, timeline.EditBranchStartFrame);
            Assert.IsFalse(timeline.TryGetFrame(3, out _));
            Assert.IsFalse(timeline.TryGetFrame(4, out _));
            Assert.AreEqual(5, timeline.SelectedFrame);
        }

        [Test]
        public void RuntimeAiLabTimeline_ReviewSeekDoesNotTruncateFuture()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(capacity: 8, checkpointInterval: 2);
            for (int frame = 1; frame <= 4; frame++)
            {
                timeline.AddRecord(
                    frame,
                    frame,
                    frame,
                    CreateSnapshot("before-" + frame),
                    Array.Empty<RuntimeCommand>(),
                    timeline.ShouldCreateCheckpoint(frame) ? CreateCheckpoint(frame) : null,
                    frame);
            }

            Assert.IsTrue(timeline.SelectFrame(2));
            timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
            timeline.AddRecord(
                5,
                5f,
                5d,
                CreateSnapshot("review-capture"),
                Array.Empty<RuntimeCommand>(),
                CreateCheckpoint(5),
                500L);

            Assert.AreEqual(5, timeline.Count);
            Assert.IsTrue(timeline.TryGetFrame(3, out _));
            Assert.IsTrue(timeline.TryGetFrame(4, out _));
            Assert.IsTrue(timeline.TryGetFrame(5, out _));
            Assert.AreEqual(5, timeline.SelectedFrame);
        }

        [Test]
        public void RuntimeAiLabFrameBuffer_ReviewCaptureDoesNotTruncateFuture()
        {
            var buffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(capacity: 8);
            for (int frame = 1; frame <= 4; frame++)
                buffer.Capture(frame, frame, CreateSnapshot("before-" + frame));

            Assert.IsTrue(buffer.SelectFrame(2));
            buffer.Capture(5, 5f, CreateSnapshot("review-capture"));

            Assert.AreEqual(5, buffer.Count);
            Assert.AreEqual(1, buffer.GetAt(0).Frame);
            Assert.AreEqual(2, buffer.GetAt(1).Frame);
            Assert.AreEqual(3, buffer.GetAt(2).Frame);
            Assert.AreEqual(4, buffer.GetAt(3).Frame);
            Assert.AreEqual(5, buffer.GetAt(4).Frame);
        }

        [Test]
        public void RuntimeAiLabFrameBuffer_EditBranchTruncatesFuture()
        {
            var buffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(capacity: 8);
            for (int frame = 1; frame <= 4; frame++)
                buffer.Capture(frame, frame, CreateSnapshot("before-" + frame));

            Assert.IsTrue(buffer.SelectFrame(2));
            buffer.Capture(5, 5f, CreateSnapshot("after-branch"), truncateFuture: true);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer.GetAt(0).Frame);
            Assert.AreEqual(2, buffer.GetAt(1).Frame);
            Assert.AreEqual(5, buffer.GetAt(2).Frame);
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsTransformRunnerOverlayMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                hasUnityView: true,
                unityViewXMillimeters: 1000,
                hasPhysicsBody: true,
                physicsBodyXRaw: 1_000_000L);
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1200,
                runnerLocalFrame: 9,
                overlayFrame: 21,
                overlaySourceX: 2f,
                hasUnityView: true,
                unityViewXMillimeters: 1400,
                hasPhysicsBody: true,
                physicsBodyXRaw: 1_400_000L);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "transform" && row.EntityId == 1 && row.Field == "x"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "view" && row.EntityId == 1 && row.Field == "x"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "physicsProxy" && row.EntityId == 1 && row.Field == "xRaw"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runner" && row.EntityId == 1 && row.Field == "localFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "overlay" && row.Field == "frame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "overlay" && row.Field == "sourceCenter.x"));
            StringAssert.Contains("Frame sync mismatch", report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsAnimationBoneAndRagdollMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 12,
                overlayFrame: 20,
                overlaySourceX: 1f,
                presentationState: CreateAnimationPresentationState(
                    clipTimeSeconds: 0.2d,
                    handRotation: Quaternion.Euler(0f, 10f, 0f),
                    ragdollPosition: new Vector3(1f, 2f, 3f)));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 12,
                overlayFrame: 20,
                overlaySourceX: 1f,
                presentationState: CreateAnimationPresentationState(
                    clipTimeSeconds: 0.4d,
                    handRotation: Quaternion.Euler(0f, 25f, 0f),
                    ragdollPosition: new Vector3(2f, 2f, 3f)));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "animation" && row.Field.EndsWith("clipTime", StringComparison.Ordinal)));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "bonePose" && row.Field == "RightHand.rotation"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "procedural" && row.Field == "ragdoll.body[0].position"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsCombatMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                health: 100,
                lastDamage: 0,
                lastHitFrame: -1L,
                specialHitState: "None",
                buffCount: 1,
                buffHash: 101,
                buffEntries: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncBuffEntrySample(
                        buffId: 7001,
                        stackCount: 1,
                        startFrame: 12L,
                        endFrame: 72L,
                        isPermanent: false,
                        sourceId: 2)
                },
                statusCount: 1,
                statusHash: 201,
                statusEntries: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncStatusEntrySample(
                        statusId: 8001,
                        sourceBuffId: 7001)
                });
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                health: 87,
                lastDamage: 13,
                lastHitFrame: 24L,
                specialHitState: "Launched",
                buffCount: 1,
                buffHash: 102,
                buffEntries: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncBuffEntrySample(
                        buffId: 7001,
                        stackCount: 2,
                        startFrame: 12L,
                        endFrame: 90L,
                        isPermanent: false,
                        sourceId: 3)
                },
                statusCount: 1,
                statusHash: 202,
                statusEntries: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncStatusEntrySample(
                        statusId: 8001,
                        sourceBuffId: 7002)
                });

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "combat" && row.EntityId == 1 && row.Field == "health"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "combat" && row.EntityId == 1 && row.Field == "lastDamage"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "combat" && row.EntityId == 1 && row.Field == "lastHitFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "combat" && row.EntityId == 1 && row.Field == "specialHitState"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "buff" && row.EntityId == 1 && row.Field == "hash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "buff" && row.EntityId == 1 && row.Field == "entry.7001.stackCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "buff" && row.EntityId == 1 && row.Field == "entry.7001.endFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "buff" && row.EntityId == 1 && row.Field == "entry.7001.sourceId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "status" && row.EntityId == 1 && row.Field == "hash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "status" && row.EntityId == 1 && row.Field == "entry.8001.sourceBuffId"));
        }

        [Test]
        public void NetworkCharacterSnapshot_CapturesHashesAndNormalizesBattleState()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
            CharacterTestBattleStateComponent alive = CharacterTestBattleStateComponent.Alive;
            var battle = new CharacterTestBattleStateComponent(
                CharacterTestCharacterLifeState.Alive,
                CharacterTestBattleStateDominantReason.ControlLock,
                alive.GateFlags,
                sinceFrame: 42L,
                lastTransitionFrame: 41L,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("battle-state-test"),
                sourceTraceId: 17);
            harness.Modules.CharacterRuntime.World.SetBattleState(entity.Id, battle);

            CharacterTestNetworkCharacterSnapshot snapshot =
                CharacterTestNetworkCharacterSnapshot.FromEntity(
                    entity,
                    harness.Modules.ActionRuntime);
            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            long firstHash = participant.ComputeStateHash(harness.CurrentFrame);

            Assert.IsTrue(snapshot.HasExactBattleState);
            Assert.AreEqual((int)battle.LifeState, snapshot.BattleLifeState);
            Assert.AreEqual((int)battle.DominantReason, snapshot.BattleDominantReason);
            Assert.AreEqual((int)battle.GateFlags, snapshot.BattleGateFlags);
            Assert.AreEqual(42L, snapshot.BattleSinceFrame);
            Assert.AreEqual(41L, snapshot.BattleLastTransitionFrame);
            Assert.AreEqual("battle-state-test", snapshot.BattleSourceActionId);
            Assert.AreEqual(17, snapshot.BattleSourceTraceId);

            harness.Modules.CharacterRuntime.World.SetBattleState(
                entity.Id,
                new CharacterTestBattleStateComponent(
                    battle.LifeState,
                    battle.DominantReason,
                    battle.GateFlags,
                    sinceFrame: 43L,
                    lastTransitionFrame: battle.LastTransitionFrame,
                    sourceActionId: battle.SourceActionId,
                    sourceTraceId: battle.SourceTraceId));
            long secondHash = participant.ComputeStateHash(harness.CurrentFrame);
            Assert.AreNotEqual(firstHash, secondHash, "BattleState temporal fields must contribute to state hash.");

            snapshot.NormalizeForSchema(CharacterTestNetworkSnapshotParticipant.VfxRuntimeSchemaVersionValue);
            CharacterTestBattleStateComponent legacy = snapshot.CreateBattleStateComponent();
            Assert.IsFalse(snapshot.HasExactBattleState);
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, legacy.LifeState);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.None, legacy.DominantReason);
            Assert.AreEqual(0L, legacy.SinceFrame);
            Assert.AreEqual(0L, legacy.LastTransitionFrame);
        }

        [Test]
        public void NetworkStateHash_IgnoresDerivedContactObservabilityButKeepsForwardVelocity()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            RuntimeFrame frame = harness.CurrentFrame;
            CharacterTestCharacterPhysicsComponent physics = entity.Physics;
            long baselineHash = participant.ComputeStateHash(frame);

            entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                physics.CapsuleRadiusMillimeters,
                physics.CapsuleHeightMillimeters,
                physics.CapsuleCenterYMillimeters,
                physics.MassGrams,
                physics.MassScaleMilli,
                physics.RadiusScaleMilli,
                physics.PushScaleMilli,
                physics.BlocksCharacters,
                physics.CanBePushed,
                contactVelocityXMillimetersPerSecond: 0,
                contactVelocityYMillimetersPerSecond: 0,
                contactDisplacementXMillimeters: 90,
                contactDisplacementYMillimeters: -45,
                contactCount: 2));
            Assert.AreEqual(
                baselineHash,
                participant.ComputeStateHash(frame),
                "Derived per-frame contact observations must not invalidate replay authority.");

            entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                physics.CapsuleRadiusMillimeters,
                physics.CapsuleHeightMillimeters,
                physics.CapsuleCenterYMillimeters,
                physics.MassGrams,
                physics.MassScaleMilli,
                physics.RadiusScaleMilli,
                physics.PushScaleMilli,
                physics.BlocksCharacters,
                physics.CanBePushed,
                contactVelocityXMillimetersPerSecond: 1));
            Assert.AreNotEqual(
                baselineHash,
                participant.ComputeStateHash(frame),
                "Contact velocity affects the next motion step and remains authoritative.");
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsAttributeMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                attributeCount: 3,
                attributeHash: 301,
                attributeEntries: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                        CharacterTestCharacterAttributeIds.Hp,
                        CharacterTestCharacterAttributeIds.DefaultMaxHp,
                        100),
                    new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                        CharacterTestCharacterAttributeIds.Attack,
                        CharacterTestCharacterAttributeIds.DefaultAttack,
                        20),
                    new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                        CharacterTestCharacterAttributeIds.Defense,
                        CharacterTestCharacterAttributeIds.DefaultDefense,
                        10)
                });
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                attributeCount: 3,
                attributeHash: 302,
                attributeEntries: new[]
                {
                    new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                        CharacterTestCharacterAttributeIds.Hp,
                        CharacterTestCharacterAttributeIds.DefaultMaxHp,
                        87),
                    new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                        CharacterTestCharacterAttributeIds.Attack,
                        25,
                        20),
                    new CharacterTestRuntimeAiLabFrameSyncAttributeEntrySample(
                        CharacterTestCharacterAttributeIds.ScatterPressure,
                        CharacterTestCharacterAttributeIds.DefaultScatterPressure,
                        80)
                });

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "attribute" && row.EntityId == 1 && row.Field == "hash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "attribute"
                    && row.EntityId == 1
                    && row.Field == "entry." + CharacterTestCharacterAttributeIds.Hp.ToString(CultureInfo.InvariantCulture) + ".currentValue"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "attribute"
                    && row.EntityId == 1
                    && row.Field == "entry." + CharacterTestCharacterAttributeIds.Attack.ToString(CultureInfo.InvariantCulture) + ".baseValue"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "attribute"
                    && row.EntityId == 1
                    && row.Field == "entry." + CharacterTestCharacterAttributeIds.Defense.ToString(CultureInfo.InvariantCulture) + ".missing"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsGameplayScatterPressureMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                gameplayScatterPresent: true,
                gameplayScatterCurrentPressure: 34,
                gameplayScatterMaxPressure: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                gameplayScatterMaxDebt: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                gameplayScatterCurrentCohesionLimit: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                gameplayScatterRecoveryRate: 2,
                gameplayScatterRecoveryDelayFrames: 12,
                gameplayScatterBrokenRecoveryDelayFrames: 0,
                gameplayScatterCurrentBand: "Stable",
                gameplayScatterLastPressureFrame: 18L,
                gameplayScatterLastCohesionBreakFrame: -1L,
                gameplayScatterIsBroken: false);
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                gameplayScatterPresent: true,
                gameplayScatterCurrentPressure: 88,
                gameplayScatterMaxPressure: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                gameplayScatterMaxDebt: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax + 20,
                gameplayScatterCurrentCohesionLimit: CharacterTestCharacterAttributeIds.DefaultScatterPressureMax - 10,
                gameplayScatterRecoveryRate: 4,
                gameplayScatterRecoveryDelayFrames: 16,
                gameplayScatterBrokenRecoveryDelayFrames: 30,
                gameplayScatterCurrentBand: "Critical",
                gameplayScatterLastPressureFrame: 21L,
                gameplayScatterLastCohesionBreakFrame: 20L,
                gameplayScatterIsBroken: false);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplayScatter" && row.EntityId == 1 && row.Field == "currentPressure"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplayScatter" && row.EntityId == 1 && row.Field == "maxDebt"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplayScatter" && row.EntityId == 1 && row.Field == "currentCohesionLimit"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplayScatter" && row.EntityId == 1 && row.Field == "currentBand"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplayScatter" && row.EntityId == 1 && row.Field == "lastPressureFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplayScatter" && row.EntityId == 1 && row.Field == "lastCohesionBreakFrame"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsGameplayIdentityMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                gameplayLifecyclePresent: true,
                gameplayLifecycleState: "Alive",
                gameplayTeamPresent: true,
                gameplayTeamId: 1);
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                gameplayLifecyclePresent: true,
                gameplayLifecycleState: "Destroyed",
                gameplayTeamPresent: true,
                gameplayTeamId: 2);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplay" && row.EntityId == 1 && row.Field == "lifecycleState"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "gameplay" && row.EntityId == 1 && row.Field == "teamId"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsMovableNaturalObjectMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                movableNaturalObjects: new[]
                {
                    CreateMovableNaturalObjectSyncSample(
                        numericId: 41,
                        xMillimeters: 1200,
                        contactPressure: 50,
                        currentPotencyMotion: 10,
                        latestPotencyFrame: 22L,
                        latestPotencyResultKind: (int)CharacterTestGenesisPotencyResultKind.Burning)
                });
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                movableNaturalObjects: new[]
                {
                    CreateMovableNaturalObjectSyncSample(
                        numericId: 41,
                        xMillimeters: 1800,
                        contactPressure: 75,
                        currentPotencyMotion: 24,
                        latestPotencyFrame: 23L,
                        latestPotencyResultKind: (int)CharacterTestGenesisPotencyResultKind.CrushedOrBroken)
                });

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movableObject" && row.EntityId == 41 && row.Field == "x"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movableObject" && row.EntityId == 41 && row.Field == "contactPressure"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movableObject" && row.EntityId == 41 && row.Field == "currentPotencyMotion"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movableObject" && row.EntityId == 41 && row.Field == "latestPotencyFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movableObject" && row.EntityId == 41 && row.Field == "latestPotencyResultKind"));
        }
    }
}
