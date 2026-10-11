"""Direct albedo input for constant colors and nearest-sampled PaletteUV materials."""
import numpy as np


def _linear_color_to_srgb8(color):
    """Encode a constant shader color for the atlas's sRGB image buffer."""
    rgb=np.clip(np.asarray(color[:3],dtype=np.float64),0,1)
    srgb=np.where(rgb<=0.0031308,12.92*rgb,1.055*np.power(rgb,1/2.4)-0.055)
    return tuple(int(v) for v in np.rint(srgb*255))


def read_palette_faces(obj):
    """Return face RGB bytes, or None if any used material needs general baking.

    Do not infer semantics from texture pixels or discard connected detail layers.
    Accept only a direct opaque color or the nearest-sampled sRGB palette contract.
    """
    mesh=obj.data;images={};materials={};result={}
    for face in mesh.polygons:
        index=face.material_index
        if index not in materials:
            material=obj.material_slots[index].material
            if not material or not material.use_nodes:return None
            output=next((n for n in material.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output),None)
            if output is None or not output.inputs['Surface'].is_linked:return None
            bsdf=output.inputs['Surface'].links[0].from_node
            if bsdf.type!='BSDF_PRINCIPLED':return None
            base=bsdf.inputs['Base Color']
            if not base.is_linked:
                if (base.default_value[3]<.999 or bsdf.inputs['Alpha'].is_linked
                        or bsdf.inputs['Alpha'].default_value<.999):return None
                materials[index]=(None,_linear_color_to_srgb8(base.default_value))
            else:
                texture=base.links[0].from_node
                if texture.type!='TEX_IMAGE' or texture.interpolation!='Closest' or texture.projection!='FLAT':return None
                if not texture.image or texture.image.is_float or texture.image.colorspace_settings.name!='sRGB':return None
                if not texture.inputs['Vector'].is_linked:return None
                uv_node=texture.inputs['Vector'].links[0].from_node
                if uv_node.type!='UVMAP' or uv_node.uv_map!='PaletteUV' or uv_node.from_instancer:return None
                layer=mesh.uv_layers.get(uv_node.uv_map)
                if layer is None:return None
                image=texture.image
                if image.as_pointer() not in images:
                    pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels)
                    images[image.as_pointer()]=pixels.reshape(image.size[1],image.size[0],4)
                materials[index]=(layer,images[image.as_pointer()])
        layer,pixels=materials[index]
        if layer is None:
            result[face.index]=pixels
            continue
        height,width,_=pixels.shape
        coords=[tuple(layer.data[i].uv) for i in face.loop_indices]
        if not all(0<=v<1 for uv in coords for v in uv):return None
        texels={(int(u*width),int(v*height)) for u,v in coords}
        if len(texels)!=1:return None
        x,y=next(iter(texels));value=pixels[y,x]
        if value[3]<.999:return None
        result[face.index]=tuple(int(v) for v in np.rint(np.clip(value[:3],0,1)*255))
    return result


def srgb_pixels(values):
    # Blender's sRGB bake image buffer/save path expects these encoded values.
    # Decoding here would apply an extra transfer function and darken the PNG.
    return np.asarray(values,dtype=np.float32)/255
