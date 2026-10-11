Shader "WGame/WorldMarkerUnlit"
{
    Properties
    {
        [Toggle(_WG_MARKER_MESH)] _WgMeshCoverage ("Use Mesh Shape (No UV Mask)", Float) = 0
        [HideInInspector] _WgSrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _WgDstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _WgZWrite ("Depth Write", Float) = 0
        [MainColor] _WgMarkerColor ("Marker Color", Color) = (0.941, 0.706, 0.373, 0.82)
        _WgEdgeColor ("Edge Color", Color) = (1.0, 0.835, 0.545, 1)
        _WgMarkerTex ("Marker Texture", 2D) = "white" {}
        _WgEdgeWidth ("Edge Width", Range(0.005, 0.18)) = 0.045
        _WgDepthFadeDistance ("Depth Fade Distance", Range(0.01, 4)) = 0.45
        _WgScreenSizeClamp ("Screen Size Clamp", Range(0, 1)) = 0.72
        _WgMarkerIntensity ("Marker Intensity", Range(0, 2)) = 0.82
        _WgMarkerPulse ("Marker Pulse", Range(0, 1)) = 0.28
        _WgMarkerShape ("Marker Shape", Range(0, 1)) = 0
        _WgInnerFill ("Inner Fill", Range(0, 1)) = 0.18
        _WgFootprintScale ("Footprint Scale", Range(0.5, 1.4)) = 1.0
        _WgBloomBudget ("Bloom Budget", Range(0, 1)) = 0.32
        _WgDarkBackgroundBoost ("Dark Background Boost", Range(0, 1)) = 0.28
        _WgDissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _WgDissolveEdgeColor ("Dissolve Edge Color", Color) = (1.0, 0.75, 0.38, 1)
        _WgDissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.25)) = 0.035
        _WgOutlineColor ("Outline Color", Color) = (0.851, 0.788, 0.647, 1)
        _WgOutlineAmount ("Outline Amount", Range(0, 1)) = 0
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

            Blend [_WgSrcBlend] [_WgDstBlend]
            ZWrite [_WgZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _WG_MARKER_MESH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameFogBlend.hlsl"
            #include "../ShaderLibrary/WGameEmission.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgMarkerTex);
            SAMPLER(sampler_WgMarkerTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WgMarkerColor;
                float4 _WgEdgeColor;
                float _WgEdgeWidth;
                float _WgDepthFadeDistance;
                float _WgScreenSizeClamp;
                float _WgMarkerIntensity;
                float _WgMarkerPulse;
                float _WgMarkerShape;
                float _WgInnerFill;
                float _WgFootprintScale;
                float _WgBloomBudget;
                float _WgDarkBackgroundBoost;
                float _WgDissolveProgress;
                float4 _WgDissolveEdgeColor;
                float _WgDissolveEdgeWidth;
                float4 _WgOutlineColor;
                float _WgOutlineAmount;
                float4 _WgDebugOverlayColor;
                float _WgDebugOverlayAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float4 color : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
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

            float WgDiamondStroke(float2 centered, float edgeWidth)
            {
                float diamondDistance = abs(centered.x) + abs(centered.y);
                float outer = 1.0 - smoothstep(0.76, 0.76 + edgeWidth, diamondDistance);
                float inner = 1.0 - smoothstep(0.52, 0.52 + edgeWidth, diamondDistance);
                float crossPip = (1.0 - smoothstep(0.025, 0.025 + edgeWidth, min(abs(centered.x), abs(centered.y)))) *
                    (1.0 - smoothstep(0.36, 0.5, max(abs(centered.x), abs(centered.y))));
                float centerPip = 1.0 - smoothstep(0.14, 0.14 + edgeWidth, diamondDistance);
                return saturate(max(max(outer - inner, crossPip * 0.55), centerPip * saturate(_WgInnerFill)));
            }

            float WgCornerStroke(float2 centered, float edgeWidth)
            {
                float2 a = abs(centered);
                float cornerBand = smoothstep(0.46, 0.46 + edgeWidth, a.x) * smoothstep(0.46, 0.46 + edgeWidth, a.y);
                float horizontal = 1.0 - smoothstep(edgeWidth, edgeWidth * 2.0, abs(a.y - 0.66));
                horizontal *= smoothstep(0.48, 0.48 + edgeWidth, a.x) * (1.0 - smoothstep(0.9, 0.98, a.x));
                float vertical = 1.0 - smoothstep(edgeWidth, edgeWidth * 2.0, abs(a.x - 0.66));
                vertical *= smoothstep(0.48, 0.48 + edgeWidth, a.y) * (1.0 - smoothstep(0.9, 0.98, a.y));
                return saturate(max(horizontal, vertical) * cornerBand);
            }

            float WgBeaconStroke(float2 centered, float edgeWidth)
            {
                float2 a = abs(centered);
                float diamondDistance = a.x + a.y;
                float topDiamond = 1.0 - smoothstep(0.64, 0.64 + edgeWidth, diamondDistance + centered.y * 0.32);
                float topHollow = 1.0 - smoothstep(0.40, 0.40 + edgeWidth, diamondDistance + centered.y * 0.32);
                float lowerChevron = 1.0 - smoothstep(edgeWidth, edgeWidth * 2.0, abs(a.x - (0.28 + centered.y * 0.38)));
                lowerChevron *= smoothstep(-0.52, -0.35, centered.y) * (1.0 - smoothstep(0.20, 0.44, centered.y));
                float centerRail = 1.0 - smoothstep(0.018, 0.018 + edgeWidth * 0.55, abs(centered.x));
                centerRail *= 1.0 - smoothstep(0.58, 0.78, a.y);
                return saturate(max(max(topDiamond - topHollow, lowerChevron), centerRail * 0.45));
            }

            float WgMarkerShapeBlend(float diamond, float beacon, float corners, float shapeMode)
            {
                float targetWeight = saturate(1.0 - abs(shapeMode) * 2.0);
                float beaconWeight = saturate(1.0 - abs(shapeMode - 0.5) * 2.0);
                float cornerWeight = saturate(1.0 - abs(shapeMode - 1.0) * 2.0);
                float weighted = max(max(diamond * targetWeight, beacon * beaconWeight), corners * cornerWeight);
                float fallback = lerp(diamond, corners, shapeMode);
                return saturate(max(weighted, fallback * (1.0 - max(max(targetWeight, beaconWeight), cornerWeight))));
            }

            float4 Frag(Varyings input) : SV_Target
            {
#if defined(_WG_MARKER_MESH)
                // Grid, boundary and ring meshes already own their coverage. Do not crop them with a UV shape.
                return float4(max(_WgMarkerColor.rgb, 0.0), saturate(_WgMarkerColor.a));
#else
                float edgeWidth = max(_WgEdgeWidth * lerp(1.35, 0.75, saturate(_WgScreenSizeClamp)), WG_EPSILON);
                float footprintScale = max(_WgFootprintScale, 0.5);
                float2 centered = (input.uv * 2.0 - 1.0) * WgSafeRcp(footprintScale);
                float diamond = WgDiamondStroke(centered, edgeWidth);
                float beacon = WgBeaconStroke(centered, edgeWidth);
                float corners = WgCornerStroke(centered, edgeWidth);
                float shape = WgMarkerShapeBlend(diamond, beacon, corners, saturate(_WgMarkerShape));
                float texMask = SAMPLE_TEXTURE2D(_WgMarkerTex, sampler_WgMarkerTex, input.uv).r;

                float pulse = lerp(1.0, WgPulse01(_MxTime.y, 0.64, 0.15), saturate(_WgMarkerPulse));
                float intensity = saturate(_WgMarkerIntensity * pulse);
                float alpha = shape * texMask * _WgMarkerColor.a * input.color.a * intensity;

                float dissolveNoise = saturate(shape + texMask * 0.35);
                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                WgDissolveResult dissolve = WgEvaluateDissolve(dissolveNoise, dissolveParams);
                alpha = WgDissolveAlpha(alpha, dissolve);

                float edgeMix = saturate(shape * 1.25 + _WgOutlineAmount);
                float darkBoost = saturate(_WgDarkBackgroundBoost);
                float3 boostedMarker = lerp(_WgMarkerColor.rgb, max(_WgMarkerColor.rgb, WgColorParchment200 * 0.72), darkBoost * 0.25);
                float3 boostedEdge = lerp(_WgEdgeColor.rgb, max(_WgEdgeColor.rgb, WgColorParchment200 * 0.82), darkBoost * 0.2);
                float3 color = lerp(boostedMarker, boostedEdge, edgeMix);
                color = lerp(color, _WgOutlineColor.rgb, saturate(_WgOutlineAmount));
                color = WgOverlayColor(color, _WgDebugOverlayColor.rgb, _WgDebugOverlayAmount);
                color = WgApplyDissolveEdge(color, dissolve);

                WgEmissionParams emissionParams;
                emissionParams._WgEmissionColor = color;
                emissionParams._WgEmissionIntensity = lerp(0.58, 1.0, saturate(_WgBloomBudget));
                emissionParams._WgEmissionMaskWeight = alpha;
                emissionParams._WgEmissionPulseAmount = _WgMarkerPulse * 0.25;
                emissionParams._WgEmissionMaxIntensity = lerp(0.82, 1.35, saturate(_WgBloomBudget));
                color = WgAddEmission(color * 0.5, WgEvaluateEmission(alpha, _MxTime.y, 0.64, emissionParams));

                WgFogBlendParams fogParams;
                fogParams._WgFogColor = WgDefaultNightRuinFogColor();
                fogParams._WgFogDensity = 0.28;
                fogParams._WgFogHeightFalloff = 0.18;
                fogParams._WgFogStartDistance = 12.0;
                fogParams._WgFogEndDistance = 46.0;
                fogParams._WgFogBlendStrength = 0.18;
                fogParams._WgFogFocusClearRadius = 0.85;
                fogParams._WgFogFocusClearSoftness = 2.0;
                color = WgApplyNightRuinFog(color, input.positionWS, GetCameraPositionWS(), _MxViewFocusWorldPos.y, fogParams);

                float focusDistance = distance(input.positionWS, _MxViewFocusWorldPos.xyz);
                alpha *= lerp(1.0, smoothstep(0.25, 0.95, focusDistance), 0.18);
                alpha *= WgTransparentDepthFade(input.positionCS, input.eyeDepth, _WgDepthFadeDistance);

                return float4(saturate(color), saturate(alpha));
#endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
