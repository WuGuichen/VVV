using System;
using System.Threading;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public sealed class EditorAssetDatabaseResourceProvider : IResourceProvider
    {
        public const string Id = "editorAssetDatabase";
        public const string AssetPathProviderDataKey = "assetPath";
        public const string SubAssetNameProviderDataKey = "subAssetName";
        public const string SubClipNameProviderDataKey = "subClipName";

        public string ProviderId => Id;

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && string.Equals(entry.ProviderId, Id, StringComparison.Ordinal)
                && IsProjectAssetPath(ResolveAssetPath(entry));
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            if (context == null || context.Entry == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    Id,
                    "Editor AssetDatabase load context is missing."));
            }

            string assetPath = ResolveAssetPath(context.Entry);
            if (!IsProjectAssetPath(assetPath))
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    context.Key,
                    Id,
                    "Editor AssetDatabase asset path must be an Assets/ project path.",
                    assetPath));
            }

            Type assetType = UnityResourceTypeResolver.Resolve(context.Entry.TypeId);
            if (TryGetSubAssetName(context.Entry, out string subAssetName))
                return LoadSubAsset(context, assetPath, assetType, subAssetName);

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(assetPath, assetType);
            if (asset == null)
            {
                return ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.NotFound,
                    context.Key,
                    Id,
                    "Editor AssetDatabase asset was not found.",
                    assetPath));
            }

            return ResourceLoadResult<object>.Loaded(asset);
        }

        public IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    context != null ? context.Key : default,
                    Id,
                    "Editor AssetDatabase operation was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
            // AssetDatabase assets are owned by the Editor import database.
        }

        public static string ResolveAssetPath(ResourceCatalogEntry entry)
        {
            if (TryGetProviderData(entry, AssetPathProviderDataKey, out string assetPath))
                return assetPath.Replace('\\', '/');

            return (entry != null ? entry.Address : string.Empty).Replace('\\', '/');
        }

        private static ResourceLoadResult<object> LoadSubAsset(
            ResourceLoadContext context,
            string assetPath,
            Type assetType,
            string subAssetName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                UnityEngine.Object asset = assets[i];
                if (asset == null || !string.Equals(asset.name, subAssetName, StringComparison.Ordinal))
                    continue;
                if (assetType != typeof(UnityEngine.Object) && !assetType.IsInstanceOfType(asset))
                    continue;

                return ResourceLoadResult<object>.Loaded(asset);
            }

            return ResourceLoadResult<object>.Failed(new ResourceError(
                ResourceErrorCode.NotFound,
                context.Key,
                Id,
                "Editor AssetDatabase sub-asset was not found.",
                assetPath + "#" + subAssetName));
        }

        private static bool TryGetSubAssetName(ResourceCatalogEntry entry, out string value)
        {
            if (TryGetProviderData(entry, SubAssetNameProviderDataKey, out value))
                return true;

            return TryGetProviderData(entry, SubClipNameProviderDataKey, out value);
        }

        private static bool TryGetProviderData(ResourceCatalogEntry entry, string key, out string value)
        {
            value = string.Empty;
            if (entry == null
                || entry.ProviderData == null
                || string.IsNullOrWhiteSpace(key)
                || !entry.ProviderData.TryGetValue(key, out value))
            {
                value = string.Empty;
                return false;
            }

            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsProjectAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.IndexOf("..", StringComparison.Ordinal) < 0;
        }
    }

    public sealed class EditorSceneAssetDatabaseResourceProvider : IResourceProvider
    {
        public const string Id = "editorSceneAssetDatabase";

        public string ProviderId => Id;

        public bool CanLoad(ResourceCatalogEntry entry)
        {
            return entry != null
                && string.Equals(entry.ProviderId, Id, StringComparison.Ordinal)
                && TryGetScenePath(entry, out _);
        }

        public ResourceLoadResult<object> Load(ResourceLoadContext context)
        {
            return ResourceLoadResult<object>.Failed(new ResourceError(
                ResourceErrorCode.ProviderFailed,
                context != null ? context.Key : default,
                Id,
                "Editor scene resources must be loaded through EditorSceneFlowDriver.",
                context != null && context.Entry != null ? context.Entry.Address : string.Empty));
        }

        public IResourceOperation<object> LoadAsync(ResourceLoadContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<object>(ResourceLoadResult<object>.Failed(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    context != null ? context.Key : default,
                    Id,
                    "Editor scene resource operation was cancelled.")));
            }

            return new ImmediateResourceOperation<object>(Load(context));
        }

        public void Release(ResourceReleaseContext context)
        {
        }

        public static bool TryGetScenePath(ResourceCatalogEntry entry, out string scenePath)
        {
            scenePath = EditorAssetDatabaseResourceProvider.ResolveAssetPath(entry);
            return !string.IsNullOrWhiteSpace(scenePath)
                && scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                && scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                && scenePath.IndexOf("..", StringComparison.Ordinal) < 0;
        }
    }
}
