using System;
using System.IO;
using Newtonsoft.Json;

namespace MxFramework.Audio.Config
{
    public static class AudioBankManifestJson
    {
        public const string Format = "mx.audioBankManifest.v1";

        /// <remarks>LowFreqAlloc during startup file I/O and parsing.</remarks>
        public static bool TryLoadFile(string path, out AudioBankManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            try
            {
                return TryLoad(File.ReadAllText(path), out manifest, out error);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <remarks>LowFreqAlloc during startup parsing.</remarks>
        public static bool TryLoad(string json, out AudioBankManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            try
            {
                Dto dto = JsonConvert.DeserializeObject<Dto>(json);
                if (dto == null || dto.Format != Format || dto.SchemaVersion != 1)
                    throw new JsonSerializationException("Audio Bank manifest format/schema is invalid.");
                if (string.IsNullOrWhiteSpace(dto.Platform)
                    || string.IsNullOrWhiteSpace(dto.AudioCatalogSha256)
                    || string.IsNullOrWhiteSpace(dto.AudioManifestHash)
                    || string.IsNullOrWhiteSpace(dto.AudioSnapshotHash))
                    throw new JsonSerializationException("Audio Bank manifest linkage is incomplete.");

                BankDto[] source = dto.Banks ?? Array.Empty<BankDto>();
                var banks = new AudioBankDefinition[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    BankDto bank = source[i];
                    if (string.IsNullOrWhiteSpace(bank.StableName)
                        || string.IsNullOrWhiteSpace(bank.FileName)
                        || string.IsNullOrWhiteSpace(bank.Sha256)
                        || bank.SizeBytes <= 0
                        || !Enum.TryParse(bank.Scope, out AudioBankScope scope))
                        throw new JsonSerializationException("Audio Bank entry is invalid at index " + i + ".");
                    banks[i] = new AudioBankDefinition(
                        bank.StableName,
                        bank.FileName,
                        bank.Sha256,
                        bank.SizeBytes,
                        bank.Required,
                        bank.Group,
                        scope,
                        bank.PreloadSamples);
                }
                if (banks.Length == 0)
                    throw new JsonSerializationException("Audio Bank manifest has no Banks.");

                manifest = new AudioBankManifest(
                    dto.Platform,
                    dto.FmodProjectSha256,
                    dto.AudioCatalogSha256,
                    dto.AudioManifestHash,
                    dto.AudioSnapshotHash,
                    dto.PhysicalCatalogSha256,
                    banks);
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private sealed class Dto
        {
            [JsonProperty("format")] public string Format { get; set; }
            [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
            [JsonProperty("platform")] public string Platform { get; set; }
            [JsonProperty("fmodProjectSha256")] public string FmodProjectSha256 { get; set; }
            [JsonProperty("audioCatalogSha256")] public string AudioCatalogSha256 { get; set; }
            [JsonProperty("audioManifestHash")] public string AudioManifestHash { get; set; }
            [JsonProperty("audioSnapshotHash")] public string AudioSnapshotHash { get; set; }
            [JsonProperty("physicalCatalogSha256")] public string PhysicalCatalogSha256 { get; set; }
            [JsonProperty("banks")] public BankDto[] Banks { get; set; }
        }

        private sealed class BankDto
        {
            [JsonProperty("stableName")] public string StableName { get; set; }
            [JsonProperty("fileName")] public string FileName { get; set; }
            [JsonProperty("sha256")] public string Sha256 { get; set; }
            [JsonProperty("sizeBytes")] public long SizeBytes { get; set; }
            [JsonProperty("required")] public bool Required { get; set; }
            [JsonProperty("group")] public string Group { get; set; }
            [JsonProperty("scope")] public string Scope { get; set; }
            [JsonProperty("preloadSamples")] public bool PreloadSamples { get; set; }
        }
    }
}
