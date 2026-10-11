# SPDX-License-Identifier: GPL-3.0-or-later
"""Separate UV disk rounding from actual mesh sphere/ring fitting."""
import math
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from .islands import collect


def editable(context):
    from . import canvas_editor, decals
    if canvas_editor._session or decals._session:
        raise ValueError('请先确认或取消贴图预览，再修改 UV 或模型')
    if context.mode!='EDIT_MESH' or len(context.objects_in_mode)!=1:
        raise ValueError('请在一个网格的编辑模式中选择要处理的区域')
    obj=context.active_object
    if obj.data.users>1 or not obj.data.is_editable:
        raise ValueError('请使用可编辑的单用户网格，避免影响其他对象')
    return obj


def disk_targets(bm,uv,face_ids,strength):
    nodes={};coords=[];loops=[];edges={}
    for index in face_ids:
        face=bm.faces[index]
        ids=[]
        for loop in face.loops:
            if loop[uv].pin_uv:
                raise ValueError('岛中存在固定 UV 点，请先解除固定')
            co=loop[uv].uv
            key=(loop.vert.index,round(co.x,7),round(co.y,7))
            if key not in nodes:
                nodes[key]=len(coords);coords.append(tuple(co));loops.append([])
            n=nodes[key];ids.append(n);loops[n].append(loop)
        for a,b in zip(ids,ids[1:]+ids[:1]):
            edge=tuple(sorted((a,b)));edges[edge]=edges.get(edge,0)+1
    boundary={}
    for (a,b),count in edges.items():
        if count>2:raise ValueError('UV 岛有非流形连接，无法安全圆形化')
        if count==1:
            boundary.setdefault(a,[]).append(b);boundary.setdefault(b,[]).append(a)
    if len(boundary)<3 or any(len(v)!=2 for v in boundary.values()):
        raise ValueError('需要一圈完整外边界的圆盘 UV 岛')
    first=min(boundary);ring=[first];previous=None;current=first
    while True:
        following=next(n for n in boundary[current] if n!=previous)
        if following==first:break
        if following in ring:raise ValueError('UV 边界存在分叉')
        ring.append(following);previous,current=current,following
    if len(ring)!=len(boundary):
        raise ValueError('选中岛带有孔洞或多圈边界；本操作用于单边界圆盘')
    original=np.array(coords,dtype=float)
    outline=original[ring]
    signed=.5*np.sum(outline[:,0]*np.roll(outline[:,1],-1)-outline[:,1]*np.roll(outline[:,0],-1))
    if abs(signed)<1e-12:raise ValueError('UV 岛面积接近零，无法圆形化')
    center=(outline.min(axis=0)+outline.max(axis=0))*.5
    lengths=np.linalg.norm(np.roll(outline,-1,axis=0)-outline,axis=1)
    angles=math.atan2(*(outline[0]-center)[::-1])+np.sign(signed)*2*math.pi*np.r_[0,np.cumsum(lengths[:-1])]/lengths.sum()
    unit=np.stack([np.cos(angles),np.sin(angles)],axis=1)
    unit_area=abs(.5*np.sum(unit[:,0]*np.roll(unit[:,1],-1)-unit[:,1]*np.roll(unit[:,0],-1)))
    if unit_area<1e-10:raise ValueError('边界点分布过于退化，无法圆形化')
    positions=original.copy();positions[ring]=center+unit*math.sqrt(abs(signed)/unit_area)
    interior=np.array([i for i in range(len(coords)) if i not in boundary],dtype=int)
    # Harmonic relaxation with the circular boundary held fixed.
    if len(interior):
        pairs=np.array([(a,b) for a,b in edges]+[(b,a) for a,b in edges],dtype=int)
        degree=np.bincount(pairs[:,0],minlength=len(coords))
        for _ in range(500):
            sums=np.zeros_like(positions);np.add.at(sums,pairs[:,0],positions[pairs[:,1]])
            updated=sums[interior]/degree[interior,None]
            change=np.max(np.abs(updated-positions[interior]));positions[interior]=updated
            if change<1e-8:break
    result=original+(positions-original)*strength
    return [(loop,co) for same,co in zip(loops,result) for loop in same]


def round_points(points,mode,strength):
    p=np.asarray(points,dtype=float);origin=p.mean(axis=0);q=p-origin
    size=np.max(np.linalg.norm(q,axis=1))
    if size<1e-10:raise ValueError('选中顶点重合，无法圆整')
    q/=size
    if mode=='SPHERE':
        if len(p)<4:raise ValueError('球面圆整至少需要四个不共面的顶点')
        system=np.column_stack([2*q,np.ones(len(q))])
        fit,_,rank,_=np.linalg.lstsq(system,np.sum(q*q,axis=1),rcond=None)
        if rank<4:raise ValueError('选区接近一圈平面，请选择“圆环”，或选中完整眼球表面')
        center=fit[:3]
        if np.linalg.norm(center)>10:raise ValueError('选区不足以稳定确定球心，请扩大眼球选区')
        direction=q-center;length=np.linalg.norm(direction,axis=1)
        if np.any(length<1e-10):raise ValueError('选区包含球心顶点，无法确定移动方向')
        target=origin+size*(center+direction/length[:,None]*length.mean())
    else:
        _,singular,basis=np.linalg.svd(q,full_matrices=False)
        if len(p)<3 or singular[1]<1e-8:raise ValueError('圆环至少需要三个不共线顶点')
        plane=q@basis[:2].T
        fit,_,rank,_=np.linalg.lstsq(np.column_stack([2*plane,np.ones(len(p))]),np.sum(plane*plane,axis=1),rcond=None)
        center=fit[:2];direction=plane-center;length=np.linalg.norm(direction,axis=1)
        if rank<3 or np.any(length<1e-10):raise ValueError('请选择圆环边界，不要包含中心顶点')
        target=origin+size*((center+direction/length[:,None]*length.mean())@basis[:2])
    return p+(target-p)*strength


def round_surface(obj,bm,faces,strength):
    if not faces:raise ValueError('请用面选择选中完整圆面，包含内部面')
    vertices=sorted({v for f in faces for v in f.verts},key=lambda v:v.index)
    world=np.array([tuple(obj.matrix_world@v.co) for v in vertices])
    origin=world.mean(axis=0)
    _,singular,basis=np.linalg.svd(world-origin,full_matrices=False)
    if len(singular)<2 or singular[1]<1e-10:raise ValueError('选中区域接近直线，不能形成圆面')
    plane=(world-origin)@basis[:2].T
    temp=bmesh.new()
    try:
        verts=[temp.verts.new(tuple(p)) for p in world]
        mapping={v:i for i,v in enumerate(vertices)}
        for face in faces:temp.faces.new([verts[mapping[v]] for v in face.verts])
        temp.verts.index_update();temp.faces.index_update();temp.faces.ensure_lookup_table()
        uv=temp.loops.layers.uv.new('Projection')
        for face in temp.faces:
            for loop in face.loops:loop[uv].uv=plane[loop.vert.index]
        try:changes=disk_targets(temp,uv,list(range(len(temp.faces))),1.)
        except ValueError as exc:raise ValueError('圆面需要一块连续、无孔的选区：'+str(exc)) from exc
        result=plane.copy()
        for loop,co in changes:result[loop.vert.index]=co
        target=origin+result@basis[:2]
        return vertices,world+(target-world)*strength
    finally:temp.free()


class WGAMEUV_OT_round_uv(bpy.types.Operator):
    bl_idname='wgame_uv.round_uv'
    bl_label='选中 UV 岛圆形化'
    bl_description='将单边界岛整理成圆盘，固定圆形边界平滑内部；只改 UV，可撤销'
    bl_options={'REGISTER','UNDO'}
    def execute(self,context):
        try:
            obj=editable(context)
            bm,uv,islands=collect(obj,context.scene.tool_settings.use_uv_select_sync)
            selected=[ids for ids,chosen in islands if chosen]
            if not selected:raise ValueError('请选中需要圆形化的 UV 岛')
            changes=[]
            for ids in selected:changes.extend(disk_targets(bm,uv,ids,context.scene.wgame_uv_tools.uv_round_strength))
            for loop,co in changes:loop[uv].uv=co
            bmesh.update_edit_mesh(obj.data,loop_triangles=False,destructive=False)
            self.report({'INFO'},f'已圆形化 {len(selected)} 个 UV 岛；模型顶点未修改')
        except (ValueError,RuntimeError) as exc:
            self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


class WGAMEUV_OT_round_mesh(bpy.types.Operator):
    bl_idname='wgame_uv.round_mesh'
    bl_label='圆整选中模型顶点'
    bl_description='改变真实几何：圆面整理外边界并平滑内部，圆环只处理边界点，球面用于完整球体。UV 不变，可撤销'
    bl_options={'REGISTER','UNDO'}
    def execute(self,context):
        try:
            obj=editable(context)
            if obj.data.shape_keys:raise ValueError('模型含形态键，本版不直接圆整，避免破坏表情数据')
            bm=bmesh.from_edit_mesh(obj.data)
            bm.verts.index_update()
            vertices=[v for v in bm.verts if v.select and not v.hide]
            if len(vertices)<3:raise ValueError('请先选中眼球表面或圆环边界的顶点')
            settings=context.scene.wgame_uv_tools
            if settings.mesh_round_mode=='DISK':
                vertices,result=round_surface(obj,bm,[f for f in bm.faces if f.select and not f.hide],settings.mesh_round_strength)
            else:
                result=round_points([tuple(obj.matrix_world@v.co) for v in vertices],settings.mesh_round_mode,settings.mesh_round_strength)
            inverse=obj.matrix_world.inverted()
            for vertex,co in zip(vertices,result):vertex.co=inverse@Vector(co)
            bm.normal_update();bmesh.update_edit_mesh(obj.data,loop_triangles=True,destructive=False)
            self.report({'INFO'},f'已圆整 {len(vertices)} 个模型顶点；未改变 UV、拓扑或权重')
        except (ValueError,RuntimeError,np.linalg.LinAlgError) as exc:
            self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}
