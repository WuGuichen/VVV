"""Run in a graphical factory-startup Blender; checks the real file selector."""
import json
from pathlib import Path
import sys
import tempfile
import traceback

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon

addon.register()
output = Path(__file__).resolve().parents[4] / '.agent/uv-image-import'
output.mkdir(parents=True, exist_ok=True)
root = Path(tempfile.mkdtemp(prefix='image-import-', dir=output))
bpy.context.preferences.view.show_splash = False
bpy.context.preferences.filepaths.temporary_directory = str(root)
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'base_work.blend'))
bpy.ops.mesh.primitive_cube_add()
obj = bpy.context.object
obj.data.uv_layers.active.name = 'DetailUV'
target = bpy.data.images.new('Import target', width=16, height=16, alpha=True)
source = bpy.data.images.new('Import stamp', width=4, height=4, alpha=True)
addon.decals.write_pixels(source, np.full((4, 4, 4), [0, 1, 0, 1], dtype=np.float32))
source.filepath_raw = str(root / '印章.PNG')
source.file_format = 'PNG'
source.save()
mat = bpy.data.materials.new('Import material'); mat.use_nodes = True
texture = mat.node_tree.nodes.new('ShaderNodeTexImage'); texture.image = target
uv = mat.node_tree.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'DetailUV'
mat.node_tree.links.new(uv.outputs['UV'], texture.inputs['Vector'])
obj.data.materials.append(mat)
obj.data.materials.append(bpy.data.materials.new('Unmapped pure body'))
for face in obj.data.polygons:
    if face.index:
        face.material_index=1
        for index in face.loop_indices:obj.data.uv_layers.active.data[index].uv=(-1,-1)
area = next(a for a in bpy.context.screen.areas if a.type == 'VIEW_3D')
area.type = 'IMAGE_EDITOR'; area.spaces.active.image = target
base = addon.decals.pixels(target).copy()
state = {'phase': 0, 'selectors': []}


def finish(error=None):
    result = {'passed': error is None, 'error': error, 'selectors': state['selectors']}
    (root / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('IMAGE_IMPORT_RESULT', str(root), json.dumps(result, ensure_ascii=False), flush=True)
    bpy.ops.wm.quit_blender()


def advance():
    try:
        phase = state['phase']
        if phase in (0, 2, 4):
            operator = bpy.ops.wgame_uv.decal_image if phase == 4 else bpy.ops.wgame_uv.canvas_import
            with bpy.context.temp_override(area=area):
                options = {} if phase == 0 else {'filepath': str(root / 'base_work.blend')}
                assert operator('INVOKE_DEFAULT', **options) == {'RUNNING_MODAL'}
        elif phase in (1, 3, 5):
            windows = [(w, a) for w in bpy.context.window_manager.windows for a in w.screen.areas if a.type == 'FILE_BROWSER']
            assert windows, 'File selector did not open'
            window, browser = windows[0]
            params = browser.spaces.active.params
            state['selectors'].append({'operator': ('canvas_default', 'canvas_stale', 'projection')[(phase-1)//2], 'filename': params.filename})
            assert params.filename == '', 'File selector carried the blend filename: ' + params.filename
            assert Path(params.directory.decode()).resolve() == root.resolve(), 'Import directory was lost'
            with bpy.context.temp_override(window=window, area=browser):
                bpy.ops.file.cancel()
        elif phase == 6:
            with bpy.context.temp_override(area=area):
                assert bpy.ops.wgame_uv.decal_image(filepath=source.filepath_raw) == {'FINISHED'}
                previous = bpy.context.scene.wgame_uv_tools.decal_source
                for operator in (bpy.ops.wgame_uv.canvas_import, bpy.ops.wgame_uv.decal_image):
                    for path, message in (('', '图片'), (root / 'base_work.blend', '图片'), (root / 'missing.png', '不存在'), (root, '图片')):
                        try:
                            result = operator(filepath=str(path))
                        except RuntimeError as exc:
                            assert message in str(exc), str(exc)
                        else:
                            assert result == {'CANCELLED'}, (path, result)
                        assert bpy.context.scene.wgame_uv_tools.decal_source == previous
                        assert texture.image == target
                        assert np.array_equal(addon.decals.pixels(target), base)
                assert bpy.ops.wgame_uv.canvas_import(filepath=source.filepath_raw) == {'FINISHED'}
                assert addon.canvas_editor._session['phase'] == 'FLOAT'
                assert np.array_equal(addon.decals.pixels(target), base), 'Import committed before confirmation'
                assert bpy.ops.wgame_uv.canvas_finish(action='CANCEL') == {'FINISHED'}
                assert texture.image == target and np.array_equal(addon.decals.pixels(target), base)
        else:
            assert addon.canvas_editor._session is None
            assert not any(i.get(addon.canvas_editor.TAG) for i in bpy.data.images)
            finish()
            return None
        state['phase'] += 1
        return 1.0
    except Exception:
        finish(traceback.format_exc())
        return None


bpy.app.timers.register(advance, first_interval=1.0)
