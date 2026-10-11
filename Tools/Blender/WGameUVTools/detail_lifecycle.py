# SPDX-License-Identifier: GPL-3.0-or-later
"""Explicit promotion and reference-checked removal of detail UV backups."""
import json
import bpy

BACKUPS = 'wgame_detail_uv_backups'
PROTECTED = {'UV0', 'PaletteUV', 'FaceDetailUV'}


def backup_names(mesh):
    return json.loads(mesh.get(BACKUPS, '[]'))


def tree_references(tree, name, seen=None):
    if not tree:
        return False
    seen = seen if seen is not None else set()
    if tree.as_pointer() in seen:
        return False
    seen.add(tree.as_pointer())
    for node in tree.nodes:
        if getattr(node, 'uv_map', None) == name:
            return True
        if any(isinstance(getattr(s, 'default_value', None), str) and s.default_value == name for s in node.inputs):
            return True
        if node.type == 'GROUP' and tree_references(node.node_tree, name, seen):
            return True
    return False


def assert_mesh(context):
    obj = context.active_object
    if not obj or obj.type != 'MESH' or not obj.is_editable or not obj.data.is_editable:
        raise ValueError('请选择可编辑的角色网格')
    if context.mode not in {'OBJECT', 'EDIT_MESH'}:
        raise ValueError('请切到对象或网格编辑模式')
    if context.mode == 'EDIT_MESH' and len(context.objects_in_mode) != 1:
        raise ValueError('请一次只编辑一个网格')
    if obj.data.users > 1:
        raise ValueError('网格数据被多个对象共用，请先将网格设为单用户，避免改动其他对象')
    if any(m.type == 'NODES' and m.node_group and m.node_group.get('wgame_uv_checker_v1') for m in obj.modifiers):
        raise ValueError('请先关闭棋盘格，再替换或删除 UV')
    return obj


def promote(obj):
    mesh = obj.data
    current = mesh.uv_layers.active
    if not current or current.name in PROTECTED:
        raise ValueError('请选择制作好的细节 UV；不能替换 UV0、PaletteUV 或 FaceDetailUV')
    if current.name == 'DetailUV':
        raise ValueError('当前通道已经名为 DetailUV，无需替换')
    materials = {slot.material for slot in obj.material_slots if slot.material}
    details = [m for m in materials if m.use_nodes and m.node_tree.nodes.get('Detail_UV')
               and m.node_tree.nodes['Detail_UV'].type == 'UVMAP']
    if not details:
        raise ValueError('没有找到当前角色的 Detail_UV 材质节点，未更改 UV；本按钮用于 WGame 细节材质')
    old = mesh.uv_layers.get('DetailUV')
    source_name = current.name
    for modifier in obj.modifiers:
        for name in (source_name, 'DetailUV'):
            if tree_references(getattr(modifier, 'node_group', None), name) or any(
                p.type == 'STRING' and getattr(modifier, p.identifier, None) == name
                for p in modifier.bl_rna.properties):
                raise ValueError('修改器引用了待替换 UV，请先确认该引用；未更改数据')
    if mesh.animation_data or obj.animation_data:
        raise ValueError('对象或网格含动画/驱动，请先确认 UV 引用后再替换')
    # Capture node references before Blender can update any names implicitly.
    references = {m: [(n.name, n.uv_map) for n in m.node_tree.nodes if n.type == 'UVMAP']
                  for m in materials if m.use_nodes}
    for m in materials:
        if m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'GROUP' and (tree_references(n.node_tree, source_name) or tree_references(n.node_tree, 'DetailUV')):
                    raise ValueError('材质组内引用了待替换 UV，请先展开该引用；未更改数据')
    old_name = None
    if old:
        old.name = 'DetailUV_Old'
        old_name = old.name
    current.name = 'DetailUV'
    backups = backup_names(mesh)
    if old_name:
        backups.append(old_name)
    mesh[BACKUPS] = json.dumps(backups)
    replacements = {}
    for material, refs in references.items():
        changes = [(name, 'DetailUV' if name == 'Detail_UV' or target == source_name else old_name)
                   for name, target in refs if name == 'Detail_UV' or target == source_name or (old_name and target == 'DetailUV')]
        if not changes:
            continue
        # Copy before editing nodes; other objects keep their original material.
        replacement = material.copy()
        replacements[material] = replacement
        for name, target in changes:
            replacement.node_tree.nodes[name].uv_map = target
    for slot in obj.material_slots:
        if slot.material in replacements:
            slot.material = replacements[slot.material]
    return old_name


def remove_backup(obj, name):
    mesh = obj.data
    tracked = backup_names(mesh)
    if name not in tracked or name in PROTECTED or name == 'DetailUV':
        raise ValueError('只能删除本工具替换时保留的旧版细节 UV')
    layer = mesh.uv_layers.get(name)
    if not layer:
        raise ValueError('该旧版 UV 已不存在')
    if layer.active_render:
        raise ValueError('旧版 UV 仍是默认渲染通道，请先确认并切换渲染通道')
    if mesh.animation_data or obj.animation_data:
        raise ValueError('对象或网格含动画/驱动，需人工确认 UV 引用后再清理')
    for slot in obj.material_slots:
        if slot.material and tree_references(slot.material.node_tree, name):
            raise ValueError('当前材质仍引用该旧版 UV，未删除')
    for modifier in obj.modifiers:
        if tree_references(getattr(modifier, 'node_group', None), name):
            raise ValueError('几何节点仍引用该旧版 UV，未删除')
        for prop in modifier.bl_rna.properties:
            if prop.type == 'STRING' and getattr(modifier, prop.identifier, None) == name:
                raise ValueError('修改器仍引用该旧版 UV，未删除')
    mesh.uv_layers.remove(layer)
    mesh[BACKUPS] = json.dumps([n for n in tracked if n != name])


class WGAMEUV_OT_detail_lifecycle(bpy.types.Operator):
    bl_idname = 'wgame_uv.detail_lifecycle'
    bl_label = '正式细节 UV 管理'
    bl_description = '选择已有细节 PNG，切换正式 UV、绑定材质并同步图片；替换前保留旧 UV，支持撤销'
    bl_options = {'REGISTER', 'UNDO'}
    action: bpy.props.EnumProperty(items=[('PROMOTE', '设为正式 DetailUV', ''), ('DELETE', '删除旧版细节 UV', '')])

    def invoke(self, context, event):
        if self.action == 'DELETE':
            return context.window_manager.invoke_confirm(self, event)
        from . import detail_binding
        try:
            obj = assert_mesh(context)
        except (ValueError, RuntimeError) as exc:
            self.report({'ERROR'}, str(exc))
            return {'CANCELLED'}
        self._confirmed = True
        canvas = context.scene.wgame_uv_tools.decal_target
        memory_canvas = bool(canvas and canvas.get(detail_binding.CANVAS_TAG))
        if detail_binding.replacement_needed(obj) or (not str(getattr(context.scene, 'wgame_face_detail_texture_path', '')).strip() and not memory_canvas):
            return context.window_manager.invoke_props_dialog(self, width=480)
        return self.execute(context)

    def draw(self, context):
        from . import detail_binding
        layout = self.layout
        if hasattr(context.scene, 'wgame_face_detail_texture_path'):
            layout.prop(context.scene, 'wgame_face_detail_texture_path', text='细节 PNG')
        else:
            layout.label(text='请启用 WGame 色板助手以选择细节 PNG', icon='ERROR')
        layout.operator('wgame_uv.ensure_detail_image', text='创建并保存细节图', icon='IMAGE_DATA')
        layout.label(text='一键写入角色固定 PNG；已有固定图复用')
        if context.active_object and detail_binding.replacement_needed(context.active_object):
            layout.label(text='确认后保留旧 DetailUV，并将当前工作 UV 设为正式通道', icon='QUESTION')
            layout.label(text='已有贴图不会自动重投射；取消可继续编辑')

    def execute(self, context):
        if self.action == 'PROMOTE':
            from . import detail_binding
            try:
                obj = assert_mesh(context)
                message = detail_binding.bind(context, obj, allow_replace=getattr(self, '_confirmed', False))
                self.report({'INFO'}, message)
                return {'FINISHED'}
            except Exception as exc:
                message = str(exc)
                context.scene.wgame_uv_tools.canvas_message = message
                self.report({'ERROR'}, message)
                return {'CANCELLED'}
        was_edit = context.mode == 'EDIT_MESH'
        try:
            obj = assert_mesh(context)
            if was_edit:
                bpy.ops.object.mode_set(mode='OBJECT')
            name = context.scene.wgame_uv_tools.old_detail_uv
            remove_backup(obj, name)
            context.scene.wgame_uv_tools.old_detail_uv = ''
            self.report({'INFO'}, f'已删除 {name}，可撤销')
        except (ValueError, RuntimeError) as exc:
            self.report({'ERROR'}, str(exc))
            return {'CANCELLED'}
        finally:
            if was_edit and context.object and context.object.mode == 'OBJECT':
                bpy.ops.object.mode_set(mode='EDIT')
        return {'FINISHED'}
