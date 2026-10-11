#ifndef WGAME_CHARACTER_PALETTE_INPUT_INCLUDED
#define WGAME_CHARACTER_PALETTE_INPUT_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float _WgPaletteEnabled;
    float4 _WgColorPaletteParams;
    float4 _WgPaletteLightingParams;
    float4 _WgPaletteValueParams;
    float4 _WgPaletteShadowTint;
    float4 _WgPaletteIndirectParams;
    float _WgPaletteIndirectNormalInfluence;
    float4 _WgPaletteAdditionalLightParams;
    float4 _WgPaletteSkyTint;
    float4 _WgPaletteGroundTint;
    float4 _WgPaletteEdgeWarmTint;
    float4 _WgPaletteEdgeCoolTint;
    float4 _WgPaletteEdgeParams;
    float4 _WgPaletteDetailParams;
    float _WgPaletteRimPower;
    float _WgFaceDetailEnabled;
    float _WgFaceDetailIntensity;
    float4 _WgHitFlashColor;
    float _WgHitFlashAmount;
    float4 _WgStatusTintColor;
    float _WgStatusTintAmount;
    float _WgDissolveProgress;
    float _WgDissolveNoiseMode;
    float4 _WgDissolveEdgeColor;
    float _WgDissolveEdgeWidth;
    float4 _WgOutlineColor;
    float _WgOutlineAmount;
CBUFFER_END

#endif
