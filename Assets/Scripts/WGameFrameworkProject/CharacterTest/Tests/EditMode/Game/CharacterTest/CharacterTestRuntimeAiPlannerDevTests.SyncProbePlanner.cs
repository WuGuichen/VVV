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
        public void RuntimeAiLabFrameSyncProbe_ReportsCommandStateMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                commandState: new CharacterTestRuntimeAiLabFrameSyncCommandStateSample(
                    cameraMode: (int)CharacterTestCameraMode.MultiTargetAuto,
                    targetLockEnabled: true,
                    lockedTargetEntityId: 2,
                    runtimeAiBehaviorSelectionUnlocked: false,
                    pendingCommandCount: 0,
                    lastDrainedCommandCount: 1,
                    lastDrainedCommandHash: 111L,
                    lastDrainedCommandSignature: "debug command",
                    lastCommandErrorCount: 0));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                commandState: new CharacterTestRuntimeAiLabFrameSyncCommandStateSample(
                    cameraMode: (int)CharacterTestCameraMode.ThirdPersonControlled,
                    targetLockEnabled: false,
                    lockedTargetEntityId: 0,
                    runtimeAiBehaviorSelectionUnlocked: true,
                    pendingCommandCount: 1,
                    lastDrainedCommandCount: 2,
                    lastDrainedCommandHash: 222L,
                    lastDrainedCommandSignature: "duplicate command",
                    lastCommandErrorCount: 1));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "cameraMode"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "targetLockEnabled"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "lockedTargetEntityId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "runtimeAiBehaviorSelectionUnlocked"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "pendingCommandCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "lastDrainedCommandCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "lastDrainedCommandHash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "lastDrainedCommands"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "lastCommandErrorCount"));
        }

        [Test]
        public void RuntimeAiLabPresentationDiagnostics_ReportsTransientCommandState()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                commandState: new CharacterTestRuntimeAiLabFrameSyncCommandStateSample(
                    cameraMode: (int)CharacterTestCameraMode.MultiTargetAuto,
                    targetLockEnabled: true,
                    lockedTargetEntityId: 2,
                    runtimeAiBehaviorSelectionUnlocked: false,
                    pendingCommandCount: 0,
                    lastDrainedCommandCount: 1,
                    lastDrainedCommandHash: 111L,
                    lastDrainedCommandSignature: "frame command",
                    lastCommandErrorCount: 0));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                commandState: new CharacterTestRuntimeAiLabFrameSyncCommandStateSample(
                    cameraMode: (int)CharacterTestCameraMode.ThirdPersonControlled,
                    targetLockEnabled: true,
                    lockedTargetEntityId: 2,
                    runtimeAiBehaviorSelectionUnlocked: false,
                    pendingCommandCount: 0,
                    lastDrainedCommandCount: 0,
                    lastDrainedCommandHash: 0L,
                    lastDrainedCommandSignature: string.Empty,
                    lastCommandErrorCount: 1));

            CharacterTestRuntimeAiLabFrameSyncReport fullFrame =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);
            Assert.IsFalse(fullFrame.Passed);
            Assert.That(fullFrame.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "lastDrainedCommandCount"));
            Assert.That(fullFrame.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "command" && row.Field == "cameraMode"));
        }

        [Test]
        public void RuntimeAiLabAuthorityProbe_OnlyComparesRuntimeFrameAndAuthorityHash()
        {
            var expected = new CharacterTestRuntimeAiLabAuthoritySample(new RuntimeFrame(20), 0x1234L);
            var same = new CharacterTestRuntimeAiLabAuthoritySample(new RuntimeFrame(20), 0x1234L);
            var different = new CharacterTestRuntimeAiLabAuthoritySample(new RuntimeFrame(21), 0x5678L);

            CharacterTestRuntimeAiLabFrameSyncReport pass =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(expected, same);
            CharacterTestRuntimeAiLabFrameSyncReport fail =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(expected, different);

            Assert.IsTrue(pass.Passed, pass.SummaryText);
            Assert.IsFalse(fail.Passed);
            Assert.That(fail.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "authority" && row.Field == "runtimeFrame"));
            Assert.That(fail.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "authority" && row.Field == "stateHash"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsRuntimeEventMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                runtimeEvents: new CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample(
                    pendingCount: 1,
                    oldestFrame: 20,
                    newestFrame: 21,
                    nextSequence: 4L,
                    eventTypeName: "CharacterTestRuntimeEvent",
                    recentCount: 1,
                    recentHash: 101L,
                    lastFrame: 21,
                    lastType: (int)CharacterTestRuntimeEventType.CommandAccepted,
                    lastCommandId: CharacterTestRuntimeCommandIds.SetCameraMode,
                    lastSourceId: CharacterTestRuntimeCommandSources.DebugUi,
                    lastTargetId: 0,
                    lastEntityId: 0,
                    lastPayload0: 1,
                    lastPayload1: 0,
                    lastPayload2: 0,
                    lastMessage: "camera accepted"));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                runtimeEvents: new CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample(
                    pendingCount: 2,
                    oldestFrame: 19,
                    newestFrame: 22,
                    nextSequence: 7L,
                    eventTypeName: "CharacterTestRuntimeEvent",
                    recentCount: 2,
                    recentHash: 202L,
                    lastFrame: 22,
                    lastType: (int)CharacterTestRuntimeEventType.CommandRejected,
                    lastCommandId: CharacterTestRuntimeCommandIds.SetTargetLock,
                    lastSourceId: CharacterTestRuntimeCommandSources.DebugUi,
                    lastTargetId: 2,
                    lastEntityId: 2,
                    lastPayload0: 0,
                    lastPayload1: 1,
                    lastPayload2: 0,
                    lastMessage: "target rejected"));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "pendingCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "oldestFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "recentHash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "lastFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "lastType"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "lastCommandId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "runtimeEvent" && row.Field == "lastMessage"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_UsesSelectedFrameRuntimeEvents()
        {
            using var harness = new CharacterTestSliceTestHarness();
            CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
            var expectedEvents = new CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample(
                pendingCount: 3,
                oldestFrame: 40,
                newestFrame: 42,
                nextSequence: 12L,
                eventTypeName: "CharacterTestRuntimeEvent",
                recentCount: 3,
                recentHash: 4242L,
                lastFrame: 42,
                lastType: (int)CharacterTestRuntimeEventType.CommandAccepted,
                lastCommandId: CharacterTestRuntimeCommandIds.SetCameraMode,
                lastSourceId: CharacterTestRuntimeCommandSources.DebugUi,
                lastTargetId: 0,
                lastEntityId: 0,
                lastPayload0: (int)CharacterTestCameraMode.MultiTargetAuto,
                lastPayload1: 0,
                lastPayload2: 0,
                lastMessage: "selected frame event");
            var selectedFrame = new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                frame: 42,
                realtimeSeconds: 0.7f,
                CreateSnapshot("runtime-event-selected-frame"),
                expectedEvents);

            CharacterTestRuntimeAiLabFrameSyncSample sample =
                CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                    harness.Slice,
                    participant,
                    selectedFrame,
                    CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("selected frame"),
                    includeRuntimeEvents: true);

            Assert.AreEqual(expectedEvents.PendingCount, sample.RuntimeEvents.PendingCount);
            Assert.AreEqual(expectedEvents.RecentHash, sample.RuntimeEvents.RecentHash);
            Assert.AreEqual(expectedEvents.LastFrame, sample.RuntimeEvents.LastFrame);
            Assert.AreEqual(expectedEvents.LastMessage, sample.RuntimeEvents.LastMessage);
        }

        [Test]
        public void RuntimeAiLabPresentationDiagnostics_ReportsVfxMismatchWhileAuthorityIgnoresIt()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                vfxRuntime: CreateVfxRuntimeSyncSample(
                    activeCount: 1,
                    instance: CreateVfxInstanceSyncSample(
                        definitionId: 7101,
                        localFrame: 4,
                        anchorPositionXRaw: 1000L,
                        traceId: new RuntimeTraceIdentity(932587))));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                vfxRuntime: CreateVfxRuntimeSyncSample(
                    activeCount: 2,
                    instance: CreateVfxInstanceSyncSample(
                        definitionId: 7102,
                        localFrame: 5,
                        anchorPositionXRaw: 1200L,
                        traceId: new RuntimeTraceIdentity(999226))));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);
            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "vfx" && row.Field == "activeCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "vfx" && row.EntityId == 101 && row.Field == "definitionId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "vfx" && row.EntityId == 101 && row.Field == "anchorPositionXRaw"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "vfx" && row.EntityId == 101 && row.Field == "localFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "vfx" && row.EntityId == 101 && row.Field == "traceId"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsAudioMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                audio: CreateAudioSyncSample(
                    totalPlayRequests: 3,
                    activeEventName: "dash",
                    intentLastCueId: 500301,
                    interactionLastCueId: 500302,
                    physicalBodyPartPlayedCount: 1,
                    weaponHitPlayedCount: 2,
                    weaponHitLastCueKey: "slash-hit"));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                audio: CreateAudioSyncSample(
                    totalPlayRequests: 4,
                    activeEventName: "dash-late",
                    intentLastCueId: 500303,
                    interactionLastCueId: 500304,
                    physicalBodyPartPlayedCount: 3,
                    weaponHitPlayedCount: 1,
                    weaponHitLastCueKey: "slash-blocked"));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "totalPlayRequests"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "activeEvent.51.name"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "intent.lastCueKey"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "interaction.lastAudioCueKey"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "physical.bodyPartContactPlayedCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "weaponHit.playedCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "audio" && row.Field == "weaponHit.lastCueKey"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsRuntimeAiPlannerMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                planner: CreatePlannerSyncSample(
                    frame: 20,
                    targetEntityId: 2,
                    activeStatePath: "root/engage/movement",
                    selectedActionId: 7,
                    selectedActionName: "ApproachTarget",
                    selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                    targetDistanceMillimeters: 8300,
                    lastCommand: "SetControlIntent",
                    suppressedReason: "None",
                    rejectReason: string.Empty,
                    decisionMoveXMilli: 1000));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                planner: CreatePlannerSyncSample(
                    frame: 21,
                    targetEntityId: 3,
                    activeStatePath: "root/engage/combat",
                    selectedActionId: 8,
                    selectedActionName: "PrimaryAttack",
                    selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                    targetDistanceMillimeters: 1600,
                    lastCommand: "PrimaryAttack",
                    suppressedReason: "NoExecutableAction",
                    rejectReason: "OutOfRange",
                    decisionMoveXMilli: 0));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.Field == "frame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "targetEntityId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "activeStatePath"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "selectedActionId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "selectedActionName"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "targetDistanceMillimeters"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "lastCommand"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "decisionMoveXMilli"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsRuntimeAiPlannerMovementStateMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                planner: CreatePlannerSyncSample(
                    frame: 20,
                    targetEntityId: 2,
                    activeStatePath: "root/engage/movement",
                    selectedActionId: 9,
                    selectedActionName: "StrafeWhenBlocked",
                    selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.Strafe,
                    targetDistanceMillimeters: 4300,
                    lastCommand: "SetControlIntent",
                    suppressedReason: string.Empty,
                    rejectReason: string.Empty,
                    decisionMoveXMilli: -500,
                    movementSpeedScaleMilli: 750,
                    movementStrafeSide: -1,
                    movementDirectionHoldFrames: 12,
                    movementReactionDelayFrames: 4));
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                planner: CreatePlannerSyncSample(
                    frame: 20,
                    targetEntityId: 2,
                    activeStatePath: "root/engage/movement",
                    selectedActionId: 9,
                    selectedActionName: "StrafeWhenBlocked",
                    selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.Strafe,
                    targetDistanceMillimeters: 4300,
                    lastCommand: "SetControlIntent",
                    suppressedReason: string.Empty,
                    rejectReason: string.Empty,
                    decisionMoveXMilli: -500,
                    movementSpeedScaleMilli: 1000,
                    movementStrafeSide: 1,
                    movementDirectionHoldFrames: 0,
                    movementReactionDelayFrames: 0));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "movementSpeedScaleMilli"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "movementStrafeSide"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "movementDirectionHoldFrames"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "planner" && row.EntityId == 1 && row.Field == "movementReactionDelayFrames"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsControlBehaviorSelectionMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                controlOwner: "AiControlled",
                runtimeAiPlannerProfileSlot: 3,
                runtimeAiBehaviorHasSelection: true,
                runtimeAiBehaviorLoadoutId: 10010,
                runtimeAiBehaviorVariantId: 1,
                runtimeAiBehaviorCatalogHash: 123456789L,
                runtimeAiBehaviorProfileSlot: 3);
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                controlOwner: "LocalPlayer",
                runtimeAiPlannerProfileSlot: 4,
                runtimeAiBehaviorHasSelection: true,
                runtimeAiBehaviorLoadoutId: 10011,
                runtimeAiBehaviorVariantId: 2,
                runtimeAiBehaviorCatalogHash: 987654321L,
                runtimeAiBehaviorProfileSlot: 4);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "control" && row.EntityId == 1 && row.Field == "owner"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "control" && row.EntityId == 1 && row.Field == "runtimeAiPlannerProfileSlot"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "control" && row.EntityId == 1 && row.Field == "runtimeAiBehaviorLoadoutId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "control" && row.EntityId == 1 && row.Field == "runtimeAiBehaviorVariantId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "control" && row.EntityId == 1 && row.Field == "runtimeAiBehaviorCatalogHash"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "control" && row.EntityId == 1 && row.Field == "runtimeAiBehaviorProfileSlot"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsCollisionLifecycleMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                physicsCapsuleRadiusMillimeters: 350,
                physicsCapsuleHeightMillimeters: 1800,
                physicsContactCount: 0,
                postureProfileId: CharacterTestColliderPostureComponent.StandingProfileId,
                postureHeightMillimeters: 1800,
                postureRadiusMillimeters: 350,
                postureSource: "Standing",
                presenceState: "Active",
                battleLifeState: "Alive",
                battleDominantReason: "None");
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                physicsCapsuleRadiusMillimeters: 220,
                physicsCapsuleHeightMillimeters: 900,
                physicsContactCount: 2,
                postureProfileId: CharacterTestColliderPostureComponent.CorpseProfileId,
                postureHeightMillimeters: 900,
                postureRadiusMillimeters: 220,
                postureSource: "Lifecycle",
                presenceState: "PendingDestroy",
                presenceRequestedFrame: 19L,
                presenceTraceId: new RuntimeTraceIdentity(1201801),
                battleLifeState: "Dead",
                battleDominantReason: "Death",
                battleSinceFrame: 19L,
                battleLastTransitionFrame: 19L);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "physics" && row.EntityId == 1 && row.Field == "capsuleRadius"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "physics" && row.EntityId == 1 && row.Field == "contactCount"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "posture" && row.EntityId == 1 && row.Field == "profileId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "posture" && row.EntityId == 1 && row.Field == "source"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "presence" && row.EntityId == 1 && row.Field == "state"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "presence" && row.EntityId == 1 && row.Field == "requestedFrame"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "battle" && row.EntityId == 1 && row.Field == "lifeState"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "battle" && row.EntityId == 1 && row.Field == "dominantReason"));
        }

        [Test]
        public void RuntimeAiLabFrameSyncProbe_ReportsInputMovementStateMismatch()
        {
            CharacterTestRuntimeAiLabFrameSyncSample expected = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                inputMoveXMilli: 700,
                inputHasWorldMove: true,
                inputAbilitySlotPressed: 2,
                inputAbilityIdPressed: 910002,
                jumpBufferFramesRemaining: 3,
                movementMode: "Run",
                movementProfileId: 1202101,
                collisionFlags: "Grounded",
                surfaceMaterialKind: "Rough",
                surfaceMaterialStableId: 1012,
                surfaceElasticityRaw: 100L);
            CharacterTestRuntimeAiLabFrameSyncSample actual = CreateFrameSyncSample(
                xMillimeters: 1000,
                runnerLocalFrame: 8,
                overlayFrame: 20,
                overlaySourceX: 1f,
                inputMoveXMilli: -300,
                inputHasWorldMove: false,
                inputAbilitySlotPressed: 0,
                inputAbilityIdPressed: 0,
                jumpBufferFramesRemaining: 0,
                movementMode: "Idle",
                movementProfileId: 1202102,
                collisionFlags: "None",
                surfaceMaterialKind: "HardSlick",
                surfaceMaterialStableId: 1011,
                surfaceElasticityRaw: 200L);

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);

            Assert.IsFalse(report.Passed);
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "input" && row.EntityId == 1 && row.Field == "moveX"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "input" && row.EntityId == 1 && row.Field == "abilityIdPressed"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movement" && row.EntityId == 1 && row.Field == "jumpBufferFramesRemaining"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movement" && row.EntityId == 1 && row.Field == "movementProfileId"));
            Assert.That(report.MismatchRows, Has.Some.Matches<CharacterTestRuntimeAiLabFrameSyncMismatch>(
                row => row.Layer == "movement" && row.EntityId == 1 && row.Field == "surfaceMaterialKind"));
        }
    }
}
