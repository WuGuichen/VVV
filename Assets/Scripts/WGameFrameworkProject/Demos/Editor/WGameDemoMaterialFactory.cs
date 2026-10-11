using System;
using UnityEngine;

namespace MxFramework.Demo
{
    internal static class WGameDemoMaterialFactory
    {
        internal const string CharacterShaderName = "WGame/CharacterPaletteLegacy";
        internal const string EnvironmentShaderName = "WGame/EnvironmentStylizedLit";
        internal const string MarkerShaderName = "WGame/WorldMarkerUnlit";
        internal const string PropShaderName = "WGame/PropStylizedLit";

        internal static Material CreateCharacter(string name, Color color)
        {
            Material material = Create(name, CharacterShaderName);
            material.SetColor("_Color", color);
            return material;
        }

        internal static Material CreateEnvironment(string name, Color color)
        {
            Material material = Create(name, EnvironmentShaderName);
            ConfigureEnvironment(material, color);
            return material;
        }

        internal static void ConfigureEnvironment(Material material, Color color)
        {
            RequireMaterial(material, EnvironmentShaderName);
            material.SetColor("_WgStoneColor", color);
            material.SetColor("_WgWeatheredStoneColor", color);
            material.SetColor("_WgMossColor", color);
            material.SetFloat("_WgWeatheringCoverage", 0f);
            material.SetFloat("_WgMossCoverage", 0f);
            material.SetFloat("_WgBronzeTrimCoverage", 0f);
            material.SetFloat("_WgFacetStrength", 0.25f);
            material.SetFloat("_WgFogBlendStrength", 0.45f);
        }

        internal static Material CreateMarker(string name, Color color)
        {
            Material material = Create(name, MarkerShaderName);
            ConfigureMarker(material, color);
            return material;
        }

        internal static void ConfigureMarker(Material material, Color color)
        {
            RequireMaterial(material, MarkerShaderName);
            material.SetColor("_WgMarkerColor", color);
            material.SetColor("_WgEdgeColor", Color.Lerp(color, Color.white, 0.16f));
            material.SetFloat("_WgMarkerPulse", 0f);
            material.SetFloat("_WgBloomBudget", 0.12f);
            material.SetFloat("_WgDarkBackgroundBoost", 0.12f);
        }

        internal static Material CreateProp(string name, Color color, bool highlighted = false)
        {
            Material material = Create(name, PropShaderName);
            ConfigureProp(material, color, highlighted);
            return material;
        }

        internal static void ConfigureProp(Material material, Color color, bool highlighted = false)
        {
            RequireMaterial(material, PropShaderName);
            material.SetColor("_Color", color);
            material.SetFloat("_WgAlbedoStrength", 1f);
            material.SetFloat("_WgBronzeTrimAmount", 0f);
            material.SetFloat("_WgInteractableGlowAmount", highlighted ? 0.08f : 0f);
            material.SetFloat("_WgFacetStrength", 0.15f);
            material.SetFloat("_WgFogBlendStrength", 0.25f);
            material.SetFloat("_WgStatusTintAmount", 0f);
            material.SetFloat("_WgDissolveProgress", 0f);
            material.SetFloat("_WgOutlineAmount", 0f);
            material.SetFloat("_WgHitFlashAmount", 0f);
        }

        internal static Shader RequireShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required supported WGame shader is missing: " + shaderName);
            return shader;
        }

        private static Material Create(string name, string shaderName)
        {
            return new Material(RequireShader(shaderName))
            {
                name = name
            };
        }

        private static void RequireMaterial(Material material, string shaderName)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            material.shader = RequireShader(shaderName);
        }
    }
}
