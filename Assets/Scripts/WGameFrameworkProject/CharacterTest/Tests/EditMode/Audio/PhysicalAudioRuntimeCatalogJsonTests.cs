using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class PhysicalAudioRuntimeCatalogJsonTests
    {
        [Test]
        public void TryLoad_ParsesLegacyV1AsCompleteSyntheticPairCatalog()
        {
            const string json = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v1"",
  ""schemaVersion"": 1,
  ""physicalManifestHash"": ""physical-hash"",
  ""audioCatalogHash"": ""audio-hash"",
  ""materials"": [
    { ""runtimeMaterialId"": 510001, ""hardness"": 0.8, ""density"": 0.7, ""roughness"": 0.1, ""damping"": 0.2, ""resonance"": 0.8 },
    { ""runtimeMaterialId"": 510002, ""hardness"": 0.6, ""density"": 0.9, ""roughness"": 0.7, ""damping"": 0.4, ""resonance"": 0.7 }
  ],
  ""families"": [{
    ""runtimeFamilyId"": 520001,
    ""interactionKind"": 3,
    ""interactionMode"": 3,
    ""fallbackFamilyRuntimeId"": 0,
    ""fallbackBasisRuntimeId"": 0,
    ""responseProfileRuntimeId"": 540001,
    ""priority"": -10,
    ""cooldownFrames"": 3,
    ""weights"": { ""material"": 4, ""mass"": 1, ""contactArea"": 1, ""surface"": 0.5, ""construction"": 0.5, ""speed"": 1, ""intensity"": 1, ""angle"": 0.5 }
  }],
  ""basisSounds"": [{
    ""runtimeBasisId"": 530001,
    ""familyRuntimeId"": 520001,
    ""runtimeCueId"": 500201,
    ""priority"": -2,
    ""impactor"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": 0.5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": 0.2 }, ""surface"": 0, ""construction"": 0 },
    ""receiver"": { ""materialRuntimeId"": 510002, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": 0.8 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": 0.2 }, ""surface"": 0, ""construction"": 0 },
    ""relativeSpeed"": { ""min"": 0, ""max"": 10, ""anchor"": 5 },
    ""intensity"": { ""min"": 0, ""max"": 1, ""anchor"": 0.5 },
    ""angleDegrees"": { ""min"": 0, ""max"": 180, ""anchor"": 90 }
  }],
  ""responseProfiles"": [{
    ""runtimeResponseProfileId"": 540001,
    ""curves"": [{ ""parameterId"": 500251, ""input"": 6, ""outputMin"": 0, ""outputMax"": 1, ""points"": [{ ""input"": 0, ""output"": 0 }, { ""input"": 1, ""output"": 1 }] }]
  }]
}";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(json, out PhysicalAudioDefinitionCatalog catalog, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(catalog.PhysicalManifestHash, Is.EqualTo("physical-hash"));
            Assert.That(catalog.AudioCatalogHash, Is.EqualTo("audio-hash"));
            Assert.That(catalog.GetFamilyCandidateCount(PhysicalAudioInteractionKind.ObjectImpact, PhysicalAudioInteractionMode.Impact), Is.EqualTo(1));
            Assert.That(catalog.GetBasisCandidateCount(new PhysicalAudioFamilyId(520001)), Is.EqualTo(0),
                "legacy v1 must be converted to the v2 Pair/Variant index instead of retaining a private Family basis range");
            Assert.That(catalog.TryGetFamily(new PhysicalAudioFamilyId(520001), out PhysicalAudioFamilyDefinition family), Is.True);
            Assert.That(family.Priority, Is.EqualTo(-10));
            Assert.That(catalog.TryGetBasis(new AudioBasisId(530001), out PhysicalAudioBasisDefinition basis), Is.True);
            Assert.That(basis.CueId, Is.EqualTo(500201));
            Assert.That(basis.Priority, Is.EqualTo(-2));
            Assert.That(catalog.MaterialPairCount, Is.EqualTo(1));
            Assert.That(basis.MaterialPairId.IsValid, Is.True);
            Assert.That(basis.VariantKind, Is.EqualTo(PhysicalAudioVariantKind.Impact));
        }

        [Test]
        public void TryLoad_ParsesV2PairVariantAndMaterialFallbackIndexes()
        {
            const string json = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v2"",
  ""schemaVersion"": 2,
  ""physicalManifestHash"": ""physical-v2-hash"",
  ""audioCatalogHash"": ""audio-hash"",
  ""materials"": [
    { ""runtimeMaterialId"": 510001, ""hardness"": 0.8, ""density"": 0.7, ""roughness"": 0.1, ""damping"": 0.2, ""resonance"": 0.8, ""fallbackMaterialRuntimeId"": 0 },
    { ""runtimeMaterialId"": 510002, ""hardness"": 0.6, ""density"": 0.9, ""roughness"": 0.7, ""damping"": 0.4, ""resonance"": 0.7, ""fallbackMaterialRuntimeId"": 0 },
    { ""runtimeMaterialId"": 510003, ""hardness"": 0.2, ""density"": 0.2, ""roughness"": 0.5, ""damping"": 0.9, ""resonance"": 0.2, ""fallbackMaterialRuntimeId"": 510001 }
  ],
  ""materialPairs"": [{ ""runtimeMaterialPairId"": 550001, ""materialARuntimeId"": 510001, ""materialBRuntimeId"": 510002 }],
  ""families"": [{
    ""runtimeFamilyId"": 520001,
    ""interactionKind"": 3,
    ""interactionMode"": 3,
    ""variantKind"": 1,
    ""fallbackFamilyRuntimeId"": 0,
    ""fallbackBasisRuntimeId"": 0,
    ""responseProfileRuntimeId"": 540001,
    ""priority"": 10,
    ""cooldownFrames"": 3,
    ""weights"": { ""material"": 4, ""mass"": 1, ""contactArea"": 1, ""surface"": 0.5, ""construction"": 0.5, ""speed"": 1, ""intensity"": 1, ""angle"": 0.5 }
  }],
  ""basisSounds"": [{
    ""runtimeBasisId"": 530001,
    ""materialPairRuntimeId"": 550001,
    ""variantKind"": 1,
    ""runtimeCueId"": 500201,
    ""priority"": 2,
    ""sideA"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": 0.5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": 0.2 }, ""surface"": 0, ""construction"": 0 },
    ""sideB"": { ""materialRuntimeId"": 510002, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": 0.8 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": 0.2 }, ""surface"": 0, ""construction"": 0 },
    ""relativeSpeed"": { ""min"": 0, ""max"": 10, ""anchor"": 5 },
    ""intensity"": { ""min"": 0, ""max"": 1, ""anchor"": 0.5 },
    ""angleDegrees"": { ""min"": 0, ""max"": 180, ""anchor"": 90 }
  }],
  ""responseProfiles"": [{
    ""runtimeResponseProfileId"": 540001,
    ""curves"": [{ ""parameterId"": 500251, ""input"": 6, ""outputMin"": 0, ""outputMax"": 1, ""points"": [{ ""input"": 0, ""output"": 0 }, { ""input"": 1, ""output"": 1 }] }]
  }]
}";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(json, out PhysicalAudioDefinitionCatalog catalog, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(catalog.MaterialPairCount, Is.EqualTo(1));
            Assert.That(catalog.TryFindMaterialPair(new AcousticMaterialId(510002), new AcousticMaterialId(510001), out AcousticMaterialPairDefinition pair), Is.True);
            Assert.That(pair.Id, Is.EqualTo(new AcousticMaterialPairId(550001)));
            Assert.That(catalog.GetPairVariantBasisCandidateCount(pair.Id, PhysicalAudioVariantKind.Impact), Is.EqualTo(1));
            Assert.That(catalog.TryGetMaterial(new AcousticMaterialId(510003), out AcousticMaterialProfileDefinition fallbackMaterial), Is.True);
            Assert.That(fallbackMaterial.FallbackMaterialId, Is.EqualTo(new AcousticMaterialId(510001)));
        }

        [Test]
        public void TryLoad_ParsesContinuousInteractionContracts()
        {
            const string json = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v2"", ""schemaVersion"": 2,
  ""physicalManifestHash"": ""physical"", ""audioCatalogHash"": ""audio"",
  ""materials"": [{ ""runtimeMaterialId"": 510001 }],
  ""materialPairs"": [{ ""runtimeMaterialPairId"": 550001, ""materialARuntimeId"": 510001, ""materialBRuntimeId"": 510001 }],
  ""families"": [
    { ""runtimeFamilyId"": 520007, ""interactionKind"": 7, ""interactionMode"": 7, ""variantKind"": 7 },
    { ""runtimeFamilyId"": 520008, ""interactionKind"": 8, ""interactionMode"": 8, ""variantKind"": 8 },
    { ""runtimeFamilyId"": 520009, ""interactionKind"": 9, ""interactionMode"": 9, ""variantKind"": 9 }
  ],
  ""basisSounds"": [
    { ""runtimeBasisId"": 530007, ""familyRuntimeId"": 520007, ""materialPairRuntimeId"": 550001, ""variantKind"": 7, ""runtimeCueId"": 500307,
      ""sideA"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": .5 } },
      ""sideB"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": .5 } },
      ""relativeSpeed"": { ""min"": 0, ""max"": 10, ""anchor"": 5 }, ""intensity"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""angleDegrees"": { ""min"": 0, ""max"": 180, ""anchor"": 90 } },
    { ""runtimeBasisId"": 530008, ""familyRuntimeId"": 520008, ""materialPairRuntimeId"": 550001, ""variantKind"": 8, ""runtimeCueId"": 500308,
      ""sideA"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": .5 } },
      ""sideB"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": .5 } },
      ""relativeSpeed"": { ""min"": 0, ""max"": 10, ""anchor"": 5 }, ""intensity"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""angleDegrees"": { ""min"": 0, ""max"": 180, ""anchor"": 90 } },
    { ""runtimeBasisId"": 530009, ""familyRuntimeId"": 520009, ""materialPairRuntimeId"": 550001, ""variantKind"": 9, ""runtimeCueId"": 500309,
      ""sideA"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": .5 } },
      ""sideB"": { ""materialRuntimeId"": 510001, ""mass"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""contactArea"": { ""min"": 0, ""max"": 1, ""anchor"": .5 } },
      ""relativeSpeed"": { ""min"": 0, ""max"": 10, ""anchor"": 5 }, ""intensity"": { ""min"": 0, ""max"": 1, ""anchor"": .5 }, ""angleDegrees"": { ""min"": 0, ""max"": 180, ""anchor"": 90 } }
  ],
  ""responseProfiles"": []
}";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(json, out PhysicalAudioDefinitionCatalog catalog, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(catalog.GetFamilyCandidateCount(PhysicalAudioInteractionKind.Scrape, PhysicalAudioInteractionMode.Scrape), Is.EqualTo(1));
            Assert.That(catalog.GetFamilyCandidateCount(PhysicalAudioInteractionKind.Roll, PhysicalAudioInteractionMode.Roll), Is.EqualTo(1));
            Assert.That(catalog.GetFamilyCandidateCount(PhysicalAudioInteractionKind.Compression, PhysicalAudioInteractionMode.Compression), Is.EqualTo(1));
            Assert.That(catalog.GetPairVariantBasisCandidateCount(new AcousticMaterialPairId(550001), PhysicalAudioVariantKind.Scrape), Is.EqualTo(1));
            Assert.That(catalog.GetPairVariantBasisCandidateCount(new AcousticMaterialPairId(550001), PhysicalAudioVariantKind.Roll), Is.EqualTo(1));
            Assert.That(catalog.GetPairVariantBasisCandidateCount(new AcousticMaterialPairId(550001), PhysicalAudioVariantKind.Compression), Is.EqualTo(1));
        }

        [Test]
        public void TryLoad_RejectsCatalogWithMissingAudioHash()
        {
            const string json = @"{ ""format"": ""mx.audioPhysicalRuntimeCatalog.v1"", ""schemaVersion"": 1, ""physicalManifestHash"": ""p"", ""audioCatalogHash"": """" }";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(json, out PhysicalAudioDefinitionCatalog catalog, out string error);

            Assert.That(success, Is.False);
            Assert.That(catalog, Is.Null);
            Assert.That(error, Does.Contain("audioCatalogHash"));
        }

        [Test]
        public void TryLoad_RejectsFallbackFamilyCycle()
        {
            const string json = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v1"",
  ""schemaVersion"": 1,
  ""physicalManifestHash"": ""physical-hash"",
  ""audioCatalogHash"": ""audio-hash"",
  ""materials"": [],
  ""families"": [
    { ""runtimeFamilyId"": 520001, ""interactionKind"": 3, ""interactionMode"": 3, ""fallbackFamilyRuntimeId"": 520002, ""fallbackBasisRuntimeId"": 0, ""responseProfileRuntimeId"": 0, ""priority"": 10, ""cooldownFrames"": 0 },
    { ""runtimeFamilyId"": 520002, ""interactionKind"": 3, ""interactionMode"": 0, ""fallbackFamilyRuntimeId"": 520001, ""fallbackBasisRuntimeId"": 0, ""responseProfileRuntimeId"": 0, ""priority"": 0, ""cooldownFrames"": 0 }
  ],
  ""basisSounds"": [],
  ""responseProfiles"": []
}";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(
                json,
                out PhysicalAudioDefinitionCatalog catalog,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(catalog, Is.Null);
            Assert.That(error, Does.Contain("cycle"));
        }

        [Test]
        public void TryLoad_RejectsMaterialFallbackCycle()
        {
            const string json = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v2"",
  ""schemaVersion"": 2,
  ""physicalManifestHash"": ""physical-hash"",
  ""audioCatalogHash"": ""audio-hash"",
  ""materials"": [
    { ""runtimeMaterialId"": 510001, ""fallbackMaterialRuntimeId"": 510002 },
    { ""runtimeMaterialId"": 510002, ""fallbackMaterialRuntimeId"": 510001 }
  ],
  ""materialPairs"": [],
  ""families"": [],
  ""basisSounds"": [],
  ""responseProfiles"": []
}";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(json, out PhysicalAudioDefinitionCatalog catalog, out string error);

            Assert.That(success, Is.False);
            Assert.That(catalog, Is.Null);
            Assert.That(error, Does.Contain("cycle"));
        }

        [Test]
        public void TryLoad_RejectsLegacyStringEnums()
        {
            const string json = @"{
  ""format"": ""mx.audioPhysicalRuntimeCatalog.v1"",
  ""schemaVersion"": 1,
  ""physicalManifestHash"": ""physical-hash"",
  ""audioCatalogHash"": ""audio-hash"",
  ""materials"": [],
  ""families"": [{
    ""runtimeFamilyId"": 520001,
    ""interactionKind"": ""ObjectImpact"",
    ""interactionMode"": ""Impact"",
    ""fallbackFamilyRuntimeId"": 0,
    ""fallbackBasisRuntimeId"": 0,
    ""responseProfileRuntimeId"": 0,
    ""priority"": 0,
    ""cooldownFrames"": 0
  }],
  ""basisSounds"": [],
  ""responseProfiles"": []
}";

            bool success = PhysicalAudioRuntimeCatalogJson.TryLoad(
                json,
                out PhysicalAudioDefinitionCatalog catalog,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(catalog, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
