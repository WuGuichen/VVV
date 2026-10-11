#ifndef WGAME_SHADER_COMMON_INCLUDED
#define WGAME_SHADER_COMMON_INCLUDED

#define WG_EPSILON 0.00001

static const float3 WgColorObsidian950 = float3(0.02745, 0.06667, 0.10196);
static const float3 WgColorNavy900 = float3(0.05098, 0.11373, 0.16471);
static const float3 WgColorNavy800 = float3(0.07451, 0.16471, 0.21961);
static const float3 WgColorSlate700 = float3(0.14118, 0.20392, 0.22745);
static const float3 WgColorMoss700 = float3(0.20784, 0.26275, 0.19216);
static const float3 WgColorStone500 = float3(0.37647, 0.38039, 0.35294);
static const float3 WgColorBronze500 = float3(0.60784, 0.46275, 0.31373);
static const float3 WgColorAmber400 = float3(0.94118, 0.70588, 0.37255);
static const float3 WgColorParchment200 = float3(0.85098, 0.78824, 0.64706);
static const float3 WgColorDanger500 = float3(0.70588, 0.32549, 0.27059);
static const float3 WgColorSuccess500 = float3(0.50980, 0.65098, 0.41961);
static const float3 WgColorInfo500 = float3(0.42745, 0.60784, 0.70980);

struct WgMaskSample
{
    float channelR;
    float channelG;
    float channelB;
    float channelA;
};

struct WgPackedMaterialMask
{
    float primary;
    float secondary;
    float trim;
    float emission;
};

float WgSafeRcp(float value)
{
    return rcp(max(abs(value), WG_EPSILON));
}

float WgRemap(float value, float inputMin, float inputMax, float outputMin, float outputMax)
{
    float t = saturate((value - inputMin) * WgSafeRcp(inputMax - inputMin));
    return lerp(outputMin, outputMax, t);
}

float WgRemap01(float value, float inputMin, float inputMax)
{
    return saturate((value - inputMin) * WgSafeRcp(inputMax - inputMin));
}

float3 WgSafeNormalize(float3 value, float3 fallback)
{
    float lenSq = dot(value, value);
    return lenSq > WG_EPSILON ? value * rsqrt(lenSq) : fallback;
}

float WgLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float3 WgApplyTint(float3 baseColor, float3 tintColor, float tintAmount)
{
    return lerp(baseColor, baseColor * tintColor, saturate(tintAmount));
}

float3 WgOverlayColor(float3 baseColor, float3 overlayColor, float amount)
{
    return lerp(baseColor, overlayColor, saturate(amount));
}

float WgSoftMask(float value, float threshold, float softness)
{
    float safeSoftness = max(softness, WG_EPSILON);
    return smoothstep(threshold - safeSoftness, threshold + safeSoftness, value);
}

float WgContrast(float value, float midpoint, float contrast)
{
    return saturate((value - midpoint) * contrast + midpoint);
}

WgMaskSample WgDecodeMaskSample(float4 maskSample)
{
    WgMaskSample result;
    result.channelR = saturate(maskSample.r);
    result.channelG = saturate(maskSample.g);
    result.channelB = saturate(maskSample.b);
    result.channelA = saturate(maskSample.a);
    return result;
}

WgPackedMaterialMask WgDecodePackedMaterialMask(float4 maskSample)
{
    WgMaskSample decoded = WgDecodeMaskSample(maskSample);
    WgPackedMaterialMask result;
    result.primary = decoded.channelR;
    result.secondary = decoded.channelG;
    result.trim = decoded.channelB;
    result.emission = decoded.channelA;
    return result;
}

float3 WgBlendThreeWay(float3 baseColor, float3 secondaryColor, float3 trimColor, float secondaryWeight, float trimWeight)
{
    float safeTrim = saturate(trimWeight);
    float safeSecondary = saturate(secondaryWeight) * (1.0 - safeTrim);
    float3 blended = lerp(baseColor, secondaryColor, safeSecondary);
    return lerp(blended, trimColor, safeTrim);
}

float WgPulse01(float timeSeconds, float frequency, float phase)
{
    return 0.5 + 0.5 * sin((timeSeconds * max(frequency, 0.0) + phase) * 6.2831853);
}

float WgDistanceFade(float distanceValue, float nearDistance, float farDistance)
{
    return saturate((distanceValue - nearDistance) * WgSafeRcp(farDistance - nearDistance));
}

#endif
