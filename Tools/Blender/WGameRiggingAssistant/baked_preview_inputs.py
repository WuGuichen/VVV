"""Content fingerprint for a reviewed bake, excluding viewport and selection state."""
import hashlib
import json
from pathlib import Path

import bpy
import numpy as np


def source_fingerprint(scene, root):
    digest = hashlib.sha256()
    seen_trees = set()
    seen_images = set()

    def add(value):
        digest.update(json.dumps(value, sort_keys=True, ensure_ascii=False, allow_nan=False).encode())
        digest.update(b'\n')

    def scalar_properties(value, exclude=()):
        result = {}
        for prop in value.bl_rna.properties:
            key = prop.identifier
            if key in exclude or key == 'rna_type' or prop.is_readonly or prop.type not in {'BOOLEAN', 'INT', 'FLOAT', 'STRING', 'ENUM'}:
                continue
            try:
                item = getattr(value, key)
                result[key] = list(item) if getattr(prop, 'is_array', False) else sorted(item) if isinstance(item, set) else item
            except (AttributeError, TypeError):
                continue
        return result

    def array(values, field, width=1, dtype=np.float32):
        data = np.empty(len(values) * width, dtype=dtype)
        if len(values):values.foreach_get(field, data)
        add([field, len(values), width, str(dtype)])
        digest.update(data.tobytes())

    def image(image):
        if image is None or image.as_pointer() in seen_images:
            return
        seen_images.add(image.as_pointer())
        # Packing painted GENERATED images changes their storage kind to FILE,
        # without changing the pixels the bake consumes.
        static = image.source in {'FILE', 'GENERATED'}
        add([image.name, 'STATIC' if static else image.source, '' if static else image.filepath,
             list(image.size), image.colorspace_settings.name, image.alpha_mode])
        pixels = np.empty(len(image.pixels), dtype=np.float32)
        image.pixels.foreach_get(pixels)
        digest.update(pixels.tobytes())

    def tree(tree):
        if tree is None or tree.as_pointer() in seen_trees:
            return
        seen_trees.add(tree.as_pointer())
        add(tree.name)
        for node in sorted(tree.nodes, key=lambda n: n.name):
            add([node.name, node.bl_idname])
            add(scalar_properties(node, {'location', 'location_absolute', 'width', 'height', 'dimensions', 'select', 'hide', 'label', 'color', 'use_custom_color', 'show_options', 'show_preview', 'show_texture'}))
            for socket in list(node.inputs) + list(node.outputs):
                add(scalar_properties(socket, {'hide', 'hide_value', 'show_expanded'}))
            if hasattr(node, 'image'):
                add(node.image.name if node.image else None);image(node.image)
            if hasattr(node, 'node_tree'):tree_fn(node.node_tree)
            if hasattr(node, 'image_user'):add(scalar_properties(node.image_user))
            for prop in node.bl_rna.properties:
                if prop.type == 'POINTER':
                    value = getattr(node, prop.identifier, None)
                    if isinstance(value, bpy.types.ID):add([prop.identifier, value.name])
            if hasattr(node, 'color_ramp'):
                add(scalar_properties(node.color_ramp))
                add([scalar_properties(e) for e in node.color_ramp.elements])
            if hasattr(node, 'mapping') and hasattr(node.mapping, 'curves'):
                add(scalar_properties(node.mapping))
                add([[scalar_properties(p) for p in curve.points] for curve in node.mapping.curves])
            for name in ('texture_mapping', 'color_mapping'):
                if hasattr(node, name):add(scalar_properties(getattr(node, name)))
        add(sorted((link.from_node.name, list(link.from_node.outputs).index(link.from_socket),
                    link.to_node.name, list(link.to_node.inputs).index(link.to_socket)) for link in tree.links))

    tree_fn = tree
    add({'scene': scene.name, 'frame': scene.frame_current, 'units': scalar_properties(scene.unit_settings),
         'size': getattr(scene, 'wgame_bake_texture_size', 'AUTO'),
         'hair': {name: getattr(scene, 'wgame_hair_' + name, None) for name in
                  ('primaryWidth', 'primaryStrength', 'secondaryWidth', 'secondaryStrength',
                   'primaryShift', 'secondaryShift', 'rootDarkening', 'rimStrength', 'aoStrength')}})
    for obj in sorted(scene.objects, key=lambda o: o.name):
        if obj.type not in {'MESH', 'ARMATURE', 'CURVE', 'EMPTY'}:continue
        if obj.mode == 'EDIT' and obj.type != 'MESH':obj.update_from_editmode()
        add([obj.name, obj.type, [list(row) for row in obj.matrix_world],
             obj.parent.name if obj.parent else None, obj.parent_type, obj.parent_bone,
             bool(obj.get('wgame_rig_exclude_from_binding', False)), obj.get('wgame_hair_target', ''),
             bool(obj.get('wgame_hair_guide', False)), obj.get('wgame_rig_kind', '')])
        for modifier in obj.modifiers:
            add(scalar_properties(modifier))
            try:add({k: str(v) for k, v in modifier.items()})
            except TypeError:pass
            for prop in modifier.bl_rna.properties:
                if prop.type == 'POINTER':
                    value = getattr(modifier, prop.identifier, None)
                    if isinstance(value, bpy.types.ID):
                        add([prop.identifier, value.name])
                        if isinstance(value, bpy.types.NodeTree):tree(value)
        if obj.type == 'MESH':
            mesh = obj.data
            temporary = None
            if obj.mode == 'EDIT':
                import bmesh
                temporary = bpy.data.meshes.new('BakeFingerprintTemporary')
                copy = bmesh.from_edit_mesh(obj.data).copy()
                try:copy.to_mesh(temporary)
                finally:copy.free()
                mesh = temporary
            try:
                array(mesh.vertices, 'co', 3)
                array(mesh.edges, 'vertices', 2, np.int32)
                array(mesh.loops, 'vertex_index', dtype=np.int32)
                for field in ('loop_start', 'loop_total', 'material_index', 'use_smooth'):
                    array(mesh.polygons, field, dtype=np.bool_ if field == 'use_smooth' else np.int32)
                for attr in sorted(mesh.attributes, key=lambda a: a.name):
                    if attr.name.startswith(('.select', '.hide')):continue
                    # Blender stores custom normals as packed short pairs; hash their
                    # decoded corner vectors below, which are what the exporter uses.
                    if attr.name in {'custom_normal', 'position', '.edge_verts', '.corner_vert', '.corner_edge', 'sharp_face', 'material_index'}:continue
                    field, width = {'FLOAT_VECTOR': ('vector', 3), 'FLOAT2': ('vector', 2), 'INT32_2D': ('value', 2),
                                    'FLOAT_COLOR': ('color', 4), 'BYTE_COLOR': ('color', 4)}.get(attr.data_type, ('value', 1))
                    if attr.data_type == 'STRING':
                        add([attr.name, [v.value for v in attr.data]]);continue
                    add([attr.name, attr.data_type, attr.domain])
                    try:
                        array(attr.data, field, width, np.bool_ if attr.data_type == 'BOOLEAN' else np.int32 if attr.data_type in {'INT', 'INT8', 'INT32_2D'} else np.float32)
                    except (RuntimeError, AttributeError) as error:
                        raise ValueError(f'无法检查制作属性 {attr.name} ({attr.data_type}): {error}') from error
                add([group.name for group in obj.vertex_groups])
                add([[(g.group, g.weight) for g in vertex.groups] for vertex in mesh.vertices])
                if mesh.has_custom_normals:array(mesh.corner_normals, 'vector', 3)
                if obj.data.shape_keys:
                    for key in obj.data.shape_keys.key_blocks:
                        add([key.name, key.value, key.mute, key.relative_key.name]);array(key.data, 'co', 3)
                for slot in obj.material_slots:
                    material = slot.material
                    if material and material.get('wgame_hair_preview_source'):
                        material = bpy.data.materials.get(material['wgame_hair_preview_source'])
                    add(material.name if material else None)
                    if material:
                        add(scalar_properties(material));tree(material.node_tree)
            finally:
                if temporary is not None:bpy.data.meshes.remove(temporary)
        elif obj.type == 'ARMATURE':
            add([[b.name, b.parent.name if b.parent else None, list(b.head_local), list(b.tail_local),
                  [list(row) for row in b.matrix_local], b.use_deform] for b in obj.data.bones])
        elif obj.type == 'CURVE':
            add(scalar_properties(obj.data))
            for spline in obj.data.splines:
                add(scalar_properties(spline))
                add([scalar_properties(p, {'select'}) for p in spline.points])
                add([scalar_properties(p, {'select_control_point', 'select_left_handle', 'select_right_handle'}) for p in spline.bezier_points])
    for relative in ('Tools/Blender/WGameRiggingAssistant/baked_surface.py',
                     'Tools/Blender/WGameRiggingAssistant/baked_ao_overlap.py',
                     'Tools/Blender/WGameRiggingAssistant/export_unity_fbx.py',
                     'Tools/Blender/WGameRiggingAssistant/fbx_tangent_pruning.py',
                     'Tools/Blender/WGameRiggingAssistant/baked_atlas.py',
                     'Tools/Blender/WGameRiggingAssistant/baked_atlas_packing.py',
                     'Tools/Blender/WGameRiggingAssistant/baked_palette.py',
                     'Tools/Blender/WGamePaletteTools/hair_flow.py',
                     'ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json'):
        path = Path(root) / relative
        if path.is_file():add(relative);digest.update(path.read_bytes())
    return 'sha256:' + digest.hexdigest()
