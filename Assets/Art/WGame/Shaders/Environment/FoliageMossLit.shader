Shader "WGame/FoliageMossLit"
{
    Properties
    {
        [MainColor] _WgFoliageColor ("Foliage Color", Color) = (0.170, 0.230, 0.145, 1)
        _WgDryColor ("Dry Color", Color) = (0.505, 0.431, 0.282, 1)
        _WgMossColor ("Moss Color", Color) = (0.208, 0.263, 0.192, 1)
        _WgRampTex ("Ramp Texture", 2D) = "white" {}
        _WgMaskTex ("Mask Texture", 2D) = "white" {}
        _WgAlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.42
        _WgMossCoverage ("Moss Coverage", Range(0, 1)) = 0.35
        _WgDryCoverage ("Dry Coverage", Range(0, 1)) = 0.18
        [Enum(Material,0,GlobalFrame,1)] _WgWindSource ("Wind Source", Float) = 0
        _WgWindDirection ("Wind Direction (world XZ)", Vector) = (0.8, 0, 0.6, 0)
        _WgWindBendStrength ("Wind Strength", Range(0, 1)) = 0.55
        _WgWindBendFrequency ("Wind Speed", Range(0, 4)) = 0.85
        _WgWindGustStrength ("Wind Gusts", Range(0, 1)) = 0.65
        _WgWindTipFlutter ("Wind Tip Flutter", Range(0, 1)) = 0.3
        _WgShadowReceiveStrength ("Environment Shadow Receive", Range(0, 1)) = 1
        _WgRootShadeStrength ("Root Shade Strength", Range(0, 0.35)) = 0.14
        _WgRootShadeHeight ("Root Shade Height (UV fraction)", Range(0.05, 1)) = 0.35
        _WgRootShadeFadeDistance ("Root Shade Fade End (m)", Range(1, 100)) = 35
        [HideInInspector] _WgFogBlendStrength ("Legacy Fog Blend (Unused)", Range(0, 1)) = 0.58
        _WgFacetStrength ("Facet Strength", Range(0, 1)) = 0.28
        _WgVertexTintStrength ("Leaf Vertex Tint", Range(0, 1)) = 0
        _WgVertexTintAmount ("Vertex Color Amount", Range(0, 1)) = 0.42
        _WgPatchColorStrength ("Patch Color Correlation", Range(0, 1)) = 0
        _WgWindGustSpatialScale ("Gust Spatial Scale", Range(0.05, 1)) = 0.48
        [HideInInspector] _WgSeparateWindHeight ("Separate Authored Wind Height", Float) = 0
        _WgLeafNormalUpBlend ("Leaf Upward Lighting", Range(0, 0.8)) = 0
        _WgWetnessOverride ("Wetness Override", Range(0, 1)) = 0
        _WgDebugOverlayColor ("Debug Overlay Color", Color) = (0.427, 0.608, 0.710, 1)
        _WgDebugOverlayAmount ("Debug Overlay Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #include "../ShaderLibrary/WGameFoliageInputs.hlsl"
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WgFoliageVertex
            #pragma fragment WgFoliageFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ WG_FOLIAGE_INDIRECT
            #pragma target 4.5 WG_FOLIAGE_INDIRECT
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            #include "../ShaderLibrary/WGameSceneLighting.hlsl"

            float4 WgFoliageFragment(WgFoliageVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                #if !defined(WG_FOLIAGE_INDIRECT)
                    UNITY_SETUP_INSTANCE_ID(input);
                #endif
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 mask = WgFoliageMaskAndClip(input.uv, input.color.a);
                float3 normalWS = WgFoliageLitNormal(input.normalWS, IS_FRONT_VFACE(face, 1, -1));
                float normalUp = dot(normalWS, float3(0, 1, 0)) * 0.5 + 0.5;
                float moss = saturate(max(mask.g, _WgMossCoverage * smoothstep(0.1, 0.82, normalUp)));
                float dry = saturate(max(mask.b, _WgDryCoverage));
                float3 albedo = lerp(_WgFoliageColor.rgb, _WgDryColor.rgb, dry);
                albedo = lerp(albedo, _WgMossColor.rgb, moss * (1.0 - dry * 0.35));
                float tintFloor = lerp(0.28, 0.02, saturate((_WgVertexTintAmount - 0.42) / 0.58));
                float3 vertexTint = lerp(max(saturate(WgLuminance(input.color.rgb)), 0.28),
                    max(input.color.rgb, tintFloor), saturate(_WgVertexTintStrength));
                albedo *= lerp(1.0, vertexTint, saturate(_WgVertexTintAmount));
                float variation = input.instanceVariation;
                albedo *= lerp(1.0, 0.85 + variation * 0.15, _WgFacetStrength);
                float rampSeed = saturate(normalUp * 0.4 + mask.g * 0.3 + mask.b * 0.3);
                float3 ramp = SAMPLE_TEXTURE2D(_WgRampTex, sampler_WgRampTex, float2(rampSeed, 0.46)).rgb;
                albedo *= max(ramp, 0.36);
                float wetness = saturate(max(max(_MxWetness, _MxRain * 0.65), _WgWetnessOverride));
                albedo = lerp(albedo, albedo * float3(0.58, 0.72, 0.74), wetness * 0.28);
                albedo = lerp(albedo, lerp(albedo, WgColorParchment200, 0.16), saturate(_MxSnowCoverage) * 0.16);

                // A bounded material tint at the blade base, applied once to both direct
                // and indirect albedo. Do not multiply the scene's GI micro-AO again.
                float rootWeight = (1.0 - smoothstep(0.0, max(_WgRootShadeHeight, 0.001), saturate(input.uv.y)))
                    * saturate(input.rootShadeDistanceFade);
                albedo *= 1.0 - clamp(_WgRootShadeStrength, 0.0, 0.35) * rootWeight;

                WgMainLightData mainLight = WgGetMainLightData(input.positionWS, float3(0.22, 0.82, 0.44));
                float receivedShadow = mainLight.shadowAttenuation;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    // GetMainLight(shadowCoord) samples realtime shadow only. Match
                    // URP's unlightmapped receive fade at the main shadow distance.
                    if (_MxDirectionalShadowEnabled < 0.5)
                        receivedShadow = lerp(receivedShadow, 1.0, GetMainLightShadowFade(input.positionWS));
                #endif
                float attenuation = lerp(1.0, receivedShadow, saturate(_WgShadowReceiveStrength))
                    * mainLight.distanceAttenuation;
                float3 color = WgSceneDiffuse(albedo, normalWS, input.positionWS,
                    GetNormalizedScreenSpaceUV(input.positionCS), mainLight.directionWS, mainLight.color, attenuation, 1.0);
                color = WgOverlayColor(color, _WgDebugOverlayColor.rgb, _WgDebugOverlayAmount);
                // The framework height-fog pass already composites opaque cutout depth.
                // When absent, honor the scene's Unity fog settings instead of a fixed night-ruin look.
                if (_MxPipelineHeightFogEnabled < 0.5) color = MixFog(color, input.fogFactor);
                return float4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WgFoliageVertex
            #pragma fragment WgFoliageDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ WG_FOLIAGE_INDIRECT
            #pragma target 4.5 WG_FOLIAGE_INDIRECT
            half WgFoliageDepth(WgFoliageVaryings input) : SV_Target
            {
                #if !defined(WG_FOLIAGE_INDIRECT)
                    UNITY_SETUP_INSTANCE_ID(input);
                #endif
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                WgFoliageMaskAndClip(input.uv, input.color.a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WgFoliageVertex
            #pragma fragment WgFoliageNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ WG_FOLIAGE_INDIRECT
            #pragma target 4.5 WG_FOLIAGE_INDIRECT
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            float4 WgFoliageNormals(WgFoliageVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                #if !defined(WG_FOLIAGE_INDIRECT)
                    UNITY_SETUP_INSTANCE_ID(input);
                #endif
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                WgFoliageMaskAndClip(input.uv, input.color.a);
                float3 normalWS = WgFoliageLitNormal(input.normalWS, IS_FRONT_VFACE(face, 1, -1));
                #if defined(_GBUFFER_NORMALS_OCT)
                return float4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5)), 0);
                #else
                return float4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
