using System.Linq;
using MxFramework.Animation;
using MxFramework.Editor.Animation;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationModOverrideTests
    {
        [Test]
        public void Merge_ValidOverride_ChangesActionMaskBlendBakeAndKeepsStableHashes()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake);
            ResourceCatalog catalog = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask, bake);
            MxAnimationCompatibilityProfile profile = CompatibilityProfile(
                new[] { baseAttack, modAttack, baseRun, modRun },
                new[] { baseMask, modMask });

            MxAnimationModOverrideMergeResult first = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    profile,
                    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));
            MxAnimationModOverrideMergeResult second = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    profile,
                    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));

            Assert.IsTrue(first.Success, Describe(first));
            Assert.IsTrue(second.Success, Describe(second));
            Assert.AreEqual(baseDefinition.DefinitionHash, first.BaseDefinitionHash);
            Assert.AreEqual(overrideDefinition.OverrideHash, first.OverrideHash);
            Assert.AreEqual(first.MergedDefinition.DefinitionHash, second.MergedDefinition.DefinitionHash);
            Assert.AreEqual(3, first.AcceptedOverrideCount);
            Assert.AreEqual(0, first.RejectedOverrideCount);
            Assert.IsTrue(first.MergedDefinition.TryFindBinding("attack", "action:attack", out MxAnimationActionBinding binding));
            Assert.AreEqual(modAttack, binding.Clip);
            Assert.IsTrue(first.MergedDefinition.TryFindLayerDefinition(new MxAnimationLayerId("upper_body"), out MxAnimationLayerDefinition layer));
            Assert.AreEqual(modMask, layer.AvatarMaskKey);
            Assert.IsTrue(first.MergedDefinition.TryFindBlend1DDefinition("locomotion", "speed", out MxAnimationBlend1DDefinition blend));
            Assert.AreEqual(modRun, blend.Points[1].ClipKey);
            Assert.IsTrue(first.MergedPackageExpectation.Resources.Any(resource => resource.Key == bake));
        }

        [Test]
        public void Merge_WhenBaseHashMismatch_RejectsOverride()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey attack = Clip("demo.animation.attack.base");
            ResourceKey run = Clip("demo.animation.run.base");
            ResourceKey mask = Mask("demo.animation.mask.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, attack, run, mask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake,
                expectedBaseHash: "sha256:stale");

            MxAnimationModOverrideMergeResult result = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(baseDefinition, overrideDefinition));

            Assert.IsFalse(result.Success);
            AssertIssue(result, MxAnimationModOverrideIssueCodes.BaseHashMismatch, "baseHash");
        }

        [Test]
        public void Merge_WhenBaseExpectationMissing_RejectsOverride()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey attack = Clip("demo.animation.attack.base");
            ResourceKey run = Clip("demo.animation.run.base");
            ResourceKey mask = Mask("demo.animation.mask.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, attack, run, mask);
            var overrideDefinition = new MxAnimationModOverrideDefinition(
                baseDefinition.SetId,
                new MxAnimationModPackageManifest("mod.anim.demo", 1),
                overrideVersion: 1,
                actionOverrides: new[]
                {
                    new MxAnimationActionBindingOverride(new MxAnimationActionBinding(
                        "attack",
                        "action:attack",
                        modAttack,
                        new MxAnimationLayerId("upper_body")))
                });

            MxAnimationModOverrideMergeResult result = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(baseDefinition, overrideDefinition));

            Assert.IsFalse(result.Success);
            AssertIssue(result, MxAnimationModOverrideIssueCodes.BaseVersionExpectationMissing, "expectedBaseVersion");
            AssertIssue(result, MxAnimationModOverrideIssueCodes.BaseHashExpectationMissing, "expectedBaseHash");
        }

        [Test]
        public void Merge_WhenOverrideContentChangesButHashIsReused_RejectsOverride()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition original = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake);
            MxAnimationModOverrideDefinition tampered = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake,
                requiredBindingPath: "Hips/ChangedBinding",
                overrideHash: original.OverrideHash);
            ResourceCatalog catalog = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask, bake);

            MxAnimationModOverrideMergeResult result = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    tampered,
                    catalog,
                    CompatibilityProfile(new[] { baseAttack, modAttack, baseRun, modRun }, new[] { baseMask, modMask }),
                    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));

            Assert.IsFalse(result.Success);
            AssertIssue(result, MxAnimationModOverrideIssueCodes.OverrideHashMismatch, "overrideHash");
        }

        [Test]
        public void Merge_WhenCatalogAndCompatibilityProfileMissing_RejectsFailClosed()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake);

            MxAnimationModOverrideMergeResult result = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(baseDefinition, overrideDefinition));

            Assert.IsFalse(result.Success);
            AssertIssue(result, MxAnimationModOverrideIssueCodes.MappingValidationFailed, "CatalogMissing");
            AssertIssue(result, MxAnimationModOverrideIssueCodes.CompatibilityValidationFailed, "compatibilityProfile");
            AssertIssue(result, MxAnimationModOverrideIssueCodes.PackageValidationFailed, MxAnimationPackageValidationIssueCodes.PackageCatalogMissing);
        }

        [Test]
        public void Merge_WhenOverrideCompatibilityFails_RejectsWithDiagnostics()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake,
                requiredBindingPath: "Hips/MissingWeapon");
            ResourceCatalog catalog = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask, bake);
            MxAnimationCompatibilityProfile profile = CompatibilityProfile(
                new[] { baseAttack, modAttack, baseRun, modRun },
                new[] { baseMask, modMask });

            MxAnimationModOverrideMergeResult result = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    profile,
                    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));

            Assert.IsFalse(result.Success);
            AssertIssue(result, MxAnimationModOverrideIssueCodes.CompatibilityValidationFailed, MxAnimationCompatibilityIssueCodes.ClipBindingPathMissing);
            Assert.IsNull(result.MergedDefinition);
        }

        [Test]
        public void Merge_WhenPackageBakeResourceMissing_RejectsWithPackageDiagnostics()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake);
            ResourceCatalog catalog = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask);
            MxAnimationCompatibilityProfile profile = CompatibilityProfile(
                new[] { baseAttack, modAttack, baseRun, modRun },
                new[] { baseMask, modMask });

            MxAnimationModOverrideMergeResult result = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    profile,
                    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));

            Assert.IsFalse(result.Success);
            AssertIssue(result, MxAnimationModOverrideIssueCodes.PackageValidationFailed, MxAnimationPackageValidationIssueCodes.BakeArtifactMissing);
        }

        [Test]
        public void Warmup_MergedOverridePackage_LoadsAndReleasesHandles()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake);
            ResourceCatalog catalog = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask, bake);
            MxAnimationCompatibilityProfile compatibilityProfile = CompatibilityProfile(
                new[] { baseAttack, modAttack, baseRun, modRun },
                new[] { baseMask, modMask });
            MxAnimationModOverrideMergeResult merge = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    compatibilityProfile,
                    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));
            ResourceManager manager = new ResourceManager();
            manager.RegisterProvider(new MemoryResourceProvider()
                .Register(Address(idle), "Idle")
                .Register(Address(baseAttack), "BaseAttack")
                .Register(Address(modAttack), "ModAttack")
                .Register(Address(baseRun), "BaseRun")
                .Register(Address(modRun), "ModRun")
                .Register(Address(baseMask), "BaseMask")
                .Register(Address(modMask), "ModMask")
                .Register(Address(bake), "Bake"));
            manager.AddCatalog(catalog);
            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));

            MxAnimationWarmupResult warmup = service.Warmup(new MxAnimationWarmupRequest(
                merge.MergedDefinition,
                MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "mod-catalog-hash"),
                catalog,
                null,
                null,
                true,
                compatibilityProfile,
                merge.MergedPackageExpectation,
                new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash")));

            Assert.IsTrue(merge.Success, Describe(merge));
            Assert.IsTrue(warmup.Success, Describe(warmup));
            Assert.AreEqual(7, manager.CreateDebugSnapshot().LoadedCount);

            service.Release(warmup);

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }

        [Test]
        public void WorkstationPreview_ReportIncludesMergerCountsHashesPackageAndWarmupDiagnostics()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake,
                includeBlend2D: true);
            ResourceCatalog catalog = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask, bake);
            var packageCatalog = new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "mod-catalog-hash");

            MxAnimationModOverrideWorkstationPreview preview =
                MxAnimationModOverrideWorkstationPreviewBuilder.Build(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    packageCatalog,
                    null,
                    CompatibilityProfile(new[] { baseAttack, modAttack, baseRun, modRun }, new[] { baseMask, modMask }),
                    runWarmupValidation: true);

            Assert.IsTrue(preview.Success, preview.ReportText);
            Assert.IsTrue(preview.MergeResult.Success, Describe(preview.MergeResult));
            Assert.AreEqual(4, preview.MergeResult.AcceptedOverrideCount);
            Assert.AreEqual(0, preview.MergeResult.RejectedOverrideCount);
            Assert.That(preview.Rows.Any(row => row.Status == MxAnimationModOverridePreviewRowStatus.Accepted && row.Category == "action"));
            Assert.That(preview.Rows.Any(row => row.Status == MxAnimationModOverridePreviewRowStatus.Accepted && row.Category == "event"));
            Assert.That(preview.Rows.Any(row => row.Status == MxAnimationModOverridePreviewRowStatus.Accepted && row.Category == "layer"));
            Assert.That(preview.Rows.Any(row => row.Status == MxAnimationModOverridePreviewRowStatus.Accepted && row.Category == "blend1D"));
            Assert.That(preview.Rows.Any(row => row.Status == MxAnimationModOverridePreviewRowStatus.Accepted && row.Category == "blend2D"));
            Assert.That(preview.Rows.Any(row => row.Status == MxAnimationModOverridePreviewRowStatus.Accepted && row.Category == "packageResource"));
            Assert.That(preview.ReportText, Does.Contain("acceptedOverrides: 4"));
            Assert.That(preview.ReportText, Does.Contain("rejectedOverrides: 0"));
            Assert.That(preview.ReportText, Does.Contain("baseHash: " + baseDefinition.DefinitionHash));
            Assert.That(preview.ReportText, Does.Contain("overrideHash: " + overrideDefinition.OverrideHash));
            Assert.That(preview.ReportText, Does.Contain("mergedDefinitionHash: " + preview.MergeResult.MergedDefinition.DefinitionHash));
            Assert.That(preview.ReportText, Does.Contain("packageDiagnostics:"));
            Assert.That(preview.ReportText, Does.Contain("compatibilityDiagnostics:"));
            Assert.That(preview.ReportText, Does.Contain("warmupValidation:"));
            Assert.That(preview.ReportText, Does.Contain("- success: true"));
        }

        [Test]
        public void WorkstationPreview_RejectedDiagnosticsSurfaceMergerIssueFields()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey bake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                bake,
                expectedBaseHash: "sha256:stale");

            MxAnimationModOverrideWorkstationPreview preview =
                MxAnimationModOverrideWorkstationPreviewBuilder.Build(
                    baseDefinition,
                    overrideDefinition,
                    null,
                    null,
                    null,
                    null);

            Assert.IsFalse(preview.Success);
            Assert.IsTrue(preview.Rows.Any(row =>
                row.Status == MxAnimationModOverridePreviewRowStatus.Rejected
                && row.Code == MxAnimationModOverrideIssueCodes.BaseHashMismatch
                && row.Field == "baseHash"), preview.ReportText);
            Assert.That(preview.ReportText, Does.Contain(MxAnimationModOverrideIssueCodes.BaseHashMismatch));
            Assert.That(preview.ReportText, Does.Contain("field=baseHash"));
            Assert.That(preview.ReportText, Does.Contain("expected=sha256:stale"));
            Assert.That(preview.ReportText, Does.Contain("actual=" + baseDefinition.DefinitionHash));
            Assert.That(preview.ReportText, Does.Contain("Mod animation override was authored for a different base mapping hash."));
        }

        [Test]
        public void WorkstationPreview_BuildFromRegistriesFailsWhenPackageBuilderReportFails()
        {
            MxAnimationClipRegistryAsset baseRegistry = CreateRegistryAsset("base.anim.demo", Clip("demo.animation.attack.base"));
            MxAnimationClipRegistryAsset overrideRegistry = CreateRegistryAsset("mod.anim.demo", Clip("demo.animation.attack.mod", "mod.anim.demo"));
            try
            {
                MxAnimationPackageBuildResult failedPackageBuild = MxAnimationPackageBuilder.Build(
                    overrideRegistry,
                    new MxAnimationPackageBuilderOptions(
                        packageId: "mod.anim.demo",
                        packageVersion: 2,
                        providerSampleKind: MxAnimationPackageProviderSampleKind.Memory));
                Assert.IsFalse(failedPackageBuild.Success, failedPackageBuild.ReportText);

                MxAnimationModOverrideWorkstationPreview preview =
                    MxAnimationModOverrideWorkstationPreviewBuilder.BuildFromRegistries(
                        baseRegistry,
                        overrideRegistry,
                        failedPackageBuild,
                        compatibilityReport: null,
                        overrideVersion: 1,
                        resultVersion: 0,
                        loadOrder: 10,
                        runWarmupValidation: false);

                Assert.IsFalse(preview.InputDiagnosticsSuccess, preview.ReportText);
                Assert.IsFalse(preview.Success, preview.ReportText);
                Assert.That(preview.ReportText, Does.Contain("packageBuilderSuccess: false"));
                Assert.That(preview.ReportText, Does.Contain("inputDiagnosticsSuccess: false"));
                Assert.That(preview.ReportText, Does.Contain("packageBuildReport:"));
            }
            finally
            {
                DestroyRegistryAsset(baseRegistry);
                DestroyRegistryAsset(overrideRegistry);
            }
        }

        [Test]
        public void WorkstationPreview_PackageResourceRowsReflectMergeRejection()
        {
            ResourceKey idle = Clip("demo.animation.idle");
            ResourceKey baseAttack = Clip("demo.animation.attack.base");
            ResourceKey modAttack = Clip("demo.animation.attack.mod", "mod.anim.demo");
            ResourceKey baseRun = Clip("demo.animation.run.base");
            ResourceKey modRun = Clip("demo.animation.run.mod", "mod.anim.demo");
            ResourceKey baseMask = Mask("demo.animation.mask.base");
            ResourceKey modMask = Mask("demo.animation.mask.mod", "mod.anim.demo");
            ResourceKey missingBake = Bake("demo.animation.bake.attack.mod", "mod.anim.demo");
            MxAnimationSetDefinition baseDefinition = CreateBaseDefinition(idle, baseAttack, baseRun, baseMask);
            MxAnimationModOverrideDefinition overrideDefinition = CreateOverride(
                baseDefinition,
                modAttack,
                modRun,
                modMask,
                missingBake);
            ResourceCatalog catalogWithoutBake = Catalog(idle, baseAttack, modAttack, baseRun, modRun, baseMask, modMask);
            var packageCatalog = new MxAnimationPackageCatalog(catalogWithoutBake, version: 2, catalogHash: "mod-catalog-hash");

            MxAnimationModOverrideWorkstationPreview preview =
                MxAnimationModOverrideWorkstationPreviewBuilder.Build(
                    baseDefinition,
                    overrideDefinition,
                    catalogWithoutBake,
                    packageCatalog,
                    null,
                    CompatibilityProfile(new[] { baseAttack, modAttack, baseRun, modRun }, new[] { baseMask, modMask }));

            Assert.IsFalse(preview.Success, preview.ReportText);
            Assert.That(preview.Rows.Any(row =>
                row.Status == MxAnimationModOverridePreviewRowStatus.Rejected
                && row.Category == "packageResource"
                && row.Target.Contains(missingBake.Id)), preview.ReportText);
            Assert.That(preview.ReportText, Does.Contain(MxAnimationModOverrideIssueCodes.PackageValidationFailed));
            Assert.That(preview.ReportText, Does.Contain(MxAnimationPackageValidationIssueCodes.BakeArtifactMissing));
        }

        private static MxAnimationSetDefinition CreateBaseDefinition(
            ResourceKey idle,
            ResourceKey attack,
            ResourceKey run,
            ResourceKey mask)
        {
            return new MxAnimationSetDefinition(
                "demo.actor",
                1,
                idle,
                idle,
                new[]
                {
                    new MxAnimationActionBinding(
                        "attack",
                        "action:attack",
                        attack,
                        new MxAnimationLayerId("upper_body"))
                },
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base),
                    new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), avatarMaskKey: mask)
                },
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idle),
                            new MxAnimationBlend1DPoint(1000, run)
                        })
                },
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Spine" },
                    null,
                    new[] { new MxAnimationClipCompatibilityExpectation(attack, new[] { "Hips/Spine" }) },
                    new[] { new MxAnimationAvatarMaskCompatibilityExpectation(mask, new[] { "Hips/Spine" }) }));
        }

        private static MxAnimationModOverrideDefinition CreateOverride(
            MxAnimationSetDefinition baseDefinition,
            ResourceKey attack,
            ResourceKey run,
            ResourceKey mask,
            ResourceKey bake,
            string expectedBaseHash = null,
            string requiredBindingPath = "Hips/Spine",
            string overrideHash = "",
            bool includeBlend2D = false)
        {
            MxAnimationBlend2DDefinitionOverride[] blend2DOverrides = includeBlend2D
                ? new[]
                {
                    new MxAnimationBlend2DDefinitionOverride(new MxAnimationBlend2DDefinition(
                        "directional_locomotion",
                        "moveX",
                        "moveY",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, Clip("demo.animation.idle")),
                            new MxAnimationBlend2DPoint(1000, 0, run),
                            new MxAnimationBlend2DPoint(0, 1000, attack)
                        }))
                }
                : null;

            return new MxAnimationModOverrideDefinition(
                baseDefinition.SetId,
                new MxAnimationModPackageManifest(
                    "mod.anim.demo",
                    2,
                    "Demo Animation Mod",
                    "mod.anim.demo.catalog",
                    "mod-catalog-hash",
                    loadOrder: 10),
                overrideVersion: 1,
                expectedBaseVersion: baseDefinition.Version,
                expectedBaseHash: expectedBaseHash ?? baseDefinition.DefinitionHash,
                actionOverrides: new[]
                {
                    new MxAnimationActionBindingOverride(new MxAnimationActionBinding(
                        "attack",
                        "action:attack",
                        attack,
                        new MxAnimationLayerId("upper_body"),
                        presentationEvents: new[]
                        {
                            new MxAnimationPresentationEvent(
                                "event:attack.vfx",
                                MxAnimationEventTimeDomain.PresentationFrame,
                                3f,
                                "VFX",
                                default,
                                "WeaponSocket")
                        },
                        fadeDurationSeconds: 0.05f))
                },
                layerOverrides: new[]
                {
                    new MxAnimationLayerDefinitionOverride(
                        new MxAnimationLayerDefinition(
                            new MxAnimationLayerId("upper_body"),
                            avatarMaskKey: mask))
                },
                blend1DOverrides: new[]
                {
                    new MxAnimationBlend1DDefinitionOverride(new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, Clip("demo.animation.idle")),
                            new MxAnimationBlend1DPoint(1000, run)
                        }))
                },
                blend2DOverrides: blend2DOverrides,
                packageResources: new[]
                {
                    new MxAnimationPackageResourceExpectation(attack, "hash-" + attack.Id, "memory"),
                    new MxAnimationPackageResourceExpectation(mask, "hash-" + mask.Id, "memory"),
                    new MxAnimationPackageResourceExpectation(run, "hash-" + run.Id, "memory"),
                    new MxAnimationPackageResourceExpectation(bake, "hash-" + bake.Id, "memory")
                },
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Spine" },
                    null,
                    new[] { new MxAnimationClipCompatibilityExpectation(attack, new[] { requiredBindingPath }) },
                    new[] { new MxAnimationAvatarMaskCompatibilityExpectation(mask, new[] { "Hips/Spine" }) }),
                acceptedProviderIds: new[] { "memory" },
                overrideHash: overrideHash);
        }

        private static MxAnimationClipRegistryAsset CreateRegistryAsset(string packageId, ResourceKey attackKey)
        {
            var idleClip = new AnimationClip { name = packageId + ".idle" };
            var attackClip = new AnimationClip { name = packageId + ".attack" };
            var mask = new AvatarMask { name = packageId + ".upper" };
            var asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            asset.AnimationSetId = "demo.actor";
            asset.Version = 1;
            asset.PackageId = packageId;
            asset.Clips = new[]
            {
                new MxAnimationClipRegistryClipEntry
                {
                    ClipId = "idle",
                    Clip = idleClip,
                    ResourceId = "demo.animation.idle",
                    IsDefault = true,
                    IsFallback = true
                },
                new MxAnimationClipRegistryClipEntry
                {
                    ClipId = "attack",
                    Clip = attackClip,
                    ResourceId = attackKey.Id,
                    PackageId = attackKey.PackageId
                }
            };
            asset.Layers = new[]
            {
                new MxAnimationClipRegistryLayerEntry
                {
                    LayerId = "upper_body",
                    AvatarMask = mask,
                    AvatarMaskResourceId = "demo.animation.mask.upper",
                    AvatarMaskPackageId = packageId
                }
            };
            asset.Bindings = new[]
            {
                new MxAnimationClipRegistryBindingEntry
                {
                    BindingId = "attack",
                    ActionKey = "action:attack",
                    ClipId = "attack",
                    LayerId = "upper_body"
                }
            };
            return asset;
        }

        private static void DestroyRegistryAsset(MxAnimationClipRegistryAsset asset)
        {
            if (asset == null)
                return;

            MxAnimationClipRegistryClipEntry[] clips = asset.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].Clip != null)
                    Object.DestroyImmediate(clips[i].Clip);
            }

            MxAnimationClipRegistryLayerEntry[] layers = asset.Layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].AvatarMask != null)
                    Object.DestroyImmediate(layers[i].AvatarMask);
            }

            Object.DestroyImmediate(asset);
        }

        private static ResourceCatalog Catalog(params ResourceKey[] keys)
        {
            return new ResourceCatalog(
                "mod.anim.demo.catalog",
                "mod.anim.demo",
                keys.Select(key => new ResourceCatalogEntry(
                    key.Id,
                    key.TypeId,
                    "memory",
                    Address(key),
                    key.Variant,
                    key.PackageId,
                    hash: "hash-" + key.Id)));
        }

        private static MxAnimationCompatibilityProfile CompatibilityProfile(
            ResourceKey[] clips,
            ResourceKey[] masks)
        {
            var skeleton = new MxAnimationSkeletonCompatibilityProfile(
                "humanoid",
                "sha256:skeleton",
                new[] { "Hips", "Hips/Spine" },
                null);
            return new MxAnimationCompatibilityProfile(
                skeleton,
                clips.Select(clip => new MxAnimationClipCompatibilityProfile(
                    clip,
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Spine" })),
                masks.Select(mask => new MxAnimationAvatarMaskCompatibilityProfile(
                    mask,
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Spine" })));
        }

        private static ResourceKey Clip(string id, string packageId = "")
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip, packageId: packageId);
        }

        private static ResourceKey Mask(string id, string packageId = "")
        {
            return new ResourceKey(id, ResourceTypeIds.AvatarMask, packageId: packageId);
        }

        private static ResourceKey Bake(string id, string packageId = "")
        {
            return new ResourceKey(id, MxAnimationResourceTypeIds.BakeArtifact, packageId: packageId);
        }

        private static string Address(ResourceKey key)
        {
            return (string.IsNullOrWhiteSpace(key.PackageId) ? "base" : key.PackageId) + "/" + key.Id;
        }

        private static void AssertIssue(MxAnimationModOverrideMergeResult result, string code, string field)
        {
            Assert.IsTrue(
                result.Issues.Any(issue => issue.Code == code && issue.Field == field),
                Describe(result));
        }

        private static string Describe(MxAnimationModOverrideMergeResult result)
        {
            return string.Join("\n", result.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }

        private static string Describe(MxAnimationWarmupResult result)
        {
            return string.Join("\n", result.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }
    }
}
