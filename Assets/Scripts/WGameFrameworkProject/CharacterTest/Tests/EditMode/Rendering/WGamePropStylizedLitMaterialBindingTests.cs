using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGamePropStylizedLitMaterialBindingTests
    {
        private const string ShaderName = "WGame/PropStylizedLit";

        [Test]
        public void Shader_ExposesTraditionalAlbedoProperties()
        {
            Shader shader = Shader.Find(ShaderName);

            Assert.NotNull(shader, $"{ShaderName} must be imported for the prop shader smoke test.");
            Material material = new Material(shader);
            try
            {
                AssertRequiredProperty(material, "_BaseMap");
                AssertRequiredProperty(material, "_Color");
                AssertRequiredProperty(material, "_WgAlbedoStrength");
                AssertRequiredProperty(material, "_WgBaseColor");
                AssertRequiredProperty(material, "_WgRampTex");
                AssertRequiredProperty(material, "_WgMaskTex");
                AssertRequiredProperty(material, "_WgNormalTex");
                AssertRequiredProperty(material, "_WgDissolveNoiseTex");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Material_CanStoreTraditionalAlbedoProperties()
        {
            Shader shader = Shader.Find(ShaderName);

            Assert.NotNull(shader, $"{ShaderName} must be imported for the prop shader smoke test.");
            Material material = new Material(shader);
            try
            {
                int baseMapId = Shader.PropertyToID("_BaseMap");
                int colorId = Shader.PropertyToID("_Color");
                int albedoStrengthId = Shader.PropertyToID("_WgAlbedoStrength");
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);

                try
                {
                    var tint = new Color(0.45f, 0.56f, 0.67f, 1f);
                    material.SetTexture(baseMapId, texture);
                    material.SetColor(colorId, tint);
                    material.SetFloat(albedoStrengthId, 1f);

                    Assert.AreSame(texture, material.GetTexture(baseMapId));
                    AssertColorApproximately(tint, material.GetColor(colorId), "_Color");
                    Assert.AreEqual(1f, material.GetFloat(albedoStrengthId));
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        private static void AssertRequiredProperty(Material material, string propertyName)
        {
            Assert.IsTrue(material.HasProperty(Shader.PropertyToID(propertyName)), $"{ShaderName} missing {propertyName}.");
        }

        private static void AssertColorApproximately(Color expected, Color actual, string propertyName)
        {
            const float Tolerance = 0.0001f;

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), $"{propertyName}.r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), $"{propertyName}.g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), $"{propertyName}.b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance), $"{propertyName}.a");
        }
    }
}
