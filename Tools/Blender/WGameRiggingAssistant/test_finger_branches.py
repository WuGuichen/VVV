"""Blender background regressions for hand-relative branch selection."""
import importlib.util
import json
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


def main():
    spec = importlib.util.spec_from_file_location('finger_branch_test', Path(__file__).with_name('__init__.py'))
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    armature = bpy.data.armatures.new('TestHand')
    rig = bpy.data.objects.new('TestHand', armature)
    bpy.context.collection.objects.link(rig)
    a._select_only(bpy.context, rig)
    bpy.ops.object.mode_set(mode='EDIT')
    bone = armature.edit_bones.new('hand.l')
    bone.head, bone.tail = (0, 0, 0), (.05, 0, 0)
    bpy.ops.object.mode_set(mode='OBJECT')
    vertices = [(0, 0, -1), (0, 0, 1), (.025, 0, 0), (.04, 0, 0), (.05, 0, 0)]
    edges = [(2, 3), (3, 4)]
    ends = []
    for endpoint in ((.145, -.02, -.01), (.16, 0, 0), (.15, .025, -.01), (.13, .047, -.02), (.085, -.065, -.02)):
        previous = 4
        for i in range(1, 23):
            point = Vector((.05, 0, 0)).lerp(Vector(endpoint), i/22)
            index = len(vertices)
            vertices.append(point)
            edges.append((previous, index))
            previous = index
        ends.append(previous)
    mesh = bpy.data.meshes.new('HandBody')
    mesh.from_pydata(vertices, edges, [])
    obj = bpy.data.objects.new('HandBody', mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.update()
    result = a._hand_geodesic_analysis(obj, rig, 'l')
    assert set(result['main_tips']) == set(ends[:4])
    assert result['thumb_tip'] == ends[4]
    assert all(len(path) >= 4 for path in result['paths'].values())
    # Body height alone must not change finger inference.
    mesh.vertices[1].co.z = 4
    bpy.context.view_layer.update()
    assert a._hand_geodesic_analysis(obj, rig, 'l')['tips'] == result['tips']
    obj['wgame_thumb_hint_l'] = json.dumps({'vertex': ends[4], 'fingerprint': a._hand_hint_fingerprint(obj)})
    assert a._hand_geodesic_analysis(obj, rig, 'l')['thumb_tip'] == ends[4]
    mesh.vertices[ends[4]].co.y -= .001
    try:
        a._hand_geodesic_analysis(obj, rig, 'l')
        raise AssertionError('stale manual hint must fail')
    except ValueError as error:
        assert '已失效' in str(error)
    obj.pop('wgame_thumb_hint_l')
    obj['wgame_thumb_hint_l'] = json.dumps({'vertex': ends[0], 'fingerprint': a._hand_hint_fingerprint(obj)})
    try:
        a._hand_geodesic_analysis(obj, rig, 'l')
        raise AssertionError('main digit cannot be reused as thumb')
    except ValueError as error:
        assert '重合' in str(error)
    obj.pop('wgame_thumb_hint_l')
    # Uniform scaling of hand, body and bone must retain the same branches.
    for vertex in mesh.vertices:
        vertex.co *= 2
    a._select_only(bpy.context, rig)
    bpy.ops.object.mode_set(mode='EDIT')
    armature.edit_bones['hand.l'].tail *= 2
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    scaled = a._hand_geodesic_analysis(obj, rig, 'l')
    assert set(scaled['main_tips']) == set(ends[:4]) and scaled['thumb_tip'] == ends[4]
    a.register()
    try:
        a._wgame_rig_object = lambda *_args: rig
        a._finger_authoring_mesh = lambda *_args: obj
        coords = [tuple(v.co) for v in mesh.vertices]
        assert bpy.ops.wgame_rigging.hand_tip_assist(side='l', action='SHOW') == {'FINISHED'}
        bm = bmesh.from_edit_mesh(mesh)
        bm.verts.ensure_lookup_table()
        assert {v.index for v in bm.verts if v.select} == set(scaled['tips'])
        for vertex in bm.verts:
            vertex.select_set(vertex.index == ends[4])
        bmesh.update_edit_mesh(mesh)
        assert bpy.ops.wgame_rigging.hand_tip_assist(side='l', action='SET') == {'FINISHED'}
        assert a._hand_thumb_hint(obj, 'l') == ends[4]
        assert bpy.ops.wgame_rigging.hand_tip_assist(side='l', action='CLEAR') == {'FINISHED'}
        assert a._hand_thumb_hint(obj, 'l') is None
        bpy.ops.object.mode_set(mode='OBJECT')
        assert coords == [tuple(v.co) for v in mesh.vertices]
    finally:
        a.unregister()
    # A disconnected thumb is a thumb-specific diagnostic, not a forced fifth result.
    disconnected = bpy.data.meshes.new('DisconnectedThumb')
    disconnected.from_pydata(vertices, [e for e in edges if e != (4, ends[3]+1)], [])
    obj.data = disconnected
    a._select_only(bpy.context, rig)
    bpy.ops.object.mode_set(mode='EDIT')
    armature.edit_bones['hand.l'].tail /= 2
    bpy.ops.object.mode_set(mode='OBJECT')
    try:
        a._hand_geodesic_analysis(obj, rig, 'l')
        raise AssertionError('disconnected thumb must not produce five branches')
    except a._HandBranchError as error:
        assert '拇指候选 0' in str(error) and error.candidates
    print('WG_FINGER_BRANCHES_OK short_side_thumb body_height scale manual_hint stale_guard overlap_guard')


if __name__ == '__main__':
    main()
