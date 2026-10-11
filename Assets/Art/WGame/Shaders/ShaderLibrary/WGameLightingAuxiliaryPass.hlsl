#ifndef WGAME_LIGHTING_AUXILIARY_PASS_INCLUDED
#define WGAME_LIGHTING_AUXILIARY_PASS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#if defined(LOD_FADE_CROSSFADE)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#if defined(WG_AUX_CHARACTER)
#include "WGameDissolve.hlsl"
TEXTURE2D(_WgDissolveNoiseTex);SAMPLER(sampler_WgDissolveNoiseTex);
#endif
struct WgAuxInput
{
    float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float3 previousOS:TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct WgAuxOutput
{
    float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;
    float4 currentCS:TEXCOORD2;float4 previousCS:TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
WgAuxOutput WgAuxVert(WgAuxInput input)
{
    WgAuxOutput o=(WgAuxOutput)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.uv=input.uv;
    o.currentCS=mul(_NonJitteredViewProjMatrix,mul(UNITY_MATRIX_M,input.positionOS));
    float4 previous=unity_MotionVectorsParams.x==1?float4(input.previousOS,1):input.positionOS;
    o.previousCS=mul(_PrevViewProjMatrix,mul(UNITY_PREV_MATRIX_M,previous));return o;
}
float4 WgAuxFrag(WgAuxOutput input,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    #if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
    #endif
    #if defined(WG_AUX_CHARACTER)
    if(_WgDissolveProgress>=1)clip(-1);
    if(_WgDissolveProgress>0.00001)
    {
        #if defined(WG_AUX_TEXTURE_DISSOLVE)
        float noise=SAMPLE_TEXTURE2D(_WgDissolveNoiseTex,sampler_WgDissolveNoiseTex,input.uv).r;
        #else
        float noise=_WgDissolveNoiseMode<0.5?WgDissolveProceduralNoise(input.uv):SAMPLE_TEXTURE2D(_WgDissolveNoiseTex,sampler_WgDissolveNoiseTex,input.uv).r;
        #endif
        WgDissolveParams p;p._WgDissolveProgress=_WgDissolveProgress;p._WgDissolveEdgeWidth=_WgDissolveEdgeWidth;p._WgDissolveEdgeColor=_WgDissolveEdgeColor.rgb;p._WgLifetimeFade=0;p._WgDissolveNoiseStrength=1;
        clip(WgEvaluateDissolve(noise,p).clipValue);
    }
    #endif
    #if defined(WG_AUX_MOTION)
    return float4(CalcNdcMotionVectorFromCsPositions(input.currentCS,input.previousCS),0,0);
    #else
    float3 n=normalize(input.normalWS)*IS_FRONT_VFACE(front,1,-1);
    #if defined(_GBUFFER_NORMALS_OCT)
    return float4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*0.5+0.5)),0);
    #else
    return float4(n,0);
    #endif
    #endif
}
#endif
