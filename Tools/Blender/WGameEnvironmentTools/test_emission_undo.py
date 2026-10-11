"""GUI regression: run Blender with --factory-startup --enable-event-simulate.

Uses real paint strokes and Ctrl+Z / Ctrl+Shift+Z events in an isolated window.
Add -- --view3d to cover projection painting instead of the image editor.
Result JSON is written under Temp/EnvironmentSurface; no source artwork is read.
"""
import json
import sys
import traceback
from pathlib import Path

import bpy
import numpy as np
from mathutils import Quaternion,Vector
from bpy_extras.view3d_utils import location_3d_to_region_2d

if bpy.app.background:raise RuntimeError('Native stroke undo requires a graphical Blender test window')
root=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(root/'Tools/Blender'))
import WGameEnvironmentTools as addon
import WGameUVTools as uv_addon
from WGameEnvironmentTools import authoring

view3d='--view3d' in sys.argv;mode='view3d' if view3d else 'image'
cache=root/'Temp/EnvironmentSurface'/('emission-undo-'+mode);cache.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.temporary_directory=str(cache)+'/'
bpy.context.preferences.view.show_splash=False
state={};checks=[]

def check(condition,message):
    if not condition:raise AssertionError(message)
    checks.append(message);print('PASS',message,flush=True)

def finish(error=None):
    (cache/'result.json').write_text(json.dumps({'success':error is None,'checks':checks,'error':error},indent=2),encoding='utf-8')
    if error:print(error,flush=True)
    bpy.ops.wm.quit_blender()

def pixels(name):return np.array(bpy.data.images[name].pixels[:])

def mouse(region,point=(.5,.5)):
    if not view3d:return tuple(region.view2d.view_to_region(*point,clip=False))
    return tuple(location_3d_to_region_2d(region,state['area'].spaces.active.region_3d,
                                        Vector((point[0]*2-1,point[1]*2-1,0))))

def stroke(region,point=(.5,.5)):
    x,y=mouse(region,point)
    return bpy.ops.paint.image_paint('EXEC_DEFAULT',True,stroke=[
        dict(name='',mouse=(x,y),mouse_event=(x,y),location=(0,0,0),pressure=1,size=16,
             x_tilt=0,y_tilt=0,time=0,is_start=True),
        dict(name='',mouse=(x+5,y),mouse_event=(x+5,y),location=(0,0,0),pressure=1,size=16,
             x_tilt=0,y_tilt=0,time=.1,is_start=False)])

def shortcut(region,redo=False):
    x,y=mouse(region);xy=dict(x=region.x+int(x),y=region.y+int(y));window=bpy.context.window
    window.event_simulate(type='MOUSEMOVE',value='NOTHING',**xy)
    window.event_simulate(type='LEFT_CTRL',value='PRESS',**xy)
    if redo:window.event_simulate(type='LEFT_SHIFT',value='PRESS',ctrl=True,**xy)
    window.event_simulate(type='Z',value='PRESS',ctrl=True,shift=redo,**xy)
    window.event_simulate(type='Z',value='RELEASE',ctrl=True,shift=redo,**xy)
    if redo:window.event_simulate(type='LEFT_SHIFT',value='RELEASE',ctrl=True,**xy)
    window.event_simulate(type='LEFT_CTRL',value='RELEASE',**xy)

def setup():
    try:
        addon.register();uv_addon.register();scene=bpy.context.scene
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        scene['wgame_environment_work']=True;scene['wgame_environment_pipeline']='shared_detail_group_bake_v2'
        bpy.ops.mesh.primitive_plane_add();obj=bpy.context.object;obj.name='Prop_Undo';obj['wgame_environment_asset']=obj.name
        obj.data.uv_layers.active.name='DetailUV'
        material=bpy.data.materials.new('Original');material.use_nodes=True;obj.data.materials.append(material)
        palette=authoring.palette_module();palette._apply_color_profile_to_polygons(obj,[0],palette.color_index_from_rgb8(180,40,30),11)
        detail=bpy.data.images.new('UndoDetail',width=128,height=128,alpha=True);detail.generated_color=(0,0,0,0)
        scene.wgame_environment_detail_image=detail
        bpy.context.preferences.edit.use_global_undo=True;bpy.ops.ed.undo_push(message='Fixture')
        area=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D');state['area']=area
        with bpy.context.temp_override(area=area,region=next(r for r in area.regions if r.type=='WINDOW')):
            check(bpy.ops.wgame.environment_brush('INVOKE_DEFAULT')=={'RUNNING_MODAL'},'start prior palette brush')
        check(bpy.ops.wgame.environment_paint('EXEC_DEFAULT',True,channel='EMISSION')=={'FINISHED'},'auto-create emission and enter painting')
        check(not bpy.context.window_manager.wgame_environment_brush_active,'prior palette brush released')
        state['mask']=scene.tool_settings.image_paint.canvas.name;state['detail']=detail.name
        if view3d:
            region3d=area.spaces.active.region_3d;region3d.view_rotation=Quaternion((1,0,0,0))
            region3d.view_perspective='ORTHO';region3d.view_location=(0,0,0);region3d.view_distance=4
        else:
            area.type='IMAGE_EDITOR';area.spaces.active.mode='PAINT';area.spaces.active.image=bpy.data.images[state['mask']]
        state['phase']=-1;bpy.app.timers.register(advance,first_interval=1)
    except Exception:finish(traceback.format_exc())
    return None

def advance():
    try:
        area=state['area'];region=next(r for r in area.regions if r.type=='WINDOW')
        with bpy.context.temp_override(area=area,region=region):
            phase=state['phase'];mask=state['mask'];detail=state['detail']
            if phase==-1:
                bpy.ops.wm.tool_set_by_id(name='builtin.brush')
                if not view3d:bpy.ops.image.view_all(fit_view=True)
            elif phase==0:
                state['before']=pixels(mask);state['detail_before']=pixels(detail)
                check(stroke(region)=={'FINISHED'},'native emission stroke')
                state['painted']=pixels(mask);check(not np.array_equal(state['painted'],state['before']),'stroke changes emission pixels')
            elif phase==1:shortcut(region)
            elif phase==2:
                check(np.array_equal(pixels(mask),state['before']),'Ctrl+Z restores pixels and keeps new mask')
                check(np.array_equal(pixels(detail),state['detail_before']),'emission undo preserves base image')
                shortcut(region,True)
            elif phase==3:
                check(np.array_equal(pixels(mask),state['painted']),'Ctrl+Shift+Z restores emission stroke')
                bpy.ops.wgame.environment_paint('EXEC_DEFAULT',True,channel='EMISSION',erase='ERASE')
            elif phase==4:
                stroke(region);state['erased']=pixels(mask)
                check(not np.array_equal(state['erased'],state['painted']),'erase changes emission pixels')
            elif phase==5:shortcut(region)
            elif phase==6:
                check(np.array_equal(pixels(mask),state['painted']),'undo erase restores emission');shortcut(region)
            elif phase==7:
                check(np.array_equal(pixels(mask),state['before']),'switching erase keeps previous stroke history');shortcut(region,True)
            elif phase==8:
                check(np.array_equal(pixels(mask),state['painted']),'redo first stroke');shortcut(region,True)
            elif phase==9:
                check(np.array_equal(pixels(mask),state['erased']),'redo erase')
                bpy.ops.wgame.environment_paint('EXEC_DEFAULT',True,channel='DETAIL',erase='DRAW')
            elif phase==10:
                stroke(region,(.7,.5));state['detail_after']=pixels(detail)
                check(not np.array_equal(state['detail_after'],state['detail_before']),'native base-color stroke')
            elif phase==11:shortcut(region)
            elif phase==12:
                check(np.array_equal(pixels(detail),state['detail_before']),'undo base-color stroke')
                check(np.array_equal(pixels(mask),state['erased']),'base undo preserves emission');shortcut(region,True)
            elif phase==13:
                check(np.array_equal(pixels(detail),state['detail_after']),'redo base-color stroke')
                bpy.ops.wgame.environment_paint('EXEC_DEFAULT',True,channel='EMISSION',erase='DRAW')
            elif phase==14:shortcut(region)
            elif phase==15:
                check(np.array_equal(pixels(detail),state['detail_before']),'undo remains chronological across canvas switches')
                check(np.array_equal(pixels(mask),state['erased']),'canvas switch undo preserves emission pixels')
                finish();return None
        state['phase']+=1;return .4
    except Exception:finish(traceback.format_exc())
    return None

bpy.app.timers.register(setup,first_interval=1)
