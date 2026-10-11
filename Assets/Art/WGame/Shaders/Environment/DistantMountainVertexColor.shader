Shader "WGame/DistantMountainVertexColor"
{
    Properties
    {
        [MainColor] _WgMountainTint ("Mountain Tint", Color) = (1, 1, 1, 1)
        [Enum(BakedColor, 0, AlbedoAO, 1)] _WgMountainColorMode ("Vertex Color Content", Float) = 0
        _WgMountainLightingStrength ("Lighting Strength", Range(0, 1)) = 1
        _WgMountainLegacyRelight ("Baked Color Relighting", Range(0, 1)) = 0.18
        _WgMountainShadowTint ("Cool Ambient Tint", Color) = (0.68, 0.80, 0.94, 1)
        _WgMountainLightTint ("Warm Light Tint", Color) = (1.06, 1.025, 0.94, 1)
        _WgMountainAmbient ("Ambient Strength", Range(0, 1)) = 0.52
        _WgMountainDirect ("Main Light Strength", Range(0, 2)) = 0.8
        _WgMountainWrap ("Light Wrap", Range(0, 1)) = 0.28
        _WgMountainAO ("Vertex Alpha Occlusion", Range(0, 1)) = 0.65
        _WgMountainRidgeTint ("Exposed Ridge Tint", Color) = (1.0, 0.94, 0.80, 1)
        _WgMountainRidge ("Exposed Ridge Strength", Range(0, 0.3)) = 0.08
        _WgMountainFogColor ("Distance Haze Color", Color) = (0.43, 0.54, 0.61, 1)
        _WgMountainFogStart ("Distance Haze Start", Float) = 80
        _WgMountainFogEnd ("Distance Haze End", Float) = 450
        _WgMountainFogStrength ("Distance Haze Strength", Range(0, 1)) = 0.35
        _WgMountainHazeBase ("Valley Haze Base Height", Float) = 0
        _WgMountainHazeHeight ("Valley Haze Height", Float) = 60
        _WgMountainHazeStrength ("Valley Haze Strength", Range(0, 1)) = 0.08
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

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "../ShaderLibrary/WGameFrameGlobals.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WgMountainTint;
                half4 _WgMountainShadowTint;
                half4 _WgMountainLightTint;
                half4 _WgMountainRidgeTint;
                half4 _WgMountainFogColor;
                half _WgMountainColorMode;
                half _WgMountainLightingStrength;
                half _WgMountainLegacyRelight;
                half _WgMountainAmbient;
                half _WgMountainDirect;
                half _WgMountainWrap;
                half _WgMountainAO;
                half _WgMountainRidge;
                float _WgMountainFogStart;
                float _WgMountainFogEnd;
                half _WgMountainFogStrength;
                float _WgMountainHazeBase;
                float _WgMountainHazeHeight;
                half _WgMountainHazeStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionWS;

                float fogRange = max(_WgMountainFogEnd - _WgMountainFogStart, 0.001);
                float cameraDistance = distance(positionWS, GetCameraPositionWS());
                half distanceHaze = saturate((cameraDistance - _WgMountainFogStart) / fogRange);
                distanceHaze = distanceHaze * distanceHaze * (3.0h - 2.0h * distanceHaze);
                half valley = 1.0h - saturate((positionWS.y - _WgMountainHazeBase)
                    / max(_WgMountainHazeHeight, 0.001));
                // Valley haze grows with distance, never washing out nearby geometry.
                // Keep authored distant aerial perspective. Only the material's
                // valley-height contribution is replaced by the shared height fog.
                half valleyStrength = _MxPipelineHeightFogEnabled > 0.5 ? 0 : _WgMountainHazeStrength;
                output.fogFactor = saturate(distanceHaze * _WgMountainFogStrength
                    + distanceHaze * (1.0h - _WgMountainFogStrength) * valley * valleyStrength);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 mountainColor = input.color.rgb * _WgMountainTint.rgb;
                half normalLengthSq = dot(input.normalWS, input.normalWS);
                half3 normalWS = input.normalWS * rsqrt(max(normalLengthSq, 0.0001h));
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Parameterless GetMainLight does not sample a shadow map or cookie.
                Light mainLight = GetMainLight();
                half wrappedLight = saturate((dot(normalWS, mainLight.direction) + _WgMountainWrap)
                    / (1.0h + _WgMountainWrap));
                wrappedLight = wrappedLight * wrappedLight * (3.0h - 2.0h * wrappedLight);
                half skyFill = lerp(0.8h, 1.0h, saturate(normalWS.y * 0.5h + 0.5h));
                half3 directLight = mainLight.color * mainLight.distanceAttenuation;
                half3 lighting = _WgMountainShadowTint.rgb * (_WgMountainAmbient * skyFill)
                    + _WgMountainLightTint.rgb * directLight * (wrappedLight * _WgMountainDirect);

                // Legacy RGB already contains directional shading; only gently relight it.
                // AlbedoAO uses unlit RGB and linear alpha (0=occluded, 1=open).
                half albedoMode = saturate(_WgMountainColorMode);
                half relight = _WgMountainLightingStrength
                    * lerp(_WgMountainLegacyRelight, 1.0h, albedoMode) * step(0.0001h, normalLengthSq);
                half occlusion = lerp(1.0h, saturate(input.color.a), _WgMountainAO * albedoMode);
                half3 shaded = mountainColor * lerp(half3(1.0h, 1.0h, 1.0h), lighting, relight) * occlusion;

                half grazing = 1.0h - saturate(dot(normalWS, viewDirectionWS));
                half ridge = grazing * grazing * grazing;
                ridge *= smoothstep(0.05h, 0.7h, normalWS.y) * wrappedLight * occlusion;
                shaded += _WgMountainRidgeTint.rgb * directLight
                    * (ridge * _WgMountainRidge * _WgMountainLightingStrength);
                half3 color = lerp(shaded, _WgMountainFogColor.rgb, input.fogFactor);
                return half4(color, 1.0);
            }
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
                half4 _WgMountainTint;
                half4 _WgMountainShadowTint;
                half4 _WgMountainLightTint;
                half4 _WgMountainRidgeTint;
                half4 _WgMountainFogColor;
                half _WgMountainColorMode;
                half _WgMountainLightingStrength;
                half _WgMountainLegacyRelight;
                half _WgMountainAmbient;
                half _WgMountainDirect;
                half _WgMountainWrap;
                half _WgMountainAO;
                half _WgMountainRidge;
                float _WgMountainFogStart;
                float _WgMountainFogEnd;
                half _WgMountainFogStrength;
                float _WgMountainHazeBase;
                float _WgMountainHazeHeight;
                half _WgMountainHazeStrength;
            CBUFFER_END
            
            
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
                half4 _WgMountainTint;
                half4 _WgMountainShadowTint;
                half4 _WgMountainLightTint;
                half4 _WgMountainRidgeTint;
                half4 _WgMountainFogColor;
                half _WgMountainColorMode;
                half _WgMountainLightingStrength;
                half _WgMountainLegacyRelight;
                half _WgMountainAmbient;
                half _WgMountainDirect;
                half _WgMountainWrap;
                half _WgMountainAO;
                half _WgMountainRidge;
                float _WgMountainFogStart;
                float _WgMountainFogEnd;
                half _WgMountainFogStrength;
                float _WgMountainHazeBase;
                float _WgMountainHazeHeight;
                half _WgMountainHazeStrength;
            CBUFFER_END
            
            #define WG_AUX_MOTION 1
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

    }

    FallBack Off
}
