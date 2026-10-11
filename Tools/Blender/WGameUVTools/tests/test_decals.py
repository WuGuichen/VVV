import pathlib
import sys
import bpy
import numpy as np
from mathutils import Quaternion
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
d=addon.decals
# Alpha compositing, triangle shared edges must not be painted twice.
target=np.zeros((8,8,4),np.float32)
source=np.zeros((2,2,4),np.float32);source[:]=[1,0,0,.5]
tri=np.asarray([[[0,0],[1,0],[1,1]],[[0,0],[1,1],[0,1]]],float)
result,count=d.composite(target,source,tri,tri,1)
assert count==64 and np.allclose(result[:,:,3],.5) and np.allclose(result[:,:,0],1)
result,count=d.composite(target,source,tri[:1],tri[:1],1)
assert 0<count<64 and np.any(result[:,:,3]==0)
print('PASS: alpha composite; no double blending on shared edges; outside pixels preserved')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_plane_add()
obj=bpy.context.object;obj.data.uv_layers.active.name='DetailUV'
image=bpy.data.images.new('Target',width=8,height=8,alpha=True)
image.colorspace_settings.name='sRGB';d.write_pixels(image,target)
src=bpy.data.images.new('Source',width=2,height=2,alpha=True)
src.colorspace_settings.name='sRGB';d.write_pixels(src,source)
mat=bpy.data.materials.new('DetailMaterial');mat.use_nodes=True
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
uv=mat.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='DetailUV'
mat.node_tree.links.new(uv.outputs['UV'],tex.inputs['Vector']);obj.data.materials.append(mat)
area=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D')
area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0,0))
bpy.context.scene.wgame_uv_tools.decal_source=src
coords=[tuple(l.uv) for l in obj.data.uv_layers.active.data]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
with bpy.context.temp_override(area=area):
    assert bpy.ops.wgame_uv.decal(action='START')=={'FINISHED'}
    assert d._session and np.allclose(d.pixels(image),target)
    bpy.context.scene.wgame_uv_tools.decal_x=.1
    assert bpy.ops.wgame_uv.decal(action='CANCEL')=={'FINISHED'}
    assert not any(o.get(d.TAG) for o in bpy.data.objects)
    assert bpy.ops.wgame_uv.decal(action='START')=={'FINISHED'}
    assert bpy.ops.wgame_uv.decal(action='APPLY')=={'FINISHED'}
    assert np.any(d.pixels(image)[:,:,3]>.4)
    assert not any(o.get(d.TAG) for o in bpy.data.objects)
    assert bpy.ops.wgame_uv.decal(action='UNDO')=={'FINISHED'}
    assert np.allclose(d.pixels(image),target)
    # Selection changes cancel application rather than painting stale receiver faces.
    bpy.ops.wgame_uv.decal(action='START')
    bpy.ops.mesh.select_all(action='DESELECT')
    try:bpy.ops.wgame_uv.decal(action='APPLY')
    except RuntimeError:pass
    assert np.allclose(d.pixels(image),target)
    bpy.ops.wgame_uv.decal(action='CANCEL')
    bpy.ops.mesh.select_all(action='SELECT')
    # Save writes PNG and preserves the prior file as a backup.
    import tempfile
    directory=pathlib.Path(tempfile.mkdtemp(prefix='wgame-decal-test-'))
    image.filepath_raw=str(directory/'detail.png');image.file_format='PNG';image.save()
    original_file=(directory/'detail.png').read_bytes()
    bpy.ops.wgame_uv.decal(action='START');bpy.ops.wgame_uv.decal(action='APPLY')
    assert bpy.ops.wgame_uv.decal(action='SAVE')=={'FINISHED'}
    backups=list(directory.glob('detail.before-decal-*.png'))
    assert len(backups)==1 and backups[0].read_bytes()==original_file
    assert (directory/'detail.png').read_bytes()!=original_file
bpy.ops.object.mode_set(mode='OBJECT')
assert coords==[tuple(l.uv) for l in obj.data.uv_layers.active.data]
assert obj.active_material==mat
print('PASS: preview does not paint; cancel cleans up; apply changes target; explicit undo restores; source mesh/UV/material unchanged')
print('PASS: stale selection rejected; PNG save with exact prior-file backup')

# Crossing receiver UVs are clipped to the target canvas, never repeated or clamped.
crossing=tri*2-.5
result,count=d.composite(target,source,crossing,tri,1)
assert count==64 and np.allclose(result[:,:,3],.5)
result,count=d.composite(target,source,tri+2,tri,1)
assert count==0 and np.array_equal(result,target)
for item,co in zip(obj.data.uv_layers['DetailUV'].data,coords):item.uv=np.asarray(co)*2-.5
cross_coords=[tuple(item.uv) for item in obj.data.uv_layers['DetailUV'].data]
d.write_pixels(image,target)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
with bpy.context.temp_override(area=area):
    assert bpy.ops.wgame_uv.decal(action='START')=={'FINISHED'}
    # Render the disposable overlay to verify it agrees with the clipped write.
    preview=d._session['preview'];preview.hide_render=False;obj.hide_render=True
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=4
    scene.render.film_transparent=True
    camera_data=bpy.data.cameras.new('ClipTestCamera');camera_data.type='ORTHO';camera_data.ortho_scale=2.4
    camera=bpy.data.objects.new('ClipTestCamera',camera_data);scene.collection.objects.link(camera)
    camera.location=(0,0,3);scene.camera=camera
    scene.render.resolution_x=64;scene.render.resolution_y=64;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=str(directory/'clipped_preview.png')
    bpy.ops.render.render(write_still=True)
    rendered=d.pixels(bpy.data.images.load(scene.render.filepath))
    assert .4<rendered[32,32,3]<.6,rendered[32,32]
    assert rendered[32,53,3]==0 and rendered[53,32,3]==0
    obj.hide_render=False
    scene.camera=None;bpy.data.objects.remove(camera,do_unlink=True);bpy.data.cameras.remove(camera_data)
    assert bpy.ops.wgame_uv.decal(action='APPLY')=={'FINISHED'}
    assert np.allclose(d.pixels(image)[:,:,3],.5,atol=1/255)  # Byte canvas quantization.
    assert bpy.ops.wgame_uv.decal(action='UNDO')=={'FINISHED'}
    assert np.array_equal(d.pixels(image),target)
bpy.ops.object.mode_set(mode='OBJECT')
assert [tuple(item.uv) for item in obj.data.uv_layers['DetailUV'].data]==cross_coords
for invalid in ((-1,-1),(0,0),(float('nan'),0)):
    for item in obj.data.uv_layers['DetailUV'].data:item.uv=invalid
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    with bpy.context.temp_override(area=area):
        try:
            d.start(bpy.context)
        except ValueError:pass
        else:raise AssertionError('Blank, degenerate or non-finite UV was accepted')
    assert d._session is None and np.array_equal(d.pixels(image),target)
    bpy.ops.object.mode_set(mode='OBJECT')
for item,co in zip(obj.data.uv_layers['DetailUV'].data,cross_coords):item.uv=co
print('PASS: crossing UV preview render matches clipped canvas; outside-only writes nothing; invalid UV remains rejected')
