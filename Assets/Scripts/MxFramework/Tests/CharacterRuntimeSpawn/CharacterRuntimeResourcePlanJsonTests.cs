using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterRuntimeResourcePlanJsonTests
    {
        [Test]
        public void CompiledResourceArtifacts_MapToRuntimeCatalogPlanAndAudioManifest()
        {
            ResourceCatalog catalog = CharacterImportedPackageJson.LoadResourceCatalog(RuntimeCatalogJson);
            CharacterResourcePlan plan = CharacterImportedPackageJson.LoadCharacterResourcePlan(ResourcePlanJson);
            CharacterAudioCueManifest audio = CharacterImportedPackageJson.LoadAudioCueManifest(AudioManifestJson);

            Assert.AreEqual("runtime.catalog.test", catalog.CatalogId);
            Assert.AreEqual(2, catalog.Entries.Count);
            Assert.AreEqual("10", catalog.Entries[0].ProviderData["retainPriority"]);
            Assert.AreEqual("char.test", plan.CharacterStableId);
            Assert.AreEqual("hash-a", plan.PlanHash);
            CollectionAssert.AreEqual(new[] { BodyKey }, plan.GetResourceKeys(CharacterResourcePlanGroupKind.SpawnCritical));
            CollectionAssert.AreEqual(new[] { SwordKey }, plan.GetResourceKeys(CharacterResourcePlanGroupKind.EquipmentInitial));
            CollectionAssert.AreEqual(new[] { "Master", "Character" }, plan.Audio.RequiredBanks);
            CollectionAssert.AreEqual(new[] { 500101 }, plan.Audio.RequiredCueIds);
            CollectionAssert.AreEqual(new[] { "500101", "cue.hit" }, plan.Audio.RequiredCueKeys);
            Assert.AreEqual(1, audio.Cues.Count);
            Assert.AreEqual("cue.hit", audio.Cues[0].CueId);

            var provider = new MemoryResourceProvider();
            provider.Register("body", "body");
            provider.Register("sword", "sword");
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(catalog);

            var orchestrator = new CharacterResourceOrchestrator(
                new ResourcePreloadService(manager),
                manager);
            ResourcePreloadResult preload = orchestrator.PreloadForSpawn(plan).Result.Value;
            CharacterResourceSession session = orchestrator.AcquireForSpawn(plan, preload);

            Assert.IsTrue(preload.Success);
            Assert.AreEqual(2, session.LoadedResourceHandles.Count);
            CollectionAssert.AreEqual(new[] { "500101", "cue.hit" }, session.AudioCueKeys);

            orchestrator.Release(session);
            Assert.AreEqual(2, provider.ReleaseCount);
        }

        private static readonly ResourceKey BodyKey = new ResourceKey("char.test.body", ResourceTypeIds.GameObject, "default", "test_package");
        private static readonly ResourceKey SwordKey = new ResourceKey("char.test.weapon.sword", ResourceTypeIds.GameObject, "default", "test_package");

        private const string RuntimeCatalogJson = @"{
  ""format"": ""mx.characterRuntimeResourceCatalog.v1"",
  ""schemaVersion"": 1,
  ""catalogId"": ""runtime.catalog.test"",
  ""packageId"": ""test_package"",
  ""entries"": [
    {
      ""id"": ""char.test.body"",
      ""type"": ""GameObject"",
      ""provider"": ""memory"",
      ""address"": ""body"",
      ""variant"": ""default"",
      ""packageId"": ""test_package"",
      ""labels"": [""spawnCritical""],
      ""dependencies"": [],
      ""hash"": ""hash-body"",
      ""size"": 12,
      ""allowOverride"": false,
      ""providerData"": { ""retainPriority"": ""10"" }
    },
    {
      ""id"": ""char.test.weapon.sword"",
      ""type"": ""GameObject"",
      ""provider"": ""memory"",
      ""address"": ""sword"",
      ""variant"": ""default"",
      ""packageId"": ""test_package"",
      ""labels"": [""equipmentInitial""],
      ""dependencies"": [],
      ""hash"": ""hash-sword"",
      ""size"": 7,
      ""allowOverride"": false,
      ""providerData"": {}
    }
  ]
}";

        private const string ResourcePlanJson = @"{
  ""format"": ""mx.characterResourcePlan.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""test_package"",
  ""characterStableId"": ""char.test"",
  ""planHash"": ""hash-a"",
  ""spawnCritical"": {
    ""required"": true,
    ""failurePolicy"": ""FailSpawn"",
    ""resources"": [
      {
        ""resourceKey"": ""char.test.body"",
        ""typeId"": ""GameObject"",
        ""variant"": ""default"",
        ""packageId"": ""test_package"",
        ""usage"": ""characterModel"",
        ""stableId"": ""char.test.body""
      }
    ]
  },
  ""equipmentInitial"": {
    ""required"": true,
    ""failurePolicy"": ""UseFallbackEquipment"",
    ""resources"": [
      {
        ""resourceKey"": ""char.test.weapon.sword"",
        ""typeId"": ""GameObject"",
        ""variant"": ""default"",
        ""packageId"": ""test_package"",
        ""usage"": ""weaponModel"",
        ""stableId"": ""char.test.weapon.sword""
      }
    ]
  },
  ""audio"": {
    ""required"": true,
    ""failurePolicy"": ""MuteMissingCue"",
    ""requiredBanks"": [""Master"", ""Character""],
    ""requiredCues"": [""500101"", ""cue.hit""]
  }
}";

        private const string AudioManifestJson = @"{
  ""format"": ""mx.characterAudioCueManifest.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""test_package"",
  ""characterStableId"": ""char.test"",
  ""banks"": [""Master"", ""Character""],
  ""cues"": [
    {
      ""cueId"": ""cue.hit"",
      ""stableId"": ""audio.hit"",
      ""resourceKey"": ""audio.hit"",
      ""eventPath"": ""event:/Character/Test/Hit"",
      ""bank"": ""Character"",
      ""fallbackPolicy"": ""MuteMissingCue"",
      ""providerData"": { ""fmodEventGuid"": ""guid-hit"" }
    }
  ]
}";
    }
}
