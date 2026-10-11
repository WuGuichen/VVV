"""Scene authoring: PaletteUV + sparse DetailUV, one shared RGBA canvas.

No per-object textures, automated painting, decals or automatic full-mesh unwrap.
The character UV tools remain the owner of selection-based island editing.
"""
import json
import uuid
from pathlib import Path

import bpy
import numpy as np
from . import authoring,emission

MATERIAL='WGame_Environment_PaletteDetail_Shared'
IMAGE='Environment_SharedDetail_RGBA'
PIPELINE='shared_detail_group_bake_v2'


def palette_image(objects):
    for obj in objects:
        for material in obj.data.materials:
            if not material or not material.use_nodes:continue
            for node in material.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image and node.image.name.startswith('WGameRgb565LookupAtlas_Technical'):
                    return node.image
    # Pure-color selections may not use the atlas yet. Rebuilding it would
    # remove the image still referenced by other or cached shared materials.
    existing=bpy.data.images.get('WGameRgb565LookupAtlas_Technical')
    if existing is not None and all(size>0 for size in existing.size):return existing
    return authoring.palette_module()._build_preview_image()


def make_material(palette,detail,name=MATERIAL):
    module=authoring.palette_module()
    material=bpy.data.materials.new(name);material.use_nodes=True
    material['wgame_environment_shared']=True
    tree=material.node_tree;nodes=tree.nodes;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');output.location=(850,50)
    bsdf=nodes.new('ShaderNodeBsdfPrincipled');bsdf.location=(600,50)
    palette_uv=nodes.new('ShaderNodeUVMap');palette_uv.name='Palette_UV';palette_uv.uv_map='PaletteUV';palette_uv.location=(-750,300)
    color=nodes.new('ShaderNodeTexImage');color.name='Palette_Texture';color.image=palette
    color.interpolation='Closest';color.extension='CLIP';color.location=(-520,300)
    tree.links.new(palette_uv.outputs['UV'],color.inputs['Vector'])
    detail_uv=nodes.new('ShaderNodeUVMap');detail_uv.name='Detail_UV';detail_uv.uv_map='DetailUV';detail_uv.location=(-750,0)
    texture=nodes.new('ShaderNodeTexImage');texture.name='Detail_Texture';texture.image=detail
    texture.extension='CLIP';texture.interpolation='Linear';texture.location=(-520,0)
    tree.links.new(detail_uv.outputs['UV'],texture.inputs['Vector'])
    mix=nodes.new('ShaderNodeMixRGB');mix.name='Mix_Detail';mix.label='共享细节覆盖调色板';mix.location=(280,250)
    tree.links.new(texture.outputs['Alpha'],mix.inputs[0]);tree.links.new(color.outputs['Color'],mix.inputs[1])
    tree.links.new(texture.outputs['Color'],mix.inputs[2]);tree.links.new(mix.outputs[0],bsdf.inputs['Base Color'])
    data=nodes.new('ShaderNodeVertexColor');data.layer_name='PaletteData';data.location=(-500,-330)
    for index,(field,socket) in enumerate((('roughness','Roughness'),('metallic','Metallic'),('specular','Specular IOR Level'))):
        ramp=module._build_profile_ramp(nodes,'Surface '+field,field);ramp.location=(-100,-180-index*180)
        tree.links.new(data.outputs['Alpha'],ramp.inputs['Fac']);tree.links.new(ramp.outputs['Color'],bsdf.inputs[socket])
    tree.links.new(bsdf.outputs[0],output.inputs['Surface']);nodes.active=texture
    mask=emission.find_mask(detail)
    if mask:emission.attach(material,mask)
    return material


def canvas(scene=None,obj=None):
    scene=scene or bpy.context.scene
    obj=obj or getattr(bpy.context,'object',None)
    images=set()
    targets=[obj] if obj and obj.type=='MESH' and obj.get('wgame_environment_asset') else list(scene.objects)
    for target in targets:
        if target.type!='MESH' or not target.get('wgame_environment_asset'):continue
        for mat in target.data.materials:
            node=mat.node_tree.nodes.get('Detail_Texture') if mat and mat.use_nodes else None
            if node and node.image:images.add(node.image)
    if len(images)!=1:raise ValueError('请选择使用一张细节图的模型')
    return next(iter(images))


def images_for_save(scene):
    """Saving must preserve draft canvases even when the draft cannot bake yet."""
    images=set();visited=set()
    def collect(tree):
        if tree is None or tree.as_pointer() in visited:return
        visited.add(tree.as_pointer())
        for node in tree.nodes:
            if node.type=='GROUP':collect(node.node_tree)
            if node.type!='TEX_IMAGE' or not node.image:continue
            links=node.inputs['Vector'].links
            if node.name=='Detail_Texture' or any(l.from_node.type=='UVMAP' and l.from_node.uv_map=='DetailUV' for l in links):
                images.add(node.image)
    for obj in scene.objects:
        if obj.type!='MESH' or not obj.get('wgame_environment_asset'):continue
        for mat in obj.data.materials:
            if mat and mat.use_nodes:collect(mat.node_tree)
    # The shared canvas can be temporarily unbound while artists edit materials.
    for image in bpy.data.images:
        if image.get('wgame_environment_shared_detail') or emission.is_mask(image):images.add(image)
    image=getattr(scene,'wgame_environment_detail_image',None)
    if image:images.add(image)
    return images


def convert(scene,reset_detail=True):
    """Called only after an explicit packed backup. Keep palette and geometry intact."""
    if not scene.get('wgame_environment_work'):raise ValueError('这不是场景制作文件')
    objects=[o for o in scene.objects if o.type=='MESH' and o.get('wgame_environment_asset')]
    if not objects:raise ValueError('没有场景模型')
    if scene.get('wgame_environment_pipeline')==PIPELINE:
        return {'objects':len(objects),'alreadyConverted':True}
    palette=palette_image(objects)
    detail=bpy.data.images.new(IMAGE,width=2048,height=2048,alpha=True)
    detail.generated_color=(0,0,0,0);detail.colorspace_settings.name='sRGB'
    detail['wgame_environment_shared_detail']=True;detail.pack()
    detail['wgame_detail_source_id']=uuid.uuid4().hex
    material=make_material(palette,detail)
    scene.tool_settings.image_paint.canvas=detail
    old_materials=set();old_images=set();vertex_count=0
    for obj in objects:
        if obj.mode=='EDIT':obj.update_from_editmode()
        mesh=obj.data;vertex_count+=len(mesh.vertices)
        if mesh.users>1:mesh=obj.data=mesh.copy()
        palette_uv=mesh.uv_layers.get('PaletteUV')
        if palette_uv is None:raise ValueError('缺少调色板 UV：'+obj.name)
        palette_values=np.array([p.uv[:] for p in palette_uv.data],dtype=np.float32)
        previous=mesh.uv_layers.get('DetailUV')
        detail_values=np.array([p.uv[:] for p in previous.data],dtype=np.float32) if previous and not reset_detail else None
        for mat in mesh.materials:
            if mat:
                old_materials.add(mat)
                if mat.use_nodes:
                    for node in mat.node_tree.nodes:
                        if node.type=='TEX_IMAGE' and node.image and node.image not in (palette,detail):old_images.add(node.image)
        while mesh.uv_layers:mesh.uv_layers.remove(mesh.uv_layers[0])
        mesh.uv_layers.new(name='PaletteUV').data.foreach_set('uv',palette_values.ravel())
        layer=mesh.uv_layers.new(name='DetailUV')
        if detail_values is None:detail_values=np.full((len(mesh.loops),2),-1,dtype=np.float32)
        layer.data.foreach_set('uv',detail_values.ravel())
        mesh.uv_layers.active=layer;layer.active_render=True
        mesh.materials.clear();mesh.materials.append(material)
        for face in mesh.polygons:face.material_index=0
        obj['wgame_environment_mode']='shared_detail_bake'
        for key in ('wgame_environment_paint','wgame_environment_paint_size','wgame_environment_max_size','wgame_environment_style_pass','wgame_environment_group','wgame_environment_density'):
            if key in obj:del obj[key]
        mesh.update()
    # Only remove materials/images created by this experiment, after they have no users.
    for mat in old_materials:
        if mat.users==0 and mat.get('wgameEnvironmentMaterial'):bpy.data.materials.remove(mat)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='IMAGE_EDITOR' and area.spaces.active.image in old_images:
                area.spaces.active.image=detail
    for image in old_images:
        if image.users==0 and ('_Paint' in image.name or image.name.startswith('WGame_Industrial_')):
            bpy.data.images.remove(image)
    scene['wgame_environment_pipeline']=PIPELINE
    if 'wgame_environment_catalog' in scene:del scene['wgame_environment_catalog']
    scene['wgame_environment_style']='user_authored'
    return {'objects':len(objects),'vertices':vertex_count,'uv':['PaletteUV','DetailUV'],
            'sharedImages':[palette.name,detail.name],'detailSize':2048,'detailUvInitiallyEmpty':reset_detail}


def validate(scene=None):
    from .group_bake import detail_image,mapped_faces
    from .registration import require_registered
    scene=scene or bpy.context.scene;objects=[];images=set()
    require_registered(scene)
    for obj in scene.objects:
        if obj.type!='MESH' or not obj.get('wgame_environment_asset'):continue
        mesh=obj.data
        if not mesh.uv_layers.get('PaletteUV') or not mesh.uv_layers.get('DetailUV'):
            raise ValueError('缺少 PaletteUV 或 DetailUV：'+obj.name)
        if obj.mode=='EDIT':obj.update_from_editmode()
        mapped=len(mapped_faces(obj));empty=len(mesh.polygons)-mapped
        objects.append(dict(name=obj.name,mappedFaces=mapped,emptyFaces=empty))
        image=detail_image(obj)
        if image:images.add(image.name)
        for index in {face.material_index for face in mesh.polygons}:
            mask=emission.mask_image(mesh.materials[index])
            if mask:emission.validate_image(mask)
    return {'format':'mx.environmentSharedDetailValidation.v2','objects':objects,'images':sorted(images)}
