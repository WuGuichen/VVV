using MxFramework.Audio;
using MxFramework.Audio.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioBankManifestJsonTests
    {
        [Test]
        public void TryLoad_ValidManifest_BuildsImmutableDefinitions()
        {
            const string json = @"{
  ""format"": ""mx.audioBankManifest.v1"",
  ""schemaVersion"": 1,
  ""platform"": ""Desktop"",
  ""fmodProjectSha256"": ""project"",
  ""audioCatalogSha256"": ""catalog"",
  ""audioManifestHash"": ""manifest"",
  ""audioSnapshotHash"": ""snapshot"",
  ""physicalCatalogSha256"": ""physical"",
  ""banks"": [{
    ""stableName"": ""Master"",
    ""fileName"": ""Master.bank"",
    ""sha256"": ""bank"",
    ""sizeBytes"": 128,
    ""required"": true,
    ""group"": ""bootstrap"",
    ""scope"": ""AppPersistent"",
    ""preloadSamples"": true
  }]
}";

            Assert.IsTrue(AudioBankManifestJson.TryLoad(json, out AudioBankManifest manifest, out string error), error);
            Assert.AreEqual("Desktop", manifest.Platform);
            Assert.AreEqual(1, manifest.Banks.Count);
            Assert.AreEqual(AudioBankScope.AppPersistent, manifest.Banks[0].Scope);
            Assert.IsTrue(manifest.Banks[0].PreloadSamples);
        }

        [Test]
        public void TryLoad_MissingLinkageOrInvalidBank_IsRejected()
        {
            const string json = @"{
  ""format"": ""mx.audioBankManifest.v1"",
  ""schemaVersion"": 1,
  ""platform"": ""Desktop"",
  ""banks"": []
}";

            Assert.IsFalse(AudioBankManifestJson.TryLoad(json, out _, out string error));
            Assert.IsNotEmpty(error);
        }
    }
}
