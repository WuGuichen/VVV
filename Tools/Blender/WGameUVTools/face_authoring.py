# SPDX-License-Identifier: GPL-3.0-or-later
"""Build detail UVs from a 3D face selection, independent of previous UV islands."""
import math
import heapq
import bpy
import bmesh
from mathutils import Vector
from .islands import native_operation, area


def connected_faces(faces):
    todo = set(faces)
    for first in sorted(todo, key=lambda f:f.index):
        if first not in todo:continue
        todo.remove(first)
        stack = [first]
        component = set()
        while stack:
            face = stack.pop()
            component.add(face)
            for edge in face.edges:
                for other in edge.link_faces:
                    if other in todo:
                        todo.remove(other)
                        stack.append(other)
        yield component


def can_unwrap_continuously(faces):
    for component in connected_faces(faces):
        edges = {e for f in component for e in f.edges}
        counts = [sum(f in component for f in e.link_faces) for e in edges]
        if not any(n == 1 for n in counts) or any(n > 2 for n in counts):
            return False
    return True


def band_seams(faces):
    """Cut separated end rings in the temporary unwrap, preserving planar holes."""
    seams = set()
    for component in connected_faces(faces):
        edges = {e for f in component for e in f.edges}
        vertices = {v for f in component for v in f.verts}
        counts = {e:sum(f in component for f in e.link_faces) for e in edges}
        if len(vertices)-len(edges)+len(component) != 0 or any(n>2 for n in counts.values()):continue
        boundary = {e for e,n in counts.items() if n==1}
        rim_vertices = {v for e in boundary for v in e.verts}
        if any(sum(e in boundary for e in v.link_edges)!=2 for v in rim_vertices):continue
        rings=[];remaining=set(rim_vertices)
        while remaining:
            first=min(remaining,key=lambda v:v.index);remaining.remove(first)
            ring={first};stack=[first]
            while stack:
                v=stack.pop()
                for e in v.link_edges:
                    other=e.other_vert(v)
                    if e in boundary and other in remaining:
                        remaining.remove(other);ring.add(other);stack.append(other)
            rings.append(ring)
        if len(rings)!=2:continue
        centers=[sum((v.co for v in ring),Vector())/len(ring) for ring in rings]
        axis=centers[1]-centers[0]
        extent=max((v.co-centers[0]).length for v in vertices)
        epsilon=max(extent*1e-5,1e-10)
        if axis.length<=epsilon:continue
        axis.normalize()
        # A washer or a facial patch with a hole has overlapping end projections.
        # A straight post/cylinder has distinct rims separated along its axis.
        if min(v.co.dot(axis) for v in rings[1])-max(v.co.dot(axis) for v in rings[0])<=epsilon:continue
        internal={e for e,n in counts.items() if n==2}
        reached=set(rings[0]);stack=list(reached)
        while stack:
            v=stack.pop()
            for e in v.link_edges:
                other=e.other_vert(v)
                if e in internal and e.seam and other not in reached:
                    reached.add(other);stack.append(other)
        if reached & rings[1]:continue  # The author's cut already opens the band.
        distances={v:0. for v in rings[0]};previous={}
        queue=[(0.,v.index,v) for v in rings[0]];heapq.heapify(queue)
        end=None
        while queue:
            distance,_,v=heapq.heappop(queue)
            if distance!=distances[v]:continue
            if v in rings[1]:end=v;break
            for e in sorted((e for e in v.link_edges if e in internal),key=lambda e:e.other_vert(v).index):
                other=e.other_vert(v)
                candidate=distance+max(e.calc_length(),epsilon)*(.25 if e.seam else 1.)
                if candidate<distances.get(other,float('inf')):
                    distances[other]=candidate;previous[other]=(v,e)
                    heapq.heappush(queue,(candidate,other.index,other))
        while end in previous:
            end,e=previous[end]
            seams.add(tuple(sorted(v.index for v in e.verts)))
    return seams


class WGAMEUV_OT_face_authoring(bpy.types.Operator):
    bl_idname = 'wgame_uv.face_authoring'
    bl_label = '选面制作细节 UV'
    bl_options = {'REGISTER', 'UNDO'}
    action: bpy.props.EnumProperty(items=[('CREATE', '从选中面生成 UV 岛', ''),
        ('CLEAR', '选中面取消细节 UV', ''), ('NEW', '新建空白细节 UV（保留旧版）', '')])

    def execute(self, context):
        if context.mode != 'EDIT_MESH' or len(context.objects_in_mode_unique_data) != 1:
            self.report({'WARNING'}, '请一次编辑一个网格，在模型上选择要绘制的面')
            return {'CANCELLED'}
        obj = context.active_object
        bm = bmesh.from_edit_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        bm.faces.index_update()
        bm.verts.index_update()
        settings = context.scene.wgame_uv_tools
        if self.action == 'NEW':
            if len(bm.loops.layers.uv) >= 8:
                self.report({'WARNING'}, 'UV 通道已达到 8 个，请先整理不需要的通道')
                return {'CANCELLED'}
            uv = bm.loops.layers.uv.new('DetailUV_Work')
            name = uv.name
            for face in bm.faces:
                for loop in face.loops:
                    loop[uv].uv = (-1, -1)
            bmesh.update_edit_mesh(obj.data)
            obj.data.uv_layers.active_index = obj.data.uv_layers.find(name)
            settings.uv_map = ''
            self.report({'INFO'}, f'已新建 {name}，旧通道保留；材质仍使用原通道，棋盘格可检查新通道')
            return {'FINISHED'}
        uv = bm.loops.layers.uv.active
        if not uv or uv.name in {'UV0', 'PaletteUV', 'FaceDetailUV'} or (settings.uv_map and settings.uv_map != uv.name):
            self.report({'WARNING'}, '请选择新建的工作 UV；不能改 UV0、PaletteUV 或 FaceDetailUV，检查 UV 应与活动层一致')
            return {'CANCELLED'}
        faces = [f for f in bm.faces if f.select and not f.hide]
        if not faces:
            self.report({'WARNING'}, '请先在三维模型上选中完整的面')
            return {'CANCELLED'}
        if self.action == 'CLEAR':
            for face in faces:
                for loop in face.loops:
                    loop[uv].uv = (-1, -1)
            bmesh.update_edit_mesh(obj.data, loop_triangles=False, destructive=False)
            self.report({'INFO'}, f'已将 {len(faces)} 个面的全部 UV 角点留空，可撤销')
            return {'FINISHED'}
        if any(l[uv].pin_uv for f in faces for l in f.loops):
            self.report({'WARNING'}, '选中面有固定 UV 点，请先解除固定')
            return {'CANCELLED'}
        editor = context.area if context.area and context.area.type == 'IMAGE_EDITOR' else None
        if editor is None:
            editor = next((a for a in context.screen.areas if a.type == 'IMAGE_EDITOR' and a.ui_type == 'UV'), None)
        if editor is None:
            editor = next((a for a in context.screen.areas if a.type == 'IMAGE_EDITOR'), None)
        if not editor:
            self.report({'WARNING'}, '请打开 UV 或图片编辑器，也可切到 UV Editing 工作区')
            return {'CANCELLED'}
        # Texture painting changes this editor to PAINT even in UV Editing.
        # Restore its UV display without replacing the canvas or its pixels.
        if editor.ui_type != 'UV':
            image = editor.spaces.active.image
            editor.ui_type = 'UV'
            editor.spaces.active.image = image
        ids = [f.index for f in faces]
        id_set = set(ids)
        operation = 'SMART' if settings.face_unwrap_mode == 'SPLIT' else 'UNWRAP'
        if operation == 'UNWRAP' and not can_unwrap_continuously(faces):
            self.report({'WARNING'}, '选区封闭或存在非流形连接；请缩小到开放区域，或明确选择“自动切分”')
            return {'CANCELLED'}
        other = [l[uv].uv.copy() for f in bm.faces if f.index not in id_set for l in f.loops
                 if tuple(l[uv].uv) != (-1.0, -1.0)]
        cursor = max((p.x for p in other), default=-0.05) + 0.05
        seams = band_seams(faces) if operation == 'UNWRAP' else set()
        try:
            with context.temp_override(area=editor):
                result = native_operation(context, obj, ids, operation, settings, seam_edges=seams)
            if any(not math.isfinite(v) for coords in result.values() for co in coords for v in co):
                raise ValueError('展开结果无效，未写入原模型')
            if any(area(coords) < 1e-12 for coords in result.values()):
                raise ValueError('选区存在无法展开的退化面，未写入原模型')
            points = [p for coords in result.values() for p in coords]
            offset = Vector((cursor-min(p.x for p in points), -min(p.y for p in points)))
            bm = bmesh.from_edit_mesh(obj.data)
            bm.faces.ensure_lookup_table()
            uv = bm.loops.layers.uv.active
            for f in bm.faces:
                for loop in f.loops:
                    loop.uv_select_vert = f.index in result
                    loop.uv_select_edge = f.index in result
            for index, coords in result.items():
                for loop, co in zip(bm.faces[index].loops, coords):
                    loop[uv].uv = co + offset
            bmesh.update_edit_mesh(obj.data, loop_triangles=False, destructive=False)
            with context.temp_override(area=editor, region=next(r for r in editor.regions if r.type == 'WINDOW')):
                bpy.ops.image.view_selected()
            self.report({'INFO'}, f'已重新展开 {len(ids)} 个面；' +
                ('复杂/封闭区域已自动切分为适合展开的岛' if operation == 'SMART' else
                 '环形侧面已沿临时纵向切缝展开' if seams else '开放区域按连通块与已有切缝展开'))
        except (RuntimeError, ValueError) as exc:
            self.report({'ERROR'}, str(exc))
            return {'CANCELLED'}
        return {'FINISHED'}
