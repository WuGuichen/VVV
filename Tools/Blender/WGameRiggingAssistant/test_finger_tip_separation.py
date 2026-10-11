"""Background Blender regression for nearby finger tips and full-rig preservation.

Run --background --factory-startup --python-exit-code 1 --python this_file --
    live-fixture.json result.json [baseline-addon.py]
The fixture contains mesh geometry and complete armature snapshots, not a .blend.
"""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Matrix, Vector


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    return addon


def main():
    assert bpy.app.background, 'Use an isolated background Blender'
    args = sys.argv[sys.argv.index('--') + 1:]
    fixture = json.loads(Path(args[0]).read_text(encoding='utf-8'))
    addon = load(Path(__file__).with_name('__init__.py'), 'finger_separation_test')
    addon.register()
    results = {'version': list(addon.bl_info['version']), 'checks': []}
    try:
        # Two tips 19 mm apart can be different digits via a long palm path.
        points = [Vector(p) for p in [(0, 0, 0), (.019, 0, 0), (0, .08, 0), (.004, 0, 0), (.018, .001, 0)]]
        adjacency = {0: [(2, .08), (3, .004)], 1: [(2, .082)], 2: [(0, .08), (1, .082)], 3: [(0, .004)], 4: []}
        assert addon._separate_hand_tip_candidates(points, adjacency, 0, 1, .0202)
        assert not addon._separate_hand_tip_candidates(points, adjacency, 0, 3, .0202)
        assert not addon._separate_hand_tip_candidates(points, adjacency, 0, 4, .0202)
        results['checks'].append('near_distinct_digits_same_cap_and_disconnected_guards')

        mesh = bpy.data.meshes.new('FingerFixture')
        mesh.from_pydata(fixture['vertices'], fixture['edges'], fixture['faces'])
        obj = bpy.data.objects.new('FingerFixture', mesh)
        bpy.context.collection.objects.link(obj)
        obj.matrix_world = Matrix(fixture['mesh_matrix'])
        armature = bpy.data.armatures.new('rig')
        rig = bpy.data.objects.new('rig', armature)
        bpy.context.collection.objects.link(rig)
        rig['wgame_rig_kind'] = 'deform_skeleton'
        rig.matrix_world = Matrix(fixture['rig_matrix'])
        addon._select_only(bpy.context, rig)
        bpy.ops.object.mode_set(mode='EDIT')
        for data in fixture['bones']:
            bone = armature.edit_bones.new(data['name'])
            bone.head, bone.tail = data['head'], data['tail']
            bone.matrix = Matrix(data['matrix'])
            bone.use_deform = data['deform']
        for data in fixture['bones']:
            bone = armature.edit_bones[data['name']]
            if data['parent']:
                bone.parent = armature.edit_bones[data['parent']]
            bone.use_connect = data['connect']
        bpy.ops.object.mode_set(mode='OBJECT')
        settings = bpy.context.scene.wgame_rigging_assistant
        settings.mesh_name, settings.rig_name = obj.name, rig.name
        settings.wgame_humanoid_mapping_json = fixture['mapping']
        addon._set_vertex_group_mask_vertices(obj, 'WGame.CustomBindMask', fixture['mask'], True)
        geometry = addon._hand_hint_fingerprint(obj)
        for side, raw in fixture['thumb_hints'].items():
            if raw:
                hint = json.loads(raw)
                hint['fingerprint'] = geometry
                obj['wgame_thumb_hint_' + side] = json.dumps(hint)
        hints = {side: obj.get('wgame_thumb_hint_' + side) for side in ('l', 'r')}
        original = {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
        original_mask = addon._mesh_vertex_group_ids(obj, 'WGame.CustomBindMask')
        if len(args) > 2:
            baseline = load(args[2], 'finger_separation_baseline')
            assert len(baseline._hand_geodesic_analysis(obj, rig, 'l')['main_tips']) == 4
            try:
                baseline._hand_geodesic_analysis(obj, rig, 'r')
                raise AssertionError('baseline must reproduce right 3/4 failure')
            except baseline._HandBranchError as error:
                assert 'R 已识别主指 3/4' in str(error)
                results['baseline_error'] = str(error)
            results['checks'].append('baseline_left_pass_right_3_of_4_reproduced')
        results['tips'] = {}
        for side in ('l', 'r'):
            analysis = addon._hand_geodesic_analysis(obj, rig, side)
            assert len(analysis['main_tips']) == 4 and len(set(analysis['tips'])) == 5
            chains, owners, counts = addon._fit_finger_chains(analysis)
            addon._validate_finger_chain_fit(obj, analysis, chains, owners)
            results['tips'][side] = {'tips': analysis['tips'], 'sample_counts': counts}
            crossed = dict(chains)
            crossed['thumb'] = chains['index']
            try:
                addon._validate_finger_chain_fit(obj, analysis, crossed, owners)
                raise AssertionError('cross-finger chain must fail')
            except ValueError:
                pass
        results['checks'].append('both_hands_five_branches_surface_validation_cross_digit_rejection')
        with tempfile.TemporaryDirectory(prefix='finger-full-rig-test-') as temporary:
            # Use the real backup code on an isolated file, never the live user's file.
            bpy.ops.wm.save_as_mainfile(filepath=str(Path(temporary) / 'fixture.blend'))
            addon._select_only(bpy.context, obj)
            bpy.ops.object.mode_set(mode='EDIT')
            assert bpy.ops.wgame_rigging.generate_finger_bones() == {'FINISHED'}
            assert Path(settings.backup_filepath).is_file()
            first = {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
            assert len(first) == len(original) + 30
            assert all(first[name] == data for name, data in original.items())
            assert not addon._finger_structure_errors(rig, settings.wgame_humanoid_mapping_json)
            assert bpy.ops.wgame_rigging.generate_finger_bones() == {'FINISHED'}
            assert first == {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
            results['checks'].append('actual_operator_30_bones_backup_nonfinger_preservation_repeat')
            original_validator = addon._validate_finger_chain_fit
            addon._validate_finger_chain_fit = lambda *_args: (_ for _ in ()).throw(ValueError('injected fit failure'))
            addon._select_only(bpy.context, obj)
            bpy.ops.object.mode_set(mode='EDIT')
            try:
                bpy.ops.wgame_rigging.generate_finger_bones()
            except RuntimeError as error:
                assert 'injected fit failure' in str(error)
            finally:
                addon._validate_finger_chain_fit = original_validator
            assert bpy.context.object == obj and obj.mode == 'EDIT'
            assert first == {b.name: addon._bone_snapshot_entry(b) for b in rig.data.bones}
            bpy.ops.object.mode_set(mode='OBJECT')
            assert addon._hand_hint_fingerprint(obj) == geometry
            assert original_mask == addon._mesh_vertex_group_ids(obj, 'WGame.CustomBindMask')
            assert hints == {side: obj.get('wgame_thumb_hint_' + side) for side in ('l', 'r')}
            results['checks'].append('failure_preserves_bones_edit_context_geometry_mask_and_thumb_hints')
        results['bone_count'] = len(first)
        results['original_bone_count'] = len(original)
        results['completed'] = True
        Path(args[1]).write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
        print('WG_FINGER_SEPARATION_OK', len(original), '+ 30 bones, full surface validation, repeated generation, preservation')
    finally:
        addon.unregister()


if __name__ == '__main__':
    main()
