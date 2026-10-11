# SPDX-License-Identifier: GPL-3.0-or-later
"""Island scope and isolated wrappers around Blender's native UV operators."""
import math
import bpy
import bmesh
from mathutils import Vector


class WGAMEUV_OT_clear_selected(bpy.types.Operator):
    bl_idname = 'wgame_uv.clear_selected'
    bl_label = '选中 UV 设为 (-1, -1)'
    bl_description = '将选中的 UV 点收拢到 (-1, -1)；不扩展到整岛，可撤销'
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        if context.mode != 'EDIT_MESH':
            self.report({'WARNING'}, '请进入编辑模式并选择要留空的 UV')
            return {'CANCELLED'}
        sync = context.scene.tool_settings.use_uv_select_sync
        settings = context.scene.wgame_uv_tools
        targets = []
        for obj in context.objects_in_mode_unique_data:
            if obj.type != 'MESH':
                continue
            bm = bmesh.from_edit_mesh(obj.data)
            uv = bm.loops.layers.uv.active
            if not uv or not (uv.name == 'DetailUV' or uv.name.startswith('DetailUV_Work')) or (settings.uv_map and settings.uv_map != uv.name):
                self.report({'WARNING'}, '请确认活动细节 UV；禁止修改 PaletteUV，且检查 UV 必须与活动 UV 一致')
                return {'CANCELLED'}
            loops = [l for f in bm.faces if not f.hide and (sync or f.select) for l in f.loops
                     if ((f.select if context.tool_settings.mesh_select_mode[2] else l.vert.select)
                         if sync else l.uv_select_vert)]
            targets.append((obj, uv, loops))
        count = sum(len(loops) for _, _, loops in targets)
        if not count:
            self.report({'WARNING'}, '没有选中的 UV 点')
            return {'CANCELLED'}
        for obj, uv, loops in targets:
            for loop in loops:
                loop[uv].uv = (-1.0, -1.0)
            bmesh.update_edit_mesh(obj.data, loop_triangles=False, destructive=False)
        self.report({'INFO'}, f'已将 {count} 个选中 UV 角点设为 (-1, -1)，可 Ctrl+Z 撤销')
        return {'FINISHED'}


def area(coords):
    return abs(sum(a.x * b.y - b.x * a.y for a, b in zip(coords, coords[1:] + coords[:1]))) * 0.5


def collect(obj, sync):
    bm = bmesh.from_edit_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bm.verts.index_update()
    bm.verts.index_update()
    bm.faces.index_update()
    layer = bm.loops.layers.uv.active
    if layer is None or not (layer.name == 'DetailUV' or layer.name.startswith('DetailUV_Work')):
        raise ValueError('请使用 DetailUV 或临时 DetailUV_Work；UV0 和 PaletteUV 不做细节修整')
    valid = {f for f in bm.faces if area([l[layer].uv.copy() for l in f.loops]) > 1e-12}
    remaining = set(valid)
    islands = []
    while remaining:
        seed = min(remaining, key=lambda f: f.index)
        remaining.remove(seed)
        stack, faces = [seed], []
        while stack:
            face = stack.pop()
            faces.append(face)
            for edge in face.edges:
                own = {l.vert: l[layer].uv for l in face.loops if l.vert in edge.verts}
                for other in edge.link_faces:
                    if other not in remaining:
                        continue
                    theirs = {l.vert: l[layer].uv for l in other.loops if l.vert in edge.verts}
                    if all((own[v] - theirs[v]).length_squared < 1e-12 for v in edge.verts):
                        remaining.remove(other)
                        stack.append(other)
        # Hidden faces protect the entire island from partial processing.
        if any(f.hide for f in faces):
            continue
        chosen = any(f.select and (sync or any(l.uv_select_vert for l in f.loops)) for f in faces)
        islands.append((sorted(f.index for f in faces), chosen))
    return bm, layer, islands


def native_operation(context, obj, indices, operation, settings, seam_edges=()):
    """Use a disposable scene so native tools cannot touch other islands or UV maps."""
    bm = bmesh.from_edit_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bm.verts.index_update()
    uv = bm.loops.layers.uv.active
    vertices, mapping, faces, coords = [], {}, [], []
    for index in indices:
        face = bm.faces[index]
        verts = []
        for loop in face.loops:
            key = loop.vert.index
            if key not in mapping:
                mapping[key] = len(vertices)
                vertices.append(tuple(loop.vert.co))
            verts.append(mapping[key])
        faces.append(verts)
        coords.append([tuple(l[uv].uv) for l in face.loops])
    seam_keys = set()
    if operation == 'UNWRAP':
        source_seams = set(seam_edges)
        source_seams.update(tuple(sorted(v.index for v in e.verts)) for e in bm.edges if e.seam)
        seam_keys = {tuple(sorted((mapping[a],mapping[b]))) for a,b in source_seams if a in mapping and b in mapping}
    old_scene = context.window.scene
    old_active = context.view_layer.objects.active
    edit_objects = list(context.objects_in_mode)
    temp_scene = temp_mesh = temp_obj = None
    try:
        bpy.ops.object.mode_set(mode='OBJECT')
        temp_scene = bpy.data.scenes.new('WGame UV Temporary')
        temp_mesh = bpy.data.meshes.new('WGame UV Temporary')
        temp_mesh.from_pydata(vertices, [], faces)
        for edge in temp_mesh.edges:
            edge.use_seam = tuple(sorted(edge.vertices)) in seam_keys
        temp_mesh.uv_layers.new(name='UV')
        for face, uvs in zip(temp_mesh.polygons, coords):
            for loop_index, co in zip(face.loop_indices, uvs):
                temp_mesh.uv_layers.active.data[loop_index].uv = co
        temp_obj = bpy.data.objects.new('WGame UV Temporary', temp_mesh)
        temp_scene.collection.objects.link(temp_obj)
        context.window.scene = temp_scene
        temp_obj.select_set(True)
        context.view_layer.objects.active = temp_obj
        temp_scene.tool_settings.use_uv_select_sync = False
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.select_all(action='SELECT')
        if operation == 'OVERLAP':
            bpy.ops.uv.select_overlap(extend=False)
        elif operation == 'RELAX':
            bpy.ops.uv.minimize_stretch(iterations=settings.relax_iterations, fill_holes=True)
        elif operation in {'UNWRAP', 'SMART'}:
            if operation == 'SMART':
                bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02,
                    correct_aspect=False, scale_to_bounds=True)
            else:
                bpy.ops.uv.unwrap(method='ANGLE_BASED', fill_holes=True, correct_aspect=False,
                    margin=0.02)
        else:
            bpy.ops.uv.pack_islands(rotate=True, scale=True, merge_overlap=False,
                margin_method='FRACTION', margin=settings.padding / settings.resolution,
                udim_source='ORIGINAL_AABB')
        result_bm = bmesh.from_edit_mesh(temp_mesh)
        result_bm.faces.ensure_lookup_table()
        result_uv = result_bm.loops.layers.uv.active
        if operation == 'OVERLAP':
            return {indices[f.index] for f in result_bm.faces if any(l.uv_select_vert for l in f.loops)}
        return {indices[f.index]: [l[result_uv].uv.copy() for l in f.loops] for f in result_bm.faces}
    finally:
        if context.object and context.object.mode == 'EDIT' and context.object == temp_obj:
            bpy.ops.object.mode_set(mode='OBJECT')
        context.window.scene = old_scene
        context.view_layer.objects.active = old_active
        for ob in edit_objects:
            ob.select_set(True)
        if old_active and old_active.mode != 'EDIT':
            bpy.ops.object.mode_set(mode='EDIT')
        if temp_obj:
            bpy.data.objects.remove(temp_obj, do_unlink=True)
        if temp_mesh:
            bpy.data.meshes.remove(temp_mesh)
        if temp_scene:
            bpy.data.scenes.remove(temp_scene)


class WGAMEUV_OT_island_tools(bpy.types.Operator):
    bl_idname = 'wgame_uv.island_tools'
    bl_label = 'UV 岛修整'
    bl_options = {'REGISTER', 'UNDO'}
    action: bpy.props.EnumProperty(items=[('OVERLAP', '检查重叠', ''), ('RELAX', '松弛选中岛', ''),
        ('SEPARATE', '分开选中岛', ''), ('PACK', '打包选中岛', '')])

    def execute(self, context):
        if context.mode != 'EDIT_MESH' or not context.active_object:
            self.report({'WARNING'}, '请进入网格编辑模式')
            return {'CANCELLED'}
        if len(context.objects_in_mode) != 1:
            self.report({'WARNING'}, '本版请一次编辑一个网格对象')
            return {'CANCELLED'}
        uv_area = next((a for a in context.screen.areas if a.type == 'IMAGE_EDITOR' and a.ui_type == 'UV'), None)
        if not uv_area:
            self.report({'WARNING'}, '请打开 UV Editing 工作区')
            return {'CANCELLED'}
        obj = context.active_object
        settings = context.scene.wgame_uv_tools
        if not obj.data.uv_layers.active:
            self.report({'WARNING'}, '当前网格没有 UV 通道')
            return {'CANCELLED'}
        if settings.uv_map and settings.uv_map != obj.data.uv_layers.active.name:
            self.report({'WARNING'}, '检查 UV 与活动 UV 不一致，请先选择要编辑的活动 UV')
            return {'CANCELLED'}
        try:
            bm, uv, islands = collect(obj, context.scene.tool_settings.use_uv_select_sync)
            chosen = [faces for faces, selected in islands if selected or self.action == 'OVERLAP']
            if not chosen:
                raise ValueError('没有可处理的有效 UV 岛；请选择岛，留空区域不会处理')
            ids = sorted(i for group in chosen for i in group)
            if self.action != 'OVERLAP' and any(l[uv].pin_uv for i in ids for l in bm.faces[i].loops):
                raise ValueError('选中岛包含固定点，请先解除固定或取消选择该岛')
            old = {i: [l[uv].uv.copy() for l in bm.faces[i].loops] for i in ids}
            if self.action == 'SEPARATE':
                all_coords = [l[uv].uv for f in bm.faces for l in f.loops]
                cursor = max(1.0, max(co.x for co in all_coords)) + settings.padding / settings.resolution
                result = {}
                for group in chosen:
                    points = [co for i in group for co in old[i]]
                    dx, dy = cursor - min(p.x for p in points), -min(p.y for p in points)
                    for i in group:
                        result[i] = [co + Vector((dx, dy)) for co in old[i]]
                    cursor += max(p.x for p in points) - min(p.x for p in points) + settings.padding / settings.resolution
            else:
                with context.temp_override(area=uv_area):
                    result = native_operation(context, obj, ids, self.action, settings)
            bm, uv, _ = collect(obj, context.scene.tool_settings.use_uv_select_sync)
            if self.action == 'OVERLAP':
                marked = {i for group in chosen if any(i in result for i in group) for i in group}
                for face in bm.faces:
                    if face.hide:
                        continue
                    face.select_set(face.index in marked)
                    for loop in face.loops:
                        loop.uv_select_vert = face.index in marked
                        loop.uv_select_edge = face.index in marked
                bm.select_flush_mode()
                self.report({'INFO'}, f'找到 {sum(any(i in result for i in g) for g in chosen)} 个涉及重叠的岛；请确认是否为有意重叠')
            else:
                if self.action == 'RELAX':
                    for group in chosen:
                        before = [p for i in group for p in old[i]]
                        after = [p for i in group for p in result[i]]
                        old_area = sum(area(old[i]) for i in group)
                        new_area = sum(area(result[i]) for i in group)
                        if new_area <= 1e-12:
                            raise ValueError('松弛结果退化，未写入原模型')
                        scale = math.sqrt(old_area / new_area)
                        def center(points):
                            return Vector(((min(p.x for p in points)+max(p.x for p in points))/2,
                                           (min(p.y for p in points)+max(p.y for p in points))/2))
                        a, b = center(before), center(after)
                        for i in group:
                            result[i] = [(co-b)*scale+a for co in result[i]]
                if self.action == 'PACK':
                    # Normalize the destination tile to 0-1 even after separation outside it.
                    points = [p for values in result.values() for p in values]
                    offset = Vector((math.floor(min(p.x for p in points)), math.floor(min(p.y for p in points))))
                    result = {i: [p-offset for p in values] for i, values in result.items()}
                for i, coords in result.items():
                    for loop, co in zip(bm.faces[i].loops, coords):
                        loop[uv].uv = co
                self.report({'INFO'}, f'已处理 {len(chosen)} 个岛；可撤销，现有贴图对应位置会改变')
            bmesh.update_edit_mesh(obj.data, loop_triangles=False, destructive=False)
        except (ValueError, RuntimeError) as exc:
            self.report({'ERROR'}, str(exc))
            return {'CANCELLED'}
        return {'FINISHED'}

