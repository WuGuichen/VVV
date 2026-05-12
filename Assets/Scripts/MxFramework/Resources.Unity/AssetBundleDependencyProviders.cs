using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public interface IAssetBundleDependencyProvider
    {
        IReadOnlyList<string> GetDependencies(string bundleName);
    }

    public sealed class EmptyAssetBundleDependencyProvider : IAssetBundleDependencyProvider
    {
        public static readonly EmptyAssetBundleDependencyProvider Instance = new EmptyAssetBundleDependencyProvider();
        private static readonly string[] Empty = Array.Empty<string>();

        private EmptyAssetBundleDependencyProvider()
        {
        }

        public IReadOnlyList<string> GetDependencies(string bundleName)
        {
            return Empty;
        }
    }

    public sealed class AssetBundleManifestDependencyProvider : IAssetBundleDependencyProvider
    {
        private readonly AssetBundleManifest _manifest;
        private readonly Dictionary<string, string[]> _cache = new Dictionary<string, string[]>(StringComparer.Ordinal);

        public AssetBundleManifestDependencyProvider(AssetBundleManifest manifest)
        {
            _manifest = manifest;
        }

        public IReadOnlyList<string> GetDependencies(string bundleName)
        {
            if (_manifest == null || string.IsNullOrWhiteSpace(bundleName))
                return Array.Empty<string>();

            if (!_cache.TryGetValue(bundleName, out string[] dependencies))
            {
                dependencies = _manifest.GetAllDependencies(bundleName) ?? Array.Empty<string>();
                _cache[bundleName] = dependencies;
            }

            return dependencies;
        }
    }

    public sealed class DictionaryAssetBundleDependencyProvider : IAssetBundleDependencyProvider
    {
        private readonly Dictionary<string, string[]> _dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);

        public DictionaryAssetBundleDependencyProvider Register(string bundleName, params string[] dependencies)
        {
            _dependencies[bundleName ?? string.Empty] = dependencies ?? Array.Empty<string>();
            return this;
        }

        public IReadOnlyList<string> GetDependencies(string bundleName)
        {
            return _dependencies.TryGetValue(bundleName ?? string.Empty, out string[] dependencies)
                ? dependencies
                : Array.Empty<string>();
        }
    }
}
