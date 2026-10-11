Shader "WGame/MagicVfxUnlit"
{
    Properties
    {
        [Toggle(_WG_VFX_TEXTURED)] _WgTexturedMode ("Texture and Vertex Color Mode", Float) = 0
        [MainTexture] _BaseMap ("Texture Mode: Color / Alpha", 2D) = "white" {}
        [HDR] _BaseColor ("Texture Mode: Tint", Color) = (1, 1, 1, 1)
        [Toggle(_EMISSION)] _WgTextureEmission ("Texture Mode: Emission", Float) = 0
        _EmissionMap ("Texture Mode: Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Texture Mode: Emission Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _SrcBlendAlpha ("Source Alpha Blend", Float) = 5
        [HideInInspector] _DstBlendAlpha ("Destination Alpha Blend", Float) = 10
        [HideInInspector] _ZWrite ("Depth Write", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 0
        [MainColor] _WgVfxColor ("VFX Color", Color) = (0.941, 0.706, 0.373, 0.72)
        _WgVfxSecondaryColor ("VFX Secondary Color", Color) = (1.0, 0.835, 0.545, 1)
        _WgEmissionIntensity ("Emission Intensity", Range(0, 4)) = 1.2
        _WgNoiseTex ("Noise Texture", 2D) = "gray" {}
        _WgMaskTex ("Mask Texture", 2D) = "white" {}
        _WgSoftness ("Softness", Range(0.001, 0.35)) = 0.08
        _WgDepthFadeDistance ("Depth Fade Distance", Range(0.01, 4)) = 0.65
        _WgBloomBudget ("Bloom Budget", Range(0, 1)) = 0.45
        _WgDissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _WgDissolveEdgeColor ("Dissolve Edge Color", Color) = (1.0, 0.75, 0.38, 1)
        _WgDissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.25)) = 0.04
        _WgStatusTintColor ("Status Tint Color", Color) = (0.509, 0.651, 0.420, 1)
        _WgStatusTintAmount ("Status Tint Amount", Range(0, 1)) = 0
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

            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _WG_VFX_TEXTURED
            #pragma shader_feature_local_fragment _EMISSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameFogBlend.hlsl"
            #include "../ShaderLibrary/WGameEmission.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgNoiseTex);
            SAMPLER(sampler_WgNoiseTex);
            TEXTURE2D(_WgMaskTex);
            SAMPLER(sampler_WgMaskTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _WgVfxColor;
                float4 _WgVfxSecondaryColor;
                float _WgEmissionIntensity;
                float _WgSoftness;
                float _WgDepthFadeDistance;
                float _WgBloomBudget;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgStatusTintColor;
                float _WgStatusTintAmount;
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

            float WgTransparentDepthFade(float4 positionCS, float eyeDepth, float fadeDistance)
            {
                float2 screenUv = positionCS.xy / _ScaledScreenParams.xy;
                float sceneRawDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                return saturate((sceneEyeDepth - eyeDepth) * WgSafeRcp(max(fadeDistance, WG_EPSILON)));
            }

            float WgAngularVfxShape(float2 uv, float softness)
            {
                float2 centered = uv * 2.0 - 1.0;
                float diamondDistance = abs(centered.x) + abs(centered.y);
                float outerDiamond = 1.0 - smoothstep(0.72, 0.72 + softness, diamondDistance);
                float innerCore = 1.0 - smoothstep(0.22, 0.22 + softness, diamondDistance);
                float edgeRay = 1.0 - smoothstep(0.035, 0.035 + softness * 0.45, min(abs(centered.x), abs(centered.y)));
                float rayMask = 1.0 - smoothstep(0.78, 1.0, max(abs(centered.x), abs(centered.y)));
                return saturate(max(outerDiamond, innerCore * 0.85) + edgeRay * rayMask * 0.22);
            }

            float4 Frag(Varyings input) : SV_Target
            {
#if defined(_WG_VFX_TEXTURED)
                // Preserve authored particle/line shape, vertex color and lifetime alpha without procedural reshaping.
                float2 uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor * input.color;
#if defined(_EMISSION)
                color.rgb += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
#endif
                return float4(max(color.rgb, 0.0), saturate(color.a));
#else
                float softness = max(_WgSoftness, WG_EPSILON);
                float maskValue = SAMPLE_TEXTURE2D(_WgMaskTex, sampler_WgMaskTex, input.uv).r;
                float noiseValue = SAMPLE_TEXTURE2D(_WgNoiseTex, sampler_WgNoiseTex, input.uv + _MxTime.yy * float2(0.035, -0.021)).r;

                float shape = WgAngularVfxShape(input.uv, softness) * saturate(maskValue);
                float edgeSeed = saturate(abs(input.uv.x - 0.5) + abs(input.uv.y - 0.5));
                float edge = WgSoftMask(edgeSeed + noiseValue * 0.18, 0.38, softness);
                float alpha = shape * _WgVfxColor.a * input.color.a;

                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                WgDissolveResult dissolve = WgEvaluateDissolve(noiseValue, dissolveParams);
                alpha = WgDissolveAlpha(alpha, dissolve);

                float3 color = lerp(_WgVfxColor.rgb, _WgVfxSecondaryColor.rgb, saturate(edge + dissolve.edge));
                color = WgOverlayColor(color, _WgStatusTintColor.rgb, _WgStatusTintAmount);
                color = WgOverlayColor(color, _WgDebugOverlayColor.rgb, _WgDebugOverlayAmount);
                color = WgApplyDissolveEdge(color, dissolve);

                WgEmissionParams emissionParams;
                emissionParams._WgEmissionColor = color;
                emissionParams._WgEmissionIntensity = _WgEmissionIntensity;
                emissionParams._WgEmissionMaskWeight = alpha;
                emissionParams._WgEmissionPulseAmount = 0.22;
                emissionParams._WgEmissionMaxIntensity = lerp(0.85, 2.35, saturate(_WgBloomBudget));
                color = WgAddEmission(color * 0.35, WgEvaluateEmission(alpha, _MxTime.y, 0.8, emissionParams));

                WgFogBlendParams fogParams;
                fogParams._WgFogColor = WgDefaultNightRuinFogColor();
                fogParams._WgFogDensity = 0.32;
                fogParams._WgFogHeightFalloff = 0.24;
                fogParams._WgFogStartDistance = 10.0;
                fogParams._WgFogEndDistance = 44.0;
                fogParams._WgFogBlendStrength = 0.28;
                fogParams._WgFogFocusClearRadius = 1.0;
                fogParams._WgFogFocusClearSoftness = 2.2;
                color = WgApplyNightRuinFog(color, input.positionWS, GetCameraPositionWS(), _MxViewFocusWorldPos.y, fogParams);

                float focusDistance = distance(input.positionWS, _MxViewFocusWorldPos.xyz);
                alpha *= lerp(1.0, smoothstep(0.35, 1.2, focusDistance), 0.22);
                alpha *= WgTransparentDepthFade(input.positionCS, input.eyeDepth, _WgDepthFadeDistance);

                return float4(saturate(color), saturate(alpha));
#endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
