# SPDX-License-Identifier: GPL-3.0-or-later
"""Explicit PNG binding with staged materials and rollback of the entire UV operation."""
import json
from pathlib import Path
import bpy
import bmesh
import uuid
from .detail_lifecycle import BACKUPS, PROTECTED, backup_names, tree_references

TAG = 'wgame_detail_binding_v1'
CANVAS_TAG = 'wgame_detail_canvas_v1'
REVISION = 'wgame_detail_binding_revision'
_history = globals().get('_history', [])


@bpy.app.handlers.persistent
def sync_history_images(_):
    """Blender Undo restores data, but pinned editor images need explicit UI restoration."""
    for record in reversed(_history):
        obj = bpy.data.objects.get(record['object'])
        if not obj or obj.type != 'MESH':
            continue
        token = obj.data.get(REVISION)
        active = True if token == record['after'] else False if token == record['before'] else None
        if active is None or active == record['active']:
            continue
        for screen in bpy.data.screens:
            for area in screen.areas:
                for space in area.spaces:
                    if space.type != 'IMAGE_EDITOR' or space.as_pointer() not in record['images']:
                        continue
                    before, after = record['images'][space.as_pointer()]
                    name = space.image.name if space.image else None
                    if name in {before, after, None}:
                        space.image = bpy.data.images.get(after if active else before)
        record['active'] = active


@bpy.app.handlers.persistent
def clear_history(_):
    _history.clear()


def register_history():
    for collection, callback in ((bpy.app.handlers.undo_post, sync_history_images),
                                  (bpy.app.handlers.redo_post, sync_history_images),
                                  (bpy.app.handlers.load_pre, clear_history)):
        for previous in list(collection):
            if previous.__module__ == __name__ and previous.__name__ == callback.__name__:
                collection.remove(previous)
        collection.append(callback)


def unregister_history():
    for collection, callback in ((bpy.app.handlers.undo_post, sync_history_images),
                                  (bpy.app.handlers.redo_post, sync_history_images),
                                  (bpy.app.handlers.load_pre, clear_history)):
        if callback in collection:
            collection.remove(callback)


def replacement_needed(obj):
    current = obj.data.uv_layers.active
    return bool(current and current.name != 'DetailUV' and obj.data.uv_layers.get('DetailUV'))


def validate_canvas(image):
    if image.source not in {'GENERATED','FILE'} or image.channels!=4 or image.alpha_mode not in {'STRAIGHT','PREMUL'}:
        raise ValueError('细节源图必须为普通 RGBA（Straight/Premultiplied Alpha）；未更改数据')
    width,height=image.size
    if width<1 or height<1 or len(image.pixels)!=width*height*4:
        raise ValueError('细节源图没有完整、有效的像素数据；未更改数据')


def _image(scene):
    value = str(getattr(scene, 'wgame_face_detail_texture_path', '')).strip()
    if not value:
        canvas = getattr(getattr(scene, 'wgame_uv_tools', None), 'decal_target', None)
        if canvas and canvas.get(CANVAS_TAG):
            validate_canvas(canvas)
            return canvas, False
        raise ValueError('请点“创建并保存细节图”，或选择已有 RGBA PNG；未更改 UV 或材质')
    return load_png(Path(bpy.path.abspath(value)).resolve())


def load_png(path):
    """Reuse loaded pixels, including dirty images; never reload or resize a source."""
    path = Path(path).resolve()
    if not path.is_file() or path.suffix.lower() != '.png':
        raise ValueError('细节 PNG 不存在，请重新选择已有 PNG；未更改 UV 或材质')
    with path.open('rb') as handle:
        header = handle.read(26)
    if header[:8] != b'\x89PNG\r\n\x1a\n' or header[12:16] != b'IHDR' or len(header) < 26:
        raise ValueError('所选文件不是有效 PNG；未更改 UV 或材质')
    if int.from_bytes(header[16:20], 'big') < 1 or int.from_bytes(header[20:24], 'big') < 1 or header[25] != 6:
        raise ValueError('请选择有效尺寸、带 Alpha 通道的 RGBA PNG；未更改 UV 或材质')
    matches = [i for i in bpy.data.images if i.source in {'FILE','GENERATED'} and i.filepath
               and Path(bpy.path.abspath(i.filepath, library=i.library)).resolve() == path]
    if len(matches) > 1:
        raise ValueError('同一路径有多个已加载图像，请先确认使用哪份未保存图像；未更改数据')
    owned = not matches
    image = matches[0] if matches else bpy.data.images.load(str(path), check_existing=False)
    try:
        validate_canvas(image)
        if owned:
            image.colorspace_settings.name = 'sRGB'
        return image, owned
    except Exception:
        if owned:
            bpy.data.images.remove(image)
        raise


def _face_slots(obj, name, allow_blank=False):
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(obj.data)
        layer = bm.loops.layers.uv.get(name)
        faces = ((f.material_index, [tuple(l[layer].uv) for l in f.loops]) for f in bm.faces)
    else:
        layer = obj.data.uv_layers[name]
        faces = ((f.material_index, [tuple(layer.data[n].uv) for n in f.loop_indices]) for f in obj.data.polygons)
    slots = set()
    blank_slots = set()
    for index, coords in faces:
        if all(x == (-1.0, -1.0) for x in coords):
            blank_slots.add(index)
            continue
        if not all(0 <= x <= 1 and 0 <= y <= 1 for x, y in coords):
            raise ValueError('细节 UV 含未完整映射或超出 0–1 的面，请先检查；未更改数据')
        if index >= len(obj.material_slots) or not obj.material_slots[index].material:
            raise ValueError('细节 UV 对应的面缺少材质，请先指定材质；未更改数据')
        slots.add(index)
    if not slots and allow_blank:
        slots = blank_slots
        if any(n>=len(obj.material_slots) or not obj.material_slots[n].material for n in slots):
            raise ValueError('工作 UV 对应的面缺少材质；未更改数据')
    if not slots:
        raise ValueError('当前细节 UV 尚无有效面，请先生成 UV 岛；未更改数据')
    return slots


def _unique_backup(mesh):
    name = 'DetailUV_Old'
    index = 0
    while mesh.uv_layers.get(name):
        index += 1
        name = f'DetailUV_Old.{index:03d}'
    return name


def _detail_textures(material, source):
    return [n for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and
            any(l.from_node.type == 'UVMAP' and l.from_node.uv_map in {source, 'DetailUV'}
                for l in n.inputs['Vector'].links)]


def _configure(material, image, source, old_name, bind, intensity):
    tree = material.node_tree
    textures = _detail_textures(material, source) if bind else []
    if len(textures) > 1:
        raise ValueError('材质含多个细节图像绑定，无法确定目标；未更改数据')
    designated = textures[0].inputs['Vector'].links[0].from_node if textures else tree.nodes.get('Detail_UV')
    for node in tree.nodes:
        if node.type == 'UVMAP':
            if node.uv_map == source or node == designated:
                node.uv_map = 'DetailUV'
            elif old_name and node.uv_map == 'DetailUV':
                node.uv_map = old_name
    if not bind:
        return
    if textures:
        texture = textures[0]
        if designated.name != 'Detail_UV' and tree.nodes.get('Detail_UV'):
            raise ValueError('材质的 Detail_UV 名称冲突，请先确认；未更改数据')
        designated.name = 'Detail_UV'
    else:
        shaders = [n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED']
        if len(shaders) != 1:
            raise ValueError('细节材质需一个 Principled BSDF，复杂材质需人工确认；未更改数据')
        shader = shaders[0]
        uv = tree.nodes.get('Detail_UV')
        if uv and uv.type != 'UVMAP':
            raise ValueError('材质的 Detail_UV 名称被其他节点占用；未更改数据')
        uv = uv or tree.nodes.new('ShaderNodeUVMap')
        uv.name = 'Detail_UV';uv.uv_map = 'DetailUV'
        texture = tree.nodes.new('ShaderNodeTexImage');texture.name = 'Detail_Texture'
        tree.links.new(uv.outputs['UV'], texture.inputs['Vector'])
        mix = tree.nodes.new('ShaderNodeMixRGB');mix.name = 'WGame Detail Overlay'
        mix.blend_type = 'MIX'
        alpha = tree.nodes.new('ShaderNodeMath');alpha.name = 'WGame Detail Intensity'
        alpha.operation = 'MULTIPLY';alpha.inputs[1].default_value = intensity
        base = shader.inputs['Base Color']
        if base.links:
            tree.links.new(base.links[0].from_socket, mix.inputs[1])
        else:
            mix.inputs[1].default_value = base.default_value
        tree.links.new(texture.outputs['Color'], mix.inputs[2])
        tree.links.new(texture.outputs['Alpha'], alpha.inputs[0])
        tree.links.new(alpha.outputs[0], mix.inputs[0])
        tree.links.new(mix.outputs[0], base)
    texture.image = image;texture.extension = 'CLIP';texture.interpolation = 'Linear'
    designated = texture.inputs['Vector'].links[0].from_node
    designated.uv_map = 'DetailUV'
    tree.nodes.active = texture
    material[TAG] = True


def bind(context, obj, allow_replace=False, commit_hook=None, image_override=None, allow_blank=False):
    """No source image reload, file write, UV-coordinate edit, or whole-material rebuild."""
    mesh = obj.data
    current = mesh.uv_layers.active
    if not current or current.name in PROTECTED:
        raise ValueError('请选择制作好的细节 UV；不能替换 UV0、PaletteUV 或 FaceDetailUV')
    source = current.name
    old = mesh.uv_layers.get('DetailUV') if source != 'DetailUV' else None
    if old and not allow_replace:
        raise ValueError('已有正式 DetailUV，请点击按钮并确认“保留旧版并切换”；未更改数据')
    if mesh.animation_data or obj.animation_data:
        raise ValueError('对象或网格含动画/驱动，请先确认 UV 引用；未更改数据')
    for modifier in obj.modifiers:
        for name in {source, 'DetailUV'}:
            if tree_references(getattr(modifier, 'node_group', None), name) or any(
                    p.type == 'STRING' and getattr(modifier, p.identifier, None) == name for p in modifier.bl_rna.properties):
                raise ValueError('修改器引用了待切换 UV，请先确认；未更改数据')
    relevant = _face_slots(obj, source, allow_blank=allow_blank)
    originals = [s.material for s in obj.material_slots]
    for material in {m for m in originals if m}:
        if not material.use_nodes:
            if any(originals[n] == material for n in relevant):
                raise ValueError('细节面的材质未启用节点；未更改数据')
            continue
        if material.node_tree.animation_data:
            raise ValueError('材质节点含动画/驱动，请先确认；未更改数据')
        if any(n.type == 'GROUP' and any(tree_references(n.node_tree, name) for name in {source, 'DetailUV'})
               for n in material.node_tree.nodes):
            raise ValueError('材质组内引用了待切换 UV，请先确认；未更改数据')
    image, owned_image = (image_override, False) if image_override is not None else _image(context.scene)
    validate_canvas(image)
    copies = {}
    settings = context.scene.wgame_uv_tools
    old_setting = settings.old_detail_uv
    old_target = settings.decal_target
    paint = context.scene.tool_settings.image_paint
    old_canvas = paint.canvas
    old_path = getattr(context.scene, 'wgame_face_detail_texture_path', '')
    editors = [(a.spaces.active, a.spaces.active.image) for a in context.screen.areas if a.type == 'IMAGE_EDITOR'] if context.screen else []
    old_name = _unique_backup(mesh) if old else None
    backup_value = mesh.get(BACKUPS)
    previous_revision = mesh.get(REVISION)
    revision = uuid.uuid4().hex
    was_edit = obj.mode == 'EDIT'
    committed = False
    try:
        backups = backup_names(mesh)
        for material in {m for m in originals if m and m.use_nodes}:
            is_detail = any(originals[n] == material for n in relevant)
            affected_ref = source != 'DetailUV' and any(n.type == 'UVMAP' and n.uv_map in {source, 'DetailUV'} for n in material.node_tree.nodes)
            if not is_detail and not affected_ref:
                continue
            textures = _detail_textures(material, source)
            if source == 'DetailUV' and material.get(TAG) and len(textures) == 1 and textures[0].image == image:
                continue  # Repeat clicks synchronize the editor without copying materials again.
            replacement = material.copy();copies[material] = replacement
            _configure(replacement, image, source, old_name, is_detail,
                       float(getattr(context.scene, 'wgame_face_detail_intensity', 1.0)))
        if was_edit:
            bpy.ops.object.mode_set(mode='OBJECT')
        committed = True
        if old:
            mesh.uv_layers['DetailUV'].name = old_name
        # Mode changes invalidate Blender's edit-mesh RNA layer pointers.
        mesh.uv_layers[source].name = 'DetailUV'
        for slot, original in zip(obj.material_slots, originals):
            if original in copies:
                slot.material = copies[original]
        if old_name:
            mesh[BACKUPS] = json.dumps(backups + [old_name])
        settings.old_detail_uv = old_name or old_setting
        settings.decal_target = image
        paint.canvas = image
        if image_override is not None and hasattr(context.scene, 'wgame_face_detail_texture_path'):
            context.scene.wgame_face_detail_texture_path = ''
        for space, _ in editors:
            space.image = image  # Explicit choice also works when the editor image is pinned.
        mesh[REVISION] = revision
        if commit_hook:
            commit_hook()
        if was_edit:
            bpy.ops.object.mode_set(mode='EDIT')
        message = f'已绑定 {image.name}：DetailUV、细节材质和 UV 图片已同步'
        if old_name:
            message += f'；旧版保留为 {old_name}，原贴图未重投射'
        if image.is_dirty:
            message += '；使用内存中未保存的图像，未覆盖 PNG'
        settings.canvas_message = message
        _history.append({'object': obj.name, 'before': previous_revision, 'after': revision, 'active': True,
                         'images': {space.as_pointer(): (previous.name if previous else None, image.name)
                                    for space, previous in editors}})
        del _history[:-64]
        for area in context.screen.areas if context.screen else []:
            area.tag_redraw()
        return message
    except Exception:
        if committed:
            if obj.mode == 'EDIT':
                bpy.ops.object.mode_set(mode='OBJECT')
            if source != 'DetailUV' and mesh.uv_layers.get('DetailUV'):
                mesh.uv_layers['DetailUV'].name = source
            if old_name and mesh.uv_layers.get(old_name):
                mesh.uv_layers[old_name].name = 'DetailUV'
            for slot, original in zip(obj.material_slots, originals):
                slot.material = original
            if backup_value is None:
                if BACKUPS in mesh:
                    del mesh[BACKUPS]
            else:
                mesh[BACKUPS] = backup_value
            settings.old_detail_uv = old_setting;settings.decal_target = old_target;paint.canvas = old_canvas
            if hasattr(context.scene, 'wgame_face_detail_texture_path'):
                context.scene.wgame_face_detail_texture_path = old_path
            if previous_revision is None:
                if REVISION in mesh:
                    del mesh[REVISION]
            else:
                mesh[REVISION] = previous_revision
            for space, previous in editors:
                space.image = previous
            if was_edit:
                bpy.ops.object.mode_set(mode='EDIT')
        for replacement in copies.values():
            bpy.data.materials.remove(replacement)
        if owned_image:
            bpy.data.images.remove(image)
        raise
