using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGameCharacterShaderMigrationTests
    {
        private static object Call(string method, params object[] args)
        {
            Type type = Type.GetType("MxFramework.Project.Editor.WGameCharacterShaderMigration, MxFramework.Project.Editor", true);
            return type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyMigration_PreservesValuesAndIsIdempotent(bool palette)
        {
            var material = new Material(Shader.Find("WGame/CharacterPaletteLegacy"));
            try
            {
                var color = new Color(.2f, .4f, .6f, 1);
                material.SetColor("_Color", color);
                material.SetFloat("_WgHitFlashAmount", .4f);
                material.SetFloat("_WgPaletteEnabled", palette ? 1 : 0);
                if (palette) material.EnableKeyword("_WG_PALETTE_ON");
                material.shader = Shader.Find("WGame/CharacterStylizedLit");
                Assert.That(Call("Migrate", material, Shader.Find("WGame/CharacterPaletteLegacy")), Is.True);
                Assert.That(material.shader.name, Is.EqualTo("WGame/CharacterPaletteLegacy"));
                Assert.That(Vector4.Distance(material.GetColor("_Color"), color), Is.LessThan(0.000001f));
                Assert.That(material.GetFloat("_WgHitFlashAmount"), Is.EqualTo(.4f));
                Assert.That(material.IsKeywordEnabled("_WG_PALETTE_ON"), Is.EqualTo(palette));
                Assert.That(Call("Migrate", material, Shader.Find("WGame/CharacterPaletteLegacy")), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void ConflictingFlags_AreReportedWithoutChangingMaterial()
        {
            var material = new Material(Shader.Find("WGame/CharacterPaletteLegacy"));
            try
            {
                material.SetFloat("_WgPaletteEnabled", 1);
                material.DisableKeyword("_WG_PALETTE_ON");
                material.shader = Shader.Find("WGame/CharacterStylizedLit");
                object[] args = { material, null };
                Assert.That(Call("Classify", args), Is.EqualTo("Conflict"));
                Assert.That(args[1], Is.Not.Null);
                Assert.That(Call("Migrate", material, Shader.Find("WGame/CharacterPaletteLegacy")), Is.False);
                Assert.That(material.shader.name, Is.EqualTo("WGame/CharacterStylizedLit"));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void BakedMaterial_RequiresBothTextures_AndNeverBecomesLegacy()
        {
            var material = new Material(Shader.Find("WGame/CharacterStylizedLit"));
            try
            {
                object[] args = { material, null };
                Assert.That(Call("Classify", args), Is.EqualTo("Conflict"));
                material.SetTexture("_WgBakedBaseColor", Texture2D.whiteTexture);
                Assert.That(Call("Classify", args), Is.EqualTo("Conflict"));
                material.SetTexture("_WgSurfaceDataTex", Texture2D.whiteTexture);
                Assert.That(Call("Classify", args), Is.EqualTo("Baked"));
                Assert.That(Call("Migrate", material, Shader.Find("WGame/CharacterPaletteLegacy")), Is.False);
                Assert.That(material.shader.name, Is.EqualTo("WGame/CharacterStylizedLit"));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void ShaderSurfaces_AreSeparateAndKeepRuntimeEffects()
        {
            var baked = new Material(Shader.Find("WGame/CharacterStylizedLit"));
            var legacy = new Material(Shader.Find("WGame/CharacterPaletteLegacy"));
            try
            {
                foreach (string property in new[] { "_WgPaletteEnabled", "_WgColorPaletteTex", "_WgFaceDetailTex", "_Color", "_WgBakedSurfaceEnabled" })
                    Assert.That(baked.HasProperty(property), Is.False, property);
                foreach (string property in new[] { "_WgBakedBaseColor", "_WgSurfaceDataTex", "_WgHairShape" })
                {
                    Assert.That(baked.HasProperty(property), Is.True, property);
                    Assert.That(legacy.HasProperty(property), Is.False, property);
                }
                foreach (Material material in new[] { baked, legacy })
                    foreach (string property in new[] { "_WgHitFlashAmount", "_WgDissolveProgress", "_WgOutlineAmount" })
                        Assert.That(material.HasProperty(property), Is.True, property);
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(legacy); }
        }
    }
}
