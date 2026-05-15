using MxFramework.Demo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class RuntimeVerticalSliceSampleResourceTestTests
    {
        [Test]
        public void Run_CompletesSamplesResourceChainAndReleasesAllResources()
        {
            System.Func<string, Object> previousLoader = RuntimeVerticalSliceSampleResourceTest.AssetPathLoader;
            RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = AssetDatabase.LoadAssetAtPath<Object>;
            var resourceTest = new RuntimeVerticalSliceSampleResourceTest();

            try
            {
                RuntimeVerticalSliceSampleResourceTestResult result = resourceTest.Run();

                Assert.IsTrue(result.Success, result.FailureMessage + "\n" + string.Join("\n", result.LogLines));
                Assert.AreEqual(5, result.DirectPrefabCount);
                Assert.AreEqual(7, result.DirectTextureCount);
                Assert.AreEqual(4, result.DirectAudioClipCount);
                Assert.AreEqual(5, result.InstantiatedPrefabCount);
                Assert.AreEqual(5, result.DestroyedPrefabCount);

                Assert.NotNull(result.AfterWarmupSnapshot);
                Assert.AreEqual(16, result.AfterWarmupSnapshot.LoadedCount);
                Assert.AreEqual(40, result.AfterWarmupSnapshot.TotalRefCount);

                Assert.NotNull(result.AfterDirectLoadSnapshot);
                Assert.AreEqual(16, result.AfterDirectLoadSnapshot.LoadedCount);
                Assert.AreEqual(56, result.AfterDirectLoadSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterDirectLoadSnapshot.FailedCount);

                Assert.NotNull(result.AfterDirectReleaseSnapshot);
                Assert.AreEqual(16, result.AfterDirectReleaseSnapshot.LoadedCount);
                Assert.AreEqual(40, result.AfterDirectReleaseSnapshot.TotalRefCount);

                Assert.NotNull(result.AfterFullReleaseSnapshot);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.LoadedCount);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.FailedCount);

                Assert.AreEqual(16, result.ProviderLoadCount);
                Assert.AreEqual(16, result.ProviderReleaseCount);
                StringAssert.Contains("Samples warmup: package 16/16", string.Join("\n", result.LogLines));
                StringAssert.Contains("Samples direct: Katana=1, StatusAura prefabs=4", string.Join("\n", result.LogLines));
                StringAssert.Contains("fullRelease loaded=0 refs=0 failed=0", string.Join("\n", result.LogLines));
            }
            finally
            {
                resourceTest.Dispose();
                RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = previousLoader;
            }
        }
    }
}
