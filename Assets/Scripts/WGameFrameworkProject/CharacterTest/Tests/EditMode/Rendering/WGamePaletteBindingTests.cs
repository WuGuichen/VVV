using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor;
using MxFramework.Editor.CharacterImport;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGamePaletteBindingTests
    {
        [TestCase("WGame/CharacterPaletteLegacy")]
        [TestCase("WGame/PropStylizedLit")]
        public void Shader_ExposesPaletteProperties(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.NotNull(shader, shaderName);
            using (var material = new MaterialScope(shader))
            {
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteEnabled"));
                Assert.IsTrue(material.Material.HasProperty("_WgColorPaletteTex"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteRow"));
                Assert.IsTrue(material.Material.HasProperty("_WgColorPaletteParams"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteTex"));
            }
        }

        [Test]
        public void CharacterShader_ExposesCharacterFaceDetailProperties()
        {
            Shader shader = Shader.Find("WGame/CharacterPaletteLegacy");
            Assert.NotNull(shader);
            using (var material = new MaterialScope(shader))
            {
                Assert.IsTrue(material.Material.HasProperty("_WgFaceDetailEnabled"));
                Assert.IsTrue(material.Material.HasProperty("_WgFaceDetailTex"));
                Assert.IsTrue(material.Material.HasProperty("_WgFaceDetailIntensity"));
                Assert.IsFalse(material.Material.HasProperty("_WgFacePreset"));
                Assert.IsFalse(material.Material.HasProperty("_WgFaceDetailParams"));
                Assert.IsFalse(material.Material.HasProperty("_WgFaceFeatureColors"));
                Assert.IsTrue(material.Material.HasProperty("_Cull"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteClassShadowFloor"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteClassWrap"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteClassAo"));
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteIndirectParams"));
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteIndirectNormalInfluence"));
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteAdditionalLightParams"));
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteSkyTint"));
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteGroundTint"));
                Assert.IsTrue(material.Material.HasProperty("_WgPaletteDetailParams"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteDetailScale"));
                Assert.IsFalse(material.Material.HasProperty("_WgPaletteDetailStrength"));
            }
        }

        [Test]
        public void FaceDetail_UsesDirectCharacterTextureUv()
        {
            const string Path = "Assets/Art/WGame/Shaders/ShaderLibrary/WGameFaceDetail.hlsl";
            string source = File.ReadAllText(Path);
            StringAssert.Contains("WgFaceDetailValidity", source);
            StringAssert.DoesNotContain("WgFaceDetailAtlasUv", source);
            string shader = File.ReadAllText("Assets/Art/WGame/Shaders/Character/CharacterPaletteLegacy.shader");
            StringAssert.Contains("sampler_WgFaceDetailTex,\n                                input.faceDetailUv", shader);
            StringAssert.DoesNotContain("_WG_FACE_DETAIL_COLOR_ON", shader);
            StringAssert.DoesNotContain("WgCharacterPaletteColor", shader);
        }

        [Test]
        public void CharacterPaletteLighting_UsesSixteenSurfaceProfilesAndPreservesHdrOutput()
        {
            const string PalettePath = "Assets/Art/WGame/Shaders/ShaderLibrary/WGamePalette.hlsl";
            const string PaletteDetailPath = "Assets/Art/WGame/Shaders/ShaderLibrary/WGamePaletteDetail.hlsl";
            const string ProfilePath = "Assets/Art/WGame/Shaders/ShaderLibrary/WGameSurfaceProfiles.generated.hlsl";
            const string CharacterShaderPath = "Assets/Art/WGame/Shaders/Character/CharacterPaletteLegacy.shader";
            const string PropShaderPath = "Assets/Art/WGame/Shaders/Props/PropStylizedLit.shader";
            const string GuiPath = "Assets/Scripts/WGameFrameworkProject/Editor/WGameCharacterPaletteLegacyShaderGui.cs";
            string paletteSource = File.ReadAllText(PalettePath);
            string detailSource = File.ReadAllText(PaletteDetailPath);
            string profileSource = File.ReadAllText(ProfilePath);
            string shaderSource = File.ReadAllText(CharacterShaderPath);
            string propShaderSource = File.ReadAllText(PropShaderPath);
            string guiSource = File.ReadAllText(GuiPath);

            StringAssert.Contains("WgEvaluateReadablePaletteThreeBand", paletteSource);
            StringAssert.Contains("WgDecodeSurfaceProfile", paletteSource);
            StringAssert.Contains("WgColorPaletteUvFromIndex", paletteSource);
            StringAssert.Contains("WgColorPaletteIndex", paletteSource);
            StringAssert.Contains("WgColorPaletteUvIsValid", paletteSource);
            StringAssert.Contains("WgEvaluatePaletteAdditionalLight", paletteSource);
            StringAssert.Contains("WgSurfaceProfileShadowFloor", paletteSource);
            StringAssert.Contains("WgSurfaceProfileMetallic", paletteSource);
            StringAssert.Contains("WgApplyPaletteCharacterMaterialResponse", paletteSource);
            StringAssert.Contains("WgApplyPaletteMaterialResponse", paletteSource);
            StringAssert.Contains("WgSurfaceProfileSelect", profileSource);
            StringAssert.Contains("WG_SURFACE_PROFILE_COUNT = 16.0", profileSource);
            StringAssert.Contains("WgEvaluatePaletteProceduralDetail", detailSource);
            StringAssert.Contains("WgEvaluatePaletteProceduralDetail", shaderSource);
            StringAssert.Contains("WgDecodePaletteVertexData(input.color)", shaderSource);
            StringAssert.Contains("WgDecodePaletteVertexData(input.color)", propShaderSource);
            StringAssert.Contains("WgDecodeSurfaceProfile(vertexColor.a)", paletteSource);
            StringAssert.DoesNotContain("paletteSample.a", paletteSource);
            StringAssert.Contains("float2 paletteSampleUv = input.paletteUv.xy;", shaderSource);
            StringAssert.Contains("float2 paletteSampleUv = input.paletteUv.xy;", propShaderSource);
            StringAssert.Contains("SAMPLE_TEXTURE2D_LOD(_WgColorPaletteTex", shaderSource);
            StringAssert.Contains("SAMPLE_TEXTURE2D_LOD(_WgColorPaletteTex", propShaderSource);
            StringAssert.Contains("return float4(1.0, 0.0, 1.0, 1.0);", shaderSource);
            StringAssert.DoesNotContain("_WgPaletteRow", shaderSource);
            StringAssert.DoesNotContain("_WgPaletteRow", propShaderSource);
            StringAssert.DoesNotContain("materialClass", paletteSource);
            StringAssert.DoesNotContain("_WgPaletteClass", shaderSource);
            StringAssert.Contains("float4 positionWS : TEXCOORD0;", shaderSource);
            StringAssert.Contains("float4 paletteUv : TEXCOORD4;", shaderSource);
            StringAssert.Contains(
                "float3 positionOS = float3(input.paletteUv.z, input.positionWS.w, input.paletteUv.w);",
                shaderSource);
            StringAssert.Contains("emission * 20.0", detailSource);
            StringAssert.Contains("fwidth(coordinate)", detailSource);
            StringAssert.Contains("distanceFade > WG_EPSILON", detailSource);
            StringAssert.Contains("float scratchMask", detailSource);
            StringAssert.Contains("WgSurfaceProfileDetailMode(surfaceProfile)", detailSource);
            StringAssert.Contains("WgPaletteDetailModeMask(detailMode, 7.0) * frost", detailSource);
            StringAssert.Contains("WgPaletteDetailModeMask(detailMode, 8.0) * crystal", detailSource);
            StringAssert.DoesNotContain("SAMPLE_TEXTURE", detailSource);
            StringAssert.DoesNotContain("input.uv", detailSource);
            StringAssert.DoesNotContain("WgPaletteDetailHash", detailSource);
            StringAssert.DoesNotContain("sin(", detailSource);
            StringAssert.Contains("detailParams.w > 0.5 ? WgPaletteDetailTriangle(coordinate1) : 0.0", detailSource);
            StringAssert.Contains("float specularSignal = diffuseSignal", detailSource);
            StringAssert.DoesNotContain("WgPaletteDetailModeMask(detailMode, 6.0) * brushed", detailSource.Substring(
                detailSource.IndexOf("float diffuseSignal", StringComparison.Ordinal),
                detailSource.IndexOf("float specularSignal", StringComparison.Ordinal) - detailSource.IndexOf("float diffuseSignal", StringComparison.Ordinal)),
                "Brushed metal must remain specular-only.");
            // The existing ShadowCaster and MotionVectors passes are independent of palette detail.
            string[] expectedPasses = { "Forward", "ShadowCaster", "DepthOnly", "DepthNormals", "MotionVectors" };
            string[] actualPasses = System.Text.RegularExpressions.Regex.Matches(
                    shaderSource, @"(?m)^\s*Name\s+""([^""]+)""\s*$")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();
            CollectionAssert.AreEqual(expectedPasses, actualPasses,
                "Palette detail must retain exactly the existing rendering passes.");
            Assert.AreEqual(
                expectedPasses.Length,
                System.Text.RegularExpressions.Regex.Matches(shaderSource, @"(?m)^\s*Pass\s*$").Count,
                "Palette detail must not add an unnamed rendering pass.");
            StringAssert.Contains("#pragma multi_compile _ _CLUSTER_LIGHT_LOOP", shaderSource);
            StringAssert.Contains("LIGHT_LOOP_BEGIN(pixelLightCount)", shaderSource);
            StringAssert.Contains("#include \"../ShaderLibrary/WGameSceneLighting.hlsl\"", shaderSource);
            StringAssert.Contains("WgSceneDiffuse(surfaceColor, normalWS, positionWS,", shaderSource);
            StringAssert.Contains("attenuation, paletteVertex.ao);", shaderSource);
            StringAssert.Contains("WgSceneDiffuse(_Color.rgb, normalWS, input.positionWS.xyz,", shaderSource);
            StringAssert.Contains(
                "CustomEditor \"MxFramework.Project.Editor.WGameCharacterPaletteLegacyShaderGui\"",
                shaderSource);
            StringAssert.Contains("return float4(max(color, 0.0), 1.0);", shaderSource);
            StringAssert.Contains("return float4(saturate(color), 1.0);", propShaderSource);
            StringAssert.Contains("Surface Profile Response", guiSource);
            StringAssert.Contains("256 x 256 RGB · all cells valid", guiSource);
            StringAssert.DoesNotContain("_WgPaletteTex", guiSource);
            StringAssert.DoesNotContain("Material Class Response", guiSource);
        }

        [Test]
        public void SurfaceProfileCatalog_DefaultsCoverSkinHairFabricLeatherWoodMetalAndCrystal()
        {
            WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out string issue);
            Assert.NotNull(catalog, issue);
            Assert.AreEqual(16, catalog.SurfaceProfiles.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    "skin_soft", "skin_glossy", "hair_matte", "hair_sheen",
                    "fabric_woven", "fabric_silk", "leather_rough", "leather_polished",
                    "wood_raw", "wood_varnished", "matte_solid_rough", "matte_solid_smooth",
                    "metal_brushed", "metal_polished", "crystal_clear", "crystal_frosted"
                },
                catalog.SurfaceProfiles.ConvertAll(profile => profile.ProfileId));
            Assert.That(catalog.SurfaceProfiles[0].ShadowFloor, Is.GreaterThan(catalog.SurfaceProfiles[4].ShadowFloor));
            Assert.That(catalog.SurfaceProfiles[0].Wrap, Is.GreaterThan(catalog.SurfaceProfiles[13].Wrap));
            Assert.That(catalog.SurfaceProfiles[0].Ao, Is.LessThan(catalog.SurfaceProfiles[6].Ao));
            Assert.That(catalog.SurfaceProfiles[13].Specular, Is.GreaterThan(catalog.SurfaceProfiles[12].Specular));
            Assert.That(catalog.SurfaceProfiles[14].Specular, Is.GreaterThan(catalog.SurfaceProfiles[13].Specular));
            Assert.That(catalog.SurfaceProfiles[15].Roughness, Is.GreaterThan(catalog.SurfaceProfiles[14].Roughness));
            Assert.AreEqual(0, catalog.SurfaceProfiles[0].DetailModeIndex);
            Assert.AreEqual(6, catalog.SurfaceProfiles[12].DetailModeIndex);
            Assert.AreEqual(7, catalog.SurfaceProfiles[15].DetailModeIndex);
            for (int index = 0; index < catalog.SurfaceProfiles.Count; index++)
                Assert.AreEqual(8 + index * 16, catalog.SurfaceProfiles[index].AlphaByte);
        }

        [Test]
        public void Binding_AppliesGlobalAtlasWithoutProfileState()
        {
            Shader shader = Shader.Find("WGame/CharacterPaletteLegacy");
            Assert.NotNull(shader);
            var root = new GameObject("PaletteBindingTest");
            var mesh = new Mesh();
            var atlas = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var material = new Material(shader);
            try
            {
                material.EnableKeyword("_WG_PALETTE_ON");
                material.SetTexture("_WgColorPaletteTex", atlas);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                CharacterPaletteBinding binding = root.AddComponent<CharacterPaletteBinding>();
                binding.Configure(atlas, 256, 256, root.transform);

                Assert.AreEqual(256, binding.AtlasWidth);
                Assert.AreEqual(256, binding.AtlasHeight);
                Assert.AreEqual(1, binding.BoundRendererSlotCount);
                Assert.AreSame(material, renderer.sharedMaterial, "Palette binding must not instantiate a material.");
                Assert.IsNull(typeof(CharacterPaletteBinding).GetMethod("SetPaletteRow"));
                Assert.IsNull(typeof(CharacterPaletteBinding).GetMethod("SetPaletteAtlas"));
                Assert.IsNull(typeof(CharacterPaletteBinding).GetMethod("ConfigureProfile"));

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Assert.AreEqual(1f, block.GetFloat(Shader.PropertyToID("_WgPaletteEnabled")));
                Assert.AreSame(atlas, block.GetTexture(Shader.PropertyToID("_WgColorPaletteTex")));
                Vector4 parameters = block.GetVector(Shader.PropertyToID("_WgColorPaletteParams"));
                Assert.AreEqual(new Vector4(256f, 256f, 1f / 256f, 1f / 256f), parameters);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void PaletteConfigParser_AcceptsColorProfileSchemaAndRejectsLegacyBindings()
        {
            const string Valid = "{\"format\":\"mx.characterPaletteBinding.v5\",\"schemaVersion\":\"5.0\",\"enabled\":true,\"colorAtlasResourceKey\":\"wgame.character.color_palette.shared\",\"addressingMode\":\"colorUvSurfaceProfileVertexAlpha\",\"atlasWidth\":256,\"atlasHeight\":256,\"surfaceProfileSource\":\"paletteDataAlpha16\",\"surfaceProfileCount\":16,\"faceDetailEnabled\":true,\"faceDetailTextureResourceKey\":\"char.test.texture.face_detail\",\"faceDetailIntensity\":0.8,\"cullMode\":\"Off\"}";
            CharacterImportedPackagePrefabBuilder.CharacterPaletteBindingDocument valid =
                CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(Valid, out string validIssue);

            Assert.NotNull(valid, validIssue);
            Assert.AreEqual(CharacterImportedPackagePrefabBuilder.PaletteAddressingMode, valid.AddressingMode);
            Assert.AreEqual(256, valid.AtlasWidth);
            Assert.AreEqual(256, valid.AtlasHeight);
            Assert.AreEqual(CharacterImportedPackagePrefabBuilder.PaletteSurfaceProfileSource, valid.SurfaceProfileSource);
            Assert.AreEqual(CharacterImportedPackagePrefabBuilder.SharedPaletteResourceKey, valid.ColorAtlasResourceKey);
            Assert.AreEqual(16, valid.SurfaceProfileCount);
            Assert.IsTrue(valid.FaceDetailEnabled);
            Assert.AreEqual("char.test.texture.face_detail", valid.FaceDetailTextureResourceKey);
            Assert.AreEqual(0.8f, valid.FaceDetailIntensity);
            Assert.AreEqual(CharacterImportedPackagePrefabBuilder.PaletteCullModeOff, valid.CullMode);
            Assert.IsEmpty(validIssue);

            string missingFaceTextureJson = Valid.Replace(",\"faceDetailTextureResourceKey\":\"char.test.texture.face_detail\"", string.Empty);
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(missingFaceTextureJson, out string missingFaceTextureIssue));
            StringAssert.Contains("faceDetailTextureResourceKey", missingFaceTextureIssue);

            const string LegacyV1 = "{\"format\":\"mx.characterPaletteBinding.v1\",\"schemaVersion\":\"1.0\",\"enabled\":true,\"defaultRow\":3}";
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(LegacyV1, out string legacyV1Issue));
            StringAssert.Contains("FaceDetailTextureMigrationRequired", legacyV1Issue);

            const string LegacyV2 = "{\"format\":\"mx.characterPaletteBinding.v2\",\"schemaVersion\":\"2.0\",\"enabled\":true,\"profileRow\":3}";
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(LegacyV2, out string legacyV2Issue));
            StringAssert.Contains("FaceDetailTextureMigrationRequired", legacyV2Issue);

            const string LegacyV3 = "{\"format\":\"mx.characterPaletteBinding.v3\",\"schemaVersion\":\"3.0\",\"enabled\":true,\"addressingMode\":\"globalTokenUv\"}";
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(LegacyV3, out string legacyV3Issue));
            StringAssert.Contains("FaceDetailTextureMigrationRequired", legacyV3Issue);
            const string LegacyV4 = "{\"format\":\"mx.characterPaletteBinding.v4\",\"schemaVersion\":\"4.0\",\"enabled\":true}";
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(LegacyV4, out string legacyV4Issue));
            StringAssert.Contains("FaceDetailTextureMigrationRequired", legacyV4Issue);

            string profileInjected = Valid.Replace("\"addressingMode\"", "\"profileRow\":3,\"addressingMode\"");
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(profileInjected, out string profileIssue));
            StringAssert.Contains("legacy Profile", profileIssue);

            string invalidDimensions = Valid.Replace("\"atlasWidth\":256", "\"atlasWidth\":128");
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(invalidDimensions, out string dimensionsIssue));
            StringAssert.Contains("256x256", dimensionsIssue);

            string invalidProfileSource = Valid.Replace("paletteDataAlpha16", "paletteAlpha");
            CharacterImportedPackagePrefabBuilder.CharacterPaletteBindingDocument rejectedProfileSource =
                CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(invalidProfileSource, out string profileSourceIssue);
            Assert.IsNull(rejectedProfileSource);
            StringAssert.Contains("paletteDataAlpha16", profileSourceIssue);

            string missingProfileSource = Valid.Replace(",\"surfaceProfileSource\":\"paletteDataAlpha16\"", string.Empty);
            CharacterImportedPackagePrefabBuilder.CharacterPaletteBindingDocument rejectedMissingProfileSource =
                CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(missingProfileSource, out string missingProfileSourceIssue);
            Assert.IsNull(rejectedMissingProfileSource);
            StringAssert.Contains("required", missingProfileSourceIssue);

            string invalidIntensity = Valid.Replace("\"faceDetailIntensity\":0.8", "\"faceDetailIntensity\":1.2");
            Assert.IsNull(CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(invalidIntensity, out string intensityIssue));
            StringAssert.Contains("faceDetailIntensity", intensityIssue);

            string invalidCull = Valid.Replace("\"cullMode\":\"Off\"", "\"cullMode\":\"Both\"");
            CharacterImportedPackagePrefabBuilder.CharacterPaletteBindingDocument rejectedCull =
                CharacterImportedPackagePrefabBuilder.ParsePaletteBindingJson(invalidCull, out string cullIssue);
            Assert.IsNull(rejectedCull);
            StringAssert.Contains("cullMode", cullIssue);

        }

        [Test]
        public void ColorCatalog_UsesUniqueBidirectionalRgb565Addresses()
        {
            WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out string issue);
            Assert.NotNull(catalog, issue);
            byte[] rgb = WGameSurfacePaletteCatalog.DecodeRgb8(catalog);
            var unique = new System.Collections.Generic.HashSet<int>();
            for (int colorIndex = 0; colorIndex < WGameSurfacePaletteCatalog.ColorCount; colorIndex++)
            {
                WGameSurfacePaletteCatalog.DecodeRgb565(colorIndex, out int red5, out int green6, out int blue5);
                Assert.AreEqual(colorIndex, WGameSurfacePaletteCatalog.ColorIndexFromRgb565(red5, green6, blue5));
                Color32 expected = WGameSurfacePaletteCatalog.FormulaColor(colorIndex);
                int offset = colorIndex * 3;
                Assert.AreEqual(expected.r, rgb[offset]);
                Assert.AreEqual(expected.g, rgb[offset + 1]);
                Assert.AreEqual(expected.b, rgb[offset + 2]);
                Assert.AreEqual(colorIndex, WGameSurfacePaletteCatalog.ColorIndexFromRgb8(expected.r, expected.g, expected.b));
                Assert.IsTrue(unique.Add((expected.r << 16) | (expected.g << 8) | expected.b), "Duplicate RGB8 at " + colorIndex);
            }
            Assert.AreEqual(65_536, unique.Count);
            Assert.AreEqual("wgame.color.v3.r00.g00.b00", WGameSurfacePaletteCatalog.ColorId(0));
            Assert.AreEqual("wgame.color.v3.r31.g63.b31", WGameSurfacePaletteCatalog.ColorId(65_535));
        }

        [Test]
        public void SharedPaletteAtlas_HasFixedImportContract()
        {
            const string Path = "Assets/Art/WGame/Textures/Character/WGameColorPaletteAtlas.png";
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
            Assert.NotNull(atlas);
            Assert.AreEqual(256, atlas.width);
            Assert.AreEqual(256, atlas.height);

            var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
            Assert.NotNull(importer);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(TextureImporterAlphaSource.None, importer.alphaSource);
            Assert.IsFalse(importer.alphaIsTransparency);

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(source, File.ReadAllBytes(Path), false));
                Color32[] pixels = source.GetPixels32();
                Assert.AreEqual(256 * 256, pixels.Length);
                WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out string issue);
                Assert.NotNull(catalog, issue);
                byte[] rgb8 = WGameSurfacePaletteCatalog.DecodeRgb8(catalog);
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    Assert.AreEqual(rgb8[index * 3], pixel.r, "R at " + index);
                    Assert.AreEqual(rgb8[index * 3 + 1], pixel.g, "G at " + index);
                    Assert.AreEqual(rgb8[index * 3 + 2], pixel.b, "B at " + index);
                    Assert.AreEqual(255, pixel.a, "Color Atlas has no material alpha at " + index);
                }
                WGameColorPaletteAtlasReceipt receipt = WGameSurfacePaletteCatalog.ReadReceipt(
                    WGameCharacterPaletteAssetGenerator.ReceiptPath,
                    out string receiptIssue);
                Assert.NotNull(receipt, receiptIssue);
                Assert.AreEqual(catalog.CatalogHash, receipt.CatalogHash);
                Assert.AreEqual(65536, receipt.ColorCount);
                Assert.AreEqual(16, receipt.SurfaceProfileCount);
                Assert.AreEqual("RGB24", receipt.TextureFormat);
                Assert.AreEqual(WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(Path)), receipt.AtlasHash);
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>(WGameCharacterPaletteAssetGenerator.LegacyTexturePath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SharedFaceDetailAtlas_IsRetired()
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/WGame/Textures/Character/WGameFaceDetailAtlas.png"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/WGame/Textures/Character/WGameFaceDetailAtlas_FaceParts_RGBA.png"));
        }

        [Test]
        public void FaceDetailTestTexture_Is512DirectRgba()
        {
            const string Path = "Assets/Art/WGame/Textures/Character/WGameFaceDetailTestRGBA.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(Path), false));
                Assert.AreEqual(512, texture.width);
                Assert.AreEqual(512, texture.height);
                Assert.IsTrue(texture.GetPixels32().Any(pixel => pixel.a < 255), "FaceDetail source must contain coverage alpha.");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void FaceDetailTestTexture_HasFixedImportContract()
        {
            const string Path = "Assets/Art/WGame/Textures/Character/WGameFaceDetailTestRGBA.png";
            string[] diagnostics = CharacterUnityResourceCatalogUpdater.NormalizeFaceDetailTextureImporterForTests(Path);
            Assert.IsEmpty(diagnostics);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
            Assert.NotNull(texture);
            Assert.AreEqual(512, texture.width);
            Assert.AreEqual(512, texture.height);
            var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
            Assert.NotNull(importer);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.IsTrue(importer.mipmapEnabled);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(512, importer.maxTextureSize);
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.IsTrue(android.overridden);
            Assert.AreEqual(512, android.maxTextureSize);
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, android.format);
        }

        [Test]
        public void FaceDetailTestTexture_NormalizesEyeAndMouthLandmarks()
        {
            const string Path = "Assets/Art/WGame/Textures/Character/WGameFaceDetailTestRGBA.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(Path), false));
                const int gridSize = 1;
                const int stride = 4;
                int tileSize = texture.width / gridSize;
                Color32[] pixels = texture.GetPixels32();
                var leftEyes = new Vector2[gridSize * gridSize];
                var rightEyes = new Vector2[gridSize * gridSize];
                var mouths = new Vector2[gridSize * gridSize];

                for (int preset = 0; preset < leftEyes.Length; preset++)
                {
                    int tileColumn = preset % gridSize;
                    int tileRow = preset / gridSize;
                    Vector2 leftSum = Vector2.zero;
                    Vector2 rightSum = Vector2.zero;
                    Vector2 mouthSum = Vector2.zero;
                    float leftWeight = 0f;
                    float rightWeight = 0f;
                    float mouthWeight = 0f;
                    for (int localY = 0; localY < tileSize; localY += stride)
                    {
                        for (int localX = 0; localX < tileSize; localX += stride)
                        {
                            int x = tileColumn * tileSize + localX;
                            int y = tileRow * tileSize + localY;
                            Color32 pixel = pixels[y * texture.width + x];
                            var localUv = new Vector2(
                                (localX + 0.5f) / tileSize,
                                (localY + 0.5f) / tileSize);
                            float green = pixel.g / 255f;
                            float blue = pixel.b / 255f;
                            if (localUv.x < 0.5f)
                            {
                                leftSum += localUv * green;
                                leftWeight += green;
                            }
                            else
                            {
                                rightSum += localUv * green;
                                rightWeight += green;
                            }

                            mouthSum += localUv * blue;
                            mouthWeight += blue;
                        }
                    }

                    Assert.Greater(leftWeight, 1f, "Preset " + preset + " is missing the left iris marker.");
                    Assert.Greater(rightWeight, 1f, "Preset " + preset + " is missing the right iris marker.");
                    Assert.Greater(mouthWeight, 1f, "Preset " + preset + " is missing the mouth marker.");
                    leftEyes[preset] = leftSum / leftWeight;
                    rightEyes[preset] = rightSum / rightWeight;
                    mouths[preset] = mouthSum / mouthWeight;
                }

                for (int preset = 1; preset < leftEyes.Length; preset++)
                {
                    Assert.Less(Vector2.Distance(leftEyes[0], leftEyes[preset]), 0.02f, "Left iris drift in preset " + preset + ".");
                    Assert.Less(Vector2.Distance(rightEyes[0], rightEyes[preset]), 0.02f, "Right iris drift in preset " + preset + ".");
                    Assert.Less(Vector2.Distance(mouths[0], mouths[preset]), 0.02f, "Mouth drift in preset " + preset + ".");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Binding_AppliesCharacterFaceDetailTextureThroughPropertyBlock()
        {
            Shader shader = Shader.Find("WGame/CharacterPaletteLegacy");
            Assert.NotNull(shader);
            var root = new GameObject("FaceDetailBindingTest");
            root.SetActive(false);
            var mesh = new Mesh();
            var paletteAtlas = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var faceTexture = new Texture2D(512, 512, TextureFormat.RGBA32, true);
            var material = new Material(shader);
            try
            {
                material.EnableKeyword("_WG_PALETTE_ON");
                material.EnableKeyword("_WG_FACE_DETAIL_ON");
                material.SetTexture("_WgColorPaletteTex", paletteAtlas);
                material.SetTexture("_WgFaceDetailTex", faceTexture);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                CharacterPaletteBinding binding = root.AddComponent<CharacterPaletteBinding>();
                binding.Configure(
                    paletteAtlas,
                    256,
                    256,
                    root.transform,
                    faceDetailEnabled: true,
                    faceDetailTexture: faceTexture,
                    faceDetailIntensity: 0.75f);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Assert.AreEqual(1f, block.GetFloat(Shader.PropertyToID("_WgFaceDetailEnabled")));
                Assert.AreSame(faceTexture, block.GetTexture(Shader.PropertyToID("_WgFaceDetailTex")));
                Assert.AreEqual(0.75f, block.GetFloat(Shader.PropertyToID("_WgFaceDetailIntensity")));
                Assert.AreSame(material, renderer.sharedMaterial);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(faceTexture);
                UnityEngine.Object.DestroyImmediate(paletteAtlas);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Binding_MissingAtlasDisablesPaletteWithoutInstantiatingMaterial()
        {
            Shader shader = Shader.Find("WGame/CharacterPaletteLegacy");
            Assert.NotNull(shader);
            var root = new GameObject("PaletteMissingAtlasTest");
            root.SetActive(false);
            var mesh = new Mesh();
            var material = new Material(shader);
            try
            {
                material.EnableKeyword("_WG_PALETTE_ON");
                material.SetTexture("_WgColorPaletteTex", null);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                LogAssert.Expect(
                    LogType.Warning,
                    "CharacterPaletteBinding on 'PaletteMissingAtlasTest' has no shared RGB Color Palette atlas; palette rendering is disabled.");
                CharacterPaletteBinding binding = root.AddComponent<CharacterPaletteBinding>();
                binding.Configure(null, 256, 256, root.transform);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Assert.AreEqual(0f, block.GetFloat(Shader.PropertyToID("_WgPaletteEnabled")));
                Assert.AreSame(material, renderer.sharedMaterial);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private sealed class MaterialScope : IDisposable
        {
            public MaterialScope(Shader shader) => Material = new Material(shader);
            public Material Material { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Material);
        }
    }
}
