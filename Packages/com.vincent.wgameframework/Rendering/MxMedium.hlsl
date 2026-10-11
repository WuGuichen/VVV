#ifndef MX_MEDIUM_INCLUDED
#define MX_MEDIUM_INCLUDED

float MxTransmittance(float opticalDepth) { return exp(-max(opticalDepth, 0)); }

float MxHeightDensity(float height, float baseHeight, float falloff)
{
    return exp(-max(0, height - baseHeight) * max(falloff, 0));
}

float MxFocusVisibility(float3 positionWS, float3 focus, float radius, float softness)
{
    return smoothstep(max(radius, 0), max(radius, 0) + max(softness, 0.001), distance(positionWS, focus));
}

float MxFogAmountFromOpticalDepth(float opticalDepth, float3 positionWS, float3 focus,
    float maxDistance, float4 scattering, float4 clear, bool sky)
{
    float anisotropy = clamp(scattering.x, -0.95, 0.95);
    float phase = lerp(1 - anisotropy * 0.35, 1 + anisotropy * 0.35,
        saturate(distance(positionWS, focus) / max(maxDistance, 1)));
    float amount = saturate(1 - MxTransmittance(opticalDepth * max(scattering.y, 0) * phase));
    return amount * (sky ? 1 : MxFocusVisibility(positionWS, focus, clear.x, clear.y));
}

// Matches the existing height-fog approximation; not a ray integral.
float MxHeightFogAmount(float3 positionWS, float distanceToCamera, float3 focus,
    float4 densityDistanceNoiseEnabled, float4 height, float4 scattering, float4 clear)
{
    if (densityDistanceNoiseEnabled.w < 0.5) return 0;
    float range = max(densityDistanceNoiseEnabled.y, 1);
    float opticalDepth = max(densityDistanceNoiseEnabled.x, 0) * saturate(distanceToCamera / range)
        * MxHeightDensity(positionWS.y, height.x, height.y);
    return MxFogAmountFromOpticalDepth(opticalDepth, positionWS, focus, range, scattering, clear, false);
}

// Directions both point away from the sample. Normalized phase, integral = 1.
float MxHenyeyGreenstein(float3 toLight, float3 toEye, float anisotropy)
{
    float g = clamp(anisotropy, -0.85, 0.85);
    float denominator = max(1 + g * g + 2 * g * dot(toLight, toEye), 0.001);
    return (1 - g * g) / (12.5663706 * denominator * sqrt(denominator));
}

#endif
