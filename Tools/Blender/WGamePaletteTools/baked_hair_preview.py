"""EEVEE preview of the baked hair contract; owns only transient preview data."""
import math
import bpy
from mathutils import Vector

SIGN = 'WGamePreviewTangentSign'
ENABLE = 'WGameHairPreviewEnabled'
LIGHT = 'WGameHairPreviewLightDirection'
LIGHT_COLOR = 'WGameHairPreviewLightColor'


def update_light(material,light):
    direction=light.matrix_world.to_quaternion()@Vector((0,0,1))
    for name,values in ((LIGHT,direction),(LIGHT_COLOR,[c*light.data.energy for c in light.data.color])):
        node=material.node_tree.nodes.get(name)
        if node:
            for socket,value in zip(node.inputs,values):socket.default_value=value


def prepare_mesh(obj):
    mesh=obj.data
    if len(mesh.uv_layers)<3:raise ValueError('烘焙模型缺少发流 UV1 或头发标记 UV2')
    mesh.calc_tangents(uvmap=mesh.uv_layers[0].name)
    attr=mesh.attributes.get(SIGN) or mesh.attributes.new(name=SIGN,type='FLOAT',domain='CORNER')
    attr.data.foreach_set('value',[loop.bitangent_sign for loop in mesh.loops])


def lighting(scene):
    scene.render.engine='BLENDER_EEVEE'
    world=bpy.data.worlds.new('WGame Preview World');world.use_nodes=True;scene.world=world
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.12,.14,.18,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
    sun=bpy.data.lights.new('WGame Preview Key','SUN');sun.energy=2.;sun.angle=math.radians(8);sun.color=(1.,.97,.92)
    key=bpy.data.objects.new('WGame Preview Key',sun);scene.collection.objects.link(key)
    key.rotation_euler=(math.radians(70),math.radians(-15),math.radians(-20))
    fill=bpy.data.lights.new('WGame Preview Fill','SUN');fill.energy=.65;fill.angle=math.radians(30)
    obj=bpy.data.objects.new('WGame Preview Fill',fill);scene.collection.objects.link(obj)
    obj.rotation_euler=(math.radians(60),math.radians(35),math.radians(35))
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    return key


def build(material,base,control,settings,uv_names,light,profile=None,profile_specular=None):
    tree=material.node_tree;nodes=tree.nodes;links=tree.links
    frame=nodes.new('NodeFrame');frame.label='WGame 发流双亮带 / Baked Hair Preview'
    def node(kind,name=None):
        n=nodes.new(kind);n.parent=frame
        if name:n.name=n.label=name
        return n
    def bind(value,socket):
        if isinstance(value,bpy.types.NodeSocket):links.new(value,socket)
        else:socket.default_value=value
    def math_node(op,*values):
        n=node('ShaderNodeMath');n.operation=op
        for s,v in zip(n.inputs,values):bind(v,s)
        return n.outputs[0]
    def vector(op,*values):
        n=node('ShaderNodeVectorMath');n.operation=op
        for s,v in zip(n.inputs,values):bind(v,s)
        return n.outputs['Value' if op=='DOT_PRODUCT' else 'Vector']
    def scale(v,s):
        n=node('ShaderNodeVectorMath');n.operation='SCALE';bind(v,n.inputs[0]);bind(s,n.inputs['Scale'])
        return n.outputs['Vector']
    def clamp(v):return math_node('MINIMUM',1.,math_node('MAXIMUM',0.,v))
    def smoothstep(low,high,value):
        t=clamp(math_node('DIVIDE',math_node('SUBTRACT',value,low),high-low))
        return math_node('MULTIPLY',math_node('MULTIPLY',t,t),math_node('SUBTRACT',3.,math_node('MULTIPLY',2.,t)))
    def mix(a,b,t):return vector('ADD',scale(a,math_node('SUBTRACT',1.,t)),scale(b,t))
    def separate(value):
        n=node('ShaderNodeSeparateXYZ');bind(value,n.inputs[0]);return n.outputs
    geometry=node('ShaderNodeNewGeometry');normal=geometry.outputs['Normal'];view=geometry.outputs['Incoming']
    tangent=node('ShaderNodeTangent');tangent.direction_type='UV_MAP';tangent.uv_map=uv_names[0]
    flow=node('ShaderNodeUVMap');flow.uv_map=uv_names[1];xy=separate(flow.outputs['UV'])
    meta=node('ShaderNodeUVMap');meta.uv_map=uv_names[2];progress,mask,_=separate(meta.outputs['UV'])
    sign=node('ShaderNodeAttribute');sign.attribute_name=SIGN
    bitangent=scale(vector('CROSS_PRODUCT',normal,tangent.outputs[0]),sign.outputs['Fac'])
    direction=vector('NORMALIZE',vector('ADD',scale(tangent.outputs[0],xy[0]),scale(bitangent,xy[1])))
    light_node=node('ShaderNodeCombineXYZ',LIGHT)
    light_color=node('ShaderNodeCombineXYZ',LIGHT_COLOR)
    update_light(material,light)
    half_vector=vector('NORMALIZE',vector('ADD',light_node.outputs[0],view))
    # The directional lobes replace the regular surface specular response, so
    # they must still honor its strength (matte .06 / sheen .18 in Catalog v3).
    if profile_specular is None:
        raise ValueError('Baked hair preview requires the surface-profile specular table')
    finish=profile_specular
    def lobe(prefix):
        shifted=vector('NORMALIZE',vector('ADD',direction,scale(normal,float(settings[prefix+'Shift']))))
        dot=vector('DOT_PRODUCT',shifted,half_vector)
        sine=clamp(math_node('SUBTRACT',1.,math_node('MULTIPLY',dot,dot)))
        width=float(settings[prefix+'Width'])*(.65 if prefix=='secondary' else 1.)
        power=max(2.,2./max(.01,width**2))*.5
        # Unity's smoothstep(-1,0,tangentHalf).
        t=clamp(math_node('ADD',dot,1.))
        smooth=math_node('MULTIPLY',math_node('MULTIPLY',t,t),math_node('SUBTRACT',3.,math_node('MULTIPLY',2.,t)))
        response=math_node('MULTIPLY',math_node('POWER',sine,power),smooth)
        if prefix=='primary':response=math_node('ADD',math_node('MULTIPLY',.65,response),math_node('MULTIPLY',.35,smoothstep(.10,.90,response)))
        strength=float(settings[prefix+'Strength'])*(2. if prefix=='primary' else .45)
        return math_node('MULTIPLY',response,math_node('MULTIPLY',finish,strength))
    t=clamp(math_node('DIVIDE',progress,.3))
    smooth=math_node('MULTIPLY',math_node('MULTIPLY',t,t),math_node('SUBTRACT',3.,math_node('MULTIPLY',2.,t)))
    root=math_node('ADD',1.-float(settings['rootDarkening']),math_node('MULTIPLY',smooth,float(settings['rootDarkening'])))
    ao=separate(control)[0]
    # EEVEE supplies actual light/shadow response; no unconditionally glowing band.
    visibility=node('ShaderNodeBsdfDiffuse');visibility.inputs['Color'].default_value=(1,1,1,1)
    lit=node('ShaderNodeShaderToRGB');links.new(visibility.outputs[0],lit.inputs[0])
    luminance=node('ShaderNodeRGBToBW');links.new(lit.outputs['Color'],luminance.inputs[0])
    attenuation=clamp(luminance.outputs[0])
    rgb=separate(base)
    high=math_node('MAXIMUM',rgb[0],math_node('MAXIMUM',rgb[1],rgb[2]))
    low=math_node('MINIMUM',rgb[0],math_node('MINIMUM',rgb[1],rgb[2]))
    neutral=clamp(math_node('SUBTRACT',1.,math_node('MULTIPLY',4.,math_node('SUBTRACT',high,low))))
    pale=math_node('MULTIPLY',neutral,smoothstep(.4,.8,vector('DOT_PRODUCT',base,(.2126,.7152,.0722))))
    lit=math_node('MULTIPLY',smoothstep(-.15,.55,vector('DOT_PRODUCT',normal,light_node.outputs[0])),attenuation)
    shadow=math_node('SUBTRACT',1.,math_node('MULTIPLY',lit,math_node('ADD',.4,math_node('MULTIPLY',.6,clamp(ao)))))
    tint=mix((1.,1.,1.),(.84,.82,1.),math_node('MULTIPLY',shadow,pale))
    diffuse=node('ShaderNodeBsdfDiffuse','WGame Hair Diffuse')
    bind(scale(vector('MULTIPLY',base,tint),math_node('MULTIPLY',.68,math_node('MULTIPLY',root,ao))),diffuse.inputs['Color'])
    diffuse.inputs['Roughness'].default_value=.45
    primary=scale(mix(base,light_color.outputs[0],.7),lobe('primary'))
    secondary=scale(mix(base,light_color.outputs[0],.2),lobe('secondary'))
    facing=clamp(math_node('ADD',math_node('MULTIPLY',vector('DOT_PRODUCT',normal,light_node.outputs[0]),.5),.5))
    rim=math_node('MULTIPLY',math_node('POWER',math_node('SUBTRACT',1.,clamp(vector('DOT_PRODUCT',normal,view))),4.),
        math_node('MULTIPLY',clamp(vector('DOT_PRODUCT',scale(light_node.outputs[0],-1.),view)),float(settings['rimStrength'])))
    specular=vector('ADD',scale(vector('ADD',primary,secondary),facing),scale(base,rim))
    weight=math_node('MULTIPLY',attenuation,math_node('MULTIPLY',root,math_node('ADD',.35,math_node('MULTIPLY',.65,ao))))
    highlight=node('ShaderNodeEmission','WGame Hair Directional Highlights');bind(scale(specular,weight),highlight.inputs['Color'])
    add=node('ShaderNodeAddShader');links.new(diffuse.outputs[0],add.inputs[0]);links.new(highlight.outputs[0],add.inputs[1])
    enabled=node('ShaderNodeValue',ENABLE);enabled.outputs[0].default_value=1.
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
    original=output.inputs['Surface'].links[0].from_socket
    final=node('ShaderNodeMixShader','WGame Hair Region Only')
    bind(math_node('MULTIPLY',clamp(mask),enabled.outputs[0]),final.inputs[0])
    links.new(original,final.inputs[1]);links.new(add.outputs[0],final.inputs[2]);links.new(final.outputs[0],output.inputs['Surface'])
    material['wgame_hair_preview_parameters']=str(dict(settings))
    material['wgame_hair_preview_style']='softBands.v2.profileSpecular'
