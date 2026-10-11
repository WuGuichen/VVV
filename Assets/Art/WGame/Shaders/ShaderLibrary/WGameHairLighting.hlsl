#ifndef WGAME_HAIR_LIGHTING_INCLUDED
#define WGAME_HAIR_LIGHTING_INCLUDED

// xy = width/strength of primary; zw = width/strength of secondary.
float WgHairRootFactor(float rootProgress, float strength)
{
    return lerp(1.0 - strength, 1.0, smoothstep(0.0, 0.3, rootProgress));
}

float3 WgHairShadowTint(float3 baseColor, float3 normal, float3 lightDirection, float attenuation, float ao)
{
    // Reserve the cool shadow tint for pale neutral hair; colored/dark palettes
    // keep their hue. This is lighting, never a change to the baked base map.
    float high = max(baseColor.r, max(baseColor.g, baseColor.b));
    float low = min(baseColor.r, min(baseColor.g, baseColor.b));
    float neutral = saturate(1.0 - (high - low) * 4.0);
    float pale = smoothstep(0.4, 0.8, dot(baseColor, float3(0.2126, 0.7152, 0.0722))) * neutral;
    float lit = smoothstep(-0.15, 0.55, dot(normal, lightDirection)) * saturate(attenuation);
    float shadow = 1.0 - lit * lerp(0.4, 1.0, saturate(ao));
    return lerp(float3(1, 1, 1), float3(0.84, 0.82, 1.0), shadow * pale);
}

float WgHairLobe(float3 tangent, float3 normal, float3 halfVector, float shift, float width)
{
    float3 shifted = WgSafeNormalize(tangent + normal * shift, tangent);
    float tangentHalf = dot(shifted, halfVector);
    float sineSquared = saturate(1.0 - tangentHalf * tangentHalf);
    float power = max(2.0, 2.0 / max(0.01, width * width));
    return pow(sineSquared, power * 0.5) * smoothstep(-1.0, 0.0, tangentHalf);
}

float3 WgShadeHair(float3 baseColor, float3 normal, float3 tangent,
    float3 lightDirection, float3 viewDirection, float3 lightColor,
    float attenuation, float ao, float rootProgress, float4 lobes, float4 shape)
{
    float3 halfVector = WgSafeNormalize(lightDirection + viewDirection, normal);
    // A readable soft-edged band, with a deliberately subordinate second lobe.
    // Shared with Blender's baked_hair_preview.py; existing author parameters
    // remain the inputs, so older baked packages adopt the new default style.
    float primaryLobe = WgHairLobe(tangent, normal, halfVector, shape.x, lobes.x);
    float primary = lerp(primaryLobe, smoothstep(0.10, 0.90, primaryLobe), 0.35) * lobes.y * 2.0;
    float secondary = WgHairLobe(tangent, normal, halfVector, shape.y, lobes.z * 0.65) * lobes.w * 0.45;
    float root = WgHairRootFactor(rootProgress, shape.z);
    float facing = saturate(dot(normal, lightDirection) * 0.5 + 0.5);
    float3 specular = (lerp(baseColor, lightColor, 0.7) * primary
        + lerp(baseColor, lightColor, 0.2) * secondary) * facing;
    float rim = pow(1.0 - saturate(dot(normal, viewDirection)), 4.0)
        * saturate(dot(-lightDirection, viewDirection)) * shape.w;
    return (specular + baseColor * rim) * attenuation * lerp(0.35, 1.0, ao) * root;
}
#endif
