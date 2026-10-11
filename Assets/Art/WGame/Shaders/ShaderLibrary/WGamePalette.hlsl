#ifndef WGAME_PALETTE_INCLUDED
#define WGAME_PALETTE_INCLUDED

#include "WGameShaderCommon.hlsl"
#include "WGameSurfaceProfiles.generated.hlsl"

static const float WG_PALETTE_DIFFUSE_SHOULDER_START = 0.74;
static const float WG_PALETTE_DIFFUSE_SHOULDER_RANGE = 0.24;

struct WgPaletteVertexData
{
    float ao;
    float rim;
    float emission;
    float surfaceProfile;
};

float2 WgColorPaletteUvFromIndex(float colorIndex, float4 paletteParams)
{
    float columnCount = max(1.0, floor(paletteParams.x + 0.5));
    float rowCount = max(1.0, floor(paletteParams.y + 0.5));
    float inverseColumnCount = paletteParams.z > WG_EPSILON ? paletteParams.z : rcp(columnCount);
    float inverseRowCount = paletteParams.w > WG_EPSILON ? paletteParams.w : rcp(rowCount);
    float maximumIndex = columnCount * rowCount - 1.0;
    float index = clamp(floor(colorIndex + 0.5), 0.0, maximumIndex);
    float row = floor(index * inverseColumnCount);
    float column = index - row * columnCount;
    return float2((column + 0.5) * inverseColumnCount, (row + 0.5) * inverseRowCount);
}

float WgColorPaletteIndex(float2 paletteUv, float4 paletteParams)
{
    float columnCount = max(1.0, floor(paletteParams.x + 0.5));
    float rowCount = max(1.0, floor(paletteParams.y + 0.5));
    float column = min(columnCount - 1.0, floor(saturate(paletteUv.x) * columnCount));
    float row = min(rowCount - 1.0, floor(saturate(paletteUv.y) * rowCount));
    return row * columnCount + column;
}

float WgColorPaletteUvIsValid(float2 paletteUv)
{
    return step(0.0, paletteUv.x)
        * step(0.0, paletteUv.y)
        * step(paletteUv.x, 1.0)
        * step(paletteUv.y, 1.0);
}

float WgColorIndexMask(float colorIndex, float expectedColorIndex)
{
    return 1.0 - saturate(abs(colorIndex - expectedColorIndex));
}

float WgDecodeSurfaceProfile(float vertexAlpha)
{
    return min(WG_SURFACE_PROFILE_COUNT - 1.0, floor(saturate(vertexAlpha) * WG_SURFACE_PROFILE_COUNT));
}

WgPaletteVertexData WgDecodePaletteVertexData(float4 vertexColor)
{
    WgPaletteVertexData result;
    result.ao = saturate(vertexColor.r);
    result.rim = saturate(vertexColor.g);
    result.emission = saturate(vertexColor.b);
    result.surfaceProfile = WgDecodeSurfaceProfile(vertexColor.a);
    return result;
}

float WgSurfaceFamilyMask(float surfaceProfile, float expectedFamily)
{
    return 1.0 - saturate(abs(WgSurfaceProfileFamily(surfaceProfile) - expectedFamily));
}

float WgSurfaceFinishMask(float surfaceProfile, float expectedFinish)
{
    return 1.0 - saturate(abs(WgSurfaceProfileFinish(surfaceProfile) - expectedFinish));
}

float3 WgLiftPaletteValue(float3 color, float minimumValue)
{
    float peak = max(color.r, max(color.g, color.b));
    float scale = max(1.0, max(0.0, minimumValue) * WgSafeRcp(peak));
    return max(color, 0.0) * scale;
}

float3 WgCompressPaletteDiffuse(float3 color)
{
    float3 safeColor = max(color, 0.0);
    float peak = max(safeColor.r, max(safeColor.g, safeColor.b));
    float shoulder = max(0.0, peak - WG_PALETTE_DIFFUSE_SHOULDER_START);
    float compressedPeak = min(peak, WG_PALETTE_DIFFUSE_SHOULDER_START)
        + shoulder * WG_PALETTE_DIFFUSE_SHOULDER_RANGE * WgSafeRcp(shoulder + WG_PALETTE_DIFFUSE_SHOULDER_RANGE);
    return safeColor * compressedPeak * WgSafeRcp(peak);
}

float3 WgEvaluateReadablePaletteThreeBand(
    float3 paletteColor,
    float surfaceProfile,
    float wrappedLight,
    float attenuation,
    float4 lightingParams,
    float4 valueParams,
    float3 shadowTint,
    float castShadowStrength,
    out float3 readableColor,
    out float shadowWeight)
{
    float shadowThreshold = saturate(lightingParams.y);
    float lightThreshold = max(shadowThreshold + 0.001, saturate(lightingParams.z));
    float softness = max(0.001, lightingParams.w);
    float formLight = saturate(wrappedLight);
    float midBlend = smoothstep(shadowThreshold - softness, shadowThreshold + softness, formLight);
    float lightBlend = smoothstep(lightThreshold - softness, lightThreshold + softness, formLight);
    float minimumValue = WgSurfaceProfileShadowFloor(surfaceProfile);
    readableColor = WgCompressPaletteDiffuse(WgLiftPaletteValue(paletteColor, minimumValue));
    float3 shadowColor = readableColor * max(shadowTint, 0.0) * max(0.0, valueParams.x);
    float3 midColor = readableColor * max(valueParams.y, 0.0);
    float3 lightColor = readableColor * max(valueParams.z, 0.0);
    float3 formColor = WgCompressPaletteDiffuse(lerp(lerp(shadowColor, midColor, midBlend), lightColor, lightBlend));

    // Delicate semi-realistic penumbra warmth for skin (Family 0)
    float isSkin = WgSurfaceFamilyMask(surfaceProfile, 0.0);
    float penumbra = smoothstep(shadowThreshold - softness * 1.5, shadowThreshold, formLight)
        * (1.0 - smoothstep(shadowThreshold, shadowThreshold + softness * 2.0, formLight));
    float3 bloodSheen = readableColor * float3(0.28, 0.10, 0.06) * (penumbra * 0.16 * isSkin);
    formColor += bloodSheen;

    float castShadowWeight = (1.0 - saturate(attenuation)) * saturate(castShadowStrength);
    shadowWeight = saturate((1.0 - midBlend) + castShadowWeight * midBlend);
    return lerp(formColor, shadowColor, castShadowWeight);
}

float3 WgEvaluatePaletteAdditionalLight(
    float3 readablePaletteColor,
    float surfaceProfile,
    float3 normalWS,
    float3 lightDirectionWS,
    float3 viewDirectionWS,
    float3 lightColor,
    float wrappedLight,
    float distanceAttenuation,
    float shadowAttenuation,
    float4 lightingParams,
    float4 additionalLightParams)
{
    float softness = max(max(0.001, lightingParams.w), max(0.001, additionalLightParams.y));
    float threshold = saturate(lightingParams.y);
    float toonBand = smoothstep(threshold - softness, threshold + softness, saturate(wrappedLight));
    toonBand = lerp(saturate(additionalLightParams.z), 1.0, toonBand);
    float attenuation = min(max(0.0, distanceAttenuation), max(0.0, additionalLightParams.w)) * saturate(shadowAttenuation);
    float energy = max(0.0, additionalLightParams.x) * attenuation;
    float3 safeLightColor = min(max(lightColor, 0.0), 2.0);
    float3 color = readablePaletteColor * safeLightColor * toonBand * energy;
    float3 halfDirectionWS = WgSafeNormalize(lightDirectionWS + viewDirectionWS, normalWS);
    float normalHalf = saturate(dot(normalWS, halfDirectionWS));
    float specular = pow(normalHalf, max(1.0, WgSurfaceProfileSpecularPower(surfaceProfile)))
        * max(0.0, WgSurfaceProfileSpecular(surfaceProfile));
    float metallic = saturate(WgSurfaceProfileMetallic(surfaceProfile));
    float crystal = WgSurfaceFamilyMask(surfaceProfile, 7.0);
    float3 specularColor = lerp(readablePaletteColor, float3(1.0, 0.97, 0.90), saturate(metallic * 0.8 + crystal * 0.65));
    return color + specularColor * safeLightColor * specular * energy;
}

float3 WgApplyPaletteMaterialResponse(
    float3 color,
    float3 paletteColor,
    float surfaceProfile,
    float3 normalWS,
    float3 lightDirectionWS,
    float3 viewDirectionWS,
    float rimWeight,
    float proceduralSpecularMultiplier,
    float rimPower,
    float3 lightColor = float3(1.0, 1.0, 1.0),
    float lightVisibility = 1.0)
{
    // Preserve authored lobe shapes; visibility only attenuates sun-driven terms.
    // Keep the incoming diffuse/GI and the character's studio reflections separate.
    float sunVisibility = saturate(lightVisibility);
    float directHighlight = sunVisibility;
    float3 halfDirectionWS = WgSafeNormalize(lightDirectionWS + viewDirectionWS, normalWS);
    float normalHalf = saturate(dot(normalWS, halfDirectionWS));
    float normalView = saturate(dot(normalWS, viewDirectionWS));
    float fresnel = 1.0 - normalView;
    float specularStrength = max(0.0, WgSurfaceProfileSpecular(surfaceProfile)) * max(0.0, proceduralSpecularMultiplier);
    float specularPower = max(1.0, WgSurfaceProfileSpecularPower(surfaceProfile));

    float isHair = WgSurfaceFamilyMask(surfaceProfile, 1.0);
    float metallic = saturate(WgSurfaceProfileMetallic(surfaceProfile));
    float crystalMask = WgSurfaceFamilyMask(surfaceProfile, 7.0);
    float3 metalSpecularColor = lerp(paletteColor, lightColor, 0.34) * lightColor;
    float3 crystalSpecularColor = lerp(
        paletteColor,
        max(lightColor, 0.35),
        0.38);

    UNITY_BRANCH
    if (isHair > 0.5)
    {
        // Soft sheen hair (Profile 03) vs matte hair (Profile 02)
        float isSheenHair = WgSurfaceFinishMask(surfaceProfile, 1.0);
        float3 hairTangent = WgSafeNormalize(cross(normalWS, float3(0.0, 1.0, 0.0)), float3(1.0, 0.0, 0.0));
        float dotTH = dot(hairTangent, halfDirectionWS);
        float sinTH = sqrt(max(0.0, 1.0 - dotTH * dotTH));

        // Soft, diffused hair luster tinted by light color
        float hairAniso = pow(sinTH, max(8.0, specularPower * 1.1)) * specularStrength * lerp(0.35, 0.75, isSheenHair);
        float3 hairSpecularColor = lerp(paletteColor, lightColor, 0.32);
        color += hairSpecularColor * hairAniso * directHighlight;
    }
    else
    {
        float isMetal = WgSurfaceFamilyMask(surfaceProfile, 6.0);
        float isCrystal = crystalMask;

        // Clean, sharp PBR specular tinted by light color
        float specular = pow(normalHalf, specularPower) * specularStrength;
        float3 specColor = lerp(lightColor, metalSpecularColor, isMetal);
        specColor = lerp(specColor, crystalSpecularColor, isCrystal);

        // Metal specular concentration: sharp physical energy focus on polished surfaces
        float metalSpecularBoost = isMetal * (specular * lerp(1.2, 3.2, metallic));

        // Reuse the primary lobe for the crystal glint instead of evaluating a
        // second, extremely narrow pow() that is usually invisible on small gems.
        float3 crystalSharpGlint = isCrystal * specular * 0.72 * crystalSpecularColor;

        color += (specColor * (specular + metalSpecularBoost) + crystalSharpGlint) * directHighlight;
    }

    // Backlight directional gating: only allow subtle rim light on true backlit silhouettes
    float backLightGating = saturate(dot(-lightDirectionWS, normalWS) * 0.5 + 0.5);
    float rimFactor = pow(fresnel, max(2.0, rimPower)) * pow(backLightGating, 2.0);
    float rimStrength = max(0.0, WgSurfaceProfileRim(surfaceProfile));
    color += paletteColor * max(lightColor, 0.0) * rimFactor * rimStrength * saturate(rimWeight) * 0.35 * sunVisibility;

    // Hair subtle sheen (only when backlit)
    float hairSheen = isHair * pow(fresnel, 4.0) * rimStrength * backLightGating * 0.25;
    color += paletteColor * max(lightColor, 0.0) * hairSheen * sunVisibility;

    // Leather subtle tight highlight tinted by light color
    float leatherTight = WgSurfaceFamilyMask(surfaceProfile, 3.0)
        * pow(normalHalf, max(24.0, specularPower * 1.8)) * specularStrength * 0.40;
    color += lerp(paletteColor, lightColor, 0.40) * leatherTight * directHighlight;
    return color;
}

float3 WgApplyPaletteCharacterMaterialResponse(
    float3 color,
    float3 paletteColor,
    float surfaceProfile,
    float3 normalWS,
    float3 lightDirectionWS,
    float3 viewDirectionWS,
    float rimWeight,
    float proceduralSpecularMultiplier,
    float rimPower,
    float3 lightColor = float3(1.0, 1.0, 1.0),
    float lightVisibility = 1.0)
{
    float3 response = WgApplyPaletteMaterialResponse(
        color, paletteColor, surfaceProfile, normalWS, lightDirectionWS,
        viewDirectionWS, rimWeight, proceduralSpecularMultiplier, rimPower,
        lightColor, lightVisibility);

    float sunVisibility = saturate(lightVisibility);
    float directHighlight = sunVisibility;
    float isMetal = WgSurfaceFamilyMask(surfaceProfile, 6.0);
    float isCrystal = WgSurfaceFamilyMask(surfaceProfile, 7.0);
    UNITY_BRANCH
    if (isMetal + isCrystal > 0.5)
    {
        float3 halfDirectionWS = WgSafeNormalize(lightDirectionWS + viewDirectionWS, normalWS);
        float normalHalf = saturate(dot(normalWS, halfDirectionWS));
        float normalView = saturate(dot(normalWS, viewDirectionWS));
        float fresnel = 1.0 - normalView;

        // Fixed powers use multiplication so the broad readability lobes add no
        // additional transcendental pow() cost.
        float normalHalf2 = normalHalf * normalHalf;
        float normalHalf4 = normalHalf2 * normalHalf2;
        float normalHalf8 = normalHalf4 * normalHalf4;
        float normalHalf16 = normalHalf8 * normalHalf8;
        float normalHalf32 = normalHalf16 * normalHalf16;
        float normalView2 = normalView * normalView;
        float fresnel2 = fresnel * fresnel;
        float fresnel4 = fresnel2 * fresnel2;

        float specularStrength = max(0.0, WgSurfaceProfileSpecular(surfaceProfile))
            * max(0.0, proceduralSpecularMultiplier);
        float roughness = saturate(WgSurfaceProfileRoughness(surfaceProfile));
        float metallic = saturate(WgSurfaceProfileMetallic(surfaceProfile));

        // Build two view-space studio-light bands analytically. Faceted ornaments
        // receive bright and dark faces without any reflection lookup texture.
        float3 reflectionWS = reflect(-viewDirectionWS, normalWS);
        float reflectionHemisphere = saturate(reflectionWS.y * 0.5 + 0.5);
        float3 normalVS = TransformWorldToViewDir(normalWS, true);
        float sideBand = saturate(1.0 - abs(normalVS.x - 0.42) * 2.6);
        sideBand *= sideBand;
        float topBand = saturate(normalVS.y * 0.72 + 0.38);
        topBand *= topBand;
        float studioBand = saturate(sideBand * 0.78 + topBand * 0.42);
        float studioSpark = studioBand * studioBand;
        float studioSparkTight = studioSpark * studioSpark;

        // Restore darker metal faces with the authored hue and broad reflections.
        float metalBroad = (normalHalf4 * 0.82 + normalHalf16 * 0.48)
            * specularStrength * lerp(1.0, 0.72, roughness);
        float metalEnvironment = (studioBand * 1.18
            + reflectionHemisphere * 0.14) * lerp(0.55, 1.0, metallic);
        float metalEdge = (fresnel2 * 0.42 + fresnel4 * 0.24) * specularStrength;
        float3 brightMetal = WgLiftPaletteValue(paletteColor, 0.82);
        float3 metalHighlightColor = lerp(brightMetal, max(lightColor, 0.72), 0.15);
        float metalSpark = normalHalf32 * specularStrength * 1.25 * directHighlight + studioSparkTight * 0.26;
        response += metalHighlightColor * (metalBroad * directHighlight + metalEnvironment + metalEdge) * isMetal;

        // Crystal bodies retain the authored hue; reflected facets use the light color.
        float3 brightCrystal = WgLiftPaletteValue(paletteColor, 0.82);
        float3 crystalReflection = max(lightColor, 0.35);
        float crystalBroad = (normalHalf4 * 0.72 + normalHalf16 * 0.54)
            * specularStrength;
        float crystalFacet = studioBand * 0.82 + reflectionHemisphere * 0.10;
        float crystalInterior = 0.26 + normalView2 * 0.26;
        float crystalEdge = fresnel2 * 0.34 + fresnel4 * 0.22;
        float3 crystalHighlightColor = lerp(brightCrystal, crystalReflection, 0.60);
        float crystalSpark = normalHalf32 * specularStrength * 0.72 * directHighlight + studioSparkTight * 0.16;
        response += (brightCrystal * (crystalInterior + crystalEdge)
            + crystalHighlightColor * (crystalBroad * directHighlight + crystalFacet)) * isCrystal;

        // Rebuild the reflected body from value and authored hue before adding
        // small white sparkles. This prevents HDR reflection sums from clipping
        // every color channel into a pale beige or white surface.
        float metalValue = max(response.r, max(response.g, response.b));
        float3 metalHue = brightMetal * WgSafeRcp(max(
            brightMetal.r,
            max(brightMetal.g, brightMetal.b)));
        response = lerp(
            response,
            metalHue * metalValue,
            isMetal * 0.90);

        float crystalValue = max(response.r, max(response.g, response.b));
        float3 crystalHue = brightCrystal * WgSafeRcp(max(
            brightCrystal.r,
            max(brightCrystal.g, brightCrystal.b)));
        response = lerp(
            response,
            crystalHue * crystalValue,
            isCrystal * 0.78);

        response += lightColor * (metalSpark * isMetal + crystalSpark * isCrystal);
    }

    float isFabric = WgSurfaceFamilyMask(surfaceProfile, 2.0);
    float isMatteSolid = WgSurfaceFamilyMask(surfaceProfile, 5.0);
    UNITY_BRANCH
    if (isFabric + isMatteSolid > 0.5)
    {
        float smoothFinish = WgSurfaceFinishMask(surfaceProfile, 1.0);
        float isWoven = isFabric * (1.0 - smoothFinish);
        float isSilk = isFabric * smoothFinish;
        float isRoughMatte = isMatteSolid * (1.0 - smoothFinish);

        float normalLight = saturate(dot(normalWS, lightDirectionWS));
        float normalView = saturate(dot(normalWS, viewDirectionWS));
        float fresnel = 1.0 - normalView;
        float fresnel2 = fresnel * fresnel;
        float fresnel4 = fresnel2 * fresnel2;
        float3 halfDirectionWS = WgSafeNormalize(lightDirectionWS + viewDirectionWS, normalWS);
        float normalHalf = saturate(dot(normalWS, halfDirectionWS));
        float normalHalf2 = normalHalf * normalHalf;
        float normalHalf4 = normalHalf2 * normalHalf2;
        float normalHalf8 = normalHalf4 * normalHalf4;
        float normalHalf16 = normalHalf8 * normalHalf8;
        float upward = saturate(normalWS.y * 0.5 + 0.5);
        float backLight = saturate(dot(-lightDirectionWS, normalWS) * 0.5 + 0.5);

        float paletteLuminance = WgLuminance(max(paletteColor, 0.0));
        float lightCloth = smoothstep(0.38, 0.62, paletteLuminance);
        float darkCloth = 1.0 - lightCloth;

        // White woven cloth receives cool fold shadows and a restrained ivory
        // lift on upward-facing planes instead of reading as a flat white slab.
        float whiteFoldShadow = saturate(
            (1.0 - normalLight) * 0.55
            + (1.0 - upward) * 0.35
            + (1.0 - normalView) * 0.10);
        float3 whiteClothShadowTint = float3(0.76, 0.83, 0.93);
        float3 whiteClothLightTint = float3(1.08, 1.02, 0.92);
        float3 whiteClothTone = lerp(
            whiteClothLightTint,
            whiteClothShadowTint,
            smoothstep(0.12, 0.72, whiteFoldShadow));
        float whiteWovenMask = isWoven * lightCloth;
        response = lerp(response, response * whiteClothTone, whiteWovenMask * 0.58);
        float whiteValue = max(response.r, max(response.g, response.b));
        float whiteValueCeiling = lerp(
            0.96,
            0.42,
            smoothstep(0.15, 0.85, whiteFoldShadow));
        float3 whiteClothHue = paletteColor * whiteClothTone;
        whiteClothHue *= WgSafeRcp(max(
            whiteClothHue.r,
            max(whiteClothHue.g, whiteClothHue.b)));
        response = lerp(
            response,
            whiteClothHue * min(whiteValue, whiteValueCeiling),
            whiteWovenMask * 0.70);

        // Dark woven and matte surfaces retain their authored hue while gaining
        // a low charcoal/indigo floor that exposes the existing garment planes.
        float darkReadabilityFloor = lerp(0.045, 0.075, isFabric);
        float3 readableDarkCloth = WgLiftPaletteValue(paletteColor, darkReadabilityFloor);
        float darkForm = 0.50 + normalLight * 0.22 + upward * 0.10 + normalView * 0.08;
        float darkSurfaceMask = saturate((isFabric + isRoughMatte) * darkCloth);
        response = lerp(
            response,
            max(response, readableDarkCloth * darkForm),
            darkSurfaceMask * 0.45);
        float darkValue = max(response.r, max(response.g, response.b));
        float3 darkHue = readableDarkCloth * WgSafeRcp(max(
            readableDarkCloth.r,
            max(readableDarkCloth.g, readableDarkCloth.b)));
        float darkBlueChroma = saturate(
            (paletteColor.b - max(paletteColor.r, paletteColor.g)) * 10.0);
        darkHue *= lerp(
            float3(1.0, 1.0, 1.0),
            float3(0.72, 0.90, 1.28),
            darkBlueChroma);
        darkHue *= WgSafeRcp(max(darkHue.r, max(darkHue.g, darkHue.b)));
        // Keep the authored dark hue without capping the evaluated lighting.
        // A fixed albedo-derived ceiling suppressed even valid sky/GI fill.
        response = lerp(
            response,
            darkHue * darkValue,
            darkSurfaceMask * 0.94);

        // Textile highlights stay broad and colored. Woven cloth uses a soft
        // ridge plus fuzz edge, silk uses a narrower flowing band, and rough
        // matte cloth receives only enough lift to avoid featureless black.
        float wovenSheen = normalHalf4 * 0.085 * directHighlight
            + fresnel2 * (0.055 + backLight * 0.085) * sunVisibility;
        float silkSheen = (normalHalf8 * 0.18 + normalHalf16 * 0.12) * directHighlight
            + fresnel2 * (0.08 + backLight * 0.12) * sunVisibility;
        float matteSheen = normalHalf2 * 0.018 * directHighlight + fresnel4 * 0.035 * sunVisibility;
        float sheenAmount = wovenSheen * isWoven
            + silkSheen * isSilk
            + matteSheen * isRoughMatte;
        float3 liftedClothHue = WgLiftPaletteValue(
            paletteColor,
            lerp(0.18, 0.48, lightCloth));
        float3 clothSheenColor = lerp(
            liftedClothHue,
            max(lightColor, 0.35),
            lerp(0.14, 0.28, lightCloth));
        response += clothSheenColor * sheenAmount * max(0.0, proceduralSpecularMultiplier);
    }

    return response;
}

float3 WgApplyPaletteEdgeLighting(
    float3 color,
    float3 paletteColor,
    float surfaceProfile,
    float3 normalWS,
    float3 viewDirectionWS,
    float rimWeight,
    float3 warmTint,
    float3 coolTint,
    float4 edgeParams)
{
    float normalView = saturate(dot(normalWS, viewDirectionWS));
    float edge = pow(1.0 - normalView, max(0.25, edgeParams.y));
    float3 normalVS = TransformWorldToViewDir(normalWS, true);
    float coolSide = smoothstep(-max(0.01, edgeParams.z), max(0.01, edgeParams.z), normalVS.x);
    float3 edgeTint = lerp(max(warmTint, 0.0), max(coolTint, 0.0), coolSide);
    edgeTint *= lerp(float3(1.0, 1.0, 1.0), saturate(paletteColor * 2.0), saturate(edgeParams.w));
    float profileEdge = lerp(0.55, 1.0, saturate(WgSurfaceProfileRim(surfaceProfile) * 2.2));
    float authoredEdge = lerp(0.50, 1.0, saturate(rimWeight));
    return color + edgeTint * edge * profileEdge * authoredEdge * max(0.0, edgeParams.x);
}

#endif
