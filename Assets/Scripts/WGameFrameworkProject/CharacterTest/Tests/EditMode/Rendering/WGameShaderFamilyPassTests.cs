using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGameShaderFamilyPassTests
    {
        private static readonly ShaderTagId LightModeTag = new ShaderTagId("LightMode");

        [TestCase("WGame/CharacterStylizedLit")]
        [TestCase("WGame/CharacterStylizedLitLite")]
        [TestCase("WGame/CharacterPaletteLegacy")]
        [TestCase("WGame/CharacterTraditionalLit")]
        [TestCase("WGame/EnvironmentStylizedLit")]
        [TestCase("WGame/PropStylizedLit")]
        public void OpaqueShaders_ExposeShadowCasterAndDepthOnlyPasses(string shaderName)
        {
            Shader shader = FindRequiredShader(shaderName);

            AssertPass(shader, "ShadowCaster", "ShadowCaster");
            AssertPass(shader, "DepthOnly", "DepthOnly");
        }

        [Test]
        public void FoliageShader_IsReceiveOnlyAndKeepsDepthOnlyPass()
        {
            Shader shader = FindRequiredShader("WGame/FoliageMossLit");

            Assert.That(HasLightMode(shader, "ShadowCaster"), Is.False, "WGame/FoliageMossLit should not cast realtime gameplay shadows.");
            AssertPass(shader, "DepthOnly", "DepthOnly");
            AssertPass(shader, "DepthNormals", "DepthNormals");
        }

        [TestCase("WGame/MagicVfxUnlit")]
        [TestCase("WGame/WorldMarkerUnlit")]
        [TestCase("WGame/ImpactDecal")]
        public void TransparentShaders_DoNotWriteShadowCasterOrDepthOnlyPasses(string shaderName)
        {
            Shader shader = FindRequiredShader(shaderName);

            Assert.That(HasLightMode(shader, "ShadowCaster"), Is.False, $"{shaderName} should not cast transparent marker/VFX/decal shadows.");
            Assert.That(HasLightMode(shader, "DepthOnly"), Is.False, $"{shaderName} should not write transparent marker/VFX/decal depth.");
        }

        private static Shader FindRequiredShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.NotNull(shader, $"{shaderName} must be imported.");
            return shader;
        }

        private static void AssertPass(Shader shader, string passName, string expectedLightMode)
        {
            int passIndex = FindLightModePass(shader, expectedLightMode);
            Assert.That(passIndex, Is.GreaterThanOrEqualTo(0), $"{shader.name} missing pass {passName}.");

            ShaderTagId lightMode = shader.FindPassTagValue(passIndex, LightModeTag);
            Assert.That(lightMode.name, Is.EqualTo(expectedLightMode).IgnoreCase, $"{shader.name} pass {passName} has an unexpected LightMode tag.");
        }

        private static bool HasLightMode(Shader shader, string expectedLightMode)
        {
            return FindLightModePass(shader, expectedLightMode) >= 0;
        }

        private static int FindLightModePass(Shader shader, string expectedLightMode)
        {
            for (int i = 0; i < shader.passCount; i++)
            {
                ShaderTagId lightMode = shader.FindPassTagValue(i, LightModeTag);
                if (string.Equals(lightMode.name, expectedLightMode, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }
}
