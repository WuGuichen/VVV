using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Runtime;
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
        private readonly List<IResourceProvider> _externalProviders = new List<IResourceProvider>();
        private readonly List<ResourceCatalog> _compositionRootCatalogs = new List<ResourceCatalog>();

        public GlobalResourceRuntimeMode Mode { get; set; } = GlobalResourceRuntimeMode.Editor;
        public string BuildTargetName { get; set; } = string.Empty;
        public bool AllowMissingPreloadGroupInEditor { get; set; } = true;
        public IList<IResourceProvider> ExternalProviders => _externalProviders;
        public IList<ResourceCatalog> CompositionRootCatalogs => _compositionRootCatalogs;
    }

    public interface IGlobalResourceEditorRuntimeFactory
    {
        GlobalResourceRuntimeServices Create(GlobalResourceRuntimeOptions options);
    }

    public sealed class GlobalResourceRuntimeServices : IDisposable
    {
        private static IGlobalResourceEditorRuntimeFactory _editorRuntimeFactory;

        private readonly List<ResourceGroupHandle> _preloadedGroups = new List<ResourceGroupHandle>();
        private readonly List<IDisposable> _ownedProviders = new List<IDisposable>();
        private readonly ResourceSceneFlowDriver _ownedSceneFlowDriver;
        private bool _disposed;

        public GlobalResourceRuntimeServices(
            GlobalResourceRuntimeMode mode,
            ResourceManager resourceManager,
            ResourcePreloadService preloadService,
            GeneratedResourcePreloadGroupCatalog preloadGroups = null,
            string bootstrapErrorMessage = "",
            bool allowMissingPreloadGroupInEditor = true,
            ISceneFlowDriver sceneFlowDriver = null)
            : this(mode, resourceManager, preloadService, false, sceneFlowDriver, preloadGroups,
                bootstrapErrorMessage, allowMissingPreloadGroupInEditor)
        {
        }

        public GlobalResourceRuntimeServices(
            GlobalResourceRuntimeMode mode,
            ResourceManager resourceManager,
            ResourcePreloadService preloadService,
            bool ownsSceneFlowDriver,
            ISceneFlowDriver sceneFlowDriver,
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
            SceneFlowDriver = sceneFlowDriver;
            if (ownsSceneFlowDriver && sceneFlowDriver != null && !(sceneFlowDriver is ResourceSceneFlowDriver))
                throw new ArgumentException("Owned scene retirement requires a ResourceSceneFlowDriver.", nameof(sceneFlowDriver));
            _ownedSceneFlowDriver = ownsSceneFlowDriver ? sceneFlowDriver as ResourceSceneFlowDriver : null;
            SceneFlowController = sceneFlowDriver != null ? new SceneFlowController(sceneFlowDriver) : null;
        }

        public GlobalResourceRuntimeMode Mode { get; }
        public ResourceManager ResourceManager { get; }
        public ResourcePreloadService PreloadService { get; }
        public GeneratedResourcePreloadGroupCatalog PreloadGroups { get; }
        public string BootstrapErrorMessage { get; }
        public bool HasBootstrapError => !string.IsNullOrWhiteSpace(BootstrapErrorMessage);
        public bool AllowMissingPreloadGroupInEditor { get; }
        public ISceneFlowDriver SceneFlowDriver { get; }
        public SceneFlowController SceneFlowController { get; }
        public bool SceneRetirementComplete => _ownedSceneFlowDriver == null || _ownedSceneFlowDriver.IsRetired;
        public SceneFlowError SceneRetirementError => _ownedSceneFlowDriver != null ? _ownedSceneFlowDriver.RetirementError : SceneFlowError.None;

        public static IGlobalResourceEditorRuntimeFactory EditorRuntimeFactory => _editorRuntimeFactory;

        /// <summary>Registers a session-owned provider; disposes it after loaded handles are released.</summary>
        /// <remarks>LowFreqAlloc; composition only. Shared providers use ResourceManager.RegisterProvider.</remarks>
        public void RegisterOwnedProvider(IResourceProvider provider)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GlobalResourceRuntimeServices));
            ResourceManager.RegisterProvider(provider);
            if (provider is IDisposable disposable && !_ownedProviders.Contains(disposable))
                _ownedProviders.Add(disposable);
        }

        public static void RegisterEditorRuntimeFactory(IGlobalResourceEditorRuntimeFactory factory)
        {
            _editorRuntimeFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static void ClearEditorRuntimeFactory(IGlobalResourceEditorRuntimeFactory factory = null)
        {
            if (factory == null || ReferenceEquals(_editorRuntimeFactory, factory))
                _editorRuntimeFactory = null;
        }

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
            {
                _ownedSceneFlowDriver?.Dispose();
                return;
            }

            _disposed = true;
            var errors = new List<Exception>();
            try { _ownedSceneFlowDriver?.Dispose(); }
            catch (Exception exception) { errors.Add(exception); }
            for (int i = _preloadedGroups.Count - 1; i >= 0; i--)
            {
                try { PreloadService.ReleaseGroup(_preloadedGroups[i]); }
                catch (Exception exception) { errors.Add(exception); }
            }

            _preloadedGroups.Clear();
            try { ResourceManager.ReleaseAllLoaded("runtimeDispose"); }
            catch (Exception exception) { errors.Add(exception); }
            for (int i = _ownedProviders.Count - 1; i >= 0; i--)
            {
                try { _ownedProviders[i].Dispose(); }
                catch (Exception exception) { errors.Add(exception); }
            }
            _ownedProviders.Clear();
            if (errors.Count > 0) throw new AggregateException("Owned resource provider cleanup failed.", errors);
        }

        private static GlobalResourceRuntimeServices CreateEditor(GlobalResourceRuntimeOptions options)
        {
            IGlobalResourceEditorRuntimeFactory factory = _editorRuntimeFactory;
            if (factory != null)
            {
                try
                {
                    GlobalResourceRuntimeServices editorServices = factory.Create(options);
                    if (editorServices != null)
                        return editorServices;
                }
                catch (Exception ex) when (ex is ResourceCatalogException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    return CreateUnavailable(GlobalResourceRuntimeMode.Editor, ex.Message, options);
                }
            }

            var resourceManager = new ResourceManager();
            if (options.CompositionRootCatalogs.Count != 0)
                return CreateUnavailable(GlobalResourceRuntimeMode.Editor, "Composition-root catalogs require the Unity Editor resource factory so they can be checked against the compiled CatalogMount allowlist.", options);
            GlobalResourceRuntimeBootstrap.RegisterProvidersAndCatalogs(
                resourceManager, options.ExternalProviders, Array.Empty<ResourceCatalog>());
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
                string buildTargetName = options.BuildTargetName;
                if (!Application.isEditor)
                {
                    string playerTarget = ResolvePlayerBuildTargetName(Application.platform);
                    if (!string.IsNullOrWhiteSpace(buildTargetName)
                        && !string.Equals(buildTargetName, playerTarget, StringComparison.Ordinal))
                        throw new ResourceCatalogException("Requested Offline resource target " + buildTargetName
                            + " does not match the running Player target " + playerTarget + ".");
                    buildTargetName = playerTarget;
                }
                GlobalResourceRuntimeBootstrapResult bootstrap =
                    GlobalResourceRuntimeBootstrap.CreateFromStreamingAssets(
                        buildTargetName,
                        options.ExternalProviders,
                        options.CompositionRootCatalogs);
                return new GlobalResourceRuntimeServices(
                    GlobalResourceRuntimeMode.Offline,
                    bootstrap.ResourceManager,
                    new ResourcePreloadService(bootstrap.ResourceManager),
                    preloadGroups: bootstrap.PreloadGroups,
                    allowMissingPreloadGroupInEditor: options.AllowMissingPreloadGroupInEditor,
                    sceneFlowDriver: bootstrap.SceneAssetBundleProvider != null
                        ? new ResourceSceneFlowDriver(bootstrap.ResourceManager, bootstrap.SceneAssetBundleProvider)
                        : null,
                    ownsSceneFlowDriver: true);
            }
            catch (Exception ex) when (ex is ResourceCatalogException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return CreateUnavailable(GlobalResourceRuntimeMode.Offline, ex.Message, options);
            }
        }

        internal static string ResolvePlayerBuildTargetName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer: return "StandaloneWindows64";
                case RuntimePlatform.OSXPlayer: return "StandaloneOSX";
                case RuntimePlatform.LinuxPlayer: return "StandaloneLinux64";
                default: throw new ResourceCatalogException(
                    "Offline resource build target is not configured for Player platform " + platform + ".");
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
            SceneAssetBundleProvider sceneAssetBundleProvider,
            ResourceCatalog catalog,
            GeneratedResourcePreloadGroupCatalog preloadGroups,
            GeneratedAssetBundleDependencyManifest dependencyManifest)
        {
            ResourceManager = resourceManager;
            AssetBundleProvider = assetBundleProvider;
            SceneAssetBundleProvider = sceneAssetBundleProvider;
            Catalog = catalog;
            PreloadGroups = preloadGroups;
            DependencyManifest = dependencyManifest;
        }

        public ResourceManager ResourceManager { get; }
        public AssetBundleProvider AssetBundleProvider { get; }
        public SceneAssetBundleProvider SceneAssetBundleProvider { get; }
        public ResourceCatalog Catalog { get; }
        public GeneratedResourcePreloadGroupCatalog PreloadGroups { get; }
        public GeneratedAssetBundleDependencyManifest DependencyManifest { get; }
    }

    public static class GlobalResourceRuntimeBootstrap
    {
        public const string CatalogRelativePath = "MxFramework/Resources/global_runtime_catalog.json";
        public const string ArtifactSetRelativePath = "MxFramework/Resources/global_resource_artifact_set.json";
        public const string CatalogMountsRelativePath = "MxFramework/Resources/global_catalog_mounts.json";
        public const string ExternalBundleRootRelativePath = "MxFramework/Resources/ExternalBundles";
        public const string PreloadGroupsRelativePath = "MxFramework/Resources/global_preload_groups.json";
        public const string BundleDependenciesRelativePath = "MxFramework/Resources/global_bundle_dependencies.json";
        public const string BundleRootRelativePath = "MxFramework/Resources/Bundles";

        public static GlobalResourceRuntimeBootstrapResult CreateFromStreamingAssets(
            string buildTargetName = "",
            IEnumerable<IResourceProvider> externalProviders = null,
            IEnumerable<ResourceCatalog> compositionRootCatalogs = null)
        {
            EnsureStreamingAssetsFileSystemSupported();
            GeneratedResourceArtifactSet artifactSet = GeneratedResourceArtifactSetLoader.LoadFromStreamingAssets(ArtifactSetRelativePath);
            string target = string.IsNullOrWhiteSpace(buildTargetName) ? artifactSet.BuildTarget : buildTargetName;
            if (!string.Equals(target, artifactSet.BuildTarget, StringComparison.Ordinal))
                throw new ResourceCatalogException("Generated resource artifact-set build target does not match requested target.");

            string root = Application.streamingAssetsPath;
            ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromFile(Path.Combine(root, artifactSet.GetRequiredRelativePath(GeneratedResourceArtifactSetLoader.CatalogKind)));
            GeneratedResourcePreloadGroupCatalog preloadGroups = GeneratedResourcePreloadGroupLoader.LoadFromFile(Path.Combine(root, artifactSet.GetRequiredRelativePath(GeneratedResourceArtifactSetLoader.PreloadGroupsKind)));
            GeneratedAssetBundleDependencyManifest dependencyManifest = GeneratedAssetBundleDependencyManifestLoader.LoadFromFile(Path.Combine(root, artifactSet.GetRequiredRelativePath(GeneratedResourceArtifactSetLoader.BundleDependenciesKind)));
            ValidateArtifactIdentity(artifactSet, catalog, preloadGroups, dependencyManifest);
            ValidateBuildTarget(dependencyManifest, target);
            string bundleRoot = Path.Combine(Application.streamingAssetsPath, BundleRootRelativePath, target);
            string externalBundleRoot = Path.Combine(Application.streamingAssetsPath, ExternalBundleRootRelativePath, target);

            List<IResourceProvider> mergedExternalProviders = CopyProviders(externalProviders) ?? new List<IResourceProvider>();
            var availableProviderIds = new List<string> { AssetBundleProvider.ExternalId };
            for (int i = 0; i < mergedExternalProviders.Count; i++)
                if (mergedExternalProviders[i] != null) availableProviderIds.Add(mergedExternalProviders[i].ProviderId);
            string mountsPath = artifactSet.GetRequiredRelativePath(GeneratedResourceArtifactSetLoader.CatalogMountsKind);
            ResolvedResourceCatalogMountSet mounted = GeneratedResourceCatalogMountLoader.ResolveFromJson(
                File.ReadAllText(Path.Combine(root, mountsPath)), root, compositionRootCatalogs, availableProviderIds);
            if (!string.Equals(mounted.MappingInputFingerprint, artifactSet.MappingInputFingerprint, StringComparison.Ordinal) ||
                !string.Equals(mounted.BuildPlanFingerprint, artifactSet.BuildPlanFingerprint, StringComparison.Ordinal))
                throw new ResourceCatalogException("Generated CatalogMount artifact belongs to a different build generation.");
            if (UsesProvider(mounted.Catalogs, AssetBundleProvider.ExternalId) && !ContainsProvider(mergedExternalProviders, AssetBundleProvider.ExternalId))
            {
                mergedExternalProviders.Add(new AssetBundleProvider(externalBundleRoot, AssetBundleProvider.ExternalId));
            }

            return Create(
                catalog,
                preloadGroups,
                dependencyManifest,
                bundleRoot,
                target,
                mergedExternalProviders,
                mounted);
        }

        public static GlobalResourceRuntimeBootstrapResult Create(
            ResourceCatalog catalog,
            GeneratedResourcePreloadGroupCatalog preloadGroups,
            GeneratedAssetBundleDependencyManifest dependencyManifest,
            string bundleRootPath,
            string expectedBuildTargetName = "",
            IEnumerable<IResourceProvider> externalProviders = null,
            ResolvedResourceCatalogMountSet mountedCatalogs = null)
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
            SceneAssetBundleProvider sceneProvider = UsesProvider(catalog, SceneAssetBundleProvider.Id)
                ? new SceneAssetBundleProvider(bundleRootPath, dependencyManifest.CreateDependencyProvider())
                : null;
            List<IResourceProvider> externalProviderList = CopyProviders(externalProviders);
            IReadOnlyList<ResourceCatalog> externalCatalogList = mountedCatalogs != null ? mountedCatalogs.Catalogs : Array.Empty<ResourceCatalog>();
            manager.RegisterProvider(provider);
            if (sceneProvider != null)
                manager.RegisterProvider(sceneProvider);
            RegisterExternalResourceProviders(
                manager,
                externalProviderList);
            manager.AddCatalog(catalog);
            AddExternalCatalogs(manager, externalCatalogList);

            return new GlobalResourceRuntimeBootstrapResult(
                manager,
                provider,
                sceneProvider,
                catalog,
                preloadGroups,
                dependencyManifest);
        }

        internal static void RegisterProvidersAndCatalogs(
            ResourceManager manager,
            IEnumerable<IResourceProvider> externalProviders,
            IEnumerable<ResourceCatalog> externalCatalogs)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            List<IResourceProvider> providerList = CopyProviders(externalProviders);
            List<ResourceCatalog> catalogList = CopyCatalogs(externalCatalogs) ?? new List<ResourceCatalog>();
            RegisterExternalResourceProviders(manager, providerList);
            AddExternalCatalogs(manager, catalogList);
        }

        /// <remarks>AllocByDesign. Registers only catalogs already resolved through the generated CatalogMount allowlist.</remarks>
        public static void RegisterMountedResources(
            ResourceManager manager,
            IEnumerable<IResourceProvider> externalProviders,
            ResolvedResourceCatalogMountSet mountedCatalogs)
        {
            RegisterProvidersAndCatalogs(manager, externalProviders,
                mountedCatalogs != null ? mountedCatalogs.Catalogs : Array.Empty<ResourceCatalog>());
        }

        private static void RegisterExternalResourceProviders(
            ResourceManager manager,
            IEnumerable<IResourceProvider> externalProviders)
        {
            if (externalProviders != null)
            {
                foreach (IResourceProvider externalProvider in externalProviders)
                {
                    if (externalProvider == null)
                        continue;
                    manager.RegisterProvider(externalProvider);
                }
            }

        }

        private static void AddExternalCatalogs(
            ResourceManager manager,
            IEnumerable<ResourceCatalog> externalCatalogs)
        {
            if (externalCatalogs == null)
                return;

            foreach (ResourceCatalog externalCatalog in externalCatalogs)
            {
                if (externalCatalog != null) manager.AddCatalog(externalCatalog);
            }
        }

        private static List<IResourceProvider> CopyProviders(IEnumerable<IResourceProvider> providers)
        {
            if (providers == null)
                return null;

            var result = new List<IResourceProvider>();
            foreach (IResourceProvider provider in providers)
                result.Add(provider);
            return result;
        }

        private static bool ContainsProvider(IEnumerable<IResourceProvider> providers, string providerId)
        {
            if (providers == null || string.IsNullOrWhiteSpace(providerId))
                return false;

            foreach (IResourceProvider provider in providers)
            {
                if (provider != null && string.Equals(provider.ProviderId, providerId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool UsesProvider(IEnumerable<ResourceCatalog> catalogs, string providerId)
        {
            if (catalogs == null || string.IsNullOrWhiteSpace(providerId))
                return false;

            foreach (ResourceCatalog catalog in catalogs)
            {
                if (catalog == null)
                    continue;
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = catalog.Entries[i];
                    if (entry != null && string.Equals(entry.ProviderId, providerId, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool UsesProvider(ResourceCatalog catalog, string providerId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(providerId))
                return false;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry != null && string.Equals(entry.ProviderId, providerId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static List<ResourceCatalog> CopyCatalogs(IEnumerable<ResourceCatalog> catalogs)
        {
            if (catalogs == null)
                return null;

            var result = new List<ResourceCatalog>();
            foreach (ResourceCatalog catalog in catalogs)
                result.Add(catalog);
            return result;
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

        private static void ValidateArtifactIdentity(
            GeneratedResourceArtifactSet set,
            ResourceCatalog catalog,
            GeneratedResourcePreloadGroupCatalog preloadGroups,
            GeneratedAssetBundleDependencyManifest dependencies)
        {
            if (!string.Equals(set.CatalogId, catalog.CatalogId, StringComparison.Ordinal) ||
                !string.Equals(set.ProfileId, preloadGroups.ProfileId, StringComparison.Ordinal) ||
                !string.Equals(set.CatalogId, preloadGroups.CatalogId, StringComparison.Ordinal) ||
                !string.Equals(set.ProfileId, dependencies.ProfileId, StringComparison.Ordinal) ||
                !string.Equals(set.BuildTarget, dependencies.BuildTarget, StringComparison.Ordinal) ||
                !string.Equals(set.MappingInputFingerprint, preloadGroups.MappingInputFingerprint, StringComparison.Ordinal) ||
                !string.Equals(set.BuildPlanFingerprint, preloadGroups.BuildPlanFingerprint, StringComparison.Ordinal) ||
                !string.Equals(set.MappingInputFingerprint, dependencies.MappingInputFingerprint, StringComparison.Ordinal) ||
                !string.Equals(set.BuildPlanFingerprint, dependencies.BuildPlanFingerprint, StringComparison.Ordinal))
                throw new ResourceCatalogException("Generated resource artifacts do not belong to one committed build generation.");
        }

    }
}
