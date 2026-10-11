import pathlib
import sys
import bpy
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
obj=bpy.context.object
obj.data.uv_layers.active.name='DetailUV'
before=[tuple(l.uv) for l in obj.data.uv_layers.active.data]
bpy.context.scene.wgame_uv_tools.cells=18
bpy.ops.wgame_uv.checker()
addon.hot_reload.reload_now()
assert bpy.context.scene.wgame_uv_tools.cells==18
assert len(addon.checker_modifiers(obj))==1
assert [tuple(l.uv) for l in obj.data.uv_layers.active.data]==before
assert bpy.context.window_manager.wgame_uv_reload_status.startswith('热更成功')
assert sum(h.__name__=='sync_checker_uv' for h in bpy.app.handlers.depsgraph_update_post)==1
addon.hot_reload.reload_now()
assert len(addon.checker_modifiers(obj))==1
assert sum(h.__name__=='sync_checker_uv' for h in bpy.app.handlers.depsgraph_update_post)==1
bpy.ops.wgame_uv.checker(action='CLEAR')
assert not addon.checker_modifiers(obj)
print('PASS: repeated hot reload, settings/UV preservation, checker restoration, no duplicate handlers')
