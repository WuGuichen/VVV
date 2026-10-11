using System;
using System.IO;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGenesisConfigPipelineTests
    {
        [Test]
        public void GenesisConfigJson_RoundTripsDefaultDocumentAndLoadsAuthoringSample()
        {
            CharacterTestGenesisConfigDocument document =
                CharacterTestGenesisConfigDefaults.CreateDefaultDocument();
            CharacterTestGenesisConfigValidationReport validation =
                CharacterTestGenesisConfigValidator.Validate(document);
            string json = CharacterTestGenesisConfigJson.ToJson(document);

            bool loaded = CharacterTestGenesisConfigJson.TryLoad(
                json,
                out CharacterTestGenesisConfigDocument roundTrip,
                out CharacterTestGenesisConfigValidationReport roundTripReport);
            bool sampleLoaded = CharacterTestGenesisConfigJson.TryLoad(
                ReadAuthoringSample(),
                out CharacterTestGenesisConfigDocument sample,
                out CharacterTestGenesisConfigValidationReport sampleReport);

            Assert.IsTrue(validation.Success, validation.ToSummaryLine());
            Assert.IsTrue(loaded, roundTripReport.ToSummaryLine());
            Assert.IsTrue(sampleLoaded, sampleReport.ToSummaryLine());
            Assert.AreEqual(CharacterTestGenesisConfigDefaults.Schema, roundTrip.Schema);
            Assert.AreEqual(4, roundTrip.PotencyProfiles.Length);
            Assert.AreEqual(1, roundTrip.LivingNaturalAttributesProfiles.Length);
            Assert.AreEqual(3, sample.FeedbackMappings.Length);
            StringAssert.Contains("\"schema\"", json);
            StringAssert.Contains("\"resultKind\":\"Burning\"", json);
        }

        [Test]
        public void GenesisConfigValidator_ReportsStructuredInvalidConfigIssues()
        {
            CharacterTestGenesisConfigDocument document =
                CharacterTestGenesisConfigDefaults.CreateDefaultDocument();
            document.SchemaVersion = CharacterTestGenesisConfigDefaults.SchemaVersion + 1;
            document.PotencyProfiles[1].StableId = document.PotencyProfiles[0].StableId;
            document.PotencyProfiles[0].BasePotency.D = -1;
            document.LivingNaturalAttributesProfiles[0].CohesionLimit = 0;
            document.FeedbackMappings[0].DebugOnly = false;

            CharacterTestGenesisConfigValidationReport report =
                CharacterTestGenesisConfigValidator.Validate(document);

            Assert.IsFalse(report.Success);
            Assert.IsTrue(report.ContainsCode("schema-version-mismatch"));
            Assert.IsTrue(report.ContainsCode("potency-profile-duplicate"));
            Assert.IsTrue(report.ContainsCode("potency-state-out-of-range"));
            Assert.IsTrue(report.ContainsCode("value-not-positive"));
            Assert.IsTrue(report.ContainsCode("feedback-not-debug-only"));
        }

        [Test]
        public void GenesisConfigRuntimeAdapter_DefaultProfilesDrivePotencySamplesAndSceneCatalog()
        {
            CharacterTestGenesisConfigDocument document =
                CharacterTestGenesisConfigDefaults.CreateDefaultDocument();
            bool adapterCreated = CharacterTestGenesisConfigRuntimeAdapter.TryCreate(
                document,
                out CharacterTestGenesisConfigRuntimeAdapter adapter,
                out CharacterTestGenesisConfigValidationReport adapterReport);
            bool catalogCreated = CharacterTestWorldObjectDefinitionCatalog.TryCreateCharacterTestSceneCatalog(
                document,
                out CharacterTestWorldObjectDefinitionCatalog catalog,
                out CharacterTestGenesisConfigValidationReport catalogReport);

            Assert.IsTrue(adapterCreated, adapterReport.ToSummaryLine());
            Assert.IsTrue(catalogCreated, catalogReport.ToSummaryLine());
            Assert.IsTrue(adapter.TryCreatePotencyFacet(
                CharacterTestGenesisConfigDefaults.ClampBurningCrushProfileId,
                out CharacterTestWorldObjectPotencyFacet clampFacet));
            Assert.AreEqual(new CharacterTestGenesisPotencyTriplet(995, 1, 995), clampFacet.BasePotency);
            Assert.IsTrue(adapter.TryGetFeedbackMapping(
                CharacterTestGenesisPotencyResultKind.Burning,
                out CharacterTestGenesisMaterialFeedbackMappingConfig burningFeedback));
            Assert.IsTrue(burningFeedback.DebugOnly);
            Assert.AreEqual("support-and-structure-weakened", burningFeedback.NextContactModifier);

            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                SpawnSpec(catalog, CharacterTestWorldObjectDefinitionIds.MovableSlickLight, 10),
                SpawnSpec(catalog, CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy, 20),
                SpawnSpec(catalog, CharacterTestWorldObjectDefinitionIds.MovableSphere, 30)
            });

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                10,
                CharacterTestGenesisPotencyInfluenceKind.Fire,
                new RuntimeFrame(12),
                out _,
                out string message), message);
            Assert.AreEqual(
                CharacterTestGenesisPotencyResultKind.Burning,
                world.MovableNaturalObjects[0].LatestPotencyApplication.Result);

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                20,
                CharacterTestGenesisPotencyInfluenceKind.Hammer,
                new RuntimeFrame(13),
                out _,
                out message), message);
            Assert.AreEqual(
                CharacterTestGenesisPotencyResultKind.Cracked,
                world.MovableNaturalObjects[1].LatestPotencyApplication.Result);

            Assert.IsTrue(world.ApplyMovableNaturalObjectPotencyInfluence(
                30,
                CharacterTestGenesisPotencyInfluenceKind.Condense,
                new RuntimeFrame(14),
                out _,
                out message), message);
            Assert.AreEqual(
                CharacterTestGenesisPotencyResultKind.CondensedLayer,
                world.MovableNaturalObjects[2].LatestPotencyApplication.Result);
        }

        [Test]
        public void GenesisConfigRuntimeAdapter_LivingProfileDrivesRuntimeStateAndCommandMagnitudes()
        {
            CharacterTestGenesisConfigDocument document =
                CharacterTestGenesisConfigDefaults.CreateDefaultDocument();
            CharacterTestGenesisLivingNaturalAttributesProfileConfig profile =
                document.LivingNaturalAttributesProfiles[0];
            profile.BodyScaleMillimeters = 1600;
            profile.MassGrams = 52000;
            profile.SelfCohesion = 80;
            profile.CohesionLimit = 90;
            profile.DefaultExternalPressure = 30;
            profile.DefaultRecoveryAmount = 30;
            profile.DefaultOverlimitPressure = 95;

            bool adapterCreated = CharacterTestGenesisConfigRuntimeAdapter.TryCreate(
                document,
                out CharacterTestGenesisConfigRuntimeAdapter adapter,
                out CharacterTestGenesisConfigValidationReport report);
            Assert.IsTrue(adapterCreated, report.ToSummaryLine());
            Assert.IsTrue(adapter.TryCreateLivingNaturalAttributesState(
                CharacterTestGenesisConfigDefaults.LivingDefaultProfileId,
                new RuntimeFrame(20),
                out CharacterTestLivingNaturalAttributesRuntimeState state));
            Assert.AreEqual(1600, state.BodyScaleMillimeters);
            Assert.AreEqual(52000, state.MassGrams);
            Assert.AreEqual(80, state.SelfCohesion);
            Assert.AreEqual(90, state.CohesionLimit);

            using (var harness = CreateHarness())
            {
                CharacterTestCharacterEntity entity = harness.SpawnLocalPlayer();
                entity.SetLivingNaturalAttributes(state);
                CharacterTestNetworkSnapshotParticipant participant = harness.CreateSnapshotParticipant();
                long hashBefore = participant.ComputeStateHash(new RuntimeFrame(900));

                EnqueueConfiguredOperation(
                    harness,
                    adapter,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure);
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.SelfMaintaining,
                    entity.LivingNaturalAttributes.LifecycleState);
                Assert.AreEqual(30, entity.LivingNaturalAttributes.ExternalPressure);
                Assert.AreEqual(30, entity.LivingNaturalAttributes.Scatter);

                EnqueueConfiguredOperation(
                    harness,
                    adapter,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ReleaseAndRecover);
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                    entity.LivingNaturalAttributes.LifecycleState);
                Assert.AreEqual(0, entity.LivingNaturalAttributes.Scatter);
                Assert.AreEqual(30, entity.LivingNaturalAttributes.RecoveryAmount);

                EnqueueConfiguredOperation(
                    harness,
                    adapter,
                    entity.Id.Value,
                    CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate);
                Assert.AreEqual(
                    CharacterTestLivingNaturalAttributesLifecycleState.InactiveMaterial,
                    entity.LivingNaturalAttributes.LifecycleState);
                Assert.AreEqual(95, entity.LivingNaturalAttributes.ExternalPressure);
                Assert.AreEqual(185, entity.LivingNaturalAttributes.Scatter);
                Assert.AreNotEqual(hashBefore, participant.ComputeStateHash(new RuntimeFrame(900)));
            }
        }

        private static string ReadAuthoringSample()
        {
            string path = Path.Combine(
                Environment.CurrentDirectory,
                "Tools",
                "MxFramework.Authoring",
                "samples",
                "genesis",
                "character_test_genesis_profiles.json");
            return File.ReadAllText(path);
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

        private static void EnqueueConfiguredOperation(
            CharacterTestSliceTestHarness harness,
            CharacterTestGenesisConfigRuntimeAdapter adapter,
            int targetEntityId,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation)
        {
            Assert.IsTrue(adapter.TryResolveLivingOperationMagnitude(
                CharacterTestGenesisConfigDefaults.LivingDefaultProfileId,
                operation,
                out int magnitude));
            RuntimeCommandValidationResult result = harness.Enqueue(
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeState(
                    harness.CurrentFrame,
                    CharacterTestRuntimeCommandSources.GenesisProbe,
                    targetEntityId,
                    operation,
                    magnitude,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0012)));
            Assert.IsTrue(result.Success, result.Error.Message);
            harness.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.IsEmpty(harness.Modules.RuntimeCommands.LastCommandErrors);
        }

        private static CharacterTestMovableNaturalObjectSpawnSpec SpawnSpec(
            CharacterTestWorldObjectDefinitionCatalog catalog,
            int definitionId,
            int numericId)
        {
            Assert.IsTrue(catalog.TryGetDefinition(definitionId, out CharacterTestWorldObjectDefinition definition));
            Assert.IsTrue(definition.TryCreateMovableNaturalObjectSpawnSpec(
                numericId,
                instanceStableId: null,
                halfExtentXMillimeters: 0,
                halfExtentYMillimeters: 0,
                halfExtentZMillimeters: 0,
                xMillimeters: numericId * 100,
                yMillimeters: CharacterTestMovableNaturalObjectSpawnSpec.DefaultHalfExtentMillimeters,
                zMillimeters: 0,
                out CharacterTestMovableNaturalObjectSpawnSpec spec));
            return spec;
        }
    }
}
