using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Shared project material setup for runtime views and their Editor builders.</summary>
    internal static class CharacterTestUnlitMaterials
    {
        internal const string MarkerShaderName = "WGame/WorldMarkerUnlit";
        internal const string VfxShaderName = "WGame/MagicVfxUnlit";
        private static Shader _markerShader;
        private static Shader _vfxShader;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MarkerColorId = Shader.PropertyToID("_WgMarkerColor");

        internal static Shader RequireVfxShader() => Require(ref _vfxShader, VfxShaderName);

        private static Shader Require(ref Shader cached, string name)
        {
            if (cached == null) cached = Shader.Find(name);
            if (cached == null || !cached.isSupported)
                throw new UnityException("Required project shader is unavailable: " + name);
            return cached;
        }

        internal static Material CreateMarker(string name, Color color)
        {
            var material = new Material(Require(ref _markerShader, MarkerShaderName)) { name = name };
            material.EnableKeyword("_WG_MARKER_MESH");
            material.SetFloat("_WgMeshCoverage", 1f);
            material.SetColor(MarkerColorId, color);
            bool transparent = color.a < 1f;
            material.SetInt("_WgSrcBlend", (int)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetInt("_WgDstBlend", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetInt("_WgZWrite", transparent ? 0 : 1);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.renderQueue = (int)(transparent ? RenderQueue.Transparent : RenderQueue.Geometry);
            return material;
        }

        /// <summary>Switch only the shader/mode, preserving authored texture, tint, blend state and bindings.</summary>
        internal static void AdoptVfxShader(Material material)
        {
            bool emission = material.IsKeywordEnabled("_EMISSION");
            int renderQueue = material.renderQueue;
            material.shader = RequireVfxShader();
            material.renderQueue = renderQueue;
            material.EnableKeyword("_WG_VFX_TEXTURED");
            material.SetFloat("_WgTexturedMode", 1f);
            if (emission) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            material.SetFloat("_WgTextureEmission", emission ? 1f : 0f);
        }

        internal static void SetVfxColor(Material material, Color color) => material.SetColor(BaseColorId, color);

        internal static Material CreateVfx(string name, Color color, bool additive = false)
        {
            var material = new Material(RequireVfxShader()) { name = name };
            ConfigureVfx(material, color, additive);
            return material;
        }

        internal static void ConfigureVfx(Material material, Color color, bool additive = false)
        {
            AdoptVfxShader(material);
            SetVfxColor(material, color);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            material.SetInt("_DstBlendAlpha", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
