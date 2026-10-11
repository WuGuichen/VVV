"""Shared authoring masks; runtime emission remains in baked SurfaceData.B."""
import math
import uuid

import bpy

NODE='Emission_Detail_Texture'
TAG='wgame_environment_emission_mask'
OWNER='wgame_environment_emission_for'
INTENSITY='wgame_environment_emission_intensity'
DEFAULT_INTENSITY=8.0
MAX_INTENSITY=64.0


def intensity(image):
    # Existing masks retain their previous 1x appearance until explicitly edited.
    value=float(image.get(INTENSITY,1.0))
    if not math.isfinite(value) or not 1<=value<=MAX_INTENSITY:
        raise ValueError('HDR 发光强度必须在 1–64 之间：'+image.name)
    return value


def current_mask(scene):
    from . import shared_detail
    obj=getattr(bpy.context,'object',None)
    try:detail=shared_detail.canvas(scene=scene,obj=obj)
    except ValueError:detail=scene.wgame_environment_detail_image
    return find_mask(detail)


def intensity_get(scene):
    image=current_mask(scene)
    return intensity(image) if image else DEFAULT_INTENSITY


def intensity_set(scene,value):
    image=current_mask(scene)
    if image is None:return
    set_intensity(image,value)


def set_intensity(image,value):
    value=float(value)
    if not math.isfinite(value) or not 1<=value<=MAX_INTENSITY:raise ValueError('HDR 发光强度必须在 1–64 之间')
    image[INTENSITY]=value
    for material in bpy.data.materials:
        if mask_image(material)==image:attach(material,image)


def group_intensity(objects):
    images={mask_image(obj.data.materials[index]) for obj in objects
            for index in {face.material_index for face in obj.data.polygons}}-{None}
    values={intensity(image) for image in images}
    if len(values)>1:raise ValueError('共用同一细节图的发光强度必须一致，请统一后再烘焙')
    return next(iter(values)) if values else 1.0


def is_mask(image):
    return bool(image and image.get(TAG))


def mask_image(material):
    if not material or not material.use_nodes or not material.get('wgame_environment_shared'):return None
    node=material.node_tree.nodes.get(NODE)
    return node.image if node else None


def find_mask(detail):
    identifier=detail.get('wgame_detail_source_id') if detail else None
    if not identifier:return None
    images=[image for image in bpy.data.images if is_mask(image) and image.get(OWNER)==identifier]
    if len(images)>1:raise ValueError('细节图关联了多张发光遮罩，请先整理：'+detail.name)
    return images[0] if images else None


def ensure_mask(detail):
    if detail.source not in {'FILE','GENERATED'} or detail.channels!=4 or min(detail.size)<1:
        raise ValueError('请先选择有效的普通 RGBA 细节图')
    if detail.get('wgame_canvas_preview'):raise ValueError('请先确认或取消二维浮动预览')
    image=find_mask(detail)
    if image:
        validate_image(image)
        return image
    if not detail.get('wgame_detail_source_id'):detail['wgame_detail_source_id']=uuid.uuid4().hex
    image=bpy.data.images.new(detail.name+'_Emission',width=detail.size[0],height=detail.size[1],alpha=True)
    image.generated_color=(0,0,0,1);image.colorspace_settings.name='Non-Color'
    image[TAG]=True;image[OWNER]=detail['wgame_detail_source_id']
    image[INTENSITY]=DEFAULT_INTENSITY
    image['wgame_detail_source_id']=uuid.uuid4().hex
    image.use_fake_user=True;image.pack()
    return image


def validate_image(image):
    if not is_mask(image) or image.source not in {'FILE','GENERATED'} or image.channels!=4 or min(image.size)<1:
        raise ValueError('发光遮罩必须是有效的普通 RGBA 图片')
    if image.colorspace_settings.name!='Non-Color':raise ValueError('发光遮罩应使用 Non-Color 色彩空间：'+image.name)
    intensity(image)


def attach(material,image):
    """Preview uses the final base color and a clamped, alpha-aware scalar mask."""
    validate_image(image)
    tree=material.node_tree;nodes=tree.nodes
    detail_uv=nodes.get('Detail_UV')
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
    bsdf=output.inputs['Surface'].links[0].from_node
    if detail_uv is None or bsdf.type!='BSDF_PRINCIPLED':raise ValueError('需要 WGame 场景共享细节材质')
    def node(kind,name):
        item=nodes.get(name) or nodes.new(kind);item.name=name;return item
    texture=node('ShaderNodeTexImage',NODE);texture.image=image
    texture.extension='CLIP';texture.interpolation='Linear'
    tree.links.new(detail_uv.outputs['UV'],texture.inputs['Vector'])
    separate=node('ShaderNodeSeparateColor','Emission_Mask_R')
    tree.links.new(texture.outputs['Color'],separate.inputs['Color'])
    strength=node('ShaderNodeMath','Emission_Mask_Amount');strength.operation='MULTIPLY';strength.use_clamp=True
    tree.links.new(separate.outputs['Red'],strength.inputs[0]);tree.links.new(texture.outputs['Alpha'],strength.inputs[1])
    base=bsdf.inputs['Base Color']
    if base.is_linked:tree.links.new(base.links[0].from_socket,bsdf.inputs['Emission Color'])
    else:bsdf.inputs['Emission Color'].default_value=base.default_value
    hdr=node('ShaderNodeMath','Emission_HDR_Strength');hdr.operation='MULTIPLY';hdr.use_clamp=False
    hdr.inputs[1].default_value=intensity(image)
    tree.links.new(strength.outputs[0],hdr.inputs[0]);tree.links.new(hdr.outputs[0],bsdf.inputs['Emission Strength'])


def prepare(context):
    from . import authoring,shared_detail
    from .group_bake import mapped_faces
    obj=context.object
    if not obj or not obj.get('wgame_environment_asset'):raise ValueError('请选择已登记的场景模型')
    authoring.activate(obj)
    if not mapped_faces(obj):raise ValueError('先为需要发光的面展开 DetailUV')
    try:detail=shared_detail.canvas(obj=obj)
    except ValueError:detail=context.scene.wgame_environment_detail_image
    if detail is None:raise ValueError('请先选择共享细节图')
    context.scene.wgame_environment_detail_image=detail
    if bpy.ops.wgame.environment_bind_detail()!={'FINISHED'}:raise ValueError('细节材质绑定失败')
    image=ensure_mask(detail)
    for material in bpy.data.materials:
        if not material.use_nodes or not material.get('wgame_environment_shared'):continue
        node=material.node_tree.nodes.get('Detail_Texture')
        if node and node.image==detail:attach(material,image)
    return image


def disable_on_empty(material):
    """An unallocated UV samples no emission; keep constant-face bake reuse."""
    if mask_image(material) is None:return
    tree=material.node_tree
    bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    socket=bsdf.inputs['Emission Strength']
    for link in list(socket.links):tree.links.remove(link)
    socket.default_value=0


def prepare_bake(obj):
    """Only the isolated worker adapts mask-driven surfaces to the shared baker.

    The baker multiplies PaletteData.B by Principled Emission Strength. The mask
    replaces that authored scalar here; other materials retain their old data.
    """
    attribute=obj.data.color_attributes.get('PaletteData')
    if attribute is None:return
    slots={index for index,mat in enumerate(obj.data.materials) if mask_image(mat) is not None}
    for index in slots:
        tree=obj.data.materials[index].node_tree
        bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
        socket=bsdf.inputs['Emission Strength']
        if socket.is_linked and socket.links[0].from_node.name=='Emission_HDR_Strength':
            # Bake the normalized mask, never the HDR gain into an 8-bit channel.
            # The group manifest carries the gain to preview and Unity.
            tree.links.new(tree.nodes['Emission_Mask_Amount'].outputs[0],socket)
    for face in obj.data.polygons:
        if face.material_index not in slots:continue
        for index in face.loop_indices:
            value=list(attribute.data[index].color);value[2]=1;attribute.data[index].color=value


def paint_value_changed(scene,context):
    if not context or context.mode!='PAINT_TEXTURE':return
    paint=scene.tool_settings.image_paint
    if not is_mask(paint.canvas):return
    amount=0 if scene.wgame_environment_emission_erasing else scene.wgame_environment_emission_value
    unified=paint.unified_paint_settings
    unified.use_unified_color=True;unified.color=(amount,amount,amount)
    if paint.brush:
        paint.brush.blend='MIX';paint.brush.color=(amount,amount,amount)


def connect_baked(tree,bsdf,base,control,gain=1.0):
    separate=tree.nodes.new('ShaderNodeSeparateColor')
    tree.links.new(control.outputs['Color'],separate.inputs['Color'])
    hdr=tree.nodes.new('ShaderNodeMath');hdr.name='Emission_HDR_Strength';hdr.operation='MULTIPLY'
    hdr.inputs[1].default_value=gain
    tree.links.new(separate.outputs['Blue'],hdr.inputs[0]);tree.links.new(hdr.outputs[0],bsdf.inputs['Emission Strength'])
    tree.links.new(base.outputs['Color'],bsdf.inputs['Emission Color'])
