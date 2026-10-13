using System;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public sealed class ResourcePlanOrchestratorTests
    {
        [TestCase("global-package")]
        [TestCase("profile-variant")]
        [TestCase("label-variant")]
        [TestCase("logical-aliases")]
        [TestCase("explicit-package")]
        [TestCase("explicit-variant")]
        [TestCase("fallback-variant")]
        public void StrongPreparation_ResolvedRequestsPreserveEachAcquisition(string scenario)
        {
            const string id = "demo.preload";
            const string package = "demo.package";
            const string label = "warmup.demo";
            bool useVariant = scenario != "global-package" && scenario != "explicit-package";
            var provider = new MemoryResourceProvider().Register("default", "Default").Register("high", "High");
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("resolved", package, new[]
            {
                new ResourceCatalogEntry(id, ResourceTypeIds.String, provider.ProviderId, "default", labels: new[] { label }),
                new ResourceCatalogEntry(id, ResourceTypeIds.String, provider.ProviderId, "high", "pc.high", labels: new[] { label })
            }));
            if (useVariant)
                manager.SetVariantProfile(new ResourceVariantProfile(
                    scenario == "fallback-variant" ? "pc.ultra" : "pc.high", new[] { "pc.high", string.Empty }));

            var logicalKey = new ResourceKey(id, ResourceTypeIds.String);
            var resolvedKey = new ResourceKey(id, ResourceTypeIds.String, useVariant ? "pc.high" : string.Empty, package);
            ResourceKey[] keys = scenario == "label-variant" ? Array.Empty<ResourceKey>()
                : scenario == "logical-aliases" ? new[] { logicalKey, resolvedKey }
                : new[] { scenario == "explicit-package" || scenario == "explicit-variant" ? resolvedKey : logicalKey };
            var plan = new ResourcePlan("resolved", scenario, keys,
                labels: scenario == "label-variant" ? new[] { label } : null);
            var service = new ResourcePreloadService(manager);
            var orchestrator = new ResourcePlanOrchestrator(service, manager);
            ResourceRuntimeConfigurationIdentity identity = manager.SealRuntimeConfiguration();
            int count = scenario == "logical-aliases" ? 2 : 1;

            ResourcePreloadResult ordinary = orchestrator.Preload(plan).Result.Value;
            Assert.IsTrue(ordinary.Success);
            Assert.AreEqual(count, ordinary.RequestedCount);
            Assert.AreEqual(count, ordinary.LoadedCount);
            Assert.AreEqual(scenario == "explicit-package" || scenario == "explicit-variant" ? resolvedKey : logicalKey,
                ordinary.RequestedKeys[0]);
            service.ReleaseGroup(ordinary.Handle);
            service.ReleaseGroup(ordinary.Handle);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);

            ResourcePlanPreparation preparation = orchestrator.Prepare(identity, plan);
            Assert.AreEqual(ResourcePlanPreparationStatus.Prepared, preparation.Status);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.None, preparation.Error.Code);
            ResourcePlanAcquireResult acquired = orchestrator.AcquirePrepared(plan, preparation);
            Assert.IsTrue(acquired.Success);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.None, preparation.Error.Code);
            Assert.AreEqual(count, acquired.Session.LoadedResourceHandles.Count);
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(resolvedKey, acquired.Session.LoadedResourceHandles[i].Key);
                Assert.AreEqual(useVariant ? "High" : "Default", acquired.Session.LoadedResourceHandles[i].Value);
            }
            if (count == 2)
                Assert.AreNotSame(acquired.Session.LoadedResourceHandles[0], acquired.Session.LoadedResourceHandles[1]);
            Assert.AreEqual(count, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.AlreadyAcquired,
                orchestrator.AcquirePrepared(plan, preparation).Error.Code);
            preparation.Dispose();
            Assert.AreEqual(count, manager.CreateDebugSnapshot().TotalRefCount);

            ResourcePlanPreparation candidate = orchestrator.Prepare(identity, plan);
            Assert.AreEqual(ResourcePlanPreparationStatus.Prepared, candidate.Status);
            _ = candidate.Progress;
            _ = candidate.Error;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = candidate.Status;
                _ = candidate.Progress;
                _ = candidate.Error;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            candidate.Release();
            candidate.Dispose();
            Assert.AreEqual(ResourcePlanPreparationStatus.Released, candidate.Status);
            Assert.AreEqual(count, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.IsFalse(acquired.Session.LoadedResourceHandles[0].IsReleased);
            orchestrator.Release(acquired.Session);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(2, provider.ReleaseCount);
        }

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

        [TestCase("package")]
        [TestCase("variant")]
        [TestCase("id")]
        [TestCase("type")]
        public void StrongPreparation_RejectsSubstitutedRequestWithoutReleasingLiveOwnership(string field)
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var liveOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            ResourcePlan livePlan = CreatePlan("live", BodyKey);
            ResourcePlanAcquireResult live = liveOwner.AcquirePrepared(livePlan, liveOwner.Prepare(identity, livePlan));
            var requested = new ResourceKey(field == "id" ? "other.id" : BodyKey.Id,
                field == "type" ? ResourceTypeIds.String : BodyKey.TypeId,
                field == "variant" ? "other.variant" : BodyKey.Variant,
                field == "package" ? "other.package" : BodyKey.PackageId);
            ResourcePlan plan = CreatePlan("substituted", requested);
            var owner = new ResourcePlanOrchestrator(new SubstitutingPreloadService(fixture.Manager, BodyKey), fixture.Manager);
            ResourcePlanPreparation preparation = owner.Prepare(identity, plan);

            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.PreparationResultInvalid, owner.AcquirePrepared(plan, preparation).Error.Code);
            preparation.Release();
            preparation.Dispose();
            Assert.AreEqual(1, fixture.Manager.CreateDebugSnapshot().TotalRefCount);
            Assert.IsFalse(live.Session.LoadedResourceHandles[0].IsReleased);
            Assert.AreEqual(0, fixture.Provider.ReleaseCount);
            liveOwner.Release(live.Session);
            Assert.AreEqual(0, fixture.Manager.CreateDebugSnapshot().TotalRefCount);
        }

        [TestCase("forged")]
        [TestCase("missing")]
        [TestCase("duplicate")]
        public void StrongPreparation_UnprovenHandlesCannotAcquireOrReleaseLiveOwnership(string scenario)
        {
            ResourceFixture fixture = CreateFixture(BodyKey, SwordKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var service = new ResourcePreloadService(fixture.Manager);
            var liveOwner = new ResourcePlanOrchestrator(service, fixture.Manager);
            ResourcePlan plan = CreatePlan("unproven", BodyKey, SwordKey);
            ResourcePlanAcquireResult live = liveOwner.AcquirePrepared(plan, liveOwner.Prepare(identity, plan));
            var owner = new ResourcePlanOrchestrator(new UnprovenPreloadService(service, live.Session, scenario), fixture.Manager);
            ResourcePlanPreparation preparation = owner.Prepare(identity, plan);

            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.PreparationResultInvalid, owner.AcquirePrepared(plan, preparation).Error.Code);
            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            preparation.Release();
            preparation.Dispose();
            Assert.AreEqual(2, fixture.Manager.CreateDebugSnapshot().TotalRefCount);
            Assert.IsFalse(live.Session.LoadedResourceHandles[0].IsReleased);
            Assert.IsFalse(live.Session.LoadedResourceHandles[1].IsReleased);
            Assert.AreEqual(0, fixture.Provider.ReleaseCount);
            liveOwner.Release(live.Session);
            Assert.AreEqual(0, fixture.Manager.CreateDebugSnapshot().TotalRefCount);
        }

        [Test]
        public void StrongPreparation_RejectsPreloadFromAnotherManagerAndReleaseGroupIgnoresForeignGroups()
        {
            ResourceFixture first = CreateFixture(BodyKey);
            ResourceFixture second = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = first.Manager.SealRuntimeConfiguration();
            second.Manager.SealRuntimeConfiguration();
            var firstService = new ResourcePreloadService(first.Manager);
            var secondService = new ResourcePreloadService(second.Manager);
            var liveOwner = new ResourcePlanOrchestrator(firstService, first.Manager);
            ResourcePlan plan = CreatePlan("foreign-preload", BodyKey);
            ResourcePreloadResult ordinary = firstService.PreloadAsync(new ResourcePreloadPlan("foreign-group", new[] { BodyKey })).Result.Value;
            secondService.ReleaseGroup(ordinary.Handle);
            Assert.IsFalse(ordinary.Handle.IsReleased);
            Assert.AreEqual(1, first.Manager.CreateDebugSnapshot().TotalRefCount);
            firstService.ReleaseGroup(ordinary.Handle);
            ResourcePlanAcquireResult live = liveOwner.AcquirePrepared(plan, liveOwner.Prepare(identity, plan));
            var owner = new ResourcePlanOrchestrator(secondService, first.Manager);
            ResourcePlanPreparation preparation = owner.Prepare(identity, plan);

            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.PreparationResultInvalid, owner.AcquirePrepared(plan, preparation).Error.Code);
            Assert.AreEqual(1, first.Manager.CreateDebugSnapshot().TotalRefCount);
            Assert.IsFalse(live.Session.LoadedResourceHandles[0].IsReleased);
            Assert.AreEqual(0, second.Manager.CreateDebugSnapshot().TotalRefCount);
            liveOwner.Release(live.Session);
            Assert.AreEqual(0, first.Manager.CreateDebugSnapshot().TotalRefCount);
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

        [Test]
        public void StrongPreparation_RejectsPartialFailureAndReleasesPartialHandles()
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var orchestrator = new ResourcePlanOrchestrator(
                new ResourcePreloadService(fixture.Manager),
                fixture.Manager);
            var plan = new ResourcePlan(
                "resource.plan.test",
                "partial",
                new[] { BodyKey, SwordKey },
                labels: null,
                failFast: false);

            ResourcePlanPreparation preparation = orchestrator.Prepare(identity, plan);
            ResourcePlanAcquireResult acquire = orchestrator.AcquirePrepared(plan, preparation);

            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            Assert.IsFalse(acquire.Success);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.PreparationFailed, acquire.Error.Code);
            Assert.AreEqual(0, fixture.Manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void StrongPreparation_MalformedPartialSuccessCannotAcquireAndReleasesHandles()
        {
            ResourceFixture fixture = CreateFixture(BodyKey, SwordKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var orchestrator = new ResourcePlanOrchestrator(
                new PartialSuccessPreloadService(fixture.Manager),
                fixture.Manager);
            ResourcePlan plan = CreatePlan("malformed-success", BodyKey, SwordKey);

            ResourcePlanPreparation preparation = orchestrator.Prepare(identity, plan);
            ResourcePlanAcquireResult acquire = orchestrator.AcquirePrepared(plan, preparation);

            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            Assert.IsFalse(acquire.Success);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.PreparationResultInvalid, acquire.Error.Code);
            Assert.AreEqual(0, fixture.Manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);

            Assert.AreEqual(ResourcePlanPreparationStatus.Failed, preparation.Status);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.PreparationResultInvalid,
                orchestrator.AcquirePrepared(plan, preparation).Error.Code);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void StrongPreparation_RejectsIncompleteCrossManagerCrossPlanForeignAndRepeatedAcquireBeforeMutation()
        {
            ResourceFixture first = CreateFixture(BodyKey);
            ResourceFixture second = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity firstIdentity = first.Manager.SealRuntimeConfiguration();
            second.Manager.SealRuntimeConfiguration();
            var firstOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(first.Manager), first.Manager);
            var foreignOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(first.Manager), first.Manager);
            var otherManagerOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(second.Manager), second.Manager);
            ResourcePlan plan = CreatePlan("strong-a", BodyKey);
            ResourcePlan otherPlan = CreatePlan("strong-b", BodyKey);

            var pendingService = new PendingPreloadService();
            var pendingOwner = new ResourcePlanOrchestrator(pendingService, first.Manager);
            ResourcePlanPreparation pending = pendingOwner.Prepare(firstIdentity, plan);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.PreparationIncomplete,
                pendingOwner.AcquirePrepared(plan, pending).Error.Code);
            Assert.AreEqual(0, first.Manager.CreateDebugSnapshot().LoadedCount);

            ResourcePlanPreparation preparation = firstOwner.Prepare(firstIdentity, plan);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.ConfigurationIdentityMismatch,
                otherManagerOwner.AcquirePrepared(plan, preparation).Error.Code);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.PlanMismatch,
                firstOwner.AcquirePrepared(otherPlan, preparation).Error.Code);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.ForeignOwner,
                foreignOwner.AcquirePrepared(plan, preparation).Error.Code);
            Assert.AreEqual(1, first.Manager.CreateDebugSnapshot().LoadedCount);

            ResourcePlanAcquireResult acquired = firstOwner.AcquirePrepared(plan, preparation);
            Assert.IsTrue(acquired.Success);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.AlreadyAcquired,
                firstOwner.AcquirePrepared(plan, preparation).Error.Code);
            Assert.AreEqual(1, first.Manager.CreateDebugSnapshot().LoadedCount);

            firstOwner.Release(acquired.Session);
            Assert.AreEqual(0, first.Manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(0, second.Manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void StrongPreparation_CandidateReleaseDoesNotReleaseLivePlanOwnership()
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var liveOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            var candidateOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            ResourcePlan plan = CreatePlan("shared", BodyKey);
            ResourcePlanAcquireResult live = liveOwner.AcquirePrepared(plan, liveOwner.Prepare(identity, plan));
            ResourcePlanAcquireResult candidate = candidateOwner.AcquirePrepared(plan, candidateOwner.Prepare(identity, plan));

            Assert.IsTrue(live.Success);
            Assert.IsTrue(candidate.Success);
            Assert.AreNotSame(live.Session, candidate.Session);
            Assert.AreEqual(2, fixture.Manager.CreateDebugSnapshot().TotalRefCount);

            candidateOwner.Release(candidate.Session);

            Assert.IsFalse(live.Session.IsReleased);
            Assert.IsFalse(live.Session.LoadedResourceHandles[0].IsReleased);
            Assert.AreEqual(1, fixture.Manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(0, fixture.Provider.ReleaseCount);

            liveOwner.Release(live.Session);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void StrongPreparation_AbandonedPreparedCandidateReleasesExactlyOnceWithoutAffectingLiveSession()
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var liveOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            var candidateOwner = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            ResourcePlan plan = CreatePlan("abandon", BodyKey);
            ResourcePlanAcquireResult live = liveOwner.AcquirePrepared(plan, liveOwner.Prepare(identity, plan));
            ResourcePlanPreparation candidate = candidateOwner.Prepare(identity, plan);

            Assert.AreEqual(ResourcePlanPreparationStatus.Prepared, candidate.Status);
            Assert.AreEqual(2, fixture.Manager.CreateDebugSnapshot().TotalRefCount);

            candidate.Release();
            candidate.Dispose();

            Assert.AreEqual(ResourcePlanPreparationStatus.Released, candidate.Status);
            Assert.AreEqual(
                ResourcePlanPreparationErrorCode.PreparationReleased,
                candidateOwner.AcquirePrepared(plan, candidate).Error.Code);
            Assert.AreEqual(1, fixture.Manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(0, fixture.Provider.ReleaseCount);

            liveOwner.Release(live.Session);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void StrongPreparation_ReleaseWaitsForNonCooperativeOperationAndDrainsLateSuccess()
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var service = new DeferredSuccessPreloadService(fixture.Manager);
            var orchestrator = new ResourcePlanOrchestrator(service, fixture.Manager);
            ResourcePlanPreparation preparation = orchestrator.Prepare(identity, CreatePlan("deferred-release", BodyKey));

            preparation.Release();

            Assert.AreEqual(ResourcePlanPreparationStatus.Releasing, preparation.Status);
            Assert.AreEqual(ResourcePlanPreparationErrorCode.PreparationReleased, preparation.Error.Code);
            Assert.AreEqual(1, fixture.Manager.CreateDebugSnapshot().LoadedCount);

            service.Complete();

            Assert.AreEqual(ResourcePlanPreparationStatus.Released, preparation.Status);
            Assert.AreEqual(0, fixture.Manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
            preparation.Dispose();
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void StrongPreparation_SteadyPublicPollingAllocatesZero()
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            ResourceRuntimeConfigurationIdentity identity = fixture.Manager.SealRuntimeConfiguration();
            var orchestrator = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            ResourcePlanPreparation preparation = orchestrator.Prepare(identity, CreatePlan("noalloc", BodyKey));

            _ = preparation.Status;
            _ = preparation.Progress;
            _ = preparation.Error;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = preparation.Status;
                _ = preparation.Progress;
                _ = preparation.Error;
                _ = preparation.PlanFingerprint;
                _ = preparation.ConfigurationIdentity;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);

            preparation.Release();
            Assert.AreEqual(ResourcePlanPreparationStatus.Released, preparation.Status);
            _ = GC.GetAllocatedBytesForCurrentThread();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                preparation.Release();
                preparation.Dispose();
                _ = preparation.Status;
                _ = preparation.Progress;
                _ = preparation.Error;
                _ = preparation.PlanFingerprint;
                _ = preparation.ConfigurationIdentity;
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);

            ResourcePlan acquiredPlan = CreatePlan("noalloc-acquired", BodyKey);
            ResourcePlanPreparation acquiredPreparation = orchestrator.Prepare(identity, acquiredPlan);
            ResourcePlanAcquireResult acquired = orchestrator.AcquirePrepared(acquiredPlan, acquiredPreparation);
            Assert.IsTrue(acquired.Success);
            _ = acquiredPreparation.Status;
            _ = acquiredPreparation.Progress;
            _ = acquiredPreparation.Error;
            _ = GC.GetAllocatedBytesForCurrentThread();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                acquiredPreparation.Release();
                acquiredPreparation.Dispose();
                _ = acquiredPreparation.Status;
                _ = acquiredPreparation.Progress;
                _ = acquiredPreparation.Error;
                _ = acquiredPreparation.PlanFingerprint;
                _ = acquiredPreparation.ConfigurationIdentity;
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            orchestrator.Release(acquired.Session);
        }

        [Test]
        public void ResourcePlan_CollectionsCannotDriftAfterPreparation()
        {
            ResourcePlan plan = CreatePlan("immutable", BodyKey);

            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ResourceKey>)plan.ResourceKeys).Add(SwordKey));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)plan.Labels).Add("drift"));
            Assert.AreEqual(1, plan.ResourceKeys.Count);
        }

        [Test]
        public void StrongPreparation_PublicOwnershipCollectionsCannotBeMutatedOrLeakOnRelease()
        {
            ResourceFixture fixture = CreateFixture(BodyKey);
            var orchestrator = new ResourcePlanOrchestrator(new ResourcePreloadService(fixture.Manager), fixture.Manager);
            ResourcePlan plan = CreatePlan("immutable-ownership", BodyKey);
            ResourcePreloadResult preload = orchestrator.Preload(plan).Result.Value;

            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ResourceKey>)preload.RequestedKeys).Clear());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ResourceError>)preload.Errors).Clear());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ResourceHandle<object>>)preload.Handle.Handles).Clear());

            ResourcePlanSession session = orchestrator.Acquire(plan, preload);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ResourceHandle<object>>)session.LoadedResourceHandles).Clear());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ResourcePlanDiagnostic>)session.Diagnostics).Clear());
            orchestrator.Release(session);

            Assert.AreEqual(0, fixture.Manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, fixture.Provider.ReleaseCount);
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

        private sealed class PendingPreloadService : IResourcePreloadService
        {
            private readonly PendingPreloadOperation _operation = new PendingPreloadOperation();

            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default)
            {
                return _operation;
            }

            public void ReleaseGroup(ResourceGroupHandle handle)
            {
            }
        }

        private sealed class SubstitutingPreloadService : IResourcePreloadService
        {
            private readonly ResourcePreloadService _inner;
            private readonly ResourceKey _actualKey;

            public SubstitutingPreloadService(IResourceManager manager, ResourceKey actualKey)
            {
                _inner = new ResourcePreloadService(manager);
                _actualKey = actualKey;
            }

            public IResourceOperation<ResourcePreloadResult> PreloadAsync(ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default)
            {
                ResourcePreloadResult actual = _inner.PreloadAsync(new ResourcePreloadPlan(plan.GroupId, new[] { _actualKey })).Result.Value;
                return new ImmediateResourceOperation<ResourcePreloadResult>(ResourceLoadResult<ResourcePreloadResult>.Loaded(
                    new ResourcePreloadResult(plan.GroupId, actual.Handle, plan.ExplicitKeys, actual.Errors)));
            }

            public void ReleaseGroup(ResourceGroupHandle handle) => _inner.ReleaseGroup(handle);
        }

        private sealed class UnprovenPreloadService : IResourcePreloadService
        {
            private readonly ResourcePreloadService _inner;
            private readonly ResourcePlanSession _live;
            private readonly string _scenario;

            public UnprovenPreloadService(ResourcePreloadService inner, ResourcePlanSession live, string scenario)
            {
                _inner = inner;
                _live = live;
                _scenario = scenario;
            }

            public IResourceOperation<ResourcePreloadResult> PreloadAsync(ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default)
            {
                ResourceHandle<object> first = _live.LoadedResourceHandles[0];
                ResourceHandle<object> second = _live.LoadedResourceHandles[1];
                if (_scenario == "forged")
                    first = (ResourceHandle<object>)Activator.CreateInstance(typeof(ResourceHandle<object>),
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                        new object[] { first.Key, first.Entry, first.ProviderId, first.Value }, null);
                else if (_scenario == "missing")
                    first = null;
                else
                    second = first;
                // Reflection supplies adversarial data at an otherwise internal construction boundary.
                var group = (ResourceGroupHandle)Activator.CreateInstance(typeof(ResourceGroupHandle),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                    new object[] { plan.GroupId, new[] { first, second } }, null);
                return new ImmediateResourceOperation<ResourcePreloadResult>(ResourceLoadResult<ResourcePreloadResult>.Loaded(
                    new ResourcePreloadResult(plan.GroupId, group, plan.ExplicitKeys, null)));
            }

            public void ReleaseGroup(ResourceGroupHandle handle) => _inner.ReleaseGroup(handle);
        }

        private sealed class PartialSuccessPreloadService : IResourcePreloadService
        {
            private readonly ResourcePreloadService _inner;

            public PartialSuccessPreloadService(IResourceManager resourceManager)
            {
                _inner = new ResourcePreloadService(resourceManager);
            }

            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default)
            {
                var partialPlan = new ResourcePreloadPlan(
                    plan.GroupId,
                    new[] { plan.ExplicitKeys[0] },
                    labels: null,
                    failFast: plan.FailFast,
                    maxConcurrentLoads: plan.MaxConcurrentLoads);
                return _inner.PreloadAsync(partialPlan, cancellationToken);
            }

            public void ReleaseGroup(ResourceGroupHandle handle)
            {
                _inner.ReleaseGroup(handle);
            }
        }

        private sealed class DeferredSuccessPreloadService : IResourcePreloadService
        {
            private readonly ResourcePreloadService _inner;
            private DeferredSuccessOperation _operation;

            public DeferredSuccessPreloadService(IResourceManager resourceManager)
            {
                _inner = new ResourcePreloadService(resourceManager);
            }

            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default)
            {
                _operation = new DeferredSuccessOperation(_inner.PreloadAsync(plan, cancellationToken).Result);
                return _operation;
            }

            public void ReleaseGroup(ResourceGroupHandle handle)
            {
                _inner.ReleaseGroup(handle);
            }

            public void Complete()
            {
                _operation.Complete();
            }
        }

        private sealed class DeferredSuccessOperation : IResourceOperation<ResourcePreloadResult>
        {
            private readonly ResourceLoadResult<ResourcePreloadResult> _result;

            public DeferredSuccessOperation(ResourceLoadResult<ResourcePreloadResult> result)
            {
                _result = result;
            }

            public bool IsDone { get; private set; }
            public bool IsCancelled => false;
            public float Progress => IsDone ? 1f : 0.5f;
            public ResourceLoadResult<ResourcePreloadResult> Result => _result;
            public void Cancel() { }
            public void Complete() { IsDone = true; }
        }

        private sealed class PendingPreloadOperation : IResourceOperation<ResourcePreloadResult>
        {
            public bool IsDone => false;
            public bool IsCancelled => false;
            public float Progress => 0.5f;
            public ResourceLoadResult<ResourcePreloadResult> Result => default;
            public void Cancel()
            {
            }
        }
    }
}
