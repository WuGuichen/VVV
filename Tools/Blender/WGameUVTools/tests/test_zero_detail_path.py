"""Complete native route from blank/new working UV, with no formal UV or detail node."""
import bpy,bmesh,sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
bpy.types.Scene.wgame_face_detail_texture_path=bpy.props.StringProperty(subtype='FILE_PATH')
test_root=Path(__file__).resolve().parents[2]
png=test_root/'explicit-detail.png'
if not png.exists():
    image=bpy.data.images.new('Zero Path PNG',width=512,height=512,alpha=True)
    image.filepath_raw=str(png);image.file_format='PNG';image.save();bpy.data.images.remove(image)
results=[]
for arbitrary in (None,'用户新建的眼部坐标'):
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.mesh.primitive_plane_add()
    obj=bpy.context.object;mesh=obj.data
    mesh.uv_layers.active.name='UV0';mesh.uv_layers.new(name='PaletteUV')
    protected={u.name:[tuple(l.uv) for l in u.data] for u in mesh.uv_layers}
    material=bpy.data.materials.new('Original zero-path material');material.use_nodes=True
    mesh.materials.clear();mesh.materials.append(material)
    assert material.node_tree.nodes.get('Detail_UV') is None
    assert mesh.uv_layers.get('DetailUV') is None
    bpy.ops.object.mode_set(mode='EDIT')
    assert bpy.ops.wgame_uv.face_authoring(action='NEW')=={'FINISHED'}
    if arbitrary:
        bpy.ops.object.mode_set(mode='OBJECT');mesh.uv_layers.active.name=arbitrary;bpy.ops.object.mode_set(mode='EDIT')
    work=mesh.uv_layers.active.name
    bm=bmesh.from_edit_mesh(mesh);uv=bm.loops.layers.uv.active
    assert all(tuple(l[uv].uv)==(-1.,-1.) for f in bm.faces for l in f.loops)
    area=next((a for a in bpy.context.screen.areas if a.type=='IMAGE_EDITOR'),bpy.context.screen.areas[0])
    area.type='IMAGE_EDITOR';area.ui_type='UV'
    if not any(a.type=='VIEW_3D' for a in bpy.context.screen.areas):
        viewport=next(a for a in bpy.context.screen.areas if a!=area and a.type=='PROPERTIES')
        viewport.type='VIEW_3D'
    area.spaces.active.use_image_pin=True
    settings=bpy.context.scene.wgame_uv_tools
    settings.uv_map='';settings.face_unwrap_mode='CONTIGUOUS' if 'CONTIGUOUS' in [i.identifier for i in settings.bl_rna.properties['face_unwrap_mode'].enum_items] else settings.face_unwrap_mode
    assert bpy.ops.wgame_uv.face_authoring(action='CREATE')=={'FINISHED'}
    bm=bmesh.from_edit_mesh(mesh);uv=bm.loops.layers.uv.active
    coords=[tuple(l[uv].uv) for f in bm.faces for l in f.loops]
    assert all(0<=x<=1 and 0<=y<=1 for x,y in coords),coords
    bpy.context.scene.wgame_face_detail_texture_path=str(png)
    assert bpy.ops.wgame_uv.detail_lifecycle('INVOKE_DEFAULT',action='PROMOTE')=={'FINISHED'}
    assert mesh.uv_layers.active.name=='DetailUV'
    bm=bmesh.from_edit_mesh(mesh);uv=bm.loops.layers.uv.active
    assert [tuple(l[uv].uv) for f in bm.faces for l in f.loops]==coords
    for name,values in protected.items():
        layer=bm.loops.layers.uv[name]
        assert [tuple(l[layer].uv) for f in bm.faces for l in f.loops]==values
    assert obj.active_material.node_tree.nodes['Detail_UV'].uv_map=='DetailUV'
    target=area.spaces.active.image
    assert target and Path(target.filepath).resolve()==png.resolve()
    assert area.spaces.active.use_image_pin
    assert bpy.ops.wgame_uv.enter_paint()=={'FINISHED'}
    assert bpy.context.mode=='PAINT_TEXTURE'
    assert bpy.context.scene.tool_settings.image_paint.canvas==target
    # Background mode does not dispatch the interactive workspace timer.
    # Exercise its existing destination-view function in the isolated scene.
    addon.paint_workflow.configure_paint_view(bpy.context.window,target,None)
    assert any(a.spaces.active.shading.type=='MATERIAL' for a in bpy.context.screen.areas if a.type=='VIEW_3D')
    results.append({'startUV':work,'noOldFormalUV':True,'noOldDetailNode':True,'nativeCreate':True,'nativeInvokePromotion':True,'coordinatesPreserved':True,'protectedUVsPreserved':True,'pngSynchronizedWithPin':True,'enteredPaintPreview':True})
(test_root/'zero-path-test-result.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS COMPLETE ZERO PATHS',json.dumps(results,ensure_ascii=False))
