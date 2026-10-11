Shader "WGame/CharacterStylizedLit"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Render Face", Float) = 2
        [NoScaleOffset] _WgBakedBaseColor ("Baked Base Color", 2D) = "white" {}
        [NoScaleOffset] _WgSurfaceDataTex ("AO / Rim / Emission / Surface Profile", 2D) = "white" {}
        _WgHairLobes ("Hair Width/Strength Primary, Secondary", Vector) = (0.32, 0.45, 0.5, 0.16)
        _WgHairShape ("Hair Shift Primary/Secondary, Root, Rim", Vector) = (0.08, -0.16, 0.18, 0.12)
        [HideInInspector] _WgPaletteLightingParams ("Lighting (Wrap, Shadow Threshold, Light Threshold, Softness)", Vector) = (0.30, 0.35, 0.68, 0.055)
        [HideInInspector] _WgPaletteValueParams ("Values (Shadow, Mid, Light, Ambient)", Vector) = (0.76, 1.02, 1.18, 0.52)
        [HideInInspector] _WgPaletteShadowTint ("Shadow Tint", Color) = (0.84, 0.88, 0.96, 1)
        [HideInInspector] _WgPaletteIndirectParams ("Indirect (Hemisphere, Skin View Fill, Direct AO, Cast Shadow)", Vector) = (0.27, 0.11, 0.08, 0.50)
        [HideInInspector] _WgPaletteIndirectNormalInfluence ("Indirect Normal Influence", Range(0, 1)) = 0.15
        [HideInInspector] _WgPaletteAdditionalLightParams ("Additional Light (Strength, Softness, Shadow Floor, Attenuation Clamp)", Vector) = (0.25, 0.18, 0.08, 2.0)
        [HideInInspector] _WgPaletteSkyTint ("Sky Fill Tint", Color) = (0.55, 0.65, 0.82, 1)
        [HideInInspector] _WgPaletteGroundTint ("Ground Fill Tint", Color) = (0.38, 0.27, 0.20, 1)
        _WgPaletteEdgeWarmTint ("Edge Warm Tint", Color) = (1.00, 0.80, 0.52, 1)
        _WgPaletteEdgeCoolTint ("Edge Cool Tint", Color) = (0.38, 0.70, 0.86, 1)
        _WgPaletteEdgeParams ("Edge Light (Strength, Power, Side Softness, Base Color Mix)", Vector) = (0.085, 4.25, 0.24, 0.28)
        _WgPaletteDetailParams ("Procedural Detail (Strength, Fade Start, Fade End, Quality)", Vector) = (0.65, 3.0, 12.0, 1.0)
        _WgPaletteRimPower ("Rim Power", Range(0.25, 8.0)) = 2.6
        [Enum(Procedural, 0, Texture, 1)] _WgDissolveNoiseMode ("Dissolve Noise Source", Float) = 0
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterBakedInput.hlsl"
            #include "../ShaderLibrary/WGameShaderCommon.hlsl"
            #include "../ShaderLibrary/WGameSceneLighting.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            #include "../ShaderLibrary/WGameRimLighting.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"
            #include "../ShaderLibrary/WGamePalette.hlsl"
            #include "../ShaderLibrary/WGamePaletteDetail.hlsl"
            #include "../ShaderLibrary/WGameHairLighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

            TEXTURE2D(_WgBakedBaseColor);
            SAMPLER(sampler_WgBakedBaseColor);
            TEXTURE2D(_WgSurfaceDataTex);
            SAMPLER(sampler_WgSurfaceDataTex);
            SAMPLER(wg_point_clamp_sampler);
            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 hairFlowAndPosition : TEXCOORD1;
                float2 hairMeta : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD3;
                float4 hairFlowAndPosition : TEXCOORD4;
                float2 hairMeta : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 tangentWS : TEXCOORD6;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = float4(positionWS, input.positionOS.y);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.hairFlowAndPosition = float4(input.hairFlowAndPosition, input.positionOS.xz);
                output.hairMeta = input.hairMeta;
                return output;
            }

            float3 WgShadePaletteAdditionalLight(
                Light light,
                float3 readableSurfaceColor,
                float surfaceProfile,
                float3 normalWS,
                float3 viewDirectionWS)
            {
                float attenuation = max(0.0, light.distanceAttenuation) * saturate(light.shadowAttenuation);
                float3 diffuse = WgSceneDirect(readableSurfaceColor, normalWS, light.direction, light.color, attenuation);
                float3 halfDirection = WgSafeNormalize(light.direction + viewDirectionWS, normalWS);
                float specular = pow(saturate(dot(normalWS, halfDirection)), max(1.0, WgSurfaceProfileSpecularPower(surfaceProfile)))
                    * max(0.0, WgSurfaceProfileSpecular(surfaceProfile));
                float metallic = saturate(WgSurfaceProfileMetallic(surfaceProfile));
                float crystal = WgSurfaceFamilyMask(surfaceProfile, 7.0);
                float3 specularColor = lerp(readableSurfaceColor, float3(1.0, 0.97, 0.90), saturate(metallic * 0.8 + crystal * 0.65));
                return diffuse + specularColor * max(light.color, 0.0) * specular * attenuation;
            }

            float4 Frag(
                Varyings input,
                FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Completed effects must also hide the baked surface.
                if (_WgDissolveProgress >= 1.0)
                    clip(-1.0);
#if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
#endif
                float4 baseColorSample = SAMPLE_TEXTURE2D(_WgBakedBaseColor, sampler_WgBakedBaseColor, input.uv);
                float4 surfaceData = SAMPLE_TEXTURE2D(_WgSurfaceDataTex, sampler_WgSurfaceDataTex, input.uv);
                float2 derivativeX = ddx(input.uv) * _WgSurfaceDataTex_TexelSize.zw;
                float2 derivativeY = ddy(input.uv) * _WgSurfaceDataTex_TexelSize.zw;
                float lod = max(0.0, floor(0.5 * log2(max(1.0, max(dot(derivativeX, derivativeX), dot(derivativeY, derivativeY))))));
                surfaceData.a = SAMPLE_TEXTURE2D_LOD(_WgSurfaceDataTex, wg_point_clamp_sampler, input.uv, lod).a;
                WgPaletteVertexData surface = WgDecodePaletteVertexData(surfaceData);
                float faceDirection = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
                float3 normalWS = WgSafeNormalize(
                    input.normalWS * faceDirection,
                    float3(0.0, 1.0, 0.0));
                float3 positionWS = input.positionWS.xyz;
                float3 positionOS = float3(input.hairFlowAndPosition.z, input.positionWS.w, input.hairFlowAndPosition.w);
                float3 viewDirectionWS = WgSafeNormalize(GetCameraPositionWS() - positionWS, float3(0.0, 0.0, 1.0));
                // Character curves use actual receiver depth; extending each
                // triangle's plane into neighbouring PCF taps causes speckled self-shadowing.
                // Offset only the shadow lookup along the shading normal to avoid
                // revealing the faceted caster beneath a smoothly shaded surface.
                float3 shadowReceiverWS = positionWS;
                if (_MxDirectionalShadowEnabled > 0.5)
                    shadowReceiverWS += normalWS * max(0.0, _MxDirectionalShadowRange.z);
                WgMainLightData mainLight = WgGetMainLightData(shadowReceiverWS, float3(0.35, 0.78, 0.45), false);
                float attenuation = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float3 surfaceColor = baseColorSample.rgb;
                float3 readableSurfaceColor = surfaceColor;

                float3 additionalLightColor = 0.0;
#if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();

#if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0u;
                    lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                    ++lightIndex)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    additionalLightColor += WgShadePaletteAdditionalLight(
                        light,
                        readableSurfaceColor,
                        surface.surfaceProfile,
                        normalWS,
                        viewDirectionWS);
                }
#endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    additionalLightColor += WgShadePaletteAdditionalLight(
                        light,
                        readableSurfaceColor,
                        surface.surfaceProfile,
                        normalWS,
                        viewDirectionWS);
                LIGHT_LOOP_END
#endif

                float3 color = WgSceneDiffuse(surfaceColor, normalWS, positionWS,
                    GetNormalizedScreenSpaceUV(input.positionCS), mainLight.directionWS, mainLight.color,
                    attenuation, surface.ao);
                color += additionalLightColor;
                WgPaletteProceduralDetail proceduralDetail = WgEvaluatePaletteProceduralDetail(
                    positionOS,
                    positionWS,
                    surface.surfaceProfile,
                    surface.emission,
                    _WgPaletteDetailParams);
                color *= proceduralDetail.diffuseMultiplier;
                if (WgSurfaceFamilyMask(surface.surfaceProfile, 1.0) > 0.5 && input.hairMeta.y > 0.5)
                {
                    float3 meshTangent = WgSafeNormalize(input.tangentWS.xyz, float3(1, 0, 0));
                    float3 bitangent = cross(normalWS, meshTangent) * input.tangentWS.w * faceDirection;
                    float3 hairDirection = WgSafeNormalize(meshTangent * input.hairFlowAndPosition.x + bitangent * input.hairFlowAndPosition.y, meshTangent);
                    color *= WgHairRootFactor(input.hairMeta.x, _WgHairShape.z);
                    // The cool hair tint is a normal-dependent palette treatment, not sun visibility.
                    // Diffuse already attenuated the sun; retain the same GI in cast shadows.
                    color *= WgHairShadowTint(readableSurfaceColor, normalWS, mainLight.directionWS, 1.0, surface.ao);
                    float profileSpecularStrength = WgSurfaceProfileSpecular(surface.surfaceProfile);
                    float4 hairLobes = _WgHairLobes;
                    hairLobes.yw *= profileSpecularStrength;
                    float3 hairLight = WgShadeHair(readableSurfaceColor, normalWS, hairDirection,
                        mainLight.directionWS, viewDirectionWS, mainLight.color, attenuation,
                        surface.ao, input.hairMeta.x, hairLobes, _WgHairShape);
                    // Keep the hair's directional specular response separate from diffuse irradiance.
                    color = color * 0.85 + hairLight;
                }
                else
                color = WgApplyPaletteCharacterMaterialResponse(
                    color,
                    readableSurfaceColor,
                    surface.surfaceProfile,
                    normalWS,
                    mainLight.directionWS,
                    viewDirectionWS,
                    surface.rim,
                    proceduralDetail.specularMultiplier,
                    _WgPaletteRimPower,
                    mainLight.color,
                    attenuation);
                color = WgApplyPaletteEdgeLighting(
                    color,
                    readableSurfaceColor,
                    surface.surfaceProfile,
                    normalWS,
                    viewDirectionWS,
                    surface.rim,
                    _WgPaletteEdgeWarmTint.rgb,
                    _WgPaletteEdgeCoolTint.rgb,
                    _WgPaletteEdgeParams);

                float wetness = saturate(max(_MxWetness, _MxRain * 0.5));
                color = lerp(color, color * float3(0.72, 0.82, 0.88), wetness * 0.22);
                color = WgOverlayColor(color, _WgStatusTintColor.rgb, _WgStatusTintAmount);
                color = WgOverlayColor(color, _WgHitFlashColor.rgb, _WgHitFlashAmount);

                WgRimLightingParams outlineParams;
                outlineParams._WgRimColor = _WgOutlineColor.rgb;
                outlineParams._WgRimAmount = _WgOutlineAmount;
                outlineParams._WgRimPower = 1.45;
                outlineParams._WgRimMin = 0.04;
                outlineParams._WgRimMax = 0.82;
                color = WgApplyRimLighting(color, normalWS, viewDirectionWS, outlineParams);

                float pulse = 0.82 + sin(_MxTime.y * 3.4557519) * 0.18;
                color += baseColorSample.rgb * surface.emission * pulse * 1.35;

                UNITY_BRANCH
                if (_WgDissolveProgress > WG_EPSILON)
                {
                    float dissolveNoise;
                    // Keep the unused noise source out of the executed branch.
                    UNITY_BRANCH
                    if (_WgDissolveNoiseMode < 0.5)
                        dissolveNoise = WgDissolveProceduralNoise(input.uv);
                    else
                        dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, input.uv).r;
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

                return float4(max(color, 0.0), 1.0);
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterBakedInput.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            void WgCharacterDepthShadowClip(float2 uv)
            {
                if (_WgDissolveProgress <= WG_EPSILON)
                    return;

                float dissolveNoise;
                // Keep the unused noise source out of the executed branch.
                UNITY_BRANCH
                if (_WgDissolveNoiseMode < 0.5)
                    dissolveNoise = WgDissolveProceduralNoise(uv);
                else
                    dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, uv).r;
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterBakedInput.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            void WgCharacterDepthShadowClip(float2 uv)
            {
                if (_WgDissolveProgress <= WG_EPSILON)
                    return;

                float dissolveNoise;
                // Keep the unused noise source out of the executed branch.
                UNITY_BRANCH
                if (_WgDissolveNoiseMode < 0.5)
                    dissolveNoise = WgDissolveProceduralNoise(uv);
                else
                    dissolveNoise = SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, uv).r;
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
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull [_Cull] ZWrite On ZTest LEqual
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WgAuxVert
            #pragma fragment WgAuxFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterBakedInput.hlsl"
            #define WG_AUX_CHARACTER 1
            
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            Cull [_Cull] ZWrite On ZTest LEqual
            ColorMask RG
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WgAuxVert
            #pragma fragment WgAuxFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterBakedInput.hlsl"
            #define WG_AUX_CHARACTER 1
            #define WG_AUX_MOTION 1
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

    }

    FallBack Off
    CustomEditor "MxFramework.Project.Editor.WGameCharacterStylizedLitShaderGui"
}
