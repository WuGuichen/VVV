using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Network.UnityTransport;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestNetworkLoopbackTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyObjectNamed("MxFramework Default Input Service");
        }

        [Test]
        public void RemoteClient_ExhaustedProductionHistoryStopsAuthorityWithoutAutomaticReconnect()
        {
            var transport = new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate);
            var config = new NetworkSessionConfig(1, 60, 2, 1, "character-test", "character-test-resources", NetworkStateSyncPolicy.BootstrapOnly);
            const string sessionId = "character-test-history-exhaustion";
            using (var server = new CharacterTestUnityTransportServerRunner(transport, NullRuntimeLogger.Instance,
                config: config, sessionId: sessionId, createServerViews: false, disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(transport, NullRuntimeLogger.Instance,
                config: config, sessionId: sessionId, createClientViews: false, disposeTransport: false))
            {
                Tick(server, client, 180);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                var field = typeof(CharacterTestUnityTransportClientRunner).GetField("_rollbackCoordinator",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(field);
                var history = (NetworkPredictionRollbackCoordinator)field.GetValue(client);
                Assert.AreEqual(NetworkPredictionRollbackCoordinator.DefaultHistoryCapacity, history.HistoryCount,
                    "The actual GameSlice must have filled and wrapped its production history.");
                int owned = client.CaptureDiagnostics().OwnedEntityId;
                Assert.Greater(owned, 0);
                var command = CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(RuntimeFrame.Zero,
                    CharacterTestRuntimeCommandSources.CameraInput, owned, owned, Vector2.right);
                RuntimeFrame before = client.Slice.CurrentFrame;
                int pending = client.Slice.Services.Modules.RuntimeCommands.CommandBuffer.PendingCount;
                transport.Send(new NetworkMessageEnvelope(sessionId, "server", "client-0", 1000000,
                    NetworkChannel.ReliableOrdered, NetworkMessageKind.AuthoritativeCommands, server.Slice.CurrentFrame,
                    new NetworkAuthoritativeCommandPacket("other-peer", server.Slice.CurrentFrame, RuntimeFrame.Zero, new[] { command })));
                Tick(client, 5);
                Assert.Greater(history.Diagnostics.RollbackFailureCount, 0);
                StringAssert.Contains("rollback history missing", client.Session.RecoveryReason);
                Assert.AreEqual(NetworkSessionStatus.RecoveryFailed, client.Session.Status);
                Assert.AreEqual(before, client.Slice.CurrentFrame);
                Assert.AreEqual(pending, client.Slice.Services.Modules.RuntimeCommands.CommandBuffer.PendingCount);
                Assert.AreEqual(0, client.Session.SnapshotRequestSentCount);
            }
        }

        [Test]
        public void LoopbackCommands_ExecuteOnServerAndClientMirrorConvergesFromSnapshot()
        {
            using (var runner = new CharacterTestNetworkLoopbackRunner(
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                transportOptions: LoopbackNetworkTransportOptions.ReliableImmediate,
                createServerViews: false))
            {
                Tick(runner, 20);

                Assert.AreEqual(NetworkSessionStatus.Connected, runner.ClientSession.Status);
                Assert.GreaterOrEqual(runner.ServerSlice.CharacterEntities.Count, 2,
                    "Runtime errors: " + string.Join(" | ", System.Linq.Enumerable.Select(runner.ServerSlice.CaptureDiagnostics().Errors, error => error.ModuleId + ": " + error.Message + " " + error.Exception.StackTrace))
                    + " Events: " + string.Join(" | ", runner.ServerSlice.RecentRuntimeEvents) + " Story: " + JsonConvert.SerializeObject(runner.ServerSlice.StorySnapshot) + " Resource: " + runner.ServerSlice.Services.Modules.Resources.BootstrapErrorMessage);
                Assert.AreEqual(
                    runner.ServerSlice.CharacterEntities.Count,
                    runner.ClientSlice.CharacterEntities.Count);

                int primaryEntityId = runner.ServerSlice.CharacterEntities[0].Id.Value;
                RuntimeCommandValidationResult camera = runner.ServerSlice.EnqueueSetCameraMode(
                    CharacterTestCameraMode.MultiTargetAuto);
                RuntimeCommandValidationResult targetLock = runner.ServerSlice.EnqueueToggleTargetLock(primaryEntityId);

                Assert.IsTrue(camera.Success, camera.Error.Message);
                Assert.IsTrue(targetLock.Success, targetLock.Error.Message);
                Assert.AreEqual(CharacterTestCameraMode.ThirdPersonControlled, runner.ServerSlice.CameraMode);
                Assert.AreEqual(CharacterTestCameraMode.ThirdPersonControlled, runner.ClientSlice.CameraMode);

                // Force a known divergence so full-snapshot recovery is exercised deterministically.
                CharacterTestCharacterEntity divergedClient = FindNonLocal(runner.ClientSlice);
                divergedClient.SetTransform(new CharacterTestCharacterTransformComponent(
                    divergedClient.Transform.X + 3f,
                    divergedClient.Transform.Y,
                    divergedClient.Transform.Z,
                    divergedClient.Transform.YawDegrees));

                Tick(runner, 24);

                Assert.AreEqual(CharacterTestCameraMode.MultiTargetAuto, runner.ServerSlice.CameraMode);
                Assert.AreEqual(runner.ServerSlice.CameraMode, runner.ClientSlice.CameraMode);
                Assert.AreEqual(runner.ServerSlice.TargetLockEnabled, runner.ClientSlice.TargetLockEnabled);
                Assert.AreEqual(runner.ServerSlice.LockedTargetEntityId, runner.ClientSlice.LockedTargetEntityId);
                Assert.Greater(runner.ClientSession.ResyncCount, 0);
                CharacterTestNetworkLoopbackDiagnostics diagnostics = runner.CaptureDiagnostics();
                Assert.GreaterOrEqual(diagnostics.FullSnapshotReceivedCount, 2);
                Assert.Greater(diagnostics.DigestSnapshotReceivedCount, 0);
                Assert.Greater(diagnostics.SnapshotRequestCount, 0);
                Assert.AreEqual(FindNonLocal(runner.ServerSlice).Transform.X,
                    FindNonLocal(runner.ClientSlice).Transform.X, 0.001f,
                    "The requested full snapshot must repair the injected client position.");
            }
        }

        [Test]
        public void LoopbackTargetLockViewSwitchCommand_ConvergesFromSnapshot()
        {
            using (var runner = new CharacterTestNetworkLoopbackRunner(
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                transportOptions: LoopbackNetworkTransportOptions.ReliableImmediate,
                createServerViews: false))
            {
                Tick(runner, 20);

                Assert.AreEqual(NetworkSessionStatus.Connected, runner.ClientSession.Status);
                CharacterTestCharacterEntity primary = FindLocalPlayer(runner.ServerSlice);
                RuntimeCommandValidationResult targetLock =
                    runner.ServerSlice.EnqueueToggleTargetLock(primary.Id.Value);

                Assert.IsTrue(targetLock.Success, targetLock.Error.Message);
                Tick(runner, 24);

                Assert.IsTrue(runner.ServerSlice.TargetLockEnabled);
                int initialTargetId = runner.ServerSlice.LockedTargetEntityId;
                CharacterTestCharacterEntity switchTarget =
                    FindOtherHostileTarget(runner.ServerSlice, primary, initialTargetId);
                RuntimeCommandValidationResult viewSwitch = runner.ServerSlice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                        runner.ServerSlice.CurrentFrame,
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        switchTarget.Id.Value,
                        Vector2.right,
                        new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0028)));

                Assert.IsTrue(viewSwitch.Success, viewSwitch.Error.Message);
                Tick(runner, 24);

                Assert.IsTrue(runner.ServerSlice.TargetLockEnabled);
                Assert.AreEqual(switchTarget.Id.Value, runner.ServerSlice.LockedTargetEntityId);
                Assert.AreEqual(runner.ServerSlice.TargetLockEnabled, runner.ClientSlice.TargetLockEnabled);
                Assert.AreEqual(runner.ServerSlice.LockedTargetEntityId, runner.ClientSlice.LockedTargetEntityId);
                if (runner.ServerSlice.LockedCombatTarget.HasTarget)
                {
                    Assert.IsTrue(runner.ClientSlice.LockedCombatTarget.HasTarget);
                    Assert.AreEqual(
                        runner.ServerSlice.LockedCombatTarget.TargetGameplayEntityId,
                        runner.ClientSlice.LockedCombatTarget.TargetGameplayEntityId);
                    Assert.AreEqual(
                        runner.ServerSlice.LockedCombatTarget.LogicalHurtboxId,
                        runner.ClientSlice.LockedCombatTarget.LogicalHurtboxId);
                }
            }
        }

        [Test]
        public void DeterministicUnits_QuantizeAuthorityStateToNamedIntegerScales()
        {
            var transform = new CharacterTestCharacterTransformComponent(
                1.234f,
                -2.345f,
                0.004f,
                yawDegrees: -90f);
            var input = new CharacterTestCharacterInputComponent(
                new Vector2(0.25f, -0.75f),
                jumpPressed: true,
                worldMove: new Vector2(-0.5f, 0.125f),
                hasWorldMove: true);
            var physics = new CharacterTestCharacterPhysicsComponent(
                capsuleRadiusMeters: 0.345f,
                capsuleHeightMeters: 1.875f,
                capsuleCenterY: 0.937f,
                massKg: 81.25f,
                massScale: 1.25f,
                radiusScale: 0.5f,
                pushScale: 0.75f);

            Assert.AreEqual(1234, transform.XMillimeters);
            Assert.AreEqual(-2345, transform.YMillimeters);
            Assert.AreEqual(4, transform.ZMillimeters);
            Assert.AreEqual(270000, transform.YawMilliDegrees);
            Assert.AreEqual(
                359999,
                CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0, -1).YawMilliDegrees);
            Assert.AreEqual(
                0,
                CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0, 360000).YawMilliDegrees);
            Assert.AreEqual(250, input.MoveXMilli);
            Assert.AreEqual(-750, input.MoveYMilli);
            Assert.AreEqual(-500, input.WorldMoveXMilli);
            Assert.AreEqual(125, input.WorldMoveYMilli);
            Assert.AreEqual(345, physics.CapsuleRadiusMillimeters);
            Assert.AreEqual(1875, physics.CapsuleHeightMillimeters);
            Assert.AreEqual(937, physics.CapsuleCenterYMillimeters);
            Assert.AreEqual(81250, physics.MassGrams);
            Assert.AreEqual(1250, physics.MassScaleMilli);
            Assert.AreEqual(500, physics.RadiusScaleMilli);
            Assert.AreEqual(750, physics.PushScaleMilli);
        }

        [Test]
        public void SetControlIntentCommand_EncodesAndValidatesPayload()
        {
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                new RuntimeFrame(4),
                CharacterTestRuntimeCommandSources.PlayerInput,
                7,
                new Vector2(0.25f, -0.75f),
                jumpPressed: true,
                jumpHeld: true,
                jumpReleased: false,
                attackPrimaryPressed: true,
                attackSecondaryPressed: true,
                debugSecondaryPressed: true,
                debugPrimaryPressed: true,
                skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(6),
                dodgePressed: true,
                movementSpeedMultiplier: 0.5f,
                traceId: new RuntimeTraceIdentity(993379));

            CharacterTestControlIntent decoded = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());

            Assert.AreEqual(CharacterTestRuntimeCommandIds.SetControlIntent, command.CommandId);
            Assert.AreEqual(7, decoded.TargetEntityId);
            Assert.AreEqual(250, decoded.MoveXMilli);
            Assert.AreEqual(-750, decoded.MoveYMilli);
            Assert.AreEqual(250, decoded.WorldMoveXMilli);
            Assert.AreEqual(-750, decoded.WorldMoveYMilli);
            Assert.AreEqual(0.25f, decoded.Move.x, 0.001f);
            Assert.AreEqual(-0.75f, decoded.Move.y, 0.001f);
            Assert.AreEqual(0.25f, decoded.WorldMove.x, 0.001f);
            Assert.AreEqual(-0.75f, decoded.WorldMove.y, 0.001f);
            Assert.IsTrue(decoded.JumpPressed);
            Assert.IsTrue(decoded.JumpHeld);
            Assert.IsFalse(decoded.JumpReleased);
            Assert.IsTrue(decoded.AttackPrimaryPressed);
            Assert.IsTrue(decoded.AttackSecondaryPressed);
            Assert.IsTrue(decoded.DodgePressed);
            Assert.IsTrue(decoded.DebugPrimaryPressed);
            Assert.IsTrue(decoded.DebugSecondaryPressed);
            Assert.AreEqual(CharacterTestSkillSlotRuntime.ToMask(6), decoded.SkillSlotPressedMask);
            Assert.AreEqual(500, decoded.MovementSpeedScaleMilli);
            Assert.IsTrue(validator.Validate(command).Success);

            RuntimeCommand legacySchema = new RuntimeCommand(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.PlayerInput,
                CharacterTestRuntimeCommandIds.SetControlIntent,
                targetId: 7,
                payload0: command.Payload0,
                payload1: CharacterTestControlIntent.AttackPrimaryPressedFlag
                    | CharacterTestControlIntent.AttackSecondaryPressedFlag,
                payload2: CharacterTestControlIntent.LegacySchemaVersion);
            CharacterTestControlIntent legacyDecoded =
                CharacterTestRuntimeCommandFactory.DecodeControlIntent(legacySchema);
            Assert.IsTrue(validator.Validate(legacySchema).Success);
            Assert.IsTrue(legacyDecoded.AttackPrimaryPressed);
            Assert.IsFalse(legacyDecoded.AttackSecondaryPressed);
            Assert.IsFalse(legacyDecoded.DebugPrimaryPressed);
            Assert.IsFalse(legacyDecoded.DebugSecondaryPressed);
            Assert.IsFalse(legacyDecoded.DodgePressed);
            Assert.AreEqual(0, legacyDecoded.SkillSlotPressedMask);
            Assert.AreEqual(
                CharacterTestControlIntent.DefaultMovementSpeedScaleMilli,
                legacyDecoded.MovementSpeedScaleMilli);

            RuntimeCommand invalidTarget = new RuntimeCommand(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.PlayerInput,
                CharacterTestRuntimeCommandIds.SetControlIntent,
                targetId: 0,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2);
            RuntimeCommand invalidSchema = new RuntimeCommand(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.PlayerInput,
                CharacterTestRuntimeCommandIds.SetControlIntent,
                targetId: 7,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: (CharacterTestControlIntent.SchemaVersion + 1) << 16);

            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, validator.Validate(invalidTarget).Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, validator.Validate(invalidSchema).Error.Code);
        }

        [Test]
        public void TurnInPlaceCommand_IsQuantizedZeroMoveAndEnvelopeSchemaBound()
        {
            var intent = new CharacterTestControlIntent(
                targetEntityId: 7,
                move: Vector2.right,
                worldMove: Vector2.up,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: true,
                turnInPlace: true,
                turnInPlaceYawMilliDegrees: 450123);
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                new RuntimeFrame(4),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                intent,
                traceId: new RuntimeTraceIdentity(935295));
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            CharacterTestControlIntent decoded = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);

            Assert.IsTrue(validator.Validate(command).Success);
            Assert.IsTrue(decoded.TurnInPlace);
            Assert.AreEqual(
                CharacterTestDeterministicUnits.NormalizeMilliDegrees(450123),
                decoded.TurnInPlaceYawMilliDegrees);
            Assert.IsFalse(decoded.AttackPrimaryPressed);
            Assert.AreEqual(Vector2.zero, decoded.Move);
            Assert.AreEqual(Vector2.zero, decoded.WorldMove);

            int legacyMetadata = CharacterTestRuntimeCommandPayloadCodec.PackControlIntentMetadata(
                CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentFlags(command.Payload2),
                CharacterTestControlIntent.MovementSpeedScaleSchemaVersion);
            RuntimeCommand legacyPayloadWithEnvelope = new RuntimeCommand(
                command.Frame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                legacyMetadata,
                command.TraceId,
                command.Sequence,
                command.Envelope);
            Assert.IsFalse(validator.Validate(legacyPayloadWithEnvelope).Success);

            CharacterTestCharacterInputComponent input = CharacterTestCharacterInputComponent.FromDeterministic(
                1000,
                0,
                0,
                1000,
                hasWorldMove: true,
                jumpPressed: false,
                turnInPlace: true,
                turnInPlaceYawMilliDegrees: 450123,
                controlIntentSourceId: CharacterTestRuntimeCommandSources.RuntimeAiPlanner);
            Assert.IsFalse(input.HasActiveMove);
            Assert.AreEqual(0, input.MoveXMilli);
            Assert.AreEqual(0, input.WorldMoveYMilli);
            Assert.AreEqual(
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                input.ControlIntentSourceId);
        }

        [Test]
        public void NetworkSnapshotV1_WritesIntegerAuthorityFieldsWithoutLegacyFloats()
        {
            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(slice, 8);
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);

                RuntimeCustomState snapshot = participant.CaptureSnapshot(slice.CurrentFrame);
                JObject document = JObject.Parse(snapshot.PayloadJson);
                JArray characters = (JArray)document["characters"];
                Assert.NotNull(characters);
                Assert.Greater(characters.Count, 0);
                JObject character = (JObject)characters[0];

                Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue, snapshot.SchemaVersion);
                Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue, (int)document["schemaVersion"]);
                Assert.IsNotNull(character["xMillimeters"]);
                Assert.IsNotNull(character["yawMilliDegrees"]);
                Assert.IsNotNull(character["inputMoveXMilli"]);
                Assert.IsNotNull(character["physicsCapsuleRadiusMillimeters"]);
                Assert.IsNull(character["x"]);
                Assert.IsNull(character["yawDegrees"]);
                Assert.IsNull(character["inputMoveX"]);
                Assert.IsNull(character["physicsCapsuleRadiusMeters"]);
            }
        }

        [Test]
        public void NetworkSnapshotLegacySchema0_NormalizesFloatFieldsToIntegerFields()
        {
            var snapshot = new CharacterTestNetworkCharacterSnapshot
            {
                LegacyX = 1.25f,
                LegacyY = 0.5f,
                LegacyZ = -2.75f,
                LegacyYawDegrees = -45f,
                LegacyInputMoveX = 0.5f,
                LegacyInputMoveY = -0.25f,
                LegacyInputWorldMoveX = -0.75f,
                LegacyInputWorldMoveY = 1f,
                LegacyPhysicsCapsuleRadiusMeters = 0.34f,
                LegacyPhysicsCapsuleHeightMeters = 1.86f,
                LegacyPhysicsCapsuleCenterY = 0.93f,
                LegacyPhysicsMassKg = 80f,
                LegacyPhysicsMassScale = 1.4f,
                LegacyPhysicsRadiusScale = 1.2f,
                LegacyPhysicsPushScale = 0.75f,
                PhysicsBlocksCharacters = true,
                PhysicsCanBePushed = true
            };

            snapshot.NormalizeForSchema(CharacterTestNetworkSnapshotParticipant.LegacySchemaVersionValue);
            CharacterTestCharacterTransformComponent transform = snapshot.CreateTransformComponent();
            CharacterTestCharacterInputComponent input = snapshot.CreateInputComponent();
            CharacterTestCharacterPhysicsComponent physics = snapshot.CreatePhysicsComponent(
                CharacterTestCharacterPhysicsComponent.Default);

            Assert.AreEqual(1250, snapshot.XMillimeters);
            Assert.AreEqual(500, snapshot.YMillimeters);
            Assert.AreEqual(-2750, snapshot.ZMillimeters);
            Assert.AreEqual(315000, snapshot.YawMilliDegrees);
            Assert.AreEqual(0.5f, transform.Y, 0.001f);
            Assert.AreEqual(500, input.MoveXMilli);
            Assert.AreEqual(-250, input.MoveYMilli);
            Assert.AreEqual(-750, input.WorldMoveXMilli);
            Assert.AreEqual(1000, input.WorldMoveYMilli);
            Assert.AreEqual(340, physics.CapsuleRadiusMillimeters);
            Assert.AreEqual(80000, physics.MassGrams);
            Assert.AreEqual(1400, physics.MassScaleMilli);
            Assert.AreEqual(1200, physics.RadiusScaleMilli);
            Assert.AreEqual(750, physics.PushScaleMilli);
        }

        [Test]
        public void LocalInputProvider_SubmitsControlIntentCommandAndAppliesSameFrame()
        {
            var provider = new FakeInputProvider();
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                inputProvider: provider))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity localPlayer = FindLocalPlayer(slice);
                provider.SetSnapshot(CreateInputSnapshot(
                    new Vector2(0.5f, -0.25f),
                    jumpPressed: true,
                    jumpHeld: true,
                    attackPrimaryPressed: true,
                    attackSecondaryPressed: true,
                    debugPrimaryPressed: true,
                    debugSecondaryPressed: true));

                slice.Tick(1d / 60d);

                AssertDrainedCommand(slice, CharacterTestRuntimeCommandIds.SetControlIntent);
                Assert.AreEqual(0.5f, localPlayer.Input.Move.x, 0.001f);
                Assert.AreEqual(-0.25f, localPlayer.Input.Move.y, 0.001f);
                Assert.IsTrue(localPlayer.Input.JumpPressed);
                Assert.IsTrue(localPlayer.Input.JumpHeld);
                Assert.IsFalse(localPlayer.Input.JumpReleased);
                Assert.IsTrue(localPlayer.Input.AttackPrimaryPressed);
                Assert.IsTrue(localPlayer.Input.AttackSecondaryPressed);
                Assert.IsTrue(localPlayer.Input.DebugPrimaryPressed);
                Assert.IsTrue(localPlayer.Input.DebugSecondaryPressed);
            }
        }

        [Test]
        public void LocalInputCommandFailure_LogsRepeatedReasonOnce()
        {
            var provider = new FakeInputProvider();
            var logger = new CountingRuntimeLogger();
            using (var slice = new GameSlice(
                logger,
                createCharacterViews: false,
                inputProvider: provider))
            {
                slice.SetRuntimeCommandSubmitter(command =>
                    RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.InvalidPayload,
                        command,
                        slice.CurrentFrame,
                        "forced failure")));

                Tick(slice, 8);

                Assert.AreEqual(1, logger.LocalInputWarningCount);
            }
        }

        [Test]
        public void NetworkSyncProbe_RejectsOfflineStart()
        {
            var probe = new CharacterTestNetworkSyncProbe();

            CharacterTestNetworkSyncProbeStatus status = probe.Start(
                null,
                CharacterTestNetworkLoopbackDiagnostics.Unavailable,
                CharacterTestNetworkSessionStatus.Offline());

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Rejected, status.Result);
            StringAssert.Contains("rejected", status.Summary);
        }

        [Test]
        public void NetworkSyncProbe_EnqueuesProbeCommandThroughGameSliceSubmitter()
        {
            var probe = new CharacterTestNetworkSyncProbe();
            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                RuntimeCommand captured = default;
                int submitCount = 0;
                slice.SetRuntimeCommandSubmitter(command =>
                {
                    submitCount++;
                    captured = command;
                    return RuntimeCommandValidationResult.Accepted(command);
                });
                CharacterTestNetworkLoopbackDiagnostics diagnostics =
                    CreateSyncProbeDiagnostics(ownedEntityId: 7);
                var sessionStatus = new CharacterTestNetworkSessionStatus
                {
                    Kind = CharacterTestNetworkSessionKind.Join,
                    LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                    NetworkMode = CharacterTestNetworkMode.UnityTransportClient,
                    ClientStatus = NetworkSessionStatus.Connected,
                    OwnedEntityId = 7,
                    FullSnapshotReceivedCount = 1,
                    CanEnterGameplay = true
                };

                CharacterTestNetworkSyncProbeStatus start =
                    probe.Start(slice, diagnostics, sessionStatus);
                for (int i = 0; i < CharacterTestNetworkSyncProbe.DefaultWarmupFrames; i++)
                    probe.Tick(1d / 60d, slice, diagnostics, NullRuntimeLogger.Instance);

                Assert.AreEqual(0, submitCount);

                probe.Tick(1d / 60d, slice, diagnostics, NullRuntimeLogger.Instance);

                Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Running, start.Result);
                Assert.AreEqual(1, submitCount);
                Assert.AreEqual(CharacterTestRuntimeCommandIds.SetControlIntent, captured.CommandId);
                Assert.AreEqual(7, captured.TargetId);
                Assert.AreEqual(CharacterTestRuntimeCommandSources.PlayerInput, captured.SourceId);
                Assert.AreEqual(CharacterTestNetworkSyncProbe.TraceId, captured.TraceId);
            }
        }

        [Test]
        public void NetworkSyncProbe_MoveStopLoopCreatesMoveAndStopCommands()
        {
            CharacterTestControlIntent forward = CharacterTestRuntimeCommandFactory.DecodeControlIntent(
                CharacterTestNetworkSyncProbe.CreateProbeCommand(new RuntimeFrame(1), 7, 1));
            CharacterTestControlIntent stopAfterForward = CharacterTestRuntimeCommandFactory.DecodeControlIntent(
                CharacterTestNetworkSyncProbe.CreateProbeCommand(new RuntimeFrame(65), 7, 65));
            CharacterTestControlIntent backward = CharacterTestRuntimeCommandFactory.DecodeControlIntent(
                CharacterTestNetworkSyncProbe.CreateProbeCommand(new RuntimeFrame(125), 7, 125));
            CharacterTestControlIntent stopAfterBackward = CharacterTestRuntimeCommandFactory.DecodeControlIntent(
                CharacterTestNetworkSyncProbe.CreateProbeCommand(new RuntimeFrame(190), 7, 190));

            Assert.Greater(forward.Move.y, 0.5f);
            Assert.AreEqual(0f, stopAfterForward.Move.sqrMagnitude, 0.0001f);
            Assert.Less(backward.Move.y, -0.5f);
            Assert.AreEqual(0f, stopAfterBackward.Move.sqrMagnitude, 0.0001f);
        }

        [Test]
        public void NetworkSyncProbe_MoveStopLoopCreatesFixedInputSnapshots()
        {
            InputSnapshot forward = CharacterTestNetworkSyncProbe.CreateInputSnapshot(
                CharacterTestNetworkSyncProbe.CreateProbeInputSample(
                    1,
                    CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop));
            InputSnapshot stopAfterForward = CharacterTestNetworkSyncProbe.CreateInputSnapshot(
                CharacterTestNetworkSyncProbe.CreateProbeInputSample(
                    65,
                    CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop));
            InputSnapshot backward = CharacterTestNetworkSyncProbe.CreateInputSnapshot(
                CharacterTestNetworkSyncProbe.CreateProbeInputSample(
                    125,
                    CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop));
            InputSnapshot stopAfterBackward = CharacterTestNetworkSyncProbe.CreateInputSnapshot(
                CharacterTestNetworkSyncProbe.CreateProbeInputSample(
                    190,
                    CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop));

            Assert.Greater(forward.Move.y, 0.5f);
            Assert.AreEqual(0f, stopAfterForward.Move.sqrMagnitude, 0.0001f);
            Assert.Less(backward.Move.y, -0.5f);
            Assert.AreEqual(0f, stopAfterBackward.Move.sqrMagnitude, 0.0001f);
        }

        [Test]
        public void NetworkSyncProbe_ProviderModeDoesNotSubmitDuplicateCommands()
        {
            var probe = new CharacterTestNetworkSyncProbe();
            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                int submitCount = 0;
                slice.SetRuntimeCommandSubmitter(command =>
                {
                    submitCount++;
                    return RuntimeCommandValidationResult.Accepted(command);
                });
                CharacterTestNetworkLoopbackDiagnostics diagnostics =
                    CreateSyncProbeDiagnostics(ownedEntityId: 7);
                var sessionStatus = new CharacterTestNetworkSessionStatus
                {
                    Kind = CharacterTestNetworkSessionKind.Join,
                    LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                    NetworkMode = CharacterTestNetworkMode.UnityTransportClient,
                    ClientStatus = NetworkSessionStatus.Connected,
                    OwnedEntityId = 7,
                    FullSnapshotReceivedCount = 1,
                    CanEnterGameplay = true
                };

                probe.Start(slice, diagnostics, sessionStatus);
                for (int i = 0; i < CharacterTestNetworkSyncProbe.DefaultWarmupFrames; i++)
                    probe.Tick(1d / 60d, slice, diagnostics, NullRuntimeLogger.Instance, submitProbeCommands: false);

                Assert.IsTrue(probe.TryCreateFixedInputSnapshot(out InputSnapshot fixedInput));
                Assert.Greater(fixedInput.Move.y, 0.5f);

                probe.Tick(1d / 60d, slice, diagnostics, NullRuntimeLogger.Instance, submitProbeCommands: false);
                CharacterTestNetworkSyncProbeStatus status = probe.Stop(cancelled: true);

                Assert.AreEqual(0, submitCount);
                Assert.AreEqual(1, status.SubmittedCommandCount);
                Assert.AreEqual(1, status.NonZeroIntentCommandCount);
            }
        }

        [Test]
        public void NetworkSyncProbe_WritesDedicatedJsonlLog()
        {
            var probe = new CharacterTestNetworkSyncProbe();
            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                slice.SetRuntimeCommandSubmitter(command =>
                    RuntimeCommandValidationResult.Accepted(command));
                CharacterTestNetworkLoopbackDiagnostics diagnostics =
                    CreateSyncProbeDiagnostics(ownedEntityId: 7);
                var sessionStatus = new CharacterTestNetworkSessionStatus
                {
                    Kind = CharacterTestNetworkSessionKind.Join,
                    LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                    NetworkMode = CharacterTestNetworkMode.UnityTransportClient,
                    ClientStatus = NetworkSessionStatus.Connected,
                    OwnedEntityId = 7,
                    FullSnapshotReceivedCount = 1,
                    CanEnterGameplay = true
                };

                probe.Start(slice, diagnostics, sessionStatus);
                for (int i = 0; i < CharacterTestNetworkSyncProbe.DefaultWarmupFrames; i++)
                    probe.Tick(1d / 60d, slice, diagnostics, NullRuntimeLogger.Instance);

                probe.Tick(1d / 60d, slice, diagnostics, NullRuntimeLogger.Instance);
                CharacterTestNetworkSyncProbeStatus status = probe.Stop(cancelled: true);

                Assert.IsFalse(string.IsNullOrWhiteSpace(status.LogPath));
                Assert.IsFalse(status.LogWriteFailed);
                Assert.IsTrue(File.Exists(status.LogPath), status.LogPath);
                string log = File.ReadAllText(status.LogPath);
                StringAssert.Contains("\"eventType\":\"start\"", log);
                StringAssert.Contains("\"eventType\":\"frame\"", log);
                StringAssert.Contains("\"eventType\":\"cancelled\"", log);
                StringAssert.Contains("\"pattern\":\"MoveStopLoop\"", log);
                StringAssert.Contains("\"segment\":\"Forward\"", log);

                File.Delete(status.LogPath);
            }
        }

        [Test]
        public void NetworkSyncProbe_EvaluatesHealthyPathAsPass()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 10,
                    authoritativeCommandEchoCount: 10,
                    runtimeCommandSentBytes: 1000L),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 310,
                    authoritativeCommandEchoCount: 310,
                    runtimeCommandSentBytes: 5000L,
                    snapshotSentBytes: 1200L),
                submittedCommandCount: 300,
                rejectedCommandCount: 0,
                elapsedFrames: 300,
                elapsedSeconds: 5d,
                maxPendingTransportMessages: 0,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Pass, status.Result);
            Assert.AreEqual("PASS", status.ResultCode);
            Assert.AreEqual(300, status.DeltaAuthoritativeCommandReceived);
            Assert.AreEqual(4000L, status.DeltaRuntimeCommandBytes);
        }

        [Test]
        public void NetworkSyncProbe_EvaluatesMissingStopIntentAsFail()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 10,
                    authoritativeCommandEchoCount: 10,
                    runtimeCommandSentBytes: 1000L),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 40,
                    authoritativeCommandEchoCount: 40,
                    runtimeCommandSentBytes: 2000L),
                submittedCommandCount: 30,
                rejectedCommandCount: 0,
                elapsedFrames: 30,
                elapsedSeconds: 0.5d,
                maxPendingTransportMessages: 0,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty,
                zeroIntentCommandCount: 0,
                nonZeroIntentCommandCount: 30);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Fail, status.Result);
            Assert.AreEqual("NO_STOP_INTENT", status.ResultCode);
        }

        [Test]
        public void NetworkSyncProbe_EvaluatesLateCommandsAsFail()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(authoritativeCommandReceivedCount: 1),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 20,
                    authoritativeCommandEchoCount: 20,
                    authoritativeCommandLateCount: 1),
                submittedCommandCount: 20,
                rejectedCommandCount: 0,
                elapsedFrames: 20,
                elapsedSeconds: 0.33d,
                maxPendingTransportMessages: 0,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Fail, status.Result);
            Assert.AreEqual("AUTH_LATE_GROWING", status.ResultCode);
        }

        [Test]
        public void NetworkSyncProbe_EvaluatesSustainedFullSnapshotGrowthAsFail()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(
                    fullSnapshotReceivedCount: 1,
                    stateSyncPolicy: NetworkStateSyncPolicy.SnapshotVerification),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 30,
                    authoritativeCommandEchoCount: 30,
                    fullSnapshotReceivedCount: 1 + CharacterTestNetworkSyncProbe.FullSnapshotStormThreshold + 1,
                    stateSyncPolicy: NetworkStateSyncPolicy.SnapshotVerification),
                submittedCommandCount: 30,
                rejectedCommandCount: 0,
                elapsedFrames: 30,
                elapsedSeconds: 0.5d,
                maxPendingTransportMessages: 0,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Fail, status.Result);
            Assert.AreEqual("FULL_SNAPSHOT_STORM", status.ResultCode);
        }

        [Test]
        public void NetworkSyncProbe_AllowsSingleFullSnapshotTailPacket()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(
                    fullSnapshotReceivedCount: 1,
                    stateSyncPolicy: NetworkStateSyncPolicy.SnapshotVerification),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 30,
                    authoritativeCommandEchoCount: 30,
                    fullSnapshotReceivedCount: 1 + CharacterTestNetworkSyncProbe.FullSnapshotStormThreshold,
                    stateSyncPolicy: NetworkStateSyncPolicy.SnapshotVerification),
                submittedCommandCount: 30,
                rejectedCommandCount: 0,
                elapsedFrames: 30,
                elapsedSeconds: 0.5d,
                maxPendingTransportMessages: 0,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Pass, status.Result);
            Assert.AreEqual("PASS", status.ResultCode);
        }

        [Test]
        public void NetworkSyncProbe_BootstrapOnlyFailsWhenStateSyncGrows()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(fullSnapshotReceivedCount: 1),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 30,
                    authoritativeCommandEchoCount: 30,
                    fullSnapshotReceivedCount: 2,
                    digestSnapshotReceivedCount: 1,
                    snapshotRequestCount: 1),
                submittedCommandCount: 30,
                rejectedCommandCount: 0,
                elapsedFrames: 30,
                elapsedSeconds: 0.5d,
                maxPendingTransportMessages: 0,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Fail, status.Result);
            Assert.AreEqual("STATE_SYNC_ACTIVE", status.ResultCode);
            Assert.AreEqual(1, status.DeltaFullSnapshot);
            Assert.AreEqual(1, status.DeltaDigestSnapshot);
            Assert.AreEqual(1, status.DeltaSnapshotRequest);
        }

        [Test]
        public void NetworkSyncProbe_EvaluatesPendingTransportBacklogAsWarning()
        {
            CharacterTestNetworkSyncProbeStatus status = CharacterTestNetworkSyncProbe.Evaluate(
                CreateSyncProbeDiagnostics(),
                CreateSyncProbeDiagnostics(
                    authoritativeCommandReceivedCount: 30,
                    authoritativeCommandEchoCount: 30,
                    runtimeCommandSentBytes: 2000L),
                submittedCommandCount: 30,
                rejectedCommandCount: 0,
                elapsedFrames: 30,
                elapsedSeconds: 0.5d,
                maxPendingTransportMessages: CharacterTestNetworkSyncProbe.PendingTransportWarningMessages + 1,
                maxSentPayloadBytes: 512,
                maxFrameDriftFrames: 1,
                lastError: string.Empty);

            Assert.AreEqual(CharacterTestNetworkSyncProbeResult.Warning, status.Result);
            Assert.AreEqual("RELIABLE_QUEUE_BLOCKED", status.ResultCode);
        }

        [Test]
        public void ControlIntentCommand_RejectsNonLocalTargetWithoutChangingInput()
        {
            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity nonLocal = FindNonLocal(slice);
                CharacterTestCharacterInputComponent before = nonLocal.Input;

                RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                    slice.CurrentFrame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    nonLocal.Id.Value,
                    Vector2.up,
                    jumpPressed: true,
                    jumpHeld: true,
                    jumpReleased: false,
                    attackPrimaryPressed: true,
                    attackSecondaryPressed: true,
                    traceId: new RuntimeTraceIdentity(940708));
                RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(command);

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / 60d);

                AssertLastCommandErrorContains(slice, "not command-driven");
                Assert.AreEqual(before.Move, nonLocal.Input.Move);
                Assert.AreEqual(before.JumpPressed, nonLocal.Input.JumpPressed);
                Assert.AreEqual(before.JumpHeld, nonLocal.Input.JumpHeld);
                Assert.AreEqual(before.JumpReleased, nonLocal.Input.JumpReleased);
                Assert.AreEqual(before.AttackPrimaryPressed, nonLocal.Input.AttackPrimaryPressed);
                Assert.AreEqual(before.AttackSecondaryPressed, nonLocal.Input.AttackSecondaryPressed);
            }
        }

        [Test]
        public void ControlIntentCommand_AppliesToRemotePlayerTarget()
        {
            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity remote = FindNonLocal(slice);
                Assert.IsTrue(slice.CharacterRuntime.TrySetControlOwner(
                    remote.Id.Value,
                    CharacterTestCharacterControlOwner.RemotePlayer));

                RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                    slice.CurrentFrame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    remote.Id.Value,
                    new Vector2(0.5f, 0.75f),
                    new Vector2(0.25f, 0.5f),
                    jumpPressed: true,
                    jumpHeld: true,
                    jumpReleased: false,
                    attackPrimaryPressed: true,
                    attackSecondaryPressed: false,
                    traceId: new RuntimeTraceIdentity(974409));
                RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(command);

                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);
                slice.Tick(1d / 60d);

                Assert.AreEqual(CharacterTestCharacterControlOwner.RemotePlayer, remote.Control.Owner);
                Assert.AreEqual(0.5f, remote.Input.Move.x, 0.001f);
                Assert.AreEqual(0.75f, remote.Input.Move.y, 0.001f);
                Assert.AreEqual(0.25f, remote.Input.WorldMove.x, 0.001f);
                Assert.AreEqual(0.5f, remote.Input.WorldMove.y, 0.001f);
                Assert.IsTrue(remote.Input.HasWorldMove);
                Assert.IsTrue(remote.Input.JumpPressed);
                Assert.IsTrue(remote.Input.JumpHeld);
                Assert.IsTrue(remote.Input.AttackPrimaryPressed);
            }
        }

        [Test]
        public void LoopbackControlIntent_ExecutesOnServerAndClientMirrorConvergesFromSnapshot()
        {
            var provider = new FakeInputProvider();
            using (var runner = new CharacterTestNetworkLoopbackRunner(
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                transportOptions: LoopbackNetworkTransportOptions.ReliableImmediate,
                createServerViews: false))
            {
                runner.ServerSlice.SetLocalInputProvider(provider);
                Tick(runner, 8);

                CharacterTestCharacterEntity serverLocal = FindLocalPlayer(runner.ServerSlice);
                RuntimeFrame inputFrame = runner.ServerSlice.CurrentFrame;
                provider.SetSnapshot(CreateInputSnapshot(
                    new Vector2(0.25f, 0.5f),
                    jumpPressed: true,
                    jumpHeld: true,
                    attackPrimaryPressed: true,
                    attackSecondaryPressed: true));

                Tick(runner, 24);

                CharacterTestCharacterEntity clientLocal = FindLocalPlayer(runner.ClientSlice);
                Assert.AreEqual(0.25f, serverLocal.Input.Move.x, 0.001f);
                Assert.AreEqual(0.5f, serverLocal.Input.Move.y, 0.001f);
                Assert.IsTrue(serverLocal.Input.JumpPressed);
                Assert.IsTrue(serverLocal.Input.AttackPrimaryPressed);
                Assert.IsTrue(serverLocal.Input.AttackSecondaryPressed);
                Assert.AreEqual(serverLocal.Input.Move.x, clientLocal.Input.Move.x, 0.001f);
                Assert.AreEqual(serverLocal.Input.Move.y, clientLocal.Input.Move.y, 0.001f);
                Assert.AreEqual(serverLocal.Input.JumpPressed, clientLocal.Input.JumpPressed);
                Assert.AreEqual(serverLocal.Input.AttackPrimaryPressed, clientLocal.Input.AttackPrimaryPressed);
                Assert.AreEqual(serverLocal.Input.AttackSecondaryPressed, clientLocal.Input.AttackSecondaryPressed);
                Assert.GreaterOrEqual(runner.ServerSession.LastAckedInputFrame.Value, inputFrame.Value);
                CharacterTestNetworkLoopbackDiagnostics diagnostics = runner.CaptureDiagnostics();
                Assert.GreaterOrEqual(diagnostics.FullSnapshotReceivedCount, 2);
                Assert.Greater(diagnostics.DigestSnapshotReceivedCount, 0);
                Assert.Greater(diagnostics.SnapshotRequestCount, 0);
            }
        }

        [Test]
        public void UnityTransportLoopbackControlIntent_ExecutesOnServerAndClientMirrorConvergesFromSnapshot()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port, "server"));
                WaitForUnityTransportConnection(serverTransport, clientTransport);

                var provider = new FakeInputProvider();
                using (var runner = new CharacterTestNetworkSessionRunner(
                    serverTransport,
                    clientTransport,
                    NullRuntimeLogger.Instance,
                    config: new NetworkSessionConfig(
                        protocolVersion: 1,
                        tickRate: 60,
                        inputDelayFrames: 2,
                        snapshotIntervalFrames: 1,
                        configHash: "character-test",
                        resourceCatalogHash: "character-test-resources"),
                    sessionId: "character-test-utp-test",
                    transportMode: "unity-transport-loopback",
                    createServerViews: false,
                    disposeTransports: false))
                {
                    runner.ServerSlice.SetLocalInputProvider(provider);
                    Tick(runner, 24);

                    Assert.AreEqual(NetworkSessionStatus.Connected, runner.ClientSession.Status);
                    CharacterTestCharacterEntity serverLocal = FindLocalPlayer(runner.ServerSlice);
                    RuntimeFrame inputFrame = runner.ServerSlice.CurrentFrame;
                    provider.SetSnapshot(CreateInputSnapshot(
                    new Vector2(-0.5f, 0.75f),
                    jumpPressed: true,
                    jumpHeld: true,
                    attackPrimaryPressed: true,
                    attackSecondaryPressed: true));

                    Tick(runner, 24);

                    CharacterTestCharacterEntity clientLocal = FindLocalPlayer(runner.ClientSlice);
                    Assert.AreEqual(-0.5f, serverLocal.Input.Move.x, 0.001f);
                    Assert.AreEqual(0.75f, serverLocal.Input.Move.y, 0.001f);
                    Assert.IsTrue(serverLocal.Input.JumpPressed);
                    Assert.IsTrue(serverLocal.Input.AttackPrimaryPressed);
                    Assert.IsTrue(serverLocal.Input.AttackSecondaryPressed);
                    Assert.AreEqual(serverLocal.Input.Move.x, clientLocal.Input.Move.x, 0.001f);
                    Assert.AreEqual(serverLocal.Input.Move.y, clientLocal.Input.Move.y, 0.001f);
                    Assert.AreEqual(serverLocal.Input.JumpPressed, clientLocal.Input.JumpPressed);
                    Assert.AreEqual(serverLocal.Input.AttackPrimaryPressed, clientLocal.Input.AttackPrimaryPressed);
                    Assert.AreEqual(serverLocal.Input.AttackSecondaryPressed, clientLocal.Input.AttackSecondaryPressed);
                    Assert.GreaterOrEqual(runner.ServerSession.LastAckedInputFrame.Value, inputFrame.Value);

                    CharacterTestNetworkLoopbackDiagnostics diagnostics = runner.CaptureDiagnostics();
                    Assert.AreEqual("unity-transport-loopback", diagnostics.TransportMode);
                    StringAssert.Contains("unity-transport", diagnostics.TransportId);
                    Assert.GreaterOrEqual(diagnostics.FullSnapshotReceivedCount, 2);
                    Assert.Greater(diagnostics.DigestSnapshotReceivedCount, 0);
                    Assert.Greater(diagnostics.SnapshotRequestCount, 0);
                }
            }
        }

        [Test]
        public void UnityTransportRemoteEndpointControlIntent_ExecutesOnServerAndClientConvergesFromSnapshot()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var clientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(clientTransport.StartClient("127.0.0.1", port, "server"));

                NetworkSessionConfig config = new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources");
                var provider = new FakeInputProvider();
                using (var server = new CharacterTestUnityTransportServerRunner(
                    serverTransport,
                    NullRuntimeLogger.Instance,
                    config: config,
                    sessionId: "character-test-utp-remote-test",
                    transportMode: "unity-transport-server",
                    createServerViews: false,
                    disposeTransport: false))
                using (var client = new CharacterTestUnityTransportClientRunner(
                    clientTransport,
                    NullRuntimeLogger.Instance,
                    config: config,
                    sessionId: "character-test-utp-remote-test",
                    transportMode: "unity-transport-client",
                    createClientViews: false,
                    disposeTransport: false))
                {
                    client.Slice.SetLocalInputProvider(provider);
                    Tick(server, client, 20);

                    Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                    Assert.GreaterOrEqual(server.Slice.CharacterEntities.Count, 2);
                    Assert.AreEqual(
                        server.Slice.CharacterEntities.Count,
                        client.Slice.CharacterEntities.Count);

                    CharacterTestNetworkLoopbackDiagnostics initialClientDiagnostics = client.CaptureDiagnostics();
                    int serverLocalEntityId = initialClientDiagnostics.OwnedEntityId;
                    DisableAiControlledCharacters(server.Slice);
                    RuntimeFrame inputFrame = client.Slice.CurrentFrame;
                    provider.SetSnapshot(CreateInputSnapshot(
                    new Vector2(0.6f, -0.4f),
                    jumpPressed: true,
                    jumpHeld: true,
                    attackPrimaryPressed: true,
                    attackSecondaryPressed: true));

                    Tick(server, client, 16);

                    CharacterTestCharacterEntity serverLocal = FindEntityById(server.Slice, serverLocalEntityId);
                    CharacterTestCharacterEntity clientLocal = FindLocalPlayer(client.Slice);
                    CharacterTestNetworkLoopbackDiagnostics serverDiagnostics = server.CaptureDiagnostics();
                    CharacterTestNetworkLoopbackDiagnostics clientDiagnostics = client.CaptureDiagnostics();
                    string inputFailure = FormatInputDiagnostics(serverDiagnostics, clientDiagnostics)
                        + " errors="
                        + FormatCommandErrors(server.Slice)
                        + " drained="
                        + FormatDrainedCommands(server.Slice)
                        + " serverEntity="
                        + serverLocal.Control.Owner
                        + "/" + serverLocal.BattleState.DominantReason
                        + "/" + serverLocal.BattleState.GateFlags
                        + "/action=" + serverLocal.Action.ActiveActionId;
                    Assert.AreEqual(0.6f, serverLocal.Input.Move.x, 0.001f, inputFailure);
                    Assert.AreEqual(-0.4f, serverLocal.Input.Move.y, 0.001f);
                    Assert.IsTrue(serverLocal.Input.JumpPressed);
                    Assert.IsTrue(serverLocal.Input.AttackPrimaryPressed);
                    Assert.IsTrue(serverLocal.Input.AttackSecondaryPressed);
                    Assert.AreEqual(
                        serverLocal.Input.Move.x,
                        clientLocal.Input.Move.x,
                        0.001f,
                        inputFailure + " clientInputs=" + FormatInputs(client.Slice));
                    Assert.AreEqual(serverLocal.Input.Move.y, clientLocal.Input.Move.y, 0.001f);
                    Assert.AreEqual(serverLocal.Input.JumpPressed, clientLocal.Input.JumpPressed);
                    Assert.AreEqual(serverLocal.Input.AttackPrimaryPressed, clientLocal.Input.AttackPrimaryPressed);
                    Assert.AreEqual(serverLocal.Input.AttackSecondaryPressed, clientLocal.Input.AttackSecondaryPressed);
                    Assert.GreaterOrEqual(server.Session.LastAckedInputFrame.Value, inputFrame.Value);

                    Assert.AreEqual("unity-transport-server", serverDiagnostics.TransportMode);
                    Assert.AreEqual("unity-transport-client", clientDiagnostics.TransportMode);
                    Assert.GreaterOrEqual(clientDiagnostics.ResyncCount, 1);
                    Assert.GreaterOrEqual(clientDiagnostics.SuppressedSnapshotMismatchCount, 0);
                    Assert.GreaterOrEqual(clientDiagnostics.FullSnapshotReceivedCount, 1);
                    Assert.Greater(serverDiagnostics.DigestSnapshotSentCount, 0);
                    Assert.Greater(serverDiagnostics.AuthoritativeCommandSentCount, 0);
                    Assert.Greater(clientDiagnostics.AuthoritativeCommandEchoCount, 0);
                    Assert.AreEqual(0, clientDiagnostics.AuthoritativeCommandEnqueuedCount);
                    Assert.Greater(serverDiagnostics.SnapshotSentBytes, 0);
                    Assert.Greater(clientDiagnostics.RuntimeCommandSentBytes, 0);
                    Assert.Greater(clientDiagnostics.DeliveredBytes, 0);
                }
            }
        }

        [Test]
        public void MultiClientLoopback_AssignsDistinctOwnedEntitiesAndAppliesCommandsPerPeer()
        {
            var transport = new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate);
            NetworkSessionConfig config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 0,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var providerA = new FakeInputProvider();
            var providerB = new FakeInputProvider();
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-two-client-loopback",
                serverPeerId: "server",
                transportMode: "loopback-server",
                createServerViews: false,
                disposeTransport: false))
            using (var clientA = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-two-client-loopback",
                serverPeerId: "server",
                clientPeerId: "client-a",
                transportMode: "loopback-client-a",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            using (var clientB = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-two-client-loopback",
                serverPeerId: "server",
                clientPeerId: "client-b",
                transportMode: "loopback-client-b",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                clientA.Slice.SetLocalInputProvider(providerA);
                clientB.Slice.SetLocalInputProvider(providerB);
                Tick(server, clientA, clientB, 40);

                CharacterTestNetworkLoopbackDiagnostics serverDiagnostics = server.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics diagnosticsA = clientA.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics diagnosticsB = clientB.CaptureDiagnostics();
                Assert.AreEqual(2, serverDiagnostics.ConnectedPeerCount);
                Assert.Greater(diagnosticsA.OwnedEntityId, 0);
                Assert.Greater(diagnosticsB.OwnedEntityId, 0);
                Assert.AreNotEqual(diagnosticsA.OwnedEntityId, diagnosticsB.OwnedEntityId);
                Assert.AreEqual(diagnosticsA.OwnedEntityId, FindLocalPlayer(clientA.Slice).Id.Value);
                Assert.AreEqual(diagnosticsB.OwnedEntityId, FindLocalPlayer(clientB.Slice).Id.Value);
                Assert.AreEqual(1, CountLocalPlayers(clientA.Slice), FormatOwners(clientA.Slice));
                Assert.AreEqual(1, CountLocalPlayers(clientB.Slice), FormatOwners(clientB.Slice));

                providerA.SetSnapshot(CreateInputSnapshot(Vector2.right));
                providerB.SetSnapshot(CreateInputSnapshot(
                    Vector2.left,
                    attackPrimaryPressed: true));
                Tick(server, clientA, clientB, 30);

                CharacterTestCharacterEntity serverA = FindEntityById(server.Slice, diagnosticsA.OwnedEntityId);
                CharacterTestCharacterEntity serverB = FindEntityById(server.Slice, diagnosticsB.OwnedEntityId);
                Assert.Greater(serverA.Input.Move.x, 0.5f);
                Assert.Less(serverB.Input.Move.x, -0.5f);
                Assert.IsFalse(serverA.Input.AttackPrimaryPressed);
                Assert.IsTrue(serverB.Input.AttackPrimaryPressed);
                Assert.Greater(server.Session.GetLastAckedInputFrame("client-a").Value, 0);
                Assert.Greater(server.Session.GetLastAckedInputFrame("client-b").Value, 0);
            }
        }

        [Test]
        public void LoopbackRunner_UsesConfiguredSnapshotInterval()
        {
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            using (var runner = new CharacterTestNetworkSessionRunner(
                transport,
                transport,
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 4,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-snapshot-interval-test",
                transportMode: "loopback-interval-test",
                createServerViews: false))
            {
                Tick(runner, 12);

                int receivedBefore = runner.ClientSession.FullSnapshotReceivedCount
                    + runner.ClientSession.DigestSnapshotReceivedCount;
                Assert.Greater(receivedBefore, 0);
                Assert.Greater(runner.ClientSession.AverageSnapshotIntervalFrames, 0);
                Assert.LessOrEqual(
                    runner.ClientSession.MaxSnapshotIntervalFrames,
                    runner.Config.SnapshotIntervalFrames);

                Tick(runner, 1);

                int receivedAfter = runner.ClientSession.FullSnapshotReceivedCount
                    + runner.ClientSession.DigestSnapshotReceivedCount;
                Assert.GreaterOrEqual(receivedAfter, receivedBefore);
                Assert.LessOrEqual(
                    runner.ClientSession.MaxSnapshotIntervalFrames,
                    runner.Config.SnapshotIntervalFrames);
            }
        }

        [Test]
        public void ConnectedPeerWithoutControlPolicy_RemainsUnboundAndDoesNotSpawnCharacter()
        {
            var config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 0,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var transport = new LoopbackNetworkTransport();
            var controlGrants = new CharacterTestNetworkEntityControlGrantState();
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: -2f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None),
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2,
                    x: 2f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None)
            };

            using (var slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = startupSpecs,
                SuppressMissingStartupSpawnWarning = true
            }))
            {
                var server = new NetworkSessionRunner(
                    NetworkSessionRole.Server,
                    "character-test-unbound-peer",
                    "server",
                    "server",
                    config,
                    transport,
                    slice.RuntimeCommands.CommandBuffer,
                    commandAuthorizer: controlGrants.AuthorizeCommand);
                var client = new NetworkSessionRunner(
                    NetworkSessionRole.Client,
                    "character-test-unbound-peer",
                    "client",
                    "server",
                    config,
                    transport);

                client.Connect(RuntimeFrame.Zero);
                server.ProcessIncoming(RuntimeFrame.Zero);
                client.ProcessIncoming(RuntimeFrame.Zero);
                Tick(slice, 8);
                int configuredEntityCount = slice.CharacterEntities.Count;

                controlGrants.SyncConnectedPeers(
                    server,
                    slice.CharacterRuntime,
                    slice.RuntimeCommands.CommandBuffer);
                Tick(slice, 8);
                controlGrants.SyncConnectedPeers(
                    server,
                    slice.CharacterRuntime,
                    slice.RuntimeCommands.CommandBuffer);

                Assert.AreEqual(2, configuredEntityCount);
                Assert.AreEqual(configuredEntityCount, slice.CharacterEntities.Count);
                Assert.AreEqual(1, controlGrants.ConnectedPeerCount);
                Assert.AreEqual(0, controlGrants.AssignedPeerCount);
                Assert.AreEqual(0, controlGrants.GetControlledEntityId("client"));
                Assert.AreEqual("client->unbound", controlGrants.CreateSummary());
            }
        }

        [Test]
        public void LoopbackRunner_UnboundPeerUsesConfiguredRosterAndAuthoritativePlannerCommands()
        {
            var config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 0,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var transport = new LoopbackNetworkTransport();
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: -2f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2,
                    x: 2f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled)
            };

            using (var runner = new CharacterTestNetworkSessionRunner(
                transport,
                transport,
                useExpandedReplayContract: true,
                logger: NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-unbound-runner",
                transportMode: "loopback-unbound",
                createServerViews: false,
                startupCharacterSpawnSpecs: startupSpecs,
                suppressMissingStartupSpawnWarning: true,
                peerControlMode: CharacterTestNetworkPeerControlMode.Unbound))
            {
                Tick(runner, 30);

                Assert.AreEqual(NetworkSessionStatus.Connected, runner.ClientSession.Status);
                Assert.AreEqual(2, runner.ServerSlice.CharacterEntities.Count);
                Assert.AreEqual(2, runner.ClientSlice.CharacterEntities.Count);
                Assert.AreEqual(0, CountLocalPlayers(runner.ServerSlice));
                Assert.AreEqual(0, CountLocalPlayers(runner.ClientSlice));
                Assert.AreEqual(0, runner.CaptureDiagnostics().OwnedEntityId);

                CharacterTestRuntimeAiPlannerSnapshot planner =
                    runner.ServerSlice.Services.RuntimeAiPlannerCommands.CreateSnapshot();
                Assert.AreEqual(2, planner.Agents.Count);
                bool submittedCommand = false;
                for (int i = 0; i < planner.Agents.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerAgentSnapshot agent = planner.Agents[i];
                    submittedCommand |= !string.Equals(agent.LastCommand, "none", StringComparison.Ordinal);
                    Assert.That(agent.RejectReason, Does.Not.Contain("entity-control grant"));
                }

                Assert.IsTrue(
                    submittedCommand,
                    "Authoritative Runtime AI Planner commands must continue while the connected peer is unbound.");
            }
        }

        [Test]
        public void OwnershipRelease_PrunesPendingOwnedEntityCommandsForDisconnectedPeer()
        {
            var config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 4,
                snapshotIntervalFrames: 4,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var transport = new LoopbackNetworkTransport();
            var controlGrants = new CharacterTestNetworkEntityControlGrantState();
            var peerControlPolicy = new CharacterTestSingleCharacterPeerControlPolicy();

            using (var slice = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                var server = new NetworkSessionRunner(
                    NetworkSessionRole.Server,
                    "character-test-prune",
                    "server",
                    "server",
                    config,
                    transport,
                    slice.RuntimeCommands.CommandBuffer,
                    commandAuthorizer: controlGrants.AuthorizeCommand);
                var client = new NetworkSessionRunner(
                    NetworkSessionRole.Client,
                    "character-test-prune",
                    "client",
                    "server",
                    config,
                    transport);

                client.Connect(RuntimeFrame.Zero);
                server.ProcessIncoming(RuntimeFrame.Zero);
                client.ProcessIncoming(RuntimeFrame.Zero);
                Tick(slice, 8);
                controlGrants.SyncConnectedPeers(
                    server,
                    slice.CharacterRuntime,
                    slice.RuntimeCommands.CommandBuffer,
                    peerControlPolicy);

                int entityId = controlGrants.GetControlledEntityId("client");
                Assert.Greater(entityId, 0);
                RuntimeCommand pendingCommand = CharacterTestRuntimeCommandFactory.SetControlIntent(
                    new RuntimeFrame(10),
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    entityId,
                    Vector2.right,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    attackSecondaryPressed: false,
                    debugSecondaryPressed: false,
                    debugPrimaryPressed: false,
                    traceId: new RuntimeTraceIdentity(906136));
                RuntimeCommandValidationResult enqueue =
                    slice.RuntimeCommands.CommandBuffer.Enqueue(pendingCommand);

                Assert.IsTrue(enqueue.Success);
                Assert.AreEqual(1, slice.PendingRuntimeCommandCount);

                transport.QueueEvent(new NetworkTransportEvent(
                    NetworkTransportEventKind.Disconnected,
                    "client",
                    new RuntimeFrame(1),
                    "test disconnect"));
                server.ProcessIncoming(new RuntimeFrame(1));
                controlGrants.SyncConnectedPeers(
                    server,
                    slice.CharacterRuntime,
                    slice.RuntimeCommands.CommandBuffer,
                    peerControlPolicy);

                Assert.AreEqual(0, slice.PendingRuntimeCommandCount);
                Assert.AreEqual(0, controlGrants.GetControlledEntityId("client"));
                Assert.AreEqual(
                    CharacterTestCharacterControlOwner.None,
                    slice.CharacterRuntime.FindEntityById(entityId).Control.Owner);
            }
        }

        [Test]
        public void LateJoinSnapshot_AppliesFullCharacterState()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(frame, snapshot);

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(server.CharacterEntities.Count, client.CharacterEntities.Count);
                Assert.AreEqual(server.CameraMode, client.CameraMode);
                Assert.AreEqual(server.TargetLockEnabled, client.TargetLockEnabled);
                Assert.AreEqual(serverParticipant.ComputeStateHash(frame), clientParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void SnapshotRejectsWrongNumericParticipantWithSameLabelBeforeWorldMutation()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = client.CurrentFrame;
                RuntimeCustomState valid = serverParticipant.CaptureSnapshot(frame);
                long hashBefore = clientParticipant.ComputeStateHash(frame);
                int entityCountBefore = client.CharacterEntities.Count;
                RuntimeCustomState wrongNumericIdentity = new RuntimeCustomState(
                    CharacterTestNetworkSnapshotParticipant.NumericParticipantId + 1,
                    valid.TypeId,
                    valid.SchemaVersion,
                    valid.PayloadJson);

                NetworkSnapshotApplyResult result =
                    clientParticipant.ApplySnapshot(frame, wrongNumericIdentity);

                Assert.IsFalse(result.Success);
                StringAssert.Contains("participant mismatch", result.Message);
                Assert.AreEqual(frame.Value, client.CurrentFrame.Value);
                Assert.AreEqual(hashBefore, clientParticipant.ComputeStateHash(frame));
                Assert.AreEqual(entityCountBefore, client.CharacterEntities.Count);
            }
        }

        [Test]
        public void LateJoinSnapshot_AppliesCharacterPhysicsProfileAndHash()
        {
            using (var server = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateHeadlessPhysicsRuntime(),
                createCharacterViews: false))
            using (var client = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateHeadlessPhysicsRuntime(),
                createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                CharacterTestCharacterEntity serverEntity = server.CharacterEntities[0];
                serverEntity.SetPhysics(serverEntity.Physics.WithRuntimeModifiers(
                    massScale: 1.4f,
                    radiusScale: 1.2f,
                    pushScale: 0.75f));

                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(frame, snapshot);
                CharacterTestCharacterEntity clientEntity = client.CharacterEntities[0];

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(serverEntity.Physics.MassScale, clientEntity.Physics.MassScale, 0.0001f);
                Assert.AreEqual(serverEntity.Physics.RadiusScale, clientEntity.Physics.RadiusScale, 0.0001f);
                Assert.AreEqual(serverEntity.Physics.PushScale, clientEntity.Physics.PushScale, 0.0001f);
                Assert.AreEqual(serverEntity.Physics.EffectiveMassKg, clientEntity.Physics.EffectiveMassKg, 0.0001f);
                Assert.AreEqual(serverParticipant.ComputeStateHash(frame), clientParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void LateJoinSnapshot_AppliesSurfaceMaterialStateAndHash()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                CharacterTestCharacterEntity serverEntity = server.CharacterEntities[0];
                serverEntity.SetMotionState(
                    verticalVelocity: 0f,
                    lastMoveInput: Vector2.zero,
                    isGrounded: true,
                    horizontalVelocity: Vector2.right * 3f,
                    surfaceMaterial: CombatSurfaceMaterial.BouncePad,
                    surfaceBounceSpeedCapY: 12f);

                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(frame, snapshot);
                CharacterTestCharacterEntity clientEntity = client.CharacterEntities[0];

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, clientEntity.SurfaceMaterialKind);
                Assert.AreEqual(serverEntity.SurfaceMaterialStableId, clientEntity.SurfaceMaterialStableId);
                Assert.AreEqual(serverEntity.SurfaceFriction, clientEntity.SurfaceFriction, 0.0001f);
                Assert.AreEqual(serverEntity.SurfaceElasticity, clientEntity.SurfaceElasticity, 0.0001f);
                Assert.AreEqual(serverEntity.SurfaceFixedBounceHeight, clientEntity.SurfaceFixedBounceHeight, 0.0001f);
                Assert.AreEqual(serverEntity.SurfaceBounceSpeedCapY, clientEntity.SurfaceBounceSpeedCapY, 0.0001f);
                Assert.AreEqual(serverParticipant.ComputeStateHash(frame), clientParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void LateJoinSnapshot_MissingPhysicsFieldsRestoresDefinitionProfile()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                CharacterTestCharacterEntity clientEntity = client.CharacterEntities[0];
                CharacterTestCharacterPhysicsComponent definitionPhysics = clientEntity.Physics;
                clientEntity.SetPhysics(clientEntity.Physics.WithRuntimeModifiers(
                    massScale: 2f,
                    radiusScale: 1.5f,
                    pushScale: 0.25f));

                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                string legacyPayload = RemoveCharacterPhysicsFields(snapshot.PayloadJson);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(
                    frame,
                    new RuntimeCustomState(
                        CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                        CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                        CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                        legacyPayload));

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(definitionPhysics.CapsuleRadiusMeters, clientEntity.Physics.CapsuleRadiusMeters, 0.0001f);
                Assert.AreEqual(definitionPhysics.CapsuleHeightMeters, clientEntity.Physics.CapsuleHeightMeters, 0.0001f);
                Assert.AreEqual(definitionPhysics.MassKg, clientEntity.Physics.MassKg, 0.0001f);
                Assert.AreEqual(1f, clientEntity.Physics.MassScale, 0.0001f);
                Assert.AreEqual(1f, clientEntity.Physics.RadiusScale, 0.0001f);
            }
        }

        [Test]
        public void SnapshotHash_OwnedStateIgnoresPeerLocalControlPresentation()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                Assert.IsTrue(server.CharacterRuntime.TrySetControlOwner(
                    1,
                    CharacterTestCharacterControlOwner.RemotePlayer));
                Assert.IsTrue(client.CharacterRuntime.TrySetControlOwner(
                    1,
                    CharacterTestCharacterControlOwner.LocalPlayer));
                Assert.IsTrue(server.CharacterRuntime.TrySetControlOwner(
                    2,
                    CharacterTestCharacterControlOwner.LocalPlayer));
                Assert.IsTrue(client.CharacterRuntime.TrySetControlOwner(
                    2,
                    CharacterTestCharacterControlOwner.RemotePlayer));
                var serverParticipant = new CharacterTestNetworkSnapshotParticipant(
                    server.CharacterRuntime,
                    server.RuntimeCommands);
                var clientParticipant = new CharacterTestNetworkSnapshotParticipant(
                    client.CharacterRuntime,
                    client.RuntimeCommands);
                RuntimeFrame frame = server.CurrentFrame;

                Assert.AreEqual(
                    serverParticipant.ComputeOwnedStateHash(frame, 1),
                    clientParticipant.ComputeOwnedStateHash(frame, 1));
                Assert.AreEqual(
                    serverParticipant.ComputeNonOwnedStateHash(frame, 1),
                    clientParticipant.ComputeNonOwnedStateHash(frame, 1));
            }
        }

        [Test]
        public void SnapshotApply_PrunesPendingCommandsForNonPlayerOwnedEntities()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                Assert.IsTrue(server.CharacterRuntime.TrySetControlOwner(
                    1,
                    CharacterTestCharacterControlOwner.None));

                RuntimeCommand pendingCommand = CharacterTestRuntimeCommandFactory.SetControlIntent(
                    new RuntimeFrame(64),
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    1,
                    Vector2.right,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    attackSecondaryPressed: false,
                    debugSecondaryPressed: false,
                    debugPrimaryPressed: false,
                    traceId: new RuntimeTraceIdentity(959859));
                RuntimeCommandValidationResult enqueue =
                    client.RuntimeCommands.CommandBuffer.Enqueue(pendingCommand);
                Assert.IsTrue(enqueue.Success);
                Assert.AreEqual(1, client.PendingRuntimeCommandCount);

                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(server.CurrentFrame);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(server.CurrentFrame, snapshot);

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(0, client.PendingRuntimeCommandCount);
                Assert.AreEqual(
                    CharacterTestCharacterControlOwner.None,
                    client.CharacterRuntime.FindEntityById(1).Control.Owner);
            }
        }

        [Test]
        public void LateJoinSnapshot_AppliesActiveProjectileStateAndHash()
        {
            using (var server = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateHeadlessPhysicsRuntime(),
                createCharacterViews: false))
            using (var client = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateHeadlessPhysicsRuntime(),
                createCharacterViews: false))
            {
                Tick(server, 8);
                CombatProjectileRuntimeState serverProjectileState = SpawnFireballProjectile(server);

                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(frame, snapshot);
                CombatProjectileRuntimeState clientProjectileState =
                    client.CombatTraceSystem.CreateProjectileStateSnapshot();

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsNotNull(clientProjectileState);
                Assert.AreEqual(serverProjectileState.DefinitionHash, clientProjectileState.DefinitionHash);
                Assert.AreEqual(serverProjectileState.NextProjectileId, clientProjectileState.NextProjectileId);
                Assert.AreEqual(serverProjectileState.ActiveCount, clientProjectileState.ActiveCount);
                Assert.AreEqual(
                    serverProjectileState.ActiveProjectiles[0],
                    clientProjectileState.ActiveProjectiles[0]);
                Assert.AreEqual(
                    serverParticipant.ComputeStateHash(frame),
                    clientParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void LateJoinSnapshot_MissingProjectileFieldAppliesAsEmptyProjectileState()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateHeadlessPhysicsRuntime(),
                createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                CombatProjectileRuntimeState clientProjectileState = SpawnFireballProjectile(client);
                Assert.AreEqual(1, clientProjectileState.ActiveCount);

                CharacterTestNetworkSnapshotParticipant serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                CharacterTestNetworkSnapshotParticipant clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                string legacyPayload = RemoveJsonTailProperty(snapshot.PayloadJson, "projectiles");
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(
                    frame,
                    new RuntimeCustomState(
                        CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                        CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                        CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                        legacyPayload));

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(0, client.CombatTraceSystem.CreateProjectileStateSnapshot().ActiveCount);
            }
        }

        [Test]
        public void SnapshotApply_PreservesMirrorEntitiesWhenIdentityMatches()
        {
            using (var server = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var client = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                Tick(server, 8);
                Tick(client, 8);
                CharacterTestCharacterEntity before = client.CharacterEntities[0];
                var serverParticipant =
                    new CharacterTestNetworkSnapshotParticipant(server.CharacterRuntime, server.RuntimeCommands);
                var clientParticipant =
                    new CharacterTestNetworkSnapshotParticipant(client.CharacterRuntime, client.RuntimeCommands);

                RuntimeFrame frame = server.CurrentFrame;
                RuntimeCustomState snapshot = serverParticipant.CaptureSnapshot(frame);
                NetworkSnapshotApplyResult result = clientParticipant.ApplySnapshot(frame, snapshot);

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreSame(before, client.CharacterEntities[0]);
                Assert.AreEqual(serverParticipant.ComputeStateHash(frame), clientParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void UnityTransportClientRunner_WaitsForConnectionWithoutLocalInputWarning()
        {
            var logger = new CountingRuntimeLogger();
            using (var client = new CharacterTestUnityTransportClientRunner(
                new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate),
                logger,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-disconnected-client-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "127.0.0.1:7777"))
            {
                Tick(client, 130);

                Assert.AreEqual(0, logger.LocalInputWarningCount);
                Assert.AreEqual(1, logger.NetworkConnectionWarningCount);
                Assert.AreNotEqual(NetworkSessionStatus.Connected, client.Session.Status);
            }
        }

        [Test]
        public void UnityTransportClientRunner_ActiveInputWhileDisconnectedLogsActionableWarning()
        {
            var provider = new FakeInputProvider();
            var logger = new CountingRuntimeLogger();
            using (var client = new CharacterTestUnityTransportClientRunner(
                new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate),
                logger,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-disconnected-input-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "127.0.0.1:7777"))
            {
                client.Slice.SetLocalInputProvider(provider);
                Tick(client, 8);
                provider.SetSnapshot(CreateInputSnapshot(Vector2.zero, attackPrimaryPressed: true));
                Tick(client, 2);

                Assert.AreEqual(0, logger.LocalInputWarningCount);
                Assert.AreEqual(1, logger.DisconnectedActiveInputWarningCount);
            }
        }

        [Test]
        public void UnityTransportClientRunner_ConnectedClientWarnsWhenSnapshotsStop()
        {
            var logger = new CountingRuntimeLogger();
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-snapshot-stall-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                logger,
                config: server.Config,
                sessionId: "character-test-snapshot-stall-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                Tick(server, client, 24);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);

                Tick(client, 190);

                Assert.AreEqual(1, logger.ServerSnapshotStallWarningCount);
            }
        }

        [Test]
        public void UnityTransportClientRunner_PredictsLocalControlWhenSnapshotsStop()
        {
            var provider = new FakeInputProvider();
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 0,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-client-prediction-no-snapshot-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: server.Config,
                sessionId: "character-test-client-prediction-no-snapshot-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                client.Slice.SetLocalInputProvider(provider);
                Tick(server, client, 24);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                CharacterTestNetworkLoopbackDiagnostics diagnostics = client.CaptureDiagnostics();
                int expectedLeadFrames = Mathf.Clamp(
                    client.Config.InputDelayFrames
                    + Mathf.Max(2, (diagnostics.RttEstimateFrames + 1) / 2)
                    + 2,
                    1,
                    client.Config.InputDelayFrames + 10);
                Assert.AreEqual(expectedLeadFrames, diagnostics.PredictionLeadFrames);
                Assert.Greater(diagnostics.PredictionHistoryCount, 0);

                provider.SetSnapshot(CreateInputSnapshot(Vector2.right));
                Tick(server, client, 20);

                CharacterTestCharacterEntity before = FindLocalPlayer(client.Slice);
                float x = before.Transform.X;
                float z = before.Transform.Z;

                Tick(client, 8);

                CharacterTestCharacterEntity after = FindLocalPlayer(client.Slice);
                float dx = after.Transform.X - x;
                float dz = after.Transform.Z - z;
                Assert.Greater(dx * dx + dz * dz, 0.0001f);
                Assert.Greater(client.CaptureDiagnostics().PredictionHistoryCount, 0);
            }
        }

        [Test]
        public void UnityTransportClientRunner_BootstrapOnlyHoldsLocalSimulationWhenServerFrameStalls()
        {
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            var config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources",
                stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly);
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-client-bootstrap-no-hold-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-client-bootstrap-no-hold-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                Tick(server, client, 40);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                Assert.Greater(client.Session.LastServerFrame.Value, 0L);

                long before = client.Slice.CurrentFrame.Value;
                Tick(client, 20);

                Assert.Less(client.Slice.CurrentFrame.Value, before + 20L);
                CharacterTestNetworkLoopbackDiagnostics diagnostics = client.CaptureDiagnostics();
                Assert.LessOrEqual(
                    client.Slice.CurrentFrame.Value - diagnostics.ServerFrame.Value,
                    diagnostics.PredictionLeadFrames,
                    "Client should not run so far ahead that remote authoritative commands become late.");
            }
        }

        [Test]
        public void UnityTransportClientRunner_BootstrapOnlyCatchesUpWhenServerFrameRunsAhead()
        {
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            var config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources",
                stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly);
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-client-bootstrap-catch-up-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-client-bootstrap-catch-up-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                Tick(server, client, 40);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                Assert.Greater(client.Session.LastServerFrame.Value, 0L);

                Tick(server, 90);
                long before = client.Slice.CurrentFrame.Value;
                Tick(client, 40);

                long advancedFrames = client.Slice.CurrentFrame.Value - before;
                CharacterTestNetworkLoopbackDiagnostics diagnostics = client.CaptureDiagnostics();
                Assert.Greater(
                    advancedFrames,
                    40L,
                    "Client should run limited extra simulation steps when BootstrapOnly server frame is ahead.");
                Assert.LessOrEqual(
                    Math.Abs(client.Slice.CurrentFrame.Value - diagnostics.ServerFrame.Value),
                    diagnostics.PredictionLeadFrames,
                    "Client catch-up should bring the current simulation frame back inside the prediction lead window.");
            }
        }

        [Test]
        public void UnityTransportClientRunner_PredictionLeadIsCappedUnderHighRtt()
        {
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 10));
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-client-prediction-cap-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: server.Config,
                sessionId: "character-test-client-prediction-cap-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                Tick(server, client, 90);

                CharacterTestNetworkLoopbackDiagnostics diagnostics = client.CaptureDiagnostics();
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                Assert.Greater(diagnostics.RttEstimateFrames, server.Config.InputDelayFrames + 12);
                Assert.AreEqual(server.Config.InputDelayFrames + 10, diagnostics.PredictionLeadFrames);
                Assert.GreaterOrEqual(diagnostics.MaxRttFrames, diagnostics.RttEstimateFrames);
            }
        }

        [Test]
        public void UnityTransportClientRunner_BootstrapOnlyHandlesAuthoritativeCommandEchoThroughRollbackHook()
        {
            var provider = new FakeInputProvider();
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 6));
            var config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources",
                stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly);
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-client-command-rollback-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-client-command-rollback-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                client.Slice.SetLocalInputProvider(provider);
                Tick(server, client, 80);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);

                provider.SetSnapshot(CreateInputSnapshot(new Vector2(0.5f, -0.25f), jumpPressed: true, jumpHeld: true));
                Tick(server, client, 90);

                CharacterTestNetworkLoopbackDiagnostics serverDiagnostics = server.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics clientDiagnostics = client.CaptureDiagnostics();
                Assert.Greater(serverDiagnostics.AuthoritativeCommandSentCount, 0);
                Assert.Greater(clientDiagnostics.AuthoritativeCommandReceivedCount, 0);
                Assert.Greater(clientDiagnostics.AuthoritativeCommandEchoCount, 0);
                Assert.AreEqual(0, clientDiagnostics.AuthoritativeCommandEnqueuedCount);
                Assert.AreEqual(0, clientDiagnostics.AuthoritativeCommandLateCount);
                Assert.AreEqual(0, clientDiagnostics.AuthoritativeCommandDroppedCount);
                Assert.Greater(clientDiagnostics.RollbackHistoryCount, 0);
                Assert.AreEqual(0, clientDiagnostics.RollbackFailureCount, clientDiagnostics.LastRollbackFailure);
            }
        }

        [Test]
        public void UnityTransportClientRunner_AppliesFullSnapshotCorrectionThroughRollbackHook()
        {
            var logger = new CountingRuntimeLogger();
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-idle-mismatch-warning-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                logger,
                config: server.Config,
                sessionId: "character-test-idle-mismatch-warning-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                Tick(server, client, 90);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);
                Assert.GreaterOrEqual(client.Session.FullSnapshotReceivedCount, 1);
                Assert.Greater(client.Session.DigestSnapshotReceivedCount, 0);
                int resyncBeforeMismatch = client.Session.ResyncCount;
                int fullSnapshotBeforeMismatch = client.Session.FullSnapshotReceivedCount;
                int correctionBeforeMismatch = client.CaptureDiagnostics().RollbackSnapshotCorrectionCount;

                CharacterTestCharacterEntity nonLocal = FindNonLocal(client.Slice);
                nonLocal.SetTransform(new CharacterTestCharacterTransformComponent(
                    nonLocal.Transform.X + 3f,
                    nonLocal.Transform.Y,
                    nonLocal.Transform.Z,
                    nonLocal.Transform.YawDegrees));
                server.Session.BroadcastSnapshot(server.Slice.CurrentFrame, fullSnapshot: true);
                Tick(client, 20);

                CharacterTestNetworkLoopbackDiagnostics diagnostics = client.CaptureDiagnostics();
                string correctionFailure =
                    "fullBefore="
                    + fullSnapshotBeforeMismatch
                    + " fullAfter="
                    + client.Session.FullSnapshotReceivedCount
                    + " resyncBefore="
                    + resyncBeforeMismatch
                    + " resyncAfter="
                    + client.Session.ResyncCount
                    + " applyFailures="
                    + diagnostics.SnapshotApplyFailureCount
                    + " corrections="
                    + diagnostics.RollbackSnapshotCorrectionCount
                    + " recent="
                    + diagnostics.RecentDiagnostics;
                Assert.Greater(client.Session.FullSnapshotReceivedCount, fullSnapshotBeforeMismatch, correctionFailure);
                Assert.Greater(client.Session.ResyncCount, resyncBeforeMismatch, correctionFailure);
                Assert.Greater(
                    diagnostics.RollbackSnapshotCorrectionCount,
                    correctionBeforeMismatch,
                    diagnostics.RecentDiagnostics);
                Assert.AreEqual(0, diagnostics.RollbackFailureCount, diagnostics.LastRollbackFailure);

                RuntimeFrame frame = client.Slice.CurrentFrame;
                while (server.Slice.CurrentFrame < frame)
                    server.Tick(1d / 60d);
                Assert.AreEqual(server.Slice.CurrentFrame, frame);
                var serverParticipant = new CharacterTestNetworkSnapshotParticipant(
                    server.Slice.CharacterRuntime,
                    server.Slice.RuntimeCommands);
                var clientParticipant = new CharacterTestNetworkSnapshotParticipant(
                    client.Slice.CharacterRuntime,
                    client.Slice.RuntimeCommands);
                Assert.AreEqual(
                    serverParticipant.ComputeStateHash(frame),
                    clientParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void UnityTransportClientRunner_AttackActivityDoesNotTriggerIdleMismatchWarning()
        {
            var provider = new FakeInputProvider();
            var logger = new CountingRuntimeLogger();
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 2,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources"),
                sessionId: "character-test-attack-mismatch-warning-test",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                logger,
                config: server.Config,
                sessionId: "character-test-attack-mismatch-warning-test",
                serverPeerId: "server",
                clientPeerId: "client-0",
                transportMode: "unity-transport-client-test",
                createClientViews: false,
                connectTargetLabel: "loopback",
                disposeTransport: false))
            {
                client.Slice.SetLocalInputProvider(provider);
                Tick(server, client, 24);
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);

                CharacterTestCharacterEntity local = FindLocalPlayer(client.Slice);
                CharacterTestCharacterEntity nonOwned = FindOtherHostileTarget(
                    client.Slice,
                    local,
                    excludedTargetEntityId: -1);
                int mismatchBefore = client.Session.SnapshotMismatchCount;
                int resyncBefore = client.Session.ResyncCount;
                nonOwned.SetTransform(new CharacterTestCharacterTransformComponent(
                    nonOwned.Transform.X + 3f,
                    nonOwned.Transform.Y,
                    nonOwned.Transform.Z,
                    nonOwned.Transform.YawDegrees));
                provider.SetSnapshot(CreateInputSnapshot(Vector2.zero, attackPrimaryPressed: true));

                Tick(server, client, 60);

                CharacterTestNetworkLoopbackDiagnostics diagnostics = client.CaptureDiagnostics();
                CharacterTestCharacterEntity serverNonOwned = FindEntityById(
                    server.Slice,
                    nonOwned.Id.Value);
                var serverParticipant = new CharacterTestNetworkSnapshotParticipant(
                    server.Slice.CharacterRuntime,
                    server.Slice.RuntimeCommands);
                var clientParticipant = new CharacterTestNetworkSnapshotParticipant(
                    client.Slice.CharacterRuntime,
                    client.Slice.RuntimeCommands);
                string mismatchFailure = "mismatch=" + diagnostics.SnapshotMismatchCount
                    + " suppressed=" + diagnostics.SuppressedSnapshotMismatchCount
                    + " resync=" + diagnostics.ResyncCount
                    + " comparable=" + diagnostics.ComparableSnapshotHashCount
                    + " missing=" + diagnostics.MissingSnapshotHashCount
                    + " full=" + diagnostics.FullSnapshotReceivedCount
                    + " digest=" + diagnostics.DigestSnapshotReceivedCount
                    + " serverFrame=" + server.Slice.CurrentFrame.Value
                    + " clientFrame=" + client.Slice.CurrentFrame.Value
                    + " serverX=" + serverNonOwned.Transform.X
                    + " clientX=" + nonOwned.Transform.X
                    + " serverHash=" + serverParticipant.ComputeStateHash(server.Slice.CurrentFrame)
                    + " clientHash=" + clientParticipant.ComputeStateHash(client.Slice.CurrentFrame);
                Assert.IsTrue(
                    client.Session.SnapshotMismatchCount > mismatchBefore
                    || client.Session.ResyncCount > resyncBefore,
                    "The injected non-owned divergence should be detected or corrected. " + mismatchFailure);
                Assert.AreEqual(0, logger.IdleSuppressedMismatchWarningCount);
            }
        }

        [Test]
        public void RoomLoopback_TwoClientsReadyStartAndReceiveDistinctOwnedEntities()
        {
            var transport = new CodecLoopbackNetworkTransport();
            var gameplayConfig = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 1,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var roomConfig = new NetworkRoomConfig(
                "character-test-room-test",
                "CharacterTest Room Test",
                maxMembers: 4,
                minReadyMembers: 2,
                requireAllReady: true,
                allowLateJoin: false,
                gameplaySessionConfig: gameplayConfig);

            using (var server = new CharacterTestUnityTransportRoomServerRunner(
                transport,
                roomConfig,
                autoStartWhenReady: true,
                logger: NullRuntimeLogger.Instance,
                createServerViews: false,
                disposeTransport: false))
            using (var clientA = new CharacterTestUnityTransportRoomClientRunner(
                transport,
                roomConfig,
                "Alice",
                autoReady: true,
                logger: NullRuntimeLogger.Instance,
                clientPeerId: "client-a",
                createClientViews: false,
                connectTargetLabel: "loopback-room",
                disposeTransport: false))
            using (var clientB = new CharacterTestUnityTransportRoomClientRunner(
                transport,
                roomConfig,
                "Bob",
                autoReady: true,
                logger: NullRuntimeLogger.Instance,
                clientPeerId: "client-b",
                createClientViews: false,
                connectTargetLabel: "loopback-room",
                disposeTransport: false))
            {
                Tick(server, clientA, clientB, 80);

                Assert.AreEqual(NetworkRoomStatus.InGame, server.RoomSession.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, clientA.RoomSession.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, clientB.RoomSession.Status);
                Assert.AreEqual(NetworkSessionStatus.Connected, clientA.GameplaySession.Status);
                Assert.AreEqual(NetworkSessionStatus.Connected, clientB.GameplaySession.Status);

                CharacterTestNetworkLoopbackDiagnostics diagnosticsA = clientA.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics diagnosticsB = clientB.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics serverDiagnostics = server.CaptureDiagnostics();
                string ownershipFailure =
                    "serverOwnership="
                    + serverDiagnostics.OwnershipSummary
                    + " serverPeers="
                    + server.GameplaySession.ConnectedPeerCount
                    + " aStatus="
                    + clientA.GameplaySession.Status
                    + " aOwnership="
                    + diagnosticsA.OwnershipSummary
                    + " aFullSnapshots="
                    + diagnosticsA.FullSnapshotReceivedCount
                    + " bStatus="
                    + clientB.GameplaySession.Status
                    + " bOwnership="
                    + diagnosticsB.OwnershipSummary
                    + " bFullSnapshots="
                    + diagnosticsB.FullSnapshotReceivedCount;
                Assert.Greater(diagnosticsA.OwnedEntityId, 0, ownershipFailure);
                Assert.Greater(diagnosticsB.OwnedEntityId, 0, ownershipFailure);
                Assert.AreNotEqual(diagnosticsA.OwnedEntityId, diagnosticsB.OwnedEntityId);
                Assert.AreEqual(2, server.RoomSession.State.ReadyMemberCount);
                Assert.AreEqual(2, server.RoomSession.State.ConnectedMemberCount);
                Assert.AreEqual(2, server.GameplaySession.ConnectedPeerCount);
                Assert.Greater(diagnosticsA.RoomSentBytes, 0);
                Assert.AreEqual(1, CountLocalPlayers(clientA.Slice), FormatOwners(clientA.Slice));
                Assert.AreEqual(1, CountLocalPlayers(clientB.Slice), FormatOwners(clientB.Slice));
            }
        }

        [Test]
        public void UnityTransportHostRunner_LocalInputExecutesOnHostOwnedEntity()
        {
            var transport = new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate);
            NetworkSessionConfig config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 1,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var provider = new FakeInputProvider();
            using (var host = new CharacterTestUnityTransportHostRunner(
                transport,
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                serverPeerId: "server",
                localClientPeerId: "host-player",
                createClientViews: false,
                disposeTransports: false))
            {
                host.Slice.SetLocalInputProvider(provider);
                Tick(host, 40);

                CharacterTestNetworkLoopbackDiagnostics diagnostics = host.CaptureDiagnostics();
                Assert.AreEqual(NetworkSessionStatus.Connected, diagnostics.ClientStatus);
                Assert.AreEqual(1, diagnostics.ConnectedPeerCount);
                Assert.Greater(diagnostics.OwnedEntityId, 0, diagnostics.OwnershipSummary);
                Assert.AreEqual(diagnostics.OwnedEntityId, FindLocalPlayer(host.Slice).Id.Value);
                DisableAiControlledCharacters(host.ServerRunner.Slice);

                provider.SetSnapshot(CreateInputSnapshot(
                    new Vector2(0.5f, -0.25f),
                    jumpPressed: true,
                    jumpHeld: true,
                    attackPrimaryPressed: true));
                Tick(host, 20);

                CharacterTestCharacterEntity serverOwned =
                    FindEntityById(host.ServerRunner.Slice, diagnostics.OwnedEntityId);
                CharacterTestCharacterEntity hostLocal = FindLocalPlayer(host.Slice);
                CharacterTestNetworkLoopbackDiagnostics inputDiagnostics = host.CaptureDiagnostics();
                string inputFailure = FormatInputDiagnostics(inputDiagnostics, inputDiagnostics)
                    + " serverInputs=" + FormatInputs(host.ServerRunner.Slice)
                    + " clientInputs=" + FormatInputs(host.Slice);
                Assert.AreEqual(
                    0.5f,
                    serverOwned.Input.Move.x,
                    0.001f,
                    inputFailure);
                Assert.AreEqual(-0.25f, serverOwned.Input.Move.y, 0.001f);
                Assert.IsTrue(serverOwned.Input.JumpPressed);
                Assert.IsTrue(serverOwned.Input.AttackPrimaryPressed);
                Assert.AreEqual(
                    serverOwned.Input.Move.x,
                    hostLocal.Input.Move.x,
                    0.001f,
                    inputFailure);
                Assert.AreEqual(serverOwned.Input.Move.y, hostLocal.Input.Move.y, 0.001f);
            }
        }

        [Test]
        public void UnityTransportHostRunner_RemoteClientGetsDistinctOwnership()
        {
            var transport = new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate);
            NetworkSessionConfig config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 1,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var hostProvider = new FakeInputProvider();
            var remoteProvider = new FakeInputProvider();
            using (var host = new CharacterTestUnityTransportHostRunner(
                transport,
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-host-with-remote",
                serverPeerId: "server",
                localClientPeerId: "host-player",
                createClientViews: false,
                disposeTransports: false))
            using (var remote = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                sessionId: "character-test-host-with-remote",
                serverPeerId: "server",
                clientPeerId: "remote-player",
                transportMode: "loopback-remote-client",
                createClientViews: false,
                connectTargetLabel: "loopback-host",
                disposeTransport: false))
            {
                host.Slice.SetLocalInputProvider(hostProvider);
                remote.Slice.SetLocalInputProvider(remoteProvider);
                Tick(host, remote, 60);

                CharacterTestNetworkLoopbackDiagnostics hostDiagnostics = host.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics remoteDiagnostics = remote.CaptureDiagnostics();
                Assert.AreEqual(2, hostDiagnostics.ConnectedPeerCount, hostDiagnostics.OwnershipSummary);
                Assert.Greater(hostDiagnostics.OwnedEntityId, 0, hostDiagnostics.OwnershipSummary);
                Assert.Greater(remoteDiagnostics.OwnedEntityId, 0, remoteDiagnostics.OwnershipSummary);
                Assert.AreNotEqual(hostDiagnostics.OwnedEntityId, remoteDiagnostics.OwnedEntityId);
                Assert.AreEqual(1, CountLocalPlayers(host.Slice), FormatOwners(host.Slice));
                Assert.AreEqual(1, CountLocalPlayers(remote.Slice), FormatOwners(remote.Slice));

                hostProvider.SetSnapshot(CreateInputSnapshot(Vector2.right, attackPrimaryPressed: true));
                remoteProvider.SetSnapshot(CreateInputSnapshot(Vector2.left, jumpPressed: true, jumpHeld: true));
                Tick(host, remote, 30);

                CharacterTestCharacterEntity serverHost =
                    FindEntityById(host.ServerRunner.Slice, hostDiagnostics.OwnedEntityId);
                CharacterTestCharacterEntity serverRemote =
                    FindEntityById(host.ServerRunner.Slice, remoteDiagnostics.OwnedEntityId);
                Assert.AreEqual(1f, serverHost.Input.Move.x, 0.001f);
                Assert.IsTrue(serverHost.Input.AttackPrimaryPressed);
                Assert.AreEqual(-1f, serverRemote.Input.Move.x, 0.001f);
                Assert.IsTrue(serverRemote.Input.JumpPressed);
            }
        }

        [Test]
        public void UnityTransportHostRunner_DefaultSessionId_AllowsDefaultRemoteClientHandshake()
        {
            var transport = new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate);
            NetworkSessionConfig config = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 1,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            using (var host = new CharacterTestUnityTransportHostRunner(
                transport,
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                serverPeerId: "server",
                localClientPeerId: "host-player",
                createClientViews: false,
                disposeTransports: false))
            using (var remote = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: config,
                serverPeerId: "server",
                clientPeerId: "remote-player",
                transportMode: "loopback-remote-client",
                createClientViews: false,
                connectTargetLabel: "loopback-host",
                disposeTransport: false))
            {
                Tick(host, remote, 60);

                Assert.AreEqual(CharacterTestUnityTransportSessionIds.DirectLan, host.ServerSession.SessionId);
                Assert.AreEqual(host.ServerSession.SessionId, remote.Session.SessionId);
                CharacterTestNetworkLoopbackDiagnostics remoteDiagnostics = remote.CaptureDiagnostics();
                Assert.AreEqual(
                    NetworkSessionStatus.Connected,
                    remote.Session.Status,
                    string.Join(" | ", remote.Session.RecentDiagnostics));
                Assert.AreEqual(2, host.ServerSession.ConnectedPeerCount, host.CaptureDiagnostics().OwnershipSummary);
                Assert.Greater(remoteDiagnostics.OwnedEntityId, 0, remoteDiagnostics.OwnershipSummary);
            }
        }

        [Test]
        public void UnityTransportHostRunner_RealTransportRemoteClientGetsDistinctOwnership()
        {
            ushort port = GetAvailableUdpPort();
            using (var serverTransport = new UnityTransportNetworkTransport())
            using (var hostClientTransport = new UnityTransportNetworkTransport())
            using (var remoteClientTransport = new UnityTransportNetworkTransport())
            {
                Assert.IsTrue(serverTransport.StartServer(port, "127.0.0.1"));
                Assert.IsTrue(hostClientTransport.StartClient("127.0.0.1", port, "server"));
                Assert.IsTrue(remoteClientTransport.StartClient("127.0.0.1", port, "server"));

                NetworkSessionConfig config = new NetworkSessionConfig(
                    protocolVersion: 1,
                    tickRate: 60,
                    inputDelayFrames: 1,
                    snapshotIntervalFrames: 1,
                    configHash: "character-test",
                    resourceCatalogHash: "character-test-resources");
                var hostProvider = new FakeInputProvider();
                var remoteProvider = new FakeInputProvider();
                using (var host = new CharacterTestUnityTransportHostRunner(
                    serverTransport,
                    hostClientTransport,
                    NullRuntimeLogger.Instance,
                    config: config,
                    serverPeerId: "server",
                    localClientPeerId: "host-player",
                    createClientViews: false,
                    connectTargetLabel: "127.0.0.1:" + port,
                    disposeTransports: false))
                using (var remote = new CharacterTestUnityTransportClientRunner(
                    remoteClientTransport,
                    NullRuntimeLogger.Instance,
                    config: config,
                    serverPeerId: "server",
                    clientPeerId: "remote-player",
                    transportMode: "unity-transport-remote-client",
                    createClientViews: false,
                    connectTargetLabel: "127.0.0.1:" + port,
                    disposeTransport: false))
                {
                    host.Slice.SetLocalInputProvider(hostProvider);
                    remote.Slice.SetLocalInputProvider(remoteProvider);
                    Tick(host, remote, 120);

                    CharacterTestNetworkLoopbackDiagnostics hostDiagnostics = host.CaptureDiagnostics();
                    CharacterTestNetworkLoopbackDiagnostics remoteDiagnostics = remote.CaptureDiagnostics();
                    string failure =
                        "hostClientStatus="
                        + host.LocalClientSession.Status
                        + " remoteStatus="
                        + remote.Session.Status
                        + " serverPeers="
                        + host.ServerSession.ConnectedPeerCount
                        + " ownership="
                        + hostDiagnostics.OwnershipSummary
                        + " hostOwned="
                        + hostDiagnostics.OwnedEntityId
                        + " remoteOwned="
                        + remoteDiagnostics.OwnedEntityId
                        + " hostFullSnapshots="
                        + hostDiagnostics.FullSnapshotReceivedCount
                        + " remoteFullSnapshots="
                        + remoteDiagnostics.FullSnapshotReceivedCount
                        + " remoteDiagnostics="
                        + string.Join(" | ", remote.Session.RecentDiagnostics);
                    Assert.AreEqual(NetworkSessionStatus.Connected, host.LocalClientSession.Status, failure);
                    Assert.AreEqual(NetworkSessionStatus.Connected, remote.Session.Status, failure);
                    Assert.AreEqual(2, host.ServerSession.ConnectedPeerCount, failure);
                    Assert.Greater(hostDiagnostics.OwnedEntityId, 0, failure);
                    Assert.Greater(remoteDiagnostics.OwnedEntityId, 0, failure);
                    Assert.AreNotEqual(hostDiagnostics.OwnedEntityId, remoteDiagnostics.OwnedEntityId, failure);
                    Assert.AreEqual(1, CountLocalPlayers(host.Slice), FormatOwners(host.Slice));
                    Assert.AreEqual(1, CountLocalPlayers(remote.Slice), FormatOwners(remote.Slice));

                    hostProvider.SetSnapshot(CreateInputSnapshot(Vector2.right, attackPrimaryPressed: true));
                    remoteProvider.SetSnapshot(CreateInputSnapshot(Vector2.left, jumpPressed: true, jumpHeld: true));
                    Tick(host, remote, 60);

                    CharacterTestCharacterEntity serverHost =
                        FindEntityById(host.ServerRunner.Slice, hostDiagnostics.OwnedEntityId);
                    CharacterTestCharacterEntity serverRemote =
                        FindEntityById(host.ServerRunner.Slice, remoteDiagnostics.OwnedEntityId);
                    Assert.Greater(serverHost.Input.Move.x, 0.5f);
                    Assert.IsTrue(serverHost.Input.AttackPrimaryPressed);
                    Assert.Less(serverRemote.Input.Move.x, -0.5f);
                    Assert.IsTrue(serverRemote.Input.JumpPressed);
                }
            }
        }

        [Test]
        public void UnityTransportHostRunner_DisposeReleasesServerAndLocalTransports()
        {
            var serverTransport = new DisposableNetworkTransport();
            var localClientTransport = new DisposableNetworkTransport();
            var host = new CharacterTestUnityTransportHostRunner(
                serverTransport,
                localClientTransport,
                NullRuntimeLogger.Instance,
                createClientViews: false);

            host.Dispose();

            Assert.AreEqual(1, serverTransport.DisposeCount);
            Assert.AreEqual(1, localClientTransport.DisposeCount);
            Assert.Throws<ObjectDisposedException>(() => host.Tick(0.016));
            Assert.Throws<ObjectDisposedException>(() => host.CaptureDiagnostics());
        }

        [Test]
        public void RoomHostRunner_ManualReadyAndStartGateGameplay()
        {
            var transport = new CodecLoopbackNetworkTransport();
            var gameplayConfig = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 1,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var roomConfig = new NetworkRoomConfig(
                "character-test-room-host-manual-test",
                "CharacterTest Host Room Manual Test",
                maxMembers: 4,
                minReadyMembers: 2,
                requireAllReady: true,
                allowLateJoin: false,
                gameplaySessionConfig: gameplayConfig);

            var remoteLogger = new CountingRuntimeLogger();
            var hostProvider = new FakeInputProvider();
            var remoteProvider = new FakeInputProvider();
            using (var host = new CharacterTestUnityTransportRoomHostRunner(
                transport,
                transport,
                roomConfig,
                "Host",
                autoReady: false,
                autoStartWhenReady: false,
                logger: NullRuntimeLogger.Instance,
                serverPeerId: "server",
                localClientPeerId: "host-player",
                createClientViews: false,
                disposeTransports: false))
            using (var remote = new CharacterTestUnityTransportRoomClientRunner(
                transport,
                roomConfig,
                "Remote",
                autoReady: false,
                logger: remoteLogger,
                clientPeerId: "remote-player",
                createClientViews: false,
                connectTargetLabel: "loopback-room-host",
                disposeTransport: false))
            {
                Tick(host, remote, 40);

                Assert.AreEqual(NetworkRoomStatus.Open, host.ServerRoomSession.Status);
                Assert.AreEqual(2, host.ServerRoomSession.State.ConnectedMemberCount);
                Assert.AreEqual(0, host.ServerRoomSession.State.ReadyMemberCount);
                Assert.IsTrue(host.CanSetRoomReady);
                Assert.IsTrue(remote.CanSetRoomReady);
                Assert.IsFalse(host.CanStartRoomGame);
                Assert.AreEqual(NetworkSessionStatus.Disconnected, host.LocalClientGameplaySession.Status);
                Assert.AreEqual(NetworkSessionStatus.Disconnected, remote.GameplaySession.Status);

                Assert.IsTrue(host.TrySetLocalRoomReady(true, out string hostReadyReason), hostReadyReason);
                Assert.IsTrue(remote.TrySetLocalRoomReady(true, out string remoteReadyReason), remoteReadyReason);
                Tick(host, remote, 30);

                Assert.AreEqual(NetworkRoomStatus.Open, host.ServerRoomSession.Status);
                Assert.AreEqual(2, host.ServerRoomSession.State.ReadyMemberCount);
                Assert.IsTrue(host.LocalRoomReady);
                Assert.IsTrue(remote.LocalRoomReady);
                Assert.IsTrue(host.CanStartRoomGame);

                Assert.IsTrue(host.TryStartRoomGame(out string startReason), startReason);
                Tick(host, remote, 59);

                Assert.AreEqual(NetworkRoomStatus.InGame, host.ServerRoomSession.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, host.LocalClientRoomSession.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, remote.RoomSession.Status);
                Assert.AreEqual(NetworkSessionStatus.Connected, host.LocalClientGameplaySession.Status);
                Assert.AreEqual(NetworkSessionStatus.Connected, remote.GameplaySession.Status);
                Assert.AreEqual(0, remoteLogger.NetworkConnectionWarningCount);
                Assert.AreEqual(0, remoteLogger.DisconnectedActiveInputWarningCount);

                CharacterTestNetworkLoopbackDiagnostics hostDiagnostics = host.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics remoteDiagnostics = remote.CaptureDiagnostics();
                Assert.Greater(hostDiagnostics.OwnedEntityId, 0, hostDiagnostics.OwnershipSummary);
                Assert.Greater(remoteDiagnostics.OwnedEntityId, 0, remoteDiagnostics.OwnershipSummary);
                Assert.AreNotEqual(hostDiagnostics.OwnedEntityId, remoteDiagnostics.OwnedEntityId);
                Assert.LessOrEqual(Math.Abs(hostDiagnostics.FrameDriftFrames), 2, "Host local client should not lag server gameplay frames.");
                DisableAiControlledCharacters(host.ServerRunner.Slice);

                host.Slice.SetLocalInputProvider(hostProvider);
                remote.Slice.SetLocalInputProvider(remoteProvider);
                hostProvider.SetSnapshot(CreateInputSnapshot(Vector2.right, attackPrimaryPressed: true));
                remoteProvider.SetSnapshot(CreateInputSnapshot(Vector2.left, jumpPressed: true, jumpHeld: true));
                Tick(host, remote, 30);

                CharacterTestCharacterEntity serverHost =
                    FindEntityById(host.ServerRunner.Slice, hostDiagnostics.OwnedEntityId);
                CharacterTestCharacterEntity serverRemote =
                    FindEntityById(host.ServerRunner.Slice, remoteDiagnostics.OwnedEntityId);
                Assert.Greater(serverHost.Input.Move.x, 0.5f);
                Assert.IsTrue(serverHost.Input.AttackPrimaryPressed);
                CharacterTestNetworkLoopbackDiagnostics postInputHostDiagnostics = host.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics postInputRemoteDiagnostics = remote.CaptureDiagnostics();
                Assert.Less(
                    serverRemote.Input.Move.x,
                    -0.5f,
                    FormatInputDiagnostics(postInputHostDiagnostics, postInputRemoteDiagnostics)
                        + " serverInputs=" + FormatInputs(host.ServerRunner.Slice)
                        + " hostInputs=" + FormatInputs(host.Slice)
                        + " remoteInputs=" + FormatInputs(remote.Slice));
                Assert.IsTrue(serverRemote.Input.JumpPressed);

                CharacterTestCharacterEntity hostMirrorRemote =
                    FindEntityById(host.Slice, remoteDiagnostics.OwnedEntityId);
                CharacterTestCharacterEntity remoteMirrorHost =
                    FindEntityById(remote.Slice, hostDiagnostics.OwnedEntityId);
                CharacterTestCharacterEntity hostLocal =
                    FindEntityById(host.Slice, hostDiagnostics.OwnedEntityId);
                Assert.Less(
                    hostMirrorRemote.Input.Move.x,
                    -0.5f,
                    FormatOwners(host.Slice)
                        + " | "
                        + FormatInputDiagnostics(postInputHostDiagnostics, postInputRemoteDiagnostics));
                Assert.IsTrue(hostMirrorRemote.Input.JumpPressed);
                Assert.Greater(remoteMirrorHost.Input.Move.x, 0.5f, FormatOwners(remote.Slice));
                Assert.IsTrue(remoteMirrorHost.Input.AttackPrimaryPressed);
                Assert.Greater(hostLocal.Animation.LocomotionSpeed01, 0.1f);
            }
        }

        [Test]
        public void RoomHostRunner_LocalAndRemoteMembersStartAndReceiveDistinctOwnedEntities()
        {
            var transport = new CodecLoopbackNetworkTransport();
            var gameplayConfig = new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 1,
                snapshotIntervalFrames: 1,
                configHash: "character-test",
                resourceCatalogHash: "character-test-resources");
            var roomConfig = new NetworkRoomConfig(
                "character-test-room-host-test",
                "CharacterTest Host Room Test",
                maxMembers: 4,
                minReadyMembers: 2,
                requireAllReady: true,
                allowLateJoin: false,
                gameplaySessionConfig: gameplayConfig);

            using (var host = new CharacterTestUnityTransportRoomHostRunner(
                transport,
                transport,
                roomConfig,
                "Host",
                autoReady: true,
                autoStartWhenReady: true,
                logger: NullRuntimeLogger.Instance,
                serverPeerId: "server",
                localClientPeerId: "host-player",
                createClientViews: false,
                disposeTransports: false))
            using (var remote = new CharacterTestUnityTransportRoomClientRunner(
                transport,
                roomConfig,
                "Remote",
                autoReady: true,
                logger: NullRuntimeLogger.Instance,
                clientPeerId: "remote-player",
                createClientViews: false,
                connectTargetLabel: "loopback-room-host",
                disposeTransport: false))
            {
                Tick(host, remote, 120);

                Assert.AreEqual(NetworkRoomStatus.InGame, host.ServerRoomSession.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, host.LocalClientRoomSession.Status);
                Assert.AreEqual(NetworkRoomStatus.InGame, remote.RoomSession.Status);
                Assert.AreEqual(NetworkSessionStatus.Connected, host.LocalClientGameplaySession.Status);
                Assert.AreEqual(NetworkSessionStatus.Connected, remote.GameplaySession.Status);
                Assert.IsNotNull(host.ServerRoomSession.State.FindMember("host-player"));
                Assert.IsNotNull(host.ServerRoomSession.State.FindMember("remote-player"));

                CharacterTestNetworkLoopbackDiagnostics hostDiagnostics = host.CaptureDiagnostics();
                CharacterTestNetworkLoopbackDiagnostics remoteDiagnostics = remote.CaptureDiagnostics();
                Assert.AreEqual(2, hostDiagnostics.RoomReadyCount, hostDiagnostics.RoomMembers);
                Assert.AreEqual(2, hostDiagnostics.RoomConnectedCount, hostDiagnostics.RoomMembers);
                Assert.AreEqual(2, hostDiagnostics.ConnectedPeerCount, hostDiagnostics.OwnershipSummary);
                Assert.Greater(hostDiagnostics.OwnedEntityId, 0, hostDiagnostics.OwnershipSummary);
                Assert.Greater(remoteDiagnostics.OwnedEntityId, 0, remoteDiagnostics.OwnershipSummary);
                Assert.AreNotEqual(hostDiagnostics.OwnedEntityId, remoteDiagnostics.OwnedEntityId);
                Assert.AreEqual(1, CountLocalPlayers(host.Slice), FormatOwners(host.Slice));
                Assert.AreEqual(1, CountLocalPlayers(remote.Slice), FormatOwners(remote.Slice));
            }
        }

        [Test]
        public void SnapshotSchemaMismatch_ReturnsStructuredFailure()
        {
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null);

            NetworkSnapshotApplyResult result = participant.ApplySnapshot(
                RuntimeFrame.Zero,
                new RuntimeCustomState(
                    CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                    CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue + 1,
                    "{}"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("schema mismatch", result.Message);
        }

        private static void Tick(CharacterTestNetworkLoopbackRunner runner, int count)
        {
            for (int i = 0; i < count; i++)
                runner.Tick(1d / 60d);
        }

        private static void Tick(CharacterTestNetworkSessionRunner runner, int count)
        {
            for (int i = 0; i < count; i++)
                runner.Tick(1d / 60d);
        }

        private static void Tick(CharacterTestUnityTransportHostRunner host, int count)
        {
            for (int i = 0; i < count; i++)
                host.Tick(1d / 60d);
        }

        private static void Tick(CharacterTestUnityTransportServerRunner server, int count)
        {
            for (int i = 0; i < count; i++)
                server.Tick(1d / 60d);
        }

        private static void Tick(
            CharacterTestUnityTransportHostRunner host,
            CharacterTestUnityTransportClientRunner client,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                host.Tick(1d / 60d);
                client.Tick(1d / 60d);
            }
        }

        private static void Tick(
            CharacterTestUnityTransportRoomHostRunner host,
            CharacterTestUnityTransportRoomClientRunner client,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                host.Tick(1d / 60d);
                client.Tick(1d / 60d);
            }
        }

        private static void Tick(
            CharacterTestUnityTransportServerRunner server,
            CharacterTestUnityTransportClientRunner client,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                server.Tick(1d / 60d);
                client.Tick(1d / 60d);
            }
        }

        private static void Tick(
            CharacterTestUnityTransportServerRunner server,
            CharacterTestUnityTransportClientRunner clientA,
            CharacterTestUnityTransportClientRunner clientB,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                server.Tick(1d / 60d);
                clientA.Tick(1d / 60d);
                clientB.Tick(1d / 60d);
            }
        }

        private static void Tick(
            CharacterTestUnityTransportRoomServerRunner server,
            CharacterTestUnityTransportRoomClientRunner clientA,
            CharacterTestUnityTransportRoomClientRunner clientB,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                server.Tick(1d / 60d);
                clientA.Tick(1d / 60d);
                clientB.Tick(1d / 60d);
            }
        }

        private static void Tick(CharacterTestUnityTransportClientRunner client, int count)
        {
            for (int i = 0; i < count; i++)
                client.Tick(1d / 60d);
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / 60d);
        }

        private static InputSnapshot CreateInputSnapshot(
            Vector2 move,
            bool jumpPressed = false,
            bool jumpHeld = false,
            bool jumpReleased = false,
            bool attackPrimaryPressed = false,
            bool attackSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            bool debugSecondaryPressed = false)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackPrimaryHeld: false,
                attackSecondaryPressed,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false,
                debugPrimaryPressed: debugPrimaryPressed,
                debugSecondaryPressed: debugSecondaryPressed);
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(GameSlice slice)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            Assert.Fail("Missing local player entity.");
            return null;
        }

        private static CharacterTestCharacterEntity FindOtherHostileTarget(
            GameSlice slice,
            CharacterTestCharacterEntity primary,
            int excludedTargetEntityId)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Id.Value == primary.Id.Value || entity.Id.Value == excludedTargetEntityId)
                    continue;
                if (entity.Team.TeamId == primary.Team.TeamId)
                    continue;
                if (!entity.CombatState.IsAlive || !entity.CombatState.CanReceiveCombatHit)
                    continue;
                return entity;
            }

            Assert.Fail("Missing secondary hostile target.");
            return null;
        }

        private static CombatProjectileRuntimeState SpawnFireballProjectile(GameSlice slice)
        {
            CharacterTestCharacterEntity localPlayer = FindLocalPlayer(slice);
            localPlayer.SetTransform(new CharacterTestCharacterTransformComponent(
                localPlayer.Transform.X,
                localPlayer.Transform.Y,
                localPlayer.Transform.Z,
                yawDegrees: 0f));

            Assert.IsTrue(slice.ActionRuntime.TryStartIntent(
                localPlayer,
                CharacterTestCharacterActionConfigSource.FireballIntentId,
                slice.CurrentFrame));

            CombatProjectileRuntimeState state = null;
            for (int i = 0; i < 20; i++)
            {
                Tick(slice, 1);
                state = slice.CombatTraceSystem.CreateProjectileStateSnapshot();
                if (state != null && state.ActiveCount > 0)
                    break;
            }

            Assert.IsNotNull(state);
            Assert.AreEqual(1, state.ActiveCount);
            return state;
        }

        private static CharacterTestCharacterPhysicsRuntime CreateHeadlessPhysicsRuntime()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(100),
                new CombatBodyId(100),
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(100),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20))));

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "network-test");
        }

        private static string RemoveJsonTailProperty(string json, string propertyName)
        {
            string token = ",\"" + propertyName + "\":";
            int start = json.IndexOf(token, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Expected JSON property was not found: " + propertyName);
            int end = json.LastIndexOf('}');
            Assert.Greater(end, start);
            return json.Substring(0, start) + json.Substring(end);
        }

        private static string RemoveCharacterPhysicsFields(string json)
        {
            JObject document = JObject.Parse(json);
            JArray characters = (JArray)document["characters"];
            Assert.NotNull(characters);
            string[] propertyNames =
            {
                "physicsCapsuleRadiusMillimeters",
                "physicsCapsuleHeightMillimeters",
                "physicsCapsuleCenterYMillimeters",
                "physicsMassGrams",
                "physicsMassScaleMilli",
                "physicsRadiusScaleMilli",
                "physicsPushScaleMilli",
                "physicsBlocksCharacters",
                "physicsCanBePushed",
                "physicsContactVelocityXMillimetersPerSecond",
                "physicsContactVelocityYMillimetersPerSecond",
                "physicsContactDisplacementXMillimeters",
                "physicsContactDisplacementYMillimeters",
                "physicsContactCount"
            };
            for (int i = 0; i < characters.Count; i++)
            {
                var character = (JObject)characters[i];
                for (int propertyIndex = 0; propertyIndex < propertyNames.Length; propertyIndex++)
                    Assert.IsTrue(character.Remove(propertyNames[propertyIndex]), "Expected character physics property was not found: " + propertyNames[propertyIndex]);
            }

            return document.ToString(Formatting.None);
        }

        private static CharacterTestCharacterEntity FindNonLocal(GameSlice slice)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            Assert.Fail("Missing non-local entity.");
            return null;
        }

        private static CharacterTestCharacterEntity FindEntityById(GameSlice slice, int entityId)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Id.Value == entityId)
                    return entity;
            }

            Assert.Fail("Missing entity id: " + entityId);
            return null;
        }

        private static int CountLocalPlayers(GameSlice slice)
        {
            int count = 0;
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                if (slice.CharacterEntities[i].Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    count++;
            }

            return count;
        }

        private static string FormatOwners(GameSlice slice)
        {
            string text = string.Empty;
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (text.Length > 0)
                    text += ",";
                text += entity.Id.Value + ":" + entity.Control.Owner;
            }

            return text;
        }

        private static void DisableAiControlledCharacters(GameSlice slice)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled)
                {
                    Assert.IsTrue(slice.CharacterRuntime.TrySetControlOwner(
                        entity.Id.Value,
                        CharacterTestCharacterControlOwner.None));
                }
            }
        }

        private static string FormatInputs(GameSlice slice)
        {
            string text = string.Empty;
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (text.Length > 0)
                    text += ",";
                text += entity.Id.Value + ":" + entity.Control.Owner
                    + "@" + entity.Input.Move.x + "/" + entity.Input.Move.y;
            }

            return text;
        }

        private static string FormatInputDiagnostics(
            CharacterTestNetworkLoopbackDiagnostics server,
            CharacterTestNetworkLoopbackDiagnostics client)
        {
            return "serverOwned=" + server.OwnedEntityId
                + " clientOwned=" + client.OwnedEntityId
                + " sent=" + server.AuthoritativeCommandSentCount
                + " received=" + client.AuthoritativeCommandReceivedCount
                + " echo=" + client.AuthoritativeCommandEchoCount
                + " enqueued=" + client.AuthoritativeCommandEnqueuedCount
                + " late=" + client.AuthoritativeCommandLateCount
                + " dropped=" + client.AuthoritativeCommandDroppedCount
                + " serverFrame=" + server.ServerFrame.Value
                + " clientFrame=" + client.ClientLastServerFrame.Value
                + " ownership=" + client.OwnershipSummary;
        }

        private static string FormatCommandErrors(GameSlice slice)
        {
            var errors = slice.RuntimeCommands.LastCommandErrors;
            if (errors == null || errors.Count == 0)
                return "none";

            string text = string.Empty;
            for (int i = 0; i < errors.Count; i++)
            {
                if (i > 0)
                    text += " | ";
                text += errors[i].Message;
            }

            return text;
        }

        private static string FormatDrainedCommands(GameSlice slice)
        {
            var commands = slice.RuntimeCommands.LastDrainedCommands;
            if (commands == null || commands.Count == 0)
                return "none";

            string text = string.Empty;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (i > 0)
                    text += " | ";
                text += command.CommandId
                    + "@" + command.Frame.Value
                    + " target=" + command.TargetId
                    + " p0=" + command.Payload0;
            }

            return text;
        }

        private static CharacterTestNetworkLoopbackDiagnostics CreateSyncProbeDiagnostics(
            int ownedEntityId = 1,
            int authoritativeCommandReceivedCount = 0,
            int authoritativeCommandEchoCount = 0,
            int authoritativeCommandEnqueuedCount = 0,
            int authoritativeCommandLateCount = 0,
            int authoritativeCommandDroppedCount = 0,
            int snapshotRequestCount = 0,
            int fullSnapshotSentCount = 0,
            int fullSnapshotReceivedCount = 1,
            int digestSnapshotSentCount = 0,
            int digestSnapshotReceivedCount = 0,
            long runtimeCommandSentBytes = 0L,
            long snapshotSentBytes = 0L,
            NetworkStateSyncPolicy stateSyncPolicy = NetworkStateSyncPolicy.BootstrapOnly)
        {
            return new CharacterTestNetworkLoopbackDiagnostics(
                available: true,
                serverStatus: NetworkSessionStatus.Connected,
                clientStatus: NetworkSessionStatus.Connected,
                serverFrame: RuntimeFrame.Zero,
                clientLastServerFrame: RuntimeFrame.Zero,
                lastAckedInputFrame: RuntimeFrame.Zero,
                rttEstimateFrames: 2,
                pendingRuntimeCommands: 0,
                pendingTransportMessages: 0,
                lastSnapshotHash: 0L,
                resyncCount: 0,
                snapshotApplyFailureCount: 0,
                transportMode: "test",
                transportId: "sync-probe-test",
                localPeerId: "client-test",
                ownedEntityId: ownedEntityId,
                connectedPeerCount: 1,
                ownershipSummary: "client-test->" + ownedEntityId,
                snapshotRequestCount: snapshotRequestCount,
                authoritativeCommandReceivedCount: authoritativeCommandReceivedCount,
                authoritativeCommandEchoCount: authoritativeCommandEchoCount,
                authoritativeCommandEnqueuedCount: authoritativeCommandEnqueuedCount,
                authoritativeCommandLateCount: authoritativeCommandLateCount,
                authoritativeCommandDroppedCount: authoritativeCommandDroppedCount,
                fullSnapshotSentCount: fullSnapshotSentCount,
                digestSnapshotSentCount: digestSnapshotSentCount,
                fullSnapshotReceivedCount: fullSnapshotReceivedCount,
                digestSnapshotReceivedCount: digestSnapshotReceivedCount,
                runtimeCommandSentBytes: runtimeCommandSentBytes,
                snapshotSentBytes: snapshotSentBytes,
                stateSyncPolicy: stateSyncPolicy,
                heartbeatIntervalFrames: NetworkSessionConfig.DefaultHeartbeatIntervalFrames);
        }

        private static void AssertDrainedCommand(GameSlice slice, int commandId)
        {
            for (int i = 0; i < slice.RuntimeCommands.LastDrainedCommands.Count; i++)
            {
                if (slice.RuntimeCommands.LastDrainedCommands[i].CommandId == commandId)
                    return;
            }

            Assert.Fail("Missing drained command id: " + commandId);
        }

        private static void AssertLastCommandErrorContains(GameSlice slice, string message)
        {
            for (int i = 0; i < slice.RuntimeCommands.LastCommandErrors.Count; i++)
            {
                if (slice.RuntimeCommands.LastCommandErrors[i].Message.Contains(message))
                    return;
            }

            Assert.Fail("Missing command error containing: " + message);
        }

        private static void WaitForUnityTransportConnection(
            UnityTransportNetworkTransport serverTransport,
            UnityTransportNetworkTransport clientTransport)
        {
            var events = new System.Collections.Generic.List<NetworkTransportEvent>();
            for (int i = 0; i < 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                events.Clear();
                serverTransport.PollEvents("server", frame, events);
                clientTransport.PollEvents("client-0", frame, events);
                for (int j = 0; j < events.Count; j++)
                {
                    if (events[j].Kind == NetworkTransportEventKind.Connected
                        && string.Equals(events[j].PeerId, "server", System.StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            Assert.Fail("Unity Transport client did not connect to loopback server.");
        }

        private static ushort GetAvailableUdpPort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        private static void DestroyObjectNamed(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            if (gameObject != null)
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private sealed class CountingRuntimeLogger : IRuntimeLogger
        {
            public int LocalInputWarningCount { get; private set; }
            public int NetworkConnectionWarningCount { get; private set; }
            public int DisconnectedActiveInputWarningCount { get; private set; }
            public int ServerSnapshotStallWarningCount { get; private set; }
            public int IdleSuppressedMismatchWarningCount { get; private set; }

            public void Log(RuntimeLogLevel level, string category, string message)
            {
                if (level == RuntimeLogLevel.Warning
                    && string.Equals(category, "CharacterTest.LocalInputCommands", System.StringComparison.Ordinal)
                    && message.Contains("forced failure"))
                {
                    LocalInputWarningCount++;
                }

                if (level == RuntimeLogLevel.Warning
                    && string.Equals(category, "CharacterTest.Network", System.StringComparison.Ordinal)
                    && message.Contains("Unity Transport client is not connected"))
                {
                    NetworkConnectionWarningCount++;
                }

                if (level == RuntimeLogLevel.Warning
                    && string.Equals(category, "CharacterTest.Network", System.StringComparison.Ordinal)
                    && message.Contains("Local control input ignored"))
                {
                    DisconnectedActiveInputWarningCount++;
                }

                if (level == RuntimeLogLevel.Warning
                    && string.Equals(category, "CharacterTest.Network", System.StringComparison.Ordinal)
                    && message.Contains("authoritative snapshots stopped"))
                {
                    ServerSnapshotStallWarningCount++;
                }

                if (level == RuntimeLogLevel.Warning
                    && string.Equals(category, "CharacterTest.Network", System.StringComparison.Ordinal)
                    && message.Contains("Idle client prediction hash mismatches are growing"))
                {
                    IdleSuppressedMismatchWarningCount++;
                }
            }
        }

        private sealed class DisposableNetworkTransport : INetworkTransport, IDisposable
        {
            private readonly LoopbackNetworkTransport _inner =
                new LoopbackNetworkTransport(LoopbackNetworkTransportOptions.ReliableImmediate);

            public int DisposeCount { get; private set; }

            public string TransportId => "disposable-" + _inner.TransportId;

            public void Send(NetworkMessageEnvelope message)
            {
                _inner.Send(message);
            }

            public int Poll(
                string receiverPeerId,
                RuntimeFrame frame,
                System.Collections.Generic.List<NetworkMessageEnvelope> output)
            {
                return _inner.Poll(receiverPeerId, frame, output);
            }

            public int PollEvents(
                string peerId,
                RuntimeFrame frame,
                System.Collections.Generic.List<NetworkTransportEvent> output)
            {
                return _inner.PollEvents(peerId, frame, output);
            }

            public NetworkTransportSnapshot CreateSnapshot(string peerId)
            {
                return _inner.CreateSnapshot(peerId);
            }

            public void Clear()
            {
                _inner.Clear();
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
