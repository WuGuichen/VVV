using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Animation;
using MxFramework.Editor.Animation;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationPackageBuilderTests
    {
        [Test]
        public void Build_FromRegistryBakeAndCompatibilityProfile_CreatesValidatedBundlePackage()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                MxAnimationPackageBuildResult result = BuildPackage(
                    asset,
                    skeletonRoot,
                    MxAnimationPackageProviderSampleKind.LocalAssetBundle);

                Assert.IsTrue(result.Success, result.ReportText);
                Assert.IsTrue(result.ExportResult.Success, MxAnimationClipRegistryExporter.CreateReportText(result.ExportResult));
                Assert.IsFalse(result.CatalogValidation.HasErrors);
                Assert.IsTrue(result.PackageValidation.Success, Describe(result.PackageValidation));
                Assert.AreEqual("demo.package", result.Expectation.PackageId);
                Assert.AreEqual("demo.package.catalog", result.Expectation.CatalogId);
                Assert.AreEqual(result.PackageCatalog.CatalogHash, result.Expectation.CatalogHash);
                Assert.That(result.Expectation.AcceptedProviderIds, Contains.Item(AssetBundleProvider.Id));
                Assert.That(result.Expectation.Resources.Select(resource => resource.Kind), Contains.Item(MxAnimationPackageResourceKind.AnimationClip));
                Assert.That(result.Expectation.Resources.Select(resource => resource.Kind), Contains.Item(MxAnimationPackageResourceKind.AvatarMask));
                Assert.That(result.Expectation.Resources.Select(resource => resource.Kind), Contains.Item(MxAnimationPackageResourceKind.BakeArtifact));
                Assert.That(result.Expectation.Resources.Select(resource => resource.Kind), Contains.Item(MxAnimationPackageResourceKind.CompatibilityProfile));
                Assert.IsTrue(result.CatalogSnapshot.Entries.All(entry => entry.ProviderId == AssetBundleProvider.Id));
                Assert.IsTrue(result.CatalogSnapshot.Entries.All(entry => entry.Address.StartsWith("demo-animation-package|", StringComparison.Ordinal)));
                Assert.That(result.ReportText, Does.Contain("sampleProvider: " + AssetBundleProvider.Id));
                Assert.That(result.ReportText, Does.Contain("warmup: pass Expectation + PackageCatalog"));
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        [Test]
        public void Build_WithMemoryProvider_WarmupConsumesGeneratedPackageExpectation()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                MxAnimationPackageBuildResult result = BuildPackage(
                    asset,
                    skeletonRoot,
                    MxAnimationPackageProviderSampleKind.Memory);
                var provider = new MemoryResourceProvider();
                for (int i = 0; i < result.CatalogSnapshot.Entries.Count; i++)
                    provider.Register(result.CatalogSnapshot.Entries[i].Address, result.CatalogSnapshot.Entries[i].Id);

                var manager = new ResourceManager();
                manager.RegisterProvider(provider);
                manager.AddCatalog(result.CatalogSnapshot);
                var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));

                MxAnimationWarmupResult warmup = service.Warmup(new MxAnimationWarmupRequest(
                    result.ExportResult.Definition,
                    MxAnimationClipRegistryBuilder.FromCatalog(
                        result.CatalogSnapshot,
                        version: result.Expectation.Version,
                        catalogHash: result.PackageCatalog.CatalogHash),
                    result.CatalogSnapshot,
                    null,
                    null,
                    true,
                    null,
                    result.Expectation,
                    result.PackageCatalog));

                Assert.IsTrue(warmup.Success, Describe(warmup));
                Assert.AreEqual(result.Expectation.Resources.Count, warmup.RequiredKeys.Count);
                Assert.AreEqual(result.Expectation.Resources.Count, warmup.PreloadResult.LoadedCount);
                service.Release(warmup);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        [Test]
        public void Build_WithRemoteBundleProvider_WritesRemoteProviderData()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                MxAnimationPackageBuildResult result = BuildPackage(
                    asset,
                    skeletonRoot,
                    MxAnimationPackageProviderSampleKind.RemoteBundle,
                    remoteBundleUrl: "file:///tmp/demo-animation-package",
                    remoteCacheKey: "demo-animation-package-cache",
                    remoteBundleHash: "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

                Assert.IsTrue(result.Success, result.ReportText);
                for (int i = 0; i < result.CatalogSnapshot.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = result.CatalogSnapshot.Entries[i];
                    Assert.AreEqual(RemoteBundleProvider.Id, entry.ProviderId);
                    Assert.AreEqual("file:///tmp/demo-animation-package", entry.ProviderData["url"]);
                    Assert.AreEqual("demo-animation-package", entry.ProviderData["bundleName"]);
                    Assert.AreEqual("demo-animation-package-cache", entry.ProviderData["cacheKey"]);
                    Assert.AreEqual("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", entry.ProviderData["hash.demo-animation-package"]);
                    Assert.AreNotEqual(entry.Hash, entry.ProviderData["hash.demo-animation-package"]);
                }
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        [Test]
        public void Build_WhenBundleProviderIsNotAccepted_ReportsProviderDiagnostics()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                MxAnimationPackageBuildResult result = BuildPackage(
                    asset,
                    skeletonRoot,
                    MxAnimationPackageProviderSampleKind.LocalAssetBundle,
                    acceptedProviderIds: new[] { "memory" });

                Assert.IsFalse(result.Success, result.ReportText);
                Assert.IsTrue(result.CatalogValidation.HasErrors);
                AssertPackageIssue(
                    result.PackageValidation,
                    MxAnimationPackageValidationIssueCodes.PackageResourceProviderMismatch);
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        [Test]
        public void Build_WithoutBakeAndCompatibilityInputs_ReportsMissingPackageInputs()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                var options = new MxAnimationPackageBuilderOptions(
                    packageVersion: 4,
                    providerSampleKind: MxAnimationPackageProviderSampleKind.LocalAssetBundle,
                    bundleName: "demo-animation-package");
                MxAnimationPackageBuildResult result = MxAnimationPackageBuilder.Build(asset, options);

                Assert.IsFalse(result.Success, result.ReportText);
                AssertPackageIssue(result.PackageValidation, MxAnimationPackageValidationIssueCodes.BakeArtifactMissing);
                AssertPackageIssue(result.PackageValidation, MxAnimationPackageValidationIssueCodes.CompatibilityProfileMissing);
                Assert.That(result.ReportText, Does.Contain(MxAnimationPackageValidationIssueCodes.BakeArtifactMissing));
                Assert.That(result.ReportText, Does.Contain(MxAnimationPackageValidationIssueCodes.CompatibilityProfileMissing));
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        [Test]
        public void Build_WithStaleBakeCompatibilityReport_FailsPackagePreview()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                MxAnimationBatchBakeReport staleBake =
                    MxAnimationWorkstationBakeUtility.BakeRegistryClips(asset, new[] { 0 });
                Assert.IsTrue(staleBake.Success, staleBake.ReportText);

                MxAnimationCompatibilityWorkstationReport compatibilityReport =
                    MxAnimationWorkstationBakeUtility.BuildCompatibilityReport(
                        asset,
                        skeletonRoot,
                        "humanoid",
                        new[] { "WeaponSocket" },
                        staleBake);
                Assert.IsFalse(compatibilityReport.Success, compatibilityReport.ReportText);

                var options = new MxAnimationPackageBuilderOptions(
                    packageVersion: 4,
                    providerSampleKind: MxAnimationPackageProviderSampleKind.LocalAssetBundle,
                    bundleName: "demo-animation-package");
                MxAnimationPackageBuildResult result =
                    MxAnimationPackageBuilder.Build(asset, options, staleBake, compatibilityReport);

                Assert.IsFalse(result.Success, result.ReportText);
                Assert.AreSame(compatibilityReport, result.CompatibilityReport);
                Assert.That(result.ReportText, Does.Contain("success: false"));
                Assert.That(result.ReportText, Does.Contain("compatibilityIssues:"));
                Assert.That(result.ReportText, Does.Contain("bakeFreshnessIssues:"));
                Assert.That(result.ReportText, Does.Contain("BakeSkeletonProfileHashMismatch"));
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        [Test]
        public void BuildExpectation_WhenCatalogEntriesAreMissing_ReportsSpecificResourceDiagnostics()
        {
            MxAnimationClipRegistryAsset asset = CreatePackageRegistry(out GameObject skeletonRoot);
            try
            {
                MxAnimationPackageBuildResult result = BuildPackage(
                    asset,
                    skeletonRoot,
                    MxAnimationPackageProviderSampleKind.LocalAssetBundle);
                var emptyCatalog = new ResourceCatalog(
                    result.CatalogSnapshot.CatalogId,
                    result.CatalogSnapshot.PackageId,
                    Array.Empty<ResourceCatalogEntry>());

                MxAnimationPackageValidationReport report = MxAnimationPackageCatalogValidator.Validate(
                    new MxAnimationPackageCatalog(
                        emptyCatalog,
                        result.PackageCatalog.Version,
                        result.PackageCatalog.CatalogHash,
                        result.PackageCatalog.PackageId,
                        result.PackageCatalog.CatalogId),
                    result.Expectation);

                Assert.IsFalse(report.Success);
                AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.AnimationClipMissing);
                AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.AvatarMaskMissing);
                AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.BakeArtifactMissing);
                AssertPackageIssue(report, MxAnimationPackageValidationIssueCodes.CompatibilityProfileMissing);
            }
            finally
            {
                DestroyPackageRegistry(asset, skeletonRoot);
            }
        }

        private static MxAnimationPackageBuildResult BuildPackage(
            MxAnimationClipRegistryAsset asset,
            GameObject skeletonRoot,
            MxAnimationPackageProviderSampleKind providerSampleKind,
            IEnumerable<string> acceptedProviderIds = null,
            string remoteBundleUrl = "",
            string remoteCacheKey = "",
            string remoteBundleHash = "")
        {
            MxAnimationSkeletonCompatibilityProfile skeletonProfile =
                MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
                    skeletonRoot,
                    "humanoid",
                    new[] { "WeaponSocket" });
            MxAnimationBatchBakeReport batchReport =
                MxAnimationWorkstationBakeUtility.BakeRegistryClips(asset, new[] { 0 }, skeletonProfile);
            MxAnimationCompatibilityWorkstationReport compatibilityReport =
                MxAnimationWorkstationBakeUtility.BuildCompatibilityReport(
                    asset,
                    skeletonRoot,
                    "humanoid",
                    new[] { "WeaponSocket" },
                    batchReport);
            var options = new MxAnimationPackageBuilderOptions(
                packageVersion: 4,
                providerSampleKind: providerSampleKind,
                bundleName: "demo-animation-package",
                remoteBundleUrl: remoteBundleUrl,
                remoteCacheKey: remoteCacheKey,
                remoteBundleHash: remoteBundleHash,
                acceptedProviderIds: acceptedProviderIds);
            return MxAnimationPackageBuilder.Build(asset, options, batchReport, compatibilityReport);
        }

        private static MxAnimationClipRegistryAsset CreatePackageRegistry(out GameObject skeletonRoot)
        {
            skeletonRoot = new GameObject("PackageSkeleton");
            GameObject hips = CreateChild(skeletonRoot.transform, "Hips");
            GameObject spine = CreateChild(hips.transform, "Spine");
            CreateChild(skeletonRoot.transform, "WeaponSocket");

            var clip = new AnimationClip { name = "Attack", frameRate = 30f };
            SetCurve(clip, "Hips/Spine", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            SetCurve(clip, "WeaponSocket", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 2f));
            var mask = new AvatarMask { name = "UpperBody" };
            mask.AddTransformPath(spine.transform, false);

            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            asset.AnimationSetId = "demo.set";
            asset.Version = 3;
            asset.PackageId = "demo.package";
            asset.Clips = new[]
            {
                new MxAnimationClipRegistryClipEntry
                {
                    ClipId = "attack",
                    Clip = clip,
                    ResourceId = "demo.animation.attack",
                    IsDefault = true,
                    IsFallback = true
                }
            };
            asset.Layers = new[]
            {
                new MxAnimationClipRegistryLayerEntry
                {
                    LayerId = "upper_body",
                    ProfileId = "humanoid.upper",
                    DefaultWeight = 1f,
                    AvatarMask = mask,
                    AvatarMaskResourceId = "demo.animation.mask.upper_body"
                }
            };
            asset.Bindings = new[]
            {
                new MxAnimationClipRegistryBindingEntry
                {
                    BindingId = "attack",
                    ActionKey = "action:1001",
                    ClipId = "attack",
                    LayerId = "upper_body",
                    Loop = false,
                    PlaybackSpeed = 1f
                }
            };
            return asset;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void DestroyPackageRegistry(MxAnimationClipRegistryAsset asset, GameObject skeletonRoot)
        {
            if (asset != null)
            {
                MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].Clip != null)
                        UnityEngine.Object.DestroyImmediate(clips[i].Clip);
                }

                MxAnimationClipRegistryLayerEntry[] layers = asset.Layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].AvatarMask != null)
                        UnityEngine.Object.DestroyImmediate(layers[i].AvatarMask);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }

            if (skeletonRoot != null)
                UnityEngine.Object.DestroyImmediate(skeletonRoot);
        }

        private static void SetCurve(AnimationClip clip, string path, string propertyName, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName),
                curve);
        }

        private static void AssertPackageIssue(MxAnimationPackageValidationReport report, string code)
        {
            if (report.Issues.Any(issue => issue.Code == code))
                return;

            Assert.Fail("Expected package issue: " + code + "\n" + Describe(report));
        }

        private static string Describe(MxAnimationPackageValidationReport report)
        {
            return string.Join("\n", report.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }

        private static string Describe(MxAnimationWarmupResult result)
        {
            return string.Join("\n", result.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }
    }
}
