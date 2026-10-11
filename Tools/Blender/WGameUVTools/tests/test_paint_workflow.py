import runpy
import pathlib
import bpy
import numpy as np
runpy.run_path(str(pathlib.Path(__file__).with_name('test_decals.py')))
import WGameUVTools as addon
obj=bpy.context.active_object
source=bpy.context.scene.wgame_uv_tools.decal_source
target=bpy.context.scene.wgame_uv_tools.decal_target
before=addon.decals.pixels(target)
bpy.context.scene.wgame_uv_tools.decal_workflow='PROJECTION'
uvs=[tuple(l.uv) for l in obj.data.uv_layers['DetailUV'].data]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
assert bpy.ops.wgame_uv.enter_paint()=={'FINISHED'}
assert bpy.context.mode=='PAINT_TEXTURE'
assert obj.data.use_paint_mask
assert obj.data.uv_layers.active.name=='DetailUV'
assert bpy.context.scene.tool_settings.image_paint.canvas==target
assert bpy.context.scene.tool_settings.image_paint.mode=='IMAGE'
assert sum(p.select for p in obj.data.polygons)==1
assert [tuple(l.uv) for l in obj.data.uv_layers['DetailUV'].data]==uvs
assert np.allclose(addon.decals.pixels(target),before)
assert bpy.ops.wgame_uv.decal(action='START')=={'FINISHED'}
assert bpy.context.mode=='PAINT_TEXTURE'
assert bpy.ops.wgame_uv.decal(action='APPLY')=={'FINISHED'}
assert np.any(addon.decals.pixels(target)!=before)
assert bpy.ops.wgame_uv.decal(action='UNDO')=={'FINISHED'}
assert np.allclose(addon.decals.pixels(target),before)
print('PASS: enter texture paint, preserve face mask/UV, correct canvas, preview/apply/undo in texture-paint mode')
