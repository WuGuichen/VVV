using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class StreamingResourceCatalogLoaderTests
    {
        [Test]
        public void LoadFromStreamingAssets_ParsesCatalog()
        {
            ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromStreamingAssets("MxFramework/ResourcesDemo/catalog_m3.json");

            Assert.AreEqual("mxframework.resources.m3.demo", catalog.CatalogId);
            Assert.AreEqual("mxframework.demo", catalog.PackageId);
            Assert.AreEqual(1, catalog.Entries.Count);
            Assert.AreEqual("demo.text.bundle_main", catalog.Entries[0].Id);
            Assert.AreEqual(AssetBundleProvider.Id, catalog.Entries[0].ProviderId);
        }

        [Test]
        public void LoadFromJson_ParsesEntryFieldsAndPackageFallback()
        {
            ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromJson(
                "{"
                + "\"schemaVersion\":1,"
                + "\"catalogId\":\"demo.catalog\","
                + "\"packageId\":\"demo.package\","
                + "\"entries\":[{"
                + "\"id\":\"demo.text.bundle\","
                + "\"type\":\"TextAsset\","
                + "\"variant\":\"zh_CN_android_hd\","
                + "\"provider\":\"assetBundle\","
                + "\"address\":\"main|asset.txt\","
                + "\"labels\":[\"demo\",\"m3\"],"
                + "\"dependencies\":[{\"id\":\"demo.dep\",\"type\":\"TextAsset\",\"variant\":\"\",\"packageId\":\"dep.package\"}],"
                + "\"hash\":\"abc\","
                + "\"size\":42,"
                + "\"allowOverride\":true,"
                + "\"providerData\":{\"bundleName\":\"main\",\"cacheKey\":\"demo.main.v1\"}"
                + "}]"
                + "}");

            ResourceCatalogEntry entry = catalog.Entries[0];
            Assert.AreEqual("demo.package", entry.PackageId);
            Assert.AreEqual("zh_CN_android_hd", entry.Variant);
            Assert.AreEqual(2, entry.Labels.Count);
            Assert.AreEqual(1, entry.Dependencies.Count);
            Assert.AreEqual("dep.package", entry.Dependencies[0].PackageId);
            Assert.AreEqual("abc", entry.Hash);
            Assert.AreEqual(42, entry.Size);
            Assert.IsTrue(entry.AllowOverride);
            Assert.AreEqual(2, entry.ProviderData.Count);
            Assert.AreEqual("main", entry.ProviderData["bundleName"]);
            Assert.AreEqual("demo.main.v1", entry.ProviderData["cacheKey"]);
        }

        [Test]
        public void LoadFromJson_WhenProviderDataMissing_UsesEmptyDictionary()
        {
            ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromJson(
                "{"
                + "\"schemaVersion\":1,"
                + "\"catalogId\":\"demo.catalog\","
                + "\"packageId\":\"demo.package\","
                + "\"entries\":[{"
                + "\"id\":\"demo.text.bundle\","
                + "\"type\":\"TextAsset\","
                + "\"variant\":\"\","
                + "\"provider\":\"assetBundle\","
                + "\"address\":\"main|asset.txt\""
                + "}]"
                + "}");

            Assert.IsNotNull(catalog.Entries[0].ProviderData);
            Assert.AreEqual(0, catalog.Entries[0].ProviderData.Count);
        }

        [Test]
        public void LoadFromJson_WhenSchemaUnsupported_Throws()
        {
            Assert.Throws<ResourceCatalogException>(() => StreamingResourceCatalogLoader.LoadFromJson("{\"schemaVersion\":2,\"entries\":[]}"));
        }
    }
}
