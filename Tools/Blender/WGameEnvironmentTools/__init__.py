bl_info = {
    'name': 'WGame 场景调色板与细节 UV', 'author': 'WGame', 'version': (2,2,3),
    'blender': (5,2,0), 'category': 'Material', 'location': '3D 视图 / UV 编辑器 → WGame 场景',
    'description': '独立编辑细节 UV，按共用细节图联合使用角色烘焙流程',
}
import bpy
import addon_utils
from bpy_extras import view3d_utils
from bpy.app.handlers import persistent
from bpy.props import BoolProperty, FloatProperty, FloatVectorProperty, EnumProperty, PointerProperty, StringProperty
from . import authoring, shared_detail, workflow, vertex_export, color_profile, palette_brush, registration, emission
from .presets import COLORS, LABELS


def is_source(context):
    return bool(context.scene.get('wgame_environment_work')) and not workflow.PREVIEW


def paint_color_changed(scene,context):
    if context and context.mode=='PAINT_TEXTURE':
        paint=scene.tool_settings.image_paint
        if emission.is_mask(paint.canvas):return
        if paint.brush:
            settings=paint.unified_paint_settings
            (settings if settings.use_unified_color else paint.brush).color=tuple(scene.wgame_environment_color)


def preset_changed(scene,context):
    rgb,profile=COLORS[scene.wgame_environment_preset]
    scene.wgame_environment_color_active=-1
    scene.wgame_environment_color=tuple(v/255 for v in rgb)
    scene.wgame_environment_profile=str(profile)


class EnvironmentOperator:
    @classmethod
    def poll(cls,context):return is_source(context)


class WG_ENV_OT_Register(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_register';bl_label='登记选中模型';bl_options={'REGISTER','UNDO'}
    bl_description='保留几何、材质与已有 UV，补齐制作数据并登记为可烘焙的场景组件'
    category: EnumProperty(name='新模型默认类别',items=[
        ('Arch_','建筑',''),('Ground_','地坪',''),('Sec_','安防',''),
        ('Veh_','车辆',''),('Tool_','工具',''),('Prop_','道具','')],default='Prop_')
    asset_id: StringProperty(name='组件 ID',description='仅单选可指定；已有规范名称默认作为稳定 ID')

    @classmethod
    def poll(cls,context):
        return is_source(context) and context.mode=='OBJECT' and any(o.type=='MESH' for o in context.selected_objects)

    def invoke(self,context,_event):
        objects=[o for o in context.selected_objects if o.type=='MESH']
        self.asset_id=''
        if len(objects)==1:
            obj=objects[0]
            self.category=next((p for p in registration.assembly.CATEGORIES if obj.name.startswith(p)),'Prop_')
            self.asset_id=str(obj.get('wgame_environment_asset',''))
        return context.window_manager.invoke_props_dialog(self,width=440)

    def draw(self,context):
        self.layout.prop(self,'category')
        if len([o for o in context.selected_objects if o.type=='MESH'])==1:
            self.layout.prop(self,'asset_id')
        self.layout.label(text='已有 Arch_ / Prop_ 等前缀会保留；空 ID 自动从名称生成')
        self.layout.label(text='保留现有颜色和 UV；新 DetailUV 初始化为留空')

    def execute(self,context):
        try:
            result=registration.register_models(context.scene,context.selected_objects,self.category,self.asset_id)
            self.report({'INFO'},f"已登记 {result['registered']} 个；已有 {result['unchanged']} 个保持不变")
            for area in context.screen.areas if context.screen else []:area.tag_redraw()
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_SelectUnregistered(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_select_unregistered';bl_label='选择未登记模型';bl_options={'REGISTER','UNDO'}

    @classmethod
    def poll(cls,context):return is_source(context) and context.mode=='OBJECT'

    def execute(self,context):
        objects=registration.pending(context.scene)
        bpy.ops.object.select_all(action='DESELECT')
        selected=[]
        for obj in objects:
            if obj.name not in context.view_layer.objects:continue
            obj.hide_set(False);obj.hide_viewport=False;obj.hide_select=False;obj.select_set(True);selected.append(obj)
        if selected:context.view_layer.objects.active=selected[0]
        self.report({'INFO'},f'已选择 {len(selected)} / {len(objects)} 个未登记模型；不可见集合中的模型请先显示集合')
        return {'FINISHED'}


class WG_ENV_OT_Auxiliary(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_auxiliary';bl_label='设为辅助模型';bl_options={'REGISTER','UNDO'}
    bl_description='仅对未登记网格生效；明确排除烘焙，不删除或隐藏模型'
    excluded: BoolProperty(default=True)

    def execute(self,context):
        try:
            count=registration.set_auxiliary(context.selected_objects,self.excluded)
            self.report({'INFO'},f'{count} 个模型已'+('设为辅助网格' if self.excluded else '恢复为待登记'))
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Color(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_apply_color';bl_label='给选中面上色';bl_options={'REGISTER','UNDO'}
    def execute(self,context):
        try:
            module=authoring.palette_module()
            color_profile.seed(context.scene)
            item=color_profile.active(context.scene)
            if item is None:raise ValueError('请先在场景配色 Profile 中选择颜色')
            if any(not o.get('wgame_environment_asset') for o in context.selected_objects if o.type=='MESH'):
                raise ValueError('请选择已接入的场景模型')
            targets=[]
            for obj in context.selected_objects:
                if obj.type!='MESH':continue
                if obj.mode=='EDIT':obj.update_from_editmode()
                faces=[poly.index for poly in obj.data.polygons if poly.select]
                if not faces and obj.mode!='EDIT':faces=[poly.index for poly in obj.data.polygons]
                if faces:targets.append((obj,faces,palette_brush.brush_material_slot(context.scene,obj)))
            module.apply_color_profile_to_selection(context,int(item.color_index),int(item.surface_profile),int(item.category_index))
            for obj,faces,slot in targets:
                palette_brush.assign_material(obj,faces,slot)
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Eyedropper(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_eyedropper';bl_label='吸管吸色'
    bl_description='在 3D 视图点击任意场景模型的面，选中其场景配色 Profile'

    def execute(self,context):
        return self.invoke(context,None)

    def invoke(self,context,_event):
        area=context.area if context.area and context.area.type=='VIEW_3D' else next(
            (item for item in context.screen.areas if item.type=='VIEW_3D'),None)
        if area is None:
            self.report({'ERROR'},'请先打开一个 3D 视图')
            return {'CANCELLED'}
        region=next((item for item in area.regions if item.type=='WINDOW'),None)
        if region is None:
            self.report({'ERROR'},'找不到 3D 视图的绘制区域')
            return {'CANCELLED'}
        self._area=area
        self._region=region
        self._view=area.spaces.active.region_3d
        context.window_manager.modal_handler_add(self)
        context.window.cursor_modal_set('EYEDROPPER')
        area.header_text_set('场景吸管：在 3D 视图点击模型表面取色 · Esc 退出')
        return {'RUNNING_MODAL'}

    def _finish(self,context):
        self._area.header_text_set(None)
        context.window.cursor_modal_restore()

    def modal(self,context,event):
        if event.type=='WINDOW_DEACTIVATE' or (event.type in {'ESC','RIGHTMOUSE'} and event.value=='PRESS'):
            self._finish(context)
            return {'CANCELLED'}
        if event.type=='LEFTMOUSE' and event.value=='PRESS':
            x=event.mouse_x-self._region.x
            y=event.mouse_y-self._region.y
            if not (0<=x<self._region.width and 0<=y<self._region.height):
                return {'PASS_THROUGH'}
            point=(x,y)
            origin=view3d_utils.region_2d_to_origin_3d(self._region,self._view,point)
            direction=view3d_utils.region_2d_to_vector_3d(self._region,self._view,point)
            try:
                obj,index=palette_brush.sample_hit(context.scene,context.evaluated_depsgraph_get(),origin,direction)
                found=palette_brush.select_from_face(context.scene,obj,index)
            except ValueError as error:
                self.report({'WARNING'},str(error))
                return {'RUNNING_MODAL'}
            self.report({'INFO'} if found>=0 else {'WARNING'},
                        f'已选中 {context.scene.wgame_environment_colors[found].label}' if found>=0 else
                        '已吸取颜色；请先将它加入场景配色 Profile')
            self._finish(context)
            return {'FINISHED'}
        return {'PASS_THROUGH'}


class WG_ENV_OT_EditUV(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_edit_uv';bl_label='编辑细节 UV'
    def execute(self,context):
        obj=context.object
        if obj is None or not obj.get('wgame_environment_asset'):
            self.report({'ERROR'},'请选择场景模型');return {'CANCELLED'}
        if not hasattr(context.scene,'wgame_uv_tools'):addon_utils.enable('WGameUVTools',default_set=True)
        if obj.data.users>1:obj.data=obj.data.copy()
        try:image=shared_detail.canvas(obj=obj)
        except ValueError:image=context.scene.wgame_environment_detail_image
        obj.data.uv_layers.active=obj.data.uv_layers['DetailUV']
        context.scene.wgame_uv_tools.uv_map='DetailUV'
        context.scene.wgame_uv_tools.ui_tab_uv='CREATE'
        context.scene.wgame_uv_tools.ui_tab_view='CREATE'
        workspace=bpy.data.workspaces.get('UV Editing')
        if workspace:context.window.workspace=workspace
        window=context.window
        def setup():
            for area in window.screen.areas:
                if area.type=='IMAGE_EDITOR':
                    area.ui_type='UV';area.spaces.active.image=image;area.spaces.active.show_region_ui=True
                elif area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True
            return None
        setup();bpy.app.timers.register(setup,first_interval=.2)
        context.scene.tool_settings.mesh_select_mode=(False,False,True)
        if obj.mode!='EDIT':bpy.ops.object.mode_set(mode='EDIT')
        return {'FINISHED'}


class WG_ENV_OT_UVAction(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_uv_action';bl_label='选区 UV';bl_options={'REGISTER','UNDO'}
    action:EnumProperty(items=[('CREATE','生成选区 UV',''),('CLEAR','选区取消细节 UV','')])
    def execute(self,context):
        obj=context.object
        if not obj or obj.mode!='EDIT' or not obj.data.uv_layers.get('DetailUV'):
            self.report({'ERROR'},'先进入细节 UV 编辑并选择需要的面');return {'CANCELLED'}
        obj.data.uv_layers.active_index=obj.data.uv_layers.find('DetailUV')
        context.scene.wgame_uv_tools.uv_map='DetailUV'
        return bpy.ops.wgame_uv.face_authoring(action=self.action)


class WG_ENV_OT_Bind(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_bind_detail';bl_label='让选中模型共用此图';bl_options={'REGISTER','UNDO'}
    def execute(self,context):
        active=context.view_layer.objects.active
        restore_edit=active is not None and active.mode=='EDIT'
        try:
            from .group_bake import mapped_faces
            image=context.scene.wgame_environment_detail_image
            objects=[o for o in context.selected_objects if o.type=='MESH' and o.get('wgame_environment_asset')]
            if image is None or not objects:raise ValueError('请选择模型与一张 RGBA 细节图')
            if image.channels!=4 or not image.size[0]:raise ValueError('细节图必须是有效 RGBA 图片')
            if restore_edit:bpy.ops.object.mode_set(mode='OBJECT')
            targets={}
            for obj in objects:
                mapped=mapped_faces(obj)
                if not mapped:raise ValueError(obj.name+'：先选择需要细节的面并生成选区 UV')
                existing={p.index for p in obj.data.polygons if obj.data.materials[p.material_index] and
                          obj.data.materials[p.material_index].get('wgame_environment_shared')}
                pure={index:color_profile.face_pair(obj,index) for index in mapped
                      if index not in existing}
                targets[obj]=(mapped|existing,pure)
            color_profile.seed(context.scene)
            palette=shared_detail.palette_image(objects)
            material=next((m for m in bpy.data.materials if m.get('wgame_environment_shared') and m.node_tree.nodes.get('Detail_Texture') and m.node_tree.nodes['Detail_Texture'].image==image),None)
            if material is None or material.node_tree.nodes.get('Palette_Texture') is None:
                material=shared_detail.make_material(palette,image)
            elif material.node_tree.nodes['Palette_Texture'].image is None:
                material.node_tree.nodes['Palette_Texture'].image=palette
            for obj in objects:
                if obj.data.users>1:obj.data=obj.data.copy()
                material_slot=next((index for index,value in enumerate(obj.data.materials) if value==material),None)
                if material_slot is None:
                    material_slot=len(obj.data.materials);obj.data.materials.append(material)
                faces,pure=targets[obj]
                groups={}
                for index,(color,profile) in pure.items():
                    slots=obj.data.attributes.get('ColorProfileSlot')
                    bound=int(slots.data[index].value) if slots else -1
                    item=color_profile.find_entry(context.scene,(color,profile),bound)
                    category=item.category_index if item is not None else -1
                    groups.setdefault((color,profile,category),[]).append(index)
                module=authoring.palette_module()
                for (color,profile,category),indices in groups.items():
                    module._apply_color_profile_to_polygons(obj,indices,color,profile,category)
                palette_brush.assign_material(obj,faces,material_slot)
            self.report({'INFO'},'已让展开细节 UV 的面共用此图；其余原材质已保留')
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}
        finally:
            if restore_edit and active is not None and active.mode!='EDIT':
                context.view_layer.objects.active=active
                bpy.ops.object.mode_set(mode='EDIT')


class WG_ENV_OT_Emission(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_prepare_emission';bl_label='创建 / 接入发光遮罩';bl_options={'REGISTER','UNDO'}
    def execute(self,context):
        try:
            image=emission.prepare(context)
            self.report({'INFO'},'已接入自发光遮罩：'+image.name+'；黑色不发光，白色发光')
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Paint(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_paint';bl_label='绘制共享细节'
    erase:EnumProperty(items=[('DRAW','绘制',''),('ERASE','擦除','')],default='DRAW')
    channel:EnumProperty(items=[('DETAIL','底色细节',''),('EMISSION','自发光','')],default='DETAIL')
    def execute(self,context):
        try:
            from .group_bake import mapped_faces
            obj=context.object
            if obj is None or not obj.get('wgame_environment_asset'):raise ValueError('请选择场景模型')
            # Release the modal face-color brush before native texture painting.
            # Otherwise it keeps consuming Ctrl+Z intended for image strokes.
            context.window_manager.wgame_environment_brush_active=False
            if obj.mode=='EDIT':bpy.ops.object.mode_set(mode='OBJECT')
            valid=mapped_faces(obj)
            if not valid:raise ValueError('先选择需要细节的面并展开 DetailUV；不会自动展开整个模型')
            if self.channel=='EMISSION':
                masks={emission.mask_image(obj.data.materials[obj.data.polygons[index].material_index]) for index in valid}
                if None in masks or len(masks)!=1:
                    # Record creation separately so the first stroke undo cannot
                    # remove an implicitly created image and its material links.
                    if bpy.ops.wgame.environment_prepare_emission('EXEC_DEFAULT',True)!={'FINISHED'}:
                        raise ValueError('发光遮罩准备失败')
                    image=emission.find_mask(shared_detail.canvas(obj=obj))
                else:image=next(iter(masks))
                emission.validate_image(image)
            else:image=shared_detail.canvas(obj=obj)
            if obj.mode!='TEXTURE_PAINT':authoring.activate(obj)
            obj.data.uv_layers.active=obj.data.uv_layers['DetailUV']
            selected={p.index for p in obj.data.polygons if p.select}&valid
            for p in obj.data.polygons:p.select=p.index in (selected or valid)
            obj.data.use_paint_mask=True
            paint=context.scene.tool_settings.image_paint;paint.mode='IMAGE';paint.canvas=image
            node_name=emission.NODE if self.channel=='EMISSION' else 'Detail_Texture'
            for mat in obj.data.materials:
                if mat and mat.use_nodes and mat.node_tree.nodes.get(node_name):
                    mat.node_tree.nodes.active=mat.node_tree.nodes[node_name]
            if obj.mode!='TEXTURE_PAINT':bpy.ops.object.mode_set(mode='TEXTURE_PAINT')
            paint.mode='IMAGE';paint.canvas=image
            if not paint.brush:
                bpy.ops.brush.asset_activate(asset_library_type='ESSENTIALS',
                    relative_asset_identifier='brushes/essentials_brushes-mesh_texture.blend/Brush/Paint Hard')
            if self.channel=='EMISSION':
                context.scene.wgame_environment_emission_erasing=self.erase=='ERASE'
                emission.paint_value_changed(context.scene,context)
            else:
                if paint.brush:paint.brush.blend='ERASE_ALPHA' if self.erase=='ERASE' else 'MIX'
                paint_color_changed(context.scene,context)
            for area in context.screen.areas:
                if area.type=='IMAGE_EDITOR':area.spaces.active.mode='PAINT';area.spaces.active.image=image
                elif area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL'
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Save(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_save';bl_label='保存制作文件与细节图'
    def execute(self,context):
        try:workflow.save_source();self.report({'INFO'},'已保存共享画布与制作文件');return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_AssemblyAudit(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_assembly_audit';bl_label='检查 Unity 拼装组件'
    def execute(self,context):
        try:
            report,path=workflow.audit_for_unity(context.scene)
            self.report({'INFO'},f"检查 {len(report['assets'])} 件；待复核 {report['warningCount']} 项。报告：{path}")
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Bake(EnvironmentOperator,bpy.types.Operator):
    bl_idname='wgame.environment_bake';bl_label='按共同细节图联合烘焙'
    def execute(self,context):
        try:workflow.start(context.scene);return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Review(bpy.types.Operator):
    bl_idname='wgame.environment_review';bl_label='查看本次烘焙'
    def execute(self,context):
        try:workflow.preview();return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Return(bpy.types.Operator):
    bl_idname='wgame.environment_return';bl_label='返回制作'
    def execute(self,context):workflow.return_to_source();return {'FINISHED'}


class WG_ENV_OT_Publish(bpy.types.Operator):
    bl_idname='wgame.environment_publish';bl_label='发布已查看的烘焙结果'
    def execute(self,context):
        try:self.report({'INFO'},str(workflow.publish()));return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_SyncUnity(bpy.types.Operator):
    bl_idname='wgame.environment_sync_unity';bl_label='同步已发布源包到 Unity'
    def execute(self,context):
        try:
            path,changed,stale=workflow.sync_to_unity()
            message=f'Unity 资源已同步：更新 {changed} 个文件；目录：{path}'
            if stale:message+=f'；旧版多余文件 {len(stale)} 个未自动删除'
            self.report({'WARNING'} if stale else {'INFO'},message)
            return {'FINISHED'}
        except Exception as error:self.report({'ERROR'},str(error));return {'CANCELLED'}


class WG_ENV_OT_Log(bpy.types.Operator):
    bl_idname='wgame.environment_log';bl_label='查看烘焙日志'
    def execute(self,context):
        if not workflow.JOB:return {'CANCELLED'}
        bpy.ops.wm.path_open(filepath=str(workflow.JOB['directory']/'bake.log'))
        return {'FINISHED'}


class WG_ENV_OT_VertexExport(bpy.types.Operator):
    bl_idname='wgame.environment_export_vertex_mountains'
    bl_label='导出选中顶点色远山到 Unity'
    bl_description='独立 FBX、线性顶点色及 Alpha 遮蔽；不生成 UV 或贴图，不改制作模型'

    @classmethod
    def poll(cls,context):
        return context.mode=='OBJECT' and vertex_export.is_work_file() and any(o.type=='MESH' for o in context.selected_objects)

    def execute(self,context):
        try:
            records,path=vertex_export.export_selected(context)
            self.report({'INFO'},f'已导出 {len(records)} 座顶点色远山：{path}；Unity 导入时使用清单指定材质')
            return {'FINISHED'}
        except Exception as error:
            self.report({'ERROR'},str(error))
            return {'CANCELLED'}


def draw_panel(panel,context):
    layout=panel.layout;scene=context.scene
    if vertex_export.is_work_file():
        layout.label(text='远山：纯顶点色 / 无 UV / 无贴图')
        layout.label(text='选中需要导出的山体；保持现有制作布局')
        layout.operator('wgame.environment_export_vertex_mountains')
        obj=context.object
        if obj and obj.get('wgame_mountain_export_id'):
            layout.label(text='导出编号：'+obj['wgame_mountain_export_id'])
        return
    if workflow.PREVIEW:
        layout.label(text='组内模型共用最终烘焙贴图')
        layout.operator('wgame.environment_return');layout.operator('wgame.environment_publish');return
    if not scene.get('wgame_environment_work'):layout.label(text='请打开场景制作文件');return
    if scene.get('wgame_environment_pipeline')!=shared_detail.PIPELINE:
        layout.label(text='此文件尚未迁移到共享细节工作流');return
    intake=layout.box();missing=registration.pending(scene)
    registered=sum(o.type=='MESH' and bool(o.get('wgame_environment_asset')) for o in scene.objects)
    auxiliary=sum(o.type=='MESH' and not o.get('wgame_environment_asset') and bool(o.get(registration.EXCLUDE_KEY)) for o in scene.objects)
    intake.label(text=f'模型登记：已登记 {registered} · 待登记 {len(missing)} · 辅助 {auxiliary}')
    if missing:
        warning=intake.column(align=True);warning.alert=True
        warning.label(text='这些模型不会自动参加烘焙，请先登记',icon='ERROR')
        for item in missing[:4]:warning.label(text=item.name)
        if len(missing)>4:warning.label(text=f'另有 {len(missing)-4} 个…')
    row=intake.row(align=True)
    row.operator('wgame.environment_select_unregistered');row.operator('wgame.environment_register')
    row=intake.row(align=True)
    row.operator('wgame.environment_auxiliary',text='设为辅助模型').excluded=True
    row.operator('wgame.environment_auxiliary',text='恢复待登记').excluded=False
    color_profile.seed(scene)
    obj=context.object
    layout.label(text='制作：PaletteUV + DetailUV')
    profile_box=layout.box();profile_box.label(text='场景配色 Profile',icon='COLOR')
    if scene.wgame_environment_colors:
        profile_box.template_list('WG_ENV_UL_Colors','',scene,'wgame_environment_colors',
                                  scene,'wgame_environment_color_active',
                                  rows=min(8,max(4,len(scene.wgame_environment_colors))))
        selected=color_profile.active(scene)
        if selected:
            profile_box.label(text=f'{selected.hex} · 表面类型 {selected.surface_profile}')
    else:
        profile_box.label(text='尚无配色条目')
    row=profile_box.row(align=True)
    row.operator('wgame.environment_profile_import_used',text='从模型导入已用',icon='IMPORT')
    row.prop(scene,'wgame_environment_color_profile_expanded',text='配置配色表',
             icon='TRIA_DOWN' if scene.wgame_environment_color_profile_expanded else 'TRIA_RIGHT',emboss=False)
    if scene.wgame_environment_color_profile_expanded:
        settings=profile_box.column(align=True)
        settings.prop(scene,'wgame_environment_color_profile_name',text='Profile 名称')
        settings.prop(scene,'wgame_environment_color_profile_id',text='ID')
        row=settings.row(align=True)
        row.prop(scene,'wgame_environment_color_profile_path',text='文件')
        row.operator('wgame.environment_profile_load',text='',icon='IMPORT')
        row.operator('wgame.environment_profile_save',text='',icon='EXPORT')
        settings.prop(scene,'wgame_environment_preset',text='快捷色草稿')
        settings.prop(scene,'wgame_environment_color',text='颜色草稿')
        settings.prop(scene,'wgame_environment_profile',text='表面类型草稿')
        settings.prop(scene,'wgame_environment_color_new_label',text='新颜色名称')
        row=settings.row(align=True)
        row.operator('wgame.environment_profile_add',text='添加新颜色',icon='ADD')
        row.operator('wgame.environment_profile_update',text='更新当前 Profile',icon='FILE_REFRESH')
        row.operator('wgame.environment_profile_remove',text='',icon='TRASH')
    paint_box=layout.box();paint_box.label(text='调色板底色画笔',icon='BRUSH_DATA')
    brush_active=getattr(context.window_manager,'wgame_environment_brush_active',False)
    row=paint_box.row(align=True);row.scale_y=1.35
    if brush_active:
        row.operator('wgame.environment_brush_stop',text='退出画笔绘制 (Esc / 右键)',icon='CANCEL')
    else:
        row.operator('wgame.environment_brush',text='开始画笔绘制',icon='BRUSH_DATA')
    row.operator('wgame.environment_apply_color',text='应用到选中面',icon='FACESEL')
    row=paint_box.row(align=True)
    row.prop(scene,'wgame_environment_brush_radius',text='笔刷半径')
    row.label(text='[F]')
    row=paint_box.row(align=True)
    row.prop(scene,'wgame_environment_brush_symmetry_x',text='X轴镜像',toggle=True)
    row.prop(scene,'wgame_environment_brush_fill_island',text='孤岛填充',toggle=True)
    row.prop(scene,'wgame_environment_brush_cull_backfaces',text='背面剔除',toggle=True)
    if not brush_active:
        row=paint_box.row()
        row.operator('wgame.environment_eyedropper',text='吸管吸色 (Alt)',icon='EYEDROPPER')
    paint_box.label(text='左键涂抹 · Shift填充 · Alt吸色 · 0-9切Profile · Ctrl+Z撤销')
    box=layout.box();box.label(text='细节 UV 按模型独立编辑')
    box.operator('wgame.environment_edit_uv')
    if hasattr(scene,'wgame_uv_tools'):
        box.prop(scene.wgame_uv_tools,'face_unwrap_mode',text='展开方式')
        row=box.row(align=True)
        row.operator('wgame.environment_uv_action',text='生成选区 UV').action='CREATE'
        row.operator('wgame.environment_uv_action',text='选区留空').action='CLEAR'
    box.label(text='仅展开需要绘制的区域；可重叠复用')
    box=layout.box();box.label(text='纹理细节画笔（需展开 DetailUV）')
    box.prop(scene,'wgame_environment_detail_image',text='细节图')
    box.operator('wgame.environment_bind_detail')
    if obj and obj.get('wgame_environment_asset'):
        try:box.label(text='当前：'+shared_detail.canvas(obj=obj).name)
        except ValueError:pass
    row=box.row(align=True)
    row.operator('wgame.environment_paint',text='绘制').erase='DRAW'
    row.operator('wgame.environment_paint',text='擦除').erase='ERASE'
    box.label(text='同图且 UV 重叠的区域会同步变化')
    box=layout.box();box.label(text='自发光（Emission）')
    box.operator('wgame.environment_prepare_emission')
    mask=None
    if obj and obj.get('wgame_environment_asset'):
        try:
            mask=emission.find_mask(shared_detail.canvas(obj=obj))
            if mask:box.label(text='遮罩：'+mask.name)
        except ValueError:pass
    row=box.row();row.enabled=mask is not None
    row.prop(scene,'wgame_environment_emission_intensity',text='发光强度（HDR）')
    box.prop(scene,'wgame_environment_emission_value',text='画笔发光量',slider=True)
    row=box.row(align=True)
    operator=row.operator('wgame.environment_paint',text='绘制发光');operator.channel='EMISSION';operator.erase='DRAW'
    operator=row.operator('wgame.environment_paint',text='擦除发光');operator.channel='EMISSION';operator.erase='ERASE'
    box.label(text='黑色不发光，白色最亮；颜色沿用底色')
    box.label(text='HDR 强度控制亮度，画笔发光量控制局部遮罩')
    box.label(text='共用 DetailUV；同图同 UV 的区域同步发光')
    layout.operator('wgame.environment_save')
    layout.operator('wgame.environment_assembly_audit')
    layout.prop(scene,'wgame_environment_bake_size',text='合图尺寸')
    layout.prop(scene,'wgame_environment_bake_device',text='烘焙设备')
    layout.operator('wgame.environment_bake');layout.operator('wgame.environment_review')
    layout.operator('wgame.environment_sync_unity')
    if workflow.JOB:
        layout.label(text=workflow.JOB.get('status',''));layout.operator('wgame.environment_log')


class WG_ENV_PT_Panel(bpy.types.Panel):
    bl_label='场景上色与共享细节';bl_idname='WG_ENV_PT_Panel'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='WGame 场景'
    def draw(self,context):draw_panel(self,context)


class WG_ENV_PT_UV(bpy.types.Panel):
    bl_label='场景上色与共享细节';bl_idname='WG_ENV_PT_UV'
    bl_space_type='IMAGE_EDITOR';bl_region_type='UI';bl_category='WGame 场景'
    def draw(self,context):draw_panel(self,context)


CLASSES=(WG_ENV_OT_Register,WG_ENV_OT_SelectUnregistered,WG_ENV_OT_Auxiliary,
         WG_ENV_OT_Color,WG_ENV_OT_Eyedropper,WG_ENV_OT_EditUV,WG_ENV_OT_UVAction,WG_ENV_OT_Bind,WG_ENV_OT_Emission,WG_ENV_OT_Paint,
         WG_ENV_OT_Save,WG_ENV_OT_AssemblyAudit,WG_ENV_OT_Bake,WG_ENV_OT_Review,
         WG_ENV_OT_Return,WG_ENV_OT_Publish,WG_ENV_OT_SyncUnity,WG_ENV_OT_Log,WG_ENV_OT_VertexExport,
         *color_profile.CLASSES,
         palette_brush.WG_ENV_OT_BrushStroke,palette_brush.WG_ENV_OT_Brush,palette_brush.WG_ENV_OT_BrushStop,
         WG_ENV_PT_Panel,WG_ENV_PT_UV)


@persistent
def initialise_scene_ui(*_):
    scene=getattr(bpy.context,'scene',None)
    if scene is None:return
    if scene.get('wgame_environment_pipeline')!=shared_detail.PIPELINE:return
    color_profile.seed(scene)
    try:scene.wgame_environment_detail_image=shared_detail.canvas(scene)
    except ValueError:pass


def register():
    for cls in CLASSES:bpy.utils.register_class(cls)
    scene=bpy.types.Scene
    color_profile.register_properties()
    scene.wgame_environment_color=FloatVectorProperty(subtype='COLOR_GAMMA',size=3,min=0,max=1,default=(.62,.22,.16),update=paint_color_changed)
    scene.wgame_environment_profile=EnumProperty(items=authoring.palette_module().PROFILE_ITEMS,default='11')
    scene.wgame_environment_preset=EnumProperty(items=[(key,LABELS[key],'') for key in COLORS],default='red',update=preset_changed)
    scene.wgame_environment_brush_radius=FloatProperty(name='笔刷半径',default=.035,min=.002,max=.6,subtype='DISTANCE')
    scene.wgame_environment_brush_symmetry_x=BoolProperty(name='X轴镜像',default=False)
    scene.wgame_environment_brush_fill_island=BoolProperty(name='孤岛填充',default=False)
    scene.wgame_environment_brush_cull_backfaces=BoolProperty(name='背面剔除',default=True)
    bpy.types.WindowManager.wgame_environment_brush_active=BoolProperty(default=False,options={'SKIP_SAVE'})
    scene.wgame_environment_detail_image=PointerProperty(type=bpy.types.Image)
    scene.wgame_environment_emission_value=FloatProperty(name='画笔发光量',default=1,min=0,max=1,update=emission.paint_value_changed)
    scene.wgame_environment_emission_intensity=FloatProperty(name='发光强度（HDR）',min=1,max=64,soft_max=16,
        get=emission.intensity_get,set=emission.intensity_set)
    scene.wgame_environment_emission_erasing=BoolProperty(default=False,options={'SKIP_SAVE'})
    scene.wgame_environment_bake_size=EnumProperty(items=[(v,v,'') for v in ('AUTO','512','1024','2048','4096')],default='AUTO')
    scene.wgame_environment_bake_device=EnumProperty(items=[
        ('AUTO','自动','纯色场景用 CPU，含绘制细节时优先 GPU'),
        ('GPU','GPU','优先使用 OptiX，无法使用时回退 CPU'),
        ('CPU','CPU','始终使用 CPU')],default='AUTO')
    bpy.app.handlers.save_pre.append(workflow.before_save)
    bpy.app.handlers.load_pre.append(workflow.return_to_source)
    bpy.app.handlers.load_post.append(initialise_scene_ui)
    initialise_scene_ui()
    if not bpy.app.background and not bpy.app.timers.is_registered(initialise_scene_ui):
        bpy.app.timers.register(initialise_scene_ui,first_interval=.1)


def unregister():
    workflow.return_to_source()
    if bpy.app.timers.is_registered(initialise_scene_ui):bpy.app.timers.unregister(initialise_scene_ui)
    if initialise_scene_ui in bpy.app.handlers.load_post:bpy.app.handlers.load_post.remove(initialise_scene_ui)
    for handler,callback in ((bpy.app.handlers.save_pre,workflow.before_save),(bpy.app.handlers.load_pre,workflow.return_to_source)):
        if callback in handler:handler.remove(callback)
    color_profile.unregister_properties()
    for cls in reversed(CLASSES):bpy.utils.unregister_class(cls)
    for name in ('color','profile','preset','brush_radius','brush_symmetry_x','brush_fill_island','brush_cull_backfaces','detail_image','emission_value','emission_intensity','emission_erasing','bake_size','bake_device'):
        if hasattr(bpy.types.Scene,'wgame_environment_'+name):delattr(bpy.types.Scene,'wgame_environment_'+name)
    if hasattr(bpy.types.WindowManager,'wgame_environment_brush_active'):
        delattr(bpy.types.WindowManager,'wgame_environment_brush_active')
