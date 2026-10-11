using MxFramework.Combat.Physics;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGenesisForceOriginTests
    {
        [Test]
        public void GenesisForceOriginSamples_CoverFourCanonicalSources()
        {
            CharacterTestGenesisForceOriginSample selfWeight =
                CharacterTestGenesisSurfaceMaterialHarness.RunSelfWeightForceOriginSample(
                    CombatSurfaceMaterial.Rough);
            CharacterTestGenesisForceOriginSample activeForce =
                CharacterTestGenesisSurfaceMaterialHarness.RunActiveForceOriginSample(
                    CombatSurfaceMaterial.Rough);
            CharacterTestGenesisForceOriginSample continuousExternal =
                CharacterTestGenesisSurfaceMaterialHarness.RunContinuousExternalForceOriginSample(
                    CombatSurfaceMaterial.HardSlick,
                    forceTicks: 12,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    accelerationX: 30f);
            CharacterTestGenesisForceOriginSample instantImpulse =
                CharacterTestGenesisSurfaceMaterialHarness.RunInstantImpulseForceOriginSample(
                    CombatSurfaceMaterial.HardSlick,
                    followTicks: 12,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    impulseSpeedX: 6f);

            TestContext.WriteLine(selfWeight.ToSummaryLine());
            TestContext.WriteLine(activeForce.ToSummaryLine());
            TestContext.WriteLine(continuousExternal.ToSummaryLine());
            TestContext.WriteLine(instantImpulse.ToSummaryLine());

            Assert.AreEqual(CharacterTestGenesisForceOriginKind.SelfWeight, selfWeight.OriginKind);
            Assert.AreEqual(CharacterTestGenesisForceOriginKind.ActiveForce, activeForce.OriginKind);
            Assert.AreEqual(
                CharacterTestGenesisForceOriginKind.ContinuousExternalForce,
                continuousExternal.OriginKind);
            Assert.AreEqual(CharacterTestGenesisForceOriginKind.InstantImpulse, instantImpulse.OriginKind);

            Assert.IsTrue(selfWeight.Landed);
            Assert.IsTrue(selfWeight.FinalGrounded);
            Assert.Less(selfWeight.MinVerticalVelocity, -1f);
            Assert.Less(selfWeight.FinalY, selfWeight.InitialY);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, selfWeight.ResolvedSurfaceKind);
            Assert.AreEqual(
                CharacterTestGenesisSurfaceMaterialHarness.DefaultMassKg,
                selfWeight.MassKg,
                0.0001f);

            Assert.IsTrue(activeForce.ControlInputApplied);
            Assert.Greater(activeForce.FinalHorizontalSpeed, 3.5f);
            Assert.Greater(activeForce.DisplacementZ, 0.3f);
            Assert.AreEqual(activeForce.TickCount, activeForce.SourceTickCount);

            Assert.AreEqual(12, continuousExternal.ExternalForceTicks);
            Assert.AreEqual(12, continuousExternal.SourceTickCount);
            Assert.Greater(
                continuousExternal.FinalHorizontalSpeed,
                continuousExternal.FirstTickHorizontalSpeed + 1f);
            Assert.Greater(continuousExternal.DisplacementX, 0.4f);

            Assert.IsTrue(instantImpulse.ImpulseApplied);
            Assert.AreEqual(1, instantImpulse.SourceTickCount);
            Assert.AreEqual(0, instantImpulse.ExternalForceTicks);
            Assert.Greater(instantImpulse.FirstTickHorizontalSpeed, 5f);
            Assert.Greater(
                instantImpulse.FirstTickHorizontalSpeed,
                continuousExternal.FirstTickHorizontalSpeed + 4f);

            StringAssert.Contains("origin=SelfWeight", selfWeight.ToSummaryLine());
            StringAssert.Contains("sourceTicks=12", continuousExternal.ToSummaryLine());
            StringAssert.Contains("impulse=True", instantImpulse.ToSummaryLine());
        }

        [Test]
        public void GenesisForceOriginSamples_ContinuousForceDoesNotCollapseIntoImpulse()
        {
            CharacterTestGenesisForceOriginSample continuousExternal =
                CharacterTestGenesisSurfaceMaterialHarness.RunContinuousExternalForceOriginSample(
                    CombatSurfaceMaterial.HardSlick,
                    forceTicks: 12,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    accelerationX: 30f);
            CharacterTestGenesisForceOriginSample instantImpulse =
                CharacterTestGenesisSurfaceMaterialHarness.RunInstantImpulseForceOriginSample(
                    CombatSurfaceMaterial.HardSlick,
                    followTicks: 12,
                    tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                    impulseSpeedX: 6f);

            Assert.IsFalse(continuousExternal.ImpulseApplied);
            Assert.IsFalse(instantImpulse.ControlInputApplied);
            Assert.AreEqual(12, continuousExternal.ExternalForceTicks);
            Assert.AreEqual(1, instantImpulse.SourceTickCount);
            Assert.Less(continuousExternal.FirstTickHorizontalSpeed, 1f);
            Assert.Greater(continuousExternal.FinalHorizontalSpeed, 4f);
            Assert.Greater(instantImpulse.FirstTickHorizontalSpeed, 5f);
            Assert.Greater(
                instantImpulse.FirstTickHorizontalSpeed,
                continuousExternal.FirstTickHorizontalSpeed * 8f);
        }

        [Test]
        public void GenesisForceOrigin_CommandBoundaryRecordsStateHashAndReplay()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false))
            {
                CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                var participant = new CharacterTestNetworkSnapshotParticipant(
                    slice.CharacterRuntime,
                    slice.RuntimeCommands);
                var recorder = new CharacterTestReplayRecorder(CreateReplayConfig());
                recorder.Begin(slice.CurrentFrame, participant.CaptureSnapshot(slice.CurrentFrame));

                RuntimeFrame frame = slice.CurrentFrame;
                RuntimeCommandValidationResult commandResult = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.SetControlIntent(
                        frame,
                        CharacterTestRuntimeCommandSources.PlayerInput,
                        entity.Id.Value,
                        Vector2.up,
                        Vector2.up,
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        attackPrimaryPressed: false,
                        traceId: new RuntimeTraceIdentity(949857)));
                Assert.IsTrue(commandResult.Success, commandResult.Error.Message);

                slice.Tick(1d / CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate);
                long fullHash = participant.ComputeStateHash(frame);
                long ownedHash = participant.ComputeOwnedStateHash(frame, entity.Id.Value);
                RecordReplayFrame(slice, recorder, frame, fullHash);
                CharacterTestReplayCapture capture = recorder.CreateCapture(
                    () => new GameSlice(
                        NullRuntimeLogger.Instance,
                        createCharacterViews: false,
                        enableLocalInputCommands: false));
                RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);

                Assert.That(slice.RuntimeCommands.LastDrainedCommands, Has.Some.Matches<RuntimeCommand>(
                    command => command.CommandId == CharacterTestRuntimeCommandIds.SetControlIntent
                        && command.SourceId == CharacterTestRuntimeCommandSources.PlayerInput
                        && command.TargetId == entity.Id.Value));
                Assert.IsEmpty(slice.RuntimeCommands.LastCommandErrors);
                Assert.That(slice.RecentRuntimeEvents, Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.ControlIntentApplied
                        && evt.EntityId == entity.Id.Value));
                Assert.Greater(entity.HorizontalVelocity.magnitude, 0.1f);
                Assert.AreNotEqual(0L, fullHash);
                Assert.AreNotEqual(0L, ownedHash);
                Assert.AreEqual(1, capture.FrameCount);
                Assert.AreEqual(1, capture.AuthoritativeCommandCount);
                Assert.IsTrue(playback.Success, playback.FailureMessage);
            }
        }

        private static CharacterTestCharacterEntity SpawnLocalPlayer(GameSlice slice)
        {
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult spawn = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    frame,
                    CharacterTestRuntimeCommandSources.System,
                    CharacterTestCharacterSpawnSpec.TemporaryPlayer,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0013)));
            Assert.IsTrue(spawn.Success, spawn.Error.Message);
            slice.Tick(0d);

            foreach (CharacterTestCharacterEntity entity in slice.CharacterEntities)
            {
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            Assert.Fail("Expected CharacterTest local player entity.");
            return null;
        }

        private static void RecordReplayFrame(
            GameSlice slice,
            CharacterTestReplayRecorder recorder,
            RuntimeFrame frame,
            long hash)
        {
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

        private static NetworkSessionConfig CreateReplayConfig()
        {
            return new NetworkSessionConfig(
                protocolVersion: 1,
                tickRate: CharacterTestGenesisSurfaceMaterialHarness.DefaultTickRate,
                inputDelayFrames: 2,
                snapshotIntervalFrames: 2,
                configHash: "character-test-genesis-force-origin",
                resourceCatalogHash: "character-test-resources",
                stateSyncPolicy: NetworkStateSyncPolicy.BootstrapOnly);
        }
    }
}
