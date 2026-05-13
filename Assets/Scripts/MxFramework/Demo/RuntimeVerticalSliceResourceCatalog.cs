using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public static class RuntimeVerticalSliceResourceCatalog
    {
        public const string CatalogId = "mxframework.runtime_vertical_slice";
        public const string PackageId = "mxframework.demo.runtime_vertical_slice";
        public const string WarmupGroupId = "runtime_vertical_slice";
        public const string WarmupLabel = "warmup.runtime_vertical_slice";
        public const string MemoryProviderId = "memory";
        public const string UiLabel = "ui";
        public const string ConfigLabel = "config";
        public const string ArtLabel = "art";
        public const string ProviderDataAssetPathKey = "assetPath";

        public static IReadOnlyList<RuntimeVerticalSliceResourceAsset> CreateRuntimeAssets(
            RuntimeVerticalSliceSceneConfig config,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Font font)
        {
            var assets = new List<RuntimeVerticalSliceResourceAsset>();
            AddAsset(assets, "mxframework.showcase.config.runtime_vertical_slice", "config/scene", config, ConfigLabel);
            AddAsset(assets, "mxframework.showcase.ui.panel_settings", "ui/panel_settings", panelSettings, UiLabel);
            AddAsset(assets, "mxframework.showcase.ui.visual_tree", "ui/visual_tree", visualTree, UiLabel);
            AddAsset(assets, "mxframework.showcase.ui.style_sheet", "ui/style_sheet", styleSheet, UiLabel);
            AddAsset(assets, "mxframework.showcase.ui.font", "ui/font", font, UiLabel);
            return assets;
        }

        public static ResourceCatalog CreateCatalog(IEnumerable<RuntimeVerticalSliceResourceAsset> assets)
        {
            var entries = new List<ResourceCatalogEntry>();
            if (assets != null)
            {
                foreach (RuntimeVerticalSliceResourceAsset asset in assets)
                {
                    if (asset.Asset == null)
                        continue;

                    entries.Add(asset.CreateEntry());
                }
            }

            return new ResourceCatalog(CatalogId, PackageId, entries);
        }

        public static MemoryResourceProvider CreateMemoryProvider(IEnumerable<RuntimeVerticalSliceResourceAsset> assets)
        {
            var provider = new MemoryResourceProvider();
            if (assets == null)
                return provider;

            foreach (RuntimeVerticalSliceResourceAsset asset in assets)
            {
                if (asset.Asset != null)
                    provider.Register(asset.Address, asset.Asset);
            }

            return provider;
        }

        public static string GetTypeId(Object asset)
        {
            if (asset is GameObject)
                return ResourceTypeIds.GameObject;
            if (asset is Texture2D)
                return ResourceTypeIds.Texture2D;
            if (asset is Sprite)
                return ResourceTypeIds.Sprite;
            if (asset is AudioClip)
                return ResourceTypeIds.AudioClip;
            if (asset is TextAsset)
                return ResourceTypeIds.TextAsset;
            if (asset is Material)
                return ResourceTypeIds.Material;
            if (asset is PanelSettings)
                return ResourceTypeIds.PanelSettings;
            if (asset is VisualTreeAsset)
                return ResourceTypeIds.VisualTreeAsset;
            if (asset is StyleSheet)
                return ResourceTypeIds.StyleSheet;
            if (asset is Font)
                return ResourceTypeIds.Font;
            if (asset is RuntimeVerticalSliceSceneConfig)
                return nameof(RuntimeVerticalSliceSceneConfig);
            return ResourceTypeIds.Object;
        }

        public static string CreateAddress(string suffix)
        {
            return "runtime_vertical_slice/" + (suffix ?? string.Empty).Trim('/');
        }

        private static void AddAsset(
            List<RuntimeVerticalSliceResourceAsset> assets,
            string id,
            string addressSuffix,
            Object asset,
            string label)
        {
            if (asset == null)
                return;

            assets.Add(new RuntimeVerticalSliceResourceAsset(
                id,
                GetTypeId(asset),
                CreateAddress(addressSuffix),
                asset,
                string.Empty,
                new[] { WarmupLabel, label }));
        }
    }

    public readonly struct RuntimeVerticalSliceResourceAsset
    {
        public RuntimeVerticalSliceResourceAsset(
            string id,
            string typeId,
            string address,
            Object asset,
            string assetPath,
            IEnumerable<string> labels)
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? ResourceTypeIds.Object;
            Address = address ?? string.Empty;
            Asset = asset;
            AssetPath = assetPath ?? string.Empty;
            Labels = labels != null ? new List<string>(labels) : new List<string>();
        }

        public string Id { get; }
        public string TypeId { get; }
        public string Address { get; }
        public Object Asset { get; }
        public string AssetPath { get; }
        public IReadOnlyList<string> Labels { get; }

        public ResourceCatalogEntry CreateEntry()
        {
            Dictionary<string, string> providerData = string.IsNullOrWhiteSpace(AssetPath)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { { RuntimeVerticalSliceResourceCatalog.ProviderDataAssetPathKey, AssetPath } };

            return new ResourceCatalogEntry(
                Id,
                TypeId,
                RuntimeVerticalSliceResourceCatalog.MemoryProviderId,
                Address,
                variant: string.Empty,
                packageId: RuntimeVerticalSliceResourceCatalog.PackageId,
                labels: Labels,
                providerData: providerData);
        }
    }
}
