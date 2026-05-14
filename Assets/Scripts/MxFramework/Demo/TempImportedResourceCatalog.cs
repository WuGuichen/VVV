using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Demo
{
    public static class TempImportedResourceCatalog
    {
        public const string CatalogId = "mxframework.samples";
        public const string PackageId = "mxframework.samples";
        public const string MemoryProviderId = "memory";
        public const string ProviderDataAssetPathKey = "assetPath";
        public const string PackageLabel = "package.mxframework.samples";

        public const string DomainArtLabel = "domain.art";
        public const string DomainUiLabel = "domain.ui";
        public const string DomainVfxLabel = "domain.vfx";
        public const string DomainAudioLabel = "domain.audio";

        public const string SampleKatanaLabel = "sample.katana";
        public const string SampleStartScreenLabel = "sample.start_screen";
        public const string SampleStatusAurasLabel = "sample.status_auras";
        public const string SampleMagicEffectsLabel = "sample.magic_effects";

        public const string WarmupStartScreenLabel = "warmup.demo.start_screen";
        public const string WarmupCombatLabel = "warmup.demo.combat";
        public const string WarmupStatusEffectsLabel = "warmup.demo.status_effects";
        public const string WarmupMagicEffectsLabel = "warmup.demo.magic_effects";

        private static readonly TempImportedResourceCatalogAsset[] Assets =
        {
            new TempImportedResourceCatalogAsset(
                "art.weapon.katana.generic_01",
                ResourceTypeIds.GameObject,
                "mxframework.samples/art/weapon/katana/generic_01",
                "Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab",
                new[] { PackageLabel, DomainArtLabel, SampleKatanaLabel, WarmupCombatLabel }),

            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.burn",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/burn",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Burn.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.lightning",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/lightning",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Lightning.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.smoke",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/smoke",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Smoke.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "vfx.status_aura.stun",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/stun",
                "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Stun.prefab",
                new[] { PackageLabel, DomainVfxLabel, SampleStatusAurasLabel, WarmupCombatLabel, WarmupStatusEffectsLabel }),

            new TempImportedResourceCatalogAsset(
                "ui.start_screen.button.normal",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/button/normal",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.button.hover",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/button/hover",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/button_hover.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.separator.diamond_line",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/separator/diamond_line",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/separator_diamond_line.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.archive_book",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/archive_book",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_archive_book.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.continue_hourglass",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/continue_hourglass",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_continue_hourglass.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.exit_door",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/exit_door",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_exit_door.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),
            new TempImportedResourceCatalogAsset(
                "ui.start_screen.icon.settings_cog",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/settings_cog",
                "Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_settings_cog.png",
                new[] { PackageLabel, DomainUiLabel, SampleStartScreenLabel, WarmupStartScreenLabel }),

            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.explosion_fire_1",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/explosion_fire_1",
                "Assets/Audio/MxFramework/Samples/MagicEffects/explosion_fire_1.ogg",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.explosion_lightning_3",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/explosion_lightning_3",
                "Assets/Audio/MxFramework/Samples/MagicEffects/explosion_lightning_3.wav",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.loop_fire_2",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/loop_fire_2",
                "Assets/Audio/MxFramework/Samples/MagicEffects/loop_fire_2.ogg",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel }),
            new TempImportedResourceCatalogAsset(
                "audio.magic_effect.wind",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/wind",
                "Assets/Audio/MxFramework/Samples/MagicEffects/wind.wav",
                new[] { PackageLabel, DomainAudioLabel, SampleMagicEffectsLabel, WarmupCombatLabel, WarmupMagicEffectsLabel })
        };

        public static IReadOnlyList<TempImportedResourceCatalogAsset> GetAssets()
        {
            return Assets;
        }

        public static ResourceCatalog CreateCatalog()
        {
            var entries = new List<ResourceCatalogEntry>(Assets.Length);
            for (int i = 0; i < Assets.Length; i++)
                entries.Add(Assets[i].CreateEntry());

            return new ResourceCatalog(CatalogId, PackageId, entries);
        }

        public static MemoryResourceProvider CreateMemoryProvider(ResourceCatalog catalog, Func<string, UnityEngine.Object> loadAssetAtPath)
        {
            if (loadAssetAtPath == null)
                throw new ArgumentNullException(nameof(loadAssetAtPath));

            var provider = new MemoryResourceProvider();
            if (catalog == null)
                return provider;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !string.Equals(entry.ProviderId, MemoryProviderId, StringComparison.Ordinal))
                    continue;
                if (!entry.ProviderData.TryGetValue(ProviderDataAssetPathKey, out string assetPath) || string.IsNullOrWhiteSpace(assetPath))
                    continue;

                UnityEngine.Object asset = loadAssetAtPath(assetPath);
                if (asset != null)
                    provider.Register(entry.Address, asset);
            }

            return provider;
        }

        public static ResourceCatalogValidationReport ValidateCatalogBootstrap(
            ResourceCatalog catalog,
            IEnumerable<string> registeredMemoryAddresses = null)
        {
            var report = new ResourceCatalogValidationReport();
            if (catalog == null)
            {
                report.AddError("CatalogMissing", default, "Temp imported resource catalog is null.");
                return report;
            }

            HashSet<string> registeredAddresses = CreateRegisteredAddressSet(registeredMemoryAddresses);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (!IsSupportedDirectType(entry.TypeId))
                    report.AddError("UnsupportedDirectAssetType", key, "Sample catalog direct entry type is not supported: " + entry.TypeId + ".");

                if (!string.Equals(entry.ProviderId, MemoryProviderId, StringComparison.Ordinal))
                    report.AddError("UnexpectedProvider", key, "Sample catalog entry must use the memory provider for the editor bootstrap slice.");

                if (!entry.ProviderData.TryGetValue(ProviderDataAssetPathKey, out string assetPath) || string.IsNullOrWhiteSpace(assetPath))
                {
                    report.AddError("ProviderDataAssetPathMissing", key, "Memory sample catalog entry is missing providerData.assetPath.");
                }
                else
                {
                    if (assetPath.IndexOf("_TempImportedResources", StringComparison.Ordinal) >= 0)
                        report.AddError("TempImportedResourcePathForbidden", key, "Sample catalog entry must not point at _TempImportedResources: " + assetPath + ".");
                    if (IsFmodCatalogValue(assetPath))
                        report.AddError("FmodCatalogEntryForbidden", key, "FMOD bank/event values must not be ordinary Resource Catalog entries: " + assetPath + ".");
                }

                if (entry.Address.IndexOf("_TempImportedResources", StringComparison.Ordinal) >= 0)
                    report.AddError("TempImportedResourceAddressForbidden", key, "Sample catalog address must not point at _TempImportedResources.");
                if (IsFmodCatalogValue(entry.Address))
                    report.AddError("FmodCatalogEntryForbidden", key, "FMOD bank/event values must not be ordinary Resource Catalog entries: " + entry.Address + ".");

                if (registeredAddresses != null && !registeredAddresses.Contains(entry.Address))
                    report.AddError("MemoryAddressUnregistered", key, "Memory provider address is not registered for editor bootstrap: " + entry.Address + ".");
            }

            return report;
        }

        private static bool IsSupportedDirectType(string typeId)
        {
            return string.Equals(typeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Texture2D, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AudioClip, StringComparison.Ordinal);
        }

        private static bool IsFmodCatalogValue(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.EndsWith(".bank", StringComparison.OrdinalIgnoreCase)
                    || value.IndexOf("bank:/", StringComparison.OrdinalIgnoreCase) >= 0
                    || value.IndexOf("event:/", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static HashSet<string> CreateRegisteredAddressSet(IEnumerable<string> registeredMemoryAddresses)
        {
            if (registeredMemoryAddresses == null)
                return null;

            var addresses = new HashSet<string>(StringComparer.Ordinal);
            foreach (string address in registeredMemoryAddresses)
            {
                if (!string.IsNullOrWhiteSpace(address))
                    addresses.Add(address);
            }

            return addresses;
        }
    }

    public readonly struct TempImportedResourceCatalogAsset
    {
        public TempImportedResourceCatalogAsset(
            string id,
            string typeId,
            string address,
            string assetPath,
            IEnumerable<string> labels)
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? ResourceTypeIds.Object;
            Address = address ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Labels = labels != null ? new List<string>(labels) : new List<string>();
        }

        public string Id { get; }
        public string TypeId { get; }
        public string Address { get; }
        public string AssetPath { get; }
        public IReadOnlyList<string> Labels { get; }

        public ResourceCatalogEntry CreateEntry()
        {
            return new ResourceCatalogEntry(
                Id,
                TypeId,
                TempImportedResourceCatalog.MemoryProviderId,
                Address,
                variant: string.Empty,
                packageId: TempImportedResourceCatalog.PackageId,
                dependencies: Array.Empty<ResourceKey>(),
                labels: Labels,
                hash: string.Empty,
                size: 0,
                allowOverride: false,
                providerData: new Dictionary<string, string>
                {
                    { TempImportedResourceCatalog.ProviderDataAssetPathKey, AssetPath }
                });
        }
    }
}
