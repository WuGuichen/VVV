using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string SurfacePaletteCatalogRelativePath = "ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json";
    private const string SurfacePaletteCatalogFormat = "mx.wgameSurfacePaletteCatalog.v3";
    private const string SurfacePaletteCatalogSchemaVersion = "3.0";
    private const string SurfacePaletteUsageFormat = "mx.characterPaletteUsage.v2";
    private const string CharacterPaletteBindingFormat = "mx.characterPaletteBinding.v5";
    private const int SurfacePaletteWidth = 256;
    private const int SurfacePaletteHeight = 256;
    private const int SurfacePaletteColorCount = 65536;
    private const int SurfaceProfileCount = 16;
    private const int LogicalTokenCount = SurfacePaletteColorCount * SurfaceProfileCount;

    private static readonly JsonSerializerOptions s_surfacePaletteAuthorityJsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // manifest.json 的 kind 以字符串形式持久化（"Character"），必须与包读取路径保持一致的枚举转换。
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static bool TryHandleSurfacePaletteApi(HttpListenerContext context, string rootPath, JsonSerializerOptions jsonOptions, string path)
    {
        if (!path.StartsWith("/api/authoring/palette/", StringComparison.Ordinal)) return false;
        if (path == "/api/authoring/palette/catalog" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadSurfacePaletteCatalog(rootPath, true), jsonOptions);
            return true;
        }
        if (path == "/api/authoring/palette/query" && context.Request.HttpMethod == "GET")
        {
            WriteSurfacePaletteQuery(context, rootPath, jsonOptions);
            return true;
        }
        if (path == "/api/authoring/palette/references" && context.Request.HttpMethod == "GET")
        {
            WriteSurfacePaletteReferences(context, rootPath, jsonOptions);
            return true;
        }
        if (path == "/api/authoring/palette/usage" && context.Request.HttpMethod == "POST")
        {
            SaveSurfacePaletteUsage(context, rootPath, jsonOptions);
            return true;
        }
        if (path == "/api/authoring/palette/binding" && context.Request.HttpMethod == "POST")
        {
            SaveCharacterPaletteBinding(context, rootPath, jsonOptions);
            return true;
        }
        WriteJson(context.Response, new
        {
            code = "SURFACE_PALETTE_READ_ONLY",
            message = "RGB565 Color Palette Catalog v3 is generator-owned. Create, Duplicate, Update, and Retire are not supported."
        }, jsonOptions, 404);
        return true;
    }

    private static void WriteSurfacePaletteQuery(HttpListenerContext context, string rootPath, JsonSerializerOptions jsonOptions)
    {
        JsonObject catalog = ReadSurfacePaletteCatalog(rootPath, true);
        SurfacePaletteTables tables = DecodeSurfacePaletteTables(catalog);
        int profile = ResolveProfile(catalog, context.Request.QueryString["surfaceProfile"] ?? context.Request.QueryString["profile"]);
        bool nearest = string.Equals(context.Request.QueryString["nearest"], "true", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(context.Request.QueryString["nearestHex"]);
        var colorIndices = new List<int>();

        if (TryResolveLogicalTokenQuery(catalog, context.Request.QueryString, out int logicalColor, out int logicalProfile))
        {
            colorIndices.Add(logicalColor);
            if (profile < 0) profile = logicalProfile;
        }
        else if (TryResolveColorIndexQuery(context.Request.QueryString, out int directColor))
        {
            colorIndices.Add(directColor);
        }
        else if (TryResolveRgb565Query(context.Request.QueryString, out int formulaColor))
        {
            colorIndices.Add(formulaColor);
        }
        else
        {
            string hex = NormalizeSurfacePaletteHex(context.Request.QueryString["hex"] ?? context.Request.QueryString["nearestHex"]);
            if (!string.IsNullOrEmpty(hex))
            {
                (int r, int g, int b) = ParseSurfacePaletteHex(hex);
                if (nearest)
                {
                    colorIndices.Add(ColorIndexFromRgb8(r, g, b));
                }
                else
                {
                    for (int index = 0; index < SurfacePaletteColorCount; index++)
                    {
                        int offset = index * 3;
                        if (tables.Rgb[offset] == r && tables.Rgb[offset + 1] == g && tables.Rgb[offset + 2] == b)
                            colorIndices.Add(index);
                    }
                }
            }
        }

        if (colorIndices.Count == 0)
        {
            WriteJson(context.Response, new { code = "SURFACE_PALETTE_COLOR_NOT_FOUND", message = "No matching formula Color was found.", catalogHash = catalog["catalogHash"]?.GetValue<string>() }, jsonOptions, 404);
            return;
        }
        var matches = new JsonArray();
        foreach (int colorIndex in colorIndices.Distinct().Take(64))
        {
            if (profile >= 0) matches.Add(BuildTokenDescriptor(catalog, tables, colorIndex, profile));
            else matches.Add(BuildColorDescriptor(catalog, tables, colorIndex));
        }
        WriteJson(context.Response, new
        {
            catalogHash = catalog["catalogHash"]?.GetValue<string>() ?? string.Empty,
            queryMode = nearest ? "nearestRgb565" : "exact",
            aliasCount = 0,
            matches
        }, jsonOptions);
    }

    private static void WriteSurfacePaletteReferences(HttpListenerContext context, string rootPath, JsonSerializerOptions jsonOptions)
    {
        JsonObject catalog = ReadSurfacePaletteCatalog(rootPath, true);
        int? logicalToken = null;
        int? colorIndex = null;
        if (TryResolveLogicalTokenQuery(catalog, context.Request.QueryString, out int parsedColor, out int parsedProfile))
        {
            colorIndex = parsedColor;
            logicalToken = parsedColor * 16 + parsedProfile;
        }
        else if (TryResolveColorIndexQuery(context.Request.QueryString, out int parsedIndex)) colorIndex = parsedIndex;
        if (!colorIndex.HasValue)
        {
            WriteJson(context.Response, new { code = "SURFACE_PALETTE_QUERY_REQUIRED", message = "logicalToken, tokenId, colorIndex, or x/y is required." }, jsonOptions, 400);
            return;
        }
        List<SurfacePaletteReference> references = FindSurfacePaletteReferences(rootPath, logicalToken, colorIndex.Value);
        WriteJson(context.Response, new
        {
            catalogHash = catalog["catalogHash"]?.GetValue<string>() ?? string.Empty,
            colorIndex,
            logicalToken,
            referenceCount = references.Count,
            faceCount = references.Sum(item => item.FaceCount),
            references
        }, jsonOptions);
    }

    private static void SaveSurfacePaletteUsage(HttpListenerContext context, string rootPath, JsonSerializerOptions jsonOptions)
    {
        JsonObject request = ReadSurfacePaletteRequest(context);
        string package = request["package"]?.GetValue<string>() ?? string.Empty;
        string expectedRevision = request["expectedRevision"]?.GetValue<string>() ?? string.Empty;
        JsonObject usage = request["usage"] as JsonObject ?? throw new InvalidOperationException("usage document is required.");
        string packagePath = ResolveSafePath(rootPath, package);
        string packagesRoot = Path.GetFullPath(Path.Combine(rootPath, "Authoring", "CharacterPackages"));
        if (!IsPathAtOrUnderRoot(packagesRoot, packagePath)) throw new InvalidOperationException("Palette usage must belong to Authoring/CharacterPackages.");

        string currentRevision = ComputeCharacterPackageRevision(packagePath);
        if (!string.IsNullOrWhiteSpace(expectedRevision) && !string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
        {
            WriteJson(context.Response, new { code = "PALETTE_USAGE_REVISION_CONFLICT", message = "Character package revision is stale.", currentRevision }, jsonOptions, 409);
            return;
        }

        JsonObject catalog = ReadSurfacePaletteCatalog(rootPath, true);
        string currentHash = catalog["catalogHash"]?.GetValue<string>() ?? string.Empty;
        if (!string.Equals(usage["catalogHash"]?.GetValue<string>(), currentHash, StringComparison.Ordinal))
        {
            WriteJson(context.Response, new { code = "SURFACE_PALETTE_USAGE_HASH_CONFLICT", message = "Usage Catalog hash is stale.", currentHash }, jsonOptions, 409);
            return;
        }
        if (!ValidateUsage(catalog, usage, out string issue))
        {
            WriteJson(context.Response, new { code = "SURFACE_PALETTE_USAGE_INVALID", message = issue, currentHash }, jsonOptions, 400);
            return;
        }

        // 权威判定：usage.modelHash 是「模型版本标签」，必须等于包声明的本体模型源文件哈希。
        // 服务端不具备重算 tokenUsages 的能力（面数统计只在浏览器端 three.js 中实现），
        // 因此这里只做拒绝，绝不用正确哈希去覆盖一份可能属于其它模型版本的 tokenUsages。
        if (!TryAuthorizeSurfacePaletteUsageModelHash(
            packagePath, usage, out string authorityCode, out string authorityMessage,
            out string expectedModelHash, out string actualModelHash))
        {
            WriteJson(context.Response, new
            {
                code = authorityCode,
                message = authorityMessage,
                expectedModelHash,
                actualModelHash,
                currentRevision
            }, jsonOptions, 409);
            return;
        }

        usage["format"] = SurfacePaletteUsageFormat;
        usage["schemaVersion"] = "2.0";
        string target = Path.Combine(packagePath, "config", "character_palette_usage.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        WriteSurfacePaletteJsonAtomic(target, usage);
        EnsureSurfacePaletteConfigRegistered(packagePath, "config/character_palette_usage.json");
        WriteJson(context.Response, new { success = true, catalogHash = currentHash, modelHash = expectedModelHash, revision = ComputeCharacterPackageRevision(packagePath) }, jsonOptions);
    }

    private static bool TryAuthorizeSurfacePaletteUsageModelHash(
        string packagePath,
        JsonObject usage,
        out string code,
        out string message,
        out string expectedModelHash,
        out string actualModelHash)
    {
        code = string.Empty;
        message = string.Empty;
        expectedModelHash = string.Empty;
        actualModelHash = usage["modelHash"]?.GetValue<string>() ?? string.Empty;

        CharacterResourcePackage authorityPackage = ReadSurfacePaletteAuthorityPackage(packagePath);
        CharacterBodyModelResolution body = CharacterPaletteUsageAuthority.ResolveBodyModel(authorityPackage, packagePath, true);
        CharacterPaletteUsageCheckResult check = CharacterPaletteUsageAuthority.ValidateUsageModelHash(body, actualModelHash);
        expectedModelHash = check.ExpectedModelHash;
        actualModelHash = check.ActualModelHash;
        if (check.IsValid)
            return true;

        code = check.Code;
        message = check.Message;
        return false;
    }

    /// <summary>
    /// 只读取本体模型解析所需的最小内容（manifest + resource_catalog），
    /// 避免色板保存路径被无关配置（如 character_actions.json）的解析失败连带阻塞。
    /// </summary>
    private static CharacterResourcePackage ReadSurfacePaletteAuthorityPackage(string packagePath)
    {
        var package = new CharacterResourcePackage { PackagePath = packagePath };
        string manifestPath = Path.Combine(packagePath, "manifest.json");
        if (File.Exists(manifestPath))
            package.Manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), s_surfacePaletteAuthorityJsonOptions)
                ?? new CharacterPackageManifest();
        string catalogPath = Path.Combine(packagePath, "resource_catalog.json");
        if (File.Exists(catalogPath))
            package.ResourceCatalog = JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(File.ReadAllText(catalogPath), s_surfacePaletteAuthorityJsonOptions)
                ?? new CharacterPackageResourceCatalog();
        return package;
    }

    private static void SaveCharacterPaletteBinding(HttpListenerContext context, string rootPath, JsonSerializerOptions jsonOptions)
    {
        JsonObject request = ReadSurfacePaletteRequest(context);
        string package = request["package"]?.GetValue<string>() ?? string.Empty;
        string expectedRevision = request["expectedRevision"]?.GetValue<string>() ?? string.Empty;
        JsonObject binding = request["binding"] as JsonObject ?? throw new InvalidOperationException("binding document is required.");
        string packagePath = ResolveSafePath(rootPath, package);
        if (CharacterBakedSurfaceAuthority.Exists(packagePath))
        {
            WriteJson(context.Response, new { code = "BAKED_SURFACE_AUTHORING_ONLY", message = "请在 Blender 修改配色和细节，再重新烘焙导出。" }, jsonOptions, 409);
            return;
        }
        string packagesRoot = Path.GetFullPath(Path.Combine(rootPath, "Authoring", "CharacterPackages"));
        if (!IsPathAtOrUnderRoot(packagesRoot, packagePath)) throw new InvalidOperationException("Palette binding must belong to Authoring/CharacterPackages.");
        string currentRevision = ComputeCharacterPackageRevision(packagePath);
        if (!string.IsNullOrWhiteSpace(expectedRevision) && !string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
        {
            WriteJson(context.Response, new { code = "PALETTE_BINDING_REVISION_CONFLICT", message = "Character package revision is stale.", currentRevision }, jsonOptions, 409);
            return;
        }
        if (!ValidateCharacterPaletteBindingV5(packagePath, binding, out string issue))
        {
            WriteJson(context.Response, new { code = "PALETTE_BINDING_INVALID", message = issue, currentRevision }, jsonOptions, 400);
            return;
        }
        binding["format"] = CharacterPaletteBindingFormat;
        binding["schemaVersion"] = "5.0";
        string target = Path.Combine(packagePath, "config", "character_palette_binding.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        WriteSurfacePaletteJsonAtomic(target, binding);
        EnsureSurfacePaletteConfigRegistered(packagePath, "config/character_palette_binding.json");
        WriteJson(context.Response, new { success = true, revision = ComputeCharacterPackageRevision(packagePath) }, jsonOptions);
    }

    private static void EnsureSurfacePaletteConfigRegistered(string packagePath, string relativePath)
    {
        string manifestPath = Path.Combine(packagePath, "manifest.json");
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject
            ?? throw new InvalidDataException("Character package manifest is malformed.");
        JsonArray configFiles = manifest["configFiles"] as JsonArray;
        if (configFiles == null)
        {
            configFiles = new JsonArray();
            manifest["configFiles"] = configFiles;
        }
        if (configFiles.Any(item => string.Equals(item?.GetValue<string>(), relativePath, StringComparison.Ordinal)))
            return;
        configFiles.Add(relativePath);
        WriteSurfacePaletteJsonAtomic(manifestPath, manifest);
    }

    private static bool ValidateCharacterPaletteBindingV5(string packagePath, JsonObject binding, out string issue)
    {
        issue = string.Empty;
        if (binding["format"]?.GetValue<string>() != CharacterPaletteBindingFormat || binding["schemaVersion"]?.GetValue<string>() != "5.0")
        { issue = "Binding requires mx.characterPaletteBinding.v5 / 5.0."; return false; }
        if (binding["colorAtlasResourceKey"]?.GetValue<string>() != "wgame.character.color_palette.shared"
            || binding["addressingMode"]?.GetValue<string>() != "colorUvSurfaceProfileVertexAlpha"
            || binding["atlasWidth"]?.GetValue<int>() != 256 || binding["atlasHeight"]?.GetValue<int>() != 256
            || binding["surfaceProfileSource"]?.GetValue<string>() != "paletteDataAlpha16"
            || binding["surfaceProfileCount"]?.GetValue<int>() != 16)
        { issue = "Binding v5 Color Palette contract is invalid."; return false; }
        bool enabled = binding["faceDetailEnabled"]?.GetValue<bool>() ?? false;
        if (!enabled) return true;
        string manifestPath = Path.Combine(packagePath, "manifest.json");
        string packageId = JsonNode.Parse(File.ReadAllText(manifestPath))?["packageId"]?.GetValue<string>() ?? string.Empty;
        string expectedKey = "char." + packageId + ".texture.face_detail";
        string resourceKey = binding["faceDetailTextureResourceKey"]?.GetValue<string>() ?? string.Empty;
        double intensity = binding["faceDetailIntensity"]?.GetValue<double>() ?? 1.0;
        if (resourceKey != expectedKey) { issue = "FaceDetail texture must use current package resource key " + expectedKey + "."; return false; }
        if (double.IsNaN(intensity) || double.IsInfinity(intensity) || intensity < 0.0 || intensity > 1.0)
        { issue = "faceDetailIntensity must be from 0 through 1."; return false; }
        JsonObject catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(packagePath, "resource_catalog.json"))) as JsonObject;
        JsonArray entries = catalog?["entries"] as JsonArray;
        JsonObject entry = entries?.OfType<JsonObject>().FirstOrDefault(item => item["resourceKey"]?.GetValue<string>() == resourceKey);
        if (entry == null || !string.Equals(entry["typeId"]?.GetValue<string>(), "texture", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entry["usage"]?.GetValue<string>(), "texture", StringComparison.OrdinalIgnoreCase))
        { issue = "FaceDetail resource must be a package texture resource."; return false; }
        string relative = entry["relativePath"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..", StringComparison.Ordinal) || !relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        { issue = "FaceDetail resource path must be a safe package PNG."; return false; }
        string texturePath = Path.GetFullPath(Path.Combine(packagePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathAtOrUnderRoot(packagePath, texturePath) || !File.Exists(texturePath))
        { issue = "FaceDetail texture file is missing."; return false; }
        byte[] png = File.ReadAllBytes(texturePath);
        if (png.Length < 26 || Encoding.ASCII.GetString(png, 1, 3) != "PNG"
            || ReadPngInt32(png, 16) != 512 || ReadPngInt32(png, 20) != 512 || png[25] != 6)
        { issue = "FaceDetail texture must be a 512x512 RGBA PNG."; return false; }
        return true;
    }

    private static int ReadPngInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static bool ValidateUsage(JsonObject catalog, JsonObject usage, out string issue)
    {
        issue = string.Empty;
        string format = usage["format"]?.GetValue<string>() ?? SurfacePaletteUsageFormat;
        string schema = usage["schemaVersion"]?.GetValue<string>() ?? "2.0";
        if (format != SurfacePaletteUsageFormat || schema != "2.0") { issue = "Usage requires mx.characterPaletteUsage.v2 / 2.0."; return false; }
        JsonArray entries = usage["tokenUsages"] as JsonArray ?? new JsonArray();
        JsonArray profiles = catalog["surfaceProfiles"] as JsonArray ?? new JsonArray();
        foreach (JsonNode node in entries)
        {
            if (node is not JsonObject entry) { issue = "tokenUsages entries must be objects."; return false; }
            int colorIndex = entry["colorIndex"]?.GetValue<int>() ?? -1;
            int logical = entry["logicalToken"]?.GetValue<int>() ?? -1;
            string profileId = entry["surfaceProfileId"]?.GetValue<string>() ?? string.Empty;
            string tokenId = entry["tokenId"]?.GetValue<string>() ?? string.Empty;
            int faceCount = entry["faceCount"]?.GetValue<int>() ?? 0;
            int profile = ResolveProfile(catalog, profileId);
            if (colorIndex < 0 || colorIndex >= SurfacePaletteColorCount || profile < 0 || logical != colorIndex * 16 + profile
                || tokenId != SurfaceTokenId(catalog, colorIndex, profile) || faceCount <= 0)
            { issue = "Usage contains an invalid formula Token."; return false; }
        }
        return true;
    }

    private static JsonObject ReadSurfacePaletteCatalog(string rootPath, bool validateHash)
    {
        string path = Path.GetFullPath(Path.Combine(rootPath, SurfacePaletteCatalogRelativePath));
        if (!File.Exists(path)) throw new FileNotFoundException("Color Palette Catalog is missing.", path);
        JsonObject catalog = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject ?? throw new InvalidDataException("Catalog root must be an object.");
        if (catalog["format"]?.GetValue<string>() != SurfacePaletteCatalogFormat
            || catalog["schemaVersion"]?.GetValue<string>() != SurfacePaletteCatalogSchemaVersion
            || catalog["width"]?.GetValue<int>() != 256 || catalog["height"]?.GetValue<int>() != 256
            || catalog["colorCount"]?.GetValue<int>() != SurfacePaletteColorCount
            || catalog["surfaceProfileCount"]?.GetValue<int>() != SurfaceProfileCount)
            throw new InvalidDataException("RGB565 Color Palette Catalog v3 contract is invalid.");
        SurfacePaletteTables tables = DecodeSurfacePaletteTables(catalog);
        if (tables.Rgb.Length != SurfacePaletteColorCount * 3)
            throw new InvalidDataException("RGB565 Color Palette compact RGB table length is invalid.");
        var unique = new HashSet<int>();
        for (int index = 0; index < SurfacePaletteColorCount; index++)
        {
            (int red5, int green6, int blue5) = DecodeRgb565(index);
            (int r, int g, int b) = ExpandRgb565(red5, green6, blue5);
            int offset = index * 3;
            if (tables.Rgb[offset] != r || tables.Rgb[offset + 1] != g || tables.Rgb[offset + 2] != b)
                throw new InvalidDataException("RGB565 formula differs from compact RGB table at colorIndex " + index + ".");
            if (!unique.Add((r << 16) | (g << 8) | b))
                throw new InvalidDataException("RGB565 Color Palette contains duplicate RGB8 values.");
        }
        JsonObject colorData = catalog["colorData"] as JsonObject ?? throw new InvalidDataException("colorData is missing.");
        if (unique.Count != SurfacePaletteColorCount
            || colorData["uniqueRgb8Count"]?.GetValue<int>() != SurfacePaletteColorCount
            || colorData["aliasCount"]?.GetValue<int>() != 0)
            throw new InvalidDataException("RGB565 Color Palette must contain 65,536 unique RGB8 values and zero aliases.");
        if (validateHash)
        {
            string stored = catalog["catalogHash"]?.GetValue<string>() ?? string.Empty;
            string computed = ComputeSurfacePaletteCatalogHash(catalog);
            if (stored != computed) throw new InvalidDataException($"Color Palette catalogHash mismatch. Stored={stored}, computed={computed}.");
        }
        return catalog;
    }

    private static SurfacePaletteTables DecodeSurfacePaletteTables(JsonObject catalog)
    {
        JsonObject data = catalog["colorData"] as JsonObject ?? throw new InvalidDataException("colorData is missing.");
        byte[] rgb = Convert.FromBase64String(data["rgb8Base64"]?.GetValue<string>() ?? string.Empty);
        return new SurfacePaletteTables(rgb);
    }

    private static JsonObject BuildColorDescriptor(JsonObject catalog, SurfacePaletteTables tables, int colorIndex)
    {
        int x = colorIndex % 256;
        int y = colorIndex / 256;
        (int red5, int green6, int blue5) = DecodeRgb565(colorIndex);
        int offset = colorIndex * 3;
        int r = tables.Rgb[offset]; int g = tables.Rgb[offset + 1]; int b = tables.Rgb[offset + 2];
        (double actualL, double actualC, double actualH) = SurfacePaletteRgbToOklch(r, g, b);
        return new JsonObject
        {
            ["colorId"] = ColorId(colorIndex), ["colorIndex"] = colorIndex, ["x"] = x, ["y"] = y,
            ["red5"] = red5, ["green6"] = green6, ["blue5"] = blue5,
            ["srgbHex"] = $"#{r:X2}{g:X2}{b:X2}",
            ["srgb8"] = new JsonObject { ["r"] = r, ["g"] = g, ["b"] = b },
            ["normalizedRgb565"] = new JsonObject
            {
                ["r"] = Math.Round(red5 / 31.0, 6), ["g"] = Math.Round(green6 / 63.0, 6), ["b"] = Math.Round(blue5 / 31.0, 6)
            },
            ["oklch"] = new JsonObject { ["l"] = Math.Round(actualL, 6), ["c"] = Math.Round(actualC, 6), ["h"] = Math.Round(actualH, 4) },
            ["canonicalColorIndex"] = colorIndex,
            ["isAlias"] = false
        };
    }

    private static JsonObject BuildTokenDescriptor(JsonObject catalog, SurfacePaletteTables tables, int colorIndex, int profile)
    {
        JsonObject descriptor = BuildColorDescriptor(catalog, tables, colorIndex);
        JsonObject profileObject = ((JsonArray)catalog["surfaceProfiles"]!)[profile] as JsonObject ?? new JsonObject();
        descriptor["tokenId"] = SurfaceTokenId(catalog, colorIndex, profile);
        descriptor["logicalToken"] = colorIndex * 16 + profile;
        descriptor["surfaceProfile"] = profile;
        descriptor["surfaceProfileId"] = profileObject["profileId"]?.GetValue<string>() ?? string.Empty;
        descriptor["surfaceProfileAlphaByte"] = profileObject["alphaByte"]?.GetValue<int>() ?? 0;
        descriptor["profile"] = profileObject.DeepClone();
        return descriptor;
    }

    private static bool TryResolveLogicalTokenQuery(JsonObject catalog, System.Collections.Specialized.NameValueCollection query, out int colorIndex, out int profile)
    {
        colorIndex = -1; profile = -1;
        if (int.TryParse(query["logicalToken"], out int logical) && logical >= 0 && logical < LogicalTokenCount)
        { colorIndex = logical / 16; profile = logical % 16; return true; }
        string tokenId = query["tokenId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tokenId)) return false;
        int separator = tokenId.LastIndexOf('.');
        if (separator <= 0 || !TryParseColorId(tokenId.Substring(0, separator).Replace("wgame.surface", "wgame.color"), out colorIndex)) return false;
        profile = ResolveProfile(catalog, tokenId.Substring(separator + 1));
        if (profile >= 0) return true;
        colorIndex = -1; return false;
    }

    private static bool TryResolveColorIndexQuery(System.Collections.Specialized.NameValueCollection query, out int colorIndex)
    {
        colorIndex = -1;
        if (int.TryParse(query["colorIndex"] ?? query["index"], out int direct) && direct >= 0 && direct < SurfacePaletteColorCount)
        { colorIndex = direct; return true; }
        if (int.TryParse(query["x"], out int x) && int.TryParse(query["y"], out int y) && x >= 0 && x < 256 && y >= 0 && y < 256)
        { colorIndex = y * 256 + x; return true; }
        string colorId = query["colorId"] ?? string.Empty;
        return TryParseColorId(colorId, out colorIndex);
    }

    private static bool TryResolveRgb565Query(System.Collections.Specialized.NameValueCollection query, out int colorIndex)
    {
        colorIndex = -1;
        if (!int.TryParse(query["red5"] ?? query["r5"], out int red5)
            || !int.TryParse(query["green6"] ?? query["g6"], out int green6)
            || !int.TryParse(query["blue5"] ?? query["b5"], out int blue5)
            || red5 < 0 || red5 >= 32 || green6 < 0 || green6 >= 64 || blue5 < 0 || blue5 >= 32) return false;
        colorIndex = ColorIndexFromRgb565(red5, green6, blue5);
        return true;
    }

    private static bool TryParseColorId(string value, out int colorIndex)
    {
        colorIndex = -1;
        string[] parts = (value ?? string.Empty).Split('.');
        if (parts.Length != 6 || parts[0] != "wgame" || parts[1] != "color" || parts[2] != "v3"
            || !int.TryParse(parts[3].TrimStart('r'), out int red5)
            || !int.TryParse(parts[4].TrimStart('g'), out int green6)
            || !int.TryParse(parts[5].TrimStart('b'), out int blue5)
            || red5 < 0 || red5 >= 32 || green6 < 0 || green6 >= 64 || blue5 < 0 || blue5 >= 32) return false;
        colorIndex = ColorIndexFromRgb565(red5, green6, blue5);
        return true;
    }

    private static int ResolveProfile(JsonObject catalog, string value)
    {
        if (int.TryParse(value, out int index) && index >= 0 && index < 16) return index;
        if (string.IsNullOrWhiteSpace(value)) return -1;
        JsonArray profiles = catalog["surfaceProfiles"] as JsonArray ?? new JsonArray();
        for (int i = 0; i < profiles.Count; i++)
            if (profiles[i]?["profileId"]?.GetValue<string>() == value) return i;
        return -1;
    }

    private static string ColorId(int colorIndex)
    {
        (int red5, int green6, int blue5) = DecodeRgb565(colorIndex);
        return $"wgame.color.v3.r{red5:00}.g{green6:00}.b{blue5:00}";
    }

    private static string SurfaceTokenId(JsonObject catalog, int colorIndex, int profile)
    {
        string profileId = catalog["surfaceProfiles"]?[profile]?["profileId"]?.GetValue<string>() ?? string.Empty;
        return ColorId(colorIndex).Replace("wgame.color", "wgame.surface") + "." + profileId;
    }

    private static int ColorIndexFromRgb565(int red5, int green6, int blue5)
    {
        return (red5 << 11) | (green6 << 5) | blue5;
    }

    private static (int Red5, int Green6, int Blue5) DecodeRgb565(int colorIndex)
    {
        return ((colorIndex >> 11) & 31, (colorIndex >> 5) & 63, colorIndex & 31);
    }

    private static (int Red, int Green, int Blue) ExpandRgb565(int red5, int green6, int blue5)
    {
        return ((red5 * 255 + 15) / 31, (green6 * 255 + 31) / 63, (blue5 * 255 + 15) / 31);
    }

    private static int ColorIndexFromRgb8(int red, int green, int blue)
    {
        int red5 = (Math.Clamp(red, 0, 255) * 31 + 127) / 255;
        int green6 = (Math.Clamp(green, 0, 255) * 63 + 127) / 255;
        int blue5 = (Math.Clamp(blue, 0, 255) * 31 + 127) / 255;
        return ColorIndexFromRgb565(red5, green6, blue5);
    }

    private static string ComputeSurfacePaletteCatalogHash(JsonObject catalog)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteCanonicalSurfacePaletteNode(writer, catalog, true);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonicalSurfacePaletteNode(Utf8JsonWriter writer, JsonNode node, bool root = false)
    {
        switch (node)
        {
            case JsonObject obj:
                writer.WriteStartObject();
                foreach ((string key, JsonNode value) in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (root && key == "catalogHash") continue;
                    writer.WritePropertyName(key);
                    if (value == null) writer.WriteNullValue(); else WriteCanonicalSurfacePaletteNode(writer, value);
                }
                writer.WriteEndObject(); break;
            case JsonArray array:
                writer.WriteStartArray(); foreach (JsonNode value in array) { if (value == null) writer.WriteNullValue(); else WriteCanonicalSurfacePaletteNode(writer, value); } writer.WriteEndArray(); break;
            default: node.WriteTo(writer); break;
        }
    }

    private static JsonObject ReadSurfacePaletteRequest(HttpListenerContext context)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body) as JsonObject ?? throw new InvalidDataException("Request body must be a JSON object.");
    }

    private static void WriteSurfacePaletteJsonAtomic(string path, JsonObject document)
    {
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + Environment.NewLine, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private static string NormalizeSurfacePaletteHex(string value)
    {
        string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!normalized.StartsWith('#')) normalized = "#" + normalized;
        return normalized.Length == 7 && normalized.Skip(1).All(Uri.IsHexDigit) ? normalized : string.Empty;
    }

    private static (int Red, int Green, int Blue) ParseSurfacePaletteHex(string value)
    {
        string normalized = NormalizeSurfacePaletteHex(value);
        if (string.IsNullOrEmpty(normalized)) throw new InvalidOperationException("Hex must be #RRGGBB.");
        return (int.Parse(normalized.AsSpan(1, 2), NumberStyles.HexNumber), int.Parse(normalized.AsSpan(3, 2), NumberStyles.HexNumber), int.Parse(normalized.AsSpan(5, 2), NumberStyles.HexNumber));
    }

    private static (double L, double C, double H) SurfacePaletteRgbToOklch(int red, int green, int blue)
    {
        (double l, double a, double b) = SurfacePaletteRgbToOklab(red, green, blue);
        double c = Math.Sqrt(a * a + b * b);
        double h = c > 1e-9 ? Math.Atan2(b, a) * 180.0 / Math.PI : 0.0;
        if (h < 0) h += 360.0;
        return (l, c, h);
    }

    private static (double L, double A, double B) SurfacePaletteRgbToOklab(int red, int green, int blue)
    {
        static double Linear(double v) => v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
        double r = Linear(red / 255.0), g = Linear(green / 255.0), b = Linear(blue / 255.0);
        double lr = Math.Cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b);
        double mr = Math.Cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b);
        double sr = Math.Cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
        return (.2104542553 * lr + .7936177850 * mr - .0040720468 * sr,
            1.9779984951 * lr - 2.4285922050 * mr + .4505937099 * sr,
            .0259040371 * lr + .7827717662 * mr - .8086757660 * sr);
    }

    private static List<SurfacePaletteReference> FindSurfacePaletteReferences(string rootPath, int? logicalToken, int colorIndex)
    {
        string packageRoot = Path.Combine(rootPath, "Authoring", "CharacterPackages");
        var result = new List<SurfacePaletteReference>();
        if (!Directory.Exists(packageRoot)) return result;
        foreach (string path in Directory.EnumerateFiles(packageRoot, "character_palette_usage.json", SearchOption.AllDirectories))
        {
            try
            {
                JsonObject usage = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                if (usage?["format"]?.GetValue<string>() != SurfacePaletteUsageFormat || usage["tokenUsages"] is not JsonArray entries) continue;
                foreach (JsonNode node in entries)
                {
                    if (node is not JsonObject entry) continue;
                    int entryColor = entry["colorIndex"]?.GetValue<int>() ?? -1;
                    int entryLogical = entry["logicalToken"]?.GetValue<int>() ?? -1;
                    if ((logicalToken.HasValue && entryLogical != logicalToken.Value) || (!logicalToken.HasValue && entryColor != colorIndex)) continue;
                    result.Add(new SurfacePaletteReference
                    {
                        Package = Path.GetRelativePath(rootPath, Directory.GetParent(Directory.GetParent(path)!.FullName)!.FullName).Replace('\\', '/'),
                        PackageId = usage["packageId"]?.GetValue<string>() ?? string.Empty,
                        ModelHash = usage["modelHash"]?.GetValue<string>() ?? string.Empty,
                        Source = entry["source"]?.GetValue<string>() ?? "character",
                        FaceCount = entry["faceCount"]?.GetValue<int>() ?? 0,
                        InvalidFaceCount = usage["invalidFaceCount"]?.GetValue<int>() ?? 0
                    });
                }
            }
            catch { }
        }
        return result;
    }

    private sealed record SurfacePaletteTables(byte[] Rgb);
    private sealed class SurfacePaletteReference
    {
        public string Package { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string ModelHash { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int FaceCount { get; set; }
        public int InvalidFaceCount { get; set; }
    }
}
