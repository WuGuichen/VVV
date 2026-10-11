using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>
    /// CD-M1 authority-state checks for the explicit TurnInPlace control.
    /// These tests keep the state path separate from the attack-geometry
    /// integration tests: the authority contract is snapshot, hash, rollback,
    /// replay, and wire-codec behavior.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM1AuthorityStateTests
    {
        [Test]
        public void Snapshot_CapturesAndAppliesTurnInPlaceFields()
        {
            using (GameSlice source = CreateSlice())
            using (GameSlice destination = CreateSlice())
            {
                Tick(source, 8);
                Tick(destination, 8);
                CharacterTestCharacterEntity sourceEntity = FindPlayer(source);
                CharacterTestCharacterEntity destinationEntity = FindPlayer(destination);
                sourceEntity.SetInput(CreateTurnInput(135790, CharacterTestRuntimeCommandSources.RuntimeAiPlanner));

                RuntimeFrame frame = source.CurrentFrame;
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(
                    source.CharacterRuntime,
                    source.RuntimeCommands);
                var destinationParticipant = new CharacterTestNetworkSnapshotParticipant(
                    destination.CharacterRuntime,
                    destination.RuntimeCommands);
                RuntimeCustomState snapshot = sourceParticipant.CaptureSnapshot(frame);

                JObject document = JObject.Parse(snapshot.PayloadJson);
                JObject character = FindCharacterDocument(document, sourceEntity.Id.Value);
                Assert.IsNotNull(character);
                Assert.AreEqual(true, (bool)character["inputTurnInPlace"]);
                Assert.AreEqual(135790, (int)character["inputTurnInPlaceYawMilliDegrees"]);
                Assert.AreEqual(
                    CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                    (int)character["inputControlIntentSourceId"]);

                NetworkSnapshotApplyResult apply = destinationParticipant.ApplySnapshot(frame, snapshot);

                Assert.IsTrue(apply.Success, apply.Message);
                Assert.IsTrue(destinationEntity.Input.TurnInPlace);
                Assert.AreEqual(135790, destinationEntity.Input.TurnInPlaceYawMilliDegrees);
                Assert.AreEqual(
                    CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                    destinationEntity.Input.ControlIntentSourceId);
                Assert.AreEqual(0, destinationEntity.Input.MoveXMilli);
                Assert.AreEqual(0, destinationEntity.Input.MoveYMilli);
                Assert.AreEqual(0, destinationEntity.Input.WorldMoveXMilli);
                Assert.AreEqual(0, destinationEntity.Input.WorldMoveYMilli);
                Assert.IsFalse(destinationEntity.Input.HasWorldMove);
                Assert.IsFalse(destinationEntity.Input.AttackPrimaryPressed);
                Assert.IsFalse(destinationEntity.Input.AttackSecondaryPressed);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(frame),
                    destinationParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void StateHash_ChangesWhenTurnInPlaceFieldsChange()
        {
            using (GameSlice slice = CreateSlice())
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity entity = FindPlayer(slice);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                long baseline = participant.ComputeStateHash(frame);

                entity.SetInput(CreateTurnInput(90000, CharacterTestRuntimeCommandSources.RuntimeAiPlanner));

                long turned = participant.ComputeStateHash(frame);
                Assert.AreNotEqual(baseline, turned);
            }
        }

        [Test]
        public void RollbackAndFullSnapshotRestore_PreserveTurnInPlaceFieldsAndHash()
        {
            using (GameSlice slice = CreateSlice())
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity entity = FindPlayer(slice);
                entity.SetInput(CreateTurnInput(270000, CharacterTestRuntimeCommandSources.PlayerInput));

                RuntimeFrame frame = slice.CurrentFrame;
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                RuntimeCustomState fullSnapshot = participant.CaptureSnapshot(frame);
                CharacterTestNetworkSnapshotDocument rollbackDocument =
                    participant.CaptureRollbackDocument(frame);
                long expectedHash = participant.ComputeStateHash(frame);

                entity.SetInput(CharacterTestCharacterInputComponent.None);
                NetworkSnapshotApplyResult rollback = participant.ApplyRollbackDocument(
                    frame,
                    rollbackDocument,
                    preserveLocalPredictedEntity: false);

                Assert.IsTrue(rollback.Success, rollback.Message);
                AssertTurnInput(entity, 270000, CharacterTestRuntimeCommandSources.PlayerInput);
                Assert.AreEqual(expectedHash, participant.ComputeStateHash(frame));

                entity.SetInput(CharacterTestCharacterInputComponent.None);
                NetworkSnapshotApplyResult fullRestore = participant.ApplySnapshot(frame, fullSnapshot);

                Assert.IsTrue(fullRestore.Success, fullRestore.Message);
                AssertTurnInput(entity, 270000, CharacterTestRuntimeCommandSources.PlayerInput);
                Assert.AreEqual(expectedHash, participant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void Replay_ContainsTurnInPlaceCommandAndReplaysWithMatchingHash()
        {
            using (GameSlice slice = CreateSlice())
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity entity = FindPlayer(slice);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                var recorder = new CharacterTestReplayRecorder(CreateConfig());
                recorder.Begin(slice.CurrentFrame, participant.CaptureSnapshot(slice.CurrentFrame));

                const int frameCount = 3;
                for (int i = 0; i < frameCount; i++)
                {
                    RuntimeFrame frame = slice.CurrentFrame;
                    CharacterTestControlIntent intent = new CharacterTestControlIntent(
                        entity.Id.Value,
                        Vector2.zero,
                        Vector2.zero,
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        attackPrimaryPressed: false,
                        attackSecondaryPressed: false,
                        debugSecondaryPressed: false,
                        debugPrimaryPressed: false,
                        skillSlotPressedMask: 0,
                        dodgePressed: false,
                        movementSpeedMultiplier: 1f,
                        turnInPlace: true,
                        turnInPlaceYawMilliDegrees: 90000 + (i * 45000));
                    RuntimeCommandValidationResult enqueue = slice.EnqueueRuntimeCommand(
                        CharacterTestRuntimeCommandFactory.SetControlIntent(
                            frame,
                            CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                            intent,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0002, sequence: i)));
                    Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

                    slice.Tick(1d / 60d);
                    Assert.IsTrue(
                        ContainsTurnCommand(slice.RuntimeCommands.LastDrainedCommands),
                        "TurnInPlace command was not present in the drained authoritative frame.");
                    long hash = participant.ComputeStateHash(frame);
                    recorder.RecordAuthoritativeCommands(new NetworkAuthoritativeCommandPacket(
                        "local",
                        frame,
                        frame,
                        slice.RuntimeCommands.LastDrainedCommands));
                    recorder.RecordFrame(
                        frame,
                        slice.RuntimeCommands.LastDrainedCommands,
                        hash,
                        CharacterTestReplayDiagnostics.CreateFrameSummary(
                            frame,
                            slice.RuntimeCommands.LastDrainedCommands.Count,
                            slice.CharacterEntities.Count,
                            hash));
                }

                CharacterTestReplayCapture capture = recorder.CreateCapture();
                Assert.AreEqual(frameCount, capture.FrameCount);
                Assert.IsTrue(ContainsTurnCommand(capture), "Replay capture did not retain TurnInPlace.");

                RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);

                Assert.IsTrue(playback.Success, playback.FailureMessage);
                Assert.AreEqual(capture.FrameCount, playback.FramesPlayed);
                Assert.AreEqual(playback.ExpectedResultHash, playback.ActualResultHash);
            }
        }

        [Test]
        public void JsonCodec_TurnInPlaceRoundTripPreservesEnvelopeAndQuantizedYaw()
        {
            var codec = new JsonNetworkMessageCodec();
            CharacterTestControlIntent intent = new CharacterTestControlIntent(
                targetEntityId: 7,
                move: Vector2.right,
                worldMove: Vector2.up,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: true,
                attackSecondaryPressed: true,
                debugSecondaryPressed: false,
                debugPrimaryPressed: false,
                skillSlotPressedMask: 0,
                dodgePressed: false,
                movementSpeedMultiplier: 1f,
                turnInPlace: true,
                turnInPlaceYawMilliDegrees: 450123);
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                new RuntimeFrame(11),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                intent,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0003));
            var message = new NetworkMessageEnvelope(
                "session",
                "planner",
                "server",
                3,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RuntimeCommands,
                new RuntimeFrame(11),
                new NetworkRuntimeCommandPacket("planner", new RuntimeFrame(11), new[] { command }));

            NetworkCodecEncodeResult encoded = codec.Encode(message);
            Assert.IsTrue(encoded.Success, encoded.Error.ToString());
            NetworkCodecDecodeResult decoded = codec.Decode(encoded.Bytes);

            Assert.IsTrue(decoded.Success, decoded.Error.ToString());
            NetworkRuntimeCommandPacket packet = (NetworkRuntimeCommandPacket)decoded.Message.Payload;
            CharacterTestControlIntent decodedIntent =
                CharacterTestRuntimeCommandFactory.DecodeControlIntent(packet.Commands[0]);
            Assert.IsTrue(decodedIntent.TurnInPlace);
            Assert.AreEqual(
                CharacterTestDeterministicUnits.NormalizeMilliDegrees(450123),
                decodedIntent.TurnInPlaceYawMilliDegrees);
            Assert.IsFalse(decodedIntent.AttackPrimaryPressed);
            Assert.IsFalse(decodedIntent.AttackSecondaryPressed);
            Assert.AreEqual(Vector2.zero, decodedIntent.Move);
            Assert.AreEqual(Vector2.zero, decodedIntent.WorldMove);
            Assert.AreEqual(command.Envelope, packet.Commands[0].Envelope);
        }

        [Test]
        public void JsonCodec_MalformedTurnInPlaceEnvelopeFailsClosed()
        {
            var codec = new JsonNetworkMessageCodec();
            const string malformedJson =
                "{\"schemaVersion\":0,\"sessionId\":\"s\",\"senderPeerId\":\"a\",\"receiverPeerId\":\"b\",\"sequence\":1,\"channel\":0,\"kind\":2,\"frame\":1,\"payload\":{\"inputPeerId\":\"a\",\"clientFrame\":1,\"commands\":[{\"frame\":1,\"sourceId\":8,\"commandId\":100,\"targetId\":7,\"payload0\":0,\"payload1\":0,\"payload2\":335544329,\"traceId\":\"bad\",\"sequence\":1,\"envelope\":{\"typeId\":0,\"schemaVersion\":1,\"provenance\":0,\"data0\":90000}}]}}";

            NetworkCodecDecodeResult decoded = codec.Decode(Encoding.UTF8.GetBytes(malformedJson));

            Assert.IsFalse(decoded.Success);
            Assert.IsNull(decoded.Message);
            Assert.AreEqual(NetworkCodecErrorCode.InvalidPayload, decoded.Error.Code);
        }

        private static GameSlice CreateSlice()
        {
            return new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false);
        }

        private static CharacterTestCharacterInputComponent CreateTurnInput(
            int yawMilliDegrees,
            int controlIntentSourceId)
        {
            return new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackSecondaryPressed: false,
                worldMove: Vector2.zero,
                hasWorldMove: false,
                movementSpeedScaleMilli: CharacterTestControlIntent.DefaultMovementSpeedScaleMilli,
                turnInPlace: true,
                turnInPlaceYawMilliDegrees: yawMilliDegrees,
                controlIntentSourceId: controlIntentSourceId);
        }

        private static void AssertTurnInput(
            CharacterTestCharacterEntity entity,
            int yawMilliDegrees,
            int controlIntentSourceId)
        {
            Assert.IsTrue(entity.Input.TurnInPlace);
            Assert.AreEqual(yawMilliDegrees, entity.Input.TurnInPlaceYawMilliDegrees);
            Assert.AreEqual(controlIntentSourceId, entity.Input.ControlIntentSourceId);
            Assert.AreEqual(0, entity.Input.MoveXMilli);
            Assert.AreEqual(0, entity.Input.MoveYMilli);
            Assert.AreEqual(0, entity.Input.WorldMoveXMilli);
            Assert.AreEqual(0, entity.Input.WorldMoveYMilli);
        }

        private static JObject FindCharacterDocument(JObject document, int entityId)
        {
            JArray characters = (JArray)document["characters"];
            if (characters == null)
                return null;
            for (int i = 0; i < characters.Count; i++)
            {
                JObject character = characters[i] as JObject;
                if (character != null && (int)character["entityId"] == entityId)
                    return character;
            }

            return null;
        }

        private static bool ContainsTurnCommand(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null)
                return false;
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].CommandId != CharacterTestRuntimeCommandIds.SetControlIntent)
                    continue;
                if (CharacterTestRuntimeCommandFactory.DecodeControlIntent(commands[i]).TurnInPlace)
                    return true;
            }

            return false;
        }

        private static bool ContainsTurnCommand(CharacterTestReplayCapture capture)
        {
            for (int i = 0; i < capture.RuntimeReplaySnapshot.Records.Count; i++)
            {
                if (ContainsTurnCommand(capture.RuntimeReplaySnapshot.Records[i].Commands))
                    return true;
            }

            return false;
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / 60d);
        }

        private static CharacterTestCharacterEntity FindPlayer(GameSlice slice)
        {
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            Assert.Fail("Expected CharacterTest local player entity.");
            return null;
        }

        private static NetworkSessionConfig CreateConfig()
        {
            return new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: 60,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 2,
                configHash: "cd-m1-authority-state",
                resourceCatalogHash: "character-test-resources",
                stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly);
        }
    }
}
