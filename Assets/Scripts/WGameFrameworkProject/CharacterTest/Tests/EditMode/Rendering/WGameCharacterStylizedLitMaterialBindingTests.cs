using System;
using System.IO;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGameCharacterStylizedLitMaterialBindingTests
    {
        private const string PaletteShaderName = "WGame/CharacterPaletteLegacy";
        private const string TraditionalShaderName = "WGame/CharacterTraditionalLit";

        [Test]
        public void PaletteShader_ExposesOnlyPaletteFlatCompatibilityAndRuntimeProperties()
        {
            Shader shader = FindRequiredShader(PaletteShaderName);
            using (var fixture = RendererFixture.Create(shader))
            {
                foreach (string property in new[]
                {
                    "_Color", "_Cull", "_WgPaletteEnabled", "_WgColorPaletteTex",
                    "_WgColorPaletteParams", "_WgPaletteLightingParams", "_WgPaletteDetailParams",
                    "_WgFaceDetailEnabled", "_WgFaceDetailTex", "_WgFaceDetailIntensity",
                    "_WgHitFlashColor", "_WgHitFlashAmount", "_WgStatusTintColor",
                    "_WgStatusTintAmount", "_WgDissolveNoiseTex", "_WgDissolveProgress",
                    "_WgDissolveEdgeColor", "_WgDissolveEdgeWidth", "_WgOutlineColor", "_WgOutlineAmount"
                })
                    AssertRequiredProperty(fixture.Material, property, PaletteShaderName);

                foreach (string property in new[]
                {
                    "_BaseMap", "_AlphaClip", "_Cutoff", "_WgNormalTex", "_WgMaskTex",
                    "_WgEmissionMaskTex", "_WgMagicCoreColor", "_WgBaseColor", "_WgClothColor",
                    "_WgLeatherColor", "_WgBronzeColor", "_WgRampTex", "_WgRimColor", "_WgRimPower"
                })
                    Assert.IsFalse(fixture.Material.HasProperty(property), $"{PaletteShaderName} must not expose {property}.");
            }
        }

        [Test]
        public void TraditionalShader_ExposesTraditionalAndRuntimePropertiesOnly()
        {
            Shader shader = FindRequiredShader(TraditionalShaderName);
            using (var fixture = RendererFixture.Create(shader))
            {
                foreach (string property in new[]
                {
                    "_BaseMap", "_Color", "_AlphaClip", "_Cutoff", "_Cull", "_WgNormalTex",
                    "_WgMaskTex", "_WgEmissionMaskTex", "_WgMagicCoreColor", "_WgDissolveNoiseTex",
                    "_WgHitFlashColor", "_WgHitFlashAmount", "_WgStatusTintColor",
                    "_WgStatusTintAmount", "_WgDissolveProgress", "_WgDissolveEdgeColor",
                    "_WgDissolveEdgeWidth", "_WgOutlineColor", "_WgOutlineAmount"
                })
                    AssertRequiredProperty(fixture.Material, property, TraditionalShaderName);

                foreach (string property in new[]
                {
                    "_WgPaletteEnabled", "_WgColorPaletteTex", "_WgColorPaletteParams",
                    "_WgPaletteLightingParams", "_WgFaceDetailEnabled", "_WgFaceDetailTex"
                })
                    Assert.IsFalse(fixture.Material.HasProperty(property), $"{TraditionalShaderName} must not expose {property}.");
            }
        }

        [Test]
        public void TraditionalShader_DefaultTexturesAreNeutralAndDoNotExposeScaleOffset()
        {
            Shader shader = FindRequiredShader(TraditionalShaderName);
            Assert.AreEqual("black", shader.GetPropertyTextureDefaultName(shader.FindPropertyIndex("_WgMaskTex")));
            Assert.AreEqual("bump", shader.GetPropertyTextureDefaultName(shader.FindPropertyIndex("_WgNormalTex")));
            foreach (string property in new[] { "_WgNormalTex", "_WgMaskTex", "_WgEmissionMaskTex", "_WgDissolveNoiseTex" })
            {
                int index = shader.FindPropertyIndex(property);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), property);
                Assert.That(
                    shader.GetPropertyFlags(index) & UnityEngine.Rendering.ShaderPropertyFlags.NoScaleOffset,
                    Is.Not.EqualTo(UnityEngine.Rendering.ShaderPropertyFlags.None),
                    property);
            }
        }

        [TestCase(
            "Assets/Art/WGame/Shaders/Character/CharacterStylizedLit.shader",
            "MxFramework.Project.Editor.WGameCharacterStylizedLitShaderGui")]
        [TestCase(
            "Assets/Art/WGame/Shaders/Character/CharacterPaletteLegacy.shader",
            "MxFramework.Project.Editor.WGameCharacterPaletteLegacyShaderGui")]
        [TestCase(
            "Assets/Art/WGame/Shaders/Character/CharacterTraditionalLit.shader",
            "MxFramework.Project.Editor.WGameCharacterTraditionalLitShaderGui")]
        public void Shader_DeclaresResolvableCustomEditor(string shaderPath, string customEditorTypeName)
        {
            string source = File.ReadAllText(shaderPath);
            StringAssert.Contains("CustomEditor \"" + customEditorTypeName + "\"", source);
            Type type = Type.GetType(customEditorTypeName + ", MxFramework.Project.Editor", false);
            Assert.That(type, Is.Not.Null);
            Assert.That(typeof(ShaderGUI).IsAssignableFrom(type), Is.True);
        }

        [Test]
        public void TraditionalShaderGui_SynchronizesOptionalTextureKeywords()
        {
            Shader shader = FindRequiredShader(TraditionalShaderName);
            using (var fixture = RendererFixture.Create(shader))
            {
                Type type = Type.GetType(
                    "MxFramework.Project.Editor.WGameCharacterTraditionalLitShaderGui, MxFramework.Project.Editor",
                    true);
                var gui = (ShaderGUI)Activator.CreateInstance(type, true);
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                try
                {
                    gui.ValidateMaterial(fixture.Material);
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_WG_NORMALMAP_ON"));
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_WG_MATERIAL_MASK_ON"));
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_WG_EMISSION_ON"));

                    fixture.Material.SetFloat("_AlphaClip", 1f);
                    fixture.Material.SetTexture("_WgNormalTex", texture);
                    fixture.Material.SetTexture("_WgMaskTex", texture);
                    fixture.Material.SetTexture("_WgEmissionMaskTex", texture);
                    gui.ValidateMaterial(fixture.Material);
                    Assert.IsTrue(fixture.Material.IsKeywordEnabled("_ALPHATEST_ON"));
                    Assert.IsTrue(fixture.Material.IsKeywordEnabled("_WG_NORMALMAP_ON"));
                    Assert.IsTrue(fixture.Material.IsKeywordEnabled("_WG_MATERIAL_MASK_ON"));
                    Assert.IsTrue(fixture.Material.IsKeywordEnabled("_WG_EMISSION_ON"));

                    fixture.Material.SetFloat("_AlphaClip", 0f);
                    fixture.Material.SetTexture("_WgNormalTex", null);
                    fixture.Material.SetTexture("_WgMaskTex", null);
                    fixture.Material.SetTexture("_WgEmissionMaskTex", null);
                    gui.ValidateMaterial(fixture.Material);
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_ALPHATEST_ON"));
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_WG_NORMALMAP_ON"));
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_WG_MATERIAL_MASK_ON"));
                    Assert.IsFalse(fixture.Material.IsKeywordEnabled("_WG_EMISSION_ON"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        [TestCase(PaletteShaderName)]
        [TestCase("WGame/CharacterStylizedLit")]
        [TestCase(TraditionalShaderName)]
        public void MaterialBindingHub_DrivesSharedRuntimeEffects(string shaderName)
        {
            Shader shader = FindRequiredShader(shaderName);
            using (var fixture = RendererFixture.Create(shader))
            {
                var hub = new MaterialBindingHub();
                var subject = new MxRenderSubjectId(587);
                int hitColorId = Shader.PropertyToID("_WgHitFlashColor");
                int hitAmountId = Shader.PropertyToID("_WgHitFlashAmount");
                MaterialBinding binding = hub.Bind(subject, MxMaterialChannel.HitFlash,
                    MaterialBindingScope.ForRendererSubMesh(fixture.Renderer, 0));
                var expected = new Color(1f, 0.42f, 0.22f, 1f);
                hub.SetColor(binding, hitColorId, expected);
                hub.SetFloat(binding, hitAmountId, 0.65f);
                hub.Flush();
                MaterialPropertyBlock block = fixture.ReadPropertyBlock();
                AssertColorApproximately(expected, block.GetColor(hitColorId), "_WgHitFlashColor");
                Assert.AreEqual(0.65f, block.GetFloat(hitAmountId));
            }
        }

        [Test]
        public void Character000003_UsesTraditionalShaderAndPreservesImportedSurfaceData()
        {
            const string root = "Assets/MxFrameworkGenerated/CharacterPackages/000003";
            Material body = AssetDatabase.LoadAssetAtPath<Material>(root + "/materials/000003_Body.mat");
            Material eye = AssetDatabase.LoadAssetAtPath<Material>(root + "/materials/000003_Eye.mat");
            Material preview = AssetDatabase.LoadAssetAtPath<Material>(
                root + "/preview_materials/CharacterPreview_148C8C8C.mat");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "/prefabs/000003_character_preview.prefab");

            Assert.That(body, Is.Not.Null);
            Assert.That(eye, Is.Not.Null);
            Assert.That(preview, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(body.shader.name, Is.EqualTo(TraditionalShaderName));
            Assert.That(eye.shader.name, Is.EqualTo(TraditionalShaderName));
            Assert.That(preview.shader.name, Is.EqualTo(TraditionalShaderName));
            Assert.That(body.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(body.GetTexture("_WgNormalTex"), Is.Not.Null);
            Assert.That(body.GetTexture("_WgMaskTex"), Is.Not.Null);
            Assert.That(body.GetFloat("_AlphaClip"), Is.EqualTo(1f));
            Assert.That(body.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
            Assert.That(body.IsKeywordEnabled("_WG_NORMALMAP_ON"), Is.True);
            Assert.That(body.IsKeywordEnabled("_WG_MATERIAL_MASK_ON"), Is.True);
            Assert.That(eye.GetTexture("_BaseMap"), Is.Not.Null);

            var assigned = new System.Collections.Generic.HashSet<Material>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                        assigned.Add(material);
                }
            }
            Assert.That(assigned, Does.Contain(body));
            Assert.That(assigned, Does.Contain(eye));
        }

        private static Shader FindRequiredShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.NotNull(shader, shaderName);
            Assert.IsTrue(shader.isSupported, shaderName);
            return shader;
        }

        private static void AssertRequiredProperty(Material material, string propertyName, string shaderName)
        {
            Assert.IsTrue(material.HasProperty(propertyName), $"{shaderName} missing {propertyName}.");
        }

        private static void AssertColorApproximately(Color expected, Color actual, string propertyName)
        {
            const float tolerance = 0.0001f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), propertyName + ".r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), propertyName + ".g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), propertyName + ".b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), propertyName + ".a");
        }

        private sealed class RendererFixture : IDisposable
        {
            private RendererFixture(GameObject gameObject, Mesh mesh, MeshRenderer renderer, Material material)
            {
                GameObject = gameObject;
                Mesh = mesh;
                Renderer = renderer;
                Material = material;
            }

            public GameObject GameObject { get; }
            public Mesh Mesh { get; }
            public MeshRenderer Renderer { get; }
            public Material Material { get; }

            public static RendererFixture Create(Shader shader)
            {
                var gameObject = new GameObject("WGameCharacterShaderTests");
                var mesh = new Mesh();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
                var material = new Material(shader);
                renderer.sharedMaterial = material;
                return new RendererFixture(gameObject, mesh, renderer, material);
            }

            public MaterialPropertyBlock ReadPropertyBlock()
            {
                var block = new MaterialPropertyBlock();
                Renderer.GetPropertyBlock(block, 0);
                return block;
            }

            public void Dispose()
            {
                if (Material != null) UnityEngine.Object.DestroyImmediate(Material);
                if (GameObject != null) UnityEngine.Object.DestroyImmediate(GameObject);
                if (Mesh != null) UnityEngine.Object.DestroyImmediate(Mesh);
            }
        }
    }
}
