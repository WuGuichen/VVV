using System;
using System.Collections.Generic;
using MxFramework.Editor;
using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class CharacterTestWorldObjectContentProjection
    {
        private readonly ResourceCatalog _resourceCatalog;

        public CharacterTestWorldObjectContentProjection()
        {
            _resourceCatalog = SampleResourceCatalogBuilder.BuildCatalog();
        }

        public bool TryResolvePrefab(
            ResourceKey key,
            out GameObject prefab,
            out string diagnostic)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(key.Id))
            {
                diagnostic = "定义尚未绑定 Prefab ResourceKey。";
                return false;
            }

            ResourceCatalogEntry entry = FindEntry(key);
            if (entry == null)
            {
                diagnostic = "Resource Catalog 中不存在资源：" + key;
                return false;
            }

            if (!string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
            {
                diagnostic = "资源类型不是 GameObject：" + entry.TypeId;
                return false;
            }

            if (entry.ProviderData == null
                || !entry.ProviderData.TryGetValue(
                    SampleResourceCatalogBuilder.ProviderDataAssetPathKey,
                    out string assetPath)
                || string.IsNullOrWhiteSpace(assetPath))
            {
                diagnostic = "资源目录条目缺少 editor assetPath：" + key;
                return false;
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                prefab = null;
                diagnostic = "ResourceKey 未解析到有效 Prefab：" + key;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public Texture2D ResolveThumbnail(ResourceKey key)
        {
            return TryResolvePrefab(key, out GameObject prefab, out _)
                ? AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab)
                : null;
        }

        public IReadOnlyList<ResourceCatalogEntry> FindPrefabEntries(string search, string category)
        {
            string normalizedSearch = (search ?? string.Empty).Trim();
            string normalizedCategory = (category ?? string.Empty).Trim();
            var entries = new List<ResourceCatalogEntry>();
            IReadOnlyList<ResourceCatalogEntry> source = _resourceCatalog?.Entries;
            for (int i = 0; source != null && i < source.Count; i++)
            {
                ResourceCatalogEntry entry = source[i];
                if (entry == null
                    || !string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedSearch)
                    && entry.Id.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Address.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedCategory)
                    && !ContainsLabel(entry.Labels, normalizedCategory))
                {
                    continue;
                }

                entries.Add(entry);
            }

            return entries;
        }

        private ResourceCatalogEntry FindEntry(ResourceKey key)
        {
            IReadOnlyList<ResourceCatalogEntry> entries = _resourceCatalog?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ResourceCatalogEntry entry = entries[i];
                if (entry == null)
                    continue;
                ResourceKey candidate = entry.CreateKey(_resourceCatalog.PackageId);
                if (candidate.Equals(key))
                    return entry;
            }

            return null;
        }

        private static bool ContainsLabel(IReadOnlyList<string> labels, string expected)
        {
            for (int i = 0; labels != null && i < labels.Count; i++)
            {
                if (string.Equals(labels[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
