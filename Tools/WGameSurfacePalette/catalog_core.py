"""Deterministic WGame RGB565 Color Palette v3 and Surface Profile primitives."""

from __future__ import annotations

import base64
import copy
import hashlib
import json
import math
from pathlib import Path
from typing import Any


FORMAT = "mx.wgameSurfacePaletteCatalog.v3"
SCHEMA_VERSION = "3.0"
GENERATOR_ID = "mx.wgameRgb565ColorPaletteGenerator.v3"
WIDTH = 256
HEIGHT = 256
COLOR_COUNT = WIDTH * HEIGHT
SURFACE_PROFILE_COUNT = 16
LOGICAL_TOKEN_COUNT = COLOR_COUNT * SURFACE_PROFILE_COUNT
RED_BITS = 5
GREEN_BITS = 6
BLUE_BITS = 5
RED_LEVELS = 1 << RED_BITS
GREEN_LEVELS = 1 << GREEN_BITS
BLUE_LEVELS = 1 << BLUE_BITS
INVALID_PALETTE_UV = (-1.0, -1.0)
RGB_ENCODING = "base64-rgb8-row-major-bottom-left"


DETAIL_MODES = {
    "clean": 0,
    "woven": 1,
    "silk": 2,
    "leather": 3,
    "wood": 4,
    "matte": 5,
    "brushed": 6,
    "frost": 7,
    "crystal": 8,
}


def _profile(
    profile_id: str,
    display_name: str,
    family: int,
    finish: int,
    shadow: float,
    wrap: float,
    ao: float,
    specular: float,
    power: float,
    rim: float,
    roughness: float,
    metallic: float,
    detail_mode: str,
    detail_scale: float,
    detail_diffuse: float,
    detail_specular: float,
) -> dict[str, Any]:
    index = len(SURFACE_PROFILES)
    return {
        "index": index,
        "profileId": profile_id,
        "displayName": display_name,
        "family": family,
        "finish": finish,
        "alphaByte": 8 + index * 16,
        "shadowFloor": shadow,
        "wrap": wrap,
        "ao": ao,
        "specular": specular,
        "specularPower": power,
        "rim": rim,
        "roughness": roughness,
        "metallic": metallic,
        "detailMode": detail_mode,
        "detailModeIndex": DETAIL_MODES[detail_mode],
        "detailScale": detail_scale,
        "detailDiffuse": detail_diffuse,
        "detailSpecular": detail_specular,
    }


SURFACE_PROFILES: list[dict[str, Any]] = []
SURFACE_PROFILES.extend([
    _profile("skin_soft", "Skin Soft", 0, 0, .130, .48, .30, .040, 18, .10, .68, 0.0, "clean", 0, 0, 0),
    _profile("skin_glossy", "Skin Glossy", 0, 1, .130, .46, .28, .110, 32, .14, .42, 0.0, "clean", 0, 0, .04),
    _profile("hair_matte", "Hair Matte", 1, 0, .075, .36, .48, .060, 20, .22, .62, 0.0, "clean", 0, 0, .03),
    _profile("hair_sheen", "Hair Sheen", 1, 1, .075, .34, .44, .180, 44, .30, .36, 0.0, "clean", 0, 0, .10),
    _profile("fabric_woven", "Fabric Woven", 2, 0, .045, .32, .65, .010, 8, .14, .90, 0.0, "woven", 48, .035, .02),
    _profile("fabric_silk", "Fabric Silk", 2, 1, .055, .30, .52, .140, 38, .26, .38, 0.0, "silk", 72, .015, .10),
    _profile("leather_rough", "Leather Rough", 3, 0, .040, .24, .72, .060, 14, .20, .72, 0.0, "leather", 14, .050, .04),
    _profile("leather_polished", "Leather Polished", 3, 1, .050, .22, .64, .180, 36, .28, .38, 0.0, "leather", 18, .028, .12),
    _profile("wood_raw", "Wood Raw", 4, 0, .050, .22, .70, .030, 10, .14, .82, 0.0, "wood", 12, .055, .02),
    _profile("wood_varnished", "Wood Varnished", 4, 1, .055, .20, .60, .150, 32, .22, .32, 0.0, "wood", 16, .032, .10),
    _profile("matte_solid_rough", "Matte Solid Rough", 5, 0, .050, .22, .62, .015, 8, .10, .94, 0.0, "matte", 30, .025, .01),
    _profile("matte_solid_smooth", "Matte Solid Smooth", 5, 1, .060, .20, .52, .080, 26, .16, .48, 0.0, "matte", 38, .012, .06),
    _profile("metal_brushed", "Metal Brushed", 6, 0, .055, .12, .55, .280, 34, .34, .42, .78, "brushed", 72, 0, .15),
    _profile("metal_polished", "Metal Polished", 6, 1, .065, .10, .42, .480, 64, .46, .16, .95, "clean", 0, 0, .10),
    _profile("crystal_clear", "Crystal Clear", 7, 0, .080, .16, .35, .580, 72, .52, .18, 0.0, "crystal", 0, .008, .12),
    _profile("crystal_frosted", "Crystal Frosted", 7, 1, .075, .20, .46, .180, 22, .38, .52, 0.0, "frost", 46, .030, .16),
])
for _profile_index, _profile_value in enumerate(SURFACE_PROFILES):
    _profile_value["index"] = _profile_index
    _profile_value["alphaByte"] = 8 + _profile_index * 16


def _srgb_to_linear(value: float) -> float:
    if value <= 0.04045:
        return value / 12.92
    return ((value + 0.055) / 1.055) ** 2.4


def srgb8_to_oklch(red: int, green: int, blue: int) -> dict[str, float]:
    r = _srgb_to_linear(red / 255.0)
    g = _srgb_to_linear(green / 255.0)
    b = _srgb_to_linear(blue / 255.0)
    l_root = math.copysign(abs(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b) ** (1 / 3), r + g + b)
    m_root = math.copysign(abs(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b) ** (1 / 3), r + g + b)
    s_root = math.copysign(abs(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b) ** (1 / 3), r + g + b)
    lightness = 0.2104542553 * l_root + 0.7936177850 * m_root - 0.0040720468 * s_root
    lab_a = 1.9779984951 * l_root - 2.4285922050 * m_root + 0.4505937099 * s_root
    lab_b = 0.0259040371 * l_root + 0.7827717662 * m_root - 0.8086757660 * s_root
    chroma = math.sqrt(lab_a * lab_a + lab_b * lab_b)
    hue = math.degrees(math.atan2(lab_b, lab_a)) % 360.0 if chroma > 1.0e-9 else 0.0
    return {"l": round(lightness, 6), "c": round(chroma, 6), "h": round(hue, 4)}


def color_index_from_xy(x: int, y: int) -> int:
    if not 0 <= x < WIDTH or not 0 <= y < HEIGHT:
        raise ValueError("Color coordinates must be inside 256x256.")
    return y * WIDTH + x


def color_coordinates(color_index: int) -> tuple[int, int]:
    if not 0 <= color_index < COLOR_COUNT:
        raise ValueError("colorIndex must be in 0..65535.")
    return color_index % WIDTH, color_index // WIDTH


def rgb565_components(color_index: int) -> tuple[int, int, int]:
    if not 0 <= int(color_index) < COLOR_COUNT:
        raise ValueError("colorIndex must be in 0..65535.")
    value = int(color_index)
    return (value >> 11) & 31, (value >> 5) & 63, value & 31


def color_index_from_rgb565(red5: int, green6: int, blue5: int) -> int:
    if not 0 <= int(red5) < RED_LEVELS:
        raise ValueError("red5 must be in 0..31.")
    if not 0 <= int(green6) < GREEN_LEVELS:
        raise ValueError("green6 must be in 0..63.")
    if not 0 <= int(blue5) < BLUE_LEVELS:
        raise ValueError("blue5 must be in 0..31.")
    return (int(red5) << 11) | (int(green6) << 5) | int(blue5)


def _expand_component(value: int, maximum: int) -> int:
    return (int(value) * 255 + maximum // 2) // maximum


def _quantize_component(value: int, maximum: int) -> int:
    return (max(0, min(255, int(value))) * maximum + 127) // 255


def rgb8_from_color_index(color_index: int) -> tuple[int, int, int]:
    red5, green6, blue5 = rgb565_components(color_index)
    return (
        _expand_component(red5, RED_LEVELS - 1),
        _expand_component(green6, GREEN_LEVELS - 1),
        _expand_component(blue5, BLUE_LEVELS - 1),
    )


def color_index_from_rgb8(red: int, green: int, blue: int) -> int:
    return color_index_from_rgb565(
        _quantize_component(red, RED_LEVELS - 1),
        _quantize_component(green, GREEN_LEVELS - 1),
        _quantize_component(blue, BLUE_LEVELS - 1),
    )


def logical_token(color_index: int, surface_profile: int) -> int:
    if not 0 <= surface_profile < SURFACE_PROFILE_COUNT:
        raise ValueError("surfaceProfile must be in 0..15.")
    return color_index * SURFACE_PROFILE_COUNT + surface_profile


def split_logical_token(value: int) -> tuple[int, int]:
    if not 0 <= value < LOGICAL_TOKEN_COUNT:
        raise ValueError("logicalToken must be in 0..1048575.")
    return value // SURFACE_PROFILE_COUNT, value % SURFACE_PROFILE_COUNT


def color_token_id(color_index: int) -> str:
    red5, green6, blue5 = rgb565_components(color_index)
    return f"wgame.color.v3.r{red5:02d}.g{green6:02d}.b{blue5:02d}"


def surface_token_id(color_index: int, surface_profile: int) -> str:
    return f"{color_token_id(color_index).replace('wgame.color', 'wgame.surface')}.{SURFACE_PROFILES[surface_profile]['profileId']}"


def decode_rgb8(catalog: dict[str, Any]) -> bytes:
    raw = base64.b64decode(catalog["colorData"]["rgb8Base64"].encode("ascii"), validate=True)
    if len(raw) != COLOR_COUNT * 3:
        raise ValueError("rgb8Base64 must contain exactly 196608 bytes.")
    return raw


def build_catalog() -> dict[str, Any]:
    rgb_bytes = bytearray(COLOR_COUNT * 3)
    seen_rgb: set[tuple[int, int, int]] = set()
    for index in range(COLOR_COUNT):
        rgb = rgb8_from_color_index(index)
        if rgb in seen_rgb:
            raise AssertionError(f"RGB565 expansion produced duplicate RGB8 at index {index}.")
        seen_rgb.add(rgb)
        offset = index * 3
        rgb_bytes[offset:offset + 3] = bytes(rgb)

    catalog: dict[str, Any] = {
        "format": FORMAT,
        "schemaVersion": SCHEMA_VERSION,
        "width": WIDTH,
        "height": HEIGHT,
        "colorCount": COLOR_COUNT,
        "surfaceProfileCount": SURFACE_PROFILE_COUNT,
        "logicalTokenCount": LOGICAL_TOKEN_COUNT,
        "addressing": {
            "colorIndex": "y*256+x",
            "rgb565": "(red5<<11)|(green6<<5)|blue5",
            "red5": "(colorIndex>>11)&31",
            "green6": "(colorIndex>>5)&63",
            "blue5": "colorIndex&31",
            "logicalToken": "colorIndex*16+surfaceProfile",
            "tokenId": "wgame.surface.v3.r{red5:02}.g{green6:02}.b{blue5:02}.{profileId}",
            "invalidPaletteUv": [INVALID_PALETTE_UV[0], INVALID_PALETTE_UV[1]],
        },
        "colorFormula": {
            "encoding": "RGB565",
            "redBits": RED_BITS,
            "greenBits": GREEN_BITS,
            "blueBits": BLUE_BITS,
            "redLevels": RED_LEVELS,
            "greenLevels": GREEN_LEVELS,
            "blueLevels": BLUE_LEVELS,
            "expandToRgb8": "round(component*255/componentMax)",
            "quantizeFromRgb8": "round(component8*componentMax/255)",
            "allAddressesUnique": True,
        },
        "surfaceProfiles": copy.deepcopy(SURFACE_PROFILES),
        "colorData": {
            "rgb8Encoding": RGB_ENCODING,
            "rgb8Base64": base64.b64encode(bytes(rgb_bytes)).decode("ascii"),
            "uniqueRgb8Count": COLOR_COUNT,
            "aliasCount": 0,
        },
        "generator": {
            "id": GENERATOR_ID,
            "profileTableVersion": "1.0",
            "catalogIsGeneratorOwned": True,
            "dynamicTokenMutation": False,
        },
        "catalogHash": "",
    }
    catalog["catalogHash"] = compute_catalog_hash(catalog)
    return catalog


def describe_color(catalog: dict[str, Any], color_index: int) -> dict[str, Any]:
    x, y = color_coordinates(color_index)
    rgb_data = decode_rgb8(catalog)
    offset = color_index * 3
    rgb = tuple(rgb_data[offset:offset + 3])
    red5, green6, blue5 = rgb565_components(color_index)
    return {
        "colorId": color_token_id(color_index),
        "colorIndex": color_index,
        "x": x,
        "y": y,
        "red5": red5,
        "green6": green6,
        "blue5": blue5,
        "srgbHex": "#{:02X}{:02X}{:02X}".format(*rgb),
        "srgb8": {"r": rgb[0], "g": rgb[1], "b": rgb[2]},
        "normalizedRgb565": {
            "r": round(red5 / (RED_LEVELS - 1), 6),
            "g": round(green6 / (GREEN_LEVELS - 1), 6),
            "b": round(blue5 / (BLUE_LEVELS - 1), 6),
        },
        "oklch": srgb8_to_oklch(*rgb),
        "canonicalColorIndex": color_index,
        "isAlias": False,
    }


def describe_token(catalog: dict[str, Any], color_index: int, surface_profile: int) -> dict[str, Any]:
    color = describe_color(catalog, color_index)
    profile = copy.deepcopy(catalog["surfaceProfiles"][surface_profile])
    return {
        **color,
        "tokenId": surface_token_id(color_index, surface_profile),
        "logicalToken": logical_token(color_index, surface_profile),
        "surfaceProfileId": profile["profileId"],
        "surfaceProfile": surface_profile,
        "surfaceProfileAlphaByte": profile["alphaByte"],
        "profile": profile,
    }


def canonical_catalog_bytes(catalog: dict[str, Any]) -> bytes:
    payload = copy.deepcopy(catalog)
    payload.pop("catalogHash", None)
    return json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def compute_catalog_hash(catalog: dict[str, Any]) -> str:
    return "sha256:" + hashlib.sha256(canonical_catalog_bytes(catalog)).hexdigest()


def validate_catalog(catalog: dict[str, Any]) -> list[str]:
    issues: list[str] = []
    if catalog.get("format") != FORMAT or catalog.get("schemaVersion") != SCHEMA_VERSION:
        issues.append("format/schemaVersion mismatch")
    if catalog.get("width") != WIDTH or catalog.get("height") != HEIGHT:
        issues.append("atlas dimensions must be 256x256")
    if catalog.get("colorCount") != COLOR_COUNT or catalog.get("surfaceProfileCount") != SURFACE_PROFILE_COUNT:
        issues.append("color/profile counts mismatch")
    if catalog.get("logicalTokenCount") != LOGICAL_TOKEN_COUNT:
        issues.append("logicalTokenCount mismatch")
    profiles = catalog.get("surfaceProfiles") or []
    if len(profiles) != SURFACE_PROFILE_COUNT:
        issues.append("surfaceProfiles must contain 16 entries")
    else:
        if [item.get("index") for item in profiles] != list(range(16)):
            issues.append("surface profile indices must be 0..15")
        if [item.get("alphaByte") for item in profiles] != [8 + index * 16 for index in range(16)]:
            issues.append("surface profile alpha centers mismatch")
        if len({item.get("profileId") for item in profiles}) != 16:
            issues.append("surface profile IDs must be unique")
    try:
        rgb = decode_rgb8(catalog)
        seen = set()
        for index in range(COLOR_COUNT):
            actual = tuple(rgb[index * 3:index * 3 + 3])
            expected = rgb8_from_color_index(index)
            if actual != expected:
                issues.append(f"RGB565 formula mismatch at colorIndex {index}")
                break
            if actual in seen:
                issues.append(f"duplicate RGB8 at colorIndex {index}")
                break
            seen.add(actual)
        if len(seen) != COLOR_COUNT:
            issues.append("Color Palette must contain 65,536 unique RGB8 values")
        color_data = catalog.get("colorData") or {}
        if color_data.get("uniqueRgb8Count") != COLOR_COUNT or color_data.get("aliasCount") != 0:
            issues.append("RGB565 Catalog must report 65,536 unique colors and zero aliases")
        if "canonicalColorIndexU16Base64" in color_data or "cMaxU16Base64" in color_data:
            issues.append("v3 Catalog must not contain legacy alias or Cmax tables")
    except Exception as exception:
        issues.append(str(exception))
    if catalog.get("catalogHash") != compute_catalog_hash(catalog):
        issues.append("catalogHash mismatch")
    return issues


def _hlsl_float(value: float) -> str:
    text = f"{float(value):.6f}".rstrip("0").rstrip(".")
    if "." not in text:
        text += ".0"
    return text


def build_surface_profiles_hlsl(catalog: dict[str, Any]) -> str:
    profiles = catalog["surfaceProfiles"]
    fields = [
        ("SHADOW_FLOOR", "shadowFloor"), ("WRAP", "wrap"), ("AO", "ao"),
        ("SPECULAR", "specular"), ("SPECULAR_POWER", "specularPower"), ("RIM", "rim"),
        ("ROUGHNESS", "roughness"), ("METALLIC", "metallic"),
        ("DETAIL_MODE", "detailModeIndex"), ("DETAIL_SCALE", "detailScale"),
        ("DETAIL_DIFFUSE", "detailDiffuse"), ("DETAIL_SPECULAR", "detailSpecular"),
        ("FAMILY", "family"), ("FINISH", "finish"),
    ]
    lines = [
        "// Generated from ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json. Do not edit.",
        "#ifndef WGAME_SURFACE_PROFILES_GENERATED_INCLUDED",
        "#define WGAME_SURFACE_PROFILES_GENERATED_INCLUDED",
        "",
        "static const float WG_SURFACE_PROFILE_COUNT = 16.0;",
    ]
    for constant, field in fields:
        for group in range(4):
            values = ", ".join(_hlsl_float(profiles[group * 4 + offset][field]) for offset in range(4))
            lines.append(f"static const float4 WG_SURFACE_{constant}_{group} = float4({values});")
    lines.extend([
        "",
        "float WgSurfaceProfileSelect(float profile, float4 group0, float4 group1, float4 group2, float4 group3)",
        "{",
        "    float index = clamp(floor(profile + 0.5), 0.0, 15.0);",
        "    float group = floor(index * 0.25);",
        "    float local = index - group * 4.0;",
        "    float4 localMask = 1.0 - saturate(abs(local - float4(0.0, 1.0, 2.0, 3.0)));",
        "    float4 values = float4(dot(group0, localMask), dot(group1, localMask), dot(group2, localMask), dot(group3, localMask));",
        "    float4 groupMask = 1.0 - saturate(abs(group - float4(0.0, 1.0, 2.0, 3.0)));",
        "    return dot(values, groupMask);",
        "}",
        "",
    ])
    for constant, _ in fields:
        camel = "".join(part.title() for part in constant.lower().split("_"))
        lines.append(f"float WgSurfaceProfile{camel}(float profile) {{ return WgSurfaceProfileSelect(profile, WG_SURFACE_{constant}_0, WG_SURFACE_{constant}_1, WG_SURFACE_{constant}_2, WG_SURFACE_{constant}_3); }}")
    lines.extend(["", "#endif", ""])
    return "\n".join(lines)


def write_catalog(path: Path, catalog: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8", newline="\n")
