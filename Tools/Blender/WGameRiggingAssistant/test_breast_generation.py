"""Blender background validation of optional breast bone generation."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import bpy
from mathutils import Vector


def main():
    spec = importlib.util.spec_from_file_location('breast_generation_test', Path(__file__).with_name('__init__.py'))
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    a.register()
    try:
        armature = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', armature)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        a._select_only(bpy.context, rig)
        bpy.ops.object.mode_set(mode='EDIT')
        root = armature.edit_bones.new('root.x')
        root.head, root.tail = (0, 0, 0), (0, 0, .1)
        chest = armature.edit_bones.new('upper_chest.x')
        chest.head, chest.tail, chest.parent = (0, 0, 1.2), (0, 0, 1.4), root
        head = armature.edit_bones.new('head.x')
        head.head, head.tail, head.parent = (0, 0, 1.4), (0, 0, 1.7), chest
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh = bpy.data.meshes.new('Body')
        points = [(0, 0, 0), (0, 0, 1.7)] + [(x, y, z) for x in (-.09, -.06, .06, .09)
                 for y in (-.11, -.10, -.09) for z in (1.19, 1.21, 1.23)]
        mesh.from_pydata(points, [], [])
        body = bpy.data.objects.new('Body', mesh)
        bpy.context.collection.objects.link(body)
        a._participant_meshes = lambda *_args: [body]
        a._scope_vertex_ids_by_object = lambda *_args: {body.name: set(range(len(points)))}
        settings = bpy.context.scene.wgame_rigging_assistant
        plan = a._breast_bone_plan(bpy.context, settings, rig)
        before = {b.name: a._bone_snapshot_entry(b) for b in armature.bones}
        broken = dict(plan)
        h, t, _ = broken['breast_02.r']
        broken['breast_02.r'] = (h, t, 'missing_parent')
        try:
            a._create_breast_bones(bpy.context, rig, broken)
            raise AssertionError('invalid parent must fail')
        except KeyError:
            pass
        assert {b.name: a._bone_snapshot_entry(b) for b in armature.bones} == before
        result = a._create_breast_bones(bpy.context, rig, plan)
        assert result['valid'] and result['left'] == ['breast_01.l', 'breast_02.l']
        assert all(a._bone_snapshot_entry(armature.bones[n]) == v for n, v in before.items())
        assert len(armature.bones) == 7
        assert not settings.wgame_skeleton_accepted and not a._base_skeleton_snapshot(rig)
        assert bpy.ops.wgame_rigging.generate_breast_bones() == {'FINISHED'}
        assert len(armature.bones) == 7
        try:
            a._create_breast_bones(bpy.context, rig, plan)
            raise AssertionError('duplicate names must fail')
        except ValueError:
            pass
        snapshot = a._capture_base_skeleton_snapshot(rig)
        assert set(snapshot) == set(before)
        assert a._detect_breast_bone_chains(rig)['valid']
        assert not body.vertex_groups
        print('WG_BREAST_GENERATION_OK geometry_plan chains rollback duplicate_guard no_acceptance no_weights')
    finally:
        a.unregister()


if __name__ == '__main__':
    main()
