"""Scene-owned curated palette. Entries live in the Blend, independent of characters."""
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

import bpy
import bmesh
from bpy.props import CollectionProperty, EnumProperty, FloatVectorProperty, IntProperty, StringProperty

from . import authoring
from .presets import COLORS, LABELS

FORMAT = 'mx.environmentColorProfile.v1'
MAX_COLORS = 256
COLOR_SOURCE = 'materialOrPaletteV3'


def palette():
    return authoring.palette_module()


def active(scene):
    items = scene.wgame_environment_colors
    index = scene.wgame_environment_color_active
    return items[index] if 0 <= index < len(items) else None


def pair(item):
    return int(item.color_index), int(item.surface_profile)


def find_entry(scene, value, category=-1):
    """Resolve an existing face binding, never guess between identical entries."""
    matches = [item for item in scene.wgame_environment_colors if pair(item) == value]
    bound = next((item for item in matches if int(item.category_index) == category), None)
    return bound if bound is not None else matches[0] if len(matches) == 1 else None


def face_references(scene, item, slot, actual):
    if actual != pair(item):
        return False
    if slot >= 0:
        return slot == int(item.category_index)
    # Legacy unbound faces can follow a unique entry, but duplicate values do
    # not establish ownership. Applying a selected entry makes it explicit.
    return find_entry(scene, actual) == item


def sync_selection(scene, _context=None):
    item = active(scene)
    if item is not None:
        scene.wgame_environment_color = tuple(v / 255 for v in palette().rgb8_from_color_index(item.color_index))
        scene.wgame_environment_profile = item.surface_profile


def unique_category(scene):
    used = {int(item.category_index) for item in scene.wgame_environment_colors}
    return next(value for value in range(MAX_COLORS) if value not in used)


def unique_slot(scene, label):
    stem = re.sub(r'[^a-z0-9]+', '_', label.lower()).strip('_') or 'color'
    used = {item.slot_id for item in scene.wgame_environment_colors}
    value = stem
    suffix = 2
    while value in used:
        value = f'{stem}_{suffix:02d}'
        suffix += 1
    return value


def add(scene, color_index, surface_profile, label):
    if len(scene.wgame_environment_colors) >= MAX_COLORS:
        raise ValueError('一个场景配色 Profile 最多支持 256 个条目')
    item = scene.wgame_environment_colors.add()
    item.slot_id = unique_slot(scene, label)
    item.label = label
    item.category_index = unique_category(scene)
    item.color_index = color_index
    item.surface_profile = str(surface_profile)
    rgb = palette().rgb8_from_color_index(color_index)
    item.hex = '#{:02X}{:02X}{:02X}'.format(*rgb)
    item.color_preview = tuple(v / 255 for v in rgb)
    scene['wgame_environment_color_profile_initialized'] = True
    return item


def scene_meshes(scene):
    return [obj for obj in scene.objects if obj.type == 'MESH' and obj.get('wgame_environment_asset')]


def material_profile(material):
    """Resolve the plain material's authored surface, before palette conversion."""
    explicit = material.get('wgame_surface_profile')
    if explicit is not None:
        if not isinstance(explicit, int) or not 0 <= explicit < 16:
            raise ValueError('无效的材质表面类型：' + material.name)
        return explicit
    tree = material.node_tree
    node = next((node for node in tree.nodes if node.type == 'BSDF_PRINCIPLED'), None) if tree else None
    if node is None:
        return 11
    roughness = float(node.inputs['Roughness'].default_value)
    metallic = float(node.inputs['Metallic'].default_value)
    return (13 if roughness < .28 else 12) if metallic >= .5 else (10 if roughness >= .75 else 11)


def face_pair(obj, index, material_cache=None):
    """Sample the current material; shared faces keep their authored palette token."""
    mesh = obj.data
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(mesh)
        bm.faces.ensure_lookup_table()
        face = bm.faces[index]
        material_index = face.material_index
        profile_layer = bm.faces.layers.int.get('SurfaceProfile')
        token_layer = bm.faces.layers.int.get('PaletteToken')
        profile = int(face[profile_layer]) if profile_layer else 11
        token = int(face[token_layer]) if token_layer else -1
    else:
        face = mesh.polygons[index]
        material_index = face.material_index
        profile_attr = mesh.attributes.get('SurfaceProfile')
        token_attr = mesh.attributes.get('PaletteToken')
        profile = int(profile_attr.data[index].value) if profile_attr else 11
        token = int(token_attr.data[index].value) if token_attr else -1
    if material_index >= len(mesh.materials):
        raise ValueError(f'{obj.name} 的面 {index} 没有有效材质')
    material = mesh.materials[material_index]
    if material is None:
        raise ValueError(f'{obj.name} 的面 {index} 没有材质')
    if material.get('wgame_environment_shared'):
        if not 0 <= profile < 16:
            raise ValueError(f'{obj.name} 的面 {index} 表面类型无效')
        if token < 0 or token // 16 >= 65536 or token % 16 != profile:
            raise ValueError(f'{obj.name} 的面 {index} 调色板数据无效')
        return token // 16, profile
    key = material.as_pointer()
    if material_cache is not None and key in material_cache:
        return material_cache[key]
    base = authoring.bake_module().source_base(material)
    if hasattr(base, 'node'):
        raise ValueError(f'{material.name} 的底色是节点图，不能按纯色材质吸色')
    rgb = []
    for value in base[:3]:
        linear = max(0.0, min(1.0, float(value)))
        srgb = 12.92 * linear if linear <= .0031308 else 1.055 * linear ** (1 / 2.4) - .055
        rgb.append(round(255 * srgb))
    color = palette().color_index_from_rgb8(*rgb)
    # Pure materials do not read SurfaceProfile/PaletteData. Those mesh fields
    # may still contain a previous whole-object classification (e.g. metal on
    # concrete). Sampling it would change the finish as soon as the face is painted.
    profile = material_profile(material)
    if material_cache is not None:
        material_cache[key] = (color, profile)
    return color, profile


def used_pairs(scene, material_names=None):
    counts = Counter()
    material_cache = {}
    for obj in scene_meshes(scene):
        if obj.mode == 'EDIT':
            obj.update_from_editmode()
        for face in obj.data.polygons:
            value = face_pair(obj, face.index, material_cache)
            counts[value] += 1
            if material_names is not None:
                material_names[value][obj.data.materials[face.material_index].name] += 1
    return counts


def used_label(color_index, profile, material_name=None):
    module = palette()
    for key, (rgb, preset_profile) in COLORS.items():
        if profile == preset_profile and color_index == module.color_index_from_rgb8(*rgb):
            return LABELS[key]
    if material_name:
        return f'{material_name.removeprefix("Mat_").replace("_", " ")} · {profile:02d}'
    return f'模型已用 {module.color_hex(color_index)} · {profile:02d}'


def add_used(scene, counts=None, names=None):
    if counts is None:
        names = defaultdict(Counter)
        counts = used_pairs(scene, names)
    existing = {pair(item) for item in scene.wgame_environment_colors}
    missing = [value for value, _ in counts.most_common() if value not in existing]
    if len(scene.wgame_environment_colors) + len(missing) > MAX_COLORS:
        raise ValueError('已用颜色超过场景配色 Profile 的 256 条上限')
    for color_index, profile in missing:
        material_name = names[(color_index, profile)].most_common(1)[0][0] if names else None
        add(scene, color_index, profile, used_label(color_index, profile, material_name))
    if missing and scene.wgame_environment_color_active < 0:
        scene.wgame_environment_color_active = 0
    return len(missing)


def prioritize_used(scene, counts):
    selected = active(scene)
    selected_category = int(selected.category_index) if selected else -1
    for target, (value, _count) in enumerate(counts.most_common()):
        entry = find_entry(scene, value)
        source = next((index for index, item in enumerate(scene.wgame_environment_colors)
                       if entry is not None and item.category_index == entry.category_index), -1)
        if source >= 0 and source != target:
            scene.wgame_environment_colors.move(source, target)
    if selected_category >= 0:
        scene.wgame_environment_color_active = next(
            (index for index, item in enumerate(scene.wgame_environment_colors)
             if item.category_index == selected_category), -1)


def seed(scene):
    if not scene.get('wgame_environment_work'):
        return
    if scene.get('wgame_environment_color_profile_source') == COLOR_SOURCE:
        return
    if scene.wgame_environment_colors:
        names = defaultdict(Counter)
        counts = used_pairs(scene, names)
        add_used(scene, counts, names)
        prioritize_used(scene, counts)
        scene['wgame_environment_color_profile_initialized'] = True
        scene['wgame_environment_color_profile_source'] = COLOR_SOURCE
        return
    if scene.get('wgame_environment_color_profile_initialized'):
        scene['wgame_environment_color_profile_source'] = COLOR_SOURCE
        return
    names = defaultdict(Counter)
    counts = used_pairs(scene, names)
    if counts:
        add_used(scene, counts, names)
    else:
        for key, (rgb, profile) in COLORS.items():
            add(scene, palette().color_index_from_rgb8(*rgb), profile, LABELS[key])
        scene.wgame_environment_color_active = 0
    scene['wgame_environment_color_profile_source'] = COLOR_SOURCE


def category_attribute(mesh):
    attr = mesh.attributes.get('ColorProfileSlot')
    if attr is None:
        attr = mesh.attributes.new(name='ColorProfileSlot', type='INT', domain='FACE')
        for value in attr.data:
            value.value = -1
    if attr.domain != 'FACE' or attr.data_type != 'INT':
        raise ValueError('ColorProfileSlot 不是 FACE / INT')
    return attr


def classify_used(scene):
    assigned = 0
    material_cache = {}
    active_obj = bpy.context.view_layer.objects.active
    restore_edit = active_obj is not None and active_obj.mode == 'EDIT'
    if restore_edit:
        bpy.ops.object.mode_set(mode='OBJECT')
    try:
        for obj in scene_meshes(scene):
            slots = obj.data.attributes.get('ColorProfileSlot')
            matching = []
            for face in obj.data.polygons:
                slot = int(slots.data[face.index].value) if slots else -1
                item = find_entry(scene, face_pair(obj, face.index, material_cache), slot)
                matching.append((face.index, int(item.category_index) if item is not None else None))
            if not any(category is not None for _, category in matching):
                continue
            if obj.data.users > 1:
                obj.data = obj.data.copy()
            attr = category_attribute(obj.data)
            for index, category in matching:
                if category is not None and attr.data[index].value != category:
                    attr.data[index].value = category
                    assigned += 1
    finally:
        if restore_edit and active_obj is not None:
            bpy.context.view_layer.objects.active = active_obj
            bpy.ops.object.mode_set(mode='EDIT')
    return assigned


def referenced(scene, item):
    count = 0
    material_cache = {}
    for obj in scene_meshes(scene):
        if obj.mode == 'EDIT':
            bm = bmesh.from_edit_mesh(obj.data)
            bm.faces.ensure_lookup_table()
            layer = bm.faces.layers.int.get('ColorProfileSlot')
            indices = [(face.index, int(face[layer]) if layer else -1) for face in bm.faces]
        else:
            slots = obj.data.attributes.get('ColorProfileSlot')
            indices = [(index, int(slots.data[index].value) if slots else -1)
                       for index in range(len(obj.data.polygons))]
        for index, slot in indices:
            actual = face_pair(obj, index, material_cache)
            if face_references(scene, item, slot, actual):
                count += 1
    return count


def remap(scene, item, color_index, surface_profile):
    from . import palette_brush
    module = palette()
    category = int(item.category_index)
    old_pair = pair(item)
    if old_pair == (color_index, surface_profile):
        return 0
    active_obj = bpy.context.view_layer.objects.active
    restore_edit = active_obj is not None and active_obj.mode == 'EDIT'
    if restore_edit:
        bpy.ops.object.mode_set(mode='OBJECT')
    count = 0
    material_cache = {}
    try:
        for obj in scene_meshes(scene):
            slots = obj.data.attributes.get('ColorProfileSlot')
            faces = []
            for index in range(len(obj.data.polygons)):
                slot = slots.data[index].value if slots else -1
                actual = face_pair(obj, index, material_cache)
                if face_references(scene, item, slot, actual):
                    faces.append(index)
            if not faces:
                continue
            material_slot = palette_brush.brush_material_slot(scene, obj)
            module._apply_color_profile_to_polygons(obj, faces, color_index, surface_profile, category)
            palette_brush.assign_material(obj, faces, material_slot)
            count += len(faces)
    finally:
        if restore_edit and active_obj is not None:
            bpy.context.view_layer.objects.active = active_obj
            bpy.ops.object.mode_set(mode='EDIT')
    item.color_index = color_index
    item.surface_profile = str(surface_profile)
    rgb = module.rgb8_from_color_index(color_index)
    item.hex = '#{:02X}{:02X}{:02X}'.format(*rgb)
    item.color_preview = tuple(v / 255 for v in rgb)
    return count


def profile_path(scene):
    value = scene.wgame_environment_color_profile_path.strip()
    if not value:
        if not bpy.data.filepath:
            raise ValueError('请先保存制作文件，或指定场景 Profile 文件路径')
        value = '//../Shared/environment_color_profile.json'
    return Path(bpy.path.abspath(value)).resolve()


def document(scene):
    return {
        'format': FORMAT,
        'catalogHash': palette().STATE.catalog['catalogHash'],
        'colorSource': COLOR_SOURCE,
        'profileId': scene.wgame_environment_color_profile_id,
        'displayName': scene.wgame_environment_color_profile_name,
        'colors': [
            {'slotId': item.slot_id, 'label': item.label, 'categoryIndex': item.category_index,
             'colorIndex': item.color_index, 'srgbHex': item.hex,
             'surfaceProfile': int(item.surface_profile)}
            for item in scene.wgame_environment_colors],
    }


def load_document(scene, data):
    module = palette()
    if data.get('format') != FORMAT or data.get('catalogHash') != module.STATE.catalog['catalogHash']:
        raise ValueError('场景 Profile 格式或全局色板版本不匹配')
    colors = data.get('colors')
    if not isinstance(colors, list) or not 0 < len(colors) <= MAX_COLORS:
        raise ValueError('场景 Profile 需要 1–256 个颜色条目')
    parsed = []
    slots = set()
    categories = set()
    for row in colors:
        slot = str(row['slotId']).strip()
        category = int(row['categoryIndex'])
        color = int(row['colorIndex'])
        profile = int(row['surfaceProfile'])
        if not slot or slot in slots or category in categories or not 0 <= category < MAX_COLORS:
            raise ValueError('场景 Profile 含重复或非法分类')
        if not 0 <= color < 65536 or not 0 <= profile < 16:
            raise ValueError('场景 Profile 含非法颜色或表面类型')
        if str(row['srgbHex']).upper() != module.color_hex(color).upper():
            raise ValueError('场景 Profile 的颜色索引与 Hex 不一致')
        parsed.append((slot, str(row['label']).strip(), category, color, profile))
        slots.add(slot)
        categories.add(category)
    incoming = {category: (slot, color, profile) for slot, _, category, color, profile in parsed}
    for item in scene.wgame_environment_colors:
        if referenced(scene, item) and incoming.get(int(item.category_index)) != (
                item.slot_id, *pair(item)):
            raise ValueError('Profile 文件与已上色面不一致；请先更新当前条目')
    scene.wgame_environment_colors.clear()
    for slot, label, category, color, profile in parsed:
        item = scene.wgame_environment_colors.add()
        item.slot_id = slot
        item.label = label
        item.category_index = category
        item.color_index = color
        item.surface_profile = str(profile)
        rgb = module.rgb8_from_color_index(color)
        item.hex = module.color_hex(color)
        item.color_preview = tuple(v / 255 for v in rgb)
    scene.wgame_environment_color_profile_id = str(data.get('profileId', 'base'))
    scene.wgame_environment_color_profile_name = str(data.get('displayName', '场景配色'))
    scene.wgame_environment_color_active = 0
    scene['wgame_environment_color_profile_initialized'] = True
    scene['wgame_environment_color_profile_source'] = str(data.get('colorSource', 'legacyPaletteToken'))


class WG_ENV_ColorItem(bpy.types.PropertyGroup):
    slot_id: StringProperty(name='条目 ID')
    label: StringProperty(name='名称')
    category_index: IntProperty(name='分类 ID', default=-1)
    color_index: IntProperty(name='颜色索引', default=0, min=0, max=65535)
    surface_profile: EnumProperty(name='表面类型', items=palette().PROFILE_ITEMS, default='11')
    hex: StringProperty(name='Hex')
    color_preview: FloatVectorProperty(name='颜色', subtype='COLOR_GAMMA', size=3, min=0, max=1)


class WG_ENV_UL_Colors(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_propname, index):
        row = layout.row(align=True)
        swatch = row.row(align=True)
        swatch.enabled = False
        swatch.prop(item, 'color_preview', text='')
        row.label(text=f'C{item.category_index:02d}')
        row.label(text=item.label or f'颜色 {index + 1:02d}')


class WG_ENV_OT_ProfileAdd(bpy.types.Operator):
    bl_idname = 'wgame.environment_profile_add'
    bl_label = '添加新颜色'
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        module = palette()
        rgb = [round(value * 255) for value in scene.wgame_environment_color]
        value = (module.color_index_from_rgb8(*rgb), int(scene.wgame_environment_profile))
        label = scene.wgame_environment_color_new_label.strip() or f'颜色 {len(scene.wgame_environment_colors) + 1:02d}'
        try:
            add(scene, *value, label)
        except ValueError as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}
        scene.wgame_environment_color_active = len(scene.wgame_environment_colors) - 1
        scene.wgame_environment_color_new_label = ''
        return {'FINISHED'}


class WG_ENV_OT_ProfileImport(bpy.types.Operator):
    bl_idname = 'wgame.environment_profile_import_used'
    bl_label = '从模型导入已用颜色'
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        try:
            added = add_used(context.scene)
            classified = classify_used(context.scene)
            self.report({'INFO'}, f'已导入 {added} 个条目，关联 {classified} 个已有面')
            return {'FINISHED'}
        except ValueError as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}


class WG_ENV_OT_ProfileUpdate(bpy.types.Operator):
    bl_idname = 'wgame.environment_profile_update'
    bl_label = '更新当前 Profile'
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        item = active(scene)
        if item is None:
            return {'CANCELLED'}
        module = palette()
        rgb = [round(value * 255) for value in scene.wgame_environment_color]
        value = (module.color_index_from_rgb8(*rgb), int(scene.wgame_environment_profile))
        try:
            count = remap(scene, item, *value)
            self.report({'INFO'}, f'已更新 {item.label}，同步 {count} 个面')
            return {'FINISHED'}
        except ValueError as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}


class WG_ENV_OT_ProfileRemove(bpy.types.Operator):
    bl_idname = 'wgame.environment_profile_remove'
    bl_label = '删除未使用的颜色'
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        item = active(scene)
        if item is None:
            return {'CANCELLED'}
        count = referenced(scene, item)
        if count:
            self.report({'ERROR'}, f'{item.label} 仍被 {count} 个面使用')
            return {'CANCELLED'}
        index = scene.wgame_environment_color_active
        scene.wgame_environment_colors.remove(index)
        scene.wgame_environment_color_active = min(index, len(scene.wgame_environment_colors) - 1)
        return {'FINISHED'}


class WG_ENV_OT_ProfileSave(bpy.types.Operator):
    bl_idname = 'wgame.environment_profile_save'
    bl_label = '保存场景配色 Profile'

    def execute(self, context):
        try:
            target = profile_path(context.scene)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(document(context.scene), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
            context.scene.wgame_environment_color_profile_path = str(target)
            self.report({'INFO'}, f'已保存：{target}')
            return {'FINISHED'}
        except (OSError, ValueError) as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}


class WG_ENV_OT_ProfileLoad(bpy.types.Operator):
    bl_idname = 'wgame.environment_profile_load'
    bl_label = '读取场景配色 Profile'
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        try:
            target = profile_path(context.scene)
            load_document(context.scene, json.loads(target.read_text(encoding='utf-8-sig')))
            context.scene.wgame_environment_color_profile_path = str(target)
            self.report({'INFO'}, f'已读取：{target}')
            return {'FINISHED'}
        except (OSError, ValueError, KeyError, TypeError) as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}


CLASSES = (WG_ENV_ColorItem, WG_ENV_UL_Colors, WG_ENV_OT_ProfileAdd,
           WG_ENV_OT_ProfileImport, WG_ENV_OT_ProfileUpdate, WG_ENV_OT_ProfileRemove,
           WG_ENV_OT_ProfileSave, WG_ENV_OT_ProfileLoad)


def register_properties():
    scene = bpy.types.Scene
    scene.wgame_environment_colors = CollectionProperty(type=WG_ENV_ColorItem)
    scene.wgame_environment_color_active = IntProperty(name='当前配色', default=-1, min=-1, update=sync_selection)
    scene.wgame_environment_color_profile_id = StringProperty(name='Profile ID', default='base')
    scene.wgame_environment_color_profile_name = StringProperty(name='Profile 名称', default='场景配色')
    scene.wgame_environment_color_profile_path = StringProperty(name='Profile 文件', subtype='FILE_PATH', default='')
    scene.wgame_environment_color_new_label = StringProperty(name='新颜色名称', default='', options={'SKIP_SAVE'})
    scene.wgame_environment_color_profile_expanded = bpy.props.BoolProperty(name='配置配色表', default=False, options={'SKIP_SAVE'})


def unregister_properties():
    for name in ('colors', 'color_active', 'color_profile_id', 'color_profile_name',
                 'color_profile_path', 'color_new_label', 'color_profile_expanded'):
        value = 'wgame_environment_' + name
        if hasattr(bpy.types.Scene, value):
            delattr(bpy.types.Scene, value)
