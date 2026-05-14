using System.Collections.Generic;
using MxFramework.Demo;
using MxFramework.Editor;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class TempImportedResourceRuntimeUsageTests
    {
        [TestCase(TempImportedResourceCatalog.PackageLabel, 16)]
        [TestCase(TempImportedResourceCatalog.WarmupStartScreenLabel, 7)]
        [TestCase(TempImportedResourceCatalog.WarmupCombatLabel, 9)]
        [TestCase(TempImportedResourceCatalog.WarmupStatusEffectsLabel, 4)]
        [TestCase(TempImportedResourceCatalog.WarmupMagicEffectsLabel, 4)]
        public void PreloadAsync_BySampleLabel_LoadsExpectedGroupAndReleaseRestoresSnapshot(
            string label,
            int expectedCount)
        {
            SampleRuntimeFixture fixture = CreateFixture();
            var service = new ResourcePreloadService(fixture.Manager);

            ResourcePreloadResult result = service.PreloadAsync(new ResourcePreloadPlan(
                "issue67." + label,
                labels: new[] { label })).Result.Value;

            Assert.IsTrue(result.Success, CreateErrorText(result.Errors));
            Assert.AreEqual(expectedCount, result.RequestedCount);
            Assert.AreEqual(expectedCount, result.LoadedCount);
            Assert.AreEqual(expectedCount, result.Handle.Handles.Count);

            ResourceDebugSnapshot loaded = fixture.Manager.CreateDebugSnapshot();
            Assert.AreEqual(TempImportedResourceCatalog.CatalogId, loaded.Catalogs[0].CatalogId);
            Assert.AreEqual(TempImportedResourceCatalog.PackageId, loaded.Catalogs[0].PackageId);
            Assert.AreEqual(1, loaded.CatalogCount);
            Assert.AreEqual(16, loaded.EntryCount);
            Assert.AreEqual(1, loaded.ProviderCount);
            Assert.AreEqual(expectedCount, loaded.LoadedCount);
            Assert.AreEqual(expectedCount, loaded.TotalRefCount);

            service.ReleaseGroup(result.Handle);
            service.ReleaseGroup(result.Handle);

            Assert.IsTrue(result.Handle.IsReleased);
            ResourceDebugSnapshot released = fixture.Manager.CreateDebugSnapshot();
            Assert.AreEqual(0, released.LoadedCount);
            Assert.AreEqual(0, released.TotalRefCount);
            Assert.AreEqual(expectedCount, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void LoadDirectAssets_FromConfigProfile_LoadsExpectedTypesAndReleaseRestoresSnapshot()
        {
            SampleRuntimeFixture fixture = CreateFixture();
            ResourceKeyConfigProfile profile = ResourceKeyConfigProfile.CreateSample();

            var prefabHandles = new List<ResourceHandle<GameObject>>();
            var textureHandles = new List<ResourceHandle<Texture2D>>();
            var audioHandles = new List<ResourceHandle<AudioClip>>();
            GameObject katanaInstance = null;

            try
            {
                ResourceHandle<GameObject> weapon = Load<GameObject>(fixture.Manager, profile.WeaponPrefab);
                prefabHandles.Add(weapon);
                katanaInstance = Object.Instantiate(weapon.Value);
                Assert.NotNull(katanaInstance);

                for (int i = 0; i < profile.StatusAuraPrefabs.Count; i++)
                    prefabHandles.Add(Load<GameObject>(fixture.Manager, profile.StatusAuraPrefabs[i]));

                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenButtonNormalTexture));
                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenButtonHoverTexture));
                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenSeparatorTexture));
                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenArchiveIconTexture));
                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenContinueIconTexture));
                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenExitIconTexture));
                textureHandles.Add(Load<Texture2D>(fixture.Manager, profile.StartScreenSettingsIconTexture));

                for (int i = 0; i < profile.MagicEffectAudioClips.Count; i++)
                    audioHandles.Add(Load<AudioClip>(fixture.Manager, profile.MagicEffectAudioClips[i]));

                ResourceDebugSnapshot loaded = fixture.Manager.CreateDebugSnapshot();
                Assert.AreEqual(16, loaded.LoadedCount);
                Assert.AreEqual(16, loaded.TotalRefCount);
                Assert.AreEqual(0, loaded.FailedCount);
            }
            finally
            {
                if (katanaInstance != null)
                    Object.DestroyImmediate(katanaInstance);

                ReleaseAll(fixture.Manager, audioHandles);
                ReleaseAll(fixture.Manager, textureHandles);
                ReleaseAll(fixture.Manager, prefabHandles);
            }

            ResourceDebugSnapshot released = fixture.Manager.CreateDebugSnapshot();
            Assert.AreEqual(0, released.LoadedCount);
            Assert.AreEqual(0, released.TotalRefCount);
            Assert.AreEqual(16, fixture.Provider.ReleaseCount);
        }

        [Test]
        public void Load_WhenSampleKeyMissing_ReturnsNotFoundAndRecordsDiagnostics()
        {
            SampleRuntimeFixture fixture = CreateFixture();

            ResourceLoadResult<ResourceHandle<Texture2D>> result = fixture.Manager.Load<Texture2D>(
                new ResourceKey(
                    "ui.start_screen.missing",
                    ResourceTypeIds.Texture2D,
                    string.Empty,
                    TempImportedResourceCatalog.PackageId));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Error.Code);

            ResourceDebugSnapshot snapshot = fixture.Manager.CreateDebugSnapshot();
            Assert.AreEqual(0, snapshot.LoadedCount);
            Assert.AreEqual(1, snapshot.FailedCount);
            Assert.AreEqual(ResourceErrorCode.NotFound, snapshot.RecentErrors[snapshot.RecentErrors.Count - 1].Code);
        }

        private static SampleRuntimeFixture CreateFixture()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            ResourceCatalogValidationReport validation = ResourceCatalogEditorValidator.ValidateCatalog(
                catalog,
                new[] { TempImportedResourceCatalog.MemoryProviderId });
            validation.Merge(TempImportedResourceCatalog.ValidateCatalogBootstrap(catalog));
            Assert.IsFalse(validation.HasErrors, ResourceCatalogEditorValidator.CreateReportText(catalog, validation));

            MemoryResourceProvider provider = TempImportedResourceCatalog.CreateMemoryProvider(
                catalog,
                AssetDatabase.LoadAssetAtPath<Object>);

            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.SetVariantProfile(ResourceVariantProfile.Empty);
            manager.AddCatalog(catalog);
            manager.ValidateCatalogs();

            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            Assert.AreEqual(1, snapshot.CatalogCount);
            Assert.AreEqual(16, snapshot.EntryCount);
            Assert.AreEqual(1, snapshot.ProviderCount);
            Assert.AreEqual(0, snapshot.LoadedCount);

            return new SampleRuntimeFixture(manager, provider);
        }

        private static ResourceHandle<T> Load<T>(ResourceManager manager, ResourceKey key)
        {
            ResourceLoadResult<ResourceHandle<T>> result = manager.Load<T>(key);
            Assert.IsTrue(result.Success, result.Error.Message);
            Assert.NotNull(result.Value.Value);
            return result.Value;
        }

        private static void ReleaseAll<T>(ResourceManager manager, List<ResourceHandle<T>> handles)
        {
            for (int i = handles.Count - 1; i >= 0; i--)
                manager.Release(handles[i]);
        }

        private static string CreateErrorText(IReadOnlyList<ResourceError> errors)
        {
            var lines = new List<string>();
            for (int i = 0; i < errors.Count; i++)
                lines.Add(errors[i].ToString());

            return string.Join("\n", lines.ToArray());
        }

        private readonly struct SampleRuntimeFixture
        {
            public SampleRuntimeFixture(ResourceManager manager, MemoryResourceProvider provider)
            {
                Manager = manager;
                Provider = provider;
            }

            public ResourceManager Manager { get; }
            public MemoryResourceProvider Provider { get; }
        }
    }
}
