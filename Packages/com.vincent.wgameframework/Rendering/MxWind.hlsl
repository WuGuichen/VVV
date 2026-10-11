#ifndef MX_WIND_INCLUDED
#define MX_WIND_INCLUDED
#include "MxFrameInputs.hlsl"

float3 MxWindDirection()
{
    float2 direction = _MxWindDirection.xz;
    float lengthSquared = dot(direction, direction);
    direction = lengthSquared > 0.00001 ? direction * rsqrt(lengthSquared) : float2(1, 0);
    return float3(direction.x, 0, direction.y);
}

float MxWindSway(float3 positionWS, float time, float frequency, float variation)
{
    float phase = dot(positionWS.xz, float2(0.37, 0.23)) + time * max(frequency, 0);
    return sin(phase * 6.2831853) * 0.62 + sin(phase * 3.1 + variation * 2.4) * 0.22;
}

float3 MxWindDisplacement(float3 positionWS, float time, float frequency, float variation)
{
    return MxWindDirection() * MxWindSway(positionWS, time, frequency, variation)
        * saturate(_MxWindStrength + _MxWindTurbulence * 0.35);
}

#endif
