#ifndef WGAME_EMISSION_INCLUDED
#define WGAME_EMISSION_INCLUDED

#include "WGameShaderCommon.hlsl"

struct WgEmissionParams
{
    float3 _WgEmissionColor;
    float _WgEmissionIntensity;
    float _WgEmissionMaskWeight;
    float _WgEmissionPulseAmount;
    float _WgEmissionMaxIntensity;
};

float3 WgAmberEmissionColor()
{
    return WgColorAmber400;
}

float3 WgEmberEmissionColor()
{
    return lerp(WgColorDanger500, WgColorAmber400, 0.28);
}

float3 WgHealingEmissionColor()
{
    return WgColorSuccess500;
}

float3 WgInfoEmissionColor()
{
    return WgColorInfo500;
}

float WgClampEmissionIntensity(float intensity, float maxIntensity)
{
    return min(max(intensity, 0.0), max(maxIntensity, 0.0));
}

float WgEmissionPulse(float timeSeconds, float frequency, float pulseAmount)
{
    return lerp(1.0, WgPulse01(timeSeconds, frequency, 0.0), saturate(pulseAmount));
}

float3 WgEvaluateEmission(float maskValue, float timeSeconds, float pulseFrequency, WgEmissionParams paramsData)
{
    float maskWeight = saturate(maskValue) * saturate(paramsData._WgEmissionMaskWeight);
    float pulse = WgEmissionPulse(timeSeconds, pulseFrequency, paramsData._WgEmissionPulseAmount);
    float intensity = WgClampEmissionIntensity(paramsData._WgEmissionIntensity * pulse, paramsData._WgEmissionMaxIntensity);
    return paramsData._WgEmissionColor * intensity * maskWeight;
}

float3 WgAddEmission(float3 litColor, float3 emissionColor)
{
    return litColor + max(emissionColor, 0.0);
}

#endif
