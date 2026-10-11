#ifndef WGAME_MAIN_LIGHT_INCLUDED
#define WGAME_MAIN_LIGHT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "WGameShaderCommon.hlsl"
#include "Packages/com.vincent.wgameframework/Rendering/MxDirectionalShadow.hlsl"

struct WgMainLightData
{
    float3 directionWS;
    float3 color;
    float shadowAttenuation;
    float distanceAttenuation;
};

WgMainLightData WgGetMainLightData(float3 positionWS, float3 fallbackDirectionWS, bool receiverPlaneCorrection)
{
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    Light mainLight;
    UNITY_BRANCH
    if (_MxDirectionalShadowEnabled > 0.5)
    { mainLight = GetMainLight(); mainLight.shadowAttenuation = MxDirectionalSunVisibility(positionWS, receiverPlaneCorrection); }
    else mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
#else
    Light mainLight = GetMainLight();
    if (_MxDirectionalShadowEnabled > 0.5) mainLight.shadowAttenuation = MxDirectionalSunVisibility(positionWS, receiverPlaneCorrection);
#endif

    WgMainLightData result;
    result.directionWS = WgSafeNormalize(mainLight.direction, WgSafeNormalize(fallbackDirectionWS, float3(0.0, 1.0, 0.0)));
    result.color = max(mainLight.color, 0.0);
    result.shadowAttenuation = saturate(mainLight.shadowAttenuation);
    result.distanceAttenuation = saturate(mainLight.distanceAttenuation);
    return result;
}

WgMainLightData WgGetMainLightData(float3 positionWS, float3 fallbackDirectionWS)
{
    return WgGetMainLightData(positionWS, fallbackDirectionWS, true);
}

float3 WgApplyMainLightColor(float3 color, WgMainLightData mainLight, float influence)
{
    float luminance = WgLuminance(mainLight.color);
    float3 fallbackTint = max(luminance.xxx, 0.35);
    float3 lightTint = lerp(fallbackTint, max(mainLight.color, 0.12), saturate(luminance));
    return lerp(color, color * lightTint, saturate(influence));
}

float3 WgApplyMainLightShadowReceive(float3 color, WgMainLightData mainLight, float receiveStrength)
{
    // The fixed-direction path attenuates authored direct terms at their source.
    // Do not apply a second tint/dim operation to the composed surface.
    if (_MxDirectionalShadowEnabled > 0.5) return color;
    float attenuation = saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
    float shadow = (1.0 - attenuation) * saturate(receiveStrength);
    float luminance = WgLuminance(color);
    float maxReadableDrop = luminance * 0.55;
    float relativeDrop = max(luminance * 0.34, 0.025);
    float valueDrop = min(maxReadableDrop, relativeDrop);
    float valueScale = saturate(1.0 - shadow * valueDrop * WgSafeRcp(max(luminance, WG_EPSILON)));
    float3 coldTint = lerp(float3(1.0, 1.0, 1.0), float3(0.78, 0.86, 0.96), shadow * 0.55);
    return min(color, color * coldTint * valueScale);
}

#endif
