using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class WGameSurfacePaletteCatalog
    {
        internal const string Format = "mx.wgameSurfacePaletteCatalog.v3";
        internal const string SchemaVersion = "3.0";
        internal const string ReceiptFormat = "mx.wgameColorPaletteAtlasReceipt.v3";
        internal const string ReceiptSchemaVersion = "3.0";
        internal const string CatalogProjectPath = "ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json";
        internal const int Width = 256;
        internal const int Height = 256;
        internal const int ColorCount = Width * Height;
        internal const int SurfaceProfileCount = 16;
        internal const int LogicalTokenCount = ColorCount * SurfaceProfileCount;
        internal const int RedBits = 5;
        internal const int GreenBits = 6;
        internal const int BlueBits = 5;
        internal const int RedLevels = 1 << RedBits;
        internal const int GreenLevels = 1 << GreenBits;
        internal const int BlueLevels = 1 << BlueBits;
        internal static readonly Vector2 InvalidPaletteUv = new Vector2(-1f, -1f);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal static WGameSurfacePaletteCatalogDocument LoadAndValidate(out string issue)
        {
            issue = string.Empty;
            string path = Path.GetFullPath(CatalogProjectPath);
            if (!File.Exists(path))
            {
                issue = "Color Palette catalog is missing: " + CatalogProjectPath + ".";
                return null;
            }
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                WGameSurfacePaletteCatalogDocument document = JsonSerializer.Deserialize<WGameSurfacePaletteCatalogDocument>(json, JsonOptions);
                if (document == null)
                {
                    issue = "Color Palette catalog is empty.";
                    return null;
                }
                string computedHash = ComputeCatalogHash(json);
                if (!string.Equals(document.CatalogHash, computedHash, StringComparison.Ordinal))
                {
                    issue = "Color Palette catalogHash mismatch. Stored=" + document.CatalogHash + ", computed=" + computedHash + ".";
                    return null;
                }
                if (!ValidateDocument(document, out issue))
                    return null;
                return document;
            }
            catch (Exception ex)
            {
                issue = "Color Palette catalog could not be read: " + ex.Message;
                return null;
            }
        }

        internal static Color32[] BuildPixels(WGameSurfacePaletteCatalogDocument document)
        {
            byte[] rgb = DecodeRgb8(document);
            var pixels = new Color32[ColorCount];
            for (int index = 0; index < pixels.Length; index++)
            {
                int offset = index * 3;
                pixels[index] = new Color32(rgb[offset], rgb[offset + 1], rgb[offset + 2], 255);
            }
            return pixels;
        }

        internal static byte[] DecodeRgb8(WGameSurfacePaletteCatalogDocument document)
        {
            byte[] bytes = Convert.FromBase64String(document?.ColorData?.Rgb8Base64 ?? string.Empty);
            if (bytes.Length != ColorCount * 3)
                throw new InvalidDataException("Color Palette rgb8Base64 must decode to 196608 bytes.");
            return bytes;
        }

        internal static Color32 GetColor(WGameSurfacePaletteCatalogDocument document, int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= ColorCount)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));
            byte[] rgb = DecodeRgb8(document);
            int offset = colorIndex * 3;
            return new Color32(rgb[offset], rgb[offset + 1], rgb[offset + 2], 255);
        }

        internal static void DecodeRgb565(int colorIndex, out int red5, out int green6, out int blue5)
        {
            if (colorIndex < 0 || colorIndex >= ColorCount)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));
            red5 = (colorIndex >> 11) & 31;
            green6 = (colorIndex >> 5) & 63;
            blue5 = colorIndex & 31;
        }

        internal static int ColorIndexFromRgb565(int red5, int green6, int blue5)
        {
            if (red5 < 0 || red5 >= RedLevels) throw new ArgumentOutOfRangeException(nameof(red5));
            if (green6 < 0 || green6 >= GreenLevels) throw new ArgumentOutOfRangeException(nameof(green6));
            if (blue5 < 0 || blue5 >= BlueLevels) throw new ArgumentOutOfRangeException(nameof(blue5));
            return (red5 << 11) | (green6 << 5) | blue5;
        }

        internal static Color32 FormulaColor(int colorIndex)
        {
            DecodeRgb565(colorIndex, out int red5, out int green6, out int blue5);
            return new Color32(
                ExpandComponent(red5, RedLevels - 1),
                ExpandComponent(green6, GreenLevels - 1),
                ExpandComponent(blue5, BlueLevels - 1),
                255);
        }

        internal static int ColorIndexFromRgb8(byte red, byte green, byte blue)
        {
            return ColorIndexFromRgb565(
                QuantizeComponent(red, RedLevels - 1),
                QuantizeComponent(green, GreenLevels - 1),
                QuantizeComponent(blue, BlueLevels - 1));
        }

        private static byte ExpandComponent(int value, int maximum)
        {
            return checked((byte)((value * 255 + maximum / 2) / maximum));
        }

        private static int QuantizeComponent(byte value, int maximum)
        {
            return (value * maximum + 127) / 255;
        }

        internal static int ColorIndexFromUv(Vector2 uv, out bool isCenter)
        {
            isCenter = false;
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                return -1;
            float columnValue = uv.x * Width - 0.5f;
            float rowValue = uv.y * Height - 0.5f;
            int column = Mathf.RoundToInt(columnValue);
            int row = Mathf.RoundToInt(rowValue);
            isCenter = column >= 0 && column < Width && row >= 0 && row < Height
                && Mathf.Abs(columnValue - column) <= 0.001f
                && Mathf.Abs(rowValue - row) <= 0.001f;
            return isCenter ? row * Width + column : -1;
        }

        internal static Vector2 UvFromColorIndex(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= ColorCount)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));
            int x = colorIndex % Width;
            int y = colorIndex / Width;
            return new Vector2((x + 0.5f) / Width, (y + 0.5f) / Height);
        }

        internal static int LogicalToken(int colorIndex, int surfaceProfile)
        {
            if (colorIndex < 0 || colorIndex >= ColorCount)
                throw new ArgumentOutOfRangeException(nameof(colorIndex));
            if (surfaceProfile < 0 || surfaceProfile >= SurfaceProfileCount)
                throw new ArgumentOutOfRangeException(nameof(surfaceProfile));
            return colorIndex * SurfaceProfileCount + surfaceProfile;
        }

        internal static void SplitLogicalToken(int logicalToken, out int colorIndex, out int surfaceProfile)
        {
            if (logicalToken < 0 || logicalToken >= LogicalTokenCount)
                throw new ArgumentOutOfRangeException(nameof(logicalToken));
            colorIndex = logicalToken / SurfaceProfileCount;
            surfaceProfile = logicalToken % SurfaceProfileCount;
        }

        internal static byte EncodeSurfaceProfileAlpha(int surfaceProfile)
        {
            if (surfaceProfile < 0 || surfaceProfile >= SurfaceProfileCount)
                throw new ArgumentOutOfRangeException(nameof(surfaceProfile));
            return checked((byte)(8 + surfaceProfile * 16));
        }

        internal static int DecodeSurfaceProfileAlpha(byte alpha)
        {
            return Math.Min(SurfaceProfileCount - 1, alpha / 16);
        }

        internal static string ColorId(int colorIndex)
        {
            DecodeRgb565(colorIndex, out int red5, out int green6, out int blue5);
            return string.Format(CultureInfo.InvariantCulture, "wgame.color.v3.r{0:00}.g{1:00}.b{2:00}", red5, green6, blue5);
        }

        internal static string TokenId(WGameSurfacePaletteCatalogDocument document, int colorIndex, int surfaceProfile)
        {
            return ColorId(colorIndex).Replace("wgame.color", "wgame.surface")
                + "." + document.SurfaceProfiles[surfaceProfile].ProfileId;
        }

        internal static bool TryParseColorId(string value, out int colorIndex)
        {
            colorIndex = -1;
            string[] parts = (value ?? string.Empty).Split('.');
            if (parts.Length != 6
                || !string.Equals(parts[0], "wgame", StringComparison.Ordinal)
                || !string.Equals(parts[1], "color", StringComparison.Ordinal)
                || !string.Equals(parts[2], "v3", StringComparison.Ordinal)
                || !parts[3].StartsWith("r", StringComparison.Ordinal)
                || !parts[4].StartsWith("g", StringComparison.Ordinal)
                || !parts[5].StartsWith("b", StringComparison.Ordinal)
                || !int.TryParse(parts[3].Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int red5)
                || !int.TryParse(parts[4].Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int green6)
                || !int.TryParse(parts[5].Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int blue5)
                || red5 < 0 || red5 >= RedLevels || green6 < 0 || green6 >= GreenLevels || blue5 < 0 || blue5 >= BlueLevels)
                return false;
            colorIndex = ColorIndexFromRgb565(red5, green6, blue5);
            return true;
        }

        internal static bool TryParseTokenId(WGameSurfacePaletteCatalogDocument document, string value, out int colorIndex, out int surfaceProfile)
        {
            colorIndex = -1;
            surfaceProfile = -1;
            string input = value ?? string.Empty;
            int profileSeparator = input.LastIndexOf('.');
            if (profileSeparator <= 0)
                return false;
            string colorPart = input.Substring(0, profileSeparator).Replace("wgame.surface", "wgame.color");
            string profileId = input.Substring(profileSeparator + 1);
            if (!TryParseColorId(colorPart, out colorIndex))
                return false;
            for (int index = 0; index < document.SurfaceProfiles.Count; index++)
            {
                if (!string.Equals(document.SurfaceProfiles[index].ProfileId, profileId, StringComparison.Ordinal))
                    continue;
                surfaceProfile = index;
                return true;
            }
            colorIndex = -1;
            return false;
        }

        internal static string ComputeCatalogHash(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json ?? string.Empty))
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                    WriteCanonicalElement(writer, document.RootElement, true);
                return ComputeSha256(stream.ToArray());
            }
        }

        internal static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes ?? Array.Empty<byte>());
                var builder = new StringBuilder("sha256:", 71);
                for (int index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        internal static WGameColorPaletteAtlasReceipt ReadReceipt(string path, out string issue)
        {
            issue = string.Empty;
            try
            {
                if (!File.Exists(Path.GetFullPath(path)))
                {
                    issue = "Color Palette atlas receipt is missing: " + path + ".";
                    return null;
                }
                WGameColorPaletteAtlasReceipt receipt = JsonSerializer.Deserialize<WGameColorPaletteAtlasReceipt>(File.ReadAllText(Path.GetFullPath(path)), JsonOptions);
                if (receipt == null || receipt.Format != ReceiptFormat || receipt.SchemaVersion != ReceiptSchemaVersion
                    || receipt.Width != Width || receipt.Height != Height || receipt.ColorCount != ColorCount
                    || receipt.SurfaceProfileCount != SurfaceProfileCount)
                {
                    issue = "Color Palette atlas receipt contract is invalid.";
                    return null;
                }
                return receipt;
            }
            catch (Exception ex)
            {
                issue = "Color Palette atlas receipt could not be read: " + ex.Message;
                return null;
            }
        }

        internal static void WriteReceipt(string path, WGameColorPaletteAtlasReceipt receipt)
        {
            File.WriteAllText(Path.GetFullPath(path), JsonSerializer.Serialize(receipt, JsonOptions) + "\n", new UTF8Encoding(false));
        }

        private static bool ValidateDocument(WGameSurfacePaletteCatalogDocument document, out string issue)
        {
            issue = string.Empty;
            if (document.Format != Format || document.SchemaVersion != SchemaVersion
                || document.Width != Width || document.Height != Height
                || document.ColorCount != ColorCount || document.SurfaceProfileCount != SurfaceProfileCount
                || document.LogicalTokenCount != LogicalTokenCount)
            {
                issue = "RGB565 Color Palette v3 dimensions or counts are invalid.";
                return false;
            }
            if (document.SurfaceProfiles == null || document.SurfaceProfiles.Count != SurfaceProfileCount)
            {
                issue = "Color Palette requires exactly 16 Surface Profiles.";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < SurfaceProfileCount; index++)
            {
                WGameSurfaceProfile profile = document.SurfaceProfiles[index];
                if (profile == null || profile.Index != index || profile.AlphaByte != EncodeSurfaceProfileAlpha(index)
                    || string.IsNullOrWhiteSpace(profile.ProfileId) || !ids.Add(profile.ProfileId))
                {
                    issue = "Surface Profile order, alpha center, or ID is invalid at index " + index + ".";
                    return false;
                }
            }
            try
            {
                byte[] rgb = DecodeRgb8(document);
                var unique = new HashSet<int>();
                for (int index = 0; index < ColorCount; index++)
                {
                    Color32 expected = FormulaColor(index);
                    int offset = index * 3;
                    if (rgb[offset] != expected.r || rgb[offset + 1] != expected.g || rgb[offset + 2] != expected.b)
                    {
                        issue = "RGB565 formula differs from compact RGB table at colorIndex " + index + ".";
                        return false;
                    }
                    if (!unique.Add((rgb[offset] << 16) | (rgb[offset + 1] << 8) | rgb[offset + 2]))
                    {
                        issue = "RGB565 Color Palette contains a duplicate RGB8 value at colorIndex " + index + ".";
                        return false;
                    }
                }
                if (unique.Count != ColorCount || document.ColorData.UniqueRgb8Count != ColorCount || document.ColorData.AliasCount != 0)
                { issue = "RGB565 Color Palette must contain 65,536 unique RGB8 values and zero aliases."; return false; }
            }
            catch (Exception ex)
            {
                issue = ex.Message;
                return false;
            }
            return true;
        }

        private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element, bool isRoot = false)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = new List<JsonProperty>();
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (isRoot && property.Name == "catalogHash") continue;
                        properties.Add(property);
                    }
                    properties.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
                    writer.WriteStartObject();
                    foreach (JsonProperty property in properties)
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonicalElement(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement child in element.EnumerateArray()) WriteCanonicalElement(writer, child);
                    writer.WriteEndArray();
                    break;
                default: element.WriteTo(writer); break;
            }
        }
    }

    [Serializable]
    internal sealed class WGameSurfacePaletteCatalogDocument
    {
        [JsonPropertyName("format")] public string Format { get; set; } = string.Empty;
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("colorCount")] public int ColorCount { get; set; }
        [JsonPropertyName("surfaceProfileCount")] public int SurfaceProfileCount { get; set; }
        [JsonPropertyName("logicalTokenCount")] public int LogicalTokenCount { get; set; }
        [JsonPropertyName("surfaceProfiles")] public List<WGameSurfaceProfile> SurfaceProfiles { get; set; } = new List<WGameSurfaceProfile>();
        [JsonPropertyName("colorData")] public WGameColorData ColorData { get; set; }
        [JsonPropertyName("catalogHash")] public string CatalogHash { get; set; } = string.Empty;
    }

    [Serializable]
    internal sealed class WGameColorData
    {
        [JsonPropertyName("rgb8Base64")] public string Rgb8Base64 { get; set; } = string.Empty;
        [JsonPropertyName("uniqueRgb8Count")] public int UniqueRgb8Count { get; set; }
        [JsonPropertyName("aliasCount")] public int AliasCount { get; set; }
    }

    [Serializable]
    internal sealed class WGameSurfaceProfile
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("profileId")] public string ProfileId { get; set; } = string.Empty;
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonPropertyName("family")] public int Family { get; set; }
        [JsonPropertyName("finish")] public int Finish { get; set; }
        [JsonPropertyName("alphaByte")] public int AlphaByte { get; set; }
        [JsonPropertyName("shadowFloor")] public float ShadowFloor { get; set; }
        [JsonPropertyName("wrap")] public float Wrap { get; set; }
        [JsonPropertyName("ao")] public float Ao { get; set; }
        [JsonPropertyName("specular")] public float Specular { get; set; }
        [JsonPropertyName("specularPower")] public float SpecularPower { get; set; }
        [JsonPropertyName("rim")] public float Rim { get; set; }
        [JsonPropertyName("roughness")] public float Roughness { get; set; }
        [JsonPropertyName("metallic")] public float Metallic { get; set; }
        [JsonPropertyName("detailModeIndex")] public int DetailModeIndex { get; set; }
        [JsonPropertyName("detailScale")] public float DetailScale { get; set; }
        [JsonPropertyName("detailDiffuse")] public float DetailDiffuse { get; set; }
        [JsonPropertyName("detailSpecular")] public float DetailSpecular { get; set; }
    }

    [Serializable]
    internal sealed class WGameColorPaletteAtlasReceipt
    {
        [JsonPropertyName("format")] public string Format { get; set; } = WGameSurfacePaletteCatalog.ReceiptFormat;
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = WGameSurfacePaletteCatalog.ReceiptSchemaVersion;
        [JsonPropertyName("generatorVersion")] public string GeneratorVersion { get; set; } = "mx.wgameRgb565ColorPaletteUnityGenerator.v3";
        [JsonPropertyName("catalogHash")] public string CatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("atlasHash")] public string AtlasHash { get; set; } = string.Empty;
        [JsonPropertyName("colorCount")] public int ColorCount { get; set; } = WGameSurfacePaletteCatalog.ColorCount;
        [JsonPropertyName("surfaceProfileCount")] public int SurfaceProfileCount { get; set; } = WGameSurfacePaletteCatalog.SurfaceProfileCount;
        [JsonPropertyName("width")] public int Width { get; set; } = WGameSurfacePaletteCatalog.Width;
        [JsonPropertyName("height")] public int Height { get; set; } = WGameSurfacePaletteCatalog.Height;
        [JsonPropertyName("textureFormat")] public string TextureFormat { get; set; } = "RGB24";
    }
}
