# SPDX-License-Identifier: GPL-3.0-or-later
"""One character detail PNG; create/save or reuse without losing existing pixels."""
from pathlib import Path
from datetime import datetime
from contextlib import contextmanager
import hashlib
import json
import os
import shutil
import tempfile
import bpy
import numpy as np
from bpy_extras.io_utils import ExportHelper
from . import detail_binding, decals
from .detail_lifecycle import assert_mesh

FIXED_OWNER='wgame_detail_fixed_path_v1'
SAVED_FILE='wgame_detail_saved_file_sha256'
SAVED_PIXELS='wgame_detail_saved_pixels_sha256'


def default_path():
    if bpy.data.filepath:
        source = Path(bpy.data.filepath).resolve()
        for parent in source.parents:
            if parent.parent.name == 'Characters' and parent.parent.parent.name == 'ArtSource' and len(parent.name)==6 and parent.name.isdecimal():
                return parent/'FaceDetail/face_detail_rgba.png'
        return source.parent/'FaceDetail/face_detail_rgba.png'
    return Path(bpy.app.tempdir)/'face_detail_rgba.png'


def fixed_path(source=None):
    """Resolve the registered Work authority, never a display name or temporary file."""
    value=str(source if source is not None else bpy.data.filepath)
    if not value:
        raise ValueError('请先打开已登记角色的正式 Work Blend；未创建细节图')
    origin=Path(value).resolve()
    root=next((p for p in origin.parents if p.parent.name=='Characters' and p.parent.parent.name=='ArtSource'
               and len(p.name)==6 and p.name.isdecimal() and p.name!='000000'),None)
    if root is None or origin.suffix.lower()!='.blend' or not origin.is_file():
        raise ValueError('请打开 ArtSource/Characters/<六位编号> 中已登记的正式 Work Blend')
    manifest=root/'authoring_manifest.json'
    if not manifest.is_file():
        raise ValueError('角色缺少 authoring_manifest.json，无法确认固定细节图归属')
    doc=json.loads(manifest.read_text(encoding='utf-8-sig'))
    if doc.get('characterId')!=root.name or doc.get('packageId')!=root.name:
        raise ValueError('角色制作清单身份与目录不一致；未写入细节图')
    authority=(doc.get('authority') or {}).get('blenderPath')
    if not authority:
        work=[i.get('path') for i in doc.get('items',[]) if i.get('kind')=='BlenderWork' and i.get('role')=='active-work']
        if len(work)!=1 or not work[0]:
            raise ValueError('角色制作清单没有唯一的 active-work Blend；未写入细节图')
        authority=work[0]
    declared=(root/authority).resolve()
    if Path(authority).is_absolute() or not declared.is_relative_to(root) or declared!=origin:
        raise ValueError('当前文件不是角色清单中的正式 Work Blend；未写入细节图')
    path=(root/'FaceDetail/face_detail_rgba.png').resolve()
    if not path.is_relative_to(root):
        raise ValueError('固定细节图路径越出角色目录；未写入')
    return path


def _file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def _pixel_hash(image):
    return hashlib.sha256(decals.pixels(image).tobytes()).hexdigest()


def fixed_status(context, require_canvas=False):
    from . import canvas_editor
    if canvas_editor._session or decals._session:
        raise ValueError('请先确认或取消浮动选区／贴花预览，再保存细节图')
    path=fixed_path()
    if path.exists() and not path.is_file():
        raise ValueError('固定 PNG 路径被目录占用；未写入')
    image=context.scene.wgame_uv_tools.decal_target
    matches=[i for i in bpy.data.images if i.source in {'FILE','GENERATED'} and i.filepath
             and Path(bpy.path.abspath(i.filepath,library=i.library)).resolve()==path]
    if len(matches)>1 and image is None:
        raise ValueError('固定 PNG 有多份已加载图像，请先确认使用哪份内存像素；未写入')
    if image is None and matches:
        image=matches[0]
    if require_canvas and image is None:
        raise ValueError('请先点“创建并保存细节图”')
    expected=_file_hash(path)
    conflict=False;write=expected is None
    if image:
        detail_binding.validate_canvas(image)
        if not image.is_editable:
            raise ValueError('当前细节图不可编辑；未写入')
        owner=image.get(FIXED_OWNER)
        if owner and Path(owner).resolve()!=path:
            raise ValueError('当前画布属于另一角色，请先绑定当前角色的细节图；未写入')
        current=Path(bpy.path.abspath(image.filepath)).resolve() if image.filepath else None
        if expected is not None:
            baseline=image.get(SAVED_FILE)
            if baseline:
                conflict=baseline!=expected or current!=path
                write=conflict or image.get(SAVED_PIXELS)!=_pixel_hash(image)
            else:
                conflict=current!=path or image.is_dirty
                write=conflict
    return {'path':path,'image':image,'expected':expected,'write':write,'conflict':conflict}


def _stage_png(image,path):
    """Export exact current pixels through a disposable image, preserving dirty state."""
    temporary=None;copy=None
    try:
        fd,temporary=tempfile.mkstemp(prefix='.face-detail-',suffix='.tmp',dir=str(path.parent));os.close(fd)
        width,height=image.size
        copy=bpy.data.images.new('__WGameDetailPngWriter',width=width,height=height,alpha=True,float_buffer=image.is_float)
        copy.alpha_mode='STRAIGHT';copy.colorspace_settings.name=image.colorspace_settings.name
        decals.write_pixels(copy,decals.pixels(image))
        copy.file_format='PNG';copy.filepath_raw=temporary;copy.save()
        return Path(temporary)
    except Exception:
        if temporary:Path(temporary).unlink(missing_ok=True)
        raise
    finally:
        if copy is not None:bpy.data.images.remove(copy)


@contextmanager
def fixed_write(context,image,plan):
    """Commit file and image metadata inside the binding transaction; roll back failures."""
    path=plan['path'];old_file=path.read_bytes() if path.is_file() else None
    old_path=image.filepath_raw;old_format=image.file_format;old_fake=image.use_fake_user
    keys=(FIXED_OWNER,SAVED_FILE,SAVED_PIXELS,detail_binding.CANVAS_TAG)
    old_props={k:image.get(k) for k in keys}
    old_scene_path=getattr(context.scene,'wgame_face_detail_texture_path','')
    written=False;temporary=None;backup=None;created=[]

    def commit():
        nonlocal written,temporary,backup
        if _file_hash(path)!=plan['expected']:
            raise ValueError('固定 PNG 在确认后发生变化；未覆盖，请重新点击检查')
        if plan['write']:
            if not path.parent.exists():path.parent.mkdir();created.append(path.parent)
            temporary=_stage_png(image,path)
            if _file_hash(path)!=plan['expected']:
                raise ValueError('固定 PNG 在写入前发生变化；未覆盖')
            if plan['conflict']:
                folder=path.parent/'.backups'
                if not folder.exists():folder.mkdir();created.append(folder)
                backup=folder/(path.stem+'-'+datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'.png')
                with backup.open('xb') as stream:stream.write(old_file)
            if plan['expected'] is None:
                # Exclusive creation protects a file created concurrently after preflight.
                with path.open('xb') as stream:
                    written=True;stream.write(temporary.read_bytes())
            else:
                os.replace(temporary,path);written=True
        image.filepath_raw=str(path);image.file_format='PNG';image.use_fake_user=True
        image[detail_binding.CANVAS_TAG]=True;image[FIXED_OWNER]=str(path)
        image[SAVED_FILE]=_file_hash(path);image[SAVED_PIXELS]=_pixel_hash(image)
        if hasattr(context.scene,'wgame_face_detail_texture_path'):
            context.scene.wgame_face_detail_texture_path=str(path)

    try:
        yield commit
    except Exception:
        if written:
            if old_file is None:path.unlink(missing_ok=True)
            else:
                fd,rollback=tempfile.mkstemp(prefix='.face-detail-rollback-',suffix='.tmp',dir=str(path.parent));os.close(fd)
                try:Path(rollback).write_bytes(old_file);os.replace(rollback,path)
                finally:Path(rollback).unlink(missing_ok=True)
        image.filepath_raw=old_path;image.file_format=old_format;image.use_fake_user=old_fake
        for key,value in old_props.items():
            if value is None:
                if key in image:del image[key]
            else:image[key]=value
        if hasattr(context.scene,'wgame_face_detail_texture_path'):
            context.scene.wgame_face_detail_texture_path=old_scene_path
        if backup:backup.unlink(missing_ok=True)
        raise
    finally:
        if temporary:temporary.unlink(missing_ok=True)
        for folder in reversed(created):
            try:folder.rmdir()
            except OSError:pass


def ensure_fixed(context,allow_replace=False,overwrite=False):
    obj=assert_mesh(context);plan=fixed_status(context)
    if detail_binding.replacement_needed(obj) and not allow_replace:
        raise ValueError('已有正式 DetailUV，请确认保留旧版；未创建或写入 PNG')
    if plan['conflict'] and not overwrite:
        raise ValueError('当前内存细节图与固定 PNG 冲突；请点击按钮并明确勾选“备份后用当前内存图更新固定 PNG”；未更改两份像素')
    image=plan['image'];owned=False;new=False
    try:
        if image is None:
            if plan['expected'] is not None:image,owned=detail_binding.load_png(plan['path'])
            else:
                image=bpy.data.images.new('FaceDetail',width=2048,height=2048,alpha=True);owned=True;new=True
                image.generated_color=(0,0,0,0);image.alpha_mode='STRAIGHT';image.colorspace_settings.name='sRGB'
                decals.write_pixels(image,np.zeros((2048,2048,4),dtype=np.float32))
        with fixed_write(context,image,plan) as commit:
            detail_binding.bind(context,obj,allow_replace=allow_replace,image_override=image,allow_blank=True,commit_hook=commit)
    except Exception:
        if owned and image is not None:bpy.data.images.remove(image)
        raise
    for area in context.screen.areas if context.screen else []:
        if area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL'
        area.tag_redraw()
    verb='已创建并保存 2048×2048 透明细节图' if new else '已保存当前细节图' if plan['write'] else '已复用固定细节图（保留原像素与尺寸）'
    message=verb+'：'+str(plan['path'])+'；UV、材质和图片已绑定'
    if plan['conflict']:message+='；原 PNG 已备份到 FaceDetail/.backups'
    context.scene.wgame_uv_tools.canvas_message=message
    return image,message


def save_fixed(context,overwrite=False):
    plan=fixed_status(context,require_canvas=True)
    if plan['conflict'] and not overwrite:
        raise ValueError('当前内存细节图与固定 PNG 冲突；请明确确认备份后更新，未覆盖')
    with fixed_write(context,plan['image'],plan) as commit:commit()
    message='细节图已保存到固定 PNG：'+str(plan['path'])
    if plan['conflict']:message+='；原 PNG 已备份到 FaceDetail/.backups'
    context.scene.wgame_uv_tools.canvas_message=message
    return message


def create(context, name='FaceDetail', allow_replace=False):
    obj=assert_mesh(context)
    if detail_binding.replacement_needed(obj) and not allow_replace:
        raise ValueError('已有正式 DetailUV，请点击按钮并确认保留旧版；未创建图像')
    image=None
    old_image=context.scene.wgame_uv_tools.decal_target
    try:
        resolution=2048
        image=bpy.data.images.new(name or 'FaceDetail',width=resolution,height=resolution,alpha=True)
        image.generated_color=(0,0,0,0)
        image.alpha_mode='STRAIGHT';image.colorspace_settings.name='sRGB'
        image[detail_binding.CANVAS_TAG]=True
        image.use_fake_user=True
        decals.write_pixels(image,np.zeros((resolution,resolution,4),dtype=np.float32))
        detail_binding.bind(context,obj,allow_replace=allow_replace,image_override=image,allow_blank=True)
    except Exception:
        if image and bpy.data.images.get(image.name)==image:
            bpy.data.images.remove(image)
        raise
    if old_image and old_image.is_editable:
        old_image.use_fake_user=True  # An explicit new canvas retains the previous image after saving the Work file.
    for area in context.screen.areas if context.screen else []:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
        area.tag_redraw()
    message=f'已新建 {image.name}：{resolution}×{resolution} 全透明 RGBA；UV、材质和图片已绑定；尚未保存 PNG，可继续绘制或点“保存细节 PNG”'
    context.scene.wgame_uv_tools.canvas_message=message
    return image,message


def save(context, filepath, overwrite=False):
    from . import canvas_editor
    if canvas_editor._session:
        raise ValueError('请先确认或取消二维浮动选区，再保存细节图')
    image=context.scene.wgame_uv_tools.decal_target
    if not image:
        raise ValueError('没有细节画布，请先新建或绑定细节图')
    detail_binding.validate_canvas(image)
    decals.pixels(image)
    path=Path(bpy.path.abspath(filepath)).resolve()
    if path.suffix.lower()!='.png':
        raise ValueError('请选择 PNG 文件名')
    if path.exists() and not overwrite:
        raise ValueError('该 PNG 已存在，未覆盖；请另选文件名，或明确勾选“备份后覆盖已有 PNG”')
    path.parent.mkdir(parents=True,exist_ok=True)
    previous=image.filepath_raw;previous_format=image.file_format
    reserved=False;backup=None
    if path.exists():
        backup=path.with_name(path.stem+'.before-detail-'+datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'.png')
        shutil.copy2(path,backup)
    else:
        with path.open('xb'):
            pass
        reserved=True
    try:
        image.filepath_raw=str(path);image.file_format='PNG'
        image.save()
    except Exception:
        image.filepath_raw=previous;image.file_format=previous_format
        if reserved and path.is_file():
            path.unlink()
        raise
    if hasattr(context.scene,'wgame_face_detail_texture_path'):
        context.scene.wgame_face_detail_texture_path=str(path)
    message='细节 PNG 已保存：'+str(path)
    if backup:
        message+='；覆盖前备份：'+backup.name
    context.scene.wgame_uv_tools.canvas_message=message
    return message


class WGAMEUV_OT_new_detail_image(bpy.types.Operator):
    bl_idname='wgame_uv.new_detail_image'
    bl_label='新建透明细节贴图'
    bl_description='新建 2048×2048 全透明 RGBA，绑定当前工作 UV；保留旧图，不改变最终烘焙尺寸'
    bl_options={'REGISTER','UNDO'}
    image_name:bpy.props.StringProperty(name='画布名称',default='FaceDetail')

    def invoke(self,context,event):
        try:obj=assert_mesh(context)
        except ValueError as error:
            self.report({'ERROR'},str(error));return {'CANCELLED'}
        if detail_binding.replacement_needed(obj):
            self._confirmed=True
            return context.window_manager.invoke_props_dialog(self,width=480)
        return self.execute(context)

    def draw(self,context):
        self.layout.prop(self,'image_name')
        self.layout.label(text='新建 2048×2048 全透明 RGBA；保留旧图像')
        self.layout.label(text='制作尺寸与最终烘焙尺寸独立')
        self.layout.label(text='确认后保留旧 DetailUV，并绑定当前工作 UV')
        self.layout.label(text='已有 PNG 不会覆盖，取消不会创建画布')

    def execute(self,context):
        try:
            _,message=create(context,self.image_name,allow_replace=getattr(self,'_confirmed',False))
            self.report({'INFO'},message);return {'FINISHED'}
        except Exception as error:
            context.scene.wgame_uv_tools.canvas_message=str(error)
            self.report({'ERROR'},str(error));return {'CANCELLED'}


class WGAMEUV_OT_save_detail_image(bpy.types.Operator,ExportHelper):
    bl_idname='wgame_uv.save_detail_image'
    bl_label='保存细节 PNG'
    bl_description='另存当前细节画布，默认角色 FaceDetail 目录；已有 PNG 需明确选择备份后覆盖'
    filename_ext='.png'
    filter_glob:bpy.props.StringProperty(default='*.png',options={'HIDDEN'})
    check_existing:bpy.props.BoolProperty(default=False,options={'HIDDEN'})
    overwrite_existing:bpy.props.BoolProperty(name='备份后覆盖已有 PNG',default=False)

    def invoke(self,context,event):
        image=context.scene.wgame_uv_tools.decal_target
        if not image:
            self.report({'ERROR'},'请先新建或绑定细节画布');return {'CANCELLED'}
        self.filepath=str(Path(bpy.path.abspath(image.filepath)) if image.filepath else default_path())
        self.overwrite_existing=False
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

    def execute(self,context):
        try:
            message=save(context,self.filepath,self.overwrite_existing)
            self.report({'INFO'},message);return {'FINISHED'}
        except Exception as error:
            context.scene.wgame_uv_tools.canvas_message=str(error)
            self.report({'ERROR'},str(error));return {'CANCELLED'}


class WGAMEUV_OT_new_detail_canvas(bpy.types.Operator):
    """New entry keeps old registered operator instances valid during a live update."""
    bl_idname='wgame_uv.new_detail_canvas'
    bl_label='新建透明细节贴图（2048×2048）'
    bl_description=WGAMEUV_OT_new_detail_image.bl_description
    bl_options={'REGISTER','UNDO'}
    image_name:bpy.props.StringProperty(name='画布名称',default='FaceDetail')
    invoke=WGAMEUV_OT_new_detail_image.invoke
    draw=WGAMEUV_OT_new_detail_image.draw
    execute=WGAMEUV_OT_new_detail_image.execute


def invoke_fixed(operator,context,require_canvas=False):
    try:
        obj=None if require_canvas else assert_mesh(context)
        plan=fixed_status(context,require_canvas=require_canvas)
        operator._confirmed=False
        operator.overwrite_existing=False
        operator._replace=bool(obj and detail_binding.replacement_needed(obj))
        operator._conflict=plan['conflict']
        if operator._replace or operator._conflict:
            operator._confirmed=True
            return context.window_manager.invoke_props_dialog(operator,width=640)
        return operator.execute(context)
    except Exception as error:
        context.scene.wgame_uv_tools.canvas_message=str(error)
        operator.report({'ERROR'},str(error));return {'CANCELLED'}


def draw_fixed(operator,context):
    layout=operator.layout
    layout.label(text='固定文件：FaceDetail/face_detail_rgba.png')
    if getattr(operator,'_replace',False):
        layout.label(text='确认后保留旧 DetailUV，并绑定当前工作 UV')
    if getattr(operator,'_conflict',False):
        layout.label(text='当前内存图与固定 PNG 冲突；取消保留两份内容',icon='ERROR')
        layout.prop(operator,'overwrite_existing')
        layout.label(text='勾选后保存当前实际像素，旧 PNG 备份至 FaceDetail/.backups')
    layout.label(text='新图固定 2048×2048；已有图不缩放；不保存 Blend')


class WGAMEUV_OT_ensure_detail_image(bpy.types.Operator):
    bl_idname='wgame_uv.ensure_detail_image'
    bl_label='创建并保存细节图'
    bl_description='一键创建 2048 透明 RGBA 并保存固定角色 PNG；已有图复用，冲突需明确确认'
    bl_options={'REGISTER','UNDO'}
    overwrite_existing:bpy.props.BoolProperty(name='备份后用当前内存图更新固定 PNG',default=False)

    def invoke(self,context,event):return invoke_fixed(self,context)
    def draw(self,context):draw_fixed(self,context)
    def execute(self,context):
        try:
            _,message=ensure_fixed(context,allow_replace=getattr(self,'_confirmed',False),overwrite=self.overwrite_existing)
            self.report({'INFO'},message);return {'FINISHED'}
        except Exception as error:
            context.scene.wgame_uv_tools.canvas_message=str(error)
            self.report({'ERROR'},str(error));return {'CANCELLED'}


class WGAMEUV_OT_save_fixed_detail_image(bpy.types.Operator):
    bl_idname='wgame_uv.save_fixed_detail_image'
    bl_label='保存细节图'
    bl_description='将当前实际像素更新到同一角色 FaceDetail/face_detail_rgba.png；不另选路径'
    overwrite_existing:bpy.props.BoolProperty(name='备份后用当前内存图更新固定 PNG',default=False)

    def invoke(self,context,event):return invoke_fixed(self,context,require_canvas=True)
    def draw(self,context):draw_fixed(self,context)
    def execute(self,context):
        try:
            message=save_fixed(context,overwrite=self.overwrite_existing)
            self.report({'INFO'},message);return {'FINISHED'}
        except Exception as error:
            context.scene.wgame_uv_tools.canvas_message=str(error)
            self.report({'ERROR'},str(error));return {'CANCELLED'}


CLASSES=(WGAMEUV_OT_new_detail_image,WGAMEUV_OT_save_detail_image,WGAMEUV_OT_new_detail_canvas,
         WGAMEUV_OT_ensure_detail_image,WGAMEUV_OT_save_fixed_detail_image)
