using System;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public sealed class ResourcePlanOrchestratorTests
    {
        [Test]
        public void PreloadAcquireRelease_HoldsHandlesAndReleaseIsIdempotent()
        {
            ResourceFixture fixture = CreateFixture(BodyKey, SwordKey);
            var orchestrator = new ResourcePlanOrchestrator(
                new ResourcePreloadService(fixture.Manager),
                fixture.Manager);
            ResourcePlan plan = CreatePlan("hash-a", BodyKey, SwordKey);

            ResourcePreloadResult preload = orchestrator.Preload(plan).Result.Value;
            ResourcePlanSession session = orchestrator.Acquire(plan, preload);

            Assert.IsTrue(preload.Success);
            Assert.AreEqual(2, session.LoadedResourceHandles.Count);
            Assert.AreEqual("hash-a", session.PlanHash);

            orchestrator.Release(session);
            orchestrator.Release(session);

            Assert.IsTrue(session.IsReleased);
            Assert.AreEqual(2, fixture.Provider.ReleaseCount);
            AssertDiagnostic(session, ResourcePlanDiagnosticCodes.SessionAlreadyReleased);
        }

        [Test]
        public void PrepareChange_PreloadsOnlyNewResourcesAndReleasesOldResourcesOnCommit()
        {
            ResourceFixture fixture = CreateFixture(BodyKey, SwordKey, ShieldKey, AxeKey);
            var orchestrator = new ResourcePlanOrchestrator(
                new ResourcePreloadService(fixture.Manager),
                fixture.Manager);
            ResourcePlan current = CreatePlan("hash-a", BodyKey, SwordKey, ShieldKey);
            ResourcePlan next = CreatePlan("hash-b", BodyKey, ShieldKey, AxeKey);
            ResourcePlanSession session = orchestrator.Acquire(
                current,
                orchestrator.Preload(current).Result.Value);

            ResourcePlanDiff diff = orchestrator.PrepareChange(session, next);

            CollectionAssert.AreEqual(new[] { BodyKey, ShieldKey }, diff.KeepResources);
            CollectionAssert.AreEqual(new[] { AxeKey }, diff.PreloadResources);
            CollectionAssert.AreEqual(new[] { SwordKey }, diff.ReleaseResources);
            Assert.IsTrue(diff.PreloadOperation.Result.Value.Success);

            orchestrator.CommitChange(session, diff);

            Assert.AreEqual("hash-b", session.PlanHash);
            Assert.AreEqual(3, session.LoadedResourceHandles.Count);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);

            orchestrator.Release(session);

            Assert.AreEqual(4, fixture.Provider.ReleaseCount);
        }

        private static void AssertDiagnostic(ResourcePlanSession session, string code)
        {
            for (int i = 0; i < session.Diagnostics.Count; i++)
            {
                if (string.Equals(session.Diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }

            Assert.Fail("Expected diagnostic code: " + code);
        }

        private static ResourcePlan CreatePlan(string hash, params ResourceKey[] keys)
        {
            return new ResourcePlan("resource.plan.test", hash, keys, labels: null, failFast: true);
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
            manager.AddCatalog(new ResourceCatalog("resource.plan.test", PackageId, entries));
            return new ResourceFixture(manager, provider);
        }

        private static readonly string PackageId = "test_package";
        private static readonly ResourceKey BodyKey = new ResourceKey("resource.plan.body", ResourceTypeIds.GameObject, "default", PackageId);
        private static readonly ResourceKey SwordKey = new ResourceKey("resource.plan.weapon.sword", ResourceTypeIds.GameObject, "default", PackageId);
        private static readonly ResourceKey ShieldKey = new ResourceKey("resource.plan.weapon.shield", ResourceTypeIds.GameObject, "default", PackageId);
        private static readonly ResourceKey AxeKey = new ResourceKey("resource.plan.weapon.axe", ResourceTypeIds.GameObject, "default", PackageId);

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
