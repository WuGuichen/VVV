"""Bind a transparent detail atlas without replacing unrelated pure materials."""
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools import authoring, shared_detail, register, unregister
from WGameEnvironmentTools.group_bake import detail_image, mapped_faces

bpy.ops.wm.read_factory_settings(use_empty=True)
register()
scene = bpy.context.scene
scene['wgame_environment_work'] = True
scene['wgame_environment_pipeline'] = 'shared_detail_group_bake_v2'
mesh = bpy.data.meshes.new('TwoMaterials')
mesh.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
                  (2, 0, 0), (2, 1, 0)], [],
                 [(0, 1, 2, 3), (1, 4, 5, 2)])
mesh.update()
obj = bpy.data.objects.new('ScenePart', mesh)
scene.collection.objects.link(obj)
obj['wgame_environment_asset'] = obj.name
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
for index, rgb in enumerate(((.18, .02, .01), (.02, .10, .22))):
    material = bpy.data.materials.new(f'PureMaterial{index}')
    material.use_nodes = True
    material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*rgb, 1)
    mesh.materials.append(material)
    mesh.polygons[index].material_index = index
module = authoring.palette_module()
stale = module.color_index_from_rgb8(219, 164, 45)
module._apply_color_profile_to_polygons(obj, [0, 1], stale, 11)
detail = mesh.uv_layers.new(name='DetailUV')
for loop_index, uv in zip(mesh.polygons[0].loop_indices,
                          ((-.2, .78), (.4, .78), (.4, 1.2), (-.2, 1.2))):
    detail.data[loop_index].uv = uv
for loop_index in mesh.polygons[1].loop_indices:
    detail.data[loop_index].uv = (-1, -1)
before = [tuple(value.uv) for value in detail.data]
image_path = (authoring.repository()/'ArtSource/Environment/Base/Shared/stylized_environment_detail_stamps_v1.png')
image = bpy.data.images.load(str(image_path), check_existing=True)
scene.wgame_environment_detail_image = image
# Reusing a cached shared material must not delete the palette it references.
cached_palette=module._build_preview_image()
cached_material=shared_detail.make_material(cached_palette,image)
expected_color, expected_profile = __import__('WGameEnvironmentTools').color_profile.face_pair(obj, 0)
assert expected_color != stale
assert 'FINISHED' in bpy.ops.wgame.environment_bind_detail()
assert cached_material.node_tree.nodes['Palette_Texture'].image==cached_palette
assert mesh.polygons[0].material_index == 2
assert mesh.polygons[1].material_index == 1
assert mesh.materials[1].name == 'PureMaterial1'
assert mesh.attributes['PaletteToken'].data[0].value == expected_color * 16 + expected_profile
assert mesh.attributes['PaletteToken'].data[1].value == stale * 16 + 11
assert [tuple(value.uv) for value in mesh.uv_layers['DetailUV'].data] == before
assert detail_image(obj) == image
cached_material.node_tree.nodes['Palette_Texture'].image=None
bpy.ops.object.mode_set(mode='EDIT')
assert 'FINISHED' in bpy.ops.wgame.environment_bind_detail()
assert cached_material.node_tree.nodes['Palette_Texture'].image==cached_palette
assert obj.mode == 'EDIT'
bpy.ops.object.mode_set(mode='OBJECT')
assert mesh.polygons[1].material_index == 1
assert 'FINISHED' in bpy.ops.wgame.environment_paint(erase='DRAW')
assert obj.mode == 'TEXTURE_PAINT'
assert scene.tool_settings.image_paint.canvas == image
bpy.ops.object.mode_set(mode='OBJECT')
assert mapped_faces(obj)=={0}
for index in mesh.polygons[1].loop_indices:mesh.uv_layers['DetailUV'].data[index].uv=(-.58,-1.32)
assert mapped_faces(obj)=={0}, 'Collapsed UV outside the clipped canvas is empty'
for index in mesh.polygons[1].loop_indices:mesh.uv_layers['DetailUV'].data[index].uv=(.5,.5)
try:mapped_faces(obj)
except ValueError:pass
else:raise AssertionError('Collapsed UV within the canvas was accepted')
for index in mesh.polygons[1].loop_indices:mesh.uv_layers['DetailUV'].data[index].uv=(-1,-1)
for invalid in ((0,0),(float('nan'),0),(float('inf'),0)):
    for i in mesh.polygons[0].loop_indices:mesh.uv_layers['DetailUV'].data[i].uv=invalid
    try:mapped_faces(obj)
    except ValueError:pass
    else:raise AssertionError('Invalid DetailUV was accepted: '+repr(invalid))
for item,co in zip(mesh.uv_layers['DetailUV'].data,before):item.uv=co
unregister()
print('PASS crossing DetailUV binds/paints without moving UVs; pure materials preserved; invalid UV rejected')
