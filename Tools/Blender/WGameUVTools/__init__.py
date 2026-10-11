# SPDX-License-Identifier: GPL-3.0-or-later
"""Small UV inspection tools; see REFERENCES.md for upstream design references."""

import bpy
from array import array
from .islands import WGAMEUV_OT_island_tools, WGAMEUV_OT_clear_selected
from .face_authoring import WGAMEUV_OT_face_authoring
from .detail_lifecycle import WGAMEUV_OT_detail_lifecycle
from . import detail_binding
from . import detail_images
from .detail_images import WGAMEUV_OT_new_detail_image, WGAMEUV_OT_save_detail_image, WGAMEUV_OT_new_detail_canvas, WGAMEUV_OT_ensure_detail_image, WGAMEUV_OT_save_fixed_detail_image
from .hot_reload import WGAMEUV_OT_hot_reload
from .uv_contract import WGAMEUV_OT_normalize
from . import decals
from .decals import WGAMEUV_OT_decal, WGAMEUV_OT_decal_image
from .paint_workflow import WGAMEUV_OT_enter_paint
from . import canvas_editor
from . import decal_drop
from .decal_drop import WGAMEUV_OT_drop_decal_image, WGAMEUV_FH_decal_source
from .canvas_editor import WGAMEUV_OT_canvas_transform, WGAMEUV_OT_canvas_import, WGAMEUV_OT_canvas_finish, WGAMEUV_OT_canvas_history
from .rounding import WGAMEUV_OT_round_uv, WGAMEUV_OT_round_mesh
from . import feather
from .feather import WGAMEUV_OT_feather_islands, WGAMEUV_OT_select_island, WGAMEUV_OT_feather_selected, WGAMEUV_OT_clear_selection
from bpy.props import EnumProperty, IntProperty, PointerProperty, StringProperty

bl_info = {
    "name": "WGame UV 检查助手",
    "author": "WGame",
    "version": (1, 11, 0),
    "blender": (5, 2, 0),
    "location": "UV Editor / 3D View > Sidebar > WGame UV",
    "description": "一键棋盘格与原生 UV 拉伸显示，保护原材质和色板 UV",
    "category": "UV",
}

TAG = "wgame_uv_checker_v1"
_viewport_states = {}
_image_states = {}


@bpy.app.handlers.persistent
def clear_session_references(_):
    # File loading invalidates RNA pointers; do not dereference old images/spaces.
    _viewport_states.clear()
    _image_states.clear()
    canvas_editor.clear_session()
    decals.clear_session()


def checker_image(settings):
    key = f"image|{settings.pattern}|{settings.cells}"
    for image in bpy.data.images:
        if image.get(TAG) == key:
            return image
    size = settings.cells * 8
    image = bpy.data.images.new("WGame UV 棋盘格", width=size, height=size)
    image[TAG] = key
    image.colorspace_settings.name = 'Non-Color'
    colors = ((0.025, 0.2, 0.65, 1), (0.95, 0.65, 0.08, 1)) if settings.pattern == 'COLOR' else (
        (0.025, 0.025, 0.025, 1), (0.8, 0.8, 0.8, 1))
    rows = [array('f', [v for x in range(size) for v in colors[(x // 8 + parity) % 2]])
            for parity in (0, 1)]
    pixels = array('f')
    for y in range(size):
        pixels.extend(rows[(y // 8) % 2])
    image.pixels.foreach_set(pixels)
    image.update()
    image.pack()
    return image


def preview_image(context, settings):
    image = checker_image(settings)
    for space in uv_spaces(context):
        key = space.as_pointer()
        if key not in _image_states:
            _image_states[key] = (space, space.image)
        space.image = image


def mesh_targets(context):
    objects = context.objects_in_mode if context.mode == 'EDIT_MESH' else context.selected_objects
    return [obj for obj in objects if obj.type == 'MESH']


def checker_modifiers(obj):
    return [mod for mod in obj.modifiers if mod.type == 'NODES'
            and mod.node_group and mod.node_group.get(TAG)]


def resolve_uv(obj, requested=""):
    layer = obj.data.uv_layers.get(requested) if requested else obj.data.uv_layers.active
    if layer is None:
        raise ValueError(f"{obj.name}：没有{'指定的 ' + requested if requested else '活动'} UV")
    if layer.name == "PaletteUV":
        raise ValueError(f"{obj.name}：PaletteUV 是取色通道，请选择你展开的 UV")
    if not obj.is_editable:
        raise ValueError(f"{obj.name}：对象不可编辑")
    return layer.name


def checker_material(uv_name, settings):
    key = f"{uv_name}|{settings.pattern}|{settings.cells}"
    for material in bpy.data.materials:
        if material.get(TAG) == key:
            return material
    material = bpy.data.materials.new("WGame UV 棋盘格 · " + uv_name)
    material[TAG] = key
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    uv = tree.nodes.new('ShaderNodeUVMap')
    uv.uv_map = uv_name
    checker = tree.nodes.new('ShaderNodeTexImage')
    checker.image = checker_image(settings)
    checker.interpolation = 'Closest'
    checker.extension = 'REPEAT'
    emission = tree.nodes.new('ShaderNodeEmission')
    output = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(uv.outputs['UV'], checker.inputs['Vector'])
    tree.links.new(checker.outputs['Color'], emission.inputs['Color'])
    tree.links.new(emission.outputs[0], output.inputs['Surface'])
    return material


def checker_group(material):
    for group in bpy.data.node_groups:
        if group.get(TAG) and group.bl_idname == 'GeometryNodeTree':
            node = group.nodes.get('CheckerMaterial')
            if node and node.inputs['Material'].default_value == material:
                return group
    group = bpy.data.node_groups.new("WGame UV 棋盘格预览", 'GeometryNodeTree')
    group[TAG] = True
    group.interface.new_socket(name='Geometry', in_out='INPUT', socket_type='NodeSocketGeometry')
    group.interface.new_socket(name='Geometry', in_out='OUTPUT', socket_type='NodeSocketGeometry')
    source = group.nodes.new('NodeGroupInput')
    target = group.nodes.new('NodeGroupOutput')
    node = group.nodes.new('GeometryNodeSetMaterial')
    node.name = 'CheckerMaterial'
    node.inputs['Material'].default_value = material
    group.links.new(source.outputs['Geometry'], node.inputs['Geometry'])
    group.links.new(node.outputs['Geometry'], target.inputs['Geometry'])
    return group


def enable_checker(obj, uv_name, settings):
    group = checker_group(checker_material(uv_name, settings))
    modifiers = checker_modifiers(obj)
    modifier = modifiers[0] if modifiers else obj.modifiers.new("WGame UV 棋盘格（预览）", 'NODES')
    modifier.node_group = group
    modifier.show_viewport = True
    modifier.show_in_editmode = True
    modifier.show_render = False
    # This changes evaluated material assignment only, never mesh slots or face indices.
    obj.update_tag()


def disable_checker(objects):
    for obj in objects:
        for modifier in checker_modifiers(obj):
            obj.modifiers.remove(modifier)
    cleanup_unused()


def cleanup_unused():
    for group in list(bpy.data.node_groups):
        if group.get(TAG) and group.users == 0:
            bpy.data.node_groups.remove(group)
    for material in list(bpy.data.materials):
        if material.get(TAG) and material.users == 0:
            bpy.data.materials.remove(material)


def uv_spaces(context):
    if context.area and context.area.type == 'IMAGE_EDITOR' and context.area.ui_type == 'UV':
        return [context.area.spaces.active]
    return [area.spaces.active for area in context.screen.areas
            if area.type == 'IMAGE_EDITOR' and area.ui_type == 'UV'] if context.screen else []


def set_stretch(spaces, mode):
    for space in spaces:
        space.uv_editor.show_stretch = mode != 'OFF'
        if mode != 'OFF':
            space.uv_editor.display_stretch_type = mode
            space.uv_editor.stretch_opacity = 1.0
            space.overlay.show_overlays = True


def preview_shading(context):
    if not context.screen:
        return
    for area in context.screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            key = space.as_pointer()
            if key not in _viewport_states:
                _viewport_states[key] = (space, space.shading.type)
            space.shading.type = 'MATERIAL'


def restore_shading_if_idle():
    if any(checker_modifiers(obj) for obj in bpy.data.objects if obj.type == 'MESH'):
        return
    for space, original in _viewport_states.values():
        try:
            if space.shading.type == 'MATERIAL':
                space.shading.type = original
        except ReferenceError:
            pass
    _viewport_states.clear()
    for space, original in _image_states.values():
        try:
            if space.image and space.image.get(TAG):
                space.image = original
        except ReferenceError:
            pass
    _image_states.clear()


def current_uv_get(self):
    obj = bpy.context.active_object
    layer = obj.data.uv_layers.active if obj and obj.type == 'MESH' else None
    return layer.name if layer else ''


def current_uv_set(self, name):
    obj = bpy.context.active_object
    if not name or not obj or obj.type != 'MESH':
        return
    index = obj.data.uv_layers.find(name)
    if index >= 0:
        obj.data.uv_layers.active_index = index
        sync_checker_uv(bpy.context.scene)


@bpy.app.handlers.persistent
def sync_checker_uv(scene, depsgraph=None):
    if not hasattr(scene, 'wgame_uv_tools'):
        return
    for obj in scene.objects:
        if obj.type != 'MESH':
            continue
        mods = checker_modifiers(obj)
        if not mods:
            continue
        layer = obj.data.uv_layers.active
        if not layer or layer.name == 'PaletteUV':
            disable_checker([obj])
            restore_shading_if_idle()
            continue
        for mod in mods:
            node = mod.node_group.nodes.get('CheckerMaterial')
            material = node.inputs['Material'].default_value if node else None
            source = next((n for n in material.node_tree.nodes if n.type == 'UVMAP'), None) if material else None
            if source and source.uv_map != layer.name:
                enable_checker(obj, layer.name, scene.wgame_uv_tools)
                cleanup_unused()
                break


UI_TABS = [('CREATE', '制作', '选面展开、松弛与打包'), ('CHECK', '检查', '棋盘格、拉伸与重叠'), ('DECAL', '贴花', '素材预览、绘入与保存'), ('CHANNELS', '通道', '正式通道、重做与旧版清理'), ('GEOMETRY','模型','圆整模型真实顶点')]


class WGAMEUV_Settings(bpy.types.PropertyGroup):
    feather_radius: IntProperty(name="羽化半径", default=4, min=1, max=64, description="羽化过渡的像素宽度")
    feather_mode: EnumProperty(name="羽化方向", items=[('CENTER', '居中羽化', '以原边缘为中心内外平滑过渡，最自然柔和'), ('INSIDE', '向内收缩', '向内部渐变，完全不超出原孤岛边界'), ('OUTSIDE', '向外扩张', '保持内部完全不透明，向透明区域外扩渐隐')], default='CENTER')
    feather_falloff: EnumProperty(name="过渡曲线", items=[('SMOOTH', '平滑曲线', 'S 型 Hermite 平滑过渡，柔和自然'), ('LINEAR', '线性过渡', '线性均匀衰减')], default='SMOOTH')
    uv_round_strength: bpy.props.FloatProperty(name="UV 圆整强度",default=1,min=0,max=1)
    mesh_round_strength: bpy.props.FloatProperty(name="模型圆整强度",default=1,min=0,max=1)
    mesh_round_mode: EnumProperty(name="圆整方式",items=[('DISK','圆面（含内部）','平面圆盘，圆整外边界并平滑内部'),('RING','圆环边界','只选一圈边界顶点'),('SPHERE','球面','完整立体球体表面')],default='DISK')
    decal_workflow: EnumProperty(name="编辑方式", items=[('CANVAS','二维画布','直接摆放素材或移动像素'),('PROJECTION','三维投射','限定接收面后投射')],default='CANVAS',update=canvas_editor.workflow_changed)
    canvas_message: StringProperty(options={'SKIP_SAVE'})
    canvas_show_uv: bpy.props.BoolProperty(name="显示 UV 岛",default=True)
    canvas_scale_x: bpy.props.FloatProperty(name="横向拉伸",default=1,min=.01,max=100,update=canvas_editor.field_update)
    canvas_scale_y: bpy.props.FloatProperty(name="纵向拉伸",default=1,min=.01,max=100,update=canvas_editor.field_update)
    canvas_shear: bpy.props.FloatProperty(name="倾斜变形",default=0,min=-5,max=5,update=canvas_editor.field_update)
    canvas_x: bpy.props.FloatProperty(name="位置 X（像素）",update=canvas_editor.field_update)
    canvas_y: bpy.props.FloatProperty(name="位置 Y（像素）",update=canvas_editor.field_update)
    canvas_scale: bpy.props.FloatProperty(name="缩放",default=1,min=.01,max=100,update=canvas_editor.field_update)
    canvas_angle: bpy.props.FloatProperty(name="旋转",subtype='ANGLE',update=canvas_editor.field_update)
    decal_message: StringProperty(options={'SKIP_SAVE'})
    ui_tab_uv: EnumProperty(name="工具页", items=UI_TABS, default='CREATE')
    ui_tab_view: EnumProperty(name="工具页", items=UI_TABS, default='DECAL')
    decal_source: PointerProperty(type=bpy.types.Image)
    decal_target: PointerProperty(type=bpy.types.Image)
    decal_x: bpy.props.FloatProperty(name="水平位置",default=0,update=decals.refresh_preview)
    decal_y: bpy.props.FloatProperty(name="垂直位置",default=0,update=decals.refresh_preview)
    decal_scale: bpy.props.FloatProperty(name="大小",default=1,min=.01,max=20,update=decals.refresh_preview)
    decal_angle: bpy.props.FloatProperty(name="旋转",subtype='ANGLE',default=0,update=decals.refresh_preview)
    decal_opacity: bpy.props.FloatProperty(name="透明度",default=1,min=0,max=1,update=decals.refresh_preview)
    face_unwrap_mode: EnumProperty(name="展开方式", items=[('KEEP', '保持连续', '开放选区连续展开；柱面闭环添加临时纵向切缝，平面孔洞保留，不按角度切碎'), ('SPLIT', '自动切分', '明确允许按角度切成多座岛')], default='KEEP')
    old_detail_uv: StringProperty(name="旧版 UV")
    relax_iterations: IntProperty(name="松弛迭代", default=30, min=1, max=200)
    resolution: IntProperty(name="贴图尺寸", default=1024, min=64, max=16384)
    padding: IntProperty(name="间距（像素）", default=8, min=1, max=64)
    uv_map: StringProperty(name="当前 UV", description="与正在编辑的活动 UV 同步；选择后切换活动通道", get=current_uv_get, set=current_uv_set)
    pattern: EnumProperty(name="颜色", items=[('MONO', "黑白", "黑白棋盘格"), ('COLOR', "蓝黄", "双色棋盘格")])
    cells: IntProperty(name="每边格数", default=32, min=2, max=256)


class WGAMEUV_OT_checker(bpy.types.Operator):
    bl_idname = 'wgame_uv.checker'
    bl_label = "切换棋盘格"
    bl_description = "对编辑中的网格或选中对象显示棋盘格；再次点击恢复原材质"
    bl_options = {'REGISTER', 'UNDO'}
    action: EnumProperty(items=[('TOGGLE', "切换", ""), ('UPDATE', "更新", ""), ('CLEAR', "全部关闭", "")])

    def execute(self, context):
        if self.action == 'CLEAR':
            disable_checker([o for o in bpy.data.objects if o.type == 'MESH' and o.is_editable])
            restore_shading_if_idle()
            return {'FINISHED'}
        objects = mesh_targets(context)
        if not objects:
            self.report({'WARNING'}, "请先选中角色网格")
            return {'CANCELLED'}
        if self.action == 'TOGGLE' and any(checker_modifiers(obj) for obj in objects):
            disable_checker(objects)
            restore_shading_if_idle()
            return {'FINISHED'}
        settings = context.scene.wgame_uv_tools
        try:
            # Validate the entire selection before adding any preview modifiers.
            names = [(obj, resolve_uv(obj)) for obj in objects]
        except ValueError as exc:
            self.report({'ERROR'}, str(exc))
            return {'CANCELLED'}
        for obj, name in names:
            enable_checker(obj, name, settings)
        cleanup_unused()
        preview_shading(context)
        preview_image(context, settings)
        self.report({'INFO'}, "棋盘格已显示；修改格数后点击“更新棋盘格”")
        return {'FINISHED'}


class WGAMEUV_OT_stretch(bpy.types.Operator):
    bl_idname = 'wgame_uv.stretch'
    bl_label = "UV 拉伸显示"
    bl_description = "使用 Blender 原生 UV 热力图；显示在 UV 编辑器内，不改变展开结果"
    bl_options = {'REGISTER', 'UNDO'}
    mode: EnumProperty(items=[('ANGLE', "角度", "检查形状变形"), ('AREA', "面积", "检查相对面积差异"), ('OFF', "关闭", "关闭拉伸显示")])

    def execute(self, context):
        spaces = uv_spaces(context)
        if not spaces:
            self.report({'WARNING'}, "请先切换到 UV Editing 工作区，再点击拉伸显示")
            return {'CANCELLED'}
        if self.mode != 'OFF':
            objects = mesh_targets(context)
            if context.mode != 'EDIT_MESH' or not objects:
                self.report({'WARNING'}, "请进入网格编辑模式，并选中要检查的面")
                return {'CANCELLED'}
            try:
                names = [(obj, resolve_uv(obj)) for obj in objects]
            except ValueError as exc:
                self.report({'ERROR'}, str(exc))
                return {'CANCELLED'}
            for obj, name in names:
                obj.data.uv_layers.active_index = obj.data.uv_layers.find(name)
        set_stretch(spaces, self.mode)
        for area in context.screen.areas:
            area.tag_redraw()
        return {'FINISHED'}


def draw_creation(layout, context, settings):
    box = layout.box()
    box.label(text="选面生成")
    box.prop(settings, 'face_unwrap_mode', expand=True)
    box.operator('wgame_uv.face_authoring', text="从选中面生成 UV 岛", icon='UV').action = 'CREATE'
    box.operator('wgame_uv.face_authoring', text="选中面取消细节 UV").action = 'CLEAR'
    box.label(text="在模型上选面；保留其他区域")
    box = layout.box()
    box.label(text="修整选中岛")
    box.prop(settings, 'uv_round_strength')
    box.operator('wgame_uv.round_uv', text="选中 UV 岛圆形化")
    box.prop(settings, 'relax_iterations')
    box.operator('wgame_uv.island_tools', text="松弛选中岛").action = 'RELAX'
    box.operator('wgame_uv.island_tools', text="分开选中岛（移到右侧）").action = 'SEPARATE'
    box = layout.box()
    box.label(text="布局与打包")
    box.prop(settings, 'resolution', text='打包间距参考尺寸')
    box.prop(settings, 'padding')
    box.operator('wgame_uv.island_tools', text="打包选中岛到 0–1").action = 'PACK'
    box.label(text="会缩放旋转；不避让未选中岛")
    layout.label(text="修整会改变已有贴图的位置", icon='INFO')
    draw_detail_canvas(layout, context, settings)
    layout.operator('wgame_uv.enter_paint',text="完成 UV → 进入细节绘制",icon='TPAINT_HLT')


def draw_checks(layout, context, settings):
    box = layout.box()
    box.label(text="棋盘格")
    box.prop(settings, 'pattern', expand=True)
    box.prop(settings, 'cells')
    on = any(checker_modifiers(o) for o in mesh_targets(context))
    box.operator('wgame_uv.checker', text="关闭棋盘格" if on else "显示棋盘格", icon='TEXTURE').action = 'TOGGLE'
    if on:
        box.operator('wgame_uv.checker', text="更新棋盘格").action = 'UPDATE'
    box.operator('wgame_uv.checker', text="关闭所有棋盘格").action = 'CLEAR'
    box.label(text="导出或贴花前关闭棋盘格")
    box = layout.box()
    box.label(text="拉伸热力图")
    row = box.row(align=True)
    spaces = uv_spaces(context)
    for mode, label in [('ANGLE', "角度"), ('AREA', "面积"), ('OFF', "关闭")]:
        depressed = bool(spaces) and all(
            (not s.uv_editor.show_stretch) if mode == 'OFF' else
            (s.uv_editor.show_stretch and s.uv_editor.display_stretch_type == mode) for s in spaces)
        row.operator('wgame_uv.stretch', text=label, depress=depressed).mode = mode
    box.label(text="编辑模式选面；蓝低、红高")
    box = layout.box()
    box.label(text="重叠定位")
    box.operator('wgame_uv.island_tools', text="检查全部有效岛重叠").action = 'OVERLAP'
    box.label(text="选中相关岛，不修改坐标")


def draw_detail_feedback(layout, context, message):
    import unicodedata
    width = max(16, int(getattr(context.region, 'width', 300) / 8) - 5)
    for paragraph in message.split('；'):
        line = '';used = 0
        for char in paragraph:
            size = 2 if unicodedata.east_asian_width(char) in {'W', 'F'} else 1
            if used + size > width:
                layout.label(text=line);line = '';used = 0
            line += char;used += size
        if line:
            layout.label(text=line)


def draw_detail_canvas(layout, context, settings):
    box = layout.box()
    box.label(text='细节制作画布 · 新建 2048×2048 RGBA')
    box.operator('wgame_uv.ensure_detail_image', text='创建并保存细节图', icon='IMAGE_DATA')
    box.label(text='固定 PNG：FaceDetail/face_detail_rgba.png')
    canvas = settings.decal_target
    if canvas:
        draw_detail_feedback(box, context, f'当前画布：{canvas.name}；{canvas.size[0]}×{canvas.size[1]} RGBA')
        box.label(text='绘制后保存，更新同一固定 PNG', icon='INFO')
        box.operator('wgame_uv.save_fixed_detail_image', text='保存细节图', icon='FILE_TICK')


def draw_channels(layout, context, settings):
    box = layout.box()
    box.label(text="正式通道")
    box.label(text="UV0 → PaletteUV → DetailUV")
    box.operator('wgame_uv.normalize_channels', text="整理为三个正式 UV").action = 'NORMALIZE'
    box.label(text="无冲突时合并，保留有效数据")
    draw_detail_canvas(layout, context, settings)
    box = layout.box()
    box.label(text="重做与替换")
    box.operator('wgame_uv.face_authoring', text="新建空白细节 UV（保留旧版）").action = 'NEW'
    if hasattr(context.scene, 'wgame_face_detail_texture_path'):
        box.prop(context.scene, 'wgame_face_detail_texture_path', text='细节 PNG')
        box.label(text='RGBA PNG · 保留制作源图尺寸')
    else:
        box.label(text='请启用 WGame 色板助手以选择细节 PNG', icon='ERROR')
    box.operator('wgame_uv.detail_lifecycle', text="将当前 UV 设为正式 DetailUV").action = 'PROMOTE'
    box.label(text='同步材质与 UV 图片；保留当前 UV 布局')
    if settings.canvas_message:
        draw_detail_feedback(box, context, settings.canvas_message)
    box = layout.box()
    box.label(text="旧版清理")
    obj = context.active_object
    if obj and obj.type == 'MESH':
        box.prop_search(settings, 'old_detail_uv', obj.data, 'uv_layers')
    box.operator('wgame_uv.detail_lifecycle', text="删除旧版细节 UV").action = 'DELETE'
    box.label(text="删除前检查引用；需确认")


def draw_panel(layout, context):
    row = layout.row(align=True)
    row.label(text="版本 " + ".".join(str(part) for part in bl_info['version']))
    row.operator('wgame_uv.hot_reload', text="热更新", icon='FILE_REFRESH')
    status = context.window_manager.wgame_uv_reload_status
    if status and not status.startswith('热更成功'):
        layout.label(text=status)
    settings = context.scene.wgame_uv_tools
    obj = context.active_object
    if obj and obj.type == 'MESH':
        row = layout.row(align=True)
        row.prop_search(settings, 'uv_map', obj.data, 'uv_layers')
        row.operator('wgame_uv.normalize_channels', text="", icon='UV', emboss=True).action = 'EDIT'
    else:
        layout.label(text="请选中角色网格", icon='INFO')
    field = 'ui_tab_uv' if context.area.type == 'IMAGE_EDITOR' else 'ui_tab_view'
    layout.separator()
    row = layout.row(align=True)
    row.scale_y = 1.25
    row.prop(settings, field, expand=True)
    layout.separator()
    tab = getattr(settings, field)
    if tab == 'CREATE':
        draw_creation(layout, context, settings)
    elif tab == 'CHECK':
        draw_checks(layout, context, settings)
    elif tab == 'DECAL':
        layout.row().prop(settings, 'decal_workflow', expand=True)
        if settings.decal_workflow == 'CANVAS':
            canvas_editor.draw(layout, context)
        else:
            decals.draw(layout, context)
    elif tab == 'GEOMETRY':
        box=layout.box()
        box.label(text="圆整真实模型顶点")
        box.prop(settings,'mesh_round_mode')
        box.prop(settings,'mesh_round_strength')
        box.operator('wgame_uv.round_mesh',text="圆整选中模型顶点")
        box.label(text="圆面：选中整块面，包含内部")
        box.label(text="修改几何，不修改 UV；可撤销")
    else:
        draw_channels(layout, context, settings)


class WGAMEUV_PT_image(bpy.types.Panel):
    bl_label = "UV 与贴图助手"
    bl_space_type = 'IMAGE_EDITOR'
    bl_region_type = 'UI'
    bl_category = 'WGame UV'

    @classmethod
    def poll(cls, context):
        return context.space_data.mode in {'UV', 'PAINT', 'VIEW'}

    def draw(self, context):
        draw_panel(self.layout, context)


class WGAMEUV_PT_view(bpy.types.Panel):
    bl_label = "UV 与贴图助手"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'WGame UV'

    def draw(self, context):
        draw_panel(self.layout, context)


CLASSES = (WGAMEUV_Settings, WGAMEUV_OT_checker, WGAMEUV_OT_stretch, WGAMEUV_OT_island_tools, WGAMEUV_OT_clear_selected, WGAMEUV_OT_face_authoring, WGAMEUV_OT_detail_lifecycle, WGAMEUV_OT_new_detail_image, WGAMEUV_OT_save_detail_image, WGAMEUV_OT_new_detail_canvas, WGAMEUV_OT_ensure_detail_image, WGAMEUV_OT_save_fixed_detail_image, WGAMEUV_OT_hot_reload, WGAMEUV_OT_normalize, WGAMEUV_OT_decal, WGAMEUV_OT_decal_image, WGAMEUV_OT_enter_paint, WGAMEUV_OT_canvas_transform, WGAMEUV_OT_canvas_import, WGAMEUV_OT_canvas_finish, WGAMEUV_OT_canvas_history, WGAMEUV_OT_drop_decal_image, WGAMEUV_FH_decal_source, WGAMEUV_OT_round_uv, WGAMEUV_OT_round_mesh, WGAMEUV_OT_feather_islands, WGAMEUV_OT_select_island, WGAMEUV_OT_feather_selected, WGAMEUV_OT_clear_selection, WGAMEUV_PT_image, WGAMEUV_PT_view)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.wgame_uv_tools = PointerProperty(type=WGAMEUV_Settings)
    bpy.types.WindowManager.wgame_uv_reload_status = StringProperty(options={'SKIP_SAVE'})
    bpy.app.handlers.load_pre.append(clear_session_references)
    bpy.app.handlers.depsgraph_update_post.append(sync_checker_uv)
    bpy.app.handlers.save_pre.append(canvas_editor.before_save)
    canvas_editor.register_keymaps()
    feather.register_overlay()
    detail_binding.register_history()


def unregister():
    detail_binding.unregister_history()
    feather.unregister_overlay()
    canvas_editor.unregister_keymaps()
    if canvas_editor.before_save in bpy.app.handlers.save_pre:
        bpy.app.handlers.save_pre.remove(canvas_editor.before_save)
    canvas_editor.cancel()
    decals.cancel()
    if sync_checker_uv in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.remove(sync_checker_uv)
    disable_checker([o for o in bpy.data.objects if o.type == 'MESH' and o.is_editable])
    restore_shading_if_idle()
    if clear_session_references in bpy.app.handlers.load_pre:
        bpy.app.handlers.load_pre.remove(clear_session_references)
    del bpy.types.Scene.wgame_uv_tools
    del bpy.types.WindowManager.wgame_uv_reload_status
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)

