#ifndef WGAME_CHARACTER_TRADITIONAL_INPUT_INCLUDED
#define WGAME_CHARACTER_TRADITIONAL_INPUT_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _Color;
    float _AlphaClip;
    float _Cutoff;
    float4 _WgMagicCoreColor;
    float4 _WgHitFlashColor;
    float _WgHitFlashAmount;
    float4 _WgStatusTintColor;
    float _WgStatusTintAmount;
    float _WgDissolveProgress;
    float4 _WgDissolveEdgeColor;
    float _WgDissolveEdgeWidth;
    float4 _WgOutlineColor;
    float _WgOutlineAmount;
CBUFFER_END

#endif
