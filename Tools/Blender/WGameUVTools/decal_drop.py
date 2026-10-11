# SPDX-License-Identifier: GPL-3.0-or-later
"""Local FileHandler for the WGame decal sidebar; never open over the canvas."""
from pathlib import Path
import bpy
from . import decals,canvas_editor


def drop_target(context):
    area=getattr(context,'area',None);region=getattr(context,'region',None)
    settings=getattr(context.scene,'wgame_uv_tools',None)
    return bool(area and area.type=='IMAGE_EDITOR' and region and region.type=='UI'
                and getattr(region,'active_panel_category',None)=='WGame UV'
                and settings and settings.ui_tab_uv=='DECAL'
                and context.space_data.mode in {'UV','PAINT','VIEW'})


def dropped_path(operator):
    files=list(operator.files)
    if len(files)>1:
        raise ValueError('每次只拖入一张素材；本次多文件未导入，请分别拖入')
    if files:
        if not operator.directory:
            raise ValueError('拖入文件缺少目录，未导入')
        path=Path(operator.directory)/files[0].name
    else:
        if not operator.filepath:
            raise ValueError('没有收到文件；请从资源管理器拖入 PNG、TGA 或 TIFF')
        path=Path(bpy.path.abspath(operator.filepath))
    return path.resolve()


def import_source(context,path):
    if canvas_editor._session or decals._session:
        raise ValueError('请先确认或取消当前浮动选区／贴花预览，再拖入素材；当前操作已保留')
    settings=context.scene.wgame_uv_tools
    target=settings.decal_target
    if target and target.filepath and Path(bpy.path.abspath(target.filepath,library=target.library)).resolve()==path:
        raise ValueError('这是当前细节底图，请拖入独立贴花素材；底图未更改')
    before={i.as_pointer() for i in bpy.data.images}
    image=decals.load_source_image(str(path))
    if image==target or image==context.space_data.image:
        raise ValueError('素材与当前画布相同，请使用独立素材；画布未更改')
    reused=image.as_pointer() in before
    settings.decal_source=image
    next_step='摆放已加载素材' if settings.decal_workflow=='CANVAS' else '开始预览'
    message=f'拖入成功：{"复用" if reused else "已加载"} {image.name} · {image.size[0]}×{image.size[1]}；下一步点“{next_step}”'
    if reused and image.is_dirty:message+='；保留素材的内存修改'
    settings.decal_message=message
    for area in context.screen.areas:area.tag_redraw()
    return message


class WGAMEUV_OT_drop_decal_image(bpy.types.Operator):
    bl_idname='wgame_uv.drop_decal_image'
    bl_label='拖入贴花素材'
    bl_description='只加入贴花素材，保留当前画布；每次一张 PNG/TGA/TIFF，然后摆放或开始预览'
    bl_options={'REGISTER','UNDO'}
    filepath:bpy.props.StringProperty(subtype='FILE_PATH',options={'SKIP_SAVE','HIDDEN'})
    directory:bpy.props.StringProperty(subtype='DIR_PATH',options={'SKIP_SAVE','HIDDEN'})
    files:bpy.props.CollectionProperty(type=bpy.types.OperatorFileListElement,options={'SKIP_SAVE','HIDDEN'})

    @classmethod
    def poll(cls,context):return drop_target(context)

    def invoke(self,context,event):return self.execute(context)

    def execute(self,context):
        try:
            message=import_source(context,dropped_path(self))
            self.report({'INFO'},message);return {'FINISHED'}
        except (ValueError,RuntimeError,OSError,ReferenceError) as error:
            message=str(error);context.scene.wgame_uv_tools.decal_message='拖入失败：'+message
            self.report({'ERROR'},message);return {'CANCELLED'}


class WGAMEUV_FH_decal_source(bpy.types.FileHandler):
    bl_idname='WGAMEUV_FH_decal_source'
    bl_label='加入 WGame 贴花素材'
    bl_import_operator='wgame_uv.drop_decal_image'
    bl_file_extensions='.png;.tga;.tif;.tiff'

    @classmethod
    def poll_drop(cls,context):return drop_target(context)


def draw_hint(layout,context):
    from . import draw_detail_feedback
    box=layout.box();box.label(text='拖入贴花素材',icon='FILE_IMAGE')
    if context.area.type=='IMAGE_EDITOR':
        box.label(text='从资源管理器拖到本侧栏')
        box.label(text='WGame UV → 贴花；保留画布')
    else:box.label(text='请拖到左侧图像编辑器的贴花侧栏')
    box.label(text='PNG / TGA / TIFF · 每次一张')
    message=context.scene.wgame_uv_tools.decal_message
    if message.startswith(('拖入成功','拖入失败')):
        draw_detail_feedback(box,context,message)


CLASSES=(WGAMEUV_OT_drop_decal_image,WGAMEUV_FH_decal_source)
