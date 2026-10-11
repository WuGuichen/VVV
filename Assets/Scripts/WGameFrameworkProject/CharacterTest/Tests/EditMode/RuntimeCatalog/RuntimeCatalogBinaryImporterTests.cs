using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using NUnit.Framework;

namespace MxFramework.Tests.RuntimeCatalog
{
    public sealed class RuntimeCatalogBinaryImporterTests
    {
        [Test]
        public void AbilityDefinitions_BinaryMatchesJson()
        {
            AssertSameAbilityDefinitions();
        }

        [Test]
        public void ProjectileDefinitions_BinaryMatchesJson()
        {
            AssertSameProjectileDefinitions();
        }

        [Test]
        public void GeneratedCatalogs_BinaryImportMatchesJsonStableShape()
        {
            AssertSameAbilityDefinitions();
            AssertSameAbilityEffectTimelines();
            AssertSameBuffDefinitions();
            AssertSameBuffPresentations();
            AssertSameAuraDefinitions();
            AssertSameProjectileDefinitions();
            AssertSameProjectileSpawnProfiles();
            AssertSameProjectilePresentationBindings();
            AssertSameVfxDefinitions();
            AssertSameVfxTriggerMaps();
        }

        [Test]
        public void ImportBytes_InvalidHeaderAndPayload_ReturnStructuredDiagnostics()
        {
            byte[] abilityBytes = ReadBytes("Assets/MxFrameworkGenerated/Abilities/ability_catalog.bytes");

            byte[] badMagic = Clone(abilityBytes);
            badMagic[0] ^= 0x7f;
            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> badMagicResult =
                AbilityDefinitionBinaryImporter.ImportBytes(badMagic, "memory://bad-magic.bytes");
            AssertLoadFailed(badMagicResult, "InvalidHeader");

            byte[] badFormat = Clone(abilityBytes);
            badFormat[4] = 127;
            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> badFormatResult =
                AbilityDefinitionBinaryImporter.ImportBytes(badFormat, "memory://bad-format.bytes");
            AssertLoadFailed(badFormatResult, "InvalidHeader");

            byte[] badHash = Clone(abilityBytes);
            badHash[badHash.Length - 1] ^= 0x01;
            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> badHashResult =
                AbilityDefinitionBinaryImporter.ImportBytes(badHash, "memory://bad-hash.bytes");
            AssertLoadFailed(badHashResult, "StableHashMismatch");

            byte[] truncated = new byte[6];
            Array.Copy(abilityBytes, truncated, truncated.Length);
            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> truncatedResult =
                AbilityDefinitionBinaryImporter.ImportBytes(truncated, "memory://truncated.bytes");
            AssertLoadFailed(truncatedResult, "TruncatedFile");

            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> schemaResult =
                RuntimeCatalogBinary.Import(
                    abilityBytes,
                    AbilityDefinitionBinaryImporter.CatalogKind,
                    "999",
                    ReadEmptyAbilityCatalog,
                    "memory://wrong-schema.bytes");
            AssertLoadFailed(schemaResult, "InvalidHeader");

            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> negativeEntryCount =
                RuntimeCatalogBinary.Import(
                    BuildCatalog(
                        AbilityDefinitionBinaryImporter.CatalogKind,
                        AbilityDefinitionBinaryImporter.CurrentSchemaVersion,
                        -1,
                        writer => { }),
                    AbilityDefinitionBinaryImporter.CatalogKind,
                    AbilityDefinitionBinaryImporter.CurrentSchemaVersion,
                    ReadEmptyAbilityCatalog,
                    "memory://negative-entry-count.bytes");
            AssertLoadFailed(negativeEntryCount, "InvalidHeader");
        }

        [Test]
        public void ImportBytes_InvalidEntries_ReturnStructuredDiagnostics()
        {
            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> duplicateAbility =
                AbilityDefinitionBinaryImporter.ImportBytes(
                    BuildDuplicateAbilityCatalogBytes(),
                    "memory://duplicate-ability.bytes");
            AssertLoadFailed(duplicateAbility, "InvalidPayload");
            StringAssert.Contains("Duplicate ability id", duplicateAbility.Diagnostics[0]);

            RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry> unknownEnum =
                AbilityEffectTimelineBinaryImporter.ImportBytes(
                    BuildTimelineWithUnknownEnumBytes(),
                    "memory://unknown-enum.bytes");
            AssertLoadFailed(unknownEnum, "InvalidPayload");
            StringAssert.Contains("trackKind", unknownEnum.Diagnostics[0]);
        }

        private static void AssertSameAbilityDefinitions()
        {
            AbilityDefinitionRegistry json = AbilityDefinitionJsonImporter.ImportFile(
                PathFor("Assets/MxFrameworkGenerated/Abilities/ability_catalog.json"));
            RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> binary =
                AbilityDefinitionBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Abilities/ability_catalog.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameAbilityEffectTimelines()
        {
            AbilityEffectTimelineRegistry json = AbilityEffectTimelineJsonImporter.ImportFile(
                PathFor("Assets/MxFrameworkGenerated/Abilities/ability_effect_timelines.json"));
            RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry> binary =
                AbilityEffectTimelineBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Abilities/ability_effect_timelines.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameBuffDefinitions()
        {
            GameplayBuffDefinitionRegistry json = GameplayBuffDefinitionJsonImporter.ImportFile(
                PathFor("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json"));
            RuntimeCatalogBinaryLoadResult<GameplayBuffDefinitionRegistry> binary =
                GameplayBuffDefinitionBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameBuffPresentations()
        {
            GameplayBuffPresentationDefinitionRegistry json =
                GameplayBuffPresentationDefinitionJsonImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.json"));
            RuntimeCatalogBinaryLoadResult<GameplayBuffPresentationDefinitionRegistry> binary =
                GameplayBuffPresentationDefinitionBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            CollectionAssert.AreEqual(json.ToArray(), binary.Catalog.ToArray());
        }

        private static void AssertSameAuraDefinitions()
        {
            CombatGameplayAuraBuffDefinitionRegistry json =
                CombatGameplayAuraBuffDefinitionJsonImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.json"));
            RuntimeCatalogBinaryLoadResult<CombatGameplayAuraBuffDefinitionRegistry> binary =
                CombatGameplayAuraBuffDefinitionBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameProjectileDefinitions()
        {
            CombatProjectileDefinitionRegistry json = CombatProjectileConfigJsonImporter.ImportFile(
                PathFor("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json"));
            RuntimeCatalogBinaryLoadResult<CombatProjectileDefinitionRegistry> binary =
                CombatProjectileConfigBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameProjectileSpawnProfiles()
        {
            CombatProjectileSpawnProfileRegistry json =
                CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json"));
            RuntimeCatalogBinaryLoadResult<CombatProjectileSpawnProfileRegistry> binary =
                CombatProjectileSpawnProfileConfigBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameProjectilePresentationBindings()
        {
            CombatProjectileUnityPresentationRegistry json =
                CombatProjectileUnityPresentationConfigJsonImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json"));
            RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> binary =
                CombatProjectileUnityPresentationConfigBinaryImporter.ImportFile(
                    PathFor("Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            List<CombatProjectileUnityPresentationBindingV2> jsonBindings = CopyBindings(json);
            List<CombatProjectileUnityPresentationBindingV2> binaryBindings = CopyBindings(binary.Catalog);
            Assert.AreEqual(jsonBindings.Count, binaryBindings.Count);
            jsonBindings.Sort(CompareProjectileBindings);
            binaryBindings.Sort(CompareProjectileBindings);
            for (int i = 0; i < jsonBindings.Count; i++)
            {
                Assert.AreEqual(jsonBindings[i].ProjectileDefinitionId, binaryBindings[i].ProjectileDefinitionId);
                Assert.AreEqual(jsonBindings[i].FlightBodyResource, binaryBindings[i].FlightBodyResource);
                Assert.AreEqual(jsonBindings[i].ViewNamePrefix, binaryBindings[i].ViewNamePrefix);
            }
        }

        private static void AssertSameVfxDefinitions()
        {
            VfxDefinitionRegistry json = VfxConfigJsonImporter.ImportDefinitionFile(
                PathFor("Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json"));
            RuntimeCatalogBinaryLoadResult<VfxDefinitionRegistry> binary =
                VfxConfigBinaryImporter.ImportDefinitionFile(
                    PathFor("Assets/MxFrameworkGenerated/Vfx/vfx_definitions.bytes"));

            AssertLoaded(binary);
            Assert.AreEqual(json.Count, binary.Catalog.Count);
            Assert.AreEqual(json.ComputeStableHash(), binary.Catalog.ComputeStableHash());
        }

        private static void AssertSameVfxTriggerMaps()
        {
            VfxTriggerMapResolver json = VfxConfigJsonImporter.ImportTriggerMapFile(
                PathFor("Assets/MxFrameworkGenerated/Vfx/vfx_trigger_maps.json"));
            RuntimeCatalogBinaryLoadResult<VfxTriggerMapResolver> binary =
                VfxConfigBinaryImporter.ImportTriggerMapFile(
                    PathFor("Assets/MxFrameworkGenerated/Vfx/vfx_trigger_maps.bytes"));

            AssertLoaded(binary);
            List<VfxSpawnRequest> jsonOutput = ResolveKnownVfxMapping(json);
            List<VfxSpawnRequest> binaryOutput = ResolveKnownVfxMapping(binary.Catalog);
            Assert.AreEqual(jsonOutput.Count, binaryOutput.Count);
            Assert.AreEqual(jsonOutput[0].DefinitionId, binaryOutput[0].DefinitionId);
            Assert.AreEqual(jsonOutput[0].LifetimeFramesOverride, binaryOutput[0].LifetimeFramesOverride);
        }

        private static AbilityDefinitionRegistry ReadEmptyAbilityCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            loadedEntryCount = header.EntryCount;
            return new AbilityDefinitionRegistry();
        }

        private static byte[] BuildDuplicateAbilityCatalogBytes()
        {
            return BuildCatalog(
                AbilityDefinitionBinaryImporter.CatalogKind,
                AbilityDefinitionBinaryImporter.CurrentSchemaVersion,
                2,
                writer =>
                {
                    WriteAbilityDefinition(writer, 900001, "ability.test.duplicate");
                    WriteAbilityDefinition(writer, 900001, "ability.test.duplicate");
                });
        }

        private static byte[] BuildTimelineWithUnknownEnumBytes()
        {
            return BuildCatalog(
                AbilityEffectTimelineBinaryImporter.CatalogKind,
                AbilityEffectTimelineBinaryImporter.CurrentSchemaVersion,
                1,
                writer =>
                {
                    writer.Write(930010); // timeline identity
                    writer.Write("timeline.test.unknown_enum");
                    writer.Write(1);
                    writer.Write(1);
                    writer.Write(930011); // event identity
                    writer.Write("event.test.unknown_enum");
                    writer.Write(999); // the only intentionally invalid field: trackKind
                    writer.Write((int)AbilityEffectTimelineEventKind.EmitGameplayEvent);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write((int)AbilityEffectTimelineShapeKind.None);
                    writer.Write((int)AbilityEffectTimelineAnchorMode.CapturedAnchor);
                    WriteRawVector(writer);
                    WriteRawVector(writer);
                    writer.Write(0L);
                    writer.Write(0);
                    writer.Write(0); writer.Write(string.Empty); // target policy
                    writer.Write(0); writer.Write(string.Empty); // gameplay request
                    writer.Write(0); // effect
                    writer.Write(0); writer.Write(string.Empty); // presentation
                    writer.Write(false);
                    writer.Write(false);
                });
        }

        private static void WriteAbilityDefinition(BinaryWriter writer, int abilityId, string stableId)
        {
            writer.Write(abilityId);
            writer.Write(stableId);
            writer.Write("Duplicate Ability");
            writer.Write(930001); writer.Write("TestIntent");
            writer.Write(930002); writer.Write("testSlot");
            writer.Write(0L);
            writer.Write(0); writer.Write(string.Empty); // cooldown group
            writer.Write(0); // costs
            writer.Write(0); writer.Write(string.Empty); // target policy
            writer.Write(301);
            writer.Write(930003); writer.Write("ActionTest");
            writer.Write(false); // no damage payload
            writer.Write(0); writer.Write(string.Empty); // presentation
            writer.Write(0); writer.Write(string.Empty); // effect timeline
        }

        private static void WriteRawVector(BinaryWriter writer)
        {
            writer.Write(0L);
            writer.Write(0L);
            writer.Write(0L);
        }

        private static byte[] BuildCatalog(
            string catalogKind,
            string schemaVersion,
            int entryCount,
            Action<BinaryWriter> writePayload)
        {
            byte[] payload;
            using (var payloadStream = new MemoryStream())
            using (var payloadWriter = new BinaryWriter(payloadStream))
            {
                writePayload(payloadWriter);
                payload = payloadStream.ToArray();
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                RuntimeCatalogBinary.WriteHeader(
                    writer,
                    catalogKind,
                    schemaVersion,
                    "runtime-catalog-tests",
                    entryCount,
                    RuntimeCatalogBinary.ComputeStableHash(payload, 0, payload.Length));
                writer.Write(payload);
                return stream.ToArray();
            }
        }

        private static List<VfxSpawnRequest> ResolveKnownVfxMapping(VfxTriggerMapResolver resolver)
        {
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                new RuntimeFrame(1),
                VfxSourceDomains.CombatHit,
                MxFramework.Game.CharacterTest.CharacterTestCharacterActionConfigSource.FireballPresentationProfileId,
                0,
                0,
                0L,
                0,
                0,
                VfxAnchor.World(VfxPose.Identity),
                VfxParameterBlock.Empty,
                new RuntimeTraceIdentity(990001)),
                output);
            Assert.AreEqual(1, count);
            return output;
        }

        private static List<CombatProjectileUnityPresentationBindingV2> CopyBindings(
            CombatProjectileUnityPresentationRegistry registry)
        {
            var bindings = new List<CombatProjectileUnityPresentationBindingV2>();
            registry.CopyBindingsV2To(bindings);
            return bindings;
        }

        private static int CompareProjectileBindings(
            CombatProjectileUnityPresentationBindingV2 left,
            CombatProjectileUnityPresentationBindingV2 right)
        {
            return left.ProjectileDefinitionId.CompareTo(right.ProjectileDefinitionId);
        }

        private static void AssertLoaded<TCatalog>(RuntimeCatalogBinaryLoadResult<TCatalog> result)
        {
            Assert.IsTrue(
                result.Success,
                result.FallbackReason + ": " + string.Join("; ", result.Diagnostics));
            Assert.IsNotNull(result.Catalog);
        }

        private static void AssertLoadFailed<TCatalog>(
            RuntimeCatalogBinaryLoadResult<TCatalog> result,
            string fallbackReason)
        {
            Assert.IsFalse(result.Success);
            Assert.AreEqual(fallbackReason, result.FallbackReason);
            Assert.IsNotEmpty(result.Diagnostics);
        }

        private static byte[] Clone(byte[] bytes)
        {
            var copy = new byte[bytes.Length];
            Array.Copy(bytes, copy, bytes.Length);
            return copy;
        }

        private static byte[] ReadBytes(string relativePath)
        {
            return File.ReadAllBytes(PathFor(relativePath));
        }

        private static string PathFor(string relativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        }
    }
}
