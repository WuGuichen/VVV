using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterResourceOrchestratorTests
    {
        [Test]
        public void PreloadAcquireRelease_HoldsResourceHandlesAndRecordsAudioPlan()
        {
            ResourceFixture fixture = CreateFixture(BodyKey, SwordKey);
            var orchestrator = new CharacterResourceOrchestrator(
                new ResourcePreloadService(fixture.Manager),
                fixture.Manager);
            CharacterResourcePlan plan = CreatePlan("hash-a", new[] { SwordKey }, new[] { "Master", "Character" }, new[] { 500101 });

            ResourcePreloadResult preload = orchestrator.PreloadForSpawn(plan).Result.Value;
            CharacterResourceSession session = orchestrator.AcquireForSpawn(plan, preload);

            Assert.IsTrue(preload.Success);
            Assert.AreEqual(2, session.LoadedResourceHandles.Count);
            Assert.AreEqual("hash-a", session.PlanHash);
            CollectionAssert.AreEqual(new[] { "Master", "Character" }, session.AudioBanks);
            CollectionAssert.AreEqual(new[] { 500101 }, session.AudioCueIds);
            AssertDiagnostic(session, CharacterResourceDiagnostics.AudioWarmupDeferred);

            orchestrator.Release(session);
            orchestrator.Release(session);

            Assert.IsTrue(session.IsReleased);
            Assert.AreEqual(2, fixture.Provider.ReleaseCount);
            AssertDiagnostic(session, CharacterResourceDiagnostics.SessionAlreadyReleased);
        }

        [Test]
        public void EquipmentChangeDiff_PreloadsNewResourcesKeepsSharedResourcesAndReleasesOldResources()
        {
            ResourceFixture fixture = CreateFixture(BodyKey, SwordKey, ShieldKey, AxeKey);
            var orchestrator = new CharacterResourceOrchestrator(
                new ResourcePreloadService(fixture.Manager),
                fixture.Manager);
            CharacterResourcePlan current = CreatePlan("hash-a", new[] { SwordKey, ShieldKey }, new[] { "Character" }, new[] { 500101 });
            CharacterResourcePlan next = CreatePlan("hash-b", new[] { ShieldKey, AxeKey }, new[] { "Character", "Weapon" }, new[] { 500101, 500202 });
            CharacterResourceSession session = orchestrator.AcquireForSpawn(
                current,
                orchestrator.PreloadForSpawn(current).Result.Value);

            CharacterEquipmentResourceDiff diff = orchestrator.PrepareEquipmentChange(session, next);

            CollectionAssert.AreEqual(new[] { ShieldKey }, diff.KeepResources);
            CollectionAssert.AreEqual(new[] { AxeKey }, diff.PreloadResources);
            CollectionAssert.AreEqual(new[] { SwordKey }, diff.ReleaseResources);
            CollectionAssert.AreEqual(new[] { "Character" }, diff.AudioDiff.KeepBanks);
            CollectionAssert.AreEqual(new[] { "Weapon" }, diff.AudioDiff.PreloadBanks);
            CollectionAssert.AreEqual(new[] { 500202 }, diff.AudioDiff.PreloadCueIds);
            Assert.IsTrue(diff.PreloadOperation.Result.Value.Success);

            orchestrator.CommitEquipmentChange(session, diff);

            Assert.AreEqual("hash-b", session.PlanHash);
            Assert.AreEqual(3, session.LoadedResourceHandles.Count);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
            AssertDiagnostic(session, CharacterResourceDiagnostics.AudioWarmupDeferred);

            orchestrator.Release(session);

            Assert.AreEqual(4, fixture.Provider.ReleaseCount);
        }

        private static void AssertDiagnostic(CharacterResourceSession session, string code)
        {
            for (int i = 0; i < session.Diagnostics.Count; i++)
            {
                if (string.Equals(session.Diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }

            Assert.Fail("Expected diagnostic code: " + code);
        }

        private static CharacterResourcePlan CreatePlan(
            string hash,
            ResourceKey[] equipmentResources,
            string[] banks,
            int[] cues)
        {
            return new CharacterResourcePlan(
                "char.test",
                hash,
                new[]
                {
                    new CharacterResourcePlanGroup(
                        CharacterResourcePlanGroupKind.SpawnCritical,
                        "spawn",
                        new[] { BodyKey },
                        required: true,
                        failurePolicy: CharacterResourceFailurePolicy.FailSpawn),
                    new CharacterResourcePlanGroup(
                        CharacterResourcePlanGroupKind.EquipmentInitial,
                        "equipment",
                        equipmentResources,
                        required: true,
                        failurePolicy: CharacterResourceFailurePolicy.UseFallbackEquipment)
                },
                new CharacterAudioResourcePlan(banks, cues, Array.Empty<string>()));
        }

        private static ResourceFixture CreateFixture(params ResourceKey[] keys)
        {
            var provider = new MemoryResourceProvider();
            var entries = new ResourceCatalogEntry[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ResourceKey key = keys[i];
                string address = key.Id.Replace('.', '/');
                provider.Register(address, key.Id);
                entries[i] = new ResourceCatalogEntry(
                    key.Id,
                    key.TypeId,
                    provider.ProviderId,
                    address,
                    key.Variant,
                    key.PackageId);
            }

            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("character.test", PackageId, entries));
            return new ResourceFixture(manager, provider);
        }

        private static readonly string PackageId = "test_package";
        private static readonly ResourceKey BodyKey = new ResourceKey("char.test.prefab.body", ResourceTypeIds.GameObject, "default", PackageId);
        private static readonly ResourceKey SwordKey = new ResourceKey("char.test.prefab.weapon.sword", ResourceTypeIds.GameObject, "default", PackageId);
        private static readonly ResourceKey ShieldKey = new ResourceKey("char.test.prefab.weapon.shield", ResourceTypeIds.GameObject, "default", PackageId);
        private static readonly ResourceKey AxeKey = new ResourceKey("char.test.prefab.weapon.axe", ResourceTypeIds.GameObject, "default", PackageId);

        private readonly struct ResourceFixture
        {
            public ResourceFixture(ResourceManager manager, MemoryResourceProvider provider)
            {
                Manager = manager;
                Provider = provider;
            }

            public ResourceManager Manager { get; }
            public MemoryResourceProvider Provider { get; }
        }
    }
}
