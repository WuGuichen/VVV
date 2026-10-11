#ifndef WGAME_FOLIAGE_INPUTS_INCLUDED
#define WGAME_FOLIAGE_INPUTS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "WGameShaderCommon.hlsl"
#include "Packages/com.vincent.wgameframework/Rendering/MxWind.hlsl"

#if defined(WG_FOLIAGE_INDIRECT)
    #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
    #include "UnityIndirect.cginc"
    struct WgVegetationInstance
    {
        float4x4 objectToWorld;
        float4x4 worldToObject;
    };
    StructuredBuffer<WgVegetationInstance> _WgVegetationInstances;
    StructuredBuffer<uint> _WgVegetationVisibleIndices;
    uint _WgVegetationUseVisibleIndices;
    uint _WgVegetationVisibleOffset;
#endif

TEXTURE2D(_WgRampTex); SAMPLER(sampler_WgRampTex);
TEXTURE2D(_WgMaskTex); SAMPLER(sampler_WgMaskTex);

// Keep one material layout and one alpha/deformation contract across all passes.
CBUFFER_START(UnityPerMaterial)
    float4 _WgFoliageColor;
    float4 _WgDryColor;
    float4 _WgMossColor;
    float _WgAlphaCutoff;
    float _WgMossCoverage;
    float _WgDryCoverage;
    float _WgWindBendStrength;
    float _WgWindBendFrequency;
    float _WgWindSource;
    float4 _WgWindDirection;
    float _WgWindGustStrength;
    float _WgWindTipFlutter;
    float _WgShadowReceiveStrength;
    float _WgRootShadeStrength;
    float _WgRootShadeHeight;
    float _WgRootShadeFadeDistance;
    float _WgFogBlendStrength;
    float _WgFacetStrength;
    float _WgVertexTintStrength;
    float _WgVertexTintAmount;
    float _WgPatchColorStrength;
    float _WgWindGustSpatialScale;
    float _WgSeparateWindHeight;
    float _WgLeafNormalUpBlend;
    float _WgWetnessOverride;
    float4 _WgDebugOverlayColor;
    float _WgDebugOverlayAmount;
CBUFFER_END

struct WgFoliageAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float2 windUV : TEXCOORD1;
    float4 color : COLOR;
    #if defined(WG_FOLIAGE_INDIRECT)
        uint indirectInstanceID : SV_InstanceID;
    #else
        UNITY_VERTEX_INPUT_INSTANCE_ID
    #endif
};

struct WgFoliageVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float4 color : TEXCOORD3;
    float fogFactor : TEXCOORD4;
    float rootShadeDistanceFade : TEXCOORD5;
    float instanceVariation : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float WgFoliageSeed(float3 rootPositionWS)
{
    float2 cell = frac(rootPositionWS.xz * float2(0.1031, 0.11369));
    cell += dot(cell, cell.yx + 19.19);
    return frac(cell.x * cell.y);
}

// Both sides of a narrow leaf keep some upward canopy lighting. This changes
// shading only; the actual geometry remains the depth and baked-shadow source.
float3 WgFoliageLitNormal(float3 normalWS, float faceSign)
{
    float3 faceNormal = WgSafeNormalize(normalWS, float3(0, 1, 0)) * faceSign;
    return WgSafeNormalize(lerp(faceNormal, float3(0, 1, 0),
        saturate(_WgLeafNormalUpBlend)), float3(0, 1, 0));
}

// Response belongs to foliage, while GlobalFrame remains the optional weather owner.
// World-metre amplitudes are independent of instance scale. Maximum displacement
// is 0.42 * sqrt(1 + 0.18^2) < 0.427 m, within the renderer's 1 m bounds padding.
float3 WgFoliageWind(float3 rootPositionWS, float heightWeight)
{
    float useGlobal = step(0.5, _WgWindSource);
    float3 directionWS = WgSafeNormalize(float3(_WgWindDirection.x, 0, _WgWindDirection.z), float3(0.8, 0, 0.6));
    directionWS = lerp(directionWS, MxWindDirection(), useGlobal);
    float3 crossWindWS = float3(-directionWS.z, 0, directionWS.x);
    float strength = saturate(_WgWindBendStrength) * lerp(1.0, saturate(_MxWindStrength), useGlobal);
    float turbulence = lerp(saturate(_WgWindGustStrength), saturate(_MxWindTurbulence), useGlobal);
    float time = MxGameTime() * max(_WgWindBendFrequency, 0.0);

    // Stable root hash survives draw repacking and works in classic instancing too.
    // No SV_InstanceID phase: culling/reordering must not restart the animation.
    float seed = WgFoliageSeed(rootPositionWS);
    float phase = seed * 6.2831853;
    // Constant phase fronts move along the main wind, rather than flickering per clump.
    float gustPhase = dot(rootPositionWS, directionWS) * max(_WgWindGustSpatialScale, 0.05) - time * 1.15;
    float gust = smoothstep(-0.6, 1.1, sin(gustPhase)
        + 0.3 * sin(gustPhase * 0.47 + dot(rootPositionWS, crossWindWS) * 0.23));
    gust = lerp(0.35, gust, turbulence);
    float sway = sin(time * (1.7 + seed * 0.45) + phase);
    float flutter = sin(time * (5.1 + seed * 1.3) + phase * 2.7)
        * 0.65 + sin(time * 7.9 + phase * 4.1) * 0.35;
    float height = saturate(heightWeight);
    float bend = height * height;
    float tip = bend * bend;
    return (directionWS * ((0.35 + 0.55 * gust + 0.10 * sway) * bend)
        + crossWindWS * (0.18 * saturate(_WgWindTipFlutter) * flutter * tip)) * (strength * 0.42);
}

WgFoliageVaryings WgFoliageVertex(WgFoliageAttributes input)
{
    WgFoliageVaryings output = (WgFoliageVaryings)0;
    #if defined(WG_FOLIAGE_INDIRECT)
        InitIndirectDrawArgs(0);
        uint recordIndex = GetIndirectInstanceID(input.indirectInstanceID);
        if (_WgVegetationUseVisibleIndices != 0)
            recordIndex = _WgVegetationVisibleIndices[_WgVegetationVisibleOffset + recordIndex];
        WgVegetationInstance instance = _WgVegetationInstances[recordIndex];
        float3 positionWS = mul(instance.objectToWorld, input.positionOS).xyz;
        float3 normalWS = WgSafeNormalize(mul(input.normalOS, (float3x3)instance.worldToObject), float3(0, 1, 0));
        float3 rootPositionWS = mul(instance.objectToWorld, float4(0, 0, 0, 1)).xyz;
        float upScale = length(mul((float3x3)instance.objectToWorld, float3(0, 1, 0)));
    #else
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        float3 rootPositionWS = TransformObjectToWorld(float3(0, 0, 0));
        float upScale = length(mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0)));
    #endif
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    // The root stays anchored while wind bends the tip. Both backends use the
    // same world-space camera distance, including nonuniform owner transforms.
    float fadeEnd = max(_WgRootShadeFadeDistance, 0.001);
    output.rootShadeDistanceFade = 1.0 - smoothstep(fadeEnd * 0.6, fadeEnd,
        distance(GetCameraPositionWS(), rootPositionWS));
    // All generated leaf centerlines have y=height*UV.y. Short leaves respond
    // proportionally, instead of receiving the tall leaf's world-metre bend.
    // Capping the factor at one retains the existing 0.427 m bound at any scale.
    float windHeight = lerp(input.uv.y, input.windUV.x, saturate(_WgSeparateWindHeight));
    float leafHeightWS = max(input.positionOS.y, 0.0) * upScale / max(windHeight, 0.001);
    positionWS += WgFoliageWind(rootPositionWS, windHeight) * saturate(leafHeightWS / 0.46);
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = normalWS;
    output.uv = input.uv;
    output.color = input.color;
    float patchVariation = saturate(0.5 + 0.38 * sin(rootPositionWS.x * 0.28)
        * sin(rootPositionWS.z * 0.24 + 0.7));
    output.instanceVariation = lerp(WgFoliageSeed(rootPositionWS), patchVariation,
        saturate(_WgPatchColorStrength));
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

float4 WgFoliageMaskAndClip(float2 uv, float vertexAlpha)
{
    float4 mask = SAMPLE_TEXTURE2D(_WgMaskTex, sampler_WgMaskTex, uv);
    clip(saturate(mask.r * _WgFoliageColor.a * vertexAlpha) - _WgAlphaCutoff);
    return mask;
}

#endif
