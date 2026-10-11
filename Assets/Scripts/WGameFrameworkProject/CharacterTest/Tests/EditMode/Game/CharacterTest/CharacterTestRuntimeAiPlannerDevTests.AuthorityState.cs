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
        public void RuntimeAiPlannerAuthorityState_DoesNotHashLastCommandDiagnostic()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot left = CreatePlannerAuthorityState("SetControlIntent");
            CharacterTestRuntimeAiPlannerStateSnapshot right = CreatePlannerAuthorityState("PrimaryAttack");
            var leftHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            var rightHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);

            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(leftHash, left);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(rightHash, right);

            Assert.AreEqual(leftHash.ToHash(), rightHash.ToHash());
        }

        [Test]
        public void RuntimeAiPlannerAuthorityState_ReactiveThreatStateClonesAndAffectsHash()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot baseline = CreatePlannerAuthorityState("SetControlIntent");
            CharacterTestRuntimeAiPlannerStateSnapshot reactive = baseline.CloneForRollback();
            CharacterTestRuntimeAiPlannerAgentStateSnapshot agent = reactive.Slots[0].Agents[0];
            agent.ObservedTargetAttackEntityId = 9;
            agent.ObservedTargetAttackActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
            agent.ObservedTargetAttackLocalFrame = 12;
            agent.TargetAttackSequence = 3;
            agent.LastRespondedTargetAttackSequence = 2;
            agent.NextDodgeAllowedFrame = 90;
            var baselineHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            var reactiveHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);

            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(baselineHash, baseline);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(reactiveHash, reactive);

            Assert.AreNotEqual(baselineHash.ToHash(), reactiveHash.ToHash());
            CharacterTestRuntimeAiPlannerStateSnapshot restored = reactive.CloneForRollback();
            Assert.AreEqual(agent.ObservedTargetAttackActionId, restored.Slots[0].Agents[0].ObservedTargetAttackActionId);
            Assert.AreEqual(agent.TargetAttackSequence, restored.Slots[0].Agents[0].TargetAttackSequence);
            Assert.AreEqual(agent.NextDodgeAllowedFrame, restored.Slots[0].Agents[0].NextDodgeAllowedFrame);
        }

        [Test]
        public void RuntimeAiPlannerAuthorityState_UsesIntegerSchemaBoundariesForLocksAndReservations()
        {
            Assert.AreEqual(RuntimeHashSchemaIds.RuntimeAiPlannerState, RuntimeAiPlannerStateHashFields.SchemaId);
            Assert.AreEqual(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, BehaviorExecutionStateHashFields.SchemaId);

            CharacterTestRuntimeAiPlannerStateSnapshot baseline = CreatePlannerAuthorityState("SetControlIntent");
            baseline.DamageZoneLocks = new[]
            {
                new CharacterTestDamageZoneLockStateSnapshot
                {
                    SourceEntityId = 7,
                    TargetGameplayEntityIndex = 2,
                    TargetGameplayEntityGeneration = 1,
                    TargetCombatEntityId = 9,
                    ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0092,
                    HitZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0028,
                    LogicalHurtboxId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0053
                }
            };
            baseline.EngagementReservations = new[]
            {
                new CharacterTestEngagementReservationStateSnapshot
                {
                    SourceEntityId = 7,
                    TargetEntityId = 9,
                    TargetCombatEntityId = 9,
                    ResolvedActionRevision = 1,
                    ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0092,
                    AnchorId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0007,
                    Capacity = 1,
                    CapacitySlot = 0,
                    AcquiredFrame = 1,
                    UpdatedFrame = 1,
                    ZoneBindingRevision = 1,
                    ZoneRevision = 1,
                    AnchorRevision = 1,
                    AnchorContentHash = RuntimePreparedDigest.PrepareExisting("anchor-a"),
                    PhaseId = CharacterTestAuthoredContentIds.D400_Id33,
                    PhaseRevision = 1,
                    CombatPhysicsRevision = 1,
                    GraphRevision = 1,
                    GraphContentHash = 2
                }
            };
            CharacterTestRuntimeAiPlannerStateSnapshot changed = baseline.CloneForRollback();
            changed.EngagementReservations[0].AnchorContentHash = RuntimePreparedDigest.PrepareExisting("anchor-b");
            var baselineHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            var changedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(baselineHash, baseline);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(changedHash, changed);

            Assert.Greater(baselineHash.FieldCount, 0);
            Assert.AreNotEqual(baselineHash.ToHash(), changedHash.ToHash());
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_SuppressesDuplicateAudioPresentation()
        {
            using var harness = new CharacterTestSliceTestHarness(CreateFrameSyncAudioSliceOptions());
            CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();

            harness.Tick(1d / 60d);
            RuntimeAiLabHeadlessFrameRecord checkpoint =
                CaptureHeadlessRecord(
                    harness,
                    participant,
                    createCheckpoint: true,
                    includeRuntimeEvents: true);

            RuntimeFrame actionFrame = harness.CurrentFrame;
            entity.EnqueueAudioCueRequest(CreateFrameSyncAudioCueRequest(
                actionFrame,
                entity.Id.Value));
            harness.Tick(1d / 60d);
            RuntimeAiLabHeadlessFrameRecord expected =
                CaptureHeadlessRecord(harness, participant, createCheckpoint: true);

            Assert.IsTrue(expected.Sample.Audio.ServiceAvailable);
            Assert.AreEqual(1, expected.Sample.Audio.TotalPlayRequests);
            Assert.AreEqual(1, expected.Sample.Audio.IntentDrain.PlayedCount);
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackCueId, expected.Sample.Audio.IntentDrain.LastCueId);

            harness.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);

            CharacterTestCharacterEntity replayEntity = harness.FindEntityById(entity.Id.Value);
            Assert.NotNull(replayEntity);
            Assert.AreEqual(actionFrame.Value, harness.CurrentFrame.Value);
            replayEntity.EnqueueAudioCueRequest(CreateFrameSyncAudioCueRequest(
                harness.CurrentFrame,
                replayEntity.Id.Value));
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
            Assert.AreEqual(0, replayedDrain.PlayedCount);
            Assert.AreEqual(expectedDrain.FailedCount, replayedDrain.FailedCount);
            Assert.AreEqual(expectedDrain.DroppedCount, replayedDrain.DroppedCount);
            Assert.AreEqual(expectedDrain.SkippedCount + 1, replayedDrain.SkippedCount);
            Assert.AreEqual(expectedDrain.LastCueId, replayedDrain.LastCueId);
            Assert.AreEqual(expectedDrain.LastEventId, replayedDrain.LastEventId);
            Assert.AreEqual(expectedDrain.LastEmitterId, replayedDrain.LastEmitterId);
            Assert.AreEqual(expected.Sample.Audio.TotalPlayRequests, replayed.Audio.TotalPlayRequests);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysRuntimeCommandStateExactly()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestCharacterEntity localPlayer = harness.SpawnLocalPlayer();
            RuntimeCommandValidationResult enemySpawn = harness.Enqueue(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    harness.CurrentFrame,
                    CharacterTestRuntimeCommandSources.System,
                    CharacterTestCharacterSpawnSpec.TemporaryAiOpponent,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0031)));
            Assert.IsTrue(enemySpawn.Success, enemySpawn.Error.Message);
            harness.Tick(1d / 60d);

            CharacterTestCharacterEntity enemy = null;
            for (int i = 0; i < harness.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = harness.CharacterEntities[i];
                if (entity.Team.TeamId != localPlayer.Team.TeamId)
                {
                    enemy = entity;
                    break;
                }
            }

            Assert.NotNull(enemy, "Command-state replay needs an opposing target for target lock.");
            harness.Modules.RuntimeCommands.SetRuntimeAiBehaviorSelectionUnlocked(true);

            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            var records = new List<RuntimeAiLabHeadlessFrameRecord>(4);
            for (int i = 0; i < 4; i++)
            {
                RuntimeFrame commandFrame = harness.CurrentFrame;
                RuntimeCommandValidationResult command;
                switch (i)
                {
                    case 0:
                        command = harness.Enqueue(CharacterTestRuntimeCommandFactory.SetCameraMode(
                            commandFrame,
                            CharacterTestRuntimeCommandSources.DebugUi,
                            CharacterTestCameraMode.MultiTargetAuto,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0032)));
                        break;
                    case 1:
                        command = harness.Enqueue(CharacterTestRuntimeCommandFactory.SetTargetLock(
                            commandFrame,
                            CharacterTestRuntimeCommandSources.DebugUi,
                            enabled: true,
                            enemy.Id.Value,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0033)));
                        break;
                    case 2:
                        command = harness.Enqueue(CharacterTestRuntimeCommandFactory.SetCameraMode(
                            commandFrame,
                            CharacterTestRuntimeCommandSources.DebugUi,
                            CharacterTestCameraMode.ThirdPersonControlled,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0034)));
                        break;
                    default:
                        command = harness.Enqueue(CharacterTestRuntimeCommandFactory.SetTargetLock(
                            commandFrame,
                            CharacterTestRuntimeCommandSources.DebugUi,
                            enabled: false,
                            enemy.Id.Value,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0035)));
                        break;
                }

                Assert.IsTrue(command.Success, command.Error.Message);
                harness.Tick(1d / 60d);
                records.Add(CaptureHeadlessRecord(harness, participant, createCheckpoint: true));
            }

            RuntimeAiLabHeadlessFrameRecord checkpoint = records[1];
            RuntimeAiLabHeadlessFrameRecord expected = records[3];
            Assert.AreEqual((int)CharacterTestCameraMode.MultiTargetAuto, checkpoint.Sample.CommandState.CameraMode);
            Assert.IsTrue(checkpoint.Sample.CommandState.TargetLockEnabled);
            Assert.AreEqual(enemy.Id.Value, checkpoint.Sample.CommandState.LockedTargetEntityId);
            Assert.IsTrue(checkpoint.Sample.CommandState.RuntimeAiBehaviorSelectionUnlocked);
            StringAssert.DoesNotContain("\"cameraMode\"", checkpoint.Checkpoint.PayloadJson);
            StringAssert.Contains("\"targetLockEnabled\"", checkpoint.Checkpoint.PayloadJson);
            StringAssert.Contains("\"runtimeAiBehaviorSelectionUnlocked\"", checkpoint.Checkpoint.PayloadJson);

            harness.Modules.RuntimeCommands.SetRuntimeAiBehaviorSelectionUnlocked(false);
            harness.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);

            for (int i = 2; i < records.Count; i++)
            {
                RuntimeAiLabHeadlessFrameRecord record = records[i];
                for (int commandIndex = 0; commandIndex < record.Commands.Length; commandIndex++)
                {
                    if (!CharacterTestRuntimeAiPlannerDevCommandReplay.ShouldReplayCommand(record.Commands[commandIndex]))
                        continue;

                    RuntimeCommandValidationResult commandResult =
                        harness.Enqueue(record.Commands[commandIndex]);
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
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysRuntimeEventsExactly()
        {
            using var harness = new CharacterTestSliceTestHarness();
            harness.SpawnLocalPlayer();
            harness.Modules.RuntimeCommands.ClearWarmupSubmissionState();

            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            RuntimeAiLabHeadlessFrameRecord checkpoint =
                CaptureHeadlessRecord(harness, participant, createCheckpoint: true);
            var records = new List<RuntimeAiLabHeadlessFrameRecord>(2);
            CharacterTestCameraMode[] cameraModes =
            {
                CharacterTestCameraMode.MultiTargetAuto,
                CharacterTestCameraMode.ThirdPersonControlled
            };

            for (int i = 0; i < cameraModes.Length; i++)
            {
                RuntimeCommandValidationResult command = harness.Enqueue(
                    CharacterTestRuntimeCommandFactory.SetCameraMode(
                        harness.CurrentFrame,
                        CharacterTestRuntimeCommandSources.DebugUi,
                        cameraModes[i],
                        new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0036, sequence: i)));
                Assert.IsTrue(command.Success, command.Error.Message);
                harness.Tick(1d / 60d);
                records.Add(CaptureHeadlessRecord(
                    harness,
                    participant,
                    createCheckpoint: false,
                    includeRuntimeEvents: true));
            }

            RuntimeAiLabHeadlessFrameRecord expected = records[records.Count - 1];
            Assert.Greater(expected.Sample.RuntimeEvents.PendingCount, 0);
            Assert.Greater(expected.Sample.RuntimeEvents.RecentCount, 0);
            Assert.AreEqual(
                (int)CharacterTestRuntimeEventType.CameraModeChanged,
                expected.Sample.RuntimeEvents.LastType);

            harness.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);
            Assert.AreEqual(0, harness.Modules.RuntimeCommands.RecentEvents.Count);
            Assert.AreEqual(0, harness.Modules.RuntimeCommands.EventQueueSnapshot.PendingCount);

            for (int i = 0; i < records.Count; i++)
            {
                RuntimeAiLabHeadlessFrameRecord record = records[i];
                for (int commandIndex = 0; commandIndex < record.Commands.Length; commandIndex++)
                {
                    RuntimeCommand command = record.Commands[commandIndex];
                    if (!CharacterTestRuntimeAiPlannerDevCommandReplay.ShouldReplayCommand(command))
                        continue;

                    RuntimeCommandValidationResult replay = harness.Enqueue(command);
                    Assert.IsTrue(replay.Success, replay.Error.Message);
                }

                harness.Tick(1d / 60d);
            }

            CharacterTestRuntimeAiLabFrameSyncSample actual =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    harness.Slice,
                    participant,
                    selectedFrame: null,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("headless"),
                    includeRuntimeEvents: true);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, actual);

            Assert.IsTrue(report.Passed, report.SummaryText);
            Assert.AreEqual(
                expected.Sample.RuntimeEvents.RecentHash,
                actual.RuntimeEvents.RecentHash);
            Assert.AreEqual(
                expected.Sample.RuntimeEvents.NextSequence,
                actual.RuntimeEvents.NextSequence);
        }

        [Test]
        public void RuntimeAiLabHeadlessSeek_ReplaysMovableNaturalObjectPotencyExactly()
        {
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.natural.frame-sync.potency",
                    numericId: 10,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    massKg: 20f,
                    CombatSurfaceMaterial.HardSlick,
                    new Vector3(0.5f, 0.5f, 0.5f),
                    new Vector3(0f, 0.5f, 0f),
                    CharacterTestMovableNaturalObjectShapeKind.Aabb,
                    new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                    CharacterTestGenesisPotencyProfile.WithDefaultThresholds(
                        immuneMotion: false,
                        immuneCohesion: false,
                        immuneScatter: false,
                        burningEnabled: true,
                        crushEnabled: true,
                        crackEnabled: false,
                        condensedLayerEnabled: true))
            };

            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                movableNaturalObjectSpawnSpecs: specs);
            var participant = new CharacterTestNetworkSnapshotParticipant(
                slice.CharacterRuntime,
                slice.RuntimeCommands);
            var records = new List<RuntimeAiLabHeadlessFrameRecord>(4);
            CharacterTestGenesisPotencyInfluenceKind[] influences =
            {
                CharacterTestGenesisPotencyInfluenceKind.Fire,
                CharacterTestGenesisPotencyInfluenceKind.Hammer,
                CharacterTestGenesisPotencyInfluenceKind.Condense
            };

            for (int i = 0; i < influences.Length; i++)
            {
                int targetNumericId = influences[i] == CharacterTestGenesisPotencyInfluenceKind.Condense
                    ? 0
                    : 10;
                RuntimeCommandValidationResult command = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluence(
                        slice.CurrentFrame,
                        CharacterTestRuntimeCommandSources.DebugUi,
                        targetNumericId,
                        influences[i],
                        traceId: new RuntimeTraceIdentity(992147)));
                Assert.IsTrue(command.Success, command.Error.Message);

                slice.Tick(1d / 60d);
                records.Add(CaptureHeadlessRecord(slice, participant, createCheckpoint: true));
            }

            RuntimeAiLabHeadlessFrameRecord checkpoint = records[0];
            RuntimeAiLabHeadlessFrameRecord expected = records[2];
            Assert.Greater(expected.Sample.MovableNaturalObjects.Count, 0);
            StringAssert.Contains("\"movableNaturalObjects\"", checkpoint.Checkpoint.PayloadJson);

            slice.Services.Network.RestoreNetworkRollbackRuntimeState(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.ElapsedSeconds);
            NetworkSnapshotApplyResult apply = participant.ApplyRollbackSnapshot(
                new RuntimeFrame(checkpoint.Frame),
                checkpoint.Checkpoint,
                preserveLocalPredictedEntity: false);
            Assert.IsTrue(apply.Success, apply.Message);
            CharacterTestRuntimeAiLabAuthoritySample restoredCheckpoint =
                CharacterTestRuntimeAiLabAuthorityProbe.Capture(slice, participant);
            CharacterTestRuntimeAiLabFrameSyncReport restoreReport =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(checkpoint.Authority, restoredCheckpoint);
            Assert.IsTrue(restoreReport.Passed, "Checkpoint restore mismatch: " + restoreReport.SummaryText);

            for (int i = 1; i < records.Count; i++)
            {
                RuntimeAiLabHeadlessFrameRecord record = records[i];
                for (int commandIndex = 0; commandIndex < record.Commands.Length; commandIndex++)
                {
                    RuntimeCommandValidationResult commandResult =
                        slice.EnqueueRuntimeCommand(record.Commands[commandIndex]);
                    Assert.IsTrue(commandResult.Success, commandResult.Error.Message);
                }

                slice.Tick(1d / 60d);
            }

            CharacterTestRuntimeAiLabFrameSyncSample replayed =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    slice,
                    participant,
                    selectedFrame: null,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("headless"));
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected.Sample, replayed);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }
    }
}
