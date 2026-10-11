"""Group models by their detail image, then use the actual character atlas baker.

Authoring: independent PaletteUV / DetailUV per mesh. Output: individual meshes
with UV0 referencing each group's shared BaseColor and SurfaceData, exactly the
character aoRimEmissionProfile16 contract. No per-model intermediate atlas tiles.
"""
import hashlib
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

import bpy
import bmesh
import numpy as np
from . import authoring, assembly, emission

FORMAT='mx.environmentSharedBakedSurface.v2'


def detail_image(obj):
    images=set()
    used={p.material_index for p in obj.data.polygons}
    for index in used:
        if index>=len(obj.data.materials):raise ValueError('材质槽无效：'+obj.name)
        material=obj.data.materials[index]
        if not material or not material.use_nodes:raise ValueError('Missing material: '+obj.name)
        for node in material.node_tree.nodes:
            if node.name==emission.NODE and material.get('wgame_environment_shared'):continue
            if node.type!='TEX_IMAGE' or not node.image:continue
            links=node.inputs['Vector'].links
            if len(links)==1 and links[0].from_node.type=='UVMAP' and links[0].from_node.uv_map=='DetailUV':
                images.add(node.image)
    if not images:
        if mapped_faces(obj):raise ValueError('存在已展开的 DetailUV，但材质未连接细节图：'+obj.name)
        # A constant Principled base needs no image. Keep authored material slots
        # and colors intact; only the isolated bake worker creates the atlas UV.
        if all(not hasattr(authoring.bake_module().source_base(obj.data.materials[i]),'node') for i in used):
            return None
        raise ValueError('没有细节图且底色并非纯色；请检查材质连接：'+obj.name)
    if len(images)>1:raise ValueError('模型实际引用多张细节图，请先拆分：'+obj.name+' ('+', '.join(sorted(i.name for i in images))+')')
    return next(iter(images))


def image_key(image):
    if image is None:return assembly.image_publish_key(None)
    path=bpy.path.abspath(image.filepath) if image.filepath else ''
    identity=str(image['wgame_detail_source_id']) if image.get('wgame_detail_source_id') else str(Path(path).resolve()) if path else image.name
    pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels)
    digest=hashlib.sha256()
    digest.update(json.dumps([identity,list(image.size),image.colorspace_settings.name]).encode())
    digest.update(pixels.tobytes())
    return digest.hexdigest()[:16]


def groups_for(meshes):
    groups=defaultdict(list);cache={}
    for obj in meshes:
        image=detail_image(obj)
        key=cache.setdefault(image,image_key(image)) if image not in cache else cache[image]
        groups[key].append(obj)
    result={}
    for key,objects in groups.items():
        masks={emission.mask_image(obj.data.materials[index]) for obj in objects
               for index in {face.material_index for face in obj.data.polygons}}-{None}
        if masks:
            for mask in masks:emission.validate_image(mask)
            mask_keys=sorted(image_key(mask)+':'+str(emission.intensity(mask)) for mask in masks)
            key=hashlib.sha256((key+':'+':'.join(mask_keys)).encode()).hexdigest()[:16]
        result[key]=objects
    return dict(sorted(result.items()))


def mapped_faces(obj):
    """Finite, non-degenerate authored islands may cross the CLIP image bounds."""
    mesh=obj.data
    if mesh.is_editmode:
        # MeshUVLoopLayer.data is empty in Edit Mode, even after
        # update_from_editmode(). Read the live BMesh, including unsaved UV edits.
        edit=bmesh.from_edit_mesh(mesh)
        edit.faces.ensure_lookup_table();edit.faces.index_update()
        uv=edit.loops.layers.uv.get('DetailUV')
        if uv is None:raise ValueError('Missing DetailUV: '+obj.name)
        faces=((face.index,[tuple(loop[uv].uv) for loop in face.loops]) for face in edit.faces)
    else:
        uv=mesh.uv_layers.get('DetailUV')
        if uv is None:raise ValueError('Missing DetailUV: '+obj.name)
        faces=((face.index,[tuple(uv.data[i].uv) for i in face.loop_indices]) for face in mesh.polygons)
    mapped=set()
    for index,values in faces:
        if all(p==(-1.,-1.) for p in values):continue
        # Blender may store infinity as the largest finite float32 UV.
        if not all(math.isfinite(c) and abs(c)<1e30 for p in values for c in p):
            raise ValueError('细节 UV 含无效数值，请整理或整面留空：'+obj.name)
        area=abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(values,values[1:]+values[:1])))*.5
        if area<1e-12:
            # A collapsed point strictly outside the CLIP image cannot sample
            # any detail. It is equivalent to an empty island for the bake,
            # even if UV editing moved it away from the (-1,-1) sentinel.
            if len(set(values))==1 and (values[0][0]<0 or values[0][0]>1 or
                                        values[0][1]<0 or values[0][1]>1):
                continue
            raise ValueError('细节 UV 存在退化面：'+obj.name)
        mapped.add(index)
    return mapped


def isolate_unmapped_palette(obj,cache):
    """An explicitly empty DetailUV cannot contribute. Simplify only that subset.

    This exposes the character baker's proven pure-palette fast path. Never infer
    that painted/mapped regions are empty from a low-resolution image probe.
    """
    active=mapped_faces(obj)
    source_slots=list(obj.data.materials)
    if not active and all(not hasattr(authoring.bake_module().source_base(source_slots[i]),'node')
                          for i in {p.material_index for p in obj.data.polygons}):
        obj['_environment_fast_bake_uv']=True
        return
    known=True
    for mat in {source_slots[p.material_index] for p in obj.data.polygons}:
        tree=mat.node_tree
        value=authoring.bake_module().source_base(mat)
        mix=tree.nodes.get('Mix_Detail');palette=tree.nodes.get('Palette_Texture');detail=tree.nodes.get('Detail_Texture')
        if not (mat.get('wgame_environment_shared') and hasattr(value,'node') and value.node==mix
                and mix.type=='MIX_RGB' and mix.blend_type=='MIX' and palette and detail and detail.extension=='CLIP'
                and len(mix.inputs[0].links)==1 and mix.inputs[0].links[0].from_node==detail
                and len(mix.inputs[1].links)==1 and mix.inputs[1].links[0].from_node==palette
                and len(mix.inputs[2].links)==1 and mix.inputs[2].links[0].from_node==detail):known=False
    obj['_environment_fast_bake_uv']=known
    if not known:return
    replacements={}
    for face in obj.data.polygons:
        if face.index in active:continue
        source=source_slots[face.material_index]
        if not source.get('wgame_environment_shared'):continue
        if source not in cache:
            material=source.copy();material.name=source.name+'_PaletteOnly'
            tree=material.node_tree
            bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
            palette=tree.nodes.get('Palette_Texture')
            if palette is None:raise ValueError('Missing palette source on '+obj.name)
            tree.links.new(palette.outputs['Color'],bsdf.inputs['Base Color'])
            emission.disable_on_empty(material)
            cache[source]=material
        material=cache[source]
        if source not in replacements:
            index=next((i for i,m in enumerate(obj.data.materials) if m==material),None)
            if index is None:index=len(obj.data.materials);obj.data.materials.append(material)
            replacements[source]=index
        face.material_index=replacements[source]


def bake_uv(obj,preserve_detail=True):
    """Keep authored islands. Temporary constant-color triangles need no unwrap."""
    if not obj.get('_environment_fast_bake_uv'):
        return authoring.bake_module().prepare_uv(obj,preserve_detail)
    mesh=obj.data;source=mesh.uv_layers['DetailUV']
    target=mesh.uv_layers.get('BakeUV') or mesh.uv_layers.new(name='BakeUV')
    protected=mapped_faces(obj) if preserve_detail else set()
    for face in mesh.polygons:
        if face.index in protected:
            for index in face.loop_indices:target.data[index].uv=source.data[index].uv
        else:
            # Distinct temporary charts prevent coincident edges from merging.
            # The character baker subsequently collapses identical constants.
            x=(face.index%256)*.05+2;y=(face.index//256)*.05+2
            for index,value in zip(face.loop_indices,((x,y),(x+.04,y),(x,y+.04))):
                target.data[index].uv=value
    mesh.uv_layers.active=target;target.active_render=True
    return protected


def export_one(obj,path,material):
    authoring.activate(obj)
    # Hair transport channels are character-specific; the static object needs UV0.
    while len(obj.data.uv_layers)>1:obj.data.uv_layers.remove(obj.data.uv_layers[-1])
    # Baking has already captured every authored surface in the shared textures.
    # Collapse face assignments in the isolated worker, otherwise FBX preserves
    # redundant submeshes even when Unity binds them to the same baked material.
    obj.data.materials.clear();obj.data.materials.append(material)
    for face in obj.data.polygons:face.material_index=0
    obj.data.update();obj.update_tag(refresh={'DATA'});bpy.context.view_layer.update()
    obj.location=(0,0,0)
    result=bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
        use_mesh_modifiers=False,mesh_smooth_type='FACE',colors_type='SRGB',use_tspace=True,
        use_triangles=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    if 'FINISHED' not in result:raise RuntimeError('FBX export failed: '+obj.name)


def run(output,bake_device='AUTO'):
    from .registration import require_registered
    require_registered(bpy.context.scene)
    output=Path(output).resolve()
    if output.exists() and any(output.iterdir()):raise ValueError('联合烘焙输出目录必须为空，避免混入旧分组')
    output.mkdir(parents=True,exist_ok=True)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('wgame_environment_asset')]
    if not meshes:raise ValueError('No environment assets')
    # Freeze static modifiers in the isolated worker before baking. Otherwise an
    # evaluated Geometry Nodes cache can resurrect the character-only UV channels
    # after they have been removed from the original mesh at export time.
    for obj in meshes:
        authoring.activate(obj)
        if obj.data.users>1:obj.data=obj.data.copy()
        for modifier in list(obj.modifiers):
            if modifier.show_render:
                modifier.show_viewport=True
                bpy.ops.object.modifier_apply(modifier=modifier.name)
            else:obj.modifiers.remove(modifier)
    groups=groups_for(meshes);records=[];assets=[]
    source_hash=authoring.bake_module().sha(bpy.data.filepath).split(':')[-1]
    for obj in bpy.context.scene.objects:obj.hide_render=True
    for key,objects in groups.items():
        path=output/key;path.mkdir(exist_ok=True)
        source_image=detail_image(objects[0]);image_name=source_image.name if source_image else ''
        publish_key=assembly.image_publish_key(source_image)
        emission_intensity=emission.group_intensity(objects)
        cache={}
        for obj in objects:
            mapped_faces(obj)
            obj.hide_render=False
            isolate_unmapped_palette(obj,cache)
            emission.prepare_bake(obj)
        print('GROUP_BAKE',key,len(objects),image_name,flush=True)
        result=authoring.bake_module().prepare(objects,None,path,key,uv_initializer=bake_uv,
                                                bake_device=bake_device,batch_bake=True)
        # The manifest owns the final texture binding; do not export authoring
        # materials and their PaletteUV / DetailUV texture references.
        export_material=bpy.data.materials.new('EnvironmentBaked_'+key)
        # Keep each original model independent; only textures and their UV space share ownership.
        for obj in objects:
            identifier=str(obj['wgame_environment_asset'])
            if not identifier.replace('_','').replace('-','').isalnum():raise ValueError('Unsafe asset identifier')
            relative=key+'/'+identifier+'.fbx'
            assembly_record=assembly.record(obj)
            export_one(obj,output/relative,export_material)
            assets.append(dict(id=identifier,group=key,model=relative,uv='UV0',assembly=assembly_record))
            obj.hide_render=True
        records.append(dict(id=key,publishKey=publish_key,sourceDetailImage=image_name,
            emissionIntensity=emission_intensity,
            models=[o['wgame_environment_asset'] for o in objects],
            baseColor=key+'/BaseColor.png',surfaceData=key+'/SurfaceData.png',
            surfaceDataLayout=result['surfaceDataLayout'],textureSize=result['textureSize'],
            atlasLayout=result['atlasLayout'],bakerVersion=result['toolVersion']))
    files=[dict(path=p.relative_to(output).as_posix(),sha256=authoring.bake_module().sha(p).split(':')[-1])
           for p in sorted(output.rglob('*')) if p.is_file() and p.suffix in ('.fbx','.png')]
    document=dict(format=FORMAT,source=str(Path(bpy.data.filepath).resolve()),sourceSha256=source_hash,
                  groups=records,assets=assets,files=files)
    (output/'environment_build.json').write_text(json.dumps(document,indent=2)+'\n',encoding='utf-8')
    print('GROUP_BUILD_READY',output,flush=True)
    return document
