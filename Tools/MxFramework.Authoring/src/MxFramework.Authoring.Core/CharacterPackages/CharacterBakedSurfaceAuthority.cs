using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>Validates the immutable Blender baked surface receipt before import.</summary>
    public static class CharacterBakedSurfaceAuthority
    {
        public const string RelativePath = "config/character_baked_surface_binding.json";
        public const string Format = "mx.characterBakedSurfaceBinding.v1";

        public static bool Exists(string root) => File.Exists(Path.Combine(root, RelativePath));

        public static List<CharacterPaletteUsageGateIssue> Validate(string root, string packageId)
        {
            var result = new List<CharacterPaletteUsageGateIssue>();
            try
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, RelativePath)));
                JsonElement d = json.RootElement;
                if (d.GetProperty("format").GetString() != Format || d.GetProperty("schemaVersion").GetString() != "1.0"
                    || !d.GetProperty("enabled").GetBoolean() || d.GetProperty("packageId").GetString() != packageId
                    || d.GetProperty("uvLayout").GetString() != "bakeUv0HairFlowUv1HairMetaUv2"
                    || d.GetProperty("surfaceDataLayout").GetString() != "aoRimEmissionProfile16")
                    throw new InvalidDataException("Invalid baked surface format, owner or channel layout.");
                int size = d.GetProperty("textureSize").GetInt32();
                if (size != 512 && size != 1024 && size != 2048 && size != 4096) throw new InvalidDataException("Invalid baked surface texture size.");
                foreach (string stem in new[] { "baseColor", "surfaceData", "model" })
                {
                    string file = ResolveFile(root, d.GetProperty(stem + "File").GetString());
                    using SHA256 hash = SHA256.Create();
                    string actual = "sha256:" + BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
                    if (actual != d.GetProperty(stem + "Hash").GetString()) throw new InvalidDataException("Baked resource hash differs: " + stem);
                }
                JsonElement hair = d.GetProperty("hair");
                foreach (string field in new[] { "primaryWidth", "primaryStrength", "secondaryWidth", "secondaryStrength", "primaryShift", "secondaryShift", "rootDarkening", "rimStrength", "aoStrength" })
                {
                    double value = hair.GetProperty(field).GetDouble();
                    double minimum = field.EndsWith("Shift", StringComparison.Ordinal) ? -1 : field.EndsWith("Width", StringComparison.Ordinal) ? .02 : 0;
                    double maximum = field.EndsWith("Strength", StringComparison.Ordinal) && field != "aoStrength" ? 2 : 1;
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                        throw new InvalidDataException("Invalid hair parameter: " + field);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
            {
                result.Add(new CharacterPaletteUsageGateIssue { Code = "BAKED_SURFACE_INVALID", Message = ex.Message, SourcePath = RelativePath });
            }
            return result;
        }

        public static string ResolveFile(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Baked path must be package relative.");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(prefix, relative));
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new InvalidDataException("Baked file is missing or outside package: " + relative);
            return full;
        }
    }
}
