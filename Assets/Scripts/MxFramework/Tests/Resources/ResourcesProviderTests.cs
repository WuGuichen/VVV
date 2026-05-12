using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class ResourcesProviderTests
    {
        private const string DemoKey = "demo.text.resources_provider";

        [SetUp]
        public void SetUp()
        {
            ResourcesProviderTestFixture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            ResourcesProviderTestFixture.Delete();
        }

        [Test]
        public void Load_TextAssetFromUnityResources_ReturnsHandle()
        {
            var provider = new ResourcesProvider();
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "resources.demo",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        DemoKey,
                        ResourceTypeIds.TextAsset,
                        ResourcesProvider.Id,
                        ResourcesProviderTestFixture.DemoAddress)
                }));

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey(DemoKey, ResourceTypeIds.TextAsset));

            Assert.IsTrue(result.Success, result.Error.Message);
            Assert.AreEqual(ResourcesProviderTestFixture.DemoText, result.Value.Value.text.Trim());
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

            manager.Release(result.Value);

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        [Test]
        public void Load_MissingUnityResourcesAsset_ReturnsNotFound()
        {
            var provider = new ResourcesProvider();
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "resources.demo",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.text.missing",
                        ResourceTypeIds.TextAsset,
                        ResourcesProvider.Id,
                        ResourcesProviderTestFixture.MissingAddress)
                }));

            ResourceLoadResult<ResourceHandle<TextAsset>> result = manager.Load<TextAsset>(new ResourceKey("demo.text.missing", ResourceTypeIds.TextAsset));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResourceErrorCode.NotFound, result.Error.Code);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
        }
    }
}
