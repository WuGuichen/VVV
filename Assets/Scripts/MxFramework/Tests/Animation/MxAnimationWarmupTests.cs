using System.Linq;
using MxFramework.Animation;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationWarmupTests
    {
        [Test]
        public void Warmup_LoadsDefinitionKeysAndLabels_ThenReleasesGroup()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceKey fallback = ClipKey("demo.animation.fallback");
            ResourceKey attack = ClipKey("demo.animation.attack");
            ResourceKey mask = MaskKey("demo.animation.mask.upper_body");
            ResourceKey labelClip = ClipKey("demo.animation.label.extra");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", "Idle")
                .Register("clips/fallback", "Fallback")
                .Register("clips/attack", "Attack")
                .Register("masks/upper", "UpperMask")
                .Register("clips/label", "LabelClip");
            ResourceCatalog catalog = Catalog(
                Entry(idle, "clips/idle", hash: "hash-idle"),
                Entry(fallback, "clips/fallback", hash: "hash-fallback"),
                Entry(attack, "clips/attack", hash: "hash-attack"),
                Entry(mask, "masks/upper"),
                Entry(labelClip, "clips/label", hash: "hash-label", labels: new[] { "warmup.combat" }));
            ResourceManager manager = CreateManager(provider, catalog);
            var preload = new ResourcePreloadService(manager);
            var service = new MxAnimationWarmupService(preload);
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 3, catalogHash: "catalog-hash");
            MxAnimationSetDefinition definition = CreateDefinition(
                idle,
                fallback,
                attack,
                mask,
                warmup: new MxAnimationWarmupDefinition("combat", labels: new[] { "warmup.combat" }));

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, registry, catalog));

            Assert.IsTrue(result.Success, Describe(result));
            Assert.AreEqual("combat", result.GroupId);
            Assert.AreEqual(4, result.RequiredKeys.Count);
            Assert.AreEqual(5, result.PreloadResult.RequestedCount);
            Assert.AreEqual(5, result.PreloadResult.LoadedCount);
            Assert.AreEqual(5, manager.CreateDebugSnapshot().LoadedCount);

            service.Release(result);

            Assert.IsTrue(result.Handle.IsReleased);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Warmup_IncludesBlend2DPointClips()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceKey fallback = ClipKey("demo.animation.fallback");
            ResourceKey left = ClipKey("demo.animation.left");
            ResourceKey right = ClipKey("demo.animation.right");
            ResourceKey forward = ClipKey("demo.animation.forward");
            ResourceKey backward = ClipKey("demo.animation.backward");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", "Idle")
                .Register("clips/fallback", "Fallback")
                .Register("clips/left", "Left")
                .Register("clips/right", "Right")
                .Register("clips/forward", "Forward")
                .Register("clips/backward", "Backward");
            ResourceCatalog catalog = Catalog(
                Entry(idle, "clips/idle", hash: "hash-idle"),
                Entry(fallback, "clips/fallback", hash: "hash-fallback"),
                Entry(left, "clips/left", hash: "hash-left"),
                Entry(right, "clips/right", hash: "hash-right"),
                Entry(forward, "clips/forward", hash: "hash-forward"),
                Entry(backward, "clips/backward", hash: "hash-backward"));
            ResourceManager manager = CreateManager(provider, catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash");
            var definition = new MxAnimationSetDefinition(
                "demo.actor",
                1,
                idle,
                fallback,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        "move.x",
                        "move.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(-1000, 0, left),
                            new MxAnimationBlend2DPoint(1000, 0, right),
                            new MxAnimationBlend2DPoint(0, 1000, forward),
                            new MxAnimationBlend2DPoint(0, -1000, backward)
                        })
                });

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, registry, catalog));

            Assert.IsTrue(result.Success, Describe(result));
            Assert.AreEqual(6, result.RequiredKeys.Count);
            Assert.AreEqual(6, result.PreloadResult.RequestedCount);
            Assert.AreEqual(6, result.PreloadResult.LoadedCount);
            Assert.AreEqual(6, manager.CreateDebugSnapshot().LoadedCount);

            service.Release(result);

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Warmup_WhenSyncVersionMismatch_ReportsDiagnosticsAndSkipsPreload()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(Entry(idle, "clips/idle", hash: "hash-idle"));
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("clips/idle", "Idle"), catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 2, catalogHash: "catalog-v2");
            var syncState = new MxAnimationPresentationSyncState(
                actorId: "actor.1",
                animationSetId: definition.SetId,
                animationSetVersion: definition.Version,
                animationSetHash: "stale-set-hash",
                resourceCatalogHash: "catalog-v1",
                clipRegistryVersion: 1,
                actionId: 0,
                actionKey: string.Empty,
                actionInstanceId: 10,
                startedAtCombatFrame: 20,
                localFrame: 5,
                status: MxAnimationPresentationSyncStatus.Running);

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, registry, catalog, syncState));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.PreloadResult);
            AssertIssue(result, MxAnimationWarmupIssueCodes.AnimationSetHashMismatch, default, "animationSetHash");
            AssertIssue(result, MxAnimationWarmupIssueCodes.ResourceCatalogHashMismatch, default, "resourceCatalogHash");
            AssertIssue(result, MxAnimationWarmupIssueCodes.ClipRegistryVersionMismatch, default, "clipRegistryVersion");
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Warmup_WhenCatalogHasWrongClipType_ReportsConcreteKey()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(new ResourceCatalogEntry(idle.Id, ResourceTypeIds.String, "memory", "clips/idle"));
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("clips/idle", "Idle"), catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, null, catalog));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.PreloadResult);
            MxAnimationWarmupIssue issue = AssertIssue(result, MxAnimationWarmupIssueCodes.CatalogValidationFailed, idle, "ClipCatalogTypeMismatch");
            Assert.That(issue.Message, Does.Contain(ResourceTypeIds.String));
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Warmup_WhenPreloadPartiallyFails_ReportsFailedResource()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceKey fallback = ClipKey("demo.animation.fallback");
            ResourceKey attack = ClipKey("demo.animation.attack");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", "Idle")
                .Register("clips/fallback", "Fallback");
            ResourceCatalog catalog = Catalog(
                Entry(idle, "clips/idle", hash: "hash-idle"),
                Entry(fallback, "clips/fallback", hash: "hash-fallback"),
                Entry(attack, "clips/missing", hash: "hash-attack"));
            ResourceManager manager = CreateManager(provider, catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(idle, fallback, attack);
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash");

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, registry, catalog));

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.PreloadResult);
            Assert.AreEqual(3, result.PreloadResult.RequestedCount);
            Assert.AreEqual(2, result.PreloadResult.LoadedCount);
            MxAnimationWarmupIssue issue = AssertIssue(result, MxAnimationWarmupIssueCodes.PreloadResourceFailed, attack, "resource");
            Assert.AreEqual(ResourceErrorCode.NotFound, issue.ResourceError.Code);

            service.Release(result);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void Warmup_ReleaseGroup_PreservesOtherConsumerRefCount()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(Entry(idle, "clips/idle", hash: "hash-idle"));
            var provider = new MemoryResourceProvider().Register("clips/idle", "Idle");
            ResourceManager manager = CreateManager(provider, catalog);
            ResourceHandle<object> externalHandle = manager.Load<object>(idle).Value;
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash");

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, registry, catalog));

            Assert.IsTrue(result.Success, Describe(result));
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(2, manager.CreateDebugSnapshot().TotalRefCount);

            service.Release(result);

            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
            Assert.AreEqual(0, provider.ReleaseCount);

            manager.Release(externalHandle);

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        [Test]
        public void Warmup_WhenExpectedClipEntryHashDiffers_ReportsSpecificClip()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(Entry(idle, "clips/idle", hash: "hash-v2"));
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("clips/idle", "Idle"), catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);
            MxAnimationClipRegistry localRegistry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-v2");
            var expectedRegistry = new MxAnimationClipRegistry(
                version: 1,
                catalogId: "demo",
                catalogHash: "catalog-v2",
                entries: new[] { new MxAnimationClipRegistryEntry(idle, "hash-v1") });

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(
                definition,
                localRegistry,
                catalog,
                expectedClipRegistry: expectedRegistry));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.PreloadResult);
            MxAnimationWarmupIssue issue = AssertIssue(result, MxAnimationWarmupIssueCodes.ClipRegistryEntryHashMismatch, idle, "catalogEntryHash");
            Assert.AreEqual("hash-v1", issue.Expected);
            Assert.AreEqual("hash-v2", issue.Actual);
        }

        private static MxAnimationSetDefinition CreateDefinition(
            ResourceKey defaultClip,
            ResourceKey fallbackClip,
            ResourceKey actionClip = default,
            ResourceKey mask = default,
            MxAnimationWarmupDefinition warmup = null)
        {
            MxAnimationActionBinding[] actions = actionClip.IsValid
                ? new[]
                {
                    new MxAnimationActionBinding(
                        "attack",
                        "action:attack",
                        actionClip,
                        new MxAnimationLayerId("upper_body"))
                }
                : null;
            MxAnimationLayerDefinition[] layers = mask.IsValid
                ? new[] { new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), avatarMaskKey: mask) }
                : null;

            return new MxAnimationSetDefinition(
                "demo.actor",
                1,
                defaultClip,
                fallbackClip,
                actions,
                layers: layers,
                warmup: warmup);
        }

        private static ResourceCatalog Catalog(params ResourceCatalogEntry[] entries)
        {
            return new ResourceCatalog("demo", string.Empty, entries);
        }

        private static ResourceCatalogEntry Entry(ResourceKey key, string address, string hash = "", string[] labels = null)
        {
            return new ResourceCatalogEntry(
                key.Id,
                key.TypeId,
                "memory",
                address,
                variant: key.Variant,
                packageId: key.PackageId,
                hash: hash,
                labels: labels);
        }

        private static ResourceManager CreateManager(MemoryResourceProvider provider, ResourceCatalog catalog)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(catalog);
            return manager;
        }

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }

        private static ResourceKey MaskKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AvatarMask);
        }

        private static MxAnimationWarmupIssue AssertIssue(
            MxAnimationWarmupResult result,
            string code,
            ResourceKey key,
            string field)
        {
            MxAnimationWarmupIssue issue = result.Issues.FirstOrDefault(candidate =>
                candidate.Code == code
                && candidate.Field == field
                && (!key.IsValid || candidate.Key == key));
            Assert.IsNotNull(issue, Describe(result));
            return issue;
        }

        private static string Describe(MxAnimationWarmupResult result)
        {
            return string.Join("\n", result.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }
    }
}
