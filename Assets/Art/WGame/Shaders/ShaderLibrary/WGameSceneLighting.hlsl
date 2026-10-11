#ifndef WGAME_SCENE_LIGHTING_INCLUDED
#define WGAME_SCENE_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "WGameFrameGlobals.hlsl"
#include "Packages/com.vincent.wgameframework/Rendering/MxDirectionalShadow.hlsl"
TEXTURE2D_X(_MxSceneIrradiance);
float4 _MxSceneIrradiance_TexelSize;
float _MxSceneLightingEnabled;
float4 _MxSceneLightingLook;
float4 _MxSceneShadowTint;
// xy: material specular and light-facing rim strengths; valid only for an enabled scene provider.
float4 _MxSceneSurfaceHighlights;
float4 _MxSceneLightingBoundsMin,_MxSceneLightingBoundsMax;
float WgSceneLightingActive(float3 positionWS)
{
    return _MxSceneLightingEnabled * (all(positionWS >= _MxSceneLightingBoundsMin.xyz) && all(positionWS <= _MxSceneLightingBoundsMax.xyz) ? 1.0 : 0.0);
}

float3 WgSceneIndirect(float3 positionWS,float3 normalWS,float2 uv)
{
    float expectedDepth=-TransformWorldToView(positionWS).z;
    float2 pixel=uv*_MxSceneIrradiance_TexelSize.zw-0.5;
    float2 basePixel=floor(pixel),fraction=frac(pixel);
    float3 sum=0;float total=0;
    [unroll] for(int y=0;y<2;y++)[unroll]for(int x=0;x<2;x++)
    {
        float2 sampleUv=(basePixel+float2(x,y)+0.5)*_MxSceneIrradiance_TexelSize.xy;
        float4 value=SAMPLE_TEXTURE2D_X_LOD(_MxSceneIrradiance,sampler_PointClamp,sampleUv,0);
        float3 sampleNormal=SampleSceneNormals(sampleUv);
        float weight=(x?fraction.x:1-fraction.x)*(y?fraction.y:1-fraction.y);
        weight*=exp2(-abs(expectedDepth-value.a)*8)*pow(saturate(dot(sampleNormal,normalWS)),8);
        weight*=step(0.0001,value.a);
        sum+=value.rgb*weight;total+=weight;
    }
    return total>0.00000001?sum/total:0;
}
float WgSceneDiffuseResponse(float3 normalWS,float3 lightDirection)
{
    // The same material response also renders without a world cache. Never read stale
    // camera Look globals when the scene provider has not enabled them for this camera.
    float bandStrength=_MxSceneLightingEnabled>0.5?saturate(_MxSceneLightingLook.w):0.25;
    float ndl=saturate(dot(normalWS,lightDirection));
    float band=lerp(0.18,0.62,smoothstep(0.22,0.38,ndl));
    band=lerp(band,1,smoothstep(0.62,0.78,ndl));
    return lerp(ndl,band*step(0.001,ndl),bandStrength);
}
float3 WgSceneDirect(float3 albedo,float3 normalWS,float3 lightDirection,float3 lightColor,float attenuation)
{
    float strength=_MxSceneLightingEnabled>0.5?max(0,_MxSceneLightingLook.x):1;
    return max(0,albedo*lightColor*(WgSceneDiffuseResponse(normalWS,lightDirection)*attenuation*strength));
}
float3 WgSceneDiffuse(float3 albedo,float3 normalWS,float3 positionWS,float2 uv,
    float3 lightDirection,float3 lightColor,float attenuation,float microAo)
{
    float ndl=WgSceneDiffuseResponse(normalWS,lightDirection);
    float strength=_MxSceneLightingEnabled>0.5?max(0,_MxSceneLightingLook.x):1;
    float3 color=albedo*lightColor*(ndl*attenuation*strength);
    if(WgSceneLightingActive(positionWS)>0.5 && _MxSceneLightingLook.y>0)
    {
        float3 indirect=WgSceneIndirect(positionWS,normalWS,uv)*_MxSceneLightingLook.y;
        indirect*=lerp(1,microAo,saturate(_MxSceneLightingLook.z));
        // Fixed-direction visibility belongs to direct light. Preserve the authored
        // normal-dependent indirect tint, without feeding new occlusion into GI.
        float indirectResponse=_MxDirectionalShadowEnabled>0.5?ndl:ndl*attenuation;
        indirect*=lerp(_MxSceneShadowTint.rgb,1,saturate(indirectResponse));
        color+=albedo*indirect;
    }
    return max(0,color);
}
#endif
