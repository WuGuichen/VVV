#ifndef WGAME_PALETTE_DETAIL_INCLUDED
#define WGAME_PALETTE_DETAIL_INCLUDED

#include "WGamePalette.hlsl"

struct WgPaletteProceduralDetail
{
    float diffuseMultiplier;
    float specularMultiplier;
};

float WgPaletteDetailTriangle(float coordinate)
{
    float triangleValue = abs(frac(coordinate) * 2.0 - 1.0);
    float footprintFade = 1.0 - saturate(fwidth(coordinate) * 2.0);
    return (lerp(0.5, triangleValue, footprintFade) - 0.5) * 2.0;
}

float WgPaletteDetailModeMask(float mode, float expected)
{
    return 1.0 - saturate(abs(mode - expected));
}

WgPaletteProceduralDetail WgEvaluatePaletteProceduralDetail(
    float3 positionOS,
    float3 positionWS,
    float surfaceProfile,
    float emission,
    float4 detailParams)
{
    WgPaletteProceduralDetail result;
    result.diffuseMultiplier = 1.0;
    result.specularMultiplier = 1.0;
    float globalStrength = saturate(detailParams.x);
    float diffuseAmplitude = max(0.0, WgSurfaceProfileDetailDiffuse(surfaceProfile));
    float specularAmplitude = max(0.0, WgSurfaceProfileDetailSpecular(surfaceProfile));
    float frequency = max(0.01, WgSurfaceProfileDetailScale(surfaceProfile));
    float detailMode = WgSurfaceProfileDetailMode(surfaceProfile);
    float wovenDetailMask = WgPaletteDetailModeMask(detailMode, 1.0);
    float silkDetailMask = WgPaletteDetailModeMask(detailMode, 2.0);
    float matteDetailMask = WgPaletteDetailModeMask(detailMode, 5.0);
    float textileDetailMask = saturate(wovenDetailMask + silkDetailMask + matteDetailMask);
    diffuseAmplitude *= 1.0
        + wovenDetailMask * 0.65
        + silkDetailMask * 0.20
        + matteDetailMask * 0.28;
    specularAmplitude *= 1.0
        + wovenDetailMask * 0.35
        + silkDetailMask * 0.18
        + matteDetailMask * 0.20;
    UNITY_BRANCH
    if (globalStrength > WG_EPSILON
        && frequency > WG_EPSILON
        && max(diffuseAmplitude, specularAmplitude) > WG_EPSILON)
    {
        float fadeStart = max(0.0, detailParams.y);
        float fadeEnd = max(fadeStart + 0.01, detailParams.z);
        float distanceFade = 1.0 - smoothstep(fadeStart, fadeEnd, distance(positionWS, GetCameraPositionWS()));
        UNITY_BRANCH
        if (distanceFade > WG_EPSILON)
        {
            float objectFootprint = max(length(ddx(positionOS)), length(ddy(positionOS))) * frequency;
            float footprintFade = 1.0 - smoothstep(0.35, 0.75, objectFootprint);
            float amount = globalStrength * distanceFade * footprintFade;
            UNITY_BRANCH
            if (amount > WG_EPSILON)
            {
                const float3 basis0 = float3(0.742, 0.321, 0.588);
                const float3 basis1 = float3(-0.283, 0.901, 0.328);
                float coordinate0 = dot(positionOS, basis0) * frequency;
                float coordinate1 = dot(positionOS, basis1) * frequency;
                float primary = WgPaletteDetailTriangle(coordinate0);
                float secondary = detailParams.w > 0.5 ? WgPaletteDetailTriangle(coordinate1) : 0.0;
                float weave = detailParams.w > 0.5
                    ? (primary + secondary) * 0.45 + primary * secondary * 0.20
                    : primary * 0.55;
                float silk = detailParams.w > 0.5 ? primary * 0.30 + secondary * 0.12 : primary * 0.22;
                float leatherVolume = clamp(primary * secondary + (primary - secondary) * 0.20, -1.0, 1.0);
                float leatherFold = WgPaletteDetailTriangle(coordinate0 * 0.37 + secondary * 0.15);
                float leather = detailParams.w > 0.5 ? leatherVolume * 0.72 + leatherFold * 0.28 : primary * 0.55;
                float wood = detailParams.w > 0.5
                    ? clamp(primary * 0.62 + WgPaletteDetailTriangle(coordinate1 * 0.31 + primary * 0.2) * 0.38, -1.0, 1.0)
                    : primary * 0.50;
                float matte = detailParams.w > 0.5 ? (primary + secondary) * 0.32 : primary * 0.35;
                float scratchMask = smoothstep(0.86, 0.98, abs(secondary));
                float brushed = clamp(primary * 0.75 + secondary * 0.25 + scratchMask * primary * 0.30, -1.0, 1.0);
                float frost = detailParams.w > 0.5 ? clamp((primary + secondary) * 0.42 + primary * secondary * 0.22, -1.0, 1.0) : primary * 0.45;
                float crystal = detailParams.w > 0.5 ? (primary * 0.20 + secondary * 0.12) : primary * 0.12;

                float diffuseSignal =
                    WgPaletteDetailModeMask(detailMode, 1.0) * weave
                    + WgPaletteDetailModeMask(detailMode, 2.0) * silk
                    + WgPaletteDetailModeMask(detailMode, 3.0) * leather
                    + WgPaletteDetailModeMask(detailMode, 4.0) * wood
                    + WgPaletteDetailModeMask(detailMode, 5.0) * matte
                    + WgPaletteDetailModeMask(detailMode, 7.0) * frost
                    + WgPaletteDetailModeMask(detailMode, 8.0) * crystal;
                float nonEmissive = 1.0 - saturate(emission * 20.0);
                float specularSignal = diffuseSignal
                    + WgPaletteDetailModeMask(detailMode, 6.0) * brushed * nonEmissive;
                result.diffuseMultiplier = clamp(
                    1.0 + clamp(diffuseSignal, -1.0, 1.0) * amount * diffuseAmplitude,
                    lerp(0.94, 0.90, textileDetailMask),
                    lerp(1.06, 1.10, textileDetailMask));
                result.specularMultiplier = clamp(
                    1.0 + clamp(specularSignal, -1.0, 1.0) * amount * specularAmplitude * 3.0,
                    lerp(0.80, 0.76, textileDetailMask),
                    lerp(1.25, 1.32, textileDetailMask));
            }
        }
    }
    return result;
}

#endif
