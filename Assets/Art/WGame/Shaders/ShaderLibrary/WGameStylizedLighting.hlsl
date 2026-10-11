#ifndef WGAME_STYLIZED_LIGHTING_INCLUDED
#define WGAME_STYLIZED_LIGHTING_INCLUDED

#include "WGameShaderCommon.hlsl"

struct WgStylizedLightingInput
{
    float3 normalWS;
    float3 lightDirectionWS;
    float3 viewDirectionWS;
    float3 baseColor;
    float rampValue;
    float facetValue;
};

struct WgStylizedLightingParams
{
    float _WgRampContrast;
    float _WgRampOffset;
    float _WgRampStepCount;
    float _WgFacetStrength;
    float _WgShadowStrength;
    float _WgLightWrap;
};

float WgHalfLambert(float3 normalWS, float3 lightDirectionWS, float lightWrap)
{
    float ndl = dot(WgSafeNormalize(normalWS, float3(0.0, 1.0, 0.0)), WgSafeNormalize(lightDirectionWS, float3(0.0, 1.0, 0.0)));
    float wrapped = (ndl + saturate(lightWrap)) * WgSafeRcp(1.0 + saturate(lightWrap));
    return saturate(wrapped);
}

float WgQuantizeRamp(float value, float stepCount)
{
    float safeSteps = max(stepCount, 1.0);
    return floor(saturate(value) * safeSteps) * WgSafeRcp(safeSteps);
}

float WgComputeRampValue(float lambertValue, float authoredRampValue, WgStylizedLightingParams paramsData)
{
    float ramp = saturate(lambertValue + paramsData._WgRampOffset);
    ramp = WgContrast(ramp, 0.5, max(paramsData._WgRampContrast, 0.0));
    ramp = lerp(ramp, saturate(authoredRampValue), saturate(abs(authoredRampValue)));
    return paramsData._WgRampStepCount > 1.0 ? WgQuantizeRamp(ramp, paramsData._WgRampStepCount) : ramp;
}

float2 WgRampUv(float rampValue, float rampRow)
{
    return float2(saturate(rampValue), saturate(rampRow));
}

float3 WgThreeBandRampColor(float3 shadowColor, float3 midColor, float3 litColor, float rampValue)
{
    float safeRamp = saturate(rampValue);
    float midWeight = smoothstep(0.18, 0.58, safeRamp);
    float litWeight = smoothstep(0.52, 0.92, safeRamp);
    return lerp(lerp(shadowColor, midColor, midWeight), litColor, litWeight);
}

float3 WgApplyFacetLighting(float3 litColor, float facetValue, float facetStrength)
{
    float facet = lerp(1.0, saturate(facetValue) * 0.5 + 0.75, saturate(facetStrength));
    return litColor * facet;
}

float3 WgApplyStylizedLighting(WgStylizedLightingInput inputData, WgStylizedLightingParams paramsData, float3 shadowColor, float3 litColor)
{
    float lambertValue = WgHalfLambert(inputData.normalWS, inputData.lightDirectionWS, paramsData._WgLightWrap);
    float ramp = WgComputeRampValue(lambertValue, inputData.rampValue, paramsData);
    float3 rampColor = WgThreeBandRampColor(shadowColor, inputData.baseColor, litColor, ramp);
    float3 faceted = WgApplyFacetLighting(rampColor, inputData.facetValue, paramsData._WgFacetStrength);
    return lerp(inputData.baseColor, faceted, saturate(1.0 - paramsData._WgShadowStrength));
}

#endif
