using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class GeneratedResourceBuildArtifactLoaderTests
    {
        [Test]
        public void PreloadGroupLoader_CreatesPlansFromGeneratedJson()
        {
            const string json = @"{
  ""schemaVersion"": 1,
  ""profileId"": ""global.default"",
  ""catalogId"": ""global.runtime"",
  ""groups"": [
    {
      ""id"": ""boot.base"",
      ""explicitKeys"": [
        { ""id"": ""ui.startup.button.normal"", ""type"": ""Texture2D"", ""variant"": """", ""packageId"": """" }
      ],
      ""labels"": [""preload.boot.base""],
      ""failFast"": true,
      ""maxConcurrentLoads"": 4
    }
  ]
}";

            GeneratedResourcePreloadGroupCatalog catalog = GeneratedResourcePreloadGroupLoader.LoadFromJson(json);

            Assert.AreEqual("global.default", catalog.ProfileId);
            Assert.AreEqual("global.runtime", catalog.CatalogId);
            Assert.IsTrue(catalog.TryGetPlan("boot.base", out ResourcePreloadPlan plan));
            Assert.AreEqual(1, plan.ExplicitKeys.Count);
            Assert.AreEqual("ui.startup.button.normal", plan.ExplicitKeys[0].Id);
            Assert.AreEqual("preload.boot.base", plan.Labels[0]);
            Assert.IsTrue(plan.FailFast);
            Assert.AreEqual(4, plan.MaxConcurrentLoads);
        }

        [Test]
        public void DependencyManifestLoader_CreatesDictionaryProvider()
        {
            const string json = @"{
  ""schemaVersion"": 1,
  ""profileId"": ""global.default"",
  ""buildTarget"": ""StandaloneOSX"",
  ""bundles"": [
    { ""bundleName"": ""global.ui.startup"", ""dependencies"": [""global.shared""] }
  ]
}";

            GeneratedAssetBundleDependencyManifest manifest = GeneratedAssetBundleDependencyManifestLoader.LoadFromJson(json);
            DictionaryAssetBundleDependencyProvider provider = manifest.CreateDependencyProvider();

            Assert.AreEqual("global.default", manifest.ProfileId);
            Assert.AreEqual("StandaloneOSX", manifest.BuildTarget);
            Assert.AreEqual(1, provider.GetDependencies("global.ui.startup").Count);
            Assert.AreEqual("global.shared", provider.GetDependencies("global.ui.startup")[0]);
            Assert.AreEqual(0, provider.GetDependencies("missing").Count);
        }

        [Test]
        public void GlobalRuntimeBootstrap_RegistersProviderCatalogAndPreloadGroups()
        {
            var catalog = new ResourceCatalog(
                "global.runtime",
                "mxframework.samples",
                new[]
                {
                    new ResourceCatalogEntry(
                        "ui.start_screen.button.normal",
                        ResourceTypeIds.Texture2D,
                        AssetBundleProvider.Id,
                        "global.ui.start_screen.assetbundle|Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png",
                        labels: new[] { "preload.boot.base" })
                });
            var preloadGroups = GeneratedResourcePreloadGroupLoader.LoadFromJson(@"{
  ""schemaVersion"": 1,
  ""profileId"": ""global.default"",
  ""catalogId"": ""global.runtime"",
  ""groups"": [
    { ""id"": ""boot.base"", ""labels"": [""preload.boot.base""], ""explicitKeys"": [], ""failFast"": true, ""maxConcurrentLoads"": 4 }
  ]
}");
            var dependencies = GeneratedAssetBundleDependencyManifestLoader.LoadFromJson(@"{
  ""schemaVersion"": 1,
  ""profileId"": ""global.default"",
  ""buildTarget"": ""StandaloneOSX"",
  ""bundles"": [
    { ""bundleName"": ""global.ui.start_screen.assetbundle"", ""dependencies"": [] }
  ]
}");

            GlobalResourceRuntimeBootstrapResult result = GlobalResourceRuntimeBootstrap.Create(catalog, preloadGroups, dependencies, "Temp/MxFrameworkGlobalResourceBootstrapTests");

            Assert.NotNull(result.ResourceManager);
            Assert.NotNull(result.AssetBundleProvider);
            Assert.AreEqual(catalog, result.Catalog);
            Assert.IsTrue(result.PreloadGroups.TryGetPlan("boot.base", out ResourcePreloadPlan plan));
            Assert.AreEqual("preload.boot.base", plan.Labels[0]);
            Assert.IsTrue(result.ResourceManager.Contains(new ResourceKey("ui.start_screen.button.normal", ResourceTypeIds.Texture2D, packageId: "mxframework.samples")));
            Assert.AreEqual(AssetBundleProvider.Id, result.Catalog.Entries[0].ProviderId);

            var preloadService = new ResourcePreloadService(result.ResourceManager);
            IResourceOperation<ResourcePreloadResult> operation = preloadService.PreloadAsync(plan);
            ResourceLoadResult<ResourcePreloadResult> preload = operation.Result;
            Assert.IsTrue(preload.Success, preload.Error.Message);
            Assert.IsFalse(preload.Value.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, preload.Value.Errors[0].Code);
            Assert.AreEqual(0, result.AssetBundleProvider.LoadedBundleCount);
        }

        [Test]
        public void GlobalRuntimeBootstrap_WhenManifestBuildTargetMismatches_FailsFast()
        {
            var catalog = new ResourceCatalog("global.runtime", string.Empty, System.Array.Empty<ResourceCatalogEntry>());
            var preloadGroups = new GeneratedResourcePreloadGroupCatalog("global.default", "global.runtime", System.Array.Empty<ResourcePreloadPlan>());
            var dependencies = GeneratedAssetBundleDependencyManifestLoader.LoadFromJson(@"{
  ""schemaVersion"": 1,
  ""profileId"": ""global.default"",
  ""buildTarget"": ""StandaloneWindows64"",
  ""bundles"": []
}");

            Assert.Throws<ResourceCatalogException>(() =>
                GlobalResourceRuntimeBootstrap.Create(catalog, preloadGroups, dependencies, "Temp/MxFrameworkGlobalResourceBootstrapTests", "StandaloneOSX"));
        }
    }
}
