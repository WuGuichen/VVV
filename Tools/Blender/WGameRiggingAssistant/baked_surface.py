"""Bake layered WGame authoring materials in an isolated Blender export process.

Only prepare() mutates meshes/materials, and callers must use an export snapshot.
The source scene is never saved by the worker.
"""
import hashlib
import importlib.util
import json
import math
import runpy
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

FORMAT = 'mx.characterBakedSurfaceBinding.v1'
BINDING = 'character_baked_surface_binding.json'
VERSION = '1.7.1'
HAIR_DEFAULTS = {'primaryWidth':0.32,'primaryStrength':0.45,'secondaryWidth':0.5,
                 'secondaryStrength':0.16,'primaryShift':0.08,'secondaryShift':-0.16,
                 'rootDarkening':0.18,'rimStrength':0.12,'aoStrength':0.5}


def hair_module():
    path = Path(__file__).parent.parent/'WGamePaletteTools'/'hair_flow.py'
    spec = importlib.util.spec_from_file_location('wgame_export_hair_flow',path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha(path):
    return 'sha256:'+hashlib.sha256(Path(path).read_bytes()).hexdigest()


def snapshot(path, texture_size=None):
    """Pack copies of in-memory images for the snapshot, restoring every source link."""
    swapped, copies, preview_slots = [], [], []
    scene=bpy.context.scene
    previous_size=scene.get('wgame_bake_output_size')
    previous_origin={key:scene.get(key) for key in ('wgame_bake_authoring_path','wgame_bake_authoring_hash')}
    try:
        scene['wgame_bake_output_size']=str(texture_size or getattr(scene,'wgame_bake_texture_size','AUTO'))
        origin=Path(bpy.data.filepath) if bpy.data.filepath else None
        scene['wgame_bake_authoring_path']=str(origin) if origin else ''
        scene['wgame_bake_authoring_hash']=sha(origin) if origin and origin.is_file() else ''
        for obj in bpy.context.scene.objects:
            if obj.type == 'MESH' and obj.mode == 'EDIT':
                obj.update_from_editmode()
            if obj.type == 'MESH':
                for slot in obj.material_slots:
                    if slot.material and slot.material.get('wgame_hair_preview_source'):
                        original=bpy.data.materials.get(slot.material['wgame_hair_preview_source'])
                        if original is None:raise ValueError('Hair preview source material is missing')
                        preview_slots.append((slot,slot.material));slot.material=original
        # Old imported slots and orphan materials can retain missing palette files.
        # Only materials assigned to faces participate in this scene's bake.
        trees, seen_trees = [], set()
        def include_tree(tree):
            if tree is None or tree.as_pointer() in seen_trees:return
            seen_trees.add(tree.as_pointer());trees.append(tree)
            for node in tree.nodes:
                if node.type == 'GROUP':include_tree(node.node_tree)
        for obj in scene.objects:
            if obj.type != 'MESH':continue
            for index in {p.material_index for p in obj.data.polygons}:
                if index >= len(obj.material_slots):continue
                material=obj.material_slots[index].material
                if material and material.use_nodes:include_tree(material.node_tree)
        replacements = {}
        for tree in trees:
            for node in tree.nodes:
                if node.type != 'TEX_IMAGE' or not node.image or node.image.source not in {'FILE','GENERATED'}:
                    continue
                source = node.image
                if source not in replacements:
                    if source.size[0] == 0 or source.size[1] == 0:
                        raise ValueError(f'Missing source image: {source.name}')
                    copy = source.copy()
                    copies.append(copy)
                    # Image.copy alone does not copy unsaved paint pixels reliably.
                    pixels = np.empty(len(source.pixels),dtype=np.float32)
                    source.pixels.foreach_get(pixels)
                    copy.pixels.foreach_set(pixels)
                    copy.pack()
                    replacements[source] = copy
                swapped.append((node,source))
                node.image = replacements[source]
        Path(path).parent.mkdir(parents=True,exist_ok=True)
        result = bpy.ops.wm.save_as_mainfile(filepath=str(path),copy=True,check_existing=False)
        if 'FINISHED' not in result:
            raise RuntimeError('Could not save export snapshot')
    finally:
        for key,value in previous_origin.items():
            if value is None:
                if key in scene:del scene[key]
            else:scene[key]=value
        if previous_size is None:
            if 'wgame_bake_output_size' in scene:del scene['wgame_bake_output_size']
        else:scene['wgame_bake_output_size']=previous_size
        for slot,material in preview_slots:slot.material=material
        for node,source in swapped:
            node.image = source
        for image in copies:
            bpy.data.images.remove(image)


def select(obj):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def source_base(material):
    if not material or not material.use_nodes:
        raise ValueError('Baking requires a node material with a Principled base-color output')
    outputs = [n for n in material.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output]
    links = outputs[0].inputs['Surface'].links if len(outputs)==1 else []
    if len(links)!=1 or links[0].from_node.type!='BSDF_PRINCIPLED':
        raise ValueError(f'{material.name}: connect final color layers to one Principled BSDF Base Color')
    socket = links[0].from_node.inputs['Base Color']
    return socket.links[0].from_socket if socket.is_linked else tuple(socket.default_value)


def prepare_uv(obj, preserve_detail=True):
    """Preserve existing valid detail islands; unwrap only unallocated faces."""
    mesh = obj.data
    detail = (mesh.uv_layers.get('DetailUV') or mesh.uv_layers.get('FaceDetailUV')) if preserve_detail else None
    layer = mesh.uv_layers.get('BakeUV') or mesh.uv_layers.new(name='BakeUV')
    mesh.uv_layers.active = layer
    layer.active_render = True
    preserve = set()
    if detail:
        for p in mesh.polygons:
            uv = [detail.data[i].uv.copy() for i in p.loop_indices]
            area = abs(sum(a.x*b.y-a.y*b.x for a,b in zip(uv,uv[1:]+uv[:1])))*0.5
            # Authored UVs may cross 0..1 with a repeating image sampler.
            # Preserve them too: an apparently flat low-resolution probe must
            # not erase paint that falls between its samples.
            if area>1e-12 and all(math.isfinite(c) for point in uv for c in point):
                preserve.add(p.index)
                for i in p.loop_indices:
                    layer.data[i].uv = detail.data[i].uv
    select(obj)
    bpy.context.scene.tool_settings.mesh_select_mode=(False,False,True)
    for v in mesh.vertices:v.select=False
    for e in mesh.edges:e.select=False
    for p in mesh.polygons:
        p.select = p.index not in preserve
    if len(preserve)<len(mesh.polygons):
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(89),island_margin=0.005)
        bpy.ops.object.mode_set(mode='OBJECT')
        # Restore source islands explicitly; selection synchronization must never
        # let an unwrap operation change an authored detail chart.
        layer=mesh.uv_layers['BakeUV']
        detail=mesh.uv_layers.get('DetailUV') or mesh.uv_layers.get('FaceDetailUV')
        for p in mesh.polygons:
            if p.index in preserve:
                for i in p.loop_indices:layer.data[i].uv=detail.data[i].uv
            else:
                for i in p.loop_indices:
                    layer.data[i].uv *= 0.1
    for p in mesh.polygons:
        p.select = True
    return preserve


def pixels(image):
    result = np.empty(len(image.pixels),dtype=np.float32)
    image.pixels.foreach_get(result)
    return result.reshape((-1,4))


def constant_face_key(obj, face, varying_uv=None):
    """Prove albedo and emission are constant, or the same function of varying_uv.

    Color inputs must be constant; continuous controls must produce the same
    stored byte. Position/noise/group graphs remain unique. The caller excludes
    hair from per-face constant tiling to retain coherent hair charts.
    """
    mat=obj.material_slots[face.material_index].material
    value=source_base(mat)
    visited=set(); stack=[value.node] if hasattr(value,'node') else []
    output=next(n for n in mat.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
    strength=output.inputs['Surface'].links[0].from_node.inputs['Emission Strength']
    if strength.is_linked:stack.append(strength.links[0].from_node)
    key=[mat.name]
    allowed={'TEX_IMAGE','UVMAP','RGB','VALUE','MIX_RGB','MIX','VERTEX_COLOR','VALTORGB','MATH','SEPARATE_COLOR','COMBINE_COLOR'}
    while stack:
        n=stack.pop()
        if n in visited:continue
        visited.add(n)
        if n.type not in allowed:return None
        # Box projection also depends on the surface normal; equal UVs alone
        # cannot prove that two faces sample the same image function.
        if n.type=='TEX_IMAGE' and (not n.inputs['Vector'].is_linked or n.projection!='FLAT'):return None
        if n.type=='TEX_IMAGE' and n.interpolation=='Closest' and n.projection=='FLAT' and n.image:
            # Float roundoff in PaletteUV is harmless if every corner samples
            # the same nearest texel. Prove this per image consumer; the same
            # UV used by a linear sampler must still pass the exact check below.
            source=n.inputs['Vector'].links[0].from_node
            if source.type=='UVMAP':
                layer=obj.data.uv_layers.get(source.uv_map)
                if layer is None:return None
                values=[tuple(layer.data[i].uv) for i in face.loop_indices]
                width,height=n.image.size
                if source.uv_map!=varying_uv and width>0 and height>0 and all(0<=c<1 for uv in values for c in uv):
                    texels={(math.floor(u*width),math.floor(v*height)) for u,v in values}
                    if len(texels)==1:
                        key.append((n.name,source.uv_map,next(iter(texels))))
                        continue
        if n.type=='UVMAP':
            layer=obj.data.uv_layers.get(n.uv_map)
            if layer is None or n.from_instancer:return None
            if n.uv_map==varying_uv:
                key.append(('varyingDetailUV',n.uv_map))
                continue
            values=[tuple(layer.data[i].uv) for i in face.loop_indices]
            if len(set(values))!=1:return None
            key.append((n.uv_map,values[0]))
        if n.type=='VERTEX_COLOR':
            layer=obj.data.color_attributes.get(n.layer_name)
            if layer is None or layer.domain!='CORNER':return None
            values=[tuple(layer.data[i].color) for i in face.loop_indices]
            if len(set(values))!=1:return None
            key.append((n.layer_name,values[0]))
        stack.extend(link.from_node for socket in n.inputs for link in socket.links)
    surface=obj.data.color_attributes['BakedSurfaceSource']
    # These are the actual non-hair control outputs. Treat tiny interpolation
    # errors as equal only when the complete range rounds to the same PNG byte.
    # Do not apply this quantization to vertex colors feeding the albedo graph.
    values=[]
    for i in face.loop_indices:
        r,g,b,a=surface.data[i].color
        if strength.is_linked:
            # The verified graph may vary with DetailUV or amplify its input.
            # Keep the vertex multiplier exact; quantizing it before the product
            # could incorrectly merge different emission results.
            values.append(tuple(round(max(0,min(1,v))*255) for v in (r,g,a))+(b,))
        else:
            values.append(tuple(round(max(0,min(1,v))*255) for v in (r,g,b*strength.default_value,a)))
    if len(set(values))!=1:return None
    key.append(values[0])
    return tuple(key)


def detail_reuse_keys(obj, protected):
    """Prove the same material function of DetailUV, not matching probe pixels.

    Other graph inputs and all control channels must be constant on a face.
    Unknown/context-dependent material graphs remain independent.
    """
    mesh=obj.data
    detail=mesh.uv_layers.get('DetailUV') or mesh.uv_layers.get('FaceDetailUV')
    if detail is None:return {}
    hair=mesh.attributes.get('WGameBakeHair')
    result={}
    for index in sorted(protected):
        key=constant_face_key(obj,mesh.polygons[index],detail.name)
        # Hair's geometric AO and ordinary authored AO have different semantics.
        if key is not None and hair and hair.data[index].value>.5:key+=('shared-hair-ao',)
        result[index]=key
    return result


def ensure_chart_footprints(obj,excluded,preserved,minimum=.025):
    """Reserve real texel footprints for plain charts, including low-resolution exports."""
    from collections import defaultdict
    m=obj.data;uv=m.uv_layers['BakeUV'];edges=defaultdict(list)
    faces=[p for p in m.polygons if p.index not in excluded]
    for p in faces:
        ids=list(p.loop_indices)
        for a,b in zip(ids,ids[1:]+ids[:1]):
            key=tuple(sorted(((m.loops[a].vertex_index,tuple(round(c,6) for c in uv.data[a].uv)),(m.loops[b].vertex_index,tuple(round(c,6) for c in uv.data[b].uv)))))
            edges[key].append(p.index)
    adjacent=defaultdict(set)
    for group in edges.values():
        for f in group:adjacent[f].update(group)
    remaining={p.index for p in faces}
    while remaining:
        todo=[min(remaining)];remaining.remove(todo[0]);chart=[]
        while todo:
            f=todo.pop();chart.append(f)
            for n in adjacent[f]:
                if n in remaining:remaining.remove(n);todo.append(n)
        if any(f in preserved for f in chart):continue
        loops=[i for f in chart for i in m.polygons[f].loop_indices]
        low=Vector((min(uv.data[i].uv.x for i in loops),min(uv.data[i].uv.y for i in loops)))
        extent=Vector((max(uv.data[i].uv.x for i in loops),max(uv.data[i].uv.y for i in loops)))-low
        scale=Vector((max(1,minimum/max(1e-8,extent.x)),max(1,minimum/max(1e-8,extent.y))))
        for i in loops:
            value=uv.data[i].uv-low
            uv.data[i].uv=(low.x+value.x*scale.x,low.y+value.y*scale.y)


def make_image(name,size,linear=False):
    image = bpy.data.images.new(name,width=size,height=size,alpha=True,float_buffer=True)
    image.colorspace_settings.name = 'Non-Color' if linear else 'sRGB'
    # Control alpha stores a surface profile, not opacity.
    if linear:image.alpha_mode='CHANNEL_PACKED'
    image.generated_color = (0,0,0,1)
    return image


def raster_face_labels(meshes,size,margin,face_labels=None,background=0):
    """Raster face-owned labels without lighting, rays or category averaging."""
    ids=np.full((size,size),-1,dtype=np.int32)
    seen=set()
    for obj in meshes:
        m=obj.data;uv=m.uv_layers['BakeUV'];data=m.color_attributes['BakedSurfaceSource']
        m.calc_loop_triangles()
        for tri in m.loop_triangles:
            p=np.array([uv.data[i].uv[:] for i in tri.loops],dtype=np.float64)*size
            profile=(face_labels[obj.name][tri.polygon_index] if face_labels is not None
                     else min(15,int(data.data[tri.loops[0]].color[3]*16)))
            key=(tuple(p.ravel()),profile)
            if key in seen:continue
            seen.add(key)
            low=np.maximum(0,np.floor(p.min(axis=0)).astype(int));high=np.minimum(size-1,np.ceil(p.max(axis=0)).astype(int))
            if np.any(high<low):continue
            x,y=np.meshgrid(np.arange(low[0],high[0]+1)+.5,np.arange(low[1],high[1]+1)+.5)
            e=p[1]-p[0];f=p[2]-p[0];det=e[0]*f[1]-e[1]*f[0]
            if abs(det)<1e-12:continue
            dx,dy=x-p[0,0],y-p[0,1]
            a=(dx*f[1]-dy*f[0])/det;b=(e[0]*dy-e[1]*dx)/det
            covered=(a>=-1e-6)&(b>=-1e-6)&(a+b<=1.000001)
            view=ids[low[1]:high[1]+1,low[0]:high[0]+1]
            view[covered]=profile
            if not np.any(covered):
                center=np.clip(np.floor(p.mean(axis=0)).astype(int),0,size-1)
                if ids[center[1],center[0]]<0:ids[center[1],center[0]]=profile
    for _ in range(margin+1):
        old=ids.copy()
        for dy,dx in ((0,1),(0,-1),(1,0),(-1,0)):
            shifted=np.roll(old,(dy,dx),(0,1))
            if dy==1:shifted[0,:]=-1
            if dy==-1:shifted[-1,:]=-1
            if dx==1:shifted[:,0]=-1
            if dx==-1:shifted[:,-1]=-1
            mask=(ids<0)&(shifted>=0);ids[mask]=shifted[mask]
    ids[ids<0]=background
    return ids.ravel()


def raster_profiles(meshes,size,margin):
    return raster_face_labels(meshes,size,margin)


def write_palette_base(meshes,image,face_colors,size):
    palette=runpy.run_path(str(Path(__file__).with_name('baked_palette.py')))
    colors=sorted({color for values in face_colors.values() for color in values.values()})
    indices={color:i for i,color in enumerate(colors)}
    labels={name:{face:indices[color] for face,color in values.items()} for name,values in face_colors.items()}
    ids=raster_face_labels(meshes,size,int(size/128),labels,background=-1)
    rgba=np.zeros((size*size,4),dtype=np.float32);rgba[:,3]=1
    valid=ids>=0;rgba[valid,:3]=palette['srgb_pixels'](colors)[ids[valid]]
    image.pixels.foreach_set(rgba.ravel())


def save_png(image,path):
    image.filepath_raw = str(path)
    image.file_format = 'PNG'
    if image.alpha_mode!='CHANNEL_PACKED' or not image.is_float:
        image.save()
        return
    # Blender unpremultiplies float buffers on PNG save even in CHANNEL_PACKED
    # mode. An 8-bit staging image preserves the four independent data channels.
    packed=bpy.data.images.new(image.name+'_PackedPNG',width=image.size[0],height=image.size[1],alpha=True)
    try:
        packed.colorspace_settings.name='Non-Color';packed.alpha_mode='CHANNEL_PACKED'
        packed.pixels.foreach_set(pixels(image).ravel())
        packed.filepath_raw=str(path);packed.file_format='PNG';packed.save()
    finally:bpy.data.images.remove(packed)


def connect(tree,value,socket):
    if hasattr(value,'node'):
        tree.links.new(value,socket)
    else:
        socket.default_value = value


def configure_bake_device(scene,preferred):
    """The isolated worker starts with factory preferences, so select its device here."""
    scene.cycles.device='CPU'
    if preferred not in {'CPU','GPU'}:raise ValueError('Unsupported bake device: '+str(preferred))
    if preferred=='CPU':
        print('BAKE_DEVICE CPU',flush=True)
        return
    preferences=bpy.context.preferences.addons.get('cycles')
    if preferences:
        for backend in ('OPTIX','CUDA'):
            try:
                devices=preferences.preferences
                devices.compute_device_type=backend
                devices.get_devices()
                matching=[device for device in devices.devices if device.type==backend]
                if not matching:continue
                for device in devices.devices:device.use=device.type==backend
                scene.cycles.device='GPU'
                print('BAKE_DEVICE',backend,','.join(device.name for device in matching),flush=True)
                return
            except (RuntimeError,TypeError,ValueError) as error:
                print('BAKE_DEVICE_UNAVAILABLE',backend,str(error),flush=True)
    print('BAKE_DEVICE CPU (no usable NVIDIA GPU)',flush=True)


def bake_materials(meshes,image,kind,height,settings):
    """Bake non-overlapping UVs from one group in a single Cycles submission."""
    saved={obj:[(slot,slot.material) for slot in obj.material_slots] for obj in meshes}
    try:
        for obj in meshes:
            for slot,source in saved[obj]:
                mat=source.copy()
                slot.material=mat
                tree=mat.node_tree
                output=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
                original_bsdf=output.inputs['Surface'].links[0].from_node
                if kind=='color':
                    value=source_base(mat)
                else:
                    attr=tree.nodes.new('ShaderNodeVertexColor')
                    attr.layer_name='BakedSurfaceSource'
                    value=attr.outputs['Alpha'] if kind=='profile' else attr.outputs['Color']
                    if kind=='control':
                        separate=tree.nodes.new('ShaderNodeSeparateColor')
                        tree.links.new(value,separate.inputs['Color'])
                        ao=tree.nodes.new('ShaderNodeAmbientOcclusion')
                        ao.inputs['Distance'].default_value=height*0.02
                        ao.samples=32
                        hair=tree.nodes.new('ShaderNodeAttribute')
                        hair.attribute_name='WGameBakeHair'
                        factor=tree.nodes.new('ShaderNodeMath');factor.operation='MULTIPLY'
                        tree.links.new(hair.outputs['Fac'],factor.inputs[0]);factor.inputs[1].default_value=settings['aoStrength']
                        mix=tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX'
                        tree.links.new(factor.outputs[0],mix.inputs[0]);mix.inputs[1].default_value=(1,1,1,1)
                        tree.links.new(ao.outputs['AO'],mix.inputs[2])
                        mul=tree.nodes.new('ShaderNodeMath');mul.operation='MULTIPLY'
                        tree.links.new(separate.outputs[0],mul.inputs[0]);tree.links.new(mix.outputs[0],mul.inputs[1])
                        combine=tree.nodes.new('ShaderNodeCombineColor')
                        tree.links.new(mul.outputs[0],combine.inputs[0])
                        tree.links.new(separate.outputs[1],combine.inputs[1])
                        emission=tree.nodes.new('ShaderNodeMath');emission.operation='MULTIPLY';emission.use_clamp=True
                        tree.links.new(separate.outputs[2],emission.inputs[0])
                        strength=original_bsdf.inputs['Emission Strength']
                        connect(tree,strength.links[0].from_socket if strength.is_linked else float(strength.default_value),emission.inputs[1])
                        tree.links.new(emission.outputs[0],combine.inputs[2])
                        value=combine.outputs[0]
                emit=tree.nodes.new('ShaderNodeEmission')
                connect(tree,value,emit.inputs['Color'])
                emit.inputs['Strength'].default_value=1
                tree.links.new(emit.outputs[0],output.inputs['Surface'])
                target=tree.nodes.new('ShaderNodeTexImage');target.image=image
                tree.nodes.active=target
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.hide_set(False)
            obj.select_set(True)
        bpy.context.view_layer.objects.active=meshes[0]
        bpy.ops.object.bake(type='EMIT',use_clear=False,margin=int(image.size[0]/128),margin_type='EXTEND',uv_layer='BakeUV')
    finally:
        for entries in saved.values():
            for slot,source in entries:
                temp=slot.material
                if temp==source:continue
                slot.material=source
                bpy.data.materials.remove(temp)


def bake_material(obj,image,kind,height,settings):
    bake_materials([obj],image,kind,height,settings)


def detail_resolution(meshes,preserved):
    required=512.0
    for obj in meshes:
        mesh=obj.data
        detail=mesh.uv_layers.get('DetailUV') or mesh.uv_layers.get('FaceDetailUV')
        if not detail:continue
        source_size=max([max(n.image.size) for s in obj.material_slots for n in s.material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'detail' in n.name.lower()]+[512])
        target=mesh.uv_layers['BakeUV']
        for face in mesh.polygons:
            if face.index not in preserved[obj.name]:continue
            loops=list(face.loop_indices)
            for a,b in zip(loops,loops[1:]+loops[:1]):
                old_span=(detail.data[a].uv-detail.data[b].uv).length
                new_span=(target.data[a].uv-target.data[b].uv).length
                if old_span<=1e-7:continue
                if new_span<=1e-10:raise ValueError(f'{obj.name}: degenerate detail UV at face {face.index}')
                required=max(required,source_size*old_span/new_span)
    return required


def prepare(meshes,rig,output,package_id,uv_initializer=None,bake_device='CPU',batch_bake=False):
    output=Path(output)
    output.mkdir(parents=True,exist_ok=True)
    hf=hair_module()
    atlas=runpy.run_path(str(Path(__file__).with_name('baked_atlas.py')))
    palette=runpy.run_path(str(Path(__file__).with_name('baked_palette.py')))
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=16
    scene.cycles.seed=0
    scene.render.bake.use_selected_to_active=False
    settings=dict(HAIR_DEFAULTS)
    settings.update({k:float(scene.get('wgame_hair_'+k,v)) for k,v in HAIR_DEFAULTS.items()})
    height=max(o.dimensions.z for o in meshes)
    preserved={}
    duplicates={}
    report=[]
    face_colors={}
    flat_color_faces={}
    for obj in meshes:
        select(obj)
        for slot in obj.material_slots:
            source_base(slot.material)
        # Triangulation is fixed before tangent construction, including the FBX roundtrip.
        mod=obj.modifiers.new('BakedSurfaceTriangulate','TRIANGULATE')
        if hasattr(mod,'keep_custom_normals'):mod.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mesh=obj.data
        flags=hf.region(mesh)
        if not mesh.has_custom_normals:
            for face,enabled in zip(mesh.polygons,flags):
                if enabled:face.use_smooth=True
            mesh.update()
        report.append({'mesh':obj.name,**hf.generate(obj,rig,scene.objects)})
        bake_hair=hf.attribute(mesh,'WGameBakeHair','FLOAT','FACE')
        for face,flag in zip(mesh.polygons,flags):bake_hair.data[face.index].value=1.0 if flag else 0.0
        src=mesh.color_attributes.get('PaletteData')
        if not src:raise ValueError(f'{obj.name}: missing PaletteData')
        data=mesh.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        profile_values=hf.profiles(mesh)
        for p,flag in zip(mesh.polygons,flags):
            for i in p.loop_indices:
                color=list(src.data[i].color)
                profile=profile_values[p.index]
                color[3]=(8+16*profile)/255
                data.data[i].color=color
        face_colors[obj.name]=palette['read_palette_faces'](obj)
        flat_color_faces[obj.name]=set(face_colors[obj.name]) if face_colors[obj.name] is not None else set()
        initialize_uv=prepare_uv if uv_initializer is None else uv_initializer
        preserved[obj.name]=initialize_uv(obj,preserve_detail=face_colors[obj.name] is None)
        hair_unwrap=atlas['unwrap_hair'](obj,flags,preserved[obj.name],face_colors[obj.name])
        report[-1]['unwrap']=hair_unwrap
        representatives={}; copies={}
        for p,flag in zip(mesh.polygons,flags):
            if flag or p.index in preserved[obj.name]:continue
            key=constant_face_key(obj,p)
            if key is not None:
                if key in representatives:copies[p.index]=representatives[key]
                else:representatives[key]=p.index
        duplicates[obj.name]=copies
        # A face-constant triangle may be physically tiny. Allocate it a proper
        # atlas tile rather than preserving its geometric UV area.
        for original in representatives.values():
            p=mesh.polygons[original]
            for i,value in zip(p.loop_indices,((0,0),(.04,0),(0,.04))):mesh.uv_layers['BakeUV'].data[i].uv=value
        ensure_chart_footprints(obj,copies,preserved[obj.name])
        print('BAKE_UV_TILES',obj.name,'constantDuplicates',len(copies),'detailFaces',len(preserved[obj.name]),flush=True)
    direct_palette=all(values is not None for values in face_colors.values())
    # A direct palette only needs two small control bakes. GPU setup costs more
    # than it saves there; keep an explicit GPU choice for heavier detail groups.
    selected_device=('CPU' if direct_palette else 'GPU') if bake_device=='AUTO' else bake_device
    configure_bake_device(scene,selected_device)
    # One shared atlas for all body meshes, with no UV island overlap.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    for obj in meshes:
        for v in obj.data.vertices:v.select=False
        for e in obj.data.edges:e.select=False
        for p in obj.data.polygons:
            p.hide=p.index in duplicates[obj.name]
            p.select=not p.hide
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True,rotate_method='ANY',shape_method='CONCAVE',margin_method='FRACTION',margin=16/2048)
    bpy.ops.object.mode_set(mode='OBJECT')
    for obj in meshes:
        for p in obj.data.polygons:p.hide=False
        uv=obj.data.uv_layers['BakeUV']
        for duplicate,original in duplicates[obj.name].items():
            for target,source in zip(obj.data.polygons[duplicate].loop_indices,obj.data.polygons[original].loop_indices):
                uv.data[target].uv=uv.data[source].uv
    required=detail_resolution(meshes,preserved)
    requested=str(scene.get('wgame_bake_output_size',getattr(scene,'wgame_bake_texture_size','AUTO')))
    if requested not in {'AUTO','512','1024','2048','4096'}:raise ValueError('Unsupported baked texture size')
    size=(512 if direct_palette else 2048 if required<=2048.01 else 4096) if requested=='AUTO' else int(requested)
    probe_uvs={o.name:[v.uv[:] for v in o.data.uv_layers['BakeUV'].data] for o in meshes}
    for attempt in range(2):
        color=make_image('WGameBakedBaseColor',size)
        control=make_image('WGameBakedSurfaceData',size,True)
        if batch_bake:
            if not direct_palette:bake_materials(meshes,color,'color',height,settings)
            bake_materials(meshes,control,'control',height,settings)
        else:
            for obj in meshes:
                if not direct_palette:bake_material(obj,color,'color',height,settings)
                bake_material(obj,control,'control',height,settings)
        if direct_palette:write_palette_base(meshes,color,face_colors,size)
        packed=pixels(control);packed[:,3]=(8+16*raster_profiles(meshes,size,int(size/128)))/255
        reuse_keys={obj.name:detail_reuse_keys(obj,preserved[obj.name]) for obj in meshes}
        layout=atlas['compact'](meshes,duplicates,pixels(color),packed,size,preserved,reuse_keys,flat_color_faces,color_is_srgb=direct_palette)
        required=detail_resolution(meshes,preserved)
        if requested=='AUTO' and required>size+.01:
            if size==4096:raise ValueError(f'Final detail density requires {math.ceil(required)} px; choose an explicit size to downsample')
            bpy.data.images.remove(color);bpy.data.images.remove(control);size=4096
            for obj in meshes:
                for item,value in zip(obj.data.uv_layers['BakeUV'].data,probe_uvs[obj.name]):item.uv=value
            continue
        break
    print('BAKE_ATLAS_COMPACT',json.dumps(layout['stats']),flush=True)
    # Re-evaluate the source graph at the final layout, rather than enlarging an
    # already downsampled detail image. The Work scene is still untouched.
    for image in (color,control):
        cleared=np.zeros((size*size,4),dtype=np.float32);cleared[:,3]=1
        image.pixels.foreach_set(cleared.ravel())
    if batch_bake:
        if not direct_palette:bake_materials(meshes,color,'color',height,settings)
        bake_materials(meshes,control,'control',height,settings)
    else:
        for obj in meshes:
            if not direct_palette:bake_material(obj,color,'color',height,settings)
            bake_material(obj,control,'control',height,settings)
    if direct_palette:write_palette_base(meshes,color,face_colors,size)
    packed=pixels(control);packed[:,3]=(8+16*raster_profiles(meshes,size,int(size/128)))/255
    rgba=pixels(color);rgba[:,3]=1
    atlas['fill_constants'](layout,rgba,packed,size,color_is_srgb=direct_palette)
    overlap=runpy.run_path(str(Path(__file__).with_name('baked_ao_overlap.py')))
    print('BAKE_HAIR_AO_OVERLAP_TEXELS',overlap['neutralize_hair_overlap'](meshes,packed,size),flush=True)
    control.pixels.foreach_set(packed.ravel());color.pixels.foreach_set(rgba.ravel())
    base_path=output/'BaseColor.png';data_path=output/'SurfaceData.png'
    save_png(color,base_path);save_png(control,data_path)
    for obj in meshes:
        mesh=obj.data
        mesh.calc_tangents(uvmap='BakeUV')
        vectors=mesh.attributes.get(hf.FLOW)
        progress=mesh.attributes.get(hf.PROGRESS)
        flags=hf.region(mesh)
        normal_data=mesh.corner_normals
        uv0=[tuple(x.uv) for x in mesh.uv_layers['BakeUV'].data]
        flow_uv=[(0.0,0.0)]*len(mesh.loops)
        meta_uv=[(0.0,0.0)]*len(mesh.loops)
        for p,enabled in zip(mesh.polygons,flags):
            if not enabled:continue
            for i in p.loop_indices:
                loop=mesh.loops[i];normal=normal_data[i].vector
                tangent=loop.tangent;bitangent=normal.cross(tangent)*loop.bitangent_sign
                direction=vectors.data[loop.vertex_index].vector
                value=Vector((direction.dot(tangent),direction.dot(bitangent)))
                if value.length<1e-6:raise ValueError(f'{obj.name}: invalid tangent flow at corner {i}')
                flow_uv[i]=tuple(value.normalized())
                meta_uv[i]=(progress.data[loop.vertex_index].value,1.0)
        while mesh.uv_layers:mesh.uv_layers.remove(mesh.uv_layers[0])
        for name,values in [('UV0',uv0),('HairFlow',flow_uv),('HairMeta',meta_uv)]:
            layer=mesh.uv_layers.new(name=name)
            for item,value in zip(layer.data,values):item.uv=value
        mesh.uv_layers.active_index=0;mesh.uv_layers[0].active_render=True
        mesh.calc_tangents(uvmap='UV0')
    return {'format':FORMAT,'schemaVersion':'1.0','packageId':package_id,'enabled':True,
            'uvLayout':'bakeUv0HairFlowUv1HairMetaUv2','surfaceDataLayout':'aoRimEmissionProfile16',
            'textureSize':size,'baseColorFile':'BaseColor.png','surfaceDataFile':'SurfaceData.png',
            'requestedTextureSize':requested,'sourceRecommendedSize':int(math.ceil(required)),
            'colorSource':'authoringPalette' if direct_palette else 'materialGraph',
            'baseColorHash':sha(base_path),'surfaceDataHash':sha(data_path),'hair':settings,
            'cullMode':'Off','toolVersion':VERSION,'hairReport':report,'atlasLayout':layout['stats']}


def finalize(document,fbx,snapshot_file):
    origin=bpy.context.scene.get('wgame_bake_authoring_path',snapshot_file)
    try:origin=Path(origin).resolve().relative_to(Path(__file__).resolve().parents[3]).as_posix()
    except ValueError:origin=str(origin)
    document['authoringSourcePath']=origin
    document['authoringSourceHash']=bpy.context.scene.get('wgame_bake_authoring_hash','')
    document['modelFile']=Path(fbx).name
    document['modelHash']=sha(fbx)
    document['sourceBlendHash']=sha(snapshot_file)
    path=Path(fbx).parent/BINDING
    path.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return path


def validate_mesh(obj):
    mesh=obj.data
    if len(mesh.uv_layers)<3:raise ValueError(f'{obj.name}: baked mesh needs three UV channels')
    mesh.calc_tangents(uvmap=mesh.uv_layers[0].name)
    for i,loop in enumerate(mesh.loops):
        uv=mesh.uv_layers[0].data[i].uv
        flow=mesh.uv_layers[1].data[i].uv
        meta=mesh.uv_layers[2].data[i].uv
        if not all(math.isfinite(v) for v in (*uv,*flow,*meta)):raise ValueError('Non-finite baked mesh data')
        if not all(-1e-5<=v<=1.00001 for v in uv):raise ValueError('BakeUV outside atlas')
        if meta.y>0.5 and (abs(flow.length-1)>0.015 or not -1e-5<=meta.x<=1.00001):raise ValueError('Invalid baked hair flow')
    return {'uvChannels':3,'corners':len(mesh.loops),'hairCorners':sum(v.uv.y>0.5 for v in mesh.uv_layers[2].data)}


def transfer_baked_hair(source,target):
    """Project baked UV and object-space flow onto a reduced mesh, then re-encode.

    Used for LOD acceptance; does not generate or publish a LOD chain.
    Objects must share the same local coordinate system and bind pose.
    """
    from mathutils.bvhtree import BVHTree
    from mathutils.geometry import barycentric_transform
    src=source.data;dst=target.data
    src.calc_loop_triangles();src.calc_tangents(uvmap=src.uv_layers[0].name)
    triangles=list(src.loop_triangles)
    bvh=BVHTree.FromPolygons([v.co for v in src.vertices],[t.vertices[:] for t in triangles],all_triangles=True)
    flow=[];meta=[];uv=[]
    for loop in dst.loops:
        point=dst.vertices[loop.vertex_index].co
        location,normal,index,distance=bvh.find_nearest(point)
        if index is None:raise ValueError('LOD projection has no source triangle')
        tri=triangles[index];verts=[src.vertices[i].co for i in tri.vertices]
        weights=barycentric_transform(location,*verts,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
        tangent_flow=Vector((0,0,0));progress=0.;mask=0.;tex=Vector((0,0))
        for i,w in zip(tri.loops,weights):
            source_loop=src.loops[i];n=src.corner_normals[i].vector;t=source_loop.tangent
            f=src.uv_layers[1].data[i].uv;h=src.uv_layers[2].data[i].uv
            tangent_flow+=(t*f.x+n.cross(t)*source_loop.bitangent_sign*f.y)*w
            tex+=src.uv_layers[0].data[i].uv*w;progress+=h.x*w;mask+=h.y*w
        flow.append(tangent_flow);meta.append((max(0,min(1,progress)),1.0 if mask>.5 else 0.0));uv.append(tuple(tex))
    while dst.uv_layers:dst.uv_layers.remove(dst.uv_layers[0])
    layer=dst.uv_layers.new(name='UV0')
    for value,coord in zip(layer.data,uv):value.uv=coord
    dst.calc_tangents(uvmap='UV0')
    encoded=[]
    for i,loop in enumerate(dst.loops):
        if meta[i][1]<.5:encoded.append((0.,0.));continue
        n=dst.corner_normals[i].vector;t=loop.tangent;b=n.cross(t)*loop.bitangent_sign
        v=Vector((flow[i].dot(t),flow[i].dot(b)))
        if v.length<1e-7:raise ValueError(f'LOD flow is degenerate at corner {i}')
        encoded.append(tuple(v.normalized()))
    for name,values in [('HairFlow',encoded),('HairMeta',meta)]:
        layer=dst.uv_layers.new(name=name)
        for item,value in zip(layer.data,values):item.uv=value
    return validate_mesh(target)


def publish_package(staged,package_root,model_relative):
    """Register baked textures with the existing resource catalog and manifest."""
    import copy,os,shutil
    staged=Path(staged);root=Path(package_root)
    d=json.loads((staged/BINDING).read_text(encoding='utf-8'))
    catalog_path=root/'resource_catalog.json';manifest_path=root/'manifest.json'
    catalog=json.loads(catalog_path.read_text(encoding='utf-8-sig'))
    manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    package_id=d['packageId']
    template=next((r for r in catalog['entries'] if r.get('usage')=='texture'),None)
    if template is None:raise ValueError('Package needs an existing texture import descriptor')
    d['modelFile']=model_relative
    if sha(root/model_relative)!=d['modelHash']:raise ValueError('Synced model differs from baked model')
    writes=[]
    for stem,filename in [('baseColor','BaseColor.png'),('surfaceData','SurfaceData.png')]:
        key=f'char.{package_id}.texture.baked_'+('base_color' if stem=='baseColor' else 'surface_data')
        relative='resources/textures/baked/'+filename
        d[stem+'File']=relative
        entry=copy.deepcopy(template)
        entry.update(resourceKey=key,localId=key.split('.texture.')[1],stableId=f'charpkg.{package_id}.resource.'+key.split('.texture.')[1],relativePath=relative,hash=d[stem+'Hash'])
        entry['hashes']={'algorithm':'sha256','contentHash':d[stem+'Hash'],'importHash':'','dependencyHash':''}
        entry['dependencies']=[];entry['importHints']['targetRelativePath']=relative
        entry['importHints']['metadata']={'bakedSurface':'true','textureSemantic':'albedo' if stem=='baseColor' else 'data'}
        entry.setdefault('conflictPolicy',{})['allowOverwrite']=True
        catalog['entries']=[r for r in catalog['entries'] if r['resourceKey']!=key]+[entry]
        writes.append((root/relative,(staged/filename).read_bytes()))
    relative='config/'+BINDING
    manifest['configFiles']=list(dict.fromkeys(manifest.get('configFiles',[])+[relative]))
    writes.extend([(root/relative,(json.dumps(d,ensure_ascii=False,indent=2)+'\n').encode()),
                   (catalog_path,(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n').encode()),
                   (manifest_path,(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode())])
    backups={p:p.read_bytes() if p.exists() else None for p,_ in writes}
    try:
        for p,content in writes:
            p.parent.mkdir(parents=True,exist_ok=True)
            tmp=p.with_suffix(p.suffix+'.bake-tmp');tmp.write_bytes(content);os.replace(tmp,p)
    except Exception:
        for p,content in backups.items():
            if content is None:p.unlink(missing_ok=True)
            else:p.write_bytes(content)
        raise
    return d
