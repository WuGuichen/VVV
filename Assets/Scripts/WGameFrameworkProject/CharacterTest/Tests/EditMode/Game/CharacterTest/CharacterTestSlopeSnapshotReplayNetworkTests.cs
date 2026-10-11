using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSlopeSnapshotReplayNetworkTests
    {
        private const int TickRate = 60;
        private const int Slope15BodyId = 10080;
        private const int Slope30BodyId = 10081;
        private const int Slope50BodyId = 10082;
        private const int SlopeColliderId = 1;
        private const int SlopeLayer = 1;
        private const int MotionSequenceTicks = 24;
        private const int IdleTicks = 600;

        private static readonly int[] FormalSlopeBodyIds =
        {
            Slope15BodyId,
            Slope30BodyId,
            Slope50BodyId,
        };

        [Test]
        public void Rollback_ThirtyDegreeMidSlopeRestoreAndContinueMatchesAuthorityAndHash()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("level=formal hash=first");
            using (GameSlice slice = CreateSlice(physics))
            {
                CharacterTestCharacterEntity entity = PrepareThirtyDegreeCharacter(slice, physics);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame captureFrame = slice.CurrentFrame;
                CharacterTestNetworkRollbackState rollback =
                    slice.Services.Network.CaptureRollbackState(captureFrame, participant);

                AuthoritySample[] uninterrupted = RunMotionSequence(
                    slice,
                    participant,
                    entity.Id.Value,
                    MotionSequenceTicks,
                    1201710);

                Assert.IsTrue(slice.Services.Network.RestoreRollbackState(
                    captureFrame,
                    rollback,
                    participant,
                    out string failure), failure);
                Assert.AreEqual(captureFrame, slice.CurrentFrame);

                AuthoritySample[] restored = RunMotionSequence(
                    slice,
                    participant,
                    entity.Id.Value,
                    MotionSequenceTicks,
                    1201710);

                AssertSamplesEqual(uninterrupted, restored);
                Assert.IsTrue(uninterrupted[0].Diagnostic.HasCollision);
                Assert.AreEqual(
                    CombatPhysicsShapeKind.Obb,
                    uninterrupted[0].Diagnostic.TargetShapeKind);
                Assert.AreEqual(
                    new CombatBodyId(Slope30BodyId),
                    uninterrupted[0].Diagnostic.TargetBodyId);
            }
        }

        [Test]
        public void Rollback_ThirtyDegreeIdleSixHundredTicksIsExactAndDoesNotDrift()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("level=idle hash=second");
            using (GameSlice slice = CreateSlice(physics))
            {
                CharacterTestCharacterEntity entity = PrepareThirtyDegreeCharacter(slice, physics);
                int originX = entity.Transform.XMillimeters;
                int originZ = entity.Transform.ZMillimeters;
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame captureFrame = slice.CurrentFrame;
                CharacterTestNetworkRollbackState rollback =
                    slice.Services.Network.CaptureRollbackState(captureFrame, participant);
                var baseline = new AuthoritySample[IdleTicks];

                for (int i = 0; i < IdleTicks; i++)
                {
                    baseline[i] = RunIntentStep(
                        slice,
                        participant,
                        entity.Id.Value,
                        worldMoveXMilli: 0,
                        traceId: new RuntimeTraceIdentity(920808, 0, 0, i));
                    Assert.AreEqual(originX, baseline[i].XMillimeters, "baseline X drift at tick " + i);
                    Assert.AreEqual(originZ, baseline[i].ZMillimeters, "baseline Z drift at tick " + i);
                    Assert.IsTrue(baseline[i].Grounded, "baseline grounded at tick " + i);
                }

                Assert.IsTrue(slice.Services.Network.RestoreRollbackState(
                    captureFrame,
                    rollback,
                    participant,
                    out string failure), failure);

                for (int i = 0; i < IdleTicks; i++)
                {
                    AuthoritySample restored = RunIntentStep(
                        slice,
                        participant,
                        entity.Id.Value,
                        worldMoveXMilli: 0,
                        traceId: new RuntimeTraceIdentity(920808, 0, 0, i));
                    AssertSampleEqual(baseline[i], restored, "idle tick " + i);
                    Assert.AreEqual(originX, restored.XMillimeters, "restored X drift at tick " + i);
                    Assert.AreEqual(originZ, restored.ZMillimeters, "restored Z drift at tick " + i);
                    Assert.IsTrue(restored.Grounded, "restored grounded at tick " + i);
                }
            }
        }

        [Test]
        public void Replay_ThirtyDegreeHashesMatchAndTransientDiagnosticIsRecomputedAfterTick()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime(
                "level=character-test hash=12242125253111897828");
            using (GameSlice source = CreateSlice(physics))
            {
                CharacterTestCharacterEntity entity = PrepareThirtyDegreeCharacter(source, physics);
                CharacterTestNetworkSnapshotParticipant participant =
                    source.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame startFrame = source.CurrentFrame;
                RuntimeCustomState bootstrap = participant.CaptureSnapshot(startFrame);
                var recorder = new CharacterTestReplayRecorder(CreateReplayConfig());
                recorder.Begin(startFrame, bootstrap);
                var expectedDiagnostics = new CharacterTestCharacterMotionDiagnostic[MotionSequenceTicks];

                for (int i = 0; i < MotionSequenceTicks; i++)
                {
                    RuntimeFrame frame = source.CurrentFrame;
                    RuntimeCommand command = CreateSlopeCommand(
                        frame,
                        entity.Id.Value,
                        ResolveWorldMoveXMilli(i),
                        new RuntimeTraceIdentity(1201712, sequence: i));
                    RuntimeCommandValidationResult enqueue = source.EnqueueRuntimeCommand(command);
                    Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

                    source.Tick(1d / TickRate);
                    long hash = participant.ComputeStateHash(frame);
                    IReadOnlyList<RuntimeCommand> drained = source.RuntimeCommands.LastDrainedCommands;
                    recorder.RecordAuthoritativeCommands(new NetworkAuthoritativeCommandPacket(
                        "local",
                        frame,
                        frame,
                        drained));
                    recorder.RecordFrame(
                        frame,
                        drained,
                        hash,
                        CharacterTestReplayDiagnostics.CreateFrameSummary(
                            frame,
                            drained.Count,
                            source.CharacterEntities.Count,
                            hash));
                    Assert.IsTrue(source.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                        entity.Id.Value,
                        out expectedDiagnostics[i]));
                    Assert.AreEqual(frame, expectedDiagnostics[i].Frame);
                }

                CharacterTestReplayCapture capture = recorder.CreateCapture(
                    () => CreateSlice(CharacterTestNetworkSessionRunner.CloneReplayPhysicsRuntime(physics)));
                Assert.AreEqual(CharacterTestReplayRecorder.SchemaVersion, capture.RuntimeReplaySnapshot.Header.SchemaVersion);
                Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue, bootstrap.SchemaVersion);
                StringAssert.DoesNotContain("groundSnapAppliedDistance", bootstrap.PayloadJson);
                StringAssert.DoesNotContain("surfaceClassification", bootstrap.PayloadJson);
                StringAssert.DoesNotContain("motionDiagnostic", bootstrap.PayloadJson);

                using (var driver = new CharacterTestReplayFrameDriver(capture))
                {
                    driver.Reset(capture.RuntimeReplaySnapshot.Header);
                    for (int i = 0; i < capture.FrameCount; i++)
                    {
                        RuntimeReplayFrameRecord record = capture.RuntimeReplaySnapshot.Records[i];
                        RuntimeReplayPlaybackFrameResult result = driver.RunFrame(record);
                        Assert.AreEqual(0, result.CommandErrors.Count, "replay command errors at " + record.Frame.Value);
                        Assert.AreEqual(record.ResultHash, result.ActualResultHash, "replay hash at " + record.Frame.Value);
                        Assert.IsTrue(driver.Slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                            entity.Id.Value,
                            out CharacterTestCharacterMotionDiagnostic actual));
                        Assert.AreEqual(record.Frame, actual.Frame);
                        Assert.AreEqual(expectedDiagnostics[i], actual, "diagnostic at " + record.Frame.Value);
                    }
                }

                RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);
                Assert.IsTrue(playback.Success, playback.FailureMessage);
            }
        }

        [Test]
        public void ReplayClones_PreserveThreeFormalObbsSnapStaticOnlyAndIsolation()
        {
            CharacterTestCharacterPhysicsRuntime source = CreateFormalSlopeRuntime(
                "level=character-test hash=12242125253111897828");
            var dynamicBody = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 99);
            source.World.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(dynamicBody.Value),
                dynamicBody,
                FixVector3.Zero));
            source.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                dynamicBody,
                new CombatColliderId(1),
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer,
                R(-500000, -500000, -500000),
                R(500000, 500000, 500000)));

            CharacterTestCharacterPhysicsRuntime core =
                CharacterTestNetworkSessionRunner.CloneReplayPhysicsRuntime(source);
            CharacterTestCharacterPhysicsRuntime transport =
                CharacterTestUnityTransportServerRunner.CloneReplayPhysicsRuntime(source);

            AssertFormalClone(source, core, dynamicBody);
            AssertFormalClone(source, transport, dynamicBody);
            Assert.AreNotSame(core, transport);
            Assert.AreNotSame(core.World, transport.World);

            Assert.IsTrue(core.World.TryGetBody(
                new CombatBodyId(Slope30BodyId),
                out CombatPhysicsBody coreBody));
            FixVector3 shifted = coreBody.Position + new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            core.World.SetBodyPosition(coreBody.BodyId, shifted);
            Assert.IsTrue(source.World.TryGetBody(coreBody.BodyId, out CombatPhysicsBody sourceBody));
            Assert.IsTrue(transport.World.TryGetBody(coreBody.BodyId, out CombatPhysicsBody transportBody));
            Assert.AreNotEqual(shifted, sourceBody.Position);
            Assert.AreEqual(sourceBody.Position, transportBody.Position);
        }

        [Test]
        public void PhysicsOwnership_OfflineCoreMirrorRemoteAndHostMatchCurrentMatrix()
        {
            CharacterTestCharacterPhysicsRuntime offlinePhysics = CreateFormalSlopeRuntime("offline");
            using (GameSlice offline = CreateSlice(offlinePhysics))
            {
                Assert.AreSame(offlinePhysics, offline.PhysicsRuntime);
                AssertFormalPhysicsOwner(offline.PhysicsRuntime);
            }

            var coreTransport = new LoopbackNetworkTransport();
            CharacterTestCharacterPhysicsRuntime corePhysics = CreateFormalSlopeRuntime("core-loopback");
            try
            {
                using (var core = new CharacterTestNetworkSessionRunner(
                    coreTransport,
                    coreTransport,
                    NullRuntimeLogger.Instance,
                    physicsRuntime: corePhysics,
                    config: CreateReplayConfig(),
                    createServerViews: false,
                    disposeTransports: false))
                {
                    Assert.AreSame(corePhysics, core.ServerSlice.PhysicsRuntime);
                    AssertFormalPhysicsOwner(core.ServerSlice.PhysicsRuntime);
                    Assert.IsNull(core.ClientSlice.PhysicsRuntime, "Core loopback client is a snapshot mirror.");
                }
            }
            finally
            {
                coreTransport.Clear();
            }

            var serverTransport = new LoopbackNetworkTransport();
            var clientTransport = new LoopbackNetworkTransport();
            CharacterTestCharacterPhysicsRuntime serverPhysics = CreateFormalSlopeRuntime("remote-server");
            CharacterTestCharacterPhysicsRuntime clientPhysics = CreateFormalSlopeRuntime("remote-client-local-factory");
            try
            {
                using (var server = new CharacterTestUnityTransportServerRunner(
                    serverTransport,
                    NullRuntimeLogger.Instance,
                    physicsRuntime: serverPhysics,
                    config: CreateReplayConfig(),
                    createServerViews: false,
                    disposeTransport: false))
                using (var client = new CharacterTestUnityTransportClientRunner(
                    clientTransport,
                    NullRuntimeLogger.Instance,
                    physicsRuntime: clientPhysics,
                    config: CreateReplayConfig(),
                    createClientViews: false,
                    disposeTransport: false,
                    connectOnCreate: false))
                {
                    Assert.AreSame(serverPhysics, server.Slice.PhysicsRuntime);
                    Assert.AreSame(clientPhysics, client.Slice.PhysicsRuntime);
                    Assert.AreNotSame(server.Slice.PhysicsRuntime, client.Slice.PhysicsRuntime);
                    AssertFormalPhysicsOwner(server.Slice.PhysicsRuntime);
                    AssertFormalPhysicsOwner(client.Slice.PhysicsRuntime);
                }
            }
            finally
            {
                serverTransport.Clear();
                clientTransport.Clear();
            }

            var hostServerTransport = new LoopbackNetworkTransport();
            var hostClientTransport = new LoopbackNetworkTransport();
            CharacterTestCharacterPhysicsRuntime hostPhysics = CreateFormalSlopeRuntime("host-shared");
            try
            {
                using (var host = new CharacterTestUnityTransportHostRunner(
                    hostServerTransport,
                    hostClientTransport,
                    NullRuntimeLogger.Instance,
                    physicsRuntime: hostPhysics,
                    config: CreateReplayConfig(),
                    createClientViews: false,
                    disposeTransports: false))
                {
                    Assert.AreSame(hostPhysics, host.ServerRunner.Slice.PhysicsRuntime);
                    Assert.AreSame(hostPhysics, host.LocalClientRunner.Slice.PhysicsRuntime);
                    AssertFormalPhysicsOwner(host.ServerRunner.Slice.PhysicsRuntime);
                    AssertFormalPhysicsOwner(host.LocalClientRunner.Slice.PhysicsRuntime);
                }
            }
            finally
            {
                hostServerTransport.Clear();
                hostClientTransport.Clear();
            }
        }

        [Test]
        public void SnapshotHash_IgnoresPhysicsSourceDescriptionAndCarriesNoStaticDiagnosticPayload()
        {
            CharacterTestCharacterPhysicsRuntime leftPhysics = CreateFormalSlopeRuntime(
                "level=character-test hash=12242125253111897828");
            CharacterTestCharacterPhysicsRuntime rightPhysics = CreateFormalSlopeRuntime(
                "level=different diagnostic hash=0");
            using (GameSlice left = CreateSlice(leftPhysics))
            using (GameSlice right = CreateSlice(rightPhysics))
            {
                CharacterTestCharacterEntity leftEntity = PrepareThirtyDegreeCharacter(left, leftPhysics);
                CharacterTestCharacterEntity rightEntity = PrepareThirtyDegreeCharacter(right, rightPhysics);
                Assert.AreEqual(left.CurrentFrame, right.CurrentFrame);
                Assert.AreEqual(leftEntity.Id, rightEntity.Id);

                CharacterTestNetworkSnapshotParticipant leftParticipant =
                    left.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant rightParticipant =
                    right.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = left.CurrentFrame;
                RuntimeCustomState leftSnapshot = leftParticipant.CaptureSnapshot(frame);
                RuntimeCustomState rightSnapshot = rightParticipant.CaptureSnapshot(frame);

                Assert.AreNotEqual(leftPhysics.SourceDescription, rightPhysics.SourceDescription);
                Assert.AreEqual(leftParticipant.ComputeStateHash(frame), rightParticipant.ComputeStateHash(frame));
                Assert.AreEqual(leftSnapshot.PayloadJson, rightSnapshot.PayloadJson);
                StringAssert.DoesNotContain("SourceDescription", leftSnapshot.PayloadJson);
                StringAssert.DoesNotContain("groundSnapAppliedDistance", leftSnapshot.PayloadJson);
                StringAssert.DoesNotContain("surfaceClassification", leftSnapshot.PayloadJson);
                StringAssert.DoesNotContain("obbColliders", leftSnapshot.PayloadJson);
            }
        }

        [Test]
        public void Replay_ThirtyDegreeJumpAndLandReplaysHashesAndObbDiagnostics()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("replay-30-jump-land");
            using (GameSlice source = CreateSlice(physics))
            {
                CharacterTestCharacterEntity entity = PrepareThirtyDegreeCharacter(source, physics);
                CharacterTestNetworkSnapshotParticipant participant =
                    source.Services.Network.CreateSnapshotParticipant();
                var recorder = new CharacterTestReplayRecorder(CreateReplayConfig());
                recorder.Begin(source.CurrentFrame, participant.CaptureSnapshot(source.CurrentFrame));
                var diagnostics = new List<CharacterTestCharacterMotionDiagnostic>();
                bool sawAirborne = false;
                bool landedOnThirtyDegreeObb = false;

                for (int i = 0; i < 240; i++)
                {
                    RuntimeFrame frame = source.CurrentFrame;
                    RuntimeCommand command = CreateIntentCommand(
                        frame,
                        entity.Id.Value,
                        worldMoveXMilli: 0,
                        jumpPressed: i == 0,
                        jumpHeld: i <= 10,
                        jumpReleased: i == 11,
                        traceId: new RuntimeTraceIdentity(987689, 0, 0, i));
                    CharacterTestCharacterMotionDiagnostic diagnostic = RecordReplayFrame(
                        source,
                        participant,
                        recorder,
                        command);
                    diagnostics.Add(diagnostic);

                    if (!entity.IsGrounded)
                        sawAirborne = true;
                    if (sawAirborne
                        && entity.IsGrounded
                        && diagnostic.HasCollision
                        && diagnostic.TargetBodyId.Equals(new CombatBodyId(Slope30BodyId))
                        && diagnostic.TargetShapeKind == CombatPhysicsShapeKind.Obb)
                    {
                        landedOnThirtyDegreeObb = true;
                        break;
                    }
                }

                Assert.IsTrue(sawAirborne, "Jump must leave the 30-degree support.");
                Assert.IsTrue(landedOnThirtyDegreeObb, "Jump must land back on body 10081 within 240 ticks.");
                CharacterTestReplayCapture capture = recorder.CreateCapture(
                    () => CreateSlice(CharacterTestNetworkSessionRunner.CloneReplayPhysicsRuntime(physics)));
                AssertReplayMatches(
                    capture,
                    diagnostics.ToArray(),
                    entity.Id.Value);
            }
        }

        [Test]
        public void Replay_FiftyDegreeBlockedNeverClimbsAndReplaysSteepObbDiagnostics()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("replay-50-blocked");
            using (GameSlice source = CreateSlice(physics))
            {
                Tick(source, 8);
                CharacterTestCharacterEntity entity = FindPlayer(source);
                PlaceOnSlope(entity, physics, Slope50BodyId);
                int startY = entity.Transform.YMillimeters;
                int highestY = startY;
                CharacterTestNetworkSnapshotParticipant participant =
                    source.Services.Network.CreateSnapshotParticipant();
                var recorder = new CharacterTestReplayRecorder(CreateReplayConfig());
                recorder.Begin(source.CurrentFrame, participant.CaptureSnapshot(source.CurrentFrame));
                var diagnostics = new CharacterTestCharacterMotionDiagnostic[120];
                bool sawSteepObb = false;

                for (int i = 0; i < diagnostics.Length; i++)
                {
                    RuntimeCommand command = CreateIntentCommand(
                        source.CurrentFrame,
                        entity.Id.Value,
                        worldMoveXMilli: -1000,
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        traceId: new RuntimeTraceIdentity(970325, 0, 0, i));
                    diagnostics[i] = RecordReplayFrame(
                        source,
                        participant,
                        recorder,
                        command);
                    highestY = System.Math.Max(highestY, entity.Transform.YMillimeters);
                    if (diagnostics[i].HasCollision
                        && diagnostics[i].TargetBodyId.Equals(new CombatBodyId(Slope50BodyId))
                        && diagnostics[i].TargetShapeKind == CombatPhysicsShapeKind.Obb
                        && diagnostics[i].SurfaceClassification == CharacterTestMotionSurfaceClassification.Steep)
                    {
                        sawSteepObb = true;
                        Assert.IsTrue(
                            diagnostics[i].AppliedDelta.Y <= Fix64.Zero,
                            "Steep OBB contact must not apply a positive Y delta at tick " + i);
                    }
                }

                Assert.IsTrue(sawSteepObb, "The blocked sequence must hit the formal 50-degree OBB.");
                Assert.LessOrEqual(
                    highestY,
                    startY + 10,
                    "Authority Y must not climb beyond the 10 mm skin allowance.");
                CharacterTestReplayCapture capture = recorder.CreateCapture(
                    () => CreateSlice(CharacterTestNetworkSessionRunner.CloneReplayPhysicsRuntime(physics)));
                AssertReplayMatches(capture, diagnostics, entity.Id.Value);
            }
        }

        [Test]
        public void Replay_MutatedSetControlIntentFirstDivergesAtModifiedOrNextAffectedFrame()
        {
            RecordedReplay recorded = RecordThirtyDegreeMotionReplay();
            CharacterTestReplayCapture source = recorded.Capture;
            var records = new List<RuntimeReplayFrameRecord>(source.RuntimeReplaySnapshot.Records);
            int mutationIndex = 4;
            RuntimeReplayFrameRecord originalRecord = records[mutationIndex];
            var commands = new List<RuntimeCommand>(originalRecord.Commands);
            int commandIndex = -1;
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].CommandId == CharacterTestRuntimeCommandIds.SetControlIntent
                    && commands[i].TargetId == recorded.EntityId)
                {
                    commandIndex = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(commandIndex, 0, "Expected the recorded player SetControlIntent.");
            commands[commandIndex] = CreateIntentCommand(
                originalRecord.Frame,
                recorded.EntityId,
                worldMoveXMilli: 1000,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                traceId: new RuntimeTraceIdentity(981426));
            records[mutationIndex] = new RuntimeReplayFrameRecord(
                originalRecord.Frame,
                commands,
                originalRecord.ResultHash,
                originalRecord.DiagnosticsSummary);
            var mutatedRuntime = new RuntimeReplaySnapshot(source.RuntimeReplaySnapshot.Header, records);
            var mutated = new CharacterTestReplayCapture(
                mutatedRuntime,
                source.BootstrapSnapshot,
                source.TickRate,
                source.StateSyncPolicy,
                source.AuthoritativeCommands,
                source.ReplaySliceFactory,
                source.SessionAuthorityBootstrapSnapshot,
                source.SessionAuthorityHash);

            int firstDivergenceIndex = -1;
            using (var driver = new CharacterTestReplayFrameDriver(mutated))
            {
                driver.Reset(mutated.RuntimeReplaySnapshot.Header);
                for (int i = 0; i < records.Count; i++)
                {
                    RuntimeReplayPlaybackFrameResult result = driver.RunFrame(records[i]);
                    if (result.ActualResultHash == records[i].ResultHash)
                        continue;

                    firstDivergenceIndex = i;
                    break;
                }
            }

            Assert.AreEqual(
                mutationIndex,
                firstDivergenceIndex,
                "SetControlIntent is applied in the recorded Tick, so its first hash divergence must be the modified frame.");
            RuntimeFrame expectedFailureFrame = records[firstDivergenceIndex].Frame;
            RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(mutated);
            Assert.IsFalse(playback.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, playback.FailureCode);
            Assert.AreEqual(expectedFailureFrame, playback.FailureFrame);
        }

        [Test]
        public void CoreLoopback_ThirtyDegreeServerAndSnapshotMirrorConvergeAtFullSnapshotBoundary()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("core-loopback-30");
            using (var runner = new CharacterTestNetworkLoopbackRunner(
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                config: CreateReplayConfig(NetworkStateSyncPolicy.SnapshotVerification, 1),
                transportOptions: LoopbackNetworkTransportOptions.ReliableImmediate,
                createServerViews: false))
            {
                Tick(runner, 30);
                CharacterTestNetworkLoopbackDiagnostics diagnostics = runner.CaptureDiagnostics();
                Assert.AreEqual(NetworkSessionStatus.Connected, diagnostics.ClientStatus);
                Assert.Greater(diagnostics.OwnedEntityId, 0);
                int entityId = diagnostics.OwnedEntityId;
                CharacterTestCharacterEntity serverEntity =
                    runner.ServerSlice.CharacterRuntime.FindEntityById(entityId);
                Assert.IsNotNull(serverEntity);
                PlaceOnSlope(serverEntity, physics, Slope30BodyId);

                for (int i = 0; i < 12; i++)
                {
                    RuntimeCommandValidationResult submit = runner.SubmitClientCommand(
                        CreateIntentCommand(
                            runner.ServerSlice.CurrentFrame,
                            entityId,
                            worldMoveXMilli: -1000,
                            jumpPressed: false,
                            jumpHeld: false,
                            jumpReleased: false,
                            traceId: new RuntimeTraceIdentity(935857, 0, 0, i)));
                    Assert.IsTrue(submit.Success, submit.Error.Message);
                    runner.Tick(1d / TickRate);
                }

                Assert.IsTrue(runner.ServerSlice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                    entityId,
                    out CharacterTestCharacterMotionDiagnostic serverDiagnostic));
                Assert.AreEqual(new CombatBodyId(Slope30BodyId), serverDiagnostic.TargetBodyId);
                Assert.AreEqual(CombatPhysicsShapeKind.Obb, serverDiagnostic.TargetShapeKind);
                Assert.IsNull(runner.ClientSlice.PhysicsRuntime);

                RuntimeFrame snapshotFrame = runner.ServerSlice.CurrentFrame;
                var serverParticipant = new CharacterTestNetworkSnapshotParticipant(
                    runner.ServerSlice.CharacterRuntime,
                    runner.ServerSlice.RuntimeCommands);
                var clientParticipant = new CharacterTestNetworkSnapshotParticipant(
                    runner.ClientSlice.CharacterRuntime,
                    runner.ClientSlice.RuntimeCommands);
                CharacterTestNetworkRollbackState fullCorrection =
                    runner.ServerSlice.Services.Network.CaptureRollbackState(
                        snapshotFrame,
                        serverParticipant);
                Assert.IsTrue(runner.ClientSlice.Services.Network.RestoreRollbackState(
                    snapshotFrame,
                    fullCorrection,
                    clientParticipant,
                    out string correctionFailure), correctionFailure);
                CharacterTestCharacterEntity clientEntity =
                    runner.ClientSlice.CharacterRuntime.FindEntityById(entityId);
                Assert.IsNotNull(clientEntity);
                AssertMirrorMotionState(serverEntity, clientEntity);
                Assert.IsFalse(runner.ClientSlice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                    entityId,
                    out _), "A snapshot mirror without physics must not fabricate an OBB diagnostic.");

                Assert.AreEqual(
                    ComputeSlopeMotionAuthorityHash(snapshotFrame, serverEntity),
                    ComputeSlopeMotionAuthorityHash(snapshotFrame, clientEntity),
                    "The peer-neutral slope motion authority projection must hash identically.");
            }
        }

        [Test]
        public void ReversedObbRegistration_ProducesIdenticalAuthorityHashesAndDiagnostics()
        {
            CharacterTestCharacterPhysicsRuntime orderedPhysics =
                CreateFormalSlopeRuntime("ordered", reverseRegistration: false);
            CharacterTestCharacterPhysicsRuntime reversedPhysics =
                CreateFormalSlopeRuntime("reversed", reverseRegistration: true);
            using (GameSlice ordered = CreateSlice(orderedPhysics))
            using (GameSlice reversed = CreateSlice(reversedPhysics))
            {
                CharacterTestCharacterEntity orderedEntity =
                    PrepareThirtyDegreeCharacter(ordered, orderedPhysics);
                CharacterTestCharacterEntity reversedEntity =
                    PrepareThirtyDegreeCharacter(reversed, reversedPhysics);
                Assert.AreEqual(ordered.CurrentFrame, reversed.CurrentFrame);
                Assert.AreEqual(orderedEntity.Id, reversedEntity.Id);
                CharacterTestNetworkSnapshotParticipant orderedParticipant =
                    ordered.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant reversedParticipant =
                    reversed.Services.Network.CreateSnapshotParticipant();

                AuthoritySample[] orderedSamples = RunMotionSequence(
                    ordered,
                    orderedParticipant,
                    orderedEntity.Id.Value,
                    MotionSequenceTicks,
                    1201711);
                AuthoritySample[] reversedSamples = RunMotionSequence(
                    reversed,
                    reversedParticipant,
                    reversedEntity.Id.Value,
                    MotionSequenceTicks,
                    1201711);

                AssertSamplesEqual(orderedSamples, reversedSamples);
            }
        }

        [Test]
        public void TransientReset_RemovesDiagnosticWithoutChangingSameFrameSnapshotOrHash()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("transient-reset");
            using (GameSlice slice = CreateSlice(physics))
            {
                CharacterTestCharacterEntity entity = PrepareThirtyDegreeCharacter(slice, physics);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                AuthoritySample sample = RunIntentStep(
                    slice,
                    participant,
                    entity.Id.Value,
                    worldMoveXMilli: -1000,
                    traceId: new RuntimeTraceIdentity(907218));
                Assert.IsTrue(slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                    entity.Id.Value,
                    out CharacterTestCharacterMotionDiagnostic beforeDiagnostic));
                Assert.AreEqual(sample.Diagnostic, beforeDiagnostic);
                RuntimeCustomState beforeSnapshot = participant.CaptureSnapshot(sample.Frame);
                long beforeHash = participant.ComputeStateHash(sample.Frame);

                slice.CharacterRuntime.ResetRuntimeAiLabReplayTransientState();

                Assert.IsFalse(slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                    entity.Id.Value,
                    out _));
                RuntimeCustomState afterSnapshot = participant.CaptureSnapshot(sample.Frame);
                long afterHash = participant.ComputeStateHash(sample.Frame);
                Assert.AreEqual(beforeSnapshot.SchemaVersion, afterSnapshot.SchemaVersion);
                Assert.AreEqual(beforeSnapshot.PayloadJson, afterSnapshot.PayloadJson);
                Assert.AreEqual(beforeHash, afterHash);
            }
        }

        private static GameSlice CreateSlice(CharacterTestCharacterPhysicsRuntime physics)
        {
            return new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                createCharacterViews: false,
                enableLocalInputCommands: false);
        }

        private static CharacterTestCharacterEntity PrepareThirtyDegreeCharacter(
            GameSlice slice,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            Tick(slice, 8);
            CharacterTestCharacterEntity entity = FindPlayer(slice);
            PlaceOnSlope(entity, physics, Slope30BodyId);
            Tick(slice, 8);

            Assert.IsTrue(entity.IsGrounded);
            Assert.IsTrue(entity.PhysicsEnabled);
            Assert.IsTrue(slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                entity.Id.Value,
                out CharacterTestCharacterMotionDiagnostic diagnostic));
            Assert.IsTrue(diagnostic.HasCollision);
            Assert.AreEqual(new CombatBodyId(Slope30BodyId), diagnostic.TargetBodyId);
            Assert.AreEqual(CombatPhysicsShapeKind.Obb, diagnostic.TargetShapeKind);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, diagnostic.SurfaceMaterial.Kind);
            return entity;
        }

        private static void PlaceOnSlope(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physics,
            int slopeBodyId)
        {
            var bodyId = new CombatBodyId(slopeBodyId);
            var colliderId = new CombatColliderId(SlopeColliderId);
            Assert.IsTrue(physics.World.TryGetBody(bodyId, out CombatPhysicsBody body));
            Assert.IsTrue(physics.World.TryGetObbCollider(
                bodyId,
                colliderId,
                out CombatPhysicsObbCollider slope));

            FixVector3 halfExtents = physics.GetCharacterHalfExtents(entity);
            Fix64 radius = Fix64.Min(halfExtents.X.Abs(), halfExtents.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(halfExtents.Y.Abs() - radius, Fix64.Zero);
            Fix64 support = radius + segmentHalf * slope.AxisY.Y.Abs();
            FixVector3 topFaceCenter = slope.GetWorldCenter(body.Position) + slope.AxisY * slope.HalfExtents.Y;
            FixVector3 characterCenter = topFaceCenter + slope.AxisY * support;
            Fix64 feetY = characterCenter.Y - physics.GetCharacterCenterOffsetY(entity);

            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(characterCenter.X),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(feetY),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(characterCenter.Z)));
            entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                physicsEnabled: true,
                collisionFlags: CombatMotionCollisionFlags.Grounded,
                surfaceMaterial: CombatSurfaceMaterial.Rough);
            entity.SetInput(CharacterTestCharacterInputComponent.None);
        }

        private static AuthoritySample[] RunMotionSequence(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant participant,
            int entityId,
            int count,
            int traceKind)
        {
            var samples = new AuthoritySample[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = RunIntentStep(
                    slice,
                    participant,
                    entityId,
                    ResolveWorldMoveXMilli(i),
                    new RuntimeTraceIdentity(traceKind, sequence: i));
            }

            return samples;
        }

        private static AuthoritySample RunIntentStep(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant participant,
            int entityId,
            short worldMoveXMilli,
            RuntimeTraceIdentity traceId)
        {
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                CreateSlopeCommand(frame, entityId, worldMoveXMilli, traceId));
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
            slice.Tick(1d / TickRate);

            CharacterTestCharacterEntity entity = slice.CharacterRuntime.FindEntityById(entityId);
            Assert.IsNotNull(entity);
            Assert.IsTrue(slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                entityId,
                out CharacterTestCharacterMotionDiagnostic diagnostic));
            Assert.AreEqual(frame, diagnostic.Frame);
            return new AuthoritySample(
                frame,
                entity,
                participant.ComputeStateHash(frame),
                diagnostic);
        }

        private static RuntimeCommand CreateSlopeCommand(
            RuntimeFrame frame,
            int entityId,
            short worldMoveXMilli,
            RuntimeTraceIdentity traceId)
        {
            return CreateIntentCommand(
                frame,
                entityId,
                worldMoveXMilli,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                traceId: traceId);
        }

        private static RuntimeCommand CreateIntentCommand(
            RuntimeFrame frame,
            int entityId,
            short worldMoveXMilli,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            RuntimeTraceIdentity traceId)
        {
            CharacterTestControlIntent intent = CharacterTestControlIntent.FromDeterministic(
                entityId,
                worldMoveXMilli,
                0,
                worldMoveXMilli,
                0,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed: false);
            return CharacterTestRuntimeCommandFactory.SetControlIntent(
                frame,
                CharacterTestRuntimeCommandSources.PlayerInput,
                intent,
                traceId);
        }

        private static short ResolveWorldMoveXMilli(int index)
        {
            if (index < MotionSequenceTicks / 3)
                return -1000;
            if (index < MotionSequenceTicks * 2 / 3)
                return 0;
            return 1000;
        }

        private static CharacterTestCharacterMotionDiagnostic RecordReplayFrame(
            GameSlice source,
            CharacterTestNetworkSnapshotParticipant participant,
            CharacterTestReplayRecorder recorder,
            RuntimeCommand command)
        {
            RuntimeFrame frame = source.CurrentFrame;
            Assert.AreEqual(frame, command.Frame);
            RuntimeCommandValidationResult enqueue = source.EnqueueRuntimeCommand(command);
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
            source.Tick(1d / TickRate);
            long hash = participant.ComputeStateHash(frame);
            IReadOnlyList<RuntimeCommand> drained = source.RuntimeCommands.LastDrainedCommands;
            recorder.RecordAuthoritativeCommands(new NetworkAuthoritativeCommandPacket(
                "local",
                frame,
                frame,
                drained));
            recorder.RecordFrame(
                frame,
                drained,
                hash,
                CharacterTestReplayDiagnostics.CreateFrameSummary(
                    frame,
                    drained.Count,
                    source.CharacterEntities.Count,
                    hash));
            Assert.IsTrue(source.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                command.TargetId,
                out CharacterTestCharacterMotionDiagnostic diagnostic));
            Assert.AreEqual(frame, diagnostic.Frame);
            return diagnostic;
        }

        private static void AssertReplayMatches(
            CharacterTestReplayCapture capture,
            CharacterTestCharacterMotionDiagnostic[] expectedDiagnostics,
            int entityId)
        {
            Assert.AreEqual(expectedDiagnostics.Length, capture.FrameCount);
            using (var driver = new CharacterTestReplayFrameDriver(capture))
            {
                driver.Reset(capture.RuntimeReplaySnapshot.Header);
                for (int i = 0; i < capture.FrameCount; i++)
                {
                    RuntimeReplayFrameRecord record = capture.RuntimeReplaySnapshot.Records[i];
                    RuntimeReplayPlaybackFrameResult result = driver.RunFrame(record);
                    Assert.AreEqual(0, result.CommandErrors.Count, "command errors at " + record.Frame.Value);
                    Assert.AreEqual(record.ResultHash, result.ActualResultHash, "hash at " + record.Frame.Value);
                    Assert.IsTrue(driver.Slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                        entityId,
                        out CharacterTestCharacterMotionDiagnostic actual));
                    Assert.AreEqual(record.Frame, actual.Frame);
                    Assert.AreEqual(expectedDiagnostics[i], actual, "diagnostic at " + record.Frame.Value);
                }
            }

            RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);
            Assert.IsTrue(playback.Success, playback.FailureMessage);
        }

        private static RecordedReplay RecordThirtyDegreeMotionReplay()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreateFormalSlopeRuntime("replay-mutation-source");
            using (GameSlice source = CreateSlice(physics))
            {
                CharacterTestCharacterEntity entity = PrepareThirtyDegreeCharacter(source, physics);
                CharacterTestNetworkSnapshotParticipant participant =
                    source.Services.Network.CreateSnapshotParticipant();
                var recorder = new CharacterTestReplayRecorder(CreateReplayConfig());
                recorder.Begin(source.CurrentFrame, participant.CaptureSnapshot(source.CurrentFrame));
                for (int i = 0; i < MotionSequenceTicks; i++)
                {
                    RecordReplayFrame(
                        source,
                        participant,
                        recorder,
                        CreateSlopeCommand(
                            source.CurrentFrame,
                            entity.Id.Value,
                            ResolveWorldMoveXMilli(i),
                            new RuntimeTraceIdentity(1201713, sequence: i)));
                }

                CharacterTestReplayCapture capture = recorder.CreateCapture(
                    () => CreateSlice(CharacterTestNetworkSessionRunner.CloneReplayPhysicsRuntime(physics)));
                return new RecordedReplay(capture, entity.Id.Value);
            }
        }

        private static CharacterTestCharacterPhysicsRuntime CreateFormalSlopeRuntime(
            string sourceDescription,
            bool reverseRegistration = false)
        {
            var world = new CombatPhysicsWorld();
            if (reverseRegistration)
            {
                AddFormalSlope50(world);
                AddFormalSlope30(world);
                AddFormalSlope15(world);
            }
            else
            {
                AddFormalSlope15(world);
                AddFormalSlope30(world);
                AddFormalSlope50(world);
            }

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(SlopeLayer),
                R(450000, 900000, 450000),
                Fix64.FromRaw(10000),
                Fix64.FromRaw(350000),
                staticColliderCount: 3,
                sourceDescription: sourceDescription,
                groundSnapDistance: Fix64.FromRaw(100000));
        }

        private static void AddFormalSlope15(CombatPhysicsWorld world)
        {
            AddSlope(
                world,
                Slope15BodyId,
                R(-22500000, 534976, -7000000),
                R(258819, 965926, 0),
                R(965926, -258819, 0));
        }

        private static void AddFormalSlope30(CombatPhysicsWorld world)
        {
            AddSlope(
                world,
                Slope30BodyId,
                R(-22500000, 1283494, 15000000),
                R(500000, 866025, 0),
                R(866025, -500000, 0));
        }

        private static void AddFormalSlope50(CombatPhysicsWorld world)
        {
            AddSlope(
                world,
                Slope50BodyId,
                R(-23200001, 2137436, 18500000),
                R(766045, 642788, 0),
                R(642788, -766044, 0));
        }

        private static void AddSlope(
            CombatPhysicsWorld world,
            int bodyIdValue,
            FixVector3 center,
            FixVector3 axisY,
            FixVector3 axisZ)
        {
            var bodyId = new CombatBodyId(bodyIdValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(bodyIdValue),
                bodyId,
                center));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(SlopeColliderId),
                SlopeLayer,
                FixVector3.Zero,
                R(1250000, 250000, 3000000),
                R(0, 0, -1000000),
                axisY,
                axisZ,
                CombatSurfaceMaterial.Rough,
                priority: 0));
        }

        private static void AssertFormalClone(
            CharacterTestCharacterPhysicsRuntime source,
            CharacterTestCharacterPhysicsRuntime clone,
            CombatBodyId excludedDynamicBody)
        {
            Assert.IsNotNull(clone);
            Assert.AreNotSame(source, clone);
            Assert.AreNotSame(source.World, clone.World);
            Assert.AreEqual(source.ObstacleLayerMask, clone.ObstacleLayerMask);
            Assert.AreEqual(source.MotionObstacleLayerMask, clone.MotionObstacleLayerMask);
            Assert.AreEqual(source.CharacterHalfExtents, clone.CharacterHalfExtents);
            Assert.AreEqual(source.SkinWidth, clone.SkinWidth);
            Assert.AreEqual(source.MaxStepHeight, clone.MaxStepHeight);
            Assert.AreEqual(Fix64.FromRaw(100000), clone.GroundSnapDistance);
            Assert.AreEqual(3, clone.StaticColliderCount);
            Assert.AreEqual(3, clone.World.TotalColliderCount);
            Assert.AreEqual(source.SourceDescription, clone.SourceDescription);
            Assert.IsFalse(clone.World.TryGetBody(excludedDynamicBody, out _));

            var ordered = new List<CombatPhysicsObbCollider>();
            clone.World.CopyObbCollidersTo(ordered);
            Assert.AreEqual(FormalSlopeBodyIds.Length, ordered.Count);
            for (int i = 0; i < FormalSlopeBodyIds.Length; i++)
            {
                var bodyId = new CombatBodyId(FormalSlopeBodyIds[i]);
                var colliderId = new CombatColliderId(SlopeColliderId);
                Assert.AreEqual(bodyId, ordered[i].BodyId);
                Assert.IsTrue(source.World.TryGetBody(bodyId, out CombatPhysicsBody expectedBody));
                Assert.IsTrue(clone.World.TryGetBody(bodyId, out CombatPhysicsBody actualBody));
                Assert.AreEqual(expectedBody, actualBody);
                Assert.IsTrue(source.World.TryGetObbCollider(
                    bodyId,
                    colliderId,
                    out CombatPhysicsObbCollider expectedCollider));
                Assert.IsTrue(clone.World.TryGetObbCollider(
                    bodyId,
                    colliderId,
                    out CombatPhysicsObbCollider actualCollider));
                Assert.AreEqual(expectedCollider, actualCollider);
            }
        }

        private static void AssertFormalPhysicsOwner(CharacterTestCharacterPhysicsRuntime runtime)
        {
            Assert.IsNotNull(runtime);
            Assert.AreEqual(Fix64.FromRaw(100000), runtime.GroundSnapDistance);
            for (int i = 0; i < FormalSlopeBodyIds.Length; i++)
            {
                Assert.IsTrue(runtime.World.TryGetObbCollider(
                    new CombatBodyId(FormalSlopeBodyIds[i]),
                    new CombatColliderId(SlopeColliderId),
                    out CombatPhysicsObbCollider collider));
                Assert.AreEqual(SlopeLayer, collider.Layer);
                Assert.AreEqual(CombatSurfaceMaterial.Rough, collider.SurfaceMaterial);
            }
        }

        private static NetworkSessionConfig CreateReplayConfig(
            NetworkStateSyncPolicy stateSyncPolicy = NetworkStateSyncPolicy.BootstrapOnly,
            int snapshotIntervalFrames = 2)
        {
            return new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: TickRate,
                inputDelayFrames: 2,
                snapshotIntervalFrames: snapshotIntervalFrames,
                configHash: "slope-t4",
                resourceCatalogHash: "slope-t4-resources",
                stateSyncPolicy: stateSyncPolicy);
        }

        private static CharacterTestCharacterEntity FindPlayer(GameSlice slice)
        {
            foreach (CharacterTestCharacterEntity entity in slice.CharacterEntities)
            {
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            Assert.Fail("Expected a local CharacterTest player.");
            return null;
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / TickRate);
        }

        private static void Tick(CharacterTestNetworkLoopbackRunner runner, int count)
        {
            for (int i = 0; i < count; i++)
                runner.Tick(1d / TickRate);
        }

        private static void AssertMirrorMotionState(
            CharacterTestCharacterEntity expected,
            CharacterTestCharacterEntity actual)
        {
            Assert.AreEqual(expected.Transform.XMillimeters, actual.Transform.XMillimeters);
            Assert.AreEqual(expected.Transform.YMillimeters, actual.Transform.YMillimeters);
            Assert.AreEqual(expected.Transform.ZMillimeters, actual.Transform.ZMillimeters);
            Assert.AreEqual(expected.Transform.YawMilliDegrees, actual.Transform.YawMilliDegrees);
            Assert.AreEqual(expected.VerticalVelocityMillimetersPerSecond, actual.VerticalVelocityMillimetersPerSecond);
            Assert.AreEqual(
                expected.HorizontalVelocityXMillimetersPerSecond,
                actual.HorizontalVelocityXMillimetersPerSecond);
            Assert.AreEqual(
                expected.HorizontalVelocityYMillimetersPerSecond,
                actual.HorizontalVelocityYMillimetersPerSecond);
            Assert.AreEqual(expected.IsGrounded, actual.IsGrounded);
            Assert.AreEqual(expected.CollisionFlags, actual.CollisionFlags);
            Assert.AreEqual(expected.SurfaceMaterial, actual.SurfaceMaterial);
            Assert.AreEqual(
                expected.SurfaceBounceSpeedCapYMillimetersPerSecond,
                actual.SurfaceBounceSpeedCapYMillimetersPerSecond);
        }

        private static long ComputeSlopeMotionAuthorityHash(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity)
        {
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            hash.AddLong(1, frame.Value);
            hash.AddInt(2, entity.Id.Value);
            hash.AddInt(3, entity.Transform.XMillimeters);
            hash.AddInt(4, entity.Transform.YMillimeters);
            hash.AddInt(5, entity.Transform.ZMillimeters);
            hash.AddInt(6, entity.Transform.YawMilliDegrees);
            hash.AddInt(7, entity.VerticalVelocityMillimetersPerSecond);
            hash.AddInt(8, entity.HorizontalVelocityXMillimetersPerSecond);
            hash.AddInt(9, entity.HorizontalVelocityYMillimetersPerSecond);
            hash.AddInt(10, entity.DesiredVelocityXMillimetersPerSecond);
            hash.AddInt(11, entity.DesiredVelocityYMillimetersPerSecond);
            hash.AddInt(12, entity.IsGrounded ? 1 : 0);
            hash.AddInt(13, (int)entity.CollisionFlags);
            hash.AddInt(14, (int)entity.SurfaceMaterial.Kind);
            hash.AddInt(15, (int)entity.SurfaceMaterial.Kind);
            hash.AddLong(16, entity.SurfaceMaterial.Friction.RawValue);
            hash.AddLong(17, entity.SurfaceMaterial.Elasticity.RawValue);
            hash.AddLong(
                18,
                entity.SurfaceMaterial.FixedBounceHeight.RawValue);
            hash.AddInt(
                19,
                entity.SurfaceBounceSpeedCapYMillimetersPerSecond);
            return hash.ToHash();
        }

        private static void AssertSamplesEqual(AuthoritySample[] expected, AuthoritySample[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                AssertSampleEqual(expected[i], actual[i], "motion tick " + i);
        }

        private static void AssertSampleEqual(
            AuthoritySample expected,
            AuthoritySample actual,
            string context)
        {
            Assert.AreEqual(expected.Frame, actual.Frame, context + " frame");
            Assert.AreEqual(expected.XMillimeters, actual.XMillimeters, context + " X");
            Assert.AreEqual(expected.YMillimeters, actual.YMillimeters, context + " Y");
            Assert.AreEqual(expected.ZMillimeters, actual.ZMillimeters, context + " Z");
            Assert.AreEqual(expected.YawMilliDegrees, actual.YawMilliDegrees, context + " yaw");
            Assert.AreEqual(expected.VerticalVelocity, actual.VerticalVelocity, context + " vertical velocity");
            Assert.AreEqual(expected.HorizontalVelocityX, actual.HorizontalVelocityX, context + " horizontal X");
            Assert.AreEqual(expected.HorizontalVelocityY, actual.HorizontalVelocityY, context + " horizontal Y");
            Assert.AreEqual(expected.DesiredVelocityX, actual.DesiredVelocityX, context + " desired X");
            Assert.AreEqual(expected.DesiredVelocityY, actual.DesiredVelocityY, context + " desired Y");
            Assert.AreEqual(expected.Grounded, actual.Grounded, context + " grounded");
            Assert.AreEqual(expected.CollisionFlags, actual.CollisionFlags, context + " flags");
            Assert.AreEqual(expected.SurfaceMaterial, actual.SurfaceMaterial, context + " material");
            Assert.AreEqual(expected.SurfaceBounceSpeedCapY, actual.SurfaceBounceSpeedCapY, context + " bounce cap");
            Assert.AreEqual(expected.StateHash, actual.StateHash, context + " hash");
            Assert.AreEqual(expected.Diagnostic, actual.Diagnostic, context + " diagnostic");
        }

        private static FixVector3 R(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }

        private readonly struct AuthoritySample
        {
            public AuthoritySample(
                RuntimeFrame frame,
                CharacterTestCharacterEntity entity,
                long stateHash,
                CharacterTestCharacterMotionDiagnostic diagnostic)
            {
                Frame = frame;
                XMillimeters = entity.Transform.XMillimeters;
                YMillimeters = entity.Transform.YMillimeters;
                ZMillimeters = entity.Transform.ZMillimeters;
                YawMilliDegrees = entity.Transform.YawMilliDegrees;
                VerticalVelocity = entity.VerticalVelocityMillimetersPerSecond;
                HorizontalVelocityX = entity.HorizontalVelocityXMillimetersPerSecond;
                HorizontalVelocityY = entity.HorizontalVelocityYMillimetersPerSecond;
                DesiredVelocityX = entity.DesiredVelocityXMillimetersPerSecond;
                DesiredVelocityY = entity.DesiredVelocityYMillimetersPerSecond;
                Grounded = entity.IsGrounded;
                CollisionFlags = entity.CollisionFlags;
                SurfaceMaterial = entity.SurfaceMaterial;
                SurfaceBounceSpeedCapY = entity.SurfaceBounceSpeedCapYMillimetersPerSecond;
                StateHash = stateHash;
                Diagnostic = diagnostic;
            }

            public RuntimeFrame Frame { get; }
            public int XMillimeters { get; }
            public int YMillimeters { get; }
            public int ZMillimeters { get; }
            public int YawMilliDegrees { get; }
            public int VerticalVelocity { get; }
            public int HorizontalVelocityX { get; }
            public int HorizontalVelocityY { get; }
            public int DesiredVelocityX { get; }
            public int DesiredVelocityY { get; }
            public bool Grounded { get; }
            public CombatMotionCollisionFlags CollisionFlags { get; }
            public CombatSurfaceMaterial SurfaceMaterial { get; }
            public int SurfaceBounceSpeedCapY { get; }
            public long StateHash { get; }
            public CharacterTestCharacterMotionDiagnostic Diagnostic { get; }
        }

        private readonly struct RecordedReplay
        {
            public RecordedReplay(CharacterTestReplayCapture capture, int entityId)
            {
                Capture = capture;
                EntityId = entityId;
            }

            public CharacterTestReplayCapture Capture { get; }
            public int EntityId { get; }
        }
    }
}
