using System.Collections.Generic;
using MxFramework.Demo;
using MxFramework.Editor;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class TempImportedResourceCatalogTests
    {
        private static readonly string[] ExpectedIds =
        {
            "art.weapon.katana.generic_01",
            TempImportedResourceCatalog.SkeletonModelId,
            TempImportedResourceCatalog.SkeletonIdleAnimationId,
            TempImportedResourceCatalog.SkeletonWalkForwardAnimationId,
            TempImportedResourceCatalog.SkeletonRunForwardAnimationId,
            TempImportedResourceCatalog.SkeletonJumpAnimationId,
            "art.character.skeleton.animation.standing_jump_running",
            "art.character.skeleton.animation.standing_jump_running_landing",
            "art.character.skeleton.animation.standing_land_to_standing_idle",
            "art.character.skeleton.animation.standing_run_back",
            "art.character.skeleton.animation.standing_run_left",
            "art.character.skeleton.animation.standing_run_right",
            "art.character.skeleton.animation.standing_sprint_forward",
            "art.character.skeleton.animation.standing_turn_left_90",
            "art.character.skeleton.animation.standing_turn_right_90",
            "art.character.skeleton.animation.standing_walk_back",
            "art.character.skeleton.animation.standing_walk_left",
            "art.character.skeleton.animation.standing_walk_right",
            "vfx.status_aura.burn",
            "vfx.status_aura.lightning",
            "vfx.status_aura.smoke",
            "vfx.status_aura.stun",
            "ui.start_screen.button.normal",
            "ui.start_screen.button.hover",
            "ui.start_screen.separator.diamond_line",
            "ui.start_screen.icon.archive_book",
            "ui.start_screen.icon.continue_hourglass",
            "ui.start_screen.icon.exit_door",
            "ui.start_screen.icon.settings_cog",
            "audio.magic_effect.explosion_fire_1",
            "audio.magic_effect.explosion_lightning_3",
            "audio.magic_effect.loop_fire_2",
            "audio.magic_effect.wind"
        };

        [Test]
        public void CreateCatalog_CoversDirectSampleEntriesDeterministically()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();

            Assert.AreEqual(TempImportedResourceCatalog.CatalogId, catalog.CatalogId);
            Assert.AreEqual(TempImportedResourceCatalog.PackageId, catalog.PackageId);
            Assert.AreEqual(ExpectedIds.Length, catalog.Entries.Count);

            for (int i = 0; i < ExpectedIds.Length; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                Assert.AreEqual(ExpectedIds[i], entry.Id);
                Assert.AreEqual(string.Empty, entry.Variant);
                Assert.AreEqual(TempImportedResourceCatalog.PackageId, entry.PackageId);
                Assert.AreEqual(TempImportedResourceCatalog.MemoryProviderId, entry.ProviderId);
                Assert.IsFalse(entry.AllowOverride);
                Assert.AreEqual(0, entry.Dependencies.Count);
                CollectionAssert.Contains(entry.Labels, TempImportedResourceCatalog.PackageLabel);
                AssertAssetPathPolicy(entry);
            }

            CollectionAssert.Contains(FindEntry(catalog, "art.weapon.katana.generic_01").Labels, TempImportedResourceCatalog.DomainArtLabel);
            CollectionAssert.Contains(FindEntry(catalog, TempImportedResourceCatalog.SkeletonModelId).Labels, TempImportedResourceCatalog.SampleSkeletonLabel);
            CollectionAssert.Contains(FindEntry(catalog, TempImportedResourceCatalog.SkeletonIdleAnimationId).Labels, TempImportedResourceCatalog.SampleSkeletonAnimationClipLabel);
            CollectionAssert.Contains(FindEntry(catalog, "ui.start_screen.button.normal").Labels, TempImportedResourceCatalog.DomainUiLabel);
            CollectionAssert.Contains(FindEntry(catalog, "vfx.status_aura.burn").Labels, TempImportedResourceCatalog.DomainVfxLabel);
            CollectionAssert.Contains(FindEntry(catalog, "audio.magic_effect.wind").Labels, TempImportedResourceCatalog.DomainAudioLabel);
        }

        [Test]
        public void ValidateCatalog_SampleAssetPathsExistAndTypesMatch()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(
                catalog,
                new[] { TempImportedResourceCatalog.MemoryProviderId });
            report.Merge(TempImportedResourceCatalog.ValidateCatalogBootstrap(catalog));

            Assert.IsFalse(report.HasErrors, ResourceCatalogEditorValidator.CreateReportText(catalog, report));
        }

        [Test]
        public void CreateMemoryProvider_RegistersCatalogEntriesByAddress()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            MemoryResourceProvider provider = TempImportedResourceCatalog.CreateMemoryProvider(
                catalog,
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>);

            for (int i = 0; i < catalog.Entries.Count; i++)
                Assert.IsTrue(provider.CanLoad(catalog.Entries[i]), "Address was not registered: " + catalog.Entries[i].Address);

            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(catalog);

            ResourceLoadResult<ResourceHandle<GameObject>> katana = manager.Load<GameObject>(
                new ResourceKey("art.weapon.katana.generic_01", ResourceTypeIds.GameObject, string.Empty, TempImportedResourceCatalog.PackageId));
            ResourceLoadResult<ResourceHandle<Texture2D>> button = manager.Load<Texture2D>(
                new ResourceKey("ui.start_screen.button.normal", ResourceTypeIds.Texture2D, string.Empty, TempImportedResourceCatalog.PackageId));
            ResourceLoadResult<ResourceHandle<AudioClip>> wind = manager.Load<AudioClip>(
                new ResourceKey("audio.magic_effect.wind", ResourceTypeIds.AudioClip, string.Empty, TempImportedResourceCatalog.PackageId));
            ResourceLoadResult<ResourceHandle<AnimationClip>> idle = manager.Load<AnimationClip>(
                new ResourceKey(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip, string.Empty, TempImportedResourceCatalog.PackageId));

            Assert.IsTrue(katana.Success, katana.Error.Message);
            Assert.IsTrue(button.Success, button.Error.Message);
            Assert.IsTrue(wind.Success, wind.Error.Message);
            Assert.IsTrue(idle.Success, idle.Error.Message);

            manager.Release(katana.Value);
            manager.Release(button.Value);
            manager.Release(wind.Value);
            manager.Release(idle.Value);
        }

        [Test]
        public void ValidateCatalogBootstrap_ReportsMissingAssetPathUnsupportedTypesAndUnregisteredAddresses()
        {
            var catalog = new ResourceCatalog(
                TempImportedResourceCatalog.CatalogId,
                TempImportedResourceCatalog.PackageId,
                new[]
                {
                    new ResourceCatalogEntry(
                        "ui.start_screen.invalid",
                        ResourceTypeIds.TextAsset,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "mxframework.samples/ui/start_screen/invalid")
                });

            ResourceCatalogValidationReport report = TempImportedResourceCatalog.ValidateCatalogBootstrap(
                catalog,
                new[] { "different/address" });

            AssertIssue(report, "ProviderDataAssetPathMissing");
            AssertIssue(report, "UnsupportedDirectAssetType");
            AssertIssue(report, "MemoryAddressUnregistered");
        }

        [Test]
        public void ValidateCatalogBootstrap_RejectsFmodBankAndEventValues()
        {
            var catalog = new ResourceCatalog(
                TempImportedResourceCatalog.CatalogId,
                TempImportedResourceCatalog.PackageId,
                new[]
                {
                    new ResourceCatalogEntry(
                        "audio.invalid.fmod_bank",
                        ResourceTypeIds.AudioClip,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "bank:/Master",
                        providerData: new Dictionary<string, string>
                        {
                            { TempImportedResourceCatalog.ProviderDataAssetPathKey, "Assets/StreamingAssets/Master.bank" }
                        }),
                    new ResourceCatalogEntry(
                        "audio.invalid.fmod_event",
                        ResourceTypeIds.AudioClip,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "event:/MxFramework/Demo/OneShot",
                        providerData: new Dictionary<string, string>
                        {
                            { TempImportedResourceCatalog.ProviderDataAssetPathKey, "event:/MxFramework/Demo/OneShot" }
                        })
                });

            ResourceCatalogValidationReport report = TempImportedResourceCatalog.ValidateCatalogBootstrap(catalog);

            AssertIssue(report, "FmodCatalogEntryForbidden");
        }

        private static ResourceCatalogEntry FindEntry(ResourceCatalog catalog, string id)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                if (catalog.Entries[i].Id == id)
                    return catalog.Entries[i];
            }

            Assert.Fail("Missing catalog entry: " + id);
            return null;
        }

        private static void AssertAssetPathPolicy(ResourceCatalogEntry entry)
        {
            Assert.IsTrue(entry.ProviderData.TryGetValue(TempImportedResourceCatalog.ProviderDataAssetPathKey, out string assetPath));
            Assert.IsTrue(assetPath.StartsWith("Assets/", System.StringComparison.Ordinal), assetPath);
            Assert.IsFalse(assetPath.Contains("_TempImportedResources"), assetPath);
            Assert.IsFalse(assetPath.EndsWith(".bank", System.StringComparison.OrdinalIgnoreCase), assetPath);
        }

        private static void AssertIssue(ResourceCatalogValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected resource catalog validation issue: " + code);
        }
    }
}
