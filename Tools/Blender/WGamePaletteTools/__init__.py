bl_info = {
    "name": "WGame 角色画板与表面材质",
    "author": "WGame",
    "version": (3, 14, 21),
    "blender": (5, 2, 0),
    "location": "3D 视图 > 侧栏 > WGame",
    "description": "使用角色配色 Profile 与 16 种独立表面材质 Profile 完成角色上色",
    "category": "Object",
}

from base64 import b64decode
import copy
import datetime as _datetime
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
from urllib.error import URLError
from urllib.request import Request, urlopen

import bpy
import bmesh
from bpy_extras import view3d_utils
import mathutils
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
import numpy as np
import gpu
from gpu_extras.batch import batch_for_shader
from bpy.props import (
    BoolProperty,
    CollectionProperty,
    EnumProperty,
    FloatProperty,
    FloatVectorProperty,
    IntProperty,
    StringProperty,
)
from bpy.types import Operator, Panel, PropertyGroup, UIList


CATALOG_FORMAT = "mx.wgameSurfacePaletteCatalog.v3"
CATALOG_SCHEMA = "3.0"
CHARACTER_COLOR_PROFILE_FORMAT = "mx.characterColorProfile.v1"
CHARACTER_COLOR_PROFILE_SCHEMA = "1.0"
USAGE_FORMAT = "mx.characterPaletteUsage.v2"
WIDTH = 256
HEIGHT = 256
COLOR_COUNT = WIDTH * HEIGHT
PROFILE_COUNT = 16
LOGICAL_TOKEN_COUNT = COLOR_COUNT * PROFILE_COUNT
PAGE_SIZE = 64
INVALID_INDEX = -1
COLOR_PROFILE_SLOT_ATTRIBUTE = "ColorProfileSlot"
CATALOG_RELATIVE = Path("ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json")

PROFILE_ITEMS = (
    ("0", "00 柔和皮肤", "柔和、克制的皮肤光照"),
    ("1", "01 光泽皮肤", "高光更明显的皮肤"),
    ("2", "02 哑光头发", "低高光的头发"),
    ("3", "03 柔光头发", "带宽边缘柔光的头发"),
    ("4", "04 编织布料", "带轻微编织细节的布料"),
    ("5", "05 丝绸", "更顺滑、更亮的布料"),
    ("6", "06 粗糙皮革", "颗粒更明显的皮革"),
    ("7", "07 抛光皮革", "高光更集中的皮革"),
    ("8", "08 原木", "未上漆的木材"),
    ("9", "09 清漆木材", "带清漆高光的木材"),
    ("10", "10 粗糙哑光", "粗颗粒哑光实体表面"),
    ("11", "11 细腻哑光", "细颗粒哑光实体表面"),
    ("12", "12 拉丝金属", "带拉丝与轻微划痕的金属"),
    ("13", "13 抛光金属", "紧致高光的抛光金属"),
    ("14", "14 清透晶体", "风格化不透明晶体"),
    ("15", "15 磨砂晶体", "带磨砂颗粒的晶体"),
)

DETAIL_MODE_LABELS = {
    "clean": "干净",
    "woven": "编织纹理",
    "silk": "丝绸纹理",
    "leather": "皮革纹理",
    "wood": "木纹",
    "matte": "哑光颗粒",
    "brushed": "拉丝细节",
    "crystal": "晶体细节",
    "frost": "磨砂细节",
}


class _State:
    catalog = None
    rgb8 = b""
    filtered_indices = []
    source = "不可用"
    favorites = set()
    recent = []
    rebuilding = False


STATE = _State()


def _repository_root() -> Path:
    configured = os.environ.get("WGAME_REPOSITORY_ROOT", "").strip()
    if configured:
        return Path(configured).resolve()
    deployment = Path(__file__).with_name("deployment.json")
    if deployment.is_file():
        try:
            value = json.loads(deployment.read_text(encoding="utf-8")).get("repositoryRoot", "")
            if value:
                return Path(value).resolve()
        except Exception:
            pass
    return Path(__file__).resolve().parents[3]


def _cache_root() -> Path:
    path = Path(bpy.utils.user_resource("CONFIG", path="wgame_rgb565_palette_v3", create=True))
    path.mkdir(parents=True, exist_ok=True)
    return path


def _cache_catalog_path() -> Path:
    return _cache_root() / "catalog.json"


def _preferences_path() -> Path:
    return _cache_root() / "preferences.json"


def _read_preferences() -> None:
    try:
        data = json.loads(_preferences_path().read_text(encoding="utf-8"))
        STATE.favorites = {int(value) for value in data.get("favorites", []) if 0 <= int(value) < COLOR_COUNT}
        STATE.recent = [int(value) for value in data.get("recent", []) if 0 <= int(value) < COLOR_COUNT][:64]
    except Exception:
        STATE.favorites = set()
        STATE.recent = []


def _write_preferences() -> None:
    document = {"favorites": sorted(STATE.favorites), "recent": STATE.recent[:64]}
    _preferences_path().write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")


def _server_url(scene) -> str:
    return str(getattr(scene, "wgame_palette_server_url", "http://127.0.0.1:4873")).rstrip("/")


def _request_json(url: str):
    request = Request(url, headers={"Accept": "application/json"})
    with urlopen(request, timeout=2.0) as response:
        return json.loads(response.read().decode("utf-8"))


def _canonical_catalog_hash(catalog) -> str:
    payload = copy.deepcopy(catalog)
    payload.pop("catalogHash", None)
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")
    return "sha256:" + hashlib.sha256(encoded).hexdigest()


def _validate_catalog(catalog) -> None:
    if (
        catalog.get("format") != CATALOG_FORMAT
        or catalog.get("schemaVersion") != CATALOG_SCHEMA
        or catalog.get("width") != WIDTH
        or catalog.get("height") != HEIGHT
        or catalog.get("colorCount") != COLOR_COUNT
        or catalog.get("surfaceProfileCount") != PROFILE_COUNT
        or catalog.get("logicalTokenCount") != LOGICAL_TOKEN_COUNT
    ):
        raise ValueError("Catalog 必须是 mx.wgameSurfacePaletteCatalog.v3，并包含 65,536 种唯一 RGB565 颜色和 16 个 Profile。")
    profiles = catalog.get("surfaceProfiles") or []
    if len(profiles) != PROFILE_COUNT:
        raise ValueError("Catalog 必须恰好包含 16 个已冻结的表面材质 Profile。")
    if [int(item.get("index", -1)) for item in profiles] != list(range(PROFILE_COUNT)):
        raise ValueError("表面材质 Profile 的索引必须是 0..15。")
    if [int(item.get("alphaByte", -1)) for item in profiles] != [8 + index * 16 for index in range(PROFILE_COUNT)]:
        raise ValueError("表面材质 Profile 的 Alpha 中心值与约定不一致。")
    color_data = catalog.get("colorData") or {}
    rgb8 = b64decode(color_data.get("rgb8Base64", ""), validate=True)
    if len(rgb8) != COLOR_COUNT * 3:
        raise ValueError("紧凑 RGB8 表必须包含 196608 字节。")
    unique = set()
    for index in range(COLOR_COUNT):
        expected = rgb8_from_color_index(index)
        actual = tuple(rgb8[index * 3:index * 3 + 3])
        if actual != expected:
            raise ValueError(f"colorIndex {index} 的 RGB565 公式结果不一致。")
        if actual in unique:
            raise ValueError(f"colorIndex {index} 出现重复 RGB8 颜色。")
        unique.add(actual)
    if len(unique) != COLOR_COUNT or color_data.get("uniqueRgb8Count") != COLOR_COUNT or color_data.get("aliasCount") != 0:
        raise ValueError("RGB565 Catalog 必须包含 65,536 种唯一 RGB8 颜色，且不能存在别名。")
    if catalog.get("catalogHash") != _canonical_catalog_hash(catalog):
        raise ValueError("Catalog hash 与规范化紧凑 JSON 不一致。")


def load_catalog(context=None, prefer_server=True):
    context = context or bpy.context
    catalog = None
    source = "不可用"
    if prefer_server:
        try:
            catalog = _request_json(f"{_server_url(context.scene)}/api/authoring/palette/catalog")
            source = "创作服务"
        except (OSError, URLError, TimeoutError, ValueError, json.JSONDecodeError):
            catalog = None
    if catalog is None:
        repository_catalog = _repository_root() / CATALOG_RELATIVE
        if repository_catalog.is_file():
            catalog = json.loads(repository_catalog.read_text(encoding="utf-8"))
            source = "仓库"
        elif _cache_catalog_path().is_file():
            catalog = json.loads(_cache_catalog_path().read_text(encoding="utf-8"))
            source = "离线缓存"
    if catalog is None:
        STATE.catalog = None
        STATE.rgb8 = b""
        STATE.source = "不可用"
        raise FileNotFoundError("无法读取 WGame RGB565 角色画板 v3 Catalog。")
    _validate_catalog(catalog)
    color_data = catalog["colorData"]
    STATE.catalog = catalog
    STATE.rgb8 = b64decode(color_data["rgb8Base64"], validate=True)
    STATE.source = source
    if source != "离线缓存":
        _cache_catalog_path().write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    rebuild_color_items(context)
    return catalog


def color_coordinates(color_index: int):
    if not 0 <= int(color_index) < COLOR_COUNT:
        raise ValueError("colorIndex 必须在 0..65535 范围内。")
    return int(color_index) % WIDTH, int(color_index) // WIDTH


def rgb565_components(color_index: int):
    if not 0 <= int(color_index) < COLOR_COUNT:
        raise ValueError("colorIndex 必须在 0..65535 范围内。")
    value = int(color_index)
    return (value >> 11) & 31, (value >> 5) & 63, value & 31


def color_index_from_rgb565(red5: int, green6: int, blue5: int) -> int:
    if not 0 <= int(red5) < 32 or not 0 <= int(green6) < 64 or not 0 <= int(blue5) < 32:
        raise ValueError("RGB565 分量必须满足 R5 0..31、G6 0..63、B5 0..31。")
    return (int(red5) << 11) | (int(green6) << 5) | int(blue5)


def rgb8_from_color_index(color_index: int):
    red5, green6, blue5 = rgb565_components(color_index)
    return ((red5 * 255 + 15) // 31, (green6 * 255 + 31) // 63, (blue5 * 255 + 15) // 31)


def color_index_from_rgb8(red: int, green: int, blue: int) -> int:
    return color_index_from_rgb565(
        (max(0, min(255, int(red))) * 31 + 127) // 255,
        (max(0, min(255, int(green))) * 63 + 127) // 255,
        (max(0, min(255, int(blue))) * 31 + 127) // 255,
    )


def color_id(color_index: int) -> str:
    red5, green6, blue5 = rgb565_components(color_index)
    return f"wgame.color.v3.r{red5:02d}.g{green6:02d}.b{blue5:02d}"


def logical_token(color_index: int, surface_profile: int) -> int:
    if not 0 <= int(surface_profile) < PROFILE_COUNT:
        raise ValueError("surfaceProfile 必须在 0..15 范围内。")
    return int(color_index) * PROFILE_COUNT + int(surface_profile)


def surface_token_id(color_index: int, surface_profile: int) -> str:
    profile = STATE.catalog["surfaceProfiles"][int(surface_profile)]
    return color_id(color_index).replace("wgame.color", "wgame.surface") + "." + profile["profileId"]


def color_hex(color_index: int) -> str:
    offset = int(color_index) * 3
    return "#{:02X}{:02X}{:02X}".format(*STATE.rgb8[offset:offset + 3])


def _set_scene_color(scene, color_index: int) -> None:
    color_index = max(0, min(COLOR_COUNT - 1, int(color_index)))
    scene.wgame_palette_selected_color = color_index
    scene.wgame_palette_red5 = rgb565_components(color_index)[0]
    scene.wgame_palette_page = 0
    _update_color_preview(scene)


def _set_profile_item_color(item, color_index: int) -> None:
    color_index = max(0, min(COLOR_COUNT - 1, int(color_index)))
    rgb8 = rgb8_from_color_index(color_index)
    previous = STATE.rebuilding
    STATE.rebuilding = True
    try:
        item.color_index = color_index
        item.hex = "#{:02X}{:02X}{:02X}".format(*rgb8)
        item.color_preview = tuple(value / 255.0 for value in rgb8)
    finally:
        STATE.rebuilding = previous


def _active_profile_item(scene):
    items = scene.wgame_character_color_profile_colors
    index = int(scene.wgame_character_color_profile_active)
    return items[index] if 0 <= index < len(items) else None


def _profile_item_color_update(self, context):
    if STATE.rebuilding:
        return
    values = self.color_preview
    color_index = color_index_from_rgb8(
        round(float(values[0]) * 255.0),
        round(float(values[1]) * 255.0),
        round(float(values[2]) * 255.0),
    )
    _set_profile_item_color(self, color_index)
    if context is not None and getattr(context, "scene", None) is not None:
        _set_scene_color(context.scene, color_index)


def _profile_item_surface_update(self, context):
    if STATE.rebuilding or context is None or getattr(context, "scene", None) is None:
        return
    context.scene.wgame_palette_selected_profile = str(self.surface_profile)


def _profile_active_update(self, context):
    if STATE.rebuilding:
        return
    item = _active_profile_item(self)
    if item is not None:
        _set_scene_color(self, int(item.color_index))
        self.wgame_palette_selected_profile = str(item.surface_profile)


def _next_profile_category_index(scene) -> int:
    used = {int(item.category_index) for item in scene.wgame_character_color_profile_colors if int(item.category_index) >= 0}
    candidate = 0
    while candidate in used:
        candidate += 1
    return candidate


def _profile_slot_id(label: str, used) -> str:
    stem = re.sub(r"[^a-z0-9]+", "_", str(label).strip().lower()).strip("_") or "color"
    candidate = stem
    suffix = 2
    while candidate in used:
        candidate = f"{stem}_{suffix:02d}"
        suffix += 1
    return candidate


def _resolve_character_color_profile_path(scene) -> Path:
    configured = str(scene.wgame_character_color_profile_path).strip()
    if configured:
        return Path(bpy.path.abspath(configured)).resolve()
    package_value = str(scene.wgame_palette_package_root).strip()
    if package_value and package_value != "//":
        return Path(bpy.path.abspath(package_value)).resolve() / "config" / "character_color_profile.json"
    raise ValueError("请选择角色配色 Profile 文件，或先配置角色包目录。")


def character_color_profile_document(scene):
    if STATE.catalog is None:
        raise RuntimeError("保存角色配色 Profile 前，请先加载 RGB565 角色画板 v3。")
    colors = []
    used_slots = set()
    used_categories = set()
    for position, item in enumerate(scene.wgame_character_color_profile_colors):
        label = str(item.label).strip() or f"颜色 {position + 1:02d}"
        slot_id = str(item.slot_id).strip()
        if not slot_id or slot_id in used_slots:
            slot_id = _profile_slot_id(label, used_slots)
        used_slots.add(slot_id)
        item.slot_id = slot_id
        item.label = label
        category_index = int(item.category_index)
        if category_index < 0 or category_index in used_categories:
            category_index = 0
            while category_index in used_categories:
                category_index += 1
            item.category_index = category_index
        used_categories.add(category_index)
        color_index = int(item.color_index)
        surface_profile = int(item.surface_profile)
        colors.append(
            {
                "slotId": slot_id,
                "label": label,
                "categoryIndex": category_index,
                "colorIndex": color_index,
                "srgbHex": color_hex(color_index),
                "surfaceProfile": surface_profile,
                "surfaceProfileId": STATE.catalog["surfaceProfiles"][surface_profile]["profileId"],
            }
        )
    return {
        "format": CHARACTER_COLOR_PROFILE_FORMAT,
        "schemaVersion": CHARACTER_COLOR_PROFILE_SCHEMA,
        "profileId": str(scene.wgame_character_color_profile_id).strip() or "default",
        "displayName": str(scene.wgame_character_color_profile_name).strip() or "角色配色",
        "catalogHash": STATE.catalog.get("catalogHash", ""),
        "colors": colors,
    }


def save_character_color_profile(scene) -> Path:
    path = _resolve_character_color_profile_path(scene)
    document = character_color_profile_document(scene)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    scene.wgame_character_color_profile_path = str(path)
    return path


def load_character_color_profile(scene) -> Path:
    if STATE.catalog is None:
        raise RuntimeError("加载角色配色 Profile 前，请先加载 RGB565 角色画板 v3。")
    path = _resolve_character_color_profile_path(scene)
    document = json.loads(path.read_text(encoding="utf-8"))
    if document.get("format") != CHARACTER_COLOR_PROFILE_FORMAT or document.get("schemaVersion") != CHARACTER_COLOR_PROFILE_SCHEMA:
        raise ValueError("角色配色 Profile 必须使用 mx.characterColorProfile.v1 / 1.0。")
    expected_hash = str(STATE.catalog.get("catalogHash", ""))
    profile_hash = str(document.get("catalogHash", ""))
    if profile_hash != expected_hash:
        raise ValueError("角色配色 Profile 的 catalogHash 与当前 RGB565 Catalog 不一致。")
    colors = document.get("colors")
    if not isinstance(colors, list):
        raise ValueError("角色配色 Profile 的 colors 必须是数组。")
    if len(colors) > 256:
        raise ValueError("一个角色配色 Profile 最多支持 256 种精选颜色。")
    seen_slots = set()
    seen_categories = set()
    normalized = []
    for position, entry in enumerate(colors):
        if not isinstance(entry, dict):
            raise ValueError(f"角色配色 Profile 的第 {position} 个条目无效。")
        slot_id = str(entry.get("slotId", "")).strip()
        label = str(entry.get("label", "")).strip()
        category_index = int(entry.get("categoryIndex", position))
        color_index = int(entry.get("colorIndex", INVALID_INDEX))
        surface_profile = int(entry.get("surfaceProfile", 0))
        if not re.fullmatch(r"[a-z0-9][a-z0-9_]*", slot_id) or slot_id in seen_slots:
            raise ValueError(f"角色配色 Profile 的第 {position} 个条目包含无效或重复的 slotId。")
        if not 0 <= color_index < COLOR_COUNT:
            raise ValueError(f"角色配色 Profile 条目 {slot_id} 的 colorIndex 无效。")
        if category_index < 0 or category_index in seen_categories:
            raise ValueError(f"角色配色 Profile 条目 {slot_id} 的 categoryIndex 无效或重复。")
        if not 0 <= surface_profile < PROFILE_COUNT:
            raise ValueError(f"角色配色 Profile 条目 {slot_id} 的 surfaceProfile 无效。")
        expected_profile_id = STATE.catalog["surfaceProfiles"][surface_profile]["profileId"]
        if str(entry.get("surfaceProfileId", expected_profile_id)) != expected_profile_id:
            raise ValueError(f"角色配色 Profile 条目 {slot_id} 的 surfaceProfileId 与索引不一致。")
        expected_hex = color_hex(color_index)
        provided_hex = str(entry.get("srgbHex", expected_hex)).upper()
        if provided_hex != expected_hex:
            raise ValueError(f"角色配色 Profile 条目 {slot_id} 的 Hex 与 RGB565 colorIndex {color_index} 不一致。")
        seen_slots.add(slot_id)
        seen_categories.add(category_index)
        normalized.append((slot_id, label or slot_id, category_index, color_index, surface_profile))
    previous = STATE.rebuilding
    STATE.rebuilding = True
    try:
        scene.wgame_character_color_profile_colors.clear()
        for slot_id, label, category_index, color_index, surface_profile in normalized:
            item = scene.wgame_character_color_profile_colors.add()
            item.slot_id = slot_id
            item.label = label
            item.category_index = category_index
            item.surface_profile = str(surface_profile)
            _set_profile_item_color(item, color_index)
        scene.wgame_character_color_profile_id = str(document.get("profileId", "default")).strip() or "default"
        scene.wgame_character_color_profile_name = str(document.get("displayName", "角色配色")).strip() or "角色配色"
        scene.wgame_character_color_profile_active = 0
        scene.wgame_character_color_profile_path = str(path)
    finally:
        STATE.rebuilding = previous
    if normalized:
        _set_scene_color(scene, normalized[0][3])
        scene.wgame_palette_selected_profile = str(normalized[0][4])
    return path


def color_profile_from_face(context, obj, polygon_index: int):
    if obj is None or obj.type != "MESH":
        raise ValueError("吸管目标必须是网格对象。")
    polygon_index = int(polygon_index)
    if obj.mode == "EDIT":
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        if not 0 <= polygon_index < len(bm.faces):
            raise ValueError("吸管没有拾取到有效网格面。")
        token_layer = bm.faces.layers.int.get("PaletteToken")
        profile_layer = bm.faces.layers.int.get("SurfaceProfile")
        cat_layer = bm.faces.layers.int.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
        if token_layer is None or profile_layer is None:
            raise ValueError("目标网格不包含 PaletteToken 和 SurfaceProfile 数据。")
        face = bm.faces[polygon_index]
        token_value = int(face[token_layer])
        surface_profile = int(face[profile_layer])
        category_index = (
            int(face[cat_layer])
            if cat_layer is not None and face[cat_layer] >= 0
            else INVALID_INDEX
        )
    else:
        if not 0 <= polygon_index < len(obj.data.polygons):
            raise ValueError("吸管没有拾取到有效网格面。")
        token_attribute = obj.data.attributes.get("PaletteToken")
        profile_attribute = obj.data.attributes.get("SurfaceProfile")
        category_attribute = obj.data.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
        if token_attribute is None or profile_attribute is None:
            raise ValueError("目标网格不包含 PaletteToken 和 SurfaceProfile 数据。")
        if not 0 <= polygon_index < len(token_attribute.data):
            raise ValueError("吸管拾取面数据未初始化。")
        token_value = int(token_attribute.data[polygon_index].value)
        surface_profile = int(profile_attribute.data[polygon_index].value)
        category_index = (
            int(category_attribute.data[polygon_index].value)
            if category_attribute is not None and polygon_index < len(category_attribute.data)
            else INVALID_INDEX
        )
    if not 0 <= token_value < LOGICAL_TOKEN_COUNT or not 0 <= surface_profile < PROFILE_COUNT:
        raise ValueError("这个面还没有完成角色画板上色。")
    color_index = token_value // PROFILE_COUNT
    if token_value % PROFILE_COUNT != surface_profile:
        raise ValueError("目标面的颜色与 Profile 数据不一致。")
    matching = next(
        (
            index
            for index, item in enumerate(context.scene.wgame_character_color_profile_colors)
            if (category_index >= 0 and int(item.category_index) == category_index)
            or (category_index < 0 and int(item.color_index) == color_index)
        ),
        None,
    )
    if matching is not None:
        context.scene.wgame_character_color_profile_colors[matching].surface_profile = str(surface_profile)
        context.scene.wgame_character_color_profile_active = matching
    _set_scene_color(context.scene, color_index)
    context.scene.wgame_palette_selected_profile = str(surface_profile)
    return color_index, surface_profile, category_index, matching is not None


def color_profile_from_selected_face(context):
    obj = getattr(context, "object", None)
    if obj is None or obj.type != "MESH":
        raise ValueError("请先选择一个网格面。")
    restore_edit = obj.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    try:
        selected = [polygon for polygon in obj.data.polygons if polygon.select]
        if not selected:
            raise ValueError("请先选择一个已经上色的面。")
        color_index, surface_profile, _category_index, in_profile = color_profile_from_face(
            context,
            obj,
            selected[0].index,
        )
        return color_index, surface_profile, in_profile
    finally:
        if restore_edit:
            context.view_layer.objects.active = obj
            bpy.ops.object.mode_set(mode="EDIT")


def _selected_mesh_objects(context):
    objects = [obj for obj in getattr(context, "selected_objects", []) if obj.type == "MESH"]
    active = getattr(context, "object", None)
    if not objects and active is not None and active.type == "MESH":
        objects = [active]
    return objects


def _profile_mesh_objects(context):
    """Classified meshes reference the Profile owned by their current Scene."""
    return [
        obj for obj in context.scene.objects
        if obj.type == "MESH" and obj.data.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE) is not None
    ]


def _used_color_indices(context):
    active = getattr(context.view_layer.objects, "active", None)
    restore_edit = active is not None and active.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    result = set()
    try:
        for obj in _selected_mesh_objects(context):
            attribute = obj.data.attributes.get("PaletteToken")
            if not attribute or attribute.domain != "FACE" or attribute.data_type != "INT":
                continue
            for value in attribute.data:
                token = int(value.value)
                if 0 <= token < LOGICAL_TOKEN_COUNT:
                    result.add(token // PROFILE_COUNT)
    finally:
        if restore_edit and active is not None:
            context.view_layer.objects.active = active
            bpy.ops.object.mode_set(mode="EDIT")
    return result


def _parse_direct_query(text: str):
    value = text.strip().lower()
    if not value:
        return None
    hex_match = re.fullmatch(r"#?([0-9a-f]{6})", value)
    if hex_match:
        packed = int(hex_match.group(1), 16)
        return color_index_from_rgb8((packed >> 16) & 255, (packed >> 8) & 255, packed & 255)
    if value.isdigit():
        number = int(value)
        if number < COLOR_COUNT:
            return number
        if number < LOGICAL_TOKEN_COUNT:
            return number // PROFILE_COUNT
    match = re.fullmatch(r"\(?\s*(\d{1,3})\s*[,/]\s*(\d{1,3})\s*\)?", value)
    if match:
        x, y = int(match.group(1)), int(match.group(2))
        if x < WIDTH and y < HEIGHT:
            return y * WIDTH + x
    match = re.search(r"wgame\.(?:color|surface)\.v3\.r(\d{2})\.g(\d{2})\.b(\d{2})", value)
    if match:
        red5, green6, blue5 = map(int, match.groups())
        if red5 < 32 and green6 < 64 and blue5 < 32:
            return color_index_from_rgb565(red5, green6, blue5)
    return None


def _filter_update(self, context):
    rebuild_color_items(context)


def rebuild_color_items(context=None):
    context = context or bpy.context
    if STATE.catalog is None or STATE.rebuilding or not hasattr(bpy.types.WindowManager, "wgame_palette_colors"):
        return
    scene = context.scene
    query = scene.wgame_palette_query.strip().lower()
    direct = _parse_direct_query(query)
    used = _used_color_indices(context)
    recent = set(STATE.recent)
    indices = []
    for index in range(COLOR_COUNT):
        x, y = color_coordinates(index)
        red5, green6, blue5 = rgb565_components(index)
        if scene.wgame_palette_view == "SLICE" and red5 != scene.wgame_palette_red5:
            continue
        if scene.wgame_palette_green6 >= 0 and green6 != scene.wgame_palette_green6:
            continue
        if scene.wgame_palette_blue5 >= 0 and blue5 != scene.wgame_palette_blue5:
            continue
        view = scene.wgame_palette_view
        if view == "USED" and index not in used:
            continue
        if view == "FAVORITES" and index not in STATE.favorites:
            continue
        if view == "RECENT" and index not in recent:
            continue
        if direct is not None and index != direct:
            continue
        if query and direct is None:
            searchable = f"{color_id(index)} {index} {x},{y} r{red5} g{green6} b{blue5} {color_hex(index)}".lower()
            if query not in searchable:
                continue
        indices.append(index)
    if scene.wgame_palette_view == "RECENT":
        order = {value: position for position, value in enumerate(STATE.recent)}
        indices.sort(key=lambda value: order.get(value, 9999))
    STATE.filtered_indices = indices
    page_count = max(1, math.ceil(len(indices) / PAGE_SIZE))
    scene.wgame_palette_page = max(0, min(scene.wgame_palette_page, page_count - 1))
    start = scene.wgame_palette_page * PAGE_SIZE
    page = indices[start:start + PAGE_SIZE]
    STATE.rebuilding = True
    try:
        collection = context.window_manager.wgame_palette_colors
        collection.clear()
        for index in page:
            item = collection.add()
            x, y = color_coordinates(index)
            item.color_index = index
            item.hex = color_hex(index)
            red5, green6, blue5 = rgb565_components(index)
            item.axis = f"R{red5} G{green6} B{blue5}"
            item.color_id = color_id(index)
        active = next((position for position, item in enumerate(page) if item == scene.wgame_palette_selected_color), -1)
        context.window_manager.wgame_palette_color_active = active
    finally:
        STATE.rebuilding = False


def _update_color_preview(scene):
    if not STATE.rgb8:
        return
    index = max(0, min(COLOR_COUNT - 1, int(scene.wgame_palette_selected_color)))
    offset = index * 3
    previous = STATE.rebuilding
    STATE.rebuilding = True
    try:
        scene.wgame_palette_color_preview = tuple(value / 255.0 for value in STATE.rgb8[offset:offset + 3])
        if hasattr(scene, "wgame_palette_hex_input"):
            scene.wgame_palette_hex_input = color_hex(index)
    finally:
        STATE.rebuilding = previous


def _color_picker_update(self, context):
    if STATE.rebuilding or not STATE.rgb8:
        return
    values = context.scene.wgame_palette_color_preview
    color_index = color_index_from_rgb8(
        round(float(values[0]) * 255.0),
        round(float(values[1]) * 255.0),
        round(float(values[2]) * 255.0),
    )
    context.scene.wgame_palette_selected_color = color_index
    context.scene.wgame_palette_red5 = rgb565_components(color_index)[0]
    context.scene.wgame_palette_page = 0
    rebuild_color_items(context)
    _update_color_preview(context.scene)


def _active_item_update(self, context):
    if STATE.rebuilding:
        return
    collection = self.wgame_palette_colors
    position = int(self.wgame_palette_color_active)
    if 0 <= position < len(collection):
        context.scene.wgame_palette_selected_color = int(collection[position].color_index)
        _update_color_preview(context.scene)


def _ensure_int_face_attribute(mesh, name: str):
    attribute = mesh.attributes.get(name)
    if attribute is not None and (attribute.domain != "FACE" or attribute.data_type != "INT"):
        mesh.attributes.remove(attribute)
        attribute = None
    if attribute is None:
        attribute = mesh.attributes.new(name=name, type="INT", domain="FACE")
        for value in attribute.data:
            value.value = INVALID_INDEX
    return attribute


def _ensure_palette_uv(mesh):
    layer = mesh.uv_layers.get("PaletteUV")
    if layer is None:
        if len(mesh.uv_layers) == 0:
            mesh.uv_layers.new(name="UV0")
        layer = mesh.uv_layers.new(name="PaletteUV")
        for value in layer.data:
            value.uv = (-1.0, -1.0)
    elif len(mesh.uv_layers) == 1 and mesh.uv_layers[0] == layer:
        layer.name = "UV0"
        new_layer = mesh.uv_layers.new(name="PaletteUV")
        for i, val in enumerate(mesh.uv_layers["UV0"].data):
            new_layer.data[i].uv = val.uv
        layer = new_layer
    return layer


def _ensure_palette_data(mesh):
    attribute = mesh.color_attributes.get("PaletteData")
    if attribute is not None and attribute.domain == "CORNER" and attribute.data_type == "FLOAT_COLOR":
        return attribute
    preserved = []
    if attribute is not None:
        if attribute.domain == "CORNER":
            preserved = [tuple(value.color[:3]) for value in attribute.data]
        elif attribute.domain == "POINT":
            preserved = [tuple(attribute.data[loop.vertex_index].color[:3]) for loop in mesh.loops]
        mesh.color_attributes.remove(attribute)
    attribute = mesh.color_attributes.new(name="PaletteData", type="FLOAT_COLOR", domain="CORNER")
    for index, value in enumerate(attribute.data):
        rgb = preserved[index] if index < len(preserved) else (1.0, 0.0, 0.0)
        value.color = (rgb[0], rgb[1], rgb[2], 0.0)
    return attribute


def _ensure_mesh_attributes(mesh):
    token = _ensure_int_face_attribute(mesh, "PaletteToken")
    profile = _ensure_int_face_attribute(mesh, "SurfaceProfile")
    uv = _ensure_palette_uv(mesh)
    data = _ensure_palette_data(mesh)
    return token, profile, uv, data


def _apply_color_profile_to_polygons(
    obj,
    polygon_indices,
    color_index: int,
    surface_profile: int,
    category_index: int = INVALID_INDEX,
):
    if STATE.catalog is None:
        raise RuntimeError("上色前，请先加载 RGB565 角色画板 v3。")
    if obj is None or obj.type != "MESH":
        raise ValueError("画笔目标必须是网格对象。")
    color_index = int(color_index)
    surface_profile = int(surface_profile)
    category_index = int(category_index)
    x, y = color_coordinates(color_index)
    profile = STATE.catalog["surfaceProfiles"][surface_profile]
    token_value = logical_token(color_index, surface_profile)
    expected_uv = ((x + 0.5) / WIDTH, (y + 0.5) / HEIGHT)
    alpha = int(profile["alphaByte"]) / 255.0
    mesh = obj.data

    if obj.mode == "EDIT":
        bm = bmesh.from_edit_mesh(mesh)
        bm.faces.ensure_lookup_table()
        token_layer = bm.faces.layers.int.get("PaletteToken") or bm.faces.layers.int.new("PaletteToken")
        profile_layer = bm.faces.layers.int.get("SurfaceProfile") or bm.faces.layers.int.new("SurfaceProfile")
        cat_layer = (
            bm.faces.layers.int.get(COLOR_PROFILE_SLOT_ATTRIBUTE) or bm.faces.layers.int.new(COLOR_PROFILE_SLOT_ATTRIBUTE)
            if category_index >= 0
            else bm.faces.layers.int.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
        )
        uv_layer = bm.loops.layers.uv.get("PaletteUV") or bm.loops.layers.uv.new("PaletteUV")
        col_layer = bm.loops.layers.float_color.get("PaletteData") or bm.loops.layers.float_color.new("PaletteData")
        painted = 0
        for polygon_index in dict.fromkeys(int(value) for value in polygon_indices):
            if not 0 <= polygon_index < len(bm.faces):
                continue
            face = bm.faces[polygon_index]
            face[token_layer] = token_value
            face[profile_layer] = surface_profile
            if cat_layer is not None and category_index >= 0:
                face[cat_layer] = category_index
            for loop in face.loops:
                loop[uv_layer].uv = expected_uv
                current = loop[col_layer]
                loop[col_layer] = (current[0], current[1], current[2], alpha)
            painted += 1
        if painted:
            bmesh.update_edit_mesh(mesh)
            obj["paletteRepaintRequired"] = False
            mesh["paletteRepaintRequired"] = False
        return painted

    token_attr, profile_attr, palette_uv, palette_data = _ensure_mesh_attributes(mesh)
    category_attr = (
        _ensure_int_face_attribute(mesh, COLOR_PROFILE_SLOT_ATTRIBUTE)
        if category_index >= 0
        else mesh.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
    )
    painted = 0
    for polygon_index in dict.fromkeys(int(value) for value in polygon_indices):
        if not 0 <= polygon_index < len(mesh.polygons):
            continue
        polygon = mesh.polygons[polygon_index]
        if polygon.index < len(token_attr.data):
            token_attr.data[polygon.index].value = token_value
        if polygon.index < len(profile_attr.data):
            profile_attr.data[polygon.index].value = surface_profile
        if category_attr is not None and category_index >= 0 and polygon.index < len(category_attr.data):
            category_attr.data[polygon.index].value = category_index
        for loop_index in polygon.loop_indices:
            if loop_index < len(palette_uv.data):
                palette_uv.data[loop_index].uv = expected_uv
            if loop_index < len(palette_data.data):
                current = palette_data.data[loop_index].color
                palette_data.data[loop_index].color = (current[0], current[1], current[2], alpha)
        painted += 1
    if painted:
        if token_value < 0 or obj.get("paletteRepaintRequired", True):
            token_vals = np.empty(len(token_attr.data), dtype=np.int32)
            token_attr.data.foreach_get("value", token_vals)
            obj["paletteRepaintRequired"] = bool((token_vals < 0).any())
        else:
            obj["paletteRepaintRequired"] = False
        mesh["paletteRepaintRequired"] = obj["paletteRepaintRequired"]
        mesh.update()
    return painted


def apply_color_profile_to_face(
    obj,
    polygon_index: int,
    color_index: int,
    surface_profile: int,
    category_index: int = INVALID_INDEX,
):
    painted = _apply_color_profile_to_polygons(
        obj,
        [polygon_index],
        color_index,
        surface_profile,
        category_index,
    )
    if painted:
        color_index = int(color_index)
        STATE.recent = [color_index, *[value for value in STATE.recent if value != color_index]][:64]
        _write_preferences()
    return painted


def _capture_color_profile_polygons(obj, polygon_indices):
    mesh = obj.data
    if obj.mode == "EDIT":
        bm = bmesh.from_edit_mesh(mesh)
        bm.faces.ensure_lookup_table()
        token_layer = bm.faces.layers.int.get("PaletteToken")
        profile_layer = bm.faces.layers.int.get("SurfaceProfile")
        cat_layer = bm.faces.layers.int.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
        uv_layer = bm.loops.layers.uv.get("PaletteUV")
        col_layer = bm.loops.layers.float_color.get("PaletteData")
        result = {}
        for pi in polygon_indices:
            if not 0 <= pi < len(bm.faces):
                continue
            f = bm.faces[pi]
            result[pi] = {
                "token": int(f[token_layer]) if token_layer is not None else INVALID_INDEX,
                "profile": int(f[profile_layer]) if profile_layer is not None else INVALID_INDEX,
                "category": int(f[cat_layer]) if cat_layer is not None else INVALID_INDEX,
                "uv": [
                    tuple(loop[uv_layer].uv) if uv_layer is not None else (-1.0, -1.0)
                    for loop in f.loops
                ],
                "data": [
                    tuple(loop[col_layer]) if col_layer is not None else (1.0, 0.0, 0.0, 0.0)
                    for loop in f.loops
                ],
            }
        return result

    token = mesh.attributes.get("PaletteToken")
    profile = mesh.attributes.get("SurfaceProfile")
    category = mesh.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
    palette_uv = mesh.uv_layers.get("PaletteUV")
    palette_data = mesh.color_attributes.get("PaletteData")

    token_data = token.data if token is not None else None
    profile_data = profile.data if profile is not None else None
    cat_data = category.data if category is not None else None
    uv_data = palette_uv.data if palette_uv is not None else None
    col_data = palette_data.data if palette_data is not None else None

    result = {}
    for pi in polygon_indices:
        if not 0 <= pi < len(mesh.polygons):
            continue
        p = mesh.polygons[pi]
        result[pi] = {
            "token": int(token_data[pi].value) if token_data is not None and pi < len(token_data) else INVALID_INDEX,
            "profile": int(profile_data[pi].value) if profile_data is not None and pi < len(profile_data) else INVALID_INDEX,
            "category": int(cat_data[pi].value) if cat_data is not None and pi < len(cat_data) else INVALID_INDEX,
            "uv": [
                tuple(uv_data[li].uv) if uv_data is not None and li < len(uv_data) else (-1.0, -1.0)
                for li in p.loop_indices
            ],
            "data": [
                tuple(col_data[li].color) if col_data is not None and li < len(col_data) else (1.0, 0.0, 0.0, 0.0)
                for li in p.loop_indices
            ],
        }
    return result


def _restore_color_profile_polygons(obj, state_dict):
    if not state_dict:
        return
    mesh = obj.data
    if obj.mode == "EDIT":
        bm = bmesh.from_edit_mesh(mesh)
        bm.faces.ensure_lookup_table()
        token_layer = bm.faces.layers.int.get("PaletteToken") or bm.faces.layers.int.new("PaletteToken")
        profile_layer = bm.faces.layers.int.get("SurfaceProfile") or bm.faces.layers.int.new("SurfaceProfile")
        cat_layer = bm.faces.layers.int.get(COLOR_PROFILE_SLOT_ATTRIBUTE) or bm.faces.layers.int.new(COLOR_PROFILE_SLOT_ATTRIBUTE)
        uv_layer = bm.loops.layers.uv.get("PaletteUV") or bm.loops.layers.uv.new("PaletteUV")
        col_layer = bm.loops.layers.float_color.get("PaletteData") or bm.loops.layers.float_color.new("PaletteData")
        for pi, state in state_dict.items():
            if not 0 <= pi < len(bm.faces):
                continue
            f = bm.faces[pi]
            f[token_layer] = int(state["token"])
            f[profile_layer] = int(state["profile"])
            f[cat_layer] = int(state["category"])
            for offset, loop in enumerate(f.loops):
                if offset < len(state["uv"]):
                    loop[uv_layer].uv = state["uv"][offset]
                if offset < len(state["data"]):
                    loop[col_layer] = state["data"][offset]
        bmesh.update_edit_mesh(mesh)
        return

    token, profile, palette_uv, palette_data = _ensure_mesh_attributes(mesh)
    category = _ensure_int_face_attribute(mesh, COLOR_PROFILE_SLOT_ATTRIBUTE)
    for pi, state in state_dict.items():
        if not 0 <= pi < len(mesh.polygons):
            continue
        polygon = mesh.polygons[pi]
        if pi < len(token.data):
            token.data[pi].value = int(state["token"])
        if pi < len(profile.data):
            profile.data[pi].value = int(state["profile"])
        if pi < len(category.data):
            category.data[pi].value = int(state["category"])
        for offset, loop_index in enumerate(polygon.loop_indices):
            if loop_index < len(palette_uv.data):
                palette_uv.data[loop_index].uv = state["uv"][offset]
            if loop_index < len(palette_data.data):
                palette_data.data[loop_index].color = state["data"][offset]
    mesh.update()


def _capture_color_profile_face(obj, polygon_index: int):
    cap = _capture_color_profile_polygons(obj, [int(polygon_index)])
    return cap.get(int(polygon_index), {
        "token": INVALID_INDEX,
        "profile": INVALID_INDEX,
        "category": INVALID_INDEX,
        "uv": [],
        "data": [],
    })


def _restore_color_profile_face(obj, polygon_index: int, state):
    _restore_color_profile_polygons(obj, {int(polygon_index): state})


def apply_color_profile_to_selection(
    context,
    color_index: int,
    surface_profile: int,
    category_index: int = INVALID_INDEX,
):
    if STATE.catalog is None:
        raise RuntimeError("上色前，请先加载 RGB565 角色画板 v3。")
    color_index = int(color_index)
    surface_profile = int(surface_profile)
    category_index = int(category_index)
    active = getattr(context.view_layer.objects, "active", None)
    restore_edit = active is not None and active.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    object_count = 0
    face_count = 0
    try:
        for obj in _selected_mesh_objects(context):
            mesh = obj.data
            selected = [polygon for polygon in mesh.polygons if polygon.select]
            if not selected:
                if restore_edit:
                    continue
                selected = list(mesh.polygons)
            painted = _apply_color_profile_to_polygons(
                obj,
                [polygon.index for polygon in selected],
                color_index,
                surface_profile,
                category_index,
            )
            if painted:
                object_count += 1
                face_count += painted
    finally:
        if restore_edit and active is not None:
            context.view_layer.objects.active = active
            bpy.ops.object.mode_set(mode="EDIT")
    if face_count:
        STATE.recent = [color_index, *[value for value in STATE.recent if value != color_index]][:64]
        _write_preferences()
    elif restore_edit:
        raise ValueError("应用颜色和表面材质 Profile 前，请至少选择一个面。")
    return object_count, face_count


def remap_color_profile_category(context, category_index: int, color_index: int, surface_profile: int):
    if STATE.catalog is None:
        raise RuntimeError("更新分类前，请先加载 RGB565 角色画板 v3。")
    category_index = int(category_index)
    color_index = int(color_index)
    surface_profile = int(surface_profile)
    if category_index < 0:
        raise ValueError("角色配色 Profile 条目缺少有效分类 ID。")
    x, y = color_coordinates(color_index)
    token_value = logical_token(color_index, surface_profile)
    expected_uv = ((x + 0.5) / WIDTH, (y + 0.5) / HEIGHT)
    alpha = int(STATE.catalog["surfaceProfiles"][surface_profile]["alphaByte"]) / 255.0
    active = getattr(context.view_layer.objects, "active", None)
    restore_edit = active is not None and active.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    object_count = 0
    face_count = 0
    try:
        for obj in _profile_mesh_objects(context):
            mesh = obj.data
            category_attr = mesh.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
            if category_attr is None or category_attr.domain != "FACE" or category_attr.data_type != "INT":
                continue
            token_attr, profile_attr, palette_uv, palette_data = _ensure_mesh_attributes(mesh)
            remapped = 0
            for polygon in mesh.polygons:
                if int(category_attr.data[polygon.index].value) != category_index:
                    continue
                token_attr.data[polygon.index].value = token_value
                profile_attr.data[polygon.index].value = surface_profile
                for loop_index in polygon.loop_indices:
                    palette_uv.data[loop_index].uv = expected_uv
                    current = palette_data.data[loop_index].color
                    palette_data.data[loop_index].color = (current[0], current[1], current[2], alpha)
                remapped += 1
            if remapped:
                obj["paletteRepaintRequired"] = any(value.value < 0 for value in token_attr.data)
                mesh["paletteRepaintRequired"] = obj["paletteRepaintRequired"]
                mesh.update()
                object_count += 1
                face_count += remapped
    finally:
        if restore_edit and active is not None:
            context.view_layer.objects.active = active
            bpy.ops.object.mode_set(mode="EDIT")
    return object_count, face_count


def count_color_profile_category_faces(context, category_index: int) -> int:
    active = getattr(context.view_layer.objects, "active", None)
    restore_edit = active is not None and active.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    count = 0
    try:
        for obj in _profile_mesh_objects(context):
            attribute = obj.data.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
            if attribute is None or attribute.domain != "FACE" or attribute.data_type != "INT":
                continue
            count += sum(1 for item in attribute.data if int(item.value) == int(category_index))
    finally:
        if restore_edit and active is not None:
            context.view_layer.objects.active = active
            bpy.ops.object.mode_set(mode="EDIT")
    return count


def _build_preview_image():
    if not STATE.rgb8:
        raise RuntimeError("尚未加载 RGB565 角色画板 v3。")
    image = bpy.data.images.get("WGameRgb565LookupAtlas_Technical")
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new("WGameRgb565LookupAtlas_Technical", width=WIDTH, height=HEIGHT, alpha=False, float_buffer=False)
    try:
        image.colorspace_settings.name = "sRGB"
    except Exception:
        pass
    pixels = [0.0] * (COLOR_COUNT * 4)
    for index in range(COLOR_COUNT):
        source = index * 3
        target = index * 4
        pixels[target] = STATE.rgb8[source] / 255.0
        pixels[target + 1] = STATE.rgb8[source + 1] / 255.0
        pixels[target + 2] = STATE.rgb8[source + 2] / 255.0
        pixels[target + 3] = 1.0
    image.pixels.foreach_set(pixels)
    image.update()
    image.pack()
    return image


def _build_profile_ramp(nodes, name: str, field: str):
    node = nodes.new("ShaderNodeValToRGB")
    node.name = name
    node.label = name
    ramp = node.color_ramp
    ramp.interpolation = "CONSTANT"
    while len(ramp.elements) > 1:
        ramp.elements.remove(ramp.elements[-1])
    first = ramp.elements[0]
    first.position = 0.0
    value = float(STATE.catalog["surfaceProfiles"][0].get(field, 0.0))
    first.color = (value, value, value, 1.0)
    for idx, profile in enumerate(STATE.catalog["surfaceProfiles"][1:], start=1):
        value = float(profile.get(field, 0.0))
        # Place step boundary at midpoint between profile buckets: (idx * 16) / 255.0
        element = ramp.elements.new((idx * 16) / 255.0)
        element.color = (value, value, value, 1.0)
    return node


def build_preview_material(context):
    image = _build_preview_image()
    material = bpy.data.materials.get("WGame_ColorPalette_SurfaceProfiles_Preview")
    if material is None:
        material = bpy.data.materials.new("WGame_ColorPalette_SurfaceProfiles_Preview")
    material.use_nodes = True
    material["catalogHash"] = STATE.catalog.get("catalogHash", "")
    material["surfaceProfileSource"] = "PaletteData.A"
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    texture.extension = "CLIP"
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map = "PaletteUV"
    palette_data = nodes.new("ShaderNodeVertexColor")
    palette_data.layer_name = "PaletteData"
    roughness = _build_profile_ramp(nodes, "Surface Profile Roughness", "roughness")
    metallic = _build_profile_ramp(nodes, "Surface Profile Metallic", "metallic")
    specular = _build_profile_ramp(nodes, "Surface Profile Specular", "specular")
    links.new(uv.outputs.get("UV"), texture.inputs.get("Vector"))
    base_color_output = texture.outputs.get("Color")
    face_path = str(getattr(context.scene, "wgame_face_detail_texture_path", "")).strip()
    if face_path:
        resolved_face_path = Path(bpy.path.abspath(face_path)).resolve()
        if not resolved_face_path.is_file():
            raise FileNotFoundError("角色 FaceDetail 贴图不存在。")
        face_image = bpy.data.images.load(str(resolved_face_path), check_existing=True)
        if tuple(face_image.size[:]) != (512, 512) or int(face_image.channels) != 4:
            raise ValueError("角色 FaceDetail 必须是 512×512 RGBA。")
        face_texture = nodes.new("ShaderNodeTexImage")
        face_texture.name = "Character FaceDetail RGBA"
        face_texture.image = face_image
        face_texture.interpolation = "Linear"
        face_texture.extension = "CLIP"
        face_uv = nodes.new("ShaderNodeUVMap")
        face_uv.uv_map = "DetailUV"
        mix = nodes.new("ShaderNodeMixRGB")
        mix.blend_type = "MIX"
        intensity = nodes.new("ShaderNodeMath")
        intensity.operation = "MULTIPLY"
        intensity.inputs[1].default_value = max(0.0, min(1.0, float(context.scene.wgame_face_detail_intensity)))
        links.new(face_uv.outputs.get("UV"), face_texture.inputs.get("Vector"))
        links.new(face_texture.outputs.get("Alpha"), intensity.inputs[0])
        links.new(intensity.outputs[0], mix.inputs[0])
        links.new(texture.outputs.get("Color"), mix.inputs[1])
        links.new(face_texture.outputs.get("Color"), mix.inputs[2])
        base_color_output = mix.outputs[0]
        material["faceDetailTexture"] = str(resolved_face_path)
    links.new(base_color_output, shader.inputs.get("Base Color"))
    links.new(palette_data.outputs.get("Alpha"), roughness.inputs.get("Fac"))
    links.new(palette_data.outputs.get("Alpha"), metallic.inputs.get("Fac"))
    links.new(palette_data.outputs.get("Alpha"), specular.inputs.get("Fac"))
    links.new(roughness.outputs.get("Color"), shader.inputs.get("Roughness"))
    links.new(metallic.outputs.get("Color"), shader.inputs.get("Metallic"))
    specular_input = shader.inputs.get("Specular IOR Level") or shader.inputs.get("Specular")
    if specular_input is not None:
        links.new(specular.outputs.get("Color"), specular_input)

    # Subsurface scattering for soft and glossy skin (profiles 0 and 1)
    sss_node = nodes.new("ShaderNodeValToRGB")
    sss_node.name = "Surface Profile Subsurface"
    sss_node.label = "Surface Profile Subsurface"
    sss_ramp = sss_node.color_ramp
    sss_ramp.interpolation = "CONSTANT"
    sss_ramp.elements[0].position = 0.0
    sss_ramp.elements[0].color = (0.35, 0.35, 0.35, 1.0)
    el1 = sss_ramp.elements.new(16.0 / 255.0)
    el1.color = (0.28, 0.28, 0.28, 1.0)
    el2 = sss_ramp.elements.new(32.0 / 255.0)
    el2.color = (0.0, 0.0, 0.0, 1.0)

    links.new(palette_data.outputs.get("Alpha"), sss_node.inputs.get("Fac"))
    subsurface_weight = shader.inputs.get("Subsurface Weight") or shader.inputs.get("Subsurface")
    if subsurface_weight is not None:
        links.new(sss_node.outputs.get("Color"), subsurface_weight)
    subsurface_scale = shader.inputs.get("Subsurface Scale")
    if subsurface_scale is not None:
        subsurface_scale.default_value = 0.015
    subsurface_radius = shader.inputs.get("Subsurface Radius")
    if subsurface_radius is not None:
        subsurface_radius.default_value = (1.0, 0.45, 0.3)
    links.new(shader.outputs.get("BSDF"), output.inputs.get("Surface"))
    for obj in _selected_mesh_objects(context):
        slot = next((index for index, item in enumerate(obj.data.materials) if item == material), None)
        if slot is None:
            obj.data.materials.append(material)
            slot = len(obj.data.materials) - 1
        for polygon in obj.data.polygons:
            polygon.material_index = slot
    return material


def _append_issue(issues, message):
    if len(issues) < 256:
        issues.append(message)


def _validate_mesh_object_data(context):
    issues = []
    counts = {}
    invalid_faces = 0
    for obj in _selected_mesh_objects(context):
        mesh = obj.data
        token_attr = mesh.attributes.get("PaletteToken")
        profile_attr = mesh.attributes.get("SurfaceProfile")
        palette_uv = mesh.uv_layers.get("PaletteUV")
        palette_data = mesh.color_attributes.get("PaletteData")
        contract_valid = True
        if not token_attr or token_attr.domain != "FACE" or token_attr.data_type != "INT":
            _append_issue(issues, f"{obj.name}：PaletteToken 必须是 FACE / INT。")
            contract_valid = False
        elif len(token_attr.data) != len(mesh.polygons):
            _append_issue(issues, f"{obj.name}：PaletteToken 面数据数量 {len(token_attr.data)} 与网格面数量 {len(mesh.polygons)} 不一致。")
            contract_valid = False
        if not profile_attr or profile_attr.domain != "FACE" or profile_attr.data_type != "INT":
            _append_issue(issues, f"{obj.name}：SurfaceProfile 必须是 FACE / INT。")
            contract_valid = False
        elif len(profile_attr.data) != len(mesh.polygons):
            _append_issue(issues, f"{obj.name}：SurfaceProfile 面数据数量 {len(profile_attr.data)} 与网格面数量 {len(mesh.polygons)} 不一致。")
            contract_valid = False
        if not palette_uv:
            _append_issue(issues, f"{obj.name}：PaletteUV 必须是 CORNER / FLOAT2 UV 层。")
            contract_valid = False
        elif len(palette_uv.data) != len(mesh.loops):
            _append_issue(issues, f"{obj.name}：PaletteUV 角数据数量 {len(palette_uv.data)} 与网格角数量 {len(mesh.loops)} 不一致。")
            contract_valid = False
        if not palette_data or palette_data.domain != "CORNER" or palette_data.data_type != "FLOAT_COLOR":
            _append_issue(issues, f"{obj.name}：PaletteData 必须是 CORNER / FLOAT_COLOR RGBA。")
            contract_valid = False
        elif len(palette_data.data) != len(mesh.loops):
            _append_issue(issues, f"{obj.name}：PaletteData 角数据数量 {len(palette_data.data)} 与网格角数量 {len(mesh.loops)} 不一致。")
            contract_valid = False
        if not contract_valid:
            invalid_faces += sum(max(1, len(polygon.vertices) - 2) for polygon in mesh.polygons)
            continue
        for polygon in mesh.polygons:
            exported_faces = max(1, len(polygon.vertices) - 2)
            token = int(token_attr.data[polygon.index].value)
            profile = int(profile_attr.data[polygon.index].value)
            valid = 0 <= token < LOGICAL_TOKEN_COUNT and 0 <= profile < PROFILE_COUNT
            if valid:
                color_index = token // PROFILE_COUNT
                valid = token == logical_token(color_index, profile)
            else:
                color_index = -1
            if not valid:
                invalid_faces += exported_faces
                _append_issue(issues, f"{obj.name}：面 {polygon.index} 的逻辑 Token/Profile {token}/{profile} 无效。")
                continue
            x, y = color_coordinates(color_index)
            expected_uv = ((x + 0.5) / WIDTH, (y + 0.5) / HEIGHT)
            expected_alpha = int(STATE.catalog["surfaceProfiles"][profile]["alphaByte"]) / 255.0
            for loop_index in polygon.loop_indices:
                actual_uv = palette_uv.data[loop_index].uv
                actual_alpha = float(palette_data.data[loop_index].color[3])
                if abs(actual_uv.x - expected_uv[0]) > 1.0e-6 or abs(actual_uv.y - expected_uv[1]) > 1.0e-6:
                    valid = False
                    _append_issue(issues, f"{obj.name}：面 {polygon.index} 的 PaletteUV 不在精确颜色像素中心。")
                    break
                if abs(actual_alpha - expected_alpha) > 1.0e-6:
                    valid = False
                    _append_issue(issues, f"{obj.name}：面 {polygon.index} 的 PaletteData.A 与 SurfaceProfile {profile} 不一致。")
                    break
            if not valid:
                invalid_faces += exported_faces
                continue
            counts[token] = counts.get(token, 0) + exported_faces
    return issues, counts, invalid_faces


def palette_uv_repair_plan(context):
    """Only recover derived UVs when token, category, surface and alpha agree."""
    if STATE.catalog is None:
        raise ValueError("请先加载角色画板 Catalog")
    categories = {int(item.category_index): (int(item.color_index), int(item.surface_profile))
                  for item in context.scene.wgame_character_color_profile_colors}
    plan = []
    for obj in _selected_mesh_objects(context):
        if obj.mode != "OBJECT":
            raise ValueError("请先退出网格编辑模式再修复 PaletteUV")
        mesh = obj.data
        attrs = [mesh.attributes.get(name) for name in ("PaletteToken", "SurfaceProfile", COLOR_PROFILE_SLOT_ATTRIBUTE)]
        if any(attr is None or attr.domain != "FACE" or attr.data_type != "INT" or len(attr.data) != len(mesh.polygons) for attr in attrs):
            continue
        token, surface, category = attrs
        uv, data = mesh.uv_layers.get("PaletteUV"), mesh.color_attributes.get("PaletteData")
        if not uv or len(uv.data) != len(mesh.loops) or not data or data.domain != "CORNER" or data.data_type != "FLOAT_COLOR" or len(data.data) != len(mesh.loops):
            continue
        for face in mesh.polygons:
            value = token.data[face.index].value
            profile = surface.data[face.index].value
            color = value // PROFILE_COUNT
            if not 0 <= value < LOGICAL_TOKEN_COUNT or not 0 <= profile < PROFILE_COUNT or value % PROFILE_COUNT != profile:
                continue
            if categories.get(category.data[face.index].value) != (color, profile):
                continue
            alpha = int(STATE.catalog["surfaceProfiles"][profile]["alphaByte"]) / 255.0
            if any(not math.isfinite(data.data[i].color[3]) or abs(data.data[i].color[3] - alpha) > 1e-6 for i in face.loop_indices):
                continue
            x, y = color_coordinates(color)
            expected = ((x+.5)/WIDTH, (y+.5)/HEIGHT)
            if any(any(not math.isfinite(uv.data[i].uv[a]) or abs(uv.data[i].uv[a]-expected[a]) > 1e-6 for a in (0,1)) for i in face.loop_indices):
                plan.append((obj, face.index, expected))
    return plan


def apply_palette_uv_repair(plan):
    for obj, face_index, expected in plan:
        mesh = obj.data
        uv = mesh.uv_layers["PaletteUV"]
        for index in mesh.polygons[face_index].loop_indices:
            uv.data[index].uv = expected
    for obj in {row[0] for row in plan}:
        obj.data.update()
    return len(plan)


def validate_meshes(context):
    active = getattr(context.view_layer.objects, "active", None)
    restore_edit = active is not None and active.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    try:
        return _validate_mesh_object_data(context)
    finally:
        if restore_edit and active is not None:
            context.view_layer.objects.active = active
            bpy.ops.object.mode_set(mode="EDIT")


def _remove_attribute(mesh, name):
    attribute = mesh.attributes.get(name)
    if attribute is not None:
        mesh.attributes.remove(attribute)


def migrate_selected_to_global(context):
    blend_path = Path(bpy.data.filepath) if bpy.data.filepath else None
    if blend_path is None:
        raise RuntimeError("执行破坏性画板迁移前，请先保存 .blend 文件。")
    stamp = _datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    backup_path = blend_path.with_name(f"{blend_path.stem}.pre-rgb565-palette-v3-{stamp}{blend_path.suffix}")
    try:
        result = bpy.ops.wm.save_as_mainfile(filepath=str(backup_path), copy=True)
        if "FINISHED" not in result:
            raise RuntimeError("Blender 未能完成迁移备份写入。")
    except Exception:
        shutil.copy2(blend_path, backup_path)
    active = getattr(context, "object", None)
    restore_edit = active is not None and active.mode == "EDIT"
    if restore_edit:
        bpy.ops.object.mode_set(mode="OBJECT")
    reports = []
    total_faces = 0
    for obj in [item for item in context.scene.objects if item.type == "MESH"]:
        mesh = obj.data
        token_attr, profile_attr, palette_uv, palette_data = _ensure_mesh_attributes(mesh)
        category_attr = _ensure_int_face_attribute(mesh, COLOR_PROFILE_SLOT_ATTRIBUTE)
        for polygon in mesh.polygons:
            token_attr.data[polygon.index].value = INVALID_INDEX
            profile_attr.data[polygon.index].value = INVALID_INDEX
            category_attr.data[polygon.index].value = INVALID_INDEX
            for loop_index in polygon.loop_indices:
                palette_uv.data[loop_index].uv = (-1.0, -1.0)
                current = palette_data.data[loop_index].color
                palette_data.data[loop_index].color = (current[0], current[1], current[2], 0.0)
        for legacy in ("PaletteSlot", "PaletteProfileRow", "PaletteRow", "MaterialClass", "MaterialClassV1"):
            if legacy not in {"PaletteToken", "SurfaceProfile"}:
                _remove_attribute(mesh, legacy)
            obj.pop(legacy, None)
            mesh.pop(legacy, None)
        obj["paletteRepaintRequired"] = True
        mesh["paletteRepaintRequired"] = True
        mesh.update()
        triangulated = sum(max(1, len(polygon.vertices) - 2) for polygon in mesh.polygons)
        total_faces += triangulated
        reports.append({"object": obj.name, "mesh": mesh.name, "polygonCount": len(mesh.polygons), "invalidFaceCount": triangulated})
    context.scene["paletteRepaintRequired"] = True
    for legacy in ("PaletteProfileRow", "paletteProfileRow", "palette_profile_row", "defaultRow", "profileRow"):
        context.scene.pop(legacy, None)
    report_path = blend_path.with_name(f"{blend_path.stem}.rgb565-palette-v3-migration-{stamp}.json")
    report = {
        "format": "mx.wgameColorPaletteMigrationReport.v3",
        "sourceBlend": str(blend_path),
        "backupBlend": str(backup_path),
        "catalogHash": (STATE.catalog or {}).get("catalogHash", ""),
        "paletteRepaintRequired": True,
        "invalidFaceCount": total_faces,
        "objects": reports,
        "invalidState": {"PaletteToken": -1, "SurfaceProfile": -1, "ColorProfileSlot": -1, "PaletteUV": [-1.0, -1.0], "PaletteDataAlpha": 0.0},
        "preserved": ["geometry", "armature", "weights", "UV0", "FaceDetailUV", "PaletteData.RGB"],
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if restore_edit and active is not None:
        context.view_layer.objects.active = active
        bpy.ops.object.mode_set(mode="EDIT")
    return backup_path, report_path, total_faces


def write_usage_sidecar(context):
    issues, counts, invalid_faces = validate_meshes(context)
    package_root = Path(bpy.path.abspath(context.scene.wgame_palette_package_root)).resolve()
    manifest_path = package_root / "manifest.json"
    if not manifest_path.is_file():
        raise RuntimeError("角色包目录中必须包含 manifest.json。")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    model_text = context.scene.wgame_palette_exported_model_path.strip()
    if model_text:
        model_path = Path(bpy.path.abspath(model_text)).resolve()
    else:
        candidates = sorted((package_root / "resources" / "models").glob("*.fbx"))
        model_path = candidates[0] if candidates else None
    if model_path is None or not model_path.is_file():
        raise RuntimeError("请设置导出模型路径，或在角色包 resources/models 下放置一个 FBX。")
    token_usages = []
    for token, face_count in sorted(counts.items()):
        color_index = token // PROFILE_COUNT
        profile = token % PROFILE_COUNT
        profile_id = STATE.catalog["surfaceProfiles"][profile]["profileId"]
        token_usages.append({
            "tokenId": surface_token_id(color_index, profile),
            "logicalToken": token,
            "colorIndex": color_index,
            "surfaceProfileId": profile_id,
            "faceCount": face_count,
            "source": "character",
        })
    document = {
        "format": USAGE_FORMAT,
        "schemaVersion": "2.0",
        "packageId": str(manifest.get("packageId", "")),
        "catalogHash": STATE.catalog.get("catalogHash", ""),
        "modelHash": "sha256:" + hashlib.sha256(model_path.read_bytes()).hexdigest(),
        "paletteRepaintRequired": invalid_faces > 0,
        "invalidFaceCount": invalid_faces,
        "tokenUsages": token_usages,
    }
    target = package_root / "config" / "character_palette_usage.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    config_files = manifest.setdefault("configFiles", [])
    usage_rel = "config/character_palette_usage.json"
    if usage_rel not in config_files:
        config_files.append(usage_rel)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return target, issues, invalid_faces


class WG_CharacterColorProfileItem(PropertyGroup):
    slot_id: StringProperty(name="Slot ID")
    label: StringProperty(name="名称")
    category_index: IntProperty(name="分类 ID", default=INVALID_INDEX, min=INVALID_INDEX)
    surface_profile: EnumProperty(name="表面材质 Profile", items=PROFILE_ITEMS, default="0", update=_profile_item_surface_update)
    color_index: IntProperty(name="颜色索引", default=0, min=0, max=COLOR_COUNT - 1)
    hex: StringProperty(name="精确 Hex", default="#000000")
    color_preview: FloatVectorProperty(
        name="颜色",
        subtype="COLOR_GAMMA",
        size=3,
        min=0.0,
        max=1.0,
        default=(0.0, 0.0, 0.0),
        update=_profile_item_color_update,
    )


class WG_UL_CharacterColorProfileItems(UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_propname, index):
        row = layout.row(align=True)
        swatch = row.row(align=True)
        swatch.scale_x = 0.8
        swatch.prop(item, "color_preview", text="")
        row.label(text=f"C{int(item.category_index):02d}" if int(item.category_index) >= 0 else "C--")
        row.label(text=item.label or item.slot_id or f"颜色 {index + 1:02d}")


class WG_ColorPaletteItem(PropertyGroup):
    color_index: IntProperty()
    hex: StringProperty()
    axis: StringProperty()
    color_id: StringProperty()


class WG_UL_ColorPaletteItems(UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_propname, index):
        row = layout.row(align=True)
        row.label(text=f"#{item.color_index}")
        row.label(text=item.hex)
        row.label(text=item.axis)


class WG_OT_ProfileAddColor(Operator):
    bl_idname = "wgame.character_color_profile_add"
    bl_label = "添加当前颜色"
    bl_description = "把当前 RGB565 颜色加入该角色的配色 Profile"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        scene = context.scene
        if len(scene.wgame_character_color_profile_colors) >= 256:
            self.report({"ERROR"}, "一个角色配色 Profile 最多支持 256 种精选颜色。")
            return {"CANCELLED"}
        color_index = int(scene.wgame_palette_selected_color)
        position = len(scene.wgame_character_color_profile_colors)
        label = str(scene.wgame_character_color_profile_new_label).strip() or f"颜色 {position + 1:02d}"
        used = {str(item.slot_id) for item in scene.wgame_character_color_profile_colors}
        item = scene.wgame_character_color_profile_colors.add()
        item.slot_id = _profile_slot_id(label, used)
        item.label = label
        item.category_index = _next_profile_category_index(scene)
        item.surface_profile = str(scene.wgame_palette_selected_profile)
        _set_profile_item_color(item, color_index)
        scene.wgame_character_color_profile_active = position
        scene.wgame_character_color_profile_new_label = ""
        self.report({"INFO"}, f"已将 {label} {item.hex} 加入角色配色 Profile。")
        return {"FINISHED"}


class WG_OT_ProfileImportUsedColors(Operator):
    bl_idname = "wgame.character_color_profile_import_used"
    bl_label = "从模型导入已用颜色"
    bl_description = "把选中网格当前引用的全部有效全局颜色加入角色配色 Profile"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        scene = context.scene
        used_indices = sorted(_used_color_indices(context))
        if not used_indices:
            self.report({"WARNING"}, "选中的网格没有包含任何有效的已上色颜色。")
            return {"CANCELLED"}
        existing_indices = {int(item.color_index) for item in scene.wgame_character_color_profile_colors}
        additions = [color_index for color_index in used_indices if color_index not in existing_indices]
        if len(scene.wgame_character_color_profile_colors) + len(additions) > 256:
            self.report({"ERROR"}, "导入后会超过角色配色 Profile 的 256 色上限。")
            return {"CANCELLED"}
        used_slots = {str(item.slot_id) for item in scene.wgame_character_color_profile_colors}
        first_new = len(scene.wgame_character_color_profile_colors)
        for color_index in additions:
            item = scene.wgame_character_color_profile_colors.add()
            item.label = f"模型已用 {color_hex(color_index)}"
            item.slot_id = _profile_slot_id(f"used_{color_index}", used_slots)
            item.category_index = _next_profile_category_index(scene)
            item.surface_profile = str(scene.wgame_palette_selected_profile)
            used_slots.add(item.slot_id)
            _set_profile_item_color(item, color_index)
        if additions:
            scene.wgame_character_color_profile_active = first_new
            self.report({"INFO"}, f"已向角色配色 Profile 加入 {len(additions)} 种模型已用颜色。")
        else:
            self.report({"INFO"}, "选中网格使用的所有颜色都已存在于角色配色 Profile 中。")
        return {"FINISHED"}


class WG_OT_ProfileUpdateColor(Operator):
    bl_idname = "wgame.character_color_profile_update"
    bl_label = "更新当前分类"
    bl_description = "用当前颜色和表面材质更新所选 Profile 分类及其全部网格面"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        scene = context.scene
        item = _active_profile_item(scene)
        if item is None:
            self.report({"ERROR"}, "请先选择一个角色配色 Profile 条目。")
            return {"CANCELLED"}
        color_index = int(scene.wgame_palette_selected_color)
        surface_profile = int(scene.wgame_palette_selected_profile)
        _set_profile_item_color(item, color_index)
        item.surface_profile = str(surface_profile)
        objects, faces = remap_color_profile_category(
            context,
            int(item.category_index),
            color_index,
            surface_profile,
        )
        self.report({"INFO"}, f"已更新 {item.label} 为 {item.hex}，并同步 {objects} 个对象上的 {faces} 个面。")
        return {"FINISHED"}


class WG_OT_ProfileUpdateSurface(Operator):
    bl_idname = "wgame.character_color_profile_update_surface"
    bl_label = "更新当前分类全模型材质"
    bl_description = "免选面：直接将当前所选表面材质赋予当前分类，并一键同步全模型所有属于该分类的面"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return (
            getattr(context, "object", None) is not None
            and context.object.type == "MESH"
            and _active_profile_item(context.scene) is not None
        )

    def execute(self, context):
        scene = context.scene
        item = _active_profile_item(scene)
        if item is None:
            self.report({"ERROR"}, "请先在上方列表中选择一个角色配色 Profile。")
            return {"CANCELLED"}
        surface_profile = int(scene.wgame_palette_selected_profile)
        item.surface_profile = str(surface_profile)
        objects, faces = remap_color_profile_category(
            context,
            int(item.category_index),
            int(item.color_index),
            surface_profile,
        )
        prof_name = STATE.catalog["surfaceProfiles"][surface_profile].get("name", str(surface_profile))
        self.report({"INFO"}, f"已将分类 [{item.label or item.slot_id}] 的材质更新为 [{surface_profile:02d} {prof_name}]，并同步全模型 {faces} 个面！")
        return {"FINISHED"}


class WG_OT_ProfileRemoveColor(Operator):
    bl_idname = "wgame.character_color_profile_remove"
    bl_label = "移除当前颜色"
    bl_description = "移除没有被网格面引用的角色配色 Profile 分类"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        scene = context.scene
        index = int(scene.wgame_character_color_profile_active)
        if not 0 <= index < len(scene.wgame_character_color_profile_colors):
            self.report({"ERROR"}, "请先选择一个角色配色 Profile 条目。")
            return {"CANCELLED"}
        selected_item = scene.wgame_character_color_profile_colors[index]
        referenced_faces = count_color_profile_category_faces(context, int(selected_item.category_index))
        if referenced_faces:
            self.report({"ERROR"}, f"分类 {selected_item.label} 仍被 {referenced_faces} 个面引用，不能直接移除。")
            return {"CANCELLED"}
        label = selected_item.label
        scene.wgame_character_color_profile_colors.remove(index)
        scene.wgame_character_color_profile_active = min(index, len(scene.wgame_character_color_profile_colors) - 1)
        item = _active_profile_item(scene)
        if item is not None:
            _set_scene_color(scene, int(item.color_index))
        self.report({"INFO"}, f"已从 Profile 移除未被引用的分类 {label}。")
        return {"FINISHED"}


class WG_OT_ProfileSave(Operator):
    bl_idname = "wgame.character_color_profile_save"
    bl_label = "保存角色配色 Profile"
    bl_description = "把该角色的精选配色 Profile 写入版本化 JSON"

    def execute(self, context):
        try:
            path = save_character_color_profile(context.scene)
            self.report({"INFO"}, f"角色配色 Profile 已保存：{path}")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_ProfileLoad(Operator):
    bl_idname = "wgame.character_color_profile_load"
    bl_label = "加载角色配色 Profile"
    bl_description = "加载版本化角色配色 Profile JSON 文件"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        try:
            path = load_character_color_profile(context.scene)
            self.report({"INFO"}, f"角色配色 Profile 已加载：{path}")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_ProfileSetHex(Operator):
    bl_idname = "wgame.character_color_profile_set_hex"
    bl_label = "应用 Hex"
    bl_description = "把 RGB Hex 颜色量化到最近的唯一 RGB565 地址"

    def execute(self, context):
        value = str(context.scene.wgame_palette_hex_input).strip()
        match = re.fullmatch(r"#?([0-9a-fA-F]{6})", value)
        if match is None:
            self.report({"ERROR"}, "Hex 必须包含正好 6 位十六进制数字，例如 #F2D4C5。")
            return {"CANCELLED"}
        packed = int(match.group(1), 16)
        color_index = color_index_from_rgb8((packed >> 16) & 255, (packed >> 8) & 255, packed & 255)
        _set_scene_color(context.scene, color_index)
        rebuild_color_items(context)
        self.report({"INFO"}, f"已选择最近的 RGB565 颜色 {color_hex(color_index)}（索引 {color_index}）。")
        return {"FINISHED"}


class WG_OT_ProfileReadFace(Operator):
    bl_idname = "wgame.character_color_profile_read_face"
    bl_label = "读取选中面"
    bl_description = "从第一个已上色的选中面读取颜色和表面材质 Profile"

    def execute(self, context):
        try:
            color_index, surface_profile, in_profile = color_profile_from_selected_face(context)
            suffix = "" if in_profile else "；重新上色前请先把它加入角色配色 Profile"
            self.report({"INFO" if in_profile else "WARNING"}, f"已读取 {color_hex(color_index)} / 表面材质 {surface_profile}{suffix}。")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_PaletteRefresh(Operator):
    bl_idname = "wgame.color_palette_refresh"
    bl_label = "刷新全局色库"

    def execute(self, context):
        try:
            load_catalog(context, prefer_server=True)
            self.report({"INFO"}, f"已加载 {COLOR_COUNT:,} 种颜色和 {PROFILE_COUNT} 个表面材质 Profile。")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_PalettePage(Operator):
    bl_idname = "wgame.color_palette_page"
    bl_label = "切换颜色页"
    direction: IntProperty(default=1)

    def execute(self, context):
        page_count = max(1, math.ceil(len(STATE.filtered_indices) / PAGE_SIZE))
        context.scene.wgame_palette_page = max(0, min(page_count - 1, context.scene.wgame_palette_page + self.direction))
        rebuild_color_items(context)
        return {"FINISHED"}


class WG_OT_PaletteApply(Operator):
    bl_idname = "wgame.color_palette_apply"
    bl_label = "应用颜色与表面材质"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return (
            getattr(context, "object", None) is not None
            and context.object.type == "MESH"
            and _active_profile_item(context.scene) is not None
        )

    def execute(self, context):
        try:
            item = _active_profile_item(context.scene)
            if item is None:
                raise ValueError("上色前，请先配置并选择一个角色配色 Profile 颜色。")
            context.scene.wgame_palette_selected_color = int(item.color_index)
            surface_profile = int(item.surface_profile)
            context.scene.wgame_palette_selected_profile = str(surface_profile)
            objects, faces = apply_color_profile_to_selection(
                context,
                int(item.color_index),
                surface_profile,
                int(item.category_index),
            )
            self.report({"INFO"}, f"已在 {objects} 个对象上完成 {faces} 个面的上色。")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_PaletteApplySurfaceOnly(Operator):
    bl_idname = "wgame.color_palette_apply_surface_only"
    bl_label = "仅改选中面材质 (保留原色)"
    bl_description = "局部替换：保持选中面原本的颜色不变，只将物理材质（反光/粗糙度/金属度）更新为当前所选材质"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return (
            getattr(context, "object", None) is not None
            and context.object.type == "MESH"
        )

    def execute(self, context):
        if STATE.catalog is None:
            self.report({"ERROR"}, "请先加载角色画板。")
            return {"CANCELLED"}
        surface_profile = int(context.scene.wgame_palette_selected_profile)
        alpha = int(STATE.catalog["surfaceProfiles"][surface_profile]["alphaByte"]) / 255.0

        target = context.object
        was_edit = target.mode == "EDIT"
        if was_edit:
            bpy.ops.object.mode_set(mode="OBJECT")

        mesh = target.data
        selected_indices = [p.index for p in mesh.polygons if p.select]
        if not selected_indices:
            if was_edit:
                bpy.ops.object.mode_set(mode="EDIT")
            self.report({"ERROR"}, "请在编辑模式下先选中要应用材质的面。")
            return {"CANCELLED"}

        token_attr, profile_attr, palette_uv, palette_data = _ensure_mesh_attributes(mesh)
        count = 0
        for pi in selected_indices:
            polygon = mesh.polygons[pi]
            old_token = token_attr.data[polygon.index].value
            old_color = old_token // PROFILE_COUNT if old_token >= 0 else 0
            new_token = logical_token(old_color, surface_profile)
            token_attr.data[polygon.index].value = new_token
            profile_attr.data[polygon.index].value = surface_profile
            for loop_index in polygon.loop_indices:
                current = palette_data.data[loop_index].color
                palette_data.data[loop_index].color = (current[0], current[1], current[2], alpha)
            count += 1

        mesh.update()
        if was_edit:
            bpy.ops.object.mode_set(mode="EDIT")

        prof_name = STATE.catalog["surfaceProfiles"][surface_profile].get("name", str(surface_profile))
        self.report({"INFO"}, f"已成功将表面材质更新为 [{surface_profile:02d} {prof_name}]（共 {count} 个面）。")
        return {"FINISHED"}


class WG_OT_PaletteSelectFaces(Operator):
    bl_idname = "wgame.color_palette_select_faces"
    bl_label = "按颜色选择面"
    bl_description = "选择属于当前配色 Profile 的面（支持全选或在选区内仅保留该颜色）"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return (
            getattr(context, "object", None) is not None
            and context.object.type == "MESH"
            and _active_profile_item(context.scene) is not None
        )

    filter_selection: BoolProperty(
        name="仅在选区中筛选",
        description="如果为真，仅在当前已选中的面中保留匹配的面；如果为假，则全网格选择",
        default=False,
    )

    def execute(self, context):
        target = getattr(context, "object", None)
        if target is None or target.type != "MESH":
            self.report({"ERROR"}, "请先选择一个网格对象。")
            return {"CANCELLED"}
        item = _active_profile_item(context.scene)
        if item is None:
            self.report({"ERROR"}, "请先在列表中选择目标角色配色 Profile。")
            return {"CANCELLED"}

        target_category = int(item.category_index)
        target_color = int(item.color_index)

        was_edit = target.mode == "EDIT"
        if was_edit:
            bpy.ops.object.mode_set(mode="OBJECT")

        mesh = target.data
        slot_attr = mesh.attributes.get("ColorProfileSlot")
        token_attr = mesh.attributes.get("PaletteToken")

        has_selection = any(p.select for p in mesh.polygons)
        filter_mode = self.filter_selection and has_selection

        matched_count = 0
        for pi, poly in enumerate(mesh.polygons):
            matches = False
            if slot_attr is not None and target_category >= 0:
                matches = (slot_attr.data[pi].value == target_category)
            elif token_attr is not None:
                token = token_attr.data[pi].value
                if 0 <= token < LOGICAL_TOKEN_COUNT:
                    matches = ((token // PROFILE_COUNT) == target_color)

            if filter_mode:
                if poly.select and matches:
                    poly.select = True
                    matched_count += 1
                else:
                    poly.select = False
            else:
                if matches:
                    poly.select = True
                    matched_count += 1
                else:
                    poly.select = False

        if was_edit:
            bpy.ops.object.mode_set(mode="EDIT")

        scope_str = "选区内" if filter_mode else "全模型"
        self.report(
            {"INFO"},
            f"已在{scope_str}选中 {matched_count} 个属于 C{target_category:02d} 的面。",
        )
        return {"FINISHED"}


class WG_OT_PaletteSelectLinkedColor(Operator):
    bl_idname = "wgame.color_palette_select_linked"
    bl_label = "选择连通色块"
    bl_description = "从当前选中的面出发，只沿着具有相同颜色/Profile的相邻面扩展选择（连通同色块）"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return (
            getattr(context, "object", None) is not None
            and context.object.type == "MESH"
        )

    def execute(self, context):
        target = getattr(context, "object", None)
        if target is None or target.type != "MESH":
            self.report({"ERROR"}, "请先选择一个网格对象。")
            return {"CANCELLED"}

        was_edit = target.mode == "EDIT"
        if was_edit:
            bpy.ops.object.mode_set(mode="OBJECT")

        mesh = target.data
        selected_indices = [p.index for p in mesh.polygons if p.select]
        if not selected_indices:
            if was_edit:
                bpy.ops.object.mode_set(mode="EDIT")
            self.report({"ERROR"}, "请在编辑模式下先选中色块上的任意一个面，作为连通选择的起点。")
            return {"CANCELLED"}

        slot_attr = mesh.attributes.get("ColorProfileSlot")
        token_attr = mesh.attributes.get("PaletteToken")

        vert_polys = [[] for _ in range(len(mesh.vertices))]
        for pi, poly in enumerate(mesh.polygons):
            for vi in poly.vertices:
                vert_polys[vi].append(pi)

        if slot_attr is not None and len(slot_attr.data) == len(mesh.polygons):
            poly_cats = [int(slot_attr.data[i].value) for i in range(len(mesh.polygons))]
        elif token_attr is not None and len(token_attr.data) == len(mesh.polygons):
            poly_cats = [int(token_attr.data[i].value // PROFILE_COUNT) for i in range(len(mesh.polygons))]
        else:
            poly_cats = [-1] * len(mesh.polygons)

        visited = set(selected_indices)
        q = list(selected_indices)
        init_cats = {poly_cats[pi] for pi in selected_indices}

        while q:
            curr = q.pop()
            for vi in mesh.polygons[curr].vertices:
                for npi in vert_polys[vi]:
                    if npi not in visited and poly_cats[npi] in init_cats:
                        visited.add(npi)
                        q.append(npi)

        for pi in visited:
            mesh.polygons[pi].select = True

        if was_edit:
            bpy.ops.object.mode_set(mode="EDIT")

        self.report({"INFO"}, f"已成功选中连通同色块（共计扩展选中 {len(visited)} 个面）。")
        return {"FINISHED"}


class WG_OT_PaletteReplace(Operator):
    bl_idname = "wgame.color_palette_replace"
    bl_label = "选区同色替换"
    bl_description = "读取活动面作为源颜色，将当前选中区域内所有同颜色的面统一替换为当前 Profile 目标颜色"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return (
            getattr(context, "object", None) is not None
            and context.object.type == "MESH"
            and _active_profile_item(context.scene) is not None
        )

    def execute(self, context):
        target = getattr(context, "object", None)
        if target is None or target.type != "MESH":
            self.report({"ERROR"}, "请先选择一个网格对象。")
            return {"CANCELLED"}
        dest_item = _active_profile_item(context.scene)
        if dest_item is None:
            self.report({"ERROR"}, "请先在列表中选择目标角色配色 Profile。")
            return {"CANCELLED"}

        was_edit = target.mode == "EDIT"
        if was_edit:
            bpy.ops.object.mode_set(mode="OBJECT")

        mesh = target.data
        selected_indices = [p.index for p in mesh.polygons if p.select]
        if not selected_indices:
            if was_edit:
                bpy.ops.object.mode_set(mode="EDIT")
            self.report({"ERROR"}, "请先在编辑模式下框选你要替换的区域（至少选择一个面）。")
            return {"CANCELLED"}

        # Determine source category from active face or first selected face
        active_pi = mesh.polygons.active if (0 <= mesh.polygons.active < len(mesh.polygons)) else selected_indices[0]
        src_color, src_profile, src_category, _ = color_profile_from_face(
            context,
            target,
            active_pi,
        )

        slot_attr = mesh.attributes.get("ColorProfileSlot")
        token_attr = mesh.attributes.get("PaletteToken")

        polys_to_replace = []
        for pi in selected_indices:
            matches = False
            if slot_attr is not None and src_category >= 0:
                matches = (slot_attr.data[pi].value == src_category)
            elif token_attr is not None:
                token = token_attr.data[pi].value
                if 0 <= token < LOGICAL_TOKEN_COUNT:
                    matches = ((token // PROFILE_COUNT) == src_color)
            if matches:
                polys_to_replace.append(pi)

        if not polys_to_replace:
            if was_edit:
                bpy.ops.object.mode_set(mode="EDIT")
            self.report({"WARNING"}, f"选区内没有找到与活动面相同颜色 (C{src_category:02d}) 的面。")
            return {"CANCELLED"}

        dest_color = int(dest_item.color_index)
        dest_surface = int(context.scene.wgame_palette_selected_profile)
        dest_category = int(dest_item.category_index)
        dest_item.surface_profile = str(dest_surface)

        painted = _apply_color_profile_to_polygons(
            target,
            polys_to_replace,
            dest_color,
            dest_surface,
            dest_category,
        )

        if was_edit:
            bpy.ops.object.mode_set(mode="EDIT")

        src_str = f"C{src_category:02d}" if src_category >= 0 else color_hex(src_color)
        dest_str = f"C{dest_category:02d}" if dest_category >= 0 else color_hex(dest_color)
        self.report({"INFO"}, f"成功将选区内 {painted} 个面由 {src_str} 统一替换为 {dest_str}！")
        return {"FINISHED"}


class WG_OT_PaletteBrushStroke(Operator):
    bl_idname = "wgame.color_palette_brush_stroke"
    bl_label = "WGame 画笔笔画"
    bl_description = "把一次连续画笔笔画作为独立、可撤销和可重做的网格操作提交"
    bl_options = {"INTERNAL"}

    object_name: StringProperty(options={"HIDDEN", "SKIP_SAVE"})
    polygon_indices: StringProperty(options={"HIDDEN", "SKIP_SAVE"})
    color_index: IntProperty(options={"HIDDEN", "SKIP_SAVE"})
    surface_profile: IntProperty(options={"HIDDEN", "SKIP_SAVE"})
    category_index: IntProperty(default=INVALID_INDEX, options={"HIDDEN", "SKIP_SAVE"})

    def execute(self, _context):
        obj = bpy.data.objects.get(self.object_name)
        if obj is None or obj.type != "MESH":
            self.report({"ERROR"}, "画笔笔画的目标网格已经不存在。")
            return {"CANCELLED"}
        try:
            indices = [
                int(value)
                for value in self.polygon_indices.split(",")
                if value.strip()
            ]
        except ValueError:
            self.report({"ERROR"}, "画笔笔画包含无效的面索引。")
            return {"CANCELLED"}
        if not indices:
            return {"CANCELLED"}
        painted = _apply_color_profile_to_polygons(
            obj,
            indices,
            self.color_index,
            self.surface_profile,
            self.category_index,
        )
        return {"FINISHED"} if painted else {"CANCELLED"}


class WG_OT_PaletteBrush(Operator):
    bl_idname = "wgame.color_palette_brush"
    bl_label = "画笔绘制"
    bl_description = "在 3D 视图中涂抹上色：支持半径涂抹、Shift孤岛填充、Alt吸管、F调大小、1-9选色、X镜像"
    bl_options = {"REGISTER", "BLOCKING"}
    eyedropper_only: BoolProperty(default=False, options={"SKIP_SAVE"})

    def _brush_radius(self, scene):
        return getattr(scene, "wgame_palette_brush_radius", 0.035)

    def _brush_symmetry(self, scene):
        return getattr(scene, "wgame_palette_brush_symmetry_x", False)

    def _brush_cull_backfaces(self, scene):
        return getattr(scene, "wgame_palette_brush_cull_backfaces", True)

    @classmethod
    def poll(cls, context):
        active = getattr(context, "object", None)
        wm_active = getattr(getattr(context, "window_manager", None), "wgame_palette_brush_active", False)
        return (
            not wm_active
            and context.area is not None
            and context.area.type == "VIEW_3D"
            and active is not None
            and active.type == "MESH"
        )

    def _is_mouse_over_ui(self, context, event):
        area = getattr(self, "_area", None) or getattr(context, "area", None)
        if area is None:
            return True
        mx = event.mouse_x
        my = event.mouse_y
        if not (area.x <= mx < area.x + area.width and area.y <= my < area.y + area.height):
            return True
        for r in area.regions:
            if r.type != "WINDOW" and r.width > 1 and r.height > 1:
                if r.x <= mx < r.x + r.width and r.y <= my < r.y + r.height:
                    return True
        return False

    def _set_status(self, context, text=None):
        workspace = getattr(context, "workspace", None)
        if workspace is not None:
            workspace.status_text_set(text)
        area = getattr(context, "area", None)
        if area is not None:
            area.header_text_set(text)

    def _finish(self, context):
        if getattr(self, "_finished", False):
            return
        self._finished = True
        context.window_manager.wgame_palette_brush_active = False
        draw_handler = getattr(self, "_draw_handler", None)
        if draw_handler is not None:
            bpy.types.SpaceView3D.draw_handler_remove(draw_handler, "WINDOW")
            self._draw_handler = None
        self._set_status(context, None)
        try:
            context.window.cursor_modal_restore()
        except Exception:
            pass
        target = bpy.data.objects.get(getattr(self, "_target_name", ""))
        if (
            getattr(self, "_restore_edit_mode", False)
            and target is not None
            and target.type == "MESH"
        ):
            context.view_layer.objects.active = target
            target.select_set(True)
            if target.mode != "EDIT":
                bpy.ops.object.mode_set(mode="EDIT")

    def _draw_highlight(self):
        triangles = getattr(self, "_highlight_triangles", None)
        edges = getattr(self, "_highlight_edges", None)
        circle_lines = getattr(self, "_highlight_circle", None)
        if not triangles and not edges and not circle_lines:
            return

        shader = gpu.shader.from_builtin("UNIFORM_COLOR")
        gpu.state.blend_set("ALPHA")
        gpu.state.depth_test_set("LESS_EQUAL")

        if triangles:
            fill_color = (0.05, 0.85, 1.0, 0.28) if (self._alt_mode or self.eyedropper_only) else (1.0, 0.55, 0.05, 0.25)
            shader.bind()
            shader.uniform_float("color", fill_color)
            batch_for_shader(shader, "TRIS", {"pos": triangles}).draw(shader)

        if edges:
            edge_color = (0.15, 0.95, 1.0, 1.0) if (self._alt_mode or self.eyedropper_only) else (1.0, 0.80, 0.10, 1.0)
            gpu.state.line_width_set(2.0)
            shader.bind()
            shader.uniform_float("color", edge_color)
            batch_for_shader(shader, "LINES", {"pos": edges}).draw(shader)

        if circle_lines:
            circle_color = (0.15, 0.95, 1.0, 0.9) if (self._alt_mode or self.eyedropper_only) else (1.0, 0.70, 0.08, 0.85)
            gpu.state.line_width_set(2.5)
            shader.bind()
            shader.uniform_float("color", circle_color)
            batch_for_shader(shader, "LINES", {"pos": circle_lines}).draw(shader)

        gpu.state.line_width_set(1.0)
        gpu.state.depth_test_set("NONE")
        gpu.state.blend_set("NONE")

    def _update_highlight(self, context, polygon_index, hit_loc=None, hit_norm=None):
        if (
            polygon_index == getattr(self, "_highlighted_polygon", None)
            and hit_loc is None
            and not getattr(self, "_sizing_radius", False)
        ):
            return
        self._highlighted_polygon = polygon_index
        self._highlight_triangles = []
        self._highlight_edges = []
        self._highlight_circle = []

        radius = self._brush_radius(context.scene)
        if hit_loc is not None and hit_norm is not None and radius > 0.002:
            # Build circular ring in 3D oriented with surface normal
            up = Vector((0, 0, 1)) if abs(hit_norm.z) < 0.9 else Vector((1, 0, 0))
            u = hit_norm.cross(up).normalized()
            v = hit_norm.cross(u).normalized()
            segments = 32
            pts = [
                hit_loc + (u * math.cos(i * 2 * math.pi / segments) + v * math.sin(i * 2 * math.pi / segments)) * radius
                for i in range(segments)
            ]
            self._highlight_circle = [
                pt for i in range(segments) for pt in (pts[i], pts[(i + 1) % segments])
            ]

        if polygon_index is not None:
            evaluated = self._target.evaluated_get(context.evaluated_depsgraph_get())
            evaluated_mesh = evaluated.to_mesh()
            try:
                if 0 <= polygon_index < len(evaluated_mesh.polygons):
                    polygon = evaluated_mesh.polygons[polygon_index]
                    vertices = [
                        evaluated.matrix_world @ evaluated_mesh.vertices[vertex_index].co
                        for vertex_index in polygon.vertices
                    ]
                    if len(vertices) >= 3:
                        self._highlight_triangles = [
                            coordinate
                            for index in range(1, len(vertices) - 1)
                            for coordinate in (vertices[0], vertices[index], vertices[index + 1])
                        ]
                        self._highlight_edges = [
                            coordinate
                            for index in range(len(vertices))
                            for coordinate in (vertices[index], vertices[(index + 1) % len(vertices)])
                        ]
            finally:
                evaluated.to_mesh_clear()
        context.area.tag_redraw()

    def _get_hidden_polygons(self):
        if self._target is None or self._target.type != "MESH":
            return set()
        if self._target.mode == "EDIT":
            try:
                bm = bmesh.from_edit_mesh(self._target.data)
                return {f.index for f in bm.faces if f.hide}
            except Exception:
                pass
        if hasattr(self._target.data, "polygons"):
            return {p.index for p in self._target.data.polygons if getattr(p, "hide", False)}
        return set()

    def _build_geometry_cache(self, context):
        target = self._target
        if target is None or target.type != "MESH":
            return
        mw = target.matrix_world
        mw_3x3 = mw.to_3x3()
        mesh = target.data

        if target.mode == "EDIT":
            bm = bmesh.from_edit_mesh(mesh)
            bm.faces.ensure_lookup_table()
            bm.verts.ensure_lookup_table()
            self._poly_verts = [[v.index for v in f.verts] for f in bm.faces]
            self._poly_centers = [
                mw @ f.calc_center_median()
                for f in bm.faces
            ]
            self._poly_normals = [
                (mw_3x3 @ f.normal).normalized()
                for f in bm.faces
            ]
            self._vert_polys = [[] for _ in range(len(bm.verts))]
            for pi, f in enumerate(bm.faces):
                for v in f.verts:
                    self._vert_polys[v.index].append(pi)

            cat_layer = bm.faces.layers.int.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
            token_layer = bm.faces.layers.int.get("PaletteToken")
            if cat_layer is not None:
                self._poly_categories = [int(f[cat_layer]) for f in bm.faces]
            elif token_layer is not None:
                self._poly_categories = [int(f[token_layer] // PROFILE_COUNT) if f[token_layer] >= 0 else -1 for f in bm.faces]
            else:
                self._poly_categories = [-1] * len(bm.faces)

            edge_polys = {}
            for pi, f in enumerate(bm.faces):
                pverts = [v.index for v in f.verts]
                p_len = len(pverts)
                for i in range(p_len):
                    edge = (min(pverts[i], pverts[(i + 1) % p_len]), max(pverts[i], pverts[(i + 1) % p_len]))
                    edge_polys.setdefault(edge, []).append(pi)
            self._poly_neighbors = [[] for _ in range(len(bm.faces))]
            for edge, p_list in edge_polys.items():
                if len(p_list) == 2:
                    self._poly_neighbors[p_list[0]].append(p_list[1])
                    self._poly_neighbors[p_list[1]].append(p_list[0])
                elif len(p_list) > 2:
                    for p1 in p_list:
                        for p2 in p_list:
                            if p1 != p2 and p2 not in self._poly_neighbors[p1]:
                                self._poly_neighbors[p1].append(p2)
        else:
            self._poly_verts = [p.vertices[:] for p in mesh.polygons]
            self._poly_centers = [
                mw @ (sum((mesh.vertices[vi].co for vi in p.vertices), Vector()) / len(p.vertices))
                for p in mesh.polygons
            ]
            self._poly_normals = [
                (mw_3x3 @ p.normal).normalized()
                for p in mesh.polygons
            ]
            self._vert_polys = [[] for _ in range(len(mesh.vertices))]
            for pi, poly in enumerate(mesh.polygons):
                for vi in poly.vertices:
                    self._vert_polys[vi].append(pi)

            slot_attr = mesh.attributes.get(COLOR_PROFILE_SLOT_ATTRIBUTE)
            token_attr = mesh.attributes.get("PaletteToken")
            if slot_attr is not None and len(slot_attr.data) == len(mesh.polygons):
                self._poly_categories = [int(slot_attr.data[i].value) for i in range(len(mesh.polygons))]
            elif token_attr is not None and len(token_attr.data) == len(mesh.polygons):
                self._poly_categories = [int(token_attr.data[i].value // PROFILE_COUNT) for i in range(len(mesh.polygons))]
            else:
                self._poly_categories = [-1] * len(mesh.polygons)

            edge_polys = {}
            for pi, poly in enumerate(mesh.polygons):
                pverts = poly.vertices
                p_len = len(pverts)
                for i in range(p_len):
                    edge = (min(pverts[i], pverts[(i + 1) % p_len]), max(pverts[i], pverts[(i + 1) % p_len]))
                    edge_polys.setdefault(edge, []).append(pi)
            self._poly_neighbors = [[] for _ in range(len(mesh.polygons))]
            for edge, p_list in edge_polys.items():
                if len(p_list) == 2:
                    self._poly_neighbors[p_list[0]].append(p_list[1])
                    self._poly_neighbors[p_list[1]].append(p_list[0])
                elif len(p_list) > 2:
                    for p1 in p_list:
                        for p2 in p_list:
                            if p1 != p2 and p2 not in self._poly_neighbors[p1]:
                                self._poly_neighbors[p1].append(p2)

        self._poly_kd = KDTree(len(self._poly_centers))
        for pi, co in enumerate(self._poly_centers):
            self._poly_kd.insert(co, pi)
        self._poly_kd.balance()
        self._symmetry_cache = {}

    def _face_under_mouse(self, context, event):
        if self._is_mouse_over_ui(context, event):
            return None, None, None
        region = getattr(self, "_window_region", None)
        region_3d = getattr(self, "_region_3d", None)
        if region is None or region_3d is None:
            return None, None, None
        x = event.mouse_x - region.x
        y = event.mouse_y - region.y
        if x < 0 or y < 0 or x >= region.width or y >= region.height:
            return None, None, None
        coordinate = (x, y)
        origin = view3d_utils.region_2d_to_origin_3d(region, region_3d, coordinate)
        direction = view3d_utils.region_2d_to_vector_3d(region, region_3d, coordinate)

        cur_origin = origin
        depsgraph = context.evaluated_depsgraph_get()
        hidden = self._get_hidden_polygons()
        num_faces = len(self._poly_centers) if getattr(self, "_poly_centers", None) else len(self._target.data.polygons)
        for _ in range(6):
            hit, location, normal, polygon_index, hit_object, _matrix = context.scene.ray_cast(
                depsgraph,
                cur_origin,
                direction,
            )
            if not hit or hit_object is None:
                return None, None, None
            original = getattr(hit_object, "original", hit_object)
            if original is not self._target:
                return None, None, None
            polygon_index = int(polygon_index)
            if not 0 <= polygon_index < num_faces:
                return None, None, None
            if polygon_index in hidden:
                cur_origin = location + direction * 0.002
                continue
            return polygon_index, location, normal
        return None, None, None

    def _get_polygons_in_radius(self, context, center_loc, normal):
        if not getattr(self, "_poly_centers", None):
            return [self._highlighted_polygon] if self._highlighted_polygon is not None else []
        radius = self._brush_radius(context.scene)
        if radius <= 0.005:
            return [self._highlighted_polygon] if self._highlighted_polygon is not None else []

        cull_backfaces = self._brush_cull_backfaces(context.scene)
        hidden = self._get_hidden_polygons()

        kd = getattr(self, "_poly_kd", None)
        if kd is not None:
            cands = kd.find_range(center_loc, radius)
            result = []
            for cand_co, pi, dist in cands:
                if pi in hidden:
                    continue
                if cull_backfaces and self._poly_normals[pi].dot(normal) < 0.20:
                    continue
                result.append(pi)
            return result

        rad_sq = radius * radius
        result = []
        for pi, p_center in enumerate(self._poly_centers):
            if pi in hidden:
                continue
            if (p_center - center_loc).length_squared <= rad_sq:
                if cull_backfaces and self._poly_normals[pi].dot(normal) < 0.20:
                    continue
                result.append(pi)
        return result

    def _apply_symmetry_polys(self, context, poly_list):
        if not self._brush_symmetry(context.scene):
            return poly_list
        if not getattr(self, "_poly_centers", None):
            return poly_list

        hidden = self._get_hidden_polygons()
        sym_set = set(poly_list)
        kd = getattr(self, "_poly_kd", None)

        if kd is not None:
            sym_cache = getattr(self, "_symmetry_cache", None)
            if sym_cache is None:
                self._symmetry_cache = sym_cache = {}
            for pi in poly_list:
                if pi in sym_cache:
                    partner = sym_cache[pi]
                    if partner is not None and partner not in hidden:
                        sym_set.add(partner)
                    continue
                c = self._poly_centers[pi]
                target_sym = Vector((-c.x, c.y, c.z))
                target_norm = Vector((-self._poly_normals[pi].x, self._poly_normals[pi].y, self._poly_normals[pi].z))
                cands = kd.find_n(target_sym, 4)
                best_pi = None
                best_dist_sq = 0.035 * 0.035
                for cand_co, cand_pi, cand_dist in cands:
                    if cand_pi != pi:
                        if self._poly_normals[cand_pi].dot(target_norm) > 0.4:
                            d_sq = cand_dist * cand_dist
                            if d_sq < best_dist_sq:
                                best_dist_sq = d_sq
                                best_pi = cand_pi
                sym_cache[pi] = best_pi
                if best_pi is not None:
                    sym_cache[best_pi] = pi
                    if best_pi not in hidden:
                        sym_set.add(best_pi)
            return list(sym_set)

        for pi in poly_list:
            c = self._poly_centers[pi]
            target_sym = Vector((-c.x, c.y, c.z))
            target_norm = Vector((-self._poly_normals[pi].x, self._poly_normals[pi].y, self._poly_normals[pi].z))
            best_pi = None
            best_dist_sq = 0.035 * 0.035
            for other_pi, other_c in enumerate(self._poly_centers):
                if other_pi in hidden or other_pi in sym_set:
                    continue
                d_sq = (other_c - target_sym).length_squared
                if d_sq < best_dist_sq:
                    if self._poly_normals[other_pi].dot(target_norm) > 0.4:
                        best_dist_sq = d_sq
                        best_pi = other_pi
            if best_pi is not None:
                sym_set.add(best_pi)
        return list(sym_set)

    def _get_island_polygons(self, start_polygon, match_category=None):
        hidden = self._get_hidden_polygons()
        if start_polygon is None or start_polygon in hidden:
            return []
        visited = {start_polygon}
        q = [start_polygon]
        poly_neighbors = getattr(self, "_poly_neighbors", None)
        poly_cats = getattr(self, "_poly_categories", None)
        if poly_neighbors:
            while q:
                curr = q.pop()
                for npi in poly_neighbors[curr]:
                    if npi not in visited and npi not in hidden:
                        if match_category is not None and poly_cats is not None and 0 <= npi < len(poly_cats):
                            if poly_cats[npi] != match_category:
                                continue
                        visited.add(npi)
                        q.append(npi)
        else:
            vert_polys = getattr(self, "_vert_polys", [])
            poly_verts = getattr(self, "_poly_verts", [])
            while q:
                curr = q.pop()
                for vi in poly_verts[curr]:
                    for npi in vert_polys[vi]:
                        if npi not in visited and npi not in hidden:
                            if match_category is not None and poly_cats is not None and 0 <= npi < len(poly_cats):
                                if poly_cats[npi] != match_category:
                                    continue
                            visited.add(npi)
                            q.append(npi)
        return list(visited)

    def _paint_at_mouse(self, context, event):
        polygon_index, hit_loc, hit_norm = self._face_under_mouse(context, event)
        self._update_highlight(context, polygon_index, hit_loc, hit_norm)
        if polygon_index is None:
            return

        polys_to_paint = self._get_polygons_in_radius(context, hit_loc, hit_norm)
        polys_to_paint = self._apply_symmetry_polys(context, polys_to_paint)

        painted_any = False
        poly_cats = getattr(self, "_poly_categories", None)
        new_faces = [pi for pi in polys_to_paint if pi not in self._stroke_faces]
        if new_faces:
            new_before = _capture_color_profile_polygons(self._target, new_faces)
            self._stroke_before.update(new_before)
            painted_cnt = _apply_color_profile_to_polygons(
                self._target,
                new_faces,
                self._stroke_color_index,
                self._stroke_surface_profile,
                self._stroke_category_index,
            )
            if painted_cnt:
                self._stroke_faces.update(new_faces)
                self._painted_faces.update(new_faces)
                if poly_cats:
                    for pi in new_faces:
                        if 0 <= pi < len(poly_cats):
                            poly_cats[pi] = self._stroke_category_index
                painted_any = True

        if painted_any:
            context.area.tag_redraw()
            slot_name = f"C{self._stroke_category_index:02d}" if self._stroke_category_index >= 0 else "无分类"
            sym_text = " [X镜像]" if getattr(context.scene, "wgame_palette_brush_symmetry_x", False) else ""
            mode_prefix = " [编辑模式]" if getattr(self._target, "mode", "OBJECT") == "EDIT" else ""
            self._set_status(
                context,
                f"WGame 画笔{mode_prefix}：{slot_name} · 已绘制 {len(self._painted_faces)} 面{sym_text} · Shift+左键:同色泛洪 · Alt:吸色 · F:调半径 · 0-9:选色 · Ctrl+Z:撤销",
            )

    def _commit_stroke(self):
        indices = sorted(self._stroke_faces)
        if not indices:
            self._stroke_before.clear()
            return True
        before_snapshot = dict(self._stroke_before)
        self._stroke_history.append((list(indices), before_snapshot))
        if len(self._stroke_history) > 30:
            self._stroke_history.pop(0)

        # Batch restore with single mesh update before running operator
        _restore_color_profile_polygons(self._target, self._stroke_before)

        result = bpy.ops.wgame.color_palette_brush_stroke(
            "EXEC_DEFAULT",
            object_name=self._target.name,
            polygon_indices=",".join(str(value) for value in indices),
            color_index=self._stroke_color_index,
            surface_profile=self._stroke_surface_profile,
            category_index=self._stroke_category_index,
        )
        committed = "FINISHED" in result
        if committed:
            bpy.ops.ed.undo_push(message="WGame 画笔笔画")
            self._committed_strokes += 1
            color_idx = int(self._stroke_color_index)
            STATE.recent = [color_idx, *[v for v in STATE.recent if v != color_idx]][:64]
            _write_preferences()
        else:
            self.report({"ERROR"}, "当前画笔笔画未能提交到撤销历史。")
        self._stroke_faces.clear()
        self._stroke_before.clear()
        return committed

    def _undo_last_stroke(self, context):
        if not getattr(self, "_stroke_history", None):
            self.report({"INFO"}, "画笔当前没有可撤销的笔画。")
            return False
        indices, before_data = self._stroke_history.pop()
        poly_cats = getattr(self, "_poly_categories", None)
        _restore_color_profile_polygons(self._target, before_data)
        for pi in indices:
            if pi in self._painted_faces:
                self._painted_faces.remove(pi)
            if poly_cats and 0 <= pi < len(poly_cats) and pi in before_data:
                poly_cats[pi] = int(before_data[pi].get("category", -1))
        context.area.tag_redraw()
        self._set_status(context, f"WGame 画笔：已撤销上一笔 · 剩余 {len(self._painted_faces)} 面")
        return True

    def _sample_at_mouse(self, context, event):
        polygon_index, hit_loc, hit_norm = self._face_under_mouse(context, event)
        self._update_highlight(context, polygon_index, hit_loc, hit_norm)
        if polygon_index is None:
            return False
        color_index, surface_profile, category_index, in_profile = color_profile_from_face(
            context,
            self._target,
            polygon_index,
        )
        self._color_index = color_index
        self._surface_profile = surface_profile
        self._category_index = category_index

        # color_profile_from_face already resolves stable category IDs to list positions.
        context.scene.wgame_palette_selected_color = color_index
        context.scene.wgame_palette_selected_profile = str(surface_profile)

        suffix = "" if in_profile else "；该颜色尚未加入当前角色 Profile"
        self.report(
            {"INFO" if in_profile else "WARNING"},
            f"已吸取 {color_hex(color_index)} / 表面材质 {surface_profile}{suffix}。",
        )
        return True

    def invoke(self, context, _event):
        item = _active_profile_item(context.scene)
        if item is None and not self.eyedropper_only:
            self.report({"ERROR"}, "开始画笔绘制前，请先选择一个角色配色 Profile 条目。")
            return {"CANCELLED"}
        if STATE.catalog is None:
            self.report({"ERROR"}, "开始画笔绘制前，请先加载 RGB565 角色画板 v3。")
            return {"CANCELLED"}
        target = getattr(context, "object", None)
        if target is None or target.type != "MESH":
            self.report({"ERROR"}, "请先选择要绘制的网格对象。")
            return {"CANCELLED"}
        window_region = next((region for region in context.area.regions if region.type == "WINDOW"), None)
        region_3d = getattr(context.space_data, "region_3d", None)
        if window_region is None or region_3d is None:
            self.report({"ERROR"}, "当前 3D 视图无法建立画笔拾取区域。")
            return {"CANCELLED"}

        self._target = target
        self._target_name = target.name
        self._window_region = window_region
        self._region_3d = region_3d
        self._area = context.area
        self._restore_edit_mode = target.mode == "EDIT"
        self._last_target_mode = target.mode
        self._cursor_over_ui = False
        self._last_synced_slot = getattr(context.scene, "wgame_character_color_profile_active", 0)
        self._last_synced_color = int(getattr(context.scene, "wgame_palette_selected_color", 0))
        self._last_synced_profile = int(getattr(context.scene, "wgame_palette_selected_profile", 0))
        self._finished = False
        self._painting = False
        self._alt_mode = False
        self._sizing_radius = False
        self._sizing_start_x = 0
        self._sizing_start_r = 0.035
        self._stroke_faces = set()
        self._stroke_before = {}
        self._stroke_history = []
        self._committed_strokes = 0
        self._painted_faces = set()
        self._highlighted_polygon = None
        self._highlight_triangles = []
        self._highlight_edges = []
        self._highlight_circle = []

        self._build_geometry_cache(context)

        if item is not None:
            self._color_index = int(item.color_index)
            self._surface_profile = int(item.surface_profile)
            self._category_index = int(item.category_index)
            context.scene.wgame_palette_selected_profile = str(self._surface_profile)
            context.scene.wgame_palette_selected_color = self._color_index
        else:
            self._color_index = int(context.scene.wgame_palette_selected_color)
            self._surface_profile = int(context.scene.wgame_palette_selected_profile)
            self._category_index = INVALID_INDEX

        context.window_manager.wgame_palette_brush_active = True
        self._draw_handler = bpy.types.SpaceView3D.draw_handler_add(
            self._draw_highlight,
            (),
            "WINDOW",
            "POST_VIEW",
        )
        context.window_manager.modal_handler_add(self)
        context.window.cursor_modal_set("EYEDROPPER" if self.eyedropper_only else "PAINT_BRUSH")
        mode_prefix = " [编辑模式]" if target.mode == "EDIT" else ""
        if self.eyedropper_only:
            self._set_status(context, f"WGame 吸管{mode_prefix}：单击一个已上色的面 · 右键或 Esc 取消")
        else:
            self._set_status(
                context,
                f"WGame 画笔{mode_prefix}：左键涂抹 · Shift+左键:孤岛填充 · Alt:吸色 · F:调半径 · 0-9:切色 · X:镜像 · Ctrl+Z:撤销 · Esc:退出",
            )
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        # Stop check from UI button
        if not getattr(context.window_manager, "wgame_palette_brush_active", True):
            if self._painting:
                self._commit_stroke()
                self._painting = False
            self._finish(context)
            return {"FINISHED"}

        # Dynamically refresh geometry cache if mode or topology changed
        target_mode = getattr(self._target, "mode", "OBJECT")
        if getattr(self, "_last_target_mode", None) != target_mode:
            self._last_target_mode = target_mode
            self._build_geometry_cache(context)
        elif target_mode == "EDIT":
            try:
                bm = bmesh.from_edit_mesh(self._target.data)
                if len(bm.faces) != len(self._poly_centers):
                    self._build_geometry_cache(context)
            except Exception:
                pass

        # Check if mouse is over UI (N-panel, Toolbar, Header, or outside 3D area)
        is_over_ui = self._is_mouse_over_ui(context, event)

        # Dynamic slot and color sync from scene
        curr_active_slot = getattr(context.scene, "wgame_character_color_profile_active", 0)
        curr_color = int(getattr(context.scene, "wgame_palette_selected_color", 0))
        curr_profile = int(getattr(context.scene, "wgame_palette_selected_profile", 0))
        if (
            getattr(self, "_last_synced_slot", None) != curr_active_slot
            or getattr(self, "_last_synced_color", None) != curr_color
            or getattr(self, "_last_synced_profile", None) != curr_profile
        ):
            self._last_synced_slot = curr_active_slot
            self._last_synced_color = curr_color
            self._last_synced_profile = curr_profile
            item = _active_profile_item(context.scene)
            if item is not None:
                self._color_index = int(item.color_index)
                self._surface_profile = int(item.surface_profile)
                self._category_index = int(item.category_index)
            else:
                self._color_index = curr_color
                self._surface_profile = curr_profile
                self._category_index = INVALID_INDEX
            slot_name = f"C{self._category_index:02d}" if self._category_index >= 0 else "无分类"
            sym_text = " [X镜像]" if getattr(context.scene, "wgame_palette_brush_symmetry_x", False) else ""
            mode_prefix = " [编辑模式]" if getattr(self._target, "mode", "OBJECT") == "EDIT" else ""
            self._set_status(
                context,
                f"WGame 画笔{mode_prefix}：已选 {slot_name} · 左键涂抹 · Shift+左键:孤岛填充 · Alt:吸色 · F:调半径 · 0-9:切色 · Esc:退出",
            )

        # Dynamic cursor and 3D highlight management based on UI hover
        if is_over_ui:
            if not getattr(self, "_cursor_over_ui", False):
                self._cursor_over_ui = True
                try:
                    context.window.cursor_modal_restore()
                except Exception:
                    pass
            self._update_highlight(context, None, None, None)
            if self._painting:
                self._painting = False
                self._commit_stroke()
        else:
            if getattr(self, "_cursor_over_ui", False):
                self._cursor_over_ui = False
                cursor_type = "EYEDROPPER" if (self.eyedropper_only or self._alt_mode) else "PAINT_BRUSH"
                try:
                    context.window.cursor_modal_set(cursor_type)
                except Exception:
                    pass

        # Radius sizing mode
        if getattr(self, "_sizing_radius", False):
            if event.type == "MOUSEMOVE":
                delta = (event.mouse_x - self._sizing_start_x) * 0.0006
                context.scene.wgame_palette_brush_radius = max(0.002, min(0.60, self._sizing_start_r + delta))
                polygon_index, hit_loc, hit_norm = self._face_under_mouse(context, event)
                self._update_highlight(context, polygon_index, hit_loc, hit_norm)
                return {"RUNNING_MODAL"}
            if event.type in {"LEFTMOUSE", "RET", "NUMPAD_ENTER", "SPACE"}:
                self._sizing_radius = False
                self._set_status(context, f"WGame 画笔：笔刷半径已设定为 {context.scene.wgame_palette_brush_radius * 100:.1f} cm")
                return {"RUNNING_MODAL"}
            if event.type in {"RIGHTMOUSE", "ESC"}:
                self._sizing_radius = False
                context.scene.wgame_palette_brush_radius = self._sizing_start_r
                self._set_status(context, "WGame 画笔：已取消调整半径")
                return {"RUNNING_MODAL"}
            return {"RUNNING_MODAL"}

        # Right click: if over UI, pass through; if in canvas, exit modal
        if event.type == "RIGHTMOUSE":
            if is_over_ui:
                return {"PASS_THROUGH"}
            if self._painting:
                self._commit_stroke()
                self._painting = False
            self._finish(context)
            if self.eyedropper_only:
                self.report({"INFO"}, "已取消吸管拾取。")
                return {"CANCELLED"}
            self.report({"INFO"}, f"画笔绘制完成：已累计涂抹 {len(self._painted_faces)} 个面。")
            return {"FINISHED"}

        # Keyboards to exit modal
        if event.type in {"ESC", "RET", "NUMPAD_ENTER", "WINDOW_DEACTIVATE"}:
            if self._painting:
                self._commit_stroke()
                self._painting = False
            self._finish(context)
            if self.eyedropper_only:
                self.report({"INFO"}, "已取消吸管拾取。")
                return {"CANCELLED"}
            self.report({"INFO"}, f"画笔绘制完成：已累计涂抹 {len(self._painted_faces)} 个面。")
            return {"FINISHED"}

        # Alt key triggers temporary live eyedropper
        if not is_over_ui:
            if event.alt and not self._alt_mode:
                self._alt_mode = True
                try:
                    context.window.cursor_modal_set("EYEDROPPER")
                except Exception:
                    pass
            elif not event.alt and self._alt_mode and not self.eyedropper_only:
                self._alt_mode = False
                try:
                    context.window.cursor_modal_set("PAINT_BRUSH")
                except Exception:
                    pass

        # F key: interactive brush radius sizing
        if event.type == "F" and event.value == "PRESS":
            self._sizing_radius = True
            self._sizing_start_x = event.mouse_x
            self._sizing_start_r = context.scene.wgame_palette_brush_radius
            self._set_status(context, "WGame 画笔：左右移动鼠标调整笔刷半径 · 左键确定 · 右键取消")
            return {"RUNNING_MODAL"}

        # Bracket keys: step radius
        if event.type in {"BRACKET_LEFT", "LEFT_BRACKET"} and event.value == "PRESS":
            context.scene.wgame_palette_brush_radius = max(0.005, context.scene.wgame_palette_brush_radius * 0.8)
            return {"RUNNING_MODAL"}
        if event.type in {"BRACKET_RIGHT", "RIGHT_BRACKET"} and event.value == "PRESS":
            context.scene.wgame_palette_brush_radius = min(0.60, context.scene.wgame_palette_brush_radius * 1.25)
            return {"RUNNING_MODAL"}

        # X key: toggle X symmetry
        if event.type == "X" and event.value == "PRESS":
            context.scene.wgame_palette_brush_symmetry_x = not context.scene.wgame_palette_brush_symmetry_x
            sym_state = "开启" if context.scene.wgame_palette_brush_symmetry_x else "关闭"
            self.report({"INFO"}, f"X 轴对称绘制已{sym_state}。")
            return {"RUNNING_MODAL"}

        # Number keys 0-9: quick slot switching
        NUM_KEY_MAP = {
            "ONE": 0, "TWO": 1, "THREE": 2, "FOUR": 3, "FIVE": 4,
            "SIX": 5, "SEVEN": 6, "EIGHT": 7, "NINE": 8, "ZERO": 9,
        }
        if event.type in NUM_KEY_MAP and event.value == "PRESS":
            slot_idx = NUM_KEY_MAP[event.type]
            if slot_idx < len(context.scene.wgame_character_color_profile_colors):
                context.scene.wgame_character_color_profile_active = slot_idx
                item = context.scene.wgame_character_color_profile_colors[slot_idx]
                self._color_index = int(item.color_index)
                self._surface_profile = int(context.scene.wgame_palette_selected_profile)
                self._category_index = int(item.category_index)
                self._last_synced_slot = slot_idx
                self._last_synced_color = self._color_index
                self._last_synced_profile = self._surface_profile
                self.report({"INFO"}, f"已切换至 C{self._category_index:02d} {item.label or item.slot_id}。")
                return {"RUNNING_MODAL"}

        # In-modal undo (Ctrl + Z)
        if event.ctrl and event.type == "Z" and event.value == "PRESS":
            self._undo_last_stroke(context)
            return {"RUNNING_MODAL"}

        # Mouse actions
        if event.type == "LEFTMOUSE":
            if is_over_ui:
                return {"PASS_THROUGH"}
            if event.value == "PRESS":
                polygon_index, hit_loc, hit_norm = self._face_under_mouse(context, event)
                if polygon_index is None:
                    return {"PASS_THROUGH"}

                # Live eyedropper
                if self.eyedropper_only or (event.alt and not event.shift):
                    try:
                        sampled = self._sample_at_mouse(context, event)
                    except Exception as error:
                        self.report({"ERROR"}, str(error))
                        return {"RUNNING_MODAL"}
                    if self.eyedropper_only and sampled:
                        self._finish(context)
                        return {"FINISHED"}
                    self._painting = False
                    return {"RUNNING_MODAL"}

                # Shift + Left Click: Flood fill island (same color by default)
                if event.shift or getattr(context.scene, "wgame_palette_brush_fill_island", False):
                    poly_cats = getattr(self, "_poly_categories", None)
                    orig_cat = poly_cats[polygon_index] if (poly_cats and 0 <= polygon_index < len(poly_cats)) else None
                    match_cat = None if event.alt else orig_cat
                    island_faces = self._get_island_polygons(polygon_index, match_category=match_cat)
                    island_faces = self._apply_symmetry_polys(context, island_faces)
                    if not island_faces:
                        return {"RUNNING_MODAL"}

                    # Batch capture original state for in-modal and global undo
                    before_snapshot = _capture_color_profile_polygons(self._target, island_faces)

                    self._stroke_faces.clear()
                    self._stroke_before.clear()
                    self._stroke_color_index = self._color_index
                    self._stroke_surface_profile = self._surface_profile
                    self._stroke_category_index = self._category_index

                    # Directly execute operator for a single undo step with single mesh.update
                    result = bpy.ops.wgame.color_palette_brush_stroke(
                        "EXEC_DEFAULT",
                        object_name=self._target.name,
                        polygon_indices=",".join(str(value) for value in island_faces),
                        color_index=self._stroke_color_index,
                        surface_profile=self._stroke_surface_profile,
                        category_index=self._stroke_category_index,
                    )
                    committed = "FINISHED" in result
                    if committed:
                        bpy.ops.ed.undo_push(message="WGame 孤岛填充")
                        self._stroke_history.append((list(island_faces), before_snapshot))
                        if len(self._stroke_history) > 30:
                            self._stroke_history.pop(0)
                        self._committed_strokes += 1
                        self._painted_faces.update(island_faces)
                        if poly_cats:
                            for pi in island_faces:
                                if 0 <= pi < len(poly_cats):
                                    poly_cats[pi] = self._stroke_category_index
                        color_idx = int(self._stroke_color_index)
                        STATE.recent = [color_idx, *[v for v in STATE.recent if v != color_idx]][:64]
                        _write_preferences()
                    else:
                        self.report({"ERROR"}, "孤岛填充未能提交。")

                    context.area.tag_redraw()
                    desc_str = f"同色连通区域(C{orig_cat:02d})" if (match_cat is not None and orig_cat is not None and orig_cat >= 0) else "网格孤岛"
                    self.report({"INFO"}, f"泛洪填充完成：共给 {len(island_faces)} 个面({desc_str})批量上色。")
                    return {"RUNNING_MODAL"}

                # Normal brush paint press
                self._painting = True
                self._stroke_faces.clear()
                self._stroke_before.clear()
                self._stroke_color_index = self._color_index
                self._stroke_surface_profile = self._surface_profile
                self._stroke_category_index = self._category_index
                self._paint_at_mouse(context, event)
                return {"RUNNING_MODAL"}

            if event.value == "RELEASE" and self._painting:
                self._painting = False
                self._commit_stroke()
                return {"RUNNING_MODAL"}

        if event.type == "MOUSEMOVE":
            if is_over_ui:
                return {"PASS_THROUGH"}
            polygon_index, hit_loc, hit_norm = self._face_under_mouse(context, event)
            self._update_highlight(context, polygon_index, hit_loc, hit_norm)
            if self._painting:
                self._paint_at_mouse(context, event)
                return {"RUNNING_MODAL"}

        return {"PASS_THROUGH"}

    def cancel(self, context):
        self._finish(context)


class WG_OT_PaletteBrushStop(Operator):
    bl_idname = "wgame.color_palette_brush_stop"
    bl_label = "退出画笔绘制"
    bl_description = "退出正在运行的 3D 画笔绘制"

    @classmethod
    def poll(cls, context):
        return getattr(getattr(context, "window_manager", None), "wgame_palette_brush_active", False)

    def execute(self, context):
        context.window_manager.wgame_palette_brush_active = False
        return {"FINISHED"}


class WG_OT_PaletteFavorite(Operator):
    bl_idname = "wgame.color_palette_favorite"
    bl_label = "切换收藏"

    def execute(self, context):
        index = int(context.scene.wgame_palette_selected_color)
        if index in STATE.favorites:
            STATE.favorites.remove(index)
        else:
            STATE.favorites.add(index)
        _write_preferences()
        rebuild_color_items(context)
        return {"FINISHED"}


class WG_OT_PalettePreview(Operator):
    bl_idname = "wgame.color_palette_preview"
    bl_label = "生成预览材质"

    def execute(self, context):
        try:
            material = build_preview_material(context)
            self.report({"INFO"}, f"已指定预览材质 {material.name}。")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_FaceDetailValidate(Operator):
    bl_idname = "wgame.face_detail_validate"
    bl_label = "验证角色 FaceDetail"
    bl_description = "验证 512×512 Direct RGBA，并在贴图旁写入角色 FaceDetail build manifest"

    def execute(self, context):
        try:
            path = Path(bpy.path.abspath(context.scene.wgame_face_detail_texture_path)).resolve()
            if not path.is_file():
                raise FileNotFoundError("请选择角色自己的 FaceDetail RGBA PNG。")
            image = bpy.data.images.load(str(path), check_existing=True)
            if tuple(image.size[:]) != (512, 512) or int(image.channels) != 4:
                raise ValueError("角色 FaceDetail 必须是 512×512 RGBA。")
            digest = "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()
            document = {
                "format": "mx.characterFaceDetailTexture.v1",
                "schemaVersion": "1.0",
                "width": 512,
                "height": 512,
                "encoding": "directColorRgba",
                "output": path.name,
                "textureHash": digest,
            }
            target = path.with_name("build-manifest.json")
            target.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            self.report({"INFO"}, f"角色 FaceDetail 验证通过：{target}")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_PaletteValidate(Operator):
    bl_idname = "wgame.color_palette_validate"
    bl_label = "检查角色画板数据"

    def execute(self, context):
        issues, counts, invalid = validate_meshes(context)
        context.scene.wgame_palette_validation_summary = f"{len(counts)} 个颜色/材质组合 · {invalid} 个无效三角面 · {len(issues)} 个问题"
        if issues:
            self.report({"WARNING"}, issues[0])
        else:
            self.report({"INFO"}, "颜色与 Profile 数据有效。")
        return {"FINISHED"}


class WG_OT_PaletteUsage(Operator):
    bl_idname = "wgame.color_palette_write_usage"
    bl_label = "写入 Usage v2"

    def execute(self, context):
        try:
            target, issues, invalid = write_usage_sidecar(context)
            context.scene.wgame_palette_validation_summary = f"Usage v2 · {invalid} 个无效三角面 · {len(issues)} 个问题"
            self.report({"WARNING"} if invalid else {"INFO"}, f"已写入 {target}")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


class WG_OT_PaletteMigrate(Operator):
    bl_idname = "wgame.color_palette_migrate"
    bl_label = "备份并把所有面重置为 -1"
    bl_options = {"REGISTER"}

    def execute(self, context):
        try:
            backup, report, invalid = migrate_selected_to_global(context)
            self.report({"WARNING"}, f"已重置 {invalid} 个三角面。备份：{backup.name}；报告：{report.name}")
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}


def _find_repo_source_file():
    current_path = Path(__file__).resolve()

    # 1. Read deployment.json if available
    deployment_json = current_path.parent / "deployment.json"
    if deployment_json.exists():
        try:
            with open(deployment_json, "r", encoding="utf-8") as f:
                data = json.load(f)
            repo_root = data.get("repositoryRoot")
            if repo_root:
                candidate = Path(repo_root) / "Tools" / "Blender" / "WGamePaletteTools" / "__init__.py"
                if candidate.exists():
                    return candidate.resolve()
        except Exception:
            pass

    # 2. Check parent directories if running inside repository
    for parent in current_path.parents:
        candidate = parent / "Tools" / "Blender" / "WGamePaletteTools" / "__init__.py"
        if candidate.exists() and candidate.resolve() != current_path:
            return candidate.resolve()
        if (parent / ".git").exists() or (parent / "Assets").exists():
            candidate = parent / "Tools" / "Blender" / "WGamePaletteTools" / "__init__.py"
            if candidate.exists():
                return candidate.resolve()

    # 3. Known default workspace path
    default_candidate = Path(r"E:\Unity\WxFramework\WGameFramework\Tools\Blender\WGamePaletteTools\__init__.py")
    if default_candidate.exists() and default_candidate.resolve() != current_path:
        return default_candidate.resolve()

    return None


class WG_OT_AddonHotReload(Operator):
    bl_idname = "wgame.addon_hot_reload"
    bl_label = "热更画板"
    bl_description = "从仓库同步最新画板源码并即时热重载（无需重启 Blender）"
    bl_options = {"REGISTER"}

    def execute(self, context):
        import importlib
        import sys
        import shutil

        installed_file = Path(__file__).resolve()
        from . import baked_workflow
        if baked_workflow._active_job is not None:
            self.report({'WARNING'}, '请等待当前烘焙或发布任务完成后再热更')
            return {'CANCELLED'}
        repo_file = _find_repo_source_file()
        synced = False

        if repo_file is not None and repo_file.exists() and repo_file != installed_file:
            # Pre-compile check to catch syntax errors before copying
            try:
                with open(repo_file, "r", encoding="utf-8") as f:
                    code_text = f.read()
                compile(code_text, str(repo_file), "exec")
            except Exception as err:
                self.report({"ERROR"}, f"热更中止：仓库源码存在错误: {err}")
                return {"CANCELLED"}

            try:
                for helper_name in ("hair_flow.py", "baked_preview.py", "baked_hair_preview.py", "baked_workflow.py", "baked_task_state.py", "baked_progress.py", "hair_authoring.py"):
                    helper_source = repo_file.parent / helper_name
                    if helper_source.exists():
                        compile(helper_source.read_text(encoding="utf-8"), str(helper_source), "exec")
                        shutil.copy2(helper_source, installed_file.parent / helper_name)
                with open(installed_file, "r", encoding="utf-8") as f:
                    installed_text = f.read()
                if code_text != installed_text:
                    backup_file = installed_file.with_suffix(".py.bak")
                    shutil.copy2(installed_file, backup_file)
                    shutil.copy2(repo_file, installed_file)
                    synced = True

                    dep_json = installed_file.parent / "deployment.json"
                    if dep_json.exists():
                        try:
                            with open(dep_json, "r", encoding="utf-8") as df:
                                d_data = json.load(df)
                            d_data["installedUtc"] = _datetime.datetime.now(_datetime.timezone.utc).isoformat()
                            with open(dep_json, "w", encoding="utf-8") as df:
                                json.dump(d_data, df, indent=2)
                        except Exception:
                            pass
            except Exception as err:
                self.report({"ERROR"}, f"热更同步文件失败: {err}")
                return {"CANCELLED"}

        # Safely defer dynamic reload to main event loop via timer so the operator finishes cleanly first
        def do_reload():
            try:
                mod_name = "WGamePaletteTools"
                mod = sys.modules.get(mod_name)
                if mod is not None:
                    try:
                        mod.unregister()
                    except Exception as e:
                        print("WGamePaletteTools unregister notice:", e)
                    importlib.invalidate_caches()
                    for helper_name in ("WGamePaletteTools.hair_flow", "WGamePaletteTools.baked_preview", "WGamePaletteTools.baked_task_state", "WGamePaletteTools.baked_progress", "WGamePaletteTools.baked_workflow", "WGamePaletteTools.hair_authoring"):
                        if helper_name in sys.modules:
                            importlib.reload(sys.modules[helper_name])
                    with open(installed_file, "r", encoding="utf-8") as f:
                        cur_text = f.read()
                    exec(compile(cur_text, str(installed_file), "exec"), mod.__dict__)
                    mod.register()
                    try:
                        mod.load_catalog(bpy.context, prefer_server=True)
                    except Exception:
                        pass
                    new_ver = ".".join(str(v) for v in mod.bl_info.get("version", (3, 14, 5)))
                    for window in bpy.context.window_manager.windows:
                        for area in window.screen.areas:
                            if area.type == "VIEW_3D":
                                area.tag_redraw()
                    print(f"WGamePaletteTools hot reload completed successfully (v{new_ver})!")
            except Exception as err:
                print("WGamePaletteTools hot reload timer error:", err)
            return None

        bpy.app.timers.register(do_reload, first_interval=0.05)
        new_ver = ".".join(str(v) for v in bl_info.get("version", (3, 14, 5)))
        sync_msg = "已从仓库同步并触发" if synced else "已触发"
        self.report({"INFO"}, f"{sync_msg}画板热更 (v{new_ver})！")
        return {"FINISHED"}


class WG_PT_ColorPalette(Panel):
    bl_idname = "WG_PT_color_palette"
    bl_label = "WGame 角色画板"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "WGame"

    def draw_header(self, context):
        ver_str = ".".join(str(v) for v in bl_info.get("version", (3, 14, 5)))
        self.layout.label(text=f"v{ver_str}")

    def draw(self, context):
        layout = self.layout
        scene = context.scene
        if scene.get('wgame_baked_preview', False):
            layout.label(text='当前为烘焙预览，制作配色请返回原场景', icon='INFO')
            layout.operator('wgame.baked_uv_view', text='返回制作').action='RETURN'
            return

        # 顶部版本与热更操作栏
        ver_str = ".".join(str(v) for v in bl_info.get("version", (3, 14, 5)))
        ver_row = layout.row(align=True)
        ver_row.label(text=f"画板版本 v{ver_str}", icon="PREFERENCES")
        ver_row.operator("wgame.addon_hot_reload", text="热更", icon="FILE_REFRESH")

        # 顶部微型状态与全局库摘要
        header = layout.row(align=True)
        if STATE.catalog is not None:
            catalog_hash = str(STATE.catalog.get("catalogHash", "")).removeprefix("sha256:")
            header.label(text=f"全局色库 {catalog_hash[:8]} ({COLOR_COUNT:,} 色)", icon="CHECKMARK")
        else:
            header.label(text="RGB565 色库不可用", icon="ERROR")
        header.operator("wgame.color_palette_refresh", text="", icon="FILE_REFRESH")

        if STATE.catalog is None:
            layout.label(text="请点击刷新加载画板色库", icon="INFO")
            return

        # 1. 角色配色 Profile
        box_profile = layout.box()
        row_title = box_profile.row(align=True)
        row_title.label(text="1. 角色配色 Profile", icon="COLOR")
        profile_colors = scene.wgame_character_color_profile_colors

        if profile_colors:
            box_profile.template_list(
                "WG_UL_CharacterColorProfileItems",
                "",
                scene,
                "wgame_character_color_profile_colors",
                scene,
                "wgame_character_color_profile_active",
                rows=min(8, max(4, len(profile_colors))),
            )
            active_color = _active_profile_item(scene)
            if active_color is not None:
                prof_name = next(
                    (name for p_idx, name, _ in PROFILE_ITEMS if p_idx == active_color.surface_profile),
                    active_color.surface_profile,
                )
                info_row = box_profile.row(align=True)
                info_row.label(text=f"{active_color.hex} · {prof_name}", icon="LAYER_ACTIVE")
        else:
            box_profile.label(text="尚未配置角色颜色。", icon="INFO")
            box_profile.operator("wgame.character_color_profile_import_used", text="从模型导入已用颜色", icon="IMPORT")

        btn_row = box_profile.row(align=True)
        btn_row.operator("wgame.character_color_profile_read_face", text="读取选中面", icon="EYEDROPPER")
        btn_row.prop(
            scene,
            "wgame_character_color_profile_expanded",
            text="配置配色表",
            icon="TRIA_DOWN" if scene.wgame_character_color_profile_expanded else "TRIA_RIGHT",
            emboss=False,
        )

        if scene.wgame_character_color_profile_expanded:
            configure = box_profile.column(align=True)
            configure.prop(scene, "wgame_character_color_profile_name", text="Profile 名称")
            configure.prop(scene, "wgame_character_color_profile_id", text="ID")
            file_row = configure.row(align=True)
            file_row.prop(scene, "wgame_character_color_profile_path", text="")
            file_row.operator("wgame.character_color_profile_load", text="", icon="IMPORT")
            file_row.operator("wgame.character_color_profile_save", text="", icon="EXPORT")

            configure.separator()
            configure.label(text="添加 / 更新色彩")
            picker = configure.row()
            picker.scale_y = 1.2
            picker.prop(scene, "wgame_palette_color_preview", text="颜色")
            row = configure.row(align=True)
            row.prop(scene, "wgame_palette_hex_input", text="Hex")
            row.operator("wgame.character_color_profile_set_hex", text="应用")
            configure.prop(scene, "wgame_character_color_profile_new_label", text="名称")
            add_row = configure.row(align=True)
            add_row.operator("wgame.character_color_profile_add", text="添加新颜色", icon="ADD")
            add_row.operator("wgame.character_color_profile_import_used", text="导入模型已用", icon="IMPORT")

            active_profile_color = _active_profile_item(scene)
            if active_profile_color is not None:
                configure.separator()
                configure.prop(active_profile_color, "surface_profile", text="表面材质")
                edit_row = configure.row(align=True)
                edit_row.operator("wgame.character_color_profile_update", text="更新当前 Profile", icon="FILE_REFRESH")
                edit_row.operator("wgame.character_color_profile_remove", text="", icon="TRASH")

        # 2. 绘制工具箱
        box_paint = layout.box()
        box_paint.label(text="2. 绘制工具箱", icon="BRUSH_DATA")

        # 核心操作按钮（加高放大、双主按钮布局）
        brush_active = getattr(context.window_manager, "wgame_palette_brush_active", False)
        act_row = box_paint.row(align=True)
        act_row.scale_y = 1.35
        if brush_active:
            act_row.alert = True
            act_row.operator("wgame.color_palette_brush_stop", text="退出画笔绘制 (Esc / 右键)", icon="CANCEL")
            act_row.operator("wgame.color_palette_apply", text="应用到选中面", icon="FACESEL")
        else:
            brush_op = act_row.operator("wgame.color_palette_brush", text="开始画笔绘制", icon="BRUSH_DATA")
            brush_op.eyedropper_only = False
            act_row.operator("wgame.color_palette_apply", text="应用到选中面", icon="FACESEL")

        # 区域选区与同色替换工具排
        sel_row = box_paint.row(align=True)
        sel_row.operator("wgame.color_palette_select_linked", text="选择连通色块", icon="SNAP_VOLUME")
        sel_row.operator("wgame.color_palette_replace", text="选区同色替换", icon="UV_SYNC_SELECT")

        sel_row2 = box_paint.row(align=True)
        sel_filter = sel_row2.operator("wgame.color_palette_select_faces", text="选区仅留该色", icon="SELECT_INTERSECT")
        sel_filter.filter_selection = True

        sel_all = sel_row2.operator("wgame.color_palette_select_faces", text="全选该色", icon="RESTRICT_SELECT_OFF")
        sel_all.filter_selection = False

        # 画笔参数栏
        settings = box_paint.column(align=True)
        r_row = settings.row(align=True)
        r_row.prop(scene, "wgame_palette_brush_radius", text="笔刷半径")
        r_row.label(text="[F]")

        toggles = settings.row(align=True)
        toggles.prop(scene, "wgame_palette_brush_symmetry_x", text="X轴镜像", toggle=True, icon="MOD_MIRROR")
        toggles.prop(scene, "wgame_palette_brush_fill_island", text="孤岛填充", toggle=True, icon="SNAP_VOLUME")
        toggles.prop(scene, "wgame_palette_brush_cull_backfaces", text="背面剔除", toggle=True, icon="NORMALS_FACE")

        quick_tools = box_paint.row(align=True)
        eyedropper = quick_tools.operator("wgame.color_palette_brush", text="吸管吸色 (Alt)", icon="EYEDROPPER")
        eyedropper.eyedropper_only = True
        quick_tools.operator("wgame.color_palette_preview", text="刷新预览材质", icon="MATERIAL")

        # 画笔快捷键指引卡片
        guide_row = box_paint.row(align=True)
        guide_row.prop(
            scene,
            "wgame_palette_brush_shortcuts_expanded",
            text="画笔快捷键指引",
            icon="TRIA_DOWN" if scene.wgame_palette_brush_shortcuts_expanded else "TRIA_RIGHT",
            emboss=False,
        )
        if scene.wgame_palette_brush_shortcuts_expanded:
            g_box = box_paint.box()
            g_box.scale_y = 0.88
            g_box.label(text="• 左键拖动：画笔范围涂抹", icon="DOT")
            g_box.label(text="• Shift + 左键：同色连通泛洪（只填相同色）", icon="DOT")
            g_box.label(text="• Shift+Alt + 左键：整孤岛无视颜色泛洪", icon="DOT")
            g_box.label(text="• Alt + 左键：实时吸管吸取颜色与材质", icon="DOT")
            g_box.label(text="• F 键拖动 / [ ]：平滑调节笔刷半径", icon="DOT")
            g_box.label(text="• 0 ~ 9 键：3D视口内直接切换配色槽", icon="DOT")
            g_box.label(text="• X 键：快速开关 X 轴镜像对称", icon="DOT")
            g_box.label(text="• Ctrl + Z：笔刷内撤销上一笔画", icon="DOT")
            g_box.label(text="• 右键 / Esc：结束绘制并提交", icon="DOT")

        # 3. 表面材质覆盖微调
        surface = layout.box()
        surface.label(text="3. 当前表面材质覆盖", icon="MATERIAL_DATA")
        surface.prop(scene, "wgame_palette_selected_profile", text="")
        profile = STATE.catalog["surfaceProfiles"][int(scene.wgame_palette_selected_profile)]
        detail_mode = DETAIL_MODE_LABELS.get(str(profile.get("detailMode", "clean")), str(profile.get("detailMode", "")))
        surface.label(text=f"Alpha {profile['alphaByte']} · {detail_mode} · 频率 {profile['detailScale']}")

        col = surface.column(align=True)
        col.operator("wgame.character_color_profile_update_surface", text="更新当前 Profile 材质", icon="FILE_REFRESH")
        col.operator("wgame.color_palette_apply_surface_only", text="应用到选中面 (仅材质)", icon="FACESEL")

        # 4. 数据合规检查
        validation = layout.box()
        val_row = validation.row(align=True)
        val_row.operator("wgame.color_palette_validate", text="检查画板数据合规性", icon="CHECKMARK")
        validation.label(text=scene.wgame_palette_validation_summary or "尚未检查。")

        # 5. 高级功能收纳
        face_detail = layout.box()
        face_detail.label(text="角色 FaceDetail · 512 Direct RGBA")
        face_detail.prop(scene, "wgame_face_detail_texture_path", text="贴图")
        face_detail.prop(scene, "wgame_face_detail_intensity", text="强度")
        face_detail.operator("wgame.face_detail_validate", text="验证并写入 Manifest", icon="CHECKMARK")

        usage = layout.box()
        usage.prop(
            scene,
            "wgame_palette_usage_expanded",
            text="导出 / Usage",
            icon="TRIA_DOWN" if scene.wgame_palette_usage_expanded else "TRIA_RIGHT",
            emboss=False,
        )
        if scene.wgame_palette_usage_expanded:
            usage.prop(scene, "wgame_palette_package_root", text="角色包")
            usage.prop(scene, "wgame_palette_exported_model_path", text="FBX")
            usage.operator("wgame.color_palette_write_usage", text="写入 Usage v2", icon="EXPORT")

        technical = layout.box()
        technical.prop(
            scene,
            "wgame_palette_browser_expanded",
            text="技术：RGB565 地址",
            icon="TRIA_DOWN" if scene.wgame_palette_browser_expanded else "TRIA_RIGHT",
            emboss=False,
        )
        if scene.wgame_palette_browser_expanded:
            technical.label(text="底层存储视图，不是角色配色 Profile。")
            technical.prop(scene, "wgame_palette_query", text="搜索")
            technical.prop(scene, "wgame_palette_view", text="视图")
            row = technical.row(align=True)
            row.prop(scene, "wgame_palette_red5", text="R5")
            row.prop(scene, "wgame_palette_green6", text="G6")
            row.prop(scene, "wgame_palette_blue5", text="B5")
            technical.template_list(
                "WG_UL_ColorPaletteItems",
                "",
                context.window_manager,
                "wgame_palette_colors",
                context.window_manager,
                "wgame_palette_color_active",
                rows=6,
            )
            page_count = max(1, math.ceil(len(STATE.filtered_indices) / PAGE_SIZE))
            row = technical.row(align=True)
            previous = row.operator("wgame.color_palette_page", text="", icon="TRIA_LEFT")
            previous.direction = -1
            row.label(text=f"{scene.wgame_palette_page + 1}/{page_count} · {len(STATE.filtered_indices)}")
            next_page = row.operator("wgame.color_palette_page", text="", icon="TRIA_RIGHT")
            next_page.direction = 1

        migration = layout.box()
        migration.prop(
            scene,
            "wgame_palette_migration_expanded",
            text="旧版迁移 / 全量重置",
            icon="TRIA_DOWN" if scene.wgame_palette_migration_expanded else "TRIA_RIGHT",
            emboss=False,
        )
        if scene.wgame_palette_migration_expanded:
            migration.alert = True
            migration.label(text="危险：会先备份，再重置所有面。", icon="ERROR")
            migration.operator("wgame.color_palette_migrate", text="备份并重置所有面")


CLASSES = (
    WG_CharacterColorProfileItem,
    WG_UL_CharacterColorProfileItems,
    WG_ColorPaletteItem,
    WG_UL_ColorPaletteItems,
    WG_OT_ProfileAddColor,
    WG_OT_ProfileImportUsedColors,
    WG_OT_ProfileUpdateColor,
    WG_OT_ProfileUpdateSurface,
    WG_OT_ProfileRemoveColor,
    WG_OT_ProfileSave,
    WG_OT_ProfileLoad,
    WG_OT_ProfileSetHex,
    WG_OT_ProfileReadFace,
    WG_OT_PaletteRefresh,
    WG_OT_PalettePage,
    WG_OT_PaletteApply,
    WG_OT_PaletteApplySurfaceOnly,
    WG_OT_PaletteSelectFaces,
    WG_OT_PaletteSelectLinkedColor,
    WG_OT_PaletteReplace,
    WG_OT_PaletteBrushStroke,
    WG_OT_PaletteBrush,
    WG_OT_PaletteBrushStop,
    WG_OT_PaletteFavorite,
    WG_OT_PalettePreview,
    WG_OT_FaceDetailValidate,
    WG_OT_PaletteValidate,
    WG_OT_PaletteUsage,
    WG_OT_PaletteMigrate,
    WG_OT_AddonHotReload,
    WG_PT_ColorPalette,
)


def register():
    from . import hair_authoring
    hair_authoring.register()
    for cls in CLASSES:
        try:
            bpy.utils.register_class(cls)
        except ValueError:
            pass
    scene = bpy.types.Scene
    scene.wgame_palette_server_url = StringProperty(name="创作服务", default="http://127.0.0.1:4873")
    scene.wgame_palette_query = StringProperty(name="搜索", default="", update=_filter_update)
    scene.wgame_palette_view = EnumProperty(
        name="视图",
        items=(("SLICE", "当前 R5 分片", ""), ("ALL", "技术：全部地址", ""), ("USED", "模型已用", ""), ("RECENT", "最近使用", ""), ("FAVORITES", "收藏", "")),
        default="SLICE",
        update=_filter_update,
    )
    scene.wgame_palette_red5 = IntProperty(name="R5 分片", default=0, min=0, max=31, update=_filter_update)
    scene.wgame_palette_green6 = IntProperty(name="G6", default=-1, min=-1, max=63, update=_filter_update)
    scene.wgame_palette_blue5 = IntProperty(name="B5", default=-1, min=-1, max=31, update=_filter_update)
    scene.wgame_palette_page = IntProperty(default=0, min=0, options={"SKIP_SAVE"})
    scene.wgame_palette_browser_expanded = BoolProperty(name="技术地址浏览器", default=False, options={"SKIP_SAVE"})
    scene.wgame_palette_usage_expanded = BoolProperty(name="导出 / Usage", default=False, options={"SKIP_SAVE"})
    scene.wgame_palette_migration_expanded = BoolProperty(name="旧版迁移", default=False, options={"SKIP_SAVE"})
    scene.wgame_palette_selected_color = IntProperty(name="颜色索引", default=0, min=0, max=COLOR_COUNT - 1)
    scene.wgame_palette_selected_profile = EnumProperty(name="表面材质 Profile", items=PROFILE_ITEMS, default="0")
    scene.wgame_palette_color_preview = FloatVectorProperty(name="RGB 颜色", subtype="COLOR_GAMMA", size=3, min=0.0, max=1.0, default=(0.0, 0.0, 0.0), update=_color_picker_update)
    scene.wgame_palette_hex_input = StringProperty(name="Hex", default="#000000")
    scene.wgame_character_color_profile_colors = CollectionProperty(type=WG_CharacterColorProfileItem)
    scene.wgame_character_color_profile_active = IntProperty(name="当前 Profile 颜色", default=-1, min=-1, update=_profile_active_update)
    scene.wgame_character_color_profile_id = StringProperty(name="Profile ID", default="default")
    scene.wgame_character_color_profile_name = StringProperty(name="Profile 名称", default="角色配色")
    scene.wgame_character_color_profile_path = StringProperty(name="Profile 文件", subtype="FILE_PATH", default="")
    scene.wgame_character_color_profile_new_label = StringProperty(name="新颜色名称", default="", options={"SKIP_SAVE"})
    scene.wgame_character_color_profile_expanded = BoolProperty(name="配置角色 Profile", default=False, options={"SKIP_SAVE"})
    scene.wgame_palette_package_root = StringProperty(name="角色包", subtype="DIR_PATH", default="")
    scene.wgame_palette_exported_model_path = StringProperty(name="导出的 FBX", subtype="FILE_PATH", default="")
    scene.wgame_face_detail_texture_path = StringProperty(name="角色 FaceDetail", subtype="FILE_PATH", default="")
    scene.wgame_face_detail_intensity = FloatProperty(name="FaceDetail 强度", default=1.0, min=0.0, max=1.0)
    scene.wgame_palette_validation_summary = StringProperty(default="", options={"SKIP_SAVE"})

    # Brush enhancements properties
    scene.wgame_palette_brush_radius = FloatProperty(
        name="笔刷半径",
        description="画笔涂抹作用半径（米）",
        default=0.035,
        min=0.001,
        max=1.0,
        subtype="DISTANCE",
    )
    scene.wgame_palette_brush_symmetry_x = BoolProperty(
        name="X 轴镜像",
        description="沿 X 轴对称同步上色",
        default=False,
    )
    scene.wgame_palette_brush_cull_backfaces = BoolProperty(
        name="背面剔除",
        description="防止涂抹到背向视角的背面多边形",
        default=True,
    )
    scene.wgame_palette_brush_fill_island = BoolProperty(
        name="孤岛填充",
        description="开启后左键单击直接填充整个网格孤岛（或快捷键 Shift+左键）",
        default=False,
    )
    scene.wgame_palette_brush_shortcuts_expanded = BoolProperty(
        name="画笔快捷键指南",
        default=False,
        options={"SKIP_SAVE"},
    )

    wm = bpy.types.WindowManager
    wm.wgame_palette_colors = CollectionProperty(type=WG_ColorPaletteItem, options={"SKIP_SAVE"})
    wm.wgame_palette_color_active = IntProperty(default=0, update=_active_item_update, options={"SKIP_SAVE"})
    wm.wgame_palette_brush_active = BoolProperty(default=False, options={"SKIP_SAVE"})
    _read_preferences()
    if not bpy.app.background:
        def delayed_load():
            try:
                load_catalog(bpy.context, prefer_server=True)
            except Exception:
                pass
            return None
        bpy.app.timers.register(delayed_load, first_interval=0.25)


def unregister():
    from . import hair_authoring
    hair_authoring.unregister()
    for name in (
        "wgame_palette_server_url", "wgame_palette_query", "wgame_palette_view",
        "wgame_palette_red5", "wgame_palette_green6", "wgame_palette_blue5",
        "wgame_palette_page", "wgame_palette_browser_expanded", "wgame_palette_usage_expanded", "wgame_palette_migration_expanded",
        "wgame_palette_selected_color", "wgame_palette_selected_profile", "wgame_palette_color_preview", "wgame_palette_hex_input",
        "wgame_character_color_profile_colors", "wgame_character_color_profile_active", "wgame_character_color_profile_id",
        "wgame_character_color_profile_name", "wgame_character_color_profile_path", "wgame_character_color_profile_new_label",
        "wgame_character_color_profile_expanded", "wgame_palette_package_root", "wgame_palette_exported_model_path",
        "wgame_face_detail_texture_path", "wgame_face_detail_intensity",
        "wgame_palette_validation_summary",
        "wgame_palette_brush_radius", "wgame_palette_brush_symmetry_x",
        "wgame_palette_brush_cull_backfaces", "wgame_palette_brush_fill_island",
        "wgame_palette_brush_shortcuts_expanded",
    ):
        if hasattr(bpy.types.Scene, name):
            delattr(bpy.types.Scene, name)
    for name in ("wgame_palette_colors", "wgame_palette_color_active", "wgame_palette_brush_active"):
        if hasattr(bpy.types.WindowManager, name):
            delattr(bpy.types.WindowManager, name)
    for cls in reversed(CLASSES):
        try:
            bpy.utils.unregister_class(cls)
        except RuntimeError:
            pass


if __name__ == "__main__":
    register()
