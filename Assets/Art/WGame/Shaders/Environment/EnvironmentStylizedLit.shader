Shader "WGame/EnvironmentStylizedLit"
{
    Properties
    {
        _WgStoneColor ("Stone Color", Color) = (0.075, 0.165, 0.220, 1)
        _WgWeatheredStoneColor ("Weathered Stone Color", Color) = (0.376, 0.380, 0.353, 1)
        _WgMossColor ("Moss Color", Color) = (0.208, 0.263, 0.192, 1)
        _WgBronzeTrimColor ("Bronze Trim Color", Color) = (0.608, 0.463, 0.314, 1)
        [HideInInspector] _WgRampTex ("Ramp Texture", 2D) = "white" {}
        _WgMaskTex ("Mask Texture", 2D) = "black" {}
        [Toggle(_WG_BAKED_SURFACE_ON)] _WgBakedSurface ("Shared Baked Surface", Float) = 0
        [NoScaleOffset] _WgBakedBaseColor ("Baked Base Color", 2D) = "white" {}
        [NoScaleOffset] _WgSurfaceDataTex ("AO / Rim / Emission / Surface Profile", 2D) = "white" {}
        _WgEmissionIntensity ("Emission Intensity (HDR)", Range(1, 64)) = 1
        [Toggle(_WG_WORLD_TILE_ON)] _WgWorldTileEnabled ("World Space Concrete Joints", Float) = 0
        _WgWorldTileSize ("Concrete Tile Size X/Z", Vector) = (4, 3.9, 0, 0)
        _WgWorldJointWidth ("Concrete Joint Width", Range(0.01, 0.2)) = 0.04
        _WgWorldJointColor ("Concrete Joint Color", Color) = (0.28, 0.3, 0.32, 1)
        [HideInInspector] _WgFacetStrength ("Facet Strength", Range(0, 1)) = 0.55
        _WgMossCoverage ("Moss Coverage", Range(0, 1)) = 0.15
        [HideInInspector] _WgFogBlendStrength ("Fog Blend Strength", Range(0, 1)) = 0.65
        _WgWeatheringCoverage ("Weathering Coverage", Range(0, 1)) = 0.35
        _WgBronzeTrimCoverage ("Bronze Trim Coverage", Range(0, 1)) = 0
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
            #pragma shader_feature_local_fragment _WG_BAKED_SURFACE_ON
            #pragma shader_feature_local_fragment _WG_WORLD_TILE_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameSceneLighting.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            #include "../ShaderLibrary/WGamePalette.hlsl"

            TEXTURE2D(_WgMaskTex);
            SAMPLER(sampler_WgMaskTex);
            TEXTURE2D(_WgBakedBaseColor);
            SAMPLER(sampler_WgBakedBaseColor);
            TEXTURE2D(_WgSurfaceDataTex);
            SAMPLER(sampler_WgSurfaceDataTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WgStoneColor;
                float4 _WgWeatheredStoneColor;
                float4 _WgMossColor;
                float4 _WgBronzeTrimColor;
                float _WgFacetStrength;
                float _WgMossCoverage;
                float _WgFogBlendStrength;
                float _WgWeatheringCoverage;
                float _WgBronzeTrimCoverage;
                float _WgBakedSurface;
                float _WgEmissionIntensity;
                float _WgWorldTileEnabled;
                float4 _WgWorldTileSize;
                float _WgWorldJointWidth;
                float4 _WgWorldJointColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float WgAuthoredWeathering(float maskWeathering)
            {
                return saturate(max(maskWeathering, _WgWeatheringCoverage));
            }

            float WgAuthoredMoss(float maskMoss, float normalUp)
            {
                float topBias = smoothstep(0.2, 0.85, saturate(normalUp));
                return saturate(max(maskMoss, _WgMossCoverage * topBias));
            }

            float WgAuthoredBronze(float maskBronze)
            {
                return saturate(max(maskBronze, _WgBronzeTrimCoverage));
            }

            float3 WgEnvironmentSurfaceColor(WgPackedMaterialMask materialMask, float normalUp)
            {
                float weathering = WgAuthoredWeathering(materialMask.primary);
                float moss = WgAuthoredMoss(materialMask.secondary, normalUp);
                float bronze = WgAuthoredBronze(materialMask.trim);

                float3 stone = lerp(_WgStoneColor.rgb, _WgWeatheredStoneColor.rgb, weathering);
                float3 mossStone = lerp(stone, _WgMossColor.rgb, moss);
                return lerp(mossStone, _WgBronzeTrimColor.rgb, bronze);
            }

#if defined(_WG_BAKED_SURFACE_ON)
            float3 WgEnvironmentSurfaceHighlights(float3 surfaceColor, WgPaletteVertexData surface,
                float3 normalWS, float3 viewDirectionWS, WgMainLightData mainLight, float attenuation)
            {
                // Reuse the decoded surface profile and the main light's existing shadow sample.
                // Unbaked terrain never enters this path. No material LUT or extra texture read.
                float normalLight = saturate(dot(normalWS, mainLight.directionWS));
                float normalView = saturate(dot(normalWS, viewDirectionWS));
                float3 halfDirectionWS = WgSafeNormalize(mainLight.directionWS + viewDirectionWS, normalWS);
                float specularPower = clamp(WgSurfaceProfileSpecularPower(surface.surfaceProfile), 8.0, 32.0);
                float specular = pow(saturate(dot(normalWS, halfDirectionWS)), specularPower);
                float edge = 1.0 - normalView;
                edge = edge * edge * edge;
                float litEdge = edge * smoothstep(0.0, 0.35, normalLight);
                float materialSpecular = WgSurfaceProfileSpecular(surface.surfaceProfile);
                // The baked rim channel can enhance the intrinsic grazing response; it never bypasses visibility.
                float response = specular * materialSpecular * _MxSceneSurfaceHighlights.x
                    + litEdge * max(materialSpecular, surface.rim) * _MxSceneSurfaceHighlights.y;
                // Dielectric highlights reflect the light color; metal also carries the authored surface hue.
                float3 reflectance = lerp(float3(1.0, 1.0, 1.0), surfaceColor,
                    saturate(WgSurfaceProfileMetallic(surface.surfaceProfile)));
                float visibility = normalLight * attenuation * surface.ao * max(0.0, _MxSceneLightingLook.x);
                return reflectance * mainLight.color * (response * visibility);
            }
#endif

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = WgSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                float normalUp = dot(normalWS, float3(0.0, 1.0, 0.0)) * 0.5 + 0.5;
                float3 viewDirectionWS = WgSafeNormalize(GetCameraPositionWS() - input.positionWS, float3(0.0, 0.0, 1.0));
                WgMainLightData mainLight = WgGetMainLightData(input.positionWS, float3(0.28, 0.82, 0.42));
#if defined(_WG_BAKED_SURFACE_ON)
                WgPaletteVertexData baked = WgDecodePaletteVertexData(
                    SAMPLE_TEXTURE2D(_WgSurfaceDataTex, sampler_WgSurfaceDataTex, input.uv));
                float3 surfaceColor = SAMPLE_TEXTURE2D(_WgBakedBaseColor, sampler_WgBakedBaseColor, input.uv).rgb;
#else
                float4 maskSample = SAMPLE_TEXTURE2D(_WgMaskTex, sampler_WgMaskTex, input.uv);
                WgPackedMaterialMask materialMask = WgDecodePackedMaterialMask(maskSample);
                float3 surfaceColor = WgEnvironmentSurfaceColor(materialMask, normalUp);
#endif
#if defined(_WG_WORLD_TILE_ON)
                float2 tileSize = max(_WgWorldTileSize.xy, float2(0.1, 0.1));
                float2 tileCoordinate = input.positionWS.xz / tileSize;
                float2 edgeDistance = min(frac(tileCoordinate), 1.0 - frac(tileCoordinate)) * tileSize;
                float nearestJoint = min(edgeDistance.x, edgeDistance.y);
                float antialiasWidth = max(0.01, fwidth(nearestJoint));
                float joint = 1.0 - smoothstep(_WgWorldJointWidth,
                    _WgWorldJointWidth + antialiasWidth, nearestJoint);
                float variation = frac(sin(dot(floor(tileCoordinate), float2(12.9898, 78.233))) * 43758.5453);
                surfaceColor *= lerp(0.94, 1.03, variation);
                surfaceColor = lerp(surfaceColor, _WgWorldJointColor.rgb, joint * 0.75);
#endif

                float microAo = 1.0;
#if defined(_WG_BAKED_SURFACE_ON)
                microAo = baked.ao;
#endif
                float lightAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float3 color = WgSceneDiffuse(surfaceColor, normalWS, input.positionWS,
                    GetNormalizedScreenSpaceUV(input.positionCS), mainLight.directionWS, mainLight.color,
                    lightAttenuation, microAo);
                float wetness = saturate(max(_MxWetness, _MxRain * 0.7));
                color = lerp(color, color * float3(0.65, 0.78, 0.82), wetness * 0.25);
                color = lerp(color, lerp(color, WgColorParchment200, 0.18), saturate(_MxSnowCoverage) * 0.18);
#if defined(_WG_BAKED_SURFACE_ON)
                UNITY_BRANCH
                if (_MxSceneLightingEnabled > 0.5 &&
                    _MxSceneSurfaceHighlights.x + _MxSceneSurfaceHighlights.y > 0.0)
                {
                    color += WgEnvironmentSurfaceHighlights(surfaceColor, baked, normalWS,
                        viewDirectionWS, mainLight, lightAttenuation);
                }
                else
                {
                    // Preserve existing materials on cameras/scenes that have not opted into the new response.
                    float rim = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), 3.0) * baked.rim;
                    color += _WgBronzeTrimColor.rgb * rim * 0.2;
                }
                color += surfaceColor * baked.emission * _WgEmissionIntensity;
#endif

                return float4(max(color, 0.0), _WgStoneColor.a);
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

            CBUFFER_START(UnityPerMaterial)
                float4 _WgStoneColor;
                float4 _WgWeatheredStoneColor;
                float4 _WgMossColor;
                float4 _WgBronzeTrimColor;
                float _WgFacetStrength;
                float _WgMossCoverage;
                float _WgFogBlendStrength;
                float _WgWeatheringCoverage;
                float _WgBronzeTrimCoverage;
                float _WgBakedSurface;
                float _WgEmissionIntensity;
                float _WgWorldTileEnabled;
                float4 _WgWorldTileSize;
                float _WgWorldJointWidth;
                float4 _WgWorldJointColor;
            CBUFFER_END

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

            CBUFFER_START(UnityPerMaterial)
                float4 _WgStoneColor;
                float4 _WgWeatheredStoneColor;
                float4 _WgMossColor;
                float4 _WgBronzeTrimColor;
                float _WgFacetStrength;
                float _WgMossCoverage;
                float _WgFogBlendStrength;
                float _WgWeatheringCoverage;
                float _WgBronzeTrimCoverage;
                float _WgBakedSurface;
                float _WgEmissionIntensity;
                float _WgWorldTileEnabled;
                float4 _WgWorldTileSize;
                float _WgWorldJointWidth;
                float4 _WgWorldJointColor;
            CBUFFER_END

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
                float4 _WgStoneColor;
                float4 _WgWeatheredStoneColor;
                float4 _WgMossColor;
                float4 _WgBronzeTrimColor;
                float _WgFacetStrength;
                float _WgMossCoverage;
                float _WgFogBlendStrength;
                float _WgWeatheringCoverage;
                float _WgBronzeTrimCoverage;
                float _WgBakedSurface;
                float _WgEmissionIntensity;
                float _WgWorldTileEnabled;
                float4 _WgWorldTileSize;
                float _WgWorldJointWidth;
                float4 _WgWorldJointColor;
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
                float4 _WgStoneColor;
                float4 _WgWeatheredStoneColor;
                float4 _WgMossColor;
                float4 _WgBronzeTrimColor;
                float _WgFacetStrength;
                float _WgMossCoverage;
                float _WgFogBlendStrength;
                float _WgWeatheringCoverage;
                float _WgBronzeTrimCoverage;
                float _WgBakedSurface;
                float _WgEmissionIntensity;
                float _WgWorldTileEnabled;
                float4 _WgWorldTileSize;
                float _WgWorldJointWidth;
                float4 _WgWorldJointColor;
            CBUFFER_END
            
            #define WG_AUX_MOTION 1
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

    }

    FallBack Off
}
