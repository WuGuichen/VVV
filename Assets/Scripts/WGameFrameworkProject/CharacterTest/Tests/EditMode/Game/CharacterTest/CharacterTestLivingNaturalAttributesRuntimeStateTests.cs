using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLivingNaturalAttributesRuntimeStateTests
    {
        [Test]
        public void LivingNaturalAttributesRuntimeStateCommand_ValidatesSchemaTargetAndOperation()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeFrame frame = new RuntimeFrame(12);
            RuntimeCommand valid = CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeState(
                frame,
                CharacterTestRuntimeCommandSources.GenesisProbe,
                targetEntityId: 7,
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure,
                magnitude: 40,
                traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidTarget =
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeState(
                    frame,
                    CharacterTestRuntimeCommandSources.GenesisProbe,
                    targetEntityId: 0,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure,
                    magnitude: 40,
                    traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidOperation =
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeState(
                    frame,
                    CharacterTestRuntimeCommandSources.GenesisProbe,
                    targetEntityId: 7,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.None,
                    magnitude: 40,
                    traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidMagnitude =
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeState(
                    frame,
                    CharacterTestRuntimeCommandSources.GenesisProbe,
                    targetEntityId: 7,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure,
                    CharacterTestLivingNaturalAttributesRuntimeState.MaximumCommandMagnitude + 1,
                    traceId: new RuntimeTraceIdentity(997818));
            RuntimeCommand invalidSchema = new RuntimeCommand(
                frame,
                CharacterTestRuntimeCommandSources.GenesisProbe,
                CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState,
                targetId: 7,
                payload0: (int)CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure,
                payload1: 40,
                payload2:
                    CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeStateSchemaVersion + 1,
                traceId: new RuntimeTraceIdentity(997818));

            RuntimeCommandValidationResult accepted = validator.Validate(valid);
            RuntimeCommandValidationResult rejectedTarget = validator.Validate(invalidTarget);
            RuntimeCommandValidationResult rejectedOperation = validator.Validate(invalidOperation);
            RuntimeCommandValidationResult rejectedMagnitude = validator.Validate(invalidMagnitude);
            RuntimeCommandValidationResult rejectedSchema = validator.Validate(invalidSchema);
            CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisLivingNaturalAttributesRuntimeState(valid);

            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            Assert.IsFalse(rejectedTarget.Success);
            Assert.IsFalse(rejectedOperation.Success);
            Assert.IsFalse(rejectedMagnitude.Success);
            Assert.IsFalse(rejectedSchema.Success);
            Assert.AreEqual(7, request.TargetEntityId);
            Assert.AreEqual(
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure,
                request.Operation);
            Assert.AreEqual(40, request.Magnitude);
            Assert.AreEqual(
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeStateSchemaVersion,
                request.SchemaVersion);
        }

        [Test]
        public void LivingNaturalAttributesRuntimeStateCommand_CoversEmbodySelfMaintainRecoverAndMaterialReturn()
        {
            using (var harness = CreateHarness())
            {
                CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();

                EnqueueAndTick(
                    harness,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Embody);
                CharacterTestLivingNaturalAttributesRuntimeState embody = entity.LivingNaturalAttributes;
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                    embody.LifecycleState);
                Assert.IsTrue(embody.CanEmbody);
                Assert.IsTrue(embody.BodyBoundaryPresent);
                Assert.AreEqual(
                    CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody,
                    embody.MaterialReading);

                EnqueueAndTick(
                    harness,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure);
                CharacterTestLivingNaturalAttributesRuntimeState selfMaintain = entity.LivingNaturalAttributes;
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.SelfMaintaining,
                    selfMaintain.LifecycleState);
                Assert.IsTrue(selfMaintain.CanSelfMaintain);
                Assert.Greater(selfMaintain.ExternalPressure, 0);
                Assert.Greater(selfMaintain.Scatter, 0);
                Assert.GreaterOrEqual(selfMaintain.SelfCohesion, selfMaintain.Scatter);
                Assert.IsTrue(selfMaintain.BodyBoundaryPresent);

                EnqueueAndTick(
                    harness,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ReleaseAndRecover);
                CharacterTestLivingNaturalAttributesRuntimeState recover = entity.LivingNaturalAttributes;
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                    recover.LifecycleState);
                Assert.IsTrue(recover.CanRecover);
                Assert.IsTrue(recover.RecoveredToStable);
                Assert.Greater(recover.RecoveryTicks, 0);
                Assert.Greater(recover.RecoveryAmount, 0);
                Assert.AreEqual(0, recover.Scatter);

                EnqueueAndTick(
                    harness,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate);
                CharacterTestLivingNaturalAttributesRuntimeState inactiveMaterial = entity.LivingNaturalAttributes;
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.InactiveMaterial,
                    inactiveMaterial.LifecycleState);
                Assert.IsTrue(inactiveMaterial.ReturnedToMaterial);
                Assert.IsFalse(inactiveMaterial.CanAct);
                Assert.IsFalse(inactiveMaterial.BodyBoundaryPresent);
                Assert.Greater(inactiveMaterial.Scatter, inactiveMaterial.CohesionLimit);
                Assert.AreEqual(
                    CharacterTestGenesisLivingNaturalAttributeMaterialReading.MaterialSystem,
                    inactiveMaterial.MaterialReading);
                StringAssert.Contains(
                    "stateModel=character-test-runtime-authority",
                    harness.Slice.LastGenesisLivingNaturalAttributesRuntimeStateSummary);
            }
        }

        [Test]
        public void LivingNaturalAttributesRuntimeStateCommand_ChangesHashAndRestoresThroughSnapshot()
        {
            using (var source = CreateHarness())
            using (var target = CreateHarness())
            {
                CharacterTestCharacterEntity sourceEntity = source.SpawnLocalPlayer();
                target.SpawnLocalPlayer();
                CharacterTestNetworkSnapshotParticipant sourceParticipant = source.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant targetParticipant = target.CreateSnapshotParticipant();
                RuntimeFrame hashFrame = new RuntimeFrame(901);
                long hashBefore = sourceParticipant.ComputeStateHash(hashFrame);

                EnqueueAndTick(
                    source,
                    sourceEntity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure);
                long hashAfter = sourceParticipant.ComputeStateHash(hashFrame);
                RuntimeCustomState snapshot = sourceParticipant.CaptureSnapshot(hashFrame);
                NetworkSnapshotApplyResult apply = targetParticipant.ApplySnapshot(hashFrame, snapshot);

                Assert.AreNotEqual(hashBefore, hashAfter);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(hashAfter, targetParticipant.ComputeStateHash(hashFrame));
                Assert.AreEqual(
                    sourceEntity.LivingNaturalAttributes.Scatter,
                    target.FindEntityById(sourceEntity.Id.Value).LivingNaturalAttributes.Scatter);
            }
        }

        [Test]
        public void LivingNaturalAttributesRuntimeStateCommand_ResetReturnsDefaultStableState()
        {
            using (var harness = CreateHarness())
            {
                CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
                RuntimeFrame hashFrame = new RuntimeFrame(902);
                CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
                long hashBefore = participant.ComputeStateHash(hashFrame);

                EnqueueAndTick(
                    harness,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate);
                long inactiveHash = participant.ComputeStateHash(hashFrame);
                Assert.AreNotEqual(hashBefore, inactiveHash);

                EnqueueAndTick(
                    harness,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Reset);
                CharacterTestLivingNaturalAttributesRuntimeState reset = entity.LivingNaturalAttributes;
                long resetHash = participant.ComputeStateHash(hashFrame);

                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                    reset.LifecycleState);
                Assert.AreEqual(CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.None, reset.LastOperation);
                Assert.IsTrue(reset.CanEmbody);
                Assert.IsTrue(reset.CanSelfMaintain);
                Assert.IsTrue(reset.CanRecover);
                Assert.IsFalse(reset.ReturnedToMaterial);
                Assert.AreEqual(0, reset.Scatter);
                Assert.AreNotEqual(inactiveHash, resetHash);
            }
        }

        private static CharacterTestSliceTestHarness CreateHarness()
        {
            return new CharacterTestSliceTestHarness(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false
            });
        }

        private static void EnqueueAndTick(
            CharacterTestSliceTestHarness harness,
            int targetEntityId,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation,
            int magnitude = 0)
        {
            RuntimeCommandValidationResult result = harness.Enqueue(
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeState(
                    harness.CurrentFrame,
                    CharacterTestRuntimeCommandSources.GenesisProbe,
                    targetEntityId,
                    operation,
                    magnitude,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0027)));
            Assert.IsTrue(result.Success, result.Error.Message);
            harness.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.IsEmpty(harness.Modules.RuntimeCommands.LastCommandErrors);
        }
    }
}
