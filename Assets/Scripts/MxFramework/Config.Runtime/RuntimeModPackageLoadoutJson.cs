using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MxFramework.Config.Runtime
{
    public static class RuntimeModPackageLoadoutJson
    {
        private sealed class Dto
        {
            [JsonProperty("format")]
            public string Format { get; set; }

            [JsonProperty("profileId")]
            public string ProfileId { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("enabledPackageKeys")]
            public List<string> EnabledPackageKeys { get; set; }

            [JsonProperty("updatedUtc")]
            public string UpdatedUtc { get; set; }
        }

        public static RuntimeModPackageLoadout LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Loadout json is null or empty.");

            Dto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<Dto>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse loadout json: {ex.Message}", ex);
            }

            if (dto == null)
                throw new InvalidOperationException("Loadout json parsed to null.");

            if (!string.Equals(dto.Format, RuntimeModPackageLoadout.ExpectedFormat, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Loadout format mismatch. expected='{RuntimeModPackageLoadout.ExpectedFormat}', actual='{dto.Format ?? "(null)"}'.");
            }

            if (string.IsNullOrWhiteSpace(dto.ProfileId))
                throw new InvalidOperationException("Loadout profileId is required.");

            if (dto.EnabledPackageKeys == null)
                throw new InvalidOperationException("Loadout enabledPackageKeys is required.");

            return new RuntimeModPackageLoadout(
                profileId: dto.ProfileId,
                enabledPackageKeys: dto.EnabledPackageKeys,
                displayName: dto.DisplayName ?? string.Empty,
                format: dto.Format,
                updatedUtc: dto.UpdatedUtc ?? string.Empty);
        }

        public static string SaveToJson(RuntimeModPackageLoadout loadout)
        {
            if (loadout == null)
                throw new ArgumentNullException(nameof(loadout));

            if (!string.Equals(loadout.Format, RuntimeModPackageLoadout.ExpectedFormat, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported loadout format: {loadout.Format}");

            if (string.IsNullOrWhiteSpace(loadout.ProfileId))
                throw new InvalidOperationException("Loadout profileId is required.");

            var dto = new Dto
            {
                Format = RuntimeModPackageLoadout.ExpectedFormat,
                ProfileId = loadout.ProfileId,
                DisplayName = loadout.DisplayName,
                EnabledPackageKeys = new List<string>(loadout.EnabledPackageKeys ?? Array.Empty<string>()),
                UpdatedUtc = DateTime.UtcNow.ToString("O")
            };

            return JsonConvert.SerializeObject(dto, Formatting.Indented);
        }

        public static RuntimeModPackageLoadout LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Loadout file path is null or empty.");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Loadout file not found: {path}", path);
            return LoadFromJson(File.ReadAllText(path));
        }

        public static void SaveToFile(string path, RuntimeModPackageLoadout loadout)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Loadout file path is null or empty.");

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, SaveToJson(loadout));
        }
    }
}
