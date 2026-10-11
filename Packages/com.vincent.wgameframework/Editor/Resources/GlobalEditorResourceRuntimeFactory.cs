using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEditor;

namespace MxFramework.Editor
{
    [InitializeOnLoad]
    internal static class GlobalEditorResourceRuntimeBootstrap
    {
        static GlobalEditorResourceRuntimeBootstrap()
        {
            GlobalResourceRuntimeServices.RegisterEditorRuntimeFactory(new GlobalEditorResourceRuntimeFactory());
        }
    }

    public sealed class GlobalEditorResourceRuntimeFactory : IGlobalResourceEditorRuntimeFactory
    {
        public GlobalResourceRuntimeServices Create(GlobalResourceRuntimeOptions options)
        {
            options = options ?? new GlobalResourceRuntimeOptions();

            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            if (plan.HasErrors)
                throw new ResourceCatalogException(GlobalPlayerResourceBuildProfileBuilder.CreateReportText(plan.Report));

            ResourceCatalog catalog = GlobalPlayerResourceBuildProfileBuilder.CreateEditorAssetDatabaseCatalog(plan);
            GeneratedResourcePreloadGroupCatalog preloadGroups =
                GlobalPlayerResourceBuildProfileBuilder.CreatePreloadGroupCatalog(plan);

            var resourceManager = new ResourceManager();
            resourceManager.RegisterProvider(new EditorAssetDatabaseResourceProvider());
            resourceManager.RegisterProvider(new EditorSceneAssetDatabaseResourceProvider());
            resourceManager.AddCatalog(catalog);

            var providers = new List<IResourceProvider>();
            for (int i = 0; i < options.ExternalProviders.Count; i++)
                if (options.ExternalProviders[i] != null) providers.Add(options.ExternalProviders[i]);
            string streamingRoot = Path.GetFullPath("Assets/StreamingAssets");
            var providerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < providers.Count; i++) providerIds.Add(providers[i].ProviderId);
            if (providerIds.Add(AssetBundleProvider.ExternalId))
            {
                providers.Add(new AssetBundleProvider(
                    Path.Combine(streamingRoot, GlobalResourceRuntimeBootstrap.ExternalBundleRootRelativePath, plan.Compilation.Plan.BuildTarget),
                    AssetBundleProvider.ExternalId));
            }
            ResolvedResourceCatalogMountSet mounted = GeneratedResourceCatalogMountLoader.ResolveFromJson(
                GlobalPlayerResourceBuildProfileBuilder.CreateCatalogMountManifestJson(plan),
                streamingRoot,
                options.CompositionRootCatalogs,
                providerIds);
            if (!string.Equals(mounted.MappingInputFingerprint, plan.Compilation.Plan.MappingInputFingerprint, StringComparison.Ordinal) ||
                !string.Equals(mounted.BuildPlanFingerprint, plan.Compilation.Plan.BuildPlanFingerprint, StringComparison.Ordinal))
                throw new ResourceCatalogException("CatalogMount manifest does not belong to the compiled Editor plan.");
            GlobalResourceRuntimeBootstrap.RegisterMountedResources(resourceManager, providers, mounted);

            return new GlobalResourceRuntimeServices(
                GlobalResourceRuntimeMode.Editor,
                resourceManager,
                new ResourcePreloadService(resourceManager),
                preloadGroups,
                allowMissingPreloadGroupInEditor: options.AllowMissingPreloadGroupInEditor,
                sceneFlowDriver: new EditorSceneFlowDriver(resourceManager));
        }
    }
}
