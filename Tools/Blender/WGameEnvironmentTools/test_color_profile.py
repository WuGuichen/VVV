"""Blender regression for a scene-owned, editable color Profile."""
import json
import sys
import tempfile
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools import authoring, color_profile, register, unregister

bpy.ops.wm.read_factory_settings(use_empty=True)
register()
scene = bpy.context.scene
scene['wgame_environment_work'] = True
module = authoring.palette_module()
red = module.color_index_from_rgb8(158, 56, 40)
blue = module.color_index_from_rgb8(80, 91, 98)
red_rgb = module.rgb8_from_color_index(red)
red_linear = tuple(value / 255 / 12.92 if value / 255 <= .04045 else
                   ((value / 255 + .055) / 1.055) ** 2.4 for value in red_rgb)
image = bpy.data.images.new('SharedCanvas', width=8, height=8, alpha=True)
image['wgame_environment_shared_detail'] = True
objects = []
for index in range(2):
    mesh = bpy.data.meshes.new(f'SceneMesh{index}')
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], [], [(0, 1, 2, 3)])
    mesh.update()
    obj = bpy.data.objects.new(f'ScenePart{index}', mesh)
    scene.collection.objects.link(obj)
    obj['wgame_environment_asset'] = obj.name
    material = bpy.data.materials.new(f'OriginalColor{index}')
    material.use_nodes = True
    material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*red_linear, 1)
    mesh.materials.append(material)
    module._apply_color_profile_to_polygons(obj, [0], red, 11)
    layer = mesh.uv_layers.new(name='DetailUV')
    for loop in layer.data:
        loop.uv = (-1, -1)
    objects.append(obj)

color_profile.seed(scene)
assert len(scene.wgame_environment_colors) == 1
assert all(obj.data.attributes.get('ColorProfileSlot') is None for obj in objects)
objects[0].select_set(True)
bpy.context.view_layer.objects.active = objects[0]
bpy.ops.object.mode_set(mode='EDIT')
assert 'FINISHED' in bpy.ops.wgame.environment_profile_import_used()
assert objects[0].mode == 'EDIT'
bpy.ops.object.mode_set(mode='OBJECT')
item = color_profile.active(scene)
category = item.category_index
assert all(obj.data.attributes['ColorProfileSlot'].data[0].value == category for obj in objects)

# A model added after classification still follows its matching Profile entry.
mesh = bpy.data.meshes.new('LateMesh')
mesh.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], [], [(0, 1, 2, 3)])
mesh.update()
late = bpy.data.objects.new('LatePart', mesh)
scene.collection.objects.link(late)
late['wgame_environment_asset'] = late.name
material = bpy.data.materials.new('LateOriginalColor')
material.use_nodes = True
material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*red_linear, 1)
mesh.materials.append(material)
module._apply_color_profile_to_polygons(late, [0], red, 11)
layer = mesh.uv_layers.new(name='DetailUV')
for loop in layer.data:
    loop.uv = (-1, -1)
assert mesh.attributes.get('ColorProfileSlot') is None
objects.append(late)

# A legacy slot with a stale PaletteToken must not recolor unrelated pure material.
stale_mesh = mesh.copy()
stale = bpy.data.objects.new('StaleSlotPart', stale_mesh)
scene.collection.objects.link(stale)
stale['wgame_environment_asset'] = stale.name
stale_material = bpy.data.materials.new('ActualLightMaterial')
stale_material.use_nodes = True
stale_mesh.materials.clear()
stale_mesh.materials.append(stale_material)
stale_slot = stale_mesh.attributes.new(name='ColorProfileSlot', type='INT', domain='FACE')
stale_slot.data[0].value = category

scene.wgame_environment_color = tuple(value / 255 for value in (80, 91, 98))
scene.wgame_environment_profile = '12'
assert 'FINISHED' in bpy.ops.wgame.environment_profile_update()
assert color_profile.pair(item) == (blue, 12)
for obj in objects:
    assert obj.data.attributes['PaletteToken'].data[0].value == blue * 16 + 12
    assert obj.data.attributes['ColorProfileSlot'].data[0].value == category
    assert obj.data.materials[obj.data.polygons[0].material_index].get('wgame_environment_shared')
    assert all(tuple(loop.uv) == (-1, -1) for loop in obj.data.uv_layers['DetailUV'].data)
assert stale_mesh.attributes['PaletteToken'].data[0].value == red * 16 + 11
assert stale_mesh.polygons[0].material_index == 0
assert stale_mesh.materials[0] == stale_material
assert 'FINISHED' in bpy.ops.wgame.environment_profile_import_used()
assert stale_mesh.attributes['ColorProfileSlot'].data[0].value != category

try:
    bpy.ops.wgame.environment_profile_remove()
except RuntimeError as error:
    assert '仍被 3 个面使用' in str(error)
else:
    raise AssertionError('Used Profile entry was removed')
scratch = authoring.repository()/'Temp/EnvironmentSurface'
scratch.mkdir(parents=True, exist_ok=True)
folder = Path(tempfile.mkdtemp(prefix='scene-profile-', dir=scratch))
target = folder/'environment_color_profile.json'
scene.wgame_environment_color_profile_path = str(target)
assert 'FINISHED' in bpy.ops.wgame.environment_profile_save()
saved = json.loads(target.read_text(encoding='utf-8'))
assert saved['format'] == color_profile.FORMAT and len(saved['colors']) == 2
assert 'FINISHED' in bpy.ops.wgame.environment_profile_load()
assert color_profile.pair(color_profile.active(scene)) == (blue, 12)
legacy = color_profile.add(scene, module.color_index_from_rgb8(7, 200, 5), 11, 'Legacy Custom')
legacy_category = legacy.category_index
scene.wgame_environment_colors.move(len(scene.wgame_environment_colors) - 1, 0)
scene.wgame_environment_color_active = 0
scene['wgame_environment_color_profile_source'] = 'legacyPaletteToken'
color_profile.seed(scene)
assert color_profile.pair(scene.wgame_environment_colors[0]) == (blue, 12)
assert color_profile.active(scene).category_index == legacy_category
unregister()
print('PASS scene Profile import, linked update, preservation, save and load')
