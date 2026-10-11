using System;
using System.Collections.Generic;

namespace MxFramework.Audio
{
    public enum AudioBankScope
    {
        Scene = 0,
        AppPersistent = 1
    }

    public readonly struct AudioBankDefinition
    {
        public AudioBankDefinition(
            string stableName,
            string fileName,
            string sha256,
            long sizeBytes,
            bool required,
            string group,
            AudioBankScope scope,
            bool preloadSamples)
        {
            StableName = stableName ?? string.Empty;
            FileName = fileName ?? string.Empty;
            Sha256 = sha256 ?? string.Empty;
            SizeBytes = sizeBytes;
            Required = required;
            Group = group ?? string.Empty;
            Scope = scope;
            PreloadSamples = preloadSamples;
        }

        public string StableName { get; }
        public string FileName { get; }
        public string Sha256 { get; }
        public long SizeBytes { get; }
        public bool Required { get; }
        public string Group { get; }
        public AudioBankScope Scope { get; }
        public bool PreloadSamples { get; }
    }

    /// <summary>Immutable build-time linkage between runtime catalogs and platform Banks.</summary>
    public sealed class AudioBankManifest
    {
        private readonly IReadOnlyList<AudioBankDefinition> _banks;

        /// <remarks>LowFreqAlloc during startup construction.</remarks>
        public AudioBankManifest(
            string platform,
            string fmodProjectSha256,
            string audioCatalogSha256,
            string audioManifestHash,
            string audioSnapshotHash,
            string physicalCatalogSha256,
            AudioBankDefinition[] banks)
        {
            Platform = platform ?? string.Empty;
            FmodProjectSha256 = fmodProjectSha256 ?? string.Empty;
            AudioCatalogSha256 = audioCatalogSha256 ?? string.Empty;
            AudioManifestHash = audioManifestHash ?? string.Empty;
            AudioSnapshotHash = audioSnapshotHash ?? string.Empty;
            PhysicalCatalogSha256 = physicalCatalogSha256 ?? string.Empty;
            _banks = Array.AsReadOnly(banks == null ? Array.Empty<AudioBankDefinition>() : (AudioBankDefinition[])banks.Clone());
        }

        public string Platform { get; }
        public string FmodProjectSha256 { get; }
        public string AudioCatalogSha256 { get; }
        public string AudioManifestHash { get; }
        public string AudioSnapshotHash { get; }
        public string PhysicalCatalogSha256 { get; }
        public IReadOnlyList<AudioBankDefinition> Banks => _banks;
    }
}
