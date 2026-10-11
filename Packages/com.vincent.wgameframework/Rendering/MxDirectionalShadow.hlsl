#ifndef MX_DIRECTIONAL_SHADOW_INCLUDED
#define MX_DIRECTIONAL_SHADOW_INCLUDED
Texture2D<float> _MxDirectionalShadowStaticDepth;
Texture2D<float> _MxDirectionalShadowPeripheralDepth;
Texture2D<uint> _MxDirectionalShadowDynamicDepth;
StructuredBuffer<uint> _MxDirectionalShadowStatus;
float _MxDirectionalShadowEnabled;
float4 _MxDirectionalShadowBasisX, _MxDirectionalShadowBasisY, _MxDirectionalShadowDirection;
float4 _MxDirectionalShadowStaticRect, _MxDirectionalShadowDynamicRect;
float4 _MxDirectionalShadowPeripheralRect;
// peripheral width/height, center transition width in light-space metres, reserved
float4 _MxDirectionalShadowPeripheralSize;
// Versioned bake contract: packed-depth enabled, world plane Y, Y tolerance, baked world bias.
// Provider MUST bind zero for legacy depth assets and every disabled/non-target camera.
float4 _MxDirectionalShadowStaticCoverage;
float MxDirectionalStaticVisibility(float stored, float receiver, float bias, bool receiverOnCoveragePlane)
{
    if (_MxDirectionalShadowStaticCoverage.x > .5)
    {
        uint bits = asuint(stored);
        float depth = asfloat(bits & 0xffffffc0u);
        if (receiverOnCoveragePlane) return 1 - (bits & 63u) * (1.0 / 63.0);
        return receiver <= depth + bias ? 1 : 0;
    }
    return receiver <= stored + bias ? 1 : 0;
}
// depth minimum, inverse world-depth span, world-space bias, world-space filter radius
float4 _MxDirectionalShadowRange;
// static width, static height, dynamic resolution, dynamic data present
float4 _MxDirectionalShadowSize;
float MxDirectionalRayVisibility(float2 xy, float receiver, float2 slope, float bias,
    bool sampleStatic, bool sampleDynamic, bool peripheral, float4 staticRect, float2 staticSize, bool receiverOnCoveragePlane)
{
    float2 uv = (xy - staticRect.xy) * staticRect.zw;
    float visibility = 1;
    if (sampleStatic && all(uv >= 0) && all(uv < 1))
    {
        int2 pixel = (int2)(uv * staticSize);
        float2 centre = staticRect.xy + ((float2(pixel) + .5) / staticSize) / staticRect.zw;
        float depth;
        if (peripheral) depth = _MxDirectionalShadowPeripheralDepth.Load(int3(pixel, 0));
        else depth = _MxDirectionalShadowStaticDepth.Load(int3(pixel, 0));
        visibility = MxDirectionalStaticVisibility(depth, receiver + dot(slope, centre - xy), bias, receiverOnCoveragePlane);
    }
    if (sampleDynamic)
    {
        uv = (xy - _MxDirectionalShadowDynamicRect.xy) * _MxDirectionalShadowDynamicRect.zw;
        if (all(uv >= 0) && all(uv < 1))
        {
            int2 pixel = (int2)(uv * _MxDirectionalShadowSize.z);
            float2 centre = _MxDirectionalShadowDynamicRect.xy + ((float2(pixel) + .5) / _MxDirectionalShadowSize.z) / _MxDirectionalShadowDynamicRect.zw;
            float depth = asfloat(_MxDirectionalShadowDynamicDepth.Load(int3(pixel, 0)));
            visibility = min(visibility, receiver + dot(slope, centre - xy) <= depth + bias ? 1 : 0);
        }
    }
    return visibility;
}
// Each field keeps its own comparison lattice and its authored filter support.
// Corner order is lower-left, lower-right, upper-left, upper-right.
float4 MxDirectionalRegionComparisons(float2 xy, float receiver, float2 slope, float bias,
    bool dynamic, bool peripheral, float4 rectangle, float2 size, out float2 fraction, bool receiverOnCoveragePlane)
{
    float2 texel = rcp(rectangle.zw * size);
    float2 spacing = max(texel, max(_MxDirectionalShadowRange.w, 0));
    float2 position = (xy - rectangle.xy) / spacing - .5;
    float2 lower = floor(position);
    fraction = position - lower;
    float2 ray00 = rectangle.xy + (lower + .5) * spacing;
    float2 ray10 = ray00 + float2(spacing.x, 0);
    float2 ray01 = ray00 + float2(0, spacing.y);
    float2 ray11 = ray00 + spacing;
    return float4(
        MxDirectionalRayVisibility(ray00, receiver + dot(slope, ray00 - xy), slope, bias,
            !dynamic, dynamic, peripheral, rectangle, size, receiverOnCoveragePlane),
        MxDirectionalRayVisibility(ray10, receiver + dot(slope, ray10 - xy), slope, bias,
            !dynamic, dynamic, peripheral, rectangle, size, receiverOnCoveragePlane),
        MxDirectionalRayVisibility(ray01, receiver + dot(slope, ray01 - xy), slope, bias,
            !dynamic, dynamic, peripheral, rectangle, size, receiverOnCoveragePlane),
        MxDirectionalRayVisibility(ray11, receiver + dot(slope, ray11 - xy), slope, bias,
            !dynamic, dynamic, peripheral, rectangle, size, receiverOnCoveragePlane));
}
float MxDirectionalWeightComparisons(float4 visibility, float2 fraction)
{
    return lerp(lerp(visibility.x, visibility.y, fraction.x),
        lerp(visibility.z, visibility.w, fraction.x), fraction.y);
}
float4 MxDirectionalJointWeights(float staticFraction, float dynamicFraction)
{
    // Partition a shared unit filter footprint at both bilinear thresholds.
    // Pair order: lower/lower, lower/upper, upper/lower, upper/upper.
    // Marginals are exactly each field's original bilinear weights. Coupling the
    // coverage before averaging preserves overlap; multiplying two averages does not.
    float a = 1 - staticFraction, b = 1 - dynamicFraction;
    return float4(min(a, b), max(a - b, 0), max(b - a, 0), 1 - max(a, b));
}
float MxDirectionalFilterRegion(float2 xy, float receiver, float2 slope, float bias,
    bool sampleStatic, bool sampleDynamic, bool peripheral, float4 staticRect, float2 staticSize, bool receiverOnCoveragePlane)
{
    if (!sampleStatic)
    {
        if (!sampleDynamic) return 1;
        float2 fraction;
        float4 visibility = MxDirectionalRegionComparisons(xy, receiver, slope, bias,
            true, false, _MxDirectionalShadowDynamicRect, _MxDirectionalShadowSize.zz, fraction, receiverOnCoveragePlane);
        return MxDirectionalWeightComparisons(visibility, fraction);
    }
    float2 staticFraction;
    float4 staticVisibility = MxDirectionalRegionComparisons(xy, receiver, slope, bias,
        false, peripheral, staticRect, staticSize, staticFraction, receiverOnCoveragePlane);
    if (!sampleDynamic) return MxDirectionalWeightComparisons(staticVisibility, staticFraction);
    float2 dynamicFraction;
    float4 dynamicVisibility = MxDirectionalRegionComparisons(xy, receiver, slope, bias,
        true, false, _MxDirectionalShadowDynamicRect, _MxDirectionalShadowSize.zz, dynamicFraction, receiverOnCoveragePlane);
    // Besides single-source operation, this preserves each native filter exactly
    // where the other field's footprint is empty (including outside its bounds).
    if (all(staticVisibility == 1))
        return MxDirectionalWeightComparisons(dynamicVisibility, dynamicFraction);
    if (all(dynamicVisibility == 1))
        return MxDirectionalWeightComparisons(staticVisibility, staticFraction);

    float4 weightX = MxDirectionalJointWeights(staticFraction.x, dynamicFraction.x);
    float4 weightY = MxDirectionalJointWeights(staticFraction.y, dynamicFraction.y);
    float sum = 0;
    // At most 3 intervals per axis have nonzero weight: nine union regions,
    // using the eight comparisons already loaded, with no extra depth samples.
    [unroll] for (int y = 0; y < 4; y++) [unroll] for (int x = 0; x < 4; x++)
    {
        int staticCorner = (x / 2) + 2 * (y / 2);
        int dynamicCorner = (x % 2) + 2 * (y % 2);
        sum += weightX[x] * weightY[y] * min(staticVisibility[staticCorner], dynamicVisibility[dynamicCorner]);
    }
    return saturate(sum);
}
float MxDirectionalFilteredVisibility(float2 xy, float receiver, float2 slope, float bias, bool receiverOnCoveragePlane)
{
    bool sampleCenter = _MxDirectionalShadowSize.x > 0 && _MxDirectionalShadowSize.y > 0;
    bool samplePeripheral = _MxDirectionalShadowPeripheralSize.x > 0 && _MxDirectionalShadowPeripheralSize.y > 0;
    bool sampleDynamic = false;
    if (_MxDirectionalShadowSize.w > .5) sampleDynamic = _MxDirectionalShadowStatus[0] == 0;
    float weight = 0;
    if (sampleCenter)
    {
        float2 offset = xy - _MxDirectionalShadowStaticRect.xy;
        float2 opposite = rcp(_MxDirectionalShadowStaticRect.zw) - offset;
        float edge = min(min(offset.x, offset.y), min(opposite.x, opposite.y));
        weight = _MxDirectionalShadowPeripheralSize.z > 0
            ? smoothstep(0, _MxDirectionalShadowPeripheralSize.z, edge) : (edge >= 0 ? 1 : 0);
    }
    // Each region unions static/dynamic occlusion before filtering. Only the
    // transition band evaluates both results, and it blends rather than darkens.
    if (weight >= 1)
        return MxDirectionalFilterRegion(xy,receiver,slope,bias,sampleCenter,sampleDynamic,false,
            _MxDirectionalShadowStaticRect,_MxDirectionalShadowSize.xy,receiverOnCoveragePlane);
    float outer = MxDirectionalFilterRegion(xy,receiver,slope,bias,samplePeripheral,sampleDynamic,true,
        _MxDirectionalShadowPeripheralRect,_MxDirectionalShadowPeripheralSize.xy,receiverOnCoveragePlane);
    if (weight <= 0) return outer;
    float inner = MxDirectionalFilterRegion(xy,receiver,slope,bias,sampleCenter,sampleDynamic,false,
        _MxDirectionalShadowStaticRect,_MxDirectionalShadowSize.xy,receiverOnCoveragePlane);
    return lerp(outer,inner,weight);
}
float MxDirectionalSunVisibility(float3 positionWS, bool receiverPlaneCorrection)
{
    float2 xy = float2(dot(positionWS, _MxDirectionalShadowBasisX.xyz), dot(positionWS, _MxDirectionalShadowBasisY.xyz));
    float receiver = (-dot(positionWS, _MxDirectionalShadowDirection.xyz) - _MxDirectionalShadowRange.x)
        * _MxDirectionalShadowRange.y;
    // Derivatives are evaluated in shared light coordinates, before region
    // selection, so pixel quads crossing the blend band retain correct slopes.
    // Compare each depth texel with the receiver plane at that texel's actual ray.
    // This removes slope/PCF self-shadowing on flat floors without darkening GI.
    float2 dx = ddx(xy), dy = ddy(xy);
    float dzx = ddx(receiver), dzy = ddy(receiver);
    float determinant = dx.x * dy.y - dx.y * dy.x;
    float2 slope = abs(determinant) > 1e-10 ? float2(dzx * dy.y - dzy * dx.y, dzy * dx.x - dzx * dy.x) / determinant : 0;
    // A smoothly shaded curved receiver must not extrapolate its individual
    // triangle planes across the PCF footprint: adjacent taps can hit a different
    // part of the same curved surface. Retain the authored depth bias and all
    // visibility filtering, but compare those receivers at their actual depth.
    if (!receiverPlaneCorrection) slope = 0;
    if (receiver < 0 || receiver > 1) return 1;
    float bias = _MxDirectionalShadowRange.z * _MxDirectionalShadowRange.y;
    float receiverYFootprint = .5 * (abs(ddx(positionWS.y)) + abs(ddy(positionWS.y)));
    bool receiverOnCoveragePlane = receiverPlaneCorrection && _MxDirectionalShadowStaticCoverage.x > .5
        && _MxDirectionalShadowStaticCoverage.z > 0
        && abs(positionWS.y - _MxDirectionalShadowStaticCoverage.y) + receiverYFootprint <= _MxDirectionalShadowStaticCoverage.z
        && abs(_MxDirectionalShadowRange.z - _MxDirectionalShadowStaticCoverage.w) <= 1e-6;
    return MxDirectionalFilteredVisibility(xy, receiver, slope, bias, receiverOnCoveragePlane);
}
float MxDirectionalSunVisibility(float3 positionWS)
{
    return MxDirectionalSunVisibility(positionWS, true);
}
#endif
