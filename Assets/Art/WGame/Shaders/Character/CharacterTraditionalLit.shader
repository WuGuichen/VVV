Shader "WGame/CharacterTraditionalLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _Color ("Albedo Tint", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Render Face", Float) = 2
        [NoScaleOffset] _WgNormalTex ("Normal Texture", 2D) = "bump" {}
        [NoScaleOffset] _WgMaskTex ("Material Mask", 2D) = "black" {}
        [NoScaleOffset] _WgEmissionMaskTex ("Emission Mask", 2D) = "black" {}
        _WgMagicCoreColor ("Emission Color", Color) = (0.941, 0.706, 0.373, 1)
        [NoScaleOffset] _WgDissolveNoiseTex ("Dissolve Noise Texture", 2D) = "white" {}
        _WgHitFlashColor ("Hit Flash Color", Color) = (1, 0.72, 0.5, 1)
        _WgHitFlashAmount ("Hit Flash Amount", Range(0, 1)) = 0
        _WgStatusTintColor ("Status Tint Color", Color) = (0.5, 0.8, 0.45, 1)
        _WgStatusTintAmount ("Status Tint Amount", Range(0, 1)) = 0
        _WgDissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _WgDissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 0.72, 0.35, 1)
        _WgDissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.25)) = 0.04
        _WgOutlineColor ("Outline Color", Color) = (0.851, 0.788, 0.647, 1)
        _WgOutlineAmount ("Outline Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _WG_NORMALMAP_ON
            #pragma shader_feature_local_fragment _WG_MATERIAL_MASK_ON
            #pragma shader_feature_local_fragment _WG_EMISSION_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterTraditionalInput.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameStylizedLighting.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            #include "../ShaderLibrary/WGameFogBlend.hlsl"
            #include "../ShaderLibrary/WGameEmission.hlsl"
            #include "../ShaderLibrary/WGameRimLighting.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
#if defined(_WG_NORMALMAP_ON)
            TEXTURE2D(_WgNormalTex);
            SAMPLER(sampler_WgNormalTex);
#endif
#if defined(_WG_MATERIAL_MASK_ON)
            TEXTURE2D(_WgMaskTex);
            SAMPLER(sampler_WgMaskTex);
#endif
#if defined(_WG_EMISSION_ON)
            TEXTURE2D(_WgEmissionMaskTex);
            SAMPLER(sampler_WgEmissionMaskTex);
#endif
            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(
                    TransformObjectToWorldDir(input.tangentOS.xyz),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                return output;
            }

            float3 WgTraditionalNormalWS(Varyings input)
            {
                float3 normalWS = WgSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
#if defined(_WG_NORMALMAP_ON)
                float3 tangentWS = WgSafeNormalize(input.tangentWS.xyz, float3(1.0, 0.0, 0.0));
                float3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;
                float3 normalTS = SAMPLE_TEXTURE2D(_WgNormalTex, sampler_WgNormalTex, input.uv).xyz * 2.0 - 1.0;
                normalWS = WgSafeNormalize(
                    normalTS.x * tangentWS + normalTS.y * bitangentWS + normalTS.z * normalWS,
                    normalWS);
#endif
                return normalWS;
            }

            float4 Frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
#endif
                float2 baseUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv);
                float baseAlpha = saturate(baseSample.a * _Color.a);
#if defined(_ALPHATEST_ON)
                clip(baseAlpha - _Cutoff);
#endif
                float3 materialBase = baseSample.rgb * _Color.rgb;
                float faceDirection = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
                float3 normalWS = WgTraditionalNormalWS(input) * faceDirection;
                float3 viewDirectionWS = WgSafeNormalize(GetCameraPositionWS() - input.positionWS, float3(0.0, 0.0, 1.0));
                WgMainLightData mainLight = WgGetMainLightData(input.positionWS, float3(0.35, 0.78, 0.45));
                float wrappedLight = WgHalfLambert(normalWS, mainLight.directionWS, 0.35);
                float steppedLight = WgQuantizeRamp(wrappedLight, 3.0);
                float attenuation = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float3 color = materialBase * lerp(0.85, 1.10, saturate(steppedLight * attenuation));

                float wetness = saturate(max(_MxWetness, _MxRain * 0.5));
                color = lerp(color, color * float3(0.72, 0.82, 0.88), wetness * 0.22);
                color = WgApplyMainLightColor(color, mainLight, 0.08);
                color = WgOverlayColor(color, _WgStatusTintColor.rgb, _WgStatusTintAmount);
                color = WgOverlayColor(color, _WgHitFlashColor.rgb, _WgHitFlashAmount);

                WgRimLightingParams outlineParams;
                outlineParams._WgRimColor = _WgOutlineColor.rgb;
                outlineParams._WgRimAmount = _WgOutlineAmount;
                outlineParams._WgRimPower = 1.45;
                outlineParams._WgRimMin = 0.04;
                outlineParams._WgRimMax = 0.82;
                color = WgApplyRimLighting(color, normalWS, viewDirectionWS, outlineParams);

#if defined(_WG_EMISSION_ON) && defined(_WG_MATERIAL_MASK_ON)
                float4 maskSample = SAMPLE_TEXTURE2D(_WgMaskTex, sampler_WgMaskTex, input.uv);
                WgPackedMaterialMask materialMask = WgDecodePackedMaterialMask(maskSample);
                float emissionMask = SAMPLE_TEXTURE2D(_WgEmissionMaskTex, sampler_WgEmissionMaskTex, input.uv).r
                    * materialMask.emission;
                WgEmissionParams emissionParams;
                emissionParams._WgEmissionColor = _WgMagicCoreColor.rgb;
                emissionParams._WgEmissionIntensity = 1.2;
                emissionParams._WgEmissionMaskWeight = 1.0;
                emissionParams._WgEmissionPulseAmount = 0.25;
                emissionParams._WgEmissionMaxIntensity = 2.0;
                color = WgAddEmission(color, WgEvaluateEmission(emissionMask, _MxTime.y, 0.55, emissionParams));
#endif

                UNITY_BRANCH
                if (_WgDissolveProgress > WG_EPSILON)
                {
                    float dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, input.uv).r;
                    WgDissolveParams dissolveParams;
                    dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                    dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                    dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                    dissolveParams._WgLifetimeFade = 0.0;
                    dissolveParams._WgDissolveNoiseStrength = 1.0;
                    WgDissolveResult dissolve = WgEvaluateDissolve(dissolveNoise, dissolveParams);
                    clip(dissolve.clipValue);
                    color = WgApplyDissolveEdge(color, dissolve);
                }

                WgFogBlendParams fogParams;
                fogParams._WgFogColor = WgDefaultNightRuinFogColor();
                fogParams._WgFogDensity = 0.38;
                fogParams._WgFogHeightFalloff = 0.28;
                fogParams._WgFogStartDistance = 8.0;
                fogParams._WgFogEndDistance = 42.0;
                fogParams._WgFogBlendStrength = 0.38;
                fogParams._WgFogFocusClearRadius = 1.75;
                fogParams._WgFogFocusClearSoftness = 2.5;
                color = WgApplyNightRuinFog(color, input.positionWS, GetCameraPositionWS(), _MxLocalSubjectWorldPos.y, fogParams);
                return float4(saturate(color), baseAlpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WgShadowCasterVertex
            #pragma fragment WgShadowCasterFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterTraditionalInput.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            void WgCharacterDepthShadowClip(float2 uv)
            {
#if defined(_ALPHATEST_ON)
                float2 baseUv = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv).a * _Color.a - _Cutoff);
#endif
                if (_WgDissolveProgress <= WG_EPSILON)
                    return;
                float dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, uv).r;
                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                clip(WgEvaluateDissolve(dissolveNoise, dissolveParams).clipValue);
            }

            #define WGAME_DEPTH_SHADOW_CLIP(input) WgCharacterDepthShadowClip((input).uv)
            #include "../ShaderLibrary/WGameDepthShadowPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WgDepthOnlyVertex
            #pragma fragment WgDepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterTraditionalInput.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            void WgCharacterDepthShadowClip(float2 uv)
            {
#if defined(_ALPHATEST_ON)
                float2 baseUv = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv).a * _Color.a - _Cutoff);
#endif
                if (_WgDissolveProgress <= WG_EPSILON)
                    return;
                float dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, uv).r;
                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                clip(WgEvaluateDissolve(dissolveNoise, dissolveParams).clipValue);
            }

            #define WGAME_DEPTH_SHADOW_CLIP(input) WgCharacterDepthShadowClip((input).uv)
            #include "../ShaderLibrary/WGameDepthShadowPass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "MxFramework.Project.Editor.WGameCharacterTraditionalLitShaderGui"
}
