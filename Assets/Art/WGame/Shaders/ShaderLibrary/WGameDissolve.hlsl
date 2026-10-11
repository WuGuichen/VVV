#ifndef WGAME_DISSOLVE_INCLUDED
#define WGAME_DISSOLVE_INCLUDED

#include "WGameShaderCommon.hlsl"

struct WgDissolveParams
{
    float _WgDissolveProgress;
    float _WgDissolveEdgeWidth;
    float3 _WgDissolveEdgeColor;
    float _WgLifetimeFade;
    float _WgDissolveNoiseStrength;
};

struct WgDissolveResult
{
    float visibility;
    float edge;
    float clipValue;
    float3 edgeColor;
};

float WgDissolveThreshold(WgDissolveParams paramsData)
{
    float progress = saturate(paramsData._WgDissolveProgress);
    float lifetimeFade = saturate(paramsData._WgLifetimeFade);
    return saturate(max(progress, lifetimeFade));
}

// Stable UV-space value noise keeps forward, depth and shadow coverage identical.
float WgDissolveNoiseHash(float2 cell)
{
    float3 p = frac(float3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float WgDissolveProceduralNoise(float2 uv)
{
    float2 coordinate = uv * 32.0;
    float2 cell = floor(coordinate);
    float2 blend = frac(coordinate);
    blend = blend * blend * (3.0 - 2.0 * blend);
    return lerp(
        lerp(WgDissolveNoiseHash(cell), WgDissolveNoiseHash(cell + float2(1.0, 0.0)), blend.x),
        lerp(WgDissolveNoiseHash(cell + float2(0.0, 1.0)), WgDissolveNoiseHash(cell + 1.0), blend.x),
        blend.y);
}

WgDissolveResult WgEvaluateDissolve(float noiseValue, WgDissolveParams paramsData)
{
    float threshold = WgDissolveThreshold(paramsData);
    float noise = lerp(0.5, saturate(noiseValue), saturate(paramsData._WgDissolveNoiseStrength));
    float edgeWidth = max(paramsData._WgDissolveEdgeWidth, WG_EPSILON);

    WgDissolveResult result;
    // clip(0) survives: explicitly discard the endpoint, including white texels.
    float completed = step(1.0, threshold);
    result.visibility = step(threshold, noise) * (1.0 - completed);
    result.edge = smoothstep(threshold, threshold + edgeWidth, noise) * (1.0 - smoothstep(threshold + edgeWidth, threshold + edgeWidth * 2.0, noise));
    result.clipValue = lerp(noise - threshold, -1.0, completed);
    result.edge *= 1.0 - completed;
    result.edgeColor = paramsData._WgDissolveEdgeColor * result.edge;
    return result;
}

float3 WgApplyDissolveEdge(float3 color, WgDissolveResult dissolveData)
{
    return lerp(color, dissolveData.edgeColor, saturate(dissolveData.edge));
}

float WgDissolveAlpha(float baseAlpha, WgDissolveResult dissolveData)
{
    return baseAlpha * dissolveData.visibility;
}

#endif
