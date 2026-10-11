#ifndef WGAME_RIM_LIGHTING_INCLUDED
#define WGAME_RIM_LIGHTING_INCLUDED

#include "WGameShaderCommon.hlsl"

struct WgRimLightingParams
{
    float3 _WgRimColor;
    float _WgRimAmount;
    float _WgRimPower;
    float _WgRimMin;
    float _WgRimMax;
};

float WgRimFactor(float3 normalWS, float3 viewDirectionWS, WgRimLightingParams paramsData)
{
    float3 safeNormal = WgSafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
    float3 safeView = WgSafeNormalize(viewDirectionWS, float3(0.0, 0.0, 1.0));
    float fresnel = 1.0 - saturate(dot(safeNormal, safeView));
    float shaped = pow(saturate(fresnel), max(paramsData._WgRimPower, WG_EPSILON));
    return smoothstep(paramsData._WgRimMin, paramsData._WgRimMax, shaped) * saturate(paramsData._WgRimAmount);
}

float3 WgApplyRimLighting(float3 color, float3 normalWS, float3 viewDirectionWS, WgRimLightingParams paramsData)
{
    float rim = WgRimFactor(normalWS, viewDirectionWS, paramsData);
    return color + paramsData._WgRimColor * rim;
}

float3 WgApplyReadableRim(float3 color, float3 normalWS, float3 viewDirectionWS, float amount)
{
    WgRimLightingParams paramsData;
    paramsData._WgRimColor = WgColorParchment200;
    paramsData._WgRimAmount = amount;
    paramsData._WgRimPower = 2.5;
    paramsData._WgRimMin = 0.18;
    paramsData._WgRimMax = 0.95;
    return WgApplyRimLighting(color, normalWS, viewDirectionWS, paramsData);
}

#endif
