using MxFramework.Audio.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioStreamingAssetsLoaderTests
    {
        [TestCase("MxFramework/Audio/catalog.json", "MxFramework/Audio/catalog.json")]
        [TestCase("/MxFramework\\Audio\\catalog.json", "MxFramework/Audio/catalog.json")]
        [TestCase("Assets/StreamingAssets/MxFramework/Audio/catalog.json", "MxFramework/Audio/catalog.json")]
        public void Normalize_ProducesPlatformIndependentRelativePath(string source, string expected)
        {
            Assert.AreEqual(expected, AudioStreamingAssetsLoader.Normalize(source));
        }

        [Test]
        public void StartupArtifactPaths_AreStableAndComplete()
        {
            Assert.AreEqual(
                "MxFramework/Audio/audio_bank_manifest.json",
                AudioStartupArtifactLoader.BankManifestPath);
            Assert.AreEqual(
                "MxFramework/Audio/audio_runtime_catalog.json",
                AudioStartupArtifactLoader.RuntimeCatalogPath);
            Assert.AreEqual(
                "MxFramework/Audio/audio_physical_runtime_catalog.json",
                AudioStartupArtifactLoader.PhysicalCatalogPath);
        }
    }
}
