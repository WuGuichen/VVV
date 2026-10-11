#ifndef MX_SUN_VISIBILITY_INCLUDED
#define MX_SUN_VISIBILITY_INCLUDED

float MxSampleSunVisibility(float3 positionWS, float4x4 viewProjection, float4x4 view,
    float4 range, Texture2D<float> visibility, SamplerState pointSampler)
{
    float4 clip = mul(viewProjection, float4(positionWS, 1));
    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
    if (range.z > 0.5) uv.y = 1 - uv.y;
    if (any(uv < 0) || any(uv > 1)) return 1;
    float depth = (-mul(view, float4(positionWS, 1)).z - range.x) * range.y;
    return depth <= visibility.SampleLevel(pointSampler, uv, 0) + 0.001 ? 1 : 0;
}

#endif
