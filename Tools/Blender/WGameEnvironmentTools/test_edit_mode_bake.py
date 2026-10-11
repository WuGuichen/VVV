"""Bake from live Edit Mode UVs without changing the source mode or saved work."""
import sys
import tempfile
from pathlib import Path

import bpy
import bmesh

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import WGameEnvironmentTools as addon
from WGameEnvironmentTools import authoring,emission,group_bake,shared_detail,workflow

bpy.ops.wm.read_factory_settings(use_empty=True);addon.register()
scene=bpy.context.scene;scene['wgame_environment_work']=True;scene['wgame_environment_pipeline']=shared_detail.PIPELINE
scene.wgame_environment_bake_size='512';scene.wgame_environment_bake_device='CPU'
mesh=bpy.data.meshes.new('EditModeBake')
mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0),(2,0,0),(2,1,0)],[],[(0,1,2,3),(1,4,5,2)]);mesh.update()
obj=bpy.data.objects.new('Prop_EditModeBake',mesh);scene.collection.objects.link(obj)
obj['wgame_environment_asset']=obj.name;authoring.activate(obj)
palette=authoring.palette_module()
palette._apply_color_profile_to_polygons(obj,[0,1],palette.color_index_from_rgb8(180,40,30),11)
uv=mesh.uv_layers.new(name='DetailUV',do_init=False)
for item in uv.data:item.uv=(-1,-1)
image=bpy.data.images.new('DetailEditMode',width=16,height=16,alpha=True);image.generated_color=(0,0,0,0)
image['wgame_environment_shared_detail']=True;image.pack();scene.wgame_environment_detail_image=image
material=shared_detail.make_material(shared_detail.palette_image([obj]),image);mesh.materials.append(material)
mask=emission.ensure_mask(image);emission.attach(material,mask)
test_root=authoring.repository()/'Temp/EnvironmentSurface';test_root.mkdir(parents=True,exist_ok=True)
root=Path(tempfile.mkdtemp(prefix='edit-mode-bake-',dir=test_root));source=root/'work.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
source_hash=authoring.bake_module().sha(source)
bpy.ops.object.mode_set(mode='EDIT')
edit=bmesh.from_edit_mesh(mesh);edit.faces.ensure_lookup_table()
layer=edit.loops.layers.uv.get('DetailUV')
for loop,co in zip(edit.faces[0].loops,((-.2,.1),(.8,.1),(.8,1.1),(-.2,1.1))):loop[layer].uv=co
for loop in edit.faces[1].loops:loop[layer].uv=(-.58,-1.32)
edit.faces[0].select_set(True);edit.faces[1].select_set(False)
bmesh.update_edit_mesh(mesh,loop_triangles=False,destructive=False)

def capture():
    current=bmesh.from_edit_mesh(mesh)
    layer=current.loops.layers.uv.get('DetailUV')
    return [(face.select,[tuple(loop[layer].uv) for loop in face.loops]) for face in current.faces]

before=capture();pixels_before=list(mask.pixels)
assert len(mesh.uv_layers['DetailUV'].data)==0,'Fixture must reproduce Blender Edit Mode UV storage'
assert group_bake.mapped_faces(obj)=={0}
report=shared_detail.validate(scene)
assert report['objects']==[dict(name=obj.name,mappedFaces=1,emptyFaces=1)]
assert obj.mode=='EDIT' and capture()==before and list(mask.pixels)==pixels_before
print('PASS preflight reads unsaved BMesh UVs while ordinary UV data is empty',flush=True)

workflow.start(scene)
assert obj.mode=='EDIT' and capture()==before
process=workflow.JOB['process'];code=process.wait(timeout=90)
workflow.poll()
if bpy.app.timers.is_registered(workflow.poll):bpy.app.timers.unregister(workflow.poll)
if code!=0:raise AssertionError((workflow.JOB['directory']/'bake.log').read_text(encoding='utf-8',errors='replace'))
path,document=workflow.candidate()
assert len(document['assets'])==1 and document['groups'][0]['emissionIntensity']==8
assert obj.mode=='EDIT' and capture()==before and list(mask.pixels)==pixels_before
assert authoring.bake_module().sha(source)==source_hash
bpy.ops.wm.open_mainfile(filepath=str(source))
original=bpy.data.objects['Prop_EditModeBake'].data
assert all(tuple(item.uv)==(-1,-1) for item in original.uv_layers['DetailUV'].data)
print('PASS Edit Mode start -> isolated snapshot -> real bake -> candidate; live UV/selection/mask and saved work preserved',flush=True)
print('EDIT_MODE_BAKE_TESTS_COMPLETE',path,flush=True)
