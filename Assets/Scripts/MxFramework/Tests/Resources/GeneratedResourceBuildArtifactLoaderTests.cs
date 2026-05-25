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
    }
}
