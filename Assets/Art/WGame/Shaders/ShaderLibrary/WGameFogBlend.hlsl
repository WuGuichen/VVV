#ifndef WGAME_FOG_BLEND_INCLUDED
#define WGAME_FOG_BLEND_INCLUDED

#include "WGameShaderCommon.hlsl"

#include "WGameFrameGlobals.hlsl"

struct WgFogBlendParams
{
    float3 _WgFogColor;
    float _WgFogDensity;
    float _WgFogHeightFalloff;
    float _WgFogStartDistance;
    float _WgFogEndDistance;
    float _WgFogBlendStrength;
    float _WgFogFocusClearRadius;
    float _WgFogFocusClearSoftness;
};

float WgDistanceFogFactor(float3 positionWS, float3 cameraPositionWS, WgFogBlendParams paramsData)
{
    float distanceToCamera = distance(positionWS, cameraPositionWS);
    float distanceFactor = WgDistanceFade(distanceToCamera, paramsData._WgFogStartDistance, paramsData._WgFogEndDistance);
    float density = saturate(paramsData._WgFogDensity);
    return saturate(1.0 - exp2(-distanceFactor * density * 8.0));
}

float WgHeightFogFactor(float3 positionWS, float referenceHeight, WgFogBlendParams paramsData)
{
    float heightDelta = max(positionWS.y - referenceHeight, 0.0);
    float falloff = max(paramsData._WgFogHeightFalloff, WG_EPSILON);
    return saturate(exp2(-heightDelta * falloff));
}

float WgViewFocusClearFactor(float3 positionWS, WgFogBlendParams paramsData)
{
    float radius = max(paramsData._WgFogFocusClearRadius, 0.0);
    float softness = max(paramsData._WgFogFocusClearSoftness, WG_EPSILON);
    float focusDistance = distance(positionWS, _MxViewFocusWorldPos.xyz);
    return smoothstep(radius, radius + softness, focusDistance);
}

float WgNightRuinFogFactor(float3 positionWS, float3 cameraPositionWS, float referenceHeight, WgFogBlendParams paramsData)
{
    if (_MxPipelineHeightFogEnabled > 0.5) return 0.0;
    float distanceFactor = WgDistanceFogFactor(positionWS, cameraPositionWS, paramsData);
    float heightFactor = WgHeightFogFactor(positionWS, referenceHeight, paramsData);
    float focusFactor = WgViewFocusClearFactor(positionWS, paramsData);
    return saturate(distanceFactor * heightFactor * focusFactor * paramsData._WgFogBlendStrength);
}

float3 WgApplyNightRuinFog(float3 color, float3 positionWS, float3 cameraPositionWS, float referenceHeight, WgFogBlendParams paramsData)
{
    float fogFactor = WgNightRuinFogFactor(positionWS, cameraPositionWS, referenceHeight, paramsData);
    return lerp(color, paramsData._WgFogColor, fogFactor);
}

float3 WgDefaultNightRuinFogColor()
{
    return lerp(WgColorNavy900, WgColorMoss700, 0.22);
}

#endif
