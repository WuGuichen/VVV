using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        internal const string PaletteBindingFormat = "mx.characterPaletteBinding.v5";
        internal const string PaletteBindingSchemaVersion = "5.0";
        internal const string PaletteAddressingMode = "colorUvSurfaceProfileVertexAlpha";
        internal const string PaletteSurfaceProfileSource = "paletteDataAlpha16";
        internal const string PaletteUsageFormat = "mx.characterPaletteUsage.v2";
        internal const string PaletteUsageSchemaVersion = "2.0";
        internal const string PaletteCharacterMaterialTemplateId = "wgame.character_palette_stylized_lit";
        internal const string PalettePropMaterialTemplateId = "wgame.prop_palette_stylized_lit";
        internal const string SharedPaletteResourceKey = "wgame.character.color_palette.shared";
        internal const string SharedPaletteAssetPath = WGameCharacterPaletteAssetGenerator.TexturePath;
        internal const string SharedPaletteReceiptPath = WGameCharacterPaletteAssetGenerator.ReceiptPath;
        internal const string PaletteCullModeOff = "Off";
        internal const string PaletteCullModeFront = "Front";
        internal const string PaletteCullModeBack = "Back";
        internal const string PaletteBindingRelativePath = "config/character_palette_binding.json";
        internal const string PaletteUsageRelativePath = "config/character_palette_usage.json";

        private static readonly int PaletteEnabledPropertyId = Shader.PropertyToID("_WgPaletteEnabled");
        private static readonly int PaletteTexturePropertyId = Shader.PropertyToID("_WgColorPaletteTex");
        private static readonly int PaletteParamsPropertyId = Shader.PropertyToID("_WgColorPaletteParams");
        private static readonly int PaletteDetailParamsPropertyId = Shader.PropertyToID("_WgPaletteDetailParams");
        private static readonly int FaceDetailEnabledPropertyId = Shader.PropertyToID("_WgFaceDetailEnabled");
        private static readonly int FaceDetailTexturePropertyId = Shader.PropertyToID("_WgFaceDetailTex");
        private static readonly int FaceDetailIntensityPropertyId = Shader.PropertyToID("_WgFaceDetailIntensity");
        private static readonly int CullPropertyId = Shader.PropertyToID("_Cull");

        [Serializable]
        internal sealed class CharacterPaletteBindingDocument
        {
            [JsonProperty("format", Order = 1)] public string Format { get; set; } = PaletteBindingFormat;
            [JsonProperty("schemaVersion", Order = 2)] public string SchemaVersion { get; set; } = PaletteBindingSchemaVersion;
            [JsonProperty("enabled", Order = 3)] public bool Enabled { get; set; }
            [JsonProperty("colorAtlasResourceKey", Order = 4)] public string ColorAtlasResourceKey { get; set; } = SharedPaletteResourceKey;
            [JsonProperty("addressingMode", Order = 5)] public string AddressingMode { get; set; } = PaletteAddressingMode;
            [JsonProperty("atlasWidth", Order = 6)] public int AtlasWidth { get; set; } = WGameSurfacePaletteCatalog.Width;
            [JsonProperty("atlasHeight", Order = 7)] public int AtlasHeight { get; set; } = WGameSurfacePaletteCatalog.Height;
            [JsonProperty("surfaceProfileSource", Order = 8)] public string SurfaceProfileSource { get; set; } = PaletteSurfaceProfileSource;
            [JsonProperty("surfaceProfileCount", Order = 9)] public int SurfaceProfileCount { get; set; } = WGameSurfacePaletteCatalog.SurfaceProfileCount;
            [JsonProperty("faceDetailEnabled", Order = 10)] public bool FaceDetailEnabled { get; set; }
            [JsonProperty("faceDetailTextureResourceKey", Order = 11)] public string FaceDetailTextureResourceKey { get; set; } = string.Empty;
            [JsonProperty("faceDetailIntensity", Order = 12)] public float FaceDetailIntensity { get; set; } = 1f;
            [JsonProperty("cullMode", Order = 13)] public string CullMode { get; set; } = PaletteCullModeBack;
        }

        [Serializable]
        internal sealed class CharacterPaletteUsageDocument
        {
            [JsonProperty("format", Order = 1)] public string Format { get; set; } = PaletteUsageFormat;
            [JsonProperty("schemaVersion", Order = 2)] public string SchemaVersion { get; set; } = PaletteUsageSchemaVersion;
            [JsonProperty("packageId", Order = 3)] public string PackageId { get; set; } = string.Empty;
            [JsonProperty("catalogHash", Order = 4)] public string CatalogHash { get; set; } = string.Empty;
            [JsonProperty("modelHash", Order = 5)] public string ModelHash { get; set; } = string.Empty;
            [JsonProperty("paletteRepaintRequired", Order = 6)] public bool PaletteRepaintRequired { get; set; }
            [JsonProperty("invalidFaceCount", Order = 7)] public int InvalidFaceCount { get; set; }
            [JsonProperty("tokenUsages", Order = 8)] public List<CharacterPaletteTokenUsage> TokenUsages { get; set; } = new List<CharacterPaletteTokenUsage>();
        }

        [Serializable]
        internal sealed class CharacterPaletteTokenUsage
        {
            [JsonProperty("tokenId", Order = 1)] public string TokenId { get; set; } = string.Empty;
            [JsonProperty("logicalToken", Order = 2)] public int LogicalToken { get; set; }
            [JsonProperty("colorIndex", Order = 3)] public int ColorIndex { get; set; }
            [JsonProperty("surfaceProfileId", Order = 4)] public string SurfaceProfileId { get; set; } = string.Empty;
            [JsonProperty("faceCount", Order = 5)] public int FaceCount { get; set; }
            [JsonProperty("source", Order = 6)] public string Source { get; set; } = "character";
        }

        private static CharacterPaletteBindingDocument ReadPaletteBindingDocument(string importedPackageRoot, out string issue)
        {
            issue = string.Empty;
            string path = NormalizeProjectPath(importedPackageRoot) + "/" + PaletteBindingRelativePath;
            if (!File.Exists(Path.GetFullPath(path))) return null;
            try { return ParsePaletteBindingJson(File.ReadAllText(Path.GetFullPath(path)), out issue); }
            catch (Exception ex) { issue = "Palette binding could not be read: " + ex.Message; return null; }
        }

        internal static CharacterPaletteBindingDocument ParsePaletteBindingJson(string json, out string issue)
        {
            issue = string.Empty;
            try
            {
                JObject root = JObject.Parse(json);
                string format = root.Value<string>("format") ?? string.Empty;
                string schema = root.Value<string>("schemaVersion") ?? string.Empty;
                if (!string.Equals(format, PaletteBindingFormat, StringComparison.Ordinal)
                    || !string.Equals(schema, PaletteBindingSchemaVersion, StringComparison.Ordinal))
                {
                    bool legacy = format.StartsWith("mx.characterPaletteBinding.v", StringComparison.Ordinal);
                    issue = legacy
                        ? "FaceDetailTextureMigrationRequired: Palette Binding v1-v4 is rejected; save Color Palette Binding v5."
                        : "Palette binding requires format " + PaletteBindingFormat + " and schemaVersion " + PaletteBindingSchemaVersion + ".";
                    return null;
                }
                if (root["atlasResourceKey"] != null || root["materialClassSource"] != null || root["faceFeatureTokens"] != null
                    || root["faceDetailAtlasResourceKey"] != null || root["facePreset"] != null || root["facePresetCount"] != null
                    || root["faceDetailMode"] != null || root["faceFeatureColors"] != null
                    || root["profileRow"] != null || root["defaultRow"] != null || root["weaponPolicy"] != null)
                {
                    issue = "Palette binding v5 does not accept legacy Profile, FaceDetail Atlas/Preset/Mask, material-class, or weaponPolicy fields.";
                    return null;
                }
                string[] required = { "colorAtlasResourceKey", "addressingMode", "atlasWidth", "atlasHeight", "surfaceProfileSource", "surfaceProfileCount" };
                if (required.Any(name => root[name] == null))
                {
                    issue = "Palette binding v5 is missing a required Color Palette or Surface Profile field.";
                    return null;
                }
                CharacterPaletteBindingDocument document = root.ToObject<CharacterPaletteBindingDocument>();
                if (document == null) { issue = "Palette binding document is empty."; return null; }
                if (document.AddressingMode != PaletteAddressingMode
                    || document.AtlasWidth != WGameSurfacePaletteCatalog.Width
                    || document.AtlasHeight != WGameSurfacePaletteCatalog.Height
                    || document.SurfaceProfileSource != PaletteSurfaceProfileSource
                    || document.SurfaceProfileCount != WGameSurfacePaletteCatalog.SurfaceProfileCount)
                {
                    issue = "Palette binding v5 requires 256x256 colorUvSurfaceProfileVertexAlpha with paletteDataAlpha16.";
                    return null;
                }
                if (string.IsNullOrWhiteSpace(document.ColorAtlasResourceKey)) { issue = "colorAtlasResourceKey is required."; return null; }
                document.CullMode = string.IsNullOrWhiteSpace(document.CullMode) ? PaletteCullModeBack : document.CullMode.Trim();
                if (document.CullMode != PaletteCullModeOff && document.CullMode != PaletteCullModeFront && document.CullMode != PaletteCullModeBack)
                { issue = "Palette cullMode must be Off, Front, or Back."; return null; }
                if (document.FaceDetailEnabled)
                {
                    document.FaceDetailTextureResourceKey = (document.FaceDetailTextureResourceKey ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(document.FaceDetailTextureResourceKey))
                    { issue = "Enabled FaceDetail requires faceDetailTextureResourceKey."; return null; }
                    if (document.FaceDetailIntensity < 0f || document.FaceDetailIntensity > 1f || float.IsNaN(document.FaceDetailIntensity) || float.IsInfinity(document.FaceDetailIntensity))
                    { issue = "faceDetailIntensity must be from 0 through 1."; return null; }
                }
                return document;
            }
            catch (Exception ex) { issue = "Palette binding could not be parsed: " + ex.Message; return null; }
        }

        private static CharacterPaletteUsageDocument ReadPaletteUsageDocument(string importedPackageRoot, out string issue)
        {
            issue = string.Empty;
            string path = NormalizeProjectPath(importedPackageRoot) + "/" + PaletteUsageRelativePath;
            if (!File.Exists(Path.GetFullPath(path))) { issue = "PaletteRepaintRequired: character_palette_usage.json is missing."; return null; }
            try
            {
                CharacterPaletteUsageDocument usage = JObject.Parse(File.ReadAllText(Path.GetFullPath(path))).ToObject<CharacterPaletteUsageDocument>();
                if (usage == null || usage.Format != PaletteUsageFormat || usage.SchemaVersion != PaletteUsageSchemaVersion)
                { issue = "PaletteRepaintRequired: usage requires mx.characterPaletteUsage.v2 / 2.0."; return null; }
                usage.TokenUsages ??= new List<CharacterPaletteTokenUsage>();
                return usage;
            }
            catch (Exception ex) { issue = "Palette usage could not be read: " + ex.Message; return null; }
        }

        private static Texture2D ResolvePaletteAtlas(CharacterPaletteBindingDocument document, Dictionary<string, ResourcePreviewInfo> resources)
        {
            if (document == null) return null;
            if (document.ColorAtlasResourceKey == SharedPaletteResourceKey) return AssetDatabase.LoadAssetAtPath<Texture2D>(SharedPaletteAssetPath);
            if (resources != null && resources.TryGetValue(document.ColorAtlasResourceKey, out ResourcePreviewInfo resource)
                && resource != null && !string.IsNullOrWhiteSpace(resource.AssetPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(resource.AssetPath);
            return null;
        }

        private static Texture2D ResolveFaceDetailTexture(CharacterPaletteBindingDocument document, Dictionary<string, ResourcePreviewInfo> resources)
        {
            if (document == null || !document.FaceDetailEnabled) return null;
            if (resources != null && resources.TryGetValue(document.FaceDetailTextureResourceKey, out ResourcePreviewInfo resource)
                && resource != null
                && string.Equals(resource.Usage, "texture", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(resource.AssetPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(resource.AssetPath);
            return null;
        }

        private static CharacterBodyMaterialBindingStatus TryApplyPaletteBodyMaterialBinding(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            string importedPackageRoot,
            Transform bodyModel,
            out bool handled)
        {
            handled = false;
            CharacterPaletteBindingDocument document = ReadPaletteBindingDocument(importedPackageRoot, out string issue);
            bool configExists = File.Exists(Path.GetFullPath(NormalizeProjectPath(importedPackageRoot) + "/" + PaletteBindingRelativePath));
            if (document == null)
            {
                if (!configExists) return default;
                handled = true;
                throw new InvalidOperationException(issue);
            }
            if (!document.Enabled) return default;
            handled = true;
            if (!TryLoadPaletteAuthority(importedPackageRoot, document, out PaletteAuthority authority, out issue)) throw new InvalidOperationException(issue);
            Texture2D paletteAtlas = ResolvePaletteAtlas(document, resources);
            if (!TryValidatePaletteAtlas(paletteAtlas, authority, out issue)) throw new InvalidOperationException(issue);
            Texture2D faceDetailTexture = ResolveFaceDetailTexture(document, resources);
            if (document.FaceDetailEnabled && faceDetailTexture == null) throw new InvalidOperationException("Character face-detail texture could not be resolved.");
            if (document.FaceDetailEnabled && !TryValidateCharacterFaceDetailTexture(document, faceDetailTexture, package?.PackageId, out issue))
                throw new InvalidOperationException(issue);
            Shader shader = Shader.Find("WGame/CharacterPaletteLegacy") ?? throw new InvalidOperationException("Shader WGame/CharacterPaletteLegacy was not found.");
            Renderer[] renderers = GetBodyMaterialRenderers(bodyModel);
            if (renderers.Length == 0) throw new InvalidOperationException("BodyModel has no renderer.");
            if (!TryValidatePaletteMeshAndUsage(renderers, authority, document.FaceDetailEnabled, out issue)) throw new InvalidOperationException(issue);

            string materialPath = GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId);
            EnsureFolderPath(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = (package?.PackageId ?? "character") + "_Body_Palette" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else material.shader = shader;
            RemoveObsoleteCharacterSurfaceProperties(material, baked: false);
            material.DisableKeyword("_WG_BAKED_SURFACE_ON");
            material.EnableKeyword("_WG_PALETTE_ON");
            material.SetFloat(PaletteEnabledPropertyId, 1f);
            material.SetTexture(PaletteTexturePropertyId, paletteAtlas);
            material.SetVector(PaletteParamsPropertyId, new Vector4(256f, 256f, 1f / 256f, 1f / 256f));
            material.SetVector(PaletteDetailParamsPropertyId, new Vector4(.65f, 3f, 12f, 1f));
            material.SetFloat(CullPropertyId, ResolvePaletteCullMode(document.CullMode));
            material.doubleSidedGI = document.CullMode == PaletteCullModeOff;
            ApplyFaceDetailMaterial(document, material);
            EditorUtility.SetDirty(material);
            int assigned = AssignGeneratedBodyMaterial(renderers, material);
            return CharacterBodyMaterialBindingStatus.Create("bodyMaterialBound", PaletteCharacterMaterialTemplateId, materialPath, document.ColorAtlasResourceKey, assigned,
                "Bound RGB Color Palette and 16 Surface Profiles at Catalog " + authority.Catalog.CatalogHash + ".");
        }

        private static void ApplyFaceDetailMaterial(CharacterPaletteBindingDocument document, Material material)
        {
            material.DisableKeyword("_WG_FACE_DETAIL_COLOR_ON");
            RemoveLegacyFaceDetailMaterialProperties(material);
            if (document.FaceDetailEnabled)
            {
                material.EnableKeyword("_WG_FACE_DETAIL_ON");
                material.SetFloat(FaceDetailEnabledPropertyId, 1f);
                material.SetTexture(FaceDetailTexturePropertyId, null);
                material.SetFloat(FaceDetailIntensityPropertyId, document.FaceDetailIntensity);
            }
            else
            {
                material.DisableKeyword("_WG_FACE_DETAIL_ON");
                material.SetFloat(FaceDetailEnabledPropertyId, 0f);
                material.SetTexture(FaceDetailTexturePropertyId, null);
            }
        }

        private static void RemoveLegacyFaceDetailMaterialProperties(Material material)
        {
            if (material == null)
                return;

            foreach ((string arrayPath, string propertyName) in new[]
            {
                ("m_SavedProperties.m_TexEnvs", "_BaseMap"),
                ("m_SavedProperties.m_TexEnvs", "_WgNormalTex"),
                ("m_SavedProperties.m_TexEnvs", "_WgMaskTex"),
                ("m_SavedProperties.m_TexEnvs", "_WgEmissionMaskTex"),
                ("m_SavedProperties.m_Floats", "_AlphaClip"),
                ("m_SavedProperties.m_Floats", "_Cutoff"),
                ("m_SavedProperties.m_Floats", "_WgFaceDetailColorEnabled"),
                ("m_SavedProperties.m_Floats", "_WgFacePreset"),
                ("m_SavedProperties.m_Colors", "_WgFaceDetailParams"),
                ("m_SavedProperties.m_Colors", "_WgFaceFeatureColors"),
                ("m_SavedProperties.m_Colors", "_WgFaceFeatureTokens")
            })
            {
                var serialized = new SerializedObject(material);
                SerializedProperty entries = serialized.FindProperty(arrayPath);
                if (entries == null || !entries.isArray)
                    continue;
                for (int index = entries.arraySize - 1; index >= 0; index--)
                {
                    SerializedProperty key = entries.GetArrayElementAtIndex(index)?.FindPropertyRelative("first");
                    if (key != null && string.Equals(key.stringValue, propertyName, StringComparison.Ordinal))
                        entries.DeleteArrayElementAtIndex(index);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool TryValidateCharacterFaceDetailTexture(
            CharacterPaletteBindingDocument document,
            Texture2D texture,
            string packageId,
            out string issue)
        {
            issue = string.Empty;
            string expectedKey = "char." + (packageId ?? string.Empty) + ".texture.face_detail";
            if (!string.Equals(document.FaceDetailTextureResourceKey, expectedKey, StringComparison.Ordinal))
            { issue = "FaceDetail texture must use the current package key " + expectedKey + "."; return false; }
            if (texture == null || texture.width != 512 || texture.height != 512)
            { issue = "Character FaceDetail texture must be exactly 512x512 RGBA."; return false; }
            string assetPath = AssetDatabase.GetAssetPath(texture).Replace('\\', '/');
            string packageMarker = "/CharacterPackages/" + (packageId ?? string.Empty) + "/";
            if (string.IsNullOrWhiteSpace(packageId) || assetPath.IndexOf(packageMarker, StringComparison.OrdinalIgnoreCase) < 0)
            { issue = "FaceDetail texture must be owned by the current generated character package."; return false; }
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer))
            { issue = "Character FaceDetail texture has no TextureImporter."; return false; }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.isReadable = false;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 512;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            if (!importer.DoesSourceTextureHaveAlpha())
            { issue = "Character FaceDetail texture source must contain an alpha channel."; return false; }
            return true;
        }

        private static bool TryLoadPaletteAuthority(string importedPackageRoot, CharacterPaletteBindingDocument document, out PaletteAuthority authority, out string issue)
        {
            authority = null;
            WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out issue);
            if (catalog == null) return false;
            WGameColorPaletteAtlasReceipt receipt = WGameSurfacePaletteCatalog.ReadReceipt(SharedPaletteReceiptPath, out issue);
            if (receipt == null) return false;
            if (receipt.CatalogHash != catalog.CatalogHash
                || !File.Exists(Path.GetFullPath(SharedPaletteAssetPath))
                || receipt.AtlasHash != WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(Path.GetFullPath(SharedPaletteAssetPath))))
            { issue = "Color Palette Catalog, Atlas, and receipt have drifted; regenerate formal assets."; return false; }
            CharacterPaletteUsageDocument usage = ReadPaletteUsageDocument(importedPackageRoot, out issue);
            if (usage == null) return false;
            if (usage.CatalogHash != catalog.CatalogHash) { issue = "PaletteRepaintRequired: usage Catalog hash is stale."; return false; }
            if (usage.PaletteRepaintRequired || usage.InvalidFaceCount != 0) { issue = "PaletteRepaintRequired: all faces require a valid Color and Surface Profile."; return false; }
            foreach (CharacterPaletteTokenUsage item in usage.TokenUsages)
            {
                if (item == null || item.FaceCount <= 0 || item.ColorIndex < 0 || item.ColorIndex >= WGameSurfacePaletteCatalog.ColorCount
                    || !WGameSurfacePaletteCatalog.TryParseTokenId(catalog, item.TokenId, out int colorIndex, out int profile)
                    || colorIndex != item.ColorIndex || WGameSurfacePaletteCatalog.LogicalToken(colorIndex, profile) != item.LogicalToken
                    || catalog.SurfaceProfiles[profile].ProfileId != item.SurfaceProfileId)
                { issue = "Palette usage contains an invalid formula Token."; return false; }
            }
            authority = new PaletteAuthority(catalog, receipt, usage);
            return true;
        }

        private static bool TryValidatePaletteAtlas(Texture2D atlas, PaletteAuthority authority, out string issue)
        {
            issue = string.Empty;
            if (atlas == null || atlas.width != 256 || atlas.height != 256) { issue = "RGB Color Palette Atlas must be 256x256."; return false; }
            string path = AssetDatabase.GetAssetPath(atlas);
            var source = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
            try
            {
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(Path.GetFullPath(path)), false) || source.width != 256 || source.height != 256)
                { issue = "RGB Color Palette PNG could not be decoded at 256x256."; return false; }
                Color32[] actual = source.GetPixels32();
                Color32[] expected = WGameSurfacePaletteCatalog.BuildPixels(authority.Catalog);
                for (int index = 0; index < actual.Length; index++)
                {
                    if (actual[index].r != expected[index].r || actual[index].g != expected[index].g || actual[index].b != expected[index].b)
                    { issue = "Color Palette pixel differs from Catalog at colorIndex " + index + "."; return false; }
                }
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static bool TryValidatePaletteMeshAndUsage(Renderer[] renderers, PaletteAuthority authority, bool requireFaceDetailUv, out string issue)
        {
            issue = string.Empty;
            var actualFaces = new Dictionary<int, int>();
            var seenMeshes = new HashSet<Mesh>();
            string modelHash = string.Empty;
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer?.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !seenMeshes.Add(mesh)) continue;
                string assetPath = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrWhiteSpace(assetPath) && File.Exists(Path.GetFullPath(assetPath)))
                {
                    string hash = WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(Path.GetFullPath(assetPath)));
                    if (string.IsNullOrEmpty(modelHash)) modelHash = hash;
                    else if (modelHash != hash) { issue = "Palette usage supports one model hash per package."; return false; }
                }
                var uvs = new List<Vector2>(); mesh.GetUVs(1, uvs);
                var colors = new List<Color32>(); mesh.GetColors(colors);
                if (uvs.Count != mesh.vertexCount || colors.Count != mesh.vertexCount)
                { issue = "Palette mesh '" + mesh.name + "' requires UV1 and RGBA PaletteData for every vertex."; return false; }
                if (requireFaceDetailUv)
                {
                    var faceUvs = new List<Vector2>(); mesh.GetUVs(2, faceUvs);
                    if (faceUvs.Count != mesh.vertexCount) { issue = "Palette mesh requires FaceDetailUV when face details are enabled."; return false; }
                    for (int faceUvIndex = 0; faceUvIndex < faceUvs.Count; faceUvIndex++)
                    {
                        Vector2 uv = faceUvs[faceUvIndex];
                        bool sentinel = uv == new Vector2(-1f, -1f);
                        bool canonical = uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
                        if (!sentinel && !canonical)
                        { issue = "FaceDetailUV values must be canonical 0..1 or the (-1,-1) non-carrier sentinel."; return false; }
                    }
                }
                var logicalByVertex = new int[mesh.vertexCount];
                for (int vertex = 0; vertex < mesh.vertexCount; vertex++)
                {
                    int colorIndex = WGameSurfacePaletteCatalog.ColorIndexFromUv(uvs[vertex], out bool center);
                    if (!center || colorIndex < 0) { issue = "PaletteRepaintRequired: mesh '" + mesh.name + "' has invalid or non-center Color UV."; return false; }
                    int profile = WGameSurfacePaletteCatalog.DecodeSurfaceProfileAlpha(colors[vertex].a);
                    if (colors[vertex].a != WGameSurfacePaletteCatalog.EncodeSurfaceProfileAlpha(profile))
                    { issue = "PaletteData.A must equal one of the 16 Surface Profile alpha centers."; return false; }
                    logicalByVertex[vertex] = WGameSurfacePaletteCatalog.LogicalToken(colorIndex, profile);
                }
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles) { issue = "Palette mesh must use triangle topology."; return false; }
                    int[] indices = mesh.GetIndices(subMesh);
                    for (int triangle = 0; triangle + 2 < indices.Length; triangle += 3)
                    {
                        int token = logicalByVertex[indices[triangle]];
                        if (logicalByVertex[indices[triangle + 1]] != token || logicalByVertex[indices[triangle + 2]] != token)
                        { issue = "Palette mesh has a triangle with mixed Color or Surface Profile values."; return false; }
                        actualFaces[token] = actualFaces.TryGetValue(token, out int count) ? count + 1 : 1;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(modelHash) || modelHash != authority.Usage.ModelHash)
            { issue = "Palette usage modelHash does not match the imported model."; return false; }
            Dictionary<int, int> declared = authority.Usage.TokenUsages.GroupBy(item => item.LogicalToken).ToDictionary(group => group.Key, group => group.Sum(item => item.FaceCount));
            if (actualFaces.Count != declared.Count || actualFaces.Any(pair => !declared.TryGetValue(pair.Key, out int count) || count != pair.Value))
            { issue = "Palette usage face counts do not match the imported mesh."; return false; }
            return true;
        }

        private static float ResolvePaletteCullMode(string mode) => mode == PaletteCullModeOff ? 0f : mode == PaletteCullModeFront ? 1f : 2f;

        private static void AddPaletteBindingComponent(GameObject rootObject, Transform bodyModel, string importedPackageRoot, Dictionary<string, ResourcePreviewInfo> resources)
        {
            if (HasBakedSurface(importedPackageRoot))
            {
                CharacterPaletteBinding old = rootObject.GetComponent<CharacterPaletteBinding>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                return;
            }
            CharacterPaletteBindingDocument document = ReadPaletteBindingDocument(importedPackageRoot, out string issue);
            if (document == null || !document.Enabled) { if (!string.IsNullOrWhiteSpace(issue)) Debug.LogWarning("MxFramework Character Preview: Color Palette binding skipped. " + issue); return; }
            if (!TryLoadPaletteAuthority(importedPackageRoot, document, out _, out issue)) throw new InvalidOperationException(issue);
            CharacterPaletteBinding binding = rootObject.GetComponent<CharacterPaletteBinding>() ?? rootObject.AddComponent<CharacterPaletteBinding>();
            binding.Configure(ResolvePaletteAtlas(document, resources), document.AtlasWidth, document.AtlasHeight, bodyModel,
                document.FaceDetailEnabled, ResolveFaceDetailTexture(document, resources), document.FaceDetailIntensity);
            EditorUtility.SetDirty(binding);
        }

        private static CharacterBodyMaterialBindingStatus CollectPaletteBodyMaterialBindingStatus(string importedPackageRoot, CharacterImportedPackage package, GameObject prefab, out bool handled)
        {
            handled = false;
            if (HasBakedSurface(importedPackageRoot))
            {
                handled = true;
                try
                {
                    ReadBakedSurface(importedPackageRoot);
                    string path = GetGeneratedBodyMaterialPath(importedPackageRoot, package.PackageId);
                    Material bakedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                    Renderer[] targets = GetBodyMaterialRenderers(prefab != null ? FindChildByName(prefab.transform, "BodyModel") : null);
                    int count = CountRenderersUsingMaterial(targets, bakedMaterial, out bool assigned);
                    bool valid = assigned && bakedMaterial != null && bakedMaterial.shader != null
                        && bakedMaterial.shader.name == "WGame/CharacterStylizedLit"
                        && bakedMaterial.GetTexture("_WgBakedBaseColor") != null
                        && bakedMaterial.GetTexture("_WgSurfaceDataTex") != null;
                    return CharacterBodyMaterialBindingStatus.Create(valid ? "bodyMaterialBound" : "bodyMaterialNotAssigned",
                        BakedSurfaceTemplate, path, "char." + package.PackageId + ".texture.baked_base_color", count, valid ? "Baked surface ready." : "Baked surface material is not assigned.");
                }
                catch (Exception ex) { return CharacterBodyMaterialBindingStatus.Create("bakedSurfaceInvalid", BakedSurfaceTemplate, string.Empty, string.Empty, 0, ex.Message); }
            }
            CharacterPaletteBindingDocument document = ReadPaletteBindingDocument(importedPackageRoot, out string issue);
            bool exists = File.Exists(Path.GetFullPath(NormalizeProjectPath(importedPackageRoot) + "/" + PaletteBindingRelativePath));
            if (document == null) { if (!exists) return default; handled = true; return CharacterBodyMaterialBindingStatus.Create("paletteConfigInvalid", PaletteCharacterMaterialTemplateId, string.Empty, string.Empty, 0, issue); }
            if (!document.Enabled) return default;
            handled = true;
            if (!TryLoadPaletteAuthority(importedPackageRoot, document, out PaletteAuthority authority, out issue))
                return CharacterBodyMaterialBindingStatus.Create("paletteAuthorityInvalid", PaletteCharacterMaterialTemplateId, string.Empty, document.ColorAtlasResourceKey, 0, issue);
            string materialPath = GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Renderer[] renderers = GetBodyMaterialRenderers(prefab != null ? FindChildByName(prefab.transform, "BodyModel") : null);
            CharacterPaletteBinding binding = prefab != null ? prefab.GetComponent<CharacterPaletteBinding>() : null;
            if (material == null || binding == null) return CharacterBodyMaterialBindingStatus.Create("paletteBindingMissing", PaletteCharacterMaterialTemplateId, materialPath, document.ColorAtlasResourceKey, 0, "Generated material or binding is missing.");
            int rendererCount = CountRenderersUsingMaterial(renderers, material, out bool allAssigned);
            return allAssigned
                ? CharacterBodyMaterialBindingStatus.Create("bodyMaterialBound", PaletteCharacterMaterialTemplateId, materialPath, document.ColorAtlasResourceKey, rendererCount, "RGB565 Color Palette v3 is ready at " + authority.Catalog.CatalogHash + ".")
                : CharacterBodyMaterialBindingStatus.Create("bodyMaterialNotAssigned", PaletteCharacterMaterialTemplateId, materialPath, document.ColorAtlasResourceKey, rendererCount, "Color Palette material is not assigned to every body renderer slot.");
        }

        private static void PopulatePaletteBuildResult(CharacterImportedPackagePrefabBuildResult result, string importedPackageRoot, GameObject prefab)
        {
            if (result == null) return;
            if (HasBakedSurface(importedPackageRoot)) { result.PaletteDiagnostic = "Baked surface: authoring palette and detail layers are compiled into BaseColor."; return; }
            CharacterPaletteBindingDocument document = ReadPaletteBindingDocument(importedPackageRoot, out string issue);
            if (document == null || !document.Enabled) { result.PaletteDiagnostic = issue ?? string.Empty; return; }
            CharacterPaletteBinding binding = prefab != null ? prefab.GetComponent<CharacterPaletteBinding>() : null;
            result.PaletteAtlasResourceKey = document.ColorAtlasResourceKey;
            if (TryLoadPaletteAuthority(importedPackageRoot, document, out PaletteAuthority authority, out string authorityIssue))
            {
                result.PaletteCatalogHash = authority.Catalog.CatalogHash;
                result.PaletteColorCount = WGameSurfacePaletteCatalog.ColorCount;
                result.SurfaceProfileCount = WGameSurfacePaletteCatalog.SurfaceProfileCount;
                result.PaletteInvalidTokenCount = authority.Usage.InvalidFaceCount;
            }
            result.PaletteRendererCount = binding != null ? result.BodyMaterialRendererCount : 0;
            result.PaletteDiagnostic = binding == null ? "CharacterPaletteBinding is missing."
                : binding.ColorPaletteAtlas == null ? "Shared RGB Color Palette is missing." : authorityIssue ?? string.Empty;
            result.FaceDetailTextureResourceKey = document.FaceDetailEnabled ? document.FaceDetailTextureResourceKey : string.Empty;
            Texture2D faceDetailTexture = binding != null ? binding.FaceDetailTexture : null;
            if (document.FaceDetailEnabled && faceDetailTexture != null)
            {
                string facePath = AssetDatabase.GetAssetPath(faceDetailTexture);
                result.FaceDetailTextureHash = !string.IsNullOrWhiteSpace(facePath) && File.Exists(Path.GetFullPath(facePath))
                    ? WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(Path.GetFullPath(facePath)))
                    : string.Empty;
                result.FaceDetailTextureSize = faceDetailTexture.width;
                result.FaceDetailDiagnostic = faceDetailTexture.width == 512 && faceDetailTexture.height == 512
                    ? "Character-owned 512x512 Direct RGBA FaceDetail texture is ready."
                    : "Character FaceDetail texture has an invalid size.";
            }
            else if (document.FaceDetailEnabled)
            {
                result.FaceDetailDiagnostic = "Character FaceDetail texture is missing.";
            }
        }

        private sealed class PaletteAuthority
        {
            public PaletteAuthority(WGameSurfacePaletteCatalogDocument catalog, WGameColorPaletteAtlasReceipt receipt, CharacterPaletteUsageDocument usage)
            { Catalog = catalog; Receipt = receipt; Usage = usage; }
            public WGameSurfacePaletteCatalogDocument Catalog { get; }
            public WGameColorPaletteAtlasReceipt Receipt { get; }
            public CharacterPaletteUsageDocument Usage { get; }
        }
    }
}
