# SPDX-License-Identifier: GPL-3.0-or-later
"""Connect UV authoring selections to Blender's native texture-paint canvas."""
import bpy
from . import decals


def configure_paint_view(window, target, view_state):
    for area in window.screen.areas:
        if area.type=='IMAGE_EDITOR':
            area.spaces.active.mode='PAINT'
            area.spaces.active.image=target
            area.spaces.active.show_region_ui=True
        elif area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.show_region_ui=True
            if view_state:
                region=area.spaces.active.region_3d
                region.view_rotation,region.view_location,region.view_distance,region.view_perspective=view_state
        area.tag_redraw()


class WGAMEUV_OT_enter_paint(bpy.types.Operator):
    bl_idname='wgame_uv.enter_paint'
    bl_label='进入细节绘制'
    bl_description='切到 Texture Paint，使用 DetailUV 与细节画布，保留选中面作为绘制遮罩'

    def execute(self,context):
        from . import canvas_editor
        canvas_editor.cancel()
        obj=context.active_object
        if not obj or obj.type!='MESH' or context.mode not in {'OBJECT','EDIT_MESH','PAINT_TEXTURE'}:
            self.report({'WARNING'},'请先选中角色网格')
            return {'CANCELLED'}
        if context.mode=='EDIT_MESH' and len(context.objects_in_mode)!=1:
            self.report({'WARNING'},'请一次编辑一个角色网格')
            return {'CANCELLED'}
        if not obj.data.uv_layers.get('DetailUV'):
            self.report({'WARNING'},'缺少正式 DetailUV，请先完成 UV 制作')
            return {'CANCELLED'}
        try:
            with decals.mesh_snapshot(obj) as bm:
                selected=[f for f in bm.faces if f.select and not f.hide]
                target=decals.target_for_faces(obj,selected or [f for f in bm.faces if not f.hide])
                decals.pixels(target)
                selected_count=len(selected)
            # Match checker cleanup to the existing add-on; no original material edits.
            from . import disable_checker,restore_shading_if_idle
            disable_checker([obj]);restore_shading_if_idle();decals.cancel()
            old_view=next((a.spaces.active for a in context.screen.areas if a.type=='VIEW_3D'),None)
            view_state=None
            if old_view:
                region=old_view.region_3d
                view_state=(region.view_rotation.copy(),region.view_location.copy(),region.view_distance,region.view_perspective)
            if obj.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
            workspace=bpy.data.workspaces.get('Texture Paint')
            if workspace:
                context.window.workspace=workspace
            context.view_layer.objects.active=obj
            obj.select_set(True)
            obj.data.uv_layers.active_index=obj.data.uv_layers.find('DetailUV')
            obj.data.use_paint_mask=context.scene.wgame_uv_tools.decal_workflow=='PROJECTION'
            obj.data.use_paint_mask_vertex=False
            paint=context.scene.tool_settings.image_paint
            paint.mode='IMAGE'
            paint.canvas=target
            context.scene.wgame_uv_tools.decal_target=target
            context.scene.wgame_uv_tools.ui_tab_view='DECAL'
            context.scene.wgame_uv_tools.ui_tab_uv='DECAL'
            window=context.window
            if not workspace or window.workspace == workspace:
                configure_paint_view(window,target,view_state)
            # Workspace assignment is deferred in an interactive Blender window.
            # Apply the image/view setup again only after the destination is active.
            retries=[0]
            def finish_switch():
                try:
                    if workspace and window.workspace != workspace and retries[0]<20:
                        retries[0]+=1
                        return .1
                    configure_paint_view(window,target,view_state)
                except ReferenceError:
                    pass
                return None
            bpy.app.timers.register(finish_switch,first_interval=.15)
            bpy.ops.object.mode_set(mode='TEXTURE_PAINT')
            # Mode activation may restore tool settings; assert the intended canvas again.
            paint.mode='IMAGE';paint.canvas=target
            self.report({'INFO'},f'已进入细节绘制：{target.name}，保留 {selected_count} 个接收面' if selected_count else '已进入细节绘制；请在面遮罩中选择接收区域')
        except (ValueError,RuntimeError) as exc:
            self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}
