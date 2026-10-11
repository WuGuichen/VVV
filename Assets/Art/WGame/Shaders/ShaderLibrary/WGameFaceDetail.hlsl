#ifndef WGAME_FACE_DETAIL_INCLUDED
#define WGAME_FACE_DETAIL_INCLUDED

#include "WGameShaderCommon.hlsl"

float WgFaceDetailValidity(float2 faceDetailUv)
{
    float2 lower = step(float2(0.0, 0.0), faceDetailUv);
    float2 upper = step(faceDetailUv, float2(1.0, 1.0));
    return lower.x * lower.y * upper.x * upper.y;
}

#endif
