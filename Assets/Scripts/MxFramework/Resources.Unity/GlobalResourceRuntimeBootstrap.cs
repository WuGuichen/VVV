using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
    public enum GlobalResourceRuntimeMode
    {
        Editor = 0,
        Offline = 1,
        Online = 2
    }

    public sealed class GlobalResourceRuntimeOptions
    {
        public GlobalResourceRuntimeMode Mode { get; set; } = GlobalResourceRuntimeMode.Editor;
        public string BuildTargetName { get; set; } = string.Empty;
        public bool AllowMissingPreloadGroupInEditor { get; set; } = true;
    }

    public sealed class GlobalResourceRuntimeServices : IDisposable
    {
        private readonly List<ResourceGroupHandle> _preloadedGroups = new List<ResourceGroupHandle>();
        private bool _disposed;

        public GlobalResourceRuntimeServices(
            GlobalResourceRuntimeMode mode,
            ResourceManager resourceManager,
            ResourcePreloadService preloadService,
            GeneratedResourcePreloadGroupCatalog preloadGroups = null,
            string bootstrapErrorMessage = "",
            bool allowMissingPreloadGroupInEditor = true)
        {
            Mode = mode;
            ResourceManager = resourceManager ?? new ResourceManager();
            PreloadService = preloadService ?? new ResourcePreloadService(ResourceManager);
            PreloadGroups = preloadGroups;
            BootstrapErrorMessage = bootstrapErrorMessage ?? string.Empty;
            AllowMissingPreloadGroupInEditor = allowMissingPreloadGroupInEditor;
        }

        public GlobalResourceRuntimeMode Mode { get; }
        public ResourceManager ResourceManager { get; }
        public ResourcePreloadService PreloadService { get; }
        public GeneratedResourcePreloadGroupCatalog PreloadGroups { get; }
        public string BootstrapErrorMessage { get; }
        public bool HasBootstrapError => !string.IsNullOrWhiteSpace(BootstrapErrorMessage);
        public bool AllowMissingPreloadGroupInEditor { get; }

        public static GlobalResourceRuntimeServices Create(GlobalResourceRuntimeOptions options = null)
        {
            options = options ?? new GlobalResourceRuntimeOptions();
            switch (options.Mode)
            {
                case GlobalResourceRuntimeMode.Editor:
                    return CreateEditor(options);
                case GlobalResourceRuntimeMode.Offline:
                    return CreateOffline(options);
                case GlobalResourceRuntimeMode.Online:
                    return CreateUnavailable(
                        GlobalResourceRuntimeMode.Online,
                        "Online global resource runtime mode is reserved for future remote catalog/bootstrap work.",
                        options);
                default:
                    return CreateEditor(options);
            }
        }

        public bool TryPreloadGroup(string groupId, out ResourcePreloadResult result, out string errorMessage)
        {
            result = null;
            errorMessage = string.Empty;

            if (HasBootstrapError)
            {
                errorMessage = BootstrapErrorMessage;
                return false;
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                if (Mode == GlobalResourceRuntimeMode.Editor && AllowMissingPreloadGroupInEditor)
                    return true;

                errorMessage = "Resource preload group id is missing.";
                return false;
            }

            if (PreloadGroups == null || !PreloadGroups.TryGetPlan(groupId, out ResourcePreloadPlan plan))
            {
                if (Mode == GlobalResourceRuntimeMode.Editor && AllowMissingPreloadGroupInEditor)
                    return true;

                errorMessage = "Resource preload group was not found: " + groupId + ".";
                return false;
            }

            ResourceLoadResult<ResourcePreloadResult> preload = PreloadService.PreloadAsync(plan).Result;
            if (!preload.Success || preload.Value == null)
            {
                errorMessage = preload.Error.Message;
                return false;
            }

            result = preload.Value;
            if (!result.Success)
            {
                errorMessage = "Resource preload group completed with errors: " + result.FailedCount + ".";
                return false;
            }

            if (result.Handle != null)
                _preloadedGroups.Add(result.Handle);

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = _preloadedGroups.Count - 1; i >= 0; i--)
                PreloadService.ReleaseGroup(_preloadedGroups[i]);

            _preloadedGroups.Clear();
            _disposed = true;
        }

        private static GlobalResourceRuntimeServices CreateEditor(GlobalResourceRuntimeOptions options)
        {
            var resourceManager = new ResourceManager();
            return new GlobalResourceRuntimeServices(
                GlobalResourceRuntimeMode.Editor,
                resourceManager,
                new ResourcePreloadService(resourceManager),
                allowMissingPreloadGroupInEditor: options.AllowMissingPreloadGroupInEditor);
        }

        private static GlobalResourceRuntimeServices CreateOffline(GlobalResourceRuntimeOptions options)
        {
            try
            {
                GlobalResourceRuntimeBootstrapResult bootstrap =
                    GlobalResourceRuntimeBootstrap.CreateFromStreamingAssets(options.BuildTargetName);
                return new GlobalResourceRuntimeServices(
                    GlobalResourceRuntimeMode.Offline,
                    bootstrap.ResourceManager,
                    new ResourcePreloadService(bootstrap.ResourceManager),
                    bootstrap.PreloadGroups,
                    allowMissingPreloadGroupInEditor: options.AllowMissingPreloadGroupInEditor);
            }
            catch (Exception ex) when (ex is ResourceCatalogException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return CreateUnavailable(GlobalResourceRuntimeMode.Offline, ex.Message, options);
            }
        }

        private static GlobalResourceRuntimeServices CreateUnavailable(
            GlobalResourceRuntimeMode mode,
            string errorMessage,
            GlobalResourceRuntimeOptions options)
        {
            var resourceManager = new ResourceManager();
            return new GlobalResourceRuntimeServices(
                mode,
                resourceManager,
                new ResourcePreloadService(resourceManager),
                bootstrapErrorMessage: errorMessage,
                allowMissingPreloadGroupInEditor: options.AllowMissingPreloadGroupInEditor);
        }
    }

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
