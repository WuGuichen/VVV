# SPDX-License-Identifier: GPL-3.0-or-later
"""Reload this add-on after its invoking UI operator has returned."""
import importlib
import json
from pathlib import Path
import sys
import bpy

FILES = ('islands.py', 'face_authoring.py', 'detail_binding.py', 'detail_images.py', 'detail_lifecycle.py', 'uv_contract.py', 'decals.py', 'canvas_editor.py', 'decal_drop.py', 'paint_workflow.py', 'rounding.py', 'feather.py', 'hot_reload.py', '__init__.py')
_pending = False


def reload_now():
    global _pending
    package = sys.modules[__package__]
    directory = Path(package.__file__).parent
    metadata = directory / 'deployment.json'
    source = directory
    try:
        if metadata.exists():
            source = Path(json.loads(metadata.read_text(encoding='utf-8-sig'))['sourceRoot'])
        contents = {name: (source / name).read_text(encoding='utf-8-sig') for name in FILES}
        for name, code in contents.items():
            compile(code, str(source / name), 'exec')
        if source.resolve() != directory.resolve():
            for name, code in contents.items():
                target = directory / name
                if target.exists():
                    (directory / (name + '.previous')).write_bytes(target.read_bytes())
                target.write_text(code, encoding='utf-8')
        if hasattr(package, 'canvas_editor'):
            package.canvas_editor.cancel()
        props = ('pattern', 'cells', 'relax_iterations', 'resolution', 'padding', 'old_detail_uv', 'face_unwrap_mode', 'decal_source', 'decal_target', 'decal_x', 'decal_y', 'decal_scale', 'decal_angle', 'decal_opacity', 'ui_tab_uv', 'ui_tab_view', 'decal_workflow', 'canvas_show_uv', 'uv_round_strength', 'mesh_round_strength', 'mesh_round_mode', 'feather_radius', 'feather_mode', 'feather_falloff')
        settings = [(scene, {k: getattr(scene.wgame_uv_tools, k) for k in props if hasattr(scene.wgame_uv_tools, k)})
                    for scene in bpy.data.scenes]
        previews = []
        seen = set()
        for scene in bpy.data.scenes:
            for obj in scene.objects:
                if obj.type == 'MESH' and obj.as_pointer() not in seen and package.checker_modifiers(obj):
                    seen.add(obj.as_pointer())
                    previews.append((obj, scene))
        original_views = dict(getattr(package, '_viewport_states', {}))
        original_images = dict(getattr(package, '_image_states', {}))
        views, images = [], []
        for screen in bpy.data.screens:
            for area in screen.areas:
                space = area.spaces.active
                if area.type == 'VIEW_3D':
                    views.append((space, space.shading.type))
                elif area.type == 'IMAGE_EDITOR':
                    images.append((space, space.image))
        package.unregister()
        # Execute verified text to avoid Python's same-second bytecode cache.
        for name in FILES[:-1]:
            module = importlib.import_module(__package__ + '.' + name[:-3])
            exec(compile(contents[name], str(directory / name), 'exec'), module.__dict__)
        exec(compile(contents['__init__.py'], str(directory / '__init__.py'), 'exec'), package.__dict__)
        package.register()
        for scene, values in settings:
            for key, value in values.items():
                setattr(scene.wgame_uv_tools, key, value)
        for obj, scene in previews:
            uv = obj.data.uv_layers.active
            if uv and uv.name != 'PaletteUV':
                package.enable_checker(obj, uv.name, scene.wgame_uv_tools)
        for space, mode in views:
            space.shading.type = mode
        for space, image in images:
            space.image = image
        package._viewport_states.update(original_views)
        package._image_states.update(original_images)
        version = '.'.join(map(str, package.bl_info['version']))
        bpy.context.window_manager.wgame_uv_reload_status = '热更成功 · ' + version
    except Exception as exc:
        if not hasattr(bpy.types.WindowManager, 'wgame_uv_reload_status'):
            bpy.types.WindowManager.wgame_uv_reload_status = bpy.props.StringProperty(options={'SKIP_SAVE'})
        bpy.context.window_manager.wgame_uv_reload_status = '热更失败：' + str(exc)
        import traceback
        traceback.print_exc()
    finally:
        _pending = False
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                area.tag_redraw()
    return None


class WGAMEUV_OT_hot_reload(bpy.types.Operator):
    bl_idname = 'wgame_uv.hot_reload'
    bl_label = '热更新插件'
    bl_description = '同步本机部署记录中的仓库源码并重新加载；独立 ZIP 安装只重载安装目录'

    def execute(self, context):
        global _pending
        if _pending:
            self.report({'INFO'}, '正在热更新，请稍候')
            return {'CANCELLED'}
        _pending = True
        context.window_manager.wgame_uv_reload_status = '正在热更新…'
        bpy.app.timers.register(reload_now, first_interval=0.2)
        return {'FINISHED'}
