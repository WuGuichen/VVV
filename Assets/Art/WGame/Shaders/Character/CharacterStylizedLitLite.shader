Shader "WGame/CharacterStylizedLitLite"
{
    Properties
    {
        [NoScaleOffset] _WgLiteMatCapTex ("Shared Material MatCap", 2D) = "black" {}
        _WgLiteMatCapStrength ("MatCap Strength", Range(0, 2)) = 1
        _WgLiteMatCapShadow ("MatCap in Shadow", Range(0, 1)) = 0.35
        [MainColor] _Color ("Compatibility Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Render Face", Float) = 2
        [Toggle(_WG_PALETTE_ON)] _WgPaletteEnabled ("Palette Enabled", Float) = 0
        [NoScaleOffset] _WgColorPaletteTex ("Shared RGB Color Palette", 2D) = "white" {}
        [HideInInspector] _WgColorPaletteParams ("Color Palette Params", Vector) = (256, 256, 0.00390625, 0.00390625)
        [HideInInspector] _WgPaletteLightingParams ("Palette Lighting (Wrap, Shadow Threshold, Light Threshold, Softness)", Vector) = (0.30, 0.35, 0.68, 0.055)
        [HideInInspector] _WgPaletteValueParams ("Palette Values (Shadow, Mid, Light, Ambient)", Vector) = (0.76, 1.02, 1.18, 0.52)
        [HideInInspector] _WgPaletteShadowTint ("Palette Shadow Tint", Color) = (0.84, 0.88, 0.96, 1)
        [HideInInspector] _WgPaletteIndirectParams ("Indirect (Hemisphere, Reserved, Direct AO, Cast Shadow)", Vector) = (0.27, 0.11, 0.08, 0.50)
        [HideInInspector] _WgPaletteIndirectNormalInfluence ("Palette Indirect Normal Influence", Range(0, 1)) = 0.15
        [HideInInspector] _WgPaletteAdditionalLightParams ("Palette Additional Light (Strength, Softness, Shadow Floor, Attenuation Clamp)", Vector) = (0.25, 0.18, 0.08, 2.0)
        [HideInInspector] _WgPaletteSkyTint ("Palette Sky Fill Tint", Color) = (0.55, 0.65, 0.82, 1)
        [HideInInspector] _WgPaletteGroundTint ("Palette Ground Fill Tint", Color) = (0.38, 0.27, 0.20, 1)
        _WgPaletteEdgeWarmTint ("Palette Edge Warm Tint", Color) = (1.00, 0.80, 0.52, 1)
        _WgPaletteEdgeCoolTint ("Palette Edge Cool Tint", Color) = (0.38, 0.70, 0.86, 1)
        _WgPaletteEdgeParams ("Edge (Strength, Reserved, Side Softness, Reserved)", Vector) = (0.085, 4.25, 0.24, 0.28)
        [HideInInspector] _WgPaletteDetailParams ("Palette Procedural Detail (Strength, Fade Start, Fade End, Quality)", Vector) = (0.65, 3.0, 12.0, 1.0)
        [HideInInspector] _WgPaletteRimPower ("Palette Rim Power", Range(0.25, 8.0)) = 2.6
        [Toggle(_WG_FACE_DETAIL_ON)] _WgFaceDetailEnabled ("Face Detail Enabled", Float) = 0
        [NoScaleOffset] _WgFaceDetailTex ("Character Face Detail RGBA", 2D) = "black" {}
        _WgFaceDetailIntensity ("Face Detail Intensity", Range(0, 1)) = 1
        [Toggle(_WG_LITE_DISSOLVE_ON)] _WgLiteDissolveEnabled ("Enable Dissolve", Float) = 0
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
            #pragma shader_feature_local _WG_PALETTE_ON
            #pragma shader_feature_local _WG_FACE_DETAIL_ON
            #pragma shader_feature_local_fragment _WG_LITE_DISSOLVE_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameSceneLighting.hlsl"
            #include "Packages/com.vincent.wgameframework/Rendering/MxDirectionalShadow.hlsl"
            #include "../ShaderLibrary/WGameCharacterLiteInput.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            #include "../ShaderLibrary/WGamePalette.hlsl"
            #include "../ShaderLibrary/WGameFaceDetail.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"
#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

            TEXTURE2D(_WgColorPaletteTex);
            SAMPLER(sampler_WgColorPaletteTex);
            TEXTURE2D(_WgFaceDetailTex);
            SAMPLER(sampler_WgFaceDetailTex);
            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            TEXTURE2D(_WgLiteMatCapTex);
            SAMPLER(sampler_WgLiteMatCapTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 paletteUv : TEXCOORD1;
                float2 faceDetailUv : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 uv : TEXCOORD2;
                half4 baseEmission : TEXCOORD3;
                half ao : TEXCOORD4;
                half2 specularResponse : TEXCOORD5;
                half2 response : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv = float4(input.uv, input.faceDetailUv);

                half3 baseColor = _Color.rgb;
                half ao = 1.0;
                half specular = 0.02;
                half powerBlend = 8.0;
                half reflection = 0.0;
                half rim = 0.0;
                half emission = 0.0;

#if defined(_WG_PALETTE_ON)
                // Palette addresses and profile IDs are authored per face. Decode once
                // per vertex instead of repeating the profile table in every pixel.
                float profile = WgDecodeSurfaceProfile(input.color.a);
                baseColor = SAMPLE_TEXTURE2D_LOD(
                    _WgColorPaletteTex, sampler_WgColorPaletteTex, input.paletteUv, 0).rgb;
                if (_WgPaletteEnabled < 0.5 || WgColorPaletteUvIsValid(input.paletteUv) < 0.5)
                    baseColor = half3(1.0, 0.0, 1.0);
                ao = saturate(input.color.r);
                half metal = WgSurfaceFamilyMask(profile, 6.0);
                half crystal = WgSurfaceFamilyMask(profile, 7.0);
                specular = WgSurfaceProfileSpecular(profile) * (1.0 + metal * 2.0 + crystal * 0.7);
                powerBlend = max(1.0, WgSurfaceProfileSpecularPower(profile)) / 3.0;
                reflection = metal * 0.72 + crystal * 0.48;
                rim = WgSurfaceProfileRim(profile) * saturate(input.color.g) * 0.35;
                emission = saturate(input.color.b) * (0.82 + sin(_MxTime.y * 3.4557519) * 0.18) * 1.35;
#endif
                output.baseEmission = half4(baseColor, emission);
                output.specularResponse = half2(specular, powerBlend);
                output.response = half2(reflection, rim);

                output.ao = ao;
                output.positionWS = positionWS;
                return output;
            }

            half3 ShadeAdditional(Light light, half3 normalWS, half3 baseColor)
            {
                float3 diffuse = WgSceneDirect(baseColor, normalWS, light.direction, light.color,
                    light.distanceAttenuation * light.shadowAttenuation);
                return diffuse;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
#endif
#if defined(_WG_LITE_DISSOLVE_ON)
                WgDissolveResult dissolve = (WgDissolveResult)0;
                UNITY_BRANCH
                if (_WgDissolveProgress > WG_EPSILON)
                {
                    if (_WgDissolveProgress >= 1.0) clip(-1.0);
                    float noise = _WgDissolveNoiseMode < 0.5
                        ? WgDissolveProceduralNoise(input.uv.xy)
                        : SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, input.uv.xy).r;
                    WgDissolveParams parameters;
                    parameters._WgDissolveProgress = _WgDissolveProgress;
                    parameters._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                    parameters._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                    parameters._WgLifetimeFade = 0;
                    parameters._WgDissolveNoiseStrength = 1;
                    dissolve = WgEvaluateDissolve(noise, parameters);
                    clip(dissolve.clipValue);
                }
#endif

                half3 baseColor = input.baseEmission.rgb;
#if defined(_WG_PALETTE_ON) && defined(_WG_FACE_DETAIL_ON)
                UNITY_BRANCH
                if (_WgFaceDetailEnabled > 0.5 && WgFaceDetailValidity(input.uv.zw) > 0.5)
                {
                    half4 face = SAMPLE_TEXTURE2D(_WgFaceDetailTex, sampler_WgFaceDetailTex, input.uv.zw);
                    baseColor = lerp(baseColor, face.rgb, saturate(face.a * _WgFaceDetailIntensity));
                }
#endif

                half faceSign = IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                half3 normalWS = normalize(input.normalWS) * faceSign;
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight;
                UNITY_BRANCH
                if (_MxDirectionalShadowEnabled > 0.5)
                { mainLight = GetMainLight(); mainLight.shadowAttenuation = MxDirectionalSunVisibility(input.positionWS); }
                else mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half attenuation = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 color = WgSceneDiffuse(baseColor, normalWS, input.positionWS,
                    GetNormalizedScreenSpaceUV(input.positionCS), mainLight.direction, mainLight.color,
                    attenuation, input.ao);
#if defined(_ADDITIONAL_LIGHTS)
                half3 additional = 0.0;
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0u;
                        lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        additional += ShadeAdditional(GetAdditionalLight(lightIndex, input.positionWS),
                            normalWS, baseColor);
                    }
#endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        additional += ShadeAdditional(GetAdditionalLight(lightIndex, input.positionWS),
                            normalWS, baseColor);
                    LIGHT_LOOP_END
                color += additional;
#endif

                // A single per-pixel lobe is shared by all surface profiles.
                half3 halfWS = SafeNormalize(mainLight.direction + viewWS);
                half nh = saturate(dot(normalWS, halfWS));
                half glint = saturate(nh * input.specularResponse.y + 1.0 - input.specularResponse.y);
                glint = glint * glint * (3.0 - 2.0 * glint) * input.specularResponse.x * attenuation;
                color += lerp(mainLight.color, baseColor * mainLight.color, input.response.x * 0.7) * glint;

                half3 normalVS = TransformWorldToViewDir(normalWS, false);
                UNITY_BRANCH
                if (input.response.x > 0.001 && _WgLiteMatCapStrength > 0.0)
                {
                    // Neutral shared studio response. Authored palette hue stays independent.
                    half matcap = SAMPLE_TEXTURE2D(_WgLiteMatCapTex, sampler_WgLiteMatCapTex,
                        normalVS.xy * 0.5 + 0.5).r;
                    color += baseColor * matcap * input.response.x * _WgLiteMatCapStrength
                        * lerp(_WgLiteMatCapShadow, 1.0, attenuation);
                }
                half fresnel = 1.0 - saturate(dot(normalWS, viewWS));
                half fresnel2 = fresnel * fresnel;
                half fresnel4 = fresnel2 * fresnel2;
                half side = smoothstep(-max(0.01, _WgPaletteEdgeParams.z), max(0.01, _WgPaletteEdgeParams.z), normalVS.x);
                half3 edgeTint = lerp(_WgPaletteEdgeWarmTint.rgb, _WgPaletteEdgeCoolTint.rgb, side);
                color += fresnel4 * (baseColor * input.response.y + edgeTint * max(0.0, _WgPaletteEdgeParams.x)
                    + _WgOutlineColor.rgb * saturate(_WgOutlineAmount));
                half wetness = saturate(max(_MxWetness, _MxRain * 0.5));
                color *= lerp(half3(1,1,1), half3(0.72,0.82,0.88), wetness * 0.22);
                color = lerp(color, _WgStatusTintColor.rgb, saturate(_WgStatusTintAmount));
                color = lerp(color, _WgHitFlashColor.rgb, saturate(_WgHitFlashAmount));
                color += baseColor * input.baseEmission.a;
#if defined(_WG_LITE_DISSOLVE_ON)
                if (_WgDissolveProgress > WG_EPSILON)
                    color = WgApplyDissolveEdge(color, dissolve);
#endif
                return half4(max(color, 0), 1);
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
            #pragma shader_feature_local_fragment _WG_LITE_DISSOLVE_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterLiteInput.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            void WgCharacterDepthShadowClip(float2 uv)
            {
#if defined(_WG_LITE_DISSOLVE_ON)
                if (_WgDissolveProgress <= WG_EPSILON)
                    return;

                float dissolveNoise = _WgDissolveNoiseMode < 0.5
                    ? WgDissolveProceduralNoise(uv)
                    : SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, uv).r;
                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                clip(WgEvaluateDissolve(dissolveNoise, dissolveParams).clipValue);
#endif
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
            #pragma shader_feature_local_fragment _WG_LITE_DISSOLVE_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/WGameCharacterLiteInput.hlsl"
            #include "../ShaderLibrary/WGameDissolve.hlsl"

            TEXTURE2D(_WgDissolveNoiseTex);
            SAMPLER(sampler_WgDissolveNoiseTex);

            void WgCharacterDepthShadowClip(float2 uv)
            {
#if defined(_WG_LITE_DISSOLVE_ON)
                if (_WgDissolveProgress <= WG_EPSILON)
                    return;

                float dissolveNoise = _WgDissolveNoiseMode < 0.5
                    ? WgDissolveProceduralNoise(uv)
                    : SAMPLE_TEXTURE2D(_WgDissolveNoiseTex, sampler_WgDissolveNoiseTex, uv).r;
                WgDissolveParams dissolveParams;
                dissolveParams._WgDissolveProgress = _WgDissolveProgress;
                dissolveParams._WgDissolveEdgeWidth = _WgDissolveEdgeWidth;
                dissolveParams._WgDissolveEdgeColor = _WgDissolveEdgeColor.rgb;
                dissolveParams._WgLifetimeFade = 0.0;
                dissolveParams._WgDissolveNoiseStrength = 1.0;
                clip(WgEvaluateDissolve(dissolveNoise, dissolveParams).clipValue);
#endif
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
            #include "../ShaderLibrary/WGameCharacterLiteInput.hlsl"
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
            #include "../ShaderLibrary/WGameCharacterLiteInput.hlsl"
            #define WG_AUX_CHARACTER 1
            #define WG_AUX_MOTION 1
            #include "../ShaderLibrary/WGameLightingAuxiliaryPass.hlsl"
            ENDHLSL
        }

    }

    FallBack Off
}
