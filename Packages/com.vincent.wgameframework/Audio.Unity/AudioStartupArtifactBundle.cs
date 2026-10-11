using System;
using System.Collections;

namespace MxFramework.Audio.Unity
{
    public sealed class AudioStartupArtifactBundle
    {
        public string BankManifestJson { get; internal set; } = string.Empty;
        public string RuntimeCatalogJson { get; internal set; } = string.Empty;
        public string PhysicalCatalogJson { get; internal set; } = string.Empty;
        public string Error { get; internal set; } = string.Empty;
        public bool Success => string.IsNullOrEmpty(Error);
    }

    /// <summary>Loads the linked audio startup artifact set before runtime composition.</summary>
    public static class AudioStartupArtifactLoader
    {
        public const string BankManifestPath = "MxFramework/Audio/audio_bank_manifest.json";
        public const string RuntimeCatalogPath = "MxFramework/Audio/audio_runtime_catalog.json";
        public const string PhysicalCatalogPath = "MxFramework/Audio/audio_physical_runtime_catalog.json";

        /// <remarks>AllocByDesign application startup path.</remarks>
        public static IEnumerator Load(Action<AudioStartupArtifactBundle> completed)
        {
            if (completed == null)
                throw new ArgumentNullException(nameof(completed));

            var bundle = new AudioStartupArtifactBundle();
            AudioTextLoadResult result = default;
            yield return AudioStreamingAssetsLoader.LoadText(BankManifestPath, value => result = value);
            if (!result.Success)
            {
                bundle.Error = result.Error;
                completed(bundle);
                yield break;
            }
            bundle.BankManifestJson = result.Text;

            yield return AudioStreamingAssetsLoader.LoadText(RuntimeCatalogPath, value => result = value);
            if (!result.Success)
            {
                bundle.Error = result.Error;
                completed(bundle);
                yield break;
            }
            bundle.RuntimeCatalogJson = result.Text;

            yield return AudioStreamingAssetsLoader.LoadText(PhysicalCatalogPath, value => result = value);
            if (!result.Success)
            {
                bundle.Error = result.Error;
                completed(bundle);
                yield break;
            }
            bundle.PhysicalCatalogJson = result.Text;
            completed(bundle);
        }
    }
}
