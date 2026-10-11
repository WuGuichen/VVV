Shader "WGame/PropStylizedLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        _Color ("Albedo Tint", Color) = (1, 1, 1, 1)
        _WgAlbedoStrength ("Traditional Albedo Strength", Range(0, 1)) = 0
        [MainColor] _WgBaseColor ("Base Color", Color) = (0.141, 0.204, 0.227, 1)
        _WgBronzeTrimColor ("Bronze Trim Color", Color) = (0.608, 0.463, 0.314, 1)
        _WgInteractableGlowColor ("Interactable Glow Color", Color) = (0.941, 0.706, 0.373, 1)
        _WgRampTex ("Ramp Texture", 2D) = "white" {}
        _WgMaskTex ("Mask Texture", 2D) = "black" {}
        _WgNormalTex ("Normal Texture", 2D) = "bump" {}
        [Toggle(_WG_PALETTE_ON)] _WgPaletteEnabled ("Palette Enabled", Float) = 0
        _WgColorPaletteTex ("Shared RGB Color Palette", 2D) = "white" {}
        _WgColorPaletteParams ("Color Palette Params", Vector) = (256, 256, 0.00390625, 0.00390625)
        _WgDissolveNoiseTex ("Dissolve Noise Texture", 2D) = "gray" {}
        _WgBronzeTrimAmount ("Bronze Trim Amount", Range(0, 1)) = 0.25
        _WgInteractableGlowAmount ("Interactable Glow Amount", Range(0, 1)) = 0.18
        _WgFacetStrength ("Facet Strength", Range(0, 1)) = 0.42
        _WgFogBlendStrength ("Fog Blend Strength", Range(0, 1)) = 0.42
        _WgStatusTintColor ("Status Tint Color", Color) = (0.376, 0.380, 0.353, 1)
        _WgStatusTintAmount ("Status Tint Amount", Range(0, 1)) = 0
        _WgDissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _WgDissolveEdgeColor ("Dissolve Edge Color", Color) = (1.0, 0.72, 0.35, 1)
        _WgDissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.25)) = 0.055
        _WgOutlineColor ("Outline Color", Color) = (0.941, 0.706, 0.373, 1)
        _WgOutlineAmount ("Outline Amount", Range(0, 1)) = 0
        _WgHitFlashColor ("Hit Flash Color", Color) = (1.0, 0.72, 0.5, 1)
        _WgHitFlashAmount ("Hit Flash Amount", Range(0, 1)) = 0
        _WgDebugOverlayColor ("Debug Overlay Color", Color) = (0.427, 0.608, 0.710, 1)
        _WgDebugOverlayAmount ("Debug Overlay Amount", Range(0, 1)) = 0
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

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _WG_PALETTE_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameStylizedLighting.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            #include "../ShaderLibrary/WGameFogBlend.hlsl"
            #include "../ShaderLibrary/WGameEmission.hlsl"
            #include "../ShaderLibrary/WGameRimLighting.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"
            #include "../ShaderLibrary/WGamePalette.hlsl"

            TEXTURE2D(_WgRampTex);
            SAMPLER(sampler_WgRampTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WgMaskTex);
            SAMPLER(sampler_WgMaskTex);
            TEXTURE2D(_WgNormalTex);
            SAMPLER(sampler_WgNormalTex);
            TEXTURE2D(_WgColorPaletteTex);
            SAMPLER(sampler_WgColorPaletteTex);
            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WgBaseColor;
                float4 _WgBronzeTrimColor;
                float4 _WgInteractableGlowColor;
                float _WgAlbedoStrength;
                float _WgPaletteEnabled;
                float4 _WgColorPaletteParams;
                float _WgBronzeTrimAmount;
                float _WgInteractableGlowAmount;
                float _WgFacetStrength;
                float _WgFogBlendStrength;
                float4 _WgStatusTintColor;
                float _WgStatusTintAmount;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgOutlineColor;
                float _WgOutlineAmount;
                float4 _WgHitFlashColor;
                float _WgHitFlashAmount;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 paletteUv : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float2 paletteUv : TEXCOORD4;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.paletteUv = input.paletteUv;
                output.color = input.color;
                return output;
            }

            float3 WgPropNormalWS(Varyings input)
            {
                float3 normalWS = WgSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                float3 tangentWS = WgSafeNormalize(input.tangentWS.xyz, float3(1.0, 0.0, 0.0));
                float3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;
                float3 normalTS = SAMPLE_TEXTURE2D(_WgNormalTex, sampler_WgNormalTex, input.uv).xyz * 2.0 - 1.0;
                return WgSafeNormalize(normalTS.x * tangentWS + normalTS.y * bitangentWS + normalTS.z * normalWS, normalWS);
            }

            float3 WgPropSurfaceColor(WgPackedMaterialMask materialMask)
            {
                float trimAmount = saturate(max(materialMask.trim, _WgBronzeTrimAmount));
                float glowAmount = saturate(max(materialMask.emission, _WgInteractableGlowAmount));
                float wornAmount = saturate(materialMask.primary * (1.0 - trimAmount));
                float3 wornBase = lerp(_WgBaseColor.rgb, WgColorStone500, wornAmount * 0.28);
                float3 trimmed = lerp(wornBase, _WgBronzeTrimColor.rgb, trimAmount);
                return lerp(trimmed, _WgInteractableGlowColor.rgb, glowAmount * 0.16);
            }

            float4 Frag(Varyings input) : SV_Target
            {
#if defined(_WG_PALETTE_ON)
                if (_WgPaletteEnabled > 0.5)
                {
                    float2 paletteSampleUv = input.paletteUv.xy;
                    if (WgColorPaletteUvIsValid(paletteSampleUv) < 0.5)
                        return float4(1.0, 0.0, 1.0, 1.0);
                    float4 paletteSample = SAMPLE_TEXTURE2D_LOD(_WgColorPaletteTex, sampler_WgColorPaletteTex, paletteSampleUv, 0.0);
                    WgPaletteVertexData paletteVertex = WgDecodePaletteVertexData(input.color);
                    float3 normalWS = WgSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                    float3 viewDirectionWS = WgSafeNormalize(GetCameraPositionWS() - input.positionWS, float3(0.0, 0.0, 1.0));
                    WgMainLightData mainLight = WgGetMainLightData(input.positionWS, float3(0.32, 0.79, 0.45));
                    float wrappedLight = WgHalfLambert(normalWS, mainLight.directionWS, 0.32);
                    float steppedLight = WgQuantizeRamp(wrappedLight, 3.0);
                    float attenuation = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                    float3 color = paletteSample.rgb * lerp(0.76, 1.12, steppedLight * attenuation);
                    color *= lerp(0.62, 1.0, paletteVertex.ao);
                    float3 beforeMaterialResponse = color;
                    color = WgApplyPaletteMaterialResponse(
                        color,
                        paletteSample.rgb,
                        paletteVertex.surfaceProfile,
                        normalWS,
                        mainLight.directionWS,
                        viewDirectionWS,
                        paletteVertex.rim,
                        1.0,
                        2.6,
                        mainLight.color);
                    if (_MxDirectionalShadowEnabled > 0.5)
                        color = beforeMaterialResponse + (color - beforeMaterialResponse) * attenuation;

                    float wetness = saturate(max(_MxWetness, _MxRain * 0.6));
                    color = lerp(color, color * float3(0.7, 0.82, 0.86), wetness * 0.2);
                    color = WgApplyMainLightColor(color, mainLight, 0.08);
                    color = WgApplyMainLightShadowReceive(color, mainLight, 0.68);
                    color = WgOverlayColor(color, _WgStatusTintColor.rgb, _WgStatusTintAmount);
                    color = WgOverlayColor(color, _WgHitFlashColor.rgb, _WgHitFlashAmount);
                    color = WgOverlayColor(color, _WgDebugOverlayColor.rgb, _WgDebugOverlayAmount);

                    WgRimLightingParams outlineParams;
                    outlineParams._WgRimColor = _WgOutlineColor.rgb;
                    outlineParams._WgRimAmount = _WgOutlineAmount;
                    outlineParams._WgRimPower = 1.35;
                    outlineParams._WgRimMin = 0.04;
                    outlineParams._WgRimMax = 0.84;
                    color = WgApplyRimLighting(color, normalWS, viewDirectionWS, outlineParams);

                    float pulse = 0.82 + sin(_MxTime.y * 2.8902652) * 0.18;
                    color += paletteSample.rgb * paletteVertex.emission * pulse * 1.25;

                    float dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, input.uv).r;
                    WgDissolveParams dissolveParams;
                    dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                    dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                    dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                    dissolveParams._WgLifetimeFade = 0.0;
                    dissolveParams._WgDissolveNoiseStrength = 1.0;
                    WgDissolveResult dissolve = WgEvaluateDissolve(dissolveNoise, dissolveParams);
                    if (_WgDissolveProgress > WG_EPSILON)
                        clip(dissolve.clipValue);
                    color = WgApplyDissolveEdge(color, dissolve);

                    WgFogBlendParams fogParams;
                    fogParams._WgFogColor = WgDefaultNightRuinFogColor();
                    fogParams._WgFogDensity = 0.34;
                    fogParams._WgFogHeightFalloff = 0.24;
                    fogParams._WgFogStartDistance = 8.0;
                    fogParams._WgFogEndDistance = 44.0;
                    fogParams._WgFogBlendStrength = _WgFogBlendStrength;
                    fogParams._WgFogFocusClearRadius = 1.6;
                    fogParams._WgFogFocusClearSoftness = 2.6;
                    color = WgApplyNightRuinFog(color, input.positionWS, GetCameraPositionWS(), _MxLocalSubjectWorldPos.y, fogParams);
                    return float4(saturate(color), 1.0);
                }
#endif
                float4 maskSample = SAMPLE_TEXTURE2D(_WgMaskTex, sampler_WgMaskTex, input.uv);
                WgPackedMaterialMask materialMask = WgDecodePackedMaterialMask(maskSample);

                float3 normalWS = WgPropNormalWS(input);
                float3 viewDirectionWS = WgSafeNormalize(GetCameraPositionWS() - input.positionWS, float3(0.0, 0.0, 1.0));
                WgMainLightData mainLight = WgGetMainLightData(input.positionWS, float3(0.32, 0.79, 0.45));
                float3 lightDirectionWS = mainLight.directionWS;
                float3 stylizedBase = WgPropSurfaceColor(materialMask);
                float3 traditionalBase = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _Color.rgb;
                float3 surfaceColor = lerp(stylizedBase, traditionalBase, saturate(_WgAlbedoStrength));
                float trimAmount = saturate(max(materialMask.trim, _WgBronzeTrimAmount));
                float glowAmount = saturate(max(materialMask.emission, _WgInteractableGlowAmount));
                float rampSeed = saturate(materialMask.primary * 0.22 + trimAmount * 0.52 + glowAmount * 0.28);

                WgStylizedLightingInput lightingInput;
                lightingInput.normalWS = normalWS;
                lightingInput.lightDirectionWS = lightDirectionWS;
                lightingInput.viewDirectionWS = viewDirectionWS;
                lightingInput.baseColor = surfaceColor;
                lightingInput.rampValue = rampSeed;
                lightingInput.facetValue = saturate(max(materialMask.secondary, WgLuminance(maskSample.rgb) * 0.65));

                WgStylizedLightingParams lightingParams;
                lightingParams._WgRampContrast = 1.28;
                lightingParams._WgRampOffset = -0.06;
                lightingParams._WgRampStepCount = 3.0;
                lightingParams._WgFacetStrength = _WgFacetStrength;
                lightingParams._WgShadowStrength = 0.22;
                lightingParams._WgLightWrap = 0.32;

                float3 color = WgApplyStylizedLighting(
                    lightingInput,
                    lightingParams,
                    WgColorObsidian950,
                    max(surfaceColor, WgColorParchment200 * 0.38));
                float3 rampColor = SAMPLE_TEXTURE2D(_WgRampTex, sampler_WgRampTex, WgRampUv(rampSeed, 0.5)).rgb;
                color = lerp(color, color * max(rampColor, 0.38), saturate(WgLuminance(rampColor) * 0.75));

                if (_MxDirectionalShadowEnabled > 0.5)
                {
                    // Keep the authored fill/ramp; only the direct portion loses visibility.
                    float3 fill = surfaceColor * lightingParams._WgShadowStrength;
                    fill = lerp(fill, fill * max(rampColor, 0.38), saturate(WgLuminance(rampColor) * 0.75));
                    color = fill + max(0, color - fill) * saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                }
                float wetness = saturate(max(_MxWetness, _MxRain * 0.6));
                color = lerp(color, color * float3(0.7, 0.82, 0.86), wetness * 0.2);
                color = WgApplyMainLightColor(color, mainLight, 0.08);
                color = WgApplyMainLightShadowReceive(color, mainLight, 0.68);
                color = WgOverlayColor(color, _WgStatusTintColor.rgb, _WgStatusTintAmount);
                color = WgOverlayColor(color, _WgHitFlashColor.rgb, _WgHitFlashAmount);
                color = WgOverlayColor(color, _WgDebugOverlayColor.rgb, _WgDebugOverlayAmount);

                WgRimLightingParams rimParams;
                rimParams._WgRimColor = lerp(WgColorParchment200, _WgInteractableGlowColor.rgb, glowAmount);
                rimParams._WgRimAmount = 0.18 + glowAmount * 0.24;
                rimParams._WgRimPower = 2.4;
                rimParams._WgRimMin = 0.16;
                rimParams._WgRimMax = 0.94;
                color = WgApplyRimLighting(color, normalWS, viewDirectionWS, rimParams);

                WgRimLightingParams outlineParams;
                outlineParams._WgRimColor = _WgOutlineColor.rgb;
                outlineParams._WgRimAmount = _WgOutlineAmount;
                outlineParams._WgRimPower = 1.35;
                outlineParams._WgRimMin = 0.04;
                outlineParams._WgRimMax = 0.84;
                color = WgApplyRimLighting(color, normalWS, viewDirectionWS, outlineParams);

                WgEmissionParams glowParams;
                glowParams._WgEmissionColor = _WgInteractableGlowColor.rgb;
                glowParams._WgEmissionIntensity = 0.65;
                glowParams._WgEmissionMaskWeight = glowAmount;
                glowParams._WgEmissionPulseAmount = 0.18;
                glowParams._WgEmissionMaxIntensity = 1.15;
                color = WgAddEmission(color, WgEvaluateEmission(glowAmount, _MxTime.y, 0.46, glowParams));

                float dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, input.uv).r;
                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                WgDissolveResult dissolve = WgEvaluateDissolve(dissolveNoise, dissolveParams);
                if (_WgDissolveProgress > WG_EPSILON)
                    clip(dissolve.clipValue);
                color = WgApplyDissolveEdge(color, dissolve);

                WgFogBlendParams fogParams;
                fogParams._WgFogColor = WgDefaultNightRuinFogColor();
                fogParams._WgFogDensity = 0.34;
                fogParams._WgFogHeightFalloff = 0.24;
                fogParams._WgFogStartDistance = 8.0;
                fogParams._WgFogEndDistance = 44.0;
                fogParams._WgFogBlendStrength = _WgFogBlendStrength;
                fogParams._WgFogFocusClearRadius = 1.6;
                fogParams._WgFogFocusClearSoftness = 2.6;
                color = WgApplyNightRuinFog(color, input.positionWS, GetCameraPositionWS(), _MxLocalSubjectWorldPos.y, fogParams);

                return float4(saturate(color), _WgBaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WgShadowCasterVertex
            #pragma fragment WgShadowCasterFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WgBaseColor;
                float4 _WgBronzeTrimColor;
                float4 _WgInteractableGlowColor;
                float _WgAlbedoStrength;
                float _WgPaletteEnabled;
                float4 _WgColorPaletteParams;
                float _WgBronzeTrimAmount;
                float _WgInteractableGlowAmount;
                float _WgFacetStrength;
                float _WgFogBlendStrength;
                float4 _WgStatusTintColor;
                float _WgStatusTintAmount;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgOutlineColor;
                float _WgOutlineAmount;
                float4 _WgHitFlashColor;
                float _WgHitFlashAmount;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END

            void WgPropDepthShadowClip(float2 uv)
            {
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

            #define WGAME_DEPTH_SHADOW_CLIP(input) WgPropDepthShadowClip((input).uv)
            #include "../ShaderLibrary/WGameDepthShadowPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WgDepthOnlyVertex
            #pragma fragment WgDepthOnlyFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WgBaseColor;
                float4 _WgBronzeTrimColor;
                float4 _WgInteractableGlowColor;
                float _WgAlbedoStrength;
                float _WgPaletteEnabled;
                float4 _WgColorPaletteParams;
                float _WgBronzeTrimAmount;
                float _WgInteractableGlowAmount;
                float _WgFacetStrength;
                float _WgFogBlendStrength;
                float4 _WgStatusTintColor;
                float _WgStatusTintAmount;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgOutlineColor;
                float _WgOutlineAmount;
                float4 _WgHitFlashColor;
                float _WgHitFlashAmount;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END

            void WgPropDepthShadowClip(float2 uv)
            {
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

            #define WGAME_DEPTH_SHADOW_CLIP(input) WgPropDepthShadowClip((input).uv)
            #include "../ShaderLibrary/WGameDepthShadowPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull Back ZWrite On ZTest LEqual
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WgAuxVert
            #pragma fragment WgAuxFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WgBaseColor;
                float4 _WgBronzeTrimColor;
                float4 _WgInteractableGlowColor;
                float _WgAlbedoStrength;
                float _WgPaletteEnabled;
                float4 _WgColorPaletteParams;
                float _WgBronzeTrimAmount;
                float _WgInteractableGlowAmount;
                float _WgFacetStrength;
                float _WgFogBlendStrength;
                float4 _WgStatusTintColor;
                float _WgStatusTintAmount;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgOutlineColor;
                float _WgOutlineAmount;
                float4 _WgHitFlashColor;
                float _WgHitFlashAmount;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END
            #define WG_AUX_CHARACTER 1
            #define WG_AUX_TEXTURE_DISSOLVE 1
            
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            Cull Back ZWrite On ZTest LEqual
            ColorMask RG
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WgAuxVert
            #pragma fragment WgAuxFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WgBaseColor;
                float4 _WgBronzeTrimColor;
                float4 _WgInteractableGlowColor;
                float _WgAlbedoStrength;
                float _WgPaletteEnabled;
                float4 _WgColorPaletteParams;
                float _WgBronzeTrimAmount;
                float _WgInteractableGlowAmount;
                float _WgFacetStrength;
                float _WgFogBlendStrength;
                float4 _WgStatusTintColor;
                float _WgStatusTintAmount;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgOutlineColor;
                float _WgOutlineAmount;
                float4 _WgHitFlashColor;
                float _WgHitFlashAmount;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END
            #define WG_AUX_CHARACTER 1
            #define WG_AUX_TEXTURE_DISSOLVE 1
            #define WG_AUX_MOTION 1
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

    }

    FallBack Off
}
