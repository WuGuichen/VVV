using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class VfxShaderProfileSuggestion
    {
        private const string UberShaderName = "Piloto Studio/UberFXSG";

        public static VfxShaderProfileView Create(ParticleSystemRenderer renderer)
        {
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Shader shader = material != null ? material.shader : null;
            if (shader == null) return null;
            string path = AssetDatabase.GetAssetPath(shader);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid)) return null;
            var result = new VfxShaderProfileView
            {
                ProfileId = "vfx.shader.suggested." + VfxCompositionDraftService.FileToken(guid),
                ShaderName = shader.name,
                ShaderGuid = guid,
                ShaderHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                Status = nameof(VfxProfileReviewState.AutoSuggested)
            };
            if (string.Equals(shader.name, UberShaderName, StringComparison.Ordinal))
            {
                result.ProfileId = "vfx.shader.piloto.uberfxsg";
                result.Bindings.Add(Property("appearance.palette.primary", "_WhiteColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.palette.secondary", "_MidColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.palette.detail", "_LastColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.emission.color", "_WhiteColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.emission.exposureEv", "_WhiteColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.emission.exposureEv", "_MidColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.emission.exposureEv", "_LastColor", "Color", dynamic: true));
                result.Bindings.Add(Property("appearance.erosion", "_Alpha_Clip_Threshold", "Float", dynamic: true, domain: VfxSemanticTimeDomain.LayerFrame));
                result.Bindings.Add(Property("render.texture.main", "_MainTex", "Texture", dynamic: false, requiresDerived: true));
                result.Bindings.Add(Property("render.texture.mask", "_AlphaOverride", "Texture", dynamic: false, requiresDerived: true, keyword: "_USEALPHAOVERRIDE"));
                result.Bindings.Add(Property("render.texture.noise", "_DetailNoise", "Texture", dynamic: false, requiresDerived: true));
                result.Bindings.Add(Property("render.state.renderQueue", "@RenderQueue", "Int", dynamic: false, requiresDerived: true));
                if (material.HasProperty("_Cull")) result.Bindings.Add(Property("render.state.cull", "_Cull", "Int", dynamic: false, requiresDerived: true));
                if (material.HasProperty("_ZWrite")) result.Bindings.Add(Property("render.state.zWrite", "_ZWrite", "Bool", dynamic: false, requiresDerived: true));
                if (material.HasProperty("_SrcBlend")) result.Bindings.Add(Property("render.state.srcBlend", "_SrcBlend", "Int", dynamic: false, requiresDerived: true));
                if (material.HasProperty("_DstBlend")) result.Bindings.Add(Property("render.state.dstBlend", "_DstBlend", "Int", dynamic: false, requiresDerived: true));
                result.RequiredVertexStreams.AddRange(new[] { "Position", "Normal", "Color", "UV" });
                result.CustomDataBindings.Add(new VfxShaderCustomDataBindingView
                {
                    Stream = "Custom1",
                    Channels = "XYZW",
                    SemanticPropertyId = "material.customData.primary",
                    ShaderUsage = "UberFXSG particle vertex stream payload",
                    Editable = false
                });
            }
            result.ContentHash = VfxCanonicalJson.Hash(result, "contentHash");
            return result;
        }

        private static VfxShaderPropertyBindingView Property(
            string propertyId,
            string shaderProperty,
            string propertyType,
            bool dynamic,
            VfxSemanticTimeDomain domain = VfxSemanticTimeDomain.Static,
            bool requiresDerived = false,
            string keyword = null)
        {
            return new VfxShaderPropertyBindingView
            {
                SemanticPropertyId = propertyId,
                ShaderPropertyName = shaderProperty,
                PropertyType = propertyType,
                DynamicPropertyBlock = dynamic,
                RequiresDerivedMaterial = requiresDerived,
                TimeDomain = domain.ToString(),
                Keyword = keyword
            };
        }
    }
}
