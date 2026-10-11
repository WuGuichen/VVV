"""Scene palette brush using the character brush's 3D picking and cursor behavior."""
import bpy
import bmesh
from bpy_extras import view3d_utils

from . import authoring, shared_detail, color_profile


def palette():
    return authoring.palette_module()


def selected_color(scene):
    item = color_profile.active(scene)
    if item is None:
        raise ValueError('请先在场景配色 Profile 中选择颜色')
    return item.color_index, int(item.surface_profile), item.category_index


def brush_material_slot(scene, obj):
    if obj.data.users > 1:
        editing = obj.mode == 'EDIT'
        if editing:
            bpy.ops.object.mode_set(mode='OBJECT')
        obj.data = obj.data.copy()
        if editing:
            bpy.ops.object.mode_set(mode='EDIT')
    materials = [mat for mat in obj.data.materials if mat and mat.get('wgame_environment_shared')]
    if len(materials) > 1:
        raise ValueError('模型已使用多张共享细节图，请先整理材质')
    if materials:
        return next(index for index, mat in enumerate(obj.data.materials) if mat == materials[0])
    image = scene.wgame_environment_detail_image
    if image is None:
        candidates = [item for item in bpy.data.images if item.get('wgame_environment_shared_detail')]
        if len(candidates) == 1:
            image = candidates[0]
            scene.wgame_environment_detail_image = image
    if image is None or image.channels != 4:
        raise ValueError('请先在「细节图」选择一张共享 RGBA 图')
    material = next((mat for mat in bpy.data.materials
                     if mat.get('wgame_environment_shared') and mat.use_nodes
                     and mat.node_tree.nodes.get('Detail_Texture')
                     and mat.node_tree.nodes['Detail_Texture'].image == image), None)
    if material is None:
        material = shared_detail.make_material(shared_detail.palette_image([obj]), image)
    obj.data.materials.append(material)
    return len(obj.data.materials) - 1


def assign_material(obj, faces, slot):
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        for index in faces:
            bm.faces[index].material_index = slot
        bmesh.update_edit_mesh(obj.data)
    else:
        for index in faces:
            obj.data.polygons[index].material_index = slot
        obj.data.update()


def capture(obj, faces):
    state = palette()._capture_color_profile_polygons(obj, faces)
    had_category = obj.data.attributes.get('ColorProfileSlot') is not None
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        for index in state:
            state[index]['material_index'] = bm.faces[index].material_index
            state[index]['had_category_layer'] = had_category
    else:
        for index in state:
            state[index]['material_index'] = obj.data.polygons[index].material_index
            state[index]['had_category_layer'] = had_category
    return state


def restore(obj, before):
    module = palette()
    mesh = obj.data
    had_category = any(value['had_category_layer'] for value in before.values())
    module._restore_color_profile_polygons(obj, before)
    if not had_category:
        if obj.mode == 'EDIT':
            bm = bmesh.from_edit_mesh(mesh)
            layer = bm.faces.layers.int.get(module.COLOR_PROFILE_SLOT_ATTRIBUTE)
            if layer is not None:
                bm.faces.layers.int.remove(layer)
                bmesh.update_edit_mesh(mesh)
        else:
            layer = mesh.attributes.get(module.COLOR_PROFILE_SLOT_ATTRIBUTE)
            if layer is not None:
                mesh.attributes.remove(layer)
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(mesh)
        bm.faces.ensure_lookup_table()
        for index, state in before.items():
            bm.faces[index].material_index = state['material_index']
        bmesh.update_edit_mesh(mesh)
    else:
        for index, state in before.items():
            mesh.polygons[index].material_index = state['material_index']
        mesh.update()
    invalid = any(color < 0 for color in face_colors(obj))
    obj['paletteRepaintRequired'] = invalid
    mesh['paletteRepaintRequired'] = invalid


def face_colors(obj):
    count = len(obj.data.polygons)
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        layer = bm.faces.layers.int.get('PaletteToken')
        return [int(face[layer]) // 16 if layer and int(face[layer]) >= 0 else -1 for face in bm.faces]
    attr = obj.data.attributes.get('PaletteToken')
    return [int(attr.data[i].value) // 16 for i in range(count)] if attr else [-1] * count


def face_groups(obj):
    material_cache = {}
    groups = []
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        layer = bm.faces.layers.int.get('ColorProfileSlot')
        faces = [(face.index, face.material_index, int(face[layer]) if layer else -1) for face in bm.faces]
    else:
        layer = obj.data.attributes.get('ColorProfileSlot')
        faces = [(face.index, face.material_index, int(layer.data[face.index].value) if layer else -1)
                 for face in obj.data.polygons]
    for index, material_index, slot in faces:
        material = obj.data.materials[material_index]
        if material and material.get('wgame_environment_shared') and slot >= 0:
            groups.append(('slot', slot))
        else:
            groups.append(('pair', *color_profile.face_pair(obj, index, material_cache)))
    return groups


def face_color(obj, index):
    color, profile = color_profile.face_pair(obj, index)
    if obj.mode == 'EDIT':
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        slot = bm.faces.layers.int.get('ColorProfileSlot')
        category = int(bm.faces[index][slot]) if slot else -1
    else:
        slot = obj.data.attributes.get('ColorProfileSlot')
        category = int(slot.data[index].value) if slot else -1
    return color, profile, palette().rgb8_from_color_index(color), category


def sample_hit(scene, depsgraph, origin, direction):
    hit, _location, _normal, index, evaluated, _matrix = scene.ray_cast(depsgraph, origin, direction)
    if not hit or evaluated is None:
        raise ValueError('吸管没有点到模型表面')
    obj = getattr(evaluated, 'original', None) or evaluated
    if obj.type != 'MESH' or not obj.get('wgame_environment_asset'):
        raise ValueError('请点场景模型的表面')
    if index < 0 or index >= len(obj.data.polygons):
        raise ValueError('该表面无法对应到源模型的面；请检查修改器')
    return obj, index


def select_from_face(scene, obj, index):
    color_profile.seed(scene)
    color, profile, rgb, category = face_color(obj, index)
    entry = color_profile.find_entry(scene, (color, profile), category)
    found = next((i for i, item in enumerate(scene.wgame_environment_colors)
                  if entry is not None and item.category_index == entry.category_index), -1)
    scene.wgame_environment_color_active = found
    if found < 0:
        scene.wgame_environment_color = tuple(value / 255 for value in rgb)
        scene.wgame_environment_profile = str(profile)
    else:
        # Setting an already-active index does not run Blender's update callback.
        color_profile.sync_selection(scene)
    return found


class WG_ENV_OT_BrushStroke(bpy.types.Operator):
    bl_idname = 'wgame.environment_brush_stroke'
    bl_label = '场景画笔笔画'
    bl_options = {'REGISTER', 'UNDO', 'INTERNAL'}
    object_name: bpy.props.StringProperty(options={'HIDDEN', 'SKIP_SAVE'})
    indices: bpy.props.StringProperty(options={'HIDDEN', 'SKIP_SAVE'})
    color_index: bpy.props.IntProperty(options={'HIDDEN', 'SKIP_SAVE'})
    surface_profile: bpy.props.IntProperty(options={'HIDDEN', 'SKIP_SAVE'})
    category_index: bpy.props.IntProperty(default=-1, options={'HIDDEN', 'SKIP_SAVE'})

    def execute(self, _context):
        obj = bpy.data.objects.get(self.object_name)
        if obj is None or obj.type != 'MESH' or not obj.get('wgame_environment_asset'):
            self.report({'ERROR'}, '场景画笔的目标模型已不存在')
            return {'CANCELLED'}
        try:
            faces = [int(value) for value in self.indices.split(',') if value]
            if not faces or any(index < 0 or index >= len(obj.data.polygons) for index in faces):
                raise ValueError('画笔笔画包含无效的面索引')
            slot = brush_material_slot(_context.scene, obj)
            painted = palette()._apply_color_profile_to_polygons(
                obj, faces, self.color_index, self.surface_profile, self.category_index)
            if painted:
                assign_material(obj, faces, slot)
            return {'FINISHED'} if painted else {'CANCELLED'}
        except (ValueError, RuntimeError) as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}


class _CharacterBrushHelpers:
    """Reuse geometry helpers without inheriting a registered Operator RNA type."""


for _name, _method in vars(palette().WG_OT_PaletteBrush).items():
    if _name.startswith('_') and not _name.startswith('__') and callable(_method):
        setattr(_CharacterBrushHelpers, _name, _method)


class WG_ENV_OT_Brush(_CharacterBrushHelpers, bpy.types.Operator):
    bl_idname = 'wgame.environment_brush'
    bl_label = '场景画笔绘制'
    bl_description = '在 3D 视图用画笔给场景模型逐面涂调色板底色'
    bl_options = {'REGISTER', 'BLOCKING'}

    eyedropper_only: bpy.props.BoolProperty(default=False, options={'SKIP_SAVE'})

    @classmethod
    def poll(cls, context):
        obj = getattr(context, 'object', None)
        return (context.area is not None and context.area.type == 'VIEW_3D'
                and obj is not None and obj.type == 'MESH'
                and bool(obj.get('wgame_environment_asset'))
                and bool(context.scene.get('wgame_environment_work'))
                and not getattr(context.window_manager, 'wgame_environment_brush_active', False)
                and not getattr(context.window_manager, 'wgame_palette_brush_active', False))

    def _brush_radius(self, scene):
        return scene.wgame_environment_brush_radius

    def _brush_symmetry(self, scene):
        return scene.wgame_environment_brush_symmetry_x

    def _brush_cull_backfaces(self, scene):
        return scene.wgame_environment_brush_cull_backfaces

    def _build_geometry_cache(self, context):
        super()._build_geometry_cache(context)
        self._poly_categories = face_groups(self._target)

    def _finish(self, context):
        if getattr(self, '_finished', False):
            return
        self._finished = True
        context.window_manager.wgame_environment_brush_active = False
        if getattr(self, '_draw_handler', None) is not None:
            bpy.types.SpaceView3D.draw_handler_remove(self._draw_handler, 'WINDOW')
            self._draw_handler = None
        self._set_status(context, None)
        context.window.cursor_modal_restore()

    def _restore(self, before):
        restore(self._target, before)

    def _paint(self, context, event):
        index, location, normal = self._face_under_mouse(context, event)
        self._update_highlight(context, index, location, normal)
        if index is None:
            return
        faces = [index, *self._get_polygons_in_radius(context, location, normal)]
        faces = self._apply_symmetry_polys(context, faces)
        fresh = list(dict.fromkeys(face for face in faces if face not in self._stroke_faces))
        if fresh:
            module = palette()
            self._stroke_before.update(capture(self._target, fresh))
            if module._apply_color_profile_to_polygons(self._target, fresh, *self._stroke_color):
                assign_material(self._target, fresh, self._material_slot)
                self._stroke_faces.update(fresh)
                self._painted_faces.update(fresh)
                for face in fresh:
                    self._poly_categories[face] = ('slot', self._stroke_color[2])
                context.area.tag_redraw()

    def _commit(self, context):
        if not self._stroke_faces:
            return
        faces = sorted(self._stroke_faces)
        before = dict(self._stroke_before)
        self._restore(before)
        result = bpy.ops.wgame.environment_brush_stroke(
            'EXEC_DEFAULT', object_name=self._target.name,
            indices=','.join(map(str, faces)),
            color_index=self._stroke_color[0], surface_profile=self._stroke_color[1],
            category_index=self._stroke_color[2])
        if 'FINISHED' in result:
            bpy.ops.ed.undo_push(message='WGame 场景画笔笔画')
            self._history.append((faces, before))
            self._history = self._history[-30:]
        self._stroke_faces.clear()
        self._stroke_before.clear()

    def _sample(self, context, obj, index):
        scene = context.scene
        found = select_from_face(scene, obj, index)
        if found < 0:
            self.report({'WARNING'}, '已吸取颜色；请先将它加入场景配色 Profile')
        else:
            self.report({'INFO'}, f'已选中 {scene.wgame_environment_colors[found].label}')

    def _sample_target_at_mouse(self, context, event):
        region = self._window_region
        x = event.mouse_x - region.x
        y = event.mouse_y - region.y
        if not (0 <= x < region.width and 0 <= y < region.height):
            raise ValueError('请在 3D 视图的模型表面取色')
        point = (x, y)
        origin = view3d_utils.region_2d_to_origin_3d(region, self._region_3d, point)
        direction = view3d_utils.region_2d_to_vector_3d(region, self._region_3d, point)
        return sample_hit(context.scene, context.evaluated_depsgraph_get(), origin, direction)

    def execute(self, context):
        return self.invoke(context, None)

    def invoke(self, context, _event):
        obj = context.object
        if not self.eyedropper_only:
            color_profile.seed(context.scene)
            if color_profile.active(context.scene) is None:
                self.report({'ERROR'}, '请先在场景配色 Profile 中选择颜色')
                return {'CANCELLED'}
            try:
                self._material_slot = brush_material_slot(context.scene, obj)
            except ValueError as error:
                self.report({'ERROR'}, str(error))
                return {'CANCELLED'}
        region = next((r for r in context.area.regions if r.type == 'WINDOW'), None)
        if region is None or context.space_data.region_3d is None:
            return {'CANCELLED'}
        self._target = obj
        self._area = context.area
        self._window_region = region
        self._region_3d = context.space_data.region_3d
        self._finished = False
        self._painting = False
        self._alt_mode = False
        self._sizing_radius = False
        self._stroke_faces = set()
        self._stroke_before = {}
        self._painted_faces = set()
        self._history = []
        self._highlighted_polygon = None
        self._highlight_triangles = []
        self._highlight_edges = []
        self._highlight_circle = []
        self._build_geometry_cache(context)
        self._draw_handler = bpy.types.SpaceView3D.draw_handler_add(
            self._draw_highlight, (), 'WINDOW', 'POST_VIEW')
        context.window_manager.wgame_environment_brush_active = True
        context.window_manager.modal_handler_add(self)
        context.window.cursor_modal_set('EYEDROPPER' if self.eyedropper_only else 'PAINT_BRUSH')
        self._set_status(context, '场景吸管：单击已上色的面 · Esc 退出' if self.eyedropper_only else
                         '场景画笔：左键涂抹 · Shift 填充 · Alt 吸色 · F 调大小 · 0-9 切 Profile 颜色 · X 镜像 · Ctrl+Z 撤销 · Esc 退出')
        return {'RUNNING_MODAL'}

    def modal(self, context, event):
        if (not context.window_manager.wgame_environment_brush_active
                or context.mode=='PAINT_TEXTURE' or event.type == 'WINDOW_DEACTIVATE'):
            if self._painting:
                self._commit(context)
            self._finish(context)
            return {'FINISHED','PASS_THROUGH'}
        if self._is_mouse_over_ui(context, event):
            if self._painting:
                self._commit(context)
                self._painting = False
            self._update_highlight(context, None)
            return {'PASS_THROUGH'}
        if self._alt_mode != bool(event.alt):
            self._alt_mode = bool(event.alt)
            context.window.cursor_modal_set('EYEDROPPER' if self._alt_mode or self.eyedropper_only else 'PAINT_BRUSH')
        if self._sizing_radius:
            if event.type == 'MOUSEMOVE':
                delta = (event.mouse_x - self._sizing_start_x) * .0006
                context.scene.wgame_environment_brush_radius = max(.002, min(.6, self._sizing_start_r + delta))
            elif event.type in {'LEFTMOUSE', 'RET', 'NUMPAD_ENTER'}:
                self._sizing_radius = False
            elif event.type in {'RIGHTMOUSE', 'ESC'}:
                context.scene.wgame_environment_brush_radius = self._sizing_start_r
                self._sizing_radius = False
            return {'RUNNING_MODAL'}
        if event.type == 'ESC' and event.value == 'PRESS':
            if self._painting:
                self._commit(context)
            self._finish(context)
            return {'FINISHED'}
        if event.type == 'RIGHTMOUSE' and event.value == 'PRESS':
            if self._painting:
                self._commit(context)
            self._finish(context)
            return {'FINISHED'}
        if event.type == 'F' and event.value == 'PRESS':
            self._sizing_radius = True
            self._sizing_start_x = event.mouse_x
            self._sizing_start_r = context.scene.wgame_environment_brush_radius
            return {'RUNNING_MODAL'}
        if event.type in {'LEFT_BRACKET', 'BRACKET_LEFT', 'RIGHT_BRACKET', 'BRACKET_RIGHT'} and event.value == 'PRESS':
            factor = .8 if 'LEFT' in event.type else 1.25
            context.scene.wgame_environment_brush_radius = max(.002, min(.6, context.scene.wgame_environment_brush_radius * factor))
            return {'RUNNING_MODAL'}
        if event.type == 'X' and event.value == 'PRESS':
            context.scene.wgame_environment_brush_symmetry_x = not context.scene.wgame_environment_brush_symmetry_x
            return {'RUNNING_MODAL'}
        keys = {'ONE': 0, 'TWO': 1, 'THREE': 2, 'FOUR': 3, 'FIVE': 4,
                'SIX': 5, 'SEVEN': 6, 'EIGHT': 7, 'NINE': 8, 'ZERO': 9}
        if event.type in keys and event.value == 'PRESS':
            index = keys[event.type]
            if index < len(context.scene.wgame_environment_colors):
                context.scene.wgame_environment_color_active = index
                self.report({'INFO'}, '已切换至' + context.scene.wgame_environment_colors[index].label)
            return {'RUNNING_MODAL'}
        if event.ctrl and event.type == 'Z' and event.value == 'PRESS':
            if self._history:
                faces, before = self._history.pop()
                self._restore(before)
                self._poly_categories = face_groups(self._target)
                context.area.tag_redraw()
            return {'RUNNING_MODAL'}
        if event.type == 'LEFTMOUSE':
            if event.value == 'PRESS':
                if self.eyedropper_only or (event.alt and not event.shift):
                    try:
                        obj, index = self._sample_target_at_mouse(context, event)
                        self._sample(context, obj, index)
                    except ValueError as error:
                        self.report({'WARNING'}, str(error))
                    else:
                        if self.eyedropper_only:
                            self._finish(context)
                            return {'FINISHED'}
                    return {'RUNNING_MODAL'}
                index, location, normal = self._face_under_mouse(context, event)
                if index is None:
                    return {'PASS_THROUGH'}
                try:
                    self._stroke_color = selected_color(context.scene)
                except ValueError as error:
                    self.report({'WARNING'}, str(error))
                    return {'RUNNING_MODAL'}
                if event.shift or context.scene.wgame_environment_brush_fill_island:
                    match = None if event.alt else self._poly_categories[index]
                    faces = self._apply_symmetry_polys(context, self._get_island_polygons(index, match_category=match))
                    before = capture(self._target, faces)
                    if faces and 'FINISHED' in bpy.ops.wgame.environment_brush_stroke(
                            'EXEC_DEFAULT', object_name=self._target.name, indices=','.join(map(str, faces)),
                            color_index=self._stroke_color[0], surface_profile=self._stroke_color[1],
                            category_index=self._stroke_color[2]):
                        bpy.ops.ed.undo_push(message='WGame 场景孤岛填充')
                        self._history.append((faces, before))
                        self._history = self._history[-30:]
                        for face in faces:
                            self._poly_categories[face] = ('slot', self._stroke_color[2])
                        context.area.tag_redraw()
                    return {'RUNNING_MODAL'}
                self._painting = True
                self._stroke_faces.clear()
                self._stroke_before.clear()
                self._paint(context, event)
                return {'RUNNING_MODAL'}
            if event.value == 'RELEASE' and self._painting:
                self._commit(context)
                self._painting = False
                return {'RUNNING_MODAL'}
        if event.type == 'MOUSEMOVE':
            index, location, normal = self._face_under_mouse(context, event)
            self._update_highlight(context, index, location, normal)
            if self._painting:
                self._paint(context, event)
                return {'RUNNING_MODAL'}
        return {'PASS_THROUGH'}


class WG_ENV_OT_BrushStop(bpy.types.Operator):
    bl_idname = 'wgame.environment_brush_stop'
    bl_label = '退出场景画笔'

    @classmethod
    def poll(cls, context):
        return getattr(context.window_manager, 'wgame_environment_brush_active', False)

    def execute(self, context):
        context.window_manager.wgame_environment_brush_active = False
        return {'FINISHED'}
