using System.IO;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public sealed class GlobalResourceRuntimeBootstrapResult
    {
        public GlobalResourceRuntimeBootstrapResult(
            ResourceManager resourceManager,
            AssetBundleProvider assetBundleProvider,
            ResourceCatalog catalog,
            GeneratedResourcePreloadGroupCatalog preloadGroups,
            GeneratedAssetBundleDependencyManifest dependencyManifest)
        {
            ResourceManager = resourceManager;
            AssetBundleProvider = assetBundleProvider;
            Catalog = catalog;
            PreloadGroups = preloadGroups;
            DependencyManifest = dependencyManifest;
        }

        public ResourceManager ResourceManager { get; }
        public AssetBundleProvider AssetBundleProvider { get; }
        public ResourceCatalog Catalog { get; }
        public GeneratedResourcePreloadGroupCatalog PreloadGroups { get; }
        public GeneratedAssetBundleDependencyManifest DependencyManifest { get; }
    }

    public static class GlobalResourceRuntimeBootstrap
    {
        public const string CatalogRelativePath = "MxFramework/Resources/global_runtime_catalog.json";
        public const string PreloadGroupsRelativePath = "MxFramework/Resources/global_preload_groups.json";
        public const string BundleDependenciesRelativePath = "MxFramework/Resources/global_bundle_dependencies.json";
        public const string BundleRootRelativePath = "MxFramework/Resources/Bundles";

        public static GlobalResourceRuntimeBootstrapResult CreateFromStreamingAssets(string buildTargetName = "")
        {
            EnsureStreamingAssetsFileSystemSupported();
            string target = string.IsNullOrWhiteSpace(buildTargetName)
                ? GetDefaultBuildTargetName()
                : buildTargetName;

            ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromStreamingAssets(CatalogRelativePath);
            GeneratedResourcePreloadGroupCatalog preloadGroups = GeneratedResourcePreloadGroupLoader.LoadFromStreamingAssets(PreloadGroupsRelativePath);
            GeneratedAssetBundleDependencyManifest dependencyManifest = GeneratedAssetBundleDependencyManifestLoader.LoadFromStreamingAssets(BundleDependenciesRelativePath);
            ValidateBuildTarget(dependencyManifest, target);
            string bundleRoot = Path.Combine(Application.streamingAssetsPath, BundleRootRelativePath, target);
            return Create(catalog, preloadGroups, dependencyManifest, bundleRoot, target);
        }

        public static GlobalResourceRuntimeBootstrapResult Create(
            ResourceCatalog catalog,
            GeneratedResourcePreloadGroupCatalog preloadGroups,
            GeneratedAssetBundleDependencyManifest dependencyManifest,
            string bundleRootPath,
            string expectedBuildTargetName = "")
        {
            if (catalog == null)
                throw new ResourceCatalogException("Global runtime resource catalog is missing.");
            if (preloadGroups == null)
                throw new ResourceCatalogException("Global runtime preload groups are missing.");
            if (dependencyManifest == null)
                throw new ResourceCatalogException("Global runtime bundle dependency manifest is missing.");
            ValidateBuildTarget(dependencyManifest, expectedBuildTargetName);

            var manager = new ResourceManager();
            var provider = new AssetBundleProvider(bundleRootPath, dependencyManifest.CreateDependencyProvider());
            manager.RegisterProvider(provider);
            manager.AddCatalog(catalog);

            return new GlobalResourceRuntimeBootstrapResult(
                manager,
                provider,
                catalog,
                preloadGroups,
                dependencyManifest);
        }

        private static void EnsureStreamingAssetsFileSystemSupported()
        {
#if UNITY_ANDROID || UNITY_WEBGL
            throw new ResourceCatalogException("Global resource runtime bootstrap currently requires file-system StreamingAssets. Android and WebGL need an async UnityWebRequest bootstrap path.");
#endif
        }

        private static void ValidateBuildTarget(GeneratedAssetBundleDependencyManifest manifest, string buildTargetName)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.BuildTarget) || string.IsNullOrWhiteSpace(buildTargetName))
                return;

            if (!string.Equals(manifest.BuildTarget, buildTargetName, System.StringComparison.Ordinal))
            {
                throw new ResourceCatalogException(
                    "Global resource bundle dependency manifest build target '" + manifest.BuildTarget +
                    "' does not match requested build target '" + buildTargetName + "'.");
            }
        }

        private static string GetDefaultBuildTargetName()
        {
#if UNITY_STANDALONE_OSX
            return "StandaloneOSX";
#elif UNITY_STANDALONE_WIN
            return "StandaloneWindows64";
#elif UNITY_STANDALONE_LINUX
            return "StandaloneLinux64";
#elif UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#else
            return Application.platform.ToString();
#endif
        }
    }
}
