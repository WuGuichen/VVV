# SPDX-License-Identifier: GPL-3.0-or-later
"""Canonical authoring UV order with loss-checked legacy consolidation."""
import bpy
import bmesh
from .detail_lifecycle import assert_mesh, tree_references, BACKUPS

NAMES = ('UV0', 'PaletteUV', 'DetailUV')


def empty(co):
    return abs(co[0]+1)<1e-7 and abs(co[1]+1)<1e-7


def same(a,b):
    return abs(a[0]-b[0])<1e-7 and abs(a[1]-b[1])<1e-7


def normalize(obj):
    mesh=obj.data
    if not mesh.uv_layers.get('PaletteUV'):
        raise ValueError('缺少 PaletteUV，请先用色板工具生成；不猜测取色坐标')
    snapshots={u.name:[tuple(l.uv) for l in u.data] for u in mesh.uv_layers}
    count=len(mesh.loops)
    details=list(snapshots.get('DetailUV',[(-1.,-1.)]*count))
    face=snapshots.get('FaceDetailUV')
    if face:
        conflicts=sum(not empty(a) and not empty(b) and not same(a,b) for a,b in zip(details,face))
        if conflicts:
            raise ValueError(f'FaceDetailUV 与 DetailUV 有 {conflicts} 个角点冲突，请先决定保留哪个布局；未修改')
        details=[b if empty(a) else a for a,b in zip(details,face)]
    extras=set(snapshots)-set(NAMES)
    for name in extras-{'FaceDetailUV'}:
        if any(not empty(co) and not same(co,target) for co,target in zip(snapshots[name],details)):
            raise ValueError(f'{name} 仍有独立展开，请先用正式替换/删除旧版流程处理；未修改')
    materials={s.material for s in obj.material_slots if s.material}
    for material in materials:
        if not material.use_nodes:continue
        for name in extras:
            if name!='FaceDetailUV' and tree_references(material.node_tree,name):
                raise ValueError(f'材质仍引用 {name}，未修改')
        for node in material.node_tree.nodes:
            if node.type=='GROUP' and any(tree_references(node.node_tree,n) for n in extras):
                raise ValueError('节点组仍引用待合并通道，请先迁移组内引用')
    for mod in obj.modifiers:
        if any(tree_references(getattr(mod,'node_group',None),n) for n in extras) or any(
            p.type=='STRING' and getattr(mod,p.identifier,None) in extras for p in mod.bl_rna.properties):
            raise ValueError('修改器引用了待移除通道，未修改')
    if obj.animation_data or mesh.animation_data:
        raise ValueError('对象/网格有动画或驱动，需人工核查 UV 引用')
    old_render=next((u.name for u in mesh.uv_layers if u.active_render),'UV0')
    if old_render in extras-{'FaceDetailUV'}:
        raise ValueError('待移除通道仍是默认渲染 UV，请先确认并切换')
    snapshots['UV0']=snapshots.get('UV0',[(-1.,-1.)]*count)
    snapshots['DetailUV']=details
    bm=bmesh.new()
    try:
        bm.from_mesh(mesh)
        loops=[l for f in bm.faces for l in f.loops]
        pins={u.name:[l[u].pin_uv for l in loops] for u in bm.loops.layers.uv.values()}
        for name in list(bm.loops.layers.uv.keys()):bm.loops.layers.uv.remove(bm.loops.layers.uv[name])
        for name in NAMES:
            uv=bm.loops.layers.uv.new(name)
            loops=[l for f in bm.faces for l in f.loops]
            pin_values=pins.get(name,[False]*count)
            for i,loop in enumerate(loops):
                loop[uv].uv=snapshots[name][i]
                loop[uv].pin_uv=pin_values[i]
        bm.to_mesh(mesh)
    finally:bm.free()
    replacements={}
    for material in materials:
        if material.use_nodes and any(n.type=='UVMAP' and n.uv_map=='FaceDetailUV' for n in material.node_tree.nodes):
            copied=material.copy()
            for node in copied.node_tree.nodes:
                if node.type=='UVMAP' and node.uv_map=='FaceDetailUV':node.uv_map='DetailUV'
            replacements[material]=copied
    for slot in obj.material_slots:
        if slot.material in replacements:slot.material=replacements[slot.material]
    mesh.uv_layers.active_index=2
    mesh.uv_layers['DetailUV' if old_render=='FaceDetailUV' else old_render].active_render=True
    mesh[BACKUPS]='[]'


class WGAMEUV_OT_normalize(bpy.types.Operator):
    bl_idname='wgame_uv.normalize_channels'
    bl_label='整理为三个正式 UV'
    bl_description='按 UV0、PaletteUV、DetailUV 排序；无冲突时合入旧脸部 UV，删除空白或重复通道'
    bl_options={'REGISTER','UNDO'}
    action:bpy.props.EnumProperty(items=[('NORMALIZE','整理',''),('EDIT','编辑 DetailUV','')])

    def execute(self,context):
        if self.action=='EDIT':
            obj=context.active_object
            if not obj or obj.type!='MESH' or not obj.data.uv_layers.get('DetailUV'):
                self.report({'WARNING'},'请先整理为三个正式 UV')
                return {'CANCELLED'}
            obj.data.uv_layers.active_index=obj.data.uv_layers.find('DetailUV')
            return {'FINISHED'}
        was_edit=context.mode=='EDIT_MESH'
        try:
            obj=assert_mesh(context)
            if was_edit:bpy.ops.object.mode_set(mode='OBJECT')
            normalize(obj)
            self.report({'INFO'},'正式 UV 已统一为 UV0 → PaletteUV → DetailUV；细节操作已切到 DetailUV')
        except (ValueError,RuntimeError) as exc:
            self.report({'ERROR'},str(exc))
            return {'CANCELLED'}
        finally:
            if was_edit and context.object and context.object.mode=='OBJECT':bpy.ops.object.mode_set(mode='EDIT')
        return {'FINISHED'}
