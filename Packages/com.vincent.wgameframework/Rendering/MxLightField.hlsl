#ifndef MX_LIGHT_FIELD_INCLUDED
#define MX_LIGHT_FIELD_INCLUDED

struct MxLightProbe { float4 positionValid, skyUp, skyEquator, skyGround; };
// First angular moments of incident radiance, normalized by 4*pi.
struct MxLightCoefficients { float4 r, g, b; };
StructuredBuffer<MxLightProbe> _MxLightingProbes;
StructuredBuffer<MxLightCoefficients> _MxLightingCoefficients;
StructuredBuffer<float2> _MxLightingVisibility;
StructuredBuffer<int> _MxLightingCellBricks;
StructuredBuffer<uint> _MxLightingBrickIndices;
StructuredBuffer<float> _MxLightingClearDistances;
float4 _MxLightingGridOrigin, _MxLightingGridSize;

struct MxProbeStencil { int3 lower; float3 fraction, fine; int brick; float blend; };
MxProbeStencil MxPrepareProbeStencil(float3 grid)
{
    MxProbeStencil s;
    s.lower=min((int3)floor(grid),(int3)_MxLightingGridSize.xyz-2);
    s.fraction=grid-s.lower;s.brick=-1;s.fine=0;s.blend=0;
    if(_MxLightingGridSize.w>0.5)
    {
        int3 dimensions=(int3)_MxLightingGridSize.xyz-1;
        uint cell=s.lower.x+dimensions.x*(s.lower.y+dimensions.y*s.lower.z);
        s.brick=_MxLightingCellBricks[cell];
        if(s.brick>=0)
        {
            float3 boundary=min(s.fraction,1-s.fraction);
            s.blend=smoothstep(0,0.25,min(boundary.x,min(boundary.y,boundary.z)));
            s.fine=s.fraction*2;
        }
    }
    return s;
}
uint MxProbeStencilIndex(MxProbeStencil s,uint level,uint corner,out float weight)
{
    int3 bits=int3(corner&1,(corner>>1)&1,(corner>>2)&1);
    int3 cell;float3 fraction;uint id;
    if(level==0)
    {
        cell=s.lower+bits;fraction=s.fraction;
        id=cell.x+(uint)_MxLightingGridSize.x*(cell.y+(uint)_MxLightingGridSize.y*cell.z);
    }
    else
    {
        int3 lower=min((int3)floor(s.fine),1);
        cell=lower+bits;fraction=s.fine-lower;
        id=_MxLightingBrickIndices[s.brick*27+cell.x+3*(cell.y+3*cell.z)];
    }
    float3 tri=lerp(1-fraction,fraction,(float3)bits);
    weight=tri.x*tri.y*tri.z*(level==0 ? 1-s.blend : s.blend);
    return id;
}

uint MxProbeVisibilityCell(float3 direction)
{
    float3 a = abs(direction); uint face; float2 uv;
    if (a.x >= a.y && a.x >= a.z)
    { face = direction.x >= 0 ? 0 : 1; uv = float2(direction.x >= 0 ? -direction.z : direction.z, direction.y) / max(a.x, 1e-5); }
    else if (a.y >= a.z)
    { face = direction.y >= 0 ? 2 : 3; uv = float2(direction.x, direction.y >= 0 ? -direction.z : direction.z) / max(a.y, 1e-5); }
    else
    { face = direction.z >= 0 ? 4 : 5; uv = float2(direction.z >= 0 ? direction.x : -direction.x, direction.y) / max(a.z, 1e-5); }
    uint2 cell = min((uint2)((uv * 0.5 + 0.5) * 4), 3u);
    return face * 16 + cell.y * 4 + cell.x;
}

float MxProbeVisibility(float2 moments, float distanceToProbe)
{
    float variance = max(moments.y - moments.x * moments.x, 0.0001);
    float excess = max(0, distanceToProbe - moments.x - 0.03);
    float visibility = variance / (variance + excess * excess);
    return visibility * visibility * visibility;
}

float MxProbeVisibilityForId(uint id,float3 delta,float distanceToProbe)
{
    uint bin=id*96+MxProbeVisibilityCell(delta);
    // Surface support is constrained separately by the receiver tangent plane and
    // the nearest-distance clear-path test. A bin contains a cone of rays, so its
    // minimum is not a blocker on every ray in that cone: globally clipping at
    // that distance produces angular black islands even in unobstructed support.
    return MxProbeVisibility(_MxLightingVisibility[bin],distanceToProbe);
}

float MxProbeMediumVisibilityForId(uint id,float3 delta,float distanceToProbe)
{
    uint bin=id*96+MxProbeVisibilityCell(delta);
    float visibility=MxProbeVisibilityForId(id,delta,distanceToProbe);
    if(_MxLightingGridSize.w>0.5)
    {
        // v2 persists the nearest captured sample in each bin. Reject support behind it;
        // distant sky must not make a mixed wall/sky bin transparent after normalization.
        float clearDistance=_MxLightingClearDistances[bin];
        visibility*=1-smoothstep(clearDistance+0.025,clearDistance+0.1,distanceToProbe);
    }
    return visibility;
}

// Media have no receiving surface normal or tangent plane. Reuse spatial visibility,
// but do not apply surface-facing weights or the surface's capsule AO response.
float3 MxSampleMediumAmbient(float3 positionWS, float3 toEye, float anisotropy)
{
    float3 grid = (positionWS - _MxLightingGridOrigin.xyz) * _MxLightingGridOrigin.w;
    if (any(grid < 0) || any(grid > _MxLightingGridSize.xyz - 1)) return 0;
    MxProbeStencil stencil=MxPrepareProbeStencil(grid);
    float3 sum = 0; float total = 0;
    for(uint level=0;level<2;level++)
    {
    if((level==0 ? 1-stencil.blend : stencil.blend)<=0)continue;
    for (uint corner = 0; corner < 8; corner++)
    {
        float spatialWeight;uint id=MxProbeStencilIndex(stencil,level,corner,spatialWeight);
        MxLightProbe probe = _MxLightingProbes[id];
        if (probe.positionValid.w < 0.5) continue;
        float3 delta = positionWS - probe.positionValid.xyz;
        float weight=spatialWeight*MxProbeMediumVisibilityForId(id,delta,length(delta));
        MxLightCoefficients c = _MxLightingCoefficients[id];
        // L1 phase convolution. Direction points towards the incident source, so
        // forward scattering uses -toEye. Higher moments are absent in this cache.
        float4 basis = float4(-3 * clamp(anisotropy, -0.85, 0.85) * toEye, 1);
        sum += max(0, float3(dot(c.r, basis), dot(c.g, basis), dot(c.b, basis))) * weight;
        total += weight;
    }
    }
    return total > 1e-12 ? sum / total : 0;
}

#endif
