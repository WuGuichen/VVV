using MxFramework.Demo;
using MxFramework.Editor;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class RuntimeVerticalSlicePlayerResourceTestTests
    {
        [Test]
        public void Contract_UsesPlayerStreamingAssetBundleResourcePath()
        {
            Assert.AreEqual(
                "MxFramework/Samples/mxframework_samples_player_catalog.json",
                RuntimeVerticalSlicePlayerResourceTest.DefaultCatalogRelativePath);
            Assert.AreEqual(
                "MxFramework/Samples/Bundles",
                RuntimeVerticalSlicePlayerResourceTest.DefaultBundleRootRelativePath);
            Assert.AreEqual(
                "ui.start_screen.button.normal",
                RuntimeVerticalSlicePlayerResourceTest.ExpectedResourceId);
            Assert.AreEqual(
                ResourceTypeIds.Texture2D,
                RuntimeVerticalSlicePlayerResourceTest.ExpectedTextureKey.TypeId);
            Assert.AreEqual(
                "warmup.demo.start_screen",
                RuntimeVerticalSlicePlayerResourceTest.ExpectedWarmupLabel);
        }

        [Test]
        public void Run_WhenStreamingAssetsFixtureMissing_ReturnsBlockedResult()
        {
            using (var resourceTest = new RuntimeVerticalSlicePlayerResourceTest(
                "MxFramework/Samples/missing_player_catalog.json",
                "MxFramework/Samples/MissingBundles"))
            {
                RuntimeVerticalSlicePlayerResourceTestResult result = resourceTest.Run();

                Assert.IsFalse(result.Success);
                Assert.IsFalse(result.FixtureAvailable);
                StringAssert.Contains("Player resource StreamingAssets fixture is missing", result.FailureMessage);
            }
        }

        [Test]
        public void Run_CompletesPlayerStreamingAssetBundleSmokeAndReleasesAllResources()
        {
            if (!RuntimeVerticalSlicePlayerResourceTest.DefaultFixtureExists())
                SamplePlayerResourceCatalogBuilder.Generate();
            Assert.IsTrue(RuntimeVerticalSlicePlayerResourceTest.DefaultFixtureExists());

            using (var resourceTest = new RuntimeVerticalSlicePlayerResourceTest())
            {
                RuntimeVerticalSlicePlayerResourceTestResult result = resourceTest.Run();
                string log = string.Join("\n", result.LogLines);

                Assert.IsTrue(result.Success, result.FailureMessage + "\n" + log);
                Assert.IsTrue(result.FixtureAvailable);
                Assert.AreEqual(1, result.WarmupRequestedCount);
                Assert.AreEqual(1, result.WarmupLoadedCount);
                Assert.AreEqual(0, result.WarmupFailedCount);
                Assert.IsTrue(result.DirectTextureLoaded);

                Assert.NotNull(result.AfterWarmupSnapshot);
                Assert.AreEqual(1, result.AfterWarmupSnapshot.LoadedCount);
                Assert.AreEqual(1, result.AfterWarmupSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterWarmupSnapshot.FailedCount);

                Assert.NotNull(result.AfterDirectLoadSnapshot);
                Assert.AreEqual(1, result.AfterDirectLoadSnapshot.LoadedCount);
                Assert.AreEqual(2, result.AfterDirectLoadSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterDirectLoadSnapshot.FailedCount);

                Assert.NotNull(result.AfterDirectReleaseSnapshot);
                Assert.AreEqual(1, result.AfterDirectReleaseSnapshot.LoadedCount);
                Assert.AreEqual(1, result.AfterDirectReleaseSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterDirectReleaseSnapshot.FailedCount);

                Assert.NotNull(result.AfterFullReleaseSnapshot);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.LoadedCount);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.FailedCount);

                Assert.AreEqual(1, result.LoadedBundleCountAfterWarmup);
                Assert.AreEqual(1, result.LoadedBundleCountAfterDirectLoad);
                Assert.AreEqual(0, result.LoadedBundleCountAfterFullRelease);
                StringAssert.Contains("Player resources warmup: warmup.demo.start_screen 1/1", log);
                StringAssert.Contains("Player resources direct: ui.start_screen.button.normal Texture2D", log);
                StringAssert.Contains("fullRelease loaded=0 refs=0 failed=0", log);
            }
        }
    }
}
