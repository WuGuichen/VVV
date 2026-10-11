"""Isolated Blender worker: turn the baked wall into a single-post repeatable module."""

import argparse
import hashlib
import json
import sys
from pathlib import Path

import bmesh
import bpy


def components(mesh):
    pending = set(mesh.verts)
    groups = []
    while pending:
        start = pending.pop()
        group = {start}
        queue = [start]
        while queue:
            vertex = queue.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in pending:
                    pending.remove(other)
                    group.add(other)
                    queue.append(other)
        groups.append(group)
    return groups


def build(source, output):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    objects = [item for item in bpy.context.scene.objects if item.type == 'MESH']
    if len(objects) != 1:
        raise ValueError('Expected one baked wall mesh, got ' + str(len(objects)))
    obj = objects[0]
    if [layer.name for layer in obj.data.uv_layers] != ['UV0']:
        raise ValueError('Baked wall must have only UV0')

    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    groups = components(mesh)
    main = max(groups, key=len)
    if min(vertex.co.x for vertex in main) >= -3 or max(vertex.co.x for vertex in main) <= 3:
        raise ValueError('Unexpected wall-panel topology')
    left = [group for group in groups if group is not main
            and max(vertex.co.x for vertex in group) < -2.5]
    if len(left) < 4:
        raise ValueError('The left end-post cannot be isolated safely')
    bmesh.ops.delete(mesh, geom=[vertex for group in left for vertex in group], context='VERTS')

    # The published mesh has a 6.9 m panel between two end-posts. Extend its
    # left half to the module edge; the preceding module owns the shared post.
    minimum = min(vertex.co.x for vertex in main)
    scale = 4.6 / abs(minimum)
    panel_top = max(vertex.co.z for vertex in main)
    source_sill = 0.52
    target_sill = 0.035
    if panel_top <= source_sill:
        raise ValueError('Unexpected lower wall-panel profile')
    for vertex in main:
        if not vertex.is_valid:
            continue
        if vertex.co.x < 0:
            vertex.co.x *= scale
        # Compress the narrow source skirting into the pavement and extend the
        # broad wall face to grade. This preserves connected faces and UV0.
        height = vertex.co.z
        if height <= source_sill:
            vertex.co.z = height * target_sill / source_sill
        else:
            vertex.co.z = target_sill + (height - source_sill) * (
                panel_top - target_sill) / (panel_top - source_sill)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    result = bpy.data.meshes.new('Sec_Wall_Straight_Long_SinglePost')
    mesh.to_mesh(result)
    mesh.free()
    old = obj.data
    obj.data = result
    obj.name = result.name
    bpy.data.meshes.remove(old)
    if [layer.name for layer in result.uv_layers] != ['UV0']:
        raise ValueError('Wall derivation lost UV0')

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True,
        object_types={'MESH'}, use_mesh_modifiers=False, mesh_smooth_type='FACE',
        colors_type='SRGB', use_tspace=True, use_triangles=True,
        axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP')
    vertices = [vertex.co for vertex in result.vertices]
    report = {
        'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
        'outputSha256': hashlib.sha256(output.read_bytes()).hexdigest(),
        'triangleCount': sum(len(face.vertices) - 2 for face in result.polygons),
        'removedLeftPostComponents': len(left),
        'boundsMetersBlenderXYZ': {
            'min': [round(min(vertex[axis] for vertex in vertices), 4) for axis in range(3)],
            'max': [round(max(vertex[axis] for vertex in vertices), 4) for axis in range(3)],
        },
    }
    output.with_suffix('.report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    arguments = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    print('WALL_VARIANT', json.dumps(build(arguments.source, arguments.output)), flush=True)
