"""Blender background integration check against the declared 000009 Work asset."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile

import bpy

root=Path(__file__).resolve().parents[4]
module_path=root/'Tools/Blender/WGamePaletteTools/baked_preview.py'
spec=importlib.util.spec_from_file_location('baked_preview_test_module',module_path)
preview=importlib.util.module_from_spec(spec);spec.loader.exec_module(preview)
source=root/'ArtSource/Characters/000009/Blender_Work/000009_work.blend'
source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source))
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
original_scene=bpy.context.scene;original_workspace=bpy.context.window.workspace
original_mesh=obj.data;original_materials=[s.material for s in obj.material_slots]
original_uvs={u.name:[tuple(v.uv) for v in u.data] for u in obj.data.uv_layers}
counts={kind:len(getattr(bpy.data,kind)) for kind in preview._kinds}
final=root/'ArtSource/Characters/000009/Final_Source'
bpy.ops.object.mode_set(mode='EDIT')
for _ in range(2):
    result=preview.open_preview(final,root)
    assert preview.active()
    assert bpy.context.scene!=original_scene
    assert bpy.context.scene.get(preview.TAG)
    assert bpy.context.mode=='EDIT_MESH'
    assert result['uv']=='UV0' and result['textureSize']==(512,512)
    mesh_obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    assert mesh_obj.data.attributes.get('WGamePreviewTangentSign')
    material=mesh_obj.data.materials[0]
    specular=material.node_tree.nodes['WGame Surface specular'].color_ramp
    assert abs(specular.elements[2].color[0]-.06)<1e-6
    assert abs(specular.elements[3].color[0]-.18)<1e-6
    assert any(link.from_node.name=='WGame Surface specular' and link.to_node.parent
               and link.to_node.parent.label.startswith('WGame 发流') for link in material.node_tree.links)
    assert material.node_tree.nodes['WGameHairPreviewEnabled'].outputs[0].default_value==0
    bsdf=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    assert bsdf.inputs['Specular IOR Level'].is_linked and bsdf.inputs['Subsurface Weight'].is_linked
    assert abs(bsdf.inputs['Subsurface Scale'].default_value-.015)<1e-6
    assert bpy.context.scene.view_settings.look==original_scene.view_settings.look
    assert all(not a.spaces.active.overlay.show_bones for a in bpy.context.screen.areas if a.type=='VIEW_3D')
    assert {n.uv_map for n in material.node_tree.nodes if n.type=='UVMAP'}>={mesh_obj.data.uv_layers[1].name,mesh_obj.data.uv_layers[2].name}
    assert bpy.context.scene.render.engine=='BLENDER_EEVEE'
    assert any(a.type=='IMAGE_EDITOR' and a.spaces.active.image and tuple(a.spaces.active.image.size)==(512,512) for a in bpy.context.screen.areas)
    preview.close_preview()
    assert not preview.active()
    assert bpy.context.scene==original_scene and bpy.context.window.workspace==original_workspace
    assert bpy.context.object==obj and bpy.context.mode=='EDIT_MESH'
    after={kind:len(getattr(bpy.data,kind)) for kind in preview._kinds}
    assert after==counts, (counts,after,[(m.name,m.users) for m in bpy.data.materials],[(i.name,i.users) for i in bpy.data.images])
preview.register()
try:
    preview.open_preview(final,root)
    material=next(o for o in bpy.context.scene.objects if o.type=='MESH').data.materials[0]
    bpy.ops.wgame.baked_hair_compare()
    assert material.node_tree.nodes['WGameHairPreviewEnabled'].outputs[0].default_value==1
    direction=material.node_tree.nodes['WGameHairPreviewLightDirection']
    before_direction=tuple(s.default_value for s in direction.inputs)
    bpy.ops.wgame.baked_hair_light()
    assert tuple(s.default_value for s in direction.inputs)!=before_direction
    bpy.ops.wgame.baked_hair_compare()
    assert material.node_tree.nodes['WGameHairPreviewEnabled'].outputs[0].default_value==0
    assert bpy.context.scene.view_settings.look==original_scene.view_settings.look
    with tempfile.TemporaryDirectory() as temp:
        path=Path(temp)/'save_from_preview.blend'
        bpy.ops.wm.save_as_mainfile(filepath=str(path),copy=True,check_existing=False)
        assert not preview.active()
        assert bpy.context.scene==original_scene
        assert not any(s.get(preview.TAG) for s in bpy.data.scenes)
        bpy.ops.wm.open_mainfile(filepath=str(path))
        assert not any(s.get(preview.TAG) for s in bpy.data.scenes)
finally:
    preview.unregister()
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
if obj.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
assert {u.name:[tuple(v.uv) for v in u.data] for u in obj.data.uv_layers}==original_uvs
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
print('BAKED_PREVIEW_PASS: repeated toggle, UV0/512 image, return to Edit Mode, no leaked data, safe save, unchanged Work file')
