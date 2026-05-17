using System.IO;
using System.Linq;
using System.Threading;
using MxFramework.Animation;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationWarmupTests
    {
        private const string PackageBundleName = "mx-animation-package";
        private const string PackageBundleRoot = "Temp/MxAnimationPackageLoadingBundleTests";
        private const string PackageClipAssetPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_idle.anim";
        private const string PackageMaskAssetPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Masks/SkeletonUpperBody.mask";
        private const string PackageBakeAssetPath = "Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt";
        private const string PackageProfileAssetPath = "Assets/TestAssets/MxFramework/ResourcesDemo/resource_shared_text.txt";

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
        public void Warmup_WhenPreloadOperationIsPending_ReportsPendingWithoutReadingResult()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(Entry(idle, "clips/idle", hash: "hash-idle"));
            var preload = new DeferredPreloadService();
            var service = new MxAnimationWarmupService(preload);
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash");

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(definition, registry, catalog));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.PreloadResult);
            Assert.AreEqual(0, preload.Operation.ResultReadCount);
            Assert.IsTrue(preload.Operation.IsCancelled);
            AssertIssue(result, MxAnimationWarmupIssueCodes.PreloadOperationPending, default, "preload");
        }

        [Test]
        public void WarmupAsync_WhenPreloadCompletesLater_ReturnsResultAfterPolling()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(Entry(idle, "clips/idle", hash: "hash-idle"));
            var preload = new DeferredPreloadService();
            var service = new MxAnimationWarmupService(preload);
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash");

            IResourceOperation<MxAnimationWarmupResult> operation = service.WarmupAsync(new MxAnimationWarmupRequest(definition, registry, catalog));
            preload.Operation.SetProgress(0.5f);

            Assert.IsFalse(operation.IsDone);
            Assert.AreEqual(0.5f, operation.Progress, 0.0001f);
            Assert.AreEqual("mxanimation.demo.actor.warmup", preload.Plan.GroupId);

            var preloadResult = new ResourcePreloadResult(
                preload.Plan.GroupId,
                null,
                preload.Plan.ExplicitKeys,
                null);
            preload.Operation.Complete(ResourceLoadResult<ResourcePreloadResult>.Loaded(preloadResult));

            Assert.IsTrue(operation.IsDone);
            ResourceLoadResult<MxAnimationWarmupResult> result = operation.Result;
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Value.Success, Describe(result.Value));
            Assert.AreSame(preloadResult, result.Value.PreloadResult);
            Assert.AreEqual(1, result.Value.RequiredKeys.Count);
            Assert.AreEqual(0, preload.Operation.CancelCount);
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

        [Test]
        public void WarmupRequest_PreservesPositionalSkipPreloadCompatibility()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            MxAnimationSetDefinition definition = CreateDefinition(idle, idle);

            var request = new MxAnimationWarmupRequest(
                definition,
                null,
                null,
                null,
                null,
                false);

            Assert.IsFalse(request.SkipPreloadWhenInvalid);
            Assert.IsNull(request.CompatibilityProfile);
        }

        [Test]
        public void Warmup_WhenCompatibilityProfileMismatch_ReportsDiagnosticsAndSkipsPreload()
        {
            ResourceKey idle = ClipKey("demo.animation.idle");
            ResourceCatalog catalog = Catalog(Entry(idle, "clips/idle", hash: "hash-idle"));
            ResourceManager manager = CreateManager(new MemoryResourceProvider().Register("clips/idle", "Idle"), catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            var expectation = new MxAnimationCompatibilityExpectation(
                "humanoid",
                "sha256:skeleton",
                new[] { "Hips/Head" },
                new[] { "Hips/RightHandSocket" },
                new[]
                {
                    new MxAnimationClipCompatibilityExpectation(idle, new[] { "Hips/Spine" })
                });
            var definition = new MxAnimationSetDefinition(
                "demo.actor",
                1,
                idle,
                idle,
                compatibilityExpectation: expectation);
            var profile = new MxAnimationCompatibilityProfile(
                new MxAnimationSkeletonCompatibilityProfile(
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips" },
                    new[] { "Hips/LeftHandSocket" }),
                new[]
                {
                    new MxAnimationClipCompatibilityProfile(idle, "humanoid", "sha256:skeleton", new[] { "Hips" })
                });

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(
                definition,
                MxAnimationClipRegistryBuilder.FromCatalog(catalog),
                catalog,
                compatibilityProfile: profile));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.PreloadResult);
            AssertIssue(result, MxAnimationWarmupIssueCodes.CompatibilityValidationFailed, default, MxAnimationCompatibilityIssueCodes.BonePathMissing);
            AssertIssue(result, MxAnimationWarmupIssueCodes.CompatibilityValidationFailed, default, MxAnimationCompatibilityIssueCodes.SocketPathMissing);
            AssertIssue(result, MxAnimationWarmupIssueCodes.CompatibilityValidationFailed, idle, MxAnimationCompatibilityIssueCodes.ClipBindingPathMissing);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void PackageValidator_AllowsSameMappingAcrossMemoryAndBundleProviders()
        {
            ResourceKey clip = ClipKey("demo.animation.attack");
            ResourceKey mask = MaskKey("demo.animation.mask.upper_body");
            var expectation = new MxAnimationPackageExpectation(
                "mx.anim.demo",
                version: 4,
                catalogId: "mx.anim.demo.catalog",
                catalogHash: "catalog-hash",
                acceptedProviderIds: new[] { "memory", "assetBundle", "remoteBundle" },
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(clip, "clip-hash"),
                    new MxAnimationPackageResourceExpectation(mask, "mask-hash")
                });
            ResourceCatalog memoryCatalog = Catalog(
                "mx.anim.demo.catalog",
                "mx.anim.demo",
                Entry(clip, "clips/attack", hash: "clip-hash"),
                Entry(mask, "masks/upper", hash: "mask-hash"));
            ResourceCatalog bundleCatalog = Catalog(
                "mx.anim.demo.catalog",
                "mx.anim.demo",
                Entry(clip, "mx-anim-demo|Assets/Animation/attack.anim", "assetBundle", hash: "clip-hash"),
                Entry(mask, "mx-anim-demo|Assets/Animation/upper.mask", "assetBundle", hash: "mask-hash"));

            MxAnimationPackageValidationReport memoryReport = MxAnimationPackageCatalogValidator.Validate(
                new MxAnimationPackageCatalog(memoryCatalog, version: 4, catalogHash: "catalog-hash"),
                expectation);
            MxAnimationPackageValidationReport bundleReport = MxAnimationPackageCatalogValidator.Validate(
                new MxAnimationPackageCatalog(bundleCatalog, version: 4, catalogHash: "catalog-hash"),
                expectation);

            Assert.IsTrue(memoryReport.Success, Describe(memoryReport));
            Assert.IsTrue(bundleReport.Success, Describe(bundleReport));
        }

        [Test]
        public void Warmup_UsesSameMappingWithMemoryAndAssetBundleProviders()
        {
            ResourceKey clip = ClipKey("demo.animation.attack");
            ResourceKey mask = MaskKey("demo.animation.mask.upper_body");
            ResourceKey bake = BakeKey("demo.animation.bake.attack");
            ResourceKey profile = ProfileKey("demo.animation.profile.humanoid");
            MxAnimationSetDefinition definition = CreateDefinition(clip, clip, mask: mask);
            MxAnimationPackageExpectation expectation = CreatePackageExpectation(
                clip,
                mask,
                bake,
                profile,
                "memory",
                AssetBundleProvider.Id);
            ResourceCatalog memoryCatalog = Catalog(
                "mx.anim.demo.catalog",
                "mx.anim.demo",
                Entry(clip, "memory/clip", hash: "clip-hash"),
                Entry(mask, "memory/mask", hash: "mask-hash"),
                Entry(bake, "memory/bake", hash: "bake-hash"),
                Entry(profile, "memory/profile", hash: "profile-hash"));
            ResourceManager memoryManager = CreateManager(
                new MemoryResourceProvider()
                    .Register("memory/clip", "Clip")
                    .Register("memory/mask", "Mask")
                    .Register("memory/bake", "Bake")
                    .Register("memory/profile", "Profile"),
                memoryCatalog);

            BuildAnimationPackageBundle();
            try
            {
                var bundleProvider = new AssetBundleProvider(PackageBundleRoot);
                ResourceCatalog bundleCatalog = Catalog(
                    "mx.anim.demo.catalog",
                    "mx.anim.demo",
                    Entry(clip, PackageBundleName + "|" + PackageClipAssetPath, AssetBundleProvider.Id, hash: "clip-hash"),
                    Entry(mask, PackageBundleName + "|" + PackageMaskAssetPath, AssetBundleProvider.Id, hash: "mask-hash"),
                    Entry(bake, PackageBundleName + "|" + PackageBakeAssetPath, AssetBundleProvider.Id, hash: "bake-hash"),
                    Entry(profile, PackageBundleName + "|" + PackageProfileAssetPath, AssetBundleProvider.Id, hash: "profile-hash"));
                ResourceManager bundleManager = CreateManager(bundleProvider, bundleCatalog);

                MxAnimationWarmupResult memoryResult = WarmupPackage(memoryManager, memoryCatalog, definition, expectation);
                MxAnimationWarmupResult bundleResult = WarmupPackage(bundleManager, bundleCatalog, definition, expectation);

                Assert.IsTrue(memoryResult.Success, Describe(memoryResult));
                Assert.IsTrue(bundleResult.Success, Describe(bundleResult));
                Assert.AreEqual(4, memoryResult.PreloadResult.LoadedCount);
                Assert.AreEqual(4, bundleResult.PreloadResult.LoadedCount);
                Assert.AreEqual(4, memoryManager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(4, bundleManager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, bundleProvider.LoadedBundleCount);
                Assert.AreEqual(4, bundleProvider.GetBundleRefCount(PackageBundleName));

                new MxAnimationWarmupService(new ResourcePreloadService(memoryManager)).Release(memoryResult);
                new MxAnimationWarmupService(new ResourcePreloadService(bundleManager)).Release(bundleResult);

                Assert.AreEqual(0, memoryManager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(0, bundleManager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(0, bundleProvider.LoadedBundleCount);
            }
            finally
            {
                AssetBundle.UnloadAllAssetBundles(true);
                DeleteDirectory(PackageBundleRoot);
            }
        }

        [Test]
        public void Warmup_WithPackageExpectation_PreloadsClipMaskBakeAndCompatibilityProfile()
        {
            ResourceKey clip = ClipKey("demo.animation.attack");
            ResourceKey mask = MaskKey("demo.animation.mask.upper_body");
            ResourceKey bake = BakeKey("demo.animation.bake.attack");
            ResourceKey profile = ProfileKey("demo.animation.profile.humanoid");
            var provider = new MemoryResourceProvider()
                .Register("clips/attack", "Attack")
                .Register("masks/upper", "UpperMask")
                .Register("bake/attack", "BakeArtifact")
                .Register("profiles/humanoid", "CompatibilityProfile");
            ResourceCatalog catalog = Catalog(
                "mx.anim.demo.catalog",
                "mx.anim.demo",
                Entry(clip, "clips/attack", hash: "clip-hash"),
                Entry(mask, "masks/upper", hash: "mask-hash"),
                Entry(bake, "bake/attack", hash: "bake-hash"),
                Entry(profile, "profiles/humanoid", hash: "profile-hash"));
            ResourceManager manager = CreateManager(provider, catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(clip, clip, mask: mask);
            var expectation = new MxAnimationPackageExpectation(
                "mx.anim.demo",
                version: 2,
                catalogId: "mx.anim.demo.catalog",
                catalogHash: "catalog-hash",
                acceptedProviderIds: new[] { "memory" },
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(clip, "clip-hash"),
                    new MxAnimationPackageResourceExpectation(mask, "mask-hash"),
                    new MxAnimationPackageResourceExpectation(bake, "bake-hash"),
                    new MxAnimationPackageResourceExpectation(profile, "profile-hash")
                });

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(
                definition,
                MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash"),
                catalog,
                null,
                null,
                true,
                null,
                expectation,
                new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "catalog-hash")));

            Assert.IsTrue(result.Success, Describe(result));
            Assert.AreEqual(4, result.RequiredKeys.Count);
            Assert.AreEqual(4, result.PreloadResult.RequestedCount);
            Assert.AreEqual(4, result.PreloadResult.LoadedCount);
            Assert.AreEqual(4, manager.CreateDebugSnapshot().LoadedCount);

            service.Release(result);

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void PackageValidator_WhenVersionCatalogHashAndResourceHashMismatch_ReportsDiagnostics()
        {
            ResourceKey clip = ClipKey("demo.animation.attack");
            ResourceCatalog catalog = Catalog(
                "mx.anim.demo.catalog",
                "mx.anim.demo",
                Entry(clip, "clips/attack", hash: "clip-hash-v2"));
            var expectation = new MxAnimationPackageExpectation(
                "mx.anim.demo",
                version: 1,
                catalogId: "mx.anim.demo.catalog",
                catalogHash: "catalog-hash-v1",
                acceptedProviderIds: new[] { "memory" },
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(clip, "clip-hash-v1")
                });

            MxAnimationPackageValidationReport report = MxAnimationPackageCatalogValidator.Validate(
                new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "catalog-hash-v2"),
                expectation);

            Assert.IsFalse(report.Success);
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.PackageVersionMismatch, default, "version");
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.PackageCatalogHashMismatch, default, "catalogHash");
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.PackageResourceHashMismatch, clip, "catalogEntryHash");
        }

        [Test]
        public void PackageValidator_WhenRequiredResourcesMissing_ReportsSpecificDiagnostics()
        {
            ResourceKey clip = ClipKey("demo.animation.attack");
            ResourceKey mask = MaskKey("demo.animation.mask.upper_body");
            ResourceKey bake = BakeKey("demo.animation.bake.attack");
            ResourceKey profile = ProfileKey("demo.animation.profile.humanoid");
            ResourceCatalog catalog = new ResourceCatalog("demo", string.Empty, new ResourceCatalogEntry[0]);
            var expectation = new MxAnimationPackageExpectation(
                string.Empty,
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(clip, "clip-hash"),
                    new MxAnimationPackageResourceExpectation(mask, "mask-hash"),
                    new MxAnimationPackageResourceExpectation(bake, "bake-hash"),
                    new MxAnimationPackageResourceExpectation(profile, "profile-hash")
                });

            MxAnimationPackageValidationReport report = MxAnimationPackageCatalogValidator.Validate(
                new MxAnimationPackageCatalog(catalog),
                expectation);

            Assert.IsFalse(report.Success);
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.AnimationClipMissing, clip, "catalogEntry");
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.AvatarMaskMissing, mask, "catalogEntry");
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.BakeArtifactMissing, bake, "catalogEntry");
            AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.CompatibilityProfileMissing, profile, "catalogEntry");
        }

        [Test]
        public void Warmup_WhenPackageResourceLoadFails_ReportsPreloadResourceFailed()
        {
            ResourceKey clip = ClipKey("demo.animation.attack");
            ResourceKey bake = BakeKey("demo.animation.bake.attack");
            var provider = new MemoryResourceProvider().Register("clips/attack", "Attack");
            ResourceCatalog catalog = Catalog(
                Entry(clip, "clips/attack", hash: "clip-hash"),
                Entry(bake, "bake/missing", hash: "bake-hash"));
            ResourceManager manager = CreateManager(provider, catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationSetDefinition definition = CreateDefinition(clip, clip);
            var expectation = new MxAnimationPackageExpectation(
                string.Empty,
                acceptedProviderIds: new[] { "memory" },
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(bake, "bake-hash")
                });

            MxAnimationWarmupResult result = service.Warmup(new MxAnimationWarmupRequest(
                definition,
                MxAnimationClipRegistryBuilder.FromCatalog(catalog),
                catalog,
                null,
                null,
                true,
                null,
                expectation,
                new MxAnimationPackageCatalog(catalog)));

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.PreloadResult);
            MxAnimationWarmupIssue issue = AssertIssue(result, MxAnimationWarmupIssueCodes.PreloadResourceFailed, bake, "resource");
            Assert.AreEqual(ResourceErrorCode.NotFound, issue.ResourceError.Code);

            service.Release(result);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
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

        private static ResourceCatalog Catalog(string catalogId, string packageId, params ResourceCatalogEntry[] entries)
        {
            return new ResourceCatalog(catalogId, packageId, entries);
        }

        private static ResourceCatalogEntry Entry(
            ResourceKey key,
            string address,
            string providerId = "memory",
            string hash = "",
            string[] labels = null)
        {
            return new ResourceCatalogEntry(
                key.Id,
                key.TypeId,
                providerId,
                address,
                variant: key.Variant,
                packageId: key.PackageId,
                hash: hash,
                labels: labels);
        }

        private static ResourceManager CreateManager(IResourceProvider provider, ResourceCatalog catalog)
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

        private static ResourceKey BakeKey(string id)
        {
            return new ResourceKey(id, MxAnimationResourceTypeIds.BakeArtifact);
        }

        private static ResourceKey ProfileKey(string id)
        {
            return new ResourceKey(id, MxAnimationResourceTypeIds.CompatibilityProfile);
        }

        private static MxAnimationPackageExpectation CreatePackageExpectation(
            ResourceKey clip,
            ResourceKey mask,
            ResourceKey bake,
            ResourceKey profile,
            params string[] acceptedProviderIds)
        {
            return new MxAnimationPackageExpectation(
                "mx.anim.demo",
                version: 2,
                catalogId: "mx.anim.demo.catalog",
                catalogHash: "catalog-hash",
                acceptedProviderIds: acceptedProviderIds,
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(clip, "clip-hash"),
                    new MxAnimationPackageResourceExpectation(mask, "mask-hash"),
                    new MxAnimationPackageResourceExpectation(bake, "bake-hash"),
                    new MxAnimationPackageResourceExpectation(profile, "profile-hash")
                });
        }

        private static MxAnimationWarmupResult WarmupPackage(
            ResourceManager manager,
            ResourceCatalog catalog,
            MxAnimationSetDefinition definition,
            MxAnimationPackageExpectation expectation)
        {
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            return service.Warmup(new MxAnimationWarmupRequest(
                definition,
                MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "catalog-hash"),
                catalog,
                null,
                null,
                true,
                null,
                expectation,
                new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "catalog-hash")));
        }

        private static void BuildAnimationPackageBundle()
        {
            DeleteDirectory(PackageBundleRoot);
            Directory.CreateDirectory(PackageBundleRoot);
            BuildPipeline.BuildAssetBundles(
                PackageBundleRoot,
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = PackageBundleName,
                        assetNames = new[]
                        {
                            PackageClipAssetPath,
                            PackageMaskAssetPath,
                            PackageBakeAssetPath,
                            PackageProfileAssetPath
                        }
                    }
                },
                BuildAssetBundleOptions.UncompressedAssetBundle,
                EditorUserBuildSettings.activeBuildTarget);
            Assert.IsTrue(File.Exists(Path.Combine(PackageBundleRoot, PackageBundleName)));
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
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

        private static MxAnimationPackageValidationIssue AssertPackageIssue(
            MxAnimationPackageValidationReport report,
            string code,
            ResourceKey key,
            string field)
        {
            MxAnimationPackageValidationIssue issue = report.Issues.FirstOrDefault(candidate =>
                candidate.Code == code
                && candidate.Field == field
                && (!key.IsValid || candidate.Key == key));
            Assert.IsNotNull(issue, Describe(report));
            return issue;
        }

        private static string Describe(MxAnimationWarmupResult result)
        {
            return string.Join("\n", result.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }

        private static string Describe(MxAnimationPackageValidationReport report)
        {
            return string.Join("\n", report.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }

        private sealed class DeferredPreloadService : IResourcePreloadService
        {
            public DeferredPreloadOperation<ResourcePreloadResult> Operation { get; } = new DeferredPreloadOperation<ResourcePreloadResult>();
            public ResourcePreloadPlan Plan { get; private set; }

            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                CancellationToken cancellationToken = default)
            {
                Plan = plan;
                return Operation;
            }

            public void ReleaseGroup(ResourceGroupHandle handle)
            {
            }
        }

        private sealed class DeferredPreloadOperation<T> : IResourceOperation<T>
        {
            private ResourceLoadResult<T> _result = ResourceLoadResult<T>.Failed(new ResourceError(
                ResourceErrorCode.ProviderFailed,
                default,
                string.Empty,
                "Deferred preload operation has not completed."));
            private float _progress;

            public bool IsDone { get; private set; }
            public bool IsCancelled { get; private set; }
            public int ResultReadCount { get; private set; }
            public int CancelCount { get; private set; }
            public float Progress => _progress;

            public ResourceLoadResult<T> Result
            {
                get
                {
                    ResultReadCount++;
                    return _result;
                }
            }

            public void SetProgress(float progress)
            {
                _progress = progress;
            }

            public void Complete(ResourceLoadResult<T> result)
            {
                _result = result;
                IsDone = true;
                _progress = 1f;
            }

            public void Cancel()
            {
                if (IsCancelled)
                    return;

                CancelCount++;
                IsCancelled = true;
                IsDone = true;
                _progress = 1f;
                _result = ResourceLoadResult<T>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    default,
                    string.Empty,
                    "Deferred preload operation was cancelled."));
            }
        }
    }
}
