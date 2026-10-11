#ifndef WGAME_DEPTH_SHADOW_PASS_INCLUDED
#define WGAME_DEPTH_SHADOW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

float3 _LightDirection;
float3 _LightPosition;

#ifndef WGAME_DEPTH_SHADOW_EXTRA_ATTRIBUTE_FIELDS
    #define WGAME_DEPTH_SHADOW_EXTRA_ATTRIBUTE_FIELDS
#endif

#ifndef WGAME_DEPTH_SHADOW_EXTRA_VARYING_FIELDS
    #define WGAME_DEPTH_SHADOW_EXTRA_VARYING_FIELDS
#endif

#ifndef WGAME_DEPTH_SHADOW_TRANSFER_EXTRA
    #define WGAME_DEPTH_SHADOW_TRANSFER_EXTRA(input, output)
#endif

struct WgDepthShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    WGAME_DEPTH_SHADOW_EXTRA_ATTRIBUTE_FIELDS
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WgDepthShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : TEXCOORD1;
    WGAME_DEPTH_SHADOW_EXTRA_VARYING_FIELDS
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#ifndef WGAME_DEPTH_SHADOW_POSITION_WS
    #define WGAME_DEPTH_SHADOW_POSITION_WS(input) TransformObjectToWorld((input).positionOS.xyz)
#endif

#ifndef WGAME_DEPTH_SHADOW_CLIP
    #define WGAME_DEPTH_SHADOW_CLIP(input)
#endif

float4 WgGetShadowPositionHClip(WgDepthShadowAttributes input)
{
    float3 positionWS = WGAME_DEPTH_SHADOW_POSITION_WS(input);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

WgDepthShadowVaryings WgShadowCasterVertex(WgDepthShadowAttributes input)
{
    WgDepthShadowVaryings output = (WgDepthShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = WgGetShadowPositionHClip(input);
    output.uv = input.uv;
    output.color = input.color;
    WGAME_DEPTH_SHADOW_TRANSFER_EXTRA(input, output)
    return output;
}

half4 WgShadowCasterFragment(WgDepthShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    WGAME_DEPTH_SHADOW_CLIP(input);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    return 0;
}

WgDepthShadowVaryings WgDepthOnlyVertex(WgDepthShadowAttributes input)
{
    WgDepthShadowVaryings output = (WgDepthShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = WGAME_DEPTH_SHADOW_POSITION_WS(input);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.uv = input.uv;
    output.color = input.color;
    WGAME_DEPTH_SHADOW_TRANSFER_EXTRA(input, output)
    return output;
}

half WgDepthOnlyFragment(WgDepthShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    WGAME_DEPTH_SHADOW_CLIP(input);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    return input.positionCS.z;
}

#endif
