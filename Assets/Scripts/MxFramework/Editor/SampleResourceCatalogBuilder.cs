using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class SampleResourceCatalogBuilder
    {
        public const string CatalogPath = "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";
        public const string CatalogId = "mxframework.samples";
        public const string PackageId = "mxframework.samples";
        public const string MemoryProviderId = "memory";
        public const string ProviderDataAssetPathKey = "assetPath";

        private const string PackageLabel = "package.mxframework.samples";
        private const string DomainArtLabel = "domain.art";
        private const string DomainUiLabel = "domain.ui";
        private const string DomainVfxLabel = "domain.vfx";
        private const string DomainAudioLabel = "domain.audio";
        private const string SampleKatanaLabel = "sample.katana";
        private const string SampleSkeletonLabel = "sample.skeleton";
        private const string SampleSkeletonAnimationClipLabel = "sample.skeleton.animation_clip";
        private const string SampleStartScreenLabel = "sample.start_screen";
        private const string SampleStatusAurasLabel = "sample.status_auras";
        private const string SampleMagicEffectsLabel = "sample.magic_effects";
        private const string WarmupMxAnimationLabel = "warmup.demo.mxanimation";
        private const string WarmupStartScreenLabel = "warmup.demo.start_screen";
        private const string WarmupCombatLabel = "warmup.demo.combat";
        private const string WarmupStatusEffectsLabel = "warmup.demo.status_effects";
        private const string WarmupMagicEffectsLabel = "warmup.demo.magic_effects";

        private const string SkeletonAnimationRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips";
        private const string StatusAuraPrefabRoot = "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs";
        private const string StartScreenTextureRoot = "Assets/UI/MxFramework/Samples/StartScreen/Textures";
        private const string MagicEffectsRoot = "Assets/Audio/MxFramework/Samples/MagicEffects";

        private static readonly string[] SkeletonAnimationOrder =
        {
            "standing_idle",
            "standing_walk_forward",
            "standing_run_forward",
            "standing_jump",
            "standing_jump_running",
            "standing_jump_running_landing",
            "standing_land_to_standing_idle",
            "standing_run_back",
            "standing_run_left",
            "standing_run_right",
            "standing_sprint_forward",
            "standing_turn_left_90",
            "standing_turn_right_90",
            "standing_walk_back",
            "standing_walk_left",
            "standing_walk_right"
        };

        private static readonly SampleResourceSpec[] FixedPrefixSpecs =
        {
            new SampleResourceSpec(
                "art.weapon.katana.generic_01",
                ResourceTypeIds.GameObject,
                "mxframework.samples/art/weapon/katana/generic_01",
                "Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab",
                PackageLabel,
                DomainArtLabel,
                SampleKatanaLabel,
                WarmupCombatLabel),

            new SampleResourceSpec(
                "art.character.skeleton.model",
                ResourceTypeIds.GameObject,
                "mxframework.samples/art/character/skeleton/model",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/Models/Skeleton.fbx",
                PackageLabel,
                DomainArtLabel,
                SampleSkeletonLabel,
                WarmupMxAnimationLabel)
        };

        private static readonly SampleResourceSpec[] FixedMiddleSpecs =
        {
            new SampleResourceSpec(
                "art.character.skeleton.mask.upper_body",
                ResourceTypeIds.AvatarMask,
                "mxframework.samples/art/character/skeleton/mask/upper_body",
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/Masks/SkeletonUpperBody.mask",
                PackageLabel,
                DomainArtLabel,
                SampleSkeletonLabel,
                WarmupMxAnimationLabel)
        };

        private static readonly SampleResourceSpec[] StatusAuraSpecs =
        {
            new SampleResourceSpec(
                "vfx.status_aura.burn",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/burn",
                StatusAuraPrefabRoot + "/Aura_Burn.prefab",
                PackageLabel,
                DomainVfxLabel,
                SampleStatusAurasLabel,
                WarmupCombatLabel,
                WarmupStatusEffectsLabel),
            new SampleResourceSpec(
                "vfx.status_aura.lightning",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/lightning",
                StatusAuraPrefabRoot + "/Aura_Lightning.prefab",
                PackageLabel,
                DomainVfxLabel,
                SampleStatusAurasLabel,
                WarmupCombatLabel,
                WarmupStatusEffectsLabel),
            new SampleResourceSpec(
                "vfx.status_aura.smoke",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/smoke",
                StatusAuraPrefabRoot + "/Aura_Smoke.prefab",
                PackageLabel,
                DomainVfxLabel,
                SampleStatusAurasLabel,
                WarmupCombatLabel,
                WarmupStatusEffectsLabel),
            new SampleResourceSpec(
                "vfx.status_aura.stun",
                ResourceTypeIds.GameObject,
                "mxframework.samples/vfx/status_aura/stun",
                StatusAuraPrefabRoot + "/Stun.prefab",
                PackageLabel,
                DomainVfxLabel,
                SampleStatusAurasLabel,
                WarmupCombatLabel,
                WarmupStatusEffectsLabel)
        };

        private static readonly SampleResourceSpec[] StartScreenSpecs =
        {
            new SampleResourceSpec(
                "ui.start_screen.button.normal",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/button/normal",
                StartScreenTextureRoot + "/button_normal.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel),
            new SampleResourceSpec(
                "ui.start_screen.button.hover",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/button/hover",
                StartScreenTextureRoot + "/button_hover.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel),
            new SampleResourceSpec(
                "ui.start_screen.separator.diamond_line",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/separator/diamond_line",
                StartScreenTextureRoot + "/separator_diamond_line.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel),
            new SampleResourceSpec(
                "ui.start_screen.icon.archive_book",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/archive_book",
                StartScreenTextureRoot + "/icon_archive_book.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel),
            new SampleResourceSpec(
                "ui.start_screen.icon.continue_hourglass",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/continue_hourglass",
                StartScreenTextureRoot + "/icon_continue_hourglass.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel),
            new SampleResourceSpec(
                "ui.start_screen.icon.exit_door",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/exit_door",
                StartScreenTextureRoot + "/icon_exit_door.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel),
            new SampleResourceSpec(
                "ui.start_screen.icon.settings_cog",
                ResourceTypeIds.Texture2D,
                "mxframework.samples/ui/start_screen/icon/settings_cog",
                StartScreenTextureRoot + "/icon_settings_cog.png",
                PackageLabel,
                DomainUiLabel,
                SampleStartScreenLabel,
                WarmupStartScreenLabel)
        };

        private static readonly SampleResourceSpec[] MagicEffectSpecs =
        {
            new SampleResourceSpec(
                "audio.magic_effect.explosion_fire_1",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/explosion_fire_1",
                MagicEffectsRoot + "/explosion_fire_1.ogg",
                PackageLabel,
                DomainAudioLabel,
                SampleMagicEffectsLabel,
                WarmupCombatLabel,
                WarmupMagicEffectsLabel),
            new SampleResourceSpec(
                "audio.magic_effect.explosion_lightning_3",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/explosion_lightning_3",
                MagicEffectsRoot + "/explosion_lightning_3.wav",
                PackageLabel,
                DomainAudioLabel,
                SampleMagicEffectsLabel,
                WarmupCombatLabel,
                WarmupMagicEffectsLabel),
            new SampleResourceSpec(
                "audio.magic_effect.loop_fire_2",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/loop_fire_2",
                MagicEffectsRoot + "/loop_fire_2.ogg",
                PackageLabel,
                DomainAudioLabel,
                SampleMagicEffectsLabel,
                WarmupCombatLabel,
                WarmupMagicEffectsLabel),
            new SampleResourceSpec(
                "audio.magic_effect.wind",
                ResourceTypeIds.AudioClip,
                "mxframework.samples/audio/magic_effect/wind",
                MagicEffectsRoot + "/wind.wav",
                PackageLabel,
                DomainAudioLabel,
                SampleMagicEffectsLabel,
                WarmupCombatLabel,
                WarmupMagicEffectsLabel)
        };

        public static ResourceCatalog BuildCatalog()
        {
            var specs = new List<SampleResourceSpec>();
            Add(specs, FixedPrefixSpecs);
            AddSkeletonAnimationSpecs(specs);
            Add(specs, StatusAuraSpecs);
            Add(specs, StartScreenSpecs);
            Add(specs, MagicEffectSpecs);

            var entries = new List<ResourceCatalogEntry>(specs.Count);
            for (int i = 0; i < specs.Count; i++)
                entries.Add(specs[i].CreateEntry());

            return new ResourceCatalog(CatalogId, PackageId, entries);
        }

        public static ResourceCatalogValidationReport ValidateGeneratedCatalog(ResourceCatalog catalog)
        {
            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { MemoryProviderId });
            report.Merge(ValidateSampleCatalogRules(catalog));
            return report;
        }

        public static void Generate()
        {
            ResourceCatalog catalog = BuildCatalog();
            ResourceCatalogValidationReport report = ValidateGeneratedCatalog(catalog);
            if (report.HasErrors)
            {
                Debug.LogError(ResourceCatalogEditorValidator.CreateReportText(catalog, report));
                return;
            }

            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            File.WriteAllText(CatalogPath, WriteCatalogJson(catalog));
            AssetDatabase.ImportAsset(CatalogPath);
            Debug.Log("MxFramework sample resource catalog generated: " + CatalogPath);
        }

        public static string WriteCatalogJson(ResourceCatalog catalog)
        {
            var entries = new List<ResourceCatalogEntryDto>();
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = catalog.Entries[i];
                    entries.Add(new ResourceCatalogEntryDto
                    {
                        id = entry.Id,
                        type = entry.TypeId,
                        variant = entry.Variant,
                        packageId = entry.PackageId,
                        provider = entry.ProviderId,
                        address = entry.Address,
                        labels = Copy(entry.Labels),
                        dependencies = Copy(entry.Dependencies),
                        hash = entry.Hash,
                        size = entry.Size,
                        allowOverride = entry.AllowOverride,
                        providerData = new Dictionary<string, string>(entry.ProviderData)
                    });
                }
            }

            return JsonConvert.SerializeObject(new ResourceCatalogDto
            {
                schemaVersion = 1,
                catalogId = catalog != null ? catalog.CatalogId : string.Empty,
                packageId = catalog != null ? catalog.PackageId : string.Empty,
                entries = entries.ToArray()
            }, Formatting.Indented) + "\n";
        }

        private static void AddSkeletonAnimationSpecs(List<SampleResourceSpec> specs)
        {
            Dictionary<string, string> pathsByName = FindAssetsByFileName(SkeletonAnimationRoot, "t:AnimationClip", ".anim");
            for (int i = 0; i < SkeletonAnimationOrder.Length; i++)
            {
                if (i == 4)
                    Add(specs, FixedMiddleSpecs);

                string name = SkeletonAnimationOrder[i];
                if (!pathsByName.TryGetValue(name, out string path))
                    path = SkeletonAnimationRoot + "/" + name + ".anim";

                specs.Add(new SampleResourceSpec(
                    "art.character.skeleton.animation." + name,
                    ResourceTypeIds.AnimationClip,
                    "mxframework.samples/art/character/skeleton/animation/" + name,
                    path,
                    PackageLabel,
                    DomainArtLabel,
                    SampleSkeletonLabel,
                    SampleSkeletonAnimationClipLabel,
                    WarmupMxAnimationLabel));
            }

            var extraNames = new List<string>();
            foreach (KeyValuePair<string, string> pair in pathsByName)
            {
                if (Array.IndexOf(SkeletonAnimationOrder, pair.Key) >= 0)
                    continue;

                extraNames.Add(pair.Key);
            }

            extraNames.Sort(StringComparer.Ordinal);
            for (int i = 0; i < extraNames.Count; i++)
            {
                string name = extraNames[i];
                specs.Add(new SampleResourceSpec(
                    "art.character.skeleton.animation." + name,
                    ResourceTypeIds.AnimationClip,
                    "mxframework.samples/art/character/skeleton/animation/" + name,
                    pathsByName[name],
                    PackageLabel,
                    DomainArtLabel,
                    SampleSkeletonLabel,
                    SampleSkeletonAnimationClipLabel,
                    WarmupMxAnimationLabel));
            }
        }

        private static Dictionary<string, string> FindAssetsByFileName(string root, string filter, string extension)
        {
            var paths = new List<string>();
            if (AssetDatabase.IsValidFolder(root))
            {
                string[] guids = AssetDatabase.FindAssets(filter, new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < paths.Count; i++)
                result[Path.GetFileNameWithoutExtension(paths[i])] = paths[i];

            return result;
        }

        private static void Add(List<SampleResourceSpec> specs, SampleResourceSpec[] values)
        {
            for (int i = 0; i < values.Length; i++)
                specs.Add(values[i]);
        }

        private static ResourceCatalogValidationReport ValidateSampleCatalogRules(ResourceCatalog catalog)
        {
            var report = new ResourceCatalogValidationReport();
            if (catalog == null)
                return report;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (!IsSupportedSampleType(entry.TypeId))
                    report.AddError("UnsupportedSampleAssetType", key, "Sample catalog entry type is not supported: " + entry.TypeId + ".");

                if (!string.Equals(entry.ProviderId, MemoryProviderId, StringComparison.Ordinal))
                    report.AddError("UnexpectedProvider", key, "Sample catalog entry must use the memory provider for the editor bootstrap slice.");

                if (!entry.ProviderData.TryGetValue(ProviderDataAssetPathKey, out string assetPath) || string.IsNullOrWhiteSpace(assetPath))
                {
                    report.AddError("ProviderDataAssetPathMissing", key, "Sample catalog entry is missing providerData.assetPath.");
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
            }

            return report;
        }

        private static bool IsSupportedSampleType(string typeId)
        {
            return string.Equals(typeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Texture2D, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AudioClip, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal);
        }

        private static bool IsFmodCatalogValue(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.EndsWith(".bank", StringComparison.OrdinalIgnoreCase)
                    || value.IndexOf("bank:/", StringComparison.OrdinalIgnoreCase) >= 0
                    || value.IndexOf("event:/", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return new string[0];

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static ResourceKeyDto[] Copy(IReadOnlyList<ResourceKey> values)
        {
            if (values == null || values.Count == 0)
                return new ResourceKeyDto[0];

            var copy = new ResourceKeyDto[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                ResourceKey key = values[i];
                copy[i] = new ResourceKeyDto
                {
                    id = key.Id,
                    type = key.TypeId,
                    variant = key.Variant,
                    packageId = key.PackageId
                };
            }

            return copy;
        }

        private readonly struct SampleResourceSpec
        {
            public SampleResourceSpec(
                string id,
                string typeId,
                string address,
                string assetPath,
                params string[] labels)
            {
                Id = id ?? string.Empty;
                TypeId = typeId ?? ResourceTypeIds.Object;
                Address = address ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                Labels = labels ?? Array.Empty<string>();
            }

            private string Id { get; }
            private string TypeId { get; }
            private string Address { get; }
            private string AssetPath { get; }
            private IReadOnlyList<string> Labels { get; }

            public ResourceCatalogEntry CreateEntry()
            {
                return new ResourceCatalogEntry(
                    Id,
                    TypeId,
                    MemoryProviderId,
                    Address,
                    variant: string.Empty,
                    packageId: PackageId,
                    dependencies: Array.Empty<ResourceKey>(),
                    labels: Labels,
                    hash: string.Empty,
                    size: 0,
                    allowOverride: false,
                    providerData: new Dictionary<string, string>
                    {
                        { ProviderDataAssetPathKey, AssetPath }
                    });
            }
        }

        private sealed class ResourceCatalogDto
        {
            public int schemaVersion;
            public string catalogId;
            public string packageId;
            public ResourceCatalogEntryDto[] entries;
        }

        private sealed class ResourceCatalogEntryDto
        {
            public string id;
            public string type;
            public string variant;
            public string packageId;
            public string provider;
            public string address;
            public string[] labels;
            public ResourceKeyDto[] dependencies;
            public string hash;
            public long size;
            public bool allowOverride;
            public Dictionary<string, string> providerData;
        }

        private sealed class ResourceKeyDto
        {
            public string id;
            public string type;
            public string variant;
            public string packageId;
        }
    }
}
