# SPDX-License-Identifier: GPL-3.0-or-later
"""Selected-face planar decals with a disposable preview and explicit image undo."""
import math
import os
from contextlib import contextmanager
from pathlib import Path
from datetime import datetime
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from bpy_extras.io_utils import ImportHelper

TAG = 'wgame_decal_preview'
_session = None
_undo = globals().get('_undo', None)


def cancel():
    global _session
    _session = None
    for obj in list(bpy.data.objects):
        if obj.get(TAG):
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        if mat.get(TAG) and mat.users == 0:
            bpy.data.materials.remove(mat)


def clear_session():
    global _undo
    cancel()
    _undo = None


def pixels(image):
    if image.source in {'TILED', 'MOVIE', 'SEQUENCE', 'VIEWER'} or image.channels != 4:
        raise ValueError('本版需要普通 RGBA 图片，不支持 UDIM、视频或无 Alpha 的画布')
    if image.alpha_mode not in {'STRAIGHT', 'PREMUL'}:
        raise ValueError('图片 Alpha 模式需要 Straight 或 Premultiplied')
    w, h = image.size
    if w < 1 or h < 1:
        raise ValueError('图片没有可读取的像素')
    values = np.empty(w*h*4, dtype=np.float32)
    image.pixels.foreach_get(values)
    values = values.reshape(h, w, 4)
    if image.alpha_mode == 'PREMUL':
        values[:,:,:3] = np.divide(values[:,:,:3], values[:,:,3:4], out=np.zeros_like(values[:,:,:3]), where=values[:,:,3:4]>1e-8)
    return values


def write_pixels(image, values):
    values = values.copy()
    if image.alpha_mode == 'PREMUL':
        values[:,:,:3] *= values[:,:,3:4]
    image.pixels.foreach_set(values.ravel())
    image.update()


def target_for_faces(obj, faces):
    images = set()
    for index in {f.material_index for f in faces}:
        mat = obj.material_slots[index].material if index < len(obj.material_slots) else None
        if not mat or not mat.use_nodes:
            raise ValueError('选中面没有细节材质')
        candidates = []
        # WGame environments share DetailUV between color and emission canvases.
        # The active image node is selected explicitly by the scene paint buttons.
        active=mat.node_tree.nodes.active
        channel=(active.name if active and active.name=='Emission_Detail_Texture' else 'Detail_Texture')
        for node in mat.node_tree.nodes:
            if mat.get('wgame_environment_shared') and node.name in {'Detail_Texture','Emission_Detail_Texture'} and node.name!=channel:
                continue
            if node.type != 'TEX_IMAGE' or not node.image:
                continue
            links = node.inputs['Vector'].links
            if links and links[0].from_node.type == 'UVMAP' and links[0].from_node.uv_map == 'DetailUV':
                candidates.append(node.image)
        if len(set(candidates)) != 1:
            raise ValueError('每个选中面的材质需有一张直接使用 DetailUV 的细节图片')
        images.add(candidates[0])
    if len(images) != 1:
        raise ValueError('选中面使用多张细节图片，请分批贴花')
    return images.pop()


def projected_uv(points, settings, session):
    # Projection frame is captured at preview start; orbiting does not move the decal.
    center = session['center'] + session['span'] * np.array([settings.decal_x, settings.decal_y])
    q = (np.asarray(points)-center) / (session['span'] * settings.decal_scale)
    angle = settings.decal_angle
    c, s = math.cos(angle), math.sin(angle)
    q = q @ np.array([[c, -s], [s, c]])
    q[:,1] *= session['aspect']
    return q + .5


def refresh_preview(self=None, context=None):
    if not _session:
        return
    settings = _session['scene'].wgame_uv_tools
    obj = _session['preview']
    coords = projected_uv(_session['points'], settings, _session)
    for loop, co in zip(obj.data.uv_layers['DecalUV'].data, coords):
        loop.uv = co
    obj.active_material.node_tree.nodes['Opacity'].inputs[1].default_value = settings.decal_opacity
    obj.data.update()


@contextmanager
def mesh_snapshot(obj):
    if obj.mode == 'EDIT':
        yield bmesh.from_edit_mesh(obj.data)
    else:
        bm = bmesh.new()
        try:
            bm.from_mesh(obj.data)
            bm.normal_update()
            yield bm
        finally:
            bm.free()


def start(context):
    obj = context.active_object
    if not obj or obj.type != 'MESH' or context.mode not in {'EDIT_MESH', 'PAINT_TEXTURE'}:
        raise ValueError('请进入细节绘制，使用面遮罩选择接收区域')
    if context.mode == 'EDIT_MESH' and len(context.objects_in_mode) != 1:
        raise ValueError('请一次处理一个网格')
    if context.mode == 'PAINT_TEXTURE' and not obj.data.use_paint_mask:
        raise ValueError('请开启面遮罩后选择接收面，或点击“进入细节绘制”')
    with mesh_snapshot(obj) as bm:
        return start_on_mesh(context, obj, bm)


def start_on_mesh(context, obj, bm):
    global _session
    settings = context.scene.wgame_uv_tools
    image = settings.decal_source
    if not image:
        raise ValueError('请先选择贴花素材')
    pixels(image)
    if max(image.size)>4096:
        raise ValueError('本版素材最大支持 4096 像素，请先缩小素材')
    obj = context.active_object
    if any(m.show_viewport and m.type not in {'WEIGHTED_NORMAL', 'NORMAL_EDIT'} for m in obj.modifiers):
        raise ValueError('请先关闭棋盘格及会改变预览表面的修改器，再开始贴花；本版按编辑网格投射')
    editor = next((a for a in context.screen.areas if a.type=='VIEW_3D'), None)
    if not editor:
        raise ValueError('请打开三维视图')
    bm.faces.ensure_lookup_table(); bm.faces.index_update()
    uv = bm.loops.layers.uv.get('DetailUV')
    faces = [f for f in bm.faces if f.select and not f.hide]
    if not uv or not faces:
        raise ValueError('需要 DetailUV 和选中的接收面')
    for face in faces:
        coords=[tuple(loop[uv].uv) for loop in face.loops]
        if all(co==(-1.,-1.) for co in coords):
            raise ValueError('选中面包含留空的 DetailUV，请先展开需要细节的面')
        if not all(math.isfinite(v) for co in coords for v in co):
            raise ValueError('选中面的 DetailUV 含无效数值')
        if abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(coords,coords[1:]+coords[:1])))<2e-12:
            raise ValueError('选中面的 DetailUV 已退化，请重新展开或取消细节 UV')
    target = target_for_faces(obj, faces)
    pixels(target)
    if target == image:
        raise ValueError('素材与目标画布不能是同一张图片')
    if max(target.size)>4096:
        raise ValueError('本版目标画布最大支持 4096 像素')
    if image.colorspace_settings.name != target.colorspace_settings.name:
        raise ValueError('素材和细节图请使用相同色彩空间（颜色图通常都设为 sRGB）')
    rotation = editor.spaces.active.region_3d.view_rotation
    right, up = rotation @ Vector((1,0,0)), rotation @ Vector((0,1,0))
    verts, polys, points, tri_uv, tri_points, canvas_coords = [], [], [], [], [], []
    normals = obj.matrix_world.to_3x3().inverted().transposed()
    for face in faces:
        poly=[]
        for loop in face.loops:
            world = obj.matrix_world @ loop.vert.co
            normal = (normals @ face.normal).normalized()
            poly.append(len(verts)); verts.append(tuple(world+normal*.0002))
            points.append((world.dot(right),world.dot(up)))
            canvas_coords.append(tuple(loop[uv].uv))
        polys.append(poly)
    selected=set(faces)
    for tri in bm.calc_loop_triangles():
        if tri[0].face not in selected:continue
        tri_uv.append([tuple(l[uv].uv) for l in tri])
        tri_points.append([((obj.matrix_world@l.vert.co).dot(right),(obj.matrix_world@l.vert.co).dot(up)) for l in tri])
    points=np.asarray(points)
    span=float(np.ptp(points[:,0]))
    if span<1e-8:
        raise ValueError('选区侧对当前视图，请把视图转向接收面')
    cancel()
    mesh=bpy.data.meshes.new('WGame 贴花预览');mesh.from_pydata(verts,[],polys);mesh.uv_layers.new(name='DecalUV')
    receiver=mesh.uv_layers.new(name='ReceiverUV')
    for item,co in zip(receiver.data,canvas_coords):item.uv=co
    mesh.uv_layers.active_index=0
    preview=bpy.data.objects.new('WGame 贴花预览（临时）',mesh);preview[TAG]=True
    preview.hide_render=True;preview.hide_select=True
    context.scene.collection.objects.link(preview)
    mat=bpy.data.materials.new('WGame 贴花预览');mat[TAG]=True;mat.use_nodes=True
    mat.surface_render_method='DITHERED'
    tree=mat.node_tree;tree.nodes.clear()
    tex=tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.extension='CLIP';tex.interpolation='Linear'
    source_uv=tree.nodes.new('ShaderNodeUVMap');source_uv.uv_map='DecalUV'
    tree.links.new(source_uv.outputs['UV'],tex.inputs['Vector'])
    # The preview must clip to the receiver's canvas just like composite().
    receiver_uv=tree.nodes.new('ShaderNodeUVMap');receiver_uv.uv_map='ReceiverUV'
    split=tree.nodes.new('ShaderNodeSeparateXYZ');tree.links.new(receiver_uv.outputs['UV'],split.inputs[0])
    coverage=tex.outputs['Alpha']
    for axis in ('X','Y'):
        for operation,bound in (('GREATER_THAN',0.),('LESS_THAN',1.)):
            check=tree.nodes.new('ShaderNodeMath');check.operation=operation;check.inputs[1].default_value=bound
            tree.links.new(split.outputs[axis],check.inputs[0])
            product=tree.nodes.new('ShaderNodeMath');product.operation='MULTIPLY'
            tree.links.new(coverage,product.inputs[0]);tree.links.new(check.outputs[0],product.inputs[1])
            coverage=product.outputs[0]
    alpha=tree.nodes.new('ShaderNodeMath');alpha.name='Opacity';alpha.operation='MULTIPLY'
    emission=tree.nodes.new('ShaderNodeEmission');transparent=tree.nodes.new('ShaderNodeBsdfTransparent')
    mix=tree.nodes.new('ShaderNodeMixShader');output=tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(coverage,alpha.inputs[0]);tree.links.new(alpha.outputs[0],mix.inputs[0])
    tree.links.new(tex.outputs['Color'],emission.inputs[0]);tree.links.new(transparent.outputs[0],mix.inputs[1])
    tree.links.new(emission.outputs[0],mix.inputs[2]);tree.links.new(mix.outputs[0],output.inputs['Surface'])
    mesh.materials.append(mat)
    _session={'object':obj,'scene':context.scene,'preview':preview,'target':target,'source':image,
        'source_size':tuple(image.size),'target_size':tuple(target.size),
        'points':points,'center':(points.min(axis=0)+points.max(axis=0))/2,'span':span,
        'aspect':image.size[0]/image.size[1],'tri_uv':np.asarray(tri_uv),'tri_points':np.asarray(tri_points),
        'signature':signature(obj)}
    settings.decal_x=0;settings.decal_y=0;settings.decal_scale=1;settings.decal_angle=0
    refresh_preview()
    editor.spaces.active.shading.type='MATERIAL'


def signature(obj):
    import hashlib
    if obj.mode not in {'EDIT', 'TEXTURE_PAINT'}:
        return None
    with mesh_snapshot(obj) as bm:
        uv=bm.loops.layers.uv.get('DetailUV')
        if not uv:
            return None
        return hashlib.sha256(repr((tuple(obj.matrix_world),[(f.index,f.select,[(tuple(l.vert.co),tuple(l[uv].uv)) for l in f.loops]) for f in bm.faces])).encode()).hexdigest()


def composite(target, source, triangles, source_triangles, opacity):
    """Rasterize onto target UVs; each target pixel is blended at most once."""
    height,width=target.shape[:2];sh,sw=source.shape[:2]
    output=target.copy();coverage=np.zeros((height,width),dtype=bool)
    for uv, suv in zip(triangles,source_triangles):
        triangle=uv*np.array([width,height])
        low=np.floor(np.clip(triangle.min(axis=0),0,[width,height])).astype(int)
        high=np.ceil(np.clip(triangle.max(axis=0),0,[width,height])).astype(int)
        if np.any(high<=low):continue
        x,y=np.meshgrid(np.arange(low[0],high[0])+.5,np.arange(low[1],high[1])+.5)
        p=np.stack([x,y],axis=-1)
        a,b,c=triangle;den=np.cross(b-a,c-a)
        if abs(den)<1e-10:continue
        u=np.cross(p-a,c-a)/den;v=np.cross(b-a,p-a)/den;w=1-u-v
        sample=w[...,None]*suv[0]+u[...,None]*suv[1]+v[...,None]*suv[2]
        region=(slice(low[1],high[1]),slice(low[0],high[0]))
        mask=(u>=-1e-7)&(v>=-1e-7)&(w>=-1e-7)&(sample[:,:,0]>=0)&(sample[:,:,0]<=1)&(sample[:,:,1]>=0)&(sample[:,:,1]<=1)&~coverage[region]
        if not mask.any():continue
        px=np.clip(sample[:,:,0]*sw-.5,0,sw-1);py=np.clip(sample[:,:,1]*sh-.5,0,sh-1)
        ix=np.floor(px).astype(int);iy=np.floor(py).astype(int);fx=(px-ix)[...,None];fy=(py-iy)[...,None]
        sampled=((source[iy,ix]*(1-fx)+source[iy,np.minimum(ix+1,sw-1)]*fx)*(1-fy)+
                 (source[np.minimum(iy+1,sh-1),ix]*(1-fx)+source[np.minimum(iy+1,sh-1),np.minimum(ix+1,sw-1)]*fx)*fy)
        dst=output[region];sa=sampled[:,:,3:4]*opacity;da=dst[:,:,3:4];oa=sa+da*(1-sa)
        rgb=np.divide(sampled[:,:,:3]*sa+dst[:,:,:3]*da*(1-sa),oa,out=np.zeros_like(sampled[:,:,:3]),where=oa>1e-8)
        combined=np.concatenate([rgb,oa],axis=2)
        dst[mask]=combined[mask];coverage[region]|=mask
    return output,int(coverage.sum())


def invoke_image_selector(operator, context):
    # A bare ImportHelper may seed the filename from the open .blend. Keep the
    # browsing directory, but require a fresh image selection on each invocation.
    filepath = operator.filepath
    if not filepath:
        source = context.scene.wgame_uv_tools.decal_source
        filepath = source.filepath if source and source.filepath else bpy.data.filepath
    path = Path(bpy.path.abspath(filepath or '//'))
    directory = path if path.is_dir() else path.parent
    operator.filepath = str(directory) + os.sep
    context.window_manager.fileselect_add(operator)
    return {'RUNNING_MODAL'}


def load_source_image(filepath):
    if not filepath:
        raise ValueError('请先选择一张 PNG、TGA 或 TIFF 素材图片')
    path = Path(bpy.path.abspath(filepath)).resolve()
    if path.is_dir() or path.suffix.lower() not in {'.png', '.tga', '.tif', '.tiff'}:
        raise ValueError('请选择 PNG、TGA 或 TIFF 素材图片；当前选择：' + path.name)
    if not path.is_file():
        raise ValueError('素材图片不存在，请重新选择：' + str(path))
    matches=[i for i in bpy.data.images if i.source in {'FILE','GENERATED'} and i.filepath
             and Path(bpy.path.abspath(i.filepath,library=i.library)).resolve()==path]
    if len(matches)>1:
        raise ValueError('同一路径有多份已加载素材，请先确认使用哪份内存图像')
    owned=not matches
    image=matches[0] if matches else bpy.data.images.load(str(path),check_existing=False)
    try:
        pixels(image)
        if max(image.size)>4096:
            raise ValueError('素材超过 4096 像素，请缩小后重新选择')
        return image
    except Exception:
        if owned:bpy.data.images.remove(image)
        raise


class WGAMEUV_OT_decal_image(bpy.types.Operator,ImportHelper):
    bl_idname='wgame_uv.decal_image'
    bl_label='选择贴花素材'
    filter_glob:bpy.props.StringProperty(default='*.png;*.tga;*.tif;*.tiff',options={'HIDDEN'})
    def invoke(self,context,event):
        return invoke_image_selector(self,context)
    def execute(self,context):
        try:
            image=load_source_image(self.filepath)
            cancel();context.scene.wgame_uv_tools.decal_source=image
            context.scene.wgame_uv_tools.decal_message='已加载素材，下一步点击“开始预览”'
            self.report({'INFO'},f'已加载 {image.name} · {image.size[0]} × {image.size[1]}，请开始预览')
            for window in context.window_manager.windows:
                for area in window.screen.areas:area.tag_redraw()
        except (RuntimeError,ValueError) as exc:
            context.scene.wgame_uv_tools.decal_message='加载失败：'+str(exc)
            self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


class WGAMEUV_OT_decal(bpy.types.Operator):
    bl_idname='wgame_uv.decal'
    bl_label='素材贴花'
    action:bpy.props.EnumProperty(items=[('START','开始预览',''),('APPLY','确认绘入',''),('CANCEL','取消预览',''),('UNDO','撤销上次贴花',''),('SAVE','保存细节图','')])
    def execute(self,context):
        global _undo
        try:
            from . import canvas_editor
            if canvas_editor._session:
                raise ValueError('请先确认或取消二维浮动选区')
            if self.action=='START':start(context)
            elif self.action=='CANCEL':cancel()
            elif self.action=='APPLY':
                if not _session:raise ValueError('请先开始预览')
                session=_session;settings=session['scene'].wgame_uv_tools
                if signature(session['object'])!=session['signature']:raise ValueError('选区、模型或 UV 已变化，请重新开始预览')
                with mesh_snapshot(session['object']) as current_bm:
                    if target_for_faces(session['object'],[f for f in current_bm.faces if f.select and not f.hide]) != session['target']:
                        raise ValueError('细节材质的目标图片已变化，请重新开始预览')
                if tuple(session['target'].size)!=session['target_size'] or tuple(session['source'].size)!=session['source_size']:
                    raise ValueError('图片尺寸已变化，请重新预览')
                target=pixels(session['target']);source=pixels(session['source'])
                source_triangles=projected_uv(session['tri_points'].reshape(-1,2),settings,session).reshape(-1,3,2)
                result,count=composite(target,source,session['tri_uv'],source_triangles,settings.decal_opacity)
                if not count:raise ValueError('贴花没有覆盖到目标像素，请调整位置或大小')
                _undo=(session['target'],target)
                write_pixels(session['target'],result)
                context.scene.wgame_uv_tools.decal_target=session['target']
                cancel();self.report({'INFO'},'已绘入细节图；尚未保存，可撤销上次贴花')
            elif self.action=='UNDO':
                if not _undo:raise ValueError('当前会话没有可撤销的贴花')
                image,values=_undo
                if tuple(image.size)!=(values.shape[1],values.shape[0]):raise ValueError('画布尺寸改变，无法还原')
                write_pixels(image,values);_undo=None
                self.report({'INFO'},'已恢复上次贴花前整张画布；尚未保存')
            else:
                image=context.scene.wgame_uv_tools.decal_target
                if not image or not image.filepath:raise ValueError('细节图没有文件路径，请先在图像编辑器另存为 RGBA PNG')
                path=Path(bpy.path.abspath(image.filepath))
                if path.suffix.lower()!='.png':raise ValueError('本版保存入口只写 PNG，请先另存为 PNG')
                image.file_format='PNG'
                if path.exists():
                    import shutil
                    backup=path.with_name(path.stem+'.before-decal-'+datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'.png')
                    shutil.copy2(path,backup)
                image.save();self.report({'INFO'},'细节图已保存，覆盖前已保留磁盘备份')
        except (ValueError,RuntimeError,ReferenceError,OSError) as exc:
            self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


def draw(layout,context):
    from . import decal_drop
    decal_drop.draw_hint(layout,context)
    s=context.scene.wgame_uv_tools;box=layout.box();box.label(text='细节绘制 · Texture Paint')
    box.operator('wgame_uv.enter_paint',text='进入细节绘制',icon='TPAINT_HLT')
    if context.mode == 'PAINT_TEXTURE' and context.active_object:
        box.prop(context.active_object.data,'use_paint_mask',text='只绘制选中面')
        canvas=context.scene.tool_settings.image_paint.canvas
        box.label(text='当前画布：'+(canvas.name if canvas else '未设置'))
    box.label(text='素材贴花 · 面遮罩限定接收区域')
    box.operator('wgame_uv.decal_image',text='更换贴花素材' if s.decal_source else '选择贴花素材',icon='FILE_IMAGE')
    if s.decal_source:
        image=s.decal_source
        info=box.box()
        info.label(text='已加载：'+image.name,icon='CHECKMARK')
        info.label(text=f'{image.size[0]} × {image.size[1]} · RGBA')
        preview=image.preview_ensure()
        if preview and preview.icon_id:
            info.template_icon(icon_value=preview.icon_id,scale=6)
        info.label(text='调整视角并选中接收面，然后开始预览')
    else:
        box.label(text='先选择一张透明背景的素材图片',icon='INFO')
    if s.decal_message.startswith('加载失败'):
        box.label(text=s.decal_message,icon='ERROR')
    row=box.row();row.enabled=s.decal_source is not None
    row.operator('wgame_uv.decal',text='重新开始预览' if _session else '开始预览',icon='HIDE_OFF').action='START'
    if _session:
        box.label(text='画布：'+_session['target'].name)
        for name in ('decal_x','decal_y','decal_scale','decal_angle','decal_opacity'):box.prop(s,name)
        row=box.row(align=True);row.operator('wgame_uv.decal',text='确认绘入').action='APPLY';row.operator('wgame_uv.decal',text='取消预览').action='CANCEL'
    box.operator('wgame_uv.decal',text='撤销上次贴花').action='UNDO'
    box.operator('wgame_uv.save_detail_image',text='保存细节 PNG')
    box.label(text='只向选中面投射；共享 UV 会共用图案')
    box.label(text='撤销恢复整张画布，会覆盖其后的绘画')
