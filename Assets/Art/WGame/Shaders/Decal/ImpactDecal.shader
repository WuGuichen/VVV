Shader "WGame/ImpactDecal"
{
    Properties
    {
        [MainColor] _WgDecalColor ("Decal Color", Color) = (0.165, 0.070, 0.055, 0.68)
        _WgDecalTex ("Decal Texture", 2D) = "white" {}
        _WgCrackTex ("Crack Texture", 2D) = "gray" {}
        _WgEmissionColor ("Emission Color", Color) = (1.0, 0.55, 0.25, 1)
        _WgEmissionIntensity ("Emission Intensity", Range(0, 3)) = 0.55
        _WgEdgeWidth ("Edge Width", Range(0.001, 0.25)) = 0.045
        _WgBlendToStone ("Blend To Stone", Range(0, 1)) = 0.52
        _WgDissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _WgLifetimeFade ("Lifetime Fade", Range(0, 1)) = 0
        _WgHitFlashColor ("Hit Flash Color", Color) = (1.0, 0.74, 0.42, 1)
        _WgHitFlashAmount ("Hit Flash Amount", Range(0, 1)) = 0
        _WgDepthFadeDistance ("Depth Fade Distance", Range(0.01, 3)) = 0.35
        _WgBloomBudget ("Bloom Budget", Range(0, 1)) = 0.28
        _WgDebugOverlayColor ("Debug Overlay Color", Color) = (0.427, 0.608, 0.710, 1)
        _WgDebugOverlayAmount ("Debug Overlay Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameStylizedLighting.hlsl"
            #include "../ShaderLibrary/WGameFogBlend.hlsl"
            #include "../ShaderLibrary/WGameEmission.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDecalTex);
            SAMPLER(sampler_WgDecalTex);
            TEXTURE2D(_WgCrackTex);
            SAMPLER(sampler_WgCrackTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WgDecalColor;
                float4 _WgEmissionColor;
                float _WgEmissionIntensity;
                float _WgEdgeWidth;
                float _WgBlendToStone;
                float _WgDissolveProgress;
                float _WgLifetimeFade;
                float4 _WgHitFlashColor;
                float _WgHitFlashAmount;
                float _WgDepthFadeDistance;
                float _WgBloomBudget;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                float4 color : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                output.color = input.color;
                return output;
            }

            float WgImpactDepthFade(float4 positionCS, float eyeDepth, float fadeDistance)
            {
                float2 screenUv = positionCS.xy / _ScaledScreenParams.xy;
                float sceneRawDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                return saturate((sceneEyeDepth - eyeDepth) * WgSafeRcp(max(fadeDistance, WG_EPSILON)));
            }

            float WgImpactBurstMask(float2 uv, float edgeWidth)
            {
                float2 centered = uv * 2.0 - 1.0;
                float2 absCentered = abs(centered);
                float diamond = absCentered.x + absCentered.y;
                float core = 1.0 - smoothstep(0.12, 0.12 + edgeWidth * 1.6, diamond);
                float outer = 1.0 - smoothstep(0.62, 0.62 + edgeWidth * 2.5, diamond);
                float chipA = 1.0 - smoothstep(edgeWidth, edgeWidth * 2.0, abs(absCentered.y - absCentered.x * 0.38));
                chipA *= smoothstep(0.12, 0.34, diamond) * (1.0 - smoothstep(0.72, 0.94, diamond));
                float chipB = 1.0 - smoothstep(edgeWidth, edgeWidth * 1.8, abs(absCentered.x - absCentered.y * 0.24));
                chipB *= smoothstep(0.22, 0.44, diamond) * (1.0 - smoothstep(0.78, 1.02, diamond));
                return saturate(max(core, outer * 0.45) + max(chipA, chipB) * 0.48);
            }

            float WgCrackMask(float2 uv, float crackTex, float edgeWidth)
            {
                float2 centered = uv * 2.0 - 1.0;
                float mainCrack = 1.0 - smoothstep(edgeWidth, edgeWidth * 2.3, abs(centered.x + centered.y * 0.22));
                mainCrack *= smoothstep(-0.92, -0.22, centered.y) * (1.0 - smoothstep(0.28, 0.92, centered.y));
                float branchA = 1.0 - smoothstep(edgeWidth, edgeWidth * 2.0, abs(centered.x - centered.y * 0.64 - 0.22));
                branchA *= smoothstep(-0.38, 0.0, centered.y) * (1.0 - smoothstep(0.32, 0.68, centered.y));
                float branchB = 1.0 - smoothstep(edgeWidth, edgeWidth * 1.9, abs(centered.x + centered.y * 0.72 + 0.24));
                branchB *= smoothstep(-0.7, -0.34, centered.y) * (1.0 - smoothstep(-0.14, 0.18, centered.y));
                float brokenEdge = 1.0 - smoothstep(0.58, 0.58 + edgeWidth * 2.0, abs(centered.x) + abs(centered.y));
                return saturate(max(max(mainCrack, branchA * 0.72), branchB * 0.64) + brokenEdge * crackTex * 0.32);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float edgeWidth = max(_WgEdgeWidth, WG_EPSILON);
                float decalTex = SAMPLE_TEXTURE2D(_WgDecalTex, sampler_WgDecalTex, input.uv).r;
                float crackTex = SAMPLE_TEXTURE2D(_WgCrackTex, sampler_WgCrackTex, input.uv + _MxTime.yy * float2(0.004, -0.003)).r;
                float burst = WgImpactBurstMask(input.uv, edgeWidth);
                float crack = WgCrackMask(input.uv, crackTex, edgeWidth);
                float shape = saturate(max(burst * 0.78, crack) * decalTex);

                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = edgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgEmissionColor.rgb;
                dissolveParams._WgLifetimeFade = _WgLifetimeFade;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                WgDissolveResult dissolve = WgEvaluateDissolve(saturate(max(crackTex, shape)), dissolveParams);

                float alpha = _WgDecalColor.a * input.color.a * shape;
                alpha = WgDissolveAlpha(alpha, dissolve);
                alpha *= saturate(1.0 - _WgLifetimeFade * 0.72);
                alpha *= WgImpactDepthFade(input.positionCS, input.eyeDepth, _WgDepthFadeDistance);

                float3 stoneBlend = lerp(WgColorObsidian950, WgColorStone500, 0.28);
                float3 color = lerp(_WgDecalColor.rgb, stoneBlend, saturate(_WgBlendToStone) * (1.0 - crack * 0.55));
                color = lerp(color, _WgEmissionColor.rgb, saturate(dissolve.edge + crack * 0.26));
                color = WgOverlayColor(color, _WgHitFlashColor.rgb, _WgHitFlashAmount);
                color = WgOverlayColor(color, _WgDebugOverlayColor.rgb, _WgDebugOverlayAmount);
                color = WgApplyDissolveEdge(color, dissolve);

                WgStylizedLightingInput lightingInput;
                lightingInput.normalWS = WgSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                lightingInput.lightDirectionWS = WgSafeNormalize(float3(0.28, 0.78, 0.46), float3(0.0, 1.0, 0.0));
                lightingInput.viewDirectionWS = WgSafeNormalize(GetCameraPositionWS() - input.positionWS, float3(0.0, 0.0, 1.0));
                lightingInput.baseColor = color;
                lightingInput.rampValue = saturate(shape * 0.44 + crack * 0.4);
                lightingInput.facetValue = saturate(crackTex * 0.55 + crack * 0.35);

                WgStylizedLightingParams lightingParams;
                lightingParams._WgRampContrast = 1.12;
                lightingParams._WgRampOffset = -0.1;
                lightingParams._WgRampStepCount = 3.0;
                lightingParams._WgFacetStrength = 0.25;
                lightingParams._WgShadowStrength = 0.2;
                lightingParams._WgLightWrap = 0.34;
                color = WgApplyStylizedLighting(lightingInput, lightingParams, WgColorObsidian950, max(color, WgColorParchment200 * 0.32));

                WgEmissionParams emissionParams;
                emissionParams._WgEmissionColor = _WgEmissionColor.rgb;
                emissionParams._WgEmissionIntensity = _WgEmissionIntensity;
                emissionParams._WgEmissionMaskWeight = saturate(alpha * (crack + dissolve.edge + _WgHitFlashAmount * 0.5));
                emissionParams._WgEmissionPulseAmount = 0.12;
                emissionParams._WgEmissionMaxIntensity = lerp(0.55, 1.55, saturate(_WgBloomBudget));
                color = WgAddEmission(color * 0.78, WgEvaluateEmission(alpha, _MxTime.y, 0.7, emissionParams));

                WgFogBlendParams fogParams;
                fogParams._WgFogColor = WgDefaultNightRuinFogColor();
                fogParams._WgFogDensity = 0.26;
                fogParams._WgFogHeightFalloff = 0.18;
                fogParams._WgFogStartDistance = 10.0;
                fogParams._WgFogEndDistance = 42.0;
                fogParams._WgFogBlendStrength = 0.2;
                fogParams._WgFogFocusClearRadius = 0.9;
                fogParams._WgFogFocusClearSoftness = 2.2;
                color = WgApplyNightRuinFog(color, input.positionWS, GetCameraPositionWS(), _MxViewFocusWorldPos.y, fogParams);

                return float4(saturate(color), saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
